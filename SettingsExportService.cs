using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MusicMediaKeys
{
    public static class SettingsExportService
    {
        private const long MaxImportFileBytes = 1024 * 1024;

        public static void Export(AppSettings settings, string filePath)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("File path is required.", nameof(filePath));

            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            string tempPath = filePath + ".tmp";
            try
            {
                string json = JsonSerializer.Serialize(settings, AppSettings.JsonOptions);
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, filePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }
            }
        }

        public static AppSettings ImportFromFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                throw new FileNotFoundException("Settings file not found.", filePath);

            var fi = new FileInfo(filePath);
            if (fi.Length == 0 || fi.Length > MaxImportFileBytes)
                throw new InvalidDataException("Settings file is empty or exceeds size limit.");

            string json = File.ReadAllText(filePath);
            var imported = JsonSerializer.Deserialize<AppSettings>(json, AppSettings.JsonOptions)
                ?? throw new InvalidDataException("Failed to deserialize settings.");

            Normalize(imported);
            return imported;
        }

        public static void Normalize(AppSettings settings)
        {
            if (!Enum.IsDefined(typeof(ApplicationSelectionMode), settings.SelectedApplication))
                settings.SelectedApplication = ApplicationSelectionMode.Auto;
            if (!Enum.IsDefined(typeof(OverlayMonitorMode), settings.OverlayMonitorMode))
                settings.OverlayMonitorMode = OverlayMonitorMode.PrimaryMonitor;
            if (!Enum.IsDefined(typeof(Language), settings.Language))
                settings.Language = Language.English;
            if (!Enum.IsDefined(typeof(Theme), settings.Theme))
                settings.Theme = Theme.Dark;
            if (!Enum.IsDefined(typeof(OverlayPosition), settings.OverlayPosition))
                settings.OverlayPosition = OverlayPosition.TopLeft;

            settings.CustomPlayerProcessName = KnownMediaSources.NormalizeCustomProcessName(settings.CustomPlayerProcessName);
            settings.VolumeStepPercent = Math.Clamp(settings.VolumeStepPercent, 1, 100);
            settings.VolumeDuckLevelPercent = Math.Clamp(settings.VolumeDuckLevelPercent, 1, 90);
            settings.SeekStepSeconds = Math.Clamp(settings.SeekStepSeconds, 1, 300);
            settings.OverlayDurationSeconds = Math.Clamp(settings.OverlayDurationSeconds, 1, 10);

            double scale = settings.OverlayScale;
            if (double.IsNaN(scale) || double.IsInfinity(scale))
                scale = 1.0;
            settings.OverlayScale = Math.Clamp(scale, 0.75, 1.40);

            settings.OverlayMonitorDeviceName ??= "";
            settings.CustomStartupSoundFile = AppSounds.SanitizeCustomSoundFileName(settings.CustomStartupSoundFile);

            var validLanguages = new List<Language>();
            if (settings.InstalledLanguages != null)
            {
                foreach (Language lang in settings.InstalledLanguages)
                {
                    if (Enum.IsDefined(typeof(Language), lang) && !validLanguages.Contains(lang))
                        validLanguages.Add(lang);
                }
            }
            settings.InstalledLanguages = validLanguages;
            if (validLanguages.Count > 0 && !validLanguages.Contains(settings.Language))
                settings.Language = validLanguages[0];

            settings.CustomActionHotkeys ??= new List<ActionHotkeyBinding>();
            settings.CustomActionHotkeys.RemoveAll(item => item == null || string.IsNullOrWhiteSpace(item.ActionId));
            settings.PreviousBinding ??= new HotkeyBinding { Key = MediaKeyCodes.MediaPrevTrack, Modifiers = 0 };
            settings.PlayPauseBinding ??= new HotkeyBinding { Key = MediaKeyCodes.MediaPlayPause, Modifiers = 0 };
            settings.NextBinding ??= new HotkeyBinding { Key = MediaKeyCodes.MediaNextTrack, Modifiers = 0 };
        }

        public static void CopyInto(AppSettings source, AppSettings target)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (target == null) throw new ArgumentNullException(nameof(target));

            Normalize(source);

            target.SelectedApplication = source.SelectedApplication;
            target.CustomPlayerProcessName = source.CustomPlayerProcessName;
            target.AutoStart = source.AutoStart;
            target.IsFirstRunCompleted = source.IsFirstRunCompleted;
            target.UseSystemMediaKeysOnly = source.UseSystemMediaKeysOnly;
            target.PreferWindowsMediaSession = source.PreferWindowsMediaSession;
            target.StartWithWindowOpen = source.StartWithWindowOpen;
            target.BlockMediaKeysForBrowser = source.BlockMediaKeysForBrowser;
            target.ShowOverlay = source.ShowOverlay;
            target.ShowOverlayOnTrackChange = source.ShowOverlayOnTrackChange;
            target.OverlayMonitorMode = source.OverlayMonitorMode;
            target.OverlayMonitorDeviceName = source.OverlayMonitorDeviceName;
            target.VolumeStepPercent = source.VolumeStepPercent;
            target.VolumeDuckLevelPercent = source.VolumeDuckLevelPercent;
            target.SeekStepSeconds = source.SeekStepSeconds;
            target.SkipUacPrompt = source.SkipUacPrompt;
            target.PlayStartupSound = source.PlayStartupSound;
            target.CustomStartupSoundFile = source.CustomStartupSoundFile;
            target.Language = source.Language;
            target.InstalledLanguages = source.InstalledLanguages != null
                ? new List<Language>(source.InstalledLanguages)
                : new List<Language>();
            target.Theme = source.Theme;
            target.OverlayPosition = source.OverlayPosition;
            target.OverlayDurationSeconds = source.OverlayDurationSeconds;
            target.OverlayScale = source.OverlayScale;
            target.HideOverlayInFullscreen = source.HideOverlayInFullscreen;
            target.HideHotkeysHint = source.HideHotkeysHint;
            target.HideVolumeHint = source.HideVolumeHint;
            target.ShowTooltips = source.ShowTooltips;
            target.CheckForUpdates = source.CheckForUpdates;

            target.PreviousBinding = CloneBinding(source.PreviousBinding) ?? new HotkeyBinding { Key = MediaKeyCodes.MediaPrevTrack, Modifiers = 0 };
            target.PlayPauseBinding = CloneBinding(source.PlayPauseBinding) ?? new HotkeyBinding { Key = MediaKeyCodes.MediaPlayPause, Modifiers = 0 };
            target.NextBinding = CloneBinding(source.NextBinding) ?? new HotkeyBinding { Key = MediaKeyCodes.MediaNextTrack, Modifiers = 0 };
            target.VolumeDownBinding = CloneBinding(source.VolumeDownBinding);
            target.VolumeUpBinding = CloneBinding(source.VolumeUpBinding);
            target.VolumeMuteBinding = CloneBinding(source.VolumeMuteBinding);

            target.CustomActionHotkeys.Clear();
            foreach (var item in source.CustomActionHotkeys)
            {
                if (string.IsNullOrWhiteSpace(item.ActionId)) continue;
                target.CustomActionHotkeys.Add(new ActionHotkeyBinding
                {
                    ActionId = item.ActionId,
                    Binding = CloneBinding(item.Binding)
                });
            }
        }

        private static HotkeyBinding? CloneBinding(HotkeyBinding? binding)
        {
            return binding == null
                ? null
                : new HotkeyBinding { Key = binding.Key, Modifiers = binding.Modifiers };
        }
    }
}
