using System;
using System.IO;


namespace Machinae.Forever {
    public sealed partial class NativeCamera {
        public static readonly uint[] RenderBits={1,0x80,2,0x20,0x200,0x100};
        // Main scene passes: model / model without reflections; WMO / WMO refraction.
        // The legacy mask alone affects shadow submission in this client's main rendering path.
        private static readonly uint[] MainRenderBits={0x410000,0,0,0x220000,0,0};
        private uint renderOriginal,renderOwnedBits;
        private uint renderFrameOriginal,renderFrameOwnedBits;
        private long renderFrame,renderScene;
        private bool timeOwned;
        private byte[] timeOriginal;
        public bool WorldTimeOverridden {get{return timeOwned;}}
        private void ValidateVisualClient(){if(!supportsPoseAngles)throw new NotSupportedException("Visual controls require build 70450.");FindPlayer();FindCamera();}
        private long FindRenderFrame(out long scene){
            ValidateVisualClient();
            long frame=BitConverter.ToInt64(Read(moduleBase+WorldFrameRva,8),0);
            scene=BitConverter.ToInt64(Read(frame+0x288,8),0);
            long state=BitConverter.ToInt64(Read(scene+0x78,8),0);
            // The primary render frame links back to itself at +0x40.
            if(BitConverter.ToInt64(Read(state+0x40,8),0)!=state)throw new InvalidDataException("The primary render-frame layout does not match this client.");
            return state;
        }
        public uint ReadRendering(){
            long scene;long state=FindRenderFrame(out scene);
            if(renderFrameOwnedBits!=0&&(state!=renderFrame||scene!=renderScene))RestoreRendering();
            uint legacy=BitConverter.ToUInt32(Read(moduleBase+0x5059B40,4),0);
            uint main=BitConverter.ToUInt32(Read(state,4),0);
            foreach(int index in new[]{0,3})if((main&MainRenderBits[index])==0)legacy&=~RenderBits[index];
            return legacy;
        }
        public void SetRendering(int index,bool enabled){
            if(index<0||index>=RenderBits.Length)throw new ArgumentOutOfRangeException(nameof(index));
            ReadRendering();uint current=BitConverter.ToUInt32(Read(moduleBase+0x5059B40,4),0),bit=RenderBits[index];
            if(renderOwnedBits==0)renderOriginal=current;
            renderOwnedBits|=bit;
            try{
                if(MainRenderBits[index]!=0){
                    long scene;long state=FindRenderFrame(out scene);uint main=BitConverter.ToUInt32(Read(state,4),0);
                    uint acquired=MainRenderBits[index]&~renderFrameOwnedBits;
                    renderFrameOriginal=(renderFrameOriginal&~acquired)|(main&acquired);
                    renderFrame=state;renderScene=scene;renderFrameOwnedBits|=MainRenderBits[index];
                    WriteBytes(state,BitConverter.GetBytes(enabled?main|MainRenderBits[index]:main&~MainRenderBits[index]));
                }
                WriteBytes(moduleBase+0x5059B40,BitConverter.GetBytes(enabled?current|bit:current&~bit));
            }catch{RestoreRendering();throw;}
        }
        public void RestoreRendering(){
            if(renderOwnedBits==0)return;
            if(!process.HasExited&&renderFrameOwnedBits!=0){
                long state=0,scene=0;try{state=FindRenderFrame(out scene);}catch{}
                if(state==renderFrame&&scene==renderScene){uint main=BitConverter.ToUInt32(Read(state,4),0);WriteBytes(state,BitConverter.GetBytes((main&~renderFrameOwnedBits)|(renderFrameOriginal&renderFrameOwnedBits)));}
            }
            if(!process.HasExited){uint current=BitConverter.ToUInt32(Read(moduleBase+0x5059B40,4),0);WriteBytes(moduleBase+0x5059B40,BitConverter.GetBytes((current&~renderOwnedBits)|(renderOriginal&renderOwnedBits)));}
            renderOwnedBits=renderFrameOwnedBits=0;renderFrame=renderScene=0;
        }
        public float ReadWorldTime(){
            ValidateVisualClient();long light=FindFogLight();float fraction=BitConverter.ToSingle(Read(light+0xC,4),0);
            if(!Finite(fraction)||fraction<0||fraction>1)throw new InvalidDataException("The world lighting time does not match this client.");
            return fraction*1440;
        }
        public void SetWorldTime(float minutes){
            ValidateVisualClient();if(!Finite(minutes)||minutes<0||minutes>=1440)throw new ArgumentOutOfRangeException(nameof(minutes));
            byte[] state=Read(moduleBase+0x505DB98,8);int active=BitConverter.ToInt32(state,0);float fraction=BitConverter.ToSingle(state,4);
            if(active<0||active>1||!Finite(fraction)||fraction<0||fraction>1)throw new InvalidDataException("The native time override does not match this client.");
            if(!timeOwned){timeOriginal=state;timeOwned=true;}
            WriteFloat(moduleBase+0x505DB9C,minutes/1440);
            WriteBytes(moduleBase+0x505DB98,BitConverter.GetBytes(1));
        }
        public void RestoreWorldTime(){
            if(!timeOwned)return;
            if(!process.HasExited){WriteBytes(moduleBase+0x505DB98,new byte[4]);WriteBytes(moduleBase+0x505DB9C,new[]{timeOriginal[4],timeOriginal[5],timeOriginal[6],timeOriginal[7]});WriteBytes(moduleBase+0x505DB98,new[]{timeOriginal[0],timeOriginal[1],timeOriginal[2],timeOriginal[3]});}
            timeOwned=false;timeOriginal=null;
        }
        public void RestoreVisuals(){RestoreWeather();RestoreSkyColors();RestoreLighting();RestoreWorldTime();RestoreRendering();}
    }
}

