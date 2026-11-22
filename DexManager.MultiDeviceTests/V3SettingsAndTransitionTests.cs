using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using DexManager.Forms;
using DexManager.Models;
using DexManager.Services;
internal static partial class ExperimentalFrameRateIntegrationTests {
 static string ReadLiveLog(string path){using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))using(var reader=new StreamReader(stream))return reader.ReadToEnd();}
 static void V3DefaultsAndLogs(){
  var fresh=AppSettings.CreateDefault();fresh.EnsureDefaults();
  Check(fresh.VirtualDisplay.ActivationMode==DexDisplayMode.Native,"v3 fresh default is native DeX");
  Check(fresh.KeyMappings.ConvertEnterToShiftEnter,"new settings enable manual Enter switching");
  Check(!fresh.KeyMappings.AutoAppEnterEnabled&&!fresh.Features.AutoSaveSessionLogs&&!fresh.Features.AutoSwitchToLegacyFor120Fps,"v3 fresh AI, auto-log and remembered transition defaults are off");
  var prior=AppSettings.CreateDefault();prior.SchemaVersion=33;prior.KeyMappings.ConvertEnterToShiftEnter=false;prior.KeyMappings.AutoAppEnterEnabled=true;prior.EnsureDefaults();
  Check(prior.Features.AutoSaveSessionLogs&&prior.KeyMappings.AutoAppEnterEnabled,"existing private-test logging and explicit AI selection survive migration");
  prior.Features.AutoSaveSessionLogs=false;prior.EnsureDefaults();Check(!prior.Features.AutoSaveSessionLogs,"an opted-out v3 log choice is never migrated back on");
  var old=AppSettings.CreateDefault();old.SchemaVersion=28;old.KeyMappings.AppKeyProfiles=null;old.EnsureDefaults();
  Check(!old.Features.AutoSaveSessionLogs&&!old.KeyMappings.AutoAppEnterEnabled,"2.5 public migration does not opt into AI or automatic files");
  var versioned=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"DX Manager v3.0.0");
  using(var versionedLog=new LogService()){
   var io=(SettingsService)Activator.CreateInstance(typeof(SettingsService),Hidden,null,new object[]{versionedLog,versioned},null);
   io.Save(fresh);Check(io.Load().VirtualDisplay.ActivationMode==DexDisplayMode.Native,"versioned portable-folder settings save/load works");
  }
  var directory=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"v3-log-preference");
  using(var log=new LogService()){
   log.ConfigureAutomaticLogs(false,directory);log.Info("memory-only-before-enable");
   Check(string.IsNullOrEmpty(log.AutomaticLogDirectory),"default-off does not start an automatic writer");
   log.ConfigureAutomaticLogs(true,directory);log.Info("enabled-marker");Check(log.FlushAutomaticLogs(3000),"enabled automatic writer flushes");
   var files=Directory.GetFiles(log.AutomaticLogDirectory,"dxm-session-*.log");Check(files.Any(f=>ReadLiveLog(f).Contains("enabled-marker")),"enabled checkbox policy writes session files");
   log.ConfigureAutomaticLogs(false,directory);log.Info("disabled-marker");
   Check(string.IsNullOrEmpty(log.AutomaticLogDirectory)&&files.All(f=>!File.ReadAllText(f).Contains("disabled-marker")),"turning auto-save off stops further disk writes");
   Check(log.GetSessionEntries().Any(x=>x.Contains("disabled-marker")),"turning auto-save off keeps in-memory Log View");
  }
 }
 static bool Transition(Action operation,bool accept,bool remember){
  bool shown=false;int visibleTicks=0;Exception failure=null;
  using(var timer=new System.Windows.Forms.Timer{Interval=25}){
   timer.Tick+=delegate{
    if(shown)return;
    var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x.GetType().Name=="DesktopDialog");if(dialog==null||!dialog.Visible||dialog.ActiveControl==null)return;
    if(++visibleTicks<3)return;shown=true;
    try{
     var check=Controls(dialog).OfType<CheckBox>().Single();check.Checked=remember;
     Check(dialog.AcceptButton==dialog.CancelButton,"native120 transition defaults Enter to Cancel");
     Check(((Control)dialog.CancelButton).Focused,"native120 Cancel owns focus; active="+(dialog.ActiveControl==null?"none":dialog.ActiveControl.Text));
     Check(Controls(dialog).Any(c=>c.Text.Contains("120")),"native120 transition explains frame-rate requirement");
     Check(check.Bottom<dialog.ClientSize.Height-60&&check.Width>350,"remembered-transition checkbox fits above the footer");
    }catch(Exception e){failure=e;}
    dialog.DialogResult=accept?DialogResult.OK:DialogResult.Cancel;dialog.Close();
   };
   timer.Start();operation();timer.Stop();
  }
  if(failure!=null)throw failure;return shown;
 }
 static bool Information(Action operation,string title,Action<Form> inspect=null,string capture=null){
  bool shown=false;Exception failure=null;
  using(var timer=new System.Windows.Forms.Timer{Interval=30}){
   timer.Tick+=delegate {
    if(shown)return;
    var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x.GetType().Name=="DesktopDialog"&&x.Visible);
    if(dialog==null)return;shown=true;
    try {
     Check(dialog.Text==title,"information dialog has the localized title");
     Check(!Controls(dialog).OfType<CheckBox>().Any(),"information dialog requires no extra consent checkbox");
     if(inspect!=null)inspect(dialog);
     if(capture!=null)using(var bitmap=new System.Drawing.Bitmap(dialog.Width,dialog.Height)){
      dialog.DrawToBitmap(bitmap,new System.Drawing.Rectangle(0,0,bitmap.Width,bitmap.Height));
      bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,capture));
     }
    }catch(Exception error){failure=error;}
    dialog.DialogResult=DialogResult.OK;dialog.Close();
   };
   timer.Start();operation();timer.Stop();
  }
  if(failure!=null)throw failure;return shown;
 }
 static bool NativeActive(DexOrchestrator orchestrator){
  return (bool)typeof(MainForm).GetMethod("HasActiveNativeDisplay",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{orchestrator});
 }
 static void V3EmbeddedDocumentation(MainForm main,AppSettings settings){
  foreach(var language in new[]{AppLanguage.English,AppLanguage.Korean})foreach(var theme in new[]{AppTheme.Light,AppTheme.Dark}){
   settings.Language=language;settings.Theme=theme;LocalizationService.Apply(language);Call(main,"ApplyThemeSelection",theme);
   using(var help=new HelpForm()){
    help.Show();Application.DoEvents();
    foreach(var topic in new[]{1,3}){
     Call(help,"ShowTopic",topic);Application.DoEvents();
     var text=string.Join("\n",Controls(help).Select(c=>c.Text));
     Check(text.Contains(language==AppLanguage.Korean?"네이티브 DeX":"Native DeX")&&text.Contains("30"),"actual embedded help explains native fixed grace in "+language+"/"+theme+" topic"+topic);
     Check(topic==1?text.Contains("120"):text.Contains(language==AppLanguage.Korean?"접근성":"Accessibility"),"actual help contains mode120 or accessibility setup");
     var article=(FlowLayoutPanel)Field(help,"_article");
     Check(article.Controls.Cast<Control>().All(c=>c.Height>0&&c.Width<=article.ClientSize.Width),"long help blocks retain bounded width and positive height");
     using(var bitmap=new System.Drawing.Bitmap(help.Width,help.Height)){
      help.DrawToBitmap(bitmap,new System.Drawing.Rectangle(0,0,bitmap.Width,bitmap.Height));
      bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"native-grace-help-"+topic+"-"+language+"-"+theme+".png"));
     }
    }
    help.Close();
   }
   var docType=typeof(HelpForm).Assembly.GetType("DexManager.Forms.DocumentationForm",true);
   using(var guide=(Form)Activator.CreateInstance(docType,true)){
    guide.Show();Application.DoEvents();Call(guide,"SwitchDocument",1);
    var texts=(RichTextBox[])Field(guide,"_documents");
    var versionLabel="Windows v"+typeof(HelpForm).Assembly.GetName().Version.ToString(3);
    Check(texts.All(box=>box.Text.Contains(versionLabel)&&box.Text.Contains("30")&&box.Text.Contains("120")),"both embedded full guide and FAQ match the Windows version");
    Check(texts.All(box=>box.Text.Contains(language==AppLanguage.Korean?"접근성":"accessibility")),"both embedded documents include optional AI/accessibility instructions");
    Check(texts.All(box=>!box.Text.Contains("Real DeX")&&!box.Text.Contains("리얼 DeX")),"user-facing embedded manuals use the agreed Native DeX name");
    guide.Close();
   }
  }
 }
 static void V3SingleWindowNotices(MainForm form,AppSettings settings,DeviceRuntimeServiceSet runtime,DeviceRuntimeServiceSet other){
  var fps=Field(form,"_maxFpsBox");var index=fps.GetType().GetProperty("SelectedIndex");
  var native=Field(runtime.Dex,"_nativeDex");var otherNative=Field(other.Dex,"_nativeDex");
  var original=Field(native,"_lease");var otherOriginal=Field(otherNative,"_lease");
  try {
   settings.VirtualDisplay.ActivationMode=DexDisplayMode.Native;
   Set(native,"_lease",null);Call(form,"DisplayMode",1);index.SetValue(fps,1,null);
   Check(!NativeActive(runtime.Dex),"saved native preference alone is not a running native display");
   Set(otherNative,"_lease",new VirtualDisplayLease{IsNativeDex=true,NativeStartRequested=true,DisplayId=920,Serial="other-phone"});
   Check(!Information(()=>index.SetValue(fps,2,null),"unused"),"another phone's native DeX does not warn this phone");
   Set(otherNative,"_lease",otherOriginal);
   Set(native,"_lease",new VirtualDisplayLease{IsNativeDex=true,NativeStartRequested=false,DisplayId=921});
   Check(!NativeActive(runtime.Dex),"an uncreated pending native lease is not active");
   Set(native,"_lease",new VirtualDisplayLease{IsNativeDex=true,NativeStartRequested=true,DisplayId=922,Serial="fixture-phone"});
   Check(NativeActive(runtime.Dex),"retained native display still has the60 FPS limitation");
   settings.VirtualDisplay.ActivationMode=DexDisplayMode.LegacyOverlay;
   foreach(var language in new[]{AppLanguage.English,AppLanguage.Korean})foreach(var theme in new[]{AppTheme.Light,AppTheme.Dark}){
    settings.Language=language;settings.Theme=theme;LocalizationService.Apply(language);Call(form,"ApplyThemeSelection",theme);
    for(int slot=1;slot<=3;slot++){
     Call(form,"DisplayMode",slot);index.SetValue(fps,1,null);
     Check(Information(()=>index.SetValue(fps,2,null),LocalizationService.Get("Fps.SingleNativeLimitTitle"),dialog=>{
      var text=string.Join("\n",Controls(dialog).Select(c=>c.Text));
      Check(text.Contains(LocalizationService.Get("Fps.SingleNativeLimitBody")),"selection explains60 limit and automatic120 availability after native stop");
     },slot==1?"followup-single-"+language+"-"+theme+".png":null),"single-window"+slot+"120 informs about actual native display in "+language+"/"+theme);
     Check((int)Call(form,"GetSelectedMaxFps")==120,"native limitation notice leaves120 selected");
    }
    Check(!Confirm(form,false,null,()=> (System.Threading.Tasks.Task<bool>)Call(form,"ConfirmSingleWindowFrameRateAsync",120,runtime.Dex),LocalizationService.Get("Fps.SingleNativeLimitBody")),"saved single-window120 still requires start consent while native active");
   }
   Set(native,"_lease",null);settings.VirtualDisplay.ActivationMode=DexDisplayMode.Native;
   Call(form,"DisplayMode",1);index.SetValue(fps,1,null);
   Check(!Information(()=>index.SetValue(fps,2,null),"unused"),"native preference without a display does not show limitation notice");
   using(var child=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Application.ExecutablePath,"--hold"){UseShellExecute=false,CreateNoWindow=true})){
    try {
     Set(runtime.Scrcpy,"_process",child);
     Set(runtime.Dex,"_currentSession",new ManagedDisplaySession{DisplayLease=new VirtualDisplayLease{IsNativeDex=true}});
     Check(NativeActive(runtime.Dex),"live native session is detected independently of settings");
     Set(runtime.Dex,"_currentSession",new ManagedDisplaySession{DisplayLease=new VirtualDisplayLease{IsNativeDex=false}});
     Check(!NativeActive(runtime.Dex)&&!runtime.Dex.UsesNativeDisplay,"live simulated120 uses actual legacy cleanup policy despite saved native mode");
    }finally {Set(runtime.Dex,"_currentSession",null);Set(runtime.Scrcpy,"_process",null);if(!child.HasExited)child.Kill();child.WaitForExit();}
   }
  }finally {
   Set(native,"_lease",original);Set(otherNative,"_lease",otherOriginal);
   settings.VirtualDisplay.ActivationMode=DexDisplayMode.LegacyOverlay;
   foreach(var slot in settings.SingleWindowSlots)slot.MaxFps=60;
   Call(form,"DisplayMode",0);Call(form,"LoadRunSettings");
  }
 }
 static void V3AiSetupNotices(MainForm form,AppSettings settings){
  foreach(var language in new[]{AppLanguage.English,AppLanguage.Korean})foreach(var theme in new[]{AppTheme.Light,AppTheme.Dark}){
   settings.Language=language;settings.Theme=theme;settings.KeyMappings.AutoAppEnterEnabled=false;
   LocalizationService.Apply(language);Call(form,"ApplyThemeSelection",theme);
   Check(!Information(()=>Call(form,"ShowSettingsForm"),"unused"),"opening settings never shows AI setup automatically");
   var preferences=(Form)Field(form,"_settingsForm");Set(preferences,"_settingsChanged",null);
   var ai=(CheckBox)Field(preferences,"_autoAppEnterBox");var manual=(CheckBox)Field(preferences,"_convertEnterBox");
   manual.Checked=true;
   Check(Information(()=>ai.Checked=true,LocalizationService.Get("Settings.AiEnterSetupTitle"),dialog=>{
    var body=LocalizationService.Get("Settings.AiEnterSetupBody");
    Check(Controls(dialog).Any(c=>c.Text==body),"AI setup displays the complete localized instructions");
    var steps=language==AppLanguage.Korean?new[]{"앱 전환 감지 카드","접근성 설정 열기","설치된 앱","사용 중"}:new[]{"App switch detection card","Open Accessibility settings","Installed apps","turn On"};
    var companionName=language==AppLanguage.Korean?"DX Companion 앱":"DX Companion app";
    Check(steps.All(body.Contains)&&body.Contains(companionName),"AI setup names every accessibility step and the shared Companion app");
   },"followup-ai-"+language+"-"+theme+".png"),"enabling AI shows setup in "+language+"/"+theme);
   Check(settings.KeyMappings.AutoAppEnterEnabled&&!settings.KeyMappings.ConvertEnterToShiftEnter&&!manual.Checked,"setup preserves AI/manual mutual exclusion");
   Check(!Information(()=>ai.Checked=false,"unused"),"disabling AI never shows setup");
   Set(preferences,"_loadingStoreValues",true);
   try {Check(!Information(()=>ai.Checked=true,"unused"),"loading an enabled AI control never shows setup");}
   finally {Set(preferences,"_loadingStoreValues",false);}
   Check(!settings.KeyMappings.AutoAppEnterEnabled,"programmatic control loading does not opt into AI");
   preferences.Close();
  }
 }
 static void V3SettingsAndTransitions(MainForm form,AppSettings settings,SettingsService service,LogService log){
  var fps=Field(form,"_maxFpsBox");var index=fps.GetType().GetProperty("SelectedIndex");
  var screen=(CheckBox)Field(form,"_turnScreenOffBox");
  settings.VirtualDisplay.ActivationMode=DexDisplayMode.Native;settings.Features.AutoSwitchToLegacyFor120Fps=false;
  Set(form,"_experimentalFpsDialogOpen",true);
  Check(!(bool)Call(form,"PrepareNative120Transition",new object[]{null})&&settings.VirtualDisplay.ActivationMode==DexDisplayMode.Native,"another active consent dialog cannot reenter or change the backend");
  Set(form,"_experimentalFpsDialogOpen",false);
  foreach(var language in new[]{AppLanguage.English,AppLanguage.Korean})foreach(var theme in new[]{AppTheme.Light,AppTheme.Dark}){
   settings.Language=language;settings.Theme=theme;LocalizationService.Apply(language);Call(form,"ApplyThemeSelection",theme);
   settings.VirtualDisplay.ActivationMode=DexDisplayMode.Native;settings.Features.AutoSwitchToLegacyFor120Fps=false;
   index.SetValue(fps,1,null);screen.Checked=true;
   Check(Transition(()=>index.SetValue(fps,2,null),false,true),"native120 selection presents transition consent in "+language+"/"+theme);
   Check((int)Call(form,"GetSelectedMaxFps")==60&&settings.VirtualDisplay.ActivationMode==DexDisplayMode.Native&&!settings.Features.AutoSwitchToLegacyFor120Fps,"Cancel preserves FPS, backend and remembered policy");
   Check(Transition(()=>index.SetValue(fps,2,null),true,true),"accepted transition is shown before switching");
   Check(settings.VirtualDisplay.ActivationMode==DexDisplayMode.Native&&settings.Features.AutoSwitchToLegacyFor120Fps&&!screen.Checked&&!screen.Enabled,"accepted transition keeps saved native mode and requires screen-on");
   Call(form,"ApplyRunSettings",false);
   var launch=(DeviceRunSettingsProfile)Call(form,"PrepareNative120Start",Call(form,"CaptureSelectedRunSettings"));
   Check(launch.VirtualDisplay.ActivationMode==DexDisplayMode.LegacyOverlay&&settings.VirtualDisplay.ActivationMode==DexDisplayMode.Native,"only the launch snapshot uses legacy120");
   Check(service.Load().VirtualDisplay.ActivationMode==DexDisplayMode.Native,"saved JSON still selects native after120 preparation");
   index.SetValue(fps,1,null);settings.VirtualDisplay.ActivationMode=DexDisplayMode.Native;
   Check(!Transition(()=>index.SetValue(fps,2,null),false,false),"remembered native120 selection switches without another transition popup");
   Check(settings.VirtualDisplay.ActivationMode==DexDisplayMode.Native,"remembered120 never changes the saved backend");
  }
  index.SetValue(fps,1,null);settings.VirtualDisplay.ActivationMode=DexDisplayMode.Native;
  settings.Features.AutoSwitchToLegacyFor120Fps=false;
  Check(Transition(()=>index.SetValue(fps,2,null),true,false),"unremembered120 selection still asks for temporary consent");
  Call(form,"ApplyRunSettings",false);
  DeviceRunSettingsProfile pendingLaunch=null;
  Check(!Transition(()=>pendingLaunch=(DeviceRunSettingsProfile)Call(form,"PrepareNative120Start",Call(form,"CaptureSelectedRunSettings")),false,false)&&pendingLaunch!=null,"accepted selection is not asked again at its next start");
  Check(!settings.Features.AutoSwitchToLegacyFor120Fps&&settings.VirtualDisplay.ActivationMode==DexDisplayMode.Native,"temporary consent does not enable remember or change native setting");
  index.SetValue(fps,1,null);
  Check(Transition(()=>index.SetValue(fps,2,null),false,false),"selecting120 again after60 asks when remember is off");
  Call(form,"DisplayMode",1);
  Check(!Transition(()=>index.SetValue(fps,2,null),false,false),"single-window120 never requests a DeX backend switch");
  Call(form,"ApplyRunSettings",false);
  Check(settings.VirtualDisplay.ActivationMode==DexDisplayMode.Native&&settings.SingleWindowSlots[0].MaxFps==120,"single-window120 can be saved while native DeX remains selected");
  index.SetValue(fps,1,null);Call(form,"ApplyRunSettings",false);Call(form,"DisplayMode",0);index.SetValue(fps,1,null);
  settings.Features.AutoSwitchToLegacyFor120Fps=true;
  var target=settings.GetOrCreateDeviceRunSettings("v3-background-phone");target.VirtualDisplay.ActivationMode=DexDisplayMode.Native;target.Scrcpy.MaxFps=120;
  var targetLaunch=(DeviceRunSettingsProfile)Call(form,"PrepareNative120Start",target);
  Check(targetLaunch.VirtualDisplay.ActivationMode==DexDisplayMode.LegacyOverlay&&target.VirtualDisplay.ActivationMode==DexDisplayMode.Native&&settings.VirtualDisplay.ActivationMode==DexDisplayMode.Native,"background auto-start overrides only its launch copy, not either saved phone");
  target.Scrcpy.MaxFps=60;
  Check(((DeviceRunSettingsProfile)Call(form,"PrepareNative120Start",target)).VirtualDisplay.ActivationMode==DexDisplayMode.Native,"returning to60 uses the saved native backend");
  target.VirtualDisplay.ActivationMode=DexDisplayMode.LegacyOverlay;target.Scrcpy.MaxFps=120;
  Check(((DeviceRunSettingsProfile)Call(form,"PrepareNative120Start",target)).VirtualDisplay.ActivationMode==DexDisplayMode.LegacyOverlay&&target.VirtualDisplay.ActivationMode==DexDisplayMode.LegacyOverlay,"explicit legacy setting remains legacy");
  Call(form,"ShowSettingsForm");var preferences=(Form)Field(form,"_settingsForm");
  preferences.GetType().GetField("_settingsChanged",Hidden).SetValue(preferences,null);
  var auto=(CheckBox)Field(preferences,"_native120AutoSwitchBox");var save=(CheckBox)Field(preferences,"_autoSaveLogsBox");
  Call(preferences,"LoadDexDisplayMode");Check(auto.Checked,"settings checkbox reflects remembered transition choice");
  var savedMode=Field(preferences,"_dexDisplayModeBox");
  Check((int)savedMode.GetType().GetProperty("SelectedIndex").GetValue(savedMode,null)==0,"Settings still displays the saved Native DeX mode after120");
  auto.Checked=false;Check(!settings.Features.AutoSwitchToLegacyFor120Fps,"settings checkbox disables remembered transition");
  save.Checked=true;Check(settings.Features.AutoSaveSessionLogs,"automatic log child checkbox saves preference");
  save.Checked=false;Check(!settings.Features.AutoSaveSessionLogs,"automatic log child checkbox can disable saving");
  var copy=service.Clone(settings);Check(!copy.Features.AutoSaveSessionLogs&&!copy.Features.AutoSwitchToLegacyFor120Fps,"new choices survive serialization");
  preferences.Close();settings.VirtualDisplay.ActivationMode=DexDisplayMode.LegacyOverlay;settings.Features.AutoSwitchToLegacyFor120Fps=false;
  Call(form,"LoadRunSettings");
 }
}
