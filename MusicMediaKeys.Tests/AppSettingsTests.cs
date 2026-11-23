using System.Text.Json;
using Xunit;

namespace MusicMediaKeys.Tests
{
    public class AppSettingsTests
    {
        private static AppSettings RoundTrip(AppSettings settings)
        {
            string json = JsonSerializer.Serialize(settings, AppSettings.JsonOptions);
            return JsonSerializer.Deserialize<AppSettings>(json, AppSettings.JsonOptions)!;
        }

        [Fact]
        public void Json_RoundTripsSettingsAndBindings()
        {
            AppSettings settings = new AppSettings
            {
                SelectedApplication = ApplicationSelectionMode.YandexMusic,
                PreferWindowsMediaSession = false,
                VolumeDownBinding = new HotkeyBinding { Key = 120, Modifiers = (uint)ModifierKeys.Control },
                OverlayPosition = OverlayPosition.BottomCenter,
                ShowOverlay = false,
                VolumeStepPercent = 2,
                SeekStepSeconds = 30,
                HideHotkeysHint = true,
                SkipUacPrompt = true
            };

            AppSettings loaded = RoundTrip(settings);

            Assert.Equal(ApplicationSelectionMode.YandexMusic, loaded.SelectedApplication);
            Assert.False(loaded.PreferWindowsMediaSession);
            Assert.False(loaded.ShowOverlay);
            Assert.Equal(2, loaded.VolumeStepPercent);
            Assert.Equal(30, loaded.SeekStepSeconds);
            Assert.True(loaded.SkipUacPrompt);
            Assert.True(loaded.HideHotkeysHint);
            Assert.NotNull(loaded.VolumeDownBinding);
            Assert.Equal(120, loaded.VolumeDownBinding!.Key);
            Assert.Equal(OverlayPosition.BottomCenter, loaded.OverlayPosition);
        }

        [Fact]
        public void Json_RoundTripsOverlayAndCustomActions()
        {
            AppSettings settings = new AppSettings
            {
                OverlayPosition = OverlayPosition.BottomLeft,
                OverlayDurationSeconds = 5,
                OverlayScale = 0.95,
                CustomActionHotkeys = new System.Collections.Generic.List<ActionHotkeyBinding>
                {
                    new ActionHotkeyBinding
                    {
                        ActionId = AppActions.CycleTargetPlayer,
                        Binding = new HotkeyBinding { Key = 80, Modifiers = 1 }
                    }
                }
            };

            AppSettings loaded = RoundTrip(settings);

            Assert.Equal(OverlayPosition.BottomLeft, loaded.OverlayPosition);
            Assert.Equal(5, loaded.OverlayDurationSeconds);
            Assert.Equal(0.95, loaded.OverlayScale);
            Assert.Single(loaded.CustomActionHotkeys);
            Assert.Equal(AppActions.CycleTargetPlayer, loaded.CustomActionHotkeys[0].ActionId);
            Assert.Equal(80, loaded.CustomActionHotkeys[0].Binding!.Key);
        }

        [Fact]
        public void Json_PreservesExplicitNullVolumeBindings_AndUsesDefaultsWhenOmitted()
        {
            AppSettings cleared = new AppSettings
            {
                VolumeDownBinding = null,
                VolumeUpBinding = null,
                VolumeMuteBinding = null
            };

            AppSettings roundTripped = RoundTrip(cleared);
            Assert.Null(roundTripped.VolumeDownBinding);
            Assert.Null(roundTripped.VolumeUpBinding);
            Assert.Null(roundTripped.VolumeMuteBinding);

            AppSettings fromEmptyJson = JsonSerializer.Deserialize<AppSettings>("{}", AppSettings.JsonOptions)!;
            Assert.NotNull(fromEmptyJson.VolumeDownBinding);
            Assert.NotNull(fromEmptyJson.VolumeUpBinding);
            Assert.NotNull(fromEmptyJson.VolumeMuteBinding);
        }

        [Fact]
        public void AvailableCustomActions_AllHaveValidLocalizationKeys()
        {
            foreach (var actionId in AppActions.AvailableCustomActions)
            {
                string key = AppActions.GetActionLabelKey(actionId);
                Assert.NotEqual(actionId, key);

                string ru = Localization.GetString(key, Language.Russian);
                string en = Localization.GetString(key, Language.English);

                Assert.False(string.IsNullOrWhiteSpace(ru));
                Assert.False(string.IsNullOrWhiteSpace(en));
                Assert.NotEqual(key, ru);
                Assert.NotEqual(key, en);
            }
        }
    }
}
