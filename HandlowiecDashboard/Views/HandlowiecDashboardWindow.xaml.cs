using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Data.SqlClient;
using LiveCharts;
using LiveCharts.Wpf;

namespace Kalendarz1.HandlowiecDashboard.Views
{
    public partial class HandlowiecDashboardWindow : Window
    {
        private readonly string _connectionStringHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private readonly CultureInfo _kulturaPL = new CultureInfo("pl-PL");
        private bool _isInitialized = false;
        private string _wybranyHandlowiec = "— Wszyscy —";

        private static readonly string[] _nazwyMiesiecy = {
            "", "Styczen", "Luty", "Marzec", "Kwiecien", "Maj", "Czerwiec",
            "Lipiec", "Sierpien", "Wrzesien", "Pazdziernik", "Listopad", "Grudzien"
        };

        private readonly Color[] _kolory = {
            (Color)ColorConverter.ConvertFromString("#3498db"),
            (Color)ColorConverter.ConvertFromString("#2ecc71"),
            (Color)ColorConverter.ConvertFromString("#e74c3c"),
            (Color)ColorConverter.ConvertFromString("#f39c12"),
            (Color)ColorConverter.ConvertFromString("#9b59b6"),
            (Color)ColorConverter.ConvertFromString("#1abc9c"),
            (Color)ColorConverter.ConvertFromString("#e67e22"),
            (Color)ColorConverter.ConvertFromString("#95a5a6"),
            (Color)ColorConverter.ConvertFromString("#34495e"),
            (Color)ColorConverter.ConvertFromString("#16a085")
        };

        public HandlowiecDashboardWindow()
        {
            InitializeComponent();
            Loaded += Window_Loaded;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                loadingOverlay.Visibility = Visibility.Visible;

                // Wypelnij combobox handlowcow
                await WypelnijHandlowcowAsync();

                // Wypelnij combobox lat i miesiecy
                WypelnijLataIMiesiace();

                _isInitialized = true;

                // Zaladuj dane pierwszej zakladki
                await OdswiezSprzedazMiesiecznaAsync();
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

        private async System.Threading.Tasks.Task WypelnijHandlowcowAsync()
        {
            var handlowcy = new List<string> { "— Wszyscy —" };

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var sql = @"
                    SELECT DISTINCT WYM.CDim_Handlowiec_Val
                    FROM [HANDEL].[SSCommon].[ContractorClassification] WYM
                    WHERE WYM.CDim_Handlowiec_Val IS NOT NULL
                      AND WYM.CDim_Handlowiec_Val != ''
                      AND WYM.CDim_Handlowiec_Val != 'Ogolne'
                    ORDER BY WYM.CDim_Handlowiec_Val";

                await using var cmd = new SqlCommand(sql, cn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0))
                        handlowcy.Add(reader.GetString(0));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Blad pobierania handlowcow: {ex.Message}");
            }

            cmbHandlowiec.ItemsSource = handlowcy;
            cmbHandlowiec.SelectedIndex = 0;
        }

