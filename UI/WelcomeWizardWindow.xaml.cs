using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AppLanguage = MusicMediaKeys.Language;
using ModifierKeys = MusicMediaKeys.ModifierKeys;

namespace MusicMediaKeys
{
    public partial class WelcomeWizardWindow : Window
    {
        private readonly AppSettings _settings;
        private HotkeyBinding? _prevBinding;
        private HotkeyBinding? _playBinding;
        private HotkeyBinding? _nextBinding;
        private readonly HotkeyRecorder _hotkeyRecorder;
        private bool _isInitializing = true;
        private Theme _selectedTheme;
        private AppLanguage _selectedLanguage;
        private ApplicationSelectionMode _selectedApplication;

        public WelcomeWizardWindow(AppSettings settings)
        {
            _settings = settings;
            _selectedTheme = settings.Theme;
            _selectedLanguage = settings.Language;
            _selectedApplication = settings.SelectedApplication;

            _prevBinding = CloneBinding(settings.PreviousBinding) ?? new HotkeyBinding { Key = MediaKeyCodes.MediaPrevTrack, Modifiers = 0 };
            _playBinding = CloneBinding(settings.PlayPauseBinding) ?? new HotkeyBinding { Key = MediaKeyCodes.MediaPlayPause, Modifiers = 0 };
            _nextBinding = CloneBinding(settings.NextBinding) ?? new HotkeyBinding { Key = MediaKeyCodes.MediaNextTrack, Modifiers = 0 };

            InitializeComponent();
            ModalAttention.Attach(this);
            TxtVersion.Text = "v" + AppInfo.Version;

            _hotkeyRecorder = new HotkeyRecorder(this, GetBindingForTextBox, SetBindingForTextBox, HotkeyRecorder.TrackPresets);
            foreach (TextBox field in new[] { TxtPrev, TxtPlay, TxtNext })
                _hotkeyRecorder.Attach(field);

            AppIcon.Source = TrayIconService.LoadTitleBarIcon();
            LocalizeUi();
            LoadSettingsToUi();

            _isInitializing = false;
        }

        private static HotkeyBinding? CloneBinding(HotkeyBinding? binding)
        {
            if (binding == null) return null;
            return new HotkeyBinding { Key = binding.Key, Modifiers = binding.Modifiers };
        }

        private static readonly ApplicationSelectionMode[] WizardTargetModes =
        {
            ApplicationSelectionMode.Auto,
            ApplicationSelectionMode.Spotify,
            ApplicationSelectionMode.YandexMusic,
            ApplicationSelectionMode.AppleMusic,
            ApplicationSelectionMode.Aimp,
            ApplicationSelectionMode.Foobar2000,
            ApplicationSelectionMode.Vlc,
            ApplicationSelectionMode.YouTubeMusicDesktop
        };

