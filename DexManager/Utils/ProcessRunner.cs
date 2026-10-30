using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DexManager.Models;
using DexManager.Services;

namespace DexManager.Utils
{
    public sealed class ProcessRunner
    {
        private const int OutputDrainTimeoutMs = 2000;
        private const int PostKillWaitTimeoutMs = 2000;
        private readonly LogService _logService;
        private readonly object _lifecycleSync = new object();
        private readonly HashSet<Process> _activeProcesses =
            new HashSet<Process>();
        private int _shutdownRequested;

        public ProcessRunner(LogService logService)
        {
            _logService = logService;
        }

        public bool IsShutdownRequested
        {
            get
            {
                return Interlocked.CompareExchange(
                    ref _shutdownRequested,
                    0,
                    0) != 0;
            }
        }

        public void BeginShutdown()
        {
            Process[] active;
            lock (_lifecycleSync)
            {
                if (Interlocked.Exchange(ref _shutdownRequested, 1) != 0)
                    return;
                active = new Process[_activeProcesses.Count];
                _activeProcesses.CopyTo(active);
            }

            _logService.Info(LocalizationService.Get(
                "Log.Process.ShutdownRequested"));
            foreach (var process in active)
                TryTerminateForShutdown(process);
        }

        public void BlockNewProcessesForWindowsShutdown()
        {
            lock (_lifecycleSync)
            {
                if (Interlocked.Exchange(ref _shutdownRequested, 1) != 0)
                    return;
            }
            _logService.Info(LocalizationService.Get(
                "Log.Process.ShutdownRequested"));
        }

        internal void StartPersistentProcess(Process process)
        {
            lock (_lifecycleSync)
            {
                if (IsShutdownRequested) throw new OperationCanceledException();
                process.Start();
                _activeProcesses.Add(process);
            }
        }

        internal void ForgetPersistentProcess(Process process)
        {
            lock (_lifecycleSync) _activeProcesses.Remove(process);
        }

        public ProcessResult Run(
            string fileName,
            string arguments,
            string workingDirectory,
            int timeoutMs)
        {
            return Run(fileName, arguments, workingDirectory, timeoutMs, true);
        }

        public ProcessResult Run(
            string fileName,
            string arguments,
            string workingDirectory,
            int timeoutMs,
            bool writeLog)
        {
            return Run(
                fileName,
                arguments,
                workingDirectory,
                timeoutMs,
                writeLog,
                Encoding.UTF8);
        }

        public ProcessResult Run(
            string fileName,
            string arguments,
            string workingDirectory,
            int timeoutMs,
            bool writeLog,
            Encoding outputEncoding)
        {
            if (IsShutdownRequested)
                return CreateCanceledResult(fileName, arguments);
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException(
                    LocalizationService.Get("Error.Process.PathEmpty"),
                    "fileName");
            if (!File.Exists(fileName))
                throw new FileNotFoundException(
                    LocalizationService.Get("Error.Process.FileNotFound"),
                    fileName);

            var stopwatch = Stopwatch.StartNew();
            var output = new StringBuilder();
            var error = new StringBuilder();
            var outputDrained = new TaskCompletionSource<bool>();
            var errorDrained = new TaskCompletionSource<bool>();
            var encoding = outputEncoding ?? Encoding.UTF8;

            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments ?? string.Empty,
                WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                    ? Path.GetDirectoryName(fileName)
                    : workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = encoding,
                StandardErrorEncoding = encoding
            };

            if (writeLog)
                _logService.Info(LocalizationService.Format(
                    "Log.Process.Start",
                    fileName,
                    startInfo.Arguments));

