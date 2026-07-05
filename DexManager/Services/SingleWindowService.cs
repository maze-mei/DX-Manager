using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using DexManager.Models;

namespace DexManager.Services
{
    public sealed class SingleWindowService : IDisposable
    {
        private readonly object _syncRoot = new object();
        private readonly string _scrcpyPath;
        private readonly int _processTimeoutMs;
        private readonly AdbService _adbService;
        private readonly ScrcpyLaunchCoordinator _launchCoordinator;
        private readonly LogService _logService;
        private readonly Dictionary<int, Process> _processes =
            new Dictionary<int, Process>();
        private readonly Dictionary<int, bool> _stayAwakeRequests =
            new Dictionary<int, bool>();
        private readonly Dictionary<int, bool> _screenOffRequests =
            new Dictionary<int, bool>();

        public SingleWindowService(
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
            _processTimeoutMs = Math.Max(processTimeoutMs, 1000);
            _adbService = adbService ??
                throw new ArgumentNullException("adbService");
            _launchCoordinator = launchCoordinator ??
                throw new ArgumentNullException("launchCoordinator");
            _logService = logService ??
                throw new ArgumentNullException("logService");
        }

        public event EventHandler RunningChanged;

        public bool IsRunning(int slot)
        {
            lock (_syncRoot)
            {
                Process process;
                if (!_processes.TryGetValue(slot, out process)) return false;
                if (IsProcessRunning(process)) return true;
                _processes.Remove(slot);
                _stayAwakeRequests.Remove(slot);
                _screenOffRequests.Remove(slot);
                process.Dispose();
                return false;
            }
        }

        public int RunningCount
        {
            get
            {
                lock (_syncRoot)
                {
                    var count = 0;
                    foreach (var process in _processes.Values)
                    {
                        if (IsProcessRunning(process)) count++;
                    }
                    return count;
                }
            }
        }

        public bool IsStayAwakeRequested
        {
            get
            {
                lock (_syncRoot)
                {
                    foreach (var item in _processes)
                    {
                        bool requested;
                        if (IsProcessRunning(item.Value) &&
                            _stayAwakeRequests.TryGetValue(
                                item.Key,
                                out requested) &&
                            requested)
                        {
                            return true;
                        }
                    }
                    return false;
                }
            }
        }

        public bool IsScreenOffRequested
        {
            get
            {
                lock (_syncRoot)
                {
                    foreach (var item in _processes)
                    {
                        bool requested;
                        if (IsProcessRunning(item.Value) &&
                            _screenOffRequests.TryGetValue(
                                item.Key,
                                out requested) &&
                            requested)
                        {
                            return true;
                        }
                    }
                    return false;
                }
            }
        }

        public IntPtr MainWindowHandle(int slot)
        {
            lock (_syncRoot)
            {
                Process process;
                if (!_processes.TryGetValue(slot, out process))
                    return IntPtr.Zero;
                return GetMainWindowHandle(process);
            }
        }

        public IList<IntPtr> GetWindowHandles()
        {
            var handles = new List<IntPtr>();
            lock (_syncRoot)
            {
                foreach (var process in _processes.Values)
                {
                    var handle = GetMainWindowHandle(process);
                    if (handle != IntPtr.Zero) handles.Add(handle);
                }
            }
            return handles;
        }

        public bool ContainsWindowHandle(IntPtr handle)
        {
            if (handle == IntPtr.Zero) return false;
            foreach (var candidate in GetWindowHandles())
            {
                if (candidate == handle) return true;
            }
            return false;
        }

