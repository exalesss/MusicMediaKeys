using System;

namespace MusicMediaKeys
{
    internal static class MediaSessionSourceClassifier
    {
        public static string Describe(string? sourceAppUserModelId, string? customProcessName = null)
        {
            if (string.IsNullOrWhiteSpace(sourceAppUserModelId))
                return Localization.GetString("DiagnosticsNoneValue");

            string source = sourceAppUserModelId.Trim();
            string lower = source.ToLowerInvariant();

            foreach (MediaPlayerDescriptor player in KnownMediaSources.ManagedPlayers)
            {
                if (player.MatchesSourceLower(lower))
                    return Localization.GetString(player.LocalizationKey);
            }

            string custom = KnownMediaSources.NormalizeCustomProcessName(customProcessName ?? KnownMediaSources.CustomProcessName);
            if (!string.IsNullOrEmpty(custom) && KnownMediaSources.CreateCustomDescriptor(custom).MatchesSourceLower(lower))
                return custom;

            if (KnownMediaSources.IsBrowserLower(lower))
                return Localization.GetString("WindowKindBrowser");

            return source;
        }

        public static bool IsBrowser(string? sourceAppUserModelId)
        {
            if (string.IsNullOrWhiteSpace(sourceAppUserModelId))
                return false;

            return KnownMediaSources.IsBrowserLower(sourceAppUserModelId.ToLowerInvariant());
        }

        public static bool IsTargetPlayer(string? sourceAppUserModelId, ApplicationSelectionMode selectionMode, string? customProcessName = null)
        {
            if (string.IsNullOrWhiteSpace(sourceAppUserModelId))
                return false;

            return KnownMediaSources.MatchesSelectionMode(sourceAppUserModelId.ToLowerInvariant(), selectionMode, customProcessName);
        }
    }
}
