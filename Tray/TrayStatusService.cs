using System;

namespace MusicMediaKeys
{
    public static class TrayStatusService
    {
        public static string BuildTrayText(AppSettings settings, bool isRunning, MediaApplication activeApplication, bool hotkeysSuspended = false)
        {
            string appName = Localization.GetString("AppName");

            if (hotkeysSuspended)
                return FormatTrayStatus(appName, Localization.GetString("TrayHotkeysSuspended"));

            if (settings.UseSystemMediaKeysOnly)
                return FormatTrayStatus(appName, Localization.GetString("TrayModeAnyApp"));

            if (settings.SelectedApplication == ApplicationSelectionMode.Auto)
            {
                if (!isRunning)
                    return FormatTrayStatus(appName, Localization.GetString("NoActivePrograms"));

                string activeApplicationName = MediaPlayerProviders.GetApplicationDisplayName(activeApplication);
                string appDesc = $"{Localization.GetString("Auto")} ({activeApplicationName}) {Localization.GetString("AppActive")}";
                return FormatTrayStatus(appName, appDesc);
            }

            string selectedApp;
            if (settings.SelectedApplication == ApplicationSelectionMode.Custom)
            {
                string custom = KnownMediaSources.NormalizeCustomProcessName(settings.CustomPlayerProcessName);
                selectedApp = !string.IsNullOrEmpty(custom) ? custom : Localization.GetString("CustomPlayer");
            }
            else
            {
                selectedApp = KnownMediaSources.TryGetDescriptor(settings.SelectedApplication, out MediaPlayerDescriptor? desc, settings.CustomPlayerProcessName)
                    ? Localization.GetString(desc!.LocalizationKey)
                    : Localization.GetString("YandexMusic");
            }

            string status = isRunning
                ? $"{selectedApp} {Localization.GetString("AppActive")}"
                : $"{selectedApp} {Localization.GetString("AppNotRunning")}";

            return FormatTrayStatus(appName, status);
        }

        private static string FormatTrayStatus(string appName, string status)
        {
            string format = Localization.GetString("TrayStatusFormat");
            if (string.IsNullOrEmpty(format)) format = "{0} - {1}";
            return string.Format(format, appName, status);
        }

        public static bool ShouldUseActiveIcon(bool isRunning, bool anyAppMode, bool hotkeysSuspended = false)
        {
            return !hotkeysSuspended && (isRunning || anyAppMode);
        }
    }
}
