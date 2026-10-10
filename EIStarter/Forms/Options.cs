using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using static EIStarter.RegIni;
using static EIStarter.StarterForm;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Button;

namespace EIStarter
{
    public partial class Options : Form
    {
        [DllImport("user32.dll")]
        public static extern bool EnumDisplaySettings(
                string deviceName, int modeNum, ref DEVMODE devMode);

        [StructLayout(LayoutKind.Sequential)]
        public struct DEVMODE
        {

            private const int CCHDEVICENAME = 0x20;
            private const int CCHFORMNAME = 0x20;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x20)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public ScreenOrientation dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x20)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;

        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct DISPLAY_DEVICE
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            public int StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        [DllImport("user32.dll")]
        public static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

        public Options() : this(SettingsProfile.Root) { }
        internal Options(string registryRoot)
        {
            profile = new SettingsProfile(registryRoot);
            InitializeComponent();
            LauncherTheme.PrepareBatched(this);
            cbbAdapter.Items.Add("Primary Videodriver");

            DEVMODE vDevMode = new DEVMODE();
            int i = 0;
            while (EnumDisplaySettings(null, i, ref vDevMode))
            {
                //if (!cbbResolutions.Items.Contains(vDevMode.dmPelsWidth + @"x" + vDevMode.dmPelsHeight))// + @" " + vDevMode.dmBitsPerPel))
                //    cbbResolutions.Items.Add(vDevMode.dmPelsWidth + @"x" + vDevMode.dmPelsHeight);// + @" " + vDevMode.dmBitsPerPel);
                var tmp = new Resolution(vDevMode.dmPelsWidth, vDevMode.dmPelsHeight);
                if(!rezolutions.ContainsKey(tmp.ToString()))
                    rezolutions.Add(tmp.ToString(), tmp);
                i++;
            }
            cbbResolutions.Items.Clear();
            rezolutions_list = [.. rezolutions.Values];
            rezolutions_list.Sort(new ResolutionComparer());
            foreach (var el in rezolutions_list)
            {
                cbbResolutions.Items.Add(el);
            }
            initSettings();
            PrepareUnspecifiedSettings();
            CollectSettings();
            initialSettings = new Dictionary<string, SettingsProfile.Value>(profile.Plan);
            profile.Plan.Clear();
            CollectPaths();
            initialPaths = new Dictionary<string, SettingsProfile.Value>(profile.Plan);
            initialExeName = NTRtbExeName.Text;
            if (StandalonePackage.Present(AppDomain.CurrentDomain.BaseDirectory))
            {
                string bundledBase = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Base");
                NTRtbPathCD.Text = NTRtbPathGameWork.Text = bundledBase;
                NTRtbPathRes.Text = Path.Combine(bundledBase, "Res");
                NTRtbPathCD.ReadOnly = NTRtbPathGameWork.ReadOnly = NTRtbPathRes.ReadOnly = true;
                NTRbtBrowse1.Enabled = NTRbtBrowse2.Enabled = NTRbtBrowse3.Enabled = btApplyPaths.Enabled = false;
                NTRtbExeName.ReadOnly = true;
            }
            profile.Plan.Clear();
        }
        public Dictionary<string, Resolution> rezolutions = new();
        public List<Resolution> rezolutions_list = new();
        public class Resolution
        {
            public Resolution(int w, int h)
            {
                width = w;
                height = h;
            }
            public int width, height;
            public override string ToString()
            {
                return $"{width}x{height}";
            }
        }
        public class ResolutionComparer : IComparer<Resolution>
        {
            public int Compare(Resolution x, Resolution y)
            {
                // Сравниваем ширину
                if (x.width < y.width)
                    return -1;
                else if (x.width > y.width)
                    return 1;
                else
                {
                    // Если ширины равны, сравниваем высоту
                    if (x.height < y.height)
                        return -1;
                    else if (x.height > y.height)
                        return 1;
                    else
                        return 0; // Если размеры равны
                }
            }
        }
        #region CAMERA struct
        public class CamShortcut
        {
            //[Browsable(true)]
            //[Description("Property: Shortcut")]
            //[Category("Shortcuts")]

