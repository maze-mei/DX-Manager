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
        private readonly NativeDexSessionService _nativeDex;
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
            AppSettings settings,
            DeviceRuntimeSessionRegistry runtimeSessions,
            NativeDexSessionService nativeDex = null)
        {
            _adbService = adbService;
            _nativeDex = nativeDex;
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
            if (_nativeDex != null) {
                _nativeDex.SessionEnded += NativeSessionEnded;
                _nativeDex.DisplayReleased += delegate {
                    var handler=NativeDisplayReleased;if(handler!=null)handler(this,EventArgs.Empty);
                };
            }
        }

        public event EventHandler NativeDisplayReleased;

        private bool WantsNativeDisplay { get { return _nativeDex!=null && GetDeviceRunSettings().VirtualDisplay.ActivationMode!=DexDisplayMode.LegacyOverlay; } }
        public bool UsesNativeDisplay { get { return _currentSession!=null&&_currentSession.DisplayLease!=null?_currentSession.DisplayLease.IsNativeDex:(_nativeDex!=null&&_nativeDex.HasRetainedDisplay)||WantsNativeDisplay; } }
        public VirtualDisplayLease RetainedNativeLease { get { return _nativeDex==null?null:_nativeDex.RetainedLease; } }
        public bool HasRetainedNativeForSerial(string serial) { return _nativeDex!=null&&_nativeDex.HasPendingForSerial(serial); }
        public Task ResumeAsync(string serial) { return Task.Run(delegate { lock(_operationGate)StartCore(serial,null,false,true); }); }
        public void UpdateNativeViewerState() {
            if(_nativeDex==null||!Monitor.TryEnter(_operationGate))return;
            try {
                var lease=_nativeDex.RetainedLease;if(lease==null)return;
                var session=_currentSession;
                bool active=session!=null&&session.DisplayLease!=null&&session.DisplayLease.IsNativeDex&&
                    session.DisplayLease.NativeSessionToken==lease.NativeSessionToken&&_scrcpyService.IsRunning;
                _nativeDex.SetViewerState(active,CurrentPowerDemand(lease.Serial));
            }finally{Monitor.Exit(_operationGate);}
        }
        private bool CurrentPowerDemand(string serial) {
            var session=_currentSession;
            bool requested=session!=null&&_scrcpyService.GetSessionSnapshot().StayAwakeRequested;
            var runtime=_runtimeSessions.Current.FindByTransportSerial(serial);
            if(runtime!=null)foreach(var slot in runtime.SingleWindows)requested|=slot.IsRunning&&slot.StayAwakeRequested;
            return requested;
        }
        // Device reconnection restores ownership/heartbeats independently of opening a PC window.
        // MainForm's existing auto-start policy remains the only automatic launch decision.
        public Task RestoreNativeControlAsync(string serial, Func<bool> connectionIsCurrent = null)
        {
            return Task.Run(delegate
            {
                lock (_operationGate)
                {
                    if (IsShutdownRequested || (connectionIsCurrent != null && !connectionIsCurrent()) ||
                        !HasRetainedNativeForSerial(serial)) return;
                    var settings = SnapshotRunSettings(GetDeviceRunSettings());
                    _nativeDex.Start(serial, settings.VirtualDisplay,
                        delegate { return IsShutdownRequested || (connectionIsCurrent != null && !connectionIsCurrent()); },
                        settings.Scrcpy.StayAwake, _settings.Features.DisableStayAwakeOnStop, true);
                    UpdateNativeViewerState();
                }
            });
        }
        public Task SuspendForTransportLossAsync(string serial, Func<bool> lossIsCurrent = null) {
            if(_nativeDex!=null&&(lossIsCurrent==null||lossIsCurrent()))_nativeDex.CancelPendingStart(serial);
            return Task.Run(delegate { lock(_operationGate) {
                if(lossIsCurrent!=null&&!lossIsCurrent())return;
                if(_currentSession!=null&&!string.Equals(_currentSession.Serial,serial,StringComparison.OrdinalIgnoreCase))return;
                if(UsesNativeDisplay)SuspendNativeCore(true);else StopCore();
            }});
        }
        private void SuspendNativeCore(bool transportLost = false) {
            if(_nativeDex!=null){_nativeDex.Suspend();_nativeDex.RefreshRetainedPresence();}
            _scrcpyService.Stop(transportLost);ClearSession(_currentSession);
        }

        public bool SignalNativeStopWithoutAdb()
        {
            return _nativeDex != null && _nativeDex.SignalStopWithoutAdb();
        }

        public bool IsRunning
        {
            get { return _scrcpyService.IsRunning; }
        }

        public ManagedDisplaySession CurrentSession
        {
            get { return _currentSession; }
        }

        public string DeviceIdentity { get; set; }

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

        public Task StartAsync(string serial, DeviceRunSettingsProfile launchSettings = null, bool experimentalFpsConfirmed = false)
        {
            return Task.Run(delegate
            {
                lock (_operationGate) StartCore(serial, launchSettings, experimentalFpsConfirmed);
            });
        }

        public Task StopAsync()
        {
            if(_nativeDex!=null)_nativeDex.CancelPendingStart();
            return Task.Run(delegate
            {
                lock (_operationGate) StopCore();
            });
        }

        public Task StopForSerialAsync(string serial)
        {
            if(_nativeDex!=null)_nativeDex.CancelPendingStart(serial);
            return Task.Run(delegate
            {
                lock (_operationGate)
                {
                    var captured = _currentSession == null ? _scrcpyService.RunningSerial : _currentSession.Serial;
                    if (!string.IsNullOrWhiteSpace(serial) && string.Equals(
                        captured, serial, StringComparison.OrdinalIgnoreCase)) StopCore();
                }
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
            SignalNativeStopWithoutAdb();
            if (Interlocked.Exchange(ref _shutdownRequested, 1) == 0)
                _shutdownSignal.Set();
        }

        public Task ShutdownAsync(string fallbackSerial)
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
                            ShutdownCore(fallbackSerial);
                    });
                }
                return _shutdownTask;
            }
        }

        public Task<bool> ApplyRuntimeSettingsAsync(DeviceRunSettingsProfile launchSettings = null, bool experimentalFpsConfirmed = false)
        {
            return Task.Run(delegate
            {
                lock (_operationGate)
                    return ApplyRuntimeSettingsCore(launchSettings, experimentalFpsConfirmed);
            });
        }

        private void StartCore(string requestedSerial, DeviceRunSettingsProfile launchSettings = null, bool experimentalFpsConfirmed = false, bool resumeOnly = false)
        {
            if (IsShutdownRequested) return;
            if (_scrcpyService.IsRunning)
            {
                _logService.Warning(LocalizationService.Get(
                    "Log.Dex.AlreadyRunning"));
                return;
            }

            var runSettings = launchSettings ?? GetDeviceRunSettings();
            FrameRateRules.RequireConsent(runSettings.Scrcpy.MaxFps, experimentalFpsConfirmed);
            var serial = requestedSerial;
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
            if(resumeOnly&&!HasRetainedNativeForSerial(serial))return;
            if(_currentSession!=null&&_currentSession.DisplayLease!=null&&_currentSession.DisplayLease.IsNativeDex&&!_scrcpyService.IsRunning)ClearSession(_currentSession);
            CleanupStaleSession(serial);
            var native = resumeOnly || (_nativeDex!=null && runSettings.VirtualDisplay.ActivationMode==DexDisplayMode.Native);
            if(native && FrameRateRules.RequiresScreenOn(runSettings.Scrcpy.MaxFps))
                throw new InvalidOperationException(LocalizationService.Get("Error.Native.Fps120RequiresLegacy"));
            if (native) runSettings = SnapshotRunSettings(runSettings);

            VirtualDisplayLease lease = null;
            var scrcpyStarted = false;
            try
            {
                if(native) {
                    lease=_nativeDex.Start(serial,runSettings.VirtualDisplay,delegate{return IsShutdownRequested;},runSettings.Scrcpy.StayAwake,_settings.Features.DisableStayAwakeOnStop,resumeOnly);
                    if(lease==null)return;
                } else if(_nativeDex!=null&&!_nativeDex.RecoverForSerial(serial))throw new InvalidOperationException("Stop the pending native session before changing DeX activation mode.");
                _launchCoordinator.RunExclusive(delegate
                {
                    ThrowIfShutdownRequested();
                    if (!native)
                        lease = _virtualDisplayService.EnsureVirtualDisplay(
                            serial,
                            runSettings.VirtualDisplay,
                            _settings.Timing.VirtualDisplayDetectionTimeoutMs,
                            delegate { return IsShutdownRequested; });
                    ThrowIfShutdownRequested();
                    _scrcpyService.Start(
                        runSettings.Scrcpy,
                        lease.DisplayId,
                        serial, experimentalFpsConfirmed, lease.IsNativeDex);
                    scrcpyStarted = true;
                });

                if (IsShutdownRequested)
                    throw new OperationCanceledException();

                if (!_scrcpyService.IsRunning)
                    throw new InvalidOperationException(
                        LocalizationService.Get(
                            "Error.Scrcpy.ExitedBeforeWindow"));

                TrackSession("DeX", serial, lease, runSettings.Scrcpy.StartAppPackage);
                if(native)UpdateNativeViewerState();
                if (!_scrcpyService.IsRunning)
                    throw new InvalidOperationException(
                        LocalizationService.Get(
                            "Error.Scrcpy.ExitedBeforeWindow"));
                try
                {
                    SaveLastSuccess(serial, lease.DisplayId, runSettings);
                }
                catch (Exception saveException)
                {
                    _logService.Error(
                        LocalizationService.Get(
                            "Log.Dex.LastSuccessSaveFailed"),
                        saveException);
                }
                _logService.Progress(LocalizationService.Get(
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

            if(session==null&&_nativeDex!=null&&_nativeDex.HasRetainedDisplay)_nativeDex.Stop(_nativeDex.RetainedLease);
            if (session != null)
            {
                if (!ReleaseDisplayLease(session.DisplayLease))
                    DeferDisplayCleanup(session);
            }
            ClearSession(session);
            _logService.Info(LocalizationService.Get(
                "Log.Dex.StopCleanupCompleted"));
            if (stopException != null) throw stopException;
        }

        private bool ApplyRuntimeSettingsCore(DeviceRunSettingsProfile launchSettings, bool experimentalFpsConfirmed)
        {
            if (IsShutdownRequested || !_scrcpyService.IsRunning) return false;
            launchSettings = launchSettings ?? GetDeviceRunSettings();
            FrameRateRules.RequireConsent(launchSettings.Scrcpy.MaxFps, experimentalFpsConfirmed);
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
                    !ReleaseDisplayLease(session.DisplayLease))
                {
                    DeferDisplayCleanup(session);
                    throw new InvalidOperationException(
                        LocalizationService.Get(
                            "Error.Dex.DisplayResetFailed"));
                }
                ClearSession(session);

                if (_shutdownSignal.WaitOne(1000)) return false;
                StartCore(serial, launchSettings, experimentalFpsConfirmed);
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

        private void ShutdownCore(string fallbackSerial)
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
                !ReleaseDisplayLease(session.DisplayLease))
            {
                DeferDisplayCleanup(session);
            }
            else if (session == null)
            {
                CleanupConnectedTargetOverlay(fallbackSerial);
            }
            ClearSession(session);
            _logService.Progress(LocalizationService.Get(
                "Log.Dex.ShutdownCleanupCompleted"));
        }

        private void NativeSessionEnded(string token)
        {
            Task.Run(delegate
            {
                lock (_operationGate)
                {
                    var lease = _nativeDex.RetainedLease;
                    if (lease == null || lease.NativeSessionToken != token || _nativeDex.HasActiveControl) return;
                    try {
                        var serial=lease.Serial;
                        if (!IsShutdownRequested && _adbService.IsAuthorizedDeviceConnected(serial))
                        {
                            var settings = SnapshotRunSettings(GetDeviceRunSettings());
                            if (_nativeDex.Start(serial, settings.VirtualDisplay, delegate { return IsShutdownRequested; },
                                settings.Scrcpy.StayAwake, _settings.Features.DisableStayAwakeOnStop, true) != null) {
                                UpdateNativeViewerState();return;
                            }
                        }
                        SuspendNativeCore();
                    }
                    catch (Exception error) {
                        SuspendNativeCore();
                        _logService.Error("[Native DeX] Client disconnected; retained session needs inspection.", error);
                    }
                }
            });
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
            if(session.DisplayLease!=null&&session.DisplayLease.IsNativeDex&&
                (_scrcpyService.LastExitCode==2||!_adbService.IsAuthorizedDeviceConnected(session.Serial))) {
                _nativeDex.Suspend();ClearSession(session);return;
            }
            if (!ReleaseDisplayLease(session.DisplayLease))
            {
                DeferDisplayCleanup(session);
                _logService.Warning(LocalizationService.Get(
                    "Log.Dex.NaturalExitCleanupDeferred"));
                return;
            }

            ClearSession(session);
            _logService.Progress(LocalizationService.Get(
                "Log.Dex.NaturalExitCleanupCompleted"));
        }

        private void CleanupStaleSession(string nextSerial)
        {
            var stale = _currentSession;
            if (stale == null) return;
            if ((stale.DisplayLease == null || !stale.DisplayLease.IsNativeDex) && string.Equals(
                stale.Serial,
                nextSerial,
                StringComparison.OrdinalIgnoreCase))
            {
                // A reconnect must be evaluated by EnsureVirtualDisplay.
                // Releasing the stale lease here would always delete a
                // perfectly reusable overlay before the comparison.
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
            if(lease!=null&&lease.IsNativeDex&&lease.NativeResumed&&!IsShutdownRequested) {
                SuspendNativeCore();return;
            }
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
            if (!_pendingDisplayCleanup.ContainsKey(serial))
                return true;
            var pending = _pendingDisplayCleanup[serial];
            if (pending != null && pending.IsNativeDex)
            {
                if (_nativeDex == null || !_nativeDex.Stop(pending, serial)) return false;
                _pendingDisplayCleanup.Remove(serial);
                return true;
            }
            // The device is available again. Do not delete the old overlay
            // merely because the previous connection ended unexpectedly;
            // the next start will compare its actual resolution and DPI.
            _pendingDisplayCleanup.Remove(serial);
            _logService.Info(LocalizationService.Format(
                "Log.Dex.DeferredCleanupSuperseded",
                serial));
            return true;
        }

        private bool ReleaseDisplayLease(VirtualDisplayLease lease)
        {
            if (lease == null) return true;
            if (lease.IsNativeDex) return _nativeDex != null && _nativeDex.Stop(lease);
            if (string.IsNullOrWhiteSpace(lease.Serial) ||
                !_adbService.IsAuthorizedDeviceConnected(lease.Serial))
            {
                return false;
            }
            return _virtualDisplayService.Release(lease);
        }

        private void CleanupConnectedTargetOverlay(string serial)
        {
            if (UsesNativeDisplay)
            {
                if (!_adbService.IsProcessShutdownRequested) _nativeDex.RecoverForSerial(serial);
                else _nativeDex.SignalStopWithoutAdb();
                return;
            }
            if (string.IsNullOrWhiteSpace(serial) ||
                !_adbService.IsAuthorizedDeviceConnected(serial))
            {
                return;
            }

            _virtualDisplayService.Reset(serial);
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

        private static DeviceRunSettingsProfile SnapshotRunSettings(DeviceRunSettingsProfile settings)
        {
            var serializer = new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(DeviceRunSettingsProfile));
            using (var stream = new System.IO.MemoryStream())
            {
                serializer.WriteObject(stream, settings);
                stream.Position = 0;
                return (DeviceRunSettingsProfile)serializer.ReadObject(stream);
            }
        }

        private void SaveLastSuccess(string serial, int displayId, DeviceRunSettingsProfile captured = null)
        {
            _settingsService.UpdateAndSave(_settings, delegate(
                AppSettings settings)
            {
                var runSettings = GetDeviceRunSettings(settings);
                var source = captured ?? runSettings;
                runSettings.LastSuccess.Width =
                    source.VirtualDisplay.Width;
                runSettings.LastSuccess.Height =
                    source.VirtualDisplay.Height;
                runSettings.LastSuccess.Dpi =
                    source.VirtualDisplay.Dpi;
                runSettings.LastSuccess.AdbPath = _adbService.AdbPath;
                runSettings.LastSuccess.ScrcpyPath =
                    _scrcpyService.ScrcpyPath;
                runSettings.LastSuccess.ScrcpyArguments =
                    _scrcpyService.BuildArguments(
                        source.Scrcpy,
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
            VirtualDisplayLease lease,
            string capturedAppPackage = null)
        {
            _currentSession = new ManagedDisplaySession
            {
                Mode = mode,
                Serial = serial,
                AppPackage = capturedAppPackage ?? GetDeviceRunSettings()
                    .Scrcpy.StartAppPackage,
                DisplayId = lease.DisplayId,
                ScrcpyProcessId = _scrcpyService.CurrentProcessId,
                CreatedAtUtc = DateTime.UtcNow.ToString("o"),
                DisplayLease = lease
            };
            _runtimeSessions.SetDexSession(serial, _currentSession);
            _logService.Progress(LocalizationService.Format(
                "Log.Dex.SessionStarted",
                _currentSession));
        }

        private DeviceRunSettingsProfile GetDeviceRunSettings()
        {
            return GetDeviceRunSettings(_settings);
        }

        private DeviceRunSettingsProfile GetDeviceRunSettings(
            AppSettings settings)
        {
            if (!string.IsNullOrWhiteSpace(DeviceIdentity))
                return settings.GetOrCreateDeviceRunSettings(
                    DeviceIdentity);
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
            _logService.Progress(LocalizationService.Format(
                "Log.Dex.SessionEnded",
                session));
            _runtimeSessions.SetDexSession(session.Serial, null);
            _currentSession = null;
        }
    }
}
