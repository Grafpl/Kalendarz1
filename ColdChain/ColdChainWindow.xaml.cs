using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LiveCharts;
using LiveCharts.Wpf;
using Microsoft.Win32;

namespace Kalendarz1.ColdChain
{
    /// <summary>
    /// Cold Chain HACCP (#2 — przebudowane) — kontrola temperatur na danych TemperaturyMiejsca + QC_Normy.
    /// 5 zakładek: Dashboard, Wpis pomiaru, Trendy (wykres), Incydenty (korekty HACCP), Raport CSV.
    /// </summary>
    public partial class ColdChainWindow : Window
    {
        private readonly ColdChainService _service = new();
        private List<TempNorma> _normy = new();
        private List<TempPomiar> _pomiary = new();

        public ColdChainWindow()
        {
            InitializeComponent();
            dpOd.SelectedDate = DateTime.Today.AddDays(-30);
            dpDo.SelectedDate = DateTime.Today;
            Loaded += async (_, _) => await ZaladujWszystkoAsync();
        }

        private async Task ZaladujWszystkoAsync()
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                _normy = await _service.GetNormyTempAsync();
                var partie = await _service.GetPartieAsync();
                cbPartia.ItemsSource = partie;
                cbKrzywaPartia.ItemsSource = partie;
                cbMiejsce.ItemsSource = MiejscaCC.Opcje();
                cbTrendMiejsce.ItemsSource = MiejscaCC.Opcje();
                cbTrendMiejsce.SelectedIndex = 0;
                await OdswiezDashboardAsync();
                await OdswiezTrendAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd ładowania:\n" + ex.Message
                    + "\n\nDane: TemperaturyMiejsca + QC_Normy. Uruchom ColdChain/SQL/CreateColdChain.sql (rejestr korekt).",
                    "Cold Chain", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally { Mouse.OverrideCursor = null; }
        }

