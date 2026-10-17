using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Machinae.Forever
{
    internal static class FantasyArt {
        internal static readonly FontFamily Family=StudioFonts.SelectFamily();
        internal static readonly Color Base=Color.FromArgb(32,31,31),SurfaceColor=Color.FromArgb(47,47,47),ControlColor=SurfaceColor,HoverColor=Color.FromArgb(59,59,59),Edge=Color.FromArgb(71,71,71);
        internal static readonly Color TextColor=Color.FromArgb(170,170,170),Accent=Color.FromArgb(57,157,219),Muted=Color.FromArgb(153,153,153),Disabled=Color.FromArgb(110,110,110);
        internal static void Diamond(Graphics g,float x,float y,float size,Color color) {
            using(var brush=new SolidBrush(color))g.FillPolygon(brush,new[]{new PointF(x,y-size),new PointF(x+size,y),new PointF(x,y+size),new PointF(x-size,y)});
        }
        internal static void Frame(Graphics g,Rectangle r,bool bright) {
            if(r.Width<10||r.Height<10)return;
            using(var shadow=new Pen(Base,3))g.DrawRectangle(shadow,r);
            using(var line=new Pen(bright?Accent:Edge))g.DrawRectangle(line,r);
            using(var inner=new Pen(ControlColor))g.DrawRectangle(inner,r.X+3,r.Y+3,r.Width-6,r.Height-6);
            foreach(int x in new[]{r.Left,r.Right})foreach(int y in new[]{r.Top,r.Bottom}) {
                int dx=x==r.Left?1:-1,dy=y==r.Top?1:-1;
                using(var line=new Pen(bright?Muted:Edge))g.DrawLines(line,new[]{new Point(x,y+dy*14),new Point(x,y),new Point(x+dx*14,y)});
                Diamond(g,x+dx*4,y+dy*4,2,bright?Muted:Edge);
            }
        }
        internal static void Surface(Graphics g,Rectangle bounds,Color top,Color bottom) {
            if(bounds.Width<1||bounds.Height<1)return;
            using(var brush=new LinearGradientBrush(bounds,top,bottom,90))g.FillRectangle(brush,bounds);
        }
    }
    internal sealed class FantasyPanel : Panel {
        internal FantasyPanel(){DoubleBuffered=true;}
        protected override void OnPaintBackground(PaintEventArgs e) {
            FantasyArt.Surface(e.Graphics,ClientRectangle,FantasyArt.Base,FantasyArt.Base);
        }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            FantasyArt.Frame(e.Graphics,new Rectangle(2,2,Width-5,Height-5),false);
        }
    }
    internal sealed class StudioGroup : GroupBox {
        internal StudioGroup(){DoubleBuffered=true;}
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.Clear(BackColor);
            FantasyArt.Surface(e.Graphics,new Rectangle(4,14,Width-8,Height-18),FantasyArt.SurfaceColor,FantasyArt.SurfaceColor);
            FantasyArt.Frame(e.Graphics,new Rectangle(1,10,Width-3,Height-12),false);
            using(var titleFont=new Font(FantasyArt.Family,10,FontStyle.Bold)) {
                Size title=TextRenderer.MeasureText(Text,titleFont);
                var plaque=new Rectangle(14,1,title.Width+15,23);
                FantasyArt.Surface(e.Graphics,plaque,FantasyArt.Base,FantasyArt.Base);
                using(var edge=new Pen(FantasyArt.Edge))e.Graphics.DrawRectangle(edge,plaque);
                using(var inset=new Pen(FantasyArt.Edge))e.Graphics.DrawRectangle(inset,plaque.X+2,plaque.Y+2,plaque.Width-4,plaque.Height-4);
                TextRenderer.DrawText(e.Graphics,Text,titleFont,plaque,FantasyArt.TextColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);
            }
        }
    }
    internal sealed class StudioButton : Button {
        private bool hover,pressed;
        internal bool Selected {get;set;}
        internal string Emphasis="";
        internal Color? Swatch;
        internal StudioButton(){DoubleBuffered=true;}
        protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e){hover=false;pressed=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnMouseDown(MouseEventArgs e){pressed=true;Invalidate();base.OnMouseDown(e);}
        protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
        protected override void OnPaint(PaintEventArgs e) {
            bool filled=Enabled&&(Selected||Emphasis=="play");
            Color top=pressed?FantasyArt.HoverColor:filled?FantasyArt.Edge:hover?FantasyArt.HoverColor:FantasyArt.ControlColor;
            Color bottom=top;
            if(Enabled&&Emphasis=="stop")top=bottom=hover?FantasyArt.HoverColor:FantasyArt.ControlColor;
            FantasyArt.Surface(e.Graphics,ClientRectangle,pressed?bottom:top,pressed?top:bottom);
            FantasyArt.Frame(e.Graphics,new Rectangle(1,1,Width-3,Height-3),Enabled&&(Selected||hover));
            Color text=Enabled?(Selected?FantasyArt.Accent:FantasyArt.TextColor):FantasyArt.Disabled;
            if(Enabled&&Emphasis.Length>0)using(var edge=new Pen(Emphasis=="play"?FantasyArt.Accent:FantasyArt.Muted))e.Graphics.DrawRectangle(edge,3,3,Width-7,Height-7);
            TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(5,1,Width-(Swatch.HasValue?48:10),Height-2),text,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);
            if(Swatch.HasValue){var sample=new Rectangle(Width-36,7,25,Height-14);using(var fill=new SolidBrush(Swatch.Value))e.Graphics.FillRectangle(fill,sample);using(var edge=new Pen(FantasyArt.TextColor))e.Graphics.DrawRectangle(edge,sample);}
            if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(6,6,Width-12,Height-12));
        }
    }
    internal sealed class FantasyCheckBox : CheckBox {
        internal FantasyCheckBox(){SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);}
        protected override void OnPaint(PaintEventArgs e){
            e.Graphics.Clear(BackColor);
            using(var fill=new SolidBrush(FantasyArt.Base))e.Graphics.FillRectangle(fill,1,3,14,14);
            using(var edge=new Pen(FantasyArt.Edge))e.Graphics.DrawRectangle(edge,1,3,14,14);
            if(Checked)using(var tick=new Pen(Enabled?FantasyArt.Accent:FantasyArt.Disabled,2))e.Graphics.DrawLines(tick,new[]{new Point(4,10),new Point(7,13),new Point(13,6)});
            TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(21,0,Width-21,Height),Enabled?ForeColor:FantasyArt.Disabled,TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);
            if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(20,1,Width-21,Height-2));
        }
    }
    internal sealed class FantasyNumber : NumericUpDown {
        private readonly SpinButtons arrows;
        internal FantasyNumber(){
            BorderStyle=BorderStyle.None;BackColor=FantasyArt.ControlColor;
            arrows=new SpinButtons(this);
        }
        internal void Place(Control parent,int x,int y,int width){
            var frame=new FieldFrame(this){Location=new Point(x,y),Size=new Size(width,27),BackColor=BackColor};
            parent.Controls.Add(frame);
            // Keep the standard editor and its keyboard behavior; clip the native spinner.
            var editorHost=new Panel{Location=new Point(8,5),Size=new Size(width-35,18),BackColor=BackColor};
            frame.Controls.Add(editorHost);editorHost.Controls.Add(this);SetBounds(0,0,width-17,20);
            arrows.SetBounds(width-23,1,22,25);frame.Controls.Add(arrows);arrows.BringToFront();
        }
        protected override void OnEnabledChanged(EventArgs e){base.OnEnabledChanged(e);if(arrows!=null)arrows.Enabled=Enabled;if(Parent!=null){Parent.Invalidate();if(Parent.Parent!=null)Parent.Parent.Invalidate();}}
        private sealed class FieldFrame : Panel {
            private readonly FantasyNumber owner;
            internal FieldFrame(FantasyNumber owner){this.owner=owner;DoubleBuffered=true;}
            protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using(var edge=new Pen(owner.Enabled&&owner.ContainsFocus?FantasyArt.Accent:FantasyArt.Edge))e.Graphics.DrawRectangle(edge,0,0,Width-1,Height-1);}
        }
        private sealed class SpinButtons : Control {
            private readonly FantasyNumber owner;
            private readonly System.Windows.Forms.Timer repeat=new System.Windows.Forms.Timer();
            private int direction;
            private bool hovering;
            internal SpinButtons(FantasyNumber owner){
                this.owner=owner;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
                TabStop=false;AccessibleName="Increase or decrease value";AccessibleRole=AccessibleRole.SpinButton;
                repeat.Tick+=(s,e)=>{repeat.Interval=70;Step();};
            }
            private void Step(){if(!Enabled)return;if(direction>0)owner.UpButton();else if(direction<0)owner.DownButton();}
            protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left||!Enabled)return;owner.Focus();direction=e.Y<Height/2?1:-1;Capture=true;Step();repeat.Interval=400;repeat.Start();Invalidate();}
            protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);Stop();Capture=false;}
            protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture)Stop();}
            protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);hovering=true;Invalidate();}
            protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hovering=false;Invalidate();}
            protected override void OnEnabledChanged(EventArgs e){base.OnEnabledChanged(e);if(!Enabled)Stop();Invalidate();}
            private void Stop(){repeat.Stop();direction=0;Invalidate();}
            protected override void Dispose(bool disposing){if(disposing)repeat.Dispose();base.Dispose(disposing);}
            protected override void OnPaint(PaintEventArgs e){
                FantasyArt.Surface(e.Graphics,ClientRectangle,hovering&&Enabled?FantasyArt.HoverColor:FantasyArt.ControlColor,FantasyArt.ControlColor);
                using(var edge=new Pen(FantasyArt.Edge)){e.Graphics.DrawLine(edge,0,0,0,Height);e.Graphics.DrawLine(edge,0,Height/2,Width,Height/2);}
                int x=Width/2,y=Height/4;
                using(var fill=new SolidBrush(Enabled?FantasyArt.TextColor:FantasyArt.Edge)){
                    e.Graphics.FillPolygon(fill,new[]{new Point(x,y-2),new Point(x-4,y+2),new Point(x+4,y+2)});
                    y=Height*3/4;e.Graphics.FillPolygon(fill,new[]{new Point(x,y+2),new Point(x-4,y-2),new Point(x+4,y-2)});
                }
            }
        }
    }
    internal sealed class FantasyTimeField : Control {
        internal FantasyTimeField(){DoubleBuffered=true;Size=new Size(120,25);AccessibleRole=AccessibleRole.Text;}
        protected override void OnPaint(PaintEventArgs e){
            FantasyArt.Surface(e.Graphics,ClientRectangle,FantasyArt.Base,FantasyArt.Base);
            using(var edge=new Pen(FantasyArt.Edge))e.Graphics.DrawRectangle(edge,0,0,Width-1,Height-1);
            TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(6,1,Width-12,Height-2),FantasyArt.Disabled,TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);
        }
    }
    internal sealed class FantasySlider : Control {
        private int minimum,maximum=100,value;
        public int Minimum {get{return minimum;}set{minimum=value;Value=this.value;}}
        public int Maximum {get{return maximum;}set{maximum=value;Value=this.value;}}
        public int Value {get{return value;}set{int next=Math.Max(minimum,Math.Min(maximum,value));if(next==this.value)return;this.value=next;Invalidate();if(ValueChanged!=null)ValueChanged(this,EventArgs.Empty);}}
        public TickStyle TickStyle {get;set;}
        public event EventHandler ValueChanged;
        internal FantasySlider(){DoubleBuffered=true;TabStop=true;SetStyle(ControlStyles.Selectable,true);AccessibleRole=AccessibleRole.Slider;}
        private void Pick(int x){Value=minimum+(int)Math.Round((maximum-minimum)*Math.Max(0,Math.Min(1,(x-14.0)/Math.Max(1,Width-28))));}
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left){Focus();Capture=true;Pick(e.X);}}
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(Capture)Pick(e.X);}
        protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(e.Button==MouseButtons.Left)Capture=false;}
        protected override bool IsInputKey(Keys key){return key==Keys.Left||key==Keys.Right||key==Keys.Up||key==Keys.Down||base.IsInputKey(key);}
        protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);switch(e.KeyCode){case Keys.Left:case Keys.Down:Value--;break;case Keys.Right:case Keys.Up:Value++;break;case Keys.Home:Value=minimum;break;case Keys.End:Value=maximum;break;default:return;}e.Handled=true;}
        protected override void OnPaint(PaintEventArgs e){
            e.Graphics.Clear(BackColor);int y=Height/2;var rail=new Rectangle(14,y-3,Math.Max(1,Width-28),6);
            using(var fill=new SolidBrush(FantasyArt.Base))e.Graphics.FillRectangle(fill,rail);
            using(var edge=new Pen(FantasyArt.Edge))e.Graphics.DrawRectangle(edge,rail);
            int x=14+(int)Math.Round((Width-28)*(value-minimum)/(double)Math.Max(1,maximum-minimum));
            var thumb=new Rectangle(x-6,y-10,12,20);FantasyArt.Surface(e.Graphics,thumb,FantasyArt.HoverColor,FantasyArt.Edge);
            using(var edge=new Pen(FantasyArt.Edge))e.Graphics.DrawRectangle(edge,thumb);
            using(var notch=new Pen(FantasyArt.Edge))e.Graphics.DrawLine(notch,x,y-4,x,y+4);
            if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(3,3,Width-6,Height-6));
        }
    }
    internal static class Program
    {
        [STAThread] private static void Main() {
            using(var mutex=new Mutex(true,"Local\\MachinaeForeverCameraController",out bool first)) {
                if(!first) {MessageBox.Show("The camera controller is already open.","Forever Camera Studio");return;}
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new CameraWindow());
            }
        }
    }

    internal sealed class CameraWindow : Form
    {
        private static readonly Color Background=FantasyArt.Base,PanelColor=FantasyArt.SurfaceColor,Ink=FantasyArt.TextColor,Accent=FantasyArt.Accent,Muted=FantasyArt.Muted;
        private readonly Label brandTitle=new Label();
        private string brandName="Forever Camera Studio";
        private string sessionLog;
        private static string NameFile {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ForeverCameraStudio","name.txt");}}
        private NativeCamera camera;
        private Process target;
        private DetachedCameraInput characterInput;
        private bool updating;
        private readonly Label status=new Label(),coordinates=new Label(),progress=new Label();
        private readonly Button connect=new StudioButton();
        private readonly CheckBox spectate=new FantasyCheckBox(),showCharacter=new FantasyCheckBox(),collision=new FantasyCheckBox(),onTop=new FantasyCheckBox(),auto=new FantasyCheckBox(),loop=new FantasyCheckBox(),bezier=new FantasyCheckBox();
        private readonly NumericUpDown fov=new FantasyNumber(),speed=new FantasyNumber(),duration=new FantasyNumber();
        private readonly NumericUpDown viewDistance=new FantasyNumber();
        private readonly CheckBox viewEnabled=new FantasyCheckBox();
        private readonly Label viewReadout=new Label();
        private readonly NumericUpDown fogDistance=new FantasyNumber();
        private readonly CheckBox[] renderToggles=new CheckBox[6];
        private readonly CheckBox worldTimeEnabled=new FantasyCheckBox(),slowEnabled=new FantasyCheckBox();
        private readonly NumericUpDown worldHour=new FantasyNumber(),worldMinute=new FantasyNumber(),worldSpeed=new FantasyNumber();
        private readonly NumericUpDown slowFrom=new FantasyNumber(),slowTo=new FantasyNumber(),slowDuration=new FantasyNumber();
        private readonly Label timeReadout=new Label(),slowReadout=new Label();
        private readonly FantasySlider slowSlider=new FantasySlider();
        private double worldMinutes,slowElapsed;
        private bool slowTransition,cameraSlowEnabled;
        private float slowScale=1;
        private readonly CheckBox fogEnabled=new FantasyCheckBox();
        private readonly CheckBox noFog=new FantasyCheckBox();
        private readonly CheckBox lightingEnabled=new FantasyCheckBox();
        private readonly StudioButton sunlightButton=new StudioButton(),ambientButton=new StudioButton();
        private Color sunlightColor=Color.FromArgb(255,239,210),ambientColor=Color.FromArgb(100,118,140);
        private readonly CheckBox skyEnabled=new FantasyCheckBox(),cloudsEnabled=new FantasyCheckBox();
        private readonly StudioButton[] skyButtons=new StudioButton[6],cloudButtons=new StudioButton[3];
        private readonly Color[] skyColors={Color.MidnightBlue,Color.SteelBlue,Color.SkyBlue,Color.LightSkyBlue,Color.LightGoldenrodYellow,Color.CornflowerBlue};
        private readonly Color[] cloudColors={Color.WhiteSmoke,Color.SlateGray,Color.DarkSlateBlue};
        private readonly Label fogReadout=new Label();
        private readonly NumericUpDown[] pan=new NumericUpDown[4];
        private readonly FantasySlider fovSlider=new FantasySlider();
        private readonly DataGridView grid=new DataGridView();
        private readonly CheckBox startFromSelected=new FantasyCheckBox();
        private readonly CheckBox weatherEnabled=new FantasyCheckBox();
        private readonly CheckBox thunderEnabled=new FantasyCheckBox();
        private readonly StudioButton rainButton=new StudioButton(),snowButton=new StudioButton();
        private readonly NumericUpDown weatherIntensity=new FantasyNumber();
        private readonly FantasySlider weatherSlider=new FantasySlider();
        private int weatherKind=1;
        private bool weatherDirty;
        private double weatherChangeAt;
        private CameraPoint draggedWaypoint;
        private Point waypointDragOrigin;
        private int waypointDropSlot=-1;
        private double lastWaypointScroll;
        private string waypointDragToken;
        private const string WaypointDragFormat="ForeverCameraStudio.WaypointOrder";
        private readonly BindingList<CameraPoint> points=new BindingList<CameraPoint>();
        private readonly CameraMotion motion=new CameraMotion();
        private readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
        private readonly Stopwatch clock=new Stopwatch();
        private double lastTick,lastEnvironmentRead;
        private readonly ToolTip hints=new ToolTip();
        private const int ToggleHotkey=100,StopHotkey=101,RecordHotkey=102;
        [DllImport("user32.dll",SetLastError=true)] private static extern bool RegisterHotKey(IntPtr window,int id,uint modifiers,uint key);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr window,int id);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window,out uint processId);
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);

        public CameraWindow() : this(true) { }
        public CameraWindow(bool connectOnShow) {
            using(var icon=typeof(CameraWindow).Assembly.GetManifestResourceStream("ForeverCameraStudio.AppIcon.ico"))Icon=new Icon(icon);
            if(connectOnShow){
                try{string directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ForeverCameraStudio","Logs");Directory.CreateDirectory(directory);sessionLog=Path.Combine(directory,DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Process.GetCurrentProcess().Id+".log");LogEvent("Session started: build 70450; client-clock patch removed; camera-only slow motion.");status.TextChanged+=(s,e)=>LogEvent(status.Text);}
                catch(IOException){}catch(UnauthorizedAccessException){}
            }
            try{if(File.Exists(NameFile)){string saved=File.ReadAllText(NameFile).Trim();if(saved.Length>0&&saved.Length<=32&&!saved.Any(char.IsControl))brandName=saved;}}catch(IOException){}catch(UnauthorizedAccessException){}
            Text=brandName;ClientSize=new Size(1378,760);MinimumSize=MaximumSize=Size;MaximizeBox=false;
            StartPosition=FormStartPosition.CenterScreen;Font=new Font(FantasyArt.Family,11);BackColor=Background;ForeColor=Ink;
            var header=new FantasyPanel {BackColor=FantasyArt.Base,Location=new Point(214,0),Size=new Size(1164,94)};Controls.Add(header);
            var headerLogo=new PictureBox {Name="HeaderLogo",BackColor=Color.Transparent,SizeMode=PictureBoxSizeMode.Zoom,Location=new Point(24,23),Size=new Size(56,56),TabStop=false};
            using(var stream=typeof(CameraWindow).Assembly.GetManifestResourceStream("ForeverCameraStudio.AppIcon.png"))using(var image=Image.FromStream(stream))headerLogo.Image=new Bitmap(image);
            headerLogo.Disposed+=(s,e)=>headerLogo.Image.Dispose();header.Controls.Add(headerLogo);
            brandTitle.Text=brandName;brandTitle.SetBounds(92,29,880,44);brandTitle.TextAlign=ContentAlignment.MiddleLeft;brandTitle.Font=new Font(FantasyArt.Family,26,FontStyle.Bold);brandTitle.ForeColor=FantasyArt.Accent;brandTitle.BackColor=Color.Transparent;header.Controls.Add(brandTitle);
            ButtonAt(header,connect,"Connect",996,29,144,36,Attach);
            var sidebar=new FantasyPanel{Location=new Point(0,0),Size=new Size(214,760),BackColor=FantasyArt.Base};Controls.Add(sidebar);sidebar.BringToFront();
            var content=new FantasyPanel{Location=new Point(226,135),Size=new Size(1136,531),BackColor=PanelColor,Tag="StudioContent"};Controls.Add(content);
            var general=Page(content,"Lens & Display");var spectator=Page(content,"Flight & Routes");var light=Page(content,"Environment");var weather=Page(content,"Weather");
            BuildGeneral(general);BuildSpectate(spectator);BuildLight(light);BuildWeather(weather);

            var pages=new[]{spectator,general,light,weather};var nav=new Button[pages.Length];
            for(int i=0;i<pages.Length;i++){int selected=i;nav[i]=ButtonAt(sidebar,pages[i].Name,15,24+i*56,184,44,()=>{foreach(var page in pages)page.Visible=false;pages[selected].Visible=true;for(int j=0;j<nav.Length;j++){((StudioButton)nav[j]).Selected=j==selected;nav[j].Invalidate();}});}
            foreach(var page in pages)page.Visible=false;spectator.Visible=true;((StudioButton)nav[0]).Selected=true;
            LabelAt(sidebar,"CAMERA SHORTCUTS",24,560,180,25).ForeColor=Muted;
            LabelAt(sidebar,"F8   Free camera\n\nF7   Record waypoint\n\nF10  Return to player",24,600,178,100).ForeColor=Ink;
            LabelAt(sidebar,"BUILD 70450",24,720,180,25).ForeColor=Muted;
            status.SetBounds(239,685,1100,27);status.ForeColor=Accent;status.Text="Start the client and click Connect.";Controls.Add(status);
            coordinates.SetBounds(239,722,1100,23);coordinates.ForeColor=Muted;Controls.Add(coordinates);
            timer.Interval=16;timer.Tick+=(s,e)=>TickCamera();clock.Start();lastTick=clock.Elapsed.TotalSeconds;
            Shown+=(s,e)=>{
                try{int dark=1;DwmSetWindowAttribute(Handle,20,ref dark,4);}catch{}
                if(!connectOnShow){status.Text="Preview — not connected; no changes to the game.";return;}
                bool a=RegisterHotKey(Handle,ToggleHotkey,0x4000,(uint)Keys.F8),b=RegisterHotKey(Handle,StopHotkey,0x4000,(uint)Keys.F10);
                RegisterHotKey(Handle,RecordHotkey,0x4000,(uint)Keys.F7);Guard(Attach);
                if(!a||!b)status.Text+=" A hotkey is unavailable; use the buttons.";
            };
        }

        private System.Windows.Forms.Panel Page(Control parent,string text){var page=new System.Windows.Forms.Panel{Name=text,Tag="StudioPage",Size=parent.ClientSize,BackColor=PanelColor,ForeColor=Ink};parent.Controls.Add(page);return page;}
        private Label LabelAt(Control parent,string text,int x,int y,int width,int height=25){var label=new Label{UseMnemonic=false,Text=text,Location=new Point(x,y),Size=new Size(width,height),ForeColor=Ink,BackColor=Color.Transparent};parent.Controls.Add(label);return label;}
        private GroupBox Group(Control parent,string text,int x,int y,int width,int height){var group=new StudioGroup{Text=text,Location=new Point(x,y),Size=new Size(width,height),ForeColor=Ink,BackColor=PanelColor};parent.Controls.Add(group);return group;}
        private Button ButtonAt(Control parent,Button button,string text,int x,int y,int width,int height,Action action){
            button.Text=text;button.SetBounds(x,y,width,height);button.FlatStyle=FlatStyle.Flat;button.FlatAppearance.BorderColor=FantasyArt.Edge;button.FlatAppearance.MouseOverBackColor=FantasyArt.HoverColor;button.BackColor=FantasyArt.ControlColor;button.ForeColor=Accent;button.UseVisualStyleBackColor=false;
            button.Click+=(s,e)=>Guard(action);parent.Controls.Add(button);return button;
        }
        private Button ButtonAt(Control parent,string text,int x,int y,int width,int height,Action action){return ButtonAt(parent,new StudioButton(),text,x,y,width,height,action);}
        private NumericUpDown Number(Control parent,NumericUpDown number,decimal min,decimal max,decimal value,int decimals,int x,int y,int width=100){
            number.Minimum=min;number.Maximum=max;number.DecimalPlaces=decimals;number.Value=value;number.Increment=decimals==0?1:0.1M;number.SetBounds(x,y,width,29);number.BackColor=FantasyArt.ControlColor;number.ForeColor=Ink;number.BorderStyle=BorderStyle.None;((FantasyNumber)number).Place(parent,x,y,width);return number;
        }
        private CheckBox Check(Control parent,CheckBox box,string text,int x,int y,bool value=false){box.Text=text;box.AutoSize=true;box.Padding=new Padding(0,0,7,0);box.Location=new Point(x,y);box.ForeColor=Ink;box.Checked=value;parent.Controls.Add(box);return box;}
        private void Unavailable(Control control,string reason){control.Enabled=false;hints.SetToolTip(control,reason);}

        private void BuildGeneral(Control page){
            var cameraGroup=Group(page,"Lens",18,18,786,260);LabelAt(cameraGroup,"Free camera FOV (°)",20,40,180);
            fovSlider.Minimum=20;fovSlider.Maximum=140;fovSlider.Value=90;fovSlider.TickStyle=TickStyle.None;fovSlider.SetBounds(202,33,404,40);cameraGroup.Controls.Add(fovSlider);Number(cameraGroup,fov,20,140,90,1,633,36,126);
            fov.ValueChanged+=(s,e)=>{if(updating)return;updating=true;fovSlider.Value=(int)fov.Value;updating=false;Guard(ApplySettings);};fovSlider.ValueChanged+=(s,e)=>{if(!updating)fov.Value=fovSlider.Value;};
            string[] names={"Attach X","Attach Y","Attach Z"};
            for(int i=0;i<3;i++){LabelAt(cameraGroup,names[i],20,94+i*45,160).ForeColor=Muted;var slider=new FantasySlider{Minimum=-100,Maximum=100,TickStyle=TickStyle.None};slider.SetBounds(202,87+i*45,404,35);cameraGroup.Controls.Add(slider);var field=Number(cameraGroup,new FantasyNumber(),-1000,1000,0,2,633,90+i*45,126);Unavailable(slider,"Camera attachment controls are not available yet.");Unavailable(field,"Camera attachment controls are not available yet.");}
            var window=Group(page,"Window",826,18,282,72);Check(window,onTop,"Always on top",18,30);onTop.CheckedChanged+=(s,e)=>TopMost=onTop.Checked;
            var render=Group(page,"Rendering",826,107,282,212);
            string[] renderNames={"M2 / models","Details","Terrain","WMO / buildings","Liquid / water","Mountains"};
            for(int i=0;i<renderNames.Length;i++){
                int index=i;var box=renderToggles[i]=new FantasyCheckBox();Check(render,box,renderNames[i],19,28+i*25,true);
                box.CheckedChanged+=(s,e)=>{if(!updating)Guard(()=>{RequireCamera();camera.SetRendering(index,box.Checked);status.Text=renderNames[index]+(box.Checked?" visible.":" hidden.");});};
            }
            ButtonAt(render,"Restore rendering",18,177,240,27,()=>{RequireCamera();camera.RestoreRendering();status.Text="Original rendering restored.";});
            var slow=Group(page,"Slow Motion — Not available",18,297,786,216);
            Check(slow,slowEnabled,"× NOT AVAILABLE",20,34);
            hints.SetToolTip(slowEnabled,"Slows waypoint routes and automatic camera movement only. World animations keep their normal speed.");
            ButtonAt(slow,"Play transition",587,29,174,30,()=>{slowEnabled.Checked=true;slowElapsed=0;slowTransition=true;status.Text="Camera speed transition started.";});
            LabelAt(slow,"Start speed (x)",20,78,112);Number(slow,slowFrom,0.01M,1,0.25M,2,136,76,116);
            LabelAt(slow,"End speed (x)",270,78,116);Number(slow,slowTo,0.01M,1,1,2,390,76,116);
            LabelAt(slow,"Duration (s)",524,78,112);Number(slow,slowDuration,0.1M,3600,10,1,642,76,119);
            slowSlider.Minimum=1;slowSlider.Maximum=100;slowSlider.Value=25;slowSlider.SetBounds(20,115,742,35);slow.Controls.Add(slowSlider);
            slowReadout.SetBounds(20,161,740,43);slowReadout.ForeColor=Muted;slowReadout.Text="Slow motion is unavailable in this version.\nThe controls are disabled; waypoint routes use normal playback speed.";slow.Controls.Add(slowReadout);
            slowEnabled.CheckedChanged+=(s,e)=>{if(updating)return;Guard(()=>{cameraSlowEnabled=slowEnabled.Checked;slowTransition=false;if(cameraSlowEnabled)ApplySlowSpeed((float)slowFrom.Value);else slowScale=1;status.Text=cameraSlowEnabled?"Camera slow motion enabled; world animations remain unchanged.":"Normal camera motion restored.";});};
            slowFrom.ValueChanged+=(s,e)=>{if(updating)return;updating=true;slowSlider.Value=(int)(slowFrom.Value*100);updating=false;if(slowEnabled.Checked)Guard(()=>{slowTransition=false;ApplySlowSpeed((float)slowFrom.Value);});};
            slowSlider.ValueChanged+=(s,e)=>{if(!updating)slowFrom.Value=slowSlider.Value/100M;};
            foreach(Control control in slow.Controls)if(!(control is Label))control.Enabled=false;
            var time=Group(page,"World Time",826,336,282,177);
            LabelAt(time,"Hour",18,28,60);LabelAt(time,"Minute",95,28,65);
            Number(time,worldHour,0,23,12,0,18,53,64);Number(time,worldMinute,0,59,0,0,95,53,64);
            Check(time,worldTimeEnabled,"Override",174,55);
            timeReadout.SetBounds(160,27,116,22);timeReadout.AutoEllipsis=true;timeReadout.TextAlign=ContentAlignment.TopRight;timeReadout.ForeColor=Accent;timeReadout.Text="Not connected";time.Controls.Add(timeReadout);
            LabelAt(time,"Speed (x)",18,91,88);Number(time,worldSpeed,0,3600,0,1,110,87,143);
            hints.SetToolTip(worldSpeed,"0 freezes daylight. 1 follows real time; 60 advances one game minute per second. The server clock is unchanged.");
            ButtonAt(time,"Restore time",18,126,240,28,()=>{RequireCamera();camera.RestoreWorldTime();status.Text="Original daylight time restored.";});
            worldTimeEnabled.CheckedChanged+=(s,e)=>{if(updating)return;Guard(()=>{RequireCamera();if(worldTimeEnabled.Checked)ApplyWorldTime();else camera.RestoreWorldTime();status.Text=worldTimeEnabled.Checked?"Daylight time override enabled.":"Original daylight time restored.";});};
            worldHour.ValueChanged+=(s,e)=>{if(!updating&&worldTimeEnabled.Checked)Guard(ApplyWorldTime);};
            worldMinute.ValueChanged+=(s,e)=>{if(!updating&&worldTimeEnabled.Checked)Guard(ApplyWorldTime);};
        }

        private void BuildSpectate(Control page){
            var mode=Group(page,"Free Camera",18,18,340,242);Check(mode,spectate,"Enable free camera",20,31);spectate.CheckedChanged+=(s,e)=>{if(!updating)Guard(()=>{if(spectate.Checked)EnableCamera();else StopAll();});};
            Check(mode,showCharacter,"Show character",20,64);showCharacter.CheckedChanged+=(s,e)=>{if(updating)return;Guard(()=>{if(camera!=null&&camera.FreeCameraEnabled){CameraPoint pose=camera.CaptureFreePose();ReleaseCharacterInput();camera.DisableFreeCamera();EnableCamera();camera.SetFreePose(pose);}});};
            hints.SetToolTip(showCharacter,"Keep your character visible with a detached camera. WASD: fly; E/Space: up; Q/X: down; hold right mouse: look. Shift: faster; Ctrl: slower. Collision is unavailable in this mode. Start with your character fully visible.");
            Check(mode,collision,"Collision",20,97,true);collision.CheckedChanged+=(s,e)=>{if(!updating)Guard(ApplySettings);};
            Unavailable(Check(mode,new FantasyCheckBox(),"Look at target",20,130),"Target controls are not available yet.");Unavailable(Check(mode,new FantasyCheckBox(),"Follow target",20,163),"Target controls are not available yet.");LabelAt(mode,"Speed",20,207,110);Number(mode,speed,0.1M,500,10,1,168,203,145);speed.ValueChanged+=(s,e)=>{if(!updating)Guard(ApplySettings);};
            var automatic=Group(page,"Automatic Movement",18,274,340,222);string[] axes={"Pan X","Pan Y","Pan Z","Rotation °/s"};
            for(int i=0;i<4;i++){pan[i]=new FantasyNumber();LabelAt(automatic,axes[i],20,32+i*36,135);Number(automatic,pan[i],i==3?-180:-500,i==3?180:500,0,2,168,28+i*36,145);}Check(automatic,auto,"Enable auto movement",20,180);auto.CheckedChanged+=(s,e)=>{if(updating)return;bool enable=auto.Checked;Guard(()=>{if(enable){EnableCamera();motion.StopPath();}motion.AutoEnabled=enable;});};
            var paths=Group(page,"Waypoints",376,18,732,470);string[] commands={"+ Record","↻ Update","× Delete","→ Go to","▶ PLAY","■ STOP"};Action[] actions={RecordPoint,UpdatePoint,DeletePoint,FocusPoint,PlayPath,StopPath};for(int i=0;i<commands.Length;i++){var button=ButtonAt(paths,commands[i],17+i*117,29,109,35,actions[i]);if(i>=4){((StudioButton)button).Emphasis=i==4?"play":"stop";button.Font=new Font(Font,FontStyle.Bold);}}
            Check(paths,startFromSelected,"Start from selected waypoint",18,76);
            hints.SetToolTip(startFromSelected,"When checked, PLAY starts at the selected waypoint and follows the remaining points to the end. Loop repeats that section. Duration applies to that section.");
            grid.SetBounds(18,110,695,256);grid.AutoGenerateColumns=false;grid.AllowUserToAddRows=false;grid.AllowUserToDeleteRows=false;grid.MultiSelect=false;grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;grid.RowHeadersWidth=48;grid.BackgroundColor=FantasyArt.Base;grid.BorderStyle=BorderStyle.FixedSingle;grid.GridColor=FantasyArt.Edge;grid.EnableHeadersVisualStyles=false;
            grid.ColumnHeadersDefaultCellStyle=new DataGridViewCellStyle{BackColor=FantasyArt.ControlColor,ForeColor=Muted,SelectionBackColor=FantasyArt.ControlColor};grid.DefaultCellStyle=new DataGridViewCellStyle{BackColor=PanelColor,ForeColor=Ink,SelectionBackColor=FantasyArt.Accent,SelectionForeColor=FantasyArt.Base,Format="0.###"};grid.RowHeadersDefaultCellStyle=grid.ColumnHeadersDefaultCellStyle;grid.RowTemplate.Height=34;
            foreach(string field in new[]{"X","Y","Z","Fov","Yaw","Pitch"})grid.Columns.Add(new DataGridViewTextBoxColumn{DataPropertyName=field,HeaderText=field=="Fov"?"FOV °":field=="Yaw"?"Yaw °":field=="Pitch"?"Pitch °":field,Width=field=="X"||field=="Y"||field=="Z"?118:95,SortMode=DataGridViewColumnSortMode.NotSortable});grid.DataSource=points;grid.DataError+=(s,e)=>{e.ThrowException=false;status.Text="Enter a valid number for this waypoint.";};paths.Controls.Add(grid);
            ConfigureWaypointDragging();
            LabelAt(paths,"Duration (s)",18,387,120);Number(paths,duration,1,3600,20,1,145,382,100);Check(paths,loop,"Loop",270,385);Check(paths,bezier,"Smooth curve",365,385,true);hints.SetToolTip(bezier,"Smooth curve: intermediate waypoints shape a flowing camera path; the camera may not pass through them. Off: straight lines through every waypoint.");progress.SetBounds(18,430,690,24);progress.ForeColor=Muted;progress.Text="F7 records waypoints · Drag row numbers to reorder · Stop playback to edit.";paths.Controls.Add(progress);ButtonAt(page,"Load project",723,489,180,30,LoadProject);ButtonAt(page,"Save project",920,489,185,30,SaveProject);
        }

        private void BuildLight(Control page){
            var view=Group(page,"View Distance",18,18,1100,126);
            LabelAt(view,"Set distance",22,40,210);Number(view,viewDistance,3000,50000,7000,0,230,36,180);
            Check(view,viewEnabled,"Apply temporarily",443,40);
            ButtonAt(view,"Restore original",800,32,275,36,()=>{if(camera==null)return;camera.RestoreViewDistance();SyncMode();status.Text="Original view distance restored.";});
            viewReadout.SetBounds(22,73,1040,23);viewReadout.ForeColor=Accent;viewReadout.Text="Connect to WoW to see the current and effective view distance.";view.Controls.Add(viewReadout);
            LabelAt(view,"Area scaling and fog can limit visibility. F10 restores temporary settings.",22,99,1040,23).ForeColor=Muted;
            viewEnabled.CheckedChanged+=(s,e)=>{if(updating)return;bool enable=viewEnabled.Checked;Guard(()=>{if(camera==null)throw new InvalidOperationException("Connect to WoW first.");if(enable)camera.SetViewDistance((float)viewDistance.Value);else camera.RestoreViewDistance();});};
            viewDistance.ValueChanged+=(s,e)=>{if(!updating&&viewEnabled.Checked)Guard(()=>camera.SetViewDistance((float)viewDistance.Value));};
            var fog=Group(page,"Fog",18,165,350,348);
            LabelAt(fog,"Fog end distance",22,34,300);Number(fog,fogDistance,100,50000,20000,0,22,67,300);
            Check(fog,fogEnabled,"Extend fog distance",22,111);
            Check(fog,noFog,"Disable fog",22,142);
            noFog.CheckedChanged+=(s,e)=>{if(updating)return;bool disabled=noFog.Checked;Guard(()=>{if(camera==null)throw new InvalidOperationException("Connect to WoW first.");camera.SetFogSuppressed(disabled);status.Text=disabled?"Fog suppression enabled. Use Restore original fog to undo.":"Original fog strengths restored.";});};
            ButtonAt(fog,"Restore original fog",22,178,300,32,()=>{if(camera==null)return;camera.RestoreFog();SyncMode();status.Text="Original fog settings restored.";});
            fogReadout.SetBounds(22,223,310,28);fogReadout.ForeColor=Accent;fogReadout.Text="Connect to read the current fog.";fog.Controls.Add(fogReadout);
            LabelAt(fog,"Disable fog skips distance blending.\nF10 restores the original fog settings.",22,259,310,36).ForeColor=Muted;
            fogEnabled.CheckedChanged+=(s,e)=>{if(updating)return;bool enable=fogEnabled.Checked;Guard(()=>{if(camera==null)throw new InvalidOperationException("Connect to WoW first.");if(enable){camera.SetFogDistance((float)fogDistance.Value);status.Text="Fog distance enabled; allow a few seconds for the change to blend in.";}else{camera.RestoreFog();status.Text="Original fog settings restored.";}});};
            fogDistance.ValueChanged+=(s,e)=>{if(!updating&&fogEnabled.Checked)Guard(()=>camera.SetFogDistance((float)fogDistance.Value));};
            var light=Group(page,"Lighting & Sky",391,165,350,348);
            Check(light,lightingEnabled,"Apply lighting colors",22,33);
            ButtonAt(light,sunlightButton,"Sunlight color",22,67,300,32,()=>ChooseLightingColor(true));
            ButtonAt(light,ambientButton,"Ambient color",22,110,300,32,()=>ChooseLightingColor(false));
            ButtonAt(light,"Restore lighting",22,153,300,30,()=>{RequireCamera();camera.RestoreLighting();status.Text="Original lighting restored.";});
            LabelAt(light,"Sky / daylight presets",22,190,300).ForeColor=Muted;
            ButtonAt(light,"Noon",22,215,94,29,()=>ApplySkyPreset(12,0));
            ButtonAt(light,"Sunset",125,215,94,29,()=>ApplySkyPreset(18,30));
            ButtonAt(light,"Night",228,215,94,29,()=>ApplySkyPreset(0,0));
            LabelAt(light,"Presets change daylight; F10 restores it.\nEdit individual sky colors on the right.",22,254,310,38).ForeColor=Muted;
            lightingEnabled.CheckedChanged+=(s,e)=>{if(updating)return;Guard(()=>{RequireCamera();if(lightingEnabled.Checked)camera.SetLighting(unchecked((uint)sunlightColor.ToArgb()),unchecked((uint)ambientColor.ToArgb()));else camera.RestoreLighting();status.Text=lightingEnabled.Checked?"Lighting colors enabled.":"Original lighting restored.";});};
            UpdateLightingSwatches();
            var sky=Group(page,"Sky & Clouds",764,165,354,348);
            Check(sky,skyEnabled,"Apply sky colors",22,30);
            for(int i=0;i<6;i++){
                int index=i;var button=skyButtons[i]=new StudioButton();
                ButtonAt(sky,button,"Sky color "+i,22+(i%2)*159,62+(i/2)*36,146,28,()=>ChooseSkyColor(false,index));
                hints.SetToolTip(button,"Color band "+i+" of the outdoor sky gradient. Enable Apply sky colors to use it.");
            }
            Check(sky,cloudsEnabled,"Apply cloud colors",22,174);
            for(int i=0;i<3;i++){
                int index=i;var button=cloudButtons[i]=new StudioButton();
                ButtonAt(sky,button,"Cloud color "+(i+1),22,206+i*36,305,28,()=>ChooseSkyColor(true,index));
            }
            ButtonAt(sky,"Restore sky & clouds",22,314,305,26,()=>{RequireCamera();camera.RestoreSkyColors();status.Text="Original sky and cloud colors restored.";});
            skyEnabled.CheckedChanged+=(s,e)=>{if(!updating)Guard(ApplySkyColors);};
            cloudsEnabled.CheckedChanged+=(s,e)=>{if(!updating)Guard(ApplySkyColors);};
            hints.SetToolTip(skyEnabled,"Applies the six sky bands across areas and daylight times until restored. F10 restores all original colors.");
            hints.SetToolTip(cloudsEnabled,"Applies the three outdoor cloud colors until restored. Skybox artwork and cloud coverage are unchanged.");
            UpdateSkySwatches();
        }
        private void BuildWeather(Control page){
            var weather=Group(page,"Rain & Snow",18,18,700,330);
            ButtonAt(weather,rainButton,"Rain",22,43,190,44,()=>ChooseWeather(1));
            ButtonAt(weather,snowButton,"Snow",234,43,190,44,()=>ChooseWeather(2));rainButton.Selected=true;
            LabelAt(weather,"Intensity (%)",22,114,160);Number(weather,weatherIntensity,0,100,65,0,190,110,120);
            weatherSlider.Minimum=0;weatherSlider.Maximum=100;weatherSlider.Value=65;weatherSlider.SetBounds(22,154,650,35);weather.Controls.Add(weatherSlider);
            Check(weather,weatherEnabled,"Apply weather override",22,210);
            ButtonAt(weather,"Restore original weather",430,205,240,36,()=>{RequireCamera();weatherDirty=false;camera.RestoreWeather();status.Text="Original weather restored.";});
            weatherEnabled.CheckedChanged+=(s,e)=>{if(updating)return;Guard(()=>{RequireCamera();weatherDirty=false;if(weatherEnabled.Checked)ApplyWeather();else{camera.RestoreWeather();status.Text="Original weather restored.";}});};
            weatherIntensity.ValueChanged+=(s,e)=>{if(updating)return;updating=true;try{weatherSlider.Value=(int)weatherIntensity.Value;}finally{updating=false;}ScheduleWeather();};
            weatherSlider.ValueChanged+=(s,e)=>{if(!updating)weatherIntensity.Value=weatherSlider.Value;};
            var thunder=Group(page,"Thunder",18,366,700,150);
            Check(thunder,thunderEnabled,"Thunder & lightning",22,43);
            thunderEnabled.AutoSize=false;thunderEnabled.Size=new Size(300,25);
            thunderEnabled.CheckedChanged+=(s,e)=>{if(updating)return;Guard(()=>{RequireCamera();camera.SetThunder(thunderEnabled.Checked);status.Text=thunderEnabled.Checked?"Thunder and lightning enabled · F10 restores original weather.":"Thunder stopped · the current sound may finish playing.";});};
            LabelAt(thunder,"Native thunder sounds and lightning flashes.\nUses the game's sound effects volume; works with Rain or Snow.",22,83,650,46).ForeColor=Muted;
            var info=Group(page,"Weather Controls",740,18,378,330);
            LabelAt(info,"Choose Rain or Snow, set the intensity,\nthen enable Apply weather override.\n\nChanges use the client's outdoor effects.\nThey may be hidden indoors or behind\nexisting weather and skybox artwork.\n\nF10, closing the tool and loading a project\nrestore the original weather.\n\nChanging maps releases the override.\nEnable it again in the new area.",22,40,337,270).ForeColor=Muted;
        }
        private void ChooseWeather(int kind){weatherKind=kind;rainButton.Selected=kind==1;snowButton.Selected=kind==2;rainButton.Invalidate();snowButton.Invalidate();ScheduleWeather();if(!weatherEnabled.Checked)status.Text=WeatherName(kind)+" selected · enable Apply weather override to use it.";}
        private static string WeatherName(int kind){return kind==0?"Clear":kind==1?"Rain":kind==2?"Snow":kind==3?"Sand":"Other";}
        private void ScheduleWeather(){if(weatherEnabled.Checked){weatherDirty=true;weatherChangeAt=clock.Elapsed.TotalSeconds+0.15;}}
        private void ApplyWeather(){RequireCamera();camera.SetWeather(weatherKind,(float)weatherIntensity.Value/100);weatherDirty=false;status.Text=string.Format("{0} requested at {1:0}% intensity · F10 restores original weather.",WeatherName(weatherKind),weatherIntensity.Value);}
        private void UpdateWeather(double now){
            WeatherSnapshot weather=camera.CaptureWeather();
            camera.CaptureThunder();
            if(weatherEnabled.Checked!=camera.WeatherOverridden||thunderEnabled.Checked!=camera.ThunderOverridden)SyncMode();
            if(!camera.WeatherOverridden){weatherDirty=false;return;}
            if(!weather.Pending&&(weather.Kind!=weatherKind||Math.Abs(weather.Intensity-(float)weatherIntensity.Value/100)>0.001)&&!weatherDirty){weatherDirty=true;weatherChangeAt=now+0.15;}
            if(weatherDirty&&now>=weatherChangeAt&&!weather.Pending)ApplyWeather();
        }
        private void Guard(Action action){try{action();}catch(Exception ex){status.Text=ex.Message;}finally{SyncMode();}}
        private void LogEvent(string text){if(sessionLog==null)return;try{File.AppendAllText(sessionLog,DateTime.UtcNow.ToString("o")+"  "+text+Environment.NewLine);}catch(IOException){}catch(UnauthorizedAccessException){}}
        private void RequireCamera(){if(camera==null)throw new InvalidOperationException("Connect to WoW first.");}
        private void UpdateLightingSwatches(){sunlightButton.Swatch=sunlightColor;ambientButton.Swatch=ambientColor;sunlightButton.Invalidate();ambientButton.Invalidate();}
        private void ChooseLightingColor(bool sunlight){
            using(var dialog=new ColorDialog{FullOpen=true,Color=sunlight?sunlightColor:ambientColor}){
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                if(sunlight)sunlightColor=dialog.Color;else ambientColor=dialog.Color;UpdateLightingSwatches();
                if(lightingEnabled.Checked){RequireCamera();camera.SetLighting(unchecked((uint)sunlightColor.ToArgb()),unchecked((uint)ambientColor.ToArgb()));status.Text="Lighting color updated.";}
            }
        }
        private void ApplySkyPreset(int hour,int minute){RequireCamera();updating=true;try{worldHour.Value=hour;worldMinute.Value=minute;worldSpeed.Value=0;}finally{updating=false;}ApplyWorldTime();status.Text=string.Format("Sky daylight set to {0:00}:{1:00}. F10 restores it.",hour,minute);}
        private void UpdateSkySwatches(){for(int i=0;i<6;i++){skyButtons[i].Swatch=skyColors[i];skyButtons[i].Invalidate();}for(int i=0;i<3;i++){cloudButtons[i].Swatch=cloudColors[i];cloudButtons[i].Invalidate();}}
        private void ApplySkyColors(){
            RequireCamera();camera.SetSkyColors(skyColors.Select(c=>unchecked((uint)c.ToArgb())).ToArray(),cloudColors.Select(c=>unchecked((uint)c.ToArgb())).ToArray(),skyEnabled.Checked,cloudsEnabled.Checked);
            status.Text=skyEnabled.Checked||cloudsEnabled.Checked?"Sky/cloud colors applied · F10 restores original colors.":"Original sky and cloud colors restored.";
        }
        private void ChooseSkyColor(bool cloud,int index){
            Color[] colors=cloud?cloudColors:skyColors;
            using(var dialog=new ColorDialog{FullOpen=true,Color=colors[index]}){
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                colors[index]=dialog.Color;UpdateSkySwatches();
                if(cloud?cloudsEnabled.Checked:skyEnabled.Checked)ApplySkyColors();
            }
        }
        private void ApplyWorldTime(){RequireCamera();worldMinutes=(double)(worldHour.Value*60+worldMinute.Value);camera.SetWorldTime((float)worldMinutes);LogEvent(string.Format("Daylight requested: {0:00}:{1:00}; speed {2}x.",worldHour.Value,worldMinute.Value,worldSpeed.Value));}
        private void ApplySlowSpeed(float scale){if(float.IsNaN(scale)||float.IsInfinity(scale)||scale<0.01f||scale>1)throw new ArgumentOutOfRangeException(nameof(scale));slowScale=scale;slowReadout.Text=string.Format("Camera motion: {0:0.00}x · Routes and auto movement only.\nWorld animations stay at normal speed. F10 restores camera motion.",scale);}
        private void Attach(){
            if(Process.GetProcessesByName("Machinae.Forever").Length>0)throw new InvalidOperationException("Close the older camera controller to free F8/F10.");StopAll();timer.Stop();if(camera!=null){camera.Dispose();camera=null;}
            var candidates=Process.GetProcessesByName("WowB").Where(p=>p.MainModule!=null&&FileVersionInfo.GetVersionInfo(p.MainModule.FileName).FileVersion=="1.60.1.70450").ToArray();if(candidates.Length!=1)throw new InvalidOperationException("Not connected.");
            target=candidates[0];camera=new NativeCamera(target);updating=true;fov.Value=Math.Max(20,Math.Min(140,(decimal)(camera.Capture().Fov*180/Math.PI)));fovSlider.Value=(int)fov.Value;viewDistance.Value=(decimal)camera.ReadViewDistance();LightingSnapshot lighting=camera.CaptureLighting();sunlightColor=Color.FromArgb(unchecked((int)lighting.Sunlight));ambientColor=Color.FromArgb(unchecked((int)lighting.Ambient));UpdateLightingSwatches();SkySnapshot sky=camera.CaptureSky();for(int i=0;i<6;i++)skyColors[i]=Color.FromArgb(unchecked((int)sky.Sky[i]));for(int i=0;i<3;i++)cloudColors[i]=Color.FromArgb(unchecked((int)sky.Clouds[i]));UpdateSkySwatches();int daylight=(int)camera.ReadWorldTime()%1440;worldHour.Value=daylight/60;worldMinute.Value=daylight%60;updating=false;timer.Start();lastTick=clock.Elapsed.TotalSeconds;status.Text="Connected · F8 free camera · F7 record waypoint · F10 return to player";
        }
        private void EnableCamera(){
            if(camera==null)throw new InvalidOperationException("Connect to WoW Forever first.");
            try{camera.EnableFreeCamera(showCharacter.Checked);ApplySettings();if(camera.CharacterVisibleCamera&&characterInput==null)characterInput=new DetachedCameraInput(()=>{GetWindowThreadProcessId(GetForegroundWindow(),out uint foreground);return camera!=null&&camera.CharacterVisibleCamera&&target!=null&&foreground==(uint)target.Id;});}catch{ReleaseCharacterInput();camera.DisableFreeCamera();throw;}
            SyncMode();status.Text=camera.CharacterVisibleCamera?"Character camera active · WASD / Q E · Hold right mouse to look · F10 returns to player.":"Free camera active · F7 records a waypoint · F10 returns to player.";
        }
        private void ToggleCamera(){if(camera!=null&&camera.FreeCameraEnabled)StopAll();else EnableCamera();}
        private void SyncMode(){
            updating=true;
            try {
                spectate.Checked=camera!=null&&camera.FreeCameraEnabled;auto.Checked=motion.AutoEnabled;collision.Enabled=!showCharacter.Checked;if(spectate.Checked)showCharacter.Checked=camera.CharacterVisibleCamera;
                viewEnabled.Checked=camera!=null&&camera.ViewDistanceOverridden;fogEnabled.Checked=camera!=null&&camera.FogOverridden;noFog.Checked=camera!=null&&camera.FogSuppressed;fogDistance.Enabled=!noFog.Checked;
                worldTimeEnabled.Checked=camera!=null&&camera.WorldTimeOverridden;slowEnabled.Checked=cameraSlowEnabled;
                lightingEnabled.Checked=camera!=null&&camera.LightingOverridden;
                skyEnabled.Checked=camera!=null&&camera.SkyColorsOverridden;cloudsEnabled.Checked=camera!=null&&camera.CloudColorsOverridden;
                weatherEnabled.Checked=camera!=null&&camera.WeatherOverridden;
                thunderEnabled.Checked=camera!=null&&camera.ThunderOverridden;
                if(!slowEnabled.Checked){slowScale=1;slowTransition=false;}
                if(camera!=null)try{uint flags=camera.ReadRendering();for(int i=0;i<renderToggles.Length;i++)if(renderToggles[i]!=null)renderToggles[i].Checked=(flags&NativeCamera.RenderBits[i])!=0;}catch{}
            }finally{updating=false;}
            grid.ReadOnly=motion.Playing;
            startFromSelected.Enabled=!motion.Playing;
        }
        private void ReleaseCharacterInput(){if(characterInput!=null){characterInput.Dispose();characterInput=null;}}
        private void StopAll(){ReleaseCharacterInput();motion.Stop();weatherDirty=false;cameraSlowEnabled=false;slowTransition=false;slowScale=1;if(camera!=null){camera.RestoreVisuals();camera.RestoreFog();camera.DisableFreeCamera();camera.RestoreViewDistance();}SyncMode();status.Text="Camera and visual settings restored · movement stopped.";}
        private void ApplySettings(){if(camera==null||!camera.FreeCameraEnabled)return;camera.SetFreeCameraFov((float)fov.Value);camera.SetFreeCameraSpeed((float)speed.Value);camera.SetFreeCameraCollision(collision.Checked);}
        private CameraPoint CurrentPoint(){if(camera==null)throw new InvalidOperationException("Connect to WoW first.");return camera.FreeCameraEnabled?camera.CaptureFreePose():CameraPoint.FromSnapshot(camera.Capture());}
        private int SelectedIndex(){if(grid.CurrentRow==null||grid.CurrentRow.Index>=points.Count)throw new InvalidOperationException("Select a waypoint first.");return grid.CurrentRow.Index;}
        private void ConfigureWaypointDragging(){
            grid.AllowDrop=true;
            hints.SetToolTip(grid,"Drag the row number on the left to reorder waypoints. Stop playback before reordering. Click a value to edit it.");
            grid.CellPainting+=(s,e)=>{
                if(waypointDragToken!=null&&e.RowIndex>=0&&e.RowIndex<points.Count&&ReferenceEquals(points[e.RowIndex],draggedWaypoint)){
                    using(var brush=new SolidBrush(FantasyArt.Edge))e.Graphics.FillRectangle(brush,Rectangle.Intersect(e.CellBounds,e.ClipBounds));
                    e.CellStyle.ForeColor=FantasyArt.TextColor;e.CellStyle.SelectionForeColor=FantasyArt.TextColor;
                    e.Paint(e.ClipBounds,DataGridViewPaintParts.ContentForeground|DataGridViewPaintParts.Border|DataGridViewPaintParts.Focus);e.Handled=true;
                }
            };
            grid.RowPostPaint+=(s,e)=>{
                var area=new Rectangle(e.RowBounds.Left+3,e.RowBounds.Top,grid.RowHeadersWidth-9,e.RowBounds.Height);
                TextRenderer.DrawText(e.Graphics,(e.RowIndex+1).ToString(),grid.Font,area,Muted,TextFormatFlags.Right|TextFormatFlags.VerticalCenter);
                using(var pen=new Pen(FantasyArt.TextColor))for(int offset=-3;offset<=3;offset+=3)e.Graphics.DrawLine(pen,e.RowBounds.Left+6,e.RowBounds.Top+e.RowBounds.Height/2+offset,e.RowBounds.Left+14,e.RowBounds.Top+e.RowBounds.Height/2+offset);
                if(waypointDragToken!=null&&e.RowIndex<points.Count&&ReferenceEquals(points[e.RowIndex],draggedWaypoint)){
                    using(var pen=new Pen(FantasyArt.TextColor,2))e.Graphics.DrawRectangle(pen,1,e.RowBounds.Top+1,grid.ClientSize.Width-3,e.RowBounds.Height-2);
                }
            };
            grid.MouseDown+=(s,e)=>{
                draggedWaypoint=null;waypointDragToken=null;
                if(e.Button!=MouseButtons.Left||motion.Playing)return;
                var hit=grid.HitTest(e.X,e.Y);
                if(hit.Type!=DataGridViewHitTestType.RowHeader||hit.RowIndex<0||hit.RowIndex>=points.Count)return;
                draggedWaypoint=points[hit.RowIndex];waypointDragOrigin=e.Location;
            };
            grid.MouseMove+=(s,e)=>{
                if(draggedWaypoint==null||e.Button!=MouseButtons.Left||motion.Playing)return;
                Size threshold=SystemInformation.DragSize;
                if(new Rectangle(waypointDragOrigin.X-threshold.Width/2,waypointDragOrigin.Y-threshold.Height/2,threshold.Width,threshold.Height).Contains(e.Location))return;
                if(!grid.EndEdit()){draggedWaypoint=null;status.Text="Correct the invalid waypoint value before dragging.";return;}
                waypointDragToken=Guid.NewGuid().ToString("N");var data=new DataObject();data.SetData(WaypointDragFormat,false,waypointDragToken);
                grid.Invalidate();
                try{grid.DoDragDrop(data,DragDropEffects.Move);}finally{draggedWaypoint=null;waypointDragToken=null;waypointDropSlot=-1;if(progress.Text.StartsWith("Moving waypoint "))progress.Text="Drag finished · Save project to keep any changes.";grid.Invalidate();}
            };
            grid.MouseUp+=(s,e)=>{if(waypointDragToken==null)draggedWaypoint=null;};
            grid.DragOver+=(s,e)=>{
                e.Effect=DragDropEffects.None;
                if(motion.Playing||draggedWaypoint==null||waypointDragToken==null||!e.Data.GetDataPresent(WaypointDragFormat,false)||!string.Equals(e.Data.GetData(WaypointDragFormat,false) as string,waypointDragToken))return;
                Point mouse=grid.PointToClient(new Point(e.X,e.Y));
                if(clock.Elapsed.TotalSeconds-lastWaypointScroll>0.08&&grid.Rows.Count>0){
                    int first=grid.FirstDisplayedScrollingRowIndex;
                    if(mouse.Y<grid.ColumnHeadersHeight+20&&first>0)grid.FirstDisplayedScrollingRowIndex=first-1;
                    else if(mouse.Y>grid.ClientSize.Height-24&&first>=0&&first<grid.Rows.Count-1)grid.FirstDisplayedScrollingRowIndex=first+1;
                    lastWaypointScroll=clock.Elapsed.TotalSeconds;
                }
                waypointDropSlot=WaypointDropSlot(mouse.Y);e.Effect=DragDropEffects.Move;
                int from=points.IndexOf(draggedWaypoint),to=waypointDropSlot>from?waypointDropSlot-1:waypointDropSlot;
                progress.Text=string.Format("Moving waypoint {0} → position {1} · Release to drop · Esc cancels",from+1,to+1);grid.Invalidate();
            };
            grid.DragLeave+=(s,e)=>{waypointDropSlot=-1;grid.Invalidate();};
            grid.DragDrop+=(s,e)=>{
                if(e.Effect!=DragDropEffects.Move||draggedWaypoint==null||waypointDropSlot<0)return;
                Guard(()=>MoveWaypoint(draggedWaypoint,waypointDropSlot));waypointDropSlot=-1;grid.Invalidate();
            };
            grid.Paint+=(s,e)=>{
                if(waypointDropSlot<0||grid.Rows.Count==0)return;
                int index=Math.Min(waypointDropSlot,grid.Rows.Count-1);Rectangle row=grid.GetRowDisplayRectangle(index,false);
                int y=waypointDropSlot==grid.Rows.Count?row.Bottom:row.Top;
                if(row.Height<=0&&waypointDropSlot>0){row=grid.GetRowDisplayRectangle(waypointDropSlot-1,false);y=row.Bottom;}
                if(row.Height<=0)return;
                y=Math.Max(grid.ColumnHeadersHeight+2,Math.Min(grid.ClientSize.Height-3,y));
                using(var glow=new Pen(Color.FromArgb(110,FantasyArt.TextColor),9))e.Graphics.DrawLine(glow,5,y,grid.ClientSize.Width-5,y);
                using(var pen=new Pen(FantasyArt.TextColor,4))e.Graphics.DrawLine(pen,5,y,grid.ClientSize.Width-5,y);
                using(var pen=new Pen(FantasyArt.TextColor,1))e.Graphics.DrawLine(pen,5,y,grid.ClientSize.Width-5,y);
                using(var brush=new SolidBrush(FantasyArt.TextColor)){
                    e.Graphics.FillPolygon(brush,new[]{new Point(2,y-7),new Point(13,y),new Point(2,y+7)});
                    e.Graphics.FillPolygon(brush,new[]{new Point(grid.ClientSize.Width-2,y-7),new Point(grid.ClientSize.Width-13,y),new Point(grid.ClientSize.Width-2,y+7)});
                }
                int from=points.IndexOf(draggedWaypoint),to=waypointDropSlot>from?waypointDropSlot-1:waypointDropSlot;
                var badge=new Rectangle(grid.ClientSize.Width-255,Math.Max(grid.ColumnHeadersHeight+2,Math.Min(grid.ClientSize.Height-30,y-30)),246,27);
                using(var brush=new SolidBrush(FantasyArt.Base))e.Graphics.FillRectangle(brush,badge);
                using(var pen=new Pen(FantasyArt.TextColor))e.Graphics.DrawRectangle(pen,badge);
                TextRenderer.DrawText(e.Graphics,string.Format("MOVE {0} → POSITION {1}",from+1,to+1),grid.Font,badge,FantasyArt.TextColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
            };
        }
        private int WaypointDropSlot(int y){
            int afterVisible=points.Count;
            for(int i=0;i<grid.Rows.Count;i++){
                Rectangle row=grid.GetRowDisplayRectangle(i,false);
                if(row.Height<=0)continue;
                if(y<row.Top+row.Height/2)return i;
                if(y<=row.Bottom)return i+1;
                afterVisible=i+1;
            }
            return afterVisible;
        }
        private void MoveWaypoint(CameraPoint point,int insertionSlot){
            if(motion.Playing)throw new InvalidOperationException("Stop route playback before reordering waypoints.");
            int from=points.IndexOf(point);
            if(from<0||insertionSlot<0||insertionSlot>points.Count)throw new InvalidOperationException("The waypoint order changed. Drag the row again.");
            if(!grid.EndEdit())throw new InvalidOperationException("Correct the invalid waypoint value before dragging.");
            int to=insertionSlot>from?insertionSlot-1:insertionSlot;
            if(to==from)return;
            int column=grid.CurrentCell==null?0:Math.Max(0,grid.CurrentCell.ColumnIndex);
            points.RemoveAt(from);points.Insert(to,point);grid.ClearSelection();grid.CurrentCell=grid.Rows[to].Cells[column];grid.Rows[to].Selected=true;
            progress.Text="Route order changed · Save project to keep the new order.";
            status.Text=string.Format("Waypoint moved from {0} to {1}.",from+1,to+1);
        }
        private void RecordPoint(){if(points.Count>=128)throw new InvalidOperationException("A route can contain up to 128 waypoints.");points.Add(CurrentPoint());grid.CurrentCell=grid.Rows[points.Count-1].Cells[0];status.Text="Waypoint "+points.Count+" recorded.";}
        private void UpdatePoint(){int index=SelectedIndex();points[index]=CurrentPoint();status.Text="Waypoint updated.";}
        private void DeletePoint(){StopPath();points.RemoveAt(SelectedIndex());}
        private void FocusPoint(){if(!grid.EndEdit())throw new InvalidOperationException("Correct the invalid value in the waypoint table first.");var point=points[SelectedIndex()].Copy();point.Validate();StopPath();EnableCamera();camera.SetFreePose(point);status.Text="Camera moved to the selected waypoint.";}
        private System.Collections.Generic.List<CameraPoint> PlaybackRoute(){
            int start=startFromSelected.Checked?SelectedIndex():0;
            if(startFromSelected.Checked&&start>=points.Count-1)throw new InvalidOperationException("Select a waypoint before the last one; playback needs at least two points.");
            return points.Skip(start).Select(p=>p.Copy()).ToList();
        }
        private void PlayPath(){
            if(!grid.EndEdit())throw new InvalidOperationException("Correct the invalid value in the waypoint table first.");
            var route=PlaybackRoute();int first=startFromSelected.Checked?SelectedIndex()+1:1;
            motion.Start(route,(double)duration.Value,loop.Checked,bezier.Checked);
            try{EnableCamera();camera.SetFreePose(route[0]);lastTick=clock.Elapsed.TotalSeconds;timer.Start();}catch{motion.Stop();throw;}
            SyncMode();status.Text=string.Format("Route playing from waypoint {0} · F10 stops and returns to player.",first);
        }
        private void StopPath(){motion.Stop();SyncMode();progress.Text="Route stopped; free camera remains available.";}
        private void TickCamera(){
            double now=clock.Elapsed.TotalSeconds,delta=now-lastTick;lastTick=now;
            try{
                if(slowTransition){
                    slowElapsed+=Math.Max(0,delta);double fraction=Math.Min(1,slowElapsed/(double)slowDuration.Value);
                    float scale=(float)((double)slowFrom.Value+((double)slowTo.Value-(double)slowFrom.Value)*fraction);
                    ApplySlowSpeed(scale);updating=true;slowSlider.Value=(int)Math.Round(scale*100);updating=false;
                    if(fraction>=1){slowTransition=false;status.Text="Camera speed transition complete.";}
                }
                if(camera==null)return;
                if(camera.WorldTimeOverridden&&worldSpeed.Value>0){worldMinutes=(worldMinutes+Math.Max(0,delta)*(double)worldSpeed.Value/60)%1440;camera.SetWorldTime((float)worldMinutes);}
                if(camera.FreeCameraEnabled){camera.CheckFreeCamera();motion.PanX=(float)pan[0].Value;motion.PanY=(float)pan[1].Value;motion.PanZ=(float)pan[2].Value;motion.Rotation=(float)pan[3].Value;CameraPoint current=camera.CaptureFreePose();CameraPoint manual=characterInput==null?null:characterInput.Advance(current,delta,(float)speed.Value);if(manual!=null)motion.Stop();CameraPoint next=manual??motion.Advance(delta*slowScale,current);if(next!=null)camera.SetFreePose(next);if(motion.Playing)progress.Text=string.Format("Route {0:P0}   ·   {1:F1} s",motion.Progress,motion.Progress*motion.Duration);else if(grid.ReadOnly){grid.ReadOnly=false;startFromSelected.Enabled=true;progress.Text="Route complete.";}}
                if(now-lastEnvironmentRead>=0.5){lastEnvironmentRead=now;try{viewReadout.Text=string.Format("Set: {0:N0}   /   Effective in this area: {1:N0}   /   {2}",camera.ReadViewDistance(),camera.ReadEffectiveViewDistance(),camera.ViewDistanceOverridden?"temporary override active":"original setting");int daylight=(int)camera.ReadWorldTime()%1440;timeReadout.Text=string.Format("Now {0:00}:{1:00}",daylight/60,daylight%60);camera.CaptureLighting();camera.CaptureSky();try{UpdateWeather(now);}catch(Exception weatherError){status.Text="Weather: "+weatherError.Message;}FogSnapshot fog=camera.CaptureFog();fogReadout.Text=camera.FogSuppressed?string.Format("Fog strength: {0:0.###}",fog.Density):string.Format("Current fog end: {0:N0}",fog.End);if(fogEnabled.Checked!=camera.FogOverridden||noFog.Checked!=camera.FogSuppressed||lightingEnabled.Checked!=camera.LightingOverridden||skyEnabled.Checked!=camera.SkyColorsOverridden||cloudsEnabled.Checked!=camera.CloudColorsOverridden)SyncMode();}catch{viewReadout.Text="Environment readings are temporarily unavailable.";fogReadout.Text="Fog readings are temporarily unavailable.";}}CameraSnapshot snapshot=camera.Capture();coordinates.Text=string.Format("X {0:F2}     Y {1:F2}     Z {2:F2}     |     FOV {3:F1}°     |     {4} waypoints",snapshot.X,snapshot.Y,snapshot.Z,snapshot.Fov*180/Math.PI,points.Count);
            }catch(Exception ex){try{StopAll();}catch{}timer.Stop();status.Text="Stopped: "+ex.Message;}
        }
        private CameraProject Project(){if(!grid.EndEdit())throw new InvalidOperationException("Correct the invalid value in the waypoint table first.");return new CameraProject{FogDistance=(float)fogDistance.Value,ViewDistance=(float)viewDistance.Value,Speed=(float)speed.Value,Fov=(float)fov.Value,Collision=collision.Checked,ShowCharacter=showCharacter.Checked,AlwaysOnTop=onTop.Checked,PanX=(float)pan[0].Value,PanY=(float)pan[1].Value,PanZ=(float)pan[2].Value,Rotation=(float)pan[3].Value,Duration=(float)duration.Value,Loop=loop.Checked,Bezier=bezier.Checked,Points=points.Select(p=>p.Copy()).ToList()};}
        private void SaveProject(){StopPath();using(var dialog=new SaveFileDialog{Filter="Camera preset (*.xml)|*.xml",DefaultExt="xml",FileName="camera-route.xml"})if(dialog.ShowDialog(this)==DialogResult.OK){Project().Save(dialog.FileName);status.Text="Project and route saved.";}}
        private void LoadProject(){
            StopPath();using(var dialog=new OpenFileDialog{Filter="Camera preset (*.xml)|*.xml"}){
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;CameraProject data=CameraProject.Load(dialog.FileName);StopPath();ReleaseCharacterInput();if(camera!=null)camera.DisableFreeCamera();cameraSlowEnabled=false;slowTransition=false;slowScale=1;if(camera!=null){camera.RestoreVisuals();camera.RestoreFog();camera.RestoreViewDistance();}updating=true;
                try{fogDistance.Value=(decimal)data.FogDistance;viewDistance.Value=(decimal)data.ViewDistance;speed.Value=(decimal)data.Speed;fov.Value=(decimal)data.Fov;fovSlider.Value=(int)data.Fov;collision.Checked=data.Collision;showCharacter.Checked=data.ShowCharacter;onTop.Checked=data.AlwaysOnTop;pan[0].Value=(decimal)data.PanX;pan[1].Value=(decimal)data.PanY;pan[2].Value=(decimal)data.PanZ;pan[3].Value=(decimal)data.Rotation;duration.Value=(decimal)data.Duration;loop.Checked=data.Loop;bezier.Checked=data.Bezier;points.Clear();foreach(var point in data.Points)points.Add(point);}finally{updating=false;}
                SyncMode();ApplySettings();status.Text="Project loaded. Start routes and enable visual overrides manually; auto movement is off.";
            }
        }
        protected override void WndProc(ref Message message){
            if(message.Msg==0x0312){int id=message.WParam.ToInt32();if(id==StopHotkey){Guard(StopAll);return;}GetWindowThreadProcessId(GetForegroundWindow(),out uint foreground);if(foreground==(uint)Process.GetCurrentProcess().Id||target!=null&&foreground==(uint)target.Id){if(id==ToggleHotkey)Guard(ToggleCamera);else if(id==RecordHotkey)Guard(RecordPoint);return;}}base.WndProc(ref message);
        }
        protected override void OnFormClosing(FormClosingEventArgs e){
            timer.Stop();try{StopAll();if(camera!=null){camera.Dispose();camera=null;}}catch(Exception ex){status.Text="Restore failed: "+ex.Message;e.Cancel=true;timer.Start();return;}foreach(int id in new[]{ToggleHotkey,StopHotkey,RecordHotkey})UnregisterHotKey(Handle,id);base.OnFormClosing(e);
        }
    }
}





















