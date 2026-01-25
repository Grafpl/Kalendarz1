using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Kalendarz1.DashboardPrzychodu.Models;

namespace Kalendarz1.DashboardPrzychodu.Converters
{
    // ====================================
    // PALETA KOLORÓW - WARM INDUSTRIAL
    // ====================================
    // Tła: #1c1917, #292524, #44403c, #57534e
    // Tekst: #e7e5e4, #a8a29e, #78716c
    // Amber: #f59e0b, #fbbf24
    // Green: #22c55e
    // Red: #ef4444, #7f1d1d
    // Blue: #60a5fa
    // Purple: #c084fc
    // ====================================

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
                    StatusDostawy.Zwazony => new SolidColorBrush(Color.FromArgb(30, 34, 197, 94)),       // Zielony przezroczysty #22c55e
                    StatusDostawy.BruttoWpisane => new SolidColorBrush(Color.FromArgb(30, 251, 191, 36)), // Amber przezroczysty #fbbf24
                    _ => new SolidColorBrush(Color.FromArgb(30, 239, 68, 68))                             // Czerwony przezroczysty #ef4444
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
                    StatusDostawy.Zwazony => new SolidColorBrush(Color.FromRgb(34, 197, 94)),       // #22c55e - zielony
                    StatusDostawy.BruttoWpisane => new SolidColorBrush(Color.FromRgb(251, 191, 36)), // #fbbf24 - amber
                    _ => new SolidColorBrush(Color.FromRgb(239, 68, 68))                             // #ef4444 - czerwony
                };
            }
            return new SolidColorBrush(Color.FromRgb(231, 229, 228)); // #e7e5e4
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
                    PoziomOdchylenia.OK => new SolidColorBrush(Color.FromRgb(34, 197, 94)),       // #22c55e - zielony
                    PoziomOdchylenia.Uwaga => new SolidColorBrush(Color.FromRgb(251, 191, 36)),   // #fbbf24 - amber
                    PoziomOdchylenia.Problem => new SolidColorBrush(Color.FromRgb(239, 68, 68)), // #ef4444 - czerwony
                    _ => new SolidColorBrush(Color.FromRgb(168, 162, 158))                        // #a8a29e - szary
                };
            }
            return new SolidColorBrush(Color.FromRgb(168, 162, 158)); // #a8a29e
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
                    return new SolidColorBrush(Color.FromRgb(34, 197, 94));   // #22c55e Zielony
                if (percent >= 50)
                    return new SolidColorBrush(Color.FromRgb(251, 191, 36)); // #fbbf24 Amber
                return new SolidColorBrush(Color.FromRgb(239, 68, 68));       // #ef4444 Czerwony
            }
            return new SolidColorBrush(Color.FromRgb(168, 162, 158)); // #a8a29e
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
                    PoziomOdchylenia.OK => new SolidColorBrush(Color.FromRgb(34, 197, 94)),       // #22c55e Zielony
                    PoziomOdchylenia.Uwaga => new SolidColorBrush(Color.FromRgb(251, 191, 36)),   // #fbbf24 Amber
                    PoziomOdchylenia.Problem => new SolidColorBrush(Color.FromRgb(239, 68, 68)), // #ef4444 Czerwony
                    _ => new SolidColorBrush(Color.FromRgb(68, 64, 60))                            // #44403c Szary
                };
            }
            return new SolidColorBrush(Color.FromRgb(68, 64, 60)); // #44403c
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
                return new SolidColorBrush(Color.FromRgb(239, 68, 68)); // #ef4444 Czerwony
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
                return new SolidColorBrush(Color.FromRgb(239, 68, 68));   // #ef4444 Czerwony - mniej niz plan
            if (trend > 105)
                return new SolidColorBrush(Color.FromRgb(34, 197, 94));   // #22c55e Zielony - wiecej niz plan
            return new SolidColorBrush(Color.FromRgb(168, 162, 158));     // #a8a29e Szary - OK
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
                    return new SolidColorBrush(Color.FromRgb(239, 68, 68));   // #ef4444
                if (alert.Contains("WYSOKI"))
                    return new SolidColorBrush(Color.FromRgb(251, 191, 36));  // #fbbf24
                if (alert.Contains("UWAGA"))
                    return new SolidColorBrush(Color.FromRgb(245, 158, 11)); // #f59e0b
            }
            return new SolidColorBrush(Color.FromRgb(34, 197, 94)); // #22c55e
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
