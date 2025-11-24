using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Kalendarz1
{
    /// <summary>
    /// Konwerter punktów na kolor tła
    /// </summary>
    public class PointsToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int punkty)
            {
                if (punkty >= 30)
                    return new SolidColorBrush(Color.FromRgb(46, 139, 87)); // Zielony (Sea Green)
                else if (punkty >= 20)
                    return new SolidColorBrush(Color.FromRgb(212, 175, 55)); // Złoty (Gold)
                else
                    return new SolidColorBrush(Color.FromRgb(220, 20, 60)); // Czerwony (Crimson)
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
