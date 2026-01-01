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
using Kalendarz1;
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
        private string _originalNotesValue = ""; // Śledzenie oryginalnej wartości notatek do edycji
        private readonly List<Button> _dayButtons = new();
        private readonly Dictionary<Button, DateTime> _dayButtonDates = new();
        private Button? btnToday; // Dodaj to pole na początku klasy

        private bool _showBySlaughterDate = true;
        private bool _slaughterDateColumnExists = true;
        private bool _walutaColumnExists = false;
        private bool _isInitialized = false;
        private bool _showAnulowane = false;
        private bool _isRefreshing = false;
        private List<(string name, long ms)> _lastLoadOrdersDiag = new();
        private List<(string name, long ms)> _lastAggregationDiag = new();
        private System.Windows.Threading.DispatcherTimer _autoRefreshTimer;

        // PERFORMANCE: Debouncing dla filtrów - opóźnienie 300ms
        private System.Windows.Threading.DispatcherTimer _filterDebounceTimer;
        private const int FILTER_DEBOUNCE_MS = 300;

        private readonly DataTable _dtOrders = new();
        private readonly DataTable _dtTransport = new();
        private readonly DataTable _dtHistoriaZmian = new();
        private readonly DataTable _dtDashboard = new();
        private GridLength _savedRightColumnWidth = new GridLength(550);
        private readonly Dictionary<int, string> _productCodeCache = new();
        private readonly Dictionary<int, string> _productCatalogCache = new();
        private readonly Dictionary<int, string> _productCatalogSwieze = new();
        private readonly Dictionary<int, string> _productCatalogMrozone = new();
        private Dictionary<int, string> _mapowanieScalowania = new(); // TowarIdtw -> NazwaGrupy
        private Dictionary<string, List<int>> _grupyDoProduktow = new(); // NazwaGrupy -> lista TowarId
        private List<string> _grupyTowaroweNazwy = new(); // Lista nazw grup towarowych dla kolumn w tabeli zamówień
        private Dictionary<string, string> _grupyKolumnDoNazw = new(); // Sanitized column name -> original display name
        private HashSet<string> _expandedGroups = new(); // Rozwinięte grupy
        private Dictionary<string, List<(string nazwa, decimal plan, decimal fakt, decimal zam, string stan, decimal stanDec, decimal bilans)>> _detaleGrup = new(); // Szczegóły produktów w grupach
        private int? _selectedProductId = null;
        private readonly Dictionary<string, string> _userCache = new();
        private readonly List<string> _salesmenCache = new();
        private bool _showReleasesWithoutOrders = false;
        private HashSet<string> _expandedDashboardProducts = new(); // Rozwinięte produkty w dashboardzie
        private Dictionary<string, List<(string odbiorca, decimal ilosc, string handlowiec)>> _orderDetailsPerProduct = new(); // Szczegóły zamówień per produkt

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
            InicjalizujMenuKontekstoweBilansu();

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

            // PERFORMANCE: Debouncing timer dla filtrów
            _filterDebounceTimer = new System.Windows.Threading.DispatcherTimer();
            _filterDebounceTimer.Interval = TimeSpan.FromMilliseconds(FILTER_DEBOUNCE_MS);
            _filterDebounceTimer.Tick += FilterDebounceTimer_Tick;

            // Skróty klawiaturowe
            this.KeyDown += MainWindow_KeyDown;
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;

            // Domyślny fokus w filtrze
            txtFilterRecipient.Focus();

            // Obsługa edycji notatek - zapisz przy utracie fokusa
            txtNotes.LostFocus += TxtNotes_LostFocus;
        }

        private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // F5 - Odśwież (nawet gdy fokus jest gdzie indziej)
            if (e.Key == System.Windows.Input.Key.F5)
            {
                _ = RefreshAllDataAsync();
                e.Handled = true;
            }
        }

        private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Ctrl+F - Fokus na filtr
            if (e.Key == System.Windows.Input.Key.F &&
                (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
            {
                txtFilterRecipient.Focus();
                txtFilterRecipient.SelectAll();
                e.Handled = true;
            }
            // Escape - Wyczyść filtr lub zamknij
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                if (!string.IsNullOrEmpty(txtFilterRecipient.Text))
                {
                    txtFilterRecipient.Text = "";
                    e.Handled = true;
                }
            }
            // Ctrl+N - Nowe zamówienie
            else if (e.Key == System.Windows.Input.Key.N &&
                (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
            {
                BtnNew_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
            // Ctrl+P - Drukuj
            else if (e.Key == System.Windows.Input.Key.P &&
                (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
            {
                BtnPrint_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private void FilterDebounceTimer_Tick(object sender, EventArgs e)
        {
            _filterDebounceTimer.Stop();
            ApplyFilters();
        }

        private async void AutoRefreshTimer_Tick(object sender, EventArgs e)
        {
            await RefreshAllDataAsync();
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await RefreshAllDataAsync();
        }

        #region Przyciski otwierania osobnych okien

        private void BtnOpenDashboard_Click(object sender, RoutedEventArgs e)
        {
            var dashboardWindow = new DashboardWindow(_connLibra, _connHandel, _selectedDate);
            dashboardWindow.Owner = this;
            dashboardWindow.Show();
        }

        private void BtnOpenStatystyki_Click(object sender, RoutedEventArgs e)
        {
            var statystykiWindow = new StatystykiWindow(_connLibra, _connHandel);
            statystykiWindow.Owner = this;
            statystykiWindow.Show();
        }

        private void BtnOpenHistoria_Click(object sender, RoutedEventArgs e)
        {
            var historiaWindow = new HistoriaZmianWindow(_connLibra, _connHandel, UserID);
            historiaWindow.Owner = this;
            historiaWindow.Show();
        }

        private void BtnOpenTransport_Click(object sender, RoutedEventArgs e)
        {
            var transportWindow = new TransportWindow(_connLibra, _connHandel, _connTransport, _selectedDate);
            transportWindow.Owner = this;
            transportWindow.Show();
        }

        #endregion

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
            _mapowanieScalowania.Clear(); // Odśwież mapowanie scalowania
            _grupyDoProduktow.Clear(); // Odśwież mapowanie grup
            _grupyTowaroweNazwy.Clear(); // Odśwież listę nazw grup

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                // Sprawdź czy kolumna GrupaScalowania istnieje
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

                    // Pobierz grupę scalania jeśli istnieje
                    if (hasGrupaColumn)
                    {
                        var grupaOrdinal = reader.GetOrdinal("GrupaScalowania");
                        if (!reader.IsDBNull(grupaOrdinal))
                        {
                            string grupa = reader.GetString(grupaOrdinal);
                            if (!string.IsNullOrWhiteSpace(grupa))
                            {
                                _mapowanieScalowania[towarId] = grupa;

                                // Buduj słownik grup -> produkty
                                if (!_grupyDoProduktow.ContainsKey(grupa))
                                    _grupyDoProduktow[grupa] = new List<int>();
                                _grupyDoProduktow[grupa].Add(towarId);
                            }
                        }
                    }
                }

                // Jeśli nie ma grup z KonfiguracjaProduktow, pobierz z ScalowanieTowarow
                if (!_mapowanieScalowania.Any())
                {
                    await LoadScalowanieTowarowAsync(cn);
                }

                // Ustaw listę nazw grup towarowych (posortowane)
                _grupyTowaroweNazwy = _grupyDoProduktow.Keys.OrderBy(n => n).ToList();

                return result;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd pobierania konfiguracji produktów: {ex.Message}",
                    "Ostrzeżenie", MessageBoxButton.OK, MessageBoxImage.Warning);
                return result;
            }
        }

        /// <summary>
        /// Pobiera mapowanie scalowania towarów z tabeli ScalowanieTowarow
        /// </summary>
        private async Task LoadScalowanieTowarowAsync(SqlConnection cn)
        {
            try
            {
                // Sprawdź czy tabela istnieje
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
            catch { /* Ignoruj błędy - tabela może nie istnieć */ }
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
            btnToday.Click += async (s, e) =>
            {
                _selectedDate = DateTime.Today;
                UpdateDayButtonDates();
                await RefreshAllDataAsync();
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
            await CheckAndCreateAnulowanieColumnsAsync();
            await CheckAndCreateWalutaColumnAsync();

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

        private async Task CheckAndCreateWalutaColumnAsync()
        {
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                const string checkSql = @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                                         WHERE TABLE_NAME = 'ZamowieniaMieso' AND COLUMN_NAME = 'Waluta'";

                await using var cmdCheck = new SqlCommand(checkSql, cn);
                int count = Convert.ToInt32(await cmdCheck.ExecuteScalarAsync());

                if (count == 0)
                {
                    const string alterSql = @"ALTER TABLE [dbo].[ZamowieniaMieso] ADD Waluta NVARCHAR(10) NULL DEFAULT 'PLN'";
                    await using var cmdAlter = new SqlCommand(alterSql, cn);
                    await cmdAlter.ExecuteNonQueryAsync();

                    _walutaColumnExists = true;
                }
                else
                {
                    _walutaColumnExists = true;
                }
            }
            catch (Exception ex)
            {
                _walutaColumnExists = false;
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

        private async Task CheckAndCreateAnulowanieColumnsAsync()
        {
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                var columns = new[]
                {
                    ("AnulowanePrzez", "NVARCHAR(100) NULL"),
                    ("DataAnulowania", "DATETIME NULL")
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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd tworzenia kolumn anulowania: {ex.Message}");
            }
        }

        #endregion

        #region Navigation Events

        private async void DayButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && _dayButtonDates.TryGetValue(btn, out DateTime date))
            {
                _selectedDate = date;
                UpdateDayButtonDates();
                await RefreshAllDataAsync();
            }
        }

        private async void BtnPreviousWeek_Click(object sender, RoutedEventArgs e)
        {
            _selectedDate = _selectedDate.AddDays(-7);
            int delta = ((int)_selectedDate.DayOfWeek + 6) % 7;
            _selectedDate = _selectedDate.AddDays(-delta);
            UpdateDayButtonDates();
            await RefreshAllDataAsync();
        }

        private async void BtnNextWeek_Click(object sender, RoutedEventArgs e)
        {
            _selectedDate = _selectedDate.AddDays(7);
            int delta = ((int)_selectedDate.DayOfWeek + 6) % 7;
            _selectedDate = _selectedDate.AddDays(-delta);
            UpdateDayButtonDates();
            await RefreshAllDataAsync();
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
            // PERFORMANCE: Debouncing - nie odpytuj SQL przy każdym klawiszu
            _filterDebounceTimer?.Stop();
            _filterDebounceTimer?.Start();
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

            // Otwórz dialog wyboru przyczyny anulowania
            var dialog = new PrzyczynaAnulowaniaDialog(odbiorca, ilosc);
            dialog.Owner = this;

            if (dialog.ShowDialog() == true && dialog.CzyAnulowano)
            {
                try
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();
                    await using var cmd = new SqlCommand(
                        @"UPDATE dbo.ZamowieniaMieso
                          SET Status = 'Anulowane',
                              AnulowanePrzez = @AnulowanePrzez,
                              DataAnulowania = @DataAnulowania,
                              PrzyczynaAnulowania = @PrzyczynaAnulowania
                          WHERE Id = @Id", cn);
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.Parameters.AddWithValue("@AnulowanePrzez", App.UserFullName ?? UserID ?? "Nieznany");
                    cmd.Parameters.AddWithValue("@DataAnulowania", DateTime.Now);
                    cmd.Parameters.AddWithValue("@PrzyczynaAnulowania", dialog.WybranaPrzyczyna ?? "Nieznana");
                    await cmd.ExecuteNonQueryAsync();

                    // Logowanie historii zmian z przyczyna
                    await HistoriaZmianService.LogujAnulowanie(id, UserID, App.UserFullName,
                        $"Anulowano zamówienie dla odbiorcy: {odbiorca}, ilość: {ilosc:N0} kg. Przyczyna: {dialog.WybranaPrzyczyna}");

                    MessageBox.Show($"Zamówienie zostało anulowane.\n\nPrzyczyna: {dialog.WybranaPrzyczyna}", "Sukces",
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
                        @"UPDATE dbo.ZamowieniaMieso
                          SET Status = 'Nowe',
                              AnulowanePrzez = NULL,
                              DataAnulowania = NULL
                          WHERE Id = @Id", cn);
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

        // Flaga do włączania/wyłączania diagnostyki czasów ładowania
        private bool _showLoadingDiagnostics = true;

        private async Task RefreshAllDataAsync()
        {
            // Zapobiegaj równoległym wywołaniom
            if (_isRefreshing) return;
            _isRefreshing = true;

            // Status bar - start
            UpdateStatus("Ładowanie danych...");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var timings = new System.Text.StringBuilder();
            var stepSw = new System.Diagnostics.Stopwatch();

            try
            {
                // Resetuj flagi lazy loading przy zmianie dnia
                _transportLoaded = false;
                _historiaLoaded = false;
                _dashboardLoaded = false;
                _lastLoadedDate = _selectedDate;

                // 1. Konfiguracja produktów
                stepSw.Restart();
                await GetKonfiguracjaProduktowAsync(_selectedDate);
                stepSw.Stop();
                timings.AppendLine($"1. Konfiguracja produktów: {stepSw.ElapsedMilliseconds} ms");

                // 2. Ładowanie zamówień
                stepSw.Restart();
                await LoadOrdersForDayAsync(_selectedDate);
                stepSw.Stop();
                timings.AppendLine($"2. Ładowanie zamówień: {stepSw.ElapsedMilliseconds} ms");

                // 3. Podsumowanie produktów
                stepSw.Restart();
                await DisplayProductAggregationAsync(_selectedDate);
                stepSw.Stop();
                timings.AppendLine($"3. Podsumowanie produktów: {stepSw.ElapsedMilliseconds} ms");

                // NIE ładuj Transport, Historia, Dashboard w tle - lazy loading
                // Te dane będą załadowane dopiero gdy użytkownik kliknie odpowiednią zakładkę

                // Jeśli aktualnie wybrana jest zakładka Transport/Historia/Dashboard - załaduj dane
                var selectedTab = tabOrders.SelectedItem as TabItem;
                if (selectedTab?.Header?.ToString()?.Contains("Transport") == true)
                {
                    stepSw.Restart();
                    await LoadTransportForDayAsync(_selectedDate);
                    stepSw.Stop();
                    timings.AppendLine($"4. Transport: {stepSw.ElapsedMilliseconds} ms");
                    _transportLoaded = true;
                }
                else if (selectedTab?.Header?.ToString()?.Contains("Historia") == true)
                {
                    stepSw.Restart();
                    int delta = ((int)_selectedDate.DayOfWeek + 6) % 7;
                    DateTime startOfWeek = _selectedDate.AddDays(-delta);
                    DateTime endOfWeek = startOfWeek.AddDays(6);
                    await LoadHistoriaZmianAsync(startOfWeek, endOfWeek);
                    stepSw.Stop();
                    timings.AppendLine($"4. Historia zmian: {stepSw.ElapsedMilliseconds} ms");
                    _historiaLoaded = true;
                }
                else if (selectedTab == tabDashboard)
                {
                    stepSw.Restart();
                    await LoadDashboardDataAsync(_selectedDate);
                    stepSw.Stop();
                    timings.AppendLine($"4. Dashboard: {stepSw.ElapsedMilliseconds} ms");
                    _dashboardLoaded = true;
                }

                // 5. Szczegóły zamówienia
                if (_currentOrderId.HasValue && _currentOrderId.Value > 0)
                {
                    stepSw.Restart();
                    await DisplayOrderDetailsAsync(_currentOrderId.Value);
                    stepSw.Stop();
                    timings.AppendLine($"5. Szczegóły zamówienia: {stepSw.ElapsedMilliseconds} ms");
                }
                else
                {
                    ClearDetails();
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Błąd: {ex.Message}");
                MessageBox.Show($"Błąd podczas odświeżania danych: {ex.Message}\n\nSTACKTRACE:\n{ex.StackTrace}",
                    "Błąd Krytyczny", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isRefreshing = false;
                sw.Stop();

                // Status bar - koniec
                UpdateStatus($"Gotowy - załadowano w {sw.ElapsedMilliseconds}ms");
                txtStatusTime.Text = $"Ostatnia aktualizacja: {DateTime.Now:HH:mm:ss}";

                // Pokaż diagnostykę czasów ładowania
                if (_showLoadingDiagnostics)
                {
                    var details = new System.Text.StringBuilder();
                    details.AppendLine("═══ ŁADOWANIE ZAMÓWIEŃ ═══");
                    foreach (var t in _lastLoadOrdersDiag)
                        details.AppendLine($"  {t.name}: {t.ms} ms");
                    details.AppendLine($"  SUMA: {_lastLoadOrdersDiag.Sum(x => x.ms)} ms");

                    details.AppendLine("\n═══ PODSUMOWANIE PRODUKTÓW ═══");
                    foreach (var t in _lastAggregationDiag)
                        details.AppendLine($"  {t.name}: {t.ms} ms");
                    details.AppendLine($"  SUMA: {_lastAggregationDiag.Sum(x => x.ms)} ms");

                    details.AppendLine($"\n══════════════════════");
                    details.AppendLine($"ŁĄCZNIE: {sw.ElapsedMilliseconds} ms");

                    MessageBox.Show(details.ToString(),
                        $"Diagnostyka ładowania - {_selectedDate:dd.MM.yyyy}",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    // Wyłącz po pierwszym pokazaniu (aby nie irytować)
                    _showLoadingDiagnostics = false;
                }
            }
        }

        private void UpdateStatus(string message)
        {
            if (txtStatus != null)
                txtStatus.Text = message;
        }

        private async Task LoadOrdersForDayAsync(DateTime day)
        {
            var diagSw = System.Diagnostics.Stopwatch.StartNew();
            var diagTimes = new List<(string name, long ms)>();

            day = ValidateSqlDate(day);

            // Wyczyść RowFilter przed modyfikacją kolumn, aby uniknąć NullReferenceException
            if (_dtOrders.DefaultView != null)
            {
                _dtOrders.DefaultView.RowFilter = "";
            }

            // Zawsze czyść i odtwarzaj kolumny - grupy mogą się zmienić
            _dtOrders.Clear();
            _dtOrders.Columns.Clear();

            // Podstawowe kolumny
            _dtOrders.Columns.Add("Id", typeof(int));
            _dtOrders.Columns.Add("KlientId", typeof(int));
            _dtOrders.Columns.Add("Odbiorca", typeof(string));
            _dtOrders.Columns.Add("Handlowiec", typeof(string));
            _dtOrders.Columns.Add("IloscZamowiona", typeof(decimal));
            _dtOrders.Columns.Add("IloscFaktyczna", typeof(decimal));
            _dtOrders.Columns.Add("Roznica", typeof(decimal));
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
            _dtOrders.Columns.Add("Trans", typeof(string));
            _dtOrders.Columns.Add("Prod", typeof(string));
            _dtOrders.Columns.Add("CzyMaCeny", typeof(bool));
            _dtOrders.Columns.Add("CenaInfo", typeof(string));
            _dtOrders.Columns.Add("TerminInfo", typeof(string));
            _dtOrders.Columns.Add("TransportInfo", typeof(string));
            _dtOrders.Columns.Add("CzyZrealizowane", typeof(bool));
            _dtOrders.Columns.Add("WyprInfo", typeof(string));
            _dtOrders.Columns.Add("WydanoInfo", typeof(string));

            // Dynamiczne kolumny dla grup towarowych (z sanityzowanymi nazwami)
            _grupyKolumnDoNazw.Clear();
            foreach (var grupaName in _grupyTowaroweNazwy)
            {
                string colName = SanitizeColumnName(grupaName);
                _grupyKolumnDoNazw[colName] = grupaName;
                _dtOrders.Columns.Add(colName, typeof(decimal));
            }

            diagSw.Restart();
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
            diagTimes.Add(("Kontrahenci", diagSw.ElapsedMilliseconds));

            int? selectedProductId = _selectedProductId;

            diagSw.Restart();
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
            diagTimes.Add(("KlienciZam", diagSw.ElapsedMilliseconds));

            diagSw.Restart();
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
       CAST(CASE WHEN EXISTS(SELECT 1 FROM [dbo].[ZamowieniaMiesoTowar] WHERE ZamowienieId = zm.Id AND Folia = 1)
            THEN 1 ELSE 0 END AS BIT) AS MaFolie,
       CAST(CASE WHEN EXISTS(SELECT 1 FROM [dbo].[ZamowieniaMiesoTowar] WHERE ZamowienieId = zm.Id AND Hallal = 1)
            THEN 1 ELSE 0 END AS BIT) AS MaHallal,
       CAST(CASE WHEN NOT EXISTS(SELECT 1 FROM [dbo].[ZamowieniaMiesoTowar] WHERE ZamowienieId = zm.Id AND (Cena IS NULL OR Cena = '' OR Cena = '0'))
            AND EXISTS(SELECT 1 FROM [dbo].[ZamowieniaMiesoTowar] WHERE ZamowienieId = zm.Id)
            THEN 1 ELSE 0 END AS BIT) AS CzyMaCeny,
       ISNULL(zm.CzyZrealizowane, 0) AS CzyZrealizowane,
       zm.DataWydania{slaughterDateSelect}
FROM [dbo].[ZamowieniaMieso] zm
LEFT JOIN [dbo].[ZamowieniaMiesoTowar] zmt ON zm.Id = zmt.ZamowienieId
WHERE {dateColumn} = @Day " +
                                (selectedProductId.HasValue ? "AND (zmt.KodTowaru = @ProductId OR zmt.KodTowaru IS NULL) " : "") +
                                $@"GROUP BY zm.Id, zm.KlientId, zm.DataPrzyjazdu, zm.DataUtworzenia, zm.IdUser, zm.Status,
     zm.LiczbaPojemnikow, zm.LiczbaPalet, zm.TrybE2, zm.Uwagi, zm.TransportKursID, zm.CzyZrealizowane, zm.DataWydania{slaughterDateGroupBy}
ORDER BY zm.Id";

                    await using var cmd = new SqlCommand(sql, cnLibra);
                    cmd.Parameters.AddWithValue("@Day", day);
                    if (selectedProductId.HasValue)
                        cmd.Parameters.AddWithValue("@ProductId", selectedProductId.Value);

                    using var da = new SqlDataAdapter(cmd);
                    da.Fill(temp);
                }
            }
            diagTimes.Add(("SQL Zamówienia", diagSw.ElapsedMilliseconds));

            diagSw.Restart();
            // Load transport departure times from dbo.Kurs
            var transportTimes = new Dictionary<long, (TimeSpan? GodzWyjazdu, DateTime? DataKursu)>();
            var transportKursIds = new List<long>();
            foreach (DataRow r in temp.Rows)
            {
                if (temp.Columns.Contains("TransportKursID") && !(r["TransportKursID"] is DBNull))
                {
                    long kursId = Convert.ToInt64(r["TransportKursID"]);
                    if (!transportKursIds.Contains(kursId))
                        transportKursIds.Add(kursId);
                }
            }

            if (transportKursIds.Any())
            {
                try
                {
                    await using var cnTransport = new SqlConnection(_connTransport);
                    await cnTransport.OpenAsync();
                    var kursIdsList = string.Join(",", transportKursIds);
                    var sqlKurs = $"SELECT KursID, GodzWyjazdu, DataKursu FROM dbo.Kurs WHERE KursID IN ({kursIdsList})";
                    await using var cmdKurs = new SqlCommand(sqlKurs, cnTransport);
                    await using var rdKurs = await cmdKurs.ExecuteReaderAsync();
                    while (await rdKurs.ReadAsync())
                    {
                        long kursId = rdKurs.GetInt64(0);
                        TimeSpan? godzWyjazdu = rdKurs.IsDBNull(1) ? null : rdKurs.GetTimeSpan(1);
                        DateTime? dataKursu = rdKurs.IsDBNull(2) ? null : rdKurs.GetDateTime(2);
                        transportTimes[kursId] = (godzWyjazdu, dataKursu);
                    }
                }
                catch { /* Ignore transport errors - show just checkmark */ }
            }
            diagTimes.Add(("TransportKurs", diagSw.ElapsedMilliseconds));

            diagSw.Restart();
            var releasesPerClientProduct = await GetReleasesPerClientProductAsync(day);
            diagTimes.Add(("Wydania", diagSw.ElapsedMilliseconds));

            diagSw.Restart();
            var transportInfo = await GetTransportInfoAsync(day);
            diagTimes.Add(("TransportInfo", diagSw.ElapsedMilliseconds));
            var cultureInfo = new CultureInfo("pl-PL");

            // Polskie skróty miesięcy dla formatu "Sty 12 (Ania)"
            string[] polskieMiesiaceSkrot = { "", "Sty", "Lut", "Mar", "Kwi", "Maj", "Cze", "Lip", "Sie", "Wrz", "Paź", "Lis", "Gru" };

            diagSw.Restart();
            // Pobierz sumy per zamówienie per grupa towarowa
            var sumaPerZamowieniePerGrupa = new Dictionary<int, Dictionary<string, decimal>>();
            var anulowaneZamowieniaIds = new HashSet<int>(); // Śledzenie anulowanych zamówień
            if (_grupyTowaroweNazwy.Any() && temp.Rows.Count > 0)
            {
                // Zbierz ID anulowanych zamówień
                foreach (DataRow r in temp.Rows)
                {
                    int id = Convert.ToInt32(r["Id"]);
                    string status = r["Status"]?.ToString() ?? "";
                    if (id > 0 && string.Equals(status, "Anulowane", StringComparison.OrdinalIgnoreCase))
                    {
                        anulowaneZamowieniaIds.Add(id);
                    }
                }

                var zamowieniaIds = temp.AsEnumerable().Select(r => Convert.ToInt32(r["Id"])).Where(id => id > 0).ToList();
                if (zamowieniaIds.Any())
                {
                    await using var cnLibraGrupy = new SqlConnection(_connLibra);
                    await cnLibraGrupy.OpenAsync();
                    var sqlGrupy = $"SELECT ZamowienieId, KodTowaru, SUM(Ilosc) AS Ilosc FROM [dbo].[ZamowieniaMiesoTowar] WHERE ZamowienieId IN ({string.Join(",", zamowieniaIds)}) GROUP BY ZamowienieId, KodTowaru";
                    await using var cmdGrupy = new SqlCommand(sqlGrupy, cnLibraGrupy);
                    await using var readerGrupy = await cmdGrupy.ExecuteReaderAsync();
                    while (await readerGrupy.ReadAsync())
                    {
                        int zamId = readerGrupy.GetInt32(0);
                        int kodTowaru = readerGrupy.GetInt32(1);
                        decimal iloscTowaru = readerGrupy.IsDBNull(2) ? 0m : Convert.ToDecimal(readerGrupy.GetValue(2));

                        // Znajdź grupę dla tego towaru
                        if (_mapowanieScalowania.TryGetValue(kodTowaru, out var nazwaGrupy))
                        {
                            if (!sumaPerZamowieniePerGrupa.ContainsKey(zamId))
                                sumaPerZamowieniePerGrupa[zamId] = new Dictionary<string, decimal>();

                            if (!sumaPerZamowieniePerGrupa[zamId].ContainsKey(nazwaGrupy))
                                sumaPerZamowieniePerGrupa[zamId][nazwaGrupy] = 0m;

                            sumaPerZamowieniePerGrupa[zamId][nazwaGrupy] += iloscTowaru;
                        }
                    }
                }
            }
            diagTimes.Add(("GrupyTowarowe", diagSw.ElapsedMilliseconds));

            diagSw.Restart();
            decimal totalOrdered = 0m;
            decimal totalReleased = 0m;
            decimal totalPallets = 0m;
            int totalOrdersCount = 0;
            int actualOrdersCount = 0;
            var clientsReleasedCounted = new HashSet<int>(); // Śledzenie klientów, których wydania już policzono

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

                // Notatka nie jest już pokazywana jako ikona - usunięto zgodnie z wymaganiami
                // if (hasNote)
                // {
                //     name = "📝 " + name;
                // }

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

                // Format: "Sty 12 (Ania)" - skrócony miesiąc, dzień i imię
                string createdBy = "";
                string userName = _userCache.TryGetValue(userId, out var user) ? user : "Brak";
                string imie = userName.Contains(" ") ? userName.Split(' ')[0] : userName;
                if (createdDate.HasValue)
                {
                    string miesiacSkrot = polskieMiesiaceSkrot[createdDate.Value.Month];
                    createdBy = $"{miesiacSkrot} {createdDate.Value.Day} ({imie})";
                }
                else
                {
                    createdBy = imie;
                }

                string transColumn = transportKursId.HasValue ? "✓" : "";
                string prodColumn = status == "Zrealizowane" ? "✓" : "";

                // TerminInfo - godzina + skrócony dzień tygodnia odbioru
                string terminInfo = "";
                if (arrivalDate.HasValue)
                {
                    string dzienSkrot = arrivalDate.Value.ToString("ddd", cultureInfo);
                    terminInfo = $"{arrivalDate.Value:HH:mm} {dzienSkrot}";
                }

                // TransportInfo - godzina:minuta + skrócony dzień wyjazdu z firmy
                string transportInfoStr = "";
                if (transportKursId.HasValue && transportTimes.TryGetValue(transportKursId.Value, out var kursInfo))
                {
                    if (kursInfo.GodzWyjazdu.HasValue)
                    {
                        string dzienKursu = kursInfo.DataKursu.HasValue ? kursInfo.DataKursu.Value.ToString("ddd", cultureInfo) : "";
                        transportInfoStr = $"{kursInfo.GodzWyjazdu.Value:hh\\:mm} {dzienKursu}";
                    }
                    else if (kursInfo.DataKursu.HasValue)
                    {
                        // Jeśli nie ma godziny wyjazdu, pokaż tylko dzień
                        string dzienKursu = kursInfo.DataKursu.Value.ToString("ddd", cultureInfo);
                        transportInfoStr = dzienKursu;
                    }
                }

                // WyprInfo - czy wyprodukowane
                string wyprInfo = czyZrealizowane || status == "Zrealizowane" ? "✓" : "✗";

                // WydanoInfo - godzina:minuta + skrócony dzień wydania
                DateTime? dataWydania = null;
                if (temp.Columns.Contains("DataWydania") && !(r["DataWydania"] is DBNull))
                {
                    dataWydania = Convert.ToDateTime(r["DataWydania"]);
                }
                string wydanoInfo = "";
                if (dataWydania.HasValue)
                {
                    string dzienWyd = dataWydania.Value.ToString("ddd", cultureInfo);
                    wydanoInfo = $"{dataWydania.Value:HH:mm} {dzienWyd}";
                }

                totalOrdersCount++;
                if (status != "Anulowane")
                {
                    totalOrdered += quantity;
                    // Wydania liczone raz na klienta, nie na każde zamówienie
                    if (!clientsReleasedCounted.Contains(clientId))
                    {
                        totalReleased += released;
                        clientsReleasedCounted.Add(clientId);
                    }
                    totalPallets += pallets;
                    actualOrdersCount++;
                }

                // Różnica = Zamówiono - Wydano
                decimal roznica = quantity - released;

                // Tworzenie wiersza z dynamicznymi kolumnami grup
                var newRow = _dtOrders.NewRow();
                newRow["Id"] = id;
                newRow["KlientId"] = clientId;
                newRow["Odbiorca"] = name;
                newRow["Handlowiec"] = salesman;
                newRow["IloscZamowiona"] = quantity;
                newRow["IloscFaktyczna"] = released;
                newRow["Roznica"] = roznica;
                newRow["Pojemniki"] = containers;
                newRow["Palety"] = pallets;
                newRow["TrybE2"] = modeText;
                newRow["DataPrzyjecia"] = arrivalDate?.Date ?? day;
                newRow["GodzinaPrzyjecia"] = arrivalDate?.ToString("HH:mm") ?? "08:00";
                newRow["TerminOdbioru"] = pickupTerm;
                newRow["DataUboju"] = slaughterDate.HasValue ? (object)slaughterDate.Value.Date : DBNull.Value;
                newRow["UtworzonePrzez"] = createdBy;
                newRow["Status"] = status;
                newRow["MaNotatke"] = hasNote;
                newRow["MaFolie"] = hasFoil;
                newRow["MaHallal"] = hasHallal;
                newRow["Trans"] = transColumn;
                newRow["Prod"] = prodColumn;
                newRow["CzyMaCeny"] = czyMaCeny;
                newRow["CenaInfo"] = cenaInfo;
                newRow["TerminInfo"] = terminInfo;
                newRow["TransportInfo"] = transportInfoStr;
                newRow["CzyZrealizowane"] = czyZrealizowane;
                newRow["WyprInfo"] = wyprInfo;
                newRow["WydanoInfo"] = wydanoInfo;

                // Wypełnij kolumny grup towarowych
                foreach (var grupaName in _grupyTowaroweNazwy)
                {
                    string colName = SanitizeColumnName(grupaName);
                    decimal sumaGrupy = 0m;
                    if (sumaPerZamowieniePerGrupa.TryGetValue(id, out var grupyDict) &&
                        grupyDict.TryGetValue(grupaName, out var suma))
                    {
                        sumaGrupy = suma;
                    }
                    newRow[colName] = sumaGrupy;
                }

                _dtOrders.Rows.Add(newRow);
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
                row["Roznica"] = -released; // 0 - released
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
                row["Trans"] = "";
                row["Prod"] = "";
                row["CzyMaCeny"] = false;
                row["CenaInfo"] = "";
                row["TerminInfo"] = "";
                row["TransportInfo"] = "";
                row["CzyZrealizowane"] = false;
                row["WyprInfo"] = "";
                row["WydanoInfo"] = "";

                // Kolumny grup dla wydań bez zamówień = 0
                foreach (var grupaName in _grupyTowaroweNazwy)
                {
                    string colName = SanitizeColumnName(grupaName);
                    row[colName] = 0m;
                }

                releasesWithoutOrders.Add(row);

                // Wydania bez zamówień - każdy klient tutaj jest unikalny (już sprawdzono clientsWithOrders)
                if (!clientsReleasedCounted.Contains(clientId))
                {
                    totalReleased += released;
                    clientsReleasedCounted.Add(clientId);
                }
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
                summaryRow["Roznica"] = totalOrdered - totalReleased;
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
                summaryRow["Trans"] = "";
                summaryRow["Prod"] = "";
                summaryRow["CzyMaCeny"] = false;
                summaryRow["CenaInfo"] = "";
                summaryRow["TerminInfo"] = "";
                summaryRow["TransportInfo"] = "";
                summaryRow["CzyZrealizowane"] = false;
                summaryRow["WyprInfo"] = "";
                summaryRow["WydanoInfo"] = "";

                // Sumy kolumn grup dla wiersza podsumowania (bez anulowanych zamówień)
                foreach (var grupaName in _grupyTowaroweNazwy)
                {
                    string colName = SanitizeColumnName(grupaName);
                    decimal sumaGrupy = 0m;
                    foreach (var kvp in sumaPerZamowieniePerGrupa)
                    {
                        // Pomiń anulowane zamówienia
                        if (anulowaneZamowieniaIds.Contains(kvp.Key))
                            continue;

                        if (kvp.Value.TryGetValue(grupaName, out var val))
                            sumaGrupy += val;
                    }
                    summaryRow[colName] = sumaGrupy;
                }

                _dtOrders.Rows.InsertAt(summaryRow, 0);
            }
            diagTimes.Add(("Przetwarzanie", diagSw.ElapsedMilliseconds));

            // Zapisz diagnostykę do zmiennej klasowej
            _lastLoadOrdersDiag = diagTimes;

            SetupOrdersDataGrid();
            ApplyFilters();
        }

        private async Task LoadTransportForDayAsync(DateTime day)
        {
            day = ValidateSqlDate(day);

            _dtTransport.Rows.Clear();

            if (_dtTransport.Columns.Count == 0)
            {
                _dtTransport.Columns.Add("Id", typeof(int));
                _dtTransport.Columns.Add("KlientId", typeof(int));
                _dtTransport.Columns.Add("Odbiorca", typeof(string));
                _dtTransport.Columns.Add("Handlowiec", typeof(string));
                _dtTransport.Columns.Add("IloscZamowiona", typeof(decimal));
                _dtTransport.Columns.Add("IloscWydana", typeof(decimal));
                _dtTransport.Columns.Add("Palety", typeof(decimal));
                _dtTransport.Columns.Add("Kierowca", typeof(string));
                _dtTransport.Columns.Add("Pojazd", typeof(string));
                _dtTransport.Columns.Add("GodzWyjazdu", typeof(string));
                _dtTransport.Columns.Add("Trasa", typeof(string));
                _dtTransport.Columns.Add("Status", typeof(string));
                _dtTransport.Columns.Add("Uwagi", typeof(string));
            }

            // Pobierz pełne dane transportu z bazy
            var transportDetails = new Dictionary<long, (string Kierowca, string Pojazd, string Trasa, TimeSpan? GodzWyjazdu)>();

            // Pobierz listę KursID z zamówień
            var kursIds = new HashSet<long>();
            await using (var cnLibra = new SqlConnection(_connLibra))
            {
                await cnLibra.OpenAsync();
                string dateColumn = (_showBySlaughterDate && _slaughterDateColumnExists) ? "DataUboju" : "DataZamowienia";
                string sql = $"SELECT DISTINCT TransportKursID FROM dbo.ZamowieniaMieso WHERE {dateColumn} = @Day AND TransportKursID IS NOT NULL";
                await using var cmd = new SqlCommand(sql, cnLibra);
                cmd.Parameters.AddWithValue("@Day", day);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    kursIds.Add(rd.GetInt64(0));
                }
            }

            // Pobierz szczegóły transportu
            if (kursIds.Any())
            {
                try
                {
                    await using var cnTransport = new SqlConnection(_connTransport);
                    await cnTransport.OpenAsync();
                    var kursIdsList = string.Join(",", kursIds);
                    var sqlKurs = $@"SELECT k.KursID, k.Trasa, k.GodzWyjazdu,
                                    CONCAT(ki.Imie, ' ', ki.Nazwisko) as Kierowca,
                                    p.Rejestracja
                                    FROM dbo.Kurs k
                                    LEFT JOIN dbo.Kierowca ki ON k.KierowcaID = ki.KierowcaID
                                    LEFT JOIN dbo.Pojazd p ON k.PojazdID = p.PojazdID
                                    WHERE k.KursID IN ({kursIdsList})";
                    await using var cmdKurs = new SqlCommand(sqlKurs, cnTransport);
                    await using var rdKurs = await cmdKurs.ExecuteReaderAsync();
                    while (await rdKurs.ReadAsync())
                    {
                        long kursId = rdKurs.GetInt64(0);
                        string trasa = rdKurs.IsDBNull(1) ? "" : rdKurs.GetString(1);
                        TimeSpan? godzWyjazdu = rdKurs.IsDBNull(2) ? null : rdKurs.GetTimeSpan(2);
                        string kierowca = rdKurs.IsDBNull(3) ? "" : rdKurs.GetString(3);
                        string pojazd = rdKurs.IsDBNull(4) ? "" : rdKurs.GetString(4);
                        transportDetails[kursId] = (kierowca, pojazd, trasa, godzWyjazdu);
                    }
                }
                catch { /* Ignoruj błędy transportu */ }
            }

            // Pobierz uwagi i TransportKursID dla każdego zamówienia
            var orderNotes = new Dictionary<int, (string Uwagi, long? KursId)>();
            await using (var cnLibra = new SqlConnection(_connLibra))
            {
                await cnLibra.OpenAsync();
                string dateColumn = (_showBySlaughterDate && _slaughterDateColumnExists) ? "DataUboju" : "DataZamowienia";
                string sql = $"SELECT Id, Uwagi, TransportKursID FROM dbo.ZamowieniaMieso WHERE {dateColumn} = @Day AND TransportKursID IS NOT NULL";
                await using var cmd = new SqlCommand(sql, cnLibra);
                cmd.Parameters.AddWithValue("@Day", day);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    int id = rd.GetInt32(0);
                    string uwagi = rd.IsDBNull(1) ? "" : rd.GetString(1);
                    long? kursId = rd.IsDBNull(2) ? null : rd.GetInt64(2);
                    orderNotes[id] = (uwagi, kursId);
                }
            }

            // Pobierz wszystkie uwagi i transport dla wszystkich zamówień (nie tylko z TransportKursID)
            var allOrderNotes = new Dictionary<int, (string Uwagi, long? KursId)>();
            await using (var cnLibra = new SqlConnection(_connLibra))
            {
                await cnLibra.OpenAsync();
                string dateColumn = (_showBySlaughterDate && _slaughterDateColumnExists) ? "DataUboju" : "DataZamowienia";
                string sql = $"SELECT Id, Uwagi, TransportKursID FROM dbo.ZamowieniaMieso WHERE {dateColumn} = @Day";
                await using var cmd = new SqlCommand(sql, cnLibra);
                cmd.Parameters.AddWithValue("@Day", day);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    int id = rd.GetInt32(0);
                    string uwagi = rd.IsDBNull(1) ? "" : rd.GetString(1);
                    long? kursId = rd.IsDBNull(2) ? null : rd.GetInt64(2);
                    allOrderNotes[id] = (uwagi, kursId);
                }
            }

            foreach (DataRow row in _dtOrders.Rows)
            {
                int id = Convert.ToInt32(row["Id"]);
                if (id == -1) continue; // Pomiń wiersz SUMA

                string status = row["Status"]?.ToString() ?? "";
                if (status == "Anulowane") continue; // Pomiń anulowane

                int clientId = Convert.ToInt32(row["KlientId"]);
                string odbiorca = row["Odbiorca"]?.ToString() ?? "";
                string handlowiec = row["Handlowiec"]?.ToString() ?? "";
                decimal iloscZam = row["IloscZamowiona"] is DBNull ? 0m : Convert.ToDecimal(row["IloscZamowiona"]);
                decimal iloscWyd = row["IloscFaktyczna"] is DBNull ? 0m : Convert.ToDecimal(row["IloscFaktyczna"]);
                decimal palety = row["Palety"] is DBNull ? 0m : Convert.ToDecimal(row["Palety"]);
                string trans = row["Trans"]?.ToString() ?? "";

                string kierowca = "";
                string pojazd = "";
                string trasa = "";
                string godzWyjazdu = "";
                string uwagi = "";
                string statusTransportu = trans == "✓" ? "Przypisany" : "Brak";

                if (allOrderNotes.TryGetValue(id, out var noteInfo))
                {
                    uwagi = noteInfo.Uwagi;
                    if (noteInfo.KursId.HasValue && transportDetails.TryGetValue(noteInfo.KursId.Value, out var td))
                    {
                        kierowca = td.Kierowca;
                        pojazd = td.Pojazd;
                        trasa = td.Trasa;
                        godzWyjazdu = td.GodzWyjazdu?.ToString(@"hh\:mm") ?? "";
                        statusTransportu = "Przypisany";
                    }
                }

                _dtTransport.Rows.Add(id, clientId, odbiorca, handlowiec, iloscZam, iloscWyd, palety, kierowca, pojazd, godzWyjazdu, trasa, statusTransportu, uwagi);
            }

            SetupTransportDataGrid();
        }

        private void SetupTransportDataGrid()
        {
            dgTransport.ItemsSource = _dtTransport.DefaultView;
            dgTransport.Columns.Clear();

            dgTransport.LoadingRow -= DgTransport_LoadingRow;
            dgTransport.LoadingRow += DgTransport_LoadingRow;

            // 1. Odbiorca
            dgTransport.Columns.Add(new DataGridTextColumn
            {
                Header = "Odbiorca",
                Binding = new System.Windows.Data.Binding("Odbiorca"),
                Width = new DataGridLength(150)
            });

            // 2. Handlowiec
            dgTransport.Columns.Add(new DataGridTextColumn
            {
                Header = "Hand",
                Binding = new System.Windows.Data.Binding("Handlowiec"),
                Width = new DataGridLength(50),
                ElementStyle = (Style)FindResource("BoldCellStyle")
            });

            // 3. Zamówiono
            dgTransport.Columns.Add(new DataGridTextColumn
            {
                Header = "Zam.",
                Binding = new System.Windows.Data.Binding("IloscZamowiona") { StringFormat = "N0" },
                Width = new DataGridLength(55),
                ElementStyle = (Style)FindResource("RightAlignedCellStyle")
            });

            // 4. Wydano
            dgTransport.Columns.Add(new DataGridTextColumn
            {
                Header = "Wyd.",
                Binding = new System.Windows.Data.Binding("IloscWydana") { StringFormat = "N0" },
                Width = new DataGridLength(55),
                ElementStyle = (Style)FindResource("RightAlignedCellStyle")
            });

            // 5. Kierowca
            dgTransport.Columns.Add(new DataGridTextColumn
            {
                Header = "Kierowca",
                Binding = new System.Windows.Data.Binding("Kierowca"),
                Width = new DataGridLength(150)
            });

            // 6. Pojazd
            dgTransport.Columns.Add(new DataGridTextColumn
            {
                Header = "Pojazd",
                Binding = new System.Windows.Data.Binding("Pojazd"),
                Width = new DataGridLength(85),
                ElementStyle = (Style)FindResource("CenterAlignedCellStyle")
            });

            // 7. Godzina wyjazdu
            dgTransport.Columns.Add(new DataGridTextColumn
            {
                Header = "Godz",
                Binding = new System.Windows.Data.Binding("GodzWyjazdu"),
                Width = new DataGridLength(50),
                ElementStyle = (Style)FindResource("CenterAlignedCellStyle")
            });

            // 8. Trasa
            dgTransport.Columns.Add(new DataGridTextColumn
            {
                Header = "Trasa",
                Binding = new System.Windows.Data.Binding("Trasa"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });

            // 9. Status
            dgTransport.Columns.Add(new DataGridTextColumn
            {
                Header = "Status",
                Binding = new System.Windows.Data.Binding("Status"),
                Width = new DataGridLength(80),
                ElementStyle = (Style)FindResource("CenterAlignedCellStyle")
            });
        }

        private void DgTransport_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                var status = rowView.Row.Field<string>("Status") ?? "";
                var handlowiec = rowView.Row.Field<string>("Handlowiec") ?? "";

                if (status == "Anulowane")
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(255, 230, 230));
                    e.Row.Foreground = new SolidColorBrush(Colors.Gray);
                    e.Row.FontStyle = FontStyles.Italic;
                }
                else if (!string.IsNullOrEmpty(handlowiec))
                {
                    var color = GetColorForSalesman(handlowiec);
                    e.Row.Background = new SolidColorBrush(color);
                }
            }
        }

        private void SetupOrdersDataGrid()
        {
            dgOrders.ItemsSource = _dtOrders.DefaultView;
            dgOrders.Columns.Clear();

            dgOrders.LoadingRow -= DgOrders_LoadingRow;
            dgOrders.LoadingRow += DgOrders_LoadingRow;

            // 1. Odbiorca - poszerzona kolumna
            dgOrders.Columns.Add(new DataGridTextColumn
            {
                Header = "Odbiorca",
                Binding = new System.Windows.Data.Binding("Odbiorca"),
                Width = new DataGridLength(150)
            });

            // 2. Handlowiec
            dgOrders.Columns.Add(new DataGridTextColumn
            {
                Header = "Hand.",
                Binding = new System.Windows.Data.Binding("Handlowiec"),
                Width = new DataGridLength(50),
                ElementStyle = (Style)FindResource("BoldCellStyle")
            });

            // 3. Zamówiono - pogrubione
            var zamowioneStyle = new Style(typeof(TextBlock));
            zamowioneStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            zamowioneStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Bold));

            dgOrders.Columns.Add(new DataGridTextColumn
            {
                Header = "Zam.",
                Binding = new System.Windows.Data.Binding("IloscZamowiona") { StringFormat = "N0" },
                Width = new DataGridLength(55),
                ElementStyle = zamowioneStyle
            });

            // 4. Wydano - pogrubione
            var wydaneStyle = new Style(typeof(TextBlock));
            wydaneStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            wydaneStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Bold));

            dgOrders.Columns.Add(new DataGridTextColumn
            {
                Header = "Wyd.",
                Binding = new System.Windows.Data.Binding("IloscFaktyczna") { StringFormat = "N0" },
                Width = new DataGridLength(55),
                ElementStyle = wydaneStyle
            });

            // 5. +/- (Różnica: Zam - Wyd)
            var roznicaStyle = new Style(typeof(TextBlock));
            roznicaStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            roznicaStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Bold));

            dgOrders.Columns.Add(new DataGridTextColumn
            {
                Header = "+/-",
                Binding = new System.Windows.Data.Binding("Roznica") { StringFormat = "N0" },
                Width = new DataGridLength(50),
                ElementStyle = roznicaStyle
            });

            // 6. Dynamiczne kolumny grup towarowych (ćwiartka, filet, korpus, skrzydło) z obramowaniem
            // Dodajemy specjalny styl z obramowaniem dla kolumn produktowych
            int productColumnStartIndex = dgOrders.Columns.Count;
            foreach (var kvp in _grupyKolumnDoNazw)
            {
                string colName = kvp.Key;
                string displayName = kvp.Value;

                var grupaStyle = new Style(typeof(TextBlock));
                grupaStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
                grupaStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 11.0));
                grupaStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(2, 0, 4, 0)));

                // Styl dla nagłówka z tłem, aby wyróżnić grupę kolumn produktowych
                var headerStyle = new Style(typeof(DataGridColumnHeader));
                headerStyle.Setters.Add(new Setter(DataGridColumnHeader.BackgroundProperty,
                    new SolidColorBrush(Color.FromRgb(39, 174, 96)))); // Zielony kolor dla produktów
                headerStyle.Setters.Add(new Setter(DataGridColumnHeader.ForegroundProperty, Brushes.White));
                headerStyle.Setters.Add(new Setter(DataGridColumnHeader.FontWeightProperty, FontWeights.Bold));
                headerStyle.Setters.Add(new Setter(DataGridColumnHeader.FontSizeProperty, 10.0));
                headerStyle.Setters.Add(new Setter(DataGridColumnHeader.PaddingProperty, new Thickness(4, 4, 4, 4)));
                headerStyle.Setters.Add(new Setter(DataGridColumnHeader.BorderBrushProperty,
                    new SolidColorBrush(Color.FromRgb(46, 204, 113))));
                headerStyle.Setters.Add(new Setter(DataGridColumnHeader.BorderThicknessProperty, new Thickness(1, 0, 1, 0)));

                var col = new DataGridTextColumn
                {
                    Header = displayName,
                    Binding = new System.Windows.Data.Binding(colName) { StringFormat = "N0" },
                    Width = new DataGridLength(45),
                    ElementStyle = grupaStyle,
                    HeaderStyle = headerStyle
                };

                dgOrders.Columns.Add(col);
            }

            // 7. Cena - V (zielony) jeśli wszystkie pozycje mają cenę, X (czerwony) jeśli nie
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

            // 8. Utworzone przez - po kolumnie Cena
            dgOrders.Columns.Add(new DataGridTextColumn
            {
                Header = "Utworzono",
                Binding = new System.Windows.Data.Binding("UtworzonePrzez"),
                Width = new DataGridLength(85)
            });

            // 9. Termin - godzina + skrócony dzień tygodnia odbioru
            dgOrders.Columns.Add(new DataGridTextColumn
            {
                Header = "Termin",
                Binding = new System.Windows.Data.Binding("TerminInfo"),
                Width = new DataGridLength(75),
                ElementStyle = (Style)FindResource("CenterAlignedCellStyle")
            });

            // 8. Transport - godzina:minuta + skrócony dzień wyjazdu z firmy
            dgOrders.Columns.Add(new DataGridTextColumn
            {
                Header = "Transport",
                Binding = new System.Windows.Data.Binding("TransportInfo"),
                Width = new DataGridLength(75),
                ElementStyle = (Style)FindResource("CenterAlignedCellStyle")
            });

            // 9. Wypr. - czy wyprodukowane (zielone V / czerwone X)
            var wyprStyle = new Style(typeof(TextBlock));
            wyprStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            wyprStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Bold));

            var wyprGreenTrigger = new DataTrigger { Binding = new System.Windows.Data.Binding("CzyZrealizowane"), Value = true };
            wyprGreenTrigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0, 150, 0))));

            var wyprRedTrigger = new DataTrigger { Binding = new System.Windows.Data.Binding("CzyZrealizowane"), Value = false };
            wyprRedTrigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(200, 0, 0))));

            wyprStyle.Triggers.Add(wyprGreenTrigger);
            wyprStyle.Triggers.Add(wyprRedTrigger);

            dgOrders.Columns.Add(new DataGridTextColumn
            {
                Header = "Wypr.",
                Binding = new System.Windows.Data.Binding("WyprInfo"),
                Width = new DataGridLength(45),
                ElementStyle = wyprStyle
            });

            // 10. Wydano - godzina:minuta + skrócony dzień wydania
            dgOrders.Columns.Add(new DataGridTextColumn
            {
                Header = "Wydano",
                Binding = new System.Windows.Data.Binding("WydanoInfo"),
                Width = new DataGridLength(75),
                ElementStyle = (Style)FindResource("CenterAlignedCellStyle")
            });
        }

        // Flagi lazy loading - czy dane zostały już załadowane dla aktualnego dnia
        private bool _transportLoaded = false;
        private bool _historiaLoaded = false;
        private bool _dashboardLoaded = false;
        private bool _statystykiLoaded = false;
        private DateTime _lastLoadedDate = DateTime.MinValue;

        private void TabOrders_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source != tabOrders) return;

            var selectedTab = tabOrders.SelectedItem as TabItem;

            // Sprawdź czy data się zmieniła - jeśli tak, zresetuj flagi
            if (_lastLoadedDate != _selectedDate)
            {
                _transportLoaded = false;
                _historiaLoaded = false;
                _dashboardLoaded = false;
                // Nie resetujemy _statystykiLoaded bo to zależy od własnych filtrów
                _lastLoadedDate = _selectedDate;
            }

            if (selectedTab == tabDashboard)
            {
                // Ukryj prawy panel i rozszerz lewy
                _savedRightColumnWidth = rightColumnDef.Width;
                rightPanel.Visibility = Visibility.Collapsed;
                rightColumnDef.Width = new GridLength(0);
                leftColumnDef.Width = new GridLength(1, GridUnitType.Star);

                // Lazy loading - załaduj dane Dashboard tylko gdy zakładka jest aktywna
                if (!_dashboardLoaded)
                {
                    _ = LoadDashboardDataAsync(_selectedDate);
                    _dashboardLoaded = true;
                }
            }
            else if (selectedTab == tabStatystyki)
            {
                // Ukryj prawy panel dla statystyk
                _savedRightColumnWidth = rightColumnDef.Width;
                rightPanel.Visibility = Visibility.Collapsed;
                rightColumnDef.Width = new GridLength(0);
                leftColumnDef.Width = new GridLength(1, GridUnitType.Star);

                // Lazy loading - załaduj statystyki tylko gdy zakładka jest aktywna
                if (!_statystykiLoaded)
                {
                    InitializeStatystykiTab();
                    _ = LoadStatystykiAsync();
                    _statystykiLoaded = true;
                }
            }
            else if (selectedTab?.Header?.ToString()?.Contains("Transport") == true)
            {
                // Przywróć prawy panel
                rightPanel.Visibility = Visibility.Visible;
                rightColumnDef.Width = _savedRightColumnWidth;
                leftColumnDef.Width = new GridLength(1, GridUnitType.Star);

                // Lazy loading - załaduj transport tylko gdy zakładka jest aktywna
                if (!_transportLoaded)
                {
                    _ = LoadTransportForDayAsync(_selectedDate);
                    _transportLoaded = true;
                }
            }
            else if (selectedTab?.Header?.ToString()?.Contains("Historia") == true)
            {
                // Przywróć prawy panel
                rightPanel.Visibility = Visibility.Visible;
                rightColumnDef.Width = _savedRightColumnWidth;
                leftColumnDef.Width = new GridLength(1, GridUnitType.Star);

                // Lazy loading - załaduj historię zmian tylko gdy zakładka jest aktywna
                if (!_historiaLoaded)
                {
                    int delta = ((int)_selectedDate.DayOfWeek + 6) % 7;
                    DateTime startOfWeek = _selectedDate.AddDays(-delta);
                    DateTime endOfWeek = startOfWeek.AddDays(6);
                    _ = LoadHistoriaZmianAsync(startOfWeek, endOfWeek);
                    _historiaLoaded = true;
                }
            }
            else
            {
                // Przywróć prawy panel
                rightPanel.Visibility = Visibility.Visible;
                rightColumnDef.Width = _savedRightColumnWidth;
                leftColumnDef.Width = new GridLength(1, GridUnitType.Star);
            }
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
                    // Subtelniejszy styl wiersza sumy - jasne tło, ciemny tekst
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(240, 245, 240));
                    e.Row.Foreground = new SolidColorBrush(Color.FromRgb(40, 60, 40));
                    e.Row.FontWeight = FontWeights.Bold;
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
            PopulateHistoriaFilterComboBoxes();
        }

        private void PopulateHistoriaFilterComboBoxes()
        {
            // Zapisz aktualne selekcje
            var selectedKto = cmbHistoriaKtoEdytowal?.SelectedItem?.ToString();
            var selectedOdbiorca = cmbHistoriaOdbiorca?.SelectedItem?.ToString();
            var selectedTyp = cmbHistoriaTyp?.SelectedItem?.ToString();
            var selectedHandlowiec = cmbHistoriaHandlowiec?.SelectedItem?.ToString();
            var selectedTowar = cmbHistoriaTowar?.SelectedItem?.ToString();

            // Pobierz unikalne wartości z danych
            var ktoEdytowalList = new List<string> { "(Wszystkie)" };
            var odbiorcaList = new List<string> { "(Wszystkie)" };
            var typList = new List<string> { "(Wszystkie)" };
            var handlowiecList = new List<string> { "(Wszystkie)" };
            var towarList = new List<string> { "(Wszystkie)" };

            foreach (DataRow row in _dtHistoriaZmian.Rows)
            {
                string kto = row["UzytkownikNazwa"]?.ToString() ?? "";
                string odbiorca = row["Odbiorca"]?.ToString() ?? "";
                string typ = row["TypZmiany"]?.ToString() ?? "";
                string handlowiec = row["Handlowiec"]?.ToString() ?? "";
                string opis = row["OpisZmiany"]?.ToString() ?? "";

                if (!string.IsNullOrWhiteSpace(kto) && !ktoEdytowalList.Contains(kto))
                    ktoEdytowalList.Add(kto);
                if (!string.IsNullOrWhiteSpace(odbiorca) && !odbiorcaList.Contains(odbiorca))
                    odbiorcaList.Add(odbiorca);
                if (!string.IsNullOrWhiteSpace(typ) && !typList.Contains(typ))
                    typList.Add(typ);
                if (!string.IsNullOrWhiteSpace(handlowiec) && !handlowiecList.Contains(handlowiec))
                    handlowiecList.Add(handlowiec);

                // Wyciągnij nazwy towarów z opisu zmiany
                if (!string.IsNullOrWhiteSpace(opis))
                {
                    // Szukaj nazw produktów w opisie (np. "Filet: 100 → 150" lub "Dodano: Ćwiartka 50kg")
                    foreach (var grupaName in _grupyTowaroweNazwy)
                    {
                        if (opis.Contains(grupaName) && !towarList.Contains(grupaName))
                        {
                            towarList.Add(grupaName);
                        }
                    }
                    // Dodaj też produkty z katalogu
                    foreach (var kvp in _productCatalogCache)
                    {
                        if (opis.Contains(kvp.Value) && !towarList.Contains(kvp.Value))
                        {
                            towarList.Add(kvp.Value);
                        }
                    }
                }
            }

            // Sortuj listy (bez pierwszego elementu)
            if (ktoEdytowalList.Count > 1)
            {
                var sorted = ktoEdytowalList.Skip(1).OrderBy(x => x).ToList();
                ktoEdytowalList = new List<string> { "(Wszystkie)" };
                ktoEdytowalList.AddRange(sorted);
            }
            if (odbiorcaList.Count > 1)
            {
                var sorted = odbiorcaList.Skip(1).OrderBy(x => x).ToList();
                odbiorcaList = new List<string> { "(Wszystkie)" };
                odbiorcaList.AddRange(sorted);
            }
            if (typList.Count > 1)
            {
                var sorted = typList.Skip(1).OrderBy(x => x).ToList();
                typList = new List<string> { "(Wszystkie)" };
                typList.AddRange(sorted);
            }
            if (handlowiecList.Count > 1)
            {
                var sorted = handlowiecList.Skip(1).OrderBy(x => x).ToList();
                handlowiecList = new List<string> { "(Wszystkie)" };
                handlowiecList.AddRange(sorted);
            }
            if (towarList.Count > 1)
            {
                var sorted = towarList.Skip(1).OrderBy(x => x).ToList();
                towarList = new List<string> { "(Wszystkie)" };
                towarList.AddRange(sorted);
            }

            // Wypełnij ComboBox
            if (cmbHistoriaKtoEdytowal != null)
            {
                cmbHistoriaKtoEdytowal.ItemsSource = ktoEdytowalList;
                cmbHistoriaKtoEdytowal.SelectedIndex = string.IsNullOrEmpty(selectedKto) ? 0 :
                    Math.Max(0, ktoEdytowalList.IndexOf(selectedKto));
            }
            if (cmbHistoriaOdbiorca != null)
            {
                cmbHistoriaOdbiorca.ItemsSource = odbiorcaList;
                cmbHistoriaOdbiorca.SelectedIndex = string.IsNullOrEmpty(selectedOdbiorca) ? 0 :
                    Math.Max(0, odbiorcaList.IndexOf(selectedOdbiorca));
            }
            if (cmbHistoriaTyp != null)
            {
                cmbHistoriaTyp.ItemsSource = typList;
                cmbHistoriaTyp.SelectedIndex = string.IsNullOrEmpty(selectedTyp) ? 0 :
                    Math.Max(0, typList.IndexOf(selectedTyp));
            }
            if (cmbHistoriaHandlowiec != null)
            {
                cmbHistoriaHandlowiec.ItemsSource = handlowiecList;
                cmbHistoriaHandlowiec.SelectedIndex = string.IsNullOrEmpty(selectedHandlowiec) ? 0 :
                    Math.Max(0, handlowiecList.IndexOf(selectedHandlowiec));
            }
            if (cmbHistoriaTowar != null)
            {
                cmbHistoriaTowar.ItemsSource = towarList;
                cmbHistoriaTowar.SelectedIndex = string.IsNullOrEmpty(selectedTowar) ? 0 :
                    Math.Max(0, towarList.IndexOf(selectedTowar));
            }
        }

        private void CmbHistoriaFiltr_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyHistoriaFilters();
        }

        private void BtnHistoriaCzyscFiltry_Click(object sender, RoutedEventArgs e)
        {
            if (cmbHistoriaKtoEdytowal != null) cmbHistoriaKtoEdytowal.SelectedIndex = 0;
            if (cmbHistoriaOdbiorca != null) cmbHistoriaOdbiorca.SelectedIndex = 0;
            if (cmbHistoriaTyp != null) cmbHistoriaTyp.SelectedIndex = 0;
            if (cmbHistoriaHandlowiec != null) cmbHistoriaHandlowiec.SelectedIndex = 0;
            if (cmbHistoriaTowar != null) cmbHistoriaTowar.SelectedIndex = 0;
            ApplyHistoriaFilters();
        }

        private void ApplyHistoriaFilters()
        {
            if (_dtHistoriaZmian == null || dgHistoriaZmian == null) return;

            var view = _dtHistoriaZmian.DefaultView;
            var filters = new List<string>();

            // Filtr: Kto edytował
            string kto = cmbHistoriaKtoEdytowal?.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(kto) && kto != "(Wszystkie)")
            {
                filters.Add($"UzytkownikNazwa = '{kto.Replace("'", "''")}'");
            }

            // Filtr: Odbiorca
            string odbiorca = cmbHistoriaOdbiorca?.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(odbiorca) && odbiorca != "(Wszystkie)")
            {
                filters.Add($"Odbiorca = '{odbiorca.Replace("'", "''")}'");
            }

            // Filtr: Typ zmiany
            string typ = cmbHistoriaTyp?.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(typ) && typ != "(Wszystkie)")
            {
                filters.Add($"TypZmiany = '{typ.Replace("'", "''")}'");
            }

            // Filtr: Handlowiec
            string handlowiec = cmbHistoriaHandlowiec?.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "(Wszystkie)")
            {
                filters.Add($"Handlowiec = '{handlowiec.Replace("'", "''")}'");
            }

            // Filtr: Towar (wyszukiwanie w opisie zmiany)
            string towar = cmbHistoriaTowar?.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(towar) && towar != "(Wszystkie)")
            {
                filters.Add($"OpisZmiany LIKE '%{towar.Replace("'", "''").Replace("[", "[[]").Replace("%", "[%]")}%'");
            }

            view.RowFilter = filters.Count > 0 ? string.Join(" AND ", filters) : "";
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
                if (txtOrderInfo != null) txtOrderInfo.Text = "";
                if (txtOrderClient != null) txtOrderClient.Text = "";

                int clientId = 0;
                string notes = "";
                string waluta = "PLN";
                var orderItems = new List<(int ProductCode, decimal Quantity, bool Foil, bool Hallal, string Cena)>(); // STRING!
                DateTime dateForReleases = ValidateSqlDate(_selectedDate.Date);

                await using (var cn = new SqlConnection(_connLibra))
                {
                    await cn.OpenAsync();

                    string slaughterDateSelect = _slaughterDateColumnExists ? ", DataUboju" : "";
                    string walutaSelect = _walutaColumnExists ? ", ISNULL(Waluta, 'PLN') as Waluta" : "";
                    using (var cmdInfo = new SqlCommand($@"
                SELECT KlientId, Uwagi, DataZamowienia{slaughterDateSelect}{walutaSelect}
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

                            // Odczytaj walutę
                            if (_walutaColumnExists)
                            {
                                int walutaIndex = _slaughterDateColumnExists ? 4 : 3;
                                if (readerInfo.FieldCount > walutaIndex && !readerInfo.IsDBNull(walutaIndex))
                                {
                                    waluta = readerInfo.GetString(walutaIndex);
                                }
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
                dt.Columns.Add("KodTowaru", typeof(int));  // Hidden - do identyfikacji
                dt.Columns.Add("Produkt", typeof(string));
                dt.Columns.Add("Zamówiono", typeof(decimal));
                dt.Columns.Add("Wydano", typeof(decimal));
                dt.Columns.Add("Różnica", typeof(decimal));
                dt.Columns.Add("Folia", typeof(bool));
                dt.Columns.Add("Hallal", typeof(bool));
                dt.Columns.Add("Cena", typeof(decimal));

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

                    // Konwertuj string na decimal dla ceny
                    decimal cenaValue = 0m;
                    if (!string.IsNullOrWhiteSpace(item.Cena))
                    {
                        decimal.TryParse(item.Cena, NumberStyles.Any, CultureInfo.InvariantCulture, out cenaValue);
                    }

                    dt.Rows.Add(item.ProductCode, product, ordered, released, difference, item.Foil, item.Hallal, cenaValue);
                    releases.Remove(item.ProductCode);
                }

                foreach (var kv in releases)
                {
                    if (!_productCatalogCache.ContainsKey(kv.Key))
                        continue;

                    string product = _productCatalogCache.TryGetValue(kv.Key, out var code) ?
                        code : $"Nieznany ({kv.Key})";
                    dt.Rows.Add(kv.Key, product, 0m, kv.Value, kv.Value, false, false, 0m);
                }

                txtNotes.Text = notes;
                _originalNotesValue = notes; // Zapisz oryginalną wartość do porównania przy edycji

                // Wyświetl informacje o zamówieniu
                string clientName = "";
                if (clientId > 0)
                {
                    await using var cnHandel = new SqlConnection(_connHandel);
                    await cnHandel.OpenAsync();
                    using var cmdClient = new SqlCommand("SELECT Shortcut FROM [HANDEL].[SSCommon].[STContractors] WHERE Id = @Id", cnHandel);
                    cmdClient.Parameters.AddWithValue("@Id", clientId);
                    var result = await cmdClient.ExecuteScalarAsync();
                    clientName = result?.ToString() ?? "";
                }

                // Oblicz sumy
                decimal sumaZam = orderItems.Sum(x => x.Quantity);
                int iloscPozycji = orderItems.Count;

                if (txtOrderInfo != null)
                {
                    txtOrderInfo.Text = $"(ID: {orderId} | Pozycji: {iloscPozycji} | Suma: {sumaZam:#,##0} kg)";
                }
                if (txtOrderClient != null)
                {
                    txtOrderClient.Text = !string.IsNullOrEmpty(clientName) ? $"👤 {clientName}" : "";
                }

                // Ustaw walutę w panelu
                if (cbWalutaPanel != null)
                {
                    foreach (ComboBoxItem item in cbWalutaPanel.Items)
                    {
                        if (item.Content?.ToString() == waluta)
                        {
                            cbWalutaPanel.SelectedItem = item;
                            break;
                        }
                    }
                }
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
                _originalNotesValue = "";
            }
        }
        private void SetupDetailsDataGrid()
        {
            dgDetails.Columns.Clear();
            dgDetails.CanUserAddRows = false;
            dgDetails.CanUserDeleteRows = false;

            // Ukryta kolumna KodTowaru
            dgDetails.Columns.Add(new DataGridTextColumn
            {
                Header = "KodTowaru",
                Binding = new System.Windows.Data.Binding("KodTowaru"),
                Visibility = Visibility.Collapsed
            });

            dgDetails.Columns.Add(new DataGridTextColumn
            {
                Header = "Produkt",
                Binding = new System.Windows.Data.Binding("Produkt"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = 80,
                IsReadOnly = true
            });

            // Edytowalna kolumna ilości - z separatorem tysięcy
            var zamowioneBinding = new System.Windows.Data.Binding("Zamówiono")
            {
                Mode = System.Windows.Data.BindingMode.TwoWay,
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.LostFocus,
                StringFormat = "#,##0"
            };
            dgDetails.Columns.Add(new DataGridTextColumn
            {
                Header = "Zam.",
                Binding = zamowioneBinding,
                Width = new DataGridLength(70),
                ElementStyle = (Style)FindResource("RightAlignedCellStyle"),
                IsReadOnly = false
            });

            dgDetails.Columns.Add(new DataGridTextColumn
            {
                Header = "Wyd.",
                Binding = new System.Windows.Data.Binding("Wydano") { StringFormat = "#,##0" },
                Width = new DataGridLength(70),
                ElementStyle = (Style)FindResource("RightAlignedCellStyle"),
                IsReadOnly = true
            });

            dgDetails.Columns.Add(new DataGridTextColumn
            {
                Header = "Róż.",
                Binding = new System.Windows.Data.Binding("Różnica") { StringFormat = "#,##0" },
                Width = new DataGridLength(70),
                ElementStyle = (Style)FindResource("RightAlignedCellStyle"),
                IsReadOnly = true
            });

            // Checkbox Folia
            var foliaBinding = new System.Windows.Data.Binding("Folia")
            {
                Mode = System.Windows.Data.BindingMode.TwoWay,
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged
            };
            dgDetails.Columns.Add(new DataGridCheckBoxColumn
            {
                Header = "Folia",
                Binding = foliaBinding,
                Width = new DataGridLength(40),
                IsReadOnly = false
            });

            // Checkbox Hallal
            var hallalBinding = new System.Windows.Data.Binding("Hallal")
            {
                Mode = System.Windows.Data.BindingMode.TwoWay,
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged
            };
            dgDetails.Columns.Add(new DataGridCheckBoxColumn
            {
                Header = "Hallal",
                Binding = hallalBinding,
                Width = new DataGridLength(45),
                IsReadOnly = false
            });

            // Edytowalna kolumna ceny
            var cenaBinding = new System.Windows.Data.Binding("Cena")
            {
                Mode = System.Windows.Data.BindingMode.TwoWay,
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.LostFocus,
                StringFormat = "N2"
            };
            dgDetails.Columns.Add(new DataGridTextColumn
            {
                Header = "Cena",
                Binding = cenaBinding,
                Width = new DataGridLength(60),
                ElementStyle = (Style)FindResource("RightAlignedCellStyle"),
                IsReadOnly = false
            });

            // Podpinanie eventów
            dgDetails.CellEditEnding -= DgDetails_CellEditEnding;
            dgDetails.CellEditEnding += DgDetails_CellEditEnding;
            dgDetails.PreviewKeyDown -= DgDetails_PreviewKeyDown;
            dgDetails.PreviewKeyDown += DgDetails_PreviewKeyDown;

            // Obsługa kliknięć w checkboxy (Folia/Hallal)
            dgDetails.BeginningEdit -= DgDetails_BeginningEdit;
            dgDetails.BeginningEdit += DgDetails_BeginningEdit;
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

            string notesContent = $"📦 Wydanie bez zamówienia\n\n" +
                           $"Odbiorca: {recipientName} (ID: {clientId})\n" +
                           $"Data: {day:yyyy-MM-dd dddd}\n\n" +
                           $"Poniżej lista wydanych produktów (tylko towary z katalogów 67095 i 67153)";
            txtNotes.Text = notesContent;
            _originalNotesValue = notesContent; // Wydanie bez zamówienia - notatki generowane, nie edytowalne

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
            var diagSw = System.Diagnostics.Stopwatch.StartNew();
            var diagTimes = new List<(string name, long ms)>();

            day = ValidateSqlDate(day);

            var dtAgg = new DataTable();
            dtAgg.Columns.Add("Produkt", typeof(string));
            dtAgg.Columns.Add("PlanowanyPrzychód", typeof(decimal));
            dtAgg.Columns.Add("FaktycznyPrzychód", typeof(decimal));
            dtAgg.Columns.Add("Stan", typeof(string));
            dtAgg.Columns.Add("Zamówienia", typeof(decimal));
            dtAgg.Columns.Add("Wydania", typeof(decimal));
            dtAgg.Columns.Add("Bilans", typeof(decimal));

            // Określ czy bilans ma uwzględniać wydania czy zamówienia
            bool uzywajWydan = rbBilansWydania?.IsChecked == true;

            diagSw.Restart();
            var (wspolczynnikTuszki, procentA, procentB) = await GetKonfiguracjaWydajnosciAsync(day);
            var konfiguracjaProduktow = await GetKonfiguracjaProduktowAsync(day);
            diagTimes.Add(("Konfiguracja", diagSw.ElapsedMilliseconds));

            diagSw.Restart();
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

            diagTimes.Add(("Harmonogram", diagSw.ElapsedMilliseconds));

            decimal pulaTuszki = totalMassDek * (wspolczynnikTuszki / 100m);
            decimal pulaTuszkiA = pulaTuszki * (procentA / 100m);
            decimal pulaTuszkiB = pulaTuszki * (procentB / 100m);

            diagSw.Restart();
            // OPTYMALIZACJA: Jedno zapytanie zamiast dwóch + jedno połączenie
            var actualIncomeTuszkaA = new Dictionary<int, decimal>();
            var actualIncomeElementy = new Dictionary<int, decimal>();
            await using (var cn = new SqlConnection(_connHandel))
            {
                await cn.OpenAsync();
                // Połączone zapytanie - pobiera oba typy w jednym wywołaniu
                const string sql = @"
                    SELECT MZ.idtw, SUM(ABS(MZ.ilosc)) AS Ilosc,
                           CASE WHEN MG.seria = 'sPWU' THEN 'T' ELSE 'E' END AS Typ
                    FROM [HANDEL].[HM].[MZ] MZ
                    JOIN [HANDEL].[HM].[MG] ON MZ.super = MG.id
                    WHERE MG.seria IN ('sPWU', 'sPWP', 'PWP') AND MG.aktywny=1 AND MG.data = @Day
                    GROUP BY MZ.idtw, CASE WHEN MG.seria = 'sPWU' THEN 'T' ELSE 'E' END";
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Day", day.Date);
                await using var rdr = await cmd.ExecuteReaderAsync();

                while (await rdr.ReadAsync())
                {
                    int productId = rdr.GetInt32(0);
                    decimal qty = rdr.IsDBNull(1) ? 0m : Convert.ToDecimal(rdr.GetValue(1));
                    string typ = rdr.GetString(2);

                    if (typ == "T")
                        actualIncomeTuszkaA[productId] = qty;
                    else
                        actualIncomeElementy[productId] = qty;
                }
            }
            diagTimes.Add(("PrzychodySql", diagSw.ElapsedMilliseconds));

            diagSw.Restart();
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
            diagTimes.Add(("ZamówieniaSql", diagSw.ElapsedMilliseconds));

            diagSw.Restart();
            // ✅ POBIERZ WYDANIA (WZ)
            var wydaniaSum = new Dictionary<int, decimal>();
            await using (var cn = new SqlConnection(_connHandel))
            {
                await cn.OpenAsync();
                const string sqlWydania = @"SELECT MZ.idtw, SUM(ABS(MZ.ilosc))
                    FROM [HANDEL].[HM].[MZ] MZ
                    JOIN [HANDEL].[HM].[MG] ON MZ.super = MG.id
                    WHERE MG.seria IN ('sWZ','sWZ-W') AND MG.aktywny=1 AND MG.data = @Day
                    GROUP BY MZ.idtw";
                await using var cmd = new SqlCommand(sqlWydania, cn);
                cmd.Parameters.AddWithValue("@Day", day.Date);
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    int productId = reader.GetInt32(0);
                    decimal qty = reader.IsDBNull(1) ? 0m : Convert.ToDecimal(reader.GetValue(1));
                    wydaniaSum[productId] = qty;
                }
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
            diagTimes.Add(("WydaniaStany", diagSw.ElapsedMilliseconds));

            diagSw.Restart();
            var kurczakA = _productCatalogCache.FirstOrDefault(p =>
                p.Value.Contains("Kurczak A", StringComparison.OrdinalIgnoreCase));

            decimal planA = 0m, factA = 0m, ordersA = 0m, wydaniaA = 0m, balanceA = 0m;
            string stanA = "";
            decimal stanMagA = 0m;

            if (kurczakA.Key > 0)
            {
                planA = pulaTuszkiA;
                factA = actualIncomeTuszkaA.TryGetValue(kurczakA.Key, out var a) ? a : 0m;
                ordersA = orderSum.TryGetValue(kurczakA.Key, out var z) ? z : 0m;
                wydaniaA = wydaniaSum.TryGetValue(kurczakA.Key, out var w) ? w : 0m;

                // ✅ POBIERZ STAN MAGAZYNOWY
                stanMagA = stanyMagazynowe.TryGetValue(kurczakA.Key, out var sA) ? sA : 0m;
                stanA = stanMagA > 0 ? stanMagA.ToString("N0") : "";

                // ✅ LOGIKA BILANSU: (Fakt lub Plan) + Stan - (Zamówienia lub Wydania)
                decimal odejmij = uzywajWydan ? wydaniaA : ordersA;
                if (factA > 0)
                {
                    balanceA = factA + stanMagA - odejmij;
                }
                else
                {
                    balanceA = planA + stanMagA - odejmij;
                }
            }

            decimal sumaPlanB = 0m;
            decimal sumaFaktB = 0m;
            decimal sumaZamB = 0m;
            decimal sumaWydB = 0m;
            decimal sumaStanB = 0m;

            var produktyB = new List<(string nazwa, decimal plan, decimal fakt, decimal zam, decimal wyd, string stan, decimal stanDec, decimal bilans)>();

            // Agregacja z uwzględnieniem scalania towarów
            var agregowaneGrupy = new Dictionary<string, (decimal plan, decimal fakt, decimal zam, decimal wyd, decimal stan, decimal procent)>(StringComparer.OrdinalIgnoreCase);
            var towaryWGrupach = new HashSet<int>();
            _detaleGrup.Clear(); // Wyczyść poprzednie szczegóły grup

            // Najpierw zbierz towary w grupach scalania
            foreach (var produktKonfig in konfiguracjaProduktow)
            {
                int produktId = produktKonfig.Key;
                decimal procentUdzialu = produktKonfig.Value;

                if (!_productCatalogCache.ContainsKey(produktId))
                    continue;

                string nazwaProdukt = _productCatalogCache[produktId];
                if (nazwaProdukt.Contains("Kurczak B", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (_mapowanieScalowania.TryGetValue(produktId, out var nazwaGrupy))
                {
                    towaryWGrupach.Add(produktId);

                    decimal plannedForProduct = pulaTuszkiB * (procentUdzialu / 100m);
                    var actual = actualIncomeElementy.TryGetValue(produktId, out var a) ? a : 0m;
                    var orders = orderSum.TryGetValue(produktId, out var z) ? z : 0m;
                    var releases = wydaniaSum.TryGetValue(produktId, out var r) ? r : 0m;
                    decimal stanMag = stanyMagazynowe.TryGetValue(produktId, out var sm) ? sm : 0m;

                    if (agregowaneGrupy.ContainsKey(nazwaGrupy))
                    {
                        var existing = agregowaneGrupy[nazwaGrupy];
                        agregowaneGrupy[nazwaGrupy] = (existing.plan + plannedForProduct, existing.fakt + actual, existing.zam + orders, existing.wyd + releases, existing.stan + stanMag, existing.procent + procentUdzialu);
                    }
                    else
                    {
                        agregowaneGrupy[nazwaGrupy] = (plannedForProduct, actual, orders, releases, stanMag, procentUdzialu);
                    }

                    // Zapisz szczegóły produktu dla grupy
                    if (!_detaleGrup.ContainsKey(nazwaGrupy))
                        _detaleGrup[nazwaGrupy] = new List<(string, decimal, decimal, decimal, string, decimal, decimal)>();

                    decimal odejmijProd = uzywajWydan ? releases : orders;
                    decimal balanceProd = (actual > 0 ? actual : plannedForProduct) + stanMag - odejmijProd;
                    string stanTextProd = stanMag > 0 ? stanMag.ToString("N0") : "";
                    _detaleGrup[nazwaGrupy].Add(($"      · {nazwaProdukt} ({procentUdzialu:F1}%)", plannedForProduct, actual, orders, stanTextProd, stanMag, balanceProd));
                }
            }

            // Dodaj scalone grupy
            foreach (var grupa in agregowaneGrupy.OrderByDescending(g => g.Value.procent))
            {
                string ikona = "";
                if (grupa.Key.Contains("Skrzydło", StringComparison.OrdinalIgnoreCase))
                    ikona = "🍗";
                else if (grupa.Key.Contains("Korpus", StringComparison.OrdinalIgnoreCase))
                    ikona = "🍖";
                else if (grupa.Key.Contains("Ćwiartka", StringComparison.OrdinalIgnoreCase) || grupa.Key.Contains("Noga", StringComparison.OrdinalIgnoreCase))
                    ikona = "🍗";
                else if (grupa.Key.Contains("Filet", StringComparison.OrdinalIgnoreCase))
                    ikona = "🥩";

                var (plan, fakt, zam, wyd, stan, procent) = grupa.Value;
                string stanText = stan > 0 ? stan.ToString("N0") : "";

                decimal odejmij = uzywajWydan ? wyd : zam;
                decimal balance = (fakt > 0 ? fakt : plan) + stan - odejmij;

                // Dodaj znak + lub - w zależności czy grupa jest rozwinięta
                bool isExpanded = _expandedGroups.Contains(grupa.Key);
                string expandIcon = isExpanded ? "▼" : "▶";

                string nazwaZIkonka = string.IsNullOrEmpty(ikona)
                    ? $"  {expandIcon} {grupa.Key} ({procent:F1}%)"
                    : $"  {expandIcon} {ikona} {grupa.Key} ({procent:F1}%)";

                produktyB.Add((nazwaZIkonka, plan, fakt, zam, wyd, stanText, stan, balance));

                // Jeśli grupa jest rozwinięta, dodaj szczegóły produktów
                if (isExpanded && _detaleGrup.TryGetValue(grupa.Key, out var detale))
                {
                    foreach (var detal in detale)
                    {
                        // Dodaj wydania = 0 dla szczegółów (nie mamy ich w detaleGrup)
                        produktyB.Add((detal.Item1, detal.Item2, detal.Item3, detal.Item4, 0m, detal.Item5, detal.Item6, detal.Item7));
                    }
                }

                sumaPlanB += plan;
                sumaFaktB += fakt;
                sumaZamB += zam;
                sumaWydB += wyd;
                sumaStanB += stan;
            }

            // Dodaj towary niescalone
            foreach (var produktKonfig in konfiguracjaProduktow.OrderByDescending(p => p.Value))
            {
                int produktId = produktKonfig.Key;
                if (towaryWGrupach.Contains(produktId)) continue;

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
                var releases = wydaniaSum.TryGetValue(produktId, out var r) ? r : 0m;

                // ✅ POBIERZ STAN MAGAZYNOWY
                decimal stanMag = stanyMagazynowe.TryGetValue(produktId, out var sm) ? sm : 0m;
                string stanText = stanMag > 0 ? stanMag.ToString("N0") : "";

                // ✅ LOGIKA BILANSU: (Fakt lub Plan) + Stan - (Zamówienia lub Wydania)
                decimal odejmij = uzywajWydan ? releases : orders;
                decimal balance;
                if (actual > 0)
                {
                    balance = actual + stanMag - odejmij;
                }
                else
                {
                    balance = plannedForProduct + stanMag - odejmij;
                }

                string nazwaZIkonka = string.IsNullOrEmpty(ikona)
                    ? $"  └ {nazwaProdukt} ({procentUdzialu:F1}%)"
                    : $"  └ {ikona} {nazwaProdukt} ({procentUdzialu:F1}%)";

                produktyB.Add((nazwaZIkonka, plannedForProduct, actual, orders, releases, stanText, stanMag, balance));

                sumaPlanB += plannedForProduct;
                sumaFaktB += actual;
                sumaZamB += orders;
                sumaWydB += releases;
                sumaStanB += stanMag;
            }

            // ✅ BILANS CAŁKOWITY DLA KURCZAKA B
            decimal odejmijB = uzywajWydan ? sumaWydB : sumaZamB;
            decimal bilansB;
            if (sumaFaktB > 0)
            {
                bilansB = sumaFaktB + sumaStanB - odejmijB;
            }
            else
            {
                bilansB = sumaPlanB + sumaStanB - odejmijB;
            }

            // ✅ BILANS CAŁKOWITY
            decimal bilansCalk = balanceA + bilansB;

            // ✅ DODAJ WIERSZE DO TABELI
            dtAgg.Rows.Add("═══ SUMA CAŁKOWITA ═══",
                planA + sumaPlanB,
                factA + sumaFaktB,
                (stanMagA + sumaStanB > 0 ? (stanMagA + sumaStanB).ToString("N0") : ""),
                ordersA + sumaZamB,
                wydaniaA + sumaWydB,
                bilansCalk);

            dtAgg.Rows.Add("🐔 Kurczak A",
                planA,
                factA,
                stanA,
                ordersA,
                wydaniaA,
                balanceA);

            dtAgg.Rows.Add("🐔 Kurczak B",
                sumaPlanB,
                sumaFaktB,
                (sumaStanB > 0 ? sumaStanB.ToString("N0") : ""),
                sumaZamB,
                sumaWydB,
                bilansB);

            foreach (var produkt in produktyB)
            {
                dtAgg.Rows.Add(produkt.nazwa, produkt.plan, produkt.fakt, produkt.stan, produkt.zam, produkt.wyd, produkt.bilans);
            }

            dgAggregation.ItemsSource = dtAgg.DefaultView;
            SetupAggregationDataGrid();

            // Wypełnij dashboard dostępności produktów dla handlowców
            PopulateDostepnoscProduktow(dtAgg);

            diagTimes.Add(("Agregacja", diagSw.ElapsedMilliseconds));
            _lastAggregationDiag = diagTimes;
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
                else if (produkt.StartsWith("  ▶") || produkt.StartsWith("  ▼"))
                {
                    // Wiersz grupy (zwijana/rozwijana)
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(225, 245, 254));
                    e.Row.FontWeight = FontWeights.SemiBold;
                    e.Row.FontSize = 11;
                    e.Row.Height = 28;
                    e.Row.Cursor = System.Windows.Input.Cursors.Hand;
                }
                else if (produkt.StartsWith("      ·"))
                {
                    // Wiersz szczegółowy produktu w grupie (rozwinięty)
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(240, 248, 255));
                    e.Row.FontStyle = FontStyles.Italic;
                    e.Row.FontSize = 9;
                    e.Row.Height = 22;
                }
                else if (produkt.StartsWith("  └"))
                {
                    // Towary niescalone
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(225, 245, 254));
                    e.Row.FontStyle = FontStyles.Normal;
                    e.Row.FontSize = 10;
                    e.Row.Height = 24;
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

            // Określ czy używamy wydań czy zamówień
            bool uzywajWydan = rbBilansWydania?.IsChecked == true;

            // Style dla przekreślenia
            var zamStyle = new Style(typeof(TextBlock));
            zamStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            zamStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(0, 0, 8, 0)));
            if (uzywajWydan)
            {
                zamStyle.Setters.Add(new Setter(TextBlock.TextDecorationsProperty, TextDecorations.Strikethrough));
                zamStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brushes.Gray));
            }

            var wydStyle = new Style(typeof(TextBlock));
            wydStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            wydStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(0, 0, 8, 0)));
            if (!uzywajWydan)
            {
                wydStyle.Setters.Add(new Setter(TextBlock.TextDecorationsProperty, TextDecorations.Strikethrough));
                wydStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brushes.Gray));
            }

            // 4. ZAM.
            dgAggregation.Columns.Add(new DataGridTextColumn
            {
                Header = uzywajWydan ? "Zam." : "Zam. ✓",
                Binding = new System.Windows.Data.Binding("Zamówienia") { StringFormat = "N0" },
                Width = new DataGridLength(65),
                ElementStyle = zamStyle
            });

            // 5. WYD.
            dgAggregation.Columns.Add(new DataGridTextColumn
            {
                Header = uzywajWydan ? "Wyd. ✓" : "Wyd.",
                Binding = new System.Windows.Data.Binding("Wydania") { StringFormat = "N0" },
                Width = new DataGridLength(65),
                ElementStyle = wydStyle
            });

            // 6. BILANS
            dgAggregation.Columns.Add(new DataGridTextColumn
            {
                Header = "Bil.",
                Binding = new System.Windows.Data.Binding("Bilans") { StringFormat = "N0" },
                Width = new DataGridLength(70),
                ElementStyle = (Style)FindResource("RightAlignedCellStyle")
            });

            // 7. PRODUKT
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

            dgAggregation.PreviewMouseLeftButtonUp -= DgAggregation_PreviewMouseLeftButtonUp;
            dgAggregation.PreviewMouseLeftButtonUp += DgAggregation_PreviewMouseLeftButtonUp;
        }

        private async void DgAggregation_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgAggregation.SelectedItem is DataRowView rowView)
            {
                var produktNazwa = rowView.Row.Field<string>("Produkt") ?? "";

                // Sprawdź czy kliknięto na wiersz grupy (z ikoną ▶ lub ▼)
                if (produktNazwa.StartsWith("  ▶") || produktNazwa.StartsWith("  ▼"))
                {
                    // Wyciągnij nazwę grupy
                    var nazwaGrupy = produktNazwa
                        .Replace("▶", "")
                        .Replace("▼", "")
                        .Replace("🍗", "")
                        .Replace("🍖", "")
                        .Replace("🥩", "")
                        .Trim();

                    // Usuń procent z końca
                    int lastParen = nazwaGrupy.LastIndexOf('(');
                    if (lastParen > 0)
                        nazwaGrupy = nazwaGrupy.Substring(0, lastParen).Trim();

                    // Przełącz stan rozwinięcia
                    if (_expandedGroups.Contains(nazwaGrupy))
                        _expandedGroups.Remove(nazwaGrupy);
                    else
                        _expandedGroups.Add(nazwaGrupy);

                    // Odśwież agregację aby pokazać/ukryć szczegóły
                    await RefreshAggregationAsync();
                    e.Handled = true;
                }
            }
        }

        private async Task RefreshAggregationAsync()
        {
            await DisplayProductAggregationAsync(_selectedDate);
        }

        private async void RbBilans_Checked(object sender, RoutedEventArgs e)
        {
            // Odśwież agregację przy zmianie radio button
            await RefreshAggregationAsync();
        }

        private async void DgAggregation_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgAggregation.SelectedItem is DataRowView rowView)
            {
                var produktNazwa = rowView.Row.Field<string>("Produkt") ?? "";

                if (produktNazwa.StartsWith("═══"))
                    return;

                // Jeśli to wiersz szczegółowy (rozwinięty z grupy), ignoruj double-click
                if (produktNazwa.StartsWith("      ·"))
                    return;

                // Jeśli to wiersz grupy (▶ lub ▼), toggle expand zamiast double-click
                if (produktNazwa.StartsWith("  ▶") || produktNazwa.StartsWith("  ▼"))
                {
                    // Już obsługiwane przez single-click
                    return;
                }

                var czystyProdukt = produktNazwa
                    .Replace("└", "")
                    .Replace("▶", "")
                    .Replace("▼", "")
                    .Replace("·", "")
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

                // Sprawdź czy to jest grupa scalowania
                if (_grupyDoProduktow.ContainsKey(czystyProdukt))
                {
                    // Pokaż szczegóły grupy
                    await PokazSzczegolyGrupyAsync(czystyProdukt, plan, fakt, zamowienia, bilans);
                    return;
                }

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

        private async Task PokazSzczegolyGrupyAsync(string nazwaGrupy, decimal planSuma, decimal faktSuma, decimal zamowieniaSuma, decimal bilansSuma)
        {
            if (!_grupyDoProduktow.TryGetValue(nazwaGrupy, out var produktyIds) || !produktyIds.Any())
            {
                MessageBox.Show("Brak produktów w tej grupie.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Pobierz szczegóły dla każdego produktu w grupie
            var szczegoly = new System.Text.StringBuilder();
            szczegoly.AppendLine($"📊 SZCZEGÓŁY GRUPY: {nazwaGrupy}");
            szczegoly.AppendLine($"════════════════════════════════════════");
            szczegoly.AppendLine();
            szczegoly.AppendLine($"{"Produkt",-25} {"Plan",10} {"Fakt",10} {"Zam.",10} {"Bilans",10}");
            szczegoly.AppendLine($"{"───────────────────────",-25} {"──────────",10} {"──────────",10} {"──────────",10} {"──────────",10}");

            decimal sumaPlan = 0, sumaFakt = 0, sumaZam = 0;

            foreach (var produktId in produktyIds)
            {
                string nazwaProdukt = _productCatalogCache.TryGetValue(produktId, out var n) ? n : $"ID:{produktId}";

                // Pobierz dane dla tego produktu
                var (plan, fakt, zam) = await PobierzDaneProduktuAsync(produktId, _selectedDate);

                decimal bil = (fakt > 0 ? fakt : plan) - zam;

                szczegoly.AppendLine($"{nazwaProdukt,-25} {plan,10:N0} {fakt,10:N0} {zam,10:N0} {bil,10:N0}");

                sumaPlan += plan;
                sumaFakt += fakt;
                sumaZam += zam;
            }

            decimal sumaBilans = (sumaFakt > 0 ? sumaFakt : sumaPlan) - sumaZam;

            szczegoly.AppendLine($"{"───────────────────────",-25} {"──────────",10} {"──────────",10} {"──────────",10} {"──────────",10}");
            szczegoly.AppendLine($"{"SUMA",-25} {sumaPlan,10:N0} {sumaFakt,10:N0} {sumaZam,10:N0} {sumaBilans,10:N0}");
            szczegoly.AppendLine();
            szczegoly.AppendLine($"Produktów w grupie: {produktyIds.Count}");

            MessageBox.Show(szczegoly.ToString(), $"Szczegóły grupy: {nazwaGrupy}",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task<(decimal plan, decimal fakt, decimal zam)> PobierzDaneProduktuAsync(int produktId, DateTime dzien)
        {
            decimal plan = 0, fakt = 0, zam = 0;

            try
            {
                // Pobierz plan z konfiguracji
                var konfig = await GetKonfiguracjaProduktowAsync(dzien);
                if (konfig.TryGetValue(produktId, out var procent))
                {
                    var (wsp, procA, procB) = await GetKonfiguracjaWydajnosciAsync(dzien);

                    // Pobierz masę z harmonogramu
                    decimal masaDek = 0;
                    await using (var cn = new SqlConnection(_connLibra))
                    {
                        await cn.OpenAsync();
                        var sql = "SELECT SUM(WagaDek * SztukiDek) FROM dbo.HarmonogramDostaw WHERE DataOdbioru = @Day AND Bufor IN ('B.Wolny', 'B.Kontr.', 'Potwierdzony')";
                        await using var cmd = new SqlCommand(sql, cn);
                        cmd.Parameters.AddWithValue("@Day", dzien.Date);
                        var result = await cmd.ExecuteScalarAsync();
                        if (result != DBNull.Value && result != null)
                            masaDek = Convert.ToDecimal(result);
                    }

                    decimal pulaTuszkiB = masaDek * (wsp / 100m) * (procB / 100m);
                    plan = pulaTuszkiB * (procent / 100m);
                }

                // Pobierz faktyczny przychód
                await using (var cn = new SqlConnection(_connHandel))
                {
                    await cn.OpenAsync();
                    var sql = @"SELECT SUM(ABS(MZ.ilosc)) FROM [HANDEL].[HM].[MZ] MZ
                                JOIN [HANDEL].[HM].[MG] ON MZ.super = MG.id
                                WHERE MZ.idtw = @Id AND MG.seria IN ('sPWP', 'PWP') AND MG.aktywny=1 AND MG.data = @Day";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Id", produktId);
                    cmd.Parameters.AddWithValue("@Day", dzien.Date);
                    var result = await cmd.ExecuteScalarAsync();
                    if (result != DBNull.Value && result != null)
                        fakt = Convert.ToDecimal(result);
                }

                // Pobierz zamówienia
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
                    var sql = $"SELECT SUM(Ilosc) FROM [dbo].[ZamowieniaMiesoTowar] WHERE KodTowaru = @Id AND ZamowienieId IN ({string.Join(",", orderIds)})";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Id", produktId);
                    var result = await cmd.ExecuteScalarAsync();
                    if (result != DBNull.Value && result != null)
                        zam = Convert.ToDecimal(result);
                }
            }
            catch { }

            return (plan, fakt, zam);
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

            // Filtruj zamówienia - sprawdź czy kolumny istnieją
            if (_dtOrders.Columns.Count > 0 && _dtOrders.Columns.Contains("Status"))
            {
                var conditions = new List<string>();

                if (!string.IsNullOrEmpty(txt) && _dtOrders.Columns.Contains("Odbiorca"))
                    conditions.Add($"Odbiorca LIKE '%{txt}%'");

                if (!string.IsNullOrEmpty(salesmanFilter) && _dtOrders.Columns.Contains("Handlowiec"))
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
            _originalNotesValue = "";
            _currentOrderId = null;
        }

        /// <summary>
        /// Sanityzuje nazwę grupy do użycia jako nazwa kolumny DataTable (bez znaków specjalnych)
        /// </summary>
        private string SanitizeColumnName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Grupa";
            // Zamień spacje i znaki specjalne na podkreślniki
            var sanitized = new System.Text.StringBuilder();
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c))
                    sanitized.Append(c);
                else
                    sanitized.Append('_');
            }
            return "G_" + sanitized.ToString();
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

        #region Dashboard

        private async Task LoadDashboardDataAsync(DateTime day)
        {
            day = ValidateSqlDate(day);

            _dtDashboard.Rows.Clear();

            if (_dtDashboard.Columns.Count == 0)
            {
                _dtDashboard.Columns.Add("Produkt", typeof(string));
                _dtDashboard.Columns.Add("Plan", typeof(decimal));
                _dtDashboard.Columns.Add("Fakt", typeof(decimal));
                _dtDashboard.Columns.Add("Stan", typeof(decimal));
                _dtDashboard.Columns.Add("Zamowienia", typeof(decimal));
                _dtDashboard.Columns.Add("Bilans", typeof(decimal));
                _dtDashboard.Columns.Add("Status", typeof(string));
            }

            // Pobierz dane konfiguracji wydajności
            var (wspolczynnikTuszki, procentA, procentB) = await GetKonfiguracjaWydajnosciAsync(day);

            // Pobierz konfigurację produktów (TowarID -> ProcentUdzialu z TuszkiB)
            var konfiguracjaProduktow = await GetKonfiguracjaProduktowAsync(day);

            // Pobierz masę deklaracji z HarmonogramDostaw
            decimal totalMassDek = 0m;
            await using (var cn = new SqlConnection(_connLibra))
            {
                await cn.OpenAsync();
                const string sql = @"SELECT WagaDek, SztukiDek FROM dbo.HarmonogramDostaw
                                     WHERE DataOdbioru = @Day AND Bufor IN ('B.Wolny', 'B.Kontr.', 'Potwierdzony')";
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

            // Oblicz pulę tuszki na podstawie wydajności
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
                cmd.Parameters.AddWithValue("@Day", day.Date);
                await using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    int productId = rdr.GetInt32(0);
                    decimal qty = rdr.IsDBNull(1) ? 0m : Convert.ToDecimal(rdr.GetValue(1));
                    actualIncome[productId] = qty;
                }
            }

            // Pobierz zamówienia
            var orderSum = new Dictionary<int, decimal>();
            var orderIds = _dtOrders.AsEnumerable()
                .Where(r => !string.Equals(r.Field<string>("Status"), "Anulowane", StringComparison.OrdinalIgnoreCase))
                .Where(r => r.Field<string>("Status") != "SUMA")
                .Select(r => r.Field<int>("Id"))
                .Where(id => id > 0)
                .ToList();

            // Słownik: ProductId -> lista (Odbiorca, Ilosc, Handlowiec)
            var orderDetailsPerProductId = new Dictionary<int, List<(string odbiorca, decimal ilosc, string handlowiec)>>();

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

            // Pobierz szczegóły zamówień per produkt (kto co zamówił)
            if (orderIds.Any())
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                var sql = $@"SELECT t.KodTowaru, z.Odbiorca, SUM(t.Ilosc), z.Handlowiec
                             FROM [dbo].[ZamowieniaMiesoTowar] t
                             JOIN [dbo].[ZamowieniaMieso] z ON t.ZamowienieId = z.Id
                             WHERE t.ZamowienieId IN ({string.Join(",", orderIds)})
                             GROUP BY t.KodTowaru, z.Odbiorca, z.Handlowiec
                             ORDER BY t.KodTowaru, SUM(t.Ilosc) DESC";
                using var cmd = new SqlCommand(sql, cn);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    int productId = reader.GetInt32(0);
                    string odbiorca = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    decimal ilosc = reader.IsDBNull(2) ? 0m : reader.GetDecimal(2);
                    string handlowiec = reader.IsDBNull(3) ? "" : reader.GetString(3);

                    if (!orderDetailsPerProductId.ContainsKey(productId))
                        orderDetailsPerProductId[productId] = new List<(string, decimal, string)>();
                    orderDetailsPerProductId[productId].Add((odbiorca, ilosc, handlowiec));
                }
            }

            // Pobierz stany magazynowe
            var stanyMagazynowe = new Dictionary<int, decimal>();
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                const string sqlStany = @"SELECT ProduktId, Stan FROM dbo.StanyMagazynowe WHERE Data = @Data";
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
            catch { }

            // Dodaj produkty do tabeli z uwzględnieniem scalania
            decimal totalZamowienia = 0m;
            decimal totalWydania = 0m;
            decimal totalPlan = 0m;

            // Agregacja produktów z uwzględnieniem scalania
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

                    // Oblicz plan
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

                // Oblicz plan na podstawie konfiguracji wydajności
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

            // Mapuj szczegóły zamówień do nazw produktów (w tym grup scalania)
            _orderDetailsPerProduct.Clear();
            var productIdToName = new Dictionary<int, string>();

            // Zbierz mapowanie productId -> nazwa (lub nazwa grupy)
            foreach (var product in _productCatalogCache)
            {
                int productId = product.Key;
                string productName = product.Value;

                if (_mapowanieScalowania.TryGetValue(productId, out var nazwaGrupy))
                    productIdToName[productId] = nazwaGrupy;
                else
                    productIdToName[productId] = productName;
            }

            // Zbierz szczegóły zamówień per nazwa produktu/grupy
            foreach (var kvp in orderDetailsPerProductId)
            {
                int productId = kvp.Key;
                if (!productIdToName.TryGetValue(productId, out var productName))
                    continue;

                if (!_orderDetailsPerProduct.ContainsKey(productName))
                    _orderDetailsPerProduct[productName] = new List<(string, decimal, string)>();

                _orderDetailsPerProduct[productName].AddRange(kvp.Value);
            }

            // Agreguj szczegóły zamówień dla grup (suma per odbiorca)
            foreach (var kvp in _orderDetailsPerProduct.ToList())
            {
                var aggregated = kvp.Value
                    .GroupBy(x => (x.odbiorca, x.handlowiec))
                    .Select(g => (g.Key.odbiorca, g.Sum(x => x.ilosc), g.Key.handlowiec))
                    .OrderByDescending(x => x.Item2)
                    .ToList();
                _orderDetailsPerProduct[kvp.Key] = aggregated;
            }

            // Dodaj do tabeli
            foreach (var kv in agregowane.OrderBy(x => x.Key))
            {
                var (plan, fakt, stan, zamowienia) = kv.Value;

                // Pokaż produkt jeśli ma jakiekolwiek dane lub ma plan
                if (zamowienia == 0 && fakt == 0 && stan == 0 && plan == 0) continue;

                // Bilans = dostępne (fakt lub plan) + stan - zamówienia
                decimal bilans = (fakt > 0 ? fakt : plan) + stan - zamowienia;
                string status = bilans > 0 ? "✅" : (bilans == 0 ? "⚠️" : "❌");

                // Dodaj ikonę rozwijania jeśli są szczegóły zamówień
                bool hasDetails = _orderDetailsPerProduct.ContainsKey(kv.Key) && _orderDetailsPerProduct[kv.Key].Any();
                bool isExpanded = _expandedDashboardProducts.Contains(kv.Key);
                string expandIcon = hasDetails ? (isExpanded ? "▼ " : "▶ ") : "  ";
                string displayName = expandIcon + kv.Key;

                _dtDashboard.Rows.Add(displayName, plan, fakt, stan, zamowienia, bilans, status);

                // Jeśli rozwinięty, dodaj wiersze szczegółów
                if (isExpanded && hasDetails)
                {
                    foreach (var detail in _orderDetailsPerProduct[kv.Key])
                    {
                        string detailName = $"      └ {detail.odbiorca} ({detail.handlowiec})";
                        _dtDashboard.Rows.Add(detailName, 0m, 0m, 0m, detail.ilosc, 0m, "");
                    }
                }

                totalZamowienia += zamowienia;
                totalWydania += fakt;
                totalPlan += plan;
            }

            // Aktualizuj KPI
            int klientowCount = _dtOrders.AsEnumerable()
                .Where(r => r.Field<string>("Status") != "SUMA" && r.Field<int>("Id") > 0)
                .Select(r => r.Field<int>("KlientId"))
                .Distinct().Count();

            int wydanychCount = _dtOrders.AsEnumerable()
                .Where(r => r.Field<string>("Status") != "SUMA" && r.Field<int>("Id") > 0)
                .Where(r => r.Field<decimal>("IloscFaktyczna") > 0)
                .Count();

            // Zlicz statusy bilansu
            int okCount = _dtDashboard.AsEnumerable().Count(r => r.Field<string>("Status") == "✅");
            int uwagaCount = _dtDashboard.AsEnumerable().Count(r => r.Field<string>("Status") == "⚠️");
            int brakCount = _dtDashboard.AsEnumerable().Count(r => r.Field<string>("Status") == "❌");

            txtBilansOk.Text = okCount.ToString();
            txtBilansUwaga.Text = uwagaCount.ToString();
            txtBilansBrak.Text = brakCount.ToString();

            // Wydajność główna
            txtWydajnoscGlowna.Text = $"{wspolczynnikTuszki:N0}%";
            txtTuszkaA.Text = $"{procentA:N0}%";
            txtTuszkaB.Text = $"{procentB:N0}%";

            // Aktualizuj pasek postępu wydajności (max 100%, skala 0-250px)
            double progressWidth = (double)(wspolczynnikTuszki / 100m) * 250;
            progressWydajnosc.Width = Math.Min(progressWidth, 250);

            SetupDashboardDataGrid();
        }

        private void SetupDashboardDataGrid()
        {
            dgDashboardProdukty.ItemsSource = _dtDashboard.DefaultView;
            dgDashboardProdukty.Columns.Clear();

            // 1. Produkt
            dgDashboardProdukty.Columns.Add(new DataGridTextColumn
            {
                Header = "Produkt",
                Binding = new System.Windows.Data.Binding("Produkt"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = 150
            });

            // 2. Plan
            dgDashboardProdukty.Columns.Add(new DataGridTextColumn
            {
                Header = "Plan",
                Binding = new System.Windows.Data.Binding("Plan") { StringFormat = "N0" },
                Width = new DataGridLength(80),
                ElementStyle = (Style)FindResource("RightAlignedCellStyle")
            });

            // 3. Fakt
            dgDashboardProdukty.Columns.Add(new DataGridTextColumn
            {
                Header = "Fakt",
                Binding = new System.Windows.Data.Binding("Fakt") { StringFormat = "N0" },
                Width = new DataGridLength(80),
                ElementStyle = (Style)FindResource("RightAlignedCellStyle")
            });

            // 4. Stan
            dgDashboardProdukty.Columns.Add(new DataGridTextColumn
            {
                Header = "Stan",
                Binding = new System.Windows.Data.Binding("Stan") { StringFormat = "N0" },
                Width = new DataGridLength(80),
                ElementStyle = (Style)FindResource("RightAlignedCellStyle")
            });

            // 5. Zamówienia
            dgDashboardProdukty.Columns.Add(new DataGridTextColumn
            {
                Header = "Zamów.",
                Binding = new System.Windows.Data.Binding("Zamowienia") { StringFormat = "N0" },
                Width = new DataGridLength(80),
                ElementStyle = (Style)FindResource("RightAlignedCellStyle")
            });

            // 6. Bilans
            var bilansStyle = new Style(typeof(TextBlock));
            bilansStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            bilansStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Bold));

            dgDashboardProdukty.Columns.Add(new DataGridTextColumn
            {
                Header = "Bilans",
                Binding = new System.Windows.Data.Binding("Bilans") { StringFormat = "N0" },
                Width = new DataGridLength(80),
                ElementStyle = bilansStyle
            });

            // 7. Status
            dgDashboardProdukty.Columns.Add(new DataGridTextColumn
            {
                Header = "Status",
                Binding = new System.Windows.Data.Binding("Status"),
                Width = new DataGridLength(60),
                ElementStyle = (Style)FindResource("CenterAlignedCellStyle")
            });

            // Kolorowanie wierszy
            dgDashboardProdukty.LoadingRow -= DgDashboardProdukty_LoadingRow;
            dgDashboardProdukty.LoadingRow += DgDashboardProdukty_LoadingRow;

            // Obsługa kliknięcia do rozwijania/zwijania
            dgDashboardProdukty.PreviewMouseLeftButtonUp -= DgDashboardProdukty_RowClick;
            dgDashboardProdukty.PreviewMouseLeftButtonUp += DgDashboardProdukty_RowClick;
        }

        private async void DgDashboardProdukty_RowClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgDashboardProdukty.SelectedItem is DataRowView rowView)
            {
                string produktName = rowView["Produkt"]?.ToString() ?? "";

                // Sprawdź czy to wiersz produktu (nie szczegół)
                if (produktName.TrimStart().StartsWith("└"))
                    return;

                // Wyodrębnij nazwę produktu bez ikony
                string cleanName = produktName.Replace("▶ ", "").Replace("▼ ", "").Trim();

                // Sprawdź czy produkt ma szczegóły do rozwinięcia
                if (!_orderDetailsPerProduct.ContainsKey(cleanName) || !_orderDetailsPerProduct[cleanName].Any())
                    return;

                // Toggle expand/collapse
                if (_expandedDashboardProducts.Contains(cleanName))
                    _expandedDashboardProducts.Remove(cleanName);
                else
                    _expandedDashboardProducts.Add(cleanName);

                // Odśwież dashboard
                await LoadDashboardDataAsync(_selectedDate);
            }
        }

        private void DgDashboardProdukty_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                var produktName = rowView.Row.Field<string>("Produkt") ?? "";
                var bilans = rowView.Row.Field<decimal>("Bilans");
                var status = rowView.Row.Field<string>("Status") ?? "";

                // Wiersze szczegółów (kto zamówił) - jaśniejsze tło
                if (produktName.TrimStart().StartsWith("└"))
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(245, 248, 255)); // Jasnoniebieski
                    e.Row.FontSize = 11;
                    e.Row.FontStyle = FontStyles.Italic;
                }
                else if (status == "❌" || bilans < 0)
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(255, 230, 230)); // Czerwony
                    e.Row.FontWeight = FontWeights.SemiBold;
                }
                else if (status == "⚠️" || bilans == 0)
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(255, 250, 205)); // Żółty
                }
                else
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(230, 255, 230)); // Zielony
                }
            }
        }

        #endregion

        #region Currency Panel Events

        private async void CbWalutaPanel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized || !_currentOrderId.HasValue || _currentOrderId.Value <= 0)
                return;

            if (cbWalutaPanel.SelectedItem is ComboBoxItem selectedItem)
            {
                string waluta = selectedItem.Content?.ToString() ?? "PLN";

                // Zapisz walutę w bazie
                if (_walutaColumnExists)
                {
                    try
                    {
                        await using var cn = new SqlConnection(_connLibra);
                        await cn.OpenAsync();

                        await using var cmd = new SqlCommand(
                            "UPDATE dbo.ZamowieniaMieso SET Waluta = @waluta WHERE Id = @id", cn);
                        cmd.Parameters.AddWithValue("@waluta", waluta);
                        cmd.Parameters.AddWithValue("@id", _currentOrderId.Value);
                        await cmd.ExecuteNonQueryAsync();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Błąd podczas zapisywania waluty:\n{ex.Message}",
                            "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        #endregion

        #region Details Grid Edit Events

        private object? _editOldValue = null;
        private string _editColumnName = "";
        private int _editKodTowaru = 0;
        private string _editProduktNazwa = "";

        private void DgDetails_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                // Zatwierdzenie edycji przy Enter
                dgDetails.CommitEdit(DataGridEditingUnit.Cell, true);
                dgDetails.CommitEdit(DataGridEditingUnit.Row, true);
                e.Handled = true;
            }
        }

        private void DgDetails_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            if (e.Row.Item is not DataRowView rowView)
                return;

            var row = rowView.Row;
            _editKodTowaru = row.Field<int>("KodTowaru");
            _editProduktNazwa = row.Field<string>("Produkt") ?? "";
            _editColumnName = e.Column.Header?.ToString() ?? "";

            // Zapisz starą wartość przed edycją
            _editOldValue = _editColumnName switch
            {
                "Zam." => row.Field<decimal>("Zamówiono"),
                "Cena" => row.Field<decimal>("Cena"),
                "Folia" => row.Field<bool>("Folia"),
                "Hallal" => row.Field<bool>("Hallal"),
                _ => null
            };
        }

        private async void DgDetails_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Cancel)
            {
                _editOldValue = null;
                return;
            }

            if (!_currentOrderId.HasValue || _currentOrderId.Value <= 0)
                return;

            if (e.Row.Item is not DataRowView rowView)
                return;

            string columnName = e.Column.Header?.ToString() ?? "";
            object? newValue = null;

            if (e.EditingElement is TextBox textBox)
            {
                string newText = textBox.Text;

                if (columnName == "Zam.")
                {
                    if (decimal.TryParse(newText, NumberStyles.Any, CultureInfo.CurrentCulture, out decimal newQty))
                    {
                        newValue = newQty;
                    }
                }
                else if (columnName == "Cena")
                {
                    if (decimal.TryParse(newText, NumberStyles.Any, CultureInfo.CurrentCulture, out decimal newPrice))
                    {
                        newValue = newPrice;
                    }
                }
            }
            else if (e.EditingElement is CheckBox checkBox)
            {
                newValue = checkBox.IsChecked ?? false;
            }

            if (newValue == null || (_editOldValue != null && _editOldValue.Equals(newValue)))
            {
                _editOldValue = null;
                return;
            }

            // Zapisz zmianę w bazie
            await SaveOrderItemChangeAsync(_editKodTowaru, columnName, _editOldValue, newValue, _editProduktNazwa);
            _editOldValue = null;
        }

        private async Task SaveOrderItemChangeAsync(int kodTowaru, string columnName, object? oldValue, object? newValue, string produktNazwa)
        {
            if (!_currentOrderId.HasValue) return;

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                string dbColumn = columnName switch
                {
                    "Zam." => "Ilosc",
                    "Cena" => "Cena",
                    "Folia" => "Folia",
                    "Hallal" => "Hallal",
                    _ => ""
                };

                if (string.IsNullOrEmpty(dbColumn)) return;

                // Aktualizuj wartość w bazie
                string updateSql = dbColumn == "Cena"
                    ? "UPDATE dbo.ZamowieniaMiesoTowar SET Cena = @value WHERE ZamowienieId = @orderId AND KodTowaru = @kodTowaru"
                    : $"UPDATE dbo.ZamowieniaMiesoTowar SET {dbColumn} = @value WHERE ZamowienieId = @orderId AND KodTowaru = @kodTowaru";

                await using var cmdUpdate = new SqlCommand(updateSql, cn);

                if (dbColumn == "Cena")
                {
                    cmdUpdate.Parameters.AddWithValue("@value", newValue != null ? ((decimal)newValue).ToString("F2", CultureInfo.InvariantCulture) : DBNull.Value);
                }
                else if (dbColumn == "Ilosc")
                {
                    cmdUpdate.Parameters.AddWithValue("@value", newValue ?? DBNull.Value);
                }
                else
                {
                    cmdUpdate.Parameters.AddWithValue("@value", newValue ?? false);
                }

                cmdUpdate.Parameters.AddWithValue("@orderId", _currentOrderId.Value);
                cmdUpdate.Parameters.AddWithValue("@kodTowaru", kodTowaru);

                await cmdUpdate.ExecuteNonQueryAsync();

                // Oznacz zamówienie jako zmodyfikowane dla faktur
                await using var cmdFlagModified = new SqlCommand(
                    @"UPDATE dbo.ZamowieniaMieso
                      SET CzyZmodyfikowaneDlaFaktur = 1,
                          DataOstatniejModyfikacji = SYSDATETIME(),
                          ModyfikowalPrzez = @user
                      WHERE Id = @orderId", cn);
                cmdFlagModified.Parameters.AddWithValue("@orderId", _currentOrderId.Value);
                cmdFlagModified.Parameters.AddWithValue("@user", App.UserFullName ?? UserID);
                await cmdFlagModified.ExecuteNonQueryAsync();

                // Zapisz w historii zmian (używając HistoriaZmianService)
                string staraWartosc = FormatValueForHistory(oldValue, columnName);
                string nowaWartosc = FormatValueForHistory(newValue, columnName);

                string opisZmiany = $"{produktNazwa}: {columnName} {staraWartosc} → {nowaWartosc}";

                await Services.HistoriaZmianService.LogujEdycje(
                    _currentOrderId.Value,
                    UserID,
                    App.UserFullName,
                    $"Pozycja: {produktNazwa} - {columnName}",
                    staraWartosc,
                    nowaWartosc,
                    opisZmiany);

                // Odśwież dane po zmianie ilości (aby przeliczyć różnicę)
                if (columnName == "Zam.")
                {
                    await RefreshAllDataAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zapisywania zmiany:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string FormatValueForHistory(object? value, string columnName)
        {
            if (value == null) return "brak";

            return columnName switch
            {
                "Zam." => $"{value:N0} kg",
                "Cena" => $"{value:N2} zł",
                "Folia" => (bool)value ? "TAK" : "NIE",
                "Hallal" => (bool)value ? "TAK" : "NIE",
                _ => value.ToString() ?? "brak"
            };
        }

        private async void TxtNotes_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!_currentOrderId.HasValue) return;

            string newNotes = txtNotes.Text ?? "";
            string oldNotes = _originalNotesValue ?? "";

            // Sprawdź czy notatki się zmieniły
            if (newNotes == oldNotes) return;

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                // Aktualizuj notatki w bazie
                await using var cmdUpdate = new SqlCommand(
                    "UPDATE dbo.ZamowieniaMieso SET Uwagi = @uwagi WHERE Id = @orderId", cn);
                cmdUpdate.Parameters.AddWithValue("@uwagi", newNotes);
                cmdUpdate.Parameters.AddWithValue("@orderId", _currentOrderId.Value);
                await cmdUpdate.ExecuteNonQueryAsync();

                // Zapisz zmianę w historii (używając HistoriaZmianService)
                await Services.HistoriaZmianService.LogujZmianeNotatki(
                    _currentOrderId.Value,
                    UserID,
                    oldNotes,
                    newNotes,
                    App.UserFullName);

                // Zaktualizuj oryginalną wartość
                _originalNotesValue = newNotes;

                // Krótkie potwierdzenie
                System.Diagnostics.Debug.WriteLine($"Notatki zapisane dla zamówienia {_currentOrderId.Value}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zapisywania notatek:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Scalowanie towarów
        private void InicjalizujMenuKontekstoweBilansu()
        {
            var contextMenu = new System.Windows.Controls.ContextMenu();

            var menuKonfiguracja = new System.Windows.Controls.MenuItem
            {
                Header = "Konfiguruj produkty i scalowanie"
            };
            menuKonfiguracja.Click += async (s, e) =>
            {
                // Otwórz okno konfiguracji produktów (zawiera też scalowanie)
                var konfig = new Dictionary<string, decimal>();
                var dialog = new KonfiguracjaProduktow(_connLibra, _connHandel, konfig);
                dialog.ShowDialog();

                // Po zamknięciu dialogu odśwież dane
                await RefreshAllDataAsync();
            };

            var menuOdswiez = new System.Windows.Controls.MenuItem
            {
                Header = "Odśwież podsumowanie"
            };
            menuOdswiez.Click += async (s, e) =>
            {
                await RefreshAllDataAsync();
            };

            contextMenu.Items.Add(menuKonfiguracja);
            contextMenu.Items.Add(new System.Windows.Controls.Separator());
            contextMenu.Items.Add(menuOdswiez);

            dgAggregation.ContextMenu = contextMenu;
        }
        #endregion

        #region Dashboard Dostępność Produktów

        private void PopulateDostepnoscProduktow(DataTable dtAgg)
        {
            var produkty = new List<DostepnoscProduktuModel>();

            // Przetwórz każdy wiersz z agregacji
            foreach (DataRow row in dtAgg.Rows)
            {
                string nazwa = row["Produkt"]?.ToString() ?? "";

                // Pomijaj wiersze SUMA, rozwinięte szczegóły (·) i Kurczak B
                if (nazwa.Contains("SUMA") || nazwa.TrimStart().StartsWith("·"))
                    continue;

                // Pomiń Kurczak B (tylko Kurczak A i elementy)
                if (nazwa.Contains("Kurczak B"))
                    continue;

                // Pomiń produkty mrożone
                if (nazwa.Contains("mrożon", StringComparison.OrdinalIgnoreCase) ||
                    nazwa.Contains("Mrożon", StringComparison.OrdinalIgnoreCase))
                    continue;

                decimal plan = row["PlanowanyPrzychód"] != DBNull.Value ? Convert.ToDecimal(row["PlanowanyPrzychód"]) : 0;
                decimal fakt = row["FaktycznyPrzychód"] != DBNull.Value ? Convert.ToDecimal(row["FaktycznyPrzychód"]) : 0;
                decimal zam = row["Zamówienia"] != DBNull.Value ? Convert.ToDecimal(row["Zamówienia"]) : 0;
                decimal bilans = row["Bilans"] != DBNull.Value ? Convert.ToDecimal(row["Bilans"]) : 0;
                string stanText = row["Stan"]?.ToString() ?? "0";

                // Podstawa do obliczeń - fakt jeśli > 0, inaczej plan
                decimal podstawa = fakt > 0 ? fakt : plan;
                if (podstawa <= 0 && bilans == 0) continue;

                // Oblicz procent sprzedaży (ile zostało do sprzedania)
                decimal procentDostepnosci = podstawa > 0 ? (bilans / podstawa) * 100m : 0;
                procentDostepnosci = Math.Max(0, Math.Min(procentDostepnosci, 100));

                // Szerokość paska (0-190 px)
                double szerokoscPaska = Math.Max(0, Math.Min(190, (double)(procentDostepnosci * 190m / 100m)));

                // Ustal kolory
                Brush kolorRamki, kolorPaska;
                Color kolorTla = Colors.White;

                if (bilans > 0)
                {
                    // Dostępne - zielony
                    kolorRamki = new SolidColorBrush(Color.FromRgb(39, 174, 96));    // #27AE60
                    kolorPaska = new SolidColorBrush(Color.FromRgb(144, 238, 144));  // Jasno zielony
                }
                else
                {
                    // Brak / ujemny - czerwony
                    kolorRamki = new SolidColorBrush(Color.FromRgb(231, 76, 60));    // #E74C3C
                    kolorPaska = new SolidColorBrush(Color.FromRgb(231, 76, 60));
                    kolorTla = Color.FromRgb(255, 235, 235);
                }

                // Wyczyść nazwę z ikon
                string czystaNazwa = nazwa
                    .Replace("▶", "").Replace("▼", "")
                    .Replace("└", "").Replace("🍗", "").Replace("🍖", "").Replace("🥩", "").Replace("🐔", "")
                    .Trim();

                // Ustal kolejność: Kurczak A = 0, elementy = 1
                int kolejnosc = 1;
                if (czystaNazwa.Contains("Kurczak A")) kolejnosc = 0;

                produkty.Add(new DostepnoscProduktuModel
                {
                    Nazwa = czystaNazwa,
                    KolorRamki = kolorRamki,
                    KolorTla = kolorTla,
                    KolorPaska = kolorPaska,
                    SzerokoscPaska = szerokoscPaska,
                    BilansText = $"{bilans:N0} kg",
                    PlanFaktText = fakt > 0 ? $"{fakt:N0}" : $"{plan:N0}",
                    StanText = stanText,
                    ZamowioneText = $"{zam:N0} kg",
                    ProcentText = $"{procentDostepnosci:N0}%",
                    Kolejnosc = kolejnosc,
                    Bilans = bilans
                });
            }

            // Sortuj: Kurczak A pierwszy, potem elementy (czerwone najpierw)
            produkty = produkty
                .OrderBy(p => p.Kolejnosc)
                .ThenBy(p => p.Bilans > 0 ? 1 : 0)  // Czerwone (brak) najpierw
                .ThenBy(p => p.Nazwa)
                .ToList();

            icDostepnoscProduktow.ItemsSource = produkty;
        }

        #endregion

        #region Statystyki Anulowanych Zamówień

        private readonly DataTable _dtStatystyki = new();
        private readonly DataTable _dtStatystykiPrzyczyny = new();

        private void InitializeStatystykiTab()
        {
            // Inicjalizacja dat
            if (dpStatystykiOd.SelectedDate == null)
                dpStatystykiOd.SelectedDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            if (dpStatystykiDo.SelectedDate == null)
                dpStatystykiDo.SelectedDate = DateTime.Now;

            // Wypełnij ComboBox handlowców
            var handlowiecList = new List<string> { "(Wszystkie)" };
            handlowiecList.AddRange(_salesmenCache.OrderBy(x => x));
            cmbStatystykiHandlowiec.ItemsSource = handlowiecList;
            cmbStatystykiHandlowiec.SelectedIndex = 0;

            // Inicjalizacja struktury DataTable dla odbiorców
            if (_dtStatystyki.Columns.Count == 0)
            {
                _dtStatystyki.Columns.Add("Odbiorca", typeof(string));
                _dtStatystyki.Columns.Add("Handlowiec", typeof(string));
                _dtStatystyki.Columns.Add("LiczbaAnulowanych", typeof(int));
                _dtStatystyki.Columns.Add("SumaKg", typeof(decimal));
                _dtStatystyki.Columns.Add("OstatniaData", typeof(DateTime));
            }

            // Inicjalizacja struktury DataTable dla przyczyn
            if (_dtStatystykiPrzyczyny.Columns.Count == 0)
            {
                _dtStatystykiPrzyczyny.Columns.Add("Przyczyna", typeof(string));
                _dtStatystykiPrzyczyny.Columns.Add("Liczba", typeof(int));
                _dtStatystykiPrzyczyny.Columns.Add("Procent", typeof(string));
            }

            SetupStatystykiDataGrid();
            SetupStatystykiPrzyczynyDataGrid();
        }

        private void SetupStatystykiDataGrid()
        {
            dgStatystykiAnulowane.ItemsSource = _dtStatystyki.DefaultView;
            dgStatystykiAnulowane.Columns.Clear();

            dgStatystykiAnulowane.Columns.Add(new DataGridTextColumn
            {
                Header = "Odbiorca",
                Binding = new System.Windows.Data.Binding("Odbiorca"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = 200
            });

            dgStatystykiAnulowane.Columns.Add(new DataGridTextColumn
            {
                Header = "Handlowiec",
                Binding = new System.Windows.Data.Binding("Handlowiec"),
                Width = new DataGridLength(100),
                ElementStyle = (Style)FindResource("BoldCellStyle")
            });

            dgStatystykiAnulowane.Columns.Add(new DataGridTextColumn
            {
                Header = "Liczba anulowanych",
                Binding = new System.Windows.Data.Binding("LiczbaAnulowanych"),
                Width = new DataGridLength(130),
                ElementStyle = (Style)FindResource("CenterAlignedCellStyle")
            });

            var sumStyle = new Style(typeof(TextBlock));
            sumStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            sumStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));

            dgStatystykiAnulowane.Columns.Add(new DataGridTextColumn
            {
                Header = "Suma kg",
                Binding = new System.Windows.Data.Binding("SumaKg") { StringFormat = "N0" },
                Width = new DataGridLength(100),
                ElementStyle = sumStyle
            });

            dgStatystykiAnulowane.Columns.Add(new DataGridTextColumn
            {
                Header = "Ostatnia anulacja",
                Binding = new System.Windows.Data.Binding("OstatniaData") { StringFormat = "yyyy-MM-dd" },
                Width = new DataGridLength(130),
                ElementStyle = (Style)FindResource("CenterAlignedCellStyle")
            });
        }

        private void SetupStatystykiPrzyczynyDataGrid()
        {
            dgStatystykiPrzyczyny.ItemsSource = _dtStatystykiPrzyczyny.DefaultView;
            dgStatystykiPrzyczyny.Columns.Clear();

            dgStatystykiPrzyczyny.Columns.Add(new DataGridTextColumn
            {
                Header = "Przyczyna",
                Binding = new System.Windows.Data.Binding("Przyczyna"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = 150
            });

            var countStyle = new Style(typeof(TextBlock));
            countStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            countStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Bold));

            dgStatystykiPrzyczyny.Columns.Add(new DataGridTextColumn
            {
                Header = "Liczba",
                Binding = new System.Windows.Data.Binding("Liczba"),
                Width = new DataGridLength(70),
                ElementStyle = countStyle
            });

            var percentStyle = new Style(typeof(TextBlock));
            percentStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            percentStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(155, 89, 182))));
            percentStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));

            dgStatystykiPrzyczyny.Columns.Add(new DataGridTextColumn
            {
                Header = "%",
                Binding = new System.Windows.Data.Binding("Procent"),
                Width = new DataGridLength(60),
                ElementStyle = percentStyle
            });
        }

        private async Task LoadStatystykiAsync()
        {
            _dtStatystyki.Rows.Clear();
            _dtStatystykiPrzyczyny.Rows.Clear();

            DateTime? dataOd = dpStatystykiOd.SelectedDate;
            DateTime? dataDo = dpStatystykiDo.SelectedDate;
            string handlowiec = cmbStatystykiHandlowiec?.SelectedItem?.ToString() ?? "(Wszystkie)";

            if (!dataOd.HasValue || !dataDo.HasValue) return;

            try
            {
                // Pobierz kontrahentów
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

                // Pobierz anulowane zamówienia w wybranym okresie
                var statystyki = new Dictionary<int, (int Count, decimal SumaKg, DateTime LastDate)>();
                var statystykiPrzyczyny = new Dictionary<string, int>();

                await using (var cnLibra = new SqlConnection(_connLibra))
                {
                    await cnLibra.OpenAsync();

                    // Statystyki per odbiorca
                    string sql = @"
                        SELECT zm.KlientId, COUNT(*) as Cnt, SUM(ISNULL(zmt.IloscSuma, 0)) as Suma,
                               MAX(COALESCE(zm.DataAnulowania, zm.DataUboju, zm.DataZamowienia)) as LastDate
                        FROM [dbo].[ZamowieniaMieso] zm
                        LEFT JOIN (
                            SELECT ZamowienieId, SUM(Ilosc) as IloscSuma
                            FROM [dbo].[ZamowieniaMiesoTowar]
                            GROUP BY ZamowienieId
                        ) zmt ON zm.Id = zmt.ZamowienieId
                        WHERE zm.Status = 'Anulowane'
                          AND COALESCE(zm.DataAnulowania, zm.DataUboju, zm.DataZamowienia) >= @DataOd
                          AND COALESCE(zm.DataAnulowania, zm.DataUboju, zm.DataZamowienia) <= @DataDo
                        GROUP BY zm.KlientId";

                    await using var cmd = new SqlCommand(sql, cnLibra);
                    cmd.Parameters.AddWithValue("@DataOd", dataOd.Value.Date);
                    cmd.Parameters.AddWithValue("@DataDo", dataDo.Value.Date);

                    await using var reader = await cmd.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        if (reader.IsDBNull(0)) continue;
                        int clientId = reader.GetInt32(0);
                        int count = reader.GetInt32(1);
                        decimal suma = reader.IsDBNull(2) ? 0m : Convert.ToDecimal(reader.GetValue(2));
                        DateTime lastDate = reader.IsDBNull(3) ? DateTime.MinValue : reader.GetDateTime(3);

                        statystyki[clientId] = (count, suma, lastDate);
                    }

                    // Statystyki przyczyn anulowania
                    string sqlPrzyczyny = @"
                        SELECT ISNULL(PrzyczynaAnulowania, 'Brak przyczyny') as Przyczyna, COUNT(*) as Cnt
                        FROM [dbo].[ZamowieniaMieso]
                        WHERE Status = 'Anulowane'
                          AND COALESCE(DataAnulowania, DataUboju, DataZamowienia) >= @DataOd
                          AND COALESCE(DataAnulowania, DataUboju, DataZamowienia) <= @DataDo
                        GROUP BY ISNULL(PrzyczynaAnulowania, 'Brak przyczyny')
                        ORDER BY Cnt DESC";

                    await using var cmdPrzyczyny = new SqlCommand(sqlPrzyczyny, cnLibra);
                    cmdPrzyczyny.Parameters.AddWithValue("@DataOd", dataOd.Value.Date);
                    cmdPrzyczyny.Parameters.AddWithValue("@DataDo", dataDo.Value.Date);

                    await using var readerPrzyczyny = await cmdPrzyczyny.ExecuteReaderAsync();

                    while (await readerPrzyczyny.ReadAsync())
                    {
                        string przyczyna = readerPrzyczyny.GetString(0);
                        int count = readerPrzyczyny.GetInt32(1);
                        statystykiPrzyczyny[przyczyna] = count;
                    }
                }

                // Uzupełnij tabelę odbiorców
                int totalAnulowane = 0;
                foreach (var kvp in statystyki)
                {
                    int clientId = kvp.Key;
                    var (count, suma, lastDate) = kvp.Value;

                    var (name, salesman) = contractors.TryGetValue(clientId, out var c) ? c : ($"Nieznany ({clientId})", "");

                    // Filtruj po handlowcu jeśli wybrano
                    if (handlowiec != "(Wszystkie)" && salesman != handlowiec)
                        continue;

                    _dtStatystyki.Rows.Add(name, salesman, count, suma, lastDate);
                    totalAnulowane += count;
                }

                // Sortuj po liczbie anulowanych malejąco
                _dtStatystyki.DefaultView.Sort = "LiczbaAnulowanych DESC";

                // Uzupełnij tabelę przyczyn
                int totalPrzyczyny = statystykiPrzyczyny.Values.Sum();
                foreach (var kvp in statystykiPrzyczyny.OrderByDescending(x => x.Value))
                {
                    string przyczyna = kvp.Key;
                    int count = kvp.Value;
                    double procent = totalPrzyczyny > 0 ? (double)count / totalPrzyczyny * 100 : 0;
                    _dtStatystykiPrzyczyny.Rows.Add(przyczyna, count, $"{procent:F1}%");
                }

                // Aktualizuj podsumowanie
                txtStatystykiLacznieAnulowanych.Text = totalAnulowane.ToString("N0");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania statystyk: {ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CmbStatystykiOkres_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Sprawdź czy kontrolki są już zainicjalizowane (event może być wywołany podczas ładowania XAML)
            if (dpStatystykiOd == null || dpStatystykiDo == null) return;

            if (cmbStatystykiOkres.SelectedItem is ComboBoxItem item && item.Tag is string period)
            {
                DateTime today = DateTime.Today;
                switch (period)
                {
                    case "Year":
                        dpStatystykiOd.SelectedDate = new DateTime(today.Year, 1, 1);
                        dpStatystykiDo.SelectedDate = today;
                        break;
                    case "Month":
                        dpStatystykiOd.SelectedDate = new DateTime(today.Year, today.Month, 1);
                        dpStatystykiDo.SelectedDate = today;
                        break;
                    case "Week":
                        int delta = ((int)today.DayOfWeek + 6) % 7;
                        dpStatystykiOd.SelectedDate = today.AddDays(-delta);
                        dpStatystykiDo.SelectedDate = today;
                        break;
                    case "Day":
                        dpStatystykiOd.SelectedDate = today;
                        dpStatystykiDo.SelectedDate = today;
                        break;
                }
            }
        }

        private void DpStatystyki_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            // Nie rób nic automatycznie - użytkownik kliknie Odśwież
        }

        private void CmbStatystykiHandlowiec_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Nie rób nic automatycznie - użytkownik kliknie Odśwież
        }

        private async void BtnStatystykiOdswiez_Click(object sender, RoutedEventArgs e)
        {
            await LoadStatystykiAsync();
        }

        #endregion
    }

    // Model do wyświetlania dostępności produktów na dashboardzie
    public class DostepnoscProduktuModel
    {
        public string Nazwa { get; set; } = "";
        public Brush KolorRamki { get; set; } = Brushes.Gray;
        public Color KolorTla { get; set; } = Colors.White;
        public Brush KolorPaska { get; set; } = Brushes.Gray;
        public double SzerokoscPaska { get; set; }
        public string BilansText { get; set; } = "";
        public string PlanFaktText { get; set; } = "";
        public string StanText { get; set; } = "";
        public string ZamowioneText { get; set; } = "";
        public string ProcentText { get; set; } = "";
        public int Kolejnosc { get; set; }
        public decimal Bilans { get; set; }
    }
}
