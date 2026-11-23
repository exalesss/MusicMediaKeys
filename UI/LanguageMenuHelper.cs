using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace MusicMediaKeys
{
    internal static class LanguageMenuHelper
    {
        public static void ToggleOrShowMenu(
            FrameworkElement anchor,
            IReadOnlyList<Language> available,
            Language current,
            Action<Language> onSelected)
        {
            if (available.Count <= 1)
                return;

            if (available.Count == 2)
            {
                Language next = available[0] == current ? available[1] : available[0];
                onSelected(next);
                return;
            }

            var menu = new ContextMenu
            {
                PlacementTarget = anchor,
                Placement = PlacementMode.Bottom,
                MaxHeight = 320
            };

            foreach (Language lang in available)
            {
                LanguageInfo info = LanguageCatalog.GetInfo(lang);
                var row = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center
                };

                var badgeBorder = new Border
                {
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(4, 1, 4, 1),
                    Margin = new Thickness(0, 0, 8, 0),
                    MinWidth = 28,
                    VerticalAlignment = VerticalAlignment.Center
                };
                badgeBorder.SetResourceReference(Border.BackgroundProperty, "BgHover");
                badgeBorder.SetResourceReference(Border.BorderBrushProperty, "BorderDark");
                badgeBorder.BorderThickness = new Thickness(1);

                var badgeText = new TextBlock
                {
                    Text = info.Badge,
                    FontSize = 9.5,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                badgeText.SetResourceReference(TextBlock.ForegroundProperty, "TealAccent");
                badgeBorder.Child = badgeText;
                row.Children.Add(badgeBorder);

                var nameText = new TextBlock
                {
                    Text = info.NativeName,
                    FontSize = 12,
                    FontWeight = lang == current ? FontWeights.Bold : FontWeights.Normal,
                    VerticalAlignment = VerticalAlignment.Center
                };
                if (lang == current)
                    nameText.SetResourceReference(TextBlock.ForegroundProperty, "TealAccent");
                row.Children.Add(nameText);

                var item = new MenuItem { Header = row };
                Language captured = lang;
                item.Click += (s, e) => onSelected(captured);
                menu.Items.Add(item);
            }

            menu.IsOpen = true;
        }
    }
}
