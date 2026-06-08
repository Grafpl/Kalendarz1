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

                _wszystkie = dane.OrderByDescending(s => s.MinutyMediana).ToList();
                Filtruj();
                AktualizujPodsumowanie();
                TxtStatus.Text = $"✓ Załadowano {_wszystkie.Count} klientów z estymacją.";
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
            // Upewnij się że tabela istnieje (dla świeżej instalacji)
            try { await new HistoriaRozladunkuService().EnsureTableAsync(); } catch { }

            await using var cn = new SqlConnection(ConnLibra);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT KlientId, MinutyMediana, LiczbaProb, OstatniRefresh FROM dbo.EstymacjeRozladunku", cn);
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                wynik.Add(new StatystykaRow
                {
                    KlientId = rd.GetInt32(0),
                    MinutyMediana = rd.GetInt32(1),
                    LiczbaProb = rd.GetInt32(2),
                    OstatniRefresh = rd.GetDateTime(3)
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
            TxtLiczbaKlientow.Text = _wszystkie.Count.ToString();
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

        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            var r = MessageBox.Show(
                "Pobrać świeże dane GPS z Webfleet z ostatnich 30 dni?\n\n" +
                "• Pobiera tracks dla każdego zmapowanego pojazdu\n" +
                "• Wykrywa wizyty u klientów (≤2 km, 5–180 min)\n" +
                "• Pomija pauzy i noclegi kierowców (poza 05:00–23:00)\n" +
                "• Liczy medianę z wszystkich wizyt\n\n" +
                "Może potrwać 1–3 min.",
                "Odśwież z Webfleet",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;

            BtnOdswiez.IsEnabled = false;
            TxtStatus.Text = "🔄 Pobieram dane z Webfleet…";
            try
            {
                var svc = new HistoriaRozladunkuService();
                var progress = new Progress<string>(msg => TxtStatus.Text = $"🔄 {msg}");
                var wynik = await svc.OdswiezAsync(daysBack: 30, progress);

                TxtStatus.Text = $"✓ Gotowe — {wynik.WizytaWykrytych} wizyt u {wynik.KlientowZestymowanych} klientów.";
                await ZaladujAsync();   // Reload widoku z nowymi danymi
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"✗ Błąd: {ex.Message}";
                MessageBox.Show($"Nie udało się odświeżyć:\n\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { BtnOdswiez.IsEnabled = true; }
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
            public DateTime OstatniRefresh { get; set; }

            public string MedianaDisplay => $"{MinutyMediana} min";
            public string ZaufanieDisplay => LiczbaProb >= HistoriaRozladunkuService.MinProbDoZaufania
                ? "✓ wiarygodne" : $"⏳ za mało ({LiczbaProb}/{HistoriaRozladunkuService.MinProbDoZaufania})";
            public string RefreshDisplay
            {
                get
                {
                    var dni = (DateTime.Now.Date - OstatniRefresh.Date).TotalDays;
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
