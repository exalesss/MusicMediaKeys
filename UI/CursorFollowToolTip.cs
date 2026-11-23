using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace MusicMediaKeys
{
    public static class CursorFollowToolTip
    {
        private static Popup? _popup;
        private static TextBlock? _textBlock;
        private static Border? _border;
        private static FrameworkElement? _currentElement;
        private static Window? _ownerWindow;
        private static Window? _hoveredWindow;
        private static EventHandler? _ownerDeactivatedOrMovedHandler;
        private static EventHandler? _ownerClosedHandler;
        private static bool _isEnabled = true;

        public static bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                if (!value)
                {
                    Hide();
                }
            }
        }

        public static readonly DependencyProperty AlwaysShowProperty =
            DependencyProperty.RegisterAttached(
                "AlwaysShow",
                typeof(bool),
                typeof(CursorFollowToolTip),
                new PropertyMetadata(false));

        public static bool GetAlwaysShow(DependencyObject obj) => (bool)obj.GetValue(AlwaysShowProperty);
        public static void SetAlwaysShow(DependencyObject obj, bool value) => obj.SetValue(AlwaysShowProperty, value);

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.RegisterAttached(
                "Text",
                typeof(string),
                typeof(CursorFollowToolTip),
                new PropertyMetadata(null, OnTextChanged));

        public static string? GetText(DependencyObject obj) => (string?)obj.GetValue(TextProperty);
        public static void SetText(DependencyObject obj, string? value) => obj.SetValue(TextProperty, value);

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element)
            {
                element.MouseEnter -= Element_MouseEnter;
                element.MouseMove -= Element_MouseMove;
                element.MouseLeave -= Element_MouseLeave;
                element.PreviewMouseDown -= Element_PreviewMouseDown;

                if (!string.IsNullOrEmpty(e.NewValue as string))
                {
                    element.MouseEnter += Element_MouseEnter;
                    element.MouseMove += Element_MouseMove;
                    element.MouseLeave += Element_MouseLeave;
                    element.PreviewMouseDown += Element_PreviewMouseDown;
                }
            }
        }

        private static void DetachOwnerEvents(Window? window)
        {
            if (window == null) return;

            if (_ownerDeactivatedOrMovedHandler != null)
            {
                window.Deactivated -= _ownerDeactivatedOrMovedHandler;
                window.LocationChanged -= _ownerDeactivatedOrMovedHandler;
            }
            if (_ownerClosedHandler != null)
            {
                window.Closed -= _ownerClosedHandler;
            }
        }

        private static void EnsurePopupCreated(Window window)
        {
            if (_popup != null && _ownerWindow == window) return;

            DetachOwnerEvents(_ownerWindow);

            if (_popup != null)
            {
                _popup.IsOpen = false;
                _popup = null;
            }

            _ownerWindow = window;

            _textBlock = new TextBlock
            {
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 300,
                LineHeight = 16
            };

            _border = new Border
            {
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 7, 10, 7),
                Child = _textBlock
            };

            _popup = new Popup
            {
                AllowsTransparency = true,
                Placement = PlacementMode.Relative,
                PlacementTarget = window,
                IsHitTestVisible = false,
                Child = _border
            };

            _ownerDeactivatedOrMovedHandler = (s, e) => Hide();
            _ownerClosedHandler = (s, e) =>
            {
                if (s is Window closedWindow)
                {
                    DetachOwnerEvents(closedWindow);
                }

                if (!ReferenceEquals(s, _ownerWindow))
                    return;

                Hide();
                if (_popup != null)
                {
                    _popup.IsOpen = false;
                    _popup = null;
                }
                _ownerWindow = null;
            };

            window.Deactivated += _ownerDeactivatedOrMovedHandler;
            window.LocationChanged += _ownerDeactivatedOrMovedHandler;
            window.Closed += _ownerClosedHandler;
        }

        private static void ApplyTheme(Window window)
        {
            if (_border == null || _textBlock == null) return;

            try
            {
                if (window.TryFindResource("BgCard") is Brush bg)
                {
                    _border.Background = bg;
                }
                else
                {
                    _border.Background = new SolidColorBrush(Color.FromRgb(34, 40, 49));
                }

                if (window.TryFindResource("BorderDark") is Brush border)
                {
                    _border.BorderBrush = border;
                }
                else
                {
                    _border.BorderBrush = new SolidColorBrush(Color.FromRgb(57, 62, 70));
                }

                if (window.TryFindResource("TextWhite") is Brush text)
                {
                    _textBlock.Foreground = text;
                }
                else
                {
                    _textBlock.Foreground = new SolidColorBrush(Color.FromRgb(238, 238, 238));
                }
            }
            catch
            {
                _border.Background = new SolidColorBrush(Color.FromRgb(34, 40, 49));
                _border.BorderBrush = new SolidColorBrush(Color.FromRgb(57, 62, 70));
                _textBlock.Foreground = new SolidColorBrush(Color.FromRgb(238, 238, 238));
            }
        }

        private static void Element_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is not FrameworkElement element) return;
            if (!_isEnabled && !GetAlwaysShow(element)) return;
            string? text = GetText(element);
            if (string.IsNullOrWhiteSpace(text)) return;

            Window? window = Window.GetWindow(element);
            if (window == null) return;

            EnsurePopupCreated(window);
            ApplyTheme(window);

            if (_textBlock != null)
            {
                _textBlock.Text = text;
            }

            if (_popup != null)
            {
                _popup.PlacementTarget = window;
                Point mousePos = e.GetPosition(window);
                UpdatePosition(window, mousePos);
                _popup.IsOpen = true;
            }

            _currentElement = element;
            _hoveredWindow = window;
        }

        private static void Element_MouseMove(object sender, MouseEventArgs e)
        {
            if (_popup == null || !_popup.IsOpen || _currentElement != sender || _hoveredWindow == null)
                return;

            Point mousePos = e.GetPosition(_hoveredWindow);
            UpdatePosition(_hoveredWindow, mousePos);
        }

        private static void Element_MouseLeave(object sender, MouseEventArgs e)
        {
            Hide();
        }

        private static void Element_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Hide();
        }

        public static void Hide()
        {
            if (_popup != null && _popup.IsOpen)
            {
                _popup.IsOpen = false;
            }
            _currentElement = null;
            _hoveredWindow = null;
        }

        private static void UpdatePosition(Window window, Point mousePos)
        {
            if (_popup == null) return;

            _popup.HorizontalOffset = mousePos.X + 14;
            _popup.VerticalOffset = mousePos.Y + 20;
        }
    }
}
