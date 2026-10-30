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
        private void OffsetMainContent(int offsetX)
        {
            foreach (Control control in Controls)
            {
                control.Left += offsetX;
            }
        }

        private void ApplyDesignLayout()
        {
            InitializeDesktopLayout();
        }
        private void LayoutStartAppControls(bool showAppProfile)
        {
            if (_appCard == null) return;
            var width = (int)(_appCard.Width / DesktopScale);
            var buttonWidth = 180;
            var profileWidth = showAppProfile ? 144 : 0;
            Place(_startAppBox, 24, 86, width - 58 - buttonWidth - profileWidth, 38);
            Place(_loadAppsButton, width - 24 - buttonWidth - profileWidth, 86, buttonWidth, 40);
            Place(_appProfileButton, width - 24 - 136, 86, 136, 40);
        }
        private RoundedPanel CreateCard(Point location, Size size)
        {
            var card = new RoundedPanel
            {
                Location = location,
                Size = size,
                Radius = 14,
                FillColor = _theme.CardBackground,
                BorderColor = _theme.CardBorder
            };
            Controls.Add(card);
            card.SendToBack();
            return card;
        }

        private static void MoveToCard(
            Control control,
            Control card,
            int x,
            int y)
        {
            control.Parent = card;
            control.Location = new Point(x, y);
        }

        private void ApplyTheme()
        {
            BackColor = _theme.WindowBackground;
            _deviceTabsPanel.BackColor = _theme.NavigationBackground;
            _pageTitle.ForeColor = _theme.TextPrimary;
            _indicatorStatus.ForeColor = _theme.TextPrimary;
            _indicatorDetail.ForeColor = _theme.TextTertiary;
            _deviceInfoLabel.ForeColor = _theme.TextTertiary;

            ApplyCardTheme(_statusCard);
            ApplyCardTheme(_displayCard);
            ApplyCardTheme(_optionsCard);
            ApplyCardTheme(_appCard);
            _displaySettingsTitle.ForeColor = _theme.TextPrimary;
            _optionsTitle.ForeColor = _theme.TextPrimary;

            foreach (var label in new[]
            {
                _resolutionLabel,
                _widthLabel,
                _heightLabel,
                _dpiLabel,
                _bitRateLabel,
                _maxFpsLabel,
                _startAppLabel
            })
            {
                label.ForeColor = _theme.TextSecondary;
                label.BackColor = _theme.CardBackground;
            }

            foreach (var option in new[]
            {
                _turnScreenOffBox,
                _useHidKeyboardBox,
                _useHidMouseBox,
                _forceStopAppBox,
                _flexDisplayBox,
                _hidePhonePreviewBox,
                _stayAwakeBox
            })
            {
                option.BackColor = _theme.CardBackground;
                option.ForeColor = _theme.TextPrimary;
            }

            foreach (var control in new Control[]
            {
                _resolutionBox,
                _widthBox,
                _heightBox,
                _dpiBox,
                _bitRateBox,
                _maxFpsBox,
                _startAppBox,
                _additionalArgumentsBox
            })
            {
                control.BackColor = _theme.CardSoft;
                control.ForeColor = _theme.TextPrimary;
            }

            _advancedToggle.BackColor = _theme.CardBackground;
            _advancedToggle.LinkColor = _theme.Accent;
            _advancedToggle.ActiveLinkColor = _theme.AccentHover;
            _applySettingsLink.LinkColor = _theme.Accent;
            _applySettingsLink.ActiveLinkColor = _theme.AccentHover;

            _sidebar.BackColor = _theme.WindowBackground;
            _sidebar.FillColor = _theme.NavigationBackground;
            _sidebar.BorderColor = _theme.NavigationBackground;
            foreach (Control control in _sidebar.Controls)
            {
                if (ReferenceEquals(control, _deviceSectionDivider))
                {
                    control.BackColor = _theme.CardBorder;
                    continue;
                }
                control.BackColor = _theme.NavigationBackground;
                var label = control as Label;
                if (label != null)
                    label.ForeColor = _theme.TextSecondary;
                var button = control as ThemedButton;
                if (button != null)
                    button.ForeColor = _theme.TextPrimary;
                control.Invalidate();
            }

            if (_brandLabel != null) _brandLabel.ForeColor = _theme.TextPrimary;
            if (_headerDevices != null) _headerDevices.BackColor = _theme.CardBackground;
            _startAppLabel.ForeColor = _theme.TextPrimary;
            foreach (var subtitle in new[] { _displaySubtitle, _optionsSubtitle, _appSubtitle }) if (subtitle != null) subtitle.ForeColor = _theme.TextTertiary;
            if (_transportBadge != null) { _transportBadge.BackColor = Color.Transparent; _transportBadge.ForeColor = _theme.TextPrimary; _transportSurface.FillColor = _theme.CardSoft; _transportSurface.BorderColor = _theme.ControlBorder; }
            if (_helpForm != null && !_helpForm.IsDisposed) _helpForm.ApplyCurrentTheme();
            _indicatorDot.Invalidate();
            _resolutionBox.Invalidate();
            _widthBox.Invalidate();
            _heightBox.Invalidate();
            _dpiBox.Invalidate();
            _bitRateBox.Invalidate();
            _maxFpsBox.Invalidate();
            _startAppBox.Invalidate();
            _startButton.Invalidate();
            _stopButton.Invalidate();
            _loadAppsButton.Invalidate();
            _appProfileButton.Invalidate();
            _appProfileMenu.BackColor = _theme.CardSoft;
            _appProfileMenu.ForeColor = _theme.TextPrimary;
            _saveAppProfileMenuItem.BackColor = _theme.CardSoft;
            _saveAppProfileMenuItem.ForeColor = _theme.TextPrimary;
            _deleteAppProfileMenuItem.BackColor = _theme.CardSoft;
            _deleteAppProfileMenuItem.ForeColor = _theme.TextPrimary;
            RebuildDeviceTabs();
            ApplyDesktopNoticeTheme();
            Invalidate(true);
        }

        private void ApplyThemeSelection(AppTheme theme)
        {
            _theme = ThemeColors.Use(theme);
            ApplyUpdateTheme();
            ApplyTheme();
            if (_fileTransferStatusForm != null && !_fileTransferStatusForm.IsDisposed)
                _fileTransferStatusForm.ApplyCurrentTheme();
            if (_phoneTransferStatusForm != null && !_phoneTransferStatusForm.IsDisposed)
                _phoneTransferStatusForm.ApplyCurrentTheme();
            if (_settingsForm != null &&
                !_settingsForm.IsDisposed)
            {
                _settingsForm.ApplyCurrentTheme();
            }
            if (_logForm != null && !_logForm.IsDisposed)
                _logForm.ApplyCurrentTheme();
            if (_environmentCheckForm != null &&
                !_environmentCheckForm.IsDisposed)
            {
                _environmentCheckForm.ApplyCurrentTheme();
            }
        }

        private void ApplyCardTheme(RoundedPanel card)
        {
            if (card == null) return;
            card.BackColor = Color.Transparent;
            card.FillColor = _theme.CardBackground;
            card.BorderColor = _theme.CardBorder;
            foreach (Control control in card.Controls)
            {
                var label = control as Label;
                if (label != null)
                    label.BackColor = Color.Transparent;
            }
        }

        private void AddModeSidebar()
        {
            _sidebar = new RoundedPanel
            {
                Location = new Point(14, 14),
                Size = new Size(188, 587),
                Radius = 14,
                BackColor = _theme.NavigationBackground,
                FillColor = _theme.NavigationBackground,
                BorderColor = _theme.CardBorder
            };

            _deviceSectionLabel = new UiLabel
            {
                AutoSize = true,
                Font = UiFonts.Create(9.5F, FontStyle.Bold),
                ForeColor = _theme.TextTertiary,
                BackColor = _theme.NavigationBackground,
                Location = new Point(20, 18),
                Text = LocalizationService.Get("Main.ConnectedDevices")
            };
            _sidebar.Controls.Add(_deviceSectionLabel);
            _deviceTabsPanel.BackColor = _theme.NavigationBackground;
            _sidebar.Controls.Add(_deviceTabsPanel);

            _deviceSectionDivider = new Panel
            {
                BackColor = _theme.CardBorder,
                Location = new Point(20, 166),
                Size = new Size(148, 1)
            };
            _sidebar.Controls.Add(_deviceSectionDivider);

            _modeSectionLabel = new UiLabel
            {
                AutoSize = true,
                Font = UiFonts.Create(9.5F, FontStyle.Bold),
                ForeColor = _theme.TextTertiary,
                BackColor = _theme.NavigationBackground,
                Location = new Point(20, 18),
                Text = LocalizationService.Get("Main.Mode")
            };
            _sidebar.Controls.Add(_modeSectionLabel);

            _dexModeButton = CreateSidebarButton(
                LocalizationService.Get("Main.Dex"), 52, true);
            _dexModeButton.Click += delegate { SelectDexMode(); };
            _sidebar.Controls.Add(_dexModeButton);

            _singleModeButton1 = CreateSidebarButton(
                LocalizationService.Format("Main.SingleWindow", 1),
                94,
                false);
            _singleModeButton1.Click += delegate { SelectSingleWindowPreview(1); };
            _sidebar.Controls.Add(_singleModeButton1);

            _singleModeButton2 = CreateSidebarButton(
                LocalizationService.Format("Main.SingleWindow", 2),
                136,
                false);
            _singleModeButton2.Click += delegate { SelectSingleWindowPreview(2); };
            _sidebar.Controls.Add(_singleModeButton2);

            _singleModeButton3 = CreateSidebarButton(
                LocalizationService.Format("Main.SingleWindow", 3),
                178,
                false);
            _singleModeButton3.Click += delegate { SelectSingleWindowPreview(3); };
            _sidebar.Controls.Add(_singleModeButton3);

            _sidebarHintLabel = new UiLabel
            {
                AutoEllipsis = true,
                ForeColor = _theme.TextTertiary,
                BackColor = _theme.NavigationBackground,
                Location = new Point(20, 238),
                Size = new Size(148, 70),
                Text = LocalizationService.Get("Main.SidebarHint")
            };
            _sidebar.Controls.Add(_sidebarHintLabel);

            _settingsSidebarButton = CreateSidebarButton(
                LocalizationService.Get("Main.Settings"),
                _sidebar.Height - 48,
                false,
                false);
            _settingsSidebarButton.ShowSettingsIcon = true;
            _settingsSidebarButton.TrailingText =
                "v" + Application.ProductVersion;
            _settingsSidebarButton.Anchor =
                AnchorStyles.Left | AnchorStyles.Bottom;
            _settingsSidebarButton.Click += delegate { ShowSettingsForm(); };
            _sidebar.Controls.Add(_settingsSidebarButton);

            Controls.Add(_sidebar);
            _sidebar.BringToFront();
            LayoutSidebarNavigation();
        }

        private void LayoutSidebarNavigation()
        {
            if (_sidebar == null) return;
            _deviceSectionLabel.Visible = false;
            _deviceTabsPanel.Visible = false;
            _deviceSectionDivider.Visible = false;
            _modeSectionLabel.Visible = false;
            _sidebarHintLabel.Visible = false;
            if (_brandLabel == null) return;
            var height = (int)(_sidebar.Height / DesktopScale);
            Place(_brandIcon, 16, 32, 34, 34);
            Place(_brandLabel, 60, 29, 163, 40);
            var buttons = new[] { _dexModeButton, _singleModeButton1, _singleModeButton2, _singleModeButton3 };
            for (var i = 0; i < buttons.Length; i++)
            {
                Place(buttons[i], 4, 92 + i * 42, 226, 36);
                buttons[i].CornerRadius = 5;
            }
            Place(_helpSidebarButton, 4, height - 138, 226, 36);
            Place(_settingsSidebarButton, 4, height - 92, 226, 36);
            RefreshHeaderDevices();
        }
        private ThemedButton CreateSidebarButton(
            string text,
            int y,
            bool selected,
            bool showDot = true)
        {
            return new ThemedButton
            {
                Text = text,
                Primary = selected,
                CornerRadius = 18,
                NavigationStyle = true,
                ShowNavigationDot = showDot,
                TabStop = false,
                Location = new Point(10, y),
                Size = new Size(168, 34),
                BackColor = _theme.NavigationBackground,
                ForeColor = _theme.TextSecondary
            };
        }
    }
}
