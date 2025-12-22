using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Data.SqlClient;
using DevExpress.Xpf.Charts;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;
using DevExpress.Xpf.Grid;

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

    // Klasa pomocnicza do ComboBox
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

        public HandlowiecDashboardWindow()
        {
            // Ustaw motyw DevExpress
            ApplicationThemeHelper.ApplicationThemeName = Theme.Office2019Black.Name;
            
            InitializeComponent();
            DataContext = this;
            Loaded += Window_Loaded;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                loadingOverlay.Visibility = Visibility.Visible;

                WypelnijLataIMiesiace();
                _isInitialized = true;
                await OdswiezSprzedazMiesiecznaAsync();
            }
            catch (Exception ex)
            {
                DXMessageBox.Show($"Blad inicjalizacji: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
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
            cmbRokSprzedaz.EditValue = DateTime.Now.Year;
            cmbMiesiacSprzedaz.ItemsSource = miesiace;
            cmbMiesiacSprzedaz.EditValue = miesiace.FirstOrDefault(m => m.Value == DateTime.Now.Month);

            // Top 10
            cmbRokTop10.ItemsSource = lata;
            cmbRokTop10.EditValue = DateTime.Now.Year;
            cmbMiesiacTop10.ItemsSource = miesiace;
            cmbMiesiacTop10.EditValue = miesiace.FirstOrDefault(m => m.Value == DateTime.Now.Month);
            WypelnijTowary(cmbTowarTop10);

            // Udzial handlowcow
            cmbRokUdzialOd.ItemsSource = lata;
            cmbRokUdzialOd.EditValue = DateTime.Now.Year;
            cmbMiesiacUdzialOd.ItemsSource = miesiace;
            cmbMiesiacUdzialOd.EditValue = miesiace.FirstOrDefault(m => m.Value == 1);
            cmbRokUdzialDo.ItemsSource = lata;
            cmbRokUdzialDo.EditValue = DateTime.Now.Year;
            cmbMiesiacUdzialDo.ItemsSource = miesiace;
            cmbMiesiacUdzialDo.EditValue = miesiace.FirstOrDefault(m => m.Value == DateTime.Now.Month);

            // Analiza cen
            cmbRokCeny.ItemsSource = lata;
            cmbRokCeny.EditValue = DateTime.Now.Year;
            cmbMiesiacCeny.ItemsSource = miesiace;
            cmbMiesiacCeny.EditValue = miesiace.FirstOrDefault(m => m.Value == DateTime.Now.Month);
            WypelnijTowary(cmbTowarCeny);

            // Swieze vs Mrozone
            cmbRokSM.ItemsSource = lata;
            cmbRokSM.EditValue = DateTime.Now.Year;
            cmbMiesiacSM.ItemsSource = miesiace;
            cmbMiesiacSM.EditValue = miesiace.FirstOrDefault(m => m.Value == DateTime.Now.Month);

            // Porownanie okresow
            cmbRokPorown1.ItemsSource = lata;
            cmbRokPorown1.EditValue = DateTime.Now.Year - 1;
            cmbRokPorown2.ItemsSource = lata;
            cmbRokPorown2.EditValue = DateTime.Now.Year;

            // Trend sprzedazy
            cmbOkres.ItemsSource = new[] { 3, 6, 9, 12, 18, 24 };
            cmbOkres.EditValue = 12;

            // Opakowania i Platnosci - wczytaj liste handlowcow
            WypelnijHandlowcow();

            // Ustaw daty dla opakowan
            dpOpakOd.EditValue = DateTime.Today.AddDays(-30);
            dpOpakDo.EditValue = DateTime.Today;
        }

        private void WypelnijHandlowcow()
        {
            var handlowcy = new List<ComboItem> { new ComboItem { Value = 0, Text = "Wszyscy handlowcy" } };
            try
            {
                using var cn = new SqlConnection(_connectionStringHandel);
                cn.Open();
                var sql = @"SELECT DISTINCT WYM.CDim_Handlowiec_Val AS Handlowiec
                            FROM [SSCommon].[ContractorClassification] WYM
                            WHERE WYM.CDim_Handlowiec_Val IS NOT NULL AND WYM.CDim_Handlowiec_Val != ''
                            ORDER BY Handlowiec";

                using var cmd = new SqlCommand(sql, cn);
                using var reader = cmd.ExecuteReader();
                int i = 1;
                while (reader.Read())
                {
                    handlowcy.Add(new ComboItem { Value = i++, Text = reader.GetString(0) });
                }
            }
            catch { }

            cmbHandlowiecOpak.ItemsSource = handlowcy;
            cmbHandlowiecOpak.EditValue = handlowcy.FirstOrDefault();
            cmbHandlowiecPlatnosci.ItemsSource = handlowcy;
            cmbHandlowiecPlatnosci.EditValue = handlowcy.FirstOrDefault();
        }

        private void WypelnijTowary(ComboBoxEdit cmb)
        {
            var towary = new List<string> { "Wszystkie towary" };
            try
            {
                using var cn = new SqlConnection(_connectionStringHandel);
                cn.Open();
                var sql = @"SELECT DISTINCT TW.Nazwa
                            FROM [HM].[TW] TW
                            WHERE TW.Nazwa LIKE '%kurczak%' OR TW.Nazwa LIKE '%filet%' OR TW.Nazwa LIKE '%skrzyd%'
                               OR TW.Nazwa LIKE '%udziec%' OR TW.Nazwa LIKE '%piers%' OR TW.Nazwa LIKE '%miel%'
                            ORDER BY TW.Nazwa";

                using var cmd = new SqlCommand(sql, cn);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    towary.Add(reader.GetString(0));
                }
            }
            catch { }

            cmb.ItemsSource = towary;
            cmb.EditValue = towary.FirstOrDefault();
        }

        #region Event Handlers

        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            await OdswiezBiezacaZakladkeAsync();
        }

        private async void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            await OdswiezBiezacaZakladkeAsync();
        }

        private async Task OdswiezBiezacaZakladkeAsync()
        {
            if (!_isInitialized) return;
            
            try
            {
                loadingOverlay.Visibility = Visibility.Visible;

                var selectedTab = tabControl.SelectedItem as DXTabItem;
                if (selectedTab == null) return;

                var header = selectedTab.Header?.ToString() ?? "";
                
                switch (header)
                {
                    case "Sprzedaz miesieczna":
                        await OdswiezSprzedazMiesiecznaAsync();
                        break;
                    case "Top 15 kontrahentow":
                        await OdswiezTop15Async();
                        break;
                    case "Udzial handlowcow":
                        await OdswiezUdzialHandlowcowAsync();
                        break;
                    case "Analiza cen":
                        await OdswiezAnalizeCenAsync();
                        break;
                    case "Swieze vs Mrozone":
                        await OdswiezSwiezeMrozoneAsync();
                        break;
                    case "Porownanie okresow":
                        await OdswiezPorownanieOkresowAsync();
                        break;
                    case "Trend sprzedazy":
                        await OdswiezTrendSprzedazyAsync();
                        break;
                    case "Opakowania":
                        await OdswiezOpakowaniaAsync();
                        break;
                    case "Platnosci":
                        await OdswiezPlatnosciAsync();
                        break;
                }
            }
            catch (Exception ex)
            {
                DXMessageBox.Show($"Blad odswiezania: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                loadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private async void CmbRokSprzedaz_SelectionChanged(object sender, EditValueChangedEventArgs e) => await OdswiezSprzedazMiesiecznaAsync();
        private async void CmbMiesiacSprzedaz_SelectionChanged(object sender, EditValueChangedEventArgs e) => await OdswiezSprzedazMiesiecznaAsync();
        private async void CmbTop10_SelectionChanged(object sender, EditValueChangedEventArgs e) => await OdswiezTop15Async();
        private async void CmbUdzial_SelectionChanged(object sender, EditValueChangedEventArgs e) => await OdswiezUdzialHandlowcowAsync();
        private async void CmbCeny_SelectionChanged(object sender, EditValueChangedEventArgs e) => await OdswiezAnalizeCenAsync();
        private async void CmbSM_SelectionChanged(object sender, EditValueChangedEventArgs e) => await OdswiezSwiezeMrozoneAsync();
        private async void CmbPorown_SelectionChanged(object sender, EditValueChangedEventArgs e) => await OdswiezPorownanieOkresowAsync();
        private async void CmbOkres_SelectionChanged(object sender, EditValueChangedEventArgs e) => await OdswiezTrendSprzedazyAsync();
        private async void CmbOpakowania_SelectionChanged(object sender, EditValueChangedEventArgs e) => await OdswiezOpakowaniaAsync();
        private async void DpOpakowania_SelectedDateChanged(object sender, EditValueChangedEventArgs e) => await OdswiezOpakowaniaAsync();
        private async void CmbPlatnosci_SelectionChanged(object sender, EditValueChangedEventArgs e) => await OdswiezPlatnosciAsync();

        private async void BtnOpakOkres_Click(object sender, RoutedEventArgs e)
        {
            if (sender is SimpleButton btn && btn.Tag != null)
            {
                var dni = int.Parse(btn.Tag.ToString());
                if (dni == 0)
                {
                    dpOpakOd.EditValue = DateTime.Today;
                    dpOpakDo.EditValue = DateTime.Today;
                }
                else if (dni == 9999)
                {
                    dpOpakOd.EditValue = new DateTime(2020, 1, 1);
                    dpOpakDo.EditValue = DateTime.Today;
                }
                else
                {
                    dpOpakOd.EditValue = DateTime.Today.AddDays(-dni);
                    dpOpakDo.EditValue = DateTime.Today;
                }
                await OdswiezOpakowaniaAsync();
            }
        }

        #endregion

        #region Odswiez metody - DevExpress Charts

        private async Task OdswiezSprzedazMiesiecznaAsync()
        {
            if (!_isInitialized) return;

            var rok = cmbRokSprzedaz.EditValue as int? ?? DateTime.Now.Year;
            var miesiacItem = cmbMiesiacSprzedaz.EditValue as ComboItem;
            var miesiac = miesiacItem?.Value ?? DateTime.Now.Month;

            await Task.Run(() =>
            {
                try
                {
                    using var cn = new SqlConnection(_connectionStringHandel);
                    cn.Open();

                    var sql = @"
SELECT 
    ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany') AS Handlowiec,
    SUM(FP.brutto) AS Wartosc
FROM [HM].[FakturaKontrahent] FK
INNER JOIN [HM].[FakturaPozycja] FP ON FK.id = FP.super
INNER JOIN [SSCommon].[STContractors] C ON FK.khid = C.Id
LEFT JOIN [SSCommon].[ContractorClassification] WYM ON C.Id = WYM.ElementId
WHERE YEAR(FK.data) = @Rok AND MONTH(FK.data) = @Miesiac
  AND FK.typ = 1 AND FK.anulowany = 0
GROUP BY ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany')
ORDER BY Wartosc DESC";

                    using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Rok", rok);
                    cmd.Parameters.AddWithValue("@Miesiac", miesiac);

                    var dane = new List<KeyValuePair<string, double>>();
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var handlowiec = reader.GetString(0);
                        var wartosc = reader.IsDBNull(1) ? 0 : Convert.ToDouble(reader.GetDecimal(1));
                        dane.Add(new KeyValuePair<string, double>(handlowiec, wartosc));
                    }

                    Dispatcher.Invoke(() =>
                    {
                        var diagram = chartSprzedaz.Diagram as XYDiagram2D;
                        if (diagram != null)
                        {
                            diagram.Series.Clear();
                            
                            var series = new BarSideBySideSeries2D
                            {
                                DisplayName = "Sprzedaz",
                                Brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C0392B"))
                            };

                            foreach (var item in dane)
                            {
                                series.Points.Add(new SeriesPoint(item.Key, item.Value));
                            }

                            diagram.Series.Add(series);
                        }
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                        DXMessageBox.Show($"Blad wczytywania danych: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error));
                }
            });
        }

        private async Task OdswiezTop15Async()
        {
            if (!_isInitialized) return;

            var rok = cmbRokTop10.EditValue as int? ?? DateTime.Now.Year;
            var miesiacItem = cmbMiesiacTop10.EditValue as ComboItem;
            var miesiac = miesiacItem?.Value ?? DateTime.Now.Month;
            var towar = cmbTowarTop10.EditValue?.ToString() ?? "Wszystkie towary";

            await Task.Run(() =>
            {
                try
                {
                    using var cn = new SqlConnection(_connectionStringHandel);
                    cn.Open();

                    var towarFilter = towar == "Wszystkie towary" ? "" : " AND TW.Nazwa = @Towar";

                    var sql = $@"
SELECT TOP 15
    C.shortcut AS Kontrahent,
    ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany') AS Handlowiec,
    SUM(FP.brutto) AS Wartosc
FROM [HM].[FakturaKontrahent] FK
INNER JOIN [HM].[FakturaPozycja] FP ON FK.id = FP.super
INNER JOIN [HM].[TW] TW ON FP.idtw = TW.id
INNER JOIN [SSCommon].[STContractors] C ON FK.khid = C.Id
LEFT JOIN [SSCommon].[ContractorClassification] WYM ON C.Id = WYM.ElementId
WHERE YEAR(FK.data) = @Rok AND MONTH(FK.data) = @Miesiac
  AND FK.typ = 1 AND FK.anulowany = 0{towarFilter}
GROUP BY C.shortcut, ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany')
ORDER BY Wartosc DESC";

                    using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Rok", rok);
                    cmd.Parameters.AddWithValue("@Miesiac", miesiac);
                    if (towar != "Wszystkie towary")
                        cmd.Parameters.AddWithValue("@Towar", towar);

                    var dane = new List<(string Kontrahent, string Handlowiec, double Wartosc)>();
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        dane.Add((
                            reader.GetString(0),
                            reader.GetString(1),
                            reader.IsDBNull(2) ? 0 : Convert.ToDouble(reader.GetDecimal(2))
                        ));
                    }

                    Dispatcher.Invoke(() =>
                    {
                        // Budowanie legendy
                        panelLegendaTop15.Children.Clear();
                        var handlowcyUnique = dane.Select(d => d.Handlowiec).Distinct().ToList();
                        var handlowiecKolor = new Dictionary<string, Color>();
                        
                        for (int i = 0; i < handlowcyUnique.Count && i < _kolory.Length; i++)
                        {
                            handlowiecKolor[handlowcyUnique[i]] = _kolory[i];
                            
                            var legendItem = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 15, 0) };
                            legendItem.Children.Add(new System.Windows.Shapes.Ellipse 
                            { 
                                Width = 10, Height = 10, 
                                Fill = new SolidColorBrush(_kolory[i]),
                                VerticalAlignment = VerticalAlignment.Center
                            });
                            legendItem.Children.Add(new TextBlock 
                            { 
                                Text = " " + handlowcyUnique[i], 
                                FontSize = 10, 
                                Foreground = new SolidColorBrush(_kolory[i]),
                                VerticalAlignment = VerticalAlignment.Center
                            });
                            panelLegendaTop15.Children.Add(legendItem);
                        }

                        // Wykres
                        var diagram = chartTop10.Diagram as XYDiagram2D;
                        if (diagram != null)
                        {
                            diagram.Series.Clear();
                            
                            // Grupuj po handlowcu
                            foreach (var handlowiec in handlowcyUnique)
                            {
                                var kolor = handlowiecKolor.ContainsKey(handlowiec) ? handlowiecKolor[handlowiec] : Colors.Gray;
                                var series = new BarSideBySideSeries2D
                                {
                                    DisplayName = handlowiec,
                                    Brush = new SolidColorBrush(kolor)
                                };

                                foreach (var item in dane.Where(d => d.Handlowiec == handlowiec))
                                {
                                    series.Points.Add(new SeriesPoint(item.Kontrahent, item.Wartosc));
                                }

                                diagram.Series.Add(series);
                            }
                        }

                        var suma = dane.Sum(d => d.Wartosc);
                        txtTop10Info.Text = $"Suma TOP 15: {suma:N0} zl";
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                        DXMessageBox.Show($"Blad wczytywania Top15: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error));
                }
            });
        }

        private async Task OdswiezUdzialHandlowcowAsync()
        {
            if (!_isInitialized) return;
            // TODO: Implementacja wykresów udziału handlowców
            await Task.CompletedTask;
        }

        private async Task OdswiezAnalizeCenAsync()
        {
            if (!_isInitialized) return;

            var rok = cmbRokCeny.EditValue as int? ?? DateTime.Now.Year;
            var miesiacItem = cmbMiesiacCeny.EditValue as ComboItem;
            var miesiac = miesiacItem?.Value ?? DateTime.Now.Month;
            var towar = cmbTowarCeny.EditValue?.ToString() ?? "Wszystkie towary";

            await Task.Run(() =>
            {
                try
                {
                    using var cn = new SqlConnection(_connectionStringHandel);
                    cn.Open();

                    var towarFilter = towar == "Wszystkie towary" ? "" : " AND TW.Nazwa = @Towar";

                    var sql = $@"
SELECT 
    ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany') AS Handlowiec,
    SUM(FP.Ilosc) AS SumaKg,
    AVG(CASE WHEN FP.Ilosc > 0 THEN FP.brutto / FP.Ilosc ELSE 0 END) AS SredniaCena,
    MIN(CASE WHEN FP.Ilosc > 0 THEN FP.brutto / FP.Ilosc ELSE 0 END) AS MinCena,
    MAX(CASE WHEN FP.Ilosc > 0 THEN FP.brutto / FP.Ilosc ELSE 0 END) AS MaxCena,
    SUM(FP.brutto) AS SumaWartosc,
    COUNT(*) AS LiczbaTransakcji,
    COUNT(DISTINCT FK.khid) AS LiczbaKontrahentow
FROM [HM].[FakturaKontrahent] FK
INNER JOIN [HM].[FakturaPozycja] FP ON FK.id = FP.super
INNER JOIN [HM].[TW] TW ON FP.idtw = TW.id
INNER JOIN [SSCommon].[STContractors] C ON FK.khid = C.Id
LEFT JOIN [SSCommon].[ContractorClassification] WYM ON C.Id = WYM.ElementId
WHERE YEAR(FK.data) = @Rok AND MONTH(FK.data) = @Miesiac
  AND FK.typ = 1 AND FK.anulowany = 0{towarFilter}
GROUP BY ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany')
ORDER BY SredniaCena DESC";

                    using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Rok", rok);
                    cmd.Parameters.AddWithValue("@Miesiac", miesiac);
                    if (towar != "Wszystkie towary")
                        cmd.Parameters.AddWithValue("@Towar", towar);

                    var dane = new List<HandlowiecCenyRow>();
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        dane.Add(new HandlowiecCenyRow
                        {
                            Handlowiec = reader.GetString(0),
                            SumaKg = reader.IsDBNull(1) ? 0 : Convert.ToDecimal(reader.GetValue(1)),
                            SredniaCena = reader.IsDBNull(2) ? 0 : Convert.ToDecimal(reader.GetValue(2)),
                            MinCena = reader.IsDBNull(3) ? 0 : Convert.ToDecimal(reader.GetValue(3)),
                            MaxCena = reader.IsDBNull(4) ? 0 : Convert.ToDecimal(reader.GetValue(4)),
                            SumaWartosc = reader.IsDBNull(5) ? 0 : Convert.ToDecimal(reader.GetValue(5)),
                            LiczbaTransakcji = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                            LiczbaKontrahentow = reader.IsDBNull(7) ? 0 : reader.GetInt32(7)
                        });
                    }

                    Dispatcher.Invoke(() =>
                    {
                        gridAnalizaCeny.ItemsSource = dane;

                        // Wykres cen
                        var diagramCeny = chartCeny.Diagram as XYDiagram2D;
                        if (diagramCeny != null)
                        {
                            diagramCeny.Series.Clear();
                            var series = new BarSideBySideSeries2D
                            {
                                DisplayName = "Srednia cena",
                                Brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C0392B"))
                            };

                            foreach (var item in dane)
                            {
                                series.Points.Add(new SeriesPoint(item.Handlowiec, (double)item.SredniaCena));
                            }
                            diagramCeny.Series.Add(series);
                        }

                        // Wykres kg
                        var diagramKg = chartCenyKg.Diagram as XYDiagram2D;
                        if (diagramKg != null)
                        {
                            diagramKg.Series.Clear();
                            var series = new BarSideBySideSeries2D
                            {
                                DisplayName = "Ilosc kg",
                                Brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#17A589"))
                            };

                            foreach (var item in dane)
                            {
                                series.Points.Add(new SeriesPoint(item.Handlowiec, (double)item.SumaKg));
                            }
                            diagramKg.Series.Add(series);
                        }
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                        DXMessageBox.Show($"Blad analizy cen: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error));
                }
            });
        }

        private async Task OdswiezSwiezeMrozoneAsync()
        {
            if (!_isInitialized) return;
            // TODO: Implementacja wykresów świeże vs mrożone
            await Task.CompletedTask;
        }

        private async Task OdswiezPorownanieOkresowAsync()
        {
            if (!_isInitialized) return;
            // TODO: Implementacja porównania okresów
            await Task.CompletedTask;
        }

        private async Task OdswiezTrendSprzedazyAsync()
        {
            if (!_isInitialized) return;

            var okres = cmbOkres.EditValue as int? ?? 12;

            await Task.Run(() =>
            {
                try
                {
                    using var cn = new SqlConnection(_connectionStringHandel);
                    cn.Open();

                    var sql = @"
SELECT 
    YEAR(FK.data) AS Rok,
    MONTH(FK.data) AS Miesiac,
    SUM(FP.brutto) AS Wartosc
FROM [HM].[FakturaKontrahent] FK
INNER JOIN [HM].[FakturaPozycja] FP ON FK.id = FP.super
WHERE FK.data >= DATEADD(MONTH, -@Okres, GETDATE())
  AND FK.typ = 1 AND FK.anulowany = 0
GROUP BY YEAR(FK.data), MONTH(FK.data)
ORDER BY Rok, Miesiac";

                    using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Okres", okres);

                    var dane = new List<(string Miesiac, double Wartosc)>();
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var rok = reader.GetInt32(0);
                        var miesiac = reader.GetInt32(1);
                        var wartosc = reader.IsDBNull(2) ? 0 : Convert.ToDouble(reader.GetDecimal(2));
                        dane.Add(($"{_nazwyMiesiecy[miesiac]}/{rok % 100}", wartosc));
                    }

                    Dispatcher.Invoke(() =>
                    {
                        var diagram = chartTrend.Diagram as XYDiagram2D;
                        if (diagram != null)
                        {
                            diagram.Series.Clear();
                            var series = new LineSeries2D
                            {
                                DisplayName = "Trend",
                                Brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C0392B")),
                                MarkerVisible = true
                            };

                            foreach (var item in dane)
                            {
                                series.Points.Add(new SeriesPoint(item.Miesiac, item.Wartosc));
                            }
                            diagram.Series.Add(series);
                        }
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                        DXMessageBox.Show($"Blad trendu: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error));
                }
            });
        }

        private async Task OdswiezOpakowaniaAsync()
        {
            if (!_isInitialized) return;
            // TODO: Pełna implementacja opakowań
            await Task.CompletedTask;
        }

        private async Task OdswiezPlatnosciAsync()
        {
            if (!_isInitialized) return;

            var handlowiecItem = cmbHandlowiecPlatnosci.EditValue as ComboItem;
            var handlowiec = handlowiecItem?.Text;
            var wszystcy = handlowiecItem?.Value == 0;

            await Task.Run(() =>
            {
                try
                {
                    using var cn = new SqlConnection(_connectionStringHandel);
                    cn.Open();

                    var whereHandlowiec = wszystcy ? "" : " AND ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany') = @Handlowiec";

                    var sql = $@"
SELECT 
    C.shortcut AS Kontrahent,
    ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany') AS Handlowiec,
    ISNULL(C.CreditLimit, 0) AS LimitKredytu,
    SUM(FK.brutto - ISNULL(FK.rozliczono, 0)) AS DoZaplaty,
    SUM(CASE WHEN FK.termin >= GETDATE() THEN FK.brutto - ISNULL(FK.rozliczono, 0) ELSE 0 END) AS Terminowe,
    SUM(CASE WHEN FK.termin < GETDATE() THEN FK.brutto - ISNULL(FK.rozliczono, 0) ELSE 0 END) AS Przeterminowane,
    MAX(CASE WHEN FK.termin < GETDATE() THEN DATEDIFF(DAY, FK.termin, GETDATE()) ELSE NULL END) AS DniPrzeterminowania
FROM [HM].[FakturaKontrahent] FK
INNER JOIN [SSCommon].[STContractors] C ON FK.khid = C.Id
LEFT JOIN [SSCommon].[ContractorClassification] WYM ON C.Id = WYM.ElementId
WHERE FK.typ = 1 AND FK.anulowany = 0 
  AND FK.brutto > ISNULL(FK.rozliczono, 0){whereHandlowiec}
GROUP BY C.shortcut, ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany'), ISNULL(C.CreditLimit, 0)
ORDER BY Przeterminowane DESC";

                    using var cmd = new SqlCommand(sql, cn);
                    if (!wszystcy)
                        cmd.Parameters.AddWithValue("@Handlowiec", handlowiec);

                    var dane = new List<PlatnoscRow>();
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var row = new PlatnoscRow
                        {
                            Kontrahent = reader.GetString(0),
                            Handlowiec = reader.GetString(1),
                            LimitKredytu = reader.IsDBNull(2) ? 0 : Convert.ToDecimal(reader.GetValue(2)),
                            DoZaplaty = reader.IsDBNull(3) ? 0 : Convert.ToDecimal(reader.GetValue(3)),
                            Terminowe = reader.IsDBNull(4) ? 0 : Convert.ToDecimal(reader.GetValue(4)),
                            Przeterminowane = reader.IsDBNull(5) ? 0 : Convert.ToDecimal(reader.GetValue(5)),
                            DniPrzeterminowania = reader.IsDBNull(6) ? null : (int?)reader.GetInt32(6)
                        };
                        row.PrzekroczonyLimit = row.LimitKredytu > 0 ? Math.Max(0, row.DoZaplaty - row.LimitKredytu) : 0;
                        row.PrzeterminowaneAlert = row.Przeterminowane > 0;
                        row.PrzekroczonyLimitAlert = row.PrzekroczonyLimit > 0;
                        dane.Add(row);
                    }

                    Dispatcher.Invoke(() =>
                    {
                        gridPlatnosci.ItemsSource = dane;

                        var sumaTerminowe = dane.Sum(d => d.Terminowe);
                        var sumaPrzeterminowane = dane.Sum(d => d.Przeterminowane);
                        var sumaPrzekroczony = dane.Sum(d => d.PrzekroczonyLimit);
                        var sumaDoZaplaty = dane.Sum(d => d.DoZaplaty);

                        txtSumaTerminowe.Text = $"{sumaTerminowe:N2} zl";
                        txtSumaPrzeterminowane.Text = $"{sumaPrzeterminowane:N2} zl";
                        txtSumaPrzekroczony.Text = $"{sumaPrzekroczony:N2} zl";
                        txtSumaDoZaplaty.Text = $"{sumaDoZaplaty:N2} zl";

                        txtIloscTerminowe.Text = $"{dane.Count(d => d.Terminowe > 0)} kontrahentow";
                        txtIloscPrzeterminowane.Text = $"{dane.Count(d => d.Przeterminowane > 0)} kontrahentow";
                        txtIloscPrzekroczony.Text = $"{dane.Count(d => d.PrzekroczonyLimit > 0)} kontrahentow";
                        txtIloscDoZaplaty.Text = $"{dane.Count} kontrahentow";

                        // Wykres przeterminowanych wg handlowcow
                        var grupyHandlowcow = dane
                            .GroupBy(d => d.Handlowiec)
                            .Select(g => new { Handlowiec = g.Key, Wartosc = g.Sum(x => x.Przeterminowane) })
                            .Where(x => x.Wartosc > 0)
                            .OrderByDescending(x => x.Wartosc)
                            .Take(10)
                            .ToList();

                        var diagramHandl = chartPrzeterminowaneHandlowiec.Diagram as XYDiagram2D;
                        if (diagramHandl != null)
                        {
                            diagramHandl.Series.Clear();
                            var series = new BarSideBySideSeries2D
                            {
                                DisplayName = "Przeterminowane",
                                Brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF6B6B"))
                            };

                            foreach (var item in grupyHandlowcow)
                            {
                                series.Points.Add(new SeriesPoint(item.Handlowiec, (double)item.Wartosc));
                            }
                            diagramHandl.Series.Add(series);
                        }

                        // Wskazniki
                        var daneZPrzeterminowaniem = dane.Where(d => d.DniPrzeterminowania.HasValue && d.DniPrzeterminowania.Value > 0).ToList();
                        var srednieDniOpoznienia = daneZPrzeterminowaniem.Any()
                            ? daneZPrzeterminowaniem.Average(d => d.DniPrzeterminowania.Value)
                            : 0;
                        var procentPrzeterminowanych = sumaDoZaplaty > 0 ? (sumaPrzeterminowane / sumaDoZaplaty * 100) : 0;
                        var dyscyplinaPlatnicza = sumaDoZaplaty > 0 ? (sumaTerminowe / sumaDoZaplaty * 100) : 100;

                        txtWskaznikSrednieDni.Text = $"{srednieDniOpoznienia:F0} dni";
                        txtWskaznikProcentPrzeterminowanych.Text = $"{procentPrzeterminowanych:F1}%";
                        txtWskaznikLiczbaKlientow.Text = $"{dane.Count(d => d.Przeterminowane > 0)}";
                        txtWskaznikPrzekroczonyLimit.Text = $"{dane.Count(d => d.PrzekroczonyLimit > 0)}";
                        txtWskaznikDyscyplina.Text = $"{dyscyplinaPlatnicza:F1}%";
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                        DXMessageBox.Show($"Blad platnosci: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error));
                }
            });
        }

        #endregion

        #region Grid Events

        private void GridAnalizaCeny_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var grid = sender as GridControl;
            if (grid?.SelectedItem is HandlowiecCenyRow row)
            {
                var rok = cmbRokCeny.EditValue as int? ?? DateTime.Now.Year;
                var miesiacItem = cmbMiesiacCeny.EditValue as ComboItem;
                var miesiac = miesiacItem?.Value ?? DateTime.Now.Month;
                var towar = cmbTowarCeny.EditValue?.ToString() ?? "Wszystkie towary";

                var okno = new AnalizaCenHandlowcaWindow(row.Handlowiec, rok, miesiac, towar);
                okno.Show();
            }
        }

        private void GridPlatnosci_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var grid = sender as GridControl;
            if (grid?.SelectedItem is PlatnoscRow row)
            {
                var okno = new KontrahentPlatnosciWindow(row.Kontrahent, row.Handlowiec);
                okno.Show();
            }
        }

        #endregion
    }

    #region Data Classes

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

        public string LimitKredytuTekst => LimitKredytu > 0 ? $"{LimitKredytu:N2} zl" : "-";
        public string DoZaplatyTekst => $"{DoZaplaty:N2} zl";
        public string TerminoweTekst => Terminowe > 0 ? $"{Terminowe:N2} zl" : "0,00 zl";
        public string PrzeterminowaneTekst => Przeterminowane > 0 ? $"{Przeterminowane:N2} zl" : "-";
        public string PrzekroczonyLimitTekst => PrzekroczonyLimit != 0 ? $"{PrzekroczonyLimit:N2} zl" : "-";
        public string NajpozniejszaPlatnoscTekst => DniPrzeterminowania.HasValue && DniPrzeterminowania.Value > 0
            ? $"{DniPrzeterminowania.Value} dni po terminie" : "";
    }

    #endregion
}
