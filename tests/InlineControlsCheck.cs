using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

// No windows are displayed and no registry or game profile is used.
class InlineControlsCheck
{
    static int passed;
    const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;
    const BindingFlags NonPublicInstance = BindingFlags.NonPublic | BindingFlags.Instance;
    static void Check(bool value, string text)
    { if (!value) throw new Exception(text); Console.WriteLine("PASS " + text); passed++; }
    static void Click(Control button)
    { typeof(Button).GetMethod("OnClick", NonPublicInstance).Invoke(button, new object[] { EventArgs.Empty }); }
    [STAThread] static int Main(string[] args)
    {
        try
        {
            var assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            var selections = assembly.GetType("EIStarter.SelectionControls", true);
            var theme = assembly.GetType("EIStarter.LauncherTheme", true);
            using (var combo = new ComboBox { Name = "validation" })
            {
                combo.Items.AddRange(new object[] { "Off", "On", "Keep value: custom-future-value" });
                combo.SelectedIndex = 2;
                using (var strip = (Control)selections.GetMethod("InlineChoice", PublicStatic).Invoke(null, new object[] { combo, "Shadows" }))
                {
                    Check(combo.SelectedIndex == 2 && strip.Controls[2].Tag as string == "selected", "construction preserves unknown selection");
                    Click(strip.Controls[1]);
                    Check(combo.SelectedIndex == 1 && strip.Controls.Cast<Control>().Count(c => c.Tag as string == "selected") == 1, "click selects one choice");
                    combo.SelectedIndex = 0;
                    Check(strip.Controls[0].Tag as string == "selected", "external model change refreshes buttons");
                    strip.Controls[0].GetType().GetMethod("OnKeyDown", NonPublicInstance).Invoke(strip.Controls[0], new object[] { new KeyEventArgs(Keys.Right) });
                    Check(combo.SelectedIndex == 1, "arrow key selects adjacent choice");
                    strip.Controls[1].GetType().GetMethod("OnKeyDown", NonPublicInstance).Invoke(strip.Controls[1], new object[] { new KeyEventArgs(Keys.Home) });
                    Check(combo.SelectedIndex == 0, "Home key selects first choice");
                    Check(strip.Controls[0].AccessibilityObject.Role == AccessibleRole.RadioButton &&
                        (strip.Controls[0].AccessibilityObject.State & AccessibleStates.Checked) != 0, "selection exposed to accessibility");
                    foreach (int width in new[] { 180, 420 })
                    {
                        strip.Size = new Size(width, 100);
                        Size preferred = strip.GetPreferredSize(new Size(width, 0));
                        strip.Size = new Size(width, preferred.Height);
                        strip.PerformLayout();
                        Check(strip.Controls.Cast<Control>().All(c => c.Right <= width && c.Bottom <= strip.Height), "wrapped choices remain inside width " + width);
                    }
                    Check(strip.GetPreferredSize(new Size(1, 0)).Height == strip.GetPreferredSize(new Size(strip.Width, 0)).Height,
                        "unconstrained table measurement does not reserve one row per button");
                }
                combo.SelectedIndex = 1; // Disposed view must detach its handlers.
                Check(combo.SelectedIndex == 1, "disposed view detaches cleanly");
            }
            using (var stream = assembly.GetManifestResourceStream("EIStarter.AppIcon"))
            {
                Check(stream != null, "shared application icon is embedded");
                using (var reader = new BinaryReader(stream))
                {
                    Check(reader.ReadUInt16() == 0 && reader.ReadUInt16() == 1 && reader.ReadUInt16() == 7, "application ICO contains seven sizes");
                    int[] sizes = Enumerable.Range(0, 7).Select(i => { int size = reader.ReadByte(); reader.ReadBytes(15); return size == 0 ? 256 : size; }).ToArray();
                    Check(sizes.SequenceEqual(new[] { 16, 24, 32, 48, 64, 128, 256 }), "ICO covers title bars through large Explorer icons");
                }
            }
            foreach (bool dark in new[] { true, false })
            {
                theme.GetField("Dark", PublicStatic).SetValue(null, dark);
                using (var panel = new FlowLayoutPanel { Size = new Size(206, 205), FlowDirection = FlowDirection.TopDown, WrapContents = false })
                {
                    var text = assembly.GetType("EIStarter.UiText", true);
                    text.GetProperty("Language", PublicStatic).SetValue(null, "ru", null);
                    foreach (string key in new[] { "tabGraphics", "tabMultiplayer", "tabCamera", "tabPaths" })
                    {
                        string label = (string)text.GetMethod("Get", PublicStatic).Invoke(null, new object[]{key});
                        var button = (Button)selections.GetMethod("SectionButton", PublicStatic).Invoke(null, new object[] { label, key });
                        button.Tag = key == "tabGraphics" ? "selected" : null;
                        panel.Controls.Add(button);
                    }
                    theme.GetMethod("ApplyBatched", PublicStatic).Invoke(null, new object[] { panel });
                    panel.PerformLayout();
                    using (var bitmap = new Bitmap(panel.Width, panel.Height))
                    {
                        panel.DrawToBitmap(bitmap, panel.ClientRectangle);
                        Check(panel.Controls.Cast<Control>().All(c => c.Padding.Left >= 40 && c.AccessibleName == c.Text), "section text and accessibility retained in " + (dark ? "dark" : "light") + " theme");
                        if (args.Length > 1) bitmap.Save(Path.Combine(args[1], "sections-" + (dark ? "dark" : "light") + ".png"));
                    }
                }
            }
            Console.WriteLine("PASS total=" + passed);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
