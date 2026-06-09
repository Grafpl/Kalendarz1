// ════════════════════════════════════════════════════════════════════════════
// Transport/WPF/StatystykiRozladunkuWindow.xaml.cs
//
// Okno statystyk rozładunku per klient (uczone z Webfleet GPS).
// Czyta tabelę LibraNet.EstymacjeRozladunku + dociąga nazwy z Sage.
// ════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Data.SqlClient;
using Kalendarz1.Transport.Services;

namespace Kalendarz1.Transport.WPF
{
    public partial class StatystykiRozladunkuWindow : Window
    {
        private const string ConnLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private const string ConnHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        private List<StatystykaRow> _wszystkie = new();
        private List<StatystykaRow> _przefiltrowane = new();

        public StatystykiRozladunkuWindow()
        {
            InitializeComponent();
            Loaded += async (_, _) => await ZaladujAsync();
        }

        // ════════════════════════════════════════════════════════════════════
        // LADOWANIE
        // ════════════════════════════════════════════════════════════════════
        private async Task ZaladujAsync()
        {
            TxtStatus.Text = "Ładowanie statystyk z LibraNet…";
            try
            {
                var dane = await PobierzZBazyAsync();
                if (dane.Count > 0) await DociagnijNazwyAsync(dane);

                // Najpierw klienci z największą medianą, potem ci bez wizyt
                _wszystkie = dane
                    .OrderByDescending(s => s.LiczbaProb > 0)
                    .ThenByDescending(s => s.MinutyMediana)
                    .ToList();
                Filtruj();
                AktualizujPodsumowanie();
                int bezWizyt = _wszystkie.Count(s => s.LiczbaProb == 0);
                int zWizytami = _wszystkie.Count - bezWizyt;
                TxtStatus.Text = $"✓ Załadowano {_wszystkie.Count} klientów z geolokalizacją " +
                                 $"({zWizytami} z wizytami, {bezWizyt} bez wizyt).";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"✗ Błąd: {ex.Message}";
                MessageBox.Show($"Nie udało się załadować statystyk:\n\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task<List<StatystykaRow>> PobierzZBazyAsync()
        {
            var wynik = new List<StatystykaRow>();
            try { await new HistoriaRozladunkuService().EnsureTableAsync(); } catch { }
            try { await new CzasRozladunkuService().EnsureColumnAsync(); } catch { }

            // Pokaż WSZYSTKICH klientów z geolokalizacją (Latitude/Longitude IS NOT NULL),
            // nawet jeśli nie mają jeszcze estymacji w EstymacjeRozladunku.
            // Dla nich MinutyMediana=0, LiczbaProb=0, OstatniRefresh=null.
            // Plus pokaż wartość w karcie (CzasRozladunkuMin) żeby porównać z historią.
            const string sql = @"
                SELECT kod.IdSymfonia                                   AS KlientId,
                       ISNULL(er.MinutyMediana, 0)                      AS MinutyMediana,
                       ISNULL(er.LiczbaProb, 0)                         AS LiczbaProb,
                       er.OstatniRefresh                                AS OstatniRefresh,
                       kod.CzasRozladunkuMin                            AS WKarcie
                FROM dbo.KartotekaOdbiorcyDane kod
                LEFT JOIN dbo.EstymacjeRozladunku er ON er.KlientId = kod.IdSymfonia
                WHERE kod.Latitude IS NOT NULL AND kod.Longitude IS NOT NULL";

            await using var cn = new SqlConnection(ConnLibra);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(sql, cn);
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                wynik.Add(new StatystykaRow
                {
                    KlientId = rd.GetInt32(0),
                    MinutyMediana = rd.GetInt32(1),
                    LiczbaProb = rd.GetInt32(2),
                    OstatniRefresh = rd.IsDBNull(3) ? (DateTime?)null : rd.GetDateTime(3),
                    WKarcie = rd.IsDBNull(4) ? (int?)null : rd.GetInt32(4)
                });
            }
            return wynik;
        }

        private async Task DociagnijNazwyAsync(List<StatystykaRow> dane)
        {
            var idy = dane.Select(d => d.KlientId).ToList();
            if (idy.Count == 0) return;

            var nazwy = new Dictionary<int, string>();
            try
            {
                await using var cn = new SqlConnection(ConnHandel);
                await cn.OpenAsync();
                using var cmd = cn.CreateCommand();
                var p = new List<string>();
                for (int i = 0; i < idy.Count; i++)
                {
                    var name = $"@id{i}";
                    p.Add(name);
                    cmd.Parameters.AddWithValue(name, idy[i]);
                }
                cmd.CommandText = $@"
                    SELECT Id, ISNULL(Shortcut, 'KH ' + CAST(Id AS VARCHAR(10))) AS Skrot
                    FROM [SSCommon].[STContractors]
                    WHERE Id IN ({string.Join(",", p)})";
                cmd.CommandTimeout = 20;
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                    nazwy[rd.GetInt32(0)] = rd.IsDBNull(1) ? "" : rd.GetString(1).Trim();
            }
            catch { /* silent — pokażemy ID gdy brak nazwy */ }

            foreach (var d in dane)
                d.Nazwa = nazwy.TryGetValue(d.KlientId, out var n) && !string.IsNullOrEmpty(n)
                    ? n : $"Klient #{d.KlientId}";
        }

        // ════════════════════════════════════════════════════════════════════
        // FILTRY + WIDOK
        // ════════════════════════════════════════════════════════════════════
        private void Filtruj()
        {
            // Guard: handlery TextChanged/Checked mogą się odpalić podczas parsowania XAML
            // (gdy CheckBox ma IsChecked=True) zanim DataGrid zostanie utworzony.
            if (GridStatystyki == null) return;

            string szukaj = (TxtSzukaj?.Text ?? "").Trim().ToLowerInvariant();
            bool tylkoWiarygodne = ChkTylkoWiarygodne?.IsChecked == true;

            IEnumerable<StatystykaRow> q = _wszystkie;
            if (!string.IsNullOrEmpty(szukaj))
                q = q.Where(s => (s.Nazwa ?? "").ToLowerInvariant().Contains(szukaj)
                              || s.KlientId.ToString().Contains(szukaj));
            if (tylkoWiarygodne)
                q = q.Where(s => s.LiczbaProb >= HistoriaRozladunkuService.MinProbDoZaufania);

            _przefiltrowane = q.ToList();
            GridStatystyki.ItemsSource = _przefiltrowane;
        }

        private void AktualizujPodsumowanie()
        {
            // Guard — wywoływane po załadowaniu, ale kontrolki mogą być null w edge case
            if (TxtLiczbaKlientow == null) return;

            if (_wszystkie.Count == 0)
            {
                TxtLiczbaKlientow.Text = "0";
                TxtLiczbaWiarygodnych.Text = "0";
                TxtSredniaMediana.Text = "—";
                TxtRange.Text = "—";
                PanelTop5.Children.Clear();
                PanelDol5.Children.Clear();
                return;
            }

            var wiarygodne = _wszystkie.Where(s => s.LiczbaProb >= HistoriaRozladunkuService.MinProbDoZaufania).ToList();
            int bezWizyt = _wszystkie.Count(s => s.LiczbaProb == 0);
            TxtLiczbaKlientow.Text = _wszystkie.Count.ToString();
            if (TxtLiczbaBezWizyt != null)
                TxtLiczbaBezWizyt.Text = bezWizyt > 0 ? $"({bezWizyt} bez wykrytych wizyt)" : "";
            TxtLiczbaWiarygodnych.Text = wiarygodne.Count.ToString();
            TxtSredniaMediana.Text = wiarygodne.Count > 0
                ? $"{(int)wiarygodne.Average(s => s.MinutyMediana)} min"
                : "—";

            if (wiarygodne.Count > 0)
            {
                int min = wiarygodne.Min(s => s.MinutyMediana);
                int max = wiarygodne.Max(s => s.MinutyMediana);
                TxtRange.Text = $"{min} ↔ {max} min";
            }

            // TOP 5 najdłużej / najkrócej (tylko wiarygodne)
            WypelnijTop(PanelTop5, wiarygodne.OrderByDescending(s => s.MinutyMediana).Take(5));
            WypelnijTop(PanelDol5, wiarygodne.OrderBy(s => s.MinutyMediana).Take(5));
        }

        private void WypelnijTop(StackPanel panel, IEnumerable<StatystykaRow> top)
        {
            panel.Children.Clear();
            int i = 1;
            foreach (var s in top)
            {
                var tb = new TextBlock
                {
                    Text = $"  {i}.  {s.Nazwa}  —  {s.MinutyMediana} min  ({s.LiczbaProb} wiz.)",
                    FontSize = 11,
                    Foreground = (Brush)new BrushConverter().ConvertFrom("#374151")!,
                    Margin = new Thickness(0, 2, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                panel.Children.Add(tb);
                i++;
            }
            if (panel.Children.Count == 0)
                panel.Children.Add(new TextBlock { Text = "  brak danych", FontSize = 11, Foreground = Brushes.Gray, FontStyle = FontStyles.Italic });
        }

        // ════════════════════════════════════════════════════════════════════
        // HANDLERY
        // ════════════════════════════════════════════════════════════════════
        private void TxtSzukaj_Changed(object sender, TextChangedEventArgs e) => Filtruj();
        private void Filtr_Changed(object sender, RoutedEventArgs e) => Filtruj();
        private void BtnZamknij_Click(object sender, RoutedEventArgs e) => Close();

        private void BtnDebugger_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dbg = new DebuggerStatystykiRozladunkuWindow { Owner = this };
                dbg.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie udało się otworzyć debuggera:\n\n{ex.Message}",
                                "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            var r = MessageBox.Show(
                "Pobrać świeże dane GPS z Webfleet z ostatnich 2 miesięcy (60 dni)?\n\n" +
                "• Pobiera tracks dla każdego zmapowanego pojazdu\n" +
                "• Wykrywa wizyty u klientów (≤3,5 km, 5–180 min)\n" +
                "• Pomija pauzy i noclegi kierowców (poza 05:00–23:00)\n" +
                "• Liczy medianę z wszystkich wizyt\n" +
                "• Wiarygodne mediany (≥3 wizyt) zostaną automatycznie zapisane\n" +
                "  w karcie odbiorcy (KartotekaOdbiorcyDane.CzasRozladunkuMin)\n\n" +
                "Może potrwać 3–7 min (zależy od liczby pojazdów × dni).",
                "Odśwież z Webfleet — 2 miesiące",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;

            BtnOdswiez.IsEnabled = false;
            TxtStatus.Text = "🔄 Pobieram dane z Webfleet (2 miesiące)…";
            try
            {
                var svc = new HistoriaRozladunkuService();
                var progress = new Progress<string>(msg => TxtStatus.Text = $"🔄 {msg}");
                var wynik = await svc.OdswiezAsync(daysBack: 60, progress);

                // Reload widoku przed zapisem — żeby _wszystkie miało świeże dane
                await ZaladujAsync();

                // Zapisz wiarygodne mediany do KartotekaOdbiorcyDane
                TxtStatus.Text = "💾 Zapisuję mediany do karty odbiorcy…";
                int zapisanych = await ZapiszDoKartotekiAsync();

                TxtStatus.Text = $"✓ Gotowe — {wynik.WizytaWykrytych} wizyt, {wynik.KlientowZestymowanych} klientów z estymacją, {zapisanych} zapisanych do karty odbiorcy.";
                MessageBox.Show(
                    $"✓ Aktualizacja zakończona.\n\n" +
                    $"Webfleet (60 dni):\n" +
                    $"  • {wynik.PojazdowPrzetworzonych} pojazdów × {wynik.DniPrzetworzonych} dni\n" +
                    $"  • {wynik.WizytaWykrytych} wykrytych wizyt\n" +
                    $"  • {wynik.KlientowZestymowanych} klientów z estymacją\n\n" +
                    $"KartotekaOdbiorcyDane:\n" +
                    $"  • {zapisanych} median zapisanych jako oficjalne czasy rozładunku\n" +
                    $"  • Wartości używane od razu przez ETA i planowanie",
                    "Aktualizacja zakończona", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"✗ Błąd: {ex.Message}";
                MessageBox.Show($"Nie udało się odświeżyć:\n\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { BtnOdswiez.IsEnabled = true; }
        }

        /// <summary>
        /// Zapisuje wiarygodne mediany (LiczbaProb ≥ MinProbDoZaufania) do KartotekaOdbiorcyDane.CzasRozladunkuMin.
        /// Te wartości stają się „oficjalnym" czasem rozładunku per klient — używanym przez planowanie i ETA.
        /// </summary>
        private async Task<int> ZapiszDoKartotekiAsync()
        {
            var wiarygodne = _wszystkie
                .Where(s => s.LiczbaProb >= HistoriaRozladunkuService.MinProbDoZaufania)
                .ToList();
            if (wiarygodne.Count == 0) return 0;

            var svc = new CzasRozladunkuService();
            try { await svc.EnsureColumnAsync(); } catch { }

            int zapisanych = 0;
            foreach (var s in wiarygodne)
            {
                try
                {
                    // Walidacja zakresu — CzasRozladunkuService.ZapiszDlaKlientaAsync wymaga [MinMin, MaxMin]
                    int wartosc = Math.Clamp(s.MinutyMediana,
                        CzasRozladunkuService.MinMin, CzasRozladunkuService.MaxMin);
                    await svc.ZapiszDlaKlientaAsync(s.KlientId, wartosc);
                    zapisanych++;
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ZapiszDoKartoteki {s.KlientId}] {ex.Message}"); }
            }
            return zapisanych;
        }

        // ════════════════════════════════════════════════════════════════════
        // MODEL
        // ════════════════════════════════════════════════════════════════════
        public class StatystykaRow
        {
            public int KlientId { get; set; }
            public string Nazwa { get; set; } = "";
            public int MinutyMediana { get; set; }
            public int LiczbaProb { get; set; }
            public DateTime? OstatniRefresh { get; set; }
            public int? WKarcie { get; set; }

            public string MedianaDisplay => LiczbaProb == 0 ? "—" : $"{MinutyMediana} min";
            public string WKarcieDisplay => WKarcie.HasValue ? $"{WKarcie.Value} min" : "—";
            public string ZaufanieDisplay
            {
                get
                {
                    if (LiczbaProb == 0) return "⚪ brak wizyt";
                    return LiczbaProb >= HistoriaRozladunkuService.MinProbDoZaufania
                        ? "✓ wiarygodne"
                        : $"⏳ za mało ({LiczbaProb}/{HistoriaRozladunkuService.MinProbDoZaufania})";
                }
            }
            public string RefreshDisplay
            {
                get
                {
                    if (!OstatniRefresh.HasValue) return "—";
                    var dni = (DateTime.Now.Date - OstatniRefresh.Value.Date).TotalDays;
                    if (dni < 1) return "dziś";
                    if (dni < 2) return "wczoraj";
                    if (dni < 7) return $"{(int)dni} dni temu";
                    if (dni < 30) return $"{(int)(dni / 7)} tyg. temu";
                    return $"{(int)(dni / 30)} mies. temu";
                }
            }
        }
    }
}
