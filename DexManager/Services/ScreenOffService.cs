using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace DexManager.Services
{
    public sealed class ScreenOffService : IDisposable
    {
        private const string ConfirmationText = "Device display turned off";
        private readonly string _scrcpyPath;
        private readonly int _processTimeoutMs;
        private readonly AdbService _adbService;
        private readonly ScrcpyLaunchCoordinator _launchCoordinator;
        private readonly LogService _logService;
        private readonly ScrcpyRuntimeInfo _runtimeInfo;

        public ScreenOffService(
            string scrcpyPath,
            int processTimeoutMs,
            AdbService adbService,
            ScrcpyLaunchCoordinator launchCoordinator,
            ScrcpyRuntimeInfo runtimeInfo,
            LogService logService)
        {
            if (string.IsNullOrWhiteSpace(scrcpyPath))
                throw new ArgumentException(
                    LocalizationService.Get(
                        "Error.Scrcpy.PathEmpty"),
                    "scrcpyPath");

            _scrcpyPath = Path.GetFullPath(scrcpyPath);
            _processTimeoutMs = Math.Min(
                Math.Max(processTimeoutMs, 1000),
                5000);
            _adbService = adbService ??
                throw new ArgumentNullException("adbService");
            _launchCoordinator = launchCoordinator ??
                throw new ArgumentNullException("launchCoordinator");
            _runtimeInfo = runtimeInfo ??
                throw new ArgumentNullException("runtimeInfo");
            _logService = logService ??
                throw new ArgumentNullException("logService");
        }

        public bool Reapply(Func<bool> shouldRun)
        {
            if (shouldRun == null)
                throw new ArgumentNullException("shouldRun");

            return _launchCoordinator.RunExclusive(delegate
            {
                if (!shouldRun())
                {
                    _logService.Info(LocalizationService.Get(
                        "Log.ScreenOff.Cancelled"));
                    return false;
                }
                return RunOnce();
            });
        }

        public void Dispose()
        {
        }

        private bool RunOnce()
        {
            if (!File.Exists(_scrcpyPath))
                throw new FileNotFoundException(
                    LocalizationService.Get(
                        "Error.Scrcpy.FileNotFound"),
                    _scrcpyPath);

            var serial = _adbService.TargetSerial;
            var arguments =
                (string.IsNullOrWhiteSpace(serial)
                    ? string.Empty
                    : "--serial " + Quote(serial) + " ") +
                "--no-video --no-audio --no-window -S --no-power-on " +
                "--no-cleanup";
            using (var confirmation = new ManualResetEventSlim(false))
            using (var process = CreateProcess(arguments))
            {
                var started = false;
                DataReceivedEventHandler outputHandler = delegate(
                    object sender,
                    DataReceivedEventArgs e)
                {
                    HandleOutput(e.Data, false, confirmation);
                };
                DataReceivedEventHandler errorHandler = delegate(
                    object sender,
                    DataReceivedEventArgs e)
                {
                    HandleOutput(e.Data, true, confirmation);
                };
                process.OutputDataReceived += outputHandler;
                process.ErrorDataReceived += errorHandler;

                try
                {
                    _logService.Info(LocalizationService.Get(
                        "Log.ScreenOff.Starting"));
                    process.Start();
                    started = true;
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    var stopwatch = Stopwatch.StartNew();
                    while (stopwatch.ElapsedMilliseconds < _processTimeoutMs)
                    {
                        if (confirmation.Wait(50))
                        {
                            _logService.Info(LocalizationService.Get(
                                "Log.ScreenOff.Confirmed"));
                            return true;
                        }
                        if (process.HasExited) break;
                    }

                    _logService.Warning(LocalizationService.Get(
                        "Log.ScreenOff.ConfirmationFailed"));
                    return false;
                }
                finally
                {
                    if (started && !process.HasExited)
                    {
                        process.Kill();
                        process.WaitForExit(2000);
                    }
                    process.OutputDataReceived -= outputHandler;
                    process.ErrorDataReceived -= errorHandler;
                }
            }
        }

        private void HandleOutput(
            string data,
            bool warning,
            ManualResetEventSlim confirmation)
        {
            if (string.IsNullOrWhiteSpace(data)) return;

            if (warning)
                _logService.Warning("[screen-off scrcpy] " + data);
            else
                _logService.Info("[screen-off scrcpy] " + data);

            if (data.IndexOf(
                ConfirmationText,
                StringComparison.OrdinalIgnoreCase) >= 0)
            {
                confirmation.Set();
            }
        }

        private Process CreateProcess(string arguments)
        {
            return new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _scrcpyPath,
                    Arguments = arguments,
                    WorkingDirectory = Path.GetDirectoryName(_scrcpyPath),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = _runtimeInfo.OutputEncoding,
                    StandardErrorEncoding = _runtimeInfo.OutputEncoding
                }
            };
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}