        public void Start(int slot, SingleWindowSlotSettings settings)
        {
            if (slot < 1 || slot > 3)
                throw new ArgumentOutOfRangeException("slot");
            if (settings == null)
                throw new ArgumentNullException("settings");
            if (!File.Exists(_scrcpyPath))
                throw new FileNotFoundException(
                    "scrcpy.exe를 찾을 수 없습니다.",
                    _scrcpyPath);
            if (string.IsNullOrWhiteSpace(settings.StartAppPackage))
                throw new InvalidOperationException(
                    "단일창에서 실행할 앱을 선택하세요.");

            var started = false;
            _launchCoordinator.RunExclusive(delegate
            {
                Process process;
                lock (_syncRoot)
                {
                    if (IsRunning(slot))
                    {
                        _logService.Warning(
                            "단일창 " + slot + "이 이미 실행 중입니다.");
                        return;
                    }

                    var arguments = BuildArguments(slot, settings);
                    process = CreateProcess(arguments);
                    process.EnableRaisingEvents = true;
                    process.Exited += Process_Exited;
                    process.OutputDataReceived += Process_OutputDataReceived;
                    process.ErrorDataReceived += Process_ErrorDataReceived;
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    _processes[slot] = process;
                    _stayAwakeRequests[slot] = settings.StayAwake;
                    _screenOffRequests[slot] = settings.TurnScreenOff;
                    started = true;
                    _logService.Info(
                        "단일창 " + slot + " Scrcpy 실행: " + arguments);
                }

                try
                {
                    WaitForMainWindow(process, slot);
                }
                catch
                {
                    AbortStart(process, slot);
                    throw;
                }
            });

            if (started) RaiseRunningChanged();
        }

        public void Restart(int slot, SingleWindowSlotSettings settings)
        {
            _launchCoordinator.RunExclusive(delegate
            {
                Stop(slot);
                Start(slot, settings);
            });
        }

        public void Stop(int slot)
        {
            var stopped = false;
            _launchCoordinator.RunExclusive(delegate
            {
                Process process;
                lock (_syncRoot)
                {
                    if (!_processes.TryGetValue(slot, out process)) return;
                    _processes.Remove(slot);
                    _stayAwakeRequests.Remove(slot);
                    _screenOffRequests.Remove(slot);
                }

                StopProcess(process);
                stopped = true;
                _logService.Info("단일창 " + slot + "을 중지했습니다.");
            });

            if (stopped) RaiseRunningChanged();
        }

        public void StopAll()
        {
            var stoppedCount = 0;
            _launchCoordinator.RunExclusive(delegate
            {
                List<KeyValuePair<int, Process>> processes;
                lock (_syncRoot)
                {
                    processes =
                        new List<KeyValuePair<int, Process>>(_processes);
                    _processes.Clear();
                    _stayAwakeRequests.Clear();
                    _screenOffRequests.Clear();
                }

                foreach (var item in processes)
                {
                    StopProcess(item.Value);
                    stoppedCount++;
                    _logService.Info(
                        "단일창 " + item.Key + "을 중지했습니다.");
                }
            });

            if (stoppedCount > 0) RaiseRunningChanged();
        }

        public void Dispose()
        {
            StopAll();
        }

