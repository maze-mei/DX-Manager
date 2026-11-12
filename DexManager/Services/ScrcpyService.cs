using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using DexManager.Models;
using DexManager.Utils;

namespace DexManager.Services
{
    public sealed class ScrcpyRuntimeInfo
    {
        private ScrcpyRuntimeInfo(
            int majorVersion,
            int minorVersion,
            int sdlMajorVersion, string reportedVersion = null)
        {
            MajorVersion = majorVersion;
            MinorVersion = minorVersion;
            SdlMajorVersion = sdlMajorVersion;
            ReportedVersion = reportedVersion;
        }

        public int MajorVersion { get; private set; }
        public int MinorVersion { get; private set; }
        public int SdlMajorVersion { get; private set; }
        public string ReportedVersion { get; private set; }

        public bool SupportsKeepActiveLongOption
        {
            get { return MajorVersion >= 4; }
        }

        public bool SupportsFlexDisplay
        {
            get { return MajorVersion >= 4; }
        }

        public bool RequiresRightShiftWorkaround
        {
            get { return SdlMajorVersion >= 3; }
        }

        public string StayAwakeArgument
        {
            get
            {
                return SupportsKeepActiveLongOption
                    ? "--keep-active"
                    : "-w";
            }
        }

        public Encoding OutputEncoding
        {
            get
            {
                return MajorVersion >= 4
                    ? Encoding.UTF8
                    : Encoding.Default;
            }
        }

        public static ScrcpyRuntimeInfo Detect(
            string scrcpyPath,
            int timeoutMs,
            ProcessRunner processRunner)
        {
            var major = 5;
            var minor = 0;
            var sdlMajor = 3;
            string reportedVersion = null;

            try
            {
                var result = processRunner.Run(
                    scrcpyPath,
                    "--version",
                    Path.GetDirectoryName(scrcpyPath),
                    Math.Min(Math.Max(timeoutMs, 1000), 5000),
                    false);
                var text = (result.StandardOutput ?? string.Empty) + "\n" +
                    (result.StandardError ?? string.Empty);
                var versionMatch = Regex.Match(
                    text,
                    @"scrcpy\s+(\d+)\.(\d+)(?:\.(\d+))?",
                    RegexOptions.IgnoreCase);
                if (versionMatch.Success)
                {
                    reportedVersion = versionMatch.Value.Substring(7).Trim();
                    major = int.Parse(
                        versionMatch.Groups[1].Value,
                        CultureInfo.InvariantCulture);
                    minor = int.Parse(
                        versionMatch.Groups[2].Value,
                        CultureInfo.InvariantCulture);
                }

                var sdlMatch = Regex.Match(
                    text,
                    @"SDL:\s*(\d+)\.",
                    RegexOptions.IgnoreCase);
                if (sdlMatch.Success)
                {
                    sdlMajor = int.Parse(
                        sdlMatch.Groups[1].Value,
                        CultureInfo.InvariantCulture);
                }
                else
                {
                    sdlMajor = major >= 4 ? 3 : 2;
                }
            }
            catch
            {
                // Preserve the pinned scrcpy 5.0.1 behavior if probing fails.
            }

            return new ScrcpyRuntimeInfo(major, minor, sdlMajor, reportedVersion);
        }
    }

    public sealed class ScrcpySessionSnapshot
    {
        internal ScrcpySessionSnapshot(
            bool isRunning,
            string serial,
            int displayId,
            bool stayAwakeRequested,
            bool screenOffRequested)
        {
            IsRunning = isRunning;
            Serial = serial ?? string.Empty;
            DisplayId = displayId;
            StayAwakeRequested = stayAwakeRequested;
            ScreenOffRequested = screenOffRequested;
        }

        public bool IsRunning { get; private set; }
        public string Serial { get; private set; }
        public int DisplayId { get; private set; }
        public bool StayAwakeRequested { get; private set; }
        public bool ScreenOffRequested { get; private set; }
    }

