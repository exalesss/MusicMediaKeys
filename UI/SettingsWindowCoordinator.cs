using System;
using System.Drawing;
using System.Windows;

namespace MusicMediaKeys
{
    internal sealed class SettingsWindowCoordinator
    {
        private readonly Action _onApplicationSelectionChanged;
        private readonly Action _onHotkeysSaved;

        private MainWindow? _mainWindow;
        private Icon? _lastIcon;
        private string? _lastStatusText;
        private readonly Action? _onShowOverlayPreview;
        private readonly Action? _suspendHotkeys;
        private readonly Action? _resumeHotkeys;

        public SettingsWindowCoordinator(
            Action onApplicationSelectionChanged,
            Action onHotkeysSaved,
            Action? onShowOverlayPreview = null,
            Action? suspendHotkeys = null,
            Action? resumeHotkeys = null)
        {
            _onApplicationSelectionChanged = onApplicationSelectionChanged;
            _onHotkeysSaved = onHotkeysSaved;
            _onShowOverlayPreview = onShowOverlayPreview;
            _suspendHotkeys = suspendHotkeys;
            _resumeHotkeys = resumeHotkeys;
        }

        public bool IsVisible => _mainWindow != null && _mainWindow.IsLoaded && _mainWindow.IsVisible;

        public int CurrentTabIndex
        {
            get
            {
                if (_mainWindow == null || !_mainWindow.IsLoaded) return -1;
                if (_mainWindow.Dispatcher.CheckAccess())
                {
                    return _mainWindow.TabHotkeys.IsChecked == true ? 1 : 0;
                }
                return _mainWindow.Dispatcher.Invoke(() => _mainWindow.TabHotkeys.IsChecked == true ? 1 : 0);
            }
        }

        public void Hide()
        {
            if (_mainWindow != null && _mainWindow.IsLoaded)
            {
                RunOnUi(() => _mainWindow.Hide());
            }
        }

        public void Show(AppSettings settings, int tabIndex)
        {
            if (_mainWindow != null && _mainWindow.IsLoaded)
            {
                ShowExisting(tabIndex);
                return;
            }

            CreateNew(settings, tabIndex);
        }

        public void RefreshFromSettings()
        {
            if (_mainWindow == null || !_mainWindow.IsLoaded)
                return;

            RunOnUi(() => _mainWindow.LoadSettingsToUi());
        }

        public void UpdateIcon(Icon? icon, string? statusText = null)
        {
            if (ReferenceEquals(_lastIcon, icon) && string.Equals(_lastStatusText, statusText, StringComparison.Ordinal))
                return;

            _lastIcon = icon;
            _lastStatusText = statusText;

            if (_mainWindow == null || !_mainWindow.IsLoaded)
                return;

            ApplyIconToWindow(icon, statusText);
        }

