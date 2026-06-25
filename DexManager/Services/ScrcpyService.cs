using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using DexManager.Models;
using DexManager.Utils;

namespace DexManager.Services
{
    public sealed class ScrcpyService : IDisposable
    {
        private readonly object _syncRoot = new object();
        private readonly string _scrcpyPath;
        private readonly int _processTimeoutMs;
        private readonly ProcessRunner _processRunner;
        private readonly LogService _logService;
        private Process _process;

        public ScrcpyService(
            string scrcpyPath,
            int processTimeoutMs,
            ProcessRunner processRunner,
            LogService logService)
        {
            if (string.IsNullOrWhiteSpace(scrcpyPath))
                throw new ArgumentException("Scrcpy 경로가 비어 있습니다.", "scrcpyPath");

            _scrcpyPath = Path.GetFullPath(scrcpyPath);
            _processTimeoutMs = Math.Max(processTimeoutMs, 1000);
            _processRunner = processRunner ??
                throw new ArgumentNullException("processRunner");
            _logService = logService;
        }

        public event EventHandler RunningChanged;

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

        public string ScrcpyPath
        {
            get { return _scrcpyPath; }
        }

        public int CurrentProcessId
        {
            get
            {
                lock (_syncRoot)
                {
                    return IsProcessRunning(_process) ? _process.Id : 0;
                }
            }
        }

        public IntPtr MainWindowHandle
        {
            get
            {
                lock (_syncRoot)
                {
                    if (!IsProcessRunning(_process))
                    {
                        _process = null;
                        return IntPtr.Zero;
                    }

                    for (var attempt = 0; attempt < 5; attempt++)
                    {
                        try
                        {
                            _process.Refresh();
                            if (!IsProcessRunning(_process))
                            {
                                _process = null;
                                return IntPtr.Zero;
                            }

                            var handle = _process.MainWindowHandle;
                            if (handle != IntPtr.Zero) return handle;
                        }
                        catch (InvalidOperationException)
                        {
                            _process = null;
                            return IntPtr.Zero;
                        }

                        Thread.Sleep(50);
                    }

                    return IntPtr.Zero;
                }
            }
        }

        public string BuildArguments(ScrcpySettings settings, int displayId)
        {
            if (settings == null) throw new ArgumentNullException("settings");

            var arguments = new List<string>
            {
                "--display-id", displayId.ToString(CultureInfo.InvariantCulture)
            };

            if (!string.IsNullOrWhiteSpace(settings.BitRate))
            {
                arguments.Add("-b");
                arguments.Add(settings.BitRate.Trim());
            }

            if (settings.MaxFps > 0)
            {
                arguments.Add("--max-fps");
                arguments.Add(settings.MaxFps.ToString(CultureInfo.InvariantCulture));
            }

            if (!string.IsNullOrWhiteSpace(settings.WindowTitle))
            {
                arguments.Add("--window-title");
                arguments.Add(Quote(settings.WindowTitle));
            }

            if (settings.UseHidKeyboard) arguments.Add("-K");
            if (settings.UseHidMouse) arguments.Add("-M");
            if (settings.TurnScreenOff) arguments.Add("-S");
            if (settings.StayAwake) arguments.Add("-w");

            if (!string.IsNullOrWhiteSpace(settings.StartAppPackage))
            {
                var packageName = settings.StartAppPackage.Trim();
                if (settings.ForceStopStartApp && !packageName.StartsWith("+"))
                    packageName = "+" + packageName;
                arguments.Add("--start-app=" + packageName);
            }

            if (!string.IsNullOrWhiteSpace(settings.AdditionalArguments))
            {
                arguments.Add(settings.AdditionalArguments.Trim());
            }

            return string.Join(" ", arguments);
        }

        public IList<ScrcpyAppInfo> ListApps()
        {
            if (!File.Exists(_scrcpyPath))
                throw new FileNotFoundException("scrcpy.exe를 찾을 수 없습니다.", _scrcpyPath);

            var result = _processRunner.Run(
                _scrcpyPath,
                "--list-apps",
                Path.GetDirectoryName(_scrcpyPath),
                _processTimeoutMs);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException(
                    "Scrcpy 앱 목록을 불러오지 못했습니다: " +
                    result.StandardError);
            }

