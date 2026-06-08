// ════════════════════════════════════════════════════════════════════════════
// Transport/WPF/PlanowanieTransportuWpfWindow.xaml.cs
// ════════════════════════════════════════════════════════════════════════════
// Okno planowania — sandbox WPF. Wierne oryginałowi WinForms (kursy po lewej,
// WOLNE ZAMÓWIENIA po prawej), tylko lepsze. NIE dotyka WinForms ani WPF Huba.
// Reuse: TransportRepozytorium / TransportWpfService. Szybkie dodawanie zamówień
// do zaznaczonego kursu (ładunek + spójny status, bez otwierania edytora).
// ════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Kalendarz1.Transport;
using Kalendarz1.Transport.WPF.Models;
using Kalendarz1.Transport.WPF.Services;
using Kalendarz1.Transport.WPF.Views;

namespace Kalendarz1.Transport.WPF
{
    public partial class PlanowanieTransportuWpfWindow : Window
    {
        private readonly TransportWpfService _svc = new();
        private readonly Services.EtaNastepnyService _etaSvc = new();
        private readonly string _user = App.UserID ?? "system";
        private List<KursRow> _rows = new();      // widoczne (po filtrze)
        private List<KursRow> _rowsAll = new();   // wszystkie z dnia
        private Dictionary<int, string>? _telefonyKierowcow;   // cache TransportPL.Kierowca.Telefon (load raz na sesję window)
        private HashSet<int>? _klienciZGps;                    // cache LibraNet.KartotekaOdbiorcyDane (klienci z Lat/Lng)

        private List<WolneZamowienieWpf> _wolneAll = new();
        private readonly ObservableCollection<WolneZamowienieWpf> _wolne = new();
        private bool _ladowanie;

        private List<Kierowca> _kierowcy = new();
        private List<Pojazd> _pojazdy = new();
        private bool _slownikiZaladowane;

        private Point _dragStart;
        private const string FmtWolne = "ZPSP_wolne";
        private DataGridRow? _hoverRow;
        private System.Windows.Threading.DispatcherTimer? _autoTimer;
        private System.Windows.Threading.DispatcherTimer? _statusClearTimer;
        private DragGhostAdorner? _ghost;
        private System.Windows.Documents.AdornerLayer? _ghostLayer;
        private TimelineDniaView? _timeline;   // lazy-init przy 1. przełączeniu na Timeline
        private readonly ObservableCollection<ZmianaCard> _detalZmiany = new();

        public PlanowanieTransportuWpfWindow()
        {
            InitializeComponent();
            try { WindowIconHelper.SetIcon(this); } catch { }
            WolneGrid.ItemsSource = _wolne;
            DetalListaZmian.ItemsSource = _detalZmiany;
            // grupowanie kart po kliencie — nagłówek sekcji raz, klient nie powtarza się
            var widokDetal = System.Windows.Data.CollectionViewSource.GetDefaultView(_detalZmiany);
            widokDetal.GroupDescriptions.Add(new System.Windows.Data.PropertyGroupDescription(nameof(ZmianaCard.KlientNazwa)));
            WpfDragHelper.GrupujKolekcje(_wolne, nameof(WolneZamowienieWpf.DzienOdbioru),
                nameof(WolneZamowienieWpf.DzienOdbioru), nameof(WolneZamowienieWpf.DataPrzyjazdu));
            WolneGrid.SelectionChanged += (_, _) => UpdateDodajButton();
            WolneGrid.PreviewMouseLeftButtonDown += (_, e) => _dragStart = e.GetPosition(null);
            WolneGrid.PreviewMouseMove += WolneGrid_PreviewMouseMove;
            KursyGrid.DragOver += KursyGrid_DragOver;
            KursyGrid.Drop += KursyGrid_Drop;
            KursyGrid.DragLeave += (_, _) => ResetHover();
            DataKursu.SelectedDate = DateTime.Today;
            Loaded += async (_, _) => await LoadWszystkoAsync();
            // PreviewKeyDown (tunneling) — dostajemy Enter PRZED DataGridem.
            // Inaczej DataGrid przejmuje Enter i przewija na następny wiersz.
            PreviewKeyDown += Skroty_KeyDown;
            Closed += (_, _) => { _autoTimer?.Stop(); _statusClearTimer?.Stop(); };
        }

        private async Task EnsureSlownikiAsync()
        {
            if (_slownikiZaladowane) return;
            _kierowcy = await _svc.Repo.PobierzKierowcowAsync(true);
            _pojazdy = await _svc.Repo.PobierzPojazdyAsync(true);
            _slownikiZaladowane = true;
        }

        // ── nawigacja datą ──
        private void BtnPrev_Click(object s, RoutedEventArgs e) => DataKursu.SelectedDate = (DataKursu.SelectedDate ?? DateTime.Today).AddDays(-1);
        private void BtnNext_Click(object s, RoutedEventArgs e) => DataKursu.SelectedDate = (DataKursu.SelectedDate ?? DateTime.Today).AddDays(1);
        private void BtnDzis_Click(object s, RoutedEventArgs e) => DataKursu.SelectedDate = DateTime.Today;
        private async void DataKursu_Changed(object s, SelectionChangedEventArgs e) { if (!_ladowanie) await LoadWszystkoAsync(); }
        private async void BtnRefresh_Click(object s, RoutedEventArgs e)
        {
            _svc.InwalidujCacheZmian();   // F5 / klik Odśwież = zawsze pobierz świeże pendingi
            await LoadWszystkoAsync();
        }

