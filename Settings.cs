using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MusicMediaKeys
{
    public enum Theme
    {
        Light,
        Dark
    }

    public enum ApplicationSelectionMode
    {
        Spotify = 0,
        YandexMusic = 1,
        Auto = 2,
        AppleMusic = 3,
        Aimp = 4,
        Foobar2000 = 5,
        Vlc = 6,
        YouTubeMusicDesktop = 7,
        Custom = 8
    }

    public enum OverlayPosition
    {
        TopLeft = 0,
        TopRight = 1,
        BottomLeft = 2,
        BottomRight = 3,
        TopCenter = 4,
        BottomCenter = 5,
        MiddleLeft = 6,
        MiddleRight = 7
    }

    public enum OverlayMonitorMode
    {
        ActiveMonitor = 0,
        PrimaryMonitor = 1,
        SpecificMonitor = 2
    }

    public static class OverlayPositions
    {
        public static (int Column, int Row) GetCell(OverlayPosition position) => position switch
        {
            OverlayPosition.TopCenter => (1, 0),
            OverlayPosition.TopRight => (2, 0),
            OverlayPosition.MiddleLeft => (0, 1),
            OverlayPosition.MiddleRight => (2, 1),
            OverlayPosition.BottomLeft => (0, 2),
            OverlayPosition.BottomCenter => (1, 2),
            OverlayPosition.BottomRight => (2, 2),
            _ => (0, 0)
        };
    }

    public class ActionHotkeyBinding
    {
        public string ActionId { get; set; } = "";
        public HotkeyBinding? Binding { get; set; }
    }

    public class AppSettings
    {
        public ApplicationSelectionMode SelectedApplication { get; set; } = ApplicationSelectionMode.Auto;
        public string CustomPlayerProcessName { get; set; } = "";
        public bool AutoStart { get; set; } = false;
        public bool IsFirstRunCompleted { get; set; } = false;
        public bool UseSystemMediaKeysOnly { get; set; } = false;
        public bool PreferWindowsMediaSession { get; set; } = true;
        public bool StartWithWindowOpen { get; set; } = false;
        public bool BlockMediaKeysForBrowser { get; set; } = false;
        public bool ShowOverlay { get; set; } = true;
        public bool ShowOverlayOnTrackChange { get; set; } = true;
        public OverlayMonitorMode OverlayMonitorMode { get; set; } = OverlayMonitorMode.ActiveMonitor;
        public string OverlayMonitorDeviceName { get; set; } = "";
        public int VolumeStepPercent { get; set; } = 5;
        public int VolumeDuckLevelPercent { get; set; } = 15;
        public int SeekStepSeconds { get; set; } = 10;
        public bool SkipUacPrompt { get; set; } = true;
        public bool PlayStartupSound { get; set; } = false;
        public string CustomStartupSoundFile { get; set; } = "";
        public Language Language { get; set; } = Language.English;
        public List<Language> InstalledLanguages { get; set; } = new();
        public Theme Theme { get; set; } = Theme.Dark;
        public OverlayPosition OverlayPosition { get; set; } = OverlayPosition.TopLeft;
        public int OverlayDurationSeconds { get; set; } = 3;
        public double OverlayScale { get; set; } = 1.0;
        public bool HideOverlayInFullscreen { get; set; } = true;
        public bool HideHotkeysHint { get; set; } = false;
        public bool HideVolumeHint { get; set; } = false;
        public bool ShowTooltips { get; set; } = true;
        public bool CheckForUpdates { get; set; } = true;
        public List<ActionHotkeyBinding> CustomActionHotkeys { get; set; } = new();
        public HotkeyBinding PreviousBinding { get; set; } = new HotkeyBinding { Key = MediaKeyCodes.MediaPrevTrack, Modifiers = 0 };
        public HotkeyBinding PlayPauseBinding { get; set; } = new HotkeyBinding { Key = MediaKeyCodes.MediaPlayPause, Modifiers = 0 };
        public HotkeyBinding NextBinding { get; set; } = new HotkeyBinding { Key = MediaKeyCodes.MediaNextTrack, Modifiers = 0 };
        public HotkeyBinding? VolumeDownBinding { get; set; } = new HotkeyBinding { Key = MediaKeyCodes.VolumeDown, Modifiers = 0 };
        public HotkeyBinding? VolumeUpBinding { get; set; } = new HotkeyBinding { Key = MediaKeyCodes.VolumeUp, Modifiers = 0 };
        public HotkeyBinding? VolumeMuteBinding { get; set; } = new HotkeyBinding { Key = MediaKeyCodes.VolumeMute, Modifiers = 0 };

        public IReadOnlyList<Language> GetAvailableLanguages()
        {
            if (InstalledLanguages == null || InstalledLanguages.Count == 0)
            {
                var all = new List<Language>(LanguageCatalog.All.Count);
                foreach (LanguageInfo info in LanguageCatalog.All)
                    all.Add(info.Language);
                return all;
            }

            var result = new List<Language>();
            foreach (Language lang in InstalledLanguages)
            {
                if (Enum.IsDefined(typeof(Language), lang) && !result.Contains(lang))
                    result.Add(lang);
            }

            if (result.Count > 0)
                return result;

            var fallbackAll = new List<Language>(LanguageCatalog.All.Count);
            foreach (LanguageInfo info in LanguageCatalog.All)
                fallbackAll.Add(info.Language);
            return fallbackAll;
        }

        internal static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private static string GetSettingsPath() => AppPaths.SettingsPath;

        private const int MaxSettingsFileSizeBytes = 1024 * 1024;

        public static AppSettings Load()
        {
            string settingsPath = GetSettingsPath();
            string backupPath = settingsPath + ".bak";

            if (TryLoadFromFile(settingsPath, out AppSettings? settings) && settings != null)
            {
                return settings;
            }

            if (TryLoadFromFile(backupPath, out AppSettings? backupSettings) && backupSettings != null)
            {
                EventLogService.Log("Settings", "Primary settings file corrupt or missing; successfully recovered from backup");
                try { backupSettings.Save(); }
                catch (Exception ex) { EventLogService.Log("Settings", "Failed to restore settings.json from the backup: " + ex.Message); }
                return backupSettings;
            }

            return new AppSettings();
        }

        private static bool TryLoadFromFile(string path, out AppSettings? settings)
        {
            settings = null;
            if (!File.Exists(path)) return false;

            try
            {
                var fi = new FileInfo(path);
                if (fi.Length == 0 || fi.Length > MaxSettingsFileSizeBytes)
                    return false;

                string json = File.ReadAllText(path);
                settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings != null)
                {
                    SettingsExportService.Normalize(settings);
                    return true;
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.Log($"Settings.TryLoadFromFile({Path.GetFileName(path)})", ex);
            }

            return false;
        }

        private static readonly object _saveLock = new();

        public void Save()
        {
            lock (_saveLock)
            {
                string settingsPath = GetSettingsPath();
                string tempPath = settingsPath + ".tmp";
                string backupPath = settingsPath + ".bak";

                try
                {
                    string? dir = Path.GetDirectoryName(settingsPath);
                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);

                    string json = JsonSerializer.Serialize(this, JsonOptions);
                    File.WriteAllText(tempPath, json);

                    if (File.Exists(settingsPath))
                    {
                        try { File.Copy(settingsPath, backupPath, overwrite: true); }
                        catch (Exception ex) { EventLogService.Log("Settings", "Failed to update settings.json.bak: " + ex.Message); }
                    }

                    File.Move(tempPath, settingsPath, overwrite: true);
                }
                catch (Exception ex)
                {
                    ErrorLogger.Log("Settings.Save", ex);
                    throw;
                }
                finally
                {
                    if (File.Exists(tempPath))
                    {
                        try { File.Delete(tempPath); } catch { }
                    }
                }
            }
        }

    }

    public class HotkeyBinding
    {
        public int Key { get; set; }
        public uint Modifiers { get; set; }

        public Keys GetKey() => (Keys)Key;
        public ModifierKeys GetModifiers() => (ModifierKeys)Modifiers;
    }
}
