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

        private static bool? _dataAkceptacjiProdukcjaColumnExists = null;

        public string UserID { get; set; } = "User";
        private DateTime _selectedDate = DateTime.Today;
        private readonly Dictionary<int, ZamowienieInfo> _zamowienia = new();
        private bool _notesTableEnsured = false;
        private int? _filteredProductId = null;
        private Dictionary<int, string> _produktLookup = new();
        private Button _selectedProductButton = null;
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
            var selected = SelectedZamowienie;
            if (selected == null) return;

            if (selected.Info.CzyZrealizowane)
            {
                // Cofnij realizację
                await UndoRealizedAsync();
            }
            else
            {
                // Oznacz jako zrealizowane z opcjonalną notatką
                await MarkOrderRealizedWithNoteAsync();
            }
        }

        private void UpdateZrealizowanoButtonState()
        {
            var selected = SelectedZamowienie;
            if (selected != null && selected.Info.CzyZrealizowane)
            {
                btnZrealizowano.Content = "↩ COFNIJ REAL.";
                btnZrealizowano.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C62828"));
            }
            else
            {
                btnZrealizowano.Content = "✓ ZREALIZOWANO";
                btnZrealizowano.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#19874B"));
            }
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
            UpdateZrealizowanoButtonState();
        }

        private async void dgvZamowienia2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgvZamowienia2.SelectedItem != null)
            {
                dgvZamowienia1.SelectedItem = null;
                await LoadPozycjeForSelectedAsync();
            }
            UpdateZrealizowanoButtonState();
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
            await LoadPrzychodyInfoAsync();
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

            // Utwórz przyciski towarów
            pnlProductButtons.Children.Clear();

            // Przycisk "Wszystkie"
            var btnAll = CreateProductButton(0, "Wszystkie");
            pnlProductButtons.Children.Add(btnAll);
            if (!_filteredProductId.HasValue)
            {
                SetProductButtonSelected(btnAll);
            }

            // Przyciski dla poszczególnych towarów
            foreach (var product in _produktLookup.OrderBy(k => k.Value))
            {
                var btn = CreateProductButton(product.Key, product.Value);
                pnlProductButtons.Children.Add(btn);
                if (_filteredProductId == product.Key)
                {
                    SetProductButtonSelected(btn);
                }
            }
        }

        private Button CreateProductButton(int productId, string productName)
        {
            var btn = new Button
            {
                Content = productName,
                Tag = productId,
                Height = 32,
                MinWidth = 70,
                Padding = new Thickness(10, 3, 10, 3),
                Margin = new Thickness(2),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x3C, 0x48)),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                Cursor = Cursors.Hand
            };

            btn.Click += ProductButton_Click;
            return btn;
        }

        private void SetProductButtonSelected(Button btn)
        {
            // Reset poprzednio zaznaczonego przycisku
            if (_selectedProductButton != null)
            {
                _selectedProductButton.Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x3C, 0x48));
                _selectedProductButton.BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
            }

            // Zaznacz nowy przycisk
            _selectedProductButton = btn;
            btn.Background = new SolidColorBrush(Color.FromRgb(0x3C, 0x46, 0x8E));
            btn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x5A, 0x6A, 0xDE));
        }

        private async void ProductButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int productId)
            {
                _filteredProductId = productId == 0 ? null : productId;
                SetProductButtonSelected(btn);
                await LoadOrdersAsync();
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

        private async Task<bool> CheckDataAkceptacjiProdukcjaColumnExistsAsync(SqlConnection cn)
        {
            if (_dataAkceptacjiProdukcjaColumnExists.HasValue)
                return _dataAkceptacjiProdukcjaColumnExists.Value;

            string checkSql = @"SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ZamowieniaMieso') AND name = 'DataAkceptacjiProdukcja'";
            using var cmd = new SqlCommand(checkSql, cn);
            var result = await cmd.ExecuteScalarAsync();
            _dataAkceptacjiProdukcjaColumnExists = result != null;
            return _dataAkceptacjiProdukcjaColumnExists.Value;
        }

        private static bool? _partialRealizationColumnsExist = null;
        private async Task<bool> CheckPartialRealizationColumnsExistAsync(SqlConnection cn)
        {
            if (_partialRealizationColumnsExist.HasValue)
                return _partialRealizationColumnsExist.Value;

            string checkSql = @"SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ZamowieniaMieso') AND name = 'CzyCzesciowoZrealizowane'";
            using var cmd = new SqlCommand(checkSql, cn);
            var result = await cmd.ExecuteScalarAsync();
            _partialRealizationColumnsExist = result != null;
            return _partialRealizationColumnsExist.Value;
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

                    // Sprawdź czy kolumna DataAkceptacjiProdukcja istnieje
                    bool hasAkceptacjaColumn = await CheckDataAkceptacjiProdukcjaColumnExistsAsync(cn);
                    string akceptacjaColumn = hasAkceptacjaColumn ? ", z.DataAkceptacjiProdukcja" : ", NULL AS DataAkceptacjiProdukcja";

                    // Sprawdź czy kolumny częściowej realizacji istnieją
                    bool hasPartialColumns = await CheckPartialRealizationColumnsExistAsync(cn);

                    var sqlBuilder = new System.Text.StringBuilder();
                    sqlBuilder.Append("SELECT z.Id, z.KlientId, ISNULL(z.Uwagi,'') AS Uwagi, ISNULL(z.Status,'Nowe') AS Status, ");
                    sqlBuilder.Append("(SELECT SUM(ISNULL(t.Ilosc, 0)) FROM dbo.ZamowieniaMiesoTowar t WHERE t.ZamowienieId = z.Id");
                    if (_filteredProductId.HasValue) sqlBuilder.Append(" AND t.KodTowaru=@P");
                    sqlBuilder.Append(") AS TotalIlosc, z.DataUtworzenia, z.TransportKursID, ");
                    sqlBuilder.Append("CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.ZamowieniaMiesoTowar t WHERE t.ZamowienieId = z.Id AND t.Folia = 1) THEN 1 ELSE 0 END AS BIT) AS MaFolie, ");
                    sqlBuilder.Append("ISNULL(z.CzyZrealizowane, 0) AS CzyZrealizowane, ");
                    sqlBuilder.Append("ISNULL(z.CzyWydane, 0) AS CzyWydane, ");
                    sqlBuilder.Append("CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.ZamowieniaMiesoTowar t WHERE t.ZamowienieId = z.Id AND ISNULL(t.Hallal, 0) = 1) THEN 1 ELSE 0 END AS BIT) AS MaHalal, ");
                    sqlBuilder.Append("CAST(CASE WHEN z.TransportStatus = 'Wlasny' THEN 1 ELSE 0 END AS BIT) AS WlasnyTransport, ");
                    sqlBuilder.Append("z.DataPrzyjazdu, ");
                    // Nowe pola do wykrywania zmian
                    sqlBuilder.Append("z.DataOstatniejModyfikacji, z.DataRealizacji");
                    sqlBuilder.Append(akceptacjaColumn);
                    // Pola częściowej realizacji
                    if (hasPartialColumns)
                        sqlBuilder.Append(", ISNULL(z.CzyCzesciowoZrealizowane, 0) AS CzyCzesciowoZrealizowane, z.ProcentRealizacji");
                    else
                        sqlBuilder.Append(", CAST(0 AS BIT) AS CzyCzesciowoZrealizowane, NULL AS ProcentRealizacji");
                    sqlBuilder.Append($" FROM dbo.ZamowieniaMieso z WHERE z.{dateColumn}=@D AND ISNULL(z.Status,'Nowe') NOT IN ('Anulowane')");
                    if (_filteredProductId.HasValue) sqlBuilder.Append(" AND EXISTS (SELECT 1 FROM dbo.ZamowieniaMiesoTowar t WHERE t.ZamowienieId=z.Id AND t.KodTowaru=@P)");

                    var cmd = new SqlCommand(sqlBuilder.ToString(), cn);
                    cmd.Parameters.AddWithValue("@D", _selectedDate.Date);
                    if (_filteredProductId.HasValue) cmd.Parameters.AddWithValue("@P", _filteredProductId.Value);

                    using var rd = await cmd.ExecuteReaderAsync();
                    while (await rd.ReadAsync())
                    {
                        var dataOstatniejModyfikacji = rd.IsDBNull(13) ? (DateTime?)null : rd.GetDateTime(13);
                        var dataRealizacji = rd.IsDBNull(14) ? (DateTime?)null : rd.GetDateTime(14);
                        var dataAkceptacjiProdukcja = rd.IsDBNull(15) ? (DateTime?)null : rd.GetDateTime(15);
                        var czyCzesciowoZrealizowane = rd.GetBoolean(16);
                        var procentRealizacji = rd.IsDBNull(17) ? (decimal?)null : rd.GetDecimal(17);
                        var czyZrealizowane = rd.GetBoolean(8);

                        // Sprawdź czy zamówienie zostało zmodyfikowane od czasu akceptacji przez produkcję
                        // Produkcja używa DataAkceptacjiProdukcja, a DataRealizacji jako fallback
                        bool czyZmodyfikowane = false;
                        if (czyZrealizowane && dataOstatniejModyfikacji.HasValue)
                        {
                            // Jeśli produkcja już zaakceptowała, porównaj z jej datą akceptacji
                            if (dataAkceptacjiProdukcja.HasValue)
                            {
                                czyZmodyfikowane = dataOstatniejModyfikacji.Value > dataAkceptacjiProdukcja.Value;
                            }
                            // Jeśli produkcja jeszcze nie akceptowała, użyj daty realizacji
                            else if (dataRealizacji.HasValue)
                            {
                                czyZmodyfikowane = dataOstatniejModyfikacji.Value > dataRealizacji.Value;
                            }
                        }

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
                            MaNotatke = !string.IsNullOrWhiteSpace(rd.GetString(2)),
                            CzyZrealizowane = czyZrealizowane,
                            CzyWydane = rd.GetBoolean(9),
                            MaHalal = rd.GetBoolean(10),
                            WlasnyTransport = rd.GetBoolean(11),
                            DataPrzyjazdu = rd.IsDBNull(12) ? null : rd.GetDateTime(12),
                            // Nowe pola do wykrywania zmian
                            DataOstatniejModyfikacji = dataOstatniejModyfikacji,
                            DataRealizacji = dataRealizacji,
                            DataAkceptacjiProdukcja = dataAkceptacjiProdukcja,
                            CzyZmodyfikowaneOdRealizacji = czyZmodyfikowane,
                            // Pola częściowej realizacji
                            CzyCzesciowoZrealizowane = czyCzesciowoZrealizowane,
                            ProcentRealizacji = procentRealizacji
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

            // Sort only by departure time (earliest first), "Brak kursu" at the end
            var sorted = orderListForGrid.OrderBy(o => o.SortDateTime).ThenBy(o => o.Klient).ToList();

            ZamowieniaList1.Clear();
            ZamowieniaList2.Clear();

            int midpoint = (int)Math.Ceiling(sorted.Count / 2.0);
            sorted.Take(midpoint).ToList().ForEach(ZamowieniaList1.Add);
            sorted.Skip(midpoint).ToList().ForEach(ZamowieniaList2.Add);

            UpdateAllRowColors();
            UpdateProgressInfo();
            UpdateFilteredSum();
        }

        private void UpdateFilteredSum()
        {
            decimal totalSum = ZamowieniaList1.Sum(z => z.TotalIlosc) + ZamowieniaList2.Sum(z => z.TotalIlosc);
            lblFilteredSum.Text = totalSum.ToString("N0");
        }

        private async Task LoadTransportInfoAsync()
        {
            try
            {
                using var cn = new SqlConnection(_connTransport);
                await cn.OpenAsync();

                // Pobierz wszystkie kursy do słownika (wraz z kierowcą)
                var kursyInfo = new Dictionary<long, (TimeSpan? CzasWyjazdu, DateTime DataKursu, string Kierowca)>();
                string sqlKursy = @"
                    SELECT k.KursID, k.GodzWyjazdu, k.DataKursu,
                           ISNULL(kie.Imie + ' ' + kie.Nazwisko, 'Nie przypisano') as Kierowca
                    FROM dbo.Kurs k
                    LEFT JOIN dbo.Kierowca kie ON k.KierowcaID = kie.KierowcaID";

                using (var cmd = new SqlCommand(sqlKursy, cn))
                using (var rd = await cmd.ExecuteReaderAsync())
                {
                    while (await rd.ReadAsync())
                    {
                        var kursId = rd.GetInt64(0);
                        var godzWyjazdu = rd.IsDBNull(1) ? (TimeSpan?)null : rd.GetTimeSpan(1);
                        var dataKursu = rd.GetDateTime(2);
                        var kierowca = rd.GetString(3);
                        kursyInfo[kursId] = (godzWyjazdu, dataKursu, kierowca);
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
                        order.Kierowca = info.Kierowca;
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
                lblHandlowiec.Text = "-";
                return;
            }

            var info = vm.Info;
            lblHandlowiec.Text = string.IsNullOrWhiteSpace(info.Handlowiec) ? "-" : info.Handlowiec;

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

            var orderPositions = new List<(int TowarId, decimal Ilosc, bool Folia, decimal? IloscZreal)>();
            using (var cn = new SqlConnection(_connLibra))
            {
                await cn.OpenAsync();

                // Sprawdź czy kolumna IloscZrealizowana istnieje
                bool hasZrealColumn = await CheckPartialRealizationColumnsExistAsync(cn);
                string zrealCol = hasZrealColumn ? ", zmt.IloscZrealizowana" : ", NULL AS IloscZrealizowana";

                string sql = $@"SELECT zmt.KodTowaru, zmt.Ilosc, ISNULL(zmt.Folia, 0) AS Folia{zrealCol}
                               FROM dbo.ZamowieniaMiesoTowar zmt
                               WHERE zmt.ZamowienieId=@Id" + (_filteredProductId.HasValue ? " AND zmt.KodTowaru=@P" : "");
                var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Id", info.Id);
                if (_filteredProductId.HasValue) cmd.Parameters.AddWithValue("@P", _filteredProductId.Value);

                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    orderPositions.Add((rd.GetInt32(0), rd.GetDecimal(1), rd.GetBoolean(2), rd.IsDBNull(3) ? null : rd.GetDecimal(3)));
                }
            }

            var shipments = await GetShipmentsForClientAsync(info.KlientId);
            if (_filteredProductId.HasValue)
                shipments = shipments.Where(k => k.Key == _filteredProductId.Value).ToDictionary(k => k.Key, v => v.Value);

            // Pobierz snapshot (jeśli zamówienie było realizowane)
            var snapshot = info.CzyZrealizowane ? await GetOrderSnapshotAsync(info.Id, "Realizacja") : new Dictionary<int, (decimal Ilosc, bool Folia)>();

            var ids = orderPositions.Select(p => p.TowarId).Union(shipments.Keys).Union(snapshot.Keys).Where(i => i > 0).Distinct().ToList();
            var towarMap = await LoadTowaryAsync(ids);

            var dt = new DataTable();
            dt.Columns.Add("Produkt", typeof(string));
            dt.Columns.Add("Zamówiono (kg)", typeof(decimal));
            dt.Columns.Add("Zrealizowano", typeof(string));
            dt.Columns.Add("Wydano (kg)", typeof(decimal));
            dt.Columns.Add("Różnica (kg)", typeof(decimal));
            // Kolumna zmian - pokazuje różnicę między aktualnym stanem a snapshotem
            dt.Columns.Add("Zmiana", typeof(string));

            var mapOrd = orderPositions.ToDictionary(p => p.TowarId, p => (p.Ilosc, p.Folia, p.IloscZreal));

            foreach (var id in ids)
            {
                mapOrd.TryGetValue(id, out var ord);
                shipments.TryGetValue(id, out var wyd);
                snapshot.TryGetValue(id, out var snap);

                string kod = towarMap.TryGetValue(id, out var t) ? t.Kod : $"ID:{id}";
                if (ord.Folia) kod = "🎞️ " + kod;

                // Oblicz zmianę od snapshotu
                string zmiana = "";
                if (info.CzyZrealizowane && snapshot.Count > 0)
                {
                    if (!snapshot.ContainsKey(id))
                    {
                        // Nowa pozycja dodana po realizacji
                        zmiana = "🆕 NOWE";
                        kod = "🆕 " + kod;
                    }
                    else if (ord.Ilosc != snap.Ilosc)
                    {
                        // Zmieniona ilość
                        decimal diff = ord.Ilosc - snap.Ilosc;
                        zmiana = diff > 0 ? $"+{diff:N0} kg" : $"{diff:N0} kg";
                    }
                }

                // Wyświetl zrealizowaną ilość tylko gdy różni się od zamówionej
                string zrealDisplay = "";
                if (ord.IloscZreal.HasValue && ord.IloscZreal.Value != ord.Ilosc)
                {
                    zrealDisplay = $"{ord.IloscZreal:N0}";
                }

                dt.Rows.Add(kod, ord.Ilosc, zrealDisplay, wyd, ord.Ilosc - wyd, zmiana);
            }

            // Sprawdź czy są pozycje usunięte (były w snapshocie, ale nie ma w aktualnym zamówieniu)
            if (info.CzyZrealizowane && snapshot.Count > 0)
            {
                foreach (var snapItem in snapshot.Where(s => !mapOrd.ContainsKey(s.Key)))
                {
                    string kod = towarMap.TryGetValue(snapItem.Key, out var t) ? t.Kod : $"ID:{snapItem.Key}";
                    kod = "❌ " + kod;
                    dt.Rows.Add(kod, 0, "", 0, 0, $"USUNIĘTO ({snapItem.Value.Ilosc:N0} kg)");
                }
            }

            dgvPozycje.ItemsSource = dt.DefaultView;

            // Pokaż/ukryj przycisk "Przyjmuję zmianę"
            btnAcceptChange.Visibility = info.CzyZmodyfikowaneOdRealizacji ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void btnAcceptChange_Click(object sender, RoutedEventArgs e)
        {
            var vm = SelectedZamowienie;
            if (vm == null || !vm.Info.CzyZmodyfikowaneOdRealizacji) return;

            var result = MessageBox.Show(
                $"Czy potwierdzasz, że wiesz o zmianach w zamówieniu '{vm.Info.Klient}'?\n\n" +
                "Aktualny stan pozycji zostanie zapisany jako nowy snapshot.\n" +
                "Ikona ⚠️ zniknie dopóki zamówienie nie zostanie ponownie zmodyfikowane.\n" +
                "(Magazyn ma swoją osobną akceptację)",
                "Potwierdzenie przyjęcia zmiany - Produkcja",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                // Upewnij się że kolumna DataAkceptacjiProdukcja istnieje
                var checkCmd = new SqlCommand("SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ZamowieniaMieso') AND name = 'DataAkceptacjiProdukcja'", cn);
                if ((int)await checkCmd.ExecuteScalarAsync() == 0)
                {
                    var addCmd = new SqlCommand("ALTER TABLE dbo.ZamowieniaMieso ADD DataAkceptacjiProdukcja DATETIME NULL", cn);
                    await addCmd.ExecuteNonQueryAsync();
                    // Zresetuj cache po utworzeniu kolumny
                    _dataAkceptacjiProdukcjaColumnExists = true;
                }

                // Zaktualizuj snapshot do aktualnego stanu
                await SaveOrderSnapshotAsync(cn, vm.Info.Id, "Realizacja");

                // Zaktualizuj DataAkceptacjiProdukcja na teraz (osobna akceptacja dla produkcji)
                var cmd = new SqlCommand("UPDATE dbo.ZamowieniaMieso SET DataAkceptacjiProdukcja = GETDATE() WHERE Id = @Id", cn);
                cmd.Parameters.AddWithValue("@Id", vm.Info.Id);
                await cmd.ExecuteNonQueryAsync();

                MessageBox.Show("Zmiana została przyjęta przez produkcję. Snapshot zaktualizowany.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);

                // Odśwież dane
                await ReloadAllAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas aktualizacji snapshotu:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

        private async Task LoadPrzychodyInfoAsync()
        {
            decimal planPrzychodu = 0m;
            decimal faktPrzychodu = 0m;
            decimal sumaZamowien = 0m;

            try
            {
                // 1. Plan - na podstawie HarmonogramDostaw (masa dekowania * współczynnik tuszki)
                using (var cn = new SqlConnection(_connLibra))
                {
                    await cn.OpenAsync();
                    var cmd = new SqlCommand(@"SELECT ISNULL(SUM(WagaDek * SztukiDek), 0)
                                               FROM dbo.HarmonogramDostaw
                                               WHERE DataOdbioru = @D AND Bufor = 'Potwierdzony'", cn);
                    cmd.Parameters.AddWithValue("@D", _selectedDate.Date);
                    var result = await cmd.ExecuteScalarAsync();
                    decimal sumaMasyDek = result != DBNull.Value ? Convert.ToDecimal(result) : 0m;
                    // Współczynnik tuszki ~78%
                    planPrzychodu = sumaMasyDek * 0.78m;
                }

                // 2. Faktyczny przychód - z dokumentów sPWU (produkcja)
                using (var cn = new SqlConnection(_connHandel))
                {
                    await cn.OpenAsync();
                    var cmd = new SqlCommand(@"SELECT ISNULL(SUM(ABS(MZ.ilosc)), 0)
                                               FROM [HANDEL].[HM].[MZ] MZ
                                               JOIN [HANDEL].[HM].[MG] ON MZ.super = MG.id
                                               WHERE MG.seria = 'sPWU' AND MG.aktywny = 1 AND MG.data = @D", cn);
                    cmd.Parameters.AddWithValue("@D", _selectedDate.Date);
                    var result = await cmd.ExecuteScalarAsync();
                    faktPrzychodu = result != DBNull.Value ? Convert.ToDecimal(result) : 0m;
                }

                // 3. Suma zamówień na dzień
                sumaZamowien = _zamowienia.Values
                    .Where(z => !z.IsShipmentOnly)
                    .Sum(z => z.TotalIlosc);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd ładowania przychodu: {ex.Message}");
            }

            // Bilans = faktyczny przychód - zamówienia
            decimal bilans = faktPrzychodu - sumaZamowien;

            // Aktualizuj UI
            lblPrzychPlan.Text = planPrzychodu > 0 ? $"{planPrzychodu:N0}" : "—";
            lblPrzychFakt.Text = faktPrzychodu > 0 ? $"{faktPrzychodu:N0}" : "—";
            lblPrzychZam.Text = sumaZamowien > 0 ? $"{sumaZamowien:N0}" : "—";
            lblPrzychBilans.Text = bilans != 0 ? $"{bilans:N0}" : "—";

            // Kolor bilansu
            if (bilans > 0)
                lblPrzychBilans.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50")); // zielony - nadwyżka
            else if (bilans < 0)
                lblPrzychBilans.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF5350")); // czerwony - niedobór
            else
                lblPrzychBilans.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CE93D8")); // neutralny
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
            // Simple coloring - no time-based urgency colors
            if (item.Info.IsShipmentOnly)
            {
                item.RowColor = new SolidColorBrush(Color.FromRgb(80, 58, 32));
                item.TextColor = Brushes.Gold;
            }
            else
            {
                item.RowColor = Brushes.Transparent;
                item.TextColor = Brushes.White;
            }
        }

        private void UpdateProgressInfo()
        {
            int total = _zamowienia.Values.Count(z => !z.IsShipmentOnly);
            int realized = _zamowienia.Values.Count(z => !z.IsShipmentOnly && z.CzyZrealizowane);
            int issued = _zamowienia.Values.Count(z => !z.IsShipmentOnly && z.CzyWydane);
            decimal sumaKg = _zamowienia.Values.Where(z => !z.IsShipmentOnly).Sum(z => z.TotalIlosc);

            if (total == 0)
            {
                lblZrealizowanoCount.Text = "0";
                lblZrealizowanoPercent.Text = "0%";
                lblWydanoCount.Text = "0";
                lblWydanoPercent.Text = "0%";
                // Statystyki tab
                lblStatZamowienia.Text = "0";
                lblStatZrealizowane.Text = "0";
                lblStatZrealProc.Text = "0%";
                lblStatWydane.Text = "0";
                lblStatWydaneProc.Text = "0%";
                lblStatSumaKg.Text = "0";
                lblStatSrednia.Text = "0";
                return;
            }

            double realizedPercent = (double)realized / total * 100;
            double issuedPercent = (double)issued / total * 100;
            decimal srednia = sumaKg / total;

            // Panel główny
            lblZrealizowanoCount.Text = realized.ToString();
            lblZrealizowanoPercent.Text = $"{realizedPercent:F0}%";
            lblWydanoCount.Text = issued.ToString();
            lblWydanoPercent.Text = $"{issuedPercent:F0}%";

            // Statystyki tab
            lblStatZamowienia.Text = total.ToString();
            lblStatZrealizowane.Text = realized.ToString();
            lblStatZrealProc.Text = $"{realizedPercent:F0}%";
            lblStatWydane.Text = issued.ToString();
            lblStatWydaneProc.Text = $"{issuedPercent:F0}%";
            lblStatSumaKg.Text = $"{sumaKg:N0}";
            lblStatSrednia.Text = $"{srednia:N0}";
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
        private async Task MarkOrderRealizedWithNoteAsync()
        {
            var orderId = GetSelectedOrderId();
            if (!orderId.HasValue) return;

            var selected = SelectedZamowienie;
            if (selected == null) return;

            // Pobierz pozycje zamówienia
            var items = new ObservableCollection<RealizationItem>();
            try
            {
                using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                // Najpierw upewnij się że kolumny istnieją
                await EnsurePartialRealizationColumnsAsync(cn);

                var cmd = new SqlCommand(@"SELECT t.KodTowaru, t.Ilosc, ISNULL(t.IloscZrealizowana, t.Ilosc) AS IloscZreal, ISNULL(t.PowodBraku, '') AS Powod
                                           FROM dbo.ZamowieniaMiesoTowar t WHERE t.ZamowienieId = @Id", cn);
                cmd.Parameters.AddWithValue("@Id", orderId.Value);

                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    int kodTowaru = rd.GetInt32(0);
                    string nazwa = _produktLookup.TryGetValue(kodTowaru, out var n) ? n : $"ID: {kodTowaru}";
                    items.Add(new RealizationItem
                    {
                        KodTowaru = kodTowaru,
                        NazwaTowaru = nazwa,
                        IloscZamowiona = rd.GetDecimal(1),
                        IloscZrealizowana = rd.GetDecimal(2),
                        PowodBraku = rd.GetString(3)
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd pobierania pozycji zamówienia:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (items.Count == 0)
            {
                MessageBox.Show("Brak pozycji w zamówieniu.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // === DIALOG REALIZACJI ===
            var dialog = new Window
            {
                Title = $"📋 Realizacja zamówienia: {selected.Info.Klient}",
                Width = 750,
                Height = 550,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D2D30")),
                ResizeMode = ResizeMode.CanResize,
                MinWidth = 600,
                MinHeight = 400
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // DataGrid z pozycjami
            var dgItems = new DataGrid
            {
                ItemsSource = items,
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E1E")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(15, 15, 15, 10),
                RowHeight = 40,
                FontSize = 14,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#444")),
                RowBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A2A2E")),
                AlternatingRowBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#323236")),
                HeadersVisibility = DataGridHeadersVisibility.Column,
                ColumnHeaderHeight = 35
            };

            // Style dla nagłówków kolumn (ciemne)
            var headerStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
            headerStyle.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A3A3E"))));
            headerStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
            headerStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
            headerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 5, 8, 5)));
            headerStyle.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#555"))));
            headerStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
            dgItems.ColumnHeaderStyle = headerStyle;

            // Style dla komórek (ciemne)
            var cellStyle = new Style(typeof(DataGridCell));
            cellStyle.Setters.Add(new Setter(DataGridCell.BackgroundProperty, Brushes.Transparent));
            cellStyle.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0)));
            cellStyle.Setters.Add(new Setter(DataGridCell.ForegroundProperty, Brushes.White));
            dgItems.CellStyle = cellStyle;

            // Kolumna: Produkt (readonly) - krótsza
            dgItems.Columns.Add(new DataGridTextColumn
            {
                Header = "Produkt",
                Binding = new System.Windows.Data.Binding("NazwaTowaru"),
                Width = new DataGridLength(110),
                IsReadOnly = true,
                ElementStyle = new Style(typeof(TextBlock)) { Setters = { new Setter(TextBlock.ForegroundProperty, Brushes.White), new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis) } }
            });

            // Kolumna: Zamówiono (readonly)
            dgItems.Columns.Add(new DataGridTextColumn
            {
                Header = "Zamówiono",
                Binding = new System.Windows.Data.Binding("IloscZamowiona") { StringFormat = "N0" },
                Width = new DataGridLength(90),
                IsReadOnly = true,
                ElementStyle = new Style(typeof(TextBlock)) { Setters = { new Setter(TextBlock.ForegroundProperty, Brushes.White), new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right) } }
            });

            // Kolumna: Zrealizowano (editable)
            dgItems.Columns.Add(new DataGridTextColumn
            {
                Header = "Zrealizowano",
                Binding = new System.Windows.Data.Binding("IloscZrealizowana") { StringFormat = "N0", UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged },
                Width = new DataGridLength(100),
                ElementStyle = new Style(typeof(TextBlock)) { Setters = { new Setter(TextBlock.ForegroundProperty, Brushes.LimeGreen), new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right), new Setter(TextBlock.FontWeightProperty, FontWeights.Bold) } },
                EditingElementStyle = new Style(typeof(TextBox)) { Setters = { new Setter(TextBox.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A3A1A"))), new Setter(TextBox.ForegroundProperty, Brushes.White), new Setter(TextBox.FontSizeProperty, 14.0) } }
            });

            // Kolumna: Różnica (readonly, calculated)
            var roznicaCol = new DataGridTextColumn
            {
                Header = "Różnica",
                Binding = new System.Windows.Data.Binding("Roznica") { StringFormat = "N0" },
                Width = new DataGridLength(80),
                IsReadOnly = true
            };
            var roznicaStyle = new Style(typeof(TextBlock));
            roznicaStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
            roznicaStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Bold));
            roznicaStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, new System.Windows.Data.Binding("RoznicaColor")));
            roznicaCol.ElementStyle = roznicaStyle;
            dgItems.Columns.Add(roznicaCol);

            // Kolumna: Powód braku (editable)
            dgItems.Columns.Add(new DataGridTextColumn
            {
                Header = "Powód braku",
                Binding = new System.Windows.Data.Binding("PowodBraku") { UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged },
                Width = new DataGridLength(180),
                ElementStyle = new Style(typeof(TextBlock)) { Setters = { new Setter(TextBlock.ForegroundProperty, Brushes.Orange) } },
                EditingElementStyle = new Style(typeof(TextBox)) { Setters = { new Setter(TextBox.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A2A1A"))), new Setter(TextBox.ForegroundProperty, Brushes.White) } }
            });

            Grid.SetRow(dgItems, 0);
            mainGrid.Children.Add(dgItems);

            // Podsumowanie
            var summaryPanel = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A3A5A")),
                Margin = new Thickness(15, 0, 15, 10),
                Padding = new Thickness(15, 10, 15, 10),
                CornerRadius = new CornerRadius(5)
            };
            var summaryStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            var lblSummary = new TextBlock { Foreground = Brushes.White, FontSize = 16, FontWeight = FontWeights.Bold };

            // Aktualizacja podsumowania
            void UpdateSummary()
            {
                decimal totalOrdered = items.Sum(i => i.IloscZamowiona);
                decimal totalRealized = items.Sum(i => i.IloscZrealizowana);
                decimal percent = totalOrdered > 0 ? (totalRealized / totalOrdered) * 100 : 100;
                lblSummary.Text = $"Podsumowanie: {totalRealized:N0} kg / {totalOrdered:N0} kg ({percent:N0}%)";
                lblSummary.Foreground = percent >= 100 ? Brushes.LimeGreen : (percent >= 80 ? Brushes.Yellow : Brushes.OrangeRed);
            }

            foreach (var item in items)
            {
                item.PropertyChanged += (s, e) => UpdateSummary();
            }
            UpdateSummary();

            summaryStack.Children.Add(lblSummary);
            summaryPanel.Child = summaryStack;
            Grid.SetRow(summaryPanel, 1);
            mainGrid.Children.Add(summaryPanel);

            // Notatka produkcji
            var notePanel = new StackPanel { Margin = new Thickness(15, 0, 15, 10) };
            notePanel.Children.Add(new TextBlock { Text = "Notatka produkcji (opcjonalna):", Foreground = Brushes.White, FontSize = 13, Margin = new Thickness(0, 0, 0, 5) });
            var txtNote = new TextBox
            {
                Height = 50,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E1E")),
                Foreground = Brushes.White,
                BorderBrush = Brushes.Gray
            };
            notePanel.Children.Add(txtNote);
            Grid.SetRow(notePanel, 2);
            mainGrid.Children.Add(notePanel);

            // Przyciski
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(15, 0, 15, 15) };
            var btnOk = new Button
            {
                Content = "✓ Zatwierdź realizację",
                Width = 180,
                Height = 40,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#19874B")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand
            };
            var btnCancel = new Button
            {
                Content = "Anuluj",
                Width = 100,
                Height = 40,
                FontSize = 14,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#555")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };

            btnOk.Click += (s, e) => { dialog.DialogResult = true; dialog.Close(); };
            btnCancel.Click += (s, e) => { dialog.DialogResult = false; dialog.Close(); };

            btnPanel.Children.Add(btnOk);
            btnPanel.Children.Add(btnCancel);
            Grid.SetRow(btnPanel, 3);
            mainGrid.Children.Add(btnPanel);

            dialog.Content = mainGrid;

            if (dialog.ShowDialog() != true) return;

            // === ZAPISZ REALIZACJĘ ===
            string note = txtNote.Text?.Trim() ?? "";
            decimal totalOrdered = items.Sum(i => i.IloscZamowiona);
            decimal totalRealized = items.Sum(i => i.IloscZrealizowana);
            decimal percentRealized = totalOrdered > 0 ? (totalRealized / totalOrdered) * 100 : 100;
            bool isPartial = items.Any(i => i.IloscZrealizowana < i.IloscZamowiona);

            using var cnSave = new SqlConnection(_connLibra);
            await cnSave.OpenAsync();

            // Upewnij się że kolumny istnieją
            await EnsurePartialRealizationColumnsAsync(cnSave);

            // Zapisz ilości zrealizowane per produkt
            foreach (var item in items)
            {
                var cmdItem = new SqlCommand(@"UPDATE dbo.ZamowieniaMiesoTowar
                                               SET IloscZrealizowana = @Zreal, PowodBraku = @Powod
                                               WHERE ZamowienieId = @ZamId AND KodTowaru = @Kod", cnSave);
                cmdItem.Parameters.AddWithValue("@Zreal", item.IloscZrealizowana);
                cmdItem.Parameters.AddWithValue("@Powod", string.IsNullOrEmpty(item.PowodBraku) ? (object)DBNull.Value : item.PowodBraku);
                cmdItem.Parameters.AddWithValue("@ZamId", orderId.Value);
                cmdItem.Parameters.AddWithValue("@Kod", item.KodTowaru);
                await cmdItem.ExecuteNonQueryAsync();
            }

            // Zapisz notatkę produkcji jeśli podano
            if (!string.IsNullOrEmpty(note))
            {
                await EnsureNotesTableAsync();
                var cmdNote = new SqlCommand(@"IF EXISTS (SELECT 1 FROM dbo.ZamowieniaMiesoProdukcjaNotatki WHERE ZamowienieId = @Id)
                                               UPDATE dbo.ZamowieniaMiesoProdukcjaNotatki SET NotatkaProdukcja = @N WHERE ZamowienieId = @Id
                                               ELSE INSERT INTO dbo.ZamowieniaMiesoProdukcjaNotatki (ZamowienieId, NotatkaProdukcja) VALUES (@Id, @N)", cnSave);
                cmdNote.Parameters.AddWithValue("@Id", orderId.Value);
                cmdNote.Parameters.AddWithValue("@N", note);
                await cmdNote.ExecuteNonQueryAsync();
            }

            // Zapisz snapshot pozycji zamówienia
            await SaveOrderSnapshotAsync(cnSave, orderId.Value, "Realizacja");

            // Oznacz jako zrealizowane (z informacją o częściowej realizacji)
            var cmdUpdate = new SqlCommand(@"UPDATE dbo.ZamowieniaMieso
                                       SET CzyZrealizowane = 1,
                                           CzyCzesciowoZrealizowane = @Partial,
                                           ProcentRealizacji = @Percent,
                                           DataRealizacji = GETDATE(),
                                           KtoZrealizowal = @UserID,
                                           Status = CASE
                                               WHEN @Partial = 1 THEN 'Częściowo zrealizowane'
                                               WHEN CzyWydane = 1 THEN 'Wydany'
                                               ELSE 'Zrealizowane'
                                           END
                                       WHERE Id = @I", cnSave);
            cmdUpdate.Parameters.AddWithValue("@I", orderId.Value);
            cmdUpdate.Parameters.AddWithValue("@Partial", isPartial);
            cmdUpdate.Parameters.AddWithValue("@Percent", percentRealized);
            int.TryParse(UserID, out int userId);
            cmdUpdate.Parameters.AddWithValue("@UserID", userId > 0 ? userId : (object)DBNull.Value);
            await cmdUpdate.ExecuteNonQueryAsync();

            string msg = isPartial
                ? $"Zamówienie częściowo zrealizowane ({percentRealized:N0}%)"
                : "Zamówienie w pełni zrealizowane!";
            MessageBox.Show(msg, "Realizacja", MessageBoxButton.OK, isPartial ? MessageBoxImage.Warning : MessageBoxImage.Information);

            await LoadOrdersAsync();
        }

        private async Task EnsurePartialRealizationColumnsAsync(SqlConnection cn)
        {
            // Sprawdź i dodaj kolumny do ZamowieniaMiesoTowar
            var checkCmd = new SqlCommand(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ZamowieniaMiesoTowar') AND name = 'IloscZrealizowana')
                    ALTER TABLE dbo.ZamowieniaMiesoTowar ADD IloscZrealizowana DECIMAL(18,2) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ZamowieniaMiesoTowar') AND name = 'PowodBraku')
                    ALTER TABLE dbo.ZamowieniaMiesoTowar ADD PowodBraku NVARCHAR(500) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ZamowieniaMieso') AND name = 'ProcentRealizacji')
                    ALTER TABLE dbo.ZamowieniaMieso ADD ProcentRealizacji DECIMAL(5,2) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ZamowieniaMieso') AND name = 'CzyCzesciowoZrealizowane')
                    ALTER TABLE dbo.ZamowieniaMieso ADD CzyCzesciowoZrealizowane BIT DEFAULT 0;
            ", cn);
            await checkCmd.ExecuteNonQueryAsync();
            // Zresetuj cache po utworzeniu kolumn
            _partialRealizationColumnsExist = true;
        }

        private async Task MarkOrderRealizedAsync()
        {
            var orderId = GetSelectedOrderId();
            if (!orderId.HasValue) return;
            if (MessageBox.Show("Oznaczyć zamówienie jako zrealizowane?", "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            // Ustaw CzyZrealizowane + DataRealizacji + KtoZrealizowal + Status (dla kompatybilności)
            var cmd = new SqlCommand(@"UPDATE dbo.ZamowieniaMieso
                                       SET CzyZrealizowane = 1,
                                           DataRealizacji = GETDATE(),
                                           KtoZrealizowal = @UserID,
                                           Status = CASE WHEN CzyWydane = 1 THEN 'Wydany' ELSE 'Zrealizowane' END
                                       WHERE Id = @I", cn);
            cmd.Parameters.AddWithValue("@I", orderId.Value);
            int.TryParse(UserID, out int userId);
            cmd.Parameters.AddWithValue("@UserID", userId > 0 ? userId : (object)DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
            await LoadOrdersAsync();
        }

        private async Task UndoRealizedAsync()
        {
            var orderId = GetSelectedOrderId();
            if (!orderId.HasValue) return;
            if (MessageBox.Show("Cofnąć realizację?\n\nWartości zrealizowanych ilości zostaną usunięte.", "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();

            // Wyczyść wartości realizacji z pozycji zamówienia
            var cmdClearItems = new SqlCommand(@"UPDATE dbo.ZamowieniaMiesoTowar
                                                 SET IloscZrealizowana = NULL, PowodBraku = NULL
                                                 WHERE ZamowienieId = @I", cn);
            cmdClearItems.Parameters.AddWithValue("@I", orderId.Value);
            await cmdClearItems.ExecuteNonQueryAsync();

            // Cofnij realizację zamówienia
            var cmd = new SqlCommand(@"UPDATE dbo.ZamowieniaMieso
                                       SET CzyZrealizowane = 0,
                                           CzyCzesciowoZrealizowane = 0,
                                           ProcentRealizacji = NULL,
                                           DataRealizacji = NULL,
                                           KtoZrealizowal = NULL,
                                           DataAkceptacjiProdukcja = NULL,
                                           Status = CASE WHEN CzyWydane = 1 THEN 'Wydany' ELSE 'Nowe' END
                                       WHERE Id = @I", cn);
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

        private bool _snapshotTableEnsured = false;

        private async Task EnsureSnapshotTableAsync(SqlConnection cn)
        {
            if (_snapshotTableEnsured) return;
            try
            {
                var cmd = new SqlCommand(@"
                    IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name='ZamowieniaMiesoSnapshot' AND type='U')
                    BEGIN
                        CREATE TABLE dbo.ZamowieniaMiesoSnapshot (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            ZamowienieId INT NOT NULL,
                            KodTowaru INT NOT NULL,
                            Ilosc DECIMAL(18,3) NOT NULL,
                            Folia BIT NULL,
                            Hallal BIT NULL,
                            DataSnapshotu DATETIME NOT NULL DEFAULT GETDATE(),
                            TypSnapshotu NVARCHAR(20) NOT NULL
                        );
                        CREATE INDEX IX_Snapshot_ZamowienieId ON dbo.ZamowieniaMiesoSnapshot(ZamowienieId);
                    END;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ZamowieniaMieso') AND name = 'DataOstatniejModyfikacji')
                        ALTER TABLE dbo.ZamowieniaMieso ADD DataOstatniejModyfikacji DATETIME NULL;", cn);
                await cmd.ExecuteNonQueryAsync();
                _snapshotTableEnsured = true;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Błąd tworzenia tabeli snapshotów: {ex.Message}"); }
        }

        private async Task SaveOrderSnapshotAsync(SqlConnection cn, int zamowienieId, string typSnapshotu)
        {
            try
            {
                await EnsureSnapshotTableAsync(cn);

                // Usuń stary snapshot tego samego typu
                var cmdDelete = new SqlCommand(@"DELETE FROM dbo.ZamowieniaMiesoSnapshot WHERE ZamowienieId = @ZamId AND TypSnapshotu = @Typ", cn);
                cmdDelete.Parameters.AddWithValue("@ZamId", zamowienieId);
                cmdDelete.Parameters.AddWithValue("@Typ", typSnapshotu);
                await cmdDelete.ExecuteNonQueryAsync();

                // Zapisz nowy snapshot
                var cmdInsert = new SqlCommand(@"
                    INSERT INTO dbo.ZamowieniaMiesoSnapshot (ZamowienieId, KodTowaru, Ilosc, Folia, Hallal, TypSnapshotu)
                    SELECT ZamowienieId, KodTowaru, Ilosc, Folia, Hallal, @Typ
                    FROM dbo.ZamowieniaMiesoTowar
                    WHERE ZamowienieId = @ZamId", cn);
                cmdInsert.Parameters.AddWithValue("@ZamId", zamowienieId);
                cmdInsert.Parameters.AddWithValue("@Typ", typSnapshotu);
                await cmdInsert.ExecuteNonQueryAsync();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Błąd zapisywania snapshotu: {ex.Message}"); }
        }

        private async Task<Dictionary<int, (decimal Ilosc, bool Folia)>> GetOrderSnapshotAsync(int zamowienieId, string typSnapshotu)
        {
            var snapshot = new Dictionary<int, (decimal Ilosc, bool Folia)>();
            try
            {
                using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                var cmd = new SqlCommand(@"SELECT KodTowaru, Ilosc, ISNULL(Folia, 0)
                                           FROM dbo.ZamowieniaMiesoSnapshot
                                           WHERE ZamowienieId = @ZamId AND TypSnapshotu = @Typ", cn);
                cmd.Parameters.AddWithValue("@ZamId", zamowienieId);
                cmd.Parameters.AddWithValue("@Typ", typSnapshotu);

                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    snapshot[rd.GetInt32(0)] = (rd.GetDecimal(1), rd.GetBoolean(2));
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Błąd pobierania snapshotu: {ex.Message}"); }
            return snapshot;
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
            public bool CzyZrealizowane { get; set; }
            public bool CzyWydane { get; set; }
            public bool MaHalal { get; set; }
            public bool WlasnyTransport { get; set; }
            public DateTime? DataPrzyjazdu { get; set; }
            public string Kierowca { get; set; } = "";
            // Nowe pola do wykrywania zmian
            public DateTime? DataOstatniejModyfikacji { get; set; }
            public DateTime? DataRealizacji { get; set; }
            public DateTime? DataAkceptacjiProdukcja { get; set; } // Osobna akceptacja produkcji
            public bool CzyZmodyfikowaneOdRealizacji { get; set; }

            // Pola częściowej realizacji
            public bool CzyCzesciowoZrealizowane { get; set; }
            public decimal? ProcentRealizacji { get; set; }
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

        // Klasa dla pozycji w dialogu realizacji
        public class RealizationItem : INotifyPropertyChanged
        {
            public int KodTowaru { get; set; }
            public string NazwaTowaru { get; set; } = "";
            public decimal IloscZamowiona { get; set; }

            private decimal _iloscZrealizowana;
            public decimal IloscZrealizowana
            {
                get => _iloscZrealizowana;
                set { _iloscZrealizowana = value; OnPropertyChanged(); OnPropertyChanged(nameof(Roznica)); OnPropertyChanged(nameof(RoznicaColor)); }
            }

            private string _powodBraku = "";
            public string PowodBraku
            {
                get => _powodBraku;
                set { _powodBraku = value; OnPropertyChanged(); }
            }

            public decimal Roznica => IloscZrealizowana - IloscZamowiona;
            public Brush RoznicaColor => Roznica < 0 ? Brushes.OrangeRed : (Roznica > 0 ? Brushes.LimeGreen : Brushes.White);

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public class ZamowienieViewModel : INotifyPropertyChanged
        {
            public ZamowienieInfo Info { get; }
            public ZamowienieViewModel(ZamowienieInfo info) { Info = info; }

            // Ikony przy nazwie klienta (bez ⚠️ - zamiast tego żółta czcionka)
            public string Klient => $"{(Info.MaNotatke ? "📝 " : "")}{(Info.MaFolie ? "🎞️ " : "")}{(Info.MaHalal ? "🔪 " : "")}{(Info.WlasnyTransport ? "🚚 " : "")}{Info.Klient}";

            // Kolor nazwy klienta - żółty gdy zamówienie zostało zmodyfikowane
            public Brush KlientColor => Info.CzyZmodyfikowaneOdRealizacji ? Brushes.Yellow : Brushes.White;

            // Wyświetlanie kierowcy
            public string KierowcaDisplay => Info.WlasnyTransport ? "Własny odbiór" : (string.IsNullOrEmpty(Info.Kierowca) ? "Brak" : Info.Kierowca);

            public decimal TotalIlosc => Info.TotalIlosc;
            public string Handlowiec => Info.Handlowiec;

            // Wyświetlanie zrealizowanej ilości (tylko gdy częściowo zrealizowane)
            public string ZrealizowanoDisplay => Info.CzyCzesciowoZrealizowane && Info.ProcentRealizacji.HasValue
                ? $"{Info.ProcentRealizacji:N0}%"
                : "";

            // Widoczność kolumny Zrealizowano
            public bool ShowZrealizowano => Info.CzyCzesciowoZrealizowane;

            // Kombinowany status
            public string Status
            {
                get
                {
                    if (Info.IsShipmentOnly) return "Symfonia";
                    // Jeśli jest zmodyfikowane od realizacji - pokaż "Do zaakceptowania"
                    if (Info.CzyZmodyfikowaneOdRealizacji) return "⚠ Do zaakceptowania";
                    // Częściowa realizacja
                    if (Info.CzyCzesciowoZrealizowane && Info.CzyWydane)
                        return $"⚠ Częśc. ({Info.ProcentRealizacji:N0}%) + Wyd.";
                    if (Info.CzyCzesciowoZrealizowane)
                        return $"⚠ Częśc. zreal. ({Info.ProcentRealizacji:N0}%)";
                    if (Info.CzyWydane && Info.CzyZrealizowane) return "✓ Zreal. + Wydane";
                    if (Info.CzyWydane && !Info.CzyZrealizowane) return "⚠ Tylko wydane";
                    if (Info.CzyZrealizowane) return "✓ Zrealizowane";
                    return "Nowe";
                }
            }

            // Kolor statusu
            public Brush StatusColor
            {
                get
                {
                    // Żółty dla statusu "Do zaakceptowania"
                    if (Info.CzyZmodyfikowaneOdRealizacji) return Brushes.Yellow;
                    // Pomarańczowy dla częściowej realizacji
                    if (Info.CzyCzesciowoZrealizowane) return Brushes.Orange;
                    if (Info.CzyWydane && Info.CzyZrealizowane) return Brushes.LimeGreen;
                    if (Info.CzyWydane && !Info.CzyZrealizowane) return Brushes.Orange;
                    if (Info.CzyZrealizowane) return Brushes.LightGreen;
                    return Brushes.Gray;
                }
            }

            public string CzasWyjazdDisplay
            {
                get
                {
                    // Własny transport - car icon + time
                    if (Info.WlasnyTransport && Info.DataPrzyjazdu.HasValue)
                        return $"🚗 {Info.DataPrzyjazdu.Value:HH:mm} {Info.DataPrzyjazdu.Value.ToString("dddd", new CultureInfo("pl-PL"))}";
                    if (Info.WlasnyTransport)
                        return "🚗 Własny";
                    // Regular transport - car icon + time
                    if (Info.CzasWyjazdu.HasValue && Info.DataKursu.HasValue)
                        return $"🚗 {Info.CzasWyjazdu.Value:hh\\:mm} {Info.DataKursu.Value.ToString("dddd", new CultureInfo("pl-PL"))}";
                    if (Info.IsShipmentOnly)
                        return "Nie zrobiono zamówienia";
                    return "Brak kursu";
                }
            }
            public DateTime SortDateTime
            {
                get
                {
                    if (Info.WlasnyTransport && Info.DataPrzyjazdu.HasValue)
                        return Info.DataPrzyjazdu.Value;
                    if (Info.CzasWyjazdu.HasValue && Info.DataKursu.HasValue)
                        return Info.DataKursu.Value.Add(Info.CzasWyjazdu.Value);
                    return DateTime.MaxValue;
                }
            }

            private Brush _rowColor = Brushes.Transparent;
            public Brush RowColor { get => _rowColor; set { _rowColor = value; OnPropertyChanged(); } }

            private Brush _textColor = Brushes.White;
            public Brush TextColor { get => _textColor; set { _textColor = value; OnPropertyChanged(); } }

            // Wyświetlanie ostatniej zmiany
            public string OstatniaZmianaDisplay
            {
                get
                {
                    if (!Info.CzyZrealizowane) return "-";
                    if (!Info.CzyZmodyfikowaneOdRealizacji) return "✓ OK";
                    if (Info.DataOstatniejModyfikacji.HasValue)
                        return $"⚠️ {Info.DataOstatniejModyfikacji.Value:HH:mm}";
                    return "⚠️ Zmiana";
                }
            }

            public Brush ZmianaColor => Info.CzyZmodyfikowaneOdRealizacji ? Brushes.Orange : Brushes.LimeGreen;

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        #endregion
    }
}

