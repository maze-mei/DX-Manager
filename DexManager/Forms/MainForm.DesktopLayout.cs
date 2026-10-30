using System;
using DexManager.Models;
using System.Drawing;
using System.Windows.Forms;
using DexManager.Services;
using DexManager.Utils;

namespace DexManager.Forms
{
    public sealed partial class MainForm
    {
        private Panel _desktopContent;
        private RoundedPanel _appCard;
        private ThemedSelectControl _headerDevices;
        private Label _transportBadge;
        private RoundedPanel _transportSurface;
        private SymbolView _transportUsbIcon, _transportWifiIcon;
        private bool _headerUsbConnected, _headerWifiConnected;
        private ThemedButton _headerRefreshButton;
        private SymbolView _phoneArt;
        private Label _brandLabel;
        private PictureBox _brandIcon;
        private ThemedButton _helpSidebarButton;
        private Label _displaySubtitle, _optionsSubtitle, _appSubtitle;
        private bool _desktopLayoutReady;
        private HelpForm _helpForm;
        private int _autoStartSettingsGeneration;
        private bool _lastAutoStartEnabled;

        private sealed class HeaderDeviceItem
        {
            public DeviceUiContext Context;
            public string Name;
            public override string ToString() { return Name; }
        }

        private readonly System.Collections.Generic.Dictionary<Control,Tuple<float,bool>> _desktopFonts = new System.Collections.Generic.Dictionary<Control,Tuple<float,bool>>();
        private float _lastDesktopFontFit = -1;
        private float DesktopScale { get { var dpi=Math.Max(1F,CurrentAutoScaleDimensions.Width/96F); return Math.Max(0.4F,Math.Min(dpi,Math.Min(ClientSize.Width/1200F,ClientSize.Height/768F))); } }
        private void RememberDesktopFonts(Control parent)
        {
            _desktopFonts[parent]=Tuple.Create(parent.Font.SizeInPoints,parent.Font.FontFamily.Name.Contains("SemiBold"));
            foreach(Control child in parent.Controls)RememberDesktopFonts(child);
        }
        private void FitDesktopFonts()
        {
            var fit=DesktopScale/Math.Max(1F,CurrentAutoScaleDimensions.Width/96F);
            if(Math.Abs(fit-_lastDesktopFontFit)<0.001F)return;
            _lastDesktopFontFit=fit;
            foreach(var pair in _desktopFonts)if(!pair.Key.IsDisposed)pair.Key.Font=UiFonts.Create(pair.Value.Item1*fit,pair.Value.Item2?FontStyle.Bold:FontStyle.Regular);
        }
        private int DesktopPixels(int value) { return (int)Math.Round(value * DesktopScale); }
        private void Place(Control control, int x, int y, int width, int height)
        {
            control.Bounds = new Rectangle(DesktopPixels(x), DesktopPixels(y), DesktopPixels(width), DesktopPixels(height));
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            DesktopSurface.Paint(e.Graphics, ClientRectangle);
        }

