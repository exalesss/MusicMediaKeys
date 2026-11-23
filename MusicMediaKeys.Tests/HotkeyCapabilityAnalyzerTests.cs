using Xunit;

namespace MusicMediaKeys.Tests
{
    public class HotkeyCapabilityAnalyzerTests
    {
        [Fact]
        public void MediaKeyBinding_WithoutModifiers_IsValid()
        {
            var binding = new HotkeyBinding { Key = MediaKeyCodes.MediaPlayPause, Modifiers = 0 };

            Assert.True(HotkeyCapabilityAnalyzer.IsValidBinding(binding));
            Assert.True(HotkeyCapabilityAnalyzer.IsMediaKey(binding.Key));
        }

        [Fact]
        public void StandardKeyBinding_WithModifiers_IsValid()
        {
            var binding = new HotkeyBinding { Key = (int)Keys.F9, Modifiers = (uint)ModifierKeys.Control };

            Assert.True(HotkeyCapabilityAnalyzer.IsValidBinding(binding));
            Assert.False(HotkeyCapabilityAnalyzer.IsMediaKey(binding.Key));
        }

        [Fact]
        public void StandardKeyBinding_WithoutModifiers_IsInvalid()
        {
            var binding = new HotkeyBinding { Key = (int)Keys.J, Modifiers = 0 };

            Assert.False(HotkeyCapabilityAnalyzer.IsValidBinding(binding));
        }

        [Fact]
        public void NullOrEmptyBinding_IsInvalid()
        {
            Assert.False(HotkeyCapabilityAnalyzer.IsValidBinding(null));
            Assert.False(HotkeyCapabilityAnalyzer.IsValidBinding(new HotkeyBinding { Key = (int)Keys.None, Modifiers = 0 }));
        }

        [Theory]
        [InlineData(Keys.D0, "0")]
        [InlineData(Keys.D1, "1")]
        [InlineData(Keys.D9, "9")]
        [InlineData(Keys.NumPad0, "Num 0")]
        [InlineData(Keys.NumPad5, "Num 5")]
        [InlineData(Keys.Oemcomma, ",")]
        [InlineData(Keys.OemPeriod, ".")]
        [InlineData(Keys.OemMinus, "-")]
        [InlineData(Keys.Oemplus, "+")]
        [InlineData(Keys.A, "A")]
        [InlineData(Keys.Right, "→")]
        [InlineData(Keys.PageUp, "PgUp")]
        [InlineData(Keys.Space, "Space")]
        public void FormatKeyName_FormatsCorrectly(Keys key, string expected)
        {
            Assert.Equal(expected, HotkeyCapabilityAnalyzer.FormatKeyName(key));
        }

        [Fact]
        public void FormatBinding_JoinsModifiersInFixedOrder()
        {
            var binding = new HotkeyBinding { Key = (int)Keys.O, Modifiers = (uint)(ModifierKeys.Alt | ModifierKeys.Control) };

            Assert.Equal("Ctrl + Alt + O", HotkeyCapabilityAnalyzer.FormatBinding(binding));
        }

        [Fact]
        public void FormatBinding_ShowsFnComboForMediaKeys()
        {
            var binding = new HotkeyBinding { Key = MediaKeyCodes.MediaPlayPause, Modifiers = 0 };

            Assert.Equal(Localization.GetString("MediaPlayPauseCombo"), HotkeyCapabilityAnalyzer.FormatBinding(binding));
        }

        [Fact]
        public void FormatBinding_IncludesModifiersForMediaKeys()
        {
            var binding = new HotkeyBinding
            {
                Key = MediaKeyCodes.MediaPlayPause,
                Modifiers = (uint)(ModifierKeys.Control | ModifierKeys.Alt)
            };

            Assert.Equal("Ctrl + Alt + " + Localization.GetString("MediaPlayPauseCombo"), HotkeyCapabilityAnalyzer.FormatBinding(binding));
        }
    }
}
