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
        private readonly FlowLayoutPanel _toolbar;
        private readonly Button _saveButton;
        private string _lastDisplayedMessage;

        public LogForm(LogService logService)
        {
            _logService = logService;

            Text = LocalizationService.Get("Log.Title");
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

            _saveButton = new Button
            {
                Text = LocalizationService.Get("Log.Save"),
                AutoSize = true,
                Margin = new Padding(6)
            };
            _saveButton.Click += SaveButton_Click;

            _toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 42,
                Padding = new Padding(4)
            };
            _toolbar.Controls.Add(_saveButton);

            Controls.Add(_logTextBox);
            Controls.Add(_toolbar);
            ApplyCurrentTheme();
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
                Title = LocalizationService.Get("Log.DialogTitle"),
                Filter = LocalizationService.Get("Log.Filter"),
                DefaultExt = "log",
                AddExtension = true,
                FileName = "DX_Manager_" +
                    DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log"
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    _logService.SaveSession(dialog.FileName);
                    MessageBox.Show(
                        this,
                        LocalizationService.Get("Log.Saved"),
                        LocalizationService.Get("App.Name"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        this,
                        LocalizationService.Format(
                            "Log.SaveFailed",
                            Environment.NewLine,
                            ex.Message),
                        LocalizationService.Get("App.Name"),
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

        public void ApplyCurrentTheme()
        {
            var theme = ThemeColors.Current;
            BackColor = theme.WindowBackground;
            _toolbar.BackColor = theme.WindowBackground;
            _saveButton.BackColor = theme.CardSoft;
            _saveButton.ForeColor = theme.TextPrimary;
            _logTextBox.BackColor = theme.CardBackground;
            _logTextBox.ForeColor = theme.TextPrimary;
            Invalidate(true);
        }
    }
}
