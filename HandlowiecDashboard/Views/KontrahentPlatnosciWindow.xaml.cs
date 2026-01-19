using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Microsoft.Data.SqlClient;
using LiveCharts;
using LiveCharts.Wpf;

namespace Kalendarz1.HandlowiecDashboard.Views
{
    public partial class KontrahentPlatnosciWindow : Window
    {
        private readonly string _connectionStringHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private readonly string _kontrahent;
        private readonly string _handlowiec;

        public KontrahentPlatnosciWindow(string kontrahent, string handlowiec)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            _kontrahent = kontrahent;
            _handlowiec = handlowiec;
            txtKontrahentNazwa.Text = kontrahent;
            txtHandlowiec.Text = $"Handlowiec: {handlowiec}";
            Loaded += async (s, e) => await OdswiezDaneAsync();
        }

        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            await OdswiezDaneAsync();
        }

        private async Task OdswiezDaneAsync()
        {
            try
            {
                loadingOverlay.Visibility = Visibility.Visible;

                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                // 1. Pobierz dane kontrahenta
                await PobierzDaneKontrahentaAsync(cn);

                // 2. Pobierz faktury
                var faktury = await PobierzFakturyAsync(cn);

                // 3. Oblicz aging
                ObliczAging(faktury);

                // 4. Pobierz trend
                await PobierzTrendAsync(cn);

                // 5. Pobierz historie platnosci
                await PobierzHistorieAsync(cn);

                // Wyswietl faktury
                gridFaktury.ItemsSource = faktury;
                txtLiczbaWynikow.Text = $"({faktury.Count} dokumentow)";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad wczytywania danych: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                loadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private async Task PobierzDaneKontrahentaAsync(SqlConnection cn)
        {
            var sql = @"
                SELECT C.LimitAmount,
                       SUM(CASE WHEN DK.walbrutto - ISNULL(PN.KwotaRozliczona, 0) > 0.01 THEN DK.walbrutto - ISNULL(PN.KwotaRozliczona, 0) ELSE 0 END) AS DoZaplaty,
                       SUM(CASE WHEN DK.walbrutto - ISNULL(PN.KwotaRozliczona, 0) > 0.01 AND GETDATE() <= ISNULL(PN.TerminPrawdziwy, DK.plattermin) THEN DK.walbrutto - ISNULL(PN.KwotaRozliczona, 0) ELSE 0 END) AS Terminowe,
                       SUM(CASE WHEN DK.walbrutto - ISNULL(PN.KwotaRozliczona, 0) > 0.01 AND GETDATE() > ISNULL(PN.TerminPrawdziwy, DK.plattermin) THEN DK.walbrutto - ISNULL(PN.KwotaRozliczona, 0) ELSE 0 END) AS Przeterminowane,
                       MAX(CASE WHEN DK.walbrutto - ISNULL(PN.KwotaRozliczona, 0) > 0.01 AND GETDATE() > ISNULL(PN.TerminPrawdziwy, DK.plattermin) THEN DATEDIFF(day, ISNULL(PN.TerminPrawdziwy, DK.plattermin), GETDATE()) ELSE 0 END) AS MaxDni,
                       COUNT(DISTINCT DK.id) AS IloscFaktur
                FROM [HANDEL].[SSCommon].[STContractors] C
                LEFT JOIN [HANDEL].[HM].[DK] DK ON DK.khid = C.id AND DK.anulowany = 0
                LEFT JOIN (
                    SELECT dkid, SUM(ISNULL(kwotarozl, 0)) AS KwotaRozliczona, MAX(Termin) AS TerminPrawdziwy
                    FROM [HANDEL].[HM].[PN]
                    GROUP BY dkid
                ) PN ON PN.dkid = DK.id
                WHERE C.shortcut = @Kontrahent
                GROUP BY C.LimitAmount";

            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@Kontrahent", _kontrahent);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var limitKredytu = reader.IsDBNull(0) ? 0m : Convert.ToDecimal(reader.GetValue(0));
                var doZaplaty = reader.IsDBNull(1) ? 0m : Convert.ToDecimal(reader.GetValue(1));
                var terminowe = reader.IsDBNull(2) ? 0m : Convert.ToDecimal(reader.GetValue(2));
                var przeterminowane = reader.IsDBNull(3) ? 0m : Convert.ToDecimal(reader.GetValue(3));
                var maxDni = reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader.GetValue(4));
                var iloscFaktur = reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader.GetValue(5));

                txtLimitKredytu.Text = $"{limitKredytu:N0} zl";
                txtDoZaplaty.Text = $"{doZaplaty:N0} zl";
                txtTerminowe.Text = $"{terminowe:N0} zl";
                txtPrzeterminowane.Text = $"{przeterminowane:N0} zl";
                txtMaxDni.Text = $"{maxDni} dni";
                txtIloscFaktur.Text = $"{iloscFaktur} faktur";

                if (doZaplaty > 0)
                {
                    txtTerminoweProcent.Text = $"{terminowe / doZaplaty * 100:F0}%";
                    txtPrzeterminowaneProcent.Text = $"{przeterminowane / doZaplaty * 100:F0}%";
                }

                if (limitKredytu > 0)
                {
                    var wykorzystanie = doZaplaty / limitKredytu * 100;
                    txtWykorzystanieLimit.Text = $"Wykorzystanie: {wykorzystanie:F0}%";
                    if (doZaplaty > limitKredytu)
                    {
                        txtPrzekroczonyLimit.Text = $"Przekroczono o {doZaplaty - limitKredytu:N0} zl!";
                    }
                }
            }
        }

        private async Task<List<FakturaRow>> PobierzFakturyAsync(SqlConnection cn)
        {
            var lista = new List<FakturaRow>();

            var sql = @"
                SELECT DK.kod AS NrDokumentu,
                       DK.data AS Data,
                       ISNULL(PN.TerminPrawdziwy, DK.plattermin) AS Termin,
                       DK.walbrutto AS KwotaBrutto,
                       ISNULL(PN.KwotaRozliczona, 0) AS Rozliczone,
                       DK.walbrutto - ISNULL(PN.KwotaRozliczona, 0) AS DoZaplaty,
                       CASE
                           WHEN DK.walbrutto - ISNULL(PN.KwotaRozliczona, 0) <= 0.01 THEN 'Rozliczona'
                           WHEN GETDATE() > ISNULL(PN.TerminPrawdziwy, DK.plattermin) THEN 'Przeterminowana'
                           ELSE 'W terminie'
                       END AS Status,
                       CASE
                           WHEN DK.walbrutto - ISNULL(PN.KwotaRozliczona, 0) > 0.01 AND GETDATE() > ISNULL(PN.TerminPrawdziwy, DK.plattermin)
                           THEN DATEDIFF(day, ISNULL(PN.TerminPrawdziwy, DK.plattermin), GETDATE())
                           ELSE 0
                       END AS DniPrzeterminowania
                FROM [HANDEL].[HM].[DK] DK
                INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
                LEFT JOIN (
                    SELECT dkid, SUM(ISNULL(kwotarozl, 0)) AS KwotaRozliczona, MAX(Termin) AS TerminPrawdziwy
                    FROM [HANDEL].[HM].[PN]
                    GROUP BY dkid
                ) PN ON PN.dkid = DK.id
                WHERE C.shortcut = @Kontrahent AND DK.anulowany = 0
                ORDER BY DK.data DESC";

            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@Kontrahent", _kontrahent);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var doZaplaty = reader.IsDBNull(5) ? 0m : Convert.ToDecimal(reader.GetValue(5));
                var status = reader.GetString(6);
                var dniPrzet = reader.IsDBNull(7) ? 0 : Convert.ToInt32(reader.GetValue(7));

                lista.Add(new FakturaRow
                {
                    NrDokumentu = reader.IsDBNull(0) ? "" : reader.GetString(0),
                    Data = reader.GetDateTime(1),
                    Termin = reader.IsDBNull(2) ? DateTime.Now : reader.GetDateTime(2),
                    KwotaBrutto = reader.IsDBNull(3) ? 0m : Convert.ToDecimal(reader.GetValue(3)),
                    Rozliczone = reader.IsDBNull(4) ? 0m : Convert.ToDecimal(reader.GetValue(4)),
                    DoZaplaty = doZaplaty > 0 ? doZaplaty : 0,
                    Status = status,
                    DniPrzeterminowania = dniPrzet > 0 ? dniPrzet : (int?)null,
                    JestPrzeterminowana = status == "Przeterminowana"
                });
            }

            return lista;
        }

        private void ObliczAging(List<FakturaRow> faktury)
        {
            var aging030 = faktury.Where(f => f.JestPrzeterminowana && f.DniPrzeterminowania <= 30).Sum(f => f.DoZaplaty);
            var aging3160 = faktury.Where(f => f.JestPrzeterminowana && f.DniPrzeterminowania > 30 && f.DniPrzeterminowania <= 60).Sum(f => f.DoZaplaty);
            var aging6190 = faktury.Where(f => f.JestPrzeterminowana && f.DniPrzeterminowania > 60 && f.DniPrzeterminowania <= 90).Sum(f => f.DoZaplaty);
            var aging90Plus = faktury.Where(f => f.JestPrzeterminowana && f.DniPrzeterminowania > 90).Sum(f => f.DoZaplaty);

            var count030 = faktury.Count(f => f.JestPrzeterminowana && f.DniPrzeterminowania <= 30);
            var count3160 = faktury.Count(f => f.JestPrzeterminowana && f.DniPrzeterminowania > 30 && f.DniPrzeterminowania <= 60);
            var count6190 = faktury.Count(f => f.JestPrzeterminowana && f.DniPrzeterminowania > 60 && f.DniPrzeterminowania <= 90);
            var count90Plus = faktury.Count(f => f.JestPrzeterminowana && f.DniPrzeterminowania > 90);

            txtAging030.Text = $"{aging030:N0} zl";
            txtAging030Faktury.Text = $"{count030} faktur";
            txtAging3160.Text = $"{aging3160:N0} zl";
            txtAging3160Faktury.Text = $"{count3160} faktur";
            txtAging6190.Text = $"{aging6190:N0} zl";
            txtAging6190Faktury.Text = $"{count6190} faktur";
            txtAging90Plus.Text = $"{aging90Plus:N0} zl";
            txtAging90PlusFaktury.Text = $"{count90Plus} faktur";
        }

        private async Task PobierzTrendAsync(SqlConnection cn)
        {
            var sql = @"
                SELECT YEAR(DK.data) AS Rok, MONTH(DK.data) AS Miesiac,
                       SUM(DK.walbrutto - ISNULL(PN.KwotaRozliczona, 0)) AS Saldo
                FROM [HANDEL].[HM].[DK] DK
                INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
                LEFT JOIN (
                    SELECT dkid, SUM(ISNULL(kwotarozl, 0)) AS KwotaRozliczona
                    FROM [HANDEL].[HM].[PN]
                    GROUP BY dkid
                ) PN ON PN.dkid = DK.id
                WHERE C.shortcut = @Kontrahent AND DK.anulowany = 0
                  AND DK.data >= DATEADD(month, -6, GETDATE())
                GROUP BY YEAR(DK.data), MONTH(DK.data)
                ORDER BY Rok, Miesiac";

            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@Kontrahent", _kontrahent);

            var labels = new List<string>();
            var values = new ChartValues<decimal>();
            string[] nazwyMiesiecy = { "", "Sty", "Lut", "Mar", "Kwi", "Maj", "Cze", "Lip", "Sie", "Wrz", "Paz", "Lis", "Gru" };

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var rok = reader.GetInt32(0);
                var miesiac = reader.GetInt32(1);
                var saldo = reader.IsDBNull(2) ? 0m : Convert.ToDecimal(reader.GetValue(2));

                labels.Add($"{nazwyMiesiecy[miesiac]} {rok % 100}");
                values.Add(saldo > 0 ? saldo : 0);
            }

            chartTrendSaldo.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Saldo",
                    Values = values,
                    Stroke = new SolidColorBrush(Color.FromRgb(244, 162, 97)),
                    Fill = new SolidColorBrush(Color.FromArgb(50, 244, 162, 97)),
                    PointGeometry = DefaultGeometries.Circle,
                    PointGeometrySize = 8,
                    DataLabels = true,
                    LabelPoint = p => $"{p.Y:N0}",
                    Foreground = Brushes.White
                }
            };
            axisXTrend.Labels = labels;
        }

        private async Task PobierzHistorieAsync(SqlConnection cn)
        {
            var sql = @"
                SELECT SUM(PN.kwotarozl) AS SumaZaplacona,
                       AVG(DATEDIFF(day, DK.data, PN.data)) AS SredniCzasPlatnosci,
                       MAX(PN.data) AS OstatniaPlatnosc,
                       COUNT(CASE WHEN PN.data > ISNULL(PN.Termin, DK.plattermin) THEN 1 END) AS LiczbaOpoznien
                FROM [HANDEL].[HM].[PN] PN
                INNER JOIN [HANDEL].[HM].[DK] DK ON PN.dkid = DK.id
                INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
                WHERE C.shortcut = @Kontrahent
                  AND PN.data >= DATEADD(month, -12, GETDATE())";

            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@Kontrahent", _kontrahent);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var sumaZaplacona = reader.IsDBNull(0) ? 0m : Convert.ToDecimal(reader.GetValue(0));
                var sredniCzas = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1));
                var ostatniaPlatnosc = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2);
                var liczbaOpoznien = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3));

                txtSumaZaplacona.Text = $"{sumaZaplacona:N0} zl";
                txtSredniCzas.Text = $"{sredniCzas} dni";
                txtOstatniaPlatnosc.Text = ostatniaPlatnosc.HasValue ? ostatniaPlatnosc.Value.ToString("dd.MM.yyyy") : "-";
                txtLiczbaOpoznien.Text = liczbaOpoznien.ToString();
            }
        }
    }

    // Klasa danych dla faktury
    public class FakturaRow
    {
        public string NrDokumentu { get; set; }
        public DateTime Data { get; set; }
        public DateTime Termin { get; set; }
        public decimal KwotaBrutto { get; set; }
        public decimal Rozliczone { get; set; }
        public decimal DoZaplaty { get; set; }
        public string Status { get; set; }
        public int? DniPrzeterminowania { get; set; }
        public bool JestPrzeterminowana { get; set; }

        public string DataTekst => Data.ToString("dd.MM.yyyy");
        public string TerminTekst => Termin.ToString("dd.MM.yyyy");
        public string KwotaBruttoTekst => $"{KwotaBrutto:N2}";
        public string RozliczoneTekst => $"{Rozliczone:N2}";
        public string DoZaplatyTekst => $"{DoZaplaty:N2}";
    }
}
