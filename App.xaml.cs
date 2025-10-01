// App.xaml.cs
using Microsoft.Extensions.DependencyInjection;
using NowPlayingPopup;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;

namespace NowPlayingPopup
{
    public partial class App : Application
    {
        private Mutex? _singleInstanceMutex;
        private const string MUTEX_NAME = "NowPlayingPopup_SingleInstance_Mutex_v1";

        protected override void OnStartup(StartupEventArgs e)
        {
            bool createdNew = false;
            try
            {
                _singleInstanceMutex = new Mutex(true, MUTEX_NAME, out createdNew);
            }
            catch
            {
                createdNew = false;
            }

            if (!createdNew)
            {
                // Đã có instance khác -> cố gắng đưa nó lên trước rồi exit
                TryActivateExistingInstance();

                // Hiện thông báo (nếu bạn không muốn thông báo, comment dòng dưới)
                MessageBox.Show("Ứng dụng đang chạy.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);

                // Kết thúc instance mới
                Shutdown();
                return;
            }

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _singleInstanceMutex?.ReleaseMutex();
                _singleInstanceMutex?.Dispose();
                _singleInstanceMutex = null;
            }
            catch { }
            base.OnExit(e);
        }

        private void TryActivateExistingInstance()
        {
            try
            {
                var current = Process.GetCurrentProcess();
                var procs = Process.GetProcessesByName(current.ProcessName);

                foreach (var p in procs)
                {
                    if (p.Id == current.Id) continue;

                    bool samePath = false;
                    try
                    {
                        samePath = string.Equals(
                            p.MainModule?.FileName ?? "",
                            current.MainModule?.FileName ?? "",
                            StringComparison.OrdinalIgnoreCase);
                    }
                    catch
                    {
                        // có thể không truy cập được MainModule -> bỏ qua
                    }

                    if (!samePath) continue;

                    IntPtr hWnd = p.MainWindowHandle;
                    if (hWnd == IntPtr.Zero)
                    {
                        hWnd = FindWindowByProcessId(p.Id);
                    }

                    if (hWnd != IntPtr.Zero)
                    {
                        BringWindowToFront(hWnd);
                        return;
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

        #region Win32 helpers

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        private const int SW_RESTORE = 9;

        private void BringWindowToFront(IntPtr hWnd)
        {
            try
            {
                // Nếu bị minimize, restore
                ShowWindow(hWnd, SW_RESTORE);

                // Thủ thuật thread attach để đảm bảo SetForegroundWindow có hiệu lực
                IntPtr fore = GetForegroundWindow();
                uint foreThread = GetWindowThreadProcessId(fore, out _);
                uint appThread = GetCurrentThreadId();

                if (foreThread != appThread)
                {
                    AttachThreadInput(appThread, foreThread, true);
                    SetForegroundWindow(hWnd);
                    BringWindowToTop(hWnd);
                    AttachThreadInput(appThread, foreThread, false);
                }
                else
                {
                    SetForegroundWindow(hWnd);
                    BringWindowToTop(hWnd);
                }
            }
            catch
            {
                // ignore
            }
        }

        #endregion

        #region EnumWindows helper to find top-level window by process id

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        private IntPtr FindWindowByProcessId(int pid)
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd)) return true;
                GetWindowThreadProcessId(hWnd, out uint windowPid);
                if (windowPid == (uint)pid)
                {
                    found = hWnd;
                    return false; // dừng enumerate
                }
                return true;
            }, IntPtr.Zero);

            return found;
        }

        #endregion
    }
}
