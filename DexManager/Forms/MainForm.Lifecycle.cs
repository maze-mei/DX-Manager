using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using DexManager.Models;
using DexManager.Services;
using DexManager.Utils;

namespace DexManager.Forms
{
    public sealed partial class MainForm : Form
    {
        private string GetTransferDeviceName(object service)
        {
            lock (_deviceContextsSync)
            {
                foreach (var context in _deviceContexts.Values)
                {
                    if (context.Runtime != null &&
                        (ReferenceEquals(service, context.Runtime.FileTransfers) ||
                         ReferenceEquals(service, context.Runtime.PhoneTransfers)))
                        return GetContextDisplayName(context);
                }
            }
            return StoreUiText.Get("CommonConnectedPhone", "Connected phone");
        }

        private const int WmQueryEndSession = 0x0011;
        private const int WmEndSession = 0x0016;

        protected override void WndProc(ref Message message)
        {
            // This arrives before the ordinary Windows-shutdown FormClosing
            // path. Flush the pre-opened Companion command while the desktop
            // session and socket are still available.
            if (message.Msg == WmQueryEndSession)
                BeginSystemShutdown();
            base.WndProc(ref message);

            // If another application cancels Windows shutdown after the
            // query, this process has already frozen every producer that
            // could start ADB. Exit DX Manager instead of leaving a partially
            // shut-down instance in the notification area.
            if (message.Msg == WmEndSession &&
                message.WParam == IntPtr.Zero &&
                _systemShutdownInProgress &&
                !IsDisposed)
            {
                BeginInvoke((Action)Close);
            }
        }

        private void CaptureCoordinator_ExitHotkeyPressed(object sender, EventArgs e) { RunOnUi(ExitApplication); }
        private void AutoHideService_IdleHideRequested(object sender, EventArgs e) { RunOnUi(HideApplicationForIdle); }

