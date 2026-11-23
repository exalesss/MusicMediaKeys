using System.Windows;
using System.Windows.Input;

namespace MusicMediaKeys
{
    public partial class CustomDialogWindow : Window
    {
        public bool Confirmed { get; private set; }

        public CustomDialogWindow(string message, string? title = null)
        {
            InitializeComponent();
            TxtTitle.Text = string.IsNullOrWhiteSpace(title) ? Localization.GetString("Warning") : title;
            Title = TxtTitle.Text;
            TxtMessage.Text = message;

            try
            {
                if (System.Windows.Application.Current?.MainWindow?.Icon != null)
                {
                    Icon = System.Windows.Application.Current.MainWindow.Icon;
                }
            }
            catch { }
        }

        public static void Show(Window? owner, string message, string? title = null, string? infoButtonText = null, string? infoTooltipText = null)
        {
            var dlg = new CustomDialogWindow(message, title);
            if (!string.IsNullOrWhiteSpace(infoButtonText) && !string.IsNullOrWhiteSpace(infoTooltipText))
            {
                dlg.BtnInfo.Content = infoButtonText;
                dlg.BtnInfo.Visibility = Visibility.Visible;
                CursorFollowToolTip.SetText(dlg.BtnInfo, infoTooltipText);
            }
            owner ??= System.Windows.Application.Current?.MainWindow;
            if (owner != null && owner != dlg && owner.IsVisible)
            {
                dlg.Owner = owner;
                if (owner.Icon != null) dlg.Icon = owner.Icon;
                dlg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                dlg.ShowInTaskbar = false;
            }
            else
            {
                dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                dlg.Topmost = true;
                dlg.ShowInTaskbar = false;
            }
            WarningSound.Play();
            dlg.Loaded += (s, e) => { dlg.Activate(); dlg.Focus(); };
            dlg.ShowDialog();
        }

        public static bool ShowConfirmation(
            Window? owner, 
            string message, 
            string title, 
            string? confirmText = null,
            string? cancelText = null)
        {
            var dlg = new CustomDialogWindow(message, title);
            dlg.BtnOk.Content = confirmText ?? Localization.GetString("Retry");
            dlg.BtnCancel.Content = cancelText ?? Localization.GetString("Exit");
            dlg.BtnCancel.Visibility = Visibility.Visible;
            owner ??= System.Windows.Application.Current?.MainWindow;
            if (owner != null && owner != dlg && owner.IsVisible)
            {
                dlg.Owner = owner;
                if (owner.Icon != null) dlg.Icon = owner.Icon;
                dlg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                dlg.ShowInTaskbar = false;
            }
            else
            {
                dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                dlg.Topmost = true;
                dlg.ShowInTaskbar = false;
            }
            WarningSound.Play();
            dlg.Loaded += (s, e) => { dlg.Activate(); dlg.Focus(); };
            dlg.ShowDialog();
            return dlg.Confirmed;
        }

        public string? InputValue { get; private set; }

        public static string? ShowInput(
            Window? owner,
            string message,
            string title,
            string defaultValue = "",
            string? confirmText = null,
            string? cancelText = null)
        {
            var dlg = new CustomDialogWindow(message, title);
            dlg.TxtInput.Text = defaultValue;
            dlg.TxtInput.Visibility = Visibility.Visible;
            dlg.BtnOk.Content = confirmText ?? Localization.GetString("OK");
            dlg.BtnCancel.Content = cancelText ?? Localization.GetString("Cancel");
            dlg.BtnCancel.Visibility = Visibility.Visible;
            if (owner != null && owner.IsVisible)
            {
                dlg.Owner = owner;
                if (owner.Icon != null) dlg.Icon = owner.Icon;
                dlg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                dlg.ShowInTaskbar = false;
            }
            else
            {
                dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                dlg.Topmost = true;
                dlg.ShowInTaskbar = true;
            }
            dlg.Loaded += (s, e) =>
            {
                dlg.Activate();
                dlg.TxtInput.Focus();
                dlg.TxtInput.SelectAll();
            };
            dlg.ShowDialog();
            return dlg.Confirmed ? dlg.InputValue : null;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Confirmed = false;
                Close();
            }
            else if (e.Key == Key.Enter)
            {
                Confirmed = true;
                InputValue = TxtInput.Text.Trim();
                Close();
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = true;
            InputValue = TxtInput.Text.Trim();
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            Close();
        }
    }
}