        private void ApplyIconToWindow(Icon? icon, string? statusText)
        {
            RunOnUi(() =>
            {
                if (_mainWindow == null)
                    return;

                if (icon != null)
                {
                    try
                    {
                        using (Bitmap bmp = icon.ToBitmap())
                        {
                            IntPtr hBitmap = bmp.GetHbitmap();
                            try
                            {
                                var imageSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                                    hBitmap,
                                    IntPtr.Zero,
                                    Int32Rect.Empty,
                                    System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                                imageSource.Freeze();

                                _mainWindow.Icon = imageSource;
                                _mainWindow.AppIcon.Source = imageSource;
                            }
                            finally
                            {
                                NativeMethods.DeleteObject(hBitmap);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        EventLogService.Log("SettingsWindowCoordinator", "Failed to update window icon: " + ex.Message);
                    }
                }

                if (!string.IsNullOrWhiteSpace(statusText))
                {
                    CursorFollowToolTip.SetText(_mainWindow.AppIcon, statusText);
                }
            });
        }

        public void Dispose()
        {
            if (System.Windows.Application.Current != null)
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (_mainWindow != null)
                        {
                            _mainWindow.Close();
                            _mainWindow = null;
                        }
                    }
                    catch { }
                }));
            }
            else
            {
                _mainWindow = null;
            }
        }

        private void ShowExisting(int tabIndex)
        {
            if (_mainWindow == null) return;

            RunOnUi(() =>
            {
                BringToForeground(_mainWindow);
                SelectTab(tabIndex);
            });
        }

        private void CreateNew(AppSettings settings, int tabIndex)
        {
            RunOnUi(() =>
            {
                _mainWindow = new MainWindow(
                    settings,
                    _onHotkeysSaved,
                    _onApplicationSelectionChanged,
                    _onShowOverlayPreview,
                    _suspendHotkeys,
                    _resumeHotkeys);

                _mainWindow.Loaded += (s, e) =>
                {
                    if (_lastIcon != null || !string.IsNullOrWhiteSpace(_lastStatusText))
                    {
                        ApplyIconToWindow(_lastIcon, _lastStatusText);
                    }
                };

                _mainWindow.Closed += (s, e) =>
                {
                    _resumeHotkeys?.Invoke();
                    _mainWindow = null;
                };

                SelectTab(tabIndex);
                BringToForeground(_mainWindow);
            });
        }

        internal static void BringToForeground(Window window, bool showIfHidden = true)
        {
            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }

            Action activateAction = () =>
            {
                try
                {
                    var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                    ForceForeground(handle);
                }
                catch (Exception ex)
                {
                    EventLogService.Log("SettingsWindow", "Failed to bring the window to the foreground: " + ex.Message);
                }
                try
                {
                    window.Activate();
                    window.Focus();
                }
                catch (Exception ex)
                {
                    EventLogService.Log("SettingsWindow", "Failed to activate the window: " + ex.Message);
                }
            };

            if (window.IsLoaded)
            {
                if (showIfHidden && !window.IsVisible)
                {
                    window.Show();
                }
                activateAction();
            }
            else
            {
                RoutedEventHandler? loadedHandler = null;
                loadedHandler = (s, e) =>
                {
                    window.Loaded -= loadedHandler;
                    activateAction();
                };
                window.Loaded += loadedHandler;

                if (showIfHidden && !window.IsVisible)
                {
                    window.Show();
                }
            }
        }

        internal static void ForceForeground(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;

            IntPtr foregroundHwnd = NativeMethods.GetForegroundWindow();
            if (foregroundHwnd != hWnd)
            {
                uint foregroundThreadId = foregroundHwnd != IntPtr.Zero
                    ? NativeMethods.GetWindowThreadProcessId(foregroundHwnd, IntPtr.Zero)
                    : 0;
                uint currentThreadId = NativeMethods.GetCurrentThreadId();

                bool attached = false;
                if (foregroundThreadId != 0 && foregroundThreadId != currentThreadId)
                {
                    attached = NativeMethods.AttachThreadInput(currentThreadId, foregroundThreadId, true);
                }

                try
                {
                    NativeMethods.BringWindowToTop(hWnd);
                    NativeMethods.SetForegroundWindow(hWnd);
                }
                finally
                {
                    if (attached)
                    {
                        NativeMethods.AttachThreadInput(currentThreadId, foregroundThreadId, false);
                    }
                }
            }

            try
            {
                NativeMethods.FLASHWINFO fwi = new NativeMethods.FLASHWINFO
                {
                    cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.FLASHWINFO>(),
                    hwnd = hWnd,
                    dwFlags = NativeMethods.FLASHW_STOP,
                    uCount = 0,
                    dwTimeout = 0
                };
                NativeMethods.FlashWindowEx(ref fwi);
            }
            catch { }
        }

        private void SelectTab(int tabIndex)
        {
            if (_mainWindow == null) return;
            if (tabIndex == 0) _mainWindow.TabGeneral.IsChecked = true;
            else if (tabIndex == 1) _mainWindow.TabHotkeys.IsChecked = true;
        }

        private void RunOnUi(Action action)
        {
            var app = System.Windows.Application.Current;
            if (app == null || app.Dispatcher.HasShutdownStarted || app.Dispatcher.HasShutdownFinished)
                return;

            if (app.Dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                app.Dispatcher.Invoke(action);
            }
        }
    }
}
