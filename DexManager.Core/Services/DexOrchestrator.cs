using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DexManager.Models;

namespace DexManager.Services
{
    public sealed class DexOrchestrator
    {
        private readonly AdbService _adbService;
        private readonly VirtualDisplayService _virtualDisplayService;
        private readonly ScrcpyService _scrcpyService;
        private readonly ScrcpyLaunchCoordinator _launchCoordinator;
        private readonly SettingsService _settingsService;
        private readonly LogService _logService;
        private readonly AppSettings _settings;
        private readonly DeviceRuntimeSessionRegistry _runtimeSessions;
        private readonly object _operationGate = new object();
        private readonly object _shutdownTaskLock = new object();
        private readonly ManualResetEvent _shutdownSignal =
            new ManualResetEvent(false);
        private int _shutdownRequested;
        private Task _shutdownTask;
        private ManagedDisplaySession _currentSession;
        private readonly List<DeferredDisplayCleanup>
            _pendingDisplayCleanup =
                new List<DeferredDisplayCleanup>();

        public DexOrchestrator(
            AdbService adbService,
            VirtualDisplayService virtualDisplayService,
            ScrcpyService scrcpyService,
            ScrcpyLaunchCoordinator launchCoordinator,
            SettingsService settingsService,
            LogService logService,
            AppSettings settings,
            DeviceRuntimeSessionRegistry runtimeSessions)
        {
            _adbService = adbService;
            _virtualDisplayService = virtualDisplayService;
            _scrcpyService = scrcpyService;
            _launchCoordinator = launchCoordinator;
            _settingsService = settingsService;
            _logService = logService;
            _settings = settings;
            _runtimeSessions = runtimeSessions ??
                throw new ArgumentNullException("runtimeSessions");
            _scrcpyService.RunningChanged +=
                ScrcpyService_RunningChanged;
        }

        public bool IsRunning
        {
            get { return _scrcpyService.IsRunning; }
        }

        public ManagedDisplaySession CurrentSession
        {
            get { return _currentSession; }
        }

        public bool HasDeferredDisplayCleanup
        {
            get
            {
                lock (_operationGate)
                {
                    return _pendingDisplayCleanup.Count > 0;
                }
            }
        }

        public bool IsCleanupComplete
        {
            get
            {
                lock (_operationGate)
                {
                    return !_scrcpyService.IsRunning &&
                        _currentSession == null &&
                        _pendingDisplayCleanup.Count == 0;
                }
            }
        }

        public bool IsShutdownRequested
        {
            get
            {
                return Interlocked.CompareExchange(
                    ref _shutdownRequested,
                    0,
                    0) != 0;
            }
        }

        public async Task<bool> StartAsync(
            string serial,
            string deviceIdentity,
            CancellationToken cancellationToken)
        {
            using (cancellationToken.Register(RequestShutdown))
            {
                var started = await Task.Run(delegate
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    lock (_operationGate)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        return StartCore(serial, deviceIdentity);
                    }
                }, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return started;
            }
        }

        public Task StopAsync()
        {
            return Task.Run(delegate
            {
                lock (_operationGate) StopCore();
            });
        }

        public Task<bool> StopOrConfirmCleanupAsync()
        {
            return Task.Run(delegate
            {
                lock (_operationGate)
                {
                    if (!_scrcpyService.IsRunning &&
                        _currentSession == null)
                    {
                        return _pendingDisplayCleanup.Count == 0;
                    }
                    StopCore();
                    return _pendingDisplayCleanup.Count == 0;
                }
            });
        }

        public Task<bool> RetryDeferredCleanupAsync(
            string serial,
            string deviceIdentity)
        {
            return Task.Run(delegate
            {
                lock (_operationGate)
                    return RetryDeferredCleanupCore(
                        serial,
                        deviceIdentity);
            });
        }

        public Task<bool> CleanupConnectedOverlayAsync(
            string serial,
            string deviceIdentity)
        {
            return Task.Run(delegate
            {
                lock (_operationGate)
                {
                    string verifiedIdentity;
                    if (!CleanupConnectedTargetOverlay(
                        serial,
                        deviceIdentity,
                        out verifiedIdentity))
                        return false;
                    CompleteDeferredCleanupCore(
                        serial,
                        verifiedIdentity);
                    return true;
                }
            });
        }

