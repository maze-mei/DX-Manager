using System;
using System.IO;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using DexManager.Models;
using DexManager.Utils;

namespace DexManager.Services
{
    public sealed class CompanionGuardianService : IDisposable
    {
        private const int DevicePort = 37124;
        public ForegroundAppCache ForegroundApps { get; private set; }
        private const string ServiceComponent =
            "io.github.mazemei.dxdisplaycleanup/io.github.mazemei.dxdisplaycleanup.SessionGuardianService";
        private const string StartAction =
            "io.github.mazemei.dxdisplaycleanup.START_SESSION_GUARDIAN";

        private readonly object _sync = new object();
        private readonly object _sendSync = new object();
        private readonly AdbService _adbService;
        private readonly LogService _logService;
        private readonly DisplayCleanupPermissionService _verifier;
        private readonly CancellationTokenSource _shutdown =
            new CancellationTokenSource();
        private readonly SemaphoreSlim _configurationGate =
            new SemaphoreSlim(1, 1);
        private TcpListener _listener;
        private Task _acceptTask;
        private TcpClient _guardianClient;
        private NetworkStream _guardianStream;
        private string _serial;
        private string _token;
        private bool _disposed;

        public CompanionGuardianService(
            AdbService adbService,
            LogService logService)
        {
            _adbService = adbService ??
                throw new ArgumentNullException("adbService");
            _logService = logService ??
                throw new ArgumentNullException("logService");
            _verifier = new DisplayCleanupPermissionService(adbService);
            ForegroundApps = new ForegroundAppCache();
        }

        public bool IsConnected
        {
            get
            {
                lock (_sync)
                    return _guardianClient != null &&
                        _guardianStream != null;
            }
        }

        public async Task AttachAsync(
            string serial,
            DeviceTransportKind transportKind)
        {
            if (string.IsNullOrWhiteSpace(serial) ||
                _shutdown.IsCancellationRequested)
            {
                return;
            }

            await _configurationGate.WaitAsync().ConfigureAwait(false);
            try
            {
                await AttachCoreAsync(serial, transportKind)
                    .ConfigureAwait(false);
            }
            finally
            {
                _configurationGate.Release();
            }
        }

        private async Task AttachCoreAsync(
            string serial,
            DeviceTransportKind transportKind)
        {
            var verification = await Task.Run(
                () => _verifier.Inspect(serial)).ConfigureAwait(false);
            if (_shutdown.IsCancellationRequested) return;
            if (verification.State != DisplayCleanupPermissionState.Granted ||
                verification.VersionCode <
                    DisplayCleanupPermissionService.BundledVersionCode ||
                !string.Equals(
                    verification.Serial,
                    serial,
                    StringComparison.OrdinalIgnoreCase))
            {
                _logService.Info(DeviceLogFormatter.ForSerial(
                    serial,
                    "Companion shutdown protection is unavailable. " +
                    "DX Companion 3.0.0 or later with cleanup permission is required."));
                return;
            }

            EnsureListener();
            var localPort = ((IPEndPoint)_listener.LocalEndpoint).Port;
            var token = CreateToken();

            await Task.Run(delegate
            {
                DetachInternal(true, true);
                try
                {
                    _adbService.RemoveReverseForSerial(
                        serial,
                        DevicePort,
                        false);
                }
                catch (Exception)
                {
                    // Replacing the mapping below is authoritative.
                }

                var reverse = _adbService.ReverseForSerial(
                    serial,
                    DevicePort,
                    localPort,
                    true);
                if (!reverse.IsSuccess)
                    throw new InvalidOperationException(
                        "Could not create the Companion guardian tunnel: " +
                        CombineOutput(reverse));

                var sdkResult = _adbService.ShellForSerial(
                    serial,
                    "getprop ro.build.version.sdk",
                    false);
                int sdk;
                var foreground = sdkResult.IsSuccess &&
                    int.TryParse(
                        (sdkResult.StandardOutput ?? string.Empty).Trim(),
                        out sdk) && sdk >= 26;
                var command = (foreground
                        ? "am start-foreground-service -n "
                        : "am startservice -n ") +
                    ServiceComponent + " -a " + StartAction +
                    " --ei port " + DevicePort +
                    " --es token " + token +
                    " --es transport " +
                    GetTransportArgument(transportKind) +
                    " --ei protocol_version " + CompanionGuardianProtocol.Version;
                // Publish the token before starting Android: its first connection
                // must not race am start-foreground-service's return.
                lock (_sync)
                {
                    _serial = serial;
                    _token = token;
                }
                var start = _adbService.ShellForSerial(
                    serial,
                    command,
                    false);
                if (!start.IsSuccess)
                {
                    lock (_sync)
                    {
                        _serial = null;
                        _token = null;
                        CloseGuardianClientLocked();
                    }
                    _adbService.RemoveReverseForSerial(
                        serial,
                        DevicePort,
                        false);
                    throw new InvalidOperationException(
                        "Could not start DX Companion shutdown protection: " +
                        CombineOutput(start));
                }

            }).ConfigureAwait(false);

            _logService.Info(DeviceLogFormatter.ForSerial(
                serial,
                "DX Companion shutdown protection was configured."));
        }

