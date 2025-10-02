using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Media.Control;

namespace NowPlayingPopup.Services
{
    /// <summary>
    /// Processes media data and converts album art to data URLs
    /// </summary>
    public class MediaDataProcessor
    {
        private string? _lastTrackKey;
        private string? _cachedAlbumArtDataUrl;

        public async Task<MediaPayload> ProcessSessionAsync(
            GlobalSystemMediaTransportControlsSession session,
            int currentVolumePercent)
        {
            var (mediaProps, timeline, playbackInfo) = await GetMediaInfoAsync(session);
            var (durationMs, positionMs) = GetTimingInfo(timeline);
            var isPlaying = playbackInfo?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            var trackInfo = BuildTrackInfo(mediaProps);

            await UpdateAlbumArtCacheIfNeededAsync(trackInfo.Key, mediaProps);

            return new MediaPayload
            {
                Title = trackInfo.Title,
                Artist = trackInfo.Artist,
                Album = trackInfo.Album,
                DurationMs = durationMs,
                PositionMs = positionMs,
                LastUpdatedMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                IsPlaying = isPlaying,
                AlbumArt = _cachedAlbumArtDataUrl ?? "",
                VolumePercent = currentVolumePercent
            };
        }

        public MediaPayload CreateNoPlayingPayload(int currentVolumePercent)
        {
            return new MediaPayload
            {
                Title = "No playing",
                Artist = "",
                Album = "",
                DurationMs = 0L,
                PositionMs = 0L,
                LastUpdatedMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                IsPlaying = false,
                AlbumArt = "",
                VolumePercent = currentVolumePercent
            };
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

        public void ClearCache()
        {
            _lastTrackKey = null;
            _cachedAlbumArtDataUrl = null;
        }
    }

    public class MediaPayload
    {
        public string Title { get; set; } = "";
        public string Artist { get; set; } = "";
        public string Album { get; set; } = "";
        public long DurationMs { get; set; }
        public long PositionMs { get; set; }
        public long LastUpdatedMs { get; set; }
        public bool IsPlaying { get; set; }
        public string AlbumArt { get; set; } = "";
        public int VolumePercent { get; set; }
    }
}