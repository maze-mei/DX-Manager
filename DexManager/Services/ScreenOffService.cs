using System;
using System.Diagnostics;
using System.IO;

namespace DexManager.Services
{
    public sealed class ScreenOffService : IDisposable
    {
        private readonly object _syncRoot = new object();
        private readonly string _scrcpyPath;
        private readonly LogService _logService;
        private Process _process;

        public ScreenOffService(string scrcpyPath, LogService logService)
        {
            if (string.IsNullOrWhiteSpace(scrcpyPath))
                throw new ArgumentException(
                    "Scrcpy 경로가 비어 있습니다.",
                    "scrcpyPath");

            _scrcpyPath = Path.GetFullPath(scrcpyPath);
            _logService = logService ??
                throw new ArgumentNullException("logService");
        }

        public bool IsRunning
        {
            get
            {
                lock (_syncRoot)
                {
                    return IsProcessRunning(_process);
                }
            }
        }

        public void Start()
        {
            lock (_syncRoot)
            {
                if (IsProcessRunning(_process)) return;
                if (_process != null)
                {
                    _process.Dispose();
                    _process = null;
                }
                if (!File.Exists(_scrcpyPath))
                    throw new FileNotFoundException(
                        "scrcpy.exe를 찾을 수 없습니다.",
                        _scrcpyPath);

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _scrcpyPath,
                        Arguments =
                            "--no-video --no-audio --no-window " +
                            "-S --no-power-on",
                        WorkingDirectory = Path.GetDirectoryName(_scrcpyPath),
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                process.EnableRaisingEvents = true;
                process.Exited += Process_Exited;
                process.OutputDataReceived += Process_OutputDataReceived;
                process.ErrorDataReceived += Process_ErrorDataReceived;
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                _process = process;
                _logService.Info(
                    "휴대폰 화면 끄기 보조 Scrcpy 세션을 시작했습니다.");
            }
        }

        public void Stop()
        {
            Process process;
            lock (_syncRoot)
            {
                process = _process;
                _process = null;
            }
            if (process == null) return;

            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(2000);
                }
            }
            finally
            {
                process.Dispose();
                _logService.Info(
                    "휴대폰 화면 끄기 보조 Scrcpy 세션을 종료했습니다.");
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private void Process_Exited(object sender, EventArgs e)
        {
            var process = sender as Process;
            lock (_syncRoot)
            {
                if (!ReferenceEquals(_process, sender)) return;
                _process = null;
            }
            if (process != null) process.Dispose();
            _logService.Info(
                "휴대폰 화면 끄기 보조 Scrcpy 세션이 종료되었습니다.");
        }

        private void Process_OutputDataReceived(
            object sender,
            DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                _logService.Info("[screen-off scrcpy] " + e.Data);
        }

        private void Process_ErrorDataReceived(
            object sender,
            DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                _logService.Warning("[screen-off scrcpy] " + e.Data);
        }

        private static bool IsProcessRunning(Process process)
        {
            if (process == null) return false;
            try
            {
                return !process.HasExited;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
}
