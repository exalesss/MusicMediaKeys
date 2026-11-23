using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using AppLanguage = MusicMediaKeys.Language;

namespace MusicMediaKeys.Installer
{
    public partial class InstallerWindow : Window
    {
        private static readonly ApplicationSelectionMode[] PlayerModes =
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

        private readonly InstallOptions _options = new();
        private readonly HotkeyRecorder _hotkeyRecorder;
        private HotkeyBinding? _prevBinding;
        private HotkeyBinding? _playBinding;
        private HotkeyBinding? _nextBinding;
        private int _step = 1;
        private bool _isInitializing = true;

        public InstallerWindow()
        {
            InitializeComponent();
            ModalAttention.Attach(this);

            _options.Language = (int)LanguageCatalog.DetectSystemLanguage();
            Localization.SetLanguage((AppLanguage)_options.Language);

            if (InstallerEngine.IsAdministrator())
            {
                _options.InstallDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "MusicMediaKeys");
            }

            _prevBinding = new HotkeyBinding { Key = _options.PreviousKey, Modifiers = _options.PreviousModifiers };
            _playBinding = new HotkeyBinding { Key = _options.PlayPauseKey, Modifiers = _options.PlayPauseModifiers };
            _nextBinding = new HotkeyBinding { Key = _options.NextKey, Modifiers = _options.NextModifiers };

            _hotkeyRecorder = new HotkeyRecorder(this, GetBindingForTextBox, SetBindingForTextBox, HotkeyRecorder.TrackPresets);
            foreach (TextBox field in new[] { TxtPrev, TxtPlayPause, TxtNext })
                _hotkeyRecorder.Attach(field);

            var titleIcon = TrayIconService.LoadTitleBarIcon();
            if (titleIcon != null)
            {
                Icon = titleIcon;
                AppIcon.Source = titleIcon;
            }

            TxtInstallDir.Text = _options.InstallDirectory;
            ApplyTheme();
            Localize();
            _isInitializing = false;
            ShowStep(1);
        }

        internal void ConfigureForPreview(Theme theme, AppLanguage language, int step)
        {
            _options.Theme = theme == Theme.Light ? 0 : 1;
            _options.Language = (int)language;
            ApplyTheme();
            Localize();
            ShowStep(step);
            if (step == 3)
            {
                InstallProgress.Value = 65;
                TxtInstallStatus.Text = InstallerLocalization.Get("StatusConfig", _options.Language);
            }
        }

        private HotkeyBinding? GetBindingForTextBox(TextBox tb)
        {
            if (tb == TxtPrev) return _prevBinding;
            if (tb == TxtPlayPause) return _playBinding;
            if (tb == TxtNext) return _nextBinding;
            return null;
        }

        private void SetBindingForTextBox(TextBox tb, HotkeyBinding? binding)
        {
            if (tb == TxtPrev)
            {
                _prevBinding = binding;
                _options.PreviousKey = binding?.Key ?? 0;
                _options.PreviousModifiers = binding?.Modifiers ?? 0;
            }
            else if (tb == TxtPlayPause)
            {
                _playBinding = binding;
                _options.PlayPauseKey = binding?.Key ?? 0;
                _options.PlayPauseModifiers = binding?.Modifiers ?? 0;
            }
            else if (tb == TxtNext)
            {
                _nextBinding = binding;
                _options.NextKey = binding?.Key ?? 0;
                _options.NextModifiers = binding?.Modifiers ?? 0;
            }
        }

        private void ApplyTheme()
        {
            Theme theme = _options.Theme == 0 ? Theme.Light : Theme.Dark;
            ThemeService.ApplyTheme(Application.Current, theme);

            bool isDark = theme == Theme.Dark;
            ImgThemeIcon.Source = isDark ? ThemeService.CreateSunDrawing() : ThemeService.CreateMoonDrawing();
            CursorFollowToolTip.SetText(BtnThemeToggle, Localization.GetString(isDark ? "TooltipThemeToLight" : "TooltipThemeToDark"));
        }

