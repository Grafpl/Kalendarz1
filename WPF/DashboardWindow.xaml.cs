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
        private readonly Dictionary<int, string> _productCatalogCache = new();
        private readonly Dictionary<int, string> _mapowanieScalowania = new();
        private readonly Dictionary<string, List<int>> _grupyDoProduktow = new();
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
            _dtDashboard.Columns.Add("Produkt", typeof(string));
            _dtDashboard.Columns.Add("Plan", typeof(decimal));
            _dtDashboard.Columns.Add("Fakt", typeof(decimal));
            _dtDashboard.Columns.Add("Stan", typeof(decimal));
            _dtDashboard.Columns.Add("Zamowienia", typeof(decimal));
            _dtDashboard.Columns.Add("Bilans", typeof(decimal));
            _dtDashboard.Columns.Add("Status", typeof(string));

            dgDashboardProdukty.ItemsSource = _dtDashboard.DefaultView;
            SetupDataGrid();
        }

        private void SetupDataGrid()
        {
            dgDashboardProdukty.Columns.Clear();

            dgDashboardProdukty.Columns.Add(new DataGridTextColumn
            {
                Header = "Produkt",
                Binding = new Binding("Produkt"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = 150
            });

            dgDashboardProdukty.Columns.Add(new DataGridTextColumn
            {
                Header = "Plan",
                Binding = new Binding("Plan") { StringFormat = "N0" },
                Width = new DataGridLength(80),
                ElementStyle = (Style)FindResource("RightAlignedCellStyle")
            });

            dgDashboardProdukty.Columns.Add(new DataGridTextColumn
            {
                Header = "Fakt",
                Binding = new Binding("Fakt") { StringFormat = "N0" },
                Width = new DataGridLength(80),
                ElementStyle = (Style)FindResource("RightAlignedCellStyle")
            });

            dgDashboardProdukty.Columns.Add(new DataGridTextColumn
            {
                Header = "Stan",
                Binding = new Binding("Stan") { StringFormat = "N0" },
                Width = new DataGridLength(80),
                ElementStyle = (Style)FindResource("RightAlignedCellStyle")
            });

            dgDashboardProdukty.Columns.Add(new DataGridTextColumn
            {
                Header = "Zamow.",
                Binding = new Binding("Zamowienia") { StringFormat = "N0" },
                Width = new DataGridLength(80),
                ElementStyle = (Style)FindResource("RightAlignedCellStyle")
            });

            var bilansStyle = new Style(typeof(TextBlock));
            bilansStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            bilansStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Bold));

            dgDashboardProdukty.Columns.Add(new DataGridTextColumn
            {
                Header = "Bilans",
                Binding = new Binding("Bilans") { StringFormat = "N0" },
                Width = new DataGridLength(80),
                ElementStyle = bilansStyle
            });

            dgDashboardProdukty.Columns.Add(new DataGridTextColumn
            {
                Header = "Status",
                Binding = new Binding("Status"),
                Width = new DataGridLength(60),
                ElementStyle = (Style)FindResource("CenterAlignedCellStyle")
            });

            dgDashboardProdukty.LoadingRow += DgDashboardProdukty_LoadingRow;
        }

        private void DgDashboardProdukty_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                var bilans = rowView.Row.Field<decimal>("Bilans");
                var status = rowView.Row.Field<string>("Status") ?? "";

                if (status == "Brak" || bilans < 0)
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(255, 230, 230));
                    e.Row.FontWeight = FontWeights.SemiBold;
                }
                else if (status == "Uwaga" || bilans == 0)
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(255, 250, 205));
                }
                else
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(230, 255, 230));
                }
            }
        }

        private void InitializeDate()
        {
            dpData.SelectedDate = _selectedDate;
            txtSelectedDate.Text = $"Data: {_selectedDate:yyyy-MM-dd}";
            _ = LoadDataAsync();
        }

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                _dtDashboard.Rows.Clear();

                // Zaladuj katalog produktow
                await LoadProductCatalogAsync();

                // Pobierz konfiguracje wydajnosci
                var (wspolczynnikTuszki, procentA, procentB) = await GetKonfiguracjaWydajnosciAsync(_selectedDate);

                // Aktualizuj UI wydajnosci
                txtWydajnoscGlowna.Text = $"{wspolczynnikTuszki:N0}%";
                txtTuszkaA.Text = $"{procentA:N0}%";
                txtTuszkaB.Text = $"{procentB:N0}%";

                // Pobierz konfiguracje produktow
                var konfiguracjaProduktow = await GetKonfiguracjaProduktowAsync(_selectedDate);

                // Pobierz mase deklaracji
                decimal totalMassDek = 0m;
                await using (var cn = new SqlConnection(_connLibra))
                {
                    await cn.OpenAsync();
                    const string sql = @"SELECT WagaDek, SztukiDek FROM dbo.HarmonogramDostaw
                                         WHERE DataOdbioru = @Day AND Bufor IN ('B.Wolny', 'B.Kontr.', 'Potwierdzony')";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Day", _selectedDate.Date);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        var weight = rdr.IsDBNull(0) ? 0m : Convert.ToDecimal(rdr.GetValue(0));
                        var quantity = rdr.IsDBNull(1) ? 0m : Convert.ToDecimal(rdr.GetValue(1));
                        totalMassDek += (weight * quantity);
                    }
                }

                // Oblicz pule tuszki
                decimal pulaTuszki = totalMassDek * (wspolczynnikTuszki / 100m);
                decimal pulaTuszkiA = pulaTuszki * (procentA / 100m);
                decimal pulaTuszkiB = pulaTuszki * (procentB / 100m);

                // Pobierz faktyczne przychody (produkcja)
                var actualIncome = new Dictionary<int, decimal>();
                await using (var cn = new SqlConnection(_connHandel))
                {
                    await cn.OpenAsync();
                    const string sql = @"SELECT MZ.idtw, SUM(ABS(MZ.ilosc))
                                         FROM [HANDEL].[HM].[MZ] MZ
                                         JOIN [HANDEL].[HM].[MG] ON MZ.super = MG.id
                                         WHERE MG.seria IN ('sPWU', 'sPWP', 'PWP') AND MG.aktywny=1 AND MG.data = @Day
                                         GROUP BY MZ.idtw";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Day", _selectedDate.Date);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        int productId = rdr.GetInt32(0);
                        decimal qty = rdr.IsDBNull(1) ? 0m : Convert.ToDecimal(rdr.GetValue(1));
                        actualIncome[productId] = qty;
                    }
                }

                // Pobierz zamowienia z dnia
                var orderIds = new List<int>();
                await using (var cn = new SqlConnection(_connLibra))
                {
                    await cn.OpenAsync();
                    const string sql = @"SELECT Id FROM dbo.ZamowieniaMieso
                                         WHERE DataPrzyjazdu = @Day AND Status <> 'Anulowane'";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Day", _selectedDate.Date);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        orderIds.Add(rdr.GetInt32(0));
                    }
                }

                var orderSum = new Dictionary<int, decimal>();
                if (orderIds.Any())
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();
                    var sql = $"SELECT KodTowaru, SUM(Ilosc) FROM [dbo].[ZamowieniaMiesoTowar] " +
                             $"WHERE ZamowienieId IN ({string.Join(",", orderIds)}) GROUP BY KodTowaru";
                    using var cmd = new SqlCommand(sql, cn);
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                        orderSum[reader.GetInt32(0)] = reader.IsDBNull(1) ? 0m : reader.GetDecimal(1);
                }

                // Pobierz stany magazynowe
                var stanyMagazynowe = new Dictionary<int, decimal>();
                try
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();
                    const string sqlStany = @"SELECT ProduktId, Stan FROM dbo.StanyMagazynowe WHERE Data = @Data";
                    await using var cmd = new SqlCommand(sqlStany, cn);
                    cmd.Parameters.AddWithValue("@Data", _selectedDate.Date);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        int produktId = reader.GetInt32(0);
                        decimal stan = reader.GetDecimal(1);
                        stanyMagazynowe[produktId] = stan;
                    }
                }
                catch { }

                // Agregacja produktow
                var agregowane = new Dictionary<string, (decimal plan, decimal fakt, decimal stan, decimal zam)>(StringComparer.OrdinalIgnoreCase);
                var towaryWGrupach = new HashSet<int>();

                // Najpierw zbierz towary w grupach scalania
                foreach (var product in _productCatalogCache)
                {
                    int productId = product.Key;
                    string productName = product.Value;

                    if (_mapowanieScalowania.TryGetValue(productId, out var nazwaGrupy))
                    {
                        towaryWGrupach.Add(productId);

                        decimal plan = 0m;
                        if (konfiguracjaProduktow.TryGetValue(productId, out decimal procentUdzialu))
                            plan = pulaTuszkiB * (procentUdzialu / 100m);
                        else if (productName.Contains("Kurczak A", StringComparison.OrdinalIgnoreCase) ||
                                 productName.Contains("Tuszka A", StringComparison.OrdinalIgnoreCase))
                            plan = pulaTuszkiA;
                        else if (productName.Contains("Kurczak B", StringComparison.OrdinalIgnoreCase) ||
                                 productName.Contains("Tuszka B", StringComparison.OrdinalIgnoreCase))
                            plan = pulaTuszkiB;

                        decimal fakt = actualIncome.TryGetValue(productId, out var f) ? f : 0m;
                        decimal stan = stanyMagazynowe.TryGetValue(productId, out var s) ? s : 0m;
                        decimal zam = orderSum.TryGetValue(productId, out var z) ? z : 0m;

                        if (agregowane.ContainsKey(nazwaGrupy))
                        {
                            var existing = agregowane[nazwaGrupy];
                            agregowane[nazwaGrupy] = (existing.plan + plan, existing.fakt + fakt, existing.stan + stan, existing.zam + zam);
                        }
                        else
                        {
                            agregowane[nazwaGrupy] = (plan, fakt, stan, zam);
                        }
                    }
                }

                // Dodaj towary niescalone
                foreach (var product in _productCatalogCache)
                {
                    int productId = product.Key;
                    if (towaryWGrupach.Contains(productId)) continue;

                    string productName = product.Value;

                    decimal plan = 0m;
                    if (konfiguracjaProduktow.TryGetValue(productId, out decimal procentUdzialu))
                        plan = pulaTuszkiB * (procentUdzialu / 100m);
                    else if (productName.Contains("Kurczak A", StringComparison.OrdinalIgnoreCase) ||
                             productName.Contains("Tuszka A", StringComparison.OrdinalIgnoreCase))
                        plan = pulaTuszkiA;
                    else if (productName.Contains("Kurczak B", StringComparison.OrdinalIgnoreCase) ||
                             productName.Contains("Tuszka B", StringComparison.OrdinalIgnoreCase))
                        plan = pulaTuszkiB;

                    decimal fakt = actualIncome.TryGetValue(productId, out var f) ? f : 0m;
                    decimal stan = stanyMagazynowe.TryGetValue(productId, out var s) ? s : 0m;
                    decimal zam = orderSum.TryGetValue(productId, out var z) ? z : 0m;

                    agregowane[productName] = (plan, fakt, stan, zam);
                }

                // Dodaj do tabeli i zbierz dane dla kart
                decimal totalZamowienia = 0m;
                decimal totalPlan = 0m;
                int okCount = 0, uwagaCount = 0, brakCount = 0;
                var produktyDostepnosc = new List<DostepnoscProduktuModel>();

                foreach (var kv in agregowane.OrderBy(x => x.Key))
                {
                    var (plan, fakt, stan, zamowienia) = kv.Value;

                    if (zamowienia == 0 && fakt == 0 && stan == 0 && plan == 0) continue;

                    decimal bilans = (fakt > 0 ? fakt : plan) + stan - zamowienia;
                    string status;
                    if (bilans > 0) { status = "OK"; okCount++; }
                    else if (bilans == 0) { status = "Uwaga"; uwagaCount++; }
                    else { status = "Brak"; brakCount++; }

                    _dtDashboard.Rows.Add(kv.Key, plan, fakt, stan, zamowienia, bilans, status);

                    totalZamowienia += zamowienia;
                    totalPlan += plan;

                    // Dodaj do kart dostepnosci
                    decimal procent = (plan > 0) ? Math.Min(100, (fakt > 0 ? fakt : plan + stan) / plan * 100) : 0;
                    var kolorRamki = bilans > 0 ? Brushes.Green : (bilans == 0 ? Brushes.Orange : Brushes.Red);
                    var kolorTla = bilans > 0 ? Color.FromRgb(232, 248, 232) :
                                   (bilans == 0 ? Color.FromRgb(255, 248, 225) : Color.FromRgb(255, 235, 235));
                    var kolorPaska = bilans > 0 ? Brushes.Green : (bilans == 0 ? Brushes.Orange : Brushes.Red);

                    produktyDostepnosc.Add(new DostepnoscProduktuModel
                    {
                        Nazwa = kv.Key,
                        Bilans = bilans,
                        BilansText = $"{bilans:N0} kg",
                        PlanFaktText = $"{(fakt > 0 ? fakt : plan):N0}",
                        ZamowioneText = $"{zamowienia:N0}",
                        ProcentText = $"{procent:N0}%",
                        SzerokoscPaska = Math.Max(0, Math.Min(150, (double)procent * 1.5)),
                        KolorRamki = kolorRamki,
                        KolorTla = kolorTla,
                        KolorPaska = kolorPaska
                    });
                }

                // Aktualizuj UI
                txtBilansOk.Text = okCount.ToString();
                txtBilansUwaga.Text = uwagaCount.ToString();
                txtBilansBrak.Text = brakCount.ToString();
                txtSumaZamowien.Text = $"{totalZamowienia:N0} kg";
                txtPlanProdukcji.Text = $"{totalPlan:N0} kg";

                // Ustaw karty dostepnosci (posortowane wg bilansu rosnaco - najgorsze na gorze)
                icDostepnoscProduktow.ItemsSource = produktyDostepnosc.OrderBy(p => p.Bilans).ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad podczas ladowania danych: {ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
            }
        }

        private async System.Threading.Tasks.Task LoadProductCatalogAsync()
        {
            _productCatalogCache.Clear();

            await using var cn = new SqlConnection(_connHandel);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand("SELECT ID, kod, katalog FROM [HANDEL].[HM].[TW]", cn);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                if (reader.IsDBNull(0)) continue;
                int idtw = reader.GetInt32(0);
                string kod = reader.IsDBNull(1) ? "" : reader.GetString(1);
                string katalog = reader.IsDBNull(2) ? "" : reader.GetString(2);

                // Filtruj tylko produkty swieze (katalog SWIEZE lub mrozone)
                if (katalog.Equals("SWIEZE", StringComparison.OrdinalIgnoreCase) ||
                    katalog.Equals("MROZONE", StringComparison.OrdinalIgnoreCase))
                {
                    _productCatalogCache[idtw] = kod;
                }
            }
        }

        private async System.Threading.Tasks.Task<(decimal wspolczynnikTuszki, decimal procentA, decimal procentB)> GetKonfiguracjaWydajnosciAsync(DateTime data)
        {
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                const string query = @"
                    SELECT TOP 1 WspolczynnikTuszki, ProcentTuszkaA, ProcentTuszkaB
                    FROM KonfiguracjaWydajnosci
                    WHERE DataOd <= @Data AND Aktywny = 1
                    ORDER BY DataOd DESC";

                await using var cmd = new SqlCommand(query, cn);
                cmd.Parameters.AddWithValue("@Data", data.Date);

                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return (
                        Convert.ToDecimal(reader["WspolczynnikTuszki"]),
                        Convert.ToDecimal(reader["ProcentTuszkaA"]),
                        Convert.ToDecimal(reader["ProcentTuszkaB"])
                    );
                }

                return (78.0m, 85.0m, 15.0m);
            }
            catch
            {
                return (78.0m, 85.0m, 15.0m);
            }
        }

        private async System.Threading.Tasks.Task<Dictionary<int, decimal>> GetKonfiguracjaProduktowAsync(DateTime data)
        {
            var result = new Dictionary<int, decimal>();
            _mapowanieScalowania.Clear();
            _grupyDoProduktow.Clear();

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                // Sprawdz czy kolumna GrupaScalowania istnieje
                bool hasGrupaColumn = false;
                const string checkQuery = @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                                            WHERE TABLE_NAME = 'KonfiguracjaProduktow' AND COLUMN_NAME = 'GrupaScalowania'";
                await using (var checkCmd = new SqlCommand(checkQuery, cn))
                {
                    hasGrupaColumn = (int)await checkCmd.ExecuteScalarAsync() > 0;
                }

                string query = hasGrupaColumn
                    ? @"SELECT kp.TowarID, kp.ProcentUdzialu, kp.GrupaScalowania
                        FROM KonfiguracjaProduktow kp
                        INNER JOIN (
                            SELECT MAX(DataOd) as MaxData
                            FROM KonfiguracjaProduktow
                            WHERE DataOd <= @Data AND Aktywny = 1
                        ) sub ON kp.DataOd = sub.MaxData
                        WHERE kp.Aktywny = 1"
                    : @"SELECT kp.TowarID, kp.ProcentUdzialu
                        FROM KonfiguracjaProduktow kp
                        INNER JOIN (
                            SELECT MAX(DataOd) as MaxData
                            FROM KonfiguracjaProduktow
                            WHERE DataOd <= @Data AND Aktywny = 1
                        ) sub ON kp.DataOd = sub.MaxData
                        WHERE kp.Aktywny = 1";

                await using var cmd = new SqlCommand(query, cn);
                cmd.Parameters.AddWithValue("@Data", data.Date);

                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    int towarId = Convert.ToInt32(reader["TowarID"]);
                    decimal procent = Convert.ToDecimal(reader["ProcentUdzialu"]);
                    result[towarId] = procent;

                    if (hasGrupaColumn)
                    {
                        var grupaOrdinal = reader.GetOrdinal("GrupaScalowania");
                        if (!reader.IsDBNull(grupaOrdinal))
                        {
                            string grupa = reader.GetString(grupaOrdinal);
                            if (!string.IsNullOrWhiteSpace(grupa))
                            {
                                _mapowanieScalowania[towarId] = grupa;

                                if (!_grupyDoProduktow.ContainsKey(grupa))
                                    _grupyDoProduktow[grupa] = new List<int>();
                                _grupyDoProduktow[grupa].Add(towarId);
                            }
                        }
                    }
                }

                // Jesli nie ma grup, pobierz ze ScalowanieTowarow
                if (!_mapowanieScalowania.Any())
                {
                    await LoadScalowanieTowarowAsync(cn);
                }

                return result;
            }
            catch
            {
                return result;
            }
        }

        private async System.Threading.Tasks.Task LoadScalowanieTowarowAsync(SqlConnection cn)
        {
            try
            {
                const string checkSql = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ScalowanieTowarow'";
                await using var checkCmd = new SqlCommand(checkSql, cn);
                if ((int)await checkCmd.ExecuteScalarAsync()! == 0) return;

                const string sql = "SELECT NazwaGrupy, TowarIdtw FROM [dbo].[ScalowanieTowarow]";
                await using var cmd = new SqlCommand(sql, cn);
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    string nazwaGrupy = reader.GetString(0);
                    int towarIdtw = reader.GetInt32(1);

                    _mapowanieScalowania[towarIdtw] = nazwaGrupy;

                    if (!_grupyDoProduktow.ContainsKey(nazwaGrupy))
                        _grupyDoProduktow[nazwaGrupy] = new List<int>();
                    _grupyDoProduktow[nazwaGrupy].Add(towarIdtw);
                }
            }
            catch { }
        }

        private void DpData_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            if (dpData.SelectedDate.HasValue)
            {
                _selectedDate = dpData.SelectedDate.Value;
                txtSelectedDate.Text = $"Data: {_selectedDate:yyyy-MM-dd}";
                _ = LoadDataAsync();
            }
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

    // DostepnoscProduktuModel jest zdefiniowany w MainWindow.xaml.cs
}
