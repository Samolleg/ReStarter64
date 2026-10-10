using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

class StandaloneCheck
{
    const BindingFlags Flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static int count;
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); count++; }
    static object Call(Type type, object instance, string name, params object[] args) { return type.GetMethod(name, Flags).Invoke(instance, args); }
    static int Main(string[] args)
    {
        if (args.Length == 3 && args[0] == "--child")
        {
            using (var key = Registry.CurrentUser.OpenSubKey(args[1]))
                File.WriteAllText(args[2], (string)key.GetValue("AddonPath"));
            File.WriteAllText(args[2] + ".dpi", Environment.GetEnvironmentVariable("__COMPAT_LAYER") ?? "");
            return 0;
        }
        string registry = @"Software\EIStarterValidation\standalone-" + Guid.NewGuid().ToString("N");
        try
        {
            Assembly assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            Type package = assembly.GetType("EIStarter.StandalonePackage", true);
            Type session = assembly.GetType("EIStarter.StandaloneSession", true);
            string folder = Path.GetFullPath(args[1]);
            string videoKey = registry + @"\EvilIslands\General Settings";
            Call(package, null, "EnsureResolution", registry, 1920, 1080);
            using (var key = Registry.CurrentUser.OpenSubKey(videoKey))
                Check((string)key.GetValue("VIDEORESOLUTION") == "1920x1080", "clean profile initialized before WideScreen");
            using (var key = Registry.CurrentUser.CreateSubKey(videoKey))
            { key.SetValue("VIDEORESOLUTION", "1280x720"); key.SetValue("audio", 2); }
            Call(package, null, "EnsureResolution", registry, 1920, 1080);
            using (var key = Registry.CurrentUser.OpenSubKey(videoKey))
            {
                Check((string)key.GetValue("VIDEORESOLUTION") == "1280x720", "chosen resolution preserved");
                Check((int)key.GetValue("audio") == 2, "unrelated game setting preserved");
            }
            foreach (object invalid in new object[] { "", "garbage", "1920x0", "320x200", "1080x1920", "999999999999x1080", "1920x1080junk", "1920 x1080", "1920x", 42 })
            {
                using (var key = Registry.CurrentUser.CreateSubKey(videoKey)) key.SetValue("VIDEORESOLUTION", invalid);
                Call(package, null, "EnsureResolution", registry, 2560, 1440);
                using (var key = Registry.CurrentUser.OpenSubKey(videoKey))
                    Check((string)key.GetValue("VIDEORESOLUTION") == "2560x1440", "invalid resolution repaired: " + invalid);
            }
            using (var key = Registry.CurrentUser.CreateSubKey(videoKey)) key.DeleteValue("VIDEORESOLUTION");
            Call(package, null, "EnsureResolution", registry, 0, 0);
            using (var key = Registry.CurrentUser.OpenSubKey(videoKey))
                Check((string)key.GetValue("VIDEORESOLUTION") == "800x600", "display-query failure fallback");
            using (var key = Registry.CurrentUser.CreateSubKey(videoKey)) key.SetValue("VIDEORESOLUTION", "3840x2160");
            Call(package, null, "EnsureSupportedResolution", registry, 1920, 1080, new[] { "800x600", "1920x1080" });
            using (var key = Registry.CurrentUser.OpenSubKey(videoKey)) Check((string)key.GetValue("VIDEORESOLUTION") == "1920x1080", "relocation to smaller display replaces unsupported mode");
            using (var key = Registry.CurrentUser.CreateSubKey(videoKey)) key.SetValue("VIDEORESOLUTION", "800x600");
            Call(package, null, "EnsureSupportedResolution", registry, 1920, 1080, new[] { "800x600", "1920x1080" });
            using (var key = Registry.CurrentUser.OpenSubKey(videoKey)) Check((string)key.GetValue("VIDEORESOLUTION") == "800x600", "supported user mode preserved");
            Call(package, null, "EnsureSupportedResolution", registry, 1920, 1080, new string[0]);
            using (var key = Registry.CurrentUser.OpenSubKey(videoKey)) Check((string)key.GetValue("VIDEORESOLUTION") == "800x600", "failed enumeration does not overwrite valid preference");
            foreach (string suffix in new[] { "first copy", "relocated copy" })
            {
                string root = Path.Combine(folder, suffix), mod = Path.Combine(root, "MODS", "Test");
                foreach (string file in new[] { "Engine/game.exe", "MODS/Test/addon.dll", "MODS/Test/config.reg", "Base/Res/textures.res", "Base/Res/figures.res", "Base/Config/keyboard.ini", "Base/3dfpfpu.dll", "Base/2dintmmx.dll" })
                {
                    string path = Path.Combine(root, file); Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, "fixture");
                }
                Check((bool)Call(package, null, "Present", root), "package discovery");
                var paths = (Dictionary<string, string>)Call(package, null, "Paths", root, mod, "game.exe");
                foreach (string value in paths.Values) Check(value.StartsWith(root + Path.DirectorySeparatorChar), "relocation confines every path");
                using (var key = Registry.CurrentUser.CreateSubKey(registry))
                { key.SetValue("Path", "%TEMP%\\previous", RegistryValueKind.ExpandString); key.SetValue("unrelated", 42); }
                object current = Activator.CreateInstance(session, Flags, null, new object[] { paths, registry }, null);
                Call(session, current, "Apply");
                using (var key = Registry.CurrentUser.OpenSubKey(registry)) Check((string)key.GetValue("AddonPath") == Path.Combine(mod, "addon.dll"), "loader priority overridden");
                using (var key = Registry.CurrentUser.CreateSubKey(registry + @"\Settings")) key.SetValue("AddonPath", "external edit");
                Call(session, current, "Dispose");
                using (var key = Registry.CurrentUser.OpenSubKey(registry))
                {
                    Check((string)key.GetValue("Path", null, RegistryValueOptions.DoNotExpandEnvironmentNames) == "%TEMP%\\previous", "original raw value restored");
                    Check(key.GetValueKind("Path") == RegistryValueKind.ExpandString, "original kind restored");
                    Check(key.GetValue("AddonPath") == null, "original absence restored");
                    Check((int)key.GetValue("unrelated") == 42, "unrelated value preserved");
                }
                using (var key = Registry.CurrentUser.OpenSubKey(registry + @"\Settings")) Check((string)key.GetValue("AddonPath") == "external edit", "concurrent edit preserved");
                using (var key = Registry.CurrentUser.OpenSubKey(registry + @"\EvilIslands\Path Settings")) Check(key.GetValueNames().Length == 0, "clean profile paths removed");
                current = Activator.CreateInstance(session, Flags, null, new object[] { paths, registry }, null);
                bool failed = false;
                try { Call(session, current, "Start", new ProcessStartInfo(Path.Combine(root, "missing.exe")) { UseShellExecute = false }); }
                catch (TargetInvocationException) { failed = true; }
                Check(failed, "missing process fails");
                using (var key = Registry.CurrentUser.OpenSubKey(registry)) Check(key.GetValue("AddonPath") == null, "start failure restores registry");
                string marker = Path.Combine(root, "child-observed.txt");
                current = Activator.CreateInstance(session, Flags, null, new object[] { paths, registry }, null);
                var start = new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,
                    "--child \"" + registry + "\" \"" + marker + "\"") { UseShellExecute = false, CreateNoWindow = true };
                string parentLayers = Environment.GetEnvironmentVariable("__COMPAT_LAYER");
                start.EnvironmentVariables["__COMPAT_LAYER"] = "DISABLEDXMAXIMIZEDWINDOWEDMODE HIGHDPIAWARE";
                Call(session, current, "Start", start);
                Call(session, current, "Dispose");
                Check(string.Equals(File.ReadAllText(marker + ".dpi"), "DISABLEDXMAXIMIZEDWINDOWEDMODE HIGHDPIAWARE PERPROCESSSYSTEMDPIFORCEON", StringComparison.OrdinalIgnoreCase), "child inherits DPI contract and preserves other layers without duplicates");
                Check(Environment.GetEnvironmentVariable("__COMPAT_LAYER") == parentLayers, "parent compatibility environment unchanged");
                Check(File.ReadAllText(marker) == Path.Combine(mod, "addon.dll"), "child sees package paths until exit");
                using (var key = Registry.CurrentUser.OpenSubKey(registry)) Check(key.GetValue("AddonPath") == null, "child exit restores registry");
                // Changing the package inventory must not affect selected-mod path setup.
                File.WriteAllText(Path.Combine(root, "standalone.files.sha256"),
                    new string('0', 64) + "  Base/Res/sfx.zipp\n" + new string('0', 64) + "  MODS/JamaisVu135/addon.dll");
                string renamed = Path.Combine(root, "MODS", "Renamed");
                Directory.Move(mod, renamed);
                File.Delete(Path.Combine(root, "Base/Res/textures.res"));
                var changed = (Dictionary<string, string>)Call(package, null, "Paths", root, renamed, "game.exe");
                Check(changed["AddonPath"] == Path.Combine(renamed, "addon.dll"), "stale manifest and changed resources do not lock mod path");
                File.Delete(Path.Combine(root, "standalone.files.sha256"));
                Check((bool)Call(package, null, "Present", root), "standalone works without manifest");
                Call(package, null, "Paths", root, renamed, "game.exe");
                Check(!Directory.Exists(mod), "no old mod directory recreated");
                bool escaped = false;
                try { Call(package, null, "Paths", root, folder, "game.exe"); } catch (TargetInvocationException) { escaped = true; }
                Check(escaped, "mod path confinement retained");
            }
            string incomplete = Path.Combine(folder, "incomplete"); Directory.CreateDirectory(incomplete);
            File.WriteAllText(Path.Combine(incomplete, "portable-defaults.xml"), "profile");
            Check((bool)Call(package, null, "Present", incomplete), "portable profile identifies standalone without manifest");
            Check(!(bool)Call(package, null, "Present", folder), "ordinary launcher remains ordinary");
            Console.WriteLine("PASS " + count + " standalone checks; no game or active profile touched."); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { Registry.CurrentUser.DeleteSubKeyTree(registry, false); }
    }
}
