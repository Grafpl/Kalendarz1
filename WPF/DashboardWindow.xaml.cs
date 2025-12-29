using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.WPF
{
    public partial class DashboardWindow : Window
    {
        private readonly string _connLibra;
        private readonly string _connHandel;
        private readonly DataTable _dtDashboard = new();
        private DateTime _selectedDate;
        private bool _isLoading;

        public DashboardWindow(string connLibra, string connHandel, DateTime? initialDate = null)
        {
            InitializeComponent();
            _connLibra = connLibra;
            _connHandel = connHandel;
            _selectedDate = initialDate ?? DateTime.Today;

            InitializeDataTable();
            InitializeDate();
        }

        private void InitializeDataTable()
        {
            _dtDashboard.Columns.Add("Plan", typeof(decimal));
            _dtDashboard.Columns.Add("Fakt", typeof(decimal));
            _dtDashboard.Columns.Add("Stan", typeof(decimal));
            _dtDashboard.Columns.Add("Zamowienia", typeof(decimal));
            _dtDashboard.Columns.Add("Wydania", typeof(decimal));
            _dtDashboard.Columns.Add("Bilans", typeof(decimal));
            _dtDashboard.Columns.Add("Produkt", typeof(string));
            _dtDashboard.Columns.Add("KodTowaru", typeof(int));
            _dtDashboard.Columns.Add("Katalog", typeof(string));

            dgDashboardProdukty.ItemsSource = _dtDashboard.DefaultView;
            SetupDataGrid();
        }

        private void SetupDataGrid()
        {
            dgDashboardProdukty.Columns.Clear();

            bool uzywajWydan = rbBilansWydania?.IsChecked == true;

            // Style dla wyrównania
            var rightStyle = new Style(typeof(TextBlock));
            rightStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            rightStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(0, 0, 10, 0)));

            var centerStyle = new Style(typeof(TextBlock));
            centerStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));

            var zamStyle = new Style(typeof(TextBlock));
            zamStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            zamStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(0, 0, 10, 0)));
            if (uzywajWydan)
            {
                zamStyle.Setters.Add(new Setter(TextBlock.TextDecorationsProperty, TextDecorations.Strikethrough));
                zamStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brushes.Gray));
            }

            var wydStyle = new Style(typeof(TextBlock));
            wydStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            wydStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(0, 0, 10, 0)));
            if (!uzywajWydan)
            {
                wydStyle.Setters.Add(new Setter(TextBlock.TextDecorationsProperty, TextDecorations.Strikethrough));
                wydStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brushes.Gray));
            }

            // 1. PLAN
            dgDashboardProdukty.Columns.Add(new DataGridTextColumn
            {
                Header = "Plan",
                Binding = new Binding("Plan") { StringFormat = "N0" },
                Width = new DataGridLength(80),
                ElementStyle = rightStyle
            });

            // 2. FAKT
            dgDashboardProdukty.Columns.Add(new DataGridTextColumn
            {
                Header = "Fakt",
                Binding = new Binding("Fakt") { StringFormat = "N0" },
                Width = new DataGridLength(80),
                ElementStyle = rightStyle
            });

            // 3. STAN
            dgDashboardProdukty.Columns.Add(new DataGridTextColumn
            {
                Header = "Stan",
                Binding = new Binding("Stan") { StringFormat = "N0" },
                Width = new DataGridLength(80),
                ElementStyle = centerStyle
            });

            // 4. ZAM.
            dgDashboardProdukty.Columns.Add(new DataGridTextColumn
            {
                Header = uzywajWydan ? "Zam." : "Zam. ✓",
                Binding = new Binding("Zamowienia") { StringFormat = "N0" },
                Width = new DataGridLength(85),
                ElementStyle = zamStyle
            });

            // 5. WYD.
            dgDashboardProdukty.Columns.Add(new DataGridTextColumn
            {
                Header = uzywajWydan ? "Wyd. ✓" : "Wyd.",
                Binding = new Binding("Wydania") { StringFormat = "N0" },
                Width = new DataGridLength(85),
                ElementStyle = wydStyle
            });

            // 6. BILANS (DO SPRZEDANIA)
            var bilansStyle = new Style(typeof(TextBlock));
            bilansStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            bilansStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(0, 0, 10, 0)));
            bilansStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Bold));

            dgDashboardProdukty.Columns.Add(new DataGridTextColumn
            {
                Header = "Do sprzedania",
                Binding = new Binding("Bilans") { StringFormat = "N0" },
                Width = new DataGridLength(100),
                ElementStyle = bilansStyle
            });

            // 7. PRODUKT
            dgDashboardProdukty.Columns.Add(new DataGridTextColumn
            {
                Header = "Produkt",
                Binding = new Binding("Produkt"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = 200
            });

            dgDashboardProdukty.LoadingRow -= DgDashboardProdukty_LoadingRow;
            dgDashboardProdukty.LoadingRow += DgDashboardProdukty_LoadingRow;
        }

        private void DgDashboardProdukty_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                var bilans = rowView.Row.Field<decimal>("Bilans");
                var produkt = rowView.Row.Field<string>("Produkt") ?? "";

                // Wiersz SUMA CAŁKOWITA
                if (produkt.StartsWith("═══"))
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                    e.Row.Foreground = Brushes.White;
                    e.Row.FontWeight = FontWeights.Bold;
                    e.Row.FontSize = 14;
                    return;
                }

                // Wiersz kategorii (Kurczak A, Kurczak B)
                if (produkt.StartsWith("🐔"))
                {
                    if (produkt.Contains("Kurczak A"))
                        e.Row.Background = new SolidColorBrush(Color.FromRgb(200, 230, 201));
                    else
                        e.Row.Background = new SolidColorBrush(Color.FromRgb(179, 229, 252));
                    e.Row.FontWeight = FontWeights.SemiBold;
                    e.Row.FontSize = 13;
                    return;
                }

                // Kolorowanie według bilansu
                if (bilans > 500)
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(232, 248, 232)); // Zielony - dużo dostępne
                }
                else if (bilans > 0)
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(255, 248, 225)); // Żółty - mało
                }
                else
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(255, 235, 235)); // Czerwony - brak/ujemny
                }
            }
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
                _dtDashboard.Rows.Clear();
                txtStatusTime.Text = "Ładowanie...";

                bool uzywajWydan = rbBilansWydania?.IsChecked == true;
                int? katalogFilter = rbMrozone?.IsChecked == true ? 67153 : (rbSwieze?.IsChecked == true ? 67095 : null);
                DateTime day = _selectedDate.Date;

                // 1. Pobierz nazwy i katalogi produktów z Handel
                var productInfo = new Dictionary<int, (string Nazwa, int? Katalog)>();
                await using (var cn = new SqlConnection(_connHandel))
                {
                    await cn.OpenAsync();
                    const string sql = "SELECT ID, kod, katalog FROM [HANDEL].[HM].[TW]";
                    await using var cmd = new SqlCommand(sql, cn);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        if (!rdr.IsDBNull(0))
                        {
                            int id = rdr.GetInt32(0);
                            string kod = rdr.IsDBNull(1) ? $"ID:{id}" : rdr.GetString(1);
                            int? katalog = null;
                            if (!rdr.IsDBNull(2))
                            {
                                var katVal = rdr.GetValue(2);
                                if (katVal is int ki) katalog = ki;
                                else if (int.TryParse(Convert.ToString(katVal), out int kp)) katalog = kp;
                            }
                            productInfo[id] = (kod, katalog);
                        }
                    }
                }

                // 2. Pobierz konfigurację wydajności
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

                // 3. Pobierz konfigurację produktów (procenty)
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

                // 4. Pobierz harmonogram dostaw (dla obliczenia PLAN)
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

                // 5. Pobierz FAKT - przychody tuszki (sPWU)
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

                // 6. Pobierz FAKT - przychody elementów (sPWP, PWP)
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

                // 7. Pobierz ZAMÓWIENIA dla wybranego dnia
                var orderSum = new Dictionary<int, decimal>();
                var orderIds = new List<int>();
                int zamowienAktywnych = 0;

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
                        zamowienAktywnych++;
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

                // 8. Pobierz WYDANIA (WZ)
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

                // 9. Pobierz STANY MAGAZYNOWE
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

                // 10. Znajdź Kurczak A
                var kurczakA = productInfo.FirstOrDefault(p =>
                    p.Value.Nazwa.Contains("Kurczak A", StringComparison.OrdinalIgnoreCase));

                // 11. Zbierz wszystkie produkty
                var allProductIds = orderSum.Keys
                    .Union(wydaniaSum.Keys)
                    .Union(stanyMag.Keys)
                    .Union(faktTuszka.Keys)
                    .Union(faktElementy.Keys)
                    .Union(konfiguracjaProcenty.Keys)
                    .Where(k => productInfo.ContainsKey(k))
                    .Distinct()
                    .ToList();

                decimal totalPlan = 0m, totalFakt = 0m, totalStan = 0m;
                decimal totalZam = 0m, totalWyd = 0m, totalBilans = 0m;
                decimal totalDoSprzedania = 0m, totalBrakuje = 0m;
                int produktowCount = 0;

                var produktyList = new List<(int Id, string Nazwa, int? Katalog, decimal Plan, decimal Fakt, decimal Stan, decimal Zam, decimal Wyd, decimal Bilans)>();

                foreach (var id in allProductIds)
                {
                    if (!productInfo.TryGetValue(id, out var info)) continue;
                    if (katalogFilter.HasValue && info.Katalog != katalogFilter) continue;

                    decimal plan = 0m, fakt = 0m;

                    // Kurczak A - plan z puli tuszki A
                    if (id == kurczakA.Key)
                    {
                        plan = pulaTuszkiA;
                        fakt = faktTuszka.TryGetValue(id, out var f) ? f : 0m;
                    }
                    else
                    {
                        // Elementy - plan z konfiguracji produktów
                        if (konfiguracjaProcenty.TryGetValue(id, out var procent))
                            plan = pulaTuszkiB * (procent / 100m);
                        fakt = faktElementy.TryGetValue(id, out var f) ? f : 0m;
                    }

                    decimal stan = stanyMag.TryGetValue(id, out var s) ? s : 0m;
                    decimal zam = orderSum.TryGetValue(id, out var z) ? z : 0m;
                    decimal wyd = wydaniaSum.TryGetValue(id, out var w) ? w : 0m;

                    // Bilans = (Fakt lub Plan) + Stan - (Zamówienia lub Wydania)
                    decimal odejmij = uzywajWydan ? wyd : zam;
                    decimal bilans = (fakt > 0 ? fakt : plan) + stan - odejmij;

                    produktyList.Add((id, info.Nazwa, info.Katalog, plan, fakt, stan, zam, wyd, bilans));

                    totalPlan += plan;
                    totalFakt += fakt;
                    totalStan += stan;
                    totalZam += zam;
                    totalWyd += wyd;
                    totalBilans += bilans;
                    if (bilans > 0) totalDoSprzedania += bilans;
                    if (bilans < 0) totalBrakuje += Math.Abs(bilans);
                    produktowCount++;
                }

                // Sortuj według bilansu (malejąco)
                produktyList = produktyList.OrderByDescending(p => p.Bilans).ToList();

                // Dodaj wiersz SUMA CAŁKOWITA na początku
                var sumaRow = _dtDashboard.NewRow();
                sumaRow["Produkt"] = "═══ SUMA CAŁKOWITA ═══";
                sumaRow["Plan"] = totalPlan;
                sumaRow["Fakt"] = totalFakt;
                sumaRow["Stan"] = totalStan;
                sumaRow["Zamowienia"] = totalZam;
                sumaRow["Wydania"] = totalWyd;
                sumaRow["Bilans"] = totalBilans;
                _dtDashboard.Rows.Add(sumaRow);

                // Dodaj produkty
                foreach (var p in produktyList)
                {
                    var row = _dtDashboard.NewRow();
                    row["Plan"] = p.Plan;
                    row["Fakt"] = p.Fakt;
                    row["Stan"] = p.Stan;
                    row["Zamowienia"] = p.Zam;
                    row["Wydania"] = p.Wyd;
                    row["Bilans"] = p.Bilans;
                    row["Produkt"] = p.Nazwa;
                    row["KodTowaru"] = p.Id;
                    row["Katalog"] = p.Katalog?.ToString() ?? "";
                    _dtDashboard.Rows.Add(row);
                }

                // Aktualizuj KPI w nagłówku
                txtBilansOk.Text = $"{totalDoSprzedania:N0} kg";
                txtBilansUwaga.Text = $"{totalZam:N0} kg";
                txtBilansBrak.Text = $"{totalBrakuje:N0} kg";
                txtSumaZamowien.Text = $"Suma zamówień: {totalZam:N0} kg | Stan magazynu: {totalStan:N0} kg";
                txtPlanProdukcji.Text = produktowCount.ToString();
                txtWydajnoscGlowna.Text = zamowienAktywnych.ToString();

                // Aktualizuj karty dostępności - top produkty z dodatnim bilansem
                var topProducts = produktyList
                    .Where(p => p.Bilans > 0)
                    .Take(16)
                    .Select(p =>
                    {
                        double maxBilans = produktyList.Any() ? (double)produktyList.Max(x => x.Bilans) : 1;
                        if (maxBilans <= 0) maxBilans = 1;
                        double procent = (double)p.Bilans / maxBilans * 100;

                        var kolorRamki = p.Bilans > 1000 ? Brushes.Green :
                                        (p.Bilans > 500 ? new SolidColorBrush(Color.FromRgb(46, 204, 113)) :
                                        (p.Bilans > 100 ? Brushes.Orange : Brushes.OrangeRed));
                        var kolorTla = p.Bilans > 500 ? Color.FromRgb(232, 248, 232) :
                                       (p.Bilans > 100 ? Color.FromRgb(255, 248, 225) : Color.FromRgb(255, 243, 224));

                        return new DostepnoscProduktuModel
                        {
                            Nazwa = p.Nazwa,
                            BilansText = $"{p.Bilans:N0} kg",
                            PlanFaktText = p.Fakt > 0 ? $"Fakt: {p.Fakt:N0}" : $"Plan: {p.Plan:N0}",
                            ZamowioneText = $"{p.Zam:N0} kg",
                            StanText = $"{p.Stan:N0} kg",
                            ProcentText = $"{procent:N0}%",
                            SzerokoscPaska = Math.Max(10, Math.Min(170, procent * 1.7)),
                            KolorRamki = kolorRamki,
                            KolorTla = kolorTla,
                            KolorPaska = kolorRamki
                        };
                    })
                    .ToList();

                icDostepnoscProduktow.ItemsSource = topProducts;
                txtStatusTime.Text = $"Aktualizacja: {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania danych: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatusTime.Text = "Błąd ładowania";
            }
            finally
            {
                _isLoading = false;
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
            SetupDataGrid();
            _ = LoadDataAsync();
        }

        private void RbKatalog_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            _ = LoadDataAsync();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            _ = LoadDataAsync();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
