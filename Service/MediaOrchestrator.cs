using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Control;

namespace NowPlayingPopup.Services
{
    /// <summary>
    /// Orchestrates media session monitoring and data updates
    /// </summary>
    public class MediaOrchestrator : IDisposable
    {
        private readonly MediaSessionManager _sessionManager;
        private readonly MediaDataProcessor _dataProcessor;
        private readonly VolumeMonitor _volumeMonitor;
        private readonly WebViewMessenger _messenger;
        private readonly SemaphoreSlim _pushLock = new(1, 1);
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        
        private DateTime _lastPushTime = DateTime.MinValue;
        private bool _isDisposed;
        
        private const int PUSH_DEBOUNCE_MS = 100;
        private const bool ACCEPT_ONLY_SPOTIFY = false;

        public MediaOrchestrator(
            MediaSessionManager sessionManager,
            MediaDataProcessor dataProcessor,
            VolumeMonitor volumeMonitor,
            WebViewMessenger messenger)
        {
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _dataProcessor = dataProcessor ?? throw new ArgumentNullException(nameof(dataProcessor));
            _volumeMonitor = volumeMonitor ?? throw new ArgumentNullException(nameof(volumeMonitor));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));

            AttachEventHandlers();
        }

        private void AttachEventHandlers()
        {
            _sessionManager.SessionChanged += OnSessionChanged;
            _sessionManager.MediaPropertiesChanged += OnMediaPropertiesChanged;
            _sessionManager.PlaybackInfoChanged += OnPlaybackInfoChanged;
            _sessionManager.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
            _volumeMonitor.VolumeChanged += OnVolumeChanged;
            _messenger.Ready += OnWebViewReady;
        }

        private void DetachEventHandlers()
        {
            _sessionManager.SessionChanged -= OnSessionChanged;
            _sessionManager.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            _sessionManager.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            _sessionManager.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
            _volumeMonitor.VolumeChanged -= OnVolumeChanged;
            _messenger.Ready -= OnWebViewReady;
        }

        private async void OnSessionChanged(object? sender, EventArgs e) => 
            await DebouncedPushCurrentSessionAsync();

        private async void OnMediaPropertiesChanged(object? sender, MediaSessionEventArgs e) => 
            await DebouncedPushCurrentSessionAsync();

        private async void OnPlaybackInfoChanged(object? sender, MediaSessionEventArgs e) => 
            await DebouncedPushCurrentSessionAsync();

        private async void OnTimelinePropertiesChanged(object? sender, MediaSessionEventArgs e) => 
            await DebouncedPushCurrentSessionAsync();

        private void OnVolumeChanged(object? sender, VolumeChangedEventArgs e)
        {
            var payload = new { volumePercent = e.VolumePercent };
            _messenger.SendMessage(payload);
        }

        private async void OnWebViewReady(object? sender, EventArgs e) => 
            await PushCurrentSessionAsync();

        private async Task DebouncedPushCurrentSessionAsync()
        {
            var now = DateTime.UtcNow;
            if ((now - _lastPushTime).TotalMilliseconds < PUSH_DEBOUNCE_MS)
                return;

            _lastPushTime = now;
            await PushCurrentSessionAsync();
        }

        public async Task PushCurrentSessionAsync()
        {
            if (_isDisposed || !await _pushLock.WaitAsync(100, _cancellationTokenSource.Token))
                return;

            try
            {
                var session = _sessionManager.GetCurrentSession();
                if (session == null)
                {
                    SendNoPlayingPayload();
                    return;
                }

                await ProcessMediaSessionAsync(session);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PushCurrentSessionAsync error: {ex}");
            }
            finally
            {
                _pushLock.Release();
            }
        }

        private void SendNoPlayingPayload()
        {
            var payload = _dataProcessor.CreateNoPlayingPayload(
                _volumeMonitor.GetCurrentVolumePercent());
            _messenger.SendMessage(payload);
        }

        private async Task ProcessMediaSessionAsync(GlobalSystemMediaTransportControlsSession session)
        {
            await _sessionManager.SetCurrentSessionAsync(session);

            if (ACCEPT_ONLY_SPOTIFY && !IsSpotifySession(session))
            {
                var alt = _sessionManager.FindBestSession();
                if (alt != null && alt != session)
                {
                    await ProcessMediaSessionAsync(alt);
                    return;
                }
                SendNoPlayingPayload();
                return;
            }

            var payload = await _dataProcessor.ProcessSessionAsync(
                session, 
                _volumeMonitor.GetCurrentVolumePercent());
            
            _messenger.SendMessage(payload);
        }

        private static bool IsSpotifySession(GlobalSystemMediaTransportControlsSession session)
        {
            if (!ACCEPT_ONLY_SPOTIFY) return true;

            try
            {
                if (session == null) return false;

                var aumid = session.SourceAppUserModelId;
                return !string.IsNullOrEmpty(aumid) &&
                       aumid.IndexOf("spotify", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            DetachEventHandlers();
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _pushLock?.Dispose();
        }
    }
}