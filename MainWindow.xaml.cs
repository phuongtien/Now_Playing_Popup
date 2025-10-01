// MainWindow.xaml.cs - patched for ambiguous types, moved OpenSettings, _lastPushTime, Timer fixes
using System;
using System.Text.Json;
using System.Windows.Media;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Windows.Media.Control;
using System.IO;
using System.Diagnostics;
using System.Windows.Input;
using System.Runtime.InteropServices;
using System.Text;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Reflection;
using Forms = System.Windows.Forms;
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;
using WpfMessageBoxButton = System.Windows.MessageBoxButton;
using WpfMessageBoxImage = System.Windows.MessageBoxImage;
using WpfMessageBoxResult = System.Windows.MessageBoxResult;
using ThreadingTimer = System.Threading.Timer;
using Icon = System.Drawing.Icon;
using System.Linq;
using System.Threading;

namespace NowPlayingPopup
{
    public partial class MainWindow : Window
    {
        // Media session management
        private GlobalSystemMediaTransportControlsSessionManager? _mediaManager;
        private GlobalSystemMediaTransportControlsSession? _currentSession;

        private SettingsHttpServer? _httpServer;

        // Caching and state management
        private string? _lastTrackKey;
        private string? _cachedAlbumArtDataUrl;
        private readonly SemaphoreSlim _pushLock = new(1, 1);

        // Volume monitoring
        private IAudioEndpointVolume? _audioEndpointVolume;
        private int _lastSentVolumePercent = -1;

        // Performance optimization
        private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false };
        private volatile bool _isDisposing = false;
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        // Constants
        private const string HOST_NAME = "appassets";
        private const int VOLUME_POLL_INTERVAL_MS = 2000;
        private const int PUSH_DEBOUNCE_MS = 100;
        private const bool ACCEPT_ONLY_SPOTIFY = false;

        private WidgetSettings currentSettings = new WidgetSettings();
        private ThreadingTimer? _volumeTimer;
        private string? _lastSentPayloadHash = null;

        private YouTubeMediaHandler? _youTubeHandler;

        private Forms.NotifyIcon? _notifyIcon;

        // Debounce helper
        private DateTime _lastPushTime = DateTime.MinValue;

