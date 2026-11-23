using System;
using System.Drawing;
using System.IO;
using System.Reflection;

namespace MusicMediaKeys
{
    internal sealed class TrayIconAssets : IDisposable
    {
        public Icon ActiveIcon { get; init; } = SystemIcons.Application;

        public Icon ErrorIcon { get; init; } = SystemIcons.Warning;

        public Icon InitialIcon { get; init; } = SystemIcons.Application;

        public bool OwnsActiveIcon { get; init; }

        public bool OwnsErrorIcon { get; init; }

        public void Dispose()
        {
            if (OwnsActiveIcon)
                ActiveIcon.Dispose();
            if (OwnsErrorIcon && !ReferenceEquals(ErrorIcon, ActiveIcon))
                ErrorIcon.Dispose();
        }
    }

    internal static class TrayIconService
    {
        public static TrayIconAssets LoadAssets()
        {
            Icon? appIcon = LoadIconFromResource("MusicMediaKeys.icon.ico") ?? LoadIconFromFile("icon.ico");
            Icon? appIconError = LoadIconFromResource("MusicMediaKeys.icon_error.ico") ?? LoadIconFromFile("icon error.ico");
            Icon activeIcon = appIcon ?? SystemIcons.Application;
            Icon errorIcon = appIconError ?? SystemIcons.Warning;

            return new TrayIconAssets
            {
                ActiveIcon = activeIcon,
                ErrorIcon = errorIcon,
                InitialIcon = errorIcon,
                OwnsActiveIcon = appIcon != null,
                OwnsErrorIcon = appIconError != null
            };
        }

        public static NativeTrayIcon CreateNativeTrayIcon(IntPtr hwnd, Icon icon, Action rightClickHandler, Action doubleClickHandler, Action? leftClickHandler = null)
        {
            var trayIcon = new NativeTrayIcon(hwnd, icon, Localization.GetString("AppName"));
            if (leftClickHandler != null)
                trayIcon.LeftClick += leftClickHandler;
            trayIcon.RightClick += rightClickHandler;
            trayIcon.DoubleClick += doubleClickHandler;
            return trayIcon;
        }

        private static System.Windows.Media.ImageSource? _cachedTitleBarIcon;

        public static System.Windows.Media.ImageSource? LoadTitleBarIcon()
        {
            if (_cachedTitleBarIcon != null)
                return _cachedTitleBarIcon;

            try
            {
                using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MusicMediaKeys.icon.ico");
                if (stream == null)
                    return null;

                var decoder = new System.Windows.Media.Imaging.IconBitmapDecoder(
                    stream,
                    System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
                    System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);

                System.Windows.Media.Imaging.BitmapFrame best = decoder.Frames[0];
                foreach (var frame in decoder.Frames)
                {
                    if (Math.Abs(frame.PixelWidth - 16) < Math.Abs(best.PixelWidth - 16))
                        best = frame;
                }
                if (best.CanFreeze)
                    best.Freeze();
                _cachedTitleBarIcon = best;
                return best;
            }
            catch (Exception ex)
            {
                EventLogService.Log("TrayIcon", "Failed to load the title bar icon: " + ex.Message);
                return null;
            }
        }

        private static Icon? LoadIconFromResource(string resourceName)
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                using Stream? stream = asm.GetManifestResourceStream(resourceName);
                if (stream != null)
                    return new Icon(stream);
            }
            catch (Exception ex)
            {
                EventLogService.Log("TrayIcon", $"Failed to load {resourceName}: {ex.Message}");
            }

            return null;
        }

        private static Icon? LoadIconFromFile(string fileName)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
            if (!File.Exists(path))
                return null;

            try
            {
                return new Icon(path);
            }
            catch
            {
                return null;
            }
        }
    }
}