            public float Distance { get; set; }
            public float AxisX { get; set; }
            public float AxisY { get; set; }
            public float AxisZ { get; set; }
            public float Angle { get; set; }
            public float Pitch { get; set; }
            public override string ToString()
            {
                return $"Dist: {Math.Round((double)Distance)}, Axis({AxisX},{AxisY},{AxisZ}), Angle: {Angle}, Pitch: {Math.Round((double)Pitch)}";
            }
        }
        public class CamMoveParam
        {
            public float Speed { get; set; }
            public float PowerMouse { get; set; }
            public float PowerKeyboard { get; set; }
            public override string ToString()
            {
                return $"Speed: {Speed}, PwrMouse: {PowerMouse}, PwrKey: {PowerKeyboard}";
            }
        }
        public class CamMinMaxDefParam
        {
            public float Min { get; set; }
            public float Max { get; set; }
            public float Default { get; set; }
            public override string ToString()
            {
                return $"Range: [{Min} : {Default} : {Math.Round((double)Max)}]";
            }
        }
        public class CamMinMaxParam
        {
            public float Min { get; set; }
            public float Max { get; set; }
            public override string ToString()
            {
                return $"Range: [{Min} : {Math.Round((double)Max)}]";
            }
        }
        #endregion
        #region camprop
        class PropertyCategories
        {
            bool m_FlipXAxis;
            [Browsable(true)]
            [Description("Property: FlipXAxis")]
            [Category("General")]
            [DisplayName("Flip X Axis")]
            public bool FlipXAxis
            {
                get { return m_FlipXAxis; }
                set { m_FlipXAxis = value; }
            }

            bool m_FlipYAxis;
            [Browsable(true)]
            [Description("Property: FlipYAxis")]
            [Category("General")]
            [DisplayName("Flip Y Axis")]
            public bool FlipYAxis
            {
                get { return m_FlipYAxis; }
                set { m_FlipYAxis = value; }
            }

            float m_FarClipDistance;
            [Browsable(true)]
            [Description("Property: FarClipDistance")]
            [Category("General")]
            [DisplayName("Clip Distance: Far")]
            public float FarClipDistance
            {
                get { return m_FarClipDistance; }
                set { m_FarClipDistance = value; }
            }

            float m_NearClipDistance;
            [Browsable(true)]
            [Description("Property: NearClipDistance")]
            [Category("General")]
            [DisplayName("Clip Distance: Near")]
            public float NearClipDistance
            {
                get { return m_NearClipDistance; }
                set { m_NearClipDistance = value; }
            }

            float m_TerrainSensetiveArea;
            [Browsable(true)]
            [Description("Property: TerrainSensetiveArea")]
            [Category("Area")]
            [DisplayName("Terrain Sensetive")]
            public float TerrainSensetiveArea
            {
                get { return m_TerrainSensetiveArea; }
                set { m_TerrainSensetiveArea = value; }
            }

            int m_BorderScrollArea;
            [Browsable(true)]
            [Description("Property: BorderScrollArea")]
            [Category("Area")]
            [DisplayName("Border Scroll")]
            public int BorderScrollArea
            {
                get { return m_BorderScrollArea; }
                set { m_BorderScrollArea = value; }
            }

            int m_FrameSelectionSensetiveArea;
            [Browsable(true)]
            [Description("Property: FrameSelectionSensetiveArea")]
            [Category("Area")]
            [DisplayName("Frame Selection Sensetive")]
            public int FrameSelectionSensetiveArea
            {
                get { return m_FrameSelectionSensetiveArea; }
                set { m_FrameSelectionSensetiveArea = value; }
            }

            /// <summary>
            /// CAMERA
            /// </summary>

            CamMinMaxDefParam m_Height = new CamMinMaxDefParam();
            [TypeConverter(typeof(ExpandableObjectConverter))]
            [EditorBrowsable(EditorBrowsableState.Always)]
            [Category("Camera")]
            [DisplayName("Height")]
            public CamMinMaxDefParam Height
            {
                get { return m_Height; }
                set { m_Height = value; }
            }

