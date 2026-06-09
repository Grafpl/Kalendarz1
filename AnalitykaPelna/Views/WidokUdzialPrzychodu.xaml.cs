using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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
using Microsoft.Win32;

namespace Kalendarz1.AnalitykaPelna.Views
{
    public partial class WidokUdzialPrzychodu : UserControl
    {
        private readonly UdzialPrzychoduService _service = new();
        private readonly ObservableCollection<TowarPickerItem> _wszystkieTowary = new();
        private readonly List<PrzychodPerOkresDay> _surowe = new();
        private UdzialPrzychoduDataSet _data = new();
        private FiltryAnaliz? _ostatnieFiltry;

        private GranulacjaCzasu _granulacja = GranulacjaCzasu.Dzien;
        private bool _metrykaProcent = true;
        private bool _przelicznikSilent;

        public WidokUdzialPrzychodu()
        {
            InitializeComponent();
        }

        public async Task ZastosujFiltryAsync(FiltryAnaliz f)
        {
            _ostatnieFiltry = f;
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                // 1) Lista towarów dla pickera (z sumami przychodu w okresie — do sortowania)
                var zachowajZazn = _wszystkieTowary
                    .Where(t => t.Zaznaczony)
                    .Select(t => t.IdHandel)
                    .ToHashSet();

                var towary = await _service.LoadTowaryAsync(f.DataOd, f.DataDo);
                foreach (var t in towary)
                {
                    if (zachowajZazn.Contains(t.IdHandel))
                        t.Zaznaczony = true;
                }

                _wszystkieTowary.Clear();
                foreach (var t in towary) _wszystkieTowary.Add(t);
                Filtruj();

                // 2) Surowe per-day przychody
                _surowe.Clear();
                _surowe.AddRange(await _service.LoadPrzychodPerDayAsync(f.DataOd, f.DataDo));

                Przelicz();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd ładowania udziału przychodu: " + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void Przelicz()
        {
            if (_ostatnieFiltry == null) return;

            var zaznaczone = _wszystkieTowary.Where(t => t.Zaznaczony).ToList();
            _data = _service.Agreguj(_surowe, zaznaczone,
                _ostatnieFiltry.DataOd, _ostatnieFiltry.DataDo, _granulacja);

            OdswiezKpi();
            OdswiezWykres();
            OdswiezTabele();
            OdswiezStopke();
        }

        // ─── KPI ──────────────────────────────────────────────────────────────

        private void OdswiezKpi()
        {
            kpiPrzychodCalk.Text = FormatPLN(_data.SumaCalkowitaPLN);
            kpiPrzychodZazn.Text = FormatPLN(_data.SumaZaznaczonychPLN);
            kpiUdzialZazn.Text = _data.SumaCalkowitaPLN > 0
                ? _data.UdzialZaznaczonychProc.ToString("F1", CultureInfo.InvariantCulture) + " %"
                : "—";
            kpiLiczbaTowarow.Text = _data.Serie.Count.ToString();
        }

        // ─── Wykres ───────────────────────────────────────────────────────────

        private void OdswiezWykres()
        {
            if (_data.Serie.Count == 0 || _data.OsCzasu.Count == 0)
            {
                wykres.Series = new SeriesCollection();
                osX.Labels = new List<string>();
                emptyState.Visibility = Visibility.Visible;
                emptyStateText.Text = _wszystkieTowary.Count == 0
                    ? "Brak danych w wybranym zakresie. Sprawdź daty i kliknij Zastosuj."
                    : "Zaznacz towary po lewej, żeby zobaczyć wykres.";
                return;
            }

            emptyState.Visibility = Visibility.Collapsed;

            osX.Labels = _data.OsCzasu.Select(o => o.EtykietaKrotka).ToList();
            osX.Title = _granulacja switch
            {
                GranulacjaCzasu.Dzien => "Dzień",
                GranulacjaCzasu.Tydzien => "Tydzień",
                GranulacjaCzasu.Miesiac => "Miesiąc",
                _ => "Okres"
            };

            if (_metrykaProcent)
            {
                osY.Title = "% udział w przychodzie";
                osY.LabelFormatter = v => v.ToString("F1") + " %";
            }
            else
            {
                osY.Title = "Przychód [zł]";
                osY.LabelFormatter = v => v.ToString("N0", CultureInfo.GetCultureInfo("pl-PL"));
            }

            var serie = new SeriesCollection();
            foreach (var s in _data.Serie)
            {
                var values = new ChartValues<double>(_data.OsCzasu.Select(o =>
                {
                    if (_metrykaProcent)
                        return (double)s.UdzialProcPerOkres.GetValueOrDefault(o.PoczatekOkresu);
                    return (double)s.WartoscPerOkres.GetValueOrDefault(o.PoczatekOkresu);
                }));

                Color color;
                try { color = (Color)ColorConverter.ConvertFromString(s.Kolor)!; }
                catch { color = Colors.SlateGray; }

                serie.Add(new LineSeries
                {
                    Title = string.IsNullOrWhiteSpace(s.Kod) ? s.Nazwa : s.Kod,
                    Values = values,
                    Stroke = new SolidColorBrush(color),
                    StrokeThickness = 2.5,
                    Fill = Brushes.Transparent,
                    PointGeometry = DefaultGeometries.Circle,
                    PointGeometrySize = 8,
                    PointForeground = new SolidColorBrush(color),
                    LineSmoothness = 0.25,
                    DataLabels = false,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(color)
                });
            }
            wykres.Series = serie;
        }

        // ─── Tabela ───────────────────────────────────────────────────────────

        private void OdswiezTabele()
        {
            dgTabela.ItemsSource = _data.Serie
                .OrderByDescending(s => s.SumaWartosci)
                .ToList();
        }

        // ─── Stopka ───────────────────────────────────────────────────────────

        private void OdswiezStopke()
        {
            if (_ostatnieFiltry == null) return;
            int dni = (int)(_ostatnieFiltry.DataDo - _ostatnieFiltry.DataOd).TotalDays + 1;
            txtStopka.Text =
                $"Okres: {_ostatnieFiltry.DataOd:dd.MM.yyyy} – {_ostatnieFiltry.DataDo:dd.MM.yyyy} ({dni} dni) • "
                + $"Towary z przychodem: {_wszystkieTowary.Count} • "
                + $"Wybrane: {_data.Serie.Count} • "
                + $"Granulacja: {_granulacja} • "
                + "Źródło: HANDEL Sage (FVS/FVR/FVZ)";
        }

        // ─── Picker: zaznaczanie / filtrowanie ────────────────────────────────

        private void Towar_Toggled(object sender, RoutedEventArgs e)
        {
            if (_przelicznikSilent) return;
            Przelicz();
        }

        private void Filtruj()
        {
            string fraza = (txtSzukaj.Text ?? "").Trim();
            txtSzukajPlaceholder.Visibility = string.IsNullOrEmpty(fraza)
                ? Visibility.Visible : Visibility.Collapsed;
            btnSzukajClear.Visibility = string.IsNullOrEmpty(fraza)
                ? Visibility.Collapsed : Visibility.Visible;

            IEnumerable<TowarPickerItem> widok = _wszystkieTowary;
            if (!string.IsNullOrEmpty(fraza))
            {
                widok = _wszystkieTowary.Where(t =>
                    (t.Kod ?? "").IndexOf(fraza, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (t.Nazwa ?? "").IndexOf(fraza, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            var lista = widok.ToList();
            lstTowary.ItemsSource = lista;
            txtLicznikTowarow.Text = $"{lista.Count} / {_wszystkieTowary.Count} towarów · "
                + $"zaznaczonych: {_wszystkieTowary.Count(t => t.Zaznaczony)}";
        }

        private void TxtSzukaj_TextChanged(object sender, TextChangedEventArgs e) => Filtruj();

        private void BtnSzukajClear_Click(object sender, RoutedEventArgs e)
        {
            txtSzukaj.Text = "";
            Filtruj();
        }

        private void BtnTopN_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string tag || !int.TryParse(tag, out int n)) return;

            _przelicznikSilent = true;
            try
            {
                // Top N po sumie przychodu (lista już posortowana od największej w LoadTowaryAsync)
                int i = 0;
                foreach (var t in _wszystkieTowary)
                {
                    t.Zaznaczony = i < n;
                    i++;
                }
            }
            finally { _przelicznikSilent = false; }

            Filtruj();
            Przelicz();
        }

        private void BtnWyczyscWybor_Click(object sender, RoutedEventArgs e)
        {
            _przelicznikSilent = true;
            try
            {
                foreach (var t in _wszystkieTowary) t.Zaznaczony = false;
            }
            finally { _przelicznikSilent = false; }
            Filtruj();
            Przelicz();
        }

        // ─── Granulacja + metryka ─────────────────────────────────────────────

        private void Granulacja_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton tb) return;

            // Wymuszamy wyłączność (radio behavior)
            _przelicznikSilent = true;
            try
            {
                tbDzien.IsChecked = ReferenceEquals(tb, tbDzien);
                tbTydzien.IsChecked = ReferenceEquals(tb, tbTydzien);
                tbMiesiac.IsChecked = ReferenceEquals(tb, tbMiesiac);
            }
            finally { _przelicznikSilent = false; }

            _granulacja = (tb.Tag as string) switch
            {
                "Tydzien" => GranulacjaCzasu.Tydzien,
                "Miesiac" => GranulacjaCzasu.Miesiac,
                _ => GranulacjaCzasu.Dzien
            };

            Przelicz();
        }

        private void Metryka_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton tb) return;

