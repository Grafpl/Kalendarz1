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
        private readonly string _user = App.UserID ?? "system";
        private List<KursRow> _rows = new();      // widoczne (po filtrze)
        private List<KursRow> _rowsAll = new();   // wszystkie z dnia

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
            KeyDown += Skroty_KeyDown;
            Closed += (_, _) => _autoTimer?.Stop();
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
                if (ed.ShowDialog() == true) _ = LoadWszystkoAsync();
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
        private async Task LoadKursyAsync()
        {
            try
            {
                var data = DataKursu.SelectedDate ?? DateTime.Today;
                StatusText.Text = $"Ładowanie {data:dd.MM.yyyy}...";
                DayNameText.Text = data.ToString("dddd", new CultureInfo("pl-PL"));

                var kursy = await _svc.Repo.PobierzKursyPoDacieAsync(data);
                var ladunki = await _svc.Repo.PobierzLadunkiDlaKursowAsync(kursy.Select(k => k.KursID));

                _rowsAll = kursy.Select(k => new KursRow(k,
                    ladunki.TryGetValue(k.KursID, out var l) ? l.Count : 0)).ToList();

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
                // Enter przy widocznym panelu zmian → Akceptuj wszystkie (priorytet nad edytorem)
                if (PanelZmianyDlaKursu.Visibility == Visibility.Visible && _detalZmiany.Count > 0)
                {
                    BtnDetalAkceptujWszystkie_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }
                else if (KursyGrid.SelectedItem is KursRow)
                {
                    OtworzEdytor(false);
                    e.Handled = true;
                }
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
                bool poUboju = RbUboj.IsChecked == true;
                _wolneAll = await _svc.LoadWolneZamowieniaAsync(data, poUboju);
                FiltrujWolne();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Błąd wolnych zamówień: {ex.Message}";
            }
        }

        private void FiltrujWolne()
        {
            var q = TxtSzukaj.Text?.Trim().ToLowerInvariant() ?? "";
            _wolne.Clear();
            IEnumerable<WolneZamowienieWpf> src = _wolneAll;
            if (!string.IsNullOrEmpty(q))
                src = src.Where(z => (z.KlientNazwa ?? "").ToLowerInvariant().Contains(q)
                                  || (z.Handlowiec ?? "").ToLowerInvariant().Contains(q));
            var lista = src.OrderBy(z => z.DataPrzyjazdu).ToList();
            foreach (var z in lista) _wolne.Add(z);

            WolneCountText.Text = _wolne.Count.ToString();
            int sumaPoj = lista.Sum(z => z.Pojemniki);
            WolneSuma.Text = $"Σ {lista.Count} zam.  ·  {sumaPoj} pojemników  ·  ~{(sumaPoj == 0 ? 0 : (int)Math.Ceiling(sumaPoj / 36.0))} palet";
            WolneEmpty.Visibility = _wolne.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateDodajButton();
        }

        private void TxtSzukaj_TextChanged(object s, TextChangedEventArgs e) { if (!_ladowanie) FiltrujWolne(); }
        private async void DataTyp_Changed(object s, RoutedEventArgs e) { if (!_ladowanie) await OdswiezWolneAsync(); }
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
            try
            {
                var raw = await _svc.PobierzZmianyDlaKursuAsync(row.KursID);
                foreach (var c in ZmianaCard.ScalListe(raw)) _detalZmiany.Add(c);

                // Bezpiecznik desync — gdy badge mówi co innego niż faktyczna liczba kart,
                // skoryguj badge na faktyczną wartość (źródło prawdy = panel, nie cache).
                if (row.LiczbaZmianOczekujacych != _detalZmiany.Count)
                {
                    row.LiczbaZmianOczekujacych = _detalZmiany.Count;
                    KursyGrid.Items.Refresh();
                }

                if (_detalZmiany.Count == 0)
                {
                    PanelZmianyDlaKursu.Visibility = Visibility.Collapsed;
                    return;
                }
                var trasa = string.IsNullOrEmpty(row.Trasa) ? "—" : row.Trasa;
                DetalNaglowek.Text = $"{_detalZmiany.Count} {(_detalZmiany.Count == 1 ? "zmiana" : "zmian")} dla kursu #{row.KursID} ({trasa}) — Co było → co jest";
                BtnDetalAkceptujText.Text = $"Akceptuj wszystkie ({_detalZmiany.Count})";
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
                StatusText.Text = card.IloscScalonych > 1
                    ? $"✓ Zaakceptowano {card.IloscScalonych} kolejnych edycji: {card.TypLabel} ({card.KlientNazwa})"
                    : $"✓ Zaakceptowano: {card.TypLabel} ({card.KlientNazwa})";
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
                StatusText.Text = $"✗ Odrzucono: {card.KlientNazwa} · {card.TypLabel}";
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
                StatusText.Text = $"✓ Zaakceptowano {liczbaKart} zmian ({liczbaIds} wpisów) dla kursu #{keep}.";
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
            var trasa = string.IsNullOrEmpty(row.Trasa) ? "—" : row.Trasa;
            DetalNaglowek.Text = $"{_detalZmiany.Count} {(_detalZmiany.Count == 1 ? "zmiana" : "zmian")} dla kursu #{row.KursID} ({trasa}) — Co było → co jest";
            BtnDetalAkceptujText.Text = $"Akceptuj wszystkie ({_detalZmiany.Count})";
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
                        var nazwyKlientow = ladList
                            .OrderBy(x => x.Kolejnosc)
                            .Where(x => x.KodKlienta != null && x.KodKlienta.StartsWith("ZAM_")
                                        && int.TryParse(x.KodKlienta.Substring(4), out _))
                            .Select(x =>
                            {
                                var zid = int.Parse(x.KodKlienta!.Substring(4));
                                return info.TryGetValue(zid, out var zi) ? zi.Nazwa : null;
                            })
                            .Where(s => !string.IsNullOrEmpty(s))
                            .Cast<string>()
                            .Distinct()   // ten sam klient z kilku ładunków = 1 stop
                            .ToList();

                        // Pełna trasa po kolei (po Distinct nazwy): Cezar → Trzepałka → PUBLIMAR.
                        // Dla bardzo długich (>5 stopów) skracamy: K1 → K2 → … → KN-1 → KN (N stopów).
                        row.TrasaAuto = nazwyKlientow.Count switch
                        {
                            0 => string.IsNullOrWhiteSpace(row.Trasa) ? "—" : row.Trasa!,
                            <= 5 => string.Join(" → ", nazwyKlientow),
                            _ => $"{nazwyKlientow[0]} → {nazwyKlientow[1]} → … → {nazwyKlientow[^2]} → {nazwyKlientow[^1]} ({nazwyKlientow.Count} stopów)"
                        };
                        row.TrasaAutoTooltip = nazwyKlientow.Count > 5
                            ? $"Trasa ({nazwyKlientow.Count} stopów):\n" + string.Join(" → ", nazwyKlientow)
                            : null;
                    }
                    else
                    {
                        row.TrasaAuto = string.IsNullOrWhiteSpace(row.Trasa) ? "—" : row.Trasa!;
                    }
                    row.UtworzylName = userNames.TryGetValue(row.UtworzylId, out var n) ? n : row.UtworzylId;
                    row.ZmienilName = !string.IsNullOrEmpty(row.ZmienilId) && userNames.TryGetValue(row.ZmienilId, out var nz)
                        ? nz : row.ZmienilId;
                    row.UstawHandlowcow(handl, _svc.HandlowiecUserId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TransportWPF] agregaty: {ex.Message}");
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
                if (ed.ShowDialog() == true) _ = LoadWszystkoAsync();
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
            if (WpfDragHelper.GetItemAtPoint(KursyGrid, e.GetPosition(KursyGrid)) is not KursRow target)
            {
                MessageBox.Show("Upuść zamówienie na konkretny kurs.", "Brak kursu",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (e.Data.GetData(FmtWolne) is List<WolneZamowienieWpf> items)
            {
                KursyGrid.SelectedItem = target;
                _ = DodajDoKursuAsync(items, target);
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
                StatusText.Text = $"Dodano {zamowienia.Count} zam. do kursu #{keepId}.";
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
                if (ed.ShowDialog() == true) _ = LoadWszystkoAsync();
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
            if (MessageBox.Show($"Oznaczyć {wybrane.Count} zam. jako ODBIÓR WŁASNY?\nZnikną z puli wolnych (transport po stronie klienta).",
                "Odbiór własny", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            try
            {
                foreach (var z in wybrane) await _svc.WlasnyOdbiorAsync(z.ZamowienieId, _user);
                await OdswiezWolneAsync();
                StatusText.Text = $"Oznaczono {wybrane.Count} jako odbiór własny.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
            public string TrasaAuto { get; set; } = "—";
            public string? TrasaAutoTooltip { get; set; }   // pełna lista klientów gdy więcej niż 2
            public string? KierowcaNazwa => Source.KierowcaNazwa;
            public string? PojazdRejestracja => Source.PojazdRejestracja;
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