            CamMinMaxDefParam m_DistanceToCarrier = new CamMinMaxDefParam();
            [TypeConverter(typeof(ExpandableObjectConverter))]
            [EditorBrowsable(EditorBrowsableState.Always)]
            [Category("Camera")]
            [DisplayName("Distance To Carrier")]
            public CamMinMaxDefParam DistanceToCarrier
            {
                get { return m_DistanceToCarrier; }
                set { m_DistanceToCarrier = value; }
            }

            CamMinMaxParam m_LimitDistanceToCarrier = new CamMinMaxParam();
            [TypeConverter(typeof(ExpandableObjectConverter))]
            [EditorBrowsable(EditorBrowsableState.Always)]
            [Category("Camera")]
            [DisplayName("Distance To Carrier Limit")]
            public CamMinMaxParam LimitDistanceToCarrier
            {
                get { return m_LimitDistanceToCarrier; }
                set { m_LimitDistanceToCarrier = value; }
            }

            CamMinMaxParam m_DefaultPitch = new CamMinMaxParam();
            [TypeConverter(typeof(ExpandableObjectConverter))]
            [EditorBrowsable(EditorBrowsableState.Always)]
            [Category("Camera")]
            [DisplayName("Pitch")]
            public CamMinMaxParam DefaultPitch
            {
                get { return m_DefaultPitch; }
                set { m_DefaultPitch = value; }
            }

            /// <summary>
            /// BRIEF CAMERA
            /// </summary>

            CamMinMaxParam m_BriefDistanceToCarrier = new CamMinMaxParam();
            [TypeConverter(typeof(ExpandableObjectConverter))]
            [EditorBrowsable(EditorBrowsableState.Always)]
            [Category("Briefing Camera")]
            [DisplayName("Distance To Carrier")]
            public CamMinMaxParam BriefDistanceToCarrier
            {
                get { return m_BriefDistanceToCarrier; }
                set { m_BriefDistanceToCarrier = value; }
            }

            CamMinMaxParam m_BriefLimitDistanceToCarrier = new CamMinMaxParam();
            [TypeConverter(typeof(ExpandableObjectConverter))]
            [EditorBrowsable(EditorBrowsableState.Always)]
            [Category("Briefing Camera")]
            [DisplayName("Distance To Carrier Limit")]
            public CamMinMaxParam BriefLimitDistanceToCarrier
            {
                get { return m_BriefLimitDistanceToCarrier; }
                set { m_BriefLimitDistanceToCarrier = value; }
            }

            CamMinMaxParam m_BriefDefaultPitch = new CamMinMaxParam();
            [TypeConverter(typeof(ExpandableObjectConverter))]
            [EditorBrowsable(EditorBrowsableState.Always)]
            [Category("Briefing Camera")]
            [DisplayName("Pitch")]
            public CamMinMaxParam BriefDefaultPitch
            {
                get { return m_BriefDefaultPitch; }
                set { m_BriefDefaultPitch = value; }
            }

            /// <summary>
            /// SHORTCUTS
            /// </summary>
            
            private CamShortcut m_Shortcut0 = new CamShortcut();
            private CamShortcut m_Shortcut1 = new CamShortcut();
            private CamShortcut m_Shortcut2 = new CamShortcut();
            private CamShortcut m_Shortcut3 = new CamShortcut();
            private CamMoveParam m_Pitch = new CamMoveParam();
            private CamMoveParam m_Rotate = new CamMoveParam();
            private CamMoveParam m_Scroll = new CamMoveParam();
            private CamMoveParam m_Zoom = new CamMoveParam();

            [TypeConverter(typeof(ExpandableObjectConverter))]
            [EditorBrowsable(EditorBrowsableState.Always)]
            [Category("General")]
            [DisplayName("Pitch")]
            public CamMoveParam Pitch
            {

                get { return m_Pitch; }
                set { m_Pitch = value; }
            }

