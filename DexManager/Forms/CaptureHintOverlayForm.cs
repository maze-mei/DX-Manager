using System;
using System.Drawing;
using System.Windows.Forms;
using DexManager.Services;

namespace DexManager.Forms
{
    public sealed class CaptureHintOverlayForm : Form
    {
        private readonly Timer _followTimer;
        private readonly Timer _autoHideTimer;
        private readonly Label _messageLabel;

        public CaptureHintOverlayForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(40, 40, 40);
            ForeColor = Color.White;
            Padding = new Padding(10);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            _messageLabel = new Label
            {
                AutoSize = true,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = new Font("Malgun Gothic", 9F),
                Text = LocalizationService.Get("Capture.Hint")
            };
            Controls.Add(_messageLabel);

            _followTimer = new Timer { Interval = 80 };
            _followTimer.Tick += delegate { MoveNearCursor(); };
            _autoHideTimer = new Timer();
            _autoHideTimer.Tick += delegate { HideHint(); };

            Shown += delegate
            {
                MoveNearCursor();
                _followTimer.Start();
            };
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                const int wsExNoActivate = 0x08000000;
                var parameters = base.CreateParams;
                parameters.ExStyle |= wsExNoActivate;
                return parameters;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _followTimer.Dispose();
                _autoHideTimer.Dispose();
            }
            base.Dispose(disposing);
        }

        public void ShowHint()
        {
            _autoHideTimer.Stop();
            BackColor = Color.FromArgb(40, 40, 40);
            _messageLabel.Text = LocalizationService.Get("Capture.Hint");
            MoveNearCursor();
            if (!Visible) Show();
        }

        public void ShowMessage(string message, int durationMs)
        {
            _autoHideTimer.Stop();
            BackColor = Color.FromArgb(22, 101, 52);
            _messageLabel.Text = message;
            MoveNearCursor();
            if (!Visible) Show();
            _followTimer.Start();
            _autoHideTimer.Interval = Math.Max(durationMs, 500);
            _autoHideTimer.Start();
        }

        public void HideHint()
        {
            _followTimer.Stop();
            _autoHideTimer.Stop();
            Hide();
        }

        private void MoveNearCursor()
        {
            var screen = Screen.FromPoint(Cursor.Position).WorkingArea;
            var x = Cursor.Position.X + 16;
            var y = Cursor.Position.Y + 20;

            if (x + Width > screen.Right) x = Cursor.Position.X - Width - 16;
            if (y + Height > screen.Bottom) y = Cursor.Position.Y - Height - 16;
            if (x < screen.Left) x = screen.Left;
            if (y < screen.Top) y = screen.Top;

            Location = new Point(x, y);
        }
    }
}
