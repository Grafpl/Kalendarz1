using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Kalendarz1.AnalitykaPelna.Models;
using Kalendarz1.AnalitykaPelna.Services;
using LiveCharts;
using LiveCharts.Wpf;

namespace Kalendarz1.AnalitykaPelna.Windows
{
    public partial class SzczegolyKlasyDialog : Window
    {
        private readonly WydajnoscKlasa _klasa;
        private readonly FiltryAnaliz _filtry;
        private readonly RealizacjaService _realizacja;

        // Wszystkie ważenia z klas 4–12 — załadowane raz, filtrowane klient-side
        private List<WazenieZSzczegolami> _wszystkieRaw = new();
        private List<WazenieZSzczegolami> _wazenia = new();
        private ICollectionView? _view;

        private readonly HashSet<int> _wybraneKlasy = new();
        private bool _toggleSuppress;  // blokuje rekursję podczas programowego ustawiania ToggleButton.IsChecked

        public SzczegolyKlasyDialog(WydajnoscKlasa klasa, FiltryAnaliz f)
        {
            InitializeComponent();
            _klasa = klasa;
            _filtry = f;
            _realizacja = new RealizacjaService();
            _wybraneKlasy.Add(klasa.Klasa);

            KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };

            UstawHeader();
            UstawTogglePoczatkowe();
            Loaded += async (_, _) => await ZaladujWszystkoAsync();
        }

        private void UstawHeader()
        {
            UstawHeaderTekstIKolor();
            txtHeaderPodtytul.Text =
                $"Pełen rozkład ważeń: hodowcy, partie, standardy vs rzeczywistość  •  " +
                $"Okres: {_filtry.DataOd:dd.MM.yyyy} – {_filtry.DataDo:dd.MM.yyyy}";
        }

