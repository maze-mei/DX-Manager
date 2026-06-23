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
                MessageBox.Show(
                    "DEX Manager가 이미 실행 중입니다.\r\n시스템 트레이를 확인하십시오.",
                    "DEX Manager",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
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
                logService.SetLogDirectory(
                    settingsService.ResolvePath(settings.Paths.LogFolder));
                logService.Info("DEX Manager를 시작합니다.");

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
                var adbService = new AdbService(
                    adbPath,
                    settings.Timing.ProcessTimeoutMs,
                    processRunner,
                    logService);
                var scrcpyPath = settingsService.ResolvePath(settings.Paths.ScrcpyPath);
                var scrcpyService = new ScrcpyService(
                    scrcpyPath,
                    settings.Timing.ProcessTimeoutMs,
                    processRunner,
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
                    settings,
                    logService);
                var autoHideService = new AutoHideService(
                    scrcpyService,
                    logService,
                    settings.Timing.AutoHideIdleSeconds);
                var environmentCheckService = new EnvironmentCheckService(
                    adbService,
                    scrcpyService,
                    pathService,
                    logService);
                var keyMappingService = new KeyMappingService(
                    scrcpyService,
                    adbService,
                    settings,
                    settings.KeyMappings,
                    logService);

                Application.Run(new MainForm(
                    settingsService,
                    settings,
                    logService,
                    adbService,
                    scrcpyService,
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
                    "DEX Manager 초기화에 실패했습니다.\r\n\r\n" + ex.Message,
                    "DEX Manager",
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
