using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;
using Microsoft.Win32;

class ProfileCheck
{
    static Assembly assembly;
    static BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    static Type T(string name) { return assembly.GetType("EIStarter." + name, true); }
    static object Call(object instance, string name, params object[] args)
    {
        Type type = instance as Type ?? instance.GetType();
        try { return type.GetMethods(flags).Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(instance is Type ? null : instance, args); }
        catch (TargetInvocationException ex) { throw ex.InnerException; }
    }
    static object Field(object value, string name) { return value.GetType().GetField(name, flags).GetValue(value); }
    static object Profile(string file) { return Activator.CreateInstance(T("SettingsProfile"), flags, null, new object[] { file }, null); }
    static IList Plan(object profile) { return (IList)Call(profile, "Changes", Activator.CreateInstance(Field(profile, "Plan").GetType())); }
    static void Commit(object profile, Action<int> fail) { Call(profile, "Commit", Plan(profile), fail); }
    static void Assert(bool yes, string reason) { if (!yes) throw new Exception(reason); }
    static bool Equal(object a, object b)
    {
        if (a is byte[] && b is byte[]) return ((byte[])a).SequenceEqual((byte[])b);
        if (a is string[] && b is string[]) return ((string[])a).SequenceEqual((string[])b);
        return object.Equals(a, b);
    }
    static object Data(XElement e)
    {
        switch (int.Parse((string)e.Attribute("kind")))
        {
            case 4: return int.Parse(e.Value);
            case 11: return long.Parse(e.Value);
            case -1: case 3: return Convert.FromBase64String(e.Value);
            case 7:
                string text = System.Text.Encoding.Unicode.GetString(Convert.FromBase64String(e.Value));
                return text.Length == 0 ? new string[0] : text.Split('\0');
            default: return e.Value;
        }
    }
    static XDocument DeniedRegistry() { throw new Exception("Unexpected legacy registry access"); }
    static void Ensure(string root) { Call(T("FirstRunImport"), "Ensure", root, new Func<XDocument>(DeniedRegistry)); }
    static byte[] Bytes(string file) { return File.ReadAllBytes(file); }
    [STAThread]
    static int Main(string[] args)
    {
        try { Run(args); Console.WriteLine("ALL PASS"); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    static void Run(string[] args)
    {
        string root = Path.GetFullPath(args[1]);
        Directory.SetCurrentDirectory(root);
        assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
        string file = Path.Combine(root, "restarter-settings.xml");
        string defaults = Path.Combine(root, "restarter-defaults.xml");
        string portable = Path.Combine(root, "portable-settings.xml");
        string oldIni = Path.Combine(root, "starter.config");
        string oldRenderer = Path.Combine(root, "Engine", "eid3d11.ini");
        byte[] oldXml = Bytes(portable), oldConfig = Bytes(oldIni), oldGraphics = Bytes(oldRenderer);
        Ensure(root);
        Assert(XNode.DeepEquals(XDocument.Load(portable), XDocument.Load(file)), "import values/types");
        Assert(Bytes(Path.Combine(root, "Engine", "rddraw.ini")).SequenceEqual(oldGraphics), "renderer import");
        string configText = File.ReadAllText(Path.Combine(root, "restarter.config"));
        Assert(configText.IndexOf("ExeName", StringComparison.OrdinalIgnoreCase) < 0, "ExeName leaked");
        var config = Activator.CreateInstance(T("IniFile"), flags, null, new object[] { Path.Combine(root, "restarter.config") }, null);
        var legacyConfig = Activator.CreateInstance(T("IniFile"), flags, null, new object[] { oldIni }, null);
        foreach (string key in new[] { "ModSel", "language", "UiTheme" })
            Assert(object.Equals(Call(config, "Read", key, "Settings", ""), Call(legacyConfig, "Read", key, "Settings", "")), "config import " + key);
        Assert(Bytes(portable).SequenceEqual(oldXml) && Bytes(oldIni).SequenceEqual(oldConfig) && Bytes(oldRenderer).SequenceEqual(oldGraphics), "legacy modified");
        File.WriteAllText(oldIni, "[Settings]\r\nlanguage=xx\r\n");
        File.WriteAllText(oldRenderer, "changed old renderer");
        Ensure(root);
        Assert(File.ReadAllText(Path.Combine(root, "restarter.config")) == configText, "reimport config");
        Assert(Bytes(Path.Combine(root, "Engine", "rddraw.ini")).SequenceEqual(oldGraphics), "reimport renderer");
        Console.WriteLine("PASS import-once, all imported values/types, config, renderer, legacy preservation, denying registry callback");

        var xml = XDocument.Load(file);
        xml.Root.SetAttributeValue("foreign", "keep");
        xml.Root.Add(new XComment("keep comment"), new XElement("extension", new XAttribute("owner", "other"), "keep extension"));
        xml.Root.Add(new XElement("value", new XAttribute("section", "Settings\\Other"), new XAttribute("name", "QWord"), new XAttribute("kind", 11), "9223372036854775807"));
        xml.Root.Add(new XElement("value", new XAttribute("section", "Settings\\Other"), new XAttribute("name", "Expand"), new XAttribute("kind", 2), "%TEMP%"));
        xml.Root.Add(new XElement("value", new XAttribute("section", "Settings\\Other"), new XAttribute("name", "None"), new XAttribute("kind", -1), "AAEC"));
        xml.Save(file);
        var values = xml.Root.Elements("value").ToArray();
        object profile = Profile(file);
        foreach (var e in values)
        {
            string section = (string)e.Attribute("section"), name = (string)e.Attribute("name");
            Assert(Equal(Call(profile, "Read", section, name), Data(e)), "decode " + name);
            Call(profile, "WriteExact", section, name, Data(e), (RegistryValueKind)int.Parse((string)e.Attribute("kind")));
        }
        Commit(profile, null);
        Assert(XNode.DeepEquals(xml, XDocument.Load(file)), "typed round trip");
        Console.WriteLine("PASS read/write typed round trip for " + values.Length + " fields (all option fields and Binary/DWord/String/MultiString/QWord/ExpandString/None)");

        profile = Profile(file);
        Call(profile, "WriteExact", "Settings", "MP console", 1, RegistryValueKind.DWord);
        byte[] before = Bytes(file);
        bool rejected = false;
        try { Commit(profile, delegate(int count) { throw new IOException("injected prepublication failure"); }); } catch (IOException) { rejected = true; }
        Assert(rejected && Bytes(file).SequenceEqual(before), "failed transaction changed file");
        // Independently change an unread value. Commit must merge this latest document.
        var concurrent = XDocument.Load(file);
        concurrent.Root.Add(new XElement("value", new XAttribute("section", "Settings"), new XAttribute("name", "External"), new XAttribute("kind", 1), "new"));
        concurrent.Save(file);
        Commit(profile, null);
        var saved = XDocument.Load(file);
        Assert((string)saved.Root.Attribute("foreign") == "keep" && saved.Root.Element("extension").Value == "keep extension" && saved.Root.Nodes().OfType<XComment>().Any(), "unknown metadata lost");
        Assert(saved.Root.Elements("value").Any(e => (string)e.Attribute("name") == "External" && e.Value == "new"), "concurrent unknown value lost");
        Assert(Directory.GetFiles(root, "*.tmp*").Length == 0, "temporary files leaked");
        profile = Profile(file);
        Call(profile, "WriteExact", "Settings", "MP console", 0, RegistryValueKind.DWord);
        concurrent = XDocument.Load(file);
        concurrent.Root.Elements("value").First(e => (string)e.Attribute("name") == "MP console").Value = "2";
        concurrent.Save(file); before = Bytes(file); rejected = false;
        try { Commit(profile, null); } catch (InvalidOperationException) { rejected = true; }
        Assert(rejected && Bytes(file).SequenceEqual(before), "conflict overwrote external edit");
        Console.WriteLine("PASS failed write atomicity, foreign metadata and concurrent foreign value preservation, edited-value conflict rejection");

        // Observe actual replacement under a concurrent XML reader.
        int reads = 0; Exception readerFailure = null; bool stop = false;
        var reader = new Thread(delegate() {
            while (!Volatile.Read(ref stop))
                try { using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) XDocument.Load(stream); Interlocked.Increment(ref reads); }
                catch (IOException) { } // sharing conflicts are permitted; partial XML is not
                catch (Exception ex) { readerFailure = ex; break; }
        });
        reader.Start();
        for (int i = 0; i < 24; i++) { profile = Profile(file); Call(profile, "WriteExact", "Settings", "MP console", i % 2, RegistryValueKind.DWord); Commit(profile, null); }
        Volatile.Write(ref stop, true); reader.Join();
        Assert(readerFailure == null && reads > 0, "partial XML observed: " + readerFailure);
        Console.WriteLine("PASS atomic publication with concurrent XML reader: " + reads + " complete reads");

        // UI bindings are exercised headlessly on the exact Release assembly.
        File.Copy(defaults, file, true);
        profile = Profile(file);
        Call(profile, "WriteExact", "EvilIslands\\Camera Settings", "FarClipDistance", BitConverter.GetBytes(123456f), RegistryValueKind.Binary);
        Commit(profile, null);
        Application.EnableVisualStyles();
        using (var form = (Form)Activator.CreateInstance(T("Options"), flags, null, new object[] { file }, null))
        {
            Call(form, "CollectSettings");
            var bindings = (IDictionary)Field(Field(form, "profile"), "Plan");
            Assert(bindings.Count > 60, "option bindings not exercised");
            byte[] initial = Bytes(file);
            Call(form, "ResetDefaults");
            Assert(Bytes(file).SequenceEqual(initial), "reset wrote before Apply");
            Call(form, "CollectSettings");
            var active = Field(form, "profile");
            var defaultsProfile = Profile(defaults);
            foreach (DictionaryEntry pair in (IDictionary)Field(active, "Plan"))
            {
                object v = pair.Value;
                string section = (string)Field(v, "Path"), name = (string)Field(v, "Name");
                Assert(Equal(Field(v, "Data"), Call(defaultsProfile, "Read", section, name)), "reset option " + name);
            }
            Call(active, "Commit", Call(form, "PrepareSave", false), null);
            Assert(Equal(Call(Profile(file), "Read", "EvilIslands\\Camera Settings", "FarClipDistance"), Call(defaultsProfile, "Read", "EvilIslands\\Camera Settings", "FarClipDistance")), "reset save");
            Console.WriteLine("PASS " + bindings.Count + " option bindings, defaults staging/no writes before Apply, validated defaults commit");
        }

        // Launch contract validation never starts any process.
        bool missing = false;
        try { Call(T("RebuildLaunch"), "Prepare", root); } catch (FileNotFoundException) { missing = true; }
        Assert(missing, "missing EXE not rejected");
        File.WriteAllText(Path.Combine(root, "Engine", "EIRebuild.exe"), "headless placeholder, never executed");
        missing = false;
        string dll = Path.Combine(root, "Engine", "RDDraw.dll");
        if (File.Exists(dll)) File.Delete(dll);
        try { Call(T("RebuildLaunch"), "Prepare", root); } catch (FileNotFoundException) { missing = true; }
        Assert(missing, "missing RDDraw not rejected");
        File.WriteAllText(dll, "headless placeholder, never loaded");
        string rendererIni = Path.Combine(root, "Engine", "rddraw.ini");
        File.WriteAllText(rendererIni, "; keep comment\r\n[graphics]\r\nvsync = 0\r\nforeign_key = keep\r\n[other]\r\nowner=keep\r\n");
        object renderer = Activator.CreateInstance(T("OwnedRendererSettings"), flags, null, new object[] { Path.Combine(root, "Engine") }, null);
        Assert((bool)renderer.GetType().GetProperty("Available", flags).GetValue(renderer, null), "renderer settings unavailable");
        byte[] rendererBefore = Bytes(rendererIni);
        var edits = new Dictionary<string, string> { { "vsync", "1" } };
        rejected = false;
        try { Call(renderer, "Commit", edits, new Action(delegate { throw new IOException("profile failed"); })); }
        catch (IOException) { rejected = true; }
        Assert(rejected && Bytes(rendererIni).SequenceEqual(rendererBefore), "renderer rollback");
        Call(renderer, "Commit", edits, new Action(delegate { }));
        string rendererText = File.ReadAllText(rendererIni);
        Assert(rendererText.Contains("vsync = true") && rendererText.Contains("foreign_key = keep") && rendererText.Contains("; keep comment") && rendererText.Contains("owner=keep"), "renderer foreign data preservation");
        renderer = Activator.CreateInstance(T("OwnedRendererSettings"), flags, null, new object[] { Path.Combine(root, "Engine") }, null);
        File.AppendAllText(rendererIni, "; external edit\r\n"); rendererBefore = Bytes(rendererIni); rejected = false;
        try { Call(renderer, "Commit", new Dictionary<string, string> { { "vsync", "0" } }, new Action(delegate { })); }
        catch (InvalidOperationException) { rejected = true; }
        Assert(rejected && Bytes(rendererIni).SequenceEqual(rendererBefore), "renderer conflict");
        Console.WriteLine("PASS rddraw.ini option write, foreign bytes preservation, profile-failure rollback, external conflict");
        var start = (ProcessStartInfo)Call(T("RebuildLaunch"), "Prepare", root);
        Assert(start.FileName == Path.Combine(root, "Engine", "EIRebuild.exe") && start.WorkingDirectory == Path.Combine(root, "Engine") && start.Arguments == "-profile \"" + file + "\"" && !start.UseShellExecute, "launch contract");
        string mod = Path.Combine(root, "MODS", "Demo"); Directory.CreateDirectory(mod);
        Call(T("RebuildLaunch"), "SelectMod", root, mod);
        Assert((string)Call(Profile(file), "Read", "Settings", "AddonPath") == mod + Path.DirectorySeparatorChar, "mod XML binding");
        Assert((string)T("Program").GetField("UniqueMutexName", flags).GetRawConstantValue() == "ReStarterSharp", "mutex identity");
        Console.WriteLine("PASS EIRebuild/RDDraw presence, absolute -profile argument, Engine cwd, XML mod paths, separate mutex");

        string fallback = Path.Combine(root, "fallback"); Directory.CreateDirectory(fallback);
        int imports = 0;
        var legacy = XDocument.Load(portable);
        Call(T("FirstRunImport"), "Ensure", fallback, new Func<XDocument>(delegate { imports++; return new XDocument(legacy); }));
        Call(T("FirstRunImport"), "Ensure", fallback, new Func<XDocument>(DeniedRegistry));
        Assert(imports == 1 && XNode.DeepEquals(legacy, XDocument.Load(Path.Combine(fallback, "restarter-settings.xml"))), "registry fallback injection");
        File.WriteAllBytes(oldIni, oldConfig); File.WriteAllBytes(oldRenderer, oldGraphics);
        Assert(Bytes(portable).SequenceEqual(oldXml), "portable source changed");
        Console.WriteLine("PASS read-only legacy reader fallback once; no live registry invoked in any test");
    }
}
