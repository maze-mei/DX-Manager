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
        private void RunOnUi(Action action)
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (!InvokeRequired)
            {
                action();
                return;
            }

            try
            {
                BeginInvoke(action);
            }
            catch (InvalidOperationException)
            {
                // The form handle can be destroyed between the checks above.
            }
        }

        private void HideToTray() { HideToTray(true); }
        private bool _hiddenToTray;
        private void HideToTray(bool showBalloon)
        {
            _hiddenToTray = true;
            CloseAttachedWindowsForTray();
            Hide();
            ShowInTaskbar = false;
            if (showBalloon)
                _trayService.ShowBalloon(
                    LocalizationService.Get("App.Name"),
                    LocalizationService.Get("Main.TrayContinues"));
        }

        private void CloseAttachedWindowsForTray()
        {
            var windows = new List<Form>();
            foreach (Form window in Application.OpenForms)
            {
                // scrcpy windows are outside this process; its mini bars remain separately managed.
                if (!ReferenceEquals(window, this) && !(window is MiniControlBarForm))
                    windows.Add(window);
            }
            for (var index = windows.Count - 1; index >= 0; index--)
            {
                var window = windows[index];
                if (window.IsDisposed || window.Disposing) continue;
                if (window.InvokeRequired)
                    window.BeginInvoke((Action)delegate { if (!window.IsDisposed) window.Close(); });
                else
                {
                    var hint = window as CaptureHintOverlayForm;
                    if (hint != null) hint.HideHint();
                    else window.Close();
                }
            }
        }
        private bool _explicitShowRequested;

        internal void RequestWindowActivation()
        {
            RunOnUi(delegate
            {
                if (_exitInProgress || _systemShutdownInProgress || IsDisposed || Disposing) return;
                ShowMainWindow();
            });
        }

        private void ShowMainWindow()
        {
            RunOnUi(delegate
            {
                _explicitShowRequested = true;
                _hiddenToTray = false;
                _autoHideService.ResetIdleHideState();
                ShowInTaskbar = true;
                Show();
                RefreshUpdateBanner();
                WindowState = FormWindowState.Normal;
                Activate();
            });
        }

        private void ShowLogForm()
        {
            RunOnUi(delegate
            {
                _autoHideService.ResetIdleHideState();
                if (_logForm == null || _logForm.IsDisposed) _logForm = new LogForm(_logService);
                if (!_logForm.Visible) _logForm.Show();
                _logForm.WindowState = FormWindowState.Normal;
                _logForm.BringToFront();
                _logForm.Activate();
            });
        }

        private void ShowSettingsForm()
        {
            RunOnUi(delegate
            {
                _autoHideService.ResetIdleHideState();
                if (_settingsForm == null || _settingsForm.IsDisposed)
                {
                    _settingsForm = new SettingsForm(
                        _settingsService,
                        _settings,
                        _adbService,
                        _wirelessAdbService,
                        GetSelectedDeviceSerial,
                        delegate { return _selectedDeviceIdentity; },
                        delegate { return _physicalDeviceRegistry.Current; },
                        delegate { return _runtimeSessions.Current; },
                        _logService,
                        ShowLogForm,
                        ShowEnvironmentCheck,
                        ApplyThemeSelection,
                        ApplyGeneralSettingsChanges,
                        DetachSelectedPhoneTransferAsync,
                        ConfigurePhoneTransferReceiver,
                        SetHotkeyEditorActive,
                        delegate { return new EnvironmentCheckService(_adbService, _scrcpyService, _pathService, _logService, _settingsService, _settings, delegate { return _settingsForm == null ? GetSelectedDeviceSerial() : _settingsForm.GetSettingsSelectedSerial(); }).Run(); });
                    _settingsForm.FormClosed += delegate { _settingsForm = null; };
                }
                _settingsForm.UpdateCheckRequested = ShowUpdateCheckDialog;
                if (!_settingsForm.Visible) _settingsForm.Show();
                _settingsForm.WindowState = FormWindowState.Normal;
                _settingsForm.BringToFront();
                _settingsForm.Activate();
            });
        }

        private void SetHotkeyEditorActive(bool active)
        {
            if (_exitInProgress || IsDisposed || !_interactiveServicesStarted) return;
            if (active) _captureCoordinator.Stop();
            else
            {
                try { _captureCoordinator.Start(); }
                catch (Exception ex) { _logService.Error("Could not resume capture hotkeys.", ex); }
            }
        }
        private void ApplyGeneralSettingsChanges(bool defaultsRestored)
        {
            _logService.ConfigureAutomaticLogs(_settings.Features.AutoSaveSessionLogs,AppDomain.CurrentDomain.BaseDirectory);
            if (_lastAutoStartEnabled && !_settings.Features.AutoStartDexOnDeviceConnected) System.Threading.Interlocked.Increment(ref _autoStartSettingsGeneration);
            _lastAutoStartEnabled = _settings.Features.AutoStartDexOnDeviceConnected;
            if (defaultsRestored)
            {
                LoadRunSettings();
                Array.Clear(_modeSettingsDirty, 0, _modeSettingsDirty.Length);
                UpdateApplySettingsLink();
            }

            ReconcileDeviceTabs(_physicalDeviceRegistry.Current);

            try
            {
                _captureCoordinator.ReloadHotkeys();
            }
            catch (Exception ex)
            {
                _logService.Error(
                    LocalizationService.Get(
                        "Log.Main.CaptureHotkeyRegistrationFailed"),
                    ex);
                _trayService.ShowBalloon(
                    LocalizationService.Get("App.Name"),
                    LocalizationService.Get("Main.CaptureHotkeyFailed"));
            }

            try
            {
                _autoStartService.Apply(
                    _settings.Features.StartWithWindows);
            }
            catch (Exception ex)
            {
                _logService.Error(
                    LocalizationService.Get(
                        "Log.Main.AutoStartApplyFailed"),
                    ex);
            }

            foreach (var context in GetAllDeviceContexts())
            {
                context.AutoHide.ApplySettings(
                    ReferenceEquals(context, _selectedDeviceContext) &&
                    _settings.Features.AutoHideEnabled,
                    _settings.Timing.AutoHideIdleSeconds);
                context.MiniBar.ApplySettings();
                context.KeyMapping.ReloadAppRules();
                context.Runtime.PhoneTransfers.ApplySettings();
            }
            _wirelessAdbService.SynchronizeTargetWithSettings();
            if (_settings.Features.PhoneToPcTransferEnabled)
            {
                foreach (var context in GetAllDeviceContexts())
                {
                    var serial = GetContextSerial(context);
                    if (!string.IsNullOrWhiteSpace(serial))
                    {
                        var transport = context.Device == null
                            ? null
                            : context.Device.FindTransport(serial);
                        ConfigurePhoneTransferReceiver(
                            context,
                            serial,
                            transport == null
                                ? DeviceTransportKind.Unknown
                                : transport.Kind);
                    }
                }
            }

            try
            {
                _logService.SetLogDirectory(
                    _settingsService.ResolvePath(
                        _settings.Paths.LogFolder));
            }
            catch (Exception ex)
            {
                _logService.Error(
                    LocalizationService.Get(
                        "Log.Main.LogFolderApplyFailed"),
                    ex);
            }

            UpdateRunningState();
        }

        private void ShowEnvironmentCheck()
        {
            RunOnUi(delegate
            {
                _autoHideService.ResetIdleHideState();
                if (_environmentCheckForm == null || _environmentCheckForm.IsDisposed)
                {
                    _environmentCheckForm = new EnvironmentCheckForm(_environmentCheckService);
                    _environmentCheckForm.FormClosed += delegate { _environmentCheckForm = null; };
                }
                if (!_environmentCheckForm.Visible) _environmentCheckForm.Show();
                _environmentCheckForm.WindowState = FormWindowState.Normal;
                _environmentCheckForm.BringToFront();
                _environmentCheckForm.Activate();
            });
        }

        private void HideApplicationForIdle()
        {
            HideToTray(false);
        }

        private async void ExitApplication()
        {
            if (InvokeRequired) { BeginInvoke((Action)ExitApplication); return; }
            if (_exitInProgress) return;

            _exitInProgress = true;
            _logService.Diagnostic("Shutdown", "Explicit application exit requested.");
            TraceExitState("before-runtime-shutdown-request");
            RequestAllRuntimeShutdown();
            var wakeSerials = CaptureWakeSerials();
            _logService.Diagnostic("Shutdown", "Captured wake targets: count="+wakeSerials.Count+"; serials="+string.Join(",",wakeSerials));
            var cleanupSerial = GetSelectedDeviceSerial();
            TryCleanup(
                "mini control bar",
                _miniControlBarManager.Dispose);
            TryCleanup("capture coordinator", _captureCoordinator.Stop);
            TryCleanup("key mapping", _keyMappingService.Stop);
            BeginPhoneScreenWakeSuppression();
            _phoneScreenWakeTimer.Stop();
            _dexStatusValue.Text =
                LocalizationService.Get("Status.ShuttingDown");
            Enabled = false;

            try
            {
                _exitCleanupTask = RunExitCleanupAsync(
                    wakeSerials,
                    cleanupSerial);
                await _exitCleanupTask;
            }
            finally
            {
                _allowExit = true;
                if (!IsDisposed) Close();
            }
        }

        private IList<string> CaptureWakeSerials()
        {
            var serials = new List<string>(_managedSerialHistory);
            foreach (var serial in GetScreenOffSerials())
                AddSerial(serials, serial);
            return serials;
        }

        private void TraceExitState(string stage)
        {
            try
            {
                foreach(var context in GetAllDeviceContexts())
                {
                    var scrcpy=context.Runtime.Scrcpy.GetSessionSnapshot();
                    var native=context.Runtime.Dex.RetainedNativeLease;
                    string original;
                    var hasOriginal=TryGetStayAwakeOriginalForContext(context,context.ActiveSerial,out original);
                    _logService.Diagnostic("Shutdown", "State stage="+stage+"; identity="+context.Identity+
                        "; activeSerial="+context.ActiveSerial+"; connected="+context.WasConnected+
                        "; generation="+context.ConnectionGeneration+"; scrcpyRunning="+scrcpy.IsRunning+
                        "; screenOffRequested="+scrcpy.ScreenOffRequested+"; singleWindowCount="+context.Runtime.SingleWindows.RunningCount+
                        "; nativePending="+(native!=null&&native.NativeStartRequested)+
                        "; nativeDisplay="+(native==null?-1:native.DisplayId)+
                        "; originalStayAwake="+(hasOriginal?original:"<not-recorded>"));
                }
            }
            catch(Exception error){_logService.Diagnostic("Shutdown","State snapshot unavailable: "+error.GetType().Name+": "+error.Message);}
        }

        private async Task RunExitCleanupAsync(
            IList<string> wakeSerials,
            string cleanupSerial)
        {
            _logService.Diagnostic("Shutdown","Runtime session cleanup begin; wakeTargetCount="+wakeSerials.Count);
            var runtimeCleanupWatch=System.Diagnostics.Stopwatch.StartNew();
            await CleanupAllRuntimeSessionsAsync().ConfigureAwait(false);
            _logService.Diagnostic("Shutdown","Runtime session cleanup returned; elapsedMs="+runtimeCleanupWatch.ElapsedMilliseconds);
            TraceExitState("after-runtime-session-cleanup");
            var supplementalCleanup = new[]
            {
                TryCleanupAsync(
                    "device monitor",
                    delegate
                    {
                        return Task.Run((Action)_deviceMonitor.Stop);
                    }),
                TryCleanupAsync(
                    "stay-awake setting",
                    QueueDeviceStayAwakeUpdate),
                TryCleanupAsync(
                    "phone screen wake",
                    delegate
                    {
                        return Task.Run(delegate
                        {
                            WakePhoneScreens(wakeSerials);
                        });
                    })
            };
            await Task.WhenAll(supplementalCleanup).ConfigureAwait(false);

            try
            {
                await TryCleanupAsync(
                    "ADB server",
                    delegate
                    {
                        return Task.Run(delegate
                        {
                            _adbService.KillServer();
                        });
                    }).ConfigureAwait(false);
            }
            finally
            {
                // The monitor and all device restoration work have finished.
                // Prevent a late callback from starting the ADB server again
                // between kill-server and process exit.
                _adbService.BeginProcessShutdown();
                _logService.Diagnostic("Shutdown","ADB server cleanup finished; new process creation is now blocked.");
            }
        }

        private async Task TryCleanupAsync(
            string operation,
            Func<Task> action)
        {
            var watch=System.Diagnostics.Stopwatch.StartNew();
            _logService.Diagnostic("Shutdown","Cleanup step begin: "+operation);
            try
            {
                await action().ConfigureAwait(false);
                _logService.Diagnostic("Shutdown","Cleanup step completed: "+operation+"; elapsedMs="+watch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logService.Diagnostic("Shutdown","Cleanup step failed: "+operation+"; elapsedMs="+watch.ElapsedMilliseconds+"; error="+ex.GetType().Name+": "+ex.Message);
                _logService.Error(
                    LocalizationService.Format(
                        "Log.Main.CleanupFailed",
                        operation),
                    ex);
            }
        }
    }
}