        public void NotifyConnectionLost(string serial)
        {
            lock (_sync)
            {
                if (!string.IsNullOrWhiteSpace(serial) &&
                    !DeviceSerialScope.Matches(serial, _serial))
                {
                    return;
                }
                _serial = null;
                _token = null;
                CloseGuardianClientLocked();
            }
        }

        public bool TrySendWindowsShutdown(
            bool removeOverlay,
            bool restoreStayAwake,
            string originalStayAwakeValue)
        {
            lock (_sendSync)
            {
                NetworkStream stream;
                lock (_sync) stream = _guardianStream;
                if (stream == null) return false;
                try
                {
                    stream.WriteByte(
                        CompanionGuardianProtocol.WindowsShutdown);
                    stream.WriteByte(removeOverlay ? (byte)1 : (byte)0);
                    stream.WriteByte(restoreStayAwake ? (byte)1 : (byte)0);
                    CompanionGuardianProtocol.WriteString(
                        stream,
                        originalStayAwakeValue ?? string.Empty);
                    stream.Flush();
                    return true;
                }
                catch (IOException)
                {
                    return false;
                }
                catch (ObjectDisposedException)
                {
                    return false;
                }
            }
        }

        public void RequestShutdown()
        {
            RequestShutdownCore(true, true);
        }

        public void RequestWindowsShutdown()
        {
            RequestShutdownCore(false, false);
        }

