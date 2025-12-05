using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Kalendarz1.Services;


namespace Kalendarz1.WPF
{
    public partial class MainWindow : Window
    {
        private readonly string _connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly string _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private readonly string _connTransport = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private static readonly DateTime MinSqlDate = new DateTime(1753, 1, 1);
        private static readonly DateTime MaxSqlDate = new DateTime(9999, 12, 31);

        public string UserID { get; set; } = string.Empty;
        private DateTime _selectedDate;
        private int? _currentOrderId;
        private readonly List<Button> _dayButtons = new();
        private readonly Dictionary<Button, DateTime> _dayButtonDates = new();
        private Button? btnToday; // Dodaj to pole na początku klasy

        private bool _showBySlaughterDate = true;
        private bool _slaughterDateColumnExists = true;
        private bool _isInitialized = false;
        private bool _showAnulowane = false;
        private bool _isRefreshing = false;
        private System.Windows.Threading.DispatcherTimer _autoRefreshTimer;

        private readonly DataTable _dtOrders = new();
        private readonly DataTable _dtTransport = new();
        private readonly DataTable _dtHistoriaZmian = new();
        private readonly Dictionary<int, string> _productCodeCache = new();
        private readonly Dictionary<int, string> _productCatalogCache = new();
        private readonly Dictionary<int, string> _productCatalogSwieze = new();
        private readonly Dictionary<int, string> _productCatalogMrozone = new();
        private int? _selectedProductId = null;
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

        private DataRowView _contextMenuSelectedRow = null;
        private HashSet<int> _processedOrderIds = new HashSet<int>();

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
        private void ApplyResponsiveLayout()
        {
            try
            {
                double screenHeight = SystemParameters.PrimaryScreenHeight;
                double screenWidth = SystemParameters.PrimaryScreenWidth;

                System.Diagnostics.Debug.WriteLine($"Rozdzielczość ekranu: {screenWidth}x{screenHeight}");

                LayoutPreset preset;

                if (screenHeight <= 768)
                {
                    preset = LayoutPreset.Small;        // HD 1366x768
                    System.Diagnostics.Debug.WriteLine("Preset: Small (HD 768p)");
                }
                else if (screenHeight <= 900)
                {
                    preset = LayoutPreset.Medium;       // HD+ 1600x900
                    System.Diagnostics.Debug.WriteLine("Preset: Medium (HD+ 900p)");
                }
                else if (screenHeight <= 1080)
                {
                    preset = LayoutPreset.ExtraLarge;   // ✅ FHD 1920x1080
                    System.Diagnostics.Debug.WriteLine("Preset: ExtraLarge (FHD 1080p)");
                }
                else
                {
                    preset = LayoutPreset.Large;        // 2K/4K
                    System.Diagnostics.Debug.WriteLine($"Preset: Large (2K/4K {screenHeight}p)");
                }

                ApplyPreset(preset);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BŁĄD ApplyResponsiveLayout: {ex.Message}");
                ApplyPreset(LayoutPreset.ExtraLarge);
            }
        }
        private enum LayoutPreset
        {
            Small,
            Medium,
            Large,
            ExtraLarge
        }

