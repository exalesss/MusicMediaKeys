using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace MusicMediaKeys
{
    public partial class MainWindow : Window
    {
        private readonly AppSettings _settings;
        private readonly Action _onSave;
        private readonly Action _onAppSelectionChanged;
        private readonly Action? _onShowOverlayPreview;
        private readonly Action? _suspendHotkeys;
        private readonly Action? _resumeHotkeys;
        private readonly HotkeyRecorder _hotkeyRecorder;

        public MainWindow(
            AppSettings settings, 
            Action onSave, 
            Action onAppSelectionChanged,
            Action? onShowOverlayPreview = null,
            Action? suspendHotkeys = null,
            Action? resumeHotkeys = null)
        {
            _settings = settings;
            _onSave = onSave;
            _onAppSelectionChanged = onAppSelectionChanged;
            _onShowOverlayPreview = onShowOverlayPreview;
            _suspendHotkeys = suspendHotkeys;
            _resumeHotkeys = resumeHotkeys;

            ThemeService.ApplyTheme(System.Windows.Application.Current, _settings.Theme);
            InitializeComponent();
            ModalAttention.Attach(this);
            TxtVersion.Text = AppPaths.IsPortableMode
                ? $"v{AppInfo.Version} · Portable"
                : $"v{AppInfo.Version}";

            _hotkeyRecorder = new HotkeyRecorder(this, GetBindingForTextBox, SetBindingForTextBox, HotkeyRecorder.AllPresets);
            _hotkeyRecorder.RecordingChanged += isRecording =>
            {
                if (isRecording)
                {
                    _suspendHotkeys?.Invoke();
                }
                else
                {
                    _resumeHotkeys?.Invoke();
                    ValidateHotkeys();
                }
            };
            _hotkeyRecorder.BindingChanged += field =>
            {
                _settings.Save();
                _onSave();
                ValidateHotkeys();
            };
            foreach (TextBox field in new[] { TxtPrev, TxtPlay, TxtNext, TxtVolDown, TxtVolUp, TxtVolMute })
                _hotkeyRecorder.Attach(field);

            AppIcon.Source = TrayIconService.LoadTitleBarIcon();
            LoadSettingsToUi();
            LocalizeUi();
            ValidateHotkeys();
            UpdateWindowHeight();
        }

        private sealed record StepChoice(int? Value, string Label)
        {
            public override string ToString() => Label;
        }

        private sealed class StepComboConfig
        {
            public required int[] StandardSteps { get; init; }
            public required int MinValue { get; init; }
            public required int MaxValue { get; init; }
            public required Func<int, string> FormatValue { get; init; }
            public required string CustomMenuKey { get; init; }
            public required string PromptTitleKey { get; init; }
            public required string PromptMessageKey { get; init; }
            public required string InvalidMessageKey { get; init; }
            public required Func<AppSettings, int> Getter { get; init; }
            public required Action<AppSettings, int> Setter { get; init; }
        }

        private static readonly StepComboConfig VolumeStepConfig = new()
        {
            StandardSteps = new[] { 2, 5, 10 },
            MinValue = 1,
            MaxValue = 100,
            FormatValue = step => $"{step}%",
            CustomMenuKey = "VolumeStepCustom",
            PromptTitleKey = "VolumeStepPromptTitle",
            PromptMessageKey = "VolumeStepPromptMessage",
            InvalidMessageKey = "VolumeStepInvalidMessage",
            Getter = s => s.VolumeStepPercent,
            Setter = (s, v) => s.VolumeStepPercent = v
        };

        private static readonly StepComboConfig SeekStepConfig = new()
        {
            StandardSteps = new[] { 5, 10, 15 },
            MinValue = 1,
            MaxValue = 300,
            FormatValue = step => $"{step} {Localization.GetString("UnitSecondsShort")}",
            CustomMenuKey = "SeekStepCustom",
            PromptTitleKey = "SeekStepPromptTitle",
            PromptMessageKey = "SeekStepPromptMessage",
            InvalidMessageKey = "SeekStepInvalidMessage",
            Getter = s => s.SeekStepSeconds,
            Setter = (s, v) => s.SeekStepSeconds = v
        };

        private static readonly StepComboConfig VolumeDuckLevelConfig = new()
        {
            StandardSteps = new[] { 10, 15, 25, 40 },
            MinValue = 1,
            MaxValue = 90,
            FormatValue = step => $"{step}%",
            CustomMenuKey = "VolumeDuckLevelCustom",
            PromptTitleKey = "VolumeDuckLevelPromptTitle",
            PromptMessageKey = "VolumeDuckLevelPromptMessage",
            InvalidMessageKey = "VolumeDuckLevelInvalidMessage",
            Getter = s => s.VolumeDuckLevelPercent,
            Setter = (s, v) => s.VolumeDuckLevelPercent = v
        };

        private static readonly ApplicationSelectionMode[] TargetPlayerModes =
        {
            ApplicationSelectionMode.Auto,
            ApplicationSelectionMode.Spotify,
            ApplicationSelectionMode.YandexMusic,
            ApplicationSelectionMode.AppleMusic,
            ApplicationSelectionMode.Aimp,
            ApplicationSelectionMode.Foobar2000,
            ApplicationSelectionMode.Vlc,
            ApplicationSelectionMode.YouTubeMusicDesktop,
            ApplicationSelectionMode.Custom
        };

        private bool _isUpdatingUi = true;
        private bool _isUpdatingTargetPlayerCombo = false;
        private bool _isUpdatingVolumeStepCombo = false;
        private bool _isUpdatingSeekStepCombo = false;
        private bool _isUpdatingVolumeDuckLevelCombo = false;

        private void PopulateTargetPlayerCombo()
        {
            if (ComboTargetPlayer == null) return;
            _isUpdatingTargetPlayerCombo = true;
            try
            {
                ComboTargetPlayer.Items.Clear();
                foreach (ApplicationSelectionMode mode in TargetPlayerModes)
                {
                    string label = mode == ApplicationSelectionMode.Auto
                        ? Localization.GetString("Auto")
                        : Localization.GetString(KnownMediaSources.GetDescriptor(KnownMediaSources.ToMediaApplication(mode)).LocalizationKey);
                    ComboTargetPlayer.Items.Add(label);
                }

                int idx = Array.IndexOf(TargetPlayerModes, _settings.SelectedApplication);
                ComboTargetPlayer.SelectedIndex = idx >= 0 ? idx : 0;
            }
            finally
            {
                _isUpdatingTargetPlayerCombo = false;
            }
        }

        private void PopulateVolumeStepCombo() =>
            PopulateStepCombo(ComboVolumeStep, VolumeStepConfig, value => _isUpdatingVolumeStepCombo = value);

        private void PopulateSeekStepCombo() =>
            PopulateStepCombo(ComboSeekStep, SeekStepConfig, value => _isUpdatingSeekStepCombo = value);

        private void PopulateVolumeDuckLevelCombo() =>
            PopulateStepCombo(ComboVolumeDuckLevel, VolumeDuckLevelConfig, value => _isUpdatingVolumeDuckLevelCombo = value);

        private void PopulateStepCombo(ComboBox? combo, StepComboConfig config, Action<bool> setUpdatingFlag)
        {
            if (combo == null) return;
            setUpdatingFlag(true);
            try
            {
                combo.Items.Clear();
                int currentStep = config.Getter(_settings);

                bool currentFound = false;
                foreach (int step in config.StandardSteps)
                {
                    if (step == currentStep) currentFound = true;
                    combo.Items.Add(new StepChoice(step, config.FormatValue(step)));
                }

                if (!currentFound && currentStep >= config.MinValue && currentStep <= config.MaxValue)
                {
                    int insertIdx = 0;
                    while (insertIdx < config.StandardSteps.Length && config.StandardSteps[insertIdx] < currentStep)
                    {
                        insertIdx++;
                    }
                    combo.Items.Insert(insertIdx, new StepChoice(currentStep, config.FormatValue(currentStep)));
                }

                combo.Items.Add(new StepChoice(null, Localization.GetString(config.CustomMenuKey) + "..."));

                for (int i = 0; i < combo.Items.Count - 1; i++)
                {
                    if (combo.Items[i] is StepChoice choice && choice.Value == currentStep)
                    {
                        combo.SelectedIndex = i;
                        break;
                    }
                }
            }
            finally
            {
                setUpdatingFlag(false);
            }
        }

        private void LocalizeUi()
        {
            TabGeneral.Content = Localization.GetString("General");
            TabHotkeys.Content = Localization.GetString("Hotkeys");

            PopulateThemeCombo();

            ComboLang.SelectionChanged -= ComboLang_SelectionChanged;
            IReadOnlyList<Language> availableLangs = _settings.GetAvailableLanguages();
            LanguageThemeChoices.FillLanguages(ComboLang, _settings.Language, availableLangs);
            ComboLang.IsEnabled = availableLangs.Count > 1;
            BtnLangToggle.Visibility = availableLangs.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            ComboLang.SelectionChanged += ComboLang_SelectionChanged;

            PopulateVolumeStepCombo();
            PopulateSeekStepCombo();
            PopulateVolumeDuckLevelCombo();
            PopulateTargetPlayerCombo();

            TxtOverlaySettingsBtn.Text = Localization.GetString("OverlaySettingsButton");
            CursorFollowToolTip.SetText(BtnOverlaySettings, Localization.GetString("OverlaySettingsTitle"));

            LblAppActionsHeader.Text = Localization.GetString("AppActionsHeader");
            string addActionLabel = Localization.GetString("BtnAddActionHotkey");
            ComboAddAction.Tag = addActionLabel;
            ComboAddAction.Width = Math.Ceiling(MeasureTextWidth(addActionLabel, ComboAddAction, FontWeights.SemiBold)) + 44;
            CursorFollowToolTip.SetText(ComboAddAction, Localization.GetString("TooltipAddActionHotkey"));
            PopulateAppActionsUi();

            LblTargetPlayerHeader.Text = Localization.GetString("ManagedPlayer");
            LblOperationModeHeader.Text = Localization.GetString("OperationMode");
            LblLaunchHeader.Text = Localization.GetString("LaunchAndSystem");
            LblThemeHeader.Text = Localization.GetString("ThemeHeader");
            LblLanguageHeader.Text = Localization.GetString("LanguageHeader");

            TxtBrowseCustomPlayerBtn.Text = Localization.GetString("BrowseExeButton");
            CursorFollowToolTip.SetText(ComboTargetPlayer, Localization.GetString("TooltipTargetPlayer"));
            CursorFollowToolTip.SetText(TxtCustomPlayerProcess, Localization.GetString("TooltipCustomPlayerProcess"));

            ChkControlAny.Content = Localization.GetString("SystemMediaKeysOnly");
            ChkBlockBrowser.Content = Localization.GetString("BlockMediaKeysForBrowser");
            ChkShowOverlay.Content = Localization.GetString("ShowOverlayHeader");
            LblVolumeStepHeader.Text = Localization.GetString("VolumeStepHeader");
            LblSeekStepHeader.Text = Localization.GetString("SeekStepHeader");
            LblVolumeDuckLevelHeader.Text = Localization.GetString("VolumeDuckLevelHeader");
            ChkSkipUac.Content = Localization.GetString("SkipUacPromptHeader");
            ChkStartOpen.Content = Localization.GetString("StartWithWindowOpen");
            ChkStartupSound.Content = Localization.GetString("PlayStartupSound");
            TxtChooseStartupSoundBtn.Text = Localization.GetString("StartupSoundChooseButton");
            CursorFollowToolTip.SetText(BtnChooseStartupSound, Localization.GetString("TooltipChooseStartupSound"));
            CursorFollowToolTip.SetText(BtnResetStartupSound, Localization.GetString("TooltipResetStartupSound"));
            ChkAutostart.Content = Localization.GetString("AutoStart");
            ChkCheckUpdates.Content = Localization.GetString("CheckForUpdates");
            TxtExportSettingsBtn.Text = Localization.GetString("ExportSettingsButton");
            TxtImportSettingsBtn.Text = Localization.GetString("ImportSettingsButton");

            TxtHotkeysHint.Text = Localization.GetString("HotkeysHint");
            TxtVolumeHint.Text = Localization.GetString("VolumeMixerHint");
            LblPrev.Text = Localization.GetString("PreviousTrack");
            LblPlay.Text = Localization.GetString("PlayPause");
            LblNext.Text = Localization.GetString("NextTrack");
            LblVolDown.Text = Localization.GetString("VolumeDown");
            LblVolUp.Text = Localization.GetString("VolumeUp");
            LblVolMute.Text = Localization.GetString("VolumeMute");
            CursorFollowToolTip.SetText(ChkControlAny, Localization.GetString("TooltipSystemMediaKeysOnly"));
            CursorFollowToolTip.SetText(ChkBlockBrowser, Localization.GetString("TooltipBlockMediaKeysForBrowser"));
            CursorFollowToolTip.SetText(ChkShowOverlay, Localization.GetString("TooltipShowOverlay"));
            CursorFollowToolTip.SetText(ComboVolumeStep, Localization.GetString("TooltipVolumeStep"));
            CursorFollowToolTip.SetText(ComboSeekStep, Localization.GetString("TooltipSeekStep"));
            CursorFollowToolTip.SetText(ComboVolumeDuckLevel, Localization.GetString("TooltipVolumeDuckLevel"));
            CursorFollowToolTip.SetText(ChkSkipUac, Localization.GetString("TooltipSkipUacPrompt"));
            CursorFollowToolTip.SetText(ChkStartOpen, Localization.GetString("TooltipStartWithWindowOpen"));
            CursorFollowToolTip.SetText(ChkStartupSound, Localization.GetString("TooltipPlayStartupSound"));
            CursorFollowToolTip.SetText(ChkAutostart, Localization.GetString("TooltipAutoStart"));
            CursorFollowToolTip.SetText(ChkCheckUpdates, Localization.GetString("TooltipCheckForUpdates"));
            CursorFollowToolTip.SetText(ComboTheme, Localization.GetString("TooltipThemeSelection"));
            CursorFollowToolTip.SetText(ComboLang, Localization.GetString("TooltipLanguage"));
            if (string.IsNullOrEmpty(CursorFollowToolTip.GetText(AppIcon)))
            {
                CursorFollowToolTip.SetText(AppIcon, Localization.GetString("AppName"));
            }

            LblPlaybackHeader.Text = Localization.GetString("PlaybackManagement");
            LblVolumeHeader.Text = Localization.GetString("VolumeManagement");

            UpdateThemeIcon();
            UpdateLangIcon();
            UpdateTooltipsButton();
        }

        internal void LoadSettingsToUi()
        {
            _isUpdatingUi = true;
            try
            {
                BannerHotkeysHint.Visibility = _settings.HideHotkeysHint ? Visibility.Collapsed : Visibility.Visible;
                BannerVolumeHint.Visibility = _settings.HideVolumeHint ? Visibility.Collapsed : Visibility.Visible;

                PopulateTargetPlayerCombo();
                TxtCustomPlayerProcess.Text = _settings.CustomPlayerProcessName ?? "";
                PanelCustomPlayer.Visibility = _settings.SelectedApplication == ApplicationSelectionMode.Custom
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                ChkControlAny.IsChecked = _settings.UseSystemMediaKeysOnly;
                ChkBlockBrowser.IsChecked = _settings.BlockMediaKeysForBrowser;
                ChkShowOverlay.IsChecked = _settings.ShowOverlay;
                BtnOverlaySettings.IsEnabled = _settings.ShowOverlay;
                ChkStartOpen.IsChecked = _settings.StartWithWindowOpen;
                ChkStartupSound.IsChecked = _settings.PlayStartupSound;
                UpdateStartupSoundButtons();
                ChkAutostart.IsChecked = _settings.AutoStart;
                ChkSkipUac.IsChecked = _settings.SkipUacPrompt;
                ChkCheckUpdates.IsChecked = _settings.CheckForUpdates;

                PopulateVolumeStepCombo();
                PopulateSeekStepCombo();
                PopulateVolumeDuckLevelCombo();

                _hotkeyRecorder.Show(TxtPrev);
                _hotkeyRecorder.Show(TxtPlay);
                _hotkeyRecorder.Show(TxtNext);
                _hotkeyRecorder.Show(TxtVolDown);
                _hotkeyRecorder.Show(TxtVolUp);
                _hotkeyRecorder.Show(TxtVolMute);

                PopulateAppActionsUi();

                UpdateThemeIcon();
                CursorFollowToolTip.IsEnabled = _settings.ShowTooltips;
                UpdateTooltipsButton();
                ValidateHotkeys();
            }
            finally
            {
                _isUpdatingUi = false;
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (TxtCustomPlayerProcess.IsKeyboardFocused &&
                e.OriginalSource is DependencyObject source &&
                !IsDescendantOf(source, TxtCustomPlayerProcess))
            {
                CommitCustomPlayerProcess();
                Keyboard.ClearFocus();
                FocusManager.SetFocusedElement(this, null);
            }
        }

        private void Window_Deactivated(object? sender, EventArgs e)
        {
            if (TxtCustomPlayerProcess.IsKeyboardFocused)
            {
                CommitCustomPlayerProcess();
                Keyboard.ClearFocus();
                FocusManager.SetFocusedElement(this, null);
            }
        }

        private static bool IsDescendantOf(DependencyObject? source, DependencyObject target)
        {
            DependencyObject? current = source;
            while (current != null)
            {
                if (ReferenceEquals(current, target))
                    return true;

                DependencyObject? parent = current is Visual or System.Windows.Media.Media3D.Visual3D
                    ? VisualTreeHelper.GetParent(current)
                    : LogicalTreeHelper.GetParent(current);
                current = parent ?? LogicalTreeHelper.GetParent(current);
            }
            return false;
        }

        private void BtnTooltipsToggle_Click(object sender, RoutedEventArgs e)
        {
            _settings.ShowTooltips = !_settings.ShowTooltips;
            CursorFollowToolTip.IsEnabled = _settings.ShowTooltips;
            UpdateTooltipsButton();
            _settings.Save();
        }

        private void UpdateTooltipsButton()
        {
            if (BtnTooltipsToggle == null || IconTooltipsToggle == null)
                return;

            bool enabled = _settings.ShowTooltips;
            IconTooltipsToggle.Opacity = enabled ? 1.0 : 0.35;
            CursorFollowToolTip.SetAlwaysShow(BtnTooltipsToggle, true);
            CursorFollowToolTip.SetText(
                BtnTooltipsToggle,
                Localization.GetString(enabled ? "TooltipDisableTooltips" : "TooltipEnableTooltips"));
        }

        private void BtnThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            _settings.Theme = _settings.Theme == Theme.Dark ? Theme.Light : Theme.Dark;
            ThemeService.ApplyTheme(System.Windows.Application.Current, _settings.Theme);
            UpdateThemeIcon();
            _settings.Save();
            ValidateHotkeys();
        }

        private void UpdateThemeIcon()
        {
            if (ImgThemeIcon != null)
            {
                bool isDark = _settings.Theme == Theme.Dark;
                ImgThemeIcon.Source = isDark ? ThemeService.CreateSunDrawing() : ThemeService.CreateMoonDrawing();
                CursorFollowToolTip.SetText(BtnThemeToggle, isDark ? Localization.GetString("TooltipThemeToLight") : Localization.GetString("TooltipThemeToDark"));
            }
            PopulateThemeCombo();
        }

        private void PopulateThemeCombo()
        {
            if (ComboTheme == null) return;
            ComboTheme.SelectionChanged -= ComboTheme_SelectionChanged;
            LanguageThemeChoices.FillThemes(ComboTheme, _settings.Theme);
            ComboTheme.SelectionChanged += ComboTheme_SelectionChanged;
        }

        private void UpdateLangIcon()
        {
            if (TxtLangBadge != null)
            {
                TxtLangBadge.Text = LanguageCatalog.GetInfo(_settings.Language).Badge;
            }
            if (BtnLangToggle != null)
            {
                CursorFollowToolTip.SetText(BtnLangToggle, Localization.GetString("TooltipLangToggle"));
            }
        }

        private void BtnLangToggle_Click(object sender, RoutedEventArgs e)
        {
            IReadOnlyList<Language> available = _settings.GetAvailableLanguages();
            LanguageMenuHelper.ToggleOrShowMenu(BtnLangToggle, available, _settings.Language, selected =>
            {
                if (_settings.Language == selected)
                    return;
                _settings.Language = selected;
                _settings.Save();
                Localization.SetLanguage(_settings.Language);
                LocalizeUi();
            });
        }

        private void ComboTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _settings.Theme = LanguageThemeChoices.ThemeAt(ComboTheme.SelectedIndex);
            ThemeService.ApplyTheme(System.Windows.Application.Current, _settings.Theme);
            UpdateThemeIcon();
            _settings.Save();
            ValidateHotkeys();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (System.Windows.Application.Current != null && !System.Windows.Application.Current.Dispatcher.HasShutdownStarted)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            base.OnClosing(e);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }

        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            if (PanelGeneral == null || PanelHotkeys == null) return;

            PanelGeneral.Visibility = TabGeneral.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PanelHotkeys.Visibility = TabHotkeys.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            UpdateWindowHeight();
        }

        private void UpdateWindowHeight()
        {
            this.SizeToContent = SizeToContent.Height;
        }

        private void ComboTargetPlayer_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi || _isUpdatingTargetPlayerCombo) return;
            int idx = ComboTargetPlayer.SelectedIndex;
            if (idx < 0 || idx >= TargetPlayerModes.Length) return;

            ApplicationSelectionMode mode = TargetPlayerModes[idx];
            PanelCustomPlayer.Visibility = mode == ApplicationSelectionMode.Custom ? Visibility.Visible : Visibility.Collapsed;
            UpdateWindowHeight();
            SavePlayerTarget(mode);
        }

        private void TxtCustomPlayerProcess_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitCustomPlayerProcess();
        }

        private void TxtCustomPlayerProcess_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitCustomPlayerProcess();
                Keyboard.ClearFocus();
                e.Handled = true;
            }
        }

        private void BtnBrowseCustomPlayer_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                CheckFileExists = true
            };
            if (dlg.ShowDialog(this) == true)
            {
                TxtCustomPlayerProcess.Text = KnownMediaSources.NormalizeCustomProcessName(dlg.FileName);
                CommitCustomPlayerProcess();
            }
        }

        private void CommitCustomPlayerProcess()
        {
            if (_isUpdatingUi) return;
            string normalized = KnownMediaSources.NormalizeCustomProcessName(TxtCustomPlayerProcess.Text);
            TxtCustomPlayerProcess.Text = normalized;
            if (string.Equals(_settings.CustomPlayerProcessName, normalized, StringComparison.Ordinal))
                return;

            _settings.CustomPlayerProcessName = normalized;
            KnownMediaSources.CustomProcessName = normalized;
            _settings.Save();
            _onAppSelectionChanged();
        }

        private void SavePlayerTarget(ApplicationSelectionMode mode)
        {
            if (_isUpdatingUi) return;
            _settings.SelectedApplication = mode;
            _settings.Save();
            _onAppSelectionChanged();
        }
        private void ChkControlAny_Checked(object sender, RoutedEventArgs e) { SaveBehavior(s => s.UseSystemMediaKeysOnly = true, reapplyHotkeys: true); }
        private void ChkControlAny_Unchecked(object sender, RoutedEventArgs e) { SaveBehavior(s => s.UseSystemMediaKeysOnly = false, reapplyHotkeys: true); }

        private void ChkShowOverlay_Checked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi) return;
            SaveBehavior(s => s.ShowOverlay = true);
            BtnOverlaySettings.IsEnabled = true;
        }

        private void ChkShowOverlay_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi) return;
            SaveBehavior(s => s.ShowOverlay = false);
            BtnOverlaySettings.IsEnabled = false;
        }

        private void BtnOverlaySettings_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OverlaySettingsWindow(_settings, _onShowOverlayPreview)
            {
                Owner = this
            };
            dlg.ShowDialog();
        }

        private void ComboVolumeStep_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            HandleStepComboSelectionChanged(
                ComboVolumeStep,
                _isUpdatingVolumeStepCombo,
                VolumeStepConfig,
                () => PopulateVolumeStepCombo());
        }

        private void ComboSeekStep_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            HandleStepComboSelectionChanged(
                ComboSeekStep,
                _isUpdatingSeekStepCombo,
                SeekStepConfig,
                () => PopulateSeekStepCombo());
        }

        private void ComboVolumeDuckLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            HandleStepComboSelectionChanged(
                ComboVolumeDuckLevel,
                _isUpdatingVolumeDuckLevelCombo,
                VolumeDuckLevelConfig,
                () => PopulateVolumeDuckLevelCombo());
        }

        private void HandleStepComboSelectionChanged(ComboBox? combo, bool isUpdatingCombo, StepComboConfig config, Action repopulate)
        {
            if (_isUpdatingUi || isUpdatingCombo || combo == null || combo.SelectedIndex < 0) return;

            if (combo.SelectedItem is not StepChoice choice)
                return;

            if (!choice.Value.HasValue)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    string defaultVal = config.Getter(_settings).ToString();
                    string? input = CustomDialogWindow.ShowInput(
                        this,
                        Localization.GetString(config.PromptMessageKey),
                        Localization.GetString(config.PromptTitleKey),
                        defaultValue: defaultVal,
                        confirmText: Localization.GetString("Save"),
                        cancelText: Localization.GetString("Cancel"));

                    if (!string.IsNullOrWhiteSpace(input) &&
                        int.TryParse(input, out int parsedStep) &&
                        parsedStep >= config.MinValue &&
                        parsedStep <= config.MaxValue)
                    {
                        SaveBehavior(s => config.Setter(s, parsedStep));
                        repopulate();
                    }
                    else
                    {
                        if (input != null)
                        {
                            CustomDialogWindow.Show(
                                this,
                                Localization.GetString(config.InvalidMessageKey),
                                Localization.GetString(config.PromptTitleKey));
                        }
                        repopulate();
                    }
                }));
            }
            else
            {
                int selectedStep = choice.Value.Value;
                if (selectedStep >= config.MinValue && selectedStep <= config.MaxValue)
                {
                    SaveBehavior(s => config.Setter(s, selectedStep));
                }
            }
        }
        private void ChkBlockBrowser_Checked(object sender, RoutedEventArgs e) { SaveBehavior(s => s.BlockMediaKeysForBrowser = true); }
        private void ChkBlockBrowser_Unchecked(object sender, RoutedEventArgs e) { SaveBehavior(s => s.BlockMediaKeysForBrowser = false); }
        private void ChkSkipUac_Checked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi) return;
            SaveBehavior(s => s.SkipUacPrompt = true);
            ElevationSchedulerService.EnsureElevatedTaskRegistered();
        }
        private void ChkSkipUac_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi) return;
            SaveBehavior(s => s.SkipUacPrompt = false);
            ElevationSchedulerService.DeleteElevatedTask();
        }
        private void ChkCheckUpdates_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi) return;
            _settings.CheckForUpdates = ChkCheckUpdates.IsChecked == true;
            _settings.Save();
        }

        private void ChkStartOpen_Checked(object sender, RoutedEventArgs e) { SaveBehavior(s => s.StartWithWindowOpen = true); }
        private void ChkStartOpen_Unchecked(object sender, RoutedEventArgs e) { SaveBehavior(s => s.StartWithWindowOpen = false); }

        private void ChkStartupSound_Changed(object sender, RoutedEventArgs e)
        {
            UpdateStartupSoundButtons();
            if (_isUpdatingUi) return;
            bool enabled = ChkStartupSound.IsChecked == true;
            _settings.PlayStartupSound = enabled;
            _settings.Save();
            if (enabled)
            {
                AppSounds.PlayStartupSound(_settings.CustomStartupSoundFile);
            }
            else
            {
                AppSounds.StopAllSounds();
            }
        }

        private void UpdateStartupSoundButtons()
        {
            bool enabled = ChkStartupSound.IsChecked == true;
            bool hasCustom = !string.IsNullOrWhiteSpace(_settings.CustomStartupSoundFile);
            BtnChooseStartupSound.IsEnabled = enabled;
            BtnResetStartupSound.IsEnabled = enabled;
            BtnResetStartupSound.Visibility = hasCustom ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnChooseStartupSound_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Audio files (*.wav;*.mp3;*.m4a;*.wma;*.aac)|*.wav;*.mp3;*.m4a;*.wma;*.aac|All files (*.*)|*.*",
                CheckFileExists = true
            };
            if (dlg.ShowDialog(this) != true)
                return;

            try
            {
                AppSounds.SetCustomStartupSound(dlg.FileName, _settings);
                UpdateStartupSoundButtons();
                AppSounds.PlayStartupSound(_settings.CustomStartupSoundFile);
            }
            catch (Exception ex)
            {
                CustomDialogWindow.Show(this, ex.Message, Localization.GetString("Error"));
            }
        }

        private void BtnResetStartupSound_Click(object sender, RoutedEventArgs e)
        {
            AppSounds.ResetCustomStartupSound(_settings);
            UpdateStartupSoundButtons();
            AppSounds.PlayStartupJingle();
        }

        private void BtnExportSettings_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                FileName = "MusicMediaKeys-settings.json",
                DefaultExt = ".json"
            };
            if (dlg.ShowDialog(this) != true)
                return;

            try
            {
                SettingsExportService.Export(_settings, dlg.FileName);
                CustomDialogWindow.Show(
                    this,
                    Localization.GetString("ExportSettingsSuccess"),
                    Localization.GetString("SettingsTransferTitle"));
            }
            catch (Exception ex)
            {
                CustomDialogWindow.Show(
                    this,
                    string.Format(Localization.GetString("ExportSettingsFailedFormat"), ex.Message),
                    Localization.GetString("Error"));
            }
        }

        private void BtnImportSettings_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                CheckFileExists = true
            };
            if (dlg.ShowDialog(this) != true)
                return;

            try
            {
                AppSettings imported = SettingsExportService.ImportFromFile(dlg.FileName);
                SettingsExportService.CopyInto(imported, _settings);
                _settings.Save();

                if (!AppPaths.IsPortableMode && ElevationManager.IsRunningAsAdministrator())
                {
                    if (_settings.AutoStart)
                        AutoStartManager.Enable();
                    else
                        AutoStartManager.Disable();

                    if (_settings.SkipUacPrompt)
                        ElevationSchedulerService.EnsureElevatedTaskRegistered();
                    else
                        ElevationSchedulerService.DeleteElevatedTask();
                }

                ThemeService.ApplyTheme(System.Windows.Application.Current, _settings.Theme);
                Localization.SetLanguage(_settings.Language);
                CursorFollowToolTip.IsEnabled = _settings.ShowTooltips;
                _onSave();
                _onAppSelectionChanged();
                LoadSettingsToUi();
                LocalizeUi();

                CustomDialogWindow.Show(
                    this,
                    Localization.GetString("ImportSettingsSuccess"),
                    Localization.GetString("SettingsTransferTitle"));
            }
            catch (Exception ex)
            {
                CustomDialogWindow.Show(
                    this,
                    string.Format(Localization.GetString("ImportSettingsFailedFormat"), ex.Message),
                    Localization.GetString("Error"));
            }
        }

        private void SaveBehavior(Action<AppSettings> update, bool reapplyHotkeys = false)
        {
            if (_isUpdatingUi) return;
            update(_settings);
            _settings.Save();
            if (reapplyHotkeys)
                _onSave();
            else
                _onAppSelectionChanged();
        }

        private void ChkAutostart_Checked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi) return;
            try
            {
                if (!ElevationManager.IsRunningAsAdministrator())
                {
                    CustomDialogWindow.Show(
                        this,
                        Localization.GetString("AutoStartEnableNeedAdmin"),
                        Localization.GetString("Warning"));
                    ChkAutostart.IsChecked = false;
                    return;
                }
                AutoStartManager.Enable();
                _settings.AutoStart = true;
                _settings.Save();
            }
            catch (Exception ex)
            {
                CustomDialogWindow.Show(this, ex.Message, Localization.GetString("Error"));
                ChkAutostart.IsChecked = false;
            }
        }

        private void ChkAutostart_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi) return;
            try
            {
                if (AutoStartManager.IsEnabled() && !ElevationManager.IsRunningAsAdministrator())
                {
                    CustomDialogWindow.Show(
                        this,
                        Localization.GetString("AutoStartDisableNeedAdmin"),
                        Localization.GetString("Warning"));
                    ChkAutostart.IsChecked = true;
                    return;
                }
                if (!AutoStartManager.Disable())
                {
                    throw new InvalidOperationException(Localization.GetString("AutoStartDisableFailed"));
                }
                _settings.AutoStart = false;
                _settings.Save();
            }
            catch (Exception ex)
            {
                CustomDialogWindow.Show(this, ex.Message, Localization.GetString("Error"));
                ChkAutostart.IsChecked = true;
            }
        }

        private void ComboLang_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _settings.Language = LanguageThemeChoices.LanguageAt(ComboLang, ComboLang.SelectedIndex);
            _settings.Save();
            Localization.SetLanguage(_settings.Language);
            LocalizeUi();
        }

        private HotkeyBinding? GetBindingForTextBox(TextBox tb)
        {
            if (tb == TxtPrev) return _settings.PreviousBinding;
            if (tb == TxtPlay) return _settings.PlayPauseBinding;
            if (tb == TxtNext) return _settings.NextBinding;
            if (tb == TxtVolDown) return _settings.VolumeDownBinding;
            if (tb == TxtVolUp) return _settings.VolumeUpBinding;
            if (tb == TxtVolMute) return _settings.VolumeMuteBinding;
            if (tb.Tag is ActionHotkeyBinding ab) return ab.Binding;
            return null;
        }

        private void SetBindingForTextBox(TextBox tb, HotkeyBinding? binding)
        {
            if (tb == TxtPrev) _settings.PreviousBinding = binding ?? new HotkeyBinding { Key = 0, Modifiers = 0 };
            else if (tb == TxtPlay) _settings.PlayPauseBinding = binding ?? new HotkeyBinding { Key = 0, Modifiers = 0 };
            else if (tb == TxtNext) _settings.NextBinding = binding ?? new HotkeyBinding { Key = 0, Modifiers = 0 };
            else if (tb == TxtVolDown) _settings.VolumeDownBinding = binding;
            else if (tb == TxtVolUp) _settings.VolumeUpBinding = binding;
            else if (tb == TxtVolMute) _settings.VolumeMuteBinding = binding;
            else if (tb.Tag is ActionHotkeyBinding ab) ab.Binding = binding;
        }

        public void ValidateHotkeys()
        {
            var textboxes = new List<TextBox> { TxtPrev, TxtPlay, TxtNext, TxtVolDown, TxtVolUp, TxtVolMute };
            if (StackAppActions != null)
            {
                CollectTextBoxes(StackAppActions, textboxes);
            }

            var bindings = textboxes.Select(tb => GetBindingForTextBox(tb)).ToArray();

            bool isDark = _settings.Theme == Theme.Dark;
            var dupBg = isDark
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 20, 20))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 210, 210));
            var dupBorder = isDark
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 50, 50))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(230, 80, 80));
            dupBg.Freeze();
            dupBorder.Freeze();

            for (int i = 0; i < textboxes.Count; i++)
            {
                var tb = textboxes[i];
                var b = bindings[i];

                if (tb == null) continue;

                bool isDuplicate = false;
                if (b != null && b.Key != 0)
                {
                    for (int j = 0; j < textboxes.Count; j++)
                    {
                        var bj = bindings[j];
                        if (i != j && bj != null && bj.Key != 0)
                        {
                            if (bj.Key == b.Key && bj.Modifiers == b.Modifiers)
                            {
                                isDuplicate = true;
                                break;
                            }
                        }
                    }
                }

                if (isDuplicate)
                {
                    tb.Background = dupBg;
                    tb.BorderBrush = dupBorder;
                    CursorFollowToolTip.SetText(tb, Localization.GetString("TooltipDuplicateBinding"));
                }
                else
                {
                    if (!_hotkeyRecorder.IsRecording(tb))
                    {
                        tb.ClearValue(BackgroundProperty);
                        tb.ClearValue(BorderBrushProperty);
                    }
                    CursorFollowToolTip.SetText(tb, null);
                }
            }
        }

        private static void CollectTextBoxes(Panel panel, List<TextBox> result)
        {
            foreach (var child in panel.Children)
            {
                if (child is TextBox tb)
                    result.Add(tb);
                else if (child is Panel nestedPanel)
                    CollectTextBoxes(nestedPanel, result);
            }
        }

        private void PopulateAppActionsUi()
        {
            if (StackAppActions == null) return;
            StackAppActions.Children.Clear();

            foreach (var item in _settings.CustomActionHotkeys)
            {
                var rowGrid = new Grid
                {
                    Margin = new Thickness(0, 3, 0, 3),
                    Tag = item
                };
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                string actionLabel = Localization.GetString(AppActions.GetActionLabelKey(item.ActionId));
                var label = new TextBlock
                {
                    Text = actionLabel,
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(0, 0, 10, 0)
                };
                label.SetResourceReference(TextBlock.ForegroundProperty, "TextWhite");
                Grid.SetColumn(label, 0);
                rowGrid.Children.Add(label);

                var rightGrid = new Grid();
                rightGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                rightGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var tb = new TextBox
                {
                    Style = (Style)FindResource("HotkeyBox"),
                    Height = 30,
                    Tag = item
                };
                _hotkeyRecorder.Attach(tb);
                Grid.SetColumn(tb, 0);
                rightGrid.Children.Add(tb);

                var btnDelete = new Button
                {
                    Width = 26,
                    Height = 26,
                    Margin = new Thickness(6, 0, 0, 0),
                    Background = Brushes.Transparent,
                    BorderBrush = Brushes.Transparent,
                    Cursor = Cursors.Hand,
                    Focusable = false,
                    FocusVisualStyle = null,
                    Tag = item
                };
                btnDelete.SetResourceReference(Button.ForegroundProperty, "TextGray");
                CursorFollowToolTip.SetText(btnDelete, Localization.GetString("TooltipDeleteActionHotkey"));

                var btnTemplate = new ControlTemplate(typeof(Button));
                var borderFactory = new FrameworkElementFactory(typeof(Border));
                borderFactory.Name = "DelBd";
                borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
                borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));

                var iconFactory = new FrameworkElementFactory(typeof(TextBlock));
                iconFactory.SetValue(TextBlock.TextProperty, "\uE8BB");
                iconFactory.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Segoe MDL2 Assets"));
                iconFactory.SetValue(TextBlock.FontSizeProperty, 8.0);
                iconFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                iconFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
                iconFactory.SetValue(TextBlock.ForegroundProperty, new TemplateBindingExtension(Button.ForegroundProperty));

                borderFactory.AppendChild(iconFactory);
                btnTemplate.VisualTree = borderFactory;

                var hoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
                hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new DynamicResourceExtension("BgCard"), "DelBd"));
                hoverTrigger.Setters.Add(new Setter(Button.ForegroundProperty, new SolidColorBrush(Color.FromRgb(255, 80, 80))));
                btnTemplate.Triggers.Add(hoverTrigger);
                btnDelete.Template = btnTemplate;

                btnDelete.Click += (s, e) =>
                {
                    _settings.CustomActionHotkeys.Remove(item);
                    _settings.Save();
                    _onSave();
                    PopulateAppActionsUi();
                    ValidateHotkeys();
                };

                Grid.SetColumn(btnDelete, 1);
                rightGrid.Children.Add(btnDelete);

                Grid.SetColumn(rightGrid, 1);
                rowGrid.Children.Add(rightGrid);

                StackAppActions.Children.Add(rowGrid);
            }

            FillAddActionCombo();
        }

        private bool _isFillingAddActionCombo;

        private void FillAddActionCombo()
        {
            var existingIds = _settings.CustomActionHotkeys.Select(x => x.ActionId).ToHashSet();

            _isFillingAddActionCombo = true;
            try
            {
                ComboAddAction.Items.Clear();
                foreach (var actionId in AppActions.AvailableCustomActions.Where(id => !existingIds.Contains(id)))
                    ComboAddAction.Items.Add(new ComboBoxItem { Content = Localization.GetString(AppActions.GetActionLabelKey(actionId)), Tag = actionId });
                ComboAddAction.SelectedIndex = -1;
            }
            finally
            {
                _isFillingAddActionCombo = false;
            }

            ComboAddAction.IsEnabled = ComboAddAction.Items.Count > 0;
        }

        private void ComboAddAction_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isFillingAddActionCombo || ComboAddAction.SelectedItem is not ComboBoxItem { Tag: string actionId })
                return;

            var newBinding = new ActionHotkeyBinding { ActionId = actionId, Binding = null };
            _settings.CustomActionHotkeys.Add(newBinding);
            _settings.Save();
            _onSave();

            Dispatcher.BeginInvoke(new Action(() =>
            {
                PopulateAppActionsUi();
                ValidateHotkeys();

                foreach (var child in StackAppActions.Children)
                {
                    if (child is Grid grid && grid.Tag == newBinding)
                    {
                        foreach (var elem in ((Grid)grid.Children[1]).Children)
                        {
                            if (elem is TextBox tb)
                            {
                                tb.Focus();
                                break;
                            }
                        }
                    }
                }
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void BtnCloseHotkeysHint_Click(object sender, RoutedEventArgs e)
        {
            BannerHotkeysHint.Visibility = Visibility.Collapsed;
            _settings.HideHotkeysHint = true;
            _settings.Save();
        }

        private void BtnCloseVolumeHint_Click(object sender, RoutedEventArgs e)
        {
            BannerVolumeHint.Visibility = Visibility.Collapsed;
            _settings.HideVolumeHint = true;
            _settings.Save();
        }

        internal static double MeasureTextWidth(string text, Control control, FontWeight? fontWeight = null)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            var typeface = new Typeface(
                control.FontFamily,
                control.FontStyle,
                fontWeight ?? control.FontWeight,
                control.FontStretch);

            var formatted = new FormattedText(
                text,
                System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                typeface,
                control.FontSize,
                Brushes.White,
                VisualTreeHelper.GetDpi(control).PixelsPerDip);

            return formatted.WidthIncludingTrailingWhitespace;
        }

        private int _iconEasterEggClicks;
        private DateTime _lastIconClickUtc = DateTime.MinValue;

        private void AppIcon_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (e.ChangedButton != MouseButton.Left)
                return;

            DateTime now = DateTime.UtcNow;
            if (now - _lastIconClickUtc > TimeSpan.FromMilliseconds(600))
                _iconEasterEggClicks = 0;

            _lastIconClickUtc = now;
            _iconEasterEggClicks++;

            if (_iconEasterEggClicks >= 10)
            {
                _iconEasterEggClicks = 0;
                AppSounds.PlayEasterEggSound();
                CursorFollowToolTip.SetText(AppIcon, Localization.GetString("EasterEggTooltip"));

                var wiggle = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames
                {
                    Duration = TimeSpan.FromMilliseconds(450)
                };
                wiggle.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(0, System.Windows.Media.Animation.KeyTime.FromPercent(0.0)));
                wiggle.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(-18, System.Windows.Media.Animation.KeyTime.FromPercent(0.18)));
                wiggle.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(18, System.Windows.Media.Animation.KeyTime.FromPercent(0.38)));
                wiggle.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(-12, System.Windows.Media.Animation.KeyTime.FromPercent(0.58)));
                wiggle.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(10, System.Windows.Media.Animation.KeyTime.FromPercent(0.78)));
                wiggle.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(0, System.Windows.Media.Animation.KeyTime.FromPercent(1.0)));
                AppIconRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, wiggle);
            }
        }
    }
}
