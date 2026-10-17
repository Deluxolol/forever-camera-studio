using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Machinae.Forever {
    // Hooks live in this application, on its UI message loop. No game hooks or
    // remote executable code are installed. Only camera controls are consumed.
    internal sealed class DetachedCameraInput : IDisposable {
        private delegate IntPtr Hook(int code,IntPtr message,IntPtr data);
        private readonly Hook keyboardCallback,mouseCallback;
        private readonly Func<bool> active;
        private readonly bool[] keys=new bool[256];
        private IntPtr keyboard,mouse;
        private bool looking;
        private int anchorX,anchorY,dx,dy;
        [StructLayout(LayoutKind.Sequential)] private struct MouseData {public int X,Y;public uint Mouse,Flags,Time;public UIntPtr Extra;}
        [DllImport("user32.dll",SetLastError=true)] private static extern IntPtr SetWindowsHookEx(int kind,Hook callback,IntPtr module,uint thread);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
        public DetachedCameraInput(Func<bool> isActive) {
            active=isActive;keyboardCallback=Keyboard;mouseCallback=Mouse;
            keyboard=SetWindowsHookEx(13,keyboardCallback,GetModuleHandle(null),0);
            if(keyboard==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
            mouse=SetWindowsHookEx(14,mouseCallback,GetModuleHandle(null),0);
            if(mouse==IntPtr.Zero){int error=Marshal.GetLastWin32Error();Dispose();throw new Win32Exception(error);}
        }
        private bool IsActive(){return active()&&(GetAsyncKeyState(0x12)&0x8000)==0&&(GetAsyncKeyState(0x5B)&0x8000)==0&&(GetAsyncKeyState(0x5C)&0x8000)==0;}
        private static bool ControlKey(int key){return key==0x57||key==0x41||key==0x53||key==0x44||key==0x51||key==0x45||key==0x20||key==0x58||key>=0x25&&key<=0x28;}
        private IntPtr Keyboard(int code,IntPtr message,IntPtr data) {
            if(code>=0){int key=Marshal.ReadInt32(data),kind=message.ToInt32();
                if(key>=0&&key<keys.Length&&ControlKey(key)){
                    bool down=kind==0x100||kind==0x104,up=kind==0x101||kind==0x105;
                    if(IsActive()&&(down||up)){bool captured=keys[key];keys[key]=down;if(down||captured)return new IntPtr(1);}
                    // Release our state after focus changes, but let the new window receive input.
                    if(up)keys[key]=false;
                }
            }
            return CallNextHookEx(IntPtr.Zero,code,message,data);
        }
        private IntPtr Mouse(int code,IntPtr message,IntPtr data) {
            if(code>=0&&IsActive()){
                int kind=message.ToInt32();var value=(MouseData)Marshal.PtrToStructure(data,typeof(MouseData));
                if(kind==0x204){looking=true;anchorX=value.X;anchorY=value.Y;return new IntPtr(1);}
                if(kind==0x205&&looking){looking=false;return new IntPtr(1);}
                if(kind==0x200&&looking){dx+=value.X-anchorX;dy+=value.Y-anchorY;return new IntPtr(1);}
            }else if(code>=0){looking=false;dx=dy=0;}
            return CallNextHookEx(IntPtr.Zero,code,message,data);
        }
        public CameraPoint Advance(CameraPoint pose,double seconds,float speed) {
            if(!IsActive()){Array.Clear(keys,0,keys.Length);looking=false;dx=dy=0;return null;}
            int mouseX=dx,mouseY=dy;dx=dy=0;
            float forward=(keys[0x57]||keys[0x26]?1:0)-(keys[0x53]||keys[0x28]?1:0);
            float right=(keys[0x44]||keys[0x27]?1:0)-(keys[0x41]||keys[0x25]?1:0);
            float vertical=(keys[0x45]||keys[0x20]?1:0)-(keys[0x51]||keys[0x58]?1:0);
            if(mouseX==0&&mouseY==0&&forward==0&&right==0&&vertical==0)return null;
            var next=pose.Copy();next.Yaw=CameraMotion.ShortestAngle(next.Yaw-mouseX*0.15f);next.Pitch=Math.Max(-89.9f,Math.Min(89.9f,next.Pitch+mouseY*0.15f));
            double yaw=next.Yaw*Math.PI/180,pitch=next.Pitch*Math.PI/180;
            double length=Math.Max(1,Math.Sqrt(forward*forward+right*right+vertical*vertical));
            double amount=Math.Min(0.05,Math.Max(0,seconds))*speed/length;
            if((GetAsyncKeyState(0x10)&0x8000)!=0)amount*=3;
            if((GetAsyncKeyState(0x11)&0x8000)!=0)amount*=0.2;
            // WoW's second camera matrix row points left: screen-right is
            // (sin(yaw), -cos(yaw), 0), and turning right decreases yaw.
            next.X+=(float)((forward*Math.Cos(pitch)*Math.Cos(yaw)+right*Math.Sin(yaw))*amount);
            next.Y+=(float)((forward*Math.Cos(pitch)*Math.Sin(yaw)-right*Math.Cos(yaw))*amount);
            next.Z+=(float)((-forward*Math.Sin(pitch)+vertical)*amount);next.Validate();return next;
        }
        public void Dispose(){if(mouse!=IntPtr.Zero){UnhookWindowsHookEx(mouse);mouse=IntPtr.Zero;}if(keyboard!=IntPtr.Zero){UnhookWindowsHookEx(keyboard);keyboard=IntPtr.Zero;}Array.Clear(keys,0,keys.Length);looking=false;}
    }
}
