using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using DexManager.Models;
using DexManager.Services;

namespace DexManager.Forms
{
    public sealed partial class MainForm : Form
    {
        private void PhysicalDeviceRegistry_SnapshotChanged(
            object sender,
            DeviceRegistrySnapshotChangedEventArgs e)
        {
            if (e == null || e.Current == null) return;
            RunOnUi(delegate { ReconcileDeviceTabs(e.Current); });
        }

        private void ReconcileDeviceTabs(DeviceRegistrySnapshot snapshot)
        {
            if (_exitInProgress || IsDisposed) return;

            // The compatibility context is assigned to the first registry
            // entry that is materialized.  That registry order is unrelated
            // to the presentation order, so remember that initial selection
            // is still pending before the context receives an identity.
            var initialSelectionPending =
                _initialDeviceContext != null &&
                string.IsNullOrWhiteSpace(_initialDeviceContext.Identity);

            if (!_deviceTabsVisibleForRun &&
                CountConnectedPhysicalDevices(snapshot) >= 2)
            {
                _deviceTabsVisibleForRun = true;
            }

            _devicePresentationOrder.Reconcile(snapshot.Devices);

            foreach (var context in _deviceContexts.Values)
                context.Device = null;

            foreach (var device in snapshot.Devices)
            {
                if (device == null ||
                    string.IsNullOrWhiteSpace(device.Identity)) continue;

                var context = EnsureDeviceContext(device);
                context.Device = device.Clone();
                RememberContextDisplayName(context, device);
                ConfigureContextPresentation(context);
                context.MiniBar.UpdateConnection(context.Device);
                ConfigureContextConnection(context);
            }

            foreach (var context in _deviceContexts.Values)
            {
                if (context.Device == null && context.WasConnected)
                    HandleContextDisconnected(context);
            }

            if (initialSelectionPending ||
                _selectedDeviceContext == null ||
                _selectedDeviceContext.Device == null ||
                !_selectedDeviceContext.Device.IsConnected ||
                string.IsNullOrWhiteSpace(_selectedDeviceContext.Identity))
            {
                foreach (var identity in
                    _devicePresentationOrder.GetIdentities())
                {
                    DeviceUiContext context;
                    if (_deviceContexts.TryGetValue(identity, out context) &&
                        context.Device != null &&
                        context.Device.IsConnected)
                    {
                        if (initialSelectionPending)
                        {
                            if (ReferenceEquals(
                                    context,
                                    _selectedDeviceContext))
                            {
                                RefreshInitiallyBoundDeviceSettings(context);
                            }
                            else
                            {
                                SelectDeviceContext(context, false);
                            }
                        }
                        else
                        {
                            SelectDeviceContext(context);
                        }
                        break;
                    }
                }
            }

            RebuildDeviceTabs();
            RefreshSelectedDeviceState();
            if (_settingsForm != null && !_settingsForm.IsDisposed)
                _settingsForm.RefreshSelectedDeviceContext();
        }

        private void RefreshInitiallyBoundDeviceSettings(
            DeviceUiContext context)
        {
            if (context == null) return;

            // The main form initially displays the legacy default template
            // before a physical device identity is known.  The first device
            // reuses that already-selected context, so SelectDeviceContext()
            // intentionally returns without reloading the controls.  Reload
            // directly here and do not save the stale template values into
            // the newly bound per-device profile.
            _selectedDeviceIdentity = context.Identity ?? string.Empty;
            _modeSettingsDirty = context.ModeSettingsDirty ?? new bool[4];
            LoadRunSettings();
        }

