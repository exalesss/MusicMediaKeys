using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MusicMediaKeys
{
    internal sealed record OverlayTrackMetadata(string Title, string Artist, BitmapImage? Thumbnail, bool IsInitialUpdate, bool IsTrackChanged);

    internal sealed class OverlayMediaSessionTracker : IDisposable
    {
        private readonly AppSettings _settings;
        private readonly Dispatcher _dispatcher;

        private SmtcSessionManager? _sessionManager;
        private SmtcSession? _currentSession;
        private int _updateTicket;
        private bool _isFirstMetadataUpdate = true;
        private string? _lastTrackKey;
        private volatile bool _isDisposed;

        public OverlayMediaSessionTracker(AppSettings settings, Dispatcher dispatcher)
        {
            _settings = settings;
            _dispatcher = dispatcher;
        }

        public event Action<OverlayTrackMetadata>? MetadataUpdated;
        public event Action? SessionCleared;
        public event Action<bool, bool>? SessionStateChanged;

        public bool HasActiveSession => _currentSession != null;

        public bool IsPlaying { get; private set; }

        public static bool EvaluateTrackChanged(ref string? lastTrackKey, string title, string artist, bool isInitialUpdate)
        {
            string key = $"{title}\u001F{artist}";
            bool changed = !isInitialUpdate && !string.Equals(lastTrackKey, key, StringComparison.Ordinal);
            lastTrackKey = key;
            return changed;
        }

        public async void Start()
        {
            try
            {
                var manager = await WindowsMediaSessionService.GetSessionManagerAsync();
                if (_isDisposed)
                    return;

                _sessionManager = manager;
                if (_sessionManager != null)
                {
                    _sessionManager.CurrentSessionChanged += SessionManager_SessionsChanged;
                    _sessionManager.SessionsChanged += SessionManager_SessionsChanged;
                    RefreshSession();
                }
            }
            catch (Exception ex)
            {
                EventLogService.Log("Overlay", "Failed to init GSMTC: " + ex.Message);
            }
        }

        public void RefreshSession()
        {
            try
            {
                if (_isDisposed || _sessionManager == null)
                    return;

                SmtcSession? session = FindShownSession(_sessionManager);

                if (session?.SourceAppUserModelId == _currentSession?.SourceAppUserModelId)
                {
                    session?.Dispose();
                    return;
                }

                UpdateCurrentSession(session);
            }
            catch (Exception ex)
            {
                EventLogService.Log("Overlay", "Failed to switch to the new media session: " + ex.Message);
            }
        }

        public bool TryGetTimeline(out TimeSpan position, out TimeSpan duration)
        {
            position = TimeSpan.Zero;
            duration = TimeSpan.Zero;

            if (_currentSession == null)
                return false;

            var timeline = _currentSession.GetTimelineProperties();
            if (timeline == null || timeline.EndTime.TotalSeconds <= 0)
                return false;

            position = WindowsMediaSessionService.GetLivePosition(_currentSession, timeline);
            duration = timeline.EndTime;
            return true;
        }

        public async Task TrySeekAsync(double seconds)
        {
            try
            {
                if (_currentSession != null)
                {
                    long targetTicks = TimeSpan.FromSeconds(seconds).Ticks;
                    await _currentSession.TryChangePlaybackPositionAsync(targetTicks);
                }
            }
            catch (Exception ex)
            {
                EventLogService.Log("Overlay", "ApplyTimelineSeek error: " + ex.Message);
            }
        }

        public void Dispose()
        {
            _isDisposed = true;
            try
            {
                if (_sessionManager != null)
                {
                    _sessionManager.CurrentSessionChanged -= SessionManager_SessionsChanged;
                    _sessionManager.SessionsChanged -= SessionManager_SessionsChanged;
                    _sessionManager = null;
                }
                UpdateCurrentSession(null);
            }
            catch
            {
            }
        }

        private void SessionManager_SessionsChanged(object? sender, EventArgs e)
        {
            if (_isDisposed || _dispatcher.HasShutdownStarted)
                return;

            _dispatcher.BeginInvoke(new Action(RefreshSession));
        }

        private SmtcSession? FindShownSession(SmtcSessionManager manager)
        {
            SmtcSession? current = manager.GetCurrentSession();
            if (_settings.UseSystemMediaKeysOnly)
                return current;

            if (current != null && MediaSessionSourceClassifier.IsTargetPlayer(current.SourceAppUserModelId, _settings.SelectedApplication, _settings.CustomPlayerProcessName))
                return current;

            current?.Dispose();

            SmtcSession? chosen = null;
            foreach (SmtcSession session in manager.GetSessions())
            {
                if (chosen == null && MediaSessionSourceClassifier.IsTargetPlayer(session.SourceAppUserModelId, _settings.SelectedApplication, _settings.CustomPlayerProcessName))
                {
                    chosen = session;
                }
                else
                {
                    session.Dispose();
                }
            }

            return chosen;
        }

        private void UpdateCurrentSession(SmtcSession? session)
        {
            try
            {
                if (_currentSession != null)
                {
                    _currentSession.MediaPropertiesChanged -= Session_MediaPropertiesChanged;
                    _currentSession.PlaybackInfoChanged -= Session_PlaybackInfoChanged;
                    _currentSession.TimelinePropertiesChanged -= Session_TimelinePropertiesChanged;
                    _currentSession.Dispose();
                }

                _currentSession = session;

                if (_currentSession != null)
                {
                    _currentSession.MediaPropertiesChanged += Session_MediaPropertiesChanged;
                    _currentSession.PlaybackInfoChanged += Session_PlaybackInfoChanged;
                    _currentSession.TimelinePropertiesChanged += Session_TimelinePropertiesChanged;
                    RefreshPlaybackState();
                    UpdateMetadata();
                    SessionStateChanged?.Invoke(false, true);
                }
                else
                {
                    IsPlaying = false;
                    SessionCleared?.Invoke();
                }
            }
            catch (Exception ex)
            {
                EventLogService.Log("Overlay", "Failed to update current session: " + ex.Message);
            }
        }

        private void Session_MediaPropertiesChanged(object? sender, EventArgs e)
        {
            if (_isDisposed || _dispatcher.HasShutdownStarted)
                return;

            _dispatcher.BeginInvoke(new Action(UpdateMetadata));
        }

        private void RefreshPlaybackState()
        {
            try
            {
                IsPlaying = _currentSession?.GetPlaybackStatus() == SmtcPlaybackStatus.Playing;
            }
            catch
            {
                IsPlaying = false;
            }
        }

        private void Session_PlaybackInfoChanged(object? sender, EventArgs e)
        {
            if (_isDisposed || _dispatcher.HasShutdownStarted)
                return;

            _dispatcher.BeginInvoke(new Action(() =>
            {
                RefreshPlaybackState();
                SessionStateChanged?.Invoke(true, true);
            }));
        }

        private void Session_TimelinePropertiesChanged(object? sender, EventArgs e)
        {
            if (_isDisposed || _dispatcher.HasShutdownStarted)
                return;

            _dispatcher.BeginInvoke(new Action(() => SessionStateChanged?.Invoke(false, true)));
        }

        private async void UpdateMetadata()
        {
            if (_isDisposed || _currentSession == null)
                return;

            int ticket = ++_updateTicket;
            var sessionAtStart = _currentSession;

            try
            {
                var props = await _currentSession.TryGetMediaPropertiesAsync();

                if (_isDisposed || ticket != _updateTicket || _currentSession != sessionAtStart)
                    return;

                if (props != null)
                {
                    BitmapImage? bitmap = null;
                    if (props.ThumbnailBytes is { Length: > 0 } thumbBytes)
                    {
                        try
                        {
                            using var stream = new MemoryStream(thumbBytes, writable: false);
                            bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.StreamSource = stream;
                            bitmap.DecodePixelWidth = 128;
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                            bitmap.Freeze();
                        }
                        catch
                        {
                            bitmap = null;
                        }
                    }

                    if (_isDisposed || ticket != _updateTicket || _currentSession != sessionAtStart)
                        return;

                    await _dispatcher.InvokeAsync(() =>
                    {
                        if (_isDisposed || ticket != _updateTicket || _currentSession != sessionAtStart)
                            return;

                        string title = string.IsNullOrEmpty(props.Title)
                            ? Localization.GetString("DiagnosticsNoTitle")
                            : props.Title;
                        string artist = props.Artist ?? "";
                        bool isInitial = _isFirstMetadataUpdate;
                        _isFirstMetadataUpdate = false;
                        bool isTrackChanged = EvaluateTrackChanged(ref _lastTrackKey, title, artist, isInitial);

                        MetadataUpdated?.Invoke(new OverlayTrackMetadata(title, artist, bitmap, isInitial, isTrackChanged));
                    });
                }
            }
            catch (Exception ex)
            {
                EventLogService.Log("Overlay", "Failed to update metadata: " + ex.Message);
            }
        }
    }
}