    public sealed class ScrcpyService : IDisposable
    {
        private const int WindowMonitorIntervalMs = 100;
        private const int GracefulStopWaitMs = 500;
        private const int ForcedStopWaitMs = 1000;
        private static readonly HashSet<string> ReservedAdditionalOptions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "-s", "-d", "-e",
                "--serial", "--select-usb", "--select-tcpip",
                "--tcpip",
                "--display-id", "--new-display",
                "--push-target",
                "--no-window", "--no-video",
                "--no-video-playback", "--no-playback",
                "--no-cleanup", "--kill-adb-on-close", "--otg",
                "-S", "--turn-screen-off", "--no-power-on", "--max-fps",
                "--power-off-on-close", "--screen-off-timeout",
                "-w", "--stay-awake", "--keep-active",
                "-x", "--flex-display"
            };
        private static readonly HashSet<char> ReservedShortOptionNames =
            new HashSet<char>
            {
                's', 'S', 'd', 'e', 'w', 'x'
            };
        private readonly object _syncRoot = new object();
        private readonly object _processStartSync = new object();
        private readonly string _scrcpyPath;
        private readonly int _processTimeoutMs;
        private readonly ProcessRunner _processRunner;
        private readonly AdbService _adbService;
        private readonly ScrcpyLaunchCoordinator _launchCoordinator;
        private readonly FileTransferCoordinator _fileTransferCoordinator;
        private readonly LogService _logService;
        private readonly ScrcpyRuntimeInfo _runtimeInfo;
        private Process _process;
        private bool _stayAwakeRequested;
        private bool _turnScreenOffRequested;
        private int _runningMaxFps;
        internal Func<bool> CanTurnPhoneScreenOff { get; set; }
        public bool RequiresPhoneScreenOn
        {
            get { lock (_syncRoot) return FrameRateRules.RequiresScreenOn(_runningMaxFps) && IsProcessRunning(_process); }
        }
        private string _targetSerial;
        private int _displayId;
        private IntPtr _mainWindowHandle;
        private string _deviceDisplayName = string.Empty;
        private string _fileTransferSessionId;
        private bool _stopping;
        private int _shutdownRequested;
        private int _disposed;

        public ScrcpyService(
            string scrcpyPath,
            int processTimeoutMs,
            ProcessRunner processRunner,
            AdbService adbService,
            ScrcpyLaunchCoordinator launchCoordinator,
            FileTransferCoordinator fileTransferCoordinator,
            LogService logService)
        {
            if (string.IsNullOrWhiteSpace(scrcpyPath))
                throw new ArgumentException(
                    LocalizationService.Get("Error.Scrcpy.PathEmpty"),
                    "scrcpyPath");

            _scrcpyPath = Path.GetFullPath(scrcpyPath);
            _processTimeoutMs = Math.Max(processTimeoutMs, 1000);
            _processRunner = processRunner ??
                throw new ArgumentNullException("processRunner");
            _adbService = adbService ??
                throw new ArgumentNullException("adbService");
            _launchCoordinator = launchCoordinator ??
                throw new ArgumentNullException("launchCoordinator");
            _fileTransferCoordinator = fileTransferCoordinator ??
                throw new ArgumentNullException("fileTransferCoordinator");
            _logService = logService;
            _runtimeInfo = ScrcpyRuntimeInfo.Detect(
                _scrcpyPath,
                _processTimeoutMs,
                _processRunner);
        }

        public int LastExitCode { get; private set; }
        public event EventHandler RunningChanged;

