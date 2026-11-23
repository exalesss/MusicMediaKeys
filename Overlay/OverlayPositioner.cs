using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace MusicMediaKeys
{
    internal sealed record MonitorDescriptor(string DeviceName, string DisplayLabel, bool IsPrimary, IntPtr Handle, int Width, int Height);

    internal static class OverlayPositioner
    {
        public static void ApplyNonActivatingStyles(Window window)
        {
            try
            {
                var helper = new WindowInteropHelper(window);
                if (helper.Handle != IntPtr.Zero)
                {
                    int exStyle = NativeMethods.GetWindowLong(helper.Handle, NativeMethods.GWL_EXSTYLE);
                    NativeMethods.SetWindowLong(
                        helper.Handle,
                        NativeMethods.GWL_EXSTYLE,
                        exStyle | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW);
                }
            }
            catch (Exception ex)
            {
                EventLogService.Log("Overlay", "Failed to make the overlay non-activating: " + ex.Message);
            }
        }

        public static void ShowNoActivate(IntPtr handle)
        {
            if (handle != IntPtr.Zero)
            {
                NativeMethods.ShowWindow(handle, NativeMethods.SW_SHOWNOACTIVATE);
            }
        }

        public static bool IsFullScreenAppRunning()
        {
            try
            {
                return NativeMethods.SHQueryUserNotificationState(out int state) == 0 &&
                       state is NativeMethods.QUNS_BUSY
                             or NativeMethods.QUNS_RUNNING_D3D_FULL_SCREEN
                             or NativeMethods.QUNS_PRESENTATION_MODE;
            }
            catch (Exception ex)
            {
                EventLogService.Log("Overlay", "Failed to check for full-screen apps: " + ex.Message);
                return false;
            }
        }

        public static IReadOnlyList<MonitorDescriptor> GetAvailableMonitors()
        {
            var monitors = new List<MonitorDescriptor>();
            try
            {
                int index = 0;
                NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdc, ref NativeMethods.RECT rect, IntPtr data) =>
                {
                    var info = new NativeMethods.MONITORINFOEX
                    {
                        cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>()
                    };
                    if (NativeMethods.GetMonitorInfo(hMonitor, ref info))
                    {
                        index++;
                        bool isPrimary = (info.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0;
                        int width = Math.Abs(info.rcMonitor.Right - info.rcMonitor.Left);
                        int height = Math.Abs(info.rcMonitor.Bottom - info.rcMonitor.Top);
                        string primarySuffix = isPrimary ? $" ({Localization.GetString("OverlayMonitorPrimaryBadge")})" : "";
                        string label = string.Format(
                            Localization.GetString("OverlayMonitorSpecificFormat"),
                            index,
                            width,
                            height,
                            primarySuffix);
                        monitors.Add(new MonitorDescriptor(info.szDevice ?? $"DISPLAY{index}", label, isPrimary, hMonitor, width, height));
                    }
                    return true;
                }, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                EventLogService.Log("Overlay", "Failed to enumerate monitors: " + ex.Message);
            }

            return monitors;
        }

        public static void PositionOnActiveMonitor(Window window, double dipWidth, double dipHeight, OverlayPosition position)
        {
            PositionOverlay(window, dipWidth, dipHeight, position, OverlayMonitorMode.ActiveMonitor, null);
        }

        public static void PositionOverlay(
            Window window,
            double dipWidth,
            double dipHeight,
            OverlayPosition position,
            OverlayMonitorMode monitorMode = OverlayMonitorMode.ActiveMonitor,
            string? monitorDeviceName = null)
        {
            try
            {
                var (column, row) = OverlayPositions.GetCell(position);

                IntPtr hMonitor = ResolveTargetMonitor(monitorMode, monitorDeviceName);

                if (hMonitor != IntPtr.Zero)
                {
                    var mi = new NativeMethods.MONITORINFO
                    {
                        cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>()
                    };
                    if (NativeMethods.GetMonitorInfo(hMonitor, ref mi))
                    {
                        var (targetDpiScaleX, targetDpiScaleY) = GetMonitorDpiScale(window, hMonitor);

                        IntPtr handle = new WindowInteropHelper(window).Handle;
                        if (handle != IntPtr.Zero)
                        {
                            int physWidth = (int)Math.Round(dipWidth * targetDpiScaleX);
                            int physHeight = (int)Math.Round(dipHeight * targetDpiScaleY);
                            int physGapX = (int)Math.Round(20 * targetDpiScaleX);
                            int physGapY = (int)Math.Round(20 * targetDpiScaleY);

                            int physLeft = column switch
                            {
                                0 => mi.rcWork.Left + physGapX,
                                1 => mi.rcWork.Left + (mi.rcWork.Right - mi.rcWork.Left - physWidth) / 2,
                                _ => mi.rcWork.Right - physWidth - physGapX
                            };
                            int physTop = row switch
                            {
                                0 => mi.rcWork.Top + physGapY,
                                1 => mi.rcWork.Top + (mi.rcWork.Bottom - mi.rcWork.Top - physHeight) / 2,
                                _ => mi.rcWork.Bottom - physHeight - physGapY
                            };

                            NativeMethods.SetWindowPos(
                                handle,
                                IntPtr.Zero,
                                physLeft,
                                physTop,
                                physWidth,
                                physHeight,
                                NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
                            return;
                        }

                        var windowDpi = VisualTreeHelper.GetDpi(window);
                        double leftDip = mi.rcWork.Left / windowDpi.DpiScaleX;
                        double topDip = mi.rcWork.Top / windowDpi.DpiScaleY;
                        double rightDip = mi.rcWork.Right / windowDpi.DpiScaleX;
                        double bottomDip = mi.rcWork.Bottom / windowDpi.DpiScaleY;

                        PlaceInArea(window, leftDip, topDip, rightDip, bottomDip, column, row);
                        return;
                    }
                }

                Rect workArea = SystemParameters.WorkArea;
                PlaceInArea(window, workArea.Left, workArea.Top, workArea.Right, workArea.Bottom, column, row);
            }
            catch (Exception ex)
            {
                EventLogService.Log("Overlay", "Failed to position the overlay: " + ex.Message);
            }
        }

        private static IntPtr ResolveTargetMonitor(OverlayMonitorMode monitorMode, string? monitorDeviceName)
        {
            if (monitorMode == OverlayMonitorMode.PrimaryMonitor)
            {
                foreach (MonitorDescriptor monitor in GetAvailableMonitors())
                {
                    if (monitor.IsPrimary && monitor.Handle != IntPtr.Zero)
                        return monitor.Handle;
                }
            }
            else if (monitorMode == OverlayMonitorMode.SpecificMonitor && !string.IsNullOrWhiteSpace(monitorDeviceName))
            {
                foreach (MonitorDescriptor monitor in GetAvailableMonitors())
                {
                    if (string.Equals(monitor.DeviceName, monitorDeviceName, StringComparison.OrdinalIgnoreCase) &&
                        monitor.Handle != IntPtr.Zero)
                    {
                        return monitor.Handle;
                    }
                }
            }

            IntPtr hMonitor = IntPtr.Zero;
            if (NativeMethods.GetCursorPos(out NativeMethods.POINT pt))
            {
                hMonitor = NativeMethods.MonitorFromPoint(pt, NativeMethods.MONITOR_DEFAULTTONEAREST);
            }

            if (hMonitor == IntPtr.Zero)
            {
                IntPtr fg = NativeMethods.GetForegroundWindow();
                if (fg != IntPtr.Zero)
                {
                    hMonitor = NativeMethods.MonitorFromWindow(fg, NativeMethods.MONITOR_DEFAULTTONEAREST);
                }
            }

            return hMonitor;
        }

        private static (double ScaleX, double ScaleY) GetMonitorDpiScale(Window window, IntPtr hMonitor)
        {
            try
            {
                if (NativeMethods.GetDpiForMonitor(
                        hMonitor,
                        NativeMethods.MonitorDpiType.MDT_EFFECTIVE_DPI,
                        out uint dpiX,
                        out uint dpiY) == 0 && dpiX > 0 && dpiY > 0)
                {
                    return (dpiX / 96.0, dpiY / 96.0);
                }
            }
            catch
            {
            }

            var dpi = VisualTreeHelper.GetDpi(window);
            return (dpi.DpiScaleX, dpi.DpiScaleY);
        }

        private static void PlaceInArea(Window window, double left, double top, double right, double bottom, int column, int row)
        {
            const double edgeGap = 20;
            window.Left = column switch
            {
                0 => left + edgeGap,
                1 => left + (right - left - window.Width) / 2,
                _ => right - window.Width - edgeGap
            };
            window.Top = row switch
            {
                0 => top + edgeGap,
                1 => top + (bottom - top - window.Height) / 2,
                _ => bottom - window.Height - edgeGap
            };
        }
    }
}
