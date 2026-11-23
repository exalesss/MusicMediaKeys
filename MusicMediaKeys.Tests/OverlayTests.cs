using System.Globalization;
using Xunit;

namespace MusicMediaKeys.Tests
{
    public class OverlayTests
    {
        [Theory]
        [InlineData(100.0, 0.0, 0.0, 1.0, 0.0)]
        [InlineData(100.0, 0.01, 0.0, 1.0, 1.0)]
        [InlineData(100.0, 0.5, 0.0, 1.0, 50.0)]
        [InlineData(100.0, 1.0, 0.0, 1.0, 100.0)]
        [InlineData(200.0, 226.0, 0.0, 226.0, 200.0)]
        public void SliderFillWidthConverter_ScalesUniformlyFromZeroToFullWidth(
            double width, double value, double min, double max, double expected)
        {
            var converter = new SliderFillWidthConverter();
            object result = converter.Convert(new object[] { width, value, min, max }, typeof(double), null!, CultureInfo.InvariantCulture);

            Assert.Equal(expected, (double)result, precision: 3);
        }

        [Fact]
        public void EvaluateTrackChanged_DistinguishesInitialArtworkOnlyAndNewTrack()
        {
            string? lastKey = null;

            bool first = OverlayMediaSessionTracker.EvaluateTrackChanged(ref lastKey, "Song A", "Artist 1", isInitialUpdate: true);
            Assert.False(first);

            bool sameTrackArtworkUpdate = OverlayMediaSessionTracker.EvaluateTrackChanged(ref lastKey, "Song A", "Artist 1", isInitialUpdate: false);
            Assert.False(sameTrackArtworkUpdate);

            bool nextTrack = OverlayMediaSessionTracker.EvaluateTrackChanged(ref lastKey, "Song B", "Artist 1", isInitialUpdate: false);
            Assert.True(nextTrack);
        }

        [Fact]
        public void GetAvailableMonitors_DoesNotThrow()
        {
            var monitors = OverlayPositioner.GetAvailableMonitors();
            Assert.NotNull(monitors);
        }

        [Fact]
        public void AppSettings_OverlayDefaultsArePreserved()
        {
            var settings = new AppSettings();
            Assert.True(settings.ShowOverlayOnTrackChange);
            Assert.Equal(OverlayMonitorMode.ActiveMonitor, settings.OverlayMonitorMode);
            Assert.Equal("", settings.OverlayMonitorDeviceName);
        }
    }
}
