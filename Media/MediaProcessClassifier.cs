using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MusicMediaKeys
{
    public static class MediaProcessClassifier
    {
        private static readonly string[] PlayerProcessNames =
        {
            "spotify", "spotify helper", "spotifyhelper", "vlc", "foobar2000", "itunes", "wmplayer", "yandexmusic", "yandex music", "яндекс музыка", "applemusic", "apple music", "aimp", "ytmdesktop", "pear-desktop"
        };

        private static readonly string[] PlayerProcessNameSubstrings =
        {
            "spotify", "vlc", "foobar", "itunes", "wmplayer", "yandex", "music", "aimp", "ytmdesktop", "pear-desktop"
        };

        public static bool IsDesktopPlayer(string? processName, string? executablePath)
        {
            foreach (MediaPlayerDescriptor player in KnownMediaSources.ManagedPlayers)
            {
                if (IsPlayerMatch(processName, executablePath, player))
                    return true;
            }

            if (!string.IsNullOrEmpty(KnownMediaSources.CustomProcessName) &&
                IsPlayerMatch(processName, executablePath, KnownMediaSources.CreateCustomDescriptor()))
            {
                return true;
            }

            return false;
        }

        internal static bool IsPlayerMatch(string? processName, string? executablePath, MediaPlayerDescriptor descriptor)
        {
            if (descriptor.ProcessNames.Length > 0 && MatchesExactName(processName, descriptor.ProcessNames))
                return true;

            string normalizedName = Normalize(processName);
            if (!string.IsNullOrEmpty(normalizedName) && descriptor.MatchesSourceLower(normalizedName))
                return true;

            string normalizedPath = Normalize(executablePath);
            return !string.IsNullOrEmpty(normalizedPath) && descriptor.MatchesSourceLower(normalizedPath);
        }

        public static bool IsSpotify(string? processName, string? executablePath)
        {
            return MatchesExactName(processName, KnownMediaSources.Spotify.ProcessNames) ||
                   KnownMediaSources.IsSpotifyLower(Normalize(executablePath));
        }

        public static bool IsYandexMusic(string? processName, string? executablePath)
        {
            return MatchesExactName(processName, KnownMediaSources.YandexMusic.ProcessNames) ||
                   KnownMediaSources.IsYandexMusicLower(Normalize(processName)) ||
                   KnownMediaSources.IsYandexMusicLower(Normalize(executablePath));
        }

        public static bool IsBrowser(string? processName, string? executablePath)
        {
            if (IsDesktopPlayer(processName, executablePath))
                return false;

            string normalizedName = Normalize(processName);
            if (!string.IsNullOrEmpty(normalizedName) &&
                (KnownMediaSources.BrowserExactNames.Contains(normalizedName, StringComparer.OrdinalIgnoreCase) ||
                 KnownMediaSources.BrowserMarkers.Any(sub => normalizedName.Contains(sub, StringComparison.OrdinalIgnoreCase))))
            {
                return true;
            }

            return FileNameContainsAny(executablePath, KnownMediaSources.BrowserMarkers) ||
                   FileNameContainsAny(executablePath, new[] { "iexplore" });
        }

        public static bool IsPlayer(string? processName, string? executablePath)
        {
            if (IsDesktopPlayer(processName, executablePath))
                return true;

            string normalizedName = Normalize(processName);
            if (!string.IsNullOrEmpty(normalizedName) &&
                (PlayerProcessNames.Contains(normalizedName, StringComparer.OrdinalIgnoreCase) ||
                 PlayerProcessNameSubstrings.Any(sub => normalizedName.Contains(sub, StringComparison.OrdinalIgnoreCase))))
            {
                return true;
            }

            return IsBrowser(processName, executablePath);
        }

        private static bool MatchesExactName(string? processName, params string[] candidates)
        {
            string normalizedName = Normalize(processName);
            return !string.IsNullOrEmpty(normalizedName) &&
                   candidates.Any(candidate => string.Equals(candidate, normalizedName, StringComparison.OrdinalIgnoreCase));
        }

        private static bool FileNameContainsAny(string? executablePath, IReadOnlyCollection<string> markers)
        {
            string normalizedPath = Normalize(executablePath);
            if (string.IsNullOrEmpty(normalizedPath))
                return false;

            string fileName = Path.GetFileNameWithoutExtension(normalizedPath);
            return markers.Any(marker => fileName.Contains(marker, StringComparison.OrdinalIgnoreCase));
        }

        private static string Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
        }
    }
}