        private void InitializeDesktopLayout()
        {
            DoubleBuffered = true;
            _desktopContent = new BufferedPanel { BackColor = Color.Transparent, AutoScroll = true };
            Controls.Add(_desktopContent);
            ThemedScrollBar.Attach(_desktopContent);
            _statusCard = CreateCard(Point.Empty, new Size(880, 200));
            _displayCard = CreateCard(Point.Empty, new Size(420, 256));
            _optionsCard = CreateCard(Point.Empty, new Size(420, 256));
            _appCard = CreateCard(Point.Empty, new Size(880, 180));
            foreach (var card in new[] { _statusCard, _displayCard, _optionsCard, _appCard })
            {
                card.Parent = _desktopContent;
                card.Radius = 22;
                card.BackColor = Color.Transparent;
                // Alpha blending is confined to painted surfaces; text stays opaque.
                card.SurfaceOpacity = 244;
            }
            _statusCard.Radius = 24; _displayCard.Radius = 20; _optionsCard.Radius = 20; _appCard.Radius = 20;
            _phoneArt = new SymbolView { Symbol = UiSymbol.Phone, Tile = true };
            _statusCard.Controls.Add(_phoneArt);
            _pageTitle.Parent = _statusCard;
            _pageTitle.AutoSize = false;
            _pageTitle.AutoEllipsis = true;
            _pageTitle.Font = UiFonts.Pixels(30F, true);
            _pageTitle.BackColor = Color.Transparent;
            _indicatorDot.Parent = _statusCard;
            _indicatorStatus.Parent = _statusCard;
            _indicatorStatus.Font = UiFonts.Pixels(17F, true);
            _indicatorStatus.BackColor = Color.Transparent;
            _indicatorDetail.Parent = _statusCard;
            _indicatorDetail.Visible = false;
            _indicatorDetail.BackColor = Color.Transparent;
            _deviceInfoLabel.Visible = false;
            foreach (var field in new Control[] { _resolutionBox, _maxFpsBox, _dpiBox, _bitRateBox, _widthBox, _heightBox, _startAppBox }) field.Font = UiFonts.Pixels(14F);
            foreach (var label in new[] { _resolutionLabel, _dpiLabel, _bitRateLabel, _maxFpsLabel, _widthLabel, _heightLabel }) label.Font = UiFonts.Pixels(13F);
            _headerDevices = new ThemedSelectControl { AccessibleName = LocalizationService.Get("Main.ConnectedDevices") };
            _headerDevices.CenterSelectedText = true;
            _resolutionBox.SelectionChangeCommitted += ResolutionDialogSelectionCommitted;
            _resolutionBox.CenterSelectedText = true;
            _resolutionBox.CenterInWholeField = true;
            _maxFpsBox.CenterSelectedText = true;
            _maxFpsBox.CenterInWholeField = true;
            _dpiBox.CenterText = true; _dpiBox.ShowStepButtons = false;
            _bitRateBox.CenterText = true;
            _widthBox.CenterText = true; _heightBox.CenterText = true;
            _startAppBox.CenterSelectedText = true;
            _headerDevices.SelectionChangeCommitted += delegate
            {
                var item = _headerDevices.SelectedItem as HeaderDeviceItem;
                if (item != null) SelectDeviceContext(item.Context);
            };
            _transportBadge = new UiLabel { TextAlign = ContentAlignment.MiddleCenter,
                Font=UiFonts.Pixels(15),BackColor=Color.Transparent,AutoEllipsis=true };
            _transportSurface=new RoundedPanel {Radius=8,FillColor=_theme.CardSoft,BorderColor=_theme.ControlBorder,BackColor=Color.Transparent};
            _transportUsbIcon = new SymbolView { Symbol = UiSymbol.Usb, ForeColor = _theme.TextSecondary };
            _transportWifiIcon = new SymbolView { Symbol = UiSymbol.Wifi, ForeColor = _theme.TextSecondary, Visible = false };
            _transportSurface.Controls.AddRange(new Control[] { _transportBadge, _transportUsbIcon, _transportWifiIcon });
            _headerRefreshButton = new ThemedButton { Symbol = UiSymbol.Refresh, Text = string.Empty,
                AccessibleName = LocalizationService.Get("Desktop.Refresh") };
            // Refresh only the projection; the existing monitor owns connection commands.
            _headerRefreshButton.Click += delegate { _deviceMonitor.RequestRefresh(); };
            _statusCard.Controls.AddRange(new Control[] { _headerDevices, _transportSurface, _headerRefreshButton });
            _startButton.Font = UiFonts.Create(17F * 0.75F, FontStyle.Bold);
            _stopButton.Font = UiFonts.Create(17F * 0.75F, FontStyle.Bold);
            _startButton.Parent = _statusCard;
            _stopButton.Parent = _statusCard;
            ((ThemedButton)_startButton).Symbol = UiSymbol.Launch;
            ((ThemedButton)_startButton).CenterIconText = true;
            ((ThemedButton)_stopButton).Symbol = UiSymbol.Power;
            ((ThemedButton)_stopButton).CenterIconText = true;
            _applySettingsLink.Parent = _statusCard;

            _displaySettingsTitle.Parent = _displayCard;
            _displaySettingsTitle.Font = UiFonts.Pixels(20F, true);
            _optionsTitle.Parent = _optionsCard;
            _optionsTitle.Font = UiFonts.Pixels(20F, true);
            _startAppLabel.Parent = _appCard;
            _startAppLabel.Font = UiFonts.Pixels(20F, true);
            _displaySubtitle = AddDesktopSubtitle(_displayCard, "Desktop.DisplaySubtitle");
            _optionsSubtitle = AddDesktopSubtitle(_optionsCard, "Desktop.OptionsSubtitle");
            _appSubtitle = AddDesktopSubtitle(_appCard, "Desktop.AppSubtitle");
            _appSubtitle.Text = StoreUiText.Get("MainStartAppDescription.Text", _appSubtitle.Text);
            _bitRateLabel.Text = StoreUiText.Get("MainBitrateLabel.Text", _bitRateLabel.Text);
            _loadAppsButton.Text = StoreUiText.Get("MainLoadApps.Text", _loadAppsButton.Text);
            AddDesktopSectionIcon(_displayCard, UiSymbol.Display);
            AddDesktopSectionIcon(_optionsCard, UiSymbol.Options);
            AddDesktopSectionIcon(_appCard, UiSymbol.Apps);
            foreach (var control in new Control[] { _resolutionLabel, _resolutionBox, _widthLabel, _widthBox,
                _heightLabel, _heightBox, _dpiLabel, _dpiBox, _bitRateLabel, _bitRateBox, _maxFpsLabel, _maxFpsBox })
                control.Parent = _displayCard;
            var options = new[] { _turnScreenOffBox, _useHidKeyboardBox, _useHidMouseBox,
                _forceStopAppBox, _flexDisplayBox, _stayAwakeBox, _hidePhonePreviewBox };
            var symbols = new[] { UiSymbol.Phone, UiSymbol.Keyboard, UiSymbol.Mouse, UiSymbol.Close, UiSymbol.Display, UiSymbol.Moon, UiSymbol.Display };
            for (var index = 0; index < options.Length; index++)
            {
                options[index].Parent = _optionsCard;
                options[index].Font = UiFonts.Pixels(14F);
                ((ThemedCheckBox)options[index]).Symbol = symbols[index];
                _deviceTabToolTip.SetToolTip(options[index], options[index].Text);
                options[index].Text = System.Text.RegularExpressions.Regex.Replace(options[index].Text, @"\s+\(-[^)]*\)$", string.Empty);
            }
            _hidePhonePreviewBox.Text = LocalizationService.Get("Main.HidePhonePreview.Short");
            _deviceTabToolTip.SetToolTip(_hidePhonePreviewBox,
                LocalizationService.Get("Main.HidePhonePreview.Help"));
            foreach (var control in new Control[] { _startAppBox, _loadAppsButton, _appProfileButton,
                _advancedToggle, _additionalArgumentsBox }) control.Parent = _appCard;
            ((ThemedButton)_loadAppsButton).Symbol = UiSymbol.Refresh;
            _modeHintLabel.Visible = false;
            foreach (Control control in Controls)
                if (control is Panel && control != _desktopContent && control.Height == 1) control.Visible = false;

            _sidebar.Radius = 0;
            _sidebar.BorderColor = _theme.NavigationBackground;
            _brandLabel = new UiLabel { Text = LocalizationService.Get("App.Name"), AutoSize = false,
                Font = UiFonts.Pixels(18F, true), TextAlign = ContentAlignment.MiddleLeft };
            _brandIcon = new PictureBox { Image = AppIconProvider.Current.ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom };
            _sidebar.Controls.AddRange(new Control[] { _brandLabel, _brandIcon });
            _dexModeButton.Symbol = UiSymbol.Display;
            foreach (var button in new[] { _singleModeButton1, _singleModeButton2, _singleModeButton3 }) button.Symbol = UiSymbol.Launch;
            _settingsSidebarButton.Symbol = UiSymbol.Settings;
            _settingsSidebarButton.TrailingText = null;
            _helpSidebarButton = CreateSidebarButton(LocalizationService.Get("Desktop.Help"), 0, false, false);
            _helpSidebarButton.Symbol = UiSymbol.Help;
            _helpSidebarButton.Click += delegate { ShowDesktopHelp(); };
            _sidebar.Controls.Add(_helpSidebarButton);
            foreach (var button in new[] { _dexModeButton, _singleModeButton1, _singleModeButton2, _singleModeButton3, _settingsSidebarButton, _helpSidebarButton }) button.Font = UiFonts.Pixels(14F);
            _helpSidebarButton.Font = UiFonts.Pixels(LocalizationService.IsKorean ? 14F : 13F);
            RememberDesktopFonts(this);
            _desktopLayoutReady = true;
            Resize += delegate { LayoutDesktopContent(); };
            Disposed += delegate { _brandIcon.Image.Dispose(); if (_helpForm != null) _helpForm.Dispose(); };
            LayoutDesktopContent();
        }

