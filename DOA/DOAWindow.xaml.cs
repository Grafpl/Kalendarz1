using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LiveCharts;
using LiveCharts.Wpf;
using Microsoft.Win32;

namespace Kalendarz1.DOA
{
    /// <summary>
    /// DOA Dashboard (#1) — ANALIZA padłych z FarmerCalc (Specyfikacja Drobiu).
    /// Walidacja dat, auto-refresh z debounce, loading overlay, empty state,
    /// auto-insight, status bar, skróty klawiszowe (F5/Esc/Ctrl+E).
    /// </summary>
    public partial class DOAWindow : Window
    {
        private enum TrybRanking { Wszyscy, Alerty, Najlepsi }

        private readonly DOAService _service = new();
        private List<DOARekord> _rekordy = new();
        private List<DOAHodowca> _ranking = new();
        private string? _filtrHodowca;
        private DispatcherTimer? _debounceTimer;
        private bool _ładowanieZSkrótu;
        private TrybRanking _trybRanking = TrybRanking.Wszyscy;
        private string _szukajRanking = "";

        public DOAWindow()
        {
            InitializeComponent();
            UstawZakres(DateTime.Today.AddDays(-30), DateTime.Today);
            Loaded += async (_, _) => await OdswiezAsync();
        }

        // ─── Zakres + presety ──────────────────────────────────────────────
        private void UstawZakres(DateTime od, DateTime doDate)
        {
            _ładowanieZSkrótu = true;       // wstrzymaj auto-refresh
            dpOd.SelectedDate = od;
            dpDo.SelectedDate = doDate;
            _ładowanieZSkrótu = false;
        }

        private async void Preset7_Click(object s, RoutedEventArgs e)
            { UstawZakres(DateTime.Today.AddDays(-7), DateTime.Today); await OdswiezAsync(); }
        private async void Preset30_Click(object s, RoutedEventArgs e)
            { UstawZakres(DateTime.Today.AddDays(-30), DateTime.Today); await OdswiezAsync(); }
        private async void PresetTenMies_Click(object s, RoutedEventArgs e)
        {
            var dziś = DateTime.Today;
            UstawZakres(new DateTime(dziś.Year, dziś.Month, 1), dziś);
            await OdswiezAsync();
        }
        private async void PresetPoprMies_Click(object s, RoutedEventArgs e)
        {
            var pierwszyTen = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var pierwszyPopr = pierwszyTen.AddMonths(-1);
            UstawZakres(pierwszyPopr, pierwszyTen.AddDays(-1));
            await OdswiezAsync();
        }
        private async void PresetRok_Click(object s, RoutedEventArgs e)
            { UstawZakres(new DateTime(DateTime.Today.Year, 1, 1), DateTime.Today); await OdswiezAsync(); }

        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e) => await OdswiezAsync();