        public void RequestShutdown()
        {
            _scrcpyService.RequestShutdown();
            if (Interlocked.Exchange(ref _shutdownRequested, 1) == 0)
                _shutdownSignal.Set();
        }

        public Task ShutdownAsync(
            string fallbackSerial,
            string fallbackIdentity)
        {
            RequestShutdown();
            lock (_shutdownTaskLock)
            {
                if (_shutdownTask == null ||
                    _shutdownTask.IsFaulted ||
                    _shutdownTask.IsCanceled)
                {
                    _shutdownTask = Task.Run(delegate
                    {
                        lock (_operationGate)
                            ShutdownCore(
                                fallbackSerial,
                                fallbackIdentity);
                    });
                }
                return _shutdownTask;
            }
        }

        public Task<bool> ApplyRuntimeSettingsAsync()
        {
            return Task.Run(delegate
            {
                lock (_operationGate)
                    return ApplyRuntimeSettingsCore();
            });
        }

        private bool StartCore(
            string requestedSerial,
            string deviceIdentity)
        {
            if (IsShutdownRequested) return false;
            if (_scrcpyService.IsRunning)
            {
                _logService.Warning(LocalizationService.Get(
                    "Log.Dex.AlreadyRunning"));
                return false;
            }

            var serial = requestedSerial;
            if (string.IsNullOrWhiteSpace(serial) ||
                !_adbService.IsAuthorizedDeviceConnected(serial))
            {
                throw new InvalidOperationException(
                    LocalizationService.Get(
                        "Error.Dex.NoAuthorizedDevice"));
            }
            deviceIdentity = GetVerifiedDeviceIdentity(
                serial,
                deviceIdentity);
            if (string.IsNullOrWhiteSpace(deviceIdentity))
            {
                throw new InvalidOperationException(
                    "The physical-device identity could not be verified. " +
                    "Keep the phone connected and unlocked, then try again.");
            }
            CleanupStaleSession(serial, deviceIdentity);
            var runSettings = GetDeviceRunSettings(deviceIdentity);

            VirtualDisplayLease lease = null;
            var scrcpyStarted = false;
            try
            {
                _launchCoordinator.RunExclusive(delegate
                {
                    ThrowIfShutdownRequested();
                    lease = _virtualDisplayService.EnsureVirtualDisplay(
                        serial,
                        runSettings.VirtualDisplay,
                        _settings.Timing.VirtualDisplayDetectionTimeoutMs,
                        delegate { return IsShutdownRequested; });
                    CompleteDeferredCleanupCore(
                        serial,
                        deviceIdentity);
                    ThrowIfShutdownRequested();
                    _scrcpyService.Start(
                        runSettings.Scrcpy,
                        lease.DisplayId,
                        serial);
                    scrcpyStarted = true;
                });

                if (IsShutdownRequested)
                    throw new OperationCanceledException();

                if (!_scrcpyService.IsRunning)
                    throw new InvalidOperationException(
                        LocalizationService.Get(
                            "Error.Scrcpy.ExitedBeforeWindow"));

                TrackSession(
                    "DeX",
                    serial,
                    deviceIdentity,
                    lease);
                if (!_scrcpyService.IsRunning)
                    throw new InvalidOperationException(
                        LocalizationService.Get(
                            "Error.Scrcpy.ExitedBeforeWindow"));
                try
                {
                    SaveLastSuccess(
                        serial,
                        deviceIdentity,
                        lease.DisplayId);
                }
                catch (Exception saveException)
                {
                    _logService.Error(
                        LocalizationService.Get(
                            "Log.Dex.LastSuccessSaveFailed"),
                        saveException);
                }
                _logService.Info(LocalizationService.Get(
                    "Log.Dex.StartCompleted"));
                return true;
            }
            catch (OperationCanceledException ex)
            {
                lease = GetRetainedLease(ex, lease);
                CleanupFailedStart(
                    scrcpyStarted,
                    lease,
                    deviceIdentity);
                _logService.Info(LocalizationService.Get(
                    "Log.Dex.StartCancelled"));
                throw;
            }
            catch (Exception ex)
            {
                lease = GetRetainedLease(ex, lease);
                CleanupFailedStart(
                    scrcpyStarted,
                    lease,
                    deviceIdentity);
                _logService.Error(
                    LocalizationService.Get("Log.Dex.StartFailed"),
                    ex);
                throw;
            }
        }