        private Label AddDesktopSubtitle(Control card, string key)
        {
            var label = new UiLabel { Text = LocalizationService.Get(key), AutoEllipsis = true,
                BackColor = Color.Transparent, ForeColor = _theme.TextTertiary, Font = UiFonts.Pixels(12F) };
            card.Controls.Add(label); return label;
        }
        private void AddDesktopSectionIcon(Control card, UiSymbol symbol)
        {
            var icon = new SymbolView { Symbol = symbol, Tile = true };
            Place(icon, 24, 25, 38, 38); card.Controls.Add(icon);
        }

        private void LayoutDesktopContent()
        {
            if (!_desktopLayoutReady) return;
            FitDesktopFonts();
            var totalWidth = 1200;
            var totalHeight = (int)(ClientSize.Height / DesktopScale);
            Place(_sidebar, 0, 0, 236, totalHeight);
            Place(_desktopContent, 236, 0, totalWidth - 236, totalHeight);
            // Compute viewport width independent of the last scroll position.
            var width = totalWidth - 296;
            var paired = width >= 760;
            if (!paired) width = Math.Max(560, width - (int)Math.Ceiling(SystemInformation.VerticalScrollBarWidth / DesktopScale));
            var cardWidth = paired ? (int)Math.Round((width - 18) * 0.46) : width;
            var optionsWidth = paired ? width - cardWidth - 18 : width;
            _desktopContent.SuspendLayout();
            _desktopContent.AutoScrollPosition = Point.Empty;
            Place(_statusCard, 28, 40, width, 214);
            Place(_displayCard, 28, 274, cardWidth, 258);
            Place(_optionsCard, paired ? 28 + cardWidth + 18 : 28, paired ? 274 : 550, optionsWidth, 258);
            Place(_appCard, 28, paired ? 550 : 826, width, _additionalArgumentsBox.Visible ? 238 : 190);
            _desktopContent.AutoScrollMinSize = new Size(0, DesktopPixels(_additionalArgumentsBox.Visible ? 816 : 0));
            var artSize = width < 650 ? 112 : 166;
            Place(_phoneArt, 28, 24, artSize, artSize);
            var headerX = artSize + 64;
            Place(_pageTitle, headerX, 40, width - headerX - 24, 38);
            LayoutHeaderConnection(width, headerX);
            Place(_indicatorDot, headerX, 142, 26, 26);
            Place(_indicatorStatus, headerX + 36, 140, Math.Max(80, width - 170 - 28 - 16 - headerX - 36), 30);
            Place(_indicatorDetail, headerX, 180, Math.Max(80, width - headerX - 230), 20);
            Place(_startButton, width - 198, 128, 170, 56);
            _stopButton.Bounds = _startButton.Bounds;
            Place(_applySettingsLink, width - 198, 187, 170, 20);
            _displaySettingsTitle.AutoSize=false;_optionsTitle.AutoSize=false;_startAppLabel.AutoSize=false;
            Place(_displaySettingsTitle, 74, 25, cardWidth - 98, 38);
            _displaySettingsTitle.TextAlign=ContentAlignment.MiddleLeft;_displaySubtitle.Visible=false;
            Place(_displaySubtitle, 74, 52, cardWidth - 98, 18);
            Place(_optionsTitle, 74, 25, optionsWidth - 98, 38);
            _optionsTitle.TextAlign=ContentAlignment.MiddleLeft;_optionsSubtitle.Visible=false;
            Place(_optionsSubtitle, 74, 52, optionsWidth - 98, 18);
            Place(_startAppLabel, 74, 25, width - 98, 38);
            _startAppLabel.TextAlign=ContentAlignment.MiddleLeft;_appSubtitle.Visible=false;
            Place(_appSubtitle, 74, 52, width - 98, 18);
            LayoutResolutionControls(IsCustomResolutionSelected());
            var optionWidth = (optionsWidth - 60) / 2;
            Place(_turnScreenOffBox, 24, 96, optionWidth, 40);
            Place(_useHidKeyboardBox, 24, 143, optionWidth, 40);
            Place(_useHidMouseBox, 24, 190, optionWidth, 40);
            Place(_forceStopAppBox, 36 + optionWidth, 96, optionWidth, 40);
            Place(_flexDisplayBox, 36 + optionWidth, 143, optionWidth, 40);
            Place(_stayAwakeBox, 36 + optionWidth, _selectedMode == 0 ? 143 : 190, optionWidth, 40);
            Place(_hidePhonePreviewBox, 36 + optionWidth, 190, optionWidth, 40);
            LayoutStartAppControls(_selectedMode != 0);
            Place(_advancedToggle, 28, 132, 170, 28);
            Place(_additionalArgumentsBox, 24, 168, width - 48, 40);
            LayoutSidebarNavigation();
            _desktopContent.ResumeLayout();
            PositionAppNotice();
        }

