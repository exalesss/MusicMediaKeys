using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Windows.Threading;

namespace MusicMediaKeys
{
    internal sealed class AppCoordinator : IDisposable
    {
        private const int STATUS_CHECK_INTERVAL_MS = 3000;
        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint WINEVENT_OUTOFCONTEXT = 0;

        private readonly Dispatcher _dispatcher;
        private readonly HwndSource _hwndSource;
        private readonly IntPtr _hwnd;
        private readonly AppTimers _timerCoordinator = new();
        private readonly SettingsWindowCoordinator _settingsWindowCoordinator;
        private readonly TrayIconAssets _trayIconAssets;
        private readonly HotkeyRuntimeState _hotkeyState = new();
        private readonly VolumeDuckingService _volumeDucking = new();
        private (bool UseSystem, ApplicationSelectionMode App, string CustomName)? _lastVolumeTargetKey;
        private readonly System.Threading.SemaphoreSlim _commandSemaphore = new(1, 1);
        private readonly object _statusLock = new();

        private NativeTrayIcon? _trayIcon;
        private HotkeyManager? _hotkeyManager;
        private MediaController? _mediaController;
        private AppSettings? _settings;
        private OverlayWindow? _overlayWindow;

        private bool? _lastIsRunning;
        private string? _lastTrayText;
        private int _statusCheckInProgress;
        private volatile bool _isHotkeysSuspended;

        private IntPtr _winEventHook = IntPtr.Zero;
        private WinEventProc? _winEventProc;
        private volatile bool _disposed;

        private delegate void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventProc lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        public AppCoordinator()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            _trayIconAssets = TrayIconService.LoadAssets();

            _settingsWindowCoordinator = new SettingsWindowCoordinator(
                onApplicationSelectionChanged: UpdateApplicationStatus,
                onHotkeysSaved: ApplyHotkeySettings,
                onShowOverlayPreview: () => _overlayWindow?.ShowTestPreview(),
                suspendHotkeys: () =>
                {
                    if (_hotkeyManager != null)
                    {
                        _hotkeyState.Reset(_hotkeyManager);
                    }
                },
                resumeHotkeys: () =>
                {
                    RegisterHotkeys();
                });

            var parameters = new HwndSourceParameters("MusicMediaKeys_CoordinatorHost")
            {
                WindowStyle = 0,
                ExtendedWindowStyle = 0x00000080,
                Width = 0,
                Height = 0,
                PositionX = -10000,
                PositionY = -10000
            };
            _hwndSource = new HwndSource(parameters);
            _hwndSource.AddHook(HwndHook);
            _hwnd = _hwndSource.Handle;

            InitializeTray();
            LoadSettings();
            UpdateApplicationStatus();

            if (_settings != null && _mediaController != null)
            {
                _overlayWindow = new OverlayWindow(_settings, _mediaController, cmd => SendMediaCommand(cmd));
            }

            InitializeHotkeys();
            InitializeForegroundHook();
            _timerCoordinator.StartUpdateChecks(TimeSpan.FromSeconds(20), TimeSpan.FromHours(12), CheckForUpdates);

            if (_settings?.PlayStartupSound == true)
            {
                AppSounds.PlayStartupSound(_settings.CustomStartupSoundFile);
            }

            if (_settings != null && _settings.StartWithWindowOpen)
            {
                _dispatcher.BeginInvoke(new Action(() =>
                {
                    try { ShowSettingsForm(); }
                    catch (Exception ex) { EventLogService.Log("Startup", "Failed to open the settings window: " + ex.Message); }
                }));
            }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (_trayIcon != null && _trayIcon.ProcessMessage(msg, wParam, lParam))
            {
                handled = true;
                return IntPtr.Zero;
            }

            if (msg == NativeMethods.WM_APPCOMMAND)
            {
                if (_isHotkeysSuspended)
                {
                    return IntPtr.Zero;
                }

                int cmd = (int)((lParam.ToInt64() >> 16) & 0xFFF);
                MediaCommand? toSend = WindowMessageRouting.TryMapAppCommand(cmd);
                if (!toSend.HasValue)
                {
                    return IntPtr.Zero;
                }

                AppCommandHandling handling = WindowMessageRouting.DecideAppCommandHandling(toSend, _settings);
                if (handling == AppCommandHandling.DispatchMediaCommand && _mediaController != null)
                {
                    SendMediaCommand(toSend.Value);
                    handled = true;
                    return (IntPtr)1;
                }
                handled = true;
                return (IntPtr)1;
            }

