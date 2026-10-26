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
            string exe = LauncherProfile.Executable(root), engine = Path.GetDirectoryName(exe);
            foreach (string required in new[] { exe })
                if (!File.Exists(required)) throw new FileNotFoundException("Для запуска " + LauncherProfile.EngineName + " нужен файл: " + required, required);
            string profile = Path.Combine(root, LauncherProfile.SettingsName);
            SettingsProfile.Load(profile);
            string arguments = "-profile \"" + profile + "\"";
#if RESTARTER64
            var settings = new SettingsProfile(profile);
            string selected = settings.Read("Settings", "AddonPath") as string;
            if (string.IsNullOrWhiteSpace(selected)) throw new InvalidDataException("Выберите мод перед запуском.");
            string mod = Path.GetFullPath(Path.Combine(root, selected)).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!File.Exists(Path.Combine(mod, "config.reg"))) throw new FileNotFoundException("В выбранном моде нет config.reg: " + mod);
            arguments += " --mod \"" + mod + "\"";
#endif
            return new ProcessStartInfo(exe) { WorkingDirectory = engine, UseShellExecute = false, Arguments = arguments };
        }
        internal static void SelectMod(string root, string modFolder)
        {
            var profile = new SettingsProfile(Path.Combine(root, LauncherProfile.SettingsName));
            profile.Write("", "Path", Path.GetFullPath(root) + Path.DirectorySeparatorChar, RegistryValueKind.String);
            profile.Write("", "AddonPath", Path.Combine(modFolder, "addon.dll"), RegistryValueKind.String);
            profile.Write("Settings", "AddonPath", modFolder + Path.DirectorySeparatorChar, RegistryValueKind.String);
            profile.Commit(profile.Plan.Values.ToList());
        }
    }
}
