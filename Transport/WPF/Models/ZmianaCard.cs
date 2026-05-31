// ════════════════════════════════════════════════════════════════════════════
// Transport/WPF/Models/ZmianaCard.cs — wrapper viewmodel dla TransportZmiana.
// Mapuje surowy model na WPF-friendly properties: lepsze emoji (zamiast unicode
// symboli ze starego modelu), WPF Brushes (zamiast System.Drawing.Color),
// obliczona delta liczbowa (+2 / -1) z kolorem. Bez modyfikacji starego modelu.
// ════════════════════════════════════════════════════════════════════════════

using System.Globalization;
using System.Windows.Media;

namespace Kalendarz1.Transport.WPF.Models
{
    public class ZmianaCard
    {
        public TransportZmiana Source { get; }
        public ZmianaCard(TransportZmiana z) { Source = z; }

        public int Id => Source.Id;
        public int ZamowienieId => Source.ZamowienieId;
        public string KlientNazwa => string.IsNullOrEmpty(Source.KlientNazwa) ? $"Zam. #{Source.ZamowienieId}" : Source.KlientNazwa!;
        public string ZgloszonePrzez => Source.ZgloszonePrzez;
        public string TimeAgo => Source.TimeAgo;
        public string TypLabel => Source.TypLabel;
        public string Opis => Source.Opis ?? "";
        public string Stare => string.IsNullOrEmpty(Source.StareWartosc) ? "—" : Source.StareWartosc!;
        public string Nowa => string.IsNullOrEmpty(Source.NowaWartosc) ? "—" : Source.NowaWartosc!;

        public string TypEmoji => Source.TypZmiany switch
        {
            "NoweZamowienie" => "✨",
            "ZmianaIlosci" => "📦",
            "ZmianaPojemnikow" => "📦",
            "ZmianaKg" => "⚖",
            "ZmianaAwizacji" => "⏰",
            "ZmianaTerminu" => "📅",
            "Anulowanie" => "❌",
            "ZmianaUwag" => "📝",
            "ZmianaOdbiorcy" => "🏠",
            "ZmianaDataProdukcji" => "🔧",
            _ => "🔔"
        };

        public Brush AkcentBrush
        {
            get
            {
                var c = Source.TypColor;
                return new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B));
            }
        }
        public Brush AkcentSoft
        {
            get
            {
                var c = Source.TypColor;
                return new SolidColorBrush(Color.FromArgb(0x20, c.R, c.G, c.B));
            }
        }

        // delta liczbowa: tylko gdy obie wartości to liczby (pojemniki, palety, kg)
        public string DeltaText
        {
            get
            {
                if (decimal.TryParse(Source.StareWartosc, NumberStyles.Any, CultureInfo.InvariantCulture, out var st)
                    && decimal.TryParse(Source.NowaWartosc, NumberStyles.Any, CultureInfo.InvariantCulture, out var nw))
                {
                    var d = nw - st;
                    if (d == 0) return "";
                    return d > 0 ? $"+{d.ToString("0.##", CultureInfo.InvariantCulture)}" : d.ToString("0.##", CultureInfo.InvariantCulture);
                }
                return "";
            }
        }

        public Brush DeltaBrush
        {
            get
            {
                if (decimal.TryParse(Source.StareWartosc, NumberStyles.Any, CultureInfo.InvariantCulture, out var st)
                    && decimal.TryParse(Source.NowaWartosc, NumberStyles.Any, CultureInfo.InvariantCulture, out var nw))
                {
                    var d = nw - st;
                    if (d > 0) return new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));  // zielony
                    if (d < 0) return new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));  // czerwony
                }
                return new SolidColorBrush(Color.FromRgb(0x60, 0x6C, 0x76));
            }
        }

        public string ZglosilDisplay => string.IsNullOrEmpty(ZgloszonePrzez)
            ? TimeAgo
            : $"👤 {ZgloszonePrzez} · {TimeAgo}";
    }
}
