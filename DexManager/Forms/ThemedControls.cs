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
            Font = UiFonts.Pixels(14F);
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

        private int _cornerRadius;
        public int CornerRadius { get { return _cornerRadius; } set { _cornerRadius=value; UiRendering.ClipRounded(this,value); } }
        protected override void OnSizeChanged(System.EventArgs e) { base.OnSizeChanged(e); UiRendering.ClipRounded(this,CornerRadius); }
        public bool NavigationStyle { get; set; }
        public bool DeviceNavigationStyle { get; set; }
        public bool ShowNavigationDot { get; set; }
        public bool ShowSettingsIcon { get; set; }
        public string TrailingText { get; set; }
        public string SecondaryText { get; set; }
        public Color StatusColor { get; set; }
        public UiSymbol Symbol { get; set; }
        public bool MenuCardStyle { get; set; }
        public bool ChevronUp { get; set; }
        public bool ExecutionBlocked { get; set; }
        public bool CenterIconText { get; set; }

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
            var roundedParent = Parent as RoundedPanel;
            var background = NavigationStyle
                ? BackColor
                : (roundedParent != null
                    ? roundedParent.FillColor
                    : (Parent == null ? BackColor : Parent.BackColor));
            if (background.A < 255) background = ThemeColors.Current.WindowBackground;
            DesktopSurface.PaintBehind(e.Graphics,this);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = ClientRectangle;
            bounds.Inflate(-1,-1);
            bounds.Width--;
            bounds.Height--;
            var colors = ThemeColors.Current;
            var fill = MenuCardStyle ? (Primary || _hovered ? colors.AccentSoft : colors.CardSoft) : DeviceNavigationStyle
                ? (_hovered
                    ? colors.AccentSoft
                    : (Primary ? colors.CardBackground : background))
                : NavigationStyle
                ? (Primary || _hovered ? (colors.IsDark ? Color.FromArgb(49, 53, 69) : Color.FromArgb(234, 237, 242)) : background)
                : (Primary
                    ? (_hovered ? colors.AccentHover : colors.Accent)
                    : (_hovered ? colors.AccentSoft : colors.CardSoft));
            var border = MenuCardStyle ? (Primary ? colors.Accent : colors.ControlBorder) : DeviceNavigationStyle
                ? (Primary ? colors.Accent : colors.CardBorder)
                : NavigationStyle
                ? fill
                : (Primary ? fill : colors.ControlBorder);
            var text = NavigationStyle
                ? colors.TextPrimary
                : (Primary ? Color.White : colors.TextPrimary);
            if (!Enabled || ExecutionBlocked)
            {
                fill = colors.DisabledBackground;
                border = colors.CardBorder;
                text = colors.DisabledText;
            }

            using (var path = RoundedPath(bounds, CornerRadius))
            using (Brush brush = Primary && !NavigationStyle && !MenuCardStyle && Enabled && !ExecutionBlocked
                ? (Brush)new LinearGradientBrush(bounds, colors.AccentHover, colors.Accent, 0F)
                : new SolidBrush(fill))
            using (var pen = new Pen(
                border,
                DeviceNavigationStyle && Primary ? 1.5F : 1F))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }

            if (Focused && !NavigationStyle && !MenuCardStyle)
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

            if (DeviceNavigationStyle)
            {
                DrawDeviceNavigationContent(e.Graphics, colors, text);
                return;
            }

            if (NavigationStyle && Primary && !DeviceNavigationStyle)
            {
                using (var brush = new SolidBrush(colors.Accent))
                    e.Graphics.FillRectangle(brush, 2, 9, 3, Height - 18);
            }

            if (MenuCardStyle)
            {
                DesktopSurface.DrawIcon(e.Graphics, Symbol, new Rectangle(12, (Height - 20) / 2, 20, 20), colors.Accent);
                using (var titleFont = UiFonts.Pixels(14F, true))
                using (var summaryFont = UiFonts.Pixels(13F))
                {
                    var contentWidth = Width - 56;
                    var summaryHeight = UiText.MeasureText(e.Graphics, SecondaryText ?? string.Empty,
                        summaryFont, new Size(contentWidth, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
                    summaryHeight = System.Math.Min(summaryHeight, Height - titleFont.Height - 10);
                    var top = System.Math.Max(4, (Height-titleFont.Height-summaryHeight-4)/2);
                    UiText.DrawText(e.Graphics, Text, titleFont, new Rectangle(44, top, contentWidth, titleFont.Height),
                        colors.TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    UiText.DrawText(e.Graphics, SecondaryText ?? string.Empty, summaryFont,
                        new Rectangle(44, top+titleFont.Height+4, contentWidth, summaryHeight), colors.TextSecondary,
                        TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
                }
                return;
            }
            if (CenterIconText && Symbol != UiSymbol.None && !string.IsNullOrEmpty(Text))
            {
                // Store: 20px icon + 10px gap + 17px SemiBold text, centered as one group.
                float scale = Font.SizeInPoints / 12.75F * e.Graphics.DpiX / 96F;
                int iconSize = System.Math.Max(1, (int)System.Math.Round(20F * scale));
                int gap = System.Math.Max(1, (int)System.Math.Round(10F * scale));
                var measured = UiText.Measure(e.Graphics, Text, Font, Size.Empty, TextFormatFlags.NoPrefix);
                int available = System.Math.Max(1, Width - (int)System.Math.Round(32F * scale) - iconSize - gap);
                int textWidth = System.Math.Min(available, (int)System.Math.Ceiling(measured.Width));
                int left = (Width - iconSize - gap - textWidth) / 2;
                DesktopSurface.DrawIcon(e.Graphics, Symbol,
                    new Rectangle(left, (Height - iconSize) / 2, iconSize, iconSize), text);
                UiText.DrawText(e.Graphics, Text, Font,
                    new Rectangle(left + iconSize + gap, 0, textWidth, Height), text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                return;
            }
            if (Symbol == UiSymbol.Chevron)
            {
                using (var pen = new Pen(colors.TextSecondary, 1.5F))
                { var cy=Height/2; var cx=Width/2; e.Graphics.DrawLines(pen,ChevronUp?new[]{new Point(cx-5,cy+2),new Point(cx,cy-3),new Point(cx+5,cy+2)}:new[]{new Point(cx-5,cy-2),new Point(cx,cy+3),new Point(cx+5,cy-2)}); }
            }
            else if (Symbol != UiSymbol.None)
                DesktopSurface.DrawIcon(e.Graphics, Symbol,
                    new Rectangle(string.IsNullOrEmpty(Text) ? (Width - 20) / 2 : 18, (Height - 20) / 2, 20, 20), text);
            else if (NavigationStyle && ShowNavigationDot)
            {
                var dot = new Rectangle(14, (Height - 7) / 2, 7, 7);
                using (var brush = new SolidBrush(
                    Primary ? colors.Accent : colors.TextTertiary))
                {
                    e.Graphics.FillEllipse(brush, dot);
                }
            }

            if (Symbol == UiSymbol.None && NavigationStyle && ShowSettingsIcon)
                DrawSettingsIcon(e.Graphics, text);

            if (string.IsNullOrEmpty(Text)) return;
            var leadingOffset =
                Symbol != UiSymbol.None ? 52 : ShowNavigationDot || ShowSettingsIcon ? 30 : 14;
            var trailingWidth = 0;
            if (NavigationStyle &&
                !string.IsNullOrWhiteSpace(TrailingText))
            {
                using (var trailingFont = UiFonts.Create(8.5F))
                {
                    trailingWidth = UiText.MeasureText(
                        TrailingText,
                        trailingFont).Width;
                    var trailingBounds = new Rectangle(
                        Width - trailingWidth - 13,
                        0,
                        trailingWidth,
                        Height);
                    UiText.DrawText(
                        e.Graphics,
                        TrailingText,
                        trailingFont,
                        trailingBounds,
                        Enabled ? colors.TextTertiary : colors.DisabledText,
                        TextFormatFlags.Right |
                            TextFormatFlags.VerticalCenter |
                            TextFormatFlags.EndEllipsis);
                }
            }

            var textBounds = NavigationStyle
                ? new Rectangle(
                    leadingOffset,
                    0,
                    Width - leadingOffset - 14 -
                        (trailingWidth > 0 ? trailingWidth + 8 : 0),
                    Height)
                : Symbol == UiSymbol.None ? bounds : new Rectangle(44, 0, Width - 50, Height);
            UiText.DrawText(
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

        private void DrawDeviceNavigationContent(
            Graphics graphics,
            ThemePalette colors,
            Color textColor)
        {
            var statusColor = StatusColor.IsEmpty
                ? colors.TextTertiary
                : StatusColor;
            if (!Enabled) statusColor = colors.DisabledText;

            using (var brush = new SolidBrush(statusColor))
                graphics.FillEllipse(brush, 13, Height - 20, 7, 7);

            var nameBounds = new Rectangle(13, 6, Width - 25, 23);
            UiText.DrawText(
                graphics,
                Text,
                Font,
                nameBounds,
                textColor,
                TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis |
                    TextFormatFlags.NoPrefix);

            using (var statusFont = UiFonts.Create(8.25F))
            {
                var statusBounds = new Rectangle(
                    26,
                    Height - 26,
                    Width - 38,
                    20);
                UiText.DrawText(
                    graphics,
                    SecondaryText ?? string.Empty,
                    statusFont,
                    statusBounds,
                    Enabled ? colors.TextTertiary : colors.DisabledText,
                    TextFormatFlags.Left |
                        TextFormatFlags.VerticalCenter |
                        TextFormatFlags.EndEllipsis |
                        TextFormatFlags.NoPrefix);
            }
        }

        private void DrawSettingsIcon(Graphics graphics, Color color)
        {
            var center = new PointF(18.5F, Height / 2F);
            using (var pen = new Pen(color, 1.3F)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            })
            {
                for (var index = 0; index < 8; index++)
                {
                    var angle = index * System.Math.PI / 4D;
                    var inner = new PointF(
                        center.X + (float)System.Math.Cos(angle) * 5F,
                        center.Y + (float)System.Math.Sin(angle) * 5F);
                    var outer = new PointF(
                        center.X + (float)System.Math.Cos(angle) * 7F,
                        center.Y + (float)System.Math.Sin(angle) * 7F);
                    graphics.DrawLine(pen, inner, outer);
                }
                graphics.DrawEllipse(
                    pen,
                    center.X - 5F,
                    center.Y - 5F,
                    10F,
                    10F);
                graphics.DrawEllipse(
                    pen,
                    center.X - 1.8F,
                    center.Y - 1.8F,
                    3.6F,
                    3.6F);
            }
        }

        private static GraphicsPath RoundedPath(Rectangle rectangle, int radius)
        {
            return DesktopSurface.RoundedPath(rectangle, radius);
        }
    }

    internal sealed class ThemedCheckBox : CheckBox
    {
        private bool _hovered;
        public UiSymbol Symbol { get; set; }

        public ThemedCheckBox()
        {
            AutoSize = false;
            BackColor = ThemeColors.Current.CardBackground;
            Font = UiFonts.Pixels(14F);
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
                Enabled ? (Checked ? (colors.IsDark ? Color.FromArgb(20,20,25) : Color.White) : (colors.IsDark ? colors.TextPrimary : colors.TextSecondary)) : colors.DisabledText))
            {
                e.Graphics.FillEllipse(brush, knob);
            }

            if (Symbol != UiSymbol.None)
                DesktopSurface.DrawIcon(e.Graphics, Symbol,
                    new Rectangle(1, (Height - 20) / 2, 20, 20), colors.TextPrimary);
            var offset = Symbol == UiSymbol.None ? 2 : 30;
            UiText.DrawText(
                e.Graphics,
                Text,
                Font,
                new Rectangle(offset, 0, Width - offset - 46, Height),
                Enabled ? ForeColor : colors.DisabledText,
                TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
        }

        private static GraphicsPath RoundedPath(Rectangle rectangle, int radius)
        {
            return DesktopSurface.RoundedPath(rectangle, radius);
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
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor,
                true);
        }

        private int _radius;
        public int Radius { get { return _radius; } set { _radius=value; UiRendering.ClipRounded(this,value); } }
        protected override void OnSizeChanged(System.EventArgs e) { base.OnSizeChanged(e); UiRendering.ClipRounded(this,Radius); }
        public Color FillColor { get; set; }
        public Color BorderColor { get; set; }
        public int SurfaceOpacity { get; set; } = 255;
        internal int VerticalInset { get; set; } = 1;
        public Control BackdropSource { get; set; }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            DesktopSurface.PaintBehind(e.Graphics,this);
            PaintSurface(e.Graphics);
        }
        internal void PaintSurface(Graphics graphics)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = ClientRectangle;
            bounds.Inflate(-1,-VerticalInset);
            bounds.Width--;
            bounds.Height--;
            if(bounds.Width<=0 || bounds.Height<=0)return;
            using (var path = RoundedPath(bounds, Radius))
            using (var brush = new SolidBrush(Color.FromArgb(
                System.Math.Max(0, System.Math.Min(255, SurfaceOpacity)), FillColor)))
            using (var pen = new Pen(BorderColor))
            {
                graphics.FillPath(brush, path);
                graphics.DrawPath(pen, path);
            }
        }

        private static GraphicsPath RoundedPath(Rectangle rectangle, int radius)
        {
            return DesktopSurface.RoundedPath(rectangle, radius);
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
            var bounds = new Rectangle(3, 3, Width - 7, Height - 7);
            using (var track = new Pen(ThemeColors.Current.CardBorder, System.Math.Min(Width / 8F, 6F)))
            using (var status = new Pen(StatusColor, System.Math.Min(Width / 8F, 6F)))
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

}
