using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DexManager.Models;
using DexManager.Services;
using DexManager.Utils;

namespace DexManager.Forms
{
    public sealed partial class SettingsForm : Form, IMessageFilter
    {
        private void UpdateWirelessControls()
        {
            var option = GetSelectedWirelessDeviceOption();
            var enabled = _wirelessConnectionBox != null &&
                _wirelessConnectionBox.Checked && option != null;
            if (_wirelessHostBox != null)
                _wirelessHostBox.Enabled = enabled;
            if (_wirelessPortBox != null)
                _wirelessPortBox.Enabled = enabled;
            if (_wirelessAutoReconnectBox != null)
                _wirelessAutoReconnectBox.Enabled = enabled;
            if (_wirelessPrepareButton != null)
                _wirelessPrepareButton.Enabled = enabled &&
                    option.HasAuthorizedUsb;
            if (_wirelessConnectButton != null)
                _wirelessConnectButton.Enabled = enabled;
            if (_wirelessDisconnectButton != null)
                _wirelessDisconnectButton.Enabled = enabled;
            if (_pairingPortBox != null)
                _pairingPortBox.Enabled = enabled;
            if (_pairingCodeBox != null)
                _pairingCodeBox.Enabled = enabled;
            if (_pairButton != null)
                _pairButton.Enabled = enabled;
        }

        private async void WirelessPrepareButton_Click(
            object sender,
            EventArgs e)
        {
            SaveLoadedWirelessProfileToMemory();
            var option = GetSelectedWirelessDeviceOption();
            if (option == null) return;
            var host = _wirelessHostBox.Text;
            var port = (int)_wirelessPortBox.Value;
            var autoReconnect = _wirelessAutoReconnectBox.Checked;
            await RunWirelessOperationAsync(delegate
            {
                return _wirelessAdbService.EnableFromUsbForDevice(
                    option.DeviceIdentity,
                    option.UsbSerial,
                    host,
                    port,
                    autoReconnect);
            });
            if (IsDisposed) return;
            LoadSelectedWirelessProfile(false);
        }

        private async void WirelessConnectButton_Click(
            object sender,
            EventArgs e)
        {
            SaveLoadedWirelessProfileToMemory();
            var option = GetSelectedWirelessDeviceOption();
            if (option == null) return;
            var host = _wirelessHostBox.Text;
            var port = (int)_wirelessPortBox.Value;
            var autoReconnect = _wirelessAutoReconnectBox.Checked;
            await RunWirelessOperationAsync(delegate
            {
                return _wirelessAdbService.ConnectForDevice(
                    option.DeviceIdentity,
                    host,
                    port,
                    autoReconnect);
            });
            if (IsDisposed) return;
            LoadSelectedWirelessProfile(false);
        }

        private async void WirelessDisconnectButton_Click(
            object sender,
            EventArgs e)
        {
            SaveLoadedWirelessProfileToMemory();
            var option = GetSelectedWirelessDeviceOption();
            if (option == null) return;
            await RunWirelessOperationAsync(delegate
            {
                return _wirelessAdbService.DisconnectForDevice(
                    option.DeviceIdentity);
            });
            if (IsDisposed) return;
            LoadSelectedWirelessProfile(false);
        }

        private async void PairButton_Click(
            object sender,
            EventArgs e)
        {
            SaveLoadedWirelessProfileToMemory();
            var option = GetSelectedWirelessDeviceOption();
            if (option == null) return;
            var host = _wirelessHostBox.Text;
            var port = (int)_pairingPortBox.Value;
            var pairingCode = _pairingCodeBox.Text.Trim();
            var autoReconnect = _wirelessAutoReconnectBox.Checked;
            await RunWirelessOperationAsync(delegate
            {
                return _wirelessAdbService.Pair(
                    option.DeviceIdentity,
                    host,
                    port,
                    pairingCode,
                    autoReconnect);
            });
            if (IsDisposed) return;
            _pairingCodeBox.Clear();
            LoadSelectedWirelessProfile(false);
        }

