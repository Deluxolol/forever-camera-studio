using System;
using System.IO;
using System.Text;

namespace Machinae.Forever
{
    public sealed class FogSnapshot {
        public float Start,End,Limit;
        public float Density;
        public bool NativeOverride;
    }
    public sealed partial class NativeCamera
    {
        private bool fogOwned;
        private bool fogSuppressed;
        private long suppressedLight;
        private int suppressedMap;
        private float[] originalFogStrength;
        private static readonly int[] FogStrengthOffsets={0x104,0x27C,0x330,0x3E0,0x490,0x540};
        public bool FogSuppressed {get{return fogSuppressed;}}
        public float[] ReadFogStrengths() {
            long light=FindFogLight();var values=new float[FogStrengthOffsets.Length];
            for(int i=0;i<values.Length;i++){
                values[i]=BitConverter.ToSingle(Read(light+FogStrengthOffsets[i],4),0);
                if(!Finite(values[i])||values[i]<0||values[i]>1)throw new InvalidDataException("The fog strength fields do not match this client.");
            }
            return values;
        }
        public void SetFogSuppressed(bool enabled) {
            if(!enabled){RestoreFogStrength();return;}
            long light=FindFogLight();ReadFogAt(light);
            if(fogSuppressed){if(light!=suppressedLight)throw new InvalidOperationException("The lighting object changed. Restore fog before applying again.");return;}
            originalFogStrength=ReadFogStrengths();suppressedLight=light;suppressedMap=BitConverter.ToInt32(Read(light,4),0);fogSuppressed=true;
            try{foreach(int offset in FogStrengthOffsets)WriteFloat(light+offset,0);}
            catch{RestoreFogStrength();throw;}
        }
        private void RestoreFogStrength() {
            if(!fogSuppressed)return;
            if(!process.HasExited){
                bool same=false;try{same=FindFogLight()==suppressedLight;}catch{}
                if(same)for(int i=0;i<FogStrengthOffsets.Length;i++)WriteFloat(suppressedLight+FogStrengthOffsets[i],originalFogStrength[i]);
            }
            fogSuppressed=false;originalFogStrength=null;
        }
        private long fogLight,fogCvar;
        private int fogMap;
        private byte[] fogOriginalInputs;
        private float originalHorizon,originalDisableHorizon;
        private static readonly int[] FogInputOffsets={0x212C,0x2130,0x2134,0x2138,0x213C,0x2140,0x2144,0x2148,0x214C,0x2150,0x2154,0x2158,0x2160,0x2164,0x2168};
        private static readonly int[] FogOutputOffsets={0xFC,0x100,0x13C,0x108,0xF8,0x130,0x10C,0x110,0x11C,0x120,0x124,0x128,0x114,0x138,0x134};
        public bool FogOverridden {get{return fogOwned;}}