        private void ApplyPreset(LayoutPreset preset)
        {
            try
            {
                switch (preset)
                {
                    case LayoutPreset.Small:
                        // HD 1366x768 - kompaktowy układ
                        if (dgOrders != null)
                        {
                            dgOrders.FontSize = 9;
                            dgOrders.RowHeight = 22;
                            SetHeaderStyle(dgOrders, 9, 28);
                        }

                        if (dgDetails != null)
                        {
                            dgDetails.FontSize = 9;
                            dgDetails.RowHeight = 20;
                            SetHeaderStyle(dgDetails, 9, 26);
                        }

                        if (dgAggregation != null)
                        {
                            dgAggregation.FontSize = 10;
                            dgAggregation.RowHeight = 28;
                            SetHeaderStyle(dgAggregation, 10, 32);
                        }

                        if (txtNotes != null)
                        {
                            txtNotes.FontSize = 10;
                        }
                        break;

                    case LayoutPreset.Medium:
                        // HD+ 1600x900 - standardowy układ
                        if (dgOrders != null)
                        {
                            dgOrders.FontSize = 10;
                            dgOrders.RowHeight = 26;
                            SetHeaderStyle(dgOrders, 10, 32);
                        }

                        if (dgDetails != null)
                        {
                            dgDetails.FontSize = 10;
                            dgDetails.RowHeight = 24;
                            SetHeaderStyle(dgDetails, 10, 28);
                        }

                        if (dgAggregation != null)
                        {
                            dgAggregation.FontSize = 11;
                            dgAggregation.RowHeight = 32;
                            SetHeaderStyle(dgAggregation, 11, 36);
                        }

                        if (txtNotes != null)
                        {
                            txtNotes.FontSize = 11;
                        }
                        break;

                    case LayoutPreset.Large:
                        // FHD 1920x1080 - komfortowy układ
                        if (dgOrders != null)
                        {
                            dgOrders.FontSize = 11;
                            dgOrders.RowHeight = 30;
                            SetHeaderStyle(dgOrders, 11, 36);
                        }

                        if (dgDetails != null)
                        {
                            dgDetails.FontSize = 11;
                            dgDetails.RowHeight = 28;
                            SetHeaderStyle(dgDetails, 11, 32);
                        }

                        if (dgAggregation != null)
                        {
                            dgAggregation.FontSize = 12;
                            dgAggregation.RowHeight = 36;
                            SetHeaderStyle(dgAggregation, 12, 40);
                        }

                        if (txtNotes != null)
                        {
                            txtNotes.FontSize = 12;
                        }
                        break;

                    case LayoutPreset.ExtraLarge:
                        // 2K/4K - duży układ
                        if (dgOrders != null)
                        {
                            dgOrders.FontSize = 12;
                            dgOrders.RowHeight = 35;
                            SetHeaderStyle(dgOrders, 12, 40);
                        }

                        if (dgDetails != null)
                        {
                            dgDetails.FontSize = 12;
                            dgDetails.RowHeight = 32;
                            SetHeaderStyle(dgDetails, 12, 36);
                        }

                        if (dgAggregation != null)
                        {
                            dgAggregation.FontSize = 13;
                            dgAggregation.RowHeight = 40;
                            SetHeaderStyle(dgAggregation, 13, 44);
                        }

                        if (txtNotes != null)
                        {
                            txtNotes.FontSize = 13;
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                // Loguj błąd, ale nie przerywaj działania aplikacji
                System.Diagnostics.Debug.WriteLine($"Błąd w ApplyPreset: {ex.Message}");
            }
        }
        private void SetHeaderStyle(DataGrid dataGrid, double fontSize, double height)
        {
            if (dataGrid == null) return;

            try
            {
                var headerStyle = new Style(typeof(DataGridColumnHeader));
                headerStyle.Setters.Add(new Setter(DataGridColumnHeader.BackgroundProperty,
                    new SolidColorBrush(Color.FromRgb(44, 62, 80))));
                headerStyle.Setters.Add(new Setter(DataGridColumnHeader.ForegroundProperty, Brushes.White));
                headerStyle.Setters.Add(new Setter(DataGridColumnHeader.FontWeightProperty, FontWeights.SemiBold));
                headerStyle.Setters.Add(new Setter(DataGridColumnHeader.FontSizeProperty, fontSize));
                headerStyle.Setters.Add(new Setter(DataGridColumnHeader.PaddingProperty, new Thickness(8, 4, 8, 4)));
                headerStyle.Setters.Add(new Setter(DataGridColumnHeader.HeightProperty, height));
                headerStyle.Setters.Add(new Setter(DataGridColumnHeader.HorizontalContentAlignmentProperty,
                    HorizontalAlignment.Left));

                dataGrid.ColumnHeaderStyle = headerStyle;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd w SetHeaderStyle dla {dataGrid.Name}: {ex.Message}");
            }
        }
        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                double windowHeight = this.ActualHeight;
                double windowWidth = this.ActualWidth;

                // Domyślne wartości bazowe
                const double DEFAULT_ORDER_FONT = 12;
                const double DEFAULT_ORDER_ROW = 35;
                const double DEFAULT_AGG_FONT = 13;
                const double DEFAULT_AGG_ROW = 40;
                const double DEFAULT_DETAILS_FONT = 10;
                const double DEFAULT_DETAILS_ROW = 26;

                double availableHeight = windowHeight - 180;

                // === DOPASOWANIE dgOrders ===
                int orderRowCount = _dtOrders.Rows.Count;

                if (orderRowCount > 0 && availableHeight > 0)
                {
                    double optimalRowHeight = availableHeight / (orderRowCount + 1);

                    // Skalowanie w dół tylko gdy potrzebne
                    if (orderRowCount > 25 || optimalRowHeight < 24)
                    {
                        dgOrders.FontSize = 9;
                        dgOrders.RowHeight = Math.Max(20, Math.Min(optimalRowHeight, 24));
                    }
                    else if (orderRowCount > 20 || optimalRowHeight < 28)
                    {
                        dgOrders.FontSize = 10;
                        dgOrders.RowHeight = Math.Max(24, Math.Min(optimalRowHeight, 28));
                    }
                    else if (orderRowCount > 15 || optimalRowHeight < 32)
                    {
                        dgOrders.FontSize = 11;
                        dgOrders.RowHeight = Math.Max(28, Math.Min(optimalRowHeight, 32));
                    }
                    else
                    {
                        // ✅ RESET DO DOMYŚLNYCH gdy jest dużo miejsca
                        dgOrders.FontSize = DEFAULT_ORDER_FONT;
                        dgOrders.RowHeight = DEFAULT_ORDER_ROW;
                    }

                    // Dynamiczny styl nagłówka
                    var headerHeight = dgOrders.RowHeight + 8;
                    var headerStyle = new Style(typeof(DataGridColumnHeader));
                    headerStyle.Setters.Add(new Setter(DataGridColumnHeader.BackgroundProperty, new SolidColorBrush(Color.FromRgb(44, 62, 80))));
                    headerStyle.Setters.Add(new Setter(DataGridColumnHeader.ForegroundProperty, Brushes.White));
                    headerStyle.Setters.Add(new Setter(DataGridColumnHeader.FontWeightProperty, FontWeights.SemiBold));
                    headerStyle.Setters.Add(new Setter(DataGridColumnHeader.FontSizeProperty, dgOrders.FontSize));
                    headerStyle.Setters.Add(new Setter(DataGridColumnHeader.PaddingProperty, new Thickness(8, 4, 8, 4)));
                    headerStyle.Setters.Add(new Setter(DataGridColumnHeader.HeightProperty, headerHeight));
                    headerStyle.Setters.Add(new Setter(DataGridColumnHeader.HorizontalContentAlignmentProperty, HorizontalAlignment.Left));

                    dgOrders.ColumnHeaderStyle = headerStyle;
                }

                // === DOPASOWANIE dgDetails (górna część prawego panelu) ===
                int detailsRowCount = dgDetails.Items.Count;

                if (detailsRowCount > 0)
                {
                    // Dostępna wysokość dla dgDetails (45% prawego panelu)
                    double detailsAvailableHeight = (windowHeight - 250) * 0.40; // 40% górnej części
                    double detailsOptimalHeight = detailsAvailableHeight / (detailsRowCount + 1);

                    if (detailsRowCount > 8 || detailsOptimalHeight < 22)
                    {
                        dgDetails.FontSize = 9;
                        dgDetails.RowHeight = Math.Max(18, Math.Min(detailsOptimalHeight, 22));
                    }
                    else if (detailsRowCount > 5 || detailsOptimalHeight < 26)
                    {
                        dgDetails.FontSize = 10;
                        dgDetails.RowHeight = Math.Max(22, Math.Min(detailsOptimalHeight, 26));
                    }
                    else
                    {
                        // ✅ RESET DO DOMYŚLNYCH
                        dgDetails.FontSize = DEFAULT_DETAILS_FONT;
                        dgDetails.RowHeight = DEFAULT_DETAILS_ROW;
                    }

                    var detailsHeaderStyle = new Style(typeof(DataGridColumnHeader));
                    detailsHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.BackgroundProperty, new SolidColorBrush(Color.FromRgb(44, 62, 80))));
                    detailsHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.ForegroundProperty, Brushes.White));
                    detailsHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.FontWeightProperty, FontWeights.SemiBold));
                    detailsHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.FontSizeProperty, dgDetails.FontSize));
                    detailsHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.PaddingProperty, new Thickness(6, 3, 6, 3)));
                    detailsHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.HeightProperty, dgDetails.RowHeight + 6));

                    dgDetails.ColumnHeaderStyle = detailsHeaderStyle;
                }

                // === DOPASOWANIE txtNotes (większa czcionka) ===
                if (windowHeight < 700)
                {
                    txtNotes.FontSize = 11;
                }
                else if (windowHeight < 900)
                {
                    txtNotes.FontSize = 12;
                }
                else
                {
                    txtNotes.FontSize = 13; // Domyślna większa czcionka
                }

                // === DOPASOWANIE dgAggregation ===
                int aggRowCount = dgAggregation.Items.Count;

                if (aggRowCount > 0)
                {
                    double aggAvailableHeight = (windowHeight - 250) * 0.55; // 55% dolnej części
                    double aggOptimalHeight = aggAvailableHeight / (aggRowCount + 1);

                    if (aggRowCount > 15 || aggOptimalHeight < 28)
                    {
                        dgAggregation.FontSize = 10;
                        dgAggregation.RowHeight = Math.Max(26, Math.Min(aggOptimalHeight, 32));
                    }
                    else if (aggRowCount > 10 || aggOptimalHeight < 34)
                    {
                        dgAggregation.FontSize = 11;
                        dgAggregation.RowHeight = Math.Max(30, Math.Min(aggOptimalHeight, 36));
                    }
                    else
                    {
                        // ✅ RESET DO DOMYŚLNYCH - większe wiersze
                        dgAggregation.FontSize = DEFAULT_AGG_FONT;
                        dgAggregation.RowHeight = DEFAULT_AGG_ROW;
                    }

                    var aggHeaderStyle = new Style(typeof(DataGridColumnHeader));
                    aggHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.BackgroundProperty, new SolidColorBrush(Color.FromRgb(44, 62, 80))));
                    aggHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.ForegroundProperty, Brushes.White));
                    aggHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.FontWeightProperty, FontWeights.SemiBold));
                    aggHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.FontSizeProperty, dgAggregation.FontSize));
                    aggHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.PaddingProperty, new Thickness(6, 3, 6, 3)));
                    aggHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.HeightProperty, dgAggregation.RowHeight + 6));

                    dgAggregation.ColumnHeaderStyle = aggHeaderStyle;
                }
            }
            catch
            {
                // Ignoruj błędy podczas zmiany rozmiaru
            }
        }
        private void CbLayoutPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbLayoutPreset.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                var presetName = item.Tag.ToString();
                if (Enum.TryParse<LayoutPreset>(presetName, out var preset))
                {
                    ApplyPreset(preset);
                }
            }
        }
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isInitialized) return;
            _isInitialized = true;

            SetupDayButtons();
            btnDelete.Visibility = (UserID == "11111") ? Visibility.Visible : Visibility.Collapsed;
            chkShowReleasesWithoutOrders.IsChecked = _showReleasesWithoutOrders;
            chkShowAnulowane.IsChecked = false;

            await LoadInitialDataAsync();

            _selectedDate = ValidateSqlDate(DateTime.Today);
            UpdateDayButtonDates();
            await RefreshAllDataAsync(); // To utworzy DataGridy

            // ✅ WYWOŁAJ PO RefreshAllDataAsync, gdy wszystkie kontrolki są już utworzone
            ApplyResponsiveLayout();

            // Auto-odświeżanie co 1 minutę
            _autoRefreshTimer = new System.Windows.Threading.DispatcherTimer();
            _autoRefreshTimer.Interval = TimeSpan.FromMinutes(1);
            _autoRefreshTimer.Tick += AutoRefreshTimer_Tick;
            _autoRefreshTimer.Start();
        }

        private async void AutoRefreshTimer_Tick(object sender, EventArgs e)
        {
            await RefreshAllDataAsync();
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
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

            // Najpierw dodaj przycisk "Dziś" - mniejsze kafelki
            btnToday = new Button
            {
                Style = (Style)FindResource("DayButtonStyle"),
                Width = 60,
                Height = 38,
                Margin = new Thickness(0, 0, 3, 0)
            };

            var stackToday = new StackPanel();
            stackToday.Children.Add(new TextBlock
            {
                Text = "Dziś",
                FontSize = 8,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            });
            stackToday.Children.Add(new TextBlock
            {
                Text = DateTime.Today.ToString("dd.MM"),
                FontSize = 8,
                Foreground = Brushes.White
            });
            btnToday.Content = stackToday;
            btnToday.Background = new SolidColorBrush(Color.FromRgb(241, 196, 15));
            btnToday.Click += (s, e) =>
            {
                _selectedDate = DateTime.Today;
                UpdateDayButtonDates();
                _ = RefreshAllDataAsync();
            };

            panelDays.Children.Add(btnToday);

            // Teraz dodaj separator
            var separator = new System.Windows.Controls.Separator
            {
                Width = 2,
                Margin = new Thickness(5, 0, 5, 0),
                Background = new SolidColorBrush(Color.FromRgb(229, 231, 235))
            };
            panelDays.Children.Add(separator);

            // Teraz dodaj przyciski dni tygodnia - mniejsze kafelki
            for (int i = 0; i < 7; i++)
            {
                var btn = new Button
                {
                    Style = (Style)FindResource("DayButtonStyle")
                };

                var stack = new StackPanel();
                stack.Children.Add(new TextBlock { Text = dayNames[i], FontSize = 8, FontWeight = FontWeights.Bold });
                stack.Children.Add(new TextBlock { Text = DateTime.Today.AddDays(i).ToString("dd.MM"), FontSize = 8 });
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
            await CheckAndCreateTransportKursIDColumnAsync();
            await CheckAndCreateStatusColumnsAsync();

            _productCodeCache.Clear();
            _productCatalogCache.Clear();
            _productCatalogSwieze.Clear();
            _productCatalogMrozone.Clear();

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

                    _productCodeCache[idtw] = kod;

                    if (!(katObj is DBNull))
                    {
                        int katalog = 0;
                        if (katObj is int ki)
                            katalog = ki;
                        else
                            int.TryParse(Convert.ToString(katObj), out katalog);

                        if (katalog == 67095)
                        {
                            _productCatalogSwieze[idtw] = kod;
                            _productCatalogCache[idtw] = kod;
                        }
                        else if (katalog == 67153)
                        {
                            _productCatalogMrozone[idtw] = kod;
                            _productCatalogCache[idtw] = kod;
                        }
                    }
                }
            }

            GenerateProductButtons();

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
                }
                else
                {
                    _slaughterDateColumnExists = true;
                }
            }
            catch (Exception ex)
            {
                _slaughterDateColumnExists = false;
            }
        }

        private async Task CheckAndCreateTransportKursIDColumnAsync()
        {
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                const string checkSql = @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                                         WHERE TABLE_NAME = 'ZamowieniaMieso' AND COLUMN_NAME = 'TransportKursID'";

                await using var cmdCheck = new SqlCommand(checkSql, cn);
                int count = Convert.ToInt32(await cmdCheck.ExecuteScalarAsync());

                if (count == 0)
                {
                    const string alterSql = @"ALTER TABLE [dbo].[ZamowieniaMieso] ADD TransportKursID INT NULL";
                    await using var cmdAlter = new SqlCommand(alterSql, cn);
                    await cmdAlter.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                // Kolumna może już istnieć
            }
        }

        private async Task CheckAndCreateStatusColumnsAsync()
        {
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                // Lista kolumn do utworzenia
                var columns = new[]
                {
                    ("CzyZrealizowane", "BIT DEFAULT 0 NOT NULL"),
                    ("DataRealizacji", "DATETIME NULL"),
                    ("KtoZrealizowal", "INT NULL"),
                    ("CzyWydane", "BIT DEFAULT 0 NOT NULL")
                };

                foreach (var (columnName, columnDef) in columns)
                {
                    var checkSql = $@"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                                     WHERE TABLE_NAME = 'ZamowieniaMieso' AND COLUMN_NAME = '{columnName}'";

                    await using var cmdCheck = new SqlCommand(checkSql, cn);
                    int count = Convert.ToInt32(await cmdCheck.ExecuteScalarAsync());

                    if (count == 0)
                    {
                        var alterSql = $@"ALTER TABLE [dbo].[ZamowieniaMieso] ADD {columnName} {columnDef}";
                        await using var cmdAlter = new SqlCommand(alterSql, cn);
                        await cmdAlter.ExecuteNonQueryAsync();
                    }
                }

                // Migracja istniejących danych - tylko raz przy pierwszym uruchomieniu
                var migrationSql = @"
                    -- Ustaw CzyZrealizowane na podstawie Status
                    UPDATE dbo.ZamowieniaMieso
                    SET CzyZrealizowane = 1
                    WHERE Status IN ('Zrealizowane', 'Wydany') AND CzyZrealizowane = 0;

                    -- Ustaw CzyWydane na podstawie Status lub DataWydania
                    UPDATE dbo.ZamowieniaMieso
                    SET CzyWydane = 1
                    WHERE (Status = 'Wydany' OR DataWydania IS NOT NULL) AND CzyWydane = 0;";

                await using var cmdMigrate = new SqlCommand(migrationSql, cn);
                await cmdMigrate.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd tworzenia kolumn statusów: {ex.Message}");
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

        private async void BtnNew_Click(object sender, RoutedEventArgs e)
        {
            var widokZamowienia = new Kalendarz1.WidokZamowienia(UserID, null);
            if (widokZamowienia.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                await RefreshAllDataAsync();
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
                    // Logowanie historii zmian PRZED usunięciem
                    await HistoriaZmianService.LogujUsuniecie(_currentOrderId.Value, UserID, App.UserFullName);

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

        private async void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var printWindow = new PrintOptionsWindow();
                if (printWindow.ShowDialog() == true)
                {
                    if (printWindow.GroupByProduct)
                    {
                        var printPreview = new PrintPreviewByProductWindow(_dtOrders, _selectedDate, _productCatalogCache);
                        printPreview.Owner = this;
                        printPreview.ShowDialog();
                    }
                    else
                    {
                        var printPreview = new PrintPreviewByClientWindow(_dtOrders, _selectedDate, _productCatalogCache);
                        printPreview.Owner = this;
                        printPreview.ShowDialog();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas przygotowywania wydruku:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Filter Events

        private void ChkShowAnulowane_Changed(object sender, RoutedEventArgs e)
        {
            _showAnulowane = chkShowAnulowane.IsChecked == true;
            ApplyFilters();
        }

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

        private async void RbProductCatalog_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;
            GenerateProductButtons();
            _selectedProductId = null;
            await RefreshAllDataAsync();
        }

        private void GenerateProductButtons()
        {
            pnlProductButtons.Children.Clear();

            var catalog = rbSwieze.IsChecked == true ? _productCatalogSwieze : _productCatalogMrozone;

            // Przycisk "Wszystkie"
            var btnAll = new Button
            {
                Content = "Wszystkie",
                Margin = new Thickness(2),
                Padding = new Thickness(8, 4, 8, 4),
                FontSize = 11,
                Background = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = (int?)null
            };
            btnAll.Click += ProductButton_Click;
            pnlProductButtons.Children.Add(btnAll);

            foreach (var product in catalog.OrderBy(x => x.Value))
            {
                var btn = new Button
                {
                    Content = product.Value,
                    Margin = new Thickness(2),
                    Padding = new Thickness(8, 4, 8, 4),
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
                // Reset all buttons
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

                // Highlight selected
                btn.Background = new SolidColorBrush(Color.FromRgb(46, 204, 113));
                btn.Foreground = Brushes.White;

                _selectedProductId = btn.Tag as int?;
                await RefreshAllDataAsync();
            }
        }

        private void MenuShowAnulowane_Click(object sender, RoutedEventArgs e)
        {
            _showAnulowane = menuShowAnulowane.IsChecked;
            chkShowAnulowane.IsChecked = _showAnulowane;
            ApplyFilters();
        }

        private void MenuShowWydaniaBezZam_Click(object sender, RoutedEventArgs e)
        {
            _showReleasesWithoutOrders = menuShowWydaniaBezZam.IsChecked;
            chkShowReleasesWithoutOrders.IsChecked = _showReleasesWithoutOrders;
            ApplyFilters();
        }

        #endregion
        #region Context Menu Events

        private void DgOrders_MouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var dep = (DependencyObject)e.OriginalSource;
            while (dep != null && !(dep is DataGridRow))
            {
                dep = VisualTreeHelper.GetParent(dep);
            }

            if (dep is DataGridRow row && row.Item is DataRowView rowView)
            {
                _contextMenuSelectedRow = rowView;
                var status = rowView.Row.Field<string>("Status") ?? "";
                var id = rowView.Row.Field<int>("Id");

                bool isSpecialRow = (id == -1 || id == 0 ||
                                    status == "SUMA" ||
                                    status == "Wydanie bez zamówienia");

                bool isWydanieBezZamowienia = status == "Wydanie bez zamówienia";
                bool isAnulowane = status == "Anulowane";

                // Konfiguruj widoczność i dostępność elementów menu
                menuAnuluj.Visibility = isAnulowane ? Visibility.Collapsed : Visibility.Visible;
                menuPrzywroc.Visibility = isAnulowane ? Visibility.Visible : Visibility.Collapsed;

                foreach (var item in contextMenuOrders.Items)
                {
                    if (item is MenuItem menuItem)
                    {
                        string header = menuItem.Header?.ToString() ?? "";

                        if (isWydanieBezZamowienia)
                        {
                            menuItem.IsEnabled = header.Contains("Płatności") ||
                                                header.Contains("Historia") ||
                                                header.Contains("Odśwież");
                        }
                        else if (isAnulowane)
                        {
                            menuItem.IsEnabled = header.Contains("Szczegóły zamówienia") ||
                                                header.Contains("Płatności") ||
                                                header.Contains("Historia") ||
                                                header.Contains("Odśwież") ||
                                                header.Contains("Przywróć") ||
                                                header.Contains("USUŃ") ||
                                                header.Contains("transport");
                        }
                        else
                        {
                            menuItem.IsEnabled = !isSpecialRow;
                        }
                    }
                }

                menuUsun.Visibility = (UserID == "11111") ? Visibility.Visible : Visibility.Collapsed;

                dgOrders.SelectedItem = rowView;
            }
            else
            {
                _contextMenuSelectedRow = null;
                e.Handled = true;
            }
        }

        private async void MenuDuplikuj_Click(object sender, RoutedEventArgs e)
        {
            if (_contextMenuSelectedRow == null) return;

            var id = _contextMenuSelectedRow.Row.Field<int>("Id");
            if (id <= 0)
            {
                MessageBox.Show("Nie można zduplikować tego elementu.", "Informacja",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _currentOrderId = id;
            await DisplayOrderDetailsAsync(id);

            var dlg = new MultipleDatePickerWindow("Wybierz dni dla duplikatu zamówienia");
            dlg.DatesSelected += async (s, ev) =>
            {
                if (dlg.SelectedDates.Any())
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
            };
            dlg.Show();
        }

        private async void MenuCykliczne_Click(object sender, RoutedEventArgs e)
        {
            if (_contextMenuSelectedRow == null) return;

            var id = _contextMenuSelectedRow.Row.Field<int>("Id");
            if (id <= 0)
            {
                MessageBox.Show("Nie można utworzyć zamówień cyklicznych dla tego elementu.", "Informacja",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _currentOrderId = id;
            await DisplayOrderDetailsAsync(id);

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

        private async void MenuModyfikuj_Click(object sender, RoutedEventArgs e)
        {
            if (_contextMenuSelectedRow == null) return;

            var id = _contextMenuSelectedRow.Row.Field<int>("Id");
            if (id <= 0)
            {
                MessageBox.Show("Nie można modyfikować tego elementu.", "Informacja",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _currentOrderId = id;
            await DisplayOrderDetailsAsync(id);

            var widokZamowienia = new Kalendarz1.WidokZamowienia(UserID, _currentOrderId.Value);
            if (widokZamowienia.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                await RefreshAllDataAsync();
            }
        }

        private async void MenuNotatka_Click(object sender, RoutedEventArgs e)
        {
            if (_contextMenuSelectedRow == null) return;

            var id = _contextMenuSelectedRow.Row.Field<int>("Id");
            if (id <= 0)
            {
                MessageBox.Show("Nie można dodać notatki do tego elementu.", "Informacja",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _currentOrderId = id;

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
            noteWindow.NoteSaved += async (s, ev) =>
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
            };
            noteWindow.Show();
        }

        private async void MenuAnuluj_Click(object sender, RoutedEventArgs e)
        {
            if (_contextMenuSelectedRow == null) return;

            var id = _contextMenuSelectedRow.Row.Field<int>("Id");
            if (id <= 0)
            {
                MessageBox.Show("Nie można anulować tego elementu.", "Informacja",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var odbiorca = _contextMenuSelectedRow.Row.Field<string>("Odbiorca") ?? "Nieznany";
            var ilosc = _contextMenuSelectedRow.Row.Field<decimal>("IloscZamowiona");

            var result = MessageBox.Show(
                $"Czy na pewno chcesz anulować to zamówienie?\n\n" +
                $"📦 Odbiorca: {odbiorca}\n" +
                $"⚖️ Ilość: {ilosc:N0} kg\n\n" +
                $"⚠️ Zamówienie można później przywrócić.",
                "Potwierdź anulowanie",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();
                    await using var cmd = new SqlCommand(
                        "UPDATE dbo.ZamowieniaMieso SET Status = 'Anulowane' WHERE Id = @Id", cn);
                    cmd.Parameters.AddWithValue("@Id", id);
                    await cmd.ExecuteNonQueryAsync();

                    // Logowanie historii zmian
                    await HistoriaZmianService.LogujAnulowanie(id, UserID, App.UserFullName,
                        $"Anulowano zamówienie dla odbiorcy: {odbiorca}, ilość: {ilosc:N0} kg");

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

        private async void MenuPrzywroc_Click(object sender, RoutedEventArgs e)
        {
            if (_contextMenuSelectedRow == null) return;

            var id = _contextMenuSelectedRow.Row.Field<int>("Id");
            if (id <= 0)
            {
                MessageBox.Show("Nie można przywrócić tego elementu.", "Informacja",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var odbiorca = _contextMenuSelectedRow.Row.Field<string>("Odbiorca") ?? "Nieznany";
            var ilosc = _contextMenuSelectedRow.Row.Field<decimal>("IloscZamowiona");

            var result = MessageBox.Show(
                $"Czy na pewno chcesz przywrócić to zamówienie?\n\n" +
                $"📦 Odbiorca: {odbiorca}\n" +
                $"⚖️ Ilość: {ilosc:N0} kg\n\n" +
                $"✅ Zamówienie zostanie ponownie aktywowane.",
                "Potwierdź przywrócenie",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();
                    await using var cmd = new SqlCommand(
                        "UPDATE dbo.ZamowieniaMieso SET Status = 'Nowe' WHERE Id = @Id", cn);
                    cmd.Parameters.AddWithValue("@Id", id);
                    await cmd.ExecuteNonQueryAsync();

                    // Logowanie historii zmian
                    await HistoriaZmianService.LogujPrzywrocenie(id, UserID, App.UserFullName);

                    MessageBox.Show("Zamówienie zostało przywrócone.", "Sukces",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    await RefreshAllDataAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Wystąpił błąd podczas przywracania zamówienia: {ex.Message}",
                        "Błąd krytyczny", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void MenuUsun_Click(object sender, RoutedEventArgs e)
        {
            if (_contextMenuSelectedRow == null) return;

            var id = _contextMenuSelectedRow.Row.Field<int>("Id");
            if (id <= 0)
            {
                MessageBox.Show("Nie można usunąć tego elementu.", "Informacja",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _currentOrderId = id;
            await DisplayOrderDetailsAsync(id);
            BtnDelete_Click(sender, e);
        }

        private async void MenuTransportInfo_Click(object sender, RoutedEventArgs e)
        {
            if (_contextMenuSelectedRow == null) return;

            var id = _contextMenuSelectedRow.Row.Field<int>("Id");
            if (id <= 0)
            {
                MessageBox.Show("Nie można wyświetlić informacji o transporcie dla tego elementu.",
                    "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // Pobierz TransportKursID z zamówienia
                long? transportKursId = null;
                await using (var cn = new SqlConnection(_connLibra))
                {
                    await cn.OpenAsync();
                    var sql = "SELECT TransportKursID FROM dbo.ZamowieniaMieso WHERE Id = @Id";
                    using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Id", id);
                    var result = await cmd.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                    {
                        transportKursId = Convert.ToInt64(result);
                    }
                }

                if (!transportKursId.HasValue)
                {
                    MessageBox.Show("To zamówienie nie jest przypisane do żadnego transportu.",
                        "Brak transportu", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Wybór opcji wyświetlania
                var options = MessageBox.Show(
                    "Jak chcesz wyświetlić informacje o transporcie?\n\n" +
                    "TAK - Otwórz edytor transportu\n" +
                    "NIE - Pokaż okno z informacjami",
                    "Wybierz opcję",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (options == MessageBoxResult.Yes)
                {
                    // Otwórz edytor transportu
                    try
                    {
                        // ================== POPRAWKA BŁĘDU CS7036 ==================
                        // Dodano brakujące argumenty do konstruktora
                        var repozytorium = new Kalendarz1.Transport.Repozytorium.TransportRepozytorium(_connTransport, _connLibra);
                        // ==========================================================

                        var kurs = await repozytorium.PobierzKursAsync(transportKursId.Value);

                        if (kurs != null)
                        {
                            var edytor = new Kalendarz1.Transport.Formularze.EdytorKursuWithPalety(
                                repozytorium, kurs, UserID);
                            edytor.ShowDialog();
                            await RefreshAllDataAsync();
                        }
                        else
                        {
                            MessageBox.Show("Nie znaleziono kursu transportowego.",
                                "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Błąd podczas otwierania edytora transportu:\n{ex.Message}",
                            "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else if (options == MessageBoxResult.No)
                {
                    // ================== POPRAWKA BŁĘDU CS0246 ==================
                    // Zastąpiono brakujące okno TransportInfoWindow standardowym MessageBoxem
                    var transportInfo = await GetTransportInfoAsync(transportKursId.Value);

                    var sb = new StringBuilder();
                    sb.AppendLine($"🚚 KURS #{transportInfo.KursId} - {transportInfo.DataKursu:dd.MM.yyyy}");
                    sb.AppendLine($"Status: {transportInfo.Status}");
                    sb.AppendLine($"Trasa: {transportInfo.Trasa}");
                    sb.AppendLine($"Wyjazd: {transportInfo.GodzWyjazdu:hh\\:mm} | Powrót: {transportInfo.GodzPowrotu:hh\\:mm}");
                    sb.AppendLine();
                    sb.AppendLine($"👤 Kierowca: {transportInfo.Kierowca} ({transportInfo.TelefonKierowcy})");
                    sb.AppendLine($"🚗 Pojazd: {transportInfo.MarkaPojazdu} {transportInfo.ModelPojazdu} ({transportInfo.Rejestracja})");
                    sb.AppendLine($"📦 Max palet: {transportInfo.MaxPalety}");
                    sb.AppendLine();
                    sb.AppendLine("--- ŁADUNKI ---");

                    foreach (var ladunek in transportInfo.Ladunki.OrderBy(l => l.Kolejnosc))
                    {
                        sb.AppendLine($"{ladunek.Kolejnosc}. {ladunek.NazwaKlienta} ({ladunek.KodKlienta}) - {ladunek.Palety} palet, {ladunek.Pojemniki} pojemników");
                    }

                    MessageBox.Show(sb.ToString(), "Informacje o transporcie", MessageBoxButton.OK, MessageBoxImage.Information);
                    // ==========================================================
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas pobierania informacji o transporcie:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async Task<TransportInfo> GetTransportInfoAsync(long kursId)
        {
            var info = new TransportInfo { KursId = kursId };

            try
            {
                await using var cn = new SqlConnection(_connTransport);
                await cn.OpenAsync();

                // Pobierz dane kursu
                var sqlKurs = @"
                    SELECT k.DataKursu, k.Trasa, k.GodzWyjazdu, k.GodzPowrotu, k.Status,
                           ki.Imie + ' ' + ki.Nazwisko as Kierowca, ki.Telefon,
                           p.Rejestracja, p.Marka, p.Model, p.PaletyH1
                    FROM dbo.Kurs k
                    LEFT JOIN dbo.Kierowca ki ON k.KierowcaID = ki.KierowcaID
                    LEFT JOIN dbo.Pojazd p ON k.PojazdID = p.PojazdID
                    WHERE k.KursID = @KursId";

                using (var cmd = new SqlCommand(sqlKurs, cn))
                {
                    cmd.Parameters.AddWithValue("@KursId", kursId);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        info.DataKursu = reader.GetDateTime(0);
                        info.Trasa = reader.IsDBNull(1) ? "" : reader.GetString(1);
                        info.GodzWyjazdu = reader.IsDBNull(2) ? null : reader.GetTimeSpan(2);
                        info.GodzPowrotu = reader.IsDBNull(3) ? null : reader.GetTimeSpan(3);
                        info.Status = reader.IsDBNull(4) ? "" : reader.GetString(4);
                        info.Kierowca = reader.IsDBNull(5) ? "" : reader.GetString(5);
                        info.TelefonKierowcy = reader.IsDBNull(6) ? "" : reader.GetString(6);
                        info.Rejestracja = reader.IsDBNull(7) ? "" : reader.GetString(7);
                        info.MarkaPojazdu = reader.IsDBNull(8) ? "" : reader.GetString(8);
                        info.ModelPojazdu = reader.IsDBNull(9) ? "" : reader.GetString(9);
                        info.MaxPalety = reader.IsDBNull(10) ? 0 : reader.GetInt32(10);
                    }
                }

                // Pobierz ładunki
                var sqlLadunki = @"
                    SELECT l.Kolejnosc, l.KodKlienta, l.PaletyH1, l.PojemnikiE2, l.Uwagi
                    FROM dbo.Ladunek l
                    WHERE l.KursID = @KursId
                    ORDER BY l.Kolejnosc";

                using (var cmd = new SqlCommand(sqlLadunki, cn))
                {
                    cmd.Parameters.AddWithValue("@KursId", kursId);
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var ladunek = new LadunekInfo
                        {
                            Kolejnosc = reader.GetInt32(0),
                            KodKlienta = reader.IsDBNull(1) ? "" : reader.GetString(1),
                            Palety = reader.GetInt32(2),
                            Pojemniki = reader.GetInt32(3),
                            Uwagi = reader.IsDBNull(4) ? "" : reader.GetString(4)
                        };

                        // Pobierz nazwę klienta
                        if (ladunek.KodKlienta.StartsWith("ZAM_"))
                        {
                            ladunek.NazwaKlienta = await GetClientNameFromOrder(ladunek.KodKlienta.Substring(4));
                        }
                        else
                        {
                            ladunek.NazwaKlienta = ladunek.Uwagi;
                        }

                        info.Ladunki.Add(ladunek);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas pobierania danych transportu:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return info;
        }

        private async Task<string> GetClientNameFromOrder(string orderId)
        {
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                var sql = "SELECT KlientId FROM dbo.ZamowieniaMieso WHERE Id = @Id";
                using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Id", orderId);
                var clientId = await cmd.ExecuteScalarAsync();

                if (clientId != null)
                {
                    await using var cnHandel = new SqlConnection(_connHandel);
                    await cnHandel.OpenAsync();

                    var sqlName = "SELECT Shortcut FROM [HANDEL].[SSCommon].[STContractors] WHERE Id = @Id";
                    using var cmdName = new SqlCommand(sqlName, cnHandel);
                    cmdName.Parameters.AddWithValue("@Id", clientId);
                    var name = await cmdName.ExecuteScalarAsync();
                    return name?.ToString() ?? $"Klient {clientId}";
                }
            }
            catch
            {
                // Ignoruj błędy
            }

            return "Nieznany";
        }

        private void MenuSzczegolyPlatnosci_Click(object sender, RoutedEventArgs e)
        {
            if (_contextMenuSelectedRow == null) return;

            try
            {
                var status = _contextMenuSelectedRow.Row.Field<string>("Status") ?? "";
                int clientId;
                string nazwaOdbiorcy;

                if (status == "Wydanie bez zamówienia")
                {
                    clientId = _contextMenuSelectedRow.Row.Field<int>("KlientId");
                    nazwaOdbiorcy = _contextMenuSelectedRow.Row.Field<string>("Odbiorca") ?? "Nieznany";
                }
                else
                {
                    var id = _contextMenuSelectedRow.Row.Field<int>("Id");
                    if (id <= 0)
                    {
                        MessageBox.Show("Nie można wyświetlić płatności dla tego elementu.", "Informacja",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    clientId = _contextMenuSelectedRow.Row.Field<int>("KlientId");
                    nazwaOdbiorcy = _contextMenuSelectedRow.Row.Field<string>("Odbiorca") ?? "Nieznany";
                }

                nazwaOdbiorcy = CzyscNazweZEmoji(nazwaOdbiorcy);

                if (clientId <= 0)
                {
                    MessageBox.Show("Brak informacji o kliencie dla tego zamówienia.", "Błąd",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var szczegolyWindow = new SzczegolyPlatnosciWindow(_connHandel, nazwaOdbiorcy);
                szczegolyWindow.Owner = this;
                szczegolyWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"❌ Błąd podczas otwierania szczegółów płatności:\n\n{ex.Message}\n\n" +
                    $"Stos wywołań:\n{ex.StackTrace}",
                    "Błąd krytyczny",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void MenuSzczegolyZamowienia_Click(object sender, RoutedEventArgs e)
        {
            if (_contextMenuSelectedRow == null) return;

            var id = _contextMenuSelectedRow.Row.Field<int>("Id");
            var status = _contextMenuSelectedRow.Row.Field<string>("Status") ?? "";

            if (status == "Wydanie bez zamówienia")
            {
                MessageBox.Show("To jest wydanie bez zamówienia. Szczegóły są widoczne w panelu po prawej stronie.",
                    "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (id <= 0)
            {
                MessageBox.Show("Nie można wyświetlić szczegółów dla tego elementu.", "Informacja",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _currentOrderId = id;
            await DisplayOrderDetailsAsync(id);

            var odbiorca = _contextMenuSelectedRow.Row.Field<string>("Odbiorca") ?? "Nieznany";
            var dataOdbioru = _contextMenuSelectedRow.Row.Field<DateTime>("DataPrzyjecia");
            var godzina = _contextMenuSelectedRow.Row.Field<string>("GodzinaPrzyjecia") ?? "";
            var ilosc = _contextMenuSelectedRow.Row.Field<decimal>("IloscZamowiona");
            var wydano = _contextMenuSelectedRow.Row.Field<decimal>("IloscFaktyczna");
            var palety = _contextMenuSelectedRow.Row.Field<decimal>("Palety");
            var pojemniki = _contextMenuSelectedRow.Row.Field<int>("Pojemniki");
            var trybE2 = _contextMenuSelectedRow.Row.Field<string>("TrybE2") ?? "";
            var utworzone = _contextMenuSelectedRow.Row.Field<string>("UtworzonePrzez") ?? "";

            string info = $"📦 SZCZEGÓŁY ZAMÓWIENIA #{id}\n" +
                          $"{'━',50}\n\n" +
                          $"👤 Odbiorca: {CzyscNazweZEmoji(odbiorca)}\n" +
                          $"📅 Data odbioru: {dataOdbioru:dd.MM.yyyy} {godzina}\n" +
                          $"📊 Status: {status}\n\n" +
                          $"⚖️ Zamówiono: {ilosc:N0} kg\n" +
                          $"✅ Wydano: {wydano:N0} kg\n" +
                          $"📈 Realizacja: {(ilosc > 0 ? (wydano / ilosc * 100).ToString("N1") : "0")}%\n\n" +
                          $"📦 Pojemniki: {pojemniki} szt.\n" +
                          $"🚚 Palety: {palety:N1}\n" +
                          $"⚙️ Tryb: {trybE2}\n\n" +
                          $"👨‍💼 Utworzone przez: {utworzone}";

            MessageBox.Show(info, "Szczegóły zamówienia", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        private async void MenuHistoriaZamowien_Click(object sender, RoutedEventArgs e)
        {
            if (_contextMenuSelectedRow == null) return;

            try
            {
                var clientId = _contextMenuSelectedRow.Row.Field<int>("KlientId");
                var nazwaOdbiorcy = CzyscNazweZEmoji(_contextMenuSelectedRow.Row.Field<string>("Odbiorca") ?? "Nieznany");

                if (clientId <= 0)
                {
                    MessageBox.Show("Brak informacji o kliencie.", "Błąd",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var historia = new System.Text.StringBuilder();
                historia.AppendLine($"📋 HISTORIA ZAMÓWIEŃ - {nazwaOdbiorcy}");
                historia.AppendLine($"{'━',60}");
                historia.AppendLine();

                await using (var cn = new SqlConnection(_connLibra))
                {
                    await cn.OpenAsync();

                    string sql = @"
                        SELECT TOP 20
                            zm.Id,
                            zm.DataZamowienia,
                            zm.Status,
                            SUM(ISNULL(zmt.Ilosc, 0)) as IloscCalkowita,
                            zm.LiczbaPalet
                        FROM ZamowieniaMieso zm
                        LEFT JOIN ZamowieniaMiesoTowar zmt ON zm.Id = zmt.ZamowienieId
                        WHERE zm.KlientId = @ClientId
                            AND zm.DataZamowienia >= DATEADD(MONTH, -6, GETDATE())
                        GROUP BY zm.Id, zm.DataZamowienia, zm.Status, zm.LiczbaPalet
                        ORDER BY zm.DataZamowienia DESC";

                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@ClientId", clientId);

                    await using var reader = await cmd.ExecuteReaderAsync();

                    decimal sumaKg = 0;
                    int liczbaZamowien = 0;

                    while (await reader.ReadAsync())
                    {
                        int id = reader.GetInt32(0);
                        DateTime data = reader.GetDateTime(1);
                        string statusZam = reader.IsDBNull(2) ? "Brak" : reader.GetString(2);
                        decimal ilosc = reader.IsDBNull(3) ? 0m : reader.GetDecimal(3);
                        decimal palety = reader.IsDBNull(4) ? 0m : reader.GetDecimal(4);

                        historia.AppendLine($"#{id} | {data:dd.MM.yyyy} | {statusZam,-12} | {ilosc,7:N0} kg | {palety,4:N1} pal.");

                        if (statusZam != "Anulowane")
                        {
                            sumaKg += ilosc;
                            liczbaZamowien++;
                        }
                    }

                    historia.AppendLine();
                    historia.AppendLine($"{'━',60}");
                    historia.AppendLine($"Razem (ostatnie 6 m-cy): {liczbaZamowien} zamówień | {sumaKg:N0} kg");

                    if (liczbaZamowien > 0)
                    {
                        decimal srednia = sumaKg / liczbaZamowien;
                        historia.AppendLine($"Średnia na zamówienie: {srednia:N0} kg");
                    }
                }

                MessageBox.Show(historia.ToString(), "Historia zamówień",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas pobierania historii:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void MenuHistoriaZmian_Click(object sender, RoutedEventArgs e)
        {
            if (_contextMenuSelectedRow == null || !_currentOrderId.HasValue) return;

            var orderId = _currentOrderId.Value;
            var odbiorca = CzyscNazweZEmoji(_contextMenuSelectedRow.Row.Field<string>("Odbiorca") ?? "Nieznany");

            try
            {
                var historia = new System.Text.StringBuilder();
                historia.AppendLine($"📜 HISTORIA ZMIAN ZAMÓWIENIA #{orderId}");
                historia.AppendLine($"Odbiorca: {odbiorca}");
                historia.AppendLine(new string('━', 60));
                historia.AppendLine();

                await using (var cn = new SqlConnection(_connLibra))
                {
                    await cn.OpenAsync();

                    // Sprawdź czy tabela istnieje
                    var checkSql = @"SELECT COUNT(*) FROM sys.objects
                        WHERE object_id = OBJECT_ID(N'[dbo].[HistoriaZmianZamowien]') AND type in (N'U')";
                    using var checkCmd = new SqlCommand(checkSql, cn);
                    var tableExists = (int)await checkCmd.ExecuteScalarAsync() > 0;

                    if (!tableExists)
                    {
                        MessageBox.Show("Brak zapisanej historii zmian dla tego zamówienia.\n\n" +
                            "Historia zmian będzie dostępna po wprowadzeniu pierwszych zmian.",
                            "Historia zmian", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    var sql = @"
                        SELECT
                            TypZmiany,
                            PoleZmienione,
                            WartoscPoprzednia,
                            WartoscNowa,
                            ISNULL(UzytkownikNazwa, Uzytkownik) as Uzytkownik,
                            DataZmiany,
                            OpisZmiany
                        FROM HistoriaZmianZamowien
                        WHERE ZamowienieId = @ZamowienieId
                        ORDER BY DataZmiany DESC";

                    using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@ZamowienieId", orderId);

                    using var reader = await cmd.ExecuteReaderAsync();
                    int licznik = 0;

                    while (await reader.ReadAsync())
                    {
                        licznik++;
                        string typZmiany = reader.IsDBNull(0) ? "" : reader.GetString(0);
                        string poleZmienione = reader.IsDBNull(1) ? null : reader.GetString(1);
                        string wartoscPoprzednia = reader.IsDBNull(2) ? null : reader.GetString(2);
                        string wartoscNowa = reader.IsDBNull(3) ? null : reader.GetString(3);
                        string uzytkownik = reader.IsDBNull(4) ? "Nieznany" : reader.GetString(4);
                        DateTime dataZmiany = reader.GetDateTime(5);
                        string opisZmiany = reader.IsDBNull(6) ? null : reader.GetString(6);

                        string ikona = typZmiany switch
                        {
                            "UTWORZENIE" => "➕",
                            "EDYCJA" => "✏️",
                            "ANULOWANIE" => "❌",
                            "PRZYWROCENIE" => "✅",
                            "USUNIECIE" => "🗑️",
                            _ => "📝"
                        };

                        historia.AppendLine($"{ikona} {dataZmiany:yyyy-MM-dd HH:mm} | {uzytkownik}");

                        if (!string.IsNullOrEmpty(opisZmiany))
                        {
                            historia.AppendLine($"   {opisZmiany}");
                        }
                        else if (!string.IsNullOrEmpty(poleZmienione))
                        {
                            historia.AppendLine($"   {poleZmienione}: '{wartoscPoprzednia ?? "(puste)"}' → '{wartoscNowa ?? "(puste)"}'");
                        }
                        else
                        {
                            historia.AppendLine($"   {typZmiany}");
                        }
                        historia.AppendLine();
                    }

                    if (licznik == 0)
                    {
                        historia.AppendLine("Brak zapisanych zmian dla tego zamówienia.");
                    }
                    else
                    {
                        historia.AppendLine(new string('━', 60));
                        historia.AppendLine($"Łącznie: {licznik} zmian");
                    }
                }

                MessageBox.Show(historia.ToString(), $"Historia zmian - Zamówienie #{orderId}",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas pobierania historii zmian:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void MenuOdswiez_Click(object sender, RoutedEventArgs e)
        {
            await RefreshAllDataAsync();
            MessageBox.Show("✓ Dane zostały odświeżone!", "Sukces",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private string CzyscNazweZEmoji(string nazwa)
        {
            if (string.IsNullOrEmpty(nazwa))
                return nazwa;

            nazwa = nazwa.Replace("📝", "")
                         .Replace("📦", "")
                         .Replace("🍗", "")
                         .Replace("🍖", "")
                         .Replace("🥩", "")
                         .Replace("🐔", "")
                         .Replace("└", "")
                         .Trim();

            while (nazwa.Contains("  "))
            {
                nazwa = nazwa.Replace("  ", " ");
            }

            return nazwa.Trim();
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
            // Zapobiegaj równoległym wywołaniom
            if (_isRefreshing) return;
            _isRefreshing = true;

            try
            {
                await LoadOrdersForDayAsync(_selectedDate);
                await LoadTransportForDayAsync(_selectedDate);
                await DisplayProductAggregationAsync(_selectedDate);

                // Załaduj historię zmian dla całego tygodnia
                int delta = ((int)_selectedDate.DayOfWeek + 6) % 7;
                DateTime startOfWeek = _selectedDate.AddDays(-delta);
                DateTime endOfWeek = startOfWeek.AddDays(6);
                await LoadHistoriaZmianAsync(startOfWeek, endOfWeek);

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
            finally
            {
                _isRefreshing = false;
            }
        }

        private async Task LoadOrdersForDayAsync(DateTime day)
        {
            day = ValidateSqlDate(day);

            // Zawsze czyść dane przed ładowaniem
            _dtOrders.Rows.Clear();

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
                _dtOrders.Columns.Add("MaFolie", typeof(bool));
                _dtOrders.Columns.Add("MaHallal", typeof(bool));
                _dtOrders.Columns.Add("CzyMaCeny", typeof(bool));
                _dtOrders.Columns.Add("CenaInfo", typeof(string));
                _dtOrders.Columns.Add("TerminInfo", typeof(string));
                _dtOrders.Columns.Add("TransportInfo", typeof(string));
                _dtOrders.Columns.Add("Wyprodukowano", typeof(string));
                _dtOrders.Columns.Add("WydanoInfo", typeof(string));
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

            int? selectedProductId = _selectedProductId;

            var temp = new DataTable();
            var clientsWithOrders = new HashSet<int>();
            _processedOrderIds.Clear();

            await using (var cnLibra = new SqlConnection(_connLibra))
            {
                await cnLibra.OpenAsync();
                string dateColumn = (_showBySlaughterDate && _slaughterDateColumnExists) ? "DataUboju" : "DataZamowienia";

                string sqlClients = $@"SELECT DISTINCT KlientId FROM [dbo].[ZamowieniaMieso] 
                              WHERE {dateColumn} = @Day AND KlientId IS NOT NULL";
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
       zm.LiczbaPojemnikow, zm.LiczbaPalet, zm.TrybE2, zm.Uwagi, zm.TransportKursID,
       ISNULL(zm.CzyZrealizowane, 0) AS CzyZrealizowane,
       ISNULL(zm.CzyWydane, 0) AS CzyWydane,
       zm.DataWydania,
       CAST(CASE WHEN EXISTS(SELECT 1 FROM [dbo].[ZamowieniaMiesoTowar] WHERE ZamowienieId = zm.Id AND Folia = 1)
            THEN 1 ELSE 0 END AS BIT) AS MaFolie,
       CAST(CASE WHEN EXISTS(SELECT 1 FROM [dbo].[ZamowieniaMiesoTowar] WHERE ZamowienieId = zm.Id AND Hallal = 1)
            THEN 1 ELSE 0 END AS BIT) AS MaHallal,
       CAST(CASE WHEN NOT EXISTS(SELECT 1 FROM [dbo].[ZamowieniaMiesoTowar] WHERE ZamowienieId = zm.Id AND (Cena IS NULL OR Cena = '' OR Cena = '0' OR TRY_CAST(Cena AS DECIMAL(18,2)) = 0))
            AND EXISTS(SELECT 1 FROM [dbo].[ZamowieniaMiesoTowar] WHERE ZamowienieId = zm.Id)
            THEN 1 ELSE 0 END AS BIT) AS CzyMaCeny{slaughterDateSelect}
FROM [dbo].[ZamowieniaMieso] zm
LEFT JOIN [dbo].[ZamowieniaMiesoTowar] zmt ON zm.Id = zmt.ZamowienieId
WHERE {dateColumn} = @Day " +
                                (selectedProductId.HasValue ? "AND (zmt.KodTowaru = @ProductId OR zmt.KodTowaru IS NULL) " : "") +
                                $@"GROUP BY zm.Id, zm.KlientId, zm.DataPrzyjazdu, zm.DataUtworzenia, zm.IdUser, zm.Status,
     zm.LiczbaPojemnikow, zm.LiczbaPalet, zm.TrybE2, zm.Uwagi, zm.TransportKursID,
     zm.CzyZrealizowane, zm.CzyWydane, zm.DataWydania{slaughterDateGroupBy}
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
            var transportInfo = await GetTransportInfoAsync(day);
            var cultureInfo = new CultureInfo("pl-PL");

            decimal totalOrdered = 0m;
            decimal totalReleased = 0m;
            decimal totalPallets = 0m;
            int totalOrdersCount = 0;
            int actualOrdersCount = 0;

            foreach (DataRow r in temp.Rows)
            {
                int id = Convert.ToInt32(r["Id"]);

                if (_processedOrderIds.Contains(id))
                    continue;
                _processedOrderIds.Add(id);

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
                bool hasHallal = temp.Columns.Contains("MaHallal") && !(r["MaHallal"] is DBNull) && Convert.ToBoolean(r["MaHallal"]);
                bool czyMaCeny = temp.Columns.Contains("CzyMaCeny") && !(r["CzyMaCeny"] is DBNull) && Convert.ToBoolean(r["CzyMaCeny"]);
                string cenaInfo = czyMaCeny ? "✓" : "✗";

                long? transportKursId = null;
                if (temp.Columns.Contains("TransportKursID") && !(r["TransportKursID"] is DBNull))
                {
                    transportKursId = Convert.ToInt64(r["TransportKursID"]);
                }

                // Pobierz flagi CzyZrealizowane i CzyWydane
                bool czyZrealizowane = temp.Columns.Contains("CzyZrealizowane") && !(r["CzyZrealizowane"] is DBNull) && Convert.ToBoolean(r["CzyZrealizowane"]);
                bool czyWydane = temp.Columns.Contains("CzyWydane") && !(r["CzyWydane"] is DBNull) && Convert.ToBoolean(r["CzyWydane"]);

                int containers = r["LiczbaPojemnikow"] is DBNull ? 0 : (int)Math.Round(Convert.ToDecimal(r["LiczbaPojemnikow"]));
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
                    name = "📦 " + name;
                }

                if (hasHallal)
                {
                    name = "🔪 " + name;
                }

                decimal released = 0m;
                if (releasesPerClientProduct.TryGetValue(clientId, out var perProduct))
                {
                    released = selectedProductId.HasValue ?
                        perProduct.TryGetValue(selectedProductId.Value, out var w) ? w : 0m :
                        perProduct.Values.Sum();
                }

                // ✅ PRZYWRÓCONY ORYGINALNY FORMAT
                string pickupTerm = "";
                if (arrivalDate.HasValue)
                {
                    string dzienTygodnia = arrivalDate.Value.ToString("ddd", cultureInfo); // pełny dzień
                    pickupTerm = $"{arrivalDate.Value:yyyy-MM-dd} {dzienTygodnia} {arrivalDate.Value:HH:mm}";
                }
                else
                {
                    string dzienTygodnia = day.ToString("ddd", cultureInfo);
                    pickupTerm = $"{day:yyyy-MM-dd} {dzienTygodnia}";
                }

                // ✅ PRZYWRÓCONY ORYGINALNY FORMAT - pełne imię
                string createdBy = "";
                if (createdDate.HasValue)
                {
                    string userName = _userCache.TryGetValue(userId, out var user) ? user : "Brak";

                    // Wyciągnij tylko imię (pierwszy wyraz przed spacją)
                    string imie = userName.Contains(" ") ? userName.Split(' ')[0] : userName;

                    createdBy = $"{createdDate.Value:yyyy-MM-dd HH:mm} ({imie})";
                }
                else
                {
                    string userName = _userCache.TryGetValue(userId, out var user) ? user : "Brak";
                    string imie = userName.Contains(" ") ? userName.Split(' ')[0] : userName;
                    createdBy = imie;
                }

                // Termin - godzina przyjazdu + dzień tygodnia
                string terminInfo = "";
                if (arrivalDate.HasValue)
                {
                    string dzienTerminu = arrivalDate.Value.ToString("ddd", cultureInfo);
                    terminInfo = $"{arrivalDate.Value:HH:mm} {dzienTerminu}";
                }

                // Transport - godzina wyjazdu + dzień tygodnia (jeśli przypisany)
                string transportInfoColumn = "";
                if (transportKursId.HasValue && transportInfo.TryGetValue(transportKursId.Value, out var tInfo))
                {
                    if (tInfo.GodzWyjazdu.HasValue)
                    {
                        string dzienWyjazdu = tInfo.DataKursu.ToString("ddd", cultureInfo);
                        transportInfoColumn = $"{tInfo.GodzWyjazdu.Value:hh\\:mm} {dzienWyjazdu}";
                    }
                    else
                    {
                        transportInfoColumn = tInfo.DataKursu.ToString("ddd", cultureInfo);
                    }
                }

                // Wyprodukowano - czy zrealizowano w panelu produkcji (CzyZrealizowane = 1)
                string wyprodukowano = czyZrealizowane ? "✓" : "";

                // Wydano - godzina + dzień tygodnia (jeśli CzyWydane = 1)
                string wydanoInfo = "";
                DateTime? dataWydania = temp.Columns.Contains("DataWydania") && !(r["DataWydania"] is DBNull)
                    ? Convert.ToDateTime(r["DataWydania"]) : null;
                if (czyWydane && dataWydania.HasValue)
                {
                    string dzienWydania = dataWydania.Value.ToString("ddd", cultureInfo);
                    wydanoInfo = $"{dataWydania.Value:HH:mm} {dzienWydania}";
                }
                else if (czyWydane)
                {
                    wydanoInfo = "✓";
                }

                totalOrdersCount++;
                if (status != "Anulowane")
                {
                    totalOrdered += quantity;
                    totalReleased += released;
                    totalPallets += pallets;
                    actualOrdersCount++;
                }

                // Dodanie wiersza z kolumnami Cena, Termin, Transport, Wyprodukowano, Wydano
                _dtOrders.Rows.Add(
                    id, clientId, name, salesman, quantity, released, containers, pallets, modeText,
                    arrivalDate?.Date ?? day, arrivalDate?.ToString("HH:mm") ?? "08:00",
                    pickupTerm, slaughterDate.HasValue ? (object)slaughterDate.Value.Date : DBNull.Value,
                    createdBy, status, hasNote, hasFoil, hasHallal, czyMaCeny, cenaInfo,
                    terminInfo, transportInfoColumn, wyprodukowano, wydanoInfo
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

                string dzienTygodnia = day.ToString("ddd", cultureInfo);
                row["TerminOdbioru"] = $"{day:yyyy-MM-dd} {dzienTygodnia}";

                row["DataUboju"] = DBNull.Value;
                row["UtworzonePrzez"] = "";
                row["Status"] = "Wydanie bez zamówienia";
                row["MaNotatke"] = false;
                row["MaFolie"] = false;
                row["MaHallal"] = false;
                row["CzyMaCeny"] = false;
                row["CenaInfo"] = "";
                row["TerminInfo"] = "";
                row["TransportInfo"] = "";
                row["Wyprodukowano"] = "";
                // Wydano dla wydań bez zamówień - checkmark
                row["WydanoInfo"] = "✓";
                releasesWithoutOrders.Add(row);

                totalReleased += released;
            }

            foreach (var row in releasesWithoutOrders.OrderByDescending(r => (decimal)r["IloscFaktyczna"]))
                _dtOrders.Rows.Add(row.ItemArray);

            if (_dtOrders.Rows.Count > 0)
            {
                var tempData = new List<object[]>();
                foreach (DataRow row in _dtOrders.Rows)
                {
                    var rowCopy = new object[row.ItemArray.Length];
                    row.ItemArray.CopyTo(rowCopy, 0);
                    tempData.Add(rowCopy);
                }

                var sortedData = tempData.OrderBy(arr => arr[3]?.ToString() ?? "").ToList();

                _dtOrders.Rows.Clear();

                foreach (var rowData in sortedData)
                {
                    _dtOrders.Rows.Add(rowData);
                }
            }

            if (_dtOrders.Rows.Count > 0 && actualOrdersCount > 0)
            {
                var summaryRow = _dtOrders.NewRow();
                summaryRow["Id"] = -1;
                summaryRow["KlientId"] = 0;
                summaryRow["Odbiorca"] = "═══ SUMA ═══";

                // ✅ POPRAWKA 3: Tylko liczba zamówień (bez tekstu "Zamówień:")
                summaryRow["Handlowiec"] = actualOrdersCount.ToString();

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
                summaryRow["MaHallal"] = false;
                summaryRow["CzyMaCeny"] = false;
                summaryRow["CenaInfo"] = "";
                summaryRow["TerminInfo"] = "";
                summaryRow["TransportInfo"] = "";
                summaryRow["Wyprodukowano"] = "";
                summaryRow["WydanoInfo"] = "";

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

            // 1. Odbiorca - zmniejszona szerokość
            dgOrders.Columns.Add(new DataGridTextColumn
            {
                Header = "Odbiorca",
                Binding = new System.Windows.Data.Binding("Odbiorca"),
                Width = new DataGridLength(0.7, DataGridLengthUnitType.Star),
                MinWidth = 100
            });

            // 2. Handlowiec
            dgOrders.Columns.Add(new DataGridTextColumn
            {
                Header = "Hand.",
                Binding = new System.Windows.Data.Binding("Handlowiec"),
                Width = new DataGridLength(55),
                ElementStyle = (Style)FindResource("BoldCellStyle")
            });

            // 3. Zamówiono
            dgOrders.Columns.Add(new DataGridTextColumn
            {
                Header = "Zam.",
                Binding = new System.Windows.Data.Binding("IloscZamowiona") { StringFormat = "N0" },
                Width = new DataGridLength(65),
                ElementStyle = (Style)FindResource("RightAlignedCellStyle")
            });

            // 4. Wydano
            dgOrders.Columns.Add(new DataGridTextColumn
            {
                Header = "Wyd.",
                Binding = new System.Windows.Data.Binding("IloscFaktyczna") { StringFormat = "N0" },
                Width = new DataGridLength(65),
                ElementStyle = (Style)FindResource("RightAlignedCellStyle")
            });

            // 5. Utworzone przez - węższa kolumna
            dgOrders.Columns.Add(new DataGridTextColumn
            {
                Header = "Utworzono",
                Binding = new System.Windows.Data.Binding("UtworzonePrzez"),
                Width = new DataGridLength(0.7, DataGridLengthUnitType.Star),
                MinWidth = 90
            });

            // 6. Cena - V (zielony) jeśli wszystkie pozycje mają cenę, X (czerwony) jeśli nie
            var cenaStyle = new Style(typeof(TextBlock));
            cenaStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            cenaStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Bold));

            var greenTrigger = new DataTrigger { Binding = new System.Windows.Data.Binding("CzyMaCeny"), Value = true };
            greenTrigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0, 150, 0))));

            var redTrigger = new DataTrigger { Binding = new System.Windows.Data.Binding("CzyMaCeny"), Value = false };
            redTrigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(200, 0, 0))));

            cenaStyle.Triggers.Add(greenTrigger);
            cenaStyle.Triggers.Add(redTrigger);

            dgOrders.Columns.Add(new DataGridTextColumn
            {
                Header = "Cena",
                Binding = new System.Windows.Data.Binding("CenaInfo"),
                Width = new DataGridLength(45),
                ElementStyle = cenaStyle
            });

            // 7. Termin - godzina przyjazdu + dzień tygodnia
            dgOrders.Columns.Add(new DataGridTextColumn
            {
                Header = "Termin",
                Binding = new System.Windows.Data.Binding("TerminInfo"),
                Width = new DataGridLength(75),
                ElementStyle = (Style)FindResource("CenterAlignedCellStyle")
            });

            // 8. Transport - godzina wyjazdu + dzień tygodnia (jeśli przypisany)
            dgOrders.Columns.Add(new DataGridTextColumn
            {
                Header = "Transport",
                Binding = new System.Windows.Data.Binding("TransportInfo"),
                Width = new DataGridLength(75),
                ElementStyle = (Style)FindResource("CenterAlignedCellStyle")
            });

            // 9. Wyprodukowano - czy zrealizowano w panelu produkcji
            dgOrders.Columns.Add(new DataGridTextColumn
            {
                Header = "Wypr.",
                Binding = new System.Windows.Data.Binding("Wyprodukowano"),
                Width = new DataGridLength(50),
                ElementStyle = (Style)FindResource("CenterAlignedCellStyle")
            });

            // 10. Wydano - godzina + dzień tygodnia
            dgOrders.Columns.Add(new DataGridTextColumn
            {
                Header = "Wydano",
                Binding = new System.Windows.Data.Binding("WydanoInfo"),
                Width = new DataGridLength(75),
                ElementStyle = (Style)FindResource("CenterAlignedCellStyle")
            });
        }

        private async Task LoadTransportForDayAsync(DateTime day)
        {
            day = ValidateSqlDate(day);

            // Zawsze czyść dane przed ładowaniem
            _dtTransport.Rows.Clear();

            if (_dtTransport.Columns.Count == 0)
            {
                _dtTransport.Columns.Add("Id", typeof(int));
                _dtTransport.Columns.Add("Odbiorca", typeof(string));
                _dtTransport.Columns.Add("Handlowiec", typeof(string));
                _dtTransport.Columns.Add("IloscZamowiona", typeof(decimal));
                _dtTransport.Columns.Add("IloscWydana", typeof(decimal));
                _dtTransport.Columns.Add("Palety", typeof(decimal));
                _dtTransport.Columns.Add("Kierowca", typeof(string));
                _dtTransport.Columns.Add("Pojazd", typeof(string));
                _dtTransport.Columns.Add("GodzWyjazdu", typeof(string));
                _dtTransport.Columns.Add("Trasa", typeof(string));
                _dtTransport.Columns.Add("StatusTransportu", typeof(string));
                _dtTransport.Columns.Add("Uwagi", typeof(string));
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

            var transportInfo = new Dictionary<long, (string Kierowca, string Pojazd, TimeSpan? GodzWyjazdu, string Trasa, string Status)>();
            try
            {
                await using var cnTrans = new SqlConnection(_connTransport);
                await cnTrans.OpenAsync();

                string sqlKursy = @"SELECT k.KursID, CONCAT(ki.Imie, ' ', ki.Nazwisko) as Kierowca,
                                   p.Rejestracja as Pojazd, k.GodzWyjazdu, k.Trasa, k.Status
                                   FROM [dbo].[Kurs] k
                                   LEFT JOIN [dbo].[Kierowca] ki ON k.KierowcaID = ki.KierowcaID
                                   LEFT JOIN [dbo].[Pojazd] p ON k.PojazdID = p.PojazdID
                                   WHERE k.DataKursu = @Day";

                await using var cmdKursy = new SqlCommand(sqlKursy, cnTrans);
                cmdKursy.Parameters.AddWithValue("@Day", day.Date);
                await using var rdrKursy = await cmdKursy.ExecuteReaderAsync();

                while (await rdrKursy.ReadAsync())
                {
                    long kursId = rdrKursy.GetInt64(0);
                    string kierowca = rdrKursy.IsDBNull(1) ? "" : rdrKursy.GetString(1);
                    string pojazd = rdrKursy.IsDBNull(2) ? "" : rdrKursy.GetString(2);
                    TimeSpan? godzWyjazdu = rdrKursy.IsDBNull(3) ? null : rdrKursy.GetTimeSpan(3);
                    string trasa = rdrKursy.IsDBNull(4) ? "" : rdrKursy.GetString(4);
                    string status = rdrKursy.IsDBNull(5) ? "" : rdrKursy.GetString(5);
                    transportInfo[kursId] = (kierowca, pojazd, godzWyjazdu, trasa, status);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania danych transportu: {ex.Message}");
            }

            var releasesPerClientProduct = await GetReleasesPerClientProductAsync(day);
            var cultureInfo = new CultureInfo("pl-PL");

            await using (var cnLibra = new SqlConnection(_connLibra))
            {
                await cnLibra.OpenAsync();
                string dateColumn = (_showBySlaughterDate && _slaughterDateColumnExists) ? "DataUboju" : "DataZamowienia";

                string sql = $@"
                    SELECT zm.Id, zm.KlientId, SUM(ISNULL(zmt.Ilosc,0)) AS Ilosc,
                           zm.LiczbaPalet, zm.TransportKursID, zm.Uwagi
                    FROM [dbo].[ZamowieniaMieso] zm
                    LEFT JOIN [dbo].[ZamowieniaMiesoTowar] zmt ON zm.Id = zmt.ZamowienieId
                    WHERE {dateColumn} = @Day AND zm.TransportKursID IS NOT NULL
                    GROUP BY zm.Id, zm.KlientId, zm.LiczbaPalet, zm.TransportKursID, zm.Uwagi
                    ORDER BY zm.TransportKursID, zm.Id";

                await using var cmd = new SqlCommand(sql, cnLibra);
                cmd.Parameters.AddWithValue("@Day", day);

                await using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    int id = rdr.GetInt32(0);
                    int clientId = rdr.GetInt32(1);
                    decimal quantity = rdr.IsDBNull(2) ? 0m : rdr.GetDecimal(2);
                    decimal pallets = rdr.IsDBNull(3) ? 0m : rdr.GetDecimal(3);
                    long? transportKursId = rdr.IsDBNull(4) ? null : Convert.ToInt64(rdr.GetValue(4));
                    string uwagi = rdr.IsDBNull(5) ? "" : rdr.GetString(5);

                    var (name, salesman) = contractors.TryGetValue(clientId, out var c) ? c : ($"Nieznany ({clientId})", "");

                    decimal released = 0m;
                    if (releasesPerClientProduct.TryGetValue(clientId, out var perProduct))
                    {
                        released = perProduct.Values.Sum();
                    }

                    string kierowca = "", pojazd = "", godzWyjazdStr = "", trasa = "", statusTrans = "";
                    if (transportKursId.HasValue && transportInfo.TryGetValue(transportKursId.Value, out var tInfo))
                    {
                        kierowca = tInfo.Kierowca;
                        pojazd = tInfo.Pojazd;
                        godzWyjazdStr = tInfo.GodzWyjazdu.HasValue ? tInfo.GodzWyjazdu.Value.ToString(@"hh\:mm") : "";
                        trasa = tInfo.Trasa;
                        statusTrans = tInfo.Status;
                    }

                    _dtTransport.Rows.Add(id, name, salesman, quantity, released, pallets, kierowca, pojazd, godzWyjazdStr, trasa, statusTrans, uwagi);
                }
            }

            SetupTransportDataGrid();
        }

        private void SetupTransportDataGrid()
        {
            // Sortowanie po Trasie, potem po godzinie wyjazdu
            _dtTransport.DefaultView.Sort = "Trasa ASC, GodzWyjazdu ASC";
            dgTransport.ItemsSource = _dtTransport.DefaultView;
            dgTransport.Columns.Clear();

            dgTransport.LoadingRow -= DgTransport_LoadingRow;
            dgTransport.LoadingRow += DgTransport_LoadingRow;

            dgTransport.Columns.Add(new DataGridTextColumn
            {
                Header = "Odbiorca",
                Binding = new System.Windows.Data.Binding("Odbiorca"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = 120
            });

            dgTransport.Columns.Add(new DataGridTextColumn
            {
                Header = "Hand.",
                Binding = new System.Windows.Data.Binding("Handlowiec"),
                Width = new DataGridLength(55),
                ElementStyle = (Style)FindResource("BoldCellStyle")
            });

            dgTransport.Columns.Add(new DataGridTextColumn
            {
                Header = "Kierowca",
                Binding = new System.Windows.Data.Binding("Kierowca"),
                Width = new DataGridLength(150)
            });

            dgTransport.Columns.Add(new DataGridTextColumn
            {
                Header = "Pojazd",
                Binding = new System.Windows.Data.Binding("Pojazd"),
                Width = new DataGridLength(85)
            });

            dgTransport.Columns.Add(new DataGridTextColumn
            {
                Header = "Godz.",
                Binding = new System.Windows.Data.Binding("GodzWyjazdu"),
                Width = new DataGridLength(55),
                ElementStyle = (Style)FindResource("CenterAlignedCellStyle")
            });

            dgTransport.Columns.Add(new DataGridTextColumn
            {
                Header = "Trasa",
                Binding = new System.Windows.Data.Binding("Trasa"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = 180
            });

            dgTransport.Columns.Add(new DataGridTextColumn
            {
                Header = "Uwagi",
                Binding = new System.Windows.Data.Binding("Uwagi"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = 80
            });
        }

        private void DgTransport_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgTransport.SelectedItem is DataRowView rowView)
            {
                var id = rowView.Row.Field<int>("Id");
                if (id > 0)
                {
                    _currentOrderId = id;
                    _ = DisplayOrderDetailsAsync(id);
                }
            }
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
                    e.Row.FontStyle = FontStyles.Italic;
                }
                else if (!string.IsNullOrEmpty(salesman))
                {
                    var color = GetColorForSalesman(salesman);
                    e.Row.Background = new SolidColorBrush(color);
                }

                var pallets = rowView.Row.Field<decimal?>("Palety") ?? 0m;
                if (pallets > 34 && status != "Anulowane")
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

        private void DgTransport_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                var salesman = rowView.Row.Field<string>("Handlowiec") ?? "";
                var statusTrans = rowView.Row.Field<string>("StatusTransportu") ?? "";

                // Kolorowanie według statusu transportu
                if (statusTrans == "Wydany")
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(200, 255, 200)); // Jasny zielony
                }
                else if (statusTrans == "W drodze")
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(255, 255, 200)); // Jasny żółty
                }
                else if (!string.IsNullOrEmpty(salesman))
                {
                    // Kolorowanie według handlowca (tak samo jak w zakładce Zamówienia)
                    var color = GetColorForSalesman(salesman);
                    e.Row.Background = new SolidColorBrush(color);
                }
            }
        }

        private void SetupHistoriaZmianDataGrid()
        {
            dgHistoriaZmian.ItemsSource = _dtHistoriaZmian.DefaultView;
            dgHistoriaZmian.Columns.Clear();

            dgHistoriaZmian.Columns.Add(new DataGridTextColumn
            {
                Header = "Zamówienie",
                Binding = new System.Windows.Data.Binding("ZamowienieId"),
                Width = new DataGridLength(85),
                ElementStyle = (Style)FindResource("CenterAlignedCellStyle")
            });

            dgHistoriaZmian.Columns.Add(new DataGridTextColumn
            {
                Header = "Data zmiany",
                Binding = new System.Windows.Data.Binding("DataZmiany") { StringFormat = "yyyy-MM-dd HH:mm" },
                Width = new DataGridLength(130)
            });

            dgHistoriaZmian.Columns.Add(new DataGridTextColumn
            {
                Header = "Typ",
                Binding = new System.Windows.Data.Binding("TypZmiany"),
                Width = new DataGridLength(90),
                ElementStyle = (Style)FindResource("CenterAlignedCellStyle")
            });

            dgHistoriaZmian.Columns.Add(new DataGridTextColumn
            {
                Header = "Handlowiec",
                Binding = new System.Windows.Data.Binding("Handlowiec"),
                Width = new DataGridLength(70),
                ElementStyle = (Style)FindResource("BoldCellStyle")
            });

            dgHistoriaZmian.Columns.Add(new DataGridTextColumn
            {
                Header = "Odbiorca",
                Binding = new System.Windows.Data.Binding("Odbiorca"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = 100
            });

            dgHistoriaZmian.Columns.Add(new DataGridTextColumn
            {
                Header = "Kto edytował",
                Binding = new System.Windows.Data.Binding("UzytkownikNazwa"),
                Width = new DataGridLength(120)
            });

            dgHistoriaZmian.Columns.Add(new DataGridTextColumn
            {
                Header = "Opis zmiany",
                Binding = new System.Windows.Data.Binding("OpisZmiany"),
                Width = new DataGridLength(1.5, DataGridLengthUnitType.Star),
                MinWidth = 150
            });
        }

        private async Task LoadHistoriaZmianAsync(DateTime startDate, DateTime endDate)
        {
            // Zawsze czyść dane przed ładowaniem
            _dtHistoriaZmian.Rows.Clear();

            if (_dtHistoriaZmian.Columns.Count == 0)
            {
                _dtHistoriaZmian.Columns.Add("Id", typeof(int));
                _dtHistoriaZmian.Columns.Add("ZamowienieId", typeof(int));
                _dtHistoriaZmian.Columns.Add("DataZmiany", typeof(DateTime));
                _dtHistoriaZmian.Columns.Add("TypZmiany", typeof(string));
                _dtHistoriaZmian.Columns.Add("Handlowiec", typeof(string));
                _dtHistoriaZmian.Columns.Add("Odbiorca", typeof(string));
                _dtHistoriaZmian.Columns.Add("UzytkownikNazwa", typeof(string));
                _dtHistoriaZmian.Columns.Add("OpisZmiany", typeof(string));
            }

            // Pobierz dane kontrahentów
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

            // Pobierz mapowanie zamówień do klientów
            var orderToClient = new Dictionary<int, int>();
            await using (var cnLibra = new SqlConnection(_connLibra))
            {
                await cnLibra.OpenAsync();

                // Sprawdź czy tabela historii istnieje
                string checkSql = @"SELECT COUNT(*) FROM sys.objects
                                   WHERE object_id = OBJECT_ID(N'[dbo].[HistoriaZmianZamowien]') AND type in (N'U')";
                using var checkCmd = new SqlCommand(checkSql, cnLibra);
                var tableExists = (int)await checkCmd.ExecuteScalarAsync() > 0;

                if (!tableExists)
                {
                    SetupHistoriaZmianDataGrid();
                    return;
                }

                // Pobierz zamówienia z zakresu dat
                string dateColumn = (_showBySlaughterDate && _slaughterDateColumnExists) ? "DataUboju" : "DataZamowienia";
                string sqlOrders = $@"SELECT Id, KlientId FROM dbo.ZamowieniaMieso
                                     WHERE {dateColumn} BETWEEN @StartDate AND @EndDate";
                await using var cmdOrders = new SqlCommand(sqlOrders, cnLibra);
                cmdOrders.Parameters.AddWithValue("@StartDate", startDate);
                cmdOrders.Parameters.AddWithValue("@EndDate", endDate);
                await using var rdrOrders = await cmdOrders.ExecuteReaderAsync();

                while (await rdrOrders.ReadAsync())
                {
                    int orderId = rdrOrders.GetInt32(0);
                    int clientId = rdrOrders.GetInt32(1);
                    orderToClient[orderId] = clientId;
                }

                await rdrOrders.CloseAsync();

                if (orderToClient.Count == 0)
                {
                    SetupHistoriaZmianDataGrid();
                    return;
                }

                // Pobierz historię zmian dla tych zamówień
                string orderIds = string.Join(",", orderToClient.Keys);
                string sqlHistory = $@"SELECT Id, ZamowienieId, DataZmiany, TypZmiany, UzytkownikNazwa, OpisZmiany
                                      FROM HistoriaZmianZamowien
                                      WHERE ZamowienieId IN ({orderIds})
                                      ORDER BY DataZmiany DESC";

                await using var cmdHistory = new SqlCommand(sqlHistory, cnLibra);
                await using var rdrHistory = await cmdHistory.ExecuteReaderAsync();

                while (await rdrHistory.ReadAsync())
                {
                    int id = rdrHistory.GetInt32(0);
                    int zamowienieId = rdrHistory.GetInt32(1);
                    DateTime dataZmiany = rdrHistory.GetDateTime(2);
                    string typZmiany = rdrHistory.IsDBNull(3) ? "" : rdrHistory.GetString(3);
                    string uzytkownikNazwa = rdrHistory.IsDBNull(4) ? "" : rdrHistory.GetString(4);
                    string opisZmiany = rdrHistory.IsDBNull(5) ? "" : rdrHistory.GetString(5);

                    string handlowiec = "";
                    string odbiorca = "";
                    if (orderToClient.TryGetValue(zamowienieId, out int clientId) &&
                        contractors.TryGetValue(clientId, out var contr))
                    {
                        handlowiec = contr.Salesman;
                        odbiorca = contr.Name;
                    }

                    _dtHistoriaZmian.Rows.Add(id, zamowienieId, dataZmiany, typZmiany,
                        handlowiec, odbiorca, uzytkownikNazwa, opisZmiany);
                }
            }

            SetupHistoriaZmianDataGrid();
        }


        private async Task<Dictionary<long, (DateTime DataKursu, TimeSpan? GodzWyjazdu, string Kierowca)>> GetTransportInfoAsync(DateTime day)
        {
            var result = new Dictionary<long, (DateTime DataKursu, TimeSpan? GodzWyjazdu, string Kierowca)>();
            try
            {
                await using var cn = new SqlConnection(_connTransport);
                await cn.OpenAsync();

                string sql = @"SELECT k.KursID, k.DataKursu, k.GodzWyjazdu,
                              CONCAT(ki.Imie, ' ', ki.Nazwisko) as Kierowca
                              FROM [dbo].[Kurs] k
                              LEFT JOIN [dbo].[Kierowca] ki ON k.KierowcaID = ki.KierowcaID
                              WHERE k.DataKursu = @Day";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Day", day.Date);
                await using var rdr = await cmd.ExecuteReaderAsync();

                while (await rdr.ReadAsync())
                {
                    long kursId = rdr.GetInt64(0);
                    DateTime dataKursu = rdr.GetDateTime(1);
                    TimeSpan? godzWyjazdu = rdr.IsDBNull(2) ? null : rdr.GetTimeSpan(2);
                    string kierowca = rdr.IsDBNull(3) ? "" : rdr.GetString(3);
                    result[kursId] = (dataKursu, godzWyjazdu, kierowca);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania danych transportu: {ex.Message}");
            }
            return result;
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
                var orderItems = new List<(int ProductCode, decimal Quantity, bool Foil, bool Hallal, string Cena)>(); // STRING!
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
                SELECT KodTowaru, Ilosc, ISNULL(Folia, 0) as Folia, ISNULL(Hallal, 0) as Hallal, ISNULL(Cena, '0') as Cena
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
                            bool hallal = readerItems.GetBoolean(3);
                            string cenaStr = readerItems.GetString(4); // STRING z bazy!

                            orderItems.Add((productCode, quantity, foil, hallal, cenaStr));
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
                dt.Columns.Add("Hallal", typeof(string));
                dt.Columns.Add("Cena", typeof(string)); // STRING w DataTable

                var cultureInfo = new CultureInfo("pl-PL");

                foreach (var item in orderItems)
                {
                    if (!_productCatalogCache.ContainsKey(item.ProductCode))
                        continue;

                    string product = _productCatalogCache.TryGetValue(item.ProductCode, out var code) ?
                        code : $"Nieznany ({item.ProductCode})";
                    decimal ordered = item.Quantity;
                    decimal released = releases.TryGetValue(item.ProductCode, out var w) ? w : 0m;
                    decimal difference = released - ordered;

                    string foliaText = item.Foil ? "TAK" : "";
                    string hallalText = item.Hallal ? "🔪" : "";

                    // Konwertuj string na decimal dla wyświetlenia
                    string cenaText = "";
                    if (!string.IsNullOrWhiteSpace(item.Cena) &&
                        decimal.TryParse(item.Cena, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal cenaValue) &&
                        cenaValue > 0)
                    {
                        cenaText = $"{cenaValue.ToString("N2", cultureInfo)} zł";
                    }

                    dt.Rows.Add(product, ordered, released, difference, foliaText, hallalText, cenaText);
                    releases.Remove(item.ProductCode);
                }

                foreach (var kv in releases)
                {
                    if (!_productCatalogCache.ContainsKey(kv.Key))
                        continue;

                    string product = _productCatalogCache.TryGetValue(kv.Key, out var code) ?
                        code : $"Nieznany ({kv.Key})";
                    dt.Rows.Add(product, 0m, kv.Value, kv.Value, "", "", "");
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
                MessageBox.Show($"Błąd podczas wczytywania szczegółów zamówienia:\n{ex.Message}\n\nStack:\n{ex.StackTrace}",
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
                Width = new DataGridLength(1, DataGridLengthUnitType.Star), // elastyczna
                MinWidth = 80
            });

            dgDetails.Columns.Add(new DataGridTextColumn
            {
                Header = "Zam.",
                Binding = new System.Windows.Data.Binding("Zamówiono") { StringFormat = "N0" },
                Width = new DataGridLength(60),
                ElementStyle = (Style)FindResource("RightAlignedCellStyle")
            });

            dgDetails.Columns.Add(new DataGridTextColumn
            {
                Header = "Wyd.",
                Binding = new System.Windows.Data.Binding("Wydano") { StringFormat = "N0" },
                Width = new DataGridLength(60),
                ElementStyle = (Style)FindResource("RightAlignedCellStyle")
            });

            dgDetails.Columns.Add(new DataGridTextColumn
            {
                Header = "Róż.",
                Binding = new System.Windows.Data.Binding("Różnica") { StringFormat = "N0" },
                Width = new DataGridLength(60),
                ElementStyle = (Style)FindResource("RightAlignedCellStyle")
            });

            dgDetails.Columns.Add(new DataGridTextColumn
            {
                Header = "Folia",
                Binding = new System.Windows.Data.Binding("Folia"),
                Width = new DataGridLength(45),
                ElementStyle = (Style)FindResource("CenterAlignedCellStyle")
            });

            dgDetails.Columns.Add(new DataGridTextColumn
            {
                Header = "Hallal",
                Binding = new System.Windows.Data.Binding("Hallal"),
                Width = new DataGridLength(45),
                ElementStyle = (Style)FindResource("CenterAlignedCellStyle")
            });

            dgDetails.Columns.Add(new DataGridTextColumn
            {
                Header = "Cena",
                Binding = new System.Windows.Data.Binding("Cena"),
                Width = new DataGridLength(70),
                ElementStyle = (Style)FindResource("RightAlignedCellStyle")
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
            dtAgg.Columns.Add("Stan", typeof(string));
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

            // ✅ POBIERZ STANY MAGAZYNOWE
            var stanyMagazynowe = new Dictionary<int, decimal>();
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                const string sqlStany = @"
            SELECT ProduktId, Stan 
            FROM dbo.StanyMagazynowe 
            WHERE Data = @Data";

                await using var cmd = new SqlCommand(sqlStany, cn);
                cmd.Parameters.AddWithValue("@Data", day.Date);
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    int produktId = reader.GetInt32(0);
                    decimal stan = reader.GetDecimal(1);
                    stanyMagazynowe[produktId] = stan;
                }
            }
            catch
            {
                // Tabela może nie istnieć jeszcze - ignoruj błąd
            }

            var kurczakA = _productCatalogCache.FirstOrDefault(p =>
                p.Value.Contains("Kurczak A", StringComparison.OrdinalIgnoreCase));

            decimal planA = 0m, factA = 0m, ordersA = 0m, balanceA = 0m;
            string stanA = "";
            decimal stanMagA = 0m;

            if (kurczakA.Key > 0)
            {
                planA = pulaTuszkiA;
                factA = actualIncomeTuszkaA.TryGetValue(kurczakA.Key, out var a) ? a : 0m;
                ordersA = orderSum.TryGetValue(kurczakA.Key, out var z) ? z : 0m;

                // ✅ POBIERZ STAN MAGAZYNOWY
                stanMagA = stanyMagazynowe.TryGetValue(kurczakA.Key, out var sA) ? sA : 0m;
                stanA = stanMagA > 0 ? stanMagA.ToString("N0") : "";

                // ✅ NOWA LOGIKA BILANSU: (Fakt lub Plan) + Stan - Zamówienia
                if (factA > 0)
                {
                    balanceA = factA + stanMagA - ordersA;
                }
                else
                {
                    balanceA = planA + stanMagA - ordersA;
                }
            }

            decimal sumaPlanB = 0m;
            decimal sumaFaktB = 0m;
            decimal sumaZamB = 0m;
            decimal sumaStanB = 0m;

            var produktyB = new List<(string nazwa, decimal plan, decimal fakt, decimal zam, string stan, decimal stanDec, decimal bilans)>();

            foreach (var produktKonfig in konfiguracjaProduktow.OrderByDescending(p => p.Value))
            {
                int produktId = produktKonfig.Key;
                decimal procentUdzialu = produktKonfig.Value;

                if (!_productCatalogCache.ContainsKey(produktId))
                    continue;

                string nazwaProdukt = _productCatalogCache[produktId];

                if (nazwaProdukt.Contains("Kurczak B", StringComparison.OrdinalIgnoreCase))
                    continue;

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

                // ✅ POBIERZ STAN MAGAZYNOWY
                decimal stanMag = stanyMagazynowe.TryGetValue(produktId, out var sm) ? sm : 0m;
                string stanText = stanMag > 0 ? stanMag.ToString("N0") : "";

                // ✅ NOWA LOGIKA BILANSU: (Fakt lub Plan) + Stan - Zamówienia
                decimal balance;
                if (actual > 0)
                {
                    balance = actual + stanMag - orders;
                }
                else
                {
                    balance = plannedForProduct + stanMag - orders;
                }

                string nazwaZIkonka = string.IsNullOrEmpty(ikona)
                    ? $"  └ {nazwaProdukt} ({procentUdzialu:F1}%)"
                    : $"  └ {ikona} {nazwaProdukt} ({procentUdzialu:F1}%)";

                produktyB.Add((nazwaZIkonka, plannedForProduct, actual, orders, stanText, stanMag, balance));

                sumaPlanB += plannedForProduct;
                sumaFaktB += actual;
                sumaZamB += orders;
                sumaStanB += stanMag;
            }

            // ✅ BILANS CAŁKOWITY DLA KURCZAKA B
            decimal bilansB;
            if (sumaFaktB > 0)
            {
                bilansB = sumaFaktB + sumaStanB - sumaZamB;
            }
            else
            {
                bilansB = sumaPlanB + sumaStanB - sumaZamB;
            }

            // ✅ BILANS CAŁKOWITY
            decimal bilansCalk = balanceA + bilansB;

            // ✅ DODAJ WIERSZE DO TABELI
            dtAgg.Rows.Add("═══ SUMA CAŁKOWITA ═══",
                planA + sumaPlanB,
                factA + sumaFaktB,
                (stanMagA + sumaStanB > 0 ? (stanMagA + sumaStanB).ToString("N0") : ""),
                ordersA + sumaZamB,
                bilansCalk);

            dtAgg.Rows.Add("🐔 Kurczak A",
                planA,
                factA,
                stanA,
                ordersA,
                balanceA);

            dtAgg.Rows.Add("🐔 Kurczak B",
                sumaPlanB,
                sumaFaktB,
                (sumaStanB > 0 ? sumaStanB.ToString("N0") : ""),
                sumaZamB,
                bilansB);

            foreach (var produkt in produktyB)
            {
                dtAgg.Rows.Add(produkt.nazwa, produkt.plan, produkt.fakt, produkt.stan, produkt.zam, produkt.bilans);
            }

            dgAggregation.ItemsSource = dtAgg.DefaultView;
            SetupAggregationDataGrid();
        }
        private void DgAggregation_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                var produkt = rowView.Row.Field<string>("Produkt") ?? "";

                if (produkt.StartsWith("═══ SUMA CAŁKOWITA"))
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                    e.Row.Foreground = Brushes.White;
                    e.Row.FontWeight = FontWeights.Bold;
                    e.Row.FontSize = 14;
                    e.Row.Height = 40; // Standardowa wysokość dla sumy
                }
                else if (produkt.StartsWith("🐔 Kurczak A"))
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(200, 230, 201));
                    e.Row.FontWeight = FontWeights.SemiBold;
                    e.Row.FontSize = 13;
                    e.Row.Height = 36;
                }
                else if (produkt.StartsWith("🐔 Kurczak B"))
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(179, 229, 252));
                    e.Row.FontWeight = FontWeights.SemiBold;
                    e.Row.FontSize = 13;
                    e.Row.Height = 36;
                }
                else if (produkt.StartsWith("  └"))
                {
                    // ✅ POPRAWKA 14: Mniejsze wiersze dla elementów Kurczaka B
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(225, 245, 254));
                    e.Row.FontStyle = FontStyles.Normal;
                    e.Row.FontSize = 10; // Zmniejszone z 11
                    e.Row.Height = 24;    // Zmniejszone z 32
                }
            }
        }
        private void SetupAggregationDataGrid()
        {
            dgAggregation.Columns.Clear();

            // 1. PLAN
            var planColumn = new DataGridTemplateColumn
            {
                Header = "Plan",
                Width = new DataGridLength(70)
            };

            var planTemplate = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(TextBlock));
            factory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("PlanowanyPrzychód")
            {
                StringFormat = "N0"
            });
            factory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right);
            factory.SetValue(TextBlock.PaddingProperty, new Thickness(0, 0, 8, 0));

            var multiBinding = new System.Windows.Data.MultiBinding();
            multiBinding.Converter = new StrikethroughConverter();
            multiBinding.Bindings.Add(new System.Windows.Data.Binding("FaktycznyPrzychód"));
            factory.SetBinding(TextBlock.TextDecorationsProperty, multiBinding);

            planTemplate.VisualTree = factory;
            planColumn.CellTemplate = planTemplate;
            dgAggregation.Columns.Add(planColumn);

            // 2. FAKT
            dgAggregation.Columns.Add(new DataGridTextColumn
            {
                Header = "Fakt",
                Binding = new System.Windows.Data.Binding("FaktycznyPrzychód") { StringFormat = "N0" },
                Width = new DataGridLength(70),
                ElementStyle = (Style)FindResource("RightAlignedCellStyle")
            });

            // 3. STAN - ✅ TERAZ PO FAKT
            dgAggregation.Columns.Add(new DataGridTextColumn
            {
                Header = "Stan",
                Binding = new System.Windows.Data.Binding("Stan"),
                Width = new DataGridLength(60),
                ElementStyle = (Style)FindResource("CenterAlignedCellStyle")
            });

            // 4. ZAM.
            dgAggregation.Columns.Add(new DataGridTextColumn
            {
                Header = "Zam.",
                Binding = new System.Windows.Data.Binding("Zamówienia") { StringFormat = "N0" },
                Width = new DataGridLength(70),
                ElementStyle = (Style)FindResource("RightAlignedCellStyle")
            });

            // 5. BILANS
            dgAggregation.Columns.Add(new DataGridTextColumn
            {
                Header = "Bil.",
                Binding = new System.Windows.Data.Binding("Bilans") { StringFormat = "N0" },
                Width = new DataGridLength(70),
                ElementStyle = (Style)FindResource("RightAlignedCellStyle")
            });

            // 6. PRODUKT
            dgAggregation.Columns.Add(new DataGridTextColumn
            {
                Header = "Produkt",
                Binding = new System.Windows.Data.Binding("Produkt"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = 150
            });

            dgAggregation.LoadingRow -= DgAggregation_LoadingRow;
            dgAggregation.LoadingRow += DgAggregation_LoadingRow;

            dgAggregation.MouseDoubleClick -= DgAggregation_MouseDoubleClick;
            dgAggregation.MouseDoubleClick += DgAggregation_MouseDoubleClick;
        }
        private async void DgAggregation_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgAggregation.SelectedItem is DataRowView rowView)
            {
                var produktNazwa = rowView.Row.Field<string>("Produkt") ?? "";

                if (produktNazwa.StartsWith("═══"))
                    return;

                var czystyProdukt = produktNazwa
                    .Replace("└", "")
                    .Replace("🍗", "")
                    .Replace("🍖", "")
                    .Replace("🥩", "")
                    .Replace("🐔", "")
                    .Replace("  ", " ")
                    .Trim();

                int indexProcentu = czystyProdukt.IndexOf("(");
                if (indexProcentu > 0)
                    czystyProdukt = czystyProdukt.Substring(0, indexProcentu).Trim();

                var plan = rowView.Row.Field<decimal?>("PlanowanyPrzychód") ?? 0m;
                var fakt = rowView.Row.Field<decimal?>("FaktycznyPrzychód") ?? 0m;
                var zamowienia = rowView.Row.Field<decimal?>("Zamówienia") ?? 0m;
                var bilans = rowView.Row.Field<decimal?>("Bilans") ?? 0m;

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

                var cmd = new SqlCommand(@"
            SELECT TOP 1 ID 
            FROM [HANDEL].[HM].[TW] 
            WHERE kod = @Nazwa
              AND katalog IN (67095, 67153)", cn);
                cmd.Parameters.AddWithValue("@Nazwa", nazwaProdukt.Trim());

                var result = await cmd.ExecuteScalarAsync();

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
            var txt = txtFilterRecipient.Text?.Trim().Replace("'", "''");
            string salesmanFilter = null;
            if (cbFilterSalesman.SelectedIndex > 0)
                salesmanFilter = cbFilterSalesman.SelectedItem?.ToString()?.Replace("'", "''");

            // Filtruj zamówienia
            if (_dtOrders.DefaultView != null)
            {
                var conditions = new List<string>();

                if (!string.IsNullOrEmpty(txt))
                    conditions.Add($"Odbiorca LIKE '%{txt}%'");

                if (!string.IsNullOrEmpty(salesmanFilter))
                    conditions.Add($"Handlowiec = '{salesmanFilter}'");

                if (!_showReleasesWithoutOrders)
                    conditions.Add("Status <> 'Wydanie bez zamówienia'");

                if (!_showAnulowane)
                    conditions.Add("Status <> 'Anulowane'");

                _dtOrders.DefaultView.RowFilter = string.Join(" AND ", conditions);
            }

            // Filtruj Transport
            if (_dtTransport.DefaultView != null && _dtTransport.Columns.Contains("Odbiorca"))
            {
                var transportConditions = new List<string>();
                if (!string.IsNullOrEmpty(txt))
                    transportConditions.Add($"Odbiorca LIKE '%{txt}%'");
                _dtTransport.DefaultView.RowFilter = string.Join(" AND ", transportConditions);
            }

            // Filtruj Historię zmian
            if (_dtHistoriaZmian.DefaultView != null && _dtHistoriaZmian.Columns.Contains("Odbiorca"))
            {
                var historiaConditions = new List<string>();
                if (!string.IsNullOrEmpty(txt))
                    historiaConditions.Add($"Odbiorca LIKE '%{txt}%'");
                _dtHistoriaZmian.DefaultView.RowFilter = string.Join(" AND ", historiaConditions);
            }
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

                // POPRAWIONE - Cena to VARCHAR
                var cmdCopyItems = new SqlCommand(@"INSERT INTO ZamowieniaMiesoTowar 
            (ZamowienieId, KodTowaru, Ilosc, Cena, Pojemniki, Palety, E2, Folia, Hallal) 
            SELECT @newId, KodTowaru, Ilosc, Cena, Pojemniki, Palety, E2, Folia, Hallal 
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

        #region Support Classes

        public class TransportInfo
        {
            public long KursId { get; set; }
            public DateTime DataKursu { get; set; }
            public string Trasa { get; set; } = "";
            public TimeSpan? GodzWyjazdu { get; set; }
            public TimeSpan? GodzPowrotu { get; set; }
            public string Status { get; set; } = "";
            public string Kierowca { get; set; } = "";
            public string TelefonKierowcy { get; set; } = "";
            public string Rejestracja { get; set; } = "";
            public string MarkaPojazdu { get; set; } = "";
            public string ModelPojazdu { get; set; } = "";
            public int MaxPalety { get; set; }
            public List<LadunekInfo> Ladunki { get; set; } = new List<LadunekInfo>();
        }

        public class LadunekInfo
        {
            public int Kolejnosc { get; set; }
            public string KodKlienta { get; set; } = "";
            public string NazwaKlienta { get; set; } = "";
            public int Palety { get; set; }
            public int Pojemniki { get; set; }
            public string Uwagi { get; set; } = "";
        }

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
                            Pen = new Pen(Brushes.Black, 2)
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

        #endregion
    }
   
}
