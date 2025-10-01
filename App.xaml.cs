// App.xaml.cs - Fixed with System Tray
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using WpfApp = System.Windows.Application;
using WpfWindow = System.Windows.Window;

namespace NowPlayingPopup
{
    public partial class App : WpfApp
    {
        private Mutex? _singleInstanceMutex;
        private const string MUTEX_NAME = "NowPlayingPopup_SingleInstance_Mutex_v1";

        private System.Windows.Forms.NotifyIcon? _notifyIcon;



        protected override void OnStartup(System.Windows.StartupEventArgs e)
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
                TryActivateExistingInstance();
                System.Windows.MessageBox.Show("Ứng dụng đang chạy.", "Thông báo",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                Shutdown();
                return;
            }

            base.OnStartup(e);

            // Tạo system tray sau khi base.OnStartup đã load StartupUri/MainWindow
            SetupSystemTray();
        }

        // Nếu App.xaml có Startup="OnStartup" — giữ overload này để XAML binding không lỗi.
        public void OnStartup(object? sender, System.Windows.StartupEventArgs e)
        {
            // forward tới override
            OnStartup(e);
        }

        // --- CHỈ CÓ 1 SetupSystemTray() ---
        private void SetupSystemTray()
        {
            _notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                // đảm bảo icon.ico được copy vào output (or use a pack uri)
                Icon = new System.Drawing.Icon("icon.ico"),
                Visible = true,
                Text = "Now Playing Popup"
            };

            var contextMenu = new System.Windows.Forms.ContextMenuStrip();

            // Mở Settings — gọi vào UI thread WPF bằng Dispatcher
            contextMenu.Items.Add("Mở Settings", null, (s, ev) =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;
                    mainWindow?.OpenSettings();
                });
            });

            contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            // Thoát — cũng chạy trên UI thread để shutdown an toàn
            contextMenu.Items.Add("Thoát", null, (s, ev) =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    System.Windows.Application.Current.Shutdown();
                });
            });

            _notifyIcon.ContextMenuStrip = contextMenu;

            // Double click để show/hide window — phải Invoke về UI thread
            _notifyIcon.DoubleClick += (s, ev) =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var mainWindow = System.Windows.Application.Current.MainWindow;
                    if (mainWindow != null)
                    {
                        if (mainWindow.Visibility == System.Windows.Visibility.Visible)
                        {
                            mainWindow.Hide();
                        }
                        else
                        {
                            mainWindow.Show();
                            mainWindow.Activate();
                        }
                    }
                });
            };
        }

        protected override void OnExit(System.Windows.ExitEventArgs e)
        {
            try
            {
                _notifyIcon?.Dispose();
                _notifyIcon = null;
            }
            catch { }

            try
            {
                if (_singleInstanceMutex != null)
                {
                    _singleInstanceMutex.ReleaseMutex();
                    _singleInstanceMutex.Dispose();
                    _singleInstanceMutex = null;
                }
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
                    catch { }

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
            catch { }
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
                ShowWindow(hWnd, SW_RESTORE);
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
            catch { }
        }

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
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            return found;
        }

        #endregion
    }
}
