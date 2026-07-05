using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DexManager.Models;
using DexManager.Services;
using DexManager.Utils;

namespace DexManager.Forms
{
    public sealed class EnvironmentCheckForm : Form
    {
        private readonly EnvironmentCheckService _checkService;
        private readonly Label _titleLabel;
        private readonly Label _guideLabel;
        private readonly Label _statusLabel;
        private readonly Panel _topPanel;
        private readonly Panel _cardHost;
        private readonly Panel _headerPanel;
        private readonly FlowLayoutPanel _resultPanel;
        private readonly RoundedPanel _resultCard;
        private readonly Panel _bottomPanel;
        private readonly ThemedButton _checkButton;
        private readonly ThemedButton _closeButton;
        private IList<EnvironmentCheckItem> _lastResults =
            new List<EnvironmentCheckItem>();

        public EnvironmentCheckForm(
            EnvironmentCheckService checkService)
        {
            _checkService = checkService;

            Text = LocalizationService.Get("Environment.Title");
            Icon = AppIconProvider.Current;
            StartPosition = FormStartPosition.CenterScreen;
            Font = UiFonts.Create(9.5F);
            UiWindowStyle.ApplyFixedStandardSize(this);

            _topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 112,
                Padding = new Padding(28, 20, 28, 10)
            };
            _titleLabel = new Label
            {
                AutoSize = true,
                Font = UiFonts.Create(
                    20F,
                    FontStyle.Bold),
                Location = new Point(28, 18),
                Text = LocalizationService.Get(
                    "Environment.Title")
            };
            _guideLabel = new Label
            {
                AutoEllipsis = true,
                Location = new Point(30, 61),
                Size = new Size(790, 42),
                Text = LocalizationService.Format(
                    "Environment.Guide",
                    Environment.NewLine)
            };
            _topPanel.Controls.Add(_titleLabel);
            _topPanel.Controls.Add(_guideLabel);

            _bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 70,
                Padding = new Padding(28, 12, 28, 18)
            };
            _statusLabel = new Label
            {
                AutoEllipsis = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 232,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = Padding.Empty
            };
            _checkButton = new ThemedButton
            {
                Primary = true,
                Text = LocalizationService.Get(
                    "Environment.CheckAgain"),
                Size = new Size(112, 36),
                Margin = Padding.Empty
            };
            _checkButton.Click += async delegate
            {
                await RunCheckAsync();
            };
            _closeButton = new ThemedButton
            {
                Text = LocalizationService.Get("Common.Close"),
                Size = new Size(104, 36),
                Margin = new Padding(0, 0, 12, 0)
            };
            _closeButton.Click += delegate { Close(); };
            buttonPanel.Controls.Add(_checkButton);
            buttonPanel.Controls.Add(_closeButton);
            _bottomPanel.Controls.Add(_statusLabel);
            _bottomPanel.Controls.Add(buttonPanel);