        // Auto-refresh ze zmianą daty (debounce 350ms, zabezpieczenie przed dublowaniem)
        private void DateChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_ładowanieZSkrótu) return;
            if (_debounceTimer == null)
            {
                _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
                _debounceTimer.Tick += async (_, _) =>
                {
                    _debounceTimer!.Stop();
                    await OdswiezAsync();
                };
            }
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        // ─── Walidacja zakresu ─────────────────────────────────────────────
        private (DateTime od, DateTime doD) PobierzZakres()
        {
            DateTime od = dpOd.SelectedDate ?? DateTime.Today.AddDays(-30);
            DateTime doD = dpDo.SelectedDate ?? DateTime.Today;
            // Zamień jeśli odwrotne
            if (od > doD) (od, doD) = (doD, od);
            // Przytnij datę końcową do dziś (nie pokazujemy przyszłości)
            if (doD > DateTime.Today) doD = DateTime.Today;
            // Min granica: rok 2015 (dane nie mogą być starsze)
            if (od < new DateTime(2015, 1, 1)) od = new DateTime(2015, 1, 1);
            return (od, doD);
        }

        // ─── Główny odświeżacz (3 zapytania równolegle → 3x szybciej) ──────
        private async Task OdswiezAsync()
        {
            var (od, doD) = PobierzZakres();
            if (dpOd.SelectedDate != od || dpDo.SelectedDate != doD) UstawZakres(od, doD);

            // Poprzedni okres — tej samej długości — wyliczamy ZAWSZE, żeby policzyć trendy + delta KPI
            int dni = Math.Max(1, (doD - od).Days + 1);
            DateTime poprzDo = od.AddDays(-1);
            DateTime poprzOd = poprzDo.AddDays(-(dni - 1));

            PokazLadowanie(true);
            var stoper = Stopwatch.StartNew();
            try
            {
                // 3 zapytania równolegle — Task.WhenAll
                var tBież = _service.GetRankingHodowcowAsync(od, doD);
                var tRek = _service.GetRekordyAsync(od, doD);
                var tPoprz = _service.GetRankingHodowcowAsync(poprzOd, poprzDo);
                await Task.WhenAll(tBież, tRek, tPoprz);

                _ranking = tBież.Result;
                _rekordy = tRek.Result;
                var poprzRanking = tPoprz.Result;

                // Trend per hodowca: bieżące DOA – poprzednie DOA (w punktach procentowych)
                var poprzMapa = poprzRanking
                    .Where(p => p.SumaSztukDek > 0)
                    .ToDictionary(p => KluczHodowcy(p), p => p.SredniProcDOA, StringComparer.OrdinalIgnoreCase);
                foreach (var h in _ranking)
                {
                    if (poprzMapa.TryGetValue(KluczHodowcy(h), out var poprzVal))
                        h.TrendDOAPP = h.SredniProcDOA - poprzVal;
                }

                AktualizujRankingPokaz();
                AktualizujRekordy();
                AktualizujKpi();
                AktualizujTrend();
                AktualizujPustyStan();
                AktualizujInsight();
                AktualizujPorownanieZPoprzedniego(poprzRanking);

                stoper.Stop();
                txtStatus.Text = $"✓ Ostatnia aktualizacja: {DateTime.Now:HH:mm:ss} • "
                    + $"{_rekordy.Count} dostaw • {_ranking.Count} hodowców • "
                    + $"{(od == doD ? od.ToString("dd.MM.yyyy") : $"{od:dd.MM.yyyy} – {doD:dd.MM.yyyy}")} "
                    + $"• {stoper.ElapsedMilliseconds} ms (3 zapytania równoległe)";
                txtStatus.Foreground = (SolidColorBrush)new BrushConverter().ConvertFromString("#475569")!;
            }
            catch (Exception ex)
            {
                txtStatus.Text = "⚠ Błąd ładowania danych. Sprawdź połączenie z bazą LibraNet. " + KrótkiOpisBłędu(ex);
                txtStatus.Foreground = Brushes.DarkRed;
            }
            finally
            {
                PokazLadowanie(false);
            }
        }

        private static string KluczHodowcy(DOAHodowca h)
            => string.IsNullOrEmpty(h.HodowcaId) ? (h.Hodowca ?? "") : h.HodowcaId!;

        private static string KrótkiOpisBłędu(Exception ex)
        {
            var msg = ex.Message ?? "";
            if (msg.Length > 140) msg = msg.Substring(0, 140) + "…";
            return $"({msg})";
        }

        private void PokazLadowanie(bool włączone)
        {
            loadingOverlay.Visibility = włączone ? Visibility.Visible : Visibility.Collapsed;
            Cursor = włączone ? Cursors.Wait : null;
            btnOdswiez.IsEnabled = !włączone;
        }

        // ─── Empty state dla dostaw (ranking ogarnia AktualizujRankingPokaz) ───
        private void AktualizujPustyStan()
        {
            int pokazywane = (dgRekordy.ItemsSource as System.Collections.ICollection)?.Count ?? 0;
            emptyRekordy.Visibility = pokazywane == 0 ? Visibility.Visible : Visibility.Collapsed;
            txtEmptyRekordyHint.Text = string.IsNullOrEmpty(_filtrHodowca)
                ? "Wybierz inny zakres dat lub sprawdź czy są wpisy w Specyfikacji."
                : $"Brak dostaw od '{_filtrHodowca}' w tym okresie — naciśnij Esc aby wyczyścić filtr.";
        }

        // ─── Rekordy z filtrem hodowcy ─────────────────────────────────────
        private void AktualizujRekordy()
        {
            var lista = string.IsNullOrEmpty(_filtrHodowca)
                ? _rekordy
                : _rekordy.Where(r => string.Equals(r.Hodowca, _filtrHodowca, StringComparison.OrdinalIgnoreCase)).ToList();
            dgRekordy.ItemsSource = lista;

            if (string.IsNullOrEmpty(_filtrHodowca))
            {
                txtFiltrInfo.Text = _rekordy.Count > 0
                    ? $"Wszystkie dostawy ({_rekordy.Count})"
                    : "Brak dostaw";
                btnClearFilter.Visibility = Visibility.Collapsed;
            }
            else
            {
                txtFiltrInfo.Text = $"Filtr: {_filtrHodowca} • {lista.Count} z {_rekordy.Count}";
                btnClearFilter.Visibility = Visibility.Visible;
            }
            AktualizujPustyStan();
        }

        private void DgRanking_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgRanking.SelectedItem is DOAHodowca h && h.Hodowca != _filtrHodowca)
            {
                _filtrHodowca = h.Hodowca;
                AktualizujRekordy();
            }
        }

        private void BtnClearFilter_Click(object sender, RoutedEventArgs e) => WyczyśćFiltr();
        private void WyczyśćFiltr()
        {
            if (string.IsNullOrEmpty(_filtrHodowca)) return;
            _filtrHodowca = null;
            dgRanking.UnselectAll();
            AktualizujRekordy();
        }

        // ─── KPI ────────────────────────────────────────────────────────────
        private void AktualizujKpi()
        {
            long sumaPadle = _ranking.Sum(x => x.SumaPadlych);
            long sumaDek = _ranking.Sum(x => x.SumaSztukDek);
            decimal srednie = sumaDek > 0 ? (decimal)sumaPadle / sumaDek * 100m : 0m;

            kpiSrednie.Text = sumaDek > 0 ? $"{srednie:N2}%" : "—";
            kpiPadle.Text = $"{sumaPadle:N0} / {sumaDek:N0}";
            kpiAlerty.Text = _ranking.Count(x => x.SredniProcDOA > 0.50m).ToString();
            kpiDostaw.Text = _rekordy.Count.ToString();
        }

        // ─── Porównanie z poprzednim okresem (bez fetch — z już-pobranych danych) ───
        private void AktualizujPorownanieZPoprzedniego(List<DOAHodowca> poprz)
        {
            try
            {
                long pPadle = poprz.Sum(x => x.SumaPadlych);
                long pDek = poprz.Sum(x => x.SumaSztukDek);
                if (pDek <= 0) { kpiSrednieDelta.Text = ""; return; }
                decimal poprzSr = (decimal)pPadle / pDek * 100m;

                long bieżPadle = _ranking.Sum(x => x.SumaPadlych);
                long bieżDek = _ranking.Sum(x => x.SumaSztukDek);
                if (bieżDek <= 0) { kpiSrednieDelta.Text = ""; return; }
                decimal bieżSr = (decimal)bieżPadle / bieżDek * 100m;

                decimal delta = bieżSr - poprzSr;
                string arrow = delta < 0 ? "▼" : delta > 0 ? "▲" : "▬";
                string kolor = delta < 0 ? "#10B981" : delta > 0 ? "#DC2626" : "#94A3B8";
                kpiSrednieDelta.Text = $"{arrow} {Math.Abs(delta):N2} pp vs poprz. okres ({poprzSr:N2}%)";
                kpiSrednieDelta.Foreground = BrushFromHex(kolor);
            }
            catch { kpiSrednieDelta.Text = ""; }
        }

        // ─── Szybkie filtry rankingu + wyszukiwarka ────────────────────────
        private void FiltrWszyscy_Click(object s, RoutedEventArgs e) { _trybRanking = TrybRanking.Wszyscy; AktualizujRankingPokaz(); }
        private void FiltrAlerty_Click(object s, RoutedEventArgs e)  { _trybRanking = TrybRanking.Alerty;   AktualizujRankingPokaz(); }
        private void FiltrNajlepsi_Click(object s, RoutedEventArgs e){ _trybRanking = TrybRanking.Najlepsi; AktualizujRankingPokaz(); }

        private void TxtSzukaj_TextChanged(object sender, TextChangedEventArgs e)
        {
            _szukajRanking = txtSzukaj.Text?.Trim() ?? "";
            txtSzukajWatermark.Visibility = string.IsNullOrEmpty(_szukajRanking)
                ? Visibility.Visible : Visibility.Collapsed;
            AktualizujRankingPokaz();
        }

        private void AktualizujRankingPokaz()
        {
            IEnumerable<DOAHodowca> lista = _ranking;

            switch (_trybRanking)
            {
                case TrybRanking.Alerty:
                    lista = lista.Where(x => x.SredniProcDOA > 0.50m);
                    break;
                case TrybRanking.Najlepsi:
                    lista = lista.Where(x => x.LiczbaPartii >= 3)
                                 .OrderBy(x => x.SredniProcDOA)
                                 .Take(5);
                    break;
            }
            if (!string.IsNullOrWhiteSpace(_szukajRanking))
                lista = lista.Where(x => (x.Hodowca ?? "")
                    .IndexOf(_szukajRanking, StringComparison.OrdinalIgnoreCase) >= 0);

            var koncowa = lista.ToList();
            dgRanking.ItemsSource = koncowa;

            txtRankingInfo.Text = koncowa.Count == _ranking.Count
                ? $"{koncowa.Count} hodowców"
                : $"{koncowa.Count} z {_ranking.Count}";

            OdswiezPrzyciskiFiltru();
            // empty state odświeżamy w AktualizujPustyStan przy każdym Odswiez
            if (emptyRanking != null)
                emptyRanking.Visibility = koncowa.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OdswiezPrzyciskiFiltru()
        {
            void Set(Button b, bool aktywny)
            {
                b.Background = BrushFromHex(aktywny ? "#2563EB" : "#EEF2F7");
                b.Foreground = aktywny ? Brushes.White : BrushFromHex("#0F172A");
            }
            Set(btnFiltrWszyscy,  _trybRanking == TrybRanking.Wszyscy);
            Set(btnFiltrAlerty,   _trybRanking == TrybRanking.Alerty);
            Set(btnFiltrNajlepsi, _trybRanking == TrybRanking.Najlepsi);
        }

        // ─── Auto-insight ──────────────────────────────────────────────────
        private void AktualizujInsight()
        {
            if (_ranking.Count == 0)
            {
                boxInsight.Visibility = Visibility.Collapsed;
                return;
            }

            // Najgorszy hodowca
            var top = _ranking
                .Where(x => x.SumaSztukDek > 0)
                .OrderByDescending(x => x.SredniProcDOA)
                .FirstOrDefault();
            int powyzejNormy = _ranking.Count(x => x.SredniProcDOA > 0.50m);
            int podwyzszone = _ranking.Count(x => x.SredniProcDOA > 0.20m && x.SredniProcDOA <= 0.50m);

            string ikona; string tekst; string tlo; string bordo; string fg;

            if (powyzejNormy >= 3 && top != null)
            {
                ikona = "🚨";
                tekst = $"{powyzejNormy} hodowców powyżej dopuszczalnej normy 0,5%. "
                      + $"Najwyższe DOA: {top.Hodowca} ({top.SredniProcDOA:N2}%, "
                      + $"{top.SumaPadlych:N0} z {top.SumaSztukDek:N0} szt.). "
                      + $"Warto z nimi porozmawiać — sprawdź warunki transportu/obsady.";
                tlo = "#FEE2E2"; bordo = "#FCA5A5"; fg = "#7F1D1D";
            }
            else if (powyzejNormy >= 1 && top != null)
            {
                ikona = "⚠";
                tekst = $"{powyzejNormy} hodowca powyżej normy ({top.Hodowca}: {top.SredniProcDOA:N2}%). "
                      + $"+{podwyzszone} z DOA w strefie podwyższonej. Monitoruj kolejne dostawy.";
                tlo = "#FEF3C7"; bordo = "#FCD34D"; fg = "#78350F";
            }
            else if (_rekordy.Count < 5)
            {
                ikona = "💡";
                tekst = $"Tylko {_rekordy.Count} dostaw w tym okresie — wnioski są niepewne. "
                      + "Spróbuj większego zakresu (np. '30 dni' lub 'Ten miesiąc').";
                tlo = "#EFF6FF"; bordo = "#BFDBFE"; fg = "#1E40AF";
            }
            else
            {
                ikona = "✓";
                tekst = $"Wszyscy hodowcy w normie. Średnie DOA okresu: {(_ranking.Count > 0 ? "OK" : "—")}. "
                      + "Brak alertów.";
                tlo = "#D1FAE5"; bordo = "#86EFAC"; fg = "#065F46";
            }

            txtInsightIcon.Text = ikona;
            txtInsight.Text = tekst;
            boxInsight.Background = BrushFromHex(tlo);
            boxInsight.BorderBrush = BrushFromHex(bordo);
            txtInsight.Foreground = BrushFromHex(fg);
            boxInsight.Visibility = Visibility.Visible;
        }

        // ─── Trend DOA per dzień (mini wykres) ─────────────────────────────
        private void AktualizujTrend()
        {
            try
            {
                var trendy = _rekordy
                    .Where(r => r.SztukiDek > 0)
                    .GroupBy(r => r.Data.Date)
                    .OrderBy(g => g.Key)
                    .Select(g => new
                    {
                        Data = g.Key,
                        DOA = (double)((decimal)g.Sum(r => r.Padle) / g.Sum(r => r.SztukiDek) * 100m)
                    })
                    .ToList();

                if (trendy.Count == 0)
                {
                    chartTrend.Series = new SeriesCollection();
                    if (axisTrendX != null) axisTrendX.Labels = new List<string>();
                    return;
                }

                chartTrend.Series = new SeriesCollection
                {
                    new LineSeries
                    {
                        Values = new ChartValues<double>(trendy.Select(t => t.DOA)),
                        PointGeometry = null,
                        LineSmoothness = 0.4,
                        Stroke = BrushFromHex("#2563EB"),
                        Fill = BrushFromHex("#DBEAFE")
                    }
                };
                if (axisTrendX != null)
                    axisTrendX.Labels = trendy.Select(t => t.Data.ToString("dd.MM")).ToList();
            }
            catch
            {
                try { chartTrend.Series = new SeriesCollection(); } catch { }
            }
        }

        // ─── CSV ──────────────────────────────────────────────────────────
        private void BtnCsv_Click(object sender, RoutedEventArgs e) => EksportujCsv();
        private void EksportujCsv()
        {
            if (_ranking.Count == 0)
            {
                txtStatus.Text = "ℹ Brak danych do eksportu. Załaduj dane z innego zakresu.";
                return;
            }
            var dlg = new SaveFileDialog
            {
                Filter = "CSV (*.csv)|*.csv",
                FileName = $"doa_ranking_{DateTime.Today:yyyyMMdd}.csv"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Pozycja;Hodowca;Dostaw;Padle;SztukiDek;DOA_proc;Status");
                foreach (var h in _ranking)
                {
                    sb.AppendLine(string.Join(";",
                        h.Pozycja, Csv(h.Hodowca), h.LiczbaPartii,
                        h.SumaPadlych, h.SumaSztukDek,
                        h.SredniProcDOA.ToString("F2", CultureInfo.InvariantCulture),
                        Csv(h.Status)));
                }
                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                txtStatus.Text = $"✓ Zapisano: {dlg.FileName}";
            }
            catch (Exception ex)
            {
                txtStatus.Text = "⚠ Nie udało się zapisać pliku. " + KrótkiOpisBłędu(ex);
                txtStatus.Foreground = Brushes.DarkRed;
            }
        }

        // ─── Skróty klawiszowe ─────────────────────────────────────────────
        private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // F5 → odśwież
            if (e.Key == Key.F5) { e.Handled = true; await OdswiezAsync(); return; }
            // Esc → wyczyść filtr
            if (e.Key == Key.Escape) { e.Handled = true; WyczyśćFiltr(); return; }
            // Ctrl+E → eksport CSV
            if (e.Key == Key.E && Keyboard.Modifiers == ModifierKeys.Control)
            { e.Handled = true; EksportujCsv(); return; }
        }

        // ─── Helpers ───────────────────────────────────────────────────────
        private static string Csv(string? s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
        private static SolidColorBrush BrushFromHex(string hex)
        {
            try { return (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!; }
            catch { return new SolidColorBrush(Colors.Gray); }
        }
    }
}
