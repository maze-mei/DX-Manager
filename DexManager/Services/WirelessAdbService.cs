using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using DexManager.Models;

namespace DexManager.Services
{
    public sealed class WirelessAdbService
    {
        private readonly AdbService _adbService;
        private readonly SettingsService _settingsService;
        private readonly AppSettings _settings;
        private readonly LogService _logService;
        private readonly object _reconnectSync = new object();
        private DateTime _lastReconnectAttemptUtc = DateTime.MinValue;

        public WirelessAdbService(
            AdbService adbService,
            SettingsService settingsService,
            AppSettings settings,
            LogService logService)
        {
            _adbService = adbService ??
                throw new ArgumentNullException("adbService");
            _settingsService = settingsService ??
                throw new ArgumentNullException("settingsService");
            _settings = settings ??
                throw new ArgumentNullException("settings");
            _logService = logService ??
                throw new ArgumentNullException("logService");
        }

        public bool IsWirelessMode
        {
            get
            {
                return _settings.Connection.Mode ==
                    AdbConnectionMode.Wireless;
            }
        }

        public string SavedEndpoint
        {
            get
            {
                return BuildEndpoint(
                    _settings.Connection.WirelessHost,
                    _settings.Connection.WirelessPort);
            }
        }

        public void InitializeTarget()
        {
            _adbService.SetTargetSerial(
                IsWirelessMode ? SavedEndpoint : string.Empty);
        }

        public AdbDeviceInfo SelectPreferredDevice(
            IList<AdbDeviceInfo> devices)
        {
            var candidates = devices ?? new List<AdbDeviceInfo>();
            if (IsWirelessMode)
            {
                var endpoint = SavedEndpoint;
                _adbService.SetTargetSerial(endpoint);
                return candidates.FirstOrDefault(
                    device => string.Equals(
                        device.Serial,
                        endpoint,
                        StringComparison.OrdinalIgnoreCase));
            }

            var preferred = candidates.FirstOrDefault(
                    device => !AdbService.IsTcpIpSerial(device.Serial) &&
                        device.IsAuthorized) ??
                candidates.FirstOrDefault(
                    device => !AdbService.IsTcpIpSerial(device.Serial));
            _adbService.SetTargetSerial(
                preferred == null ? string.Empty : preferred.Serial);
            return preferred;
        }

        public bool TryReconnect(bool writeLog)
        {
            if (!IsWirelessMode ||
                !_settings.Connection.AutoReconnect ||
                string.IsNullOrWhiteSpace(
                    _settings.Connection.WirelessHost))
            {
                return false;
            }

            lock (_reconnectSync)
            {
                var now = DateTime.UtcNow;
                if ((now - _lastReconnectAttemptUtc).TotalSeconds < 5)
                    return false;
                _lastReconnectAttemptUtc = now;
            }

            var endpoint = SavedEndpoint;
            _adbService.SetTargetSerial(endpoint);
            if (IsConnected(endpoint)) return true;

            if (writeLog)
                _logService.Info("무선 ADB 재연결 시도: " + endpoint);
            var result = _adbService.Connect(endpoint, writeLog);
            var connected = result.IsSuccess && IsConnected(endpoint);
            if (writeLog)
            {
                if (connected)
                    _logService.Info("무선 ADB 재연결 성공: " + endpoint);
                else
                    _logService.Warning(
                        "무선 ADB 재연결 실패: " +
                        GetResultMessage(result));
            }
            return connected;
        }

        public WirelessConnectionResult Connect(
            string host,
            int port)
        {
            var normalizedHost = NormalizeHost(host);
            var endpoint = BuildEndpoint(normalizedHost, port);
            _logService.Info("무선 ADB 연결 시도: " + endpoint);

            _adbService.StartServer();
            var result = _adbService.Connect(endpoint, true);
            if (!result.IsSuccess || !WaitForConnection(endpoint, 3000))
            {
                return WirelessConnectionResult.Failed(
                    "무선 ADB 연결에 실패했습니다: " +
                    GetResultMessage(result));
            }

            _settings.Connection.Mode = AdbConnectionMode.Wireless;
            _settings.Connection.WirelessHost = normalizedHost;
            _settings.Connection.WirelessPort = port;
            _settingsService.Save(_settings);
            _adbService.SetTargetSerial(endpoint);
            _logService.Info("무선 ADB 연결 성공: " + endpoint);
            return WirelessConnectionResult.Succeeded(
                endpoint,
                "무선 연결이 완료되었습니다.");
        }

