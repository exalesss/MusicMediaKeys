using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MusicMediaKeys
{
    internal sealed class HotkeyRecorder
    {
        public static readonly IReadOnlyList<int> TrackPresets = new[]
        {
            MediaKeyCodes.MediaPrevTrack, MediaKeyCodes.MediaPlayPause, MediaKeyCodes.MediaNextTrack
        };

        public static readonly IReadOnlyList<int> AllPresets = new[]
        {
            MediaKeyCodes.MediaPrevTrack, MediaKeyCodes.MediaPlayPause, MediaKeyCodes.MediaNextTrack,
            MediaKeyCodes.VolumeDown, MediaKeyCodes.VolumeUp, MediaKeyCodes.VolumeMute
        };

        private static readonly Brush RecordingBackground = CreateFrozenBrush(Color.FromRgb(0, 75, 80));

        private readonly Window _window;
        private readonly Func<TextBox, HotkeyBinding?> _getBinding;
        private readonly Action<TextBox, HotkeyBinding?> _setBinding;
        private readonly IReadOnlyList<int> _presetKeys;
        private TextBox? _active;

        public event Action<bool>? RecordingChanged;

        public event Action<TextBox>? BindingChanged;

        public HotkeyRecorder(
            Window window,
            Func<TextBox, HotkeyBinding?> getBinding,
            Action<TextBox, HotkeyBinding?> setBinding,
            IReadOnlyList<int> presetKeys)
        {
            _window = window;
            _getBinding = getBinding;
            _setBinding = setBinding;
            _presetKeys = presetKeys;

            window.PreviewMouseDown += Window_PreviewMouseDown;
            window.Deactivated += (s, e) => Cancel();
        }

        public bool IsRecording(TextBox field) => _active == field;

        public void Attach(TextBox field)
        {
            field.GotFocus += Field_GotFocus;
            field.LostFocus += Field_LostFocus;
            field.PreviewKeyDown += Field_PreviewKeyDown;
            field.PreviewMouseRightButtonDown += Field_PreviewMouseRightButtonDown;
            field.PreviewMouseRightButtonUp += (s, e) => e.Handled = true;
            Show(field);
        }

        public void Show(TextBox field)
        {
            field.Text = HotkeyCapabilityAnalyzer.FormatBinding(_getBinding(field));
        }

        public void Cancel()
        {
            if (_active == null)
                return;

            Stop(_active);
            ReleaseFocus();
        }

        private void Field_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox field || _active == field)
                return;

            _active = field;
            field.Background = RecordingBackground;
            field.Foreground = Brushes.White;
            field.SetResourceReference(Control.BorderBrushProperty, "TealAccent");
            field.Text = Localization.GetString("PressKey");
            RecordingChanged?.Invoke(true);
        }

        private void Field_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox field && _active == field)
                Stop(field);
        }

        private void Field_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox field || _active != field)
                return;

            e.Handled = true;
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;

            if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
                Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            {
                return;
            }

            if (key == Key.Escape)
            {
                Cancel();
                return;
            }

            var binding = new HotkeyBinding
            {
                Key = KeyInterop.VirtualKeyFromKey(key),
                Modifiers = (uint)Keyboard.Modifiers
            };

            if (!HotkeyCapabilityAnalyzer.IsValidBinding(binding))
            {
                Cancel();
                CustomDialogWindow.Show(
                    _window,
                    Localization.GetString("CapabilityRequiresModifier"),
                    Localization.GetString("Warning"),
                    Localization.GetString("WhyButton"),
                    Localization.GetString("CapabilityRequiresModifierExplanation"));
                return;
            }

            _setBinding(field, binding);
            Stop(field);
            ReleaseFocus();
            BindingChanged?.Invoke(field);
        }

        private void Field_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not TextBox field)
                return;

            e.Handled = true;
            Cancel();
            DropDownMenu.Toggle(field, () => CreatePresetMenu(field));
        }

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_active != null && !IsInside(e.OriginalSource as DependencyObject, _active))
                Cancel();
        }

        internal ContextMenu CreatePresetMenu(TextBox field)
        {
            var menu = new ContextMenu();
            menu.Items.Add(CreateMenuItem(Localization.GetString("Clear"), () => Assign(field, null)));

            bool? previousWasVolume = null;
            foreach (int key in _presetKeys)
            {
                bool isVolume = key is MediaKeyCodes.VolumeDown or MediaKeyCodes.VolumeUp or MediaKeyCodes.VolumeMute;
                if (previousWasVolume != isVolume)
                    menu.Items.Add(new Separator());
                previousWasVolume = isVolume;

                int presetKey = key;
                menu.Items.Add(CreateMenuItem(
                    Localization.GetString(GetPresetLabelKey(presetKey)),
                    () => Assign(field, new HotkeyBinding { Key = presetKey, Modifiers = 0 })));
            }

            return menu;
        }

        private void Assign(TextBox field, HotkeyBinding? binding)
        {
            _setBinding(field, binding);
            Show(field);
            BindingChanged?.Invoke(field);
        }

        private void Stop(TextBox field)
        {
            _active = null;
            field.ClearValue(Control.BackgroundProperty);
            field.ClearValue(Control.ForegroundProperty);
            field.ClearValue(Control.BorderBrushProperty);
            Show(field);
            RecordingChanged?.Invoke(false);
        }

        private void ReleaseFocus()
        {
            Keyboard.ClearFocus();
            FocusManager.SetFocusedElement(_window, _window);
        }

        private static MenuItem CreateMenuItem(string header, Action onClick)
        {
            var item = new MenuItem { Header = header };
            item.Click += (s, e) => onClick();
            return item;
        }

        private static string GetPresetLabelKey(int key) => key switch
        {
            MediaKeyCodes.MediaPrevTrack => "MenuMediaPrev",
            MediaKeyCodes.MediaPlayPause => "MenuMediaPlayPause",
            MediaKeyCodes.MediaNextTrack => "MenuMediaNext",
            MediaKeyCodes.VolumeDown => "MenuVolDown",
            MediaKeyCodes.VolumeUp => "MenuVolUp",
            _ => "MenuVolMute"
        };

        private static bool IsInside(DependencyObject? node, DependencyObject container)
        {
            while (node != null)
            {
                if (node == container)
                    return true;

                node = node is Visual
                    ? VisualTreeHelper.GetParent(node)
                    : LogicalTreeHelper.GetParent(node);
            }

            return false;
        }

        private static Brush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
