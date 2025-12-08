using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Data.SqlClient;
using LiveCharts;
using LiveCharts.Wpf;

namespace Kalendarz1.HandlowiecDashboard.Views
{
    public partial class AnalizaCenHandlowcaWindow : Window
    {
        private readonly string _connectionStringHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private readonly string _handlowiec;
        private bool _isInitialized = false;

        private static readonly string[] _nazwyMiesiecy = {
            "", "Styczen", "Luty", "Marzec", "Kwiecien", "Maj", "Czerwiec",
            "Lipiec", "Sierpien", "Wrzesien", "Pazdziernik", "Listopad", "Grudzien"
        };

        public AnalizaCenHandlowcaWindow(string handlowiec, int rok, int miesiac)
        {
            InitializeComponent();
            _handlowiec = handlowiec;
            txtHandlowiecNazwa.Text = handlowiec;
            Loaded += async (s, e) => await InitializeAsync(rok, miesiac);
        }

        private async Task InitializeAsync(int rok, int miesiac)
        {
            try
            {
                loadingOverlay.Visibility = Visibility.Visible;

                // Wypelnij filtry
                var lata = Enumerable.Range(2020, DateTime.Now.Year - 2020 + 1).Reverse().ToList();
                var miesiace = Enumerable.Range(1, 12).Select(m => new ComboItem { Value = m, Text = _nazwyMiesiecy[m] }).ToList();

                cmbRok.ItemsSource = lata;
                cmbRok.SelectedItem = rok;

                cmbMiesiac.ItemsSource = miesiace;
                cmbMiesiac.DisplayMemberPath = "Text";
                cmbMiesiac.SelectedValuePath = "Value";
                cmbMiesiac.SelectedValue = miesiac;

                _isInitialized = true;
                await OdswiezDaneAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad inicjalizacji: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                loadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void CmbFiltr_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            _ = OdswiezDaneAsync();
        }

        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            await OdswiezDaneAsync();
        }

