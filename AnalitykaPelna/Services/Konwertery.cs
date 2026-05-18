using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media;

namespace Kalendarz1.AnalitykaPelna.Services
{
    /// <summary>
    /// String path → BitmapImage (lub null). Defensywny — sprawdza czy plik istnieje
    /// i ładuje OnLoad żeby nie blokować pliku.
    /// </summary>
    public class SafeImagePathConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string? path = value as string;
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return null;
            try
            {
                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.DecodePixelHeight = 200; // optymalizacja - zmniejszamy do rozmiaru wyświetlania
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>Bool → Visibility (true = Visible, false = Collapsed).</summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>Bool → Visibility odwrócone (true = Collapsed).</summary>
    public class BoolToVisibilityInverseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// String hex (np. "#7C3AED") → SolidColorBrush. Używany w bindings dla dynamicznych kolorów
    /// (kafelki Stan magazynów, statusy, etc.).
    /// </summary>
    public class HexToBrushConverter : IValueConverter
    {
        private static readonly Brush Default = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string hex = (value as string ?? "").Trim();
            if (string.IsNullOrEmpty(hex)) return Default;
            try
            {
                var c = (Color)ColorConverter.ConvertFromString(hex);
                return new SolidColorBrush(c);
            }
            catch
            {
                return Default;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Kategoria towaru ("Mięso"/"Mrozony"/"Zywy"/"Odpady"/"Inne") → kolor pasujący.
    /// Wskazówka wizualna w tabeli — pierwsza kolumna jako pasek kolorystyczny.
    /// </summary>
    public class KategoriaKolorConverter : IValueConverter
    {
        private static readonly Brush Default = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string s = (value as string ?? "").Trim();
            return s switch
            {
                "Mięso" => new SolidColorBrush(Color.FromRgb(0x05, 0x96, 0x69)),    // zielony
                "Mrozony" => new SolidColorBrush(Color.FromRgb(0x08, 0x91, 0xB2)),  // turkus
                "Zywy" => new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),     // pomarańczowy
                "Odpady" => new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),   // szary
                "Inne" => new SolidColorBrush(Color.FromRgb(0x7C, 0x3A, 0xED)),     // fioletowy
                _ => Default
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Wartość % wydajności krojenia → kolor (czerwony &lt; 30, pomarańcz 30–60, zielony &gt; 60).
    /// Subtelne tinted background żeby od razu zobaczyć anomalie w tabeli.
    /// </summary>
    public class WydajnoscKolorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not decimal d) return Brushes.Transparent;
            double v = (double)d;
            if (v <= 0) return Brushes.Transparent;
            if (v < 30) return new SolidColorBrush(Color.FromArgb(0x33, 0xDC, 0x26, 0x26));   // czerwony tint
            if (v < 60) return new SolidColorBrush(Color.FromArgb(0x33, 0xF5, 0x9E, 0x0B));   // żółty tint
            if (v < 90) return new SolidColorBrush(Color.FromArgb(0x33, 0x05, 0x96, 0x69));   // zielony tint
            return new SolidColorBrush(Color.FromArgb(0x55, 0x16, 0xA3, 0x4A));               // mocny zielony
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Wartość kg → gradient zielony (im więcej, tym ciemniejszy zielony) dla heatmapy.
    /// Parameter = max wartość w grupie (do skalowania), domyślnie 5000.
    /// </summary>
    public class HeatmapaKolorConverter : IValueConverter
    {
        private static readonly Brush PustyBrush = Brushes.White;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not decimal kg || kg <= 0) return PustyBrush;

            decimal max = 5000m;
            if (parameter is string ps && decimal.TryParse(ps, NumberStyles.Any,
                CultureInfo.InvariantCulture, out var pmax)) max = pmax;

            // Skala 0..1
            double t = (double)Math.Min(kg, max) / (double)max;
            // Gradient: jasny zielony #DCFCE7 → ciemny zielony #15803D
            byte r = (byte)Lerp(0xDC, 0x15, t);
            byte g = (byte)Lerp(0xFC, 0x80, t);
            byte b = (byte)Lerp(0xE7, 0x3D, t);
            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }

        private static double Lerp(int a, int b, double t) => a + (b - a) * t;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Etap łańcucha (ŻYWIEC PZ / UBÓJ / KROJENIE / MM-) → kolor tła badge'a.
    /// </summary>
    public class EtapTloConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value as string ?? "") switch
            {
                "ŻYWIEC PZ" => new SolidColorBrush(Color.FromRgb(0xEC, 0xFC, 0xCB)),  // jasna zieleń
                "DO UBOJU" => new SolidColorBrush(Color.FromRgb(0xFE, 0xF3, 0xC7)),   // cytrynowy
                "UBÓJ" => new SolidColorBrush(Color.FromRgb(0xFF, 0xF7, 0xED)),       // jasny pomarańcz
                "MM- (przed krojeniem)" => new SolidColorBrush(Color.FromRgb(0xE0, 0xF2, 0xFE)),  // jasny niebieski
                "DO KROJENIA" => new SolidColorBrush(Color.FromRgb(0xF3, 0xE8, 0xFF)),  // jasny fiolet
                "KROJENIE" => new SolidColorBrush(Color.FromRgb(0xEF, 0xF6, 0xFF)),     // bardzo jasny niebieski
                "MM- (po krojeniu)" => new SolidColorBrush(Color.FromRgb(0xF3, 0xF4, 0xF6)),  // szary
                _ => new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9))
            };

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Items w grupie WydajnoscKlasa → konkretna metryka per ConverterParameter.
    /// Parametry: "Wazen", "Kg", "Min", "Sr", "Max", "Udzial".
    /// </summary>
    public class GrupaKlasMetrykConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not System.Collections.IEnumerable items) return "";
            var lista = new List<Models.WydajnoscKlasa>();
            foreach (var x in items)
                if (x is Models.WydajnoscKlasa k) lista.Add(k);
            if (lista.Count == 0) return "";

            string p = (parameter as string) ?? "";
            switch (p)
            {
                case "Wazen":
                    return lista.Sum(k => k.LiczbaWazen).ToString("N0");
                case "Kg":
                    return lista.Sum(k => k.SumaActWeightKg).ToString("N0");
                case "Min":
                    {
                        var min = lista.Min(k => k.MinWagaSzt);
                        return min <= 0 ? "—" : min.ToString("N3");
                    }
                case "Sr":
                    {
                        int sumW = lista.Sum(k => k.LiczbaWazen);
                        if (sumW <= 0) return "—";
                        decimal sumKg = lista.Sum(k => k.SumaActWeightKg);
                        return (sumKg / sumW).ToString("N3");
                    }
                case "Max":
                    {
                        var max = lista.Max(k => k.MaxWagaSzt);
                        return max <= 0 ? "—" : max.ToString("N3");
                    }
                case "Udzial":
                    return lista.Sum(k => k.ProcentUdzialu).ToString("N1") + "%";
                default:
                    return "";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Items w grupie (DataGrid GroupStyle) → podsumowanie tekstowe „N poz. • suma kg".
    /// </summary>
    public class GrupaPodsumowanieConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not System.Collections.IEnumerable items) return "";
            int count = 0;
            decimal suma = 0;
            foreach (var x in items)
            {
                count++;
                var kgProp = x.GetType().GetProperty("Kg");
                if (kgProp != null && kgProp.GetValue(x) is decimal kg) suma += kg;
            }
            return $"{count} poz.  •  {suma:N0} kg";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Etap łańcucha → kolor tekstu badge'a (kontrastujący z tłem).
    /// </summary>
    public class EtapKolorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value as string ?? "") switch
            {
                "ŻYWIEC PZ" => new SolidColorBrush(Color.FromRgb(0x36, 0x53, 0x14)),
                "DO UBOJU" => new SolidColorBrush(Color.FromRgb(0x78, 0x35, 0x0F)),
                "UBÓJ" => new SolidColorBrush(Color.FromRgb(0x7C, 0x2D, 0x12)),
                "MM- (przed krojeniem)" => new SolidColorBrush(Color.FromRgb(0x0C, 0x4A, 0x6E)),
                "DO KROJENIA" => new SolidColorBrush(Color.FromRgb(0x6B, 0x21, 0xA8)),
                "KROJENIE" => new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x8A)),
                "MM- (po krojeniu)" => new SolidColorBrush(Color.FromRgb(0x37, 0x41, 0x51)),
                _ => new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69))
            };

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Wartość kg → biały foreground gdy tło jest ciemne (kontrast w heatmapie).
    /// </summary>
    public class HeatmapaForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not decimal kg || kg <= 0) return Brushes.Transparent;
            decimal max = 5000m;
            if (parameter is string ps && decimal.TryParse(ps, NumberStyles.Any,
                CultureInfo.InvariantCulture, out var pmax)) max = pmax;
            double t = (double)Math.Min(kg, max) / (double)max;
            return t > 0.6 ? Brushes.White : Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
