using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace Machinae.Forever
{
    public class CameraPoint
    {
        public float X {get;set;}
        public float Y {get;set;}
        public float Z {get;set;}
        public float Fov {get;set;} = 90;
        public float Yaw {get;set;}
        public float Pitch {get;set;}
        public CameraPoint Copy() {return (CameraPoint)MemberwiseClone();}
        public static float FovDegrees(float radians) {
            double degrees=radians*180/Math.PI;
            if(Math.Abs(degrees-20)<0.0001)return 20;
            if(Math.Abs(degrees-140)<0.0001)return 140;
            return (float)degrees;
        }
        public static CameraPoint FromSnapshot(CameraSnapshot value) {
            return new CameraPoint {X=value.X,Y=value.Y,Z=value.Z,Fov=FovDegrees(value.Fov),Yaw=(float)(value.Yaw*180/Math.PI),Pitch=(float)(value.Pitch*180/Math.PI)};
        }
        public void Validate() {
            foreach(float value in new[]{X,Y,Z,Fov,Yaw,Pitch}) if(float.IsNaN(value)||float.IsInfinity(value)) throw new InvalidDataException("A waypoint contains an invalid value.");
            if(Math.Abs(X)>100000||Math.Abs(Y)>100000||Math.Abs(Z)>100000||Fov<20||Fov>140||Math.Abs(Pitch)>90||Math.Abs(Yaw)>360000)
                throw new InvalidDataException("A waypoint is outside the supported range.");
        }
    }

    public class CameraProject
    {
        public int Schema {get;set;} = 1;
        public float Speed {get;set;} = 10;
        public float Fov {get;set;} = 90;
        public bool Collision {get;set;} = true;
        public bool ShowCharacter {get;set;}
        public float ViewDistance {get;set;} = 7000;
        public float FogDistance {get;set;} = 20000;
        public bool AlwaysOnTop {get;set;}
        public float PanX {get;set;}
        public float PanY {get;set;}
        public float PanZ {get;set;}
        public float Rotation {get;set;}
        public float Duration {get;set;} = 20;
        public bool Loop {get;set;}
        public bool Bezier {get;set;} = true;
        public List<CameraPoint> Points {get;set;} = new List<CameraPoint>();
        public void Validate() {
            if(Schema!=1) throw new InvalidDataException("This project format is not supported.");
            foreach(float value in new[]{Speed,Fov,PanX,PanY,PanZ,Rotation,Duration,ViewDistance}) if(float.IsNaN(value)||float.IsInfinity(value)) throw new InvalidDataException("The project contains invalid numbers.");
            if(ViewDistance<3000||ViewDistance>50000)throw new InvalidDataException("View distance is outside the supported range.");
            if(float.IsNaN(FogDistance)||float.IsInfinity(FogDistance)||FogDistance<100||FogDistance>50000)throw new InvalidDataException("Fog distance must be between 100 and 50000.");
            if(Speed<0.1||Speed>500||Fov<20||Fov>140||Duration<1||Duration>3600||Math.Abs(PanX)>500||Math.Abs(PanY)>500||Math.Abs(PanZ)>500||Math.Abs(Rotation)>180)
                throw new InvalidDataException("Project settings are outside the supported range.");
            if(Points==null||Points.Count>128) throw new InvalidDataException("A route can contain up to 128 waypoints.");
            foreach(CameraPoint point in Points) {if(point==null) throw new InvalidDataException("Empty waypoint.");point.Validate();}
        }
        public void Save(string path) {
            Validate();
            // Serialize fully before touching the destination; replace only after a complete write.
            var serializer=new XmlSerializer(typeof(CameraProject));
            string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try {
                using(var writer=XmlWriter.Create(temporary,new XmlWriterSettings {Indent=true})) serializer.Serialize(writer,this);
                if(File.Exists(path)) File.Replace(temporary,path,null); else File.Move(temporary,path);
            } finally {if(File.Exists(temporary)) File.Delete(temporary);}
        }
        public static CameraProject Load(string path) {
            if(new FileInfo(path).Length>2*1024*1024) throw new InvalidDataException("The project file is too large.");
            using(var reader=XmlReader.Create(path,new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=2*1024*1024})) {
                var result=(CameraProject)new XmlSerializer(typeof(CameraProject)).Deserialize(reader);
                result.Validate();return result;
            }
        }
    }

    public sealed class CameraMotion
    {
        private List<CameraPoint> points;
        private double elapsed;
        public double Duration {get;private set;}
        public bool Loop {get;private set;}
        public bool Bezier {get;private set;}
        public bool Playing {get;private set;}
        public bool AutoEnabled {get;set;}
        public float PanX,PanY,PanZ,Rotation;
        public double Progress {get {return Duration>0?Math.Min(1,elapsed/Duration):0;}}
        public void Start(IList<CameraPoint> route,double duration,bool loop,bool bezier) {
            if(route==null||route.Count<2||route.Count>128||double.IsNaN(duration)||double.IsInfinity(duration)||duration<1||duration>3600) throw new ArgumentException("Choose at least two waypoints and a valid duration.");
            var copy=new List<CameraPoint>();
            foreach(CameraPoint point in route) {point.Validate();copy.Add(point.Copy());}
            // Unwrap yaw before interpolation, so 359 -> 1 follows a two-degree turn.
            for(int i=1;i<copy.Count;i++) copy[i].Yaw=copy[i-1].Yaw+ShortestAngle(copy[i].Yaw-copy[i-1].Yaw);
            points=copy;Duration=duration;Loop=loop;Bezier=bezier;elapsed=0;Playing=true;AutoEnabled=false;
        }
        public void Stop() {Playing=false;AutoEnabled=false;}
        public void StopPath() {Playing=false;}
        public static float ShortestAngle(float angle) {return (float)((angle+180)%360+360)%360-180;}
        private static CameraPoint Blend(CameraPoint a,CameraPoint b,double t) {
            return new CameraPoint {X=(float)(a.X+(b.X-a.X)*t),Y=(float)(a.Y+(b.Y-a.Y)*t),Z=(float)(a.Z+(b.Z-a.Z)*t),Fov=(float)(a.Fov+(b.Fov-a.Fov)*t),Yaw=(float)(a.Yaw+(b.Yaw-a.Yaw)*t),Pitch=(float)(a.Pitch+(b.Pitch-a.Pitch)*t)};
        }
        private CameraPoint Evaluate(double t) {
            if(!Bezier) {
                double segment=t*(points.Count-1);int index=Math.Min(points.Count-2,(int)segment);
                return Blend(points[index],points[index+1],segment-index);
            }
            var work=new CameraPoint[points.Count];
            for(int i=0;i<work.Length;i++) work[i]=points[i].Copy();
            for(int length=work.Length-1;length>0;length--) for(int i=0;i<length;i++) work[i]=Blend(work[i],work[i+1],t);
            return work[0];
        }
        public CameraPoint Advance(double seconds,CameraPoint current) {
            if(double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if(Playing) {
                elapsed+=seconds;
                if(Loop) elapsed%=Duration;
                double t=Math.Min(1,elapsed/Duration);
                CameraPoint value=Evaluate(t);value.Yaw=ShortestAngle(value.Yaw);
                if(t>=1&&!Loop) Playing=false;
                return value;
            }
            if(!AutoEnabled) return null;
            current.Validate();
            foreach(float rate in new[]{PanX,PanY,PanZ,Rotation}) if(float.IsNaN(rate)||float.IsInfinity(rate)) throw new ArgumentException("Invalid automatic movement.");
            double delta=Math.Min(seconds,0.1);
            var result=current.Copy();
            result.X+=(float)(PanX*delta);result.Y+=(float)(PanY*delta);result.Z+=(float)(PanZ*delta);
            result.Yaw=ShortestAngle(result.Yaw+(float)(Rotation*delta));result.Validate();
            return result;
        }
    }
}
