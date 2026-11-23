using System;

namespace MusicMediaKeys
{
    internal static class WarningSound
    {
        private static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(400);
        private static long _lastPlayed = -1;

        public static void Play()
        {
            long now = Environment.TickCount64;
            if (_lastPlayed >= 0 && now - _lastPlayed < MinInterval.TotalMilliseconds)
                return;

            _lastPlayed = now;
            try
            {
                System.Media.SystemSounds.Exclamation.Play();
            }
            catch
            {
            }
        }
    }
}
