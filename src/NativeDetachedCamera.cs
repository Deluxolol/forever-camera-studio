using System;
using System.IO;

namespace Machinae.Forever {
    public sealed partial class NativeCamera {
        // VIEW_HOUSE_EXTERIOR accepts an empty focus GUID without rebinding to the
        // player. The camera then retains its pose; the player stays an ordinary unit.
        private const int DetachedMode=10;
        private byte[] detachedFocus,detachedPose,detachedMode;
        private float detachedFov;
        public bool CharacterVisibleCamera {get{return freeCameraEnabled&&detachedFocus!=null;}}

        private void EnableCharacterCamera() {
            CameraSnapshot snapshot=Capture();long player=FindPlayer();
            uint flags=BitConverter.ToUInt32(Read(player+PlayerFlagsOffset,4),0);
            if((flags&0x80480000)!=0)throw new InvalidOperationException("Return to the normal player camera before enabling Show character.");
            byte[] mode=Read(snapshot.Address+0xCC,4);int value=BitConverter.ToInt32(mode,0);
            if(value<0||value>5||BitConverter.ToInt64(Read(snapshot.Address+0x48,8),0)!=0)
                throw new InvalidOperationException("Show character requires the normal player camera, outside a cinematic or editor.");
            // Avoid retaining the first-person fade when the native follow update stops.
            if(Read(snapshot.Address+0x1C4,1)[0]<250)
                throw new InvalidOperationException("Zoom out until your character is fully visible, then enable free camera again.");
            leasedPlayer=player;originalWorldCamera=snapshot.Address;
            detachedPose=Read(snapshot.Address+0x10,48);detachedFov=snapshot.Fov;detachedMode=mode;
            detachedFocus=Read(snapshot.Address+0x98,16);freeCameraEnabled=true;
            try {
                WriteBytes(snapshot.Address+0xCC,BitConverter.GetBytes(DetachedMode));
                WriteBytes(snapshot.Address+0x98,new byte[16]);
            }catch{DisableCharacterCamera();throw;}
        }
        private void ValidateCharacterCamera() {
            if(!freeCameraEnabled||FindPlayer()!=leasedPlayer||FindCamera()!=originalWorldCamera)
                throw new InvalidOperationException("The character or camera changed; detached camera stopped.");
            uint flags=BitConverter.ToUInt32(Read(leasedPlayer+PlayerFlagsOffset,4),0);
            if((flags&0x80480000)!=0||BitConverter.ToInt32(Read(originalWorldCamera+0xCC,4),0)!=DetachedMode)
                throw new InvalidOperationException("The client changed camera mode; detached camera stopped.");
            foreach(byte value in Read(originalWorldCamera+0x98,16))if(value!=0)
                throw new InvalidOperationException("The client changed camera focus; detached camera stopped.");
        }
        private void SetCharacterPose(CameraPoint point) {
            point.Validate();ValidateCharacterCamera();
            double yaw=point.Yaw*Math.PI/180,pitch=point.Pitch*Math.PI/180;
            float cy=(float)Math.Cos(yaw),sy=(float)Math.Sin(yaw),cp=(float)Math.Cos(pitch),sp=(float)Math.Sin(pitch);
            float[] values={point.X,point.Y,point.Z,cp*cy,cp*sy,-sp,-sy,cy,0,sp*cy,sp*sy,cp};
            byte[] pose=new byte[48];Buffer.BlockCopy(values,0,pose,0,pose.Length);
            WriteBytes(originalWorldCamera+0x10,pose);WriteFloat(originalWorldCamera+FovOffset,(float)(point.Fov*Math.PI/180));
        }
        private void DisableCharacterCamera() {
            if(detachedFocus==null)return;
            if(!process.HasExited&&FindPlayer()==leasedPlayer&&FindCamera()==originalWorldCamera) {
                // Do not overwrite a newer client-owned cinematic/editor view.
                bool owned=BitConverter.ToInt32(Read(originalWorldCamera+0xCC,4),0)==DetachedMode;
                foreach(byte value in Read(originalWorldCamera+0x98,16))if(value!=0)owned=false;
                if(owned){
                    WriteBytes(originalWorldCamera+0xCC,detachedMode);
                    WriteBytes(originalWorldCamera+0x98,detachedFocus);
                    WriteBytes(originalWorldCamera+0x10,detachedPose);
                    WriteFloat(originalWorldCamera+FovOffset,detachedFov);
                }
            }
            detachedFocus=detachedPose=detachedMode=null;freeCameraEnabled=false;
        }
    }
}
