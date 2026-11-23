using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MusicMediaKeys
{
    internal sealed class MediaRoutingService
    {
        private static readonly TimeSpan ProcessCacheTtl = TimeSpan.FromSeconds(2);

        private readonly object _lastActiveLock = new();
        private readonly object _processCacheLock = new();

        private IntPtr _lastActiveBrowserWindow = IntPtr.Zero;
        private DateTime _lastActiveBrowserTime = DateTime.MinValue;
        private IntPtr _lastActivePlayerWindow = IntPtr.Zero;
        private DateTime _lastActivePlayerTime = DateTime.MinValue;
        private IntPtr _foregroundAtKeyPress = IntPtr.Zero;

        private int[]? _cachedTargetPids;
        private DateTime _cachedTargetPidsTime = DateTime.MinValue;
        private ApplicationSelectionMode _cachedTargetMode = ApplicationSelectionMode.Auto;
        private string _cachedCustomProcessName = "";

        public void SetForegroundAtKeyPress(IntPtr hwnd)
        {
            lock (_lastActiveLock)
            {
                _foregroundAtKeyPress = hwnd;
            }
        }

        public bool SendTargetedPlayerCommand(MediaCommand command, ApplicationSelectionMode selectionMode)
        {
            try
            {
                MediaApplication targetApp = GetTargetApplication(selectionMode);
                return MediaPlayerProviders.SendCommandToSelectedPlayer(targetApp, command);
            }
            catch
            {
                return false;
            }
        }

        public void SendCommandByActiveWindow(MediaCommand command, bool blockMediaKeysForBrowser)
        {
            IntPtr capturedForeground;
            lock (_lastActiveLock)
            {
                capturedForeground = _foregroundAtKeyPress;
                _foregroundAtKeyPress = IntPtr.Zero;
            }

            IntPtr foreground = capturedForeground != IntPtr.Zero
                ? capturedForeground
                : MediaPlayerProviders.GetForegroundWindowHandle();

            if (!MediaPlayerProviders.TryCreateWindowContext(foreground, out MediaForegroundContext context))
            {
                SendToLastActive(command, blockMediaKeysForBrowser);
                return;
            }

            if (context.IsOwnProcess)
            {
                SendToLastActive(command, blockMediaKeysForBrowser);
                return;
            }

            if (context.IsDesktopPlayer)
            {
                IntPtr playerHwnd = MediaPlayerProviders.GetPlayerSendHwnd(context.PreferredDesktopPlayerWindow);
                if (MediaPlayerProviders.IsWindowValid(playerHwnd))
                {
                    SetLastActivePlayer(playerHwnd);
                    MediaPlayerProviders.SendCommandToPlayerWindow(playerHwnd, command);
                }
                return;
            }

            if (context.IsBrowser)
            {
                SetLastActiveBrowser(context.RootWindow);
                if (!blockMediaKeysForBrowser)
                    MediaPlayerProviders.SendCommandToBrowser(context.RootWindow, command);
                else
                    SendToLastActive(command, blockMediaKeysForBrowser);
                return;
            }

            SendToLastActive(command, blockMediaKeysForBrowser);
        }

        public (bool? IsBrowser, IntPtr PlayerHwnd) GetLastActiveTarget(bool excludeBrowser = false)
        {
            lock (_lastActiveLock)
            {
                bool browserOk = !excludeBrowser && MediaPlayerProviders.IsWindowValid(_lastActiveBrowserWindow);
                bool playerOk = MediaPlayerProviders.IsWindowValid(_lastActivePlayerWindow);

                if (browserOk && (!playerOk || _lastActiveBrowserTime >= _lastActivePlayerTime))
                    return (true, IntPtr.Zero);

                if (playerOk)
                    return (false, MediaPlayerProviders.GetPlayerSendHwnd(_lastActivePlayerWindow));

                return (null, IntPtr.Zero);
            }
        }

        public IEnumerable<int> GetTargetProcessIds(ApplicationSelectionMode selectionMode)
        {
            string currentCustomName = KnownMediaSources.CustomProcessName;
            DateTime now = DateTime.UtcNow;
            lock (_processCacheLock)
            {
                if (_cachedTargetPids != null &&
                    _cachedTargetMode == selectionMode &&
                    string.Equals(_cachedCustomProcessName, currentCustomName, StringComparison.OrdinalIgnoreCase) &&
                    (now - _cachedTargetPidsTime) < ProcessCacheTtl)
                {
                    return _cachedTargetPids;
                }
            }

            var list = new List<int>();
            try
            {
                MediaApplication targetApp = selectionMode == ApplicationSelectionMode.Auto
                    ? GetTargetApplication(selectionMode)
                    : KnownMediaSources.ToMediaApplication(selectionMode);
                MediaPlayerDescriptor descriptor = KnownMediaSources.GetDescriptor(targetApp, currentCustomName);
                if (descriptor.ProcessNames.Length == 0)
                    return list;

                foreach (Process process in Process.GetProcesses())
                {
                    try
                    {
                        if (process.Id == MediaPlayerProviders.OurProcessId)
                            continue;

                        string name = process.ProcessName ?? "";
                        if (!CouldMatchDescriptor(name, descriptor))
                            continue;

                        string path = MediaPlayerProviders.GetProcessExecutablePath(process.Id);
                        if (MediaProcessClassifier.IsPlayerMatch(name, path, descriptor))
                            list.Add(process.Id);
                    }
                    catch
                    {
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }

                int[] snapshot = list.ToArray();
                lock (_processCacheLock)
                {
                    _cachedTargetPids = snapshot;
                    _cachedTargetPidsTime = now;
                    _cachedTargetMode = selectionMode;
                    _cachedCustomProcessName = currentCustomName;
                }
                return snapshot;
            }
            catch (Exception ex)
            {
                EventLogService.Log("Routing", "Failed to list player processes: " + ex.Message);
                return list;
            }
        }

        private static bool CouldMatchDescriptor(string processName, MediaPlayerDescriptor descriptor)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return false;

            foreach (string candidate in descriptor.ProcessNames)
            {
                if (processName.Contains(candidate, StringComparison.OrdinalIgnoreCase) ||
                    candidate.Contains(processName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            string lower = processName.Trim().ToLowerInvariant();
            if (descriptor.MatchesSourceLower(lower))
                return true;

            if (descriptor.Application == MediaApplication.YandexMusic)
            {
                return lower.Contains("yandex") || lower.Contains("music") || lower.Contains("музыка");
            }

            return false;
        }

        public MediaApplication GetTargetApplication(ApplicationSelectionMode selectionMode)
        {
            if (selectionMode != ApplicationSelectionMode.Auto)
                return KnownMediaSources.ToMediaApplication(selectionMode);

            MediaApplication? foregroundApp = null;
            MediaApplication? lastActiveApp = null;

            IntPtr foregroundPlayerRoot = MediaPlayerProviders.GetForegroundPlayerRoot();
            if (MediaPlayerProviders.TryGetDesktopPlayerApplication(foregroundPlayerRoot, out MediaApplication detectedForegroundApp))
                foregroundApp = detectedForegroundApp;

            lock (_lastActiveLock)
            {
                if (MediaPlayerProviders.TryGetDesktopPlayerApplication(_lastActivePlayerWindow, out MediaApplication detectedLastActiveApp))
                    lastActiveApp = detectedLastActiveApp;
            }

            var runningApps = new List<MediaApplication>();
            foreach (MediaPlayerDescriptor player in KnownMediaSources.ManagedPlayers)
            {
                if (MediaPlayerProviders.FindWindow(player) != IntPtr.Zero)
                    runningApps.Add(player.Application);
            }

            if (!string.IsNullOrEmpty(KnownMediaSources.CustomProcessName) &&
                MediaPlayerProviders.FindWindow(MediaApplication.Custom) != IntPtr.Zero)
            {
                runningApps.Add(MediaApplication.Custom);
            }

            return MediaTargetSelectionService.Resolve(
                selectionMode,
                foregroundApp,
                lastActiveApp,
                runningApps);
        }

        public bool IsApplicationRunning(ApplicationSelectionMode selectionMode)
        {
            if (selectionMode == ApplicationSelectionMode.Auto)
            {
                foreach (MediaPlayerDescriptor player in KnownMediaSources.ManagedPlayers)
                {
                    if (MediaPlayerProviders.FindWindow(player) != IntPtr.Zero)
                        return true;
                }

                return !string.IsNullOrEmpty(KnownMediaSources.CustomProcessName) &&
                       MediaPlayerProviders.FindWindow(MediaApplication.Custom) != IntPtr.Zero;
            }

            return MediaPlayerProviders.FindWindow(KnownMediaSources.ToMediaApplication(selectionMode)) != IntPtr.Zero;
        }

        public IReadOnlyList<ApplicationSelectionMode> GetDetectedRunningModes()
        {
            var result = new List<ApplicationSelectionMode>();
            foreach (MediaPlayerDescriptor player in KnownMediaSources.ManagedPlayers)
            {
                if (MediaPlayerProviders.FindWindow(player) != IntPtr.Zero)
                {
                    ApplicationSelectionMode mode = KnownMediaSources.ToSelectionMode(player.Application);
                    if (!result.Contains(mode))
                        result.Add(mode);
                }
            }

            if (!string.IsNullOrEmpty(KnownMediaSources.CustomProcessName) &&
                MediaPlayerProviders.FindWindow(MediaApplication.Custom) != IntPtr.Zero)
            {
                result.Add(ApplicationSelectionMode.Custom);
            }

            return result;
        }

        public void RefreshLastActiveWindow()
        {
            if (!MediaPlayerProviders.TryGetForegroundContext(out MediaForegroundContext context))
                return;

            if (context.IsOwnProcess)
                return;

            DateTime now = DateTime.UtcNow;

            if (context.IsDesktopPlayer)
            {
                IntPtr playerHwnd = MediaPlayerProviders.GetPlayerSendHwnd(context.PreferredDesktopPlayerWindow);
                if (MediaPlayerProviders.IsWindowValid(playerHwnd))
                {
                    lock (_lastActiveLock)
                    {
                        _lastActivePlayerWindow = playerHwnd;
                        _lastActivePlayerTime = now;
                    }
                }
                return;
            }

            if (context.IsBrowser)
            {
                lock (_lastActiveLock)
                {
                    _lastActiveBrowserWindow = context.RootWindow;
                    _lastActiveBrowserTime = now;
                }
                return;
            }

            IntPtr detectedRoot = MediaPlayerProviders.GetForegroundPlayerRoot();
            if (!MediaPlayerProviders.IsWindowValid(detectedRoot))
                return;

            bool? kind = MediaPlayerProviders.ClassifyWindow(detectedRoot);
            if (kind == false)
            {
                IntPtr playerHwnd = MediaPlayerProviders.GetPlayerSendHwnd(detectedRoot);
                if (MediaPlayerProviders.IsWindowValid(playerHwnd))
                {
                    lock (_lastActiveLock)
                    {
                        _lastActivePlayerWindow = playerHwnd;
                        _lastActivePlayerTime = now;
                    }
                }
            }
        }

        private void SetLastActiveBrowser(IntPtr hwnd)
        {
            if (!MediaPlayerProviders.IsWindowValid(hwnd))
                return;

            lock (_lastActiveLock)
            {
                _lastActiveBrowserWindow = hwnd;
                _lastActiveBrowserTime = DateTime.UtcNow;
            }
        }

        private void SetLastActivePlayer(IntPtr hwnd)
        {
            if (!MediaPlayerProviders.IsWindowValid(hwnd))
                return;

            lock (_lastActiveLock)
            {
                _lastActivePlayerWindow = hwnd;
                _lastActivePlayerTime = DateTime.UtcNow;
            }
        }

        private void SendToLastActive(MediaCommand command, bool blockMediaKeysForBrowser)
        {
            (bool? isBrowser, IntPtr playerHwnd) = GetLastActiveTarget(excludeBrowser: blockMediaKeysForBrowser);
            if (isBrowser == true && !blockMediaKeysForBrowser)
            {
                IntPtr browserHwnd;
                lock (_lastActiveLock)
                {
                    browserHwnd = _lastActiveBrowserWindow;
                }
                if (MediaPlayerProviders.IsWindowValid(browserHwnd))
                    MediaPlayerProviders.SendCommandToBrowser(browserHwnd, command);
            }
            else if (isBrowser == false && playerHwnd != IntPtr.Zero)
            {
                MediaPlayerProviders.SendCommandToPlayerWindow(playerHwnd, command);
            }
        }
    }
}
