using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Machinae.Forever {
    internal static class StudioFonts {
        private static readonly PrivateFontCollection collection=new PrivateFontCollection();
        private static readonly List<IntPtr> buffers=new List<IntPtr>();
        [DllImport("gdi32.dll")]private static extern IntPtr AddFontMemResourceEx(IntPtr data,uint size,IntPtr reserved,ref uint count);
        private static int U16(byte[] b,int p){return (b[p]<<8)|b[p+1];}
        private static int U32(byte[] b,int p){return checked((int)(((uint)b[p]<<24)|((uint)b[p+1]<<16)|((uint)b[p+2]<<8)|b[p+3]));}
        private static void Put16(byte[] b,int p,int v){b[p]=(byte)(v>>8);b[p+1]=(byte)v;}
        private static void Put32(byte[] b,int p,uint v){b[p]=(byte)(v>>24);b[p+1]=(byte)(v>>16);b[p+2]=(byte)(v>>8);b[p+3]=(byte)v;}
        private static uint Sum(byte[] b,int offset,int length){uint sum=0;unchecked{for(int i=0;i<length;i+=4){uint word=0;for(int j=0;j<4;j++)word=(word<<8)|(i+j<length?b[offset+i+j]:0U);sum+=word;}}return sum;}

        // The installed family groups many independent weights under one Windows name.
        // Private, process-local aliases let GDI controls and GDI+ choose the same face.
        // Font outlines are unchanged; no font files are installed or redistributed.
        private static void LoadFace(string path,string family,string style){
            byte[] original=File.ReadAllBytes(path);int directory=-1,head=-1;
            for(int i=0;i<U16(original,4);i++){
                int p=12+i*16;string tag=Encoding.ASCII.GetString(original,p,4);
                if(tag=="name")directory=p;
                if(tag=="head")head=U32(original,p+8);
            }
            if(directory<0||head<0)throw new InvalidDataException("Font tables are unavailable.");
            int oldName=U32(original,directory+8),count=U16(original,oldName+2),strings=U16(original,oldName+4);
            if(U16(original,oldName)!=0)throw new InvalidDataException("Unsupported font name table.");
            byte[] records=new byte[6+count*12];Put16(records,2,count);Put16(records,4,records.Length);
            using(var text=new MemoryStream()){
                for(int i=0;i<count;i++){
                    int source=oldName+6+i*12,target=6+i*12;Buffer.BlockCopy(original,source,records,target,12);
                    int platform=U16(original,source),id=U16(original,source+6),length=U16(original,source+8),offset=U16(original,source+10);
                    string replacement=null;
                    if(id==1||id==16)replacement=family;
                    else if(id==2||id==17)replacement=style;
                    else if(id==3||id==4)replacement=family+" "+style;
                    else if(id==6)replacement=family.Replace(" ","")+"-"+style;
                    byte[] value;
                    if(replacement!=null)value=(platform==0||platform==3?Encoding.BigEndianUnicode:Encoding.ASCII).GetBytes(replacement);
                    else {value=new byte[length];Buffer.BlockCopy(original,oldName+strings+offset,value,0,length);}
                    Put16(records,target+8,value.Length);Put16(records,target+10,checked((int)text.Length));text.Write(value,0,value.Length);
                }
                int append=(original.Length+3)&~3,nameLength=records.Length+checked((int)text.Length);
                byte[] data=new byte[append+((nameLength+3)&~3)];Buffer.BlockCopy(original,0,data,0,original.Length);
                Buffer.BlockCopy(records,0,data,append,records.Length);Buffer.BlockCopy(text.ToArray(),0,data,append+records.Length,(int)text.Length);
                Put32(data,directory+8,(uint)append);Put32(data,directory+12,(uint)nameLength);Put32(data,directory+4,Sum(data,append,nameLength));
                Put32(data,head+8,0);unchecked{Put32(data,head+8,0xB1B0AFBAU-Sum(data,0,data.Length));}
                IntPtr buffer=Marshal.AllocHGlobal(data.Length);Marshal.Copy(data,0,buffer,data.Length);uint added=0;
                if(AddFontMemResourceEx(buffer,(uint)data.Length,IntPtr.Zero,ref added)==IntPtr.Zero){Marshal.FreeHGlobal(buffer);throw new InvalidDataException("Unable to load the selected font face.");}
                buffers.Add(buffer);collection.AddMemoryFont(buffer,data.Length);
            }
        }
        internal static FontFamily SelectFamily(){
            string directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Microsoft","Windows","Fonts");
            try {
                const string family="FCS Helvetica Neue";
                LoadFace(Path.Combine(directory,"HelveticaNeueRoman.otf"),family,"Regular");
                LoadFace(Path.Combine(directory,"HelveticaNeueBold.otf"),family,"Bold");
                return new FontFamily(family,collection);
            }catch(IOException){}catch(ArgumentException){}catch(ExternalException){}catch(UnauthorizedAccessException){}
            return new FontFamily("Segoe UI");
        }
    }
}
