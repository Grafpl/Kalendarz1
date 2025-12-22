using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Data.SqlClient;
using DevExpress.Xpf.Charts;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;
using System.Windows.Media;

namespace Kalendarz1.HandlowiecDashboard.Views
{
    public partial class AnalizaCenHandlowcaWindow : Window
    {
        private readonly string _connectionStringHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private readonly string _handlowiec;
        private readonly int _rok;
        private readonly int _miesiac;
        private string _towar;
        private bool _isInitialized = false;

        private static readonly string[] _nazwyMiesiecy = {
            "", "Styczen", "Luty", "Marzec", "Kwiecien", "Maj", "Czerwiec",
            "Lipiec", "Sierpien", "Wrzesien", "Pazdziernik", "Listopad", "Grudzien"
        };

        public AnalizaCenHandlowcaWindow(string handlowiec, int rok, int miesiac, string towar)
        {
            ApplicationThemeHelper.ApplicationThemeName = Theme.Office2019Black.Name;
            
            InitializeComponent();
            
            _handlowiec = handlowiec;
            _rok = rok;
            _miesiac = miesiac;
            _towar = towar;

            txtHandlowiecNazwa.Text = handlowiec;
            txtOkres.Text = $"{_nazwyMiesiecy[miesiac]} {rok}";

            Loaded += Window_Loaded;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            WypelnijTowary();
            _isInitialized = true;
            await OdswiezDaneAsync();
        }

        private void WypelnijTowary()
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

