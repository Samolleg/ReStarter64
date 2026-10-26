using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace EIStarter
{
    // rddraw.ini belongs to the new renderer. Keep the complete original byte
    // stream and edit only values the player selected in this dialog.
    internal sealed class OwnedRendererSettings
    {
        private static readonly Encoding ByteEncoding = Encoding.GetEncoding(28591);
        private readonly string path;
        private readonly string rendererPath;
        private readonly Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private byte[] snapshot;
        private string text;
        private string newline;
        internal bool Available { get; private set; }
        internal string Error { get; private set; }
        internal bool FirstRunDone { get; private set; }
        internal bool ConfirmFirstRun { get; set; }

#if RESTARTER64
        private readonly bool interfaceOnly64;
        private OwnedRendererSettings interfaceProfile64;
        private string interfaceName64 = "default";
        internal string InterfaceName64 => interfaceName64;
        internal OwnedRendererSettings(string engineDirectory, string interfacePath = null)
#else
        internal OwnedRendererSettings(string engineDirectory)
#endif
        {
#if RESTARTER64
            interfaceOnly64 = interfacePath != null;
            path = interfacePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config64", "rddraw.ini");
#else
            path = Path.Combine(engineDirectory, "rddraw.ini");
#endif
            rendererPath = Path.Combine(engineDirectory, LauncherProfile.EngineName);
            try
            {
                if (!File.Exists(rendererPath)) { Error = "EIRebuild executable is missing."; return; }
#if RESTARTER64
                Directory.CreateDirectory(Path.GetDirectoryName(path));
#endif
                snapshot = File.Exists(path) ? File.ReadAllBytes(path) : null;
                if (snapshot != null && (snapshot.Length > 1024 * 1024 || snapshot.Contains((byte)0)))
                { Error = "Renderer configuration has an unsupported encoding or size."; return; }
                text = snapshot == null ? "" : ByteEncoding.GetString(snapshot);
                newline = text.Contains("\r\n") ? "\r\n" : text.Contains("\n") ? "\n" : text.Contains("\r") ? "\r" : "\r\n";
                if (!Parse()) { Error = "Renderer configuration has duplicate sections or keys."; return; }
#if RESTARTER64
                if (!interfaceOnly64)
                {
                    // F10 applies this file over the main INI at startup.
                    string name = interfaceName64;
                    if (Encoding.UTF8.GetByteCount(name) == 0 || Encoding.UTF8.GetByteCount(name) > 48 ||
                        name[0] == '.' || name[0] == ' ' || name[name.Length - 1] == ' ' ||
                        name.Any(c => c < 32 || "\\/:*?\"<>|".IndexOf(c) >= 0)) name = "default";
                    interfaceName64 = name;
                    interfaceProfile64 = new OwnedRendererSettings(engineDirectory,
                        Path.Combine(Path.GetDirectoryName(path), "rddraw_profiles", name + ".ini"));
                    if (!interfaceProfile64.Available) { Error = interfaceProfile64.Error; return; }
                    foreach (var pair in interfaceProfile64.values)
                        if (IsInterfaceKey64(pair.Key)) values[pair.Key] = pair.Value;
                }
#endif
                Available = true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException)
            { Error = ex.Message; }
        }

        private struct Line
        {
            internal readonly int Start, End, Next;
            internal readonly string Content;
            internal Line(int start, int end, int next, string content)
            { Start = start; End = end; Next = next; Content = content; }
        }

        private static IEnumerable<Line> Lines(string source)
        {
            for (int start = 0; start < source.Length;)
            {
                int end = start;
                while (end < source.Length && source[end] != '\r' && source[end] != '\n') end++;
                int next = end;
                if (next < source.Length && source[next] == '\r') next++;
                if (next < source.Length && source[next] == '\n') next++;
                yield return new Line(start, end, next, source.Substring(start, end - start));
                start = next;
            }
        }

        private static string Header(string content)
        {
            string trimmed = content.Trim(' ', '\t', '\r');
            return trimmed.Length >= 2 && trimmed[0] == '[' && trimmed[trimmed.Length - 1] == ']'
                ? trimmed.Substring(1, trimmed.Length - 2).Trim(' ', '\t') : null;
        }

        private static bool Entry(string content, out string key, out string value, out int start, out int length)
        {
            key = value = null; start = length = 0;
            string trimmed = content.TrimStart(' ', '\t');
            if (trimmed.StartsWith(";", StringComparison.Ordinal) || trimmed.StartsWith("#", StringComparison.Ordinal)) return false;
            int equal = content.IndexOf('=');
            if (equal < 0) return false;
            key = content.Substring(0, equal).Trim(' ', '\t');
            int begin = equal + 1;
            while (begin < content.Length && (content[begin] == ' ' || content[begin] == '\t')) begin++;
            int end = content.Length;
            while (end > begin && (content[end - 1] == ' ' || content[end - 1] == '\t')) end--;
            start = begin;
            length = end - begin;
            value = content.Substring(begin, length);
            return key.Length != 0;
        }

        private bool Parse()
        {
            int graphicsSections = 0, setupSections = 0, firstRunKeys = 0;
            string section = "";
#if RESTARTER64
            int legacyUpscaler = 0;
#endif
            foreach (Line line in Lines(text))
            {
                string header = Header(line.Content);
                if (header != null)
                {
                    section = header;
                    if (section.Equals("graphics", StringComparison.OrdinalIgnoreCase)) graphicsSections++;
                    if (section.Equals("setup", StringComparison.OrdinalIgnoreCase)) setupSections++;
                    continue;
                }
                if (!Entry(line.Content, out string key, out string value, out _, out _)) continue;
                if (
#if RESTARTER64
                    (interfaceOnly64 ? IsInterfaceKey64(key) :
#endif
                    section.Equals("graphics", StringComparison.OrdinalIgnoreCase)
#if RESTARTER64
                    )
#endif
                    && OwnedRendererSchema.ByKey.ContainsKey(key))
                {
                    if (values.ContainsKey(key)) return false;
                    values[key] = value;
                }
#if RESTARTER64
                else if (!interfaceOnly64 && section.Equals("graphics", StringComparison.OrdinalIgnoreCase) &&
                         key.Equals("interface_profile", StringComparison.OrdinalIgnoreCase))
                    interfaceName64 = Encoding.UTF8.GetString(ByteEncoding.GetBytes(value));
                else if (!interfaceOnly64 && section.Equals("graphics", StringComparison.OrdinalIgnoreCase) &&
                         key.Equals("dlss_mode", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out legacyUpscaler);
#endif
                else if (section.Equals("setup", StringComparison.OrdinalIgnoreCase) &&
                         key.Equals("first_run_done", StringComparison.OrdinalIgnoreCase))
                {
                    firstRunKeys++;
                    FirstRunDone = value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1" ||
                        value.Equals("on", StringComparison.OrdinalIgnoreCase);
                }
            }
#if RESTARTER64
            // Match the engine's v3 migration when opening a pre-v3 profile.
            if (legacyUpscaler >= 1 && legacyUpscaler <= 10)
            {
                if (!values.ContainsKey("upscaler")) values["upscaler"] = legacyUpscaler <= 5 ? "1" : "2";
                if (!values.ContainsKey("upscaler_quality")) values["upscaler_quality"] =
                    ((legacyUpscaler - 1) % 5).ToString(CultureInfo.InvariantCulture);
            }
#endif
            return graphicsSections <= 1 && setupSections <= 1 && firstRunKeys <= 1;
        }

        internal string GetRaw(OwnedRendererOption option)
        {
            if (values.TryGetValue(option.Key, out string raw)) return raw;
#if RESTARTER64
            if (option.Type == OwnedValueType.Text) return option.TextDefault ?? "";
#endif
            return Format(option, option.Default);
        }

        internal static string Format(OwnedRendererOption option, decimal value)
        {
            if (option.Type == OwnedValueType.Boolean) return value == 0 ? "false" : "true";
            return value.ToString(CultureInfo.InvariantCulture);
        }

        internal static bool TryParse(OwnedRendererOption option, string raw, out decimal value)
        {
            value = 0;
            if (raw == null) return false;
#if RESTARTER64
            if (option.Type == OwnedValueType.Text)
                return raw.Length <= 1023 && !raw.Any(c => c == '\r' || c == '\n' || c == '\0' || c > 255);
#endif
            raw = raw.Trim();
            if (option.Type == OwnedValueType.Boolean)
            {
                if (raw == "1" || raw.Equals("true", StringComparison.OrdinalIgnoreCase) || raw.Equals("on", StringComparison.OrdinalIgnoreCase)) value = 1;
                else if (raw == "0" || raw.Equals("false", StringComparison.OrdinalIgnoreCase) || raw.Equals("off", StringComparison.OrdinalIgnoreCase)) value = 0;
                else return false;
            }
            else
            {
                NumberStyles style = option.Type == OwnedValueType.Integer
                    ? NumberStyles.AllowLeadingSign
                    : NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;
                if (!decimal.TryParse(raw, style, CultureInfo.InvariantCulture, out value)) return false;
            }
            return value >= option.Minimum && value <= option.Maximum;
        }

        internal int DetectPreset(IDictionary<string, string> selected = null)
        {
            for (int preset = 0; preset < 4; preset++)
            {
                bool matches = true;
                foreach (OwnedRendererOption option in OwnedRendererSchema.Options.Where(item => item.InPreset))
                {
                    string raw = selected != null && selected.TryGetValue(option.Key, out string candidate) ? candidate : GetRaw(option);
                    if (!TryParse(option, raw, out decimal value) || Math.Abs(value - option.Presets[preset]) >= .0001m)
                    { matches = false; break; }
                }
                if (matches) return preset;
            }
            return 4; // Custom
        }

        private static bool SectionBounds(string source, string name, out int begin, out int end)
        {
            begin = end = source.Length;
            bool inside = false;
            foreach (Line line in Lines(source))
            {
                string header = Header(line.Content);
                if (header == null) continue;
                if (inside) { end = line.Start; return true; }
                if (header.Equals(name, StringComparison.OrdinalIgnoreCase))
                { begin = line.Next; inside = true; }
            }
            return inside;
        }

        private static string Change(string source, string section, string key, string value, string newline)
        {
            if (!SectionBounds(source, section, out int begin, out int end))
            {
                string separator = source.Length == 0 || source.EndsWith("\n") || source.EndsWith("\r") ? "" : newline;
                return source + separator + "[" + section + "]" + newline + key + "=" + value + newline;
            }
            foreach (Line line in Lines(source))
            {
                if (line.Start < begin || line.Start >= end) continue;
                if (!Entry(line.Content, out string found, out _, out int valueStart, out int length) ||
                    !found.Equals(key, StringComparison.OrdinalIgnoreCase)) continue;
                int absolute = line.Start + valueStart;
                return source.Remove(absolute, length).Insert(absolute, value);
            }
            string prefix = end == 0 || source[end - 1] == '\r' || source[end - 1] == '\n' ? "" : newline;
            return source.Insert(end, prefix + key + "=" + value + newline);
        }

        internal void Commit(IDictionary<string, string> selected, Action saveOtherSettings)
        {
#if RESTARTER64
            if (interfaceProfile64 != null)
            {
                var interfaceEdits = selected.Where(pair => IsInterfaceKey64(pair.Key))
                    .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
                // Existing byte guards and rollback cover both INIs and the XML save.
                interfaceProfile64.Commit(interfaceEdits, () => CommitLocal64(selected, saveOtherSettings));
                return;
            }
            CommitLocal64(selected, saveOtherSettings);
        }

        private static bool IsInterfaceKey64(string key)
        {
            return OwnedRendererSchema.ByKey.TryGetValue(key, out OwnedRendererOption option) &&
                (option.Category == 11 || (option.Category == 8 &&
                    key != "confine_cursor" && !key.StartsWith("highlight_", StringComparison.Ordinal)));
        }

        private string ChangeInterface64(string source, string key, string value)
        {
            foreach (Line line in Lines(source))
                if (Entry(line.Content, out string found, out _, out int start, out int length) &&
                    found.Equals(key, StringComparison.OrdinalIgnoreCase))
                    return source.Remove(line.Start + start, length).Insert(line.Start + start, value);
            return Change(source, "interface", key, value, newline);
        }

        private void CommitLocal64(IDictionary<string, string> selected, Action saveOtherSettings)
        {
#endif
            if (selected == null) throw new ArgumentNullException(nameof(selected));
            if (saveOtherSettings == null) throw new ArgumentNullException(nameof(saveOtherSettings));
            var edits = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in selected)
            {
                if (!OwnedRendererSchema.ByKey.TryGetValue(pair.Key, out OwnedRendererOption option) ||
                    !TryParse(option, pair.Value, out decimal chosen))
                    throw new InvalidOperationException(UiText.Get("OwnedRendererInvalid") + ": " + pair.Key);
                string before = GetRaw(option);
#if RESTARTER64
                if (option.Type == OwnedValueType.Text)
                {
                    if (!string.Equals(before, pair.Value, StringComparison.Ordinal)) edits[pair.Key] = pair.Value;
                    continue;
                }
#endif
                if (TryParse(option, before, out decimal previous) && Math.Abs(previous - chosen) < .0001m) continue;
                edits[pair.Key] = Format(option, chosen);
            }
            bool finishFirstRun = !FirstRunDone && (ConfirmFirstRun ||
                edits.Keys.Any(key => OwnedRendererSchema.ByKey[key].InPreset));
            if (edits.Count == 0 && (!ConfirmFirstRun || FirstRunDone)) { saveOtherSettings(); return; }
            if (!Available || !File.Exists(rendererPath)) throw new InvalidOperationException(UiText.Get("OwnedRendererUnavailable"));
            string nextText = text;
            foreach (var pair in edits) nextText =
#if RESTARTER64
                interfaceOnly64 ? ChangeInterface64(nextText, pair.Key, pair.Value) :
#endif
                Change(nextText, "graphics", pair.Key, pair.Value, newline);
            if (finishFirstRun) nextText = Change(nextText, "setup", "first_run_done", "true", newline);
            byte[] next = ByteEncoding.GetBytes(nextText);

            if (snapshot == null)
            {
                using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    try
                    {
                        file.Write(next, 0, next.Length); file.Flush(true);
                        saveOtherSettings();
                    }
                    catch
                    {
                        file.Dispose();
                        try { File.Delete(path); }
                        catch (Exception rollback) when (rollback is IOException || rollback is UnauthorizedAccessException)
                        { throw new IOException(UiText.Get("OwnedRendererRollbackFailed"), rollback); }
                        throw;
                    }
                }
            }
            else
            {
                using (var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    byte[] current = new byte[file.Length];
                    int total = 0;
                    while (total < current.Length)
                    {
                        int read = file.Read(current, total, current.Length - total);
                        if (read == 0) throw new IOException(UiText.Get("SettingsChanged"));
                        total += read;
                    }
                    if (!current.SequenceEqual(snapshot)) throw new InvalidOperationException(UiText.Get("SettingsChanged"));
                    try
                    {
                        file.Position = 0; file.Write(next, 0, next.Length); file.SetLength(next.Length); file.Flush(true);
                        saveOtherSettings();
                    }
                    catch (Exception original)
                    {
                        try
                        {
                            file.Position = 0; file.Write(snapshot, 0, snapshot.Length);
                            file.SetLength(snapshot.Length); file.Flush(true);
                        }
                        catch (Exception rollback) when (rollback is IOException || rollback is UnauthorizedAccessException)
                        { throw new IOException(UiText.Get("OwnedRendererRollbackFailed"), new AggregateException(original, rollback)); }
                        throw;
                    }
                }
            }
            snapshot = next;
            text = nextText;
            FirstRunDone = FirstRunDone || finishFirstRun;
            ConfirmFirstRun = false;
            foreach (var pair in edits) values[pair.Key] = pair.Value;
        }
    }
}
