using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EIStarter
{
    public static class SimplyHelper
    {
        public static List<Control> GetAllControls(Control container)
        {
            List<Control> controlList = new List<Control>();
            foreach (Control c in container.Controls)
            {
                controlList.Add(c);
                controlList.AddRange(GetAllControls(c));
            }
            return controlList;
        }

        /// <summary>
        /// From: https://stackoverflow.com/questions/11365984/c-sharp-open-file-with-default-application-and-parameters
        /// </summary>
        /// <param name="path"></param>
        public static void OpenWithDefaultProgram(string path)
        {
            using Process fileopener = new Process();

            fileopener.StartInfo.FileName = "explorer";
            fileopener.StartInfo.Arguments = $"\"{path}\"";
            fileopener.Start();
        }

        public static bool IsGameRuning()
        {
            foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(LauncherProfile.EngineName)))
                using (process) if (!process.HasExited) return true;
            return false;
        }
        public static string ComputeFileHash(string filePath)
        {
            using (var sha256 = SHA256.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    byte[] hashBytes = sha256.ComputeHash(stream);
                    return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                }
            }
        }

    }
}