        public WirelessConnectionResult EnableFromUsb(
            string host,
            int port)
        {
            var devices = _adbService.GetDevices();
            var usbDevices = devices
                .Where(device =>
                    device.IsAuthorized &&
                    !AdbService.IsTcpIpSerial(device.Serial))
                .ToList();
            if (usbDevices.Count != 1)
            {
                return WirelessConnectionResult.Failed(
                    usbDevices.Count == 0
                        ? "승인된 USB 장치를 찾지 못했습니다."
                        : "USB 장치가 여러 개입니다. 하나만 연결하세요.");
            }

            var usbSerial = usbDevices[0].Serial;
            var normalizedHost = string.IsNullOrWhiteSpace(host)
                ? DetectWifiAddress(usbSerial)
                : NormalizeHost(host);
            if (string.IsNullOrWhiteSpace(normalizedHost))
            {
                return WirelessConnectionResult.Failed(
                    "휴대폰 Wi-Fi 주소를 자동으로 찾지 못했습니다. " +
                    "IP 주소를 직접 입력하세요.");
            }

            _logService.Info(
                "USB 장치를 무선 ADB 모드로 전환합니다: " +
                usbSerial +
                ", port=" +
                port);
            var tcpipResult = _adbService.EnableTcpIp(
                usbSerial,
                port);
            if (!tcpipResult.IsSuccess)
            {
                return WirelessConnectionResult.Failed(
                    "휴대폰의 TCP/IP 모드를 켜지 못했습니다: " +
                    GetResultMessage(tcpipResult));
            }

            Thread.Sleep(800);
            return Connect(normalizedHost, port);
        }

        public WirelessConnectionResult Pair(
            string host,
            int port,
            string pairingCode)
        {
            if (!Regex.IsMatch(
                pairingCode ?? string.Empty,
                @"^\d{6}$"))
            {
                return WirelessConnectionResult.Failed(
                    "페어링 코드는 휴대폰에 표시된 6자리 숫자입니다.");
            }
            var endpoint = BuildEndpoint(
                NormalizeHost(host),
                port);
            var result = _adbService.Pair(endpoint, pairingCode);
            if (!result.IsSuccess ||
                !ContainsIgnoreCase(
                    (result.StandardOutput ?? string.Empty) +
                    "\n" +
                    (result.StandardError ?? string.Empty),
                    "successfully paired"))
            {
                return WirelessConnectionResult.Failed(
                    "무선 페어링에 실패했습니다: " +
                    GetResultMessage(result));
            }

            return WirelessConnectionResult.Succeeded(
                endpoint,
                "페어링되었습니다. 휴대폰의 무선 디버깅 화면에 " +
                "표시된 연결 포트로 연결하세요.");
        }

        public WirelessConnectionResult Disconnect()
        {
            var endpoint = SavedEndpoint;
            if (!string.IsNullOrWhiteSpace(endpoint))
                _adbService.Disconnect(endpoint);

            _settings.Connection.Mode = AdbConnectionMode.Usb;
            _settingsService.Save(_settings);
            _adbService.SetTargetSerial(string.Empty);
            _logService.Info("무선 ADB 연결을 해제하고 USB 모드로 전환했습니다.");
            return WirelessConnectionResult.Succeeded(
                string.Empty,
                "무선 연결을 해제했습니다.");
        }

        public void UseUsb()
        {
            _settings.Connection.Mode = AdbConnectionMode.Usb;
            _settingsService.Save(_settings);
            _adbService.SetTargetSerial(string.Empty);
        }

