using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DexManager.Models;

namespace DexManager.Services
{
    public sealed class PhoneTransferReceiver : IDisposable
    {
        private const int DevicePort = 37123;
        private const string ConfigureAction =
            "io.github.mazemei.dxdisplaycleanup.CONFIGURE_PC_TRANSFER";
        private const string ReceiverComponent =
            "io.github.mazemei.dxdisplaycleanup/io.github.mazemei.dxdisplaycleanup.TransferSessionReceiver";

        private static readonly HashSet<string> ReservedNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "CON", "PRN", "AUX", "NUL",
                "COM1", "COM2", "COM3", "COM4", "COM5",
                "COM6", "COM7", "COM8", "COM9",
                "LPT1", "LPT2", "LPT3", "LPT4", "LPT5",
                "LPT6", "LPT7", "LPT8", "LPT9"
            };

        private readonly object _sync = new object();
        private readonly AdbService _adbService;
        private readonly SettingsService _settingsService;
        private readonly AppSettings _settings;
        private readonly LogService _logService;
        private readonly DisplayCleanupPermissionService _companionVerifier;
        private readonly DeviceRuntimeSessionRegistry _runtimeSessions;
        private readonly CancellationTokenSource _shutdown =
            new CancellationTokenSource();
        private readonly SemaphoreSlim _configurationGate =
            new SemaphoreSlim(1, 1);
        private readonly HashSet<TcpClient> _clients =
            new HashSet<TcpClient>();
        private TcpListener _listener;
        private Task _acceptTask;
        private string _serial;
        private string _deviceDisplayName;
        private string _token;
        private long _sequence;
        private int _activeTransfers;
        private bool _disposed;

        public PhoneTransferReceiver(
            AdbService adbService,
            SettingsService settingsService,
            AppSettings settings,
            LogService logService,
            DeviceRuntimeSessionRegistry runtimeSessions)
        {
            _adbService = adbService ??
                throw new ArgumentNullException("adbService");
            _settingsService = settingsService ??
                throw new ArgumentNullException("settingsService");
            _settings = settings ??
                throw new ArgumentNullException("settings");
            _logService = logService ??
                throw new ArgumentNullException("logService");
            _runtimeSessions = runtimeSessions ??
                throw new ArgumentNullException("runtimeSessions");
            _companionVerifier =
                new DisplayCleanupPermissionService(adbService);
        }

        public event EventHandler<PhoneTransferProgressEventArgs>
            ProgressChanged;

        public async Task AttachAsync(string serial)
        {
            await AttachAsync(serial, string.Empty).ConfigureAwait(false);
        }

        public async Task AttachAsync(
            string serial,
            string deviceDisplayName)
        {
            if (!_settings.Features.PhoneToPcTransferEnabled ||
                string.IsNullOrWhiteSpace(serial) ||
                _shutdown.IsCancellationRequested)
            {
                return;
            }
            await _configurationGate.WaitAsync().ConfigureAwait(false);
            try
            {
                await AttachCoreAsync(serial, deviceDisplayName)
                    .ConfigureAwait(false);
            }
            finally
            {
                _configurationGate.Release();
            }
        }