        private void UstawHeaderTekstIKolor()
        {
            // Tytuł zależny od liczby wybranych klas
            string tytul;
            if (_wybraneKlasy.Count == 0)
            {
                tytul = "❓  Nie wybrano żadnej klasy";
            }
            else if (_wybraneKlasy.Count == 1)
            {
                int k = _wybraneKlasy.First();
                bool duzy = k is >= 4 and <= 7;
                string ikona = duzy ? "🍗" : "🐥";
                string grupa = duzy ? "Duży kurczak (klasy 4–7)" : "Mały kurczak (klasy 8–12)";
                tytul = $"{ikona}  Klasa {k}  —  {grupa}";
            }
            else
            {
                bool wszDuze = _wybraneKlasy.All(k => k is >= 4 and <= 7);
                bool wszMale = _wybraneKlasy.All(k => k is >= 8 and <= 12);
                string ikona = wszDuze ? "🍗" : (wszMale ? "🐥" : "📊");
                string lista = string.Join(", ", _wybraneKlasy.OrderBy(x => x));
                tytul = $"{ikona}  Klasy: {lista}  ({_wybraneKlasy.Count} klas)";
            }
            txtHeaderTytul.Text = tytul;

            // Kolor: niebieski jeśli wszystkie duże, pomarańczowy jeśli wszystkie małe, fioletowy jeśli mieszane/puste
            var grad = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0)
            };

            bool wszystkieDuze = _wybraneKlasy.Count > 0 && _wybraneKlasy.All(k => k is >= 4 and <= 7);
            bool wszystkieMale = _wybraneKlasy.Count > 0 && _wybraneKlasy.All(k => k is >= 8 and <= 12);

            if (wszystkieDuze)
            {
                grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x25, 0x63, 0xEB), 0));
                grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x1E, 0x3A, 0x8A), 1));
            }
            else if (wszystkieMale)
            {
                grad.GradientStops.Add(new GradientStop(Color.FromRgb(0xF9, 0x73, 0x16), 0));
                grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x9A, 0x34, 0x12), 1));
            }
            else
            {
                grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x7C, 0x3A, 0xED), 0));
                grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x4C, 0x1D, 0x95), 1));
            }
            headerBorder.Background = grad;
        }

        private void UstawTogglePoczatkowe()
        {
            _toggleSuppress = true;
            try
            {
                foreach (var (kl, btn) in WszystkieToggle())
                    btn.IsChecked = _wybraneKlasy.Contains(kl);
            }
            finally { _toggleSuppress = false; }
        }

        private IEnumerable<(int klasa, ToggleButton btn)> WszystkieToggle()
        {
            yield return (4, kbtn4);
            yield return (5, kbtn5);
            yield return (6, kbtn6);
            yield return (7, kbtn7);
            yield return (8, kbtn8);
            yield return (9, kbtn9);
            yield return (10, kbtn10);
            yield return (11, kbtn11);
            yield return (12, kbtn12);
        }

        private async Task ZaladujWszystkoAsync()
        {
            var stoper = Stopwatch.StartNew();
            try
            {
                // Ładujemy wszystkie ważenia z klas 4–12 jednym strzałem (bez filtru klasy w SQL).
                // Filtrowanie po klasie odbywa się klient-side, dzięki czemu toggle są natychmiastowe
                // i % z partii liczony jest spójnie dla całej puli.
                var f = new FiltryAnaliz
                {
                    DataOd = _filtry.DataOd,
                    DataDo = _filtry.DataDo,
                    TowarIdHandel = _filtry.TowarIdHandel,
                    TowarIdLibra = _filtry.TowarIdLibra,
                    OdbiorcyIds = _filtry.OdbiorcyIds,
                    Handlowcy = _filtry.Handlowcy,
                    OperatorID = _filtry.OperatorID,
                    TerminalId = _filtry.TerminalId,
                    Partia = _filtry.Partia,
                    KlasaKurczaka = null,  // <-- pobieramy wszystkie klasy
                    Dostawca = _filtry.Dostawca,
                    GodzinaOd = _filtry.GodzinaOd,
                    GodzinaDo = _filtry.GodzinaDo
                };

                var raw = await _realizacja.LoadWazeniaAsync(f);
                raw = raw.Where(w => w.Klasa is >= 4 and <= 12).ToList();

                // % z partii liczone na pełnej puli (a nie tylko w obrębie wybranej klasy).
                var sumyPartii = raw
                    .Where(w => !string.IsNullOrEmpty(w.Partia))
                    .GroupBy(w => w.Partia)
                    .ToDictionary(g => g.Key, g => g.Sum(w => Math.Max(0, w.ActWeight)));

                _wszystkieRaw = raw.Select(w => new WazenieZSzczegolami(w)
                {
                    ProcentZPartii = sumyPartii.TryGetValue(w.Partia ?? "", out var sumP) && sumP > 0
                        ? Math.Max(0, w.ActWeight) / sumP * 100m
                        : 0m,
                    RoznicaProc = w.Weight > 0 ? (w.ActWeight - w.Weight) / w.Weight * 100m : 0m
                }).ToList();

                AktualizujWidok();

                stoper.Stop();
                txtCzasLad.Text = $"Załadowano {_wszystkieRaw.Count:N0} ważeń (4–12) w {stoper.Elapsed.TotalSeconds:F1}s";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd ładowania szczegółów klasy:\n" + ex.Message,
                    "Szczegóły klasy", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Filtruje pełną pulę ważeń po _wybraneKlasy i odświeża KPI, wykresy i tabelę.
        /// </summary>
        private void AktualizujWidok()
        {
            _wazenia = _wszystkieRaw.Where(w => _wybraneKlasy.Contains(w.Klasa)).ToList();

            _view = CollectionViewSource.GetDefaultView(_wazenia);
            dgWazenia.ItemsSource = _view;

            UstawHeaderTekstIKolor();
            AktualizujKpi();
            BudujHistogram();
            BudujWykresHodowcow();
            ZastosujFiltr();
        }

        private void AktualizujKpi()
        {
            if (_wazenia.Count == 0)
            {
                txtKpiWazen.Text = "0";
                txtKpiHodowcy.Text = "0";
                txtKpiPartii.Text = "0";
                txtKpiStandard.Text = "0";
                txtKpiRzeczywista.Text = "0";
                txtKpiRoznica.Text = "0";
                txtKpiRoznicaProc.Text = "—";
                return;
            }

            int liczba = _wazenia.Count(w => w.ActWeight > 0);
            int hodowcow = _wazenia.Where(w => !string.IsNullOrEmpty(w.Hodowca))
                .Select(w => w.Hodowca).Distinct().Count();
            int partii = _wazenia.Where(w => !string.IsNullOrEmpty(w.Partia))
                .Select(w => w.Partia).Distinct().Count();
            decimal sumStd = _wazenia.Sum(w => w.Weight);
            decimal sumRzecz = _wazenia.Where(w => w.ActWeight > 0).Sum(w => w.ActWeight);
            decimal roznica = sumRzecz - sumStd;
            decimal roznicaProc = sumStd > 0 ? roznica / sumStd * 100m : 0m;

            txtKpiWazen.Text = liczba.ToString("N0");
            txtKpiHodowcy.Text = hodowcow.ToString("N0");
            txtKpiPartii.Text = partii.ToString("N0");
            txtKpiStandard.Text = sumStd.ToString("N0");
            txtKpiRzeczywista.Text = sumRzecz.ToString("N0");
            txtKpiRoznica.Text = (roznica >= 0 ? "+" : "") + roznica.ToString("N0");
            txtKpiRoznicaProc.Text = (roznicaProc >= 0 ? "+" : "") + roznicaProc.ToString("N2") + "%";
            txtKpiRoznica.Foreground = new SolidColorBrush(roznica >= 0
                ? Color.FromRgb(0x05, 0x96, 0x69)
                : Color.FromRgb(0xDC, 0x26, 0x26));
        }

        private void BudujHistogram()
        {
            if (_wazenia.Count == 0)
            {
                wykresHistogram.Series = new SeriesCollection();
                osXHistogram.Labels = new List<string>();
                return;
            }

            // Histogram: zakres realnych palet 500–600 kg (filtruje anomalie typu anulacje 1 kg / 100 kg).
            // Oś X dopasowuje się dynamicznie do faktycznych danych — jeśli wszystkie ważenia są
            // np. w 540–560, pokażemy tylko 540–560 (bez pustych słupków po bokach).
            const int RANGE_MIN = 500;
            const int RANGE_MAX = 600;
            const int BIN_SIZE = 10;

            var wszystkiePozytywne = _wazenia.Where(w => w.ActWeight > 0).ToList();
            var pozytywne = wszystkiePozytywne
                .Where(w => w.ActWeight >= RANGE_MIN && w.ActWeight < RANGE_MAX)
                .ToList();
            int poza = wszystkiePozytywne.Count - pozytywne.Count;

            if (pozytywne.Count == 0)
            {
                wykresHistogram.Series = new SeriesCollection();
                osXHistogram.Labels = new List<string>();
                osXHistogram.Title = poza > 0
                    ? $"Waga (kg) — brak ważeń w zakresie {RANGE_MIN}–{RANGE_MAX} kg ({poza} poza zakresem)"
                    : "Waga (kg)";
                return;
            }

            // Dynamiczne min/max ograniczone do RANGE_MIN..RANGE_MAX, zaokrąglone do binów
            decimal minW = pozytywne.Min(w => w.ActWeight);
            decimal maxW = pozytywne.Max(w => w.ActWeight);
            int binMin = (int)(Math.Floor((double)minW / BIN_SIZE) * BIN_SIZE);
            int binMax = (int)(Math.Ceiling((double)maxW / BIN_SIZE) * BIN_SIZE);
            if (binMax <= binMin) binMax = binMin + BIN_SIZE;

            var bins = new SortedDictionary<int, int>();
            for (int b = binMin; b < binMax; b += BIN_SIZE)
                bins[b] = 0;

            foreach (var w in pozytywne)
            {
                int b = (int)(Math.Floor((double)w.ActWeight / BIN_SIZE) * BIN_SIZE);
                if (!bins.ContainsKey(b)) bins[b] = 0;
                bins[b]++;
            }

            osXHistogram.Title = poza > 0
                ? $"Waga palety (kg, {binMin}–{binMax})  •  {poza} poza {RANGE_MIN}–{RANGE_MAX} (ukryte)"
                : $"Waga palety (kg, {binMin}–{binMax})";

            int sumaWazen = bins.Values.Sum();
            var values = new ChartValues<int>(bins.Values);
            wykresHistogram.Series = new SeriesCollection
            {
                new ColumnSeries
                {
                    Title = "Liczba ważeń",
                    Values = values,
                    Fill = new SolidColorBrush(Color.FromRgb(0x7C, 0x3A, 0xED)),
                    DataLabels = true,
                    LabelPoint = p =>
                    {
                        if (p.Y <= 0) return "";
                        double proc = sumaWazen > 0 ? p.Y / (double)sumaWazen * 100.0 : 0;
                        return $"{p.Y:N0}\n({proc:N1}%)";
                    },
                    MaxColumnWidth = 80,
                    ColumnPadding = 4,
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x37))
                }
            };
            osXHistogram.Labels = bins.Keys.Select(b => $"{b}–{b + BIN_SIZE}").ToList();
            osYHistogram.LabelFormatter = v => v.ToString("N0");
        }

        private void BudujWykresHodowcow()
        {
            var topHodowcy = _wazenia
                .Where(w => !string.IsNullOrEmpty(w.Hodowca) && w.ActWeight > 0)
                .GroupBy(w => w.Hodowca)
                .Select(g => new { Hodowca = g.Key, Kg = g.Sum(w => w.ActWeight) })
                .OrderByDescending(x => x.Kg)
                .Take(10)
                .ToList();

            if (topHodowcy.Count == 0)
            {
                wykresHodowcy.Series = new SeriesCollection();
                osYHodowcy.Labels = new List<string>();
                return;
            }

            double sumaKgKlasa = (double)_wazenia.Where(w => w.ActWeight > 0).Sum(w => w.ActWeight);
            var values = new ChartValues<double>(topHodowcy.Select(t => (double)t.Kg).Reverse());
            wykresHodowcy.Series = new SeriesCollection
            {
                new RowSeries
                {
                    Title = "Suma kg",
                    Values = values,
                    Fill = new SolidColorBrush(Color.FromRgb(0x05, 0x96, 0x69)),
                    DataLabels = true,
                    LabelPoint = p =>
                    {
                        double proc = sumaKgKlasa > 0 ? p.X / sumaKgKlasa * 100.0 : 0;
                        return $"{p.X:N0} kg ({proc:N1}%)";
                    },
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x06, 0x4E, 0x3B))
                }
            };
            osYHodowcy.Labels = topHodowcy.Select(t => t.Hodowca).Reverse().ToList();
            osXHodowcy.LabelFormatter = v => v.ToString("N0");
        }

        // ─── Wybór klas (Eksplorator-style) ───────────────────────────────────
        // Klik = tylko ta klasa (reszta odznaczona). Ctrl+klik = toggle pojedynczej do/z selekcji.

        private void KlasaBtn_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not ToggleButton tb) return;
            if (tb.Tag is not string tag || !int.TryParse(tag, out int klasa)) return;

            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

            if (ctrl)
            {
                // Ctrl+klik: toggle pojedynczej klasy w selekcji
                if (_wybraneKlasy.Contains(klasa)) _wybraneKlasy.Remove(klasa);
                else _wybraneKlasy.Add(klasa);
            }
            else
            {
                // Klik bez Ctrl: ekskluzywny wybór tylko tej klasy
                _wybraneKlasy.Clear();
                _wybraneKlasy.Add(klasa);
            }

            // Bezpiecznik: zero klas → wracaj do startowej (nigdy nie pokazujemy pustego okna)
            if (_wybraneKlasy.Count == 0)
                _wybraneKlasy.Add(_klasa.Klasa);

            UstawTogglePoczatkowe();
            AktualizujWidok();
            e.Handled = true;  // Zablokuj domyślny toggle ToggleButton, sami sterujemy IsChecked
        }

        private void UstawWybor(IEnumerable<int> klasy)
        {
            _wybraneKlasy.Clear();
            foreach (var k in klasy) _wybraneKlasy.Add(k);
            UstawTogglePoczatkowe();
            AktualizujWidok();
        }

        private void BtnGrupaDuzy_Click(object sender, RoutedEventArgs e)
            => UstawWybor(new[] { 4, 5, 6, 7 });

        private void BtnGrupaMaly_Click(object sender, RoutedEventArgs e)
            => UstawWybor(new[] { 8, 9, 10, 11, 12 });

        private void BtnGrupaWszystkie_Click(object sender, RoutedEventArgs e)
            => UstawWybor(new[] { 4, 5, 6, 7, 8, 9, 10, 11, 12 });

        private void BtnGrupaReset_Click(object sender, RoutedEventArgs e)
            => UstawWybor(new[] { _klasa.Klasa });

        // ─── Quick filter / eksport / zamknij ─────────────────────────────────

        private void TxtFiltr_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtFiltrPlaceholder != null)
                txtFiltrPlaceholder.Visibility = string.IsNullOrEmpty(txtFiltr.Text)
                    ? Visibility.Visible : Visibility.Collapsed;
            ZastosujFiltr();
        }

        private void ZastosujFiltr()
        {
            if (_view == null) return;
            string txt = (txtFiltr?.Text ?? "").Trim().ToLowerInvariant();
            _view.Filter = string.IsNullOrEmpty(txt) ? null : obj =>
                obj is WazenieZSzczegolami w
                    && ((w.Hodowca ?? "").ToLowerInvariant().Contains(txt)
                     || (w.Partia ?? "").ToLowerInvariant().Contains(txt)
                     || (w.Wagowy ?? "").ToLowerInvariant().Contains(txt)
                     || (w.OperatorID ?? "").ToLowerInvariant().Contains(txt));

            int pokazane = _view.Cast<object>().Count();
            txtLicznik.Text = pokazane == _wazenia.Count
                ? $"{pokazane:N0} ważeń"
                : $"{pokazane:N0} z {_wazenia.Count:N0} ważeń";
        }

        private void BtnWyczyscFiltr_Click(object sender, RoutedEventArgs e)
        {
            if (txtFiltr != null) txtFiltr.Text = "";
            ZastosujFiltr();
        }

        private void BtnEksport_Click(object sender, RoutedEventArgs e)
        {
            string sufiks = _wybraneKlasy.Count == 1
                ? $"klasa_{_wybraneKlasy.First()}"
                : $"klasy_{string.Join("-", _wybraneKlasy.OrderBy(x => x))}";

            CsvExporter.Eksportuj(_wazenia, sufiks + "_wazenia",
                new[] { nameof(WazenieZSzczegolami.Data), nameof(WazenieZSzczegolami.Godzina),
                        nameof(WazenieZSzczegolami.Hodowca), nameof(WazenieZSzczegolami.Partia),
                        nameof(WazenieZSzczegolami.Wagowy), nameof(WazenieZSzczegolami.Klasa),
                        nameof(WazenieZSzczegolami.Weight), nameof(WazenieZSzczegolami.ActWeight),
                        nameof(WazenieZSzczegolami.Roznica), nameof(WazenieZSzczegolami.RoznicaProc),
                        nameof(WazenieZSzczegolami.ProcentZPartii), nameof(WazenieZSzczegolami.Tara) },
                this);
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e) => Close();
    }

    /// <summary>
    /// Rozszerzenie WazenieRekord o dodatkowe pola wyliczane: % z partii, % różnicy.
    /// </summary>
    public class WazenieZSzczegolami : WazenieRekord
    {
        public decimal ProcentZPartii { get; set; }
        public decimal RoznicaProc { get; set; }

        public WazenieZSzczegolami(WazenieRekord src)
        {
            Data = src.Data;
            Godzina = src.Godzina;
            ArticleID = src.ArticleID;
            NazwaTowaru = src.NazwaTowaru;
            OperatorID = src.OperatorID;
            Wagowy = src.Wagowy;
            TermID = src.TermID;
            Terminal = src.Terminal;
            Partia = src.Partia;
            Weight = src.Weight;
            ActWeight = src.ActWeight;
            Tara = src.Tara;
            Klasa = src.Klasa;
            Hodowca = src.Hodowca;
            CustomerID = src.CustomerID;
        }
    }

    /// <summary>
    /// Wartość → 1 (dodatnia), -1 (ujemna), 0 (zero) — dla DataTrigger w kolumnie Δ kg.
    /// </summary>
    public class ZnakConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal d) return d > 0 ? 1 : (d < 0 ? -1 : 0);
            if (value is double db) return db > 0 ? 1 : (db < 0 ? -1 : 0);
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
