using System;
using System.Collections.Generic;
using System.IO;

namespace MusicMediaKeys
{
    internal sealed class MediaPlayerDescriptor
    {
        private readonly Func<string, bool> _matchesSourceLower;
        private readonly Func<string, bool> _isPlaceholderWindowTitle;

        public MediaPlayerDescriptor(
            MediaApplication application,
            string localizationKey,
            string? mainWindowClassName,
            bool requireVisibleWindow,
            string[] processNames,
            Func<string, bool> matchesSourceLower,
            Func<string, bool> isPlaceholderWindowTitle)
        {
            Application = application;
            LocalizationKey = localizationKey;
            MainWindowClassName = mainWindowClassName;
            RequireVisibleWindow = requireVisibleWindow;
            ProcessNames = processNames ?? Array.Empty<string>();
            _matchesSourceLower = matchesSourceLower;
            _isPlaceholderWindowTitle = isPlaceholderWindowTitle;
        }

        public MediaApplication Application { get; }

        public string LocalizationKey { get; }

        public string? MainWindowClassName { get; }

        public bool RequireVisibleWindow { get; }

        public string[] ProcessNames { get; }

        public bool MatchesSourceLower(string lower) => _matchesSourceLower(lower);

        public bool IsPlaceholderWindowTitle(string title) => _isPlaceholderWindowTitle(title);
    }

    internal static class KnownMediaSources
    {
        public static readonly MediaPlayerDescriptor Spotify = new(
            application: MediaApplication.Spotify,
            localizationKey: "Spotify",
            mainWindowClassName: "SpotifyMainWindow",
            requireVisibleWindow: false,
            processNames: new[] { "Spotify", "Spotify Helper", "SpotifyHelper" },
            matchesSourceLower: IsSpotifyLower,
            isPlaceholderWindowTitle: title => title.StartsWith("Spotify", StringComparison.OrdinalIgnoreCase));

        public static readonly MediaPlayerDescriptor YandexMusic = new(
            application: MediaApplication.YandexMusic,
            localizationKey: "YandexMusic",
            mainWindowClassName: null,
            requireVisibleWindow: true,
            processNames: new[] { "Яндекс Музыка", "YandexMusic", "Yandex Music" },
            matchesSourceLower: IsYandexMusicLower,
            isPlaceholderWindowTitle: title =>
                title.Equals("Яндекс Музыка", StringComparison.OrdinalIgnoreCase) ||
                title.Equals("Yandex Music", StringComparison.OrdinalIgnoreCase));

        public static readonly MediaPlayerDescriptor AppleMusic = new(
            application: MediaApplication.AppleMusic,
            localizationKey: "AppleMusic",
            mainWindowClassName: null,
            requireVisibleWindow: true,
            processNames: new[] { "AppleMusic", "Apple Music" },
            matchesSourceLower: IsAppleMusicLower,
            isPlaceholderWindowTitle: title => title.Equals("Apple Music", StringComparison.OrdinalIgnoreCase));

        public static readonly MediaPlayerDescriptor Aimp = new(
            application: MediaApplication.Aimp,
            localizationKey: "Aimp",
            mainWindowClassName: "AIMP2_RemoteInfo",
            requireVisibleWindow: false,
            processNames: new[] { "AIMP", "AIMP3", "AIMP4", "AIMP5" },
            matchesSourceLower: IsAimpLower,
            isPlaceholderWindowTitle: title => title.StartsWith("AIMP", StringComparison.OrdinalIgnoreCase));

        public static readonly MediaPlayerDescriptor Foobar2000 = new(
            application: MediaApplication.Foobar2000,
            localizationKey: "Foobar2000",
            mainWindowClassName: null,
            requireVisibleWindow: true,
            processNames: new[] { "foobar2000" },
            matchesSourceLower: IsFoobar2000Lower,
            isPlaceholderWindowTitle: title => title.Equals("foobar2000", StringComparison.OrdinalIgnoreCase));

