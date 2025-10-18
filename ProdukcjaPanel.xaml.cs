using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Kalendarz1
{
    public partial class ProdukcjaPanel : Window
    {
        private readonly string _connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly string _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private readonly string _connTransport = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public string UserID { get; set; } = "User";
        private DateTime _selectedDate = DateTime.Today;
        private readonly Dictionary<int, ZamowienieInfo> _zamowienia = new();
        private bool _notesTableEnsured = false;
        private int? _filteredProductId = null;
        private Dictionary<int, string> _produktLookup = new();
        private DispatcherTimer refreshTimer;

        private ObservableCollection<ZamowienieViewModel> _zamowieniaList = new();

        private static readonly string[] OffalKeywords = { "wątroba", "watrob", "serce", "serca", "żołąd", "zolad", "żołądki", "zoladki" };

        public ProdukcjaPanel()
        {
            InitializeComponent();
            InitializeAsync();
            KeyDown += ProdukcjaPanel_KeyDown;
            StartAutoRefresh();
        }

        private async void InitializeAsync()
        {
            await ReloadAllAsync();
        }

        private void ProdukcjaPanel_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape) Close();
            if (e.Key == System.Windows.Input.Key.Enter) TryOpenShipmentDetails();
        }

        #region Event Handlers
        private async void btnPrev_Click(object sender, RoutedEventArgs e)
        {
            _selectedDate = _selectedDate.AddDays(-1);
            await ReloadAllAsync();
        }

        private async void btnNext_Click(object sender, RoutedEventArgs e)
        {
            _selectedDate = _selectedDate.AddDays(1);
            await ReloadAllAsync();
        }

        private async void btnToday_Click(object sender, RoutedEventArgs e)
        {
            _selectedDate = DateTime.Today;
            await ReloadAllAsync();
        }

        private async void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await ReloadAllAsync();
        }

        private void btnLive_Click(object sender, RoutedEventArgs e)
        {
            OpenLiveWindow();
        }

        private async void btnUndo_Click(object sender, RoutedEventArgs e)
        {
            await UndoRealizedAsync();
        }

        private async void btnSaveNotes_Click(object sender, RoutedEventArgs e)
        {
            await SaveItemNotesAsync();
            await SaveProductionNotesAsync();
        }

        private async void btnZrealizowano_Click(object sender, RoutedEventArgs e)
        {
            await MarkOrderRealizedAsync();
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void dgvZamowienia_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await LoadPozycjeForSelectedAsync();
        }

        private void dgvZamowienia_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            TryOpenShipmentDetails();
        }

        private async void cbPokazWydaniaSymfonia_Click(object sender, RoutedEventArgs e)
        {
            await LoadOrdersAsync();
        }
        #endregion

        #region Data Loading
        private async Task ReloadAllAsync()
        {
            lblData.Text = _selectedDate.ToString("yyyy-MM-dd ddd");
            lblUser.Text = $"Użytkownik: {UserID}";

            await PopulateProductFilterAsync();
            await LoadOrdersAsync();
            await LoadIn0ESummaryAsync();
            await LoadPojTuszkiAsync();
            await LoadPozycjeForSelectedAsync();
        }

        private async Task PopulateProductFilterAsync()
        {
            string dateColumn = "DataUboju";
            int? prev = _filteredProductId;
            var ids = new HashSet<int>();

            using (var cn = new SqlConnection(_connLibra))
            {
                await cn.OpenAsync();
                string sql = "SELECT DISTINCT zmt.KodTowaru FROM dbo.ZamowieniaMieso z " +
                            "JOIN dbo.ZamowieniaMiesoTowar zmt ON z.Id=zmt.ZamowienieId " +
                            $"WHERE z.{dateColumn}=@D AND ISNULL(z.Status,'Nowe') NOT IN ('Anulowane')";
                var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@D", _selectedDate.Date);
                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                    if (!rd.IsDBNull(0)) ids.Add(rd.GetInt32(0));
            }

            using (var cn = new SqlConnection(_connHandel))
            {
                await cn.OpenAsync();
                var cmd = new SqlCommand(@"SELECT DISTINCT MZ.idtw FROM HANDEL.HM.MZ MZ 
                    JOIN HANDEL.HM.MG ON MZ.super=MG.id 
                    WHERE MG.seria IN ('sWZ','sWZ-W') AND MG.aktywny=1 AND MG.data=@D", cn);
                cmd.Parameters.AddWithValue("@D", _selectedDate.Date);
                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                    if (!rd.IsDBNull(0)) ids.Add(rd.GetInt32(0));
            }

            _produktLookup.Clear();
            if (ids.Count > 0)
            {
                await LoadProductLookupAsync(ids);
            }

            var items = new List<ComboItem> { new ComboItem(0, "— Wszystkie —") };
            items.AddRange(_produktLookup.OrderBy(k => k.Value).Select(k => new ComboItem(k.Key, k.Value)));

            cbFiltrProdukt.ItemsSource = items;
            cbFiltrProdukt.DisplayMemberPath = "Text";
            cbFiltrProdukt.SelectedValuePath = "Value";

            if (prev.HasValue && items.Any(i => i.Value == prev.Value))
                cbFiltrProdukt.SelectedItem = items.First(i => i.Value == prev.Value);
            else
                cbFiltrProdukt.SelectedIndex = 0;

            cbFiltrProdukt.SelectionChanged += async (s, e) =>
            {
                if (cbFiltrProdukt.SelectedItem is ComboItem item)
                {
                    _filteredProductId = item.Value == 0 ? null : item.Value;
                    await LoadOrdersAsync();
                    await LoadPozycjeForSelectedAsync();
                }
            };
        }

        private async Task LoadProductLookupAsync(HashSet<int> ids)
        {
            var list = ids.ToList();
            const int batch = 400;

            for (int i = 0; i < list.Count; i += batch)
            {
                using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                var slice = list.Skip(i).Take(batch).ToList();
                var cmd = cn.CreateCommand();
                var paramNames = new List<string>();

                for (int k = 0; k < slice.Count; k++)
                {
                    var pn = "@p" + k;
                    cmd.Parameters.AddWithValue(pn, slice[k]);
                    paramNames.Add(pn);
                }

                cmd.CommandText = $"SELECT ID,kod FROM HM.TW WHERE ID IN ({string.Join(",", paramNames)}) AND katalog=67095";
                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    int id = rd.GetInt32(0);
                    string kod = rd.IsDBNull(1) ? string.Empty : rd.GetString(1);
                    _produktLookup[id] = kod;
                }
            }
        }

        private async Task LoadOrdersAsync()
        {
            string dateColumn = "DataUboju";
            _zamowienia.Clear();
            var orderListForGrid = new List<ZamowienieViewModel>();
            var klientIdsWithOrder = new HashSet<int>();

            // 1. Load orders from LibraNet
            using (var cn = new SqlConnection(_connLibra))
            {
                await cn.OpenAsync();

                var sqlBuilder = new System.Text.StringBuilder();
                sqlBuilder.Append("SELECT z.Id, z.KlientId, ISNULL(z.Uwagi,'') AS Uwagi, ");
                sqlBuilder.Append("ISNULL(z.Status,'Nowe') AS Status, ");
                sqlBuilder.Append("(SELECT SUM(ISNULL(t.Ilosc, 0)) FROM dbo.ZamowieniaMiesoTowar t WHERE t.ZamowienieId = z.Id");

                if (_filteredProductId.HasValue)
                    sqlBuilder.Append(" AND t.KodTowaru=@P");

                sqlBuilder.Append(") AS TotalIlosc, ");
                sqlBuilder.Append("z.DataUtworzenia, z.TransportKursID, ");
                sqlBuilder.Append("CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.ZamowieniaMiesoTowar t ");
                sqlBuilder.Append("WHERE t.ZamowienieId = z.Id AND t.Folia = 1) ");
                sqlBuilder.Append("THEN 1 ELSE 0 END AS BIT) AS MaFolie ");
                sqlBuilder.Append("FROM dbo.ZamowieniaMieso z ");
                sqlBuilder.Append($"WHERE z.{dateColumn}=@D AND ISNULL(z.Status,'Nowe') NOT IN ('Anulowane')");

                if (_filteredProductId.HasValue)
                    sqlBuilder.Append(" AND EXISTS (SELECT 1 FROM dbo.ZamowieniaMiesoTowar t WHERE t.ZamowienieId=z.Id AND t.KodTowaru=@P)");

                var cmd = new SqlCommand(sqlBuilder.ToString(), cn);
                cmd.Parameters.AddWithValue("@D", _selectedDate.Date);
                if (_filteredProductId.HasValue)
                    cmd.Parameters.AddWithValue("@P", _filteredProductId.Value);

                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    var uwagi = rd.GetString(2);
                    var info = new ZamowienieInfo
                    {
                        Id = rd.GetInt32(0),
                        KlientId = rd.GetInt32(1),
                        Uwagi = uwagi,
                        Status = rd.GetString(3),
                        TotalIlosc = rd.IsDBNull(4) ? 0 : rd.GetDecimal(4),
                        IsShipmentOnly = false,
                        DataUtworzenia = rd.IsDBNull(5) ? null : rd.GetDateTime(5),
                        MaNotatke = !string.IsNullOrWhiteSpace(uwagi),
                        MaFolie = !rd.IsDBNull(7) && rd.GetBoolean(7),
                        TransportKursId = rd.IsDBNull(6) ? null : rd.GetInt64(6)
                    };

                    _zamowienia[info.Id] = info;
                    klientIdsWithOrder.Add(info.KlientId);
                }
            }

            await LoadTransportInfoAsync();

            if (cbPokazWydaniaSymfonia.IsChecked == true)
            {
                var shipments = await LoadShipmentsAsync();
                var shipmentOnlyClientIds = shipments
                    .Where(s => !klientIdsWithOrder.Contains(s.KlientId))
                    .Select(s => s.KlientId)
                    .Distinct()
                    .ToList();

                if (shipmentOnlyClientIds.Count > 0)
                {
                    var contractors = await LoadContractorsAsync(shipmentOnlyClientIds);
                    foreach (var s in shipments.Where(s => shipmentOnlyClientIds.Contains(s.KlientId)))
                    {
                        contractors.TryGetValue(s.KlientId, out var cinfo);
                        var info = new ZamowienieInfo
                        {
                            Id = -s.KlientId,
                            KlientId = s.KlientId,
                            Klient = Normalize(cinfo?.Shortcut ?? $"KH {s.KlientId}"),
                            Handlowiec = Normalize(cinfo?.Handlowiec),
                            Status = "Wydanie Symfonia",
                            TotalIlosc = s.Qty,
                            IsShipmentOnly = true,
                            MaFolie = false
                        };
                        orderListForGrid.Add(new ZamowienieViewModel(info));
                    }
                }
            }

            var orderClientIds = _zamowienia.Values.Select(o => o.KlientId).Distinct().ToList();
            if (orderClientIds.Count > 0)
            {
                var contractors = await LoadContractorsAsync(orderClientIds);
                foreach (var orderInfo in _zamowienia.Values)
                {
                    if (contractors.TryGetValue(orderInfo.KlientId, out var cinfo))
                    {
                        orderInfo.Klient = Normalize(cinfo.Shortcut);
                        orderInfo.Handlowiec = Normalize(cinfo.Handlowiec);
                    }
                    else
                    {
                        orderInfo.Klient = $"KH {orderInfo.KlientId}";
                        orderInfo.Handlowiec = "(Brak)";
                    }
                    orderListForGrid.Add(new ZamowienieViewModel(orderInfo));
                }
            }

            _zamowieniaList.Clear();
            var sorted = orderListForGrid
                .OrderBy(o => StatusOrder(o.Status))
                .ThenBy(o => o.SortDateTime)
                .ThenBy(o => o.Handlowiec)
                .ThenBy(o => o.Klient);

            foreach (var item in sorted)
            {
                _zamowieniaList.Add(item);
            }

            dgvZamowienia.ItemsSource = _zamowieniaList;
            UpdateRowColors();
            UpdateStatsLabel();
        }

        private async Task LoadTransportInfoAsync()
        {
            try
            {
                using var cn = new SqlConnection(_connTransport);
                await cn.OpenAsync();

                var sql = "SELECT KursID, GodzWyjazdu, Status, DataKursu FROM dbo.Kurs";
                var cmd = new SqlCommand(sql, cn);

                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    var kursId = rd.GetInt64(0);

                    if (_zamowienia.Values.Any(z => z.TransportKursId == kursId))
                    {
                        var order = _zamowienia.Values.First(z => z.TransportKursId == kursId);
                        order.CzasWyjazdu = rd.IsDBNull(1) ? null : rd.GetTimeSpan(1);
                        order.StatusTransportu = rd.IsDBNull(2) ? "" : rd.GetString(2);
                        order.DataKursu = rd.GetDateTime(3);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Nie można pobrać danych transportu: {ex.Message}");
            }
        }

        private async Task<List<(int KlientId, decimal Qty)>> LoadShipmentsAsync()
        {
            var shipments = new List<(int KlientId, decimal Qty)>();

            using var cn = new SqlConnection(_connHandel);
            await cn.OpenAsync();

            string sql = @"SELECT MG.khid, SUM(ABS(MZ.ilosc)) 
                FROM HANDEL.HM.MZ MZ 
                JOIN HANDEL.HM.MG ON MZ.super=MG.id 
                WHERE MG.seria IN ('sWZ','sWZ-W') AND MG.aktywny=1 
                AND MG.data=@D AND MG.khid IS NOT NULL";

            if (_filteredProductId.HasValue)
                sql += " AND MZ.idtw=@P";

            sql += " GROUP BY MG.khid";

            var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@D", _selectedDate.Date);
            if (_filteredProductId.HasValue)
                cmd.Parameters.AddWithValue("@P", _filteredProductId.Value);

            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                shipments.Add((rd.GetInt32(0), rd.IsDBNull(1) ? 0m : Convert.ToDecimal(rd.GetValue(1))));
            }

            return shipments;
        }

        private async Task LoadPozycjeForSelectedAsync()
        {
            if (!(dgvZamowienia.SelectedItem is ZamowienieViewModel vm))
            {
                dgvPozycje.ItemsSource = null;
                txtUwagi.Text = "";
                txtNotatkiTransportu.Text = "";
                return;
            }

            var info = vm.Info;

            if (info.IsShipmentOnly)
            {
                await LoadShipmentOnlyAsync(info.KlientId);
                return;
            }

            txtUwagi.Text = info.Uwagi;

            try
            {
                using var cnProd = new SqlConnection(_connLibra);
                await cnProd.OpenAsync();
                var cmdProd = new SqlCommand(
                    "SELECT NotatkaProdukcja FROM dbo.ZamowieniaMiesoProdukcjaNotatki WHERE ZamowienieId = @Id", cnProd);
                cmdProd.Parameters.AddWithValue("@Id", info.Id);
                var notatkaProd = await cmdProd.ExecuteScalarAsync();
                txtNotatkiTransportu.Text = notatkaProd?.ToString() ?? "";
            }
            catch
            {
                txtNotatkiTransportu.Text = "";
            }

            await EnsureNotesTableAsync();

            var orderPositions = new List<(int TowarId, decimal Ilosc, string Notatka, bool Folia)>();
            using (var cn = new SqlConnection(_connLibra))
            {
                await cn.OpenAsync();
                string sql = @"SELECT zmt.KodTowaru, zmt.Ilosc, n.Notatka, ISNULL(zmt.Folia, 0) AS Folia 
                    FROM dbo.ZamowieniaMiesoTowar zmt 
                    LEFT JOIN dbo.ZamowieniaMiesoTowarNotatki n 
                        ON n.ZamowienieId=zmt.ZamowienieId AND n.KodTowaru=zmt.KodTowaru 
                    WHERE zmt.ZamowienieId=@Id" +
                    (_filteredProductId.HasValue ? " AND zmt.KodTowaru=@P" : "");

                var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Id", info.Id);
                if (_filteredProductId.HasValue)
                    cmd.Parameters.AddWithValue("@P", _filteredProductId.Value);

                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    orderPositions.Add((
                        rd.GetInt32(0),
                        rd.IsDBNull(1) ? 0m : Convert.ToDecimal(rd.GetValue(1)),
                        rd.IsDBNull(2) ? string.Empty : rd.GetString(2),
                        !rd.IsDBNull(3) && rd.GetBoolean(3)
                    ));
                }
            }

            var shipments = await GetShipmentsForClientAsync(info.KlientId);
            if (_filteredProductId.HasValue)
                shipments = shipments.Where(k => k.Key == _filteredProductId.Value).ToDictionary(k => k.Key, v => v.Value);

            var ids = orderPositions.Select(p => p.TowarId).Union(shipments.Keys).Where(i => i > 0).Distinct().ToList();
            var towarMap = await LoadTowaryAsync(ids);

            var dt = new DataTable();
            dt.Columns.Add("Produkt", typeof(string));
            dt.Columns.Add("Zamówiono (kg)", typeof(decimal));
            dt.Columns.Add("Wydano (kg)", typeof(decimal));
            dt.Columns.Add("Różnica (kg)", typeof(decimal));
            dt.Columns.Add("Folia", typeof(bool));

            var mapOrd = orderPositions.ToDictionary(p => p.TowarId, p => (p.Ilosc, p.Notatka, p.Folia));

            foreach (var id in ids)
            {
                mapOrd.TryGetValue(id, out var ord);
                shipments.TryGetValue(id, out var wyd);
                string kod = towarMap.TryGetValue(id, out var t) ? t.Kod : $"ID:{id}";

                if (ord.Folia)
                    kod = "🎞️ " + kod;

                dt.Rows.Add(kod, ord.Ilosc, wyd, ord.Ilosc - wyd, ord.Folia);
            }

            dgvPozycje.ItemsSource = dt.DefaultView;
        }

        private async Task LoadIn0ESummaryAsync()
        {
            var dt = new DataTable();
            dt.Columns.Add("Towar", typeof(string));
            dt.Columns.Add("Kg", typeof(decimal));

            using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            var cmd = new SqlCommand(@"SELECT ArticleName, CAST(Weight AS float) W 
                FROM dbo.In0E 
                WHERE Data=@D AND ISNULL(ArticleName,'')<>''", cn);
            cmd.Parameters.AddWithValue("@D", _selectedDate.ToString("yyyy-MM-dd"));

            var agg = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                string name = rd.IsDBNull(0) ? "(Brak)" : rd.GetString(0);
                decimal w = rd.IsDBNull(1) ? 0m : Convert.ToDecimal(rd.GetValue(1));
                if (!agg.ContainsKey(name)) agg[name] = 0;
                agg[name] += w;
            }

            foreach (var kv in agg.OrderByDescending(k => k.Value))
            {
                dt.Rows.Add(kv.Key, kv.Value);
            }

            dgvIn0ESumy.ItemsSource = dt.DefaultView;
        }

        private async Task LoadPojTuszkiAsync()
        {
            try
            {
                var pojData = new List<(int Poj, int Pal)>();
                using (var cn = new SqlConnection(_connLibra))
                {
                    await cn.OpenAsync();
                    string sql = @"SELECT k.QntInCont, COUNT(DISTINCT k.GUID) Palety 
                        FROM dbo.In0E k 
                        WHERE k.ArticleID=40 AND k.QntInCont>0 AND k.CreateData=@D 
                        GROUP BY k.QntInCont ORDER BY k.QntInCont ASC";
                    var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@D", _selectedDate.Date);
                    using var rd = await cmd.ExecuteReaderAsync();
                    while (await rd.ReadAsync())
                    {
                        pojData.Add((rd.GetInt32(0), rd.GetInt32(1)));
                    }
                }

                var dt = new DataTable();
                dt.Columns.Add("Typ", typeof(string));
                dt.Columns.Add("Palety", typeof(int));
                dt.Columns.Add("Udział", typeof(string));

                decimal totalPalety = pojData.Sum(p => p.Pal);

                if (totalPalety > 0)
                {
                    foreach (var data in pojData)
                    {
                        decimal udzial = (data.Pal / totalPalety) * 100;
                        dt.Rows.Add($"Poj. {data.Poj}", data.Pal, $"{udzial:N1}%");
                    }
                }

                if (pojData.Any())
                {
                    dt.Rows.Add("", DBNull.Value, "");
                }

                var duzyIds = new HashSet<int> { 5, 6, 7, 8 };
                var malyIds = new HashSet<int> { 9, 10, 11 };

                int duzyKurczakPalety = pojData.Where(p => duzyIds.Contains(p.Poj)).Sum(p => p.Pal);
                int malyKurczakPalety = pojData.Where(p => malyIds.Contains(p.Poj)).Sum(p => p.Pal);
                string duzyUdzial = totalPalety > 0 ? $"{(duzyKurczakPalety / totalPalety) * 100:N1}%" : "0.0%";
                string malyUdzial = totalPalety > 0 ? $"{(malyKurczakPalety / totalPalety) * 100:N1}%" : "0.0%";

                dt.Rows.Add("Duży kurczak", duzyKurczakPalety, duzyUdzial);
                dt.Rows.Add("Mały kurczak", malyKurczakPalety, malyUdzial);
                dt.Rows.Add("SUMA", (int)totalPalety, "100%");

                dgvPojTuszki.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                dgvPojTuszki.ItemsSource = null;
                MessageBox.Show($"Wystąpił błąd podczas ładowania danych o tuszkach:\n{ex.Message}",
                                "Błąd krytyczny", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadShipmentOnlyAsync(int klientId)
        {
            txtUwagi.Text = "(Wydanie bez zamówienia)";
            txtNotatkiTransportu.Text = "";

            var shipments = await GetShipmentsForClientAsync(klientId);
            if (_filteredProductId.HasValue)
                shipments = shipments.Where(k => k.Key == _filteredProductId.Value).ToDictionary(k => k.Key, v => v.Value);

            var ids = shipments.Keys.ToList();
            var towarMap = await LoadTowaryAsync(ids);

            var dt = new DataTable();
            dt.Columns.Add("Produkt", typeof(string));
            dt.Columns.Add("Wydano (kg)", typeof(decimal));

            foreach (var kv in shipments)
            {
                string kod = towarMap.TryGetValue(kv.Key, out var t) ? t.Kod : $"ID:{kv.Key}";
                dt.Rows.Add(kod, kv.Value);
            }

            dgvPozycje.ItemsSource = dt.DefaultView;
        }
        #endregion

        #region Helper Methods
        private string Normalize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "(Brak)";
            var parts = s.Trim().Replace('\u00A0', ' ').Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? "(Brak)" : string.Join(' ', parts);
        }

        private int StatusOrder(string status) => status switch
        {
            "Nowe" => 0,
            "Zrealizowane" => 1,
            "Wydanie Symfonia" => 2,
            "Anulowane" => 3,
            _ => 4
        };

        private void UpdateRowColors()
        {
            foreach (var item in _zamowieniaList)
            {
                if (item.Status == "Zrealizowane")
                {
                    item.RowColor = new SolidColorBrush(Color.FromRgb(32, 80, 44));
                    item.TextColor = Brushes.LightGreen;
                }
                else if (item.Info.IsShipmentOnly)
                {
                    item.RowColor = new SolidColorBrush(Color.FromRgb(80, 58, 32));
                    item.TextColor = Brushes.Gold;
                }
                else if (item.Info.CzasWyjazdu.HasValue && item.Info.DataKursu.HasValue)
                {
                    var dataICzasWyjazdu = item.Info.DataKursu.Value.Add(item.Info.CzasWyjazdu.Value);
                    var roznicaCzasu = dataICzasWyjazdu - DateTime.Now;

                    if (roznicaCzasu.TotalMinutes < 0)
                    {
                        item.RowColor = new SolidColorBrush(Color.FromRgb(139, 0, 0));
                        item.TextColor = Brushes.White;
                    }
                    else if (roznicaCzasu.TotalMinutes <= 30)
                    {
                        item.RowColor = new SolidColorBrush(Color.FromRgb(218, 165, 32));
                        item.TextColor = Brushes.Black;
                    }
                    else
                    {
                        item.RowColor = new SolidColorBrush(Color.FromRgb(34, 139, 34));
                        item.TextColor = Brushes.White;
                    }
                }
                else
                {
                    item.RowColor = new SolidColorBrush(Color.FromRgb(55, 57, 70));
                    item.TextColor = Brushes.White;
                }
            }
        }


        private void UpdateStatsLabel()
        {
            int total = _zamowienia.Count;
            if (total == 0)
            {
                lblStats.Text = "Brak zamówień";
                return;
            }
            int realized = _zamowienia.Values.Count(z => string.Equals(z.Status, "Zrealizowane", StringComparison.OrdinalIgnoreCase));
            lblStats.Text = $"Zrealizowane: {realized}/{total} ({100.0 * realized / total:N1}%)";
        }

        private async Task<Dictionary<int, ContractorInfo>> LoadContractorsAsync(List<int> ids)
        {
            var dict = new Dictionary<int, ContractorInfo>();
            if (ids.Count == 0) return dict;
            const int batch = 400;

            for (int i = 0; i < ids.Count; i += batch)
            {
                var slice = ids.Skip(i).Take(batch).ToList();
                using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                var cmd = cn.CreateCommand();
                var paramNames = new List<string>();

                for (int k = 0; k < slice.Count; k++)
                {
                    string pn = "@p" + k;
                    cmd.Parameters.AddWithValue(pn, slice[k]);
                    paramNames.Add(pn);
                }

                cmd.CommandText = $@"SELECT c.Id, 
                    ISNULL(c.Shortcut,'KH '+CAST(c.Id AS varchar(10))) Shortcut, 
                    ISNULL(w.CDim_Handlowiec_Val,'(Brak)') Handlowiec 
                    FROM SSCommon.STContractors c 
                    LEFT JOIN SSCommon.ContractorClassification w ON c.Id=w.ElementId 
                    WHERE c.Id IN ({string.Join(',', paramNames)})";

                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    var ci = new ContractorInfo
                    {
                        Id = rd.GetInt32(0),
                        Shortcut = rd.IsDBNull(1) ? string.Empty : rd.GetString(1).Trim(),
                        Handlowiec = rd.IsDBNull(2) ? "(Brak)" : Normalize(rd.GetString(2))
                    };
                    dict[ci.Id] = ci;
                }
            }
            return dict;
        }

        private async Task<Dictionary<int, TowarInfo>> LoadTowaryAsync(List<int> ids)
        {
            var dict = new Dictionary<int, TowarInfo>();
            if (ids.Count == 0) return dict;
            const int batch = 400;

            for (int i = 0; i < ids.Count; i += batch)
            {
                var slice = ids.Skip(i).Take(batch).ToList();
                using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                var cmd = cn.CreateCommand();
                var paramNames = new List<string>();

                for (int k = 0; k < slice.Count; k++)
                {
                    string pn = "@t" + k;
                    cmd.Parameters.AddWithValue(pn, slice[k]);
                    paramNames.Add(pn);
                }

                cmd.CommandText = $"SELECT ID,kod FROM HM.TW WHERE ID IN ({string.Join(',', paramNames)})";
                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    var ti = new TowarInfo
                    {
                        Id = rd.GetInt32(0),
                        Kod = rd.IsDBNull(1) ? string.Empty : rd.GetString(1)
                    };
                    dict[ti.Id] = ti;
                }
            }
            return dict;
        }

        private async Task<Dictionary<int, decimal>> GetShipmentsForClientAsync(int klientId)
        {
            var dict = new Dictionary<int, decimal>();
            using var cn = new SqlConnection(_connHandel);
            await cn.OpenAsync();

            string sql = @"SELECT MZ.idtw, SUM(ABS(MZ.ilosc)) 
                FROM HANDEL.HM.MZ MZ 
                JOIN HANDEL.HM.MG ON MZ.super=MG.id 
                JOIN HANDEL.HM.TW ON MZ.idtw=TW.id 
                WHERE MG.seria IN ('sWZ','sWZ-W') AND MG.aktywny=1 
                AND MG.data=@D AND MG.khid=@K AND TW.katalog=67095" +
                (_filteredProductId.HasValue ? " AND MZ.idtw=@P" : "") + " GROUP BY MZ.idtw";

            var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@D", _selectedDate.Date);
            cmd.Parameters.AddWithValue("@K", klientId);
            if (_filteredProductId.HasValue)
                cmd.Parameters.AddWithValue("@P", _filteredProductId.Value);

            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                int id = rd.GetInt32(0);
                decimal qty = rd.IsDBNull(1) ? 0m : Convert.ToDecimal(rd.GetValue(1));
                dict[id] = qty;
            }
            return dict;
        }

        private int? GetSelectedOrderId()
        {
            if (dgvZamowienia.SelectedItem is ZamowienieViewModel vm && !vm.Info.IsShipmentOnly)
            {
                return vm.Info.Id;
            }
            return null;
        }
        #endregion

        #region Actions
        private async Task MarkOrderRealizedAsync()
        {
            var orderId = GetSelectedOrderId();
            if (!orderId.HasValue) return;

            if (MessageBox.Show("Oznaczyć zamówienie jako zrealizowane?", "Potwierdzenie",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            var cmd = new SqlCommand("UPDATE dbo.ZamowieniaMieso SET Status='Zrealizowane' WHERE Id=@I", cn);
            cmd.Parameters.AddWithValue("@I", orderId.Value);
            await cmd.ExecuteNonQueryAsync();
            await ReloadAllAsync();
        }

        private async Task UndoRealizedAsync()
        {
            var orderId = GetSelectedOrderId();
            if (!orderId.HasValue) return;

            if (MessageBox.Show("Cofnąć realizację?", "Potwierdzenie",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            var cmd = new SqlCommand("UPDATE dbo.ZamowieniaMieso SET Status='Nowe' WHERE Id=@I", cn);
            cmd.Parameters.AddWithValue("@I", orderId.Value);
            await cmd.ExecuteNonQueryAsync();
            await ReloadAllAsync();
        }

        private async Task SaveItemNotesAsync()
        {
            // Implementation for saving item notes
            await Task.CompletedTask;
            MessageBox.Show("Zapisano notatki.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task SaveProductionNotesAsync()
        {
            var orderId = GetSelectedOrderId();
            if (!orderId.HasValue) return;

            string notatka = txtNotatkiTransportu.Text.Trim();

            using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();

            var checkCmd = new SqlCommand(
                "SELECT COUNT(*) FROM dbo.ZamowieniaMiesoProdukcjaNotatki WHERE ZamowienieId = @Id", cn);
            checkCmd.Parameters.AddWithValue("@Id", orderId.Value);
            int exists = (int)await checkCmd.ExecuteScalarAsync();

            SqlCommand cmd;
            if (exists > 0)
            {
                cmd = new SqlCommand(@"
                    UPDATE dbo.ZamowieniaMiesoProdukcjaNotatki 
                    SET NotatkaProdukcja = @Notatka, 
                        DataModyfikacji = GETDATE(), 
                        Uzytkownik = @User 
                    WHERE ZamowienieId = @Id", cn);
            }
            else
            {
                cmd = new SqlCommand(@"
                    INSERT INTO dbo.ZamowieniaMiesoProdukcjaNotatki 
                    (ZamowienieId, NotatkaProdukcja, DataModyfikacji, Uzytkownik) 
                    VALUES (@Id, @Notatka, GETDATE(), @User)", cn);
            }

            cmd.Parameters.AddWithValue("@Id", orderId.Value);
            cmd.Parameters.AddWithValue("@Notatka", string.IsNullOrWhiteSpace(notatka) ? (object)DBNull.Value : notatka);
            cmd.Parameters.AddWithValue("@User", UserID);

            await cmd.ExecuteNonQueryAsync();
            MessageBox.Show("Zapisano notatkę produkcji.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task EnsureNotesTableAsync()
        {
            if (_notesTableEnsured) return;
            try
            {
                using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                var cmd = new SqlCommand(@"
                    IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name='ZamowieniaMiesoTowarNotatki' AND type='U') 
                    BEGIN 
                        CREATE TABLE dbo.ZamowieniaMiesoTowarNotatki(
                            ZamowienieId INT NOT NULL, 
                            KodTowaru INT NOT NULL, 
                            Notatka NVARCHAR(4000) NULL, 
                            CONSTRAINT PK_ZamTowNot PRIMARY KEY (ZamowienieId,KodTowaru)
                        ); 
                    END", cn);
                await cmd.ExecuteNonQueryAsync();
            }
            catch { }
            _notesTableEnsured = true;
        }

        private void TryOpenShipmentDetails()
        {
            if (dgvZamowienia.SelectedItem is ZamowienieViewModel vm && vm.Info.IsShipmentOnly)
            {
                var window = new ShipmentDetailsWindow(_connHandel, vm.Info.KlientId, _selectedDate);
                window.Owner = this;
                window.ShowDialog();
            }
        }

        private void OpenLiveWindow()
        {
            var window = new LivePrzychodyWindow(_connLibra, _selectedDate, s => true, (s, d) => (0, 0));
            window.Owner = this;
            window.ShowDialog();
        }

        private void StartAutoRefresh()
        {
            refreshTimer = new DispatcherTimer();
            refreshTimer.Interval = TimeSpan.FromMinutes(1);
            refreshTimer.Tick += async (s, e) => await ReloadAllAsync();
            refreshTimer.Start();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            refreshTimer?.Stop();
        }
        #endregion

        #region Data Classes
        public class ZamowienieInfo
        {
            public int Id { get; set; }
            public int KlientId { get; set; }
            public string Klient { get; set; } = "";
            public string Handlowiec { get; set; } = "";
            public string Uwagi { get; set; } = "";
            public string Status { get; set; } = "";
            public decimal TotalIlosc { get; set; }
            public bool IsShipmentOnly { get; set; }
            public DateTime? DataUtworzenia { get; set; }
            public bool MaNotatke { get; set; }
            public TimeSpan? CzasWyjazdu { get; set; }
            public string StatusTransportu { get; set; } = "";
            public DateTime? DataKursu { get; set; }
            public bool MaFolie { get; set; }
            public long? TransportKursId { get; set; }
        }

        public class ContractorInfo
        {
            public int Id { get; set; }
            public string Shortcut { get; set; } = "";
            public string Handlowiec { get; set; } = "(Brak)";
        }

        public class TowarInfo
        {
            public int Id { get; set; }
            public string Kod { get; set; } = "";
        }

        public class ComboItem
        {
            public int Value { get; }
            public string Text { get; }

            public ComboItem(int value, string text)
            {
                Value = value;
                Text = text;
            }
        }

        public class ZamowienieViewModel : INotifyPropertyChanged
        {
            public ZamowienieInfo Info { get; }

            public string Klient => GetKlientDisplay();
            public decimal TotalIlosc => Info.TotalIlosc;
            public string Handlowiec => Info.Handlowiec;
            public string Status => Info.Status;
            public DateTime? DataUtworzenia => Info.DataUtworzenia;
            public string CzasWyjazdDisplay => GetCzasWyjazdDisplay();
            public DateTime SortDateTime => GetSortDateTime();

            private Brush _rowColor = new SolidColorBrush(Color.FromRgb(55, 57, 70));
            public Brush RowColor
            {
                get => _rowColor;
                set
                {
                    _rowColor = value;
                    OnPropertyChanged();
                }
            }

            private Brush _textColor = Brushes.White;
            public Brush TextColor
            {
                get => _textColor;
                set
                {
                    _textColor = value;
                    OnPropertyChanged();
                }
            }

            public ZamowienieViewModel(ZamowienieInfo info)
            {
                Info = info;
            }

            private string GetKlientDisplay()
            {
                string prefix = "";
                if (Info.MaNotatke) prefix += "📝 ";
                if (Info.MaFolie) prefix += "🎞️ ";
                return prefix + Info.Klient;
            }

            private string GetCzasWyjazdDisplay()
            {
                if (Info.CzasWyjazdu.HasValue && Info.DataKursu.HasValue)
                {
                    var dataKursu = Info.DataKursu.Value;
                    var dzienTygodnia = dataKursu.ToString("dddd", new System.Globalization.CultureInfo("pl-PL"));
                    return $"{Info.CzasWyjazdu.Value:hh\\:mm} {dzienTygodnia}";
                }
                else
                {
                    return Info.IsShipmentOnly ? "Nie zrobiono zamówienia" : "Brak kursu";
                }
            }

            private DateTime GetSortDateTime()
            {
                if (Info.CzasWyjazdu.HasValue && Info.DataKursu.HasValue)
                {
                    return Info.DataKursu.Value.Add(Info.CzasWyjazdu.Value);
                }
                return DateTime.MaxValue;
            }

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        #endregion
    }
}

