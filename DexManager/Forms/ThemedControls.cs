using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DexManager.Forms
{
    internal enum FieldChromeKind
    {
        Text,
        Combo,
        Number
    }

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
        public bool NavigationStyle { get; set; }
        public bool ShowNavigationDot { get; set; }

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
            var fill = NavigationStyle
                ? (Primary ? colors.AccentSoft : Parent.BackColor)
                : (Primary
                    ? (_hovered ? colors.AccentHover : colors.Accent)
                    : (_hovered ? colors.AccentSoft : colors.CardSoft));
            var border = NavigationStyle
                ? fill
                : (Primary ? fill : colors.ControlBorder);
            var text = NavigationStyle
                ? (Primary ? colors.TextPrimary : colors.TextSecondary)
                : (Primary ? Color.White : ForeColor);
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

            if (Focused && !NavigationStyle)
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

            if (NavigationStyle && ShowNavigationDot)
            {
                var dot = new Rectangle(14, (Height - 7) / 2, 7, 7);
                using (var brush = new SolidBrush(
                    Primary ? colors.Accent : colors.TextTertiary))
                {
                    e.Graphics.FillEllipse(brush, dot);
                }
            }

            var textBounds = NavigationStyle
                ? new Rectangle(
                    ShowNavigationDot ? 30 : 14,
                    0,
                    Width - (ShowNavigationDot ? 38 : 28),
                    Height)
                : bounds;
            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                textBounds,
                text,
                NavigationStyle
                    ? TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                        TextFormatFlags.EndEllipsis
                    : TextFormatFlags.HorizontalCenter |
                        TextFormatFlags.VerticalCenter |
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
        private bool _complete;

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

        public bool Complete
        {
            get { return _complete; }
            set
            {
                _complete = value;
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
                if (Complete)
                    e.Graphics.DrawEllipse(status, bounds);
                else
                    e.Graphics.DrawArc(status, bounds, -90, 140);
            }
        }
    }

    internal sealed class ThemedFieldHost : UserControl
    {
        private readonly Control _editor;
        private readonly FieldChromeKind _kind;
        private readonly FieldGlyph _glyph;
        private bool _focused;

        public ThemedFieldHost(Control editor, FieldChromeKind kind)
        {
            _editor = editor;
            _kind = kind;
            TabStop = false;
            BackColor = ThemeColors.Current.CardSoft;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw,
                true);

            ConfigureEditor();
            Controls.Add(_editor);
            if (_kind != FieldChromeKind.Text)
            {
                _glyph = new FieldGlyph(_editor, _kind);
                Controls.Add(_glyph);
                _glyph.BringToFront();
            }

            _editor.Enter += delegate
            {
                _focused = true;
                Invalidate();
            };
            _editor.Leave += delegate
            {
                _focused = false;
                Invalidate();
            };
            Size = new Size(200, 32);
        }

        protected override void OnResize(System.EventArgs e)
        {
            base.OnResize(e);
            LayoutChildren();
        }

        protected override void OnEnabledChanged(System.EventArgs e)
        {
            _editor.Enabled = Enabled;
            if (_glyph != null) _glyph.Enabled = Enabled;
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var colors = ThemeColors.Current;
            e.Graphics.Clear(colors.CardBackground);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = ClientRectangle;
            bounds.Width--;
            bounds.Height--;
            var fill = Enabled
                ? colors.CardSoft
                : colors.DisabledBackground;
            var border = _focused
                ? colors.Accent
                : colors.ControlBorder;
            using (var path = RoundedPath(bounds, 6))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(border))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }
        }

        private void ConfigureEditor()
        {
            var colors = ThemeColors.Current;
            _editor.BackColor = colors.CardSoft;
            _editor.ForeColor = colors.TextPrimary;

            var textBox = _editor as TextBox;
            if (textBox != null)
            {
                textBox.BorderStyle = BorderStyle.None;
                textBox.AutoSize = false;
            }

            var combo = _editor as ComboBox;
            if (combo != null)
                combo.FlatStyle = FlatStyle.Flat;

            var number = _editor as NumericUpDown;
            if (number != null)
            {
                number.BorderStyle = BorderStyle.None;
                number.TextAlign = HorizontalAlignment.Left;
                if (number.Controls.Count > 0)
                    number.Controls[0].Visible = false;
            }
        }

        private void LayoutChildren()
        {
            var glyphWidth = _kind == FieldChromeKind.Text ? 0 : 30;
            var editorHeight = _kind == FieldChromeKind.Text ? 22 : 25;
            _editor.Location = new Point(10, (Height - editorHeight) / 2);
            _editor.Size = new Size(
                System.Math.Max(
                    Width - (_kind == FieldChromeKind.Text ? 20 : 16),
                    1),
                editorHeight);
            if (_glyph != null)
            {
                _glyph.Location = new Point(Width - glyphWidth - 2, 2);
                _glyph.Size = new Size(glyphWidth, Height - 4);
                _glyph.BringToFront();
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

    internal sealed class FieldGlyph : Control
    {
        private readonly Control _editor;
        private readonly FieldChromeKind _kind;

        public FieldGlyph(Control editor, FieldChromeKind kind)
        {
            _editor = editor;
            _kind = kind;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!Enabled) return;

            var combo = _editor as ComboBox;
            if (combo != null)
            {
                combo.Focus();
                combo.DroppedDown = true;
                return;
            }

            var number = _editor as NumericUpDown;
            if (number == null) return;
            number.Focus();
            if (e.Y < Height / 2)
                number.Value = System.Math.Min(
                    number.Maximum,
                    number.Value + number.Increment);
            else
                number.Value = System.Math.Max(
                    number.Minimum,
                    number.Value - number.Increment);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var color = Enabled
                ? ThemeColors.Current.TextTertiary
                : ThemeColors.Current.DisabledText;
            using (var pen = new Pen(color, 1.4F)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            })
            {
                if (_kind == FieldChromeKind.Combo)
                {
                    DrawChevron(e.Graphics, pen, Width / 2, Height / 2, false);
                }
                else
                {
                    DrawChevron(e.Graphics, pen, Width / 2, Height / 2 - 5, true);
                    DrawChevron(e.Graphics, pen, Width / 2, Height / 2 + 5, false);
                }
            }
        }

        private static void DrawChevron(
            Graphics graphics,
            Pen pen,
            int centerX,
            int centerY,
            bool up)
        {
            var direction = up ? -1 : 1;
            graphics.DrawLines(pen, new[]
            {
                new Point(centerX - 3, centerY - direction),
                new Point(centerX, centerY + (2 * direction)),
                new Point(centerX + 3, centerY - direction)
            });
        }
    }
}
