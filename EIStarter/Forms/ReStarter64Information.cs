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
            bool russian = UiText.Language == "ru";
            Text = "ReStarter64 — " + UiText.Get("Information");
            Font = new Font("Segoe UI", 10F);
            AutoScaleMode = AutoScaleMode.Dpi;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(840, 580);
            MinimumSize = new Size(640, 420);
            var tabs = new TabControl { Dock = DockStyle.Fill };
            Controls.Add(tabs);
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
            Page(tabs, UiText.Get("Information"), overview);
            Page(tabs, russian ? "Лицензия NVIDIA DLSS" : "NVIDIA DLSS license", Resource("NvidiaLicense"));
            Page(tabs, russian ? "Сторонние компоненты" : "Third-party components", Resource("ThirdParty"));
            Page(tabs, "Microsoft MIT", Resource("MicrosoftLicense"));
            LauncherTheme.Prepare(this);
        }

        private static string Resource(string name)
        {
            using (var stream = typeof(ReStarter64Information).Assembly
                .GetManifestResourceStream("ReStarter64." + name))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
                return reader.ReadToEnd();
        }

        private static void Page(TabControl tabs, string title, string text)
        {
            var page = new TabPage(title) { Padding = new Padding(12) };
            page.Controls.Add(new TextBox { Dock = DockStyle.Fill, Multiline = true,
                ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = true,
                Text = text, Font = new Font("Segoe UI", 10F) });
            tabs.TabPages.Add(page);
        }
    }
}
#endif
