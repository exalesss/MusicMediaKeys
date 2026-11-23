using System;
using System.Threading.Tasks;

namespace MusicMediaKeys
{
    internal static class WindowsMediaSessionService
    {
        public readonly record struct CommandResult(bool Success, string Details);

        private static volatile SmtcSessionManager? _sessionManager;
        private static readonly object _syncLock = new();

        public static bool Supports(MediaCommand command)
        {
            return command == MediaCommand.Previous ||
                   command == MediaCommand.PlayPause ||
                   command == MediaCommand.Next;
        }

        public static async Task<SmtcSessionManager?> GetSessionManagerAsync()
        {
            if (_sessionManager != null)
                return _sessionManager;

            try
            {
                var manager = await SmtcSessionManager.RequestAsync().ConfigureAwait(false);
                lock (_syncLock)
                {
                    if (_sessionManager == null)
                    {
                        _sessionManager = manager;
                    }
                    else
                    {
                        manager?.Dispose();
                    }
                }
                return _sessionManager;
            }
            catch (Exception ex)
            {
                EventLogService.Log("WindowsMediaSessionService", "Failed to get GSMTC manager: " + ex.Message);
                return null;
            }
        }

        public static SmtcSessionManager? GetSessionManager()
        {
            if (_sessionManager != null)
                return _sessionManager;

            try
            {
                return Task.Run(async () => await GetSessionManagerAsync().ConfigureAwait(false))
                    .GetAwaiter().GetResult();
            }
            catch
            {
                return null;
            }
        }

        public static CommandResult TrySendCommand(MediaCommand command, bool allowBrowser = true, ApplicationSelectionMode? requiredPlayer = null)
        {
            if (!Supports(command))
                return new CommandResult(false, "Windows media session does not handle this command");

            try
            {
                using var session = TryGetCurrentSession(out string source, out string details);
                if (session == null)
                    return new CommandResult(false, details);

                string aumid = session.SourceAppUserModelId ?? "";
                bool isBrowser = MediaSessionSourceClassifier.IsBrowser(aumid);

                if (!allowBrowser && isBrowser)
                {
                    return new CommandResult(false, $"Blocked media command for browser session | {source}");
                }

                if (requiredPlayer.HasValue)
                {
                    bool matches = MediaSessionSourceClassifier.IsTargetPlayer(aumid, requiredPlayer.Value);
                    if (!matches)
                    {
                        return new CommandResult(false, $"Current media session ({source}) does not match target player ({requiredPlayer.Value})");
                    }
                }

                bool ok = false;
                Task<bool>? opTask = command switch
                {
                    MediaCommand.Previous => session.TrySkipPreviousAsync(),
                    MediaCommand.PlayPause => session.TryTogglePlayPauseAsync(),
                    MediaCommand.Next => session.TrySkipNextAsync(),
                    _ => null
                };

                if (opTask != null)
                {
                    if (Task.WaitAny(new Task[] { opTask }, 1500) == 0)
                    {
                        ok = opTask.GetAwaiter().GetResult();
                    }
                    else
                    {
                        _ = opTask.ContinueWith(
                            t => { _ = t.Exception; },
                            TaskContinuationOptions.OnlyOnFaulted);
                        return new CommandResult(false, $"Windows media session timed out | {source}");
                    }
                }

                return ok
                    ? new CommandResult(true, $"Handled by Windows media session | {source}")
                    : new CommandResult(false, $"Windows media session rejected command | {source}");
            }
            catch (Exception ex)
            {
                return new CommandResult(false, ex.Message);
            }
        }

        public static SmtcSession? TryGetCurrentSession(out string source, out string details)
        {
            source = Localization.GetString("DiagnosticsNoneValue");
            var manager = GetSessionManager();
            if (manager == null)
            {
                details = "Windows media session manager request returned no result";
                return null;
            }

            var session = manager.GetCurrentSession();
            if (session == null)
            {
                details = "No current Windows media session";
                return null;
            }

            source = MediaSessionSourceClassifier.Describe(session.SourceAppUserModelId);
            details = "Windows media session is available";
            return session;
        }

        public static async Task<string?> TryGetCurrentTrackStringAsync()
        {
            try
            {
                var manager = await GetSessionManagerAsync().ConfigureAwait(false);
                using var session = manager?.GetCurrentSession();
                if (session != null)
                {
                    var props = await session.TryGetMediaPropertiesAsync().ConfigureAwait(false);
                    if (props != null)
                    {
                        string artist = props.Artist.Trim();
                        string title = props.Title.Trim();

                        if (!string.IsNullOrEmpty(artist) && !string.IsNullOrEmpty(title))
                            return $"{artist} — {title}";
                        if (!string.IsNullOrEmpty(title))
                            return title;
                        if (!string.IsNullOrEmpty(artist))
                            return artist;
                    }
                }
            }
            catch (Exception ex)
            {
                EventLogService.Log("WindowsMediaSessionService", "GetTrackString GSMTC error: " + ex.Message);
            }

            foreach (MediaPlayerDescriptor player in KnownMediaSources.ManagedPlayers)
            {
                string? desktopTrack = MediaPlayerProviders.TryGetDesktopPlayerTrackTitle(player.Application);
                if (!string.IsNullOrEmpty(desktopTrack))
                    return desktopTrack;
            }
            return null;
        }

        public static async Task<bool> TrySeekAsync(int offsetSeconds)
        {
            try
            {
                var manager = await GetSessionManagerAsync().ConfigureAwait(false);
                using var session = manager?.GetCurrentSession();
                if (session == null) return false;

                var timeline = session.GetTimelineProperties();
                if (timeline == null) return false;

                long currentTicks = GetLivePosition(session, timeline).Ticks;
                long deltaTicks = TimeSpan.FromSeconds(offsetSeconds).Ticks;
                long targetTicks = currentTicks + deltaTicks;

                if (targetTicks < 0) targetTicks = 0;
                if (timeline.EndTime.Ticks > 0 && targetTicks > timeline.EndTime.Ticks)
                    targetTicks = timeline.EndTime.Ticks;

                return await session.TryChangePlaybackPositionAsync(targetTicks).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                EventLogService.Log("WindowsMediaSessionService", "Seek error: " + ex.Message);
                return false;
            }
        }

        public static TimeSpan GetLivePosition(SmtcSession session, SmtcTimelineProperties timeline)
        {
            return ComputeLivePosition(timeline, session.GetPlaybackStatus() == SmtcPlaybackStatus.Playing, DateTimeOffset.UtcNow);
        }

        internal static TimeSpan ComputeLivePosition(SmtcTimelineProperties timeline, bool isPlaying, DateTimeOffset utcNow)
        {
            TimeSpan position = timeline.Position;

            if (isPlaying && timeline.LastUpdatedTime > DateTimeOffset.MinValue)
            {
                TimeSpan elapsed = utcNow - timeline.LastUpdatedTime;
                if (elapsed > TimeSpan.Zero)
                    position += elapsed;
            }

            if (timeline.EndTime > TimeSpan.Zero && position > timeline.EndTime)
                position = timeline.EndTime;
            if (position < TimeSpan.Zero)
                position = TimeSpan.Zero;

            return position;
        }
    }
}
