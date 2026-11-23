using System.Collections.Generic;

namespace MusicMediaKeys
{
    public static class MediaTargetSelectionService
    {
        public static MediaApplication Resolve(
            ApplicationSelectionMode selectionMode,
            MediaApplication? foregroundApp,
            MediaApplication? lastActiveApp,
            bool spotifyRunning,
            bool yandexRunning)
        {
            var running = new List<MediaApplication>(2);
            if (spotifyRunning) running.Add(MediaApplication.Spotify);
            if (yandexRunning) running.Add(MediaApplication.YandexMusic);
            return Resolve(selectionMode, foregroundApp, lastActiveApp, running);
        }

        public static MediaApplication Resolve(
            ApplicationSelectionMode selectionMode,
            MediaApplication? foregroundApp,
            MediaApplication? lastActiveApp,
            IReadOnlyCollection<MediaApplication> runningApps)
        {
            if (selectionMode != ApplicationSelectionMode.Auto)
                return KnownMediaSources.ToMediaApplication(selectionMode);

            if (foregroundApp.HasValue)
                return foregroundApp.Value;

            if (lastActiveApp.HasValue)
                return lastActiveApp.Value;

            if (runningApps != null)
            {
                foreach (MediaApplication app in runningApps)
                {
                    if (app == MediaApplication.Spotify)
                        return MediaApplication.Spotify;
                }

                foreach (MediaApplication app in runningApps)
                {
                    return app;
                }
            }

            return MediaApplication.Spotify;
        }
    }
}
