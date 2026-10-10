using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace EIStarter
{
    internal static class RebuildLaunch
    {
        internal static ProcessStartInfo Prepare(string root)
        {
            root = Path.GetFullPath(root);
            string engine = Path.Combine(root, "Engine"), exe = Path.Combine(engine, "EIRebuild.exe");
            foreach (string required in new[] { exe, Path.Combine(engine, "RDDraw.dll") })
                if (!File.Exists(required)) throw new FileNotFoundException("Для запуска EIRebuild нужен файл: " + required, required);
            string profile = Path.Combine(root, "restarter-settings.xml");
            SettingsProfile.Load(profile);
            return new ProcessStartInfo(exe) { WorkingDirectory = engine, UseShellExecute = false, Arguments = "-profile \"" + profile + "\"" };
        }
        internal static void SelectMod(string root, string modFolder)
        {
            var profile = new SettingsProfile(Path.Combine(root, "restarter-settings.xml"));
            profile.Write("", "Path", Path.GetFullPath(root) + Path.DirectorySeparatorChar, RegistryValueKind.String);
            profile.Write("", "AddonPath", Path.Combine(modFolder, "addon.dll"), RegistryValueKind.String);
            profile.Write("Settings", "AddonPath", modFolder + Path.DirectorySeparatorChar, RegistryValueKind.String);
            profile.Commit(profile.Plan.Values.ToList());
        }
    }
}
