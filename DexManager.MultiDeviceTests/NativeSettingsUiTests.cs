using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using DexManager.Forms;
using DexManager.Models;
using DexManager.Services;
using DexManager.Utils;
internal static class NativeSettingsUiTests {
 static int checks;static BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
 [DllImport("user32.dll")]static extern int GetWindowLong(IntPtr window,int index);
 [DllImport("user32.dll")]static extern int SetWindowLong(IntPtr window,int index,int value);
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
 static object Field(object o,string n){return o.GetType().GetField(n,Hidden).GetValue(o);}
 static int Index(object selector,string identity){var items=(IList)selector.GetType().GetProperty("Items").GetValue(selector,null);for(int i=0;i<items.Count;i++){var item=items[i];var prop=item.GetType().GetProperty("DeviceIdentity");if(prop!=null&&(string)prop.GetValue(item,null)==identity)return i;}return -1;}
 static void Select(object selector,int value){selector.GetType().GetProperty("SelectedIndex").SetValue(selector,value,null);Application.DoEvents();}
 static void ShowPage(Form form,int index) {
  form.GetType().GetMethod("ShowPage",Hidden).Invoke(form,new object[]{index});
  form.PerformLayout();Application.DoEvents();
 }
 static void Capture(Form form,string name) {
  using(var bitmap=new Bitmap(form.Width,form.Height)) {
   form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));
   bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,name+".png"),ImageFormat.Png);
  }
 }
 static System.Collections.Generic.IEnumerable<Control> Controls(Control root) {
  foreach(Control child in root.Controls) {
   yield return child;
   foreach(var descendant in Controls(child))yield return descendant;
  }
 }
 static int Selected(object o){return (int)o.GetType().GetProperty("SelectedIndex").GetValue(o,null);}
 [STAThread]static int Main(){try{
  Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  foreach(var lang in new[]{AppLanguage.Korean,AppLanguage.English})foreach(var theme in new[]{AppTheme.Light,AppTheme.Dark}){
   LocalizationService.Apply(lang);typeof(MainForm).Assembly.GetType("DexManager.Forms.ThemeColors").GetMethod("Use",BindingFlags.Public|BindingFlags.Static).Invoke(null,new object[]{theme});
   var settings=AppSettings.CreateDefault();settings.Language=lang;settings.Theme=theme;settings.KeyMappings.ConvertEnterToShiftEnter=false;settings.KeyMappings.AutoAppEnterEnabled=true;
   settings.GetOrCreateDeviceRunSettings("phone-a").VirtualDisplay.ActivationMode=DexDisplayMode.LegacyOverlay;
   settings.GetOrCreateDeviceRunSettings("phone-a").VirtualDisplay.KeepBluetoothAudioOnDexStart=true;
   settings.GetOrCreateDeviceRunSettings("phone-b").VirtualDisplay.ActivationMode=DexDisplayMode.Native;
   var dir=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"state-"+lang+"-"+theme);Directory.CreateDirectory(dir);
   var log=new LogService();log.SetLogDirectory(Path.Combine(dir,"logs"));
   var store=(SettingsService)Activator.CreateInstance(typeof(SettingsService),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{log,dir},null);
   var runner=new ProcessRunner(log);runner.BlockNewProcessesForWindowsShutdown();var adb=new AdbService(Path.Combine(dir,"unused-adb.exe"),1000,runner,log);var wifi=new WirelessAdbService(adb,store,settings,log);
   var devices=new DeviceRegistrySnapshot();foreach(var id in new[]{"phone-a","phone-b"}){var phone=new PhysicalDeviceInfo{Identity=id,DisplayName="Test "+id};phone.Transports.Add(new DeviceTransportInfo{Serial=id,Kind=DeviceTransportKind.Usb,Status=AdbDeviceStatus.Device});devices.Devices.Add(phone);}
   var visibleDevices=devices;
   using(var form=new SettingsForm(store,settings,adb,wifi,()=>"phone-a",()=>"phone-a",()=>visibleDevices,()=>new DeviceRuntimeRegistrySnapshot(),log,null,null,null,null,null,null,
    runEnvironmentCheck:()=>new[]{
     new EnvironmentCheckItem {Name="ADB",Message=@"E:\Synthetic preview path\tools\scrcpy\adb.exe"},
     new EnvironmentCheckItem {Name="scrcpy",Message=@"E:\Synthetic preview path\tools\scrcpy\scrcpy.exe"},
     new EnvironmentCheckItem {Name="Version",Message="5.0.1"},
     new EnvironmentCheckItem {Name="Windows",Message="Windows 11"},
     new EnvironmentCheckItem {Name="Phone",Message="Test phone-b"}
    })){
    form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-5000,-5000);form.ShowInTaskbar=false;
    SetWindowLong(form.Handle,-20,GetWindowLong(form.Handle,-20)|0x08000000);
    SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());form.Show();
    form.GetType().GetMethod("ShowPage",Hidden).Invoke(form,new object[]{1});form.PerformLayout();Application.DoEvents();
    var selector=Field(form,"_dexDisplayModeBox");var target=Field(form,"_wirelessDeviceBox");
    int a=Index(target,"phone-a"),b=Index(target,"phone-b");Check(a>=0&&b>=0,"Both physical phones are selectable: "+lang+"/"+theme);
    Select(target,a);Check(Selected(selector)==1,"Legacy mode loads for phone A");Select(target,b);Check(Selected(selector)==0,"Native mode loads independently for phone B");
    Select(selector,1);var handler=(EventHandler)selector.GetType().GetField("SelectionChangeCommitted",Hidden).GetValue(selector);handler(selector,EventArgs.Empty);
    Check(settings.GetOrCreateDeviceRunSettings("phone-b").VirtualDisplay.ActivationMode==DexDisplayMode.LegacyOverlay,"Committed UI selection updates the selected phone");
    Check(settings.GetOrCreateDeviceRunSettings("phone-a").VirtualDisplay.ActivationMode==DexDisplayMode.LegacyOverlay,"Changing phone B preserves phone A");
    Check(File.Exists(store.SettingsFilePath),"Committed backend choice is saved");
    Select(selector,0);handler(selector,EventArgs.Empty);
    var bluetooth=(CheckBox)Field(form,"_nativeBluetoothAudioBox");
    Check(!bluetooth.Checked&&bluetooth.Enabled,"Phone B starts with the independent opt-in Bluetooth setting");
    bluetooth.Checked=true;Check(settings.GetOrCreateDeviceRunSettings("phone-b").VirtualDisplay.KeepBluetoothAudioOnDexStart,"Bluetooth toggle saves only to the selected phone");
    Select(target,a);Check(bluetooth.Checked&&!bluetooth.Enabled,"Legacy mode preserves the saved Bluetooth choice but disables the native-only editor");
    Select(target,b);bluetooth.Checked=false;Check(settings.GetOrCreateDeviceRunSettings("phone-a").VirtualDisplay.KeepBluetoothAudioOnDexStart&&!settings.GetOrCreateDeviceRunSettings("phone-b").VirtualDisplay.KeepBluetoothAudioOnDexStart,"Loading and editing phone B never overwrite phone A's Bluetooth setting");
    var control=(Control)selector;Check(control.Visible&&control.Width>=240&&control.Height>=36,"Mode selector is visible and usable at default size");
    var cards=(IDictionary)Field(form,"_storeCards");var card=(Control)cards["ConnectionDexDisplayMode"];
    Check(card.Height>=64,"Mode card reserves room for its description and editor");
    var audioCard=(Control)cards["ConnectionNativeBluetoothAudio"];Check(audioCard.Height>=64&&bluetooth.Visible,"Bluetooth option is laid out in both languages and themes");
    using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Settings-"+lang+"-"+theme+".png"),ImageFormat.Png);}
    var modeCard=(Control)cards["ConnectionDexDisplayMode"];
    Check(ReferenceEquals(audioCard.Parent.Parent,modeCard)&&
          ReferenceEquals(((Control)cards["ConnectionNative120AutoSwitch"]).Parent.Parent,modeCard),
          "Both native preferences are grouped under the DeX selector");
    var suffix=lang+"-"+theme;
    form.Height=1040;
    ShowPage(form,3);
    var input=(Control)cards["InputAiEnterCard"];
    Check(new[]{"ChatGPT","Claude","Gemini","Grok"}.All(name=>
      ReferenceEquals(((Control)cards["InputAiProfile"+name]).Parent.Parent,input)),
      "AI app switches are nested under the master control");
    var gemini=Controls((Control)cards["InputAiProfileGemini"]).OfType<CheckBox>().Single();
    Check(gemini.Visible&&gemini.Enabled,"AI options are expanded when enabled");
    var pages=(IList)Field(form,"_pages");
    var inputPage=(Control)pages[3];
    var exitCard=inputPage.Controls.Cast<Control>().First(c=>
      Controls(c).Contains((Control)Field(form,"_exitHotkeyBox")));
    var exitHint=inputPage.Controls[inputPage.Controls.GetChildIndex(exitCard)+1];
    Check(exitHint is Label&&exitHint.Text.Contains(lang==AppLanguage.Korean?"보조키":"non-modifier"),"Shortcut instructions sit directly below the exit shortcut");
    cards["InputHangulExpander"].GetType().GetProperty("Expanded").SetValue(cards["InputHangulExpander"],false,null);
    form.PerformLayout();Application.DoEvents();
    Capture(form,"SettingsInput-"+suffix);
    gemini.Checked=false;
    var rules=new AppKeyRuleResolver(store.Load().KeyMappings.AppKeyProfiles);
    Check(!rules.ShouldConvertEnter("com.google.android.apps.bard",false,false,false,false)&&
          !rules.ShouldConvertEnter(new ForegroundAppIdentity("com.android.chrome",42,
           "org.chromium.chrome.browser.webapps.SameTaskWebApkActivity","Google Gemini"),false,false,false,false)&&
          rules.ShouldConvertEnter("com.anthropic.claude",false,false,false,false),
          "Gemini switch disables both targets and preserves the Claude rule after reload");
    ShowPage(form,2);
    var pathsPage=(Control)pages[2];
    var transferHeading=pathsPage.Controls.OfType<Label>().Single(x=>x.Text==(lang==AppLanguage.Korean?"파일 전송 경로":"File transfer paths"));
    Check(pathsPage.Controls.GetChildIndex(transferHeading)<pathsPage.Controls.GetChildIndex((Control)cards["PathsPcToPhoneExpander"]),
          "File transfer paths heading precedes the transfer settings");
    Capture(form,"SettingsPaths-"+suffix);
    visibleDevices=new DeviceRegistrySnapshot();
    Select(target,-1);
    ShowPage(form,4);
    form.GetType().GetMethod("RefreshStoreEnvironment",Hidden).Invoke(form,null);
    var editors=(IDictionary)Field(form,"_storeEditors");
    var refresh=(Control)editors["RefreshDiagnosticsButton"];
    var results=(Control)editors["DiagnosticEnvironmentText"];
    var environment=cards["DiagnosticsEnvironmentCard"];
    var title=(Control)environment.GetType().GetField("TitleLabel").GetValue(environment);
    var description=(Control)environment.GetType().GetField("DescriptionLabel").GetValue(environment);
    var watch=System.Diagnostics.Stopwatch.StartNew();
    while(!results.Text.Contains("Test phone-b")&&watch.ElapsedMilliseconds<5000) {
     Application.DoEvents();Thread.Sleep(10);
    }
    form.PerformLayout();Application.DoEvents();
    Check(results.Height>=results.Font.Height*5&&results.Parent.Height>=results.Height,
          "All environment result lines fit below the heading: result="+results.Height+
          ", body="+results.Parent.Height+", font="+results.Font.Height+
          ", preferred="+results.GetPreferredSize(new Size(results.Width,0))+
          ", bounds="+results.Bounds+", card="+((Control)environment).Bounds+
          ", text="+results.Text.Replace("\n"," | "));
    Check(results.Text.Contains("Test phone-b")&&results.Parent.Top>=refresh.Bottom&&
          ReferenceEquals(refresh.Parent,title.Parent)&&refresh.Visible&&
          refresh.Parent.ClientRectangle.Contains(refresh.Bounds)&&
          !refresh.Bounds.IntersectsWith(title.Bounds)&&!refresh.Bounds.IntersectsWith(description.Bounds),
          "Expanded environment results leave an unobstructed refresh button");
    Capture(form,"SettingsDiagnostics-"+suffix);
    form.Close();
   }
  }
  Console.WriteLine("All native settings UI tests passed: "+checks);return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
