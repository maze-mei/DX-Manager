using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

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
            Font = new Font("Segoe UI", 9F);
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
                if (!IsDisposed) Focus();
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
            Font = new Font("Segoe UI", 9F);
            Size = new Size(200, 32);
            MaxLength = 256;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                ControlStyles.Selectable, true);
        }

        public int MaxLength { get; set; }

        public void SelectAll()
        {
            _selectionStart = 0;
            _selectionLength = Text.Length;
            Invalidate();
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
            Focus();
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

        protected override void OnPaint(PaintEventArgs e)
        {
            var colors = ThemeColors.Current;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            ThemedSelectControl.DrawFieldBackground(
                e.Graphics,
                ClientRectangle,
                Focused,
                Enabled);

            var textY = (Height - Font.Height) / 2;
            var textColor = Enabled ? colors.TextPrimary : colors.DisabledText;
            DrawText(e.Graphics, Text, 10, textY, textColor);

            if (Focused && _selectionLength > 0)
            {
                var prefix = Text.Substring(0, _selectionStart);
                var selected = Text.Substring(
                    _selectionStart,
                    _selectionLength);
                var selectionX = 10 + MeasureText(prefix);
                var selectionWidth = Math.Max(MeasureText(selected), 2);
                var selectionBounds = new Rectangle(
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
                    Color.White);
            }
            else if (Focused)
            {
                var caretX = 10 + MeasureText(
                    Text.Substring(0, _selectionStart));
                using (var pen = new Pen(colors.TextPrimary))
                    e.Graphics.DrawLine(
                        pen,
                        caretX,
                        textY - 1,
                        caretX,
                        textY + Font.Height + 1);
            }

            DrawAdornment(e.Graphics);
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
            Text = Text.Remove(_selectionStart - 1, 1);
            _selectionStart--;
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
            for (var index = 1; index <= Text.Length; index++)
            {
                if (MeasureText(Text.Substring(0, index)) >= targetX)
                    return index;
            }
            return Text.Length;
        }

        private int MeasureText(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            return TextRenderer.MeasureText(
                value,
                Font,
                new Size(int.MaxValue, Font.Height),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
        }

        private void DrawText(
            Graphics graphics,
            string value,
            int x,
            int y,
            Color color)
        {
            TextRenderer.DrawText(
                graphics,
                value ?? string.Empty,
                Font,
                new Point(x, y),
                color,
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
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
        private readonly IList<object> _items;
        private int _hoverIndex;

        public ThemedDropDownList(IList<object> items, int selectedIndex)
        {
            _items = items;
            _hoverIndex = selectedIndex;
            TabStop = true;
            Font = new Font("Segoe UI", 9F);
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
                Invalidate();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Up)
            {
                _hoverIndex = Math.Max(0, _hoverIndex - 1);
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
            var index = Math.Max(0, Math.Min(_items.Count - 1, e.Y / 30));
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

        private void CommitHoveredItem()
        {
            if (_hoverIndex < 0 || _hoverIndex >= _items.Count) return;
            var handler = ItemCommitted;
            if (handler != null) handler(_hoverIndex);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var colors = ThemeColors.Current;
            e.Graphics.Clear(colors.CardBackground);
            for (var index = 0; index < _items.Count; index++)
            {
                var bounds = new Rectangle(1, 1 + index * 30, Width - 2, 30);
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
