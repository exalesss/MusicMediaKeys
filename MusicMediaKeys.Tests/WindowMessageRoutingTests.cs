using Xunit;

namespace MusicMediaKeys.Tests
{
    public class WindowMessageRoutingTests
    {
        [Theory]
        [InlineData(WindowMessageRouting.AppCommandVolumeMute, MediaCommand.VolumeMute)]
        [InlineData(WindowMessageRouting.AppCommandVolumeDown, MediaCommand.VolumeDown)]
        [InlineData(WindowMessageRouting.AppCommandVolumeUp, MediaCommand.VolumeUp)]
        [InlineData(WindowMessageRouting.AppCommandMediaNextTrack, MediaCommand.Next)]
        [InlineData(WindowMessageRouting.AppCommandMediaPreviousTrack, MediaCommand.Previous)]
        [InlineData(WindowMessageRouting.AppCommandMediaPlayPause, MediaCommand.PlayPause)]
        public void TryMapAppCommand_MapsKnownCommands(int appCommand, MediaCommand expected)
        {
            MediaCommand? actual = WindowMessageRouting.TryMapAppCommand(appCommand);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void TryMapAppCommand_ReturnsNullForUnknownCommand()
        {
            Assert.Null(WindowMessageRouting.TryMapAppCommand(999));
        }

        [Fact]
        public void DecideAppCommandHandling_ReturnsDispatchMediaCommand_WhenEnabled()
        {
            AppSettings settings = new AppSettings { UseSystemMediaKeysOnly = false };

            var result = WindowMessageRouting.DecideAppCommandHandling(MediaCommand.VolumeUp, settings);

            Assert.Equal(AppCommandHandling.DispatchMediaCommand, result);
        }

        [Fact]
        public void DecideAppCommandHandling_ReturnsConsumeOnly_WhenSystemMediaKeysOnly()
        {
            AppSettings settings = new AppSettings { UseSystemMediaKeysOnly = true };

            var result = WindowMessageRouting.DecideAppCommandHandling(MediaCommand.VolumeUp, settings);

            Assert.Equal(AppCommandHandling.ConsumeOnly, result);
        }
    }
}