        private void RequestShutdownCore(
            bool notifyCompanion,
            bool removeReverse)
        {
            if (_shutdown.IsCancellationRequested) return;
            if (notifyCompanion) TrySendStopMonitoring();
            _shutdown.Cancel();
            DetachInternal(notifyCompanion, removeReverse);
            lock (_sync)
            {
                if (_listener != null)
                {
                    try { _listener.Stop(); }
                    catch (SocketException) { }
                }
                CloseGuardianClientLocked();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            RequestShutdown();
            try
            {
                if (_acceptTask != null) _acceptTask.Wait(1500);
            }
            catch (AggregateException) { }
            _configurationGate.Dispose();
            _shutdown.Dispose();
        }

        private void EnsureListener()
        {
            lock (_sync)
            {
                if (_listener != null && _acceptTask != null &&
                    !_acceptTask.IsCompleted)
                {
                    return;
                }
                if (_listener != null)
                {
                    try { _listener.Stop(); }
                    catch (SocketException) { }
                }
                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start();
                _acceptTask = Task.Run((Func<Task>)AcceptLoopAsync);
            }
        }

        private async Task AcceptLoopAsync()
        {
            while (!_shutdown.IsCancellationRequested)
            {
                TcpClient client = null;
                try
                {
                    client = await _listener.AcceptTcpClientAsync()
                        .ConfigureAwait(false);
                    var accepted = client;
                    var ignored = Task.Run(
                        (Action)delegate { HandleGuardian(accepted); });
                }
                catch (ObjectDisposedException)
                {
                    if (_shutdown.IsCancellationRequested) return;
                }
                catch (SocketException ex)
                {
                    if (_shutdown.IsCancellationRequested) return;
                    _logService.Error(
                        "Companion guardian listener failed.", ex);
                    if (client != null) client.Dispose();
                }
            }
        }

        private void HandleGuardian(TcpClient client)
        {
            long generation = -1;
            string serial = null;
            try
            {
                client.NoDelay = true;
                var stream = client.GetStream();
                stream.ReadTimeout = 10000;
                stream.WriteTimeout = 3000;
                if (CompanionGuardianProtocol.ReadInt32(stream) != CompanionGuardianProtocol.Magic ||
                    CompanionGuardianProtocol.ReadInt32(stream) != CompanionGuardianProtocol.Version)
                    throw new InvalidDataException("Unsupported Companion guardian protocol.");
                var suppliedToken = CompanionGuardianProtocol.ReadString(stream);
                if (!IsCurrentToken(suppliedToken)) return;
                lock (_sync)
                {
                    if (!IsCurrentToken(suppliedToken)) return;
                    CloseGuardianClientLocked();
                    generation = ForegroundApps.BeginConnection();
                    serial = _serial;
                    _guardianClient = client;
                    _guardianStream = stream;
                }
                lock (_sendSync)
                {
                    stream.WriteByte(1);
                    stream.Flush();
                }
                var ignored = Task.Run(delegate { SendHeartbeatLoop(client, stream); });
                bool loggedRtt = false;
                while (!_shutdown.IsCancellationRequested && IsCurrentClient(client))
                {
                    int message = stream.ReadByte();
                    if (message < 0) throw new EndOfStreamException();
                    if (message == CompanionGuardianProtocol.AccessibilityStatus)
                    {
                        long sequence = CompanionGuardianProtocol.ReadInt64(stream);
                        int available = stream.ReadByte();
                        if (sequence < 0 || available < 0 || available > 1)
                            throw new InvalidDataException("Invalid accessibility status.");
                        if (ForegroundApps.SetAvailable(generation, sequence, available == 1))
                            _logService.Info(DeviceLogFormatter.ForSerial(serial,
                                "AI keymap: accessibility " + (available == 1
                                ? "connected; app snapshot is starting."
                                : "unavailable; using default Enter.")));
                    }
                    else if (message == CompanionGuardianProtocol.ForegroundApp)
                    {
                        long sequence = CompanionGuardianProtocol.ReadInt64(stream);
                        int displayId = CompanionGuardianProtocol.ReadInt32(stream);
                        int ageMs = CompanionGuardianProtocol.ReadInt32(stream);
                        var package = CompanionGuardianProtocol.ReadString(stream);
                        if (sequence < 0 || displayId < 0 || ageMs < 0 ||
                            (package.Length != 0 && !AppKeyRuleResolver.IsPackageName(package)))
                            throw new InvalidDataException("Invalid foreground app frame.");
                        // Android can stall a root/window query: do not apply an old AI rule.
                        bool stale = ageMs > 1500;
                        if (stale) package = string.Empty;
                        var previous = ForegroundApps.GetPackage(displayId);
                        if (ForegroundApps.Update(generation, sequence, displayId, package) &&
                            (previous != package || stale))
                            _logService.Info(DeviceLogFormatter.ForSerial(serial,
                                "AI keymap: display=" + displayId + " " +
                                (string.IsNullOrEmpty(previous) ? "<unknown>" : previous) + " -> " +
                                (string.IsNullOrEmpty(package) ? "<unknown/default>" : package) +
                                "; Android event age=" + ageMs + "ms" +
                                (stale ? " (stale update discarded)" : "")));
                    }
                    else if (message == CompanionGuardianProtocol.ForegroundWindow)
                    {
                        var window = ForegroundWindowDiagnostic.Read(stream);
                        if (ForegroundApps.UpdateWindow(generation, window))
                            _logService.Info(DeviceLogFormatter.ForSerial(serial,
                                "AI keymap: web context updated; display=" + window.DisplayId +
                                " window=" + window.WindowId));
                        _logService.Info(DeviceLogFormatter.ForSerial(serial, window.FormatLog()));
                    }
                    else if (message == CompanionGuardianProtocol.Pong)
                    {
                        var probe = CompanionGuardianProtocol.ReadInt64(stream);
                        var now = Stopwatch.GetTimestamp();
                        if (probe < 0 || probe > now)
                            throw new InvalidDataException("Invalid guardian probe.");
                        ForegroundApps.Heartbeat(generation);
                        if (!loggedRtt)
                        {
                            loggedRtt = true;
                            var rtt = (now - probe) * 1000.0 / Stopwatch.Frequency;
                            _logService.Info(DeviceLogFormatter.ForSerial(serial,
                                "AI keymap: tunnel RTT=" + rtt.ToString("F1",
                                System.Globalization.CultureInfo.InvariantCulture) +
                                "ms (round trip, not full app-switch latency)."));
                        }
                    }
                    else throw new InvalidDataException("Unknown Companion guardian message.");
                }
            }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                if (!_shutdown.IsCancellationRequested)
                    _logService.Error("Companion guardian connection failed.", ex);
            }
            finally
            {
                if (generation >= 0) ForegroundApps.Disconnect(generation);
                lock (_sync)
                {
                    if (ReferenceEquals(client, _guardianClient))
                        CloseGuardianClientLocked();
                }
                try { client.Dispose(); } catch (ObjectDisposedException) { }
            }
        }