        private void LocalizeUi()
        {
            Title = Localization.GetString("WelcomeWindowTitle");
            TxtWelcomeTitle.Text = Localization.GetString("WelcomeTitle");
            TxtWelcomeSubtitle.Text = Localization.GetString("WelcomeSubtitle");
            TxtAdminHeader.Text = Localization.GetString("WelcomeAdminCardTitle");
            TxtAdminDesc.Text = Localization.GetString("WelcomeAdminCardText");

            TxtGeneralHeader.Text = Localization.GetString("General");
            ChkAutoStart.Content = Localization.GetString("AutoStart");
            ChkSkipUac.Content = Localization.GetString("SkipUacPromptHeader");
            ChkStartWithWindowOpen.Content = Localization.GetString("StartWithWindowOpen");
            TxtTargetPlayer.Text = Localization.GetString("ManagedPlayer");
            TxtLanguage.Text = Localization.GetString("LanguageHeader");
            TxtTheme.Text = Localization.GetString("ThemeHeader");

            TxtHotkeysHeader.Text = Localization.GetString("WelcomeHotkeysHeader");
            TxtPrevLabel.Text = Localization.GetString("PreviousTrack");
            TxtPlayLabel.Text = Localization.GetString("PlayPause");
            TxtNextLabel.Text = Localization.GetString("NextTrack");

            CursorFollowToolTip.SetText(ChkAutoStart, Localization.GetString("WelcomeAutostartTooltip"));
            CursorFollowToolTip.SetText(ChkSkipUac, Localization.GetString("TooltipSkipUacPrompt"));
            CursorFollowToolTip.SetText(ChkStartWithWindowOpen, Localization.GetString("WelcomeStartWithWindowTooltip"));
            CursorFollowToolTip.SetText(BtnTargetAppInfo, Localization.GetString("TooltipAuto"));
            CursorFollowToolTip.SetText(BtnHotkeysInfo, Localization.GetString("WelcomeHotkeysHint"));
            CursorFollowToolTip.SetText(BtnAdminInfo, Localization.GetString("WelcomeAdminTooltip"));
            CursorFollowToolTip.SetText(TxtWelcomeSubtitle, Localization.GetString("WelcomeSubtitleTooltip"));
            CursorFollowToolTip.SetText(BtnLangToggle, Localization.GetString("TooltipLangToggle"));
            CursorFollowToolTip.SetText(ComboLanguage, Localization.GetString("TooltipLanguage"));
            CursorFollowToolTip.SetText(ComboTheme, Localization.GetString("TooltipThemeSelection"));

            TxtSaveAndRun.Text = Localization.GetString("WelcomeSaveAndRun");

            int selectedTargetIndex = ComboTargetApp.SelectedIndex;
            if (selectedTargetIndex < 0)
            {
                selectedTargetIndex = Array.IndexOf(WizardTargetModes, _selectedApplication);
                if (selectedTargetIndex < 0) selectedTargetIndex = 0;
            }

            ComboTargetApp.SelectionChanged -= ComboTargetApp_SelectionChanged;
            ComboTargetApp.Items.Clear();
            ComboTargetApp.Items.Add($"{Localization.GetString("Auto")} (Spotify / {Localization.GetString("YandexMusic")} / ...)");
            for (int i = 1; i < WizardTargetModes.Length; i++)
            {
                if (KnownMediaSources.TryGetDescriptor(WizardTargetModes[i], out MediaPlayerDescriptor? desc))
                    ComboTargetApp.Items.Add(Localization.GetString(desc!.LocalizationKey));
            }
            ComboTargetApp.SelectedIndex = Math.Clamp(selectedTargetIndex, 0, WizardTargetModes.Length - 1);
            ComboTargetApp.SelectionChanged += ComboTargetApp_SelectionChanged;

            ComboLanguage.SelectionChanged -= ComboLanguage_SelectionChanged;
            var availableLangs = _settings.GetAvailableLanguages();
            LanguageThemeChoices.FillLanguages(ComboLanguage, _selectedLanguage, availableLangs);
            ComboLanguage.IsEnabled = availableLangs.Count > 1;
            BtnLangToggle.Visibility = availableLangs.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            ComboLanguage.SelectionChanged += ComboLanguage_SelectionChanged;

            _hotkeyRecorder.Show(TxtPrev);
            _hotkeyRecorder.Show(TxtPlay);
            _hotkeyRecorder.Show(TxtNext);

            UpdateThemeIcon();
            UpdateLangIcon();
        }

        private void LoadSettingsToUi()
        {
            ChkAutoStart.IsChecked = _settings.AutoStart;
            ChkSkipUac.IsChecked = _settings.SkipUacPrompt;
            ChkStartWithWindowOpen.IsChecked = _settings.StartWithWindowOpen;
        }

        private void UpdateThemeIcon()
        {
            bool isDark = _selectedTheme == Theme.Dark;
            ImgThemeIcon.Source = isDark ? ThemeService.CreateSunDrawing() : ThemeService.CreateMoonDrawing();
            CursorFollowToolTip.SetText(BtnThemeToggle, isDark ? Localization.GetString("TooltipThemeToLight") : Localization.GetString("TooltipThemeToDark"));

            ComboTheme.SelectionChanged -= ComboTheme_SelectionChanged;
            LanguageThemeChoices.FillThemes(ComboTheme, _selectedTheme);
            ComboTheme.SelectionChanged += ComboTheme_SelectionChanged;
        }

