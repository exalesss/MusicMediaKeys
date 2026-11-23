using Xunit;

namespace MusicMediaKeys.Tests
{
    public class MediaSessionSourceClassifierTests
    {
        [Fact]
        public void Describe_ReturnsSpotifyLabel_ForSpotifySource()
        {
            string result = MediaSessionSourceClassifier.Describe("Spotify.exe");

            Assert.Equal(Localization.GetString("Spotify"), result);
        }

        [Theory]
        [InlineData("Yandex.Music")]
        [InlineData("Yandex Музыка")]
        [InlineData("яндекс музыка")]
        [InlineData("ЯМusic")]
        [InlineData("YandexMusic.exe")]
        public void YandexMusicVariants_AreClassifiedConsistently(string aumid)
        {
            Assert.Equal(Localization.GetString("YandexMusic"), MediaSessionSourceClassifier.Describe(aumid));
            Assert.True(MediaSessionSourceClassifier.IsTargetPlayer(aumid, ApplicationSelectionMode.YandexMusic));
            Assert.False(MediaSessionSourceClassifier.IsBrowser(aumid));
        }

        [Fact]
        public void Describe_ReturnsBrowserLabel_ForBrowserSource()
        {
            string result = MediaSessionSourceClassifier.Describe("msedge.exe");

            Assert.Equal(Localization.GetString("WindowKindBrowser"), result);
        }

        [Fact]
        public void Describe_ReturnsRawSource_WhenUnknown()
        {
            string result = MediaSessionSourceClassifier.Describe("custom.player.app");

            Assert.Equal("custom.player.app", result);
        }

        [Theory]
        [InlineData("chrome.exe", true)]
        [InlineData("msedge.exe", true)]
        [InlineData("firefox.exe", true)]
        [InlineData("opera.exe", true)]
        [InlineData("brave.exe", true)]
        [InlineData("vivaldi.exe", true)]
        [InlineData("iexplore.exe", true)]
        [InlineData("Spotify.exe", false)]
        [InlineData("YandexMusic.exe", false)]
        public void IsBrowser_ClassifiesCorrectly(string aumid, bool expected)
        {
            Assert.Equal(expected, MediaSessionSourceClassifier.IsBrowser(aumid));
        }

        [Theory]
        [InlineData("Spotify.exe", ApplicationSelectionMode.Spotify, true)]
        [InlineData("chrome.exe", ApplicationSelectionMode.Spotify, false)]
        [InlineData("YandexMusic.exe", ApplicationSelectionMode.YandexMusic, true)]
        [InlineData("Spotify.exe", ApplicationSelectionMode.Auto, true)]
        [InlineData("YandexMusic.exe", ApplicationSelectionMode.Auto, true)]
        [InlineData("AppleInc.AppleMusicWin_123!App", ApplicationSelectionMode.AppleMusic, true)]
        [InlineData("AIMP.exe", ApplicationSelectionMode.Aimp, true)]
        [InlineData("foobar2000.exe", ApplicationSelectionMode.Foobar2000, true)]
        [InlineData("vlc.exe", ApplicationSelectionMode.Vlc, true)]
        [InlineData("th-ch.youtube-music", ApplicationSelectionMode.YouTubeMusicDesktop, true)]
        [InlineData("th-ch.youtube-music", ApplicationSelectionMode.Auto, true)]
        [InlineData("chrome.exe", ApplicationSelectionMode.Auto, false)]
        public void IsTargetPlayer_ClassifiesCorrectly(string aumid, ApplicationSelectionMode mode, bool expected)
        {
            Assert.Equal(expected, MediaSessionSourceClassifier.IsTargetPlayer(aumid, mode));
        }

        [Fact]
        public void IsTargetPlayer_DetectsCustomPlayerSource()
        {
            Assert.True(MediaSessionSourceClassifier.IsTargetPlayer("MusicBee.exe", ApplicationSelectionMode.Custom, "musicbee.exe"));
            Assert.True(MediaSessionSourceClassifier.IsTargetPlayer("MusicBee.exe", ApplicationSelectionMode.Auto, "musicbee"));
            Assert.Equal("musicbee", MediaSessionSourceClassifier.Describe("MusicBee.exe", "musicbee.exe"));
        }
    }
}
