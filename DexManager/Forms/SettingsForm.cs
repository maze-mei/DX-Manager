using System;
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
        private ComboBox _wakeUpModeBox;
        private CheckBox _autoHideBox;
        private CheckBox _pushCaptureBox;
        private CheckBox _resetDisplayOnStopBox;
        private CheckBox _disableStayAwakeBox;
        private CheckBox _autoStartDexBox;
        private NumericUpDown _deviceMonitorIntervalBox;
        private NumericUpDown _disconnectMonitorIntervalBox;
        private NumericUpDown _connectedStartDelayBox;
        private NumericUpDown _adbWakeUpDelayBox;
        private NumericUpDown _autoHideSecondsBox;
        private NumericUpDown _captureWaitSecondsBox;
        private NumericUpDown _processTimeoutBox;
        private TextBox _captureHotkeyBox;
        private TextBox _exitHotkeyBox;
        private CheckBox _lowLevelHotkeyBox;
        private CheckBox _keyboardDiagnosticsBox;
        private CheckBox _convertHangulBox;
        private ComboBox _hangulInputModeBox;
        private CheckBox _rightWindowsBox;
        private CheckBox _convertEnterBox;
        private ComboBox _enterInputModeBox;
        private CheckBox _ignoreShiftSpaceBox;
        private RadioButton _usbConnectionBox;
        private RadioButton _wirelessConnectionBox;
        private TextBox _wirelessHostBox;
        private NumericUpDown _wirelessPortBox;
        private CheckBox _wirelessAutoReconnectBox;
        private Label _wirelessStatusLabel;
        private NumericUpDown _pairingPortBox;
        private TextBox _pairingCodeBox;
        private Button _wirelessPrepareButton;
        private Button _wirelessConnectButton;
        private Button _wirelessDisconnectButton;
        private Button _pairButton;
        private ComboBox _languageBox;
        private ComboBox _themeBox;
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

            Text = LocalizationService.Get("Settings.Title");
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(820, 610);
            MinimumSize = new Size(760, 540);

            var description = new Label
            {
                Dock = DockStyle.Top,
                Height = 44,
                Padding = new Padding(10),
                Text = LocalizationService.Get("Settings.Description")
            };

            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(BuildGeneralTab());
            tabs.TabPages.Add(BuildConnectionTab());
            tabs.TabPages.Add(BuildPathTab());
            tabs.TabPages.Add(BuildKeyboardTab());
            tabs.TabPages.Add(BuildTimingTab());
            tabs.TabPages.Add(BuildDiagnosticsTab());

            var bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 76
            };
            _saveStatusLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 24,
                Padding = new Padding(8, 0, 12, 0),
                TextAlign = ContentAlignment.MiddleRight,
                AutoEllipsis = true,
                Visible = false
            };
            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(8)
            };
            var saveButton = new Button
            {
                Text = LocalizationService.Get("Common.Save"),
                Size = new Size(96, 32)
            };
            saveButton.Click += SaveButton_Click;
            var closeButton = new Button
            {
                Text = LocalizationService.Get("Common.Close"),
                Size = new Size(96, 32)
            };
            closeButton.Click += delegate { Close(); };
            buttons.Controls.Add(saveButton);
            buttons.Controls.Add(closeButton);
            bottomPanel.Controls.Add(_saveStatusLabel);
            bottomPanel.Controls.Add(buttons);
            _saveStatusTimer = new Timer { Interval = 2800 };
            _saveStatusTimer.Tick += delegate
            {
                _saveStatusTimer.Stop();
                _saveStatusLabel.Visible = false;
            };
            FormClosed += delegate { _saveStatusTimer.Dispose(); };

            Controls.Add(tabs);
            Controls.Add(description);
            Controls.Add(bottomPanel);
            LoadValues();
        }

        private TabPage BuildGeneralTab()
        {
            var page = new TabPage(LocalizationService.Get("Settings.General"));
            var table = CreateTable();
            _languageBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Left,
                Width = 210
            };
            foreach (AppLanguage language in Enum.GetValues(typeof(AppLanguage)))
                _languageBox.Items.Add(new LanguageOption(language));
            AddRow(table, LocalizationService.Get("Settings.Language"), _languageBox);
            AddRow(
                table,
                string.Empty,
                new Label
                {
                    AutoSize = true,
                    ForeColor = Color.DimGray,
                    Text = LocalizationService.Get("Settings.LanguageRestart")
                });
            _themeBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Left,
                Width = 210
            };
            foreach (AppTheme theme in Enum.GetValues(typeof(AppTheme)))
                _themeBox.Items.Add(new ThemeOption(theme));
            AddRow(table, LocalizationService.Get("Settings.Theme"), _themeBox);
            AddRow(
                table,
                string.Empty,
                new Label
                {
                    AutoSize = true,
                    ForeColor = Color.DimGray,
                    Text = LocalizationService.Get("Settings.ThemeRestart")
                });
            _startWithWindowsBox = AddCheck(table, LocalizationService.Get("Settings.StartWithWindows"));
            _startMinimizedBox = AddCheck(table, LocalizationService.Get("Settings.StartMinimized"));
            _wakeUpModeBox = AddCombo<ScrcpyWakeUpMode>(table, LocalizationService.Get("Settings.WakeUpMode"));
            _autoHideBox = AddCheck(table, LocalizationService.Get("Settings.AutoHide"));
            _autoStartDexBox = AddCheck(table, LocalizationService.Get("Settings.AutoStartDex"));
            _resetDisplayOnStopBox = AddCheck(table, LocalizationService.Get("Settings.ResetDisplay"));
            _disableStayAwakeBox = AddCheck(table, LocalizationService.Get("Settings.DisableStayAwake"));
            page.Controls.Add(Wrap(table));
            return page;
        }

        private TabPage BuildPathTab()
        {
            var page = new TabPage(LocalizationService.Get("Settings.Paths"));
            var table = CreateTable();

            _automaticAdbBox = new RadioButton
            {
                Text = LocalizationService.Get("Settings.AdbAuto"),
                AutoSize = true
            };
            _manualAdbBox = new RadioButton
            {
                Text = LocalizationService.Get("Settings.AdbManual"),
                AutoSize = true
            };
            _automaticAdbBox.CheckedChanged += delegate { UpdateManualAdbControls(); };
            _manualAdbBox.CheckedChanged += delegate { UpdateManualAdbControls(); };
            AddRow(table, LocalizationService.Get("Settings.AdbMode"), _automaticAdbBox);
            AddRow(table, string.Empty, _manualAdbBox);
            AddReadOnly(table, LocalizationService.Get("Settings.CurrentOs"), WindowsVersionHelper.GetDisplayName());
            AddReadOnly(table, LocalizationService.Get("Settings.CurrentAdb"), _adbService.AdbPath);
            AddReadOnly(table, LocalizationService.Get("Settings.AdbVersion"), GetAdbVersionText());

            _manualAdbPanel = CreatePathPanel(out _manualAdbPathBox, true);
            AddRow(table, LocalizationService.Get("Settings.ManualAdbPath"), _manualAdbPanel);
            _scrcpyPathBox = AddPath(table, LocalizationService.Get("Settings.ScrcpyPath"), true);
            _screenshotFolderBox = AddPath(table, LocalizationService.Get("Settings.ScreenshotFolder"), false);
            _deviceScreenshotFolderBox = AddText(table, LocalizationService.Get("Settings.DeviceFolder"));
            _logFolderBox = AddPath(table, LocalizationService.Get("Settings.LogFolder"), false);
            page.Controls.Add(Wrap(table));
            return page;
        }

        private TabPage BuildConnectionTab()
        {
            var page = new TabPage(LocalizationService.Get("Settings.Connection"));
            var table = CreateTable();

            _usbConnectionBox = new RadioButton
            {
                Text = LocalizationService.Get("Settings.Usb"),
                AutoSize = true
            };
            _wirelessConnectionBox = new RadioButton
            {
                Text = LocalizationService.Get("Settings.Wireless"),
                AutoSize = true
            };
            _usbConnectionBox.CheckedChanged += delegate
            {
                UpdateWirelessControls();
            };
            _wirelessConnectionBox.CheckedChanged += delegate
            {
                UpdateWirelessControls();
            };
            AddRow(table, LocalizationService.Get("Settings.ConnectionMode"), _usbConnectionBox);
            AddRow(table, string.Empty, _wirelessConnectionBox);

            _wirelessHostBox = AddText(table, LocalizationService.Get("Settings.PhoneIp"));
            _wirelessPortBox = AddNumber(
                table,
                LocalizationService.Get("Settings.ConnectPort"),
                1,
                65535);
            _wirelessAutoReconnectBox = AddCheck(
                table,
                LocalizationService.Get("Settings.AutoReconnect"));

            var connectionButtons = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            _wirelessPrepareButton = new Button
            {
                Text = LocalizationService.Get("Settings.PrepareWireless"),
                AutoSize = true,
                Height = 30
            };
            _wirelessPrepareButton.Click +=
                WirelessPrepareButton_Click;
            _wirelessConnectButton = new Button
            {
                Text = LocalizationService.Get("Settings.ConnectWireless"),
                AutoSize = true,
                Height = 30
            };
            _wirelessConnectButton.Click +=
                WirelessConnectButton_Click;
            _wirelessDisconnectButton = new Button
            {
                Text = LocalizationService.Get("Settings.Disconnect"),
                AutoSize = true,
                Height = 30
            };
            _wirelessDisconnectButton.Click +=
                WirelessDisconnectButton_Click;
            connectionButtons.Controls.Add(_wirelessPrepareButton);
            connectionButtons.Controls.Add(_wirelessConnectButton);
            connectionButtons.Controls.Add(_wirelessDisconnectButton);
            AddRow(table, LocalizationService.Get("Settings.WirelessActions"), connectionButtons);

            _wirelessStatusLabel = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(570, 0)
            };
            AddRow(table, LocalizationService.Get("Settings.CurrentStatus"), _wirelessStatusLabel);

            AddRow(
                table,
                "Android 11+",
                new Label
                {
                    AutoSize = true,
                    MaximumSize = new Size(570, 0),
                    Text = LocalizationService.Get("Settings.PairGuide")
                });
            _pairingPortBox = AddNumber(
                table,
                LocalizationService.Get("Settings.PairingPort"),
                1,
                65535);
            _pairingCodeBox = AddText(table, LocalizationService.Get("Settings.PairingCode"));
            _pairingCodeBox.UseSystemPasswordChar = true;
            _pairButton = new Button
            {
                Text = LocalizationService.Get("Settings.Pair"),
                AutoSize = true,
                Height = 30
            };
            _pairButton.Click += PairButton_Click;
            AddRow(table, string.Empty, _pairButton);

            page.Controls.Add(Wrap(table));
            return page;
        }

        private TabPage BuildKeyboardTab()
        {
            var page = new TabPage(LocalizationService.Get("Settings.Keyboard"));
            var table = CreateTable();
            _captureHotkeyBox = AddText(table, LocalizationService.Get("Settings.CaptureHotkey"));
            _exitHotkeyBox = AddText(table, LocalizationService.Get("Settings.ExitHotkey"));
            _lowLevelHotkeyBox = AddCheck(table, LocalizationService.Get("Settings.LowLevelHotkey"));
            _keyboardDiagnosticsBox = AddCheck(table, LocalizationService.Get("Settings.KeyDiagnostics"));
            _convertHangulBox = AddCheck(table, LocalizationService.Get("Settings.HangulCorrection"));
            _hangulInputModeBox = AddCombo<KeyInputMode>(table, LocalizationService.Get("Settings.HangulMode"));
            _rightWindowsBox = AddCheck(table, LocalizationService.Get("Settings.RightWindows"));
            _convertEnterBox = AddCheck(table, LocalizationService.Get("Settings.EnterConversion"));
            _enterInputModeBox = AddCombo<KeyInputMode>(table, LocalizationService.Get("Settings.EnterMode"));
            _ignoreShiftSpaceBox = AddCheck(table, LocalizationService.Get("Settings.IgnoreShiftSpace"));
            page.Controls.Add(Wrap(table));
            return page;
        }

        private TabPage BuildTimingTab()
        {
            var page = new TabPage(LocalizationService.Get("Settings.Timing"));
            var table = CreateTable();
            _deviceMonitorIntervalBox = AddNumber(table, LocalizationService.Get("Settings.DeviceInterval"), 200, 60000);
            _disconnectMonitorIntervalBox = AddNumber(table, LocalizationService.Get("Settings.DisconnectInterval"), 200, 60000);
            _connectedStartDelayBox = AddNumber(table, LocalizationService.Get("Settings.StartDelay"), 0, 60000);
            _adbWakeUpDelayBox = AddNumber(table, LocalizationService.Get("Settings.WakeDelay"), 0, 60000);
            _autoHideSecondsBox = AddNumber(table, LocalizationService.Get("Settings.HideDelay"), 1, 3600);
            _captureWaitSecondsBox = AddNumber(table, LocalizationService.Get("Settings.CaptureDelay"), 1, 60);
            _processTimeoutBox = AddNumber(table, LocalizationService.Get("Settings.ProcessTimeout"), 1000, 120000);
            _pushCaptureBox = AddCheck(table, LocalizationService.Get("Settings.PushCapture"));
            page.Controls.Add(Wrap(table));
            return page;
        }

        private TabPage BuildDiagnosticsTab()
        {
            var page = new TabPage(
                LocalizationService.Get("Settings.Diagnostics"));
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                Padding = new Padding(24),
                WrapContents = false
            };
            panel.Controls.Add(new Label
            {
                AutoSize = true,
                MaximumSize = new Size(700, 0),
                Margin = new Padding(0, 0, 0, 18),
                Text = LocalizationService.Get("Settings.DiagnosticsGuide")
            });
            var logButton = new Button
            {
                Text = LocalizationService.Get("Settings.OpenLogs"),
                Size = new Size(220, 36),
                Margin = new Padding(0, 0, 0, 10)
            };
            logButton.Click += delegate
            {
                if (_showLogs != null) _showLogs();
            };
            var environmentButton = new Button
            {
                Text = LocalizationService.Get("Settings.OpenEnvironment"),
                Size = new Size(220, 36)
            };
            environmentButton.Click += delegate
            {
                if (_showEnvironmentCheck != null)
                    _showEnvironmentCheck();
            };
            panel.Controls.Add(logButton);
            panel.Controls.Add(environmentButton);
            page.Controls.Add(panel);
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

        private static TableLayoutPanel CreateTable()
        {
            return new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                Padding = new Padding(12)
            };
        }

        private static Control Wrap(Control content)
        {
            return new Panel { Dock = DockStyle.Fill, AutoScroll = true, Controls = { content } };
        }

        private static TextBox AddText(TableLayoutPanel table, string label)
        {
            var box = new TextBox { Dock = DockStyle.Fill };
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
            var textBox = new TextBox { Dock = DockStyle.Fill };
            box = textBox;
            var button = new Button
            {
                Text = LocalizationService.Get("Common.Browse"),
                Dock = DockStyle.Right,
                Width = 86
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
            var panel = new Panel { Dock = DockStyle.Fill, Height = 28 };
            panel.Controls.Add(textBox);
            panel.Controls.Add(button);
            return panel;
        }

        private static NumericUpDown AddNumber(TableLayoutPanel table, string label, int min, int max)
        {
            var box = new NumericUpDown { Minimum = min, Maximum = max, Dock = DockStyle.Left, Width = 120 };
            AddRow(table, label, box);
            return box;
        }

        private static CheckBox AddCheck(TableLayoutPanel table, string label)
        {
            var box = new CheckBox { Text = label, AutoSize = true };
            AddRow(table, string.Empty, box);
            return box;
        }

        private static ComboBox AddCombo<T>(TableLayoutPanel table, string label)
        {
            var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Left, Width = 210 };
            foreach (var value in Enum.GetValues(typeof(T))) box.Items.Add(value);
            AddRow(table, label, box);
            return box;
        }

        private static void AddReadOnly(TableLayoutPanel table, string label, string value)
        {
            AddRow(table, label, new Label { AutoSize = true, MaximumSize = new Size(570, 0), Text = value });
        }

        private static void AddRow(TableLayoutPanel table, string label, Control control)
        {
            var row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 12, 8) }, 0, row);
            table.Controls.Add(control, 1, row);
            control.Margin = new Padding(3, 5, 3, 5);
        }

        private static decimal Clamp(int value, NumericUpDown box)
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