        private async Task AttachCoreAsync(
            string serial,
            string deviceDisplayName)
        {
            if (!_settings.Features.PhoneToPcTransferEnabled ||
                string.IsNullOrWhiteSpace(serial) ||
                _shutdown.IsCancellationRequested)
            {
                return;
            }

            var verification = await Task.Run(
                () => _companionVerifier.Inspect(serial))
                .ConfigureAwait(false);
            if (_shutdown.IsCancellationRequested) return;
            if (!CanConfigureTransfer(verification, serial))
            {
                _logService.Info(
                    "Phone-to-PC transfer setup was skipped for device " + serial +
                    " (verification=" + verification.State +
                    ", versionCode=" + verification.VersionCode +
                    ", required=" + DisplayCleanupPermissionService.BundledVersionCode + ").");
                return;
            }

            EnsureListener();
            var localPort = ((IPEndPoint)_listener.LocalEndpoint).Port;
            var token = CreateToken();

            await Task.Run(delegate
            {
                DetachInternal(false);
                try
                {
                    _adbService.RemoveReverseForSerial(
                        serial,
                        DevicePort,
                        false);
                }
                catch (Exception)
                {
                    // A stale mapping may belong to a DX Manager process
                    // that ended abnormally. Creating the new mapping below
                    // remains the authoritative recovery step.
                }
                var reverse = _adbService.ReverseForSerial(
                    serial,
                    DevicePort,
                    localPort,
                    true);
                if (!reverse.IsSuccess)
                    throw new InvalidOperationException(
                        "Could not create the ADB reverse tunnel: " +
                        CombineOutput(reverse));

                var configure = _adbService.ShellForSerial(
                    serial,
                    "am broadcast -n " + ReceiverComponent +
                    " -a " + ConfigureAction +
                    " --ez enabled true --ei port " + DevicePort +
                    " --es token " + token,
                    false);
                if (!configure.IsSuccess)
                {
                    _adbService.RemoveReverseForSerial(
                        serial,
                        DevicePort,
                        false);
                    throw new InvalidOperationException(
                        "Could not configure DX Companion: " +
                        CombineOutput(configure));
                }

                lock (_sync)
                {
                    _serial = serial;
                    _deviceDisplayName = string.IsNullOrWhiteSpace(
                        deviceDisplayName)
                        ? serial.Trim()
                        : deviceDisplayName.Trim();
                    _token = token;
                }
                _runtimeSessions.SetCompanionAttached(serial, true);
            }).ConfigureAwait(false);

            if (_shutdown.IsCancellationRequested)
            {
                DetachInternal(true);
                return;
            }

            _logService.Info(
                "Phone-to-PC transfer receiver is ready for device " +
                serial + ".");
        }

        internal static bool CanConfigureTransfer(
            DisplayCleanupPermissionStatus verification, string serial)
        {
            return verification != null &&
                (verification.State == DisplayCleanupPermissionState.Ready ||
                 verification.State == DisplayCleanupPermissionState.Granted) &&
                verification.VersionCode >= DisplayCleanupPermissionService.BundledVersionCode &&
                !string.IsNullOrWhiteSpace(serial) &&
                string.Equals(verification.Serial, serial, StringComparison.OrdinalIgnoreCase);
        }

        public async Task DetachAsync(string serial)
        {
            await _configurationGate.WaitAsync().ConfigureAwait(false);
            try
            {
                await Task.Run(delegate
                {
                    lock (_sync)
                    {
                        if (!string.IsNullOrWhiteSpace(serial) &&
                            !DeviceSerialScope.Matches(serial, _serial))
                        {
                            return;
                        }
                    }
                    DetachInternal(true);
                }).ConfigureAwait(false);
            }
            finally
            {
                _configurationGate.Release();
            }
        }