        private void FileTransferCoordinator_ProgressChanged(
            object sender,
            FileTransferProgressEventArgs e)
        {
            if (e == null || e.Progress == null) return;
            var coordinator = sender as FileTransferCoordinator;
            if (coordinator == null) return;
            RunOnUi(delegate
            {
                if (_exitInProgress || IsDisposed || _hiddenToTray) return;
                if (!ReferenceEquals(
                        _fileTransferStatusSource,
                        coordinator))
                {
                    if (_fileTransferStatusForm != null)
                        _fileTransferStatusForm.Dispose();
                    _fileTransferStatusForm = null;
                    _fileTransferStatusSource = coordinator;
                    _lastFileTransferProgressSequence = 0;
                }
                if (e.Progress.Sequence <= _lastFileTransferProgressSequence)
                    return;
                _lastFileTransferProgressSequence = e.Progress.Sequence;
                if (_fileTransferStatusForm != null &&
                    !_fileTransferStatusForm.IsDisposed &&
                    !string.Equals(
                        _fileTransferStatusForm.SessionId,
                        e.Progress.SessionId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    _fileTransferStatusForm.Dispose();
                    _fileTransferStatusForm = null;
                }
                if (_fileTransferStatusForm == null ||
                    _fileTransferStatusForm.IsDisposed)
                {
                    _fileTransferStatusForm = new FileTransferStatusForm(
                        coordinator,
                        _settings.Theme, GetTransferDeviceName(coordinator));
                    _fileTransferStatusForm.PositionUpdateRequested +=
                        FileTransferStatusForm_PositionUpdateRequested;
                }
                var shouldShow = !_fileTransferStatusForm.Visible;
                _fileTransferStatusForm.UpdateProgress(e.Progress);
                var target = PositionTransferStatusWindow(
                    _fileTransferStatusForm,
                    _phoneTransferStatusForm,
                    coordinator,
                    e.Progress.SessionId);
                if (shouldShow)
                {
                    ShowTransferStatusWindow(
                        _fileTransferStatusForm,
                        target);
                }
            });
        }

        private void PhoneTransferReceiver_ProgressChanged(
            object sender,
            PhoneTransferProgressEventArgs e)
        {
            if (e == null || e.Progress == null) return;
            var receiver = sender as PhoneTransferReceiver;
            if (receiver == null) return;
            RunOnUi(delegate
            {
                if (_exitInProgress || IsDisposed || _hiddenToTray) return;
                if (!ReferenceEquals(_phoneTransferStatusSource, receiver))
                {
                    if (_phoneTransferStatusForm != null)
                        _phoneTransferStatusForm.Dispose();
                    _phoneTransferStatusForm = null;
                    _phoneTransferStatusSource = receiver;
                    _lastPhoneTransferProgressSequence = 0;
                }
                if (e.Progress.Sequence <=
                    _lastPhoneTransferProgressSequence)
                {
                    return;
                }
                _lastPhoneTransferProgressSequence = e.Progress.Sequence;
                if (_phoneTransferStatusForm == null ||
                    _phoneTransferStatusForm.IsDisposed)
                {
                    _phoneTransferStatusForm =
                        new PhoneTransferStatusForm(_settings.Theme, GetTransferDeviceName(receiver));
                }
                var shouldShow = !_phoneTransferStatusForm.Visible;
                _phoneTransferStatusForm.UpdateProgress(e.Progress);
                if (shouldShow)
                {
                    var target = PositionTransferStatusWindow(
                        _phoneTransferStatusForm,
                        _fileTransferStatusForm,
                        IntPtr.Zero);
                    ShowTransferStatusWindow(
                        _phoneTransferStatusForm,
                        target);
                }
            });
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            _deviceMonitor.StateChanged -= DeviceMonitor_StateChanged;
            _physicalDeviceRegistry.SnapshotChanged -=
                PhysicalDeviceRegistry_SnapshotChanged;
            _scrcpyService.RunningChanged -= ScrcpyService_RunningChanged;
            _singleWindowService.RunningChanged -=
                SingleWindowService_RunningChanged;
            _captureCoordinator.ExitHotkeyPressed -=
                CaptureCoordinator_ExitHotkeyPressed;
            _autoHideService.IdleHideRequested -=
                AutoHideService_IdleHideRequested;
            _fileTransferCoordinator.ProgressChanged -=
                FileTransferCoordinator_ProgressChanged;
            _phoneTransferReceiver.ProgressChanged -=
                PhoneTransferReceiver_ProgressChanged;
            _phoneScreenWakeTimer.Tick -= PhoneScreenWakeTimer_Tick;

            RequestAllRuntimeShutdown(_systemShutdownInProgress);
            if (_systemShutdownInProgress)
            {
                // Windows owns process termination from this point. Do not
                // dispose runtime services here: several normal Dispose paths
                // stop child processes or send additional ADB commands.
                TryCleanup("phone screen timer", _phoneScreenWakeTimer.Dispose);
                TryCleanup("device tooltips", _deviceTabToolTip.Dispose);
                TryCleanup("tray service", _trayService.Dispose);
                return;
            }
            var cleanupStillRunning = _exitCleanupTask != null &&
                !_exitCleanupTask.IsCompleted;
            if (!cleanupStillRunning)
                TryCleanup("device monitor", _deviceMonitor.Dispose);
            TryCleanup("phone screen timer", _phoneScreenWakeTimer.Dispose);
            TryCleanup("device tooltips", _deviceTabToolTip.Dispose);
            TryCleanup("app profile menu", _appProfileMenu.Dispose);
            if (_fileTransferStatusForm != null)
                TryCleanup(
                    "file transfer window",
                    _fileTransferStatusForm.Dispose);
            if (_phoneTransferStatusForm != null)
                TryCleanup(
                    "phone transfer window",
                    _phoneTransferStatusForm.Dispose);
            if (!cleanupStillRunning)
            {
                DisposeAllDeviceContexts();
            }
            TryCleanup("tray service", _trayService.Dispose);
            if (_logForm != null)
                TryCleanup("log window", _logForm.Dispose);
            if (_settingsForm != null)
                TryCleanup("settings window", _settingsForm.Dispose);
            if (_environmentCheckForm != null)
                TryCleanup(
                    "environment check window",
                    _environmentCheckForm.Dispose);
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.WindowsShutDown ||
                _systemShutdownInProgress)
            {
                BeginSystemShutdown();
                e.Cancel = false;
                return;
            }
            if (_allowExit) return;
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                if (!_exitInProgress) HideToTray();
                return;
            }

