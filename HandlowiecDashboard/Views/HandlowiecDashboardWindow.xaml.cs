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
    // Extension methods
    public static class EnumerableExtensions
    {
        public static TResult MaxOrDefault<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> selector, TResult defaultValue = default)
        {
            if (source == null || !source.Any()) return defaultValue;
            return source.Max(selector);
        }
    }

    // Klasa pomocnicza do ComboBox - rozwiazuje problem "Value = X, Text"
    public class ComboItem
    {
        public int Value { get; set; }
        public string Text { get; set; }
        public override string ToString() => Text;
    }

    public partial class HandlowiecDashboardWindow : Window
    {
        private readonly string _connectionStringHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private readonly CultureInfo _kulturaPL = new CultureInfo("pl-PL");
        private bool _isInitialized = false;

        private static readonly string[] _nazwyMiesiecy = {
            "", "Sty", "Lut", "Mar", "Kwi", "Maj", "Cze",
            "Lip", "Sie", "Wrz", "Paz", "Lis", "Gru"
        };

        private readonly Color[] _kolory = {
            (Color)ColorConverter.ConvertFromString("#FF6B6B"),
            (Color)ColorConverter.ConvertFromString("#4ECDC4"),
            (Color)ColorConverter.ConvertFromString("#FFE66D"),
            (Color)ColorConverter.ConvertFromString("#95E1D3"),
            (Color)ColorConverter.ConvertFromString("#F38181"),
            (Color)ColorConverter.ConvertFromString("#AA96DA"),
            (Color)ColorConverter.ConvertFromString("#FCBAD3"),
            (Color)ColorConverter.ConvertFromString("#A8D8EA"),
            (Color)ColorConverter.ConvertFromString("#F4A261"),
            (Color)ColorConverter.ConvertFromString("#2EC4B6")
        };

        // Formatery dla osi
        public Func<double, string> ZlFormatter { get; set; }
        public Func<double, string> KgFormatter { get; set; }
        public Func<double, string> PercentFormatter { get; set; }

        public HandlowiecDashboardWindow()
        {
            InitializeComponent();

            // Formatery z separatorem tysiecy
            ZlFormatter = val => $"{val:N0} zl";
            KgFormatter = val => $"{val:N0} kg";
            PercentFormatter = val => $"{val:F1}%";

            DataContext = this;
            Loaded += Window_Loaded;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                loadingOverlay.Visibility = Visibility.Visible;

                // Ustaw formatery osi
                axisYSprzedaz.LabelFormatter = ZlFormatter;
                axisXTop10.LabelFormatter = ZlFormatter;
                axisYUdzial.LabelFormatter = PercentFormatter;
                axisYCenyKg.LabelFormatter = KgFormatter;
                axisYSM.LabelFormatter = KgFormatter;
                axisYPorown.LabelFormatter = ZlFormatter;
                axisYTrend.LabelFormatter = ZlFormatter;

                WypelnijLataIMiesiace();
                _isInitialized = true;
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

        private void WypelnijLataIMiesiace()
        {
            var lata = Enumerable.Range(2020, DateTime.Now.Year - 2020 + 1).Reverse().ToList();
            var miesiace = Enumerable.Range(1, 12).Select(m => new ComboItem { Value = m, Text = _nazwyMiesiecy[m] }).ToList();

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
            WypelnijTowary(cmbTowarTop10);

            // Udzial handlowcow
            cmbRokUdzialOd.ItemsSource = lata;
            cmbRokUdzialOd.SelectedItem = DateTime.Now.Year;
            cmbMiesiacUdzialOd.ItemsSource = miesiace;
            cmbMiesiacUdzialOd.DisplayMemberPath = "Text";
            cmbMiesiacUdzialOd.SelectedValuePath = "Value";
            cmbMiesiacUdzialOd.SelectedValue = 1;
            cmbRokUdzialDo.ItemsSource = lata;
            cmbRokUdzialDo.SelectedItem = DateTime.Now.Year;
            cmbMiesiacUdzialDo.ItemsSource = miesiace;
            cmbMiesiacUdzialDo.DisplayMemberPath = "Text";
            cmbMiesiacUdzialDo.SelectedValuePath = "Value";
            cmbMiesiacUdzialDo.SelectedValue = DateTime.Now.Month;

            // Analiza cen
            cmbRokCeny.ItemsSource = lata;
            cmbRokCeny.SelectedItem = DateTime.Now.Year;
            cmbMiesiacCeny.ItemsSource = miesiace;
            cmbMiesiacCeny.DisplayMemberPath = "Text";
            cmbMiesiacCeny.SelectedValuePath = "Value";
            cmbMiesiacCeny.SelectedValue = DateTime.Now.Month;
            WypelnijTowary(cmbTowarCeny);

            // Swieze vs Mrozone
            cmbRokSM.ItemsSource = lata;
            cmbRokSM.SelectedItem = DateTime.Now.Year;
            cmbMiesiacSM.ItemsSource = miesiace;
            cmbMiesiacSM.DisplayMemberPath = "Text";
            cmbMiesiacSM.SelectedValuePath = "Value";
            cmbMiesiacSM.SelectedValue = DateTime.Now.Month;

            // Porownanie okresow
            cmbRokPorown1.ItemsSource = lata;
            cmbRokPorown1.SelectedItem = DateTime.Now.Year - 1;
            cmbRokPorown2.ItemsSource = lata;
            cmbRokPorown2.SelectedItem = DateTime.Now.Year;

            // Trend sprzedazy
            cmbOkres.ItemsSource = new[] { 3, 6, 9, 12, 18, 24 };
            cmbOkres.SelectedItem = 12;

            // Opakowania i Platnosci - wczytaj liste handlowcow
            WypelnijHandlowcow();
        }

        private void WypelnijHandlowcow()
        {
            var handlowcy = new List<ComboItem> { new ComboItem { Value = 0, Text = "Wszyscy handlowcy" } };
            try
            {
                using var cn = new SqlConnection(_connectionStringHandel);
                cn.Open();
                var sql = @"SELECT DISTINCT WYM.CDim_Handlowiec_Val AS Handlowiec
                           FROM [HANDEL].[SSCommon].[ContractorClassification] WYM
                           WHERE WYM.CDim_Handlowiec_Val IS NOT NULL
                             AND WYM.CDim_Handlowiec_Val NOT IN ('Ogolne', 'Ogólne')
                           ORDER BY Handlowiec";
                using var cmd = new SqlCommand(sql, cn);
                using var reader = cmd.ExecuteReader();
                int idx = 1;
                while (reader.Read())
                {
                    handlowcy.Add(new ComboItem { Value = idx++, Text = reader.GetString(0) });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad pobierania handlowcow:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            cmbHandlowiecOpak.ItemsSource = handlowcy;
            cmbHandlowiecOpak.DisplayMemberPath = "Text";
            cmbHandlowiecOpak.SelectedValuePath = "Value";
            cmbHandlowiecOpak.SelectedIndex = 0;

            cmbHandlowiecPlat.ItemsSource = handlowcy;
            cmbHandlowiecPlat.DisplayMemberPath = "Text";
            cmbHandlowiecPlat.SelectedValuePath = "Value";
            cmbHandlowiecPlat.SelectedIndex = 0;
        }

        private void WypelnijTowary(ComboBox cmb)
        {
            var towary = new List<ComboItem> { new ComboItem { Value = 0, Text = "Wszystkie towary" } };
            try
            {
                using var cn = new SqlConnection(_connectionStringHandel);
                cn.Open();
                var sql = @"SELECT TW.ID, TW.kod, TW.kod + ' - ' + ISNULL(TW.nazwa, '') as Nazwa
                           FROM [HANDEL].[HM].[TW] TW
                           WHERE TW.katalog IN ('67095', '67153')
                           GROUP BY TW.ID, TW.kod, TW.nazwa
                           ORDER BY TW.kod";
                using var cmd = new SqlCommand(sql, cn);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    towary.Add(new ComboItem { Value = reader.GetInt32(0), Text = reader.GetString(2) });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad pobierania towarow:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            cmb.ItemsSource = towary;
            cmb.DisplayMemberPath = "Text";
            cmb.SelectedValuePath = "Value";
            cmb.SelectedIndex = 0;
        }

        #region Event Handlers

        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e) => OdswiezAktualnaZakladke();

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized || e.Source != tabControl) return;
            OdswiezAktualnaZakladke();
        }

        private void CmbRokSprzedaz_SelectionChanged(object sender, SelectionChangedEventArgs e) => OdswiezJesliGotowe();
        private void CmbMiesiacSprzedaz_SelectionChanged(object sender, SelectionChangedEventArgs e) => OdswiezJesliGotowe();
        private void CmbTop10_SelectionChanged(object sender, SelectionChangedEventArgs e) => OdswiezJesliGotowe();
        private void CmbUdzial_SelectionChanged(object sender, SelectionChangedEventArgs e) => OdswiezJesliGotowe();
        private void CmbCeny_SelectionChanged(object sender, SelectionChangedEventArgs e) => OdswiezJesliGotowe();
        private void CmbSM_SelectionChanged(object sender, SelectionChangedEventArgs e) => OdswiezJesliGotowe();
        private void CmbPorown_SelectionChanged(object sender, SelectionChangedEventArgs e) => OdswiezJesliGotowe();
        private void CmbTrend_SelectionChanged(object sender, SelectionChangedEventArgs e) => OdswiezJesliGotowe();
        private void CmbOpakowania_SelectionChanged(object sender, SelectionChangedEventArgs e) => OdswiezJesliGotowe();
        private void CmbPlatnosci_SelectionChanged(object sender, SelectionChangedEventArgs e) => OdswiezJesliGotowe();

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
                    case 5: await OdswiezPorownanieAsync(); break;
                    case 6: await OdswiezTrendAsync(); break;
                    case 7: await OdswiezOpakowaniaAsync(); break;
                    case 8: await OdswiezPlatnosciAsync(); break;
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
            var labels = new List<string>();
            decimal suma = 0;
            var daneHandlowcow = new Dictionary<string, List<(string Klient, decimal Wartosc)>>();

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var sql = @"
                    SELECT ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany') AS Handlowiec,
                           C.shortcut AS Kontrahent, SUM(DP.wartNetto) AS WartoscSprzedazy
                    FROM [HANDEL].[HM].[DK] DK
                    INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                    INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
                    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
                    WHERE YEAR(DK.data) = @Rok AND MONTH(DK.data) = @Miesiac
                      AND WYM.CDim_Handlowiec_Val IS NOT NULL
                      AND WYM.CDim_Handlowiec_Val NOT IN ('Ogolne', 'Ogólne')
                    GROUP BY ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany'), C.shortcut
                    ORDER BY Handlowiec, WartoscSprzedazy DESC";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Rok", rok);
                cmd.Parameters.AddWithValue("@Miesiac", miesiac);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var handlowiec = reader.GetString(0);
                    var klient = reader.GetString(1);
                    var wartosc = reader.IsDBNull(2) ? 0m : Convert.ToDecimal(reader.GetValue(2));

                    if (!daneHandlowcow.ContainsKey(handlowiec))
                        daneHandlowcow[handlowiec] = new List<(string, decimal)>();

                    daneHandlowcow[handlowiec].Add((klient, wartosc));
                    suma += wartosc;
                }

                var wartosciHandlowcow = new ChartValues<decimal>();
                foreach (var h in daneHandlowcow.OrderByDescending(x => x.Value.Sum(v => v.Wartosc)))
                {
                    labels.Add(h.Key);
                    wartosciHandlowcow.Add(h.Value.Sum(v => v.Wartosc));
                }

                series.Add(new ColumnSeries
                {
                    Title = "Sprzedaz",
                    Values = wartosciHandlowcow,
                    Fill = new SolidColorBrush(_kolory[0]),
                    DataLabels = true,
                    LabelPoint = p => $"{p.Y:N0} zl",
                    Foreground = Brushes.White
                });

                treeSprzedaz.Items.Clear();
                foreach (var h in daneHandlowcow.OrderByDescending(x => x.Value.Sum(v => v.Wartosc)))
                {
                    var sumaHandlowca = h.Value.Sum(v => v.Wartosc);
                    var procent = suma > 0 ? (sumaHandlowca / suma) * 100 : 0;

                    var item = new TreeViewItem
                    {
                        Header = $"{h.Key}: {sumaHandlowca:N0} zl ({procent:F1}%)",
                        Foreground = new SolidColorBrush(_kolory[labels.IndexOf(h.Key) % _kolory.Length]),
                        FontWeight = FontWeights.Bold
                    };

                    foreach (var (klient, wartosc) in h.Value.Take(10))
                    {
                        // Oblicz % klienta w stosunku do handlowca
                        var procentKlienta = sumaHandlowca > 0 ? (wartosc / sumaHandlowca) * 100 : 0;
                        item.Items.Add(new TreeViewItem
                        {
                            Header = $"  {klient}: {wartosc:N0} zl ({procentKlienta:F1}%)",
                            Foreground = Brushes.White
                        });
                    }

                    if (h.Value.Count > 10)
                    {
                        var pozostaleWartosc = h.Value.Skip(10).Sum(v => v.Wartosc);
                        var pozostaleProcent = sumaHandlowca > 0 ? (pozostaleWartosc / sumaHandlowca) * 100 : 0;
                        item.Items.Add(new TreeViewItem
                        {
                            Header = $"  ... i {h.Value.Count - 10} wiecej ({pozostaleProcent:F1}%)",
                            Foreground = Brushes.Gray,
                            FontStyle = FontStyles.Italic
                        });
                    }

                    treeSprzedaz.Items.Add(item);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad sprzedazy miesiecznej:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            chartSprzedaz.Series = series;
            axisXSprzedaz.Labels = labels;
            txtSumaSprzedaz.Text = $"CALKOWITA WARTOSC SPRZEDAZY: {suma:N0} zl";
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
            var labels = new List<string>();
            decimal sumaKg = 0;
            decimal sumaWartosc = 0;

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var sql = @"
                    SELECT TOP 10 C.shortcut AS Kontrahent,
                           ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany') AS Handlowiec,
                           SUM(DP.ilosc) AS SumaKg, SUM(DP.wartNetto) AS SumaWartosc
                    FROM [HANDEL].[HM].[DK] DK
                    INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                    INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
                    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
                    WHERE YEAR(DK.data) = @Rok AND MONTH(DK.data) = @Miesiac
                      AND (@TowarID IS NULL OR DP.idtw = @TowarID)
                    GROUP BY C.shortcut, ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany')
                    ORDER BY SumaWartosc DESC";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Rok", rok);
                cmd.Parameters.AddWithValue("@Miesiac", miesiac);
                cmd.Parameters.AddWithValue("@TowarID", (object)towarId ?? DBNull.Value);

                var wartosci = new ChartValues<decimal>();
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var kontrahent = reader.GetString(0);
                    var handlowiec = reader.GetString(1);
                    var kg = reader.IsDBNull(2) ? 0m : Convert.ToDecimal(reader.GetValue(2));
                    var wartosc = reader.IsDBNull(3) ? 0m : Convert.ToDecimal(reader.GetValue(3));

                    labels.Add($"{kontrahent} ({handlowiec})");
                    wartosci.Add(wartosc);
                    sumaKg += kg;
                    sumaWartosc += wartosc;
                }

                series.Add(new RowSeries
                {
                    Title = "Wartosc",
                    Values = wartosci,
                    Fill = new SolidColorBrush(_kolory[1]),
                    DataLabels = true,
                    LabelPoint = p => $"{p.X:N0} zl",
                    Foreground = Brushes.White
                });

                // Oblicz srednia cene
                decimal sredniaCena = sumaKg > 0 ? sumaWartosc / sumaKg : 0;
                txtTop10Info.Text = $"Suma: {sumaWartosc:N0} zl  |  Suma kg: {sumaKg:N0}  |  Srednia cena: {sredniaCena:F2} zl/kg";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad Top 10:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            chartTop10.Series = series;
            axisYTop10.Labels = labels;
        }

        #endregion

        #region Udzial Handlowcow

        private async System.Threading.Tasks.Task OdswiezUdzialHandlowcowAsync()
        {
            if (cmbRokUdzialOd.SelectedItem == null || cmbMiesiacUdzialOd.SelectedValue == null ||
                cmbRokUdzialDo.SelectedItem == null || cmbMiesiacUdzialDo.SelectedValue == null) return;

            int rokOd = (int)cmbRokUdzialOd.SelectedItem;
            int miesiacOd = (int)cmbMiesiacUdzialOd.SelectedValue;
            int rokDo = (int)cmbRokUdzialDo.SelectedItem;
            int miesiacDo = (int)cmbMiesiacUdzialDo.SelectedValue;

            var series = new SeriesCollection();
            var labels = new List<string>();
            var daneHandlowcow = new Dictionary<string, Dictionary<string, decimal>>();

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var sql = @"
                    SELECT WYM.CDim_Handlowiec_Val AS Handlowiec, YEAR(DK.data) AS Rok, MONTH(DK.data) AS Miesiac,
                           SUM(DP.wartNetto) AS WartoscSprzedazy
                    FROM [HANDEL].[HM].[DK] DK
                    INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                    INNER JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
                    WHERE (YEAR(DK.data) * 100 + MONTH(DK.data)) >= @OdData
                      AND (YEAR(DK.data) * 100 + MONTH(DK.data)) <= @DoData
                      AND WYM.CDim_Handlowiec_Val IS NOT NULL
                      AND WYM.CDim_Handlowiec_Val NOT IN ('Ogolne', 'Ogólne')
                    GROUP BY WYM.CDim_Handlowiec_Val, YEAR(DK.data), MONTH(DK.data)
                    ORDER BY Rok, Miesiac, Handlowiec";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@OdData", rokOd * 100 + miesiacOd);
                cmd.Parameters.AddWithValue("@DoData", rokDo * 100 + miesiacDo);

                var sumyMiesieczne = new Dictionary<string, decimal>();

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var handlowiec = reader.GetString(0);
                    var rok = reader.GetInt32(1);
                    var miesiac = reader.GetInt32(2);
                    var wartosc = reader.IsDBNull(3) ? 0m : Convert.ToDecimal(reader.GetValue(3));

                    var klucz = $"{_nazwyMiesiecy[miesiac]} {rok}";

                    if (!labels.Contains(klucz))
                        labels.Add(klucz);

                    if (!daneHandlowcow.ContainsKey(handlowiec))
                        daneHandlowcow[handlowiec] = new Dictionary<string, decimal>();

                    daneHandlowcow[handlowiec][klucz] = wartosc;

                    if (!sumyMiesieczne.ContainsKey(klucz))
                        sumyMiesieczne[klucz] = 0;
                    sumyMiesieczne[klucz] += wartosc;
                }

                // Znajdz maksymalny procent dla osi Y
                double maxProcent = 0;

                int idx = 0;
                foreach (var h in daneHandlowcow.OrderByDescending(x => x.Value.Values.Sum()))
                {
                    var wartosci = new ChartValues<double>();
                    foreach (var klucz in labels)
                    {
                        var wartosc = h.Value.ContainsKey(klucz) ? h.Value[klucz] : 0m;
                        var suma = sumyMiesieczne.ContainsKey(klucz) ? sumyMiesieczne[klucz] : 1m;
                        var procent = suma > 0 ? (double)(wartosc / suma * 100) : 0;
                        wartosci.Add(procent);
                        if (procent > maxProcent) maxProcent = procent;
                    }

                    // Dodaj nazwe handlowca na koncu ostatniego punktu
                    var lastValue = wartosci.LastOrDefault();

                    series.Add(new LineSeries
                    {
                        Title = h.Key,
                        Values = wartosci,
                        Stroke = new SolidColorBrush(_kolory[idx % _kolory.Length]),
                        Fill = Brushes.Transparent,
                        PointGeometry = DefaultGeometries.Circle,
                        PointGeometrySize = 8,
                        LineSmoothness = 0.3,
                        DataLabels = false,
                        Foreground = Brushes.White
                    });
                    idx++;
                }

                // Dodaj etykiety na koncu linii - dodajemy dodatkowa kolumne z nazwami
                if (labels.Count > 0)
                {
                    // Dodajemy pusta etykiete na koncu dla miejsca na nazwy
                    labels.Add("");

                    // Rozszerz wartosci o ostatni punkt (taki sam jak poprzedni) + tekst
                    foreach (LineSeries ls in series)
                    {
                        if (ls.Values.Count > 0)
                        {
                            var lastVal = (double)ls.Values[ls.Values.Count - 1];
                            ls.Values.Add(lastVal);
                        }
                    }
                }

                // Ustaw os Y do maksymalnego punktu + 10%
                axisYUdzial.MaxValue = maxProcent * 1.1;

                // Dodaj etykiety procentowe na wszystkich punktach + nazwe handlowca na koncu
                idx = 0;
                foreach (LineSeries ls in series)
                {
                    var handlowiecNazwa = ls.Title;
                    var color = _kolory[idx % _kolory.Length];
                    var valuesCount = ls.Values.Count;

                    // Ustaw etykiete na kazdym punkcie: % na wszystkich, + nazwe na ostatnim
                    ls.LabelPoint = p =>
                    {
                        if (p.Key == valuesCount - 1)
                            return $"{p.Y:F1}% {handlowiecNazwa}";
                        return $"{p.Y:F1}%";
                    };
                    ls.DataLabels = true;
                    idx++;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad udzialu handlowcow:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            chartUdzial.Series = series;
            axisXUdzial.Labels = labels;
        }

        #endregion

        #region Analiza Cen

        private List<string> _analizaCenyLabels = new List<string>();

        private async System.Threading.Tasks.Task OdswiezAnalizeCenAsync()
        {
            if (cmbRokCeny.SelectedItem == null || cmbMiesiacCeny.SelectedValue == null) return;

            int rok = (int)cmbRokCeny.SelectedItem;
            int miesiac = (int)cmbMiesiacCeny.SelectedValue;
            int? towarId = cmbTowarCeny.SelectedValue as int?;
            if (towarId == 0) towarId = null;

            var seriesCeny = new SeriesCollection();
            var seriesKg = new SeriesCollection();
            var labels = new List<string>();
            var daneTabeli = new List<HandlowiecCenyRow>();

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var sql = @"
                    SELECT WYM.CDim_Handlowiec_Val AS Handlowiec,
                           AVG(DP.cena) AS SredniaCena,
                           SUM(DP.ilosc) AS SumaKg,
                           MIN(DP.cena) AS MinCena,
                           MAX(DP.cena) AS MaxCena,
                           SUM(DP.wartNetto) AS SumaWartosc,
                           COUNT(*) AS LiczbaTransakcji,
                           COUNT(DISTINCT DK.khid) AS LiczbaKontrahentow
                    FROM [HANDEL].[HM].[DK] DK
                    INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                    INNER JOIN [HANDEL].[HM].[TW] TW ON DP.idtw = TW.ID
                    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
                    WHERE YEAR(DK.data) = @Rok AND MONTH(DK.data) = @Miesiac
                      AND TW.katalog IN ('67095', '67153')
                      AND (@TowarID IS NULL OR DP.idtw = @TowarID)
                      AND WYM.CDim_Handlowiec_Val IS NOT NULL
                      AND WYM.CDim_Handlowiec_Val NOT IN ('Ogolne', 'Ogólne')
                    GROUP BY WYM.CDim_Handlowiec_Val
                    ORDER BY SredniaCena DESC";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Rok", rok);
                cmd.Parameters.AddWithValue("@Miesiac", miesiac);
                cmd.Parameters.AddWithValue("@TowarID", (object)towarId ?? DBNull.Value);

                var wartosciCeny = new ChartValues<decimal>();
                var wartosciKg = new ChartValues<decimal>();

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var handlowiec = reader.GetString(0);
                    var sredniaCena = reader.IsDBNull(1) ? 0m : Convert.ToDecimal(reader.GetValue(1));
                    var sumaKg = reader.IsDBNull(2) ? 0m : Convert.ToDecimal(reader.GetValue(2));
                    var minCena = reader.IsDBNull(3) ? 0m : Convert.ToDecimal(reader.GetValue(3));
                    var maxCena = reader.IsDBNull(4) ? 0m : Convert.ToDecimal(reader.GetValue(4));
                    var sumaWartosc = reader.IsDBNull(5) ? 0m : Convert.ToDecimal(reader.GetValue(5));
                    var liczbaTransakcji = reader.GetInt32(6);
                    var liczbaKontrahentow = reader.GetInt32(7);

                    labels.Add(handlowiec);
                    wartosciCeny.Add(sredniaCena);
                    wartosciKg.Add(sumaKg);

                    daneTabeli.Add(new HandlowiecCenyRow
                    {
                        Handlowiec = handlowiec,
                        SumaKg = sumaKg,
                        SredniaCena = sredniaCena,
                        MinCena = minCena,
                        MaxCena = maxCena,
                        SumaWartosc = sumaWartosc,
                        LiczbaTransakcji = liczbaTransakcji,
                        LiczbaKontrahentow = liczbaKontrahentow
                    });
                }

                seriesCeny.Add(new ColumnSeries
                {
                    Title = "Srednia cena",
                    Values = wartosciCeny,
                    Fill = new SolidColorBrush(_kolory[3]),
                    DataLabels = true,
                    LabelPoint = p => $"{p.Y:F2} zl",
                    Foreground = Brushes.White
                });

                seriesKg.Add(new ColumnSeries
                {
                    Title = "Ilosc kg",
                    Values = wartosciKg,
                    Fill = new SolidColorBrush(_kolory[1]),
                    DataLabels = true,
                    LabelPoint = p => $"{p.Y:N0}",
                    Foreground = Brushes.White
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad analizy cen:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            _analizaCenyLabels = labels;
            chartCeny.Series = seriesCeny;
            axisXCeny.Labels = labels;
            chartCenyKg.Series = seriesKg;
            axisXCenyKg.Labels = labels;
            gridAnalizaCeny.ItemsSource = daneTabeli;
        }

        private void GridAnalizaCeny_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (gridAnalizaCeny.SelectedItem is HandlowiecCenyRow row)
            {
                int rok = (int)cmbRokCeny.SelectedItem;
                int miesiac = (int)cmbMiesiacCeny.SelectedValue;
                var okno = new AnalizaCenHandlowcaWindow(row.Handlowiec, rok, miesiac);
                okno.Show();
            }
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
                    SELECT CASE WHEN TW.katalog = '67153' THEN 'Mrozone' ELSE 'Swieze' END AS Typ,
                           SUM(DP.ilosc) AS SumaKg, SUM(DP.wartNetto) AS WartoscNetto
                    FROM [HANDEL].[HM].[DK] DK
                    INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                    INNER JOIN [HANDEL].[HM].[TW] TW ON DP.idtw = TW.ID
                    WHERE YEAR(DK.data) = @Rok AND MONTH(DK.data) = @Miesiac
                      AND TW.katalog IN ('67095', '67153')
                    GROUP BY CASE WHEN TW.katalog = '67153' THEN 'Mrozone' ELSE 'Swieze' END";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Rok", rok);
                cmd.Parameters.AddWithValue("@Miesiac", miesiac);

                var dane = new Dictionary<string, (decimal Kg, decimal Wartosc)>();
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var typ = reader.GetString(0);
                    var kg = reader.IsDBNull(1) ? 0m : Convert.ToDecimal(reader.GetValue(1));
                    var wartosc = reader.IsDBNull(2) ? 0m : Convert.ToDecimal(reader.GetValue(2));
                    dane[typ] = (kg, wartosc);
                }

                var swiezeKg = dane.ContainsKey("Swieze") ? dane["Swieze"].Kg : 0m;
                var mrozoneKg = dane.ContainsKey("Mrozone") ? dane["Mrozone"].Kg : 0m;

                series.Add(new ColumnSeries
                {
                    Title = "Swieze",
                    Values = new ChartValues<decimal> { swiezeKg },
                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4ECDC4")),
                    DataLabels = true,
                    LabelPoint = p => $"{p.Y:N0} kg",
                    Foreground = Brushes.White
                });

                series.Add(new ColumnSeries
                {
                    Title = "Mrozone",
                    Values = new ChartValues<decimal> { mrozoneKg },
                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#45B7D1")),
                    DataLabels = true,
                    LabelPoint = p => $"{p.Y:N0} kg",
                    Foreground = Brushes.White
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad swieze vs mrozone:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            chartSwiezeMrozone.Series = series;
            axisXSM.Labels = new[] { "" };
        }

        #endregion

        #region Porownanie okresow

        private async System.Threading.Tasks.Task OdswiezPorownanieAsync()
        {
            if (cmbRokPorown1.SelectedItem == null || cmbRokPorown2.SelectedItem == null) return;

            int rok1 = (int)cmbRokPorown1.SelectedItem;
            int rok2 = (int)cmbRokPorown2.SelectedItem;

            var series = new SeriesCollection();
            var labels = _nazwyMiesiecy.Skip(1).ToList();

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var sql = @"
                    SELECT YEAR(DK.data) AS Rok, MONTH(DK.data) AS Miesiac, SUM(DP.wartNetto) AS Wartosc
                    FROM [HANDEL].[HM].[DK] DK
                    INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                    WHERE YEAR(DK.data) IN (@Rok1, @Rok2)
                    GROUP BY YEAR(DK.data), MONTH(DK.data)
                    ORDER BY Rok, Miesiac";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Rok1", rok1);
                cmd.Parameters.AddWithValue("@Rok2", rok2);

                var daneRok1 = new decimal[12];
                var daneRok2 = new decimal[12];

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var rok = reader.GetInt32(0);
                    var miesiac = reader.GetInt32(1);
                    var wartosc = reader.IsDBNull(2) ? 0m : Convert.ToDecimal(reader.GetValue(2));

                    if (rok == rok1 && miesiac >= 1 && miesiac <= 12)
                        daneRok1[miesiac - 1] = wartosc;
                    else if (rok == rok2 && miesiac >= 1 && miesiac <= 12)
                        daneRok2[miesiac - 1] = wartosc;
                }

                series.Add(new LineSeries
                {
                    Title = rok1.ToString(),
                    Values = new ChartValues<decimal>(daneRok1),
                    Stroke = new SolidColorBrush(_kolory[0]),
                    Fill = Brushes.Transparent,
                    PointGeometry = DefaultGeometries.Circle,
                    PointGeometrySize = 8,
                    DataLabels = true,
                    LabelPoint = p => $"{p.Y:N0}",
                    Foreground = Brushes.White
                });

                series.Add(new LineSeries
                {
                    Title = rok2.ToString(),
                    Values = new ChartValues<decimal>(daneRok2),
                    Stroke = new SolidColorBrush(_kolory[1]),
                    Fill = Brushes.Transparent,
                    PointGeometry = DefaultGeometries.Square,
                    PointGeometrySize = 8,
                    DataLabels = true,
                    LabelPoint = p => $"{p.Y:N0}",
                    Foreground = Brushes.White
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad porownania:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            chartPorownanie.Series = series;
            axisXPorown.Labels = labels;
        }

        #endregion

        #region Trend sprzedazy

        private async System.Threading.Tasks.Task OdswiezTrendAsync()
        {
            if (cmbOkres.SelectedItem == null) return;

            int okres = (int)cmbOkres.SelectedItem;

            var series = new SeriesCollection();
            var labels = new List<string>();

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var dataOd = DateTime.Now.AddMonths(-okres);

                var sql = @"
                    SELECT YEAR(DK.data) AS Rok, MONTH(DK.data) AS Miesiac, SUM(DP.wartNetto) AS Wartosc
                    FROM [HANDEL].[HM].[DK] DK
                    INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                    WHERE DK.data >= @DataOd
                    GROUP BY YEAR(DK.data), MONTH(DK.data)
                    ORDER BY Rok, Miesiac";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@DataOd", new DateTime(dataOd.Year, dataOd.Month, 1));

                var wartosci = new ChartValues<decimal>();
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var rok = reader.GetInt32(0);
                    var miesiac = reader.GetInt32(1);
                    var wartosc = reader.IsDBNull(2) ? 0m : Convert.ToDecimal(reader.GetValue(2));

                    labels.Add($"{_nazwyMiesiecy[miesiac]} {rok}");
                    wartosci.Add(wartosc);
                }

                series.Add(new LineSeries
                {
                    Title = "Trend sprzedazy",
                    Values = wartosci,
                    Stroke = new SolidColorBrush(_kolory[4]),
                    Fill = new SolidColorBrush(Color.FromArgb(50, _kolory[4].R, _kolory[4].G, _kolory[4].B)),
                    PointGeometry = DefaultGeometries.Circle,
                    PointGeometrySize = 10,
                    LineSmoothness = 0.5,
                    DataLabels = true,
                    LabelPoint = p => $"{p.Y:N0}",
                    Foreground = Brushes.White
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad trendu:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            chartTrend.Series = series;
            axisXTrend.Labels = labels;
        }

        #endregion

        #region Saldo Opakowan

        private DateTime GetLastSunday(DateTime date)
        {
            int diff = (7 + ((int)date.DayOfWeek - (int)DayOfWeek.Sunday)) % 7;
            return date.AddDays(-diff).Date;
        }

        private async Task<(decimal E2, decimal H1)> PobierzSaldoNaDzien(SqlConnection cn, DateTime data, string handlowiec)
        {
            var sql = @"
SELECT
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Pojemnik Drobiowy E2' THEN MZ.Ilosc ELSE 0 END), 0) AS DECIMAL(18,0)) AS E2,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta H1' THEN MZ.Ilosc ELSE 0 END), 0) AS DECIMAL(18,0)) AS H1
FROM [HANDEL].[HM].[MZ] MZ
INNER JOIN [HANDEL].[HM].[TW] TW ON MZ.idtw = TW.id
INNER JOIN [HANDEL].[HM].[MG] MG ON MZ.super = MG.id
INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON MG.khid = C.id
LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON C.Id = WYM.ElementId
WHERE MZ.data >= '2020-01-01' AND MZ.data <= @DataDo AND MG.anulowany = 0
  AND TW.nazwa IN ('Pojemnik Drobiowy E2', 'Paleta H1')
  AND (@Handlowiec IS NULL OR WYM.CDim_Handlowiec_Val = @Handlowiec)";

            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@DataDo", data);
            cmd.Parameters.AddWithValue("@Handlowiec", (object)handlowiec ?? DBNull.Value);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
                return (reader.IsDBNull(0) ? 0m : Convert.ToDecimal(reader.GetValue(0)),
                        reader.IsDBNull(1) ? 0m : Convert.ToDecimal(reader.GetValue(1)));
            return (0, 0);
        }

        private async System.Threading.Tasks.Task OdswiezOpakowaniaAsync()
        {
            string wybranyHandlowiec = null;
            if (cmbHandlowiecOpak.SelectedItem is ComboItem item && item.Value > 0)
                wybranyHandlowiec = item.Text;

            var dane = new List<OpakowanieRow>();
            DateTime dzisiaj = DateTime.Today;
            DateTime ostatniaNiedziela = GetLastSunday(dzisiaj);
            DateTime poprzedniaNiedziela = ostatniaNiedziela.AddDays(-7);

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                // Pobierz salda na rozne daty
                var (e2Dzisiaj, h1Dzisiaj) = await PobierzSaldoNaDzien(cn, dzisiaj, wybranyHandlowiec);
                var (e2Niedziela1, h1Niedziela1) = await PobierzSaldoNaDzien(cn, ostatniaNiedziela, wybranyHandlowiec);
                var (e2Niedziela2, h1Niedziela2) = await PobierzSaldoNaDzien(cn, poprzedniaNiedziela, wybranyHandlowiec);

                // Aktualizuj karty statystyk
                txtOpakE2Aktualne.Text = $"{e2Dzisiaj:N0}";
                txtOpakH1Aktualne.Text = $"{h1Dzisiaj:N0}";

                var zmianaE2 = e2Dzisiaj - e2Niedziela1;
                var zmianaH1 = h1Dzisiaj - h1Niedziela1;
                txtOpakE2Zmiana.Text = $"vs niedz: {(zmianaE2 >= 0 ? "+" : "")}{zmianaE2:N0}";
                txtOpakE2Zmiana.Foreground = zmianaE2 > 0 ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 107, 107)) :
                                              zmianaE2 < 0 ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(78, 205, 196)) :
                                              new SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 148, 158));
                txtOpakH1Zmiana.Text = $"vs niedz: {(zmianaH1 >= 0 ? "+" : "")}{zmianaH1:N0}";
                txtOpakH1Zmiana.Foreground = zmianaH1 > 0 ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 107, 107)) :
                                              zmianaH1 < 0 ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(78, 205, 196)) :
                                              new SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 148, 158));

                txtOpakOstatniaNiedzielaData.Text = $"NIEDZIELA {ostatniaNiedziela:dd.MM}";
                txtOpakE2Niedziela1.Text = $"{e2Niedziela1:N0}";
                txtOpakH1Niedziela1.Text = $"{h1Niedziela1:N0}";
                txtOpakRazemNiedziela1.Text = $"Razem: {e2Niedziela1 + h1Niedziela1:N0}";

                txtOpakPoprzedniaNiedzielaData.Text = $"NIEDZIELA {poprzedniaNiedziela:dd.MM}";
                txtOpakE2Niedziela2.Text = $"{e2Niedziela2:N0}";
                txtOpakH1Niedziela2.Text = $"{h1Niedziela2:N0}";
                txtOpakRazemNiedziela2.Text = $"Razem: {e2Niedziela2 + h1Niedziela2:N0}";

                // Wykres historyczny (ostatnie 4 niedziele)
                var labelsE2 = new List<string>();
                var valuesE2 = new ChartValues<double>();
                var valuesH1 = new ChartValues<double>();
                for (int i = 3; i >= 0; i--)
                {
                    var data = ostatniaNiedziela.AddDays(-7 * i);
                    var (e2, h1) = await PobierzSaldoNaDzien(cn, data, wybranyHandlowiec);
                    labelsE2.Add(data.ToString("dd.MM"));
                    valuesE2.Add((double)e2);
                    valuesH1.Add((double)h1);
                }

                chartOpakowaniaE2.Series = new SeriesCollection
                {
                    new ColumnSeries { Title = "E2", Values = valuesE2, Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(78, 205, 196)),
                        DataLabels = true, LabelPoint = p => $"{p.Y:N0}", Foreground = Brushes.White }
                };
                axisXOpakE2.Labels = labelsE2;

                chartOpakowaniaH1.Series = new SeriesCollection
                {
                    new ColumnSeries { Title = "H1", Values = valuesH1, Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 162, 97)),
                        DataLabels = true, LabelPoint = p => $"{p.Y:N0}", Foreground = Brushes.White }
                };
                axisXOpakH1.Labels = labelsE2;

                // Dane per kontrahent z porownaniem tygodniowym
                var sqlKontrahenci = @"
