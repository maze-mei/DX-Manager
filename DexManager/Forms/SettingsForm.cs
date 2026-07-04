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
    public sealed class SettingsForm : Form
    {
        private readonly SettingsService _settingsService;
        private readonly AppSettings _settings;
        private readonly AdbService _adbService;
        private readonly WirelessAdbService _wirelessAdbService;
        private readonly Action _showLogs;
        private readonly Action _showEnvironmentCheck;
        private readonly ThemePalette _theme;
        private readonly List<Control> _pages = new List<Control>();
        private readonly List<ThemedButton> _navigationButtons =
            new List<ThemedButton>();
        private Panel _contentHost;

        private RadioButton _automaticAdbBox;
        private RadioButton _manualAdbBox;
        private TextBox _manualAdbPathBox;
        private Panel _manualAdbPanel;
        private TextBox _scrcpyPathBox;
        private TextBox _screenshotFolderBox;
        private TextBox _deviceScreenshotFolderBox;
        private TextBox _logFolderBox;
        private CheckBox _startWithWindowsBox;
        private CheckBox _startMinimizedBox;
        private ThemedSelectControl _wakeUpModeBox;
        private CheckBox _autoHideBox;
        private CheckBox _pushCaptureBox;
        private CheckBox _resetDisplayOnStopBox;
        private CheckBox _disableStayAwakeBox;
        private CheckBox _autoStartDexBox;
        private ThemedNumberControl _deviceMonitorIntervalBox;
        private ThemedNumberControl _disconnectMonitorIntervalBox;
        private ThemedNumberControl _connectedStartDelayBox;
        private ThemedNumberControl _adbWakeUpDelayBox;
        private ThemedNumberControl _autoHideSecondsBox;
        private ThemedNumberControl _captureWaitSecondsBox;
        private ThemedNumberControl _processTimeoutBox;
        private TextBox _captureHotkeyBox;
        private TextBox _exitHotkeyBox;
        private CheckBox _lowLevelHotkeyBox;
        private CheckBox _keyboardDiagnosticsBox;
        private CheckBox _convertHangulBox;
        private ThemedSelectControl _hangulInputModeBox;
        private CheckBox _rightWindowsBox;
        private CheckBox _convertEnterBox;
        private ThemedSelectControl _enterInputModeBox;
        private CheckBox _ignoreShiftSpaceBox;
        private RadioButton _usbConnectionBox;
        private RadioButton _wirelessConnectionBox;
        private TextBox _wirelessHostBox;
        private ThemedNumberControl _wirelessPortBox;
        private CheckBox _wirelessAutoReconnectBox;
        private Label _wirelessStatusLabel;
        private ThemedNumberControl _pairingPortBox;
        private TextBox _pairingCodeBox;
        private Button _wirelessPrepareButton;
        private Button _wirelessConnectButton;
        private Button _wirelessDisconnectButton;
        private Button _pairButton;
        private ThemedSelectControl _languageBox;
        private ThemedSelectControl _themeBox;
        private Label _saveStatusLabel;
        private Timer _saveStatusTimer;

        public SettingsForm(
            SettingsService settingsService,
            AppSettings settings,
            AdbService adbService,
            WirelessAdbService wirelessAdbService,
            Action showLogs,
            Action showEnvironmentCheck)
        {
            _settingsService = settingsService;
            _settings = settings;
            _adbService = adbService;
            _wirelessAdbService = wirelessAdbService;
            _showLogs = showLogs;
            _showEnvironmentCheck = showEnvironmentCheck;
            _theme = ThemeColors.Current;

            Text = LocalizationService.Get("Settings.Title");
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(940, 700);
            MinimumSize = new Size(860, 640);
            Font = new Font("Segoe UI", 9F);
            BackColor = _theme.WindowBackground;

            var title = new Label
            {
                AutoSize = false,
                Font = new Font("Segoe UI", 20F, FontStyle.Bold),
                ForeColor = _theme.TextPrimary,
                Location = new Point(224, 22),
                Size = new Size(690, 40),
                Text = LocalizationService.Get("Main.Settings")
            };
            var description = new Label
            {
                AutoEllipsis = true,
                ForeColor = _theme.TextTertiary,
                Location = new Point(226, 62),
                Size = new Size(690, 34),
                Text = LocalizationService.Get("Settings.Description")
            };

            _contentHost = new Panel
            {
                Location = new Point(220, 104),
                Size = new Size(704, 514),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom |
                    AnchorStyles.Left | AnchorStyles.Right,
                BackColor = _theme.WindowBackground
            };
            _pages.Add(BuildGeneralPage());
            _pages.Add(BuildConnectionPage());
            _pages.Add(BuildPathPage());
            _pages.Add(BuildKeyboardPage());
            _pages.Add(BuildTimingPage());
            _pages.Add(BuildDiagnosticsPage());
            foreach (var page in _pages)
            {
                page.Dock = DockStyle.Fill;
                page.Visible = false;
                _contentHost.Controls.Add(page);
            }

            var bottomPanel = new Panel
            {
                Location = new Point(220, 624),
                Size = new Size(704, 62),
                Anchor = AnchorStyles.Left | AnchorStyles.Right |
                    AnchorStyles.Bottom,
                BackColor = _theme.WindowBackground
            };
            _saveStatusLabel = new Label
            {
                ForeColor = _theme.TextTertiary,
                Location = new Point(0, 2),
                Size = new Size(470, 24),
                TextAlign = ContentAlignment.MiddleRight,
                AutoEllipsis = true,
                Visible = false
            };
            var saveButton = new ThemedButton
            {
                Primary = true,
                Text = LocalizationService.Get("Common.Save"),
                Location = new Point(492, 14),
                Size = new Size(100, 36),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            saveButton.Click += SaveButton_Click;
            var closeButton = new ThemedButton
            {
                Text = LocalizationService.Get("Common.Close"),
                Location = new Point(602, 14),
                Size = new Size(100, 36),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            closeButton.Click += delegate { Close(); };
            bottomPanel.Controls.Add(_saveStatusLabel);
            bottomPanel.Controls.Add(saveButton);
            bottomPanel.Controls.Add(closeButton);
            _saveStatusTimer = new Timer { Interval = 2800 };
            _saveStatusTimer.Tick += delegate
            {
                _saveStatusTimer.Stop();
                _saveStatusLabel.Visible = false;
            };
            FormClosed += delegate { _saveStatusTimer.Dispose(); };

            Controls.Add(BuildSidebar());
            Controls.Add(_contentHost);
            Controls.Add(title);
            Controls.Add(description);
            Controls.Add(bottomPanel);
            LoadValues();
            ShowPage(0);
        }

        private Control BuildGeneralPage()
        {
            var page = CreatePage();
            var appearance = CreateTable();
            _languageBox = CreateSelect();
            foreach (AppLanguage language in Enum.GetValues(typeof(AppLanguage)))
                _languageBox.Items.Add(new LanguageOption(language));
            AddRow(appearance, LocalizationService.Get("Settings.Language"), _languageBox);
            AddRow(
                appearance,
                string.Empty,
                CreateHint(LocalizationService.Get("Settings.LanguageRestart")));
            _themeBox = CreateSelect();
            foreach (AppTheme theme in Enum.GetValues(typeof(AppTheme)))
                _themeBox.Items.Add(new ThemeOption(theme));
            AddRow(appearance, LocalizationService.Get("Settings.Theme"), _themeBox);
            AddRow(
                appearance,
                string.Empty,
                CreateHint(LocalizationService.Get("Settings.ThemeRestart")));
            AddCard(page, LocalizationService.Get("Settings.GroupAppearance"), appearance);

            var startup = CreateTable();
            _startWithWindowsBox = AddCheck(startup, LocalizationService.Get("Settings.StartWithWindows"));
            _startMinimizedBox = AddCheck(startup, LocalizationService.Get("Settings.StartMinimized"));
            _wakeUpModeBox = AddCombo<ScrcpyWakeUpMode>(startup, LocalizationService.Get("Settings.WakeUpMode"));
            _autoHideBox = AddCheck(startup, LocalizationService.Get("Settings.AutoHide"));
            _autoStartDexBox = AddCheck(startup, LocalizationService.Get("Settings.AutoStartDex"));
            AddCard(page, LocalizationService.Get("Settings.GroupStartup"), startup);

            var shutdown = CreateTable();
            _resetDisplayOnStopBox = AddCheck(shutdown, LocalizationService.Get("Settings.ResetDisplay"));
            _disableStayAwakeBox = AddCheck(shutdown, LocalizationService.Get("Settings.DisableStayAwake"));
            AddCard(page, LocalizationService.Get("Settings.GroupShutdown"), shutdown);
            return page;
        }

        private Control BuildPathPage()
        {
            var page = CreatePage();
            var adbTable = CreateTable();

            _automaticAdbBox = CreateRadio(
                LocalizationService.Get("Settings.AdbAuto"));
            _manualAdbBox = CreateRadio(
                LocalizationService.Get("Settings.AdbManual"));
            _automaticAdbBox.CheckedChanged += delegate { UpdateManualAdbControls(); };
            _manualAdbBox.CheckedChanged += delegate { UpdateManualAdbControls(); };
            AddRow(adbTable, LocalizationService.Get("Settings.AdbMode"), _automaticAdbBox);
            AddRow(adbTable, string.Empty, _manualAdbBox);
            AddReadOnly(adbTable, LocalizationService.Get("Settings.CurrentOs"), WindowsVersionHelper.GetDisplayName());
            AddReadOnly(adbTable, LocalizationService.Get("Settings.CurrentAdb"), _adbService.AdbPath);
            AddReadOnly(adbTable, LocalizationService.Get("Settings.AdbVersion"), GetAdbVersionText());

            _manualAdbPanel = CreatePathPanel(out _manualAdbPathBox, true);
            AddRow(adbTable, LocalizationService.Get("Settings.ManualAdbPath"), _manualAdbPanel);
            AddCard(page, LocalizationService.Get("Settings.GroupAdb"), adbTable);

            var paths = CreateTable();
            _scrcpyPathBox = AddPath(paths, LocalizationService.Get("Settings.ScrcpyPath"), true);
            _screenshotFolderBox = AddPath(paths, LocalizationService.Get("Settings.ScreenshotFolder"), false);
            _deviceScreenshotFolderBox = AddText(paths, LocalizationService.Get("Settings.DeviceFolder"));
            _logFolderBox = AddPath(paths, LocalizationService.Get("Settings.LogFolder"), false);
            AddCard(page, LocalizationService.Get("Settings.GroupStorage"), paths);
            return page;
        }

        private Control BuildConnectionPage()
        {
            var page = CreatePage();
            var connection = CreateTable();

            _usbConnectionBox = CreateRadio(
                LocalizationService.Get("Settings.Usb"));
            _wirelessConnectionBox = CreateRadio(
                LocalizationService.Get("Settings.Wireless"));
            _usbConnectionBox.CheckedChanged += delegate
            {
                UpdateWirelessControls();
            };
            _wirelessConnectionBox.CheckedChanged += delegate
            {
                UpdateWirelessControls();
            };
            AddRow(connection, LocalizationService.Get("Settings.ConnectionMode"), _usbConnectionBox);
            AddRow(connection, string.Empty, _wirelessConnectionBox);

            _wirelessHostBox = AddText(connection, LocalizationService.Get("Settings.PhoneIp"));
            _wirelessPortBox = AddNumber(
                connection,
                LocalizationService.Get("Settings.ConnectPort"),
                1,
                65535);
            _wirelessAutoReconnectBox = AddCheck(
                connection,
                LocalizationService.Get("Settings.AutoReconnect"));

            var connectionButtons = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            _wirelessPrepareButton = CreateActionButton(
                LocalizationService.Get("Settings.PrepareWireless"), 130);
            _wirelessPrepareButton.Click +=
                WirelessPrepareButton_Click;
            _wirelessConnectButton = CreateActionButton(
                LocalizationService.Get("Settings.ConnectWireless"), 130);
            _wirelessConnectButton.Click +=
                WirelessConnectButton_Click;
            _wirelessDisconnectButton = CreateActionButton(
                LocalizationService.Get("Settings.Disconnect"), 100);
            _wirelessDisconnectButton.Click +=
                WirelessDisconnectButton_Click;
            connectionButtons.Controls.Add(_wirelessPrepareButton);
            connectionButtons.Controls.Add(_wirelessConnectButton);
            connectionButtons.Controls.Add(_wirelessDisconnectButton);
            AddRow(connection, LocalizationService.Get("Settings.WirelessActions"), connectionButtons);

            _wirelessStatusLabel = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(570, 0)
            };
            AddRow(connection, LocalizationService.Get("Settings.CurrentStatus"), _wirelessStatusLabel);
            AddCard(page, LocalizationService.Get("Settings.GroupWireless"), connection);

            var pairing = CreateTable();
            AddRow(
                pairing,
                "Android 11+",
                new Label
                {
                    AutoSize = true,
                    MaximumSize = new Size(570, 0),
                    Text = LocalizationService.Get("Settings.PairGuide")
                });
            _pairingPortBox = AddNumber(
                pairing,
                LocalizationService.Get("Settings.PairingPort"),
                1,
                65535);
            _pairingCodeBox = AddText(pairing, LocalizationService.Get("Settings.PairingCode"));
            _pairingCodeBox.UseSystemPasswordChar = true;
            _pairButton = CreateActionButton(
                LocalizationService.Get("Settings.Pair"), 100);
            _pairButton.Click += PairButton_Click;
            AddRow(pairing, string.Empty, _pairButton);
            AddCard(page, LocalizationService.Get("Settings.GroupPairing"), pairing);
            return page;
        }

        private Control BuildKeyboardPage()
        {
            var page = CreatePage();
            var hotkeys = CreateTable();
            _captureHotkeyBox = AddText(hotkeys, LocalizationService.Get("Settings.CaptureHotkey"));
            _exitHotkeyBox = AddText(hotkeys, LocalizationService.Get("Settings.ExitHotkey"));
            _lowLevelHotkeyBox = AddCheck(hotkeys, LocalizationService.Get("Settings.LowLevelHotkey"));
            _keyboardDiagnosticsBox = AddCheck(hotkeys, LocalizationService.Get("Settings.KeyDiagnostics"));
            AddCard(page, LocalizationService.Get("Settings.GroupHotkeys"), hotkeys);

            var correction = CreateTable();
            _convertHangulBox = AddCheck(correction, LocalizationService.Get("Settings.HangulCorrection"));
            _hangulInputModeBox = AddCombo<KeyInputMode>(correction, LocalizationService.Get("Settings.HangulMode"));
            _rightWindowsBox = AddCheck(correction, LocalizationService.Get("Settings.RightWindows"));
            _convertEnterBox = AddCheck(correction, LocalizationService.Get("Settings.EnterConversion"));
            _enterInputModeBox = AddCombo<KeyInputMode>(correction, LocalizationService.Get("Settings.EnterMode"));
            _ignoreShiftSpaceBox = AddCheck(correction, LocalizationService.Get("Settings.IgnoreShiftSpace"));
            AddCard(page, LocalizationService.Get("Settings.GroupInput"), correction);
            return page;
        }

        private Control BuildTimingPage()
        {
            var page = CreatePage();
            var monitoring = CreateTable();
            _deviceMonitorIntervalBox = AddNumber(monitoring, LocalizationService.Get("Settings.DeviceInterval"), 200, 60000);
            _disconnectMonitorIntervalBox = AddNumber(monitoring, LocalizationService.Get("Settings.DisconnectInterval"), 200, 60000);
            _connectedStartDelayBox = AddNumber(monitoring, LocalizationService.Get("Settings.StartDelay"), 0, 60000);
            _adbWakeUpDelayBox = AddNumber(monitoring, LocalizationService.Get("Settings.WakeDelay"), 0, 60000);
            _processTimeoutBox = AddNumber(monitoring, LocalizationService.Get("Settings.ProcessTimeout"), 1000, 120000);
            AddCard(page, LocalizationService.Get("Settings.GroupMonitoring"), monitoring);

            var capture = CreateTable();
            _autoHideSecondsBox = AddNumber(capture, LocalizationService.Get("Settings.HideDelay"), 1, 3600);
            _captureWaitSecondsBox = AddNumber(capture, LocalizationService.Get("Settings.CaptureDelay"), 1, 60);
            _pushCaptureBox = AddCheck(capture, LocalizationService.Get("Settings.PushCapture"));
            AddCard(page, LocalizationService.Get("Settings.GroupCapture"), capture);
            return page;
        }

        private Control BuildDiagnosticsPage()
        {
            var page = CreatePage();
            var panel = new FlowLayoutPanel
            {
                AutoSize = true,
                Width = 620,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false
            };
            panel.Controls.Add(new Label
            {
                AutoSize = true,
                MaximumSize = new Size(700, 0),
                Margin = new Padding(0, 0, 0, 18),
                Text = LocalizationService.Get("Settings.DiagnosticsGuide")
            });
            var logButton = CreateActionButton(
                LocalizationService.Get("Settings.OpenLogs"), 220);
            logButton.Margin = new Padding(0, 0, 0, 10);
            logButton.Click += delegate
            {
                if (_showLogs != null) _showLogs();
            };
            var environmentButton = CreateActionButton(
                LocalizationService.Get("Settings.OpenEnvironment"), 220);
            environmentButton.Click += delegate
            {
                if (_showEnvironmentCheck != null)
                    _showEnvironmentCheck();
            };
            panel.Controls.Add(logButton);
            panel.Controls.Add(environmentButton);
            AddCard(page, LocalizationService.Get("Settings.Diagnostics"), panel);
            return page;
        }

        private void LoadValues()
        {
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
            _manualAdbPathBox.Text = _settings.Paths.AdbPath;
            _scrcpyPathBox.Text = _settings.Paths.ScrcpyPath;
            _screenshotFolderBox.Text = _settings.Paths.ScreenshotFolder;
            _deviceScreenshotFolderBox.Text = _settings.Paths.DeviceScreenshotFolder;
            _logFolderBox.Text = _settings.Paths.LogFolder;

            _startWithWindowsBox.Checked = _settings.Features.StartWithWindows;
            _startMinimizedBox.Checked = _settings.Features.StartMinimizedToTray;
            _wakeUpModeBox.SelectedItem = _settings.Features.ScrcpyWakeUpMode;
            _autoHideBox.Checked = _settings.Features.AutoHideEnabled;
            _autoStartDexBox.Checked = _settings.Features.AutoStartDexOnDeviceConnected;
            _resetDisplayOnStopBox.Checked = _settings.Features.ResetVirtualDisplayOnStop;
            _disableStayAwakeBox.Checked = _settings.Features.DisableStayAwakeOnStop;
            _pushCaptureBox.Checked = _settings.Features.PushCaptureToDevice;

            _deviceMonitorIntervalBox.Value = Clamp(_settings.Timing.DeviceMonitorIntervalMs, _deviceMonitorIntervalBox);
            _disconnectMonitorIntervalBox.Value = Clamp(_settings.Timing.DisconnectMonitorIntervalMs, _disconnectMonitorIntervalBox);
            _connectedStartDelayBox.Value = Clamp(_settings.Timing.ConnectedStartDelayMs, _connectedStartDelayBox);
            _adbWakeUpDelayBox.Value = Clamp(_settings.Timing.AdbWakeUpDelayMs, _adbWakeUpDelayBox);
            _autoHideSecondsBox.Value = Clamp(_settings.Timing.AutoHideIdleSeconds, _autoHideSecondsBox);
            _captureWaitSecondsBox.Value = Clamp(_settings.Timing.CaptureWaitSeconds, _captureWaitSecondsBox);
            _processTimeoutBox.Value = Clamp(_settings.Timing.ProcessTimeoutMs, _processTimeoutBox);

            _captureHotkeyBox.Text = _settings.KeyMappings.CaptureHotkey;
            _exitHotkeyBox.Text = _settings.KeyMappings.ExitHotkey;
            _lowLevelHotkeyBox.Checked = _settings.KeyMappings.UseLowLevelHotkeys;
            _keyboardDiagnosticsBox.Checked = _settings.KeyMappings.LogKeyboardDiagnostics;
            _convertHangulBox.Checked = _settings.KeyMappings.ConvertKoreanEnglishKey;
            _hangulInputModeBox.SelectedItem = _settings.KeyMappings.KoreanEnglishInputMode;
            _rightWindowsBox.Checked = _settings.KeyMappings.HandleRightWindowsKey;
            _convertEnterBox.Checked = _settings.KeyMappings.ConvertEnterToShiftEnter;
            _enterInputModeBox.SelectedItem = _settings.KeyMappings.EnterInputMode;
            _ignoreShiftSpaceBox.Checked = _settings.KeyMappings.IgnoreShiftSpace;
            _usbConnectionBox.Checked =
                _settings.Connection.Mode == AdbConnectionMode.Usb;
            _wirelessConnectionBox.Checked =
                !_usbConnectionBox.Checked;
            _wirelessHostBox.Text =
                _settings.Connection.WirelessHost ?? string.Empty;
            _wirelessPortBox.Value = Clamp(
                _settings.Connection.WirelessPort,
                _wirelessPortBox);
            _wirelessAutoReconnectBox.Checked =
                _settings.Connection.AutoReconnect;
            _pairingPortBox.Value = _wirelessPortBox.Value;
            UpdateWirelessStatus();
            UpdateManualAdbControls();
            UpdateWirelessControls();
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            try
            {
                SaveValues();
                _settingsService.Save(_settings);
                ShowSaveStatus(
                    LocalizationService.Get("Settings.SavedInline"),
                    Color.DarkGreen,
                    2800);
            }
            catch (Exception ex)
            {
                ShowSaveStatus(
                    LocalizationService.Format(
                        "Settings.SaveFailedInline",
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

        private void SaveValues()
        {
            var language = _languageBox.SelectedItem as LanguageOption;
            _settings.Language = language == null
                ? AppLanguage.Auto
                : language.Value;
            var theme = _themeBox.SelectedItem as ThemeOption;
            _settings.Theme = theme == null
                ? AppTheme.Auto
                : theme.Value;
            _settings.Paths.AdbSelectionMode = _manualAdbBox.Checked
                ? AdbSelectionMode.Manual
                : AdbSelectionMode.Auto;
            _settings.Paths.AdbPath = _manualAdbPathBox.Text.Trim();
            _settings.Paths.ScrcpyPath = _scrcpyPathBox.Text.Trim();
            _settings.Paths.ScreenshotFolder = _screenshotFolderBox.Text.Trim();
            _settings.Paths.DeviceScreenshotFolder = _deviceScreenshotFolderBox.Text.Trim();
            _settings.Paths.LogFolder = _logFolderBox.Text.Trim();

            _settings.Features.StartWithWindows = _startWithWindowsBox.Checked;
            _settings.Features.StartMinimizedToTray = _startMinimizedBox.Checked;
            _settings.Features.ScrcpyWakeUpMode = (ScrcpyWakeUpMode)_wakeUpModeBox.SelectedItem;
            _settings.Features.AutoHideEnabled = _autoHideBox.Checked;
            _settings.Features.AutoStartDexOnDeviceConnected = _autoStartDexBox.Checked;
            _settings.Features.ResetVirtualDisplayOnStop = _resetDisplayOnStopBox.Checked;
            _settings.Features.DisableStayAwakeOnStop = _disableStayAwakeBox.Checked;
            _settings.Features.PushCaptureToDevice = _pushCaptureBox.Checked;

            _settings.Timing.DeviceMonitorIntervalMs = (int)_deviceMonitorIntervalBox.Value;
            _settings.Timing.DisconnectMonitorIntervalMs = (int)_disconnectMonitorIntervalBox.Value;
            _settings.Timing.ConnectedStartDelayMs = (int)_connectedStartDelayBox.Value;
            _settings.Timing.AdbWakeUpDelayMs = (int)_adbWakeUpDelayBox.Value;
            _settings.Timing.AutoHideIdleSeconds = (int)_autoHideSecondsBox.Value;
            _settings.Timing.CaptureWaitSeconds = (int)_captureWaitSecondsBox.Value;
            _settings.Timing.ProcessTimeoutMs = (int)_processTimeoutBox.Value;

            _settings.KeyMappings.CaptureHotkey = _captureHotkeyBox.Text.Trim();
            _settings.KeyMappings.ExitHotkey = _exitHotkeyBox.Text.Trim();
            _settings.KeyMappings.UseLowLevelHotkeys = _lowLevelHotkeyBox.Checked;
            _settings.KeyMappings.LogKeyboardDiagnostics = _keyboardDiagnosticsBox.Checked;
            _settings.KeyMappings.ConvertKoreanEnglishKey = _convertHangulBox.Checked;
            _settings.KeyMappings.KoreanEnglishInputMode = (KeyInputMode)_hangulInputModeBox.SelectedItem;
            _settings.KeyMappings.HandleRightWindowsKey = _rightWindowsBox.Checked;
            _settings.KeyMappings.ConvertEnterToShiftEnter = _convertEnterBox.Checked;
            _settings.KeyMappings.EnterInputMode = (KeyInputMode)_enterInputModeBox.SelectedItem;
            _settings.KeyMappings.IgnoreShiftSpace = _ignoreShiftSpaceBox.Checked;
            SaveConnectionValues();
        }

        private void SaveConnectionValues()
        {
            if (_wirelessConnectionBox.Checked &&
                string.IsNullOrWhiteSpace(_wirelessHostBox.Text))
            {
                throw new InvalidOperationException(
                    LocalizationService.Get(
                        "Settings.WirelessRequiresIp"));
            }
            _settings.Connection.Mode = _wirelessConnectionBox.Checked
                ? AdbConnectionMode.Wireless
                : AdbConnectionMode.Usb;
            SaveConnectionDetails();
        }

        private void SaveConnectionDetails()
        {
            _settings.Connection.WirelessHost =
                _wirelessHostBox.Text.Trim();
            _settings.Connection.WirelessPort =
                (int)_wirelessPortBox.Value;
            _settings.Connection.AutoReconnect =
                _wirelessAutoReconnectBox.Checked;
        }

        private void UpdateManualAdbControls()
        {
            if (_manualAdbPanel != null)
                _manualAdbPanel.Enabled = _manualAdbBox != null && _manualAdbBox.Checked;
        }

        private void UpdateWirelessControls()
        {
            var enabled = _wirelessConnectionBox != null &&
                _wirelessConnectionBox.Checked;
            if (_wirelessHostBox != null)
                _wirelessHostBox.Enabled = enabled;
            if (_wirelessPortBox != null)
                _wirelessPortBox.Enabled = enabled;
            if (_wirelessAutoReconnectBox != null)
                _wirelessAutoReconnectBox.Enabled = enabled;
            if (_wirelessPrepareButton != null)
                _wirelessPrepareButton.Enabled = enabled;
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
            var host = _wirelessHostBox.Text;
            var port = (int)_wirelessPortBox.Value;
            await RunWirelessOperationAsync(delegate
            {
                return _wirelessAdbService.EnableFromUsb(
                    host,
                    port);
            });
            _wirelessHostBox.Text =
                _settings.Connection.WirelessHost ?? string.Empty;
        }

        private async void WirelessConnectButton_Click(
            object sender,
            EventArgs e)
        {
            var host = _wirelessHostBox.Text;
            var port = (int)_wirelessPortBox.Value;
            await RunWirelessOperationAsync(delegate
            {
                return _wirelessAdbService.Connect(
                    host,
                    port);
            });
        }

        private async void WirelessDisconnectButton_Click(
            object sender,
            EventArgs e)
        {
            await RunWirelessOperationAsync(delegate
            {
                return _wirelessAdbService.Disconnect();
            });
            _usbConnectionBox.Checked = true;
        }

        private async void PairButton_Click(
            object sender,
            EventArgs e)
        {
            var host = _wirelessHostBox.Text;
            var port = (int)_pairingPortBox.Value;
            var pairingCode = _pairingCodeBox.Text.Trim();
            await RunWirelessOperationAsync(delegate
            {
                return _wirelessAdbService.Pair(
                    host,
                    port,
                    pairingCode);
            });
            _pairingCodeBox.Clear();
        }

        private async Task RunWirelessOperationAsync(
            Func<WirelessConnectionResult> operation)
        {
            SaveConnectionDetails();
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
                if (result.Success)
                    _wirelessConnectionBox.Checked =
                        _wirelessAdbService.IsWirelessMode;
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
            var target = _adbService.TargetSerial;
            if (string.IsNullOrWhiteSpace(target))
            {
                _wirelessStatusLabel.Text =
                    _settings.Connection.Mode == AdbConnectionMode.Wireless
                        ? LocalizationService.Get(
                            "Settings.WirelessWaiting")
                        : LocalizationService.Get(
                            "Settings.UsbWaiting");
                return;
            }
            _wirelessStatusLabel.Text =
                LocalizationService.Format(
                    AdbService.IsTcpIpSerial(target)
                        ? "Settings.WirelessTarget"
                        : "Settings.UsbTarget",
                    target);
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
                var lines = result.StandardOutput.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                return lines.Length == 0
                    ? LocalizationService.Get(
                        "Settings.NoVersionOutput")
                    : lines[0];
            }
            catch (Exception ex)
            {
                return LocalizationService.Format(
                    "Settings.CheckFailed",
                    ex.Message);
            }
        }

        private Control BuildSidebar()
        {
            var sidebar = new RoundedPanel
            {
                Location = new Point(16, 16),
                Size = new Size(188, 668),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom |
                    AnchorStyles.Left,
                Radius = 14,
                BackColor = _theme.NavigationBackground,
                FillColor = _theme.NavigationBackground,
                BorderColor = _theme.CardBorder
            };
            sidebar.Controls.Add(new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = _theme.TextTertiary,
                BackColor = _theme.NavigationBackground,
                Location = new Point(20, 18),
                Text = LocalizationService.Get("Main.Settings")
            });

            var labels = new[]
            {
                LocalizationService.Get("Settings.General"),
                LocalizationService.Get("Settings.Connection"),
                LocalizationService.Get("Settings.Paths"),
                LocalizationService.Get("Settings.Keyboard"),
                LocalizationService.Get("Settings.Timing"),
                LocalizationService.Get("Settings.Diagnostics")
            };
            for (var index = 0; index < labels.Length; index++)
            {
                var pageIndex = index;
                var button = new ThemedButton
                {
                    Text = labels[index],
                    Primary = index == 0,
                    CornerRadius = 18,
                    NavigationStyle = true,
                    ShowNavigationDot = true,
                    TabStop = true,
                    Location = new Point(10, 52 + index * 42),
                    Size = new Size(168, 34),
                    BackColor = _theme.NavigationBackground,
                    ForeColor = _theme.TextSecondary
                };
                button.Click += delegate { ShowPage(pageIndex); };
                _navigationButtons.Add(button);
                sidebar.Controls.Add(button);
            }
            return sidebar;
        }

        private void ShowPage(int index)
        {
            if (index < 0 || index >= _pages.Count) return;
            for (var i = 0; i < _pages.Count; i++)
            {
                _pages[i].Visible = i == index;
                _navigationButtons[i].Primary = i == index;
                _navigationButtons[i].Invalidate();
            }
            _pages[index].BringToFront();
        }

        private Control CreatePage()
        {
            return new FlowLayoutPanel
            {
                AutoScroll = true,
                BackColor = _theme.WindowBackground,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(0, 0, 8, 16)
            };
        }

        private void AddCard(
            Control page,
            string title,
            Control content)
        {
            content.Location = new Point(18, 44);
            content.Width = 630;
            content.BackColor = _theme.CardBackground;
            var preferred = content.GetPreferredSize(
                new Size(630, 0));
            content.Height = Math.Max(preferred.Height, 32);

            var card = new RoundedPanel
            {
                Size = new Size(
                    670,
                    Math.Max(content.Height + 58, 94)),
                Margin = new Padding(0, 0, 0, 14),
                Radius = 14,
                BackColor = _theme.WindowBackground,
                FillColor = _theme.CardBackground,
                BorderColor = _theme.CardBorder
            };
            card.Controls.Add(new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = _theme.TextSecondary,
                BackColor = _theme.CardBackground,
                Location = new Point(20, 15),
                Text = title
            });
            card.Controls.Add(content);
            page.Controls.Add(card);
        }

        private static TableLayoutPanel CreateTable()
        {
            var table = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                Width = 630,
                BackColor = ThemeColors.Current.CardBackground,
                Padding = new Padding(0, 0, 0, 2)
            };
            table.ColumnStyles.Add(new ColumnStyle(
                SizeType.Absolute,
                205F));
            table.ColumnStyles.Add(new ColumnStyle(
                SizeType.Percent,
                100F));
            return table;
        }

        private static Label CreateHint(string text)
        {
            return new Label
            {
                AutoSize = true,
                MaximumSize = new Size(400, 0),
                ForeColor = ThemeColors.Current.TextTertiary,
                BackColor = ThemeColors.Current.CardBackground,
                Text = text
            };
        }

        private static TextBox AddText(TableLayoutPanel table, string label)
        {
            var box = CreateTextBox();
            AddRow(table, label, box);
            return box;
        }

        private static TextBox AddPath(TableLayoutPanel table, string label, bool file)
        {
            TextBox box;
            var panel = CreatePathPanel(out box, file);
            AddRow(table, label, panel);
            return box;
        }

        private static Panel CreatePathPanel(out TextBox box, bool file)
        {
            var textBox = CreateTextBox();
            box = textBox;
            var button = new ThemedButton
            {
                Text = LocalizationService.Get("Common.Browse"),
                Dock = DockStyle.Right,
                Width = 92
            };
            button.Click += delegate
            {
                if (file)
                {
                    using (var dialog = new OpenFileDialog
                    {
                        Filter = LocalizationService.Get(
                            "Settings.ExecutableFilter")
                    })
                    {
                        if (dialog.ShowDialog() == DialogResult.OK) textBox.Text = dialog.FileName;
                    }
                }
                else
                {
                    using (var dialog = new FolderBrowserDialog())
                    {
                        if (dialog.ShowDialog() == DialogResult.OK) textBox.Text = dialog.SelectedPath;
                    }
                }
            };
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 32,
                BackColor = ThemeColors.Current.CardBackground
            };
            panel.Controls.Add(textBox);
            panel.Controls.Add(button);
            return panel;
        }

        private static TextBox CreateTextBox()
        {
            return new TextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = ThemeColors.Current.CardSoft,
                ForeColor = ThemeColors.Current.TextPrimary,
                Font = new Font("Segoe UI", 9F)
            };
        }

        private static ThemedNumberControl AddNumber(
            TableLayoutPanel table,
            string label,
            int min,
            int max)
        {
            var box = new ThemedNumberControl
            {
                Minimum = min,
                Maximum = max,
                Increment = 1,
                ShowStepButtons = true,
                Dock = DockStyle.Left,
                Width = 150,
                Height = 32
            };
            box.Value = min;
            AddRow(table, label, box);
            return box;
        }

        private static CheckBox AddCheck(TableLayoutPanel table, string label)
        {
            var box = new ThemedCheckBox
            {
                Text = label,
                Dock = DockStyle.Fill,
                Height = 30,
                BackColor = ThemeColors.Current.CardBackground
            };
            var row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(box, 0, row);
            table.SetColumnSpan(box, 2);
            box.Margin = new Padding(3, 4, 3, 5);
            return box;
        }

        private static ThemedSelectControl AddCombo<T>(
            TableLayoutPanel table,
            string label)
        {
            var box = CreateSelect();
            foreach (var value in Enum.GetValues(typeof(T))) box.Items.Add(value);
            AddRow(table, label, box);
            return box;
        }

        private static ThemedSelectControl CreateSelect()
        {
            return new ThemedSelectControl
            {
                Dock = DockStyle.Left,
                Width = 240,
                Height = 32
            };
        }

        private static RadioButton CreateRadio(string text)
        {
            return new RadioButton
            {
                Text = text,
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = ThemeColors.Current.CardBackground,
                ForeColor = ThemeColors.Current.TextPrimary
            };
        }

        private static Button CreateActionButton(
            string text,
            int width)
        {
            return new ThemedButton
            {
                Text = text,
                Size = new Size(width, 34),
                Margin = new Padding(0, 0, 8, 0)
            };
        }

        private static void AddReadOnly(TableLayoutPanel table, string label, string value)
        {
            AddRow(table, label, new Label
            {
                AutoSize = true,
                MaximumSize = new Size(400, 0),
                ForeColor = ThemeColors.Current.TextSecondary,
                BackColor = ThemeColors.Current.CardBackground,
                Text = value
            });
        }

        private static void AddRow(TableLayoutPanel table, string label, Control control)
        {
            var row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(new Label
            {
                Text = label,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = ThemeColors.Current.TextTertiary,
                BackColor = ThemeColors.Current.CardBackground,
                Margin = new Padding(3, 9, 12, 9)
            }, 0, row);
            table.Controls.Add(control, 1, row);
            control.Margin = new Padding(3, 5, 3, 6);
        }

        private static decimal Clamp(int value, ThemedNumberControl box)
        {
            if (value < box.Minimum) return box.Minimum;
            if (value > box.Maximum) return box.Maximum;
            return value;
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
    }
}