        public void ApplySettings()
        {
            if (!_settings.Features.PhoneToPcTransferEnabled)
            {
                var ignored = DetachAsync(null);
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
            _shutdown.Cancel();
            DetachInternal(notifyCompanion, removeReverse);
            lock (_sync)
            {
                if (_listener != null)
                {
                    try { _listener.Stop(); }
                        catch (SocketException) { }
                }
                foreach (var client in _clients)
                {
                    try { client.Close(); }
                    catch (SocketException) { }
                }
                _clients.Clear();
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
            _shutdown.Dispose();
        }

        private void EnsureListener()
        {
            lock (_sync)
            {
                if (IsListenerHealthy()) return;
                if (_listener != null)
                {
                    try { _listener.Stop(); }
                    catch (SocketException) { }
                    _listener = null;
                }
                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start();
                _acceptTask = Task.Run((Func<Task>)AcceptLoopAsync);
            }
        }

        private bool IsListenerHealthy()
        {
            if (_listener == null || _acceptTask == null ||
                _acceptTask.IsCompleted)
            {
                return false;
            }
            try
            {
                return _listener.Server != null &&
                    _listener.Server.IsBound;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (SocketException)
            {
                return false;
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
                    lock (_sync) _clients.Add(client);
                    var acceptedClient = client;
                    var ignored = Task.Run(
                        (Action)delegate
                        {
                            HandleClient(acceptedClient);
                        });
                }
                catch (ObjectDisposedException)
                {
                    if (_shutdown.IsCancellationRequested) return;
                }
                catch (SocketException ex)
                {
                    if (_shutdown.IsCancellationRequested) return;
                    _logService.Error(
                        "Phone-to-PC transfer listener failed.", ex);
                    if (client != null) client.Dispose();
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            var activeTransfers = Interlocked.Increment(
                ref _activeTransfers);
            UpdateActiveTransferCount(activeTransfers);
            var batchId = Guid.Empty;
            try
            {
                client.NoDelay = true;
                using (client)
                using (var stream = client.GetStream())
                {
                    stream.ReadTimeout = 30000;
                    stream.WriteTimeout = 30000;
                    if (PhoneTransferProtocol.ReadInt32(stream) !=
                            PhoneTransferProtocol.Magic ||
                        PhoneTransferProtocol.ReadInt32(stream) !=
                            PhoneTransferProtocol.Version)
                    {
                        throw new InvalidDataException(
                            "Unsupported phone transfer protocol.");
                    }

                    var suppliedToken =
                        PhoneTransferProtocol.ReadString(stream);
                    if (!IsCurrentToken(suppliedToken))
                    {
                        PhoneTransferProtocol.WriteResponse(
                            stream,
                            false,
                            "The DX Manager transfer session has expired.");
                        return;
                    }

                    var batchText =
                        PhoneTransferProtocol.ReadString(stream);
                    if (string.Equals(
                        batchText,
                        PhoneTransferProtocol.StatusProbeBatch,
                        StringComparison.Ordinal))
                    {
                        PhoneTransferProtocol.WriteResponse(
                            stream,
                            true,
                            "Ready");
                        return;
                    }
                    if (!Guid.TryParse(batchText, out batchId))
                        batchId = Guid.NewGuid();
                    var itemCount = PhoneTransferProtocol.ReadInt32(stream);
                    var totalBytes = PhoneTransferProtocol.ReadInt64(stream);
                    if (itemCount < 0 ||
                        itemCount > PhoneTransferProtocol.MaxItemCount)
                    {
                        throw new InvalidDataException(
                            "Invalid transfer item count.");
                    }

                    var destination = ResolveDestinationFolder();
                    Directory.CreateDirectory(destination);
                    PhoneTransferProtocol.WriteResponse(
                        stream,
                        true,
                        "Ready");
                    ReceiveItems(
                        stream,
                        batchId,
                        itemCount,
                        totalBytes,
                        destination);
                    PhoneTransferProtocol.WriteResponse(
                        stream,
                        true,
                        "Completed");
                }
            }
            catch (Exception ex)
            {
                _logService.Error(
                    "Phone-to-PC file transfer failed.", ex);
                Publish(
                    batchId,
                    PhoneTransferStage.Failed,
                    string.Empty,
                    0,
                    0,
                    0,
                    0,
                    ResolveDestinationFolderSafe(),
                    ex.Message);
            }
            finally
            {
                lock (_sync) _clients.Remove(client);
                activeTransfers = Interlocked.Decrement(
                    ref _activeTransfers);
                UpdateActiveTransferCount(activeTransfers);
            }
        }

        private void ReceiveItems(
            Stream stream,
            Guid batchId,
            int itemCount,
            long totalBytes,
            string destination)
        {
            var rootNames = new Dictionary<int, string>();
            var reservedRootPaths = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            var resolvedPaths = new Dictionary<string, string>(
                StringComparer.Ordinal);
            var completedItems = 0;
            long receivedBytes = 0;
            var lastByteProgressUtc = DateTime.MinValue;
            Publish(
                batchId,
                PhoneTransferStage.Receiving,
                string.Empty,
                0,
                itemCount,
                0,
                totalBytes,
                destination,
                string.Empty);

            for (var index = 0; index < itemCount; index++)
            {
                ThrowIfShutdown();
                var kind = stream.ReadByte();
                if (kind != 0 && kind != 1)
                    throw new InvalidDataException("Invalid item type.");
                var rootId = PhoneTransferProtocol.ReadInt32(stream);
                var relativePath =
                    PhoneTransferProtocol.ReadString(stream);
                var declaredSize = PhoneTransferProtocol.ReadInt64(stream);
                var modified = PhoneTransferProtocol.ReadInt64(stream);
                var safePath = ResolveSafePath(
                    destination,
                    rootId,
                    relativePath,
                    rootNames,
                    reservedRootPaths,
                    resolvedPaths);

                Publish(
                    batchId,
                    PhoneTransferStage.Receiving,
                    relativePath,
                    completedItems,
                    itemCount,
                    receivedBytes,
                    totalBytes,
                    destination,
                    string.Empty);

                if (kind == 0)
                {
                    Directory.CreateDirectory(safePath);
                }
                else
                {
                    Directory.CreateDirectory(
                        Path.GetDirectoryName(safePath));
                    var partial = CreatePartialPath(
                        safePath,
                        Guid.NewGuid());
                    try
                    {
                        using (var output = new FileStream(
                            partial,
                            FileMode.CreateNew,
                            FileAccess.Write,
                            FileShare.None,
                            1024 * 1024,
                            FileOptions.SequentialScan))
                        {
                            while (true)
                            {
                                ThrowIfShutdown();
                                var chunkLength =
                                    PhoneTransferProtocol.ReadInt32(stream);
                                if (chunkLength == 0) break;
                                if (chunkLength < 0 ||
                                    chunkLength >
                                        PhoneTransferProtocol.MaxChunkBytes)
                                {
                                    throw new InvalidDataException(
                                        "Invalid file chunk length.");
                                }
                                CopyExact(
                                    stream,
                                    output,
                                    chunkLength,
                                    delegate(int copied)
                                    {
                                        receivedBytes += copied;
                                        var now = DateTime.UtcNow;
                                        if ((now - lastByteProgressUtc)
                                                .TotalMilliseconds >= 120)
                                        {
                                            lastByteProgressUtc = now;
                                            Publish(
                                                batchId,
                                                PhoneTransferStage.Receiving,
                                                relativePath,
                                                completedItems,
                                                itemCount,
                                                receivedBytes,
                                                totalBytes,
                                                destination,
                                                string.Empty);
                                        }
                                    });
                            }
                        }
                        if (declaredSize >= 0 &&
                            new FileInfo(partial).Length != declaredSize)
                        {
                            throw new InvalidDataException(
                                "Received file size does not match metadata.");
                        }
                        File.Move(partial, safePath);
                        TrySetModifiedTime(safePath, modified);
                    }
                    finally
                    {
                        if (File.Exists(partial))
                        {
                            try { File.Delete(partial); }
                            catch (IOException) { }
                        }
                    }
                }
                completedItems++;
            }

            Publish(
                batchId,
                PhoneTransferStage.Completed,
                string.Empty,
                completedItems,
                itemCount,
                receivedBytes,
                totalBytes,
                destination,
                string.Empty);
            _logService.Info(
                "Phone-to-PC transfer completed: " +
                completedItems + " item(s), " + receivedBytes +
                " byte(s). Destination=" + destination);
        }

        private void DetachInternal(bool notifyCompanion)
        {
            DetachInternal(notifyCompanion, true);
        }

        private void DetachInternal(
            bool notifyCompanion,
            bool removeReverse)
        {
            string serial;
            lock (_sync)
            {
                serial = _serial;
                _serial = null;
                _deviceDisplayName = null;
                _token = null;
                foreach (var client in _clients)
                {
                    try { client.Close(); }
                    catch (SocketException) { }
                }
                _clients.Clear();
            }
            if (string.IsNullOrWhiteSpace(serial)) return;
            _runtimeSessions.SetCompanionAttached(serial, false);
            _runtimeSessions.SetPhoneToPcActiveTransfers(serial, 0);

            if (notifyCompanion)
            {
                try
                {
                    _adbService.ShellForSerial(
                        serial,
                        "am broadcast -n " + ReceiverComponent +
                        " -a " + ConfigureAction +
                        " --ez enabled false",
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

        private bool IsCurrentToken(string value)
        {
            string token;
            lock (_sync) token = _token;
            if (string.IsNullOrEmpty(token) ||
                string.IsNullOrEmpty(value) ||
                token.Length != value.Length)
            {
                return false;
            }
            var difference = 0;
            for (var i = 0; i < token.Length; i++)
                difference |= token[i] ^ value[i];
            return difference == 0;
        }

        private void UpdateActiveTransferCount(int count)
        {
            string serial;
            lock (_sync) serial = _serial;
            if (!string.IsNullOrWhiteSpace(serial))
            {
                _runtimeSessions.SetPhoneToPcActiveTransfers(
                    serial,
                    count);
            }
        }

        private string ResolveDestinationFolder()
        {
            var configured =
                _settings.Paths.PhoneToPcReceiveFolder;
            if (string.IsNullOrWhiteSpace(configured))
                throw new InvalidOperationException(
                    "Phone-to-PC destination folder is empty.");
            var baseFolder = Path.GetFullPath(
                _settingsService.ResolvePath(
                    Environment.ExpandEnvironmentVariables(configured)));
            string deviceName;
            lock (_sync)
            {
                deviceName = _deviceDisplayName;
                if (string.IsNullOrWhiteSpace(deviceName))
                    deviceName = _serial;
            }
            return Path.Combine(
                baseFolder,
                SanitizeFileName(string.IsNullOrWhiteSpace(deviceName)
                    ? "Phone"
                    : deviceName));
        }

        private string ResolveDestinationFolderSafe()
        {
            try { return ResolveDestinationFolder(); }
            catch (Exception) { return string.Empty; }
        }

        private static string ResolveSafePath(
            string destination,
            int rootId,
            string relativePath,
            IDictionary<int, string> rootNames,
            ISet<string> reservedPaths,
            IDictionary<string, string> resolvedPaths)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new InvalidDataException("Empty relative path.");
            var rawParts = relativePath.Replace('\\', '/').Split('/');
            var originalParts = new List<string>();
            foreach (var rawPart in rawParts)
            {
                if (string.IsNullOrWhiteSpace(rawPart) ||
                    rawPart == ".")
                {
                    continue;
                }
                if (rawPart == "..")
                    throw new InvalidDataException(
                        "Parent path segments are not allowed.");
                originalParts.Add(rawPart);
            }
            if (originalParts.Count == 0)
                throw new InvalidDataException("Empty normalized path.");

            var parts = new List<string>(originalParts.Count);
            foreach (var originalPart in originalParts)
                parts.Add(SanitizeFileName(originalPart));

            string rootName;
            if (!rootNames.TryGetValue(rootId, out rootName))
            {
                rootName = Path.GetFileName(FindAvailablePath(
                    destination,
                    parts[0],
                    reservedPaths));
                rootNames[rootId] = rootName;
            }

            var path = Path.Combine(destination, rootName);
            var logicalPath = originalParts[0];
            resolvedPaths[rootId + ":0:" + logicalPath] = path;
            for (var index = 1; index < parts.Count; index++)
            {
                logicalPath += "/" + originalParts[index];
                var logicalKey = rootId + ":" + index + ":" + logicalPath;
                string resolvedPath;
                if (!resolvedPaths.TryGetValue(logicalKey, out resolvedPath))
                {
                    resolvedPath = FindAvailablePath(
                        path,
                        parts[index],
                        reservedPaths);
                    resolvedPaths[logicalKey] = resolvedPath;
                }
                path = resolvedPath;
            }

            var fullDestination = Path.GetFullPath(destination)
                .TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            var fullPath = Path.GetFullPath(path);
            if (!fullPath.StartsWith(
                fullDestination,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "The destination path escaped its root folder.");
            }
            return fullPath;
        }

        private static string FindAvailablePath(
            string parent,
            string requested,
            ISet<string> reserved)
        {
            var extension = Path.GetExtension(requested);
            var stem = string.IsNullOrEmpty(extension)
                ? requested
                : requested.Substring(0, requested.Length - extension.Length);
            var candidate = requested;
            var index = 1;
            var fullPath = Path.GetFullPath(Path.Combine(parent, candidate));
            while (File.Exists(fullPath) ||
                   Directory.Exists(fullPath) ||
                   reserved.Contains(fullPath))
            {
                candidate = stem + " (" + index + ")" + extension;
                index++;
                fullPath = Path.GetFullPath(Path.Combine(parent, candidate));
            }
            reserved.Add(fullPath);
            return fullPath;
        }

        private static string CreatePartialPath(
            string safePath,
            Guid identifier)
        {
            var directory = Path.GetDirectoryName(safePath);
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException(
                    "The destination file has no parent folder.",
                    "safePath");
            return Path.Combine(
                directory,
                ".dxpartial-" + identifier.ToString("N"));
        }

        private static string SanitizeFileName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);
            foreach (var character in value)
            {
                builder.Append(Array.IndexOf(invalid, character) >= 0 ||
                    char.IsControl(character) ? '_' : character);
            }
            var result = builder.ToString().Trim().TrimEnd('.', ' ');
            if (string.IsNullOrEmpty(result)) result = "unnamed";
            var baseName = Path.GetFileNameWithoutExtension(result);
            if (ReservedNames.Contains(baseName)) result = "_" + result;
            return result;
        }

        private static void CopyExact(
            Stream input,
            Stream output,
            int count,
            Action<int> progress)
        {
            var buffer = new byte[Math.Min(count, 64 * 1024)];
            var remaining = count;
            while (remaining > 0)
            {
                var read = input.Read(
                    buffer,
                    0,
                    Math.Min(buffer.Length, remaining));
                if (read <= 0) throw new EndOfStreamException();
                output.Write(buffer, 0, read);
                remaining -= read;
                progress(read);
            }
        }

        private static void TrySetModifiedTime(string path, long unixMillis)
        {
            if (unixMillis <= 0) return;
            try
            {
                var utc = new DateTime(
                    1970,
                    1,
                    1,
                    0,
                    0,
                    0,
                    DateTimeKind.Utc).AddMilliseconds(unixMillis);
                File.SetLastWriteTimeUtc(path, utc);
            }
            catch (ArgumentOutOfRangeException) { }
            catch (IOException) { }
        }

        private void ThrowIfShutdown()
        {
            if (_shutdown.IsCancellationRequested)
                throw new OperationCanceledException();
        }

        private void Publish(
            Guid batchId,
            PhoneTransferStage stage,
            string currentItem,
            int completedItems,
            int totalItems,
            long receivedBytes,
            long totalBytes,
            string destination,
            string error)
        {
            var handler = ProgressChanged;
            if (handler == null) return;
            handler(this, new PhoneTransferProgressEventArgs(
                new PhoneTransferProgress(
                    Interlocked.Increment(ref _sequence),
                    batchId,
                    stage,
                    currentItem,
                    completedItems,
                    totalItems,
                    receivedBytes,
                    totalBytes,
                    destination,
                    error)));
        }

        private static string CreateToken()
        {
            var bytes = new byte[32];
            using (var random = RandomNumberGenerator.Create())
                random.GetBytes(bytes);
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static string CombineOutput(ProcessResult result)
        {
            if (result == null) return "No process result.";
            var error = (result.StandardError ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(error)) return error;
            var output = (result.StandardOutput ?? string.Empty).Trim();
            return string.IsNullOrEmpty(output)
                ? "Exit code " + result.ExitCode
                : output;
        }
    }
}