        private void Localize()
        {
            int l = _options.Language;
            AppLanguage appLang = Enum.IsDefined(typeof(AppLanguage), l) ? (AppLanguage)l : AppLanguage.English;
            Localization.SetLanguage(appLang);

            Title = InstallerLocalization.Get("WindowTitle", l);
            TxtHeaderTitle.Text = InstallerLocalization.Get("HeaderTitle", l);

            if (TxtLangBadge != null)
            {
                TxtLangBadge.Text = LanguageCatalog.GetInfo(appLang).Badge;
            }
            CursorFollowToolTip.SetText(BtnLangToggle, Localization.GetString("TooltipLangToggle"));

            LblInstallFolder.Text = InstallerLocalization.Get("InstallFolder", l);
            BtnBrowseDir.Content = InstallerLocalization.Get("Browse", l);
            CursorFollowToolTip.SetText(TxtInstallDir, InstallerLocalization.Get("TooltipInstallFolder", l));
            CursorFollowToolTip.SetText(BtnNewFolder, InstallerLocalization.Get("TooltipNewFolder", l));
            CursorFollowToolTip.SetText(BtnBrowseDir, InstallerLocalization.Get("TooltipBrowseDir", l));
            LblLanguageCatalog.Text = InstallerLocalization.Get("LanguageCatalogHeader", l);
            TxtLanguageCatalogHint.Text = InstallerLocalization.Get("LanguageCatalogHint", l);
            BtnSelectLanguages.Content = InstallerLocalization.Get("SelectLanguages", l);
            UpdateSelectedLanguagesSummary();
            ChkPortable.Content = InstallerLocalization.Get("PortableMode", l);
            CursorFollowToolTip.SetText(ChkPortable, InstallerLocalization.Get("TooltipPortableMode", l));
            ChkDesktopShortcut.Content = InstallerLocalization.Get("CreateDesktopShortcut", l);
            CursorFollowToolTip.SetText(ChkDesktopShortcut, InstallerLocalization.Get("TooltipDesktopShortcut", l));
            ChkStartMenuShortcut.Content = InstallerLocalization.Get("CreateStartMenuShortcut", l);
            CursorFollowToolTip.SetText(ChkStartMenuShortcut, InstallerLocalization.Get("TooltipStartMenuShortcut", l));

            LblTargetPlayer.Text = Localization.GetString("ManagedPlayer");
            CursorFollowToolTip.SetText(ComboPlayer, Localization.GetString("TooltipTargetPlayer"));
            BtnBrowseCustomPlayer.Content = Localization.GetString("BrowseExeButton");
            CursorFollowToolTip.SetText(TxtCustomPlayerProcess, Localization.GetString("TooltipCustomPlayerProcess"));

            LblHotkeys.Text = InstallerLocalization.Get("HotkeysHeader", l);
            LblPrev.Text = Localization.GetString("PreviousTrack");
            LblPlayPause.Text = Localization.GetString("PlayPause");
            LblNext.Text = Localization.GetString("NextTrack");
            string hotkeysHint = Localization.GetString("HotkeysHint");
            CursorFollowToolTip.SetText(TxtPrev, hotkeysHint);
            CursorFollowToolTip.SetText(TxtPlayPause, hotkeysHint);
            CursorFollowToolTip.SetText(TxtNext, hotkeysHint);

            ChkAutostart.Content = Localization.GetString("AutoStart");
            CursorFollowToolTip.SetText(ChkAutostart, Localization.GetString("TooltipAutoStart"));
            ChkStartOpen.Content = Localization.GetString("StartWithWindowOpen");
            CursorFollowToolTip.SetText(ChkStartOpen, Localization.GetString("TooltipStartWithWindowOpen"));
            ChkSkipUac.Content = Localization.GetString("SkipUacPromptHeader");
            CursorFollowToolTip.SetText(ChkSkipUac, Localization.GetString("TooltipSkipUacPrompt"));
            ChkShowOverlay.Content = Localization.GetString("ShowOverlayHeader");
            CursorFollowToolTip.SetText(ChkShowOverlay, Localization.GetString("TooltipShowOverlay"));
            ChkStartupSound.Content = Localization.GetString("PlayStartupSound");
            CursorFollowToolTip.SetText(ChkStartupSound, Localization.GetString("TooltipPlayStartupSound"));
            ChkLaunchNow.Content = InstallerLocalization.Get("LaunchNow", l);
            CursorFollowToolTip.SetText(ChkLaunchNow, InstallerLocalization.Get("TooltipLaunchNow", l));

            BtnBack.Content = InstallerLocalization.Get("Back", l);
            BtnCancel.Content = InstallerLocalization.Get("Cancel", l);

            PopulatePlayers();
            _hotkeyRecorder.Show(TxtPrev);
            _hotkeyRecorder.Show(TxtPlayPause);
            _hotkeyRecorder.Show(TxtNext);
            UpdateLangToggleVisibility();
            UpdateStepTexts();
        }

