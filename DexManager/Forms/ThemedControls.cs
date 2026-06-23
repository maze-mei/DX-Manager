using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DexManager.Forms
{
    internal sealed class ThemedButton : Button
    {
        private bool _hovered;

        public ThemedButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            BackColor = Color.White;
            ForeColor = Color.FromArgb(55, 65, 81);
            Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer, true);
        }

        public bool Primary { get; set; }

        protected override void OnMouseEnter(System.EventArgs e)
        {
            _hovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(System.EventArgs e)
        {
            _hovered = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnEnabledChanged(System.EventArgs e)
        {
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? BackColor : Parent.BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = ClientRectangle;
            bounds.Width--;
            bounds.Height--;
            var fill = Primary
                ? (_hovered ? Color.FromArgb(29, 78, 216) : Color.FromArgb(37, 99, 235))
                : Color.White;
            var border = Primary ? fill : Color.FromArgb(209, 213, 219);
            var text = Primary ? Color.White : ForeColor;
            if (!Enabled)
            {
                fill = Color.FromArgb(243, 244, 246);
                border = Color.FromArgb(229, 231, 235);
                text = Color.FromArgb(156, 163, 175);
            }

            using (var path = RoundedPath(bounds, 7))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(border))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }

            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                bounds,
                text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis);
        }

        private static GraphicsPath RoundedPath(Rectangle rectangle, int radius)
        {
            var diameter = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class ThemedCheckBox : CheckBox
    {
        private bool _hovered;

        public ThemedCheckBox()
        {
            AutoSize = false;
            BackColor = Color.FromArgb(248, 250, 252);
            Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            ForeColor = Color.FromArgb(31, 41, 55);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque |
                ControlStyles.ResizeRedraw, true);
            CheckedChanged += delegate { Invalidate(); };
        }

        protected override void OnMouseEnter(System.EventArgs e)
        {
            _hovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(System.EventArgs e)
        {
            _hovered = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var box = new Rectangle(0, (Height - 18) / 2, 18, 18);
            var fill = Checked ? Color.FromArgb(37, 99, 235) : Color.White;
            var border = Checked
                ? Color.FromArgb(37, 99, 235)
                : (_hovered ? Color.FromArgb(156, 163, 175) : Color.FromArgb(209, 213, 219));
            if (!Enabled)
            {
                fill = Color.FromArgb(243, 244, 246);
                border = Color.FromArgb(229, 231, 235);
            }

            using (var path = RoundedPath(box, 4))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(border))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }

            if (Checked)
            {
                using (var pen = new Pen(Color.White, 2F)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                })
                {
                    e.Graphics.DrawLines(pen, new[]
                    {
                        new Point(4, box.Top + 9),
                        new Point(8, box.Top + 13),
                        new Point(15, box.Top + 5)
                    });
                }
            }

            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                new Rectangle(27, 0, Width - 27, Height),
                Enabled ? ForeColor : Color.FromArgb(156, 163, 175),
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private static GraphicsPath RoundedPath(Rectangle rectangle, int radius)
        {
            var diameter = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
