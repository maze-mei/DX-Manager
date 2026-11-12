using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using DexManager.Forms;
using DexManager.Models;
using DexManager.Services;
using DexManager.Utils;

internal static class DesktopNoticeIntegrationTests
{
    private static int checks;
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr window, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr window, int index, int value);
    private static object Field(object instance, string name) { return instance.GetType().GetField(name, Hidden).GetValue(instance); }
    private static void SetField(object instance, string name, object value) { instance.GetType().GetField(name, Hidden).SetValue(instance, value); }
    private static object Invoke(object instance, string name, params object[] args)
    {
        var types = new Type[args.Length];
        for (var i = 0; i < args.Length; i++) types[i] = args[i] == null ? typeof(object) : args[i].GetType();
        return instance.GetType().GetMethod(name, Hidden, null, types, null).Invoke(instance, args);
    }
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
    private static bool NoticeVisible(MainForm form) { var notice = Field(form, "_appNotice") as Control; return notice != null && notice.Visible; }
    private static void Mode(MainForm form, int slot) { Invoke(form, "DisplayMode", slot); }
    private static void StartWithoutApp(MainForm form, int slot)
    {
        ((Task)Invoke(form, "StartSingleWindowAsync", slot)).GetAwaiter().GetResult();
        Check(NoticeVisible(form), "missing app notice appears for slot " + slot);
    }
    private static void CloseNotice(MainForm form)
    {
        foreach (Control control in ((Control)Field(form, "_appNotice")).Controls)
        {
            var button = control as Button;
            if (button != null) { button.PerformClick(); return; }
        }
        throw new Exception("notice close button missing");
    }
    private static void ShowOffscreen(Form form)
    {
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-5000, -5000);
        form.ShowInTaskbar = false;
        SetWindowLong(form.Handle, -20, GetWindowLong(form.Handle, -20) | 0x08000000);
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        form.Show(); form.PerformLayout(); Application.DoEvents();
    }
    private static void Capture(Form form, string name)
    {
        ShowOffscreen(form);
        using (var bitmap = new Bitmap(form.Width, form.Height))
        using (var stream = new MemoryStream())
        {
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            bitmap.Save(stream, ImageFormat.Png);
            Console.WriteLine("IMAGE:" + name + ":" + Convert.ToBase64String(stream.ToArray()));
        }
    }
    private static void DisposeIfNeeded(object value) { var disposable = value as IDisposable; if (disposable != null) disposable.Dispose(); }
    private static void WaitForChild(Process child)
    {
        var time = Stopwatch.StartNew();
        while (!child.HasExited && time.ElapsedMilliseconds < 6000) { Application.DoEvents(); Thread.Sleep(10); }
        if (!child.HasExited) { child.Kill(); throw new Exception("duplicate test process did not exit"); }
        Check(child.ExitCode == 0, "secondary program exits after signaling the primary");
        Application.DoEvents();
    }
    private static void VerifyRelaunch(MainForm form)
    {
        var assembly = typeof(MainForm).Assembly;
        var program = assembly.GetType("DexManager.Program");
        var name = (string)program.GetField("SingleInstanceName", BindingFlags.NonPublic | BindingFlags.Static).GetRawConstantValue();
        var type = assembly.GetType("DexManager.Services.SingleInstanceActivationService");
        using (var mutex = new Mutex(false, name))
        {
            Check(mutex.WaitOne(0), "UI fixture exclusively owns the private instance scope");
            var listener = Activator.CreateInstance(type, new object[] { name });
            try
            {
                EventHandler activate = delegate { Invoke(form, "RequestWindowActivation"); };
                type.GetEvent("ActivationRequested").AddEventHandler(listener, activate);
                type.GetMethod("StartListening").Invoke(listener, null);
                Invoke(form, "HideToTray", false);
                Check(!form.Visible, "primary window is hidden in the tray before re-execution");
                var info = new ProcessStartInfo(assembly.Location) { UseShellExecute = false, CreateNoWindow = true };
                using (var child = Process.Start(info)) WaitForChild(child);
                var deadline = Stopwatch.StartNew();
                while (!form.Visible && deadline.ElapsedMilliseconds < 2000) { Application.DoEvents(); Thread.Sleep(10); }
                Check(form.Visible && form.WindowState == FormWindowState.Normal,
                    "actual EXE re-execution restores the existing MainForm");
                Invoke(form, "HideToTray", false); info.Arguments = "--autorun";
                using (var child = Process.Start(info)) WaitForChild(child);
                Check(!form.Visible, "duplicate Windows startup does not open the tray window");
                Invoke(form, "RequestWindowActivation");
            }
            finally { DisposeIfNeeded(listener); mutex.ReleaseMutex(); }
        }
    }
    private static void VerifyAttachedWindows(MainForm form, SettingsService store, AppSettings settings,
        AdbService adb, WirelessAdbService wifi, LogService log, EnvironmentCheckService environment,
        DeviceRuntimeServiceSet runtime)
    {
        foreach (var path in new[] { "close-button", "manual-tray", "idle-hide" })
        {
            Invoke(form, "RequestWindowActivation");
            Console.WriteLine("STAGE construct attached windows for " + path);
            var secondary = new Form[]
            {
                new SettingsForm(store, settings, adb, wifi, null, null, null, null, log, null, null, null, null, null, null),
                new HelpForm(), new DocumentationForm(), new LogForm(log),
                new EnvironmentCheckForm(environment), new ThirdPartyLicensesForm(),
                new FileTransferStatusForm(runtime.FileTransfers, settings.Theme, "fixture"),
                new PhoneTransferStatusForm(settings.Theme, "fixture")
            };
            Console.WriteLine("STAGE attached constructors completed");
            var miniType = typeof(MainForm).Assembly.GetType("DexManager.Forms.MiniControlBarForm");
            using (var mini = (Form)Activator.CreateInstance(miniType, Hidden, null,
                new object[] { IntPtr.Zero, settings.Theme, "F8" }, null))
            using (var hint = new CaptureHintOverlayForm())
            {
                foreach (var window in secondary) { Console.WriteLine("STAGE show " + window.GetType().Name); ShowOffscreen(window); }
                Console.WriteLine("STAGE show mini and hint"); ShowOffscreen(mini); ShowOffscreen(hint);
                Console.WriteLine("STAGE hide via " + path);
                if (path == "close-button") form.Close();
                else if (path == "idle-hide") Invoke(form, "HideApplicationForIdle");
                else Invoke(form, "HideToTray", false);
                Application.DoEvents();
                Check(!form.Visible && !form.IsDisposed, path + " keeps the main process in the tray");
                foreach (var window in secondary)
                    Check(window.IsDisposed && !window.Visible, path + " closes " + window.GetType().Name);
                Check(!mini.IsDisposed && mini.Visible, path + " leaves the scrcpy mini bar managed separately");
                Check(!hint.Visible && !hint.IsDisposed, path + " hides the reusable capture hint");
                Check((bool)Field(form, "_hiddenToTray"), path + " suppresses automatic transfer-window reopening");
                Invoke(form, "RequestWindowActivation");
                Check(!(bool)Field(form, "_hiddenToTray") && form.Visible, "restoring main re-enables transfer notices");
                Check(Array.TrueForAll(secondary, window => !window.Visible),
                    "restoring main does not reopen dismissed secondary windows");
            }
            foreach (var window in secondary) window.Dispose();
        }
    }
    [STAThread] private static int Main()
    {
        MainForm form = null;
        DeviceRuntimeServiceSet runtime = null;
        CaptureCoordinator captureCoordinator = null;
        DeviceMonitorService monitor = null;
        KeyMappingService keymap = null;
        AutoHideService autoHide = null;
        HotkeyService hotkeys = null;
        try
        {
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e) { var failure = e.ExceptionObject as Exception; if (failure != null) { Console.WriteLine("UNHANDLED TYPE: " + failure.GetType().FullName); Console.WriteLine("UNHANDLED MESSAGE: " + failure.Message); } };
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            LocalizationService.Apply(AppLanguage.Korean);
            var settings = AppSettings.CreateDefault();
            settings.Theme = AppTheme.Dark;
            settings.Language = AppLanguage.Korean;
            settings.Features.AutoStartDexOnDeviceConnected = false;
            settings.Features.AutoHideEnabled = false;
            var log = new LogService();
            var service = new SettingsService(log);
            var runner = new ProcessRunner(log); runner.BlockNewProcessesForWindowsShutdown();
            var adbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "unused-adb.exe");
            var scrcpyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "unused-scrcpy.exe");
            var adb = new AdbService(adbPath, 1000, runner, log);
            var path = new PathService(service, log, runner);
            var wifi = new WirelessAdbService(adb, service, settings, log);
            var registry = new PhysicalDeviceRegistry();
            var sessions = new DeviceRuntimeSessionRegistry();
            var launch = new ScrcpyLaunchCoordinator();
            var factory = new DeviceRuntimeServiceFactory(scrcpyPath, adbPath, 1000, runner, adb,
                launch, service, settings, log, sessions);
            runtime = factory.Create();
            monitor = new DeviceMonitorService(adb, wifi, registry, log, 1000, 1000);
            hotkeys = new HotkeyService(log, settings);
            var capture = new CaptureService(adb, service, settings, log);
            captureCoordinator = new CaptureCoordinator(hotkeys, capture, runtime.Scrcpy,
                runtime.SingleWindows, settings, log);
            autoHide = new AutoHideService(runtime.Scrcpy, runtime.SingleWindows, log, 60);
            var environment = new EnvironmentCheckService(adb, runtime.Scrcpy, path, log,
                service, settings, () => wifi.SelectedSerial);
            keymap = new KeyMappingService(runtime.Scrcpy, runtime.SingleWindows, adb,
                settings, settings.KeyMappings, log, runtime.CompanionGuardian.ForegroundApps);
            form = new MainForm(service, settings, log, adb, wifi, runtime.Scrcpy, runtime.SingleWindows,
                runtime.ScreenOff, launch, monitor, runtime.Dex, captureCoordinator, autoHide,
                new AutoStartService(log), environment, keymap, runtime.FileTransfers, runtime.PhoneTransfers,
                sessions, runtime, registry, factory, path, capture, false);
            // Exercise real controls without starting monitoring, hooks, onboarding or device commands.
            var shown = typeof(MainForm).GetMethod("MainForm_Shown", Hidden);
            form.Shown -= (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), form, shown);
            ShowOffscreen(form);

            SetField(form, "_selectedDeviceIdentity", "fixture-A"); Mode(form, 2);
            StartWithoutApp(form, 2);
            foreach (var slot in new[] { 0, 1, 3 })
            {
                Mode(form, slot);
                Check(!NoticeVisible(form), "slot 2 notice is hidden in mode " + slot);
            }
            Mode(form, 2);
            Check(NoticeVisible(form), "slot 2 notice returns when its slot is selected");
            var slotWithApp = Invoke(form, "GetSingleWindowSettings", 1);
            slotWithApp.GetType().GetProperty("StartAppPackage").SetValue(slotWithApp, "com.android.chrome", null);
            Mode(form, 1);
            Check(!NoticeVisible(form), "notice stays hidden in another slot with an app selected");
            Mode(form, 2);
            Check(NoticeVisible(form), "another slot's app does not clear slot 2 notice during switching");
            slotWithApp.GetType().GetProperty("StartAppPackage").SetValue(slotWithApp, "", null);
            Mode(form, 1); StartWithoutApp(form, 1);
            Mode(form, 2); Check(NoticeVisible(form), "slot 2 retains its independent notice");
            Mode(form, 1); CloseNotice(form);
            Check(!NoticeVisible(form), "close button dismisses only slot 1 notice");
            Mode(form, 0); Mode(form, 1);
            Check(!NoticeVisible(form), "dismissed slot 1 notice stays dismissed");
            Mode(form, 2); Check(NoticeVisible(form), "closing slot 1 preserves slot 2 notice");

            SetField(form, "_selectedDeviceIdentity", "fixture-B"); Mode(form, 2);
            Check(!NoticeVisible(form), "device A notice does not leak to device B");
            StartWithoutApp(form, 2);
            SetField(form, "_selectedDeviceIdentity", "fixture-A"); Mode(form, 2);
            Check(NoticeVisible(form), "device A retains its own slot 2 notice");
            SetField(form, "_selectedDeviceIdentity", "fixture-B"); Mode(form, 2); CloseNotice(form);
            SetField(form, "_selectedDeviceIdentity", "fixture-A"); Mode(form, 2);
            Check(NoticeVisible(form), "closing device B notice preserves device A notice");

            Invoke(form, "SetSelectedAppPackage", "com.openai.chatgpt", "ChatGPT");
            Invoke(form, "UpdateStartAppAvailability");
            Check(!NoticeVisible(form), "selecting an app clears that slot's missing app notice");
            Mode(form, 0); Mode(form, 2);
            Check(!NoticeVisible(form), "resolved notice stays cleared after returning");
            StartWithoutApp(form, 2);
            Invoke(form, "ShowAppsLoadedNotice", 3, "fixture-A");
            Mode(form, 0);
            Check(NoticeVisible(form), "general app-list notices retain their existing cross-mode behavior");
            CloseNotice(form); Mode(form, 2);
            Check(NoticeVisible(form), "general notice does not erase a pending slot notice");
            Mode(form, 0);
            Check(!NoticeVisible(form), "pending notice stays hidden in DeX");

            VerifyRelaunch(form);
            VerifyAttachedWindows(form, service, settings, adb, wifi, log, environment, runtime);

            foreach (var theme in new[] { AppTheme.Dark, AppTheme.Light })
            {
                Invoke(form, "ApplyThemeSelection", theme);
                var textType = typeof(MainForm).Assembly.GetType("DexManager.Forms.UiText");
                using (var bitmap = new Bitmap(10, 10))
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    textType.GetMethod("Configure", BindingFlags.Static | BindingFlags.Public).Invoke(null, new object[] { graphics });
                    Check(graphics.TextRenderingHint == System.Drawing.Text.TextRenderingHint.AntiAlias,
                        "verified F2 renderer remains active in " + theme);
                }
                Check(((LinkLabel)Field(form, "_advancedToggle")).UseCompatibleTextRendering,
                    "native link retains F2 compatible text in " + theme);
                ((Label)Field(form, "_pageTitle")).Text = "Galaxy S26 Ultra";
                Capture(form, "Main-" + theme);
                using (var help = new HelpForm()) Capture(help, "Help-" + theme);
            }
            Console.WriteLine("PASS desktop notice/UI integration: " + checks);
            return 0;
        }
        catch (Exception ex) { Console.WriteLine("FAIL TYPE: " + ex.GetType().FullName); Console.WriteLine("FAIL MESSAGE: " + ex.Message); if (ex.InnerException != null) Console.WriteLine("INNER: " + ex.InnerException.GetType().FullName + " " + ex.InnerException.Message); return 1; }
        finally
        {
            if (form != null) { DisposeIfNeeded(Field(form, "_trayService")); form.Dispose(); }
            DisposeIfNeeded(captureCoordinator); DisposeIfNeeded(autoHide); DisposeIfNeeded(hotkeys);
            DisposeIfNeeded(keymap); DisposeIfNeeded(monitor); DisposeIfNeeded(runtime);
        }
    }
}