            var output = (result.StandardOutput ?? string.Empty) + "\n" +
                (result.StandardError ?? string.Empty);
            var apps = ParseAppList(output);
            if (apps.Count == 0)
                throw new InvalidOperationException(
                    "Scrcpy 앱 목록 결과를 해석하지 못했습니다.");

            _logService.Info("Scrcpy 앱 목록을 불러왔습니다: " + apps.Count + "개");
            return apps;
        }

        public void Start(ScrcpySettings settings, int displayId)
        {
            lock (_syncRoot)
            {
                if (IsProcessRunning(_process))
                {
                    _logService.Warning("Scrcpy가 이미 실행 중이므로 중복 실행을 건너뜁니다.");
                    return;
                }

                if (!File.Exists(_scrcpyPath))
                    throw new FileNotFoundException("scrcpy.exe를 찾을 수 없습니다.", _scrcpyPath);

                var arguments = BuildArguments(settings, displayId);
                var process = CreateProcess(arguments, true);
                process.EnableRaisingEvents = true;
                process.Exited += Process_Exited;
                process.OutputDataReceived += Process_OutputDataReceived;
                process.ErrorDataReceived += Process_ErrorDataReceived;
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                _process = process;

                _logService.Info("Scrcpy 실행: " + arguments);
            }

            RaiseRunningChanged();
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
                    process.CloseMainWindow();
                    if (!process.WaitForExit(2000)) process.Kill();
                }
            }
            finally
            {
                process.Dispose();
                _logService.Info("Scrcpy를 중지했습니다.");
                RaiseRunningChanged();
            }
        }

        public bool RunWakeUp(int delayMs)
        {
            if (!File.Exists(_scrcpyPath))
            {
                _logService.Warning("Scrcpy Wake-up 파일이 없습니다: " + _scrcpyPath);
                return false;
            }

            _logService.Info("Scrcpy Wake-up을 실행합니다.");
            using (var process = CreateProcess(
                "--no-audio --max-size=64 --max-fps=1",
                false))
            {
                try
                {
                    process.Start();
                    Thread.Sleep(Math.Max(delayMs, 500));
                    if (!process.HasExited) process.Kill();
                    process.WaitForExit(2000);
                    return true;
                }
                catch (Exception ex)
                {
                    _logService.Error("Scrcpy Wake-up 실행에 실패했습니다.", ex);
                    return false;
                }
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private Process CreateProcess(string arguments, bool redirectOutput)
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
                    WindowStyle = ProcessWindowStyle.Minimized,
                    RedirectStandardOutput = redirectOutput,
                    RedirectStandardError = redirectOutput
                }
            };
        }

        private void Process_Exited(object sender, EventArgs e)
        {
            lock (_syncRoot)
            {
                if (ReferenceEquals(_process, sender)) _process = null;
            }

            _logService.Info("Scrcpy 프로세스가 종료되었습니다.");
            RaiseRunningChanged();
        }

        private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                _logService.Info("[scrcpy] " + e.Data);
        }

        private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                _logService.Warning("[scrcpy] " + e.Data);
        }

        private void RaiseRunningChanged()
        {
            var handler = RunningChanged;
            if (handler != null) handler(this, EventArgs.Empty);
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

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static IList<ScrcpyAppInfo> ParseAppList(string output)
        {
            var apps = new List<ScrcpyAppInfo>();
            var lines = (output ?? string.Empty).Split(
                new[] { "\r\n", "\n" },
                StringSplitOptions.None);

            foreach (var line in lines)
            {
                var match = Regex.Match(
                    line,
                    @"^\s*([*\-])\s+(.+?)\s{2,}([A-Za-z0-9._]+)\s*$");
                if (!match.Success) continue;

                apps.Add(new ScrcpyAppInfo
                {
                    IsSystemApp = match.Groups[1].Value == "*",
                    Name = match.Groups[2].Value.Trim(),
                    PackageName = match.Groups[3].Value.Trim()
                });
            }

            return apps
                .GroupBy(app => app.PackageName, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
    }
}