        public void RequestShutdown()
        {
            lock (_processStartSync)
                Interlocked.Exchange(ref _shutdownRequested, 1);
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

        public string ScrcpyPath
        {
            get { return _scrcpyPath; }
        }

        public ScrcpyRuntimeInfo RuntimeInfo
        {
            get { return _runtimeInfo; }
        }

        public int CurrentProcessId
        {
            get
            {
                lock (_syncRoot)
                {
                    var process = _process;
                    if (!IsProcessRunning(process)) return 0;

                    try
                    {
                        return process.Id;
                    }
                    catch (InvalidOperationException)
                    {
                        return 0;
                    }
                }
            }
        }

        public bool TryGetWindowTarget(
            IntPtr windowHandle,
            out string serial,
            out int processId)
        {
            serial = string.Empty;
            processId = 0;
            if (windowHandle == IntPtr.Zero) return false;
            lock (_syncRoot)
            {
                if (_mainWindowHandle != windowHandle ||
                    !IsProcessRunning(_process) ||
                    string.IsNullOrWhiteSpace(_targetSerial))
                {
                    return false;
                }

                try
                {
                    processId = _process.Id;
                    serial = _targetSerial;
                    return processId > 0;
                }
                catch (InvalidOperationException)
                {
                    serial = string.Empty;
                    processId = 0;
                    return false;
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

        public string RunningSerial
        {
            get
            {
                lock (_syncRoot)
                {
                    return IsProcessRunning(_process)
                        ? _targetSerial
                        : string.Empty;
                }
            }
        }

        public ScrcpySessionSnapshot GetSessionSnapshot()
        {
            lock (_syncRoot)
            {
                var running = IsProcessRunning(_process);
                return new ScrcpySessionSnapshot(
                    running,
                    running ? _targetSerial : string.Empty,
                    running ? _displayId : 0,
                    running && _stayAwakeRequested,
                    running && _turnScreenOffRequested);
            }
        }

        public bool TryGetCachedDisplayForWindow(IntPtr window, out int displayId)
        {
            lock (_syncRoot)
            {
                displayId = _displayId;
                return window != IntPtr.Zero && window == _mainWindowHandle &&
                    _process != null && displayId > 0;
            }
        }

        public IntPtr MainWindowHandle
        {
            get
            {
                lock (_syncRoot)
                {
                    // This property is read from the system-wide low-level
                    // keyboard hook. Never refresh or wait for a Process here:
                    // a closing scrcpy window may synchronously raise Exited,
                    // and blocking this callback stalls keyboard input globally.
                    return _mainWindowHandle;
                }
            }
        }

        public string BuildArguments(
            ScrcpySettings settings,
            int displayId,
            string serial)
        {
            return BuildArguments(
                settings,
                displayId,
                serial,
                _fileTransferCoordinator.GetScrcpyPushTarget());
        }

        private string BuildArguments(
            ScrcpySettings settings,
            int displayId,
            string serial,
            string pushTarget)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            if (string.IsNullOrWhiteSpace(serial))
                throw new ArgumentException(
                    LocalizationService.Get("Error.Adb.SerialEmpty"),
                    "serial");
            ValidateAdditionalArguments(settings.AdditionalArguments);

            var arguments = new List<string>
            {
            };
            AddSerialArgument(arguments, serial);
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
            arguments.Add(Quote(BuildWindowTitle(
                "DX Manager - DeX Station",
                DeviceDisplayName)));

            if (_runtimeInfo.MajorVersion >= 5) AddHardwareDecodingArgument(arguments, settings.AdditionalArguments);
            if (settings.UseHidKeyboard) arguments.Add("-K");
            if (settings.UseHidMouse) arguments.Add("-M");
            if (settings.StayAwake)
                arguments.Add(_runtimeInfo.StayAwakeArgument);
            if (settings.TurnScreenOff && !FrameRateRules.RequiresScreenOn(settings.MaxFps) &&
                (CanTurnPhoneScreenOff == null || CanTurnPhoneScreenOff()))
            {
                arguments.Add("-S");
                arguments.Add("--no-power-on");
            }
            arguments.Add("--push-target");
            arguments.Add(Quote(pushTarget));

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

        internal static void AddHardwareDecodingArgument(IList<string> arguments, string additional)
        {
            // Respect an explicit user override; never emit two decoder selections.
            foreach (var token in TokenizeArguments(additional ?? string.Empty))
                if (string.Equals(token, "--hwdec", StringComparison.Ordinal) ||
                    token.StartsWith("--hwdec=", StringComparison.Ordinal)) return;
            arguments.Add("--hwdec=auto");
        }

        public string ReadReportedVersion()
        {
            var result = _processRunner.Run(_scrcpyPath, "--version",
                Path.GetDirectoryName(_scrcpyPath), Math.Min(_processTimeoutMs, 5000), false);
            if (!result.IsSuccess) throw new InvalidOperationException(result.StandardError);
            var match = Regex.Match((result.StandardOutput ?? "") + "\n" +
                (result.StandardError ?? ""), @"scrcpy\s+(\d+\.\d+(?:\.\d+)?)",
                RegexOptions.IgnoreCase);
            if (!match.Success) throw new InvalidOperationException(
                LocalizationService.Get("Update.VersionUnavailable"));
            return match.Groups[1].Value;
        }
        public static void ValidateAdditionalArguments(string arguments)
        {
            foreach (var token in TokenizeArguments(arguments))
            {
                var option = token;
                var equalsIndex = option.IndexOf('=');
                if (equalsIndex > 0)
                    option = option.Substring(0, equalsIndex);
                if (ReservedAdditionalOptions.Contains(option) ||
                    IsReservedLongOptionAbbreviation(option) ||
                    ContainsReservedShortOption(option))
                {
                    throw new InvalidOperationException(
                        LocalizationService.Format(
                            "Error.Scrcpy.ReservedAdditionalArgument",
                            token));
                }
            }
        }

        private static bool IsReservedLongOptionAbbreviation(string option)
        {
            if (string.IsNullOrEmpty(option) ||
                !option.StartsWith("--", StringComparison.Ordinal) ||
                option.Length <= 2)
            {
                return false;
            }

            foreach (var reserved in ReservedAdditionalOptions)
            {
                if (!reserved.StartsWith("--", StringComparison.Ordinal))
                    continue;
                if (reserved.Length > option.Length &&
                    reserved.StartsWith(
                        option,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool ContainsReservedShortOption(string option)
        {
            if (string.IsNullOrEmpty(option) ||
                option.Length <= 2 ||
                option[0] != '-' ||
                option[1] == '-')
            {
                return false;
            }
            for (var index = 1; index < option.Length; index++)
            {
                if (ReservedShortOptionNames.Contains(option[index]))
                    return true;
            }
            return false;
        }

        private static IEnumerable<string> TokenizeArguments(string value)
        {
            var tokens = new List<string>();
            var token = new StringBuilder();
            var inQuotes = false;
            foreach (var character in value ?? string.Empty)
            {
                if (character == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }
                if (char.IsWhiteSpace(character) && !inQuotes)
                {
                    if (token.Length == 0) continue;
                    tokens.Add(token.ToString());
                    token.Clear();
                    continue;
                }
                token.Append(character);
            }
            if (token.Length > 0) tokens.Add(token.ToString());
            return tokens;
        }

        public IList<ScrcpyAppInfo> ListApps(string serial)
        {
            if (!File.Exists(_scrcpyPath))
                throw new FileNotFoundException(
                    LocalizationService.Get(
                        "Error.Scrcpy.FileNotFound"),
                    _scrcpyPath);

            var arguments = new List<string>();
            AddRequiredSerialArgument(arguments, serial);
            arguments.Add("--list-apps");
            var result = _launchCoordinator.RunExclusive(delegate
            {
                return _processRunner.Run(
                    _scrcpyPath,
                    string.Join(" ", arguments),
                    Path.GetDirectoryName(_scrcpyPath),
                    _processTimeoutMs,
                    true,
                    _runtimeInfo.OutputEncoding);
            });
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException(
                    LocalizationService.Format(
                        "Error.Scrcpy.AppListLoadFailed",
                        result.StandardError));
            }

            var output = (result.StandardOutput ?? string.Empty) + "\n" +
                (result.StandardError ?? string.Empty);
            var apps = ParseAppList(output);
            if (apps.Count == 0)
                throw new InvalidOperationException(
                    LocalizationService.Get(
                        "Error.Scrcpy.AppListParseFailed"));

            _logService.Info(LocalizationService.Format(
                "Log.Scrcpy.AppListLoaded",
                apps.Count));
            return apps;
        }

        public void Start(
            ScrcpySettings settings,
            int displayId,
            string serial, bool experimentalFpsConfirmed = false)
        {
            FrameRateRules.RequireConsent(settings.MaxFps, experimentalFpsConfirmed);
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

                CleanupStaleProcess();
                Process process = null;
                string transferSessionId = null;
                try
                {
                    lock (_syncRoot)
                    {
                        if (IsProcessRunning(_process))
                        {
                            _logService.Warning(LocalizationService.Get(
                                "Log.Scrcpy.AlreadyRunning"));
                            return;
                        }

                        if (!File.Exists(_scrcpyPath))
                            throw new FileNotFoundException(
                                LocalizationService.Get(
                                    "Error.Scrcpy.FileNotFound"),
                                _scrcpyPath);

                        var pushTarget = _fileTransferCoordinator
                            .PrepareScrcpyPushTarget(serial);
                        var arguments = BuildArguments(
                            settings,
                            displayId,
                            serial,
                            pushTarget);
                        transferSessionId =
                            _fileTransferCoordinator.BeginSession(
                                serial,
                                "DeX",
                                pushTarget);
                        process = CreateProcess(
                            arguments,
                            true,
                            transferSessionId);
                        process.EnableRaisingEvents = true;
                        process.Exited += Process_Exited;
                        process.OutputDataReceived +=
                            Process_OutputDataReceived;
                        process.ErrorDataReceived +=
                            Process_ErrorDataReceived;
                        _process = process;
                        _stopping = false;
                        _stayAwakeRequested = settings.StayAwake;
                        _turnScreenOffRequested = settings.TurnScreenOff && !FrameRateRules.RequiresScreenOn(settings.MaxFps);
                        _targetSerial = serial;
                        _displayId = displayId;
                        _mainWindowHandle = IntPtr.Zero;
                        _fileTransferSessionId = transferSessionId;

                        _logService.Progress(LocalizationService.Format(
                            "Log.Scrcpy.Start",
                            arguments));
                    }

                    lock (_processStartSync)
                    {
                        if (Interlocked.CompareExchange(
                                ref _shutdownRequested, 0, 0) != 0 ||
                            Interlocked.CompareExchange(
                                ref _disposed, 0, 0) != 0)
                        {
                            throw new InvalidOperationException(
                                LocalizationService.Get(
                                    "Error.Scrcpy.ShutdownRequested"));
                        }
                        lock (_syncRoot) _runningMaxFps = settings.MaxFps;
                        process.Start();
                    }
                    _fileTransferCoordinator.BindProcess(
                        transferSessionId,
                        process.Id);
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    WaitForMainWindow(process);
                    started = true;
                }
                catch
                {
                    if (process != null) AbortStart(process);
                    else _fileTransferCoordinator.EndSession(
                        transferSessionId);
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
                string serial;
                lock (_syncRoot)
                {
                    process = _process;
                    if (process == null || _stopping) return;
                    _stopping = true;
                    _mainWindowHandle = IntPtr.Zero;
                    serial = _targetSerial;
                }

                try
                {
                    EnsureProcessStopped(process);
                    CompleteExplicitStop(process);
                    stopped = true;
                    _logService.Info(DeviceLogFormatter.ForSerial(
                        serial,
                        LocalizationService.Get(
                            "Log.Scrcpy.Stopped")));
                }
                catch
                {
                    if (!IsProcessRunning(process))
                    {
                        CompleteExplicitStop(process);
                        stopped = true;
                    }
                    else
                    {
                        lock (_syncRoot)
                        {
                            if (ReferenceEquals(_process, process))
                                _stopping = false;
                        }
                    }
                    throw;
                }
            });

            if (stopped) RaiseRunningChanged();
        }

        public bool RunWakeUp(string serial, int delayMs)
        {
            return _launchCoordinator.RunExclusive(delegate
            {
                return RunWakeUpCore(serial, delayMs);
            });
        }

        private bool RunWakeUpCore(string serial, int delayMs)
        {
            if (!File.Exists(_scrcpyPath))
            {
                _logService.Warning(LocalizationService.Format(
                    "Log.Scrcpy.WakeUpFileMissing",
                    _scrcpyPath));
                return false;
            }

            _logService.Info(LocalizationService.Get(
                "Log.Scrcpy.WakeUpStarting"));
            var arguments = new List<string>();
            AddSerialArgument(arguments, serial);
            arguments.Add("--no-audio");
            arguments.Add("--max-size=64");
            arguments.Add("--max-fps=1");
            using (var process = CreateProcess(
                string.Join(" ", arguments),
                false,
                string.Empty))
            {
                try
                {
                    lock (_processStartSync)
                    {
                        if (Interlocked.CompareExchange(
                                ref _shutdownRequested, 0, 0) != 0 ||
                            Interlocked.CompareExchange(
                                ref _disposed, 0, 0) != 0)
                        {
                            return false;
                        }
                        process.Start();
                    }
                    var waitMilliseconds = Math.Max(delayMs, 500);
                    var stopwatch = Stopwatch.StartNew();
                    while (stopwatch.ElapsedMilliseconds < waitMilliseconds)
                    {
                        if (Interlocked.CompareExchange(
                                ref _shutdownRequested,
                                0,
                                0) != 0)
                        {
                            StopWakeUpProcess(process);
                            return false;
                        }
                        Thread.Sleep(50);
                    }
                    StopWakeUpProcess(process);
                    return true;
                }
                catch (Exception ex)
                {
                    _logService.Error(
                        LocalizationService.Get(
                            "Log.Scrcpy.WakeUpFailed"),
                        ex);
                    return false;
                }
            }
        }

        private void StopWakeUpProcess(Process process)
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                if (!IsProcessRunning(process))
                {
                    process.WaitForExit();
                    return;
                }
                try
                {
                    process.Kill();
                }
                catch (InvalidOperationException)
                {
                    process.WaitForExit();
                    return;
                }
                if (process.WaitForExit(2000))
                {
                    process.WaitForExit();
                    return;
                }
            }
            throw new TimeoutException(LocalizationService.Get(
                "Error.Scrcpy.StopTimeout"));
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            RequestShutdown();
            Stop();
        }

        private Process CreateProcess(
            string arguments,
            bool redirectOutput,
            string transferSessionId)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _scrcpyPath,
                Arguments = arguments,
                WorkingDirectory = Path.GetDirectoryName(_scrcpyPath),
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Minimized,
                RedirectStandardOutput = redirectOutput,
                RedirectStandardError = redirectOutput
            };
            if (redirectOutput)
            {
                startInfo.StandardOutputEncoding =
                    _runtimeInfo.OutputEncoding;
                startInfo.StandardErrorEncoding =
                    _runtimeInfo.OutputEncoding;
            }
            _fileTransferCoordinator.ConfigureScrcpyProcess(
                startInfo,
                transferSessionId);
            return new Process
            {
                StartInfo = startInfo
            };
        }

        private void Process_Exited(object sender, EventArgs e)
        {
            var process = sender as Process;
            var ownedProcess = false;
            string transferSessionId = null;
            string serial = null;
            lock (_syncRoot)
            {
                if (ReferenceEquals(_process, process) && !_stopping)
                {
                    serial = _targetSerial;
                    try { LastExitCode = process.ExitCode; } catch (InvalidOperationException) { LastExitCode = 2; }
                    _process = null;
                    _stayAwakeRequested = false;
                    _turnScreenOffRequested = false;
                    _targetSerial = string.Empty;
                    _displayId = 0;
                    _mainWindowHandle = IntPtr.Zero;
                    transferSessionId = _fileTransferSessionId;
                    _fileTransferSessionId = string.Empty;
                    ownedProcess = true;
                }
            }

            if (!ownedProcess) return;
            _fileTransferCoordinator.EndSession(transferSessionId);
            _logService.Progress(DeviceLogFormatter.ForSerial(
                serial,
                LocalizationService.Get(
                    "Log.Scrcpy.ProcessExited")));
            RaiseRunningChanged();
            QueueDrainAndDispose(process);
        }

        private void WaitForMainWindow(Process process)
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
                        LocalizationService.Get(
                            "Error.Scrcpy.ExitedBeforeWindow"));

                process.Refresh();
                var handle = process.MainWindowHandle;
                if (handle != IntPtr.Zero)
                {
                    string transferSessionId = null;
                    lock (_syncRoot)
                    {
                        if (ReferenceEquals(_process, process) && !_stopping)
                        {
                            _mainWindowHandle = handle;
                            transferSessionId = _fileTransferSessionId;
                        }
                    }
                    _fileTransferCoordinator.BindWindow(
                        transferSessionId,
                        handle);
                    _logService.Progress(DeviceLogFormatter.ForSerial(
                        GetActiveSerialForProcess(process),
                        LocalizationService.Format(
                            "Log.Scrcpy.WindowReady",
                            process.Id)));
                    QueueWindowMonitor(process, handle);
                    return;
                }
                Thread.Sleep(50);
            }

