using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MusicMediaKeys
{
    public partial class OverlaySettingsWindow : Window
    {
        private sealed record MonitorChoice(OverlayMonitorMode Mode, string DeviceName, string Label)
        {
            public override string ToString() => Label;
        }

        private readonly AppSettings _settings;
        private readonly Action? _onTestPreview;
        private bool _isInitializing = true;
        private bool _isDraggingSlider = false;
        private bool _hasPendingSave = false;
        private bool _isUpdatingMonitorCombo = false;

        public OverlaySettingsWindow(AppSettings settings, Action? onTestPreview = null)
        {
            InitializeComponent();
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _onTestPreview = onTestPreview;

            LoadSettingsToUi();
            LocalizeUi();
            _isInitializing = false;
        }

        private void LoadSettingsToUi()
        {
            GetPositionCell(_settings.OverlayPosition).IsChecked = true;
            ChkShowOnTrackChange.IsChecked = _settings.ShowOverlayOnTrackChange;
            ChkHideInFullscreen.IsChecked = _settings.HideOverlayInFullscreen;
            PopulateMonitorCombo();

            int duration = Math.Clamp(_settings.OverlayDurationSeconds, 1, 10);
            SldDuration.Value = duration;
            TxtDurationVal.Text = $"{duration} {Localization.GetString("UnitSecondsShort")}";

            int scalePercent = (int)Math.Round(Math.Clamp(_settings.OverlayScale, 0.75, 1.40) * 100);
            SldScale.Value = scalePercent;
            TxtScaleVal.Text = $"{scalePercent}%";
        }

        private void LocalizeUi()
        {
            Title = Localization.GetString("OverlaySettingsTitle");
            TxtTitle.Text = Localization.GetString("OverlaySettingsTitle");
            LblPositionHeader.Text = Localization.GetString("OverlayInteractiveScreenTitle");
            TxtPositionName.Text = GetPositionName(_settings.OverlayPosition);
            foreach (OverlayPosition position in Enum.GetValues<OverlayPosition>())
                CursorFollowToolTip.SetText(GetPositionCell(position), GetPositionName(position));
            ChkShowOnTrackChange.Content = Localization.GetString("OverlayShowOnTrackChange");
            CursorFollowToolTip.SetText(ChkShowOnTrackChange, Localization.GetString("TooltipOverlayShowOnTrackChange"));
            ChkHideInFullscreen.Content = Localization.GetString("OverlayHideInFullscreen");
            CursorFollowToolTip.SetText(ChkHideInFullscreen, Localization.GetString("TooltipOverlayHideInFullscreen"));
            LblMonitor.Text = Localization.GetString("OverlayMonitorLabel");
            PopulateMonitorCombo();
            LblDuration.Text = Localization.GetString("OverlayDurationLabel");
            LblScale.Text = Localization.GetString("OverlayScaleLabel");
            TxtPreviewBtn.Text = Localization.GetString("OverlayTestPreviewButton");
            TxtDoneBtn.Text = Localization.GetString("OverlayCloseButton");

            int duration = (int)SldDuration.Value;
            TxtDurationVal.Text = $"{duration} {Localization.GetString("UnitSecondsShort")}";
        }

        private RadioButton GetPositionCell(OverlayPosition position) => position switch
        {
            OverlayPosition.TopCenter => PosTopCenter,
            OverlayPosition.TopRight => PosTopRight,
            OverlayPosition.MiddleLeft => PosMiddleLeft,
            OverlayPosition.MiddleRight => PosMiddleRight,
            OverlayPosition.BottomLeft => PosBottomLeft,
            OverlayPosition.BottomCenter => PosBottomCenter,
            OverlayPosition.BottomRight => PosBottomRight,
            _ => PosTopLeft
        };

        private static string GetPositionName(OverlayPosition position) =>
            Localization.GetString("OverlayPosition_" + position);

        private void Position_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton cell || !Enum.TryParse(cell.Tag as string, out OverlayPosition position))
                return;

            TxtPositionName.Text = GetPositionName(position);
            if (_isInitializing) return;

            _settings.OverlayPosition = position;
            _settings.Save();
            _onTestPreview?.Invoke();
        }

        private void ChkShowOnTrackChange_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            _settings.ShowOverlayOnTrackChange = ChkShowOnTrackChange.IsChecked == true;
            _settings.Save();
        }

        private void ChkHideInFullscreen_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            _settings.HideOverlayInFullscreen = ChkHideInFullscreen.IsChecked == true;
            _settings.Save();
        }

        private void PopulateMonitorCombo()
        {
            if (ComboMonitor == null) return;
            _isUpdatingMonitorCombo = true;
            try
            {
                ComboMonitor.Items.Clear();
                string activeLabel = Localization.GetString("OverlayMonitorActive");
                string primaryLabel = Localization.GetString("OverlayMonitorPrimary");
                ComboMonitor.Items.Add(new MonitorChoice(OverlayMonitorMode.ActiveMonitor, "", activeLabel));
                ComboMonitor.Items.Add(new MonitorChoice(OverlayMonitorMode.PrimaryMonitor, "", primaryLabel));

                double maxBaseWidth = Math.Max(
                    MainWindow.MeasureTextWidth(activeLabel, ComboMonitor),
                    MainWindow.MeasureTextWidth(primaryLabel, ComboMonitor));
                ComboMonitor.Width = Math.Ceiling(maxBaseWidth) + 44;

                foreach (MonitorDescriptor monitor in OverlayPositioner.GetAvailableMonitors())
                {
                    ComboMonitor.Items.Add(new MonitorChoice(OverlayMonitorMode.SpecificMonitor, monitor.DeviceName, monitor.DisplayLabel));
                }

                int selectedIdx = 0;
                if (_settings.OverlayMonitorMode == OverlayMonitorMode.PrimaryMonitor)
                {
                    selectedIdx = 1;
                }
                else if (_settings.OverlayMonitorMode == OverlayMonitorMode.SpecificMonitor &&
                         !string.IsNullOrWhiteSpace(_settings.OverlayMonitorDeviceName))
                {
                    for (int i = 2; i < ComboMonitor.Items.Count; i++)
                    {
                        if (ComboMonitor.Items[i] is MonitorChoice choice &&
                            string.Equals(choice.DeviceName, _settings.OverlayMonitorDeviceName, StringComparison.OrdinalIgnoreCase))
                        {
                            selectedIdx = i;
                            break;
                        }
                    }
                }

                ComboMonitor.SelectedIndex = selectedIdx;
            }
            finally
            {
                _isUpdatingMonitorCombo = false;
            }
        }

        private void ComboMonitor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            StopMonitorMarquee();
            if (_isInitializing || _isUpdatingMonitorCombo || ComboMonitor.SelectedItem is not MonitorChoice choice)
                return;

            _settings.OverlayMonitorMode = choice.Mode;
            _settings.OverlayMonitorDeviceName = choice.DeviceName;
            _settings.Save();
            _onTestPreview?.Invoke();
        }

        private void ComboMonitor_MouseEnter(object sender, MouseEventArgs e)
        {
            if (ComboMonitor.IsDropDownOpen || ComboMonitor.SelectedItem is not MonitorChoice choice)
                return;

            ComboMonitor.ApplyTemplate();
            if (ComboMonitor.Template.FindName("PART_MarqueeClip", ComboMonitor) is not FrameworkElement clip ||
                ComboMonitor.Template.FindName("PART_MarqueeTransform", ComboMonitor) is not TranslateTransform transform)
            {
                return;
            }

            double viewportWidth = clip.ActualWidth;
            if (viewportWidth <= 0)
                viewportWidth = Math.Max(0, ComboMonitor.Width - 34);

            double textWidth = MainWindow.MeasureTextWidth(choice.Label, ComboMonitor);
            if (textWidth <= viewportWidth + 2)
                return;

            double overflow = textWidth - viewportWidth + 6;
            double scrollSeconds = Math.Max(0.8, overflow / 45.0);
            double totalSeconds = 0.3 + scrollSeconds + 0.8 + 0.25;

            var anim = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromSeconds(totalSeconds),
                RepeatBehavior = RepeatBehavior.Forever
            };
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.3))));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(-overflow, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.3 + scrollSeconds))));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(-overflow, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.3 + scrollSeconds + 0.8))));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(totalSeconds))));

            transform.BeginAnimation(TranslateTransform.XProperty, anim);
        }

        private void ComboMonitor_MouseLeave(object sender, MouseEventArgs e)
        {
            StopMonitorMarquee();
        }

        private void StopMonitorMarquee()
        {
            if (ComboMonitor?.Template?.FindName("PART_MarqueeTransform", ComboMonitor) is TranslateTransform transform)
            {
                transform.BeginAnimation(TranslateTransform.XProperty, null);
                transform.X = 0;
            }
        }

        private void SldDuration_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing || TxtDurationVal == null) return;
            int seconds = (int)Math.Round(e.NewValue);
            TxtDurationVal.Text = $"{seconds} {Localization.GetString("UnitSecondsShort")}";
            _settings.OverlayDurationSeconds = seconds;
            if (_isDraggingSlider)
                _hasPendingSave = true;
            else
                SaveNow();
        }

        private void SldScale_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing || TxtScaleVal == null) return;
            int percent = (int)Math.Round(e.NewValue);
            TxtScaleVal.Text = $"{percent}%";
            _settings.OverlayScale = percent / 100.0;
            if (_isDraggingSlider)
                _hasPendingSave = true;
            else
                SaveNow();
            _onTestPreview?.Invoke();
        }

        private void Slider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            _isDraggingSlider = true;
        }

        private void Slider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            _isDraggingSlider = false;
            FlushPendingSave();
        }

        private void Slider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDraggingSlider)
                FlushPendingSave();
        }

        private void SaveNow()
        {
            _hasPendingSave = false;
            _settings.Save();
        }

        private void FlushPendingSave()
        {
            if (_hasPendingSave)
                SaveNow();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            FlushPendingSave();
            base.OnClosing(e);
        }

        private void BtnTestPreview_Click(object sender, RoutedEventArgs e)
        {
            _onTestPreview?.Invoke();
        }

        private void BtnDone_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }
    }
}
