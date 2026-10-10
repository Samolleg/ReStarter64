using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Windows.Forms;

namespace EIStarter
{
    public partial class Options
    {
        private readonly SettingsProfile profile;
        private Dictionary<string, SettingsProfile.Value> initialSettings, initialPaths;
        private string initialExeName;
        private readonly HashSet<string> unknownCameraBools = new HashSet<string>();
        private static void SelectIndex(ComboBox combo, int index) { combo.SelectedIndex = index >= 0 && index < combo.Items.Count ? index : -1; }
        private object ReadGame(string section, string key) { return profile.Read(SettingsProfile.Root + @"\EvilIslands\" + section, key); }
        private void PrepareUnspecifiedSettings()
        {
            foreach (var pair in new[] { (cbMPSecondSpeed,"MP Speed-control"), (cbMPDisableMQ,"MQ client"), (cbMPConsoleSupport,"MP console") })
                if (!(profile.Read(SettingsProfile.Root + @"\settings", pair.Item2) is int)) pair.Item1.CheckState = CheckState.Indeterminate;
            if (!(ReadGame("general settings", "audio") is int)) cbbAudio.SelectedIndex = -1;
            foreach (var axis in new[]{"X","Y"})
                if (!(ReadGame("camera settings", "CameraFlip"+axis+"Axis") is int)) unknownCameraBools.Add("Flip"+axis+"Axis");
        }
        private List<SettingsProfile.Value> PrepareSave(bool pathsOnly)
        {
            profile.Plan.Clear();
            if (pathsOnly) CollectPaths(); else CollectSettings();
            var changes = profile.Changes(pathsOnly ? initialPaths : initialSettings);
            foreach (var value in changes)
            {
                if (value.Data is byte[] bytes)
                {
                    float number = BitConverter.ToSingle(bytes, 0);
                    if (float.IsNaN(number) || float.IsInfinity(number)) throw new InvalidOperationException(UiText.Get("EnterNumber"));
                }
                if (pathsOnly && value.Data is string path && (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)))
                    throw new InvalidOperationException(UiText.Get("InvalidSettingsPath"));
                if (value.Name.Equals("videoresolution", StringComparison.OrdinalIgnoreCase))
                {
                    var size = value.Data.ToString().Split('x');
                    if (size.Length != 2 || !int.TryParse(size[0], out int width) || !int.TryParse(size[1], out int height) || width < 640 || height < 480 || width > 65535 || height > 65535 || width < height)
                        throw new InvalidOperationException(UiText.Get("InvalidResolution"));
                }
            }
            if (pathsOnly)
            {
                if (NTRtbExeName.Text != initialExeName && (!SimplyHelper.ValidateExeName(NTRtbExeName.Text) || !File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Engine",NTRtbExeName.Text))))
                    throw new InvalidOperationException(UiText.Get("InvalidSettingsExe"));
            }
            else ValidateCamera(changes);
            return changes;
        }
        private void ValidateCamera(List<SettingsProfile.Value> changes)
        {
            bool Changed(string name) { return changes.Any(v => v.Path.EndsWith(@"\camera settings",StringComparison.OrdinalIgnoreCase) && v.Name.Equals(name,StringComparison.OrdinalIgnoreCase)); }
            float? Number(string name)
            {
                var entry = profile.Plan.Values.FirstOrDefault(v => v.Path.EndsWith(@"\camera settings",StringComparison.OrdinalIgnoreCase) && v.Name.Equals(name,StringComparison.OrdinalIgnoreCase));
                if (entry == null || !(entry.Data is byte[] bytes)) return null;
                float number = BitConverter.ToSingle(bytes,0);
                return float.IsNaN(number) || float.IsInfinity(number) ? (float?)null : number;
            }
            if (Changed("NearClipDistance") || Changed("FarClipDistance"))
            {
                float? near=Number("NearClipDistance"), far=Number("FarClipDistance");
                if (!near.HasValue || !far.HasValue || near <= 0 || far <= near) throw new InvalidOperationException(UiText.Get("InvalidCameraRange"));
            }
            foreach (var entry in changes.Where(v=>v.Path.EndsWith(@"\camera settings",StringComparison.OrdinalIgnoreCase)))
            {
                if (entry.Data is int integer && integer < 0) throw new InvalidOperationException(UiText.Get("InvalidCameraRange"));
                if (entry.Data is byte[] bytes && (entry.Name.EndsWith("Speed") || entry.Name.EndsWith("PowerKeyboard") || entry.Name.EndsWith("PowerMouse")) && BitConverter.ToSingle(bytes,0) < 0)
                    throw new InvalidOperationException(UiText.Get("InvalidCameraRange"));
            }
            foreach(var template in new[]{"Camera{0}Height","Camera{0}DistanceToCarrier","Camera{0}LimitDistanceToCarrier","CameraDefault{0}Pitch","CameraBriefing{0}LimitDistanceToCarrier","CameraBriefing{0}DistanceToCarrier","CameraBriefing{0}Pitch"})
            {
                string minKey=string.Format(template,"Min"), maxKey=string.Format(template,"Max"), defKey=string.Format(template,"Default");
                if (!Changed(minKey) && !Changed(maxKey) && !Changed(defKey)) continue;
                float? min=Number(minKey), max=Number(maxKey), def=Number(defKey);
                if (!min.HasValue || !max.HasValue || min > max || (def.HasValue && (def < min || def > max)))
                    throw new InvalidOperationException(UiText.Get("InvalidCameraRange"));
            }
        }
        private void SaveSettings(bool pathsOnly)
        {
            DialogResult = DialogResult.None;
            if (!ValidateChildren()) return;
            try
            {
                var changes = PrepareSave(pathsOnly);
                CommitSettings(pathsOnly, () => profile.Commit(changes));
                if (pathsOnly)
                {
                    initialPaths = new Dictionary<string, SettingsProfile.Value>(profile.Plan);
                    StarterForm.ExeName = initialExeName = NTRtbExeName.Text;
                }
                else
                {
                    initialSettings = new Dictionary<string, SettingsProfile.Value>(profile.Plan);
                    DialogResult = DialogResult.OK;
                }
            }
            catch(Exception ex) when (ex is InvalidOperationException || ex is System.ComponentModel.Win32Exception || ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException)
            {
                MessageBox.Show(this, UiText.Get("SettingsNotSaved") + "\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
