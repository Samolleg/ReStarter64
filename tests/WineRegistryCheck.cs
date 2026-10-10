using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

class WineRegistryCheck
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    const string Logical = @"Software\Gipat.ru\EI_Starter\Settings";
    static Type settings, portable;
    static string root;
    static int count;
    static object Call(object o, string name, params object[] args) { return o.GetType().GetMethod(name, F).Invoke(o, args); }
    static object New() { return Activator.CreateInstance(settings, F, null, new object[] { root }, null); }
    static void Write(object p, string name, object data, RegistryValueKind kind) { Call(p, "WriteExact", Logical, name, data, kind); }
    static void Commit(object p, Action<int> hook)
    {
        var plan = (IDictionary)settings.GetField("Plan", F).GetValue(p);
        var list = (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(settings.GetNestedType("Value", F)));
        foreach (var value in plan.Values) list.Add(value);
        Call(p, "Commit", list, hook);
    }
    static object Read(string name) { using (var k = Registry.CurrentUser.OpenSubKey(root + "\\Settings")) return k == null ? null : k.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames); }
    static void Put(string name, object value, RegistryValueKind kind) { using (var k = Registry.CurrentUser.CreateSubKey(root + "\\Settings")) k.SetValue(name, value, kind); }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); count++; }
    static Exception Failure(Action a) { try { a(); } catch (TargetInvocationException e) { return e.InnerException; } throw new Exception("Expected failure"); }
    static int Main(string[] args)
    {
        bool child = args[0] == "--interrupt";
        int offset = child ? 1 : 0;
        var asm = Assembly.LoadFrom(Path.GetFullPath(args[offset]));
        settings = asm.GetType("EIStarter.SettingsProfile", true);
        portable = asm.GetType("EIStarter.PortableProfile", true);
        root = child ? args[3] : @"Software\EIStarterValidation\wine-" + Guid.NewGuid().ToString("N");
        if (child)
        {
            Activator.CreateInstance(portable, F, null, new object[] { args[2], root, args[4] }, null);
            var partial = New();
            Write(partial, "partial-a", 11, RegistryValueKind.DWord);
            Write(partial, "partial-b", 22, RegistryValueKind.DWord);
            Commit(partial, n => { if (n == 1) Environment.Exit(27); });
            return 99;
        }
        string work = Path.GetFullPath(args[1]); Directory.CreateDirectory(work);
        string journal = Path.Combine(work, "recovery.xml");
        try
        {
            var imports = settings.GetMethods(F).Select(m => m.GetCustomAttributes(typeof(DllImportAttribute), false)).SelectMany(a => a);
            Check(!imports.Any(), "Wine settings has no native transaction imports");
            Check(settings.GetMethod("CommitCompatible", F) != null, "Wine backend compiled");
            object[] values = { int.MinValue, long.MinValue, new byte[] { 0, 0, 192, 127 }, new[] { "first", "второй" }, "%TEMP%\\raw", "plain" };
            RegistryValueKind[] kinds = { RegistryValueKind.DWord, RegistryValueKind.QWord, RegistryValueKind.Binary, RegistryValueKind.MultiString, RegistryValueKind.ExpandString, RegistryValueKind.String };
            for (int i = 0; i < values.Length; i++) Put("v" + i, values[i], kinds[i]);
            Put("delete", "restore", RegistryValueKind.String);
            var p = New();
            for (int i = 0; i < values.Length; i++) Write(p, "v" + i, "changed", RegistryValueKind.String);
            Write(p, "delete", null, RegistryValueKind.None);
            Write(p, "new", 42, RegistryValueKind.DWord);
            Failure(() => Commit(p, n => { if (n == values.Length + 2) throw new IOException("injected last-write failure"); }));
            for (int i = 0; i < values.Length; i++)
            {
                var actual = Read("v" + i);
                Check(actual is Array ? ((IEnumerable)actual).Cast<object>().SequenceEqual(((IEnumerable)values[i]).Cast<object>()) : Equals(actual, values[i]), "exact value rollback " + i);
                using (var key = Registry.CurrentUser.OpenSubKey(root + "\\Settings")) Check(key.GetValueKind("v" + i) == kinds[i], "exact kind rollback " + i);
            }
            Check((string)Read("delete") == "restore" && Read("new") == null, "deleted value restored and newly written value removed");
            Commit(p, null);
            Check(Read("delete") == null && (int)Read("new") == 42, "retry after rollback succeeds");
            var conflict = New(); Write(conflict, "first", 1, RegistryValueKind.DWord); Write(conflict, "second", 2, RegistryValueKind.DWord);
            var error = Failure(() => Commit(conflict, n => { if (n == 1) Put("second", 77, RegistryValueKind.DWord); }));
            Check(error is InvalidOperationException && Read("first") == null && (int)Read("second") == 77, "mid-commit conflict preserves external value and rolls back own write");
            var rollbackConflict = New(); Write(rollbackConflict, "owned", 1, RegistryValueKind.DWord);
            error = Failure(() => Commit(rollbackConflict, n => { Put("owned", 99, RegistryValueKind.DWord); throw new IOException("external writer"); }));
            Check(error is AggregateException && (int)Read("owned") == 99, "rollback conflict reported without overwriting external value");

            // The durable lease precedes writes. Terminate a separate process halfway
            // through a nontransactional commit, then recover through the real API.
            Registry.CurrentUser.DeleteSubKeyTree(root, false);
            Put("original", "%TEMP%\\before", RegistryValueKind.ExpandString);
            File.Copy(args[2], Path.Combine(work, "portable-defaults.xml"), true);
            var start = new System.Diagnostics.ProcessStartInfo(Assembly.GetExecutingAssembly().Location,
                "--interrupt \"" + Path.GetFullPath(args[0]) + "\" \"" + work + "\" \"" + root + "\" \"" + journal + "\"") { UseShellExecute = false, CreateNoWindow = true };
            using (var process = System.Diagnostics.Process.Start(start)) { process.WaitForExit(); Check(process.ExitCode == 27, "child terminated after first registry write"); }
            Check((int)Read("partial-a") == 11 && Read("partial-b") == null && File.Exists(journal), "partial commit actually observed with journal retained");
            portable.GetMethod("Recover", F).Invoke(null, new object[] { root, journal });
            Check((string)Read("original") == "%TEMP%\\before" && Read("partial-a") == null && Read("partial-b") == null, "next launch restores original host after partial commit");
            Check(!File.Exists(journal), "successful recovery consumes journal");
            Console.WriteLine("PASS " + count + " Wine registry checks on Windows; actual Wine runtime still required.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        finally { Registry.CurrentUser.DeleteSubKeyTree(root, false); }
    }
}