        private void LayoutHeaderConnection(int width, int headerX)
        {
            _transportUsbIcon.ForeColor = _theme.TextSecondary;
            _transportWifiIcon.ForeColor = _theme.TextSecondary;
            var both = _headerUsbConnected && _headerWifiConnected;
            var any = _headerUsbConnected || _headerWifiConnected;
            var labelLeft = both ? 64 : any ? 36 : 10;
            var textWidth = (int)Math.Ceiling(UiText.MeasureText(_transportBadge.Text,
                _transportBadge.Font, Size.Empty, TextFormatFlags.NoPadding).Width / DesktopScale);
            var badgeWidth = Math.Max(78, labelLeft + textWidth + 14);
            var deviceWidth = Math.Min(230, width - headerX - badgeWidth - 70);
            Place(_headerDevices, headerX, 90, deviceWidth, 38);
            Place(_transportSurface, headerX + deviceWidth + 10, 90, badgeWidth, 40);
            Place(_headerRefreshButton, headerX + deviceWidth + badgeWidth + 20, 90, 40, 40);
            Place(_transportUsbIcon, 10, 10, 20, 20);
            Place(_transportWifiIcon, both ? 36 : 10, 10, 20, 20);
            Place(_transportBadge, labelLeft, 1, badgeWidth - labelLeft - 10, 38);
        }