        private void WypelnijLataIMiesiace()
        {
            var lata = Enumerable.Range(2020, DateTime.Now.Year - 2020 + 1).Reverse().ToList();
            var miesiace = Enumerable.Range(1, 12).Select(m => new { Value = m, Text = _nazwyMiesiecy[m] }).ToList();

            // Sprzedaz miesieczna
            cmbRokSprzedaz.ItemsSource = lata;
            cmbRokSprzedaz.SelectedItem = DateTime.Now.Year;
            cmbMiesiacSprzedaz.ItemsSource = miesiace;
            cmbMiesiacSprzedaz.DisplayMemberPath = "Text";
            cmbMiesiacSprzedaz.SelectedValuePath = "Value";
            cmbMiesiacSprzedaz.SelectedValue = DateTime.Now.Month;

            // Top 10
            cmbRokTop10.ItemsSource = lata;
            cmbRokTop10.SelectedItem = DateTime.Now.Year;
            cmbMiesiacTop10.ItemsSource = miesiace;
            cmbMiesiacTop10.DisplayMemberPath = "Text";
            cmbMiesiacTop10.SelectedValuePath = "Value";
            cmbMiesiacTop10.SelectedValue = DateTime.Now.Month;
            WypelnijTowary();

            // Udzial handlowcow
            cmbRokUdzial.ItemsSource = lata;
            cmbRokUdzial.SelectedItem = DateTime.Now.Year;
            cmbMiesiacUdzial.ItemsSource = miesiace;
            cmbMiesiacUdzial.DisplayMemberPath = "Text";
            cmbMiesiacUdzial.SelectedValuePath = "Value";
            cmbMiesiacUdzial.SelectedValue = DateTime.Now.Month;

            // Analiza cen
            cmbRokCeny.ItemsSource = lata;
            cmbRokCeny.SelectedItem = DateTime.Now.Year;
            cmbMiesiacCeny.ItemsSource = miesiace;
            cmbMiesiacCeny.DisplayMemberPath = "Text";
            cmbMiesiacCeny.SelectedValuePath = "Value";
            cmbMiesiacCeny.SelectedValue = DateTime.Now.Month;

            // Swieze vs Mrozone
            cmbRokSM.ItemsSource = lata;
            cmbRokSM.SelectedItem = DateTime.Now.Year;
            cmbMiesiacSM.ItemsSource = miesiace;
            cmbMiesiacSM.DisplayMemberPath = "Text";
            cmbMiesiacSM.SelectedValuePath = "Value";
            cmbMiesiacSM.SelectedValue = DateTime.Now.Month;
        }

        private void WypelnijTowary()
        {
            var towary = new List<dynamic>
            {
                new { Value = 0, Text = "Wszystkie towary" }
            };

            try
            {
                using var cn = new SqlConnection(_connectionStringHandel);
                cn.Open();

                var sql = @"
                    SELECT DISTINCT TW.ID, TW.kod
                    FROM [HANDEL].[HM].[TW] TW
                    WHERE TW.katalog IN ('67095', '67153')
                    ORDER BY TW.kod";

                using var cmd = new SqlCommand(sql, cn);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    towary.Add(new { Value = reader.GetInt32(0), Text = reader.GetString(1) });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Blad pobierania towarow: {ex.Message}");
            }

            cmbTowarTop10.ItemsSource = towary;
            cmbTowarTop10.DisplayMemberPath = "Text";
            cmbTowarTop10.SelectedValuePath = "Value";
            cmbTowarTop10.SelectedIndex = 0;
        }

        #region Event Handlers