        private long FindFogLight() {
            if(!supportsPoseAngles)throw new NotSupportedException("Fog controls require build 70450.");
            FindPlayer();FindCamera();
            long frame=BitConverter.ToInt64(Read(moduleBase+WorldFrameRva,8),0);
            long scene=BitConverter.ToInt64(Read(frame+0x288,8),0);
            long holder=BitConverter.ToInt64(Read(scene+0x30,8),0);
            long light=BitConverter.ToInt64(Read(holder,8),0);
            int map=BitConverter.ToInt32(Read(light,4),0);
            if(map<0||map>100000)throw new InvalidDataException("The lighting object does not match this client.");
            return light;
        }
        private FogSnapshot ReadFogAt(long light) {
            byte[] data=Read(light+0xF8,16);
            float near=BitConverter.ToSingle(data,4),far=BitConverter.ToSingle(data,8);
            float limit=BitConverter.ToSingle(Read(light+0x44,4),0);
            byte active=Read(light+0x2128,1)[0];
            if(!Finite(near)||Math.Abs(near)>100000||!Finite(far)||far<=0||far>50000||!Finite(limit)||limit<=0||limit>50000||active>1)
                throw new InvalidDataException("The fog fields do not match the validated layout.");
            return new FogSnapshot{Start=near,End=far,Limit=limit,Density=BitConverter.ToSingle(data,12),NativeOverride=active!=0};
        }
        public FogSnapshot CaptureFog() {
            long light=FindFogLight();
            if(fogSuppressed&&(light!=suppressedLight||BitConverter.ToInt32(Read(light,4),0)!=suppressedMap))RestoreFogStrength();
            if(fogOwned&&(light!=fogLight||BitConverter.ToInt32(Read(light,4),0)!=fogMap))RestoreFog();
            return ReadFogAt(light);
        }
        private long FindHorizonSwitch() {
            long cvar=BitConverter.ToInt64(Read(moduleBase+0x7092698,8),0);
            byte[] name=Read(cvar+0x70,16);
            long pointer=BitConverter.ToInt64(name,0),length=BitConverter.ToInt64(name,8);
            if(length!=19||Encoding.ASCII.GetString(Read(pointer,19))!="disableHorizonStart")throw new InvalidDataException("The horizon setting does not match this client.");
            return cvar;
        }
        public void SetFogDistance(float distance) {
            if(!Finite(distance)||distance<100||distance>50000)throw new ArgumentOutOfRangeException(nameof(distance),"Fog distance must be between 100 and 50000.");
            long light=FindFogLight();
            FogSnapshot current=ReadFogAt(light);
            if(fogOwned) {
                if(light!=fogLight||BitConverter.ToInt32(Read(light,4),0)!=fogMap||!current.NativeOverride)throw new InvalidOperationException("The lighting object changed. Restore fog before applying again.");
                WriteFloat(light+0x2130,distance);WriteFloat(light+0x2134,distance);return;
            }
            if(current.NativeOverride)throw new InvalidOperationException("A fog override is already active outside this tool.");
            long cvar=FindHorizonSwitch();
            float horizon=BitConverter.ToSingle(Read(moduleBase+0x4936B18,4),0),disabled=BitConverter.ToSingle(Read(cvar+0x10,4),0);
            if(!Finite(horizon)||horizon<0||horizon>50000||!Finite(disabled)||disabled<0||disabled>1)throw new InvalidDataException("The horizon fields do not match this client.");
            fogOriginalInputs=Read(light+0x2128,0x44);fogLight=light;fogMap=BitConverter.ToInt32(Read(light,4),0);fogCvar=cvar;originalHorizon=horizon;originalDisableHorizon=disabled;fogOwned=true;
            try {
                // Seed the client's override with its current colors and secondary fog parameters.
                // Only the requested start/end range changes. The client updates render data itself.
                byte[] outputs=Read(light,0x1A8);
                for(int i=0;i<FogInputOffsets.Length;i++) {
                    byte[] value=new byte[4];Buffer.BlockCopy(outputs,FogOutputOffsets[i],value,0,4);WriteBytes(light+FogInputOffsets[i],value);
                }
                WriteBytes(light+0x215C,new byte[]{1});
                // The renderer reads the float cache of this graphics setting. Its stored string
                // and saved configuration remain unchanged; restore this exact cache on exit.
                WriteFloat(cvar+0x10,1);
                WriteFloat(moduleBase+0x4936B18,50000);
                WriteFloat(light+0x212C,0);
                WriteFloat(light+0x2130,distance);WriteFloat(light+0x2134,distance);
                WriteBytes(light+0x2128,new byte[]{1});
            } catch {RestoreFog();throw;}
        }
        public void RestoreFog() {
            RestoreFogStrength();
            if(!fogOwned)return;
            if(!process.HasExited) {
                bool same=false;
                try{same=FindFogLight()==fogLight;}catch{}
                if(same) {
                    WriteBytes(fogLight+0x2128,new byte[]{fogOriginalInputs[0]});
                    foreach(int offset in FogInputOffsets) {
                        byte[] value=new byte[4];Buffer.BlockCopy(fogOriginalInputs,offset-0x2128,value,0,4);WriteBytes(fogLight+offset,value);
                    }
                    WriteBytes(fogLight+0x215C,new byte[]{fogOriginalInputs[0x34]});
                }
                if(FindHorizonSwitch()==fogCvar)WriteFloat(fogCvar+0x10,originalDisableHorizon);
                WriteFloat(moduleBase+0x4936B18,originalHorizon);
            }
            fogOwned=false;fogOriginalInputs=null;
        }
    }
}
