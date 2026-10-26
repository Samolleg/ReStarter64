using System;
using System.IO;

namespace EIStarter
{
    internal static class LauncherProfile
    {
#if RESTARTER64
        internal const string Name = "ReStarter64";
        internal const string Description = "ReStarter64 for EIRebuild64";
        internal const string EngineName = "EIRebuild64.exe";
        internal const string ConfigName = "restarter64.config";
        internal const string SettingsName = "restarter64-settings.xml";
        internal const string DefaultsName = "restarter64-defaults.xml";
        internal const string DefaultExecutable = @"Engine64\EIRebuild64.exe";
#else
        internal const string Name = "ReStarter";
        internal const string Description = "ReStarter for EIRebuild";
        internal const string EngineName = "EIRebuild.exe";
        internal const string ConfigName = "restarter.config";
        internal const string SettingsName = "restarter-settings.xml";
        internal const string DefaultsName = "restarter-defaults.xml";
        internal const string DefaultExecutable = @"Engine\EIRebuild.exe";
#endif
        internal static string Executable(string root)
        {
            string relative = DefaultExecutable;
#if RESTARTER64
            relative = new IniFile(Path.Combine(root, ConfigName)).Read("ExecutablePath", "Settings", relative);
#endif
            string path = Path.GetFullPath(Path.Combine(root, relative));
            if (!string.Equals(Path.GetFileName(path), EngineName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Expected " + EngineName + ": " + path);
#if RESTARTER64
            // Keep the separate x64 engine layout; renderer settings live in Config64.
            if (string.Equals(Path.GetDirectoryName(path).TrimEnd('\\', '/'),
                Path.Combine(Path.GetFullPath(root), "Engine"), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Use a separate directory for EIRebuild64 (default: Engine64).");
#endif
            return path;
        }
    }
}