            if (msg == WindowMessageRouting.WmHotkey)
            {
                _hotkeyManager?.ProcessHotkeyMessage(msg, wParam, lParam);
                handled = true;
                return IntPtr.Zero;
            }

            return IntPtr.Zero;
        }

        private void InitializeTray()
        {
            StartStatusCheckTimer();
            _timerCoordinator.ScheduleTrayIconCreation(1200, (s, e) => CreateTrayIcon());
        }

        private void CreateTrayIcon()
        {
            if (_trayIcon != null) return;
            _trayIcon = TrayIconService.CreateNativeTrayIcon(_hwnd, _trayIconAssets.InitialIcon, OnTrayRightClick, OnTrayDoubleClick);
            lock (_statusLock)
            {
                _lastIsRunning = null;
                _lastTrayText = null;
            }
            UpdateApplicationStatus();
        }

        private void OnTrayRightClick()
        {
            _dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (_settings == null) return;
                    var options = new TrayMenuOptions
                    {
                        SelectedApplication = _settings.SelectedApplication,
                        CustomPlayerProcessName = _settings.CustomPlayerProcessName,
                        OnCyclePlayer = CycleTargetPlayer
                    };
                    TrayContextMenuService.Show(
                        _hwnd,
                        _settings.Theme,
                        () => ShowSettingsForm(false),
                        () => ShowSettingsForm(true),
                        Exit,
                        options);
                }
                catch (Exception ex)
                {
                    EventLogService.Log("TrayMenu", "Failed to display tray menu: " + ex.Message);
                }
            }));
        }

        private void OnTrayDoubleClick()
        {
            _dispatcher.BeginInvoke(new Action(() =>
            {
                ShowSettingsForm(openHotkeysTab: false);
            }));
        }

        private void StartStatusCheckTimer()
        {
            _timerCoordinator.StartStatusTimer(STATUS_CHECK_INTERVAL_MS, (s, e) => UpdateApplicationStatus());
        }

        private void UpdateApplicationStatus()
        {
            if (_disposed || _mediaController == null || _settings == null) return;
            var controller = _mediaController;
            var settings = _settings;
            controller.ApplySettings(settings);

            var currentTargetKey = (
                settings.UseSystemMediaKeysOnly,
                settings.SelectedApplication,
                KnownMediaSources.NormalizeCustomProcessName(settings.CustomPlayerProcessName));
            if (_lastVolumeTargetKey != null && _lastVolumeTargetKey.Value != currentTargetKey)
            {
                _volumeDucking.Reset();
            }
            _lastVolumeTargetKey = currentTargetKey;

            if (System.Threading.Interlocked.CompareExchange(ref _statusCheckInProgress, 1, 0) != 0)
                return;

            Task.Run(() =>
            {
                try
                {
                    if (_disposed) return;

                    bool anyAppMode = settings.UseSystemMediaKeysOnly;
                    bool suspended = _isHotkeysSuspended;
                    bool isRunning = (anyAppMode || suspended) ? false : controller.IsApplicationRunning();
                    MediaApplication activeApplication = (anyAppMode || suspended) ? MediaApplication.YandexMusic : controller.GetActiveApplication();
                    string trayText = TrayStatusService.BuildTrayText(settings, isRunning, activeApplication, suspended);

                    bool changed;
                    lock (_statusLock)
                    {
                        changed = _lastIsRunning != isRunning || _lastTrayText != trayText;
                    }

                    if (changed && !_disposed && !_dispatcher.HasShutdownStarted)
                    {
                        _dispatcher.BeginInvoke(new Action(() => ApplyStatusUpdate(isRunning, trayText, anyAppMode, suspended)));
                    }
                }
                catch (Exception ex)
                {
                    EventLogService.Log("Status", "Status check error: " + ex.Message);
                }
                finally
                {
                    System.Threading.Interlocked.Exchange(ref _statusCheckInProgress, 0);
                }
            });
        }

        private void ApplyStatusUpdate(bool isRunning, string trayText, bool anyAppMode, bool hotkeysSuspended)
        {
            try
            {
                if (_disposed || _mediaController == null || _settings == null) return;
                lock (_statusLock)
                {
                    _lastIsRunning = isRunning;
                    _lastTrayText = trayText;
                }
                Icon iconToUse = TrayStatusService.ShouldUseActiveIcon(isRunning, anyAppMode, hotkeysSuspended)
                    ? _trayIconAssets.ActiveIcon
                    : _trayIconAssets.ErrorIcon;

                _settingsWindowCoordinator.UpdateIcon(iconToUse, trayText);
                if (_trayIcon != null)
                {
                    _trayIcon.SetIcon(iconToUse);
                    _trayIcon.SetText(trayText);
                }
            }
            catch (Exception ex)
            {
                EventLogService.Log("TrayIcon", "Failed to update tray icon: " + ex.Message);
            }
        }

        private void LoadSettings()
        {
            try
            {
                _settings = AppSettings.Load();
                CursorFollowToolTip.IsEnabled = _settings.ShowTooltips;
                _mediaController = new MediaController();
                _mediaController.ApplySettings(_settings);

                ThemeService.ApplyTheme(System.Windows.Application.Current, _settings.Theme);
                Localization.SetLanguage(_settings.Language);
                Localization.LanguageChanged += Localization_LanguageChanged;

                if (!AppPaths.IsPortableMode && _settings.AutoStart)
                {
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            if (ElevationManager.IsRunningAsAdministrator() && !AutoStartManager.IsEnabledForCurrentExecutable())
                            {
                                AutoStartManager.Enable();
                            }
                        }
                        catch (Exception ex)
                        {
                            EventLogService.Log("AutoStart", "Failed to register the autostart task: " + ex.Message);
                        }
                    });
                }
                EventLogService.Log("Settings", "Settings loaded successfully");
            }
            catch (Exception ex)
            {
                _settings = new AppSettings();
                CursorFollowToolTip.IsEnabled = _settings.ShowTooltips;
                _mediaController = new MediaController();
                _mediaController.ApplySettings(_settings);
                ThemeService.ApplyTheme(System.Windows.Application.Current, _settings.Theme);
                Localization.SetLanguage(_settings.Language);
                Localization.LanguageChanged += Localization_LanguageChanged;
                EventLogService.Log("Settings", "Settings load failed, defaults applied: " + ex.Message);
            }
        }

        private void Localization_LanguageChanged(object? sender, EventArgs e)
        {
            UpdateApplicationStatus();
        }

        private void InitializeHotkeys()
        {
            try
            {
                _hotkeyManager = new HotkeyManager(_hwnd);
                RegisterHotkeys();
                EventLogService.Log("Hotkeys", "Hotkeys registered via native RegisterHotKey");
            }
            catch (Exception ex)
            {
                EventLogService.Log("Hotkeys", "Hotkey initialization failed: " + ex.Message);
            }
        }

        private void InitializeForegroundHook()
        {
            if (_winEventHook == IntPtr.Zero)
            {
                _winEventProc = new WinEventProc(OnForegroundWindowChanged);
                _winEventHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _winEventProc, 0, 0, WINEVENT_OUTOFCONTEXT);
            }
        }

        private void OnForegroundWindowChanged(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            try
            {
                _mediaController?.RefreshLastActiveWindow();
            }
            catch (Exception ex)
            {
                EventLogService.Log("Routing", "Failed to track the foreground window: " + ex.Message);
            }
        }

        private void SendMediaCommand(MediaCommand command, IntPtr foregroundAtKeyPress = default)
        {
            if (_disposed || _mediaController == null || _settings == null) return;
            var controller = _mediaController;
            var settings = _settings;

            Task.Run(async () =>
            {
                await _commandSemaphore.WaitAsync();
                try
                {
                    if (_disposed) return;

                    var result = MediaCommandDispatchService.Send(controller, settings, command, foregroundAtKeyPress);
                    EventLogService.Log("Command", MediaCommandDispatchService.BuildStatusText(command, result.Success, result.Details));
                    if (!result.Success)
                    {
                        WarningSound.Play();
                    }
                    if (result.Success && settings.ShowOverlay && !_disposed && !_dispatcher.HasShutdownStarted)
                    {
                        await _dispatcher.InvokeAsync(() => _overlayWindow?.ShowOverlay());
                    }
                }
                catch (Exception ex)
                {
                    EventLogService.Log("Command", $"Dispatch error for {command}: {ex.Message}");
                }
                finally
                {
                    _commandSemaphore.Release();
                }
            });
        }

        private void RegisterHotkeys()
        {
            if (_hotkeyManager == null || _settings == null || _mediaController == null)
                return;

            _hotkeyState.Reset(_hotkeyManager);

            var result = HotkeyRegistrationExecutionService.RegisterAll(
                _settings,
                _hotkeyManager,
                _hotkeyState,
                SendMediaCommand,
                GetAppAction,
                NativeMethods.GetForegroundWindow,
                onlyAllowResumeAction: _isHotkeysSuspended);

            if (result.FailedLabelKeys.Count > 0)
            {
                EventLogService.Log("Hotkeys", "Failed to register hotkeys: " + string.Join(", ", result.FailedLabelKeys));
            }
        }

        private Action GetAppAction(string actionId)
        {
            return () =>
            {
                _dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        switch (actionId)
                        {
                            case AppActions.ToggleMainWindow:
                                if (_settingsWindowCoordinator.IsVisible && _settingsWindowCoordinator.CurrentTabIndex == 0)
                                    _settingsWindowCoordinator.Hide();
                                else if (_settings != null)
                                    _settingsWindowCoordinator.Show(_settings, tabIndex: 0);
                                break;

                            case AppActions.OpenHotkeysTab:
                                if (_settingsWindowCoordinator.IsVisible && _settingsWindowCoordinator.CurrentTabIndex == 1)
                                    _settingsWindowCoordinator.Hide();
                                else if (_settings != null)
                                    _settingsWindowCoordinator.Show(_settings, tabIndex: 1);
                                break;

                            case AppActions.ToggleOverlay:
                                if (_settings != null)
                                {
                                    _settings.ShowOverlay = !_settings.ShowOverlay;
                                    _settings.Save();
                                    _settingsWindowCoordinator.RefreshFromSettings();
                                    if (_settings.ShowOverlay)
                                    {
                                        _overlayWindow?.ShowTestPreview();
                                    }
                                }
                                break;

                            case AppActions.ToggleControlAnyPlayer:
                                if (_settings != null)
                                {
                                    _settings.UseSystemMediaKeysOnly = !_settings.UseSystemMediaKeysOnly;
                                    _settings.Save();
                                    RegisterHotkeys();
                                    UpdateApplicationStatus();
                                    _settingsWindowCoordinator.RefreshFromSettings();
                                }
                                break;

                            case AppActions.CycleTargetPlayer:
                                CycleTargetPlayer();
                                break;

                            case AppActions.ShowOverlayNow:
                                _overlayWindow?.ShowTestPreview();
                                break;

                            case AppActions.CopyTrackInfo:
                                Task.Run(async () =>
                                {
                                    try
                                    {
                                        string? track = await WindowsMediaSessionService.TryGetCurrentTrackStringAsync();
                                        if (!string.IsNullOrWhiteSpace(track) && !_disposed && !_dispatcher.HasShutdownStarted)
                                        {
                                            await _dispatcher.InvokeAsync(() =>
                                            {
                                                try
                                                {
                                                    System.Windows.Clipboard.SetDataObject(track, true);
                                                    _overlayWindow?.ShowTestPreview();
                                                }
                                                catch (Exception ex)
                                                {
                                                    EventLogService.Log("CopyTrackInfo", "Clipboard error: " + ex.Message);
                                                }
                                            });
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        EventLogService.Log("CopyTrackInfo", "Failed to copy track info: " + ex.Message);
                                    }
                                });
                                break;

                            case AppActions.SeekBackward:
                                ExecuteSeekAction(-1, "SeekBackward");
                                break;

                            case AppActions.SeekForward:
                                ExecuteSeekAction(1, "SeekForward");
                                break;

                            case AppActions.ToggleVolumeDucking:
                                ExecuteVolumeDuckingAction();
                                break;

                            case AppActions.FocusTargetPlayer:
                                if (_mediaController == null || !_mediaController.TryFocusTargetPlayer())
                                {
                                    WarningSound.Play();
                                }
                                break;

                            case AppActions.ToggleHotkeysSuspended:
                                ToggleHotkeysSuspended();
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        EventLogService.Log("AppAction", $"Error executing action {actionId}: {ex.Message}");
                    }
                }));
            };
        }

        private (ApplicationSelectionMode Mode, string CustomName) CycleTargetPlayer()
        {
            if (_settings == null)
                return (ApplicationSelectionMode.Auto, "");

            var modes = new System.Collections.Generic.List<ApplicationSelectionMode> { ApplicationSelectionMode.Auto };
            if (_mediaController != null)
            {
                foreach (ApplicationSelectionMode detected in _mediaController.GetDetectedRunningModes())
                {
                    if (!modes.Contains(detected))
                        modes.Add(detected);
                }
            }

            int idx = modes.IndexOf(_settings.SelectedApplication);
            _settings.SelectedApplication = idx >= 0
                ? modes[(idx + 1) % modes.Count]
                : ApplicationSelectionMode.Auto;

            _settings.Save();
            UpdateApplicationStatus();
            _settingsWindowCoordinator.RefreshFromSettings();
            return (_settings.SelectedApplication, _settings.CustomPlayerProcessName);
        }

        internal void ToggleHotkeysSuspended()
        {
            _isHotkeysSuspended = !_isHotkeysSuspended;
            RegisterHotkeys();
            UpdateApplicationStatus();
            EventLogService.Log("Hotkeys", _isHotkeysSuspended ? "Hotkeys suspended" : "Hotkeys resumed");
        }

        private void ExecuteVolumeDuckingAction()
        {
            if (_disposed || _mediaController == null || _settings == null)
                return;

            var controller = _mediaController;
            var settings = _settings;

            Task.Run(async () =>
            {
                await _commandSemaphore.WaitAsync();
                try
                {
                    if (_disposed) return;

                    bool useSystem = settings.UseSystemMediaKeysOnly;
                    var pids = useSystem ? null : controller.GetTargetProcessIds();
                    float targetLevel = Math.Clamp(settings.VolumeDuckLevelPercent, 1, 90) / 100f;

                    bool ducked = _volumeDucking.Toggle(
                        targetLevel,
                        () => controller.VolumeService.GetVolume(useSystem, pids),
                        v => controller.VolumeService.SetVolume(useSystem, pids, v));

                    EventLogService.Log("VolumeDucking", ducked ? $"Volume ducked to {settings.VolumeDuckLevelPercent}%" : "Volume restored");

                    if (settings.ShowOverlay && !_disposed && !_dispatcher.HasShutdownStarted)
                    {
                        await _dispatcher.InvokeAsync(() => _overlayWindow?.ShowOverlay());
                    }
                }
                catch (Exception ex)
                {
                    EventLogService.Log("VolumeDucking", "Failed to toggle volume ducking: " + ex.Message);
                }
                finally
                {
                    _commandSemaphore.Release();
                }
            });
        }

        private void ExecuteSeekAction(int direction, string logCategory)
        {
            Task.Run(async () =>
            {
                try
                {
                    int step = (_settings != null && _settings.SeekStepSeconds > 0) ? _settings.SeekStepSeconds : 10;
                    await WindowsMediaSessionService.TrySeekAsync(direction * step);
                    if (_settings != null && _settings.ShowOverlay && !_disposed && !_dispatcher.HasShutdownStarted)
                    {
                        await _dispatcher.InvokeAsync(() => _overlayWindow?.ShowOverlay());
                    }
                }
                catch (Exception ex)
                {
                    EventLogService.Log(logCategory, $"Failed to seek ({logCategory}): " + ex.Message);
                }
            });
        }

        private async void CheckForUpdates()
        {
            try
            {
                if (_settings?.CheckForUpdates == true)
                    await UpdateService.CheckAsync();
            }
            catch (Exception ex)
            {
                EventLogService.Log("Update", "Scheduled update check error: " + ex.Message);
            }
        }

        public void Exit()
        {
            Dispose();
            System.Windows.Application.Current?.Shutdown();
            Environment.Exit(0);
        }

        private void ShowSettingsForm(bool openHotkeysTab = false)
        {
            if (_settings == null)
                return;
            _settingsWindowCoordinator.Show(_settings, openHotkeysTab ? 1 : 0);
            bool isRunning;
            string? trayText;
            lock (_statusLock)
            {
                isRunning = _lastIsRunning == true;
                trayText = _lastTrayText;
            }
            Icon iconToUse = TrayStatusService.ShouldUseActiveIcon(isRunning, _settings?.UseSystemMediaKeysOnly == true)
                ? _trayIconAssets.ActiveIcon
                : _trayIconAssets.ErrorIcon;
            _settingsWindowCoordinator.UpdateIcon(iconToUse, trayText);
        }

        private void ApplyHotkeySettings()
        {
            if (_settings == null) return;
            _mediaController?.ApplySettings(_settings);
            RegisterHotkeys();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_winEventHook != IntPtr.Zero)
            {
                UnhookWinEvent(_winEventHook);
                _winEventHook = IntPtr.Zero;
                _winEventProc = null;
            }

            Localization.LanguageChanged -= Localization_LanguageChanged;
            AppSounds.StopAllSounds();
            _timerCoordinator.Dispose();
            _hotkeyManager?.Dispose();
            _settingsWindowCoordinator.Dispose();

            OverlayWindow? overlay = _overlayWindow;
            _overlayWindow = null;
            if (overlay != null)
            {
                if (overlay.Dispatcher.CheckAccess())
                {
                    try { overlay.Close(); } catch { }
                }
                else if (!overlay.Dispatcher.HasShutdownStarted)
                {
                    overlay.Dispatcher.BeginInvoke(new Action(() => { try { overlay.Close(); } catch { } }));
                }
            }

            _trayIcon?.Dispose();
            _trayIcon = null;
            _trayIconAssets.Dispose();
            _hwndSource.Dispose();
        }
    }
}
