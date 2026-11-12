using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using DexManager.Forms;
using DexManager.Models;
using DexManager.Services;
using DexManager.Utils;

internal static partial class ExperimentalFrameRateIntegrationTests
{
    private static int checks;
    private static readonly BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    private static object Field(object obj, string name) { return obj.GetType().GetField(name, Hidden).GetValue(obj); }
    private static void Set(object obj, string name, object value) { obj.GetType().GetField(name, Hidden).SetValue(obj, value); }
    private static object Call(object obj, string name, params object[] args)
    {
        var method = obj.GetType().GetMethods(Hidden).Single(m => m.Name == name && m.GetParameters().Length == args.Length);
        return method.Invoke(obj, args);
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; Console.WriteLine("PASS " + message); }
    private static void ConsentRequired(Action operation, string message)
    {
        try { operation(); throw new Exception("Missing consent gate: " + message); }
        catch (InvalidOperationException exception) { Check(exception.Message.Contains("confirmation"), message); }
    }
    private static IEnumerable<Control> Controls(Control root)
    {
        foreach (Control c in root.Controls) { yield return c; foreach (var child in Controls(c)) yield return child; }
    }
    private static void PersistedSettings()
    {
        var root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "saved-settings");
        Directory.CreateDirectory(root);
        var log = new LogService();
        var settingsService = (SettingsService)Activator.CreateInstance(typeof(SettingsService), Hidden, null, new object[] { log, root }, null);
        foreach (var original in new[] { false, true })
        {
            var settings = AppSettings.CreateDefault();
            var device = settings.GetOrCreateDeviceRunSettings("phone-A");
            var other = settings.GetOrCreateDeviceRunSettings("phone-B");
            foreach (var scrcpy in new[] { settings.Scrcpy, device.Scrcpy })
            { scrcpy.MaxFps = 120; scrcpy.TurnScreenOff = false; scrcpy.TurnScreenOffBeforeHighFps = original; }
            foreach (var slot in settings.SingleWindowSlots.Concat(device.SingleWindowSlots))
            { slot.MaxFps = 120; slot.TurnScreenOff = false; slot.TurnScreenOffBeforeHighFps = original; }
            device.SingleWindowAppProfiles.Add(new SingleWindowAppProfile { PackageName = "test.app", MaxFps = 120, TurnScreenOffBeforeHighFps = original });
            settingsService.Save(settings);
            var loaded = settingsService.Load();
            var profile = loaded.GetOrCreateDeviceRunSettings("phone-A");
            Check(loaded.Scrcpy.MaxFps == 120 && loaded.Scrcpy.TurnScreenOffBeforeHighFps == original && !loaded.Scrcpy.TurnScreenOff,
                "root 120 and prior " + original + " survive actual JSON save/load");
            Check(profile.Scrcpy.MaxFps == 120 && profile.Scrcpy.TurnScreenOffBeforeHighFps == original,
                "device DeX 120/backup survive JSON");
            Check(profile.SingleWindowSlots.All(x => x.MaxFps == 120 && x.TurnScreenOffBeforeHighFps == original && !x.TurnScreenOff),
                "all device slots preserve 120/backup");
            Check(loaded.SingleWindowSlots.All(x => x.MaxFps == 120 && x.TurnScreenOffBeforeHighFps == original), "default slots preserve backup");
            var app = profile.SingleWindowAppProfiles.Single();
            Check(app.MaxFps == 120 && app.TurnScreenOffBeforeHighFps == original && !app.TurnScreenOff, "app profile preserves backup");
            var clone = settingsService.Clone(loaded);
            Check(clone.GetOrCreateDeviceRunSettings("phone-A").SingleWindowAppProfiles.Single().TurnScreenOffBeforeHighFps == original, "deep clone preserves backup");
            loaded.Scrcpy.MaxFps = 60;
            profile.Scrcpy.MaxFps = 30;
            foreach (var slot in profile.SingleWindowSlots) slot.MaxFps = 60;
            app.MaxFps = 30;
            settingsService.Save(loaded);
            loaded = settingsService.Load(); profile = loaded.GetOrCreateDeviceRunSettings("phone-A");
            Check(loaded.Scrcpy.TurnScreenOff == original && !loaded.Scrcpy.TurnScreenOffBeforeHighFps.HasValue, "60 restores exact root value");
            Check(profile.Scrcpy.TurnScreenOff == original && !profile.Scrcpy.TurnScreenOffBeforeHighFps.HasValue, "30 restores exact per-phone DeX value");
            Check(profile.SingleWindowSlots.All(x => x.TurnScreenOff == original && !x.TurnScreenOffBeforeHighFps.HasValue), "60 restores all slots");
            Check(profile.SingleWindowAppProfiles.Single().TurnScreenOff == original && !profile.SingleWindowAppProfiles.Single().TurnScreenOffBeforeHighFps.HasValue, "30 restores app-profile value");
            Check(loaded.GetOrCreateDeviceRunSettings("phone-B").Scrcpy.MaxFps == 60 && other.Scrcpy.MaxFps == 60, "other phone retains default 60");
        }
        var legacy = new ScrcpySettings { MaxFps = 120, TurnScreenOff = false };
        FrameRateRules.Normalize(legacy); legacy.MaxFps = 60; FrameRateRules.Normalize(legacy);
        Check(legacy.TurnScreenOff && legacy.TurnScreenOffBeforeHighFps == null, "legacy 120 without backup restores documented default");
    }
    private static bool Confirm(MainForm form, bool accept, string capture, Func<Task<bool>> operation = null, string requiredText = null)
    {
        Exception failure = null; bool handled = false;
        using (var timer = new Timer { Interval = 40 })
        {
            timer.Tick += delegate
            {
                if (handled) return;
                var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.GetType().Name == "DesktopDialog");
                if (dialog == null) return;
                handled = true;
                try
                {
                    Check(dialog.AcceptButton == dialog.CancelButton, "120 warning defaults to Cancel");
                    Check(((Control)dialog.CancelButton).Focused, "Cancel actually owns initial keyboard focus");
                    var text = string.Join("\n", Controls(dialog).Select(c => c.Text));
                    Check(text.Contains("OLED") && text.Contains("120") && text.Length > 250, "warning includes full risk information");
                    if(requiredText!=null)Check(text.Contains(requiredText),"saved120 start consent includes the native single-window limitation");
                }
                catch (Exception exception) { failure = exception; }
                dialog.DialogResult = accept ? DialogResult.OK : DialogResult.Cancel; dialog.Close();
            };
            timer.Start();
            var result = (operation == null ? (Task<bool>)Call(form,"ConfirmExperimentalFrameRateAsync",120) : operation()).GetAwaiter().GetResult();
            timer.Stop(); if (failure != null) throw failure;
            Check(handled,"actual 120 consent UI was shown"); return result;
        }
    }
    private static void RuntimeAndUi()
    {
        var log = new LogService(); var settings = AppSettings.CreateDefault(); settings.VirtualDisplay.ActivationMode=DexDisplayMode.LegacyOverlay;
        var service = new SettingsService(log); var runner = new ProcessRunner(log); runner.BlockNewProcessesForWindowsShutdown();
        var adb = new AdbService("unused-adb.exe",1000,runner,log); var path = new PathService(service,log,runner);
        var wifi = new WirelessAdbService(adb,service,settings,log); var registry = new PhysicalDeviceRegistry();
        var sessions = new DeviceRuntimeSessionRegistry(); var launch = new ScrcpyLaunchCoordinator();
        var factory = new DeviceRuntimeServiceFactory("unused-scrcpy.exe","unused-adb.exe",1000,runner,adb,launch,service,settings,log,sessions);
        var runtime = factory.Create(); var other = factory.Create();
        using (var monitor = new DeviceMonitorService(adb,wifi,registry,log,1000,1000))
        using (var hotkeys = new HotkeyService(log,settings))
        using (var captureCoordinator = new CaptureCoordinator(hotkeys,new CaptureService(adb,service,settings,log),runtime.Scrcpy,runtime.SingleWindows,settings,log))
        using (var autoHide = new AutoHideService(runtime.Scrcpy,runtime.SingleWindows,log,60))
        using (var keymap = new KeyMappingService(runtime.Scrcpy,runtime.SingleWindows,adb,settings,settings.KeyMappings,log))
        using (var form = new MainForm(service,settings,log,adb,wifi,runtime.Scrcpy,runtime.SingleWindows,runtime.ScreenOff,launch,monitor,runtime.Dex,captureCoordinator,autoHide,new AutoStartService(log),
            new EnvironmentCheckService(adb,runtime.Scrcpy,path,log,service,settings,()=>"fixture-serial"),keymap,runtime.FileTransfers,runtime.PhoneTransfers,sessions,runtime,registry,factory,path,new CaptureService(adb,service,settings,log),false))
        {
            var shown=typeof(MainForm).GetMethod("MainForm_Shown",Hidden);
            form.Shown-=(EventHandler)Delegate.CreateDelegate(typeof(EventHandler),form,shown);
            form.Show(); Application.DoEvents();
            V3SettingsAndTransitions(form,settings,service,log);
            V3SingleWindowNotices(form,settings,runtime,other);
            V3AiSetupNotices(form,settings);
            V3EmbeddedDocumentation(form,settings);
            foreach (var language in new[] { AppLanguage.English,AppLanguage.Korean })
            foreach (var theme in new[] { AppTheme.Light,AppTheme.Dark })
            {
                settings.Language=language;settings.Theme=theme; LocalizationService.Apply(language); Call(form,"ApplyThemeSelection",theme);
                var fps=Field(form,"_maxFpsBox"); var selected=fps.GetType().GetProperty("SelectedIndex");
                var screen=(CheckBox)Field(form,"_turnScreenOffBox");
                foreach (var original in new[] { false,true })
                {
                    selected.SetValue(fps,1,null); screen.Checked=original;
                    selected.SetValue(fps,2,null);
                    Check(!screen.Checked && !screen.Enabled && !Application.OpenForms.Cast<Form>().Any(f=>f.GetType().Name=="DesktopDialog"),"120 selection disables screen-off with no popup");
                    selected.SetValue(fps,0,null);
                    Check(screen.Checked==original && screen.Enabled,"30 UI restores prior "+original);
                }
                selected.SetValue(fps,2,null);
                var mainCapture=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"120-main-"+language+"-"+theme+".png");
                var output=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"120-"+language+"-"+theme+".png");
                Check(!Confirm(form,false,output),"Cancel blocks 120 start");
                Check(Confirm(form,true,output),"fresh confirmation is required on next start");
            }
            var fpsControl=Field(form,"_maxFpsBox");var fpsSelection=fpsControl.GetType().GetProperty("SelectedIndex");
            var screenControl=(CheckBox)Field(form,"_turnScreenOffBox");
            fpsSelection.SetValue(fpsControl,1,null);screenControl.Checked=false;fpsSelection.SetValue(fpsControl,2,null);
            Call(form,"ApplyRunSettings",false);Call(form,"DisplayMode",1);
            Check((int)Call(form,"GetSelectedMaxFps")==60 && screenControl.Enabled,"another slot retains 60 and enabled screen control");
            Call(form,"DisplayMode",0);
            Check((int)Call(form,"GetSelectedMaxFps")==120 && !screenControl.Enabled,"returning to DeX projects saved120");
            fpsSelection.SetValue(fpsControl,1,null);
            Check(!screenControl.Checked && screenControl.Enabled,"slot switch preserves prior false preference");
            settings.SingleWindowSlots[0].StartAppPackage="profile.app";
            settings.SingleWindowAppProfiles.Add(new SingleWindowAppProfile{PackageName="profile.app",AppName="Profile",MaxFps=120,TurnScreenOffBeforeHighFps=false});
            Call(form,"DisplayMode",1);
            Check((int)Call(form,"GetSelectedMaxFps")==120 && !screenControl.Enabled,"app profile120 is not overwritten by older slot60");
            fpsSelection.SetValue(fpsControl,0,null);
            Check(!screenControl.Checked && screenControl.Enabled,"app-profile120 restores original false in UI");
            foreach(var mode in new[]{0,1,2,3})
            {
                settings.Scrcpy.MaxFps=120;settings.Scrcpy.TurnScreenOffBeforeHighFps=false;
                foreach(var savedSlot in settings.SingleWindowSlots){savedSlot.MaxFps=120;savedSlot.TurnScreenOffBeforeHighFps=false;savedSlot.StartAppPackage="cancel.app";}
                Call(form,"DisplayMode",mode);
                var method=mode==0?"StartDexAsync":"StartSingleWindowAsync";
                var arguments=mode==0?new object[0]:new object[]{mode};
                Confirm(form,false,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"cancel-mode-"+mode+".png"),async delegate
                {
                    await (Task)Call(form,method,arguments); return true;
                });
                Check(!runtime.Scrcpy.IsRunning && runtime.SingleWindows.RunningCount==0,"actual saved120 start cancellation leaves no process in mode "+mode);
            }
            var high=settingsServiceClone(service,settings); high.Scrcpy.MaxFps=120;
            ConsentRequired(()=>runtime.Dex.StartAsync("phone-A",new DeviceRunSettingsProfile{Scrcpy=high.Scrcpy,VirtualDisplay=high.VirtualDisplay}).GetAwaiter().GetResult(),"DeX core blocks unconfirmed start before ADB/overlay");
            ConsentRequired(()=>runtime.Scrcpy.Start(high.Scrcpy,1,"phone-A"),"scrcpy core blocks unconfirmed 120");
            var slot=high.SingleWindowSlots[0];slot.MaxFps=120;slot.StartAppPackage="test.app";
            Check(runtime.Scrcpy.BuildArguments(high.Scrcpy,1,"phone-A").Contains("--hwdec=auto"),"DeX includes the pinned5.x hardware-decoding default");
            Check(((string)Call(runtime.SingleWindows,"BuildArguments",1,slot,"phone-A")).Contains("--hwdec=auto"),"single-window includes the same pinned5.x decoding default");
            high.Scrcpy.AdditionalArguments="--hwdec=d3d11va";slot.AdditionalArguments="--hwdec=d3d11va";
            Check(!runtime.Scrcpy.BuildArguments(high.Scrcpy,1,"phone-A").Contains("--hwdec=auto"),"DeX preserves an explicit hardware-decoding override");
            Check(!((string)Call(runtime.SingleWindows,"BuildArguments",1,slot,"phone-A")).Contains("--hwdec=auto"),"single-window preserves an explicit decoding override");
            high.Scrcpy.AdditionalArguments="";slot.AdditionalArguments="";
            ConsentRequired(()=>runtime.SingleWindows.Start(1,slot,"phone-A"),"single-window core blocks unconfirmed start before ADB/file checks");
            ConsentRequired(()=>runtime.SingleWindows.Restart(1,slot),"restart refuses unconfirmed 120 before stopping old window");
            foreach (var argument in new[]{"--max-fps=120","--max-fps 120","--max-f=120","-S","--no-power-on"})
            { try { ScrcpyService.ValidateAdditionalArguments(argument); throw new Exception("override accepted"); } catch(InvalidOperationException){Check(true,"reserved override rejected: "+argument);} }
            high.Scrcpy.TurnScreenOff=true;
            var args=runtime.Scrcpy.BuildArguments(high.Scrcpy,1,"phone-A");
            Check(args.Contains("--max-fps 120") && !args.Contains("-S") && !args.Contains("--no-power-on"),"DeX argument builder forces screen on at 120");
            slot.TurnScreenOff=true;
            args=(string)Call(runtime.SingleWindows,"BuildArguments",1,slot,"phone-A");
            Check(args.Contains("--max-fps 120") && !args.Contains("-S"),"slot builder forces screen on at 120");
            using (var child = Process.Start(new ProcessStartInfo(Application.ExecutablePath,"--hold") { UseShellExecute=false,CreateNoWindow=true }))
            {
                try
                {
                    Set(runtime.Scrcpy,"_process",child);Set(runtime.Scrcpy,"_runningMaxFps",120);
                    high.Scrcpy.MaxFps=60;
                    Check(runtime.Scrcpy.RequiresPhoneScreenOn,"live120 state uses running process FPS");
                    Check(!runtime.Scrcpy.BuildArguments(high.Scrcpy,1,"phone-A").Contains("-S"),"same-phone ordinary DeX start suppresses screen off");
                    slot.MaxFps=60;
                    Check(!((string)Call(runtime.SingleWindows,"BuildArguments",1,slot,"phone-A")).Contains("-S"),"same-phone slot start suppresses screen off");
                    Check(!runtime.ScreenOff.Reapply("phone-A",()=>true),"delayed screen off is suppressed before process IO");
                    Check(other.Scrcpy.BuildArguments(high.Scrcpy,1,"phone-B").Contains("-S"),"another physical phone still permits screen off");
                    Set(runtime.Scrcpy,"_process",null);
                    Check(runtime.Scrcpy.BuildArguments(high.Scrcpy,1,"phone-A").Contains("-S"),"last120 exit restores remaining ordinary request");
                    var processes=(IDictionary<int,Process>)Field(runtime.SingleWindows,"_processes");
                    var frameRates=(IDictionary<int,int>)Field(runtime.SingleWindows,"_runningMaxFps");
                    try
                    {
                        processes[1]=child;frameRates[1]=120;
                        Check(runtime.SingleWindows.RequiresPhoneScreenOn && !runtime.Scrcpy.BuildArguments(high.Scrcpy,1,"phone-A").Contains("-S"),"live120 app slot also suppresses ordinary DeX screen off");
                        Check(!runtime.ScreenOff.Reapply("phone-A",()=>true),"120 app slot suppresses delayed screen off");
                    }
                    finally {processes.Remove(1);frameRates.Remove(1);}
                }
                finally { Set(runtime.Scrcpy,"_process",null); if(!child.HasExited)child.Kill();child.WaitForExit(); }
            }
            form.Hide(); Set(form,"_allowExit",true);
        }
        runtime.ScreenOff.Dispose();other.ScreenOff.Dispose();
    }
    private static AppSettings settingsServiceClone(SettingsService service,AppSettings settings) { return service.Clone(settings); }
    [STAThread] private static int Main(string[] args)
    {
        if(args.Contains("--hold")){System.Threading.Thread.Sleep(30000);return 0;}
        try
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            PersistedSettings(); V3DefaultsAndLogs();RuntimeAndUi();
            Console.WriteLine("Experimental FPS/v3 policy checks passed: "+checks);return 0;
        }
        catch(Exception exception) { Console.WriteLine(exception);return 1; }
    }
}
