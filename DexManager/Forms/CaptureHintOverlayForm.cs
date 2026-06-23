using System;
using System.Drawing;
using System.Windows.Forms;

namespace DexManager.Forms
{
    public sealed class CaptureHintOverlayForm : Form
    {
        private readonly Timer _followTimer;

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

            var label = new Label
            {
                AutoSize = true,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = new Font("Malgun Gothic", 9F),
                Text =
                    "F8 다시 누름: Scrcpy 전체 캡처\r\n" +
                    "마우스 드래그: 선택 영역 캡처\r\n" +
                    "ESC: 취소"
            };
            Controls.Add(label);

            _followTimer = new Timer { Interval = 80 };
            _followTimer.Tick += delegate { MoveNearCursor(); };

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
                _followTimer.Dispose();
            base.Dispose(disposing);
        }

        public void ShowHint()
        {
            MoveNearCursor();
            if (!Visible) Show();
        }

        public void HideHint()
        {
            _followTimer.Stop();
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