        private DeviceUiContext EnsureDeviceContext(PhysicalDeviceInfo device)
        {
            DeviceUiContext context;
            lock (_deviceContextsSync)
            {
                if (_deviceContexts.TryGetValue(
                        device.Identity,
                        out context))
                {
                    return context;
                }
            }

            var session = _runtimeSessions.Current.FindByIdentity(
                device.Identity);
            var runtime = session == null
                ? null
                : _runtimeServiceFactory.Find(session.ServiceInstanceId);

            if (runtime == null &&
                _initialDeviceContext != null &&
                string.IsNullOrWhiteSpace(_initialDeviceContext.Identity))
            {
                context = _initialDeviceContext;
                runtime = context.Runtime;
            }
            else
            {
                runtime = runtime ?? _runtimeServiceFactory.Create();
                context = CreateDeviceContext(runtime);
                AttachContextEvents(context);
                context.MiniBar.Start();
            }

            context.Identity = device.Identity;
            context.Device = device.Clone();
            RememberContextDisplayName(context, device);
            context.Runtime.Dex.DeviceIdentity = device.Identity;
            ConfigureContextPresentation(context);
            lock (_deviceContextsSync)
                _deviceContexts.Add(device.Identity, context);
            if (ReferenceEquals(context, _selectedDeviceContext))
                _selectedDeviceIdentity = device.Identity;

            var transport = GetContextTransport(context);
            if (transport != null &&
                !string.IsNullOrWhiteSpace(transport.Serial))
            {
                _runtimeSessions.BindServiceInstance(
                    transport.Serial,
                    runtime.InstanceId);
            }
            _logService.Info(LocalizationService.Format(
                "Log.DeviceContext.Registered",
                GetContextDisplayName(context),
                transport == null ? device.Identity : transport.Serial,
                GetTransportText(transport)));
            return context;
        }

        private void AttachContextEvents(DeviceUiContext context)
        {
            context.Runtime.Scrcpy.RunningChanged +=
                ScrcpyService_RunningChanged;
            context.Runtime.SingleWindows.RunningChanged +=
                SingleWindowService_RunningChanged;
            context.Capture.ExitHotkeyPressed +=
                CaptureCoordinator_ExitHotkeyPressed;
            context.AutoHide.IdleHideRequested +=
                AutoHideService_IdleHideRequested;
            context.Runtime.FileTransfers.ProgressChanged +=
                FileTransferCoordinator_ProgressChanged;
            context.Runtime.PhoneTransfers.ProgressChanged +=
                PhoneTransferReceiver_ProgressChanged;
        }

        private void ConfigureContextConnection(DeviceUiContext context)
        {
            if (context == null || context.Device == null)
            {
                if (context != null && context.WasConnected)
                    HandleContextDisconnected(context);
                return;
            }

            RecordAvailableTransports(context);
            StopSessionsOnMissingTransports(context);
            var selectedTransport = GetContextTransport(context);
            var serial = selectedTransport == null
                ? string.Empty
                : selectedTransport.Serial;
            if (string.IsNullOrWhiteSpace(serial))
            {
                if (context.WasConnected)
                    HandleContextDisconnected(context);
                return;
            }
            _runtimeSessions.BindServiceInstance(
                serial,
                context.Runtime.InstanceId);
            var newlyConnected = !context.WasConnected;
            var autoStartResumed = RestoreAutoStartAfterReconnect(context, newlyConnected);
            var connectionChanged = newlyConnected ||
                !string.Equals(
                    context.ActiveSerial,
                    serial,
                    StringComparison.OrdinalIgnoreCase);
            var previousSerial = context.ActiveSerial;
            if (context.WasConnected && connectionChanged &&
                !string.IsNullOrWhiteSpace(previousSerial))
            {
                context.Runtime.FileTransfers.CancelSerial(previousSerial);
                context.Runtime.CompanionGuardian.NotifyConnectionLost(
                    previousSerial);
                var detachTask = context.Runtime.PhoneTransfers.DetachAsync(
                    previousSerial);
                var previousTransport = context.Device.FindTransport(previousSerial);
                if (previousTransport == null || !previousTransport.IsAuthorized)
                {
                    context.RequireManualStartAfterTransportLoss = true;
                    context.AutoStartBlockedSerial = previousSerial;
                    ForgetDeviceConnection(previousSerial);
                    MarkSerialDisconnected(previousSerial);
                }
                else ForgetDeviceConnectionTimestamp(previousSerial);
            }
            context.WasConnected = true;
            context.ActiveSerial = serial;
            if (connectionChanged)
            {
                context.ConnectionGeneration++;
                if (!newlyConnected &&
                    !string.IsNullOrWhiteSpace(previousSerial))
                {
                    SwitchContextTransportAsync(
                        context,
                        previousSerial,
                        serial,
                        context.ConnectionGeneration,
                        autoStartResumed);
                    return;
                }
                RecordDeviceConnected(serial);
                MarkSerialReconnected(serial);
                ConfigurePhoneTransferReceiver(
                    context,
                    serial,
                    selectedTransport.Kind);
                RetryContextCleanupAsync(
                    context,
                    serial,
                    newlyConnected);
            }
            else if (autoStartResumed)
            {
                RetryContextCleanupAsync(context, serial, true);
            }
        }

