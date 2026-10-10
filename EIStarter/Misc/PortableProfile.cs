using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Win32;

namespace EIStarter
{
    // The legacy engine reads HKCU. Lease its settings only while this launcher
    // owns the existing process mutex; keep the user's durable profile beside EXE.
    internal sealed class PortableProfile : IDisposable
    {
        private readonly string root, profileFile, journal;
        private readonly List<SettingsProfile.Value> before;
        private bool active;
        internal static string RecoveryFile => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EIStarter", "portable-recovery.xml");

        private static bool InScope(string suffix) => suffix.Equals("EvilIslands", StringComparison.OrdinalIgnoreCase) ||
            suffix.StartsWith("EvilIslands\\", StringComparison.OrdinalIgnoreCase) ||
            suffix.Equals("Settings", StringComparison.OrdinalIgnoreCase) || suffix.StartsWith("Settings\\", StringComparison.OrdinalIgnoreCase);
        private static bool Preference(SettingsProfile.Value v)
        {
            string suffix = v.Path.Substring(SettingsProfile.Root.Length).TrimStart('\\');
            return InScope(suffix) && !suffix.Equals(@"EvilIslands\Path Settings", StringComparison.OrdinalIgnoreCase) &&
                !(suffix.Equals("Settings", StringComparison.OrdinalIgnoreCase) && v.Name.Equals("AddonPath", StringComparison.OrdinalIgnoreCase));
        }
        private static List<SettingsProfile.Value> Snapshot(string root)
        {
            var result = new List<SettingsProfile.Value>();
            void Read(string suffix)
            {
                using (var k = Registry.CurrentUser.OpenSubKey(root + "\\" + suffix))
                {
                    if (k == null) return;
                    foreach (var name in k.GetValueNames()) result.Add(new SettingsProfile.Value {
                        Path = SettingsProfile.Root + "\\" + suffix, Name = name, Kind = k.GetValueKind(name),
                        Data = k.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) });
                    foreach (var sub in k.GetSubKeyNames()) Read(suffix + "\\" + sub);
                }
            }
            Read("EvilIslands"); Read("Settings");
            using (var k = Registry.CurrentUser.OpenSubKey(root))
                if (k != null) foreach (string name in new[] { "Path", "AddonPath" })
                {
                    object data = k.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                    if (data != null) result.Add(new SettingsProfile.Value { Path = SettingsProfile.Root,
                        Name = name, Kind = k.GetValueKind(name), Data = data });
                }
            return result;
        }
        private static string Encode(SettingsProfile.Value v)
        {
            if (v.Data is byte[] b) return Convert.ToBase64String(b);
            if (v.Data is string[] a) return Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(string.Join("\0", a)));
            return Convert.ToString(v.Data, System.Globalization.CultureInfo.InvariantCulture);
        }
        private static List<SettingsProfile.Value> Load(string file)
        {
            var document = XDocument.Load(file);
            if (document.Root.Name != "profile" || (string)document.Root.Attribute("version") != "1")
                throw new InvalidDataException("Неверный формат профиля: " + file);
            var result = new List<SettingsProfile.Value>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in document.Root.Elements("value"))
            {
                string suffix = (string)e.Attribute("section"), name = (string)e.Attribute("name");
                if (suffix == null || name == null || suffix.Contains("/") || suffix.Split('\\').Any(p => p == "..") ||
                    !(InScope(suffix) || (suffix == "" && (name == "Path" || name == "AddonPath"))) ||
                    !ids.Add(suffix + "\n" + name)) throw new InvalidDataException("Недопустимое значение профиля: " + file);
                var kind = (RegistryValueKind)int.Parse((string)e.Attribute("kind"));
                object data;
                switch (kind)
                {
                    case RegistryValueKind.DWord: data = int.Parse(e.Value, System.Globalization.CultureInfo.InvariantCulture); break;
                    case RegistryValueKind.QWord: data = long.Parse(e.Value, System.Globalization.CultureInfo.InvariantCulture); break;
                    case RegistryValueKind.Binary: case RegistryValueKind.None: data = Convert.FromBase64String(e.Value); break;
                    case RegistryValueKind.MultiString:
                        string s = System.Text.Encoding.Unicode.GetString(Convert.FromBase64String(e.Value));
                        data = s.Length == 0 ? new string[0] : s.Split('\0'); break;
                    case RegistryValueKind.String: case RegistryValueKind.ExpandString: data = e.Value; break;
                    default: throw new InvalidDataException("Неподдерживаемый тип профиля: " + file);
                }
                result.Add(new SettingsProfile.Value { Path = SettingsProfile.Root + (suffix.Length == 0 ? "" : "\\" + suffix), Name = name, Kind = kind, Data = data });
            }
            return result;
        }
        private static void Save(string file, IEnumerable<SettingsProfile.Value> values)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            var xml = new XDocument(new XElement("profile", new XAttribute("version", "1"), values.Select(v =>
                new XElement("value", new XAttribute("section", v.Path.Substring(SettingsProfile.Root.Length).TrimStart('\\')),
                    new XAttribute("name", v.Name), new XAttribute("kind", (int)v.Kind), Encode(v)))));
            string temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { xml.Save(stream); stream.Flush(true); }
                if (File.Exists(file)) File.Replace(temporary, file, null); else File.Move(temporary, file);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        private static void Replace(string root, List<SettingsProfile.Value> desired)
        {
            var profile = new SettingsProfile(root);
            foreach (var v in Snapshot(root)) profile.WriteExact(v.Path, v.Name, null, RegistryValueKind.None);
            foreach (var v in desired) profile.WriteExact(v.Path, v.Name, v.Data, v.Kind);
            profile.Commit(profile.Plan.Values.ToList());
        }
        internal static void Recover(string root, string journal)
        {
            if (!File.Exists(journal)) return;
            var original = Load(journal); // Invalid journal fails closed, never guesses prior values.
            Replace(root, original);
            File.Delete(journal);
        }
        internal PortableProfile(string packageRoot, string root, string journal)
        {
            this.root = root; this.journal = journal;
            if (File.Exists(journal)) throw new InvalidOperationException("Сначала нужно восстановить предыдущий профиль.");
            profileFile = Path.Combine(packageRoot, "portable-settings.xml");
            string defaults = Path.Combine(packageRoot, "portable-defaults.xml");
            var values = Load(defaults);
            if (File.Exists(profileFile))
                foreach (var saved in Load(profileFile))
                {
                    values.RemoveAll(v => v.Path.Equals(saved.Path, StringComparison.OrdinalIgnoreCase) && v.Name.Equals(saved.Name, StringComparison.OrdinalIgnoreCase));
                    values.Add(saved);
                }
            if (values.Any(v => !Preference(v))) throw new InvalidDataException("Профиль содержит машинные пути.");
            // Check durable writable storage before touching HKCU, also materialize first-run defaults.
            Save(profileFile, values);
            before = Snapshot(root);
            Save(journal, before);
            try { Replace(root, values); active = true; }
            catch { Recover(root, journal); throw; }
        }
        public void Dispose()
        {
            if (!active) return;
            // Even a disk-full failure while saving preferences must restore the host profile.
            try { Save(profileFile, Snapshot(root).Where(Preference)); }
            finally
            {
                Replace(root, before);
                File.Delete(journal);
                active = false;
            }
        }
    }
}