        private async Task LoadWszystkoAsync()
        {
            await LoadKursyAsync();
            await OdswiezWolneAsync();
            if (_timeline != null && PanelTimeline.Visibility == Visibility.Visible)
            {
                _timeline.UstawDate(DataKursu.SelectedDate ?? DateTime.Today);
                await _timeline.RenderAsync();
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // PRZEŁĄCZNIK WIDOKU: Lista ↔ Timeline (Gantt)
        // ════════════════════════════════════════════════════════════════════
        private void WidokLista_Click(object s, RoutedEventArgs e)
        {
            TglWidokLista.IsChecked = true;
            TglWidokTimeline.IsChecked = false;
            PanelLista.Visibility = Visibility.Visible;
            PanelTimeline.Visibility = Visibility.Collapsed;
        }

        private void BtnGeokoduj_Click(object s, RoutedEventArgs e)
        {
            // Skrót: otwiera Mapa Klientów (Kartoteka.Features.Mapa.MapaKlientowWindow).
            // User klika tam "📍 Geokoduj adresy" - leci Nominatim + UPSERT do
            // KartotekaOdbiorcyDane. Po tym ETA dziala dla zgeokodowanych klientow.
            try
            {
                var win = new Kalendarz1.Kartoteka.Features.Mapa.MapaKlientowWindow { Owner = this };
                win.Show();
                StatusTymczasowy("📍 Otwarto Mapa Klientów — kliknij tam „📍 Geokoduj adresy\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie udało się otworzyć Mapy Klientów:\n{ex.Message}\n\nMożesz przejść ręcznie: Menu główne → Sprzedaż i CRM → 🗺️ Mapa.",
                    "Mapa Klientów", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// Raport transportowy — wszystkie kursy z wybranej daty + drukowanie.
        /// Otwiera istniejący WinForms TransportRaportForm z czasów starego planowania.
        /// Window-level dialog, niemodalny — można zostawić otwarte obok WPF.
        /// </summary>
        private void BtnRaport_Click(object s, RoutedEventArgs e)
        {
            try
            {
                const string conn = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";
                var raport = new Kalendarz1.Transport.TransportRaportForm(conn);
                raport.Show();   // WinForms .Show() — niemodalny, ale wymaga że WinForms message loop działa
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie udało się otworzyć raportu:\n{ex.Message}", "Raport", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void TglEta_Click(object s, RoutedEventArgs e)
        {
            // Toggle widoczność kolumny "🎯 ETA → następny" + dolicz live z Webfleet
            if (KolEta != null) KolEta.Visibility = TglEta.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            if (TglEta.IsChecked == true)
            {
                TglEta.Background = (Brush)FindResource("AccentSoft");
                TglEta.Foreground = (Brush)FindResource("AccentDark");
                // Live dolicz GPS-ETA (Webfleet) — nie blokujemy UI
                StatusTymczasowy("🎯 Pobieranie pozycji GPS pojazdów z Webfleet…");
                var lad = await _svc.Repo.PobierzLadunkiDlaKursowAsync(_rowsAll.Select(r => r.KursID));
                var zamIds = lad.SelectMany(kv => kv.Value)
                                .Where(l => l.KodKlienta != null && l.KodKlienta.StartsWith("ZAM_") && int.TryParse(l.KodKlienta.Substring(4), out _))
                                .Select(l => int.Parse(l.KodKlienta!.Substring(4))).Distinct();
                var info = zamIds.Any() ? await _svc.ResolveNazwyAsync(zamIds) : new Dictionary<int, ZamowienieNazwaInfo>();
                await WyliczEtaDlaWszystkichAsync(lad, info);
                StatusTymczasowy("✓ ETA wyliczone");
            }
            else
            {
                TglEta.Background = Brushes.Transparent;
                TglEta.Foreground = (Brush)FindResource("InkSecondary");
            }
        }

        private async void WidokTimeline_Click(object s, RoutedEventArgs e)
        {
            TglWidokLista.IsChecked = false;
            TglWidokTimeline.IsChecked = true;
            PanelLista.Visibility = Visibility.Collapsed;
            PanelTimeline.Visibility = Visibility.Visible;

            if (_timeline == null)   // lazy-init: nie wczytuj timeline'a przy starcie okna
            {
                _timeline = new TimelineDniaView { Svc = _svc, Uzytkownik = _user };
                _timeline.KursOtwarty += OtworzEdytorKursu;
                // po drop/utworzeniu kursu w timeline — odśwież listę kursów i pulę wolnych
                // (sam timeline renderuje się u siebie, więc tu tylko dane listy)
                _timeline.Zmieniono += async () => { await LoadKursyAsync(); await OdswiezWolneAsync(); };
                PanelTimeline.Content = _timeline;
            }
            _timeline.UstawDate(DataKursu.SelectedDate ?? DateTime.Today);
            await _timeline.RenderAsync();
        }

        // klik na pasek kursu w timeline → edytor (po OK pełne odświeżenie)
        private void OtworzEdytorKursu(long kursId)
        {
            try
            {
                var data = DataKursu.SelectedDate ?? DateTime.Today;
                var ed = new EdytorKursuWpfWindow(_svc, _user, data, kursId) { Owner = this };
                if (ed.ShowDialog() == true)
                {
                    _svc.InwalidujCacheZmian();   // po edycji pendingi mogły się zmienić
                    _ = LoadWszystkoAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd otwierania edytora:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // KURSY
        // ════════════════════════════════════════════════════════════════════
        // Brushes — używane dla per-segment Foreground. Statyczne (Frozen) — bezpieczne dla data-bindingu.
        private static readonly Brush _trasaSzary = (Brush)new SolidColorBrush(Color.FromRgb(0x1F, 0x27, 0x33)).GetAsFrozen();
        private static readonly Brush _trasaCzerwony = (Brush)new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28)).GetAsFrozen();
        private static readonly Brush _trasaSep = (Brush)new SolidColorBrush(Color.FromRgb(0x7B, 0x87, 0x94)).GetAsFrozen();

        /// <summary>
        /// Buduje listę segmentów trasy: max 5 stopów renderowanych w pełni, więcej → K1 → K2 → … → KN-1 → KN.
        /// Nazwy bez GPS dostają kolor czerwony (alert dla planisty).
        /// </summary>
        private static List<TrasaSegment> ZbudujSegmenty(List<string> nazwy, HashSet<string> bezGps)
        {
            var wynik = new List<TrasaSegment>();
            if (nazwy.Count == 0) return wynik;

            void Dodaj(string nazwa, bool ostatni)
            {
                wynik.Add(new TrasaSegment
                {
                    Nazwa = nazwa,
                    Kolor = bezGps.Contains(nazwa) ? _trasaCzerwony : _trasaSzary,
                    BezGps = bezGps.Contains(nazwa),
                    Separator = ostatni ? "" : "  →  "
                });
            }

            if (nazwy.Count <= 5)
            {
                for (int i = 0; i < nazwy.Count; i++)
                    Dodaj(nazwy[i], i == nazwy.Count - 1);
            }
            else
            {
                // K1 → K2 → … → KN-1 → KN
                Dodaj(nazwy[0], false);
                Dodaj(nazwy[1], false);
                wynik.Add(new TrasaSegment { Nazwa = "…", Kolor = _trasaSep, Separator = "  →  " });
                Dodaj(nazwy[^2], false);
                Dodaj(nazwy[^1], true);
                // Liczba stopów na końcu (jako dodatkowy „segment" w stylu pomocniczym)
                wynik.Add(new TrasaSegment { Nazwa = $" ({nazwy.Count} stopów)", Kolor = _trasaSep });
            }
            return wynik;
        }

        /// <summary>
        /// Cache klientów z geokodowanym adresem (LibraNet.KartotekaOdbiorcyDane.Latitude/Longitude IS NOT NULL).
        /// Używany do oznaczania w kolumnie Trasa tych kursów, gdzie któryś klient nie ma GPS — czerwona czcionka + ⚠.
        /// Cache 1 zapytanie na sesję okna (geokodowanie zmienia się rzadko).
        /// </summary>
        private async Task ZapewnijKlienciZGpsAsync()
        {
            if (_klienciZGps != null) return;
            var set = new HashSet<int>();
            try
            {
                await using var cn = new Microsoft.Data.SqlClient.SqlConnection(Services.TransportWpfService.ConnLibra);
                await cn.OpenAsync();
                await using var cmd = new Microsoft.Data.SqlClient.SqlCommand(
                    @"SELECT IdSymfonia FROM dbo.KartotekaOdbiorcyDane
                      WHERE Latitude IS NOT NULL AND Longitude IS NOT NULL", cn);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync()) set.Add(rd.GetInt32(0));
            }
            catch { /* silent — brak listy nie blokuje renderu, po prostu nie podświetlamy */ }
            _klienciZGps = set;
        }

        /// <summary>
        /// Cache telefonów kierowców z TransportPL.Kierowca. Ładujemy raz na sesję okna
        /// (telefony zmieniają się rzadko, kafelek odświeża się często — szkoda zapytania per refresh).
        /// </summary>
        private async Task ZapewnijTelefonyKierowcowAsync()
        {
            if (_telefonyKierowcow != null) return;
            var dict = new Dictionary<int, string>();
            try
            {
                await using var cn = new Microsoft.Data.SqlClient.SqlConnection(Services.TransportWpfService.ConnTransport);
                await cn.OpenAsync();
                await using var cmd = new Microsoft.Data.SqlClient.SqlCommand(
                    "SELECT KierowcaID, Telefon FROM dbo.Kierowca WHERE Telefon IS NOT NULL AND LTRIM(RTRIM(Telefon)) <> ''", cn);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    int id = rd.GetInt32(0);
                    string tel = rd.GetString(1).Trim();
                    if (!string.IsNullOrEmpty(tel)) dict[id] = tel;
                }
            }
            catch { /* silent — telefony są niekrytyczne, brak nie blokuje listy kursów */ }
            _telefonyKierowcow = dict;
        }

        private async Task LoadKursyAsync()
        {
            if (_ladowanie) return;   // re-entrancy guard — chroni przed podwójnym wywołaniem
            _ladowanie = true;
            try
            {
                var data = DataKursu.SelectedDate ?? DateTime.Today;
                StatusText.Text = $"Ładowanie {data:dd.MM.yyyy}...";
                DayNameText.Text = data.ToString("dddd", new CultureInfo("pl-PL"));

                var kursy = await _svc.Repo.PobierzKursyPoDacieAsync(data);
                var ladunki = await _svc.Repo.PobierzLadunkiDlaKursowAsync(kursy.Select(k => k.KursID));

                _rowsAll = kursy.Select(k => new KursRow(k,
                    ladunki.TryGetValue(k.KursID, out var l) ? l.Count : 0)).ToList();

                // Wpisz telefony kierowców pod nazwiskami (cache na poziomie sesji okna)
                await ZapewnijTelefonyKierowcowAsync();
                foreach (var row in _rowsAll)
                {
                    if (row.Source.KierowcaID.HasValue
                        && _telefonyKierowcow != null
                        && _telefonyKierowcow.TryGetValue(row.Source.KierowcaID.Value, out var tel)
                        && !string.IsNullOrWhiteSpace(tel))
                    {
                        row.KierowcaTelefon = $"📞 {tel}";
                    }
                }

                // Cache klientów z GPS — do oznaczania w kolumnie Trasa (czerwony + ⚠ dla bez GPS)
                await ZapewnijKlienciZGpsAsync();

                await UzupelnijAgregatyAsync(ladunki);
                FiltrujKursy();
                StatusText.Text = _rowsAll.Count == 0
                    ? $"Brak kursów na {data:dd.MM.yyyy}"
                    : $"Załadowano {_rowsAll.Count} kursów na {data:dd.MM.yyyy}";
                UpdateButtons();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Błąd: {ex.Message}";
                MessageBox.Show($"Błąd ładowania kursów:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { _ladowanie = false; }
        }

        // filtr listy kursów: szukaj (trasa/kierowca/pojazd) + „tylko wymagające uwagi"
        private void FiltrujKursy()
        {
            var q = TxtFiltrKursy.Text?.Trim().ToLowerInvariant() ?? "";

            IEnumerable<KursRow> src = _rowsAll;
            if (!string.IsNullOrEmpty(q))
                src = src.Where(r => ((r.TrasaAuto ?? "") + " " + (r.KierowcaNazwa ?? "") + " " + (r.PojazdRejestracja ?? ""))
                    .ToLowerInvariant().Contains(q));

            _rows = src.ToList();
            KursyGrid.ItemsSource = _rows;
            KursyEmpty.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void FiltrKursyTekst_Changed(object s, TextChangedEventArgs e) { if (!_ladowanie) FiltrujKursy(); }

        private async void Skroty_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5) { await LoadWszystkoAsync(); e.Handled = true; return; }
            if (Keyboard.FocusedElement is TextBox) return;   // nie przejmuj skrótów podczas pisania
            if (e.Key == Key.Insert) { OtworzEdytor(true); e.Handled = true; }
            else if (e.Key == Key.Delete && KursyGrid.SelectedItem is KursRow) { BtnUsun_Click(this, new RoutedEventArgs()); e.Handled = true; }
            else if (e.Key == Key.Enter)
            {
                // Enter SŁUŻY WYŁĄCZNIE do akceptacji zmian (preferencja użytkownika).
                // Zawsze e.Handled = true → blokuje domyślne przewijanie wiersza DataGrid w dół.
                if (PanelZmianyDlaKursu.Visibility == Visibility.Visible && _detalZmiany.Count > 0)
                    BtnDetalAkceptujWszystkie_Click(this, new RoutedEventArgs());
                e.Handled = true;
                // Edytor otwierany dwuklikiem na wierszu lub przyciskiem "Edytuj" — NIE Enterem.
            }
            else if (e.Key == Key.Escape && PanelZmianyDlaKursu.Visibility == Visibility.Visible)
            {
                // Esc — zwija panel zmian (odznacza wiersz kursu)
                KursyGrid.SelectedItem = null;
                e.Handled = true;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // WOLNE ZAMÓWIENIA
        // ════════════════════════════════════════════════════════════════════
        private async Task OdswiezWolneAsync()
        {
            try
            {
                var data = DataKursu.SelectedDate ?? DateTime.Today;
                // Wolne zawsze wg daty UBOJU (preferencja użytkownika, brak przełącznika UI).
                _wolneAll = await _svc.LoadWolneZamowieniaAsync(data, poUboju: true);
                FiltrujWolne();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Błąd wolnych zamówień: {ex.Message}";
            }
        }

        private void FiltrujWolne()
        {
            _wolne.Clear();
            var lista = _wolneAll.OrderBy(z => z.DataPrzyjazdu).ToList();
            foreach (var z in lista) _wolne.Add(z);

            WolneCountText.Text = _wolne.Count.ToString();
            WolneEmpty.Visibility = _wolne.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateDodajButton();
        }

        private async void BtnOdswiezWolne_Click(object s, RoutedEventArgs e) => await OdswiezWolneAsync();

        // ════════════════════════════════════════════════════════════════════
        // SELEKCJA kursu + podgląd ładunków
        // ════════════════════════════════════════════════════════════════════
        private async void KursyGrid_SelectionChanged(object s, SelectionChangedEventArgs e)
        {
            UpdateButtons();
            UpdateDodajButton();
            await OdswiezDetalZmianAsync();
        }

        // Pamięć stałych pędzli (nie tworzymy nowych SolidColorBrush za każdym razem)
        private static readonly Brush EtaSzary = new SolidColorBrush(Color.FromRgb(0xBD, 0xBD, 0xBD));
        private static readonly Brush EtaZielony = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
        private static readonly Brush EtaAmber = new SolidColorBrush(Color.FromRgb(0xB2, 0x6A, 0x00));
        private static readonly Brush EtaCzerwony = new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));
        private static readonly Brush EtaNiebieski = new SolidColorBrush(Color.FromRgb(0x15, 0x65, 0xC0));

        /// <summary>
        /// Wypełnia ETA dla zaznaczonego kursu z aktualnej pozycji GPS pojazdu (Webfleet) do
        /// pierwszego nieodwiedzonego klienta w kolejności ładunków. Wynik: minuty dojazdu + dystans.
        /// BEZ powrotu do bazy — tylko między obecną pozycją a następnym celem.
        /// Wymaga: pojazd zmapowany w WebfleetVehicleMapping + GPS dostępny + klient ze współrzędnymi.
        /// </summary>
        private async Task WypelnijEtaNastepnyGpsAsync(KursRow row, Dictionary<long, List<Ladunek>> ladunki, Dictionary<int, ZamowienieNazwaInfo> info, bool zastapiony = false)
        {
            row.EtaCzas = "";
            row.EtaKlient = "—";
            row.EtaKolor = EtaSzary;
            row.EtaTooltip = null;

            // Drugi kurs tym samym pojazdem już wystartował — ten kurs jest skończony
            if (zastapiony)
            {
                row.EtaCzas = "✓ koniec kursu";
                row.EtaKlient = "następny kurs tym pojazdem już wystartował";
                row.EtaKolor = EtaSzary;
                row.EtaTooltip = "Pojazd przeszedł do kolejnej trasy dzisiaj — pierwszy kurs zakończony.";
                return;
            }

            ladunki.TryGetValue(row.KursID, out var lad);
            var wynik = await _etaSvc.WyliczDlaKursuAsync(
                row.Source.DataKursu,
                row.Source.PojazdID,
                row.Source.GodzWyjazdu,
                lad ?? new List<Ladunek>(),
                info);

            row.EtaTooltip = BudujTooltipEta(wynik);
            UstawWierszEtaWgStatusu(row, wynik);
        }

        /// <summary>Składa tooltip o pełnym kontekście ETA — pozycja pojazdu, awizacja, statystyki.</summary>
        private static string? BudujTooltipEta(EtaNastepnyService.WynikEta w)
        {
            // Brak GPS pojazdu w ogóle → krótki tooltip wyłącznie z powodem
            if (string.IsNullOrEmpty(w.SkadAdres))
                return string.IsNullOrEmpty(w.Powod) ? null : w.Powod;

            var t = new System.Text.StringBuilder();
            string predkoscTxt = w.Stoi ? "stoi" : $"{w.Predkosc} km/h";
            string lokalizacja = w.WBazie ? $"🏠 W bazie (Koziołki) — {w.SkadAdres}" : $"📍 {w.SkadAdres}";
            t.Append($"{lokalizacja} · {predkoscTxt}");

            if (w.AwizacjaCelu.HasValue && !string.IsNullOrEmpty(w.KlientNazwa) && w.KlientNazwa != "—")
                t.Append($"\n🕐 Awizacja {w.KlientNazwa}: {w.AwizacjaCelu:HH:mm}");
            if (w.DoStartu.HasValue)
                t.Append($"\n⏰ Start kursu za: {FormatTimeSpanShort(w.DoStartu.Value)}");
            if (w.ObsluzonychPoTerminie > 0 && w.LiczbaPrzystankow > 0)
                t.Append($"\n✓ Po terminie: {w.ObsluzonychPoTerminie}/{w.LiczbaPrzystankow} klient(ów)");
            return t.ToString();
        }

        /// <summary>Wypełnia EtaCzas/EtaKlient/EtaKolor na podstawie statusu z silnika ETA.</summary>
        private void UstawWierszEtaWgStatusu(KursRow row, EtaNastepnyService.WynikEta w)
        {
            switch (w.Status)
            {
                case EtaNastepnyService.EtaStatus.HistoriaKursu:
                    row.EtaCzas = "—";
                    row.EtaKlient = w.Powod;   // „kurs zakończony" / „kurs zaplanowany"
                    row.EtaKolor = EtaSzary;
                    return;

                case EtaNastepnyService.EtaStatus.BrakDanych:
                    // Mamy pozycję pojazdu? Pokaż tylko ją w linii 2 (cenne dla planisty)
                    row.EtaCzas = w.Powod;
                    row.EtaKlient = !string.IsNullOrEmpty(w.SkadMiasto)
                        ? (w.Stoi ? $"🛰 stoi · {w.SkadMiasto}" : $"🚛 jedzie · {w.SkadMiasto} ({w.Predkosc} km/h)")
                        : "—";
                    row.EtaKolor = EtaSzary;
                    return;

                case EtaNastepnyService.EtaStatus.WBazie:
                    row.EtaCzas = "🏠 W bazie";
                    row.EtaKlient = w.Stoi ? "✓ koniec trasy" : $"manewruje ({w.Predkosc} km/h)";
                    row.EtaKolor = EtaNiebieski;
                    return;

                case EtaNastepnyService.EtaStatus.DoBazy:
                {
                    row.EtaCzas = $"🏠 powrót do bazy · {FormatCzasDystans(w.Czas, w.DystansKm)}";
                    row.EtaKlient = w.Stoi
                        ? $"🛰 stoi · {w.SkadMiasto}"
                        : $"🚛 jedzie · {w.SkadMiasto} ({w.Predkosc} km/h)";
                    row.EtaKolor = EtaNiebieski;
                    return;
                }

                case EtaNastepnyService.EtaStatus.UKlienta:
                    row.EtaCzas = "📍 na miejscu";
                    row.EtaKlient = w.Stoi
                        ? $"rozładunek · {w.KlientNazwa}"
                        : $"manewruje · {w.KlientNazwa} ({w.Predkosc} km/h)";
                    row.EtaKolor = EtaZielony;
                    return;

                case EtaNastepnyService.EtaStatus.PrzedWyjazdem:
                {
                    int doStartu = (int)Math.Ceiling(w.DoStartu!.Value.TotalMinutes);
                    row.EtaCzas = doStartu < 60
                        ? $"⏰ start za {doStartu} min"
                        : $"⏰ start za {w.DoStartu.Value.Hours}h {w.DoStartu.Value.Minutes:00}min";

                    // Pokażmy też dokąd jedzie i jaki dystans (bezpieczeństwo planisty)
                    var linia2 = new System.Text.StringBuilder();
                    linia2.Append(w.WBazie ? "🏠 w bazie" : (w.Stoi ? $"🛰 stoi · {w.SkadMiasto}" : $"🚛 jedzie · {w.SkadMiasto}"));
                    linia2.Append($" → {w.KlientNazwa}");
                    if (w.AwizacjaCelu.HasValue) linia2.Append($" · awiz. {w.AwizacjaCelu:HH:mm}");
                    row.EtaKlient = linia2.ToString();
                    row.EtaKolor = EtaNiebieski;
                    return;
                }

                case EtaNastepnyService.EtaStatus.DoKlienta:
                {
                    row.EtaCzas = $"za {FormatCzasDystans(w.Czas, w.DystansKm)}";

                    string statusPojazdu = w.WBazie
                        ? (w.Stoi ? "🏠 w bazie" : "🚛 wyjeżdża z bazy")
                        : (w.Stoi ? $"🛰 stoi · {w.SkadMiasto}" : $"🚛 jedzie · {w.SkadMiasto}");

                    var linia2 = new System.Text.StringBuilder();
                    linia2.Append(statusPojazdu).Append(" → ").Append(w.KlientNazwa);
                    if (w.AwizacjaCelu.HasValue)
                        linia2.Append($" · awiz. {w.AwizacjaCelu:HH:mm}");

                    if (w.Spoznienie.HasValue)
                    {
                        int spMin = (int)Math.Ceiling(w.Spoznienie.Value.TotalMinutes);
                        linia2.Append($" · spóźniony {spMin} min");
                        row.EtaKolor = EtaCzerwony;
                    }
                    else if (w.Zapas.HasValue)
                    {
                        int zapMin = (int)Math.Floor(w.Zapas.Value.TotalMinutes);
                        linia2.Append($" · zapas {zapMin} min");
                        row.EtaKolor = zapMin < 30 ? EtaAmber : EtaZielony;
                    }
                    else
                    {
                        // Klient bez awizacji — neutralnie wg odległości
                        int min = (int)Math.Ceiling(w.Czas.TotalMinutes);
                        row.EtaKolor = min < 30 ? EtaAmber : EtaZielony;
                    }
                    row.EtaKlient = linia2.ToString();
                    return;
                }
            }
        }

        /// <summary>„za 25 min · 12.4 km" / „1h 30min · 78 km" (bez prefiksu "za ").</summary>
        private static string FormatCzasDystans(TimeSpan czas, double km)
        {
            int min = (int)Math.Ceiling(czas.TotalMinutes);
            return min < 60
                ? $"{min} min  ·  {km:F1} km"
                : $"{czas.Hours}h {czas.Minutes:00}min  ·  {km:F1} km";
        }

        /// <summary>„45 min" / „2h 15min" — do tooltipa.</summary>
        private static string FormatTimeSpanShort(TimeSpan ts)
        {
            int totalMin = (int)Math.Ceiling(ts.TotalMinutes);
            return totalMin < 60 ? $"{totalMin} min" : $"{ts.Hours}h {ts.Minutes:00}min";
        }

        /// <summary>Wpisuje tymczasowy komunikat w status bar i czyści go po 5 s.
        /// Dla komunikatów akcji typu „✓ Zaakceptowano N zmian" — nie blokuje miejsca dla nowych info.</summary>
        private void StatusTymczasowy(string text)
        {
            StatusText.Text = text;
            _statusClearTimer?.Stop();
            _statusClearTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _statusClearTimer.Tick += (_, _) =>
            {
                if (StatusText.Text == text) StatusText.Text = "";   // czyść tylko jeśli nikt nie nadpisał
                _statusClearTimer?.Stop();
            };
            _statusClearTimer.Start();
        }

        // ════════════════════════════════════════════════════════════════════
        // PANEL DETALI ZMIAN dla zaznaczonego kursu (auto-show)
        // ════════════════════════════════════════════════════════════════════
        private async Task OdswiezDetalZmianAsync()
        {
            _detalZmiany.Clear();
            if (KursyGrid?.SelectedItem is not KursRow row || !row.MaZmiany)
            {
                PanelZmianyDlaKursu.Visibility = Visibility.Collapsed;
                return;
            }
            var requestedKursId = row.KursID;   // snapshot — chronimy się przed race przy szybkim klikaniu
            try
            {
                var raw = await _svc.PobierzZmianyDlaKursuAsync(requestedKursId);

                // Po awaicie sprawdź czy user dalej jest na tym samym wierszu — inaczej zignoruj wynik
                if (KursyGrid?.SelectedItem is not KursRow current || current.KursID != requestedKursId)
                    return;

                _detalZmiany.Clear();   // wyczyść jeszcze raz na wypadek gdyby ktoś dorzucił między await
                foreach (var c in ZmianaCard.ScalListe(raw)) _detalZmiany.Add(c);

                // Bezpiecznik desync — badge ≠ faktyczna liczba kart → panel jest źródłem prawdy
                if (current.LiczbaZmianOczekujacych != _detalZmiany.Count)
                {
                    current.LiczbaZmianOczekujacych = _detalZmiany.Count;
                    KursyGrid.Items.Refresh();
                }

                if (_detalZmiany.Count == 0)
                {
                    PanelZmianyDlaKursu.Visibility = Visibility.Collapsed;
                    return;
                }
                DetalNaglowek.Text = $"{_detalZmiany.Count} {(_detalZmiany.Count == 1 ? "zmiana" : "zmian")} do akceptacji  ·  kurs #{current.KursID}";
                BtnDetalAkceptujText.Text = $"Akceptuj wszystkie ({_detalZmiany.Count}) — Enter";
                PanelZmianyDlaKursu.Visibility = Visibility.Visible;
            }
            catch { PanelZmianyDlaKursu.Visibility = Visibility.Collapsed; }
        }

        private async void BtnDetalAkceptujJedno_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not ZmianaCard card) return;
            if (KursyGrid?.SelectedItem is not KursRow row) return;
            b.IsEnabled = false;
            try
            {
                await _svc.AkceptujGrupeIPrzeliczAsync(card.Ids, row.KursID, card.ZamowienieId, card.Source.TypZmiany, _user);
                _detalZmiany.Remove(card);
                row.LiczbaZmianOczekujacych = Math.Max(0, row.LiczbaZmianOczekujacych - card.IloscScalonych);
                KursyGrid.Items.Refresh();
                AktualizujDetalNaglowek(row);
                StatusTymczasowy(card.IloscScalonych > 1
                    ? $"✓ Zaakceptowano {card.IloscScalonych} kolejnych edycji: {card.TypLabel} ({card.KlientNazwa})"
                    : $"✓ Zaakceptowano: {card.TypLabel} ({card.KlientNazwa})");
            }
            catch (Exception ex) { MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error); b.IsEnabled = true; }
        }

        private async void BtnDetalOdrzucJedno_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not ZmianaCard card) return;
            if (KursyGrid?.SelectedItem is not KursRow row) return;
            var iloscTxt = card.IloscScalonych > 1 ? $" ({card.IloscScalonych} kolejnych edycji)" : "";
            var kontekst = $"{card.TypLabel} · {card.KlientNazwa}{iloscTxt}   {card.Stare} → {card.Nowa}";
            var dlg = new Dialogs.OdrzucPowodDialog(kontekst) { Owner = this };
            if (dlg.ShowDialog() != true) return;
            b.IsEnabled = false;
            try
            {
                await _svc.OdrzucGrupeAsync(card.Ids, _user, dlg.Powod);
                _detalZmiany.Remove(card);
                row.LiczbaZmianOczekujacych = Math.Max(0, row.LiczbaZmianOczekujacych - card.IloscScalonych);
                KursyGrid.Items.Refresh();
                AktualizujDetalNaglowek(row);
                StatusTymczasowy($"✗ Odrzucono: {card.KlientNazwa} · {card.TypLabel}");
            }
            catch (Exception ex) { MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error); b.IsEnabled = true; }
        }

        private async void BtnDetalAkceptujWszystkie_Click(object sender, RoutedEventArgs e)
        {
            if (KursyGrid?.SelectedItem is not KursRow row || _detalZmiany.Count == 0) return;

            int liczbaKart = _detalZmiany.Count;
            int liczbaIds = _detalZmiany.Sum(c => c.IloscScalonych);
            BtnDetalAkceptujWszystkie.IsEnabled = false;
            try
            {
                await _svc.AkceptujWszystkieDlaKursuAsync(row.KursID, _user);
                _detalZmiany.Clear();
                row.LiczbaZmianOczekujacych = 0;
                long keep = row.KursID;
                await LoadKursyAsync();      // przelicz wypełnienie/ładunki po sync PojemnikiE2
                var again = _rows.FirstOrDefault(r => r.KursID == keep);
                if (again != null) { KursyGrid.SelectedItem = again; KursyGrid.ScrollIntoView(again); }
                StatusTymczasowy($"✓ Zaakceptowano {liczbaKart} zmian ({liczbaIds} wpisów) dla kursu #{keep}.");
            }
            catch (Exception ex) { StatusText.Text = $"Błąd akceptacji: {ex.Message}"; }
            finally { BtnDetalAkceptujWszystkie.IsEnabled = true; }
        }

        private void BtnDetalEdytor_Click(object sender, RoutedEventArgs e) => OtworzEdytor(false);

        private void BtnDetalHistoria_Click(object sender, RoutedEventArgs e)
        {
            if (KursyGrid?.SelectedItem is not KursRow row) return;
            try
            {
                var win = new Windows.HistoriaZmianKursuWindow(_svc, row.KursID, row.Trasa ?? "") { Owner = this };
                win.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AktualizujDetalNaglowek(KursRow row)
        {
            if (_detalZmiany.Count == 0)
            {
                PanelZmianyDlaKursu.Visibility = Visibility.Collapsed;
                return;
            }
            DetalNaglowek.Text = $"{_detalZmiany.Count} {(_detalZmiany.Count == 1 ? "zmiana" : "zmian")} do akceptacji  ·  kurs #{row.KursID}";
            BtnDetalAkceptujText.Text = $"Akceptuj wszystkie ({_detalZmiany.Count}) — Enter";
        }

        private void UpdateButtons()
        {
            bool sel = KursyGrid?.SelectedItem != null;
            if (BtnEdytuj != null) BtnEdytuj.IsEnabled = sel;
            if (BtnUsun != null) BtnUsun.IsEnabled = sel;
        }

        private void UpdateDodajButton()
        {
            bool kursSel = KursyGrid?.SelectedItem is KursRow;
            bool wolneSel = WolneGrid?.SelectedItems != null && WolneGrid.SelectedItems.Count > 0;
            if (BtnDodajDoKursu != null) BtnDodajDoKursu.IsEnabled = kursSel && wolneSel;
            if (DodajHint != null)
            {
                DodajHint.Text = !kursSel
                    ? "① Zaznacz kurs po lewej  →  ② zaznacz zamówienia  →  ③ Dodaj"
                    : (KursyGrid.SelectedItem is KursRow r
                        ? $"Dodasz do kursu #{r.KursID} ({r.Trasa ?? "—"})"
                        : "");
            }
        }

        // Dociąga per kurs: KG (suma zamówień), nazwiska handlowców, nazwę twórcy (do avatara).
        private async Task UzupelnijAgregatyAsync(Dictionary<long, List<Ladunek>> ladunki)
        {
            try
            {
                var allZam = new List<int>();
                foreach (var kv in ladunki)
                    foreach (var l in kv.Value)
                        if (l.KodKlienta != null && l.KodKlienta.StartsWith("ZAM_") && int.TryParse(l.KodKlienta.Substring(4), out var id))
                            allZam.Add(id);

                var info = allZam.Count > 0
                    ? await _svc.ResolveNazwyAsync(allZam.Distinct())
                    : new Dictionary<int, ZamowienieNazwaInfo>();
                var allUserIds = _rowsAll.Select(r => r.UtworzylId)
                    .Concat(_rowsAll.Where(r => !string.IsNullOrEmpty(r.ZmienilId)).Select(r => r.ZmienilId))
                    .Where(s => !string.IsNullOrEmpty(s));
                var userNames = await _svc.PobierzNazwyUzytkownikowAsync(allUserIds);
                await _svc.EnsureHandlowiecMapAsync();

                // pending zmiany — mapa ZamId→typy (filtr ZmianaStatusu, cache 30 s)
                var pendingMap = await _svc.PobierzOczekujaceMapaAsync();

                foreach (var row in _rowsAll)
                {
                    decimal kg = 0;
                    var handl = new List<string>();
                    var seenZamIdy = new HashSet<int>();   // dedupowanie: ten sam zam może być w 2 ładunkach
                    int pending = 0;
                    if (ladunki.TryGetValue(row.KursID, out var lad))
                    {
                        foreach (var l in lad)
                        {
                            if (l.KodKlienta != null && l.KodKlienta.StartsWith("ZAM_")
                                && int.TryParse(l.KodKlienta.Substring(4), out var id))
                            {
                                if (info.TryGetValue(id, out var zi))
                                {
                                    kg += zi.IloscKg;
                                    if (!string.IsNullOrWhiteSpace(zi.Handlowiec) && !handl.Contains(zi.Handlowiec))
                                        handl.Add(zi.Handlowiec);
                                }
                                // licz typy tylko RAZ na unikalne zamówienie — żeby badge zgadzał się z liczbą kart
                                if (seenZamIdy.Add(id) && pendingMap.TryGetValue(id, out var typy))
                                    pending += typy.Count;
                            }
                        }
                    }
                    row.Kg = kg;
                    row.LiczbaZmianOczekujacych = pending;

                    // Auto-trasa: pierwszy → ostatni klient z ładunków (po Kolejność), unikalni.
                    // Logistyk nie musi ręcznie wpisywać — program sam zczytuje kolejność.
                    if (ladunki.TryGetValue(row.KursID, out var ladList))
                    {
                        // Para (Nazwa, BezGps) — żeby wiedzieć których brakuje GPS
                        var przystanki = ladList
                            .OrderBy(x => x.Kolejnosc)
                            .Where(x => x.KodKlienta != null && x.KodKlienta.StartsWith("ZAM_")
                                        && int.TryParse(x.KodKlienta.Substring(4), out _))
                            .Select(x =>
                            {
                                var zid = int.Parse(x.KodKlienta!.Substring(4));
                                if (!info.TryGetValue(zid, out var zi) || string.IsNullOrEmpty(zi.Nazwa))
                                    return (Nazwa: (string?)null, KlientId: 0);
                                return (Nazwa: (string?)zi.Nazwa, KlientId: zi.KlientId);
                            })
                            .Where(t => !string.IsNullOrEmpty(t.Nazwa))
                            .GroupBy(t => t.Nazwa)   // ten sam klient z kilku ładunków = 1 stop
                            .Select(g => (Nazwa: g.Key!, KlientId: g.First().KlientId))
                            .ToList();

                        var nazwyKlientow = przystanki.Select(p => p.Nazwa).ToList();
                        var bezGps = przystanki
                            .Where(p => p.KlientId > 0 && _klienciZGps != null && !_klienciZGps.Contains(p.KlientId))
                            .Select(p => p.Nazwa)
                            .ToHashSet();

                        // Fallback string (sortowanie/eksport)
                        row.TrasaAuto = nazwyKlientow.Count switch
                        {
                            0 => string.IsNullOrWhiteSpace(row.Trasa) ? "—" : row.Trasa!,
                            <= 5 => string.Join(" → ", nazwyKlientow),
                            _ => $"{nazwyKlientow[0]} → {nazwyKlientow[1]} → … → {nazwyKlientow[^2]} → {nazwyKlientow[^1]} ({nazwyKlientow.Count} stopów)"
                        };

                        // Segmenty per-klient (UI: ItemsControl z osobnymi TextBlocki — czerwony tylko nazwa bez GPS)
                        row.TrasaSegmenty = ZbudujSegmenty(przystanki.Select(p => p.Nazwa).ToList(), bezGps);

                        // Tooltip — pełna lista + sekcja „bez GPS" gdy są
                        var tooltipSb = new System.Text.StringBuilder();
                        if (nazwyKlientow.Count > 5)
                            tooltipSb.AppendLine($"Trasa ({nazwyKlientow.Count} stopów):").Append(string.Join(" → ", nazwyKlientow));
                        if (bezGps.Count > 0)
                        {
                            if (tooltipSb.Length > 0) tooltipSb.AppendLine().AppendLine();
                            tooltipSb.Append($"⚠ Bez GPS: {string.Join(", ", bezGps)}");
                            tooltipSb.AppendLine().Append("Geokoduj w: Kartoteka → Mapa klientów");
                        }
                        row.TrasaAutoTooltip = tooltipSb.Length > 0 ? tooltipSb.ToString() : null;
                    }
                    else
                    {
                        row.TrasaAuto = string.IsNullOrWhiteSpace(row.Trasa) ? "—" : row.Trasa!;
                        row.TrasaSegmenty = new List<TrasaSegment>();
                    }
                    row.UtworzylName = userNames.TryGetValue(row.UtworzylId, out var n) ? n : row.UtworzylId;
                    row.ZmienilName = !string.IsNullOrEmpty(row.ZmienilId) && userNames.TryGetValue(row.ZmienilId, out var nz)
                        ? nz : row.ZmienilId;
                    row.UstawHandlowcow(handl, _svc.HandlowiecUserId);
                }

                // ETA z GPS — tylko gdy toggle 🎯 ETA jest włączone (drogie: Webfleet per pojazd)
                if (TglEta?.IsChecked == true)
                    await WyliczEtaDlaWszystkichAsync(ladunki, info);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TransportWPF] agregaty: {ex.Message}");
            }
        }

        /// <summary>Równolegle pobiera GPS + liczy ETA dla wszystkich kursów z pojazdem (Task.WhenAll).</summary>
        private async Task WyliczEtaDlaWszystkichAsync(Dictionary<long, List<Ladunek>> ladunki, Dictionary<int, ZamowienieNazwaInfo> info)
        {
            try
            {
                // Tym samym pojazdem mogą jechać 2 kursy w jednym dniu (np. 14:00 + 21:00).
                // Gdy drugi kurs już wystartował (GodzWyjazdu <= teraz), pierwszy nie ma sensu liczyć
                // ETA do bazy — pojazd jest w nowej trasie. Oznaczamy go jako „koniec".
                var teraz = DateTime.Now.TimeOfDay;
                var zastapioneKursy = new HashSet<long>();
                var kursyPerPojazd = _rowsAll
                    .Where(r => r.Source.PojazdID.HasValue && r.Source.GodzWyjazdu.HasValue)
                    .GroupBy(r => r.Source.PojazdID!.Value);
                foreach (var grupa in kursyPerPojazd)
                {
                    var posort = grupa.OrderBy(r => r.Source.GodzWyjazdu!.Value).ToList();
                    for (int i = 0; i < posort.Count - 1; i++)
                    {
                        var nast = posort[i + 1];
                        if (nast.Source.GodzWyjazdu!.Value <= teraz)
                            zastapioneKursy.Add(posort[i].KursID);
                    }
                }

                var taski = _rowsAll
                    .Where(r => r.Source.PojazdID.HasValue)
                    .Select(r => WypelnijEtaNastepnyGpsAsync(r, ladunki, info, zastapioneKursy.Contains(r.KursID)))
                    .ToList();
                await Task.WhenAll(taski);
                KursyGrid.Items.Refresh();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ETA GPS] {ex.Message}");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // SZYBKIE DODAWANIE wolnych → zaznaczony kurs
        // ════════════════════════════════════════════════════════════════════
        private void WolneGrid_DoubleClick(object s, MouseButtonEventArgs e)
        {
            // Dwuklik w wolne → NOWY KURS z tym odbiorcą gotowy do uzupełnienia kierowcy/pojazdu/godzin.
            if (WolneGrid.SelectedItem is not WolneZamowienieWpf z) return;
            try
            {
                var data = DataKursu.SelectedDate ?? DateTime.Today;
                var ed = new EdytorKursuWpfWindow(_svc, _user, data, kursId: null, preselect: new[] { z }) { Owner = this };
                if (ed.ShowDialog() == true)
                {
                    _svc.InwalidujCacheZmian();
                    _ = LoadWszystkoAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDodajDoKursu_Click(object s, RoutedEventArgs e)
        {
            var wybrane = WolneGrid.SelectedItems.Cast<WolneZamowienieWpf>().ToList();
            _ = DodajDoKursuAsync(wybrane);
        }

        // drag&drop: wolne zamówienie(a) → wiersz kursu
        private void WolneGrid_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (!WpfDragHelper.ExceededThreshold(_dragStart, e.GetPosition(null))) return;
            var items = WolneGrid.SelectedItems.Cast<WolneZamowienieWpf>().ToList();
            if (items.Count == 0) return;

            StartGhost(items.Count == 1
                ? $"📦 {items[0].KlientNazwa} — przeciągnij na kurs"
                : $"📦 {items.Count} zam. — przeciągnij na kurs");
            if (DropHint != null) DropHint.Visibility = Visibility.Visible;
            try { DragDrop.DoDragDrop(WolneGrid, new DataObject(FmtWolne, items), DragDropEffects.Copy); }
            catch { }
            finally
            {
                EndGhost();
                if (DropHint != null) DropHint.Visibility = Visibility.Collapsed;
                ResetHover();
            }
        }

        private void StartGhost(string text)
        {
            try
            {
                if (Content is not UIElement host) return;
                _ghostLayer = System.Windows.Documents.AdornerLayer.GetAdornerLayer(host);
                if (_ghostLayer == null) return;
                _ghost = new DragGhostAdorner(host, text);
                _ghostLayer.Add(_ghost);
            }
            catch { }
        }

        private void EndGhost()
        {
            try { if (_ghostLayer != null && _ghost != null) _ghostLayer.Remove(_ghost); } catch { }
            _ghost = null; _ghostLayer = null;
        }

        private void KursyGrid_DragOver(object sender, DragEventArgs e)
        {
            bool ok = e.Data.GetDataPresent(FmtWolne);
            e.Effects = ok ? DragDropEffects.Copy : DragDropEffects.None;
            if (ok)
            {
                Highlight(WpfDragHelper.GetRowAtPoint(KursyGrid, e.GetPosition(KursyGrid)));
                if (_ghost != null && Content is IInputElement root) _ghost.SetPosition(e.GetPosition(root));
            }
            e.Handled = true;
        }

        private void Highlight(DataGridRow? row)
        {
            if (ReferenceEquals(row, _hoverRow)) return;
            ResetHover();
            if (row != null)
            {
                row.Background = new SolidColorBrush(Color.FromRgb(0xC8, 0xE6, 0xC9)); // zielony cel
                _hoverRow = row;
            }
        }
        private void ResetHover()
        {
            if (_hoverRow != null) { _hoverRow.ClearValue(Control.BackgroundProperty); _hoverRow = null; }
        }

        private void KursyGrid_Drop(object sender, DragEventArgs e)
        {
            ResetHover();
            if (!e.Data.GetDataPresent(FmtWolne)) return;
            if (e.Data.GetData(FmtWolne) is not List<WolneZamowienieWpf> items || items.Count == 0) return;

            // Upadek NA WIERSZ kursu → dodaj do istniejącego.
            // Upadek na PUSTY OBSZAR listy → otwórz edytor NOWEGO kursu z tymi N odbiorcami od razu jako ładunki.
            if (WpfDragHelper.GetItemAtPoint(KursyGrid, e.GetPosition(KursyGrid)) is KursRow target)
            {
                KursyGrid.SelectedItem = target;
                _ = DodajDoKursuAsync(items, target);
            }
            else
            {
                try
                {
                    var data = DataKursu.SelectedDate ?? DateTime.Today;
                    var ed = new EdytorKursuWpfWindow(_svc, _user, data, kursId: null, preselect: items) { Owner = this };
                    if (ed.ShowDialog() == true)
                    {
                        _svc.InwalidujCacheZmian();
                        _ = LoadWszystkoAsync();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd otwierania edytora:\n{ex.Message}", "Błąd",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            e.Handled = true;
        }

        private async Task DodajDoKursuAsync(List<WolneZamowienieWpf> zamowienia, KursRow? target = null)
        {
            var row = target ?? KursyGrid.SelectedItem as KursRow;
            if (row == null)
            {
                MessageBox.Show("Najpierw zaznacz kurs po lewej stronie (lub przeciągnij na wiersz kursu).", "Brak kursu",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (zamowienia == null || zamowienia.Count == 0) return;

            try
            {
                StatusText.Text = $"Dodawanie {zamowienia.Count} zam. do kursu #{row.KursID}...";
                foreach (var z in zamowienia)
                {
                    await _svc.Repo.DodajLadunekAsync(new Ladunek
                    {
                        KursID = row.KursID,
                        KodKlienta = z.KodKlienta,
                        PojemnikiE2 = z.Pojemniki,
                        TrybE2 = z.TrybE2
                    });
                }

                // pełny zbiór zamówień w kursie → spójny status + auto-healing
                var lad = await _svc.Repo.PobierzLadunkiAsync(row.KursID);
                var zamIds = lad.Where(l => l.KodKlienta != null && l.KodKlienta.StartsWith("ZAM_")
                                            && int.TryParse(l.KodKlienta.Substring(4), out _))
                                .Select(l => int.Parse(l.KodKlienta!.Substring(4)))
                                .ToHashSet();
                await _svc.SyncStatusyKursuAsync(row.KursID, zamIds, _user);

                long keepId = row.KursID;
                await LoadKursyAsync();
                await OdswiezWolneAsync();
                var again = _rows.FirstOrDefault(r => r.KursID == keepId);
                if (again != null) { KursyGrid.SelectedItem = again; KursyGrid.ScrollIntoView(again); }
                WolneGrid.UnselectAll();   // czyste pole pod kolejne klikanie
                StatusTymczasowy($"✓ Dodano {zamowienia.Count} zam. do kursu #{keepId}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd dodawania do kursu:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Błąd dodawania.";
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // EDYTOR / NOWY / USUŃ
        // ════════════════════════════════════════════════════════════════════
        private void KursyGrid_DoubleClick(object s, MouseButtonEventArgs e)
        {
            if (KursyGrid.SelectedItem is KursRow) OtworzEdytor(false);
        }
        private void BtnNowy_Click(object s, RoutedEventArgs e) => OtworzEdytor(true);
        private void BtnEdytuj_Click(object s, RoutedEventArgs e) => OtworzEdytor(false);

        private void OtworzEdytor(bool nowy)
        {
            try
            {
                var data = DataKursu.SelectedDate ?? DateTime.Today;
                long? kursId = null;
                if (!nowy)
                {
                    if (KursyGrid.SelectedItem is not KursRow row) return;
                    kursId = row.KursID;
                }
                var ed = new EdytorKursuWpfWindow(_svc, _user, data, kursId) { Owner = this };
                if (ed.ShowDialog() == true)
                {
                    _svc.InwalidujCacheZmian();   // po edycji świeże pendingi
                    _ = LoadWszystkoAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd otwierania edytora:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnUsun_Click(object s, RoutedEventArgs e)
        {
            if (KursyGrid.SelectedItem is not KursRow row) return;
            var msg = $"Usunąć kurs #{row.KursID}?\n" +
                      $"Trasa: {row.Trasa ?? "—"}\n" +
                      $"Kierowca: {row.KierowcaNazwa ?? "—"} · Pojazd: {row.PojazdRejestracja ?? "—"}\n\n" +
                      "Zamówienia w kursie wrócą do statusu wolnych.";
            if (MessageBox.Show(msg, "Potwierdź usunięcie", MessageBoxButton.YesNo, MessageBoxImage.Question)
                != MessageBoxResult.Yes) return;

            try
            {
                await _svc.Repo.UsunKursAsync(row.KursID);   // sam zwalnia statusy zamówień
                StatusText.Text = $"Usunięto kurs #{row.KursID}";
                await LoadWszystkoAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd usuwania:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // SZYBKI PRZYDZIAŁ kierowcy/pojazdu
        // ════════════════════════════════════════════════════════════════════
        private async void MiPrzydzial_Click(object sender, RoutedEventArgs e)
        {
            if (KursyGrid.SelectedItem is not KursRow row) return;
            try
            {
                await EnsureSlownikiAsync();
                var dlg = new Dialogs.SzybkiPrzydzialDialog(_kierowcy, _pojazdy,
                    row.Source.KierowcaID, row.Source.PojazdID, $"🧑‍✈ Przydział — kurs #{row.KursID}") { Owner = this };
                if (dlg.ShowDialog() != true) return;

                var k = row.Source;
                k.KierowcaID = dlg.KierowcaID;
                k.PojazdID = dlg.PojazdID;
                await _svc.Repo.AktualizujNaglowekKursuAsync(k, _user);

                long keep = row.KursID;
                await LoadKursyAsync();
                var again = _rows.FirstOrDefault(r => r.KursID == keep);
                if (again != null) { KursyGrid.SelectedItem = again; KursyGrid.ScrollIntoView(again); }
                StatusText.Text = $"Zaktualizowano przydział kursu #{keep}.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd przydziału:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // ODBIÓR WŁASNY + AUTO-ODŚWIEŻANIE
        // ════════════════════════════════════════════════════════════════════
        private async void MiWlasnyOdbior_Click(object sender, RoutedEventArgs e)
        {
            var wybrane = WolneGrid.SelectedItems.Cast<WolneZamowienieWpf>().ToList();
            if (wybrane.Count == 0) return;
            // Bez potwierdzenia — klik = wykonaj. Status w pasku informuje.
            try
            {
                foreach (var z in wybrane) await _svc.WlasnyOdbiorAsync(z.ZamowienieId, _user);
                await OdswiezWolneAsync();
                StatusTymczasowy($"✓ Oznaczono {wybrane.Count} jako odbiór własny.");
            }
            catch (Exception ex) { StatusText.Text = $"Błąd: {ex.Message}"; }
        }

        private void TglAuto_Click(object sender, RoutedEventArgs e)
        {
            if (TglAuto.IsChecked == true)
            {
                _autoTimer ??= new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(45) };
                _autoTimer.Tick -= AutoTick;
                _autoTimer.Tick += AutoTick;
                _autoTimer.Start();
                TglAuto.Background = new SolidColorBrush(Color.FromRgb(0xC8, 0xE6, 0xC9));
                TglAuto.Foreground = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
                StatusText.Text = "Auto-odświeżanie WŁ. (co 45 s)";
            }
            else
            {
                _autoTimer?.Stop();
                TglAuto.ClearValue(BackgroundProperty);
                TglAuto.ClearValue(ForegroundProperty);
                StatusText.Text = "Auto-odświeżanie WYŁ.";
            }
        }

        private async void AutoTick(object? sender, EventArgs e)
        {
            var keep = (KursyGrid.SelectedItem as KursRow)?.KursID;
            await LoadWszystkoAsync();
            if (keep.HasValue)
            {
                var again = _rows.FirstOrDefault(r => r.KursID == keep.Value);
                if (again != null) KursyGrid.SelectedItem = again;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // Row wrapper
        // ════════════════════════════════════════════════════════════════════
        public class HandlowiecAvatar
        {
            public string? Id { get; set; }
            public string Name { get; set; } = "";
        }

        /// <summary>
        /// Pojedynczy segment trasy (klient + separator). Pozwala na per-klient kolor — czerwony
        /// dla klientów bez GPS, ciemny dla pozostałych. Separator " → " między, pusty po ostatnim.
        /// </summary>
        public class TrasaSegment
        {
            public string Nazwa { get; set; } = "";
            public Brush Kolor { get; set; } = Brushes.Black;
            public string Separator { get; set; } = "";   // "  →  " między, "" po ostatnim
            public bool BezGps { get; set; }
            public FontWeight Waga => BezGps ? FontWeights.SemiBold : FontWeights.Normal;
        }

        public class KursRow
        {
            public Kurs Source { get; }
            public int LiczbaLadunkow { get; }
            public KursRow(Kurs k, int liczbaLadunkow) { Source = k; LiczbaLadunkow = liczbaLadunkow; }

            // ── ZMIANY OCZEKUJĄCE (TransportZmiany) ──
            public int LiczbaZmianOczekujacych { get; set; }
            public bool MaZmiany => LiczbaZmianOczekujacych > 0;
            public Visibility ZmianyVis => MaZmiany ? Visibility.Visible : Visibility.Collapsed;
            public string ZmianyText => MaZmiany ? LiczbaZmianOczekujacych.ToString() : "";
            public string? ZmianyTooltip => MaZmiany
                ? $"🔔 {LiczbaZmianOczekujacych} {(LiczbaZmianOczekujacych == 1 ? "zmiana" : "zmian")} do akceptacji — otwórz kurs"
                : null;

            public long KursID => Source.KursID;
            public string? Trasa => Source.Trasa;

            // Auto-trasa wyliczana z ładunków: pierwszy → ostatni klient (po Kolejność).
            // Wypełniana w UzupelnijAgregatyAsync. Fallback gdy puste: ręczna Source.Trasa, potem "—".
            public string TrasaAuto { get; set; } = "—";   // fallback gdy brak segmentów (sortowanie/eksport)
            public string? TrasaAutoTooltip { get; set; }
            // Segmenty trasy — każdy klient osobno z własnym kolorem.
            // Klient bez GPS → czerwony; reszta → standardowy ciemny. Separator " → " między.
            public List<TrasaSegment> TrasaSegmenty { get; set; } = new();
            public bool MaSegmenty => TrasaSegmenty.Count > 0;
            public Visibility SegmentyVis => MaSegmenty ? Visibility.Visible : Visibility.Collapsed;
            public Visibility FallbackVis => MaSegmenty ? Visibility.Collapsed : Visibility.Visible;

            // ETA do następnego przystanku — toggle button w toolbarze pokazuje/ukrywa kolumnę.
            // Wypełniane w UzupelnijAgregatyAsync na podstawie awizacji ładunków + DateTime.Now.
            public string EtaCzas { get; set; } = "";       // "za 23 min · 12 km" lub diagnostyka („brak GPS pojazdu")
            public string EtaKlient { get; set; } = "";     // „Z Łódź → Damak" / „🛰 Łódź (pojazd stoi)"
            public Brush EtaKolor { get; set; } = Brushes.Gray;   // czerwony=opóźniony, amber=za <30min, zielony=OK, szary=brak/po wszystkim
            public string? EtaTooltip { get; set; }   // pełny adres GPS pojazdu (postext z Webfleet) + prędkość
            public string? KierowcaNazwa => Source.KierowcaNazwa;
            public string? PojazdRejestracja => Source.PojazdRejestracja;

            // Telefon kierowcy (z TransportPL.Kierowca.Telefon) — wypełniany w LoadKursyAsync.
            // Pod nazwiskiem w kolumnie Kierowca — żeby planista mógł zadzwonić bez otwierania karty.
            public string KierowcaTelefon { get; set; } = "";
            public string Status => Source.Status ?? "Planowany";
            public int PaletyNominal => Source.PaletyNominal;

            // ── kolumny wierne oryginałowi ──
            public string GodzWyjazduDisplay => Source.GodzWyjazdu?.ToString(@"hh\:mm") ?? "--:--";

            public bool BrakKierowcy => string.IsNullOrEmpty(KierowcaNazwa);
            public bool BrakPojazdu => string.IsNullOrEmpty(PojazdRejestracja);
            public string KierowcaDisplay => BrakKierowcy ? "⚠ BRAK" : KierowcaNazwa!;
            public string PojazdDisplay => BrakPojazdu ? "⚠ BRAK" : PojazdRejestracja!;
            // indywidualny kolor kierowcy/pojazdu (niezależnie) — z ID przez Knuth-hash
            public Brush KolorKierowcy => BrakKierowcy ? Brushes.Transparent : Helpers.KolorZId.BrushDlaInt(Source.KierowcaID);
            public Brush KolorPojazdu => BrakPojazdu ? Brushes.Transparent : Helpers.KolorZId.BrushDlaInt(Source.PojazdID);
            public Brush KierowcaFg => BrakKierowcy ? new SolidColorBrush(Color.FromRgb(0xE6, 0x7E, 0x22)) : new SolidColorBrush(Color.FromRgb(0x37, 0x47, 0x4F));
            public Brush PojazdFg => BrakPojazdu ? new SolidColorBrush(Color.FromRgb(0xE6, 0x7E, 0x22)) : new SolidColorBrush(Color.FromRgb(0x37, 0x47, 0x4F));
            public FontWeight KierowcaWaga => BrakKierowcy ? FontWeights.Bold : FontWeights.Normal;
            public FontWeight PojazdWaga => BrakPojazdu ? FontWeights.Bold : FontWeights.Normal;

            public int Pal => Source.PaletyNominal;
            public int Poj => Source.SumaE2;
            public decimal Kg { get; set; }
            public string KgDisplay => Kg > 0 ? Kg.ToString("N0") : "—";
            public string WypDisplay => Source.PaletyPojazdu <= 0 ? "—" : $"{Source.ProcNominal:F0}%";

            public string UtworzylId => Source.Utworzyl ?? "";
            public string UtworzylName { get; set; } = "";
            public string UtworzylDataDisplay => string.IsNullOrEmpty(Source.Utworzyl) ? "" : Source.UtworzonoUTC.ToLocalTime().ToString("dd.MM HH:mm");
            public string UtworzylDisplay
            {
                get
                {
                    var nm = string.IsNullOrEmpty(UtworzylName) ? UtworzylId : UtworzylName;
                    if (string.IsNullOrEmpty(nm) && string.IsNullOrEmpty(UtworzylDataDisplay)) return "—";
                    return string.IsNullOrEmpty(UtworzylDataDisplay) ? nm : $"{nm} · {UtworzylDataDisplay}";
                }
            }

            // ── ZMIENIŁ (ostatnia modyfikacja) ──
            public string ZmienilId => Source.Zmienil ?? "";
            public string ZmienilName { get; set; } = "";
            public string ZmienilDataDisplay => Source.ZmienionoUTC.HasValue
                ? Source.ZmienionoUTC.Value.ToLocalTime().ToString("dd.MM HH:mm") : "";
            public bool BylZmieniany => !string.IsNullOrEmpty(Source.Zmienil) && Source.ZmienionoUTC.HasValue;
            public Visibility ZmienilVis => BylZmieniany ? Visibility.Visible : Visibility.Collapsed;
            public string ZmienilDisplay
            {
                get
                {
                    if (!BylZmieniany) return "";
                    var nm = string.IsNullOrEmpty(ZmienilName) ? ZmienilId : ZmienilName;
                    return $"✎ {nm} · {ZmienilDataDisplay}";
                }
            }

            public List<HandlowiecAvatar> HandlowcyAvatars { get; private set; } = new();
            public string HandlowcyText { get; private set; } = "";
            public string HandlowcyTooltip { get; private set; } = "";

            public void UstawHandlowcow(List<string> names, Func<string, string?> resolveId)
            {
                HandlowcyAvatars = names.Take(3).Select(n => new HandlowiecAvatar { Name = n, Id = resolveId(n) }).ToList();
                HandlowcyText = names.Count == 0 ? ""
                    : names.Count <= 2 ? string.Join(", ", names.Select(Skroc))
                    : $"{Skroc(names[0])} +{names.Count - 1}";
                HandlowcyTooltip = names.Count == 0 ? "" : "Handlowcy:\n" + string.Join("\n", names);
            }

            private static string Skroc(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return s;
                var p = s.Split(' ');
                return p.Length >= 2 && p[1].Length > 0 ? $"{p[0]} {p[1][0]}." : s;
            }

            public string GodzinyDisplay =>
                $"{Source.GodzWyjazdu?.ToString(@"hh\:mm") ?? "—"} → {Source.GodzPowrotu?.ToString(@"hh\:mm") ?? "—"}";

            public string WypelnienieDisplay => Source.PaletyPojazdu <= 0
                ? "—"
                : $"{Source.PaletyNominal}/{Source.PaletyPojazdu}  ({Source.ProcNominal:F0}%)";

            // kolor wiersza: pusty kurs = czerwonawy, brak kierowcy/pojazdu = żółty, OK = biały
            public Brush RowBg
            {
                get
                {
                    if (LiczbaLadunkow == 0) return new SolidColorBrush(Color.FromRgb(0xFE, 0xE6, 0xE6));
                    if (string.IsNullOrEmpty(KierowcaNazwa) || string.IsNullOrEmpty(PojazdRejestracja))
                        return new SolidColorBrush(Color.FromRgb(0xFF, 0xF8, 0xE1));
                    return Brushes.White;
                }
            }

            // kolor tekstu wypełnienia: zielony <75%, pomarańcz 75-100%, czerwony >100%
            public Brush WypelnienieFg
            {
                get
                {
                    if (Source.PaletyPojazdu <= 0) return new SolidColorBrush(Color.FromRgb(0x90, 0xA4, 0xAE));
                    var p = Source.ProcNominal;
                    if (p > 100) return new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));
                    if (p >= 75) return new SolidColorBrush(Color.FromRgb(0xEF, 0x6C, 0x00));
                    return new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
                }
            }

            // lewy pasek statusu „bez czytania": pusty=czerwony, brak przydziału=amber, przeładowany=czerwony, OK=zielony
            public Brush StatusAccent
            {
                get
                {
                    if (LiczbaLadunkow == 0) return new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));
                    if (BrakKierowcy || BrakPojazdu) return new SolidColorBrush(Color.FromRgb(0xB2, 0x6A, 0x00));
                    if (Source.PaletyPojazdu > 0 && Source.ProcNominal > 100) return new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));
                    return new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
                }
            }

            // zgrupowane metryki + mini-pasek wypełnienia + tooltip wiersza (audyt: kto utworzył)
            public string LadunekLinia1 => $"{Pal} pal · {Poj} poj";
            // metryki + ładowność pojazdu w kontekście — żeby od razu widzieć ile zostało miejsca
            public string LadunekIPojazdLinia => Source.PaletyPojazdu > 0
                ? $"{Pal}/{Source.PaletyPojazdu} pal · {Poj} poj"
                : $"{Pal} pal · {Poj} poj";
            public string KgDisplay2 => Kg > 0 ? $"{Kg:N0} kg" : "—";
            public double WypBarWidth => Source.PaletyPojazdu <= 0 ? 0 : Math.Min(100.0, (double)Source.ProcNominal) / 100.0 * 78.0;
            public string? WierszTooltip
            {
                get
                {
                    var lines = new List<string>();
                    var twNm = string.IsNullOrEmpty(UtworzylName) ? UtworzylId : UtworzylName;
                    if (!string.IsNullOrEmpty(twNm) || !string.IsNullOrEmpty(UtworzylDataDisplay))
                    {
                        var t = string.IsNullOrEmpty(twNm) ? "" : $"Utworzył: {twNm}";
                        if (!string.IsNullOrEmpty(UtworzylDataDisplay)) t += (t.Length > 0 ? " · " : "") + UtworzylDataDisplay;
                        if (!string.IsNullOrEmpty(t)) lines.Add(t);
                    }
                    if (BylZmieniany)
                    {
                        var zmNm = string.IsNullOrEmpty(ZmienilName) ? ZmienilId : ZmienilName;
                        lines.Add($"Zmienił: {zmNm} · {ZmienilDataDisplay}");
                    }
                    return lines.Count == 0 ? null : string.Join("\n", lines);
                }
            }

            public Brush StatusBg => Status switch
            {
                "WTrasie" or "W trasie" => new SolidColorBrush(Color.FromRgb(232, 245, 233)),
                "Zakonczony" or "Zakończony" => new SolidColorBrush(Color.FromRgb(232, 234, 246)),
                "Anulowany" => new SolidColorBrush(Color.FromRgb(255, 235, 238)),
                "Akceptowany" => new SolidColorBrush(Color.FromRgb(225, 245, 254)),
                _ => new SolidColorBrush(Color.FromRgb(255, 243, 224))
            };
            public Brush StatusFg => Status switch
            {
                "WTrasie" or "W trasie" => new SolidColorBrush(Color.FromRgb(46, 125, 50)),
                "Zakonczony" or "Zakończony" => new SolidColorBrush(Color.FromRgb(57, 73, 171)),
                "Anulowany" => new SolidColorBrush(Color.FromRgb(198, 40, 40)),
                "Akceptowany" => new SolidColorBrush(Color.FromRgb(2, 119, 189)),
                _ => new SolidColorBrush(Color.FromRgb(230, 81, 0))
            };
        }
    }
}