            cmbTowar.ItemsSource = towary;
            cmbTowar.EditValue = _towar;
        }

        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            await OdswiezDaneAsync();
        }

        private async void CmbTowar_SelectionChanged(object sender, EditValueChangedEventArgs e)
        {
            if (!_isInitialized) return;
            _towar = cmbTowar.EditValue?.ToString() ?? "Wszystkie towary";
            await OdswiezDaneAsync();
        }

        private async Task OdswiezDaneAsync()
        {
            if (!_isInitialized) return;

            try
            {
                loadingOverlay.Visibility = Visibility.Visible;

                await Task.Run(() =>
                {
                    using var cn = new SqlConnection(_connectionStringHandel);
                    cn.Open();

                    var towarFilter = _towar == "Wszystkie towary" ? "" : " AND TW.Nazwa = @Towar";

                    // Podsumowanie
                    var sqlSummary = $@"
SELECT 
    SUM(FP.Ilosc) AS SumaKg,
    AVG(CASE WHEN FP.Ilosc > 0 THEN FP.brutto / FP.Ilosc ELSE 0 END) AS SredniaCena,
    MIN(CASE WHEN FP.Ilosc > 0 THEN FP.brutto / FP.Ilosc ELSE 0 END) AS MinCena,
    MAX(CASE WHEN FP.Ilosc > 0 THEN FP.brutto / FP.Ilosc ELSE 0 END) AS MaxCena,
    SUM(FP.brutto) AS SumaWartosc
FROM [HM].[FakturaKontrahent] FK
INNER JOIN [HM].[FakturaPozycja] FP ON FK.id = FP.super
INNER JOIN [HM].[TW] TW ON FP.idtw = TW.id
INNER JOIN [SSCommon].[STContractors] C ON FK.khid = C.Id
LEFT JOIN [SSCommon].[ContractorClassification] WYM ON C.Id = WYM.ElementId
WHERE YEAR(FK.data) = @Rok AND MONTH(FK.data) = @Miesiac
  AND FK.typ = 1 AND FK.anulowany = 0
  AND ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany') = @Handlowiec{towarFilter}";

                    using (var cmd = new SqlCommand(sqlSummary, cn))
                    {
                        cmd.Parameters.AddWithValue("@Rok", _rok);
                        cmd.Parameters.AddWithValue("@Miesiac", _miesiac);
                        cmd.Parameters.AddWithValue("@Handlowiec", _handlowiec);
                        if (_towar != "Wszystkie towary")
                            cmd.Parameters.AddWithValue("@Towar", _towar);

                        using var reader = cmd.ExecuteReader();
                        if (reader.Read())
                        {
                            var sumaKg = reader.IsDBNull(0) ? 0 : Convert.ToDecimal(reader.GetValue(0));
                            var sredniaCena = reader.IsDBNull(1) ? 0 : Convert.ToDecimal(reader.GetValue(1));
                            var minCena = reader.IsDBNull(2) ? 0 : Convert.ToDecimal(reader.GetValue(2));
                            var maxCena = reader.IsDBNull(3) ? 0 : Convert.ToDecimal(reader.GetValue(3));
                            var sumaWartosc = reader.IsDBNull(4) ? 0 : Convert.ToDecimal(reader.GetValue(4));

                            Dispatcher.Invoke(() =>
                            {
                                txtSumaKg.Text = $"{sumaKg:N0} kg";
                                txtSredniaCena.Text = $"{sredniaCena:F2} zl/kg";
                                txtMinCena.Text = $"{minCena:F2} zl/kg";
                                txtMaxCena.Text = $"{maxCena:F2} zl/kg";
                                txtSumaWartosc.Text = $"{sumaWartosc:N0} zl";
                            });
                        }
                    }

                    // Historia cen (po dniach)
                    var sqlHistory = $@"
SELECT 
    CAST(FK.data AS DATE) AS Dzien,
    AVG(CASE WHEN FP.Ilosc > 0 THEN FP.brutto / FP.Ilosc ELSE 0 END) AS SredniaCena
FROM [HM].[FakturaKontrahent] FK
INNER JOIN [HM].[FakturaPozycja] FP ON FK.id = FP.super
INNER JOIN [HM].[TW] TW ON FP.idtw = TW.id
INNER JOIN [SSCommon].[STContractors] C ON FK.khid = C.Id
LEFT JOIN [SSCommon].[ContractorClassification] WYM ON C.Id = WYM.ElementId
WHERE YEAR(FK.data) = @Rok AND MONTH(FK.data) = @Miesiac
  AND FK.typ = 1 AND FK.anulowany = 0
  AND ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany') = @Handlowiec{towarFilter}
GROUP BY CAST(FK.data AS DATE)
ORDER BY Dzien";

                    var historiaCen = new List<(string Dzien, double Cena)>();
                    using (var cmd = new SqlCommand(sqlHistory, cn))
                    {
                        cmd.Parameters.AddWithValue("@Rok", _rok);
                        cmd.Parameters.AddWithValue("@Miesiac", _miesiac);
                        cmd.Parameters.AddWithValue("@Handlowiec", _handlowiec);
                        if (_towar != "Wszystkie towary")
                            cmd.Parameters.AddWithValue("@Towar", _towar);

                        using var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            var dzien = reader.GetDateTime(0).ToString("dd.MM");
                            var cena = reader.IsDBNull(1) ? 0 : Convert.ToDouble(reader.GetValue(1));
                            historiaCen.Add((dzien, cena));
                        }
                    }

                    Dispatcher.Invoke(() =>
                    {
                        var diagram = chartCenyWCzasie.Diagram as XYDiagram2D;
                        if (diagram != null)
                        {
                            diagram.Series.Clear();
                            var series = new LineSeries2D
                            {
                                DisplayName = "Cena",
                                Brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F4A261")),
                                MarkerVisible = true
                            };

                            foreach (var item in historiaCen)
                            {
                                series.Points.Add(new SeriesPoint(item.Dzien, item.Cena));
                            }
                            diagram.Series.Add(series);
                        }
                    });

                    // Per kontrahent
                    var sqlKontrahent = $@"
SELECT 
    C.shortcut AS Kontrahent,
    SUM(FP.Ilosc) AS SumaKg,
    AVG(CASE WHEN FP.Ilosc > 0 THEN FP.brutto / FP.Ilosc ELSE 0 END) AS Cena,
    SUM(FP.brutto) AS Wartosc
FROM [HM].[FakturaKontrahent] FK
INNER JOIN [HM].[FakturaPozycja] FP ON FK.id = FP.super
INNER JOIN [HM].[TW] TW ON FP.idtw = TW.id
INNER JOIN [SSCommon].[STContractors] C ON FK.khid = C.Id
LEFT JOIN [SSCommon].[ContractorClassification] WYM ON C.Id = WYM.ElementId
WHERE YEAR(FK.data) = @Rok AND MONTH(FK.data) = @Miesiac
  AND FK.typ = 1 AND FK.anulowany = 0
  AND ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany') = @Handlowiec{towarFilter}
GROUP BY C.shortcut
ORDER BY Wartosc DESC";

                    var daneKontrahent = new List<KontrahentCenyRow>();
                    using (var cmd = new SqlCommand(sqlKontrahent, cn))
                    {
                        cmd.Parameters.AddWithValue("@Rok", _rok);
                        cmd.Parameters.AddWithValue("@Miesiac", _miesiac);
                        cmd.Parameters.AddWithValue("@Handlowiec", _handlowiec);
                        if (_towar != "Wszystkie towary")
                            cmd.Parameters.AddWithValue("@Towar", _towar);

                        using var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            daneKontrahent.Add(new KontrahentCenyRow
                            {
                                Kontrahent = reader.GetString(0),
                                SumaKg = reader.IsDBNull(1) ? 0 : Convert.ToDecimal(reader.GetValue(1)),
                                Cena = reader.IsDBNull(2) ? 0 : Convert.ToDecimal(reader.GetValue(2)),
                                Wartosc = reader.IsDBNull(3) ? 0 : Convert.ToDecimal(reader.GetValue(3))
                            });
                        }
                    }

                    Dispatcher.Invoke(() => gridPerKontrahent.ItemsSource = daneKontrahent);

                    // Per produkt
                    var sqlProdukt = $@"
SELECT 
    TW.kod AS Kod,
    TW.nazwa AS Towar,
    SUM(FP.Ilosc) AS SumaKg,
    AVG(CASE WHEN FP.Ilosc > 0 THEN FP.brutto / FP.Ilosc ELSE 0 END) AS SredniaCena,
    MIN(CASE WHEN FP.Ilosc > 0 THEN FP.brutto / FP.Ilosc ELSE 0 END) AS MinCena,
    MAX(CASE WHEN FP.Ilosc > 0 THEN FP.brutto / FP.Ilosc ELSE 0 END) AS MaxCena,
    SUM(FP.brutto) AS SumaWartosc,
    COUNT(*) AS LiczbaTransakcji
FROM [HM].[FakturaKontrahent] FK
INNER JOIN [HM].[FakturaPozycja] FP ON FK.id = FP.super
INNER JOIN [HM].[TW] TW ON FP.idtw = TW.id
INNER JOIN [SSCommon].[STContractors] C ON FK.khid = C.Id
LEFT JOIN [SSCommon].[ContractorClassification] WYM ON C.Id = WYM.ElementId
WHERE YEAR(FK.data) = @Rok AND MONTH(FK.data) = @Miesiac
  AND FK.typ = 1 AND FK.anulowany = 0
  AND ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany') = @Handlowiec{towarFilter}
GROUP BY TW.kod, TW.nazwa
ORDER BY SumaWartosc DESC";

                    var daneProdukt = new List<ProduktCenyRow>();
                    using (var cmd = new SqlCommand(sqlProdukt, cn))
                    {
                        cmd.Parameters.AddWithValue("@Rok", _rok);
                        cmd.Parameters.AddWithValue("@Miesiac", _miesiac);
                        cmd.Parameters.AddWithValue("@Handlowiec", _handlowiec);
                        if (_towar != "Wszystkie towary")
                            cmd.Parameters.AddWithValue("@Towar", _towar);

                        using var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            daneProdukt.Add(new ProduktCenyRow
                            {
                                Kod = reader.GetString(0),
                                Towar = reader.GetString(1),
                                SumaKg = reader.IsDBNull(2) ? 0 : Convert.ToDecimal(reader.GetValue(2)),
                                SredniaCena = reader.IsDBNull(3) ? 0 : Convert.ToDecimal(reader.GetValue(3)),
                                MinCena = reader.IsDBNull(4) ? 0 : Convert.ToDecimal(reader.GetValue(4)),
                                MaxCena = reader.IsDBNull(5) ? 0 : Convert.ToDecimal(reader.GetValue(5)),
                                SumaWartosc = reader.IsDBNull(6) ? 0 : Convert.ToDecimal(reader.GetValue(6)),
                                LiczbaTransakcji = reader.IsDBNull(7) ? 0 : reader.GetInt32(7)
                            });
                        }
                    }

                    Dispatcher.Invoke(() => gridPerProdukt.ItemsSource = daneProdukt);
                });
            }
            catch (Exception ex)
            {
                DXMessageBox.Show($"Blad wczytywania danych: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                loadingOverlay.Visibility = Visibility.Collapsed;
            }
        }
    }

    public class KontrahentCenyRow
    {
        public string Kontrahent { get; set; }
        public decimal SumaKg { get; set; }
        public decimal Cena { get; set; }
        public decimal Wartosc { get; set; }

        public string SumaKgTekst => $"{SumaKg:N2}";
        public string CenaTekst => $"{Cena:F2}";
        public string WartoscTekst => $"{Wartosc:N2}";
    }

    public class ProduktCenyRow
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
