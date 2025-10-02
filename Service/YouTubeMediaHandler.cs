using System;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace NowPlayingPopup.Services
{
    /// <summary>
    /// Handles YouTube media extraction and monitoring via JavaScript injection
    /// </summary>
    public class YouTubeMediaHandler : IDisposable
    {
        private readonly CoreWebView2 _webView;
        private readonly WebViewMessenger _messenger;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private Task? _pollingTask;
        private bool _isDisposed;

        private const int POLL_INTERVAL_MS = 2000;
        private const string YOUTUBE_DOMAIN = "youtube.com";

        public bool IsPolling { get; private set; }

        public event EventHandler<YouTubeMediaEventArgs>? MediaDetected;
        public event EventHandler? MediaCleared;

        public YouTubeMediaHandler(CoreWebView2 webView, WebViewMessenger messenger)
        {
            _webView = webView ?? throw new ArgumentNullException(nameof(webView));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            _cancellationTokenSource = new CancellationTokenSource();

            AttachEventHandlers();
        }

        private void AttachEventHandlers()
        {
            _webView.WebMessageReceived += OnWebMessageReceived;
        }

        private void DetachEventHandlers()
        {
            _webView.WebMessageReceived -= OnWebMessageReceived;
        }

        /// <summary>
        /// Starts polling YouTube for media information
        /// </summary>
        public void StartPollingAsync()
        {
            if (IsPolling)
            {
                Debug.WriteLine("[YouTubeMediaHandler] Already polling");
                return;
            }

            IsPolling = true;
            _pollingTask = Task.Run(() => PollLoopAsync(_cancellationTokenSource.Token));
            Debug.WriteLine("[YouTubeMediaHandler] Started polling");
        }

        /// <summary>
        /// Stops polling YouTube
        /// </summary>
        public void StopPolling()
        {
            if (!IsPolling) return;

            IsPolling = false;
            Debug.WriteLine("[YouTubeMediaHandler] Stopped polling");
        }

        private async Task PollLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && !_isDisposed)
            {
                try
                {
                    await PollYouTubeAsync();
                    await Task.Delay(POLL_INTERVAL_MS, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[YouTubeMediaHandler] Poll error: {ex.Message}");
                    await Task.Delay(POLL_INTERVAL_MS, cancellationToken);
                }
            }
        }

        /// <summary>
        /// Polls YouTube once for current media information
        /// </summary>
        public async Task PollYouTubeAsync()
        {
            if (_webView == null || _isDisposed) return;

            try
            {
                // Check if we're on YouTube
                var currentUrl = await GetCurrentUrlAsync();
                if (!IsYouTubeUrl(currentUrl))
                {
                    return;
                }

                var mediaInfo = await ExtractYouTubeMediaInfoAsync();
                
                if (mediaInfo != null)
                {
                    // Send to WebView
                    _messenger?.SendMessage(mediaInfo);
                    
                    // Raise event
                    MediaDetected?.Invoke(this, new YouTubeMediaEventArgs(mediaInfo));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[YouTubeMediaHandler] PollYouTubeAsync error: {ex.Message}");
            }
        }

        private async Task<string?> GetCurrentUrlAsync()
        {
            try
            {
                var js = "window.location.href";
                var result = await _webView.ExecuteScriptAsync(js);
                return result?.Trim('"');
            }
            catch
            {
                return null;
            }
        }

        private static bool IsYouTubeUrl(string? url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            return url.Contains(YOUTUBE_DOMAIN, StringComparison.OrdinalIgnoreCase);
        }

        private async Task<YouTubeMediaInfo?> ExtractYouTubeMediaInfoAsync()
        {
            const string extractionScript = @"
(function() {
    try {
        const video = document.querySelector('video');
        if (!video) return null;

        // Extract title
        let title = document.title.replace(' - YouTube', '').trim();
        
        // Try to get better title from player
        const ytTitle = document.querySelector('h1.ytd-watch-metadata yt-formatted-string');
        if (ytTitle && ytTitle.textContent) {
            title = ytTitle.textContent.trim();
        }

        // Extract artist/channel
        let artist = '';
        const channelName = document.querySelector('#channel-name a');
        if (channelName && channelName.textContent) {
            artist = channelName.textContent.trim();
        }

        // Extract thumbnail
        let thumbnail = '';
        const metaThumb = document.querySelector('link[rel=""image_src""]');
        if (metaThumb && metaThumb.href) {
            thumbnail = metaThumb.href;
        } else {
            // Fallback: try video ID from URL
            const videoId = new URLSearchParams(window.location.search).get('v');
            if (videoId) {
                thumbnail = `https://i.ytimg.com/vi/${videoId}/hqdefault.jpg`;
            }
        }

        // Get playback state
        const duration = video.duration * 1000 || 0;
        const position = video.currentTime * 1000 || 0;
        const isPlaying = !video.paused && !video.ended;

        return {
            title: title || 'Unknown',
            artist: artist || 'YouTube',
            album: '',
            durationMs: Math.round(duration),
            positionMs: Math.round(position),
            lastUpdatedMs: Date.now(),
            isPlaying: isPlaying,
            albumArt: thumbnail,
            volumePercent: Math.round(video.volume * 100)
        };
    } catch (error) {
        console.error('YouTube media extraction error:', error);
        return null;
    }
})();
";

            try
            {
                var result = await _webView.ExecuteScriptAsync(extractionScript);
                
                if (string.IsNullOrEmpty(result) || result == "null")
                {
                    return null;
                }

                // Clean JSON response (remove escape characters)
                var cleanedJson = CleanJsonResponse(result);
                var mediaInfo = JsonSerializer.Deserialize<YouTubeMediaInfo>(cleanedJson);

                return mediaInfo;
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"[YouTubeMediaHandler] JSON parse error: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[YouTubeMediaHandler] Extraction error: {ex.Message}");
                return null;
            }
        }

        private static string CleanJsonResponse(string json)
        {
            // Remove outer quotes if present
            if (json.StartsWith("\"") && json.EndsWith("\""))
            {
                json = json.Substring(1, json.Length - 2);
            }

            // Unescape JSON
            return json.Replace("\\\"", "\"")
                      .Replace("\\\\", "\\")
                      .Replace("\\n", "\n")
                      .Replace("\\r", "\r")
                      .Replace("\\t", "\t");
        }

        private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            // Handle any custom messages from YouTube page if needed
            try
            {
                var message = e.TryGetWebMessageAsString();
                if (string.IsNullOrEmpty(message)) return;

                Debug.WriteLine($"[YouTubeMediaHandler] Received message: {message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[YouTubeMediaHandler] Message handling error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            StopPolling();
            DetachEventHandlers();
            
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();

            try
            {
                _pollingTask?.Wait(TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[YouTubeMediaHandler] Disposal error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// YouTube media information
    /// </summary>
    public class YouTubeMediaInfo
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("artist")]
        public string Artist { get; set; } = "";

        [JsonPropertyName("album")]
        public string Album { get; set; } = "";

        [JsonPropertyName("durationMs")]
        public long DurationMs { get; set; }

        [JsonPropertyName("positionMs")]
        public long PositionMs { get; set; }

        [JsonPropertyName("lastUpdatedMs")]
        public long LastUpdatedMs { get; set; }

        [JsonPropertyName("isPlaying")]
        public bool IsPlaying { get; set; }

        [JsonPropertyName("albumArt")]
        public string AlbumArt { get; set; } = "";

        [JsonPropertyName("volumePercent")]
        public int VolumePercent { get; set; }
    }

    /// <summary>
    /// Event args for YouTube media detection
    /// </summary>
    public class YouTubeMediaEventArgs : EventArgs
    {
        public YouTubeMediaInfo MediaInfo { get; }

        public YouTubeMediaEventArgs(YouTubeMediaInfo mediaInfo)
        {
            MediaInfo = mediaInfo ?? throw new ArgumentNullException(nameof(mediaInfo));
        }
    }
}