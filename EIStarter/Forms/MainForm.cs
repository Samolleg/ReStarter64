using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
//using WMPLib;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Windows.Forms;
using static EIStarter.RegIni;

namespace EIStarter
{
    public partial class StarterForm : Form
    {
        // TODO: lang button -> lang(planet) & theme(brush) buttons
        //public readonly Size default_size = new Size(640, 360);
        Size size_lay = new Size(640, 360);

        public string lang = "en";
        public string theme = "";

        public string design_dir = @"design\";
        public const string ExeName = "EIRebuild.exe";

        public bool isusecustomskins = true;

        // "mod.config" override "config.reg"
        public bool isINImods = false;

        private List<string> languages = [];
        private List<string> themes = [];
        public List<Mod> mods = [];

        private Dictionary<Control, string> btn2maskMap = [];

        // folder "Mods" exists and contains one or more mods
        public bool isModsExists = false;

        //WindowsMediaPlayer WMP = new WindowsMediaPlayer();
        private readonly SoundPlayer simpleSound = new();


        public class Mod
        {
            public string name = "unknown";
            public string path;
            public string ver = "0.0.0";
            public string author = "unknown";
            public string email = "";
            public string site = "";
            public string date = "";
            public bool issingle = true;
            public bool ismulti = true;

            public string pluginpath = "none";
            public string plugintext = "Plugin";
        }
        public StarterForm()
        {
            InitializeComponent();
            //grfx = this.CreateGraphics();
#if DEBUG
            NTRlbDebug1.Visible = true;
#else
            NTRlbDebug1.Visible = false;
#endif
            btn2maskMap.Add(btPlay, "pl");
            btn2maskMap.Add(btOptions, "op");
            btn2maskMap.Add(btReadme, "re");
            btn2maskMap.Add(btWeb, "we");
            btn2maskMap.Add(btUninstall, "un");
            btn2maskMap.Add(btExit, "ex");
            btn2maskMap.Add(btLang, "lang");
            btn2maskMap.Add(btSkin, "theme");
            btn2maskMap.Add(btModInfo, "info");

            EnumerateLangs();


            IniFile cfg = new("restarter.config");

            theme = cfg.Read("Theme", "Settings", "");

            string lastsel = cfg.Read("ModSel", "Settings");
            string tempconv = cfg.Read("UsingINIconfigs", "Settings"); //isINImods
            if (!string.IsNullOrWhiteSpace(tempconv))
                isINImods = Convert.ToBoolean(tempconv);

string currentLanguage = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            if (!string.IsNullOrWhiteSpace(currentLanguage))
                lang = cfg.Read("language", "Settings", currentLanguage);
            else
                lang = cfg.Read("language", "Settings", "en");

            UiText.Language = lang;
            if (Directory.Exists(@"Mods\"))
            {
                foreach (string modpath in Directory.EnumerateDirectories(@"Mods\"))
                {
                    if (!File.Exists($@"{modpath}\config.reg") && !File.Exists($@"{modpath}\mod.config"))
                        continue;

                    APIMode curMode = APIMode.EI;

                    IniFile iniCfg = new($@"{modpath}\mod.config");

                    EIRegFile regCfg = new();

                    if (File.Exists($@"{modpath}\config.reg") && !regCfg.isLoaded)
                        regCfg.Load($@"{modpath}\config.reg");


                    if (isINImods && File.Exists($@"{modpath}\mod.config"))
                        curMode = APIMode.INI;
                    else if (!regCfg.isLoaded)
                        continue;

                    RegIni ri = new(regCfg, iniCfg, curMode);

                    Mod mod;
                    if (curMode == APIMode.INI)
                        mod = new() { path = $@"{modpath}\mod.config" };
                    else
                        mod = new() { path = $@"{modpath}\config.reg" };
                    mods.Add(mod);
                    isModsExists = true;

                    mod.name = ri.GetStr("Title", "MOD", "Unkn_Title");
                    //mod.path = $@"{modpath}\mod.config";
                    mod.ver = ri.GetStr("Version", "MOD", "");
                    mod.author = ri.GetStr("Author", "MOD", "Unknown_Author");
                    mod.email = ri.GetStr("AuthorEmail", "MOD", "");
                    mod.site = ri.GetStr("URL", "MOD", "");
                    mod.date = ri.GetStr("date_DMY", "MOD", "");
                    mod.pluginpath = ri.GetStr("pluginpath", "Starter", "none");
                    mod.plugintext = ri.GetStr("plugintext", "Starter", "Plugin");
                    try
                    {
                        mod.issingle = ri.GetBool("Single", "MOD", true);
                    }
                    catch { }
                    try
                    {
                        mod.ismulti = ri.GetBool("Multi", "MOD", true);
                    }
                    catch { }
                    cbbMod.Items.Add(mod.name);
                    if (mod.path == lastsel)
                        cbbMod.SelectedIndex = cbbMod.Items.Count - 1;

                    if (cbbMod.SelectedIndex < 0)
                        cbbMod.SelectedIndex = 0;
                }
            }
            else
            {
                Mod mod = new() { path = $@"\mod.config" };
                mods.Add(mod);
                isModsExists = false;

                cbbMod.Items.Clear();
                cbbMod.Items.Add("No mods");
                cbbMod.SelectedIndex = cbbMod.Items.Count - 1;
                //cbbMod.Visible = false;
            }

            /*if (File.Exists(@"design\" + lang + @"\autorun.mp3"))
            {
                WindowsMediaPlayer WMP = new WindowsMediaPlayer();
                WMP.settings.volume = 1000;
                WMP.URL = @"design\" + lang + @"\autorun.mp3";
                WMP.controls.play();
            }*/

            EnumerateThemes();

            InitLang();


            if (SimplyHelper.IsGameRuning())
                MessageBox.Show(UiText.Get("GameRunning"), UiText.Get("Warning"));

            NTRlbDebug1.Text = theme;
        }

        /// <summary>
        /// Set style for all elements by current design_dir + lang.
        /// </summary>
        public void InitLang()
        {
            ApplyAppearance();
        }

        public void ApplyLayout()
        {
            MessageBox.Show("ApplyLayout - not implemented yet!");
            return; 
            this.Size = size_lay;
            List<LayButton> to_resolve = new();

            List<Control> allControls = SimplyHelper.GetAllControls(this);

            //foreach (Control control in allControls)
            for(int i = 0; i< allControls.Count; i++) 
            {
                Control control = allControls[i];
                if (string.IsNullOrEmpty(control.Name))
                    continue;
                if (!btns_map.ContainsKey(control.Name))
                    continue;

                //MessageBox.Show(control.Name);

                //Control ed = control;
                LayButton bt = btns_map[control.Name];

                EditControl(control, bt);
            }
        }
        public void EditControl(Control ed, LayButton bt)
        {
            MessageBox.Show("EditControl - not implemented yet!");
            return;
            switch (bt.xmode)
            {
                case LayMode.Abs:
                    {
                        ed.Location = new Point(bt.xoffset, ed.Location.Y);
                        break;
                    }
                case LayMode.Center:
                    {
                        ed.Location = new Point((int)(this.Size.Width / 2) + bt.xoffset, ed.Location.Y);
                        NTRlbDebug1.Text += "\r\n" + this.Size.Width / 2;
                        break;
                    }
            }
            switch (bt.ymode)
            {
                case LayMode.Abs:
                    {
                        ed.Location = new Point(ed.Location.X, bt.yoffset);
                        break;
                    }
                case LayMode.Center:
                    {
                        ed.Location = new Point(ed.Location.X, (int)(this.Size.Height / 2) + bt.yoffset);
                        break;
                    }
            }
            //this.InvokePaint(ed, new PaintEventArgs(grfx, ed.DisplayRectangle));
        }
        //Graphics grfx = null;

        Dictionary<string, LayButton> btns_map = new(); // Layout data
        public Size ReadLayoutSize(string filename)
        {
            MessageBox.Show("ReadLayoutSize - not implemented yet!");
            return size_lay;
            string[] lines = File.ReadAllLines(filename);
            for (int x = 0; x < lines.Count(); x++)
            {
                if (lines[x].StartsWith("//"))
                    continue;
                #region args parse
                string[] strfield = lines[x].Split(' ');
                int[] intfield = new int[strfield.Length];
                //float[] floatfield = new float[strfield.Length];

                for (int i = 0; i < strfield.Length; i++)
                {
                    if (int.TryParse(strfield[i], out int intValue))
                        intfield[i] = intValue;
                    else
                        intfield[i] = 0;

                    // not else for float variation
                    //if (float.TryParse(strfield[i], out float floatValue))
                    //    floatfield[i] = floatValue;
                    //else
                    //    floatfield[i] = 0f;
                }
                #endregion

                switch (strfield[0])
                {
                    case "#window":
                        {
                            if (strfield.Length > 2)
                            {
                                if (intfield[1] > 200 && intfield[2] > 200)
                                {
                                    size_lay = new Size(intfield[1], intfield[2]);
                                    return size_lay;
                                }
                            }
                            break;
                        }
                }
            }
            return new Size(200, 200);
        }
        public void ReadLayout(string filename)
        {
            MessageBox.Show("ReadLayout - not implemented yet!");
            return;
            string[] lines = File.ReadAllLines(filename);

            var bStart = true;
            var laybtn = new LayButton();

            for (int x = 0; x < lines.Count(); x++)
            {
                if (lines[x].StartsWith("//"))
                    continue;
                #region args parse
                string[] strfield = lines[x].Split(' ');
                int[] intfield = new int[strfield.Length];
                float[] floatfield = new float[strfield.Length];

                for (int i = 0; i < strfield.Length; i++)
                {
                    if (int.TryParse(strfield[i], out int intValue))
                        intfield[i] = intValue;
                    else
                        intfield[i] = 0;

                    // not else for float variation
                    if (float.TryParse(strfield[i], out float floatValue))
                        floatfield[i] = floatValue;
                    else
                        floatfield[i] = 0f;
                }
                #endregion

                switch (strfield[0])
                {/*
                    case "#window":
                        {
                            if (strfield.Length > 2)
                            {
                                if (intfield[1] > 200 && intfield[2] > 200)
                                {
                                    size_lay = new Size(intfield[1], intfield[2]);
                                }
                            }
                            break;
                        }*/
                    case "#button":
                        {
                            if (strfield.Length > 1)
                            {
                                if (bStart)
                                    bStart = false;
                                else
                                {
                                    if (btns_map.ContainsKey(laybtn.name))
                                        btns_map[laybtn.name] = laybtn;
                                    else
                                        btns_map.Add(laybtn.name, laybtn);
                                }
                                if (!btns_map.ContainsKey(strfield[1]))
                                {
                                    laybtn = new LayButton();
                                    laybtn.name = strfield[1];
                                }
                                else
                                    laybtn = btns_map[strfield[1]];
                            }
                            break;
                        }
                    case "#posx":
                        {
                            laybtn.xneedresolve = false;
                            switch (strfield[1])
                            {
                                case "abs":
                                case "absolute":
                                case "xy":
                                    {
                                        laybtn.xmode = LayMode.Abs;
                                        break;
                                    }
                                case "inherit":
                                case "parent":
                                    {
                                        laybtn.xmode = LayMode.Inherit;
                                        laybtn.xneedresolve = true;
                                        break;
                                    }
                                case "left":
                                case "pos1":
                                    {
                                        laybtn.xmode = LayMode.Abs; // equal
                                        break;
                                    }
                                case "center":
                                case "middle":
                                case "pos2":
                                    {
                                        laybtn.xmode = LayMode.Center;
                                        break;
                                    }
                                case "right":
                                case "pos3":
                                    {
                                        laybtn.xmode = LayMode.Pos3;
                                        break;
                                    }
                            }
                            if (strfield.Length > 2)
                                laybtn.xoffset = intfield[2];
                            break;
                        }
                    case "#posy":
                        {
                            laybtn.yneedresolve = false;
                            switch (strfield[1])
                            {
                                case "abs":
                                case "absolute":
                                    {
                                        laybtn.ymode = LayMode.Abs;
                                        break;
                                    }
                                case "inherit":
                                case "parent":
                                    {
                                        laybtn.ymode = LayMode.Inherit;
                                        laybtn.yneedresolve = true;
                                        break;
                                    }
                                case "up":
                                case "top":
                                case "pos1":
                                    {
                                        laybtn.ymode = LayMode.Abs; // equal
                                        break;
                                    }
                                case "center":
                                case "middle":
                                case "pos2":
                                    {
                                        laybtn.ymode = LayMode.Center;
                                        break;
                                    }
                                case "bottom":
                                case "down":
                                case "pos3":
                                    {
                                        laybtn.ymode = LayMode.Pos3;
                                        break;
                                    }
                            }
                            if (strfield.Length > 2)
                                laybtn.yoffset = intfield[2];
                            break;
                        }
                }
            }
            if (!bStart)
            //    bStart = false;
            //else
            {
                if (btns_map.ContainsKey(laybtn.name))
                    btns_map[laybtn.name] = laybtn;
                else
                    btns_map.Add(laybtn.name, laybtn);
            }
        }
        public enum LayMode
        {
            None,

            //Pos1, // Left|Top
            Center, // Center|Middle
            Pos3, // Right|Bottom

            Abs,
            Inherit
        }
        public struct LayButton
        {
            public string name = "";
            public LayMode xmode = LayMode.None;
            public LayMode ymode = LayMode.None;
            public bool xneedresolve = false;
            public bool yneedresolve = false;
            public int xoffset = 0;
            public int yoffset = 0;

            public LayButton()
            {
            }
        }

        /// <summary>
        /// Get all language codes in 'lang' directory.
        /// (old) Get all langs names in design_dir
        /// </summary>
        public void EnumerateLangs()
        {
            if (Directory.Exists("lang"))
            {
                languages.Clear();
                foreach (string langpath in Directory.EnumerateDirectories("lang"))
                {
                    // TODO: know is true translate folder or not

                    languages.Add(Path.GetFileName(langpath));
                }
                //InitLang();
            }
            if (languages.Count < 1)
                languages.Add("en");
        }

        /// <summary>
        /// Get design folder and enumerate themes in directory.
        /// </summary>
        public void EnumerateThemes()
        {
            if (cbbMod.SelectedIndex >= 0)
            {
                Mod mod = mods[cbbMod.SelectedIndex];

                if (File.Exists(mod.path)
                    && Directory.Exists($@"{Path.GetDirectoryName(mod.path)}\design\")
                    )
                {
                    design_dir = $@"{Path.GetDirectoryName(mod.path)}\design\";
                }
                else //if (design_dir != @"design\")
                    design_dir = @"design\";
            }
            else
                design_dir = @"design\";

            themes.Clear();
            foreach (string dir in new string[] { design_dir, @"design\" })
            {
                if (!Directory.Exists(dir))
                    continue;
                foreach (string langpath in Directory.EnumerateDirectories(dir))
                {
                    if (!langpath.ToLowerInvariant().EndsWith("locale")
                        && !themes.Contains(Path.GetFileName(langpath))
                        )
                        themes.Add(Path.GetFileName(langpath));
                }
            }
            //if (themes.Count < 1)
            themes.Add("");
        }

        /// <summary>
        /// Generate design directory priority list
        /// </summary>
        /// <returns></returns>
        public string[] GetPriorityDirs()
        {
            // TODO: HOT! sort by lang or by theme
            return [
                @$"{design_dir}{theme}\locale\{lang}\",
                @$"{design_dir}{theme}\",
                @$"{design_dir}locale\{lang}\",
                design_dir,

                @$"design\{theme}\locale\{lang}\",
                @$"design\{theme}\",
                @$"design\locale\{lang}\",
                @"design\" ];
        }

        /// <summary>
        /// Set button image. Autosize to image.
        /// </summary>
        /// <param name="button">target Button</param>
        /// <param name="mask">img name mask</param>
        private void SetButtonStyle(Button button, string mask)
        {
            // Text controls are painted at the current monitor DPI.
        }

        private void _MouseEnter(object sender, EventArgs e)
        {
            if (btn2maskMap.ContainsKey((Control)sender))
                SetButtonStyle((Button)sender, btn2maskMap[(Control)sender] + "_h");
        }

        private void _MouseLeave(object sender, EventArgs e)
        {
            if (btn2maskMap.ContainsKey((Control)sender))
                SetButtonStyle((Button)sender, btn2maskMap[(Control)sender]);
        }

        private void StarterForm_MouseMove(object sender, MouseEventArgs e)
        {
            Control s = (Control)sender;
            s.Capture = false;
            //this.Opacity = 0.9; 
            Message m = Message.Create(base.Handle, 161, new IntPtr(2), IntPtr.Zero);
            this.WndProc(ref m);
        }

        private void BtnS0(object sender)
        {
            if (btn2maskMap.ContainsKey((Control)sender))
                SetButtonStyle((Button)sender, btn2maskMap[(Control)sender]);
        }
        private bool SoundCheck(string str)
        {
            if (File.Exists(str))
            {
                if (simpleSound.SoundLocation != str)
                    simpleSound.SoundLocation = str;
                return true;
            }
            return false;
        }
        private void BtnS1(object sender, bool fast = false)
        {
            if (!fast)
            {
                foreach (string path in GetPriorityDirs())
                {
                    if (SoundCheck(Path.Combine(path, "click.wav")))
                    {
                        /*
                        WMP.settings.volume = 1000;
                        WMP.URL = design_dir + lang + @"\click.wav";
                        WMP.controls.play();
                        */
                        simpleSound.Play();
                        break;
                    }
                }

                foreach (var button in btn2maskMap)
                {
                    if (button.Key.Equals(sender))
                        SetButtonStyle((Button)button.Key, button.Value + "_d");
                    else
                        SetButtonStyle((Button)button.Key, button.Value);
                }
            }
            else
            {
                foreach (var button in btn2maskMap)
                {
                    if (button.Key.Equals(sender))
                    {
                        SetButtonStyle((Button)button.Key, button.Value + "_d");
                        return;
                    }
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            BtnS1(sender);
            try
            {
                var start = RebuildLaunch.Prepare(AppDomain.CurrentDomain.BaseDirectory);
                if (SimplyHelper.IsGameRuning()) throw new InvalidOperationException(UiText.Get("GameRunning"));
                if (Process.Start(start) != null) Application.Exit();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "ReStarter", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                BtnS0(sender);
            }
        }
        private void button2_Click(object sender, EventArgs e)
        {
            BtnS1(sender);
            using Options options = new();
            //options.Localise("ru",true); // DEBUG: Export translate
            options.Localise(lang);
            options.ShowDialog(this);
            BtnS0(sender);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            BtnS1(sender);

            string path = @$"lang\{lang}\readme.txt";
            string path_def = @"lang\en\readme.txt";
            if (File.Exists(path))
                SimplyHelper.OpenWithDefaultProgram(path);
            else if (File.Exists(path_def))
                SimplyHelper.OpenWithDefaultProgram(path_def);

            BtnS0(sender);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            BtnS1(sender);
            Process.Start(
                new ProcessStartInfo("https://allods.gipat.ru")
                { UseShellExecute = true }
                );
            BtnS0(sender);
        }

        private void button5_Click(object sender, EventArgs e) { }

        private void button6_Click(object sender, EventArgs e)
        {
            BtnS1(sender);
            Application.Exit();
        }


        private void _ChangeUICues(object sender, UICuesEventArgs e)
        {
            BtnS1(sender);
        }

        /// <summary>
        /// Set next lang from languages list.
        /// </summary>
        private void LangSwitch()
        {
            for (int i = 0; i < languages.Count; i++)
            {
                if (languages[i] == lang && i != languages.Count - 1)
                {
                    lang = languages[i + 1];
                    break;
                }
                if (i == languages.Count - 1)
                    lang = languages[0];
            }
            InitLang();
        }

        /// <summary>
        /// Set next theme from themes list.
        /// </summary>
        private void ThemeSwitch()
        {

            for (int i = 0; i < themes.Count; i++)
            {
                if (themes[i] == theme && i != themes.Count - 1)
                {
                    theme = themes[i + 1];
                    break;
                }
                if (i == themes.Count - 1)
                    theme = themes[0];
            }
            NTRlbDebug1.Text = theme;
            InitLang();
        }

        private void langbtn_Click(object sender, EventArgs e)
        {
            BtnS1(sender, true);

            LangSwitch();
        }

        private void skinbtn_Click(object sender, EventArgs e)
        {
            BtnS1(sender, true);

            ThemeSwitch();
        }

        private void StarterForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            IniFile cfg = new("restarter.config");

            if (cbbMod.SelectedIndex >= 0)
                cfg.Write("ModSel", mods[cbbMod.SelectedIndex].path, "Settings");
            cfg.Write("language", lang, "Settings");
            cfg.Write("Theme", theme, "Settings");
            cfg.Write("ModSkins", isusecustomskins.ToString(), "Settings");
            cfg.Write("UsingINIconfigs", isINImods.ToString(), "Settings");

        }

        private void ModCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            BtnS1(sender);
            Mod mod = mods[cbbMod.SelectedIndex];
            /*if (!Directory.Exists(mod.path))
            {
                MessageBox.Show(string.Format(UiText.Get("BadModPath"), mod.path));
                BtnS0(sender);
                return;
            }*/

            EnumerateLangs();
            EnumerateThemes();

            InitLang();

            string modFullPath = isModsExists ? Path.GetDirectoryName(Path.GetFullPath(mod.path)) : AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            RebuildLaunch.SelectMod(AppDomain.CurrentDomain.BaseDirectory, modFullPath);
            // Get addon.dll version
            try
            {
                if (File.Exists(@$"{modFullPath}\addon.dll"))
                    NTRlbAddonVer.Text = $"Parhelion(Addon.dll) v{FileVersionInfo.GetVersionInfo(@$"{modFullPath}\addon.dll").FileVersion}";
                else if (File.Exists($@"{Directory.GetCurrentDirectory()}\addon.dll"))
                    NTRlbAddonVer.Text = $"Parhelion(Addon.dll) v{FileVersionInfo.GetVersionInfo($@"{Directory.GetCurrentDirectory()}\addon.dll").FileVersion}";
                else
                    NTRlbAddonVer.Text = $"Parhelion(Addon.dll) not found!";
            }
            catch { }


            BtnS0(sender);
        }

        private void infobtn_Click(object sender, EventArgs e)
        {
            BtnS1(sender);
            if (cbbMod.SelectedIndex < 0)
            {
                BtnS0(sender);
                return;
            }
            using ModInfo modInfo = new();
            modInfo.SetMod(mods[cbbMod.SelectedIndex]);
            //modInfo.Localise("ru", true); // DEBUG: Export translate
            modInfo.Localise(lang);
            modInfo.ShowDialog();
            BtnS0(sender);
        }

        private void NTRlbAddonVer_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            // TODO: Addon.dll version click!
            Process.Start(
                new ProcessStartInfo("https://addon.gipath.org")
                { UseShellExecute = true }
                );
        }

    }
}