            [TypeConverter(typeof(ExpandableObjectConverter))]
            [EditorBrowsable(EditorBrowsableState.Always)]
            [Category("General")]
            [DisplayName("Rotate")]
            public CamMoveParam Rotate
            {

                get { return m_Rotate; }
                set { m_Rotate = value; }
            }

            [TypeConverter(typeof(ExpandableObjectConverter))]
            [EditorBrowsable(EditorBrowsableState.Always)]
            [Category("General")]
            [DisplayName("Scroll")]
            public CamMoveParam Scroll
            {

                get { return m_Scroll; }
                set { m_Scroll = value; }
            }

            [TypeConverter(typeof(ExpandableObjectConverter))]
            [EditorBrowsable(EditorBrowsableState.Always)]
            [Category("General")]
            [DisplayName("Zoom")]
            public CamMoveParam Zoom
            {

                get { return m_Zoom; }
                set { m_Zoom = value; }
            }

            float m_XDeploy;
            [Browsable(true)]
            [Description("Property: XDeploy")]
            [Category("General")]
            [DisplayName("Deploy X")]
            public float XDeploy
            {
                get { return m_XDeploy; }
                set { m_XDeploy = value; }
            }

            float m_YDeploy;
            [Browsable(true)]
            [Description("Property: YDeploy")]
            [Category("General")]
            [DisplayName("Deploy Y")]
            public float YDeploy
            {
                get { return m_YDeploy; }
                set { m_YDeploy = value; }
            }

            [TypeConverter(typeof(ExpandableObjectConverter))]
            [EditorBrowsable(EditorBrowsableState.Always)]
            [Category("Shortcuts")]
            [DisplayName("Shortcut 0")]
            public CamShortcut Shortcut0
            {

                get { return m_Shortcut0; }
                set { m_Shortcut0 = value; }
            }

            [TypeConverter(typeof(ExpandableObjectConverter))]
            [EditorBrowsable(EditorBrowsableState.Always)]
            [Category("Shortcuts")]
            [DisplayName("Shortcut 1")]
            public CamShortcut Shortcut1
            {
                get { return m_Shortcut1; }
                set { m_Shortcut1 = value; }
            }

            [TypeConverter(typeof(ExpandableObjectConverter))]
            [EditorBrowsable(EditorBrowsableState.Always)]
            [Category("Shortcuts")]
            [DisplayName("Shortcut 2")]
            public CamShortcut Shortcut2
            {
                get { return m_Shortcut2; }
                set { m_Shortcut2 = value; }
            }

