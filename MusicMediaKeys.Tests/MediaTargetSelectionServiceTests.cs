using Xunit;

namespace MusicMediaKeys.Tests
{
    public class MediaTargetSelectionServiceTests
    {
        [Fact]
        public void Resolve_RespectsExplicitSpotifySelection()
        {
            MediaApplication result = MediaTargetSelectionService.Resolve(
                ApplicationSelectionMode.Spotify,
                MediaApplication.YandexMusic,
                MediaApplication.YandexMusic,
                spotifyRunning: false,
                yandexRunning: true);

            Assert.Equal(MediaApplication.Spotify, result);
        }

        [Fact]
        public void Resolve_PrefersForegroundDesktopPlayerInAuto()
        {
            MediaApplication result = MediaTargetSelectionService.Resolve(
                ApplicationSelectionMode.Auto,
                MediaApplication.YandexMusic,
                MediaApplication.Spotify,
                spotifyRunning: true,
                yandexRunning: true);

            Assert.Equal(MediaApplication.YandexMusic, result);
        }

        [Fact]
        public void Resolve_FallsBackToLastActiveDesktopPlayerInAuto()
        {
            MediaApplication result = MediaTargetSelectionService.Resolve(
                ApplicationSelectionMode.Auto,
                null,
                MediaApplication.YandexMusic,
                spotifyRunning: true,
                yandexRunning: true);

            Assert.Equal(MediaApplication.YandexMusic, result);
        }

        [Fact]
        public void Resolve_UsesRunningAppWhenOnlyOneIsAvailable()
        {
            MediaApplication result = MediaTargetSelectionService.Resolve(
                ApplicationSelectionMode.Auto,
                null,
                null,
                spotifyRunning: false,
                yandexRunning: true);

            Assert.Equal(MediaApplication.YandexMusic, result);
        }

        [Fact]
        public void Resolve_SupportsExtendedBuiltInAndCustomModes()
        {
            Assert.Equal(MediaApplication.Aimp, MediaTargetSelectionService.Resolve(ApplicationSelectionMode.Aimp, null, null, new[] { MediaApplication.Spotify }));
            Assert.Equal(MediaApplication.Vlc, MediaTargetSelectionService.Resolve(ApplicationSelectionMode.Auto, null, null, new[] { MediaApplication.Vlc }));
            Assert.Equal(MediaApplication.Custom, MediaTargetSelectionService.Resolve(ApplicationSelectionMode.Custom, null, null, System.Array.Empty<MediaApplication>()));
        }
    }
}
