using System;
using System.Drawing;
using System.Windows.Forms;
using DexManager.Services;

namespace DexManager.Forms
{
    public sealed class LogForm : Form
    {
        private readonly LogService _logService;
        private readonly RichTextBox _logTextBox;
        private string _lastDisplayedMessage;

        public LogForm(LogService logService)
        {
            _logService = logService;

            Text = "DEX Manager 로그";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(860, 520);
            MinimumSize = new Size(640, 360);

            _logTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.White,
                Font = new Font("Consolas", 9F),
                WordWrap = false
            };

            var saveButton = new Button
            {
                Text = "로그 저장",
                AutoSize = true,
                Margin = new Padding(6)
            };
            saveButton.Click += SaveButton_Click;

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 42,
                Padding = new Padding(4)
            };
            toolbar.Controls.Add(saveButton);

            Controls.Add(_logTextBox);
            Controls.Add(toolbar);
            Load += LogForm_Load;
            FormClosed += LogForm_FormClosed;
        }

        private void LogForm_Load(object sender, EventArgs e)
        {
            LoadSessionLog();
            _logService.EntryWritten += LogService_EntryWritten;
        }

        private void LogForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            _logService.EntryWritten -= LogService_EntryWritten;
        }

        private void LoadSessionLog()
        {
            foreach (var line in _logService.GetSessionEntries())
                AppendIfNew(line);
            MoveToEnd();
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new SaveFileDialog
            {
                Title = "로그 저장",
                Filter = "로그 파일 (*.log)|*.log|텍스트 파일 (*.txt)|*.txt",
                DefaultExt = "log",
                AddExtension = true,
                FileName = "DEX_Manager_" +
                    DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log"
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    _logService.SaveSession(dialog.FileName);
                    MessageBox.Show(
                        this,
                        "현재 실행 로그를 저장했습니다.",
                        "DEX Manager",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        this,
                        "로그를 저장하지 못했습니다.\r\n\r\n" + ex.Message,
                        "DEX Manager",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        private void LogService_EntryWritten(object sender, LogEventArgs e)
        {
            if (IsDisposed || !IsHandleCreated) return;

            BeginInvoke((Action)delegate
            {
                AppendIfNew(e.Message);
                MoveToEnd();
            });
        }

        private void AppendIfNew(string line)
        {
            var comparable = GetComparableMessage(line);
            if (string.Equals(
                comparable,
                _lastDisplayedMessage,
                StringComparison.Ordinal))
            {
                return;
            }

            _lastDisplayedMessage = comparable;
            _logTextBox.AppendText(line + Environment.NewLine);
        }

        private static string GetComparableMessage(string line)
        {
            if (string.IsNullOrEmpty(line)) return string.Empty;
            var levelEnd = line.IndexOf("] ", StringComparison.Ordinal);
            return levelEnd >= 0 ? line.Substring(levelEnd + 2) : line;
        }

        private void MoveToEnd()
        {
            _logTextBox.SelectionStart = _logTextBox.TextLength;
            _logTextBox.ScrollToCaret();
        }
    }
}
