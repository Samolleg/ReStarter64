using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace EIStarter
{
    internal static class UiText
    {
        private static readonly string[] Languages = { "en", "ru", "de", "fr", "pl", "ko", "zh" };
        private static readonly Dictionary<string, string[]> Entries = Load();
        public static string Language { get; set; } = "en";
        private static Dictionary<string, string[]> Load()
        {
            var entries = new Dictionary<string, string[]>(StringComparer.Ordinal);
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("EIStarter.UiText.tsv"))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var columns = line.Split('|');
                    if (columns.Length != 8) throw new InvalidDataException("Invalid translation: " + columns[0]);
                    var values = new string[7];
                    Array.Copy(columns, 1, values, 0, 7);
                    entries.Add(columns[0], values);
                }
            }
            return entries;
        }
        public static string Get(string key)
        {
            if (!Entries.TryGetValue(key, out var values)) return key;
            int index = Array.IndexOf(Languages, Language);
            return values[index < 0 ? 0 : index].Replace("<rn>", Environment.NewLine);
        }
        public static void Apply(Control control)
        {
            if (Entries.ContainsKey(control.Name)) control.Text = Get(control.Name);
            if (control is ComboBox combo)
                for (int i = 0; i < combo.Items.Count; i++)
                    if (Entries.ContainsKey(combo.Name + "__" + i)) combo.Items[i] = Get(combo.Name + "__" + i);
                    else if (combo.Items[i] is string text && Entries.ContainsKey(text)) combo.Items[i] = Get(text);
            foreach (Control child in control.Controls) Apply(child);
        }
    }
}
