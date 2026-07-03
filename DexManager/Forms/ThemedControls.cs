using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DexManager.Forms
{
    internal sealed class ThemedButton : Button
    {
        private bool _hovered;
        private bool _primary;

        public ThemedButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            BackColor = ThemeColors.Current.CardBackground;
            ForeColor = ThemeColors.Current.TextSecondary;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            CornerRadius = 7;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer, true);
        }

        public bool Primary
        {
            get { return _primary; }
            set
            {
                _primary = value;
                Invalidate();
            }
        }

        public int CornerRadius { get; set; }

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

        protected override void OnEnter(System.EventArgs e)
        {
            Invalidate();
            base.OnEnter(e);
        }

        protected override void OnLeave(System.EventArgs e)
        {
            Invalidate();
            base.OnLeave(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? BackColor : Parent.BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = ClientRectangle;
            bounds.Width--;
            bounds.Height--;
            var colors = ThemeColors.Current;
            var fill = Primary
                ? (_hovered ? colors.AccentHover : colors.Accent)
                : (_hovered ? colors.AccentSoft : colors.CardSoft);
            var border = Primary ? fill : colors.ControlBorder;
            var text = Primary ? Color.White : ForeColor;
            if (!Enabled)
            {
                fill = colors.DisabledBackground;
                border = colors.CardBorder;
                text = colors.DisabledText;
            }

            using (var path = RoundedPath(bounds, CornerRadius))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(border))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }

            if (Focused)
            {
                var focusBounds = bounds;
                focusBounds.Inflate(-2, -2);
                using (var path = RoundedPath(
                    focusBounds,
                    System.Math.Max(CornerRadius - 2, 2)))
                using (var pen = new Pen(
                    Primary ? Color.White : colors.Accent,
                    1.5F))
                {
                    e.Graphics.DrawPath(pen, path);
                }
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
            BackColor = ThemeColors.Current.CardBackground;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            ForeColor = ThemeColors.Current.TextPrimary;
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

        protected override void OnEnter(System.EventArgs e)
        {
            Invalidate();
            base.OnEnter(e);
        }

        protected override void OnLeave(System.EventArgs e)
        {
            Invalidate();
            base.OnLeave(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var colors = ThemeColors.Current;
            if (Focused)
            {
                var focusBounds = ClientRectangle;
                focusBounds.Width--;
                focusBounds.Height--;
                using (var path = RoundedPath(focusBounds, 5))
                using (var brush = new SolidBrush(colors.AccentSoft))
                using (var pen = new Pen(colors.Accent))
                {
                    e.Graphics.FillPath(brush, path);
                    e.Graphics.DrawPath(pen, path);
                }
            }

            var toggle = new Rectangle(Width - 42, (Height - 20) / 2, 38, 20);
            var fill = Checked
                ? (_hovered ? colors.AccentHover : colors.Accent)
                : (_hovered ? colors.ControlBorder : colors.CardBorder);
            var border = Checked ? fill : colors.ControlBorder;
            if (!Enabled)
            {
                fill = colors.DisabledBackground;
                border = colors.CardBorder;
            }

            using (var path = RoundedPath(toggle, 10))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(border))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }

            var knob = new Rectangle(
                Checked ? toggle.Right - 17 : toggle.Left + 3,
                toggle.Top + 3,
                14,
                14);
            using (var brush = new SolidBrush(
                Enabled ? Color.White : colors.DisabledText))
            {
                e.Graphics.FillEllipse(brush, knob);
            }

            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                new Rectangle(2, 0, Width - 52, Height),
                Enabled ? ForeColor : colors.DisabledText,
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

    internal sealed class RoundedPanel : Panel
    {
        public RoundedPanel()
        {
            DoubleBuffered = true;
            Radius = 14;
            FillColor = ThemeColors.Current.CardBackground;
            BorderColor = ThemeColors.Current.CardBorder;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw,
                true);
        }

        public int Radius { get; set; }
        public Color FillColor { get; set; }
        public Color BorderColor { get; set; }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? BackColor : Parent.BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = ClientRectangle;
            bounds.Width--;
            bounds.Height--;
            using (var path = RoundedPath(bounds, Radius))
            using (var brush = new SolidBrush(FillColor))
            using (var pen = new Pen(BorderColor))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }
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

    internal sealed class StatusRing : Control
    {
        private Color _statusColor = Color.DarkOrange;

        public StatusRing()
        {
            Size = new Size(40, 40);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        public Color StatusColor
        {
            get { return _statusColor; }
            set
            {
                _statusColor = value;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new Rectangle(5, 5, Width - 11, Height - 11);
            using (var track = new Pen(ThemeColors.Current.CardBorder, 6F))
            using (var status = new Pen(StatusColor, 6F))
            {
                track.StartCap = track.EndCap = LineCap.Round;
                status.StartCap = status.EndCap = LineCap.Round;
                e.Graphics.DrawEllipse(track, bounds);
                e.Graphics.DrawArc(status, bounds, -90, 140);
            }
        }
    }
}
