using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MusicMediaKeys
{
    public class NonModalComboBox : ComboBox
    {
        private Window? _window;
        private bool _releasingMouse;
        private UIElement? _list;
        private bool _pressedInList;

        static NonModalComboBox()
        {
            EventManager.RegisterClassHandler(typeof(NonModalComboBox), Mouse.LostMouseCaptureEvent, new MouseEventHandler(OnLostMouseCapture));
        }

        protected override void OnGotMouseCapture(MouseEventArgs e)
        {
            base.OnGotMouseCapture(e);

            if (IsDropDownOpen)
                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(ReleaseMouse));
        }

        public override void OnApplyTemplate()
        {
            if (_list != null)
            {
                _list.PreviewMouseLeftButtonDown -= List_PreviewMouseLeftButtonDown;
                _list.PreviewMouseLeftButtonUp -= List_PreviewMouseLeftButtonUp;
            }

            base.OnApplyTemplate();

            _list = (GetTemplateChild("Popup") as Popup)?.Child;
            if (_list != null)
            {
                _list.PreviewMouseLeftButtonDown += List_PreviewMouseLeftButtonDown;
                _list.PreviewMouseLeftButtonUp += List_PreviewMouseLeftButtonUp;
            }
        }

        private void List_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _pressedInList = true;
        }

        private void List_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_pressedInList)
                e.Handled = true;
            _pressedInList = false;
        }

        protected override void OnDropDownOpened(EventArgs e)
        {
            base.OnDropDownOpened(e);
            _pressedInList = false;

            _window = Window.GetWindow(this);
            if (_window != null)
            {
                _window.PreviewMouseDown += Window_PreviewMouseDown;
                _window.Deactivated += Window_Deactivated;
            }
        }

        protected override void OnDropDownClosed(EventArgs e)
        {
            if (_window != null)
            {
                _window.PreviewMouseDown -= Window_PreviewMouseDown;
                _window.Deactivated -= Window_Deactivated;
                _window = null;
            }

            base.OnDropDownClosed(e);
        }

        private void ReleaseMouse()
        {
            if (!IsDropDownOpen || Mouse.Captured != this)
                return;

            _releasingMouse = true;
            try
            {
                Mouse.Capture(null);
            }
            finally
            {
                _releasingMouse = false;
            }
        }

        private static void OnLostMouseCapture(object sender, MouseEventArgs e)
        {
            if (((NonModalComboBox)sender)._releasingMouse)
                e.Handled = true;
        }

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsInside(e.OriginalSource as DependencyObject))
                IsDropDownOpen = false;
        }

        private void Window_Deactivated(object? sender, EventArgs e)
        {
            IsDropDownOpen = false;
        }

        private bool IsInside(DependencyObject? node) => IsUnder(node, this) || (_list != null && IsUnder(node, _list));

        private static bool IsUnder(DependencyObject? node, DependencyObject container)
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
    }
}
