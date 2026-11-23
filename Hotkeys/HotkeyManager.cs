using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace MusicMediaKeys
{
    public class HotkeyManager : IDisposable
    {
        private const int MinHotkeyId = 1000;
        private const int MaxHotkeyId = 0xBFFF;
        private IntPtr _windowHandle;
        private Dictionary<int, Action> _hotkeyCallbacks;
        private int _hotkeyIdCounter = MinHotkeyId;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public HotkeyManager(IntPtr windowHandle)
        {
            _windowHandle = windowHandle;
            _hotkeyCallbacks = new Dictionary<int, Action>();
        }

        private const uint MOD_NOREPEAT = 0x4000;

        public int RegisterHotkey(Keys key, ModifierKeys modifiers, Action callback, bool allowRepeat = false)
        {
            if (modifiers == ModifierKeys.None && !HotkeyCapabilityAnalyzer.IsMediaKey((int)key))
                return -1;

            int hotkeyId = AllocateHotkeyId();
            if (hotkeyId == -1)
                return -1;

            uint fsModifiers = (uint)modifiers;
            if (!allowRepeat)
            {
                fsModifiers |= MOD_NOREPEAT;
            }
            uint vk = (uint)key;

            if (RegisterHotKey(_windowHandle, hotkeyId, fsModifiers, vk))
            {
                _hotkeyCallbacks[hotkeyId] = callback;
                return hotkeyId;
            }

            return -1;
        }

        private int AllocateHotkeyId()
        {
            int range = MaxHotkeyId - MinHotkeyId + 1;
            for (int i = 0; i < range; i++)
            {
                int candidate = _hotkeyIdCounter++;
                if (_hotkeyIdCounter > MaxHotkeyId)
                    _hotkeyIdCounter = MinHotkeyId;

                if (!_hotkeyCallbacks.ContainsKey(candidate))
                    return candidate;
            }
            return -1;
        }

        public void UnregisterHotkey(int hotkeyId)
        {
            if (_hotkeyCallbacks.Remove(hotkeyId))
                UnregisterHotKey(_windowHandle, hotkeyId);
        }

        public bool ProcessHotkey(int hotkeyId)
        {
            if (_hotkeyCallbacks.TryGetValue(hotkeyId, out Action? callback))
            {
                callback?.Invoke();
                return true;
            }
            return false;
        }

        public bool ProcessHotkeyMessage(int msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == NativeMethods.WM_HOTKEY)
                return ProcessHotkey(wParam.ToInt32());
            return false;
        }

        public void Dispose()
        {
            foreach (var id in _hotkeyCallbacks.Keys.ToArray())
                UnregisterHotKey(_windowHandle, id);
            _hotkeyCallbacks.Clear();
        }
    }

    [Flags]
    public enum ModifierKeys : uint
    {
        None = 0,
        Alt = 1,
        Control = 2,
        Shift = 4,
        Win = 8
    }
}
