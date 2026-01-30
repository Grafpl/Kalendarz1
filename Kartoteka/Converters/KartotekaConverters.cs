using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Kalendarz1.Kartoteka.Converters
{
    public class KategoriaToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString() switch
            {
                "A" => new SolidColorBrush(Color.FromRgb(250, 204, 21)),   // #FACC15 yellow-400
                "B" => new SolidColorBrush(Color.FromRgb(167, 243, 208)),  // #A7F3D0 emerald-200
                _ => new SolidColorBrush(Color.FromRgb(229, 231, 235))     // #E5E7EB gray-200
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class KategoriaToForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString() switch
            {
                "A" => new SolidColorBrush(Color.FromRgb(113, 63, 18)),   // #713F12 yellow-900
                "B" => new SolidColorBrush(Color.FromRgb(6, 95, 70)),     // #065F46 emerald-800
                _ => new SolidColorBrush(Color.FromRgb(75, 85, 99))       // #4B5563 gray-600
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class AlertToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString() switch
            {
                "LimitExceeded" => new SolidColorBrush(Color.FromRgb(254, 226, 226)),  // red-100
                "Overdue" => new SolidColorBrush(Color.FromRgb(254, 249, 195)),         // yellow-100
                "Inactive" => new SolidColorBrush(Color.FromRgb(255, 237, 213)),        // orange-100
                "NewClient" => new SolidColorBrush(Color.FromRgb(219, 234, 254)),       // blue-100
                _ => new SolidColorBrush(Colors.Transparent)
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class AlertToBorderConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString() switch
            {
                "LimitExceeded" => new SolidColorBrush(Color.FromRgb(248, 113, 113)),  // red-400
                "Overdue" => new SolidColorBrush(Color.FromRgb(250, 204, 21)),          // yellow-400
                "Inactive" => new SolidColorBrush(Color.FromRgb(251, 146, 60)),         // orange-400
                "NewClient" => new SolidColorBrush(Color.FromRgb(96, 165, 250)),        // blue-400
                _ => new SolidColorBrush(Colors.Transparent)
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class AlertToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString() switch
            {
                "LimitExceeded" => "\U0001F534",  // red circle
                "Overdue" => "\U0001F7E1",         // yellow circle
                "Inactive" => "\U0001F7E0",        // orange circle
                "NewClient" => "\U0001F535",       // blue circle
                _ => ""
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class ProcentToProgressColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double procent)
            {
                if (procent > 100) return new SolidColorBrush(Color.FromRgb(239, 68, 68));   // red-500
                if (procent > 80) return new SolidColorBrush(Color.FromRgb(234, 179, 8));     // yellow-500
                return new SolidColorBrush(Color.FromRgb(34, 197, 94));                        // green-500
            }
            return new SolidColorBrush(Color.FromRgb(34, 197, 94));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class DecimalToFormattedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal d)
            {
                if (d >= 1_000_000) return $"{d / 1_000_000:N1}M";
                if (d >= 1_000) return $"{d / 1_000:N0}k";
                return d.ToString("N0");
            }
            return "0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool boolValue = value is bool b && b;
            bool invert = parameter?.ToString()?.ToLower() == "invert";
            if (invert) boolValue = !boolValue;
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is Visibility v && v == Visibility.Visible;
    }

    public class FakturaStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString() switch
            {
                "Zapłacona" => new SolidColorBrush(Color.FromRgb(22, 163, 74)),       // green-600
                "Przeterminowana" => new SolidColorBrush(Color.FromRgb(220, 38, 38)),  // red-600
                "Nieopłacona" => new SolidColorBrush(Color.FromRgb(202, 138, 4)),      // yellow-600
                "Anulowana" => new SolidColorBrush(Color.FromRgb(107, 114, 128)),      // gray-500
                _ => new SolidColorBrush(Color.FromRgb(107, 114, 128))
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