        private void StopCore()
        {
            var session = _currentSession;
            Exception stopException = null;
            try
            {
                _scrcpyService.Stop();
            }
            catch (Exception ex)
            {
                stopException = ex;
                _logService.Error(
                    LocalizationService.Get(
                        "Log.Dex.StopProcessFailed"),
                    ex);
            }

            if (_scrcpyService.IsRunning)
            {
                if (stopException != null) throw stopException;
                throw new InvalidOperationException(
                    LocalizationService.Get(
                        "Error.Scrcpy.StopTimeout"));
            }

            if (session != null)
            {
                if (!ReleaseDisplayLease(
                    session.DisplayLease,
                    session.DeviceIdentity))
                    DeferDisplayCleanup(session);
            }
            ClearSession(session);
            _logService.Info(LocalizationService.Get(
                "Log.Dex.StopCleanupCompleted"));
            if (stopException != null) throw stopException;
        }

        private bool ApplyRuntimeSettingsCore()
        {
            if (IsShutdownRequested) return false;
            try
            {
                var serial = _currentSession == null
                    ? string.Empty
                    : _currentSession.Serial;
                if (string.IsNullOrWhiteSpace(serial) ||
                    !_adbService.IsAuthorizedDeviceConnected(serial))
                {
                    _logService.Warning(LocalizationService.Get(
                        "Log.Dex.ApplyDeferredNoDevice"));
                    return false;
                }

                _logService.Info(LocalizationService.Get(
                    "Log.Dex.RemovingDisplayForApply"));
                var session = _currentSession;
                _scrcpyService.Stop();
                if (session != null &&
                    !ReleaseDisplayLease(
                        session.DisplayLease,
                        session.DeviceIdentity))
                {
                    DeferDisplayCleanup(session);
                    throw new InvalidOperationException(
                        LocalizationService.Get(
                            "Error.Dex.DisplayResetFailed"));
                }
                ClearSession(session);

                if (_shutdownSignal.WaitOne(1000)) return false;
                var deviceIdentity = session == null
                    ? string.Empty
                    : session.DeviceIdentity;
                if (!StartCore(serial, deviceIdentity)) return false;
                if (!_scrcpyService.IsRunning) return false;
                _logService.Info(LocalizationService.Get(
                    "Log.Dex.ApplyCompleted"));
                return true;
            }
            catch (Exception ex)
            {
                _logService.Error(
                    LocalizationService.Get("Log.Dex.ApplyFailed"),
                    ex);
                throw;
            }
        }

        private void ShutdownCore(
            string fallbackSerial,
            string fallbackIdentity)
        {
            var session = _currentSession;
            Exception stopException = null;
            try
            {
                _scrcpyService.Stop();
            }
            catch (Exception ex)
            {
                stopException = ex;
                _logService.Error(
                    LocalizationService.Get(
                        "Log.Dex.StopProcessFailed"),
                    ex);
            }

            if (_scrcpyService.IsRunning)
            {
                if (stopException != null) throw stopException;
                throw new InvalidOperationException(
                    LocalizationService.Get(
                        "Error.Scrcpy.StopTimeout"));
            }

            if (session != null &&
                !ReleaseDisplayLease(
                    session.DisplayLease,
                    session.DeviceIdentity))
            {
                DeferDisplayCleanup(session);
            }
            else if (session == null)
            {
                string verifiedIdentity;
                if (!CleanupConnectedTargetOverlay(
                    fallbackSerial,
                    fallbackIdentity,
                    out verifiedIdentity))
                {
                    throw new InvalidOperationException(
                        LocalizationService.Get(
                            "Error.Dex.DisplayResetFailed"));
                }
                CompleteDeferredCleanupCore(
                    fallbackSerial,
                    verifiedIdentity);
            }
            ClearSession(session);
            _logService.Info(LocalizationService.Get(
                "Log.Dex.ShutdownCleanupCompleted"));
            if (stopException != null) throw stopException;
        }

        private void ScrcpyService_RunningChanged(
            object sender,
            EventArgs e)
        {
            if (_scrcpyService.IsRunning) return;
            Task.Run(delegate
            {
                lock (_operationGate)
                {
                    if (!_scrcpyService.IsRunning)
                        CleanupNaturallyEndedSession();
                }
            });
        }

