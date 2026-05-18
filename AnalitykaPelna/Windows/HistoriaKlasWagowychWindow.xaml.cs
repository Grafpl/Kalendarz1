using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Kalendarz1.AnalitykaPelna.Models;
using Kalendarz1.AnalitykaPelna.Services;
using LiveCharts;
using LiveCharts.Wpf;

namespace Kalendarz1.AnalitykaPelna.Windows
{
    public partial class HistoriaKlasWagowychWindow : Window
    {
        private readonly FiltryAnaliz _filtry;
        private readonly WydajnoscService _service;

        private List<HistoriaKlasPunkt> _surowePunkty = new();
        private List<HistoriaKlasOkres> _okresy = new();

        private OkresAgregacji _okres = OkresAgregacji.Tygodniowa;
        private HistoriaTryb _tryb = HistoriaTryb.PerKlasa;
        private HistoriaMetryka _metryka = HistoriaMetryka.Kg;

        // Per-klasa: które klasy są zaznaczone (4..12)
        private readonly HashSet<int> _wybraneKlasy = new(Enumerable.Range(4, 9));

        // Per-grupa: Duzy/Maly/Razem (string tag z ToggleButton)
        private readonly HashSet<string> _wybraneGrupy = new() { "Duzy", "Maly" };

        private bool _toggleSuppress;

        // Kolory dla 9 klas — odróżnialne dla wykresu liniowego
        private static readonly Color[] KOLORY_KLAS = new[]
        {
            Color.FromRgb(0x1E, 0x3A, 0x8A), // 4 — granat
            Color.FromRgb(0x25, 0x63, 0xEB), // 5 — niebieski
            Color.FromRgb(0x3B, 0x82, 0xF6), // 6 — jasny niebieski
            Color.FromRgb(0x06, 0xB6, 0xD4), // 7 — turkus
            Color.FromRgb(0x10, 0xB9, 0x81), // 8 — zielony
            Color.FromRgb(0xF5, 0x9E, 0x0B), // 9 — pomarańczowy
            Color.FromRgb(0xF9, 0x73, 0x16), // 10 — pomarańcz mocny
            Color.FromRgb(0xDC, 0x26, 0x26), // 11 — czerwony
            Color.FromRgb(0x9A, 0x34, 0x12), // 12 — bordowy
        };

        public HistoriaKlasWagowychWindow(FiltryAnaliz filtry)
        {
            InitializeComponent();
            _filtry = filtry;
            _service = new WydajnoscService();

            KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };

            txtHeaderPodtytul.Text =
                $"Wykres liniowy + tabela: ile sztuk/kg każdej klasy w wybranych okresach  •  " +
                $"Zakres: {_filtry.DataOd:dd.MM.yyyy} – {_filtry.DataDo:dd.MM.yyyy}";

            UstawTogglePoczatkowe();
            Loaded += async (_, _) => await ZaladujAsync();
        }

        // ─── Inicjalizacja toggle buttonów ────────────────────────────────────

        private void UstawTogglePoczatkowe()
        {
            _toggleSuppress = true;
            try
            {
                foreach (var (kl, btn) in WszystkieKlasyToggle())
                    btn.IsChecked = _wybraneKlasy.Contains(kl);
                gbtnDuzy.IsChecked = _wybraneGrupy.Contains("Duzy");
                gbtnMaly.IsChecked = _wybraneGrupy.Contains("Maly");
                gbtnRazem.IsChecked = _wybraneGrupy.Contains("Razem");
            }
            finally { _toggleSuppress = false; }
        }

        private IEnumerable<(int klasa, ToggleButton btn)> WszystkieKlasyToggle()
        {
            yield return (4, kbtn4); yield return (5, kbtn5); yield return (6, kbtn6);
            yield return (7, kbtn7); yield return (8, kbtn8); yield return (9, kbtn9);
            yield return (10, kbtn10); yield return (11, kbtn11); yield return (12, kbtn12);
        }

        // ─── Ładowanie danych ─────────────────────────────────────────────────

