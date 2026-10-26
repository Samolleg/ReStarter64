#if RESTARTER64
using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace EIStarter
{
    internal sealed class ReStarter64Information : Form
    {
        internal ReStarter64Information()
        {
            SuspendLayout();
            bool russian = UiText.Language == "ru";
            Text = LauncherProfile.Name + " — " + UiText.Get("Information");
            Font = new Font("Segoe UI", 11F);
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(920, 600);
            MinimumSize = new Size(820, 500);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2,
                RowCount = 2, Padding = new Padding(16) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);
            var navigation = new TableLayoutPanel { AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top,
                ColumnCount = 1, Margin = new Padding(0, 0, 12, 0) };
            navigation.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            root.Controls.Add(navigation, 0, 0);
            var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1,
                RowCount = 2, Padding = new Padding(14, 8, 14, 12), Margin = Padding.Empty };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var heading = new Label { AutoSize = true, Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 18F, FontStyle.Bold), Margin = new Padding(0, 0, 0, 14) };
            var body = new TextBox { Dock = DockStyle.Fill, Multiline = true,
                ReadOnly = true, BorderStyle = BorderStyle.None, ScrollBars = ScrollBars.Vertical,
                WordWrap = true, Font = Font, Margin = Padding.Empty };
            content.Controls.Add(heading, 0, 0);
            content.Controls.Add(body, 0, 1);
            root.Controls.Add(content, 1, 0);
            string overview = russian
                ? "ReStarter64 — самостоятельный локальный стартер EIRebuild64.\r\n\r\n"
                    + "Запускает только EIRebuild64 с выбранным модом. Собственные настройки: restarter64.config, "
                    + "restarter64-settings.xml, restarter64-defaults.xml и Config64/rddraw.ini. "
                    + "Параметры рендера соответствуют меню F10; изменения сохраняются при нажатии «Применить».\r\n\r\n"
                    + "Лицензия NVIDIA DLSS и сведения о сторонних компонентах приведены на соседних вкладках. "
                    + "Логотипы и изображения NVIDIA Marks не используются.\r\n\r\n"
                : "ReStarter64 is a standalone local launcher for EIRebuild64.\r\n\r\n"
                    + "Starts EIRebuild64 with the selected mod. Its settings are restarter64.config, "
                    + "restarter64-settings.xml, restarter64-defaults.xml and Config64/rddraw.ini. "
                    + "Renderer settings match F10; Apply saves your edits.\r\n\r\n"
                    + "The NVIDIA DLSS license and third-party notices are included in the other tabs. "
                    + "No NVIDIA logos or NVIDIA Marks artwork are used.\r\n\r\n";
            overview += "This product uses NVIDIA DLSS and NVIDIA NGX.\r\n"
                + "NVIDIA DLSS SDK. Copyright NVIDIA Corporation. All rights reserved.\r\n"
                + "NVIDIA and GeForce RTX are trademarks and/or registered trademarks of NVIDIA Corporation "
                + "in the U.S. and other countries.\r\n\r\n"
                + "DLSS SDK v310.9.16: https://github.com/NVIDIA/DLSS/tree/v310.9.16";
            string[] titles = { UiText.Get("Information"),
                russian ? "Лицензия NVIDIA DLSS" : "NVIDIA DLSS license",
                russian ? "Сторонние компоненты" : "Third-party components", "Microsoft MIT" };
            string[] pages = { overview, Resource("NvidiaLicense"), Resource("ThirdParty"), Resource("MicrosoftLicense") };
            void SelectPage(int index)
            {
                heading.Text = titles[index];
                body.AccessibleName = titles[index];
                body.Text = pages[index];
                body.Select(0, 0);
                foreach (Control entry in navigation.Controls)
                    entry.Tag = entry.Name == "informationSection_" + index ? "selected" : null;
                LauncherTheme.ApplyBatched(navigation);
            }
            for (int i = 0; i < titles.Length; i++)
            {
                int index = i;
                var button = SelectionControls.SectionButton(titles[i], null);
                button.Name = "informationSection_" + i;
                button.AutoSize = true;
                button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                button.MinimumSize = new Size(220, 42);
                button.Padding = new Padding(12, 8, 12, 8);
                button.Dock = DockStyle.Fill;
                button.Click += (sender, args) => SelectPage(index);
                navigation.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                navigation.Controls.Add(button, 0, i);
            }
            var footer = new FlowLayoutPanel { AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 16, 0, 0) };
            var close = SelectionControls.Button(UiText.Get("Close"));
            close.Tag = "primary";
            close.DialogResult = DialogResult.Cancel;
            footer.Controls.Add(close);
            root.Controls.Add(footer, 0, 1);
            root.SetColumnSpan(footer, 2);
            AcceptButton = close;
            CancelButton = close;
            SelectPage(0);
            LauncherTheme.PrepareBatched(this);
            ResumeLayout(true);
        }

        private static string Resource(string name)
        {
            using (var stream = typeof(ReStarter64Information).Assembly
                .GetManifestResourceStream("ReStarter64." + name))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
                return reader.ReadToEnd();
        }
    }
}
#endif
