using System;
using System.Threading;
using System.Windows.Forms;
using DexManager.Forms;
using DexManager.Services;
using DexManager.Utils;

namespace DexManager
{
    internal static class Program
    {
        private const string SingleInstanceName =
            "DexManager-73D79582-CC69-4AEC-A24E-F3755E77A32C";
        private static Mutex _singleInstanceMutex;

        [STAThread]
        private static void Main(string[] args)
        {
            bool createdNew;
            _singleInstanceMutex = new Mutex(
                true,
                SingleInstanceName,
                out createdNew);
            if (!createdNew)
            {
                _singleInstanceMutex.Dispose();
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var logService = new LogService();

            try
            {
                var settingsService = new SettingsService(logService);
                var settings = settingsService.Load();
                LocalizationService.Apply(settings.Language);
                logService.SetLogDirectory(
                    settingsService.ResolvePath(settings.Paths.LogFolder));
                logService.Info("DX Manager를 시작합니다.");

                var processRunner = new ProcessRunner(logService);
                var pathService = new PathService(
                    settingsService,
                    logService,
                    processRunner);
                var autoStartService = new AutoStartService(logService);
                autoStartService.Apply(settings.Features.StartWithWindows);
                var adbPath = pathService.SelectAdbPath(
                    settings,
                    settings.Timing.ProcessTimeoutMs);
                Environment.SetEnvironmentVariable(
                    "ADB",
                    adbPath,
                    EnvironmentVariableTarget.Process);
                logService.Info(
                    "Scrcpy가 사용할 ADB 경로를 지정했습니다: " +
                    adbPath);
                var adbService = new AdbService(
                    adbPath,
                    settings.Timing.ProcessTimeoutMs,
                    processRunner,
                    logService);
                var wirelessAdbService = new WirelessAdbService(
                    adbService,
                    settingsService,
                    settings,
                    logService);
                wirelessAdbService.InitializeTarget();
                var scrcpyPath = settingsService.ResolvePath(settings.Paths.ScrcpyPath);
                var scrcpyLaunchCoordinator =
                    new ScrcpyLaunchCoordinator();
                var scrcpyService = new ScrcpyService(
                    scrcpyPath,
                    settings.Timing.ProcessTimeoutMs,
                    processRunner,
                    adbService,
                    scrcpyLaunchCoordinator,
                    logService);
                var singleWindowService = new SingleWindowService(
                    scrcpyPath,
                    settings.Timing.ProcessTimeoutMs,
                    adbService,
                    scrcpyLaunchCoordinator,
                    logService);
                var screenOffService = new ScreenOffService(
                    scrcpyPath,
                    settings.Timing.ProcessTimeoutMs,
                    adbService,
                    scrcpyLaunchCoordinator,
                    logService);
                var virtualDisplayService = new VirtualDisplayService(
                    adbService,
                    logService);
                var orchestrator = new DexOrchestrator(
                    adbService,
                    virtualDisplayService,
                    scrcpyService,
                    settingsService,
                    logService,
                    settings);
                var deviceMonitor = new DeviceMonitorService(
                    adbService,
                    wirelessAdbService,
                    logService,
                    settings.Timing.DeviceMonitorIntervalMs);
                var hotkeyService = new HotkeyService(
                    logService,
                    settings.KeyMappings);
                var captureService = new CaptureService(
                    adbService,
                    settingsService,
                    settings,
                    logService);
                var captureCoordinator = new CaptureCoordinator(
                    hotkeyService,
                    captureService,
                    scrcpyService,
                    singleWindowService,
                    settings,
                    logService);
                var autoHideService = new AutoHideService(
                    scrcpyService,
                    singleWindowService,
                    logService,
                    settings.Timing.AutoHideIdleSeconds);
                var environmentCheckService = new EnvironmentCheckService(
                    adbService,
                    scrcpyService,
                    pathService,
                    logService);
                var keyMappingService = new KeyMappingService(
                    scrcpyService,
                    singleWindowService,
                    adbService,
                    settings,
                    settings.KeyMappings,
                    logService);

                Application.Run(new MainForm(
                    settingsService,
                    settings,
                    logService,
                    adbService,
                    wirelessAdbService,
                    scrcpyService,
                    singleWindowService,
                    screenOffService,
                    deviceMonitor,
                    orchestrator,
                    captureCoordinator,
                    autoHideService,
                    environmentCheckService,
                    keyMappingService,
                    IsAutoRun(args)));
            }
            catch (Exception ex)
            {
                logService.Error("프로그램 초기화에 실패했습니다.", ex);
                MessageBox.Show(
                    LocalizationService.Format(
                        "Program.InitFailed",
                        Environment.NewLine,
                        ex.Message),
                    LocalizationService.Get("App.Name"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                if (_singleInstanceMutex != null)
                {
                    _singleInstanceMutex.ReleaseMutex();
                    _singleInstanceMutex.Dispose();
                    _singleInstanceMutex = null;
                }
            }
        }

        private static bool IsAutoRun(string[] args)
        {
            if (args == null) return false;

            foreach (var argument in args)
            {
                if (string.Equals(
                    argument,
                    "--autorun",
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