        private async Task RunWirelessOperationAsync(
            Func<WirelessConnectionResult> operation)
        {
            SetWirelessButtonsEnabled(false);
            UseWaitCursor = true;
            try
            {
                var result = await Task.Run(operation);
                if (IsDisposed) return;
                _wirelessStatusLabel.Text = result.Message +
                    (string.IsNullOrWhiteSpace(result.Endpoint)
                        ? string.Empty
                        : " (" + result.Endpoint + ")");
                _wirelessStatusLabel.ForeColor = result.Success
                    ? Color.DarkGreen
                    : Color.Firebrick;
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                {
                    _wirelessStatusLabel.Text =
                        LocalizationService.Format(
                            "Settings.WirelessOperationFailed",
                            ex.Message);
                    _wirelessStatusLabel.ForeColor = Color.Firebrick;
                }
            }
            finally
            {
                if (!IsDisposed)
                {
                    UseWaitCursor = false;
                    UpdateWirelessControls();
                }
            }
        }

        private void SetWirelessButtonsEnabled(bool enabled)
        {
            _wirelessPrepareButton.Enabled = enabled;
            _wirelessConnectButton.Enabled = enabled;
            _wirelessDisconnectButton.Enabled = enabled;
            _pairButton.Enabled = enabled;
        }

        private void UpdateWirelessStatus()
        {
            var option = GetSelectedWirelessDeviceOption();
            if (option == null)
            {
                _wirelessStatusLabel.Text =
                    LocalizationService.Get("Settings.NoWirelessDevice");
                return;
            }

            var profile = _settings.FindDeviceWirelessConnection(
                option.DeviceIdentity);
            var expectedEndpoint = profile == null
                ? string.Empty
                : WirelessAdbService.BuildEndpoint(
                    profile.WirelessHost,
                    profile.WirelessPort);
            var wirelessTarget = option.FindAuthorizedWirelessSerial(
                expectedEndpoint);
            var usbTarget = option.UsbSerial;
            if (!string.IsNullOrWhiteSpace(usbTarget) &&
                !string.IsNullOrWhiteSpace(wirelessTarget))
            {
                _wirelessStatusLabel.Text = LocalizationService.Format(
                    "Settings.ObservedUsbWireless",
                    usbTarget,
                    wirelessTarget);
                return;
            }
            if (!string.IsNullOrWhiteSpace(usbTarget))
            {
                _wirelessStatusLabel.Text = LocalizationService.Format(
                    "Settings.ObservedUsb",
                    usbTarget);
                return;
            }
            if (!string.IsNullOrWhiteSpace(wirelessTarget))
            {
                _wirelessStatusLabel.Text = LocalizationService.Format(
                    "Settings.ObservedWireless",
                    wirelessTarget);
                return;
            }
            if (string.IsNullOrWhiteSpace(usbTarget) &&
                string.IsNullOrWhiteSpace(wirelessTarget))
            {
                _wirelessStatusLabel.Text = LocalizationService.Get(
                    "Settings.ObservedDisconnected");
                return;
            }
        }

        private void PopulateWirelessDevices()
        {
            _loadingWirelessDevice = true;
            try
            {
                _wirelessDeviceBox.Items.Clear();
                var snapshot = _getDeviceSnapshot();
                if (snapshot != null && snapshot.Devices != null)
                {
                    foreach (var device in snapshot.Devices)
                    {
                        if (device != null && device.IsConnected &&
                            !string.IsNullOrWhiteSpace(device.Identity))
                        {
                            _wirelessDeviceBox.Items.Add(
                                new WirelessDeviceOption(device));
                        }
                    }
                }

                var selectedIdentity = _getSelectedDeviceIdentity();
                var selectedIndex = -1;
                for (var index = 0;
                    index < _wirelessDeviceBox.Items.Count;
                    index++)
                {
                    var option = _wirelessDeviceBox.Items[index] as
                        WirelessDeviceOption;
                    if (option != null && string.Equals(
                        option.DeviceIdentity,
                        selectedIdentity,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        selectedIndex = index;
                        break;
                    }
                }
                if (selectedIndex < 0 &&
                    _wirelessDeviceBox.Items.Count > 0)
                {
                    selectedIndex = 0;
                }
                _wirelessDeviceBox.SelectedIndex = selectedIndex;
            }
            finally
            {
                _loadingWirelessDevice = false;
            }
            LoadSelectedWirelessProfile();
            if (_storeUiReady && !_loadingStoreValues) RefreshDisplayCleanupStatusAsync();
        }

        public void RefreshSelectedDeviceContext()
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                BeginInvoke((Action)RefreshSelectedDeviceContext);
                return;
            }

