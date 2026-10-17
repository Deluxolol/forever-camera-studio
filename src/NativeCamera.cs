using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Machinae.Forever
{
    public sealed class CameraSnapshot
    {
        public long Address;
        public float X, Y, Z, Fov;
        public float ZoomTarget, ZoomCurrent;
        public float Yaw, Pitch;
    }

    public sealed partial class NativeCamera : IDisposable
    {
        public const int SupportedBuild = 70450;
        private readonly long WorldFrameRva;
        private readonly int CameraFieldOffset;
        private readonly long EncodedPlayerRva, KeyObjectRva, PlayerVtableRva;
        private const int FovOffset = 0x40;
        private const int TargetZoomOffset = 0x274;
        private const int CurrentZoomOffset = 0x278;
        private readonly Process process;
        private readonly long moduleBase;
        private readonly IntPtr readHandle;
        private IntPtr writeHandle;
        private long overrideAddress;
        private float originalFov;
        private bool hasOverride;
        private readonly bool supportsFreeCamera;
        private readonly long SpectatorRva;
        private readonly int PlayerFlagsOffset;
        private const uint FreeCameraMask = 0x400000;
        private bool freeCameraEnabled;
        private long leasedPlayer;
        private byte[] originalSpectator;
        private float originalWorldFov;
        private long originalWorldCamera;
        private byte[] originalAngles;
        private bool changedAngles;
        private readonly bool supportsPoseAngles;
        private bool disposed;
        private bool viewOverride;
        private float originalViewDistance;
        public bool ViewDistanceOverridden {get{return viewOverride;}}
        public float ReadViewDistance() {
            if(!supportsPoseAngles)throw new NotSupportedException("Environment controls require build 70450.");
            FindPlayer();FindCamera();
            float value=BitConverter.ToSingle(Read(moduleBase+0x4936DA8,4),0);
            if(!Finite(value)||value<3000||value>50000)throw new InvalidDataException("The current view distance is outside the supported range.");
            return value;
        }
        public float ReadEffectiveViewDistance() {
            ReadViewDistance();
            long frame=BitConverter.ToInt64(Read(moduleBase+WorldFrameRva,8),0);
            long scene=BitConverter.ToInt64(Read(frame+0x288,8),0);
            float value=BitConverter.ToSingle(Read(scene+0xD0,4),0);
            if(!Finite(value)||value<3000||value>50000)throw new InvalidDataException("The world scene has no valid view distance.");
            return value;
        }
        public void SetViewDistance(float value) {
            if(!Finite(value)||value<3000||value>50000)throw new ArgumentOutOfRangeException(nameof(value),"View distance must be between 3000 and 50000.");
            float before=ReadViewDistance();
            if(!viewOverride){originalViewDistance=before;viewOverride=true;}
            // This scalar is the clamped output of the native farclip callback.
            WriteFloat(moduleBase+0x4936DA8,value);
        }
        public void RestoreViewDistance() {
            if(!viewOverride)return;
            if(!process.HasExited)WriteFloat(moduleBase+0x4936DA8,originalViewDistance);
            viewOverride=false;
        }

        public bool FreeCameraEnabled { get { return freeCameraEnabled; } }

        [DllImport("kernel32.dll", SetLastError=true)]
        private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
        [DllImport("kernel32.dll", SetLastError=true)]
        private static extern bool ReadProcessMemory(IntPtr handle, IntPtr address, byte[] bytes, UIntPtr length, out UIntPtr read);
        [DllImport("kernel32.dll", SetLastError=true)]
        private static extern bool WriteProcessMemory(IntPtr handle, IntPtr address, byte[] bytes, UIntPtr length, out UIntPtr written);
        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        public NativeCamera(Process target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            string version = FileVersionInfo.GetVersionInfo(target.MainModule.FileName).FileVersion;
            if (version != "1.60.1.70450") throw new NotSupportedException("Expected WoW Forever build 70450; found " + version);
            supportsFreeCamera = true;
            supportsPoseAngles=true;
            WorldFrameRva=0x505DBB8;CameraFieldOffset=0x480;
            SpectatorRva=0x535E540;PlayerFlagsOffset=0x26D8;
            EncodedPlayerRva=0x524C1D0;KeyObjectRva=0x5054E08;
            PlayerVtableRva=0x42E9DB0;
            if (!Environment.Is64BitProcess) throw new NotSupportedException("Use the x64 version of this camera tool.");
            process = target;
            moduleBase = target.MainModule.BaseAddress.ToInt64();
            readHandle = OpenProcess(0x10, false, target.Id);
            if (readHandle == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            try { Capture(); } catch { CloseHandle(readHandle); throw; }
        }

        private byte[] Read(long address, int length)
        {
            if (disposed) throw new ObjectDisposedException(nameof(NativeCamera));
            if (address < 0x10000 || address > 0x00007FFFFFFFFFFF) throw new InvalidDataException("Invalid camera pointer.");
            byte[] bytes = new byte[length];
            UIntPtr count;
            if (!ReadProcessMemory(readHandle, new IntPtr(address), bytes, (UIntPtr)length, out count) || count.ToUInt64() != (ulong)length)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Unable to read the camera.");
            return bytes;
        }

        private long FindCamera()
        {
            if (process.HasExited) throw new InvalidOperationException("The WoW client has closed.");
            long frame = BitConverter.ToInt64(Read(moduleBase + WorldFrameRva, 8), 0);
            return BitConverter.ToInt64(Read(frame + CameraFieldOffset, 8), 0);
        }

        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

        public CameraSnapshot Capture()
        {
            long camera = FindCamera();
            byte[] data = Read(camera, CurrentZoomOffset + 4);
            float[] matrix = new float[9];
            for (int i=0;i<9;i++) {
                matrix[i] = BitConverter.ToSingle(data, 28+i*4);
                if (!Finite(matrix[i]) || Math.Abs(matrix[i]) > 1.01) throw new InvalidDataException("Camera rotation is invalid; no write performed.");
            }
            for (int row=0;row<3;row++) {
                double norm=0;
                for (int col=0;col<3;col++) norm += matrix[row*3+col]*matrix[row*3+col];
                if (Math.Abs(norm-1) > 0.02) throw new InvalidDataException("Camera matrix is invalid; no write performed.");
                for (int other=row+1;other<3;other++) {
                    double dot=0;
                    for (int col=0;col<3;col++) dot += matrix[row*3+col]*matrix[other*3+col];
                    if (Math.Abs(dot) > 0.02) throw new InvalidDataException("Camera matrix is invalid; no write performed.");
                }
            }
            var result = new CameraSnapshot {
                Address=camera,
                X=BitConverter.ToSingle(data,16), Y=BitConverter.ToSingle(data,20), Z=BitConverter.ToSingle(data,24),
                Fov=BitConverter.ToSingle(data,FovOffset),
                Yaw=(float)Math.Atan2(matrix[1],matrix[0]),
                Pitch=(float)Math.Asin(Math.Max(-1,Math.Min(1,-matrix[2]))),
                ZoomTarget=BitConverter.ToSingle(data,TargetZoomOffset), ZoomCurrent=BitConverter.ToSingle(data,CurrentZoomOffset)
            };
            if (!Finite(result.Fov) || result.Fov<=0 || result.Fov>=Math.PI ||
                !Finite(result.X)||!Finite(result.Y)||!Finite(result.Z)||Math.Abs(result.X)>100000||Math.Abs(result.Y)>100000||Math.Abs(result.Z)>100000)
                throw new InvalidDataException("Camera values are invalid; no write performed.");
            return result;
        }

        private void WriteFloat(long address, float value)
        {
            if (!Finite(value)) throw new ArgumentOutOfRangeException(nameof(value));
            WriteBytes(address,BitConverter.GetBytes(value));
        }

        private void WriteBytes(long address, byte[] bytes)
        {
            if (writeHandle == IntPtr.Zero) {
                writeHandle = OpenProcess(0x28, false, process.Id); // VM_OPERATION | VM_WRITE
                if (writeHandle == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }
            UIntPtr count;
            if (!WriteProcessMemory(writeHandle,new IntPtr(address),bytes,(UIntPtr)bytes.Length,out count) || count.ToUInt64()!=(ulong)bytes.Length)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"Unable to update camera data.");
        }

        private long FindPlayer()
        {
            if (!supportsFreeCamera) throw new NotSupportedException("Free-camera controls require build 70450.");
            ulong encoded=BitConverter.ToUInt64(Read(moduleBase+EncodedPlayerRva,8),0);
            long keyObject=BitConverter.ToInt64(Read(moduleBase+KeyObjectRva,8),0);
            ulong key=BitConverter.ToUInt64(Read(keyObject+0x107,8),0);
            long player=checked((long)PlayerPointerCodec.Decode70450(encoded,key));
            long vtable=BitConverter.ToInt64(Read(player,8),0);
            if (vtable-moduleBase!=PlayerVtableRva) throw new InvalidDataException("The active-player object does not match the validated build.");
            return player;
        }

        private void ValidateLease()
        {
            if(detachedFocus!=null){ValidateCharacterCamera();return;}
            if (!freeCameraEnabled || FindPlayer()!=leasedPlayer) throw new InvalidOperationException("Free camera is inactive or the player changed.");
            uint flags=BitConverter.ToUInt32(Read(leasedPlayer+PlayerFlagsOffset,4),0);
            if ((flags&FreeCameraMask)==0) throw new InvalidOperationException("The client ended free-camera mode.");
        }

        public void EnableFreeCamera()
        {
            EnableFreeCamera(false);
        }

        public void EnableFreeCamera(bool showCharacter)
        {
            if (freeCameraEnabled) return;
            if(showCharacter){EnableCharacterCamera();return;}
            CameraSnapshot camera=Capture();
            long player=FindPlayer();
            uint flags=BitConverter.ToUInt32(Read(player+PlayerFlagsOffset,4),0);
            if ((flags&FreeCameraMask)!=0) throw new InvalidOperationException("Free-camera mode is already active outside this tool.");
            byte[] before=Read(moduleBase+SpectatorRva,160);
            float current=BitConverter.ToSingle(before,132), desired=BitConverter.ToSingle(before,136), speed=BitConverter.ToSingle(before,140);
            if (!Finite(current)||current<=0||current>=Math.PI||!Finite(desired)||desired<=0||desired>=Math.PI||!Finite(speed)||speed<0||speed>10000)
                throw new InvalidDataException("Spectator-camera fields do not match this build.");
            leasedPlayer=player;
            originalSpectator=before;
            originalWorldFov=camera.Fov;
            originalWorldCamera=camera.Address;
            originalAngles=Read(camera.Address+0x154,8);
            changedAngles=false;
            try {
                WriteFloat(moduleBase+SpectatorRva,camera.Yaw);
                WriteFloat(moduleBase+SpectatorRva+4,camera.Pitch);
                WriteFloat(moduleBase+SpectatorRva+16,camera.X);
                WriteFloat(moduleBase+SpectatorRva+20,camera.Y);
                WriteFloat(moduleBase+SpectatorRva+24,camera.Z);
                WriteFloat(moduleBase+SpectatorRva+132,camera.Fov);
                WriteFloat(moduleBase+SpectatorRva+136,camera.Fov);
                freeCameraEnabled=true;
                // Only change the camera bit, preserving every unrelated flag.
                flags=BitConverter.ToUInt32(Read(player+PlayerFlagsOffset,4),0);
                WriteBytes(player+PlayerFlagsOffset,BitConverter.GetBytes(flags|FreeCameraMask));
            } catch {
                DisableFreeCamera();
                throw;
            }
        }

        public void SetFreeCameraPosition(float x,float y,float z)
        {
            if (!Finite(x)||!Finite(y)||!Finite(z)||Math.Abs(x)>100000||Math.Abs(y)>100000||Math.Abs(z)>100000) throw new ArgumentOutOfRangeException("position");
            if(CharacterVisibleCamera){CameraPoint pose=CaptureFreePose();pose.X=x;pose.Y=y;pose.Z=z;SetCharacterPose(pose);return;}
            ValidateLease();
            byte[] data=new byte[12];
            Buffer.BlockCopy(BitConverter.GetBytes(x),0,data,0,4);
            Buffer.BlockCopy(BitConverter.GetBytes(y),0,data,4,4);
            Buffer.BlockCopy(BitConverter.GetBytes(z),0,data,8,4);
            WriteBytes(moduleBase+SpectatorRva+16,data);
        }

        public void SetFreeCameraFov(float degrees)
        {
            if (!Finite(degrees)||degrees<20||degrees>140) throw new ArgumentOutOfRangeException(nameof(degrees));
            ValidateLease();
            if(CharacterVisibleCamera){WriteFloat(originalWorldCamera+FovOffset,(float)(degrees*Math.PI/180));return;}
            WriteFloat(moduleBase+SpectatorRva+136,(float)(degrees*Math.PI/180));
        }

        public void SetFreeCameraSpeed(float speed)
        {
            if (!Finite(speed)||speed<0.1||speed>500) throw new ArgumentOutOfRangeException(nameof(speed));
            ValidateLease();
            if(CharacterVisibleCamera)return; // Input speed belongs to the local controller.
            WriteFloat(moduleBase+SpectatorRva+140,speed);
        }

        public void SetFreeCameraCollision(bool enabled)
        {
            ValidateLease();
            if(CharacterVisibleCamera)return; // Detached camera has no native collision solver.
            WriteBytes(moduleBase+SpectatorRva+152,new byte[]{enabled?(byte)1:(byte)0});
        }

        public void CheckFreeCamera() { ValidateLease(); }

        public CameraPoint CaptureFreePose()
        {
            ValidateLease();
            if(CharacterVisibleCamera)return CameraPoint.FromSnapshot(Capture());
            byte[] data=Read(moduleBase+SpectatorRva,140);
            var point=new CameraPoint {X=BitConverter.ToSingle(data,16),Y=BitConverter.ToSingle(data,20),Z=BitConverter.ToSingle(data,24),Yaw=(float)(BitConverter.ToSingle(data,0)*180/Math.PI),Pitch=(float)(BitConverter.ToSingle(data,4)*180/Math.PI),Fov=CameraPoint.FovDegrees(BitConverter.ToSingle(data,136))};
            point.Yaw=CameraMotion.ShortestAngle(point.Yaw);
            point.Validate();return point;
        }

        public void SetFreePose(CameraPoint point)
        {
            point.Validate();ValidateLease();
            if(CharacterVisibleCamera){SetCharacterPose(point);return;}
            if(!supportsPoseAngles) throw new NotSupportedException("Camera routes require build 70450. Use the earlier tool for build 70374.");
            byte[] angles=new byte[8],position=new byte[12];
            Buffer.BlockCopy(BitConverter.GetBytes((float)(point.Yaw*Math.PI/180)),0,angles,0,4);
            Buffer.BlockCopy(BitConverter.GetBytes((float)(point.Pitch*Math.PI/180)),0,angles,4,4);
            Buffer.BlockCopy(BitConverter.GetBytes(point.X),0,position,0,4);
            Buffer.BlockCopy(BitConverter.GetBytes(point.Y),0,position,4,4);
            Buffer.BlockCopy(BitConverter.GetBytes(point.Z),0,position,8,4);
            long worldCamera=FindCamera();
            if(worldCamera!=originalWorldCamera)throw new InvalidOperationException("The world camera changed; movement stopped.");
            // The native camera update copies these angles back into the spectator state each frame.
            changedAngles=true;
            WriteBytes(worldCamera+0x154,angles);
            WriteBytes(moduleBase+SpectatorRva,angles);
            WriteBytes(moduleBase+SpectatorRva+16,position);
            WriteFloat(moduleBase+SpectatorRva+136,(float)(point.Fov*Math.PI/180));
        }

        public void DisableFreeCamera()
        {
            if(detachedFocus!=null){DisableCharacterCamera();return;}
            if (originalSpectator==null) return;
            if (!process.HasExited && FindPlayer()==leasedPlayer) {
                if (freeCameraEnabled) {
                    uint flags=BitConverter.ToUInt32(Read(leasedPlayer+PlayerFlagsOffset,4),0);
                    WriteBytes(leasedPlayer+PlayerFlagsOffset,BitConverter.GetBytes(flags&~FreeCameraMask));
                }
                // Restore only fields owned by this tool, leaving other state alone.
                foreach (int offset in new int[]{0,4,16,20,24,132,136,140}) {
                    byte[] saved=new byte[4];
                    Buffer.BlockCopy(originalSpectator,offset,saved,0,4);
                    WriteBytes(moduleBase+SpectatorRva+offset,saved);
                }
                WriteBytes(moduleBase+SpectatorRva+152,new byte[]{originalSpectator[152]});
                if (FindCamera()==originalWorldCamera) {
                    if(changedAngles)WriteBytes(originalWorldCamera+0x154,originalAngles);
                    WriteFloat(originalWorldCamera+FovOffset,originalWorldFov);
                }
                originalSpectator=null;
                freeCameraEnabled=false;
            } else {
                originalSpectator=null;
                freeCameraEnabled=false;
            }
        }

        public void OverrideFov(float degrees)
        {
            if (!Finite(degrees)||degrees<20||degrees>140) throw new ArgumentOutOfRangeException(nameof(degrees),"FOV must be 20–140 degrees.");
            CameraSnapshot snapshot=Capture();
            if (!hasOverride) { overrideAddress=snapshot.Address; originalFov=snapshot.Fov; hasOverride=true; }
            if (snapshot.Address!=overrideAddress) throw new InvalidOperationException("The camera changed. Override stopped.");
            WriteFloat(snapshot.Address+FovOffset,(float)(degrees*Math.PI/180.0));
        }

        public void RestoreFov()
        {
            if (!hasOverride) return;
            try {
                if (!process.HasExited && FindCamera()==overrideAddress) WriteFloat(overrideAddress+FovOffset,originalFov);
            } finally { hasOverride=false; }
        }

        public void Dispose()
        {
            if (disposed) return;
            try { RestoreVisuals(); RestoreFog(); DisableFreeCamera(); RestoreFov(); RestoreViewDistance(); }
            finally {
                if (writeHandle!=IntPtr.Zero) { CloseHandle(writeHandle); writeHandle=IntPtr.Zero; }
                CloseHandle(readHandle);
                disposed=true;
            }
        }
    }
}

