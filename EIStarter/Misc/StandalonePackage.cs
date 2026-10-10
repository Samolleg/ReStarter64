using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace EIStarter
{
    // Base or the portable profile identifies an unpacked standalone distribution.
    // There is no user setting: all paths are recomputed from the executable's directory.
    internal static class StandalonePackage
    {
        private static bool ValidResolution(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            string[] parts = value.Split('x');
            if (parts.Any(p => p.Length == 0 || p.Any(c => c < '0' || c > '9'))) return false;
            return parts.Length == 2 && int.TryParse(parts[0], out int width) &&
                int.TryParse(parts[1], out int height) && width >= 640 && width <= 65535 &&
                height >= 480 && height <= 65535 && width >= height;
        }

        internal static void EnsureResolution(string registryRoot, int desktopWidth, int desktopHeight)
        {
            var profile = new SettingsProfile(registryRoot);
            string section = SettingsProfile.Root + @"\EvilIslands\General Settings";
            // WideScreen reads this before the original engine can supply any default.
            if (ValidResolution(profile.Read(section, "VIDEORESOLUTION") as string)) return;
            string resolution = desktopWidth + "x" + desktopHeight;
            if (!ValidResolution(resolution)) resolution = "800x600";
            profile.Write(section, "VIDEORESOLUTION", resolution, RegistryValueKind.String);
            profile.Commit(profile.Plan.Values.ToList());
        }

        internal static void PrepareDisplay()
        {
            var mode = new Options.DEVMODE();
            mode.dmSize = (short)System.Runtime.InteropServices.Marshal.SizeOf(typeof(Options.DEVMODE));
            bool found = Options.EnumDisplaySettings(null, -1, ref mode);
            int width = found ? mode.dmPelsWidth : 800, height = found ? mode.dmPelsHeight : 600;
            var available = new HashSet<string>();
            for (int i = 0; i < 10000 && Options.EnumDisplaySettings(null, i, ref mode); i++)
                available.Add(mode.dmPelsWidth + "x" + mode.dmPelsHeight);
            EnsureSupportedResolution(SettingsProfile.Root, width, height, available.ToArray());
        }

        internal static void EnsureSupportedResolution(string root, int width, int height, string[] available)
        {
            EnsureResolution(root, width, height);
            var profile = new SettingsProfile(root);
            string section = SettingsProfile.Root + @"\EvilIslands\General Settings";
            string saved = profile.Read(section, "VIDEORESOLUTION") as string;
            if (available.Length != 0 && !available.Contains(saved))
            {
                string current = width + "x" + height;
                if (!ValidResolution(current)) current = "800x600";
                profile.Write(section, "VIDEORESOLUTION", current, RegistryValueKind.String);
                profile.Commit(profile.Plan.Values.ToList());
            }
        }

        internal static bool Present(string root) => Directory.Exists(Path.Combine(root, "Base")) ||
            File.Exists(Path.Combine(root, "portable-defaults.xml"));

        internal static Dictionary<string, string> Paths(string root, string mod, string exe)
        {
            root = Path.GetFullPath(root);
            mod = Path.GetFullPath(mod);
            string mods = Path.Combine(root, "MODS") + Path.DirectorySeparatorChar;
            if (!mod.StartsWith(mods, StringComparison.OrdinalIgnoreCase) || !SimplyHelper.ValidateExeName(exe))
                throw new InvalidOperationException("Invalid standalone game or mod path.");
            string basePath = Path.Combine(root, "Base") + Path.DirectorySeparatorChar;
            return new Dictionary<string, string> {
                { "Path", root + Path.DirectorySeparatorChar },
                { "AddonPath", Path.Combine(mod, "addon.dll") },
                { @"Settings\AddonPath", mod + Path.DirectorySeparatorChar },
                { @"EvilIslands\Path Settings\Resources Path", Path.Combine(basePath, "Res") + Path.DirectorySeparatorChar },
                { @"EvilIslands\Path Settings\Work Path", basePath },
                { @"EvilIslands\Path Settings\CD-ROM Path", basePath }
            };
        }
    }

    // The launcher stays alive while the game uses the shared legacy registry paths.
    // Restore only our own unchanged values; never overwrite a concurrent external edit.
    internal sealed class StandaloneSession : IDisposable
    {
        private readonly string root;
        private readonly List<SettingsProfile.Value> before = new List<SettingsProfile.Value>();
        private readonly Dictionary<string, string> paths;
        private bool applied;
        internal Process Game { get; private set; }

        internal StandaloneSession(Dictionary<string, string> paths, string root = SettingsProfile.Root)
        {
            this.paths = paths;
            this.root = root;
        }

        internal void Apply()
        {
            if (applied) throw new InvalidOperationException("Standalone session is already active.");
            var profile = new SettingsProfile(root);
            foreach (var item in paths)
            {
                int split = item.Key.LastIndexOf('\\');
                string suffix = split < 0 ? "" : "\\" + item.Key.Substring(0, split);
                string name = item.Key.Substring(split + 1);
                string logical = SettingsProfile.Root + suffix;
                object data = profile.Read(logical, name);
                using (var key = Registry.CurrentUser.OpenSubKey(root + suffix))
                    before.Add(new SettingsProfile.Value { Path = logical, Name = name, Data = data,
                        Kind = data == null ? RegistryValueKind.None : key.GetValueKind(name) });
                profile.Write(logical, name, item.Value, RegistryValueKind.String);
            }
            profile.Commit(profile.Plan.Values.ToList());
            applied = true;
        }

        internal void Start(ProcessStartInfo start)
        {
            // Installed copies used per-path Windows DPI overrides. Carry the same
            // pixel-coordinate contract with the child after a package is moved.
            string layers = start.EnvironmentVariables["__COMPAT_LAYER"] ?? "";
            foreach (string layer in new[] { "HIGHDPIAWARE", "PERPROCESSSYSTEMDPIFORCEON" })
                if (!layers.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                    .Contains(layer, StringComparer.OrdinalIgnoreCase)) layers += " " + layer;
            start.EnvironmentVariables["__COMPAT_LAYER"] = layers.Trim();
            Apply();
            try { Game = Process.Start(start); if (Game == null) throw new IOException("Game process was not created."); }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            if (!applied) return;
            if (Game != null) { Game.WaitForExit(); Game.Dispose(); Game = null; }
            var restore = new SettingsProfile(root);
            foreach (var previous in before)
            {
                object current = restore.Read(previous.Path, previous.Name);
                string suffix = previous.Path.Substring(SettingsProfile.Root.Length).TrimStart('\\');
                string id = suffix.Length == 0 ? previous.Name : suffix + "\\" + previous.Name;
                if (Equals(current, paths[id]))
                    restore.Write(previous.Path, previous.Name, previous.Data, previous.Kind);
            }
            restore.Commit(restore.Plan.Values.ToList());
            applied = false;
        }
    }
}
