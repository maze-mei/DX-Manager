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

            Text = "DEX Manager 환경 점검";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(820, 430);
            MinimumSize = new Size(680, 360);

            var guide = new Label
            {
                Dock = DockStyle.Top,
                Height = 66,
                Padding = new Padding(10),
                Text =
                    "Android 보안 정책상 USB 디버깅과 RSA 승인은 자동으로 켤 수 없습니다.\r\n" +
                    "아래 점검 결과에 따라 휴대폰과 USB 드라이버 상태를 확인하십시오."
            };

            _resultList = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };
            _resultList.Columns.Add("상태", 80);
            _resultList.Columns.Add("점검 항목", 130);
            _resultList.Columns.Add("결과", 560);

            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(6)
            };
            _checkButton = new Button
            {
                Text = "다시 점검",
                Size = new Size(100, 30)
            };
            _checkButton.Click += async delegate { await RunCheckAsync(); };

            var closeButton = new Button
            {
                Text = "닫기",
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
                    "환경 점검 중 오류가 발생했습니다.\r\n\r\n" + ex.Message,
                    "DEX Manager",
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
            if (status == EnvironmentCheckStatus.Passed) return "정상";
            if (status == EnvironmentCheckStatus.Warning) return "확인";
            return "실패";
        }

        private static Color GetStatusColor(EnvironmentCheckStatus status)
        {
            if (status == EnvironmentCheckStatus.Passed) return Color.DarkGreen;
            if (status == EnvironmentCheckStatus.Warning) return Color.DarkOrange;
            return Color.DarkRed;
        }
    }
}
