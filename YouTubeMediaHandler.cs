using Microsoft.Web.WebView2.Core;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace NowPlayingPopup
{
    public class YouTubeMediaHandler
    {
        private readonly CoreWebView2 _webView;
        private readonly MainWindow _mainWindow;

        public YouTubeMediaHandler(CoreWebView2 webView, MainWindow mainWindow)
        {
            _webView = webView;
            _mainWindow = mainWindow;
            _webView.WebMessageReceived += OnWebMessageReceived;
        }

        /// <summary>
        /// Gọi JS trên YouTube player để lấy thông tin bài đang chơi
        /// </summary>
        public async Task PollYouTubeAsync()
        {
            if (_webView == null) return;

            string js = @"
                (function() {
                    const video = document.querySelector('video');
                    if (!video) return null;

                    const title = document.title.replace(' - YouTube','');
                    const artist = ''; // có thể parse từ mô tả nếu muốn
                    const thumbnail = document.querySelector('link[rel=image_src]')?.href || '';
                    const duration = video.duration * 1000 || 0;
                    const position = video.currentTime * 1000 || 0;
                    const isPlaying = !video.paused;

                    return { title, artist, album: '', durationMs: duration, positionMs: position, isPlaying, albumArt: thumbnail };
                })();
            ";

            try
            {
                var result = await _webView.ExecuteScriptAsync(js);
                if (!string.IsNullOrEmpty(result) && result != "null")
                {
                    // JS trả về chuỗi JSON, cần clean trước khi Deserialize
                    var json = result.Replace("\\u0022", "\"");
                    var doc = JsonDocument.Parse(json);
                    var payload = doc.RootElement.Clone();

                    _mainWindow.Dispatcher.Invoke(() =>
                    {
                        _mainWindow.SendJsonToWeb(payload);
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("YouTubeMediaHandler error: " + ex.Message);
            }
        }

        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            // Nếu muốn, có thể xử lý tương tác từ JS YouTube ở đây
        }
    }
}