        private void CmbHandlowiec_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            _wybranyHandlowiec = cmbHandlowiec.SelectedItem?.ToString() ?? "— Wszyscy —";
            OdswiezAktualnaZakladke();
        }

        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            OdswiezAktualnaZakladke();
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            if (e.Source != tabControl) return;
            OdswiezAktualnaZakladke();
        }

        private void CmbRokSprzedaz_SelectionChanged(object sender, SelectionChangedEventArgs e) => OdswiezJesliGotowe();
        private void CmbMiesiacSprzedaz_SelectionChanged(object sender, SelectionChangedEventArgs e) => OdswiezJesliGotowe();
        private void CmbTop10_SelectionChanged(object sender, SelectionChangedEventArgs e) => OdswiezJesliGotowe();
        private void CmbUdzial_SelectionChanged(object sender, SelectionChangedEventArgs e) => OdswiezJesliGotowe();
        private void CmbCeny_SelectionChanged(object sender, SelectionChangedEventArgs e) => OdswiezJesliGotowe();
        private void CmbSM_SelectionChanged(object sender, SelectionChangedEventArgs e) => OdswiezJesliGotowe();

        private void OdswiezJesliGotowe()
        {
            if (!_isInitialized) return;
            OdswiezAktualnaZakladke();
        }

        private async void OdswiezAktualnaZakladke()
        {
            if (!_isInitialized) return;

            try
            {
                loadingOverlay.Visibility = Visibility.Visible;

                switch (tabControl.SelectedIndex)
                {
                    case 0: await OdswiezSprzedazMiesiecznaAsync(); break;
                    case 1: await OdswiezTop10Async(); break;
                    case 2: await OdswiezUdzialHandlowcowAsync(); break;
                    case 3: await OdswiezAnalizeCenAsync(); break;
                    case 4: await OdswiezSwiezeMrozoneAsync(); break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                loadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        #endregion

        #region Sprzedaz Miesieczna

        private async System.Threading.Tasks.Task OdswiezSprzedazMiesiecznaAsync()
        {
            if (cmbRokSprzedaz.SelectedItem == null || cmbMiesiacSprzedaz.SelectedValue == null) return;

            int rok = (int)cmbRokSprzedaz.SelectedItem;
            int miesiac = (int)cmbMiesiacSprzedaz.SelectedValue;

            var series = new SeriesCollection();
            decimal suma = 0;

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var sql = @"
                    SELECT
                        C.shortcut AS Kontrahent,
                        SUM(DP.wartNetto) AS WartoscSprzedazy
                    FROM [HANDEL].[HM].[DK] DK
                    INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                    INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
                    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
                    WHERE YEAR(DK.data) = @Rok AND MONTH(DK.data) = @Miesiac";

                if (_wybranyHandlowiec != "— Wszyscy —")
                    sql += " AND WYM.CDim_Handlowiec_Val = @Handlowiec";

                sql += @"
                    GROUP BY C.shortcut
                    ORDER BY WartoscSprzedazy DESC";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Rok", rok);
                cmd.Parameters.AddWithValue("@Miesiac", miesiac);
                if (_wybranyHandlowiec != "— Wszyscy —")
                    cmd.Parameters.AddWithValue("@Handlowiec", _wybranyHandlowiec);

                var dane = new List<(string Kontrahent, decimal Wartosc)>();
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var wartosc = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
                    suma += wartosc;
                    dane.Add((reader.GetString(0), wartosc));
                }

                int idx = 0;
                foreach (var (kontrahent, wartosc) in dane)
                {
                    decimal procent = suma > 0 ? (wartosc / suma) * 100 : 0;
                    series.Add(new PieSeries
                    {
                        Title = $"{kontrahent}: {wartosc:N0} zl ({procent:F1}%)",
                        Values = new ChartValues<decimal> { wartosc },
                        DataLabels = true,
                        LabelPoint = p => $"{procent:F1}%",
                        Fill = new SolidColorBrush(_kolory[idx % _kolory.Length])
                    });
                    idx++;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Blad sprzedazy miesiecznej: {ex.Message}");
            }

            chartSprzedaz.Series = series;
            txtSumaSprzedaz.Text = $"CALKOWITA WARTOSC SPRZEDAZY: {suma:N2} zl";
        }

        #endregion

        #region Top 10 Odbiorcy

        private async System.Threading.Tasks.Task OdswiezTop10Async()
        {
            if (cmbRokTop10.SelectedItem == null || cmbMiesiacTop10.SelectedValue == null) return;

            int rok = (int)cmbRokTop10.SelectedItem;
            int miesiac = (int)cmbMiesiacTop10.SelectedValue;
            int? towarId = cmbTowarTop10.SelectedValue as int?;
            if (towarId == 0) towarId = null;

            var series = new SeriesCollection();

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var sql = @"
                    SELECT TOP 10
                        C.shortcut AS Kontrahent,
                        ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany') AS Handlowiec,
                        SUM(DP.ilosc) AS SumaIlosci
                    FROM [HANDEL].[HM].[DK] DK
                    INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                    INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
                    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
                    WHERE YEAR(DK.data) = @Rok AND MONTH(DK.data) = @Miesiac
                      AND (@TowarID IS NULL OR DP.idtw = @TowarID)
                    GROUP BY C.shortcut, ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany')
                    ORDER BY SumaIlosci DESC";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Rok", rok);
                cmd.Parameters.AddWithValue("@Miesiac", miesiac);
                cmd.Parameters.AddWithValue("@TowarID", (object)towarId ?? DBNull.Value);

                var dane = new List<(string Kontrahent, string Handlowiec, decimal Ilosc)>();
                decimal suma = 0;

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var ilosc = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2);
                    suma += ilosc;
                    dane.Add((reader.GetString(0), reader.GetString(1), ilosc));
                }

                int idx = 0;
                foreach (var (kontrahent, handlowiec, ilosc) in dane)
                {
                    decimal procent = suma > 0 ? (ilosc / suma) * 100 : 0;
                    series.Add(new PieSeries
                    {
                        Title = $"{kontrahent} ({handlowiec}): {ilosc:N0} kg ({procent:F1}%)",
                        Values = new ChartValues<decimal> { ilosc },
                        DataLabels = true,
                        LabelPoint = p => $"{procent:F1}%",
                        Fill = new SolidColorBrush(_kolory[idx % _kolory.Length])
                    });
                    idx++;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Blad Top 10: {ex.Message}");
            }

            chartTop10.Series = series;
        }

        #endregion

        #region Udzial Handlowcow

        private async System.Threading.Tasks.Task OdswiezUdzialHandlowcowAsync()
        {
            if (cmbRokUdzial.SelectedItem == null || cmbMiesiacUdzial.SelectedValue == null) return;

            int rok = (int)cmbRokUdzial.SelectedItem;
            int miesiac = (int)cmbMiesiacUdzial.SelectedValue;

            var series = new SeriesCollection();

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var sql = @"
                    SELECT
                        WYM.CDim_Handlowiec_Val AS Handlowiec,
                        SUM(DP.wartNetto) AS WartoscSprzedazy
                    FROM [HANDEL].[HM].[DK] DK
                    INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                    INNER JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
                    WHERE YEAR(DK.data) = @Rok AND MONTH(DK.data) = @Miesiac
                      AND WYM.CDim_Handlowiec_Val IS NOT NULL
                      AND WYM.CDim_Handlowiec_Val != 'Ogolne'
                    GROUP BY WYM.CDim_Handlowiec_Val
                    ORDER BY WartoscSprzedazy DESC";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Rok", rok);
                cmd.Parameters.AddWithValue("@Miesiac", miesiac);

                var dane = new List<(string Handlowiec, decimal Wartosc)>();
                decimal suma = 0;

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var wartosc = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
                    suma += wartosc;
                    dane.Add((reader.GetString(0), wartosc));
                }

                int idx = 0;
                foreach (var (handlowiec, wartosc) in dane)
                {
                    decimal procent = suma > 0 ? (wartosc / suma) * 100 : 0;
                    series.Add(new PieSeries
                    {
                        Title = $"{handlowiec}: {wartosc:N0} zl ({procent:F1}%)",
                        Values = new ChartValues<decimal> { wartosc },
                        DataLabels = true,
                        LabelPoint = p => $"{procent:F1}%",
                        Fill = new SolidColorBrush(_kolory[idx % _kolory.Length])
                    });
                    idx++;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Blad udzialu handlowcow: {ex.Message}");
            }

            chartUdzial.Series = series;
        }

        #endregion

        #region Analiza Cen

        private async System.Threading.Tasks.Task OdswiezAnalizeCenAsync()
        {
            if (cmbRokCeny.SelectedItem == null || cmbMiesiacCeny.SelectedValue == null) return;

            int rok = (int)cmbRokCeny.SelectedItem;
            int miesiac = (int)cmbMiesiacCeny.SelectedValue;

            var series = new SeriesCollection();
            var labels = new List<string>();

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var sql = @"
                    SELECT
                        WYM.CDim_Handlowiec_Val AS Handlowiec,
                        AVG(DP.cena) AS SredniaCena
                    FROM [HANDEL].[HM].[DK] DK
                    INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                    INNER JOIN [HANDEL].[HM].[TW] TW ON DP.idtw = TW.ID
                    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
                    WHERE YEAR(DK.data) = @Rok AND MONTH(DK.data) = @Miesiac
                      AND TW.katalog IN ('67095', '67153')
                      AND WYM.CDim_Handlowiec_Val IS NOT NULL
                      AND WYM.CDim_Handlowiec_Val != 'Ogolne'
                    GROUP BY WYM.CDim_Handlowiec_Val
                    ORDER BY SredniaCena DESC";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Rok", rok);
                cmd.Parameters.AddWithValue("@Miesiac", miesiac);

                var wartosci = new ChartValues<decimal>();

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    labels.Add(reader.GetString(0));
                    wartosci.Add(reader.IsDBNull(1) ? 0 : reader.GetDecimal(1));
                }

                series.Add(new ColumnSeries
                {
                    Title = "Srednia cena",
                    Values = wartosci,
                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F4A261")),
                    DataLabels = true,
                    LabelPoint = p => $"{p.Y:F2}"
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Blad analizy cen: {ex.Message}");
            }

            chartCeny.Series = series;
            axisXCeny.Labels = labels;
        }

        #endregion

        #region Swieze vs Mrozone

        private async System.Threading.Tasks.Task OdswiezSwiezeMrozoneAsync()
        {
            if (cmbRokSM.SelectedItem == null || cmbMiesiacSM.SelectedValue == null) return;

            int rok = (int)cmbRokSM.SelectedItem;
            int miesiac = (int)cmbMiesiacSM.SelectedValue;

            var series = new SeriesCollection();

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var sql = @"
                    SELECT
                        CASE WHEN TW.katalog = '67153' THEN 'Mrozone' ELSE 'Swieze' END AS Typ,
                        SUM(DP.ilosc) AS SumaKg,
                        SUM(DP.wartNetto) AS WartoscNetto
                    FROM [HANDEL].[HM].[DK] DK
                    INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                    INNER JOIN [HANDEL].[HM].[TW] TW ON DP.idtw = TW.ID
                    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
                    WHERE YEAR(DK.data) = @Rok AND MONTH(DK.data) = @Miesiac
                      AND TW.katalog IN ('67095', '67153')";

                if (_wybranyHandlowiec != "— Wszyscy —")
                    sql += " AND WYM.CDim_Handlowiec_Val = @Handlowiec";

                sql += @"
                    GROUP BY CASE WHEN TW.katalog = '67153' THEN 'Mrozone' ELSE 'Swieze' END";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Rok", rok);
                cmd.Parameters.AddWithValue("@Miesiac", miesiac);
                if (_wybranyHandlowiec != "— Wszyscy —")
                    cmd.Parameters.AddWithValue("@Handlowiec", _wybranyHandlowiec);

                var dane = new List<(string Typ, decimal Kg, decimal Wartosc)>();
                decimal sumaKg = 0;

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var kg = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
                    var wartosc = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2);
                    sumaKg += kg;
                    dane.Add((reader.GetString(0), kg, wartosc));
                }

                var koloryTypow = new Dictionary<string, Color>
                {
                    { "Swieze", (Color)ColorConverter.ConvertFromString("#2ecc71") },
                    { "Mrozone", (Color)ColorConverter.ConvertFromString("#3498db") }
                };

                foreach (var (typ, kg, wartosc) in dane)
                {
                    decimal procent = sumaKg > 0 ? (kg / sumaKg) * 100 : 0;
                    series.Add(new PieSeries
                    {
                        Title = $"{typ}: {kg:N0} kg ({procent:F1}%)",
                        Values = new ChartValues<decimal> { kg },
                        DataLabels = true,
                        LabelPoint = p => $"{procent:F1}%",
                        Fill = new SolidColorBrush(koloryTypow.ContainsKey(typ) ? koloryTypow[typ] : _kolory[0])
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Blad swieze vs mrozone: {ex.Message}");
            }

            chartSwiezeMrozone.Series = series;
        }

        #endregion
    }
}
