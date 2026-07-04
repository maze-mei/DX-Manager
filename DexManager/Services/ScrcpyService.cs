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
        private readonly AdbService _adbService;
        private readonly ScrcpyLaunchCoordinator _launchCoordinator;
        private readonly LogService _logService;
        private Process _process;
        private bool _stayAwakeRequested;
        private bool _turnScreenOffRequested;

        public ScrcpyService(
            string scrcpyPath,
            int processTimeoutMs,
            ProcessRunner processRunner,
            AdbService adbService,
            ScrcpyLaunchCoordinator launchCoordinator,
            LogService logService)
        {
            if (string.IsNullOrWhiteSpace(scrcpyPath))
                throw new ArgumentException("Scrcpy 경로가 비어 있습니다.", "scrcpyPath");

            _scrcpyPath = Path.GetFullPath(scrcpyPath);
            _processTimeoutMs = Math.Max(processTimeoutMs, 1000);
            _processRunner = processRunner ??
                throw new ArgumentNullException("processRunner");
            _adbService = adbService ??
                throw new ArgumentNullException("adbService");
            _launchCoordinator = launchCoordinator ??
                throw new ArgumentNullException("launchCoordinator");
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

        public bool IsStayAwakeRequested
        {
            get
            {
                lock (_syncRoot)
                {
                    return IsProcessRunning(_process) &&
                        _stayAwakeRequested;
                }
            }
        }

        public bool IsScreenOffRequested
        {
            get
            {
                lock (_syncRoot)
                {
                    return IsProcessRunning(_process) &&
                        _turnScreenOffRequested;
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
            };
            AddSerialArgument(arguments);
            arguments.Add("--display-id");
            arguments.Add(displayId.ToString(CultureInfo.InvariantCulture));

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

            arguments.Add("--window-title");
            arguments.Add(Quote("DX Manager - DeX Station"));

            if (settings.UseHidKeyboard) arguments.Add("-K");
            if (settings.UseHidMouse) arguments.Add("-M");
            if (settings.StayAwake) arguments.Add("--keep-active");
            if (settings.TurnScreenOff)
            {
                arguments.Add("-S");
                arguments.Add("--no-power-on");
            }

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

            var arguments = new List<string>();
            AddSerialArgument(arguments);
            arguments.Add("--list-apps");
            var result = _launchCoordinator.RunExclusive(delegate
            {
                return _processRunner.Run(
                    _scrcpyPath,
                    string.Join(" ", arguments),
                    Path.GetDirectoryName(_scrcpyPath),
                    _processTimeoutMs);
            });
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
            var started = false;
            _launchCoordinator.RunExclusive(delegate
            {
                Process process;
                lock (_syncRoot)
                {
                    if (IsProcessRunning(_process))
                    {
                        _logService.Warning(
                            "Scrcpy가 이미 실행 중이므로 중복 실행을 건너뜁니다.");
                        return;
                    }

                    if (!File.Exists(_scrcpyPath))
                        throw new FileNotFoundException(
                            "scrcpy.exe를 찾을 수 없습니다.",
                            _scrcpyPath);

                    var arguments = BuildArguments(settings, displayId);
                    process = CreateProcess(arguments, true);
                    process.EnableRaisingEvents = true;
                    process.Exited += Process_Exited;
                    process.OutputDataReceived += Process_OutputDataReceived;
                    process.ErrorDataReceived += Process_ErrorDataReceived;
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    _process = process;
                    _stayAwakeRequested = settings.StayAwake;
                    _turnScreenOffRequested = settings.TurnScreenOff;
                    started = true;

                    _logService.Info("Scrcpy 실행: " + arguments);
                }

                try
                {
                    WaitForMainWindow(process);
                }
                catch
                {
                    AbortStart(process);
                    throw;
                }
            });

            if (started) RaiseRunningChanged();
        }

        public void Stop()
        {
            var stopped = false;
            _launchCoordinator.RunExclusive(delegate
            {
                Process process;
                lock (_syncRoot)
                {
                    process = _process;
                    _process = null;
                    _stayAwakeRequested = false;
                    _turnScreenOffRequested = false;
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
                    stopped = true;
                    _logService.Info("Scrcpy를 중지했습니다.");
                }
            });

            if (stopped) RaiseRunningChanged();
        }

        public bool RunWakeUp(int delayMs)
        {
            return _launchCoordinator.RunExclusive(delegate
            {
                return RunWakeUpCore(delayMs);
            });
        }

        private bool RunWakeUpCore(int delayMs)
        {
            if (!File.Exists(_scrcpyPath))
            {
                _logService.Warning("Scrcpy Wake-up 파일이 없습니다: " + _scrcpyPath);
                return false;
            }

            _logService.Info("Scrcpy Wake-up을 실행합니다.");
            var arguments = new List<string>();
            AddSerialArgument(arguments);
            arguments.Add("--no-audio");
            arguments.Add("--max-size=64");
            arguments.Add("--max-fps=1");
            using (var process = CreateProcess(
                string.Join(" ", arguments),
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
            var ownedProcess = false;
            lock (_syncRoot)
            {
                if (ReferenceEquals(_process, sender))
                {
                    _process = null;
                    _stayAwakeRequested = false;
                    _turnScreenOffRequested = false;
                    ownedProcess = true;
                }
            }

            if (!ownedProcess) return;
            _logService.Info("Scrcpy 프로세스가 종료되었습니다.");
            RaiseRunningChanged();
        }

        private void WaitForMainWindow(Process process)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.ElapsedMilliseconds < _processTimeoutMs)
            {
                if (!IsProcessRunning(process))
                    throw new InvalidOperationException(
                        "Scrcpy가 창을 준비하기 전에 종료되었습니다.");

                process.Refresh();
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    _logService.Info(
                        "Scrcpy 창 준비 완료: PID=" + process.Id);
                    return;
                }
                Thread.Sleep(50);
            }

            throw new TimeoutException(
                "Scrcpy 창 준비 시간이 초과되었습니다.");
        }

        private void AbortStart(Process process)
        {
            lock (_syncRoot)
            {
                if (ReferenceEquals(_process, process))
                {
                    _process = null;
                    _stayAwakeRequested = false;
                    _turnScreenOffRequested = false;
                }
            }

            try
            {
                if (IsProcessRunning(process))
                {
                    process.Kill();
                    process.WaitForExit(2000);
                }
            }
            finally
            {
                process.Dispose();
            }
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

        private void AddSerialArgument(ICollection<string> arguments)
        {
            var serial = _adbService.TargetSerial;
            if (string.IsNullOrWhiteSpace(serial)) return;
            arguments.Add("--serial");
            arguments.Add(Quote(serial));
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
