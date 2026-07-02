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

        public EnvironmentCheckForm(EnvironmentCheckService checkService)
        {
            _checkService = checkService;

            Text = LocalizationService.Get("Environment.Title");
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(820, 430);
            MinimumSize = new Size(680, 360);

            var guide = new Label
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

            var panel = new FlowLayoutPanel
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

            var closeButton = new Button
            {
                Text = LocalizationService.Get("Common.Close"),
                Size = new Size(90, 30)
            };
            closeButton.Click += delegate { Close(); };

            panel.Controls.Add(_checkButton);
            panel.Controls.Add(closeButton);
            Controls.Add(_resultList);
            Controls.Add(guide);
            Controls.Add(panel);
            Shown += async delegate { await RunCheckAsync(); };
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
