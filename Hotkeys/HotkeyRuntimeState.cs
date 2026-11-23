using System;
using System.Collections.Generic;

namespace MusicMediaKeys
{
    public sealed class HotkeyRuntimeState
    {
        private readonly Dictionary<string, int> _registeredIds = new(StringComparer.Ordinal);

        public Dictionary<int, string> RegisteredHotkeyActions { get; } = new();

        public void Reset(HotkeyManager? hotkeyManager)
        {
            if (hotkeyManager != null)
            {
                var idsToUnregister = new HashSet<int>();
                foreach (int id in _registeredIds.Values)
                {
                    if (id != -1)
                        idsToUnregister.Add(id);
                }
                foreach (int id in RegisteredHotkeyActions.Keys)
                {
                    if (id != -1)
                        idsToUnregister.Add(id);
                }
                foreach (int id in idsToUnregister)
                {
                    hotkeyManager.UnregisterHotkey(id);
                }
            }

            _registeredIds.Clear();
            RegisteredHotkeyActions.Clear();
        }

        public void SetRegisteredHotkeyId(string actionId, int id)
        {
            _registeredIds[actionId] = id;
        }

        public int GetRegisteredHotkeyId(string actionId)
        {
            return _registeredIds.TryGetValue(actionId, out int id) ? id : -1;
        }

        public void TrackRegisteredHotkey(int id, string actionId)
        {
            if (id != -1)
                RegisteredHotkeyActions[id] = actionId;
        }
    }
}