            _exitInProgress = true;
            _allowExit = true;
            RequestAllRuntimeShutdown();
            TryCleanup(
                "mini control bar",
                _miniControlBarManager.Dispose);
            TryCleanup("capture coordinator", _captureCoordinator.Stop);
            TryCleanup("key mapping", _keyMappingService.Stop);
            BeginPhoneScreenWakeSuppression();
            _phoneScreenWakeTimer.Stop();

            if (_exitCleanupTask == null)
            {
                var wakeSerials = CaptureWakeSerials();
                var cleanupSerial = GetSelectedDeviceSerial();
                _exitCleanupTask = RunExitCleanupAsync(
                    wakeSerials,
                    cleanupSerial);
            }
            try
            {
                if (!_exitCleanupTask.Wait(5000))
                {
                    e.Cancel = true;
                    _allowExit = false;
                    _logService.Warning(LocalizationService.Get(
                        "Log.Main.CleanupDeferredForShutdown"));
                    ScheduleCloseAfterExitCleanup();
                }
            }
            catch (AggregateException ex)
            {
                _logService.Error(
                    LocalizationService.Format(
                        "Log.Main.CleanupFailed",
                        "system shutdown"),
                    ex.GetBaseException());
            }
        }

        private void BeginSystemShutdown()
        {
            if (_systemShutdownInProgress) return;

            _systemShutdownInProgress = true;
            _exitInProgress = true;
            _allowExit = true;
            _logService.Info(LocalizationService.Get(
                "Log.Main.SystemShutdownDetected"));

            var cleanupTargets = CaptureWindowsShutdownCleanupTargets();
            BeginPhoneScreenWakeSuppression();
            _phoneScreenWakeTimer.Stop();

            // Freeze process creation first. The Companion command below uses
            // an existing socket and therefore remains available without ADB.
            _adbService.BlockNewProcessesForWindowsShutdown();

            // The only device operation allowed during Windows shutdown is a
            // write to the already-open Companion socket. Starting adb.exe at
            // this point can surface native application-error dialogs while
            // Windows is tearing down the desktop session.
            foreach (var target in cleanupTargets)
            {
                var sent = target.Guardian != null &&
                    target.Guardian.TrySendWindowsShutdown(
                        target.RemoveOverlay,
                        target.RestoreStayAwake,
                        target.OriginalStayAwakeValue);
                _logService.Info(DeviceLogFormatter.ForSerial(
                    target.Serial,
                    sent
                        ? "Sent Windows shutdown cleanup to DX Companion."
                        : "Skipped Windows shutdown device cleanup because " +
                          "a verified DX Companion session was not connected."));
            }

            RequestAllRuntimeShutdown(true);
            TryCleanup(
                "device monitor shutdown request",
                _deviceMonitor.RequestShutdown);
            StopAllInteractiveContextsForWindowsShutdown();
            // This only serializes values already held in memory and never
            // starts ADB, so it is safe after process creation is blocked.
            TrySavePendingRunSettingsForSystemShutdown();
        }

        private void TrySavePendingRunSettingsForSystemShutdown()
        {
            if (_selectedMode < 0 ||
                _selectedMode >= _modeSettingsDirty.Length ||
                !_modeSettingsDirty[_selectedMode])
            {
                return;
            }
            try
            {
                ApplyRunSettings(false);
                _modeSettingsDirty[_selectedMode] = false;
            }
            catch (Exception ex)
            {
                _logService.Warning(LocalizationService.Format(
                    "Log.Main.ModeSwitchSaveFailed",
                    ex.Message));
            }
        }

