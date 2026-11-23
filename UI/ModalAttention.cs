using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MusicMediaKeys
{
    internal static class ModalAttention
    {
        private const int WM_SETCURSOR = 0x0020;
        private const int HTERROR = -2;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MBUTTONDOWN = 0x0207;

        private static readonly TimeSpan BlinkDuration = TimeSpan.FromMilliseconds(80);

        [DllImport("user32.dll")]
        private static extern IntPtr GetLastActivePopup(IntPtr hWnd);

        public static void Attach(Window owner)
        {
            owner.SourceInitialized += (s, e) =>
                HwndSource.FromHwnd(new WindowInteropHelper(owner).Handle)?.AddHook(WndProc);
        }

        private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != WM_SETCURSOR)
                return IntPtr.Zero;

            long value = lParam.ToInt64();
            int hitTest = (short)(value & 0xFFFF);
            int mouseMessage = (int)((value >> 16) & 0xFFFF);

            if (hitTest == HTERROR && mouseMessage is WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_MBUTTONDOWN)
            {
                IntPtr dialog = GetLastActivePopup(hwnd);
                if (dialog != hwnd && HwndSource.FromHwnd(dialog)?.RootVisual is Window window)
                    Blink(window);
            }

            return IntPtr.Zero;
        }

        private static void Blink(Window window)
        {
            if (window.Content is not Border frame ||
                frame.TryFindResource("BorderDark") is not SolidColorBrush normal ||
                frame.TryFindResource("TealAccent") is not SolidColorBrush accent)
            {
                return;
            }

            var brush = new SolidColorBrush(normal.Color);
            frame.BorderBrush = brush;

            var blink = new ColorAnimation(accent.Color, BlinkDuration)
            {
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(3)
            };
            blink.Completed += (s, e) =>
            {
                if (frame.BorderBrush == brush)
                    frame.SetResourceReference(Border.BorderBrushProperty, "BorderDark");
            };
            brush.BeginAnimation(SolidColorBrush.ColorProperty, blink);
        }
    }
}
