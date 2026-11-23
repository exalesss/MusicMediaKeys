using System;

namespace MusicMediaKeys
{
    public readonly record struct MediaForegroundContext(
        IntPtr ForegroundWindow,
        IntPtr RootWindow,
        int RootProcessId,
        int ForegroundProcessId)
    {
        public bool IsOwnProcess =>
            RootProcessId == MediaPlayerProviders.OurProcessId ||
            ForegroundProcessId == MediaPlayerProviders.OurProcessId;

        public bool IsBrowser =>
            MediaPlayerProviders.IsBrowserProcess(RootProcessId) ||
            MediaPlayerProviders.IsBrowserProcess(ForegroundProcessId);

        public bool IsDesktopPlayer =>
            MediaPlayerProviders.IsDesktopPlayerProcess(RootProcessId) ||
            MediaPlayerProviders.IsDesktopPlayerProcess(ForegroundProcessId);

        public IntPtr PreferredDesktopPlayerWindow => MediaPlayerProviders.IsDesktopPlayerProcess(RootProcessId)
            ? RootWindow
            : ForegroundWindow;
    }
}
