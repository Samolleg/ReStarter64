using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

class OwnedRendererCheck
{
    const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
    const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    static Type modelType, schemaType, formType;
    static IDictionary options;
    static string engine, ini, dll, rddraw, registryRoot;
    static int passed;

    static object Field(object obj, string name) { return obj.GetType().GetField(name, Instance).GetValue(obj); }
    static object OptionField(object obj, string name) { return obj.GetType().GetField(name, Instance).GetValue(obj); }
    static object Invoke(object obj, string name, params object[] args) { return obj.GetType().GetMethod(name, Instance).Invoke(obj, args); }
    static bool Available(object obj) { return (bool)modelType.GetProperty("Available", Instance).GetValue(obj, null); }
    static object Open() { return Activator.CreateInstance(modelType, Instance, null, new object[] { engine }, null); }
    static string Raw(object obj, string key) { return (string)Invoke(obj, "GetRaw", options[key]); }
    static int Preset(object obj) { return (int)Invoke(obj, "DetectPreset", (object)null); }
    static Dictionary<string, string> Edit(string key, string value) { return new Dictionary<string, string> { { key, value } }; }
    static void Commit(object obj, IDictionary<string, string> edits, Action other) { Invoke(obj, "Commit", edits, other); }
    static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        passed++;
        Console.WriteLine("PASS " + message);
    }
    static void Reject(Action action, string message)
    {
        bool rejected = false;
        try { action(); }
        catch (TargetInvocationException ex)
        {
            rejected = ex.InnerException is IOException || ex.InnerException is InvalidOperationException ||
                ex.InnerException is UnauthorizedAccessException;
        }
        Check(rejected, message);
    }
    static Control Find(Control parent, string name)
    {
        if (parent.Name == name) return parent;
        foreach (Control child in parent.Controls)
        {
            Control found = Find(child, name);
            if (found != null) return found;
        }
        return null;
    }
    static void Click(Control control)
    { typeof(Button).GetMethod("OnClick", Instance).Invoke(control, new object[] { EventArgs.Empty }); }
    static byte[] Bytes(string text, Encoding encoding) { return encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray(); }
    static void Write(string text) { File.WriteAllText(ini, text, new UTF8Encoding(false)); }

    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            var assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            modelType = assembly.GetType("EIStarter.OwnedRendererSettings", true);
            schemaType = assembly.GetType("EIStarter.OwnedRendererSchema", true);
            formType = assembly.GetType("EIStarter.Options", true);
            options = (IDictionary)schemaType.GetField("ByKey", Static).GetValue(null);
            var listed = (Array)schemaType.GetField("Options", Static).GetValue(null);
            engine = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Engine");
            Directory.CreateDirectory(engine);
            ini = Path.Combine(engine, "eid3d11.ini");
            dll = Path.Combine(engine, "DDraw.dll");
            rddraw = Path.Combine(engine, "RDDraw.dll");
            registryRoot = @"Software\EIStarterValidation\owned-" + Guid.NewGuid().ToString("N");
            File.WriteAllBytes(dll, new byte[] { 0x11 }); // Presence only; never loaded or hashed.

            Check(listed.Length == 50 && options.Count == 50 &&
                (int)schemaType.GetField("PlayerSettingCount", Static).GetRawConstantValue() == 49,
                "all 49 player settings plus window height are present and unique");
            Check(options.Contains("window_width") && options.Contains("window_height") &&
                options.Contains("cave_brightness") && options.Contains("highlight_hover_objects"),
                "display, lighting and interface endpoints are mapped");
            foreach (object option in listed)
            {
                decimal initial = (decimal)OptionField(option, "Default");
                decimal min = (decimal)OptionField(option, "Minimum");
                decimal max = (decimal)OptionField(option, "Maximum");
                decimal[] presets = (decimal[])OptionField(option, "Presets");
                Check(initial >= min && initial <= max && (presets == null ||
                    (presets.Length == 4 && presets[2] == initial && presets.All(value => value >= min && value <= max))),
                    "valid defaults and four preset values: " + OptionField(option, "Key"));
            }
            Check(Available(Open()) && !File.Exists(ini) && Preset(Open()) == 2, "missing INI uses registered High defaults");
            int callbacks = 0;
            using (var firstRun = (Form)Activator.CreateInstance(formType, Instance, null, new object[] { registryRoot }, null))
            {
                Invoke(firstRun, "BuildSettings");
                var picker = (ComboBox)Field(firstRun, "ownedRendererPreset");
                Check(picker.Items.Count == 6 && picker.SelectedIndex == 0,
                    "first-run UI offers automatic in-game choice before four presets");
                var inlinePreset = Find(firstRun, "choice_ownedRendererPreset");
                Check(inlinePreset is FlowLayoutPanel && inlinePreset.Controls.Count == 6 &&
                    inlinePreset.Controls.Cast<Control>().All(c => c is Button), "first-run preset uses six inline buttons");
                var bindings = (IDictionary)Field(firstRun, "ownedRendererControls");
                Check(bindings.Keys.Cast<string>().Where(key => bindings[key] is ComboBox).All(key =>
                    Find(firstRun, "choice_owned_" + key) is FlowLayoutPanel), "all renderer bool and enum choices are inline");
                var shadows = Find(firstRun, "choice_owned_shadows");
                int openForms = Application.OpenForms.Count;
                Click(shadows.Controls[0]);
                Check(((ComboBox)bindings["shadows"]).SelectedIndex == 0 &&
                    shadows.Controls[0].Tag as string == "selected" && Application.OpenForms.Count == openForms &&
                    !File.Exists(ini), "actual shadow button updates pending value without a dialog or write");
                Click(shadows.Controls[1]);
                Check(((ComboBox)bindings["shadows"]).SelectedIndex == 1 &&
                    shadows.Controls[1].Tag as string == "selected", "actual shadow buttons reflect restored selection");
                Click(inlinePreset.Controls[1]);
                Check(picker.SelectedIndex == 1 && ((ComboBox)bindings["anisotropy"]).SelectedIndex == 2,
                    "actual Low preset button updates dependent inline choices");
                Click(inlinePreset.Controls[0]);
                Invoke(firstRun, "CommitSettings", false, (Action)(() => callbacks++));
                Check(!File.Exists(ini), "returning to automatic first-run choice leaves INI absent");
                picker.SelectedIndex = 3; // High, equal to defaults but explicitly requested.
                Invoke(firstRun, "CommitSettings", false, (Action)(() => callbacks++));
                Check(File.ReadAllText(ini).Contains("first_run_done=true") &&
                    !File.ReadAllText(ini).Contains("render_scale="),
                    "explicit High locks the first-run choice without rewriting graphics defaults");
            }
            File.Delete(ini);
            callbacks = 0;
            Commit(Open(), Edit("vsync", "true"), () => callbacks++);
            Check(File.ReadAllText(ini).Contains("vsync=true") && !File.ReadAllText(ini).Contains("first_run_done"),
                "independent option keeps the first-run graphics suggestion available");
            using (var firstRun = (Form)Activator.CreateInstance(formType, Instance, null, new object[] { registryRoot }, null))
            {
                Invoke(firstRun, "BuildSettings");
                Check(((ComboBox)Field(firstRun, "ownedRendererPreset")).SelectedIndex == 0,
                    "first-run UI still shows automatic preset after independent edit");
            }
            File.Delete(ini);
            callbacks = 0;
            Commit(Open(), new Dictionary<string, string>(), () => callbacks++);
            Check(callbacks == 1 && !File.Exists(ini), "no-op does not create an INI");
            Reject(() => Commit(Open(), Edit("render_scale", "85"), () => { throw new IOException("registry failure"); }),
                "failed downstream write propagates for missing INI");
            Check(!File.Exists(ini), "missing INI creation rolls back on downstream failure");
            Commit(Open(), Edit("render_scale", "85"), () => callbacks++);
            Check(File.ReadAllText(ini).Contains("render_scale=85") && File.ReadAllText(ini).Contains("first_run_done=true"),
                "first explicit edit creates INI and prevents first-run preset from overwriting it");

            foreach (Encoding encoding in new Encoding[] { new UTF8Encoding(false), new UTF8Encoding(true), Encoding.GetEncoding(1251) })
            foreach (string newline in new[] { "\r\n", "\n" })
            {
                string original = ("; Ёжик\n[other]\nunknown=keep\n[setup]\nfirst_run_done=false\n[graphics]\nsmaa=true\nfuture=42\nrender_scale=100\n").Replace("\n", newline);
                File.WriteAllBytes(ini, Bytes(original, encoding));
                var before = File.ReadAllBytes(ini);
                var settings = Open();
                Check(Available(settings) && Raw(settings, "smaa") == "true", "load supported bytes and line endings");
                File.SetAttributes(ini, FileAttributes.ReadOnly);
                Commit(settings, Edit("smaa", "on"), () => callbacks++);
                Check(before.SequenceEqual(File.ReadAllBytes(ini)), "semantic no-op is byte exact on read-only INI");
                File.SetAttributes(ini, FileAttributes.Normal);
                Commit(settings, Edit("smaa", "false"), () => callbacks++);
                string expected = original.Replace("first_run_done=false", "first_run_done=true").Replace("smaa=true", "smaa=false");
                Check(Bytes(expected, encoding).SequenceEqual(File.ReadAllBytes(ini)),
                    "only selected setting and first-run marker change; unknown bytes survive");
            }
            Write("[setup]\nfirst_run_done=true\n[graphics]\nfog_strength=0,7\nfuture=42\n");
            byte[] untouchedInvalid = File.ReadAllBytes(ini);
            using (var form = (Form)Activator.CreateInstance(formType, Instance, null, new object[] { registryRoot }, null))
            {
                Invoke(form, "BuildSettings");
                var controls = (IDictionary)Field(form, "ownedRendererControls");
                Invoke(form, "CommitSettings", false, (Action)(() => callbacks++));
                Check(untouchedInvalid.SequenceEqual(File.ReadAllBytes(ini)), "unchanged invalid raw value is byte exact");
                ((TextBox)controls["fog_strength"]).Text = "0,8";
                Invoke(form, "CommitSettings", false, (Action)(() => callbacks++));
                Check(File.ReadAllText(ini).Contains("fog_strength=0.8") && File.ReadAllText(ini).Contains("future=42"),
                    "explicit Russian decimal input becomes renderer-compatible dot without losing unknown keys");
            }
            foreach (string broken in new[] {
                "[graphics]\nsmaa=true\nsmaa=false\n", "[graphics]\nsmaa=true\n[graphics]\nrender_scale=100\n",
                "[setup]\nfirst_run_done=true\n[setup]\nfirst_run_done=false\n",
                "[graphics]\nsmaa=true\0\n" })
            {
                Write(broken);
                Check(!Available(Open()), "ambiguous or unsupported INI is unavailable");
            }
            Write("[setup]\nfirst_run_done=true\n[graphics]\nsmaa=true\nunknown=keep\n");
            var stale = Open();
            File.AppendAllText(ini, "; changed elsewhere\n");
            byte[] external = File.ReadAllBytes(ini);
            Reject(() => Commit(stale, Edit("smaa", "false"), () => callbacks++), "external edit rejects changed save");
            Check(external.SequenceEqual(File.ReadAllBytes(ini)), "external edit is preserved");
            Write("[setup]\nfirst_run_done=true\n[graphics]\nsmaa=true\nunknown=keep\n");
            var rollback = Open();
            byte[] snapshot = File.ReadAllBytes(ini);
            Reject(() => Commit(rollback, Edit("smaa", "false"), () => { throw new IOException("injected registry error"); }),
                "downstream error propagates");
            Check(snapshot.SequenceEqual(File.ReadAllBytes(ini)) && Raw(rollback, "smaa") == "true",
                "downstream error restores bytes and model");
            Reject(() => Commit(Open(), Edit("render_scale", "49"), () => callbacks++), "out-of-range value rejected");
            Reject(() => Commit(Open(), Edit("render_scale", "85.0"), () => callbacks++), "renderer-incompatible integer decimal rejected");
            Reject(() => Commit(Open(), Edit("fog_strength", "0,5"), () => callbacks++), "renderer-incompatible decimal comma rejected");
            Check(snapshot.SequenceEqual(File.ReadAllBytes(ini)), "invalid values do not write");
            Commit(Open(), Edit("fog_strength", "1e0"), () => callbacks++);
            Check(snapshot.SequenceEqual(File.ReadAllBytes(ini)), "renderer-compatible scientific float is a byte-exact no-op");

            Write("[setup]\nfirst_run_done=true\n[graphics]\nvsync=true\nunknown=keep\n");
            var low = new Dictionary<string, string>();
            foreach (object option in listed)
            {
                decimal[] presets = (decimal[])OptionField(option, "Presets");
                if (presets != null) low[(string)OptionField(option, "Key")] = presets[0].ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            Commit(Open(), low, () => callbacks++);
            Check(Preset(Open()) == 0 && Raw(Open(), "vsync") == "true" && File.ReadAllText(ini).Contains("unknown=keep"),
                "Low preset matches renderer table and preserves personal and future keys");

            File.WriteAllBytes(dll, new byte[] { 0x22, 0x33 });
            using (var locked = new FileStream(dll, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Check(Available(Open()), "different and locked DLL content needs no hash or read");
            File.Delete(dll);
            File.WriteAllBytes(rddraw, new byte[] { 0x55 });
            using (var locked = new FileStream(rddraw, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Check(!Available(Open()), "an unrelated DLL alone cannot enable game.exe renderer settings");
            File.Delete(rddraw);
            Check(!Available(Open()), "missing game renderer DLL disables editor");
            File.WriteAllBytes(dll, new byte[] { 0x44 });

            Write("[setup]\nfirst_run_done=true\n[graphics]\nvsync=true\nunknown=keep\n");
            byte[] initialUi = File.ReadAllBytes(ini);
            using (var form = (Form)Activator.CreateInstance(formType, Instance, null, new object[] { registryRoot }, null))
            {
                Invoke(form, "BuildSettings");
                var controls = (IDictionary)Field(form, "ownedRendererControls");
                var preset = (ComboBox)Field(form, "ownedRendererPreset");
                Check(controls.Count == 50 && preset.Items.Count == 5 && preset.SelectedIndex == 2,
                    "actual settings form binds all options and four presets plus Custom");
                Check(Find(form, "ownedRendererF10Hint") is Label && Find(form, "ownedCategoryButton_8") is Button,
                    "F10 hint and all categories appear in the form");
                Check(assembly.GetType("EIStarter.RendererSettings") == null &&
                    assembly.GetType("EIStarter.Direct3DSettings") == null &&
                    Find(form, "choice_direct3DApi") == null,
                    "old renderer and dgVoodoo controls are absent");
                Click(Find(form, "choice_ownedRendererPreset").Controls[0]);
                Check(initialUi.SequenceEqual(File.ReadAllBytes(ini)), "choosing a preset is deferred until Save");
                Invoke(form, "CommitSettings", true, (Action)(() => callbacks++));
                Check(initialUi.SequenceEqual(File.ReadAllBytes(ini)), "Apply Paths ignores renderer edits");
                Invoke(form, "CommitSettings", false, (Action)(() => callbacks++));
                Check(Preset(Open()) == 0 && Raw(Open(), "vsync") == "true", "actual form writes preset and preserves VSync");
                var width = (TextBox)controls["window_width"];
                var height = (TextBox)controls["window_height"];
                width.Text = "1024";
                Reject(() => Invoke(form, "CollectOwnedEdits"), "one-sided window dimensions rejected");
                height.Text = "768";
                Invoke(form, "CommitSettings", false, (Action)(() => callbacks++));
                Check(Raw(Open(), "window_width") == "1024" && Raw(Open(), "window_height") == "768",
                    "window width and height are saved together");
                byte[] saved = File.ReadAllBytes(ini);
                preset.SelectedIndex = 3;
                Check(saved.SequenceEqual(File.ReadAllBytes(ini)), "unsaved edits remain in memory");
            }
            Check(Preset(Open()) == 0, "closing settings cancels unsaved preset change");
            Write("[setup]\nfirst_run_done=true\n[graphics]\nshadows=future-mode\nfuture=keep\n");
            byte[] unknownUi = File.ReadAllBytes(ini);
            using (var form = (Form)Activator.CreateInstance(formType, Instance, null, new object[] { registryRoot }, null))
            {
                Invoke(form, "BuildSettings");
                var choices = Find(form, "choice_owned_shadows");
                Check(choices is FlowLayoutPanel && choices.Controls.Count == 3 &&
                    choices.Controls[2].Tag as string == "selected", "unknown enum remains a selected inline keep-value choice");
                Invoke(form, "CommitSettings", false, (Action)(() => callbacks++));
                Check(unknownUi.SequenceEqual(File.ReadAllBytes(ini)), "inline unknown-value no-op preserves exact INI bytes");
                Click(choices.Controls[1]);
                Check(unknownUi.SequenceEqual(File.ReadAllBytes(ini)), "explicit replacement of unknown enum remains deferred");
                Invoke(form, "CommitSettings", false, (Action)(() => callbacks++));
                Check(Raw(Open(), "shadows") == "true" && File.ReadAllText(ini).Contains("future=keep"),
                    "inline choice saves known replacement without losing future keys");
            }
            Console.WriteLine("PASS total=" + passed);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally
        {
            if (registryRoot != null) Registry.CurrentUser.DeleteSubKeyTree(registryRoot, false);
            if (engine != null && Directory.Exists(engine))
            {
                foreach (string path in Directory.GetFiles(engine))
                { File.SetAttributes(path, FileAttributes.Normal); File.Delete(path); }
                Directory.Delete(engine);
            }
        }
    }
}
