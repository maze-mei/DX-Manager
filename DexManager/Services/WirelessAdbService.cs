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
        private long _transitionGeneration;

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
                var connection = _settings.Connection;
                return connection != null && connection.Mode ==
                    AdbConnectionMode.Wireless;
            }
        }

        public string SavedEndpoint
        {
            get
            {
                var connection = _settings.Connection;
                if (connection == null) return string.Empty;
                return BuildEndpoint(
                    connection.WirelessHost,
                    connection.WirelessPort);
            }
        }

        public void InitializeTarget()
        {
            SynchronizeTargetWithSettings();
        }

        public void SynchronizeTargetWithSettings()
        {
            lock (_reconnectSync)
            {
                var connection = GetConnectionSnapshot();
                _transitionGeneration++;
                if (connection.Mode == AdbConnectionMode.Wireless)
                {
                    _adbService.SetTargetSerial(connection.Endpoint);
                    return;
                }

                var current = _adbService.TargetSerial;
                if (AdbService.IsTcpIpSerial(current) ||
                    AdbService.IsEmulatorSerial(current))
                {
                    _adbService.SetTargetSerial(string.Empty);
                }
            }
        }

        public AdbDeviceInfo SelectPreferredDevice(
            IList<AdbDeviceInfo> devices)
        {
            return SelectPreferredDeviceWithGeneration(devices).Device;
        }

        public WirelessDeviceSelection SelectPreferredDeviceWithGeneration(
            IList<AdbDeviceInfo> devices)
        {
            return SelectPreferredDeviceWithGeneration(
                devices,
                string.Empty);
        }

        public WirelessDeviceSelection SelectPreferredDeviceWithGeneration(
            IList<AdbDeviceInfo> devices,
            string targetWhenUnavailable)
        {
            lock (_reconnectSync)
            {
                var connection = GetConnectionSnapshot();
                var preferred = FindPreferredDeviceCore(
                    devices,
                    _adbService.TargetSerial,
                    connection);
                var unavailableTarget = string.IsNullOrWhiteSpace(
                    targetWhenUnavailable)
                    ? string.Empty
                    : targetWhenUnavailable.Trim();
                var target = preferred == null
                    ? (unavailableTarget.Length > 0
                        ? unavailableTarget
                        : connection.Mode == AdbConnectionMode.Wireless
                        ? connection.Endpoint
                        : string.Empty)
                    : preferred.Serial;
                _adbService.SetTargetSerial(target);
                return new WirelessDeviceSelection(
                    preferred,
                    _transitionGeneration);
            }
        }

        public bool IsTransitionGenerationCurrent(long generation)
        {
            lock (_reconnectSync)
            {
                return generation == _transitionGeneration;
            }
        }

        public AdbDeviceInfo FindPreferredDevice(
            IList<AdbDeviceInfo> devices,
            string currentSerial)
        {
            lock (_reconnectSync)
            {
                return FindPreferredDeviceCore(
                    devices,
                    currentSerial,
                    GetConnectionSnapshot());
            }
        }

        public bool TryReconnect(bool writeLog)
        {
            lock (_reconnectSync)
            {
                var connection = GetConnectionSnapshot();
                if (connection.Mode != AdbConnectionMode.Wireless ||
                    !connection.AutoReconnect ||
                    string.IsNullOrWhiteSpace(connection.Host))
                {
                    return false;
                }

                var now = DateTime.UtcNow;
                if ((now - _lastReconnectAttemptUtc).TotalSeconds < 5)
                    return false;
                _lastReconnectAttemptUtc = now;

                var endpoint = connection.Endpoint;
                _transitionGeneration++;
                _adbService.SetTargetSerial(endpoint);
                if (IsConnected(endpoint)) return true;

                if (writeLog)
                    _logService.Info(LocalizationService.Format(
                        "Log.Wireless.ReconnectAttempt",
                        endpoint));
                var result = _adbService.Connect(endpoint, writeLog);
                var connected = result.IsSuccess && IsConnected(endpoint);
                if (writeLog)
                {
                    if (connected)
                        _logService.Info(LocalizationService.Format(
                            "Log.Wireless.ReconnectSucceeded",
                            endpoint));
                    else
                        _logService.Warning(LocalizationService.Format(
                            "Log.Wireless.ReconnectFailed",
                            GetResultMessage(result)));
                }
                return connected;
            }
        }

        public WirelessConnectionResult Connect(
            string host,
            int port)
        {
            lock (_reconnectSync)
            {
                _transitionGeneration++;
                var normalizedHost = NormalizeHost(host);
                var endpoint = BuildEndpoint(normalizedHost, port);
                _logService.Info(LocalizationService.Format(
                    "Log.Wireless.ConnectAttempt",
                    endpoint));

                _adbService.StartServer();
                var wasConnected = IsConnected(endpoint);
                var previousTarget = _adbService.TargetSerial;
                var result = _adbService.Connect(endpoint, true);
                if (!result.IsSuccess || !WaitForConnection(endpoint, 3000))
                {
                    RollbackConnection(
                        endpoint,
                        wasConnected,
                        previousTarget);
                    return WirelessConnectionResult.Failed(
                        LocalizationService.Format(
                            "Wireless.ConnectFailed",
                            GetResultMessage(result)));
                }

                try
                {
                    _settingsService.UpdateAndSave(_settings, delegate(
                        AppSettings settings)
                    {
                        settings.Connection.Mode = AdbConnectionMode.Wireless;
                        settings.Connection.WirelessHost = normalizedHost;
                        settings.Connection.WirelessPort = port;
                    });
                }
                catch
                {
                    RollbackConnection(
                        endpoint,
                        wasConnected,
                        previousTarget);
                    throw;
                }
                _adbService.SetTargetSerial(endpoint);
                _logService.Info(LocalizationService.Format(
                    "Log.Wireless.ConnectSucceeded",
                    endpoint));
                return WirelessConnectionResult.Succeeded(
                    endpoint,
                    LocalizationService.Get("Wireless.Connected"));
            }
        }

        public WirelessConnectionResult EnableFromUsb(
            string host,
            int port)
        {
            lock (_reconnectSync)
            {
                var devices = _adbService.GetDevices();
                var usbDevices = devices
                    .Where(device =>
                        device.IsAuthorized &&
                        !AdbService.IsTcpIpSerial(device.Serial) &&
                        !AdbService.IsEmulatorSerial(device.Serial))
                    .ToList();
                if (usbDevices.Count != 1)
                {
                    return WirelessConnectionResult.Failed(
                        usbDevices.Count == 0
                            ? LocalizationService.Get("Wireless.NoUsb")
                            : LocalizationService.Get(
                                "Wireless.MultipleUsb"));
                }

                var usbSerial = usbDevices[0].Serial;
                var normalizedHost = string.IsNullOrWhiteSpace(host)
                    ? DetectWifiAddress(usbSerial)
                    : NormalizeHost(host);
                if (string.IsNullOrWhiteSpace(normalizedHost))
                {
                    return WirelessConnectionResult.Failed(
                        LocalizationService.Get("Wireless.NoWifiIp"));
                }

                _logService.Info(LocalizationService.Format(
                    "Log.Wireless.EnableFromUsb",
                    usbSerial,
                    port));
                var tcpipResult = _adbService.EnableTcpIp(
                    usbSerial,
                    port);
                if (!tcpipResult.IsSuccess)
                {
                    return WirelessConnectionResult.Failed(
                        LocalizationService.Format(
                            "Wireless.TcpipFailed",
                            GetResultMessage(tcpipResult)));
                }

                Thread.Sleep(800);
                return Connect(normalizedHost, port);
            }
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
                    LocalizationService.Get(
                        "Wireless.InvalidPairCode"));
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
                    LocalizationService.Format(
                        "Wireless.PairFailed",
                        GetResultMessage(result)));
            }

            return WirelessConnectionResult.Succeeded(
                endpoint,
                LocalizationService.Get("Wireless.Paired"));
        }

        public WirelessConnectionResult Disconnect()
        {
            lock (_reconnectSync)
            {
                _transitionGeneration++;
                var connection = GetConnectionSnapshot();
                var endpoint = connection.Endpoint;
                _settingsService.UpdateAndSave(_settings, delegate(
                    AppSettings settings)
                {
                    settings.Connection.Mode = AdbConnectionMode.Usb;
                });
                ProcessResult disconnectResult = null;
                if (!string.IsNullOrWhiteSpace(endpoint))
                    disconnectResult = _adbService.Disconnect(endpoint);
                _adbService.SetTargetSerial(string.Empty);
                if (disconnectResult != null && !disconnectResult.IsSuccess)
                {
                    _logService.Warning(LocalizationService.Format(
                        "Log.Wireless.DisconnectCommandFailed",
                        GetResultMessage(disconnectResult)));
                }
                _logService.Info(LocalizationService.Get(
                    "Log.Wireless.Disconnected"));
                return WirelessConnectionResult.Succeeded(
                    string.Empty,
                    LocalizationService.Get("Wireless.Disconnected"));
            }
        }

        public void UseUsb()
        {
            lock (_reconnectSync)
            {
                _transitionGeneration++;
                _settingsService.UpdateAndSave(_settings, delegate(
                    AppSettings settings)
                {
                    settings.Connection.Mode = AdbConnectionMode.Usb;
                });
                _adbService.SetTargetSerial(string.Empty);
            }
        }

        private AdbDeviceInfo FindPreferredDeviceCore(
            IList<AdbDeviceInfo> devices,
            string currentSerial,
            ConnectionSnapshot connection)
        {
            var candidates = devices ?? new List<AdbDeviceInfo>();
            if (connection.Mode == AdbConnectionMode.Wireless)
            {
                return candidates.FirstOrDefault(
                    device => string.Equals(
                        device.Serial,
                        connection.Endpoint,
                        StringComparison.OrdinalIgnoreCase) &&
                        device.IsAuthorized);
            }

            var current = candidates.FirstOrDefault(device =>
                string.Equals(
                    device.Serial,
                    currentSerial,
                    StringComparison.OrdinalIgnoreCase) &&
                !AdbService.IsTcpIpSerial(device.Serial) &&
                !AdbService.IsEmulatorSerial(device.Serial) &&
                device.IsAuthorized);
            if (current != null) return current;

            return candidates.FirstOrDefault(
                    device => !AdbService.IsTcpIpSerial(device.Serial) &&
                        !AdbService.IsEmulatorSerial(device.Serial) &&
                        device.IsAuthorized) ??
                candidates.FirstOrDefault(
                    device => !AdbService.IsTcpIpSerial(device.Serial) &&
                        !AdbService.IsEmulatorSerial(device.Serial));
        }

        private void RollbackConnection(
            string endpoint,
            bool wasConnected,
            string previousTarget)
        {
            try
            {
                if (!wasConnected)
                {
                    var result = _adbService.Disconnect(endpoint);
                    if (!result.IsSuccess)
                    {
                        _logService.Warning(LocalizationService.Format(
                            "Log.Wireless.DisconnectCommandFailed",
                            GetResultMessage(result)));
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.Warning(LocalizationService.Format(
                    "Log.Wireless.DisconnectCommandFailed",
                    ex.Message));
            }
            finally
            {
                _adbService.SetTargetSerial(previousTarget);
            }
        }

        private ConnectionSnapshot GetConnectionSnapshot()
        {
            var connection = _settings.Connection;
            if (connection == null)
            {
                return new ConnectionSnapshot(
                    AdbConnectionMode.Usb,
                    false,
                    string.Empty,
                    5555);
            }
            return new ConnectionSnapshot(
                connection.Mode,
                connection.AutoReconnect,
                connection.WirelessHost,
                connection.WirelessPort);
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
                    LocalizationService.Get("Wireless.HostEmpty"),
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
                    LocalizationService.Get("Wireless.HostInvalid"),
                    "host");
            return value;
        }

        private static string GetResultMessage(ProcessResult result)
        {
            if (result == null)
                return LocalizationService.Get(
                    "Wireless.NoResult");
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

        private sealed class ConnectionSnapshot
        {
            public ConnectionSnapshot(
                AdbConnectionMode mode,
                bool autoReconnect,
                string host,
                int port)
            {
                Mode = mode;
                AutoReconnect = autoReconnect;
                Host = host ?? string.Empty;
                Port = port;
                Endpoint = string.Empty;
                if (Mode == AdbConnectionMode.Wireless &&
                    !string.IsNullOrWhiteSpace(Host))
                {
                    Endpoint = BuildEndpoint(Host, Port);
                }
            }

            public AdbConnectionMode Mode { get; private set; }
            public bool AutoReconnect { get; private set; }
            public string Host { get; private set; }
            public int Port { get; private set; }
            public string Endpoint { get; private set; }
        }
    }

    public sealed class WirelessDeviceSelection
    {
        internal WirelessDeviceSelection(
            AdbDeviceInfo device,
            long transitionGeneration)
        {
            Device = device;
            TransitionGeneration = transitionGeneration;
        }

        public AdbDeviceInfo Device { get; private set; }
        public long TransitionGeneration { get; private set; }
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
