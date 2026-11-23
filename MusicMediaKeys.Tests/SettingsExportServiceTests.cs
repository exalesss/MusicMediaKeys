using System;
using System.IO;
using Xunit;

namespace MusicMediaKeys.Tests
{
    public class SettingsExportServiceTests
    {
        [Fact]
        public void ExportAndImport_RoundTripsAndClampsValues()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"mmk_export_{Guid.NewGuid():N}.json");
            try
            {
                var source = new AppSettings
                {
                    SelectedApplication = ApplicationSelectionMode.Custom,
                    CustomPlayerProcessName = @"C:\Players\musicbee.exe",
                    VolumeStepPercent = 250,
                    VolumeDuckLevelPercent = 0,
                    SeekStepSeconds = 999,
                    OverlayDurationSeconds = 50,
                    OverlayScale = 3.0,
                    PlayStartupSound = true,
                    ShowOverlayOnTrackChange = false,
                    OverlayMonitorMode = OverlayMonitorMode.PrimaryMonitor
                };

                SettingsExportService.Export(source, tempFile);
                AppSettings imported = SettingsExportService.ImportFromFile(tempFile);

                Assert.Equal(ApplicationSelectionMode.Custom, imported.SelectedApplication);
                Assert.Equal("musicbee", imported.CustomPlayerProcessName);
                Assert.Equal(100, imported.VolumeStepPercent);
                Assert.Equal(1, imported.VolumeDuckLevelPercent);
                Assert.Equal(300, imported.SeekStepSeconds);
                Assert.Equal(10, imported.OverlayDurationSeconds);
                Assert.Equal(1.40, imported.OverlayScale, precision: 2);
                Assert.True(imported.PlayStartupSound);
                Assert.False(imported.ShowOverlayOnTrackChange);
                Assert.Equal(OverlayMonitorMode.PrimaryMonitor, imported.OverlayMonitorMode);

                var target = new AppSettings();
                SettingsExportService.CopyInto(imported, target);
                Assert.Equal("musicbee", target.CustomPlayerProcessName);
                Assert.True(target.PlayStartupSound);
                Assert.Equal(OverlayMonitorMode.PrimaryMonitor, target.OverlayMonitorMode);
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [Fact]
        public void Normalize_SanitizesNaNEnumsInstalledLanguagesAndCustomSoundPath()
        {
            var settings = new AppSettings
            {
                SelectedApplication = (ApplicationSelectionMode)999,
                OverlayMonitorMode = (OverlayMonitorMode)999,
                Language = Language.English,
                Theme = (Theme)999,
                OverlayPosition = (OverlayPosition)999,
                OverlayScale = double.NaN,
                CustomStartupSoundFile = "..\\..\\evil.wav",
                InstalledLanguages = new System.Collections.Generic.List<Language> { Language.Russian, (Language)999, Language.Russian },
                CustomActionHotkeys = new System.Collections.Generic.List<ActionHotkeyBinding>
                {
                    null!,
                    new ActionHotkeyBinding { ActionId = "" },
                    new ActionHotkeyBinding { ActionId = "PlayPause" }
                }
            };

            SettingsExportService.Normalize(settings);

            Assert.Equal(ApplicationSelectionMode.Auto, settings.SelectedApplication);
            Assert.Equal(OverlayMonitorMode.PrimaryMonitor, settings.OverlayMonitorMode);
            Assert.Equal(Theme.Dark, settings.Theme);
            Assert.Equal(OverlayPosition.TopLeft, settings.OverlayPosition);
            Assert.Equal(1.0, settings.OverlayScale);
            Assert.Equal("", settings.CustomStartupSoundFile);
            Assert.Equal(new[] { Language.Russian }, settings.InstalledLanguages);
            Assert.Equal(Language.Russian, settings.Language);
            Assert.Single(settings.CustomActionHotkeys);
        }
    }
}
