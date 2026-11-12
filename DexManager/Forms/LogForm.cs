using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using DexManager.Services;
using DexManager.Utils;

namespace DexManager.Forms
{
    public sealed class LogForm : Form
    {
        private readonly LogService _logService;
        private readonly RichTextBox _logTextBox;
        private readonly Panel _toolbar;
        private readonly Button _saveButton;
        private string _lastDisplayedMessage,_lastDisplayedLevel;
        private LogEntryKind _lastDisplayedKind;
        private sealed class ColorSpan { public int Start,Length; public LogEventArgs Entry; }
        private readonly System.Collections.Generic.List<ColorSpan> _colorSpans=new System.Collections.Generic.List<ColorSpan>();
        private readonly RoundedPanel _logSurface;
        private readonly Label _heading, _description, _footer;

        public LogForm(LogService logService)
        {
            _logService = logService;

            Text = LocalizationService.Get("Log.Title");
            Icon = AppIconProvider.Current;
            StartPosition = FormStartPosition.CenterParent;
            Font = UiFonts.Pixels(14F);
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            UiWindowStyle.ApplyStoreSize(this, 1040, 680);

            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimumSize = new Size(760, 480);
            _logTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.White,
                Font = new Font("Consolas", 13F * 0.75F),
                WordWrap = false
            };

            _saveButton = new ThemedButton
            {
                Text = LocalizationService.Get("Log.Save"),
                Size = new Size(110, 40),
                Margin = new Padding(4, 2, 4, 2)
            };
            _saveButton.Click += SaveButton_Click;

            _toolbar = new BufferedPanel
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Size = new Size(256, 52),
                Padding = Padding.Empty
            };
            _saveButton.Bounds = new Rectangle(136, 4, 110, 40);
            _toolbar.Controls.Add(_saveButton);
            var bottom = new ThemedButton { Text = LocalizationService.Get("Desktop.LogBottom"), Size = new Size(110, 40), Margin = new Padding(4, 2, 4, 2) };
            bottom.Click += delegate { MoveToEnd(); };
            bottom.Bounds = new Rectangle(16, 4, 110, 40);
            _toolbar.Controls.Add(bottom);

            _heading = new UiLabel { Text = LocalizationService.Get("Log.Title"), Font = UiFonts.Pixels(24F, true), BackColor = Color.Transparent };
            _description = new UiLabel { Text = LocalizationService.Get("Desktop.LogSubtitle"), BackColor = Color.Transparent };
            _footer = new UiLabel { Text = LocalizationService.Get("Desktop.LogFooter"), BackColor = Color.Transparent };
            _logSurface = new RoundedPanel { Radius = 10, Padding = new Padding(12), BackColor = Color.Transparent };
            _logTextBox.BorderStyle = BorderStyle.None;
            _logSurface.Controls.Add(_logTextBox);
            ThemedScrollBar.Attach(_logTextBox);
            Controls.AddRange(new Control[] { _logSurface, _heading, _description, _footer });
            Resize += delegate { LayoutLog(); };
            LayoutLog();
            Controls.Add(_toolbar);
            _toolbar.BringToFront();
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
            foreach (var entry in _logService.GetSessionEvents())
                AppendIfNew(entry);
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
                InitialDirectory = Directory.Exists(_logService.LogDirectory)
                    ? _logService.LogDirectory
                    : string.Empty,
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

            try
            {
                BeginInvoke((Action)delegate
                {
                    if (IsDisposed) return;
                    AppendIfNew(e);
                    MoveToEnd();
                });
            }
            catch (InvalidOperationException)
            {
                // The form handle was destroyed after the checks above.
            }
        }

        private void AppendIfNew(LogEventArgs entry)
        {
            var line=entry.Message;
            var comparable = GetComparableMessage(line);
            if (string.Equals(
                comparable,
                _lastDisplayedMessage,
                StringComparison.Ordinal) && _lastDisplayedKind==entry.Kind && _lastDisplayedLevel==entry.Level)
            {
                return;
            }

            _lastDisplayedMessage = comparable;_lastDisplayedKind=entry.Kind;_lastDisplayedLevel=entry.Level;
            int start=_logTextBox.TextLength;
            _logTextBox.Select(start,0);
            _logTextBox.SelectionColor=EntryColor(entry);
            _logTextBox.AppendText(line + Environment.NewLine);
            _colorSpans.Add(new ColorSpan {Start=start,Length=_logTextBox.TextLength-start,Entry=entry});
        }

        private static Color EntryColor(LogEventArgs entry) {
            bool dark=ThemeColors.Current.IsDark;
            if(entry.Kind==LogEntryKind.ToolOutput)return ThemeColors.Current.TextPrimary;
            if(entry.Level=="ERROR")return dark?Color.FromArgb(255,107,129):Color.FromArgb(176,0,32);
            if(entry.Level=="WARN")return dark?Color.FromArgb(255,179,71):Color.FromArgb(150,53,0);
            if(entry.Kind==LogEntryKind.Progress)return dark?Color.FromArgb(188,140,255):Color.FromArgb(89,0,168);
            return ThemeColors.Current.TextPrimary;
        }
        private void RefreshEntryColors() {
            int start=_logTextBox.SelectionStart,length=_logTextBox.SelectionLength;
            foreach(var span in _colorSpans){_logTextBox.Select(span.Start,span.Length);_logTextBox.SelectionColor=EntryColor(span.Entry);}
            _logTextBox.Select(Math.Min(start,_logTextBox.TextLength),Math.Min(length,Math.Max(0,_logTextBox.TextLength-start)));
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

        protected override void OnPaintBackground(PaintEventArgs e) { DesktopSurface.Paint(e.Graphics, ClientRectangle); }
        private void LayoutLog()
        {
            var s = Math.Max(1F, CurrentAutoScaleDimensions.Width / 96F);
            _heading.Bounds = new Rectangle((int)(22*s), (int)(18*s), (int)(600*s), (int)(38*s));
            _toolbar.Bounds = new Rectangle(ClientSize.Width - (int)(280*s), (int)(37*s), (int)(256*s), (int)(52*s));
            _description.Bounds = new Rectangle((int)(22*s), (int)(57*s), Math.Max(100,_toolbar.Left-(int)(38*s)), (int)(24*s));
            _description.TextAlign = ContentAlignment.BottomLeft;
            _logSurface.Bounds = new Rectangle((int)(22*s), (int)(90*s), ClientSize.Width - (int)(44*s), ClientSize.Height - (int)(134*s));
            _footer.Bounds = new Rectangle((int)(22*s), ClientSize.Height - (int)(34*s), ClientSize.Width - (int)(44*s), (int)(24*s));
        }
        public void ApplyCurrentTheme()
        {
            var theme = ThemeColors.Current;
            BackColor = theme.WindowBackground;
            _toolbar.BackColor = Color.Transparent;
            _heading.ForeColor = theme.TextPrimary;
            _description.ForeColor = theme.TextTertiary;
            _footer.ForeColor = theme.TextTertiary;
            _logSurface.FillColor = theme.CardBackground;
            _logSurface.BorderColor = theme.CardBorder;
            _saveButton.BackColor = theme.CardSoft;
            _saveButton.ForeColor = theme.TextPrimary;
            _logTextBox.BackColor = theme.CardBackground;
            _logTextBox.ForeColor = theme.TextPrimary;
            RefreshEntryColors();
            ThemedScrollBar.RefreshFor(_logTextBox);
            Invalidate(true);
        }
    }
}
