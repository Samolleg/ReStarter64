using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Win32;

class PortableCheck
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static int count;
    static void Check(bool b, string message) { if (!b) throw new Exception(message); count++; }
    static object Call(Type t, string name, params object[] args) { return t.GetMethod(name, F).Invoke(null, args); }
    static void Put(string root, string section, string name, object value, RegistryValueKind kind)
    { using (var k = Registry.CurrentUser.CreateSubKey(root + "\\" + section)) k.SetValue(name, value, kind); }
    static object Get(string root, string section, string name)
    { using (var k = Registry.CurrentUser.OpenSubKey(root + "\\" + section)) return k == null ? null : k.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames); }
    static bool Fails(Action action) { try { action(); return false; } catch { return true; } }
    static int Main(string[] args)
    {
        if (args.Length == 5 && args[0] == "--abandon")
        {
            Type childType = Assembly.LoadFrom(args[1]).GetType("EIStarter.PortableProfile", true);
            Activator.CreateInstance(childType, F, null, new object[] { args[2], args[3], args[4] }, null);
            return 23; // Deliberately no Dispose: persisted recovery must work after process exit.
        }
        string root = @"Software\EIStarterValidation\portable-" + Guid.NewGuid().ToString("N");
        string work = Path.GetFullPath(args[1]); Directory.CreateDirectory(work);
        string journal = Path.Combine(work, "recovery.xml");
        var asm = Assembly.LoadFrom(Path.GetFullPath(args[0]));
        var type = asm.GetType("EIStarter.PortableProfile", true);
        Func<string, IDisposable> begin = folder => (IDisposable)Activator.CreateInstance(type, F, null, new object[] { folder, root, journal }, null);
        Action recover = () => Call(type, "Recover", root, journal);
        try
        {
            File.Copy(args[2], Path.Combine(work, "portable-defaults.xml"), true);
            const string general = @"EvilIslands\General Settings", game = @"EvilIslands\Game Specific Settings";
            Put(root, general, "Audio", 0, RegistryValueKind.DWord);
            Put(root, general, "videoresolution", "640x480", RegistryValueKind.String);
            Put(root, "Settings", "Console AutoExec", new[] { "foreign command", "second command" }, RegistryValueKind.MultiString);
            Put(root, "Settings", "foreign-binary", new byte[] { 0, 0, 192, 127 }, RegistryValueKind.Binary);
            Put(root, "Settings", "minimum", int.MinValue, RegistryValueKind.DWord);
            Put(root, "Settings", "long", long.MinValue, RegistryValueKind.QWord);
            Put(root, "Settings", "raw", "%TEMP%\\foreign", RegistryValueKind.ExpandString);
            Put(root, "Unrelated", "sentinel", 42, RegistryValueKind.DWord);
            using (begin(work))
            {
                Check(File.Exists(journal), "durable journal before runtime lease");
                Check((int)Get(root, general, "Audio") == 2, "safe audio on first run despite foreign off setting");
                Check(Get(root, general, "videoresolution") == null, "foreign display mode not inherited");
                Check((int)Get(root, game, "Quest Light R") == 120 && (int)Get(root, game, "Quest Light Radius") == 5, "packaged quest lighting");
                Check(((string[])Get(root, "Settings", "Console AutoExec")).Length == 0, "foreign commands excluded");
                Check(Get(root, "Settings", "foreign-binary") == null, "unknown host setting excluded during lease");
                Put(root, general, "Audio", 1, RegistryValueKind.DWord);
                Put(root, general, "videoresolution", "1920x1080", RegistryValueKind.String);
                Put(root, @"EvilIslands\Path Settings", "Work Path", "temporary package path", RegistryValueKind.String);
                Check(Fails(() => begin(work)), "pending journal blocks second lease");
            }
            Check(!File.Exists(journal), "successful restore consumes journal");
            Check((int)Get(root, general, "Audio") == 0, "host audio restored");
            Check((string)Get(root, general, "videoresolution") == "640x480", "host display restored");
            Check(((string[])Get(root, "Settings", "Console AutoExec")).SequenceEqual(new[] { "foreign command", "second command" }), "MULTI_SZ roundtrip");
            Check(((byte[])Get(root, "Settings", "foreign-binary")).SequenceEqual(new byte[] { 0, 0, 192, 127 }), "NaN-shaped binary restored exactly");
            Check((int)Get(root, "Settings", "minimum") == int.MinValue, "minimum DWORD restored exactly");
            Check((long)Get(root, "Settings", "long") == long.MinValue, "QWORD restored");
            Check((string)Get(root, "Settings", "raw") == "%TEMP%\\foreign", "ExpandString remains raw");
            Check((int)Get(root, "Unrelated", "sentinel") == 42, "outside scope preserved");
            Check(Get(root, @"EvilIslands\Path Settings", "Work Path") == null, "new machine path removed");
            Check(!File.ReadAllText(Path.Combine(work, "portable-settings.xml")).Contains("temporary package path"), "machine paths excluded from portable settings");
            string moved = Path.Combine(work, "relocated"); Directory.CreateDirectory(moved);
            File.Copy(Path.Combine(work, "portable-settings.xml"), Path.Combine(moved, "portable-settings.xml"), true);
            File.Copy(args[2], Path.Combine(moved, "portable-defaults.xml"), true);
            using (begin(moved))
            {
                Check((int)Get(root, general, "Audio") == 1, "user audio selection survives relocation");
                Check((string)Get(root, general, "videoresolution") == "1920x1080", "saved resolution survives relocation");
            }
            // Abandon a lease exactly as a killed launcher does; a new instance restores the journal.
            var childInfo = new System.Diagnostics.ProcessStartInfo(Assembly.GetExecutingAssembly().Location,
                "--abandon \"" + Path.GetFullPath(args[0]) + "\" \"" + moved + "\" \"" + root + "\" \"" + journal + "\"") { UseShellExecute = false, CreateNoWindow = true };
            using (var child = System.Diagnostics.Process.Start(childInfo)) { child.WaitForExit(); Check(child.ExitCode == 23, "separate child abandoned lease"); }
            recover();
            Check((int)Get(root, general, "Audio") == 0, "interrupted lease recovered");
            recover(); Check(!File.Exists(journal), "recovery idempotent");
            string profile = Path.Combine(moved, "portable-settings.xml");
            string original = File.ReadAllText(profile);
            File.WriteAllText(profile, "broken");
            Check(Fails(() => begin(moved)), "corrupt user profile rejected without reset");
            Check(!File.Exists(journal) && (int)Get(root, general, "Audio") == 0, "corrupt profile leaves host untouched");
            File.WriteAllText(profile, original);
            File.WriteAllText(journal, "broken");
            Check(Fails(recover) && (int)Get(root, general, "Audio") == 0, "corrupt journal fails closed");
            File.Delete(journal);
            // Saving preferences can fail; the host must still be restored.
            var last = begin(moved);
            File.Delete(profile); Directory.CreateDirectory(profile);
            Check(Fails(() => last.Dispose()), "profile-write failure reported");
            Check((int)Get(root, general, "Audio") == 0 && !File.Exists(journal), "host restored after profile-write failure");
            Registry.CurrentUser.DeleteSubKeyTree(root, false);
            using (begin(work)) Check((int)Get(root, general, "Audio") == 1, "saved portable preference used on completely clean host");
            var remaining = (System.Collections.ICollection)Call(type, "Snapshot", root);
            Check(remaining.Count == 0, "clean host left without setting values");
            Console.WriteLine("PASS " + count + " portable profile checks; isolated registry only.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        finally
        {
            if (File.Exists(journal)) { try { recover(); } catch { } }
            Registry.CurrentUser.DeleteSubKeyTree(root, false);
        }
    }
}
