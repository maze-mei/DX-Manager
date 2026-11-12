using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using DexManager.Services;
using DexManager.Utils;

namespace DexManager.Forms
{
    public sealed class ThirdPartyLicensesForm : Form
    {
        private readonly Panel _topPanel;
        private readonly Panel _contentHost;
        private readonly RoundedPanel _contentCard;
        private readonly Panel _tabPanel;
        private readonly ThemedButton _noticesButton;
        private readonly ThemedButton _scrcpyButton;
        private readonly ThemedButton _scrcpyDexButton;
        private readonly RichTextBox _contentBox;
        private readonly Panel _bottomPanel;
        private readonly ThemedButton _closeButton;
        private readonly Label _titleLabel;
        private readonly Label _guideLabel;
        private readonly string _noticesText;
        private readonly string _scrcpyLicenseText;
        private readonly string _scrcpyDexLicenseText;

        public ThirdPartyLicensesForm()
        {
            Text = LocalizationService.Get("Licenses.Title");
            Icon = AppIconProvider.Current;
            StartPosition = FormStartPosition.CenterScreen;
            Font = UiFonts.Create(9.5F);
            UiWindowStyle.ApplyFixedStandardSize(this);

            _noticesText = ReadBundledText(
                Path.Combine(
                    "licenses",
                    "THIRD_PARTY_NOTICES.md"));
            _scrcpyLicenseText = ReadBundledText(
                Path.Combine("licenses", "scrcpy-LICENSE.txt"));
            _scrcpyDexLicenseText = ReadBundledText(Path.Combine("licenses", "ScrcpyDex-NOTICE.txt")) +
                Environment.NewLine + Environment.NewLine +
                ReadBundledText(Path.Combine("licenses", "ScrcpyDex-APACHE-2.0.txt"));

            _topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 104
            };
            _titleLabel = new UiLabel
            {
                AutoSize = true,
                Font = UiFonts.Create(20F, FontStyle.Bold),
                Location = new Point(28, 20),
                Text = LocalizationService.Get("Licenses.Title")
            };
            _guideLabel = new UiLabel
            {
                AutoEllipsis = true,
                Location = new Point(30, 63),
                Size = new Size(710, 24),
                Text = LocalizationService.Get("Licenses.Guide")
            };
            _topPanel.Controls.Add(_titleLabel);
            _topPanel.Controls.Add(_guideLabel);

            _bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 68,
                Padding = new Padding(24, 12, 24, 16)
            };
            _closeButton = new ThemedButton
            {
                Dock = DockStyle.Right,
                Size = new Size(108, 36),
                Text = LocalizationService.Get("Common.Close")
            };
            _closeButton.Click += delegate { Close(); };
            _bottomPanel.Controls.Add(_closeButton);

            _contentHost = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 0, 24, 8)
            };
            _contentCard = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Radius = 14,
                Padding = new Padding(18)
            };
            _tabPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 46
            };
            _noticesButton = CreateTabButton(
                LocalizationService.Get("Licenses.NoticesTab"),
                0,
                160);
            _noticesButton.Click += delegate
            {
                ShowDocument(_noticesText, _noticesButton);
            };
            _scrcpyButton = CreateTabButton(
                LocalizationService.Get("Licenses.ScrcpyTab"),
                170,
                190);
            _scrcpyButton.Click += delegate
            {
                ShowDocument(_scrcpyLicenseText, _scrcpyButton);
            };
            _scrcpyDexButton = CreateTabButton(
                LocalizationService.Get("Licenses.ScrcpyDexTab"),
                370,
                220);
            _scrcpyDexButton.Click += delegate
            {
                ShowDocument(_scrcpyDexLicenseText, _scrcpyDexButton);
            };
            _tabPanel.Controls.Add(_noticesButton);
            _tabPanel.Controls.Add(_scrcpyButton);
            _tabPanel.Controls.Add(_scrcpyDexButton);

            _contentBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                DetectUrls = true,
                WordWrap = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Font = UiFonts.Create(9.5F),
                TabStop = false
            };
            _contentCard.Controls.Add(_contentBox);
            ThemedScrollBar.Attach(_contentBox);
            _contentCard.Controls.Add(_tabPanel);
            _contentHost.Controls.Add(_contentCard);

            Controls.Add(_contentHost);
            Controls.Add(_bottomPanel);
            Controls.Add(_topPanel);

            ApplyCurrentTheme();
            ShowDocument(_noticesText, _noticesButton);
        }

        private ThemedButton CreateTabButton(
            string text,
            int left,
            int width)
        {
            return new ThemedButton
            {
                Location = new Point(left, 0),
                Size = new Size(width, 34),
                CornerRadius = 17,
                Text = text
            };
        }

        private string ReadBundledText(string relativePath)
        {
            var path = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                relativePath);
            try
            {
                return File.Exists(path)
                    ? File.ReadAllText(path)
                    : LocalizationService.Format(
                        "Licenses.FileMissing",
                        path);
            }
            catch (Exception ex)
            {
                return LocalizationService.Format(
                    "Licenses.ReadFailed",
                    path,
                    ex.Message);
            }
        }

        private void ShowDocument(string text, ThemedButton selected)
        {
            _noticesButton.Primary = selected == _noticesButton;
            _scrcpyButton.Primary = selected == _scrcpyButton;
            _scrcpyDexButton.Primary = selected == _scrcpyDexButton;
            _contentBox.Text = text ?? string.Empty;
            _contentBox.SelectionStart = 0;
            _contentBox.SelectionLength = 0;
            _contentBox.ScrollToCaret();
        }

        private void ApplyCurrentTheme()
        {
            var theme = ThemeColors.Current;
            BackColor = theme.WindowBackground;
            _topPanel.BackColor = theme.WindowBackground;
            _contentHost.BackColor = theme.WindowBackground;
            _bottomPanel.BackColor = theme.WindowBackground;
            _titleLabel.BackColor = theme.WindowBackground;
            _titleLabel.ForeColor = theme.TextPrimary;
            _guideLabel.BackColor = theme.WindowBackground;
            _guideLabel.ForeColor = theme.TextTertiary;
            _contentCard.BackColor = theme.WindowBackground;
            _contentCard.FillColor = theme.CardBackground;
            _contentCard.BorderColor = theme.CardBorder;
            _tabPanel.BackColor = theme.CardBackground;
            _noticesButton.BackColor = theme.CardBackground;
            _scrcpyButton.BackColor = theme.CardBackground;
            _scrcpyDexButton.BackColor = theme.CardBackground;
            _contentBox.BackColor = theme.CardBackground;
            _contentBox.ForeColor = theme.TextSecondary;
            _closeButton.BackColor = theme.WindowBackground;
            Invalidate(true);
        }
    }
}