        private async Task OdswiezDashboardAsync()
        {
            DateTime od = dpOd.SelectedDate ?? DateTime.Today.AddDays(-30);
            DateTime doD = dpDo.SelectedDate ?? DateTime.Today;

            _pomiary = await _service.GetPomiaryAsync(od, doD, _normy);
            dgPomiary.ItemsSource = _pomiary;
            lstKafelki.ItemsSource = _service.BudujKafelki(_pomiary, _normy);

            var zSrednia = _pomiary.Where(p => p.Srednia.HasValue).ToList();
            int poza = zSrednia.Count(p => p.CzyPozaNorma);
            decimal compliance = zSrednia.Count > 0 ? (decimal)(zSrednia.Count - poza) / zSrednia.Count * 100m : 0m;

            kpiCompliance.Text = zSrednia.Count > 0 ? $"{compliance:N1}%" : "—";
            kpiPomiary.Text = zSrednia.Count.ToString();
            kpiIncydenty.Text = poza.ToString();

            // Incydenty (poza normą)
            dgIncydenty.ItemsSource = _pomiary.Where(p => p.CzyPozaNorma).ToList();

            // Ranking hodowców + niekompletne partie (na już-załadowanych danych)
            dgRankingHod.ItemsSource = _service.BudujRankingHodowcow(_pomiary);
            var niekompletne = _service.BudujNiekompletne(_pomiary);
            dgNiekompletne.ItemsSource = niekompletne;
            kpiNiekompletne.Text = niekompletne.Count.ToString();

            // Raport podsumowanie
            txtRaportPodsum.Text = zSrednia.Count == 0
                ? "Brak pomiarów w wybranym okresie."
                : $"Okres {od:dd.MM.yyyy}–{doD:dd.MM.yyyy}: {zSrednia.Count} pomiarów, "
                  + $"{zSrednia.Count - poza} w normie, {poza} poza normą. Zgodność: {compliance:N1}%.";
        }

        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            await OdswiezDashboardAsync();
            await OdswiezTrendAsync();
        }

        // ─── WPIS ──────────────────────────────────────────────────────────
        private TempNorma? NormaDlaMiejsca(string miejsce)
            => _normy.FirstOrDefault(n => n.Miejsce.Equals(miejsce, StringComparison.OrdinalIgnoreCase));

        private string WybraneMiejsce() => cbMiejsce.SelectedValue as string ?? "";

        private void CbMiejsce_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var norma = NormaDlaMiejsca(WybraneMiejsce());
            txtNormaInfo.Text = norma != null ? $"Norma: {norma.ZakresFormatted}" : "Brak normy dla tego miejsca";
            PrzeliczSrednia();
        }

        private void Proba_TextChanged(object sender, TextChangedEventArgs e) => PrzeliczSrednia();

        private void PrzeliczSrednia()
        {
            var proby = new[] { txtP1.Text, txtP2.Text, txtP3.Text, txtP4.Text }
                .Select(ParseDec).Where(x => x.HasValue).Select(x => x!.Value).ToList();
            if (proby.Count == 0)
            {
                txtSrednia.Text = "—";
                txtPodgladStatus.Text = "";
                boxPodglad.Background = BrushFromHex("#F1F5F9");
                return;
            }
            decimal sr = Math.Round(proby.Average(), 2);
            txtSrednia.Text = $"{sr:N1}°C";

            var norma = NormaDlaMiejsca(WybraneMiejsce());
            if (norma == null)
            {
                txtPodgladStatus.Text = "(wybierz miejsce aby ocenić vs norma)";
                boxPodglad.Background = BrushFromHex("#F1F5F9");
                return;
            }
            if (norma.IsInNorm(sr))
            {
                txtPodgladStatus.Text = $"✓ W normie ({norma.ZakresFormatted})";
                boxPodglad.Background = BrushFromHex("#D1FAE5");
            }
            else
            {
                txtPodgladStatus.Text = $"🚨 POZA NORMĄ ({norma.ZakresFormatted})";
                boxPodglad.Background = BrushFromHex("#FEE2E2");
            }
        }

        private async void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            string partia = (cbPartia.SelectedItem as PartiaItem)?.Partia ?? cbPartia.Text?.Trim() ?? "";
            string miejsce = WybraneMiejsce();
            if (string.IsNullOrWhiteSpace(partia)) { Komunikat("Wybierz partię.", false); return; }
            if (string.IsNullOrWhiteSpace(miejsce)) { Komunikat("Wybierz miejsce pomiaru.", false); return; }

            var p1 = ParseDec(txtP1.Text); var p2 = ParseDec(txtP2.Text);
            var p3 = ParseDec(txtP3.Text); var p4 = ParseDec(txtP4.Text);
            if (!p1.HasValue && !p2.HasValue && !p3.HasValue && !p4.HasValue)
            { Komunikat("Wpisz co najmniej jedną próbkę.", false); return; }

            // Walidacja: rozrzut próbek + sanity per miejsce (ostrzeżenie, nie blokada)
            string? ostrzezenie = ValidujProbki(miejsce, p1, p2, p3, p4);
            if (ostrzezenie != null)
            {
                var odp = MessageBox.Show(
                    ostrzezenie + "\n\nZapisać mimo to?", "Nietypowe wartości",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (odp != MessageBoxResult.Yes) return;
            }

            try
            {
                btnZapisz.IsEnabled = false;
                await _service.ZapiszPomiarAsync(partia, miejsce, p1, p2, p3, p4, App.UserID);
                Komunikat($"✓ Zapisano pomiar: partia {partia}, {miejsce}.", true);
                txtP1.Clear(); txtP2.Clear(); txtP3.Clear(); txtP4.Clear();
                PrzeliczSrednia();
                await OdswiezDashboardAsync();
                await OdswiezTrendAsync();
            }
            catch (Exception ex) { Komunikat("Błąd zapisu: " + ex.Message, false); }
            finally { btnZapisz.IsEnabled = true; }
        }

        /// <summary>Walidacja próbek: rozrzut + sanity per miejsce. Zwraca treść ostrzeżenia lub null gdy OK.</summary>
        private static string? ValidujProbki(string miejsce, params decimal?[] proby)
        {
            var v = proby.Where(x => x.HasValue).Select(x => x!.Value).ToList();
            if (v.Count == 0) return null;
            var uwagi = new List<string>();

            // Rozrzut próbek
            decimal rozrzut = v.Max() - v.Min();
            if (rozrzut > 3m)
                uwagi.Add($"Duży rozrzut próbek: {rozrzut:N1}°C (od {v.Min():N1} do {v.Max():N1}).");

            // Sanity per miejsce (realistyczne zakresy fizyczne)
            (decimal lo, decimal hi) zakres = miejsce.ToLowerInvariant() switch
            {
                "oparzalnik" => (35m, 75m),
                "schladzalnik" => (-5m, 12m),
                "rampa" => (-5m, 25m),
                "chiller" => (-10m, 12m),
                "tunel" => (-40m, -5m),
                _ => (-50m, 100m)
            };
            foreach (var x in v)
                if (x < zakres.lo || x > zakres.hi)
                {
                    uwagi.Add($"Wartość {x:N1}°C nietypowa dla miejsca '{miejsce}' (oczekiwane {zakres.lo:N0}…{zakres.hi:N0}°C).");
                    break;
                }

            return uwagi.Count > 0 ? string.Join("\n", uwagi) : null;
        }

        // ─── TRENDY ────────────────────────────────────────────────────────
        private string WybraneTrendMiejsce() => cbTrendMiejsce.SelectedValue as string ?? "oparzalnik";

        private async void CbTrendMiejsce_SelectionChanged(object sender, SelectionChangedEventArgs e) => await OdswiezTrendAsync();

        private async Task OdswiezTrendAsync()
        {
            if (cbTrendMiejsce.SelectedItem == null) return;
            DateTime od = dpOd.SelectedDate ?? DateTime.Today.AddDays(-30);
            DateTime doD = dpDo.SelectedDate ?? DateTime.Today;
            string miejsce = WybraneTrendMiejsce();

            try
            {
                var trend = await _service.GetTrendAsync(miejsce, od, doD);
                var norma = NormaDlaMiejsca(miejsce);

                var wartosci = new ChartValues<double>(trend.Select(t => (double)t.Srednia));
                var seria = new SeriesCollection
                {
                    new LineSeries
                    {
                        Title = $"Temp {miejsce}",
                        Values = wartosci,
                        PointGeometrySize = 6,
                        Stroke = (Brush)new BrushConverter().ConvertFromString("#2563EB")!,
                        Fill = Brushes.Transparent
                    }
                };

                // Linie norm (min/max) jako poziome serie
                if (norma?.Max.HasValue == true)
                    seria.Add(LiniaNormy("Max", (double)norma.Max.Value, "#DC2626", trend.Count));
                if (norma?.Min.HasValue == true)
                    seria.Add(LiniaNormy("Min", (double)norma.Min.Value, "#F59E0B", trend.Count));

                chartTrend.Series = seria;
                axisTrendX.Labels = trend.Select(t => t.Data.ToString("dd.MM")).ToList();
                txtTrendInfo.Text = trend.Count == 0
                    ? "Brak pomiarów dla tego miejsca w okresie."
                    : $"{trend.Count} pomiarów. {(norma != null ? "Norma: " + norma.ZakresFormatted : "")}";
            }
            catch (Exception ex)
            {
                txtTrendInfo.Text = "Błąd: " + ex.Message;
            }
        }

        private static LineSeries LiniaNormy(string tytul, double poziom, string hex, int liczbaPunktow)
        {
            int n = Math.Max(liczbaPunktow, 2);
            return new LineSeries
            {
                Title = tytul,
                Values = new ChartValues<double>(Enumerable.Repeat(poziom, n)),
                PointGeometry = null,
                StrokeDashArray = new DoubleCollection { 4, 3 },
                Stroke = (Brush)new BrushConverter().ConvertFromString(hex)!,
                Fill = Brushes.Transparent
            };
        }

        // ─── KRZYWA SCHŁADZANIA (per partia) ───────────────────────────────
        private async void BtnKrzywa_Click(object sender, RoutedEventArgs e)
        {
            string partia = (cbKrzywaPartia.SelectedItem as PartiaItem)?.Partia
                            ?? cbKrzywaPartia.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(partia)) { txtKrzywaInfo.Text = "Wybierz partię."; return; }

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                var pomiary = await _service.GetPomiaryPartiiAsync(partia, _normy);
                string[] kolejnosc = MiejscaCC.KolejnoscKrzywej;

                var etapy = new List<(string m, decimal? v, decimal? min, decimal? max)>();
                foreach (var m in kolejnosc)
                {
                    var last = pomiary.Where(x => x.Miejsce == m && x.Srednia.HasValue)
                                      .OrderByDescending(x => x.DataPomiaru).FirstOrDefault();
                    var norma = NormaDlaMiejsca(m);
                    etapy.Add((m, last?.Srednia, norma?.Min, norma?.Max));
                }

                var dostepne = etapy.Where(x => x.v.HasValue).ToList();
                if (dostepne.Count == 0)
                {
                    txtKrzywaInfo.Text = "Brak pomiarów dla tej partii.";
                    chartKrzywa.Series = new SeriesCollection();
                    txtKrzywaOcena.Text = "Brak danych.";
                    return;
                }

                chartKrzywa.Series = new SeriesCollection
                {
                    new LineSeries
                    {
                        Title = $"Partia {partia}",
                        Values = new ChartValues<double>(dostepne.Select(x => (double)x.v!.Value)),
                        PointGeometrySize = 14,
                        LineSmoothness = 0,
                        Stroke = (Brush)new BrushConverter().ConvertFromString("#2563EB")!,
                        Fill = Brushes.Transparent
                    }
                };
                axisKrzywaX.Labels = dostepne.Select(x => x.m).ToList();

                // Ocena HACCP: każdy etap w normie + czy temperatura spada
                var problemy = new List<string>();
                foreach (var et in etapy)
                {
                    if (!et.v.HasValue) { problemy.Add($"brak pomiaru: {et.m}"); continue; }
                    bool ok = (!et.min.HasValue || et.v.Value >= et.min.Value)
                           && (!et.max.HasValue || et.v.Value <= et.max.Value);
                    if (!ok) problemy.Add($"{et.m} {et.v.Value:N1}°C poza normą");
                }
                // Czy spada (rampa > chiller > tunel)?
                bool spada = true;
                for (int i = 1; i < dostepne.Count; i++)
                    if (dostepne[i].v!.Value > dostepne[i - 1].v!.Value) spada = false;

                txtKrzywaInfo.Text = $"{dostepne.Count}/3 etapów zmierzonych.";
                txtKrzywaOcena.Text = problemy.Count == 0 && spada
                    ? "✓ Krzywa poprawna — temperatura spada na każdym etapie, wszystkie w normie."
                    : (spada ? "" : "⚠ Temperatura nie spada konsekwentnie rampa→chiller→tunel. ")
                      + (problemy.Count > 0 ? "Uwagi: " + string.Join("; ", problemy) : "");
            }
            catch (Exception ex) { txtKrzywaInfo.Text = "Błąd: " + ex.Message; }
            finally { Mouse.OverrideCursor = null; }
        }

        // ─── INCYDENTY ─────────────────────────────────────────────────────
        private async void BtnKorekta_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.Tag is not TempPomiar p) return;
            string? istniejaca = p.MaKorekta ? await _service.GetKorektaOpisAsync(p.Id) : null;

            var dlg = new KorektaDialog(p, istniejaca) { Owner = this };
            if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.Korekta))
            {
                try
                {
                    await _service.ZapiszKorekteAsync(p.Id, dlg.Korekta, App.UserID);
                    await OdswiezDashboardAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Błąd zapisu korekty: " + ex.Message, "Cold Chain",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // ─── RAPORT CSV ────────────────────────────────────────────────────
        private void BtnEksportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (_pomiary.Count == 0) { MessageBox.Show("Brak danych do eksportu.", "Raport"); return; }
            DateTime od = dpOd.SelectedDate ?? DateTime.Today.AddDays(-30);
            DateTime doD = dpDo.SelectedDate ?? DateTime.Today;

            var dlg = new SaveFileDialog
            {
                Filter = "CSV (*.csv)|*.csv",
                FileName = $"coldchain_haccp_{od:yyyyMMdd}_{doD:yyyyMMdd}.csv"
            };
            if (dlg.ShowDialog() != true) return;

            var sb = new StringBuilder();
            sb.AppendLine("Data;Partia;Hodowca;Miejsce;Proba1;Proba2;Proba3;Proba4;Srednia;NormaMin;NormaMax;Status;Wykonal");
            foreach (var p in _pomiary)
            {
                sb.AppendLine(string.Join(";",
                    p.DataPomiaru.ToString("yyyy-MM-dd HH:mm"),
                    Csv(p.PartiaId), Csv(p.Hodowca), Csv(p.Miejsce),
                    Dec(p.Proba1), Dec(p.Proba2), Dec(p.Proba3), Dec(p.Proba4), Dec(p.Srednia),
                    Dec(p.NormaMin), Dec(p.NormaMax),
                    Csv(p.CzyPozaNorma ? (p.MaKorekta ? "POZA-SKORYGOWANE" : "POZA-NORMA") : "OK"),
                    Csv(p.Wykonal)));
            }
            File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
            MessageBox.Show("Zapisano raport: " + dlg.FileName, "Raport HACCP",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ─── Helpers ───────────────────────────────────────────────────────
        private void Komunikat(string tekst, bool ok)
        {
            txtKomunikat.Text = tekst;
            txtKomunikat.Foreground = ok ? BrushFromHex("#059669") : Brushes.DarkRed;
        }

        private static decimal? ParseDec(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Replace(',', '.').Trim();
            return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
        }

        private static string Dec(decimal? d) => d.HasValue ? d.Value.ToString("F2", CultureInfo.InvariantCulture) : "";
        private static string Csv(string? s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";

        private static SolidColorBrush BrushFromHex(string hex)
        {
            try { return (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!; }
            catch { return new SolidColorBrush(Colors.Gray); }
        }
    }
}