        private async Task OdswiezDaneAsync()
        {
            if (cmbRok.SelectedItem == null || cmbMiesiac.SelectedValue == null) return;

            int rok = (int)cmbRok.SelectedItem;
            int miesiac = (int)cmbMiesiac.SelectedValue;

            try
            {
                loadingOverlay.Visibility = Visibility.Visible;

                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                // 1. Pobierz wszystkie transakcje
                var transakcje = await PobierzTransakcjeAsync(cn, rok, miesiac);

                // 2. Oblicz statystyki
                await AktualizujStatystykiAsync(cn, rok, miesiac, transakcje);

                // 3. Trend dzienny
                await AktualizujTrendDziennyAsync(cn, rok, miesiac);

                // 4. Top produkty
                await AktualizujTopProduktyAsync(cn, rok, miesiac);

                // 5. Top kontrahenci
                await AktualizujTopKontrahenciAsync(cn, rok, miesiac);

                // 6. Podsumowanie per produkt
                await AktualizujPerProduktAsync(cn, rok, miesiac);

                // Tabela transakcji
                gridTransakcje.ItemsSource = transakcje;
                txtLiczbaWynikow.Text = $"({transakcje.Count} wynikow)";
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

        private async Task<List<TransakcjaRow>> PobierzTransakcjeAsync(SqlConnection cn, int rok, int miesiac)
        {
            var lista = new List<TransakcjaRow>();

            var sql = @"
                SELECT DK.data AS Data, DK.nrdokwewn AS NrDokumentu, C.shortcut AS Kontrahent,
                       TW.kod + ' - ' + ISNULL(TW.nazwa, '') AS Towar,
                       DP.ilosc AS Ilosc, DP.cena AS Cena, DP.wartNetto AS Wartosc
                FROM [HANDEL].[HM].[DK] DK
                INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                INNER JOIN [HANDEL].[HM].[TW] TW ON DP.idtw = TW.ID
                INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
                LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
                WHERE YEAR(DK.data) = @Rok AND MONTH(DK.data) = @Miesiac
                  AND TW.katalog IN ('67095', '67153')
                  AND WYM.CDim_Handlowiec_Val = @Handlowiec
                ORDER BY DK.data DESC, DK.nrdokwewn";

            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@Rok", rok);
            cmd.Parameters.AddWithValue("@Miesiac", miesiac);
            cmd.Parameters.AddWithValue("@Handlowiec", _handlowiec);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                lista.Add(new TransakcjaRow
                {
                    Data = reader.GetDateTime(0),
                    NrDokumentu = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Kontrahent = reader.GetString(2),
                    Towar = reader.GetString(3),
                    Ilosc = reader.IsDBNull(4) ? 0m : Convert.ToDecimal(reader.GetValue(4)),
                    Cena = reader.IsDBNull(5) ? 0m : Convert.ToDecimal(reader.GetValue(5)),
                    Wartosc = reader.IsDBNull(6) ? 0m : Convert.ToDecimal(reader.GetValue(6))
                });
            }

            return lista;
        }

        private async Task AktualizujStatystykiAsync(SqlConnection cn, int rok, int miesiac, List<TransakcjaRow> transakcje)
        {
            if (transakcje.Count == 0)
            {
                txtSumaKg.Text = "0 kg";
                txtSumaWartosc.Text = "0 zl";
                txtSredniaCena.Text = "0.00 zl/kg";
                txtMinCena.Text = "0.00 zl/kg";
                txtMaxCena.Text = "0.00 zl/kg";
                txtLiczbaTransakcji.Text = "0";
                txtLiczbaKontrahentow.Text = "0 kontrahentow";
                return;
            }

            var sumaKg = transakcje.Sum(t => t.Ilosc);
            var sumaWartosc = transakcje.Sum(t => t.Wartosc);
            var sredniaCena = sumaKg > 0 ? sumaWartosc / sumaKg : 0;
            var minCena = transakcje.Where(t => t.Cena > 0).Min(t => t.Cena);
            var maxCena = transakcje.Max(t => t.Cena);
            var liczbaTransakcji = transakcje.Count;
            var liczbaKontrahentow = transakcje.Select(t => t.Kontrahent).Distinct().Count();

            txtSumaKg.Text = $"{sumaKg:N0} kg";
            txtSumaWartosc.Text = $"{sumaWartosc:N0} zl";
            txtSredniaCena.Text = $"{sredniaCena:F2} zl/kg";
            txtMinCena.Text = $"{minCena:F2} zl/kg";
            txtMaxCena.Text = $"{maxCena:F2} zl/kg";
            txtLiczbaTransakcji.Text = $"{liczbaTransakcji}";
            txtLiczbaKontrahentow.Text = $"{liczbaKontrahentow} kontrahentow";

            // Znajdz towary z min/max cena
            var towarMinCena = transakcje.Where(t => t.Cena == minCena).FirstOrDefault()?.Towar ?? "";
            var towarMaxCena = transakcje.Where(t => t.Cena == maxCena).FirstOrDefault()?.Towar ?? "";
            txtMinCenaTowar.Text = towarMinCena.Length > 30 ? towarMinCena.Substring(0, 30) + "..." : towarMinCena;
            txtMaxCenaTowar.Text = towarMaxCena.Length > 30 ? towarMaxCena.Substring(0, 30) + "..." : towarMaxCena;

            // Porownanie z poprzednim miesiacem
            var poprzedniMiesiac = miesiac == 1 ? 12 : miesiac - 1;
            var poprzedniRok = miesiac == 1 ? rok - 1 : rok;

            var sqlPoprzedni = @"
                SELECT SUM(DP.ilosc) AS SumaKg, SUM(DP.wartNetto) AS SumaWartosc
                FROM [HANDEL].[HM].[DK] DK
                INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                INNER JOIN [HANDEL].[HM].[TW] TW ON DP.idtw = TW.ID
                LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
                WHERE YEAR(DK.data) = @Rok AND MONTH(DK.data) = @Miesiac
                  AND TW.katalog IN ('67095', '67153')
                  AND WYM.CDim_Handlowiec_Val = @Handlowiec";

            await using var cmd = new SqlCommand(sqlPoprzedni, cn);
            cmd.Parameters.AddWithValue("@Rok", poprzedniRok);
            cmd.Parameters.AddWithValue("@Miesiac", poprzedniMiesiac);
            cmd.Parameters.AddWithValue("@Handlowiec", _handlowiec);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var poprzedniKg = reader.IsDBNull(0) ? 0m : Convert.ToDecimal(reader.GetValue(0));
                var poprzedniWartosc = reader.IsDBNull(1) ? 0m : Convert.ToDecimal(reader.GetValue(1));
                var poprzedniaSrednia = poprzedniKg > 0 ? poprzedniWartosc / poprzedniKg : 0;

                if (poprzedniKg > 0)
                {
                    var zmianaKg = (sumaKg - poprzedniKg) / poprzedniKg * 100;
                    txtSumaKgZmiana.Text = $"vs poprz: {(zmianaKg >= 0 ? "+" : "")}{zmianaKg:F1}%";
                    txtSumaKgZmiana.Foreground = zmianaKg >= 0 ? new SolidColorBrush(Color.FromRgb(78, 205, 196)) : new SolidColorBrush(Color.FromRgb(255, 107, 107));
                }

                if (poprzedniWartosc > 0)
                {
                    var zmianaWartosc = (sumaWartosc - poprzedniWartosc) / poprzedniWartosc * 100;
                    txtSumaWartoscZmiana.Text = $"vs poprz: {(zmianaWartosc >= 0 ? "+" : "")}{zmianaWartosc:F1}%";
                    txtSumaWartoscZmiana.Foreground = zmianaWartosc >= 0 ? new SolidColorBrush(Color.FromRgb(78, 205, 196)) : new SolidColorBrush(Color.FromRgb(255, 107, 107));
                }

                if (poprzedniaSrednia > 0)
                {
                    var zmianaCena = (sredniaCena - poprzedniaSrednia) / poprzedniaSrednia * 100;
                    txtSredniaCenaZmiana.Text = $"vs poprz: {(zmianaCena >= 0 ? "+" : "")}{zmianaCena:F1}%";
                    txtSredniaCenaZmiana.Foreground = zmianaCena >= 0 ? new SolidColorBrush(Color.FromRgb(78, 205, 196)) : new SolidColorBrush(Color.FromRgb(255, 107, 107));
                }
            }
        }