        private IList<WindowsShutdownCleanupTarget>
            CaptureWindowsShutdownCleanupTargets()
        {
            var targets = new List<WindowsShutdownCleanupTarget>();
            var added = new HashSet<string>(
                System.StringComparer.OrdinalIgnoreCase);
            foreach (var context in GetAllDeviceContexts())
            {
                var dex = context.Runtime.Scrcpy.GetSessionSnapshot();
                string original = null;
                var serial = GetWindowsShutdownSerial(context, dex);
                // Preserve the overlay-mode recovery policy, but a loopback
                // session must not delete overlays that it did not create.
                var removeOverlay = !string.IsNullOrWhiteSpace(serial) &&
                    context.Runtime.Dex.WantsOverlayCleanup;
                var restoreStayAwake =
                    _settings.Features.DisableStayAwakeOnStop &&
                    TryGetStayAwakeOriginalForContext(
                        context,
                        serial,
                        out original);
                if (!removeOverlay && !restoreStayAwake) continue;
                if (string.IsNullOrWhiteSpace(serial) || !added.Add(serial))
                    continue;

                targets.Add(new WindowsShutdownCleanupTarget
                {
                    Serial = serial,
                    Guardian = context.Runtime.CompanionGuardian,
                    RemoveOverlay = removeOverlay,
                    RestoreStayAwake = restoreStayAwake,
                    OriginalStayAwakeValue = restoreStayAwake &&
                        original != MissingStayAwakeValue
                        ? original
                        : null
                });
            }
            return targets;
        }

        private string GetWindowsShutdownSerial(
            DeviceUiContext context,
            ScrcpySessionSnapshot dex)
        {
            if (context == null || context.Runtime == null)
                return string.Empty;
            if (dex.IsRunning && !string.IsNullOrWhiteSpace(dex.Serial))
                return dex.Serial;
            foreach (var serial in context.Runtime.SingleWindows
                .GetRunningSerials())
            {
                if (!string.IsNullOrWhiteSpace(serial)) return serial;
            }
            return GetContextSerial(context);
        }

        private bool TryGetStayAwakeOriginalForContext(
            DeviceUiContext context,
            string preferredSerial,
            out string original)
        {
            if (TryGetStayAwakeOriginalValue(preferredSerial, out original))
                return true;
            if (context != null && context.Device != null &&
                context.Device.Transports != null)
            {
                foreach (var transport in context.Device.Transports)
                {
                    if (transport != null &&
                        TryGetStayAwakeOriginalValue(
                            transport.Serial,
                            out original))
                    {
                        return true;
                    }
                }
            }
            original = null;
            return false;
        }

        private sealed class WindowsShutdownCleanupTarget
        {
            public string Serial;
            public CompanionGuardianService Guardian;
            public bool RemoveOverlay;
            public bool RestoreStayAwake;
            public string OriginalStayAwakeValue;
        }

        private void ScheduleCloseAfterExitCleanup()
        {
            if (_forcedCloseContinuationScheduled ||
                _exitCleanupTask == null)
            {
                return;
            }
            _forcedCloseContinuationScheduled = true;
            _exitCleanupTask.ContinueWith(delegate
            {
                RunOnUi(delegate
                {
                    _allowExit = true;
                    if (!IsDisposed) Close();
                });
            }, TaskScheduler.Default);
        }