            [TypeConverter(typeof(ExpandableObjectConverter))]
            [EditorBrowsable(EditorBrowsableState.Always)]
            [Category("Shortcuts")]
            [DisplayName("Shortcut 3")]
            public CamShortcut Shortcut3
            {
                get { return m_Shortcut3; }
                set { m_Shortcut3 = value; }
            }
        }
#endregion
        private void readshortcut(RegIni ri, CamShortcut cut, string regname)
        {
            cut.Distance = ri.GetFlt(regname + "Distance", "camera settings");
            cut.AxisX = ri.GetFlt(regname + "AxisX", "camera settings");
            cut.AxisY = ri.GetFlt(regname + "AxisY", "camera settings");
            cut.AxisZ = ri.GetFlt(regname + "AxisZ", "camera settings");
            cut.Angle = ri.GetFlt(regname + "Angle", "camera settings");
            cut.Pitch = ri.GetFlt(regname + "Pitch", "camera settings");
        }
        private void readminmax(RegIni ri, CamMinMaxParam cut, string regname)
        {
            cut.Min = ri.GetFlt(string.Format(regname, "Min"), "camera settings");
            cut.Max = ri.GetFlt(string.Format(regname, "Max"), "camera settings");
        }
        private void readminmaxdef(RegIni ri, CamMinMaxDefParam cut, string regname)
        {
            cut.Min = ri.GetFlt(string.Format(regname, "Min"), "camera settings");
            cut.Max = ri.GetFlt(string.Format(regname, "Max"), "camera settings");
            cut.Default = ri.GetFlt(string.Format(regname, "Default"), "camera settings");
        }
        private void readmov(RegIni ri, CamMoveParam cut, string regname)
        {
            cut.Speed = ri.GetFlt(string.Format(regname, "Speed"), "camera settings");
            cut.PowerKeyboard = ri.GetFlt(string.Format(regname, "PowerKeyboard"), "camera settings");
            cut.PowerMouse = ri.GetFlt(string.Format(regname, "PowerMouse"), "camera settings");
        }
        private void writeshortcut(RegIni ri, CamShortcut cut, string regname)
        {
            ri.SetFlt(regname + "Distance", cut.Distance, "camera settings");
            ri.SetFlt(regname + "AxisX", cut.AxisX, "camera settings");
            ri.SetFlt(regname + "AxisY", cut.AxisY, "camera settings");
            ri.SetFlt(regname + "AxisZ", cut.AxisZ, "camera settings");
            ri.SetFlt(regname + "Angle", cut.Angle, "camera settings");
            ri.SetFlt(regname + "Pitch", cut.Pitch, "camera settings");
        }
        private void writeminmax(RegIni ri, CamMinMaxParam cut, string regname)
        {
            ri.SetFlt(string.Format(regname, "Min"), cut.Min, "camera settings");
            ri.SetFlt(string.Format(regname, "Max"), cut.Max, "camera settings");
        }
        private void writeminmaxdef(RegIni ri, CamMinMaxDefParam cut, string regname)
        {
            ri.SetFlt(string.Format(regname, "Min"), cut.Min, "camera settings");
            ri.SetFlt(string.Format(regname, "Max"), cut.Max, "camera settings");
            ri.SetFlt(string.Format(regname, "Default"), cut.Default, "camera settings");
        }
        private void writemov(RegIni ri, CamMoveParam cut, string regname)
        {
            ri.SetFlt(string.Format(regname, "Speed"), cut.Speed, "camera settings");
            ri.SetFlt(string.Format(regname, "PowerKeyboard"), cut.PowerKeyboard, "camera settings");
            ri.SetFlt(string.Format(regname, "PowerMouse"), cut.PowerMouse, "camera settings");
        }

