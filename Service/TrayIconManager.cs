using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace NowPlayingPopup.Services
{
    /// <summary>
    /// Manages system tray icon and context menu
    /// </summary>
    public class TrayIconManager : IDisposable
    {
        private NotifyIcon? _notifyIcon;
        private readonly Window _mainWindow;
        private bool _isDisposed;

        public event EventHandler? ShowRequested;
        public event EventHandler? ExitRequested;

        public TrayIconManager(Window mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        public void Initialize()
        {
            if (_notifyIcon != null) return;

            try
            {
                _notifyIcon = new NotifyIcon
                {
                    Text = "Now Playing Popup",
                    Visible = true
                };

                LoadIcon();
                CreateContextMenu();
                AttachEventHandlers();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrayIconManager initialization failed: {ex}");
            }
        }

        private void LoadIcon()
        {
            try
            {
                var res = Application.GetResourceStream(new Uri("pack://application:,,,/Resources/app.ico"));
                if (res != null)
                {
                    using var s = res.Stream;
                    _notifyIcon!.Icon = new Icon(s);
                }
                else
                {
                    string? exePath = null;
                    try
                    {
                        exePath = Process.GetCurrentProcess().MainModule?.FileName;
                    }
                    catch
                    {
                        exePath = null;
                    }

                    if (string.IsNullOrEmpty(exePath))
                        exePath = Assembly.GetEntryAssembly()?.Location;

                    if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                        _notifyIcon!.Icon = Icon.ExtractAssociatedIcon(exePath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Icon loading failed: {ex}");
            }
        }

        private void CreateContextMenu()
        {
            var menu = new ContextMenuStrip();

            var showItem = new ToolStripMenuItem("Open");
            showItem.Click += (s, e) => ShowRequested?.Invoke(this, EventArgs.Empty);
            menu.Items.Add(showItem);

            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);
            menu.Items.Add(exitItem);

            _notifyIcon!.ContextMenuStrip = menu;
        }

        private void AttachEventHandlers()
        {
            _notifyIcon!.DoubleClick -= OnDoubleClick;
            _notifyIcon.DoubleClick += OnDoubleClick;
        }

        private void OnDoubleClick(object? sender, EventArgs e)
        {
            ShowRequested?.Invoke(this, EventArgs.Empty);
        }

        public void ShowMainWindow()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    _mainWindow.Show();
                    _mainWindow.WindowState = WindowState.Normal;
                    _mainWindow.Activate();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"ShowMainWindow failed: {ex}");
                }
            });
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            try
            {
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                    _notifyIcon = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrayIconManager disposal failed: {ex}");
            }
        }
    }
}