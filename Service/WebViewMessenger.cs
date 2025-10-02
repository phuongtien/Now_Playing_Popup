using System;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using NowPlayingPopup.Models;

namespace NowPlayingPopup.Services
{
    /// <summary>
    /// Handles communication between WPF and WebView2
    /// </summary>
    public class WebViewMessenger : IDisposable
    {
        private readonly WebView2 _webView;
        private readonly JsonSerializerOptions _jsonOptions;
        private string? _lastSentPayloadHash;
        private bool _isDisposed;

        public event EventHandler<WebMessageEventArgs>? MessageReceived;
        public event EventHandler? Ready;
        public event EventHandler<WidgetSettings>? SettingsReceived;
        public event EventHandler<string>? ApplyPositionRequested;

        public WebViewMessenger(WebView2 webView)
        {
            _webView = webView ?? throw new ArgumentNullException(nameof(webView));
            _jsonOptions = new JsonSerializerOptions { WriteIndented = false };
        }

        public void AttachMessageHandler()
        {
            if (_webView.CoreWebView2 != null)
            {
                _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            }
        }

        public void DetachMessageHandler()
        {
            if (_webView.CoreWebView2 != null)
            {
                _webView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
            }
        }

        private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            if (_isDisposed) return;

            try
            {
                string? message = args.TryGetWebMessageAsString();
                if (string.IsNullOrEmpty(message)) return;

                ProcessWebMessage(message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WebMessage processing failed: {ex}");
            }
        }

        private void ProcessWebMessage(string message)
        {
            try
            {
                using var doc = JsonDocument.Parse(message);
                if (!doc.RootElement.TryGetProperty("type", out var typeElement))
                {
                    MessageReceived?.Invoke(this, new WebMessageEventArgs(message));
                    return;
                }

                var type = typeElement.GetString();

                switch (type)
                {
                    case "ready":
                        Ready?.Invoke(this, EventArgs.Empty);
                        break;

                    case "widgetSettings":
                        if (doc.RootElement.TryGetProperty("data", out var dataElement))
                        {
                            var newSettings = dataElement.Deserialize<WidgetSettings>();
                            if (newSettings != null)
                            {
                                SettingsReceived?.Invoke(this, newSettings);
                            }
                        }
                        break;

                    case "applyPositionNow":
                        if (doc.RootElement.TryGetProperty("data", out var positionData) &&
                            positionData.TryGetProperty("popupPosition", out var positionElement))
                        {
                            var position = positionElement.GetString();
                            if (!string.IsNullOrEmpty(position))
                            {
                                ApplyPositionRequested?.Invoke(this, position);
                            }
                        }
                        break;

                    default:
                        MessageReceived?.Invoke(this, new WebMessageEventArgs(message));
                        break;
                }
            }
            catch (JsonException)
            {
                MessageReceived?.Invoke(this, new WebMessageEventArgs(message));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ProcessWebMessage error: {ex}");
                MessageReceived?.Invoke(this, new WebMessageEventArgs(message));
            }
        }

        public void SendMessage(object payload)
        {
            if (_isDisposed) return;

            try
            {
                var json = JsonSerializer.Serialize(payload, _jsonOptions);
                if (json == _lastSentPayloadHash) return;
                _lastSentPayloadHash = json;

                _webView?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        _webView?.CoreWebView2?.PostWebMessageAsString(json);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Post to webview error: {ex}");
                    }
                }));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SendMessage error: {ex}");
            }
        }

        public void SendMessage(JsonElement payload)
        {
            if (_isDisposed) return;

            try
            {
                var json = payload.GetRawText();
                if (json == _lastSentPayloadHash) return;
                _lastSentPayloadHash = json;

                _webView?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        _webView?.CoreWebView2?.PostWebMessageAsString(json);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Post to webview error: {ex}");
                    }
                }));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SendMessage (JsonElement) error: {ex}");
            }
        }

        public void SendSettings(WidgetSettings settings)
        {
            var settingsMessage = new
            {
                type = "applySettings",
                data = new
                {
                    theme = settings.Theme,
                    playerAppearance = settings.PlayerAppearance,
                    tintColor = settings.TintColor,
                    magicColors = settings.MagicColors,
                    coverGlow = settings.CoverGlow,
                    opacity = settings.Opacity,
                    cornerRadius = settings.CornerRadius,
                    borderWidth = settings.BorderWidth,
                    borderColor = settings.BorderColor,
                    shadowIntensity = settings.ShadowIntensity,
                    backgroundBlur = settings.BackgroundBlur,
                    customBackgroundColor = settings.CustomBackgroundColor,
                    showAlbumArt = settings.ShowAlbumArt,
                    showArtistName = settings.ShowArtistName,
                    showAlbumName = settings.ShowAlbumName,
                    showTrackTime = settings.ShowTrackTime,
                    showProgressBar = settings.ShowProgressBar,
                    showVolumeBar = settings.ShowVolumeBar,
                    enableVisualizer = settings.EnableVisualizer,
                    fontSize = settings.FontSize,
                    textAlignment = settings.TextAlignment,
                    coverStyle = settings.CoverStyle,
                    enableAnimations = settings.EnableAnimations,
                    animationSpeed = settings.AnimationSpeed,
                    fadeInOut = settings.FadeInOut,
                    rememberPosition = settings.RememberPosition,
                    positionX = settings.PositionX,
                    positionY = settings.PositionY,
                    popupPosition = settings.PopupPosition,
                    alwaysOnTop = settings.AlwaysOnTop
                }
            };

            SendMessage(settingsMessage);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            DetachMessageHandler();
        }
    }

    public class WebMessageEventArgs : EventArgs
    {
        public string Message { get; }

        public WebMessageEventArgs(string message)
        {
            Message = message;
        }
    }
}