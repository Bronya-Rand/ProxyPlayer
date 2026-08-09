using Windows.Media;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace ProxyPlayerServer
{
    /// <summary>
    /// The service responsible for managing media sessions and retrieving their 
    /// properties.
    /// </summary>
    /// <remarks>
    /// This is the proxy backend for ProxyPlayer, needed to communicate SMTC 
    /// sessions and actions to the plugin.
    /// </remarks>
    public sealed class MediaSessionService : IDisposable
    {
        private GlobalSystemMediaTransportControlsSessionManager? sessionManager;
        private GlobalSystemMediaTransportControlsSession? currentSession;

        private bool isInitializing;
        private Windows.Foundation.TypedEventHandler<GlobalSystemMediaTransportControlsSessionManager, SessionsChangedEventArgs>? sessionsChangedHandler;

        // Metadata
        public string Title { get; private set; } = string.Empty;
        public string Artist { get; private set; } = string.Empty;
        public string Album { get; private set; } = string.Empty;
        public byte[]? CoverArtBytes { get; private set; } // Raw stream of the cover art image

        // Playback state
        public double PositionSeconds { get; private set; }
        public double DurationSeconds { get; private set; }
        public DateTimeOffset PositionLastUpdatedUtc { get; private set; } = DateTimeOffset.UtcNow;
        public GlobalSystemMediaTransportControlsSessionPlaybackStatus PlaybackStatus { get; private set; }

        // Capabilities
        public bool SupportsShuffle { get; private set; }
        public bool SupportsRepeat { get; private set; }
        public bool SupportsStop { get; private set; }
        public bool? IsShuffleActive { get; private set; }
        public MediaPlaybackAutoRepeatMode? AutoRepeatMode { get; private set; }

        public string? SelectedSessionId { get; private set; }
        public event Action? OnSessionChanged;

        public async Task InitializeAsync()
        {
            if (isInitializing || sessionManager != null) return;
            isInitializing = true;

            try
            {
                sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                sessionsChangedHandler = (_, _) => RefreshActiveSession();
                sessionManager.SessionsChanged += sessionsChangedHandler;
                RefreshActiveSession();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize MediaSessionService: {ex.Message}");
            }
            finally
            {
                isInitializing = false;
            }
        }

        public string[] GetAvailableSessions() =>
            sessionManager?.GetSessions()
                .Select(session => session.SourceAppUserModelId)
                .ToArray() ?? [];

        public string SetSelectedSession(string sessionId)
        {
            SelectedSessionId = sessionId;
            RefreshActiveSession();
            return SelectedSessionId ?? string.Empty;
        }
        private void RefreshActiveSession()
        {
            if (sessionManager == null) return;

            var sessions = sessionManager.GetSessions();

            // If the selected session is no longer available, reset the selection
            if (SelectedSessionId != null && !sessions.Any(s => s.SourceAppUserModelId == SelectedSessionId))
            {
                SelectedSessionId = null;
            }

            // Determine the target session
            var target = SelectedSessionId != null
                ? sessions.FirstOrDefault(s => s.SourceAppUserModelId == SelectedSessionId)
                : sessionManager.GetCurrentSession();

            if (currentSession != target)
            {
                // Unsubscribe from events of the old session
                if (currentSession != null)
                {
                    currentSession.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
                    currentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                    currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                }

                currentSession = target;
                PositionSeconds = 0;
                DurationSeconds = 0;

                if (currentSession != null)
                {
                    currentSession.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
                    currentSession.MediaPropertiesChanged += OnMediaPropertiesChanged;
                    currentSession.PlaybackInfoChanged += OnPlaybackInfoChanged;
                }
            }

            // Refresh properties based on the new session
            if (currentSession != null)
            {
                _ = RefreshMediaPropertiesAsync();
                RefreshPlaybackInfo();
                RefreshTimelineProperties(force: true);
            }
            else
            {
                // Reset properties when no session is active
                Title = string.Empty;
                Artist = string.Empty;
                Album = string.Empty;
                PositionSeconds = 0;
                DurationSeconds = 0;
                PlaybackStatus = GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed;
                CoverArtBytes = null;
                SupportsShuffle = false;
                SupportsRepeat = false;
                SupportsStop = false;
                IsShuffleActive = null;
                AutoRepeatMode = null;
                OnSessionChanged?.Invoke();
            }
        }

        private void OnTimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession session, TimelinePropertiesChangedEventArgs args) =>
            RefreshTimelineProperties();

        private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession session, MediaPropertiesChangedEventArgs args) =>
            _ = RefreshMediaPropertiesAsync();

        private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession session, PlaybackInfoChangedEventArgs args) =>
            RefreshPlaybackInfo();

        private void RefreshTimelineProperties(bool force = false)
        {
            if (currentSession == null) return;
            var timeline = currentSession.GetTimelineProperties();

            var duration = timeline.EndTime.Subtract(timeline.StartTime).TotalSeconds;
            // Ignore transient events that cause duration to become 0 randomly (unless force is true)
            if (!force && duration <= 0 && DurationSeconds > 0) return;

            PositionSeconds = timeline.Position.TotalSeconds;
            DurationSeconds = Math.Max(0, duration);
            PositionLastUpdatedUtc = timeline.LastUpdatedTime != default
                ? timeline.LastUpdatedTime.ToUniversalTime()
                : DateTimeOffset.UtcNow;

            OnSessionChanged?.Invoke();
        }
        private async Task RefreshMediaPropertiesAsync()
        {
            if (currentSession == null) return;

            try
            {
                var properties = await currentSession.TryGetMediaPropertiesAsync();

                Title = properties?.Title ?? string.Empty;
                Artist = properties?.Artist ?? string.Empty;
                Album = properties?.AlbumTitle ?? string.Empty;
                CoverArtBytes = await ReadThumbnailAsync(properties?.Thumbnail);
                OnSessionChanged?.Invoke();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to refresh media properties: {ex.Message}");
            }
        }

        // Helper method to read the thumbnail stream into a byte array
        private static async Task<byte[]> ReadThumbnailAsync(IRandomAccessStreamReference? thumbnail)
        {
            if (thumbnail == null) return [];
            try
            {
                using var stream = await thumbnail.OpenReadAsync();
                using var reader = new DataReader(stream);
                var bytes = new byte[stream.Size];
                await reader.LoadAsync((uint)stream.Size);
                reader.ReadBytes(bytes);
                return bytes;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to read thumbnail: {ex.Message}");
                return [];
            }
        }
        private void RefreshPlaybackInfo()
        {
            if (currentSession == null) return;

            var playbackInfo = currentSession.GetPlaybackInfo();
            PlaybackStatus = playbackInfo.PlaybackStatus;
            SupportsShuffle = playbackInfo.Controls.IsShuffleEnabled;
            SupportsRepeat = playbackInfo.Controls.IsRepeatEnabled;
            SupportsStop = playbackInfo.Controls.IsStopEnabled;
            IsShuffleActive = playbackInfo.IsShuffleActive;
            AutoRepeatMode = playbackInfo.AutoRepeatMode;

            OnSessionChanged?.Invoke();
        }
        public async Task PlayPauseSessionAsync()
        {
            if (currentSession == null) return;
            if (PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                await currentSession.TryPauseAsync();
            else
                await currentSession.TryPlayAsync();
        }
        public async Task StopAsync()
        {
            if (currentSession != null)
                await currentSession.TryStopAsync();
        }
        public async Task ToggleShuffleAsync()
        {
            if (currentSession == null) return;
            var playbackInfo = currentSession.GetPlaybackInfo();
            if (playbackInfo.Controls.IsShuffleEnabled && playbackInfo.IsShuffleActive.HasValue)
            {
                await currentSession.TryChangeShuffleActiveAsync(!playbackInfo.IsShuffleActive.Value);
            }
        }
        public async Task CycleRepeatModeAsync()
        {
            if (currentSession == null) return;
            var playbackInfo = currentSession.GetPlaybackInfo();
            if (playbackInfo.Controls.IsRepeatEnabled && playbackInfo.AutoRepeatMode.HasValue)
            {
                var nextMode = playbackInfo.AutoRepeatMode.Value switch
                {
                    MediaPlaybackAutoRepeatMode.None => MediaPlaybackAutoRepeatMode.List,
                    MediaPlaybackAutoRepeatMode.List => MediaPlaybackAutoRepeatMode.Track,
                    MediaPlaybackAutoRepeatMode.Track => MediaPlaybackAutoRepeatMode.None,
                    _ => MediaPlaybackAutoRepeatMode.None
                };
                await currentSession.TryChangeAutoRepeatModeAsync(nextMode);
            }
        }
        public async Task NextAsync()
        {
            if (currentSession != null)
                await currentSession.TrySkipNextAsync();
        }
        public async Task PreviousAsync()
        {
            if (currentSession != null)
                await currentSession.TrySkipPreviousAsync();
        }
        public void Dispose()
        {
            if (currentSession != null)
            {
                currentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                currentSession.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
            }
            if (sessionManager != null && sessionsChangedHandler != null)
            {
                sessionManager.SessionsChanged -= sessionsChangedHandler;
            }
            GC.SuppressFinalize(this);
        }
    }
}
