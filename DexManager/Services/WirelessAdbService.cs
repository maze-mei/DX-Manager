using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly object _stateSync = new object();
        private DateTime _nextReconnectAttemptUtc = DateTime.MinValue;
        private long _transitionGeneration;
        private string _selectedSerial = string.Empty;
        private string _verifiedMdnsSerial = string.Empty;
        private string _verifiedMdnsIdentity = string.Empty;

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
                var profiles =
                    _settings.DeviceWirelessConnectionProfiles;
                if (profiles != null && profiles.Count > 0)
                {
                    return profiles.Any(profile =>
                        profile != null &&
                        profile.Mode == AdbConnectionMode.Wireless);
                }
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

        public string SelectedSerial
        {
            get
            {
                lock (_stateSync)
                    return _selectedSerial;
            }
        }

        public void InitializeTarget()
        {
            SynchronizeTargetWithSettings();
        }

        public void SynchronizeTargetWithSettings()
        {
            lock (_stateSync)
            {
                var connection = GetConnectionSnapshot();
                AdvanceTransitionGeneration();
                if (connection.Mode == AdbConnectionMode.Wireless)
                {
                    SetSelectedSerial(connection.Endpoint);
                    return;
                }

                var current = _selectedSerial;
                if (AdbService.IsWirelessSerial(current) ||
                    AdbService.IsEmulatorSerial(current))
                {
                    SetSelectedSerial(string.Empty);
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
                ConnectionSnapshot connection;
                string currentSerial;
                long generation;
                lock (_stateSync)
                {
                    connection = GetConnectionSnapshot();
                    currentSerial = _selectedSerial;
                    generation = _transitionGeneration;
                }
                var preferred = FindPreferredDeviceCore(
                    devices,
                    currentSerial,
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
                lock (_stateSync)
                {
                    if (generation != _transitionGeneration)
                        return new WirelessDeviceSelection(null, generation);
                    SetSelectedSerial(target);
                    return new WirelessDeviceSelection(preferred, generation);
                }
            }
        }

        public bool IsTransitionGenerationCurrent(long generation)
        {
            lock (_stateSync)
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
            return TryReconnect(writeLog, null);
        }

        public bool TryReconnect(
            bool writeLog,
            DeviceRegistrySnapshot physicalDevices)
        {
            lock (_reconnectSync)
            {
                long generation;
                lock (_stateSync)
                {
                    if (DateTime.UtcNow < _nextReconnectAttemptUtc) return false;
                    generation = _transitionGeneration;
                }
                var connections = GetReconnectSnapshots();
                if (connections.Count == 0) return false;
                var duration = Stopwatch.StartNew();
                var connected = false;
                try
                {
                    connected = TryReconnectCore(writeLog, physicalDevices, connections);
                    return connected;
                }
                finally
                {
                    lock (_stateSync)
                    {
                        if (generation == _transitionGeneration)
                            _nextReconnectAttemptUtc = DateTime.UtcNow.AddSeconds(5);
                    }
                    if (!connected)
                        _logService.Warning("Wireless auto reconnect: no connected device; duration=" +
                            duration.ElapsedMilliseconds + "ms; retry cooldown=5s.");
                }
            }
        }

        private bool TryReconnectCore(bool writeLog, DeviceRegistrySnapshot physicalDevices,
            IList<ConnectionSnapshot> connections)
        {
                var devices = _adbService.GetDevices(false);
                IList<AdbMdnsServiceRecord> mdnsServices = null;
                var mdnsLookupAttempted = false;
                var anyConnected = false;
                foreach (var connection in connections)
                {
                    var endpoint = connection.Endpoint;
                    if (string.IsNullOrWhiteSpace(endpoint)) continue;

                    if (IsStableDeviceIdentity(connection.DeviceIdentity))
                    {
                        var connectedForIdentity =
                            FindConnectedWirelessDevice(
                                physicalDevices,
                                devices,
                                connection.DeviceIdentity);
                        if (connectedForIdentity != null)
                        {
                            anyConnected = true;
                            RefreshConnectedWirelessProfile(
                                connection,
                                connectedForIdentity,
                                ref mdnsServices,
                                ref mdnsLookupAttempted);
                            continue;
                        }
                    }

                    var savedEndpointDevice = FindAuthorizedDevice(
                        devices,
                        endpoint);
                    if (savedEndpointDevice != null)
                    {
                        if (!IsStableDeviceIdentity(connection.DeviceIdentity) ||
                            VerifyDeviceIdentity(
                                connection.DeviceIdentity,
                                savedEndpointDevice.Serial))
                        {
                            anyConnected = true;
                            continue;
                        }
                        if (writeLog)
                        {
                            _logService.Warning(LocalizationService.Format(
                                "Log.Wireless.DeviceMismatch",
                                endpoint));
                        }
                    }
                    else
                    {
                        if (writeLog)
                            _logService.Info(LocalizationService.Format(
                                "Log.Wireless.ReconnectAttempt",
                                endpoint));
                        var result = _adbService.Connect(endpoint, writeLog, 4000);
                        var connectedSerial = result.IsSuccess ? WaitForEndpoint(endpoint, 3000) : string.Empty;
                        if (!string.IsNullOrWhiteSpace(connectedSerial))
                        {
                            if (!IsStableDeviceIdentity(
                                    connection.DeviceIdentity) ||
                                VerifyDeviceIdentity(
                                    connection.DeviceIdentity,
                                    connectedSerial))
                            {
                                anyConnected = true;
                                devices = _adbService.GetDevices(false);
                                continue;
                            }
                            RollbackDeviceConnection(endpoint, false);
                        }
                        if (writeLog && string.IsNullOrWhiteSpace(
                            connectedSerial))
                        {
                            _logService.Warning(LocalizationService.Format(
                                "Log.Wireless.ReconnectFailed",
                                GetResultMessage(result)));
                        }
                    }

                    if (!IsStableDeviceIdentity(connection.DeviceIdentity))
                        continue;

                    if (!mdnsLookupAttempted)
                    {
                        mdnsLookupAttempted = true;
                        mdnsServices = _adbService.GetMdnsServices(
                            false,
                            2000);
                    }
                    var service = WirelessMdnsResolver
                        .ResolveEndpointForIdentity(
                            connection.DeviceIdentity,
                            endpoint,
                            mdnsServices);
                    var discoveredEndpoint =
                        WirelessMdnsResolver.BuildEndpoint(service);
                    if (discoveredEndpoint.Length == 0 ||
                        string.Equals(
                            discoveredEndpoint,
                            endpoint,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        if (writeLog)
                        {
                            _logService.Info(LocalizationService.Format(
                                "Log.Wireless.MdnsNoMatch",
                                connection.DeviceIdentity,
                                endpoint));
                        }
                        continue;
                    }

                    var alreadyConnected = FindAuthorizedDevice(
                        devices,
                        discoveredEndpoint) != null;
                    var connectResult = _adbService.Connect(
                        discoveredEndpoint,
                        writeLog, 4000);
                    var connectedMdnsSerial = connectResult.IsSuccess ? WaitForMdnsConnection(
                        discoveredEndpoint,
                        service,
                        3000) : string.Empty;
                    if (string.IsNullOrWhiteSpace(connectedMdnsSerial))
                    {
                        if (!alreadyConnected)
                        {
                            RollbackDeviceConnection(
                                discoveredEndpoint,
                                false);
                        }
                        if (writeLog)
                        {
                            _logService.Warning(LocalizationService.Format(
                                "Log.Wireless.ReconnectFailed",
                                GetResultMessage(connectResult)));
                        }
                        continue;
                    }

                    if (!VerifyDeviceIdentity(
                        connection.DeviceIdentity,
                        connectedMdnsSerial))
                    {
                        if (!alreadyConnected && string.Equals(
                            connectedMdnsSerial,
                            discoveredEndpoint,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            RollbackDeviceConnection(
                                discoveredEndpoint,
                                false);
                        }
                        if (writeLog)
                        {
                            _logService.Warning(LocalizationService.Format(
                                "Log.Wireless.DeviceMismatch",
                                discoveredEndpoint));
                        }
                        continue;
                    }

                    if (!SaveDiscoveredWirelessEndpoint(
                        connection.DeviceIdentity,
                        service.Host,
                        service.Port,
                        connection.AutoReconnect,
                        IsCurrentWirelessProfile(
                            connection.DeviceIdentity), connection)) continue;
                    _logService.Info(LocalizationService.Format(
                        "Log.Wireless.MdnsEndpointUpdated",
                        connection.DeviceIdentity,
                        endpoint,
                        discoveredEndpoint));
                    SetSelectedSerialForVerifiedDevice(
                        connection.DeviceIdentity,
                        connectedMdnsSerial);
                    anyConnected = true;
                    devices = _adbService.GetDevices(false);
                    if (writeLog)
                    {
                        _logService.Info(LocalizationService.Format(
                            "Log.Wireless.ReconnectSucceeded",
                            discoveredEndpoint));
                    }
                }
                return anyConnected;
        }

        private AdbDeviceInfo FindConnectedWirelessDevice(
            DeviceRegistrySnapshot physicalDevices,
            IList<AdbDeviceInfo> devices,
            string expectedIdentity)
        {
            return WirelessMdnsResolver.FindVerifiedConnectedDevice(
                expectedIdentity, physicalDevices,
                (devices ?? new List<AdbDeviceInfo>()).Where(device =>
                    device != null && AdbService.IsWirelessSerial(device.Serial)).ToList(),
                _adbService.GetDeviceIdentity);
        }

        private static AdbDeviceInfo FindAuthorizedDevice(
            IList<AdbDeviceInfo> devices,
            string serial)
        {
            return (devices ?? new List<AdbDeviceInfo>()).FirstOrDefault(
                device => device != null && device.IsAuthorized &&
                    string.Equals(
                        device.Serial,
                        serial,
                        StringComparison.OrdinalIgnoreCase));
        }

        private bool VerifyDeviceIdentity(
            string expectedIdentity,
            string serial)
        {
            if (!IsStableDeviceIdentity(expectedIdentity) ||
                string.IsNullOrWhiteSpace(serial))
            {
                return false;
            }
            var actual = _adbService.GetDeviceIdentity(serial);
            return IsStableDeviceIdentity(actual) &&
                string.Equals(
                    expectedIdentity,
                    actual,
                    StringComparison.OrdinalIgnoreCase);
        }

        private string WaitForEndpoint(string endpoint, int timeoutMs)
        {
            var started = Environment.TickCount;
            do
            {
                var device = FindAuthorizedDevice(
                    _adbService.GetDevices(false),
                    endpoint);
                if (device != null) return device.Serial;
                Thread.Sleep(150);
            }
            while (unchecked(Environment.TickCount - started) < timeoutMs);
            return string.Empty;
        }

        private string WaitForMdnsConnection(
            string endpoint,
            AdbMdnsServiceRecord service,
            int timeoutMs)
        {
            var mdnsSerial = WirelessMdnsResolver.BuildMdnsSerial(service);
            var started = Environment.TickCount;
            do
            {
                var devices = _adbService.GetDevices(false);
                var device = FindAuthorizedDevice(devices, endpoint) ??
                    FindAuthorizedDevice(devices, mdnsSerial);
                if (device != null) return device.Serial;
                Thread.Sleep(150);
            }
            while (unchecked(Environment.TickCount - started) < timeoutMs);
            return string.Empty;
        }

        private void RefreshConnectedWirelessProfile(
            ConnectionSnapshot connection,
            AdbDeviceInfo device,
            ref IList<AdbMdnsServiceRecord> mdnsServices,
            ref bool mdnsLookupAttempted)
        {
            if (connection == null || device == null ||
                !IsStableDeviceIdentity(connection.DeviceIdentity))
            {
                return;
            }

            string host;
            int port;
            if (TrySplitEndpoint(device.Serial, out host, out port))
            {
                var connectedEndpoint = BuildEndpoint(host, port);
                if (!string.Equals(
                    connectedEndpoint,
                    connection.Endpoint,
                    StringComparison.OrdinalIgnoreCase))
                {
                    if (!SaveDiscoveredWirelessEndpoint(
                        connection.DeviceIdentity,
                        host,
                        port,
                        connection.AutoReconnect,
                        IsCurrentWirelessProfile(
                            connection.DeviceIdentity), connection)) return;
                    _logService.Info(LocalizationService.Format(
                        "Log.Wireless.MdnsEndpointUpdated",
                        connection.DeviceIdentity,
                        connection.Endpoint,
                        connectedEndpoint));
                }
                SetSelectedSerialForVerifiedDevice(
                    connection.DeviceIdentity,
                    device.Serial);
                return;
            }

            if (!AdbService.IsMdnsConnectSerial(device.Serial)) return;
            if (!mdnsLookupAttempted)
            {
                mdnsLookupAttempted = true;
                mdnsServices = _adbService.GetMdnsServices(false, 2000);
            }
            var service = WirelessMdnsResolver.ResolveConnectedService(
                device.Serial,
                mdnsServices);
            if (service != null)
            {
                var endpoint = WirelessMdnsResolver.BuildEndpoint(service);
                if (endpoint.Length > 0 && !string.Equals(
                    endpoint,
                    connection.Endpoint,
                    StringComparison.OrdinalIgnoreCase))
                {
                    if (!SaveDiscoveredWirelessEndpoint(
                        connection.DeviceIdentity,
                        service.Host,
                        service.Port,
                        connection.AutoReconnect,
                        IsCurrentWirelessProfile(
                            connection.DeviceIdentity), connection)) return;
                    _logService.Info(LocalizationService.Format(
                        "Log.Wireless.MdnsEndpointUpdated",
                        connection.DeviceIdentity,
                        connection.Endpoint,
                        endpoint));
                }
            }
            SetSelectedSerialForVerifiedDevice(
                connection.DeviceIdentity,
                device.Serial);
        }

        private bool IsCurrentWirelessProfile(string identity)
        {
            return string.Equals(
                GetConnectionSnapshot().DeviceIdentity,
                identity,
                StringComparison.OrdinalIgnoreCase);
        }

        private void SetSelectedSerialForVerifiedDevice(
            string identity,
            string serial)
        {
            lock (_stateSync)
            {
            if (!IsCurrentWirelessProfile(identity)) return;
            if (AdbService.IsMdnsConnectSerial(serial))
            {
                _verifiedMdnsIdentity = identity;
                _verifiedMdnsSerial = serial;
            }
            else
            {
                _verifiedMdnsIdentity = string.Empty;
                _verifiedMdnsSerial = string.Empty;
            }
            SetSelectedSerial(serial);
            }
        }

        private bool SaveDiscoveredWirelessEndpoint(
            string identity,
            string host,
            int port,
            bool autoReconnect,
            bool updateCurrentConnection,
            ConnectionSnapshot expectedProfile = null)
        {
            if (!IsStableDeviceIdentity(identity)) return false;
            var normalizedHost = NormalizeHost(host);
            var applied = false;
            _settingsService.UpdateAndSave(_settings, delegate(
                AppSettings settings)
            {
                if (expectedProfile != null && !WirelessMdnsResolver.CanRefreshProfile(
                    settings.FindDeviceWirelessConnection(identity),
                    expectedProfile.Host, expectedProfile.Port)) return;
                var profile = settings.GetOrCreateDeviceWirelessConnection(
                    identity,
                    false);
                profile.Mode = AdbConnectionMode.Wireless;
                profile.WirelessHost = normalizedHost;
                profile.WirelessPort = port;
                if (expectedProfile == null) profile.AutoReconnect = autoReconnect;
                applied = true;
                if (updateCurrentConnection && settings.Connection != null &&
                    (expectedProfile == null || WirelessMdnsResolver.CanRefreshConnection(
                        settings.Connection, expectedProfile.Host, expectedProfile.Port)))
                {
                    settings.Connection.Mode = AdbConnectionMode.Wireless;
                    settings.Connection.WirelessHost = normalizedHost;
                    settings.Connection.WirelessPort = port;
                    settings.Connection.AutoReconnect = profile.AutoReconnect;
                }
            });
            return applied;
        }

        private string ResolveMdnsEndpointForIdentity(
            string identity,
            string savedEndpoint)
        {
            if (!IsStableDeviceIdentity(identity))
                return string.Empty;
            var services = _adbService.GetMdnsServices(false, 2000);
            var service = WirelessMdnsResolver.ResolveEndpointForIdentity(
                identity,
                savedEndpoint,
                services);
            return WirelessMdnsResolver.BuildEndpoint(service);
        }

        private static bool TrySplitEndpoint(
            string value,
            out string host,
            out int port)
        {
            host = string.Empty;
            port = 0;
            if (!AdbService.IsTcpIpSerial(value)) return false;
            var endpoint = (value ?? string.Empty).Trim();
            if (endpoint.StartsWith("[", StringComparison.Ordinal))
            {
                var close = endpoint.IndexOf("]:", StringComparison.Ordinal);
                if (close <= 1 || !int.TryParse(
                    endpoint.Substring(close + 2), out port))
                    return false;
                host = endpoint.Substring(1, close - 1);
            }
            else
            {
                var separator = endpoint.LastIndexOf(':');
                if (separator <= 0 || !int.TryParse(
                    endpoint.Substring(separator + 1), out port))
                    return false;
                host = endpoint.Substring(0, separator);
            }
            return host.Length > 0 && port > 0 && port <= 65535;
        }

        public DeviceWirelessConnectionProfile GetDeviceProfile(
            string deviceIdentity,
            bool seedFromLegacyConnection)
        {
            lock (_stateSync)
            {
                return _settings.GetOrCreateDeviceWirelessConnection(
                    deviceIdentity,
                    seedFromLegacyConnection);
            }
        }

        public WirelessConnectionResult ConnectForDevice(
            string deviceIdentity,
            string host,
            int port,
            bool autoReconnect)
        {
            return ConnectForDeviceCore(
                deviceIdentity,
                host,
                port,
                autoReconnect,
                true);
        }

        private WirelessConnectionResult ConnectForDeviceCore(
            string deviceIdentity,
            string host,
            int port,
            bool autoReconnect,
            bool allowMdnsFallback)
        {
            var identity = NormalizeDeviceIdentity(deviceIdentity);
            lock (_reconnectSync)
            {
                var normalizedHost = NormalizeHost(host);
                var endpoint = BuildEndpoint(normalizedHost, port);
                _logService.Info(LocalizationService.Format(
                    "Log.Wireless.DeviceConnectAttempt",
                    identity,
                    endpoint));

                _adbService.StartServer();
                var wasConnected = IsConnected(endpoint);
                var result = _adbService.Connect(endpoint, true);
                var connectedSerial = result.IsSuccess
                    ? WaitForEndpoint(endpoint, 3000)
                    : string.Empty;
                if (string.IsNullOrWhiteSpace(connectedSerial))
                {
                    RollbackDeviceConnection(endpoint, wasConnected);
                    var replacement = allowMdnsFallback
                        ? ResolveMdnsEndpointForIdentity(identity, endpoint)
                        : string.Empty;
                    if (!string.IsNullOrWhiteSpace(replacement) &&
                        !string.Equals(
                            replacement,
                            endpoint,
                            StringComparison.OrdinalIgnoreCase) &&
                        TrySplitEndpoint(
                            replacement,
                            out var replacementHost,
                            out var replacementPort))
                    {
                        return ConnectForDeviceCore(
                            identity,
                            replacementHost,
                            replacementPort,
                            autoReconnect,
                            false);
                    }
                    return WirelessConnectionResult.Failed(
                        LocalizationService.Format(
                            "Wireless.ConnectFailed",
                            GetResultMessage(result)));
                }

                var connectedIdentity =
                    _adbService.GetDeviceIdentity(connectedSerial);
                if (!DeviceIdentityMatches(identity, connectedIdentity))
                {
                    RollbackDeviceConnection(endpoint, wasConnected);
                    var replacement = allowMdnsFallback
                        ? ResolveMdnsEndpointForIdentity(identity, endpoint)
                        : string.Empty;
                    if (!string.IsNullOrWhiteSpace(replacement) &&
                        !string.Equals(
                            replacement,
                            endpoint,
                            StringComparison.OrdinalIgnoreCase) &&
                        TrySplitEndpoint(
                            replacement,
                            out var replacementHost,
                            out var replacementPort))
                    {
                        return ConnectForDeviceCore(
                            identity,
                            replacementHost,
                            replacementPort,
                            autoReconnect,
                            false);
                    }
                    return WirelessConnectionResult.Failed(
                        LocalizationService.Format(
                            "Wireless.DeviceMismatch",
                            endpoint));
                }

                try
                {
                    _settingsService.UpdateAndSave(_settings, delegate(
                        AppSettings settings)
                    {
                        var profile = settings
                            .GetOrCreateDeviceWirelessConnection(
                                identity,
                                false);
                        profile.Mode = AdbConnectionMode.Wireless;
                        profile.WirelessHost = normalizedHost;
                        profile.WirelessPort = port;
                        profile.AutoReconnect = autoReconnect;
                    });
                }
                catch
                {
                    RollbackDeviceConnection(endpoint, wasConnected);
                    throw;
                }
                _logService.Info(LocalizationService.Format(
                    "Log.Wireless.DeviceConnectSucceeded",
                    identity,
                    endpoint));
                if (IsCurrentWirelessProfile(identity))
                    SetSelectedSerial(connectedSerial);
                return WirelessConnectionResult.Succeeded(
                    endpoint,
                    LocalizationService.Get("Wireless.Connected"));
            }
        }

        public WirelessConnectionResult EnableFromUsbForDevice(
            string deviceIdentity,
            string usbSerial,
            string host,
            int port,
            bool autoReconnect)
        {
            var identity = NormalizeDeviceIdentity(deviceIdentity);
            var serial = (usbSerial ?? string.Empty).Trim();
            if (serial.Length == 0)
            {
                return WirelessConnectionResult.Failed(
                    LocalizationService.Get("Wireless.SelectedDeviceNoUsb"));
            }

            lock (_reconnectSync)
            {
                var usbDevice = _adbService.GetDevices().FirstOrDefault(
                    device => device.IsAuthorized &&
                        string.Equals(
                            device.Serial,
                            serial,
                            StringComparison.OrdinalIgnoreCase) &&
                        !AdbService.IsWirelessSerial(device.Serial) &&
                        !AdbService.IsEmulatorSerial(device.Serial));
                if (usbDevice == null)
                {
                    return WirelessConnectionResult.Failed(
                        LocalizationService.Get(
                            "Wireless.SelectedDeviceNoUsb"));
                }

                var usbIdentity = _adbService.GetDeviceIdentity(serial);
                if (!DeviceIdentityMatches(identity, usbIdentity))
                {
                    return WirelessConnectionResult.Failed(
                        LocalizationService.Format(
                            "Wireless.DeviceMismatch",
                            serial));
                }

                var detectedHost = DetectWifiAddress(serial);
                var normalizedHost = string.IsNullOrWhiteSpace(host)
                    ? detectedHost
                    : NormalizeHost(host);
                if (string.IsNullOrWhiteSpace(normalizedHost))
                {
                    return WirelessConnectionResult.Failed(
                        LocalizationService.Get("Wireless.NoWifiIp"));
                }

                _logService.Info(LocalizationService.Format(
                    "Log.Wireless.DeviceEnableFromUsb",
                    identity,
                    serial,
                    port));
                var tcpipResult = _adbService.EnableTcpIp(serial, port);
                if (!tcpipResult.IsSuccess)
                {
                    return WirelessConnectionResult.Failed(
                        LocalizationService.Format(
                            "Wireless.TcpipFailed",
                            GetResultMessage(tcpipResult)));
                }

                Thread.Sleep(800);
                var connection = ConnectForDevice(
                    identity,
                    normalizedHost,
                    port,
                    autoReconnect);
                if (connection.Success ||
                    string.IsNullOrWhiteSpace(detectedHost) ||
                    string.Equals(
                        normalizedHost,
                        detectedHost,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return connection;
                }

                _logService.Info(LocalizationService.Format(
                    "Log.Wireless.RetryDetectedAddress",
                    identity,
                    detectedHost));
                return ConnectForDevice(
                    identity,
                    detectedHost,
                    port,
                    autoReconnect);
            }
        }

        public WirelessConnectionResult DisconnectForDevice(
            string deviceIdentity)
        {
            var identity = NormalizeDeviceIdentity(deviceIdentity);
            lock (_reconnectSync)
            {
                var wasCurrentProfile = IsCurrentWirelessProfile(identity);
                var profile = _settings.FindDeviceWirelessConnection(
                    identity);
                var endpoints = CollectWirelessDisconnectEndpoints(
                    identity,
                    profile);
                _settingsService.UpdateAndSave(_settings, delegate(
                    AppSettings settings)
                {
                    var saved = settings
                        .GetOrCreateDeviceWirelessConnection(
                            identity,
                            false);
                    saved.Mode = AdbConnectionMode.Usb;
                });
                foreach (var endpoint in endpoints)
                {
                    var disconnectResult = _adbService.Disconnect(endpoint);
                    if (!disconnectResult.IsSuccess &&
                        !IsAlreadyDisconnected(disconnectResult))
                    {
                        _logService.Warning(LocalizationService.Format(
                            "Log.Wireless.DisconnectCommandFailed",
                            GetResultMessage(disconnectResult)));
                    }
                }
                if (wasCurrentProfile)
                    SetSelectedSerial(string.Empty);
                _logService.Info(LocalizationService.Format(
                    "Log.Wireless.DeviceDisconnected",
                    identity,
                    string.Join(", ", endpoints)));
                return WirelessConnectionResult.Succeeded(
                    string.Empty,
                    LocalizationService.Get("Wireless.Disconnected"));
            }
        }

        public WirelessConnectionResult Connect(
            string host,
            int port)
        {
            lock (_reconnectSync)
            {
                AdvanceTransitionGeneration();
                var normalizedHost = NormalizeHost(host);
                var endpoint = BuildEndpoint(normalizedHost, port);
                _logService.Info(LocalizationService.Format(
                    "Log.Wireless.ConnectAttempt",
                    endpoint));

                _adbService.StartServer();
                var wasConnected = IsConnected(endpoint);
                var previousTarget = SelectedSerial;
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
                SetSelectedSerial(endpoint);
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
                        !AdbService.IsWirelessSerial(device.Serial) &&
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
            return Pair(
                string.Empty,
                host,
                port,
                pairingCode,
                false);
        }

        public WirelessConnectionResult Pair(
            string deviceIdentity,
            string host,
            int port,
            string pairingCode,
            bool autoReconnect)
        {
            lock (_reconnectSync)
            {
                AdvanceTransitionGeneration();
                return PairCore(deviceIdentity, host, port, pairingCode, autoReconnect);
            }
        }

        private WirelessConnectionResult PairCore(
            string deviceIdentity, string host, int port, string pairingCode,
            bool autoReconnect)
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

            var paired = TryRegisterPairedMdnsDevice(
                deviceIdentity,
                NormalizeHost(host),
                autoReconnect);
            if (paired != null) return paired;

            _logService.Info(LocalizationService.Format(
                "Log.Wireless.MdnsNoMatch",
                string.IsNullOrWhiteSpace(deviceIdentity)
                    ? LocalizationService.Get("Common.Unknown")
                    : deviceIdentity,
                NormalizeHost(host)));
            return WirelessConnectionResult.Succeeded(
                string.Empty,
                LocalizationService.Get("Wireless.PairedManualFallback"));
        }

        private WirelessConnectionResult TryRegisterPairedMdnsDevice(
            string expectedIdentity,
            string pairingHost,
            bool autoReconnect)
        {
            var normalizedExpectedIdentity =
                IsStableDeviceIdentity(expectedIdentity)
                    ? expectedIdentity.Trim()
                    : string.Empty;
            var started = Environment.TickCount;
            do
            {
                var services = _adbService.GetMdnsServices(false, 1000);
                var service = WirelessMdnsResolver
                    .ResolveEndpointForPairingHost(pairingHost, services);
                var endpoint = WirelessMdnsResolver.BuildEndpoint(service);
                if (endpoint.Length > 0)
                {
                    var connected = FindAuthorizedDevice(
                        _adbService.GetDevices(false),
                        endpoint);
                    if (connected == null && service != null)
                    {
                        connected = FindAuthorizedDevice(
                            _adbService.GetDevices(false),
                            WirelessMdnsResolver.BuildMdnsSerial(service));
                    }
                    var wasConnected = connected != null;
                    if (connected == null)
                    {
                        _adbService.Connect(endpoint, false);
                        var connectedSerial = WaitForMdnsConnection(
                            endpoint,
                            service,
                            1200);
                        if (!string.IsNullOrWhiteSpace(connectedSerial))
                        {
                            connected = new AdbDeviceInfo
                            {
                                Serial = connectedSerial,
                                Status = AdbDeviceStatus.Device,
                                RawStatus = "device"
                            };
                        }
                    }

                    if (connected != null)
                    {
                        var actualIdentity = _adbService.GetDeviceIdentity(
                            connected.Serial);
                        if (IsStableDeviceIdentity(actualIdentity) &&
                            (normalizedExpectedIdentity.Length == 0 ||
                             string.Equals(
                                normalizedExpectedIdentity,
                                actualIdentity,
                                StringComparison.OrdinalIgnoreCase)))
                        {
                            SaveDiscoveredWirelessEndpoint(
                                actualIdentity,
                                service.Host,
                                service.Port,
                                autoReconnect,
                                true);
                            _logService.Info(LocalizationService.Format(
                                "Log.Wireless.MdnsPairEndpointFound",
                                actualIdentity,
                                endpoint));
                            SetSelectedSerialForVerifiedDevice(
                                actualIdentity,
                                connected.Serial);
                            return WirelessConnectionResult.Succeeded(
                                endpoint,
                                LocalizationService.Get(
                                    "Wireless.PairedAndConnected"));
                        }

                        if (!wasConnected && string.Equals(
                            connected.Serial,
                            endpoint,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            RollbackDeviceConnection(endpoint, false);
                        }
                        if (normalizedExpectedIdentity.Length > 0 &&
                            IsStableDeviceIdentity(actualIdentity) &&
                            !string.Equals(
                                normalizedExpectedIdentity,
                                actualIdentity,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            return WirelessConnectionResult.Failed(
                                LocalizationService.Format(
                                    "Wireless.DeviceMismatch",
                                    endpoint));
                        }
                    }
                }
                Thread.Sleep(200);
            }
            while (unchecked(Environment.TickCount - started) < 4000);

            return null;
        }

        public WirelessConnectionResult Disconnect()
        {
            lock (_reconnectSync)
            {
                AdvanceTransitionGeneration();
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
                SetSelectedSerial(string.Empty);
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
                AdvanceTransitionGeneration();
                _settingsService.UpdateAndSave(_settings, delegate(
                    AppSettings settings)
                {
                    settings.Connection.Mode = AdbConnectionMode.Usb;
                });
                SetSelectedSerial(string.Empty);
            }
        }

        private AdbDeviceInfo FindPreferredDeviceCore(
            IList<AdbDeviceInfo> devices,
            string currentSerial,
            ConnectionSnapshot connection)
        {
            var candidates = devices ?? new List<AdbDeviceInfo>();
            var verifiedSerial = GetVerifiedMdnsSerial(connection.DeviceIdentity, currentSerial);
            if (connection.Mode == AdbConnectionMode.Wireless)
            {
                var endpoint = candidates.FirstOrDefault(
                    device => string.Equals(
                        device.Serial,
                        connection.Endpoint,
                        StringComparison.OrdinalIgnoreCase) &&
                        device.IsAuthorized);
                if (endpoint != null) return endpoint;

                if (IsStableDeviceIdentity(connection.DeviceIdentity) && verifiedSerial.Length > 0)
                {
                    return candidates.FirstOrDefault(device =>
                        device.IsAuthorized &&
                        AdbService.IsMdnsConnectSerial(device.Serial) &&
                        string.Equals(
                            device.Serial,
                            verifiedSerial,
                            StringComparison.OrdinalIgnoreCase));
                }

                if (IsStableDeviceIdentity(connection.DeviceIdentity))
                {
                    foreach (var device in candidates)
                    {
                        if (device == null || !device.IsAuthorized ||
                            !AdbService.IsMdnsConnectSerial(device.Serial) ||
                            !VerifyDeviceIdentity(
                                connection.DeviceIdentity,
                                device.Serial))
                        {
                            continue;
                        }
                        RememberVerifiedMdnsSerial(connection, device.Serial);
                        return device;
                    }
                }
                return null;
            }

            var current = candidates.FirstOrDefault(device =>
                string.Equals(
                    device.Serial,
                    currentSerial,
                    StringComparison.OrdinalIgnoreCase) &&
                !AdbService.IsWirelessSerial(device.Serial) &&
                !AdbService.IsEmulatorSerial(device.Serial) &&
                device.IsAuthorized);
            if (current != null) return current;

            return candidates.FirstOrDefault(
                    device => !AdbService.IsWirelessSerial(device.Serial) &&
                        !AdbService.IsEmulatorSerial(device.Serial) &&
                        device.IsAuthorized) ??
                candidates.FirstOrDefault(
                    device => !AdbService.IsWirelessSerial(device.Serial) &&
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
                SetSelectedSerial(previousTarget);
            }
        }

        private void RollbackDeviceConnection(
            string endpoint,
            bool wasConnected)
        {
            if (wasConnected || string.IsNullOrWhiteSpace(endpoint)) return;
            try
            {
                var result = _adbService.Disconnect(endpoint);
                if (!result.IsSuccess)
                {
                    _logService.Warning(LocalizationService.Format(
                        "Log.Wireless.DisconnectCommandFailed",
                        GetResultMessage(result)));
                }
            }
            catch (Exception ex)
            {
                _logService.Warning(LocalizationService.Format(
                    "Log.Wireless.DisconnectCommandFailed",
                    ex.Message));
            }
        }

        private void SetSelectedSerial(string serial)
        {
            var normalized = string.IsNullOrWhiteSpace(serial)
                ? string.Empty
                : serial.Trim();
            lock (_stateSync)
            {
            if (!string.Equals(
                normalized,
                _verifiedMdnsSerial,
                StringComparison.OrdinalIgnoreCase))
            {
                _verifiedMdnsSerial = string.Empty;
                _verifiedMdnsIdentity = string.Empty;
            }
            if (string.Equals(
                _selectedSerial,
                normalized,
                StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _selectedSerial = normalized;
            }
            _logService.Progress(
                normalized.Length == 0
                    ? LocalizationService.Get("Log.Adb.TargetCleared")
                    : LocalizationService.Format(
                        "Log.Adb.TargetSelected",
                        normalized));
        }

        private ConnectionSnapshot GetConnectionSnapshot()
        {
            lock (_stateSync)
            {
            var connection = _settings.Connection;
            if (connection == null)
            {
                return new ConnectionSnapshot(
                    AdbConnectionMode.Usb,
                    false,
                    string.Empty,
                    5555,
                    string.Empty);
            }
            return new ConnectionSnapshot(
                connection.Mode,
                connection.AutoReconnect,
                connection.WirelessHost,
                connection.WirelessPort,
                FindUniqueWirelessProfileIdentity(connection));
            }
        }

        private string FindUniqueWirelessProfileIdentity(
            ConnectionSettings connection)
        {
            if (connection == null ||
                connection.Mode != AdbConnectionMode.Wireless ||
                _settings.DeviceWirelessConnectionProfiles == null)
            {
                return string.Empty;
            }

            string wantedHost;
            try
            {
                wantedHost = NormalizeHost(connection.WirelessHost);
            }
            catch (ArgumentException)
            {
                return string.Empty;
            }

            var matches = _settings.DeviceWirelessConnectionProfiles
                .Where(profile => profile != null &&
                    profile.Mode == AdbConnectionMode.Wireless &&
                    profile.WirelessPort == connection.WirelessPort &&
                    IsSameHost(profile.WirelessHost, wantedHost))
                .Select(profile => profile.DeviceIdentity ?? string.Empty)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(2)
                .ToArray();
            return matches.Length == 1 && IsStableDeviceIdentity(matches[0])
                ? matches[0]
                : string.Empty;
        }

        private IList<ConnectionSnapshot> GetReconnectSnapshots()
        {
            lock (_stateSync)
            {
            var candidates = new List<ConnectionSnapshot>();
            var profiles = _settings.DeviceWirelessConnectionProfiles;
            if (profiles == null || profiles.Count == 0)
            {
                AddReconnectSnapshot(
                    candidates,
                    GetConnectionSnapshot());
            }
            else
            {
                foreach (var profile in profiles)
                {
                    if (profile == null) continue;
                    AddReconnectSnapshot(
                        candidates,
                        new ConnectionSnapshot(
                            profile.Mode,
                            profile.AutoReconnect,
                            profile.WirelessHost,
                            profile.WirelessPort,
                            profile.DeviceIdentity));
                }
            }

            var result = new List<ConnectionSnapshot>();
            foreach (var group in candidates.GroupBy(
                item => item.Endpoint,
                StringComparer.OrdinalIgnoreCase))
            {
                var owners = group.Select(item =>
                        item.DeviceIdentity ?? string.Empty)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(2)
                    .ToArray();
                if (owners.Length == 1)
                    result.Add(group.First());
            }
            return result;
            }
        }

        private void AdvanceTransitionGeneration()
        {
            lock (_stateSync)
            {
                _transitionGeneration++;
                _nextReconnectAttemptUtc = DateTime.MinValue;
            }
        }

        private string GetVerifiedMdnsSerial(string identity, string currentSerial)
        {
            lock (_stateSync)
            {
                return string.Equals(identity, _verifiedMdnsIdentity, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(currentSerial, _verifiedMdnsSerial, StringComparison.OrdinalIgnoreCase)
                    ? _verifiedMdnsSerial : string.Empty;
            }
        }

        private void RememberVerifiedMdnsSerial(ConnectionSnapshot expected, string serial)
        {
            lock (_stateSync)
            {
                var current = GetConnectionSnapshot();
                if (current.Mode != expected.Mode ||
                    !string.Equals(current.DeviceIdentity, expected.DeviceIdentity, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(current.Endpoint, expected.Endpoint, StringComparison.OrdinalIgnoreCase)) return;
                _verifiedMdnsIdentity = expected.DeviceIdentity;
                _verifiedMdnsSerial = serial;
            }
        }

        private static bool DeviceIdentityMatches(
            string expectedIdentity,
            string actualIdentity)
        {
            var expected = (expectedIdentity ?? string.Empty).Trim();
            var actual = (actualIdentity ?? string.Empty).Trim();
            if (expected.Length == 0 || actual.Length == 0) return true;
            if (PhysicalDeviceRegistry.IsTemporaryIdentity(expected))
                return true;
            return string.Equals(
                expected,
                actual,
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsStableDeviceIdentity(string identity)
        {
            var value = (identity ?? string.Empty).Trim();
            return value.Length > 0 &&
                !PhysicalDeviceRegistry.IsTemporaryIdentity(value);
        }

        private static void AddReconnectSnapshot(
            IList<ConnectionSnapshot> result,
            ConnectionSnapshot snapshot)
        {
            if (snapshot == null ||
                snapshot.Mode != AdbConnectionMode.Wireless ||
                !snapshot.AutoReconnect ||
                string.IsNullOrWhiteSpace(snapshot.Endpoint))
            {
                return;
            }
            result.Add(snapshot);
        }

        private static string NormalizeDeviceIdentity(
            string deviceIdentity)
        {
            var identity = (deviceIdentity ?? string.Empty).Trim();
            if (identity.Length == 0)
            {
                throw new ArgumentException(
                    "Device identity is empty.",
                    "deviceIdentity");
            }
            return identity;
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

        private static bool IsSameHost(string left, string right)
        {
            try
            {
                return string.Equals(
                    NormalizeHost(left),
                    NormalizeHost(right),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private IList<string> CollectWirelessDisconnectEndpoints(
            string identity,
            DeviceWirelessConnectionProfile profile)
        {
            var endpoints = new List<string>();
            var unique = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            string savedEndpoint = string.Empty;
            if (profile != null &&
                profile.Mode == AdbConnectionMode.Wireless)
            {
                try
                {
                    savedEndpoint = BuildEndpoint(
                        profile.WirelessHost,
                        profile.WirelessPort);
                }
                catch (ArgumentException)
                {
                    savedEndpoint = string.Empty;
                }
                AddDisconnectEndpointIfOwned(
                    endpoints,
                    unique,
                    savedEndpoint,
                    identity);
            }

            var devices = _adbService.GetDevices(false);
            IList<AdbMdnsServiceRecord> mdnsServices = null;
            var mdnsLookupAttempted = false;
            foreach (var device in devices ?? new List<AdbDeviceInfo>())
            {
                if (device == null || !device.IsAuthorized ||
                    !AdbService.IsWirelessSerial(device.Serial) ||
                    !VerifyDeviceIdentity(identity, device.Serial))
                {
                    continue;
                }

                AddDisconnectEndpointIfOwned(
                    endpoints,
                    unique,
                    device.Serial,
                    identity);
                if (AdbService.IsMdnsConnectSerial(device.Serial))
                {
                    if (!mdnsLookupAttempted)
                    {
                        mdnsLookupAttempted = true;
                        mdnsServices = _adbService.GetMdnsServices(
                            false,
                            2000);
                    }
                    var service = WirelessMdnsResolver
                        .ResolveConnectedService(device.Serial, mdnsServices);
                    var discovered =
                        WirelessMdnsResolver.BuildEndpoint(service);
                    if (discovered.Length > 0)
                    {
                        AddDisconnectEndpointIfOwned(
                            endpoints,
                            unique,
                            discovered,
                            identity);
                    }
                }
            }
            return endpoints;
        }

        private void AddDisconnectEndpointIfOwned(
            IList<string> endpoints,
            ISet<string> unique,
            string endpoint,
            string identity)
        {
            if (string.IsNullOrWhiteSpace(endpoint) ||
                EndpointHasDifferentProfileOwner(endpoint, identity))
            {
                return;
            }

            var connected = FindAuthorizedDevice(
                _adbService.GetDevices(false),
                endpoint);
            if (connected != null &&
                !VerifyDeviceIdentity(identity, connected.Serial))
            {
                return;
            }
            if (unique.Add(endpoint)) endpoints.Add(endpoint);
        }

        private bool EndpointHasDifferentProfileOwner(
            string endpoint,
            string identity)
        {
            foreach (var profile in _settings.DeviceWirelessConnectionProfiles ??
                new List<DeviceWirelessConnectionProfile>())
            {
                if (profile == null ||
                    profile.Mode != AdbConnectionMode.Wireless ||
                    string.Equals(
                        profile.DeviceIdentity,
                        identity,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                try
                {
                    var other = BuildEndpoint(
                        profile.WirelessHost,
                        profile.WirelessPort);
                    if (string.Equals(
                        other,
                        endpoint,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch (ArgumentException)
                {
                    // Invalid profiles cannot claim an endpoint.
                }
            }
            return false;
        }

        private static bool IsAlreadyDisconnected(ProcessResult result)
        {
            if (result == null) return false;
            var output = (result.StandardError ?? string.Empty) + "\n" +
                (result.StandardOutput ?? string.Empty);
            return output.IndexOf(
                    "no such device",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                output.IndexOf(
                    "not connected",
                    StringComparison.OrdinalIgnoreCase) >= 0;
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
                int port,
                string deviceIdentity)
            {
                Mode = mode;
                AutoReconnect = autoReconnect;
                Host = host ?? string.Empty;
                Port = port;
                DeviceIdentity = deviceIdentity ?? string.Empty;
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
            public string DeviceIdentity { get; private set; }
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