        private void UpdateLangIcon()
        {
            if (TxtLangBadge != null)
            {
                TxtLangBadge.Text = LanguageCatalog.GetInfo(_selectedLanguage).Badge;
            }
        }

        private void BtnThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            SetTheme(_selectedTheme == Theme.Dark ? Theme.Light : Theme.Dark);
        }

        private void ComboTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            SetTheme(LanguageThemeChoices.ThemeAt(ComboTheme.SelectedIndex));
        }

        private void SetTheme(Theme theme)
        {
            if (_selectedTheme == theme) return;

            _selectedTheme = theme;
            ThemeService.ApplyTheme(Application.Current, theme);
            UpdateThemeIcon();
        }

        private void BtnLangToggle_Click(object sender, RoutedEventArgs e)
        {
            LanguageMenuHelper.ToggleOrShowMenu(BtnLangToggle, _settings.GetAvailableLanguages(), _selectedLanguage, newLang =>
            {
                if (_selectedLanguage == newLang)
                    return;
                _selectedLanguage = newLang;
                Localization.SetLanguage(newLang);
                LocalizeUi();
            });
        }

        private void ComboLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            var newLang = LanguageThemeChoices.LanguageAt(ComboLanguage, ComboLanguage.SelectedIndex);
            if (_selectedLanguage != newLang)
            {
                _selectedLanguage = newLang;
                Localization.SetLanguage(newLang);
                LocalizeUi();
            }
        }

        private void ComboTargetApp_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            int idx = ComboTargetApp.SelectedIndex;
            _selectedApplication = idx >= 0 && idx < WizardTargetModes.Length
                ? WizardTargetModes[idx]
                : ApplicationSelectionMode.Auto;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (DialogResult != true)
            {
                ThemeService.ApplyTheme(Application.Current, _settings.Theme);
                Localization.SetLanguage(_settings.Language);
            }
            base.OnClosed(e);
        }

        private void SaveSettings()
        {
            _settings.Theme = _selectedTheme;
            _settings.Language = _selectedLanguage;
            _settings.SelectedApplication = _selectedApplication;
            _settings.IsFirstRunCompleted = true;
            if (ChkAutoStart.IsChecked == true)
            {
                if (WindowsTaskSchedulerService.IsRunningFromUntrustedPath(Environment.ProcessPath))
                {
                    CustomDialogWindow.Show(this, Localization.GetString("AutoStartUntrustedPath"), Localization.GetString("Warning"));
                    _settings.AutoStart = false;
                }
                else
                {
                    _settings.AutoStart = true;
                }
            }
            else
            {
                _settings.AutoStart = false;
            }
            _settings.SkipUacPrompt = ChkSkipUac.IsChecked == true;
            _settings.StartWithWindowOpen = ChkStartWithWindowOpen.IsChecked == true;

            if (_settings.SkipUacPrompt)
            {
                ElevationSchedulerService.EnsureElevatedTaskRegistered();
            }
            else
            {
                ElevationSchedulerService.DeleteElevatedTask();
            }

            _settings.SelectedApplication = _selectedApplication;

            _settings.PreviousBinding = _prevBinding ?? new HotkeyBinding { Key = MediaKeyCodes.MediaPrevTrack, Modifiers = 0 };
            _settings.PlayPauseBinding = _playBinding ?? new HotkeyBinding { Key = MediaKeyCodes.MediaPlayPause, Modifiers = 0 };
            _settings.NextBinding = _nextBinding ?? new HotkeyBinding { Key = MediaKeyCodes.MediaNextTrack, Modifiers = 0 };

            _settings.Save();
        }

        private void BtnSaveAndRun_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            DialogResult = true;
            Close();
        }

        private HotkeyBinding? GetBindingForTextBox(TextBox tb)
        {
            if (tb == TxtPrev) return _prevBinding;
            if (tb == TxtPlay) return _playBinding;
            if (tb == TxtNext) return _nextBinding;
            return null;
        }

        private void SetBindingForTextBox(TextBox tb, HotkeyBinding? binding)
        {
            if (tb == TxtPrev) _prevBinding = binding;
            else if (tb == TxtPlay) _playBinding = binding;
            else if (tb == TxtNext) _nextBinding = binding;
        }

    }
}
