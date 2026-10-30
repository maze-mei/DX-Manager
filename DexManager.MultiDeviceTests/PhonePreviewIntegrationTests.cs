// Feature integration and regression tests: asyouplz.
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Forms;
using DexManager.Forms;
using DexManager.Models;
using DexManager.Services;
using DexManager.Utils;

internal static class PhonePreviewIntegrationTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        checks++;
    }
    private static object Field(object target, string name) { return target.GetType().GetField(name, Hidden).GetValue(target); }
    private static void Set(object target, string name, object value) { target.GetType().GetField(name, Hidden).SetValue(target, value); }

    private static void SettingsAndRuntime()
    {
        var log = new LogService();
        var service = new SettingsService(log);
        var settings = AppSettings.CreateDefault();
        foreach (var preference in new[] { true, false })
        {
            var phone = settings.GetOrCreateDeviceRunSettings("serial:phone-a");
            phone.VirtualDisplay.HidePhonePreview = true;
            phone.Scrcpy.MaxFps = 60;
            phone.Scrcpy.TurnScreenOff = preference;
            phone.Scrcpy.TurnScreenOffBeforeHighFps = null;
            service.Save(settings);
            settings = service.Load();
            phone = settings.GetOrCreateDeviceRunSettings("serial:phone-a");
            Check(phone.VirtualDisplay.HidePhonePreview && phone.Scrcpy.TurnScreenOff == preference, "hidden mode keeps the saved ordinary power preference");
            phone.Scrcpy.MaxFps = 120;
            phone.Scrcpy.TurnScreenOffBeforeHighFps = preference;
            service.Save(settings);
            settings = service.Load();
            phone = settings.GetOrCreateDeviceRunSettings("serial:phone-a");
            Check(phone.VirtualDisplay.HidePhonePreview && !phone.Scrcpy.TurnScreenOff, "120 and hidden mode survive migration together");
            phone.Scrcpy.MaxFps = 60;
            service.Save(settings);
            settings = service.Load();
            phone = settings.GetOrCreateDeviceRunSettings("serial:phone-a");
            Check(phone.Scrcpy.TurnScreenOff == preference && !phone.Scrcpy.TurnScreenOffBeforeHighFps.HasValue, "leaving 120 restores the exact saved preference");
            Check(!settings.GetOrCreateDeviceRunSettings("serial:phone-b").VirtualDisplay.HidePhonePreview, "other phone remains in ordinary mode");
        }

        var runner = new ProcessRunner(log);
        runner.BlockNewProcessesForWindowsShutdown();
        var adbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "unused-adb.exe");
        var adb = new AdbService(adbPath, 1000, runner, log);
        var factory = new DeviceRuntimeServiceFactory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "unused-scrcpy.exe"),
            adbPath, 1000, runner, adb, new ScrcpyLaunchCoordinator(), service, settings, log, new DeviceRuntimeSessionRegistry());
        var runtime = factory.Create();
        var other = factory.Create();
        try
        {
            var high = DeviceRunSettingsProfile.Create("serial:phone-a", settings.VirtualDisplay, settings.Scrcpy, settings.LastSuccess, settings.SingleWindowSlots, settings.SingleWindowAppProfiles);
            high.Scrcpy.MaxFps = 120;
            var gate = Field(runtime.Dex, "_operationGate");
            Task pending;
            lock (gate)
            {
                pending = runtime.Dex.StartAsync("phone-a", high);
                runtime.Dex.CancelPendingStart();
            }
            pending.GetAwaiter().GetResult();
            Check(!runtime.Dex.IsBusy && !runtime.Dex.IsRunning, "cancelled queued start drains without ADB or consent IO");
            Task stopping;
            lock (gate)
            {
                pending = runtime.Dex.StartAsync("phone-a", high);
                stopping = runtime.Dex.StopForSerialAsync("phone-a");
            }
            Task.WhenAll(pending, stopping).GetAwaiter().GetResult();
            Check(!runtime.Dex.IsBusy, "transport loss cancels the queued start for that transport");
            lock (gate)
            {
                pending = runtime.Dex.StartAsync("phone-a", high);
                stopping = runtime.Dex.StopForSerialAsync("phone-b");
            }
            var consentBlocked = false;
            try { pending.GetAwaiter().GetResult(); }
            catch (InvalidOperationException ex) { consentBlocked = ex.Message.Contains("120 FPS"); }
            stopping.GetAwaiter().GetResult();
            Check(consentBlocked && !runtime.Dex.IsBusy, "another transport cannot cancel a start or bypass 120 consent");

            var ordinary = DeviceRunSettingsProfile.Create("serial:phone-a", settings.VirtualDisplay, settings.Scrcpy, settings.LastSuccess, settings.SingleWindowSlots, settings.SingleWindowAppProfiles).Scrcpy;
            ordinary.MaxFps = 60; ordinary.TurnScreenOff = true;
            using (var current = Process.GetCurrentProcess())
            {
                try
                {
                    Set(runtime.Dex, "_lastStartUsedLoopback", true);
                    Set(runtime.Scrcpy, "_process", current);
                    Check(runtime.Dex.RequiresPhoneScreenOn, "running hidden DeX owns a screen-on requirement");
                    Check(!runtime.Scrcpy.BuildArguments(ordinary, 1, "phone-a").Contains("-S"), "hidden DeX suppresses the same-phone screen-off argument");
                    var slot = settings.SingleWindowSlots[0]; slot.MaxFps = 60; slot.TurnScreenOff = true;
                    var arguments = (string)runtime.SingleWindows.GetType().GetMethod("BuildArguments", Hidden, null, new[] { typeof(int), typeof(SingleWindowSlotSettings), typeof(string) }, null)
                        .Invoke(runtime.SingleWindows, new object[] { 1, slot, "phone-a" });
                    Check(!arguments.Contains("-S"), "hidden DeX suppresses same-phone single-window screen-off");
                    Check(!runtime.ScreenOff.Reapply("phone-a", () => true), "hidden mode blocks delayed screen-off before process IO");
                    Check(other.Scrcpy.BuildArguments(ordinary, 1, "phone-b").Contains("-S"), "another physical phone keeps its own power policy");
                }
                finally { Set(runtime.Scrcpy, "_process", null); }
            }
            Check(!runtime.Dex.RequiresPhoneScreenOn && runtime.Scrcpy.BuildArguments(ordinary, 1, "phone-a").Contains("-S"), "hidden process exit releases its screen-on requirement");
        }
        finally { runtime.ScreenOff.Dispose(); other.ScreenOff.Dispose(); }
    }

    private static object Call(object target, string name, params object[] args)
    {
        var method=target.GetType().GetMethods(Hidden | BindingFlags.Public);
        return System.Linq.Enumerable.Single(method, x => x.Name==name && x.GetParameters().Length==args.Length).Invoke(target,args);
    }
    private static void Ui()
    {
        var settings=AppSettings.CreateDefault();settings.Features.AutoStartDexOnDeviceConnected=false;
        var log=new LogService();var store=new SettingsService(log);
        var runner=new ProcessRunner(log);runner.BlockNewProcessesForWindowsShutdown();
        var adb=new AdbService("unused-adb.exe",1000,runner,log);var paths=new PathService(store,log,runner);
        var wifi=new WirelessAdbService(adb,store,settings,log);var registry=new PhysicalDeviceRegistry();
        var sessions=new DeviceRuntimeSessionRegistry();var launch=new ScrcpyLaunchCoordinator();
        var factory=new DeviceRuntimeServiceFactory("unused-scrcpy.exe","unused-adb.exe",1000,runner,adb,launch,store,settings,log,sessions);
        var runtime=factory.Create();var monitor=new DeviceMonitorService(adb,wifi,registry,log,1000,1000);
        var hotkeys=new HotkeyService(log,settings);var capture=new CaptureService(adb,store,settings,log);
        var captures=new CaptureCoordinator(hotkeys,capture,runtime.Scrcpy,runtime.SingleWindows,settings,log);
        var autoHide=new AutoHideService(runtime.Scrcpy,runtime.SingleWindows,log,60);
        var environment=new EnvironmentCheckService(adb,runtime.Scrcpy,paths,log,store,settings,()=>null);
        var keymap=new KeyMappingService(runtime.Scrcpy,runtime.SingleWindows,adb,settings,settings.KeyMappings,log);
        try
        {
            foreach(var language in new[]{AppLanguage.English,AppLanguage.Korean})
            foreach(var theme in new[]{AppTheme.Light,AppTheme.Dark})
            {
                settings.Language=language;settings.Theme=theme;LocalizationService.Apply(language);
                using(var main=new MainForm(store,settings,log,adb,wifi,runtime.Scrcpy,runtime.SingleWindows,runtime.ScreenOff,launch,monitor,runtime.Dex,captures,autoHide,new AutoStartService(log),environment,keymap,runtime.FileTransfers,runtime.PhoneTransfers,sessions,runtime,registry,factory,paths,capture,false))
                {
                    main.Shown-=(EventHandler)Delegate.CreateDelegate(typeof(EventHandler),main,typeof(MainForm).GetMethod("MainForm_Shown",Hidden));
                    main.StartPosition=FormStartPosition.Manual;main.Location=new Point(-4000,-4000);main.ShowInTaskbar=false;main.Show();Application.DoEvents();
                    Call(main,"DisplayMode",0);
                    var hidden=(CheckBox)Field(main,"_hidePhonePreviewBox");var off=(CheckBox)Field(main,"_turnScreenOffBox");
                    Check(hidden.Visible && !hidden.Checked,"hidden preview is opt-in in DeX mode");
                    Check(hidden.Parent==off.Parent && !hidden.Bounds.IntersectsWith(((Control)Field(main,"_stayAwakeBox")).Bounds),"new option has an independent launch-options position");
                    Set(main,"_loadingRunSettings",true);hidden.Checked=true;Call(main,"UpdatePhonePreviewControls");
                    Check(!off.Enabled && !((Control)Field(main,"_dpiBox")).Enabled && !((Control)Field(main,"_resolutionBox")).Enabled,"hidden mode disables incompatible power and size controls");
                    hidden.Checked=false;Set(main,"_loadingRunSettings",false);Call(main,"UpdatePhonePreviewControls");
                    Check(off.Enabled && ((Control)Field(main,"_dpiBox")).Enabled,"ordinary mode restores control availability");
                    var fps=Field(main,"_maxFpsBox");fps.GetType().GetProperty("SelectedIndex").SetValue(fps,2,null);Application.DoEvents();
                    Check(!off.Enabled,"120 FPS retains screen-on priority without hidden mode");
                    fps.GetType().GetProperty("SelectedIndex").SetValue(fps,1,null);Application.DoEvents();
                    Call(main,"DisplayMode",1);Check(!hidden.Visible,"single-app mode does not expose the DeX-only option");
                    Call(main,"DisplayMode",0);Call(main,"HideToTray",false);
                }
            }
        }
        finally {captures.Dispose();autoHide.Dispose();hotkeys.Dispose();keymap.Dispose();monitor.Dispose();runtime.ScreenOff.Dispose();}
    }
    [STAThread] private static int Main()
    {
        try
        {
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            SettingsAndRuntime();Ui();
            Console.WriteLine("Phone-preview settings, lifecycle, power and UI checks passed: " + checks);
            return 0;
        }
        catch(Exception exception){Console.WriteLine(exception);return 1;}
    }
}
