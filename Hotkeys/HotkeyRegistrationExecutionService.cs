using System;
using System.Collections.Generic;

namespace MusicMediaKeys
{
    public sealed class HotkeyRegistrationExecutionResult
    {
        public IReadOnlyList<string> FailedLabelKeys { get; init; } = Array.Empty<string>();
    }

    public static class HotkeyRegistrationExecutionService
    {
        public static HotkeyRegistrationExecutionResult RegisterAll(
            AppSettings settings,
            HotkeyManager hotkeyManager,
            HotkeyRuntimeState state,
            Action<MediaCommand, IntPtr> mediaCommandSender,
            Func<string, Action> appActionFactory,
            Func<IntPtr> foregroundWindowGetter,
            bool onlyAllowResumeAction = false)
        {
            List<string> failedLabelKeys = new();

            var items = HotkeyRegistrationPlanService.Build(settings);
            HashSet<string> duplicates = new HashSet<string>();
            for (int i = 0; i < items.Count; i++)
            {
                var item1 = items[i];
                if (item1.Binding == null || item1.Binding.Key == 0) continue;
                for (int j = i + 1; j < items.Count; j++)
                {
                    var item2 = items[j];
                    if (item2.Binding == null || item2.Binding.Key == 0) continue;
                    if (item1.Binding.Key == item2.Binding.Key && item1.Binding.Modifiers == item2.Binding.Modifiers)
                    {
                        duplicates.Add(item1.ActionId);
                        duplicates.Add(item2.ActionId);
                    }
                }
            }

            foreach (HotkeyRegistrationItem item in items)
            {
                if (onlyAllowResumeAction && item.ActionId != AppActions.ToggleHotkeysSuspended)
                {
                    state.SetRegisteredHotkeyId(item.ActionId, -1);
                    continue;
                }

                if (duplicates.Contains(item.ActionId))
                {
                    state.SetRegisteredHotkeyId(item.ActionId, -1);
                    failedLabelKeys.Add(item.LabelKey);
                    continue;
                }

                int registeredId = item.Kind == HotkeyRegistrationKind.MediaCommand && item.Command.HasValue
                    ? RegisterMedia(item, hotkeyManager, state, mediaCommandSender, foregroundWindowGetter)
                    : RegisterApp(item, hotkeyManager, state, appActionFactory(item.ActionId));

                state.SetRegisteredHotkeyId(item.ActionId, registeredId);

                if (item.Binding != null && item.Binding.Key != (int)Keys.None && registeredId == -1)
                {
                    failedLabelKeys.Add(item.LabelKey);
                }
            }

            return new HotkeyRegistrationExecutionResult
            {
                FailedLabelKeys = failedLabelKeys
            };
        }

        private static int RegisterMedia(
            HotkeyRegistrationItem item,
            HotkeyManager hotkeyManager,
            HotkeyRuntimeState state,
            Action<MediaCommand, IntPtr> mediaCommandSender,
            Func<IntPtr> foregroundWindowGetter)
        {
            HotkeyBinding? binding = item.Binding;
            MediaCommand command = item.Command!.Value;
            if (binding == null || binding.Key == (int)Keys.None)
                return -1;

            bool allowRepeat = command == MediaCommand.VolumeUp || command == MediaCommand.VolumeDown;
            int id = hotkeyManager.RegisterHotkey(
                binding.GetKey(),
                binding.GetModifiers(),
                () =>
                {
                    try { mediaCommandSender(command, foregroundWindowGetter()); }
                    catch (Exception ex) { EventLogService.Log("Hotkeys", $"Hotkey for {command} failed: {ex.Message}"); }
                },
                allowRepeat);

            state.TrackRegisteredHotkey(id, item.ActionId);
            return id;
        }

        private static int RegisterApp(
            HotkeyRegistrationItem item,
            HotkeyManager hotkeyManager,
            HotkeyRuntimeState state,
            Action action)
        {
            HotkeyBinding? binding = item.Binding;
            if (binding == null || binding.Key == (int)Keys.None)
                return -1;

            int id = hotkeyManager.RegisterHotkey(binding.GetKey(), binding.GetModifiers(), action);
            state.TrackRegisteredHotkey(id, item.ActionId);
            return id;
        }
    }
}