            var process = new Process { StartInfo = startInfo };
            try
            {
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data == null)
                        outputDrained.TrySetResult(true);
                    else
                        lock (output) output.AppendLine(e.Data);
                };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data == null)
                        errorDrained.TrySetResult(true);
                    else
                        lock (error) error.AppendLine(e.Data);
                };

                lock (_lifecycleSync)
                {
                    if (IsShutdownRequested)
                        return CreateCanceledResult(fileName, arguments);
                    process.Start();
                    _activeProcesses.Add(process);
                }
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                var timedOut = !process.WaitForExit(timeoutMs);
                var terminated = !timedOut;
                if (timedOut)
                {
                    terminated = TryTerminateProcess(process);
                    if (!terminated)
                    {
                        UntrackProcess(process);
                        ScheduleProcessReaper(process);
                        process = null;
                    }
                }

                // A descendant may inherit stdout/stderr after the requested
                // process exits. Wait for both readers only for a bounded time.
                Task.WaitAll(
                    new[] { outputDrained.Task, errorDrained.Task },
                    OutputDrainTimeoutMs);

                if (IsShutdownRequested)
                    return CreateCanceledResult(fileName, arguments);

                stopwatch.Stop();
                string standardOutput;
                string standardError;
                lock (output) standardOutput = output.ToString().Trim();
                lock (error) standardError = error.ToString().Trim();
                var result = new ProcessResult
                {
                    FileName = fileName,
                    Arguments = startInfo.Arguments,
                    ExitCode = !timedOut && terminated
                        ? process.ExitCode
                        : -1,
                    StandardOutput = standardOutput,
                    StandardError = standardError,
                    TimedOut = timedOut,
                    Duration = stopwatch.Elapsed
                };

                if (writeLog) LogResult(result);
                return result;
            }
            catch
            {
                if (IsShutdownRequested)
                    return CreateCanceledResult(fileName, arguments);
                throw;
            }
            finally
            {
                if (process != null)
                {
                    UntrackProcess(process);
                    process.Dispose();
                }
            }
        }

        private void UntrackProcess(Process process)
        {
            if (process == null) return;
            lock (_lifecycleSync) _activeProcesses.Remove(process);
        }

        private static ProcessResult CreateCanceledResult(
            string fileName,
            string arguments)
        {
            return new ProcessResult
            {
                FileName = fileName ?? string.Empty,
                Arguments = arguments ?? string.Empty,
                ExitCode = -1,
                StandardOutput = string.Empty,
                StandardError = string.Empty,
                TimedOut = false,
                Canceled = true,
                Duration = TimeSpan.Zero
            };
        }

        private static void TryTerminateForShutdown(Process process)
        {
            if (process == null) return;
            try
            {
                if (!process.HasExited) process.Kill();
            }
            catch
            {
                // Windows is already ending the process tree. Do not surface
                // a second failure while the desktop session is shutting down.
            }
        }

        private bool TryTerminateProcess(Process process)
        {
            try
            {
                if (!process.HasExited) process.Kill();
                if (!process.WaitForExit(PostKillWaitTimeoutMs))
                    return false;
                return true;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
            catch (Exception ex)
            {
                _logService.Warning(LocalizationService.Format(
                    "Log.Process.KillTimedOutFailed",
                    ex.Message));
                return false;
            }
        }

        private void ScheduleProcessReaper(Process process)
        {
            _logService.Warning(LocalizationService.Get(
                "Log.Process.TerminationDeferred"));
            Task.Run(delegate
            {
                var terminated = false;
                try
                {
                    for (var attempt = 0; attempt < 5 && !terminated; attempt++)
                    {
                        if (attempt > 0) Thread.Sleep(500);
                        terminated = TryTerminateProcess(process);
                    }
                    if (!terminated)
                    {
                        _logService.Error(
                            LocalizationService.Get(
                                "Log.Process.TerminationFailed"),
                            new TimeoutException());
                    }
                }
                finally
                {
                    process.Dispose();
                }
            });
        }

        public Task<ProcessResult> RunAsync(
            string fileName,
            string arguments,
            string workingDirectory,
            int timeoutMs)
        {
            return Task.Run(delegate
            {
                return Run(fileName, arguments, workingDirectory, timeoutMs);
            });
        }

        private void LogResult(ProcessResult result)
        {
            var summary = LocalizationService.Format(
                "Log.Process.End",
                result.ExitCode,
                result.TimedOut,
                (long)result.Duration.TotalMilliseconds);

            if (result.IsSuccess)
                _logService.Info(summary);
            else
                _logService.Warning(summary + " Error=" + result.StandardError);
        }
    }
}