        // Only suppress fallback within the current connection episode.
        // A restored original transport or a new physical connection can auto-start again.
        private bool RestoreAutoStartAfterReconnect(DeviceUiContext context, bool newlyConnected)
        {
            if (!context.RequireManualStartAfterTransportLoss) return false;
            var restored = context.Device.FindTransport(context.AutoStartBlockedSerial);
            if (!newlyConnected && (restored == null || !restored.IsAuthorized)) return false;
            context.RequireManualStartAfterTransportLoss = false;
            context.AutoStartBlockedSerial = string.Empty;
            return true;
        }

        private void RecordAvailableTransports(DeviceUiContext context)
        {
            if (context.Device.Transports == null) return;
            foreach (var transport in context.Device.Transports)
            {
                if (transport == null || !transport.IsAuthorized ||
                    string.IsNullOrWhiteSpace(transport.Serial)) continue;
                lock (_deviceConnectionSync)
                {
                    if (!_deviceConnectedAtUtc.ContainsKey(transport.Serial))
                        _deviceConnectedAtUtc[transport.Serial] = DateTime.UtcNow;
                }
                MarkSerialReconnected(transport.Serial);
            }
        }

        private void StopSessionsOnMissingTransports(DeviceUiContext context)
        {
            var serials = new System.Collections.Generic.HashSet<string>(
                context.Runtime.SingleWindows.GetRunningSerials(), StringComparer.OrdinalIgnoreCase);
            var dexSerial = context.Runtime.Scrcpy.RunningSerial;
            if (!string.IsNullOrWhiteSpace(dexSerial)) serials.Add(dexSerial);
            foreach (var serial in serials)
            {
                var transport = context.Device.FindTransport(serial);
                if (transport != null && transport.IsAuthorized) continue;
                context.RequireManualStartAfterTransportLoss = true;
                context.AutoStartBlockedSerial = serial;
                ForgetDeviceConnection(serial);
                MarkSerialDisconnected(serial);
                StopDisconnectedSerialAsync(context, serial);
            }
        }

        private async Task StopContextTransportAsync(DeviceUiContext context, string serial)
        {
            // Invalidate pending DeX immediately and include all old window cleanup
            // in the task awaited by the next launch for this physical phone.
            var previousCleanup = context.DexCleanupTask;
            var stopDex = context.Runtime.Dex.StopForSerialAsync(serial);
            await Task.WhenAll(previousCleanup, stopDex);
            await Task.Run(delegate { context.Runtime.SingleWindows.StopForSerial(serial); });
        }

        private async void StopDisconnectedSerialAsync(DeviceUiContext context, string serial)
        {
            try
            {
                context.DexCleanupTask = StopContextTransportAsync(context, serial);
                await context.DexCleanupTask;
            }
            catch (Exception ex) { _logService.Error("Could not stop disconnected transport " + serial + ".", ex); }
        }

