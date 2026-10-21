using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using OptimizationTool.Services;
using OptimizationTool.UI;

namespace OptimizationTool
{
    static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        // unique mutex name for pulse
        private static readonly Mutex mutex = new Mutex(true, "PulseOptimizationToolMutex");

        [STAThread]
        static void Main()
        {
            // If another instance is already running bring it to the front and exit quietly
            if (!mutex.WaitOne(TimeSpan.Zero, true))
            {
                BringExistingInstanceToFront();
                return;
            }

            // initialize discord rpc on startup
            DiscordRpc.Initialize();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());

            // shut down discord rpc on exit
            DiscordRpc.Shutdown();

            mutex.ReleaseMutex();
        }

        private static void BringExistingInstanceToFront()
        {
            var current = Process.GetCurrentProcess();
            foreach (var process in Process.GetProcessesByName(current.ProcessName))
            {
                // find the other running instance (excluding current process ID)
                if (process.Id != current.Id && process.MainWindowHandle != IntPtr.Zero)
                {
                    // restore if minimized and bring to front
                    ShowWindow(process.MainWindowHandle, SW_RESTORE);
                    SetForegroundWindow(process.MainWindowHandle);
                    break;
                }
            }
        }
    }
}