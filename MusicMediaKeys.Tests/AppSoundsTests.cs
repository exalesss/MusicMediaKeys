using System;
using System.IO;
using System.Text;
using Xunit;

namespace MusicMediaKeys.Tests
{
    public class AppSoundsTests
    {
        [Fact]
        public void GenerateStartupJingleWave_ProducesValidRiffWaveBytesAndSmoothTail()
        {
            byte[] bytes = AppSounds.GenerateStartupJingleWave();

            Assert.True(bytes.Length > 44);
            Assert.Equal("RIFF", Encoding.ASCII.GetString(bytes, 0, 4));
            Assert.Equal("WAVE", Encoding.ASCII.GetString(bytes, 8, 4));

            short lastSample = BitConverter.ToInt16(bytes, bytes.Length - 2);
            Assert.InRange(lastSample, (short)-5, (short)5);
        }

        [Fact]
        public void GenerateEasterEggWave_ProducesValidRiffWaveBytes()
        {
            byte[] bytes = AppSounds.GenerateEasterEggWave();

            Assert.True(bytes.Length > 44);
            Assert.Equal("RIFF", Encoding.ASCII.GetString(bytes, 0, 4));
            Assert.Equal("WAVE", Encoding.ASCII.GetString(bytes, 8, 4));
        }

        [Fact]
        public void SetAndResetCustomStartupSound_ReplacesSingleFileAndCleansUp()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"mmk_snd_{Guid.NewGuid():N}");
            string sourceWav = Path.Combine(tempDir, "source1.wav");
            string sourceMp3 = Path.Combine(tempDir, "source2.mp3");
            string dataDir = Path.Combine(tempDir, "Data");
            Directory.CreateDirectory(tempDir);

            try
            {
                File.WriteAllBytes(sourceWav, AppSounds.GenerateStartupJingleWave());
                File.WriteAllBytes(sourceMp3, new byte[] { 1, 2, 3, 4 });

                var settings = new AppSettings();
                AppSounds.SetCustomStartupSound(sourceWav, settings, dataDir);
                Assert.Equal("custom_startup.wav", settings.CustomStartupSoundFile);
                Assert.True(File.Exists(Path.Combine(dataDir, "custom_startup.wav")));

                AppSounds.SetCustomStartupSound(sourceMp3, settings, dataDir);
                Assert.Equal("custom_startup.mp3", settings.CustomStartupSoundFile);
                Assert.False(File.Exists(Path.Combine(dataDir, "custom_startup.wav")));
                Assert.True(File.Exists(Path.Combine(dataDir, "custom_startup.mp3")));

                AppSounds.ResetCustomStartupSound(settings, dataDir);
                Assert.Equal("", settings.CustomStartupSoundFile);
                Assert.False(File.Exists(Path.Combine(dataDir, "custom_startup.mp3")));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
        }

        [Theory]
        [InlineData("..\\secret.wav")]
        [InlineData("../../secret.mp3")]
        [InlineData("\\\\attacker\\share\\evil.wav")]
        [InlineData("C:\\Windows\\Media\\chimes.wav")]
        [InlineData("custom_startup.exe")]
        [InlineData("")]
        [InlineData(null)]
        public void ResolveSafeCustomSoundPath_RejectsTraversalUncAndInvalidExtensions(string? input)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"mmk_snd_guard_{Guid.NewGuid():N}");
            Assert.Null(AppSounds.ResolveSafeCustomSoundPath(input, tempDir));
        }

        [Fact]
        public void ResolveSafeCustomSoundPath_AllowsValidLocalFileName()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"mmk_snd_guard_{Guid.NewGuid():N}");
            string? resolved = AppSounds.ResolveSafeCustomSoundPath("custom_startup.mp3", tempDir);
            Assert.Equal(Path.GetFullPath(Path.Combine(tempDir, "custom_startup.mp3")), resolved);
        }

        [Fact]
        public void StopAllSounds_AfterPlayStartupJingle_StopsImmediatelyWithoutBlocking()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            AppSounds.PlayStartupJingle();
            AppSounds.StopAllSounds();
            sw.Stop();

            Assert.True(sw.ElapsedMilliseconds < 200, $"Expected instant non-blocking cancellation, took {sw.ElapsedMilliseconds} ms");
        }
    }
}
