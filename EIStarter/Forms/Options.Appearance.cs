using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace EIStarter
{
    public partial class Options
    {
        private bool modernSettingsBuilt;
        private ErrorProvider inputErrors;
        private void CameraFields(TableLayoutPanel page, object model, string prefix)
        {
            foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(model))
            {
                if (!property.IsBrowsable || property.IsReadOnly) continue;
                object value = property.GetValue(model);
                string key = prefix + property.Name;
                if (property.PropertyType == typeof(bool))
                {
                    var source = new CheckBox { Name = key, Checked = (bool)value };
                    if (unknownCameraBools.Contains(property.Name)) source.CheckState = CheckState.Indeterminate;
                    CameraResetRequested += () => source.CheckState = unknownCameraBools.Contains(property.Name)
                        ? CheckState.Indeterminate : (bool)property.GetValue(model) ? CheckState.Checked : CheckState.Unchecked;
                    var toggle = SelectionControls.Toggle(source);
                    source.CheckStateChanged += (sender, args) => { unknownCameraBools.Remove(property.Name); property.SetValue(model, source.Checked); };
                    toggle.Disposed += (sender, args) => source.Dispose();
                    Setting(page, UiText.Get(property.DisplayName), toggle);
                }
                else if (property.PropertyType == typeof(float) || property.PropertyType == typeof(int))
                {
                    var editor = new TextBox { Name = key, Text = (value is float f && float.IsNaN(f)) || (value is int n && n == int.MinValue) ? "" : property.Converter.ConvertToString(value), Font = Font };
                    var errors = inputErrors;
                    CameraResetRequested += () => {
                        object current = property.GetValue(model);
                        editor.Text = (current is float number && float.IsNaN(number)) || (current is int integer && integer == int.MinValue)
                            ? "" : property.Converter.ConvertToString(current);
                        editor.Modified = false;
                        errors.SetError(editor, "");
                    };

                    editor.Validating += (sender, args) =>
                    {
                        if (!editor.Modified) return;
                        try
                        {
                            object parsed = property.Converter.ConvertFromString(editor.Text);
                            if (parsed is float number && (float.IsNaN(number) || float.IsInfinity(number))) throw new FormatException();
                            property.SetValue(model, parsed);
                            editor.Modified = false;
                            errors.SetError(editor, "");
                        }
                        catch (Exception ex) when (ex is ArgumentException || ex is FormatException || ex is NotSupportedException || ex is OverflowException || ex.InnerException is FormatException || ex.InnerException is OverflowException)
                        {
                            errors.SetError(editor, UiText.Get("EnterNumber"));
                            args.Cancel = true;
                        }
                    };
                    Setting(page, UiText.Get(property.DisplayName), editor);
                }
                else if (value != null)
                {
                    var group = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Size = Size.Empty, Dock = DockStyle.Top, ColumnCount = 1, Margin = new Padding(0, 0, 0, 6) };
                    group.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                    var expand = SelectionControls.Button(UiText.Get(property.Category) + " · " + UiText.Get(property.DisplayName) + "   +");
                    expand.Dock = DockStyle.Fill;
                    expand.TextAlign = ContentAlignment.MiddleLeft;
                    var fields = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Size = Size.Empty, Dock = DockStyle.Top, ColumnCount = 1, Padding = new Padding(12), Visible = false };
                    fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                    CameraFields(fields, value, key + "_");
                    expand.Click += (sender, args) => { fields.Visible = !fields.Visible; expand.Text = UiText.Get(property.Category) + " · " + UiText.Get(property.DisplayName) + (fields.Visible ? "   −" : "   +"); };
                    group.Controls.Add(expand);
                    group.Controls.Add(fields);
                    page.Controls.Add(group);
                }
            }
        }
        protected override void SetVisibleCore(bool value)
        {
            if (value && !modernSettingsBuilt)
            {
                modernSettingsBuilt = true;
                BuildSettings();
                PerformLayout();
            }
            base.SetVisibleCore(value);
        }
        private TableLayoutPanel SettingsPage(string title)
        {
            var page = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Size = Size.Empty, Dock = DockStyle.Top, ColumnCount = 1, Padding = new Padding(14, 8, 14, 12) };
            page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            page.Controls.Add(new Label { Text = title, AutoSize = true, Font = new Font("Segoe UI", 18F, FontStyle.Bold), Margin = new Padding(0, 0, 0, 14) });
            return page;
        }

        private void Setting(TableLayoutPanel page, string title, Control editor)
        {
            var row = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Size = Size.Empty, Dock = DockStyle.Top, ColumnCount = 2, Margin = new Padding(0, 0, 0, 6) };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
            row.Controls.Add(new Label { Text = title, AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 4, 16, 4) }, 0, 0);
            editor.Anchor = editor.Name.StartsWith("toggle_") ? AnchorStyles.Left : AnchorStyles.Left | AnchorStyles.Right;
            editor.Margin = Padding.Empty;
            row.Controls.Add(SelectionControls.InputSurface(editor), 1, 0);
            page.Controls.Add(row);
        }

        private void BuildSettings()
        {
            SuspendLayout();
            inputErrors = new ErrorProvider { ContainerControl = this };
            Disposed += (sender, args) => inputErrors.Dispose();
            UiText.Apply(this);
            Text = "ReStarter — " + UiText.Get("Settings");
            splitContainer1.Visible = false; // Keep the existing values and localisation model.
            FormBorderStyle = FormBorderStyle.Sizable;
            Font = new Font("Segoe UI", 11F);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(16) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 194));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);
            root.BringToFront();
            var navigation = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            var content = new Panel { Dock = DockStyle.Fill };
            var viewports = new List<Panel>();
            root.Controls.Add(navigation, 0, 0);
            root.Controls.Add(content, 1, 0);
            var tabs = new List<TabPage>();
            var pages = new List<TableLayoutPanel>();
            foreach (TabPage old in tabControlOptions.TabPages)
            {
                if (old == tabUtils || old == tabAddon) continue;
                tabs.Add(old);
                pages.Add(SettingsPage(old.Text));
            }
            var graphics = pages[0];
            graphics.Controls.Add(new Label { Name = "gameSettingsHeading", AutoSize = true, Dock = DockStyle.Top, Text = UiText.Get("GameSettingsHeading"), Font = new Font(Font, FontStyle.Bold), Margin = new Padding(0, 18, 0, 8) });
            graphics.Controls.Add(new Label { AutoSize = true, Dock = DockStyle.Top, Text = UiText.Get("GameEngineHint"), Margin = new Padding(0, 0, 0, 12) });
            Setting(graphics, lbAdapter.Text, new Label { AutoSize = true, Text = UiText.Get("GameManagedAdapter") });
            graphics.Controls.Add(new Label { AutoSize = true, Dock = DockStyle.Top, Text = UiText.Get("GameSettingsDefaults"), Margin = new Padding(0, 0, 0, 12) });
            Setting(graphics, lbResolution.Text, SelectionControls.DropdownChoice(cbbResolutions, lbResolution.Text));
            Setting(graphics, lbAudio.Text, SelectionControls.InlineChoice(cbbAudio, lbAudio.Text));
            BuildOwnedRendererSettings(graphics);
            foreach (var check in new[] { cbMPConsoleSupport, cbMPDisableMQ, cbMPSecondSpeed })
                Setting(pages[1], check.Text, SelectionControls.Toggle(check));
            CameraFields(pages[2], propGridCamera.SelectedObject, "camera_");
            var fields = new[] { (lbPathCD, NTRtbPathCD, NTRbtBrowse1), (lbPathRes, NTRtbPathRes, NTRbtBrowse2), (lbPathGameWorkdir, NTRtbPathGameWork, NTRbtBrowse3) };
            foreach (var field in fields)
            {
                var line = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Size = Size.Empty, Dock = DockStyle.Top, ColumnCount = 2 };
                line.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                line.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                field.Item2.Dock = DockStyle.Fill;
                field.Item2.Font = Font;
                var browse = SelectionControls.Button("…");
                browse.AutoSize = false;
                browse.Size = new Size(40, 40);
                browse.MinimumSize = new Size(40, 40);
                browse.Margin = new Padding(8, 0, 0, 0);
                browse.Click += (sender, args) => {
                    if (field.Item3 == NTRbtBrowse1) NTRbtBrowse1_Click(sender, args);
                    else if (field.Item3 == NTRbtBrowse2) NTRbtBrowse2_Click(sender, args);
                    else NTRbtBrowse3_Click(sender, args);
                };
                line.Controls.Add(SelectionControls.InputSurface(field.Item2), 0, 0);
                line.Controls.Add(browse, 1, 0);
                Setting(pages[3], field.Item1.Text, line);
            }
            NTRtbExeName.Font = Font;
            Setting(pages[3], lbEXEName.Text, new Label { AutoSize = true, Text = @"Engine\EIRebuild.exe" });
            var savePaths = SelectionControls.Button(btApplyPaths.Text);
            savePaths.Click += btApplyPaths_Click;
            pages[3].Controls.Add(savePaths);
            for (int i = 0; i < pages.Count; i++)
            {
                int index = i;
                var button = SelectionControls.SectionButton(tabs[i].Text, tabs[i].Name);
                button.Name = "section_" + i;
                button.Click += (sender, args) =>
                {
                    content.SuspendLayout();
                    foreach (var viewport in viewports) viewport.Visible = false;
                    viewports[index].Visible = true;
                    viewports[index].BringToFront();
                    viewports[index].AutoScrollPosition = Point.Empty;
                    content.ResumeLayout(true);

                    foreach (Control entry in navigation.Controls) entry.Tag = entry == button ? "selected" : null;
                    LauncherTheme.Apply(navigation);
                };
                navigation.Controls.Add(button);
                var viewport = new Panel { Name = "settingsViewport_" + i, Dock = DockStyle.Fill, AutoScroll = true };
                viewport.Controls.Add(pages[i]);
                viewports.Add(viewport);
                content.Controls.Add(viewport);
                viewport.Visible = i == 0;
                pages[i].Visible = true;
                button.Tag = i == 0 ? "selected" : null;
            }
            Disposed += (sender, args) => { foreach (var page in pages) page.Dispose(); };
            var footer = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Size = Size.Empty, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 16, 0, 0) };
            var cancel = SelectionControls.Button(btCancel.Text);
            cancel.DialogResult = DialogResult.Cancel;
            cancel.CausesValidation = false;
            var apply = SelectionControls.Button(btOk.Text);
            apply.DialogResult = DialogResult.None;
            apply.Click += (sender, args) => SaveSettings(false);
            apply.Tag = "primary";
            footer.Controls.Add(cancel);
            footer.Controls.Add(apply);
            var reset = SelectionControls.Button(UiText.Get("ResetDefaults"));
            reset.Name = "resetDefaults";
            reset.CausesValidation = false;
            reset.Click += (sender, args) => ResetDefaults();
            footer.Controls.Add(reset);
            CancelButton = cancel;
            root.Controls.Add(footer, 0, 1);
            root.SetColumnSpan(footer, 2);
            LauncherTheme.ApplyBatched(root);
            float scale = DeviceDpi / 96F;
            if (scale != 1F) root.Scale(new SizeF(scale, scale));
            ClientSize = new Size((int)(900 * scale), (int)(650 * scale));
            MinimumSize = new Size((int)(820 * scale), (int)(550 * scale));
            ResumeLayout(true);
        }
    }
}
