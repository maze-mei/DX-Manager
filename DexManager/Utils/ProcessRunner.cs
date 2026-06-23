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
                throw new ArgumentException("실행 파일 경로가 비어 있습니다.", "fileName");
            if (!File.Exists(fileName))
                throw new FileNotFoundException("실행 파일을 찾을 수 없습니다.", fileName);

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
                _logService.Info("프로세스 실행: " + fileName + " " + startInfo.Arguments);

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
                        _logService.Warning("시간 초과 프로세스를 종료하지 못했습니다: " + ex.Message);
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
            var summary = string.Format(
                "프로세스 종료: ExitCode={0}, Timeout={1}, Duration={2}ms",
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
