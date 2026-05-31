// ════════════════════════════════════════════════════════════════════════════
// Transport/WPF/Models/ZmianaCard.cs — wrapper viewmodel dla TransportZmiana.
// Obsługuje GRUPOWANIE wielu kolejnych zmian tego samego typu na tym samym
// zamówieniu (np. handlowiec edytował kg 7 razy → 1 karta z deltą najstarsza→
// najnowsza, badge "×7"). Akceptacja/odrzucenie obejmuje WSZYSTKIE IDs w grupie.
// Filtruje typ "ZmianaStatusu" (wewnętrzny przy przypisywaniu kursów — nie
// edycja handlowca).
// ════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Kalendarz1.Transport.WPF.Models
{
    public class ZmianaCard
    {
        /// <summary>Najnowsza zmiana w grupie — używana do meta (typ, klient, kto/kiedy).</summary>
        public TransportZmiana Source { get; }

        /// <summary>Wszystkie ID zmian w tej grupie (do akceptacji/odrzucenia hurtem).</summary>
        public List<int> Ids { get; }

        public int IloscScalonych => Ids.Count;

        private readonly string _stareOd;
        private readonly DateTime _najstarszaData;

        public ZmianaCard(TransportZmiana z) : this(new[] { z }) { }

        public ZmianaCard(IEnumerable<TransportZmiana> grupa)
        {
            var lista = grupa.OrderBy(z => z.DataZgloszenia).ToList();
            if (lista.Count == 0) throw new ArgumentException("Pusta grupa zmian");
            Source = lista[lista.Count - 1];                         // najnowsza → meta
            Ids = lista.Select(z => z.Id).ToList();
            _stareOd = lista[0].StareWartosc ?? "";                  // najstarsza → punkt wyjścia delty
            _najstarszaData = lista[0].DataZgloszenia;
        }

        /// <summary>Typy zmian filtrowane (szum) — JEDNO źródło prawdy dla licznika 🔔 i listy kart:
        /// — "ZmianaStatusu" (wewnętrzny — przy przypisywaniu kursów),
        /// — "ZmianaUwag" (zmiana notatek — niska wartość dla logistyka).
        /// PUBLIC — żeby serwis (mapa pendingów) i UI (karty) używały tego samego filtra.
        /// Inaczej: badge mówi "1", panel pokazuje pustkę → click bez efektu.</summary>
        public static readonly HashSet<string> TypyDoUkrycia = new() { "ZmianaStatusu", "ZmianaUwag" };

        /// <summary>Grupuje surowe zmiany po (ZamowienieId, TypZmiany) → karty z deltą najstarsza→najnowsza.</summary>
        public static List<ZmianaCard> ScalListe(IEnumerable<TransportZmiana> raw)
        {
            return raw
                .Where(z => !TypyDoUkrycia.Contains(z.TypZmiany))
                .GroupBy(z => (z.ZamowienieId, z.TypZmiany))
                .Select(g => new ZmianaCard(g))
                .OrderBy(c => c.KlientNazwa)
                .ThenByDescending(c => c.Source.DataZgloszenia)
                .ToList();
        }

        public int Id => Source.Id;
        public int ZamowienieId => Source.ZamowienieId;
        public string KlientNazwa => string.IsNullOrEmpty(Source.KlientNazwa) ? $"Zam. #{Source.ZamowienieId}" : Source.KlientNazwa!;
        public string ZgloszonePrzez => Source.ZgloszonePrzez;
        public string TimeAgo => Source.TimeAgo;

        /// <summary>Konkretna etykieta z jednostką — to widzi użytkownik obok klienta.</summary>
        public string TypLabel => Source.TypZmiany switch
        {
            "NoweZamowienie" => "Nowe zamówienie",
            "ZmianaIlosci" => "Liczba palet",
            "ZmianaPojemnikow" => "Pojemniki E2",
            "ZmianaKg" => "Waga [kg]",
            "ZmianaAwizacji" => "Awizacja (data/godz.)",
            "ZmianaTerminu" => "Termin",
            "Anulowanie" => "Anulowanie zamówienia",
            "ZmianaUwag" => "Uwagi",
            "ZmianaOdbiorcy" => "Odbiorca",
            "ZmianaDataProdukcji" => "Data produkcji",
            _ => Source.TypZmiany
        };

        public string Opis => Source.Opis ?? "";

        /// <summary>Najstarsza wartość w grupie — pełna delta od początku.</summary>
        public string Stare => string.IsNullOrEmpty(_stareOd) ? "—" : _stareOd;

        /// <summary>Najnowsza wartość w grupie.</summary>
        public string Nowa => string.IsNullOrEmpty(Source.NowaWartosc) ? "—" : Source.NowaWartosc;

        // Ikony dobrane tak, żeby na pierwszy rzut oka rozróżniać typy:
        //   🆕 nowe  |  🟫 palety (brązowy = drewno)  |  📦 pojemniki E2
        //   ⚖ waga   |  ⏰ awizacja (godzina)         |  📅 termin (data)
        //   🚫 anulowanie (mocniej niż ❌)             |  🏠 odbiorca
        //   🏭 data produkcji (fabryka, nie 🔧ślusarstwo)
        public string TypEmoji => Source.TypZmiany switch
        {
            "NoweZamowienie" => "🆕",
            "ZmianaIlosci" => "🟫",
            "ZmianaPojemnikow" => "📦",
            "ZmianaKg" => "⚖",
            "ZmianaAwizacji" => "⏰",
            "ZmianaTerminu" => "📅",
            "Anulowanie" => "🚫",
            "ZmianaOdbiorcy" => "🏠",
            "ZmianaDataProdukcji" => "🏭",
            _ => "🔔"
        };

        public Brush AkcentBrush
        {
            get { var c = Source.TypColor; return new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B)); }
        }
        public Brush AkcentSoft
        {
            get { var c = Source.TypColor; return new SolidColorBrush(Color.FromArgb(0x20, c.R, c.G, c.B)); }
        }

        // Delta liczbowa — tylko gdy obie końcówki są liczbami (oldest → newest)
        public string DeltaText
        {
            get
            {
                if (decimal.TryParse(_stareOd, NumberStyles.Any, CultureInfo.InvariantCulture, out var st)
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
                if (decimal.TryParse(_stareOd, NumberStyles.Any, CultureInfo.InvariantCulture, out var st)
                    && decimal.TryParse(Source.NowaWartosc, NumberStyles.Any, CultureInfo.InvariantCulture, out var nw))
                {
                    var d = nw - st;
                    if (d > 0) return new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
                    if (d < 0) return new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));
                }
                return new SolidColorBrush(Color.FromRgb(0x60, 0x6C, 0x76));
            }
        }

        // Badge "×N" — pokazywany tylko gdy scalonych > 1
        public string ScaloneText => IloscScalonych > 1 ? $"×{IloscScalonych}" : "";
        public Visibility ScaloneVis => IloscScalonych > 1 ? Visibility.Visible : Visibility.Collapsed;
        public string? ScaloneTooltip => IloscScalonych > 1
            ? $"{IloscScalonych} kolejnych edycji tego pola — pokazujemy pełną deltę od najstarszej ({_najstarszaData:dd.MM HH:mm}) do najnowszej ({Source.DataZgloszenia:dd.MM HH:mm})."
            : null;

        public string ZglosilDisplay => string.IsNullOrEmpty(ZgloszonePrzez) ? TimeAgo : $"👤 {ZgloszonePrzez} · {TimeAgo}";
    }
}