        private void RefreshHeaderDevices()
        {
            if (_headerDevices == null) return;
            _headerDevices.SelectedIndex = -1;
            _headerDevices.Items.Clear();
            var selected = -1;
            foreach (var item in GetDeviceContextEntries())
            {
                var context = item.Value;
                if (context.Device == null || !context.Device.IsConnected) continue;
                _headerDevices.Items.Add(new HeaderDeviceItem { Context = context, Name = GetContextDisplayName(context) });
                if (ReferenceEquals(context, _selectedDeviceContext)) selected = _headerDevices.Items.Count - 1;
            }
            _headerDevices.SelectedIndex = selected;
            _headerDevices.Enabled = _headerDevices.Items.Count > 0;
            _pageTitle.Text = _selectedDeviceContext == null || _selectedDeviceContext.Device == null || !_selectedDeviceContext.Device.IsConnected || string.IsNullOrWhiteSpace(_selectedDeviceContext.Identity)
                ? LocalizationService.Get("Main.WaitingPhone") : GetContextDisplayName(_selectedDeviceContext);
            var device = _selectedDeviceContext == null ? null : _selectedDeviceContext.Device;
            var usb = device != null && device.SelectAuthorizedTransport(DeviceTransportKind.Usb, null) != null;
            var wifi = device != null && device.SelectAuthorizedTransport(DeviceTransportKind.Wireless, null) != null;
            _transportBadge.Text = usb && wifi ? "USB + Wi-Fi" : wifi ? "Wi-Fi" : usb ? "USB" : "-";
            _headerUsbConnected = usb;
            _headerWifiConnected = wifi;
            _transportUsbIcon.Visible = usb;
            _transportWifiIcon.Visible = wifi;
            if (_desktopLayoutReady)
            {
                var width = (int)Math.Round(_statusCard.Width / DesktopScale);
                LayoutHeaderConnection(width, (width < 650 ? 112 : 166) + 64);
            }
        }

