using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace EIStarter
{
    // One dialog owns a read snapshot and a write plan. No live writes until Commit.
    internal sealed class SettingsProfile
    {
        internal const string Root = @"Software\Gipat.ru\EI_Starter";
        private readonly string root;
        internal sealed class Value
        {
            internal string Path, Name;
            internal object Data;
            internal RegistryValueKind Kind;
        }
        private readonly Dictionary<string, Value> snapshot = new Dictionary<string, Value>(StringComparer.OrdinalIgnoreCase);
        internal Dictionary<string, Value> Plan = new Dictionary<string, Value>(StringComparer.OrdinalIgnoreCase);
        internal SettingsProfile(string root = Root) { this.root = root; }
        private string PathFor(string path) { return root + path.Substring(Root.Length); }
        private static string Id(string path, string name) { return path + "\n" + name; }
        private static Value ReadValue(RegistryKey key, string path, string name)
        {
            object data = key == null ? null : key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            return new Value { Path = path, Name = name, Data = data, Kind = data == null ? RegistryValueKind.None : key.GetValueKind(name) };
        }
        internal object Read(string path, string name)
        {
            path = PathFor(path);
            string id = Id(path, name);
            if (!snapshot.TryGetValue(id, out var value))
            {
                using (var key = Registry.CurrentUser.OpenSubKey(path)) value = ReadValue(key, path, name);
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
            path = PathFor(path);
            Plan[Id(path, name)] = new Value { Path = path, Name = name, Data = data, Kind = kind };
        }
        internal static bool Equal(Value a, Value b)
        {
            if (a == null || b == null) return a == b;
            if (a.Kind != b.Kind) return false;
            if (a.Data is byte[] x && b.Data is byte[] y) return x.SequenceEqual(y);
            if (a.Data is string[] sx && b.Data is string[] sy) return sx.SequenceEqual(sy);
            return Equals(a.Data, b.Data);
        }
        internal List<Value> Changes(Dictionary<string, Value> baseline)
        {
            return Plan.Where(pair => !baseline.TryGetValue(pair.Key, out var previous) || !Equal(previous, pair.Value)).Select(pair => pair.Value).ToList();
        }
        internal void Commit(List<Value> changes, Action<int> afterWrite = null)
        {
            if (changes.Count == 0) return;
#if WINE_COMPAT
            CommitCompatible(changes, afterWrite);
#else
            using (var transaction = CreateTransaction(IntPtr.Zero, IntPtr.Zero, 0, 0, 0, 0, null))
            {
                if (transaction.IsInvalid) throw new Win32Exception();
                try
                {
                    // Read through the transaction so conflicting concurrent commits cannot be silently overwritten.
                    foreach (var previous in snapshot.Values)
                    {
                        SafeRegistryHandle handle;
                        int status = RegOpenKeyTransacted(new IntPtr(unchecked((int)0x80000001)), previous.Path, 0, 0x20019, out handle, transaction, IntPtr.Zero);
                        if (status != 0 && status != 2) throw new Win32Exception(status);
                        using (handle)
                        using (var key = status == 0 ? RegistryKey.FromHandle(handle) : null)
                            if (!Equal(previous, ReadValue(key, previous.Path, previous.Name))) throw new InvalidOperationException(UiText.Get("SettingsChanged"));
                    }
                    int count = 0;
                    foreach (var value in changes)
                    {
                        SafeRegistryHandle handle;
                        int disposition;
                        int status = RegCreateKeyTransacted(new IntPtr(unchecked((int)0x80000001)), value.Path, 0, null, 0, 0x2001F, IntPtr.Zero, out handle, out disposition, transaction, IntPtr.Zero);
                        if (status != 0) throw new Win32Exception(status);
                        using (handle)
                        using (var key = RegistryKey.FromHandle(handle))
                        {
                            if (value.Data == null) key.DeleteValue(value.Name, false);
                            else key.SetValue(value.Name, value.Data, value.Kind);
                        }
                        afterWrite?.Invoke(++count);
                    }
                    if (!CommitTransaction(transaction)) throw new Win32Exception();
                }
                catch { RollbackTransaction(transaction); throw; }
            }
#endif
            foreach (var value in changes) snapshot[Id(value.Path, value.Name)] = value;
        }
#if WINE_COMPAT
        private static Value Current(Value value)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(value.Path))
                return ReadValue(key, value.Path, value.Name);
        }
        private static void Set(Value value)
        {
            if (value.Data == null)
            {
                using (var key = Registry.CurrentUser.OpenSubKey(value.Path, true))
                    key?.DeleteValue(value.Name, false);
            }
            else
                using (var key = Registry.CurrentUser.CreateSubKey(value.Path))
                    key.SetValue(value.Name, value.Data, value.Kind);
        }
        // Wine has no transacted registry contract. The launcher mutex serializes
        // our writers; PortableProfile's durable journal covers an interrupted lease.
        // These checks/compensating writes are not a kernel transaction and cannot
        // lock out unrelated programs. Do not overwrite a detected external edit.
        private void CommitCompatible(List<Value> changes, Action<int> afterWrite)
        {
            foreach (var previous in snapshot.Values)
                if (!Equal(previous, Current(previous)))
                    throw new InvalidOperationException(UiText.Get("SettingsChanged"));
            var attempted = new List<Value>();
            try
            {
                foreach (var value in changes)
                {
                    var previous = snapshot[Id(value.Path, value.Name)];
                    if (!Equal(previous, Current(previous)))
                        throw new InvalidOperationException(UiText.Get("SettingsChanged"));
                    attempted.Add(value);
                    Set(value);
                    if (!Equal(value, Current(value)))
                        throw new InvalidOperationException(UiText.Get("SettingsChanged"));
                    afterWrite?.Invoke(attempted.Count);
                }
            }
            catch (Exception failure)
            {
                var errors = new List<Exception> { failure };
                for (int i = attempted.Count - 1; i >= 0; i--)
                {
                    var value = attempted[i];
                    var previous = snapshot[Id(value.Path, value.Name)];
                    try
                    {
                        var current = Current(value);
                        if (Equal(previous, current)) continue;
                        if (!Equal(value, current))
                            throw new InvalidOperationException(UiText.Get("SettingsChanged"));
                        Set(previous);
                        if (!Equal(previous, Current(previous)))
                            throw new InvalidOperationException(UiText.Get("SettingsChanged"));
                    }
                    catch (Exception restore) { errors.Add(restore); }
                }
                if (errors.Count != 1)
                    throw new AggregateException("Не удалось полностью восстановить настройки после ошибки записи.", errors);
                throw;
            }
        }
#else
        [DllImport("KtmW32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateTransaction(IntPtr attributes, IntPtr uow, uint options, uint isolation, uint flags, uint timeout, string description);
        [DllImport("KtmW32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CommitTransaction(SafeFileHandle transaction);
        [DllImport("KtmW32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RollbackTransaction(SafeFileHandle transaction);
        [DllImport("advapi32.dll", EntryPoint = "RegCreateKeyTransactedW", CharSet = CharSet.Unicode)]
        private static extern int RegCreateKeyTransacted(IntPtr root, string subkey, int reserved, string cls, int options, int access, IntPtr security, out SafeRegistryHandle key, out int disposition, SafeFileHandle transaction, IntPtr extended);
        [DllImport("advapi32.dll", EntryPoint = "RegOpenKeyTransactedW", CharSet = CharSet.Unicode)]
        private static extern int RegOpenKeyTransacted(IntPtr root, string subkey, int options, int access, out SafeRegistryHandle key, SafeFileHandle transaction, IntPtr extended);
#endif
    }
}