        private void PopulatePlayers()
        {
            int prevIdx = ComboPlayer.SelectedIndex;
            ComboPlayer.Items.Clear();
            foreach (ApplicationSelectionMode mode in PlayerModes)
            {
                string label = mode == ApplicationSelectionMode.Auto
                    ? Localization.GetString("Auto")
                    : Localization.GetString(KnownMediaSources.GetDescriptor(KnownMediaSources.ToMediaApplication(mode)).LocalizationKey);
                ComboPlayer.Items.Add(label);
            }
            ComboPlayer.SelectedIndex = prevIdx >= 0 && prevIdx < PlayerModes.Length ? prevIdx : 0;
        }

        private void UpdateSelectedLanguagesSummary()
        {
            if (TxtSelectedLanguagesSummary == null)
                return;

            TxtSelectedLanguagesSummary.Text = string.Format(
                InstallerLocalization.Get("LanguagesSelectedFormat", _options.Language),
                _options.InstalledLanguages.Count,
                LanguageCatalog.All.Count);
        }

        private void UpdateLangToggleVisibility()
        {
            BtnLangToggle.Visibility = _options.InstalledLanguages.Count > 1
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void BtnSelectLanguages_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new LanguageCatalogWindow(_options.Language, _options.InstalledLanguages)
            {
                Owner = this
            };
            if (dlg.ShowDialog() == true && dlg.SelectedLanguages != null && dlg.SelectedLanguages.Count > 0)
            {
                _options.InstalledLanguages = dlg.SelectedLanguages.ToList();
                if (!_options.InstalledLanguages.Contains(_options.Language))
                {
                    _options.Language = _options.InstalledLanguages[0];
                }
                Localize();
            }
        }

        private void ShowStep(int step)
        {
            _step = step;
            PanelStep1.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
            PanelStep2.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
            PanelStep3.Visibility = step == 3 ? Visibility.Visible : Visibility.Collapsed;
            PanelStep4.Visibility = step == 4 ? Visibility.Visible : Visibility.Collapsed;

            BtnBack.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
            BtnCancel.Visibility = step < 3 ? Visibility.Visible : Visibility.Collapsed;
            BtnPrimary.Visibility = step == 3 ? Visibility.Collapsed : Visibility.Visible;
            BtnPrimary.IsEnabled = step != 3;
            BtnClose.IsEnabled = step != 3;

            UpdateStepTexts();
        }

        private void UpdateStepTexts()
        {
            int l = _options.Language;
            switch (_step)
            {
                case 1:
                    TxtStepTitle.Text = InstallerLocalization.Get("Step1Title", l);
                    TxtStepSubtitle.Text = InstallerLocalization.Get("Step1Subtitle", l);
                    BtnPrimary.Content = InstallerLocalization.Get("Next", l);
                    break;
                case 2:
                    TxtStepTitle.Text = InstallerLocalization.Get("Step2Title", l);
                    TxtStepSubtitle.Text = InstallerLocalization.Get("Step2Subtitle", l);
                    BtnPrimary.Content = InstallerLocalization.Get("Install", l);
                    break;
                case 3:
                    TxtStepTitle.Text = InstallerLocalization.Get("Step3Title", l);
                    TxtStepSubtitle.Text = InstallerLocalization.Get("Step3Subtitle", l);
                    BtnPrimary.Content = InstallerLocalization.Get("Install", l);
                    break;
                case 4:
                    TxtStepTitle.Text = InstallerLocalization.Get("Step4Title", l);
                    TxtStepSubtitle.Text = InstallerLocalization.Get("Step4Subtitle", l);
                    BtnPrimary.Content = InstallerLocalization.Get("Finish", l);
                    break;
            }
        }

