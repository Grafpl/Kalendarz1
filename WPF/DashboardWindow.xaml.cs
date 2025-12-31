using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.WPF
{
    public partial class DashboardWindow : Window
    {
        private readonly string _connLibra;
        private readonly string _connHandel;
        private DateTime _selectedDate;
        private bool _isLoading;

        // Dane dla 4 produktów
        private class ProductData
        {
            public int Id { get; set; }
            public string Nazwa { get; set; } = "";
            public decimal Plan { get; set; }
            public decimal Fakt { get; set; }
            public decimal Stan { get; set; }
            public decimal Zamowienia { get; set; }
            public decimal Wydania { get; set; }
            public decimal Bilans { get; set; }
        }

        public DashboardWindow(string connLibra, string connHandel, DateTime? initialDate = null)
        {
            InitializeComponent();
            _connLibra = connLibra;
            _connHandel = connHandel;
            _selectedDate = initialDate ?? DateTime.Today;

            InitializeDate();
        }

        private void InitializeDate()
        {
            dpData.SelectedDate = _selectedDate;
            txtSelectedDate.Text = $"Data: {_selectedDate:yyyy-MM-dd dddd}";
            _ = LoadDataAsync();
        }

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                bool uzywajWydan = rbBilansWydania?.IsChecked == true;
                DateTime day = _selectedDate.Date;

                // 1. Pobierz nazwy produktów z Handel
                var productInfo = new Dictionary<int, string>();
                await using (var cn = new SqlConnection(_connHandel))
                {
                    await cn.OpenAsync();
                    const string sql = "SELECT ID, kod FROM [HANDEL].[HM].[TW]";
                    await using var cmd = new SqlCommand(sql, cn);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        if (!rdr.IsDBNull(0))
                        {
                            int id = rdr.GetInt32(0);
                            string kod = rdr.IsDBNull(1) ? $"ID:{id}" : rdr.GetString(1);
                            productInfo[id] = kod;
                        }
                    }
                }

                // 2. Znajdź ID produktów po nazwach
                int? kurczakAId = null, cwiartkaId = null, filetId = null, korpusId = null;
                foreach (var p in productInfo)
                {
                    var nazwa = p.Value.ToLower();
                    if (kurczakAId == null && nazwa.Contains("kurczak") && nazwa.Contains("a") && !nazwa.Contains("b"))
                        kurczakAId = p.Key;
                    else if (cwiartkaId == null && nazwa.Contains("wiartk"))
                        cwiartkaId = p.Key;
                    else if (filetId == null && nazwa.Contains("filet"))
                        filetId = p.Key;
                    else if (korpusId == null && nazwa.Contains("korpus"))
                        korpusId = p.Key;
                }

                // 3. Pobierz konfigurację wydajności
                decimal wspolczynnikTuszki = 64m, procentA = 35m, procentB = 65m;
                try
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();
                    const string sql = @"SELECT WspolczynnikTuszki, ProcentTuszkiA, ProcentTuszkiB
                                         FROM dbo.KonfiguracjaWydajnosci WHERE Data = @Day";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Day", day);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    if (await rdr.ReadAsync())
                    {
                        wspolczynnikTuszki = rdr.IsDBNull(0) ? 64m : rdr.GetDecimal(0);
                        procentA = rdr.IsDBNull(1) ? 35m : rdr.GetDecimal(1);
                        procentB = rdr.IsDBNull(2) ? 65m : rdr.GetDecimal(2);
                    }
                }
                catch { }

                // 4. Pobierz konfigurację produktów (procenty)
                var konfiguracjaProcenty = new Dictionary<int, decimal>();
                try
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();
                    const string sql = @"SELECT ProduktId, Procent FROM dbo.KonfiguracjaProduktow WHERE Data = @Day";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Day", day);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        if (!rdr.IsDBNull(0) && !rdr.IsDBNull(1))
                            konfiguracjaProcenty[rdr.GetInt32(0)] = rdr.GetDecimal(1);
                    }
                }
                catch { }

                // 5. Pobierz harmonogram dostaw (dla obliczenia PLAN)
                decimal totalMassDek = 0m;
                await using (var cn = new SqlConnection(_connLibra))
                {
                    await cn.OpenAsync();
                    const string sql = @"SELECT WagaDek, SztukiDek FROM dbo.HarmonogramDostaw
                                         WHERE DataOdbioru = @Day AND Bufor = 'Potwierdzony'";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Day", day);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        var weight = rdr.IsDBNull(0) ? 0m : Convert.ToDecimal(rdr.GetValue(0));
                        var quantity = rdr.IsDBNull(1) ? 0m : Convert.ToDecimal(rdr.GetValue(1));
                        totalMassDek += (weight * quantity);
                    }
                }
                decimal pulaTuszki = totalMassDek * (wspolczynnikTuszki / 100m);
                decimal pulaTuszkiA = pulaTuszki * (procentA / 100m);
                decimal pulaTuszkiB = pulaTuszki * (procentB / 100m);

                // 6. Pobierz FAKT - przychody tuszki (sPWU)
                var faktTuszka = new Dictionary<int, decimal>();
                await using (var cn = new SqlConnection(_connHandel))
                {
                    await cn.OpenAsync();
                    const string sql = @"SELECT MZ.idtw, SUM(ABS(MZ.ilosc))
                                         FROM [HANDEL].[HM].[MZ] MZ
                                         JOIN [HANDEL].[HM].[MG] ON MZ.super = MG.id
                                         WHERE MG.seria = 'sPWU' AND MG.aktywny=1 AND MG.data = @Day
                                         GROUP BY MZ.idtw";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Day", day);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        int id = rdr.GetInt32(0);
                        decimal qty = rdr.IsDBNull(1) ? 0m : Convert.ToDecimal(rdr.GetValue(1));
                        faktTuszka[id] = qty;
                    }
                }

                // 7. Pobierz FAKT - przychody elementów (sPWP, PWP)
                var faktElementy = new Dictionary<int, decimal>();
                await using (var cn = new SqlConnection(_connHandel))
                {
                    await cn.OpenAsync();
                    const string sql = @"SELECT MZ.idtw, SUM(ABS(MZ.ilosc))
                                         FROM [HANDEL].[HM].[MZ] MZ
                                         JOIN [HANDEL].[HM].[MG] ON MZ.super = MG.id
                                         WHERE MG.seria IN ('sPWP', 'PWP') AND MG.aktywny=1 AND MG.data = @Day
                                         GROUP BY MZ.idtw";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Day", day);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        int id = rdr.GetInt32(0);
                        decimal qty = rdr.IsDBNull(1) ? 0m : Convert.ToDecimal(rdr.GetValue(1));
                        faktElementy[id] = qty;
                    }
                }

                // 8. Pobierz ZAMÓWIENIA dla wybranego dnia
                var orderSum = new Dictionary<int, decimal>();
                var orderIds = new List<int>();

                await using (var cn = new SqlConnection(_connLibra))
                {
                    await cn.OpenAsync();
                    const string sql = @"SELECT Id FROM dbo.ZamowieniaMieso
                                         WHERE DataUboju = @Day AND Status <> 'Anulowane'";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Day", day);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        orderIds.Add(rdr.GetInt32(0));
                    }
                }

                if (orderIds.Any())
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();
                    var sql = $@"SELECT KodTowaru, SUM(Ilosc) FROM [dbo].[ZamowieniaMiesoTowar]
                                 WHERE ZamowienieId IN ({string.Join(",", orderIds)}) GROUP BY KodTowaru";
                    await using var cmd = new SqlCommand(sql, cn);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        int id = rdr.GetInt32(0);
                        decimal qty = rdr.IsDBNull(1) ? 0m : rdr.GetDecimal(1);
                        orderSum[id] = qty;
                    }
                }

                // 9. Pobierz WYDANIA (WZ)
                var wydaniaSum = new Dictionary<int, decimal>();
                await using (var cn = new SqlConnection(_connHandel))
                {
                    await cn.OpenAsync();
                    const string sql = @"SELECT MZ.idtw, SUM(ABS(MZ.ilosc))
                                         FROM [HANDEL].[HM].[MZ] MZ
                                         JOIN [HANDEL].[HM].[MG] ON MZ.super = MG.id
                                         WHERE MG.seria IN ('sWZ','sWZ-W') AND MG.aktywny=1 AND MG.data = @Day
                                         GROUP BY MZ.idtw";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Day", day);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        int id = rdr.GetInt32(0);
                        decimal qty = rdr.IsDBNull(1) ? 0m : Convert.ToDecimal(rdr.GetValue(1));
                        wydaniaSum[id] = qty;
                    }
                }

                // 10. Pobierz STANY MAGAZYNOWE
                var stanyMag = new Dictionary<int, decimal>();
                try
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();
                    const string sql = @"SELECT ProduktId, Stan FROM dbo.StanyMagazynowe WHERE Data = @Data";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Data", day);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        if (!rdr.IsDBNull(0))
                            stanyMag[rdr.GetInt32(0)] = rdr.IsDBNull(1) ? 0m : rdr.GetDecimal(1);
                    }
                }
                catch { }

                // 11. Oblicz dane dla 4 produktów
                ProductData GetProductData(int? id, bool isTuszka)
                {
                    if (!id.HasValue) return new ProductData();

                    decimal plan = 0m, fakt = 0m;
                    if (isTuszka)
                    {
                        plan = pulaTuszkiA;
                        fakt = faktTuszka.TryGetValue(id.Value, out var f) ? f : 0m;
                    }
                    else
                    {
                        if (konfiguracjaProcenty.TryGetValue(id.Value, out var procent))
                            plan = pulaTuszkiB * (procent / 100m);
                        fakt = faktElementy.TryGetValue(id.Value, out var f) ? f : 0m;
                    }

                    decimal stan = stanyMag.TryGetValue(id.Value, out var s) ? s : 0m;
                    decimal zam = orderSum.TryGetValue(id.Value, out var z) ? z : 0m;
                    decimal wyd = wydaniaSum.TryGetValue(id.Value, out var w) ? w : 0m;
                    decimal odejmij = uzywajWydan ? wyd : zam;
                    decimal bilans = (fakt > 0 ? fakt : plan) + stan - odejmij;

                    return new ProductData
                    {
                        Id = id.Value,
                        Nazwa = productInfo.TryGetValue(id.Value, out var n) ? n : "",
                        Plan = plan,
                        Fakt = fakt,
                        Stan = stan,
                        Zamowienia = zam,
                        Wydania = wyd,
                        Bilans = bilans
                    };
                }

                var kurczakA = GetProductData(kurczakAId, true);
                var cwiartka = GetProductData(cwiartkaId, false);
                var filet = GetProductData(filetId, false);
                var korpus = GetProductData(korpusId, false);

                // 12. Aktualizuj UI
                UpdateProductCard(txtKurczakAPlan, txtKurczakAFakt, txtKurczakAStan, txtKurczakAZam,
                    txtKurczakABilans, chartKurczakA, kurczakA, Color.FromRgb(39, 174, 96));

                UpdateProductCard(txtCwiartkaPlan, txtCwiartkaFakt, txtCwiartkaStan, txtCwiartkaZam,
                    txtCwiartkaBilans, chartCwiartka, cwiartka, Color.FromRgb(52, 152, 219));

                UpdateProductCard(txtFiletPlan, txtFiletFakt, txtFiletStan, txtFiletZam,
                    txtFiletBilans, chartFilet, filet, Color.FromRgb(155, 89, 182));

                UpdateProductCard(txtKorpusPlan, txtKorpusFakt, txtKorpusStan, txtKorpusZam,
                    txtKorpusBilans, chartKorpus, korpus, Color.FromRgb(230, 126, 34));

                // 13. Aktualizuj nagłówek
                decimal totalBilans = kurczakA.Bilans + cwiartka.Bilans + filet.Bilans + korpus.Bilans;
                decimal totalZam = kurczakA.Zamowienia + cwiartka.Zamowienia + filet.Zamowienia + korpus.Zamowienia;
                txtDoSprzedania.Text = $"{Math.Max(0, totalBilans):N0} kg";
                txtZamowione.Text = $"{totalZam:N0} kg";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania danych: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void UpdateProductCard(TextBlock txtPlan, TextBlock txtFakt, TextBlock txtStan, TextBlock txtZam,
            TextBlock txtBilans, Grid chartGrid, ProductData data, Color barColor)
        {
            txtPlan.Text = $"{data.Plan:N0}";
            txtFakt.Text = $"{data.Fakt:N0}";
            txtStan.Text = $"{data.Stan:N0}";
            txtZam.Text = $"{data.Zamowienia:N0}";
            txtBilans.Text = $"{data.Bilans:N0} kg";

            // Tworzenie wykresu słupkowego
            CreateBarChart(chartGrid, data, barColor);
        }

        private void CreateBarChart(Grid chartGrid, ProductData data, Color primaryColor)
        {
            chartGrid.Children.Clear();
            chartGrid.ColumnDefinitions.Clear();

            // 4 kolumny dla: Plan, Fakt, Stan, Zam
            for (int i = 0; i < 4; i++)
                chartGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            decimal maxValue = Math.Max(1, new[] { data.Plan, data.Fakt, data.Stan, data.Zamowienia }.Max());

            var values = new[] { data.Plan, data.Fakt, data.Stan, data.Zamowienia };
            var labels = new[] { "Plan", "Fakt", "Stan", "Zam" };
            var colors = new[]
            {
                Color.FromRgb(44, 62, 80),   // Plan - ciemny
                Color.FromRgb(39, 174, 96),  // Fakt - zielony
                Color.FromRgb(52, 152, 219), // Stan - niebieski
                Color.FromRgb(231, 76, 60)   // Zam - czerwony
            };

            for (int i = 0; i < 4; i++)
            {
                var stack = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Bottom,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(5, 0, 5, 0)
                };

                // Wartość nad słupkiem
                var valueText = new TextBlock
                {
                    Text = $"{values[i]:N0}",
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(colors[i]),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 3)
                };
                stack.Children.Add(valueText);

                // Słupek
                double height = maxValue > 0 ? (double)(values[i] / maxValue) * 150 : 0;
                var bar = new Border
                {
                    Width = 45,
                    Height = Math.Max(5, height),
                    CornerRadius = new CornerRadius(4, 4, 0, 0),
                    Background = new SolidColorBrush(colors[i])
                };
                stack.Children.Add(bar);

                // Etykieta
                var label = new TextBlock
                {
                    Text = labels[i],
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 5, 0, 0)
                };
                stack.Children.Add(label);

                Grid.SetColumn(stack, i);
                chartGrid.Children.Add(stack);
            }
        }

        private void DpData_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            if (dpData.SelectedDate.HasValue)
            {
                _selectedDate = dpData.SelectedDate.Value;
                txtSelectedDate.Text = $"Data: {_selectedDate:yyyy-MM-dd dddd}";
                _ = LoadDataAsync();
            }
        }

        private void RbBilans_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            _ = LoadDataAsync();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
