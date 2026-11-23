using System;
using System.Globalization;
using System.Windows.Data;

namespace MusicMediaKeys
{
    public sealed class SliderFillWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 4 ||
                values[0] is not double width ||
                values[1] is not double value ||
                values[2] is not double minimum ||
                values[3] is not double maximum)
            {
                return 0.0;
            }

            if (double.IsNaN(width) || width <= 0 || maximum <= minimum)
                return 0.0;

            double ratio = Math.Clamp((value - minimum) / (maximum - minimum), 0.0, 1.0);
            return ratio * width;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public sealed class SliderKnobOffsetConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 5 ||
                values[0] is not double width ||
                values[1] is not double value ||
                values[2] is not double minimum ||
                values[3] is not double maximum ||
                values[4] is not double knobWidth)
            {
                return 0.0;
            }

            if (double.IsNaN(width) || width <= 0 || maximum <= minimum)
                return 0.0;

            double ratio = Math.Clamp((value - minimum) / (maximum - minimum), 0.0, 1.0);
            double maxOffset = Math.Max(0.0, width - knobWidth);
            return Math.Clamp(ratio * width - knobWidth / 2.0, 0.0, maxOffset);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public sealed class SliderKnobSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double trackHeight))
                return trackHeight >= 4 ? 12.0 : 9.0;
            if (value is double d)
                return d >= 4 ? 12.0 : 9.0;
            return 12.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
