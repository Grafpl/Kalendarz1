using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Kalendarz1.Opakowania.Models;

namespace Kalendarz1.Opakowania.Converters
{
    /// <summary>
    /// Konwerter boolean -> Visibility
    /// </summary>
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
        {
            return value is Visibility v && v == Visibility.Visible;
        }
    }

    /// <summary>
    /// Konwerter salda na kolor (dodatnie=czerwony, ujemne=zielony) - JASNY MOTYW
    /// </summary>
    public class SaldoToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int saldo)
            {
                if (saldo > 0)
                    return new SolidColorBrush(Color.FromRgb(231, 76, 60)); // #E74C3C - Czerwony
                if (saldo < 0)
                    return new SolidColorBrush(Color.FromRgb(39, 174, 96)); // #27AE60 - Zielony
                return new SolidColorBrush(Color.FromRgb(127, 140, 141)); // #7F8C8D - Szary
            }
            return new SolidColorBrush(Color.FromRgb(127, 140, 141));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Konwerter salda na tekst z formatowaniem: "150 (wydane)" lub "50 (zwrot)"
    /// </summary>
    public class SaldoToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int saldo)
            {
                if (saldo == 0) return "0";
                if (saldo > 0) return $"{saldo} (wydane)";
                return $"{Math.Abs(saldo)} (zwrot)";
            }
            return "0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                var parts = str.Split(' ');
                if (parts.Length > 0 && int.TryParse(parts[0], out int result))
                {
                    if (str.Contains("zwrot")) return -result;
                    return result;
                }
            }
            return 0;
        }
    }

    /// <summary>
    /// Konwerter boolean na kolor tła wiersza (dla potwierdzeń) - JASNY MOTYW
    /// </summary>
    public class PotwierdzenieToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool jestPotwierdzone && jestPotwierdzone)
            {
                return new SolidColorBrush(Color.FromRgb(232, 245, 233)); // #E8F5E9 - Jasny zielony
            }
            return new SolidColorBrush(Colors.Transparent);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Konwerter wiersza salda na kolor tła (na podstawie progów) - JASNY MOTYW
    /// </summary>
    public class SaldoRowBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SaldoOpakowania saldo)
            {
                if (saldo.MaxSaldoDodatnie >= SaldoOpakowania.ProgKrytyczny)
                    return new SolidColorBrush(Color.FromRgb(255, 235, 238)); // #FFEBEE - Jasny czerwony
                if (saldo.MaxSaldoDodatnie >= SaldoOpakowania.ProgOstrzezenia)
                    return new SolidColorBrush(Color.FromRgb(255, 243, 224)); // #FFF3E0 - Jasny pomarańczowy
            }
            return new SolidColorBrush(Colors.Transparent);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Konwerter statusu potwierdzenia na kolor - JASNY MOTYW
    /// </summary>
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                return status switch
                {
                    "Potwierdzone" => new SolidColorBrush(Color.FromRgb(39, 174, 96)), // #27AE60
                    "Rozbieżność" => new SolidColorBrush(Color.FromRgb(231, 76, 60)), // #E74C3C
                    "Oczekujące" => new SolidColorBrush(Color.FromRgb(243, 156, 18)), // #F39C12
                    "Anulowane" => new SolidColorBrush(Color.FromRgb(149, 165, 166)), // #95A5A6
                    _ => new SolidColorBrush(Color.FromRgb(149, 165, 166))
                };
            }
            return new SolidColorBrush(Color.FromRgb(149, 165, 166));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Konwerter null -> Visibility
    /// </summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool invert = parameter?.ToString()?.ToLower() == "invert";
            bool isNull = value == null;

            if (invert) isNull = !isNull;

            return isNull ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Konwerter daty na tekst
    /// </summary>
    public class DateToTextConverter : IValueConverter
    {
        public string Format { get; set; } = "dd.MM.yyyy";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime date)
            {
                return date.ToString(Format);
            }
            if (value is DateTime?)
            {
                var nullableDate = (DateTime?)value;
                if (nullableDate.HasValue)
                {
                    return nullableDate.Value.ToString(Format);
                }
            }
            return "-";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str && DateTime.TryParse(str, out DateTime result))
                return result;
            return null;
        }
    }

    /// <summary>
    /// Konwerter odwrotny boolean
    /// </summary>
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && !b;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && !b;
        }
    }

    /// <summary>
    /// Konwerter dla koloru tła karty salda - JASNY MOTYW
    /// </summary>
    public class SaldoCardBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int saldo)
            {
                if (saldo > 0)
                    return new SolidColorBrush(Color.FromRgb(255, 235, 238)); // #FFEBEE - Jasny czerwony
                if (saldo < 0)
                    return new SolidColorBrush(Color.FromRgb(232, 245, 233)); // #E8F5E9 - Jasny zielony
                return new SolidColorBrush(Color.FromRgb(248, 249, 250)); // #F8F9FA - Szary
            }
            return new SolidColorBrush(Color.FromRgb(248, 249, 250));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Konwerter boolean -> FontWeight
    /// </summary>
    public class BoolToFontWeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b)
                return FontWeights.Bold;
            return FontWeights.Normal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Konwerter zero -> Visibility (dla pustych list)
    /// </summary>
    public class ZeroToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count && count == 0)
                return Visibility.Visible;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Konwerter boolean -> tło ostrzeżenia - JASNY MOTYW
    /// </summary>
    public class BoolToWarningBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool hasWarning && hasWarning)
                return new SolidColorBrush(Color.FromRgb(255, 243, 224)); // #FFF3E0 - Pomarańczowy
            return new SolidColorBrush(Color.FromRgb(232, 245, 233)); // #E8F5E9 - Zielony
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Konwerter statusu -> tło - JASNY MOTYW
    /// </summary>
    public class StatusToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                return status switch
                {
                    "Potwierdzone" => new SolidColorBrush(Color.FromRgb(232, 245, 233)), // #E8F5E9
                    "Rozbieżność" => new SolidColorBrush(Color.FromRgb(255, 235, 238)), // #FFEBEE
                    "Oczekujące" => new SolidColorBrush(Color.FromRgb(255, 243, 224)), // #FFF3E0
                    "Anulowane" => new SolidColorBrush(Color.FromRgb(236, 240, 241)), // #ECF0F1
                    _ => new SolidColorBrush(Color.FromRgb(236, 240, 241))
                };
            }
            return new SolidColorBrush(Color.FromRgb(236, 240, 241));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Konwerter dokumentu -> tło - JASNY MOTYW
    /// </summary>
    public class DokumentBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string typDokumentu)
            {
                return typDokumentu switch
                {
                    "WZ" or "Wydanie" => new SolidColorBrush(Color.FromRgb(255, 235, 238)), // #FFEBEE - Czerwony
                    "PZ" or "Przyjęcie" => new SolidColorBrush(Color.FromRgb(232, 245, 233)), // #E8F5E9 - Zielony
                    "Saldo" or "BO" => new SolidColorBrush(Color.FromRgb(255, 249, 196)), // #FFF9C4 - Żółty
                    _ => new SolidColorBrush(Colors.Transparent)
                };
            }
            return new SolidColorBrush(Colors.Transparent);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Konwerter boolean -> tekst grupowania
    /// </summary>
    public class BoolToGroupTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isGrouped)
            {
                return isGrouped ? "Rozgrupuj" : "Grupuj";
            }
            return "Grupuj";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Konwerter progu - sprawdza czy wartość przekracza progi ostrzeżenia/krytyczny
    /// </summary>
    public class ThresholdConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return false;

            int intValue = 0;
            if (value is int i) intValue = i;
            else if (value is double d) intValue = (int)d;
            else if (value is decimal dec) intValue = (int)dec;
            else if (int.TryParse(value.ToString(), out int parsed)) intValue = parsed;

            string level = parameter?.ToString() ?? "Warning";

            int progOstrzezenia = SaldoOpakowania.ProgOstrzezenia;
            int progKrytyczny = SaldoOpakowania.ProgKrytyczny;

            if (level == "Critical")
                return intValue >= progKrytyczny;
            else // Warning
                return intValue >= progOstrzezenia && intValue < progKrytyczny;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Konwerter dla MultiBinding - sprawdza wiele wartości
    /// </summary>
    public class MultiBoolToVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            foreach (var value in values)
            {
                if (value is bool b && !b)
                    return Visibility.Collapsed;
            }
            return Visibility.Visible;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Konwerter wartości na szerokość (dla wykresów słupkowych)
    /// </summary>
    public class ValueToWidthConverter : IValueConverter
    {
        public double MaxWidth { get; set; } = 200;
        public double MaxValue { get; set; } = 100;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue)
            {
                double ratio = Math.Min(1.0, Math.Abs(intValue) / MaxValue);
                return ratio * MaxWidth;
            }
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Konwerter typu opakowania na emoji
    /// </summary>
    public class TypOpakowaniToEmojiConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TypOpakowania typ)
            {
                return typ.Kod switch
                {
                    "E2" => "📦",
                    "H1" => "🎨",
                    "EURO" => "🟩",
                    "PCV" => "🟪",
                    "DREW" => "🟫",
                    _ => "📦"
                };
            }
            if (value is string kod)
            {
                return kod switch
                {
                    "E2" => "📦",
                    "H1" => "🎨",
                    "EURO" => "🟩",
                    "PCV" => "🟪",
                    "DREW" => "🟫",
                    _ => "📦"
                };
            }
            return "📦";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Konwerter typu opakowania na kolor
    /// </summary>
    public class TypOpakowaniToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string kod = null;
            if (value is TypOpakowania typ)
                kod = typ.Kod;
            else if (value is string s)
                kod = s;

            return kod switch
            {
                "E2" => new SolidColorBrush(Color.FromRgb(52, 152, 219)), // #3498DB
                "H1" => new SolidColorBrush(Color.FromRgb(230, 126, 34)), // #E67E22
                "EURO" => new SolidColorBrush(Color.FromRgb(39, 174, 96)), // #27AE60
                "PCV" => new SolidColorBrush(Color.FromRgb(155, 89, 182)), // #9B59B6
                "DREW" => new SolidColorBrush(Color.FromRgb(243, 156, 18)), // #F39C12
                _ => new SolidColorBrush(Color.FromRgb(127, 140, 141)) // #7F8C8D
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Konwerter typu opakowania na jasne tło
    /// </summary>
    public class TypOpakowaniToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string kod = null;
            if (value is TypOpakowania typ)
                kod = typ.Kod;
            else if (value is string s)
                kod = s;

            return kod switch
            {
                "E2" => new SolidColorBrush(Color.FromRgb(235, 245, 255)), // #EBF5FF
                "H1" => new SolidColorBrush(Color.FromRgb(255, 247, 237)), // #FFF7ED
                "EURO" => new SolidColorBrush(Color.FromRgb(232, 245, 233)), // #E8F5E9
                "PCV" => new SolidColorBrush(Color.FromRgb(243, 232, 255)), // #F3E8FF
                "DREW" => new SolidColorBrush(Color.FromRgb(254, 243, 199)), // #FEF3C7
                _ => new SolidColorBrush(Color.FromRgb(248, 249, 250)) // #F8F9FA
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Konwerter daty na tekst z dniem tygodnia (np. "01.12.2025 Pn")
    /// </summary>
    public class DateWithDayOfWeekConverter : IValueConverter
    {
        private static readonly string[] DniTygodnia = { "Nd", "Pn", "Wt", "Sr", "Cz", "Pt", "Sb" };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime date)
            {
                string dzien = DniTygodnia[(int)date.DayOfWeek];
                return $"{date:dd.MM.yyyy} {dzien}";
            }
            if (value is DateTime? nullableDate && nullableDate.HasValue)
            {
                string dzien = DniTygodnia[(int)nullableDate.Value.DayOfWeek];
                return $"{nullableDate.Value:dd.MM.yyyy} {dzien}";
            }
            return "-";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Konwerter odwrotny boolean -> Visibility (true = Collapsed, false = Visible)
    /// </summary>
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool boolValue = value is bool b && b;
            return boolValue ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility v && v == Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Konwerter niezerowej wartości -> Visibility (0 = Collapsed, inne = Visible)
    /// </summary>
    public class NonZeroToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue)
                return intValue != 0 ? Visibility.Visible : Visibility.Collapsed;
            if (value is double doubleValue)
                return doubleValue != 0 ? Visibility.Visible : Visibility.Collapsed;
            if (value is decimal decimalValue)
                return decimalValue != 0 ? Visibility.Visible : Visibility.Collapsed;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Konwerter typu dokumentu (MW1, MP) na kolor tła
    /// </summary>
    public class TypToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string typDokumentu)
            {
                return typDokumentu switch
                {
                    "MW1" => new SolidColorBrush(Color.FromRgb(255, 235, 238)), // #FFEBEE - Jasny czerwony
                    "MP" => new SolidColorBrush(Color.FromRgb(232, 245, 233)),  // #E8F5E9 - Jasny zielony
                    _ => new SolidColorBrush(Color.FromRgb(241, 245, 249))      // #F1F5F9 - Jasny szary
                };
            }
            return new SolidColorBrush(Color.FromRgb(241, 245, 249));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Konwerter typu dokumentu (MW1, MP) na kolor tekstu
    /// </summary>
    public class TypToForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string typDokumentu)
            {
                return typDokumentu switch
                {
                    "MW1" => new SolidColorBrush(Color.FromRgb(231, 76, 60)),  // #E74C3C - Czerwony
                    "MP" => new SolidColorBrush(Color.FromRgb(39, 174, 96)),   // #27AE60 - Zielony
                    _ => new SolidColorBrush(Color.FromRgb(71, 85, 105))       // #475569 - Szary
                };
            }
            return new SolidColorBrush(Color.FromRgb(71, 85, 105));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Konwerter wartości względnej na procent (dla wykresów)
    /// </summary>
    public class RelativeValueToPercentConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is int current && values[1] is int max && max > 0)
            {
                return (double)Math.Abs(current) / Math.Abs(max) * 100;
            }
            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
