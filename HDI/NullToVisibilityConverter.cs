using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Kalendarz1.HDI
{
    /// <summary>
    /// Null → Visible (pokazuje placeholder), not-null → Collapsed.
    /// Używane do pokazywania placeholdera 📦 gdy obrazek towaru nie istnieje.
    /// </summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value == null ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
