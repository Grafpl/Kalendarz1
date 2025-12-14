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
using System.Windows.Media;

namespace Kalendarz1.WPF
{
    public partial class PanelFakturWindow : Window, INotifyPropertyChanged
    {
        private readonly string _connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly string _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private readonly string _connTransport = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public string UserID { get; set; } = string.Empty;

        private DateTime _selectedDate;
        private int? _currentOrderId;
        private bool _showFakturowane = false;
        private readonly DataTable _dtDetails = new();
        private readonly List<Button> _dayButtons = new();
        private readonly Dictionary<Button, DateTime> _dayButtonDates = new();
        private readonly Dictionary<int, (string Name, string Salesman)> _contractorsCache = new();
        private readonly Dictionary<int, string> _productsCache = new();
        private int? _selectedProductId = null;
        private bool _isRefreshing = false;
        private bool _pendingRefresh = false;

        public ObservableCollection<ZamowienieViewModel> ZamowieniaList { get; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public PanelFakturWindow()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += PanelFakturWindow_Loaded;
        }

        private async void PanelFakturWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _selectedDate = DateTime.Today;
            SetupDayButtons();
            await LoadContractorsCacheAsync();
            await LoadProductsCacheAsync();
            await RefreshDataAsync();
        }

        private void SetupDayButtons()
        {
            panelDays.Children.Clear();
            _dayButtons.Clear();
            _dayButtonDates.Clear();

            string[] dayNames = { "Pon", "Wt", "Śr", "Czw", "Pt", "Sob", "Nd" };

            // Przycisk Dziś
            var btnToday = new Button
            {
                Content = new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = "Dziś", FontSize = 10, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center },
                        new TextBlock { Text = DateTime.Today.ToString("dd.MM"), FontSize = 9, HorizontalAlignment = HorizontalAlignment.Center }
                    }
                },
                Style = (Style)FindResource("DayButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(25, 118, 210)),
                Foreground = Brushes.White
            };
            btnToday.Click += (s, e) =>
            {
                _selectedDate = DateTime.Today;
                UpdateDayButtonDates();
                _ = RefreshDataAsync();
            };
            panelDays.Children.Add(btnToday);

            // Separator
            panelDays.Children.Add(new Separator { Width = 2, Margin = new Thickness(5, 0, 5, 0) });

            // Przyciski dni tygodnia
            for (int i = 0; i < 7; i++)
            {
                var btn = new Button { Style = (Style)FindResource("DayButtonStyle") };
                var stack = new StackPanel();
                stack.Children.Add(new TextBlock { Text = dayNames[i], FontSize = 9, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center });
                stack.Children.Add(new TextBlock { Text = DateTime.Today.AddDays(i).ToString("dd.MM"), FontSize = 9, HorizontalAlignment = HorizontalAlignment.Center });
                btn.Content = stack;
                btn.Click += DayButton_Click;
                _dayButtonDates[btn] = DateTime.Today.AddDays(i);
                _dayButtons.Add(btn);
                panelDays.Children.Add(btn);
            }

            UpdateDayButtonDates();
        }

        private void UpdateDayButtonDates()
        {
            int delta = ((int)_selectedDate.DayOfWeek + 6) % 7;
            DateTime startOfWeek = _selectedDate.AddDays(-delta);

            string[] dayNames = { "Pon", "Wt", "Śr", "Czw", "Pt", "Sob", "Nd" };

            for (int i = 0; i < _dayButtons.Count; i++)
            {
                var date = startOfWeek.AddDays(i);
                _dayButtonDates[_dayButtons[i]] = date;

                var stack = new StackPanel();
                stack.Children.Add(new TextBlock
                {
                    Text = dayNames[i],
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                stack.Children.Add(new TextBlock
                {
                    Text = date.ToString("dd.MM"),
                    FontSize = 9,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                _dayButtons[i].Content = stack;

                if (date.Date == _selectedDate.Date)
                {
                    _dayButtons[i].Background = new SolidColorBrush(Color.FromRgb(25, 118, 210));
                    _dayButtons[i].Foreground = Brushes.White;
                }
                else if (date.Date == DateTime.Today)
                {
                    _dayButtons[i].Background = new SolidColorBrush(Color.FromRgb(200, 230, 255));
                    _dayButtons[i].Foreground = Brushes.Black;
                }
                else
                {
                    _dayButtons[i].Background = Brushes.White;
                    _dayButtons[i].Foreground = Brushes.Black;
                }
            }
        }

        private async void DayButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && _dayButtonDates.TryGetValue(btn, out var date))
            {
                _selectedDate = date;
                UpdateDayButtonDates();
                await RefreshDataAsync();
            }
        }

        private void BtnPrevWeek_Click(object sender, RoutedEventArgs e)
        {
            _selectedDate = _selectedDate.AddDays(-7);
            UpdateDayButtonDates();
            _ = RefreshDataAsync();
        }

        private void BtnNextWeek_Click(object sender, RoutedEventArgs e)
        {
            _selectedDate = _selectedDate.AddDays(7);
            UpdateDayButtonDates();
            _ = RefreshDataAsync();
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await RefreshDataAsync();
        }

        private async Task LoadContractorsCacheAsync()
        {
            _contractorsCache.Clear();
            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                const string sql = @"SELECT c.Id, c.Shortcut, wym.CDim_Handlowiec_Val
                                     FROM [HANDEL].[SSCommon].[STContractors] c
                                     LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] wym ON c.Id = wym.ElementId";
                await using var cmd = new SqlCommand(sql, cn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    int id = reader.GetInt32(0);
                    string shortcut = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    string salesman = reader.IsDBNull(2) ? "" : reader.GetString(2);
                    _contractorsCache[id] = (shortcut, salesman);
                }
            }
            catch { }
        }

        private async Task LoadProductsCacheAsync()
        {
            _productsCache.Clear();
            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                // Pobierz tylko produkty z katalogów mięsnych (Świeże=67095, Mrożone=67153)
                const string sql = @"SELECT ID, kod FROM [HANDEL].[HM].[TW]
                                     WHERE katalog IN (67095, 67153)
                                     ORDER BY kod";
                await using var cmd = new SqlCommand(sql, cn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    int id = reader.GetInt32(0);
                    string kod = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    if (!string.IsNullOrWhiteSpace(kod))
                        _productsCache[id] = kod;
                }

                GenerateProductButtons();
            }
            catch { }
        }

        private void GenerateProductButtons()
        {
            pnlProductButtons.Children.Clear();

            // Przycisk "Wszystkie"
            var btnAll = new Button
            {
                Content = "Wszystkie",
                Margin = new Thickness(2),
                Padding = new Thickness(10, 5, 10, 5),
                FontSize = 11,
                Background = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = (int?)null
            };
            btnAll.Click += ProductButton_Click;
            pnlProductButtons.Children.Add(btnAll);

            foreach (var product in _productsCache.OrderBy(x => x.Value))
            {
                var btn = new Button
                {
                    Content = product.Value,
                    Margin = new Thickness(2),
                    Padding = new Thickness(10, 5, 10, 5),
                    FontSize = 11,
                    Background = new SolidColorBrush(Color.FromRgb(236, 240, 241)),
                    Foreground = Brushes.Black,
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(189, 195, 199)),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = product.Key
                };
                btn.Click += ProductButton_Click;
                pnlProductButtons.Children.Add(btn);
            }
        }

        private async void ProductButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                // Reset wszystkich przycisków
                foreach (var child in pnlProductButtons.Children.OfType<Button>())
                {
                    if (child.Tag == null)
                    {
                        child.Background = new SolidColorBrush(Color.FromRgb(52, 152, 219));
                        child.Foreground = Brushes.White;
                    }
                    else
                    {
                        child.Background = new SolidColorBrush(Color.FromRgb(236, 240, 241));
                        child.Foreground = Brushes.Black;
                    }
                }

                // Podświetl wybrany
                btn.Background = new SolidColorBrush(Color.FromRgb(46, 204, 113));
                btn.Foreground = Brushes.White;

                _selectedProductId = btn.Tag as int?;
                await RefreshDataAsync();
            }
        }

        private async Task RefreshDataAsync()
        {
            // Zapobiega równoczesnym odświeżeniom które powodują duplikaty
            if (_isRefreshing)
            {
                _pendingRefresh = true;
                return;
            }

            _isRefreshing = true;
            _pendingRefresh = false;

            try
            {
                await LoadOrdersAsync();
                ClearDetails();
            }
            finally
            {
                _isRefreshing = false;

                // Jeśli było żądanie odświeżenia podczas pracy, odśwież ponownie
                if (_pendingRefresh)
                {
                    _pendingRefresh = false;
                    await RefreshDataAsync();
                }
            }
        }

        private async Task LoadOrdersAsync()
        {
            ZamowieniaList.Clear();

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                // Upewnij się że kolumny istnieją
                await EnsureColumnsExistAsync(cn);

                string productFilter = _selectedProductId.HasValue ? "AND zmt.KodTowaru = @ProductId " : "";

                string sql = $@"
                    SELECT zm.Id, zm.KlientId,
                           SUM(ISNULL(zmt.Ilosc, 0)) AS IloscZamowiona,
                           SUM(ISNULL(CAST(zmt.Cena AS decimal(18,2)) * zmt.Ilosc, 0)) AS Wartosc,
                           zm.DataZamowienia, zm.DataUboju, zm.Status, zm.IdUser,
                           ISNULL(zm.CzyZafakturowane, 0) AS CzyZafakturowane,
                           zm.NumerFaktury,
                           zm.TransportKursID,
                           ISNULL(zm.CzyZmodyfikowaneDlaFaktur, 0) AS CzyZmodyfikowaneDlaFaktur,
                           zm.DataOstatniejModyfikacji,
                           zm.ModyfikowalPrzez,
                           CASE WHEN COUNT(zmt.Id) = 0 THEN 0
                                WHEN SUM(CASE WHEN zmt.Cena IS NULL OR zmt.Cena = '' OR CAST(zmt.Cena AS decimal(18,2)) = 0 THEN 1 ELSE 0 END) = 0 THEN 1
                                ELSE 0 END AS CzyMaCeny
                    FROM [dbo].[ZamowieniaMieso] zm
                    LEFT JOIN [dbo].[ZamowieniaMiesoTowar] zmt ON zm.Id = zmt.ZamowienieId
                    WHERE zm.DataUboju = @Day
                      AND zm.Status <> 'Anulowane'
                      {productFilter}
                    GROUP BY zm.Id, zm.KlientId, zm.DataZamowienia, zm.DataUboju, zm.Status, zm.IdUser,
                             zm.CzyZafakturowane, zm.NumerFaktury, zm.TransportKursID,
                             zm.CzyZmodyfikowaneDlaFaktur, zm.DataOstatniejModyfikacji, zm.ModyfikowalPrzez
                    ORDER BY zm.Id";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Day", _selectedDate.Date);
                if (_selectedProductId.HasValue)
                    cmd.Parameters.AddWithValue("@ProductId", _selectedProductId.Value);

                var kursIds = new HashSet<long>();
                var tempList = new List<ZamowienieInfo>();
                var seenIds = new HashSet<int>(); // Zapobiega duplikatom

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    int id = reader.GetInt32(0);

                    // Pomijamy jeśli już widzieliśmy to zamówienie
                    if (!seenIds.Add(id))
                        continue;
                    int clientId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                    decimal ilosc = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2);
                    decimal wartosc = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3);
                    DateTime? dataZam = reader.IsDBNull(4) ? null : reader.GetDateTime(4);
                    DateTime? dataUboju = reader.IsDBNull(5) ? null : reader.GetDateTime(5);
                    string status = reader.IsDBNull(6) ? "" : reader.GetString(6);
                    string idUser = reader.IsDBNull(7) ? "" : reader.GetValue(7).ToString() ?? "";
                    bool czyZafakturowane = !reader.IsDBNull(8) && Convert.ToBoolean(reader.GetValue(8));
                    string numerFaktury = reader.IsDBNull(9) ? "" : reader.GetValue(9).ToString() ?? "";
                    long? transportKursId = reader.IsDBNull(10) ? null : Convert.ToInt64(reader.GetValue(10));
                    bool czyZmodyfikowane = !reader.IsDBNull(11) && Convert.ToBoolean(reader.GetValue(11));
                    DateTime? dataModyfikacji = reader.IsDBNull(12) ? null : reader.GetDateTime(12);
                    string modyfikowalPrzez = reader.IsDBNull(13) ? "" : reader.GetString(13);
                    bool czyMaCeny = !reader.IsDBNull(14) && Convert.ToInt32(reader.GetValue(14)) == 1;

                    var (name, salesman) = _contractorsCache.TryGetValue(clientId, out var c) ? c : ($"Klient {clientId}", "");

                    var info = new ZamowienieInfo
                    {
                        Id = id,
                        KlientId = clientId,
                        Klient = name,
                        Handlowiec = salesman,
                        TotalIlosc = ilosc,
                        Wartosc = wartosc,
                        DataZamowienia = dataZam,
                        DataUboju = dataUboju,
                        Status = status,
                        UtworzonePrzez = idUser,
                        CzyZafakturowane = czyZafakturowane,
                        NumerFaktury = numerFaktury,
                        TransportKursID = transportKursId,
                        CzyZmodyfikowaneDlaFaktur = czyZmodyfikowane,
                        DataOstatniejModyfikacji = dataModyfikacji,
                        ModyfikowalPrzez = modyfikowalPrzez,
                        CzyMaCeny = czyMaCeny
                    };

                    if (transportKursId.HasValue)
                        kursIds.Add(transportKursId.Value);

                    tempList.Add(info);
                }

                // Pobierz info o transporcie
                if (kursIds.Count > 0)
                {
                    var transportInfo = await LoadTransportInfoAsync(kursIds);
                    foreach (var info in tempList)
                    {
                        if (info.TransportKursID.HasValue && transportInfo.TryGetValue(info.TransportKursID.Value, out var ti))
                        {
                            info.GodzWyjazdu = ti.GodzWyjazdu;
                            info.Kierowca = ti.Kierowca;
                            info.Pojazd = ti.Pojazd;
                        }
                    }
                }

                // Filtruj i dodaj do listy
                foreach (var info in tempList)
                {
                    if (!_showFakturowane && info.CzyZafakturowane)
                        continue;

                    ZamowieniaList.Add(new ZamowienieViewModel(info));
                }

                txtOrdersCount.Text = $"{ZamowieniaList.Count} zamówień";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania zamówień: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task<Dictionary<long, (string GodzWyjazdu, string Kierowca, string Pojazd)>> LoadTransportInfoAsync(HashSet<long> kursIds)
        {
            var result = new Dictionary<long, (string GodzWyjazdu, string Kierowca, string Pojazd)>();
            if (kursIds.Count == 0) return result;

            string[] polskieMiesiace = { "", "Sty", "Lut", "Mar", "Kwi", "Maj", "Cze", "Lip", "Sie", "Wrz", "Paź", "Lis", "Gru" };

            try
            {
                await using var cn = new SqlConnection(_connTransport);
                await cn.OpenAsync();

                var kursIdsList = string.Join(",", kursIds);
                string sql = $@"
                    SELECT k.KursID, k.GodzWyjazdu, k.DataKursu,
                           ISNULL(kier.Imie + ' ' + kier.Nazwisko, '') AS Kierowca,
                           ISNULL(p.Rejestracja, '') AS Pojazd
                    FROM dbo.Kurs k
                    LEFT JOIN dbo.Kierowca kier ON k.KierowcaID = kier.KierowcaID
                    LEFT JOIN dbo.Pojazd p ON k.PojazdID = p.PojazdID
                    WHERE k.KursID IN ({kursIdsList})";

                await using var cmd = new SqlCommand(sql, cn);
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    long kursId = reader.GetInt64(0);
                    TimeSpan? godzWyjazdu = reader.IsDBNull(1) ? null : reader.GetTimeSpan(1);
                    DateTime? dataKursu = reader.IsDBNull(2) ? null : reader.GetDateTime(2);
                    string kierowca = reader.IsDBNull(3) ? "" : reader.GetString(3);
                    string pojazd = reader.IsDBNull(4) ? "" : reader.GetString(4);

                    string godzWyjazduStr = "";
                    if (godzWyjazdu.HasValue && dataKursu.HasValue)
                    {
                        string miesiac = polskieMiesiace[dataKursu.Value.Month];
                        godzWyjazduStr = $"{godzWyjazdu.Value:hh\\:mm} {miesiac} {dataKursu.Value.Day}";
                    }
                    else if (godzWyjazdu.HasValue)
                    {
                        godzWyjazduStr = godzWyjazdu.Value.ToString(@"hh\:mm");
                    }

                    result[kursId] = (godzWyjazduStr, kierowca, pojazd);
                }
            }
            catch { }
            return result;
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _ = RefreshDataAsync();
        }

        private void ChkShowFakturowane_Changed(object sender, RoutedEventArgs e)
        {
            _showFakturowane = chkShowFakturowane.IsChecked == true;
            _ = RefreshDataAsync();
        }

        private async void DgOrders_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgOrders.SelectedItem is ZamowienieViewModel vm)
            {
                _currentOrderId = vm.Info.Id;
                await LoadOrderDetailsAsync(vm.Info.Id);

                btnMarkFakturowane.IsEnabled = !vm.Info.CzyZafakturowane;
                btnCofnijFakturowanie.IsEnabled = vm.Info.CzyZafakturowane;

                // Pokaż/ukryj panel zmiany
                if (vm.Info.CzyZmodyfikowaneDlaFaktur)
                {
                    borderZmiana.Visibility = Visibility.Visible;
                    string czasZmiany = vm.Info.DataOstatniejModyfikacji.HasValue
                        ? $" o godz. {vm.Info.DataOstatniejModyfikacji.Value:HH:mm}" : "";
                    string ktoZmienil = !string.IsNullOrEmpty(vm.Info.ModyfikowalPrzez)
                        ? $"\nZmienił: {vm.Info.ModyfikowalPrzez}" : "";
                    txtZmianaInfo.Text = $"Zamówienie zostało zmodyfikowane{czasZmiany}.{ktoZmienil}";

                    // Załaduj szczegóły zmian
                    var zmiany = await LoadChangeHistoryAsync(vm.Info.Id);
                    icZmianyList.ItemsSource = zmiany;
                }
                else
                {
                    borderZmiana.Visibility = Visibility.Collapsed;
                    icZmianyList.ItemsSource = null;
                }

                if (vm.Info.CzyZafakturowane)
                {
                    txtInvoiceStatus.Text = $"Zamówienie zafakturowane.\nNr: {vm.Info.NumerFaktury}";
                }
                else if (vm.Info.CzyZmodyfikowaneDlaFaktur)
                {
                    txtInvoiceStatus.Text = "Zamówienie wymaga zatwierdzenia zmiany.";
                }
                else
                {
                    txtInvoiceStatus.Text = "Zamówienie gotowe do zafakturowania.";
                }
                return;
            }
            ClearDetails();
        }

        private async Task<List<string>> LoadChangeHistoryAsync(int orderId)
        {
            var changes = new List<string>();
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                // Pobierz ostatnie zmiany od ostatniego zatwierdzenia (typ EDYCJA)
                string sql = @"
                    SELECT TOP 10 OpisZmiany, UzytkownikNazwa, DataZmiany
                    FROM [dbo].[HistoriaZmianZamowien]
                    WHERE ZamowienieId = @OrderId AND TypZmiany = 'EDYCJA'
                    ORDER BY DataZmiany DESC";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@OrderId", orderId);
                await using var reader = await cmd.ExecuteReaderAsync();

                string[] polskieMiesiace = { "", "sty", "lut", "mar", "kwi", "maj", "cze", "lip", "sie", "wrz", "paź", "lis", "gru" };

                while (await reader.ReadAsync())
                {
                    string opis = reader.IsDBNull(0) ? "" : reader.GetString(0);
                    string kto = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    DateTime? kiedy = reader.IsDBNull(2) ? null : reader.GetDateTime(2);

                    string kiedyStr = kiedy.HasValue
                        ? $"{polskieMiesiace[kiedy.Value.Month]} {kiedy.Value.Day} {kiedy.Value:HH:mm}"
                        : "";

                    if (!string.IsNullOrEmpty(opis))
                    {
                        string change = $"{opis}";
                        if (!string.IsNullOrEmpty(kiedyStr))
                            change += $" ({kiedyStr})";
                        changes.Add(change);
                    }
                }

                if (changes.Count == 0)
                    changes.Add("Brak szczegółowych informacji o zmianach");
            }
            catch
            {
                changes.Add("Nie udało się pobrać historii zmian");
            }
            return changes;
        }

        private void DgOrders_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_currentOrderId.HasValue)
            {
                var widokZamowienia = new Kalendarz1.WidokZamowienia(UserID, _currentOrderId.Value);
                widokZamowienia.ShowDialog();
                _ = RefreshDataAsync();
            }
        }

        private async Task LoadOrderDetailsAsync(int orderId)
        {
            _dtDetails.Clear();
            _dtDetails.Columns.Clear();

            _dtDetails.Columns.Add("Produkt", typeof(string));
            _dtDetails.Columns.Add("Ilosc", typeof(decimal));
            _dtDetails.Columns.Add("Cena", typeof(string));
            _dtDetails.Columns.Add("Wartosc", typeof(decimal));

            try
            {
                var vm = ZamowieniaList.FirstOrDefault(z => z.Info.Id == orderId);
                if (vm != null)
                {
                    txtOdbiorca.Text = vm.Info.Klient;
                    txtHandlowiec.Text = $"Handlowiec: {vm.Info.Handlowiec ?? "brak"}";
                    txtDataZamowienia.Text = vm.Info.DataZamowienia.HasValue
                        ? $"Data zamówienia: {vm.Info.DataZamowienia.Value:dd.MM.yyyy}" : "";

                    if (!string.IsNullOrEmpty(vm.Info.GodzWyjazdu) || !string.IsNullOrEmpty(vm.Info.Kierowca) || !string.IsNullOrEmpty(vm.Info.Pojazd))
                    {
                        borderTransport.Visibility = Visibility.Visible;
                        txtGodzWyjazdu.Text = vm.Info.GodzWyjazdu ?? "";
                        txtKierowca.Text = vm.Info.Kierowca ?? "";
                        txtPojazd.Text = vm.Info.Pojazd ?? "";
                    }
                    else
                    {
                        borderTransport.Visibility = Visibility.Collapsed;
                    }
                }

                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                var productNames = new Dictionary<int, string>();
                await using (var cnHandel = new SqlConnection(_connHandel))
                {
                    await cnHandel.OpenAsync();
                    await using var cmdProd = new SqlCommand("SELECT ID, nazwa FROM [HANDEL].[HM].[TW]", cnHandel);
                    await using var readerProd = await cmdProd.ExecuteReaderAsync();
                    while (await readerProd.ReadAsync())
                    {
                        productNames[readerProd.GetInt32(0)] = readerProd.IsDBNull(1) ? "" : readerProd.GetString(1);
                    }
                }

                string sql = @"SELECT KodTowaru, Ilosc, Cena FROM [dbo].[ZamowieniaMiesoTowar] WHERE ZamowienieId = @OrderId";
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@OrderId", orderId);
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    int kodTowaru = reader.GetInt32(0);
                    decimal ilosc = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
                    string cena = reader.IsDBNull(2) ? "" : reader.GetString(2);

                    decimal cenaDecimal = 0;
                    decimal.TryParse(cena.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out cenaDecimal);

                    string produktNazwa = productNames.TryGetValue(kodTowaru, out var name) ? name : $"Produkt {kodTowaru}";

                    var row = _dtDetails.NewRow();
                    row["Produkt"] = produktNazwa;
                    row["Ilosc"] = ilosc;
                    row["Cena"] = cena;
                    row["Wartosc"] = ilosc * cenaDecimal;
                    _dtDetails.Rows.Add(row);
                }

                SetupDetailsDataGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania szczegółów: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetupDetailsDataGrid()
        {
            dgDetails.ItemsSource = _dtDetails.DefaultView;
            dgDetails.Columns.Clear();

            dgDetails.Columns.Add(new DataGridTextColumn
            {
                Header = "Produkt",
                Binding = new System.Windows.Data.Binding("Produkt"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });

            var rightStyle = new Style(typeof(TextBlock));
            rightStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));

            dgDetails.Columns.Add(new DataGridTextColumn
            {
                Header = "Ilość",
                Binding = new System.Windows.Data.Binding("Ilosc") { StringFormat = "N2" },
                Width = new DataGridLength(70),
                ElementStyle = rightStyle
            });

            dgDetails.Columns.Add(new DataGridTextColumn
            {
                Header = "Cena",
                Binding = new System.Windows.Data.Binding("Cena"),
                Width = new DataGridLength(70),
                ElementStyle = rightStyle
            });

            dgDetails.Columns.Add(new DataGridTextColumn
            {
                Header = "Wartość",
                Binding = new System.Windows.Data.Binding("Wartosc") { StringFormat = "N2" },
                Width = new DataGridLength(80),
                ElementStyle = rightStyle
            });
        }

        private void ClearDetails()
        {
            _currentOrderId = null;
            txtOdbiorca.Text = "Wybierz zamówienie...";
            txtHandlowiec.Text = "";
            txtDataZamowienia.Text = "";
            _dtDetails.Clear();
            dgDetails.ItemsSource = null;
            btnMarkFakturowane.IsEnabled = false;
            btnCofnijFakturowanie.IsEnabled = false;
            txtInvoiceStatus.Text = "Wybierz zamówienie z listy";
            borderTransport.Visibility = Visibility.Collapsed;
            borderZmiana.Visibility = Visibility.Collapsed;
            icZmianyList.ItemsSource = null;
        }

        private async void BtnAcceptChange_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentOrderId.HasValue) return;

            var vm = ZamowieniaList.FirstOrDefault(z => z.Info.Id == _currentOrderId.Value);
            if (vm == null) return;

            var result = MessageBox.Show(
                $"Czy potwierdzasz, że wiesz o zmianach w zamówieniu '{vm.Info.Klient}'?\n\n" +
                "Kliknięcie 'Tak' oznaczy zmianę jako przyjętą do wiadomości.",
                "Potwierdzenie przyjęcia zmiany - Faktury",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();

                    string sql = "UPDATE [dbo].[ZamowieniaMieso] SET CzyZmodyfikowaneDlaFaktur = 0 WHERE Id = @Id";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Id", _currentOrderId.Value);
                    await cmd.ExecuteNonQueryAsync();

                    MessageBox.Show("Zmiana została przyjęta.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                    await RefreshDataAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas akceptacji zmiany:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnMarkFakturowane_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentOrderId.HasValue) return;

            var result = MessageBox.Show(
                "Czy na pewno chcesz oznaczyć to zamówienie jako zafakturowane?",
                "Potwierdzenie",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();

                    string sql = "UPDATE [dbo].[ZamowieniaMieso] SET CzyZafakturowane = 1 WHERE Id = @Id";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Id", _currentOrderId.Value);
                    await cmd.ExecuteNonQueryAsync();

                    MessageBox.Show("Zamówienie zostało oznaczone jako zafakturowane.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                    await RefreshDataAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnCofnijFakturowanie_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentOrderId.HasValue) return;

            var vm = ZamowieniaList.FirstOrDefault(z => z.Info.Id == _currentOrderId.Value);
            if (vm == null) return;

            var result = MessageBox.Show(
                $"Czy na pewno chcesz cofnąć fakturowanie zamówienia '{vm.Info.Klient}'?\n\n" +
                "Zamówienie wróci do listy do zafakturowania.",
                "Potwierdzenie cofnięcia fakturowania",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();

                    string sql = "UPDATE [dbo].[ZamowieniaMieso] SET CzyZafakturowane = 0, NumerFaktury = NULL WHERE Id = @Id";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Id", _currentOrderId.Value);
                    await cmd.ExecuteNonQueryAsync();

                    MessageBox.Show("Cofnięto fakturowanie zamówienia.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                    await RefreshDataAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async Task EnsureColumnsExistAsync(SqlConnection cn)
        {
            try
            {
                string sql = @"
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ZamowieniaMieso' AND COLUMN_NAME = 'CzyZafakturowane')
                        ALTER TABLE [dbo].[ZamowieniaMieso] ADD CzyZafakturowane BIT DEFAULT 0;
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ZamowieniaMieso' AND COLUMN_NAME = 'NumerFaktury')
                        ALTER TABLE [dbo].[ZamowieniaMieso] ADD NumerFaktury NVARCHAR(50) NULL;
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ZamowieniaMieso' AND COLUMN_NAME = 'CzyZmodyfikowaneDlaFaktur')
                        ALTER TABLE [dbo].[ZamowieniaMieso] ADD CzyZmodyfikowaneDlaFaktur BIT DEFAULT 0;
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ZamowieniaMieso' AND COLUMN_NAME = 'DataOstatniejModyfikacji')
                        ALTER TABLE [dbo].[ZamowieniaMieso] ADD DataOstatniejModyfikacji DATETIME NULL;
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ZamowieniaMieso' AND COLUMN_NAME = 'ModyfikowalPrzez')
                        ALTER TABLE [dbo].[ZamowieniaMieso] ADD ModyfikowalPrzez NVARCHAR(100) NULL;
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ZamowieniaMieso' AND COLUMN_NAME = 'TransportKursID')
                        ALTER TABLE [dbo].[ZamowieniaMieso] ADD TransportKursID BIGINT NULL;";
                await using var cmd = new SqlCommand(sql, cn);
                await cmd.ExecuteNonQueryAsync();
            }
            catch { }
        }

        #region Data Classes

        public class ZamowienieInfo
        {
            public int Id { get; set; }
            public int KlientId { get; set; }
            public string Klient { get; set; } = "";
            public string Handlowiec { get; set; } = "";
            public decimal TotalIlosc { get; set; }
            public decimal Wartosc { get; set; }
            public DateTime? DataZamowienia { get; set; }
            public DateTime? DataUboju { get; set; }
            public string Status { get; set; } = "";
            public string UtworzonePrzez { get; set; } = "";
            public bool CzyZafakturowane { get; set; }
            public string NumerFaktury { get; set; } = "";
            public long? TransportKursID { get; set; }
            public string GodzWyjazdu { get; set; } = "";
            public string Kierowca { get; set; } = "";
            public string Pojazd { get; set; } = "";
            public bool CzyZmodyfikowaneDlaFaktur { get; set; }
            public DateTime? DataOstatniejModyfikacji { get; set; }
            public string ModyfikowalPrzez { get; set; } = "";
            public bool CzyMaCeny { get; set; }
        }

        public class ZamowienieViewModel : INotifyPropertyChanged
        {
            private static readonly string[] _polskieMiesiace = { "", "sty", "lut", "mar", "kwi", "maj", "cze", "lip", "sie", "wrz", "paź", "lis", "gru" };

            public ZamowienieInfo Info { get; }
            public ZamowienieViewModel(ZamowienieInfo info) { Info = info; }

            // Klient z wykrzyknikiem gdy jest zmiana
            public string Klient => Info.CzyZmodyfikowaneDlaFaktur ? $"⚠️ {Info.Klient}" : Info.Klient;

            // Kolor nazwy klienta - pomarańczowy gdy zmiana, czarny normalnie
            public Brush KlientColor => Info.CzyZmodyfikowaneDlaFaktur ? Brushes.OrangeRed : Brushes.Black;

            public string Handlowiec => Info.Handlowiec;
            public decimal TotalIlosc => Info.TotalIlosc;
            public decimal Wartosc => Info.Wartosc;

            // Status wyświetlany
            public string StatusDisplay
            {
                get
                {
                    if (Info.CzyZafakturowane) return "Zafakturowane";
                    if (Info.CzyZmodyfikowaneDlaFaktur) return "⚠ Do zatwierdzenia";
                    return Info.Status;
                }
            }

            public Brush StatusColor
            {
                get
                {
                    if (Info.CzyZmodyfikowaneDlaFaktur) return Brushes.OrangeRed;
                    if (Info.CzyZafakturowane) return new SolidColorBrush(Color.FromRgb(46, 125, 50)); // Zielony
                    if (Info.Status == "Wydano") return new SolidColorBrush(Color.FromRgb(2, 119, 189)); // Niebieski
                    if (Info.Status == "Zrealizowane") return new SolidColorBrush(Color.FromRgb(56, 142, 60)); // Zielony
                    if (Info.Status == "W realizacji") return new SolidColorBrush(Color.FromRgb(25, 118, 210)); // Niebieski
                    if (Info.Status == "Nowe") return new SolidColorBrush(Color.FromRgb(245, 124, 0)); // Pomarańczowy
                    return Brushes.Black;
                }
            }

            // Kolumna ostatniej zmiany - format: sty 12 12:00
            public string OstatniaZmiana
            {
                get
                {
                    if (!Info.CzyZmodyfikowaneDlaFaktur) return "";
                    if (Info.DataOstatniejModyfikacji.HasValue)
                    {
                        var d = Info.DataOstatniejModyfikacji.Value;
                        return $"{_polskieMiesiace[d.Month]} {d.Day} {d:HH:mm}";
                    }
                    return "Zmiana";
                }
            }

            public Brush ZmianaColor => Info.CzyZmodyfikowaneDlaFaktur ? Brushes.OrangeRed : Brushes.Transparent;

            // Kto zmienił - imię i nazwisko
            public string KtoZmienil => Info.ModyfikowalPrzez ?? "";

            // Czy wszystkie towary mają ceny - ✓ lub ✗
            public string CenaDisplay => Info.CzyMaCeny ? "✓" : "✗";
            public Brush CenaColor => Info.CzyMaCeny
                ? new SolidColorBrush(Color.FromRgb(46, 125, 50))   // Zielony dla ✓
                : new SolidColorBrush(Color.FromRgb(198, 40, 40));  // Czerwony dla ✗

            // Wyjazd
            public string GodzWyjazdu => Info.GodzWyjazdu ?? "";
            public string Kierowca => Info.Kierowca ?? "";
            public string Pojazd => Info.Pojazd ?? "";

            // Kolor tła wiersza - zgodny z WidokZamowienia
            public Brush RowBackground
            {
                get
                {
                    if (Info.CzyZmodyfikowaneDlaFaktur)
                        return new SolidColorBrush(Color.FromRgb(255, 243, 224)); // Pomarańczowe tło dla zmian
                    if (Info.CzyZafakturowane)
                        return new SolidColorBrush(Color.FromRgb(200, 230, 201)); // Ciemniejszy zielony
                    if (Info.Status == "Wydano")
                        return new SolidColorBrush(Color.FromRgb(225, 245, 254)); // Jasnoniebieski
                    if (Info.Status == "Zrealizowane")
                        return new SolidColorBrush(Color.FromRgb(232, 245, 233)); // Jasnozielony
                    if (Info.Status == "W realizacji")
                        return new SolidColorBrush(Color.FromRgb(227, 242, 253)); // Jasnoniebieski
                    // Nowe - żółte tło
                    return new SolidColorBrush(Color.FromRgb(255, 248, 225));
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;
        }

        #endregion
    }
}
