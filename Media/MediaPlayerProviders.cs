using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace MusicMediaKeys
{
    internal static class MediaPlayerProviders
    {
        private const uint GA_ROOT = 2;

        public static readonly int OurProcessId = Environment.ProcessId;

        [DllImport("user32.dll", EntryPoint = "FindWindowW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindowNative(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsChild(IntPtr hwndParent, IntPtr hwndChild);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags, StringBuilder lpExeName, ref int lpdwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        public static IntPtr FindWindow(MediaApplication application)
        {
            return FindWindow(KnownMediaSources.GetDescriptor(application));
        }

        public static IntPtr FindWindow(MediaPlayerDescriptor descriptor)
        {
            if (string.IsNullOrEmpty(descriptor.MainWindowClassName) && descriptor.ProcessNames.Length == 0)
                return IntPtr.Zero;

            if (!string.IsNullOrEmpty(descriptor.MainWindowClassName))
            {
                IntPtr classHwnd = FindWindowNative(descriptor.MainWindowClassName, null);
                if (IsWindowValid(classHwnd))
                    return classHwnd;
            }
            return FindWindowByOwnerProcessName(descriptor.ProcessNames, descriptor.RequireVisibleWindow);
        }

        public static string GetApplicationDisplayName(MediaApplication application)
        {
            if (application == MediaApplication.Custom && !string.IsNullOrWhiteSpace(KnownMediaSources.CustomProcessName))
                return KnownMediaSources.CustomProcessName;

            return Localization.GetString(KnownMediaSources.GetDescriptor(application).LocalizationKey);
        }

        public static bool IsWindowValid(IntPtr hwnd) => hwnd != IntPtr.Zero && IsWindow(hwnd);

        public static bool TryFocusPlayerWindow(IntPtr hwnd)
        {
            if (!IsWindowValid(hwnd))
                return false;

            if (NativeMethods.IsIconic(hwnd))
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
            else
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_SHOW);

            SettingsWindowCoordinator.ForceForeground(hwnd);
            return true;
        }

        public static IntPtr GetForegroundWindowHandle() => NativeMethods.GetForegroundWindow();

        public static bool TryGetForegroundContext(out MediaForegroundContext context)
        {
            IntPtr foreground = NativeMethods.GetForegroundWindow();
            return TryCreateWindowContext(foreground, out context);
        }

        public static bool TryCreateWindowContext(IntPtr foreground, out MediaForegroundContext context)
        {
            context = default;
            if (!IsWindowValid(foreground))
                return false;

            IntPtr root = GetRootWindow(foreground);
            GetWindowProcessIds(root, foreground, out int rootProcessId, out int foregroundProcessId);
            if (rootProcessId == 0 || foregroundProcessId == 0)
                return false;

            context = new MediaForegroundContext(foreground, root, rootProcessId, foregroundProcessId);
            return true;
        }

        public static IntPtr GetRootWindow(IntPtr hwnd)
        {
            if (!IsWindowValid(hwnd))
                return IntPtr.Zero;

            IntPtr root = GetAncestor(hwnd, GA_ROOT);
            return root == IntPtr.Zero ? hwnd : root;
        }

        public static void GetWindowProcessIds(IntPtr root, IntPtr foreground, out int rootProcessId, out int foregroundProcessId)
        {
            rootProcessId = 0;
            foregroundProcessId = 0;
            uint rootPid = 0;
            uint foregroundPid = 0;

            if (root != IntPtr.Zero)
                NativeMethods.GetWindowThreadProcessId(root, out rootPid);

            if (foreground != IntPtr.Zero)
                NativeMethods.GetWindowThreadProcessId(foreground, out foregroundPid);

            rootProcessId = (int)rootPid;
            foregroundProcessId = (int)foregroundPid;
        }

        public static void SendCommandToPlayerWindow(IntPtr hwnd, MediaCommand command)
        {
            if (!IsWindowValid(hwnd))
                return;

            NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
            if ((int)pid == OurProcessId)
                return;

            hwnd = ResolveMainPlayerWindow((int)pid, hwnd);
            PostMessage(hwnd, (uint)NativeMethods.WM_APPCOMMAND, IntPtr.Zero, new IntPtr((int)command));
        }

        public static bool SendCommandToSelectedPlayer(MediaApplication application, MediaCommand command)
        {
            IntPtr hwnd = FindWindow(application);
            if (!IsWindowValid(hwnd))
                return false;

            PostMessage(hwnd, (uint)NativeMethods.WM_APPCOMMAND, IntPtr.Zero, new IntPtr((int)command));
            return true;
        }

        public static void SendCommandToBrowser(IntPtr browserHwnd, MediaCommand command)
        {
            if (!IsWindowValid(browserHwnd))
                return;

            PostMessage(browserHwnd, (uint)NativeMethods.WM_APPCOMMAND, IntPtr.Zero, new IntPtr((int)command));
        }

        public static bool? ClassifyWindow(IntPtr root)
        {
            if (!IsWindowValid(root))
                return null;

            NativeMethods.GetWindowThreadProcessId(root, out uint pid);
            if ((int)pid == OurProcessId)
                return null;
            if (IsDesktopPlayerProcess((int)pid) || IsPlayerProcess((int)pid))
                return false;
            if (IsBrowserProcess((int)pid))
                return true;
            return null;
        }

        public static IntPtr GetPlayerSendHwnd(IntPtr root)
        {
            if (!IsWindowValid(root))
                return root;

            NativeMethods.GetWindowThreadProcessId(root, out uint pid);
            return ResolveMainPlayerWindow((int)pid, root);
        }

        private static IntPtr ResolveMainPlayerWindow(int processId, IntPtr fallbackHwnd)
        {
            foreach (MediaPlayerDescriptor player in KnownMediaSources.ManagedPlayers)
            {
                if (IsProcessForApplication(processId, player.Application))
                {
                    IntPtr main = FindWindow(player);
                    if (IsWindowValid(main))
                        return main;
                }
            }

            if (!string.IsNullOrEmpty(KnownMediaSources.CustomProcessName) &&
                IsProcessForApplication(processId, MediaApplication.Custom))
            {
                IntPtr main = FindWindow(MediaApplication.Custom);
                if (IsWindowValid(main))
                    return main;
            }

            return fallbackHwnd;
        }

        private static bool IsProcessForApplication(int processId, MediaApplication application)
        {
            MediaPlayerDescriptor descriptor = KnownMediaSources.GetDescriptor(application);
            return MatchesProcess(processId, (name, path) => MediaProcessClassifier.IsPlayerMatch(name, path, descriptor));
        }

        public static bool TryGetDesktopPlayerApplication(IntPtr hwnd, out MediaApplication application)
        {
            application = MediaApplication.Spotify;
            if (!IsWindowValid(hwnd))
                return false;

            NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
            foreach (MediaPlayerDescriptor player in KnownMediaSources.ManagedPlayers)
            {
                if (IsProcessForApplication((int)pid, player.Application))
                {
                    application = player.Application;
                    return true;
                }
            }

            if (!string.IsNullOrEmpty(KnownMediaSources.CustomProcessName) &&
                IsProcessForApplication((int)pid, MediaApplication.Custom))
            {
                application = MediaApplication.Custom;
                return true;
            }

            return false;
        }

        public static bool IsSpotifyProcess(int processId)
        {
            return MatchesProcess(processId, MediaProcessClassifier.IsSpotify);
        }

        public static bool IsYandexMusicProcess(int processId)
        {
            return MatchesProcess(processId, MediaProcessClassifier.IsYandexMusic);
        }

        public static bool IsDesktopPlayerProcess(int processId)
        {
            return MatchesProcess(processId, MediaProcessClassifier.IsDesktopPlayer);
        }

        public static bool IsPlayerProcess(int processId)
        {
            if (processId == OurProcessId)
                return false;
            return MatchesProcess(processId, MediaProcessClassifier.IsPlayer);
        }

        public static bool IsBrowserProcess(int processId)
        {
            if (processId == OurProcessId)
                return false;
            return MatchesProcess(processId, MediaProcessClassifier.IsBrowser);
        }

        public static IntPtr GetForegroundPlayerRoot()
        {
            IntPtr foreground = NativeMethods.GetForegroundWindow();
            if (!IsWindowValid(foreground))
                return IntPtr.Zero;

            IntPtr root = GetRootWindow(foreground);
            NativeMethods.GetWindowThreadProcessId(root, out uint rootPid);
            NativeMethods.GetWindowThreadProcessId(foreground, out uint foregroundPid);

            if ((int)rootPid == OurProcessId || (int)foregroundPid == OurProcessId)
                return IntPtr.Zero;

            if (IsDesktopPlayerProcess((int)rootPid) || IsDesktopPlayerProcess((int)foregroundPid))
                return root;

            IntPtr knownPlayerWnd = FindFirstKnownPlayerWindow();

            if (IsWindowValid(knownPlayerWnd) && (root == knownPlayerWnd || IsChild(knownPlayerWnd, foreground)))
                return root;

            IntPtr top = GetTopLevelWindow(foreground);

            if ((int)foregroundPid != OurProcessId && (IsPlayerProcess((int)foregroundPid) || IsDesktopPlayerProcess((int)foregroundPid)))
                return IsWindowValid(root) ? root : (IsWindowValid(top) ? top : IntPtr.Zero);

            if ((int)rootPid != OurProcessId && (IsPlayerProcess((int)rootPid) || IsDesktopPlayerProcess((int)rootPid)) && IsWindowValid(root))
                return root;

            if (top != IntPtr.Zero && top != root && IsWindowValid(top))
            {
                NativeMethods.GetWindowThreadProcessId(top, out uint topPid);
                if ((int)topPid != OurProcessId && (IsPlayerProcess((int)topPid) || IsDesktopPlayerProcess((int)topPid)))
                    return top;
            }

            IntPtr current = foreground;
            uint lastCheckedPid = 0;
            while (current != IntPtr.Zero && IsWindow(current))
            {
                NativeMethods.GetWindowThreadProcessId(current, out uint pid);
                if (pid != lastCheckedPid)
                {
                    lastCheckedPid = pid;
                    if ((int)pid != OurProcessId && (IsPlayerProcess((int)pid) || IsDesktopPlayerProcess((int)pid)))
                        return IsWindowValid(root) ? root : current;
                }
                current = GetParent(current);
            }

            if (IsDesktopPlayerProcess((int)rootPid) && IsWindowValid(root))
                return root;

            if (IsBrowserProcess((int)rootPid) || IsBrowserProcess((int)foregroundPid))
                return root;

            knownPlayerWnd = FindFirstKnownPlayerWindow();
            if (IsWindowValid(knownPlayerWnd))
            {
                NativeMethods.GetWindowThreadProcessId(knownPlayerWnd, out uint playerPid);
                if ((int)rootPid == (int)playerPid || IsDesktopPlayerProcess((int)rootPid))
                    return root;
            }

            return IntPtr.Zero;
        }

        private static IntPtr FindFirstKnownPlayerWindow()
        {
            foreach (MediaPlayerDescriptor player in KnownMediaSources.ManagedPlayers)
            {
                IntPtr hwnd = FindWindow(player);
                if (IsWindowValid(hwnd))
                    return hwnd;
            }

            return IntPtr.Zero;
        }

        private static bool MatchesProcess(int processId, Func<string?, string?, bool> predicate)
        {
            if (processId == OurProcessId)
                return false;

            ProcessIdentity identity = GetProcessIdentity(processId);
            return identity.Path.Length > 0 && predicate(identity.Name, identity.Path);
        }

        private readonly record struct ProcessIdentity(string Name, string Path, DateTime ReadAtUtc);

        private static readonly TimeSpan ProcessIdentityLifetime = TimeSpan.FromSeconds(10);
        private static readonly Dictionary<int, ProcessIdentity> ProcessIdentities = new();
        private static readonly object ProcessIdentitiesLock = new();

        private static ProcessIdentity GetProcessIdentity(int processId)
        {
            DateTime now = DateTime.UtcNow;
            lock (ProcessIdentitiesLock)
            {
                if (ProcessIdentities.TryGetValue(processId, out ProcessIdentity cached) &&
                    now - cached.ReadAtUtc < ProcessIdentityLifetime)
                {
                    return cached;
                }
            }

            string path = GetProcessExecutablePath(processId);
            var identity = new ProcessIdentity(path.Length > 0 ? System.IO.Path.GetFileNameWithoutExtension(path) : "", path, now);

            lock (ProcessIdentitiesLock)
            {
                if (ProcessIdentities.Count > 1024)
                    ProcessIdentities.Clear();
                ProcessIdentities[processId] = identity;
            }

            return identity;
        }

        private static IntPtr FindWindowByOwnerProcessName(string[] allowedProcessNames, bool requireVisible)
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows((hWnd, _) =>
            {
                if (requireVisible && !IsWindowVisible(hWnd))
                    return true;

                NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid == 0 || pid == OurProcessId)
                    return true;

                string ownerName = GetProcessIdentity((int)pid).Name;
                foreach (string allowed in allowedProcessNames)
                {
                    if (string.Equals(ownerName, allowed, StringComparison.OrdinalIgnoreCase))
                    {
                        found = hWnd;
                        return false;
                    }
                }

                return true;
            }, IntPtr.Zero);

            return found;
        }

        internal static string GetProcessExecutablePath(int processId)
        {
            IntPtr hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
            if (hProcess == IntPtr.Zero)
                return string.Empty;

            try
            {
                var buffer = new StringBuilder(1024);
                int size = buffer.Capacity;
                if (QueryFullProcessImageName(hProcess, 0, buffer, ref size))
                {
                    return buffer.ToString();
                }
                return string.Empty;
            }
            finally
            {
                CloseHandle(hProcess);
            }
        }

        private static IntPtr GetTopLevelWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
                return IntPtr.Zero;

            IntPtr previous = IntPtr.Zero;
            while (hwnd != IntPtr.Zero && IsWindow(hwnd))
            {
                previous = hwnd;
                hwnd = GetParent(hwnd);
            }

            return previous;
        }

        public static string? TryGetDesktopPlayerTrackTitle(MediaApplication application)
        {
            try
            {
                IntPtr hwnd = FindWindow(application);
                if (!IsWindowValid(hwnd)) return null;

                var sb = new StringBuilder(512);
                if (GetWindowText(hwnd, sb, sb.Capacity) > 0)
                {
                    string title = sb.ToString().Trim();
                    MediaPlayerDescriptor descriptor = KnownMediaSources.GetDescriptor(application);
                    if (!string.IsNullOrEmpty(title) && !descriptor.IsPlaceholderWindowTitle(title))
                    {
                        return title;
                    }
                }
            }
            catch (Exception ex)
            {
                EventLogService.Log("Routing", "Failed to read the player window title: " + ex.Message);
            }
            return null;
        }
    }
}
