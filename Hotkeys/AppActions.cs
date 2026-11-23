using System.Collections.Generic;

namespace MusicMediaKeys
{
    public static class AppActions
    {
        public const string Previous = "Previous";
        public const string PlayPause = "PlayPause";
        public const string Next = "Next";
        public const string VolumeDown = "VolumeDown";
        public const string VolumeUp = "VolumeUp";
        public const string VolumeMute = "VolumeMute";

        public const string ToggleMainWindow = "ToggleMainWindow";
        public const string OpenHotkeysTab = "OpenHotkeysTab";
        public const string ToggleOverlay = "ToggleOverlay";
        public const string ShowOverlayNow = "ShowOverlayNow";
        public const string CycleTargetPlayer = "CycleTargetPlayer";
        public const string ToggleControlAnyPlayer = "ToggleGameSafeMode";
        public const string CopyTrackInfo = "CopyTrackInfo";
        public const string SeekBackward = "SeekBackward";
        public const string SeekForward = "SeekForward";
        public const string ToggleVolumeDucking = "ToggleVolumeDucking";
        public const string FocusTargetPlayer = "FocusTargetPlayer";
        public const string ToggleHotkeysSuspended = "ToggleHotkeysSuspended";

        public static IReadOnlyList<string> AvailableCustomActions { get; } = new[]
        {
            ToggleMainWindow,
            OpenHotkeysTab,
            ToggleOverlay,
            ShowOverlayNow,
            CopyTrackInfo,
            SeekForward,
            SeekBackward,
            ToggleVolumeDucking,
            FocusTargetPlayer,
            ToggleHotkeysSuspended,
            CycleTargetPlayer,
            ToggleControlAnyPlayer
        };

        public static string GetActionLabelKey(string actionId) => actionId switch
        {
            ToggleMainWindow => "AppAction_ToggleMainWindow",
            OpenHotkeysTab => "AppAction_OpenHotkeysTab",
            ToggleOverlay => "AppAction_ToggleOverlay",
            ShowOverlayNow => "AppAction_ShowOverlayNow",
            CopyTrackInfo => "AppAction_CopyTrackInfo",
            SeekBackward => "AppAction_SeekBackward",
            SeekForward => "AppAction_SeekForward",
            ToggleVolumeDucking => "AppAction_ToggleVolumeDucking",
            FocusTargetPlayer => "AppAction_FocusTargetPlayer",
            ToggleHotkeysSuspended => "AppAction_ToggleHotkeysSuspended",
            CycleTargetPlayer => "AppAction_CycleTargetPlayer",
            ToggleControlAnyPlayer => "AppAction_ToggleControlAnyPlayer",
            _ => actionId
        };
    }
}
