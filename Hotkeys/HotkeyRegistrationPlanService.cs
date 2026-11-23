using System.Collections.Generic;

namespace MusicMediaKeys
{
    public enum HotkeyRegistrationKind
    {
        MediaCommand,
        AppAction
    }

    public sealed class HotkeyRegistrationItem
    {
        public string ActionId { get; init; } = "";
        public string LabelKey { get; init; } = "";
        public HotkeyBinding? Binding { get; init; }
        public HotkeyRegistrationKind Kind { get; init; }
        public MediaCommand? Command { get; init; }
    }

    public static class HotkeyRegistrationPlanService
    {
        public static IReadOnlyList<HotkeyRegistrationItem> Build(AppSettings settings)
        {
            var list = new List<HotkeyRegistrationItem>
            {
                CreateMedia(AppActions.Previous, "PreviousTrackShort", settings.PreviousBinding, MediaCommand.Previous),
                CreateMedia(AppActions.PlayPause, "PlayPauseShort", settings.PlayPauseBinding, MediaCommand.PlayPause),
                CreateMedia(AppActions.Next, "NextTrackShort", settings.NextBinding, MediaCommand.Next),
                CreateMedia(AppActions.VolumeUp, "VolumeUpShort", settings.VolumeUpBinding, MediaCommand.VolumeUp),
                CreateMedia(AppActions.VolumeDown, "VolumeDownShort", settings.VolumeDownBinding, MediaCommand.VolumeDown),
                CreateMedia(AppActions.VolumeMute, "VolumeMuteShort", settings.VolumeMuteBinding, MediaCommand.VolumeMute)
            };

            if (settings.CustomActionHotkeys != null)
            {
                foreach (var custom in settings.CustomActionHotkeys)
                {
                    if (string.IsNullOrWhiteSpace(custom.ActionId)) continue;
                    list.Add(CreateApp(custom.ActionId, AppActions.GetActionLabelKey(custom.ActionId), custom.Binding));
                }
            }

            return list;
        }

        private static HotkeyRegistrationItem CreateMedia(string actionId, string labelKey, HotkeyBinding? binding, MediaCommand command)
        {
            return new HotkeyRegistrationItem
            {
                ActionId = actionId,
                LabelKey = labelKey,
                Binding = binding,
                Kind = HotkeyRegistrationKind.MediaCommand,
                Command = command
            };
        }

        private static HotkeyRegistrationItem CreateApp(string actionId, string labelKey, HotkeyBinding? binding)
        {
            return new HotkeyRegistrationItem
            {
                ActionId = actionId,
                LabelKey = labelKey,
                Binding = binding,
                Kind = HotkeyRegistrationKind.AppAction
            };
        }
    }
}
