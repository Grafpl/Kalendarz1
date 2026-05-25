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

namespace Kalendarz1.Transport.WPF
{
    public partial class PlanowanieTransportuWpfWindow : Window
    {
        private readonly TransportWpfService _svc = new();
        private readonly string _user = App.UserID ?? "system";
        private List<KursRow> _rows = new();

        private List<WolneZamowienieWpf> _wolneAll = new();
        private readonly ObservableCollection<WolneZamowienieWpf> _wolne = new();
        private bool _ladowanie;

        private List<Kierowca> _kierowcy = new();
        private List<Pojazd> _pojazdy = new();
        private bool _slownikiZaladowane;

        private Point _dragStart;
        private const string FmtWolne = "ZPSP_wolne";

        public PlanowanieTransportuWpfWindow()
        {
            InitializeComponent();
            try { WindowIconHelper.SetIcon(this); } catch { }
            WolneGrid.ItemsSource = _wolne;
            WolneGrid.SelectionChanged += (_, _) => UpdateDodajButton();
            WolneGrid.PreviewMouseLeftButtonDown += (_, e) => _dragStart = e.GetPosition(null);
            WolneGrid.PreviewMouseMove += WolneGrid_PreviewMouseMove;
            KursyGrid.DragOver += KursyGrid_DragOver;
            KursyGrid.Drop += KursyGrid_Drop;
            DataKursu.SelectedDate = DateTime.Today;
            Loaded += async (_, _) => await LoadWszystkoAsync();
            KeyDown += async (_, e) => { if (e.Key == Key.F5) { await LoadWszystkoAsync(); e.Handled = true; } };
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
        private async void BtnRefresh_Click(object s, RoutedEventArgs e) => await LoadWszystkoAsync();

        private async Task LoadWszystkoAsync()
        {
            await LoadKursyAsync();
            await OdswiezWolneAsync();
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

                _rows = kursy.Select(k => new KursRow(k,
                    ladunki.TryGetValue(k.KursID, out var l) ? l.Count : 0)).ToList();
                KursyGrid.ItemsSource = _rows;

                UpdateKpi();
                StatusText.Text = $"Załadowano {_rows.Count} kursów na {data:dd.MM.yyyy}";
                UpdateButtons();
                PodgladNaglowek.Text = "— wybierz kurs —";
                PodgladGrid.ItemsSource = null;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Błąd: {ex.Message}";
                MessageBox.Show($"Błąd ładowania kursów:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateKpi()
        {
            KpiKursy.Text = _rows.Count.ToString();
            KpiZKierowca.Text = _rows.Count(r => !string.IsNullOrEmpty(r.KierowcaNazwa)).ToString();
            KpiBezZasobow.Text = _rows.Count(r => string.IsNullOrEmpty(r.KierowcaNazwa) || string.IsNullOrEmpty(r.PojazdRejestracja)).ToString();
            KpiPalety.Text = _rows.Sum(r => r.PaletyNominal).ToString();
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
                KpiWolne.Text = _wolneAll.Count.ToString();
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
            if (KursyGrid.SelectedItem is KursRow row) await LoadPodgladAsync(row);
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

        private async Task LoadPodgladAsync(KursRow row)
        {
            try
            {
                PodgladNaglowek.Text = $"#{row.KursID} · {row.Trasa ?? "—"}";
                var dbLad = await _svc.Repo.PobierzLadunkiAsync(row.KursID);

                var rows = dbLad.OrderBy(l => l.Kolejnosc).Select(l => new LadunekWierszWpf
                {
                    LadunekID = l.LadunekID,
                    Kolejnosc = l.Kolejnosc,
                    KodKlienta = l.KodKlienta,
                    PojemnikiE2 = l.PojemnikiE2,
                    Uwagi = l.Uwagi,
                    NazwaKlienta = l.KodKlienta ?? "—"
                }).ToList();

                var zamIds = rows.Where(r => r.ZamowienieId.HasValue).Select(r => r.ZamowienieId!.Value).ToList();
                if (zamIds.Count > 0)
                {
                    var nazwy = await _svc.ResolveNazwyAsync(zamIds);
                    foreach (var r in rows)
                        if (r.ZamowienieId.HasValue && nazwy.TryGetValue(r.ZamowienieId.Value, out var info))
                        {
                            r.NazwaKlienta = info.Nazwa;
                            r.Awizacja = info.Awizacja;
                        }
                }
                PodgladGrid.ItemsSource = rows;
                PodgladNaglowek.Text = $"#{row.KursID} · {rows.Count} ładunków · {rows.Sum(r => r.PojemnikiE2)} poj.";
            }
            catch (Exception ex)
            {
                PodgladNaglowek.Text = $"Błąd: {ex.Message}";
                PodgladGrid.ItemsSource = null;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // SZYBKIE DODAWANIE wolnych → zaznaczony kurs
        // ════════════════════════════════════════════════════════════════════
        private void WolneGrid_DoubleClick(object s, MouseButtonEventArgs e)
        {
            if (WolneGrid.SelectedItem is WolneZamowienieWpf z) _ = DodajDoKursuAsync(new List<WolneZamowienieWpf> { z });
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
            try { DragDrop.DoDragDrop(WolneGrid, new DataObject(FmtWolne, items), DragDropEffects.Copy); }
            catch { }
        }

        private void KursyGrid_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(FmtWolne) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void KursyGrid_Drop(object sender, DragEventArgs e)
        {
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
        // Row wrapper
        // ════════════════════════════════════════════════════════════════════
        public class KursRow
        {
            public Kurs Source { get; }
            public int LiczbaLadunkow { get; }
            public KursRow(Kurs k, int liczbaLadunkow) { Source = k; LiczbaLadunkow = liczbaLadunkow; }

            public long KursID => Source.KursID;
            public string? Trasa => Source.Trasa;
            public string? KierowcaNazwa => Source.KierowcaNazwa;
            public string? PojazdRejestracja => Source.PojazdRejestracja;
            public string Status => Source.Status ?? "Planowany";
            public int PaletyNominal => Source.PaletyNominal;

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