        public static string BuildEndpoint(string host, int port)
        {
            if (string.IsNullOrWhiteSpace(host)) return string.Empty;
            if (port < 1 || port > 65535)
                throw new ArgumentOutOfRangeException("port");

            var value = NormalizeHost(host);
            if (value.StartsWith("[", StringComparison.Ordinal) &&
                value.EndsWith("]", StringComparison.Ordinal))
            {
                return value + ":" + port;
            }
            if (value.IndexOf(':') >= 0)
                return "[" + value + "]:" + port;
            return value + ":" + port;
        }

        private string DetectWifiAddress(string usbSerial)
        {
            var result = _adbService.ShellForSerial(
                usbSerial,
                "ip route",
                true);
            if (!result.IsSuccess) return string.Empty;

            var output = result.StandardOutput ?? string.Empty;
            var wifiRoute = output
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .FirstOrDefault(line =>
                    line.IndexOf(
                        "wlan",
                        StringComparison.OrdinalIgnoreCase) >= 0);
            var wifiMatch = Regex.Match(
                wifiRoute ?? string.Empty,
                @"\bsrc\s+(?<ip>\d{1,3}(?:\.\d{1,3}){3})\b",
                RegexOptions.IgnoreCase);
            if (wifiMatch.Success)
                return wifiMatch.Groups["ip"].Value;

            var sourceMatch = Regex.Match(
                output,
                @"\bsrc\s+(?<ip>\d{1,3}(?:\.\d{1,3}){3})\b",
                RegexOptions.IgnoreCase);
            if (sourceMatch.Success)
                return sourceMatch.Groups["ip"].Value;

            var addressMatch = Regex.Match(
                output,
                @"\b(?<ip>\d{1,3}(?:\.\d{1,3}){3})\b");
            return addressMatch.Success
                ? addressMatch.Groups["ip"].Value
                : string.Empty;
        }

        private bool WaitForConnection(
            string endpoint,
            int timeoutMs)
        {
            var started = Environment.TickCount;
            do
            {
                if (IsConnected(endpoint)) return true;
                Thread.Sleep(150);
            }
            while (unchecked(Environment.TickCount - started) < timeoutMs);
            return false;
        }

        private bool IsConnected(string endpoint)
        {
            return _adbService.GetDevices(false).Any(
                device => device.IsAuthorized &&
                    string.Equals(
                        device.Serial,
                        endpoint,
                        StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizeHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException(
                    "무선 ADB IP 주소가 비어 있습니다.",
                    "host");

            var value = host.Trim();
            if (value.StartsWith("[", StringComparison.Ordinal) &&
                value.EndsWith("]", StringComparison.Ordinal))
            {
                value = value.Substring(1, value.Length - 2);
            }
            if (value.IndexOfAny(new[] { ' ', '\t', '\r', '\n' }) >= 0 ||
                !Regex.IsMatch(value, @"^[A-Za-z0-9._:%-]+$"))
                throw new ArgumentException(
                    "무선 ADB 주소 형식이 올바르지 않습니다.",
                    "host");
            return value;
        }

        private static string GetResultMessage(ProcessResult result)
        {
            if (result == null) return "결과 없음";
            var text = !string.IsNullOrWhiteSpace(result.StandardError)
                ? result.StandardError
                : result.StandardOutput;
            return string.IsNullOrWhiteSpace(text)
                ? "ExitCode=" + result.ExitCode
                : text.Trim();
        }

        private static bool ContainsIgnoreCase(
            string value,
            string text)
        {
            return (value ?? string.Empty).IndexOf(
                text,
                StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    public sealed class WirelessConnectionResult
    {
        private WirelessConnectionResult(
            bool success,
            string endpoint,
            string message)
        {
            Success = success;
            Endpoint = endpoint ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public bool Success { get; private set; }
        public string Endpoint { get; private set; }
        public string Message { get; private set; }

        public static WirelessConnectionResult Succeeded(
            string endpoint,
            string message)
        {
            return new WirelessConnectionResult(
                true,
                endpoint,
                message);
        }

        public static WirelessConnectionResult Failed(string message)
        {
            return new WirelessConnectionResult(
                false,
                string.Empty,
                message);
        }
    }
}
