using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using DexManager.FileTransfer;
using DexManager.Models;
using DexManager.Utils;

namespace DexManager.Services
{
    public sealed partial class FileTransferCoordinator : IDisposable
    {
        private const int MaximumFileNameBytes = 255;
        private const int MaximumCollisionIndex = 9999;
        private const int CancelBurstMilliseconds = 1000;
        private const int ShortAdbTimeoutMs = 5000;
        private const int FinalCommitRecoveryAttempts = 6;
        private const int StaleCleanupCooldownMilliseconds = 5000;
        private const int ProcessPollMilliseconds = 100;
        private const int MaximumVisibleQueueItems = 5;
        private readonly object _syncRoot = new object();
        private readonly object _targetPreparationRoot = new object();
        private readonly string _realAdbPath;
        private readonly string _proxyPath;
        private readonly string _pipeName;
        private readonly string _pipeToken;
        private readonly AppSettings _settings;
        private readonly LogService _logService;
        private readonly DeviceRuntimeSessionRegistry _runtimeSessions;
        private readonly BlockingCollection<TransferWorkItem> _queue =
            new BlockingCollection<TransferWorkItem>(
                new ConcurrentQueue<TransferWorkItem>());
        private readonly Dictionary<string, TransferSession> _sessions =
            new Dictionary<string, TransferSession>(
                StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TransferWorkItem> _requests =
            new Dictionary<string, TransferWorkItem>(
                StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<NamedPipeServerStream> _connectedPipes =
            new HashSet<NamedPipeServerStream>();
        private readonly Thread _acceptThread;
        private readonly Thread _workerThread;
        private NamedPipeServerStream _waitingPipe;
        private TransferWorkItem _activeItem;
        private Process _activeAdbProcess;
        private int _shutdownRequested;
        private int _disposed;
        private bool _proxyMissingLogged;
        private DateTime _lastStaleCleanupUtc = DateTime.MinValue;
        private long _progressSequence;

        public FileTransferCoordinator(
            string realAdbPath,
            AppSettings settings,
            LogService logService,
            DeviceRuntimeSessionRegistry runtimeSessions,
            string proxyPath = null)
        {
            if (string.IsNullOrWhiteSpace(realAdbPath))
                throw new ArgumentException("ADB path is empty.", "realAdbPath");
            _realAdbPath = Path.GetFullPath(realAdbPath);
            _settings = settings ?? throw new ArgumentNullException("settings");
            _logService = logService ?? throw new ArgumentNullException("logService");
            _runtimeSessions = runtimeSessions ??
                throw new ArgumentNullException("runtimeSessions");

            if (!string.IsNullOrWhiteSpace(proxyPath))
            {
                _proxyPath = Path.GetFullPath(proxyPath);
            }
            else
            {
                var proxyDir = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "tools",
                    "adb-proxy");
                _proxyPath = AdbProxyLocator.Resolve(
                    proxyDir,
                    AppDomain.CurrentDomain.BaseDirectory);
            }
            _pipeName = "DXManager.Transfer." +
                Process.GetCurrentProcess().Id.ToString(
                    CultureInfo.InvariantCulture) + "." +
                Guid.NewGuid().ToString("N");
            _pipeToken = CreateToken();

            _acceptThread = new Thread(AcceptLoop)
            {
                IsBackground = true,
                Name = "DX Manager file-transfer IPC"
            };
            _workerThread = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "DX Manager file-transfer worker"
            };
            _acceptThread.Start();
            _workerThread.Start();
        }

        public event EventHandler<FileTransferProgressEventArgs> ProgressChanged;

        public string GetScrcpyPushTarget()
        {
            return NormalizeRemoteDirectory(
                _settings.Paths.FileTransferTargetFolder) + "/";
        }

