using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using DexManager.Models;
using DexManager.Utils;

namespace DexManager.Services
{
    public sealed class SingleWindowService : IDisposable
    {
        private const int WindowMonitorIntervalMs = 100;
        private const int GracefulStopWaitMs = 500;
        private const int ForcedStopWaitMs = 1000;
        private static readonly Regex NewDisplayIdRegex = new Regex(
            @"New display:.*\(id=(\d+)\)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private readonly object _syncRoot = new object();
        private readonly string _scrcpyPath;
        private readonly int _processTimeoutMs;
        private readonly AdbService _adbService;
        private readonly ScrcpyLaunchCoordinator _launchCoordinator;
        private readonly LogService _logService;
        private readonly ScrcpyRuntimeInfo _runtimeInfo;
        private bool _unsupportedFlexDisplayLogged;
        private readonly Dictionary<int, Process> _processes =
            new Dictionary<int, Process>();
        private readonly Dictionary<int, bool> _stayAwakeRequests =
            new Dictionary<int, bool>();
        private readonly Dictionary<int, bool> _screenOffRequests =
            new Dictionary<int, bool>();
        private readonly Dictionary<int, string> _targetSerials =
            new Dictionary<int, string>();
        private readonly Dictionary<int, int> _displayIds =
            new Dictionary<int, int>();
        private readonly Dictionary<int, IntPtr> _windowHandles =
            new Dictionary<int, IntPtr>();
        private readonly HashSet<int> _stoppingSlots = new HashSet<int>();
        private int _shutdownRequested;
        private int _disposed;

        public SingleWindowService(
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
            _processTimeoutMs = Math.Max(processTimeoutMs, 1000);
            _adbService = adbService ??
                throw new ArgumentNullException("adbService");
            _launchCoordinator = launchCoordinator ??
                throw new ArgumentNullException("launchCoordinator");
            _runtimeInfo = runtimeInfo ??
                throw new ArgumentNullException("runtimeInfo");
            _logService = logService ??
                throw new ArgumentNullException("logService");
        }

        public event EventHandler RunningChanged;

        public void RequestShutdown()
        {
            Interlocked.Exchange(ref _shutdownRequested, 1);
        }

        public bool IsRunning(int slot)
        {
            lock (_syncRoot)
            {
                Process process;
                if (!_processes.TryGetValue(slot, out process)) return false;
                return IsProcessRunning(process);
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

        public IList<string> GetRunningSerials()
        {
            var serials = new List<string>();
            lock (_syncRoot)
            {
                foreach (var item in _processes)
                {
                    string serial;
                    if (!IsProcessRunning(item.Value) ||
                        !_targetSerials.TryGetValue(item.Key, out serial) ||
                        string.IsNullOrWhiteSpace(serial) ||
                        serials.Contains(serial))
                    {
                        continue;
                    }
                    serials.Add(serial);
                }
            }
            return serials;
        }

        public IList<string> GetStayAwakeSerials()
        {
            return GetRequestedSerials(_stayAwakeRequests);
        }

        public IList<string> GetScreenOffSerials()
        {
            return GetRequestedSerials(_screenOffRequests);
        }

        private IList<string> GetRequestedSerials(
            IDictionary<int, bool> requests)
        {
            var serials = new List<string>();
            lock (_syncRoot)
            {
                foreach (var item in _processes)
                {
                    bool requested;
                    string serial;
                    if (!IsProcessRunning(item.Value) ||
                        !requests.TryGetValue(item.Key, out requested) ||
                        !requested ||
                        !_targetSerials.TryGetValue(item.Key, out serial) ||
                        string.IsNullOrWhiteSpace(serial) ||
                        serials.Contains(serial))
                    {
                        continue;
                    }
                    serials.Add(serial);
                }
            }
            return serials;
        }

        public IntPtr MainWindowHandle(int slot)
        {
            lock (_syncRoot)
            {
                IntPtr handle;
                return _windowHandles.TryGetValue(slot, out handle)
                    ? handle
                    : IntPtr.Zero;
            }
        }

        public IList<IntPtr> GetWindowHandles()
        {
            var handles = new List<IntPtr>();
            lock (_syncRoot)
            {
                foreach (var handle in _windowHandles.Values)
                {
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

        public bool TryGetAdbTargetForWindow(
            IntPtr windowHandle,
            out string serial,
            out int displayId)
        {
            serial = null;
            displayId = 0;
            if (windowHandle == IntPtr.Zero) return false;

            lock (_syncRoot)
            {
                foreach (var item in _processes)
                {
                    IntPtr handle;
                    if (!_windowHandles.TryGetValue(item.Key, out handle) ||
                        handle != windowHandle)
                    {
                        continue;
                    }

                    string targetSerial;
                    int targetDisplayId;
                    if (!_targetSerials.TryGetValue(
                            item.Key,
                            out targetSerial) ||
                        string.IsNullOrWhiteSpace(targetSerial) ||
                        !_displayIds.TryGetValue(
                            item.Key,
                            out targetDisplayId) ||
                        targetDisplayId <= 0)
                    {
                        return false;
                    }

                    serial = targetSerial;
                    displayId = targetDisplayId;
                    return true;
                }
            }
            return false;
        }

        public bool TryGetSerialForWindow(
            IntPtr windowHandle,
            out string serial)
        {
            serial = null;
            if (windowHandle == IntPtr.Zero) return false;
            lock (_syncRoot)
            {
                foreach (var item in _processes)
                {
                    string targetSerial;
                    IntPtr handle;
                    if (_windowHandles.TryGetValue(item.Key, out handle) &&
                        handle == windowHandle &&
                        _targetSerials.TryGetValue(
                            item.Key,
                            out targetSerial) &&
                        !string.IsNullOrWhiteSpace(targetSerial))
                    {
                        serial = targetSerial;
                        return true;
                    }
                }
            }
            return false;
        }

        public void Start(int slot, SingleWindowSlotSettings settings)
        {
            Start(slot, settings, _adbService.TargetSerial);
        }

        public void Start(
            int slot,
            SingleWindowSlotSettings settings,
            string requestedSerial)
        {
            if (slot < 1 || slot > 3)
                throw new ArgumentOutOfRangeException("slot");
            if (settings == null)
                throw new ArgumentNullException("settings");
            if (!File.Exists(_scrcpyPath))
                throw new FileNotFoundException(
                    LocalizationService.Get(
                        "Error.Scrcpy.FileNotFound"),
                    _scrcpyPath);
            if (string.IsNullOrWhiteSpace(settings.StartAppPackage))
                throw new InvalidOperationException(
                    LocalizationService.Get(
                        "Error.SingleWindow.AppRequired"));

            var started = false;
            _launchCoordinator.RunExclusive(delegate
            {
                if (Interlocked.CompareExchange(
                    ref _shutdownRequested,
                    0,
                    0) != 0 ||
                    Interlocked.CompareExchange(ref _disposed, 0, 0) != 0)
                {
                    throw new InvalidOperationException(
                        LocalizationService.Get(
                            "Error.Scrcpy.ShutdownRequested"));
                }

                CleanupStaleSlot(slot);
                Process process = null;
                try
                {
                    lock (_syncRoot)
                    {
                        Process existing;
                        if (_processes.TryGetValue(slot, out existing) &&
                            IsProcessRunning(existing))
                        {
                            _logService.Warning(LocalizationService.Format(
                                "Log.SingleWindow.AlreadyRunning",
                                slot));
                            return;
                        }

                        var serial = requestedSerial;
                        if (string.IsNullOrWhiteSpace(serial) ||
                            !_adbService.IsAuthorizedDeviceConnected(serial))
                        {
                            throw new InvalidOperationException(
                                LocalizationService.Get(
                                    "Error.Dex.NoAuthorizedDevice"));
                        }
                        var arguments = BuildArguments(
                            slot,
                            settings,
                            serial);
                        process = CreateProcess(arguments);
                        process.EnableRaisingEvents = true;
                        process.Exited += Process_Exited;
                        process.OutputDataReceived +=
                            Process_OutputDataReceived;
                        process.ErrorDataReceived +=
                            Process_ErrorDataReceived;
                        _processes[slot] = process;
                        _stayAwakeRequests[slot] = settings.StayAwake;
                        _screenOffRequests[slot] = settings.TurnScreenOff;
                        _targetSerials[slot] = serial;
                        _displayIds.Remove(slot);
                        _windowHandles.Remove(slot);
                        _stoppingSlots.Remove(slot);

                        _logService.Info(LocalizationService.Format(
                            "Log.SingleWindow.Start",
                            slot,
                            arguments));
                    }

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    WaitForMainWindow(process, slot);
                    started = true;
                }
                catch
                {
                    if (process != null) AbortStart(process, slot);
                    throw;
                }
            });

            if (started) RaiseRunningChanged();
        }

        public void Restart(int slot, SingleWindowSlotSettings settings)
        {
            _launchCoordinator.RunExclusive(delegate
            {
                string serial;
                lock (_syncRoot)
                {
                    if (!_targetSerials.TryGetValue(slot, out serial))
                        serial = _adbService.TargetSerial;
                }
                Stop(slot);
                Start(slot, settings, serial);
            });
        }

        public void Stop(int slot)
        {
            var stopped = false;
            _launchCoordinator.RunExclusive(delegate
            {
                stopped = StopSlotCore(slot);
            });

            if (stopped) RaiseRunningChanged();
        }

        public void StopAll()
        {
            var stoppedCount = 0;
            var errors = new List<Exception>();
            _launchCoordinator.RunExclusive(delegate
            {
                List<int> slots;
                lock (_syncRoot)
                {
                    slots = new List<int>(_processes.Keys);
                }

                foreach (var slot in slots)
                {
                    try
                    {
                        if (StopSlotCore(slot)) stoppedCount++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                        _logService.Error(
                            LocalizationService.Format(
                                "Log.SingleWindow.StopFailed",
                                slot),
                            ex);
                    }
                }
            });

            if (stoppedCount > 0) RaiseRunningChanged();
            if (errors.Count > 0) throw new AggregateException(errors);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            RequestShutdown();
            StopAll();
        }

        private bool StopSlotCore(int slot)
        {
            Process process;
            lock (_syncRoot)
            {
                if (!_processes.TryGetValue(slot, out process) ||
                    _stoppingSlots.Contains(slot))
                {
                    return false;
                }
                _stoppingSlots.Add(slot);
                _windowHandles.Remove(slot);
            }

            try
            {
                EnsureProcessStopped(process);
                CompleteExplicitStop(slot, process);
                _logService.Info(LocalizationService.Format(
                    "Log.SingleWindow.Stopped",
                    slot));
                return true;
            }
            catch
            {
                if (!IsProcessRunning(process))
                {
                    CompleteExplicitStop(slot, process);
                    return true;
                }

                lock (_syncRoot)
                {
                    Process current;
                    if (_processes.TryGetValue(slot, out current) &&
                        ReferenceEquals(current, process))
                    {
                        _stoppingSlots.Remove(slot);
                    }
                }
                throw;
            }
        }

        private string BuildArguments(
            int slot,
            SingleWindowSlotSettings settings)
        {
            return BuildArguments(
                slot,
                settings,
                _adbService.TargetSerial);
        }

        private string BuildArguments(
            int slot,
            SingleWindowSlotSettings settings,
            string serial)
        {
            ScrcpyService.ValidateAdditionalArguments(
                settings.AdditionalArguments);
            var arguments = new List<string>();
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
            if (settings.StayAwake)
                arguments.Add(_runtimeInfo.StayAwakeArgument);
            if (settings.FlexDisplay)
            {
                if (_runtimeInfo.SupportsFlexDisplay)
                {
                    arguments.Add("--flex-display");
                }
                else if (!_unsupportedFlexDisplayLogged)
                {
                    _unsupportedFlexDisplayLogged = true;
                    _logService.Warning(LocalizationService.Get(
                        "Log.Scrcpy.FlexDisplayUnsupported"));
                }
            }
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
                    StandardOutputEncoding = _runtimeInfo.OutputEncoding,
                    StandardErrorEncoding = _runtimeInfo.OutputEncoding
                }
            };
        }

        private void Process_Exited(object sender, EventArgs e)
        {
            var process = sender as Process;
            var slot = 0;
            var ownedProcess = false;
            lock (_syncRoot)
            {
                foreach (var item in _processes)
                {
                    if (!ReferenceEquals(item.Value, process)) continue;
                    slot = item.Key;
                    break;
                }
                if (slot > 0 && !_stoppingSlots.Contains(slot))
                {
                    _processes.Remove(slot);
                    _stayAwakeRequests.Remove(slot);
                    _screenOffRequests.Remove(slot);
                    _targetSerials.Remove(slot);
                    _displayIds.Remove(slot);
                    _windowHandles.Remove(slot);
                    ownedProcess = true;
                }
            }

            if (ownedProcess)
            {
                _logService.Info(LocalizationService.Format(
                    "Log.SingleWindow.ProcessExited",
                    slot));
                RaiseRunningChanged();
                QueueDrainAndDispose(process);
            }
        }

        private void Process_OutputDataReceived(
            object sender,
            DataReceivedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Data)) return;
            CaptureDisplayId(sender as Process, e.Data);
            _logService.Info("[single scrcpy] " + e.Data);
        }

        private void Process_ErrorDataReceived(
            object sender,
            DataReceivedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Data)) return;
            CaptureDisplayId(sender as Process, e.Data);
            _logService.Warning("[single scrcpy] " + e.Data);
        }

