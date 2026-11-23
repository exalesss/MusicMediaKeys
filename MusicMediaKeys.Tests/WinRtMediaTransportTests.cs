using System;
using System.Threading.Tasks;
using Xunit;

namespace MusicMediaKeys.Tests
{
    public class WinRtMediaTransportTests
    {
        [Fact]
        public async Task RequestManager_SucceedsAndEnumeratesSessionsWithoutError()
        {
            using SmtcSessionManager? manager = await SmtcSessionManager.RequestAsync();
            Assert.NotNull(manager);

            EventHandler handler = (_, _) => { };
            manager!.CurrentSessionChanged += handler;
            manager.SessionsChanged += handler;

            var sessions = manager.GetSessions();
            Assert.NotNull(sessions);
            foreach (var s in sessions)
                s.Dispose();

            using var current = manager.GetCurrentSession();

            manager.CurrentSessionChanged -= handler;
            manager.SessionsChanged -= handler;
        }

        [Fact]
        public void ComputeLivePosition_AdvancesWhenPlayingAndClampsAtEndTime()
        {
            var baseTime = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
            var timeline = new SmtcTimelineProperties(
                Position: TimeSpan.FromSeconds(30),
                EndTime: TimeSpan.FromSeconds(100),
                LastUpdatedTime: baseTime);

            TimeSpan playingPos = WindowsMediaSessionService.ComputeLivePosition(
                timeline,
                isPlaying: true,
                utcNow: baseTime.AddSeconds(15));
            Assert.Equal(TimeSpan.FromSeconds(45), playingPos);

            TimeSpan clampedPos = WindowsMediaSessionService.ComputeLivePosition(
                timeline,
                isPlaying: true,
                utcNow: baseTime.AddSeconds(200));
            Assert.Equal(TimeSpan.FromSeconds(100), clampedPos);

            TimeSpan pausedPos = WindowsMediaSessionService.ComputeLivePosition(
                timeline,
                isPlaying: false,
                utcNow: baseTime.AddSeconds(15));
            Assert.Equal(TimeSpan.FromSeconds(30), pausedPos);
        }
    }
}
