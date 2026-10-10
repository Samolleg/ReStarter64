using System;
using Microsoft.Win32;

namespace EIStarter
{
    // Existing option bindings, backed exclusively by the XML provider.
    class REGedit
    {
        private string keyPath;
        private readonly string defaultKeyPath;
        private readonly SettingsProfile profile;
        public REGedit(string keyPath, SettingsProfile profile)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.keyPath = keyPath; defaultKeyPath = keyPath + "\\";
        }
        public void SwitchSubKeyPath(string path) { if (!string.IsNullOrWhiteSpace(path)) keyPath = defaultKeyPath + path; }
        public string Read(string name, string fallback = null) => profile.Read(keyPath, name) as string ?? fallback;
        public int Read(string name, int fallback = 0) => profile.Read(keyPath, name) is int number ? number : (keyPath.EndsWith("camera settings", StringComparison.OrdinalIgnoreCase) ? int.MinValue : fallback);
        public byte[] ReadB(string name, byte[] fallback = null) => profile.Read(keyPath, name) is byte[] bytes && bytes.Length == 4 ? bytes : BitConverter.GetBytes(float.NaN);
        public void WriteB(string name, byte[] data, string reserved = "nil") => profile.Write(keyPath, name, data, RegistryValueKind.Binary);
        public void Write(string name, string data, string reserved = "nil") => profile.Write(keyPath, name, data, RegistryValueKind.String);
        public void Write(string name, int data, string reserved = "nil") => profile.Write(keyPath, name, data, RegistryValueKind.DWord);
        public void Write(string name, bool data, string reserved = "nil") => profile.Write(keyPath, name, Convert.ToInt32(data), RegistryValueKind.DWord);
    }
}
