using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Kalendarz1.MarketIntelligence.Models;

namespace Kalendarz1.MarketIntelligence.Converters
{
    /// <summary>
    /// Konwertuje ImpactLevel na kolor paska bocznego karty
    /// </summary>
    public class ImpactLevelToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ImpactLevel impact)
            {
                var colorHex = impact switch
                {
                    ImpactLevel.Critical => "#E53935",   // Czerwony
                    ImpactLevel.High => "#FB8C00",       // Pomarańczowy
                    ImpactLevel.Medium => "#FDD835",     // Żółty
                    ImpactLevel.Low => "#43A047",        // Zielony
                    _ => "#78909C"                       // Szary
                };
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Konwertuje SentimentScore (-1 do +1) na kolor
    /// </summary>
    public class SentimentScoreToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double score)
            {
                var colorHex = score switch
                {
                    >= 0.5 => "#4CAF50",   // Ciemnozielony - bardzo pozytywny
                    >= 0.2 => "#8BC34A",   // Jasnozielony - pozytywny
                    >= -0.2 => "#9E9E9E",  // Szary - neutralny
                    >= -0.5 => "#FF9800",  // Pomarańczowy - negatywny
                    _ => "#F44336"          // Czerwony - bardzo negatywny
                };
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Konwertuje SentimentScore na ikonę strzałki
    /// </summary>
    public class SentimentScoreToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double score)
            {
                return score switch
                {
                    >= 0.5 => "▲",    // Bardzo pozytywny
                    >= 0.1 => "↗",    // Pozytywny
                    <= -0.5 => "▼",   // Bardzo negatywny
                    <= -0.1 => "↘",   // Negatywny
                    _ => "━"          // Neutralny
                };
            }
            return "━";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Konwertuje kategorię na kolor tła badge
    /// </summary>
    public class CategoryToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string category)
            {
                var colorHex = category?.ToUpperInvariant() switch
                {
                    "HPAI" => "#D32F2F",
                    "CENY" => "#1976D2",
                    "KONKURENCJA" => "#7B1FA2",
                    "REGULACJE" => "#455A64",
                    "KLIENCI" => "#00796B",
                    "EKSPORT" => "#0288D1",
                    "IMPORT" => "#F57C00",
                    "KOSZTY" => "#C2185B",
                    "POGODA" => "#0097A7",
                    "LOGISTYKA" => "#5D4037",
                    "INWESTYCJE" => "#388E3C",
                    _ => "#616161"
                };
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Konwertuje SeverityLevel na kolor
    /// </summary>
    public class SeverityToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SeverityLevel severity)
            {
                var colorHex = severity switch
                {
                    SeverityLevel.Critical => "#E53935",
                    SeverityLevel.Warning => "#FB8C00",
                    SeverityLevel.Positive => "#43A047",
                    SeverityLevel.Info => "#1976D2",
                    _ => "#78909C"
                };
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Konwertuje string na Brush (dla pól zwracających hex color)
    /// </summary>
    public class StringToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string colorHex && !string.IsNullOrEmpty(colorHex))
            {
                try
                {
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
                }
                catch
                {
                    return new SolidColorBrush(Colors.Gray);
                }
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Konwertuje ImpactLevel na widoczność (pokazuje tylko dla High i Critical)
    /// </summary>
    public class ImpactLevelToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ImpactLevel impact)
            {
                return (impact == ImpactLevel.Critical || impact == ImpactLevel.High)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Konwertuje bool na Visibility
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                // Jeśli parametr = "Inverse", odwróć logikę
                if (parameter?.ToString() == "Inverse")
                    return boolValue ? Visibility.Collapsed : Visibility.Visible;
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Konwertuje string na Visibility (pokazuje gdy nie jest pusty)
    /// </summary>
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var hasValue = !string.IsNullOrWhiteSpace(value?.ToString());
            if (parameter?.ToString() == "Inverse")
                return hasValue ? Visibility.Collapsed : Visibility.Visible;
            return hasValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Konwertuje ImpactLevel na tekst polski
    /// </summary>
    public class ImpactLevelToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ImpactLevel impact)
            {
                return impact switch
                {
                    ImpactLevel.Critical => "KRYTYCZNY",
                    ImpactLevel.High => "WYSOKI",
                    ImpactLevel.Medium => "ŚREDNI",
                    ImpactLevel.Low => "NISKI",
                    _ => "NIEZNANY"
                };
            }
            return "NIEZNANY";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Skraca tekst do określonej długości
    /// </summary>
    public class TruncateTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string text && !string.IsNullOrEmpty(text))
            {
                int maxLength = 100;
                if (parameter != null && int.TryParse(parameter.ToString(), out var parsedLength))
                {
                    maxLength = parsedLength;
                }
                return text.Length > maxLength ? text.Substring(0, maxLength - 3) + "..." : text;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Konwertuje SentimentScore na procent (do paska postępu)
    /// </summary>
    public class SentimentScoreToPercentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double score)
            {
                // Mapuj -1 do +1 na 0 do 100
                return (score + 1) * 50;
            }
            return 50;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Multi-value converter do określania koloru na podstawie wielu wartości
    /// </summary>
    public class MultiValueToColorConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // values[0] = SentimentScore, values[1] = ImpactLevel
            if (values.Length >= 2 && values[0] is double score && values[1] is ImpactLevel impact)
            {
                // Dla Critical impact, zawsze czerwony
                if (impact == ImpactLevel.Critical)
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E53935"));

                // W przeciwnym razie bazuj na sentymencie
                var colorHex = score switch
                {
                    >= 0.3 => "#4CAF50",
                    >= 0 => "#8BC34A",
                    <= -0.3 => "#F44336",
                    _ => "#FF9800"
                };
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
