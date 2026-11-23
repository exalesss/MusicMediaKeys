using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using AppLanguage = MusicMediaKeys.Language;

namespace MusicMediaKeys.Installer
{
    public partial class UninstallerWindow : Window
    {
        private readonly int _language;
        private readonly string _installDir;
        private bool _isRunning;
        private bool _isCompleted;

        public UninstallerWindow()
            : this(null, null)
        {
        }

        internal UninstallerWindow(Theme? overrideTheme, AppLanguage? overrideLanguage)
        {
            InitializeComponent();
            ModalAttention.Attach(this);
            _installDir = InstallerEngine.ResolveInstalledDirectory();

            if (overrideTheme.HasValue && overrideLanguage.HasValue)
            {
                ThemeService.ApplyTheme(Application.Current, overrideTheme.Value);
                _language = (int)overrideLanguage.Value;
            }
            else
            {
                var detected = InstallerEngine.TryReadInstalledThemeAndLanguage(_installDir);
                if (detected.HasValue)
                {
                    Theme theme = detected.Value.Theme == 0 ? Theme.Light : Theme.Dark;
                    ThemeService.ApplyTheme(Application.Current, theme);
                    _language = Enum.IsDefined(typeof(AppLanguage), detected.Value.Language)
                        ? detected.Value.Language
                        : (int)LanguageCatalog.DetectSystemLanguage();
                }
                else
                {
                    ThemeService.ApplyTheme(Application.Current, Theme.Dark);
                    _language = (int)LanguageCatalog.DetectSystemLanguage();
                }
            }

            Localization.SetLanguage((AppLanguage)_language);

            var titleIcon = TrayIconService.LoadTitleBarIcon();
            if (titleIcon != null)
            {
                Icon = titleIcon;
                AppIcon.Source = titleIcon;
            }

            Title = InstallerLocalization.Get("UninstallWindowTitle", _language);
            TxtHeader.Text = InstallerLocalization.Get("UninstallWindowTitle", _language).ToUpperInvariant();
            TxtTitle.Text = InstallerLocalization.Get("UninstallTitle", _language);
            TxtDesc.Text = InstallerLocalization.Get("UninstallDesc", _language);
            ChkRemoveData.Content = InstallerLocalization.Get("UninstallRemoveData", _language);
            CursorFollowToolTip.SetText(ChkRemoveData, InstallerLocalization.Get("TooltipUninstallRemoveData", _language));
            BtnCancel.Content = InstallerLocalization.Get("Cancel", _language);
            BtnAction.Content = InstallerLocalization.Get("UninstallButton", _language);
        }

        private async void BtnAction_Click(object sender, RoutedEventArgs e)
        {
            if (_isCompleted)
            {
                Close();
                return;
            }
            if (_isRunning) return;

            _isRunning = true;
            BtnAction.IsEnabled = false;
            BtnCancel.IsEnabled = false;
            BtnClose.IsEnabled = false;
            ChkRemoveData.IsEnabled = false;
            PanelProgress.Visibility = Visibility.Visible;
            TxtTitle.Text = InstallerLocalization.Get("UninstallProgress", _language);

            try
            {
                var progress = new Progress<(int Percent, string Status)>(p =>
                {
                    UninstallProgress.Value = p.Percent;
                    TxtStatus.Text = p.Status;
                });
                await InstallerEngine.UninstallAsync(_installDir, ChkRemoveData.IsChecked == true, _language, progress);
                _isCompleted = true;
                BtnAction.IsEnabled = true;
                BtnAction.Content = InstallerLocalization.Get("Finish", _language);
                BtnClose.IsEnabled = true;
            }
            catch (Exception ex)
            {
                CustomDialogWindow.Show(this, ex.Message, InstallerLocalization.Get("UninstallWindowTitle", _language));
                _isRunning = false;
                BtnAction.IsEnabled = true;
                BtnCancel.IsEnabled = true;
                BtnClose.IsEnabled = true;
                ChkRemoveData.IsEnabled = true;
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            if (!_isRunning || _isCompleted)
                Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (_isCompleted)
            {
                InstallerEngine.ScheduleSelfDeleteOnExit(_installDir, Environment.ProcessPath);
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }
    }
}
