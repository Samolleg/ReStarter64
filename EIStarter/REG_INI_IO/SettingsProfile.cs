using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Win32;

namespace EIStarter
{
    // Logical XML sections only; no registry operations.
    internal sealed class SettingsProfile
    {
        internal const string Root = "";
        internal static string FileName => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "restarter-settings.xml");
        internal sealed class Value
        {
            internal string Path, Name;
            internal object Data;
            internal RegistryValueKind Kind;
        }
        private readonly string file;
        private readonly Dictionary<string, Value> loaded;
        private readonly Dictionary<string, Value> snapshot = new Dictionary<string, Value>(StringComparer.OrdinalIgnoreCase);
        internal Dictionary<string, Value> Plan = new Dictionary<string, Value>(StringComparer.OrdinalIgnoreCase);
        internal SettingsProfile(string file = null)
        {
            this.file = Path.GetFullPath(string.IsNullOrEmpty(file) ? FileName : file);
            loaded = Values(Load(this.file));
        }
        private static string Section(string path) => path.TrimStart('\\');
        private static string Id(string path, string name) => Section(path) + "\n" + name;
        internal static XDocument Load(string file)
        {
            var xml = XDocument.Load(file, LoadOptions.PreserveWhitespace);
            if (xml.Root == null || xml.Root.Name != "profile" || (string)xml.Root.Attribute("version") != "1")
                throw new InvalidDataException("Неверный формат профиля: " + file);
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in xml.Root.Elements("value"))
            {
                string path = (string)e.Attribute("section"), name = (string)e.Attribute("name");
                if (path == null || name == null || path.Contains("/") || path.Split('\\').Any(p => p == "..") || !ids.Add(Id(path, name)))
                    throw new InvalidDataException("Недопустимое или повторное значение профиля: " + file);
                Decode(e);
            }
            return xml;
        }
        private static Value Decode(XElement e)
        {
            var kind = (RegistryValueKind)int.Parse((string)e.Attribute("kind"), CultureInfo.InvariantCulture);
            object data;
            switch (kind)
            {
                case RegistryValueKind.DWord: data = int.Parse(e.Value, CultureInfo.InvariantCulture); break;
                case RegistryValueKind.QWord: data = long.Parse(e.Value, CultureInfo.InvariantCulture); break;
                case RegistryValueKind.Binary: case RegistryValueKind.None: data = Convert.FromBase64String(e.Value); break;
                case RegistryValueKind.MultiString:
                    string text = System.Text.Encoding.Unicode.GetString(Convert.FromBase64String(e.Value));
                    data = text.Length == 0 ? new string[0] : text.Split('\0'); break;
                case RegistryValueKind.String: case RegistryValueKind.ExpandString: data = e.Value; break;
                default: throw new InvalidDataException("Неподдерживаемый тип профиля.");
            }
            return new Value { Path = Section((string)e.Attribute("section")), Name = (string)e.Attribute("name"), Kind = kind, Data = data };
        }
        internal static string Encode(Value v)
        {
            if (v.Data is byte[] b) return Convert.ToBase64String(b);
            if (v.Data is string[] a) return Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(string.Join("\0", a)));
            return Convert.ToString(v.Data, CultureInfo.InvariantCulture);
        }
        private static Dictionary<string, Value> Values(XDocument xml) => xml.Root.Elements("value").Select(Decode).ToDictionary(v => Id(v.Path, v.Name), StringComparer.OrdinalIgnoreCase);
        internal object Read(string path, string name)
        {
            string id = Id(path, name);
            if (!snapshot.TryGetValue(id, out var value))
            {
                if (!loaded.TryGetValue(id, out value)) value = new Value { Path = Section(path), Name = name, Kind = RegistryValueKind.None };
                snapshot.Add(id, value);
            }
            return value.Data;
        }
        internal void Write(string path, string name, object data, RegistryValueKind kind)
        {
            if (data is byte[] raw && raw.Length == 4 && float.IsNaN(BitConverter.ToSingle(raw, 0))) return;
            if (data is int number && number == int.MinValue) return;
            WriteExact(path, name, data, kind);
        }
        internal void WriteExact(string path, string name, object data, RegistryValueKind kind)
        {
            Read(path, name);
            Plan[Id(path, name)] = new Value { Path = Section(path), Name = name, Data = data, Kind = kind };
        }
        internal static bool Equal(Value a, Value b)
        {
            if (a == null || b == null) return a == b;
            if (a.Data == null || b.Data == null) return a.Data == b.Data;
            if (a.Kind != b.Kind) return false;
            if (a.Data is byte[] x && b.Data is byte[] y) return x.SequenceEqual(y);
            if (a.Data is string[] sx && b.Data is string[] sy) return sx.SequenceEqual(sy);
            return Equals(a.Data, b.Data);
        }
        internal List<Value> Changes(Dictionary<string, Value> baseline) => Plan.Where(p => !baseline.TryGetValue(p.Key, out var previous) || !Equal(previous, p.Value)).Select(p => p.Value).ToList();
        internal void Commit(List<Value> changes, Action<int> afterWrite = null)
        {
            if (changes.Count == 0) return;
            byte[] before = File.ReadAllBytes(file);
            var xml = Load(file);
            var current = Values(xml);
            foreach (var previous in snapshot)
            {
                current.TryGetValue(previous.Key, out var now);
                if (!Equal(previous.Value, now ?? new Value())) throw new InvalidOperationException(UiText.Get("SettingsChanged"));
            }
            int count = 0;
            foreach (var value in changes)
            {
                var e = xml.Root.Elements("value").FirstOrDefault(n => Id((string)n.Attribute("section"), (string)n.Attribute("name")).Equals(Id(value.Path, value.Name), StringComparison.OrdinalIgnoreCase));
                if (value.Data == null) e?.Remove();
                else
                {
                    if (e == null) { e = new XElement("value", new XAttribute("section", Section(value.Path)), new XAttribute("name", value.Name)); xml.Root.Add(e); }
                    e.SetAttributeValue("kind", (int)value.Kind); e.Value = Encode(value);
                }
                afterWrite?.Invoke(++count); // Failures before publication preserve the original.
            }
            SaveAtomic(file, xml, before);
            foreach (var value in changes) snapshot[Id(value.Path, value.Name)] = value;
        }
        internal static void SaveAtomic(string file, XDocument xml, byte[] expected = null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file)));
            string temp = file + "." + Guid.NewGuid().ToString("N") + ".tmp", backup = temp + ".bak";
            try
            {
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { xml.Save(stream); stream.Flush(true); }
                Load(temp);
                if (expected != null)
                {
                    if (!File.ReadAllBytes(file).SequenceEqual(expected)) throw new InvalidOperationException(UiText.Get("SettingsChanged"));
                    File.Replace(temp, file, backup);
                }
                else File.Move(temp, file);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); if (File.Exists(backup)) File.Delete(backup); }
        }
    }
}