SELECT
    C.shortcut AS Kontrahent,
    ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany') AS Handlowiec,
    CAST(ISNULL(SUM(CASE WHEN MZ.data <= @Dzisiaj AND TW.nazwa = 'Pojemnik Drobiowy E2' THEN MZ.Ilosc ELSE 0 END), 0) AS DECIMAL(18,0)) AS E2Dzisiaj,
    CAST(ISNULL(SUM(CASE WHEN MZ.data <= @Niedziela1 AND TW.nazwa = 'Pojemnik Drobiowy E2' THEN MZ.Ilosc ELSE 0 END), 0) AS DECIMAL(18,0)) AS E2Niedziela,
    CAST(ISNULL(SUM(CASE WHEN MZ.data <= @Dzisiaj AND TW.nazwa = 'Paleta H1' THEN MZ.Ilosc ELSE 0 END), 0) AS DECIMAL(18,0)) AS H1Dzisiaj,
    CAST(ISNULL(SUM(CASE WHEN MZ.data <= @Niedziela1 AND TW.nazwa = 'Paleta H1' THEN MZ.Ilosc ELSE 0 END), 0) AS DECIMAL(18,0)) AS H1Niedziela
FROM [HANDEL].[HM].[MZ] MZ
INNER JOIN [HANDEL].[HM].[TW] TW ON MZ.idtw = TW.id
INNER JOIN [HANDEL].[HM].[MG] MG ON MZ.super = MG.id
INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON MG.khid = C.id
LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON C.Id = WYM.ElementId
WHERE MZ.data >= '2020-01-01' AND MG.anulowany = 0
  AND TW.nazwa IN ('Pojemnik Drobiowy E2', 'Paleta H1')
  AND (@Handlowiec IS NULL OR WYM.CDim_Handlowiec_Val = @Handlowiec)
