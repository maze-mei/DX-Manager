using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using DexManager.Utils;

namespace DexManager.Forms
{
    internal sealed class ThemedSelectControl : Control
    {
        private int _selectedIndex = -1;
        private ThemedDropDownForm _dropDown;

        public ThemedSelectControl()
        {
            Items = new List<object>();
            TabStop = true;
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 9.5F);
            Size = new Size(200, 32);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                ControlStyles.Selectable, true);
        }

        public IList<object> Items { get; private set; }

        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set
            {
                var normalized = value >= 0 && value < Items.Count ? value : -1;
                if (_selectedIndex == normalized) return;
                _selectedIndex = normalized;
                Invalidate();
                OnSelectedIndexChanged(EventArgs.Empty);
            }
        }

        public object SelectedItem
        {
            get
            {
                return _selectedIndex >= 0 && _selectedIndex < Items.Count
                    ? Items[_selectedIndex]
                    : null;
            }
            set
            {
                var index = -1;
                for (var i = 0; i < Items.Count; i++)
                {
                    if (ReferenceEquals(Items[i], value) ||
                        Equals(Items[i], value))
                    {
                        index = i;
                        break;
                    }
                }
                SelectedIndex = index;
            }
        }

        public event EventHandler SelectedIndexChanged;
        public event EventHandler SelectionChangeCommitted;

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!Enabled || e.Button != MouseButtons.Left) return;
            Focus();
            ShowDropDown();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (!Enabled)
            {
                base.OnKeyDown(e);
                return;
            }

            if (e.KeyCode == Keys.Enter ||
                e.KeyCode == Keys.Space ||
                (e.Alt && e.KeyCode == Keys.Down))
            {
                ShowDropDown();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Down && Items.Count > 0)
            {
                CommitSelection(Math.Min(Items.Count - 1, SelectedIndex + 1));
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Up && Items.Count > 0)
            {
                CommitSelection(Math.Max(0, SelectedIndex - 1));
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            var key = keyData & Keys.KeyCode;
            if (key == Keys.Up ||
                key == Keys.Down ||
                key == Keys.Enter ||
                key == Keys.Space)
            {
                return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnEnter(EventArgs e)
        {
            Invalidate();
            base.OnEnter(e);
        }

        protected override void OnLeave(EventArgs e)
        {
            Invalidate();
            base.OnLeave(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var colors = ThemeColors.Current;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            DrawFieldBackground(e.Graphics, ClientRectangle, Focused, Enabled);

            var text = SelectedItem == null
                ? string.Empty
                : SelectedItem.ToString();
            TextRenderer.DrawText(
                e.Graphics,
                text,
                Font,
                new Rectangle(10, 0, Math.Max(Width - 42, 0), Height),
                Enabled ? colors.TextPrimary : colors.DisabledText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            using (var pen = new Pen(
                Enabled ? colors.TextTertiary : colors.DisabledText,
                1.4F)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            })
            {
                var centerX = Width - 17;
                var centerY = Height / 2;
                e.Graphics.DrawLines(pen, new[]
                {
                    new Point(centerX - 3, centerY - 1),
                    new Point(centerX, centerY + 2),
                    new Point(centerX + 3, centerY - 1)
                });
            }
        }

        private void ShowDropDown()
        {
            if (Items.Count == 0) return;
            if (_dropDown != null && !_dropDown.IsDisposed)
            {
                _dropDown.Close();
                return;
            }

            _dropDown = new ThemedDropDownForm(
                Items,
                SelectedIndex,
                Width);
            _dropDown.ItemSelected += delegate(int index)
            {
                CommitSelection(index);
            };
            _dropDown.FormClosed += delegate
            {
                _dropDown = null;
                if (!IsDisposed && IsHandleCreated)
                    BeginInvoke((Action)delegate { Focus(); });
            };
            var screenPoint = PointToScreen(new Point(0, Height + 3));
            var workingArea = Screen.FromControl(this).WorkingArea;
            if (screenPoint.Y + _dropDown.Height > workingArea.Bottom)
                screenPoint.Y = PointToScreen(Point.Empty).Y -
                    _dropDown.Height - 3;
            _dropDown.Location = screenPoint;
            _dropDown.Show(FindForm());
        }

        private void CommitSelection(int index)
        {
            SelectedIndex = index;
            var handler = SelectionChangeCommitted;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void OnSelectedIndexChanged(EventArgs e)
        {
            var handler = SelectedIndexChanged;
            if (handler != null) handler(this, e);
        }

        internal static void DrawFieldBackground(
            Graphics graphics,
            Rectangle rectangle,
            bool focused,
            bool enabled)
        {
            var colors = ThemeColors.Current;
            var bounds = rectangle;
            bounds.Width--;
            bounds.Height--;
            var fill = enabled ? colors.CardSoft : colors.DisabledBackground;
            var border = focused ? colors.Accent : colors.ControlBorder;
            using (var path = RoundedPath(bounds, 6))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(border))
            {
                graphics.FillPath(brush, path);
                graphics.DrawPath(pen, path);
            }
        }

        internal static GraphicsPath RoundedPath(Rectangle rectangle, int radius)
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

    internal class ThemedTextControl : Control
    {
        private int _selectionStart;
        private int _selectionLength;

        public ThemedTextControl()
        {
            TabStop = true;
            Cursor = Cursors.IBeam;
            Font = new Font("Segoe UI", 9.5F);
            Size = new Size(200, 32);
            MaxLength = 256;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                ControlStyles.Selectable, true);
        }

        public int MaxLength { get; set; }
        public bool UsePasswordMask { get; set; }
        public bool UseMiddleEllipsis { get; set; }

        public void SelectAll()
        {
            _selectionStart = 0;
            _selectionLength = Text.Length;
            Invalidate();
        }

        public void Clear()
        {
            Text = string.Empty;
        }

        protected override void OnTextChanged(EventArgs e)
        {
            _selectionStart = Math.Min(_selectionStart, Text.Length);
            _selectionLength = Math.Min(
                _selectionLength,
                Text.Length - _selectionStart);
            Invalidate();
            base.OnTextChanged(e);
        }

        protected override void OnEnter(EventArgs e)
        {
            SelectAll();
            base.OnEnter(e);
        }

        protected override void OnLeave(EventArgs e)
        {
            _selectionLength = 0;
            Invalidate();
            base.OnLeave(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!Enabled || e.Button != MouseButtons.Left) return;
            var alreadyFocused = Focused;
            Focus();
            if (!alreadyFocused)
            {
                SelectAll();
                return;
            }
            _selectionStart = FindCharacterIndex(e.X - 10);
            _selectionLength = 0;
            Invalidate();
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            if (!Enabled || char.IsControl(e.KeyChar))
            {
                base.OnKeyPress(e);
                return;
            }
            if (!AcceptCharacter(e.KeyChar) ||
                Text.Length - _selectionLength >= MaxLength)
            {
                e.Handled = true;
                return;
            }
            ReplaceSelection(e.KeyChar.ToString());
            e.Handled = true;
            base.OnKeyPress(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (!Enabled)
            {
                base.OnKeyDown(e);
                return;
            }

            if (e.Control && e.KeyCode == Keys.A)
            {
                SelectAll();
                e.SuppressKeyPress = true;
            }
            else if (e.Control && e.KeyCode == Keys.C)
            {
                CopySelection();
                e.SuppressKeyPress = true;
            }
            else if (e.Control && e.KeyCode == Keys.V)
            {
                PasteText();
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Back)
            {
                Backspace();
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Delete)
            {
                Delete();
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Left)
            {
                MoveCaret(-1);
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Right)
            {
                MoveCaret(1);
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Home)
            {
                _selectionStart = 0;
                _selectionLength = 0;
                Invalidate();
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.End)
            {
                _selectionStart = Text.Length;
                _selectionLength = 0;
                Invalidate();
                e.SuppressKeyPress = true;
            }
            base.OnKeyDown(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            var key = keyData & Keys.KeyCode;
            if (key == Keys.Left ||
                key == Keys.Right ||
                key == Keys.Up ||
                key == Keys.Down ||
                key == Keys.Home ||
                key == Keys.End ||
                key == Keys.Delete)
            {
                return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var colors = ThemeColors.Current;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            ThemedSelectControl.DrawFieldBackground(
                e.Graphics,
                ClientRectangle,
                Focused,
                Enabled);

            var value = Text ?? string.Empty;
            var displayValue = UsePasswordMask
                ? new string('\u2022', value.Length)
                : value;
            var selectionStart = Math.Max(
                0,
                Math.Min(_selectionStart, value.Length));
            var selectionLength = Math.Max(
                0,
                Math.Min(_selectionLength, value.Length - selectionStart));
            var textY = (Height - Font.Height) / 2;
            var textColor = Enabled ? colors.TextPrimary : colors.DisabledText;
            using (var format = CreateTextFormat())
            {
                if (UseMiddleEllipsis && !Focused)
                {
                    displayValue = FitMiddleEllipsis(
                        e.Graphics,
                        displayValue,
                        Math.Max(Width - 20, 0),
                        format);
                }
                DrawText(
                    e.Graphics,
                    displayValue,
                    10,
                    textY,
                    textColor,
                    format);

                if (Focused && selectionLength > 0)
                {
                    var prefix = displayValue.Substring(0, selectionStart);
                    var selected = displayValue.Substring(
                        selectionStart,
                        selectionLength);
                    var selectionX = 10F + MeasureText(
                        e.Graphics,
                        prefix,
                        format);
                    var selectionWidth = Math.Max(
                        MeasureText(e.Graphics, selected, format),
                        2F);
                    var selectionBounds = new RectangleF(
                        selectionX,
                        textY - 2,
                        selectionWidth,
                        Font.Height + 4);
                    using (var brush = new SolidBrush(colors.Accent))
                        e.Graphics.FillRectangle(brush, selectionBounds);
                    DrawText(
                        e.Graphics,
                        selected,
                        selectionX,
                        textY,
                        Color.White,
                        format);
                }
                else if (Focused)
                {
                    var caretX = 10F + MeasureText(
                        e.Graphics,
                        displayValue.Substring(0, selectionStart),
                        format);
                    using (var pen = new Pen(colors.TextPrimary))
                        e.Graphics.DrawLine(
                            pen,
                            caretX,
                            textY - 1,
                            caretX,
                            textY + Font.Height + 1);
                }
            }

            DrawAdornment(e.Graphics);
        }

        private string FitMiddleEllipsis(
            Graphics graphics,
            string value,
            float maximumWidth,
            StringFormat format)
        {
            if (string.IsNullOrEmpty(value) ||
                MeasureText(graphics, value, format) <= maximumWidth)
            {
                return value;
            }

            const string ellipsis = "...";
            var low = 0;
            var high = value.Length;
            var best = ellipsis;
            while (low <= high)
            {
                var keep = (low + high) / 2;
                var left = (keep + 1) / 2;
                var right = keep / 2;
                var candidate = value.Substring(0, left) +
                    ellipsis +
                    value.Substring(value.Length - right);
                if (MeasureText(graphics, candidate, format) <= maximumWidth)
                {
                    best = candidate;
                    low = keep + 1;
                }
                else
                {
                    high = keep - 1;
                }
            }
            return best;
        }

        protected virtual bool AcceptCharacter(char value)
        {
            return !char.IsControl(value);
        }

        protected virtual void DrawAdornment(Graphics graphics)
        {
        }

        protected void ReplaceSelection(string value)
        {
            var filtered = FilterText(value);
            if (string.IsNullOrEmpty(filtered)) return;
            var available = MaxLength - (Text.Length - _selectionLength);
            if (available <= 0) return;
            if (filtered.Length > available)
                filtered = filtered.Substring(0, available);
            Text = Text.Remove(_selectionStart, _selectionLength)
                .Insert(_selectionStart, filtered);
            _selectionStart += filtered.Length;
            _selectionLength = 0;
            Invalidate();
        }

        protected virtual string FilterText(string value)
        {
            var result = string.Empty;
            foreach (var character in value ?? string.Empty)
            {
                if (AcceptCharacter(character))
                    result += character;
            }
            return result;
        }

        private void Backspace()
        {
            if (_selectionLength > 0)
            {
                ReplaceSelectionWithEmpty();
                return;
            }
            if (_selectionStart <= 0) return;
            _selectionStart--;
            Text = Text.Remove(_selectionStart, 1);
            Invalidate();
        }

        private void Delete()
        {
            if (_selectionLength > 0)
            {
                ReplaceSelectionWithEmpty();
                return;
            }
            if (_selectionStart >= Text.Length) return;
            Text = Text.Remove(_selectionStart, 1);
            Invalidate();
        }

        private void ReplaceSelectionWithEmpty()
        {
            Text = Text.Remove(_selectionStart, _selectionLength);
            _selectionLength = 0;
            Invalidate();
        }

        private void MoveCaret(int offset)
        {
            if (_selectionLength > 0)
            {
                _selectionStart = offset < 0
                    ? _selectionStart
                    : _selectionStart + _selectionLength;
                _selectionLength = 0;
            }
            else
            {
                _selectionStart = Math.Max(
                    0,
                    Math.Min(Text.Length, _selectionStart + offset));
            }
            Invalidate();
        }

        private void CopySelection()
        {
            if (_selectionLength <= 0) return;
            try
            {
                Clipboard.SetText(Text.Substring(
                    _selectionStart,
                    _selectionLength));
            }
            catch
            {
            }
        }

        private void PasteText()
        {
            try
            {
                if (Clipboard.ContainsText())
                    ReplaceSelection(Clipboard.GetText());
            }
            catch
            {
            }
        }

        private int FindCharacterIndex(int targetX)
        {
            if (targetX <= 0) return 0;
            using (var graphics = CreateGraphics())
            using (var format = CreateTextFormat())
            {
                var previousWidth = 0F;
                for (var index = 1; index <= Text.Length; index++)
                {
                    var currentWidth = MeasureText(
                        graphics,
                        Text.Substring(0, index),
                        format);
                    if (targetX < (previousWidth + currentWidth) / 2F)
                        return index - 1;
                    previousWidth = currentWidth;
                }
            }
            return Text.Length;
        }

        private float MeasureText(
            Graphics graphics,
            string value,
            StringFormat format)
        {
            if (string.IsNullOrEmpty(value)) return 0F;
            return graphics.MeasureString(
                value,
                Font,
                int.MaxValue,
                format).Width;
        }

        private void DrawText(
            Graphics graphics,
            string value,
            float x,
            float y,
            Color color,
            StringFormat format)
        {
            using (var brush = new SolidBrush(color))
                graphics.DrawString(
                    value ?? string.Empty,
                    Font,
                    brush,
                    new PointF(x, y),
                    format);
        }

        private static StringFormat CreateTextFormat()
        {
            var format = (StringFormat)StringFormat.GenericTypographic.Clone();
            format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces |
                StringFormatFlags.NoWrap;
            return format;
        }
    }

    internal sealed class ThemedHotkeyControl : ThemedTextControl
    {
        private string _valueBeforeCapture = string.Empty;

        public ThemedHotkeyControl()
        {
            MaxLength = 80;
        }

        protected override void OnEnter(EventArgs e)
        {
            _valueBeforeCapture = Text;
            base.OnEnter(e);
        }

        protected override bool ProcessCmdKey(
            ref Message message,
            Keys keyData)
        {
            var key = keyData & Keys.KeyCode;
            if (key == Keys.Tab)
                return base.ProcessCmdKey(ref message, keyData);

            if (key == Keys.Escape)
            {
                Text = _valueBeforeCapture;
                SelectAll();
                return true;
            }

            if (key == Keys.Delete || key == Keys.Back)
            {
                Text = string.Empty;
                SelectAll();
                return true;
            }

            if (IsModifierKey(key))
                return true;

            Text = BuildShortcut(key, keyData);
            _valueBeforeCapture = Text;
            SelectAll();
            return true;
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            e.Handled = true;
        }

        private static string BuildShortcut(Keys key, Keys keyData)
        {
            var parts = new List<string>();
            AppendModifier(
                parts,
                NativeMethods.VkLControl,
                NativeMethods.VkRControl,
                (keyData & Keys.Control) == Keys.Control,
                "LeftCtrl",
                "RightCtrl",
                "Ctrl");
            AppendModifier(
                parts,
                NativeMethods.VkLMenu,
                NativeMethods.VkRMenu,
                (keyData & Keys.Alt) == Keys.Alt,
                "LeftAlt",
                "RightAlt",
                "Alt");
            AppendModifier(
                parts,
                NativeMethods.VkLShift,
                NativeMethods.VkRShift,
                (keyData & Keys.Shift) == Keys.Shift,
                "LeftShift",
                "RightShift",
                "Shift");

            if (IsDown(Keys.LWin)) parts.Add("LeftWindows");
            if (IsDown(Keys.RWin)) parts.Add("RightWindows");
            parts.Add(key.ToString());
            return string.Join("+", parts.ToArray());
        }

        private static void AppendModifier(
            ICollection<string> parts,
            int leftKey,
            int rightKey,
            bool genericDown,
            string leftName,
            string rightName,
            string genericName)
        {
            var leftDown = IsDown((Keys)leftKey);
            var rightDown = IsDown((Keys)rightKey);
            if (leftDown) parts.Add(leftName);
            if (rightDown) parts.Add(rightName);
            if (!leftDown && !rightDown && genericDown)
                parts.Add(genericName);
        }

        private static bool IsDown(Keys key)
        {
            return (NativeMethods.GetAsyncKeyState((int)key) & 0x8000) != 0;
        }

        private static bool IsModifierKey(Keys key)
        {
            return key == Keys.ShiftKey ||
                key == Keys.LShiftKey ||
                key == Keys.RShiftKey ||
                key == Keys.ControlKey ||
                key == Keys.LControlKey ||
                key == Keys.RControlKey ||
                key == Keys.Menu ||
                key == Keys.LMenu ||
                key == Keys.RMenu ||
                key == Keys.LWin ||
                key == Keys.RWin;
        }
    }

    internal sealed class ThemedNumberControl : ThemedTextControl
    {
        private decimal _value;
        private bool _updatingText;

        public ThemedNumberControl()
        {
            Minimum = 0;
            Maximum = 100;
            Increment = 1;
            ShowStepButtons = true;
            MaxLength = 8;
            Value = 0;
        }

        public decimal Minimum { get; set; }
        public decimal Maximum { get; set; }
        public decimal Increment { get; set; }
        public bool ShowStepButtons { get; set; }

        public decimal Value
        {
            get { return _value; }
            set
            {
                var normalized = Math.Max(Minimum, Math.Min(Maximum, value));
                if (_value == normalized && Text == normalized.ToString()) return;
                _value = normalized;
                _updatingText = true;
                Text = normalized.ToString();
                _updatingText = false;
                var handler = ValueChanged;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        public event EventHandler ValueChanged;

        protected override bool AcceptCharacter(char value)
        {
            return char.IsDigit(value);
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            if (_updatingText) return;
            decimal parsed;
            if (!decimal.TryParse(Text, out parsed)) return;
            parsed = Math.Max(Minimum, Math.Min(Maximum, parsed));
            if (_value == parsed) return;
            _value = parsed;
            var handler = ValueChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        protected override void OnLeave(EventArgs e)
        {
            decimal parsed;
            Value = decimal.TryParse(Text, out parsed) ? parsed : Minimum;
            base.OnLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (ShowStepButtons && e.X >= Width - 30)
            {
                Focus();
                Value += e.Y < Height / 2 ? Increment : -Increment;
                return;
            }
            base.OnMouseDown(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Up)
            {
                Value += Increment;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Down)
            {
                Value -= Increment;
                e.SuppressKeyPress = true;
            }
            base.OnKeyDown(e);
        }

        protected override void DrawAdornment(Graphics graphics)
        {
            if (!ShowStepButtons) return;
            var colors = ThemeColors.Current;
            using (var pen = new Pen(
                Enabled ? colors.TextTertiary : colors.DisabledText,
                1.4F)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            })
            {
                DrawChevron(graphics, pen, Width - 17, Height / 2 - 5, true);
                DrawChevron(graphics, pen, Width - 17, Height / 2 + 5, false);
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

    internal sealed class ThemedDropDownForm : Form
    {
        public ThemedDropDownForm(
            IList<object> items,
            int selectedIndex,
            int width)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            BackColor = ThemeColors.Current.CardBackground;

            var list = new ThemedDropDownList(items, selectedIndex)
            {
                Dock = DockStyle.Fill
            };
            list.ItemCommitted += delegate(int index)
            {
                SelectedIndex = index;
                var handler = ItemSelected;
                if (handler != null) handler(index);
                Close();
            };
            Controls.Add(list);
            ClientSize = new Size(
                Math.Max(width, 120),
                Math.Min(items.Count, 8) * 30 + 2);
            Shown += delegate { list.Focus(); };
            Deactivate += delegate { Close(); };
        }

        public int SelectedIndex { get; private set; }
        public event Action<int> ItemSelected;

        protected override bool ShowWithoutActivation
        {
            get { return false; }
        }
    }

    internal sealed class ThemedDropDownList : Control
    {
        private const int ItemHeight = 30;
        private readonly IList<object> _items;
        private int _hoverIndex;
        private int _topIndex;

        public ThemedDropDownList(IList<object> items, int selectedIndex)
        {
            _items = items;
            _hoverIndex = selectedIndex >= 0
                ? selectedIndex
                : (items.Count > 0 ? 0 : -1);
            _topIndex = Math.Max(0, _hoverIndex - 3);
            TabStop = true;
            Font = new Font("Segoe UI", 9.5F);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw,
                true);
        }

        public event Action<int> ItemCommitted;

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down)
            {
                _hoverIndex = Math.Min(_items.Count - 1, _hoverIndex + 1);
                EnsureHoverVisible();
                Invalidate();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Up)
            {
                _hoverIndex = Math.Max(0, _hoverIndex - 1);
                EnsureHoverVisible();
                Invalidate();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                CommitHoveredItem();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                var form = FindForm();
                if (form != null) form.Close();
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_items.Count == 0)
            {
                base.OnMouseMove(e);
                return;
            }
            var row = Math.Max(
                0,
                Math.Min(
                    VisibleRowCount - 1,
                    Math.Max(e.Y - 1, 0) / ItemHeight));
            var index = Math.Min(_items.Count - 1, _topIndex + row);
            if (_hoverIndex != index)
            {
                _hoverIndex = index;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left &&
                _hoverIndex >= 0 &&
                _hoverIndex < _items.Count)
            {
                CommitHoveredItem();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (_items.Count <= VisibleRowCount)
            {
                base.OnMouseWheel(e);
                return;
            }
            var direction = e.Delta > 0 ? -1 : 1;
            _topIndex = Math.Max(
                0,
                Math.Min(MaxTopIndex, _topIndex + direction * 3));
            _hoverIndex = Math.Max(
                _topIndex,
                Math.Min(
                    _topIndex + VisibleRowCount - 1,
                    _hoverIndex));
            Invalidate();
            base.OnMouseWheel(e);
        }

        protected override void OnResize(EventArgs e)
        {
            EnsureHoverVisible();
            base.OnResize(e);
        }

        private void CommitHoveredItem()
        {
            if (_hoverIndex < 0 || _hoverIndex >= _items.Count) return;
            var handler = ItemCommitted;
            if (handler != null) handler(_hoverIndex);
        }

        private int VisibleRowCount
        {
            get { return Math.Max(1, Height / ItemHeight); }
        }

        private int MaxTopIndex
        {
            get { return Math.Max(0, _items.Count - VisibleRowCount); }
        }

        private void EnsureHoverVisible()
        {
            _topIndex = Math.Max(0, Math.Min(MaxTopIndex, _topIndex));
            if (_hoverIndex < 0) return;
            if (_hoverIndex < _topIndex)
                _topIndex = _hoverIndex;
            else if (_hoverIndex >= _topIndex + VisibleRowCount)
                _topIndex = _hoverIndex - VisibleRowCount + 1;
            _topIndex = Math.Max(0, Math.Min(MaxTopIndex, _topIndex));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var colors = ThemeColors.Current;
            e.Graphics.Clear(colors.CardBackground);
            var lastIndex = Math.Min(
                _items.Count,
                _topIndex + VisibleRowCount);
            for (var index = _topIndex; index < lastIndex; index++)
            {
                var row = index - _topIndex;
                var bounds = new Rectangle(
                    1,
                    1 + row * ItemHeight,
                    Width - 2,
                    ItemHeight);
                if (index == _hoverIndex)
                {
                    using (var brush = new SolidBrush(colors.AccentSoft))
                        e.Graphics.FillRectangle(brush, bounds);
                }
                TextRenderer.DrawText(
                    e.Graphics,
                    _items[index] == null ? string.Empty : _items[index].ToString(),
                    Font,
                    new Rectangle(10, bounds.Top, bounds.Width - 20, bounds.Height),
                    colors.TextPrimary,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }
            if (_items.Count > VisibleRowCount)
            {
                var trackHeight = Math.Max(Height - 8, 1);
                var thumbHeight = Math.Max(
                    24,
                    trackHeight * VisibleRowCount / _items.Count);
                var travel = Math.Max(trackHeight - thumbHeight, 0);
                var thumbTop = 4 + (MaxTopIndex == 0
                    ? 0
                    : travel * _topIndex / MaxTopIndex);
                using (var brush = new SolidBrush(colors.ControlBorder))
                {
                    e.Graphics.FillRectangle(
                        brush,
                        Width - 5,
                        thumbTop,
                        2,
                        thumbHeight);
                }
            }
            using (var pen = new Pen(colors.ControlBorder))
            {
                var border = ClientRectangle;
                border.Width--;
                border.Height--;
                e.Graphics.DrawRectangle(pen, border);
            }
        }
    }
}
