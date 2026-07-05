using System;
using System.Diagnostics;
using System.IO;
using System.Text;
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
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            if (writeLog)
                _logService.Info(LocalizationService.Format(
                    "Log.Process.Start",
                    fileName,
                    startInfo.Arguments));

            using (var process = new Process { StartInfo = startInfo })
            {
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data != null) output.AppendLine(e.Data);
                };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data != null) error.AppendLine(e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                var exited = process.WaitForExit(timeoutMs);
                if (!exited)
                {
                    try { process.Kill(); }
                    catch (Exception ex)
                    {
                        _logService.Warning(LocalizationService.Format(
                            "Log.Process.KillTimedOutFailed",
                            ex.Message));
                    }
                }
                else
                {
                    process.WaitForExit();
                }

                stopwatch.Stop();
                var result = new ProcessResult
                {
                    FileName = fileName,
                    Arguments = startInfo.Arguments,
                    ExitCode = exited ? process.ExitCode : -1,
                    StandardOutput = output.ToString().Trim(),
                    StandardError = error.ToString().Trim(),
                    TimedOut = !exited,
                    Duration = stopwatch.Elapsed
                };

                if (writeLog) LogResult(result);
                return result;
            }
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