        public string PrepareScrcpyPushTarget(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial)) return GetScrcpyPushTarget();
            lock (_targetPreparationRoot)
            {
                var directory = NormalizeRemoteDirectory(
                    _settings.Paths.FileTransferTargetFolder);
                if (CanCleanupStaleTransferArtifacts())
                    CleanupStaleRemoteTransferArtifacts(serial, directory);
                try
                {
                    var script = new StringBuilder();
                    script.AppendLine("set -e");
                    AppendDecodedVariable(script, "dir", directory);
                    script.AppendLine("mkdir -p \"$dir\"");
                    script.AppendLine("[ -d \"$dir\" ]");
                    var result = RunCleanupScript(serial, script);
                    if (result.ExitCode == 0) return directory + "/";
                    var detail = string.IsNullOrWhiteSpace(result.ErrorTail)
                        ? result.OutputTail
                        : result.ErrorTail;
                    _logService.Warning(LocalizationService.Format(
                        "Log.FileTransfer.TargetPrepareFailed",
                        directory,
                        detail));
                }
                catch (Exception ex)
                {
                    _logService.Warning(LocalizationService.Format(
                        "Log.FileTransfer.TargetPrepareFailed",
                        directory,
                        ex.Message));
                }
                return directory + "/";
            }
        }

        private bool CanCleanupStaleTransferArtifacts()
        {
            lock (_syncRoot)
            {
                return _sessions.Count == 0 &&
                    _requests.Count == 0 &&
                    (DateTime.UtcNow - _lastStaleCleanupUtc)
                        .TotalMilliseconds >=
                        StaleCleanupCooldownMilliseconds;
            }
        }

        private void CleanupStaleRemoteTransferArtifacts(
            string serial,
            string directory)
        {
            try
            {
                var script = new StringBuilder();
                script.AppendLine("set -e");
                AppendDecodedVariable(script, "dir", directory);
                script.AppendLine("rm -f /sdcard/.dxm-file-*.part");
                script.AppendLine("for path in \"$dir\"/.dxm-dir-*.part; do");
                script.AppendLine("  [ -e \"$path\" ] || continue");
                script.AppendLine("  rm -rf \"$path\"");
                script.AppendLine("done");
                script.AppendLine(
                    "rm -f /data/local/tmp/.dxm-commit-*.result");
                var result = RunCleanupScript(serial, script);
                if (result.ExitCode == 0) return;
                _logService.Warning(LocalizationService.Format(
                    "Log.FileTransfer.TempCleanupFailed",
                    GetCleanupFailure(directory, result)));
            }
            catch (Exception ex)
            {
                _logService.Warning(LocalizationService.Format(
                    "Log.FileTransfer.TempCleanupFailed",
                    ex.Message));
            }
            finally
            {
                lock (_syncRoot) _lastStaleCleanupUtc = DateTime.UtcNow;
            }
        }

        public string BeginSession(
            string serial,
            string displayName,
            string remoteDirectory)
        {
            if (string.IsNullOrWhiteSpace(serial)) return string.Empty;
            if (!_settings.Features.ManagedFileTransferEnabled)
                return string.Empty;
            if (Interlocked.CompareExchange(
                    ref _shutdownRequested,
                    0,
                    0) != 0)
            {
                return string.Empty;
            }
            if (!File.Exists(_proxyPath))
            {
                lock (_syncRoot)
                {
                    if (!_proxyMissingLogged)
                    {
                        _proxyMissingLogged = true;
                        _logService.Warning(LocalizationService.Format(
                            "Log.FileTransfer.ProxyMissing",
                            _proxyPath));
                    }
                }
                return string.Empty;
            }

            var id = Guid.NewGuid().ToString("N");
            lock (_syncRoot)
            {
                _sessions[id] = new TransferSession(
                    id,
                    serial,
                    displayName,
                    NormalizeRemoteDirectory(remoteDirectory));
            }
            PublishTransferState(serial);
            return id;
        }

        public void ConfigureScrcpyProcess(
            ProcessStartInfo startInfo,
            string sessionId)
        {
            if (startInfo == null) throw new ArgumentNullException("startInfo");
            TransferSession session;
            lock (_syncRoot)
            {
                if (string.IsNullOrWhiteSpace(sessionId) ||
                    !_sessions.TryGetValue(sessionId, out session) ||
                    !session.Active)
                {
                    startInfo.EnvironmentVariables["ADB"] = _realAdbPath;
                    return;
                }
            }

            startInfo.EnvironmentVariables["ADB"] = _proxyPath;
            startInfo.EnvironmentVariables[
                FileTransferEnvironment.RealAdbPath] = _realAdbPath;
            startInfo.EnvironmentVariables[
                FileTransferEnvironment.PipeName] = _pipeName;
            startInfo.EnvironmentVariables[
                FileTransferEnvironment.PipeToken] = _pipeToken;
            startInfo.EnvironmentVariables[
                FileTransferEnvironment.SessionId] = session.Id;
            startInfo.EnvironmentVariables[
                FileTransferEnvironment.SessionSerial] = session.Serial;
            startInfo.EnvironmentVariables[
                FileTransferEnvironment.RemoteDirectory] =
                    session.RemoteDirectory + "/";
            startInfo.EnvironmentVariables[
                FileTransferEnvironment.Enabled] = "1";
        }

        public void BindProcess(string sessionId, int processId)
        {
            lock (_syncRoot)
            {
                TransferSession session;
                if (_sessions.TryGetValue(sessionId ?? string.Empty,
                    out session))
                {
                    session.ProcessId = processId;
                }
            }
        }

        public void BindWindow(string sessionId, IntPtr windowHandle)
        {
            lock (_syncRoot)
            {
                TransferSession session;
                if (_sessions.TryGetValue(sessionId ?? string.Empty,
                    out session))
                {
                    session.WindowHandle = windowHandle;
                }
            }
        }

        public IntPtr GetWindowHandle(string sessionId)
        {
            lock (_syncRoot)
            {
                TransferSession session;
                return _sessions.TryGetValue(sessionId ?? string.Empty,
                    out session)
                    ? session.WindowHandle
                    : IntPtr.Zero;
            }
        }

        public void EndSession(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) return;
            TransferSession session;
            lock (_syncRoot)
            {
                if (!_sessions.TryGetValue(sessionId, out session)) return;
                session.Active = false;
                session.WindowHandle = IntPtr.Zero;
            }
            CancelSessionRequests(sessionId, false);
            lock (_syncRoot) _sessions.Remove(sessionId);
            PublishTransferState(session.Serial);
        }

        public void CancelSerial(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial)) return;
            string[] sessions;
            TransferWorkItem[] requests;
            lock (_syncRoot)
            {
                sessions = _sessions.Values
                    .Where(item => DeviceSerialScope.Matches(
                        serial,
                        item.Serial))
                    .Select(item => item.Id)
                    .ToArray();
                var sessionSet = new HashSet<string>(
                    sessions,
                    StringComparer.OrdinalIgnoreCase);
                requests = _requests.Values
                    .Where(item => sessionSet.Contains(
                        item.Request.SessionId) &&
                        !item.IsTerminal)
                    .ToArray();
            }
            if (requests.Length > 0)
            {
                _logService.Warning(LocalizationService.Format(
                    "Log.FileTransfer.DeviceDisconnected",
                    serial,
                    requests.Length));
            }
            foreach (var sessionId in sessions)
                CancelSessionRequests(sessionId, false);
        }

        public void CancelTransfer(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId)) return;
            TransferWorkItem item;
            lock (_syncRoot)
                _requests.TryGetValue(requestId, out item);
            if (item != null)
                CancelSessionRequests(item.Request.SessionId, true);
        }

        private void PublishTransferState(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial)) return;
            var activeSessions = 0;
            var queuedItems = 0;
            lock (_syncRoot)
            {
                foreach (var session in _sessions.Values)
                {
                    if (session.Active && DeviceSerialScope.Matches(
                            serial,
                            session.Serial)) activeSessions++;
                }
                foreach (var request in _requests.Values)
                {
                    if (!request.IsTerminal &&
                        DeviceSerialScope.Matches(
                            serial,
                            request.Session.Serial)) queuedItems++;
                }
            }
            _runtimeSessions.SetPcToPhoneTransferState(
                serial,
                activeSessions,
                queuedItems);
        }

        public void RequestShutdown()
        {
            if (Interlocked.Exchange(ref _shutdownRequested, 1) != 0) return;

            NamedPipeServerStream waiting;
            NamedPipeServerStream[] connected;
            lock (_syncRoot)
            {
                waiting = _waitingPipe;
                connected = _connectedPipes.ToArray();
                foreach (var session in _sessions.Values)
                    session.Active = false;
            }
            if (waiting != null)
            {
                try { waiting.Dispose(); }
                catch { }
            }
            foreach (var pipe in connected)
            {
                try { pipe.Dispose(); }
                catch { }
            }

            TransferWorkItem active;
            lock (_syncRoot) active = _activeItem;
            if (active != null) CancelItem(active);
            foreach (var item in _queue.ToArray()) CancelItem(item);
            _queue.CompleteAdding();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            RequestShutdown();
            if (Thread.CurrentThread != _acceptThread)
                _acceptThread.Join(2000);
            var workerStopped = true;
            if (Thread.CurrentThread != _workerThread)
                workerStopped = _workerThread.Join(7000);
            if (workerStopped) _queue.Dispose();
        }
    }
}
