using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace MusicMediaKeys
{
    internal sealed class NativeTrayIcon : IDisposable
    {
        public const int WM_TRAYICON = 0x8000 + 100;
        public static readonly int WmTaskbarCreated = unchecked((int)RegisterWindowMessage("TaskbarCreated"));

        private const uint NIM_ADD = 0x00000000;
        private const uint NIM_MODIFY = 0x00000001;
        private const uint NIM_DELETE = 0x00000002;

        private const uint NIF_MESSAGE = 0x00000001;
        private const uint NIF_ICON = 0x00000002;
        private const uint NIF_TIP = 0x00000004;

        private const uint MSGFLT_ALLOW = 1;

        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_RBUTTONUP = 0x0205;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public uint dwState;
            public uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public uint uTimeoutOrVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public uint dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ChangeWindowMessageFilterEx(IntPtr hWnd, uint message, uint action, IntPtr pChangeFilterStruct);

        private readonly IntPtr _hwnd;
        private readonly uint _id;
        private Icon? _icon;
        private string _tip = "";
        private bool _isAdded;
        private bool _disposed;

        public event Action? LeftClick;
        public event Action? RightClick;
        public event Action? DoubleClick;

        public NativeTrayIcon(IntPtr hwnd, Icon initialIcon, string initialTip, uint id = 1)
        {
            _hwnd = hwnd;
            _id = id;
            _icon = initialIcon;
            _tip = initialTip ?? "";

            if (_hwnd != IntPtr.Zero)
                ChangeWindowMessageFilterEx(_hwnd, (uint)WmTaskbarCreated, MSGFLT_ALLOW, IntPtr.Zero);

            AddIcon();
        }

        private void AddIcon()
        {
            if (_disposed || _hwnd == IntPtr.Zero) return;
            NOTIFYICONDATA data = CreateData(NIF_MESSAGE | NIF_ICON | NIF_TIP);
            _isAdded = Shell_NotifyIcon(NIM_ADD, ref data);
        }

        public void SetIcon(Icon? icon)
        {
            if (_disposed) return;
            _icon = icon;
            if (!_isAdded)
            {
                AddIcon();
                return;
            }
            NOTIFYICONDATA data = CreateData(NIF_ICON);
            if (!Shell_NotifyIcon(NIM_MODIFY, ref data))
            {
                _isAdded = false;
                AddIcon();
            }
        }

        public void SetText(string tip)
        {
            if (_disposed) return;
            _tip = tip ?? "";
            if (!_isAdded)
            {
                AddIcon();
                return;
            }
            NOTIFYICONDATA data = CreateData(NIF_TIP);
            if (!Shell_NotifyIcon(NIM_MODIFY, ref data))
            {
                _isAdded = false;
                AddIcon();
            }
        }

        private NOTIFYICONDATA CreateData(uint flags)
        {
            NOTIFYICONDATA data = new NOTIFYICONDATA();
            data.cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>();
            data.hWnd = _hwnd;
            data.uID = _id;
            data.uFlags = flags;
            data.uCallbackMessage = WM_TRAYICON;
            data.hIcon = _icon?.Handle ?? IntPtr.Zero;
            data.szTip = _tip.Length > 127 ? _tip.Substring(0, 127) : _tip;
            return data;
        }

        public bool ProcessMessage(int msg, IntPtr wParam, IntPtr lParam)
        {
            if (_disposed) return false;

            if (WmTaskbarCreated != 0 && msg == WmTaskbarCreated)
            {
                _isAdded = false;
                AddIcon();
                return true;
            }

            if (msg == WM_TRAYICON)
            {
                int mouseEvent = unchecked((int)(lParam.ToInt64() & 0xFFFF));
                if (mouseEvent == WM_LBUTTONUP)
                {
                    LeftClick?.Invoke();
                    return true;
                }
                if (mouseEvent == WM_RBUTTONUP)
                {
                    RightClick?.Invoke();
                    return true;
                }
                if (mouseEvent == WM_LBUTTONDBLCLK)
                {
                    DoubleClick?.Invoke();
                    return true;
                }
            }

            return false;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_isAdded)
            {
                NOTIFYICONDATA data = CreateData(0);
                Shell_NotifyIcon(NIM_DELETE, ref data);
                _isAdded = false;
            }
        }
    }
}