        private void CleanupNaturallyEndedSession()
        {
            var session = _currentSession;
            if (session == null) return;
            if (!ReleaseDisplayLease(
                session.DisplayLease,
                session.DeviceIdentity))
            {
                DeferDisplayCleanup(session);
                _logService.Warning(LocalizationService.Get(
                    "Log.Dex.NaturalExitCleanupDeferred"));
                return;
            }

            ClearSession(session);
            _logService.Info(LocalizationService.Get(
                "Log.Dex.NaturalExitCleanupCompleted"));
        }

        private void CleanupStaleSession(
            string nextSerial,
            string nextDeviceIdentity)
        {
            var stale = _currentSession;
            if (stale == null) return;
            if (string.Equals(
                stale.Serial,
                nextSerial,
                StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    stale.DeviceIdentity,
                    nextDeviceIdentity,
                    StringComparison.OrdinalIgnoreCase))
            {
                // A reconnect must be evaluated by EnsureVirtualDisplay.
                // Preserve the cleanup evidence until that comparison and
                // any required recreation have actually succeeded.
                DeferDisplayCleanup(stale);
                return;
            }
            if (ReleaseDisplayLease(
                stale.DisplayLease,
                stale.DeviceIdentity))
            {
                ClearSession(stale);
                return;
            }

            DeferDisplayCleanup(stale);
            if (string.Equals(
                stale.Serial,
                nextSerial,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    LocalizationService.Get(
                        "Error.Dex.DisplayResetFailed"));
            }
        }

        private void CleanupFailedStart(
            bool scrcpyStarted,
            VirtualDisplayLease lease,
            string deviceIdentity)
        {
            if (scrcpyStarted || _scrcpyService.IsRunning)
            {
                try
                {
                    _scrcpyService.Stop();
                }
                catch (Exception cleanupException)
                {
                    _logService.Error(
                        LocalizationService.Get(
                            "Log.Dex.StopProcessFailed"),
                        cleanupException);
                }
            }

            if (_scrcpyService.IsRunning)
            {
                if (_currentSession == null && lease != null)
                    TrackSession(
                        "DeX",
                        lease.Serial,
                        deviceIdentity,
                        lease);
                return;
            }

            try
            {
                if (lease != null &&
                    !ReleaseDisplayLease(
                        lease,
                        deviceIdentity))
                {
                    DeferDisplayCleanup(
                        lease,
                        deviceIdentity);
                    ClearSession(_currentSession);
                    return;
                }
                ClearSession(_currentSession);
            }
            catch (Exception cleanupException)
            {
                _logService.Error(
                    LocalizationService.Get(
                        "Log.Dex.ShutdownCleanupFailed"),
                    cleanupException);
            }
        }

        private bool RetryDeferredCleanupCore(
            string serial,
            string deviceIdentity)
        {
            if (string.IsNullOrWhiteSpace(serial)) return true;
            var verifiedIdentity = GetVerifiedDeviceIdentity(
                serial,
                deviceIdentity);
            // Keep the entry until EnsureVirtualDisplay or an explicit Reset
            // succeeds for this verified physical device.
            return !string.IsNullOrWhiteSpace(verifiedIdentity);
        }

        private void CompleteDeferredCleanupCore(
            string serial,
            string deviceIdentity)
        {
            var pendingEntries = GetMatchingDeferredCleanupEntries(
                serial,
                deviceIdentity);
            if (pendingEntries.Count == 0) return;
            RemoveDeferredCleanupEntries(pendingEntries);
            _logService.Info(LocalizationService.Format(
                "Log.Dex.DeferredCleanupCompleted",
                serial));
        }

