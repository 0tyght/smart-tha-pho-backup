using System;
using System.IO;
using System.Drawing;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using System.Collections.Generic;

namespace SmartThaPho.Backup {
  internal sealed class BackupController {
    public readonly string Root;
    readonly string statusOverride;
    public Process Operation;
    public BackupController(string root,string statusOverride=null) { Root=root;this.statusOverride=statusOverride; }
    public bool Busy { get { return Operation!=null && !Operation.HasExited; } }
    public void Run(string action) {
      if(Busy) {if(action=="Stop")File.WriteAllText(Path.Combine(Root,".runtime","backup","cancel-start"),"cancel");return;}
      if(action=="Start"){string cancel=Path.Combine(Root,".runtime","backup","cancel-start");if(File.Exists(cancel))File.Delete(cancel);}
      string script=Path.Combine(Root,"scripts","backup","server-control.ps1");
      if(!File.Exists(script)) throw new FileNotFoundException("ไม่พบตัวควบคุมเซิร์ฟเวอร์",script);
      var info=new ProcessStartInfo("powershell.exe","-NoProfile -ExecutionPolicy Bypass -File \""+script+"\" -Action "+action);
      info.WorkingDirectory=Root;info.UseShellExecute=false;info.CreateNoWindow=true;info.WindowStyle=ProcessWindowStyle.Hidden;
      Operation=Process.Start(info);
    }
    public Dictionary<string,object> Read() {
      string file=statusOverride??Path.Combine(Root,".runtime","backup","status.json");
      if(!File.Exists(file)) return new Dictionary<string,object>();
      return new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(file));
    }
    public void Probe(Action<bool,bool> done) {
      ThreadPool.QueueUserWorkItem(delegate {
        bool apiReady=false,tunnelRunning=false;
        try {
          var request=(System.Net.HttpWebRequest)System.Net.WebRequest.Create("http://127.0.0.1:4200/api/health");request.Timeout=1200;request.ReadWriteTimeout=1200;
          using(var response=request.GetResponse())using(var reader=new StreamReader(response.GetResponseStream())){
            var body=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(reader.ReadToEnd());
            apiReady=body.ContainsKey("database")&&Convert.ToString(body["database"])=="ready";
          }
          string state=Path.Combine(Root,".runtime","backup","processes.json");
          var processes=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(state));
          var record=processes["tunnel"] as Dictionary<string,object>;
          if(record!=null){using(var process=Process.GetProcessById(Convert.ToInt32(record["id"]))){tunnelRunning=!process.HasExited&&process.StartTime.ToUniversalTime().ToString("o")==Convert.ToString(record["startTime"]);}}
        }catch{}
        done(apiReady,tunnelRunning);
      });
    }
  }
  internal sealed class MonitorForm : Form {
    readonly BackupController controller;
    readonly Label message=new Label(),database=new Label(),api=new Label(),online=new Label();
    readonly TextBox url=new TextBox();
    readonly Button start=new Button(),stop=new Button(),open=new Button(),copy=new Button();
    readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
    readonly ToolTip details=new ToolTip();
    string direct="";
    bool probing;
    public MonitorForm(BackupController controller, bool autoStart = true) {
      this.controller=controller; Text="Smart Tha Pho — เซิร์ฟเวอร์สำรอง";StartPosition=FormStartPosition.CenterScreen;
      ClientSize=new Size(780,510);MinimumSize=new Size(740,540);Font=new Font("Tahoma",10);AutoScaleMode=AutoScaleMode.Dpi;
      BackColor=SystemColors.Window;
      var layout=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=1,RowCount=8};
      layout.RowStyles.Add(new RowStyle(SizeType.Absolute,40));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,110));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,32));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,40));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,56));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));
      layout.Controls.Add(new Label {Text="Smart Tha Pho | เครื่องสำรอง",Font=new Font("Tahoma",17,FontStyle.Bold),Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,0);
      message.Text="กำลังเริ่มระบบอัตโนมัติ…";message.Dock=DockStyle.Fill;message.AutoEllipsis=true;message.TextAlign=ContentAlignment.MiddleLeft;layout.Controls.Add(message,0,1);
      var states=new TableLayoutPanel {ColumnCount=2,RowCount=3,Dock=DockStyle.Fill};states.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,170));states.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
      for(int i=0;i<3;i++)states.RowStyles.Add(new RowStyle(SizeType.Percent,33.333f));
      AddState(states,0,"ฐานข้อมูล",database);AddState(states,1,"API / เว็บไซต์",api);AddState(states,2,"ช่องทางออนไลน์",online);layout.Controls.Add(states,0,2);
      layout.Controls.Add(new Label {Text="ลิงก์ตรงของเครื่องนี้ (ใช้ได้ทันทีเมื่อออนไลน์)",Dock=DockStyle.Fill,TextAlign=ContentAlignment.BottomLeft},0,3);
      url.ReadOnly=true;url.Dock=DockStyle.Fill;url.AccessibleName="ลิงก์ออนไลน์ของเซิร์ฟเวอร์สำรอง";layout.Controls.Add(url,0,4);
      var buttons=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.LeftToRight,WrapContents=false};
      SetButton(start,"เริ่มระบบ",()=>controller.Run("Start"));SetButton(stop,"หยุดระบบ",()=>controller.Run("Stop"));SetButton(open,"เปิดเว็บไซต์",()=>OpenUrl(direct));SetButton(copy,"คัดลอกลิงก์",()=>{if(direct!="")Clipboard.SetText(direct);});
      buttons.Controls.AddRange(new Control[]{start,stop,open,copy});layout.Controls.Add(buttons,0,5);
      var links=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown};
      var local=new LinkLabel {Text="เปิดในเครื่อง: http://127.0.0.1:4200/",AutoSize=true,Margin=new Padding(0,5,0,10)};local.LinkClicked+=(s,e)=>OpenUrl("http://127.0.0.1:4200/");links.Controls.Add(local);
      var pages=new LinkLabel {Text="เปิดลิงก์สำรองบน GitHub Pages",AutoSize=true,Margin=new Padding(0,0,0,10)};pages.LinkClicked+=(s,e)=>OpenUrl("https://0tyght.github.io/smart-tha-pho-backup/");links.Controls.Add(pages);
      var logs=new LinkLabel {Text="เปิดโฟลเดอร์บันทึกการทำงาน",AutoSize=true};logs.LinkClicked+=(s,e)=>Process.Start("explorer.exe",Path.Combine(controller.Root,".runtime","backup"));links.Controls.Add(logs);layout.Controls.Add(links,0,6);
      layout.Controls.Add(new Label {Text="ใช้ข้อมูลที่คัดลอกจากเครื่องนี้ • LINE และงานส่งข้อความอัตโนมัติปิดอยู่\nหยุดระบบจะไม่ปิด MySQL ที่โปรแกรมอื่นอาจใช้อยู่",Dock=DockStyle.Fill,ForeColor=Color.FromArgb(65,65,65)},0,7);
      Controls.Add(layout);timer.Interval=1500;timer.Tick+=(s,e)=>RefreshState();timer.Start();Shown+=(s,e)=>{if(autoStart)RunSafely("Start");else RefreshState();};
      FormClosing+=(s,e)=>{if(MessageBox.Show("หยุดเซิร์ฟเวอร์สำรองและปิดหน้าต่าง?","ปิดเซิร์ฟเวอร์สำรอง",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes){e.Cancel=true;return;}controller.Run("Stop");if(controller.Operation!=null&&!controller.Operation.WaitForExit(10000)){e.Cancel=true;MessageBox.Show("กำลังหยุดระบบ กรุณารอสักครู่แล้วปิดอีกครั้ง","กำลังหยุด");}};
    }
    void AddState(TableLayoutPanel p,int row,string label,Label value) {p.Controls.Add(new Label {Text=label,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,row);value.Dock=DockStyle.Fill;value.Text="รอเริ่ม";value.TextAlign=ContentAlignment.MiddleLeft;p.Controls.Add(value,1,row);}
    void SetButton(Button b,string text,Action action) {b.Text=text;b.Size=new Size(145,38);b.Margin=new Padding(0,8,10,0);b.Click+=(s,e)=>{try{action();RefreshState();}catch(Exception x){MessageBox.Show(x.Message,"ดำเนินการไม่สำเร็จ");}};}
    void RunSafely(string action) {try{controller.Run(action);}catch(Exception e){message.Text=e.Message;message.ForeColor=Color.DarkRed;}}
    string Get(Dictionary<string,object> d,string key) {return d.ContainsKey(key)?Convert.ToString(d[key]):"";}
    void State(Label label,string value) {label.Text=value=="ready"?"พร้อมใช้งาน":value=="error"?"ไม่พร้อมใช้งาน — ตรวจบันทึกการทำงาน":value=="stopped"?"หยุดแล้ว":value=="shared"?"เปิดใช้งานร่วมกับโปรแกรมอื่น":"กำลังตรวจสอบ";label.ForeColor=value=="ready"?Color.FromArgb(20,100,45):value=="error"?Color.DarkRed:Color.FromArgb(85,65,10);}
    void RefreshState() {try{var d=controller.Read();message.Text=Get(d,"message");details.SetToolTip(message,message.Text);message.ForeColor=Get(d,"phase")=="error"?Color.DarkRed:Color.FromArgb(20,45,35);State(database,Get(d,"database"));State(api,Get(d,"api"));State(online,Get(d,"online"));direct=Get(d,"url");url.Text=direct;start.Enabled=!controller.Busy;stop.Enabled=true;open.Enabled=direct!=""&&Get(d,"online")=="ready";copy.Enabled=direct!="";
      if(Get(d,"phase")=="ready"&&!probing){probing=true;controller.Probe((healthy,tunnel)=>{if(IsDisposed||!IsHandleCreated){probing=false;return;}BeginInvoke((Action)(()=>{probing=false;if(!healthy){api.Text="ติดต่อ API หรือฐานข้อมูลไม่ได้";api.ForeColor=Color.DarkRed;database.Text="ตรวจสอบไม่ได้";database.ForeColor=Color.DarkRed;message.Text="ระบบไม่พร้อม กรุณาหยุดและเริ่มใหม่ หรือตรวจบันทึกการทำงาน";message.ForeColor=Color.DarkRed;}if(!tunnel){online.Text="Tunnel หยุดทำงาน";online.ForeColor=Color.DarkRed;open.Enabled=false;}}));});}
    }catch(IOException){}catch(ArgumentException){} }
    void OpenUrl(string value) {if(value!="")Process.Start(new ProcessStartInfo(value){UseShellExecute=true});}
  }
  internal static class Program {
    [STAThread] static void Main(string[] args) {
      bool created;using(var mutex=new Mutex(true,"Local\\SmartThaPhoBackupMonitor",out created)){
        if(!created && !(args.Length>=2 && args[0]=="--capture")){MessageBox.Show("ตัวเปิดเซิร์ฟเวอร์สำรองเปิดอยู่แล้ว");return;}
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        var root=Environment.GetEnvironmentVariable("SMART_THA_PHO_BACKUP_ROOT")??@"C:\xampp\htdocs\smart-tha-pho-backup";
        if(args.Length>=2 && args[0]=="--capture") {using(var form=new MonitorForm(new BackupController(root,args.Length>2?args[2]:null),false)){if(args.Length>3&&args[3]=="minimum")form.Size=form.MinimumSize;form.Show();Application.DoEvents();using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(0,0,form.Width,form.Height));bitmap.Save(args[1],System.Drawing.Imaging.ImageFormat.Png);} }return;}
        Application.Run(new MonitorForm(new BackupController(root),!(args.Length==1&&args[0]=="--monitor-only")));
      }
    }
  }
}
