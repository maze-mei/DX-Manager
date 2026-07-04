using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using DexManager.Models;
using DexManager.Services;

namespace DexManager.Forms
{
    public sealed class EnvironmentCheckForm : Form
    {
        private readonly EnvironmentCheckService _checkService;
        private readonly ListView _resultList;
        private readonly Button _checkButton;
        private readonly Button _closeButton;
        private readonly Label _guide;
        private readonly FlowLayoutPanel _buttonPanel;

        public EnvironmentCheckForm(EnvironmentCheckService checkService)
        {
            _checkService = checkService;

            Text = LocalizationService.Get("Environment.Title");
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(820, 430);
            MinimumSize = new Size(680, 360);

            _guide = new Label
            {
                Dock = DockStyle.Top,
                Height = 66,
                Padding = new Padding(10),
                Text = LocalizationService.Format(
                    "Environment.Guide",
                    Environment.NewLine)
            };

            _resultList = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };
            _resultList.Columns.Add(LocalizationService.Get("Environment.Status"), 80);
            _resultList.Columns.Add(LocalizationService.Get("Environment.Item"), 130);
            _resultList.Columns.Add(LocalizationService.Get("Environment.Result"), 560);

            _buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(6)
            };
            _checkButton = new Button
            {
                Text = LocalizationService.Get("Environment.CheckAgain"),
                Size = new Size(100, 30)
            };
            _checkButton.Click += async delegate { await RunCheckAsync(); };

            _closeButton = new Button
            {
                Text = LocalizationService.Get("Common.Close"),
                Size = new Size(90, 30)
            };
            _closeButton.Click += delegate { Close(); };

            _buttonPanel.Controls.Add(_checkButton);
            _buttonPanel.Controls.Add(_closeButton);
            Controls.Add(_resultList);
            Controls.Add(_guide);
            Controls.Add(_buttonPanel);
            ApplyCurrentTheme();
            Shown += async delegate { await RunCheckAsync(); };
        }

        public void ApplyCurrentTheme()
        {
            var theme = ThemeColors.Current;
            BackColor = theme.WindowBackground;
            _guide.BackColor = theme.WindowBackground;
            _guide.ForeColor = theme.TextPrimary;
            _buttonPanel.BackColor = theme.WindowBackground;
            _checkButton.BackColor = theme.CardSoft;
            _checkButton.ForeColor = theme.TextPrimary;
            _closeButton.BackColor = theme.CardSoft;
            _closeButton.ForeColor = theme.TextPrimary;
            _resultList.BackColor = theme.CardBackground;
            _resultList.ForeColor = theme.TextPrimary;
            Invalidate(true);
        }

        private async Task RunCheckAsync()
        {
            _checkButton.Enabled = false;
            _resultList.Items.Clear();

            try
            {
                var results = await Task.Run(
                    delegate { return _checkService.Run(); });
                ShowResults(results);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    LocalizationService.Format(
                        "Environment.RunFailed",
                        Environment.NewLine,
                        ex.Message),
                    LocalizationService.Get("App.Name"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                _checkButton.Enabled = true;
            }
        }

        private void ShowResults(IEnumerable<EnvironmentCheckItem> results)
        {
            foreach (var result in results)
            {
                var item = new ListViewItem(GetStatusText(result.Status));
                item.SubItems.Add(result.Name);
                item.SubItems.Add(result.Message);
                item.ForeColor = GetStatusColor(result.Status);
                _resultList.Items.Add(item);
            }
        }

        private static string GetStatusText(EnvironmentCheckStatus status)
        {
            if (status == EnvironmentCheckStatus.Passed)
                return LocalizationService.Get("Environment.Passed");
            if (status == EnvironmentCheckStatus.Warning)
                return LocalizationService.Get("Environment.Warning");
            return LocalizationService.Get("Environment.Failed");
        }

        private static Color GetStatusColor(EnvironmentCheckStatus status)
        {
            if (status == EnvironmentCheckStatus.Passed) return Color.DarkGreen;
            if (status == EnvironmentCheckStatus.Warning) return Color.DarkOrange;
            return Color.DarkRed;
        }
    }
}
