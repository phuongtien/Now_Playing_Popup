using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using NowPlayingPopup.Models;
using NowPlayingPopup.Services;
using WpfProgressBar = System.Windows.Controls.ProgressBar;
using WpfMessageBox = System.Windows.MessageBox;
using WpfApp = System.Windows.Application;

namespace NowPlayingPopup
{
    public partial class MainWindow : Window
    {
        // Services
        private MediaSessionManager? _mediaSessionManager;
        private MediaDataProcessor? _mediaDataProcessor;
        private VolumeMonitor? _volumeMonitor;
        private WebViewMessenger? _webViewMessenger;
        private MediaOrchestrator? _mediaOrchestrator;
        private SettingsManager? _settingsManager;
        private WindowManager? _windowManager;
        private TrayIconManager? _trayIconManager;
        private SettingsHttpServer? _httpServer;
        private YouTubeMediaHandler? _youTubeHandler;
        private UpdateService? _updateService;

        // Constants
        private const string HOST_NAME = "appassets";

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
            StateChanged += MainWindow_StateChanged;
            LocationChanged += MainWindow_LocationChanged;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await InitializeServicesAsync();
                await InitializeApplicationAsync();

                // Start HTTP server for settings
                _httpServer = new SettingsHttpServer(_settingsManager!, port: 5005);
                _httpServer.Start();

