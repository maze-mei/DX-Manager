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
        private void SelectDexMode()
        {
            SaveCurrentModeBeforeSwitch();
            DisplayMode(0);
        }

        private void DisplayMode(int slot)
        {
            if (slot < 0 || slot > 3) slot = 0;
            _selectedMode = slot;
            if (_selectedDeviceContext != null)
                _selectedDeviceContext.SelectedMode = slot;
            SetSelectedModeButton(0);
            if (slot == 0)
            {
                _modeHintLabel.Text =
                    LocalizationService.Get("Main.DexMode");
                _displaySettingsTitle.Text = LocalizationService.Get(
                    "Main.DisplaySettings.Dex");
                _startButton.Text = LocalizationService.Get(
                    "Main.StartDex");
                _stopButton.Text = LocalizationService.Get(
                    "Main.StopDex");
                _flexDisplayBox.Visible = false;
                _flexDisplayBox.Enabled = false;
                LayoutStartAppControls(false);
            }
            else
            {
                SetSelectedModeButton(slot);
                _modeHintLabel.Text = LocalizationService.Format(
                    "Main.SingleMode",
                    slot);
                _displaySettingsTitle.Text = LocalizationService.Format(
                    "Main.DisplaySettings.Single",
                    slot);
                _startButton.Text = LocalizationService.Get(
                    "Main.StartSingle");
                _stopButton.Text = LocalizationService.Get(
                    "Main.StopSingle");
                _flexDisplayBox.Visible = true;
                _flexDisplayBox.Enabled = true;
                LayoutStartAppControls(true);
            }
            LoadRunSettings();
            SetAppProfileControlsVisible(slot > 0);
            LayoutDesktopContent();
            UpdateRunningState();
        }

        private void SelectSingleWindowPreview(int slot)
        {
            SaveCurrentModeBeforeSwitch();
            DisplayMode(slot);
        }

        private void SaveCurrentModeBeforeSwitch()
        {
            try
            {
                ApplyRunSettings(false);
            }
            catch (Exception ex)
            {
                _logService.Warning(LocalizationService.Format(
                    "Log.Main.ModeSwitchSaveFailed",
                    ex.Message));
            }
        }

        private SingleWindowSlotSettings GetSingleWindowSettings(int slot)
        {
            return GetSingleWindowSettings(
                GetSelectedDeviceRunSettings(),
                slot);
        }

        private static SingleWindowSlotSettings GetSingleWindowSettings(
            DeviceRunSettingsProfile settings,
            int slot)
        {
            if (settings == null)
                throw new ArgumentNullException("settings");
            if (slot < 1 || slot > settings.SingleWindowSlots.Count)
                throw new ArgumentOutOfRangeException("slot");
            return settings.SingleWindowSlots[slot - 1];
        }

        private bool IsCustomResolutionSelected()
        {
            var preset = _resolutionBox.SelectedItem as ResolutionPreset;
            return preset == null || !AppSettings.IsPresetResolution(preset.Width,preset.Height);
        }

        private int GetCurrentCustomWidth()
        {
            return _selectedMode == 0
                ? GetSelectedDeviceRunSettings()
                    .VirtualDisplay.CustomWidth
                : GetSingleWindowSettings(_selectedMode).CustomWidth;
        }

        private int GetCurrentCustomHeight()
        {
            return _selectedMode == 0
                ? GetSelectedDeviceRunSettings()
                    .VirtualDisplay.CustomHeight
                : GetSingleWindowSettings(_selectedMode).CustomHeight;
        }

        private void StoreCurrentCustomResolution()
        {
            var width = (int)_widthBox.Value;
            var height = (int)_heightBox.Value;
            var selectedMode = _selectedMode;
            _settingsService.UpdateInMemory(_settings, delegate(
                AppSettings settings)
            {
                var runSettings = GetDeviceRunSettings(
                    settings,
                    _selectedDeviceIdentity);
                if (selectedMode == 0)
                {
                    runSettings.VirtualDisplay.CustomWidth = width;
                    runSettings.VirtualDisplay.CustomHeight = height;
                    return;
                }

                var slot = GetSingleWindowSettings(
                    runSettings,
                    selectedMode);
                slot.CustomWidth = width;
                slot.CustomHeight = height;
            });
        }

        private bool IsSelectedModeRunning()
        {
            return _selectedMode == 0
                ? _scrcpyService.IsRunning
                : _singleWindowService.IsRunning(_selectedMode);
        }

        private void AttachRunSettingChangeHandlers()
        {
            EventHandler changed = delegate { MarkRunSettingsDirty(); };
            _resolutionBox.SelectedIndexChanged += changed;
            _widthBox.ValueChanged += changed;
            _heightBox.ValueChanged += changed;
            _dpiBox.ValueChanged += changed;
            _bitRateBox.TextChanged += changed;
            _maxFpsBox.SelectedIndexChanged += MaxFpsSelectionChanged;
            _turnScreenOffBox.CheckedChanged += changed;
            _stayAwakeBox.CheckedChanged += changed;
            _useHidKeyboardBox.CheckedChanged += changed;
            _useHidMouseBox.CheckedChanged += changed;
            _forceStopAppBox.CheckedChanged += changed;
            _flexDisplayBox.CheckedChanged += changed;
            _hidePhonePreviewBox.CheckedChanged += delegate
            {
                if (_loadingRunSettings) return;
                var previous = GetSelectedDeviceRunSettings()
                    .VirtualDisplay.HidePhonePreview;
                var requested = _hidePhonePreviewBox.Checked;
                try
                {
                    // Persist only this choice immediately. A reconnect may
                    // auto-start before the next Start/Save-mode action.
                    _settingsService.UpdateAndSave(_settings, delegate(AppSettings settings)
                    {
                        GetDeviceRunSettings(settings, _selectedDeviceIdentity)
                            .VirtualDisplay.HidePhonePreview = requested;
                    });
                }
                catch (Exception ex)
                {
                    _loadingRunSettings = true;
                    _hidePhonePreviewBox.Checked = previous;
                    _loadingRunSettings = false;
                    UpdatePhonePreviewControls();
                    ShowError(LocalizationService.Get("Error.ApplyLaunchSettings"), ex);
                    return;
                }
                UpdatePhonePreviewControls();
                MarkRunSettingsDirty();
            };
            _additionalArgumentsBox.TextChanged += changed;
            _startAppBox.SelectedIndexChanged += changed;
        }

        private void MarkRunSettingsDirty()
        {
            if (_loadingRunSettings ||
                _selectedMode < 0 ||
                _selectedMode >= _modeSettingsDirty.Length)
            {
                return;
            }

            _modeSettingsDirty[_selectedMode] = true;
            UpdateApplySettingsLink();
        }

        private void UpdatePhonePreviewControls()
        {
            if (IsDisposed || Disposing) return;
            var isDex = _selectedMode == 0;
            var hidePreview = isDex && _hidePhonePreviewBox.Checked;
            _hidePhonePreviewBox.Visible = isDex;
            _hidePhonePreviewBox.Enabled = isDex &&
                !string.IsNullOrWhiteSpace(_selectedDeviceIdentity) &&
                !PhysicalDeviceRegistry.IsTemporaryIdentity(_selectedDeviceIdentity) &&
                !_exitInProgress && !_orchestrator.IsRunning &&
                !_orchestrator.IsBusy &&
                !_phonePreviewPendingStarts.ContainsKey(_orchestrator);

            // Keep the checkbox value so returning to the existing overlay
            // mode restores the user's preference; only the DeX launch ignores it.
            _turnScreenOffBox.Enabled = !hidePreview && !FrameRateRules.RequiresScreenOn(GetSelectedMaxFps());
            _turnScreenOffBox.Text = LocalizationService.Get(
                hidePreview
                    ? "Main.ScreenOff.HiddenPreview"
                    : "Main.ScreenOff");
            _deviceTabToolTip.SetToolTip(
                _turnScreenOffBox,
                hidePreview
                    ? LocalizationService.Get("Main.ScreenOff.HiddenPreview.Help")
                    : string.Empty);
            _resolutionBox.Enabled = !hidePreview;
            _dpiBox.Enabled = !hidePreview;
            _widthBox.Enabled = !hidePreview && IsCustomResolutionSelected();
            _heightBox.Enabled = !hidePreview && IsCustomResolutionSelected();
            foreach (var control in new Control[]
            {
                _resolutionBox, _dpiBox, _widthBox, _heightBox,
                _resolutionLabel, _dpiLabel
            })
            {
                _deviceTabToolTip.SetToolTip(
                    control,
                    hidePreview
                        ? LocalizationService.Get("Main.HidePhonePreview.ResolutionHelp")
                        : string.Empty);
            }
        }

        private void BeginPhonePreviewStart(DexOrchestrator orchestrator)
        {
            int count;
            _phonePreviewPendingStarts.TryGetValue(orchestrator, out count);
            _phonePreviewPendingStarts[orchestrator] = count + 1;
            UpdatePhonePreviewControls();
            if (!IsDisposed && !Disposing && _selectedMode == 0 &&
                ReferenceEquals(orchestrator, _orchestrator))
                _startButton.Enabled = false;
        }

        private void EndPhonePreviewStart(DexOrchestrator orchestrator)
        {
            int count;
            if (_phonePreviewPendingStarts.TryGetValue(orchestrator, out count))
            {
                if (count <= 1) _phonePreviewPendingStarts.Remove(orchestrator);
                else _phonePreviewPendingStarts[orchestrator] = count - 1;
            }
            UpdatePhonePreviewControls();
        }

        private void UpdateApplySettingsLink()
        {
            if (_applySettingsLink == null) return;
            _applySettingsLink.Visible =
                IsSelectedModeRunning() &&
                _modeSettingsDirty[_selectedMode];
        }

        private bool IsAnySingleWindowRunning()
        {
            for (var slot = 1; slot <= 3; slot++)
            {
                if (_singleWindowService.IsRunning(slot)) return true;
            }
            return false;
        }

        private bool IsAnyScrcpyRunning()
        {
            return _scrcpyService.IsRunning ||
                IsAnySingleWindowRunning();
        }

        private string GetSingleWindowStatusDetail(int slot)
        {
            var settings = GetSingleWindowSettings(slot);
            var app = string.IsNullOrWhiteSpace(settings.StartAppName)
                ? settings.StartAppPackage
                : settings.StartAppName;
            return string.IsNullOrWhiteSpace(app)
                ? LocalizationService.Get("Main.SingleRunningDetail")
                : LocalizationService.Format(
                    "Main.AppRunningDetail",
                    app);
        }

        private void UpdateSingleWindowIndicator(int slot)
        {
            if (!string.IsNullOrWhiteSpace(_connectionError))
            {
                SetConnectionIndicator(
                    Color.Firebrick,
                    LocalizationService.Get("Status.Error"),
                    _connectionError);
                return;
            }
            if (_lastDeviceState == null ||
                _lastDeviceState.Status != AdbDeviceStatus.Device)
            {
                UpdateIndicatorForDevice(_lastDeviceState);
                return;
            }

            var settings = GetSingleWindowSettings(slot);
            SetConnectionIndicator(
                _theme.Accent,
                LocalizationService.Format(IsSingleWindowAppMissing(slot)?"Main.SingleWaiting":"Main.SingleReady", slot),
                string.IsNullOrWhiteSpace(settings.StartAppPackage)
                    ? LocalizationService.Get("Main.SelectApp")
                    : LocalizationService.Get("Main.PressStart"));
        }

        private void SetSelectedModeButton(int slot)
        {
            SetButtonPrimary(_dexModeButton, slot == 0);
            SetButtonPrimary(_singleModeButton1, slot == 1);
            SetButtonPrimary(_singleModeButton2, slot == 2);
            SetButtonPrimary(_singleModeButton3, slot == 3);
        }

        private static void SetButtonPrimary(ThemedButton button, bool selected)
        {
            if (button == null) return;
            button.Primary = selected;
            button.Invalidate();
        }

        private void AddTopMenu(string text, int x, Action action)
        {
            var menu = new UiLinkLabel
            {
                AutoSize = true,
                LinkColor = Color.FromArgb(75, 85, 99),
                ActiveLinkColor = Color.FromArgb(37, 99, 235),
                Location = new Point(x, 43),
                Text = text
            };
            menu.LinkClicked += delegate { action(); };
            Controls.Add(menu);
        }

        private Label AddSectionTitle(string text, int x, int y)
        {
            var label = new UiLabel
            {
                AutoSize = true,
                Font = UiFonts.Create(13F, FontStyle.Bold),
                ForeColor = _theme.TextSecondary,
                Location = new Point(x, y),
                Text = text
            };
            Controls.Add(label);
            return label;
        }

        private Label AddFieldLabel(string text, int x, int y)
        {
            var label = new UiLabel
            {
                AutoSize = true,
                ForeColor = _theme.TextTertiary,
                Location = new Point(x, y),
                Text = text
            };
            Controls.Add(label);
            return label;
        }

        private void AddDivider(int y)
        {
            Controls.Add(new Panel
            {
                BackColor = _theme.CardBorder,
                Location = new Point(32, y),
                Size = new Size(573, 1)
            });
        }

        private static string GetDeviceStatusText(DeviceState state)
        {
            if (state.Status == AdbDeviceStatus.Device)
                return LocalizationService.Format(
                    "Device.Connected",
                    state.Serial);
            if (state.Status == AdbDeviceStatus.Unauthorized)
                return LocalizationService.Get("Device.Unauthorized");
            if (state.Status == AdbDeviceStatus.Offline)
                return LocalizationService.Get("Device.Offline");
            return LocalizationService.Get("Device.Disconnected");
        }

        private static decimal Clamp(int value, ThemedNumberControl box)
        {
            if (value < box.Minimum) return box.Minimum;
            if (value > box.Maximum) return box.Maximum;
            return value;
        }
    }
}
