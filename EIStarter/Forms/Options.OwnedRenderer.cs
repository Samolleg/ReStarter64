using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace EIStarter
{
    public partial class Options
    {
        private OwnedRendererSettings ownedRendererSettings;
        private ComboBox ownedRendererPreset;
        private readonly Dictionary<string, Control> ownedRendererControls = new Dictionary<string, Control>(StringComparer.OrdinalIgnoreCase);
        private bool applyingOwnedPreset;
        private int ownedPresetOffset;

        private static readonly string[] OwnedCategoryKeys = {
            "OwnedCategoryDisplay", "OwnedCategoryLighting", "OwnedCategoryTerrain",
            "OwnedCategoryWater", "OwnedCategoryVegetation", "OwnedCategoryAtmosphere",
            "OwnedCategoryEffects", "OwnedCategoryWorld", "OwnedCategoryInterface"
        };

        private static string OwnedChoiceLabel(string choice)
        {
            if (UiText.Language != "ru") return choice;
            switch (choice)
            {
                case "Borderless fullscreen": return "Без рамки на весь экран";
                case "Windowed": return "В окне";
                case "Original": return "Оригинальное";
                case "Faithful": return "Как в оригинале";
                case "Atmospheric": return "Атмосферный";
                case "Volumetric": return "Объёмный";
                case "Fast": return "Быстрое";
                case "Full": return "Полное";
                case "Fine": return "Точное";
                case "Simple": return "Простые";
                case "Both": return "Оба";
                case "Dense": return "Плотные";
                case "Normal": return "Обычное";
                case "Low": return "Низкое";
                case "Medium": return "Среднее";
                case "High": return "Высокое";
                case "Off": return UiText.Get("Off");
                case "On": return UiText.Get("On");
                default: return choice;
            }
        }

        private static string OwnedOptionHint(OwnedRendererOption option)
        {
            string range = option.Type == OwnedValueType.Boolean ? "0–1" :
                option.Minimum.ToString(CultureInfo.InvariantCulture) + "–" + option.Maximum.ToString(CultureInfo.InvariantCulture);
            string note = option.Key == "window_width" || option.Key == "window_height"
                ? UiText.Get("OwnedWindowAuto") : option.Key == "frame_limit" ? UiText.Get("OwnedFrameUnlimited") : "";
            return option.Key + " · " + range + (note.Length == 0 ? "" : " · " + note);
        }

        private void BuildOwnedRendererSettings(TableLayoutPanel graphics)
        {
            graphics.Controls.Add(new Label { Name = "ownedRendererHeading", Text = UiText.Get("OwnedRendererHeading"),
                AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(0, 18, 0, 8) });
            graphics.Controls.Add(new Label { Name = "ownedRendererF10Hint", Text = UiText.Get("OwnedRendererF10Hint"),
                AutoSize = true, MaximumSize = new Size(720, 0), Margin = new Padding(0, 0, 0, 10) });
            ownedRendererSettings = new OwnedRendererSettings(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Engine"));
            if (!ownedRendererSettings.Available)
            {
                graphics.Controls.Add(new Label { Name = "ownedRendererUnavailable", AutoSize = true,
                    Text = UiText.Get("OwnedRendererUnavailable") + " " + ownedRendererSettings.Error });
                return;
            }

            ownedRendererPreset = new ComboBox { Name = "ownedRendererPreset", DropDownStyle = ComboBoxStyle.DropDownList };
            ownedPresetOffset = ownedRendererSettings.FirstRunDone ? 0 : 1;
            if (ownedPresetOffset == 1) ownedRendererPreset.Items.Add(UiText.Get("OwnedPresetAuto"));
            for (int i = 0; i < 5; i++) ownedRendererPreset.Items.Add(UiText.Get("OwnedPreset" + i));
            ownedRendererPreset.SelectedIndex = ownedPresetOffset == 1 ? 0 : ownedRendererSettings.DetectPreset();
            var presetPicker = SelectionControls.InlineChoice(ownedRendererPreset, UiText.Get("OwnedRendererPreset"));
            presetPicker.Disposed += (sender, args) => ownedRendererPreset.Dispose();
            Setting(graphics, UiText.Get("OwnedRendererPreset"), presetPicker);
            graphics.Controls.Add(new Label { Text = UiText.Get("OwnedPresetNote"), AutoSize = true,
                MaximumSize = new Size(720, 0), Margin = new Padding(0, 0, 0, 12) });
            if (!ownedRendererSettings.FirstRunDone)
                graphics.Controls.Add(new Label { Text = UiText.Get("OwnedFirstRunNote"), AutoSize = true,
                    MaximumSize = new Size(720, 0), Margin = new Padding(0, 0, 0, 12) });

            for (int category = 0; category < OwnedCategoryKeys.Length; category++)
            {
                int categoryIndex = category;
                string heading = UiText.Get(OwnedCategoryKeys[category]);
                var group = new TableLayoutPanel { Name = "ownedCategory_" + category,
                    AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top,
                    ColumnCount = 1, Margin = new Padding(0, 0, 0, 6) };
                group.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                var expand = SelectionControls.Button(heading + (category == 0 ? "   −" : "   +"));
                expand.Name = "ownedCategoryButton_" + category;
                expand.Dock = DockStyle.Fill;
                expand.TextAlign = ContentAlignment.MiddleLeft;
                var fields = new TableLayoutPanel { Name = "ownedCategoryFields_" + category,
                    AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    Dock = DockStyle.Top, ColumnCount = 1, Padding = new Padding(12, 6, 0, 8),
                    Visible = category == 0 };
                fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                foreach (OwnedRendererOption option in OwnedRendererSchema.Options.Where(item => item.Category == categoryIndex))
                    AddOwnedOption(fields, option);
                expand.Click += (sender, args) =>
                {
                    fields.Visible = !fields.Visible;
                    expand.Text = heading + (fields.Visible ? "   −" : "   +");
                };
                group.Controls.Add(expand);
                group.Controls.Add(fields);
                graphics.Controls.Add(group);
            }
            ownedRendererPreset.SelectedIndexChanged += (sender, args) => ApplyOwnedPreset();
        }

        private void AddOwnedOption(TableLayoutPanel fields, OwnedRendererOption option)
        {
            string raw = ownedRendererSettings.GetRaw(option);
            Control editor;
            if (option.Type == OwnedValueType.Boolean || option.Choices != null)
            {
                var combo = new ComboBox { Name = "owned_" + option.Key, DropDownStyle = ComboBoxStyle.DropDownList };
                string[] choices = option.Type == OwnedValueType.Boolean
                    ? new[] { UiText.Get("Off"), UiText.Get("On") }
                    : option.Choices.Select(OwnedChoiceLabel).ToArray();
                combo.Items.AddRange(choices);
                if (OwnedRendererSettings.TryParse(option, raw, out decimal initial) &&
                    initial == decimal.Truncate(initial) && initial >= option.Minimum && initial <= option.Maximum)
                    combo.SelectedIndex = (int)(initial - option.Minimum);
                else
                {
                    combo.Items.Add(UiText.Get("OwnedKeepValue") + ": " + (raw.Length > 48 ? raw.Substring(0, 48) + "…" : raw));
                    combo.SelectedIndex = combo.Items.Count - 1;
                }
                editor = combo;
                var picker = SelectionControls.InlineChoice(combo, option.Label);
                picker.Disposed += (sender, args) => combo.Dispose();
                var tip = new ToolTip();
                tip.SetToolTip(picker, OwnedOptionHint(option));
                foreach (Control choice in picker.Controls) tip.SetToolTip(choice, OwnedOptionHint(option));
                picker.Disposed += (sender, args) => tip.Dispose();
                Setting(fields, option.Label, picker);
                combo.SelectedIndexChanged += (sender, args) => UpdateOwnedPreset();
            }
            else
            {
                var input = new TextBox { Name = "owned_" + option.Key, Text = raw, Font = Font };
                input.Tag = raw;
                editor = input;
                var tip = new ToolTip();
                tip.SetToolTip(input, OwnedOptionHint(option));
                input.Disposed += (sender, args) => tip.Dispose();
                Setting(fields, option.Label, input);
                input.TextChanged += (sender, args) => UpdateOwnedPreset();
            }
            ownedRendererControls.Add(option.Key, editor);
        }

        private string OwnedSelectedRaw(OwnedRendererOption option)
        {
            Control control = ownedRendererControls[option.Key];
            if (control is TextBox input) return option.Type == OwnedValueType.Float &&
                !string.Equals(input.Text, (string)input.Tag, StringComparison.Ordinal)
                ? input.Text.Replace(',', '.') : input.Text;
            var combo = (ComboBox)control;
            int count = option.Type == OwnedValueType.Boolean ? 2 : option.Choices.Length;
            return combo.SelectedIndex >= 0 && combo.SelectedIndex < count
                ? OwnedRendererSettings.Format(option, option.Minimum + combo.SelectedIndex)
                : ownedRendererSettings.GetRaw(option);
        }

        private void SetOwnedValue(OwnedRendererOption option, decimal value)
        {
            Control control = ownedRendererControls[option.Key];
            if (control is TextBox input) input.Text = OwnedRendererSettings.Format(option, value);
            else ((ComboBox)control).SelectedIndex = (int)(value - option.Minimum);
        }

        private void RestoreOwnedInitialValues()
        {
            foreach (OwnedRendererOption option in OwnedRendererSchema.Options)
            {
                string raw = ownedRendererSettings.GetRaw(option);
                Control control = ownedRendererControls[option.Key];
                if (control is TextBox input) { input.Text = raw; continue; }
                var combo = (ComboBox)control;
                int count = option.Type == OwnedValueType.Boolean ? 2 : option.Choices.Length;
                combo.SelectedIndex = OwnedRendererSettings.TryParse(option, raw, out decimal value) &&
                    value == decimal.Truncate(value) && value >= option.Minimum && value - option.Minimum < count
                    ? (int)(value - option.Minimum) : count;
            }
        }

        private void ApplyOwnedPreset()
        {
            int preset = ownedRendererPreset.SelectedIndex - ownedPresetOffset;
            if (applyingOwnedPreset) return;
            if (ownedPresetOffset == 1 && ownedRendererPreset.SelectedIndex == 0)
            {
                applyingOwnedPreset = true;
                try { RestoreOwnedInitialValues(); ownedRendererSettings.ConfirmFirstRun = false; }
                finally { applyingOwnedPreset = false; }
                return;
            }
            if (preset < 0 || preset >= 4) return;
            applyingOwnedPreset = true;
            try
            {
                ownedRendererSettings.ConfirmFirstRun = true;
                foreach (OwnedRendererOption option in OwnedRendererSchema.Options.Where(item => item.InPreset))
                    SetOwnedValue(option, option.Presets[preset]);
            }
            finally { applyingOwnedPreset = false; }
            UpdateOwnedPreset();
        }

        private void UpdateOwnedPreset()
        {
            if (applyingOwnedPreset || ownedRendererPreset == null) return;
            var selected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (OwnedRendererOption option in OwnedRendererSchema.Options.Where(item => item.InPreset))
                selected[option.Key] = OwnedSelectedRaw(option);
            bool changedPreset = OwnedRendererSchema.Options.Where(option => option.InPreset).Any(option =>
            {
                string before = ownedRendererSettings.GetRaw(option), after = OwnedSelectedRaw(option);
                if (string.Equals(before, after, StringComparison.Ordinal)) return false;
                return !OwnedRendererSettings.TryParse(option, before, out decimal original) ||
                    !OwnedRendererSettings.TryParse(option, after, out decimal current) ||
                    Math.Abs(original - current) >= .0001m;
            });
            int detected = ownedPresetOffset == 1 && !changedPreset && !ownedRendererSettings.ConfirmFirstRun
                ? 0 : ownedRendererSettings.DetectPreset(selected) + ownedPresetOffset;
            if (ownedRendererPreset.SelectedIndex != detected)
            {
                applyingOwnedPreset = true;
                try { ownedRendererPreset.SelectedIndex = detected; }
                finally { applyingOwnedPreset = false; }
            }
        }

        private Dictionary<string, string> CollectOwnedEdits()
        {
            var edits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (OwnedRendererOption option in OwnedRendererSchema.Options)
            {
                string current = OwnedSelectedRaw(option);
                if (!string.Equals(current, ownedRendererSettings.GetRaw(option), StringComparison.Ordinal))
                    edits.Add(option.Key, current);
            }
            if (edits.ContainsKey("window_width") || edits.ContainsKey("window_height"))
            {
                bool widthValid = OwnedRendererSettings.TryParse(OwnedRendererSchema.ByKey["window_width"],
                    OwnedSelectedRaw(OwnedRendererSchema.ByKey["window_width"]), out decimal width);
                bool heightValid = OwnedRendererSettings.TryParse(OwnedRendererSchema.ByKey["window_height"],
                    OwnedSelectedRaw(OwnedRendererSchema.ByKey["window_height"]), out decimal height);
                if (!widthValid || !heightValid || (width == 0) != (height == 0))
                    throw new InvalidOperationException(UiText.Get("OwnedWindowPair"));
            }
            return edits;
        }

        private void CommitSettings(bool pathsOnly, Action saveRegistry)
        {
            if (pathsOnly || ownedRendererSettings == null || !ownedRendererSettings.Available)
            { saveRegistry(); return; }
            ownedRendererSettings.Commit(CollectOwnedEdits(), saveRegistry);
        }
    }
}
