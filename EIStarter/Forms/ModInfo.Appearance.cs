using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace EIStarter
{
    public partial class ModInfo
    {
        private bool cardBuilt;
        protected override void SetVisibleCore(bool value)
        {
            if (value && !cardBuilt)
            {
                cardBuilt = true;
                if (!string.IsNullOrEmpty(mod.path) && Directory.Exists(Path.GetDirectoryName(mod.path)))
                    ModInfo_Load(this, EventArgs.Empty);
                BuildCard();
            }
            base.SetVisibleCore(value);
        }

        private void BuildCard()
        {
            SuspendLayout();
            splitContainer1.Visible = false;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            Font = new Font("Segoe UI", 11F);
            Text = LauncherProfile.Name + " — " + UiText.Get("ModInfo");

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 140));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);
            root.BringToFront();
            var hero = new ArtworkPanel { Dock = DockStyle.Fill, Margin = Padding.Empty };
            hero.LoadArtwork(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "background.jpg"));
            var title = new Label { Name = "modTitle", Text = string.IsNullOrWhiteSpace(mod.name) ? UiText.Get("ModInfo") : mod.name,
                Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(28, 18, 28, 18), Font = new Font("Segoe UI", 26F, FontStyle.Bold) };
            hero.Controls.Add(title);
            root.Controls.Add(hero, 0, 0);
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty, Padding = new Padding(24, 18, 24, 8) };
            var details = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2 };
            details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 75));
            scroll.Controls.Add(details);
            root.Controls.Add(scroll, 0, 1);
            void Row(string key, string text, bool contact = false)
            {
                if (string.IsNullOrWhiteSpace(text)) return;
                int row = details.RowCount++;
                details.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                details.Controls.Add(new Label { Text = UiText.Get(key), AutoSize = true, Dock = DockStyle.Fill,
                    Margin = new Padding(0, 5, 18, 12), ForeColor = Color.Gray }, 0, row);
                Label label = new Label { Name = "meta_" + key, Text = text, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 5, 0, 12) };
                string target = text;
                if (contact && !text.Contains(":") && text.Contains("@")) target = "mailto:" + text;
                if (contact && Uri.TryCreate(target, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == "https" || uri.Scheme == "http" || uri.Scheme == "mailto"))
                {
                    label.Dispose();
                    var link = new LinkLabel { Name = "meta_" + key, Text = text, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 5, 0, 12) };
                    link.LinkClicked += (sender, args) => SimplyHelper.OpenWithDefaultProgram(uri.AbsoluteUri);
                    label = link;
                }
                details.Controls.Add(label, 1, row);
            }
            Row("Version", mod.ver);
            Row("Authors", mod.author);
            Row("Date", mod.date);
            Row("Website", mod.site, true);
            Row("Contact", mod.email, true);
            Row("SinglePlayer", UiText.Get(mod.issingle ? "Yes" : "No"));
            Row("Multiplayer", UiText.Get(mod.ismulti ? "Yes" : "No"));
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(24, 12, 16, 16), Margin = Padding.Empty };
            void Action(string key, EventHandler handler)
            {
                var button = SelectionControls.Button(UiText.Get(key));
                button.Click += handler;
                actions.Controls.Add(button);
            }
            if (File.Exists(readmepath)) Action("btReadme", button2_Click);
            if (File.Exists(changelogpath)) Action("btChangelog", button3_Click);
            if (!string.IsNullOrEmpty(mod.path) && Directory.Exists(Path.GetDirectoryName(mod.path))) Action("btModFolder", button4_Click);
            if (!string.IsNullOrEmpty(mod.path) && File.Exists(Path.Combine(Path.GetDirectoryName(mod.path), mod.pluginpath ?? "")))
                Action(mod.plugintext, button1_Click);
            var close = SelectionControls.Button(UiText.Get("Close"));
            close.Tag = "primary";
            close.DialogResult = DialogResult.Cancel;
            actions.Controls.Add(close);
            CancelButton = close;
            root.Controls.Add(actions, 0, 2);
            LauncherTheme.Apply(root);
            float scale = DeviceDpi / 96F;
            if (scale != 1F) root.Scale(new SizeF(scale, scale));
            ClientSize = new Size((int)(700 * scale), (int)(520 * scale));
            MinimumSize = new Size((int)(600 * scale), (int)(460 * scale));
            ResumeLayout(true);
        }
    }
}
