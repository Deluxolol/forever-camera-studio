using System;
using System.IO;

namespace Machinae.Forever {
    public sealed class LightingSnapshot {
        public uint Sunlight,Ambient;
        public bool NativeOverride;
    }
    public sealed partial class NativeCamera {
        private long lightingLight;
        private int lightingMap;
        private byte[] lightingOriginal;
        private bool lightingOwned;
        public bool LightingOverridden {get{return lightingOwned;}}
        private static readonly int[] LightingOutputs={0x19BC,0x19D4,0x19E0,0x19EC};
        private double LightingGamma(){
            uint flags=BitConverter.ToUInt32(Read(moduleBase+0x4935BDC,4),0);
            double exponent=(flags&8)!=0?BitConverter.ToSingle(Read(moduleBase+0x44D1B74,4),0):1;
            if(double.IsNaN(exponent)||exponent<1||exponent>4)throw new InvalidDataException("The lighting color conversion does not match this client.");
            return exponent;
        }
        private uint ReadLightingColor(long light,int offset,double gamma){
            byte[] bytes=Read(light+offset,12);uint color=0xFF000000;
            for(int i=0;i<3;i++){
                float value=BitConverter.ToSingle(bytes,i*4);
                if(!Finite(value)||value<0||value>100)throw new InvalidDataException("The lighting color fields do not match this client.");
                uint channel=(uint)Math.Round(255*Math.Pow(Math.Min(1,value),1/gamma));
                color|=channel<<(16-i*8);
            }
            return color;
        }
        private bool SameLighting(long light){return light==lightingLight&&BitConverter.ToInt32(Read(light,4),0)==lightingMap;}
        public LightingSnapshot CaptureLighting(){
            long light=FindFogLight();
            if(lightingOwned&&!SameLighting(light))RestoreLighting();
            byte flag=Read(light+0x23F8,1)[0];if(flag>1)throw new InvalidDataException("The native lighting override flag is invalid.");
            double gamma=LightingGamma();
            return new LightingSnapshot{Sunlight=ReadLightingColor(light,0x19BC,gamma),Ambient=ReadLightingColor(light,0x19D4,gamma),NativeOverride=flag!=0};
        }
        public void SetLighting(uint sunlight,uint ambient){
            long light=FindFogLight();var current=CaptureLighting();
            if(lightingOwned&&!SameLighting(light))throw new InvalidOperationException("The current lighting object changed. Apply the colors again.");
            if(!lightingOwned){
                if(current.NativeOverride)throw new InvalidOperationException("A lighting override is already active outside this tool.");
                byte[] direction=Read(light+0x1990,12);double length=0;
                for(int i=0;i<3;i++){float v=BitConverter.ToSingle(direction,i*4);if(!Finite(v)||Math.Abs(v)>1.01)throw new InvalidDataException("The light direction is invalid.");length+=v*v;}
                if(length<0.5||length>1.5)throw new InvalidDataException("The light direction is outside its validated range.");
                lightingOriginal=Read(light+0x23F8,32);lightingLight=light;lightingMap=BitConverter.ToInt32(Read(light,4),0);lightingOwned=true;
                try{WriteBytes(light+0x23FC,direction);}catch{RestoreLighting();throw;}
            }
            try{
                // Native override consumes sRGB packed colors and converts them for rendering.
                // All three ambient hemispheres use the requested fill color.
                WriteBytes(light+0x2408,BitConverter.GetBytes(ambient|0xFF000000));
                WriteBytes(light+0x240C,BitConverter.GetBytes(ambient|0xFF000000));
                WriteBytes(light+0x2410,BitConverter.GetBytes(ambient|0xFF000000));
                WriteBytes(light+0x2414,BitConverter.GetBytes(sunlight|0xFF000000));
                WriteBytes(light+0x23F8,new byte[]{1});
            }catch{RestoreLighting();throw;}
        }
        public void RestoreLighting(){
            if(!lightingOwned)return;
            if(!process.HasExited){
                bool same=false;try{same=SameLighting(FindFogLight());}catch{}
                if(same){
                    WriteBytes(lightingLight+0x23F8,new byte[]{0});
                    var inputs=new byte[28];Buffer.BlockCopy(lightingOriginal,4,inputs,0,28);WriteBytes(lightingLight+0x23FC,inputs);
                    WriteBytes(lightingLight+0x23F8,new[]{lightingOriginal[0]});
                }
            }
            lightingOwned=false;lightingOriginal=null;
        }
    }
}