        private async Task AktualizujTrendDziennyAsync(SqlConnection cn, int rok, int miesiac)
        {
            var sql = @"
                SELECT CAST(DK.data AS DATE) AS Dzien,
                       AVG(DP.cena) AS SredniaCena,
                       SUM(DP.ilosc) AS SumaKg
                FROM [HANDEL].[HM].[DK] DK
                INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                INNER JOIN [HANDEL].[HM].[TW] TW ON DP.idtw = TW.ID
                LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
                WHERE YEAR(DK.data) = @Rok AND MONTH(DK.data) = @Miesiac
                  AND TW.katalog IN ('67095', '67153')
                  AND WYM.CDim_Handlowiec_Val = @Handlowiec
                GROUP BY CAST(DK.data AS DATE)
                ORDER BY Dzien";

            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@Rok", rok);
            cmd.Parameters.AddWithValue("@Miesiac", miesiac);
            cmd.Parameters.AddWithValue("@Handlowiec", _handlowiec);

            var labels = new List<string>();
            var ceny = new ChartValues<decimal>();
            var kg = new ChartValues<decimal>();

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var dzien = reader.GetDateTime(0);
                labels.Add(dzien.ToString("dd.MM"));
                ceny.Add(reader.IsDBNull(1) ? 0m : Convert.ToDecimal(reader.GetValue(1)));
                kg.Add(reader.IsDBNull(2) ? 0m : Convert.ToDecimal(reader.GetValue(2)));
            }

