using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Kalendarz1.DashboardPrzychodu.Models;

namespace Kalendarz1.DashboardPrzychodu.Converters
{
    /// <summary>
    /// Konwertuje StatusDostawy na kolor tła wiersza
    /// </summary>
    public class StatusToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is StatusDostawy status)
            {
                return status switch
                {
                    StatusDostawy.Zwazony => new SolidColorBrush(Color.FromArgb(40, 78, 204, 163)),      // Zielony przezroczysty
                    StatusDostawy.BruttoWpisane => new SolidColorBrush(Color.FromArgb(40, 255, 179, 71)), // Pomarańczowy przezroczysty
                    _ => new SolidColorBrush(Color.FromArgb(40, 233, 69, 96))                             // Czerwony przezroczysty
                };
            }
            return new SolidColorBrush(Colors.Transparent);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Konwertuje StatusDostawy na kolor tekstu statusu
    /// </summary>
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is StatusDostawy status)
            {
                return status switch
                {
                    StatusDostawy.Zwazony => new SolidColorBrush(Color.FromRgb(78, 204, 163)),      // #4ECCA3 - zielony
                    StatusDostawy.BruttoWpisane => new SolidColorBrush(Color.FromRgb(255, 179, 71)), // #FFB347 - pomarańczowy
                    _ => new SolidColorBrush(Color.FromRgb(233, 69, 96))                             // #E94560 - czerwony
                };
            }
            return new SolidColorBrush(Colors.White);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Konwertuje PoziomOdchylenia na kolor tekstu
    /// </summary>
    public class OdchylenieToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PoziomOdchylenia poziom)
            {
                return poziom switch
                {
                    PoziomOdchylenia.OK => new SolidColorBrush(Color.FromRgb(78, 204, 163)),      // #4ECCA3 - zielony
                    PoziomOdchylenia.Uwaga => new SolidColorBrush(Color.FromRgb(255, 179, 71)),   // #FFB347 - pomarańczowy
                    PoziomOdchylenia.Problem => new SolidColorBrush(Color.FromRgb(233, 69, 96)), // #E94560 - czerwony
                    _ => new SolidColorBrush(Color.FromRgb(148, 163, 184))                        // #94A3B8 - szary
                };
            }
            return new SolidColorBrush(Color.FromRgb(148, 163, 184));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Konwertuje PoziomOdchylenia na ikonę emoji
    /// </summary>
    public class OdchylenieToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PoziomOdchylenia poziom)
            {
                return poziom switch
                {
                    PoziomOdchylenia.OK => "✅",
                    PoziomOdchylenia.Uwaga => "⚠️",
                    PoziomOdchylenia.Problem => "🔴",
                    _ => ""
                };
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Konwertuje StatusDostawy na ikonę emoji
    /// </summary>
    public class StatusToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is StatusDostawy status)
            {
                return status switch
                {
                    StatusDostawy.Zwazony => "✅",
                    StatusDostawy.BruttoWpisane => "⏳",
                    _ => "🔴"
                };
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Konwertuje wartość na Visibility (true/not null = Visible)
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool visible = value switch
            {
                bool b => b,
                int i => i != 0,
                decimal d => d != 0,
                string s => !string.IsNullOrEmpty(s),
                null => false,
                _ => true
            };

            // Parametr "Invert" odwraca logikę
            if (parameter?.ToString()?.ToLower() == "invert")
                visible = !visible;

            return visible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Konwertuje wartość decimal na sformatowany string z separatorami tysięcy
    /// </summary>
    public class DecimalToFormattedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal d)
            {
                string format = parameter?.ToString() ?? "N0";
                return d.ToString(format, new CultureInfo("pl-PL"));
            }
            if (value is int i)
            {
                string format = parameter?.ToString() ?? "N0";
                return i.ToString(format, new CultureInfo("pl-PL"));
            }
            return value?.ToString() ?? "-";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Konwertuje procent na szerokość paska postępu
    /// </summary>
    public class PercentToWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double maxWidth = 200; // domyślna maksymalna szerokość
            if (parameter != null && double.TryParse(parameter.ToString(), out double parsed))
                maxWidth = parsed;

            if (value is int percent)
            {
                double clampedPercent = Math.Max(0, Math.Min(100, percent));
                return clampedPercent / 100 * maxWidth;
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Konwertuje procent realizacji na kolor paska
    /// </summary>
    public class PercentToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int percent)
            {
                if (percent >= 80)
                    return new SolidColorBrush(Color.FromRgb(78, 204, 163));  // Zielony
                if (percent >= 50)
                    return new SolidColorBrush(Color.FromRgb(255, 179, 71));  // Pomarańczowy
                return new SolidColorBrush(Color.FromRgb(233, 69, 96));       // Czerwony
            }
            return new SolidColorBrush(Color.FromRgb(148, 163, 184));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Konwertuje wartość null na "-" lub pusty string
    /// </summary>
    public class NullToDashConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return "-";

            if (value is decimal d && d == 0)
                return "-";

            if (value is int i && i == 0)
                return "-";

            if (value is decimal dec)
                return dec.ToString("N2", new CultureInfo("pl-PL"));

            return value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Multi-value converter - pokazuje odchylenie tylko dla zważonych
    /// </summary>
    public class StatusOdchylenieConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2)
                return "-";

            var status = values[0] as StatusDostawy? ?? StatusDostawy.Oczekuje;
            var odchylenie = values[1]?.ToString() ?? "-";

            return status == StatusDostawy.Zwazony ? odchylenie : "-";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Konwertuje odchylenie procentowe na kolor paska bocznego wiersza
    /// </summary>
    public class OdchylenieToBarColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PoziomOdchylenia poziom)
            {
                return poziom switch
                {
                    PoziomOdchylenia.OK => new SolidColorBrush(Color.FromRgb(78, 204, 163)),       // Zielony #4ECCA3
                    PoziomOdchylenia.Uwaga => new SolidColorBrush(Color.FromRgb(255, 179, 71)),    // Pomarańczowy #FFB347
                    PoziomOdchylenia.Problem => new SolidColorBrush(Color.FromRgb(233, 69, 96)),   // Czerwony #E94560
                    _ => new SolidColorBrush(Color.FromRgb(74, 85, 104))                            // Szary #4A5568
                };
            }
            return new SolidColorBrush(Color.FromRgb(74, 85, 104));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Konwertuje wartość bool na kolor tła wiersza z problemem
    /// </summary>
    public class ProblemToRowBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool jestProblem && jestProblem)
            {
                return new SolidColorBrush(Color.FromRgb(45, 31, 31)); // #2D1F1F - ciemny czerwonawy
            }
            return new SolidColorBrush(Colors.Transparent);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Konwertuje wartosc bool na Brush dla obramowania pulsujacego
    /// </summary>
    public class BoolToBorderBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b)
                return new SolidColorBrush(Color.FromRgb(233, 69, 96)); // Czerwony
            return new SolidColorBrush(Colors.Transparent);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Konwertuje TrendProc na kolor (dla kolumny TREND)
    /// &lt;95% = czerwony (mniej niz plan)
    /// &gt;105% = zielony (wiecej niz plan)
    /// 95-105% = szary (OK)
    /// </summary>
    public class TrendToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            decimal trend = 100;
            if (value is decimal d)
                trend = d;
            else if (value is double dbl)
                trend = (decimal)dbl;
            else if (value is int i)
                trend = i;

            if (trend < 95)
                return new SolidColorBrush(Color.FromRgb(233, 69, 96));  // Czerwony - mniej niz plan
            if (trend > 105)
                return new SolidColorBrush(Color.FromRgb(78, 204, 163)); // Zielony - wiecej niz plan
            return new SolidColorBrush(Color.FromRgb(156, 163, 175));    // Szary - OK
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Konwertuje wartosc bool na Visibility z odwracaniem
    /// </summary>
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return b ? Visibility.Collapsed : Visibility.Visible;
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Konwertuje PoziomAlertu na kolor tla
    /// </summary>
    public class AlertToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string alert)
            {
                if (alert.Contains("KRYTYCZNY"))
                    return new SolidColorBrush(Color.FromRgb(220, 38, 38));
                if (alert.Contains("WYSOKI"))
                    return new SolidColorBrush(Color.FromRgb(251, 191, 36));
                if (alert.Contains("UWAGA"))
                    return new SolidColorBrush(Color.FromRgb(251, 146, 60));
            }
            return new SolidColorBrush(Color.FromRgb(78, 204, 163));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