            _przelicznikSilent = true;
            try
            {
                tbMetrykaProc.IsChecked = ReferenceEquals(tb, tbMetrykaProc);
                tbMetrykaPLN.IsChecked = ReferenceEquals(tb, tbMetrykaPLN);
            }
            finally { _przelicznikSilent = false; }

            _metrykaProcent = (tb.Tag as string) == "Proc";
            OdswiezWykres();
        }

        // ─── Eksport CSV ──────────────────────────────────────────────────────

        private void BtnEksport_Click(object sender, RoutedEventArgs e) => EksportujCsv();

        public void EksportujCsv()
        {
            if (_data.Serie.Count == 0 || _data.OsCzasu.Count == 0)
            {
                MessageBox.Show("Brak danych do eksportu. Zaznacz towary i kliknij Zastosuj.",
                    "Eksport", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Filter = "CSV (*.csv)|*.csv",
                FileName = $"udzial_przychodu_{_data.DataOd:yyyyMMdd}_{_data.DataDo:yyyyMMdd}_{_granulacja}.csv"
            };
            if (dlg.ShowDialog() != true) return;

            var sb = new StringBuilder();
            sb.Append("Kod;Nazwa;Suma_PLN;Sredni_proc");
            foreach (var o in _data.OsCzasu)
                sb.Append(";").Append(Csv(o.EtykietaPelna));
            sb.AppendLine();

            // Wiersz: suma globalna per okres (mianownik)
            sb.Append(";;;");
            sb.Append("100,00");  // średni % bazy = 100
            foreach (var o in _data.OsCzasu)
            {
                sb.Append(";");
                sb.Append(_data.SumaCalkowitaPerOkres.GetValueOrDefault(o.PoczatekOkresu)
                    .ToString("F2", CultureInfo.InvariantCulture));
            }
            sb.AppendLine();

            foreach (var s in _data.Serie.OrderByDescending(x => x.SumaWartosci))
            {
                sb.Append(Csv(s.Kod)).Append(";");
                sb.Append(Csv(s.Nazwa)).Append(";");
                sb.Append(s.SumaWartosci.ToString("F2", CultureInfo.InvariantCulture)).Append(";");
                sb.Append(s.SredniUdzialProc.ToString("F2", CultureInfo.InvariantCulture));

                foreach (var o in _data.OsCzasu)
                {
                    sb.Append(";");
                    if (_metrykaProcent)
                        sb.Append(s.UdzialProcPerOkres.GetValueOrDefault(o.PoczatekOkresu)
                            .ToString("F2", CultureInfo.InvariantCulture));
                    else
                        sb.Append(s.WartoscPerOkres.GetValueOrDefault(o.PoczatekOkresu)
                            .ToString("F2", CultureInfo.InvariantCulture));
                }
                sb.AppendLine();
            }

            File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
            MessageBox.Show("Zapisano: " + dlg.FileName, "Eksport",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private static string FormatPLN(decimal v) => v.ToString("N0", CultureInfo.GetCultureInfo("pl-PL")) + " zł";

        private static string Csv(string? s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
    }
}
