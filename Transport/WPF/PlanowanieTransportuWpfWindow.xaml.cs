// ════════════════════════════════════════════════════════════════════════════
// Transport/WPF/PlanowanieTransportuWpfWindow.xaml.cs
// ════════════════════════════════════════════════════════════════════════════
// Okno planowania — sandbox WPF (lista kursów + podgląd ładunków). NIE dotyka
// WinForms (TransportMainFormImproved) ani istniejącego WPF Huba. Otwiera własny
// edytor WPF (EdytorKursuWpfWindow). Reuse: TransportRepozytorium / TransportWpfService.
// ════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
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

        public PlanowanieTransportuWpfWindow()
        {
            InitializeComponent();
            try { WindowIconHelper.SetIcon(this); } catch { }
            DataKursu.SelectedDate = DateTime.Today;
            Loaded += async (_, _) => await LoadKursyAsync();
            KeyDown += async (_, e) => { if (e.Key == Key.F5) { await LoadKursyAsync(); e.Handled = true; } };
        }

        // ── nawigacja datą ──
        private void BtnPrev_Click(object s, RoutedEventArgs e) => DataKursu.SelectedDate = (DataKursu.SelectedDate ?? DateTime.Today).AddDays(-1);
        private void BtnNext_Click(object s, RoutedEventArgs e) => DataKursu.SelectedDate = (DataKursu.SelectedDate ?? DateTime.Today).AddDays(1);
        private void BtnDzis_Click(object s, RoutedEventArgs e) => DataKursu.SelectedDate = DateTime.Today;
        private async void DataKursu_Changed(object s, SelectionChangedEventArgs e) => await LoadKursyAsync();
        private async void BtnRefresh_Click(object s, RoutedEventArgs e) => await LoadKursyAsync();

        // ════════════════════════════════════════════════════════════════════
        // LOAD
        // ════════════════════════════════════════════════════════════════════
        private async Task LoadKursyAsync()
        {
            try
            {
                var data = DataKursu.SelectedDate ?? DateTime.Today;
                StatusText.Text = $"Ładowanie {data:dd.MM.yyyy}...";
                DayNameText.Text = data.ToString("dddd", new CultureInfo("pl-PL"));

                var kursy = await _svc.Repo.PobierzKursyPoDacieAsync(data);

                // liczba ładunków per kurs (jedno zapytanie)
                var ladunki = await _svc.Repo.PobierzLadunkiDlaKursowAsync(kursy.Select(k => k.KursID));

                _rows = kursy.Select(k => new KursRow(k,
                    ladunki.TryGetValue(k.KursID, out var l) ? l.Count : 0)).ToList();
                KursyGrid.ItemsSource = _rows;

                UpdateKpi();
                StatusText.Text = $"Załadowano {_rows.Count} kursów na {data:dd.MM.yyyy}";
                UpdateButtons();
                PodgladNaglowek.Text = "Wybierz kurs z listy";
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
        // SELEKCJA + podgląd ładunków
        // ════════════════════════════════════════════════════════════════════
        private async void KursyGrid_SelectionChanged(object s, SelectionChangedEventArgs e)
        {
            UpdateButtons();
            if (KursyGrid.SelectedItem is KursRow row)
                await LoadPodgladAsync(row);
        }

        private void UpdateButtons()
        {
            bool sel = KursyGrid?.SelectedItem != null;
            if (BtnEdytuj != null) BtnEdytuj.IsEnabled = sel;
            if (BtnUsun != null) BtnUsun.IsEnabled = sel;
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
                PodgladNaglowek.Text = $"#{row.KursID} · {rows.Count} ładunków";
            }
            catch (Exception ex)
            {
                PodgladNaglowek.Text = $"Błąd podglądu: {ex.Message}";
                PodgladGrid.ItemsSource = null;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // AKCJE
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
                    _ = LoadKursyAsync();
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
                await LoadKursyAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd usuwania:\n{ex.Message}", "Błąd",
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
