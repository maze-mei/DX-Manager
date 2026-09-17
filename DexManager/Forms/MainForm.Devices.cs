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
        private sealed class DeviceUiContext
        {
            public string Identity;
            public string DisplayName = string.Empty;
            public PhysicalDeviceInfo Device;
            public DeviceRuntimeServiceSet Runtime;
            public CaptureCoordinator Capture;
            public AutoHideService AutoHide;
            public EnvironmentCheckService EnvironmentCheck;
            public KeyMappingService KeyMapping;
            public MiniControlBarManager MiniBar;
            public bool WasConnected;
            public string ActiveSerial = string.Empty;
            public int ConnectionGeneration;
            public int SelectedMode;
            public bool[] ModeSettingsDirty = new bool[4];
        }

        private DeviceUiContext CreateInitialDeviceContext()
        {
            return new DeviceUiContext
            {
                Runtime = _activeRuntime,
                Capture = _captureCoordinator,
                AutoHide = _autoHideService,
                EnvironmentCheck = _environmentCheckService,
                KeyMapping = _keyMappingService,
                MiniBar = _miniControlBarManager,
                SelectedMode = _selectedMode,
                ModeSettingsDirty = _modeSettingsDirty
            };
        }

        private DeviceUiContext CreateDeviceContext(
            DeviceRuntimeServiceSet runtime)
        {
            var hotkeys = new HotkeyService(_logService, _settings);
            var capture = new CaptureCoordinator(
                hotkeys,
                _captureService,
                runtime.Scrcpy,
                runtime.SingleWindows,
                _settings,
                _logService);
            var context = new DeviceUiContext
            {
                Runtime = runtime,
                Capture = capture,
                AutoHide = new AutoHideService(
                    runtime.Scrcpy,
                    runtime.SingleWindows,
                    _logService,
                    _settings.Timing.AutoHideIdleSeconds),
                KeyMapping = new KeyMappingService(
                    runtime.Scrcpy,
                    runtime.SingleWindows,
                    _adbService,
                    _settings,
                    _settings.KeyMappings,
                    _logService)
            };
            context.EnvironmentCheck = new EnvironmentCheckService(
                _adbService,
                runtime.Scrcpy,
                _pathService,
                _logService,
                _settingsService,
                _settings,
                delegate { return GetContextSerial(context); });
            context.MiniBar = new MiniControlBarManager(
                _settings,
                runtime.Scrcpy,
                runtime.SingleWindows,
                capture,
                ShowMainWindow,
                _logService);
            return context;
        }

        private void RebuildDeviceTabs()
        {
            _deviceTabsPanel.Visible = _deviceTabsVisibleForRun;
            _deviceTabsPanel.SuspendLayout();
            try
            {
                while (_deviceTabsPanel.Controls.Count > 0)
                    _deviceTabsPanel.Controls[0].Dispose();
                _deviceTabsPanel.Controls.Clear();
                _deviceTabButtons.Clear();
                foreach (var item in GetDeviceContextEntries())
                {
                    var context = item.Value;
                    var device = context.Device;
                    var transport = GetContextTransport(context);
                    var connected = transport != null &&
                        transport.IsAuthorized;
                    var transportText = GetTransportText(transport);
                    var statusText = connected
                        ? LocalizationService.Format(
                            "Device.Connected",
                            transportText)
                        : device != null && device.IsConnected
                            ? GetConfiguredTransportWaitingText(context)
                            : LocalizationService.Get(
                                "Device.Disconnected");
                    var button = new ThemedButton
                    {
                        Size = new Size(158, 52),
                        Margin = new Padding(0, 0, 0, 6),
                        NavigationStyle = true,
                        DeviceNavigationStyle = true,
                        Primary = ReferenceEquals(
                            context,
                            _selectedDeviceContext),
                        Text = GetContextDisplayName(context),
                        SecondaryText = statusText,
                        StatusColor = connected
                            ? Color.FromArgb(40, 156, 72)
                            : device != null && device.IsConnected
                                ? Color.FromArgb(224, 143, 24)
                                : _theme.TextTertiary,
                        CornerRadius = 10,
                        BackColor = _theme.NavigationBackground,
                        ForeColor = _theme.TextSecondary,
                        TabStop = false
                    };
                    button.Click += delegate
                    {
                        SelectDeviceContext(context);
                    };
                    _deviceTabToolTip.SetToolTip(
                        button,
                        GetContextDisplayName(context) +
                            Environment.NewLine + statusText);
                    _deviceTabButtons[item.Key] = button;
                    _deviceTabsPanel.Controls.Add(button);
                }
            }
            finally
            {
                _deviceTabsPanel.ResumeLayout();
            }
            LayoutSidebarNavigation();
        }

        private static int CountConnectedPhysicalDevices(
            DeviceRegistrySnapshot snapshot)
        {
            var count = 0;
            if (snapshot == null || snapshot.Devices == null) return count;
            foreach (var device in snapshot.Devices)
            {
                if (device != null && device.IsConnected) count++;
            }
            return count;
        }

        private void SelectDeviceContext(DeviceUiContext context)
        {
            SelectDeviceContext(context, true);
        }

        private void SelectDeviceContext(
            DeviceUiContext context,
            bool saveCurrentMode)
        {
            if (context == null || ReferenceEquals(
                    context,
                    _selectedDeviceContext) &&
                !string.IsNullOrWhiteSpace(context.Identity))
            {
                return;
            }

            if (saveCurrentMode) SaveCurrentModeBeforeSwitch();
            if (_selectedDeviceContext != null)
            {
                _selectedDeviceContext.SelectedMode = _selectedMode;
                _selectedDeviceContext.ModeSettingsDirty =
                    _modeSettingsDirty;
            }
            StopInteractiveContext(_selectedDeviceContext);
            _selectedDeviceContext = context;
            _selectedDeviceIdentity = context.Identity ?? string.Empty;
            _modeSettingsDirty = context.ModeSettingsDirty ??
                new bool[4];
            ActivateContextServices(context);
            if (_interactiveServicesStarted)
                StartInteractiveContext(context);
            if (_settingsForm != null && !_settingsForm.IsDisposed)
                _settingsForm.RefreshSelectedDeviceContext();
            if (_environmentCheckForm != null &&
                !_environmentCheckForm.IsDisposed)
            {
                _environmentCheckForm.Close();
            }
            _connectionError = null;
            var serial = GetContextSerial(context);
            var transport = GetContextTransport(context);
            _logService.Info(LocalizationService.Format(
                "Log.DeviceContext.Selected",
                GetContextDisplayName(context),
                string.IsNullOrWhiteSpace(serial)
                    ? context.Identity
                    : serial,
                GetTransportText(transport)));
            RefreshSelectedDeviceState();
            RebuildDeviceTabs();
            DisplayMode(context.SelectedMode);
        }

        private void ActivateContextServices(DeviceUiContext context)
        {
            _activeRuntime = context.Runtime;
            _fileTransferCoordinator = context.Runtime.FileTransfers;
            _phoneTransferReceiver = context.Runtime.PhoneTransfers;
            _scrcpyService = context.Runtime.Scrcpy;
            _singleWindowService = context.Runtime.SingleWindows;
            _screenOffService = context.Runtime.ScreenOff;
            _orchestrator = context.Runtime.Dex;
            _captureCoordinator = context.Capture;
            _autoHideService = context.AutoHide;
            _environmentCheckService = context.EnvironmentCheck;
            _keyMappingService = context.KeyMapping;
            _miniControlBarManager = context.MiniBar;
        }

        private void StartInteractiveContext(DeviceUiContext context)
        {
            if (context == null) return;
            try { context.Capture.Start(); }
            catch (Exception ex)
            {
                _logService.Error(
                    LocalizationService.Get(
                        "Log.Main.CaptureHotkeyRegistrationFailed"),
                    ex);
            }
            if (_settings.Features.AutoHideEnabled)
                context.AutoHide.Start();
            try { context.KeyMapping.Start(); }
            catch (Exception ex)
            {
                _logService.Error(
                    LocalizationService.Get(
                        "Log.Main.KeyMappingStartFailed"),
                    ex);
            }
        }

        private void StopInteractiveContext(DeviceUiContext context)
        {
            if (context == null) return;
            TryCleanup("capture coordinator", context.Capture.Stop);
            TryCleanup("automatic hide", context.AutoHide.Stop);
            TryCleanup("key mapping", context.KeyMapping.Stop);
        }

        private void RefreshSelectedDeviceState()
        {
            var context = _selectedDeviceContext;
            var device = context == null ? null : context.Device;
            var transport = GetContextTransport(context);
            _lastDeviceState = transport == null
                ? DeviceState.Disconnected()
                : new DeviceState
                {
                    IsConnected = transport.IsAuthorized,
                    Serial = transport.Serial,
                    DisplayName = device.DisplayName,
                    Status = transport.Status
                };
            _deviceInfoLabel.Text = device == null
                ? LocalizationService.Get("Main.WaitingPhone")
                : transport == null || !transport.IsAuthorized
                    ? LocalizationService.Format(
                        "Main.WaitingConfiguredTransport",
                        GetContextDisplayName(context),
                        GetConfiguredTransportWaitingText(context))
                    : LocalizationService.Format(
                        "Main.ConnectedDevice",
                        GetContextDisplayName(context),
                        GetTransportText(transport));
            _adbStatusValue.Text = _lastDeviceState.IsConnected
                ? LocalizationService.Get("Status.Ready")
                : LocalizationService.Get("Status.Idle");
            _deviceStatusValue.Text = GetDeviceStatusText(_lastDeviceState);
            if (!IsSelectedModeRunning())
                UpdateIndicatorForDevice(_lastDeviceState);
        }

        private string GetContextSerial(DeviceUiContext context)
        {
            var transport = GetContextTransport(context);
            return transport == null
                ? string.Empty
                : transport.Serial ?? string.Empty;
        }

        private DeviceTransportInfo GetContextTransport(
            DeviceUiContext context)
        {
            if (context == null || context.Device == null ||
                string.IsNullOrWhiteSpace(context.Identity))
            {
                return null;
            }

            var profile = _settings.FindDeviceWirelessConnection(
                context.Identity);
            var mode = profile == null
                ? AdbConnectionMode.Usb
                : profile.Mode;
            var preferredSerial = context.ActiveSerial ?? string.Empty;
            if (profile != null &&
                profile.Mode == AdbConnectionMode.Wireless)
            {
                preferredSerial = WirelessAdbService.BuildEndpoint(
                    profile.WirelessHost,
                    profile.WirelessPort);
            }
            else if (profile == null &&
                _settings.Connection != null &&
                _settings.Connection.Mode ==
                    AdbConnectionMode.Wireless)
            {
                var legacyEndpoint = WirelessAdbService.BuildEndpoint(
                    _settings.Connection.WirelessHost,
                    _settings.Connection.WirelessPort);
                if (context.Device.FindTransport(legacyEndpoint) != null)
                {
                    mode = AdbConnectionMode.Wireless;
                    preferredSerial = legacyEndpoint;
                }
            }

            return context.Device.SelectAuthorizedTransport(
                mode == AdbConnectionMode.Wireless
                    ? DeviceTransportKind.Wireless
                    : DeviceTransportKind.Usb,
                preferredSerial);
        }

        private string GetConfiguredTransportWaitingText(
            DeviceUiContext context)
        {
            var profile = context == null
                ? null
                : _settings.FindDeviceWirelessConnection(
                    context.Identity);
            var wireless = profile != null &&
                profile.Mode == AdbConnectionMode.Wireless;
            if (profile == null && context != null &&
                context.Device != null &&
                _settings.Connection != null &&
                _settings.Connection.Mode ==
                    AdbConnectionMode.Wireless)
            {
                var legacyEndpoint = WirelessAdbService.BuildEndpoint(
                    _settings.Connection.WirelessHost,
                    _settings.Connection.WirelessPort);
                wireless = context.Device.FindTransport(
                    legacyEndpoint) != null;
            }
            return LocalizationService.Get(
                wireless
                    ? "Device.WaitingWireless"
                    : "Device.WaitingUsb");
        }

        private static string GetContextDisplayName(DeviceUiContext context)
        {
            if (context == null) return string.Empty;
            if (context.Device != null &&
                !string.IsNullOrWhiteSpace(context.Device.DisplayName))
            {
                return context.Device.DisplayName;
            }
            if (!string.IsNullOrWhiteSpace(context.DisplayName))
                return context.DisplayName;
            return context.Identity ?? string.Empty;
        }

        private static void RememberContextDisplayName(
            DeviceUiContext context,
            PhysicalDeviceInfo device)
        {
            if (context == null || device == null ||
                string.IsNullOrWhiteSpace(device.DisplayName))
            {
                return;
            }
            context.DisplayName = device.DisplayName.Trim();
        }

        private static void ConfigureContextPresentation(
            DeviceUiContext context)
        {
            if (context == null || context.Runtime == null) return;
            var displayName = GetContextDisplayName(context);
            context.Runtime.Scrcpy.DeviceDisplayName = displayName;
            context.Runtime.SingleWindows.DeviceDisplayName = displayName;
        }

        private static string GetTransportText(DeviceTransportInfo transport)
        {
            if (transport == null) return "-";
            switch (transport.Kind)
            {
                case DeviceTransportKind.Usb: return "USB";
                case DeviceTransportKind.Wireless: return "Wi-Fi";
                case DeviceTransportKind.Emulator: return "Emulator";
                default: return "ADB";
            }
        }

        private bool IsActiveRuntimeSender(object sender)
        {
            return ReferenceEquals(sender, _scrcpyService) ||
                ReferenceEquals(sender, _singleWindowService);
        }

        private ScreenOffService GetScreenOffServiceForSerial(string serial)
        {
            foreach (var context in GetAllDeviceContexts())
            {
                var dexSession = context.Runtime.Scrcpy
                    .GetSessionSnapshot();
                if (string.Equals(
                        dexSession.Serial,
                        serial,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return context.Runtime.ScreenOff;
                }
                foreach (var candidate in context.Runtime.SingleWindows
                    .GetRunningSerials())
                {
                    if (string.Equals(
                            candidate,
                            serial,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return context.Runtime.ScreenOff;
                    }
                }
            }
            return _screenOffService;
        }

        private void RequestAllRuntimeShutdown()
        {
            RequestAllRuntimeShutdown(false);
        }

        private void RequestAllRuntimeShutdown(bool windowsShutdown)
        {
            foreach (var context in GetAllDeviceContexts())
            {
                context.Runtime.Dex.RequestShutdown();
                context.Runtime.SingleWindows.RequestShutdown();
                context.Runtime.ScreenOff.RequestShutdown();
                context.Runtime.FileTransfers.RequestShutdown();
                if (windowsShutdown)
                {
                    context.Runtime.PhoneTransfers.RequestWindowsShutdown();
                    context.Runtime.CompanionGuardian
                        .RequestWindowsShutdown();
                }
                else
                {
                    context.Runtime.PhoneTransfers.RequestShutdown();
                    context.Runtime.CompanionGuardian.RequestShutdown();
                }
            }
        }

        private void StopAllInteractiveContextsForWindowsShutdown()
        {
            foreach (var context in GetAllDeviceContexts())
            {
                TryCleanup(
                    "mini control bar",
                    context.MiniBar.Dispose);
                TryCleanup(
                    "capture coordinator",
                    context.Capture.Stop);
                TryCleanup(
                    "automatic hide",
                    context.AutoHide.Stop);
                TryCleanup(
                    "key mapping",
                    context.KeyMapping.Stop);
            }
        }

        private IList<DeviceUiContext> GetAllDeviceContexts()
        {
            var result = new List<DeviceUiContext>();
            if (_initialDeviceContext != null)
                result.Add(_initialDeviceContext);
            lock (_deviceContextsSync)
            {
                foreach (var context in _deviceContexts.Values)
                {
                    if (!result.Contains(context)) result.Add(context);
                }
            }
            return result;
        }

        private IList<KeyValuePair<string, DeviceUiContext>>
            GetDeviceContextEntries()
        {
            lock (_deviceContextsSync)
            {
                var result = new List<KeyValuePair<string, DeviceUiContext>>();
                var added = new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);
                foreach (var identity in
                    _devicePresentationOrder.GetIdentities())
                {
                    DeviceUiContext context;
                    if (_deviceContexts.TryGetValue(identity, out context))
                    {
                        result.Add(new KeyValuePair<string, DeviceUiContext>(
                            identity,
                            context));
                        added.Add(identity);
                    }
                }
                foreach (var item in _deviceContexts)
                {
                    if (added.Add(item.Key)) result.Add(item);
                }
                return result;
            }
        }

        private async Task CleanupAllRuntimeSessionsAsync()
        {
            foreach (var context in GetAllDeviceContexts())
            {
                var serial = GetContextSerial(context);
                await TryCleanupAsync(
                    "DeX session " + GetContextDisplayName(context),
                    delegate
                    {
                        return context.Runtime.Dex.ShutdownAsync(serial);
                    }).ConfigureAwait(false);
                await TryCleanupAsync(
                    "single-window sessions " +
                        GetContextDisplayName(context),
                    delegate
                    {
                        return Task.Run(
                            (Action)context.Runtime.SingleWindows.StopAll);
                    }).ConfigureAwait(false);
            }
        }

        private void DisposeAllDeviceContexts()
        {
            foreach (var context in GetAllDeviceContexts())
            {
                context.Runtime.Scrcpy.RunningChanged -=
                    ScrcpyService_RunningChanged;
                context.Runtime.SingleWindows.RunningChanged -=
                    SingleWindowService_RunningChanged;
                context.Capture.ExitHotkeyPressed -=
                    CaptureCoordinator_ExitHotkeyPressed;
                context.AutoHide.IdleHideRequested -=
                    AutoHideService_IdleHideRequested;
                context.Runtime.FileTransfers.ProgressChanged -=
                    FileTransferCoordinator_ProgressChanged;
                context.Runtime.PhoneTransfers.ProgressChanged -=
                    PhoneTransferReceiver_ProgressChanged;
                TryCleanup("mini control bar", context.MiniBar.Dispose);
                TryCleanup("capture coordinator", context.Capture.Dispose);
                TryCleanup("automatic hide", context.AutoHide.Dispose);
                TryCleanup("key mapping", context.KeyMapping.Dispose);
                TryCleanup(
                    "screen-off service",
                    context.Runtime.ScreenOff.Dispose);
                TryCleanup(
                    "single-window service",
                    context.Runtime.SingleWindows.Dispose);
                TryCleanup(
                    "scrcpy service",
                    context.Runtime.Scrcpy.Dispose);
                TryCleanup(
                    "file transfer service",
                    context.Runtime.FileTransfers.Dispose);
                TryCleanup(
                    "phone transfer receiver",
                    context.Runtime.PhoneTransfers.Dispose);
                TryCleanup(
                    "Companion guardian",
                    context.Runtime.CompanionGuardian.Dispose);
            }
        }

        private Task DetachSelectedPhoneTransferAsync(string serial)
        {
            return _phoneTransferReceiver.DetachAsync(serial);
        }
    }
}