        public static readonly MediaPlayerDescriptor Vlc = new(
            application: MediaApplication.Vlc,
            localizationKey: "Vlc",
            mainWindowClassName: null,
            requireVisibleWindow: true,
            processNames: new[] { "vlc" },
            matchesSourceLower: IsVlcLower,
            isPlaceholderWindowTitle: title => title.Equals("VLC media player", StringComparison.OrdinalIgnoreCase));

        public static readonly MediaPlayerDescriptor YouTubeMusicDesktop = new(
            application: MediaApplication.YouTubeMusicDesktop,
            localizationKey: "YouTubeMusicDesktop",
            mainWindowClassName: null,
            requireVisibleWindow: true,
            processNames: new[] { "YouTube Music", "YouTube Music Desktop App", "ytmdesktop", "pear-desktop" },
            matchesSourceLower: IsYouTubeMusicDesktopLower,
            isPlaceholderWindowTitle: title =>
                title.Equals("YouTube Music", StringComparison.OrdinalIgnoreCase) ||
                title.Equals("YouTube Music Desktop App", StringComparison.OrdinalIgnoreCase));

        public static readonly IReadOnlyList<MediaPlayerDescriptor> ManagedPlayers = new[]
        {
            Spotify,
            YandexMusic,
            AppleMusic,
            Aimp,
            Foobar2000,
            Vlc,
            YouTubeMusicDesktop
        };

        private static volatile string _customProcessName = "";

        public static string CustomProcessName
        {
            get => _customProcessName;
            set => _customProcessName = NormalizeCustomProcessName(value);
        }

        public static string NormalizeCustomProcessName(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            string trimmed = raw.Trim().Trim('"', '\'');
            if (string.IsNullOrWhiteSpace(trimmed))
                return string.Empty;

            try
            {
                if (trimmed.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }) >= 0)
                    trimmed = Path.GetFileNameWithoutExtension(trimmed);
                else if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    trimmed = trimmed[..^4];
            }
            catch
            {
                return string.Empty;
            }