        private List<DeferredDisplayCleanup>
            GetMatchingDeferredCleanupEntries(
            string serial,
            string deviceIdentity)
        {
            var matches = new List<DeferredDisplayCleanup>();
            if (!HasStableIdentity(deviceIdentity)) return matches;
            foreach (var pending in _pendingDisplayCleanup)
            {
                if (HasStableIdentity(pending.DeviceIdentity) &&
                    string.Equals(
                        pending.DeviceIdentity,
                        deviceIdentity,
                        StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(pending);
                }
            }
            return matches;
        }

        private void RemoveDeferredCleanupEntries(
            IList<DeferredDisplayCleanup> entries)
        {
            for (var index = 0; index < entries.Count; index++)
                _pendingDisplayCleanup.Remove(entries[index]);
        }

        private static bool HasStableIdentity(string identity)
        {
            return !string.IsNullOrWhiteSpace(identity) &&
                !PhysicalDeviceRegistry.IsTemporaryIdentity(identity);
        }

        private bool ReleaseDisplayLease(
            VirtualDisplayLease lease,
            string expectedDeviceIdentity)
        {
            if (lease == null) return true;
            string verifiedIdentity;
            var cleanupSerial = FindVerifiedCleanupTransport(
                lease.Serial,
                expectedDeviceIdentity,
                out verifiedIdentity);
            if (string.IsNullOrWhiteSpace(cleanupSerial))
                return false;

            if (string.Equals(
                cleanupSerial,
                lease.Serial,
                StringComparison.OrdinalIgnoreCase))
            {
                return _virtualDisplayService.Release(lease);
            }

            var reset = _virtualDisplayService.Reset(cleanupSerial);
            if (reset) lease.OwnsOverlaySetting = false;
            return reset;
        }

        private bool CleanupConnectedTargetOverlay(
            string serial,
            string expectedDeviceIdentity,
            out string verifiedDeviceIdentity)
        {
            var cleanupSerial = FindVerifiedCleanupTransport(
                serial,
                expectedDeviceIdentity,
                out verifiedDeviceIdentity);
            if (string.IsNullOrWhiteSpace(cleanupSerial))
                return false;

            return _virtualDisplayService.Reset(cleanupSerial);
        }

        private string FindVerifiedCleanupTransport(
            string preferredSerial,
            string expectedDeviceIdentity,
            out string verifiedDeviceIdentity)
        {
            verifiedDeviceIdentity = GetVerifiedDeviceIdentity(
                preferredSerial,
                expectedDeviceIdentity);
            if (!string.IsNullOrWhiteSpace(verifiedDeviceIdentity))
                return preferredSerial;

            if (!HasStableIdentity(expectedDeviceIdentity))
                return string.Empty;

            IList<AdbDeviceInfo> devices;
            if (!_adbService.TryGetDevices(false, out devices) ||
                devices == null)
            {
                return string.Empty;
            }

            foreach (var device in devices)
            {
                if (device == null ||
                    device.Status != AdbDeviceStatus.Device ||
                    string.IsNullOrWhiteSpace(device.Serial) ||
                    string.Equals(
                        device.Serial,
                        preferredSerial,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var liveIdentity = _adbService.GetDeviceIdentity(
                    device.Serial);
                if (!string.Equals(
                    liveIdentity,
                    expectedDeviceIdentity,
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                verifiedDeviceIdentity = liveIdentity;
                return device.Serial;
            }
            return string.Empty;
        }

        private string GetVerifiedDeviceIdentity(
            string serial,
            string expectedDeviceIdentity)
        {
            if (string.IsNullOrWhiteSpace(serial) ||
                !_adbService.IsAuthorizedDeviceConnected(serial))
            {
                return string.Empty;
            }

            var liveIdentity = _adbService.GetDeviceIdentity(serial);
            if (!HasStableIdentity(liveIdentity)) return string.Empty;
            if (HasStableIdentity(expectedDeviceIdentity) &&
                !string.Equals(
                    expectedDeviceIdentity,
                    liveIdentity,
                    StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }
            return liveIdentity;
        }

        private void DeferDisplayCleanup(ManagedDisplaySession session)
        {
            if (session == null) return;
            DeferDisplayCleanup(
                session.DisplayLease,
                session.DeviceIdentity);
            ClearSession(session);
        }

        private void DeferDisplayCleanup(
            VirtualDisplayLease lease,
            string deviceIdentity)
        {
            if (lease == null || string.IsNullOrWhiteSpace(lease.Serial))
                return;
            var normalizedIdentity = deviceIdentity ?? string.Empty;
            for (var index = 0;
                index < _pendingDisplayCleanup.Count;
                index++)
            {
                var pending = _pendingDisplayCleanup[index];
                if (string.Equals(
                        pending.Lease.Serial,
                        lease.Serial,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        pending.DeviceIdentity,
                        normalizedIdentity,
                        StringComparison.OrdinalIgnoreCase))
                {
                    pending.Lease = lease;
                    _logService.Warning(LocalizationService.Format(
                        "Log.Dex.DeferredCleanupStored",
                        lease.Serial));
                    return;
                }
            }
            _pendingDisplayCleanup.Add(new DeferredDisplayCleanup
            {
                DeviceIdentity = normalizedIdentity,
                Lease = lease
            });
            _logService.Warning(LocalizationService.Format(
                "Log.Dex.DeferredCleanupStored",
                lease.Serial));
        }

        private sealed class DeferredDisplayCleanup
        {
            public string DeviceIdentity { get; set; }
            public VirtualDisplayLease Lease { get; set; }
        }

        private static VirtualDisplayLease GetRetainedLease(
            Exception error,
            VirtualDisplayLease current)
        {
            if (current != null || error == null) return current;
            return error.Data[VirtualDisplayService.RetainedLeaseDataKey]
                as VirtualDisplayLease;
        }

        private void ThrowIfShutdownRequested()
        {
            if (IsShutdownRequested)
                throw new OperationCanceledException();
        }

        private void SaveLastSuccess(
            string serial,
            string deviceIdentity,
            int displayId)
        {
            _settingsService.UpdateAndSave(_settings, delegate(
                AppSettings settings)
            {
                var runSettings = GetDeviceRunSettings(
                    settings,
                    deviceIdentity);
                runSettings.LastSuccess.Width =
                    runSettings.VirtualDisplay.Width;
                runSettings.LastSuccess.Height =
                    runSettings.VirtualDisplay.Height;
                runSettings.LastSuccess.Dpi =
                    runSettings.VirtualDisplay.Dpi;
                runSettings.LastSuccess.AdbPath = _adbService.AdbPath;
                runSettings.LastSuccess.ScrcpyPath =
                    _scrcpyService.ScrcpyPath;
                runSettings.LastSuccess.ScrcpyArguments =
                    _scrcpyService.BuildArguments(
                        runSettings.Scrcpy,
                        displayId,
                        serial);
                runSettings.LastSuccess.DisplayId = displayId;
                runSettings.LastSuccess.SavedAtUtc =
                    DateTime.UtcNow.ToString("o");
            });
        }

        private void TrackSession(
            string mode,
            string serial,
            string deviceIdentity,
            VirtualDisplayLease lease)
        {
            _currentSession = new ManagedDisplaySession
            {
                Mode = mode,
                Serial = serial,
                DeviceIdentity = deviceIdentity ?? string.Empty,
                AppPackage = GetDeviceRunSettings(deviceIdentity)
                    .Scrcpy.StartAppPackage,
                DisplayId = lease.DisplayId,
                ScrcpyProcessId = _scrcpyService.CurrentProcessId,
                CreatedAtUtc = DateTime.UtcNow.ToString("o"),
                DisplayLease = lease
            };
            _runtimeSessions.SetDexSession(serial, _currentSession);
            _logService.Info(LocalizationService.Format(
                "Log.Dex.SessionStarted",
                _currentSession));
        }

        private DeviceRunSettingsProfile GetDeviceRunSettings(
            string deviceIdentity)
        {
            return GetDeviceRunSettings(
                _settings,
                deviceIdentity);
        }

        private DeviceRunSettingsProfile GetDeviceRunSettings(
            AppSettings settings,
            string deviceIdentity)
        {
            if (!string.IsNullOrWhiteSpace(deviceIdentity))
                return settings.GetOrCreateDeviceRunSettings(
                    deviceIdentity);
            return new DeviceRunSettingsProfile
            {
                DeviceIdentity = string.Empty,
                VirtualDisplay = settings.VirtualDisplay,
                Scrcpy = settings.Scrcpy,
                LastSuccess = settings.LastSuccess,
                SingleWindowSlots = settings.SingleWindowSlots,
                SingleWindowAppProfiles =
                    settings.SingleWindowAppProfiles
            };
        }

        private void ClearSession(ManagedDisplaySession session)
        {
            if (session == null ||
                !ReferenceEquals(_currentSession, session)) return;
            _logService.Info(LocalizationService.Format(
                "Log.Dex.SessionEnded",
                session));
            _runtimeSessions.SetDexSession(session.Serial, null);
            _currentSession = null;
        }
    }
}
