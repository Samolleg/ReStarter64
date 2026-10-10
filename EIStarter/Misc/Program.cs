using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;

namespace EIStarter
{
    internal static class Program
    {
        private static Mutex mutex;
        internal static StandaloneSession StandaloneGame;
        const string uniqueMutexName = "EIStarterSharp";

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        /// <summary>
        /// Главная точка входа для приложения.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // показ ошибки пользователю вместо отладки
#if CATCH
            try
            {
#endif
                mutex = new Mutex(true, uniqueMutexName, out bool createdNew);

                if (!createdNew)
                {
                    const string windowTitle = "Evil Islands"; // TODO: use class StarterForm.<window title>
                    IntPtr hWnd = FindWindow(null, windowTitle);

                    if (hWnd != IntPtr.Zero)
                        SetForegroundWindow(hWnd);
                    else
                        MessageBox.Show("The application is already running, but the window could not be found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                GuiHelper.setSelfDir();
                PortableProfile portable = null;
                try
                {
                    string folder = AppDomain.CurrentDomain.BaseDirectory;
                    bool standalone = StandalonePackage.Present(folder);
                    string recovery = PortableProfile.RecoveryFile;
                    if (standalone || System.IO.File.Exists(recovery))
                    {
                        if (Process.GetProcessesByName("game").Length != 0)
                            throw new InvalidOperationException("Закройте игру перед запуском стартера: её профиль ещё используется.");
                        PortableProfile.Recover(SettingsProfile.Root, recovery);
                        if (standalone)
                        {
                            portable = new PortableProfile(folder, SettingsProfile.Root, recovery);
                            StandalonePackage.PrepareDisplay();
                        }
                    }
                    try { Application.Run(new StarterForm()); }
                    finally
                    {
                        try { StandaloneGame?.Dispose(); StandaloneGame = null; }
                        finally { portable?.Dispose(); portable = null; }
                    }
                }
                catch (Exception ex)
                {
                    try { portable?.Dispose(); }
                    catch (Exception restore) { ex = new Exception(ex.Message + "\nВосстановление профиля: " + restore.Message); }
                    MessageBox.Show(ex.Message, "EIStarter", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
#if CATCH
            }
            catch (Exception ex)
            {
                // TODO: 'help us by sending error to server'
                MessageBox.Show($"General Error:\r\n{ex.Message}\r\nHRESULT: {ex.HResult}\r\nSource: {ex.Source}\r\nStack:\r\n{ex.StackTrace}");

                if (!Debugger.IsAttached)
                    Debugger.Launch(); // Предложит выбрать и подключить отладчик (например, Visual Studio)

            }
#endif

        }
    }
}
