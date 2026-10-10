using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Win32;

namespace EIStarter
{
    internal static class FirstRunImport
    {
        // The sole legacy registry boundary. Always read-only, and invoked only
        // when the new durable profile is absent. Tests inject a denying reader.
        internal static XDocument ReadLegacyRegistry()
        {
            var xml = new XDocument(new XElement("profile", new XAttribute("version", "1")));
            using (var root = Registry.CurrentUser.OpenSubKey(@"Software\Gipat.ru\EI_Starter", false))
            {
                if (root == null) return xml;
                void Add(RegistryKey key, string section)
                {
                    foreach (string name in key.GetValueNames())
                    {
                        if (section == "" && name != "Path" && name != "AddonPath") continue;
                        var value = new SettingsProfile.Value { Path = section, Name = name,
                            Kind = key.GetValueKind(name), Data = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) };
                        if (value.Data != null) xml.Root.Add(new XElement("value", new XAttribute("section", section),
                            new XAttribute("name", name), new XAttribute("kind", (int)value.Kind), SettingsProfile.Encode(value)));
                    }
                    foreach (string sub in key.GetSubKeyNames())
                    {
                        if (section == "" && !sub.Equals("EvilIslands", StringComparison.OrdinalIgnoreCase) && !sub.Equals("Settings", StringComparison.OrdinalIgnoreCase)) continue;
                        using (var child = key.OpenSubKey(sub, false)) if (child != null) Add(child, section == "" ? sub : section + "\\" + sub);
                    }
                }
                Add(root, "");
            }
            return xml;
        }

        internal static void Ensure(string root, Func<XDocument> registryReader = null)
        {
            string settings = Path.Combine(root, "restarter-settings.xml");
            if (File.Exists(settings)) { SettingsProfile.Load(settings); return; }
            string portable = Path.Combine(root, "portable-settings.xml");
            var xml = File.Exists(portable) ? SettingsProfile.Load(portable) : (registryReader ?? ReadLegacyRegistry)();
            string defaults = Path.Combine(root, "restarter-defaults.xml");
            if (!File.Exists(defaults))
            {
                using (var stream = typeof(FirstRunImport).Assembly.GetManifestResourceStream("ReStarter.Defaults.xml"))
                    SettingsProfile.SaveAtomic(defaults, XDocument.Load(stream));
            }
            // An empty fresh machine has no old preferences to import.
            if (!xml.Root.Elements("value").Any()) xml = SettingsProfile.Load(defaults);
            string oldConfig = Path.Combine(root, "starter.config"), config = Path.Combine(root, "restarter.config");
            if (!File.Exists(config) && File.Exists(oldConfig))
            {
                var ini = new IniFile(oldConfig);
                var output = new IniFile(config);
                foreach (string key in new[] { "ModSel", "language", "Theme", "UiTheme", "ModSkins", "UsingINIconfigs" })
                {
                    string value = ini.Read(key, "Settings", "");
                    if (value.Length != 0) output.Write(key, value, "Settings");
                }
            }
            string engine = Path.Combine(root, "Engine"), renderer = Path.Combine(engine, "rddraw.ini"), oldRenderer = Path.Combine(engine, "eid3d11.ini");
            if (!File.Exists(renderer) && File.Exists(oldRenderer)) File.Copy(oldRenderer, renderer);
            // Publish last: if import fails, a retry still sees the missing profile.
            SettingsProfile.SaveAtomic(settings, xml);
        }
    }
}
