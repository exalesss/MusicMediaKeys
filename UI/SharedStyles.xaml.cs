using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MusicMediaKeys
{
    public partial class SharedStyles : ResourceDictionary
    {
        public SharedStyles()
        {
            InitializeComponent();
        }

        private void ComboBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is ComboBox cb && cb.IsDropDownOpen)
                return;

            if (e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Left || e.Key == Key.Right ||
                e.Key == Key.PageUp || e.Key == Key.PageDown || e.Key == Key.Home || e.Key == Key.End ||
                e.Key == Key.Back || e.Key == Key.Delete || e.Key == Key.Space || e.Key == Key.Return)
            {
                e.Handled = true;
            }
        }

        private void ComboBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ComboBox cb && !cb.IsDropDownOpen)
            {
                e.Handled = true;
                var parent = VisualTreeHelper.GetParent(cb) as UIElement;
                parent?.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = UIElement.MouseWheelEvent,
                    Source = sender
                });
            }
        }
    }
}
