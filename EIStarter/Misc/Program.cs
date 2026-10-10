using System;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EIStarter
{
    internal static class Program
    {
        internal const string UniqueMutexName = "ReStarterSharp";
        internal static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReStarter");
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string className, string title);
        [STAThread]
        static void Main()
        {
            try
            {
                using (var mutex = new Mutex(true, UniqueMutexName, out bool createdNew))
                {
                    if (!createdNew)
                    {
                        var window = FindWindow(null, "ReStarter");
                        if (window != IntPtr.Zero) SetForegroundWindow(window);
                        else MessageBox.Show("ReStarter уже запущен.", "ReStarter");
                        return;
                    }
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    GuiHelper.setSelfDir();
                    Directory.CreateDirectory(DataDirectory);
                    FirstRunImport.Ensure(AppDomain.CurrentDomain.BaseDirectory);
                    Application.Run(new StarterForm());
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "ReStarter", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }
}