        private void TryCleanup(string operation, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                _logService.Error(
                    LocalizationService.Format(
                        "Log.Main.CleanupFailed",
                        operation),
                    ex);
            }
        }

        private void UpdateRunningState()
        {
            var running = IsSelectedModeRunning();
            _scrcpyStatusValue.Text = running
                ? LocalizationService.Get("Status.Running")
                : LocalizationService.Get("Status.Stopped");
            _dexStatusValue.Text = running
                ? LocalizationService.Get("Status.Running")
                : LocalizationService.Get("Status.Idle");
            UpdateStartAppAvailability();
            var dexBusy = _selectedMode == 0 &&
                (_orchestrator.IsBusy || _phonePreviewPendingStarts.ContainsKey(_orchestrator));
            _startButton.Enabled = !running && !dexBusy && GetContextTransport(_selectedDeviceContext) != null && GetContextTransport(_selectedDeviceContext).IsAuthorized;
            _stopButton.Enabled = running && !dexBusy;
            _startButton.Visible = !running;
            _stopButton.Visible = running;
            UpdateApplySettingsLink();
            UpdatePhonePreviewControls();
            if (!string.IsNullOrWhiteSpace(_connectionError))
            {
                SetConnectionIndicator(
                    Color.Firebrick,
                    LocalizationService.Get("Status.Error"),
                    _connectionError);
                return;
            }
            if (running && _selectedMode == 0)
                SetConnectionIndicator(
                    Color.ForestGreen,
                    LocalizationService.Get("Main.DexRunning"),
                    LocalizationService.Get("Main.DexRunningDetail"));
            else if (running)
                SetConnectionIndicator(
                    Color.ForestGreen,
                    LocalizationService.Format(
                        "Main.SingleRunning",
                        _selectedMode),
                    GetSingleWindowStatusDetail(_selectedMode));
            else if (_selectedMode > 0)
                UpdateSingleWindowIndicator(_selectedMode);
            else
                UpdateIndicatorForDevice(_lastDeviceState);
        }

        private void SetOperationState(bool operationRunning, string status)
        {
            var running = IsSelectedModeRunning();
            _startButton.Visible = !running;
            _stopButton.Visible = running;
            UpdateStartAppAvailability();
            _startButton.Enabled = !operationRunning && !running && GetContextTransport(_selectedDeviceContext) != null && GetContextTransport(_selectedDeviceContext).IsAuthorized;
            _stopButton.Enabled = !operationRunning && running;
            _applySettingsLink.Enabled = !operationRunning;
            UpdatePhonePreviewControls();
            if (operationRunning) _hidePhonePreviewBox.Enabled = false;
            _dexStatusValue.Text = status;
        }

        private void UpdateIndicatorForDevice(DeviceState state)
        {
            if (!string.IsNullOrWhiteSpace(_connectionError))
            {
                SetConnectionIndicator(
                    Color.Firebrick,
                    LocalizationService.Get("Status.Error"),
                    _connectionError);
                return;
            }
            if (state != null && state.Status == AdbDeviceStatus.Device)
            {
                SetConnectionIndicator(
                    Color.ForestGreen,
                    LocalizationService.Get("Main.PhoneConnected"),
                    LocalizationService.Get("Main.WaitingDex"));
                return;
            }
            if (state != null && state.Status == AdbDeviceStatus.Unauthorized)
            {
                SetConnectionIndicator(
                    Color.DarkOrange,
                    LocalizationService.Get("Main.AuthorizationRequired"),
                    LocalizationService.Get("Main.AuthorizationDetail"));
                return;
            }
            if (state != null && state.Status == AdbDeviceStatus.Offline)
            {
                SetConnectionIndicator(
                    Color.Firebrick,
                    LocalizationService.Get("Main.DeviceOffline"),
                    LocalizationService.Get("Main.DeviceOfflineDetail"));
                return;
            }
            SetConnectionIndicator(
                Color.DarkOrange,
                LocalizationService.Get("Main.Waiting"),
                LocalizationService.Get("Main.WaitingPhone"));
        }

        private void SetConnectionIndicator(Color color, string status, string detail)
        {
            _indicatorDot.StatusColor = color;
            var argb = color.ToArgb();
            _indicatorDot.Complete =
                argb == Color.ForestGreen.ToArgb() ||
                argb == Color.Green.ToArgb() ||
                argb == Color.DarkGreen.ToArgb();
            if (_indicatorDot.Complete && !IsSelectedModeRunning()) { status = _selectedMode==0 ? LocalizationService.Get("Main.DexReady") : LocalizationService.Format(IsSingleWindowAppMissing(_selectedMode)?"Main.SingleWaiting":"Main.SingleReady",_selectedMode); _indicatorDot.StatusColor = _theme.Accent; }
            _indicatorStatus.Text = status;
            var device = _lastDeviceState;
            _indicatorDetail.Text =
                _settings.Features.ShowConnectedDeviceInfo &&
                device != null &&
                device.Status == AdbDeviceStatus.Device &&
                !string.IsNullOrWhiteSpace(_deviceInfoLabel.Text)
                    ? detail + "  ·  " + _deviceInfoLabel.Text
                    : detail;
        }

        private void ShowError(string message, Exception exception)
        {
            _logService.Error(message, exception);
            _connectionError = message + ": " + exception.Message;
            SetConnectionIndicator(
                Color.Firebrick,
                LocalizationService.Get("Status.Error"),
                _connectionError);
            ShowDesktopNotice(message + Environment.NewLine + exception.Message, DesktopNoticeKind.Error);
        }
    }
}