        private void initSettings()
        {
            //TODO: registry/ini switch support

            APIMode DataSource = APIMode.Win;

            IniFile gameini = new IniFile("Engine/config/game.ini");
            IniFile addonini = new IniFile("Engine/addon.ini");

            REGedit addon = new REGedit(@"Software\Gipat.ru\EI_Starter", profile: profile);
            REGedit game = new REGedit(@"Software\Gipat.ru\EI_Starter\EvilIslands", profile: profile);

            RegIni ri = new RegIni(game, gameini, DataSource);
            RegIni ri2 = new RegIni(addon, addonini, DataSource);

            //ri.SetMode(DataSource);
            //ri2.SetMode(DataSource);

            propGridCamera.SelectedObject = new PropertyCategories();
            PropertyCategories prop = (PropertyCategories)propGridCamera.SelectedObject;

            NTRtbExeName.Text = StarterForm.ExeName;
            NTRtbPathCD.Text = ri.GetStr("CD-ROM Path", "Path Settings");
            NTRtbPathRes.Text = ri.GetStr("Resources Path", "Path Settings");
            NTRtbPathGameWork.Text = ri.GetStr("Work Path", "Path Settings");

            prop.BorderScrollArea = ri.GetInt("CameraBorderScrollArea", "camera settings");
            prop.TerrainSensetiveArea = ri.GetFlt("CameraTerrainSensetiveArea", "camera settings");
            prop.FrameSelectionSensetiveArea = ri.GetInt("CameraFrameSelectionSensetiveArea", "camera settings");

            prop.FarClipDistance = ri.GetFlt("FarClipDistance", "camera settings");
            prop.NearClipDistance = ri.GetFlt("NearClipDistance", "camera settings");
            prop.FlipXAxis = ri.GetBool("CameraFlipXAxis", "camera settings");
            prop.FlipYAxis = ri.GetBool("CameraFlipYAxis", "camera settings");
            prop.XDeploy = ri.GetFlt("CameraDefaultXDeploy", "camera settings");
            prop.YDeploy = ri.GetFlt("CameraDefaultYDeploy", "camera settings");

            readshortcut(ri, prop.Shortcut0, "CameraShortcut 0 ");
            readshortcut(ri, prop.Shortcut1, "CameraShortcut 1 ");
            readshortcut(ri, prop.Shortcut2, "CameraShortcut 2 ");
            readshortcut(ri, prop.Shortcut3, "CameraShortcut 3 ");

            readminmax(ri, prop.DefaultPitch, "CameraDefault{0}Pitch");
            readminmax(ri, prop.LimitDistanceToCarrier, "Camera{0}LimitDistanceToCarrier");
            readminmax(ri, prop.BriefLimitDistanceToCarrier, "CameraBriefing{0}LimitDistanceToCarrier");
            readminmax(ri, prop.BriefDistanceToCarrier, "CameraBriefing{0}DistanceToCarrier");
            readminmax(ri, prop.BriefDefaultPitch, "CameraBriefing{0}Pitch");
            readminmaxdef(ri, prop.DistanceToCarrier, "Camera{0}DistanceToCarrier");
            readminmaxdef(ri, prop.Height, "Camera{0}Height");

            readmov(ri, prop.Pitch, "CameraDefaultPitch{0}");
            readmov(ri, prop.Rotate, "CameraDefaultRotate{0}");
            readmov(ri, prop.Scroll, "CameraDefaultScroll{0}");
            readmov(ri, prop.Zoom, "CameraDefaultZoom{0}");

            //checkBox7.Checked = GetBool(game, "safesound", "general settings");

            cbMPSecondSpeed.Checked = ri2.GetBool("MP Speed-control", "settings");
            cbMPDisableMQ.Checked = ri2.GetBool("MQ client", "settings");
            cbMPConsoleSupport.Checked = ri2.GetBool("MP console", "settings");

            SelectIndex(cbbAudio, ri.GetInt("audio", "general settings"));

            SelectIndex(cbbAdapter, ri.GetInt("drawingtarget", "general settings"));

            var targetrez = ri.GetStr("videoresolution", "general settings");
            foreach (var rezfind in cbbResolutions.Items)
            {
                if (rezfind.ToString() == targetrez) 
                { 
                    cbbResolutions.SelectedIndex = cbbResolutions.Items.IndexOf(rezfind);
                    break;
                }
            }

            return;
        }

        private void CollectPaths()
        {
            //TODO: registry/ini switch support
            APIMode DataSource = APIMode.Win;
            IniFile gameini = new("Engine/config/game.ini");
            IniFile addonini = new("Engine/addon.ini");

            REGedit addon = new("Software\\Gipat.ru\\EI_Starter", profile: profile);
            REGedit game = new("Software\\Gipat.ru\\EI_Starter\\EvilIslands", profile: profile);

            RegIni ri = new(game, gameini, DataSource);
            RegIni ri2 = new(addon, addonini, DataSource);


            ri.SetStr("CD-ROM Path", NTRtbPathCD.Text, "Path Settings");
            ri.SetStr("Resources Path", NTRtbPathRes.Text, "Path Settings");
            ri.SetStr("Work Path", NTRtbPathGameWork.Text, "Path Settings");
        }

