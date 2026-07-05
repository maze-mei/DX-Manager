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

        public ScreenOffService(
            string scrcpyPath,
            int processTimeoutMs,
            AdbService adbService,
            ScrcpyLaunchCoordinator launchCoordinator,
            LogService logService)
        {
            if (string.IsNullOrWhiteSpace(scrcpyPath))
                throw new ArgumentException(
                    "Scrcpy 경로가 비어 있습니다.",
                    "scrcpyPath");

            _scrcpyPath = Path.GetFullPath(scrcpyPath);
            _processTimeoutMs = Math.Min(
                Math.Max(processTimeoutMs, 1000),
                5000);
            _adbService = adbService ??
                throw new ArgumentNullException("adbService");
            _launchCoordinator = launchCoordinator ??
                throw new ArgumentNullException("launchCoordinator");
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
                    _logService.Info(
                        "화면 OFF 재적용 조건이 사라져 실행을 취소했습니다.");
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
                    "scrcpy.exe를 찾을 수 없습니다.",
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
                    _logService.Info(
                        "휴대폰 화면 OFF 재적용 Scrcpy를 순차 실행합니다.");
                    process.Start();
                    started = true;
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    var stopwatch = Stopwatch.StartNew();
                    while (stopwatch.ElapsedMilliseconds < _processTimeoutMs)
                    {
                        if (confirmation.Wait(50))
                        {
                            _logService.Info(
                                "휴대폰 화면 OFF 재적용을 확인했습니다.");
                            return true;
                        }
                        if (process.HasExited) break;
                    }

                    _logService.Warning(
                        "휴대폰 화면 OFF 재적용 확인에 실패했습니다.");
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
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                }
            };
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}