                // Check for updates
                _ = Task.Run(async () => await _updateService!.CheckForUpdatesOnStartupAsync());
            }
            catch (Exception ex)
            {
                await HandleInitializationErrorAsync(ex);
            }
        }

        private async Task InitializeServicesAsync()
        {
            // Initialize settings first
            _settingsManager = new SettingsManager();
            _settingsManager.Load();
            _settingsManager.SettingsChanged += OnSettingsChanged;

            // Initialize window manager
            _windowManager = new WindowManager(this);

            // Initialize tray icon
            _trayIconManager = new TrayIconManager(this);
            _trayIconManager.ShowRequested += (s, e) =>
            {
                _windowManager.RestoreFromTray();
                // Temporarily show in taskbar
                this.ShowInTaskbar = true;
                _ = Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    Dispatcher.Invoke(() => this.ShowInTaskbar = false);
                });
            };
            _trayIconManager.ExitRequested += (s, e) => WpfApp.Current.Shutdown();

            _trayIconManager.Initialize();

            // Initialize media services
            _mediaSessionManager = new MediaSessionManager();
            _mediaDataProcessor = new MediaDataProcessor();
            _volumeMonitor = new VolumeMonitor();

            // Initialize update service
            _updateService = new UpdateService(this);
        }

        private async Task InitializeApplicationAsync()
        {
            if (!await InitializeWebView2Async()) return;
            if (!SetupVirtualHostMapping()) return;

            ConfigureWebView2Settings();

            // Initialize messenger after WebView2 is ready
            _webViewMessenger = new WebViewMessenger(webView);
            _webViewMessenger.AttachMessageHandler();
            _webViewMessenger.Ready += async (s, e) => await _mediaOrchestrator!.PushCurrentSessionAsync();
            _webViewMessenger.SettingsReceived += OnWebViewSettingsReceived;
            _webViewMessenger.ApplyPositionRequested += OnApplyPositionRequested;

            webView.Source = new Uri($"https://{HOST_NAME}/index.html");
            _windowManager!.ApplySettings(_settingsManager!.CurrentSettings);

            // Initialize media orchestration
            if (await _mediaSessionManager!.InitializeAsync())
            {
                _volumeMonitor!.Initialize();

                _mediaOrchestrator = new MediaOrchestrator(
                    _mediaSessionManager,
                    _mediaDataProcessor!,
                    _volumeMonitor,
                    _webViewMessenger);
            }

            // Initialize YouTube handler
            _youTubeHandler = new YouTubeMediaHandler(webView.CoreWebView2, _webViewMessenger);

            // Start YouTube polling in background
            _ = Task.Run(async () =>
            {
                while (!IsDisposed)
                {
                    await Task.Delay(2000);

                    // Fallback to YouTube if no native media session
                    if (_mediaSessionManager?.GetCurrentSession() == null)
                    {
                        await _youTubeHandler.PollYouTubeAsync();
                    }
                }
            });
        }

        private bool IsDisposed { get; set; }

        private async Task<bool> InitializeWebView2Async()
        {
            try
            {
                string userDataFolder = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "webview2_user_data");

                var options = new CoreWebView2EnvironmentOptions(
                    additionalBrowserArguments: "--disable-gpu --disable-features=VizDisplayCompositor");

                CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(
                    browserExecutableFolder: null,
                    userDataFolder: userDataFolder,
                    options: options);

                await webView.EnsureCoreWebView2Async(env);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WebView2 initialization failed: {ex}");
                return false;
            }
        }

        private bool SetupVirtualHostMapping()
        {
            var wwwrootPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "wwwroot");

            if (!System.IO.Directory.Exists(wwwrootPath))
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
                var settings = webView.CoreWebView2.Settings;
                settings.AreDevToolsEnabled = false;
                settings.IsStatusBarEnabled = false;
                settings.AreDefaultContextMenusEnabled = false;
                settings.AreBrowserAcceleratorKeysEnabled = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WebView2 settings configuration failed: {ex}");
            }
        }

        private void OnSettingsChanged(object? sender, WidgetSettings settings)
        {
            _windowManager?.ApplySettings(settings);
            _webViewMessenger?.SendSettings(settings);
        }

        private void OnWebViewSettingsReceived(object? sender, WidgetSettings settings)
        {
            try
            {
                _settingsManager?.Update(settings);
            }
            catch (ValidationException ex)
            {
                Debug.WriteLine($"Settings validation failed: {ex.Message}");
                WpfMessageBox.Show($"Invalid settings:\n{ex.Message}", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnApplyPositionRequested(object? sender, string position)
        {
            Dispatcher.Invoke(() =>
            {
                _settingsManager?.UpdatePopupPosition(position);
                _windowManager?.ApplyPopupPosition(position);
            });
        }

        private void MainWindow_LocationChanged(object? sender, EventArgs e)
        {
            if (_settingsManager?.CurrentSettings.RememberPosition == true)
            {
                _settingsManager.UpdatePosition(this.Left, this.Top);
                _ = Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    _settingsManager?.Save();
                });
            }
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Minimized)
            {
                _windowManager?.MinimizeToTray();
            }
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            IsDisposed = true;
            CleanupServices();
        }

        private void CleanupServices()
        {
            _httpServer?.Dispose();
            _mediaOrchestrator?.Dispose();
            _mediaSessionManager?.Dispose();
            _volumeMonitor?.Dispose();
            _webViewMessenger?.Dispose();
            _trayIconManager?.Dispose();
            _youTubeHandler?.Dispose();
        }

        private async Task HandleInitializationErrorAsync(Exception ex)
        {
            WpfMessageBox.Show($"Application initialization failed: {ex.Message}");
            Debug.WriteLine($"Initialization error: {ex}");
        }

        // Public API for backward compatibility (if needed)
        public WidgetSettings GetCurrentSettings() =>
            _settingsManager?.CurrentSettings ?? new WidgetSettings();

        public void UpdateSettingsFromWeb(WidgetSettings newSettings)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    _settingsManager?.Update(newSettings);
                }
                catch (ValidationException ex)
                {
                    Debug.WriteLine($"Settings update validation failed: {ex.Message}");
                }
            });
        }

        public void OpenSettings()
        {
            try
            {
                int port = _httpServer?.Port ?? 5005;
                var url = $"http://localhost:{port}/settings.html";
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OpenSettings error: {ex}");
                WpfMessageBox.Show($"Failed to open settings: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}