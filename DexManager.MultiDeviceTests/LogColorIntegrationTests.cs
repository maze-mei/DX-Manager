using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using DexManager.Forms;
using DexManager.Models;
using DexManager.Services;
internal static class LogColorIntegrationTests {
 [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr window,int index);
 [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr window,int index,int value);
 static int checks;static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
 static void Pump(){for(int i=0;i<8;i++){Application.DoEvents();Thread.Sleep(5);}}
 static double Channel(double value){value/=255;return value<=0.04045?value/12.92:Math.Pow((value+0.055)/1.055,2.4);}
 static double Luminance(Color c){return 0.2126*Channel(c.R)+0.7152*Channel(c.G)+0.0722*Channel(c.B);}
 static double Contrast(Color a,Color b){double x=Luminance(a),y=Luminance(b);return (Math.Max(x,y)+0.05)/(Math.Min(x,y)+0.05);}
 static Color At(RichTextBox box,string text){int index=box.Text.IndexOf(text,StringComparison.Ordinal);Check(index>=0,"Displayed entry exists: "+text);box.Select(index,text.Length);return box.SelectionColor;}
 [STAThread]static int Main(){try{
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  foreach(var theme in new[]{AppTheme.Light,AppTheme.Dark}){
   typeof(LogForm).Assembly.GetType("DexManager.Forms.ThemeColors").GetMethod("Use",BindingFlags.Public|BindingFlags.Static).Invoke(null,new object[]{theme});
   using(var log=new LogService()){
    log.Command("cmd sample");log.Response("response sample");log.Warning("warning sample");log.Error("error sample",null);log.Info("info sample");log.Progress("중요 안내 / Creating internal DeX: 1600x900, DPI 150.");log.ToolOutput("WARN","[scrcpy] raw warning sample");
    Check(log.GetSessionEvents()[0].Kind==LogEntryKind.Command&&log.GetSessionEvents()[1].Kind==LogEntryKind.Response,"History preserves source kind without encoding it in text");
    Check(log.GetSessionEntries().All(s=>!s.Contains("[CMD]")&&!s.Contains("[RECV]")),"Saved text receives no UI color/type markers");
    var path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"plain-"+theme+".log");log.SaveSession(path);Check(!File.ReadAllText(path).Contains("\\cf"),"Plain log contains no RichText color controls");
    using(var form=new LogForm(log)){
     form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-5000,-5000);form.ShowInTaskbar=false;SetWindowLong(form.Handle,-20,GetWindowLong(form.Handle,-20)|0x08000000);form.Show();Pump();
     var box=(RichTextBox)typeof(LogForm).GetField("_logTextBox",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);var font=box.Font;
     var colors=new[]{At(box,"cmd sample"),At(box,"response sample"),At(box,"warning sample"),At(box,"error sample"),At(box,"info sample")};
     var progress=At(box,"중요 안내 / Creating internal DeX: 1600x900, DPI 150.");
     Check(colors[0].ToArgb()==colors[4].ToArgb()&&colors[1].ToArgb()==colors[4].ToArgb(),"Routine commands and responses use the normal color in "+theme);
     Check(new[]{colors[4],progress,colors[2],colors[3]}.Select(c=>c.ToArgb()).Distinct().Count()==4,"Progress/warnings/errors/general are distinct in "+theme);
     Check(new[]{progress,colors[2],colors[3]}.All(c=>Contrast(c,box.BackColor)>=4.5),"Stronger highlights retain readable contrast in "+theme);
     Check(At(box,"[scrcpy] raw warning sample").ToArgb()==colors[4].ToArgb(),"Raw scrcpy output uses the normal color even when its saved level is WARN");
     var entry=log.GetSessionEvents().First(e=>e.Kind==LogEntryKind.Progress);int start=box.Text.IndexOf(entry.Message,StringComparison.Ordinal);
     box.Select(start,entry.Message.Length);Check(box.SelectionColor.ToArgb()==progress.ToArgb(),"Timestamp, INFO and message share the full-entry highlight");
     log.Command("live command");Pump();Check(At(box,"live command").ToArgb()==colors[0].ToArgb(),"Live entries use the same command color as history");
     var next=theme==AppTheme.Light?AppTheme.Dark:AppTheme.Light;
     typeof(LogForm).Assembly.GetType("DexManager.Forms.ThemeColors").GetMethod("Use",BindingFlags.Public|BindingFlags.Static).Invoke(null,new object[]{next});form.ApplyCurrentTheme();
     Check(At(box,"중요 안내 / Creating internal DeX: 1600x900, DPI 150.").ToArgb()!=progress.ToArgb(),"Theme changes recolor existing progress entries");
     Check(box.Font.Name==font.Name&&box.Font.Size==font.Size,"Color changes preserve the log font and size");
     var len=box.TextLength;log.Command("live command");Pump();Check(box.TextLength==len,"Color metadata preserves consecutive-message deduplication");
     form.Close();
    }
   }
  }
  using(var once=new LogService()){once.ProgressOnce("startup","startup sample");once.ProgressOnce("startup","startup sample");Check(once.GetSessionEvents().Length==1&&once.GetSessionEvents()[0].Kind==LogEntryKind.Progress,"Once-only startup notices retain highlight metadata");}
  var nativeSettings=AppSettings.CreateDefault();nativeSettings.VirtualDisplay.ActivationMode=DexDisplayMode.Native;nativeSettings.VirtualDisplay.Dpi=150;nativeSettings.EnsureDefaults();Check(nativeSettings.VirtualDisplay.Dpi==150,"Native150-DPI final input is preserved independently of initialization");
  var legacySettings=AppSettings.CreateDefault();legacySettings.VirtualDisplay.ActivationMode=DexDisplayMode.LegacyOverlay;legacySettings.VirtualDisplay.Dpi=150;legacySettings.EnsureDefaults();Check(legacySettings.VirtualDisplay.Dpi==150,"Legacy150-DPI settings remain unchanged");
  var defaults=AppSettings.CreateDefault();var expected=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads","DX Manager");Check(defaults.Paths.PhoneToPcReceiveFolder==expected,"Receive default matches official Downloads root");
  var old=AppSettings.CreateDefault();old.SchemaVersion=31;old.Paths.PhoneToPcReceiveFolder="receive";old.EnsureDefaults();Check(old.Paths.PhoneToPcReceiveFolder==expected,"Old private receive default migrates on load");
  var custom=AppSettings.CreateDefault();custom.SchemaVersion=31;custom.Paths.PhoneToPcReceiveFolder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"custom");var kept=custom.Paths.PhoneToPcReceiveFolder;custom.EnsureDefaults();Check(custom.Paths.PhoneToPcReceiveFolder==kept,"Explicit custom receive root is preserved");
  var current=AppSettings.CreateDefault();current.Paths.PhoneToPcReceiveFolder="receive";current.EnsureDefaults();Check(current.Paths.PhoneToPcReceiveFolder=="receive","A newly chosen receive folder is not repeatedly migrated");
  Console.WriteLine("All log color/path integration checks passed: "+checks);return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