        private void SendHeartbeatLoop(TcpClient client, NetworkStream stream)
        {
            try
            {
                while (!_shutdown.IsCancellationRequested && IsCurrentClient(client))
                {
                    lock (_sendSync)
                    {
                        stream.WriteByte(CompanionGuardianProtocol.Ping);
                        CompanionGuardianProtocol.WriteInt64(stream, Stopwatch.GetTimestamp());
                        stream.Flush();
                    }
                    if (_shutdown.Token.WaitHandle.WaitOne(3000)) break;
                }
            }
            catch (IOException) { try { client.Close(); } catch (Exception) { } }
            catch (ObjectDisposedException) { }
        }
        private void TrySendStopMonitoring()
        {
            lock (_sendSync)
            {
                NetworkStream stream;
                lock (_sync) stream = _guardianStream;
                if (stream == null) return;
                try
                {
                    stream.WriteByte(
                        CompanionGuardianProtocol.StopMonitoring);
                    stream.Flush();
                }
                catch (IOException) { }
                catch (ObjectDisposedException) { }
            }
        }

        private void DetachInternal(bool notifyCompanion, bool removeReverse)
        {
            string serial;
            lock (_sync)
            {
                serial = _serial;
                _serial = null;
                _token = null;
                CloseGuardianClientLocked();
            }
            if (string.IsNullOrWhiteSpace(serial)) return;
            if (notifyCompanion)
            {
                try
                {
                    _adbService.ShellForSerial(
                        serial,
                        "am stopservice -n " + ServiceComponent,
                        false);
                }
                catch (Exception) { }
            }
            if (!removeReverse) return;
            try
            {
                _adbService.RemoveReverseForSerial(
                    serial,
                    DevicePort,
                    false);
            }
            catch (Exception) { }
        }

        private bool IsCurrentToken(string supplied)
        {
            string token;
            lock (_sync) token = _token;
            if (string.IsNullOrEmpty(token) ||
                string.IsNullOrEmpty(supplied) ||
                token.Length != supplied.Length)
            {
                return false;
            }
            var difference = 0;
            for (var index = 0; index < token.Length; index++)
                difference |= token[index] ^ supplied[index];
            return difference == 0;
        }

        private bool IsCurrentClient(TcpClient client)
        {
            lock (_sync)
                return ReferenceEquals(client, _guardianClient);
        }

        private void CloseGuardianClientLocked()
        {
            ForegroundApps.Invalidate();
            var stream = _guardianStream;
            var client = _guardianClient;
            _guardianStream = null;
            _guardianClient = null;
            if (stream != null)
            {
                try { stream.Dispose(); }
                catch (ObjectDisposedException) { }
            }
            if (client != null)
            {
                try { client.Close(); }
                catch (SocketException) { }
            }
        }

        private static string CreateToken()
        {
            var bytes = new byte[32];
            using (var generator = RandomNumberGenerator.Create())
                generator.GetBytes(bytes);
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static string GetTransportArgument(
            DeviceTransportKind transportKind)
        {
            switch (transportKind)
            {
                case DeviceTransportKind.Usb:
                    return "usb";
                case DeviceTransportKind.Wireless:
                    return "wireless";
                default:
                    return "unknown";
            }
        }

        private static string CombineOutput(
            DexManager.Models.ProcessResult result)
        {
            if (result == null) return string.Empty;
            var error = (result.StandardError ?? string.Empty).Trim();
            var output = (result.StandardOutput ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(error) ? output : error;
        }
    }
}
