using System;
using System.IO;

namespace Machinae.Forever {
    public sealed class ThunderSnapshot {
        public int PendingId, ActiveId, SoundKit, SoundHandle;
        public bool Stopping;
    }
    public sealed partial class NativeCamera {
        private const int ThunderRecordId=134;
        private bool thunderOwned;
        private long thunderObject,thunderScene;
        private int thunderMap,thunderOriginalId;
        private float thunderOriginalInterval;
        public bool ThunderOverridden {get{return thunderOwned;}}

        // Native scene request 1A56090 publishes an ID at weather+1220 after the interval.
        // The ordinary weather update owns all LightningObject allocation, sound and cleanup.
        private void ValidateThunderRecord(){
            long database=moduleBase+0x6BDDCB0;
            long storage=BitConverter.ToInt64(Read(database+0x160,8),0);
            byte[] header=Read(storage,0x90);
            int first=BitConverter.ToInt32(header,0x40),last=BitConverter.ToInt32(header,0x44);
            int count=BitConverter.ToInt32(header,0x48),stride=BitConverter.ToInt32(header,0x38);
            if(first!=133||last!=135||count!=3||stride!=108)throw new InvalidDataException("The native thunder presets do not match this client.");
            long indices=BitConverter.ToInt64(header,0x80);
            int index=Read(indices+ThunderRecordId-first,1)[0];
            if(index>=count)throw new InvalidDataException("The thunder preset is unavailable.");
            long data=BitConverter.ToInt64(header,8);
            byte[] row=Read(data+index*stride,20);
            // These three native SoundKit variants are selected by LightningObject itself.
            if(BitConverter.ToInt32(row,8)!=297299||BitConverter.ToInt32(row,12)!=373387||BitConverter.ToInt32(row,16)!=373243)
                throw new InvalidDataException("The thunder preset has unexpected sound variants.");
        }
        private bool SameThunder(long weather,long scene,int map){return weather==thunderObject&&scene==thunderScene&&map==thunderMap;}
        public ThunderSnapshot CaptureThunder(){
            long scene;int map;long weather=FindWeather(out scene,out map);
            if(thunderOwned&&!SameThunder(weather,scene,map))thunderOwned=false;
            byte[] generator=Read(weather+0x1220,0x38);
            int pending=BitConverter.ToInt32(generator,0),active=BitConverter.ToInt32(generator,0x34);
            long count=BitConverter.ToInt64(generator,0x18);
            if(pending<0||pending>100000||active<0||active>100000||generator[8]>1||count<0||count>64)
                throw new InvalidDataException("The thunder controller does not match this client.");
            var result=new ThunderSnapshot{PendingId=pending,ActiveId=active,Stopping=generator[8]!=0};
            if(count>0){
                long array=BitConverter.ToInt64(generator,0x10);
                long effect=BitConverter.ToInt64(Read(array,8),0);
                byte[] sound=Read(effect+0x90,12);
                result.SoundKit=BitConverter.ToInt32(sound,0);result.SoundHandle=BitConverter.ToInt32(sound,8);
            }
            return result;
        }
        public void SetThunder(bool enabled){
            if(!enabled){RestoreThunder();return;}
            ValidateThunderRecord();
            long scene;int map;long weather=FindWeather(out scene,out map);
            var state=CaptureThunder();
            if(thunderOwned)return;
            if(state.PendingId!=0||state.Stopping)throw new InvalidOperationException("Wait for the current thunder change to finish.");
            thunderOriginalId=state.ActiveId;
            thunderOriginalInterval=BitConverter.ToSingle(Read(weather+0x124C,4),0);
            if(!Finite(thunderOriginalInterval))throw new InvalidDataException("The thunder interval is invalid.");
            thunderObject=weather;thunderScene=scene;thunderMap=map;thunderOwned=true;
            WriteFloat(weather+0x1224,0);
            WriteBytes(weather+0x1220,BitConverter.GetBytes(ThunderRecordId));
        }
        public void RestoreThunder(){
            if(!thunderOwned)return;
            if(!process.HasExited){
                long scene=0;int map=0;long weather=0;try{weather=FindWeather(out scene,out map);}catch{}
                if(SameThunder(weather,scene,map)){
                    // Request native cleanup after the current bolt and sound finish. Never free
                    // client objects or sound handles from another thread.
                    WriteBytes(weather+0x1220,new byte[4]);
                    WriteFloat(weather+0x1224,thunderOriginalInterval);
                    WriteBytes(weather+0x1228,new byte[]{1});
                    if(thunderOriginalId!=0)WriteBytes(weather+0x1220,BitConverter.GetBytes(thunderOriginalId));
                }
            }
            thunderOwned=false;
        }
    }
}
