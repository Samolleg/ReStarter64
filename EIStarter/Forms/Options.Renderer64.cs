#if RESTARTER64
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
        private readonly Dictionary<string, string> ownedPending64 =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Control> ownedSurfaces64 =
            new Dictionary<string, Control>(StringComparer.OrdinalIgnoreCase);
        private ToolTip ownedTips64;
        private Control dlssButton64;
        private Label dlssStatus64;
        private NvidiaAdapter.Detection? nvidia64;
        private static string Text64(string english, string russian) => UiText.Language == "ru" ? russian : english;

        private void BuildOwnedRendererSettings64(TableLayoutPanel graphics)
        {
            ownedRendererSettings = new OwnedRendererSettings(
                Path.GetDirectoryName(LauncherProfile.Executable(AppDomain.CurrentDomain.BaseDirectory)));
            graphics.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(720, 0),
                Text = Text64("These settings match F10. Changes apply on the next game start.",
                    "Настройки соответствуют F10. Изменения применяются при следующем запуске игры."),
                Margin = new Padding(0, 0, 0, 12) });
            if (!ownedRendererSettings.Available)
            {
                graphics.Controls.Add(new Label { AutoSize = true,
                    Text = UiText.Get("OwnedRendererUnavailable") + " " + ownedRendererSettings.Error });
                return;
            }
            ownedTips64 = new ToolTip();
            Disposed += (sender, args) => ownedTips64.Dispose();
            AddOwnedOption(graphics, OwnedRendererSchema.ByKey["ui_language"]);
            ownedRendererPreset = new ComboBox { Name = "ownedRendererPreset", DropDownStyle = ComboBoxStyle.DropDownList };
            ownedPresetOffset = ownedRendererSettings.FirstRunDone ? 0 : 1;
            if (ownedPresetOffset == 1) ownedRendererPreset.Items.Add(UiText.Get("OwnedPresetAuto"));
            ownedRendererPreset.Items.AddRange(UiText.Language == "ru"
                ? new[] { "Низкое", "Среднее", "Высокое", "Ультра", "Своё" }
                : new[] { "Low", "Medium", "High", "Ultra", "Custom" });
            ownedRendererPreset.SelectedIndex = ownedPresetOffset == 1 ? 0 : ownedRendererSettings.DetectPreset();
            var presets = SelectionControls.DropdownChoice(ownedRendererPreset, Text64("Preset", "Пресет"));
            Setting(graphics, Text64("Preset", "Пресет"), presets);
            graphics.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(720, 0),
                Text = Text64("HUD values also update the active interface profile: ", "Настройки HUD также сохраняются в активный профиль интерфейса: ")
                    + ownedRendererSettings.InterfaceName64 + ". " +
                    Text64("Drag widgets, manage profiles and view live memory/effect data in F10.",
                        "Перемещение панелей, управление профилями и текущие данные памяти/эффектов доступны в F10."),
                Margin = new Padding(0, 0, 0, 12) });

            foreach (int category in OwnedRendererSchema64.CategoryOrder)
            {
                if (category == 10) continue;
                string title = OwnedRendererSchema64.Category(category);
                bool open = category == 0 || category == 10;
                bool built = false;
                var group = new TableLayoutPanel { Name = "ownedCategory_" + category, Dock = DockStyle.Top,
                    AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1,
                    Margin = new Padding(0, 0, 0, 6) };
                group.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                group.SuspendLayout();
                var expand = SelectionControls.Button(title + (open ? "   −" : "   +"));
                expand.Dock = DockStyle.Fill;
                expand.TextAlign = ContentAlignment.MiddleLeft;
                var fields = new TableLayoutPanel { Name = "ownedCategoryFields_" + category, Dock = DockStyle.Top,
                    AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1,
                    Padding = new Padding(12, 6, 0, 8), Visible = open };
                fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                Action populate = () =>
                {
                    if (built) return;
                    built = true;
                    fields.SuspendLayout();
                    foreach (var option in OwnedRendererSchema.Options.Where(item => item.Category == category))
                        AddOwnedOption(fields, option);
                    LauncherTheme.ApplyBatched(fields);
                    fields.ResumeLayout(true);
                    RefreshOwnedAvailability64();
                };
                if (open) populate();
                expand.Click += (sender, args) =>
                {
                    group.SuspendLayout();
                    populate();
                    fields.Visible = !fields.Visible;
                    expand.Text = title + (fields.Visible ? "   −" : "   +");
                    group.ResumeLayout(true);
                };
                group.Controls.Add(expand);
                group.Controls.Add(fields);
                group.ResumeLayout(false);
                graphics.Controls.Add(group);
            }
            ownedRendererPreset.SelectedIndexChanged += (sender, args) => ApplyOwnedPreset();
        }

        private void AddOwnedOption64(TableLayoutPanel fields, OwnedRendererOption option)
        {
            string raw = OwnedSelectedRaw(option);
            Control editor, surface;
            if (option.Type == OwnedValueType.Boolean || option.Choices != null)
            {
                var combo = new ComboBox { Name = "owned_" + option.Key, DropDownStyle = ComboBoxStyle.DropDownList };
                string[] labels = option.Type == OwnedValueType.Boolean
                    ? new[] { Text64("Off", "Выкл."), Text64("On", "Вкл.") }
                    : UiText.Language == "ru" ? option.RussianChoices : option.Choices;
                combo.Items.AddRange(labels);
                if (OwnedRendererSettings.TryParse(option, raw, out decimal initial) &&
                    initial == decimal.Truncate(initial) && initial >= option.Minimum && initial <= option.Maximum)
                    combo.SelectedIndex = (int)(initial - option.Minimum);
                else
                {
                    combo.Items.Add(UiText.Get("OwnedKeepValue") + ": " + raw);
                    combo.SelectedIndex = combo.Items.Count - 1;
                }
                editor = combo;
                surface = option.Key == "upscaler_quality" || labels.Length > 5
                    ? SelectionControls.DropdownChoice(combo, option.Label)
                    : SelectionControls.InlineChoice(combo, option.Label);
                surface.Disposed += (sender, args) => combo.Dispose();
                combo.SelectedIndexChanged += (sender, args) => UpdateOwnedPreset();
                if (option.Key == "upscaler")
                {
                    dlssButton64 = surface.Controls[1];
                    dlssButton64.Enabled = false;
                    dlssStatus64 = new Label { AutoSize = true, MaximumSize = new Size(720, 0),
                        Text = Text64("Checking NVIDIA adapter…", "Проверка адаптера NVIDIA…"),
                        Margin = new Padding(0, 0, 0, 8) };
                    Shown += async (sender, args) =>
                    {
                        var result = await NvidiaAdapter.Result;
                        if (IsDisposed || dlssButton64.IsDisposed) return;
                        nvidia64 = result;
                        dlssButton64.Enabled = result == NvidiaAdapter.Detection.Present;
                        dlssStatus64.Text = result == NvidiaAdapter.Detection.Present
                            ? Text64("NVIDIA detected. The game checks DLSS support.", "NVIDIA обнаружена. Поддержку DLSS проверит игра.")
                            : result == NvidiaAdapter.Detection.Absent
                            ? Text64("DLSS unavailable: no NVIDIA adapter detected.", "DLSS недоступен: адаптер NVIDIA не обнаружен.")
                            : Text64("DLSS unavailable: DXGI adapter enumeration failed.", "DLSS недоступен: не удалось получить список адаптеров DXGI.");
                        ownedTips64.SetToolTip(dlssButton64, dlssStatus64.Text);
                        RefreshOwnedAvailability64();
                    };
                }
            }
            else
            {
                var input = new TextBox { Name = "owned_" + option.Key, Text = raw, Tag = raw, Font = Font };
                editor = input;
                if (option.Type == OwnedValueType.Text) surface = input;
                else
                {
                    var line = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                        Dock = DockStyle.Top, ColumnCount = 3, Margin = Padding.Empty };
                    line.SuspendLayout();
                    line.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                    line.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38));
                    line.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38));
                    line.Controls.Add(SelectionControls.InputSurface(input), 0, 0);
                    for (int direction = -1; direction <= 1; direction += 2)
                    {
                        int delta = direction;
                        var step = SelectionControls.Button(delta < 0 ? "−" : "+");
                        step.AutoSize = false;
                        step.MinimumSize = new Size(32, 32);
                        step.Size = new Size(32, 32);
                        step.Padding = Padding.Empty;
                        step.Margin = new Padding(3, 0, 0, 0);
                        step.Click += (sender, args) =>
                        {
                            if (!OwnedRendererSettings.TryParse(option, input.Text.Replace(',', '.'), out decimal value))
                                value = option.Default;
                            input.Text = OwnedRendererSettings.Format(option,
                                Math.Max(option.Minimum, Math.Min(option.Maximum, value + delta * option.Step)));
                        };
                        line.Controls.Add(step, delta < 0 ? 1 : 2, 0);
                    }
                    line.ResumeLayout(false);
                    surface = line;
                    if (option.Key == "frame_limit" || option.Key == "render_scale")
                    {
                        int[] values = option.Key == "frame_limit"
                            ? new[] { 0, 30, 60, 90, 120, 144, 165, 240 }
                            : new[] { 50, 67, 75, 85, 100 };
                        var standards = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList,
                            Dock = DockStyle.Top, Font = Font };
                        standards.Items.AddRange(values.Select(value => value == 0 ? Text64("Off", "Выкл.") :
                            value + (option.Key == "render_scale" ? "%" : " FPS")).ToArray());
                        standards.Items.Add(Text64("Custom", "Своё"));
                        bool syncing = false;
                        Action sync = () =>
                        {
                            syncing = true;
                            standards.SelectedIndex = int.TryParse(input.Text, out int value) && Array.IndexOf(values, value) >= 0
                                ? Array.IndexOf(values, value) : values.Length;
                            syncing = false;
                        };
                        standards.SelectedIndexChanged += (sender, args) =>
                        {
                            if (!syncing && standards.SelectedIndex < values.Length)
                                input.Text = values[standards.SelectedIndex].ToString(CultureInfo.InvariantCulture);
                        };
                        input.TextChanged += (sender, args) => sync();
                        sync();
                        line.RowCount = 2;
                        line.Controls.Add(standards, 0, 1);
                        line.SetColumnSpan(standards, 3);
                    }
                }
                input.TextChanged += (sender, args) => UpdateOwnedPreset();
            }
            ownedRendererControls.Add(option.Key, editor);
            Setting(fields, option.Label, surface);
            // Setting may wrap a plain text field in an input surface.
            ownedSurfaces64.Add(option.Key, surface);
            string hint = option.Type == OwnedValueType.Text
                ? Text64("Effect IDs and priorities, as in F10.", "Идентификаторы эффектов и приоритеты, как в F10.")
                : option.Minimum.ToString(CultureInfo.InvariantCulture) + "–" +
                    option.Maximum.ToString(CultureInfo.InvariantCulture) + Text64(" · step ", " · шаг ") +
                    option.Step.ToString(CultureInfo.InvariantCulture);
            ownedTips64.SetToolTip(surface, hint);
            foreach (Control child in surface.Controls) ownedTips64.SetToolTip(child, hint);
            if (option.Key == "upscaler") fields.Controls.Add(dlssStatus64);
        }

        private decimal Number64(string key)
        {
            var option = OwnedRendererSchema.ByKey[key];
            return OwnedRendererSettings.TryParse(option, OwnedSelectedRaw(option), out decimal value) ? value : option.Default;
        }

        private void RefreshOwnedAvailability64()
        {
            if (ownedRendererSettings == null || !ownedRendererSettings.Available) return;
            foreach (var pair in ownedSurfaces64)
            {
                string key = pair.Key;
                bool enabled = true;
                if (key == "upscaler_quality" || key == "upscale_sharpness")
                    enabled = Number64("upscaler") != 0 && !(Number64("upscaler") == 1 &&
                        nvidia64 != NvidiaAdapter.Detection.Present);
                if (key == "render_scale" || key == "smaa") enabled = Number64("upscaler") == 0;
                if (key == "water_reflections" || key == "water_reflection_quality" || key == "water_physical")
                    enabled = Number64("water_simple") == 0 &&
                        (key == "water_reflections" || Number64("water_reflections") != 0);
                if (key == "shadow_cascade_quality" || key == "shadow_close_cascades" || key == "shadow_far")
                    enabled = Number64("shadows") != 0;
                if (key == "point_shadow_quality") enabled = Number64("point_shadows") != 0;
                if (key == "shadow_softness") enabled = Number64("shadows") != 0 || Number64("point_shadows") != 0;
                if (key == "terrain_cliffs") enabled = Number64("terrain_blend") != 0;
                if (key == "neighbour_fog_range") enabled = Number64("neighbour_maps") != 0;
                if (key == "ground_cover_distance") enabled = Number64("ground_cover") != 0;
                if (key == "fog_volume_quality") enabled = Number64("fog_preset") >= 2;
                if (key == "fog_strength") enabled = Number64("fog_preset") != 0;
                if (key == "depth_of_field_strength") enabled = Number64("depth_of_field") != 0;
                if (key.EndsWith("_fade_transparency", StringComparison.Ordinal))
                    enabled = Number64(key.Replace("_fade_transparency", "_auto_fade")) != 0;
                if (key.EndsWith("_weight", StringComparison.Ordinal) && key.StartsWith("critter_", StringComparison.Ordinal))
                    enabled = Number64(key.Substring(0, key.Length - "_weight".Length)) != 0;
                if (key == "combat_plate_alt" || key == "combat_plate_paused") enabled = Number64("combat_plate_combat") == 0;
                pair.Value.Enabled = enabled;
            }
            if (ownedRendererControls.TryGetValue("upscaler_quality", out Control control))
            {
                var combo = (ComboBox)control;
                string label = Number64("upscaler") == 1 ? "DLAA" : Text64("Native AA", "Сглаживание (полное разрешение)");
                if (!Equals(combo.Items[4], label)) combo.Items[4] = label;
            }
        }
    }
}
#endif

