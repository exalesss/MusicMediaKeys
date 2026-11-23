using System.Text;

namespace MusicMediaKeys
{
    public static class HotkeyCapabilityAnalyzer
    {
        public static bool IsValidBinding(HotkeyBinding? binding)
        {
            if (binding == null || binding.Key == (int)Keys.None)
                return false;

            return IsMediaKey(binding.Key) || binding.Modifiers != 0;
        }

        public static bool IsMediaKey(int key)
        {
            return key == MediaKeyCodes.MediaPrevTrack ||
                   key == MediaKeyCodes.MediaNextTrack ||
                   key == MediaKeyCodes.MediaPlayPause ||
                   key == MediaKeyCodes.VolumeDown ||
                   key == MediaKeyCodes.VolumeUp ||
                   key == MediaKeyCodes.VolumeMute;
        }

        public static string FormatBinding(HotkeyBinding? binding)
        {
            if (binding == null || binding.Key == (int)Keys.None)
                return Localization.GetString("NotAssigned");

            if (IsMediaKey(binding.Key))
            {
                string mediaLabel = binding.Key switch
                {
                    MediaKeyCodes.MediaPrevTrack => Localization.GetString("MediaPrevCombo"),
                    MediaKeyCodes.MediaPlayPause => Localization.GetString("MediaPlayPauseCombo"),
                    MediaKeyCodes.MediaNextTrack => Localization.GetString("MediaNextCombo"),
                    MediaKeyCodes.VolumeDown => Localization.GetString("VolumeDownCombo"),
                    MediaKeyCodes.VolumeUp => Localization.GetString("VolumeUpCombo"),
                    _ => Localization.GetString("VolumeMuteCombo")
                };

                if (binding.Modifiers == 0)
                    return mediaLabel;

                var mediaSb = new StringBuilder();
                ModifierKeys mediaMods = binding.GetModifiers();
                if (mediaMods.HasFlag(ModifierKeys.Control)) mediaSb.Append("Ctrl + ");
                if (mediaMods.HasFlag(ModifierKeys.Alt)) mediaSb.Append("Alt + ");
                if (mediaMods.HasFlag(ModifierKeys.Shift)) mediaSb.Append("Shift + ");
                if (mediaMods.HasFlag(ModifierKeys.Win)) mediaSb.Append("Win + ");
                mediaSb.Append(mediaLabel);
                return mediaSb.ToString();
            }

            var sb = new StringBuilder();
            ModifierKeys modifiers = binding.GetModifiers();
            if (modifiers.HasFlag(ModifierKeys.Control)) sb.Append("Ctrl + ");
            if (modifiers.HasFlag(ModifierKeys.Alt)) sb.Append("Alt + ");
            if (modifiers.HasFlag(ModifierKeys.Shift)) sb.Append("Shift + ");
            if (modifiers.HasFlag(ModifierKeys.Win)) sb.Append("Win + ");
            sb.Append(FormatKeyName(binding.GetKey()));
            return sb.ToString();
        }

        public static string FormatKeyName(Keys key)
        {
            if (key >= Keys.D0 && key <= Keys.D9)
                return ((int)key - (int)Keys.D0).ToString();
            if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
                return "Num " + ((int)key - (int)Keys.NumPad0);
            return key switch
            {
                Keys.Left => "←",
                Keys.Right => "→",
                Keys.Up => "↑",
                Keys.Down => "↓",
                Keys.PageUp => "PgUp",
                Keys.PageDown => "PgDn",
                Keys.Return => "Enter",
                Keys.Escape => "Esc",
                Keys.Back => "Backspace",
                Keys.Delete => "Del",
                Keys.Insert => "Ins",
                Keys.CapsLock => "Caps Lock",
                Keys.PrintScreen => "PrtSc",
                Keys.Apps => "Menu",
                Keys.Multiply => "Num *",
                Keys.Add => "Num +",
                Keys.Subtract => "Num -",
                Keys.Decimal => "Num .",
                Keys.Divide => "Num /",
                Keys.Oemcomma => ",",
                Keys.OemPeriod => ".",
                Keys.OemMinus => "-",
                Keys.Oemplus => "+",
                Keys.OemQuestion => "/",
                Keys.Oemtilde => "~",
                Keys.Oem1 => ";",
                Keys.OemOpenBrackets => "[",
                Keys.OemCloseBrackets => "]",
                Keys.OemQuotes => "'",
                Keys.OemBackslash => "\\",
                _ => key.ToString()
            };
        }
    }
}