            SaveLoadedWirelessProfileToMemory();
            PopulateWirelessDevices();
            if ((_activePageIndex == 4 || _activePageIndex == 6) &&
                !_displayCleanupOperationRunning)
            {
                RefreshDeviceDiagnosticsAsync();
                RefreshDisplayCleanupStatusAsync();
            }
        }

        private void WirelessDeviceBox_SelectedIndexChanged(
            object sender,
            EventArgs e)
        {
            if (_loadingWirelessDevice) return;
            SaveLoadedWirelessProfileToMemory();
            LoadSelectedWirelessProfile();
            if (_storeUiReady && !_loadingStoreValues) RefreshDisplayCleanupStatusAsync();
        }

        private void LoadSelectedWirelessProfile()
        {
            LoadSelectedWirelessProfile(true);
        }

        private void LoadSelectedWirelessProfile(bool updateStatus)
        {
            var option = GetSelectedWirelessDeviceOption();
            _loadingWirelessDevice = true;
            try
            {
                if (option == null)
                {
                    _loadedWirelessDeviceIdentity = string.Empty;
                    _usbConnectionBox.Checked = true;
                    _wirelessHostBox.Text = string.Empty;
                    _wirelessPortBox.Value = Clamp(5555,
                        _wirelessPortBox);
                    _wirelessAutoReconnectBox.Checked = true;
                }
                else
                {
                    var profile = _wirelessAdbService.GetDeviceProfile(
                        option.DeviceIdentity,
                        ShouldSeedLegacyConnection(option));
                    _loadedWirelessDeviceIdentity = option.DeviceIdentity;
                    _usbConnectionBox.Checked =
                        profile.Mode == AdbConnectionMode.Usb;
                    _wirelessConnectionBox.Checked =
                        !_usbConnectionBox.Checked;
                    _wirelessHostBox.Text =
                        profile.WirelessHost ?? string.Empty;
                    _wirelessPortBox.Value = Clamp(
                        profile.WirelessPort,
                        _wirelessPortBox);
                    _wirelessAutoReconnectBox.Checked =
                        profile.AutoReconnect;
                }
                _pairingPortBox.Value = _wirelessPortBox.Value;
            }
            finally
            {
                _loadingWirelessDevice = false;
            }
            SynchronizeStoreSelectors();
            LoadDexDisplayMode();
            if (_storeUiReady && !_loadingStoreValues) SaveStoreScope(s => {}, false);
            if (updateStatus) UpdateWirelessStatus();
            UpdateWirelessControls();
        }

        private void SaveLoadedWirelessProfileToMemory()
        {
            if (_loadingWirelessDevice ||
                string.IsNullOrWhiteSpace(
                    _loadedWirelessDeviceIdentity))
            {
                return;
            }
            var identity = _loadedWirelessDeviceIdentity;
            var mode = _wirelessConnectionBox.Checked
                ? AdbConnectionMode.Wireless
                : AdbConnectionMode.Usb;
            var host = (_wirelessHostBox.Text ?? string.Empty).Trim();
            var port = (int)_wirelessPortBox.Value;
            var autoReconnect = _wirelessAutoReconnectBox.Checked;
            // Serialize the edited policy with discovery's settings snapshot/save.
            _settingsService.UpdateInMemory(_settings, delegate(AppSettings settings)
            {
                var profile = settings.GetOrCreateDeviceWirelessConnection(identity, false);
                profile.Mode = mode;
                profile.WirelessHost = host;
                profile.WirelessPort = port;
                profile.AutoReconnect = autoReconnect;
            });
        }

        private WirelessDeviceOption GetSelectedWirelessDeviceOption()
        {
            return _wirelessDeviceBox == null
                ? null
                : _wirelessDeviceBox.SelectedItem as
                    WirelessDeviceOption;
        }

        private bool ShouldSeedLegacyConnection(
            WirelessDeviceOption option)
        {
            if (option == null ||
                _settings.FindDeviceWirelessConnection(
                    option.DeviceIdentity) != null)
            {
                return false;
            }
            var legacy = _settings.Connection;
            if (legacy == null) return false;
            var endpoint = WirelessAdbService.BuildEndpoint(
                legacy.WirelessHost,
                legacy.WirelessPort);
            if (!string.IsNullOrWhiteSpace(endpoint) &&
                option.ContainsTransport(endpoint))
            {
                return true;
            }
            return (_settings.DeviceWirelessConnectionProfiles == null ||
                    _settings.DeviceWirelessConnectionProfiles.Count == 0) &&
                string.Equals(
                    option.DeviceIdentity,
                    _getSelectedDeviceIdentity(),
                    StringComparison.OrdinalIgnoreCase);
        }