        private async void SwitchContextTransportAsync(
            DeviceUiContext context,
            string previousSerial,
            string serial,
            int generation,
            bool allowAutoStart)
        {
            try
            {
                context.DexCleanupTask = StopContextTransportAsync(context, previousSerial);
                await context.DexCleanupTask;
            }
            catch (Exception ex)
            {
                _logService.Error(
                    "Could not stop the previous transport session " +
                    previousSerial + ".",
                    ex);
            }

            if (_exitInProgress || context.ConnectionGeneration != generation ||
                !context.WasConnected || !string.Equals(
                    context.ActiveSerial,
                    serial,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    GetContextSerial(context),
                    serial,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            RecordDeviceConnected(serial);
            MarkSerialReconnected(serial);
            var transport = context.Device == null
                ? null
                : context.Device.FindTransport(serial);
            ConfigurePhoneTransferReceiver(
                context,
                serial,
                transport == null
                    ? DeviceTransportKind.Unknown
                    : transport.Kind);
            RetryContextCleanupAsync(context, serial, allowAutoStart);
            RefreshSelectedDeviceState();
            RebuildDeviceTabs();
        }

        private async void RetryContextCleanupAsync(
            DeviceUiContext context,
            string serial,
            bool allowAutoStart)
        {
            if (context == null) return;
            var generation = context.ConnectionGeneration;
            var orchestrator = context.Runtime.Dex;
            var autoStart = allowAutoStart &&
                _settings.Features.AutoStartDexOnDeviceConnected &&
                !context.RequireManualStartAfterTransportLoss &&
                IsContextConnectionCurrent(context, serial, generation) &&
                !orchestrator.IsRunning;
            // Lock the mode before the first await, including cleanup and the
            // configured connection delay, not only during helper negotiation.
            if (autoStart) BeginPhonePreviewStart(orchestrator);
            try
            {
                var autoStartGeneration = _autoStartSettingsGeneration;
                await context.DexCleanupTask;
                if (!IsContextConnectionCurrent(context, serial, generation)) return;
                var cleanupReady = await context.Runtime.Dex
                    .RetryDeferredCleanupAsync(serial);
                if (cleanupReady && autoStart && !context.RequireManualStartAfterTransportLoss &&
                    _settings.Features.AutoStartDexOnDeviceConnected)
                {
                    var launchTransport = context.Device == null ? null :
                        context.Device.SelectLaunchTransport(context.ActiveSerial);
                    if (launchTransport == null || !await WaitForDeviceStartDelayAsync(
                        launchTransport.Serial, context, generation)) return;
                    if (autoStartGeneration != _autoStartSettingsGeneration || !_settings.Features.AutoStartDexOnDeviceConnected) return;
                    await StartDexForContextAsync(context, launchTransport.Serial, generation);
                }
            }
            catch (Exception ex)
            {
                _logService.Error(
                    "Could not retry deferred cleanup for " + serial + ".",
                    ex);
            }
            finally
            {
                if (autoStart)
                {
                    EndPhonePreviewStart(orchestrator);
                    if (!_exitInProgress && !IsDisposed &&
                        ReferenceEquals(context, _selectedDeviceContext))
                        UpdateRunningState();
                }
            }
        }

        private async Task StartDexForContextAsync(
            DeviceUiContext context,
            string serial,
            int generation)
        {
            if (context.RequireManualStartAfterTransportLoss || !IsContextLaunchCurrent(context, serial, generation) ||
                context.Runtime.Dex.IsShutdownRequested ||
                context.Runtime.Dex.IsRunning)
            {
                return;
            }

            var orchestrator = context.Runtime.Dex;
            try
            {
                var runSettings = GetDeviceRunSettings(
                    _settings,
                    context.Identity);
                runSettings = new DeviceRunSettingsProfile
                {
                    DeviceIdentity = context.Identity,
                    VirtualDisplay = AppSettings.CloneVirtualDisplay(runSettings.VirtualDisplay),
                    Scrcpy = AppSettings.CloneScrcpy(runSettings.Scrcpy)
                };
                if (!await ConfirmExperimentalFrameRateAsync(runSettings.Scrcpy.MaxFps) ||
                    !IsContextLaunchCurrent(context, serial, generation) || context.Runtime.Dex.IsShutdownRequested) return;
                if (runSettings.Scrcpy.TurnScreenOff && !runSettings.VirtualDisplay.HidePhonePreview)
                    RememberManagedSerial(serial);

                if (ReferenceEquals(context, _selectedDeviceContext))
                {
                    _connectionError = null;
                    SetOperationState(
                        true,
                        LocalizationService.Get("Status.Starting"));
                    SetConnectionIndicator(
                        Color.DarkOrange,
                        LocalizationService.Get("Main.DexStarting"),
                        LocalizationService.Get("Main.DexPreparing"));
                }

                _logService.Info(
                    "Starting DeX automatically for " +
                    GetContextDisplayName(context) + " (" + serial + ").");
                context.PendingSessionStarts++;
                try { await context.Runtime.Dex.StartAsync(serial, runSettings, true); }
                finally { context.PendingSessionStarts--; }

                if (_exitInProgress ||
                    !IsContextLaunchCurrent(
                        context,
                        serial,
                        generation) ||
                    !orchestrator.IsRunning)
                {
                    return;
                }

                RememberStartedApp(
                    context.Identity,
                    runSettings.Scrcpy.StartAppPackage,
                    runSettings.Scrcpy.StartAppName);
                context.ModeSettingsDirty[0] = false;
            }
            catch (Exception ex)
            {
                _logService.Error(
                    "Could not start DeX automatically for " +
                    GetContextDisplayName(context) + " (" + serial + ").",
                    ex);
                if (!_exitInProgress && ReferenceEquals(
                        context,
                        _selectedDeviceContext))
                {
                    ShowError(
                        LocalizationService.Get("Error.StartDex"),
                        ex);
                }
            }
            finally
            {
                if (!_exitInProgress && !IsDisposed)
                {
                    if (ReferenceEquals(context, _selectedDeviceContext))
                        UpdateRunningState();
                    UpdatePhoneScreenWakeSchedule();
                    RebuildDeviceTabs();
                }
            }
        }

        private bool IsContextLaunchCurrent(DeviceUiContext context, string serial, int generation)
        {
            var transport = context == null || context.Device == null ? null :
                context.Device.SelectLaunchTransport(context.ActiveSerial);
            return context != null && !_exitInProgress && !IsDisposed &&
                context.ConnectionGeneration == generation && context.WasConnected &&
                transport != null && string.Equals(transport.Serial, serial, StringComparison.OrdinalIgnoreCase) &&
                !IsSerialMarkedDisconnected(serial);
        }

        private bool IsContextConnectionCurrent(
            DeviceUiContext context,
            string serial,
            int generation)
        {
            return context != null &&
                !_exitInProgress &&
                !IsDisposed &&
                context.ConnectionGeneration == generation &&
                context.WasConnected &&
                string.Equals(
                    context.ActiveSerial,
                    serial,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    GetContextSerial(context),
                    serial,
                    StringComparison.OrdinalIgnoreCase) &&
                !IsSerialMarkedDisconnected(serial);
        }

        private void HandleContextDisconnected(DeviceUiContext context)
        {
            context.MiniBar.UpdateConnection(null);
            if (context.PendingSessionStarts > 0 || context.Runtime.Dex.IsRunning ||
                context.Runtime.SingleWindows.GetRunningSerials().Count > 0)
            {
                context.RequireManualStartAfterTransportLoss = true;
                context.AutoStartBlockedSerial = context.ActiveSerial;
            }
            context.WasConnected = false;
            context.ConnectionGeneration++;
            var serial = context.ActiveSerial;
            context.ActiveSerial = string.Empty;
            if (string.IsNullOrWhiteSpace(serial)) return;
            ForgetDeviceConnection(serial);
            MarkSerialDisconnected(serial);
            context.Runtime.FileTransfers.CancelSerial(serial);
            context.Runtime.CompanionGuardian.NotifyConnectionLost(serial);
            var detachTask = context.Runtime.PhoneTransfers.DetachAsync(serial);
            // StopAsync invalidates an in-flight start immediately. Do not
            // postpone cancellation until a background work item is scheduled.
            var stopDexTask = context.Runtime.Dex.IsRunning ||
                context.Runtime.Dex.IsBusy
                ? context.Runtime.Dex.StopAsync()
                : Task.FromResult(0);
            var previousCleanup = context.DexCleanupTask;
            context.DexCleanupTask = Task.Run(async delegate
            {
                try
                {
                    await Task.WhenAll(previousCleanup, stopDexTask).ConfigureAwait(false);
                    context.Runtime.SingleWindows.StopForSerial(serial);
                }
                catch (Exception ex)
                {
                    _logService.Error(
                        "Could not clean the disconnected device runtime " +
                        serial + ".",
                        ex);
                }
            });
        }

        private async void ConfigurePhoneTransferReceiver(
            DeviceUiContext context,
            string serial,
            DeviceTransportKind transportKind)
        {
            if (_exitInProgress || context == null ||
                string.IsNullOrWhiteSpace(serial)) return;
            try
            {
                await context.Runtime.PhoneTransfers.AttachAsync(
                    serial,
                    GetContextDisplayName(context));
            }
            catch (Exception ex)
            {
                _logService.Error(
                    "Could not prepare phone-to-PC transfer for " + serial +
                    ".",
                    ex);
            }

            if (_exitInProgress || !context.WasConnected ||
                !string.Equals(
                    context.ActiveSerial,
                    serial,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            try
            {
                await context.Runtime.CompanionGuardian.AttachAsync(
                    serial,
                    transportKind);
            }
            catch (Exception ex)
            {
                _logService.Error(
                    "Could not prepare DX Companion shutdown protection for " +
                    serial + ".",
                    ex);
            }
        }

        private void ConfigurePhoneTransferReceiver(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial)) return;
            foreach (var context in GetAllDeviceContexts())
            {
                if (!string.Equals(
                    GetContextSerial(context),
                    serial,
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                var transport = context.Device == null
                    ? null
                    : context.Device.FindTransport(serial);
                ConfigurePhoneTransferReceiver(
                    context,
                    serial,
                    transport == null
                        ? DeviceTransportKind.Unknown
                        : transport.Kind);
                return;
            }
            _logService.Warning(
                "Could not find a device runtime for phone transfer: " +
                serial + ".");
        }

    }
}
