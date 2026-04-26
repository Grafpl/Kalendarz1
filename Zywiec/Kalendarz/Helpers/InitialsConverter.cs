using System;
using System.Globalization;

namespace Kalendarz1.Zywiec.Kalendarz
{
    /// <summary>
    /// Konwerter do wyświetlania inicjałów z imienia i nazwiska
    /// </summary>
    public class InitialsConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string name && !string.IsNullOrWhiteSpace(name))
            {
                var parts = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                    return $"{char.ToUpper(parts[0][0])}{char.ToUpper(parts[1][0])}";
                else if (parts.Length == 1 && parts[0].Length >= 2)
                    return $"{char.ToUpper(parts[0][0])}{char.ToUpper(parts[0][1])}";
                else if (parts.Length == 1)
                    return parts[0].Substring(0, 1).ToUpper();
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