        private async Task ZaladujAsync()
        {
            var stoper = Stopwatch.StartNew();
            try
            {
                _surowePunkty = await _service.LoadHistoriaKlasAsync(_filtry);
                Przelicz();
                stoper.Stop();
                txtCzasLad.Text = $"Załadowano {_surowePunkty.Count:N0} punktów w {stoper.Elapsed.TotalSeconds:F1}s";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd ładowania historii klas:\n" + ex.Message,
                    "Historia klas wagowych", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>Pełny rerender — agreguje, KPI, wykres i tabelę. Wywołuj po każdej zmianie opcji.</summary>
        private void Przelicz()
        {
            _okresy = WydajnoscService.AgregujHistorie(_surowePunkty, _okres);
            AktualizujKpi();
            BudujWykres();
            BudujTabele();
        }

        // ─── KPI ──────────────────────────────────────────────────────────────

        private void AktualizujKpi()
        {
            txtKpiOkresow.Text = _okresy.Count.ToString("N0");

            int sumWazen = _okresy.Sum(o => o.SumaWazen);
            decimal sumKg = _okresy.Sum(o => o.SumaKg);
            decimal sumDuzy = _okresy.Sum(o => o.KgGrupa(new[] { 4, 5, 6, 7 }));
            decimal sumMaly = _okresy.Sum(o => o.KgGrupa(new[] { 8, 9, 10, 11, 12 }));

            txtKpiWazen.Text = sumWazen.ToString("N0");
            txtKpiKg.Text = sumKg.ToString("N0");
            txtKpiDuzy.Text = sumDuzy.ToString("N0");
            txtKpiDuzyProc.Text = sumKg > 0 ? $"{sumDuzy / sumKg * 100m:N1}%" : "—";
            txtKpiMaly.Text = sumMaly.ToString("N0");
            txtKpiMalyProc.Text = sumKg > 0 ? $"{sumMaly / sumKg * 100m:N1}%" : "—";

            // Top klasa
            var topKlasa = Enumerable.Range(4, 9)
                .Select(k => new { Klasa = k, Kg = _okresy.Sum(o => o.KgPerKlasa.TryGetValue(k, out var v) ? v : 0m) })
                .OrderByDescending(x => x.Kg)
                .FirstOrDefault();
            if (topKlasa != null && topKlasa.Kg > 0)
            {
                txtKpiTopKlasa.Text = $"Klasa {topKlasa.Klasa}";
                txtKpiTopKlasaKg.Text = $"{topKlasa.Kg:N0} kg • {topKlasa.Kg / sumKg * 100m:N1}%";
            }
            else
            {
                txtKpiTopKlasa.Text = "—";
                txtKpiTopKlasaKg.Text = "";
            }
        }

        // ─── Wykres ───────────────────────────────────────────────────────────

        private void BudujWykres()
        {
            if (_okresy.Count == 0)
            {
                wykresHistoria.Series = new SeriesCollection();
                osXHistoria.Labels = new List<string>();
                return;
            }

            string jednostka = _metryka == HistoriaMetryka.Kg ? "kg" : "szt.";
            osYHistoria.Title = _metryka == HistoriaMetryka.Kg ? "Suma kg" : "Liczba ważeń";
            osYHistoria.LabelFormatter = v => v.ToString("N0");
            osXHistoria.Title = OkresTitleX();
            osXHistoria.Labels = _okresy.Select(o => o.EtykietaKrotka).ToList();

            var serie = new SeriesCollection();

            if (_tryb == HistoriaTryb.PerKlasa)
            {
                foreach (int klasa in _wybraneKlasy.OrderBy(k => k))
                {
                    var values = new ChartValues<double>(_okresy.Select(o =>
                        WartoscDlaKlasy(o, klasa)));

                    var color = KOLORY_KLAS[klasa - 4];
                    serie.Add(new LineSeries
                    {
                        Title = $"Klasa {klasa}",
                        Values = values,
                        Stroke = new SolidColorBrush(color),
                        StrokeThickness = 2.5,
                        Fill = Brushes.Transparent,
                        PointGeometry = LiveCharts.Wpf.DefaultGeometries.Circle,
                        PointGeometrySize = 9,
                        PointForeground = new SolidColorBrush(color),
                        LineSmoothness = 0.25,
                        DataLabels = false,
                        FontSize = 10,
                        Foreground = new SolidColorBrush(color)
                    });
                }
            }
            else // PerGrupa
            {
                if (_wybraneGrupy.Contains("Duzy"))
                    serie.Add(SeriaDlaGrupy("🍗 Duży kurczak (4–7)",
                        new[] { 4, 5, 6, 7 },
                        Color.FromRgb(0x25, 0x63, 0xEB), Color.FromArgb(0x33, 0x25, 0x63, 0xEB)));

                if (_wybraneGrupy.Contains("Maly"))
                    serie.Add(SeriaDlaGrupy("🐥 Mały kurczak (8–12)",
                        new[] { 8, 9, 10, 11, 12 },
                        Color.FromRgb(0xF9, 0x73, 0x16), Color.FromArgb(0x33, 0xF9, 0x73, 0x16)));

                if (_wybraneGrupy.Contains("Razem"))
                    serie.Add(SeriaDlaGrupy("📊 Razem (4–12)",
                        Enumerable.Range(4, 9),
                        Color.FromRgb(0x7C, 0x3A, 0xED), Color.FromArgb(0x22, 0x7C, 0x3A, 0xED)));
            }

            wykresHistoria.Series = serie;
        }

        private double WartoscDlaKlasy(HistoriaKlasOkres o, int klasa)
        {
            if (_metryka == HistoriaMetryka.Kg)
                return o.KgPerKlasa.TryGetValue(klasa, out var v) ? (double)v : 0.0;
            return o.WazeniaPerKlasa.TryGetValue(klasa, out var w) ? w : 0.0;
        }

        private double WartoscDlaGrupy(HistoriaKlasOkres o, IEnumerable<int> klasy)
        {
            if (_metryka == HistoriaMetryka.Kg)
                return (double)o.KgGrupa(klasy);
            return o.WazeniaGrupa(klasy);
        }

        private LineSeries SeriaDlaGrupy(string tytul, IEnumerable<int> klasy, Color stroke, Color fill)
        {
            var klasyList = klasy.ToList();
            var values = new ChartValues<double>(_okresy.Select(o => WartoscDlaGrupy(o, klasyList)));
            return new LineSeries
            {
                Title = tytul,
                Values = values,
                Stroke = new SolidColorBrush(stroke),
                StrokeThickness = 3,
                Fill = new SolidColorBrush(fill),
                PointGeometry = LiveCharts.Wpf.DefaultGeometries.Circle,
                PointGeometrySize = 11,
                PointForeground = new SolidColorBrush(stroke),
                LineSmoothness = 0.25,
                DataLabels = true,
                LabelPoint = p => p.Y > 0 ? p.Y.ToString("N0") : "",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(stroke)
            };
        }

        private string OkresTitleX() => _okres switch
        {
            OkresAgregacji.Dzienna => "Dzień",
            OkresAgregacji.Tygodniowa => "Tydzień (numer ISO)",
            OkresAgregacji.Miesieczna => "Miesiąc",
            OkresAgregacji.Kwartalna => "Kwartał",
            OkresAgregacji.Roczna => "Rok",
            _ => "Okres"
        };

        // ─── Tabela ───────────────────────────────────────────────────────────

        private void BudujTabele()
        {
            var dt = new DataTable();
            dt.Columns.Add("Okres", typeof(string));
            dt.Columns.Add("Od", typeof(string));
            dt.Columns.Add("Do", typeof(string));

            if (_tryb == HistoriaTryb.PerKlasa)
            {
                foreach (int klasa in _wybraneKlasy.OrderBy(k => k))
                    dt.Columns.Add($"K{klasa}", typeof(string));
            }
            else
            {
                if (_wybraneGrupy.Contains("Duzy")) dt.Columns.Add("Duży (4-7)", typeof(string));
                if (_wybraneGrupy.Contains("Maly")) dt.Columns.Add("Mały (8-12)", typeof(string));
                if (_wybraneGrupy.Contains("Razem")) dt.Columns.Add("Razem (4-12)", typeof(string));
            }
            dt.Columns.Add("Σ Okres", typeof(string));

            string fmt = _metryka == HistoriaMetryka.Kg ? "N0" : "N0";

            foreach (var o in _okresy)
            {
                var row = dt.NewRow();
                row["Okres"] = o.Etykieta;
                row["Od"] = o.DataOd.ToString("dd.MM.yyyy");
                row["Do"] = o.DataDo.ToString("dd.MM.yyyy");

                double sumOkres = 0;
                if (_tryb == HistoriaTryb.PerKlasa)
                {
                    foreach (int klasa in _wybraneKlasy.OrderBy(k => k))
                    {
                        double w = WartoscDlaKlasy(o, klasa);
                        row[$"K{klasa}"] = w > 0 ? w.ToString(fmt) : "—";
                        sumOkres += w;
                    }
                }
                else
                {
                    if (_wybraneGrupy.Contains("Duzy"))
                    {
                        double w = WartoscDlaGrupy(o, new[] { 4, 5, 6, 7 });
                        row["Duży (4-7)"] = w > 0 ? w.ToString(fmt) : "—";
                        sumOkres += w;
                    }
                    if (_wybraneGrupy.Contains("Maly"))
                    {
                        double w = WartoscDlaGrupy(o, new[] { 8, 9, 10, 11, 12 });
                        row["Mały (8-12)"] = w > 0 ? w.ToString(fmt) : "—";
                        sumOkres += w;
                    }
                    if (_wybraneGrupy.Contains("Razem"))
                    {
                        double w = WartoscDlaGrupy(o, Enumerable.Range(4, 9));
                        row["Razem (4-12)"] = w > 0 ? w.ToString(fmt) : "—";
                        // Nie dodajemy do sumOkres — to byłoby double-counting
                    }
                }

                row["Σ Okres"] = sumOkres > 0 ? sumOkres.ToString(fmt) : "—";
                dt.Rows.Add(row);
            }

            // Wiersz Σ Razem (suma wszystkich okresów)
            if (_okresy.Count > 0)
            {
                var sumRow = dt.NewRow();
                sumRow["Okres"] = "Σ RAZEM";
                sumRow["Od"] = _okresy.First().DataOd.ToString("dd.MM.yyyy");
                sumRow["Do"] = _okresy.Last().DataDo.ToString("dd.MM.yyyy");
                double total = 0;
                if (_tryb == HistoriaTryb.PerKlasa)
                {
                    foreach (int klasa in _wybraneKlasy.OrderBy(k => k))
                    {
                        double s = _okresy.Sum(o => WartoscDlaKlasy(o, klasa));
                        sumRow[$"K{klasa}"] = s.ToString(fmt);
                        total += s;
                    }
                }
                else
                {
                    if (_wybraneGrupy.Contains("Duzy"))
                    {
                        double s = _okresy.Sum(o => WartoscDlaGrupy(o, new[] { 4, 5, 6, 7 }));
                        sumRow["Duży (4-7)"] = s.ToString(fmt);
                        total += s;
                    }
                    if (_wybraneGrupy.Contains("Maly"))
                    {
                        double s = _okresy.Sum(o => WartoscDlaGrupy(o, new[] { 8, 9, 10, 11, 12 }));
                        sumRow["Mały (8-12)"] = s.ToString(fmt);
                        total += s;
                    }
                    if (_wybraneGrupy.Contains("Razem"))
                    {
                        double s = _okresy.Sum(o => WartoscDlaGrupy(o, Enumerable.Range(4, 9)));
                        sumRow["Razem (4-12)"] = s.ToString(fmt);
                    }
                }
                sumRow["Σ Okres"] = total.ToString(fmt);
                dt.Rows.Add(sumRow);
            }

            dgHistoria.ItemsSource = dt.DefaultView;
        }

        private void DgHistoria_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            // Wyrównanie do prawej dla kolumn liczbowych (wszystko poza Okres/Od/Do)
            if (e.PropertyName != "Okres" && e.PropertyName != "Od" && e.PropertyName != "Do"
                && e.Column is DataGridTextColumn tc)
            {
                tc.ElementStyle = NowyStylKolumnyLiczbowej();
            }
        }

        private static Style NowyStylKolumnyLiczbowej()
        {
            var s = new Style(typeof(TextBlock));
            s.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            s.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(0, 0, 8, 0)));
            s.Setters.Add(new Setter(TextBlock.FontFamilyProperty, new FontFamily("Consolas")));
            return s;
        }

        // ─── Toggle agregacji / trybu / metryki ───────────────────────────────

        private void OkresBtn_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_toggleSuppress) return;
            if (sender is not ToggleButton tb || tb.Tag is not string tag) return;
            if (!Enum.TryParse<OkresAgregacji>(tag, out var ag)) return;
            _okres = ag;
            _toggleSuppress = true;
            try
            {
                tbDzien.IsChecked = ag == OkresAgregacji.Dzienna;
                tbTydzien.IsChecked = ag == OkresAgregacji.Tygodniowa;
                tbMiesiac.IsChecked = ag == OkresAgregacji.Miesieczna;
                tbKwartal.IsChecked = ag == OkresAgregacji.Kwartalna;
                tbRok.IsChecked = ag == OkresAgregacji.Roczna;
            }
            finally { _toggleSuppress = false; }
            Przelicz();
            e.Handled = true;
        }

        private void TrybBtn_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_toggleSuppress) return;
            if (sender is not ToggleButton tb || tb.Tag is not string tag) return;
            if (!Enum.TryParse<HistoriaTryb>(tag, out var tr)) return;
            _tryb = tr;
            _toggleSuppress = true;
            try
            {
                tbPerKlasa.IsChecked = tr == HistoriaTryb.PerKlasa;
                tbPerGrupa.IsChecked = tr == HistoriaTryb.PerGrupa;

                bool perKlasa = tr == HistoriaTryb.PerKlasa;
                panelPerKlasa.Visibility = perKlasa ? Visibility.Visible : Visibility.Collapsed;
                panelPerGrupa.Visibility = perKlasa ? Visibility.Collapsed : Visibility.Visible;
                txtNagOpcji.Text = perKlasa ? "🎯 Klasy:" : "🎯 Grupy:";
            }
            finally { _toggleSuppress = false; }
            Przelicz();
            e.Handled = true;
        }

        private void MetrykaBtn_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_toggleSuppress) return;
            if (sender is not ToggleButton tb || tb.Tag is not string tag) return;
            if (!Enum.TryParse<HistoriaMetryka>(tag, out var m)) return;
            _metryka = m;
            _toggleSuppress = true;
            try
            {
                tbKg.IsChecked = m == HistoriaMetryka.Kg;
                tbWazenia.IsChecked = m == HistoriaMetryka.Wazenia;
            }
            finally { _toggleSuppress = false; }
            Przelicz();
            e.Handled = true;
        }

        // ─── Eksplorator-style multi-select klas ──────────────────────────────

        private void KlasaBtn_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not ToggleButton tb || tb.Tag is not string tag) return;
            if (!int.TryParse(tag, out int klasa)) return;

            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            if (ctrl)
            {
                if (_wybraneKlasy.Contains(klasa)) _wybraneKlasy.Remove(klasa);
                else _wybraneKlasy.Add(klasa);
            }
            else
            {
                _wybraneKlasy.Clear();
                _wybraneKlasy.Add(klasa);
            }
            if (_wybraneKlasy.Count == 0) _wybraneKlasy.UnionWith(Enumerable.Range(4, 9));

            UstawTogglePoczatkowe();
            Przelicz();
            e.Handled = true;
        }

        private void GrupaBtn_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not ToggleButton tb || tb.Tag is not string tag) return;
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            if (ctrl)
            {
                if (_wybraneGrupy.Contains(tag)) _wybraneGrupy.Remove(tag);
                else _wybraneGrupy.Add(tag);
            }
            else
            {
                _wybraneGrupy.Clear();
                _wybraneGrupy.Add(tag);
            }
            if (_wybraneGrupy.Count == 0)
            {
                _wybraneGrupy.Add("Duzy");
                _wybraneGrupy.Add("Maly");
            }
            UstawTogglePoczatkowe();
            Przelicz();
            e.Handled = true;
        }

        private void BtnGrupaDuzy_Click(object sender, RoutedEventArgs e)
        {
            _wybraneKlasy.Clear();
            _wybraneKlasy.UnionWith(new[] { 4, 5, 6, 7 });
            UstawTogglePoczatkowe();
            Przelicz();
        }

        private void BtnGrupaMaly_Click(object sender, RoutedEventArgs e)
        {
            _wybraneKlasy.Clear();
            _wybraneKlasy.UnionWith(new[] { 8, 9, 10, 11, 12 });
            UstawTogglePoczatkowe();
            Przelicz();
        }

        private void BtnGrupaWszystkie_Click(object sender, RoutedEventArgs e)
        {
            _wybraneKlasy.Clear();
            _wybraneKlasy.UnionWith(Enumerable.Range(4, 9));
            UstawTogglePoczatkowe();
            Przelicz();
        }

        // ─── Eksport / zamknij ────────────────────────────────────────────────

        private void BtnEksport_Click(object sender, RoutedEventArgs e)
        {
            if (_okresy.Count == 0)
            {
                MessageBox.Show("Brak danych do eksportu.", "Eksport CSV",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Eksport tabelaryczny: per-okres × per-klasa lub per-grupa
            var rekordy = _okresy.Select(o =>
            {
                var d = new System.Dynamic.ExpandoObject() as IDictionary<string, object>;
                d["Okres"] = o.Etykieta;
                d["Od"] = o.DataOd.ToString("yyyy-MM-dd");
                d["Do"] = o.DataDo.ToString("yyyy-MM-dd");
                if (_tryb == HistoriaTryb.PerKlasa)
                {
                    foreach (int k in _wybraneKlasy.OrderBy(x => x))
                        d[$"K{k}_{(_metryka == HistoriaMetryka.Kg ? "kg" : "szt")}"] = WartoscDlaKlasy(o, k);
                }
                else
                {
                    if (_wybraneGrupy.Contains("Duzy")) d["Duzy_4_7"] = WartoscDlaGrupy(o, new[] { 4, 5, 6, 7 });
                    if (_wybraneGrupy.Contains("Maly")) d["Maly_8_12"] = WartoscDlaGrupy(o, new[] { 8, 9, 10, 11, 12 });
                    if (_wybraneGrupy.Contains("Razem")) d["Razem_4_12"] = WartoscDlaGrupy(o, Enumerable.Range(4, 9));
                }
                return d;
            }).Cast<object>().ToList();

            // Prosty CSV (pomijamy CsvExporter — ten działa na refleksji typu, a nasze rekordy to ExpandoObject)
            var sb = new System.Text.StringBuilder();
            if (rekordy.Count > 0 && rekordy[0] is IDictionary<string, object> first)
            {
                sb.AppendLine(string.Join(";", first.Keys));
                foreach (var r in rekordy.Cast<IDictionary<string, object>>())
                    sb.AppendLine(string.Join(";", r.Values.Select(v =>
                        v is double d ? d.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)
                        : v?.ToString() ?? "")));
            }

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"Historia_klas_{_okres}_{_tryb}_{_metryka}_{DateTime.Now:yyyy-MM-dd_HHmm}.csv",
                Filter = "Plik CSV (*.csv)|*.csv",
                DefaultExt = "csv"
            };
            if (dlg.ShowDialog(this) != true) return;
            System.IO.File.WriteAllText(dlg.FileName, sb.ToString(),
                new System.Text.UTF8Encoding(true));
            MessageBox.Show($"Zapisano {rekordy.Count} okresów do:\n{dlg.FileName}",
                "Eksport CSV", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e) => Close();
    }
}
