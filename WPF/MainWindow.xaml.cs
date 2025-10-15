using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Kalendarz1.WPF
{
    public partial class MainWindow : Window
    {
        private readonly string _connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly string _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        private static readonly DateTime MinSqlDate = new DateTime(1753, 1, 1);
        private static readonly DateTime MaxSqlDate = new DateTime(9999, 12, 31);

        public string UserID { get; set; } = string.Empty;
        private DateTime _selectedDate;
        private int? _currentOrderId;
        private readonly List<Button> _dayButtons = new();
        private readonly Dictionary<Button, DateTime> _dayButtonDates = new();

        private bool _showBySlaughterDate = true;
        private bool _slaughterDateColumnExists = true;
        private bool _isInitialized = false;

        private readonly DataTable _dtOrders = new();
        private readonly Dictionary<int, string> _productCodeCache = new();
        private readonly Dictionary<int, string> _productCatalogCache = new();
        private readonly Dictionary<string, string> _userCache = new();
        private readonly List<string> _salesmenCache = new();
        private bool _showReleasesWithoutOrders = false;

        private readonly Dictionary<string, Color> _salesmanColors = new Dictionary<string, Color>();
        private readonly List<Color> _colorPalette = new List<Color>
        {
            Color.FromRgb(230, 255, 230), Color.FromRgb(230, 242, 255), Color.FromRgb(255, 240, 230),
            Color.FromRgb(230, 255, 247), Color.FromRgb(255, 230, 242), Color.FromRgb(245, 245, 220),
            Color.FromRgb(255, 228, 225), Color.FromRgb(240, 255, 255), Color.FromRgb(240, 248, 255)
        };
        private int _colorIndex = 0;

        private DateTime ValidateSqlDate(DateTime date)
        {
            if (date < MinSqlDate) return MinSqlDate;
            if (date > MaxSqlDate) return MaxSqlDate;
            return date.Date;
        }

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isInitialized) return;
            _isInitialized = true;

            SetupDayButtons();
            btnDelete.Visibility = (UserID == "11111") ? Visibility.Visible : Visibility.Collapsed;
            chkShowReleasesWithoutOrders.IsChecked = _showReleasesWithoutOrders;

            await LoadInitialDataAsync();

            _selectedDate = ValidateSqlDate(DateTime.Today);
            UpdateDayButtonDates();
            await RefreshAllDataAsync();
        }

        #region Konfiguracja Wydajności i Produktów

        private async Task<(decimal wspolczynnikTuszki, decimal procentA, decimal procentB)> GetKonfiguracjaWydajnosciAsync(DateTime data)
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
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd pobierania konfiguracji wydajności: {ex.Message}\n\nUżyto wartości domyślnych.",
                    "Ostrzeżenie", MessageBoxButton.OK, MessageBoxImage.Warning);
                return (78.0m, 85.0m, 15.0m);
            }
        }

        private async Task<Dictionary<int, decimal>> GetKonfiguracjaProduktowAsync(DateTime data)
        {
            var result = new Dictionary<int, decimal>();

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                const string query = @"
                    SELECT kp.TowarID, kp.ProcentUdzialu
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
                }

                return result;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd pobierania konfiguracji produktów: {ex.Message}",
                    "Ostrzeżenie", MessageBoxButton.OK, MessageBoxImage.Warning);
                return result;
            }
        }

        #endregion

        #region Setup and Initialization

        private void SetupDayButtons()
        {
            string[] dayNames = { "Pon", "Wt", "Śr", "Czw", "Pt", "So", "Nd" };

            for (int i = 0; i < 7; i++)
            {
                var btn = new Button
                {
                    Style = (Style)FindResource("DayButtonStyle")
                };

                var stack = new StackPanel();
                stack.Children.Add(new TextBlock { Text = dayNames[i], FontSize = 9, FontWeight = FontWeights.Bold });
                stack.Children.Add(new TextBlock { Text = DateTime.Today.AddDays(i).ToString("dd.MM"), FontSize = 9 });
                btn.Content = stack;

                btn.Click += DayButton_Click;
                _dayButtonDates[btn] = DateTime.Today.AddDays(i);

                _dayButtons.Add(btn);
                panelDays.Children.Add(btn);
            }
        }

        private async Task LoadInitialDataAsync()
        {
            await CheckAndCreateSlaughterDateColumnAsync();

            _productCodeCache.Clear();
            _productCatalogCache.Clear();

            await using (var cn = new SqlConnection(_connHandel))
            {
                await cn.OpenAsync();
                await using var cmd = new SqlCommand("SELECT ID, kod, katalog FROM [HANDEL].[HM].[TW]", cn);
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    int idtw = reader.GetInt32(0);
                    string kod = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    object katObj = reader.GetValue(2);

                    bool inCatalog = false;
                    if (!(katObj is DBNull))
                    {
                        if (katObj is int ki)
                            inCatalog = (ki == 67095 || ki == 67153);
                        else
                        {
                            string katStr = Convert.ToString(katObj);
                            inCatalog = (katStr == "67095" || katStr == "67153");
                        }
                    }

                    _productCodeCache[idtw] = kod;
                    if (inCatalog)
                        _productCatalogCache[idtw] = kod;
                }
            }

            var productList = _productCatalogCache.OrderBy(x => x.Value)
                .Select(k => new KeyValuePair<int, string>(k.Key, k.Value)).ToList();
            productList.Insert(0, new KeyValuePair<int, string>(0, "— Wszystkie towary —"));

            cbFilterProduct.ItemsSource = productList;
            cbFilterProduct.SelectedIndex = 0;

            _userCache.Clear();
            await using (var cn2 = new SqlConnection(_connLibra))
            {
                await cn2.OpenAsync();
                await using var cmd = new SqlCommand("SELECT ID, Name FROM dbo.operators", cn2);
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var idStr = reader.IsDBNull(0) ? "" : reader.GetString(0);
                    var name = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    if (!string.IsNullOrEmpty(idStr))
                        _userCache[idStr] = name;
                }
            }

            _salesmenCache.Clear();
            await using (var cn3 = new SqlConnection(_connHandel))
            {
                await cn3.OpenAsync();
                await using var cmd = new SqlCommand(
                    @"SELECT DISTINCT CDim_Handlowiec_Val 
                      FROM [HANDEL].[SSCommon].[ContractorClassification] 
                      WHERE CDim_Handlowiec_Val IS NOT NULL 
                      ORDER BY 1", cn3);
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var val = reader.IsDBNull(0) ? "" : reader.GetString(0);
                    if (!string.IsNullOrWhiteSpace(val))
                        _salesmenCache.Add(val);
                }
            }

            var salesmenList = new List<string> { "— Wszyscy —" };
            salesmenList.AddRange(_salesmenCache);
            cbFilterSalesman.ItemsSource = salesmenList;
            cbFilterSalesman.SelectedIndex = 0;

            if (_slaughterDateColumnExists)
            {
                rbSlaughterDate.IsChecked = true;
            }
            else
            {
                rbSlaughterDate.IsEnabled = false;
                rbSlaughterDate.Content = "Data uboju (niedostępne)";
                rbPickupDate.IsChecked = true;
            }
        }

        private async Task CheckAndCreateSlaughterDateColumnAsync()
        {
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                const string checkSql = @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                                         WHERE TABLE_NAME = 'ZamowieniaMieso' AND COLUMN_NAME = 'DataUboju'";

                await using var cmdCheck = new SqlCommand(checkSql, cn);
                int count = Convert.ToInt32(await cmdCheck.ExecuteScalarAsync());

                if (count == 0)
                {
                    const string alterSql = @"ALTER TABLE [dbo].[ZamowieniaMieso] ADD DataUboju DATE NULL";
                    await using var cmdAlter = new SqlCommand(alterSql, cn);
                    await cmdAlter.ExecuteNonQueryAsync();

                    _slaughterDateColumnExists = true;
                    MessageBox.Show("Kolumna 'DataUboju' została dodana do bazy danych.",
                        "Aktualizacja bazy", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    _slaughterDateColumnExists = true;
                }
            }
            catch (Exception ex)
            {
                _slaughterDateColumnExists = false;
                MessageBox.Show($"Nie można dodać kolumny DataUboju do bazy danych.\n" +
                               $"Funkcja filtrowania po dacie uboju będzie niedostępna.\n\n" +
                               $"Błąd: {ex.Message}",
                    "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        #endregion
        #region Navigation Events

        private void DayButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && _dayButtonDates.TryGetValue(btn, out DateTime date))
            {
                _selectedDate = date;
                UpdateDayButtonDates();
                _ = RefreshAllDataAsync();
            }
        }

        private void BtnPreviousWeek_Click(object sender, RoutedEventArgs e)
        {
            _selectedDate = _selectedDate.AddDays(-7);
            int delta = ((int)_selectedDate.DayOfWeek + 6) % 7;
            _selectedDate = _selectedDate.AddDays(-delta);
            UpdateDayButtonDates();
            _ = RefreshAllDataAsync();
        }

        private void BtnNextWeek_Click(object sender, RoutedEventArgs e)
        {
            _selectedDate = _selectedDate.AddDays(7);
            int delta = ((int)_selectedDate.DayOfWeek + 6) % 7;
            _selectedDate = _selectedDate.AddDays(-delta);
            UpdateDayButtonDates();
            _ = RefreshAllDataAsync();
        }

        private void UpdateDayButtonDates()
        {
            int delta = ((int)_selectedDate.DayOfWeek + 6) % 7;
            DateTime startOfWeek = _selectedDate.AddDays(-delta);

            lblWeekRange.Text = $"{startOfWeek:dd.MM.yyyy}\n{startOfWeek.AddDays(6):dd.MM.yyyy}";

            string[] dayNames = { "Pon", "Wt", "Śr", "Czw", "Pt", "So", "Nd" };

            for (int i = 0; i < Math.Min(7, _dayButtons.Count); i++)
            {
                var dt = startOfWeek.AddDays(i);
                var btn = _dayButtons[i];

                _dayButtonDates[btn] = dt;

                var stack = new StackPanel();
                stack.Children.Add(new TextBlock { Text = dayNames[i], FontSize = 9, FontWeight = FontWeights.Bold });
                stack.Children.Add(new TextBlock { Text = dt.ToString("dd.MM"), FontSize = 9 });
                btn.Content = stack;

                if (dt.Date == _selectedDate.Date)
                {
                    btn.Background = new SolidColorBrush(Color.FromRgb(52, 152, 219));
                    btn.Foreground = Brushes.White;
                }
                else if (dt.Date == DateTime.Today)
                {
                    btn.Background = new SolidColorBrush(Color.FromRgb(241, 196, 15));
                    btn.Foreground = Brushes.White;
                }
                else
                {
                    btn.Background = new SolidColorBrush(Color.FromRgb(245, 247, 250));
                    btn.Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80));
                }
            }
        }

        #endregion

        #region Action Button Events

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            _ = RefreshAllDataAsync();
        }

        private async void BtnNew_Click(object sender, RoutedEventArgs e)
        {
            var widokZamowienia = new Kalendarz1.WidokZamowienia(UserID, null);
            if (widokZamowienia.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                await RefreshAllDataAsync();
            }
        }

        private async void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentOrderId.HasValue || _currentOrderId.Value <= 0)
            {
                MessageBox.Show("Najpierw wybierz zamówienie do edycji.",
                    "Brak wyboru", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var widokZamowienia = new Kalendarz1.WidokZamowienia(UserID, _currentOrderId.Value);
            if (widokZamowienia.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                await RefreshAllDataAsync();
            }
        }

        private async void BtnNote_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentOrderId.HasValue || _currentOrderId.Value <= 0)
            {
                MessageBox.Show("Najpierw wybierz zamówienie, do którego chcesz dodać notatkę.",
                    "Brak wyboru", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string currentNote = "";
            await using (var cn = new SqlConnection(_connLibra))
            {
                await cn.OpenAsync();
                var cmd = new SqlCommand("SELECT Uwagi FROM ZamowieniaMieso WHERE Id = @Id", cn);
                cmd.Parameters.AddWithValue("@Id", _currentOrderId.Value);
                var result = await cmd.ExecuteScalarAsync();
                currentNote = result?.ToString() ?? "";
            }

            var noteWindow = new NoteWindow(currentNote);
            if (noteWindow.ShowDialog() == true)
            {
                try
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();
                    var cmd = new SqlCommand("UPDATE ZamowieniaMieso SET Uwagi = @Uwagi WHERE Id = @Id", cn);
                    cmd.Parameters.AddWithValue("@Id", _currentOrderId.Value);
                    cmd.Parameters.AddWithValue("@Uwagi", string.IsNullOrEmpty(noteWindow.NoteText)
                        ? DBNull.Value : noteWindow.NoteText);
                    await cmd.ExecuteNonQueryAsync();

                    MessageBox.Show("Notatka została zapisana.", "Sukces",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    await DisplayOrderDetailsAsync(_currentOrderId.Value);
                    await RefreshAllDataAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas zapisywania notatki: {ex.Message}",
                        "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentOrderId.HasValue || _currentOrderId.Value <= 0)
            {
                MessageBox.Show("Najpierw wybierz zamówienie do anulowania.",
                    "Brak wyboru", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show("Czy na pewno chcesz anulować wybrane zamówienie? Tej operacji nie można cofnąć.",
                "Potwierdź anulowanie", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();
                    await using var cmd = new SqlCommand(
                        "UPDATE dbo.ZamowieniaMieso SET Status = 'Anulowane' WHERE Id = @Id", cn);
                    cmd.Parameters.AddWithValue("@Id", _currentOrderId.Value);
                    await cmd.ExecuteNonQueryAsync();

                    MessageBox.Show("Zamówienie zostało anulowane.", "Sukces",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    await RefreshAllDataAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Wystąpił błąd podczas anulowania zamówienia: {ex.Message}",
                        "Błąd krytyczny", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnDuplicate_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentOrderId.HasValue || _currentOrderId.Value <= 0)
            {
                MessageBox.Show("Najpierw wybierz zamówienie do duplikacji.",
                    "Brak wyboru", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new MultipleDatePickerWindow("Wybierz dni dla duplikatu zamówienia");
            if (dlg.ShowDialog() == true && dlg.SelectedDates.Any())
            {
                try
                {
                    int created = 0;
                    foreach (var date in dlg.SelectedDates)
                    {
                        await DuplicateOrderAsync(_currentOrderId.Value, date, dlg.CopyNotes);
                        created++;
                    }

                    MessageBox.Show($"Zamówienie zostało zduplikowane na {created} dni.\n" +
                                  $"Od {dlg.SelectedDates.Min():yyyy-MM-dd} do {dlg.SelectedDates.Max():yyyy-MM-dd}",
                        "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);

                    _selectedDate = dlg.SelectedDates.First();
                    UpdateDayButtonDates();
                    await RefreshAllDataAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas duplikowania: {ex.Message}",
                        "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnCyclic_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentOrderId.HasValue || _currentOrderId.Value <= 0)
            {
                MessageBox.Show("Najpierw wybierz zamówienie wzorcowe dla cyklu.",
                    "Brak wyboru", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new CyclicOrdersWindow();
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    int created = 0;
                    foreach (var date in dlg.SelectedDays)
                    {
                        await DuplicateOrderAsync(_currentOrderId.Value, date, false);
                        created++;
                    }

                    MessageBox.Show($"Utworzono {created} zamówień cyklicznych.\n" +
                                  $"Od {dlg.StartDate:yyyy-MM-dd} do {dlg.EndDate:yyyy-MM-dd}",
                        "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);

                    await RefreshAllDataAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas tworzenia zamówień cyklicznych: {ex.Message}",
                        "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentOrderId.HasValue || _currentOrderId.Value <= 0)
            {
                MessageBox.Show("Najpierw wybierz zamówienie do usunięcia.",
                    "Brak wyboru", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show("Czy na pewno chcesz TRWALE usunąć wybrane zamówienie? Tej operacji nie można cofnąć.",
                "Potwierdź usunięcie", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();

                    using (var cmd = new SqlCommand("DELETE FROM dbo.ZamowieniaMiesoTowar WHERE ZamowienieId = @Id", cn))
                    {
                        cmd.Parameters.AddWithValue("@Id", _currentOrderId.Value);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    using (var cmd = new SqlCommand("DELETE FROM dbo.ZamowieniaMieso WHERE Id = @Id", cn))
                    {
                        cmd.Parameters.AddWithValue("@Id", _currentOrderId.Value);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    MessageBox.Show("Zamówienie zostało trwale usunięte.", "Sukces",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    await RefreshAllDataAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas usuwania zamówienia: {ex.Message}",
                        "Błąd krytyczny", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion
        #region Filter Events

        private void TxtFilterRecipient_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
     
        }

        private void CbFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
      
        }

        private async void CbFilterProduct_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await RefreshAllDataAsync();
        }

        private void ChkShowReleases_Changed(object sender, RoutedEventArgs e)
        {
            _showReleasesWithoutOrders = chkShowReleasesWithoutOrders.IsChecked == true;
            ApplyFilters();
          
        }

        private async void RbDateFilter_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            if (rbSlaughterDate.IsChecked == true && _slaughterDateColumnExists)
            {
                _showBySlaughterDate = true;
                await RefreshAllDataAsync();
            }
            else if (rbPickupDate.IsChecked == true)
            {
                _showBySlaughterDate = false;
                await RefreshAllDataAsync();
            }
        }

        #endregion

        #region DataGrid Events

        private async void DgOrders_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgOrders.SelectedItem is DataRowView row)
            {
                var status = row.Row.Field<string>("Status") ?? "";

                if (status == "Wydanie bez zamówienia")
                {
                    var clientId = row.Row.Field<int>("KlientId");
                    _currentOrderId = null;
                    await DisplayReleaseWithoutOrderDetailsAsync(clientId, _selectedDate);
                    return;
                }

                var id = row.Row.Field<int>("Id");
                if (id > 0)
                {
                    _currentOrderId = id;
                    await DisplayOrderDetailsAsync(id);
                    return;
                }
            }

            ClearDetails();
        }

        private async void DgOrders_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_currentOrderId.HasValue && _currentOrderId.Value > 0)
            {
                var widokZamowienia = new Kalendarz1.WidokZamowienia(UserID, _currentOrderId.Value);
                if (widokZamowienia.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    await RefreshAllDataAsync();
                }
            }
        }

        #endregion

        #region Data Loading

        private async Task RefreshAllDataAsync()
        {
            try
            {
                await LoadOrdersForDayAsync(_selectedDate);
                await DisplayProductAggregationAsync(_selectedDate);
       

                if (_currentOrderId.HasValue && _currentOrderId.Value > 0)
                {
                    await DisplayOrderDetailsAsync(_currentOrderId.Value);
                }
                else
                {
                    ClearDetails();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas odświeżania danych: {ex.Message}\n\nSTACKTRACE:\n{ex.StackTrace}",
                    "Błąd Krytyczny", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadOrdersForDayAsync(DateTime day)
        {
            day = ValidateSqlDate(day);

            if (_dtOrders.Columns.Count == 0)
            {
                _dtOrders.Columns.Add("Id", typeof(int));
                _dtOrders.Columns.Add("KlientId", typeof(int));
                _dtOrders.Columns.Add("Odbiorca", typeof(string));
                _dtOrders.Columns.Add("Handlowiec", typeof(string));
                _dtOrders.Columns.Add("IloscZamowiona", typeof(decimal));
                _dtOrders.Columns.Add("IloscFaktyczna", typeof(decimal));
                _dtOrders.Columns.Add("Pojemniki", typeof(int));
                _dtOrders.Columns.Add("Palety", typeof(decimal));
                _dtOrders.Columns.Add("TrybE2", typeof(string));
                _dtOrders.Columns.Add("DataPrzyjecia", typeof(DateTime));
                _dtOrders.Columns.Add("GodzinaPrzyjecia", typeof(string));
                _dtOrders.Columns.Add("TerminOdbioru", typeof(string));
                _dtOrders.Columns.Add("DataUboju", typeof(DateTime));
                _dtOrders.Columns.Add("UtworzonePrzez", typeof(string));
                _dtOrders.Columns.Add("Status", typeof(string));
                _dtOrders.Columns.Add("MaNotatke", typeof(bool));
                _dtOrders.Columns.Add("MaFolie", typeof(bool));  // DODAJ TĘ LINIĘ
            }
            else
            {
                _dtOrders.Clear();
            }

            var contractors = new Dictionary<int, (string Name, string Salesman)>();
            await using (var cnHandel = new SqlConnection(_connHandel))
            {
                await cnHandel.OpenAsync();
                const string sqlContr = @"SELECT c.Id, c.Shortcut, wym.CDim_Handlowiec_Val 
                                FROM [HANDEL].[SSCommon].[STContractors] c 
                                LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] wym 
                                ON c.Id = wym.ElementId";
                await using var cmdContr = new SqlCommand(sqlContr, cnHandel);
                await using var rd = await cmdContr.ExecuteReaderAsync();

                while (await rd.ReadAsync())
                {
                    int id = rd.GetInt32(0);
                    string shortcut = rd.IsDBNull(1) ? "" : rd.GetString(1);
                    string salesman = rd.IsDBNull(2) ? "" : rd.GetString(2);
                    contractors[id] = (string.IsNullOrWhiteSpace(shortcut) ? $"KH {id}" : shortcut, salesman);
                }
            }

            int? selectedProductId = null;
            if (cbFilterProduct.SelectedIndex > 0 && cbFilterProduct.SelectedValue is int selectedId)
                selectedProductId = selectedId;

            var temp = new DataTable();
            var clientsWithOrders = new HashSet<int>();

            await using (var cnLibra = new SqlConnection(_connLibra))
            {
                await cnLibra.OpenAsync();
                string dateColumn = (_showBySlaughterDate && _slaughterDateColumnExists) ? "DataUboju" : "DataZamowienia";

                string sqlClients = $@"SELECT DISTINCT KlientId FROM [dbo].[ZamowieniaMieso] 
                              WHERE {dateColumn} = @Day AND Status <> 'Anulowane' AND KlientId IS NOT NULL";
                await using var cmdClients = new SqlCommand(sqlClients, cnLibra);
                cmdClients.Parameters.AddWithValue("@Day", day);
                await using var readerClients = await cmdClients.ExecuteReaderAsync();

                while (await readerClients.ReadAsync())
                {
                    clientsWithOrders.Add(readerClients.GetInt32(0));
                }
            }

            if (_productCatalogCache.Keys.Any())
            {
                await using (var cnLibra = new SqlConnection(_connLibra))
                {
                    await cnLibra.OpenAsync();
                    var idwList = string.Join(",", _productCatalogCache.Keys);
                    string dateColumn = (_showBySlaughterDate && _slaughterDateColumnExists) ? "zm.DataUboju" : "zm.DataZamowienia";
                    string slaughterDateSelect = _slaughterDateColumnExists ? ", zm.DataUboju" : "";
                    string slaughterDateGroupBy = _slaughterDateColumnExists ? ", zm.DataUboju" : "";

                    string sql = $@"
SELECT zm.Id, zm.KlientId, SUM(ISNULL(zmt.Ilosc,0)) AS Ilosc, 
       zm.DataPrzyjazdu, zm.DataUtworzenia, zm.IdUser, zm.Status,
       zm.LiczbaPojemnikow, zm.LiczbaPalet, zm.TrybE2, zm.Uwagi,
       CAST(CASE WHEN EXISTS(SELECT 1 FROM [dbo].[ZamowieniaMiesoTowar] WHERE ZamowienieId = zm.Id AND Folia = 1) 
            THEN 1 ELSE 0 END AS BIT) AS MaFolie{slaughterDateSelect}
FROM [dbo].[ZamowieniaMieso] zm
JOIN [dbo].[ZamowieniaMiesoTowar] zmt ON zm.Id = zmt.ZamowienieId
WHERE {dateColumn} = @Day AND zmt.KodTowaru IN ({idwList}) AND zm.Status <> 'Anulowane' " +
                                        (selectedProductId.HasValue ? "AND zmt.KodTowaru = @ProductId " : "") +
                                        $@"GROUP BY zm.Id, zm.KlientId, zm.DataPrzyjazdu, zm.DataUtworzenia, zm.IdUser, zm.Status,
     zm.LiczbaPojemnikow, zm.LiczbaPalet, zm.TrybE2, zm.Uwagi{slaughterDateGroupBy}
ORDER BY zm.Id";

                    await using var cmd = new SqlCommand(sql, cnLibra);
                    cmd.Parameters.AddWithValue("@Day", day);
                    if (selectedProductId.HasValue)
                        cmd.Parameters.AddWithValue("@ProductId", selectedProductId.Value);

                    using var da = new SqlDataAdapter(cmd);
                    da.Fill(temp);
                }
            }

            var releasesPerClientProduct = await GetReleasesPerClientProductAsync(day);
            var cultureInfo = new CultureInfo("pl-PL");

            int totalOrdersCount = 0;
            decimal totalOrdered = 0m;
            decimal totalReleased = 0m;
            decimal totalPallets = 0m;

            foreach (DataRow r in temp.Rows)
            {
                int id = Convert.ToInt32(r["Id"]);
                int clientId = Convert.ToInt32(r["KlientId"]);
                decimal quantity = Convert.ToDecimal(r["Ilosc"]);
                DateTime? arrivalDate = r["DataPrzyjazdu"] is DBNull ? null : Convert.ToDateTime(r["DataPrzyjazdu"]);
                DateTime? createdDate = r["DataUtworzenia"] is DBNull ? null : Convert.ToDateTime(r["DataUtworzenia"]);
                DateTime? slaughterDate = null;

                if (_slaughterDateColumnExists && temp.Columns.Contains("DataUboju"))
                {
                    slaughterDate = r["DataUboju"] is DBNull ? null : Convert.ToDateTime(r["DataUboju"]);
                }

                string userId = r["IdUser"]?.ToString() ?? "";
                string status = r["Status"]?.ToString() ?? "Nowe";
                string notes = r["Uwagi"]?.ToString() ?? "";
                bool hasNote = !string.IsNullOrWhiteSpace(notes);
                bool hasFoil = temp.Columns.Contains("MaFolie") && !(r["MaFolie"] is DBNull) && Convert.ToBoolean(r["MaFolie"]);


                int containers = r["LiczbaPojemnikow"] is DBNull ? 0 : Convert.ToInt32(r["LiczbaPojemnikow"]);
                decimal pallets = Math.Ceiling(r["LiczbaPalet"] is DBNull ? 0m : Convert.ToDecimal(r["LiczbaPalet"]));
                bool modeE2 = r["TrybE2"] != DBNull.Value && Convert.ToBoolean(r["TrybE2"]);
                string modeText = modeE2 ? "E2 (40)" : "STD (36)";

                var (name, salesman) = contractors.TryGetValue(clientId, out var c) ? c : ($"Nieznany ({clientId})", "");

                if (hasNote)
                {
                    name = "📝 " + name;
                }

                if (hasFoil)
                {
                    name = "📦 " + name;  // DODAJ IKONKĘ FOLII
                }


                decimal released = 0m;
                if (releasesPerClientProduct.TryGetValue(clientId, out var perProduct))
                {
                    released = selectedProductId.HasValue ?
                        perProduct.TryGetValue(selectedProductId.Value, out var w) ? w : 0m :
                        perProduct.Values.Sum();
                }

                string pickupTerm = arrivalDate.HasValue ?
                    arrivalDate.Value.ToString("yyyy-MM-dd dddd HH:mm", cultureInfo) :
                    day.ToString("yyyy-MM-dd dddd", cultureInfo);

                string createdBy = "";
                if (createdDate.HasValue)
                {
                    string userName = _userCache.TryGetValue(userId, out var user) ? user : "Brak";
                    createdBy = $"{createdDate.Value:yyyy-MM-dd HH:mm} ({userName})";
                }
                else
                {
                    createdBy = _userCache.TryGetValue(userId, out var user) ? user : "Brak";
                }

                totalOrdersCount++;
                totalOrdered += quantity;
                totalReleased += released;
                totalPallets += pallets;

                _dtOrders.Rows.Add(
         id, clientId, name, salesman, quantity, released, containers, pallets, modeText,
         arrivalDate?.Date ?? day, arrivalDate?.ToString("HH:mm") ?? "08:00",
         pickupTerm, slaughterDate.HasValue ? (object)slaughterDate.Value.Date : DBNull.Value,
         createdBy, status, hasNote, hasFoil  // DODAJ hasFoil
     );
            }

            var releasesWithoutOrders = new List<DataRow>();
            foreach (var kv in releasesPerClientProduct)
            {
                int clientId = kv.Key;
                if (clientsWithOrders.Contains(clientId)) continue;

                decimal released = selectedProductId.HasValue ?
                    kv.Value.TryGetValue(selectedProductId.Value, out var w) ? w : 0m :
                    kv.Value.Values.Sum();

                if (released == 0) continue;

                var (name, salesman) = contractors.TryGetValue(clientId, out var c) ? c : ($"Nieznany ({clientId})", "");
                var row = _dtOrders.NewRow();
                row["Id"] = 0;
                row["KlientId"] = clientId;
                row["Odbiorca"] = name;
                row["Handlowiec"] = salesman;
                row["IloscZamowiona"] = 0m;
                row["IloscFaktyczna"] = released;
                row["Pojemniki"] = 0;
                row["Palety"] = 0m;
                row["TrybE2"] = "";
                row["DataPrzyjecia"] = day;
                row["GodzinaPrzyjecia"] = "";
                row["TerminOdbioru"] = day.ToString("yyyy-MM-dd dddd", cultureInfo);
                row["DataUboju"] = DBNull.Value;
                row["UtworzonePrzez"] = "";
                row["Status"] = "Wydanie bez zamówienia";
                row["MaNotatke"] = false;
                releasesWithoutOrders.Add(row);

                totalReleased += released;
            }

            foreach (var row in releasesWithoutOrders.OrderByDescending(r => (decimal)r["IloscFaktyczna"]))
                _dtOrders.Rows.Add(row.ItemArray);

            // Sortowanie po handlowcu
            if (_dtOrders.Rows.Count > 0)
            {
                // Utworz tymczasową kopię wszystkich danych
                var tempData = new List<object[]>();
                foreach (DataRow row in _dtOrders.Rows)
                {
                    var rowCopy = new object[row.ItemArray.Length];
                    row.ItemArray.CopyTo(rowCopy, 0);
                    tempData.Add(rowCopy);
                }

                // Posortuj według kolumny Handlowiec (indeks 3)
                var sortedData = tempData.OrderBy(arr => arr[3]?.ToString() ?? "").ToList();

                // Wyczyść i dodaj posortowane dane
                _dtOrders.Rows.Clear();

                foreach (var rowData in sortedData)
                {
                    _dtOrders.Rows.Add(rowData);
                }
            }

            // Potem dodaj wiersz sumy na początek
            if (_dtOrders.Rows.Count > 0)
            {
                var summaryRow = _dtOrders.NewRow();
                summaryRow["Id"] = -1;
                summaryRow["KlientId"] = 0;
                summaryRow["Odbiorca"] = "═══ SUMA ═══";
                summaryRow["Handlowiec"] = $"Zamówień: {totalOrdersCount}";
                summaryRow["IloscZamowiona"] = totalOrdered;
                summaryRow["IloscFaktyczna"] = totalReleased;
                summaryRow["Pojemniki"] = 0;
                summaryRow["Palety"] = totalPallets;
                summaryRow["TrybE2"] = "";
                summaryRow["DataPrzyjecia"] = day;
                summaryRow["GodzinaPrzyjecia"] = "";
                summaryRow["TerminOdbioru"] = "";
                summaryRow["DataUboju"] = DBNull.Value;
                summaryRow["UtworzonePrzez"] = "";
                summaryRow["Status"] = "SUMA";
                summaryRow["MaNotatke"] = false;
                summaryRow["MaFolie"] = false;

                _dtOrders.Rows.InsertAt(summaryRow, 0);
            }

            SetupOrdersDataGrid();
            ApplyFilters();
        }
        private void SetupOrdersDataGrid()
        {
            dgOrders.ItemsSource = _dtOrders.DefaultView;
            dgOrders.Columns.Clear();

            dgOrders.LoadingRow -= DgOrders_LoadingRow;
            dgOrders.LoadingRow += DgOrders_LoadingRow;

            dgOrders.Columns.Add(new DataGridTextColumn
            {
                Header = "Odbiorca",
                Binding = new System.Windows.Data.Binding("Odbiorca"),
                Width = DataGridLength.Auto,  // ZMIENIONE z 2.5* na Auto
                ElementStyle = (Style)FindResource("RightAlignedCellStyle")
            });

            dgOrders.Columns.Add(new DataGridTextColumn
            {
                Header = "Handlowiec",
                Binding = new System.Windows.Data.Binding("Handlowiec"),
                Width = DataGridLength.Auto,
                ElementStyle = (Style)FindResource("BoldCellStyle")
            });

            dgOrders.Columns.Add(new DataGridTextColumn
            {
                Header = "Zamówiono",
                Binding = new System.Windows.Data.Binding("IloscZamowiona") { StringFormat = "N0" },
                Width = DataGridLength.Auto,
                ElementStyle = (Style)FindResource("RightAlignedCellStyle")
            });

            dgOrders.Columns.Add(new DataGridTextColumn
            {
                Header = "Wydano",
                Binding = new System.Windows.Data.Binding("IloscFaktyczna") { StringFormat = "N0" },
                Width = DataGridLength.Auto,
                ElementStyle = (Style)FindResource("RightAlignedCellStyle")
            });

            dgOrders.Columns.Add(new DataGridTextColumn
            {
                Header = "Palety",
                Binding = new System.Windows.Data.Binding("Palety") { StringFormat = "N1" },
                Width = new DataGridLength(60),
                ElementStyle = (Style)FindResource("CenterAlignedCellStyle")
            });

            dgOrders.Columns.Add(new DataGridTextColumn
            {
                Header = "Termin Odbioru",
                Binding = new System.Windows.Data.Binding("TerminOdbioru"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = 100
            });

            dgOrders.Columns.Add(new DataGridTextColumn
            {
                Header = "Utworzone przez",
                Binding = new System.Windows.Data.Binding("UtworzonePrzez"),
                Width = new DataGridLength(0.8, DataGridLengthUnitType.Star),
                MinWidth = 80
            });
        }
        private void DgOrders_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                var status = rowView.Row.Field<string>("Status") ?? "";
                var salesman = rowView.Row.Field<string>("Handlowiec") ?? "";
                var id = rowView.Row.Field<int>("Id");

                if (id == -1 || status == "SUMA")
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(92, 138, 58));
                    e.Row.Foreground = Brushes.White;
                    e.Row.FontWeight = FontWeights.Bold;
                    e.Row.FontSize = 14;
                    return;
                }

                if (status == "Wydanie bez zamówienia")
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(255, 240, 200));
                    e.Row.FontStyle = FontStyles.Italic;
                }
                else if (status == "Anulowane")
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(255, 230, 230));
                    e.Row.Foreground = new SolidColorBrush(Colors.Gray);
                }
                else if (!string.IsNullOrEmpty(salesman))
                {
                    var color = GetColorForSalesman(salesman);
                    e.Row.Background = new SolidColorBrush(color);
                }

                var pallets = rowView.Row.Field<decimal?>("Palety") ?? 0m;
                if (pallets > 34)
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(255, 100, 100));
                    e.Row.Foreground = Brushes.White;
                }
            }
        }

        private Color GetColorForSalesman(string salesman)
        {
            if (string.IsNullOrEmpty(salesman))
                return Colors.White;

            if (!_salesmanColors.ContainsKey(salesman))
            {
                _salesmanColors[salesman] = _colorPalette[_colorIndex % _colorPalette.Count];
                _colorIndex++;
            }
            return _salesmanColors[salesman];
        }

        private async Task<Dictionary<int, Dictionary<int, decimal>>> GetReleasesPerClientProductAsync(DateTime day)
        {
            day = ValidateSqlDate(day);
            var dict = new Dictionary<int, Dictionary<int, decimal>>();
            if (!_productCatalogCache.Keys.Any()) return dict;

            await using var cn = new SqlConnection(_connHandel);
            await cn.OpenAsync();
            var idwList = string.Join(",", _productCatalogCache.Keys);

            string sql = $@"SELECT MG.khid, MZ.idtw, SUM(ABS(MZ.ilosc)) AS qty 
                   FROM [HANDEL].[HM].[MZ] MZ 
                   JOIN [HANDEL].[HM].[MG] ON MZ.super = MG.id 
                   WHERE MG.seria IN ('sWZ','sWZ-W') AND MG.aktywny = 1 
                   AND MG.data = @Day AND MG.khid IS NOT NULL AND MZ.idtw IN ({idwList}) 
                   GROUP BY MG.khid, MZ.idtw";

            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@Day", day);
            await using var rdr = await cmd.ExecuteReaderAsync();

            while (await rdr.ReadAsync())
            {
                int clientId = rdr.IsDBNull(0) ? 0 : rdr.GetInt32(0);
                int productId = rdr.GetInt32(1);
                decimal qty = rdr.IsDBNull(2) ? 0m : Convert.ToDecimal(rdr.GetValue(2));

                if (!dict.TryGetValue(clientId, out var perProduct))
                {
                    perProduct = new Dictionary<int, decimal>();
                    dict[clientId] = perProduct;
                }

                if (perProduct.ContainsKey(productId))
                    perProduct[productId] += qty;
                else
                    perProduct[productId] = qty;
            }

            return dict;
        }

        private async Task DisplayOrderDetailsAsync(int orderId)
        {
            try
            {
                dgDetails.ItemsSource = null;
                txtNotes.Clear();

                int clientId = 0;
                string notes = "";
                var orderItems = new List<(int ProductCode, decimal Quantity, bool Foil)>();
                DateTime dateForReleases = ValidateSqlDate(_selectedDate.Date);

                await using (var cn = new SqlConnection(_connLibra))
                {
                    await cn.OpenAsync();

                    string slaughterDateSelect = _slaughterDateColumnExists ? ", DataUboju" : "";
                    using (var cmdInfo = new SqlCommand($@"
                        SELECT KlientId, Uwagi, DataZamowienia{slaughterDateSelect} 
                        FROM dbo.ZamowieniaMieso 
                        WHERE Id = @Id", cn))
                    {
                        cmdInfo.Parameters.AddWithValue("@Id", orderId);
                        using var readerInfo = await cmdInfo.ExecuteReaderAsync();

                        if (await readerInfo.ReadAsync())
                        {
                            clientId = readerInfo.IsDBNull(0) ? 0 : readerInfo.GetInt32(0);
                            notes = readerInfo.IsDBNull(1) ? "" : readerInfo.GetString(1);

                            var orderDate = readerInfo.GetDateTime(2);
                            DateTime? slaughterDate = null;

                            if (_slaughterDateColumnExists && !readerInfo.IsDBNull(3))
                            {
                                slaughterDate = readerInfo.GetDateTime(3);
                            }

                            dateForReleases = (_showBySlaughterDate && slaughterDate.HasValue)
                                ? slaughterDate.Value
                                : orderDate;
                        }
                    }

                    using (var cmdItems = new SqlCommand(@"
                        SELECT KodTowaru, Ilosc, ISNULL(Folia, 0) as Folia
                        FROM dbo.ZamowieniaMiesoTowar
                        WHERE ZamowienieId = @Id", cn))
                    {
                        cmdItems.Parameters.AddWithValue("@Id", orderId);
                        using var readerItems = await cmdItems.ExecuteReaderAsync();

                        while (await readerItems.ReadAsync())
                        {
                            int productCode = readerItems.GetInt32(0);
                            decimal quantity = readerItems.IsDBNull(1) ? 0m : readerItems.GetDecimal(1);
                            bool foil = readerItems.GetBoolean(2);

                            orderItems.Add((productCode, quantity, foil));
                        }
                    }
                }

                var releases = new Dictionary<int, decimal>();
                if (clientId > 0)
                {
                    await using (var cn = new SqlConnection(_connHandel))
                    {
                        await cn.OpenAsync();
                        const string sql = @"
                    SELECT MZ.idtw, SUM(ABS(MZ.ilosc)) 
                    FROM [HANDEL].[HM].[MZ] MZ 
                    JOIN [HANDEL].[HM].[MG] ON MZ.super = MG.id 
                    WHERE MG.seria IN ('sWZ','sWZ-W') 
                    AND MG.aktywny = 1 
                    AND MG.data = @Day 
                    AND MG.khid = @ClientId 
                    GROUP BY MZ.idtw";

                        await using var cmd = new SqlCommand(sql, cn);
                        cmd.Parameters.AddWithValue("@Day", dateForReleases);
                        cmd.Parameters.AddWithValue("@ClientId", clientId);
                        using var rd = await cmd.ExecuteReaderAsync();

                        while (await rd.ReadAsync())
                        {
                            int productId = rd.IsDBNull(0) ? 0 : rd.GetInt32(0);
                            decimal quantity = rd.IsDBNull(1) ? 0m : Convert.ToDecimal(rd.GetValue(1));
                            releases[productId] = quantity;
                        }
                    }
                }

                var dt = new DataTable();
                dt.Columns.Add("Produkt", typeof(string));
                dt.Columns.Add("Zamówiono", typeof(decimal));
                dt.Columns.Add("Wydano", typeof(decimal));
                dt.Columns.Add("Różnica", typeof(decimal));
                dt.Columns.Add("Folia", typeof(string));

                foreach (var item in orderItems)
                {
                    if (!_productCatalogCache.ContainsKey(item.ProductCode))
                        continue;

                    string product = _productCatalogCache.TryGetValue(item.ProductCode, out var code) ?
                        code : $"Nieznany ({item.ProductCode})";
                    decimal ordered = item.Quantity;
                    decimal released = releases.TryGetValue(item.ProductCode, out var w) ? w : 0m;
                    decimal difference = released - ordered;

                    dt.Rows.Add(product, ordered, released, difference, item.Foil ? "TAK" : "NIE");
                    releases.Remove(item.ProductCode);
                }

                foreach (var kv in releases)
                {
                    if (!_productCatalogCache.ContainsKey(kv.Key))
                        continue;

                    string product = _productCatalogCache.TryGetValue(kv.Key, out var code) ?
                        code : $"Nieznany ({kv.Key})";
                    dt.Rows.Add(product, 0m, kv.Value, kv.Value, "B/D");
                }

                txtNotes.Text = notes;

                if (dt.Rows.Count > 0)
                {
                    dgDetails.ItemsSource = dt.DefaultView;
                    SetupDetailsDataGrid();
                }
                else
                {
                    var dtEmpty = new DataTable();
                    dtEmpty.Columns.Add("Info", typeof(string));
                    dtEmpty.Rows.Add("Brak pozycji w zamówieniu");
                    dgDetails.ItemsSource = dtEmpty.DefaultView;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas wczytywania szczegółów zamówienia:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                dgDetails.ItemsSource = null;
                txtNotes.Clear();
            }
        }

        private void SetupDetailsDataGrid()
        {
            dgDetails.Columns.Clear();

            dgDetails.Columns.Add(new DataGridTextColumn
            {
                Header = "Produkt",
                Binding = new System.Windows.Data.Binding("Produkt"),
                Width = new DataGridLength(1.5, DataGridLengthUnitType.Star)
            });

            dgDetails.Columns.Add(new DataGridTextColumn
            {
                Header = "Zamówiono",
                Binding = new System.Windows.Data.Binding("Zamówiono") { StringFormat = "N0" },
                Width = DataGridLength.Auto,
                ElementStyle = (Style)FindResource("RightAlignedCellStyle")
            });

            dgDetails.Columns.Add(new DataGridTextColumn
            {
                Header = "Wydano",
                Binding = new System.Windows.Data.Binding("Wydano") { StringFormat = "N0" },
                Width = DataGridLength.Auto,
                ElementStyle = (Style)FindResource("RightAlignedCellStyle")
            });

            dgDetails.Columns.Add(new DataGridTextColumn
            {
                Header = "Różnica",
                Binding = new System.Windows.Data.Binding("Różnica") { StringFormat = "N0" },
                Width = DataGridLength.Auto,
                ElementStyle = (Style)FindResource("RightAlignedCellStyle")
            });

            dgDetails.Columns.Add(new DataGridTextColumn
            {
                Header = "Folia",
                Binding = new System.Windows.Data.Binding("Folia"),
                Width = new DataGridLength(70),
                ElementStyle = (Style)FindResource("CenterAlignedCellStyle")
            });
        }
        private async Task DisplayReleaseWithoutOrderDetailsAsync(int clientId, DateTime day)
        {
            day = ValidateSqlDate(day);

            var dt = new DataTable();
            dt.Columns.Add("Produkt", typeof(string));
            dt.Columns.Add("Wydano", typeof(decimal));

            string recipientName = "Nieznany";
            await using (var cn = new SqlConnection(_connHandel))
            {
                await cn.OpenAsync();
                var cmdName = new SqlCommand("SELECT Shortcut FROM [HANDEL].[SSCommon].[STContractors] WHERE Id = @Id", cn);
                cmdName.Parameters.AddWithValue("@Id", clientId);
                var result = await cmdName.ExecuteScalarAsync();
                if (result != null)
                    recipientName = result.ToString() ?? $"KH {clientId}";
            }

            if (clientId > 0)
            {
                await using (var cn = new SqlConnection(_connHandel))
                {
                    await cn.OpenAsync();
                    const string sql = @"
                        SELECT MZ.idtw, SUM(ABS(MZ.ilosc)) 
                        FROM [HANDEL].[HM].[MZ] MZ 
                        JOIN [HANDEL].[HM].[MG] ON MZ.super = MG.id 
                        JOIN [HANDEL].[HM].[TW] ON MZ.idtw = TW.id 
                        WHERE MG.seria IN ('sWZ','sWZ-W') 
                        AND MG.aktywny = 1 
                        AND MG.data = @Day 
                        AND MG.khid = @ClientId 
                        AND TW.katalog IN (67095, 67153) 
                        GROUP BY MZ.idtw
                        ORDER BY SUM(ABS(MZ.ilosc)) DESC";

                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Day", day.Date);
                    cmd.Parameters.AddWithValue("@ClientId", clientId);
                    using var rd = await cmd.ExecuteReaderAsync();

                    while (await rd.ReadAsync())
                    {
                        int productId = rd.IsDBNull(0) ? 0 : rd.GetInt32(0);
                        decimal quantity = rd.IsDBNull(1) ? 0m : Convert.ToDecimal(rd.GetValue(1));
                        string product = _productCatalogCache.TryGetValue(productId, out var code)
                            ? code : $"Nieznany ({productId})";
                        dt.Rows.Add(product, quantity);
                    }
                }
            }

            txtNotes.Text = $"📦 Wydanie bez zamówienia\n\n" +
                           $"Odbiorca: {recipientName} (ID: {clientId})\n" +
                           $"Data: {day:yyyy-MM-dd dddd}\n\n" +
                           $"Poniżej lista wydanych produktów (tylko towary z katalogów 67095 i 67153)";

            dgDetails.ItemsSource = dt.DefaultView;
            dgDetails.Columns.Clear();

            dgDetails.Columns.Add(new DataGridTextColumn
            {
                Header = "Produkt",
                Binding = new System.Windows.Data.Binding("Produkt"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });

            dgDetails.Columns.Add(new DataGridTextColumn
            {
                Header = "Wydano (kg)",
                Binding = new System.Windows.Data.Binding("Wydano") { StringFormat = "N0" },
                Width = new DataGridLength(120),
                ElementStyle = (Style)FindResource("RightAlignedCellStyle")
            });
        }

        private async Task DisplayProductAggregationAsync(DateTime day)
        {
            day = ValidateSqlDate(day);

            var dtAgg = new DataTable();
            dtAgg.Columns.Add("Produkt", typeof(string));
            dtAgg.Columns.Add("PlanowanyPrzychód", typeof(decimal));
            dtAgg.Columns.Add("FaktycznyPrzychód", typeof(decimal));
            dtAgg.Columns.Add("Zamówienia", typeof(decimal));
            dtAgg.Columns.Add("Bilans", typeof(decimal));

            var (wspolczynnikTuszki, procentA, procentB) = await GetKonfiguracjaWydajnosciAsync(day);
            var konfiguracjaProduktow = await GetKonfiguracjaProduktowAsync(day);

            decimal totalMassDek = 0m;
            await using (var cn = new SqlConnection(_connLibra))
            {
                await cn.OpenAsync();
                const string sql = @"SELECT WagaDek, SztukiDek FROM dbo.HarmonogramDostaw 
                    WHERE DataOdbioru = @Day AND Bufor = 'Potwierdzony'";
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Day", day.Date);
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

            // PRZYCHODY FAKTYCZNE DLA KURCZAK A (sPWU)
            var actualIncomeTuszkaA = new Dictionary<int, decimal>();
            await using (var cn = new SqlConnection(_connHandel))
            {
                await cn.OpenAsync();
                const string sql = @"SELECT MZ.idtw, SUM(ABS(MZ.ilosc)) 
                    FROM [HANDEL].[HM].[MZ] MZ 
                    JOIN [HANDEL].[HM].[MG] ON MZ.super = MG.id 
                    WHERE MG.seria = 'sPWU' AND MG.aktywny=1 AND MG.data = @Day 
                    GROUP BY MZ.idtw";
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Day", day.Date);
                await using var rdr = await cmd.ExecuteReaderAsync();

                while (await rdr.ReadAsync())
                {
                    int productId = rdr.GetInt32(0);
                    decimal qty = rdr.IsDBNull(1) ? 0m : Convert.ToDecimal(rdr.GetValue(1));
                    actualIncomeTuszkaA[productId] = qty;
                }
            }

            // PRZYCHODY FAKTYCZNE DLA KURCZAK B I ELEMENTÓW (sPWP)
            var actualIncomeElementy = new Dictionary<int, decimal>();
            await using (var cn = new SqlConnection(_connHandel))
            {
                await cn.OpenAsync();
                const string sql = @"SELECT MZ.idtw, SUM(ABS(MZ.ilosc)) 
                    FROM [HANDEL].[HM].[MZ] MZ 
                    JOIN [HANDEL].[HM].[MG] ON MZ.super = MG.id 
                    WHERE MG.seria IN ('sPWP', 'PWP') AND MG.aktywny=1 AND MG.data = @Day 
                    GROUP BY MZ.idtw";
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Day", day.Date);
                await using var rdr = await cmd.ExecuteReaderAsync();

                while (await rdr.ReadAsync())
                {
                    int productId = rdr.GetInt32(0);
                    decimal qty = rdr.IsDBNull(1) ? 0m : Convert.ToDecimal(rdr.GetValue(1));
                    actualIncomeElementy[productId] = qty;
                }
            }

            var orderSum = new Dictionary<int, decimal>();
            var orderIds = _dtOrders.AsEnumerable()
                .Where(r => !string.Equals(r.Field<string>("Status"), "Anulowane", StringComparison.OrdinalIgnoreCase))
                .Where(r => r.Field<string>("Status") != "SUMA")
                .Select(r => r.Field<int>("Id"))
                .Where(id => id > 0)
                .ToList();

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

            // ZMIANA: Szukaj produktu "Kurczak A" zamiast "Kurczak A"
            var kurczakA = _productCatalogCache.FirstOrDefault(p =>
                p.Value.Contains("Kurczak A", StringComparison.OrdinalIgnoreCase));

            decimal planA = 0m, factA = 0m, ordersA = 0m, balanceA = 0m;

            if (kurczakA.Key > 0)
            {
                planA = pulaTuszkiA;
                factA = actualIncomeTuszkaA.TryGetValue(kurczakA.Key, out var a) ? a : 0m;
                ordersA = orderSum.TryGetValue(kurczakA.Key, out var z) ? z : 0m;
                balanceA = factA - ordersA;
            }

            decimal sumaPlanB = 0m;
            decimal sumaFaktB = 0m;
            decimal sumaZamB = 0m;

            var produktyB = new List<(string nazwa, decimal plan, decimal fakt, decimal zam, decimal bilans)>();

            foreach (var produktKonfig in konfiguracjaProduktow.OrderByDescending(p => p.Value))
            {
                int produktId = produktKonfig.Key;
                decimal procentUdzialu = produktKonfig.Value;

                if (!_productCatalogCache.ContainsKey(produktId))
                    continue;

                string nazwaProdukt = _productCatalogCache[produktId];

                // ZMIANA: Pomiń "Kurczak B" zamiast "Kurczak B"
                if (nazwaProdukt.Contains("Kurczak B", StringComparison.OrdinalIgnoreCase))
                    continue;

                // DODAJ IKONKĘ
                string ikona = "";
                if (nazwaProdukt.Contains("Skrzydło", StringComparison.OrdinalIgnoreCase))
                    ikona = "🍗";
                else if (nazwaProdukt.Contains("Korpus", StringComparison.OrdinalIgnoreCase))
                    ikona = "🍖";
                else if (nazwaProdukt.Contains("Ćwiartka", StringComparison.OrdinalIgnoreCase))
                    ikona = "🍗";
                else if (nazwaProdukt.Contains("Filet", StringComparison.OrdinalIgnoreCase))
                    ikona = "🥩";

                decimal plannedForProduct = pulaTuszkiB * (procentUdzialu / 100m);
                var actual = actualIncomeElementy.TryGetValue(produktId, out var a) ? a : 0m;
                var orders = orderSum.TryGetValue(produktId, out var z) ? z : 0m;
                var balance = actual - orders;

                string nazwaZIkonka = string.IsNullOrEmpty(ikona)
                    ? $"  └ {nazwaProdukt} ({procentUdzialu:F1}%)"
                    : $"  └ {ikona} {nazwaProdukt} ({procentUdzialu:F1}%)";

                produktyB.Add((nazwaZIkonka, plannedForProduct, actual, orders, balance));

                sumaPlanB += plannedForProduct;
                sumaFaktB += actual;
                sumaZamB += orders;
            }

            decimal bilansB = sumaFaktB - sumaZamB;

            dtAgg.Rows.Add("═══ SUMA CAŁKOWITA ═══", planA + sumaPlanB, factA + sumaFaktB, ordersA + sumaZamB, balanceA + bilansB);

            // ZMIANA: Wyświetlaj "Kurczak A" i "Kurczak B"
            dtAgg.Rows.Add("🐔 Kurczak A", planA, factA, ordersA, balanceA);
            dtAgg.Rows.Add("🐔 Kurczak B", sumaPlanB, sumaFaktB, sumaZamB, bilansB);

            foreach (var produkt in produktyB)
            {
                dtAgg.Rows.Add(produkt.nazwa, produkt.plan, produkt.fakt, produkt.zam, produkt.bilans);
            }

            dgAggregation.ItemsSource = dtAgg.DefaultView;
            SetupAggregationDataGrid();
        }

        private void DgAggregation_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                var produkt = rowView.Row.Field<string>("Produkt") ?? "";

                // SUMA CAŁKOWITA - ciemnozielony
                if (produkt.StartsWith("═══ SUMA CAŁKOWITA"))
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                    e.Row.Foreground = Brushes.White;
                    e.Row.FontWeight = FontWeights.Bold;
                    e.Row.FontSize = 14;
                }
                // KURCZAK A - jasnozielony (ZMIANA)
                else if (produkt.StartsWith("🐔 Kurczak A"))
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(200, 230, 201));
                    e.Row.FontWeight = FontWeights.SemiBold;
                    e.Row.FontSize = 13;
                }
                // KURCZAK B - jasnoniebieski (ZMIANA)
                else if (produkt.StartsWith("🐔 Kurczak B"))
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(179, 229, 252));
                    e.Row.FontWeight = FontWeights.SemiBold;
                    e.Row.FontSize = 13;
                }
                // ELEMENTY - bardzo jasny niebieski
                else if (produkt.StartsWith("  └"))
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(225, 245, 254));
                    e.Row.FontStyle = FontStyles.Normal;
                    e.Row.FontSize = 11;
                }
            }
        }
        private void SetupAggregationDataGrid()
        {
            dgAggregation.Columns.Clear();

            // Kolumna Plan z przekreśleniem gdy jest faktyczny przychód
            var planColumn = new DataGridTemplateColumn
            {
                Header = "Plan",
                Width = DataGridLength.Auto
            };

            var planTemplate = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(TextBlock));
            factory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("PlanowanyPrzychód")
            {
                StringFormat = "N0"
            });
            factory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right);
            factory.SetValue(TextBlock.PaddingProperty, new Thickness(0, 0, 8, 0));

            // Dodaj MultiBinding dla TextDecorations
            var multiBinding = new System.Windows.Data.MultiBinding();
            multiBinding.Converter = new StrikethroughConverter();
            multiBinding.Bindings.Add(new System.Windows.Data.Binding("FaktycznyPrzychód"));
            factory.SetBinding(TextBlock.TextDecorationsProperty, multiBinding);

            planTemplate.VisualTree = factory;
            planColumn.CellTemplate = planTemplate;
            dgAggregation.Columns.Add(planColumn);

            dgAggregation.Columns.Add(new DataGridTextColumn
            {
                Header = "Fakt",
                Binding = new System.Windows.Data.Binding("FaktycznyPrzychód") { StringFormat = "N0" },
                Width = DataGridLength.Auto,
                ElementStyle = (Style)FindResource("RightAlignedCellStyle")
            });

            dgAggregation.Columns.Add(new DataGridTextColumn
            {
                Header = "Zam.",
                Binding = new System.Windows.Data.Binding("Zamówienia") { StringFormat = "N0" },
                Width = DataGridLength.Auto,
                ElementStyle = (Style)FindResource("RightAlignedCellStyle")
            });

            dgAggregation.Columns.Add(new DataGridTextColumn
            {
                Header = "Bilans",
                Binding = new System.Windows.Data.Binding("Bilans") { StringFormat = "N0" },
                Width = DataGridLength.Auto,
                ElementStyle = (Style)FindResource("RightAlignedCellStyle")
            });

            dgAggregation.Columns.Add(new DataGridTextColumn
            {
                Header = "Produkt",
                Binding = new System.Windows.Data.Binding("Produkt"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });

            dgAggregation.LoadingRow -= DgAggregation_LoadingRow;
            dgAggregation.LoadingRow += DgAggregation_LoadingRow;

            // DODAJ TO NA KOŃCU:
            dgAggregation.MouseDoubleClick -= DgAggregation_MouseDoubleClick;
            dgAggregation.MouseDoubleClick += DgAggregation_MouseDoubleClick;
        }
        private async void DgAggregation_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgAggregation.SelectedItem is DataRowView rowView)
            {
                var produktNazwa = rowView.Row.Field<string>("Produkt") ?? "";

                // Ignoruj TYLKO wiersz sumy całkowitej
                if (produktNazwa.StartsWith("═══"))
                    return;

                // Usuń ikonki i spacje z nazwy produktu
                var czystyProdukt = produktNazwa
                    .Replace("└", "")
                    .Replace("🍗", "")
                    .Replace("🍖", "")
                    .Replace("🥩", "")
                    .Replace("🐔", "")
                    .Replace("  ", " ")
                    .Trim();

                // Wyciągnij nazwę produktu (bez procentu w nawiasie)
                int indexProcentu = czystyProdukt.IndexOf("(");
                if (indexProcentu > 0)
                    czystyProdukt = czystyProdukt.Substring(0, indexProcentu).Trim();

                var plan = rowView.Row.Field<decimal?>("PlanowanyPrzychód") ?? 0m;
                var fakt = rowView.Row.Field<decimal?>("FaktycznyPrzychód") ?? 0m;
                var zamowienia = rowView.Row.Field<decimal?>("Zamówienia") ?? 0m;
                var bilans = rowView.Row.Field<decimal?>("Bilans") ?? 0m;

                // Znajdź ID produktu
                int? produktId = await ZnajdzIdProduktuAsync(czystyProdukt);

                if (!produktId.HasValue)
                {
                    MessageBox.Show($"Nie można znaleźć produktu w bazie:\n\n" +
                                   $"Szukany: '{czystyProdukt}'\n" +
                                   $"Oryginalny: '{produktNazwa}'\n\n" +
                                   $"Sprawdź czy produkt istnieje w katalogu 67095 lub 67153.",
                        "Produkt nie znaleziony", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Otwórz okno z potencjalnymi odbiorcami
                var okno = new PotencjalniOdbiorcy(
                    _connHandel,
                    produktId.Value,
                    czystyProdukt,
                    plan,
                    fakt,
                    zamowienia,
                    bilans,
                    _selectedDate);
                okno.ShowDialog();
            }
        }
        private async Task<int?> ZnajdzIdProduktuAsync(string nazwaProdukt)
        {
            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();

                // Najpierw spróbuj dokładnego dopasowania
                var cmd = new SqlCommand(@"
            SELECT TOP 1 ID 
            FROM [HANDEL].[HM].[TW] 
            WHERE kod = @Nazwa
              AND katalog IN (67095, 67153)", cn);
                cmd.Parameters.AddWithValue("@Nazwa", nazwaProdukt.Trim());

                var result = await cmd.ExecuteScalarAsync();

                // Jeśli nie znaleziono, spróbuj LIKE
                if (result == null)
                {
                    cmd = new SqlCommand(@"
                SELECT TOP 1 ID 
                FROM [HANDEL].[HM].[TW] 
                WHERE kod LIKE @Nazwa + '%'
                  AND katalog IN (67095, 67153)
                ORDER BY LEN(kod)", cn);
                    cmd.Parameters.AddWithValue("@Nazwa", nazwaProdukt.Trim());

                    result = await cmd.ExecuteScalarAsync();
                }

                // Jeśli nadal nie znaleziono, spróbuj wyszukać po fragmencie
                if (result == null)
                {
                    cmd = new SqlCommand(@"
                SELECT TOP 1 ID 
                FROM [HANDEL].[HM].[TW] 
                WHERE kod LIKE '%' + @Nazwa + '%'
                  AND katalog IN (67095, 67153)
                ORDER BY 
                    CASE WHEN kod = @Nazwa THEN 0
                         WHEN kod LIKE @Nazwa + '%' THEN 1
                         ELSE 2 END,
                    LEN(kod)", cn);
                    cmd.Parameters.AddWithValue("@Nazwa", nazwaProdukt.Trim());

                    result = await cmd.ExecuteScalarAsync();
                }

                return result != null ? Convert.ToInt32(result) : (int?)null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas wyszukiwania produktu:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        #endregion
        #region Helper Methods

        private void ApplyFilters()
        {
            if (_dtOrders.DefaultView == null) return;

            var conditions = new List<string>();

            var txt = txtFilterRecipient.Text?.Trim().Replace("'", "''");
            if (!string.IsNullOrEmpty(txt))
                conditions.Add($"Odbiorca LIKE '%{txt}%'");

            if (cbFilterSalesman.SelectedIndex > 0)
            {
                var salesman = cbFilterSalesman.SelectedItem?.ToString()?.Replace("'", "''");
                if (!string.IsNullOrEmpty(salesman))
                    conditions.Add($"Handlowiec = '{salesman}'");
            }

            if (!_showReleasesWithoutOrders)
            {
                conditions.Add("Status <> 'Wydanie bez zamówienia'");
            }

            _dtOrders.DefaultView.RowFilter = string.Join(" AND ", conditions);
        }


        private void ClearDetails()
        {
            dgDetails.ItemsSource = null;
            txtNotes.Clear();
            _currentOrderId = null;
        }

        private async Task DuplicateOrderAsync(int sourceId, DateTime targetDate, bool copyNotes = false)
        {
            targetDate = ValidateSqlDate(targetDate);

            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            await using var tr = cn.BeginTransaction();

            try
            {
                int clientId = 0;
                string notes = "";
                DateTime arrivalTime = DateTime.Today.AddHours(8);
                int containers = 0;
                decimal pallets = 0m;
                bool modeE2 = false;

                using (var cmd = new SqlCommand(@"SELECT KlientId, Uwagi, DataPrzyjazdu, LiczbaPojemnikow, LiczbaPalet, TrybE2 
                                                 FROM ZamowieniaMieso WHERE Id = @Id", cn, tr))
                {
                    cmd.Parameters.AddWithValue("@Id", sourceId);
                    using var reader = await cmd.ExecuteReaderAsync();

                    if (await reader.ReadAsync())
                    {
                        clientId = reader.GetInt32(0);
                        notes = reader.IsDBNull(1) ? "" : reader.GetString(1);
                        arrivalTime = reader.GetDateTime(2);
                        arrivalTime = targetDate.Date.Add(arrivalTime.TimeOfDay);
                        containers = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                        pallets = reader.IsDBNull(4) ? 0m : reader.GetDecimal(4);
                        modeE2 = reader.IsDBNull(5) ? false : reader.GetBoolean(5);
                    }
                }

                var cmdGetId = new SqlCommand("SELECT ISNULL(MAX(Id),0)+1 FROM ZamowieniaMieso", cn, tr);
                int newId = Convert.ToInt32(await cmdGetId.ExecuteScalarAsync());

                var cmdInsert = new SqlCommand(@"INSERT INTO ZamowieniaMieso 
                    (Id, DataZamowienia, DataPrzyjazdu, KlientId, Uwagi, IdUser, DataUtworzenia, 
                     LiczbaPojemnikow, LiczbaPalet, TrybE2, TransportStatus) 
                    VALUES (@id, @dz, @dp, @kid, @uw, @u, GETDATE(), @poj, @pal, @e2, 'Oczekuje')", cn, tr);

                cmdInsert.Parameters.AddWithValue("@id", newId);
                cmdInsert.Parameters.AddWithValue("@dz", targetDate.Date);
                cmdInsert.Parameters.AddWithValue("@dp", arrivalTime);
                cmdInsert.Parameters.AddWithValue("@kid", clientId);

                string finalNotes = copyNotes && !string.IsNullOrEmpty(notes) ? notes : "";
                cmdInsert.Parameters.AddWithValue("@uw", string.IsNullOrEmpty(finalNotes) ? DBNull.Value : finalNotes);

                cmdInsert.Parameters.AddWithValue("@u", UserID);
                cmdInsert.Parameters.AddWithValue("@poj", containers);
                cmdInsert.Parameters.AddWithValue("@pal", pallets);
                cmdInsert.Parameters.AddWithValue("@e2", modeE2);
                await cmdInsert.ExecuteNonQueryAsync();

                var cmdCopyItems = new SqlCommand(@"INSERT INTO ZamowieniaMiesoTowar 
                    (ZamowienieId, KodTowaru, Ilosc, Cena, Pojemniki, Palety, E2) 
                    SELECT @newId, KodTowaru, Ilosc, Cena, Pojemniki, Palety, E2 
                    FROM ZamowieniaMiesoTowar WHERE ZamowienieId = @sourceId", cn, tr);
                cmdCopyItems.Parameters.AddWithValue("@newId", newId);
                cmdCopyItems.Parameters.AddWithValue("@sourceId", sourceId);
                await cmdCopyItems.ExecuteNonQueryAsync();

                await tr.CommitAsync();
            }
            catch
            {
                await tr.RollbackAsync();
                throw;
            }
        }

        #endregion

        public class StrikethroughConverter : IMultiValueConverter
        {
            public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
            {
                if (values.Length > 0 && values[0] != null && values[0] != DependencyProperty.UnsetValue)
                {
                    decimal faktycznyPrzychod = 0;
                    if (values[0] is decimal dec)
                        faktycznyPrzychod = dec;
                    else if (decimal.TryParse(values[0].ToString(), out var parsed))
                        faktycznyPrzychod = parsed;

                    if (faktycznyPrzychod > 0)
                    {
                        var textDecorations = new TextDecorationCollection();
                        var strikethrough = new TextDecoration
                        {
                            Location = TextDecorationLocation.Strikethrough,
                            Pen = new Pen(Brushes.Black, 2) // Gruba kreska (2 piksele)
                        };
                        textDecorations.Add(strikethrough);
                        return textDecorations;
                    }
                }

                return null;
            }

            public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }
    }
}