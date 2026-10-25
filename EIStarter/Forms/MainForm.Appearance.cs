using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace EIStarter
{
    public partial class StarterForm
    {
        private TableLayoutPanel appearanceLayout;
        private Label heading;
        private Label modCaption;
        private ComboBox appearanceChoice;
        private Control modChoice;
        private Control themeChoices;

        private void ApplyAppearance()
        {
            UiText.Language = lang;
            if (appearanceLayout == null) CreateAppearance();
            heading.Text = lang == "ru" ? "ПРОКЛЯТЫЕ\nЗЕМЛИ" : "EVIL\nISLANDS";
            modCaption.Text = UiText.Get("SelectMod");
            btPlay.Text = UiText.Get("Play");
            btOptions.Text = UiText.Get("Settings");
            btReadme.Text = UiText.Get("Information");
            btWeb.Text = UiText.Get("Website");
            btModInfo.Text = UiText.Get("AboutMod");
            btLang.Text = lang.ToUpperInvariant();


            for (int i = 0; i < themeChoices.Controls.Count; i++) themeChoices.Controls[i].AccessibleName = UiText.Get(i == 0 ? "DarkTheme" : "LightTheme");
            modChoice.Text = cbbMod.SelectedIndex >= 0 ? DescribeMod(cbbMod.SelectedIndex) + "   ›" : UiText.Get("NoMods") + "   ›";
            LauncherTheme.Apply(this);
        }

        private void CreateAppearance()
        {
            SuspendLayout();
            BackgroundImage = null;
            FormBorderStyle = FormBorderStyle.Sizable;
            Text = LauncherProfile.Name;
            MaximizeBox = true;
            Font = new Font("Segoe UI", 10F);
            Controls.Clear();
            appearanceLayout = new TableLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, ColumnCount = 1, RowCount = 2 };
            appearanceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            appearanceLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            appearanceLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(appearanceLayout);
            var artwork = new ArtworkPanel { Name = "artwork", Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(36, 30, 36, 18) };
            artwork.LoadArtwork(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "background.jpg"));
            appearanceLayout.Controls.Add(artwork, 0, 0);
            heading = new Label { AutoSize = true, Font = new Font("Georgia", 32F, FontStyle.Bold), Location = new Point(36, 54), BackColor = Color.Transparent };
            var eyebrow = new Label { AutoSize = true, Text = "E V I L   I S L A N D S", Font = new Font("Segoe UI", 9F), Location = new Point(39, 28), BackColor = Color.Transparent };
            artwork.Controls.Add(heading);
            artwork.Controls.Add(eyebrow);
            var launch = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(32, 20, 32, 16), Margin = Padding.Empty, ColumnCount = 3, RowCount = 3 };
            launch.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            launch.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            launch.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            for (int i = 0; i < 3; i++) launch.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            appearanceLayout.Controls.Add(launch, 0, 1);
            modCaption = new Label { AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold), Margin = new Padding(0, 0, 0, 7) };
            launch.Controls.Add(modCaption, 0, 0);
            launch.SetColumnSpan(modCaption, 2);
            cbbMod.Font = Font;
            cbbMod.Dock = DockStyle.Fill;
            cbbMod.Margin = new Padding(0, 0, 10, 0);
            cbbMod.DropDownWidth = 560;
            cbbMod.Visible = false;
            Controls.Add(cbbMod);
            modChoice = SelectionControls.Choice(cbbMod, UiText.Get("ModPicker"), true, DescribeMod);
            modChoice.Font = new Font("Segoe UI", 12F);
            launch.Controls.Add(modChoice, 0, 1);
            StyleAction(btModInfo, false);
            btModInfo.Margin = new Padding(0, 0, 24, 8);
            launch.Controls.Add(btModInfo, 1, 1);
            StyleAction(btPlay, true);
            btPlay.Dock = DockStyle.Fill;
            btPlay.Margin = new Padding(20, 0, 0, 0);
            launch.Controls.Add(btPlay, 2, 0);
            launch.SetRowSpan(btPlay, 2);
            var navigation = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0, 20, 0, 0) };
            foreach (var button in new[] { btOptions, btReadme, btWeb, btLang })
            {
                StyleAction(button, false);
                navigation.Controls.Add(button);
            }
            launch.Controls.Add(navigation, 0, 2);
            launch.SetColumnSpan(navigation, 2);
            appearanceChoice = new ComboBox { Name = "appearanceChoice", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, Margin = new Padding(20, 22, 0, 0), AccessibleName = "Theme" };
            appearanceChoice.Items.AddRange(new object[] { "Graphite theme", "Light theme" });
            appearanceChoice.SelectedIndex = LauncherTheme.Dark ? 0 : 1;
            appearanceChoice.SelectedIndexChanged += (sender, args) =>
            {
                LauncherTheme.Dark = appearanceChoice.SelectedIndex == 0;
                new IniFile(LauncherProfile.ConfigName).Write("UiTheme", LauncherTheme.Dark ? "graphite" : "light", "Settings");
                LauncherTheme.Apply(this);
            };
            appearanceChoice.Visible = false;
            Controls.Add(appearanceChoice);
            themeChoices = SelectionControls.ThemeIcons(appearanceChoice);
            themeChoices.Margin = new Padding(20, 22, 0, 0);
            launch.Controls.Add(themeChoices, 2, 2);
            LauncherTheme.Prepare(this);
            float scale = DeviceDpi / 96F;
            if (scale != 1F) appearanceLayout.Scale(new SizeF(scale, scale));
            ClientSize = new Size((int)(960 * scale), (int)(650 * scale));
            MinimumSize = new Size((int)(850 * scale), (int)(590 * scale));
            ResumeLayout(true);
        }

        private string DescribeMod(int index)
        {
            var mod = mods[index];
            return mod.name + (string.IsNullOrWhiteSpace(mod.ver) ? "" : "  ·  " + mod.ver);
        }

        private void StyleAction(Button button, bool primary)
        {
            button.Image = null;
            button.BackgroundImage = null;
            button.Font = new Font("Segoe UI", primary ? 17F : 11F, primary ? FontStyle.Bold : FontStyle.Regular);
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Padding = new Padding(primary ? 24 : 8, 4, primary ? 24 : 8, 4);
            button.AutoSize = true;
            button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            button.Size = new Size(1, 1);
            button.MinimumSize = new Size(0, primary ? 64 : 28);
            button.Margin = new Padding(0, 0, 8, 0);
            button.Anchor = AnchorStyles.Top | AnchorStyles.Bottom;
            button.Tag = primary ? "primary" : "navigation";
        }
    }

    internal sealed class ArtworkPanel : Panel
    {
        private Bitmap artwork;
        public bool HasArtwork => artwork != null;
        public ArtworkPanel() { DoubleBuffered = true; ResizeRedraw = true; }

        public void LoadArtwork(string path)
        {
            Bitmap next = null;
            try
            {
                // Own the decoded pixels, without holding the user's file open.
                using (var stream = File.OpenRead(path))
                using (var image = Image.FromStream(stream))
                    next = new Bitmap(image);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (ArgumentException) { }
            catch (OutOfMemoryException) { } // GDI+ also reports invalid image data this way.
            var previous = artwork;
            artwork = next;
            previous?.Dispose();
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (ClientSize.Width < 1 || ClientSize.Height < 1) return;
            e.Graphics.Clear(Color.FromArgb(24, 25, 29));
            if (artwork != null)
            {
                float scale = Math.Max((float)Width / artwork.Width, (float)Height / artwork.Height);
                float width = artwork.Width * scale, height = artwork.Height * scale;
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                e.Graphics.DrawImage(artwork, new RectangleF((Width - width) / 2, (Height - height) / 2, width, height));
            }
            using (var shade = new LinearGradientBrush(ClientRectangle, Color.FromArgb(155, 10, 12, 17), Color.Transparent, 0F) { WrapMode = WrapMode.TileFlipXY })
                e.Graphics.FillRectangle(shade, ClientRectangle);
            int fadeHeight = Math.Max(1, Height / 4);
            var fade = new Rectangle(0, Height - fadeHeight, Width, fadeHeight);
            using (var shade = new LinearGradientBrush(fade, Color.Transparent, Color.FromArgb(200, 22, 23, 27), 90F) { WrapMode = WrapMode.TileFlipXY })
                e.Graphics.FillRectangle(shade, fade);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) artwork?.Dispose();
            base.Dispose(disposing);
        }
    }
}