        private sealed class WirelessDeviceOption
        {
            private readonly PhysicalDeviceInfo _device;

            public WirelessDeviceOption(PhysicalDeviceInfo device)
            {
                _device = device == null
                    ? new PhysicalDeviceInfo()
                    : device.Clone();
            }

            public string DeviceIdentity
            {
                get { return _device.Identity ?? string.Empty; }
            }

            public string UsbSerial
            {
                get
                {
                    if (_device.Transports == null) return string.Empty;
                    foreach (var transport in _device.Transports)
                    {
                        if (transport != null &&
                            transport.Kind == DeviceTransportKind.Usb &&
                            transport.IsAuthorized)
                        {
                            return transport.Serial ?? string.Empty;
                        }
                    }
                    return string.Empty;
                }
            }

            public bool HasAuthorizedUsb
            {
                get { return !string.IsNullOrWhiteSpace(UsbSerial); }
            }

            public bool ContainsTransport(string serial)
            {
                return _device.FindTransport(serial) != null;
            }

            public string FindAuthorizedWirelessSerial(
                string preferredEndpoint)
            {
                if (_device.Transports == null) return string.Empty;
                foreach (var transport in _device.Transports)
                {
                    if (transport != null &&
                        transport.Kind == DeviceTransportKind.Wireless &&
                        transport.IsAuthorized &&
                        !string.IsNullOrWhiteSpace(preferredEndpoint) &&
                        string.Equals(
                            transport.Serial,
                            preferredEndpoint,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return transport.Serial;
                    }
                }
                foreach (var transport in _device.Transports)
                {
                    if (transport != null &&
                        transport.Kind == DeviceTransportKind.Wireless &&
                        transport.IsAuthorized)
                    {
                        return transport.Serial ?? string.Empty;
                    }
                }
                return string.Empty;
            }

            public override string ToString()
            {
                return string.IsNullOrWhiteSpace(_device.DisplayName) ? LocalizationService.Get("Common.Unknown") : _device.DisplayName;
            }
        }

        private string GetAdbVersionText()
        {
            try
            {
                var result = _adbService.GetVersion();
                if (!result.IsSuccess)
                    return LocalizationService.Format(
                        "Settings.CheckFailed",
                        result.StandardError);
                return AdbVersionParser.GetDisplayVersion(
                    result.StandardOutput,
                    LocalizationService.Get(
                        "Settings.NoVersionOutput"));
            }
            catch (Exception ex)
            {
                return LocalizationService.Format(
                    "Settings.CheckFailed",
                    ex.Message);
            }
        }

        private string GetAdbDisplayName()
        {
            if (_settings.Paths.AdbSelectionMode == AdbSelectionMode.Manual)
                return LocalizationService.Get("Settings.AdbTypeManual");

            var selectedPath = NormalizePath(_adbService.AdbPath);
            if (PathsEqual(
                selectedPath,
                ResolveConfiguredPath(_settings.Paths.Win7AdbPath)))
            {
                return LocalizationService.Get("Settings.AdbTypeLegacy");
            }
            var scrcpyPath = ResolveConfiguredPath(
                _settings.Paths.ScrcpyPath);
            var scrcpyAdbPath = string.IsNullOrWhiteSpace(scrcpyPath)
                ? string.Empty
                : Path.Combine(
                    Path.GetDirectoryName(scrcpyPath) ?? string.Empty,
                    "adb.exe");
            if (PathsEqual(selectedPath, scrcpyAdbPath))
                return LocalizationService.Get("Settings.AdbTypeScrcpy");

            return LocalizationService.Get("Settings.AdbTypeExternal");
        }

        private string ResolveConfiguredPath(string configuredPath)
        {
            try
            {
                return NormalizePath(
                    _settingsService.ResolvePath(configuredPath));
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool PathsEqual(string left, string right)
        {
            return !string.IsNullOrWhiteSpace(left) &&
                !string.IsNullOrWhiteSpace(right) &&
                string.Equals(
                    NormalizePath(left),
                    NormalizePath(right),
                    StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            try
            {
                return Path.GetFullPath(path)
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return path.Trim();
            }
        }
    }
}
