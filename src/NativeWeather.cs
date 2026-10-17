using System;
using System.IO;
using System.Threading;

namespace Machinae.Forever {
    public sealed class WeatherSnapshot {
        public int Kind,ActiveKind,NativeKind,RecordId;
        public float Intensity;
        public bool Pending;
    }
    public sealed partial class NativeCamera {
        private sealed class WeatherRequest {
            internal byte Type,Kind;
            internal float Intensity,Scale;
            internal long Record;
            internal int RecordId;
        }
        private bool weatherOwned;
        private long weatherObject,weatherScene;
        private int weatherMap;
        private WeatherRequest weatherOriginal;
        private byte[] weatherOriginalQueue;
        public bool WeatherOverridden {get{return weatherOwned;}}
        private long FindWeather(out long scene,out int map){
            long light=FindFogLight();map=BitConverter.ToInt32(Read(light,4),0);
            long frame=BitConverter.ToInt64(Read(moduleBase+WorldFrameRva,8),0);
            scene=BitConverter.ToInt64(Read(frame+0x288,8),0);
            long weather=BitConverter.ToInt64(Read(scene+0x40,8),0);
            if(BitConverter.ToInt64(Read(weather+0x1148,8),0)!=scene)throw new InvalidDataException("The weather controller does not belong to the current scene.");
            byte[] state=Read(weather+0x11C8,2);
            if(state[0]>1||state[1]>1)throw new InvalidDataException("The weather request queue does not match this client.");
            return weather;
        }
        private bool SameWeather(long value,long scene,int map){return value==weatherObject&&scene==weatherScene&&map==weatherMap;}
        private WeatherRequest ReadWeatherRequest(long weather){
            var state=new WeatherRequest{Type=Read(weather+0x1140,1)[0],Kind=Read(weather+0x1174,1)[0],Intensity=BitConverter.ToSingle(Read(weather+0x1154,4),0),Scale=BitConverter.ToSingle(Read(weather+0x1184,4),0),Record=BitConverter.ToInt64(Read(weather+0x1288,8),0),RecordId=BitConverter.ToInt32(Read(weather+0x1290,4),0)};
            if(state.Kind>4||!Finite(state.Intensity)||state.Intensity<0||state.Intensity>1||!Finite(state.Scale)||state.Scale<0||state.Scale>100||(state.Record!=0&&(state.Record<0x10000||state.Record>0x00007FFFFFFFFFFF)))throw new InvalidDataException("The weather settings do not match this client.");
            return state;
        }
        public WeatherSnapshot CaptureWeather(){
            long scene;int map;long weather=FindWeather(out scene,out map);
            if(weatherOwned&&!SameWeather(weather,scene,map)){weatherOwned=false;weatherOriginal=null;weatherOriginalQueue=null;}
            var state=ReadWeatherRequest(weather);int active=0;
            if(BitConverter.ToInt64(Read(weather+0x11D8,8),0)!=0)active=1;
            else if(BitConverter.ToInt64(Read(weather+0x11E0,8),0)!=0)active=2;
            else if(BitConverter.ToInt64(Read(weather+0x11E8,8),0)!=0)active=3;
            // Current-client presets use particulate emitters with legacy Kind=0.
            // Their precipitation type lives in Type and must also drive the UI readback.
            int kind=state.Kind;
            if(state.Kind==0&&state.Record!=0){
                // A legacy/clear request can leave Type at the preceding preset's
                // value. Read the actual record type so restoration stays truthful.
                int recordType=Read(state.Record,1)[0]&7;
                if(recordType==1||recordType==2)kind=recordType;
                long emitters=BitConverter.ToInt64(Read(weather+0x58,8),0);
                if(emitters>0&&emitters<=128)active=kind;
            }
            byte[] queue=Read(weather+0x11C8,2);
            return new WeatherSnapshot{Kind=kind,NativeKind=state.Kind,RecordId=state.RecordId,ActiveKind=active,Intensity=state.Intensity,Pending=queue[0]!=0||queue[1]!=0};
        }
        private WeatherRequest SnowRequest(float intensity){
            const int id=628;
            long database=moduleBase+0x6BDE7A0;
            long storage=BitConverter.ToInt64(Read(database+0x160,8),0);
            byte[] header=Read(storage,0xC0);
            int first=BitConverter.ToInt32(header,0x40),last=BitConverter.ToInt32(header,0x44);
            int stride=BitConverter.ToInt32(header,0x38),count=BitConverter.ToInt32(header,0x48);
            int entries=BitConverter.ToInt32(header,0xB8);
            if(first!=1||last!=632||stride!=26||count!=74||entries<1||entries>4096)
                throw new InvalidDataException("The snowfall presets do not match this client.");
            // Match the native sparse ID index, rather than assuming an ordinal row.
            long index=BitConverter.ToInt64(header,0xB0);
            byte[] ids=Read(index,entries*4);
            int row=-1;
            for(int i=0;i<entries;i++)if(BitConverter.ToUInt16(ids,i*4)+first==id){row=ids[i*4+2];break;}
            if(row<0||row>=count)throw new InvalidDataException("The native snowfall preset is unavailable.");
            long data=BitConverter.ToInt64(header,8),record=data+row*stride;
            byte[] values=Read(record,stride);
            long metadata=BitConverter.ToInt64(Read(database+0x10,8),0);
            byte[] type=Read(metadata,24),legacy=Read(metadata+5*24,24);
            if(BitConverter.ToInt32(type,8)!=1||BitConverter.ToInt32(type,12)!=0||BitConverter.ToInt32(type,16)!=3||
               BitConverter.ToInt32(legacy,8)!=1||BitConverter.ToInt32(legacy,12)!=62||BitConverter.ToInt32(legacy,16)!=1||
               (values[0]&7)!=2||(values[7]&0x40)!=0)
                throw new InvalidDataException("The native preset is not compatible with snowfall.");
            return new WeatherRequest{Type=2,Kind=0,Intensity=intensity,Scale=1,Record=record,RecordId=id};
        }
        private void QueueWeather(long weather,WeatherRequest request){
            byte[] flags=Read(weather+0x11C8,2);
            if(flags[0]!=0||flags[1]!=0)throw new InvalidOperationException("The client is processing a weather change. Try again shortly.");
            byte[] input=Read(weather+0x11A8,28);
            input[0]=request.Type;input[1]=request.Kind;
            Buffer.BlockCopy(BitConverter.GetBytes(request.Intensity),0,input,4,4);
            Buffer.BlockCopy(BitConverter.GetBytes(request.Record),0,input,8,8);
            Buffer.BlockCopy(BitConverter.GetBytes(request.RecordId),0,input,16,4);
            input[20]=0; // Immediate intensity, using the normal native weather update.
            Buffer.BlockCopy(BitConverter.GetBytes(request.Scale),0,input,24,4);
            WriteBytes(weather+0x11A8,input);
            WriteBytes(weather+0x11C8,new byte[]{1}); // Publish only after all fields are ready.
        }
        public void SetWeather(int kind,float intensity){
            if((kind!=1&&kind!=2)||!Finite(intensity)||intensity<0||intensity>1)throw new ArgumentOutOfRangeException("Choose rain or snow and an intensity between 0 and 100%.");
            WeatherRequest request=kind==2?SnowRequest(intensity):new WeatherRequest{Type=1,Kind=1,Intensity=intensity,Scale=1};
            long scene;int map;long weather=FindWeather(out scene,out map);
            if(weatherOwned&&!SameWeather(weather,scene,map)){weatherOwned=false;weatherOriginal=null;weatherOriginalQueue=null;}
            if(!weatherOwned){
                byte[] flags=Read(weather+0x11C8,2);if(flags[0]!=0||flags[1]!=0)throw new InvalidOperationException("Wait for the current weather change to finish.");
                weatherOriginal=ReadWeatherRequest(weather);weatherOriginalQueue=Read(weather+0x11A8,33);
                weatherObject=weather;weatherScene=scene;weatherMap=map;weatherOwned=true;
            }
            // Rain retains the working legacy effect. Snow uses the native snowfall
            // preset and its particulate emitters, rather than the legacy haze layer.
            QueueWeather(weather,request);
        }
        public void RestoreWeather(){
            RestoreThunder();
            if(!weatherOwned)return;
            if(!process.HasExited){
                long scene=0;int map=0;long weather=0;try{weather=FindWeather(out scene,out map);}catch{}
                if(SameWeather(weather,scene,map)){
                    WaitWeatherQueue(weather);
                    QueueWeather(weather,weatherOriginal);
                    WaitWeatherQueue(weather);
                    WriteBytes(weather+0x11A8,weatherOriginalQueue);
                }
            }
            weatherOwned=false;weatherOriginal=null;weatherOriginalQueue=null;
        }
        private void WaitWeatherQueue(long weather){
            for(int i=0;i<150;i++){
                byte[] flags=Read(weather+0x11C8,2);
                if(flags[0]==0&&flags[1]==0)return;
                Thread.Sleep(10);
            }
            throw new InvalidOperationException("Waiting for the game to finish restoring weather. Keep the character in the world and try again.");
        }
    }
}