        private void CaptureDisplayId(Process process, string line)
        {
            if (process == null || string.IsNullOrWhiteSpace(line)) return;

            var match = NewDisplayIdRegex.Match(line);
            int displayId;
            if (!match.Success ||
                !int.TryParse(
                    match.Groups[1].Value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out displayId) ||
                displayId <= 0)
            {
                return;
            }

            lock (_syncRoot)
            {
                foreach (var item in _processes)
                {
                    if (!ReferenceEquals(item.Value, process) ||
                        _stoppingSlots.Contains(item.Key))
                    {
                        continue;
                    }

                    _displayIds[item.Key] = displayId;
                    return;
                }
            }
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

        private void EnsureProcessStopped(Process process)
        {
            if (!IsProcessRunning(process)) return;
            var closeRequested = process.CloseMainWindow();
            if (closeRequested &&
                process.WaitForExit(GracefulStopWaitMs))
            {
                return;
            }
            if (!IsProcessRunning(process)) return;

            try
            {
                process.Kill();
            }
            catch (InvalidOperationException)
            {
                return;
            }
            if (!process.WaitForExit(ForcedStopWaitMs))
            {
                _logService.Warning(LocalizationService.Get(
                    "Log.Process.TerminationDeferred"));
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
                if (Interlocked.CompareExchange(
                        ref _shutdownRequested,
                        0,
                        0) != 0)
                {
                    throw new OperationCanceledException();
                }
                if (!IsProcessRunning(process))
                    throw new InvalidOperationException(
                        LocalizationService.Format(
                            "Error.SingleWindow.ExitedBeforeWindow",
                            slot));

                process.Refresh();
                var handle = process.MainWindowHandle;
                if (handle != IntPtr.Zero)
                {
                    lock (_syncRoot)
                    {
                        Process current;
                        if (_processes.TryGetValue(slot, out current) &&
                            ReferenceEquals(current, process) &&
                            !_stoppingSlots.Contains(slot))
                        {
                            _windowHandles[slot] = handle;
                        }
                    }
                    _logService.Info(LocalizationService.Format(
                        "Log.SingleWindow.WindowReady",
                        slot,
                        process.Id));
                    QueueWindowMonitor(process, slot, handle);
                    return;
                }
                Thread.Sleep(50);
            }

            throw new TimeoutException(
                LocalizationService.Format(
                    "Error.SingleWindow.WindowTimeout",
                    slot));
        }

        private void AbortStart(Process process, int slot)
        {
            var ownsProcess = false;
            lock (_syncRoot)
            {
                Process current;
                if (_processes.TryGetValue(slot, out current) &&
                    ReferenceEquals(current, process))
                {
                    _stoppingSlots.Add(slot);
                    ownsProcess = true;
                }
            }

            if (!ownsProcess) return;
            try
            {
                EnsureProcessStopped(process);
                CompleteExplicitStop(slot, process);
            }
            catch
            {
                if (!IsProcessRunning(process))
                    CompleteExplicitStop(slot, process);
                else
                {
                    lock (_syncRoot)
                    {
                        Process current;
                        if (_processes.TryGetValue(slot, out current) &&
                            ReferenceEquals(current, process))
                        {
                            _stoppingSlots.Remove(slot);
                        }
                    }
                }
                throw;
            }
        }

        private void CompleteExplicitStop(int slot, Process process)
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
                    _targetSerials.Remove(slot);
                    _displayIds.Remove(slot);
                    _windowHandles.Remove(slot);
                }
                _stoppingSlots.Remove(slot);
            }
            QueueDrainAndDispose(process);
        }

        private void CleanupStaleSlot(int slot)
        {
            Process stale = null;
            lock (_syncRoot)
            {
                Process current;
                if (_processes.TryGetValue(slot, out current) &&
                    !IsProcessRunning(current) &&
                    !_stoppingSlots.Contains(slot))
                {
                    stale = current;
                    _processes.Remove(slot);
                    _stayAwakeRequests.Remove(slot);
                    _screenOffRequests.Remove(slot);
                    _targetSerials.Remove(slot);
                    _displayIds.Remove(slot);
                    _windowHandles.Remove(slot);
                }
            }
            if (stale != null) QueueDrainAndDispose(stale);
        }

        private void QueueDrainAndDispose(Process process)
        {
            if (process == null) return;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    RetryTerminateProcess(process);
                }
                catch (Exception ex)
                {
                    _logService.Warning(
                        "[single scrcpy] Process termination retry failed: " +
                        ex.Message);
                }
                try
                {
                    DrainAndDispose(process);
                }
                catch (Exception ex)
                {
                    _logService.Warning(
                        "[single scrcpy] Process cleanup failed: " +
                        ex.Message);
                }
            });
        }

        private void QueueWindowMonitor(
            Process process,
            int slot,
            IntPtr handle)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                var missingChecks = 0;
                while (true)
                {
                    lock (_syncRoot)
                    {
                        Process current;
                        IntPtr currentHandle;
                        if (!_processes.TryGetValue(slot, out current) ||
                            !ReferenceEquals(current, process) ||
                            _stoppingSlots.Contains(slot) ||
                            !_windowHandles.TryGetValue(
                                slot,
                                out currentHandle) ||
                            currentHandle != handle)
                        {
                            return;
                        }
                    }

                    if (NativeMethods.IsWindow(handle))
                    {
                        missingChecks = 0;
                    }
                    else if (++missingChecks >= 2)
                    {
                        try
                        {
                            Stop(slot);
                        }
                        catch (Exception ex)
                        {
                            _logService.Warning(
                                "[single scrcpy] Window-close cleanup failed: " +
                                ex.Message);
                        }
                        return;
                    }
                    Thread.Sleep(WindowMonitorIntervalMs);
                }
            });
        }

        private void RetryTerminateProcess(Process process)
        {
            for (var attempt = 0;
                attempt < 5 && IsProcessRunning(process);
                attempt++)
            {
                try
                {
                    process.Kill();
                }
                catch (InvalidOperationException)
                {
                    return;
                }

                if (process.WaitForExit(ForcedStopWaitMs)) return;
                Thread.Sleep(WindowMonitorIntervalMs);
            }
        }

        private void DrainAndDispose(Process process)
        {
            if (process == null) return;
            try
            {
                if (!IsProcessRunning(process)) process.WaitForExit();
            }
            catch (InvalidOperationException)
            {
            }
            finally
            {
                process.Exited -= Process_Exited;
                process.OutputDataReceived -= Process_OutputDataReceived;
                process.ErrorDataReceived -= Process_ErrorDataReceived;
            }
            process.Dispose();
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }
    }
}
