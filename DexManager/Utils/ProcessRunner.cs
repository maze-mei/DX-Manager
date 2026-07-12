using System;
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
        private readonly LogService _logService;

        public ProcessRunner(LogService logService)
        {
            _logService = logService;
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
                    if (e.Data != null)
                    {
                        lock (output) output.AppendLine(e.Data);
                    }
                };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data != null)
                    {
                        lock (error) error.AppendLine(e.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                var timedOut = !process.WaitForExit(timeoutMs);
                var terminated = !timedOut;
                if (timedOut)
                {
                    terminated = TryTerminateProcess(process);
                    if (!terminated)
                    {
                        ScheduleProcessReaper(process);
                        process = null;
                    }
                }
                else
                {
                    process.WaitForExit();
                }

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
            finally
            {
                if (process != null) process.Dispose();
            }
        }

        private bool TryTerminateProcess(Process process)
        {
            try
            {
                if (!process.HasExited) process.Kill();
                if (!process.WaitForExit(2000)) return false;
                process.WaitForExit();
                return true;
            }
            catch (InvalidOperationException)
            {
                try { process.WaitForExit(); }
                catch (InvalidOperationException) { }
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