        private int _resolutionBeforeDialogWidth, _resolutionBeforeDialogHeight;
        private bool IsResolutionEditorCommandSelected()
        {
            var preset=_resolutionBox.SelectedItem as ResolutionPreset;
            return preset!=null && preset.Width==0;
        }
        private Form CreateCustomResolutionDialog(out ThemedNumberControl width, out ThemedNumberControl height)
        {
            var dialog=new Form {Text=LocalizationService.Get("Main.Custom"),Icon=AppIconProvider.Current,
                Font=UiFonts.Pixels(14),FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,
                StartPosition=FormStartPosition.CenterParent,ClientSize=new Size(360,160),BackColor=_theme.CardBackground};
            width=new ThemedNumberControl {Minimum=320,Maximum=4096,Value=_widthBox.Value,CenterText=true,Bounds=new Rectangle(24,40,140,40)};
            height=new ThemedNumberControl {Minimum=240,Maximum=4096,Value=_heightBox.Value,CenterText=true,Bounds=new Rectangle(188,40,140,40)};
            var first=width;
            foreach(var input in new[]{width,height})input.MouseDown+=delegate(object sender,MouseEventArgs mouse) {
                var field=(ThemedNumberControl)sender;
                if(mouse.Button==MouseButtons.Left && mouse.X<field.Width-30)
                    field.BeginInvoke((Action)delegate {if(!field.IsDisposed && field.Focused)field.SelectAll();});
            };
            var ok=new ThemedButton {Primary=true,Text=LocalizationService.IsKorean?"확인":"OK",Bounds=new Rectangle(228,104,100,40),DialogResult=DialogResult.OK};
            dialog.Controls.AddRange(new Control[]{new UiLabel {Text=LocalizationService.Get("Main.Width"),Bounds=new Rectangle(24,14,140,24),ForeColor=_theme.TextPrimary},new UiLabel {Text=LocalizationService.Get("Main.Height"),Bounds=new Rectangle(188,14,140,24),ForeColor=_theme.TextPrimary},width,height,ok});
            dialog.AcceptButton=ok;dialog.ActiveControl=first;
            dialog.Shown+=delegate {dialog.Activate();dialog.BeginInvoke((Action)delegate {if(!dialog.IsDisposed){first.Focus();first.SelectAll();}});};
            return dialog;
        }
        private void CommitCustomResolution(int width,int height)
        {
            _widthBox.Value=width;_heightBox.Value=height;
            StoreCurrentCustomResolution();
            _settingsService.UpdateInMemory(_settings,delegate(DexManager.Models.AppSettings settings){settings.RememberCustomResolution(width,height);});
            RefreshRecentResolutionOptions();
            MarkRunSettingsDirty();
        }
        private void ResolutionDialogSelectionCommitted(object sender, EventArgs e)
        {
            if (_loadingRunSettings || !IsResolutionEditorCommandSelected()) return;
            ThemedNumberControl width,height;
            using(var dialog=CreateCustomResolutionDialog(out width,out height))
            {
                if(dialog.ShowDialog(this)==DialogResult.OK)CommitCustomResolution((int)width.Value,(int)height.Value);
                else {
                    if(_resolutionBeforeDialogWidth>0 && _resolutionBeforeDialogHeight>0){_widthBox.Value=_resolutionBeforeDialogWidth;_heightBox.Value=_resolutionBeforeDialogHeight;}
                    RefreshRecentResolutionOptions();
                }
            }
        }        private void ShowDesktopHelp()
        {
            _autoHideService.ResetIdleHideState();
            if (_helpForm == null || _helpForm.IsDisposed) _helpForm = new HelpForm();
            if (!_helpForm.Visible) _helpForm.Show();
            _helpForm.WindowState = FormWindowState.Normal;
            _helpForm.BringToFront();
            _helpForm.Activate();
        }
    }
}
