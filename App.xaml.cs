using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace MechCore
{
    public partial class App : Application
    {
        private static Mutex? _singleInstanceMutex;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        protected override void OnStartup(StartupEventArgs e)
        {
            const string mutexName = "Global\\MechCore_EngineeringSuite_SingleInstance_Mutex";
            _singleInstanceMutex = new Mutex(true, mutexName, out bool isNewInstance);

            if (!isNewInstance)
            {
                BringExistingInstanceToFront();
                Shutdown();
                return;
            }

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_singleInstanceMutex != null)
            {
                try
                {
                    _singleInstanceMutex.ReleaseMutex();
                }
                catch (ApplicationException)
                {
                    // Мьютекс не принадлежал текущему потоку
                }
                finally
                {
                    _singleInstanceMutex.Dispose();
                    _singleInstanceMutex = null;
                }
            }

            base.OnExit(e);
        }

        private static void BringExistingInstanceToFront()
        {
            var currentProcess = Process.GetCurrentProcess();
            var processes = Process.GetProcessesByName(currentProcess.ProcessName);

            foreach (var process in processes)
            {
                if (process.Id != currentProcess.Id && process.MainWindowHandle != IntPtr.Zero)
                {
                    ShowWindow(process.MainWindowHandle, SW_RESTORE);
                    SetForegroundWindow(process.MainWindowHandle);
                    break;
                }
            }
        }
    }
}