        private void CollectSettings()
        {
            //TODO: registry/ini switch support

            APIMode DataSource = APIMode.Win;

            IniFile gameini = new("Engine/config/game.ini");
            IniFile addonini = new("Engine/addon.ini");

            REGedit addon = new("Software\\Gipat.ru\\EI_Starter", profile: profile);
            REGedit game = new("Software\\Gipat.ru\\EI_Starter\\EvilIslands", profile: profile);

            RegIni ri = new(game, gameini, DataSource);
            RegIni ri2 = new(addon, addonini, DataSource);
            PropertyCategories prop = (PropertyCategories)propGridCamera.SelectedObject;


            
            if (cbMPSecondSpeed.CheckState != CheckState.Indeterminate) ri2.SetBool("MP Speed-control", cbMPSecondSpeed.Checked, "settings");
            if (cbMPDisableMQ.CheckState != CheckState.Indeterminate) ri2.SetBool("MQ client", cbMPDisableMQ.Checked, "settings");
            if (cbMPConsoleSupport.CheckState != CheckState.Indeterminate) ri2.SetBool("MP console", cbMPConsoleSupport.Checked, "settings");



            ri.SetInt("CameraBorderScrollArea", prop.BorderScrollArea, "camera settings");
            ri.SetFlt("CameraTerrainSensetiveArea", prop.TerrainSensetiveArea, "camera settings");
            ri.SetInt("CameraFrameSelectionSensetiveArea", prop.FrameSelectionSensetiveArea, "camera settings");

            ri.SetFlt("FarClipDistance", prop.FarClipDistance, "camera settings");
            ri.SetFlt("NearClipDistance", prop.NearClipDistance, "camera settings");

            if (!unknownCameraBools.Contains("FlipXAxis")) ri.SetBool("CameraFlipXAxis", prop.FlipXAxis, "camera settings");
            if (!unknownCameraBools.Contains("FlipYAxis")) ri.SetBool("CameraFlipYAxis", prop.FlipYAxis, "camera settings");
            ri.SetFlt("CameraDefaultXDeploy", prop.XDeploy, "camera settings");
            ri.SetFlt("CameraDefaultYDeploy", prop.YDeploy, "camera settings");

            
            writeshortcut(ri, prop.Shortcut0, "CameraShortcut 0 ");
            writeshortcut(ri, prop.Shortcut1, "CameraShortcut 1 ");
            writeshortcut(ri, prop.Shortcut2, "CameraShortcut 2 ");
            writeshortcut(ri, prop.Shortcut3, "CameraShortcut 3 ");

            writeminmax(ri, prop.DefaultPitch, "CameraDefault{0}Pitch");
            writeminmax(ri, prop.LimitDistanceToCarrier, "Camera{0}LimitDistanceToCarrier");
            writeminmax(ri, prop.BriefLimitDistanceToCarrier, "CameraBriefing{0}LimitDistanceToCarrier");
            writeminmax(ri, prop.BriefDistanceToCarrier, "CameraBriefing{0}DistanceToCarrier");
            writeminmax(ri, prop.BriefDefaultPitch, "CameraBriefing{0}Pitch");
            writeminmaxdef(ri, prop.DistanceToCarrier, "Camera{0}DistanceToCarrier");
            writeminmaxdef(ri, prop.Height, "Camera{0}Height");

            writemov(ri, prop.Pitch, "CameraDefaultPitch{0}");
            writemov(ri, prop.Rotate, "CameraDefaultRotate{0}");
            writemov(ri, prop.Scroll, "CameraDefaultScroll{0}");
            writemov(ri, prop.Zoom, "CameraDefaultZoom{0}");

            if (cbbAudio.SelectedIndex >= 0) ri.SetInt("audio", cbbAudio.SelectedIndex, "general settings");


            if (cbbResolutions.SelectedItem != null) ri.SetStr("videoresolution", cbbResolutions.SelectedItem.ToString(), "general settings");
            

        }

        private void numericUpDown1_Leave(object sender, EventArgs e)
        {
            if (!nbDrawRadiusTerrain.Validate())
            {
                btOk.Enabled = false;
            }
        }

        private void _Validating(object sender, CancelEventArgs e)
        {
            NumericUpDown tmp = (NumericUpDown)sender;
            if (Convert.ToString(tmp.Value) == "")
                e.Cancel = true;
        }

        /// <summary>
        /// Get user selecton to target text box
        /// </summary>
        /// <param name="box">target</param>
        private void OpenPath(TextBox box)
        {
            FolderBrowserDialog opn = new()
            {
                ShowNewFolderButton = true,
                SelectedPath = box.Text
            };
            if (opn.ShowDialog() == DialogResult.OK)
            {
                box.Text = opn.SelectedPath;
                if (!box.Text.EndsWith("\\"))
                    box.Text += "\\";
            }
        }

