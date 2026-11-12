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
        private void LoadValues()
        {
            _loadingStoreValues = true;
            try {
            foreach (var item in _languageBox.Items)
            {
                var option = item as LanguageOption;
                if (option != null && option.Value == _settings.Language)
                {
                    _languageBox.SelectedItem = option;
                    break;
                }
            }
            foreach (var item in _themeBox.Items)
            {
                var option = item as ThemeOption;
                if (option != null && option.Value == _settings.Theme)
                {
                    _themeBox.SelectedItem = option;
                    break;
                }
            }
            _automaticAdbBox.Checked =
                _settings.Paths.AdbSelectionMode == AdbSelectionMode.Auto;
            _manualAdbBox.Checked = !_automaticAdbBox.Checked;
            _manualAdbPathBox.Text = ResolveDisplayPath(
                _settings.Paths.AdbPath);
            _scrcpyPathBox.Text = ResolveDisplayPath(
                _settings.Paths.ScrcpyPath);
            _screenshotFolderBox.Text = ResolveDisplayPath(
                _settings.Paths.ScreenshotFolder);
            _deviceScreenshotFolderBox.Text = _settings.Paths.DeviceScreenshotFolder;
            _logFolderBox.Text = ResolveDisplayPath(
                _settings.Paths.LogFolder);

            _startWithWindowsBox.Checked = _settings.Features.StartWithWindows;
            _startMinimizedBox.Checked = _settings.Features.StartMinimizedToTray;
            _wakeUpModeBox.SelectedItem = _settings.Features.ScrcpyWakeUpMode;
            _autoHideBox.Checked = _settings.Features.AutoHideEnabled;
            _autoHideSecondsBox.Enabled = _autoHideBox.Checked;
            _autoStartDexBox.Checked = _settings.Features.AutoStartDexOnDeviceConnected;
            _showConnectedDeviceInfoBox.Checked =
                _settings.Features.ShowConnectedDeviceInfo;
            _miniControlBarBox.Checked =
                _settings.Features.MiniControlBarEnabled;
            foreach (var item in _miniControlBarSideBox.Items)
            {
                var option = item as MiniControlBarSideOption;
                if (option != null &&
                    option.Value ==
                        _settings.Features.MiniControlBarSide)
                {
                    _miniControlBarSideBox.SelectedItem = option;
                    break;
                }
            }
            _miniControlBarSideBox.Enabled =
                _miniControlBarBox.Checked;
            _resetDisplayOnStopBox.Checked = true;
            _disableStayAwakeBox.Checked = _settings.Features.DisableStayAwakeOnStop;
            _pushCaptureBox.Checked = _settings.Features.PushCaptureToDevice;
            _managedFileTransferBox.Checked =
                _settings.Features.ManagedFileTransferEnabled;
            _fileTransferTargetFolderBox.Text =
                _settings.Paths.FileTransferTargetFolder;
            _phoneToPcTransferBox.Checked =
                _settings.Features.PhoneToPcTransferEnabled;
            _phoneToPcReceiveFolderBox.Text = ResolveDisplayPath(
                _settings.Paths.PhoneToPcReceiveFolder);

            _deviceMonitorIntervalBox.Value = MillisecondsToSeconds(
                _settings.Timing.DeviceMonitorIntervalMs,
                _deviceMonitorIntervalBox);
            _disconnectMonitorIntervalBox.Value = MillisecondsToSeconds(
                _settings.Timing.DisconnectMonitorIntervalMs,
                _disconnectMonitorIntervalBox);
            _connectedStartDelayBox.Value = MillisecondsToSeconds(
                _settings.Timing.ConnectedStartDelayMs,
                _connectedStartDelayBox);
            _adbWakeUpDelayBox.Value = MillisecondsToSeconds(
                _settings.Timing.AdbWakeUpDelayMs,
                _adbWakeUpDelayBox);
            _autoHideSecondsBox.Value = Clamp(_settings.Timing.AutoHideIdleSeconds, _autoHideSecondsBox);
            _captureWaitSecondsBox.Value = Clamp(_settings.Timing.CaptureWaitSeconds, _captureWaitSecondsBox);
            _processTimeoutBox.Value = MillisecondsToSeconds(
                _settings.Timing.ProcessTimeoutMs,
                _processTimeoutBox);
            _virtualDisplayTimeoutBox.Value = MillisecondsToSeconds(
                _settings.Timing.VirtualDisplayDetectionTimeoutMs,
                _virtualDisplayTimeoutBox);

            _captureHotkeyBox.Text = _settings.KeyMappings.CaptureHotkey;
            _exitHotkeyBox.Text = _settings.KeyMappings.ExitHotkey;
            _lowLevelHotkeyBox.Checked = _settings.KeyMappings.UseLowLevelHotkeys;
            _keyboardDiagnosticsBox.Checked = _settings.KeyMappings.LogKeyboardDiagnostics;
            _keyInputModeBox.SelectedItem = _settings.KeyMappings.KoreanEnglishInputMode;
            if (_enterInputModeBox != null) _enterInputModeBox.SelectedItem = _settings.KeyMappings.EnterInputMode;
            _convertHangulBox.Checked = _settings.KeyMappings.ConvertKoreanEnglishKey;
            _rightWindowsBox.Checked = _settings.KeyMappings.HandleRightWindowsKey;
            _convertEnterBox.Checked = _settings.KeyMappings.ConvertEnterToShiftEnter;
            if (_autoAppEnterBox != null) _autoAppEnterBox.Checked = _settings.KeyMappings.AutoAppEnterEnabled;
            _ignoreShiftSpaceBox.Checked = _settings.KeyMappings.IgnoreShiftSpace;
            PopulateWirelessDevices();
            UpdateManualAdbControls();
            UpdateWirelessControls();
            } finally { _loadingStoreValues = false; }
            if (_storeUiReady) RestoreStoreExpansion();
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            var previousTheme = _settings.Theme;
            try
            {
                _settingsService.UpdateAndSave(_settings, SaveValues);
            }
            catch (Exception ex)
            {
                ShowSaveStatus(
                    LocalizationService.Format(
                        "Settings.SaveFailedInline",
                        ex.Message),
                    Color.Firebrick,
                    5000);
                return;
            }

            try
            {
                if (previousTheme != _settings.Theme &&
                    _applyTheme != null)
                {
                    _applyTheme(_settings.Theme);
                }
                if (_settingsChanged != null) _settingsChanged(false);
                ShowSaveStatus(
                    LocalizationService.Get("Settings.SavedInline"),
                    Color.DarkGreen,
                    2800);
            }
            catch (Exception ex)
            {
                ShowSaveStatus(
                    LocalizationService.Format(
                        "Settings.SavedApplyFailedInline",
                        ex.Message),
                    Color.Firebrick,
                    5000);
            }
        }

        private void ResetDefaultsButton_Click(
            object sender,
            EventArgs e)
        {
            var result = DesktopDialogs.Show(
                this,
                LocalizationService.Get(
                    "Settings.ResetDefaultsConfirm"),
                LocalizationService.Get("App.Name"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (result != DialogResult.Yes) return;

            var previousTheme = _settings.Theme;
            try
            {
                var defaults = AppSettings.CreateDefault();
                _settingsService.SaveAndApply(_settings, defaults);
            }
            catch (Exception ex)
            {
                ShowSaveStatus(
                    LocalizationService.Format(
                        "Settings.SaveFailedInline",
                        ex.Message),
                    Color.Firebrick,
                    5000);
                return;
            }

            try
            {
                LoadValues();
                if (previousTheme != _settings.Theme &&
                    _applyTheme != null)
                    _applyTheme(_settings.Theme);
                if (_settingsChanged != null) _settingsChanged(true);
                ShowSaveStatus(
                    LocalizationService.Get(
                        "Settings.ResetDefaultsDone"),
                    Color.DarkGreen,
                    5000);
            }
            catch (Exception ex)
            {
                ShowSaveStatus(
                    LocalizationService.Format(
                        "Settings.SavedApplyFailedInline",
                        ex.Message),
                    Color.Firebrick,
                    5000);
            }
        }

        private void ShowSaveStatus(
            string message,
            Color color,
            int durationMs)
        {
            _saveStatusTimer.Stop();
            _saveStatusLabel.ForeColor = color;
            _saveStatusLabel.Text = message;
            _saveStatusLabel.Visible = true;
            _saveStatusTimer.Interval = Math.Max(durationMs, 500);
            _saveStatusTimer.Start();
        }

        private void SaveValues(AppSettings settings)
        {
            var language = _languageBox.SelectedItem as LanguageOption;
            settings.Language = language == null
                ? AppLanguage.Auto
                : language.Value;
            var theme = _themeBox.SelectedItem as ThemeOption;
            settings.Theme = theme == null
                ? AppTheme.Auto
                : theme.Value;
            var manualAdbPath = ToConfiguredPath(
                _manualAdbPathBox.Text);
            var scrcpyPath = ToConfiguredPath(
                _scrcpyPathBox.Text);
            if (_manualAdbBox.Checked &&
                string.IsNullOrWhiteSpace(manualAdbPath))
            {
                throw new InvalidOperationException(
                    LocalizationService.Get(
                        "Settings.ManualAdbPathRequired"));
            }
            if (string.IsNullOrWhiteSpace(scrcpyPath))
            {
                throw new InvalidOperationException(
                    LocalizationService.Get(
                        "Settings.ScrcpyPathRequired"));
            }
            if (_manualAdbBox.Checked)
                EnsureExecutableExists(manualAdbPath);
            EnsureExecutableExists(scrcpyPath);
            settings.Paths.AdbSelectionMode = _manualAdbBox.Checked
                ? AdbSelectionMode.Manual
                : AdbSelectionMode.Auto;
            settings.Paths.AdbPath = manualAdbPath;
            settings.Paths.ScrcpyPath = scrcpyPath;
            settings.Paths.ScreenshotFolder = ToConfiguredPath(
                _screenshotFolderBox.Text);
            settings.Paths.DeviceScreenshotFolder = NormalizeDeviceFolder(
                _deviceScreenshotFolderBox.Text,
                false);
            settings.Paths.FileTransferTargetFolder =
                NormalizeFileTransferTargetFolder(
                    _fileTransferTargetFolderBox.Text);
            settings.Paths.PhoneToPcReceiveFolder = ToConfiguredPath(
                _phoneToPcReceiveFolderBox.Text);
            if (string.IsNullOrWhiteSpace(
                settings.Paths.PhoneToPcReceiveFolder))
            {
                throw new InvalidOperationException(
                    LocalizationService.Get(
                        "Settings.PhoneToPcReceiveFolderRequired"));
            }
            settings.Paths.LogFolder = ToConfiguredPath(
                _logFolderBox.Text);

            settings.Features.StartWithWindows = _startWithWindowsBox.Checked;
            settings.Features.StartMinimizedToTray = _startMinimizedBox.Checked;
            settings.Features.ScrcpyWakeUpMode = (ScrcpyWakeUpMode)_wakeUpModeBox.SelectedItem;
            settings.Features.AutoHideEnabled = _autoHideBox.Checked;
            settings.Features.AutoStartDexOnDeviceConnected = _autoStartDexBox.Checked;
            settings.Features.ShowConnectedDeviceInfo =
                _showConnectedDeviceInfoBox.Checked;
            settings.Features.ResetVirtualDisplayOnStop = true;
            settings.Features.DisableStayAwakeOnStop = _disableStayAwakeBox.Checked;
            settings.Features.PushCaptureToDevice = _pushCaptureBox.Checked;
            settings.Features.ManagedFileTransferEnabled =
                _managedFileTransferBox.Checked;
            settings.Features.PhoneToPcTransferEnabled =
                _phoneToPcTransferBox.Checked;
            settings.Features.MiniControlBarEnabled =
                _miniControlBarBox.Checked;
            var miniControlBarSide =
                _miniControlBarSideBox.SelectedItem as
                    MiniControlBarSideOption;
            settings.Features.MiniControlBarSide =
                miniControlBarSide == null
                    ? MiniControlBarSide.Right
                    : miniControlBarSide.Value;

            settings.Timing.DeviceMonitorIntervalMs =
                SecondsToMilliseconds(_deviceMonitorIntervalBox);
            settings.Timing.DisconnectMonitorIntervalMs =
                SecondsToMilliseconds(_disconnectMonitorIntervalBox);
            settings.Timing.ConnectedStartDelayMs =
                SecondsToMilliseconds(_connectedStartDelayBox);
            settings.Timing.AdbWakeUpDelayMs =
                SecondsToMilliseconds(_adbWakeUpDelayBox);
            settings.Timing.AutoHideIdleSeconds = (int)_autoHideSecondsBox.Value;
            settings.Timing.CaptureWaitSeconds = (int)_captureWaitSecondsBox.Value;
            settings.Timing.ProcessTimeoutMs =
                SecondsToMilliseconds(_processTimeoutBox);
            settings.Timing.VirtualDisplayDetectionTimeoutMs =
                SecondsToMilliseconds(_virtualDisplayTimeoutBox);

            settings.KeyMappings.CaptureHotkey = _captureHotkeyBox.Text.Trim();
            settings.KeyMappings.ExitHotkey = _exitHotkeyBox.Text.Trim();
            if (!HotkeyService.IsValidShortcut(
                    settings.KeyMappings.CaptureHotkey) ||
                !HotkeyService.IsValidShortcut(
                    settings.KeyMappings.ExitHotkey))
            {
                throw new InvalidOperationException(
                    LocalizationService.Get(
                        "Settings.InvalidHotkey"));
            }
            if (HotkeyService.ShortcutsConflict(
                settings.KeyMappings.CaptureHotkey,
                settings.KeyMappings.ExitHotkey,
                _lowLevelHotkeyBox.Checked))
            {
                throw new InvalidOperationException(
                    LocalizationService.Get(
                        "Settings.HotkeysMustDiffer"));
            }
            settings.KeyMappings.UseLowLevelHotkeys = _lowLevelHotkeyBox.Checked;
            settings.KeyMappings.LogKeyboardDiagnostics = _keyboardDiagnosticsBox.Checked;
            var keyInputMode = ReadStoreInputMode(_keyInputModeBox);
            settings.KeyMappings.KoreanEnglishInputMode = keyInputMode;
            settings.KeyMappings.EnterInputMode = _enterInputModeBox == null ? keyInputMode : ReadStoreInputMode(_enterInputModeBox);
            settings.KeyMappings.ConvertKoreanEnglishKey = _convertHangulBox.Checked;
            settings.KeyMappings.HandleRightWindowsKey = _rightWindowsBox.Checked;
            settings.KeyMappings.ConvertEnterToShiftEnter = _convertEnterBox.Checked;
            if (_autoAppEnterBox != null) settings.KeyMappings.AutoAppEnterEnabled = _autoAppEnterBox.Checked;
            settings.KeyMappings.IgnoreShiftSpace = _ignoreShiftSpaceBox.Checked;
            SaveConnectionValues(settings);
        }

        private void SaveConnectionValues(AppSettings settings)
        {
            if (_wirelessConnectionBox.Checked &&
                string.IsNullOrWhiteSpace(_wirelessHostBox.Text))
            {
                throw new InvalidOperationException(
                    LocalizationService.Get(
                        "Settings.WirelessRequiresIp"));
            }
            SaveSelectedDeviceConnection(settings);
        }

        private string ResolveDisplayPath(string configuredPath)
        {
            try
            {
                return _settingsService.ResolvePath(configuredPath);
            }
            catch
            {
                return configuredPath ?? string.Empty;
            }
        }

        private string ToConfiguredPath(string displayedPath)
        {
            var value = (displayedPath ?? string.Empty).Trim();
            if (value.Length == 0) return string.Empty;

            try
            {
                var fullPath = _settingsService.ResolvePath(value);
                var basePath = Path.GetFullPath(
                    _settingsService.BaseDirectory)
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar) +
                    Path.DirectorySeparatorChar;
                if (fullPath.StartsWith(
                    basePath,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return fullPath.Substring(basePath.Length);
                }
                return fullPath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    LocalizationService.Format(
                        "Settings.InvalidPath",
                        value),
                    ex);
            }
        }

        private void SaveSelectedDeviceConnection(AppSettings settings)
        {
            var option = GetSelectedWirelessDeviceOption();
            if (option == null ||
                string.IsNullOrWhiteSpace(option.DeviceIdentity))
            {
                return;
            }
            var host = _wirelessHostBox.Text.Trim();
            var port = (int)_wirelessPortBox.Value;
            if (!string.IsNullOrWhiteSpace(host))
                WirelessAdbService.BuildEndpoint(host, port);
            var profile = settings.GetOrCreateDeviceWirelessConnection(
                option.DeviceIdentity,
                ShouldSeedLegacyConnection(option));
            profile.Mode = _wirelessConnectionBox.Checked
                ? AdbConnectionMode.Wireless
                : AdbConnectionMode.Usb;
            profile.WirelessHost = host;
            profile.WirelessPort = port;
            profile.AutoReconnect =
                _wirelessAutoReconnectBox.Checked;
        }

        private void EnsureExecutableExists(string configuredPath)
        {
            var fullPath = _settingsService.ResolvePath(configuredPath);
            if (File.Exists(fullPath)) return;
            throw new InvalidOperationException(LocalizationService.Format(
                "Settings.ExecutableNotFound",
                fullPath));
        }

        private void UpdateManualAdbControls()
        {
            if (_manualAdbPanel != null)
                _manualAdbPanel.Enabled = _manualAdbBox != null && _manualAdbBox.Checked;
        }

        private static string NormalizeFileTransferTargetFolder(string value)
        {
            return NormalizeDeviceFolder(value, true);
        }

        private static string NormalizeDeviceFolder(
            string value,
            bool trailingSlash)
        {
            var normalized = (value ?? string.Empty)
                .Trim()
                .Replace('\\', '/');
            while (normalized.Contains("//"))
                normalized = normalized.Replace("//", "/");
            normalized = normalized.TrimEnd('/');

            var validRoot = string.Equals(
                    normalized,
                    "/sdcard",
                    StringComparison.Ordinal) ||
                normalized.StartsWith(
                    "/sdcard/",
                    StringComparison.Ordinal) ||
                string.Equals(
                    normalized,
                    "/storage/emulated/0",
                    StringComparison.Ordinal) ||
                normalized.StartsWith(
                    "/storage/emulated/0/",
                    StringComparison.Ordinal);
            var components = normalized.Split('/');
            var validComponents = true;
            foreach (var component in components)
            {
                if (string.Equals(component, ".", StringComparison.Ordinal) ||
                    string.Equals(component, "..", StringComparison.Ordinal) ||
                    Encoding.UTF8.GetByteCount(component) > 255)
                {
                    validComponents = false;
                    break;
                }
            }
            var containsControlCharacter = false;
            foreach (var character in normalized)
            {
                if (!char.IsControl(character)) continue;
                containsControlCharacter = true;
                break;
            }
            if (!validRoot || !validComponents ||
                containsControlCharacter ||
                normalized.IndexOf('"') >= 0)
            {
                throw new InvalidOperationException(
                    LocalizationService.Get(
                        "Settings.FileTransferTargetFolderInvalid"));
            }
            return trailingSlash
                ? normalized + "/"
                : normalized;
        }
        private sealed class LanguageOption
        {
            public LanguageOption(AppLanguage value)
            {
                Value = value;
            }

            public AppLanguage Value { get; private set; }

            public override string ToString()
            {
                return LocalizationService.GetLanguageName(Value);
            }
        }

        private sealed class ThemeOption
        {
            public ThemeOption(AppTheme value)
            {
                Value = value;
            }

            public AppTheme Value { get; private set; }

            public override string ToString()
            {
                if (Value == AppTheme.Light)
                    return LocalizationService.Get("Settings.ThemeLight");
                if (Value == AppTheme.Dark)
                    return LocalizationService.Get("Settings.ThemeDark");
                return LocalizationService.Get("Settings.ThemeAuto");
            }
        }

        private sealed class MiniControlBarSideOption
        {
            public MiniControlBarSideOption(MiniControlBarSide value)
            {
                Value = value;
            }

            public MiniControlBarSide Value { get; private set; }

            public override string ToString()
            {
                return LocalizationService.Get(
                    Value == MiniControlBarSide.Left
                        ? "Settings.MiniControlBarLeft"
                        : "Settings.MiniControlBarRight");
            }
        }
    }
}
