namespace MusicMediaKeys
{
    public enum AppCommandHandling
    {
        Ignore,
        DispatchMediaCommand,
        ConsumeOnly
    }

    public static class WindowMessageRouting
    {
        public const int WmHotkey = 0x0312;
        public const int AppCommandVolumeMute = 8;
        public const int AppCommandVolumeDown = 9;
        public const int AppCommandVolumeUp = 10;
        public const int AppCommandMediaNextTrack = 11;
        public const int AppCommandMediaPreviousTrack = 12;
        public const int AppCommandMediaPlayPause = 14;

        public static MediaCommand? TryMapAppCommand(int appCommand)
        {
            return appCommand switch
            {
                AppCommandVolumeMute => MediaCommand.VolumeMute,
                AppCommandVolumeDown => MediaCommand.VolumeDown,
                AppCommandVolumeUp => MediaCommand.VolumeUp,
                AppCommandMediaNextTrack => MediaCommand.Next,
                AppCommandMediaPreviousTrack => MediaCommand.Previous,
                AppCommandMediaPlayPause => MediaCommand.PlayPause,
                _ => null
            };
        }

        public static AppCommandHandling DecideAppCommandHandling(MediaCommand? command, AppSettings? settings)
        {
            if (!command.HasValue)
                return AppCommandHandling.ConsumeOnly;

            if (settings != null && !settings.UseSystemMediaKeysOnly)
                return AppCommandHandling.DispatchMediaCommand;

            return AppCommandHandling.ConsumeOnly;
        }
    }
}
