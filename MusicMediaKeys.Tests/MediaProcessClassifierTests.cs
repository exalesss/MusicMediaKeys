using Xunit;

namespace MusicMediaKeys.Tests
{
    public class MediaProcessClassifierTests
    {
        [Fact]
        public void IsSpotify_DetectsKnownProcessName()
        {
            Assert.True(MediaProcessClassifier.IsSpotify("Spotify", ""));
        }

        [Fact]
        public void IsYandexMusic_DetectsRussianProcessName()
        {
            Assert.True(MediaProcessClassifier.IsYandexMusic("Яндекс Музыка", ""));
        }

        [Fact]
        public void IsYandexMusic_DetectsExecutablePath()
        {
            Assert.True(MediaProcessClassifier.IsYandexMusic("App", @"C:\Users\me\AppData\Local\Yandex\Music\music.exe"));
        }

        [Fact]
        public void IsBrowser_DetectsKnownBrowserProcess()
        {
            Assert.True(MediaProcessClassifier.IsBrowser("chrome", ""));
        }

        [Fact]
        public void IsBrowser_DoesNotTreatYandexMusicAsBrowser()
        {
            Assert.False(MediaProcessClassifier.IsBrowser("Яндекс Музыка", @"C:\Users\me\AppData\Local\Programs\YandexMusic\Яндекс Музыка.exe"));
        }

        [Fact]
        public void IsBrowser_DetectsYandexBrowserByExecutableName()
        {
            Assert.True(MediaProcessClassifier.IsBrowser("browser", @"C:\Users\me\AppData\Local\Yandex\YandexBrowser\Application\browser.exe"));
        }

        [Fact]
        public void IsBrowser_IgnoresBrowserWordsInUserProfilePath()
        {
            Assert.False(MediaProcessClassifier.IsBrowser("Discord", @"C:\Users\Operator\AppData\Local\Discord\app-1.0\Discord.exe"));
        }

        [Fact]
        public void IsPlayer_TreatsBrowsersAsPlayableTargets()
        {
            Assert.True(MediaProcessClassifier.IsPlayer("msedge", @"C:\Program Files\Microsoft\Edge\Application\msedge.exe"));
        }

        [Fact]
        public void IsDesktopPlayer_DetectsSpotify()
        {
            Assert.True(MediaProcessClassifier.IsDesktopPlayer("Spotify", ""));
        }

        [Fact]
        public void IsBrowser_DetectsFirefox()
        {
            Assert.True(MediaProcessClassifier.IsBrowser("firefox", ""));
        }

        [Theory]
        [InlineData("vivaldi", @"C:\Users\me\AppData\Local\Vivaldi\Application\vivaldi.exe")]
        [InlineData("iexplore", @"C:\Program Files\Internet Explorer\iexplore.exe")]
        public void IsBrowser_DetectsVivaldiAndIexplore(string processName, string exePath)
        {
            Assert.True(MediaProcessClassifier.IsBrowser(processName, exePath));
        }

        [Fact]
        public void IsYandexMusic_DetectsShortCyrillicAlias()
        {
            Assert.True(MediaProcessClassifier.IsYandexMusic("ЯМusic", ""));
            Assert.False(MediaProcessClassifier.IsBrowser("ЯМusic", ""));
        }

        [Fact]
        public void IsPlayer_RejectsUnrelatedProcess()
        {
            Assert.False(MediaProcessClassifier.IsPlayer("notepad", @"C:\Windows\notepad.exe"));
        }

        [Theory]
        [InlineData("AppleMusic", @"C:\Program Files\WindowsApps\AppleInc.AppleMusicWin\AppleMusic.exe")]
        [InlineData("AIMP", @"C:\Program Files\AIMP\AIMP.exe")]
        [InlineData("foobar2000", @"C:\Program Files\foobar2000\foobar2000.exe")]
        [InlineData("vlc", @"C:\Program Files\VideoLAN\VLC\vlc.exe")]
        [InlineData("ytmdesktop", @"C:\Users\me\AppData\Local\Programs\ytmdesktop\ytmdesktop.exe")]
        [InlineData("pear-desktop", @"C:\Users\me\AppData\Local\Programs\pear-desktop\YouTube Music.exe")]
        public void IsDesktopPlayer_DetectsNewBuiltInPlayers(string processName, string exePath)
        {
            Assert.True(MediaProcessClassifier.IsDesktopPlayer(processName, exePath));
            Assert.False(MediaProcessClassifier.IsBrowser(processName, exePath));
        }

        [Theory]
        [InlineData("musicbee", "musicbee")]
        [InlineData("musicbee.exe", "musicbee")]
        [InlineData(@"  ""C:\Program Files\MusicBee\MusicBee.exe""  ", "MusicBee")]
        public void NormalizeCustomProcessName_StripsPathAndExtension(string raw, string expected)
        {
            Assert.Equal(expected, KnownMediaSources.NormalizeCustomProcessName(raw));
        }

        [Fact]
        public void IsDesktopPlayer_DetectsCustomPlayerWhenConfigured()
        {
            string prev = KnownMediaSources.CustomProcessName;
            try
            {
                KnownMediaSources.CustomProcessName = @"C:\Tools\MusicBee\MusicBee.exe";
                Assert.True(MediaProcessClassifier.IsDesktopPlayer("MusicBee", @"C:\Tools\MusicBee\MusicBee.exe"));
                Assert.False(MediaProcessClassifier.IsDesktopPlayer("notepad", @"C:\Windows\notepad.exe"));
            }
            finally
            {
                KnownMediaSources.CustomProcessName = prev;
            }
        }
    }
}
