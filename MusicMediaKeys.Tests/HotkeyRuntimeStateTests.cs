using Xunit;

namespace MusicMediaKeys.Tests
{
    public class HotkeyRuntimeStateTests
    {
        [Fact]
        public void SetAndGetRegisteredHotkeyId_RoundTripsValue()
        {
            HotkeyRuntimeState state = new();

            state.SetRegisteredHotkeyId(AppActions.PlayPause, 1234);

            Assert.Equal(1234, state.GetRegisteredHotkeyId(AppActions.PlayPause));
        }

        [Fact]
        public void TrackRegisteredHotkey_SkipsMissingIds()
        {
            HotkeyRuntimeState state = new();

            state.TrackRegisteredHotkey(-1, AppActions.PlayPause);

            Assert.Empty(state.RegisteredHotkeyActions);
        }

        [Fact]
        public void Reset_ClearsRuntimeCollections()
        {
            HotkeyRuntimeState state = new();
            state.SetRegisteredHotkeyId(AppActions.PlayPause, 1234);
            state.TrackRegisteredHotkey(1234, AppActions.PlayPause);

            state.Reset(null);

            Assert.Equal(-1, state.GetRegisteredHotkeyId(AppActions.PlayPause));
            Assert.Empty(state.RegisteredHotkeyActions);
        }
    }
}
