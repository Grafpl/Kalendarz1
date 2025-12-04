using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Globalization;
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
    public partial class ProdukcjaPanel : Window, INotifyPropertyChanged
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

        public ObservableCollection<ZamowienieViewModel> ZamowieniaList1 { get; set; } = new();
        public ObservableCollection<ZamowienieViewModel> ZamowieniaList2 { get; set; } = new();

        private double _realizationProgress;
        public double RealizationProgress
        {
            get => _realizationProgress;
            set { _realizationProgress = value; OnPropertyChanged(); }
        }

        private string _realizationProgressText;
        public string RealizationProgressText
        {
            get => _realizationProgressText;
            set { _realizationProgressText = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public ProdukcjaPanel()
        {
            InitializeComponent();
            this.DataContext = this;
            InitializeAsync();
            KeyDown += ProdukcjaPanel_KeyDown;
            StartAutoRefresh();
        }

        private async void InitializeAsync()
        {
            await ReloadAllAsync();
        }

        private void ProdukcjaPanel_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Close();
            if (e.Key == Key.Enter) TryOpenShipmentDetails();
        }

        private ZamowienieViewModel SelectedZamowienie =>
            dgvZamowienia1.SelectedItem as ZamowienieViewModel ?? dgvZamowienia2.SelectedItem as ZamowienieViewModel;

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

        private async void dgvZamowienia1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgvZamowienia1.SelectedItem != null)
            {
                dgvZamowienia2.SelectedItem = null;
                await LoadPozycjeForSelectedAsync();
            }
        }

        private async void dgvZamowienia2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgvZamowienia2.SelectedItem != null)
            {
                dgvZamowienia1.SelectedItem = null;
                await LoadPozycjeForSelectedAsync();
            }
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
            lblData.Text = _selectedDate.ToString("yyyy-MM-dd dddd", new CultureInfo("pl-PL"));

            await PopulateProductFilterAsync();
            await LoadOrdersAsync();
            await LoadIn0ESummaryAsync();
            await LoadPojTuszkiAsync();
            await LoadPozycjeForSelectedAsync();
            await LoadHandlowcyStatsAsync();
        }

        private async Task PopulateProductFilterAsync()
        {
            string dateColumn = "DataUboju";
            var ids = new HashSet<int>();

            try
            {
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
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd połączenia z bazą LibraNet:\n{ex.Message}", "Błąd krytyczny", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _produktLookup.Clear();
            if (ids.Count > 0)
            {
                await LoadProductLookupAsync(ids);
            }

            var items = new List<ComboItem> { new ComboItem(0, "— Wszystkie —") };
            items.AddRange(_produktLookup.OrderBy(k => k.Value).Select(k => new ComboItem(k.Key, k.Value)));

            var prevSelectedValue = cbFiltrProdukt.SelectedValue;
            cbFiltrProdukt.ItemsSource = items;
            cbFiltrProdukt.DisplayMemberPath = "Text";
            cbFiltrProdukt.SelectedValuePath = "Value";

            if (prevSelectedValue != null && items.Any(i => i.Value.Equals(prevSelectedValue)))
                cbFiltrProdukt.SelectedValue = prevSelectedValue;
            else
                cbFiltrProdukt.SelectedIndex = 0;

            if (cbFiltrProdukt.Tag == null)
            {
                cbFiltrProdukt.SelectionChanged += async (s, e) =>
                {
                    if (cbFiltrProdukt.SelectedItem is ComboItem item)
                    {
                        _filteredProductId = item.Value == 0 ? null : item.Value;
                        await LoadOrdersAsync();
                    }
                };
                cbFiltrProdukt.Tag = "Initialized";
            }
        }

        private async Task LoadProductLookupAsync(HashSet<int> ids)
        {
            var list = ids.ToList();
            const int batch = 400;

            for (int i = 0; i < list.Count; i += batch)
            {
                try
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
                        _produktLookup[rd.GetInt32(0)] = rd.GetString(1);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd połączenia z bazą Handel podczas pobierania produktów:\n{ex.Message}", "Błąd krytyczny", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
        }

        private async Task LoadOrdersAsync()
        {
            string dateColumn = "DataUboju";
            _zamowienia.Clear();
            var orderListForGrid = new List<ZamowienieViewModel>();
            var klientIdsWithOrder = new HashSet<int>();

            try
            {
                using (var cn = new SqlConnection(_connLibra))
                {
                    await cn.OpenAsync();
                    var sqlBuilder = new System.Text.StringBuilder();
                    sqlBuilder.Append("SELECT z.Id, z.KlientId, ISNULL(z.Uwagi,'') AS Uwagi, ISNULL(z.Status,'Nowe') AS Status, ");
                    sqlBuilder.Append("(SELECT SUM(ISNULL(t.Ilosc, 0)) FROM dbo.ZamowieniaMiesoTowar t WHERE t.ZamowienieId = z.Id");
                    if (_filteredProductId.HasValue) sqlBuilder.Append(" AND t.KodTowaru=@P");
                    sqlBuilder.Append(") AS TotalIlosc, z.DataUtworzenia, z.TransportKursID, ");
                    sqlBuilder.Append("CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.ZamowieniaMiesoTowar t WHERE t.ZamowienieId = z.Id AND t.Folia = 1) THEN 1 ELSE 0 END AS BIT) AS MaFolie ");
                    sqlBuilder.Append($"FROM dbo.ZamowieniaMieso z WHERE z.{dateColumn}=@D AND ISNULL(z.Status,'Nowe') NOT IN ('Anulowane')");
                    if (_filteredProductId.HasValue) sqlBuilder.Append(" AND EXISTS (SELECT 1 FROM dbo.ZamowieniaMiesoTowar t WHERE t.ZamowienieId=z.Id AND t.KodTowaru=@P)");

                    var cmd = new SqlCommand(sqlBuilder.ToString(), cn);
                    cmd.Parameters.AddWithValue("@D", _selectedDate.Date);
                    if (_filteredProductId.HasValue) cmd.Parameters.AddWithValue("@P", _filteredProductId.Value);

                    using var rd = await cmd.ExecuteReaderAsync();
                    while (await rd.ReadAsync())
                    {
                        var info = new ZamowienieInfo
                        {
                            Id = rd.GetInt32(0),
                            KlientId = rd.GetInt32(1),
                            Uwagi = rd.GetString(2),
                            Status = rd.GetString(3),
                            TotalIlosc = rd.IsDBNull(4) ? 0 : rd.GetDecimal(4),
                            DataUtworzenia = rd.IsDBNull(5) ? (DateTime?)null : rd.GetDateTime(5),
                            TransportKursId = rd.IsDBNull(6) ? null : rd.GetInt64(6),
                            MaFolie = rd.GetBoolean(7),
                            MaNotatke = !string.IsNullOrWhiteSpace(rd.GetString(2))
                        };
                        _zamowienia[info.Id] = info;
                        klientIdsWithOrder.Add(info.KlientId);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd połączenia z bazą LibraNet podczas pobierania zamówień:\n{ex.Message}", "Błąd krytyczny", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }


            await LoadTransportInfoAsync();

            if (cbPokazWydaniaSymfonia.IsChecked == true)
            {
                var shipments = await LoadShipmentsAsync();
                var shipmentOnlyClientIds = shipments.Select(s => s.KlientId).Distinct().Except(klientIdsWithOrder).ToList();
                if (shipmentOnlyClientIds.Any())
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
                            IsShipmentOnly = true
                        };
                        orderListForGrid.Add(new ZamowienieViewModel(info));
                    }
                }
            }

            var orderClientIds = _zamowienia.Values.Select(o => o.KlientId).Distinct().ToList();
            if (orderClientIds.Any())
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
                    }
                    orderListForGrid.Add(new ZamowienieViewModel(orderInfo));
                }
            }

            var sorted = orderListForGrid.OrderBy(o => StatusOrder(o.Status)).ThenBy(o => o.SortDateTime).ThenBy(o => o.Handlowiec).ThenBy(o => o.Klient).ToList();

            ZamowieniaList1.Clear();
            ZamowieniaList2.Clear();

            int midpoint = (int)Math.Ceiling(sorted.Count / 2.0);
            sorted.Take(midpoint).ToList().ForEach(ZamowieniaList1.Add);
            sorted.Skip(midpoint).ToList().ForEach(ZamowieniaList2.Add);

            UpdateAllRowColors();
            UpdateProgressInfo();
        }

        private async Task LoadTransportInfoAsync()
        {
            try
            {
                using var cn = new SqlConnection(_connTransport);
                await cn.OpenAsync();

                // Pobierz wszystkie kursy do słownika
                var kursyInfo = new Dictionary<long, (TimeSpan? CzasWyjazdu, DateTime DataKursu)>();
                using (var cmd = new SqlCommand("SELECT KursID, GodzWyjazdu, Status, DataKursu FROM dbo.Kurs", cn))
                using (var rd = await cmd.ExecuteReaderAsync())
                {
                    while (await rd.ReadAsync())
                    {
                        var kursId = rd.GetInt64(0);
                        var godzWyjazdu = rd.IsDBNull(1) ? (TimeSpan?)null : rd.GetTimeSpan(1);
                        var dataKursu = rd.GetDateTime(3);
                        kursyInfo[kursId] = (godzWyjazdu, dataKursu);
                    }
                }

                // Pobierz mapowanie zamówienie -> kurs z tabeli Ladunek (dla łączonych kursów)
                var zamowienieToKurs = new Dictionary<int, long>();
                using (var cmd = new SqlCommand("SELECT KursID, KodKlienta FROM dbo.Ladunek WHERE KodKlienta LIKE 'ZAM_%'", cn))
                using (var rd = await cmd.ExecuteReaderAsync())
                {
                    while (await rd.ReadAsync())
                    {
                        var kursId = rd.GetInt64(0);
                        var kodKlienta = rd.GetString(1);
                        if (kodKlienta.StartsWith("ZAM_") && int.TryParse(kodKlienta.Substring(4), out int zamId))
                        {
                            zamowienieToKurs[zamId] = kursId;
                        }
                    }
                }

                // Przypisz informacje o kursach do WSZYSTKICH zamówień (nie tylko pierwszego!)
                foreach (var order in _zamowienia.Values)
                {
                    long? kursId = order.TransportKursId;

                    // Jeśli nie ma TransportKursId, sprawdź tabelę Ladunek
                    if (!kursId.HasValue && zamowienieToKurs.TryGetValue(order.Id, out var ladunekKursId))
                    {
                        kursId = ladunekKursId;
                    }

                    // Przypisz dane kursu
                    if (kursId.HasValue && kursyInfo.TryGetValue(kursId.Value, out var info))
                    {
                        order.CzasWyjazdu = info.CzasWyjazdu;
                        order.DataKursu = info.DataKursu;
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Błąd transportu: {ex.Message}"); }
        }

        private async Task<List<(int KlientId, decimal Qty)>> LoadShipmentsAsync()
        {
            var shipments = new List<(int KlientId, decimal Qty)>();
            try
            {
                using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                string sql = @"SELECT MG.khid, SUM(ABS(MZ.ilosc)) FROM HANDEL.HM.MZ MZ JOIN HANDEL.HM.MG ON MZ.super=MG.id 
                               WHERE MG.seria IN ('sWZ','sWZ-W') AND MG.aktywny=1 AND MG.data=@D AND MG.khid IS NOT NULL";
                if (_filteredProductId.HasValue) sql += " AND MZ.idtw=@P";
                sql += " GROUP BY MG.khid";
                var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@D", _selectedDate.Date);
                if (_filteredProductId.HasValue) cmd.Parameters.AddWithValue("@P", _filteredProductId.Value);
                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    shipments.Add((rd.GetInt32(0), Convert.ToDecimal(rd.GetValue(1))));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd połączenia z bazą Handel podczas pobierania wydań:\n{ex.Message}", "Błąd krytyczny", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return shipments;
        }

        private async Task LoadPozycjeForSelectedAsync()
        {
            var vm = SelectedZamowienie;
            if (vm == null)
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
                var cmdProd = new SqlCommand("SELECT NotatkaProdukcja FROM dbo.ZamowieniaMiesoProdukcjaNotatki WHERE ZamowienieId = @Id", cnProd);
                cmdProd.Parameters.AddWithValue("@Id", info.Id);
                txtNotatkiTransportu.Text = (await cmdProd.ExecuteScalarAsync())?.ToString() ?? "";
            }
            catch { txtNotatkiTransportu.Text = ""; }

            await EnsureNotesTableAsync();

            var orderPositions = new List<(int TowarId, decimal Ilosc, bool Folia)>();
            using (var cn = new SqlConnection(_connLibra))
            {
                await cn.OpenAsync();
                string sql = @"SELECT zmt.KodTowaru, zmt.Ilosc, ISNULL(zmt.Folia, 0) AS Folia 
                               FROM dbo.ZamowieniaMiesoTowar zmt 
                               WHERE zmt.ZamowienieId=@Id" + (_filteredProductId.HasValue ? " AND zmt.KodTowaru=@P" : "");
                var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Id", info.Id);
                if (_filteredProductId.HasValue) cmd.Parameters.AddWithValue("@P", _filteredProductId.Value);

                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    orderPositions.Add((rd.GetInt32(0), rd.GetDecimal(1), rd.GetBoolean(2)));
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

            var mapOrd = orderPositions.ToDictionary(p => p.TowarId, p => (p.Ilosc, p.Folia));

            foreach (var id in ids)
            {
                mapOrd.TryGetValue(id, out var ord);
                shipments.TryGetValue(id, out var wyd);
                string kod = towarMap.TryGetValue(id, out var t) ? t.Kod : $"ID:{id}";
                if (ord.Folia) kod = "🎞️ " + kod;
                dt.Rows.Add(kod, ord.Ilosc, wyd, ord.Ilosc - wyd);
            }

            dgvPozycje.ItemsSource = dt.DefaultView;
        }

        private async Task LoadIn0ESummaryAsync()
        {
            var dt = new DataTable();
            dt.Columns.Add("Towar", typeof(string));
            dt.Columns.Add("Kg", typeof(decimal));

            try
            {
                using (var cn = new SqlConnection(_connLibra))
                {
                    await cn.OpenAsync();
                    var cmd = new SqlCommand(@"SELECT ArticleName, SUM(CAST(Weight AS float)) W 
                                           FROM dbo.In0E WHERE Data=@D AND ISNULL(ArticleName,'')<>'' 
                                           GROUP BY ArticleName ORDER BY W DESC", cn);
                    cmd.Parameters.AddWithValue("@D", _selectedDate.ToString("yyyy-MM-dd"));
                    using var rd = await cmd.ExecuteReaderAsync();
                    while (await rd.ReadAsync())
                    {
                        dt.Rows.Add(rd.GetString(0), Convert.ToDecimal(rd.GetValue(1)));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd ładowania In0E: {ex.Message}");
            }
            dgvIn0ESumy.ItemsSource = dt.DefaultView;
        }

        private async Task LoadPojTuszkiAsync()
        {
            var dt = new DataTable();
            dt.Columns.Add("Typ", typeof(string));
            dt.Columns.Add("Palety", typeof(int));
            dt.Columns.Add("Udział", typeof(string));

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


                decimal totalPalety = pojData.Sum(p => p.Pal);

                if (totalPalety > 0)
                {
                    foreach (var data in pojData)
                    {
                        decimal udzial = (data.Pal / totalPalety) * 100;
                        dt.Rows.Add($"Poj. {data.Poj}", data.Pal, $"{udzial:N1}%");
                    }
                }

                if (pojData.Any()) dt.Rows.Add("", DBNull.Value, "");

                var duzyIds = new HashSet<int> { 5, 6, 7, 8 };
                var malyIds = new HashSet<int> { 9, 10, 11 };

                int duzyKurczakPalety = pojData.Where(p => duzyIds.Contains(p.Poj)).Sum(p => p.Pal);
                int malyKurczakPalety = pojData.Where(p => malyIds.Contains(p.Poj)).Sum(p => p.Pal);
                string duzyUdzial = totalPalety > 0 ? $"{(duzyKurczakPalety / totalPalety) * 100:N1}%" : "0.0%";
                string malyUdzial = totalPalety > 0 ? $"{(malyKurczakPalety / totalPalety) * 100:N1}%" : "0.0%";

                dt.Rows.Add("Duży kurczak", duzyKurczakPalety, duzyUdzial);
                dt.Rows.Add("Mały kurczak", malyKurczakPalety, malyUdzial);
                dt.Rows.Add("SUMA", (int)totalPalety, "100%");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd ładowania tuszek: {ex.Message}");
            }
            dgvPojTuszki.ItemsSource = dt.DefaultView;
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
            dt.Columns.Add("Zamówiono (kg)", typeof(decimal));
            dt.Columns.Add("Wydano (kg)", typeof(decimal));
            dt.Columns.Add("Różnica (kg)", typeof(decimal));

            foreach (var kv in shipments.OrderBy(s => s.Key))
            {
                string kod = towarMap.TryGetValue(kv.Key, out var t) ? t.Kod : $"ID:{kv.Key}";
                dt.Rows.Add(kod, 0, kv.Value, -kv.Value);
            }

            dgvPozycje.ItemsSource = dt.DefaultView;
        }

        private async Task LoadHandlowcyStatsAsync()
        {
            var dt = new DataTable();
            dt.Columns.Add("Handlowiec", typeof(string));
            dt.Columns.Add("IloscZamowien", typeof(int));
            dt.Columns.Add("SumaKg", typeof(decimal));

            await Task.Run(() =>
            {
                var stats = _zamowienia.Values
                    .Where(z => !z.IsShipmentOnly)
                    .GroupBy(z => z.Handlowiec)
                    .Select(g => new
                    {
                        Handlowiec = g.Key,
                        IloscZamowien = g.Count(),
                        SumaKg = g.Sum(z => z.TotalIlosc)
                    })
                    .OrderByDescending(s => s.SumaKg);

                foreach (var s in stats)
                {
                    dt.Rows.Add(s.Handlowiec, s.IloscZamowien, s.SumaKg);
                }
            });

            dgvHandlowcyStats.ItemsSource = dt.DefaultView;
        }
        #endregion

        #region Helper Methods
        private string Normalize(string s) => string.IsNullOrWhiteSpace(s) ? "(Brak)" : string.Join(' ', s.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));

        private int StatusOrder(string status) => status switch { "Nowe" => 0, "Zrealizowane" => 1, "Wydanie Symfonia" => 2, _ => 3 };

        private void UpdateAllRowColors()
        {
            foreach (var item in ZamowieniaList1) UpdateRowColor(item);
            foreach (var item in ZamowieniaList2) UpdateRowColor(item);
        }

        private void UpdateRowColor(ZamowienieViewModel item)
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
                var wyjazd = item.Info.DataKursu.Value.Add(item.Info.CzasWyjazdu.Value);
                var roznica = (wyjazd - DateTime.Now).TotalMinutes;
                if (roznica < 0)
                {
                    item.RowColor = new SolidColorBrush(Color.FromRgb(139, 0, 0));
                    item.TextColor = Brushes.White;
                }
                else if (roznica <= 30)
                {
                    item.RowColor = new SolidColorBrush(Color.FromRgb(218, 165, 32));
                    item.TextColor = Brushes.Black;
                }
                else
                {
                    item.RowColor = Brushes.Transparent;
                    item.TextColor = Brushes.White;
                }
            }
            else
            {
                item.RowColor = Brushes.Transparent;
                item.TextColor = Brushes.White;
            }
        }

        private void UpdateProgressInfo()
        {
            int total = _zamowienia.Count;
            if (total == 0)
            {
                RealizationProgress = 0;
                RealizationProgressText = "0";
                return;
            }
            int realized = _zamowienia.Values.Count(z => z.Status == "Zrealizowane");
            RealizationProgress = (double)realized / total * 100;
            RealizationProgressText = $"{RealizationProgress:F0}";
        }

        private async Task<Dictionary<int, ContractorInfo>> LoadContractorsAsync(List<int> ids)
        {
            var dict = new Dictionary<int, ContractorInfo>();
            if (!ids.Any()) return dict;
            try
            {
                using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                var cmd = new SqlCommand($@"SELECT c.Id, ISNULL(c.Shortcut,'KH '+CAST(c.Id AS varchar(10))), ISNULL(w.CDim_Handlowiec_Val,'(Brak)') 
                                            FROM SSCommon.STContractors c LEFT JOIN SSCommon.ContractorClassification w ON c.Id=w.ElementId 
                                            WHERE c.Id IN ({string.Join(',', ids)})", cn);
                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    dict[rd.GetInt32(0)] = new ContractorInfo { Id = rd.GetInt32(0), Shortcut = rd.GetString(1).Trim(), Handlowiec = Normalize(rd.GetString(2)) };
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd połączenia z bazą Handel podczas pobierania kontrahentów:\n{ex.Message}", "Błąd krytyczny", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return dict;
        }

        private async Task<Dictionary<int, TowarInfo>> LoadTowaryAsync(List<int> ids)
        {
            var dict = new Dictionary<int, TowarInfo>();
            if (!ids.Any()) return dict;
            try
            {
                using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                var cmd = new SqlCommand($"SELECT ID,kod FROM HM.TW WHERE ID IN ({string.Join(',', ids)})", cn);
                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    dict[rd.GetInt32(0)] = new TowarInfo { Id = rd.GetInt32(0), Kod = rd.GetString(1) };
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd połączenia z bazą Handel podczas pobierania towarów:\n{ex.Message}", "Błąd krytyczny", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return dict;
        }

        private async Task<Dictionary<int, decimal>> GetShipmentsForClientAsync(int klientId)
        {
            var dict = new Dictionary<int, decimal>();
            try
            {
                using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                string sql = @"SELECT MZ.idtw, SUM(ABS(MZ.ilosc)) 
                               FROM HANDEL.HM.MZ MZ JOIN HANDEL.HM.MG ON MZ.super=MG.id 
                               JOIN HANDEL.HM.TW ON MZ.idtw=TW.id 
                               WHERE MG.seria IN ('sWZ','sWZ-W') AND MG.aktywny=1 AND MG.data=@D AND MG.khid=@K AND TW.katalog=67095"
                               + (_filteredProductId.HasValue ? " AND MZ.idtw=@P" : "") + " GROUP BY MZ.idtw";
                var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@D", _selectedDate.Date);
                cmd.Parameters.AddWithValue("@K", klientId);
                if (_filteredProductId.HasValue) cmd.Parameters.AddWithValue("@P", _filteredProductId.Value);

                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    dict[rd.GetInt32(0)] = Convert.ToDecimal(rd.GetValue(1));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd połączenia z bazą Handel podczas pobierania wydań klienta:\n{ex.Message}", "Błąd krytyczny", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return dict;
        }

        private int? GetSelectedOrderId() => SelectedZamowienie != null && !SelectedZamowienie.Info.IsShipmentOnly ? SelectedZamowienie.Info.Id : null;
        #endregion

        #region Actions
        private async Task MarkOrderRealizedAsync()
        {
            var orderId = GetSelectedOrderId();
            if (!orderId.HasValue) return;
            if (MessageBox.Show("Oznaczyć zamówienie jako zrealizowane?", "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            var cmd = new SqlCommand("UPDATE dbo.ZamowieniaMieso SET Status='Zrealizowane' WHERE Id=@I", cn);
            cmd.Parameters.AddWithValue("@I", orderId.Value);
            await cmd.ExecuteNonQueryAsync();
            await LoadOrdersAsync();
        }

        private async Task UndoRealizedAsync()
        {
            var orderId = GetSelectedOrderId();
            if (!orderId.HasValue) return;
            if (MessageBox.Show("Cofnąć realizację?", "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            var cmd = new SqlCommand("UPDATE dbo.ZamowieniaMieso SET Status='Nowe' WHERE Id=@I", cn);
            cmd.Parameters.AddWithValue("@I", orderId.Value);
            await cmd.ExecuteNonQueryAsync();
            await LoadOrdersAsync();
        }

        private async Task EnsureNotesTableAsync()
        {
            if (_notesTableEnsured) return;
            try
            {
                using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                var cmd = new SqlCommand(@"IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name='ZamowieniaMiesoProdukcjaNotatki' AND type='U') 
                                           CREATE TABLE dbo.ZamowieniaMiesoProdukcjaNotatki(
                                               ZamowienieId INT PRIMARY KEY, 
                                               NotatkaProdukcja NVARCHAR(MAX), 
                                               DataModyfikacji DATETIME, 
                                               Uzytkownik NVARCHAR(100));", cn);
                await cmd.ExecuteNonQueryAsync();
                _notesTableEnsured = true;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Błąd tworzenia tabeli notatek: {ex.Message}"); }
        }

        private async Task SaveProductionNotesAsync()
        {
            var orderId = GetSelectedOrderId();
            if (!orderId.HasValue) return;

            await EnsureNotesTableAsync();

            string notatka = txtNotatkiTransportu.Text.Trim();
            using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();

            var checkCmd = new SqlCommand("SELECT COUNT(*) FROM dbo.ZamowieniaMiesoProdukcjaNotatki WHERE ZamowienieId = @Id", cn);
            checkCmd.Parameters.AddWithValue("@Id", orderId.Value);

            string sql = (int)await checkCmd.ExecuteScalarAsync() > 0
                ? "UPDATE dbo.ZamowieniaMiesoProdukcjaNotatki SET NotatkaProdukcja = @Notatka, DataModyfikacji = GETDATE(), Uzytkownik = @User WHERE ZamowienieId = @Id"
                : "INSERT INTO dbo.ZamowieniaMiesoProdukcjaNotatki (ZamowienieId, NotatkaProdukcja, DataModyfikacji, Uzytkownik) VALUES (@Id, @Notatka, GETDATE(), @User)";

            var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@Id", orderId.Value);
            cmd.Parameters.AddWithValue("@Notatka", string.IsNullOrWhiteSpace(notatka) ? (object)DBNull.Value : notatka);
            cmd.Parameters.AddWithValue("@User", UserID);

            await cmd.ExecuteNonQueryAsync();
            MessageBox.Show("Zapisano notatkę produkcji.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void TryOpenShipmentDetails()
        {
            if (SelectedZamowienie != null && SelectedZamowienie.Info.IsShipmentOnly)
            {
                new ShipmentDetailsWindow(_connHandel, SelectedZamowienie.Info.KlientId, _selectedDate) { Owner = this }.ShowDialog();
            }
        }

        private void OpenLiveWindow()
        {
            new LivePrzychodyWindow(_connLibra, _selectedDate, s => true, (s, d) => (0, 0)) { Owner = this }.ShowDialog();
        }

        private void StartAutoRefresh()
        {
            refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
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
            public ComboItem(int value, string text) { Value = value; Text = text; }
        }

        public class ZamowienieViewModel : INotifyPropertyChanged
        {
            public ZamowienieInfo Info { get; }
            public ZamowienieViewModel(ZamowienieInfo info) { Info = info; }

            public string Klient => $"{(Info.MaNotatke ? "📝 " : "")}{(Info.MaFolie ? "🎞️ " : "")}{Info.Klient}";
            public decimal TotalIlosc => Info.TotalIlosc;
            public string Handlowiec => Info.Handlowiec;
            public string Status => Info.Status;
            public string CzasWyjazdDisplay => Info.CzasWyjazdu.HasValue && Info.DataKursu.HasValue
                ? $"{Info.CzasWyjazdu.Value:hh\\:mm} {Info.DataKursu.Value.ToString("dddd", new CultureInfo("pl-PL"))}"
                : (Info.IsShipmentOnly ? "Nie zrobiono zamówienia" : "Brak kursu");
            public DateTime SortDateTime => Info.CzasWyjazdu.HasValue && Info.DataKursu.HasValue ? Info.DataKursu.Value.Add(Info.CzasWyjazdu.Value) : DateTime.MaxValue;

            private Brush _rowColor = Brushes.Transparent;
            public Brush RowColor { get => _rowColor; set { _rowColor = value; OnPropertyChanged(); } }

            private Brush _textColor = Brushes.White;
            public Brush TextColor { get => _textColor; set { _textColor = value; OnPropertyChanged(); } }

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        #endregion
    }
}

