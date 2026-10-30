using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DexManager.Models;
using DexManager.Utils;

namespace DexManager.Services
{
    public sealed class AdbService
    {
        private const int PackageInstallTimeoutMs = 120000;
        private const int MdnsQueryTimeoutMs = 8000;
        private readonly string _adbPath;
        private readonly int _defaultTimeoutMs;
        private readonly ProcessRunner _processRunner;
        private readonly LogService _logService;

        public AdbService(
            string adbPath,
            int defaultTimeoutMs,
            ProcessRunner processRunner,
            LogService logService)
        {
            if (string.IsNullOrWhiteSpace(adbPath))
                throw new ArgumentException(
                    LocalizationService.Get("Error.Adb.PathEmpty"),
                    "adbPath");

            _adbPath = Path.GetFullPath(adbPath);
            _defaultTimeoutMs = Math.Max(defaultTimeoutMs, 1000);
            _processRunner = processRunner;
            _logService = logService;
        }

        public string AdbPath
        {
            get { return _adbPath; }
        }

        public bool IsProcessShutdownRequested
        {
            get { return _processRunner.IsShutdownRequested; }
        }

        internal Process StartShellSession(string serial, string command,
            DataReceivedEventHandler output, DataReceivedEventHandler error)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _adbPath,
                    Arguments = AdbCommandBuilder.ForDevice(serial, "shell -T " + command),
                    WorkingDirectory = Path.GetDirectoryName(_adbPath),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                }
            };
            process.OutputDataReceived += output;
            process.ErrorDataReceived += error;
            try
            {
                _processRunner.StartPersistentProcess(process);
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                return process;
            }
            catch
            {
                try { if (!process.HasExited) process.Kill(); } catch { }
                _processRunner.ForgetPersistentProcess(process);
                process.Dispose();
                throw;
            }
        }

        internal void ForgetShellSession(Process process)
        {
            _processRunner.ForgetPersistentProcess(process);
        }

        public void BeginProcessShutdown()
        {
            _processRunner.BeginShutdown();
            TerminateSelectedAdbProcesses();
        }

        public void BlockNewProcessesForWindowsShutdown()
        {
            _processRunner.BlockNewProcessesForWindowsShutdown();
        }

        private void TerminateSelectedAdbProcesses()
        {
            var processName = Path.GetFileNameWithoutExtension(_adbPath);
            if (string.IsNullOrWhiteSpace(processName)) return;

            var terminatedCount = 0;
            foreach (var process in Process.GetProcessesByName(processName))
            {
                using (process)
                {
                    try
                    {
                        var executablePath = process.MainModule == null
                            ? null
                            : process.MainModule.FileName;
                        if (string.IsNullOrWhiteSpace(executablePath)) continue;

                        var fullPath = Path.GetFullPath(executablePath);
                        if (!string.Equals(
                            fullPath,
                            _adbPath,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (process.HasExited) continue;
                        process.Kill();
                        terminatedCount++;
                    }
                    catch
                    {
                        // Windows session shutdown must continue even when a
                        // process exits between enumeration and inspection.
                    }
                }
            }

            if (terminatedCount > 0)
            {
                _logService.Info(LocalizationService.Format(
                    "Log.Adb.ShutdownProcessesTerminated",
                    terminatedCount));
            }
        }

        public ProcessResult StartServer()
        {
            var result = Run("start-server");
            LogCommandResult(
                LocalizationService.Get("Log.Adb.StartServerResult"),
                result);
            return result;
        }

        public ProcessResult GetVersion()
        {
            return Run("version");
        }

        public IList<AdbMdnsServiceRecord> GetMdnsServices(bool writeLog)
        {
            return GetMdnsServices(writeLog, MdnsQueryTimeoutMs);
        }

        public IList<AdbMdnsServiceRecord> GetMdnsServices(
            bool writeLog,
            int timeoutMs)
        {
            var result = Run(
                "mdns services",
                writeLog,
                Math.Max(1000, timeoutMs));
            if (result == null || !result.IsSuccess)
                return new List<AdbMdnsServiceRecord>();

            return AdbMdnsOutputParser.Parse(
                (result.StandardOutput ?? string.Empty) + "\n" +
                (result.StandardError ?? string.Empty));
        }

        public void LogStartupDiagnostics()
        {
            _logService.Info(LocalizationService.Format(
                "Log.Adb.SelectedPath",
                _adbPath));
            LogCommandResult(
                LocalizationService.Get("Log.Adb.VersionResult"),
                GetVersion());
        }

        public ProcessResult KillServer()
        {
            return Run("kill-server");
        }

        public ProcessResult GetState(string serial)
        {
            return RunForSerial(serial, "get-state", true);
        }

        public ProcessResult ShellForSerial(
            string serial,
            string command,
            bool writeLog)
        {
            if (string.IsNullOrWhiteSpace(serial))
                throw new ArgumentException(
                    LocalizationService.Get("Error.Adb.SerialEmpty"),
                    "serial");
            if (string.IsNullOrWhiteSpace(command))
                throw new ArgumentException("ADB shell command is empty.", "command");
            return RunForSerial(
                serial,
                "shell " + command,
                writeLog);
        }

        public string GetDeviceDisplayName(string serial)
        {
            var settingCommands = new[]
            {
                "settings get global device_name",
                "settings get secure bluetooth_name"
            };

            foreach (var command in settingCommands)
            {
                var value = ReadDeviceText(serial, command);
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }

            var properties = new[]
            {
                "ro.product.marketname",
                "ro.product.vendor.marketname",
                "ro.product.model"
            };

            foreach (var property in properties)
            {
                var value = ReadDeviceText(
                    serial,
                    "getprop " + property);
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }

            return string.Empty;
        }

        public string GetDeviceIdentity(string serial)
        {
            var serialProperties = new[]
            {
                "ro.serialno",
                "ro.boot.serialno"
            };

            foreach (var property in serialProperties)
            {
                var value = ReadDeviceText(
                    serial,
                    "getprop " + property);
                if (!string.IsNullOrWhiteSpace(value))
                    return "serial:" + value;
            }

            var androidId = ReadDeviceText(
                serial,
                "settings get secure android_id");
            return string.IsNullOrWhiteSpace(androidId)
                ? string.Empty
                : "android:" + androidId;
        }

        private string ReadDeviceText(string serial, string command)
        {
            var result = ShellForSerial(serial, command, false);
            if (!result.IsSuccess ||
                string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                return string.Empty;
            }

            var value = result.StandardOutput.Trim();
            return string.Equals(
                       value,
                       "null",
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       value,
                       "unknown",
                       StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : value;
        }

        public ProcessResult PushForSerial(
            string serial,
            string localPath,
            string remotePath)
        {
            ValidatePushPaths(localPath, remotePath);
            return RunForSerial(
                serial,
                "push " + Quote(localPath) + " " + Quote(remotePath),
                true);
        }

        public ProcessResult PullForSerial(
            string serial,
            string remotePath,
            string localPath,
            bool writeLog)
        {
            if (string.IsNullOrWhiteSpace(remotePath))
                throw new ArgumentException(
                    LocalizationService.Get("Error.Adb.RemotePathEmpty"),
                    "remotePath");
            if (string.IsNullOrWhiteSpace(localPath))
                throw new ArgumentException(
                    "Local ADB pull path is empty.",
                    "localPath");
            return RunForSerial(
                serial,
                "pull " + Quote(remotePath) + " " + Quote(localPath),
                writeLog);
        }

        public ProcessResult CleanupForWindowsShutdown(
            string serial,
            bool removeOverlay,
            bool restoreStayAwake,
            string originalStayAwakeValue)
        {
            var commands = new List<string>();
            if (removeOverlay)
            {
                commands.Add(
                    "settings delete global overlay_display_devices");
            }
            if (restoreStayAwake)
            {
                if (originalStayAwakeValue == null)
                {
                    commands.Add(
                        "settings delete global stay_on_while_plugged_in");
                }
                else
                {
                    int parsed;
                    if (!int.TryParse(
                            originalStayAwakeValue,
                            out parsed) ||
                        parsed < 0)
                    {
                        throw new ArgumentException(
                            "The original stay-awake value is invalid.",
                            "originalStayAwakeValue");
                    }
                    commands.Add(
                        "settings put global stay_on_while_plugged_in " +
                        parsed.ToString(
                            System.Globalization.CultureInfo.InvariantCulture));
                }
            }
            if (commands.Count == 0)
                throw new ArgumentException(
                    "No Windows shutdown cleanup command was requested.");

            return Run(
                AdbCommandBuilder.ForShellCommands(
                    serial,
                    commands.ToArray()),
                false,
                _defaultTimeoutMs);
        }

        public ProcessResult InstallPackageForSerial(
            string serial,
            string apkPath,
            bool replaceExisting)
        {
            if (string.IsNullOrWhiteSpace(apkPath))
                throw new ArgumentException(
                    "APK path is empty.",
                    "apkPath");
            var fullPath = Path.GetFullPath(apkPath);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException(
                    "APK file was not found.",
                    fullPath);
            return RunForSerial(
                serial,
                "install " + (replaceExisting ? "-r " : string.Empty) +
                Quote(fullPath),
                true,
                PackageInstallTimeoutMs);
        }

        public ProcessResult UninstallPackageForSerial(
            string serial,
            string packageName)
        {
            if (string.IsNullOrWhiteSpace(packageName) ||
                !Regex.IsMatch(
                    packageName,
                    @"^[A-Za-z0-9_]+(?:\.[A-Za-z0-9_]+)+$",
                    RegexOptions.CultureInvariant))
            {
                throw new ArgumentException(
                    "Android package name is invalid.",
                    "packageName");
            }
            return RunForSerial(
                serial,
                "uninstall " + packageName,
                true);
        }

        public ProcessResult ReverseForSerial(
            string serial,
            int devicePort,
            int localPort,
            bool writeLog)
        {
            ValidatePort(devicePort, "devicePort");
            ValidatePort(localPort, "localPort");
            return RunForSerial(
                serial,
                "reverse tcp:" + devicePort + " tcp:" + localPort,
                writeLog);
        }

        public ProcessResult RemoveReverseForSerial(
            string serial,
            int devicePort,
            bool writeLog)
        {
            ValidatePort(devicePort, "devicePort");
            return RunForSerial(
                serial,
                "reverse --remove tcp:" + devicePort,
                writeLog);
        }

        private static void ValidatePushPaths(
            string localPath,
            string remotePath)
        {
            if (!File.Exists(localPath))
                throw new FileNotFoundException(
                    LocalizationService.Get("Error.Adb.PushFileNotFound"),
                    localPath);
            if (string.IsNullOrWhiteSpace(remotePath))
                throw new ArgumentException(
                    LocalizationService.Get("Error.Adb.RemotePathEmpty"),
                    "remotePath");
        }

        public ProcessResult EnableTcpIp(string serial, int port)
        {
            ValidatePort(port, "port");
            return RunForSerial(
                serial,
                "tcpip " + port,
                true);
        }

        public ProcessResult Connect(string endpoint, bool writeLog)
        {
            return Connect(endpoint, writeLog, _defaultTimeoutMs);
        }

        public ProcessResult Connect(string endpoint, bool writeLog, int timeoutMs)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException(
                    LocalizationService.Get(
                        "Error.Adb.WirelessEndpointEmpty"),
                    "endpoint");
            return Run("connect " + Quote(endpoint.Trim()), writeLog,
                Math.Min(_defaultTimeoutMs, Math.Max(1000, timeoutMs)));
        }

        public ProcessResult Disconnect(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException(
                    LocalizationService.Get(
                        "Error.Adb.WirelessEndpointEmpty"),
                    "endpoint");
            return Run("disconnect " + Quote(endpoint.Trim()), true);
        }

        public ProcessResult Pair(
            string endpoint,
            string pairingCode)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException(
                    LocalizationService.Get(
                        "Error.Adb.PairingEndpointEmpty"),
                    "endpoint");
            if (string.IsNullOrWhiteSpace(pairingCode))
                throw new ArgumentException(
                    LocalizationService.Get(
                        "Error.Adb.PairingCodeEmpty"),
                    "pairingCode");

            _logService.Info(LocalizationService.Format(
                "Log.Adb.PairingAttempt",
                endpoint.Trim()));
            var result = Run(
                "pair " + Quote(endpoint.Trim()) + " " +
                Quote(pairingCode.Trim()),
                false);
            LogCommandResult(
                LocalizationService.Get("Log.Adb.PairResult"),
                SanitizePairResult(result));
            return result;
        }

        public IList<AdbDeviceInfo> GetDevices()
        {
            return GetDevices(true);
        }

        public IList<AdbDeviceInfo> GetDevices(bool writeLog)
        {
            IList<AdbDeviceInfo> devices;
            TryGetDevices(writeLog, out devices);
            return devices;
        }

        public bool TryGetDevices(
            bool writeLog,
            out IList<AdbDeviceInfo> devices)
        {
            var result = Run("devices", writeLog);
            devices = ParseDevices(result.StandardOutput);
            var querySucceeded = result.IsSuccess &&
                !string.IsNullOrWhiteSpace(result.StandardOutput);

            if (writeLog)
            {
                LogCommandResult(
                    LocalizationService.Get("Log.Adb.DevicesResult"),
                    result);
                _logService.Info(LocalizationService.Format(
                    "Log.Adb.DeviceCount",
                    devices.Count));
            }
            return querySucceeded;
        }

        public bool IsAuthorizedDeviceConnected(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial)) return false;
            var state = GetState(serial);
            return state.IsSuccess &&
                string.Equals(
                    state.StandardOutput.Trim(),
                    "device",
                    StringComparison.OrdinalIgnoreCase);
        }

        public AdbWakeUpResult WakeUp(
            string targetSerial,
            Func<string, bool> scrcpyWakeUp)
        {
            _logService.Info(
                LocalizationService.Get("Log.Adb.WakeUpStarting"));
            var normalizedTarget = string.IsNullOrWhiteSpace(targetSerial)
                ? string.Empty
                : targetSerial.Trim();
            if (!IsWirelessSerial(normalizedTarget))
                KillServer();
            StartServer();

            var devicesBefore = GetDevices();
            if (ContainsAuthorizedDevice(devicesBefore, normalizedTarget))
            {
                return new AdbWakeUpResult(true, false, devicesBefore);
            }

            if (scrcpyWakeUp == null)
            {
                _logService.Warning(LocalizationService.Get(
                    "Log.Adb.WakeUpScrcpyUnavailable"));
                return new AdbWakeUpResult(false, false, devicesBefore);
            }

            _logService.Warning(LocalizationService.Get(
                "Log.Adb.WakeUpFallback"));
            var scrcpyStarted = scrcpyWakeUp(normalizedTarget);
            var devicesAfter = GetDevices();
            var success = scrcpyStarted &&
                ContainsAuthorizedDevice(devicesAfter, normalizedTarget);

            if (success)
                _logService.Info(LocalizationService.Get(
                    "Log.Adb.WakeUpDeviceFound"));
            else
                _logService.Warning(LocalizationService.Get(
                    "Log.Adb.WakeUpDeviceMissing"));

            return new AdbWakeUpResult(success, true, devicesAfter);
        }

        public static IList<AdbDeviceInfo> ParseDevices(string output)
        {
            var devices = new List<AdbDeviceInfo>();
            if (string.IsNullOrWhiteSpace(output)) return devices;

            var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (line.Length == 0 ||
                    line.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("* daemon", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var columns = line.Split(
                    new[] { '\t', ' ' },
                    StringSplitOptions.RemoveEmptyEntries);
                if (columns.Length < 2) continue;

                devices.Add(new AdbDeviceInfo
                {
                    Serial = columns[0],
                    RawStatus = columns[1],
                    Status = ParseStatus(columns[1])
                });
            }

            return devices;
        }

        private ProcessResult Run(string arguments)
        {
            return Run(arguments, true);
        }

        private ProcessResult Run(string arguments, bool writeLog)
        {
            return Run(arguments, writeLog, _defaultTimeoutMs);
        }

        private ProcessResult Run(
            string arguments,
            bool writeLog,
            int timeoutMs)
        {
            var outputEncoding = string.Equals(
                (arguments ?? string.Empty).Trim(),
                "version",
                StringComparison.OrdinalIgnoreCase)
                ? Encoding.Default
                : Encoding.UTF8;
            return _processRunner.Run(
                _adbPath,
                arguments,
                Path.GetDirectoryName(_adbPath),
                Math.Max(timeoutMs, 1000),
                writeLog,
                outputEncoding);
        }

        private ProcessResult RunForSerial(
            string serial,
            string arguments,
            bool writeLog)
        {
            return Run(
                AdbCommandBuilder.ForDevice(serial, arguments),
                writeLog);
        }

        private ProcessResult RunForSerial(
            string serial,
            string arguments,
            bool writeLog,
            int timeoutMs)
        {
            return Run(
                AdbCommandBuilder.ForDevice(serial, arguments),
                writeLog,
                timeoutMs);
        }

        public static bool IsTcpIpSerial(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial)) return false;

            var value = serial.Trim();
            var separator = value.LastIndexOf(':');
            if (separator <= 0 || separator == value.Length - 1)
                return false;

            int port;
            return int.TryParse(value.Substring(separator + 1), out port) &&
                port > 0 &&
                port <= 65535;
        }

        public static bool IsMdnsConnectSerial(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial)) return false;
            var value = serial.Trim().TrimEnd('.');
            return value.EndsWith(
                "._adb-tls-connect._tcp",
                StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsWirelessSerial(string serial)
        {
            return IsTcpIpSerial(serial) || IsMdnsConnectSerial(serial);
        }

        private static bool ContainsAuthorizedDevice(
            IEnumerable<AdbDeviceInfo> devices,
            string serial)
        {
            foreach (var device in devices ?? Enumerable.Empty<AdbDeviceInfo>())
            {
                if (device == null || !device.IsAuthorized) continue;
                if (string.IsNullOrWhiteSpace(serial) ||
                    string.Equals(
                        device.Serial,
                        serial,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsEmulatorSerial(string serial)
        {
            return !string.IsNullOrWhiteSpace(serial) &&
                serial.Trim().StartsWith(
                    "emulator-",
                    StringComparison.OrdinalIgnoreCase);
        }

        private static void ValidatePort(int port, string parameterName)
        {
            if (port < 1 || port > 65535)
                throw new ArgumentOutOfRangeException(parameterName);
        }

        private static ProcessResult SanitizePairResult(
            ProcessResult result)
        {
            if (result == null) return null;
            return new ProcessResult
            {
                FileName = result.FileName,
                Arguments = "pair <address> <hidden>",
                ExitCode = result.ExitCode,
                StandardOutput = result.StandardOutput,
                StandardError = result.StandardError,
                TimedOut = result.TimedOut,
                Canceled = result.Canceled,
                Duration = result.Duration
            };
        }

        private static AdbDeviceStatus ParseStatus(string status)
        {
            if (string.Equals(status, "device", StringComparison.OrdinalIgnoreCase))
                return AdbDeviceStatus.Device;
            if (string.Equals(status, "unauthorized", StringComparison.OrdinalIgnoreCase))
                return AdbDeviceStatus.Unauthorized;
            if (string.Equals(status, "offline", StringComparison.OrdinalIgnoreCase))
                return AdbDeviceStatus.Offline;
            if (string.Equals(status, "no", StringComparison.OrdinalIgnoreCase))
                return AdbDeviceStatus.NoPermissions;
            return AdbDeviceStatus.Unknown;
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private void LogCommandResult(string title, ProcessResult result)
        {
            if (result == null) return;
            if (result.Canceled && IsProcessShutdownRequested) return;

            var text = !string.IsNullOrWhiteSpace(result.StandardOutput)
                ? result.StandardOutput
                : result.StandardError;
            text = (text ?? string.Empty)
                .Replace("\r", " ")
                .Replace("\n", " | ")
                .Trim();

            var message = title + ": ExitCode=" + result.ExitCode +
                ", Timeout=" + result.TimedOut;
            if (!string.IsNullOrWhiteSpace(text)) message += ", " + text;

            if (result.IsSuccess)
                _logService.Info(message);
            else
                _logService.Warning(message);
        }
    }

    public sealed class AdbWakeUpResult
    {
        public AdbWakeUpResult(
            bool success,
            bool usedScrcpy,
            IList<AdbDeviceInfo> devices)
        {
            Success = success;
            UsedScrcpy = usedScrcpy;
            Devices = devices ?? new List<AdbDeviceInfo>();
        }

        public bool Success { get; private set; }
        public bool UsedScrcpy { get; private set; }
        public IList<AdbDeviceInfo> Devices { get; private set; }
    }
}