        public MainWindow()
        {
            InitializeComponent();
            SetupTrayIcon();
            LoadWidgetSettings();
            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            _httpServer?.Stop();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await InitializeApplicationAsync();

                _httpServer = new SettingsHttpServer(this);
                _httpServer.Start();
                _ = Task.Run(async () => await CheckForUpdatesOnStartup());
            }
            catch (Exception ex)
            {
                await HandleInitializationErrorAsync(ex);
            }
        }

        private async Task CheckForUpdatesOnStartup()
        {
            try
            {
                const string MANIFEST_URL = "https://raw.githubusercontent.com/phuongtien/Now_Playing_Popup_clean/refs/heads/Tien_main/releases/manifest.json";

                var um = new UpdateManager();
                var manifest = await um.GetRemoteManifestAsync(MANIFEST_URL);
                if (manifest == null || string.IsNullOrEmpty(manifest.version) || string.IsNullOrEmpty(manifest.url))
                    return;

                var localVer = UpdateManager.GetLocalVersion();
                if (!UpdateManager.IsNewer(manifest.version, localVer))
                    return;

                // Hỏi user TRƯỚC KHI tải
                var msg = $"Có bản cập nhật {manifest.version}.\n\n{manifest.notes}\n\nBạn có muốn tải và cập nhật bây giờ?";
                var answer = WpfMessageBoxResult.None;

                await WpfApplication.Current.Dispatcher.InvokeAsync(() =>
                {
                    answer = WpfMessageBox.Show(msg, "Cập nhật", WpfMessageBoxButton.YesNo, WpfMessageBoxImage.Information);
                });

                if (answer != WpfMessageBoxResult.Yes)
                    return;

                // Tạo và hiện ProgressWindow
                ProgressWindow? progressWindow = null;
                await WpfApplication.Current.Dispatcher.InvokeAsync(() =>
                {
                    progressWindow = new ProgressWindow
                    {
                        Owner = this,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner
                    };
                    progressWindow.Show();
                });

                var tmpFile = Path.Combine(Path.GetTempPath(), $"NowPlayingPopup_Update_{Guid.NewGuid():N}.exe");

                try
                {
                    // Download với progress
                    using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
                    http.DefaultRequestHeaders.Add("User-Agent", "NowPlayingPopup-Updater");

                    using var response = await http.GetAsync(manifest.url!, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    var total = response.Content.Headers.ContentLength ?? -1L;
                    var canReportProgress = total > 0;

                    using var stream = await response.Content.ReadAsStreamAsync();
                    // share=none: lock when writing; acceptable since we own the file
                    using var fs = new FileStream(tmpFile, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                    var buffer = new byte[8192];
                    long totalRead = 0;
                    int bytesRead;

                    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fs.WriteAsync(buffer, 0, bytesRead);
                        totalRead += bytesRead;

                        if (canReportProgress && progressWindow != null)
                        {
                            int percent = (int)((double)totalRead / total * 100);
                            await WpfApplication.Current.Dispatcher.InvokeAsync(() =>
                            {
                                progressWindow.SetProgress(percent);
                            });
                        }
                    }

                    await fs.FlushAsync();

                    // Đóng progress window
                    await WpfApplication.Current.Dispatcher.InvokeAsync(() =>
                    {
                        progressWindow?.Close();
                        progressWindow = null;
                    });

                    // Verify checksum
                    if (!string.IsNullOrWhiteSpace(manifest.sha256))
                    {
                        var computedHash = UpdateManager.ComputeSha256(tmpFile);
                        if (!string.Equals(computedHash, manifest.sha256.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            try { File.Delete(tmpFile); } catch { }
                            await WpfApplication.Current.Dispatcher.InvokeAsync(() =>
                                WpfMessageBox.Show("File tải về không khớp checksum. Cập nhật bị hủy.",
                                              "Lỗi", WpfMessageBoxButton.OK, WpfMessageBoxImage.Error)
                            );
                            return;
                        }
                    }

                    // Unblock file để Windows không chặn
                    UnblockFile(tmpFile);

                    // Verify file có thể thực thi
                    var fileInfo = new FileInfo(tmpFile);
                    if (!fileInfo.Exists || fileInfo.Length == 0)
                    {
                        await WpfApplication.Current.Dispatcher.InvokeAsync(() =>
                            WpfMessageBox.Show("File tải về không hợp lệ.", "Lỗi",
                                          WpfMessageBoxButton.OK, WpfMessageBoxImage.Error)
                        );
                        return;
                    }

                    // Tạo batch script để chạy installer SAU KHI app đóng
                    var batchFile = Path.Combine(Path.GetTempPath(), $"RunInstaller_{Guid.NewGuid():N}.bat");
                    var appPath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                    var appProcessName = Path.GetFileNameWithoutExtension(appPath);

                    // Batch script: có log, chờ lâu hơn (30s), dùng PowerShell Start-Process -Verb runAs để nâng quyền installer
                    var batchContent = $@"@echo off
setlocal
set LOGFILE=%TEMP%\NowPlayingPopup_Update_log_{Guid.NewGuid():N}.txt
echo %DATE% %TIME% - Batch started > ""%LOGFILE%""
echo Temp file: ""{tmpFile}"" >> ""%LOGFILE%""
echo App process name: {appProcessName}.exe >> ""%LOGFILE%""
echo Waiting for app to close... >> ""%LOGFILE%""

timeout /t 2 /nobreak >nul

set COUNTER=0
:WAIT_LOOP
tasklist /FI ""IMAGENAME eq {appProcessName}.exe"" 2>NUL | find /I /N ""{appProcessName}.exe"" >NUL
if ""%ERRORLEVEL%""==""0"" (
    set /a COUNTER+=1
    echo Still running: %COUNTER% >> ""%LOGFILE%""
    if %COUNTER% GEQ 30 (
        echo Force closing app... >> ""%LOGFILE%""
        taskkill /F /IM ""{appProcessName}.exe"" >NUL 2>&1
        timeout /t 2 /nobreak >nul
        goto START_INSTALLER
    )
    timeout /t 1 /nobreak >nul
    goto WAIT_LOOP
)

:START_INSTALLER
echo Starting installer... >> ""%LOGFILE%""
echo Launching installer elevated (Start-Process -Verb runAs) >> ""%LOGFILE%""
powershell -NoProfile -Command ""Start-Process -FilePath '{tmpFile}' -Verb runAs"" >> ""%LOGFILE%"" 2>&1
echo Installer launched. %DATE% %TIME% >> ""%LOGFILE%""
echo Leaving temp files for inspection >> ""%LOGFILE%""
timeout /t 3 /nobreak >nul
endlocal
exit /b 0
";

                    File.WriteAllText(batchFile, batchContent, System.Text.Encoding.UTF8);

                    // Chạy batch script: USESHELLEXECUTE = true để Windows xử lý properly (hiện UAC khi cần)
                    bool batchStarted = false;
                    try
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = batchFile,
                            UseShellExecute = true,
                            WorkingDirectory = Path.GetTempPath()
                        };

                        var process = Process.Start(psi);

                        if (process != null)
                        {
                            batchStarted = true;

                            // Force cleanup để app đóng nhanh hơn
                            await WpfApplication.Current.Dispatcher.InvokeAsync(() =>
                            {
                                ForceCleanupBeforeUpdate();

                                // Shutdown ngay
                                WpfApplication.Current.Shutdown();

                                // Force exit sau 2 giây nếu vẫn chưa tắt
                                _ = Task.Run(async () =>
                                {
                                    await Task.Delay(2000);
                                    Environment.Exit(0);
                                });
                            });
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to start batch: {ex.Message}");
                    }

                    // Nếu batch không chạy được, thử cách khác
                    if (!batchStarted)
                    {
                        await WpfApplication.Current.Dispatcher.InvokeAsync(() =>
                        {
                            WpfMessageBox.Show(
                                "Ứng dụng sẽ đóng.\n\nVui lòng chạy file installer thủ công tại:\n" + tmpFile,
                                "Cập nhật",
                                WpfMessageBoxButton.OK,
                                WpfMessageBoxImage.Information
                            );

                            // Mở Explorer
                            try
                            {
                                Process.Start("explorer.exe", $"/select,\"{tmpFile}\"");
                            }
                            catch { }

                            WpfApplication.Current.Shutdown();
                        });
                        return;
                    }

                }
                catch (Exception downloadEx)
                {
                    // Đóng progress window nếu có lỗi
                    if (progressWindow != null)
                    {
                        await WpfApplication.Current.Dispatcher.InvokeAsync(() =>
                        {
                            progressWindow?.Close();
                        });
                    }

                    await WpfApplication.Current.Dispatcher.InvokeAsync(() =>
                        WpfMessageBox.Show($"Lỗi khi tải file:\n{downloadEx.Message}",
                                      "Lỗi", WpfMessageBoxButton.OK, WpfMessageBoxImage.Error)
                    );

                    // Dọn dẹp
                    try { if (File.Exists(tmpFile)) File.Delete(tmpFile); } catch { }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Update check failed: " + ex.Message);
            }
        }

        // Hàm unblock file để Windows không chặn
        private static void UnblockFile(string filePath)
        {
            try
            {
                // Xóa Zone.Identifier stream
                var zoneIdentifier = filePath + ":Zone.Identifier";
                try { if (File.Exists(zoneIdentifier)) File.Delete(zoneIdentifier); } catch { }

                // Dùng PowerShell Unblock-File (cách an toàn nhất)
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -Command \"Unblock-File -Path '{filePath}'\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    var process = Process.Start(psi);
                    process?.WaitForExit(5000);
                }
                catch { }

                // Đặt attributes bình thường
                try { File.SetAttributes(filePath, FileAttributes.Normal); } catch { }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UnblockFile failed: {ex.Message}");
            }
        }
        // Cleanup nhanh trước khi shutdown
        private void ForceCleanupBeforeUpdate()
        {
            try
            {
                _isDisposing = true;

                // Stop cancellation token
                try { _cancellationTokenSource?.Cancel(); } catch { }

                // Stop volume timer
                try { _volumeTimer?.Dispose(); } catch { }

                // Stop HTTP server
                try { _httpServer?.Stop(); } catch { }

                // Unsubscribe media events
                if (_currentSession != null)
                {
                    try { UnsubscribeFromSessionEvents(_currentSession); } catch { }
                }

                // Dispose WebView2
                try
                {
                    if (webView?.CoreWebView2 != null)
                    {
                        webView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
                    }
                    webView?.Dispose();
                }
                catch { }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ForceCleanupBeforeUpdate error: {ex.Message}");
            }
        }

        private async Task InitializeApplicationAsync()
        {
            if (!await InitializeWebView2Async()) return;
            if (!SetupVirtualHostMapping()) return;

            ConfigureWebView2Settings();
            webView.Source = new Uri($"https://{HOST_NAME}/index.html");
            ApplySettingsToWindow();

            StartVolumeMonitor();
            await InitMediaManagerAsync();
            _youTubeHandler = new YouTubeMediaHandler(webView.CoreWebView2, this);
            _ = Task.Run(async () =>
            {
                while (!_isDisposing)
                {
                    await Task.Delay(2000);

                    // Nếu không có session Spotify/ứng dụng khác thì fallback sang YouTube
                    if (_mediaManager == null || _mediaManager.GetCurrentSession() == null)
                    {
                        await _youTubeHandler.PollYouTubeAsync();
                    }
                }
            });
        }

        private void SetupTrayIcon()
        {
            try
            {
                _notifyIcon = new Forms.NotifyIcon();

                // Try load app icon from resources first, fallback to exe icon using robust path detection
                try
                {
                    var res = WpfApplication.GetResourceStream(new Uri("pack://application:,,,/Resources/app.ico"));
                    if (res != null)
                    {
                        using var s = res.Stream;
                        _notifyIcon.Icon = new System.Drawing.Icon(s);
                    }
                    else
                    {
                        // Robust exe path detection (Process.MainModule may be null on single-file publish)
                        string? exePath = null;
                        try { exePath = Process.GetCurrentProcess().MainModule?.FileName; } catch { exePath = null; }

                        if (string.IsNullOrEmpty(exePath))
                        {
                            try
                            {
                                var asm = Assembly.GetEntryAssembly();
                                exePath = asm?.Location;
                            }
                            catch { exePath = null; }
                        }

                        if (string.IsNullOrEmpty(exePath))
                        {
                            try
                            {
                                var asmName = Assembly.GetEntryAssembly()?.GetName().Name;
                                if (!string.IsNullOrEmpty(asmName))
                                    exePath = Path.Combine(AppContext.BaseDirectory, asmName + ".exe");
                            }
                            catch { exePath = null; }
                        }

                        if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                        {
                            _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                        }
                        else
                        {
                            // last resort: create an empty icon to avoid null reference (optional)
                            try
                            {
                                using var bmp = new System.Drawing.Bitmap(16, 16);
                                using var g = System.Drawing.Graphics.FromImage(bmp);
                                g.Clear(System.Drawing.Color.Transparent);
                                _notifyIcon.Icon = System.Drawing.Icon.FromHandle(bmp.GetHicon());
                            }
                            catch { /* ignore */ }
                        }
                    }
                }
                catch
                {
                    // Fallback safe: try to get exe icon (same robust approach)
                    try
                    {
                        string? exePath = null;
                        try { exePath = Process.GetCurrentProcess().MainModule?.FileName; } catch { exePath = null; }
                        if (string.IsNullOrEmpty(exePath))
                        {
                            exePath = Assembly.GetEntryAssembly()?.Location;
                        }
                        if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                        {
                            _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                        }
                    }
                    catch { /* give up silently */ }
                }

                _notifyIcon.Text = "Now Playing Popup";
                _notifyIcon.Visible = true;

                // Context menu
                var menu = new Forms.ContextMenuStrip();
                var showItem = new Forms.ToolStripMenuItem("Open");
                showItem.Click += (s, e) => ShowMainWindowFromTray();
                menu.Items.Add(showItem);

                var exitItem = new Forms.ToolStripMenuItem("Exit");
                exitItem.Click += (s, e) =>
                {
                    try
                    {
                        _notifyIcon.Visible = false;
                        _notifyIcon.Dispose();
                    }
                    catch { }
                    WpfApplication.Current.Shutdown();
                };
                menu.Items.Add(exitItem);

                _notifyIcon.ContextMenuStrip = menu;

                // Double click để mở
                _notifyIcon.DoubleClick += (s, e) => ShowMainWindowFromTray();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SetupTrayIcon failed: " + ex);
            }
        }


        private void ShowMainWindowFromTray()
        {
            WpfApplication.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    // Hiện cửa sổ lên
                    this.Show();
                    this.WindowState = WindowState.Normal;
                    this.Activate();

                    // Nếu muốn hiện taskbar khi mở, bật tạm - tuỳ bạn
                    this.ShowInTaskbar = true;
                    // Và (nếu muốn) reset lại sau 1s:
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(1000);
                        WpfApplication.Current.Dispatcher.Invoke(() => this.ShowInTaskbar = false);
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("ShowMainWindowFromTray failed: " + ex);
                }
            });
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Minimized)
            {
                // Khi minimize -> ẩn, chỉ hiện ở tray
                this.Hide();
                this.ShowInTaskbar = false;
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                    _notifyIcon = null;
                }
            }
            catch { }
        }

        public WidgetSettings GetCurrentSettings() => currentSettings;

        public void UpdateSettingsFromWeb(WidgetSettings newSettings)
        {
            Dispatcher.Invoke(() =>
            {
                currentSettings = newSettings;
                SaveWidgetSettings();
                ApplySettingsToWindow();
            });
        }
        public void OpenSettings()
        {
            try
            {
                int port = 5000; 
                var url = $"http://localhost:{port}/settings.html";
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                LogError("OpenSettings error", ex);
            }
        }


        private async Task<bool> InitializeWebView2Async()
        {
            try
            {
                string userDataFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "webview2_user_data");
                var options = new CoreWebView2EnvironmentOptions(
                    additionalBrowserArguments: "--disable-gpu --disable-features=VizDisplayCompositor"
                );

                CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(
                    browserExecutableFolder: null,
                    userDataFolder: userDataFolder,
                    options: options
                );

                await webView.EnsureCoreWebView2Async(env);
                ConfigureWebView2Settings();
                return true;
            }
            catch (Exception ex)
            {
                LogError("WebView2 initialization failed", ex);
                return false;
            }
        }

        private bool SetupVirtualHostMapping()
        {
            var wwwrootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");

            if (!Directory.Exists(wwwrootPath))
            {
                WpfMessageBox.Show($"wwwroot folder not found at: {wwwrootPath}");
                return false;
            }

            webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                HOST_NAME, wwwrootPath, CoreWebView2HostResourceAccessKind.Allow);
            return true;
        }

        private void ConfigureWebView2Settings()
        {
            try
            {
                webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
                var settings = webView.CoreWebView2.Settings;
                settings.AreDevToolsEnabled = false;
                settings.IsStatusBarEnabled = false;
                settings.AreDefaultContextMenusEnabled = false;
                settings.AreBrowserAcceleratorKeysEnabled = false;
            }
            catch (Exception ex)
            {
                LogError("WebView2 settings configuration failed", ex);
            }
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            if (currentSettings.RememberPosition)
            {
                currentSettings.PositionX = this.Left;
                currentSettings.PositionY = this.Top;
                Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    SaveWidgetSettings();
                });
            }
        }

        private async Task HandleInitializationErrorAsync(Exception ex)
        {
            WpfMessageBox.Show($"Application initialization failed: {ex.Message}");
            await LogErrorToFileAsync("initialization_error.log", ex);
        }

        private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            if (_isDisposing) return;

            try
            {
                string? message = args.TryGetWebMessageAsString();
                if (string.IsNullOrEmpty(message)) return;

                ProcessWebMessage(message);
            }
            catch (Exception ex)
            {
                LogError("WebMessage processing failed", ex);
            }
        }

        private void ProcessWebMessage(string message)
        {
            try
            {
                using var doc = JsonDocument.Parse(message);
                if (doc.RootElement.TryGetProperty("type", out var typeElement))
                {
                    var type = typeElement.GetString();

                    if (type == "ready")
                    {
                        _ = Task.Run(() => PushCurrentSessionAsync(), _cancellationTokenSource.Token);
                        return;
                    }

                    if (type == "widgetSettings")
                    {
                        if (doc.RootElement.TryGetProperty("data", out var dataElement))
                        {
                            var newSettings = dataElement.Deserialize<WidgetSettings>();
                            if (newSettings != null)
                            {
                                currentSettings = newSettings;
                                SaveWidgetSettings();
                                ApplySettingsToWindow();
                            }
                        }
                        return;
                    }

                    if (type == "applyPositionNow")
                    {
                        if (doc.RootElement.TryGetProperty("data", out var positionData) &&
                            positionData.TryGetProperty("popupPosition", out var positionElement))
                        {
                            var position = positionElement.GetString();
                            if (!string.IsNullOrEmpty(position))
                            {
                                currentSettings.PopupPosition = position;
                                Dispatcher.Invoke(() => ApplyPopupPosition());
                                SaveWidgetSettings();
                            }
                        }
                        return;
                    }
                }
            }
            catch (JsonException)
            {
                _ = Task.Run(() => PushCurrentSessionAsync(), _cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                LogError("ProcessWebMessage error", ex);
                _ = Task.Run(() => PushCurrentSessionAsync(), _cancellationTokenSource.Token);
            }
        }

        #region Widget Settings Management

        private string GetSettingsFilePath()
        {
            string settingsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NowPlayingPopup");

            if (!Directory.Exists(settingsDir))
                Directory.CreateDirectory(settingsDir);

            return Path.Combine(settingsDir, "widget_settings.json");
        }

        private void LoadWidgetSettings()
        {
            try
            {
                string settingsPath = GetSettingsFilePath();
                if (File.Exists(settingsPath))
                {
                    string json = File.ReadAllText(settingsPath, Encoding.UTF8);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var loadedSettings = JsonSerializer.Deserialize<WidgetSettings>(json);
                        if (loadedSettings != null)
                        {
                            currentSettings = loadedSettings;
                            var errors = currentSettings.ValidateSettings();
                            if (errors.Length > 0)
                            {
                                currentSettings = new WidgetSettings();
                                SaveWidgetSettings();
                            }
                        }
                        else
                        {
                            currentSettings = new WidgetSettings();
                            SaveWidgetSettings();
                        }
                    }
                    else
                    {
                        currentSettings = new WidgetSettings();
                        SaveWidgetSettings();
                    }
                }
                else
                {
                    currentSettings = new WidgetSettings();
                    SaveWidgetSettings();
                }
            }
            catch (Exception ex)
            {
                LogError("LoadWidgetSettings error", ex);
                currentSettings = new WidgetSettings();
                SaveWidgetSettings();
            }
        }

        private void SaveWidgetSettings()
        {
            try
            {
                string settingsPath = GetSettingsFilePath();
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                string json = JsonSerializer.Serialize(currentSettings, options);
                File.WriteAllText(settingsPath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                LogError("SaveWidgetSettings error", ex);
            }
        }

        private void ApplySettingsToWindow()
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    var (width, height) = currentSettings.GetDimensions();
                    this.Width = width;
                    this.Height = height;

                    if (currentSettings.RememberPosition)
                    {
                        this.Left = currentSettings.PositionX;
                        this.Top = currentSettings.PositionY;
                        EnsureWindowOnScreen();
                    }
                    else
                    {
                        ApplyPopupPosition();
                    }

                    RemoveWPFBorderStyling();
                    SendSettingsToWebView();
                    ApplyWindowProperties();
                });
            }
            catch (Exception ex)
            {
                LogError("ApplySettingsToWindow error", ex);
            }
        }

        private void ApplyPopupPosition()
        {
            var screen = SystemParameters.WorkArea;
            switch (currentSettings.PopupPosition)
            {
                case "top-left":
                    this.Left = screen.Left;
                    this.Top = screen.Top;
                    break;
                case "top-right":
                    this.Left = screen.Right - this.Width;
                    this.Top = screen.Top;
                    break;
                case "bottom-left":
                    this.Left = screen.Left;
                    this.Top = screen.Bottom - this.Height;
                    break;
                case "bottom-right":
                default:
                    this.Left = screen.Right - this.Width;
                    this.Top = screen.Bottom - this.Height;
                    break;
            }
        }

        private void RemoveWPFBorderStyling()
        {
            var border = this.FindName("MainBorder") as System.Windows.Controls.Border;
            if (border == null) return;

            border.Background = new SolidColorBrush(Colors.Transparent);
            border.BorderThickness = new Thickness(0);
            border.BorderBrush = null;
            border.CornerRadius = new CornerRadius(0);
            border.Effect = null;
            border.Padding = new Thickness(0);
            border.Margin = new Thickness(0);
        }

        private void SendSettingsToWebView()
        {
            try
            {
                var settingsMessage = new
                {
                    type = "applySettings",
                    data = new
                    {
                        theme = currentSettings.Theme,
                        playerAppearance = currentSettings.PlayerAppearance,
                        tintColor = currentSettings.TintColor,
                        magicColors = currentSettings.MagicColors,
                        coverGlow = currentSettings.CoverGlow,
                        opacity = currentSettings.Opacity,
                        cornerRadius = currentSettings.CornerRadius,
                        borderWidth = currentSettings.BorderWidth,
                        borderColor = currentSettings.BorderColor,
                        shadowIntensity = currentSettings.ShadowIntensity,
                        backgroundBlur = currentSettings.BackgroundBlur,
                        customBackgroundColor = currentSettings.CustomBackgroundColor,
                        showAlbumArt = currentSettings.ShowAlbumArt,
                        showArtistName = currentSettings.ShowArtistName,
                        showAlbumName = currentSettings.ShowAlbumName,
                        showTrackTime = currentSettings.ShowTrackTime,
                        showProgressBar = currentSettings.ShowProgressBar,
                        showVolumeBar = currentSettings.ShowVolumeBar,
                        enableVisualizer = currentSettings.EnableVisualizer,
                        fontSize = currentSettings.FontSize,
                        textAlignment = currentSettings.TextAlignment,
                        coverStyle = currentSettings.CoverStyle,
                        enableAnimations = currentSettings.EnableAnimations,
                        animationSpeed = currentSettings.AnimationSpeed,
                        fadeInOut = currentSettings.FadeInOut,
                        rememberPosition = currentSettings.RememberPosition,
                        positionX = currentSettings.PositionX,
                        positionY = currentSettings.PositionY,
                        popupPosition = currentSettings.PopupPosition,
                        alwaysOnTop = currentSettings.AlwaysOnTop
                    }
                };

                var json = JsonSerializer.Serialize(settingsMessage, _jsonOptions);
                webView?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        webView?.CoreWebView2?.PostWebMessageAsString(json);
                    }
                    catch (Exception ex)
                    {
                        LogError("SendSettingsToWebView failed", ex);
                    }
                }));
            }
            catch (Exception ex)
            {
                LogError("SendSettingsToWebView serialization error", ex);
            }
        }

        private void EnsureWindowOnScreen()
        {
            var workingArea = SystemParameters.WorkArea;
            const double tolerance = 10;

            if (this.Left < workingArea.Left - tolerance)
                this.Left = workingArea.Left;
            if (this.Top < workingArea.Top - tolerance)
                this.Top = workingArea.Top;
            if (this.Left + this.Width > workingArea.Right + tolerance)
                this.Left = workingArea.Right - this.Width;
            if (this.Top + this.Height > workingArea.Bottom + tolerance)
                this.Top = workingArea.Bottom - this.Height;
        }

        private void ApplyWindowProperties()
        {
            this.Opacity = Math.Max(0.1, Math.Min(1.0, currentSettings.Opacity));
            this.Topmost = currentSettings.AlwaysOnTop;
        }

        #endregion

        private async Task InitMediaManagerAsync()
        {
            try
            {
                _mediaManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                if (_mediaManager == null) return;

                _mediaManager.CurrentSessionChanged += OnCurrentSessionChanged;
                _mediaManager.SessionsChanged += OnSessionsChanged;
                await PushCurrentSessionAsync();
            }
            catch (Exception ex)
            {
                LogError("InitMediaManagerAsync error", ex);
            }
        }

        private GlobalSystemMediaTransportControlsSession? FindBestSession()
        {
            try
            {
                if (_mediaManager == null) return null;
                var sessions = _mediaManager.GetSessions();
                if (sessions == null || sessions.Count == 0) return null;

                // Prefer Spotify sessions
                foreach (var s in sessions)
                {
                    try
                    {
                        var aumid = s.SourceAppUserModelId;
                        if (!string.IsNullOrEmpty(aumid) &&
                            aumid.IndexOf("spotify", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return s;
                        }
                    }
                    catch { }
                }

                // Prefer playing sessions
                foreach (var s in sessions)
                {
                    try
                    {
                        var pi = s.GetPlaybackInfo();
                        if (pi?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                        {
                            return s;
                        }
                    }
                    catch { }
                }

                // Return first available session
                return sessions.FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        private bool IsSpotifySession(GlobalSystemMediaTransportControlsSession session,
                               GlobalSystemMediaTransportControlsSessionMediaProperties? mediaProps)
        {
            if (!ACCEPT_ONLY_SPOTIFY) return true;

            try
            {
                if (session == null) return false;

                var aumid = session.SourceAppUserModelId;
                if (!string.IsNullOrEmpty(aumid) &&
                    aumid.IndexOf("spotify", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                if (mediaProps != null)
                {
                    var art = (mediaProps.Thumbnail?.ToString() ?? "").ToLower();
                    if (art.Contains("spotify") || art.Contains("scdn"))
                    {
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        private async void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args) =>
            await DebouncedPushCurrentSessionAsync();

        private async void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args) =>
            await DebouncedPushCurrentSessionAsync();

        private async Task DebouncedPushCurrentSessionAsync()
        {
            var now = DateTime.UtcNow;
            if ((now - _lastPushTime).TotalMilliseconds < PUSH_DEBOUNCE_MS)
                return;

            _lastPushTime = now;
            await PushCurrentSessionAsync();
        }

        private async Task PushCurrentSessionAsync()
        {
            if (_isDisposing || !await _pushLock.WaitAsync(100, _cancellationTokenSource.Token))
                return;

            try
            {
                if (_mediaManager == null) return;

                var session = _mediaManager.GetCurrentSession() ?? FindBestSession();
                if (session == null)
                {
                    SendNoPlayingPayload();
                    return;
                }

                await ProcessMediaSessionAsync(session);
            }
            catch (Exception ex)
            {
                LogError("PushCurrentSessionAsync error", ex);
            }
            finally
            {
                _pushLock.Release();
            }
        }

        private void SendNoPlayingPayload()
        {
            var payload = new
            {
                title = "No playing",
                artist = "",
                album = "",
                durationMs = 0L,
                positionMs = 0L,
                lastUpdatedMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                isPlaying = false,
                albumArt = "",
                volumePercent = GetCurrentVolumePercent()
            };

            SendJsonToWeb(payload);
        }

        private async Task ProcessMediaSessionAsync(GlobalSystemMediaTransportControlsSession session)
        {
            if (_currentSession != session)
            {
                UpdateCurrentSession(session);
            }

            var (mediaProps, timeline, playbackInfo) = await GetMediaInfoAsync(session);

            if (ACCEPT_ONLY_SPOTIFY && !IsSpotifySession(session, mediaProps))
            {
                var alt = FindBestSession();
                if (alt != null && alt != session)
                {
                    await ProcessMediaSessionAsync(alt);
                    return;
                }
                SendNoPlayingPayload();
                return;
            }

            var (durationMs, positionMs) = GetTimingInfo(timeline);
            var isPlaying = playbackInfo?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            var trackInfo = BuildTrackInfo(mediaProps);

            await UpdateAlbumArtCacheIfNeededAsync(trackInfo.Key, mediaProps);

            var payload = new
            {
                title = trackInfo.Title,
                artist = trackInfo.Artist,
                album = trackInfo.Album,
                durationMs,
                positionMs,
                lastUpdatedMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                isPlaying,
                albumArt = _cachedAlbumArtDataUrl ?? "",
                volumePercent = GetCurrentVolumePercent()
            };

            SendJsonToWeb(payload);
        }

        private void UpdateCurrentSession(GlobalSystemMediaTransportControlsSession session)
        {
            if (_currentSession != null)
                UnsubscribeFromSessionEvents(_currentSession);

            _currentSession = session;
            SubscribeToSessionEvents(_currentSession);
        }

        private void SubscribeToSessionEvents(GlobalSystemMediaTransportControlsSession session)
        {
            session.MediaPropertiesChanged += OnMediaPropertiesChanged;
            session.PlaybackInfoChanged += OnPlaybackInfoChanged;
            session.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
        }

        private void UnsubscribeFromSessionEvents(GlobalSystemMediaTransportControlsSession session)
        {
            try
            {
                session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                session.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
            }
            catch { }
        }

        private async Task<(GlobalSystemMediaTransportControlsSessionMediaProperties?,
                          GlobalSystemMediaTransportControlsSessionTimelineProperties?,
                          GlobalSystemMediaTransportControlsSessionPlaybackInfo?)> GetMediaInfoAsync(
            GlobalSystemMediaTransportControlsSession session)
        {
            var mediaProps = await session.TryGetMediaPropertiesAsync();
            var timeline = session.GetTimelineProperties();
            var playbackInfo = session.GetPlaybackInfo();
            return (mediaProps, timeline, playbackInfo);
        }

        private static (long DurationMs, long PositionMs) GetTimingInfo(
            GlobalSystemMediaTransportControlsSessionTimelineProperties? timeline)
        {
            if (timeline == null) return (0L, 0L);

            try
            {
                var positionMs = (long)timeline.Position.TotalMilliseconds;
                var durationMs = (long)timeline.EndTime.TotalMilliseconds;
                return (durationMs, positionMs);
            }
            catch
            {
                return (0L, 0L);
            }
        }

        private static (string Title, string Artist, string Album, string Key) BuildTrackInfo(
            GlobalSystemMediaTransportControlsSessionMediaProperties? mediaProps)
        {
            var title = mediaProps?.Title ?? "";
            var artist = mediaProps?.Artist ?? "";
            var album = mediaProps?.AlbumTitle ?? "";
            var key = $"{title}|{artist}|{album}";
            return (title, artist, album, key);
        }

        private async Task UpdateAlbumArtCacheIfNeededAsync(string trackKey,
            GlobalSystemMediaTransportControlsSessionMediaProperties? mediaProps)
        {
            if (trackKey == _lastTrackKey) return;

            _lastTrackKey = trackKey;
            _cachedAlbumArtDataUrl = await TryGetThumbnailAsDataUrlAsync(mediaProps);
        }

        private static async Task<string?> TryGetThumbnailAsDataUrlAsync(
            GlobalSystemMediaTransportControlsSessionMediaProperties? mediaProps)
        {
            try
            {
                if (mediaProps?.Thumbnail == null) return null;

                using var ras = await mediaProps.Thumbnail.OpenReadAsync();
                using var netStream = ras.AsStreamForRead();
                using var ms = new MemoryStream();
                await netStream.CopyToAsync(ms);
                var bytes = ms.ToArray();

                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.StreamSource = new MemoryStream(bytes);
                bitmap.EndInit();
                bitmap.Freeze();

                const int MAX_DIM = 300;
                double scale = Math.Min(1.0, (double)MAX_DIM / Math.Max(bitmap.PixelWidth, bitmap.PixelHeight));

                var tb = new System.Windows.Media.Imaging.TransformedBitmap(bitmap,
                    new System.Windows.Media.ScaleTransform(scale, scale));
                tb.Freeze();

                var encoder = new System.Windows.Media.Imaging.JpegBitmapEncoder();
                encoder.QualityLevel = 75;
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(tb));

                using var outMs = new MemoryStream();
                encoder.Save(outMs);
                var outBytes = outMs.ToArray();
                var base64 = Convert.ToBase64String(outBytes);

                return $"data:image/jpeg;base64,{base64}";
            }
            catch
            {
                return null;
            }
        }

        public void SendJsonToWeb(object payload)
        {
            if (_isDisposing) return;

            try
            {
                var json = JsonSerializer.Serialize(payload, _jsonOptions);
                if (json == _lastSentPayloadHash) return;
                _lastSentPayloadHash = json;

                webView?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        webView?.CoreWebView2?.PostWebMessageAsString(json);
                    }
                    catch (Exception ex)
                    {
                        LogError("Post to webview error", ex);
                    }
                }));
            }
            catch (Exception ex)
            {
                LogError("SendJsonToWeb error", ex);
            }
        }

        // Overload mới - dùng cho JsonElement (YouTube)
        public void SendJsonToWeb(JsonElement payload)
        {
            if (_isDisposing) return;

            try
            {
                var json = payload.GetRawText();
                if (json == _lastSentPayloadHash) return;
                _lastSentPayloadHash = json;

                webView?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        webView?.CoreWebView2?.PostWebMessageAsString(json);
                    }
                    catch (Exception ex)
                    {
                        LogError("Post to webview error", ex);
                    }
                }));
            }
            catch (Exception ex)
            {
                LogError("SendJsonToWeb (JsonElement) error", ex);
            }
        }


        // Event handlers
        private async void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args) => await DebouncedPushCurrentSessionAsync();
        private async void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args) => await DebouncedPushCurrentSessionAsync();
        private async void OnTimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args) => await DebouncedPushCurrentSessionAsync();

        #region Volume Management

        private void StartVolumeMonitor()
        {
            try
            {
                if (!InitializeAudioEndpoint()) return;
                // use explicit ThreadingTimer to avoid ambiguous Timer
                _volumeTimer = new ThreadingTimer(OnVolumeTimerElapsed, null, 0, VOLUME_POLL_INTERVAL_MS);
            }
            catch (Exception ex)
            {
                LogError("StartVolumeMonitor error", ex);
            }
        }

        private bool InitializeAudioEndpoint()
        {
            try
            {
                var devEnum = new MMDeviceEnumeratorComObject() as IMMDeviceEnumerator;
                if (devEnum == null) return false;

                int hr = devEnum.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out IMMDevice device);
                if (hr != 0 || device == null) return false;

                var iid = typeof(IAudioEndpointVolume).GUID;
                const int CLSCTX_ALL = 23;
                device.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out object volumeObj);

                _audioEndpointVolume = volumeObj as IAudioEndpointVolume;
                return _audioEndpointVolume != null;
            }
            catch
            {
                return false;
            }
        }

        private void OnVolumeTimerElapsed(object? state)
        {
            if (_isDisposing || _audioEndpointVolume == null) return;

            try
            {
                int hr = _audioEndpointVolume.GetMasterVolumeLevelScalar(out float level);
                if (hr != 0) return;

                var volumePercent = (int)Math.Round(level * 100);
                if (volumePercent != _lastSentVolumePercent)
                {
                    _lastSentVolumePercent = volumePercent;
                    var payload = new { volumePercent };
                    SendJsonToWeb(payload);
                }
            }
            catch (Exception ex)
            {
                LogError("Volume poll error", ex);
            }
        }

        private int GetCurrentVolumePercent()
        {
            try
            {
                if (_audioEndpointVolume == null) return -1;
                int hr = _audioEndpointVolume.GetMasterVolumeLevelScalar(out float level);
                return hr == 0 ? (int)Math.Round(level * 100) : -1;
            }
            catch
            {
                return -1;
            }
        }

        #endregion

        #region Minimal Logging

        private static void LogError(string message, Exception ex)
        {
            Debug.WriteLine($"{message}: {ex}");
        }

        private static async Task LogErrorToFileAsync(string fileName, Exception ex)
        {
            try
            {
                var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
                var logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} -> {ex}{Environment.NewLine}";
                await File.AppendAllTextAsync(logPath, logEntry);
            }
            catch { }
        }

        #endregion

        #region COM Interfaces

        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumeratorComObject { }

        private enum EDataFlow { eRender = 0, eCapture = 1, eAll = 2 }
        private enum ERole { eConsole = 0, eMultimedia = 1, eCommunications = 2 }

        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            int NotImpl1();
            int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppDevice);
        }

        [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            int Activate([In] ref Guid iid, int dwClsCtx, IntPtr pActivationParams,
                        [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        }

        [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            int RegisterControlChangeNotify(IntPtr pNotify);
            int UnregisterControlChangeNotify(IntPtr pNotify);
            int GetChannelCount(out uint pnChannelCount);
            int SetMasterVolumeLevel(float fLevelDB, Guid pguidEventContext);
            int SetMasterVolumeLevelScalar(float fLevel, Guid pguidEventContext);
            int GetMasterVolumeLevel(out float pfLevelDB);
            int GetMasterVolumeLevelScalar(out float pfLevel);
        }

        #endregion
    }

    public class UpdateManifest
    {
        public string? name { get; set; }
        public string? version { get; set; }
        public string? notes { get; set; }
        public string? url { get; set; }
        public string? sha256 { get; set; }
        public string? publishedAt { get; set; }
    }

    public class UpdateManager
    {
        private readonly HttpClient _http;
        public UpdateManager()
        {
            _http = new HttpClient() { Timeout = TimeSpan.FromSeconds(30) };
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("NowPlayingPopup-Updater/1.0");
        }

        public async Task<UpdateManifest?> GetRemoteManifestAsync(string manifestUrl)
        {
            try
            {
                var s = await _http.GetStringAsync(manifestUrl);
                return JsonSerializer.Deserialize<UpdateManifest>(s, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                return null;
            }
        }

        public static Version? GetLocalVersion()
        {
            try
            {
                return Assembly.GetEntryAssembly()?.GetName().Version;
            }
            catch { return null; }
        }

        public static bool IsNewer(string remoteVersion, Version? local)
        {
            if (local == null) return true;
            if (!Version.TryParse(remoteVersion, out var rv)) return false;
            return rv > local;
        }

        public async Task<string?> DownloadFileAsync(string url, IProgress<double>? progress = null, CancellationToken ct = default)
        {
            var tmp = Path.Combine(Path.GetTempPath(), Path.GetFileName(new Uri(url).LocalPath));
            try
            {
                using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                resp.EnsureSuccessStatusCode();
                var total = resp.Content.Headers.ContentLength ?? -1L;
                using var stream = await resp.Content.ReadAsStreamAsync(ct);
                using var fs = File.Create(tmp);
                var buffer = new byte[81920];
                long read = 0;
                int r;
                while ((r = await stream.ReadAsync(buffer, ct)) > 0)
                {
                    await fs.WriteAsync(buffer.AsMemory(0, r), ct);
                    read += r;
                    if (total > 0 && progress != null) progress.Report((double)read / total * 100.0);
                }
                return tmp;
            }
            catch
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                return null;
            }
        }

        public static string ComputeSha256(string filePath)
        {
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(filePath);
            var hash = sha.ComputeHash(fs);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        public static bool VerifySha256(string filePath, string expectedHash)
        {
            if (string.IsNullOrWhiteSpace(expectedHash)) return true;
            var got = ComputeSha256(filePath);
            return string.Equals(got, expectedHash.Trim().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
