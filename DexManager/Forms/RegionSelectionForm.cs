using System;
using System.Drawing;
using System.Windows.Forms;
using DexManager.Utils;

namespace DexManager.Forms
{
    public sealed class RegionSelectionForm : Form
    {
        private readonly Point _startPoint;
        private readonly Timer _pollTimer;
        private readonly Timer _timeoutTimer;
        private Rectangle _selection;

        public RegionSelectionForm(Point startPoint, int timeoutSeconds)
        {
            _startPoint = startPoint;

            Bounds = SystemInformation.VirtualScreen;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            BackColor = Color.Black;
            Opacity = 0.18;
            Cursor = Cursors.Cross;
            KeyPreview = true;

            _pollTimer = new Timer { Interval = 15 };
            _pollTimer.Tick += PollTimer_Tick;

            _timeoutTimer = new Timer
            {
                Interval = Math.Max(timeoutSeconds, 1) * 1000
            };
            _timeoutTimer.Tick += delegate
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            Shown += delegate
            {
                Activate();
                Focus();
                _pollTimer.Start();
                _timeoutTimer.Start();
            };
            KeyDown += RegionSelectionForm_KeyDown;
        }

        public Rectangle SelectedScreenRectangle
        {
            get { return _selection; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_selection.Width <= 0 || _selection.Height <= 0) return;

            var local = new Rectangle(
                _selection.X - Left,
                _selection.Y - Top,
                _selection.Width,
                _selection.Height);
            using (var fill = new SolidBrush(Color.FromArgb(65, Color.Red)))
            using (var pen = new Pen(Color.Red, 2F))
            {
                e.Graphics.FillRectangle(fill, local);
                e.Graphics.DrawRectangle(pen, local);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _pollTimer.Dispose();
                _timeoutTimer.Dispose();
            }
            base.Dispose(disposing);
        }

        private void PollTimer_Tick(object sender, EventArgs e)
        {
            var current = Cursor.Position;
            _selection = CreateRectangle(_startPoint, current);
            Invalidate();

            if ((NativeMethods.GetAsyncKeyState(NativeMethods.VkLButton) & 0x8000) == 0)
            {
                DialogResult = _selection.Width > 10 && _selection.Height > 10
                    ? DialogResult.OK
                    : DialogResult.Cancel;
                Close();
            }
        }

        private void RegionSelectionForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Escape) return;
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private static Rectangle CreateRectangle(Point first, Point second)
        {
            return new Rectangle(
                Math.Min(first.X, second.X),
                Math.Min(first.Y, second.Y),
                Math.Abs(second.X - first.X),
                Math.Abs(second.Y - first.Y));
        }
    }
}
