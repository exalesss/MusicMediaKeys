using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MusicMediaKeys.Installer
{
    internal sealed class InstallOptions
    {
        public string InstallDirectory { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "MusicMediaKeys");

        public bool IsPortable { get; set; }
        public bool CreateDesktopShortcut { get; set; } = true;
        public bool CreateStartMenuShortcut { get; set; } = true;

        public int Language { get; set; } = (int)LanguageCatalog.DetectSystemLanguage();
        public List<int> InstalledLanguages { get; set; } = LanguageCatalog.All.Select(l => (int)l.Language).ToList();
        public int Theme { get; set; } = 1;
        public int SelectedApplication { get; set; } = 2;
        public string CustomPlayerProcessName { get; set; } = "";

        public bool AutoStart { get; set; } = true;
        public bool SkipUacPrompt { get; set; } = true;
        public bool StartWithWindowOpen { get; set; } = false;
        public bool ShowOverlay { get; set; } = true;
        public bool PlayStartupSound { get; set; } = false;

        public int PreviousKey { get; set; } = 0xB1;
        public uint PreviousModifiers { get; set; } = 0;
        public int PlayPauseKey { get; set; } = 0xB3;
        public uint PlayPauseModifiers { get; set; } = 0;
        public int NextKey { get; set; } = 0xB0;
        public uint NextModifiers { get; set; } = 0;
    }

    internal static class InstallerModel
    {
        public static string BuildSettingsJson(InstallOptions options)
        {
            List<int> installedLangs = options.InstalledLanguages != null && options.InstalledLanguages.Count > 0
                ? new List<int>(options.InstalledLanguages)
                : new List<int> { options.Language };

            if (!installedLangs.Contains(options.Language))
                installedLangs.Add(options.Language);

            var payload = new
            {
                SelectedApplication = options.SelectedApplication,
                CustomPlayerProcessName = options.CustomPlayerProcessName ?? "",
                AutoStart = options.AutoStart,
                IsFirstRunCompleted = true,
                SkipUacPrompt = options.SkipUacPrompt,
                StartWithWindowOpen = options.StartWithWindowOpen,
                ShowOverlay = options.ShowOverlay,
                PlayStartupSound = options.PlayStartupSound,
                Language = options.Language,
                InstalledLanguages = installedLangs,
                Theme = options.Theme,
                PreviousBinding = new { Key = options.PreviousKey, Modifiers = options.PreviousModifiers },
                PlayPauseBinding = new { Key = options.PlayPauseKey, Modifiers = options.PlayPauseModifiers },
                NextBinding = new { Key = options.NextKey, Modifiers = options.NextModifiers }
            };

            return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
