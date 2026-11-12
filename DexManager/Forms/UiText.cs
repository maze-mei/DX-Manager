using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace DexManager.Forms
{
    // Keep the verified F2 glyph shape on every supported Windows version.
    internal static class UiText
    {
        public static void Configure(Graphics graphics)
        { graphics.TextRenderingHint = TextRenderingHint.AntiAlias; }
        private static StringFormat Format(TextFormatFlags flags)
        {
            var format = new StringFormat(StringFormat.GenericDefault);
            format.Alignment = (flags & TextFormatFlags.HorizontalCenter) != 0 ? StringAlignment.Center
                : (flags & TextFormatFlags.Right) != 0 ? StringAlignment.Far : StringAlignment.Near;
            format.LineAlignment = (flags & TextFormatFlags.VerticalCenter) != 0 ? StringAlignment.Center
                : (flags & TextFormatFlags.Bottom) != 0 ? StringAlignment.Far : StringAlignment.Near;
            if ((flags & TextFormatFlags.WordBreak) == 0) format.FormatFlags |= StringFormatFlags.NoWrap;
            if ((flags & TextFormatFlags.RightToLeft) != 0) format.FormatFlags |= StringFormatFlags.DirectionRightToLeft;
            format.Trimming = (flags & TextFormatFlags.PathEllipsis) != 0 ? StringTrimming.EllipsisPath
                : (flags & TextFormatFlags.EndEllipsis) != 0 ? StringTrimming.EllipsisCharacter
                : (flags & TextFormatFlags.WordEllipsis) != 0 ? StringTrimming.EllipsisWord : StringTrimming.None;
            format.HotkeyPrefix = (flags & TextFormatFlags.NoPrefix) != 0 ? HotkeyPrefix.None : HotkeyPrefix.Show;
            return format;
        }
        public static void DrawText(Graphics graphics, string text, Font font, Rectangle bounds, Color color, TextFormatFlags flags)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0 || string.IsNullOrEmpty(text)) return;
            var state = graphics.Save();
            try {
                Configure(graphics); graphics.SetClip(bounds, CombineMode.Intersect);
                using (var format = Format(flags)) using (var brush = new SolidBrush(color)) graphics.DrawString(text, font, brush, bounds, format);
            } finally { graphics.Restore(state); }
        }
        public static void DrawText(Graphics graphics, string text, Font font, Rectangle bounds, Color color, Color background, TextFormatFlags flags)
        {
            using (var brush = new SolidBrush(background)) graphics.FillRectangle(brush, bounds);
            DrawText(graphics, text, font, bounds, color, flags);
        }
        public static Size MeasureText(string text, Font font) { return MeasureText(text, font, Size.Empty, TextFormatFlags.Default); }
        public static Size MeasureText(string text, Font font, Size proposed, TextFormatFlags flags)
        {
            using (var graphics = Graphics.FromHwnd(IntPtr.Zero)) return MeasureText(graphics, text, font, proposed, flags);
        }
        public static Size MeasureText(Graphics graphics, string text, Font font, Size proposed, TextFormatFlags flags)
        { return Size.Ceiling(Measure(graphics, text, font, proposed, flags)); }
        public static SizeF Measure(Graphics graphics, string text, Font font, Size proposed, TextFormatFlags flags)
        {
            if (string.IsNullOrEmpty(text)) return new SizeF(0, font.GetHeight(graphics));
            var state = graphics.Save();
            try {
                Configure(graphics);
                using (var format = Format(flags)) return graphics.MeasureString(text, font,
                    new SizeF(proposed.Width > 0 ? proposed.Width : 100000, proposed.Height > 0 ? proposed.Height : 100000), format);
            } finally { graphics.Restore(state); }
        }
        internal static TextFormatFlags Alignment(ContentAlignment alignment)
        {
            var flags = TextFormatFlags.WordBreak;
            if (alignment == ContentAlignment.TopCenter || alignment == ContentAlignment.MiddleCenter || alignment == ContentAlignment.BottomCenter) flags |= TextFormatFlags.HorizontalCenter;
            if (alignment == ContentAlignment.TopRight || alignment == ContentAlignment.MiddleRight || alignment == ContentAlignment.BottomRight) flags |= TextFormatFlags.Right;
            if (alignment == ContentAlignment.MiddleLeft || alignment == ContentAlignment.MiddleCenter || alignment == ContentAlignment.MiddleRight) flags |= TextFormatFlags.VerticalCenter;
            if (alignment == ContentAlignment.BottomLeft || alignment == ContentAlignment.BottomCenter || alignment == ContentAlignment.BottomRight) flags |= TextFormatFlags.Bottom;
            return flags;
        }
    }
    internal sealed class UiLabel : Label
    {
        public UiLabel() { UseCompatibleTextRendering = true; }
        public override Size GetPreferredSize(Size proposedSize)
        {
            var width = MaximumSize.Width > 0 ? MaximumSize.Width : proposedSize.Width;
            if (width > 0) width = Math.Max(1, width - Padding.Horizontal);
            var flags = TextFormatFlags.WordBreak | (UseMnemonic ? TextFormatFlags.Default : TextFormatFlags.NoPrefix);
            var measured = UiText.MeasureText(Text, Font, new Size(width, 0), flags);
            return new Size(Math.Max(MinimumSize.Width, measured.Width + Padding.Horizontal), Math.Max(MinimumSize.Height, measured.Height + Padding.Vertical));
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var bounds = new Rectangle(Padding.Left, Padding.Top, Math.Max(0, Width - Padding.Horizontal), Math.Max(0, Height - Padding.Vertical));
            var flags = UiText.Alignment(TextAlign);
            if (!UseMnemonic) flags |= TextFormatFlags.NoPrefix;
            if (AutoEllipsis) flags |= TextFormatFlags.EndEllipsis;
            if (RightToLeft == RightToLeft.Yes) flags |= TextFormatFlags.RightToLeft;
            UiText.DrawText(e.Graphics, Text, Font, bounds, ForeColor, flags);
        }
    }
    // Standard link hit testing, keyboard operation and accessibility remain native.
    internal sealed class UiLinkLabel : LinkLabel
    {
        public UiLinkLabel() { UseCompatibleTextRendering = true; }
        protected override void OnPaint(PaintEventArgs e) { UiText.Configure(e.Graphics); base.OnPaint(e); }
    }
    internal sealed class UiRadioButton : RadioButton
    {
        public UiRadioButton() { UseCompatibleTextRendering = true; }
        protected override void OnPaint(PaintEventArgs e) { UiText.Configure(e.Graphics); base.OnPaint(e); }
    }
    internal sealed class UiCheckBox : CheckBox
    {
        public UiCheckBox() { UseCompatibleTextRendering = true; }
        protected override void OnPaint(PaintEventArgs e) { UiText.Configure(e.Graphics); base.OnPaint(e); }
    }
}
