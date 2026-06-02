// ════════════════════════════════════════════════════════════════════════════
// Transport/WPF/Windows/HistoriaZmianKursuWindow — okno historii zmian dla kursu.
// Pokazuje WSZYSTKIE zmiany (Oczekuje + Zaakceptowano + Odrzucono) dla zamówień
// w kursie. Reuse TransportZmianyService.GetByZamowienieIdsAsync (zwraca po
// wszystkich statusach). Grupowanie po kliencie (KlientNazwa), filtr statusu
// radiobuttonami, kolor lewego paska wg statusu.
// ════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Kalendarz1.Transport;
using Kalendarz1.Transport.WPF.Models;
using Kalendarz1.Transport.WPF.Services;

namespace Kalendarz1.Transport.WPF.Windows
{
    public partial class HistoriaZmianKursuWindow : Window
    {
        private readonly TransportWpfService _svc;
        private readonly long _kursId;
        private readonly string _trasa;
        private List<HistoriaZmianyCard> _wszystkie = new();
        private readonly ObservableCollection<HistoriaZmianyCard> _widok = new();
        private readonly ObservableCollection<KursAuditEntry> _audit = new();

        public HistoriaZmianKursuWindow(TransportWpfService svc, long kursId, string trasa)
        {
            InitializeComponent();
            _svc = svc;
            _kursId = kursId;
            _trasa = trasa ?? "";
            TxtKursMeta.Text = $"Kurs #{_kursId}" + (string.IsNullOrEmpty(_trasa) ? "" : $"  ·  {_trasa}");
            ListaHistorii.ItemsSource = _widok;
            ListaAudit.ItemsSource = _audit;
            var view = CollectionViewSource.GetDefaultView(_widok);
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(HistoriaZmianyCard.KlientNazwa)));
            Loaded += async (_, _) => await LoadAsync();
        }

        private void WidokZam_Click(object sender, RoutedEventArgs e)
        {
            TglWidokZam.IsChecked = true; TglWidokKurs.IsChecked = false;
            ScrollZam.Visibility = Visibility.Visible;
            ScrollKurs.Visibility = Visibility.Collapsed;
            PanelFiltrowStatus.Visibility = Visibility.Visible;
            AktualizujLicznik();
        }

        private void WidokKurs_Click(object sender, RoutedEventArgs e)
        {
            TglWidokZam.IsChecked = false; TglWidokKurs.IsChecked = true;
            ScrollZam.Visibility = Visibility.Collapsed;
            ScrollKurs.Visibility = Visibility.Visible;
            PanelFiltrowStatus.Visibility = Visibility.Collapsed;
            AktualizujLicznik();
        }

        private void AktualizujLicznik()
        {
            TxtLicznik.Text = TglWidokKurs.IsChecked == true
                ? $"·  {_audit.Count} {(_audit.Count == 1 ? "zmiana" : "zmian")} nagłówka kursu"
                : $"·  pokazano {_widok.Count} / {_wszystkie.Count} zmian zamówień";
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            TxtStatus.Text = "Ładowanie historii...";
            try
            {
                var ladunki = await _svc.Repo.PobierzLadunkiAsync(_kursId);
                var zamIds = ladunki
                    .Where(l => l.KodKlienta != null && l.KodKlienta.StartsWith("ZAM_"))
                    .Select(l => int.TryParse(l.KodKlienta!.Substring(4), out var id) ? id : 0)
                    .Where(i => i > 0)
                    .Distinct()
                    .ToList();
                if (zamIds.Count == 0)
                {
                    TxtStatus.Text = "Kurs nie ma ładunków z LibraNet (ZAM_*) — brak historii.";
                    return;
                }
                var wszystko = await TransportZmianyService.GetByZamowienieIdsAsync(zamIds);
                _wszystkie = wszystko
                    .Where(z => z.TypZmiany != "ZmianaStatusu" && z.TypZmiany != "ZmianaUwag")
                    .Select(z => new HistoriaZmianyCard(z))
                    .OrderBy(c => c.KlientNazwa)
                    .ThenByDescending(c => c.Source.DataZgloszenia)
                    .ToList();
                Przefiltruj();

                // Wczytaj też zmiany nagłówka kursu (KursAuditLog)
                var audit = await _svc.PobierzAuditAsync(_kursId);
                _audit.Clear();
                foreach (var a in audit) _audit.Add(a);

                AktualizujLicznik();
                TxtStatus.Text = $"Wczytano {_wszystkie.Count} zmian zamówień + {_audit.Count} zmian nagłówka kursu.";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Błąd: {ex.Message}";
            }
        }

        private void Filtr_Changed(object sender, RoutedEventArgs e) => Przefiltruj();

        private void Przefiltruj()
        {
            _widok.Clear();
            string? statusFiltr = RbOczekuje.IsChecked == true ? "Oczekuje"
                                : RbZaakcept.IsChecked == true ? "Zaakceptowano"
                                : RbOdrzuc.IsChecked == true ? "Odrzucono"
                                : null;
            IEnumerable<HistoriaZmianyCard> src = _wszystkie;
            if (statusFiltr != null) src = src.Where(c => c.Source.StatusZmiany == statusFiltr);
            foreach (var c in src) _widok.Add(c);
            AktualizujLicznik();
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e) => Close();
    }

    /// <summary>Karta historyczna (wszystkie statusy) — odróżnia kto, kiedy, status, komentarz.</summary>
    public class HistoriaZmianyCard
    {
        public TransportZmiana Source { get; }
        public HistoriaZmianyCard(TransportZmiana z) { Source = z; }

        public string KlientNazwa => string.IsNullOrEmpty(Source.KlientNazwa) ? $"Zam. #{Source.ZamowienieId}" : Source.KlientNazwa!;

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

        public string TypLabel => Source.TypZmiany switch
        {
            "NoweZamowienie" => "Nowe zamówienie",
            "ZmianaIlosci" => "Liczba palet",
            "ZmianaPojemnikow" => "Pojemniki E2",
            "ZmianaKg" => "Waga [kg]",
            "ZmianaAwizacji" => "Awizacja",
            "ZmianaTerminu" => "Termin",
            "Anulowanie" => "Anulowanie",
            "ZmianaOdbiorcy" => "Odbiorca",
            "ZmianaDataProdukcji" => "Data produkcji",
            _ => Source.TypZmiany
        };

        public string Stare => string.IsNullOrEmpty(Source.StareWartosc) ? "—" : Source.StareWartosc!;
        public string Nowa => string.IsNullOrEmpty(Source.NowaWartosc) ? "—" : Source.NowaWartosc!;

        public Brush AkcentBrush
        {
            get { var c = Source.TypColor; return new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B)); }
        }

        // Linia "kto zgłosił + kiedy"
        public string ZglosilLinia => $"👤 {Source.ZgloszonePrzez}  ·  {Source.DataZgloszenia:dd.MM HH:mm}";

        // Linia "kto zaakceptował/odrzucił + kiedy" (gdy decyzja podjęta)
        public string ZdecydowalLinia
        {
            get
            {
                if (string.IsNullOrEmpty(Source.ZaakceptowanePrzez) || !Source.DataAkceptacji.HasValue) return "";
                var ikona = Source.StatusZmiany == "Zaakceptowano" ? "✓" : Source.StatusZmiany == "Odrzucono" ? "✗" : "•";
                return $"{ikona} {Source.ZaakceptowanePrzez}  ·  {Source.DataAkceptacji.Value:dd.MM HH:mm}";
            }
        }

        public string KomentarzLinia => string.IsNullOrEmpty(Source.Komentarz) ? "" : $"„{Source.Komentarz}\"";

        public string StatusLabel => Source.StatusZmiany switch
        {
            "Oczekuje" => "OCZEKUJE",
            "Zaakceptowano" => "ZAAKCEPT.",
            "Odrzucono" => "ODRZUCONE",
            _ => Source.StatusZmiany.ToUpperInvariant()
        };

        public Brush StatusKolor => Source.StatusZmiany switch
        {
            "Oczekuje" => new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
            "Zaakceptowano" => new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)),
            "Odrzucono" => new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28)),
            _ => new SolidColorBrush(Color.FromRgb(0x90, 0xA4, 0xAE))
        };

        public Brush StatusTloMiekkie => Source.StatusZmiany switch
        {
            "Oczekuje" => new SolidColorBrush(Color.FromRgb(0xFF, 0xF7, 0xE0)),
            "Zaakceptowano" => new SolidColorBrush(Color.FromRgb(0xE7, 0xF4, 0xE8)),
            "Odrzucono" => new SolidColorBrush(Color.FromRgb(0xFD, 0xEC, 0xEC)),
            _ => new SolidColorBrush(Color.FromRgb(0xEC, 0xEF, 0xF1))
        };
    }
}
