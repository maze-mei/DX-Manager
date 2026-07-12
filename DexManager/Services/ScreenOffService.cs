using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
        private readonly object _stateLock = new object();
        private readonly ManualResetEvent _shutdownSignal =
            new ManualResetEvent(false);
        private readonly ManualResetEvent _idleSignal =
            new ManualResetEvent(true);
        private int _activeOperations;
        private bool _disposed;
        private bool _signalsDisposed;
        private bool _signalCleanupScheduled;

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
            return Reapply(_adbService.TargetSerial, shouldRun);
        }

        public bool Reapply(string serial, Func<bool> shouldRun)
        {
            if (shouldRun == null)
                throw new ArgumentNullException("shouldRun");

            lock (_stateLock)
            {
                if (_disposed || _shutdownSignal.WaitOne(0)) return false;
                _activeOperations++;
                _idleSignal.Reset();
            }

            try
            {
                return _launchCoordinator.RunExclusive(delegate
                {
                    if (_shutdownSignal.WaitOne(0) || !shouldRun())
                    {
                        _logService.Info(LocalizationService.Get(
                            "Log.ScreenOff.Cancelled"));
                        return false;
                    }
                    return RunOnce(serial);
                });
            }
            finally
            {
                lock (_stateLock)
                {
                    _activeOperations--;
                    if (_activeOperations == 0) _idleSignal.Set();
                }
            }
        }

        public void RequestShutdown()
        {
            lock (_stateLock)
            {
                if (_disposed) return;
                _shutdownSignal.Set();
            }
        }

        public void Dispose()
        {
            lock (_stateLock)
            {
                if (_signalsDisposed) return;
                _disposed = true;
                _shutdownSignal.Set();
            }

            if (_idleSignal.WaitOne(
                Math.Min(_processTimeoutMs + 2500, 7500)))
            {
                DisposeSignals();
                return;
            }

            lock (_stateLock)
            {
                if (_signalsDisposed || _signalCleanupScheduled) return;
                _signalCleanupScheduled = true;
            }
            Task.Run(delegate
            {
                try
                {
                    _idleSignal.WaitOne();
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                DisposeSignals();
            });
        }

        private void DisposeSignals()
        {
            lock (_stateLock)
            {
                if (_signalsDisposed || _activeOperations != 0) return;
                _signalsDisposed = true;
                _shutdownSignal.Dispose();
                _idleSignal.Dispose();
            }
        }

        private bool RunOnce(string serial)
        {
            if (!File.Exists(_scrcpyPath))
                throw new FileNotFoundException(
                    LocalizationService.Get(
                        "Error.Scrcpy.FileNotFound"),
                    _scrcpyPath);

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
                        if (_shutdownSignal.WaitOne(0)) return false;
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
                    if (started) StopAndDrain(process);
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
                try
                {
                    confirmation.Set();
                }
                catch (ObjectDisposedException)
                {
                    // A late asynchronous output callback can arrive on exit.
                }
            }
        }

        private void StopAndDrain(Process process)
        {
            Exception lastError = null;
            for (var attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    if (!process.HasExited) process.Kill();
                    if (process.WaitForExit(2000))
                    {
                        process.WaitForExit();
                        return;
                    }
                }
                catch (InvalidOperationException)
                {
                    process.WaitForExit();
                    return;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }
            }

            throw new TimeoutException(
                LocalizationService.Get("Error.Scrcpy.StopTimeout"),
                lastError);
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
