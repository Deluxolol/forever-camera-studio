using System;
using System.IO;

namespace Machinae.Forever {
    public sealed class SkySnapshot {
        public uint[] Sky,Clouds;
    }
    public sealed partial class NativeCamera {
        // LightData fields, as consumed by the native day/night interpolation.
        private static readonly int[] SkyFields={5,6,7,8,9,10,12,14,13};
        private sealed class SkyTable {
            internal long Database,Data,Metadata,Storage;
            internal int Count,Stride;
            internal byte[] Layout;
        }
        private SkyTable skyTable;
        private byte[] skyOriginal;
        private bool skyOwned,cloudsOwned;
        public bool SkyColorsOverridden {get{return skyOwned;}}
        public bool CloudColorsOverridden {get{return cloudsOwned;}}
        private SkyTable FindSkyTable(){
            long table=moduleBase+0x6BE0870;
            long database=BitConverter.ToInt64(Read(table+0xB8,8),0);
            byte[] header=Read(database,0x80);
            var state=new SkyTable{Database=database,Count=BitConverter.ToInt32(header,8),Stride=BitConverter.ToInt32(header,12),Data=BitConverter.ToInt64(header,0x18),Metadata=BitConverter.ToInt64(Read(table+0x10,8),0),Storage=BitConverter.ToInt64(Read(table+0x160,8),0)};
            if(BitConverter.ToInt64(header,0)!=moduleBase+0x414E0A0||header[0x28]!=0||state.Count<1||state.Count>50000||state.Stride!=80)
                throw new InvalidDataException("The sky color database layout does not match this client.");
            byte[] storage=Read(state.Storage,0x50);
            if(BitConverter.ToInt64(storage,8)!=state.Data||BitConverter.ToInt32(storage,0x38)!=state.Stride||BitConverter.ToInt32(storage,0x48)!=state.Count)
                throw new InvalidDataException("The sky color records are not available in their validated format.");
            state.Layout=Read(state.Metadata,15*24);
            foreach(int field in SkyFields){
                int offset=field*24;
                if(BitConverter.ToInt32(state.Layout,offset+8)!=5||BitConverter.ToInt32(state.Layout,offset+12)!=87+(field-5)*25||BitConverter.ToInt32(state.Layout,offset+16)!=25)
                    throw new InvalidDataException("The packed sky color fields do not match this client.");
            }
            return state;
        }
        private bool SameSkyTable(SkyTable value){
            if(skyTable==null||skyTable.Database!=value.Database||skyTable.Data!=value.Data||skyTable.Metadata!=value.Metadata||skyTable.Storage!=value.Storage||skyTable.Count!=value.Count||skyTable.Stride!=value.Stride)return false;
            for(int i=0;i<value.Layout.Length;i++)if(skyTable.Layout[i]!=value.Layout[i])return false;
            return true;
        }
        private static int SkyBit(int index){return 87+(SkyFields[index]-5)*25;}
        private static void PutSkyBits(byte[] target,int start,int bit,uint value){
            for(int b=0;b<25;b++){
                int p=start+((bit+b)>>3),mask=1<<((bit+b)&7);
                target[p]=(byte)((target[p]&~mask)|(((value>>b)&1)!=0?mask:0));
            }
        }
        private static uint GetSkyBits(byte[] source,int start,int bit){
            uint value=0;for(int b=0;b<25;b++)if((source[start+((bit+b)>>3)]&(1<<((bit+b)&7)))!=0)value|=1u<<b;return value;
        }
        public SkySnapshot CaptureSky(){
            if(skyTable!=null&&!SameSkyTable(FindSkyTable())){skyOwned=cloudsOwned=false;skyTable=null;skyOriginal=null;}
            long light=FindFogLight();double gamma=LightingGamma();
            var sky=new uint[6];var clouds=new uint[3];
            for(int i=0;i<6;i++)sky[i]=ReadLightingColor(light,0xB50+i*12,gamma);
            for(int i=0;i<3;i++){
                uint packed=BitConverter.ToUInt32(Read(light+0xBCC+i*4,4),0);clouds[i]=0xFF000000;
                for(int channel=0;channel<3;channel++){
                    int shift=16-channel*8;double linear=((packed>>shift)&255)/255.0;
                    clouds[i]|=(uint)Math.Round(255*Math.Pow(linear,1/gamma))<<shift;
                }
            }
            return new SkySnapshot{Sky=sky,Clouds=clouds};
        }
        public void SetSkyColors(uint[] sky,uint[] clouds,bool enableSky,bool enableClouds){
            if(sky==null||sky.Length!=6||clouds==null||clouds.Length!=3)throw new ArgumentException("Choose six sky colors and three cloud colors.");
            FindFogLight();SkyTable state=FindSkyTable();
            if(skyTable!=null&&!SameSkyTable(state)){
                skyOwned=cloudsOwned=false;skyTable=null;skyOriginal=null;
                throw new InvalidOperationException("The sky color database changed. Apply the colors again.");
            }
            if(!enableSky&&!enableClouds&&!skyOwned&&!cloudsOwned)return;
            byte[] current=Read(state.Data,checked(state.Count*state.Stride));
            if(skyTable==null){skyTable=state;skyOriginal=(byte[])current.Clone();}
            bool oldSky=skyOwned,oldClouds=cloudsOwned;
            // Newly acquired fields get a fresh snapshot; unrelated record bits are preserved.
            for(int row=0;row<state.Count;row++)for(int i=0;i<9;i++){
                bool old=i<6?oldSky:oldClouds,enabled=i<6?enableSky:enableClouds;
                if(enabled&&!old)PutSkyBits(skyOriginal,row*state.Stride,SkyBit(i),GetSkyBits(current,row*state.Stride,SkyBit(i)));
                if(enabled)PutSkyBits(current,row*state.Stride,SkyBit(i),(i<6?sky[i]:clouds[i-6])&0xFFFFFF);
                else if(old)PutSkyBits(current,row*state.Stride,SkyBit(i),GetSkyBits(skyOriginal,row*state.Stride,SkyBit(i)));
            }
            skyOwned=oldSky||enableSky;cloudsOwned=oldClouds||enableClouds;
            try{
                WriteSkyRows(state,current);
                skyOwned=enableSky;cloudsOwned=enableClouds;
                if(!skyOwned&&!cloudsOwned){skyTable=null;skyOriginal=null;}
            }catch{RestoreSkyColors();throw;}
        }
        private void WriteSkyRows(SkyTable state,byte[] data){
            if(!SameSkyTable(FindSkyTable()))throw new InvalidOperationException("The sky color database changed before writing.");
            // Only the color span (bits 87..336); boundary bits retain their current values.
            var span=new byte[33];
            for(int row=0;row<state.Count;row++){
                Buffer.BlockCopy(data,row*state.Stride+10,span,0,33);
                WriteBytes(state.Data+row*state.Stride+10,span);
            }
        }
        public void RestoreSkyColors(){
            if(skyTable==null)return;
            if(!process.HasExited){
                SkyTable current=null;try{current=FindSkyTable();}catch{}
                if(current!=null&&SameSkyTable(current)){
                    byte[] records=Read(current.Data,checked(current.Count*current.Stride));
                    for(int row=0;row<current.Count;row++)for(int i=0;i<9;i++)if(i<6?skyOwned:cloudsOwned)
                        PutSkyBits(records,row*current.Stride,SkyBit(i),GetSkyBits(skyOriginal,row*current.Stride,SkyBit(i)));
                    WriteSkyRows(current,records);
                }
            }
            skyOwned=cloudsOwned=false;skyTable=null;skyOriginal=null;
        }
    }
}
