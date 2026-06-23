using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public AdbService(
            string adbPath,
            int defaultTimeoutMs,
            ProcessRunner processRunner,
            LogService logService)
        {
            if (string.IsNullOrWhiteSpace(adbPath))
                throw new ArgumentException("ADB 경로가 비어 있습니다.", "adbPath");

            _adbPath = Path.GetFullPath(adbPath);
            _defaultTimeoutMs = Math.Max(defaultTimeoutMs, 1000);
            _processRunner = processRunner;
            _logService = logService;
        }

        public string AdbPath
        {
            get { return _adbPath; }
        }

        public ProcessResult StartServer()
        {
            var result = Run("start-server");
            LogCommandResult("ADB start-server 결과", result);
            return result;
        }

        public ProcessResult GetVersion()
        {
            return Run("version");
        }

        public void LogStartupDiagnostics()
        {
            _logService.Info("선택된 ADB 경로: " + _adbPath);
            LogCommandResult("ADB version 결과", GetVersion());
        }

        public ProcessResult KillServer()
        {
            return Run("kill-server");
        }

        public ProcessResult GetState()
        {
            return Run("get-state");
        }

        public ProcessResult Shell(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                throw new ArgumentException("ADB shell 명령이 비어 있습니다.", "command");
            return Shell(command, true);
        }

        public ProcessResult Shell(string command, bool writeLog)
        {
            if (string.IsNullOrWhiteSpace(command))
                throw new ArgumentException("ADB shell command is empty.", "command");
            return Run("shell " + command, writeLog);
        }

        public ProcessResult Push(string localPath, string remotePath)
        {
            if (!File.Exists(localPath))
                throw new FileNotFoundException("전송할 파일을 찾을 수 없습니다.", localPath);
            if (string.IsNullOrWhiteSpace(remotePath))
                throw new ArgumentException("스마트폰 대상 경로가 비어 있습니다.", "remotePath");

            return Run("push " + Quote(localPath) + " " + Quote(remotePath));
        }

        public IList<AdbDeviceInfo> GetDevices()
        {
            return GetDevices(true);
        }

        public IList<AdbDeviceInfo> GetDevices(bool writeLog)
        {
            var result = Run("devices", writeLog);
            var devices = ParseDevices(result.StandardOutput);

            if (writeLog)
            {
                LogCommandResult("ADB devices 결과", result);
                _logService.Info("ADB 장치 조회 결과: " + devices.Count + "개");
            }
            return devices;
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

        public AdbWakeUpResult WakeUp(Func<bool> scrcpyWakeUp)
        {
            _logService.Info("ADB Wake-up을 시작합니다.");
            KillServer();
            StartServer();

            var devicesBefore = GetDevices();
            if (IsAuthorizedDeviceConnected())
            {
                return new AdbWakeUpResult(true, false, devicesBefore);
            }

            if (scrcpyWakeUp == null)
            {
                _logService.Warning("ADB Wake-up 실패: Scrcpy Wake-up이 구성되지 않았습니다.");
                return new AdbWakeUpResult(false, false, devicesBefore);
            }

            _logService.Warning("기본 ADB Wake-up 실패. Scrcpy Wake-up을 실행합니다.");
            var scrcpyStarted = scrcpyWakeUp();
            var devicesAfter = GetDevices();
            var success = scrcpyStarted && IsAuthorizedDeviceConnected();

            if (success)
                _logService.Info("Scrcpy Wake-up 후 ADB 장치를 확인했습니다.");
            else
                _logService.Warning("Scrcpy Wake-up 후에도 ADB 장치를 확인하지 못했습니다.");

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
            return _processRunner.Run(
                _adbPath,
                arguments,
                Path.GetDirectoryName(_adbPath),
                _defaultTimeoutMs,
                writeLog);
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
