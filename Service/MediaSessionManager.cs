using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Control;

namespace NowPlayingPopup.Services
{
    /// <summary>
    /// Manages Windows Media Session integration and events
    /// </summary>
    public class MediaSessionManager : IDisposable
    {
        private GlobalSystemMediaTransportControlsSessionManager? _mediaManager;
        private GlobalSystemMediaTransportControlsSession? _currentSession;
        private readonly SemaphoreSlim _sessionLock = new(1, 1);
        private bool _isDisposed;

        public event EventHandler? SessionChanged;
        public event EventHandler<MediaSessionEventArgs>? MediaPropertiesChanged;
        public event EventHandler<MediaSessionEventArgs>? PlaybackInfoChanged;
        public event EventHandler<MediaSessionEventArgs>? TimelinePropertiesChanged;

        public async Task<bool> InitializeAsync()
        {
            try
            {
                _mediaManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                if (_mediaManager == null) return false;

                _mediaManager.CurrentSessionChanged += OnCurrentSessionChanged;
                _mediaManager.SessionsChanged += OnSessionsChanged;
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MediaSessionManager initialization failed: {ex}");
                return false;
            }
        }

        public GlobalSystemMediaTransportControlsSession? GetCurrentSession()
        {
            return _mediaManager?.GetCurrentSession() ?? FindBestSession();
        }

        public GlobalSystemMediaTransportControlsSession? FindBestSession()
        {
            try
            {
                if (_mediaManager == null) return null;
                var sessions = _mediaManager.GetSessions();
                if (sessions == null || sessions.Count == 0) return null;

                // Prefer Spotify
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

                return sessions.FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> SetCurrentSessionAsync(GlobalSystemMediaTransportControlsSession? newSession)
        {
            if (!await _sessionLock.WaitAsync(100)) return false;

            try
            {
                if (_currentSession == newSession) return false;

                if (_currentSession != null)
                {
                    UnsubscribeFromSession(_currentSession);
                }

                _currentSession = newSession;

                if (_currentSession != null)
                {
                    SubscribeToSession(_currentSession);
                }

                return true;
            }
            finally
            {
                _sessionLock.Release();
            }
        }

        private void SubscribeToSession(GlobalSystemMediaTransportControlsSession session)
        {
            session.MediaPropertiesChanged += OnMediaPropertiesChangedInternal;
            session.PlaybackInfoChanged += OnPlaybackInfoChangedInternal;
            session.TimelinePropertiesChanged += OnTimelinePropertiesChangedInternal;
        }

        private void UnsubscribeFromSession(GlobalSystemMediaTransportControlsSession session)
        {
            try
            {
                session.MediaPropertiesChanged -= OnMediaPropertiesChangedInternal;
                session.PlaybackInfoChanged -= OnPlaybackInfoChangedInternal;
                session.TimelinePropertiesChanged -= OnTimelinePropertiesChangedInternal;
            }
            catch { }
        }

        private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
        {
            SessionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
        {
            SessionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnMediaPropertiesChangedInternal(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        {
            MediaPropertiesChanged?.Invoke(this, new MediaSessionEventArgs(sender));
        }

        private void OnPlaybackInfoChangedInternal(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        {
            PlaybackInfoChanged?.Invoke(this, new MediaSessionEventArgs(sender));
        }

        private void OnTimelinePropertiesChangedInternal(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
        {
            TimelinePropertiesChanged?.Invoke(this, new MediaSessionEventArgs(sender));
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            if (_currentSession != null)
            {
                UnsubscribeFromSession(_currentSession);
            }

            _sessionLock?.Dispose();
        }
    }

    public class MediaSessionEventArgs : EventArgs
    {
        public GlobalSystemMediaTransportControlsSession Session { get; }

        public MediaSessionEventArgs(GlobalSystemMediaTransportControlsSession session)
        {
            Session = session;
        }
    }
}