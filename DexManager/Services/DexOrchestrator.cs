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
        private readonly object _operationGate = new object();
        private readonly object _shutdownTaskLock = new object();
        private readonly ManualResetEvent _shutdownSignal =
            new ManualResetEvent(false);
        private int _shutdownRequested;
        private Task _shutdownTask;
        private ManagedDisplaySession _currentSession;
        private readonly Dictionary<string, VirtualDisplayLease>
            _pendingDisplayCleanup =
                new Dictionary<string, VirtualDisplayLease>(
                    StringComparer.OrdinalIgnoreCase);
        private int _naturalExitCleanupScheduled;

        public DexOrchestrator(
            AdbService adbService,
            VirtualDisplayService virtualDisplayService,
            ScrcpyService scrcpyService,
            ScrcpyLaunchCoordinator launchCoordinator,
            SettingsService settingsService,
            LogService logService,
            AppSettings settings)
        {
            _adbService = adbService;
            _virtualDisplayService = virtualDisplayService;
            _scrcpyService = scrcpyService;
            _launchCoordinator = launchCoordinator;
            _settingsService = settingsService;
            _logService = logService;
            _settings = settings;
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

        public Task StartAsync()
        {
            return StartAsync(null);
        }

        public Task StartAsync(string serial)
        {
            return Task.Run(delegate
            {
                lock (_operationGate) StartCore(serial);
            });
        }

        public Task StopAsync()
        {
            return Task.Run(delegate
            {
                lock (_operationGate) StopCore();
            });
        }

        public Task<bool> RetryDeferredCleanupAsync(string serial)
        {
            return Task.Run(delegate
            {
                lock (_operationGate)
                    return RetryDeferredCleanupCore(serial);
            });
        }

        public void RequestShutdown()
        {
            _scrcpyService.RequestShutdown();
            if (Interlocked.Exchange(ref _shutdownRequested, 1) == 0)
                _shutdownSignal.Set();
        }

        public Task ShutdownAsync()
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
                        lock (_operationGate) ShutdownCore();
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

        private void StartCore()
        {
            StartCore(null);
        }

        private void StartCore(string requestedSerial)
        {
            if (IsShutdownRequested) return;
            if (_scrcpyService.IsRunning)
            {
                _logService.Warning(LocalizationService.Get(
                    "Log.Dex.AlreadyRunning"));
                return;
            }

            var serial = string.IsNullOrWhiteSpace(requestedSerial)
                ? _adbService.TargetSerial
                : requestedSerial;
            if (string.IsNullOrWhiteSpace(serial) ||
                !_adbService.IsAuthorizedDeviceConnected(serial))
            {
                throw new InvalidOperationException(
                    LocalizationService.Get(
                        "Error.Dex.NoAuthorizedDevice"));
            }
            if (!RetryDeferredCleanupCore(serial))
            {
                throw new InvalidOperationException(
                    LocalizationService.Get(
                        "Error.Dex.DisplayResetFailed"));
            }
            CleanupStaleSession(serial);

            VirtualDisplayLease lease = null;
            var scrcpyStarted = false;
            try
            {
                _launchCoordinator.RunExclusive(delegate
                {
                    ThrowIfShutdownRequested();
                    lease = _virtualDisplayService.EnsureVirtualDisplay(
                        serial,
                        _settings.VirtualDisplay,
                        _settings.Timing.VirtualDisplayDetectionTimeoutMs,
                        delegate { return IsShutdownRequested; });
                    ThrowIfShutdownRequested();
                    _scrcpyService.Start(
                        _settings.Scrcpy,
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

                TrackSession("DeX", serial, lease);
                if (!_scrcpyService.IsRunning)
                    throw new InvalidOperationException(
                        LocalizationService.Get(
                            "Error.Scrcpy.ExitedBeforeWindow"));
                try
                {
                    SaveLastSuccess(serial, lease.DisplayId);
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
            }
            catch (OperationCanceledException ex)
            {
                lease = GetRetainedLease(ex, lease);
                CleanupFailedStart(scrcpyStarted, lease);
                _logService.Info(LocalizationService.Get(
                    "Log.Dex.StartCancelled"));
            }
            catch (Exception ex)
            {
                lease = GetRetainedLease(ex, lease);
                CleanupFailedStart(scrcpyStarted, lease);
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

            if (session != null &&
                _settings.Features.ResetVirtualDisplayOnStop)
            {
                if (!ReleaseDisplayLease(session.DisplayLease))
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
                    ? _adbService.TargetSerial
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
                    !ReleaseDisplayLease(session.DisplayLease))
                {
                    DeferDisplayCleanup(session);
                    throw new InvalidOperationException(
                        LocalizationService.Get(
                            "Error.Dex.DisplayResetFailed"));
                }
                ClearSession(session);

                if (_shutdownSignal.WaitOne(1000)) return false;
                StartCore(serial);
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

        private void ShutdownCore()
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
                _settings.Features.ResetVirtualDisplayOnStop &&
                !ReleaseDisplayLease(session.DisplayLease))
            {
                DeferDisplayCleanup(session);
            }
            ClearSession(session);
            _logService.Info(LocalizationService.Get(
                "Log.Dex.ShutdownCleanupCompleted"));
        }

        private void ScrcpyService_RunningChanged(
            object sender,
            EventArgs e)
        {
            if (_scrcpyService.IsRunning) return;
            if (Interlocked.Exchange(
                ref _naturalExitCleanupScheduled,
                1) != 0) return;

            Task.Run(delegate
            {
                try
                {
                    lock (_operationGate)
                    {
                        if (!_scrcpyService.IsRunning)
                            CleanupNaturallyEndedSession();
                    }
                }
                finally
                {
                    Interlocked.Exchange(
                        ref _naturalExitCleanupScheduled,
                        0);
                }
            });
        }

        private void CleanupNaturallyEndedSession()
        {
            var session = _currentSession;
            if (session == null) return;
            if (_settings.Features.ResetVirtualDisplayOnStop &&
                !ReleaseDisplayLease(session.DisplayLease))
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

        private void CleanupStaleSession(string nextSerial)
        {
            var stale = _currentSession;
            if (stale == null) return;
            if (!_settings.Features.ResetVirtualDisplayOnStop)
            {
                ClearSession(stale);
                return;
            }
            if (ReleaseDisplayLease(stale.DisplayLease))
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
            VirtualDisplayLease lease)
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
                    TrackSession("DeX", lease.Serial, lease);
                return;
            }

            try
            {
                if (lease != null &&
                    !ReleaseDisplayLease(lease))
                {
                    DeferDisplayCleanup(lease);
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

        private bool RetryDeferredCleanupCore(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial)) return true;
            VirtualDisplayLease lease;
            if (!_pendingDisplayCleanup.TryGetValue(serial, out lease))
                return true;
            if (!_settings.Features.ResetVirtualDisplayOnStop)
            {
                _pendingDisplayCleanup.Remove(serial);
                return true;
            }
            if (!ReleaseDisplayLease(lease))
            {
                _logService.Warning(LocalizationService.Format(
                    "Log.Dex.DeferredCleanupStillPending",
                    serial));
                return false;
            }

            _pendingDisplayCleanup.Remove(serial);
            _logService.Info(LocalizationService.Format(
                "Log.Dex.DeferredCleanupCompleted",
                serial));
            return true;
        }

        private bool ReleaseDisplayLease(VirtualDisplayLease lease)
        {
            if (lease == null || !lease.OwnsOverlaySetting) return true;
            if (string.IsNullOrWhiteSpace(lease.Serial) ||
                !_adbService.IsAuthorizedDeviceConnected(lease.Serial))
            {
                return false;
            }
            return _virtualDisplayService.Release(lease);
        }

        private void DeferDisplayCleanup(ManagedDisplaySession session)
        {
            if (session == null) return;
            DeferDisplayCleanup(session.DisplayLease);
            ClearSession(session);
        }

        private void DeferDisplayCleanup(VirtualDisplayLease lease)
        {
            if (lease == null || string.IsNullOrWhiteSpace(lease.Serial))
                return;
            _pendingDisplayCleanup[lease.Serial] = lease;
            _logService.Warning(LocalizationService.Format(
                "Log.Dex.DeferredCleanupStored",
                lease.Serial));
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

        private void SaveLastSuccess(string serial, int displayId)
        {
            _settingsService.UpdateAndSave(_settings, delegate(
                AppSettings settings)
            {
                settings.LastSuccess.Width = settings.VirtualDisplay.Width;
                settings.LastSuccess.Height = settings.VirtualDisplay.Height;
                settings.LastSuccess.Dpi = settings.VirtualDisplay.Dpi;
                settings.LastSuccess.AdbPath = _adbService.AdbPath;
                settings.LastSuccess.ScrcpyPath =
                    _scrcpyService.ScrcpyPath;
                settings.LastSuccess.ScrcpyArguments =
                    _scrcpyService.BuildArguments(
                        settings.Scrcpy,
                        displayId,
                        serial);
                settings.LastSuccess.DisplayId = displayId;
                settings.LastSuccess.SavedAtUtc =
                    DateTime.UtcNow.ToString("o");
            });
        }

        private void TrackSession(
            string mode,
            string serial,
            VirtualDisplayLease lease)
        {
            _currentSession = new ManagedDisplaySession
            {
                Mode = mode,
                Serial = serial,
                AppPackage = _settings.Scrcpy.StartAppPackage,
                DisplayId = lease.DisplayId,
                ScrcpyProcessId = _scrcpyService.CurrentProcessId,
                CreatedAtUtc = DateTime.UtcNow.ToString("o"),
                DisplayLease = lease
            };
            _logService.Info(LocalizationService.Format(
                "Log.Dex.SessionStarted",
                _currentSession));
        }

        private void ClearSession(ManagedDisplaySession session)
        {
            if (session == null ||
                !ReferenceEquals(_currentSession, session)) return;
            _logService.Info(LocalizationService.Format(
                "Log.Dex.SessionEnded",
                session));
            _currentSession = null;
        }
    }
}
