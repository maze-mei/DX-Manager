using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DexManager.Models;
using DexManager.Utils;

namespace DexManager.Services
{
    public sealed class AdbService
    {
        private readonly string _adbPath;
        private readonly int _defaultTimeoutMs;
        private readonly ProcessRunner _processRunner;
        private readonly LogService _logService;
        private readonly object _targetSync = new object();
        private string _targetSerial;

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

        public string TargetSerial
        {
            get
            {
                lock (_targetSync)
                {
                    return _targetSerial;
                }
            }
        }

        public void SetTargetSerial(string serial)
        {
            var normalized = string.IsNullOrWhiteSpace(serial)
                ? string.Empty
                : serial.Trim();
            lock (_targetSync)
            {
                if (string.Equals(
                    _targetSerial,
                    normalized,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
                _targetSerial = normalized;
                Environment.SetEnvironmentVariable(
                    "ANDROID_SERIAL",
                    normalized.Length == 0 ? null : normalized,
                    EnvironmentVariableTarget.Process);
            }
            _logService.Info(
                normalized.Length == 0
                    ? LocalizationService.Get(
                        "Log.Adb.TargetCleared")
                    : LocalizationService.Format(
                        "Log.Adb.TargetSelected",
                        normalized));
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

        public ProcessResult GetState()
        {
            return RunTargeted("get-state", true);
        }

        public ProcessResult GetState(string serial)
        {
            return RunForSerial(serial, "get-state", true);
        }

        public ProcessResult Shell(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                throw new ArgumentException(
                    LocalizationService.Get("Error.Adb.ShellCommandEmpty"),
                    "command");
            return Shell(command, true);
        }

        public ProcessResult Shell(string command, bool writeLog)
        {
            if (string.IsNullOrWhiteSpace(command))
                throw new ArgumentException("ADB shell command is empty.", "command");
            return RunTargeted("shell " + command, writeLog);
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

        public ProcessResult Push(string localPath, string remotePath)
        {
            ValidatePushPaths(localPath, remotePath);
            return RunTargeted(
                "push " + Quote(localPath) + " " + Quote(remotePath),
                true);
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
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException(
                    LocalizationService.Get(
                        "Error.Adb.WirelessEndpointEmpty"),
                    "endpoint");
            return Run("connect " + Quote(endpoint.Trim()), writeLog);
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

        public bool IsAuthorizedDeviceConnected()
        {
            var state = GetState();
            return state.IsSuccess &&
                string.Equals(
                    state.StandardOutput.Trim(),
                    "device",
                    StringComparison.OrdinalIgnoreCase);
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

        public AdbWakeUpResult WakeUp(Func<bool> scrcpyWakeUp)
        {
            _logService.Info(
                LocalizationService.Get("Log.Adb.WakeUpStarting"));
            if (!IsTcpIpSerial(TargetSerial))
                KillServer();
            StartServer();

            var devicesBefore = GetDevices();
            if (IsAuthorizedDeviceConnected())
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
            var scrcpyStarted = scrcpyWakeUp();
            var devicesAfter = GetDevices();
            var success = scrcpyStarted && IsAuthorizedDeviceConnected();

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
                _defaultTimeoutMs,
                writeLog,
                outputEncoding);
        }

        private ProcessResult RunTargeted(
            string arguments,
            bool writeLog)
        {
            var serial = TargetSerial;
            return string.IsNullOrWhiteSpace(serial)
                ? Run(arguments, writeLog)
                : RunForSerial(serial, arguments, writeLog);
        }

        private ProcessResult RunForSerial(
            string serial,
            string arguments,
            bool writeLog)
        {
            if (string.IsNullOrWhiteSpace(serial))
                throw new ArgumentException(
                    LocalizationService.Get("Error.Adb.SerialEmpty"),
                    "serial");
            return Run(
                "-s " + Quote(serial.Trim()) + " " + arguments,
                writeLog);
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
