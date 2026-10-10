using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EIStarter
{
    internal static class SelectionControls
    {

        public static Button Button(string text)
        {
            return new Button { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(80, 38), Padding = new Padding(12, 8, 12, 8), Margin = new Padding(0, 0, 8, 8),
                Font = new Font("Segoe UI", 10F), FlatStyle = FlatStyle.Flat };
        }

        public static Control Choice(ComboBox source, string title, bool cards = false, Func<int, string> describe = null)
        {
            if (!cards && source.Items.Count > 0 && source.Items.Count <= 5)
            {
                var strip = new FlowLayoutPanel { Name = "choice_" + source.Name, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Size = Size.Empty, Dock = DockStyle.Fill };
                for (int i = 0; i < source.Items.Count; i++)
                {
                    int index = i;
                    var button = Button(source.GetItemText(source.Items[i]));
                    button.Tag = source.SelectedIndex == i ? "selected" : null;
                    button.Click += (sender, args) => source.SelectedIndex = index;
                    strip.Controls.Add(button);
                }
                EventHandler sync = (sender, args) =>
                {
                    for (int i = 0; i < strip.Controls.Count; i++)
                        strip.Controls[i].Tag = source.SelectedIndex == i ? "selected" : null;
                    LauncherTheme.Apply(strip);
                };
                source.SelectedIndexChanged += sync;
                strip.Disposed += (sender, args) => source.SelectedIndexChanged -= sync;
                return strip;
            }
            var picker = Button("");
            picker.Name = "choice_" + source.Name;
            picker.Dock = DockStyle.Fill;
            picker.TextAlign = ContentAlignment.MiddleLeft;
            void Refresh()
            {
                picker.Text = source.SelectedIndex >= 0
                    ? (describe == null ? source.GetItemText(source.SelectedItem) : describe(source.SelectedIndex)) + "   ›"
                    : title + "   ›";
            }
            Refresh();
            EventHandler changed = (sender, args) => Refresh();
            source.SelectedIndexChanged += changed;
            picker.Disposed += (sender, args) => source.SelectedIndexChanged -= changed;
            picker.Click += (sender, args) =>
            {
                string[] items = Enumerable.Range(0, source.Items.Count)
                    .Select(i => describe == null ? source.GetItemText(source.Items[i]) : describe(i)).ToArray();
                using (var dialog = new SelectionDialog(cards && source.Name == "cbbMod" ? UiText.Get("ModPicker") : title, items, source.SelectedIndex))
                    if (dialog.ShowDialog(picker.FindForm()) == DialogResult.OK) source.SelectedIndex = dialog.SelectedIndex;
            };
            return picker;
        }

        // Settings choices stay in the page; the mod picker still uses Choice/SelectionDialog.
        public static Control InlineChoice(ComboBox source, string title)
        {
            var strip = new InlineChoicePanel { Name = "choice_" + source.Name,
                AccessibleName = title, AccessibleRole = AccessibleRole.Grouping };
            for (int i = 0; i < source.Items.Count; i++)
            {
                int index = i;
                string text = source.GetItemText(source.Items[i]);
                var button = new InlineChoiceButton { Name = strip.Name + "_" + i, Text = text,
                    AccessibleName = title + ": " + text, Tag = source.SelectedIndex == i ? "selected" : null,
                    AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    MinimumSize = new Size(52, 32), Padding = new Padding(9, 5, 9, 5),
                    Margin = new Padding(0, 0, 5, 5), Font = new Font("Segoe UI", 10F), FlatStyle = FlatStyle.Flat };
                button.Click += (sender, args) => source.SelectedIndex = index;
                strip.Controls.Add(button);
            }
            EventHandler sync = (sender, args) =>
            {
                for (int i = 0; i < strip.Controls.Count; i++)
                    strip.Controls[i].Tag = source.SelectedIndex == i ? "selected" : null;
                LauncherTheme.ApplyBatched(strip);
            };
            source.SelectedIndexChanged += sync;
            strip.Disposed += (sender, args) => source.SelectedIndexChanged -= sync;
            return strip;
        }

        public static Control DropdownChoice(ComboBox source, string title)
        {
            var host = new Panel { Name = "choice_" + source.Name, AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, Size = Size.Empty };
            source.AccessibleName = title;
            source.DropDownStyle = ComboBoxStyle.DropDownList;
            source.Dock = DockStyle.Top;
            source.Font = new Font("Segoe UI", 10F);
            source.Margin = Padding.Empty;
            host.Controls.Add(source);
            return host;
        }

        public static Button SectionButton(string title, string section)
        {
            return new SettingsSectionButton { Text = title, Section = section, AccessibleName = title,
                Size = new Size(182, 42), Padding = new Padding(43, 4, 8, 4),
                Margin = new Padding(0, 0, 0, 8), Font = new Font("Segoe UI", 10F),
                FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleLeft };
        }

        public static Control InputSurface(Control editor)
        {
            if (!(editor is TextBoxBase) && !(editor is NumericUpDown)) return editor;
            if (editor is NumericUpDown number)
            {
                var entry = new TextBox { Name = "entry_" + number.Name, Text = number.Value.ToString(), Font = number.Font };
                entry.Validating += (sender, args) =>
                {
                    decimal value;
                    args.Cancel = !decimal.TryParse(entry.Text, out value) || value < number.Minimum || value > number.Maximum;
                    if (!args.Cancel) number.Value = value;
                    entry.ForeColor = args.Cancel ? Color.IndianRed : (LauncherTheme.Dark ? Color.Gainsboro : Color.Black);
                };
                // The original control remains the settings model, including its bounds.
                number.Visible = false;
                editor = entry;
            }
            ((TextBoxBase)editor).BorderStyle = BorderStyle.None;
            var surface = new InputPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(12, 10, 12, 10), Dock = DockStyle.Fill, Margin = Padding.Empty };
            editor.Dock = DockStyle.Top;
            editor.Margin = Padding.Empty;
            surface.Controls.Add(editor);
            return surface;
        }

        public static Control ThemeIcons(ComboBox source)
        {
            var strip = new FlowLayoutPanel { Name = "choice_" + source.Name, AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink, Anchor = AnchorStyles.Right, WrapContents = false };
            var tips = new ToolTip();
            for (int i = 0; i < 2; i++)
            {
                int index = i;
                var button = new ThemeIconButton { Moon = i == 0, Size = new Size(44, 40),
                    Margin = new Padding(6, 0, 0, 0), Tag = source.SelectedIndex == i ? "selected" : null };
                button.MouseEnter += (sender, args) => tips.SetToolTip(button, button.AccessibleName);
                button.Click += (sender, args) => source.SelectedIndex = index;
                strip.Controls.Add(button);
            }
            EventHandler sync = (sender, args) =>
            {
                for (int i = 0; i < 2; i++) strip.Controls[i].Tag = source.SelectedIndex == i ? "selected" : null;
                LauncherTheme.Apply(strip);
            };
            source.SelectedIndexChanged += sync;
            strip.Disposed += (sender, args) => { source.SelectedIndexChanged -= sync; tips.Dispose(); };
            return strip;
        }

        public static Control Toggle(CheckBox source)
        {
            var button = new SwitchButton { Size = new Size(112, 38), MinimumSize = new Size(112, 38), Font = new Font("Segoe UI", 10F) };
            int width = Math.Max(TextRenderer.MeasureText(UiText.Get("Unspecified"), button.Font).Width + 60, Math.Max(TextRenderer.MeasureText(UiText.Get("On"), button.Font).Width, TextRenderer.MeasureText(UiText.Get("Off"), button.Font).Width) + 60);
            button.Size = button.MinimumSize = new Size(width, 38);
            button.Name = "toggle_" + source.Name;
            button.AccessibleName = source.Text;
            void Refresh()
            {
                button.IsOn = source.CheckState == CheckState.Checked;
                button.IsUnknown = source.CheckState == CheckState.Indeterminate;
                button.Text = UiText.Get(source.CheckState == CheckState.Indeterminate ? "Unspecified" : source.Checked ? "On" : "Off");
                button.Tag = source.CheckState == CheckState.Checked ? "selected" : null;
                LauncherTheme.Apply(button);
            }
            Refresh();
            button.Click += (sender, args) => source.CheckState = source.CheckState == CheckState.Checked ? CheckState.Unchecked : CheckState.Checked;
            EventHandler changed = (sender, args) => Refresh();
            source.CheckStateChanged += changed;
            button.Disposed += (sender, args) => source.CheckStateChanged -= changed;
            return button;
        }
    }

    internal sealed class InlineChoicePanel : FlowLayoutPanel
    {
        public InlineChoicePanel()
        {
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            WrapContents = true;
            Dock = DockStyle.Fill;
            Size = Size.Empty;
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            int limit = proposedSize.Width > 1 && proposedSize.Width < int.MaxValue
                ? proposedSize.Width : Width > 1 ? Width : int.MaxValue;
            int x = 0, y = 0, lineHeight = 0, widest = 0;
            foreach (Control child in Controls)
            {
                Size size = child.GetPreferredSize(new Size(Math.Max(1, limit - child.Margin.Horizontal), 0));
                int width = Math.Min(limit, size.Width + child.Margin.Horizontal);
                int height = size.Height + child.Margin.Vertical;
                if (x > 0 && x + width > limit) { y += lineHeight; x = 0; lineHeight = 0; }
                x += width;
                widest = Math.Max(widest, x);
                lineHeight = Math.Max(lineHeight, height);
            }
            return new Size(widest, y + lineHeight);
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            // Long translations/unknown values may wrap, but never widen the settings page.
            foreach (Control child in Controls)
                child.MaximumSize = new Size(Math.Max(child.MinimumSize.Width, ClientSize.Width - child.Margin.Horizontal), 0);
            base.OnLayout(e);
        }
    }

    internal sealed class InlineChoiceButton : Button
    {
        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            return key == Keys.Left || key == Keys.Right || key == Keys.Home || key == Keys.End || base.IsInputKey(keyData);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (Parent is InlineChoicePanel strip && (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right ||
                e.KeyCode == Keys.Home || e.KeyCode == Keys.End))
            {
                int current = strip.Controls.GetChildIndex(this);
                int index = e.KeyCode == Keys.Home ? 0 : e.KeyCode == Keys.End ? strip.Controls.Count - 1 :
                    (current + (e.KeyCode == Keys.Left ? -1 : 1) + strip.Controls.Count) % strip.Controls.Count;
                var next = (InlineChoiceButton)strip.Controls[index];
                next.Focus();
                next.OnClick(EventArgs.Empty);
                e.Handled = e.SuppressKeyPress = true;
            }
            base.OnKeyDown(e);
        }
        protected override AccessibleObject CreateAccessibilityInstance() => new ChoiceAccessible(this);
        private sealed class ChoiceAccessible : ControlAccessibleObject
        {
            private readonly InlineChoiceButton owner;
            public ChoiceAccessible(InlineChoiceButton owner) : base(owner) { this.owner = owner; }
            public override AccessibleRole Role => AccessibleRole.RadioButton;
            public override AccessibleStates State => base.State | (owner.Tag as string == "selected" ? AccessibleStates.Checked : AccessibleStates.None);
        }
    }

    internal sealed class SettingsSectionButton : Button
    {
        public string Section { get; set; }
        public SettingsSectionButton() { DoubleBuffered = true; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var state = e.Graphics.Save();
            try
            {
                float scale = DeviceDpi / 96F;
                e.Graphics.TranslateTransform(12 * scale, (Height - 22 * scale) / 2F);
                e.Graphics.ScaleTransform(scale, scale);
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var pen = new Pen(ForeColor, 1.6F) { StartCap = System.Drawing.Drawing2D.LineCap.Round,
                    EndCap = System.Drawing.Drawing2D.LineCap.Round, LineJoin = System.Drawing.Drawing2D.LineJoin.Round })
                {
                    switch (Section)
                    {
                        case "tabGraphics":
                            e.Graphics.DrawRectangle(pen, 1, 2, 20, 14);
                            e.Graphics.DrawLine(pen, 8, 20, 14, 20);
                            e.Graphics.DrawLine(pen, 11, 16, 11, 20);
                            e.Graphics.DrawLines(pen, new[] { new Point(5, 12), new Point(9, 8), new Point(12, 11), new Point(17, 5) });
                            break;
                        case "tabMultiplayer":
                            e.Graphics.DrawEllipse(pen, 7, 2, 7, 7);
                            e.Graphics.DrawArc(pen, 4, 10, 13, 16, 190, 160);
                            e.Graphics.DrawArc(pen, 0, 5, 6, 6, 70, 270);
                            e.Graphics.DrawArc(pen, 16, 5, 6, 6, 200, 270);
                            e.Graphics.DrawArc(pen, -2, 12, 10, 12, 190, 100);
                            e.Graphics.DrawArc(pen, 14, 12, 10, 12, 250, 100);
                            break;
                        case "tabCamera":
                            e.Graphics.DrawLines(pen, new[] { new Point(1, 7), new Point(5, 7), new Point(7, 3), new Point(14, 3), new Point(16, 7), new Point(21, 7), new Point(21, 19), new Point(1, 19), new Point(1, 7) });
                            e.Graphics.DrawEllipse(pen, 7, 8, 8, 8);
                            e.Graphics.DrawLine(pen, 18, 10, 19, 10);
                            break;
                        case "tabPaths":
                            e.Graphics.DrawLines(pen, new[] { new Point(1, 18), new Point(1, 4), new Point(8, 4), new Point(11, 7), new Point(20, 7), new Point(20, 10) });
                            e.Graphics.DrawLines(pen, new[] { new Point(1, 18), new Point(5, 10), new Point(22, 10), new Point(18, 19), new Point(1, 19), new Point(1, 18) });
                            break;
                    }
                }
            }
            finally { e.Graphics.Restore(state); }
        }
    }

    internal sealed class ThemeIconButton : Button
    {
        public bool Moon { get; set; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            float s = DeviceDpi / 96F, x = Width / 2F, y = Height / 2F;
            using (var pen = new Pen(ForeColor, 1.7F * s))
            {
                if (Moon)
                {
                    using (var path = new System.Drawing.Drawing2D.GraphicsPath())
                    {
                        path.AddArc(x - 10*s, y - 10*s, 20*s, 20*s, -90, -270);
                        path.AddBezier(x + 10*s, y, x - 1*s, y + 5*s, x - 5*s, y - 1*s, x, y - 10*s);
                        path.CloseFigure();
                        using (var brush = new SolidBrush(ForeColor)) e.Graphics.FillPath(brush, path);
                    }
                }
                else
                {
                    e.Graphics.DrawEllipse(pen, x - 5*s, y - 5*s, 10*s, 10*s);
                    for (int i = 0; i < 8; i++)
                    {
                        double a = i * Math.PI / 4;
                        e.Graphics.DrawLine(pen, x + (float)Math.Cos(a)*8*s, y + (float)Math.Sin(a)*8*s,
                            x + (float)Math.Cos(a)*11*s, y + (float)Math.Sin(a)*11*s);
                    }
                }
            }
        }
    }

    internal sealed class InputPanel : Panel
    {
        public InputPanel() { DoubleBuffered = true; }
        public override Size GetPreferredSize(Size proposedSize)
        {
            int height = Controls.Count == 0 ? Font.Height : Controls[0].Font.Height;
            return new Size(Math.Max(1, proposedSize.Width), height + Padding.Vertical);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(ContainsFocus ? Color.FromArgb(205,151,69) : (LauncherTheme.Dark ? Color.FromArgb(77,80,86) : Color.Silver)))
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
        protected override void OnEnter(EventArgs e) { base.OnEnter(e); Invalidate(); }
        protected override void OnLeave(EventArgs e) { base.OnLeave(e); Invalidate(); }
    }

    internal sealed class SwitchButton : Button
    {
        public bool IsOn { get; set; }
        public bool IsUnknown { get; set; }
        public SwitchButton() { DoubleBuffered = true; AccessibleRole = AccessibleRole.CheckButton; }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            float scale = DeviceDpi / 96F;
            float h = 20 * scale, w = 38 * scale, x = 6 * scale, y = (Height - h) / 2;
            using (var path = new System.Drawing.Drawing2D.GraphicsPath())
            {
                path.AddArc(x, y, h, h, 90, 180);
                path.AddArc(x + w - h, y, h, h, 270, 180);
                path.CloseFigure();
                using(var brush = new SolidBrush(IsOn ? Color.FromArgb(205,151,69) : Color.FromArgb(105,108,114))) e.Graphics.FillPath(brush,path);
            }
            using(var brush = new SolidBrush(Color.White)) e.Graphics.FillEllipse(brush, x + (IsUnknown ? (w-h)/2 : IsOn ? w-h : 0) + 3*scale, y+3*scale, h-6*scale,h-6*scale);
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle((int)(x+w+8*scale),0,Math.Max(1,Width-(int)(x+w+8*scale)),Height), ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            if(Focused) ControlPaint.DrawFocusRectangle(e.Graphics,ClientRectangle);
        }
        protected override AccessibleObject CreateAccessibilityInstance() => new SwitchAccessible(this);
        private sealed class SwitchAccessible : ControlAccessibleObject
        {
            private readonly SwitchButton owner;
            public SwitchAccessible(SwitchButton owner) : base(owner) { this.owner = owner; }
            public override AccessibleStates State => base.State | (owner.IsOn ? AccessibleStates.Checked : AccessibleStates.None);
        }
    }

    internal sealed class SelectionDialog : Form
    {
        public int SelectedIndex { get; private set; }
        public SelectionDialog(string title, string[] choices, int selected)
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 11F);
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(580, choices.Length == 0 ? 190 : Math.Min(460, 210 + choices.Length * 48));
            MinimumSize = new Size(480, choices.Length == 0 ? 220 : 320);
            SelectedIndex = selected;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 4 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);
            root.Controls.Add(new Label { Text = title, AutoSize = true, Font = new Font("Segoe UI", 18F, FontStyle.Bold), Margin = new Padding(0, 0, 0, 16) }, 0, 0);
            var search = new TextBox { Name = "searchChoices", Dock = DockStyle.Fill, AccessibleName = UiText.Get("Search"), Margin = new Padding(0, 0, 0, 16) };
            var searchRow = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 1 };
            searchRow.Controls.Add(new Label { Text = UiText.Get("Search"), AutoSize = true });
            searchRow.Controls.Add(SelectionControls.InputSurface(search));
            searchRow.Visible = choices.Length > 0;
            root.Controls.Add(searchRow, 0, 1);
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            var list = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1 };
            list.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            scroll.Controls.Add(list);
            root.Controls.Add(scroll, 0, 2);
            if (choices.Length == 0) list.Controls.Add(new Label { Text = UiText.Get("NoChoices"), AutoSize = true });
            for (int i = 0; i < choices.Length; i++)
            {
                int index = i;
                var button = SelectionControls.Button((selected == i ? "✓  " : "") + choices[i]);
                button.Name = "select_" + i;
                button.Dock = DockStyle.Fill;
                button.TextAlign = ContentAlignment.MiddleLeft;
                button.Tag = selected == i ? "selected" : null;
                button.Click += (sender, args) => { SelectedIndex = index; DialogResult = DialogResult.OK; Close(); };
                list.Controls.Add(button);
            }
            search.TextChanged += (sender, args) =>
            {
                foreach (Control choice in list.Controls) choice.Visible = choice.Text.IndexOf(search.Text, StringComparison.CurrentCultureIgnoreCase) >= 0;
            };
            var cancel = SelectionControls.Button(UiText.Get("Close"));
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Anchor = AnchorStyles.Right;
            root.Controls.Add(cancel, 0, 3);
            CancelButton = cancel;
            LauncherTheme.Prepare(this);
            ResumeLayout(true);
        }
    }
}