        private string BuildArguments(
            int slot,
            SingleWindowSlotSettings settings)
        {
            var arguments = new List<string>();
            var serial = _adbService.TargetSerial;
            if (!string.IsNullOrWhiteSpace(serial))
            {
                arguments.Add("--serial");
                arguments.Add(Quote(serial));
            }
            arguments.Add(
                "--new-display=" +
                settings.Width.ToString(CultureInfo.InvariantCulture) +
                "x" +
                settings.Height.ToString(CultureInfo.InvariantCulture) +
                "/" +
                settings.Dpi.ToString(CultureInfo.InvariantCulture));
            arguments.Add("--start-app=" + GetStartAppArgument(settings));
            arguments.Add("--window-title");
            arguments.Add(Quote(GetWindowTitle(slot, settings)));

            if (!string.IsNullOrWhiteSpace(settings.BitRate))
            {
                arguments.Add("-b");
                arguments.Add(settings.BitRate.Trim());
            }
            if (settings.MaxFps > 0)
            {
                arguments.Add("--max-fps");
                arguments.Add(
                    settings.MaxFps.ToString(CultureInfo.InvariantCulture));
            }
            if (settings.UseHidKeyboard) arguments.Add("-K");
            if (settings.UseHidMouse) arguments.Add("-M");
            if (settings.StayAwake) arguments.Add("--keep-active");
            if (settings.FlexDisplay) arguments.Add("--flex-display");
            if (settings.TurnScreenOff)
            {
                arguments.Add("-S");
                arguments.Add("--no-power-on");
            }
            if (!string.IsNullOrWhiteSpace(settings.AdditionalArguments))
                arguments.Add(settings.AdditionalArguments.Trim());

            return string.Join(" ", arguments);
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
                    WindowStyle = ProcessWindowStyle.Minimized,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                }
            };
        }

        private void Process_Exited(object sender, EventArgs e)
        {
            var process = sender as Process;
            var slot = 0;
            lock (_syncRoot)
            {
                foreach (var item in _processes)
                {
                    if (!ReferenceEquals(item.Value, process)) continue;
                    slot = item.Key;
                    break;
                }
                if (slot > 0)
                {
                    _processes.Remove(slot);
                    _stayAwakeRequests.Remove(slot);
                    _screenOffRequests.Remove(slot);
                }
            }

            if (slot > 0)
            {
                _logService.Info(
                    "단일창 " + slot + " Scrcpy 프로세스가 종료되었습니다.");
                RaiseRunningChanged();
            }
        }

        private void Process_OutputDataReceived(
            object sender,
            DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                _logService.Info("[single scrcpy] " + e.Data);
        }

        private void Process_ErrorDataReceived(
            object sender,
            DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                _logService.Warning("[single scrcpy] " + e.Data);
        }

        private void RaiseRunningChanged()
        {
            var handler = RunningChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private static string GetStartAppArgument(
            SingleWindowSlotSettings settings)
        {
            var packageName = settings.StartAppPackage.Trim();
            return settings.ForceStopStartApp &&
                !packageName.StartsWith("+", StringComparison.Ordinal)
                ? "+" + packageName
                : packageName;
        }

        private static string GetWindowTitle(
            int slot,
            SingleWindowSlotSettings settings)
        {
            var appName = string.IsNullOrWhiteSpace(settings.StartAppName)
                ? settings.StartAppPackage.Trim()
                : settings.StartAppName.Trim();
            if (string.IsNullOrWhiteSpace(appName))
                appName = "Single Window " + slot;
            return "DX Manager - " + appName;
        }

        private static void StopProcess(Process process)
        {
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
            }
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

        private void WaitForMainWindow(Process process, int slot)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.ElapsedMilliseconds < _processTimeoutMs)
            {
                if (!IsProcessRunning(process))
                    throw new InvalidOperationException(
                        "단일창 " + slot +
                        " Scrcpy가 창을 준비하기 전에 종료되었습니다.");

                process.Refresh();
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    _logService.Info(
                        "단일창 " + slot +
                        " Scrcpy 창 준비 완료: PID=" + process.Id);
                    return;
                }
                Thread.Sleep(50);
            }

            throw new TimeoutException(
                "단일창 " + slot + " Scrcpy 창 준비 시간이 초과되었습니다.");
        }

        private void AbortStart(Process process, int slot)
        {
            lock (_syncRoot)
            {
                Process current;
                if (_processes.TryGetValue(slot, out current) &&
                    ReferenceEquals(current, process))
                {
                    _processes.Remove(slot);
                    _stayAwakeRequests.Remove(slot);
                    _screenOffRequests.Remove(slot);
                }
            }

            StopProcess(process);
        }

        private static IntPtr GetMainWindowHandle(Process process)
        {
            if (!IsProcessRunning(process)) return IntPtr.Zero;
            try
            {
                process.Refresh();
                return process.MainWindowHandle;
            }
            catch (InvalidOperationException)
            {
                return IntPtr.Zero;
            }
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }
    }
}
