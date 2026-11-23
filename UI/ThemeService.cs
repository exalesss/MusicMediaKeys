using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace MusicMediaKeys
{
    public static class ThemeService
    {
        public static void ApplyTheme(System.Windows.Application? app, Theme theme)
        {
            if (app == null) return;

            if (theme == Theme.Light)
            {
                app.Resources["BgDark"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F4F5F7"));
                app.Resources["BgCard"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
                app.Resources["BgHeader"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EBEFF2"));
                app.Resources["TextWhite"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E2022"));
                app.Resources["TextGray"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#686E73"));
                app.Resources["TextMuted"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8A949D"));
                app.Resources["BorderDark"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1D9E0"));
                app.Resources["BgHover"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E9EDF0"));
                app.Resources["BgItemHover"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E7EC"));
                app.Resources["BgBtnHover"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DFE4E8"));
                app.Resources["BgInput"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
                app.Resources["TealAccent"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#008B94"));
                app.Resources["TealHover"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00ADB5"));
            }
            else
            {
                app.Resources["BgDark"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#121214"));
                app.Resources["BgCard"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A1A1E"));
                app.Resources["BgHeader"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16161A"));
                app.Resources["TextWhite"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
                app.Resources["TextGray"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A0A0A5"));
                app.Resources["TextMuted"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#66666A"));
                app.Resources["BorderDark"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A2A2E"));
                app.Resources["BgHover"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#252529"));
                app.Resources["BgItemHover"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E22"));
                app.Resources["BgBtnHover"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2F2F35"));
                app.Resources["BgInput"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#252529"));
                app.Resources["TealAccent"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00ADB5"));
                app.Resources["TealHover"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FFE0"));
            }

            if (!app.Resources.MergedDictionaries.OfType<SharedStyles>().Any())
            {
                app.Resources.MergedDictionaries.Add(new SharedStyles());
            }
        }

        public static DrawingImage CreateSunDrawing(Brush? customBrush = null)
        {
            var brush = customBrush ?? new SolidColorBrush(Color.FromRgb(255, 193, 7));
            var group = new DrawingGroup();
            
            group.Children.Add(new GeometryDrawing(brush, null, new EllipseGeometry(new Point(10, 10), 2.6, 2.6)));

            var pen = new Pen(brush, 1.1)
            {
                StartLineCap = PenLineCap.Flat,
                EndLineCap = PenLineCap.Flat
            };

            var rays = new GeometryGroup();
            double center = 10.0;
            double r1 = 4.4;
            double r2 = 7.4;
            for (int i = 0; i < 8; i++)
            {
                double angle = i * Math.PI / 4.0;
                double x1 = center + r1 * Math.Cos(angle);
                double y1 = center + r1 * Math.Sin(angle);
                double x2 = center + r2 * Math.Cos(angle);
                double y2 = center + r2 * Math.Sin(angle);
                rays.Children.Add(new LineGeometry(new Point(x1, y1), new Point(x2, y2)));
            }
            group.Children.Add(new GeometryDrawing(null, pen, rays));

            var image = new DrawingImage(group);
            image.Freeze();
            return image;
        }

        public static DrawingImage CreateMoonDrawing(Brush? customBrush = null)
        {
            var brush = customBrush ?? new SolidColorBrush(Color.FromRgb(100, 100, 100));
            var group = new DrawingGroup();

            var circle1 = new EllipseGeometry(new Point(9.5, 10), 5.8, 5.8);
            var circle2 = new EllipseGeometry(new Point(13.2, 8.2), 5.2, 5.2);
            var moon = new CombinedGeometry(GeometryCombineMode.Exclude, circle1, circle2);

            group.Children.Add(new GeometryDrawing(brush, null, moon));

            var image = new DrawingImage(group);
            image.Freeze();
            return image;
        }
    }
}