GROUP BY C.shortcut, ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany')
HAVING ISNULL(SUM(CASE WHEN MZ.data <= @Dzisiaj THEN MZ.Ilosc ELSE 0 END), 0) <> 0
ORDER BY ISNULL(SUM(CASE WHEN MZ.data <= @Dzisiaj THEN MZ.Ilosc ELSE 0 END), 0) DESC";

                await using var cmdK = new SqlCommand(sqlKontrahenci, cn);
                cmdK.Parameters.AddWithValue("@Handlowiec", (object)wybranyHandlowiec ?? DBNull.Value);
                cmdK.Parameters.AddWithValue("@Dzisiaj", dzisiaj);
                cmdK.Parameters.AddWithValue("@Niedziela1", ostatniaNiedziela);

                await using var readerK = await cmdK.ExecuteReaderAsync();
                while (await readerK.ReadAsync())
                {
                    var e2Now = readerK.IsDBNull(2) ? 0m : Convert.ToDecimal(readerK.GetValue(2));
                    var e2Week = readerK.IsDBNull(3) ? 0m : Convert.ToDecimal(readerK.GetValue(3));
                    var h1Now = readerK.IsDBNull(4) ? 0m : Convert.ToDecimal(readerK.GetValue(4));
                    var h1Week = readerK.IsDBNull(5) ? 0m : Convert.ToDecimal(readerK.GetValue(5));
                    var zE2 = e2Now - e2Week;
                    var zH1 = h1Now - h1Week;

                    dane.Add(new OpakowanieRow
                    {
                        Kontrahent = readerK.GetString(0),
                        Handlowiec = readerK.GetString(1),
                        PojemnikiE2 = e2Now,
                        PaletaH1 = h1Now,
                        Razem = e2Now + h1Now,
                        ZmianaE2Tydzien = zE2,
                        ZmianaH1Tydzien = zH1,
                        ZmianaE2TydzienAlert = zE2 > 0,
                        ZmianaE2TydzienGood = zE2 < 0,
                        ZmianaH1TydzienAlert = zH1 > 0,
                        ZmianaH1TydzienGood = zH1 < 0
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad wczytywania opakowan:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            gridOpakowania.ItemsSource = dane;
        }

        #endregion

        #region Platnosci

        private string GetKategoriaWiekowa(int? dni)
        {
            if (dni == null || dni <= 0) return "OK";
            if (dni <= 30) return "0-30";
            if (dni <= 60) return "31-60";
            if (dni <= 90) return "61-90";
            return "90+";
        }

        private async System.Threading.Tasks.Task OdswiezPlatnosciAsync()
        {
            string wybranyHandlowiec = null;
            if (cmbHandlowiecPlat.SelectedItem is ComboItem item && item.Value > 0)
                wybranyHandlowiec = item.Text;

            var dane = new List<PlatnoscRow>();

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var sql = @"
WITH PNAgg AS (
    SELECT PN.dkid, SUM(ISNULL(PN.kwotarozl,0)) AS KwotaRozliczona, MAX(PN.Termin) AS TerminPrawdziwy
    FROM [HANDEL].[HM].[PN] PN GROUP BY PN.dkid
),
Dokumenty AS (
    SELECT DISTINCT DK.id, DK.khid, DK.walbrutto, DK.plattermin
    FROM [HANDEL].[HM].[DK] DK
    INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
    WHERE DK.anulowany = 0 AND (@Handlowiec IS NULL OR WYM.CDim_Handlowiec_Val = @Handlowiec)
),
Saldo AS (
    SELECT D.khid, (D.walbrutto - ISNULL(PA.KwotaRozliczona,0)) AS DoZaplacenia,
           ISNULL(PA.TerminPrawdziwy, D.plattermin) AS TerminPlatnosci,
           CASE WHEN (D.walbrutto - ISNULL(PA.KwotaRozliczona,0)) > 0.01 AND GETDATE() > ISNULL(PA.TerminPrawdziwy, D.plattermin)
                THEN DATEDIFF(day, ISNULL(PA.TerminPrawdziwy, D.plattermin), GETDATE()) ELSE 0 END AS DniPrzeterminowania
    FROM Dokumenty D LEFT JOIN PNAgg PA ON PA.dkid = D.id
),
MaxPrzeterminowania AS (
    SELECT khid, MAX(CASE WHEN DniPrzeterminowania > 0 THEN DniPrzeterminowania ELSE NULL END) AS MaxDniPrzeterminowania
    FROM Saldo GROUP BY khid
)
SELECT C.Shortcut AS Kontrahent, ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany') AS Handlowiec,
       ISNULL(C.LimitAmount, 0) AS LimitKredytu,
       CAST(SUM(CASE WHEN S.DoZaplacenia > 0 THEN S.DoZaplacenia ELSE 0 END) AS DECIMAL(18,2)) AS DoZaplaty,
       CAST(SUM(CASE WHEN S.DoZaplacenia > 0 AND GETDATE() <= S.TerminPlatnosci THEN S.DoZaplacenia ELSE 0 END) AS DECIMAL(18,2)) AS Terminowe,
       CAST(SUM(CASE WHEN S.DoZaplacenia > 0 AND GETDATE() > S.TerminPlatnosci THEN S.DoZaplacenia ELSE 0 END) AS DECIMAL(18,2)) AS Przeterminowane,
       CAST(CASE WHEN ISNULL(C.LimitAmount, 0) > 0 THEN ISNULL(C.LimitAmount, 0) - SUM(CASE WHEN S.DoZaplacenia > 0 THEN S.DoZaplacenia ELSE 0 END) ELSE 0 END AS DECIMAL(18,2)) AS PrzekroczonyLimit,
       MP.MaxDniPrzeterminowania AS DniPrzeterminowania
FROM Saldo S
JOIN [HANDEL].[SSCommon].[STContractors] C ON C.id = S.khid
LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON C.id = WYM.ElementId
LEFT JOIN MaxPrzeterminowania MP ON MP.khid = S.khid
WHERE (@Handlowiec IS NULL OR WYM.CDim_Handlowiec_Val = @Handlowiec)
GROUP BY C.Shortcut, WYM.CDim_Handlowiec_Val, C.LimitAmount, MP.MaxDniPrzeterminowania
HAVING SUM(CASE WHEN S.DoZaplacenia > 0 THEN S.DoZaplacenia ELSE 0 END) > 0.01
ORDER BY Przeterminowane DESC, DoZaplaty DESC";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Handlowiec", (object)wybranyHandlowiec ?? DBNull.Value);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var kontrahent = reader.GetString(0);
                    var handlowiec = reader.GetString(1);
                    var limitKredytu = reader.IsDBNull(2) ? 0m : Convert.ToDecimal(reader.GetValue(2));
                    var doZaplaty = reader.IsDBNull(3) ? 0m : Convert.ToDecimal(reader.GetValue(3));
                    var terminowe = reader.IsDBNull(4) ? 0m : Convert.ToDecimal(reader.GetValue(4));
                    var przeterminowane = reader.IsDBNull(5) ? 0m : Convert.ToDecimal(reader.GetValue(5));
                    var przekroczonyLimitVal = reader.IsDBNull(6) ? 0m : Convert.ToDecimal(reader.GetValue(6));
                    var dniPrzeterminowania = reader.IsDBNull(7) ? (int?)null : Convert.ToInt32(reader.GetValue(7));
                    var przekroczonyLimit = przekroczonyLimitVal < 0 ? Math.Abs(przekroczonyLimitVal) : 0;

                    dane.Add(new PlatnoscRow
                    {
                        Kontrahent = kontrahent, Handlowiec = handlowiec, LimitKredytu = limitKredytu,
                        DoZaplaty = doZaplaty, Terminowe = terminowe, Przeterminowane = przeterminowane,
                        PrzekroczonyLimit = przekroczonyLimit, DniPrzeterminowania = dniPrzeterminowania,
                        PrzeterminowaneAlert = przeterminowane > 0, PrzekroczonyLimitAlert = przekroczonyLimit > 0,
                        KategoriaWiekowa = GetKategoriaWiekowa(dniPrzeterminowania)
                    });
                }

                // Statystyki glowne
                var sumaDoZaplaty = dane.Sum(d => d.DoZaplaty);
                var sumaTerminowe = dane.Sum(d => d.Terminowe);
                var sumaPrzeterminowane = dane.Sum(d => d.Przeterminowane);
                var sumaPrzekroczony = dane.Sum(d => d.PrzekroczonyLimit);
                var iloscKlientow = dane.Count;
                var iloscZPrzeterminowanymi = dane.Count(d => d.Przeterminowane > 0);
                var iloscZPrzekroczonym = dane.Count(d => d.PrzekroczonyLimit > 0);
                var maxDni = dane.Where(d => d.DniPrzeterminowania.HasValue).MaxOrDefault(d => d.DniPrzeterminowania.Value);
                var maxDniKlient = dane.FirstOrDefault(d => d.DniPrzeterminowania == maxDni)?.Kontrahent ?? "";

                // Aktualizuj karty
                txtPlatSumaDoZaplaty.Text = $"{sumaDoZaplaty:N0} zl";
                txtPlatIloscKlientow.Text = $"{iloscKlientow} klientow";
                txtPlatTerminowe.Text = $"{sumaTerminowe:N0} zl";
                txtPlatTerminoweProcent.Text = sumaDoZaplaty > 0 ? $"{sumaTerminowe / sumaDoZaplaty * 100:F1}%" : "0%";
                txtPlatPrzeterminowane.Text = $"{sumaPrzeterminowane:N0} zl";
                txtPlatPrzeterminowaneProcent.Text = $"{(sumaDoZaplaty > 0 ? sumaPrzeterminowane / sumaDoZaplaty * 100 : 0):F1}% ({iloscZPrzeterminowanymi} kl.)";
                txtPlatPrzekroczony.Text = $"{sumaPrzekroczony:N0} zl";
                txtPlatPrzekroczonyIlosc.Text = $"{iloscZPrzekroczonym} klientow";
                txtPlatMaxDni.Text = $"{maxDni} dni";
                txtPlatMaxDniKlient.Text = maxDniKlient;

                // Aging analysis
                var aging030 = dane.Where(d => d.DniPrzeterminowania > 0 && d.DniPrzeterminowania <= 30).Sum(d => d.Przeterminowane);
                var aging3160 = dane.Where(d => d.DniPrzeterminowania > 30 && d.DniPrzeterminowania <= 60).Sum(d => d.Przeterminowane);
                var aging6190 = dane.Where(d => d.DniPrzeterminowania > 60 && d.DniPrzeterminowania <= 90).Sum(d => d.Przeterminowane);
                var aging90Plus = dane.Where(d => d.DniPrzeterminowania > 90).Sum(d => d.Przeterminowane);
                var agingTotal = aging030 + aging3160 + aging6190 + aging90Plus;

                txtAging030.Text = $"{aging030:N0} zl";
                txtAging030Procent.Text = agingTotal > 0 ? $"{aging030 / agingTotal * 100:F0}%" : "0%";
                txtAging3160.Text = $"{aging3160:N0} zl";
                txtAging3160Procent.Text = agingTotal > 0 ? $"{aging3160 / agingTotal * 100:F0}%" : "0%";
                txtAging6190.Text = $"{aging6190:N0} zl";
                txtAging6190Procent.Text = agingTotal > 0 ? $"{aging6190 / agingTotal * 100:F0}%" : "0%";
                txtAging90Plus.Text = $"{aging90Plus:N0} zl";
                txtAging90PlusProcent.Text = agingTotal > 0 ? $"{aging90Plus / agingTotal * 100:F0}%" : "0%";

                // Top 5 dluznicy - wykres
                var top5 = dane.OrderByDescending(d => d.Przeterminowane).Take(5).ToList();
                var labels = top5.Select(d => d.Kontrahent.Length > 15 ? d.Kontrahent.Substring(0, 15) + "..." : d.Kontrahent).ToList();
                var values = new ChartValues<double>(top5.Select(d => (double)d.Przeterminowane));

                chartTopDluznicy.Series = new SeriesCollection
                {
                    new RowSeries
                    {
                        Title = "Przeterminowane",
                        Values = values,
                        Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 107, 107)),
                        DataLabels = true,
                        LabelPoint = p => $"{p.X:N0}",
                        Foreground = Brushes.White
                    }
                };
                axisYDluznicy.Labels = labels;

                // Panel top dluznicy
                panelTopDluznicy.Children.Clear();
                int idx = 1;
                foreach (var d in top5)
                {
                    var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                    sp.Children.Add(new TextBlock { Text = $"{idx}. ", Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 162, 97)), FontWeight = FontWeights.Bold, Width = 20 });
                    sp.Children.Add(new TextBlock { Text = d.Kontrahent, Foreground = Brushes.White, Width = 140, TextTrimming = TextTrimming.CharacterEllipsis });
                    sp.Children.Add(new TextBlock { Text = $"{d.Przeterminowane:N0}", Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 107, 107)), FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Right });
                    panelTopDluznicy.Children.Add(sp);
                    idx++;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad wczytywania platnosci:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            gridPlatnosci.ItemsSource = dane;
        }

        private void GridPlatnosci_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (gridPlatnosci.SelectedItem is PlatnoscRow row)
            {
                var okno = new KontrahentPlatnosciWindow(row.Kontrahent, row.Handlowiec);
                okno.Show();
            }
        }

        #endregion
    }

    // Klasa danych dla tabeli opakowan
    public class OpakowanieRow
    {
        public string Kontrahent { get; set; }
        public string Handlowiec { get; set; }
        public decimal PojemnikiE2 { get; set; }
        public decimal PaletaH1 { get; set; }
        public decimal Razem { get; set; }
        public decimal ZmianaE2Tydzien { get; set; }
        public decimal ZmianaH1Tydzien { get; set; }
        public bool ZmianaE2TydzienAlert { get; set; }
        public bool ZmianaE2TydzienGood { get; set; }
        public bool ZmianaH1TydzienAlert { get; set; }
        public bool ZmianaH1TydzienGood { get; set; }
    }

    // Klasa danych dla tabeli platnosci
    public class PlatnoscRow
    {
        public string Kontrahent { get; set; }
        public string Handlowiec { get; set; }
        public decimal LimitKredytu { get; set; }
        public decimal DoZaplaty { get; set; }
        public decimal Terminowe { get; set; }
        public decimal Przeterminowane { get; set; }
        public decimal PrzekroczonyLimit { get; set; }
        public int? DniPrzeterminowania { get; set; }
        public bool PrzeterminowaneAlert { get; set; }
        public bool PrzekroczonyLimitAlert { get; set; }
        public string KategoriaWiekowa { get; set; }
    }

    // Klasa danych dla tabeli analizy cen handlowcow
    public class HandlowiecCenyRow
    {
        public string Handlowiec { get; set; }
        public decimal SumaKg { get; set; }
        public decimal SredniaCena { get; set; }
        public decimal MinCena { get; set; }
        public decimal MaxCena { get; set; }
        public decimal SumaWartosc { get; set; }
        public int LiczbaTransakcji { get; set; }
        public int LiczbaKontrahentow { get; set; }

        public string SumaKgTekst => $"{SumaKg:N2}";
        public string SredniaCenaTekst => $"{SredniaCena:F2}";
        public string MinCenaTekst => $"{MinCena:F2}";
        public string MaxCenaTekst => $"{MaxCena:F2}";
        public string SumaWartoscTekst => $"{SumaWartosc:N2}";
    }
}