            return trimmed.Trim();
        }

        public static MediaPlayerDescriptor CreateCustomDescriptor(string? customProcessName = null)
        {
            string normalized = NormalizeCustomProcessName(customProcessName ?? _customProcessName);
            string[] processNames = string.IsNullOrEmpty(normalized) ? Array.Empty<string>() : new[] { normalized };
            string lowerTarget = normalized.ToLowerInvariant();

            return new MediaPlayerDescriptor(
                application: MediaApplication.Custom,
                localizationKey: "CustomPlayer",
                mainWindowClassName: null,
                requireVisibleWindow: false,
                processNames: processNames,
                matchesSourceLower: lower => !string.IsNullOrEmpty(lowerTarget) && lower.Contains(lowerTarget),
                isPlaceholderWindowTitle: title =>
                    !string.IsNullOrEmpty(normalized) && title.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        }

        public static readonly string[] BrowserExactNames =
        {
            "chrome", "msedge", "firefox", "browser", "opera", "brave", "vivaldi", "iexplore", "yandexbrowser", "yandex"
        };

        public static readonly string[] BrowserMarkers =
        {
            "chrome", "msedge", "edge", "firefox", "opera", "brave", "vivaldi", "browser", "yandex"
        };

        public static bool IsSpotifyLower(string lower)
        {
            return lower.Contains("spotify");
        }

        public static bool IsYandexMusicLower(string lower)
        {
            return lower.Contains("yandexmusic") ||
                   lower.Contains("yandex music") ||
                   lower.Contains("яндекс музыка") ||
                   lower.Contains("ямusic") ||
                   (lower.Contains("yandex") && (lower.Contains("music") || lower.Contains("музык")));
        }

        public static bool IsAppleMusicLower(string lower)
        {
            return lower.Contains("applemusic") ||
                   lower.Contains("apple music") ||
                   lower.Contains("appleinc.applemusicwin");
        }

        public static bool IsAimpLower(string lower) => lower.Contains("aimp");

        public static bool IsFoobar2000Lower(string lower) => lower.Contains("foobar2000") || lower.Contains("foobar");

        public static bool IsVlcLower(string lower) => lower.Contains("vlc");

        public static bool IsYouTubeMusicDesktopLower(string lower)
        {
            return lower.Contains("youtube music") ||
                   lower.Contains("ytmdesktop") ||
                   lower.Contains("th-ch.youtube-music") ||
                   lower.Contains("pear-desktop");
        }

        public static bool IsManagedPlayerLower(string lower)
        {
            foreach (MediaPlayerDescriptor player in ManagedPlayers)
            {
                if (player.MatchesSourceLower(lower))
                    return true;
            }
            return false;
        }

        public static bool IsBrowserLower(string lower)
        {
            if (IsManagedPlayerLower(lower))
                return false;

            if (lower.Contains("iexplore"))
                return true;

            foreach (string marker in BrowserMarkers)
            {
                if (lower.Contains(marker))
                    return true;
            }

            return false;
        }

        public static MediaPlayerDescriptor GetDescriptor(MediaApplication application, string? customProcessName = null)
        {
            if (application == MediaApplication.Custom)
                return CreateCustomDescriptor(customProcessName);

            foreach (MediaPlayerDescriptor player in ManagedPlayers)
            {
                if (player.Application == application)
                    return player;
            }
            return Spotify;
        }

        public static MediaApplication ToMediaApplication(ApplicationSelectionMode selectionMode) => selectionMode switch
        {
            ApplicationSelectionMode.YandexMusic => MediaApplication.YandexMusic,
            ApplicationSelectionMode.AppleMusic => MediaApplication.AppleMusic,
            ApplicationSelectionMode.Aimp => MediaApplication.Aimp,
            ApplicationSelectionMode.Foobar2000 => MediaApplication.Foobar2000,
            ApplicationSelectionMode.Vlc => MediaApplication.Vlc,
            ApplicationSelectionMode.YouTubeMusicDesktop => MediaApplication.YouTubeMusicDesktop,
            ApplicationSelectionMode.Custom => MediaApplication.Custom,
            _ => MediaApplication.Spotify
        };

        public static ApplicationSelectionMode ToSelectionMode(MediaApplication application) => application switch
        {
            MediaApplication.Spotify => ApplicationSelectionMode.Spotify,
            MediaApplication.YandexMusic => ApplicationSelectionMode.YandexMusic,
            MediaApplication.AppleMusic => ApplicationSelectionMode.AppleMusic,
            MediaApplication.Aimp => ApplicationSelectionMode.Aimp,
            MediaApplication.Foobar2000 => ApplicationSelectionMode.Foobar2000,
            MediaApplication.Vlc => ApplicationSelectionMode.Vlc,
            MediaApplication.YouTubeMusicDesktop => ApplicationSelectionMode.YouTubeMusicDesktop,
            MediaApplication.Custom => ApplicationSelectionMode.Custom,
            _ => ApplicationSelectionMode.Auto
        };

        public static bool TryGetDescriptor(ApplicationSelectionMode selectionMode, out MediaPlayerDescriptor? descriptor, string? customProcessName = null)
        {
            if (selectionMode == ApplicationSelectionMode.Auto)
            {
                descriptor = null;
                return false;
            }

            descriptor = GetDescriptor(ToMediaApplication(selectionMode), customProcessName);
            return true;
        }

        public static bool MatchesSelectionMode(string lower, ApplicationSelectionMode selectionMode, string? customProcessName = null)
        {
            if (selectionMode == ApplicationSelectionMode.Auto)
            {
                if (IsManagedPlayerLower(lower))
                    return true;

                string custom = NormalizeCustomProcessName(customProcessName ?? _customProcessName);
                return !string.IsNullOrEmpty(custom) && CreateCustomDescriptor(custom).MatchesSourceLower(lower);
            }

            return TryGetDescriptor(selectionMode, out MediaPlayerDescriptor? descriptor, customProcessName) &&
                   descriptor!.MatchesSourceLower(lower);
        }
    }
}
