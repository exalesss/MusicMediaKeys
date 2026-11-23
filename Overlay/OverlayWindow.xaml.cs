using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace MusicMediaKeys
{
    public partial class OverlayWindow : Window
    {
        private readonly DispatcherTimer _hideTimer;
        private readonly DispatcherTimer _timelineTimer;
        private readonly DispatcherTimer _copiedBadgeTimer;
        private readonly AppSettings _settings;
        private readonly MediaController _mediaController;
        private readonly OverlayMediaSessionTracker _sessionTracker;
        private readonly Action<MediaCommand> _onMediaCommand;

        private bool _isDraggingVolume;
        private bool _isUpdatingVolumeSlider;
        private bool _isDraggingTimeline;
        private bool _isTimelinePointerDown;
        private bool _isUpdatingTimeline;
        private MediaVolumeService.VolumeSessionContext? _volumeContext;
        private (bool UseSystem, ApplicationSelectionMode App, string CustomName)? _volumeContextKey;

        private readonly DoubleAnimation _fadeInAnimation = new(0, 1, TimeSpan.FromMilliseconds(200));
        private readonly DoubleAnimation _quickFadeInAnimation = new(1, TimeSpan.FromMilliseconds(100));
        private readonly DoubleAnimation _fadeOutAnimation = new(1, 0, TimeSpan.FromMilliseconds(250));
        private readonly DoubleAnimation _hoverControlsShowAnim = new(1, TimeSpan.FromMilliseconds(150));
        private readonly DoubleAnimation _hoverControlsHideAnim = new(0, TimeSpan.FromMilliseconds(150));

        public OverlayWindow(AppSettings settings, MediaController mediaController, Action<MediaCommand>? onMediaCommand = null)
        {
            InitializeComponent();
            _settings = settings;
            _mediaController = mediaController;
            _onMediaCommand = onMediaCommand ?? (cmd => Task.Run(() => _mediaController.SendCommand(cmd)));

            _sessionTracker = new OverlayMediaSessionTracker(settings, Dispatcher);
            _sessionTracker.MetadataUpdated += OnSessionMetadataUpdated;
            _sessionTracker.SessionCleared += OnSessionCleared;
            _sessionTracker.SessionStateChanged += OnSessionStateChanged;

            UpdatePosition();

            _hideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _hideTimer.Tick += HideTimer_Tick;

            _timelineTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timelineTimer.Tick += (s, ev) =>
            {
                if (this.Visibility == Visibility.Visible && !_isDraggingTimeline && _sessionTracker.HasActiveSession)
                {
                    UpdateTimeline();
                }
                else
                {
                    _timelineTimer.Stop();
                }
            };

            _copiedBadgeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1200)
            };
            _copiedBadgeTimer.Tick += (s, ev) =>
            {
                _copiedBadgeTimer.Stop();
                BadgeCopied.Visibility = Visibility.Collapsed;
            };

            _fadeOutAnimation.Completed += (s, ev) =>
            {
                if (this.Opacity <= 0.05)
                {
                    this.Visibility = Visibility.Collapsed;
                    _timelineTimer.Stop();
                    _volumeContext?.Dispose();
                    _volumeContext = null;
                    _volumeContextKey = null;
                }
            };

            this.Opacity = 0;
            this.Visibility = Visibility.Collapsed;

            _sessionTracker.Start();
            UpdateVolumeSlider();
            UpdateTimeline();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            OverlayPositioner.ApplyNonActivatingStyles(this);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Cleanup();
        }

        public void Cleanup()
        {
            try
            {
                _hideTimer.Stop();
                _timelineTimer.Stop();
                _copiedBadgeTimer.Stop();
                _sessionTracker.MetadataUpdated -= OnSessionMetadataUpdated;
                _sessionTracker.SessionCleared -= OnSessionCleared;
                _sessionTracker.SessionStateChanged -= OnSessionStateChanged;
                _sessionTracker.Dispose();
                _volumeContext?.Dispose();
                _volumeContext = null;
            }
            catch { }
        }

        private TimeSpan GetConfiguredHideDuration()
        {
            int duration = _settings.OverlayDurationSeconds > 0 ? _settings.OverlayDurationSeconds : 3;
            return TimeSpan.FromSeconds(Math.Clamp(duration, 1, 10));
        }

        private void UpdatePosition()
        {
            double scale = Math.Clamp(_settings.OverlayScale > 0 ? _settings.OverlayScale : 1.0, 0.75, 1.40);
            if (OverlayScaleTransform != null)
            {
                OverlayScaleTransform.ScaleX = scale;
                OverlayScaleTransform.ScaleY = scale;
            }

            double dipWidth = 410 * scale;
            double dipHeight = 138 * scale;
            this.Width = dipWidth;
            this.Height = dipHeight;

            OverlayPositioner.PositionOverlay(
                this,
                dipWidth,
                dipHeight,
                _settings.OverlayPosition,
                _settings.OverlayMonitorMode,
                _settings.OverlayMonitorDeviceName);
        }

        private void OnSessionMetadataUpdated(OverlayTrackMetadata metadata)
        {
            TxtTitle.Text = metadata.Title;
            TxtArtist.Text = metadata.Artist;
            ImgThumbnail.Source = metadata.Thumbnail;
            ImgThumbnailBg.Source = metadata.Thumbnail;
            UpdatePlayPauseIcon();
            UpdateTimeline();

            if (!metadata.IsInitialUpdate && metadata.IsTrackChanged && _settings.ShowOverlay && _settings.ShowOverlayOnTrackChange)
            {
                ShowOverlay();
            }
        }

        private void OnSessionCleared()
        {
            TxtTitle.Text = Localization.GetString("NoActivePrograms");
            TxtArtist.Text = "";
            ImgThumbnail.Source = null;
            ImgThumbnailBg.Source = null;
            UpdatePlayPauseIcon();
            SetEmptyTimeline();
        }

        private void OnSessionStateChanged(bool updateVolume, bool updateTimeline)
        {
            UpdatePlayPauseIcon();
            if (updateVolume)
                UpdateVolumeSlider();
            if (updateTimeline)
                UpdateTimeline();
        }

        private void UpdatePlayPauseIcon()
        {
            if (TxtOverlayPlayPauseIcon != null)
            {
                TxtOverlayPlayPauseIcon.Text = _sessionTracker.IsPlaying ? "\uE769" : "\uE768";
            }
        }

        public void UpdateVolumeSlider()
        {
            if (_isDraggingVolume) return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    bool useSystem = _settings.UseSystemMediaKeysOnly;
                    var pids = useSystem ? null : _mediaController.GetTargetProcessIds();
                    float vol = _mediaController.VolumeService.GetVolume(useSystem, pids);
                    bool isMuted = _mediaController.VolumeService.GetMute(useSystem, pids);

                    _isUpdatingVolumeSlider = true;
                    try
                    {
                        SldVolume.ValueChanged -= SldVolume_ValueChanged;
                        SldVolume.Value = vol;
                        SldVolume.ValueChanged += SldVolume_ValueChanged;
                    }
                    finally
                    {
                        _isUpdatingVolumeSlider = false;
                    }

                    TxtVolumeValue.Text = $"{(int)Math.Round(vol * 100)}%";
                    IconSpeaker.Text = GetSpeakerIcon(isMuted, vol);
                }
                catch (Exception ex)
                {
                    EventLogService.Log("Overlay", "Failed to read the volume: " + ex.Message);
                }
            }));
        }

        private static string GetSpeakerIcon(bool isMuted, float vol)
        {
            if (isMuted || vol <= 0.001f) return "\uE74F";
            if (vol > 0.66f) return "\uE995";
            if (vol > 0.33f) return "\uE994";
            return "\uE993";
        }

        private void SldVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingVolumeSlider) return;

            try
            {
                if (_volumeContext != null)
                {
                    _volumeContext.SetVolume((float)SldVolume.Value);
                }
                else
                {
                    bool useSystem = _settings.UseSystemMediaKeysOnly;
                    var pids = useSystem ? null : _mediaController.GetTargetProcessIds();
                    _mediaController.VolumeService.SetVolume(useSystem, pids, (float)SldVolume.Value);
                }
                TxtVolumeValue.Text = $"{(int)Math.Round(SldVolume.Value * 100)}%";
                IconSpeaker.Text = GetSpeakerIcon(false, (float)SldVolume.Value);

                ResetHideTimer();
            }
            catch (Exception ex) { EventLogService.Log("Overlay", "Failed to apply the slider volume: " + ex.Message); }
        }

        private void SldVolume_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingVolume = true;
            _hideTimer.Stop();
            UpdateSliderFromPointer(SldVolume, e);
            SldVolume.CaptureMouse();
            e.Handled = true;
        }

        private void SldVolume_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingVolume && SldVolume.IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed)
            {
                UpdateSliderFromPointer(SldVolume, e);
                e.Handled = true;
            }
        }

        private void SldVolume_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDraggingVolume) return;
            _isDraggingVolume = false;
            if (SldVolume.IsMouseCaptured)
                SldVolume.ReleaseMouseCapture();
            ResetHideTimer();
            e.Handled = true;
        }

        private void SldVolume_LostMouseCapture(object sender, MouseEventArgs e)
        {
            if (_isDraggingVolume)
            {
                _isDraggingVolume = false;
                ResetHideTimer();
            }
        }

        private void IconSpeaker_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                bool useSystem = _settings.UseSystemMediaKeysOnly;
                var pids = useSystem ? null : _mediaController.GetTargetProcessIds();
                bool isMuted = _mediaController.VolumeService.GetMute(useSystem, pids);
                _mediaController.VolumeService.SetMute(useSystem, pids, !isMuted);
                UpdateVolumeSlider();
                ResetHideTimer();
            }
            catch (Exception ex)
            {
                EventLogService.Log("Overlay", "Failed to toggle mute: " + ex.Message);
            }
        }

        public void ShowOverlay(bool force = false, bool keepPreviewTimeline = false)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (!force && this.Visibility != Visibility.Visible && _settings.HideOverlayInFullscreen && OverlayPositioner.IsFullScreenAppRunning())
                        return;

                    _sessionTracker.RefreshSession();
                    IntPtr handle = new WindowInteropHelper(this).EnsureHandle();
                    UpdatePosition();

                    var targetKey = (
                        _settings.UseSystemMediaKeysOnly,
                        _settings.SelectedApplication,
                        KnownMediaSources.NormalizeCustomProcessName(_settings.CustomPlayerProcessName));

                    if (_volumeContext == null || _volumeContextKey != targetKey)
                    {
                        _volumeContext?.Dispose();
                        bool useSystem = _settings.UseSystemMediaKeysOnly;
                        var pids = useSystem ? null : _mediaController.GetTargetProcessIds();
                        _volumeContext = _mediaController.VolumeService.CreateSessionContext(useSystem, pids);
                        _volumeContextKey = targetKey;
                    }

                    UpdateVolumeSlider();
                    if (!keepPreviewTimeline || _sessionTracker.HasActiveSession)
                    {
                        UpdateTimeline();
                    }

                    _hideTimer.Stop();
                    _timelineTimer.Start();
                    _hideTimer.Interval = GetConfiguredHideDuration();

                    if (this.Visibility != Visibility.Visible)
                    {
                        OverlayPositioner.ShowNoActivate(handle);
                        this.Visibility = Visibility.Visible;
                        this.BeginAnimation(Window.OpacityProperty, _fadeInAnimation);
                    }
                    else if (this.Opacity < 1)
                    {
                        this.BeginAnimation(Window.OpacityProperty, _quickFadeInAnimation);
                    }

                    if (!this.IsMouseOver && !_isDraggingVolume && !_isDraggingTimeline)
                    {
                        _hideTimer.Start();
                    }
                }
                catch (Exception ex)
                {
                    EventLogService.Log("Overlay", "Failed to show the overlay: " + ex.Message);
                }
            }));
        }

        public void UpdateTimeline()
        {
            if (_isDraggingTimeline) return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (!_sessionTracker.TryGetTimeline(out TimeSpan pos, out TimeSpan duration))
                    {
                        SetEmptyTimeline();
                        return;
                    }

                    _isUpdatingTimeline = true;
                    try
                    {
                        SldTimeline.IsEnabled = true;
                        SldTimeline.Minimum = 0;
                        SldTimeline.Maximum = duration.TotalSeconds;
                        SldTimeline.Value = pos.TotalSeconds;
                    }
                    finally
                    {
                        _isUpdatingTimeline = false;
                    }

                    TxtTimelinePosition.Text = FormatTime(pos);
                    TxtTimelineDuration.Text = FormatTime(duration);
                }
                catch (Exception ex)
                {
                    EventLogService.Log("Overlay", "UpdateTimeline error: " + ex.Message);
                }
            }));
        }

        private void SetEmptyTimeline()
        {
            _isUpdatingTimeline = true;
            try
            {
                SldTimeline.IsEnabled = false;
                SldTimeline.Minimum = 0;
                SldTimeline.Maximum = 100;
                SldTimeline.Value = 0;
            }
            finally
            {
                _isUpdatingTimeline = false;
            }
            TxtTimelinePosition.Text = "0:00";
            TxtTimelineDuration.Text = "0:00";
        }

        private static string FormatTime(TimeSpan time)
        {
            if (time < TimeSpan.Zero) time = TimeSpan.Zero;
            if (time.TotalHours >= 1)
            {
                return $"{(int)time.TotalHours}:{time.Minutes:D2}:{time.Seconds:D2}";
            }
            return $"{(int)time.TotalMinutes}:{time.Seconds:D2}";
        }

        private void SldTimeline_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingTimeline) return;

            if (_isDraggingTimeline || _isTimelinePointerDown)
            {
                TxtTimelinePosition.Text = FormatTime(TimeSpan.FromSeconds(SldTimeline.Value));
            }
            ResetHideTimer();
        }

        private void SldTimeline_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!SldTimeline.IsEnabled || SldTimeline.Maximum <= SldTimeline.Minimum)
                return;

            _isTimelinePointerDown = true;
            _isDraggingTimeline = true;
            _hideTimer.Stop();
            UpdateSliderFromPointer(SldTimeline, e);
            TxtTimelinePosition.Text = FormatTime(TimeSpan.FromSeconds(SldTimeline.Value));
            SldTimeline.CaptureMouse();
            e.Handled = true;
        }

        private void SldTimeline_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingTimeline && SldTimeline.IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed)
            {
                UpdateSliderFromPointer(SldTimeline, e);
                TxtTimelinePosition.Text = FormatTime(TimeSpan.FromSeconds(SldTimeline.Value));
                e.Handled = true;
            }
        }

        private async void SldTimeline_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            bool wasDragging = _isDraggingTimeline || _isTimelinePointerDown;
            _isDraggingTimeline = false;
            _isTimelinePointerDown = false;
            if (SldTimeline.IsMouseCaptured)
                SldTimeline.ReleaseMouseCapture();
            if (!wasDragging) return;

            e.Handled = true;
            await CommitTimelineInteractionAsync();
        }

        private async void SldTimeline_LostMouseCapture(object sender, MouseEventArgs e)
        {
            if (_isDraggingTimeline || _isTimelinePointerDown)
            {
                _isDraggingTimeline = false;
                _isTimelinePointerDown = false;
                await CommitTimelineInteractionAsync();
            }
        }

        private async Task CommitTimelineInteractionAsync()
        {
            TxtTimelinePosition.Text = FormatTime(TimeSpan.FromSeconds(SldTimeline.Value));
            if (SldTimeline.Maximum > 5 && SldTimeline.Value >= SldTimeline.Maximum - 0.5)
            {
                _onMediaCommand(MediaCommand.Next);
            }
            else
            {
                await ApplyTimelineSeekAsync(SldTimeline.Value);
            }
            ResetHideTimer();
        }

        private static void UpdateSliderFromPointer(System.Windows.Controls.Slider slider, MouseEventArgs e)
        {
            double width = slider.ActualWidth;
            if (width <= 0 || slider.Maximum <= slider.Minimum)
                return;

            double ratio = Math.Clamp(e.GetPosition(slider).X / width, 0.0, 1.0);
            slider.Value = slider.Minimum + ratio * (slider.Maximum - slider.Minimum);
        }

        private async Task ApplyTimelineSeekAsync(double seconds)
        {
            if (_sessionTracker.HasActiveSession && SldTimeline.IsEnabled && SldTimeline.Maximum > 0)
            {
                await _sessionTracker.TrySeekAsync(seconds);
            }
        }

        public void ShowTestPreview()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                bool useDemo = !_sessionTracker.HasActiveSession ||
                    string.IsNullOrEmpty(TxtTitle.Text) ||
                    TxtTitle.Text == Localization.GetString("DiagnosticsNoTitle") ||
                    TxtTitle.Text == Localization.GetString("NoActivePrograms") ||
                    TxtTitle.Text == "Track Title";
                if (useDemo)
                {
                    TxtTitle.Text = "Demo Track (Music Media Keys)";
                    TxtArtist.Text = "Artist / Album";
                    _isUpdatingTimeline = true;
                    try
                    {
                        SldTimeline.IsEnabled = true;
                        SldTimeline.Minimum = 0;
                        SldTimeline.Maximum = 215;
                        SldTimeline.Value = 74;
                    }
                    finally
                    {
                        _isUpdatingTimeline = false;
                    }
                    TxtTimelinePosition.Text = "1:14";
                    TxtTimelineDuration.Text = "3:35";
                }
                ShowOverlay(force: true, keepPreviewTimeline: useDemo);
            }));
        }

        private void HideTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                _hideTimer.Stop();
                _timelineTimer.Stop();
                this.BeginAnimation(Window.OpacityProperty, _fadeOutAnimation);
            }
            catch { }
        }

        private void ResetHideTimer()
        {
            try
            {
                _hideTimer.Stop();
                _hideTimer.Interval = GetConfiguredHideDuration();
                if (!this.IsMouseOver && !_isDraggingVolume && !_isDraggingTimeline)
                {
                    _hideTimer.Start();
                }
            }
            catch { }
        }

        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
            try
            {
                _hideTimer.Stop();
                if (this.Opacity < 1)
                {
                    this.BeginAnimation(Window.OpacityProperty, _quickFadeInAnimation);
                }
                if (HoverControlsOverlay != null)
                {
                    HoverControlsOverlay.IsHitTestVisible = true;
                    HoverControlsOverlay.BeginAnimation(OpacityProperty, _hoverControlsShowAnim);
                }
            }
            catch { }
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double step = Math.Clamp(_settings.VolumeStepPercent, 1, 100) / 100.0;
            SldVolume.Value = Math.Clamp(SldVolume.Value + (e.Delta > 0 ? step : -step), 0, 1);
            e.Handled = true;
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            try
            {
                if (HoverControlsOverlay != null)
                {
                    HoverControlsOverlay.IsHitTestVisible = false;
                    HoverControlsOverlay.BeginAnimation(OpacityProperty, _hoverControlsHideAnim);
                }
                if (!_isDraggingVolume && !_isDraggingTimeline)
                {
                    _hideTimer.Interval = TimeSpan.FromSeconds(2);
                    _hideTimer.Start();
                }
            }
            catch { }
        }

        private void BtnOverlayPrev_Click(object sender, RoutedEventArgs e)
        {
            _onMediaCommand(MediaCommand.Previous);
            ResetHideTimer();
        }

        private void BtnOverlayPlayPause_Click(object sender, RoutedEventArgs e)
        {
            _onMediaCommand(MediaCommand.PlayPause);
            ResetHideTimer();
        }

        private void BtnOverlayNext_Click(object sender, RoutedEventArgs e)
        {
            _onMediaCommand(MediaCommand.Next);
            ResetHideTimer();
        }

        private void PanelTrackInfo_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                string title = TxtTitle.Text?.Trim() ?? "";
                string artist = TxtArtist.Text?.Trim() ?? "";
                if (string.IsNullOrEmpty(title) ||
                    title == Localization.GetString("NoActivePrograms") ||
                    title == Localization.GetString("DiagnosticsNoTitle") ||
                    title == "Track Title")
                {
                    return;
                }

                string trackText = string.IsNullOrEmpty(artist) ? title : $"{artist} — {title}";
                Clipboard.SetDataObject(trackText, true);

                TxtCopiedBadge.Text = Localization.GetString("OverlayCopiedBadge");
                BadgeCopied.Visibility = Visibility.Visible;
                _copiedBadgeTimer.Stop();
                _copiedBadgeTimer.Start();
                ResetHideTimer();
            }
            catch (Exception ex)
            {
                EventLogService.Log("Overlay", "Failed to copy track from overlay: " + ex.Message);
            }
        }
    }
}
