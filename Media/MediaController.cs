using System;

namespace MusicMediaKeys
{
    public enum MediaCommand
    {
        Previous = 0x0C0000,
        PlayPause = 0x0E0000,
        Next = 0x0B0000,
        VolumeUp = 0x100000,
        VolumeDown = 0x200000,
        VolumeMute = 0x300000
    }

    public enum MediaApplication
    {
        Spotify,
        YandexMusic,
        AppleMusic,
        Aimp,
        Foobar2000,
        Vlc,
        YouTubeMusicDesktop,
        Custom
    }

    public class MediaController
    {
        private readonly MediaRoutingService _routingService = new();
        private readonly MediaVolumeService _volumeService = new();

        internal MediaVolumeService VolumeService => _volumeService;

        private volatile int _selectionMode = (int)ApplicationSelectionMode.Spotify;
        private volatile string _customPlayerProcessName = "";
        private volatile bool _useSystemMediaKeysOnly;
        private volatile bool _preferWindowsMediaSession = true;
        private volatile bool _blockMediaKeysForBrowser;
        private volatile float _volumeStep = 0.05f;

        public ApplicationSelectionMode SelectionMode
        {
            get => (ApplicationSelectionMode)_selectionMode;
            set => _selectionMode = (int)value;
        }

        public string CustomPlayerProcessName
        {
            get => _customPlayerProcessName;
            set => _customPlayerProcessName = KnownMediaSources.NormalizeCustomProcessName(value);
        }

        public bool UseSystemMediaKeysOnly
        {
            get => _useSystemMediaKeysOnly;
            set => _useSystemMediaKeysOnly = value;
        }

        public bool PreferWindowsMediaSession
        {
            get => _preferWindowsMediaSession;
            set => _preferWindowsMediaSession = value;
        }

        public bool BlockMediaKeysForBrowser
        {
            get => _blockMediaKeysForBrowser;
            set => _blockMediaKeysForBrowser = value;
        }

        public float VolumeStep
        {
            get => _volumeStep;
            set => _volumeStep = value;
        }

        public string LastCommandRouteDetails { get; private set; } = "";

        public void ApplySettings(AppSettings settings)
        {
            SelectionMode = settings.SelectedApplication;
            CustomPlayerProcessName = settings.CustomPlayerProcessName;
            KnownMediaSources.CustomProcessName = settings.CustomPlayerProcessName;
            UseSystemMediaKeysOnly = settings.UseSystemMediaKeysOnly;
            PreferWindowsMediaSession = settings.PreferWindowsMediaSession;
            BlockMediaKeysForBrowser = settings.BlockMediaKeysForBrowser;
            VolumeStep = Math.Clamp(settings.VolumeStepPercent, 1, 100) / 100f;
        }

        public void SetForegroundAtKeyPress(IntPtr hwnd)
        {
            _routingService.SetForegroundAtKeyPress(hwnd);
        }

        public bool SendCommand(MediaCommand command)
        {
            bool isVolume =
                command == MediaCommand.VolumeUp ||
                command == MediaCommand.VolumeDown ||
                command == MediaCommand.VolumeMute;

            if (UseSystemMediaKeysOnly)
            {
                if (isVolume)
                {
                    LastCommandRouteDetails = "Adjusted default audio device volume";
                    return _volumeService.TryAdjustDefaultDevice(command, VolumeStep);
                }

                if (PreferWindowsMediaSession)
                {
                    WindowsMediaSessionService.CommandResult smtcResult = WindowsMediaSessionService.TrySendCommand(command, allowBrowser: !BlockMediaKeysForBrowser);
                    if (smtcResult.Success)
                    {
                        LastCommandRouteDetails = smtcResult.Details;
                        return true;
                    }

                    LastCommandRouteDetails = smtcResult.Details;
                }
                else
                {
                    LastCommandRouteDetails = "Windows media session preference is disabled";
                }

                _routingService.SendCommandByActiveWindow(command, BlockMediaKeysForBrowser);
                if (!string.IsNullOrWhiteSpace(LastCommandRouteDetails))
                    LastCommandRouteDetails += " | Fallback to active-window routing";
                else
                    LastCommandRouteDetails = "Fallback to active-window routing";
                return true;
            }

            if (isVolume)
            {
                LastCommandRouteDetails = "Adjusted target process volume";
                return _volumeService.TryAdjustTargetProcesses(command, _routingService.GetTargetProcessIds(SelectionMode), VolumeStep);
            }

            bool targetedSuccess = _routingService.SendTargetedPlayerCommand(command, SelectionMode);
            if (!targetedSuccess)
            {
                var smtcResult = WindowsMediaSessionService.TrySendCommand(command, allowBrowser: false, requiredPlayer: SelectionMode);
                if (smtcResult.Success)
                {
                    LastCommandRouteDetails = $"{smtcResult.Details} | Fallback for minimized player";
                    return true;
                }
            }

            LastCommandRouteDetails = targetedSuccess
                ? "Command sent to selected desktop player"
                : "Failed to send command to selected desktop player";
            return targetedSuccess;
        }

        public bool IsApplicationRunning()
        {
            return _routingService.IsApplicationRunning(SelectionMode);
        }

        public MediaApplication GetActiveApplication()
        {
            return _routingService.GetTargetApplication(SelectionMode);
        }

        public System.Collections.Generic.IEnumerable<int> GetTargetProcessIds()
        {
            return _routingService.GetTargetProcessIds(SelectionMode);
        }

        public System.Collections.Generic.IReadOnlyList<ApplicationSelectionMode> GetDetectedRunningModes()
        {
            return _routingService.GetDetectedRunningModes();
        }

        public bool TryFocusTargetPlayer()
        {
            if (UseSystemMediaKeysOnly)
            {
                var last = _routingService.GetLastActiveTarget(excludeBrowser: BlockMediaKeysForBrowser);
                if (last.IsBrowser == false && MediaPlayerProviders.TryFocusPlayerWindow(last.PlayerHwnd))
                    return true;
            }

            IntPtr hwnd = MediaPlayerProviders.FindWindow(GetActiveApplication());
            return MediaPlayerProviders.TryFocusPlayerWindow(hwnd);
        }

        public void RefreshLastActiveWindow()
        {
            _routingService.RefreshLastActiveWindow();
        }
    }
}
