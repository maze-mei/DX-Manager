using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using DexManager.Forms;
using DexManager.Models;
using DexManager.Services;
using DexManager.Utils;

internal static class ScrcpyRuntimeIntegrationTests
{
    private static int checks;
    private static void Check(bool value, string name)
    {
        if (!value) throw new Exception(name);
        checks++; Console.WriteLine("PASS " + name);
    }
    private static int Count(string text, string marker)
    {
        int count = 0, offset = 0;
        while ((offset = text.IndexOf(marker, offset, StringComparison.Ordinal)) >= 0)
        { count++; offset += marker.Length; }
        return count;
    }
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--version")
        {
            Console.WriteLine("scrcpy 5.0.1");
            return 0;
        }
        if (args.Length > 0 && args[0] == "--serial") return RunFakeScrcpy(args);
        if (args.Length > 0 && args[0] == "-s") return 0;

        DeviceRuntimeServiceSet runtime = null;
        try
        {
            LocalizationService.Apply(AppLanguage.Korean);
            var scrcpy = args[0];
            var log = new LogService();
            var runner = new ProcessRunner(log);
            var adb = new AdbService(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "not-a-phone-adb.exe"), 1000, runner, log);
            var settings = AppSettings.CreateDefault();
            var store = new SettingsService(log);
            var path = new PathService(store, log, runner);
            var launch = new ScrcpyLaunchCoordinator();
            var factory = new DeviceRuntimeServiceFactory(scrcpy, adb.AdbPath, 3000,
                runner, adb, launch, store, settings, log, new DeviceRuntimeSessionRegistry());
            runtime = factory.Create();
            Check(runtime.Scrcpy.RuntimeInfo.MajorVersion == 5 &&
                runtime.Scrcpy.RuntimeInfo.ReportedVersion == "5.0.1",
                "actual product detects the selected scrcpy full version 5.0.1");
            Check(runtime.Scrcpy.ReadReportedVersion() == "5.0.1",
                "diagnostics read the selected executable rather than a hard-coded version");
            var dex = runtime.Scrcpy.BuildArguments(settings.Scrcpy, 31, "fixture-phone");
            Check(Count(dex, "--hwdec=auto") == 1, "DeX explicitly selects automatic hardware decoding once");
            var method = typeof(SingleWindowService).GetMethod("BuildArguments",
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new[] { typeof(int), typeof(SingleWindowSlotSettings), typeof(string) }, null);
            for (var slot = 1; slot <= 3; slot++)
            {
                var options = settings.SingleWindowSlots[slot - 1];
                options.StartAppPackage = "com.android.settings";
                var command = (string)method.Invoke(runtime.SingleWindows,
                    new object[] { slot, options, "fixture-phone" });
                Check(Count(command, "--hwdec=auto") == 1,
                    "single-window " + slot + " explicitly selects auto decoding once");
                options.AdditionalArguments = "--hwdec=disabled";
                command = (string)method.Invoke(runtime.SingleWindows,
                    new object[] { slot, options, "fixture-phone" });
                Check(!command.Contains("--hwdec=auto") && Count(command, "--hwdec=disabled") == 1,
                    "single-window " + slot + " preserves an explicit software-decoding override");
            }
            settings.Scrcpy.AdditionalArguments = "--hwdec=d3d11va";
            dex = runtime.Scrcpy.BuildArguments(settings.Scrcpy, 31, "fixture-phone");
            Check(!dex.Contains("--hwdec=auto") && Count(dex, "--hwdec=d3d11va") == 1,
                "DeX preserves an explicit decoder without duplicate arguments");
            settings.Scrcpy.AdditionalArguments = "--hwdec disabled";
            dex = runtime.Scrcpy.BuildArguments(settings.Scrcpy, 31, "fixture-phone");
            Check(!dex.Contains("--hwdec=auto") && dex.Contains("--hwdec disabled"),
                "space-separated explicit decoder overrides are also respected");
            var environment = new EnvironmentCheckService(adb, runtime.Scrcpy, path, log,
                store, settings, () => string.Empty);
            var entries = new List<EnvironmentCheckItem>();
            typeof(EnvironmentCheckService).GetMethod("AddScrcpyVersionCheck",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(environment, new object[] { entries });
            Check(entries.Count == 1 && entries[0].Message == "5.0.1" &&
                entries[0].Status == EnvironmentCheckStatus.Passed,
                "environment inspection includes the actual complete scrcpy version");
            var field = typeof(ScrcpyService).GetField("_scrcpyPath",
                BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(runtime.Scrcpy, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "missing-scrcpy.exe"));
            entries.Clear();
            typeof(EnvironmentCheckService).GetMethod("AddScrcpyVersionCheck",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(environment, new object[] { entries });
            Check(entries.Count == 1 && entries[0].Status == EnvironmentCheckStatus.Warning &&
                entries[0].Message.Contains("실패"),
                "an unreadable executable reports version failure without inventing a version");
            field.SetValue(runtime.Scrcpy, scrcpy);
            TestPhoneKeepActive();
            Console.WriteLine("PASS scrcpy runtime integration: " + checks);
            return 0;
        }
        catch (Exception ex) { Console.WriteLine(ex); return 1; }
        finally { if (runtime != null) { runtime.Scrcpy.Dispose(); runtime.SingleWindows.Dispose(); runtime.FileTransfers.Dispose(); runtime.PhoneTransfers.Dispose(); runtime.CompanionGuardian.Dispose(); } }
    }

    private const string FixtureDirectory = "DXM_KEEP_ACTIVE_TEST_DIRECTORY";

