using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace MusicMediaKeys
{
    internal sealed class TrayMenuOptions
    {
        public ApplicationSelectionMode SelectedApplication { get; init; }
        public string CustomPlayerProcessName { get; init; } = "";
        public Func<(ApplicationSelectionMode Mode, string CustomName)>? OnCyclePlayer { get; init; }
    }

    internal static class TrayContextMenuService
    {
        public static ContextMenu CreateContextMenu(
            Theme theme,
            Action onOpen,
            Action onHotkeys,
            Action onExit,
            TrayMenuOptions? options = null)
        {
            var menu = new ContextMenu();

            bool isLight = theme == Theme.Light;
            Brush ThemeBrush(string key, string lightFallback, string darkFallback) =>
                Application.Current?.TryFindResource(key) as Brush
                ?? (Brush)new BrushConverter().ConvertFromString(isLight ? lightFallback : darkFallback)!;

            var bgBrush = ThemeBrush("BgCard", "#FFFFFF", "#1A1A1E");
            var borderBrush = ThemeBrush("BorderDark", "#D1D9E0", "#2A2A2E");
            var textBrush = ThemeBrush("TextWhite", "#1E2022", "#FFFFFF");
            var hoverBrush = ThemeBrush("BgHover", "#E9EDF0", "#252529");
            var accentBrush = ThemeBrush("TealAccent", "#008B94", "#00ADB5");

            var menuTemplate = new ControlTemplate(typeof(ContextMenu));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.BackgroundProperty, bgBrush);
            borderFactory.SetValue(Border.BorderBrushProperty, borderBrush);
            borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(4));

            var spFactory = new FrameworkElementFactory(typeof(StackPanel));
            spFactory.SetValue(StackPanel.IsItemsHostProperty, true);
            borderFactory.AppendChild(spFactory);

            menuTemplate.VisualTree = borderFactory;
            menu.Template = menuTemplate;

            ControlTemplate CreateItemTemplate(string iconText, string? fixedText = null)
            {
                var template = new ControlTemplate(typeof(MenuItem));
                var itemBorder = new FrameworkElementFactory(typeof(Border), "Bd");
                itemBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
                itemBorder.SetValue(Border.BackgroundProperty, Brushes.Transparent);
                itemBorder.SetValue(Border.PaddingProperty, new Thickness(10, 6, 12, 6));

                var sp = new FrameworkElementFactory(typeof(StackPanel));
                sp.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

                var iconBlock = new FrameworkElementFactory(typeof(TextBlock));
                iconBlock.SetValue(TextBlock.TextProperty, iconText);
                iconBlock.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Segoe MDL2 Assets"));
                iconBlock.SetValue(TextBlock.FontSizeProperty, 12.0);
                iconBlock.SetValue(TextBlock.ForegroundProperty, accentBrush);
                iconBlock.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
                iconBlock.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 10, 0));
                sp.AppendChild(iconBlock);

                var textBlock = new FrameworkElementFactory(typeof(TextBlock));
                if (fixedText != null)
                {
                    textBlock.SetValue(TextBlock.TextProperty, fixedText);
                }
                else
                {
                    textBlock.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Header")
                    {
                        RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
                    });
                }
                textBlock.SetValue(TextBlock.FontSizeProperty, 12.0);
                textBlock.SetValue(TextBlock.ForegroundProperty, textBrush);
                textBlock.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
                sp.AppendChild(textBlock);

                itemBorder.AppendChild(sp);
                template.VisualTree = itemBorder;

                var trigger = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
                trigger.Setters.Add(new Setter(Border.BackgroundProperty, hoverBrush, "Bd"));
                template.Triggers.Add(trigger);

                return template;
            }

            if (UpdateService.Available is UpdateInfo update)
            {
                var itemUpdate = new MenuItem { Cursor = System.Windows.Input.Cursors.Hand };
                itemUpdate.Template = CreateItemTemplate("\uE896", string.Format(Localization.GetString("UpdateBadgeFormat"), UpdateService.FormatVersion(update.Version)));
                itemUpdate.Click += (s, e) => onOpen();
                menu.Items.Add(itemUpdate);
            }

            var itemOpen = new MenuItem { Cursor = System.Windows.Input.Cursors.Hand };
            itemOpen.Template = CreateItemTemplate("\uE115", Localization.GetString("Open"));
            itemOpen.Click += (s, e) => onOpen();
            menu.Items.Add(itemOpen);

            var itemHotkeys = new MenuItem { Cursor = System.Windows.Input.Cursors.Hand };
            itemHotkeys.Template = CreateItemTemplate("\uE765", Localization.GetString("Hotkeys"));
            itemHotkeys.Click += (s, e) => onHotkeys();
            menu.Items.Add(itemHotkeys);

            if (options?.OnCyclePlayer != null)
            {
                string FormatPlayerHeader(ApplicationSelectionMode mode, string customName) =>
                    string.Format(Localization.GetString("TrayMenuPlayerFormat"), GetPlayerShortLabel(mode, customName));

                var itemPlayer = new MenuItem
                {
                    Cursor = System.Windows.Input.Cursors.Hand,
                    StaysOpenOnClick = true,
                    Header = FormatPlayerHeader(options.SelectedApplication, options.CustomPlayerProcessName),
                    Template = CreateItemTemplate("\uE8AB")
                };

                DispatcherTimer? mouseLeaveTimer = null;
                itemPlayer.Click += (s, e) =>
                {
                    double oldWidth = menu.ActualWidth;
                    var (newMode, newCustom) = options.OnCyclePlayer();
                    itemPlayer.Header = FormatPlayerHeader(newMode, newCustom);
                    menu.UpdateLayout();
                    if (oldWidth > 0 && menu.ActualWidth > 0 && Math.Abs(menu.ActualWidth - oldWidth) > 0.5)
                    {
                        menu.HorizontalOffset -= (menu.ActualWidth - oldWidth);
                    }

                    if (mouseLeaveTimer == null)
                    {
                        mouseLeaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
                        mouseLeaveTimer.Tick += (ts, te) =>
                        {
                            if (!menu.IsOpen)
                            {
                                mouseLeaveTimer.Stop();
                                return;
                            }

                            try
                            {
                                if (NativeMethods.GetCursorPos(out NativeMethods.POINT pt) &&
                                    PresentationSource.FromVisual(menu) != null &&
                                    menu.ActualWidth > 0 && menu.ActualHeight > 0)
                                {
                                    Point topLeft = menu.PointToScreen(new Point(0, 0));
                                    DpiScale dpi = VisualTreeHelper.GetDpi(menu);
                                    double widthPx = menu.ActualWidth * dpi.DpiScaleX;
                                    double heightPx = menu.ActualHeight * dpi.DpiScaleY;
                                    const double marginPx = 6.0;

                                    bool inside =
                                        pt.X >= topLeft.X - marginPx &&
                                        pt.X <= topLeft.X + widthPx + marginPx &&
                                        pt.Y >= topLeft.Y - marginPx &&
                                        pt.Y <= topLeft.Y + heightPx + marginPx;

                                    if (!inside)
                                    {
                                        mouseLeaveTimer.Stop();
                                        menu.IsOpen = false;
                                    }
                                }
                            }
                            catch
                            {
                                mouseLeaveTimer.Stop();
                            }
                        };
                        menu.Closed += (cs, ce) => mouseLeaveTimer?.Stop();
                    }

                    if (!mouseLeaveTimer.IsEnabled)
                    {
                        mouseLeaveTimer.Start();
                    }
                };

                menu.Items.Add(itemPlayer);
            }

            var sep = new Separator
            {
                Margin = new Thickness(4, 3, 4, 3),
                Height = 1,
                Background = borderBrush
            };
            menu.Items.Add(sep);

            var itemExit = new MenuItem { Cursor = System.Windows.Input.Cursors.Hand };
            itemExit.Template = CreateItemTemplate("\uE711", Localization.GetString("Exit"));
            itemExit.Click += (s, e) => onExit();
            menu.Items.Add(itemExit);

            menu.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            menu.Placement = PlacementMode.MousePoint;
            menu.HorizontalOffset = -menu.DesiredSize.Width;
            menu.VerticalOffset = -menu.DesiredSize.Height;

            return menu;
        }

        private static string GetPlayerShortLabel(ApplicationSelectionMode mode, string customName)
        {
            if (mode == ApplicationSelectionMode.Auto)
                return Localization.GetString("Auto");

            if (mode == ApplicationSelectionMode.Custom)
            {
                string norm = KnownMediaSources.NormalizeCustomProcessName(customName);
                return !string.IsNullOrEmpty(norm) ? norm : Localization.GetString("CustomPlayer");
            }

            return Localization.GetString(KnownMediaSources.GetDescriptor(KnownMediaSources.ToMediaApplication(mode)).LocalizationKey);
        }

        public static void Show(
            IntPtr hostHwnd,
            Theme theme,
            Action onOpen,
            Action onHotkeys,
            Action onExit,
            TrayMenuOptions? options = null)
        {
            var menu = CreateContextMenu(theme, onOpen, onHotkeys, onExit, options);

            if (hostHwnd != IntPtr.Zero)
            {
                NativeMethods.SetForegroundWindow(hostHwnd);
            }

            menu.IsOpen = true;
        }
    }
}