        public void Localise(string lang, bool bCreate = false)
        {
            if (!bCreate) { UiText.Language = lang; return; } // Modern UI uses the cached catalog when built.
            if (!File.Exists(string.Format("lang/{0}/lang.ini", lang)))
                return;

            IniFile ini = new IniFile( string.Format("lang/{0}/lang.ini", lang) );

            List<Control> allControls = SimplyHelper.GetAllControls(this);

            if (bCreate)
            {
                IniFile ini_save = new IniFile(string.Format("lang/{0}/lang_create.ini", lang));
                ini_save.Write(this.Text, this.Text, this.Text);
                foreach (Control control in allControls)
                {
                    if (string.IsNullOrEmpty(control.Name))
                        continue;

                    string low = control.Name.ToLower();

                    if (!string.IsNullOrEmpty(control.Text) &&
                        !low.StartsWith("cbb") &&
                        !low.StartsWith("nb") &&
                        !low.StartsWith("tb") &&
                        !low.StartsWith("prop") &&

                        !low.StartsWith("ntr")
                        )
                        ini_save.Write(control.Name, control.Text, this.Text);
                    else if (!string.IsNullOrEmpty(control.Text)
                        && low.StartsWith("tb")

                        && !low.StartsWith("ntr")
                        )
                    {
                        TextBox tb = (TextBox)control;
                        ini_save.Write(tb.Name, tb.Text.Replace("\r\n","<rn>"), this.Text);
                    }

                    else if (!string.IsNullOrEmpty(control.Name)
                        && low.StartsWith("cbb")
                        && control != cbbResolutions

                        && !low.StartsWith("ntr")
                        )
                    {
                        ComboBox cbb = (ComboBox)control;
                        int itr = 0;
                        foreach (var obj in cbb.Items)
                        {
                            ini_save.Write($"{control.Name}__{itr}", obj.ToString(), this.Text);
                            itr++;
                        }
                    }
                }
            }
            else
            {
                foreach (Control control in allControls)
                {
                    if (string.IsNullOrEmpty(control.Name))
                        continue;
                    string low = control.Name.ToLower();
                    if (
                        !low.StartsWith("cbb") &&
                        !low.StartsWith("nb") &&
                        !low.StartsWith("tb") &&
                        !low.StartsWith("prop") &&

                        !low.StartsWith("ntr")
                        )
                        control.Text = ini.Read(control.Name, this.Text, control.Text);

                    else if (!string.IsNullOrEmpty(control.Text)
                        && low.StartsWith("tb")

                        && !low.StartsWith("ntr")
                        )
                    {
                        TextBox tb = (TextBox)control;
                        tb.Text = ini.Read(control.Name, this.Text, tb.Text).Replace("<rn>", "\r\n");
                    }

                    else if (
                        low.StartsWith("cbb") &&
                        control != cbbResolutions

                        && !low.StartsWith("ntr")
                        )
                    {
                        
                        ComboBox cbb = (ComboBox)control;

                        // .Net 8
#if NET
                        int itr = 0;
                        foreach (var obj in cbb.Items)
                        {
                            cbb.Items[itr] = ini.Read($"{control.Name}__{itr}", this.Text, obj.ToString());
                            itr++;
                        }
#else
                        // .Net FW 4.7
                        for (int i = 0; i < cbb.Items.Count; i++)
                        {
                            cbb.Items[i] = ini.Read($"{control.Name}__{i}", this.Text, cbb.Items[i].ToString());
                        }
#endif
                    }
                }
                this.Text = ini.Read(this.Text, this.Text, this.Text);
            }
        }

        private void btOk_Click(object sender, EventArgs e)
        {
            SaveSettings(false);
        }

        private void btApplyPaths_Click(object sender, EventArgs e)
        {
            SaveSettings(true);
        }

        private void NTRbtBrowse1_Click(object sender, EventArgs e)
        {
            OpenPath(NTRtbPathCD);
        }

        private void NTRbtBrowse2_Click(object sender, EventArgs e)
        {
            OpenPath(NTRtbPathRes);
        }

        private void NTRbtBrowse3_Click(object sender, EventArgs e)
        {
            OpenPath(NTRtbPathGameWork);
        }
    }
}