    private static void TestPhoneKeepActive()
    {
        try
        {
            TestPhoneKeepActiveCase("192.0.2.1:5555", native: true, stayAwake: true, expectsHelper: true);
            TestPhoneKeepActiveCase("adb-Fixture._adb-tls-connect._tcp", native: true, stayAwake: true,
                expectsHelper: true, disconnect: true);
            TestPhoneKeepActiveCase("FIXTURE-USB", native: true, stayAwake: true, expectsHelper: false);
            TestPhoneKeepActiveCase("192.0.2.1:5555", native: false, stayAwake: true, expectsHelper: false);
            TestPhoneKeepActiveCase("192.0.2.1:5555", native: true, stayAwake: false, expectsHelper: false);
            TestPhoneKeepActiveCase("192.0.2.1:5555", native: true, stayAwake: true,
                expectsHelper: true, failViewer: true);
        }
        finally { Environment.SetEnvironmentVariable(FixtureDirectory, null); }
    }

    private static void TestPhoneKeepActiveCase(string serial, bool native, bool stayAwake,
        bool expectsHelper, bool disconnect = false, bool failViewer = false)
    {
        var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "keep-active-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        if (failViewer) File.WriteAllText(Path.Combine(directory, "fail-viewer"), string.Empty);
        Environment.SetEnvironmentVariable(FixtureDirectory, directory);
        var executable = Assembly.GetExecutingAssembly().Location;
        using (var log = new LogService())
        {
            var runner = new ProcessRunner(log);
            var adb = new AdbService(executable, 1000, runner, log);
            var settings = AppSettings.CreateDefault();
            settings.Scrcpy.StayAwake = stayAwake;
            using (var transfers = new FileTransferCoordinator(executable, settings, log, new DeviceRuntimeSessionRegistry()))
            using (var viewer = new ScrcpyService(executable, 4000, runner, adb,
                new ScrcpyLaunchCoordinator(), transfers, log))
            {
                var start = typeof(ScrcpyService).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic);
                var failed = false;
                try { start.Invoke(viewer, new object[] { settings.Scrcpy, 31, serial, false, native }); }
                catch (TargetInvocationException) when (failViewer) { failed = true; }
                if (failViewer)
                {
                    Check(failed && !viewer.IsRunning && File.Exists(Path.Combine(directory, "helper.txt")),
                        "viewer startup failure occurs after the control-only connection starts");
                    Check(SpinWait.SpinUntil(() => log.GetSessionEntries().Any(
                        line => line.Contains(LocalizationService.Get("Log.NativeWifiKeepActive.Stopped"))), 2000),
                        "failed viewer startup releases its keep-active process");
                    return;
                }
                Check(bool.Parse(File.ReadAllText(Path.Combine(directory, "viewer.txt"))) == expectsHelper,
                    "only eligible Native DeX Wi-Fi sessions start the control-only connection before the viewer");
                if (!expectsHelper) return;

                var helper = File.ReadAllLines(Path.Combine(directory, "helper.txt"));
                Check(helper[1] == serial && helper[2] == executable && helper.Contains("--display-id=0") &&
                    helper.Contains("--keep-active") && helper.Contains("--no-video") && helper.Contains("--no-audio") &&
                    helper.Contains("--no-window") && helper.Contains("--no-clipboard-autosync") &&
                    helper.Contains("--no-power-on") && !helper.Contains("-S"),
                    "helper uses the selected serial, direct ADB and only main-display activity");
                Check(log.GetSessionEntries().Any(line => line.Contains(LocalizationService.Get("Log.NativeWifiKeepActive.Started"))),
                    "readiness produces the four-second activity log");
                using (var process = Process.GetProcessById(int.Parse(helper[0])))
                {
                    if (disconnect) File.WriteAllText(Path.Combine(directory, "disconnect"), string.Empty);
                    else viewer.Stop();
                    Check(process.WaitForExit(3000), "viewer closure stops its own keep-active process");
                    var key = disconnect ? "Log.NativeWifiKeepActive.Disconnected" : "Log.NativeWifiKeepActive.Stopped";
                    Check(SpinWait.SpinUntil(() => log.GetSessionEntries().Count(
                        line => line.Contains(LocalizationService.Get(key))) == 1, 2000),
                        "keep-active logs its termination once");
                }
            }
        }
    }

    private static int RunFakeScrcpy(string[] args)
    {
        var directory = Environment.GetEnvironmentVariable(FixtureDirectory);
        if (args.Contains("--no-window"))
        {
            File.WriteAllLines(Path.Combine(directory, "helper.txt"),
                new[] { Process.GetCurrentProcess().Id.ToString(), args[1], Environment.GetEnvironmentVariable("ADB") }.Concat(args));
            Console.WriteLine("DEBUG: Server connected");
            Thread.Sleep(15000);
            return 0;
        }

        File.WriteAllText(Path.Combine(directory, "viewer.txt"), File.Exists(Path.Combine(directory, "helper.txt")).ToString());
        if (File.Exists(Path.Combine(directory, "fail-viewer"))) return 1;
        var disconnected = false;
        using (var window = new Form { Text = "Scrcpy test fixture", FormBorderStyle = FormBorderStyle.FixedToolWindow,
            StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000) })
        using (var timer = new System.Windows.Forms.Timer { Interval = 50 })
        {
            var lifetime = Stopwatch.StartNew();
            timer.Tick += delegate
            {
                disconnected = File.Exists(Path.Combine(directory, "disconnect"));
                if (disconnected || lifetime.ElapsedMilliseconds > 15000) window.Close();
            };
            timer.Start();
            Application.Run(window);
        }
        return disconnected ? 2 : 0;
    }
}