            chartTrendCena.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Srednia cena",
                    Values = ceny,
                    Stroke = new SolidColorBrush(Color.FromRgb(78, 205, 196)),
                    Fill = new SolidColorBrush(Color.FromArgb(50, 78, 205, 196)),
                    PointGeometry = DefaultGeometries.Circle,
                    PointGeometrySize = 6,
                    DataLabels = false
                }
            };
            axisXTrendCena.Labels = labels;

            chartTrendKg.Series = new SeriesCollection
            {
                new ColumnSeries
                {
                    Title = "Ilosc kg",
                    Values = kg,
                    Fill = new SolidColorBrush(Color.FromRgb(244, 162, 97)),
                    DataLabels = false
                }
            };
            axisXTrendKg.Labels = labels;
        }

        private async Task AktualizujTopProduktyAsync(SqlConnection cn, int rok, int miesiac)
        {
            var sql = @"
                SELECT TOP 10 TW.kod + ' - ' + LEFT(ISNULL(TW.nazwa, ''), 20) AS Towar,
                       SUM(DP.wartNetto) AS SumaWartosc
                FROM [HANDEL].[HM].[DK] DK
                INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                INNER JOIN [HANDEL].[HM].[TW] TW ON DP.idtw = TW.ID
                LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
                WHERE YEAR(DK.data) = @Rok AND MONTH(DK.data) = @Miesiac
                  AND TW.katalog IN ('67095', '67153')
                  AND WYM.CDim_Handlowiec_Val = @Handlowiec
                GROUP BY TW.kod, TW.nazwa
                ORDER BY SumaWartosc DESC";

            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@Rok", rok);
            cmd.Parameters.AddWithValue("@Miesiac", miesiac);
            cmd.Parameters.AddWithValue("@Handlowiec", _handlowiec);

            var labels = new List<string>();
            var values = new ChartValues<double>();

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var towar = reader.GetString(0);
                labels.Add(towar.Length > 25 ? towar.Substring(0, 25) + "..." : towar);
                values.Add(reader.IsDBNull(1) ? 0 : Convert.ToDouble(reader.GetValue(1)));
            }

            chartTopProdukty.Series = new SeriesCollection
            {
                new RowSeries
                {
                    Title = "Wartosc",
                    Values = values,
                    Fill = new SolidColorBrush(Color.FromRgb(78, 205, 196)),
                    DataLabels = true,
                    LabelPoint = p => $"{p.X:N0}",
                    Foreground = Brushes.White
                }
            };
            axisYTopProdukty.Labels = labels;
        }

        private async Task AktualizujTopKontrahenciAsync(SqlConnection cn, int rok, int miesiac)
        {
            var sql = @"
                SELECT TOP 10 C.shortcut AS Kontrahent,
                       SUM(DP.wartNetto) AS SumaWartosc
                FROM [HANDEL].[HM].[DK] DK
                INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                INNER JOIN [HANDEL].[HM].[TW] TW ON DP.idtw = TW.ID
                INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
                LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
                WHERE YEAR(DK.data) = @Rok AND MONTH(DK.data) = @Miesiac
                  AND TW.katalog IN ('67095', '67153')
                  AND WYM.CDim_Handlowiec_Val = @Handlowiec
                GROUP BY C.shortcut
                ORDER BY SumaWartosc DESC";

            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@Rok", rok);
            cmd.Parameters.AddWithValue("@Miesiac", miesiac);
            cmd.Parameters.AddWithValue("@Handlowiec", _handlowiec);

            var labels = new List<string>();
            var values = new ChartValues<double>();

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var kontrahent = reader.GetString(0);
                labels.Add(kontrahent.Length > 20 ? kontrahent.Substring(0, 20) + "..." : kontrahent);
                values.Add(reader.IsDBNull(1) ? 0 : Convert.ToDouble(reader.GetValue(1)));
            }

            chartTopKontrahenci.Series = new SeriesCollection
            {
                new RowSeries
                {
                    Title = "Wartosc",
                    Values = values,
                    Fill = new SolidColorBrush(Color.FromRgb(244, 162, 97)),
                    DataLabels = true,
                    LabelPoint = p => $"{p.X:N0}",
                    Foreground = Brushes.White
                }
            };
            axisYTopKontrahenci.Labels = labels;
        }

        private async Task AktualizujPerProduktAsync(SqlConnection cn, int rok, int miesiac)
        {
            var sql = @"
                SELECT TW.kod AS Kod, TW.kod + ' - ' + ISNULL(TW.nazwa, '') AS Towar,
                       SUM(DP.ilosc) AS SumaKg,
                       AVG(DP.cena) AS SredniaCena,
                       MIN(DP.cena) AS MinCena,
                       MAX(DP.cena) AS MaxCena,
                       SUM(DP.wartNetto) AS SumaWartosc,
                       COUNT(*) AS LiczbaTransakcji
                FROM [HANDEL].[HM].[DK] DK
                INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                INNER JOIN [HANDEL].[HM].[TW] TW ON DP.idtw = TW.ID
                LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
                WHERE YEAR(DK.data) = @Rok AND MONTH(DK.data) = @Miesiac
                  AND TW.katalog IN ('67095', '67153')
                  AND WYM.CDim_Handlowiec_Val = @Handlowiec
                GROUP BY TW.kod, TW.nazwa
                ORDER BY SumaWartosc DESC";

            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@Rok", rok);
            cmd.Parameters.AddWithValue("@Miesiac", miesiac);
            cmd.Parameters.AddWithValue("@Handlowiec", _handlowiec);

            var lista = new List<ProduktPodsumowanieRow>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                lista.Add(new ProduktPodsumowanieRow
                {
                    Kod = reader.GetString(0),
                    Towar = reader.GetString(1),
                    SumaKg = reader.IsDBNull(2) ? 0m : Convert.ToDecimal(reader.GetValue(2)),
                    SredniaCena = reader.IsDBNull(3) ? 0m : Convert.ToDecimal(reader.GetValue(3)),
                    MinCena = reader.IsDBNull(4) ? 0m : Convert.ToDecimal(reader.GetValue(4)),
                    MaxCena = reader.IsDBNull(5) ? 0m : Convert.ToDecimal(reader.GetValue(5)),
                    SumaWartosc = reader.IsDBNull(6) ? 0m : Convert.ToDecimal(reader.GetValue(6)),
                    LiczbaTransakcji = reader.GetInt32(7)
                });
            }

            gridPerProdukt.ItemsSource = lista;
        }
    }

    // Klasa danych dla tabeli transakcji
    public class TransakcjaRow
    {
        public DateTime Data { get; set; }
        public string NrDokumentu { get; set; }
        public string Kontrahent { get; set; }
        public string Towar { get; set; }
        public decimal Ilosc { get; set; }
        public decimal Cena { get; set; }
        public decimal Wartosc { get; set; }

        public string DataTekst => Data.ToString("dd.MM.yyyy");
        public string IloscTekst => $"{Ilosc:N2}";
        public string CenaTekst => $"{Cena:F2}";
        public string WartoscTekst => $"{Wartosc:N2}";
    }

    // Klasa danych dla podsumowania per produkt
    public class ProduktPodsumowanieRow
    {
        public string Kod { get; set; }
        public string Towar { get; set; }
        public decimal SumaKg { get; set; }
        public decimal SredniaCena { get; set; }
        public decimal MinCena { get; set; }
        public decimal MaxCena { get; set; }
        public decimal SumaWartosc { get; set; }
        public int LiczbaTransakcji { get; set; }

        public string SumaKgTekst => $"{SumaKg:N2}";
        public string SredniaCenaTekst => $"{SredniaCena:F2}";
        public string MinCenaTekst => $"{MinCena:F2}";
        public string MaxCenaTekst => $"{MaxCena:F2}";
        public string SumaWartoscTekst => $"{SumaWartosc:N2}";
    }
}
