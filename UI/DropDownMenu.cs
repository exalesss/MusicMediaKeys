using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace MusicMediaKeys
{
    internal static class DropDownMenu
    {
        private static readonly TimeSpan ReopenGuard = TimeSpan.FromMilliseconds(250);

        private static readonly DependencyProperty MenuProperty =
            DependencyProperty.RegisterAttached("Menu", typeof(ContextMenu), typeof(DropDownMenu));

        private static readonly DependencyProperty ClosedAtProperty =
            DependencyProperty.RegisterAttached("ClosedAt", typeof(long), typeof(DropDownMenu), new PropertyMetadata(0L));

        public static void Toggle(FrameworkElement target, Func<ContextMenu> createMenu)
        {
            if (target.GetValue(MenuProperty) is ContextMenu { IsOpen: true } openMenu)
            {
                openMenu.IsOpen = false;
                return;
            }

            if (Environment.TickCount64 - (long)target.GetValue(ClosedAtProperty) < ReopenGuard.TotalMilliseconds)
                return;

            ContextMenu menu = createMenu();

            bool pressedInMenu = false;
            menu.PreviewMouseDown += (s, e) => pressedInMenu = true;
            menu.PreviewMouseUp += (s, e) =>
            {
                if (!pressedInMenu)
                    e.Handled = true;
                pressedInMenu = false;
            };

            menu.PlacementTarget = target;
            menu.Placement = PlacementMode.Bottom;
            menu.Closed += (s, e) =>
            {
                target.SetValue(ClosedAtProperty, Environment.TickCount64);
                target.ClearValue(MenuProperty);
            };
            target.SetValue(MenuProperty, menu);
            menu.IsOpen = true;
        }
    }
}
