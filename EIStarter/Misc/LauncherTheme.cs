using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EIStarter
{
    internal sealed class LauncherMenuRenderer : ToolStripRenderer
    {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            e.Graphics.Clear(LauncherTheme.Dark ? Color.FromArgb(46, 48, 52) : Color.White);
        }
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using (var pen = new Pen(LauncherTheme.Dark ? Color.FromArgb(77, 80, 86) : Color.Silver))
                e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected) return;
            using (var brush = new SolidBrush(LauncherTheme.Dark ? Color.FromArgb(61, 53, 40) : Color.FromArgb(248, 234, 210)))
                e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.Item.Size));
        }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = LauncherTheme.Dark ? Color.FromArgb(232, 234, 237) : Color.FromArgb(32, 33, 36);
            base.OnRenderItemText(e);
        }
    }

    internal static class LauncherTheme
    {
        private static readonly Icon ApplicationIcon = LoadApplicationIcon();
        private static Icon LoadApplicationIcon()
        {
            using (var stream = typeof(LauncherTheme).Assembly.GetManifestResourceStream("EIStarter.AppIcon"))
            using (var icon = new Icon(stream)) return (Icon)icon.Clone();
        }

        public static bool Dark = new IniFile(LauncherProfile.ConfigName).Read("UiTheme", "Settings", "graphite") != "light";
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

        public static void Apply(Control root)
        {
            Color background = Dark ? Color.FromArgb(32, 33, 36) : Color.FromArgb(245, 246, 248);
            Color surface = Dark ? Color.FromArgb(46, 48, 52) : Color.White;
            Color text = Dark ? Color.FromArgb(232, 234, 237) : Color.FromArgb(32, 33, 36);
            root.BackColor = background;
            root.ForeColor = text;
            if (root is Button button)
            {
                button.UseVisualStyleBackColor = false;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = Dark ? Color.FromArgb(77, 80, 86) : Color.FromArgb(200, 203, 208);
                button.FlatAppearance.MouseOverBackColor = Dark ? Color.FromArgb(60, 63, 69) : Color.FromArgb(226, 230, 235);
                button.BackColor = surface;
                if (button.Tag as string == "primary")
                {
                    button.BackColor = Color.FromArgb(205, 151, 69);
                    button.ForeColor = Color.FromArgb(27, 24, 19);
                    button.FlatAppearance.BorderColor = Color.FromArgb(232, 187, 112);
                    button.FlatAppearance.MouseOverBackColor = Color.FromArgb(232, 181, 94);
                }
                else if (button.Tag as string == "selected")
                {
                    button.BackColor = Dark ? Color.FromArgb(61, 53, 40) : Color.FromArgb(248, 234, 210);
                    button.ForeColor = Dark ? Color.FromArgb(239, 194, 123) : Color.FromArgb(102, 65, 14);
                    button.FlatAppearance.BorderColor = Color.FromArgb(181, 135, 68);
                }
                else if (button.Tag as string == "navigation")
                {
                    button.BackColor = background;
                    button.FlatAppearance.BorderSize = 0;
                }
            }
            if (root.Parent is ArtworkPanel)
            {
                root.BackColor = Color.Transparent;
                root.ForeColor = Color.FromArgb(248, 238, 218);
            }
            if (root is InputPanel || root is TextBoxBase || root is ComboBox || root is NumericUpDown) root.BackColor = surface;
            if (root is ComboBox combo)
            {
                combo.FlatStyle = FlatStyle.Flat;
                combo.DrawMode = DrawMode.OwnerDrawFixed;
                combo.ItemHeight = combo.Font.Height + 6;
                combo.DrawItem -= DrawCombo;
                combo.DrawItem += DrawCombo;
            }
            if (root is LinkLabel link) link.LinkColor = Dark ? Color.LightSkyBlue : Color.FromArgb(25, 90, 150);
            if (root is TabPage page) page.UseVisualStyleBackColor = false;
            if (root is TabControl tabs)
            {
                tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
                tabs.DrawItem -= DrawTab;
                tabs.DrawItem += DrawTab;
            }
            if (root is PropertyGrid grid)
            {
                grid.ViewBackColor = surface;
                grid.ViewForeColor = text;
                grid.HelpBackColor = background;
                grid.HelpForeColor = text;
                grid.CategoryForeColor = text;
                grid.LineColor = background;
                grid.CommandsBackColor = background;
                grid.CommandsForeColor = text;
                grid.Refresh();
                return; // The grid owns its internal editors and their painting.
            }
            foreach (Control child in root.Controls) Apply(child);
            if (root is Form form && form.IsHandleCreated)
            {
                int dark = Dark ? 1 : 0;
                try { DwmSetWindowAttribute(form.Handle, 20, ref dark, sizeof(int)); }
                catch (DllNotFoundException) { }
                catch (EntryPointNotFoundException) { }
            }
            root.Invalidate();
        }

        private static void DrawTab(object sender, DrawItemEventArgs e)
        {
            var tabs = (TabControl)sender;
            using (var brush = new SolidBrush(Dark ? Color.FromArgb(46, 48, 52) : Color.White)) e.Graphics.FillRectangle(brush, e.Bounds);
            TextRenderer.DrawText(e.Graphics, tabs.TabPages[e.Index].Text, tabs.Font, e.Bounds,
                tabs.ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private static void DrawCombo(object sender, DrawItemEventArgs e)
        {
            var combo = (ComboBox)sender;
            using (var brush = new SolidBrush(combo.BackColor)) e.Graphics.FillRectangle(brush, e.Bounds);
            string text = e.Index >= 0 ? combo.GetItemText(combo.Items[e.Index]) : combo.Text;
            TextRenderer.DrawText(e.Graphics, text, combo.Font, e.Bounds, combo.ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            e.DrawFocusRectangle();
        }

        // Apply theme properties as one layout update, including nested AutoSize panels.
        public static void ApplyBatched(Control root)
        {
            var controls = new System.Collections.Generic.List<Control>();
            void Suspend(Control control)
            {
                controls.Add(control);
                control.SuspendLayout();
                foreach (Control child in control.Controls) Suspend(child);
            }
            Suspend(root);
            try { Apply(root); }
            finally
            {
                for (int i = controls.Count - 1; i >= 0; i--)
                    if (!controls[i].IsDisposed) controls[i].ResumeLayout(false);
                if (!root.IsDisposed) root.PerformLayout();
            }
        }

        public static void PrepareBatched(Form form)
        {
            form.Icon = ApplicationIcon;
            ApplyBatched(form);
            form.HandleCreated += (sender, args) => ApplyBatched(form);
        }

        public static void Prepare(Form form)
        {
            form.Icon = ApplicationIcon;
            Apply(form);
            form.HandleCreated += (sender, args) => Apply(form);
        }
    }
}