            _cardHost = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 0, 24, 8)
            };
            _resultCard = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Radius = 14,
                Padding = new Padding(18, 14, 18, 14)
            };
            _headerPanel = CreateHeader();
            _resultPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };
            _resultPanel.SizeChanged += delegate
            {
                ResizeResultRows();
            };
            _resultCard.Controls.Add(_resultPanel);
            _resultCard.Controls.Add(_headerPanel);
            _cardHost.Controls.Add(_resultCard);

            Controls.Add(_cardHost);
            Controls.Add(_bottomPanel);
            Controls.Add(_topPanel);
            ApplyCurrentTheme();
            Shown += async delegate { await RunCheckAsync(); };
        }

        public void ApplyCurrentTheme()
        {
            var theme = ThemeColors.Current;
            BackColor = theme.WindowBackground;
            _topPanel.BackColor = theme.WindowBackground;
            _cardHost.BackColor = theme.WindowBackground;
            _titleLabel.BackColor = theme.WindowBackground;
            _titleLabel.ForeColor = theme.TextPrimary;
            _guideLabel.BackColor = theme.WindowBackground;
            _guideLabel.ForeColor = theme.TextTertiary;
            _bottomPanel.BackColor = theme.WindowBackground;
            _statusLabel.BackColor = theme.WindowBackground;
            _statusLabel.ForeColor = theme.TextTertiary;
            _resultCard.BackColor = theme.WindowBackground;
            _resultCard.FillColor = theme.CardBackground;
            _resultCard.BorderColor = theme.CardBorder;
            _headerPanel.BackColor = theme.CardBackground;
            foreach (var label in _headerPanel.Controls
                .OfType<Label>())
            {
                label.BackColor = theme.CardBackground;
                label.ForeColor = theme.TextTertiary;
            }
            foreach (var divider in _headerPanel.Controls
                .OfType<Panel>())
            {
                divider.BackColor = theme.CardBorder;
            }
            _resultPanel.BackColor = theme.CardBackground;
            _checkButton.BackColor = theme.WindowBackground;
            _closeButton.BackColor = theme.WindowBackground;
            if (_lastResults.Count > 0)
                ShowResults(_lastResults);
            Invalidate(true);
        }

        private Panel CreateHeader()
        {
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36
            };
            header.Controls.Add(CreateHeaderLabel(
                LocalizationService.Get("Environment.Status"),
                0,
                90));
            header.Controls.Add(CreateHeaderLabel(
                LocalizationService.Get("Environment.Item"),
                90,
                180));
            header.Controls.Add(CreateHeaderLabel(
                LocalizationService.Get("Environment.Result"),
                270,
                480));
            var divider = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 1
            };
            header.Controls.Add(divider);
            return header;
        }

        private static Label CreateHeaderLabel(
            string text,
            int left,
            int width)
        {
            return new Label
            {
                AutoEllipsis = true,
                Location = new Point(left, 4),
                Size = new Size(width, 25),
                Font = UiFonts.Create(
                    9.5F,
                    FontStyle.Bold),
                Text = text
            };
        }

        private async Task RunCheckAsync()
        {
            _checkButton.Enabled = false;
            _statusLabel.Text = LocalizationService.Get(
                "Environment.Checking");
            _resultPanel.Controls.Clear();

            try
            {
                var results = await Task.Run(
                    delegate { return _checkService.Run(); });
                _lastResults = results;
                ShowResults(results);
                var failed = results.Count(
                    item => item.Status ==
                        EnvironmentCheckStatus.Failed);
                var warning = results.Count(
                    item => item.Status ==
                        EnvironmentCheckStatus.Warning);
                _statusLabel.Text = failed > 0
                    ? LocalizationService.Get(
                        "Environment.Failed")
                    : warning > 0
                        ? LocalizationService.Get(
                            "Environment.Warning")
                        : LocalizationService.Get(
                            "Environment.Passed");
            }
            catch (Exception ex)
            {
                var failed = new EnvironmentCheckItem
                {
                    Name = LocalizationService.Get(
                        "Environment.Title"),
                    Status = EnvironmentCheckStatus.Failed,
                    Message = ex.Message
                };
                _lastResults = new[] { failed };
                ShowResults(_lastResults);
                _statusLabel.Text = LocalizationService.Get(
                    "Environment.Failed");
            }
            finally
            {
                _checkButton.Enabled = true;
            }
        }

        private void ShowResults(
            IEnumerable<EnvironmentCheckItem> results)
        {
            _resultPanel.SuspendLayout();
            _resultPanel.Controls.Clear();
            foreach (var result in results)
                _resultPanel.Controls.Add(CreateResultRow(result));
            _resultPanel.ResumeLayout();
            ResizeResultRows();
        }

        private Control CreateResultRow(
            EnvironmentCheckItem result)
        {
            var theme = ThemeColors.Current;
            var color = GetStatusColor(result.Status, theme);
            var row = new Panel
            {
                Height = 54,
                Margin = Padding.Empty,
                BackColor = theme.CardBackground,
                Tag = result
            };
            var status = new Label
            {
                AutoEllipsis = true,
                Location = new Point(0, 15),
                Size = new Size(90, 24),
                ForeColor = color,
                BackColor = theme.CardBackground,
                Font = UiFonts.Create(
                    9.5F,
                    FontStyle.Bold),
                Text = GetStatusText(result.Status)
            };
            var name = new Label
            {
                AutoEllipsis = true,
                Location = new Point(90, 15),
                Size = new Size(180, 24),
                ForeColor = color,
                BackColor = theme.CardBackground,
                Text = result.Name
            };
            var message = new Label
            {
                AutoEllipsis = true,
                Location = new Point(270, 15),
                Size = new Size(450, 24),
                ForeColor = color,
                BackColor = theme.CardBackground,
                Text = result.Message
            };
            var divider = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 1,
                BackColor = theme.CardBorder
            };
            row.Controls.Add(status);
            row.Controls.Add(name);
            row.Controls.Add(message);
            row.Controls.Add(divider);
            return row;
        }

        private void ResizeResultRows()
        {
            var width = Math.Max(
                _resultPanel.ClientSize.Width -
                SystemInformation.VerticalScrollBarWidth,
                400);
            foreach (Control row in _resultPanel.Controls)
            {
                row.Width = width;
                if (row.Controls.Count < 3) continue;
                var message = row.Controls
                    .OfType<Label>()
                    .OrderBy(label => label.Left)
                    .LastOrDefault();
                if (message != null)
                    message.Width = Math.Max(width - 270, 120);
            }
        }

        private static string GetStatusText(
            EnvironmentCheckStatus status)
        {
            if (status == EnvironmentCheckStatus.Passed)
                return LocalizationService.Get(
                    "Environment.Passed");
            if (status == EnvironmentCheckStatus.Warning)
                return LocalizationService.Get(
                    "Environment.Warning");
            return LocalizationService.Get(
                "Environment.Failed");
        }

        private static Color GetStatusColor(
            EnvironmentCheckStatus status,
            ThemePalette theme)
        {
            if (status == EnvironmentCheckStatus.Passed)
                return theme.TextPrimary;
            if (status == EnvironmentCheckStatus.Warning)
                return Color.FromArgb(245, 158, 11);
            return Color.FromArgb(239, 68, 68);
        }
    }
}