            throw new TimeoutException(
                LocalizationService.Get(
                    "Error.Scrcpy.WindowTimeout"));
        }

        private void AbortStart(Process process)
        {
            var ownedProcess = false;
            lock (_syncRoot)
            {
                if (ReferenceEquals(_process, process))
                {
                    _stopping = true;
                    ownedProcess = true;
                }
            }
            if (!ownedProcess) return;

            try
            {
                EnsureProcessStopped(process);
                CompleteExplicitStop(process);
            }
            catch
            {
                if (!IsProcessRunning(process))
                    CompleteExplicitStop(process);
                else
                {
                    lock (_syncRoot)
                    {
                        if (ReferenceEquals(_process, process))
                            _stopping = false;
                    }
                }
                throw;
            }
        }

        private void CleanupStaleProcess()
        {
            Process stale = null;
            string transferSessionId = null;
            lock (_syncRoot)
            {
                if (_process != null &&
                    !_stopping &&
                    !IsProcessRunning(_process))
                {
                    stale = _process;
                    _process = null;
                    _stayAwakeRequested = false;
                    _turnScreenOffRequested = false;
                    _targetSerial = string.Empty;
                    _displayId = 0;
                    _mainWindowHandle = IntPtr.Zero;
                    transferSessionId = _fileTransferSessionId;
                    _fileTransferSessionId = string.Empty;
                }
            }

            if (stale == null) return;
            _fileTransferCoordinator.EndSession(transferSessionId);
            RaiseRunningChanged();
            QueueDrainAndDispose(stale);
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

        private void CompleteExplicitStop(Process process)
        {
            var ownedProcess = false;
            string transferSessionId = null;
            lock (_syncRoot)
            {
                if (ReferenceEquals(_process, process))
                {
                    _process = null;
                    _stopping = false;
                    _stayAwakeRequested = false;
                    _turnScreenOffRequested = false;
                    _targetSerial = string.Empty;
                    _displayId = 0;
                    _mainWindowHandle = IntPtr.Zero;
                    transferSessionId = _fileTransferSessionId;
                    _fileTransferSessionId = string.Empty;
                    ownedProcess = true;
                }
            }
            if (ownedProcess)
            {
                _fileTransferCoordinator.EndSession(transferSessionId);
                QueueDrainAndDispose(process);
            }
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
                        "[scrcpy] Process termination retry failed: " +
                        ex.Message);
                }
                try
                {
                    DrainAndDispose(process);
                }
                catch (Exception ex)
                {
                    _logService.Warning(
                        "[scrcpy] Process cleanup failed: " + ex.Message);
                }
            });
        }

        private void QueueWindowMonitor(Process process, IntPtr handle)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                var missingChecks = 0;
                while (true)
                {
                    lock (_syncRoot)
                    {
                        if (!ReferenceEquals(_process, process) ||
                            _stopping ||
                            _mainWindowHandle != handle)
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
                            Stop();
                        }
                        catch (Exception ex)
                        {
                            _logService.Warning(
                                "[scrcpy] Window-close cleanup failed: " +
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

        private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                _logService.ToolOutput("INFO",DeviceLogFormatter.ForSerial(
                    GetActiveSerialForProcess(sender as Process),
                    "[scrcpy] " + e.Data));
        }

        private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Data)) return;
            var message = DeviceLogFormatter.ForSerial(
                GetActiveSerialForProcess(sender as Process),
                "[scrcpy] " + e.Data);
            _logService.ToolOutput(DeviceLogFormatter.IsInformationalScrcpyErrorLine(e.Data)?"INFO":"WARN",message);
        }

        private string GetActiveSerialForProcess(Process process)
        {
            lock (_syncRoot)
            {
                return ReferenceEquals(_process, process)
                    ? _targetSerial
                    : string.Empty;
            }
        }

        public string DeviceDisplayName
        {
            get
            {
                lock (_syncRoot) return _deviceDisplayName;
            }
            set
            {
                lock (_syncRoot)
                    _deviceDisplayName = (value ?? string.Empty).Trim();
            }
        }

        internal static string BuildWindowTitle(
            string baseTitle,
            string deviceDisplayName)
        {
            var title = (baseTitle ?? string.Empty).Trim();
            var device = (deviceDisplayName ?? string.Empty).Trim();
            return device.Length == 0 ? title : title + " - " + device;
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

        private static void AddSerialArgument(
            ICollection<string> arguments,
            string serial)
        {
            if (string.IsNullOrWhiteSpace(serial)) return;
            arguments.Add("--serial");
            arguments.Add(Quote(serial));
        }

        private static void AddRequiredSerialArgument(
            ICollection<string> arguments,
            string serial)
        {
            if (string.IsNullOrWhiteSpace(serial))
                throw new ArgumentException(
                    LocalizationService.Get("Error.Adb.SerialEmpty"),
                    "serial");
            AddSerialArgument(arguments, serial);
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