        private void ComboPlayer_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            int idx = ComboPlayer.SelectedIndex;
            bool isCustom = idx >= 0 && idx < PlayerModes.Length && PlayerModes[idx] == ApplicationSelectionMode.Custom;
            PanelCustomPlayer.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
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
            }
        }

        private void ChkPortable_Changed(object sender, RoutedEventArgs e)
        {
            bool portable = ChkPortable.IsChecked == true;
            ChkDesktopShortcut.IsEnabled = !portable;
            ChkStartMenuShortcut.IsEnabled = !portable;
        }

        private void BtnBrowseDir_Click(object sender, RoutedEventArgs e)
        {
            string initialDir = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string? candidate = TxtInstallDir.Text?.Trim();
            while (!string.IsNullOrWhiteSpace(candidate))
            {
                if (Directory.Exists(candidate))
                {
                    initialDir = candidate;
                    break;
                }
                candidate = Path.GetDirectoryName(candidate);
            }

            var dlg = new OpenFolderDialog
            {
                InitialDirectory = initialDir
            };
            if (dlg.ShowDialog(this) == true && !string.IsNullOrWhiteSpace(dlg.FolderName))
            {
                string selected = dlg.FolderName.Trim();
                TxtInstallDir.Text = InstallerEngine.IsSafeInstallDirectory(selected)
                    ? selected
                    : Path.Combine(selected, "MusicMediaKeys");
            }
        }

        private void BtnNewFolder_Click(object sender, RoutedEventArgs e)
        {
            int l = _options.Language;
            string currentDir = TxtInstallDir.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(currentDir))
            {
                currentDir = InstallerEngine.IsAdministrator()
                    ? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs");
            }

            string defaultName = string.Equals(Path.GetFileName(currentDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), "MusicMediaKeys", StringComparison.OrdinalIgnoreCase)
                ? "NewFolder"
                : "MusicMediaKeys";

            string? folderName = ShowNewFolderDialog(l, defaultName);
            if (string.IsNullOrWhiteSpace(folderName))
                return;

            try
            {
                string combined = Path.GetFullPath(Path.Combine(currentDir, folderName));
                TxtInstallDir.Text = combined;
            }
            catch (Exception ex)
            {
                CustomDialogWindow.Show(this, ex.Message, InstallerLocalization.Get("InstallErrorTitle", l));
            }
        }

        private void ChkStartupSound_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            if (ChkStartupSound.IsChecked == true)
                AppSounds.PlayStartupJingle();
            else
                AppSounds.StopAllSounds();
        }

        protected override void OnClosed(EventArgs e)
        {
            AppSounds.StopAllSounds();
            base.OnClosed(e);
        }

        private string? ShowNewFolderDialog(int lang, string defaultName)
        {
            string? result = null;
            var dlg = new Window
            {
                Owner = this,
                Width = 360,
                SizeToContent = SizeToContent.Height,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false
            };

            var root = new Border
            {
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(18, 16, 18, 16)
            };
            root.SetResourceReference(Border.BackgroundProperty, "BgDark");
            root.SetResourceReference(Border.BorderBrushProperty, "BorderDark");

            var stack = new StackPanel();
            var titleBlock = new TextBlock
            {
                Text = InstallerLocalization.Get("NewFolderTitle", lang),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 6)
            };
            titleBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextWhite");
            stack.Children.Add(titleBlock);

            var promptBlock = new TextBlock
            {
                Text = InstallerLocalization.Get("NewFolderPrompt", lang),
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            promptBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextGray");
            stack.Children.Add(promptBlock);

            var input = new TextBox
            {
                Text = defaultName,
                Height = 28,
                FontSize = 12,
                Padding = new Thickness(8, 0, 8, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 14)
            };
            input.SetResourceReference(TextBox.BackgroundProperty, "BgInput");
            input.SetResourceReference(TextBox.BorderBrushProperty, "BorderDark");
            input.SetResourceReference(TextBox.ForegroundProperty, "TextWhite");
            stack.Children.Add(input);

            var btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var btnCancel = new Button
            {
                Content = InstallerLocalization.Get("Cancel", lang),
                MinWidth = 80,
                Height = 28,
                Margin = new Thickness(0, 0, 8, 0)
            };
            btnCancel.SetResourceReference(Button.StyleProperty, "SecondaryButton");
            btnCancel.Click += (s, ev) => dlg.Close();

            var btnCreate = new Button
            {
                Content = InstallerLocalization.Get("Create", lang),
                MinWidth = 84,
                Height = 28
            };
            btnCreate.SetResourceReference(Button.StyleProperty, "PrimaryButton");

            void Submit()
            {
                string candidate = input.Text?.Trim() ?? "";
                if (string.IsNullOrEmpty(candidate) ||
                    candidate is "." or ".." ||
                    candidate.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                    candidate.Contains(Path.DirectorySeparatorChar) ||
                    candidate.Contains(Path.AltDirectorySeparatorChar))
                {
                    CustomDialogWindow.Show(dlg, InstallerLocalization.Get("NewFolderInvalidName", lang), InstallerLocalization.Get("NewFolderTitle", lang));
                    return;
                }
                result = candidate;
                dlg.Close();
            }

            btnCreate.Click += (s, ev) => Submit();
            input.KeyDown += (s, ev) =>
            {
                if (ev.Key == Key.Enter) { Submit(); ev.Handled = true; }
                else if (ev.Key == Key.Escape) { dlg.Close(); ev.Handled = true; }
            };

            btnRow.Children.Add(btnCancel);
            btnRow.Children.Add(btnCreate);
            stack.Children.Add(btnRow);
            root.Child = stack;
            dlg.Content = root;

            dlg.Loaded += (s, ev) =>
            {
                input.Focus();
                input.SelectAll();
            };

            dlg.ShowDialog();
            return result;
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (_step == 2) ShowStep(1);
        }

        private async void BtnPrimary_Click(object sender, RoutedEventArgs e)
        {
            if (_step == 1)
            {
                string installDir = TxtInstallDir.Text?.Trim() ?? "";
                if (!InstallerEngine.IsSafeInstallDirectory(installDir))
                {
                    CustomDialogWindow.Show(
                        this,
                        InstallerLocalization.Get("InvalidInstallDirectory", _options.Language),
                        InstallerLocalization.Get("InstallErrorTitle", _options.Language));
                    return;
                }

                _options.InstallDirectory = installDir;
                _options.IsPortable = ChkPortable.IsChecked == true;
                _options.CreateDesktopShortcut = ChkDesktopShortcut.IsChecked == true;
                _options.CreateStartMenuShortcut = ChkStartMenuShortcut.IsChecked == true;
                ShowStep(2);
                return;
            }

            if (_step == 2)
            {
                int idx = ComboPlayer.SelectedIndex;
                ApplicationSelectionMode selectedMode = idx >= 0 && idx < PlayerModes.Length
                    ? PlayerModes[idx]
                    : ApplicationSelectionMode.Auto;
                _options.SelectedApplication = (int)selectedMode;
                _options.CustomPlayerProcessName = KnownMediaSources.NormalizeCustomProcessName(TxtCustomPlayerProcess.Text);
                _options.AutoStart = ChkAutostart.IsChecked == true;
                _options.SkipUacPrompt = ChkSkipUac.IsChecked == true;
                _options.StartWithWindowOpen = ChkStartOpen.IsChecked == true;
                _options.ShowOverlay = ChkShowOverlay.IsChecked == true;
                _options.PlayStartupSound = ChkStartupSound.IsChecked == true;

                ShowStep(3);
                try
                {
                    var progress = new Progress<(int Percent, string Status)>(p =>
                    {
                        InstallProgress.Value = p.Percent;
                        TxtInstallStatus.Text = p.Status;
                    });
                    await InstallerEngine.InstallAsync(_options, progress);
                    ShowStep(4);
                }
                catch (Exception ex)
                {
                    CustomDialogWindow.Show(this, ex.Message, InstallerLocalization.Get("InstallErrorTitle", _options.Language));
                    ShowStep(2);
                }
                return;
            }

            if (_step == 4)
            {
                if (ChkLaunchNow.IsChecked == true)
                {
                    string exePath = Path.Combine(_options.InstallDirectory, "MusicMediaKeys.exe");
                    if (File.Exists(exePath))
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo { FileName = exePath, UseShellExecute = true });
                        }
                        catch { }
                    }
                }
                Close();
            }
        }

        private void BtnLangToggle_Click(object sender, RoutedEventArgs e)
        {
            var available = _options.InstalledLanguages
                .Where(id => Enum.IsDefined(typeof(AppLanguage), id))
                .Select(id => (AppLanguage)id)
                .ToList();
            AppLanguage current = Enum.IsDefined(typeof(AppLanguage), _options.Language)
                ? (AppLanguage)_options.Language
                : AppLanguage.English;

            LanguageMenuHelper.ToggleOrShowMenu(BtnLangToggle, available, current, selected =>
            {
                if (_options.Language == (int)selected)
                    return;
                _options.Language = (int)selected;
                Localize();
            });
        }

        private void BtnThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            _options.Theme = _options.Theme == 1 ? 0 : 1;
            ApplyTheme();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            if (_step != 3) Close();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject source)
                return;

            if (TxtCustomPlayerProcess.IsKeyboardFocused && !IsDescendantOf(source, TxtCustomPlayerProcess))
            {
                TxtCustomPlayerProcess.Text = KnownMediaSources.NormalizeCustomProcessName(TxtCustomPlayerProcess.Text);
                Keyboard.ClearFocus();
                FocusManager.SetFocusedElement(this, null);
            }
            else if (TxtInstallDir.IsKeyboardFocused && !IsDescendantOf(source, TxtInstallDir))
            {
                Keyboard.ClearFocus();
                FocusManager.SetFocusedElement(this, null);
            }
        }

        private void Window_Deactivated(object? sender, EventArgs e)
        {
            if (TxtCustomPlayerProcess.IsKeyboardFocused || TxtInstallDir.IsKeyboardFocused)
            {
                if (TxtCustomPlayerProcess.IsKeyboardFocused)
                    TxtCustomPlayerProcess.Text = KnownMediaSources.NormalizeCustomProcessName(TxtCustomPlayerProcess.Text);
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
    }
}
