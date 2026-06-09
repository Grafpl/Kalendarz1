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
using System.Windows.Shapes;
using Kalendarz1;
using Kalendarz1.Services;


namespace Kalendarz1.WPF
{
    public partial class MainWindow : Window
    {
        private readonly string _connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly string _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private readonly string _connTransport = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        // Pula bilansu: rodzic (towar dashboardu) → dzieci (towary doliczane do puli). Patrz KreatorPuliBilansuWindow.
        private readonly Kalendarz1.Zamowienia.Services.BilansSkladnikiService _bilansSkladnikiService;

        // Nazwa kontrahenta wybranego zamówienia — pokazywana w nagłówku kolumny "Produkt" w szczegółach
        private string _detailsClientName = "";

        // Kurczak A: stan rozwinięcia tabelki klas wagowych (przeżywa odświeżenia panelu) + referencja karty
        private bool _kurczakAExpanded = false;
        private Border _kurczakACard;

        // Zamówienia "Ogólne" zgrupowane w jeden wiersz (▶/▼) + indeks kolumny Utworzono (avatar loader)
        private bool _ogolneExpanded = false;
        private int _utworzonoColIndex = -1;
        private readonly List<object[]> _ogolneDetale = new(); // cache detali grupy — toggle bez rundy do bazy

        // Kolumny Wyd./Róż. w szczegółach — kurczone/ukrywane przy wąskim oknie
        private DataGridColumn _colWydDet, _colRozDet;

        // Cache mapy puli matka/córka (zmienia się rzadko — TTL 5 min zamiast 2 zapytań SQL na każde odświeżenie)
        private Dictionary<int, List<int>> _puleCacheMap;
        private DateTime _puleCacheTime = DateTime.MinValue;

        private static readonly DateTime MinSqlDate = new DateTime(1753, 1, 1);
        private static readonly DateTime MaxSqlDate = new DateTime(9999, 12, 31);
        private static bool _strefaColumnExists = false;
        private static bool _wariantColumnExists = false;
        private static Dictionary<int, string> _wariantNazwy = new(); // Kod wariantu → nazwa (do wyświetlenia)

        // Avatary handlowców (identycznie jak w HandlowiecDashboardWindow)
        private static Dictionary<string, BitmapSource> _handlowiecAvatarCache = new(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, string> _handlowiecMapowanie;

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

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

        // ── STATIC CACHE: schema check (kolumny są permanentne, nie zmieniają się — sprawdź RAZ na proces) ──
        private static volatile bool _staticSchemaChecked;
        private static bool _staticSlaughterDateExists, _staticWalutaExists, _staticStrefaExists;
        private static bool _staticTransportKursIDExists, _staticStatusColumnsExist, _staticAnulowanieColumnsExist;
        private static readonly object _staticSchemaLock = new();

        // ── STATIC CACHE: catalog produktów / users / salesmen (TTL 5 min, shared między oknami) ──
        private static Dictionary<int, string> _staticProductCodeCache = new();
        private static Dictionary<int, string> _staticProductCatalogCache = new();
        private static Dictionary<int, string> _staticProductCatalogSwieze = new();
        private static Dictionary<int, string> _staticProductCatalogMrozone = new();
        private static Dictionary<string, string> _staticUserCache = new();
        private static List<string> _staticSalesmenCache = new();
        private static DateTime _staticCatalogCacheTime = DateTime.MinValue;
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
        private GridLength _savedRightColumnWidth = new GridLength(750);
        private bool _rightPanelHidden = false; // Flaga czy prawy panel został ukryty
        private readonly Dictionary<int, string> _productCodeCache = new();
        private readonly Dictionary<int, string> _productCatalogCache = new();
        private readonly Dictionary<int, string> _productCatalogSwieze = new();
        private readonly Dictionary<int, string> _productCatalogMrozone = new();
        private readonly Dictionary<int, BitmapImage?> _productImages = new();
        private Dictionary<int, string> _mapowanieScalowania = new(); // TowarIdtw -> NazwaGrupy

        // ✅ CACHE dla wydań - unikamy duplikacji zapytań
        private Dictionary<int, decimal> _cachedWydaniaSum = new();
        private DateTime _cachedWydaniaDate = DateTime.MinValue;

        // ✅ CACHE dla przychodów - unikamy duplikacji zapytań
        private Dictionary<int, decimal> _cachedPrzychodyTuszkaA = new();
        private Dictionary<int, decimal> _cachedPrzychodyElementy = new();
        private DateTime _cachedPrzychodyDate = DateTime.MinValue;

        // ✅ STATIC CACHE dla kontrahentów - shared między oknami, TTL 5 min
        // (instance field = każde nowe okno ponownie ładuje tysiące kontrahentów; static = 1× per 5 min na proces)
        private static Dictionary<int, (string Name, string Salesman)> _cachedKontrahenci = new();
        private static DateTime _cachedKontrahenciTime = DateTime.MinValue;

        // ✅ STATIC CACHE kategorii odbiorców - TTL 30 min (kategorie zmieniają się rzadko)
        private static Dictionary<int, string> _kategorieOdbiorcow = new();
        private static DateTime _kategorieOdbiorcowTime = DateTime.MinValue;

        // ✅ CACHE dla konfiguracji produktów - wywoływana 2x w jednym cyklu odświeżania
        private Dictionary<int, decimal> _cachedKonfiguracjaProduktow = new();
        private DateTime _cachedKonfiguracjaProduktowDate = DateTime.MinValue;

        private Dictionary<string, List<int>> _grupyDoProduktow = new(); // NazwaGrupy -> lista TowarId

        // Prognoza klas wagowych z HarmonogramDostaw (indeksy 5-12)
        private int[] _pojemnikiKlasyPrognoza = new int[13];
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
            WindowIconHelper.SetIcon(this);
            _bilansSkladnikiService = new Kalendarz1.Zamowienia.Services.BilansSkladnikiService(_connLibra);
            wpProductCards.SizeChanged += (s, e) => ApplyCardLayout(); // responsywne karty Podsumowania dnia
            SizeChanged += (s, e) => ApplyOrdersLayout();              // responsywna tabela zamówień (progi szerokości okna)
            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            // Zatrzymaj timer auto-refresh
            _autoRefreshTimer?.Stop();

            // Wyczyść statyczne cache obrazków (memory leak prevention)
            _productImages.Clear();
            _handlowiecAvatarCache.Clear();

            // Wyczyść cache danych
            _cachedWydaniaSum.Clear();
            _cachedPrzychodyTuszkaA.Clear();
            _cachedPrzychodyElementy.Clear();
            _cachedKontrahenci.Clear();
            _kategorieOdbiorcow.Clear();
            _cachedKonfiguracjaProduktow.Clear();
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
                const double DEFAULT_DETAILS_FONT = 15;
                const double DEFAULT_DETAILS_ROW = 34;

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
                        dgDetails.FontSize = 13;
                        dgDetails.RowHeight = Math.Max(22, Math.Min(detailsOptimalHeight, 26));
                    }
                    else if (detailsRowCount > 5 || detailsOptimalHeight < 26)
                    {
                        dgDetails.FontSize = 14;
                        dgDetails.RowHeight = Math.Max(26, Math.Min(detailsOptimalHeight, 30));
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
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isInitialized) return;

            // ✅ Ustaw datę PRZED LoadInitialDataAsync, bo RadioButton.Checked może
            // uruchomić RefreshAllDataAsync zanim _selectedDate zostanie ustawiony
            _selectedDate = ValidateSqlDate(DateTime.Today);

            SetupDayButtons();
            cbiUsun.Visibility = (UserID == "11111") ? Visibility.Visible : Visibility.Collapsed;
            chkShowReleasesWithoutOrders.IsChecked = _showReleasesWithoutOrders;
            chkShowAnulowane.IsChecked = false;

            // _isInitialized = false → event handlery (RbDateFilter_Checked itp.)
            // nie będą uruchamiać RefreshAllDataAsync podczas inicjalizacji
            await LoadInitialDataAsync();
            InicjalizujMenuKontekstoweBilansu();

            // Teraz ustaw _isInitialized DOPIERO PO LoadInitialDataAsync
            _isInitialized = true;

            UpdateDayButtonDates();
            await RefreshAllDataAsync(); // To utworzy DataGridy

            // ✅ WYWOŁAJ PO RefreshAllDataAsync, gdy wszystkie kontrolki są już utworzone
            ApplyResponsiveLayout();


            // Watermark dla pola wyszukiwania produktów
            txtProductSearch.GotFocus += (s, ev) => { if (string.IsNullOrEmpty(txtProductSearch.Text)) txtSearchWatermark.Visibility = Visibility.Collapsed; };
            txtProductSearch.LostFocus += (s, ev) => { if (string.IsNullOrEmpty(txtProductSearch.Text)) txtSearchWatermark.Visibility = Visibility.Visible; };

            // Auto-odświeżanie co 3 minuty
            _autoRefreshTimer = new System.Windows.Threading.DispatcherTimer();
            _autoRefreshTimer.Interval = TimeSpan.FromMinutes(3);
            _autoRefreshTimer.Tick += AutoRefreshTimer_Tick;
            _autoRefreshTimer.Start();

            // PERFORMANCE: Debouncing timer dla filtrów
            _filterDebounceTimer = new System.Windows.Threading.DispatcherTimer();
            _filterDebounceTimer.Interval = TimeSpan.FromMilliseconds(FILTER_DEBOUNCE_MS);
            _filterDebounceTimer.Tick += FilterDebounceTimer_Tick;

            // Skróty klawiaturowe
            this.KeyDown += MainWindow_KeyDown;
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;

            // Domyślny fokus na okno (nie otwieraj ComboBox na starcie)
            this.Focus();

            // Obsługa edycji notatek - zapisz przy utracie fokusa
            txtNotes.LostFocus += TxtNotes_LostFocus;

            // ✅ PRELOAD: Załaduj dane dla sąsiednich dat w tle (przyspiesza przełączanie)
            _ = PreloadAdjacentDatesAsync(_selectedDate);
        }

        /// <summary>
        /// Preload danych dla sąsiednich dat (poprzedni i następny dzień) w tle
        /// </summary>
        private async Task PreloadAdjacentDatesAsync(DateTime centerDate)
        {
            await Task.Delay(500); // Poczekaj aż główne UI się załaduje

            var datesToPreload = new[]
            {
                centerDate.AddDays(-1),
                centerDate.AddDays(1)
            };

            foreach (var date in datesToPreload)
            {
                try
                {
                    // Preload wydań i przychodów dla tej daty
                    await PreloadDataForDateAsync(date);
                }
                catch { /* Ignoruj błędy preloadu */ }
            }
        }

        /// <summary>
        /// Preload wydań i przychodów dla konkretnej daty (bez wyświetlania)
        /// </summary>
        private async Task PreloadDataForDateAsync(DateTime date)
        {
            date = ValidateSqlDate(date);

            // Preload wydań (jeśli nie ma w cache)
            if (_cachedWydaniaDate != date.Date)
            {
                var wydaniaTask = Task.Run(async () =>
                {
                    var result = new Dictionary<int, decimal>();
                    await using var cn = new SqlConnection(_connHandel);
                    await cn.OpenAsync();
                    const string sql = @"SELECT MZ.idtw, SUM(ABS(MZ.ilosc))
                        FROM [HANDEL].[HM].[MZ] MZ
                        JOIN [HANDEL].[HM].[MG] ON MZ.super = MG.id
                        WHERE MG.seria IN ('sWZ','sWZ-W') AND MG.aktywny=1 AND MG.data = @Day
                        GROUP BY MZ.idtw";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Day", date.Date);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        int productId = reader.GetInt32(0);
                        decimal qty = reader.IsDBNull(1) ? 0m : Convert.ToDecimal(reader.GetValue(1));
                        result[productId] = qty;
                    }
                    return result;
                });

                // Preload przychodów równolegle
                var przychodyTask = Task.Run(async () =>
                {
                    var tuszkaA = new Dictionary<int, decimal>();
                    var elementy = new Dictionary<int, decimal>();
                    await using var cn = new SqlConnection(_connHandel);
                    await cn.OpenAsync();
                    const string sql = @"
                        SELECT MZ.idtw, SUM(ABS(MZ.ilosc)) AS Ilosc,
                               CASE WHEN MG.seria = 'sPWU' THEN 'T' ELSE 'E' END AS Typ
                        FROM [HANDEL].[HM].[MZ] MZ
                        JOIN [HANDEL].[HM].[MG] ON MZ.super = MG.id
                        WHERE MG.seria IN ('sPWU', 'sPWP', 'PWP') AND MG.aktywny=1 AND MG.data = @Day
                        GROUP BY MZ.idtw, CASE WHEN MG.seria = 'sPWU' THEN 'T' ELSE 'E' END";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Day", date.Date);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        int productId = rdr.GetInt32(0);
                        decimal qty = rdr.IsDBNull(1) ? 0m : Convert.ToDecimal(rdr.GetValue(1));
                        string typ = rdr.GetString(2);
                        if (typ == "T") tuszkaA[productId] = qty;
                        else elementy[productId] = qty;
                    }
                    return (tuszkaA, elementy);
                });

                await Task.WhenAll(wydaniaTask, przychodyTask);
                // Nie zapisujemy do głównego cache - to tylko preload
                // Cache zostanie zaktualizowany gdy użytkownik przełączy się na tę datę
            }
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
            // Ctrl+F - Fokus na pole wyszukiwania produktów
            if (e.Key == System.Windows.Input.Key.F &&
                (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
            {
                txtProductSearch.Focus();
                txtProductSearch.SelectAll();
                e.Handled = true;
            }
            // Escape - Wyczyść filtr produktu (resetuj na "Wszystkie")
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                if (!string.IsNullOrEmpty(txtProductSearch.Text))
                {
                    txtProductSearch.Text = "";
                    e.Handled = true;
                }
                else if (cbProductFilter.SelectedIndex > 0)
                {
                    cbProductFilter.SelectedIndex = 0;
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
            // Ctrl+Shift+D - Włącz diagnostykę czasów ładowania
            else if (e.Key == System.Windows.Input.Key.D &&
                (System.Windows.Input.Keyboard.Modifiers & (System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift))
                == (System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift))
            {
                _showLoadingDiagnostics = true;
                MessageBox.Show("Diagnostyka włączona!\n\nTeraz zmień datę lub odśwież (F5),\naby zobaczyć szczegółowe czasy ładowania.",
                    "Diagnostyka", MessageBoxButton.OK, MessageBoxImage.Information);
                e.Handled = true;
            }
            // F5 - Odśwież dane
            else if (e.Key == System.Windows.Input.Key.F5)
            {
                _ = RefreshAllDataAsync();
                e.Handled = true;
            }
        }

        private void FilterDebounceTimer_Tick(object sender, EventArgs e)
        {
            _filterDebounceTimer.Stop();
            ApplyFilters();
        }

        private async void AutoRefreshTimer_Tick(object? sender, EventArgs e)
        {
            // Pomiń jeśli już trwa odświeżanie
            if (_isRefreshing) return;
            _autoRefreshTimer.Stop();
            try
            {
                await RefreshAllDataAsync();
            }
            finally
            {
                _autoRefreshTimer.Start();
            }
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await RefreshAllDataAsync();
        }

        // Diagnostyka czasów usunięta - przycisk już nie istnieje

        #region Przyciski otwierania osobnych okien

        // Combobox "⋯ Więcej" — Dashboard / Statystyki / Transport / Usuń pod jednym kontrolką
        private void CbAkcje_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbAkcje.SelectedItem is not ComboBoxItem item) return;
            var tag = item.Tag as string;
            if (string.IsNullOrEmpty(tag)) return;

            cbAkcje.SelectedIndex = 0; // reset do nagłówka (wywoła ten handler ponownie, ale tag == null)

            switch (tag)
            {
                case "dash": BtnOpenDashboard_Click(sender, e); break;
                case "stat": BtnOpenStatystyki_Click(sender, e); break;
                case "tran": BtnOpenTransport_Click(sender, e); break;
                case "usun": BtnDelete_Click(sender, e); break;
            }
        }

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
            // ✅ CACHE: zwróć natychmiast jeśli ta sama data
            if (_cachedKonfiguracjaProduktowDate == data.Date && _cachedKonfiguracjaProduktow.Count > 0)
            {
                return _cachedKonfiguracjaProduktow;
            }

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

                // ✅ Zapisz do cache
                _cachedKonfiguracjaProduktow = result;
                _cachedKonfiguracjaProduktowDate = data.Date;

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

            // Najpierw przyciski dni tygodnia - mniejsze kafelki
            for (int i = 0; i < 7; i++)
            {
                var btn = new Button
                {
                    Style = (Style)FindResource("DayButtonStyle"),
                    ClickMode = ClickMode.Press  // Reaguje natychmiast na naciśnięcie
                };

                var stack = new StackPanel();
                stack.Children.Add(new TextBlock { Text = dayNames[i], FontSize = 8, FontWeight = FontWeights.Bold });
                stack.Children.Add(new TextBlock { Text = DateTime.Today.AddDays(i).ToString("dd.MM"), FontSize = 8 });
                btn.Content = stack;

                // Natychmiastowa reakcja na naciśnięcie myszy
                btn.PreviewMouseLeftButtonDown += DayButton_MouseDown;
                _dayButtonDates[btn] = DateTime.Today.AddDays(i);

                _dayButtons.Add(btn);
                panelDays.Children.Add(btn);
            }

            // Separator przed przyciskiem "Dziś"
            var separator = new System.Windows.Controls.Separator
            {
                Width = 2,
                Margin = new Thickness(5, 0, 5, 0),
                Background = new SolidColorBrush(Color.FromRgb(229, 231, 235))
            };
            panelDays.Children.Add(separator);

            // Przycisk "Dziś" — ŻÓŁTY, na końcu (obok niedzieli)
            btnToday = new Button
            {
                Style = (Style)FindResource("DayButtonStyle"),
                ClickMode = ClickMode.Press,
                Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xD5, 0x4F)),       // żółty
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0xB7, 0x00)),
                Foreground = new SolidColorBrush(Color.FromRgb(0x5A, 0x42, 0x00))
            };

            var stackToday = new StackPanel();
            stackToday.Children.Add(new TextBlock
            {
                Text = "Dziś",
                FontSize = 8,
                FontWeight = FontWeights.Bold
            });
            stackToday.Children.Add(new TextBlock
            {
                Text = DateTime.Today.ToString("dd.MM"),
                FontSize = 8
            });
            btnToday.Content = stackToday;
            btnToday.Click += async (s, e) =>
            {
                _selectedDate = DateTime.Today;
                UpdateDayButtonDates();
                await RefreshAllDataAsync();
            };

            panelDays.Children.Add(btnToday);
        }
        private async Task LoadInitialDataAsync()
        {
            // ✅ SCHEMA CHECK: 1 zapytanie + static cache (sprawdzane RAZ na proces — kolumny są permanentne)
            // Stare 7 zapytań było robione przy każdym otwarciu okna (7 połączeń × ~30ms = ~200ms tylko na sprawdzenia)
            await Task.WhenAll(
                EnsureSchemaCheckedFastAsync(),
                EnsureHandlowiecMappingLoadedAsync()
            );

            // ✅ STATIC CACHE TTL 5 min: produkty/users/salesmen rzadko się zmieniają — shared między oknami
            // Pierwsza inicjalizacja: 3 SQL równolegle. Kolejne otwarcia okna w ciągu 5 min: 0 SQL, instant copy z static.
            _productCodeCache.Clear();
            _productCatalogCache.Clear();
            _productCatalogSwieze.Clear();
            _productCatalogMrozone.Clear();
            _userCache.Clear();
            _salesmenCache.Clear();

            bool cacheValid = (DateTime.Now - _staticCatalogCacheTime).TotalMinutes < 5
                              && _staticProductCodeCache.Count > 0;

            if (cacheValid)
            {
                // Instant copy z static cache
                foreach (var kvp in _staticProductCodeCache) _productCodeCache[kvp.Key] = kvp.Value;
                foreach (var kvp in _staticProductCatalogCache) _productCatalogCache[kvp.Key] = kvp.Value;
                foreach (var kvp in _staticProductCatalogSwieze) _productCatalogSwieze[kvp.Key] = kvp.Value;
                foreach (var kvp in _staticProductCatalogMrozone) _productCatalogMrozone[kvp.Key] = kvp.Value;
                foreach (var kvp in _staticUserCache) _userCache[kvp.Key] = kvp.Value;
                _salesmenCache.AddRange(_staticSalesmenCache);
            }
            else
            {
                var taskProdukty = Task.Run(async () =>
                {
                    await using var cn = new SqlConnection(_connHandel);
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
                            if (katObj is int ki) katalog = ki;
                            else int.TryParse(Convert.ToString(katObj), out katalog);
                            if (katalog == 67095) { _productCatalogSwieze[idtw] = kod; _productCatalogCache[idtw] = kod; }
                            else if (katalog == 67153) { _productCatalogMrozone[idtw] = kod; _productCatalogCache[idtw] = kod; }
                        }
                    }
                });

                var taskUsers = Task.Run(async () =>
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();
                    await using var cmd = new SqlCommand("SELECT ID, Name FROM dbo.operators", cn);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var idStr = reader.IsDBNull(0) ? "" : reader.GetString(0);
                        var name = reader.IsDBNull(1) ? "" : reader.GetString(1);
                        if (!string.IsNullOrEmpty(idStr)) _userCache[idStr] = name;
                    }
                });

                var taskSalesmen = Task.Run(async () =>
                {
                    await using var cn = new SqlConnection(_connHandel);
                    await cn.OpenAsync();
                    await using var cmd = new SqlCommand(
                        @"SELECT DISTINCT CDim_Handlowiec_Val
                          FROM [HANDEL].[SSCommon].[ContractorClassification]
                          WHERE CDim_Handlowiec_Val IS NOT NULL
                          ORDER BY 1", cn);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var val = reader.IsDBNull(0) ? "" : reader.GetString(0);
                        if (!string.IsNullOrWhiteSpace(val)) _salesmenCache.Add(val);
                    }
                });

                await Task.WhenAll(taskProdukty, taskUsers, taskSalesmen);

                // Zapisz do static cache (shared między oknami przez 5 min)
                _staticProductCodeCache = new Dictionary<int, string>(_productCodeCache);
                _staticProductCatalogCache = new Dictionary<int, string>(_productCatalogCache);
                _staticProductCatalogSwieze = new Dictionary<int, string>(_productCatalogSwieze);
                _staticProductCatalogMrozone = new Dictionary<int, string>(_productCatalogMrozone);
                _staticUserCache = new Dictionary<string, string>(_userCache);
                _staticSalesmenCache = new List<string>(_salesmenCache);
                _staticCatalogCacheTime = DateTime.Now;
            }

            // Zdjęcia (BLOBy) — ŁADUJEMY W TLE, nie blokujemy startu okna.
            // Bindingi mają INPC — obrazki dograne kiedy będą gotowe.
            _ = Task.Run(async () => await LoadProductImagesAsync());

            GenerateProductButtons();

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

        // ✅ FAST schema check — 1 SQL dla wszystkich kolumn + static cache (raz na proces).
        // Pierwsze otwarcie okna w sesji: 1 SQL (~30ms). Każde kolejne: 0 SQL (instant z cache).
        // ALTER tylko gdy kolumna naprawdę nie istnieje (rzadka ścieżka — świeża instalacja).
        private async Task EnsureSchemaCheckedFastAsync()
        {
            if (_staticSchemaChecked)
            {
                _slaughterDateColumnExists = _staticSlaughterDateExists;
                _walutaColumnExists = _staticWalutaExists;
                _strefaColumnExists = _staticStrefaExists;
                return;
            }

            lock (_staticSchemaLock)
            {
                if (_staticSchemaChecked)
                {
                    _slaughterDateColumnExists = _staticSlaughterDateExists;
                    _walutaColumnExists = _staticWalutaExists;
                    _strefaColumnExists = _staticStrefaExists;
                    return;
                }
            }

            var existingCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                const string sqlAll = @"
                    SELECT TABLE_NAME + '.' + COLUMN_NAME AS FullName
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE (TABLE_NAME = 'ZamowieniaMieso'
                            AND COLUMN_NAME IN ('DataUboju','Waluta','TransportKursID',
                                                'CzyZrealizowane','DataRealizacji','KtoZrealizowal','CzyWydane',
                                                'AnulowanePrzez','DataAnulowania'))
                       OR (TABLE_NAME = 'ZamowieniaMiesoTowar' AND COLUMN_NAME = 'Strefa')";
                await using (var cmd = new SqlCommand(sqlAll, cn))
                await using (var rd = await cmd.ExecuteReaderAsync())
                {
                    while (await rd.ReadAsync()) existingCols.Add(rd.GetString(0));
                }

                // ALTERy — tylko brakujące. Każdy w oddzielnym SqlCommand bo SQL Server nie lubi multi-DDL w batch.
                var alters = new List<(string col, string sql)>();
                if (!existingCols.Contains("ZamowieniaMieso.DataUboju"))
                    alters.Add(("DataUboju", "ALTER TABLE [dbo].[ZamowieniaMieso] ADD DataUboju DATE NULL"));
                if (!existingCols.Contains("ZamowieniaMieso.Waluta"))
                    alters.Add(("Waluta", "ALTER TABLE [dbo].[ZamowieniaMieso] ADD Waluta NVARCHAR(10) NULL DEFAULT 'PLN'"));
                if (!existingCols.Contains("ZamowieniaMiesoTowar.Strefa"))
                    alters.Add(("Strefa", "ALTER TABLE [dbo].[ZamowieniaMiesoTowar] ADD Strefa BIT NULL DEFAULT 0"));
                if (!existingCols.Contains("ZamowieniaMieso.TransportKursID"))
                    alters.Add(("TransportKursID", "ALTER TABLE [dbo].[ZamowieniaMieso] ADD TransportKursID INT NULL"));
                if (!existingCols.Contains("ZamowieniaMieso.CzyZrealizowane"))
                    alters.Add(("CzyZrealizowane", "ALTER TABLE [dbo].[ZamowieniaMieso] ADD CzyZrealizowane BIT DEFAULT 0 NOT NULL"));
                if (!existingCols.Contains("ZamowieniaMieso.DataRealizacji"))
                    alters.Add(("DataRealizacji", "ALTER TABLE [dbo].[ZamowieniaMieso] ADD DataRealizacji DATETIME NULL"));
                if (!existingCols.Contains("ZamowieniaMieso.KtoZrealizowal"))
                    alters.Add(("KtoZrealizowal", "ALTER TABLE [dbo].[ZamowieniaMieso] ADD KtoZrealizowal INT NULL"));
                if (!existingCols.Contains("ZamowieniaMieso.CzyWydane"))
                    alters.Add(("CzyWydane", "ALTER TABLE [dbo].[ZamowieniaMieso] ADD CzyWydane BIT DEFAULT 0 NOT NULL"));
                if (!existingCols.Contains("ZamowieniaMieso.AnulowanePrzez"))
                    alters.Add(("AnulowanePrzez", "ALTER TABLE [dbo].[ZamowieniaMieso] ADD AnulowanePrzez NVARCHAR(100) NULL"));
                if (!existingCols.Contains("ZamowieniaMieso.DataAnulowania"))
                    alters.Add(("DataAnulowania", "ALTER TABLE [dbo].[ZamowieniaMieso] ADD DataAnulowania DATETIME NULL"));

                foreach (var (col, sql) in alters)
                {
                    try
                    {
                        await using var cmd = new SqlCommand(sql, cn);
                        await cmd.ExecuteNonQueryAsync();
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Schema ALTER {col}] {ex.Message}"); }
                }

                // Migracja danych — tylko gdy CzyZrealizowane/CzyWydane były świeżo dodane (rzadka ścieżka)
                bool migrateStatus = alters.Exists(a => a.col == "CzyZrealizowane" || a.col == "CzyWydane");
                if (migrateStatus)
                {
                    try
                    {
                        await using var cmdMig = new SqlCommand(@"
                            UPDATE dbo.ZamowieniaMieso SET CzyZrealizowane = 1
                            WHERE Status IN ('Zrealizowane', 'Wydany') AND CzyZrealizowane = 0;
                            UPDATE dbo.ZamowieniaMieso SET CzyWydane = 1
                            WHERE (Status = 'Wydany' OR DataWydania IS NOT NULL) AND CzyWydane = 0;", cn);
                        await cmdMig.ExecuteNonQueryAsync();
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Schema migrate] {ex.Message}"); }
                }

                // Po sukcesie — wszystkie kolumny istnieją (lub zostały utworzone)
                _staticSlaughterDateExists = true;
                _staticWalutaExists = true;
                _staticStrefaExists = true;
                _staticTransportKursIDExists = true;
                _staticStatusColumnsExist = true;
                _staticAnulowanieColumnsExist = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Schema check] {ex.Message}");
                // Fallback — pozostaw static flags w domyślnym false (graceful degradation)
            }

            // Kopiuj static → instance + ustaw flagę
            _slaughterDateColumnExists = _staticSlaughterDateExists;
            _walutaColumnExists = _staticWalutaExists;
            _strefaColumnExists = _staticStrefaExists;
            _staticSchemaChecked = true;
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

        private async Task CheckAndCreateStrefaColumnAsync()
        {
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                const string checkSql = @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                                         WHERE TABLE_NAME = 'ZamowieniaMiesoTowar' AND COLUMN_NAME = 'Strefa'";
                await using var cmdCheck = new SqlCommand(checkSql, cn);
                int count = Convert.ToInt32(await cmdCheck.ExecuteScalarAsync());

                if (count == 0)
                {
                    const string alterSql = @"ALTER TABLE [dbo].[ZamowieniaMiesoTowar] ADD Strefa BIT NULL DEFAULT 0";
                    await using var cmdAlter = new SqlCommand(alterSql, cn);
                    await cmdAlter.ExecuteNonQueryAsync();
                }

                _strefaColumnExists = true;

                // Kolumna Wariant (tworzy ją TowarWariantyService; tu tylko wykrywamy do odczytu w szczegółach)
                const string wariantCheck = @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                                              WHERE TABLE_NAME = 'ZamowieniaMiesoTowar' AND COLUMN_NAME = 'Wariant'";
                await using var cmdW = new SqlCommand(wariantCheck, cn);
                _wariantColumnExists = Convert.ToInt32(await cmdW.ExecuteScalarAsync()) > 0;
            }
            catch
            {
                _strefaColumnExists = false;
            }
        }

        /// <summary>
        /// Ładuje mapowanie HandlowiecName → UserID (identycznie jak EnsureHandlowiecMappingLoadedAsync w dashboardzie)
        /// </summary>
        private async Task EnsureHandlowiecMappingLoadedAsync()
        {
            if (_handlowiecMapowanie != null) return;
            _handlowiecMapowanie = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand("SELECT HandlowiecName, UserID FROM UserHandlowcy", cn);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                    _handlowiecMapowanie[rd.GetString(0)] = rd.GetString(1);
            }
            catch { }
        }

        /// <summary>
        /// Cache'uje avatar handlowca (identycznie jak EnsureAvatarCached w dashboardzie)
        /// </summary>
        private void EnsureHandlowiecAvatarCached(string handlowiec, int size = 64)
        {
            if (string.IsNullOrEmpty(handlowiec)) return;
            if (_handlowiecAvatarCache.ContainsKey(handlowiec)) return;
            if (_handlowiecMapowanie == null) return;

            BitmapSource avatarBmp = null;
            if (_handlowiecMapowanie.TryGetValue(handlowiec, out var uid))
            {
                try
                {
                    if (UserAvatarManager.HasAvatar(uid))
                        using (var av = UserAvatarManager.GetAvatarRounded(uid, size))
                            if (av != null) avatarBmp = ConvertToBitmapSource(av);
                    if (avatarBmp == null)
                        using (var defAv = UserAvatarManager.GenerateDefaultAvatar(handlowiec, uid, size))
                            avatarBmp = ConvertToBitmapSource(defAv);
                }
                catch { }
            }
            if (avatarBmp == null)
            {
                try
                {
                    using (var defAv = UserAvatarManager.GenerateDefaultAvatar(handlowiec, handlowiec, size))
                        avatarBmp = ConvertToBitmapSource(defAv);
                }
                catch { }
            }
            if (avatarBmp != null)
            {
                avatarBmp.Freeze();
                _handlowiecAvatarCache[handlowiec] = avatarBmp;
            }
        }

        private BitmapSource ConvertToBitmapSource(System.Drawing.Image image)
        {
            if (image == null) return null;
            using (var bitmap = new System.Drawing.Bitmap(image))
            {
                var hBitmap = bitmap.GetHbitmap();
                try
                {
                    return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                }
                finally
                {
                    DeleteObject(hBitmap);
                }
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

        // Natychmiastowa reakcja na naciśnięcie przycisku dnia
        private async void DayButton_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Button btn && _dayButtonDates.TryGetValue(btn, out DateTime date))
            {
                e.Handled = true; // Zapobiega dalszemu przetwarzaniu
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

        /// <summary>
        /// Publiczna metoda — przeskocz na dzisiejszy dzień i odśwież dane.
        /// Wywoływana z Menu.cs gdy kafelek "Zamówienia Klientów" aktywuje istniejące okno.
        /// </summary>
        public async Task JumpToTodayAsync()
        {
            if (!_isInitialized) return; // jeszcze się ładuje — Window_Loaded sam ustawi today
            if (_selectedDate.Date == DateTime.Today) return; // już na dziś
            _selectedDate = DateTime.Today;
            UpdateDayButtonDates();
            await RefreshAllDataAsync();
        }

        private void UpdateDayButtonDates()
        {
            int delta = ((int)_selectedDate.DayOfWeek + 6) % 7;
            DateTime startOfWeek = _selectedDate.AddDays(-delta);

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
            var win = new Kalendarz1.Zamowienia.Views.NoweZamowienieTestWindow(UserID, null);
            win.Owner = this;
            if (win.ShowDialog() == true)
            {
                await RefreshAllDataAsync();
            }
        }

        private async void BtnNewTest_Click(object sender, RoutedEventArgs e)
        {
            // Legacy — kieruje na to samo nowe okno
            BtnNew_Click(sender, e);
            await Task.CompletedTask;
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
            // Kompatybilność - TextBox jest teraz ukryty
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
            // Kompatybilność - radio buttons są teraz ukryte, produkty łączone w ComboBox
            if (!_isInitialized) return;
        }

        private string GetProductIcon(string productName)
        {
            var name = productName.ToUpperInvariant();
            if (name.Contains("KURCZAK")) return "🐔";
            if (name.Contains("FILET")) return "🥩";
            if (name.Contains("ĆWIARTKA") || name.Contains("CWIARTKA")) return "🍗";
            if (name.Contains("SKRZYDŁO") || name.Contains("SKRZYDLO")) return "🦅";
            if (name.Contains("NOGA") || name.Contains("NOGI")) return "🍗";
            if (name.Contains("PAŁKA") || name.Contains("PALKA")) return "🍗";
            if (name.Contains("KORPUS")) return "🦴";
            if (name.Contains("POLĘDWICZKI") || name.Contains("POLEDWICZKI")) return "🥓";
            if (name.Contains("SERCE") || name.Contains("SERCA")) return "❤️";
            if (name.Contains("WĄTROBA") || name.Contains("WATROBA") || name.Contains("WĄTRÓBKI") || name.Contains("WATROBKI")) return "🫀";
            if (name.Contains("ŻOŁĄDKI") || name.Contains("ZOLADKI")) return "🫘";
            return "🍖";
        }

        private string GetProductGroup(string productName)
        {
            var name = productName.ToUpperInvariant();
            if (name.Contains("KURCZAK")) return "A";
            if (name.Contains("FILET") || name.Contains("ĆWIARTKA") || name.Contains("CWIARTKA") ||
                name.Contains("SKRZYDŁO") || name.Contains("SKRZYDLO") || name.Contains("NOGA") || name.Contains("NOGI") ||
                name.Contains("PAŁKA") || name.Contains("PALKA") || name.Contains("KORPUS")) return "B";
            if (name.Contains("POLĘDWICZKI") || name.Contains("POLEDWICZKI")) return "C";
            if (name.Contains("SERCE") || name.Contains("SERCA") || name.Contains("WĄTROBA") || name.Contains("WATROBA") ||
                name.Contains("WĄTRÓBKI") || name.Contains("WATROBKI") || name.Contains("ŻOŁĄDKI") || name.Contains("ZOLADKI")) return "D";
            return "E";
        }

        private Button CreateProductButton(string text, string icon, object tag, bool isAllButton = false, BitmapImage? productImage = null)
        {
            var stack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (productImage != null)
            {
                // Zdjęcie produktu z bazy
                var imgBorder = new Border
                {
                    Width = 28,
                    Height = 28,
                    CornerRadius = new CornerRadius(4),
                    Background = new ImageBrush { ImageSource = productImage, Stretch = Stretch.UniformToFill },
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 1)
                };
                stack.Children.Add(imgBorder);
            }
            else
            {
                // Fallback na emoji
                var iconBlock = new TextBlock
                {
                    Text = icon,
                    FontSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                if (isAllButton) iconBlock.Foreground = Brushes.White;
                stack.Children.Add(iconBlock);
            }

            var nameBlock = new TextBlock
            {
                Text = text,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center
            };
            if (isAllButton) nameBlock.Foreground = Brushes.White;
            stack.Children.Add(nameBlock);

            var border = new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = isAllButton
                    ? new SolidColorBrush(Color.FromRgb(52, 152, 219))
                    : Brushes.White,
                BorderBrush = isAllButton
                    ? new SolidColorBrush(Color.FromRgb(41, 128, 185))
                    : new SolidColorBrush(Color.FromRgb(189, 195, 199)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(4, 2, 4, 2),
                Child = stack
            };

            var btn = new Button
            {
                Content = border,
                Margin = new Thickness(2),
                Padding = new Thickness(0),
                MinWidth = 65,
                Height = 52,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = tag
            };

            return btn;
        }

        private void GenerateProductButtons()
        {
            // Kompatybilność - stare przyciski nie są już używane
            pnlProductButtons.Children.Clear();
            PopulateProductComboBox();
        }

        private HashSet<int> _todayProductIds = new();
        private List<(int? id, string name, BitmapImage? image, bool inToday)> _allProductItems = new();
        private bool _isFilteringComboBox = false;

        private void PopulateProductComboBox(string filterText = null)
        {
            _isFilteringComboBox = true;
            cbProductFilter.SelectionChanged -= CbProductFilter_SelectionChanged;

            int? previousSelection = _selectedProductId;

            // Pełna przebudowa listy (nie filtrowanie tekstu)
            if (filterText == null)
            {
                _allProductItems.Clear();

                var allProducts = new Dictionary<int, string>();
                foreach (var kv in _productCatalogSwieze)
                    allProducts[kv.Key] = kv.Value;
                foreach (var kv in _productCatalogMrozone)
                    allProducts[kv.Key] = kv.Value;

                _allProductItems.Add((null, "Wszystkie produkty", null, false));

                foreach (var kv in allProducts.Where(kv => _todayProductIds.Contains(kv.Key)).OrderBy(kv => kv.Value))
                    _allProductItems.Add((kv.Key, kv.Value, GetProductImage(kv.Key), true));

                foreach (var kv in allProducts.Where(kv => !_todayProductIds.Contains(kv.Key)).OrderBy(kv => kv.Value))
                    _allProductItems.Add((kv.Key, kv.Value, GetProductImage(kv.Key), false));
            }

            cbProductFilter.Items.Clear();

            string filter = filterText?.Trim() ?? "";
            bool hasFilter = !string.IsNullOrEmpty(filter);

            var items = hasFilter
                ? _allProductItems.Where(p => p.id == null || p.name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList()
                : _allProductItems;

            var todayItems = items.Where(p => p.inToday).ToList();
            var otherItems = items.Where(p => !p.inToday && p.id.HasValue).ToList();

            // "Wszystkie" — zawsze na górze
            cbProductFilter.Items.Add(CreateProductComboItem(null, "📋 Wszystkie produkty", null, true, false));

            // Sekcja: zamówione dziś
            if (todayItems.Count > 0)
            {
                cbProductFilter.Items.Add(CreateSectionHeader($"▼ ZAMÓWIONE DZIŚ ({todayItems.Count})","#27AE60"));

                foreach (var p in todayItems)
                    cbProductFilter.Items.Add(CreateProductComboItem(p.id, p.name, p.image, false, true));
            }

            // Sekcja: pozostałe
            if (otherItems.Count > 0)
            {
                cbProductFilter.Items.Add(CreateSectionHeader($"▼ POZOSTAŁE ({otherItems.Count})", "#95A5A6"));

                foreach (var p in otherItems)
                    cbProductFilter.Items.Add(CreateProductComboItem(p.id, p.name, p.image, false, false));
            }

            // Brak wyników
            if (hasFilter && todayItems.Count == 0 && otherItems.Count == 0)
            {
                cbProductFilter.Items.Add(new ComboBoxItem
                {
                    Content = $"Brak wyników dla \"{filter}\"",
                    IsEnabled = false,
                    FontStyle = FontStyles.Italic,
                    Foreground = Brushes.Gray
                });
            }

            // Przywróć wybór
            if (!hasFilter)
            {
                bool restored = false;
                if (previousSelection.HasValue)
                {
                    for (int i = 0; i < cbProductFilter.Items.Count; i++)
                    {
                        if (cbProductFilter.Items[i] is ComboBoxItem ci && ci.Tag is int tagId && tagId == previousSelection.Value)
                        {
                            cbProductFilter.SelectedIndex = i;
                            restored = true;
                            break;
                        }
                    }
                }
                if (!restored)
                    cbProductFilter.SelectedIndex = 0;
            }

            cbProductFilter.SelectionChanged += CbProductFilter_SelectionChanged;
            _isFilteringComboBox = false;
        }

        private ComboBoxItem CreateSectionHeader(string text, string colorHex)
        {
            var c = (Color)ColorConverter.ConvertFromString(colorHex);
            return new ComboBoxItem
            {
                Content = new TextBlock
                {
                    Text = text,
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(c),
                    Padding = new Thickness(0, 4, 0, 2)
                },
                IsEnabled = false,
                IsHitTestVisible = false,
                Padding = new Thickness(4, 0, 4, 0)
            };
        }

        private ComboBoxItem CreateProductComboItem(int? id, string name, BitmapImage? image, bool isBold, bool isToday)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            if (image != null)
            {
                var img = new System.Windows.Controls.Image
                {
                    Source = image,
                    Width = 24,
                    Height = 24,
                    Margin = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                sp.Children.Add(img);
            }

            if (isToday)
            {
                sp.Children.Add(new TextBlock
                {
                    Text = "● ",
                    Foreground = new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60)),
                    FontSize = 10,
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            sp.Children.Add(new TextBlock
            {
                Text = name,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontWeight = isBold ? FontWeights.Bold : (isToday ? FontWeights.SemiBold : FontWeights.Normal)
            });

            return new ComboBoxItem
            {
                Tag = id,
                Content = sp,
                Padding = new Thickness(4, 3, 4, 3)
            };
        }

        private void TxtProductSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isFilteringComboBox) return;
            string text = txtProductSearch.Text;
            txtSearchWatermark.Visibility = string.IsNullOrEmpty(text) ? Visibility.Visible : Visibility.Collapsed;
            PopulateProductComboBox(text);
            if (!string.IsNullOrEmpty(text) && !cbProductFilter.IsDropDownOpen)
                cbProductFilter.IsDropDownOpen = true;
        }

        private async void CbProductFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized || _isFilteringComboBox) return;
            if (cbProductFilter.SelectedItem is ComboBoxItem selected && selected.IsEnabled)
            {
                _selectedProductId = selected.Tag as int?;
                UpdateWyborTowaruLabel();
                await RefreshAllDataAsync();
            }
        }

        /// <summary>Aktualizuje napis na przycisku wyboru towaru wg bieżącego filtra.</summary>
        private void UpdateWyborTowaruLabel()
        {
            if (btnWyborTowaru == null) return;
            if (!_selectedProductId.HasValue)
            {
                btnWyborTowaru.Content = "📋 Wszystkie produkty  ▾";
                return;
            }
            string nazwa = _allProductItems.FirstOrDefault(p => p.id == _selectedProductId.Value).name ?? "Towar";
            btnWyborTowaru.Content = $"🔎 {nazwa}  ▾";
        }

        /// <summary>Klik w przycisk — okno ze wszystkimi towarami; wybór filtruje i zamyka okno.</summary>
        private async void BtnWyborTowaru_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Upewnij się, że lista towarów jest zbudowana
                if (_allProductItems.Count == 0) PopulateProductComboBox();

                var okno = new Zamowienia.WyborTowaruWindow(_allProductItems, _selectedProductId) { Owner = this };
                if (okno.ShowDialog() == true && okno.Wybrano)
                {
                    _selectedProductId = okno.WybranyTowarId;
                    // Zsynchronizuj ukryty combobox (zachowuje spójność z resztą logiki), bez podwójnego refresh
                    SyncProductComboToSelection();
                    UpdateWyborTowaruLabel();
                    await RefreshAllDataAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie udało się otworzyć wyboru towaru:\n{ex.Message}",
                    "Wybór towaru", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>Ustawia ukryty combobox na _selectedProductId bez wywoływania jego handlera.</summary>
        private void SyncProductComboToSelection()
        {
            cbProductFilter.SelectionChanged -= CbProductFilter_SelectionChanged;
            int idx = 0;
            if (_selectedProductId.HasValue)
            {
                for (int i = 0; i < cbProductFilter.Items.Count; i++)
                    if (cbProductFilter.Items[i] is ComboBoxItem ci && ci.Tag is int tagId && tagId == _selectedProductId.Value)
                    { idx = i; break; }
            }
            cbProductFilter.SelectedIndex = idx;
            cbProductFilter.SelectionChanged += CbProductFilter_SelectionChanged;
        }

        /// <summary>
        /// 📦 Przychód produkcji z LibraNet (In0E) dla wybranego dnia.
        /// Jeśli w filtrze wybrano konkretny towar — jego nazwa trafia jako filtr startowy.
        /// </summary>
        private void BtnPrzychodLibra_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string? filtrStartowy = null;
                if (_selectedProductId.HasValue
                    && cbProductFilter.SelectedItem is ComboBoxItem sel
                    && sel.Content is StackPanel sp)
                {
                    // Nazwa towaru = ostatni TextBlock w itemie combo (przed nim bywa zdjęcie i kropka ●)
                    filtrStartowy = sp.Children.OfType<TextBlock>().LastOrDefault()?.Text?.Trim();
                }

                var okno = new Zamowienia.PrzychodLibraNetWindow(_selectedDate, filtrStartowy)
                {
                    Owner = this
                };
                okno.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie udało się otworzyć przychodu LibraNet:\n{ex.Message}",
                    "Przychód LibraNet", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetProductButtonStyle(Button button, bool isSelected)
        {
            if (button.Content is Border border && border.Child is StackPanel stack)
            {
                bool isAllButton = button.Tag == null || (button.Tag is int? && (int?)button.Tag == null);

                if (isSelected)
                {
                    border.Background = new SolidColorBrush(Color.FromRgb(46, 204, 113));
                    border.BorderBrush = new SolidColorBrush(Color.FromRgb(39, 174, 96));
                    foreach (var tb in stack.Children.OfType<TextBlock>())
                        tb.Foreground = Brushes.White;
                }
                else if (isAllButton)
                {
                    border.Background = new SolidColorBrush(Color.FromRgb(52, 152, 219));
                    border.BorderBrush = new SolidColorBrush(Color.FromRgb(41, 128, 185));
                    foreach (var tb in stack.Children.OfType<TextBlock>())
                        tb.Foreground = Brushes.White;
                }
                else
                {
                    border.Background = Brushes.White;
                    border.BorderBrush = new SolidColorBrush(Color.FromRgb(189, 195, 199));
                    foreach (var tb in stack.Children.OfType<TextBlock>())
                        tb.Foreground = Brushes.Black;
                }
            }
        }

        private async void ProductButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                // Reset all buttons
                foreach (var child in pnlProductButtons.Children.OfType<Button>())
                {
                    SetProductButtonStyle(child, false);
                }

                // Highlight selected
                SetProductButtonStyle(btn, true);

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
                                                header.Contains("Odśwież") ||
                                                header.Contains("dane odbiorcy");
                        }
                        else if (isAnulowane)
                        {
                            menuItem.IsEnabled = header.Contains("Szczegóły zamówienia") ||
                                                header.Contains("Płatności") ||
                                                header.Contains("Historia") ||
                                                header.Contains("Odśwież") ||
                                                header.Contains("Przywróć") ||
                                                header.Contains("USUŃ") ||
                                                header.Contains("transport") ||
                                                header.Contains("dane odbiorcy");
                        }
                        else
                        {
                            menuItem.IsEnabled = !isSpecialRow;
                        }
                    }
                }

                menuUsun.Visibility = (UserID == "11111") ? Visibility.Visible : Visibility.Collapsed;

                // Pokaż menu cyklu jeśli zamówienie ma CyklGroupId
                string cyklId = rowView.Row.Field<string>("CyklGroupId") ?? "";
                bool hasCykl = !string.IsNullOrEmpty(cyklId);
                menuPokazCykl.Visibility = hasCykl ? Visibility.Visible : Visibility.Collapsed;
                menuAnulujCykl.Visibility = hasCykl ? Visibility.Visible : Visibility.Collapsed;

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

            // Zbierz info o źródle dla historii
            string odbiorca = _contextMenuSelectedRow.Row.Field<string>("Odbiorca") ?? "Nieznany";
            decimal iloscKg = 0m;
            try { iloscKg = _contextMenuSelectedRow.Row.Field<decimal>("IloscZamowiona"); } catch { }
            string produktyInfo = "";
            try
            {
                await using var cnInfo = new SqlConnection(_connLibra);
                await cnInfo.OpenAsync();
                var cmdP = new SqlCommand(
                    "SELECT KodTowaru, SUM(Ilosc) FROM ZamowieniaMiesoTowar WHERE ZamowienieId = @Id GROUP BY KodTowaru", cnInfo);
                cmdP.Parameters.AddWithValue("@Id", id);
                var parts = new List<string>();
                using var rdr = await cmdP.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    int kod = rdr.GetInt32(0);
                    decimal kg = rdr.GetDecimal(1);
                    string nazwa = _productCatalogCache.TryGetValue(kod, out var n) ? n : $"#{kod}";
                    parts.Add($"{nazwa} {kg:N0}kg");
                }
                produktyInfo = string.Join(", ", parts);
            }
            catch { }

            _currentOrderId = id;
            await DisplayOrderDetailsAsync(id);

            var dlg = new MultipleDatePickerWindow("Wybierz dni dla duplikatu zamówienia");
            dlg.DatesSelected += async (s, ev) =>
            {
                if (!dlg.SelectedDates.Any()) return;

                int created = 0;
                var errors = new List<string>();
                bool copyNotes = dlg.CopyNotes;

                foreach (var date in dlg.SelectedDates)
                {
                    try
                    {
                        int newId = await DuplicateOrderAsync(_currentOrderId.Value, date, copyNotes: copyNotes);
                        created++;

                        // Historia — bogaty opis
                        var opis = $"Duplikat zamówienia #{_currentOrderId.Value} → #{newId}\n" +
                                   $"Odbiorca: {odbiorca}\n" +
                                   $"Data docelowa: {date:yyyy-MM-dd}\n" +
                                   $"Produkty: {produktyInfo}\n" +
                                   $"Ilość: {iloscKg:N0} kg\n" +
                                   $"Kopiowano: produkty, ceny, klasy wagowe, data produkcji, waluta" +
                                   (copyNotes ? ", notatki" : "");
                        _ = HistoriaZmianService.LogujUtworzenie(newId, UserID, App.UserFullName, opis);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{date:yyyy-MM-dd}: {ex.Message}");
                    }
                }

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Zduplikowano {created} z {dlg.SelectedDates.Count} zamówień");
                sb.AppendLine($"Odbiorca: {odbiorca}");
                sb.AppendLine($"Od {dlg.SelectedDates.Min():yyyy-MM-dd} do {dlg.SelectedDates.Max():yyyy-MM-dd}");
                if (errors.Any())
                {
                    sb.AppendLine();
                    sb.AppendLine($"Błędów: {errors.Count}");
                    foreach (var err in errors.Take(5))
                        sb.AppendLine($"  • {err}");
                }
                MessageBox.Show(sb.ToString(), "Duplikowanie",
                    MessageBoxButton.OK, errors.Any() ? MessageBoxImage.Warning : MessageBoxImage.Information);

                _selectedDate = dlg.SelectedDates.First();
                UpdateDayButtonDates();
                await RefreshAllDataAsync();
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

            // Pobierz pełne dane zamówienia źródłowego do wyświetlenia w dialogu
            var sourceInfo = new SourceOrderInfo { Id = id };
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                // Nagłówek zamówienia
                string dataProdCol = _dataProdukcjiColumnExists ? ", DataProdukcji" : "";
                var cmdHeader = new SqlCommand(
                    $"SELECT KlientId, DataZamowienia, DataPrzyjazdu, Uwagi{dataProdCol} FROM ZamowieniaMieso WHERE Id = @Id", cn);
                cmdHeader.Parameters.AddWithValue("@Id", id);
                using (var rdr = await cmdHeader.ExecuteReaderAsync())
                {
                    if (await rdr.ReadAsync())
                    {
                        int klientId = rdr.GetInt32(0);
                        sourceInfo.KlientId = klientId;
                        sourceInfo.DataZamowienia = rdr.GetDateTime(1);
                        sourceInfo.DataPrzyjazdu = rdr.IsDBNull(2) ? sourceInfo.DataZamowienia.AddHours(8) : rdr.GetDateTime(2);
                        if (_dataProdukcjiColumnExists && !rdr.IsDBNull(rdr.GetOrdinal("DataProdukcji")))
                            sourceInfo.DataProdukcji = rdr.GetDateTime(rdr.GetOrdinal("DataProdukcji"));

                        if (_cachedKontrahenci.TryGetValue(klientId, out var kInfo))
                            sourceInfo.Odbiorca = kInfo.Name;
                        else
                            sourceInfo.Odbiorca = $"Klient #{klientId}";
                    }
                }

                // Istniejące zamówienia klienta (do wykrywania kolizji)
                if (sourceInfo.KlientId > 0)
                {
                    string dateCol = (_showBySlaughterDate && _slaughterDateColumnExists) ? "DataUboju" : "DataZamowienia";
                    var cmdExist = new SqlCommand(
                        $"SELECT DISTINCT CAST({dateCol} AS DATE) FROM ZamowieniaMieso WHERE KlientId = @kid AND ISNULL(Status,'') != 'Anulowane' AND {dateCol} >= @from AND {dateCol} <= @to", cn);
                    cmdExist.Parameters.AddWithValue("@kid", sourceInfo.KlientId);
                    cmdExist.Parameters.AddWithValue("@from", DateTime.Today.AddDays(-7));
                    cmdExist.Parameters.AddWithValue("@to", DateTime.Today.AddDays(120));
                    using var rdrE = await cmdExist.ExecuteReaderAsync();
                    while (await rdrE.ReadAsync())
                    {
                        if (!rdrE.IsDBNull(0))
                            sourceInfo.ExistingOrderDates.Add(rdrE.GetDateTime(0).Date);
                    }
                }

                sourceInfo.ConnString = _connLibra;

                // Produkty zamówienia — lista z ilościami
                string strefaSel = _strefaColumnExists ? ", MAX(CAST(ISNULL(Strefa,0) AS INT))" : "";
                var cmdProd = new SqlCommand(
                    $"SELECT KodTowaru, SUM(Ilosc){strefaSel} FROM ZamowieniaMiesoTowar WHERE ZamowienieId = @Id GROUP BY KodTowaru", cn);
                cmdProd.Parameters.AddWithValue("@Id", id);
                var produkty = new List<string>();
                using (var rdr2 = await cmdProd.ExecuteReaderAsync())
                {
                    while (await rdr2.ReadAsync())
                    {
                        int kodTow = rdr2.GetInt32(0);
                        decimal ilosc = rdr2.GetDecimal(1);
                        sourceInfo.IloscKg += ilosc;
                        if (_strefaColumnExists && !rdr2.IsDBNull(2) && rdr2.GetInt32(2) > 0)
                            sourceInfo.MaStrefe = true;

                        string nazwaP = _productCatalogCache.TryGetValue(kodTow, out var pNazwa) ? pNazwa : $"#{kodTow}";
                        produkty.Add($"{nazwaP} {ilosc:N0} kg");
                    }
                }
                sourceInfo.Produkty = string.Join(", ", produkty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MenuCykliczne: read source failed: {ex.Message}");
                sourceInfo.Odbiorca = _contextMenuSelectedRow.Row.Field<string>("Odbiorca") ?? "Nieznany";
            }

            var dlg = new CyclicOrdersWindow(sourceInfo);
            if (dlg.ShowDialog() != true) return;

            if (dlg.SelectedDays == null || dlg.SelectedDays.Count == 0) return;

            var cyklGroupId = Guid.NewGuid();
            int totalDays = dlg.SelectedDays.Count;
            int created = 0;
            var errors = new List<string>();

            bool copyNotes = dlg.CopyNotes;
            bool copyKlasy = dlg.CopyKlasyWagowe;
            bool copyProd = dlg.CopyDataProdukcji;
            var godzinaMap = dlg.GodzinaPerDay ?? new Dictionary<DateTime, TimeSpan>();

            // Progress window
            var cts = new System.Threading.CancellationTokenSource();
            var progressWin = new Window
            {
                Title = "Tworzenie zamówień cyklicznych",
                Width = 420, Height = 160,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                Owner = this
            };
            var progressBar = new ProgressBar { Minimum = 0, Maximum = totalDays, Height = 22, Margin = new Thickness(16, 0, 16, 0) };
            var progressLabel = new TextBlock { Text = $"0 / {totalDays}", HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0), FontSize = 12 };
            var cancelBtn = new Button { Content = "Anuluj", Width = 80, Height = 28, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };
            cancelBtn.Click += (s2, e2) => { cts.Cancel(); cancelBtn.IsEnabled = false; cancelBtn.Content = "Anulowanie..."; };
            progressWin.Closing += (s2, e2) => { if (!cts.IsCancellationRequested) cts.Cancel(); };

            var sp = new StackPanel { Margin = new Thickness(0, 16, 0, 16) };
            sp.Children.Add(progressBar);
            sp.Children.Add(progressLabel);
            sp.Children.Add(cancelBtn);
            progressWin.Content = sp;
            progressWin.Show();

            foreach (var date in dlg.SelectedDays)
            {
                if (cts.IsCancellationRequested) break;
                try
                {
                    TimeSpan? godz = godzinaMap.TryGetValue(date, out var g) ? g : null;
                    int newId = await DuplicateOrderAsync(
                        _currentOrderId.Value,
                        date,
                        copyNotes: copyNotes,
                        copyKlasyWagowe: copyKlasy,
                        copyDataProdukcji: copyProd,
                        cyklGroupId: cyklGroupId,
                        godzinaOverride: godz);
                    created++;
                    progressBar.Value = created;
                    progressLabel.Text = $"{created} / {totalDays} — {date:yyyy-MM-dd}";

                    // Historia — bogaty opis
                    var opis = $"Zamówienie cykliczne ({created}/{totalDays}) #{id} → #{newId}\n" +
                               $"Odbiorca: {sourceInfo.Odbiorca}\n" +
                               $"Data docelowa: {date:yyyy-MM-dd}" + (godz.HasValue ? $" godz. {godz.Value:hh\\:mm}" : "") + "\n" +
                               $"Produkty: {sourceInfo.Produkty}\n" +
                               $"Ilość: {sourceInfo.IloscKg:N0} kg\n" +
                               $"Cykl: {cyklGroupId.ToString().Substring(0, 8)}... ({created}/{totalDays})\n" +
                               $"Kopiowano: produkty, ceny" +
                               (copyKlasy ? ", klasy wagowe" : "") +
                               (copyProd ? ", data produkcji" : "") +
                               (copyNotes ? ", notatki" : "") +
                               ", waluta, DataUboju";
                    _ = HistoriaZmianService.LogujUtworzenie(newId, UserID, App.UserFullName, opis);
                }
                catch (Exception ex)
                {
                    errors.Add($"{date:yyyy-MM-dd}: {ex.Message}");
                }
            }

            progressWin.Close();

            var sb = new System.Text.StringBuilder();
            if (cts.IsCancellationRequested && created < totalDays)
                sb.AppendLine($"Anulowano. Utworzono {created} z {totalDays} zamówień.");
            else
                sb.AppendLine($"Utworzono {created} z {totalDays} zamówień dla {sourceInfo.Odbiorca}.");
            sb.AppendLine($"Od {dlg.StartDate:yyyy-MM-dd} do {dlg.EndDate:yyyy-MM-dd}");
            if (errors.Any())
            {
                sb.AppendLine();
                sb.AppendLine($"Błędów: {errors.Count}");
                foreach (var err in errors.Take(10))
                    sb.AppendLine($"  • {err}");
            }
            var img = errors.Any() ? MessageBoxImage.Warning : MessageBoxImage.Information;
            MessageBox.Show(sb.ToString(), "Zamówienia cykliczne", MessageBoxButton.OK, img);

            await RefreshAllDataAsync();
        }

        private async void MenuPokazCykl_Click(object sender, RoutedEventArgs e)
        {
            if (_contextMenuSelectedRow == null) return;
            string cyklGuid = _contextMenuSelectedRow.Row.Field<string>("CyklGroupId") ?? "";
            if (string.IsNullOrEmpty(cyklGuid)) return;

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                string dateCol = (_showBySlaughterDate && _slaughterDateColumnExists) ? "DataUboju" : "DataZamowienia";
                var cmd = new SqlCommand(
                    $@"SELECT Id, {dateCol} AS DataZam, DataPrzyjazdu, KlientId, ISNULL(Status,'Nowe') AS Status,
                       (SELECT SUM(Ilosc) FROM ZamowieniaMiesoTowar WHERE ZamowienieId = zm.Id) AS Kg
                    FROM ZamowieniaMieso zm WHERE CAST(CyklGroupId AS NVARCHAR(36)) = @guid
                    ORDER BY {dateCol}", cn);
                cmd.Parameters.AddWithValue("@guid", cyklGuid);

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Cykl zamówień ({cyklGuid.Substring(0, 8)}...):");
                sb.AppendLine("─────────────────────────────────────────");
                sb.AppendLine($"{"ID",-8}{"Data",-14}{"Godz.",-8}{"Kg",10}  {"Status"}");
                sb.AppendLine("─────────────────────────────────────────");

                int count = 0;
                using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    count++;
                    int ordId = rdr.GetInt32(0);
                    string dataZam = rdr.IsDBNull(1) ? "—" : rdr.GetDateTime(1).ToString("yyyy-MM-dd");
                    string godz = rdr.IsDBNull(2) ? "" : rdr.GetDateTime(2).ToString("HH:mm");
                    string kg = rdr.IsDBNull(5) ? "0" : rdr.GetDecimal(5).ToString("N0");
                    string status = rdr.GetString(4);
                    string marker = ordId == _contextMenuSelectedRow.Row.Field<int>("Id") ? " <--" : "";
                    sb.AppendLine($"#{ordId,-7}{dataZam,-14}{godz,-8}{kg,10}  {status}{marker}");
                }
                sb.AppendLine("─────────────────────────────────────────");
                sb.AppendLine($"Razem: {count} zamówień w cyklu");

                MessageBox.Show(sb.ToString(), "Cykl zamówień", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void MenuAnulujCykl_Click(object sender, RoutedEventArgs e)
        {
            if (_contextMenuSelectedRow == null) return;
            string cyklGuid = _contextMenuSelectedRow.Row.Field<string>("CyklGroupId") ?? "";
            if (string.IsNullOrEmpty(cyklGuid)) return;

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                // Policz ile zamówień do anulowania
                var cmdCount = new SqlCommand(
                    "SELECT COUNT(*) FROM ZamowieniaMieso WHERE CAST(CyklGroupId AS NVARCHAR(36)) = @guid AND ISNULL(Status,'') NOT IN ('Anulowane','Zrealizowane')", cn);
                cmdCount.Parameters.AddWithValue("@guid", cyklGuid);
                int toCancel = Convert.ToInt32(await cmdCount.ExecuteScalarAsync());

                if (toCancel == 0)
                {
                    MessageBox.Show("Brak zamówień do anulowania w tym cyklu (wszystkie już anulowane lub zrealizowane).",
                        "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show(
                    $"Czy na pewno chcesz anulować {toCancel} zamówień z tego cyklu?\n\n(Zamówienia zrealizowane nie zostaną zmienione)",
                    "Anuluj cały cykl", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes) return;

                // Pobierz ID zamówień do anulowania (dla logowania)
                var idsToCancel = new List<int>();
                var cmdIds = new SqlCommand(
                    "SELECT Id FROM ZamowieniaMieso WHERE CAST(CyklGroupId AS NVARCHAR(36)) = @guid AND ISNULL(Status,'') NOT IN ('Anulowane','Zrealizowane')", cn);
                cmdIds.Parameters.AddWithValue("@guid", cyklGuid);
                using (var rdr = await cmdIds.ExecuteReaderAsync())
                    while (await rdr.ReadAsync()) idsToCancel.Add(rdr.GetInt32(0));

                var cmdUpdate = new SqlCommand(
                    @"UPDATE ZamowieniaMieso SET Status = 'Anulowane', AnulowanePrzez = @user, DataAnulowania = GETDATE()
                      WHERE CAST(CyklGroupId AS NVARCHAR(36)) = @guid
                        AND ISNULL(Status,'') NOT IN ('Anulowane','Zrealizowane')", cn);
                cmdUpdate.Parameters.AddWithValue("@guid", cyklGuid);
                cmdUpdate.Parameters.AddWithValue("@user", UserID);
                int affected = await cmdUpdate.ExecuteNonQueryAsync();

                // Loguj anulowanie każdego zamówienia z cyklu
                string odbiorcaCykl = _contextMenuSelectedRow.Row.Field<string>("Odbiorca") ?? "";
                foreach (var cancelId in idsToCancel)
                {
                    _ = HistoriaZmianService.LogujAnulowanie(cancelId, UserID, App.UserFullName,
                        $"Anulowanie cyklu — zamówienie #{cancelId}\n" +
                        $"Odbiorca: {odbiorcaCykl}\n" +
                        $"Anulowano {affected} zamówień z cyklu {cyklGuid.Substring(0, 8)}...\n" +
                        $"Zamówienia zrealizowane nie zostały zmienione");
                }

                MessageBox.Show($"Anulowano {affected} zamówień z cyklu.",
                    "Anulowano", MessageBoxButton.OK, MessageBoxImage.Information);

                await RefreshAllDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
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

            var win = new Kalendarz1.Zamowienia.Views.NoweZamowienieTestWindow(UserID, _currentOrderId.Value);
            win.Owner = this;
            if (win.ShowDialog() == true)
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

        private void MenuDaneOdbiorcy_Click(object sender, RoutedEventArgs e)
        {
            if (_contextMenuSelectedRow == null) return;

            int klientId = _contextMenuSelectedRow.Row.Field<int>("KlientId");
            if (klientId <= 0) return;

            var kartotekaWindow = new Kalendarz1.Kartoteka.Views.KartotekaOdbiorcowWindow(
                App.UserID ?? "11111",
                App.UserFullName ?? "Administrator",
                klientId);
            kartotekaWindow.Show();
        }

        private async Task PokazDiagnostykeContractorClassificationAsync(int klientId, string nowy, string verified, int rowsAffected)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Zapis się nie zadziałał. Wartość w bazie: \"{verified}\", oczekiwano: \"{nowy}\". Wierszy zmienionych: {rowsAffected}.");
            sb.AppendLine();
            sb.AppendLine("=== DIAGNOSTYKA ContractorClassification ===");
            sb.AppendLine();

            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();

                // 1. Obiekt typu
                sb.AppendLine("--- 1. Typ obiektu ---");
                await using (var c = new SqlCommand(
                    "SELECT name, type_desc FROM sys.objects WHERE object_id = OBJECT_ID('[HANDEL].[SSCommon].[ContractorClassification]')", cn))
                await using (var rd = await c.ExecuteReaderAsync())
                {
                    while (await rd.ReadAsync()) sb.AppendLine($"  {rd[0]} : {rd[1]}");
                }

                // 2. Kolumny (z is_computed, is_nullable)
                sb.AppendLine();
                sb.AppendLine("--- 2. Kolumny ---");
                await using (var c = new SqlCommand(
                    @"SELECT name, TYPE_NAME(user_type_id) AS Typ, max_length, is_nullable, is_computed
                      FROM sys.columns WHERE object_id = OBJECT_ID('[HANDEL].[SSCommon].[ContractorClassification]')
                      ORDER BY column_id", cn))
                await using (var rd = await c.ExecuteReaderAsync())
                {
                    while (await rd.ReadAsync())
                        sb.AppendLine($"  {rd["name"],-30} {rd["Typ"],-15} len={rd["max_length"]} null={rd["is_nullable"]} computed={rd["is_computed"]}");
                }

                // 3. Triggery
                sb.AppendLine();
                sb.AppendLine("--- 3. Triggery ---");
                await using (var c = new SqlCommand(
                    @"SELECT name, type_desc, is_disabled, is_instead_of_trigger
                      FROM sys.triggers WHERE parent_id = OBJECT_ID('[HANDEL].[SSCommon].[ContractorClassification]')", cn))
                await using (var rd = await c.ExecuteReaderAsync())
                {
                    bool any = false;
                    while (await rd.ReadAsync())
                    {
                        any = true;
                        sb.AppendLine($"  {rd["name"]} : {rd["type_desc"]} disabled={rd["is_disabled"]} instead_of={rd["is_instead_of_trigger"]}");
                    }
                    if (!any) sb.AppendLine("  (brak)");
                }

                // 4. Liczba wierszy dla tego klienta
                sb.AppendLine();
                sb.AppendLine($"--- 4. Wierszy dla ElementId={klientId} ---");
                await using (var c = new SqlCommand(
                    "SELECT COUNT(*) FROM [HANDEL].[SSCommon].[ContractorClassification] WHERE ElementId = @id", cn))
                {
                    c.Parameters.AddWithValue("@id", klientId);
                    var cnt = await c.ExecuteScalarAsync();
                    sb.AppendLine($"  COUNT = {cnt}");
                }

                // 5. Wszystkie kolumny z rzeczywistą wartością tego wiersza
                sb.AppendLine();
                sb.AppendLine($"--- 5. Wiersz dla ElementId={klientId} (wszystkie kolumny) ---");
                await using (var c = new SqlCommand(
                    "SELECT TOP 3 * FROM [HANDEL].[SSCommon].[ContractorClassification] WHERE ElementId = @id", cn))
                {
                    c.Parameters.AddWithValue("@id", klientId);
                    await using var rd = await c.ExecuteReaderAsync();
                    int rowIdx = 0;
                    while (await rd.ReadAsync())
                    {
                        sb.AppendLine($"  -- Wiersz {++rowIdx} --");
                        for (int i = 0; i < rd.FieldCount; i++)
                        {
                            var name = rd.GetName(i);
                            var v = rd.IsDBNull(i) ? "<NULL>" : rd[i]?.ToString() ?? "<NULL>";
                            if (v.Length > 80) v = v.Substring(0, 80) + "…";
                            sb.AppendLine($"    {name,-30} = {v}");
                        }
                    }
                    if (rowIdx == 0) sb.AppendLine("  (brak wierszy)");
                }

                // 6. Wzorcowy "działający" wiersz dla wybranego handlowca
                if (!string.IsNullOrEmpty(nowy))
                {
                    sb.AppendLine();
                    sb.AppendLine($"--- 6. Wzorcowy wiersz z CDim_Handlowiec_Val = '{nowy}' ---");
                    await using var c = new SqlCommand(
                        "SELECT TOP 1 * FROM [HANDEL].[SSCommon].[ContractorClassification] WHERE CDim_Handlowiec_Val = @h", cn);
                    c.Parameters.AddWithValue("@h", nowy);
                    await using var rd = await c.ExecuteReaderAsync();
                    if (await rd.ReadAsync())
                    {
                        for (int i = 0; i < rd.FieldCount; i++)
                        {
                            var name = rd.GetName(i);
                            var v = rd.IsDBNull(i) ? "<NULL>" : rd[i]?.ToString() ?? "<NULL>";
                            if (v.Length > 80) v = v.Substring(0, 80) + "…";
                            sb.AppendLine($"    {name,-30} = {v}");
                        }
                    }
                    else
                    {
                        sb.AppendLine("  (nie znaleziono wzorca — żaden kontrahent nie ma tego handlowca)");
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine();
                sb.AppendLine($"!!! Błąd diagnostyki: {ex.Message}");
            }

            sb.AppendLine();
            sb.AppendLine("Skopiuj to (Ctrl+A, Ctrl+C) i wyślij administratorowi.");

            // Pokaż w okienku z TextBox-em żeby dało się skopiować
            var win = new Window
            {
                Title = "Diagnostyka — Sage ContractorClassification",
                Width = 900, Height = 600,
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            var tb = new TextBox
            {
                Text = sb.ToString(),
                IsReadOnly = true,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.NoWrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(8)
            };
            win.Content = tb;
            try
            {
                System.Windows.Clipboard.SetText(sb.ToString());
                tb.AppendText("\n\n[Skopiowane do schowka.]");
            }
            catch { }
            win.ShowDialog();
        }

        private async void MenuPrzypiszHandlowca_Click(object sender, RoutedEventArgs e)
        {
            if (_contextMenuSelectedRow == null) return;

            int klientId = 0;
            try { klientId = _contextMenuSelectedRow.Row.Field<int>("KlientId"); } catch { }
            if (klientId <= 0)
            {
                MessageBox.Show("Nie udało się ustalić kontrahenta dla tego wiersza.",
                    "Brak danych", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string odbiorca = "";
            try { odbiorca = _contextMenuSelectedRow.Row.Field<string>("Odbiorca") ?? ""; } catch { }
            string aktualny = "";
            try { aktualny = _contextMenuSelectedRow.Row.Field<string>("Handlowiec") ?? ""; } catch { }

            // Lista istniejących handlowców z Symfonii
            var handlowcy = new List<string>();
            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(
                    "SELECT DISTINCT CDim_Handlowiec_Val FROM [HANDEL].[SSCommon].[ContractorClassification] WHERE CDim_Handlowiec_Val IS NOT NULL ORDER BY 1", cn);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    var h = rd[0]?.ToString();
                    if (!string.IsNullOrWhiteSpace(h)) handlowcy.Add(h);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Nie udało się pobrać listy handlowców:\n" + ex.Message,
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var dlg = new PrzypiszHandlowcaWpfDialog(odbiorca, aktualny, handlowcy) { Owner = this };
            if (dlg.ShowDialog() != true) return;

            string nowy = (dlg.WybranyHandlowiec ?? "").Trim();

            int rowsAffected = 0;
            string verifiedValue = "";
            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();

                // Sage Symfonia: kolumna CDim_Handlowiec (int FK do słownika wymiarów) jest źródłem prawdy.
                // INSTEAD OF UPDATE trigger nadpisuje CDim_Handlowiec_Val na podstawie CDim_Handlowiec.
                // Aby zmiana zadziałała, MUSIMY ustawić CDim_Handlowiec na właściwy ID.
                int? hidParam = null;
                if (!string.IsNullOrEmpty(nowy))
                {
                    await using var idCmd = new SqlCommand(
                        "SELECT TOP 1 CDim_Handlowiec FROM [HANDEL].[SSCommon].[ContractorClassification] " +
                        "WHERE CDim_Handlowiec_Val = @h AND CDim_Handlowiec IS NOT NULL", cn);
                    idCmd.Parameters.AddWithValue("@h", nowy);
                    var r = await idCmd.ExecuteScalarAsync();
                    if (r != null && r != DBNull.Value) hidParam = Convert.ToInt32(r);
                    else
                    {
                        MessageBox.Show(
                            "Nie znaleziono ID wymiaru dla handlowca '" + nowy + "'.\n\n" +
                            "Sage Symfonia trzyma handlowca jako referencję do słownika wymiarów. " +
                            "Aby przypisać handlowca, musi już istnieć przynajmniej jeden kontrahent " +
                            "z tym handlowcem (skąd skopiujemy ID).\n\n" +
                            "Wybierz handlowca z dropdownu, nie wpisuj ręcznie.",
                            "Brak ID wymiaru", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                // UPSERT — UPDATE jeśli wiersz istnieje, INSERT gdy nie. Trigger INSTEAD OF UPDATE
                // sam zaktualizuje _Val gdy CDim_Handlowiec się zmieni.
                const string upsertSql = @"
                    IF EXISTS (SELECT 1 FROM [HANDEL].[SSCommon].[ContractorClassification] WHERE ElementId = @id)
                        UPDATE [HANDEL].[SSCommon].[ContractorClassification]
                           SET CDim_Handlowiec = @hid, CDim_Handlowiec_Val = @h
                         WHERE ElementId = @id;
                    ELSE
                        INSERT INTO [HANDEL].[SSCommon].[ContractorClassification]
                            (Guid, ElementId, CDim_Handlowiec, CDim_Handlowiec_Val)
                        VALUES (NEWID(), @id, @hid, @h);";

                await using (var cmd = new SqlCommand(upsertSql, cn))
                {
                    cmd.Parameters.AddWithValue("@id", klientId);
                    cmd.Parameters.AddWithValue("@h", string.IsNullOrEmpty(nowy) ? (object)DBNull.Value : nowy);
                    cmd.Parameters.AddWithValue("@hid", (object?)hidParam ?? DBNull.Value);
                    rowsAffected = await cmd.ExecuteNonQueryAsync();
                }

                // Verify read-back
                await using (var verify = new SqlCommand(
                    "SELECT ISNULL(CDim_Handlowiec_Val, '') FROM [HANDEL].[SSCommon].[ContractorClassification] WHERE ElementId = @id", cn))
                {
                    verify.Parameters.AddWithValue("@id", klientId);
                    var v = await verify.ExecuteScalarAsync();
                    verifiedValue = v?.ToString() ?? "";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Nie udało się zapisać handlowca w Symfonii:\n" + ex.Message,
                    "Błąd zapisu", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Sprawdź, czy zapis faktycznie zadziałał
            if (!string.Equals(verifiedValue.Trim(), nowy.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                await PokazDiagnostykeContractorClassificationAsync(klientId, nowy, verifiedValue, rowsAffected);
                return;
            }

            // Wyczyść cache kontrahentów żeby refresh pobrał aktualne dane
            _cachedKontrahenci.Clear();
            _cachedKontrahenciTime = DateTime.MinValue;

            MessageBox.Show(
                $"✓ Zapisano handlowca: {(string.IsNullOrEmpty(nowy) ? "(brak)" : nowy)}\n" +
                $"Kontrahent: {odbiorca} (id={klientId})",
                "Zapisano", MessageBoxButton.OK, MessageBoxImage.Information);

            await RefreshAllDataAsync();
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
                var win = new Kalendarz1.Zamowienia.Views.NoweZamowienieTestWindow(UserID, _currentOrderId.Value);
                win.Owner = this;
                if (win.ShowDialog() == true)
                {
                    await RefreshAllDataAsync();
                }
            }
        }

        #endregion

        #region Data Loading

        // Flaga do włączania/wyłączania diagnostyki czasów ładowania
        private bool _showLoadingDiagnostics = false;

        private async Task RefreshAllDataAsync()
        {
            // Zapobiegaj równoległym wywołaniom
            if (_isRefreshing) return;
            _isRefreshing = true;

            // ✅ Pokaż overlay ładowania
            loadingOverlay.Visibility = Visibility.Visible;

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

                // ✅ 2b + 3: Równoległe ładowanie produktów dnia + podsumowanie
                stepSw.Restart();
                var taskTodayProducts = LoadTodayProductIdsAsync(_selectedDate);
                var taskAggregation = DisplayProductAggregationAsync(_selectedDate);
                await taskTodayProducts;
                PopulateProductComboBox();
                await taskAggregation;
                stepSw.Stop();
                timings.AppendLine($"2b+3. Produkty+Podsumowanie(||): {stepSw.ElapsedMilliseconds} ms");

                // Panel "Ostatnie zmiany kg" — fire-and-forget, nie blokuje głównego ładowania
                _ = LoadZmianyKgAsync(_selectedDate);

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

                // ✅ Ukryj overlay ładowania
                loadingOverlay.Visibility = Visibility.Collapsed;

                // Status bar - koniec
                UpdateStatus($"Gotowy - załadowano w {sw.ElapsedMilliseconds}ms");
                txtStatusTime.Text = $"Ostatnia aktualizacja: {DateTime.Now:HH:mm:ss}";

                // Pokaż diagnostykę czasów ładowania
                if (_showLoadingDiagnostics)
                {
                    ShowDiagnosticsWindow(sw.ElapsedMilliseconds);
                    _showLoadingDiagnostics = false;
                }

                // PRELOAD wyłączony — powodował dodatkowe obciążenie connection pool
                // _ = PreloadAdjacentDatesAsync(_selectedDate);
            }
        }

        private void ShowDiagnosticsWindow(long totalMs)
        {
            var allTimes = new List<(string category, string name, long ms)>();

            foreach (var t in _lastLoadOrdersDiag)
                allTimes.Add(("Zamówienia", t.name, t.ms));
            foreach (var t in _lastAggregationDiag)
                allTimes.Add(("Podsumowanie", t.name, t.ms));

            long maxMs = allTimes.Any() ? allTimes.Max(x => x.ms) : 1;
            const int barMaxLen = 30;

            var details = new System.Text.StringBuilder();
            details.AppendLine("╔══════════════════════════════════════════════════════════════╗");
            details.AppendLine("║          DIAGNOSTYKA CZASÓW ŁADOWANIA                        ║");
            details.AppendLine("╠══════════════════════════════════════════════════════════════╣");

            details.AppendLine("║                                                              ║");
            details.AppendLine("║  📋 ŁADOWANIE ZAMÓWIEŃ                                       ║");
            details.AppendLine("║  ────────────────────────────────────────────────────────    ║");
            long sumaZam = 0;
            foreach (var t in _lastLoadOrdersDiag)
            {
                int barLen = maxMs > 0 ? (int)((t.ms * barMaxLen) / maxMs) : 0;
                string bar = new string('█', barLen) + new string('░', barMaxLen - barLen);
                string warning = t.ms > 500 ? " ⚠️" : (t.ms > 200 ? " ⏱" : "");
                details.AppendLine($"║  {t.name,-16} {bar} {t.ms,5} ms{warning,-3}   ║");
                sumaZam += t.ms;
            }
            details.AppendLine($"║  {"SUMA",-16} {"",barMaxLen} {sumaZam,5} ms     ║");

            details.AppendLine("║                                                              ║");
            details.AppendLine("║  📊 PODSUMOWANIE PRODUKTÓW                                   ║");
            details.AppendLine("║  ────────────────────────────────────────────────────────    ║");
            long sumaAgg = 0;
            foreach (var t in _lastAggregationDiag)
            {
                int barLen = maxMs > 0 ? (int)((t.ms * barMaxLen) / maxMs) : 0;
                string bar = new string('█', barLen) + new string('░', barMaxLen - barLen);
                string warning = t.ms > 500 ? " ⚠️" : (t.ms > 200 ? " ⏱" : "");
                details.AppendLine($"║  {t.name,-16} {bar} {t.ms,5} ms{warning,-3}   ║");
                sumaAgg += t.ms;
            }
            details.AppendLine($"║  {"SUMA",-16} {"",barMaxLen} {sumaAgg,5} ms     ║");

            details.AppendLine("║                                                              ║");
            details.AppendLine("╠══════════════════════════════════════════════════════════════╣");
            details.AppendLine($"║  🏁 ŁĄCZNIE: {totalMs} ms                                        ║");
            details.AppendLine("╚══════════════════════════════════════════════════════════════╝");

            // Analiza wąskich gardeł
            var slowItems = allTimes.Where(x => x.ms > 200).OrderByDescending(x => x.ms).ToList();
            if (slowItems.Any())
            {
                details.AppendLine();
                details.AppendLine("⚡ WĄSKIE GARDŁA (>200ms):");
                foreach (var item in slowItems)
                {
                    double pct = totalMs > 0 ? (item.ms * 100.0 / totalMs) : 0;
                    details.AppendLine($"   • {item.name}: {item.ms}ms ({pct:F1}% całości)");
                }
            }

            // Wskazówki optymalizacji
            var sqlItems = allTimes.Where(x => x.name.Contains("Sql") || x.name.Contains("SQL")).Sum(x => x.ms);
            if (sqlItems > totalMs * 0.7)
            {
                details.AppendLine();
                details.AppendLine("💡 WSKAZÓWKI:");
                details.AppendLine($"   SQL stanowi {sqlItems * 100.0 / totalMs:F0}% czasu.");
                details.AppendLine("   Rozważ indeksy na kolumnach DataUboju, DataZamowienia.");
            }

            MessageBox.Show(details.ToString(),
                $"Diagnostyka - {_selectedDate:dd.MM.yyyy}",
                MessageBoxButton.OK, MessageBoxImage.Information);
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

            // ⚠ NAJPIERW zdejmij Sort/RowFilter z widoku! Żywy indeks DataView (po sortowaniu kolumną)
            // wskazuje kolumny, które zaraz skasujemy — bez tego pierwszy Rows.Add wywala
            // NullReference w System.Data.Index.CompareRecords. Sort przywracamy na końcu metody.
            string przywrocSort = "";
            try { przywrocSort = _dtOrders.DefaultView.Sort; } catch { }
            try { _dtOrders.DefaultView.Sort = ""; } catch { }
            try { _dtOrders.DefaultView.RowFilter = ""; } catch { }

            // Zawsze czyść i odtwarzaj kolumny - grupy mogą się zmienić
            _dtOrders.Clear();
            _dtOrders.Columns.Clear();

            // Podstawowe kolumny
            _dtOrders.Columns.Add("Id", typeof(int));
            _dtOrders.Columns.Add("KlientId", typeof(int));
            _dtOrders.Columns.Add("Kategoria", typeof(string));
            _dtOrders.Columns.Add("Odbiorca", typeof(string));
            _dtOrders.Columns.Add("Handlowiec", typeof(string));
            _dtOrders.Columns.Add("HandlowiecAvatar", typeof(BitmapSource));
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
            _dtOrders.Columns.Add("UtworzonePrzezID", typeof(string));
            _dtOrders.Columns.Add("UtworzonoGodzina", typeof(string));
            _dtOrders.Columns.Add("Status", typeof(string));
            _dtOrders.Columns.Add("MaNotatke", typeof(bool));
            _dtOrders.Columns.Add("MaFolie", typeof(bool));
            _dtOrders.Columns.Add("MaHallal", typeof(bool));
            _dtOrders.Columns.Add("MaStrefa", typeof(bool));
            _dtOrders.Columns.Add("MaMrozone", typeof(bool));
            _dtOrders.Columns.Add("Trans", typeof(string));
            _dtOrders.Columns.Add("Prod", typeof(string));
            _dtOrders.Columns.Add("CzyMaCeny", typeof(bool));
            _dtOrders.Columns.Add("CenaInfo", typeof(string));
            _dtOrders.Columns.Add("SredniaCena", typeof(decimal));
            _dtOrders.Columns.Add("TerminInfo", typeof(string));
            _dtOrders.Columns.Add("TransportInfo", typeof(string));
            _dtOrders.Columns.Add("CzyZrealizowane", typeof(bool));
            _dtOrders.Columns.Add("WyprInfo", typeof(string));
            _dtOrders.Columns.Add("WydanoInfo", typeof(string));
            _dtOrders.Columns.Add("TerminSort", typeof(DateTime));   // pełna data+godzina odbioru — sortowanie chronologiczne
            _dtOrders.Columns.Add("WydaneWszystko", typeof(int));    // -1 brak wydania / 1 wszystko / 0 częściowo (Panel Magazyniera)
            _dtOrders.Columns.Add("UtworzonoSort", typeof(DateTime)); // pełna data+godzina utworzenia — sortowanie chronologiczne
            _dtOrders.Columns.Add("CenaPoziom", typeof(int));         // 0-5: odchylenie od średniej ważonej kg dnia (czerwony→zielony), -1 = brak
            _dtOrders.Columns.Add("CenaTip", typeof(string));         // tooltip: "+1,3% vs średnia ważona dnia (8,42 zł/kg)"
            _dtOrders.Columns.Add("MaWlasnyTransport", typeof(bool)); // TransportStatus='Wlasny' → ikonka 🚗 przy nazwie
            _dtOrders.Columns.Add("WydanePoTerminie", typeof(bool));  // DataWydania > awizacja → godzina wydania na czerwono
            _dtOrders.Columns.Add("CyklGroupId", typeof(string));
            _dtOrders.Columns.Add("OgolneTyp", typeof(string)); // "" / "header" / "detail" — grupowanie handlowca "Ogólne"
            _dtOrders.Columns.Add("SortPriority", typeof(int)).DefaultValue = 1; // 0=SUMA (góra), 1=zwykłe, 8/9=Ogólne (dół) — trzymane także przy sortowaniu

            // Dynamiczne kolumny dla grup towarowych (z sanityzowanymi nazwami)
            _grupyKolumnDoNazw.Clear();
            foreach (var grupaName in _grupyTowaroweNazwy)
            {
                string colName = SanitizeColumnName(grupaName);
                _grupyKolumnDoNazw[colName] = grupaName;
                _dtOrders.Columns.Add(colName, typeof(decimal));
            }

            // ✅ OPTYMALIZACJA: Równoległe ładowanie Kontrahenci + Zamówienia + Wydania
            diagSw.Restart();
            int? selectedProductId = _selectedProductId;
            var temp = new DataTable();
            var clientsWithOrders = new HashSet<int>();
            _processedOrderIds.Clear();

            var contractors = new Dictionary<int, (string Name, string Salesman)>();
            Dictionary<int, Dictionary<int, decimal>> releasesPerClientProduct = null;

            // ✅ OPTYMALIZACJA: Użyj cache kontrahentów (ważne przez 5 minut)
            bool kontrahenciFromCache = _cachedKontrahenci.Count > 0 &&
                                        (DateTime.Now - _cachedKontrahenciTime).TotalMinutes < 5;

            // Zadanie 1: Kontrahenci z HANDEL (lub cache)
            var taskKontrahenci = Task.Run(async () =>
            {
                if (kontrahenciFromCache)
                {
                    return _cachedKontrahenci;
                }

                var result = new Dictionary<int, (string Name, string Salesman)>();
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                const string sqlContr = @"SELECT c.Id, c.Shortcut, wym.CDim_Handlowiec_Val
                                FROM [HANDEL].[SSCommon].[STContractors] c
                                LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] wym
                                ON c.Id = wym.ElementId";
                await using var cmd = new SqlCommand(sqlContr, cn);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    int id = rd.GetInt32(0);
                    string shortcut = rd.IsDBNull(1) ? "" : rd.GetString(1);
                    string salesman = rd.IsDBNull(2) ? "" : rd.GetString(2);
                    result[id] = (string.IsNullOrWhiteSpace(shortcut) ? $"KH {id}" : shortcut, salesman);
                }

                // Zapisz do cache
                _cachedKontrahenci = result;
                _cachedKontrahenciTime = DateTime.Now;

                return result;
            });

            // Zadanie 2: Zamówienia z LIBRA (KlienciZam + SQL Zamówienia)
            string dateColumn = (_showBySlaughterDate && _slaughterDateColumnExists) ? "DataUboju" : "DataZamowienia";
            string dateColumnZm = (_showBySlaughterDate && _slaughterDateColumnExists) ? "zm.DataUboju" : "zm.DataZamowienia";
            string slaughterDateSelect = _slaughterDateColumnExists ? ", zm.DataUboju" : "";
            string slaughterDateGroupBy = _slaughterDateColumnExists ? ", zm.DataUboju" : "";

            var taskZamowienia = Task.Run(async () =>
            {
                var clients = new HashSet<int>();
                var ordersTable = new DataTable();

                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                // KlienciZam
                string sqlClients = $@"SELECT DISTINCT KlientId FROM [dbo].[ZamowieniaMieso]
                              WHERE {dateColumn} = @Day AND KlientId IS NOT NULL";
                await using (var cmdClients = new SqlCommand(sqlClients, cn))
                {
                    cmdClients.Parameters.AddWithValue("@Day", day);
                    await using var readerClients = await cmdClients.ExecuteReaderAsync();
                    while (await readerClients.ReadAsync())
                        clients.Add(readerClients.GetInt32(0));
                }

                // SQL Zamówienia
                if (_productCatalogCache.Keys.Any())
                {
                    string strefaMax = _strefaColumnExists ? "CAST(MAX(CASE WHEN zmt.Strefa = 1 THEN 1 ELSE 0 END) AS BIT)" : "CAST(0 AS BIT)";
                    string sql = $@"
SELECT zm.Id, zm.KlientId, SUM(ISNULL(zmt.Ilosc,0)) AS Ilosc,
       zm.DataPrzyjazdu, zm.DataUtworzenia, zm.IdUser, zm.Status,
       zm.LiczbaPojemnikow, zm.LiczbaPalet, zm.TrybE2, zm.Uwagi, zm.TransportKursID,
       CAST(MAX(CASE WHEN zmt.Folia = 1 THEN 1 ELSE 0 END) AS BIT) AS MaFolie,
       CAST(MAX(CASE WHEN zmt.Hallal = 1 THEN 1 ELSE 0 END) AS BIT) AS MaHallal,
       {strefaMax} AS MaStrefa,
       CAST(CASE WHEN COUNT(zmt.KodTowaru) > 0
            AND SUM(CASE WHEN zmt.Cena IS NULL OR zmt.Cena = '' OR zmt.Cena = '0' THEN 1 ELSE 0 END) = 0
            THEN 1 ELSE 0 END AS BIT) AS CzyMaCeny,
       ISNULL(zm.CzyZrealizowane, 0) AS CzyZrealizowane,
       zm.DataWydania{slaughterDateSelect},
       zm.TransportStatus,
       CAST(zm.CyklGroupId AS NVARCHAR(36)) AS CyklGroupId
FROM [dbo].[ZamowieniaMieso] zm
LEFT JOIN [dbo].[ZamowieniaMiesoTowar] zmt ON zm.Id = zmt.ZamowienieId
WHERE {dateColumnZm} = @Day " +
                        (selectedProductId.HasValue ? "AND (zmt.KodTowaru = @ProductId OR zmt.KodTowaru IS NULL) " : "") +
                        $@"GROUP BY zm.Id, zm.KlientId, zm.DataPrzyjazdu, zm.DataUtworzenia, zm.IdUser, zm.Status,
     zm.LiczbaPojemnikow, zm.LiczbaPalet, zm.TrybE2, zm.Uwagi, zm.TransportKursID, zm.CzyZrealizowane, zm.DataWydania{slaughterDateGroupBy}, zm.TransportStatus, CAST(zm.CyklGroupId AS NVARCHAR(36))
ORDER BY zm.Id";

                    await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 15 };
                    cmd.Parameters.AddWithValue("@Day", day);
                    if (selectedProductId.HasValue)
                        cmd.Parameters.AddWithValue("@ProductId", selectedProductId.Value);

                    using var da = new SqlDataAdapter(cmd);
                    da.Fill(ordersTable);
                }
                return (clients, ordersTable);
            });

            // Zadanie 3: Wydania z HANDEL (równolegle!)
            var taskWydania = GetReleasesPerClientProductAsync(day);

            // Zadanie 3b: Wydania PER ZAMÓWIENIE z Panelu Magazyniera (CzyWydane + ZamowienieWydanieRoznice)
            var taskWydaniaPerOrder = GetWydaniaPerOrderAsync(day, dateColumnZm);

            // Zadanie 4: Kategorie odbiorców z KartotekaOdbiorcyDane (równolegle!) — TTL 30 min (kategorie zmieniają się rzadko)
            var taskKategorie = Task.Run(async () =>
            {
                if (_kategorieOdbiorcow.Count > 0 && (DateTime.Now - _kategorieOdbiorcowTime).TotalMinutes < 30)
                    return _kategorieOdbiorcow;

                var result = new Dictionary<int, string>();
                try
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();
                    const string sql = "SELECT IdSymfonia, KategoriaHandlowca FROM dbo.KartotekaOdbiorcyDane WHERE KategoriaHandlowca IS NOT NULL";
                    await using var cmd = new SqlCommand(sql, cn);
                    await using var rd = await cmd.ExecuteReaderAsync();
                    while (await rd.ReadAsync())
                    {
                        int id = rd.GetInt32(0);
                        string kat = rd.IsDBNull(1) ? "C" : rd.GetString(1).Trim();
                        if (!string.IsNullOrEmpty(kat))
                            result[id] = kat;
                    }
                }
                catch { }

                _kategorieOdbiorcow = result;
                _kategorieOdbiorcowTime = DateTime.Now;
                return result;
            });

            // Czekaj na wszystkie zadania
            await Task.WhenAll(taskKontrahenci, taskZamowienia, taskWydania, taskWydaniaPerOrder, taskKategorie);
            contractors = await taskKontrahenci;
            var zamResult = await taskZamowienia;
            clientsWithOrders = zamResult.clients;
            temp = zamResult.ordersTable;
            releasesPerClientProduct = await taskWydania;
            var wydaniaPerOrder = await taskWydaniaPerOrder;
            var kategorie = await taskKategorie;

            // Cache wydań
            var wydaniaSumPerProduct = new Dictionary<int, decimal>();
            foreach (var clientReleases in releasesPerClientProduct.Values)
            {
                foreach (var kvp in clientReleases)
                {
                    if (!wydaniaSumPerProduct.ContainsKey(kvp.Key))
                        wydaniaSumPerProduct[kvp.Key] = 0;
                    wydaniaSumPerProduct[kvp.Key] += kvp.Value;
                }
            }
            _cachedWydaniaSum = wydaniaSumPerProduct;
            _cachedWydaniaDate = day.Date;

            string kontrLabel = kontrahenciFromCache ? "Kontr©+Zam+Wyd(||)" : "Kontr+Zam+Wyd(||)";
            diagTimes.Add((kontrLabel, diagSw.ElapsedMilliseconds));

            // ✅ OPTYMALIZACJA: Zbierz dane potrzebne do równoległych zapytań
            diagSw.Restart();
            var transportKursIds = new List<long>();
            var zamowieniaIds = new List<int>();
            var anulowaneZamowieniaIds = new HashSet<int>();

            foreach (DataRow r in temp.Rows)
            {
                int id = Convert.ToInt32(r["Id"]);
                if (id > 0)
                {
                    zamowieniaIds.Add(id);
                    string status = r["Status"]?.ToString() ?? "";
                    if (string.Equals(status, "Anulowane", StringComparison.OrdinalIgnoreCase))
                        anulowaneZamowieniaIds.Add(id);
                }
                if (temp.Columns.Contains("TransportKursID") && !(r["TransportKursID"] is DBNull))
                {
                    long kursId = Convert.ToInt64(r["TransportKursID"]);
                    if (!transportKursIds.Contains(kursId))
                        transportKursIds.Add(kursId);
                }
            }

            // ✅ RÓWNOLEGŁE ZAPYTANIA: TransportKurs, TransportInfo, GrupyTowarowe, SrednieCeny
            var transportTimes = new Dictionary<long, (TimeSpan? GodzWyjazdu, DateTime? DataKursu)>();
            var sumaPerZamowieniePerGrupa = new Dictionary<int, Dictionary<string, decimal>>();
            var srednieCenyZamowien = new Dictionary<int, decimal>();
            Dictionary<long, (DateTime DataKursu, TimeSpan? GodzWyjazdu, string Kierowca)> transportInfo = null;

            var taskTransportKurs = Task.Run(async () =>
            {
                var result = new Dictionary<long, (TimeSpan? GodzWyjazdu, DateTime? DataKursu)>();
                if (transportKursIds.Any())
                {
                    try
                    {
                        await using var cn = new SqlConnection(_connTransport);
                        await cn.OpenAsync();
                        var kursIdsList = string.Join(",", transportKursIds);
                        var sqlKurs = $"SELECT KursID, GodzWyjazdu, DataKursu FROM dbo.Kurs WHERE KursID IN ({kursIdsList})";
                        await using var cmd = new SqlCommand(sqlKurs, cn);
                        await using var rd = await cmd.ExecuteReaderAsync();
                        while (await rd.ReadAsync())
                        {
                            long kursId = rd.GetInt64(0);
                            TimeSpan? godzWyjazdu = rd.IsDBNull(1) ? null : rd.GetTimeSpan(1);
                            DateTime? dataKursu = rd.IsDBNull(2) ? null : rd.GetDateTime(2);
                            result[kursId] = (godzWyjazdu, dataKursu);
                        }
                    }
                    catch { }
                }
                return result;
            });

            var taskTransportInfo = GetTransportInfoAsync(day);

            // ✅ KONSOLIDACJA: 1 zapytanie zamiast 3 (Grupy + Ceny + Mrożone wszystkie na ZamowieniaMiesoTowar WHERE Id IN (...))
            // Pobieramy wszystkie wiersze raz, potem rozparcelować w C# — eliminuje 2 roundtripy + 2 połączenia.
            var taskPozycjeWiersze = Task.Run(async () =>
            {
                var wiersze = new List<(int ZamId, int KodTowaru, decimal Ilosc, string Cena)>();
                if (!zamowieniaIds.Any()) return wiersze;
                try
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();
                    var sql = $@"SELECT ZamowienieId, KodTowaru, ISNULL(Ilosc, 0) AS Ilosc, ISNULL(Cena, '') AS Cena
                                 FROM [dbo].[ZamowieniaMiesoTowar]
                                 WHERE ZamowienieId IN ({string.Join(",", zamowieniaIds)})";
                    await using var cmd = new SqlCommand(sql, cn);
                    await using var rd = await cmd.ExecuteReaderAsync();
                    while (await rd.ReadAsync())
                    {
                        wiersze.Add((
                            rd.GetInt32(0),
                            rd.GetInt32(1),
                            rd.IsDBNull(2) ? 0m : Convert.ToDecimal(rd.GetValue(2)),
                            rd.IsDBNull(3) ? "" : rd.GetString(3)
                        ));
                    }
                }
                catch { }
                return wiersze;
            });

            // Czekaj na transportowe + skonsolidowane pozycje
            await Task.WhenAll(taskTransportKurs, taskTransportInfo, taskPozycjeWiersze);
            transportTimes = await taskTransportKurs;
            transportInfo = await taskTransportInfo;
            var wszystkieWiersze = await taskPozycjeWiersze;

            // Rozparcelowanie w C#: 1 pętla zamiast 3 zapytań SQL
            var zamowieniaMrozone = new HashSet<int>();
            var iloscNaZamowienie = new Dictionary<int, decimal>();      // ZamId → SUM(Ilosc) (do średniej ważonej)
            var iloscRazyCenaNaZamowienie = new Dictionary<int, decimal>(); // ZamId → SUM(Ilosc × Cena)

            foreach (var (zamId, kodTowaru, ilosc, cenaStr) in wszystkieWiersze)
            {
                // Grupy towarowe (mapowanie scalania)
                if (_mapowanieScalowania.TryGetValue(kodTowaru, out var nazwaGrupy))
                {
                    if (!sumaPerZamowieniePerGrupa.ContainsKey(zamId))
                        sumaPerZamowieniePerGrupa[zamId] = new Dictionary<string, decimal>();
                    if (!sumaPerZamowieniePerGrupa[zamId].ContainsKey(nazwaGrupy))
                        sumaPerZamowieniePerGrupa[zamId][nazwaGrupy] = 0m;
                    sumaPerZamowieniePerGrupa[zamId][nazwaGrupy] += ilosc;
                }

                // Mrożone (katalog 67153)
                if (_productCatalogMrozone.ContainsKey(kodTowaru))
                    zamowieniaMrozone.Add(zamId);

                // Średnia cena: agregacja ważona (ilość × cena), tylko niepuste ceny
                if (!string.IsNullOrWhiteSpace(cenaStr) && cenaStr != "0"
                    && decimal.TryParse(cenaStr.Replace(",", "."), System.Globalization.NumberStyles.Any,
                                        System.Globalization.CultureInfo.InvariantCulture, out var cenaVal)
                    && cenaVal > 0)
                {
                    // Gdy filtr produktu — używamy ceny tylko tego produktu (nadpisanie)
                    if (selectedProductId.HasValue)
                    {
                        if (kodTowaru == selectedProductId.Value)
                            srednieCenyZamowien[zamId] = cenaVal;
                    }
                    else
                    {
                        // Średnia ważona: gromadzimy ilość i ilość × cena, na końcu dzielimy
                        if (!iloscNaZamowienie.ContainsKey(zamId))
                        {
                            iloscNaZamowienie[zamId] = 0m;
                            iloscRazyCenaNaZamowienie[zamId] = 0m;
                        }
                        iloscNaZamowienie[zamId] += ilosc;
                        iloscRazyCenaNaZamowienie[zamId] += ilosc * cenaVal;
                    }
                }
            }

            // Finalizuj średnie ważone (tylko gdy nie ma filtra produktu)
            if (!selectedProductId.HasValue)
            {
                foreach (var kvp in iloscNaZamowienie)
                {
                    if (kvp.Value > 0)
                        srednieCenyZamowien[kvp.Key] = iloscRazyCenaNaZamowienie[kvp.Key] / kvp.Value;
                }
            }

            diagTimes.Add(("Trans+Grupy+Ceny(||)", diagSw.ElapsedMilliseconds));
            var cultureInfo = new CultureInfo("pl-PL");

            // Polskie skróty miesięcy dla formatu "Sty 12 (Ania)"
            string[] polskieMiesiaceSkrot = { "", "Sty", "Lut", "Mar", "Kwi", "Maj", "Cze", "Lip", "Sie", "Wrz", "Paź", "Lis", "Gru" };

            diagSw.Restart();
            decimal totalOrdered = 0m;
            decimal totalReleased = 0m;
            decimal totalPallets = 0m;
            decimal totalCenaWaga = 0m, totalCenaWartosc = 0m; // średnia ważona cena dla wiersza SUMA
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
                bool hasStrefa = temp.Columns.Contains("MaStrefa") && !(r["MaStrefa"] is DBNull) && Convert.ToBoolean(r["MaStrefa"]);
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

                // Ikony statusu nie są już dodawane do tekstu - wyświetlane jako kolorowe ikony w kolumnie szablonowej

                // Wydano PER ZAMÓWIENIE — z Panelu Magazyniera (faktyczne kg + flaga "czy wszystko poszło")
                decimal released = 0m;
                int wydaneWszystko = -1; // -1 = magazynier jeszcze nie wydał
                if (wydaniaPerOrder.TryGetValue(id, out var wo))
                {
                    released = selectedProductId.HasValue
                        ? (wo.PerProdukt.TryGetValue(selectedProductId.Value, out var w) ? w : 0m)
                        : wo.PerProdukt.Values.Sum();
                    wydaneWszystko = wo.Wszystko ? 1 : 0;
                }
                // Kg się zgadzają (wydano >= zamówiono) → bez ⚠, nawet gdy flaga magazyniera mówi "częściowo"
                if (wydaneWszystko == 0 && quantity > 0 && released >= quantity)
                    wydaneWszystko = 1;

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

                // Wydanie PO czasie awizacji → godzina wydania świeci na czerwono
                bool wydanePoTerminie = dataWydania.HasValue && arrivalDate.HasValue
                                        && dataWydania.Value > arrivalDate.Value;

                totalOrdersCount++;
                if (status != "Anulowane")
                {
                    totalOrdered += quantity;
                    // Wydania per zamówienie (Panel Magazyniera) — sumują się wprost
                    totalReleased += released;
                    totalPallets += pallets;
                    actualOrdersCount++;

                    // Średnia ważona cena (waga = ilość; tylko zamówienia z ceną)
                    decimal cenaZam = srednieCenyZamowien.TryGetValue(id, out var scSuma) ? scSuma : 0m;
                    if (cenaZam > 0 && quantity > 0)
                    {
                        totalCenaWaga += quantity;
                        totalCenaWartosc += cenaZam * quantity;
                    }
                }

                // Różnica = Zamówiono - Wydano
                decimal roznica = quantity - released;

                // Tworzenie wiersza z dynamicznymi kolumnami grup
                var newRow = _dtOrders.NewRow();
                newRow["Id"] = id;
                newRow["KlientId"] = clientId;
                newRow["Kategoria"] = kategorie.TryGetValue(clientId, out var kat) ? kat : "";
                newRow["Odbiorca"] = name;
                newRow["Handlowiec"] = salesman;
                EnsureHandlowiecAvatarCached(salesman);
                newRow["HandlowiecAvatar"] = _handlowiecAvatarCache.TryGetValue(salesman, out var av) ? (object)av : DBNull.Value;
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
                newRow["UtworzonePrzezID"] = userId.ToString();
                newRow["UtworzonoGodzina"] = createdDate.HasValue ? createdDate.Value.ToString("HH:mm") : "";
                newRow["Status"] = status;
                newRow["MaNotatke"] = hasNote;
                newRow["MaFolie"] = hasFoil;
                newRow["MaHallal"] = hasHallal;
                newRow["MaStrefa"] = hasStrefa;
                newRow["MaMrozone"] = zamowieniaMrozone.Contains(id);
                newRow["Trans"] = transColumn;
                newRow["Prod"] = prodColumn;
                newRow["CzyMaCeny"] = czyMaCeny;
                newRow["CenaInfo"] = cenaInfo;
                newRow["SredniaCena"] = srednieCenyZamowien.TryGetValue(id, out var sc) ? sc : 0m;
                newRow["TerminInfo"] = terminInfo;
                newRow["TransportInfo"] = transportInfoStr;
                newRow["CzyZrealizowane"] = czyZrealizowane;
                newRow["WyprInfo"] = wyprInfo;
                newRow["WydanoInfo"] = wydanoInfo;
                newRow["TerminSort"] = arrivalDate ?? day;
                newRow["WydaneWszystko"] = wydaneWszystko;
                newRow["UtworzonoSort"] = createdDate.HasValue ? (object)createdDate.Value : DBNull.Value;
                string transportStatus = temp.Columns.Contains("TransportStatus") && !(r["TransportStatus"] is DBNull)
                    ? r["TransportStatus"].ToString()?.Trim() ?? "" : "";
                newRow["MaWlasnyTransport"] = transportStatus.Equals("Wlasny", StringComparison.OrdinalIgnoreCase)
                                           || transportStatus.Equals("Własny", StringComparison.OrdinalIgnoreCase);
                newRow["WydanePoTerminie"] = wydanePoTerminie;

                // CyklGroupId
                string cyklGuid = temp.Columns.Contains("CyklGroupId") && !(r["CyklGroupId"] is DBNull)
                    ? r["CyklGroupId"].ToString() : "";
                newRow["CyklGroupId"] = cyklGuid;

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
                row["Kategoria"] = kategorie.TryGetValue(clientId, out var katWyd) ? katWyd : "";
                row["Odbiorca"] = name;
                row["Handlowiec"] = salesman;
                EnsureHandlowiecAvatarCached(salesman);
                row["HandlowiecAvatar"] = _handlowiecAvatarCache.TryGetValue(salesman, out var avWyd) ? (object)avWyd : DBNull.Value;
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
                row["UtworzonePrzezID"] = "";
                row["UtworzonoGodzina"] = "";
                row["Status"] = "Wydanie bez zamówienia";
                row["MaNotatke"] = false;
                row["MaFolie"] = false;
                row["MaHallal"] = false;
                row["MaStrefa"] = false;
                row["MaMrozone"] = false;
                row["Trans"] = "";
                row["Prod"] = "";
                row["CzyMaCeny"] = false;
                row["CenaInfo"] = "";
                row["SredniaCena"] = 0m;
                row["TerminInfo"] = "";
                row["TransportInfo"] = "";
                row["CzyZrealizowane"] = false;
                row["WyprInfo"] = "";
                row["WydanoInfo"] = "";
                row["TerminSort"] = day;
                row["WydaneWszystko"] = 1;
                row["MaWlasnyTransport"] = false;

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

                var sortedData = tempData.OrderBy(arr => arr[4]?.ToString() ?? "").ToList();

                _dtOrders.Rows.Clear();

                foreach (var rowData in sortedData)
                {
                    _dtOrders.Rows.Add(rowData);
                }
            }

            // === Poziom ceny 0-5: odchylenie od ŚREDNIEJ WAŻONEJ KILOGRAMAMI dnia ===
            // Uczciwe względem wolumenu: benchmark to średnia ważona kg (duże zamówienia ważą
            // proporcjonalnie więcej), a kolor zależy od % odchylenia — nie od rankingu.
            // Dzięki temu 90 kg po top cenie nie spycha 19 000 kg z ceną minimalnie niższą w czerwień.
            // Progi: ≥+5% ciemna zieleń / +2..5% zieleń / ±2% neutralny (w rynku) /
            //        −5..−2% pomarańcz / −10..−5% czerwień / <−10% ciemna czerwień
            {
                decimal sumaWag = 0m, sumaWartosci = 0m;
                foreach (DataRow row in _dtOrders.Rows)
                {
                    if (row["SredniaCena"] is decimal cv && cv > 0 &&
                        row["IloscZamowiona"] is decimal kg && kg > 0)
                    {
                        sumaWag += kg;
                        sumaWartosci += cv * kg;
                    }
                }
                decimal avgWazona = sumaWag > 0 ? sumaWartosci / sumaWag : 0m;

                foreach (DataRow row in _dtOrders.Rows)
                {
                    decimal cv = row["SredniaCena"] is decimal d ? d : 0m;
                    if (cv <= 0 || avgWazona <= 0)
                    {
                        row["CenaPoziom"] = -1;
                        row["CenaTip"] = "";
                        continue;
                    }
                    double odch = (double)((cv - avgWazona) / avgWazona * 100m);
                    int poziom = odch >= 5 ? 5
                               : odch >= 2 ? 4
                               : odch >= -2 ? 3
                               : odch >= -5 ? 2
                               : odch >= -10 ? 1
                               : 0;
                    row["CenaPoziom"] = poziom;
                    row["CenaTip"] = $"{(odch >= 0 ? "+" : "")}{odch:F1}% vs średnia ważona dnia ({avgWazona:N2} zł/kg)";
                }
            }

            // === Zamówienia "Ogólne" → jeden wiersz-nagłówek z możliwością rozwinięcia ===
            GrupujOgolne();

            if (_dtOrders.Rows.Count > 0 && actualOrdersCount > 0)
            {
                var summaryRow = _dtOrders.NewRow();
                summaryRow["Id"] = -1;
                summaryRow["KlientId"] = 0;
                summaryRow["Kategoria"] = "";
                summaryRow["SortPriority"] = 0; // SUMA zawsze na samej górze (też przy sortowaniu)
                summaryRow["Odbiorca"] = ""; // bez napisu — wiersz sumy poznaje się po stylu (zielone tło, bold)

                // ✅ POPRAWKA 3: Tylko liczba zamówień (bez tekstu "Zamówień:")
                summaryRow["Handlowiec"] = actualOrdersCount.ToString();
                summaryRow["HandlowiecAvatar"] = DBNull.Value;

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
                summaryRow["UtworzonoGodzina"] = "";
                summaryRow["Status"] = "SUMA";
                summaryRow["MaNotatke"] = false;
                summaryRow["MaFolie"] = false;
                summaryRow["MaHallal"] = false;
                summaryRow["MaStrefa"] = false;
                summaryRow["MaMrozone"] = false;
                summaryRow["Trans"] = "";
                summaryRow["Prod"] = "";
                summaryRow["CzyMaCeny"] = true; // SUMA nie świeci na czerwono "brakiem ceny"
                summaryRow["CenaInfo"] = "";
                summaryRow["SredniaCena"] = totalCenaWaga > 0 ? totalCenaWartosc / totalCenaWaga : 0m; // średnia ważona dnia
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

            // Przywróć sortowanie użytkownika zdjęte na początku metody (kolumny już odtworzone).
            // Try/catch: sort mógł wskazywać dynamiczną kolumnę grupy, której dziś nie ma.
            if (!string.IsNullOrEmpty(przywrocSort))
                try { _dtOrders.DefaultView.Sort = przywrocSort; } catch { }
        }

        private async Task LoadTodayProductIdsAsync(DateTime day)
        {
            _todayProductIds.Clear();
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                string dateColumn = _showBySlaughterDate && _slaughterDateColumnExists ? "z.DataUboju" : "z.DataZamowienia";
                string sql = $@"
                    SELECT DISTINCT t.KodTowaru
                    FROM dbo.ZamowieniaMiesoTowar t
                    INNER JOIN dbo.ZamowieniaMieso z ON z.ID = t.ZamowienieId
                    WHERE {dateColumn} = @Day AND z.Status <> N'Anulowane'
                      AND t.KodTowaru IS NOT NULL";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Day", day.Date);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0))
                        _todayProductIds.Add(reader.GetInt32(0));
                }
            }
            catch { }
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

        // Zwija wiersze odbiorcy "Ogólne*" do jednego wiersza-nagłówka z sumami (▶ zwinięte / ▼ rozwinięte).
        // Klik w wiersz-nagłówek przełącza stan (DgOrders_OgolneToggle) i przeładowuje listę.
        private void GrupujOgolne()
        {
            if (!_dtOrders.Columns.Contains("OgolneTyp")) return;

            // Grupujemy zamówienia przypisane do HANDLOWCA "Ogólne" (nie odbiorcy!)
            bool JestOgolne(DataRow r)
            {
                string h = (r["Handlowiec"]?.ToString() ?? "").ToLowerInvariant();
                return h.Contains("ogóln") || h.Contains("ogoln"); // "Ogólne", "OGÓLNE", bez diakrytyki itp.
            }
            _ogolneDetale.Clear();
            var detale = _dtOrders.Rows.Cast<DataRow>().Where(JestOgolne).ToList();
            if (detale.Count < 2) return; // pojedyncze zamówienie "Ogólne" zostaje zwykłym wierszem

            decimal sumZam = 0m, sumWyd = 0m, sumWaga = 0m, sumWartosc = 0m;
            foreach (var d in detale)
            {
                d["OgolneTyp"] = "detail";
                d["SortPriority"] = 9; // detale Ogólne zawsze na dole (też przy sortowaniu)
                decimal il = d["IloscZamowiona"] is decimal z ? z : 0m;
                sumZam += il;
                if (d["IloscFaktyczna"] is decimal w) sumWyd += w;
                // średnia ważona cena (waga = ilość zamówiona, tylko pozycje z ceną)
                if (d["SredniaCena"] is decimal cn && cn > 0 && il > 0)
                {
                    sumWaga += il;
                    sumWartosc += cn * il;
                }
            }
            decimal sredniaCenaGrupy = sumWaga > 0 ? sumWartosc / sumWaga : 0m;

            // Kopie detali — do cache (toggle działa w pamięci, bez przeładowania z bazy).
            // Avatar pobieramy PRZED usunięciem wierszy (usunięty DataRow nie ma już danych!)
            object avatarOgolne = detale[0]["HandlowiecAvatar"];
            _ogolneDetale.AddRange(detale.Select(d => (object[])d.ItemArray.Clone()));
            foreach (var d in detale) _dtOrders.Rows.Remove(d);

            var header = _dtOrders.NewRow();
            header["Id"] = -7777; // sentinel wiersza-nagłówka
            header["KlientId"] = 0;
            header["Kategoria"] = "";
            header["Odbiorca"] = (_ogolneExpanded ? "▼" : "▶") + $" Ogólne ({detale.Count})";
            header["Handlowiec"] = "Ogólne";
            header["HandlowiecAvatar"] = avatarOgolne ?? DBNull.Value; // avatar handlowca "Ogólne" (jeśli jest)
            header["IloscZamowiona"] = sumZam;
            header["IloscFaktyczna"] = sumWyd;
            header["Roznica"] = sumZam - sumWyd;
            header["Pojemniki"] = 0;
            header["Palety"] = 0m;
            header["TrybE2"] = "";
            header["DataPrzyjecia"] = DateTime.Today;
            header["GodzinaPrzyjecia"] = "";
            header["TerminOdbioru"] = "";
            header["DataUboju"] = DBNull.Value;
            header["UtworzonePrzez"] = "";
            header["UtworzonePrzezID"] = "";
            header["UtworzonoGodzina"] = "";
            header["Status"] = "";
            header["MaNotatke"] = false;
            header["MaFolie"] = false;
            header["MaHallal"] = false;
            header["MaStrefa"] = false;
            header["MaMrozone"] = false;
            header["Trans"] = "";
            header["Prod"] = "";
            header["CzyMaCeny"] = sredniaCenaGrupy > 0; // czerwone "-" gdy żadne zamówienie grupy nie ma ceny
            header["CenaInfo"] = "";
            header["SredniaCena"] = sredniaCenaGrupy; // średnia ważona cena pogrupowanych zamówień
            header["TerminInfo"] = "";
            header["TransportInfo"] = "";
            header["CzyZrealizowane"] = false;
            header["WyprInfo"] = "";
            header["WydanoInfo"] = "";
            header["CyklGroupId"] = "";
            header["OgolneTyp"] = "header";
            header["SortPriority"] = 8;

            // "Ogólne" ZAWSZE na samym dole tabeli (nagłówek + ewentualnie rozwinięte detale pod nim)
            _dtOrders.Rows.Add(header);
            if (_ogolneExpanded)
                foreach (var arr in _ogolneDetale)
                    _dtOrders.Rows.Add(arr);
        }

        // Sortowanie kolumn z zachowaniem przypięć: SUMA zawsze na górze, grupa "Ogólne" zawsze na dole
        private void DgOrders_Sorting(object sender, DataGridSortingEventArgs e)
        {
            string member = e.Column.SortMemberPath;
            if (string.IsNullOrEmpty(member)) return; // kolumny szablonowe bez SortMemberPath — bez sortowania

            e.Handled = true;
            var dir = e.Column.SortDirection != System.ComponentModel.ListSortDirection.Ascending
                ? System.ComponentModel.ListSortDirection.Ascending
                : System.ComponentModel.ListSortDirection.Descending;
            foreach (var c in dgOrders.Columns) c.SortDirection = null;
            e.Column.SortDirection = dir;

            string kierunek = dir == System.ComponentModel.ListSortDirection.Ascending ? "ASC" : "DESC";

            // Handlowiec: grupuj po handlowcu, a w obrębie handlowca od najwyższej ceny do najniższej
            string dodatkowy = member == "Handlowiec" ? ", SredniaCena DESC" : "";

            _dtOrders.DefaultView.Sort = $"SortPriority ASC, [{member}] {kierunek}{dodatkowy}";
        }

        private void DgOrders_OgolneToggle(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Toggle TYLKO po kliknięciu w strzałkę ▶/▼ (początek kolumny Odbiorca),
            // klik w resztę wiersza działa normalnie (zaznaczenie itd.)
            var dep = e.OriginalSource as DependencyObject;
            DataGridCell cell = null;
            DataGridRow row = null;
            while (dep != null)
            {
                if (cell == null && dep is DataGridCell c) cell = c;
                if (dep is DataGridRow r) { row = r; break; }
                dep = System.Windows.Media.VisualTreeHelper.GetParent(dep);
            }

            if (row?.Item is DataRowView drv
                && _dtOrders.Columns.Contains("OgolneTyp")
                && drv.Row["OgolneTyp"]?.ToString() == "header"
                && cell != null
                && ReferenceEquals(cell.Column, dgOrders.Columns.Count > 0 ? dgOrders.Columns[0] : null)
                && e.GetPosition(cell).X <= 26)
            {
                e.Handled = true;
                _ogolneExpanded = !_ogolneExpanded;

                // ⚡ Toggle w pamięci — bez przeładowania z bazy
                drv.Row["Odbiorca"] = (_ogolneExpanded ? "▼" : "▶") + $" Ogólne ({_ogolneDetale.Count})";
                if (_ogolneExpanded)
                {
                    foreach (var arr in _ogolneDetale)
                        _dtOrders.Rows.Add(arr); // nagłówek jest na dole → detale lądują tuż pod nim
                }
                else
                {
                    foreach (var d in _dtOrders.Rows.Cast<DataRow>()
                                 .Where(r => r["OgolneTyp"]?.ToString() == "detail").ToList())
                        _dtOrders.Rows.Remove(d);
                }
            }
        }

        private void SetupOrdersDataGrid()
        {
            // ⚡ Kolumny tabeli są statyczne — przebudowa szablonów/stylów tylko przy PIERWSZYM wywołaniu.
            // Kolejne odświeżenia tylko dopasowują szerokości (wiersze i tak płyną przez DefaultView).
            if (dgOrders.Columns.Count > 0)
            {
                ApplyOrdersLayout();
                return;
            }

            dgOrders.ItemsSource = _dtOrders.DefaultView;
            dgOrders.Columns.Clear();

            dgOrders.LoadingRow -= DgOrders_LoadingRow;
            dgOrders.LoadingRow += DgOrders_LoadingRow;

            // Toggle wiersza-nagłówka "Ogólne" (▶/▼)
            dgOrders.PreviewMouseLeftButtonDown -= DgOrders_OgolneToggle;
            dgOrders.PreviewMouseLeftButtonDown += DgOrders_OgolneToggle;

            // Sortowanie z przypiętą SUMĄ (góra) i Ogólne (dół)
            dgOrders.Sorting -= DgOrders_Sorting;
            dgOrders.Sorting += DgOrders_Sorting;

            // ✅ Avatary nie są ucinane — mogą wychodzić poza wiersz
            dgOrders.ClipToBounds = false;
            dgOrders.MinRowHeight = 40; // kompaktowe wiersze zamówień
            dgOrders.FontSize = 16;            // większa czcionka w tabeli zamówień
            dgOrders.ColumnHeaderHeight = 23;  // wąski wiersz nagłówka
            // Własny styl nagłówka — niski (styl współdzielony ma Height=40, który by wygrał)
            var orderHeaderStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
            orderHeaderStyle.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x2C, 0x3E, 0x50))));
            orderHeaderStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
            orderHeaderStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
            orderHeaderStyle.Setters.Add(new Setter(Control.FontSizeProperty, 7.5));
            orderHeaderStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6, 0, 6, 0)));
            orderHeaderStyle.Setters.Add(new Setter(FrameworkElement.HeightProperty, 23.0));
            orderHeaderStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Left));
            dgOrders.ColumnHeaderStyle = orderHeaderStyle;
            var rowStyle = new Style(typeof(DataGridRow));
            rowStyle.Setters.Add(new Setter(UIElement.ClipToBoundsProperty, false));
            dgOrders.RowStyle = rowStyle;

            // 0. Kategoria odbiorcy (A/B/C) — kolorowa litera PRZED nazwą odbiorcy (osobna kolumna usunięta)
            var kategoriaStyle = new Style(typeof(TextBlock));
            kategoriaStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Bold));
            kategoriaStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 12.0));
            kategoriaStyle.Setters.Add(new Setter(TextBlock.MarginProperty, new Thickness(0, 0, 5, 0)));
            kategoriaStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));

            var katATrigger = new DataTrigger { Binding = new System.Windows.Data.Binding("Kategoria"), Value = "A" };
            katATrigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0, 150, 0))));
            var katBTrigger = new DataTrigger { Binding = new System.Windows.Data.Binding("Kategoria"), Value = "B" };
            katBTrigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(30, 100, 200))));
            var katCTrigger = new DataTrigger { Binding = new System.Windows.Data.Binding("Kategoria"), Value = "C" };
            katCTrigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(160, 160, 160))));
            kategoriaStyle.Triggers.Add(katATrigger);
            kategoriaStyle.Triggers.Add(katBTrigger);
            kategoriaStyle.Triggers.Add(katCTrigger);

            // 1. Odbiorca - kolumna szablonowa z kolorowymi ikonami statusu
            {
                var odbiorcaCol = new DataGridTemplateColumn
                {
                    Header = "Odbiorca",
                    Width = new DataGridLength(195),
                    IsReadOnly = true
                };

                var odbiorcaCellFactory = new FrameworkElementFactory(typeof(StackPanel));
                odbiorcaCellFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
                odbiorcaCellFactory.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);
                odbiorcaCellFactory.SetValue(StackPanel.MarginProperty, new Thickness(2));

                var boolToVisConv = new BooleanToVisibilityConverter();

                // Litera kategorii (A/B/C) — PIERWSZA, przed wszystkimi ikonkami
                var katTxt = new FrameworkElementFactory(typeof(TextBlock));
                katTxt.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Kategoria"));
                katTxt.SetValue(FrameworkElement.StyleProperty, kategoriaStyle);
                odbiorcaCellFactory.AppendChild(katTxt);

                // Ikona 🚗 — klient odbiera własnym transportem (TransportStatus='Wlasny')
                var icoAuto = new FrameworkElementFactory(typeof(TextBlock));
                icoAuto.SetValue(TextBlock.TextProperty, "\U0001F697");
                icoAuto.SetValue(TextBlock.FontSizeProperty, 12.0);
                icoAuto.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 2, 0));
                icoAuto.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
                icoAuto.SetValue(FrameworkElement.ToolTipProperty, "Transport własny klienta");
                icoAuto.SetBinding(TextBlock.VisibilityProperty, new System.Windows.Data.Binding("MaWlasnyTransport") { Converter = boolToVisConv });
                odbiorcaCellFactory.AppendChild(icoAuto);

                // Ikona Mrożone ❄️ - niebieska
                var icoMrozone = new FrameworkElementFactory(typeof(TextBlock));
                icoMrozone.SetValue(TextBlock.TextProperty, "\u2744\uFE0F");
                icoMrozone.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0x42, 0xA5, 0xF5)));
                icoMrozone.SetValue(TextBlock.FontSizeProperty, 12.0);
                icoMrozone.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 2, 0));
                icoMrozone.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
                icoMrozone.SetBinding(TextBlock.VisibilityProperty, new System.Windows.Data.Binding("MaMrozone") { Converter = boolToVisConv });
                odbiorcaCellFactory.AppendChild(icoMrozone);

                // Ikona Strefa - czerwona
                var icoStrefa = new FrameworkElementFactory(typeof(TextBlock));
                icoStrefa.SetValue(TextBlock.TextProperty, "\u26A0\uFE0F");
                icoStrefa.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B)));
                icoStrefa.SetValue(TextBlock.FontSizeProperty, 12.0);
                icoStrefa.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 2, 0));
                icoStrefa.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
                icoStrefa.SetBinding(TextBlock.VisibilityProperty, new System.Windows.Data.Binding("MaStrefa") { Converter = boolToVisConv });
                odbiorcaCellFactory.AppendChild(icoStrefa);

                // Ikona Halal - teal
                var icoHalal = new FrameworkElementFactory(typeof(TextBlock));
                icoHalal.SetValue(TextBlock.TextProperty, "\U0001F52A");
                icoHalal.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0x4D, 0xB6, 0xAC)));
                icoHalal.SetValue(TextBlock.FontSizeProperty, 12.0);
                icoHalal.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 2, 0));
                icoHalal.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
                icoHalal.SetBinding(TextBlock.VisibilityProperty, new System.Windows.Data.Binding("MaHallal") { Converter = boolToVisConv });
                odbiorcaCellFactory.AppendChild(icoHalal);

                // Ikona Folia - niebieska
                var icoFolia = new FrameworkElementFactory(typeof(TextBlock));
                icoFolia.SetValue(TextBlock.TextProperty, "\U0001F39E\uFE0F");
                icoFolia.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0x64, 0xB5, 0xF6)));
                icoFolia.SetValue(TextBlock.FontSizeProperty, 12.0);
                icoFolia.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 2, 0));
                icoFolia.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
                icoFolia.SetBinding(TextBlock.VisibilityProperty, new System.Windows.Data.Binding("MaFolie") { Converter = boolToVisConv });
                odbiorcaCellFactory.AppendChild(icoFolia);

                // Tekst Odbiorca
                var odbiorcaTxt = new FrameworkElementFactory(typeof(TextBlock));
                odbiorcaTxt.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Odbiorca"));
                odbiorcaTxt.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
                odbiorcaTxt.SetValue(TextBlock.FontSizeProperty, 15.0);
                odbiorcaTxt.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
                odbiorcaCellFactory.AppendChild(odbiorcaTxt);

                odbiorcaCol.CellTemplate = new DataTemplate { VisualTree = odbiorcaCellFactory };
                dgOrders.Columns.Add(odbiorcaCol);
            }

            // 2. Handlowiec z avatarem (rozmiar jak Utworzono - 32px display, 64px source)
            {
                var handTemplate = new DataGridTemplateColumn
                {
                    Header = "",
                    Width = new DataGridLength(44),
                    IsReadOnly = true,
                    SortMemberPath = "Handlowiec", // klik w nagłówek: po handlowcu + cena malejąco
                    CanUserSort = true
                };

                var cellFactory = new FrameworkElementFactory(typeof(StackPanel));
                cellFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
                cellFactory.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);
                cellFactory.SetValue(StackPanel.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                cellFactory.SetValue(StackPanel.MarginProperty, new Thickness(2));
                cellFactory.SetValue(UIElement.SnapsToDevicePixelsProperty, true);
                cellFactory.SetValue(FrameworkElement.UseLayoutRoundingProperty, true);

                var imgFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.Image));
                imgFactory.SetBinding(System.Windows.Controls.Image.SourceProperty, new System.Windows.Data.Binding("HandlowiecAvatar"));
                imgFactory.SetValue(FrameworkElement.WidthProperty, 34.0);
                imgFactory.SetValue(FrameworkElement.HeightProperty, 34.0);
                imgFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 2, 0));
                imgFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
                imgFactory.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.Fant);
                imgFactory.SetValue(UIElement.SnapsToDevicePixelsProperty, true);

                // Tylko avatar — nazwa handlowca w tooltipie (kolumna węższa)
                imgFactory.SetBinding(FrameworkElement.ToolTipProperty, new System.Windows.Data.Binding("Handlowiec"));
                cellFactory.AppendChild(imgFactory);

                handTemplate.CellTemplate = new DataTemplate { VisualTree = cellFactory };
                // ✅ Avatar nie ucinany
                var avatarCellStyle = new Style(typeof(DataGridCell));
                avatarCellStyle.Setters.Add(new Setter(UIElement.ClipToBoundsProperty, false));
                handTemplate.CellStyle = avatarCellStyle;
                dgOrders.Columns.Add(handTemplate);
            }

            // 3. Zam. — scalone: na górze zamówiono (bold), pod spodem wydano (małe, lekkie). Kolumny Wyd. i +/- usunięte.
            {
                var zamCol = new DataGridTemplateColumn
                {
                    Header = "Zam.",
                    Width = new DataGridLength(82),
                    IsReadOnly = true
                };
                var zamStack = new FrameworkElementFactory(typeof(StackPanel));
                zamStack.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
                zamStack.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);
                zamStack.SetValue(StackPanel.HorizontalAlignmentProperty, HorizontalAlignment.Center);

                var zamTxt = new FrameworkElementFactory(typeof(TextBlock));
                zamTxt.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("IloscZamowiona") { StringFormat = "{0:N0} kg" });
                zamTxt.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
                zamTxt.SetValue(TextBlock.FontSizeProperty, 15.0);
                zamTxt.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                zamStack.AppendChild(zamTxt);

                // Wydano (Panel Magazyniera) — styl przez Style, bo trigger na "częściowo wydane" musi wygrać
                var wydStyle = new Style(typeof(TextBlock));
                wydStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 11.0));
                wydStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(139, 148, 158))));
                wydStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, "Wydano (Panel Magazyniera)"));
                var czescioweTrig = new DataTrigger { Binding = new System.Windows.Data.Binding("WydaneWszystko"), Value = 0 };
                czescioweTrig.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0xE6, 0x7E, 0x22))));
                czescioweTrig.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
                czescioweTrig.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, "Magazynier: NIE wszystko wydane"));
                wydStyle.Triggers.Add(czescioweTrig);

                var wydStack = new FrameworkElementFactory(typeof(StackPanel));
                wydStack.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
                wydStack.SetValue(StackPanel.HorizontalAlignmentProperty, HorizontalAlignment.Center);

                var wydTxt = new FrameworkElementFactory(typeof(TextBlock));
                wydTxt.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("IloscFaktyczna") { StringFormat = "{0:N0} kg" });
                wydTxt.SetValue(FrameworkElement.StyleProperty, wydStyle);
                wydStack.AppendChild(wydTxt);

                // ⚠ tylko gdy magazynier zaznaczył "nie wszystko wydane"
                var warnStyle = new Style(typeof(TextBlock));
                warnStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 9.0));
                warnStyle.Setters.Add(new Setter(TextBlock.MarginProperty, new Thickness(2, 0, 0, 0)));
                warnStyle.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Collapsed));
                warnStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, "Magazynier: NIE wszystko wydane"));
                var warnTrig = new DataTrigger { Binding = new System.Windows.Data.Binding("WydaneWszystko"), Value = 0 };
                warnTrig.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Visible));
                warnStyle.Triggers.Add(warnTrig);

                var warnTxt = new FrameworkElementFactory(typeof(TextBlock));
                warnTxt.SetValue(TextBlock.TextProperty, "⚠");
                warnTxt.SetValue(FrameworkElement.StyleProperty, warnStyle);
                wydStack.AppendChild(warnTxt);

                zamStack.AppendChild(wydStack);

                zamCol.CellTemplate = new DataTemplate { VisualTree = zamStack };
                dgOrders.Columns.Add(zamCol);
            }

            // 6. (kolumna ✓/✗ "Cena" usunięta — informacja o brakach cen jest w tooltipie/edytorze)

            // 7. Cena — średnia ważona (zł/kg); skala 6 przyciemnionych odcieni czerwień→żółć→zieleń
            //    wg pozycji ceny na tle dnia (CenaPoziom 0-5); brak ceny = "- zł/kg" NA CZERWONO
            var sredniaCenaStyle = new Style(typeof(TextBlock));
            sredniaCenaStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            sredniaCenaStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            sredniaCenaStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brushes.Black));
            sredniaCenaStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, new System.Windows.Data.Binding("CenaTip")));
            // Pusty tip → bez dymka
            var pustyTipTrig = new DataTrigger { Binding = new System.Windows.Data.Binding("CenaTip"), Value = "" };
            pustyTipTrig.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, null));
            sredniaCenaStyle.Triggers.Add(pustyTipTrig);

            // Przyciemnione (cieniowane czernią) odcienie: poniżej średniej ważonej → czerwienie, powyżej → zielenie
            var cenaPoziomKolory = new (int Poziom, Color Kolor)[]
            {
                (0, Color.FromRgb(0x7B, 0x24, 0x1C)), // ciemna czerwień
                (1, Color.FromRgb(0xB0, 0x3A, 0x2E)), // czerwień
                (2, Color.FromRgb(0xB9, 0x5A, 0x0E)), // ciemny pomarańcz
                (3, Color.FromRgb(0x8F, 0x73, 0x0A)), // ciemna żółć (oliwkowa)
                (4, Color.FromRgb(0x1E, 0x84, 0x49)), // zieleń
                (5, Color.FromRgb(0x14, 0x5A, 0x32))  // ciemna zieleń
            };
            foreach (var (poziom, kolor) in cenaPoziomKolory)
            {
                var trig = new DataTrigger { Binding = new System.Windows.Data.Binding("CenaPoziom"), Value = poziom };
                trig.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(kolor)));
                trig.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
                // Biały obrys (cień) — kolor odcina się od tła
                trig.Setters.Add(new Setter(UIElement.EffectProperty, new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.White,
                    ShadowDepth = 0,
                    BlurRadius = 3,
                    Opacity = 0.9
                }));
                sredniaCenaStyle.Triggers.Add(trig);
            }

            // Brak ceny — wygrywa z poziomem: czerwone, bold "- zł/kg"
            var brakCenyTrigger = new DataTrigger { Binding = new System.Windows.Data.Binding("CzyMaCeny"), Value = false };
            brakCenyTrigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(200, 0, 0))));
            brakCenyTrigger.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Bold));
            sredniaCenaStyle.Triggers.Add(brakCenyTrigger);

            // Zaznaczony wiersz — OSTATNI trigger (wygrywa ze wszystkim): biały tekst na niebieskiej selekcji
            var cenaSelTrig = new DataTrigger
            {
                Binding = new System.Windows.Data.Binding("IsSelected")
                {
                    RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGridRow), 1)
                },
                Value = true
            };
            cenaSelTrig.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brushes.White));
            cenaSelTrig.Setters.Add(new Setter(UIElement.EffectProperty, null));
            sredniaCenaStyle.Triggers.Add(cenaSelTrig);

            // Kolumna szablonowa: liczba (kolor+obrys) na górze, "zł/kg" małym drukiem pod spodem
            {
                var cenaCol = new DataGridTemplateColumn
                {
                    Header = "Cena",
                    Width = new DataGridLength(64),
                    SortMemberPath = "SredniaCena",
                    IsReadOnly = true
                };

                var cenaStack = new FrameworkElementFactory(typeof(StackPanel));
                cenaStack.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
                cenaStack.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);
                cenaStack.SetValue(StackPanel.HorizontalAlignmentProperty, HorizontalAlignment.Center);

                var cenaTxt = new FrameworkElementFactory(typeof(TextBlock));
                cenaTxt.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("SredniaCena") { Converter = new CenaLiczbaConverter() });
                cenaTxt.SetValue(TextBlock.FontSizeProperty, 17.0);
                cenaTxt.SetValue(FrameworkElement.StyleProperty, sredniaCenaStyle);
                cenaStack.AppendChild(cenaTxt);

                // "zł/kg" — mały, szary; przy zaznaczeniu wiersza biały
                var jednostkaStyle = new Style(typeof(TextBlock));
                jednostkaStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 8.5));
                jednostkaStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(139, 148, 158))));
                jednostkaStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
                var jednSelTrig = new DataTrigger
                {
                    Binding = new System.Windows.Data.Binding("IsSelected")
                    {
                        RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGridRow), 1)
                    },
                    Value = true
                };
                jednSelTrig.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brushes.White));
                jednostkaStyle.Triggers.Add(jednSelTrig);

                var cenaJedn = new FrameworkElementFactory(typeof(TextBlock));
                cenaJedn.SetValue(TextBlock.TextProperty, "zł/kg");
                cenaJedn.SetValue(FrameworkElement.StyleProperty, jednostkaStyle);
                cenaStack.AppendChild(cenaJedn);

                cenaCol.CellTemplate = new DataTemplate { VisualTree = cenaStack };
                dgOrders.Columns.Add(cenaCol);
            }

            // 8. Utworzono — BEZ avatara: kto utworzył (11) + godzina (9), kompaktowo
            var utworzonoColumn = new DataGridTemplateColumn
            {
                Header = "Utworzono",
                Width = new DataGridLength(92),
                SortMemberPath = "UtworzonoSort" // pełna data+godzina dodania → od najwcześniejszego
            };

            var utworzonoTemplate = new DataTemplate();
            var textContainerFactory = new FrameworkElementFactory(typeof(StackPanel));
            textContainerFactory.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
            textContainerFactory.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);
            textContainerFactory.SetValue(StackPanel.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            textContainerFactory.SetValue(StackPanel.MarginProperty, new Thickness(2));

            var textFactory = new FrameworkElementFactory(typeof(TextBlock));
            textFactory.SetValue(TextBlock.FontSizeProperty, 13.0);
            textFactory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            textFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("UtworzonePrzez"));

            var timeFactory = new FrameworkElementFactory(typeof(TextBlock));
            timeFactory.SetValue(TextBlock.FontSizeProperty, 11.0);
            timeFactory.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(139, 148, 158)));
            timeFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("UtworzonoGodzina"));

            textContainerFactory.AppendChild(textFactory);
            textContainerFactory.AppendChild(timeFactory);

            utworzonoTemplate.VisualTree = textContainerFactory;
            utworzonoColumn.CellTemplate = utworzonoTemplate;
            dgOrders.Columns.Add(utworzonoColumn);
            _utworzonoColIndex = -1; // brak avatara → loader zdjęć twórcy pomijany

            // Termin — wyśrodkowany (jak reszta tabeli)
            var terminLeftStyle = new Style(typeof(TextBlock));
            terminLeftStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            terminLeftStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));

            // 9. Termin: plan "🚚 12:00 pon." + pod spodem PRAWDZIWA godzina wydania
            //     z Panelu Magazyniera (WydanoInfo z DataWydania) — mniejszą czcionką, na zielono
            var terminCol = new DataGridTemplateColumn
            {
                Header = "Termin",
                Width = new DataGridLength(96),
                SortMemberPath = "TerminSort", // pełna data+godzina → od najwcześniejszej do następnego dnia

                IsReadOnly = true
            };
            var terminStack = new FrameworkElementFactory(typeof(StackPanel));
            terminStack.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
            terminStack.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);
            terminStack.SetValue(StackPanel.HorizontalAlignmentProperty, HorizontalAlignment.Center);

            var terminTxt = new FrameworkElementFactory(typeof(TextBlock));
            terminTxt.SetValue(TextBlock.FontSizeProperty, 15.0); // większy termin awizacji
            terminTxt.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            // Ikona zależna od transportu: 🏭 awizacja na zakładzie / 🚗 klient odbiera własnym autem
            var terminMb = new System.Windows.Data.MultiBinding { Converter = new TerminIkonaConverter() };
            terminMb.Bindings.Add(new System.Windows.Data.Binding("TerminInfo"));
            terminMb.Bindings.Add(new System.Windows.Data.Binding("MaWlasnyTransport"));
            terminTxt.SetBinding(TextBlock.TextProperty, terminMb);
            terminTxt.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            // Tooltip przez Style — lokalny SetValue wygrałby z triggerem
            var terminTipStyle = new Style(typeof(TextBlock));
            terminTipStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, "Awizacja — godzina i dzień odbioru na zakładzie"));
            var autoTipTrig = new DataTrigger { Binding = new System.Windows.Data.Binding("MaWlasnyTransport"), Value = true };
            autoTipTrig.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, "Transport własny — klient przyjeżdża o tej godzinie"));
            terminTipStyle.Triggers.Add(autoTipTrig);
            terminTxt.SetValue(FrameworkElement.StyleProperty, terminTipStyle);
            terminStack.AppendChild(terminTxt);

            // Realne wydanie — styl przez Style (settery + trigger), bo lokalne SetValue wygrałoby z triggerem
            var wydanoStyle = new Style(typeof(TextBlock));
            wydanoStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 11.0));
            wydanoStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
            wydanoStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32))));
            wydanoStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            wydanoStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, "Rzeczywista godzina wydania (Panel Magazyniera)"));
            var brakWydaniaTrig = new DataTrigger { Binding = new System.Windows.Data.Binding("WydanoInfo"), Value = "" };
            brakWydaniaTrig.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Collapsed));
            wydanoStyle.Triggers.Add(brakWydaniaTrig);
            // Wydanie PO czasie awizacji → czerwone zamiast zielonego
            var poTerminieTrig = new DataTrigger { Binding = new System.Windows.Data.Binding("WydanePoTerminie"), Value = true };
            poTerminieTrig.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0xC0, 0x39, 0x2B))));
            poTerminieTrig.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, "Wydano PO czasie awizacji (Panel Magazyniera)"));
            wydanoStyle.Triggers.Add(poTerminieTrig);
            // Zaznaczony wiersz → biały tekst (zielony ginie na niebieskim tle selekcji)
            var wydanoSelTrig = new DataTrigger
            {
                Binding = new System.Windows.Data.Binding("IsSelected")
                {
                    RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGridRow), 1)
                },
                Value = true
            };
            wydanoSelTrig.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brushes.White));
            wydanoStyle.Triggers.Add(wydanoSelTrig);

            var wydanoTxt = new FrameworkElementFactory(typeof(TextBlock));
            wydanoTxt.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("WydanoInfo") { Converter = new WydanieAutoConverter() });
            wydanoTxt.SetValue(FrameworkElement.StyleProperty, wydanoStyle);
            terminStack.AppendChild(wydanoTxt);

            terminCol.CellTemplate = new DataTemplate { VisualTree = terminStack };
            dgOrders.Columns.Add(terminCol);

            // Szerokości kolumn + szerokość lewej kolumny układu — zależne od szerokości okna
            ApplyOrdersLayout();
        }

        // Responsywna tabela zamówień: przy zwężaniu okna kolumny (w tym Zam./wyd) kurczą się progami,
        // ale tabela ZAWSZE mieści wszystkie kolumny (MinWidth lewej kolumny = suma szerokości).
        private void ApplyOrdersLayout()
        {
            if (dgOrders == null || dgOrders.Columns.Count == 0 || leftColumnDef == null) return;

            double winW = ActualWidth;
            int tier = winW < 1280 ? 2 : winW < 1520 ? 1 : 0; // 0 = pełny, 1 = kompakt, 2 = mini

            void Ustaw(string header, double w0, double w1, double w2)
            {
                var c = dgOrders.Columns.FirstOrDefault(x => (x.Header?.ToString() ?? "") == header);
                if (c != null) c.Width = new DataGridLength(tier == 0 ? w0 : tier == 1 ? w1 : w2);
            }
            Ustaw("Odbiorca", 252, 214, 182);  // szersza — większa czcionka
            Ustaw("Zam.", 104, 88, 76);         // razem z poddrukiem "wydano X kg"
            Ustaw("Cena", 80, 70, 60);          // jednostka "zł/kg" jest pod liczbą
            Ustaw("Utworzono", 108, 98, 88);
            Ustaw("Termin", 112, 98, 86);

            double tableW = 0;
            foreach (var col in dgOrders.Columns)
                if (col.Visibility == Visibility.Visible && col.Width.IsAbsolute)
                    tableW += col.Width.Value;
            tableW += 64; // marginesy TabControl, ramki, pionowy scrollbar
            if (tableW > 100)
            {
                leftColumnDef.MinWidth = tableW;
                if (!leftColumnDef.Width.IsStar && leftColumnDef.Width.Value != tableW)
                    leftColumnDef.Width = new GridLength(tableW);
            }

            // Panel "Ostatnie zmiany kg" chowamy przy wąskim oknie, by NIE zakrywał szczegółów zamówienia
            if (panelZmianyKg != null)
                panelZmianyKg.Visibility = winW < 1500 ? Visibility.Collapsed : Visibility.Visible;
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
                // Ukryj prawy panel i rozszerz lewy (tylko jeśli nie był ukryty)
                if (!_rightPanelHidden)
                {
                    _savedRightColumnWidth = rightColumnDef.Width;
                    rightPanel.Visibility = Visibility.Collapsed;
                    rightColumnDef.Width = new GridLength(0);
                    leftColumnDef.Width = new GridLength(1, GridUnitType.Star);
                    _rightPanelHidden = true;
                }

                // Lazy loading - załaduj dane Dashboard tylko gdy zakładka jest aktywna
                if (!_dashboardLoaded)
                {
                    _ = LoadDashboardDataAsync(_selectedDate);
                    _dashboardLoaded = true;
                }
            }
            else if (selectedTab == tabStatystyki)
            {
                // Ukryj prawy panel dla statystyk (tylko jeśli nie był ukryty)
                if (!_rightPanelHidden)
                {
                    _savedRightColumnWidth = rightColumnDef.Width;
                    rightPanel.Visibility = Visibility.Collapsed;
                    rightColumnDef.Width = new GridLength(0);
                    leftColumnDef.Width = new GridLength(1, GridUnitType.Star);
                    _rightPanelHidden = true;
                }

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
                // Przywróć prawy panel tylko jeśli był ukryty
                if (_rightPanelHidden)
                {
                    rightPanel.Visibility = Visibility.Visible;
                    rightColumnDef.Width = _savedRightColumnWidth;
                    _rightPanelHidden = false;
                }

                // Lazy loading - załaduj transport tylko gdy zakładka jest aktywna
                if (!_transportLoaded)
                {
                    _ = LoadTransportForDayAsync(_selectedDate);
                    _transportLoaded = true;
                }
            }
            else if (selectedTab?.Header?.ToString()?.Contains("Historia") == true)
            {
                // Przywróć prawy panel tylko jeśli był ukryty
                if (_rightPanelHidden)
                {
                    rightPanel.Visibility = Visibility.Visible;
                    rightColumnDef.Width = _savedRightColumnWidth;
                    _rightPanelHidden = false;
                }

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
                // Przywróć prawy panel tylko jeśli był ukryty
                if (_rightPanelHidden)
                {
                    rightPanel.Visibility = Visibility.Visible;
                    rightColumnDef.Width = _savedRightColumnWidth;
                    _rightPanelHidden = false;
                }
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
            // ✅ Wyłącz clipping — avatary mogą wychodzić poza wiersz
            e.Row.ClipToBounds = false;

            if (e.Row.Item is DataRowView rowView)
            {
                // ⚠ RESET — kontenery wierszy są RECYKLOWANE przy przewijaniu/odświeżaniu.
                // Bez wyczyszczenia wiersz po "czerwonym z białą czcionką" (>34 palet) albo po
                // "Anulowane" (szary, kursywa) zostawia swoje style następnemu zamówieniu.
                e.Row.ClearValue(Control.BackgroundProperty);
                e.Row.ClearValue(Control.ForegroundProperty);
                e.Row.ClearValue(Control.FontWeightProperty);
                e.Row.ClearValue(Control.FontStyleProperty);
                e.Row.ClearValue(FrameworkElement.CursorProperty);
                e.Row.ClearValue(FrameworkElement.ToolTipProperty);
                e.Row.ClearValue(FrameworkElement.MinHeightProperty);

                var status = rowView.Row.Field<string>("Status") ?? "";
                var salesman = rowView.Row.Field<string>("Handlowiec") ?? "";
                var id = rowView.Row.Field<int>("Id");

                // Rozwinięte zamówienia grupy "Ogólne" — wiersze o 3 px niższe (MinRowHeight 40 → 37)
                var ogolneTyp = rowView.Row.Table.Columns.Contains("OgolneTyp")
                    ? rowView.Row.Field<string>("OgolneTyp") ?? "" : "";
                if (ogolneTyp == "detail")
                    e.Row.MinHeight = 37;

                if (id == -1 || status == "SUMA")
                {
                    // Subtelniejszy styl wiersza sumy - jasne tło, ciemny tekst
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(240, 245, 240));
                    e.Row.Foreground = new SolidColorBrush(Color.FromRgb(40, 60, 40));
                    e.Row.FontWeight = FontWeights.Bold;
                    return;
                }

                // Wiersz-nagłówek grupy "Ogólne" (▶/▼) — wyróżniony, klikalny (toggle zwijania)
                if (id == -7777)
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xED, 0xF2));
                    e.Row.Foreground = new SolidColorBrush(Color.FromRgb(0x2C, 0x3E, 0x50));
                    e.Row.FontWeight = FontWeights.Bold;
                    e.Row.Cursor = System.Windows.Input.Cursors.Hand;
                    e.Row.ToolTip = "Kliknij strzałkę ▶/▼ (na początku wiersza), aby rozwinąć/zwinąć zamówienia Ogólne";
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

                // Load avatar for Utworzono column
                var userId = rowView.Row.Field<string>("UtworzonePrzezID");
                if (!string.IsNullOrEmpty(userId))
                {
                    LoadAvatarForOrderRow(e.Row, userId);
                }
            }
        }

        private void LoadAvatarForOrderRow(DataGridRow row, string userId)
        {
            Task.Run(() =>
            {
                var avatar = UserAvatarManager.GetAvatar(userId);
                if (avatar != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            var presenter = FindVisualChild<DataGridCellsPresenter>(row);
                            if (presenter != null && _utworzonoColIndex >= 0)
                            {
                                // Dynamiczny indeks kolumny Utworzono (ustawiany w SetupOrdersDataGrid)
                                var cell = presenter.ItemContainerGenerator.ContainerFromIndex(_utworzonoColIndex) as DataGridCell;
                                if (cell != null)
                                {
                                    var ellipse = FindVisualChild<Ellipse>(cell);
                                    if (ellipse != null && ellipse.Name == "avatarEllipse")
                                    {
                                        var imageSource = ConvertToImageSource(avatar);
                                        if (imageSource != null)
                                        {
                                            ellipse.Fill = new ImageBrush(imageSource) { Stretch = Stretch.UniformToFill };
                                            ellipse.Visibility = Visibility.Visible;
                                        }
                                    }
                                }
                            }
                        }
                        catch { }
                    });
                }
            });
        }

        private T FindVisualChild<T>(DependencyObject parent, string name = null) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                {
                    if (name == null || (child is FrameworkElement fe && fe.Name == name))
                        return typedChild;
                }

                var found = FindVisualChild<T>(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private ImageSource ConvertToImageSource(System.Drawing.Image image)
        {
            using (var memory = new System.IO.MemoryStream())
            {
                image.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                memory.Position = 0;
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = memory;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
                return bitmapImage;
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

            dgHistoriaZmian.Columns.Add(new DataGridTextColumn
            {
                Header = "Data uboju",
                Binding = new System.Windows.Data.Binding("DataUboju") { StringFormat = "yyyy-MM-dd" },
                Width = new DataGridLength(90),
                ElementStyle = (Style)FindResource("CenterAlignedCellStyle")
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
                _dtHistoriaZmian.Columns.Add("DataUboju", typeof(DateTime));
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

            // Pobierz mapowanie zamówień do klientów i DataUboju
            var orderToClient = new Dictionary<int, int>();
            var orderToDataUboju = new Dictionary<int, DateTime>();
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
                string sqlOrders = $@"SELECT Id, KlientId, DataUboju FROM dbo.ZamowieniaMieso
                                     WHERE {dateColumn} BETWEEN @StartDate AND @EndDate";
                await using var cmdOrders = new SqlCommand(sqlOrders, cnLibra);
                cmdOrders.Parameters.AddWithValue("@StartDate", startDate);
                cmdOrders.Parameters.AddWithValue("@EndDate", endDate);
                await using var rdrOrders = await cmdOrders.ExecuteReaderAsync();

                while (await rdrOrders.ReadAsync())
                {
                    int orderId = rdrOrders.GetInt32(0);
                    int clientId = rdrOrders.GetInt32(1);
                    DateTime dataUboju = rdrOrders.IsDBNull(2) ? DateTime.MinValue : rdrOrders.GetDateTime(2);
                    orderToClient[orderId] = clientId;
                    orderToDataUboju[orderId] = dataUboju;
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
                    DateTime dataUboju = DateTime.MinValue;
                    if (orderToClient.TryGetValue(zamowienieId, out int clientId) &&
                        contractors.TryGetValue(clientId, out var contr))
                    {
                        handlowiec = contr.Salesman;
                        odbiorca = contr.Name;
                    }
                    if (orderToDataUboju.TryGetValue(zamowienieId, out var du))
                    {
                        dataUboju = du;
                    }

                    _dtHistoriaZmian.Rows.Add(id, zamowienieId, dataZmiany, typZmiany,
                        handlowiec, odbiorca, uzytkownikNazwa, opisZmiany, dataUboju);
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
            var selectedDataUboju = cmbHistoriaDataUboju?.SelectedItem?.ToString();

            // Pobierz unikalne wartości z danych
            var ktoEdytowalList = new List<string> { "(Wszystkie)" };
            var odbiorcaList = new List<string> { "(Wszystkie)" };
            var typList = new List<string> { "(Wszystkie)" };
            var handlowiecList = new List<string> { "(Wszystkie)" };
            var towarList = new List<string> { "(Wszystkie)" };
            var dataUbojuList = new List<string> { "(Wszystkie)" };

            foreach (DataRow row in _dtHistoriaZmian.Rows)
            {
                string kto = row["UzytkownikNazwa"]?.ToString() ?? "";
                string odbiorca = row["Odbiorca"]?.ToString() ?? "";
                string typ = row["TypZmiany"]?.ToString() ?? "";
                string handlowiec = row["Handlowiec"]?.ToString() ?? "";
                string opis = row["OpisZmiany"]?.ToString() ?? "";
                var dataUboju = row["DataUboju"] as DateTime?;
                string dataUbojuStr = dataUboju.HasValue && dataUboju.Value > DateTime.MinValue
                    ? dataUboju.Value.ToString("yyyy-MM-dd") : "";

                if (!string.IsNullOrWhiteSpace(kto) && !ktoEdytowalList.Contains(kto))
                    ktoEdytowalList.Add(kto);
                if (!string.IsNullOrWhiteSpace(odbiorca) && !odbiorcaList.Contains(odbiorca))
                    odbiorcaList.Add(odbiorca);
                if (!string.IsNullOrWhiteSpace(typ) && !typList.Contains(typ))
                    typList.Add(typ);
                if (!string.IsNullOrWhiteSpace(handlowiec) && !handlowiecList.Contains(handlowiec))
                    handlowiecList.Add(handlowiec);
                if (!string.IsNullOrWhiteSpace(dataUbojuStr) && !dataUbojuList.Contains(dataUbojuStr))
                    dataUbojuList.Add(dataUbojuStr);

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
            if (dataUbojuList.Count > 1)
            {
                var sorted = dataUbojuList.Skip(1).OrderByDescending(x => x).ToList();
                dataUbojuList = new List<string> { "(Wszystkie)" };
                dataUbojuList.AddRange(sorted);
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
            if (cmbHistoriaDataUboju != null)
            {
                cmbHistoriaDataUboju.ItemsSource = dataUbojuList;
                cmbHistoriaDataUboju.SelectedIndex = string.IsNullOrEmpty(selectedDataUboju) ? 0 :
                    Math.Max(0, dataUbojuList.IndexOf(selectedDataUboju));
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
            if (cmbHistoriaDataUboju != null) cmbHistoriaDataUboju.SelectedIndex = 0;
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

            // Filtr: Data uboju
            string dataUboju = cmbHistoriaDataUboju?.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(dataUboju) && dataUboju != "(Wszystkie)")
            {
                if (DateTime.TryParse(dataUboju, out var dt))
                {
                    filters.Add($"DataUboju = '{dt:yyyy-MM-dd}'");
                }
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

        // Wydania per (klient, towar) za dany dzień — źródło: LibraNet (Panel Magazyniera).
        // Wcześniej brało z HANDEL HM.MG/MZ (Sage). Zmiana 2026-05-11: magazynier oznacza wydanie w panelu,
        // wartość Wyd. powinna odzwierciedlać to co faktycznie wydał (z różnicami) — nie WZ-kę z Symfoni.
        //
        // Logika ilości per pozycja:
        //   - jeśli jest wpis w ZamowienieWydanieRoznice → IloscWydana (faktyczna, może != zamówiona)
        //   - inaczej → Ilosc z ZamowieniaMiesoTowar (wydano dokładnie ile zamówiono)
        // Filtr: tylko zamówienia z CzyWydane=1 i DataWydania = @Day.
        private async Task<Dictionary<int, Dictionary<int, decimal>>> GetReleasesPerClientProductAsync(DateTime day)
        {
            day = ValidateSqlDate(day);
            var dict = new Dictionary<int, Dictionary<int, decimal>>();
            if (!_productCatalogCache.Keys.Any()) return dict;

            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();

            // Tabela różnic tworzona jest dynamicznie przez panel magazyniera przy pierwszym zapisie
            // (Magazyn/Panel/MagazynPanel.xaml.cs:272). Bez niej — fallback do Ilosc z ZamowieniaMiesoTowar.
            bool hasRoznice;
            await using (var check = new SqlCommand(
                "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.objects WHERE name='ZamowienieWydanieRoznice' AND type='U') THEN 1 ELSE 0 END", cn))
            {
                hasRoznice = Convert.ToInt32(await check.ExecuteScalarAsync()) == 1;
            }

            var idwList = string.Join(",", _productCatalogCache.Keys);
            string joinRoznice = hasRoznice
                ? "LEFT JOIN dbo.ZamowienieWydanieRoznice r ON r.ZamowienieId = z.Id AND r.KodTowaru = zt.KodTowaru"
                : "";
            string iloscExpr = hasRoznice ? "ISNULL(r.IloscWydana, zt.Ilosc)" : "zt.Ilosc";

            string sql = $@"
                SELECT z.KlientId, zt.KodTowaru, SUM({iloscExpr}) AS qty
                FROM dbo.ZamowieniaMieso z
                INNER JOIN dbo.ZamowieniaMiesoTowar zt ON zt.ZamowienieId = z.Id
                {joinRoznice}
                WHERE z.CzyWydane = 1
                  AND CAST(z.DataWydania AS DATE) = @Day
                  AND z.KlientId IS NOT NULL
                  AND zt.KodTowaru IS NOT NULL
                  AND zt.KodTowaru IN ({idwList})
                  AND zt.Ilosc > 0
                GROUP BY z.KlientId, zt.KodTowaru";

            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@Day", day);
            await using var rdr = await cmd.ExecuteReaderAsync();

            while (await rdr.ReadAsync())
            {
                int clientId = rdr.IsDBNull(0) ? 0 : rdr.GetInt32(0);
                int productId = rdr.IsDBNull(1) ? 0 : Convert.ToInt32(rdr.GetValue(1));
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

        /// <summary>
        /// Wydania PER ZAMÓWIENIE z Panelu Magazyniera dla zamówień widocznych w danym dniu.
        /// Kg = faktycznie wydane (ZamowienieWydanieRoznice.IloscWydana jeśli magazynier zapisał różnice,
        /// inaczej pełne Ilosc z pozycji). Wszystko = flaga CzyWszystkoWydane którą zaznacza magazynier.
        /// </summary>
        private async Task<Dictionary<int, (Dictionary<int, decimal> PerProdukt, bool Wszystko)>> GetWydaniaPerOrderAsync(
            DateTime day, string dateColumnZm)
        {
            day = ValidateSqlDate(day);
            var dict = new Dictionary<int, (Dictionary<int, decimal> PerProdukt, bool Wszystko)>();
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                bool hasRoznice;
                await using (var check = new SqlCommand(
                    "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.objects WHERE name='ZamowienieWydanieRoznice' AND type='U') THEN 1 ELSE 0 END", cn))
                {
                    hasRoznice = Convert.ToInt32(await check.ExecuteScalarAsync()) == 1;
                }

                string joinRoznice = hasRoznice
                    ? "LEFT JOIN dbo.ZamowienieWydanieRoznice r ON r.ZamowienieId = zm.Id AND r.KodTowaru = zt.KodTowaru"
                    : "";
                string iloscExpr = hasRoznice ? "ISNULL(r.IloscWydana, zt.Ilosc)" : "zt.Ilosc";

                string sql = $@"
                    SELECT zm.Id, zt.KodTowaru, SUM({iloscExpr}) AS qty,
                           MIN(CAST(ISNULL(zm.CzyWszystkoWydane, 1) AS INT)) AS Wszystko
                    FROM dbo.ZamowieniaMieso zm
                    INNER JOIN dbo.ZamowieniaMiesoTowar zt ON zt.ZamowienieId = zm.Id
                    {joinRoznice}
                    WHERE {dateColumnZm} = @Day
                      AND zm.CzyWydane = 1
                      AND zt.KodTowaru IS NOT NULL
                      AND zt.Ilosc > 0
                    GROUP BY zm.Id, zt.KodTowaru";

                await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 30 };
                cmd.Parameters.AddWithValue("@Day", day);
                await using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    int zamId = rdr.GetInt32(0);
                    int productId = rdr.IsDBNull(1) ? 0 : Convert.ToInt32(rdr.GetValue(1));
                    decimal qty = rdr.IsDBNull(2) ? 0m : Convert.ToDecimal(rdr.GetValue(2));
                    bool wszystko = !rdr.IsDBNull(3) && Convert.ToInt32(rdr.GetValue(3)) == 1;

                    if (!dict.TryGetValue(zamId, out var entry))
                    {
                        entry = (new Dictionary<int, decimal>(), wszystko);
                        dict[zamId] = entry;
                    }
                    var perProdukt = entry.PerProdukt;
                    if (perProdukt.ContainsKey(productId))
                        perProdukt[productId] += qty;
                    else
                        perProdukt[productId] = qty;
                    // flaga "wszystko" — jeśli którakolwiek część mówi NIE, całość = NIE
                    dict[zamId] = (perProdukt, entry.Wszystko && wszystko);
                }
            }
            catch { /* brak danych = brak wydań — tabela pokaże 0 */ }

            return dict;
        }

        // ════════════════════════════════════════════════════════════════════
        //  Wąski panel "Ostatnie zmiany kg" (po prawej od szczegółów zamówienia)
        //  TOP 15 zmian ilości pozycji (PoleZmienione 'Pozycja:% - Zam.') dla zamówień
        //  z wybranego dnia uboju. Lekkie: 1 zapytanie, mapy klientów/towarów z cache, fire-and-forget.
        // ════════════════════════════════════════════════════════════════════
        private readonly DataTable _dtZmianyKg = new();
        private bool _zmianyKgGridReady;

        private void SetupZmianyKgDataGrid()
        {
            if (_zmianyKgGridReady) return;

            _dtZmianyKg.Columns.Add("Avatar", typeof(ImageSource));
            _dtZmianyKg.Columns.Add("Klient", typeof(string));
            _dtZmianyKg.Columns.Add("Godzina", typeof(string));   // "HH:mm • Użytkownik"
            _dtZmianyKg.Columns.Add("TowarImg", typeof(BitmapImage));
            _dtZmianyKg.Columns.Add("TowarNazwa", typeof(string)); // pod zdjęciem, mała czcionka
            _dtZmianyKg.Columns.Add("DeltaTxt", typeof(string));
            _dtZmianyKg.Columns.Add("DeltaKolor", typeof(string));
            _dtZmianyKg.Columns.Add("Strzalka", typeof(string));
            _dtZmianyKg.Columns.Add("NowaTxt", typeof(string));     // nowa ilość (nad +/-)
            _dtZmianyKg.Columns.Add("StaraTxt", typeof(string));    // stara ilość (skreślona, pod +/-)
            _dtZmianyKg.Columns.Add("Tooltip", typeof(string));

            dgZmianyKg.ItemsSource = _dtZmianyKg.DefaultView;
            dgZmianyKg.Columns.Clear();
            dgZmianyKg.MinRowHeight = 58; // wieloliniowo: nowa/+-/stara + nazwa towaru pod zdjęciem, większe czcionki

            // 1. Avatar osoby zmieniającej — okrągły, dopasowany (UniformToFill w ramce 38 px)
            var avCol = new DataGridTemplateColumn { Width = new DataGridLength(46), IsReadOnly = true };
            var avBorder = new FrameworkElementFactory(typeof(Border));
            avBorder.SetValue(FrameworkElement.WidthProperty, 38.0);
            avBorder.SetValue(FrameworkElement.HeightProperty, 38.0);
            avBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(19));
            avBorder.SetValue(UIElement.ClipToBoundsProperty, true);
            avBorder.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0xEC, 0xF0, 0xF1)));
            avBorder.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            avBorder.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            var avImg = new FrameworkElementFactory(typeof(System.Windows.Controls.Image));
            avImg.SetBinding(System.Windows.Controls.Image.SourceProperty, new System.Windows.Data.Binding("Avatar"));
            avImg.SetValue(System.Windows.Controls.Image.StretchProperty, Stretch.UniformToFill);
            avImg.SetValue(FrameworkElement.WidthProperty, 38.0);
            avImg.SetValue(FrameworkElement.HeightProperty, 38.0);
            avBorder.AppendChild(avImg);
            avCol.CellTemplate = new DataTemplate { VisualTree = avBorder };
            dgZmianyKg.Columns.Add(avCol);

            // 2. Klient (góra) + godzina zmiany (dół)
            var midCol = new DataGridTemplateColumn { Width = new DataGridLength(1, DataGridLengthUnitType.Star), IsReadOnly = true };
            var midStack = new FrameworkElementFactory(typeof(StackPanel));
            midStack.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
            midStack.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);
            var klientTxt = new FrameworkElementFactory(typeof(TextBlock));
            klientTxt.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Klient"));
            klientTxt.SetValue(TextBlock.FontSizeProperty, 14.0);
            klientTxt.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            klientTxt.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            midStack.AppendChild(klientTxt);
            // Godzina zmiany • nazwa użytkownika
            var godzTxt = new FrameworkElementFactory(typeof(TextBlock));
            godzTxt.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Godzina"));
            godzTxt.SetValue(TextBlock.FontSizeProperty, 12.0);
            godzTxt.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            godzTxt.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(90, 100, 110)));
            godzTxt.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            midStack.AppendChild(godzTxt);
            midCol.CellTemplate = new DataTemplate { VisualTree = midStack };
            dgZmianyKg.Columns.Add(midCol);

            // 3. Zdjęcie towaru + strzałka kierunku (▲/▼) na górze, nazwa towaru pod spodem (mała czcionka)
            var imgCol = new DataGridTemplateColumn { Width = new DataGridLength(78), IsReadOnly = true };
            var imgOuter = new FrameworkElementFactory(typeof(StackPanel));
            imgOuter.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
            imgOuter.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);
            imgOuter.SetValue(StackPanel.HorizontalAlignmentProperty, HorizontalAlignment.Center);

            var imgStack = new FrameworkElementFactory(typeof(StackPanel));
            imgStack.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            imgStack.SetValue(StackPanel.HorizontalAlignmentProperty, HorizontalAlignment.Center);

            var towarImg = new FrameworkElementFactory(typeof(System.Windows.Controls.Image));
            towarImg.SetBinding(System.Windows.Controls.Image.SourceProperty, new System.Windows.Data.Binding("TowarImg"));
            towarImg.SetValue(FrameworkElement.WidthProperty, 22.0);
            towarImg.SetValue(FrameworkElement.HeightProperty, 22.0);
            towarImg.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            imgStack.AppendChild(towarImg);

            var strzalka = new FrameworkElementFactory(typeof(TextBlock));
            strzalka.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Strzalka"));
            strzalka.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding("DeltaKolor") { Converter = new HexToBrushSafeConverter() });
            strzalka.SetValue(TextBlock.FontSizeProperty, 12.0);
            strzalka.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            strzalka.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            strzalka.SetValue(FrameworkElement.MarginProperty, new Thickness(2, 0, 0, 0));
            imgStack.AppendChild(strzalka);
            imgOuter.AppendChild(imgStack);

            // Nazwa towaru — pod zdjęciem, mała czcionka
            var towarNazwa = new FrameworkElementFactory(typeof(TextBlock));
            towarNazwa.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("TowarNazwa"));
            towarNazwa.SetValue(TextBlock.FontSizeProperty, 8.5);
            towarNazwa.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(120, 128, 138)));
            towarNazwa.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            towarNazwa.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
            towarNazwa.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            towarNazwa.SetValue(TextBlock.MaxWidthProperty, 76.0);
            imgOuter.AppendChild(towarNazwa);

            imgCol.CellTemplate = new DataTemplate { VisualTree = imgOuter };
            dgZmianyKg.Columns.Add(imgCol);

            // 4. Nowa ilość (góra, kolor zmiany) / +/- kg (środek, bold) / stara ilość skreślona (dół, szara)
            var deltaCol = new DataGridTemplateColumn { Width = new DataGridLength(96), IsReadOnly = true };
            var deltaStack = new FrameworkElementFactory(typeof(StackPanel));
            deltaStack.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
            deltaStack.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);
            deltaStack.SetValue(StackPanel.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            deltaStack.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 0, 0, 0));

            // Nowa ilość — na górze, w kolorze zmiany (zielony/czerwony)
            var nowaTxt = new FrameworkElementFactory(typeof(TextBlock));
            nowaTxt.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("NowaTxt"));
            nowaTxt.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding("DeltaKolor") { Converter = new HexToBrushSafeConverter() });
            nowaTxt.SetValue(TextBlock.FontSizeProperty, 12.0);
            nowaTxt.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            nowaTxt.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            deltaStack.AppendChild(nowaTxt);

            // +/- kg — środek, bold, w kolorze zmiany
            var deltaTxt = new FrameworkElementFactory(typeof(TextBlock));
            deltaTxt.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("DeltaTxt"));
            deltaTxt.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding("DeltaKolor") { Converter = new HexToBrushSafeConverter() });
            deltaTxt.SetValue(TextBlock.FontSizeProperty, 11.0);
            deltaTxt.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            deltaTxt.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            deltaStack.AppendChild(deltaTxt);

            // Stara ilość — dół, szara, PRZEKREŚLONA
            var staraTxt = new FrameworkElementFactory(typeof(TextBlock));
            staraTxt.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("StaraTxt"));
            staraTxt.SetValue(TextBlock.FontSizeProperty, 9.5);
            staraTxt.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(139, 148, 158)));
            staraTxt.SetValue(TextBlock.TextDecorationsProperty, TextDecorations.Strikethrough);
            staraTxt.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            deltaStack.AppendChild(staraTxt);

            deltaCol.CellTemplate = new DataTemplate { VisualTree = deltaStack };
            dgZmianyKg.Columns.Add(deltaCol);

            _zmianyKgGridReady = true;
        }

        private async Task LoadZmianyKgAsync(DateTime day)
        {
            try
            {
                SetupZmianyKgDataGrid();
                day = ValidateSqlDate(day);
                string dateCol = (_showBySlaughterDate && _slaughterDateColumnExists) ? "DataUboju" : "DataZamowienia";

                var wpisy = new List<(int klientId, string towar, decimal stara, decimal nowa, decimal delta, DateTime kiedy, string userId, string userNazwa)>();

                await using (var cn = new SqlConnection(_connLibra))
                {
                    await cn.OpenAsync();
                    // Tabela tworzona dynamicznie — jeśli brak, panel zostaje pusty
                    bool hasHist;
                    await using (var check = new SqlCommand(
                        "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.objects WHERE name='HistoriaZmianZamowien' AND type='U') THEN 1 ELSE 0 END", cn))
                    {
                        hasHist = Convert.ToInt32(await check.ExecuteScalarAsync()) == 1;
                    }
                    if (!hasHist) { _dtZmianyKg.Rows.Clear(); return; }

                    // TOP 50 — filtr towaru robimy w pamięci (PoleZmienione trzyma nazwę), potem przycinamy do 15
                    string sql = $@"
                        SELECT TOP 50 zm.KlientId, h.PoleZmienione, h.WartoscPoprzednia, h.WartoscNowa,
                               h.DataZmiany, h.Uzytkownik, h.UzytkownikNazwa
                        FROM dbo.HistoriaZmianZamowien h
                        JOIN dbo.ZamowieniaMieso zm ON zm.Id = h.ZamowienieId
                        WHERE h.TypZmiany = 'EDYCJA'
                          AND h.PoleZmienione LIKE N'Pozycja:% - Zam.'
                          AND zm.{dateCol} = @Day
                        ORDER BY h.DataZmiany DESC";
                    await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 20 };
                    cmd.Parameters.AddWithValue("@Day", day);
                    await using var rd = await cmd.ExecuteReaderAsync();
                    while (await rd.ReadAsync())
                    {
                        int klientId = rd.IsDBNull(0) ? 0 : rd.GetInt32(0);
                        string pole = rd.IsDBNull(1) ? "" : rd.GetString(1);
                        decimal? stara = ParseKgZmiana(rd.IsDBNull(2) ? null : rd.GetString(2));
                        decimal? nowa = ParseKgZmiana(rd.IsDBNull(3) ? null : rd.GetString(3));
                        DateTime kiedy = rd.GetDateTime(4);
                        string userId = rd.IsDBNull(5) ? "" : rd.GetString(5);
                        string userNazwa = rd.IsDBNull(6) ? "" : rd.GetString(6);
                        if (!stara.HasValue || !nowa.HasValue || stara.Value == nowa.Value) continue;

                        string towar = WyciagnijTowarZPola(pole);
                        wpisy.Add((klientId, towar, stara.Value, nowa.Value, nowa.Value - stara.Value, kiedy, userId, userNazwa));
                    }
                }

                // Mapy z cache (zero dodatkowych zapytań): kod towaru ← nazwa, klient ← nazwa
                var nazwaDoKodu = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in _productCatalogCache)
                    if (!nazwaDoKodu.ContainsKey(kv.Value)) nazwaDoKodu[kv.Value] = kv.Key;

                // Filtr towaru — jeśli w Podsumowaniu dnia wybrano konkretny towar, pokazuj tylko jego zmiany
                IEnumerable<(int klientId, string towar, decimal stara, decimal nowa, decimal delta, DateTime kiedy, string userId, string userNazwa)> widoczne = wpisy;
                if (_selectedProductId.HasValue)
                {
                    int sel = _selectedProductId.Value;
                    widoczne = wpisy.Where(x => nazwaDoKodu.TryGetValue(x.towar, out int k) && k == sel);
                }

                _dtZmianyKg.Rows.Clear();
                foreach (var w in widoczne.Take(15))
                {
                    string klientNazwa = _cachedKontrahenci.TryGetValue(w.klientId, out var c) ? c.Name : $"KH {w.klientId}";

                    BitmapImage towarImg = null;
                    if (nazwaDoKodu.TryGetValue(w.towar, out int kod))
                        towarImg = GetProductImage(kod);

                    ImageSource avatar = null;
                    if (!string.IsNullOrEmpty(w.userId))
                        try
                        {
                            using var drawingImg = UserAvatarManager.GetAvatar(w.userId);
                            if (drawingImg != null) avatar = ConvertToImageSource(drawingImg);
                        }
                        catch { }

                    string deltaTxt = (w.delta > 0 ? "+" : "−") + $"{Math.Abs(w.delta):N0} kg";
                    string deltaKolor = w.delta > 0 ? "#1E8449" : "#C0392B";
                    string strzalka = w.delta > 0 ? "▲" : "▼";

                    // Imię użytkownika (pierwszy człon) przy godzinie
                    string userImie = !string.IsNullOrWhiteSpace(w.userNazwa)
                        ? (w.userNazwa.Contains(' ') ? w.userNazwa.Split(' ')[0] : w.userNazwa)
                        : w.userId;

                    var row = _dtZmianyKg.NewRow();
                    row["Avatar"] = (object)avatar ?? DBNull.Value;
                    row["Klient"] = klientNazwa;
                    row["Godzina"] = string.IsNullOrWhiteSpace(userImie) ? w.kiedy.ToString("HH:mm") : $"{w.kiedy:HH:mm} • {userImie}";
                    row["TowarImg"] = (object)towarImg ?? DBNull.Value;
                    row["TowarNazwa"] = w.towar;
                    row["DeltaTxt"] = deltaTxt;
                    row["DeltaKolor"] = deltaKolor;
                    row["Strzalka"] = strzalka;
                    row["NowaTxt"] = $"{w.nowa:N0} kg";
                    row["StaraTxt"] = $"{w.stara:N0} kg";
                    row["Tooltip"] = $"{klientNazwa} • {w.towar} • {w.stara:N0} → {w.nowa:N0} kg ({deltaTxt}) • {w.kiedy:HH:mm} • {w.userNazwa}";
                    _dtZmianyKg.Rows.Add(row);
                }
            }
            catch { /* panel pomocniczy — błąd nie blokuje głównego widoku */ }
        }

        private static decimal? ParseKgZmiana(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string s = raw.Replace("kg", "", StringComparison.OrdinalIgnoreCase).Replace(" ", "").Replace(" ", "").Trim();
            if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var v)) return v;
            if (decimal.TryParse(s.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out v)) return v;
            return null;
        }

        private static string WyciagnijTowarZPola(string pole)
        {
            const string pre = "Pozycja: ";
            const string suf = " - Zam.";
            if (pole.StartsWith(pre) && pole.EndsWith(suf) && pole.Length > pre.Length + suf.Length)
                return pole.Substring(pre.Length, pole.Length - pre.Length - suf.Length);
            return pole;
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
                var orderItems = new List<(int ProductCode, decimal Quantity, bool Foil, bool Hallal, string Cena, bool Strefa, string Wariant)>(); // STRING!
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
                SELECT KodTowaru, Ilosc, ISNULL(Folia, 0) as Folia, ISNULL(Hallal, 0) as Hallal, ISNULL(Cena, '0') as Cena" +
                (_strefaColumnExists ? ", ISNULL(Strefa, 0) as Strefa" : "") +
                (_wariantColumnExists ? ", Wariant" : "") + @"
                FROM dbo.ZamowieniaMiesoTowar
                WHERE ZamowienieId = @Id", cn))
                    {
                        cmdItems.Parameters.AddWithValue("@Id", orderId);
                        using var readerItems = await cmdItems.ExecuteReaderAsync();
                        int wariantIdx = _wariantColumnExists ? (_strefaColumnExists ? 6 : 5) : -1;

                        while (await readerItems.ReadAsync())
                        {
                            int productCode = readerItems.GetInt32(0);
                            decimal quantity = readerItems.IsDBNull(1) ? 0m : readerItems.GetDecimal(1);
                            bool foil = readerItems.GetBoolean(2);
                            bool hallal = readerItems.GetBoolean(3);
                            string cenaStr = readerItems.GetString(4); // STRING z bazy!
                            bool strefa = _strefaColumnExists ? readerItems.GetBoolean(5) : false;
                            string wariant = (wariantIdx >= 0 && !readerItems.IsDBNull(wariantIdx)) ? readerItems.GetString(wariantIdx) : "";

                            orderItems.Add((productCode, quantity, foil, hallal, cenaStr, strefa, wariant));
                        }
                    }
                }

                // Wydano per pozycja — z PANELU MAGAZYNIERA (nie z dokumentów WZ w HANDEL):
                // zamówienie wydane (CzyWydane=1) → IloscWydana z ZamowienieWydanieRoznice
                // (magazynier zapisuje różnice gdy nie wszystko poszło), fallback = pełne Ilosc pozycji.
                var releases = new Dictionary<int, decimal>();
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();

                    bool hasRoznice;
                    await using (var check = new SqlCommand(
                        "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.objects WHERE name='ZamowienieWydanieRoznice' AND type='U') THEN 1 ELSE 0 END", cn))
                    {
                        hasRoznice = Convert.ToInt32(await check.ExecuteScalarAsync()) == 1;
                    }

                    string joinRoznice = hasRoznice
                        ? "LEFT JOIN dbo.ZamowienieWydanieRoznice r ON r.ZamowienieId = zm.Id AND r.KodTowaru = zt.KodTowaru"
                        : "";
                    string iloscExpr = hasRoznice ? "ISNULL(r.IloscWydana, zt.Ilosc)" : "zt.Ilosc";

                    string sql = $@"
                        SELECT zt.KodTowaru, SUM({iloscExpr})
                        FROM dbo.ZamowieniaMieso zm
                        INNER JOIN dbo.ZamowieniaMiesoTowar zt ON zt.ZamowienieId = zm.Id
                        {joinRoznice}
                        WHERE zm.Id = @Id
                          AND zm.CzyWydane = 1
                          AND zt.KodTowaru IS NOT NULL
                        GROUP BY zt.KodTowaru";

                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Id", orderId);
                    using var rd = await cmd.ExecuteReaderAsync();
                    while (await rd.ReadAsync())
                    {
                        int productId = rd.IsDBNull(0) ? 0 : rd.GetInt32(0);
                        decimal quantity = rd.IsDBNull(1) ? 0m : Convert.ToDecimal(rd.GetValue(1));
                        releases[productId] = quantity;
                    }
                }

                var dt = new DataTable();
                dt.Columns.Add("KodTowaru", typeof(int));  // Hidden - do identyfikacji
                dt.Columns.Add("Produkt", typeof(string));
                dt.Columns.Add("ProduktImg", typeof(BitmapImage));
                dt.Columns.Add("Zamówiono", typeof(decimal));
                dt.Columns.Add("Wydano", typeof(decimal));
                dt.Columns.Add("Różnica", typeof(decimal));
                dt.Columns.Add("Folia", typeof(bool));
                dt.Columns.Add("Hallal", typeof(bool));
                dt.Columns.Add("Strefa", typeof(bool));
                dt.Columns.Add("Cena", typeof(decimal));

                var cultureInfo = new CultureInfo("pl-PL");

                // Mapa wariantów (Kod→Nazwa per towar) — tylko gdy jakaś pozycja ma wariant
                Dictionary<int, List<Zamowienia.Services.TowarWariantyService.Wariant>>? wariantMapa = null;
                if (_wariantColumnExists && orderItems.Any(i => !string.IsNullOrEmpty(i.Wariant)))
                {
                    try { wariantMapa = await new Zamowienia.Services.TowarWariantyService(_connLibra).GetMapaAsync(); }
                    catch { }
                }

                foreach (var item in orderItems)
                {
                    if (!_productCatalogCache.ContainsKey(item.ProductCode))
                        continue;

                    string product = _productCatalogCache.TryGetValue(item.ProductCode, out var code) ?
                        code : $"Nieznany ({item.ProductCode})";
                    // Dopisz wariant (np. "Filet A · Podwójny") — widoczne dla produkcji/magazynu
                    if (!string.IsNullOrEmpty(item.Wariant))
                    {
                        string wn = item.Wariant;
                        if (wariantMapa != null && wariantMapa.TryGetValue(item.ProductCode, out var wlist))
                        {
                            var wdef = wlist.FirstOrDefault(x => x.Kod == item.Wariant);
                            if (wdef != null) wn = wdef.Nazwa;
                        }
                        product += $"  ·  🔀 {wn}";
                    }
                    decimal ordered = item.Quantity;
                    decimal released = releases.TryGetValue(item.ProductCode, out var w) ? w : 0m;
                    decimal difference = released - ordered;

                    // Konwertuj string na decimal dla ceny
                    decimal cenaValue = 0m;
                    if (!string.IsNullOrWhiteSpace(item.Cena))
                    {
                        decimal.TryParse(item.Cena, NumberStyles.Any, CultureInfo.InvariantCulture, out cenaValue);
                    }

                    dt.Rows.Add(item.ProductCode, product, (object?)GetProductImage(item.ProductCode) ?? DBNull.Value, ordered, released, difference, item.Foil, item.Hallal, item.Strefa, cenaValue);
                    releases.Remove(item.ProductCode);
                }

                foreach (var kv in releases)
                {
                    if (!_productCatalogCache.ContainsKey(kv.Key))
                        continue;

                    string product = _productCatalogCache.TryGetValue(kv.Key, out var code) ?
                        code : $"Nieznany ({kv.Key})";
                    dt.Rows.Add(kv.Key, product, (object?)GetProductImage(kv.Key) ?? DBNull.Value, 0m, kv.Value, kv.Value, false, false, false, 0m);
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

                // Usunięto txtOrderInfo (ID, Pozycji, Suma) - więcej miejsca na dane
                if (txtOrderInfo != null)
                {
                    txtOrderInfo.Text = "";
                }
                if (txtOrderClient != null)
                {
                    txtOrderClient.Text = !string.IsNullOrEmpty(clientName) ? clientName : "";
                }
                // Nazwa kontrahenta trafia do nagłówka kolumny "Produkt" w szczegółach zamówienia
                _detailsClientName = clientName ?? "";

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

            // Kolumna Produkt z miniaturką zdjęcia i kolorowymi ikonami statusu.
            // Nagłówek = nazwa kontrahenta (zamiast "Produkt"), gdy znana.
            bool maKlienta = !string.IsNullOrWhiteSpace(_detailsClientName);
            object naglowekProdukt;
            if (maKlienta)
            {
                // Nazwa klienta PODŚWIETLONA — żółte tło, pogrubiona, ciemny tekst
                naglowekProdukt = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0x82)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 2, 8, 2),
                    Child = new TextBlock
                    {
                        Text = _detailsClientName,
                        FontWeight = FontWeights.Bold,
                        FontSize = 14,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0x36, 0x00)),
                        TextTrimming = TextTrimming.CharacterEllipsis
                    }
                };
            }
            else naglowekProdukt = "Produkt";

            var produktCol = new DataGridTemplateColumn
            {
                Header = naglowekProdukt,
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = 80,
                IsReadOnly = true
            };
            var cellTemplate = new DataTemplate();
            var boolToVisConverter = new BooleanToVisibilityConverter();
            var spFactory = new FrameworkElementFactory(typeof(StackPanel));
            spFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            spFactory.SetValue(StackPanel.HorizontalAlignmentProperty, HorizontalAlignment.Right); // zdjęcie+towar do prawej
            spFactory.SetValue(StackPanel.MarginProperty, new Thickness(0, 0, 10, 0));            // z lekkim odstępem
            spFactory.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);
            var imgFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.Image));
            imgFactory.SetBinding(System.Windows.Controls.Image.SourceProperty, new System.Windows.Data.Binding("ProduktImg"));
            imgFactory.SetValue(System.Windows.Controls.Image.WidthProperty, 20.0);
            imgFactory.SetValue(System.Windows.Controls.Image.HeightProperty, 20.0);
            imgFactory.SetValue(System.Windows.Controls.Image.MarginProperty, new Thickness(0, 0, 4, 0));
            imgFactory.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.HighQuality);
            spFactory.AppendChild(imgFactory);
            // Ikona Strefa - czerwona
            var icoStrefa = new FrameworkElementFactory(typeof(TextBlock));
            icoStrefa.SetValue(TextBlock.TextProperty, "\u26A0\uFE0F");
            icoStrefa.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B)));
            icoStrefa.SetValue(TextBlock.FontSizeProperty, 11.0);
            icoStrefa.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 2, 0));
            icoStrefa.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            icoStrefa.SetBinding(TextBlock.VisibilityProperty, new System.Windows.Data.Binding("Strefa") { Converter = boolToVisConverter });
            spFactory.AppendChild(icoStrefa);
            // Ikona Halal - teal
            var icoHalal = new FrameworkElementFactory(typeof(TextBlock));
            icoHalal.SetValue(TextBlock.TextProperty, "\U0001F52A");
            icoHalal.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0x4D, 0xB6, 0xAC)));
            icoHalal.SetValue(TextBlock.FontSizeProperty, 11.0);
            icoHalal.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 2, 0));
            icoHalal.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            icoHalal.SetBinding(TextBlock.VisibilityProperty, new System.Windows.Data.Binding("Hallal") { Converter = boolToVisConverter });
            spFactory.AppendChild(icoHalal);
            // Ikona Folia - niebieska
            var icoFolia = new FrameworkElementFactory(typeof(TextBlock));
            icoFolia.SetValue(TextBlock.TextProperty, "\U0001F39E\uFE0F");
            icoFolia.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0x64, 0xB5, 0xF6)));
            icoFolia.SetValue(TextBlock.FontSizeProperty, 11.0);
            icoFolia.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 2, 0));
            icoFolia.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            icoFolia.SetBinding(TextBlock.VisibilityProperty, new System.Windows.Data.Binding("Folia") { Converter = boolToVisConverter });
            spFactory.AppendChild(icoFolia);
            var txtFactory = new FrameworkElementFactory(typeof(TextBlock));
            txtFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Produkt"));
            txtFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            spFactory.AppendChild(txtFactory);
            cellTemplate.VisualTree = spFactory;
            produktCol.CellTemplate = cellTemplate;
            dgDetails.Columns.Add(produktCol);

            // Styl: wyśrodkowanie w poziomie i w pionie (wszystkie kolumny poza towarem)
            var centerBoth = new Style(typeof(TextBlock));
            centerBoth.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            centerBoth.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));

            var centerCheck = new Style(typeof(CheckBox));
            centerCheck.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            centerCheck.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));

            // Edytowalna kolumna ilości — wyświetla "1 200 kg", edycja odporna na sufiks (konwerter + parser)
            var zamowioneBinding = new System.Windows.Data.Binding("Zamówiono")
            {
                Mode = System.Windows.Data.BindingMode.TwoWay,
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.LostFocus,
                Converter = new JednostkaEditConverter("kg", "#,##0")
            };
            dgDetails.Columns.Add(new DataGridTextColumn
            {
                Header = "Zam.",
                Binding = zamowioneBinding,
                Width = new DataGridLength(82),
                ElementStyle = centerBoth,
                IsReadOnly = false
            });

            _colWydDet = new DataGridTextColumn
            {
                Header = "Wyd.",
                Binding = new System.Windows.Data.Binding("Wydano") { StringFormat = "#,##0 kg" },
                Width = new DataGridLength(82),
                ElementStyle = centerBoth,
                IsReadOnly = true
            };
            dgDetails.Columns.Add(_colWydDet);

            _colRozDet = new DataGridTextColumn
            {
                Header = "Róż.",
                Binding = new System.Windows.Data.Binding("Różnica") { StringFormat = "#,##0 kg" },
                Width = new DataGridLength(82),
                ElementStyle = centerBoth,
                IsReadOnly = true
            };
            dgDetails.Columns.Add(_colRozDet);

            // Checkbox Folia
            var foliaBinding = new System.Windows.Data.Binding("Folia")
            {
                Mode = System.Windows.Data.BindingMode.TwoWay,
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged
            };
            dgDetails.Columns.Add(new DataGridCheckBoxColumn
            {
                Header = "\U0001F39E\uFE0F Folia",
                Binding = foliaBinding,
                Width = new DataGridLength(55),
                ElementStyle = centerCheck,
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
                Header = "\U0001F52A Halal",
                Binding = hallalBinding,
                Width = new DataGridLength(55),
                ElementStyle = centerCheck,
                IsReadOnly = false
            });

            // Checkbox Strefa
            var strefaBinding = new System.Windows.Data.Binding("Strefa")
            {
                Mode = System.Windows.Data.BindingMode.TwoWay,
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged
            };
            dgDetails.Columns.Add(new DataGridCheckBoxColumn
            {
                Header = "\u26A0\uFE0F Strefa",
                Binding = strefaBinding,
                Width = new DataGridLength(60),
                ElementStyle = centerCheck,
                IsReadOnly = false
            });

            // Edytowalna kolumna ceny — "24,50 zł/kg", edycja odporna na sufiks
            var cenaBinding = new System.Windows.Data.Binding("Cena")
            {
                Mode = System.Windows.Data.BindingMode.TwoWay,
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.LostFocus,
                Converter = new JednostkaEditConverter("zł/kg", "N2")
            };
            dgDetails.Columns.Add(new DataGridTextColumn
            {
                Header = "Cena",
                Binding = cenaBinding,
                Width = new DataGridLength(86),
                ElementStyle = centerBoth,
                IsReadOnly = false
            });

            // Responsywne kurczenie Wyd./Róż. przy wąskim oknie
            dgDetails.SizeChanged -= DgDetails_SizeChanged;
            dgDetails.SizeChanged += DgDetails_SizeChanged;
            ApplyDetailsLayout();

            // Podpinanie eventów
            dgDetails.CellEditEnding -= DgDetails_CellEditEnding;
            dgDetails.CellEditEnding += DgDetails_CellEditEnding;
            dgDetails.PreviewKeyDown -= DgDetails_PreviewKeyDown;
            dgDetails.PreviewKeyDown += DgDetails_PreviewKeyDown;

            // Obsługa kliknięć w checkboxy (Folia/Hallal/Strefa)
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
                Header = string.IsNullOrWhiteSpace(recipientName) ? "Produkt" : recipientName,
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

            // Załaduj zdjęcia produktów (jeśli jeszcze nie załadowane)
            await LoadProductImagesAsync();

            day = ValidateSqlDate(day);

            var dtAgg = new DataTable();
            dtAgg.Columns.Add("Produkt", typeof(string));
            dtAgg.Columns.Add("PlanowanyPrzychód", typeof(decimal));
            dtAgg.Columns.Add("FaktycznyPrzychód", typeof(decimal));
            dtAgg.Columns.Add("Stan", typeof(string));
            dtAgg.Columns.Add("Zamówienia", typeof(decimal));
            dtAgg.Columns.Add("Wydania", typeof(decimal));
            dtAgg.Columns.Add("Bilans", typeof(decimal));
            dtAgg.Columns.Add("DoSprzedania", typeof(decimal));
            dtAgg.Columns.Add("NadmiarVal", typeof(decimal));

            // Określ czy bilans ma uwzględniać wydania czy zamówienia
            bool uzywajWydan = rbBilansWydania?.IsChecked == true;

            // ✅ OPTYMALIZACJA: Konfiguracja + Harmonogram + OrderIds równolegle
            diagSw.Restart();
            var taskKonfWydajnosci = GetKonfiguracjaWydajnosciAsync(day);
            var konfiguracjaProduktow = await GetKonfiguracjaProduktowAsync(day); // cached = instant

            string dateColAgg = (_showBySlaughterDate && _slaughterDateColumnExists) ? "DataUboju" : "DataZamowienia";

            // Pobierz surowe dane z HarmonogramDostaw (obliczenia pojemników po załadowaniu konfiguracji)
            var taskHarmonogram = Task.Run(async () =>
            {
                decimal mass = 0m;
                var dostawy = new List<(decimal wagaDek, decimal zywiecKg)>();
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                const string sql = @"SELECT WagaDek, SztukiDek FROM dbo.HarmonogramDostaw
            WHERE DataOdbioru = @Day AND Bufor IN ('B.Wolny', 'B.Kontr.', 'Potwierdzony')";
                await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 10 };
                cmd.Parameters.AddWithValue("@Day", day.Date);
                await using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    var wagaDek = rdr.IsDBNull(0) ? 0m : Convert.ToDecimal(rdr.GetValue(0));
                    var sztukiDek = rdr.IsDBNull(1) ? 0m : Convert.ToDecimal(rdr.GetValue(1));
                    decimal zywiecKg = wagaDek * sztukiDek;
                    mass += zywiecKg;
                    if (wagaDek > 0 && zywiecKg > 0)
                        dostawy.Add((wagaDek, zywiecKg));
                }
                return (mass, dostawy);
            });

            var taskOrderIds = Task.Run(async () =>
            {
                var ids = new List<int>();
                await using var cnIds = new SqlConnection(_connLibra);
                await cnIds.OpenAsync();
                var sqlIds = $"SELECT Id FROM [dbo].[ZamowieniaMieso] WHERE {dateColAgg} = @Day AND Status <> 'Anulowane'";
                await using var cmdIds = new SqlCommand(sqlIds, cnIds);
                cmdIds.Parameters.AddWithValue("@Day", day.Date);
                await using var rdIds = await cmdIds.ExecuteReaderAsync();
                while (await rdIds.ReadAsync()) ids.Add(rdIds.GetInt32(0));
                return ids;
            });

            await Task.WhenAll(taskKonfWydajnosci, taskHarmonogram, taskOrderIds);

            var (wspolczynnikTuszki, procentA, procentB) = await taskKonfWydajnosci;
            var (totalMassDek, harmDostawy) = await taskHarmonogram;
            var orderIds = await taskOrderIds;
            diagTimes.Add(("Konfig+Harm+Ids(||)", diagSw.ElapsedMilliseconds));

            decimal pulaTuszki = totalMassDek * (wspolczynnikTuszki / 100m);
            decimal pulaTuszkiA = pulaTuszki * (procentA / 100m);
            decimal pulaTuszkiB = pulaTuszki * (procentB / 100m);

            // Oblicz pojemniki per klasa wagowa z rzeczywistą konfiguracją wydajności
            var pojemnikiKlasy = new int[13];
            foreach (var (wagaDek, zywiecKg) in harmDostawy)
            {
                decimal tuszkaASztuka = wagaDek * (wspolczynnikTuszki / 100m);
                decimal tuszkaAKg = zywiecKg * (wspolczynnikTuszki / 100m) * (procentA / 100m);
                if (tuszkaASztuka <= 0) continue;

                decimal sztukWPoj = 15m / tuszkaASztuka;
                decimal liczbaPoj = tuszkaAKg / 15m;
                int dolny = (int)Math.Floor(sztukWPoj);
                int gorny = (int)Math.Ceiling(sztukWPoj);
                decimal frac = sztukWPoj - dolny;

                int pojGorne = 0, pojDolne = 0;
                if (dolny == gorny)
                    pojDolne = (int)Math.Ceiling(liczbaPoj);
                else
                {
                    pojGorne = (int)Math.Round(liczbaPoj * frac);
                    pojDolne = (int)Math.Ceiling(liczbaPoj) - pojGorne;
                }

                pojemnikiKlasy[Math.Clamp(dolny, 5, 12)] += pojDolne;
                if (pojGorne > 0)
                    pojemnikiKlasy[Math.Clamp(gorny, 5, 12)] += pojGorne;
            }
            _pojemnikiKlasyPrognoza = pojemnikiKlasy;

            // ✅ OPTYMALIZACJA: Cache przychodów + równoległe zapytania
            diagSw.Restart();
            var actualIncomeTuszkaA = new Dictionary<int, decimal>();
            var actualIncomeElementy = new Dictionary<int, decimal>();
            var orderSum = new Dictionary<int, decimal>();

            // ✅ UŻYJ CACHE PRZYCHODÓW (jeśli dostępny) - oszczędza ~500-1000ms!
            bool przychodyFromCache = _cachedPrzychodyDate == day.Date;

            if (przychodyFromCache)
            {
                actualIncomeTuszkaA = _cachedPrzychodyTuszkaA;
                actualIncomeElementy = _cachedPrzychodyElementy;

                // Tylko zamówienia z LIBRA (przychody z cache)
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
            }
            else
            {
                // Zadanie 1: Przychody z HANDEL
                var taskPrzychody = Task.Run(async () =>
                {
                    var tuszkaA = new Dictionary<int, decimal>();
                    var elementy = new Dictionary<int, decimal>();
                    await using var cn = new SqlConnection(_connHandel);
                    await cn.OpenAsync();
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
                        if (typ == "T") tuszkaA[productId] = qty;
                        else elementy[productId] = qty;
                    }
                    return (tuszkaA, elementy);
                });

                // Zadanie 2: Zamówienia z LIBRA (równolegle!)
                var taskZamowienia = Task.Run(async () =>
                {
                    var orders = new Dictionary<int, decimal>();
                    if (orderIds.Any())
                    {
                        await using var cn = new SqlConnection(_connLibra);
                        await cn.OpenAsync();
                        var sql = $"SELECT KodTowaru, SUM(Ilosc) FROM [dbo].[ZamowieniaMiesoTowar] " +
                                 $"WHERE ZamowienieId IN ({string.Join(",", orderIds)}) GROUP BY KodTowaru";
                        using var cmd = new SqlCommand(sql, cn);
                        using var reader = await cmd.ExecuteReaderAsync();
                        while (await reader.ReadAsync())
                            orders[reader.GetInt32(0)] = reader.IsDBNull(1) ? 0m : reader.GetDecimal(1);
                    }
                    return orders;
                });

                await Task.WhenAll(taskPrzychody, taskZamowienia);
                var przychodResult = await taskPrzychody;
                actualIncomeTuszkaA = przychodResult.tuszkaA;
                actualIncomeElementy = przychodResult.elementy;
                orderSum = await taskZamowienia;

                // Zapisz do cache
                _cachedPrzychodyTuszkaA = actualIncomeTuszkaA;
                _cachedPrzychodyElementy = actualIncomeElementy;
                _cachedPrzychodyDate = day.Date;
            }
            diagTimes.Add(("PrzychZam" + (przychodyFromCache ? "©" : "(||)"), diagSw.ElapsedMilliseconds));

            // ✅ Rezerwacje klas wagowych na dany dzień (z RezerwacjeKlasWagowych)
            var taskRezerwacje = RezerwacjeKlasManager.PobierzZajetoscAsync(_connLibra, day);

            // ✅ OPTYMALIZACJA: Wydania + Stany równolegle
            diagSw.Restart();
            bool wydaniaFromCache = _cachedWydaniaDate == day.Date;

            var taskWydaniaAgg = Task.Run(async () =>
            {
                if (wydaniaFromCache) return _cachedWydaniaSum;

                var result = new Dictionary<int, decimal>();
                await using var cn = new SqlConnection(_connHandel);
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
                    result[productId] = qty;
                }
                _cachedWydaniaSum = result;
                _cachedWydaniaDate = day.Date;
                return result;
            });

            var taskStanyMag = Task.Run(async () =>
            {
                var result = new Dictionary<int, decimal>();
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
                        result[reader.GetInt32(0)] = reader.GetDecimal(1);
                    }
                }
                catch { }
                return result;
            });

            await Task.WhenAll(taskWydaniaAgg, taskStanyMag, taskRezerwacje);
            var wydaniaSum = await taskWydaniaAgg;
            var stanyMagazynowe = await taskStanyMag;
            var rezerwacjeKlas = await taskRezerwacje; // Dict<int,int>: klasa → zajęte pojemniki
            diagTimes.Add(("WydStanyRez(||)" + (wydaniaFromCache ? "©" : ""), diagSw.ElapsedMilliseconds));

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

            var produktyB = new List<(string nazwa, decimal plan, decimal fakt, decimal zam, decimal wyd, string stan, decimal stanDec, decimal bilans, int towarId)>();

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

                produktyB.Add((nazwaZIkonka, plan, fakt, zam, wyd, stanText, stan, balance, 0));

                // Jeśli grupa jest rozwinięta, dodaj szczegóły produktów
                if (isExpanded && _detaleGrup.TryGetValue(grupa.Key, out var detale))
                {
                    foreach (var detal in detale)
                    {
                        // Dodaj wydania = 0 dla szczegółów (nie mamy ich w detaleGrup)
                        produktyB.Add((detal.Item1, detal.Item2, detal.Item3, detal.Item4, 0m, detal.Item5, detal.Item6, detal.Item7, 0));
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

                produktyB.Add((nazwaZIkonka, plannedForProduct, actual, orders, releases, stanText, stanMag, balance, produktId));

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


            // ✅ UTWÓRZ KARTY PRODUKTÓW Z SZABLONU DASHBOARDU
            wpProductCards.Children.Clear();

            // Załaduj produkty z domyślnego widoku dashboardu
            var dashboardProductIds = await LoadDefaultDashboardProductIdsAsync();

            // ✅ PULA BILANSU: rodzic → dzieci (zamówienia/wydania dzieci doliczane do puli rodzica)
            // ⚡ Cache 5 min — konfiguracja puli zmienia się rzadko (Kreator Puli Bilansu)
            if (_puleCacheMap == null || (DateTime.Now - _puleCacheTime).TotalMinutes > 5)
            {
                _puleCacheMap = await _bilansSkladnikiService.GetMapowanieAsync();
                _puleCacheTime = DateTime.Now;
            }
            var puleMap = _puleCacheMap;
            // Nazwy musimy znać także dla dzieci (do rozpiski na karcie)
            var nameIds = new List<int>(dashboardProductIds);
            foreach (var kv in puleMap)
                if (dashboardProductIds.Contains(kv.Key))
                    nameIds.AddRange(kv.Value);
            var productNames = await LoadProductNamesAsync(nameIds.Distinct().ToList());

            var colors = new[] {
                Color.FromRgb(52, 152, 219),  // Niebieski
                Color.FromRgb(46, 204, 113),  // Zielony
                Color.FromRgb(241, 196, 15),  // Żółty
                Color.FromRgb(230, 126, 34),  // Pomarańczowy
                Color.FromRgb(155, 89, 182),  // Fioletowy
                Color.FromRgb(26, 188, 156),  // Turkusowy
                Color.FromRgb(231, 76, 60),   // Czerwony
            };
            int colorIdx = 0;

            // ✅ #1 Suma dnia — akumulatory bilansu dla nagłówka
            decimal sumDoSprzedania = 0m, sumNadmiar = 0m;
            int cntPrzekroczone = 0;
            void AkumulujBilans(decimal b)
            {
                if (b >= 0) sumDoSprzedania += b;
                else { sumNadmiar += -b; cntPrzekroczone++; }
            }

            // Pozycje do karty zbiorczej "Bilans dnia" (wstawiana jako PIERWSZA karta panelu)
            var zbiorczaPozycje = new List<(int towarId, string nazwa, decimal baza, decimal stanMag, decimal zamW, decimal bilans, Color kolor)>();

            // 1. Kurczak A zawsze jako pierwsza karta
            if (kurczakA.Key > 0 && dashboardProductIds.Contains(kurczakA.Key))
            {
                var kurczakAName = productNames.TryGetValue(kurczakA.Key, out var n) ? n : "Kurczak A";
                decimal wartoscA = uzywajWydan ? wydaniaA : ordersA;
                AkumulujBilans(balanceA);
                zbiorczaPozycje.Add((kurczakA.Key, kurczakAName,
                    factA > 0 ? factA : planA, stanMagA, wartoscA, balanceA,
                    DarkStatusColor(wartoscA, (factA > 0 ? factA : planA) + stanMagA)));
                wpProductCards.Children.Add(CreateKurczakACard(
                    kurczakAName, planA, factA, wartoscA, balanceA, stanMagA,
                    colors[colorIdx % colors.Length], kurczakAName, kurczakA.Key,
                    _pojemnikiKlasyPrognoza, uzywajWydan, rezerwacjeKlas));
                colorIdx++;
                // Karta klas tuszki — zaraz po Kurczaku A (prognoza pojemników per klasa wagowa)
                wpProductCards.Children.Add(CreateKlasyTuszkiCard(_pojemnikiKlasyPrognoza));
            }

            // 3. Pozostałe produkty (pomijając Kurczak A) — najpierw zbierz dane, potem sortuj i twórz karty
            var cardData = new List<(string name, decimal plan, decimal fakt, decimal wartosc, decimal bilans, decimal stan, int productId, List<(string nazwa, decimal kg, int towarId)> rozpiska)>();
            foreach (var productId in dashboardProductIds)
            {
                if (!productNames.TryGetValue(productId, out var productName))
                    continue;
                if (productId == kurczakA.Key)
                    continue; // już dodany na początku

                decimal plan = 0, fakt = 0, zam = 0, wyd = 0, stan = 0, bilans = 0;

                if (konfiguracjaProduktow.TryGetValue(productId, out var procent))
                    plan = pulaTuszkiB * (procent / 100m);
                if (actualIncomeElementy.TryGetValue(productId, out var f)) fakt = f;
                if (orderSum.TryGetValue(productId, out var z)) zam = z;
                if (wydaniaSum.TryGetValue(productId, out var w)) wyd = w;
                if (stanyMagazynowe.TryGetValue(productId, out var s)) stan = s;

                // ✅ PULA BILANSU — doklej zamówienia/wydania składników (dzieci) do puli rodzica
                List<(string nazwa, decimal kg, int towarId)> skladnikiRozpiska = null;
                if (puleMap.TryGetValue(productId, out var dzieci) && dzieci.Count > 0)
                {
                    decimal zamWlasne = zam, wydWlasne = wyd;
                    decimal zamDzieci = 0m, wydDzieci = 0m;
                    var rozpiska = new List<(string nazwa, decimal kg, int towarId)>();
                    foreach (var childId in dzieci)
                    {
                        decimal zc = orderSum.TryGetValue(childId, out var zz) ? zz : 0m;
                        decimal wc = wydaniaSum.TryGetValue(childId, out var ww) ? ww : 0m;
                        zamDzieci += zc;
                        wydDzieci += wc;
                        decimal childVal = uzywajWydan ? wc : zc;
                        string childName = productNames.TryGetValue(childId, out var cn2) ? cn2 : $"#{childId}";
                        rozpiska.Add((childName, childVal, childId));
                    }
                    zam += zamDzieci;
                    wyd += wydDzieci;

                    // Rozpiska tylko gdy cokolwiek dokleiliśmy (w bieżącym trybie)
                    if ((uzywajWydan ? wydDzieci : zamDzieci) > 0)
                    {
                        skladnikiRozpiska = new List<(string nazwa, decimal kg, int towarId)>
                        {
                            ("własne", uzywajWydan ? wydWlasne : zamWlasne, productId)
                        };
                        skladnikiRozpiska.AddRange(rozpiska);
                    }
                }

                decimal baseVal = fakt > 0 ? fakt : plan;
                decimal odejmij = uzywajWydan ? wyd : zam;
                bilans = baseVal + stan - odejmij;

                decimal wartoscDoPokazania = uzywajWydan ? wyd : zam;
                AkumulujBilans(bilans);
                zbiorczaPozycje.Add((productId, productName,
                    baseVal, stan, wartoscDoPokazania, bilans,
                    DarkStatusColor(wartoscDoPokazania, baseVal + stan)));
                cardData.Add((productName, plan, fakt, wartoscDoPokazania, bilans, stan, productId, skladnikiRozpiska));
            }

            // (sortowanie problemów cofnięte — karty w kolejności z szablonu dashboardu)
            foreach (var cd in cardData)
            {
                wpProductCards.Children.Add(CreateDashboardCard(
                    cd.name, cd.plan, cd.fakt, cd.wartosc, cd.bilans, cd.stan,
                    colors[colorIdx % colors.Length], false, cd.name, cd.productId, cd.rozpiska));
                colorIdx++;
            }

            // ✅ KARTA ZBIORCZA — pierwsza w panelu: wszystkie towary ze zdjęciami i bilansami
            if (zbiorczaPozycje.Count > 0)
                wpProductCards.Children.Insert(0, CreateZbiorczaCard(zbiorczaPozycje));

            // ✅ #1 Suma dnia — zaktualizuj nagłówek
            if (txtBilansSuma != null)
            {
                txtBilansSuma.Text = cntPrzekroczone > 0
                    ? $"Do sprzedania: {sumDoSprzedania:N0} kg  ·  Nadmiar: {sumNadmiar:N0} kg  ·  przekroczone: {cntPrzekroczone}"
                    : $"Do sprzedania: {sumDoSprzedania:N0} kg";
                txtBilansSuma.Foreground = new SolidColorBrush(cntPrzekroczone > 0
                    ? Color.FromRgb(192, 57, 43) : Color.FromRgb(39, 174, 96));
            }

            // Jeśli brak szablonu - pokaż stare karty
            if (!dashboardProductIds.Any())
            {
                decimal sumaZamWyd = uzywajWydan ? (wydaniaA + sumaWydB) : (ordersA + sumaZamB);
                decimal zamWydA = uzywajWydan ? wydaniaA : ordersA;
                decimal zamWydB = uzywajWydan ? sumaWydB : sumaZamB;

                wpProductCards.Children.Add(CreateDashboardCard("SUMA", planA + sumaPlanB, factA + sumaFaktB, sumaZamWyd, bilansCalk, stanMagA + sumaStanB,
                    Color.FromRgb(76, 175, 80), true));
                wpProductCards.Children.Add(CreateKurczakACard("Kurczak A", planA, factA, zamWydA, balanceA, stanMagA,
                    Color.FromRgb(102, 187, 106), "Kurczak A", kurczakA.Key,
                    _pojemnikiKlasyPrognoza, uzywajWydan, rezerwacjeKlas));
                wpProductCards.Children.Add(CreateDashboardCard("Kurczak B", sumaPlanB, sumaFaktB, zamWydB, bilansB, sumaStanB,
                    Color.FromRgb(66, 165, 245), true));
            }

            // Zachowaj dane w dtAgg dla kompatybilności
            dtAgg.Rows.Add("═══ SUMA CAŁKOWITA ═══", planA + sumaPlanB, factA + sumaFaktB,
                (stanMagA + sumaStanB > 0 ? (stanMagA + sumaStanB).ToString("N0") : ""),
                ordersA + sumaZamB, wydaniaA + sumaWydB, bilansCalk,
                bilansCalk > 0 ? bilansCalk : 0m, bilansCalk < 0 ? Math.Abs(bilansCalk) : 0m);
            dtAgg.Rows.Add("🐔 Kurczak A", planA, factA, stanA, ordersA, wydaniaA, balanceA,
                balanceA > 0 ? balanceA : 0m, balanceA < 0 ? Math.Abs(balanceA) : 0m);
            dtAgg.Rows.Add("🐔 Kurczak B", sumaPlanB, sumaFaktB,
                (sumaStanB > 0 ? sumaStanB.ToString("N0") : ""), sumaZamB, sumaWydB, bilansB,
                bilansB > 0 ? bilansB : 0m, bilansB < 0 ? Math.Abs(bilansB) : 0m);
            foreach (var produkt in produktyB)
            {
                var doSprz = produkt.bilans > 0 ? produkt.bilans : 0m;
                var nadm = produkt.bilans < 0 ? Math.Abs(produkt.bilans) : 0m;
                dtAgg.Rows.Add(produkt.nazwa, produkt.plan, produkt.fakt, produkt.stan, produkt.zam, produkt.wyd, produkt.bilans, doSprz, nadm);
            }

            // Ustaw źródło danych dla ukrytego DataGrid (kompatybilność)
            dgAggregation.ItemsSource = dtAgg.DefaultView;
            SetupAggregationDataGrid();

            // Responsywne szerokości kart (po przebudowie panelu; stan rozwinięcia Kurczaka A przeżywa odświeżenie)
            ForceCardLayout();

            // Wypełnij dashboard dostępności produktów dla handlowców
            PopulateDostepnoscProduktow(dtAgg);

            diagTimes.Add(("Agregacja", diagSw.ElapsedMilliseconds));
            _lastAggregationDiag = diagTimes;

        }

        private string ShortenProductName(string name)
        {
            // Usuń emoji i znaki specjalne
            name = name.Replace("🍗", "").Replace("🍖", "").Replace("🥩", "").Replace("▶", "").Replace("▼", "").Trim();

            // Skróć długie nazwy produktów
            if (name.Length > 16)
            {
                // Znajdź słowo kluczowe
                if (name.Contains("Filet", StringComparison.OrdinalIgnoreCase)) return "Filet";
                if (name.Contains("Udziec", StringComparison.OrdinalIgnoreCase)) return "Udziec";
                if (name.Contains("Skrzydło", StringComparison.OrdinalIgnoreCase) || name.Contains("Skrzydlo", StringComparison.OrdinalIgnoreCase)) return "Skrzydło";
                if (name.Contains("Ćwiartka", StringComparison.OrdinalIgnoreCase) || name.Contains("Cwiartka", StringComparison.OrdinalIgnoreCase)) return "Ćwiartka";
                if (name.Contains("Pierś", StringComparison.OrdinalIgnoreCase) || name.Contains("Piers", StringComparison.OrdinalIgnoreCase)) return "Pierś";
                if (name.Contains("Noga", StringComparison.OrdinalIgnoreCase)) return "Noga";
                if (name.Contains("Podudzie", StringComparison.OrdinalIgnoreCase)) return "Podudzie";
                if (name.Contains("Wątroba", StringComparison.OrdinalIgnoreCase) || name.Contains("Watroba", StringComparison.OrdinalIgnoreCase)) return "Wątroba";
                if (name.Contains("Serce", StringComparison.OrdinalIgnoreCase)) return "Serce";
                if (name.Contains("Żołądek", StringComparison.OrdinalIgnoreCase) || name.Contains("Zoladek", StringComparison.OrdinalIgnoreCase)) return "Żołądek";

                // Skróć do 16 znaków
                return name.Substring(0, 14) + "..";
            }
            return name;
        }

        // Single-flight: jedno współbieżne ładowanie; kolejne wywołania czekają na ten sam Task.
        // Słownik wypełniany DOPIERO po pełnym wczytaniu (żaden caller nie zobaczy stanu częściowego),
        // a po błędzie Task jest zerowany — następne wywołanie próbuje ponownie.
        private Task _productImagesTask;
        private Task LoadProductImagesAsync()
        {
            if (_productImages.Count > 0) return Task.CompletedTask; // pełny cache już jest
            return _productImagesTask ??= LoadProductImagesCoreAsync();
        }

        private async Task LoadProductImagesCoreAsync()
        {
            try
            {
                var tmp = new Dictionary<int, BitmapImage?>();

                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                const string sql = @"SELECT TowarId, Zdjecie FROM dbo.TowarZdjecia WHERE Aktywne = 1";
                await using var cmd = new SqlCommand(sql, cn);
                cmd.CommandTimeout = 30;
                await using var rdr = await cmd.ExecuteReaderAsync();

                while (await rdr.ReadAsync())
                {
                    int towarId = rdr.GetInt32(0);
                    if (!rdr.IsDBNull(1))
                    {
                        byte[] imageData = (byte[])rdr["Zdjecie"];
                        var image = BytesToBitmapImage(imageData); // Freeze() w środku — bezpieczne między wątkami
                        tmp[towarId] = image;
                    }
                }

                // Commit dopiero po pełnym wczytaniu
                foreach (var kv in tmp)
                    _productImages[kv.Key] = kv.Value;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Błąd ładowania obrazków: {ex.Message}");
                _productImagesTask = null; // retry przy następnym wywołaniu
            }
        }

        private async Task<List<int>> LoadDefaultDashboardProductIdsAsync()
        {
            var productIds = new List<int>();
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                const string sql = @"SELECT TOP 1 ProduktyIds FROM dbo.DashboardWidoki WHERE IsDomyslny = 1";
                await using var cmd = new SqlCommand(sql, cn);
                var result = await cmd.ExecuteScalarAsync();

                if (result != null && result != DBNull.Value)
                {
                    var idsStr = result.ToString();
                    foreach (var idStr in idsStr.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (int.TryParse(idStr.Trim(), out int id))
                            productIds.Add(id);
                    }
                }
            }
            catch { }
            return productIds;
        }

        private async Task<Dictionary<int, string>> LoadProductNamesAsync(List<int> productIds)
        {
            var names = new Dictionary<int, string>();
            if (!productIds.Any()) return names;

            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();

                var sql = $"SELECT ID, kod FROM [HM].[TW] WHERE ID IN ({string.Join(",", productIds)})";
                await using var cmd = new SqlCommand(sql, cn);
                await using var rdr = await cmd.ExecuteReaderAsync();

                while (await rdr.ReadAsync())
                {
                    names[rdr.GetInt32(0)] = rdr.GetString(1);
                }
            }
            catch { }
            return names;
        }

        // Karta "Klasy tuszki kurczaka" — liczba pojemników (palet) per klasa wagowa
        // z PROGNOZY dnia ubojowego (_pojemnikiKlasyPrognoza, każdy poj = 15 kg).
        // Layout: 9 wierszy (klasy 4–12) w 2 kolumnach (Kl. | poj).
        private Border CreateKlasyTuszkiCard(int[] pojemnikiKlasy)
        {
            var card = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(9),
                Margin = new Thickness(2),
                Padding = new Thickness(10, 7, 10, 7),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x2C, 0x3E, 0x50)),
                BorderThickness = new Thickness(1),
                Width = 280,
                Height = 194,
                Tag = new { TowarId = 0, Nazwa = "Klasy tuszki" },
                UseLayoutRounding = true
            };

            var root = new StackPanel();
            root.Children.Add(new TextBlock
            {
                Text = "🐔 Klasy tuszki (poj.)",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x2C, 0x3E, 0x50)),
                Margin = new Thickness(0, 0, 0, 4)
            });

            // Kolory klas: 4–7 Duży (niebieski), 8–12 Mały (pomarańczowy)
            Color KolorKlasy(int kl) => kl <= 7
                ? Color.FromRgb(0x25, 0x63, 0xEB)
                : Color.FromRgb(0xF9, 0x73, 0x16);

            // Klasy 5–11 (bez 4 i 12)
            const int KL_OD = 5, KL_DO = 11;
            int sumaPoj = 0;
            for (int k = KL_OD; k <= KL_DO; k++) sumaPoj += (k < pojemnikiKlasy.Length ? pojemnikiKlasy[k] : 0);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            for (int i = KL_OD; i <= KL_DO; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (int kl = KL_OD; kl <= KL_DO; kl++)
            {
                int poj = kl < pojemnikiKlasy.Length ? pojemnikiKlasy[kl] : 0;
                int rowIdx = kl - KL_OD;
                bool ma = poj > 0;
                var kolor = KolorKlasy(kl);

                var lbl = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 1) };
                lbl.Children.Add(new Border
                {
                    Width = 8, Height = 8, CornerRadius = new CornerRadius(4),
                    Background = new SolidColorBrush(ma ? kolor : Color.FromRgb(0xD5, 0xDB, 0xDF)),
                    Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center
                });
                lbl.Children.Add(new TextBlock
                {
                    Text = $"Kl. {kl}",
                    FontSize = 11,
                    FontWeight = ma ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = new SolidColorBrush(ma ? Color.FromRgb(0x3C, 0x3C, 0x3C) : Color.FromRgb(0xAE, 0xB6, 0xBD)),
                    VerticalAlignment = VerticalAlignment.Center
                });
                Grid.SetRow(lbl, rowIdx); Grid.SetColumn(lbl, 0);
                grid.Children.Add(lbl);

                double procent = sumaPoj > 0 ? 100.0 * poj / sumaPoj : 0;
                var val = new TextBlock
                {
                    FontSize = 11,
                    FontWeight = ma ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = new SolidColorBrush(ma ? kolor : Color.FromRgb(0xC2, 0xC8, 0xCD)),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                };
                if (ma)
                {
                    val.Inlines.Add(new Run($"{poj} poj · {poj * 15:N0} kg "));
                    val.Inlines.Add(new Run($"{procent:N0}%") { Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x98, 0xA3)), FontWeight = FontWeights.SemiBold });
                }
                else val.Text = "—";
                System.Windows.Documents.Typography.SetNumeralAlignment(val, FontNumeralAlignment.Tabular);
                Grid.SetRow(val, rowIdx); Grid.SetColumn(val, 1);
                grid.Children.Add(val);
            }
            root.Children.Add(grid);

            // Stopka: suma
            root.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(0xE3, 0xE8, 0xEC)), Margin = new Thickness(0, 3, 0, 2) });
            var suma = new TextBlock { HorizontalAlignment = HorizontalAlignment.Right, FontSize = 11, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(0x2C, 0x3E, 0x50)) };
            suma.Inlines.Add(new Run("Razem: ") { Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x98, 0xA3)), FontWeight = FontWeights.Normal });
            suma.Inlines.Add(new Run($"{sumaPoj} poj · {sumaPoj * 15:N0} kg"));
            root.Children.Add(suma);

            card.ToolTip = "Prognoza pojemników tuszki A per klasa wagowa (z dnia ubojowego). 1 poj ≈ 15 kg.";
            card.Child = root;
            return card;
        }

        // Pasek postępu pod bilansem — spójna geometria na każdej karcie:
        //   [ pigułkowy tor = 100% dostępności | strefa nadmiaru ] [ % ]
        // Znacznik (pionowa kreska) wyznacza limit 100%; przy nadmiarze wypełnienie
        // PRZECHODZI przez znacznik w strefę nadmiaru. % zawsze w tej samej, prawej kolumnie.
        private FrameworkElement BuildPostepBar(double pct, Color statusColor)
        {
            const double TRACK_FRAC = 0.86;          // tor (=100%) zajmuje stałe 86% szerokości strefy paska
            const double PCT_MAX_VIS = 100.0 / TRACK_FRAC; // ~116% — dalej wypełnienie jest już pełne

            bool over = pct > 100.0;
            double fillFrac = Math.Max(0.0, Math.Min(1.0, (pct / 100.0) * TRACK_FRAC));
            string pctTxt = pct > 999.0 ? ">999%" : $"{Math.Round(pct)}%";

            var root = new Grid { Margin = new Thickness(1, 7, 4, 0) };
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // pasek
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });                   // % (stała kolumna)

            // --- strefa paska ---
            var barZone = new Grid { Height = 10, VerticalAlignment = VerticalAlignment.Center };
            barZone.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(TRACK_FRAC, GridUnitType.Star) });
            barZone.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - TRACK_FRAC, GridUnitType.Star) });

            // 1) Tor (pigułka) = 100% dostępności
            var track = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0xEE, 0xF2, 0xF5)),
                CornerRadius = new CornerRadius(5)
            };
            Grid.SetColumn(track, 0);
            barZone.Children.Add(track);

            // 2) Wypełnienie (pigułka w kolorze statusu) — może przejść przez znacznik 100%
            var fillHost = new Grid();
            Grid.SetColumnSpan(fillHost, 2);
            fillHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(fillFrac, GridUnitType.Star) });
            fillHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - fillFrac, GridUnitType.Star) });
            if (pct > 0.5)
            {
                var fill = new Border
                {
                    Background = new SolidColorBrush(statusColor),
                    CornerRadius = new CornerRadius(5)
                };
                Grid.SetColumn(fill, 0);
                fillHost.Children.Add(fill);
            }
            barZone.Children.Add(fillHost);

            // 3) Znacznik limitu 100% — rysowany NA wypełnieniu (widać "przelanie")
            var limit = new Border
            {
                Width = 2,
                Height = 14,
                Background = new SolidColorBrush(Color.FromRgb(0x9F, 0xAC, 0xB6)),
                CornerRadius = new CornerRadius(1),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, -2, -1, -2)
            };
            Grid.SetColumn(limit, 0);
            barZone.Children.Add(limit);

            Grid.SetColumn(barZone, 0);
            root.Children.Add(barZone);

            // --- % w stałej kolumnie po prawej (równa linia przez wszystkie karty) ---
            var pctLbl = new TextBlock
            {
                Text = pctTxt,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(statusColor),
                HorizontalAlignment = HorizontalAlignment.Left,   // tuż przy końcu paska
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(3, 0, 0, 0)
            };
            System.Windows.Documents.Typography.SetNumeralAlignment(pctLbl, FontNumeralAlignment.Tabular);
            Grid.SetColumn(pctLbl, 1);
            root.Children.Add(pctLbl);

            root.ToolTip = over
                ? $"Wykorzystanie puli: {pct:N0}% — PONAD dostępność (za znacznikiem 100%)"
                : $"Wykorzystanie puli: {pct:N0}% (znacznik = 100% dostępności)";
            return root;
        }

        // Ciemny odcień koloru statusu (do liczb) wg progów StatusInfo
        private Color DarkStatusColor(decimal zam, decimal available)
        {
            var (_, word, _, _) = StatusInfo(zam, available);
            return word == "Nadmiar" ? Color.FromRgb(0xC0, 0x39, 0x2B)
                 : word == "Napięte" ? Color.FromRgb(0xB9, 0x77, 0x0E)
                 : word == "OK" ? Color.FromRgb(0x1E, 0x84, 0x49)
                 : Color.FromRgb(0x7F, 0x8C, 0x8D);
        }

        // Karta zbiorcza "Bilans dnia" — pierwsza karta panelu: wszystkie towary ze zdjęciem,
        // równaniem plan/fakt + stan − zam i wynikiem (bilansem) w kg
        private Border CreateZbiorczaCard(List<(int towarId, string nazwa, decimal baza, decimal stanMag, decimal zamW, decimal bilans, Color kolor)> pozycje)
        {
            var card = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(9),
                Margin = new Thickness(2),
                Padding = new Thickness(10, 7, 10, 7),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x2C, 0x3E, 0x50)),
                BorderThickness = new Thickness(1),
                Width = 280,
                Height = 194,
                Tag = new { TowarId = 0, Nazwa = "Bilans dnia" }, // Tag ≠ null → ApplyCardLayout nadaje slot
                UseLayoutRounding = true,
                SnapsToDevicePixels = true
            };

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            var serY = new SolidColorBrush(Color.FromRgb(0xE6, 0xB0, 0x08));   // plan/fakt
            var serP = new SolidColorBrush(Color.FromRgb(0x9B, 0x59, 0xB6));   // stan
            var serB = new SolidColorBrush(Color.FromRgb(0x34, 0x98, 0xDB));   // zam
            var serSzary = new SolidColorBrush(Color.FromRgb(0x8A, 0x98, 0xA3));

            foreach (var p in pozycje)
            {
                var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(86) }); // stała kolumna wyników → równa linia

                var img = p.towarId > 0 ? GetProductImage(p.towarId) : null;
                Border ikona;
                if (img != null)
                {
                    var im = new System.Windows.Controls.Image { Source = img, Width = 30, Height = 30, Stretch = Stretch.Uniform };
                    RenderOptions.SetBitmapScalingMode(im, BitmapScalingMode.HighQuality);
                    ikona = new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(6), ClipToBounds = true, Child = im };
                }
                else
                {
                    ikona = new Border
                    {
                        Width = 32, Height = 32, CornerRadius = new CornerRadius(6),
                        Background = new SolidColorBrush(Color.FromRgb(236, 240, 241)),
                        Child = new TextBlock { Text = "📦", FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                    };
                }
                ikona.Margin = new Thickness(0, 0, 6, 0);
                ikona.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(ikona, 0);
                row.Children.Add(ikona);

                // Środek: nazwa + równanie plan/fakt + stan − zam (wartości w kolorach serii)
                var srodek = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                srodek.Children.Add(new TextBlock
                {
                    Text = p.nazwa,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x2C, 0x3E, 0x50)),
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                // Równanie (kg) — dosunięte do prawej, tuż przy kolumnie wyników → wszystko w jednej linii
                var rownanie = new TextBlock
                {
                    FontSize = 9.3,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                rownanie.Inlines.Add(new Run($"{p.baza:N0}") { Foreground = serY, FontWeight = FontWeights.SemiBold });
                rownanie.Inlines.Add(new Run(" + ") { Foreground = serSzary });
                rownanie.Inlines.Add(new Run($"{p.stanMag:N0}") { Foreground = serP, FontWeight = FontWeights.SemiBold });
                rownanie.Inlines.Add(new Run(" − ") { Foreground = serSzary });
                rownanie.Inlines.Add(new Run($"{p.zamW:N0}") { Foreground = serB, FontWeight = FontWeights.SemiBold });
                rownanie.Inlines.Add(new Run(" kg =") { Foreground = serSzary });
                srodek.Children.Add(rownanie);
                Grid.SetColumn(srodek, 1);
                row.Children.Add(srodek);

                // Wynik (bilans) z "kg" — stała kolumna, do prawej → wyniki w idealnie równej linii
                var wartoscTb = new TextBlock
                {
                    Text = (p.bilans < 0 ? $"−{Math.Abs(p.bilans):N0}" : $"{p.bilans:N0}") + " kg",
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(p.kolor),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(6, 0, 0, 0)
                };
                System.Windows.Documents.Typography.SetNumeralAlignment(wartoscTb, FontNumeralAlignment.Tabular);
                Grid.SetColumn(wartoscTb, 2);
                row.Children.Add(wartoscTb);

                stack.Children.Add(row);
            }

            card.ToolTip = "Bilans dnia: plan/fakt + stan − zam = do sprzedania (czerwone = nadmiar)\nKliknij — pokaż wszystkie towary (zdejmij filtr)";
            card.Cursor = System.Windows.Input.Cursors.Hand;
            card.MouseLeftButtonUp += (s, e) =>
            {
                // Klik w kartę "Bilans dnia" → zdejmij filtr towaru (pokaż wszystkie towary i zamówienia)
                if (cbProductFilter.Items.Count > 0)
                    cbProductFilter.SelectedIndex = 0;
            };
            card.Child = stack;
            return card;
        }

        // Responsywne kurczenie kolumn Wyd./Róż. w szczegółach zamówienia:
        //   szeroko → pełne 82 px; ciaśniej → 56 px i krótsze nagłówki; bardzo wąsko → Róż. znika, potem Wyd.
        private void DgDetails_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyDetailsLayout();

        private void ApplyDetailsLayout()
        {
            if (_colWydDet == null || _colRozDet == null) return;
            double w = dgDetails?.ActualWidth ?? 0;
            if (w <= 0) return;

            if (w < 520)
            {
                _colWydDet.Visibility = Visibility.Collapsed;
                _colRozDet.Visibility = Visibility.Collapsed;
            }
            else if (w < 620)
            {
                _colWydDet.Visibility = Visibility.Visible;
                _colWydDet.Width = new DataGridLength(56);
                _colWydDet.Header = "W.";
                _colRozDet.Visibility = Visibility.Collapsed;
            }
            else if (w < 740)
            {
                _colWydDet.Visibility = Visibility.Visible;
                _colWydDet.Width = new DataGridLength(56);
                _colWydDet.Header = "W.";
                _colRozDet.Visibility = Visibility.Visible;
                _colRozDet.Width = new DataGridLength(56);
                _colRozDet.Header = "R.";
            }
            else
            {
                _colWydDet.Visibility = Visibility.Visible;
                _colWydDet.Width = new DataGridLength(82);
                _colWydDet.Header = "Wyd.";
                _colRozDet.Visibility = Visibility.Visible;
                _colRozDet.Width = new DataGridLength(82);
                _colRozDet.Header = "Róż.";
            }
        }

        // Responsywny układ kart "Podsumowania dnia": szerokość kart wyliczana z szerokości panelu,
        // żeby zawsze równo wypełniały przestrzeń (bez pustego pasa). Rozwinięty Kurczak A = 2 sloty.
        private bool _applyingCardLayout;
        private double _lastCardAvail = -1;
        private void ApplyCardLayout()
        {
            if (_applyingCardLayout) return; // blokada re-entrancji (SizeChanged wywołane przez nasze własne zmiany)
            const double MIN_W = 184, MAX_W = 340, MARG = 4;   // mniejsze karty → cel 4 na wiersz
            double avail = wpProductCards.ActualWidth;
            if (avail < MIN_W + MARG) return;
            // Ignoruj mikro-drgania szerokości (<2 px) — gasi pętle layoutu
            if (Math.Abs(avail - _lastCardAvail) < 2 && !_cardLayoutForce) return;
            _lastCardAvail = avail;
            _cardLayoutForce = false;

            // Celujemy w 3 kolumny (3 karty w linii, 3 pod spodem); przy wąskim panelu spadamy do 2
            int cols = Math.Max(2, Math.Min(4, (int)(avail / (MIN_W + MARG))));
            double w = Math.Min(MAX_W, Math.Floor((avail - cols * MARG) / cols));

            try
            {
                _applyingCardLayout = true;
                foreach (var c in wpProductCards.Children.OfType<Border>())
                {
                    if (c.Tag == null) continue; // separator po Kurczaku A — pomijamy
                    double target = (ReferenceEquals(c, _kurczakACard) && _kurczakAExpanded)
                        ? w * 2 + MARG      // rozwinięty Kurczak A = dokładnie 2 sloty siatki
                        : w;
                    if (Math.Abs(c.Width - target) > 0.5)
                        c.Width = target;
                }
            }
            finally { _applyingCardLayout = false; }
        }

        // Wymusza pełne przeliczenie przy następnym ApplyCardLayout (po przebudowie kart / toggle Kurczaka A)
        private bool _cardLayoutForce;
        private void ForceCardLayout()
        {
            _cardLayoutForce = true;
            ApplyCardLayout();
        }

        private BitmapImage? BytesToBitmapImage(byte[] data)
        {
            if (data == null || data.Length == 0) return null;
            try
            {
                var image = new BitmapImage();
                using (var ms = new MemoryStream(data))
                {
                    ms.Position = 0;
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.DecodePixelWidth = 140;
                    image.StreamSource = ms;
                    image.EndInit();
                    image.Freeze();
                }
                return image;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Błąd dekodowania obrazka: {ex.Message}");
                return null;
            }
        }

        private BitmapImage? GetProductImage(int towarId)
        {
            return _productImages.TryGetValue(towarId, out var img) ? img : null;
        }

        // Kolor statusu wg % zamówień do (przychód+stan):
        //   > 100%          → CZERWONY (NADMIAR — sprzedane ponad dostępność, STOP)
        //   96% – 100%      → ZIELONY  (idealnie domknięte)
        //   70% – 95,9%     → POMARAŃCZOWY (jeszcze sporo do sprzedania)
        //   0% – 69,9%      → SZARY "niebezpieczny" (dużo niesprzedanego towaru)
        private (Color color, string word, string icon, double pct) StatusInfo(decimal zam, decimal available)
        {
            var green = Color.FromRgb(39, 174, 96);
            var orange = Color.FromRgb(243, 156, 18);
            var red = Color.FromRgb(231, 76, 60);
            var gray = Color.FromRgb(149, 165, 166);

            if (available <= 0)
                return zam > 0 ? (red, "Nadmiar", "🔴", 999.0) : (gray, "—", "⚪", 0.0);

            double pct = (double)(zam / available) * 100.0;
            if (pct > 100.0)
                return (red, "Nadmiar", "🔴", pct);
            if (pct >= 96.0)
                return (green, "OK", "🟢", pct);
            if (pct >= 70.0)
                return (orange, "Napięte", "🟡", pct);
            return (gray, "Wolne", "⚪", pct);
        }

        // Jeden pasek: RAMKA = przychód+stan (available), WYPEŁNIENIE = zamówienia (zam).
        // Gdy zam > available → wypełnienie wychodzi poza ramkę. Etykieta % wyśrodkowana na ramce.
        private FrameworkElement BuildUtilBar(decimal zam, decimal available, Color statusColor, double trackWidth, double height = 18)
        {
            double ratio = available > 0 ? (double)(zam / available) : (zam > 0 ? 1.5 : 0.0);
            double fillW = Math.Max(2.0, ratio * trackWidth);

            var host = new Grid
            {
                Margin = new Thickness(0, 3, 0, 2),
                ClipToBounds = false,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            // Wypełnienie (zamówienia) — może przekroczyć ramkę (overflow w prawo)
            host.Children.Add(new Border
            {
                Width = fillW,
                Height = height - 4,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(statusColor)
            });

            // Ramka (przychód+stan) — sam kontur, rysowana na wypełnieniu
            host.Children.Add(new Border
            {
                Width = trackWidth,
                Height = height,
                HorizontalAlignment = HorizontalAlignment.Left,
                CornerRadius = new CornerRadius(4),
                BorderBrush = new SolidColorBrush(Color.FromRgb(120, 130, 140)),
                BorderThickness = new Thickness(1),
                Background = Brushes.Transparent
            });

            // Etykieta danych (%) — wyśrodkowana na ramce
            var lblHost = new Grid { Width = trackWidth, Height = height, HorizontalAlignment = HorizontalAlignment.Left };
            lblHost.Children.Add(new TextBlock
            {
                Text = available > 0 ? $"{(double)(zam / available) * 100:N0}%" : (zam > 0 ? ">100%" : "—"),
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(33, 37, 41))
            });
            host.Children.Add(lblHost);

            return host;
        }

        // Pasek-skala wg mockupu: RAMKA = plan/fakt, za nią segment STAN (fiolet),
        // wypełnienie = ZAM (niebieskie, może wyjść poza ramkę i stan).
        // Nad paskiem etykiety wartości w kolorach serii + pionowe znaczniki granic.
        private FrameworkElement BuildSkalaBar(decimal baza, decimal stan, decimal zam, double width, string zamLabel)
        {
            var blue = Color.FromRgb(52, 152, 219);
            var yellow = Color.FromRgb(230, 176, 8);
            var purple = Color.FromRgb(155, 89, 182);
            var dark = Color.FromRgb(44, 62, 80);

            decimal total = Math.Max(baza + stan, zam);
            if (total <= 0) total = 1;
            double Px(decimal v) => (double)(v / total) * (width - 4) + 2;

            double xBaza = Px(baza);
            double xStan = Px(baza + stan);
            double xZam = Px(zam);

            const double lblH = 12;
            const double barTop = lblH + 2;
            const double barH = 22;

            var root = new Canvas
            {
                Width = width,
                Height = barTop + barH + (stan > 0 ? 13 : 3), // dolny rząd tylko gdy jest stan
                Margin = new Thickness(0, 3, 0, 1),
                ClipToBounds = false,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            bool przekroczone = zam > baza + stan;
            double xAvail = xStan;

            // 0) Tło wnętrza ramki (pojemność plan/fakt) — bardzo jasne, pusta część czytelna
            var inner = new Border
            {
                Width = Math.Max(3, xBaza),
                Height = barH,
                Background = new SolidColorBrush(Color.FromRgb(0xF7, 0xF9, 0xFB)),
                CornerRadius = new CornerRadius(3)
            };
            Canvas.SetLeft(inner, 0);
            Canvas.SetTop(inner, barTop);
            root.Children.Add(inner);

            // 1) Segment STAN — za ramką (jasny fiolet, zaokrąglony z prawej)
            if (stan > 0)
            {
                var stanSeg = new Border
                {
                    Width = Math.Max(2, xStan - xBaza),
                    Height = barH,
                    Background = new SolidColorBrush(Color.FromArgb(56, purple.R, purple.G, purple.B)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(120, purple.R, purple.G, purple.B)),
                    BorderThickness = new Thickness(0, 1, 1, 1),
                    CornerRadius = new CornerRadius(0, 3, 3, 0)
                };
                Canvas.SetLeft(stanSeg, xBaza);
                Canvas.SetTop(stanSeg, barTop);
                root.Children.Add(stanSeg);
            }

            // 2) Wypełnienie ZAM (gradient) — część PONAD dostępność (baza+stan) zmienia się w CZERWONĄ
            if (zam > 0)
            {
                double xFillBlueEnd = Math.Min(xZam, xAvail);
                var fillBlue = new Border
                {
                    Width = Math.Max(3, xFillBlueEnd - 2),
                    Height = barH - 4,
                    Background = new LinearGradientBrush(
                        Color.FromRgb(0x5D, 0xAD, 0xE2), Color.FromRgb(0x2E, 0x86, 0xC1), 90),
                    CornerRadius = przekroczone ? new CornerRadius(2, 0, 0, 2) : new CornerRadius(2)
                };
                Canvas.SetLeft(fillBlue, 2);
                Canvas.SetTop(fillBlue, barTop + 2);
                root.Children.Add(fillBlue);

                if (przekroczone)
                {
                    var fillRed = new Border
                    {
                        Width = Math.Max(3, xZam - xAvail),
                        Height = barH - 4,
                        Background = new LinearGradientBrush(
                            Color.FromRgb(0xEC, 0x70, 0x63), Color.FromRgb(0xC0, 0x39, 0x2B), 90),
                        CornerRadius = new CornerRadius(0, 2, 2, 0),
                        ToolTip = $"Przekroczono dostępność o {zam - (baza + stan):N0}"
                    };
                    Canvas.SetLeft(fillRed, xAvail);
                    Canvas.SetTop(fillRed, barTop + 2);
                    root.Children.Add(fillRed);
                }
            }

            // 3) Ramka = PLAN/FAKT (kontur, na wierzchu)
            var frame = new Border
            {
                Width = Math.Max(3, xBaza),
                Height = barH,
                Background = Brushes.Transparent,
                BorderBrush = new SolidColorBrush(dark),
                BorderThickness = new Thickness(1.4),
                CornerRadius = new CornerRadius(3)
            };
            Canvas.SetLeft(frame, 0);
            Canvas.SetTop(frame, barTop);
            root.Children.Add(frame);

            // 4) Pionowe znaczniki granic: żółty (koniec planu/faktu), fioletowy (koniec stanu)
            void Tick(double x, Color c)
            {
                var t = new Border
                {
                    Width = 2.6,
                    Height = barH + 4,
                    Background = new SolidColorBrush(c),
                    CornerRadius = new CornerRadius(1)
                };
                Canvas.SetLeft(t, Math.Min(width - 3, Math.Max(0, x - 1.3)));
                Canvas.SetTop(t, barTop - 2);
                root.Children.Add(t);
            }
            Tick(xBaza, yellow);
            if (stan > 0) Tick(xStan, purple);

            // 5) Etykiety:
            //    • ZAM — BIAŁĄ czcionką WEWNĄTRZ niebieskiego wypełnienia
            //    • przychód (fakt/plan) — ŻÓŁTY, nad żółtą kreską granicy
            //    • stan — FIOLETOWY, pod segmentem stanu
            void AddLbl(string txt, Color c, double x, double top)
            {
                double w = txt.Length * 5.6 + 2;
                double left = Math.Max(0, Math.Min(width - w, x - w / 2));
                var t = new TextBlock
                {
                    Text = txt,
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(c)
                };
                Canvas.SetLeft(t, left);
                Canvas.SetTop(t, top);
                root.Children.Add(t);
            }

            // ZAM — białe, wyśrodkowane w wypełnieniu (gdy za wąskie → obok, w kolorze wypełnienia)
            if (zam > 0)
            {
                string zamTxt = $"{zam:N0}";
                double zamW = zamTxt.Length * 5.6 + 2;
                double fillEnd = Math.Min(xZam, width);
                if (fillEnd - 4 >= zamW + 6)
                {
                    var t = new TextBlock
                    {
                        Text = zamTxt,
                        FontSize = 10.5,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.White
                    };
                    Canvas.SetLeft(t, Math.Max(4, (fillEnd - zamW) / 2));
                    Canvas.SetTop(t, barTop + 4);
                    root.Children.Add(t);
                }
                else
                {
                    AddLbl(zamTxt, przekroczone ? Color.FromRgb(0xC0, 0x39, 0x2B) : blue,
                        Math.Min(width - 10, xZam + zamW / 2 + 4), barTop + 5);
                }
            }

            // Przychód (fakt/plan) — żółty, centralnie nad żółtą kreską
            AddLbl($"{baza:N0}", yellow, xBaza, 0);

            // Stan — fioletowy, centralnie pod fioletową kreską (koniec stanu)
            if (stan > 0)
                AddLbl($"{stan:N0}", purple, xStan, barTop + barH + 2);

            root.ToolTip = $"Ramka = plan/fakt ({baza:N0}) · stan {stan:N0} · {zamLabel} {zam:N0}" +
                           (zam > baza + stan ? $" · PRZEKROCZONO o {zam - (baza + stan):N0}" : "");
            return root;
        }

        // Równanie bilansu: Plan/Fakt + Stan − Zam = Do sprzedania/Nadmiar (kolory serii)
        private FrameworkElement BuildRownanie(string bazaLabel, decimal baza, decimal stan, string zamLabel, decimal zam, decimal bilans)
        {
            var gray = Color.FromRgb(127, 140, 141);
            var yellow = Color.FromRgb(230, 176, 8);
            var purple = Color.FromRgb(155, 89, 182);
            var blue = Color.FromRgb(52, 152, 219);
            var bilansColor = bilans >= 0 ? Color.FromRgb(39, 174, 96) : Color.FromRgb(231, 76, 60);
            string bilansLbl = bilans >= 0 ? "Do sprzedania" : "Nadmiar";

            var row = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            for (int i = 0; i < 7; i++)
                row.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = i % 2 == 0 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto
                });

            FrameworkElement Val(string label, string value, Color c, double valSize)
            {
                var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom };
                sp.Children.Add(new TextBlock
                {
                    Text = label,
                    FontSize = 7,
                    Foreground = new SolidColorBrush(gray),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                sp.Children.Add(new TextBlock
                {
                    Text = value,
                    FontSize = valSize,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(c),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                return sp;
            }
            TextBlock Op(string s) => new TextBlock
            {
                Text = s,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(gray),
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(1, 0, 1, 2)
            };

            var e0 = Val(bazaLabel, $"{baza:N0}", yellow, 10); Grid.SetColumn(e0, 0); row.Children.Add(e0);
            var o1 = Op("+"); Grid.SetColumn(o1, 1); row.Children.Add(o1);
            var e2 = Val("Stan", $"{stan:N0}", purple, 10); Grid.SetColumn(e2, 2); row.Children.Add(e2);
            var o3 = Op("−"); Grid.SetColumn(o3, 3); row.Children.Add(o3);
            var e4 = Val(zamLabel, $"{zam:N0}", blue, 10); Grid.SetColumn(e4, 4); row.Children.Add(e4);
            var o5 = Op("="); Grid.SetColumn(o5, 5); row.Children.Add(o5);

            // Wynik w kolorowej pigułce — mniejszy, ale wciąż wyróżniony tłem
            var e6 = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(26, bilansColor.R, bilansColor.G, bilansColor.B)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(5, 1, 5, 1),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Child = Val(bilansLbl, $"{Math.Abs(bilans):N0}", bilansColor, 11)
            };
            Grid.SetColumn(e6, 6); row.Children.Add(e6);

            return row;
        }

        private Border CreateDashboardCard(string nazwa, decimal plan, decimal fakt, decimal zamLubWyd, decimal bilans, decimal stan, Color barColor, bool isSummary, string tooltip = null, int towarId = 0, List<(string nazwa, decimal kg, int towarId)> skladnikiRozpiska = null)
        {
            bool uzytoFakt = fakt > 0;
            bool uzywajWydan = rbBilansWydania?.IsChecked == true;
            string zamWydLabel = uzywajWydan ? "wyd" : "zam";
            decimal maxBarValue = Math.Max(Math.Max(plan, fakt), Math.Max(zamLubWyd, 1));
            double maxBarWidth = 155;  // Rozmiar L

            // === ASYMETRIA F: pasek statusu (lewo) + wielka liczba + pionowe równanie księgowe (prawo) ===
            decimal baseValCard = fakt > 0 ? fakt : plan;
            decimal poolCard = baseValCard + stan;
            var (statusColor, statusWord, _, statusPct) = StatusInfo(zamLubWyd, poolCard);

            bool stRed = !isSummary && statusWord == "Nadmiar";
            bool stOrange = !isSummary && statusWord == "Napięte";
            bool stGreen = !isSummary && statusWord == "OK";

            // Ciemne odcienie statusu dla liczby/% (pasek dostaje pełny kolor ze StatusInfo)
            Color numColor = isSummary ? Color.FromRgb(0x2C, 0x3E, 0x50)
                : stRed ? Color.FromRgb(0xC0, 0x39, 0x2B)
                : stOrange ? Color.FromRgb(0xB9, 0x77, 0x0E)
                : stGreen ? Color.FromRgb(0x1E, 0x84, 0x49)
                : Color.FromRgb(0x7F, 0x8C, 0x8D);
            Color stripeColor = isSummary ? Color.FromRgb(0x2C, 0x3E, 0x50) : statusColor;

            // Tło: tylko nadmiar barwi kartę; zieleń/pomarańcz = biała karta (sygnał niesie pasek + liczba)
            Color cardBg = stRed ? Color.FromRgb(0xFD, 0xEC, 0xEA) : Colors.White;
            Color cardBorder = stRed ? Color.FromRgb(0xE7, 0x4C, 0x3C) : Color.FromRgb(0xE3, 0xE8, 0xEC);

            var card = new Border
            {
                Background = new SolidColorBrush(cardBg),
                CornerRadius = new CornerRadius(9),
                Margin = new Thickness(2),
                Padding = new Thickness(0), // pasek statusu musi dotykać krawędzi
                BorderBrush = new SolidColorBrush(cardBorder),
                BorderThickness = new Thickness(1),
                Width = 280,  // wstępna — ApplyCardLayout() dopasowuje do szerokości panelu
                Height = 194, // karty podsumowania dnia
                ToolTip = tooltip ?? nazwa,
                Tag = new { TowarId = towarId, Nazwa = nazwa },
                Cursor = System.Windows.Input.Cursors.Hand,
                UseLayoutRounding = true,
                SnapsToDevicePixels = true
            };

            // Delikatna głębia (miękki cień) — spójny, lekki
            var baseShadow = new System.Windows.Media.Effects.DropShadowEffect
            {
                ShadowDepth = 1.5,
                BlurRadius = 7,
                Opacity = 0.16,
                Color = Color.FromRgb(0, 0, 0)
            };
            card.Effect = baseShadow;

            // ✅ KLIKNIĘCIE - filtruj zamówienia po tym produkcie
            if (towarId > 0)
            {
                card.MouseLeftButtonUp += (s, e) =>
                {
                    FilterOrdersByProduct(towarId, nazwa);
                };
            }

            // Hover — wyraźniejszy cień (afordancja klikalności), bez kolizji z animacją zaznaczenia
            card.MouseEnter += (s, e) =>
            {
                card.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    ShadowDepth = 2.5,
                    BlurRadius = 13,
                    Opacity = 0.28,
                    Color = Color.FromRgb(0, 0, 0)
                };
            };
            card.MouseLeave += (s, e) => card.Effect = baseShadow;

            // ✅ MENU KONTEKSTOWE (prawy przycisk)
            var contextMenu = new ContextMenu();

            var menuPowieksz = new MenuItem { Header = "🔍 Powiększ do nowego okna" };
            menuPowieksz.Click += async (s, e) =>
            {
                // Użyj tego samego okna co DashboardWindow
                await DashboardWindow.OpenProductDetailDirectlyAsync(_connLibra, _connHandel, nazwa, _selectedDate);
            };
            contextMenu.Items.Add(menuPowieksz);

            if (towarId > 0)
            {
                var menuPotencjalni = new MenuItem { Header = "👥 Potencjalni klienci" };
                menuPotencjalni.Click += (s, e) =>
                {
                    var okno = new PotencjalniOdbiorcy(
                        _connHandel,
                        towarId,
                        nazwa,
                        plan,
                        fakt,
                        zamLubWyd,
                        bilans,
                        _selectedDate);
                    okno.Show();
                };
                contextMenu.Items.Add(menuPotencjalni);

                var menuFiltruj = new MenuItem { Header = "🔎 Filtruj zamówienia" };
                menuFiltruj.Click += (s, e) =>
                {
                    FilterOrdersByProduct(towarId, nazwa);
                };
                contextMenu.Items.Add(menuFiltruj);
            }

            card.ContextMenu = contextMenu;

            // ✅ Pulsująca zielona obramówka dla filtrowanego produktu
            if (towarId > 0 && _selectedProductId.HasValue && _selectedProductId.Value == towarId)
            {
                card.BorderBrush = new SolidColorBrush(Color.FromRgb(46, 204, 113));
                card.BorderThickness = new Thickness(3);

                var pulseAnim = new System.Windows.Media.Animation.ColorAnimation
                {
                    From = Color.FromRgb(46, 204, 113),
                    To = Color.FromRgb(144, 238, 144),
                    Duration = TimeSpan.FromMilliseconds(700),
                    AutoReverse = true,
                    RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                    EasingFunction = new System.Windows.Media.Animation.SineEase()
                };

                var brush = new SolidColorBrush(Color.FromRgb(46, 204, 113));
                card.BorderBrush = brush;
                brush.BeginAnimation(SolidColorBrush.ColorProperty, pulseAnim);
            }

            string zamWydLabelCap = uzywajWydan ? "Wyd" : "Zam";
            var numBrush = new SolidColorBrush(numColor);

            var root = new Grid();
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });                       // pasek statusu
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });    // treść
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });                      // równanie

            // --- KOLUMNA 0: pasek statusu (pełny kolor) ---
            var stripe = new Border
            {
                Background = new SolidColorBrush(stripeColor),
                CornerRadius = new CornerRadius(8, 0, 0, 8)
            };
            Grid.SetColumn(stripe, 0);
            root.Children.Add(stripe);

            // --- KOLUMNA 1: treść — nagłówek / wielka liczba / pula ---
            var content = new Grid { Margin = new Thickness(10, 8, 8, 8) };
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                       // nagłówek
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });  // bilans
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                       // pasek postępu
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                       // pula
            Grid.SetColumn(content, 1);
            root.Children.Add(content);

            // Nagłówek: zdjęcie 38 px + nazwa
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var productImage = towarId > 0 ? GetProductImage(towarId) : null;
            Border imgBorder;
            if (productImage != null)
            {
                var img = new System.Windows.Controls.Image
                {
                    Source = productImage,
                    Width = 38,
                    Height = 38,
                    Stretch = Stretch.Uniform
                };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                imgBorder = new Border
                {
                    Width = 40,
                    Height = 40,
                    CornerRadius = new CornerRadius(8),
                    ClipToBounds = true,
                    Child = img,
                    Margin = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(229, 232, 235)),
                    BorderThickness = new Thickness(1)
                };
            }
            else
            {
                imgBorder = new Border
                {
                    Width = 40,
                    Height = 40,
                    CornerRadius = new CornerRadius(8),
                    Background = new SolidColorBrush(Color.FromRgb(236, 240, 241)),
                    Margin = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = "📷",
                        FontSize = 12,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166))
                    }
                };
            }
            Grid.SetColumn(imgBorder, 0);
            headerGrid.Children.Add(imgBorder);

            var titleText = new TextBlock
            {
                Text = nazwa,
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x2C, 0x3E, 0x50)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(titleText, 1);
            headerGrid.Children.Add(titleText);
            Grid.SetRow(headerGrid, 0);
            content.Children.Add(headerGrid);

            // Środek: wielka liczba bilansu (ze znakiem minus przy nadmiarze) + etykieta
            string bilansTxt = bilans < 0 ? $"−{Math.Abs(bilans):N0}" : $"{bilans:N0}";
            double numSize = bilansTxt.Length > 6 ? 36 : 44;
            var centerStack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            centerStack.Children.Add(new TextBlock
            {
                Text = bilansTxt,
                FontSize = numSize,
                FontWeight = FontWeights.ExtraBold,
                Foreground = numBrush
            });
            string bilansEtykieta = bilans > 0 ? "DO SPRZEDANIA" : (bilans == 0 ? "WYPRZEDANE" : "NADMIAR — STOP");
            Color etykietaColor = (stGreen || isSummary) ? Color.FromRgb(0x9A, 0xA7, 0xB0) : numColor;
            centerStack.Children.Add(new TextBlock
            {
                Text = string.Join(" ", bilansEtykieta.Select(c => c.ToString())), // rozstrzelone litery
                FontSize = 8,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(etykietaColor),
                Margin = new Thickness(1, -1, 0, 0)
            });
            Grid.SetRow(centerStack, 1);
            content.Children.Add(centerStack);

            // Pasek postępu pod bilansem — % wykorzystania puli w kolorze statusu
            if (!isSummary)
            {
                var postep = BuildPostepBar(statusPct, statusColor);
                Grid.SetRow(postep, 2);
                content.Children.Add(postep);
            }

            // Dół: pula matka/córka (klikalne dzieci → filtr zamówień)
            string pulaTooltipText = null;
            if (skladnikiRozpiska != null && skladnikiRozpiska.Count > 1)
            {
                var dzieci = skladnikiRozpiska.Skip(1).Where(s => s.kg != 0).ToList();
                if (dzieci.Count > 0)
                {
                    var pulaBrush = new SolidColorBrush(Color.FromRgb(0x5B, 0x6F, 0xB5));
                    var line = new TextBlock
                    {
                        FontSize = 9.3,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        Foreground = pulaBrush
                    };
                    line.Inlines.Add(new Run("+ ") { FontWeight = FontWeights.Bold });
                    for (int i = 0; i < dzieci.Count; i++)
                    {
                        if (i > 0) line.Inlines.Add(new Run(" · "));
                        int childId = dzieci[i].towarId;
                        string childName = dzieci[i].nazwa;
                        var link = new System.Windows.Documents.Hyperlink
                        {
                            TextDecorations = null,
                            Cursor = System.Windows.Input.Cursors.Hand,
                            Foreground = pulaBrush,
                            ToolTip = $"Pokaż zamówienia: {childName}"
                        };
                        link.Inlines.Add(new Run($"{childName} "));
                        link.Inlines.Add(new Run($"{dzieci[i].kg:N0}") { FontWeight = FontWeights.Bold });
                        link.Click += (s, e) => { e.Handled = true; if (childId > 0) FilterOrdersByProduct(childId, childName); };
                        line.Inlines.Add(link);
                    }
                    Grid.SetRow(line, 3);
                    content.Children.Add(line);

                    pulaTooltipText = "Pula: " + string.Join(" · ", skladnikiRozpiska.Select(s => $"{s.nazwa} {s.kg:N0}"));
                }
            }

            // --- KOLUMNA 2: pionowe równanie księgowe (% u góry, pozycje przy dole, podwójna kreska) ---
            var eqBorder = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xED, 0xF0, 0xF2)),
                BorderThickness = new Thickness(1, 0, 0, 0),
                Padding = new Thickness(8, 8, 10, 8)
            };
            var eqGrid = new Grid();
            eqGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                       // %
            eqGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                       // separator
            eqGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });  // spacer
            eqGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                       // pozycje
            eqGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                       // podwójna kreska

            var pctText = new TextBlock
            {
                Text = (poolCard <= 0 && zamLubWyd > 0) ? ">100%" : $"{Math.Round(statusPct)}%",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = numBrush,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetRow(pctText, 0);
            eqGrid.Children.Add(pctText);

            var pctSep = new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(0xED, 0xF0, 0xF2)), Margin = new Thickness(0, 4, 0, 0) };
            Grid.SetRow(pctSep, 1);
            eqGrid.Children.Add(pctSep);

            var eqRows = new StackPanel();
            var serYellow = Color.FromRgb(0xE6, 0xB0, 0x08);   // Plan/Fakt
            var serPurple = Color.FromRgb(0x9B, 0x59, 0xB6);   // Stan
            var serBlue = Color.FromRgb(0x34, 0x98, 0xDB);     // Zam/Wyd
            void EqRow(string lbl, string val, Color valColor, bool strike = false)
            {
                var rowG = new Grid { Margin = new Thickness(0, 1, 0, 1) };
                rowG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                rowG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var l = new TextBlock
                {
                    Text = lbl,
                    FontSize = 9.6,
                    Foreground = new SolidColorBrush(strike ? Color.FromRgb(0xB9, 0xC4, 0xCC) : Color.FromRgb(0x8A, 0x98, 0xA3)),
                    TextDecorations = strike ? TextDecorations.Strikethrough : null,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(l, 0);
                rowG.Children.Add(l);
                var v = new TextBlock
                {
                    Text = val,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(strike ? Color.FromRgb(0xB9, 0xC4, 0xCC) : valColor),
                    TextDecorations = strike ? TextDecorations.Strikethrough : null,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                };
                System.Windows.Documents.Typography.SetNumeralAlignment(v, FontNumeralAlignment.Tabular);
                Grid.SetColumn(v, 1);
                rowG.Children.Add(v);
                eqRows.Children.Add(rowG);
            }
            if (fakt > 0)
            {
                EqRow("Plan", $"{plan:N0}", serYellow, strike: true);
                EqRow("Fakt", $"{fakt:N0}", serYellow);
            }
            else
            {
                EqRow("Plan", $"{plan:N0}", serYellow);
            }
            EqRow("Stan", $"{stan:N0}", serPurple); // zawsze, także przy 0
            EqRow(zamWydLabelCap, $"−{zamLubWyd:N0}", serBlue);
            Grid.SetRow(eqRows, 3);
            eqGrid.Children.Add(eqRows);

            // Podwójna kreska "=" na dole kolumny
            var dblLine = new StackPanel { Margin = new Thickness(0, 3, 0, 0) };
            dblLine.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(0x7F, 0x8C, 0x8D)) });
            dblLine.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(0x7F, 0x8C, 0x8D)), Margin = new Thickness(0, 2, 0, 0) });
            Grid.SetRow(dblLine, 4);
            eqGrid.Children.Add(dblLine);

            // WYNIK pod kreską — ta sama liczba co po lewej, w kolorze statusu, z "kg"
            eqGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var eqWynik = new TextBlock
            {
                Text = bilansTxt + " kg",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = numBrush,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 2, 0, 0)
            };
            System.Windows.Documents.Typography.SetNumeralAlignment(eqWynik, FontNumeralAlignment.Tabular);
            Grid.SetRow(eqWynik, 5);
            eqGrid.Children.Add(eqWynik);

            eqBorder.Child = eqGrid;
            Grid.SetColumn(eqBorder, 2);
            root.Children.Add(eqBorder);

            // Tooltip z paskiem-skalą usunięty na życzenie — karta bez dymka po najechaniu
            card.Child = root;
            return card;
        }

        private Grid CreateMiniBar(string label, decimal value, decimal maxValue, double maxWidth, Color color, bool strikethrough)
        {
            var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };  // Rozmiar L
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            double barWidth = maxValue > 0 ? (double)(value / maxValue) * maxWidth : 5;

            var bar = new Border
            {
                Height = 18,  // Rozmiar L (S=12, M=14, L=18)
                Width = Math.Max(barWidth, 5),
                HorizontalAlignment = HorizontalAlignment.Left,
                CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(color)
            };
            Grid.SetColumn(bar, 0);
            grid.Children.Add(bar);

            var text = new TextBlock
            {
                Text = $"{label} {value:N0}",
                FontSize = 10,  // Rozmiar L (S=8, M=9, L=10)
                FontWeight = strikethrough ? FontWeights.Normal : FontWeights.SemiBold,
                Foreground = strikethrough
                    ? new SolidColorBrush(Color.FromRgb(160, 160, 160))
                    : new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                TextDecorations = strikethrough ? TextDecorations.Strikethrough : null,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            };
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);

            return grid;
        }

        /// <summary>
        /// Tworzy powiększoną kartę Kurczak A z rozbiciem na klasy wagowe (Kl.5-12)
        /// </summary>
        private Border CreateKurczakACard(string nazwa, decimal plan, decimal fakt,
            decimal zamLubWyd, decimal bilans, decimal stan, Color barColor,
            string tooltip, int towarId, int[] pojemnikiKlasy, bool uzywajWydan,
            Dictionary<int, int>? rezerwacjeKlas = null)
        {
            bool uzytoFakt = fakt > 0;
            string zamWydLabel = uzywajWydan ? "wyd" : "zam";
            decimal maxBarValue = Math.Max(Math.Max(plan, fakt), Math.Max(zamLubWyd, 1));
            double maxBarWidth = 155;

            // Status wg % zamówień do (przychód+stan) — te same progi co karty elementów (Asymetria F)
            var (kStatusColor, kWord, _, kPct) = StatusInfo(zamLubWyd, (fakt > 0 ? fakt : plan) + stan);

            bool kRed = kWord == "Nadmiar";
            bool kOrange = kWord == "Napięte";
            bool kGreen = kWord == "OK";
            Color kNum = kRed ? Color.FromRgb(0xC0, 0x39, 0x2B)
                : kOrange ? Color.FromRgb(0xB9, 0x77, 0x0E)
                : kGreen ? Color.FromRgb(0x1E, 0x84, 0x49)
                : Color.FromRgb(0x7F, 0x8C, 0x8D);
            Color kCardBg = kRed ? Color.FromRgb(0xFD, 0xEC, 0xEA) : Colors.White;
            Color kCardBorder = kRed ? Color.FromRgb(0xE7, 0x4C, 0x3C) : Color.FromRgb(0xE3, 0xE8, 0xEC);

            var card = new Border
            {
                Background = new SolidColorBrush(kCardBg),
                CornerRadius = new CornerRadius(9),
                Margin = new Thickness(2),
                Padding = new Thickness(0), // pasek statusu dotyka krawędzi
                BorderBrush = new SolidColorBrush(kCardBorder),
                BorderThickness = new Thickness(1),
                Width = 280,      // wstępna — ApplyCardLayout() nadaje 1 slot (zwinięty) lub 2 sloty (rozwinięty)
                MinHeight = 194,  // tabelka klas może być wyższa — karta wtedy urośnie
                Tag = new { TowarId = towarId, Nazwa = nazwa },
                Cursor = System.Windows.Input.Cursors.Hand,
                UseLayoutRounding = true,
                SnapsToDevicePixels = true
            };

            if (towarId > 0)
                card.MouseLeftButtonUp += (s, e) => FilterOrdersByProduct(towarId, nazwa);

            // Double-click → WidokKlasWagowychDnia
            card.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                {
                    e.Handled = true;
                    var prognozaDict = new Dictionary<int, int>();
                    for (int k = 5; k <= 12; k++)
                        prognozaDict[k] = pojemnikiKlasy[k];
                    var widok = new WidokKlasWagowychDnia(_selectedDate, _connLibra, prognozaDict);
                    widok.Show();
                }
            };

            // Menu kontekstowe
            var contextMenu = new ContextMenu();
            var menuKlasy = new MenuItem { Header = "Pokaż klasy wagowe dnia" };
            menuKlasy.Click += (s, e) =>
            {
                var prognozaDict = new Dictionary<int, int>();
                for (int k = 5; k <= 12; k++)
                    prognozaDict[k] = pojemnikiKlasy[k];
                var widok = new WidokKlasWagowychDnia(_selectedDate, _connLibra, prognozaDict);
                widok.Show();
            };
            contextMenu.Items.Add(menuKlasy);

            var menuKtoZamowil = new MenuItem { Header = "Kto zamówił Kurczak A?" };
            menuKtoZamowil.Click += async (s, e) => await PokazKtoZamowilKurczakAAsync();
            contextMenu.Items.Add(menuKtoZamowil);

            contextMenu.Items.Add(new Separator());
            var menuPowieksz = new MenuItem { Header = "Powiększ do nowego okna" };
            menuPowieksz.Click += async (s, e) =>
                await DashboardWindow.OpenProductDetailDirectlyAsync(_connLibra, _connHandel, nazwa, _selectedDate);
            contextMenu.Items.Add(menuPowieksz);
            if (towarId > 0)
            {
                var menuFiltruj = new MenuItem { Header = "Filtruj zamówienia" };
                menuFiltruj.Click += (s, e) => FilterOrdersByProduct(towarId, nazwa);
                contextMenu.Items.Add(menuFiltruj);
            }
            card.ContextMenu = contextMenu;

            // Pulsacja jeśli filtrowany
            if (towarId > 0 && _selectedProductId.HasValue && _selectedProductId.Value == towarId)
            {
                card.BorderBrush = new SolidColorBrush(Color.FromRgb(46, 204, 113));
                card.BorderThickness = new Thickness(3);
                var pulseAnim = new System.Windows.Media.Animation.ColorAnimation
                {
                    From = Color.FromRgb(46, 204, 113),
                    To = Color.FromRgb(144, 238, 144),
                    Duration = TimeSpan.FromMilliseconds(700),
                    AutoReverse = true,
                    RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                    EasingFunction = new System.Windows.Media.Animation.SineEase()
                };
                var brush = new SolidColorBrush(Color.FromRgb(46, 204, 113));
                card.BorderBrush = brush;
                brush.BeginAnimation(SolidColorBrush.ColorProperty, pulseAnim);
            }

            // Główny layout: pasek statusu | treść | równanie (przy ścianie) | tabelka klas (zwijana strzałką przy nazwie)
            var mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });                       // pasek statusu
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });    // treść
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });                      // równanie
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });                         // klasy wagowe (Visibility)

            string zamWydCapK = uzywajWydan ? "Wyd" : "Zam";
            decimal bazaK = fakt > 0 ? fakt : plan;
            decimal poolK = bazaK + stan;
            var kNumBrush = new SolidColorBrush(kNum);

            // --- KOLUMNA 0: pasek statusu ---
            var stripeK = new Border
            {
                Background = new SolidColorBrush(kStatusColor),
                CornerRadius = new CornerRadius(8, 0, 0, 8)
            };
            Grid.SetColumn(stripeK, 0);
            mainGrid.Children.Add(stripeK);

            // --- KOLUMNA 1: treść — nagłówek / wielka liczba ---
            var contentK = new Grid { Margin = new Thickness(10, 8, 8, 8) };
            contentK.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                       // nagłówek
            contentK.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });  // bilans
            contentK.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                       // pasek postępu
            Grid.SetColumn(contentK, 1);
            mainGrid.Children.Add(contentK);

            var headerGridK = new Grid();
            headerGridK.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });               // zdjęcie
            headerGridK.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // nazwa
            headerGridK.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });               // strzałka rozwijania

            var productImage = towarId > 0 ? GetProductImage(towarId) : null;
            Border imgBorder;
            if (productImage != null)
            {
                var img = new System.Windows.Controls.Image
                {
                    Source = productImage, Width = 38, Height = 38, Stretch = Stretch.Uniform
                };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                imgBorder = new Border
                {
                    Width = 40, Height = 40, CornerRadius = new CornerRadius(8),
                    ClipToBounds = true, Child = img,
                    Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(229, 232, 235)),
                    BorderThickness = new Thickness(1)
                };
            }
            else
            {
                imgBorder = new Border
                {
                    Width = 40, Height = 40, CornerRadius = new CornerRadius(8),
                    Background = new SolidColorBrush(Color.FromRgb(236, 240, 241)),
                    Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = "\U0001F4F7", FontSize = 12,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166))
                    }
                };
            }
            Grid.SetColumn(imgBorder, 0);
            headerGridK.Children.Add(imgBorder);

            var titleK = new TextBlock
            {
                Text = nazwa,
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x2C, 0x3E, 0x50)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(titleK, 1);
            headerGridK.Children.Add(titleK);

            // Strzałka rozwijania klas — PRZY NAZWIE (klik tutaj pokazuje/ukrywa tabelkę klas)
            var arrowText = new TextBlock
            {
                Text = _kurczakAExpanded ? "◀" : "▶",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x6B, 0x7A, 0x86)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var arrowHeader = new Border
            {
                Width = 22,
                Height = 22,
                CornerRadius = new CornerRadius(5),
                Background = new SolidColorBrush(Color.FromRgb(0xF2, 0xF4, 0xF6)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "Pokaż/ukryj klasy wagowe",
                Child = arrowText
            };
            arrowHeader.MouseEnter += (s, e) => arrowHeader.Background = new SolidColorBrush(Color.FromRgb(0xE1, 0xE8, 0xED));
            arrowHeader.MouseLeave += (s, e) => arrowHeader.Background = new SolidColorBrush(Color.FromRgb(0xF2, 0xF4, 0xF6));
            Grid.SetColumn(arrowHeader, 2);
            headerGridK.Children.Add(arrowHeader);

            Grid.SetRow(headerGridK, 0);
            contentK.Children.Add(headerGridK);

            string bilansTxtK = bilans < 0 ? $"−{Math.Abs(bilans):N0}" : $"{bilans:N0}";
            double numSizeK = bilansTxtK.Length > 6 ? 36 : 44;
            var centerK = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            centerK.Children.Add(new TextBlock
            {
                Text = bilansTxtK,
                FontSize = numSizeK,
                FontWeight = FontWeights.ExtraBold,
                Foreground = kNumBrush
            });
            string etykietaK = bilans > 0 ? "DO SPRZEDANIA" : (bilans == 0 ? "WYPRZEDANE" : "NADMIAR — STOP");
            Color etykietaColorK = kGreen ? Color.FromRgb(0x9A, 0xA7, 0xB0) : kNum;
            centerK.Children.Add(new TextBlock
            {
                Text = string.Join(" ", etykietaK.Select(c => c.ToString())), // rozstrzelone litery
                FontSize = 8,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(etykietaColorK),
                Margin = new Thickness(1, -1, 0, 0)
            });
            Grid.SetRow(centerK, 1);
            contentK.Children.Add(centerK);

            // Pasek postępu pod bilansem — % wykorzystania puli w kolorze statusu
            var postepK = BuildPostepBar(kPct, kStatusColor);
            Grid.SetRow(postepK, 2);
            contentK.Children.Add(postepK);

            // --- KOLUMNA 2: pionowe równanie (Plan/Fakt, Stan jeśli > 0, Zam, podwójna kreska) ---
            var eqBorderK = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xED, 0xF0, 0xF2)),
                BorderThickness = new Thickness(1, 0, 0, 0),
                Padding = new Thickness(8, 8, 10, 8)
            };
            var eqGridK = new Grid();
            eqGridK.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            eqGridK.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            eqGridK.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            eqGridK.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            eqGridK.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var pctTextK = new TextBlock
            {
                Text = (poolK <= 0 && zamLubWyd > 0) ? ">100%" : $"{Math.Round(kPct)}%",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = kNumBrush,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetRow(pctTextK, 0);
            eqGridK.Children.Add(pctTextK);

            var pctSepK = new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(0xED, 0xF0, 0xF2)), Margin = new Thickness(0, 4, 0, 0) };
            Grid.SetRow(pctSepK, 1);
            eqGridK.Children.Add(pctSepK);

            var eqRowsK = new StackPanel();
            var serYellowK = Color.FromRgb(0xE6, 0xB0, 0x08);   // Plan/Fakt
            var serPurpleK = Color.FromRgb(0x9B, 0x59, 0xB6);   // Stan
            var serBlueK = Color.FromRgb(0x34, 0x98, 0xDB);     // Zam/Wyd
            void EqRowK(string lbl, string val, Color valColor, bool strike = false)
            {
                var rowG = new Grid { Margin = new Thickness(0, 1, 0, 1) };
                rowG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                rowG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var l = new TextBlock
                {
                    Text = lbl,
                    FontSize = 9.6,
                    Foreground = new SolidColorBrush(strike ? Color.FromRgb(0xB9, 0xC4, 0xCC) : Color.FromRgb(0x8A, 0x98, 0xA3)),
                    TextDecorations = strike ? TextDecorations.Strikethrough : null,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(l, 0);
                rowG.Children.Add(l);
                var v = new TextBlock
                {
                    Text = val,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(strike ? Color.FromRgb(0xB9, 0xC4, 0xCC) : valColor),
                    TextDecorations = strike ? TextDecorations.Strikethrough : null,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                };
                System.Windows.Documents.Typography.SetNumeralAlignment(v, FontNumeralAlignment.Tabular);
                Grid.SetColumn(v, 1);
                rowG.Children.Add(v);
                eqRowsK.Children.Add(rowG);
            }
            if (fakt > 0)
            {
                EqRowK("Plan", $"{plan:N0}", serYellowK, strike: true);
                EqRowK("Fakt", $"{fakt:N0}", serYellowK);
            }
            else
            {
                EqRowK("Plan", $"{plan:N0}", serYellowK);
            }
            if (stan > 0)
                EqRowK("Stan", $"{stan:N0}", serPurpleK); // u Kurczaka A tylko gdy > 0
            EqRowK(zamWydCapK, $"−{zamLubWyd:N0}", serBlueK);
            Grid.SetRow(eqRowsK, 3);
            eqGridK.Children.Add(eqRowsK);

            var dblLineK = new StackPanel { Margin = new Thickness(0, 3, 0, 0) };
            dblLineK.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(0x7F, 0x8C, 0x8D)) });
            dblLineK.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(0x7F, 0x8C, 0x8D)), Margin = new Thickness(0, 2, 0, 0) });
            Grid.SetRow(dblLineK, 4);
            eqGridK.Children.Add(dblLineK);

            // WYNIK pod kreską — jak po lewej, w kolorze statusu, z "kg"
            eqGridK.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var eqWynikK = new TextBlock
            {
                Text = bilansTxtK + " kg",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = kNumBrush,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 2, 0, 0)
            };
            System.Windows.Documents.Typography.SetNumeralAlignment(eqWynikK, FontNumeralAlignment.Tabular);
            Grid.SetRow(eqWynikK, 5);
            eqGridK.Children.Add(eqWynikK);

            eqBorderK.Child = eqGridK;
            Grid.SetColumn(eqBorderK, 2);
            mainGrid.Children.Add(eqBorderK);

            // Tooltip z paskiem-skalą usunięty na życzenie — zostaje tylko prosta podpowiedź tekstowa
            card.ToolTip = "Kliknij 2× — klasy wagowe dnia";

            // ========== KOLUMNA 3: klasy wagowe (treść BEZ ZMIAN — zwijana uchwytem) ==========
            var rightBorder = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0xE6, 0xEA)),
                BorderThickness = new Thickness(1, 0, 0, 0),
                Margin = new Thickness(8, 6, 0, 6),
                Padding = new Thickness(10, 0, 6, 0),
                Width = 230,
                Visibility = _kurczakAExpanded ? Visibility.Visible : Visibility.Collapsed
            };

            var rightStack = new StackPanel();

            // Kolory klas wagowych
            var klasaKolory = new Dictionary<int, Color>
            {
                { 5, Color.FromRgb(220, 38, 38) },
                { 6, Color.FromRgb(234, 88, 12) },
                { 7, Color.FromRgb(202, 138, 4) },
                { 8, Color.FromRgb(101, 163, 13) },
                { 9, Color.FromRgb(22, 163, 74) },
                { 10, Color.FromRgb(8, 145, 178) },
                { 11, Color.FromRgb(37, 99, 235) },
                { 12, Color.FromRgb(124, 58, 237) }
            };

            // Oblicz sumy: duży (5-8), mały (9-12), total
            int pojDuzy = 0, pojMaly = 0;
            for (int kl = 5; kl <= 8; kl++) pojDuzy += pojemnikiKlasy[kl];
            for (int kl = 9; kl <= 12; kl++) pojMaly += pojemnikiKlasy[kl];
            int sumaPoj = pojDuzy + pojMaly;
            decimal kgDuzy = pojDuzy * 15m;
            decimal kgMaly = pojMaly * 15m;
            decimal sumaKg = kgDuzy + kgMaly;

            // Rezerwacje per klasa (pojemniki → kg)
            var rezKlas = rezerwacjeKlas ?? new Dictionary<int, int>();

            // Helper: wiersz klasy wagowej — Plan / Zamów. / Wolne
            void DodajWierszKlasy(StackPanel parent, int kl, int poj)
            {
                int rezPoj = rezKlas.TryGetValue(kl, out var rp) ? rp : 0;
                if (poj == 0 && rezPoj == 0) return;
                decimal planKg = poj * 15m;
                decimal zamKg = rezPoj * 15m;
                decimal wolneKg = planKg - zamKg;
                var klColor = klasaKolory.TryGetValue(kl, out var cc) ? cc : Color.FromRgb(128, 128, 128);

                var row = new Grid { Margin = new Thickness(0, 1, 0, 1), Height = 16 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) });

                // Kółko + klasa
                var lbl = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                lbl.Children.Add(new Border
                {
                    Width = 8, Height = 8, CornerRadius = new CornerRadius(4),
                    Background = new SolidColorBrush(klColor),
                    Margin = new Thickness(0, 0, 3, 0), VerticalAlignment = VerticalAlignment.Center
                });
                lbl.Children.Add(new TextBlock
                {
                    Text = $"Kl.{kl}", FontSize = 9, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                    VerticalAlignment = VerticalAlignment.Center
                });
                Grid.SetColumn(lbl, 0);
                row.Children.Add(lbl);

                // Plan
                var vPlan = new TextBlock
                {
                    Text = poj > 0 ? $"{planKg:N0}" : "-",
                    FontSize = 9, Foreground = new SolidColorBrush(poj > 0 ? Color.FromRgb(44, 62, 80) : Color.FromRgb(180, 180, 180)),
                    VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right
                };
                Grid.SetColumn(vPlan, 1);
                row.Children.Add(vPlan);

                // Zamówione
                var vZam = new TextBlock
                {
                    Text = rezPoj > 0 ? $"{zamKg:N0}" : "-",
                    FontSize = 9, Foreground = new SolidColorBrush(rezPoj > 0 ? Color.FromRgb(52, 152, 219) : Color.FromRgb(180, 180, 180)),
                    VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right
                };
                Grid.SetColumn(vZam, 2);
                row.Children.Add(vZam);

                // Wolne — zielony jeśli > 0, czerwony jeśli < 0, szary jeśli 0
                var wolneColor = wolneKg > 0 ? Color.FromRgb(22, 163, 74)
                    : wolneKg < 0 ? Color.FromRgb(220, 38, 38)
                    : Color.FromRgb(150, 150, 150);
                string wolnePrefix = wolneKg > 0 ? "+" : "";
                var vWolne = new TextBlock
                {
                    Text = (poj > 0 || rezPoj > 0) ? $"{wolnePrefix}{wolneKg:N0}" : "-",
                    FontSize = 9, FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(wolneColor),
                    VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right
                };
                Grid.SetColumn(vWolne, 3);
                row.Children.Add(vWolne);

                parent.Children.Add(row);
            }

            // Helper: nagłówek sekcji
            int RezSekcji(int od, int doo) { int s = 0; for (int k = od; k <= doo; k++) s += rezKlas.TryGetValue(k, out var v) ? v : 0; return s; }

            void DodajNaglowekSekcji(StackPanel parent, string tekst, Color kolor, decimal kgPlan, int klOd, int klDo)
            {
                decimal kgZam = RezSekcji(klOd, klDo) * 15m;
                decimal kgWolne = kgPlan - kgZam;
                var hdr = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(30, kolor.R, kolor.G, kolor.B)),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(5, 2, 5, 2),
                    Margin = new Thickness(0, 2, 0, 1)
                };
                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) });

                var tLabel = new TextBlock { Text = tekst, FontSize = 9, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(kolor), VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(tLabel, 0); g.Children.Add(tLabel);

                var tPlan = new TextBlock { Text = $"{kgPlan:N0}", FontSize = 9, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(kolor), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(tPlan, 1); g.Children.Add(tPlan);

                var tZam = new TextBlock { Text = kgZam > 0 ? $"{kgZam:N0}" : "-", FontSize = 9, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(kgZam > 0 ? Color.FromRgb(52, 152, 219) : Color.FromRgb(180, 180, 180)), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(tZam, 2); g.Children.Add(tZam);

                var wolneColor = kgWolne > 0 ? Color.FromRgb(22, 163, 74) : kgWolne < 0 ? Color.FromRgb(220, 38, 38) : Color.FromRgb(150, 150, 150);
                string wp = kgWolne > 0 ? "+" : "";
                var tWolne = new TextBlock { Text = $"{wp}{kgWolne:N0}", FontSize = 9, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(wolneColor), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(tWolne, 3); g.Children.Add(tWolne);

                hdr.Child = g;
                parent.Children.Add(hdr);
            }

            if (sumaPoj > 0 || rezKlas.Values.Sum() > 0)
            {
                // Nagłówki kolumn
                var colHdr = new Grid { Margin = new Thickness(0, 0, 0, 1) };
                colHdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
                colHdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) });
                colHdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) });
                colHdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) });
                var gray = new SolidColorBrush(Color.FromRgb(127, 140, 141));
                var h1 = new TextBlock { Text = "Plan", FontSize = 8, Foreground = gray, HorizontalAlignment = HorizontalAlignment.Right };
                Grid.SetColumn(h1, 1); colHdr.Children.Add(h1);
                var h2 = new TextBlock { Text = "Zamów.", FontSize = 8, Foreground = gray, HorizontalAlignment = HorizontalAlignment.Right };
                Grid.SetColumn(h2, 2); colHdr.Children.Add(h2);
                var h3 = new TextBlock { Text = "Wolne", FontSize = 8, Foreground = gray, HorizontalAlignment = HorizontalAlignment.Right };
                Grid.SetColumn(h3, 3); colHdr.Children.Add(h3);
                rightStack.Children.Add(colHdr);

                // === DUŻY (Kl.5-8) ===
                DodajNaglowekSekcji(rightStack, "DUŻY", Color.FromRgb(220, 80, 20), kgDuzy, 5, 8);
                for (int kl = 5; kl <= 8; kl++)
                    DodajWierszKlasy(rightStack, kl, pojemnikiKlasy[kl]);

                // === MAŁY (Kl.9-12) ===
                DodajNaglowekSekcji(rightStack, "MAŁY", Color.FromRgb(22, 120, 180), kgMaly, 9, 12);
                for (int kl = 9; kl <= 12; kl++)
                    DodajWierszKlasy(rightStack, kl, pojemnikiKlasy[kl]);
            }
            else
            {
                rightStack.Children.Add(new TextBlock
                {
                    Text = "Brak danych\nz harmonogramu",
                    FontSize = 9, FontStyle = FontStyles.Italic,
                    Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 160)),
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 10, 0, 0)
                });
            }

            rightBorder.Child = rightStack;
            Grid.SetColumn(rightBorder, 3);
            mainGrid.Children.Add(rightBorder);

            // Klik strzałki PRZY NAZWIE — rozwija/zwija tabelkę klas (boczny uchwyt usunięty,
            // dzięki czemu równanie plan/fakt/zam jest tuż przy prawej krawędzi karty)
            arrowHeader.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            arrowHeader.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                _kurczakAExpanded = !_kurczakAExpanded;
                rightBorder.Visibility = _kurczakAExpanded ? Visibility.Visible : Visibility.Collapsed;
                arrowText.Text = _kurczakAExpanded ? "◀" : "▶";
                ForceCardLayout();
            };

            card.Child = mainGrid;
            _kurczakACard = card; // referencja dla ApplyCardLayout (2 sloty po rozwinięciu)
            return card;
        }

        /// <summary>
        /// Pasek podsumowania dnia — kluczowe liczby na jednym pasku
        /// </summary>
        private UIElement BuildDaySummaryBar(int liczbaZamowien, decimal plan, decimal fakt,
            decimal zamWyd, decimal bilans, bool uzywajWydan)
        {
            var bar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10, 6, 10, 6)
            };

            var grid = new Grid();
            for (int i = 0; i < 4; i++)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            void AddCell(int col, string label, string value, Color valueColor)
            {
                var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                sp.Children.Add(new TextBlock
                {
                    Text = label, FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166)),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                sp.Children.Add(new TextBlock
                {
                    Text = value, FontSize = 13, FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(valueColor),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                Grid.SetColumn(sp, col);
                grid.Children.Add(sp);
            }

            AddCell(0, "Zamówień", $"{liczbaZamowien}", Color.FromRgb(236, 240, 241));
            AddCell(1, "Plan", $"{plan:N0}", Color.FromRgb(241, 196, 15));
            AddCell(2, uzywajWydan ? "Wydano" : "Zamówiono", $"{zamWyd:N0}", Color.FromRgb(52, 152, 219));

            var bilansColor = bilans >= 0 ? Color.FromRgb(46, 204, 113) : Color.FromRgb(231, 76, 60);
            string bilansPrefix = bilans >= 0 ? "+" : "";
            AddCell(3, "Bilans", $"{bilansPrefix}{bilans:N0}", bilansColor);

            bar.Child = grid;
            return bar;
        }

        /// <summary>
        /// Kompaktowa karta produktu (160px) — mniej paddings, 2 linie danych
        /// </summary>
        private Border CreateCompactCard(string nazwa, decimal plan, decimal fakt,
            decimal zamLubWyd, decimal bilans, decimal stan, Color barColor,
            string tooltip, int towarId)
        {
            bool uzytoFakt = fakt > 0;
            bool uzywajWydan = rbBilansWydania?.IsChecked == true;
            string zamWydLabel = uzywajWydan ? "wyd" : "zam";

            var card = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(5),
                Margin = new Thickness(3),
                Padding = new Thickness(8, 6, 8, 6),
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                BorderThickness = new Thickness(1),
                Width = 160,
                ToolTip = tooltip ?? nazwa,
                Tag = new { TowarId = towarId, Nazwa = nazwa },
                Cursor = System.Windows.Input.Cursors.Hand
            };

            if (towarId > 0)
                card.MouseLeftButtonUp += (s, e) => FilterOrdersByProduct(towarId, nazwa);

            // Menu kontekstowe
            var contextMenu = new ContextMenu();
            var menuPowieksz = new MenuItem { Header = "Powiększ do nowego okna" };
            menuPowieksz.Click += async (s, e) =>
                await DashboardWindow.OpenProductDetailDirectlyAsync(_connLibra, _connHandel, nazwa, _selectedDate);
            contextMenu.Items.Add(menuPowieksz);
            if (towarId > 0)
            {
                var menuFiltruj = new MenuItem { Header = "Filtruj zamówienia" };
                menuFiltruj.Click += (s, e) => FilterOrdersByProduct(towarId, nazwa);
                contextMenu.Items.Add(menuFiltruj);
            }
            card.ContextMenu = contextMenu;

            // Pulsacja
            if (towarId > 0 && _selectedProductId.HasValue && _selectedProductId.Value == towarId)
            {
                card.BorderBrush = new SolidColorBrush(Color.FromRgb(46, 204, 113));
                card.BorderThickness = new Thickness(2);
            }

            var stack = new StackPanel();

            // Nagłówek: zdjęcie + nazwa
            var hdr = new Grid { Margin = new Thickness(0, 0, 0, 3) };
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var productImage = towarId > 0 ? GetProductImage(towarId) : null;
            if (productImage != null)
            {
                var img = new System.Windows.Controls.Image
                {
                    Source = productImage, Width = 24, Height = 24, Stretch = Stretch.Uniform
                };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                var imgB = new Border
                {
                    Width = 26, Height = 26, CornerRadius = new CornerRadius(4),
                    ClipToBounds = true, Child = img,
                    Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(imgB, 0);
                hdr.Children.Add(imgB);
            }

            var title = new TextBlock
            {
                Text = ShortenProductName(nazwa), FontSize = 10, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(title, 1);
            hdr.Children.Add(title);
            stack.Children.Add(hdr);

            // Pasek kolorowy (plan lub fakt)
            decimal barSource = uzytoFakt ? fakt : plan;
            decimal maxVal = Math.Max(Math.Max(barSource, zamLubWyd), 1);
            double maxBarW = 120;

            // Plan/fakt linia
            var planLine = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            planLine.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            planLine.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var planBar = new Border
            {
                Height = 10, Width = Math.Max((double)(barSource / maxVal) * maxBarW, 4),
                HorizontalAlignment = HorizontalAlignment.Left, CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(uzytoFakt ? Color.FromRgb(241, 196, 15) : Color.FromRgb(189, 195, 199))
            };
            Grid.SetColumn(planBar, 0);
            planLine.Children.Add(planBar);

            var planText = new TextBlock
            {
                Text = $"{(uzytoFakt ? "f" : "p")} {barSource:N0}", FontSize = 8,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 0, 0, 0)
            };
            Grid.SetColumn(planText, 1);
            planLine.Children.Add(planText);
            stack.Children.Add(planLine);

            // Zam/wyd linia
            var zamLine = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            zamLine.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            zamLine.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var zamBar = new Border
            {
                Height = 10, Width = Math.Max((double)(zamLubWyd / maxVal) * maxBarW, 4),
                HorizontalAlignment = HorizontalAlignment.Left, CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(Color.FromRgb(52, 152, 219))
            };
            Grid.SetColumn(zamBar, 0);
            zamLine.Children.Add(zamBar);

            var zamText = new TextBlock
            {
                Text = $"{zamWydLabel} {zamLubWyd:N0}", FontSize = 8,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 0, 0, 0)
            };
            Grid.SetColumn(zamText, 1);
            zamLine.Children.Add(zamText);
            stack.Children.Add(zamLine);

            // Bilans — na dole
            var bilansColor = bilans >= 0 ? Color.FromRgb(39, 174, 96) : Color.FromRgb(231, 76, 60);
            string prefix = bilans >= 0 ? "+" : "";
            var bilansText = new TextBlock
            {
                Text = $"{prefix}{bilans:N0}",
                FontSize = 10, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(bilansColor),
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 2, 0, 0)
            };
            stack.Children.Add(bilansText);

            card.Child = stack;
            return card;
        }

        /// <summary>
        /// Pokazuje okno "Kto zamówił Kurczak A" — zamówienia pogrupowane per handlowiec,
        /// z oznaczeniem które mają/nie mają rezerwacji klas wagowych.
        /// </summary>
        private async Task PokazKtoZamowilKurczakAAsync()
        {
            try
            {
                string dateCol = (_showBySlaughterDate && _slaughterDateColumnExists) ? "DataUboju" : "DataZamowienia";

                // 1. Znajdź ID produktu Kurczak A z cache
                var kurczakAIds = _productCatalogCache
                    .Where(p => p.Value.Contains("Kurczak A", StringComparison.OrdinalIgnoreCase))
                    .Select(p => p.Key).ToList();

                if (!kurczakAIds.Any())
                {
                    MessageBox.Show("Nie znaleziono produktu Kurczak A w katalogu.", "Informacja",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 2. Pobierz zamówienia z tym produktem na wybrany dzień (KlientId → nazwa z cache)
                var kontrahenci = _cachedKontrahenci.Count > 0 ? _cachedKontrahenci : new Dictionary<int, (string Name, string Salesman)>();
                var zamowienia = new List<(int Id, string Odbiorca, string Handlowiec, decimal Kg)>();
                await using (var cn = new SqlConnection(_connLibra))
                {
                    await cn.OpenAsync();
                    var idList = string.Join(",", kurczakAIds);
                    var sql = $@"SELECT z.Id, z.KlientId, ISNULL(SUM(t.Ilosc),0) AS Kg
                                FROM [dbo].[ZamowieniaMieso] z
                                JOIN [dbo].[ZamowieniaMiesoTowar] t ON z.Id = t.ZamowienieId
                                WHERE z.{dateCol} = @Data AND z.Status <> 'Anulowane'
                                  AND t.KodTowaru IN ({idList})
                                GROUP BY z.Id, z.KlientId
                                ORDER BY z.KlientId";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Data", _selectedDate.Date);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        int id = rdr.GetInt32(0);
                        int klientId = rdr.IsDBNull(1) ? 0 : rdr.GetInt32(1);
                        decimal kg = rdr.IsDBNull(2) ? 0m : Convert.ToDecimal(rdr.GetValue(2));
                        var (nazwa, handlowiec) = kontrahenci.TryGetValue(klientId, out var kh)
                            ? kh : ($"Klient {klientId}", "?");
                        zamowienia.Add((id, nazwa, handlowiec, kg));
                    }
                }

                // 2. Pobierz rezerwacje klas dla tych zamówień
                var zamIdList = zamowienia.Select(z => z.Id).ToList();
                var maRezerwacje = new HashSet<int>();
                if (zamIdList.Any())
                {
                    await using var cn2 = new SqlConnection(_connLibra);
                    await cn2.OpenAsync();
                    var sqlR = $@"SELECT DISTINCT ZamowienieId FROM [dbo].[RezerwacjeKlasWagowych]
                                  WHERE ZamowienieId IN ({string.Join(",", zamIdList)}) AND Status = 'Aktywna'";
                    await using var cmdR = new SqlCommand(sqlR, cn2);
                    await using var rdR = await cmdR.ExecuteReaderAsync();
                    while (await rdR.ReadAsync())
                        maRezerwacje.Add(rdR.GetInt32(0));
                }

                // 3. Zbuduj okno WPF
                var win = new Window
                {
                    Title = $"Kurczak A — Zamówienia na {_selectedDate:dd.MM.yyyy}",
                    Width = 650, Height = 550, WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this, Background = new SolidColorBrush(Color.FromRgb(245, 247, 250)),
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
                };
                WindowIconHelper.SetIcon(win);

                var mainStack = new StackPanel { Margin = new Thickness(15) };
                var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = mainStack };
                win.Content = scroll;

                // Podsumowanie
                int total = zamowienia.Count;
                int bezKlas = zamowienia.Count(z => !maRezerwacje.Contains(z.Id));
                decimal totalKg = zamowienia.Sum(z => z.Kg);
                var summaryColor = bezKlas == 0 ? Color.FromRgb(22, 163, 74) : Color.FromRgb(220, 80, 20);

                var summaryBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                    CornerRadius = new CornerRadius(6), Padding = new Thickness(15, 10, 15, 10),
                    Margin = new Thickness(0, 0, 0, 12)
                };
                var summaryGrid = new Grid();
                summaryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                summaryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                summaryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var sumText = new TextBlock { FontSize = 13, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
                sumText.Inlines.Add(new Run($"{total} zamówień") { FontWeight = FontWeights.Bold });
                sumText.Inlines.Add(new Run($"  |  {totalKg:N0} kg") { Foreground = new SolidColorBrush(Color.FromRgb(189, 195, 199)) });
                Grid.SetColumn(sumText, 0);
                summaryGrid.Children.Add(sumText);

                var bezKlasText = new TextBlock
                {
                    Text = bezKlas == 0 ? "Wszystkie mają klasy" : $"{bezKlas} bez klas!",
                    FontSize = 12, FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(summaryColor),
                    VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(15, 0, 0, 0)
                };
                Grid.SetColumn(bezKlasText, 1);
                summaryGrid.Children.Add(bezKlasText);
                summaryBorder.Child = summaryGrid;
                mainStack.Children.Add(summaryBorder);

                // Grupowanie per handlowiec
                var grupy = zamowienia.GroupBy(z => z.Handlowiec).OrderBy(g => g.Key);
                foreach (var grupa in grupy)
                {
                    decimal kgGrupy = grupa.Sum(z => z.Kg);
                    int bezKlasGrupy = grupa.Count(z => !maRezerwacje.Contains(z.Id));

                    // Nagłówek handlowca
                    var hdrBorder = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(52, 73, 94)),
                        CornerRadius = new CornerRadius(4), Padding = new Thickness(10, 5, 10, 5),
                        Margin = new Thickness(0, 6, 0, 2)
                    };
                    var hdrGrid = new Grid();
                    hdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    hdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    hdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var hdrName = new TextBlock
                    {
                        Text = grupa.Key, FontSize = 12, FontWeight = FontWeights.Bold,
                        Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(hdrName, 0);
                    hdrGrid.Children.Add(hdrName);

                    var hdrKg = new TextBlock
                    {
                        Text = $"{kgGrupy:N0} kg", FontSize = 11,
                        Foreground = new SolidColorBrush(Color.FromRgb(189, 195, 199)),
                        VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0)
                    };
                    Grid.SetColumn(hdrKg, 1);
                    hdrGrid.Children.Add(hdrKg);

                    if (bezKlasGrupy > 0)
                    {
                        var hdrWarn = new TextBlock
                        {
                            Text = $"  {bezKlasGrupy} bez klas",
                            FontSize = 10, FontWeight = FontWeights.Bold,
                            Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60)),
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        Grid.SetColumn(hdrWarn, 2);
                        hdrGrid.Children.Add(hdrWarn);
                    }
                    hdrBorder.Child = hdrGrid;
                    mainStack.Children.Add(hdrBorder);

                    // Wiersze zamówień
                    foreach (var zam in grupa.OrderByDescending(z => z.Kg))
                    {
                        bool maKlasy = maRezerwacje.Contains(zam.Id);
                        var rowBorder = new Border
                        {
                            Background = Brushes.White,
                            CornerRadius = new CornerRadius(3),
                            BorderBrush = new SolidColorBrush(maKlasy ? Color.FromRgb(200, 230, 200) : Color.FromRgb(255, 220, 220)),
                            BorderThickness = new Thickness(0, 0, 0, 1),
                            Padding = new Thickness(10, 4, 10, 4),
                            Margin = new Thickness(0, 1, 0, 0)
                        };

                        var rowGrid = new Grid();
                        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });   // status
                        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // odbiorca
                        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });   // kg
                        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });   // klasy status

                        // Status ikona
                        var statusText = new TextBlock
                        {
                            Text = maKlasy ? "\u2705" : "\u274C",
                            FontSize = 12, VerticalAlignment = VerticalAlignment.Center
                        };
                        Grid.SetColumn(statusText, 0);
                        rowGrid.Children.Add(statusText);

                        // Odbiorca
                        var odbText = new TextBlock
                        {
                            Text = zam.Odbiorca, FontSize = 11,
                            Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                            VerticalAlignment = VerticalAlignment.Center,
                            TextTrimming = TextTrimming.CharacterEllipsis
                        };
                        Grid.SetColumn(odbText, 1);
                        rowGrid.Children.Add(odbText);

                        // Kg
                        var kgText = new TextBlock
                        {
                            Text = $"{zam.Kg:N0} kg", FontSize = 11, FontWeight = FontWeights.SemiBold,
                            Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                            VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Right
                        };
                        Grid.SetColumn(kgText, 2);
                        rowGrid.Children.Add(kgText);

                        // Status klas
                        var klasText = new TextBlock
                        {
                            Text = maKlasy ? "Przypisane" : "BRAK KLAS",
                            FontSize = 10, FontWeight = maKlasy ? FontWeights.Normal : FontWeights.Bold,
                            Foreground = new SolidColorBrush(maKlasy ? Color.FromRgb(22, 163, 74) : Color.FromRgb(220, 38, 38)),
                            VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Right
                        };
                        Grid.SetColumn(klasText, 3);
                        rowGrid.Children.Add(klasText);

                        rowBorder.Child = rowGrid;
                        mainStack.Children.Add(rowBorder);
                    }
                }

                if (!zamowienia.Any())
                {
                    mainStack.Children.Add(new TextBlock
                    {
                        Text = "Brak zamówień na Kurczak A w tym dniu.",
                        FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                        HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 30, 0, 0)
                    });
                }

                win.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Filtruje zamówienia w dgOrders po określonym produkcie (towarId)
        /// </summary>
        private void FilterOrdersByProduct(int towarId, string nazwa)
        {
            // Znajdź odpowiedni element w ComboBox i wybierz go
            for (int i = 0; i < cbProductFilter.Items.Count; i++)
            {
                if (cbProductFilter.Items[i] is ComboBoxItem item && item.Tag is int tagId && tagId == towarId)
                {
                    cbProductFilter.SelectedIndex = i;
                    return;
                }
            }

            // Jeśli nie znaleziono, ustaw filtr ręcznie
            _selectedProductId = towarId;
            _ = RefreshAllDataAsync();
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

            // 6a. DO SPRZEDANIA (bilans > 0)
            var doSprzStyle = new Style(typeof(TextBlock));
            doSprzStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            doSprzStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(0, 0, 8, 0)));
            doSprzStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0, 150, 0))));
            doSprzStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
            dgAggregation.Columns.Add(new DataGridTextColumn
            {
                Header = "Do spr.",
                Binding = new System.Windows.Data.Binding("DoSprzedania") { StringFormat = "N0" },
                Width = new DataGridLength(70),
                ElementStyle = doSprzStyle
            });

            // 6b. NADMIAR (bilans < 0)
            var nadmiarStyle = new Style(typeof(TextBlock));
            nadmiarStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            nadmiarStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(0, 0, 8, 0)));
            nadmiarStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(200, 0, 0))));
            nadmiarStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
            dgAggregation.Columns.Add(new DataGridTextColumn
            {
                Header = "Nadmiar",
                Binding = new System.Windows.Data.Binding("NadmiarVal") { StringFormat = "N0" },
                Width = new DataGridLength(70),
                ElementStyle = nadmiarStyle
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
            if (!_isInitialized) return;
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
            szczegoly.AppendLine($"{"Produkt",-25} {"Plan",10} {"Fakt",10} {"Zam.",10} {"Do spr.",10} {"Nadmiar",10}");
            szczegoly.AppendLine($"{"───────────────────────",-25} {"──────────",10} {"──────────",10} {"──────────",10} {"──────────",10} {"──────────",10}");

            decimal sumaPlan = 0, sumaFakt = 0, sumaZam = 0;

            foreach (var produktId in produktyIds)
            {
                string nazwaProdukt = _productCatalogCache.TryGetValue(produktId, out var n) ? n : $"ID:{produktId}";

                // Pobierz dane dla tego produktu
                var (plan, fakt, zam) = await PobierzDaneProduktuAsync(produktId, _selectedDate);

                decimal bil = (fakt > 0 ? fakt : plan) - zam;
                string doSprz = bil > 0 ? $"{bil:N0}" : "";
                string nadm = bil < 0 ? $"{Math.Abs(bil):N0}" : "";

                szczegoly.AppendLine($"{nazwaProdukt,-25} {plan,10:N0} {fakt,10:N0} {zam,10:N0} {doSprz,10} {nadm,10}");

                sumaPlan += plan;
                sumaFakt += fakt;
                sumaZam += zam;
            }

            decimal sumaBilans = (sumaFakt > 0 ? sumaFakt : sumaPlan) - sumaZam;
            string sumaDoSprz = sumaBilans > 0 ? $"{sumaBilans:N0}" : "";
            string sumaNadm = sumaBilans < 0 ? $"{Math.Abs(sumaBilans):N0}" : "";

            szczegoly.AppendLine($"{"───────────────────────",-25} {"──────────",10} {"──────────",10} {"──────────",10} {"──────────",10} {"──────────",10}");
            szczegoly.AppendLine($"{"SUMA",-25} {sumaPlan,10:N0} {sumaFakt,10:N0} {sumaZam,10:N0} {sumaDoSprz,10} {sumaNadm,10}");
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

                // Pobierz zamówienia (WSZYSTKIE dnia, bez filtra produktu)
                var orderIds = new List<int>();
                {
                    string dateCol = (_showBySlaughterDate && _slaughterDateColumnExists) ? "DataUboju" : "DataZamowienia";
                    await using var cnIds = new SqlConnection(_connLibra);
                    await cnIds.OpenAsync();
                    var sqlIds = $"SELECT Id FROM [dbo].[ZamowieniaMieso] WHERE {dateCol} = @Day AND Status <> 'Anulowane'";
                    await using var cmdIds = new SqlCommand(sqlIds, cnIds);
                    cmdIds.Parameters.AddWithValue("@Day", dzien.Date);
                    await using var rdIds = await cmdIds.ExecuteReaderAsync();
                    while (await rdIds.ReadAsync()) orderIds.Add(rdIds.GetInt32(0));
                }

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
            // Filtruj zamówienia - sprawdź czy kolumny istnieją
            if (_dtOrders.Columns.Count > 0 && _dtOrders.Columns.Contains("Status"))
            {
                var conditions = new List<string>();

                if (!_showReleasesWithoutOrders)
                    conditions.Add("Status <> 'Wydanie bez zamówienia'");

                if (!_showAnulowane)
                    conditions.Add("Status <> 'Anulowane'");

                _dtOrders.DefaultView.RowFilter = string.Join(" AND ", conditions);
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

        // Cache flag — czy kolumny SourceZamowienieId / CyklGroupId istnieją (sprawdzane raz)
        private static bool? _parentTrackingColumnsChecked = null;
        private static bool _parentTrackingColumnsExist = false;
        private static bool? _dataProdukcjiColumnChecked = null;
        private static bool _dataProdukcjiColumnExists = false;

        /// <summary>
        /// Duplikuje zamówienie na docelowy dzień. Rozszerzona wersja z obsługą
        /// DataProdukcji, klas wagowych i śledzenia parent → child.
        /// </summary>
        /// <param name="sourceId">ID zamówienia źródłowego</param>
        /// <param name="targetDate">Docelowa data zamówienia</param>
        /// <param name="copyNotes">Czy kopiować pole Uwagi</param>
        /// <param name="copyKlasyWagowe">Czy kopiować RezerwacjeKlasWagowych</param>
        /// <param name="copyDataProdukcji">Czy kopiować DataProdukcji (z offsetem)</param>
        /// <param name="cyklGroupId">Opcjonalny GUID grupy cyklu</param>
        /// <returns>ID nowo utworzonego zamówienia</returns>
        private async Task<int> DuplicateOrderAsync(int sourceId, DateTime targetDate,
            bool copyNotes = false, bool copyKlasyWagowe = true,
            bool copyDataProdukcji = true, Guid? cyklGroupId = null,
            TimeSpan? godzinaOverride = null)
        {
            targetDate = ValidateSqlDate(targetDate);

            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();

            // Jednorazowo sprawdź istnienie opcjonalnych kolumn — i dodaj je jeśli ich brak
            if (_parentTrackingColumnsChecked == null)
            {
                try
                {
                    var checkSql = @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                                     WHERE TABLE_NAME='ZamowieniaMieso' AND COLUMN_NAME IN ('SourceZamowienieId','CyklGroupId')";
                    await using var cmdCheck = new SqlCommand(checkSql, cn);
                    int colCount = Convert.ToInt32(await cmdCheck.ExecuteScalarAsync());

                    if (colCount < 2)
                    {
                        // Dodaj brakujące kolumny
                        try
                        {
                            var alterSql = @"
                                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='ZamowieniaMieso' AND COLUMN_NAME='SourceZamowienieId')
                                    ALTER TABLE [dbo].[ZamowieniaMieso] ADD SourceZamowienieId INT NULL;
                                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='ZamowieniaMieso' AND COLUMN_NAME='CyklGroupId')
                                    ALTER TABLE [dbo].[ZamowieniaMieso] ADD CyklGroupId UNIQUEIDENTIFIER NULL;";
                            await using var cmdAlter = new SqlCommand(alterSql, cn) { CommandTimeout = 30 };
                            await cmdAlter.ExecuteNonQueryAsync();
                            _parentTrackingColumnsExist = true;
                        }
                        catch { _parentTrackingColumnsExist = false; }
                    }
                    else
                    {
                        _parentTrackingColumnsExist = true;
                    }
                }
                catch { _parentTrackingColumnsExist = false; }
                _parentTrackingColumnsChecked = true;
            }
            if (_dataProdukcjiColumnChecked == null)
            {
                try
                {
                    var checkSql = @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                                     WHERE TABLE_NAME='ZamowieniaMieso' AND COLUMN_NAME='DataProdukcji'";
                    await using var cmdCheck = new SqlCommand(checkSql, cn);
                    _dataProdukcjiColumnExists = Convert.ToInt32(await cmdCheck.ExecuteScalarAsync()) > 0;
                }
                catch { _dataProdukcjiColumnExists = false; }
                _dataProdukcjiColumnChecked = true;
            }

            await using var tr = cn.BeginTransaction();

            try
            {
                int clientId = 0;
                string notes = "";
                DateTime arrivalTime = DateTime.Today.AddHours(8);
                DateTime sourceDataZamowienia = DateTime.Today;
                DateTime? sourceDataProdukcji = null;
                DateTime? sourceDataUboju = null;
                string sourceWaluta = "PLN";
                int containers = 0;
                decimal pallets = 0m;
                bool modeE2 = false;

                // Pobierz dane źródłowe (z DataProdukcji, DataUboju, Waluta jeśli kolumny istnieją)
                var selectCols = "KlientId, Uwagi, DataPrzyjazdu, LiczbaPojemnikow, LiczbaPalet, TrybE2, DataZamowienia";
                if (_dataProdukcjiColumnExists) selectCols += ", DataProdukcji";
                if (_slaughterDateColumnExists) selectCols += ", DataUboju";
                if (_walutaColumnExists) selectCols += ", Waluta";
                string selectSql = $"SELECT {selectCols} FROM ZamowieniaMieso WHERE Id = @Id";

                using (var cmd = new SqlCommand(selectSql, cn, tr))
                {
                    cmd.Parameters.AddWithValue("@Id", sourceId);
                    using var reader = await cmd.ExecuteReaderAsync();

                    if (await reader.ReadAsync())
                    {
                        clientId = reader.GetInt32(0);
                        notes = reader.IsDBNull(1) ? "" : reader.GetString(1);
                        arrivalTime = reader.GetDateTime(2);
                        arrivalTime = godzinaOverride.HasValue
                            ? targetDate.Date.Add(godzinaOverride.Value)
                            : targetDate.Date.Add(arrivalTime.TimeOfDay);
                        containers = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                        pallets = reader.IsDBNull(4) ? 0m : reader.GetDecimal(4);
                        modeE2 = reader.IsDBNull(5) ? false : reader.GetBoolean(5);
                        sourceDataZamowienia = reader.GetDateTime(6);

                        if (_dataProdukcjiColumnExists)
                        {
                            int ordProd = reader.GetOrdinal("DataProdukcji");
                            if (!reader.IsDBNull(ordProd))
                                sourceDataProdukcji = reader.GetDateTime(ordProd);
                        }
                        if (_slaughterDateColumnExists)
                        {
                            int ordUboj = reader.GetOrdinal("DataUboju");
                            if (!reader.IsDBNull(ordUboj))
                                sourceDataUboju = reader.GetDateTime(ordUboj);
                        }
                        if (_walutaColumnExists)
                        {
                            int ordWal = reader.GetOrdinal("Waluta");
                            if (!reader.IsDBNull(ordWal))
                                sourceWaluta = reader.GetString(ordWal);
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException($"Zamówienie źródłowe {sourceId} nie istnieje");
                    }
                }

                // Oblicz DataProdukcji i DataUboju — przesunięte o tę samą różnicę dni
                int deltaDni = (int)(targetDate.Date - sourceDataZamowienia.Date).TotalDays;

                DateTime? newDataProdukcji = null;
                if (copyDataProdukcji && sourceDataProdukcji.HasValue)
                    newDataProdukcji = sourceDataProdukcji.Value.AddDays(deltaDni);

                DateTime? newDataUboju = null;
                if (sourceDataUboju.HasValue)
                    newDataUboju = sourceDataUboju.Value.AddDays(deltaDni);
                else
                    newDataUboju = targetDate.Date; // fallback: DataUboju = DataZamowienia

                var cmdGetId = new SqlCommand("SELECT ISNULL(MAX(Id),0)+1 FROM ZamowieniaMieso", cn, tr);
                int newId = Convert.ToInt32(await cmdGetId.ExecuteScalarAsync());

                // Zbuduj dynamicznie listę kolumn i wartości w zależności od istniejących kolumn
                var cols = new List<string> { "Id", "DataZamowienia", "DataPrzyjazdu", "KlientId", "Uwagi",
                    "IdUser", "DataUtworzenia", "LiczbaPojemnikow", "LiczbaPalet", "TrybE2", "TransportStatus" };
                var vals = new List<string> { "@id", "@dz", "@dp", "@kid", "@uw",
                    "@u", "GETDATE()", "@poj", "@pal", "@e2", "'Oczekuje'" };

                if (_dataProdukcjiColumnExists)
                {
                    cols.Add("DataProdukcji");
                    vals.Add("@dataProd");
                }
                if (_slaughterDateColumnExists)
                {
                    cols.Add("DataUboju");
                    vals.Add("@dataUboj");
                }
                if (_walutaColumnExists)
                {
                    cols.Add("Waluta");
                    vals.Add("@waluta");
                }
                if (_parentTrackingColumnsExist)
                {
                    cols.Add("SourceZamowienieId");
                    vals.Add("@srcId");
                    cols.Add("CyklGroupId");
                    vals.Add("@cyklGuid");
                }

                var insertSql = $"INSERT INTO ZamowieniaMieso ({string.Join(",", cols)}) VALUES ({string.Join(",", vals)})";
                var cmdInsert = new SqlCommand(insertSql, cn, tr);
                cmdInsert.Parameters.AddWithValue("@id", newId);
                cmdInsert.Parameters.AddWithValue("@dz", targetDate.Date);
                cmdInsert.Parameters.AddWithValue("@dp", arrivalTime);
                cmdInsert.Parameters.AddWithValue("@kid", clientId);

                string finalNotes = copyNotes && !string.IsNullOrEmpty(notes) ? notes : "";
                cmdInsert.Parameters.AddWithValue("@uw", string.IsNullOrEmpty(finalNotes) ? DBNull.Value : (object)finalNotes);

                cmdInsert.Parameters.AddWithValue("@u", UserID);
                cmdInsert.Parameters.AddWithValue("@poj", containers);
                cmdInsert.Parameters.AddWithValue("@pal", pallets);
                cmdInsert.Parameters.AddWithValue("@e2", modeE2);

                if (_dataProdukcjiColumnExists)
                    cmdInsert.Parameters.AddWithValue("@dataProd", newDataProdukcji.HasValue ? (object)newDataProdukcji.Value : DBNull.Value);
                if (_slaughterDateColumnExists)
                    cmdInsert.Parameters.AddWithValue("@dataUboj", newDataUboju.HasValue ? (object)newDataUboju.Value : DBNull.Value);
                if (_walutaColumnExists)
                    cmdInsert.Parameters.AddWithValue("@waluta", sourceWaluta);
                if (_parentTrackingColumnsExist)
                {
                    cmdInsert.Parameters.AddWithValue("@srcId", sourceId);
                    cmdInsert.Parameters.AddWithValue("@cyklGuid", cyklGroupId.HasValue ? (object)cyklGroupId.Value : DBNull.Value);
                }

                await cmdInsert.ExecuteNonQueryAsync();

                // Kopiuj produkty
                var cmdCopyItems = new SqlCommand(
                    _strefaColumnExists
                    ? @"INSERT INTO ZamowieniaMiesoTowar
            (ZamowienieId, KodTowaru, Ilosc, Cena, Pojemniki, Palety, E2, Folia, Hallal, Strefa)
            SELECT @newId, KodTowaru, Ilosc, Cena, Pojemniki, Palety, E2, Folia, Hallal, ISNULL(Strefa, 0)
            FROM ZamowieniaMiesoTowar WHERE ZamowienieId = @sourceId"
                    : @"INSERT INTO ZamowieniaMiesoTowar
            (ZamowienieId, KodTowaru, Ilosc, Cena, Pojemniki, Palety, E2, Folia, Hallal)
            SELECT @newId, KodTowaru, Ilosc, Cena, Pojemniki, Palety, E2, Folia, Hallal
            FROM ZamowieniaMiesoTowar WHERE ZamowienieId = @sourceId", cn, tr);
                cmdCopyItems.Parameters.AddWithValue("@newId", newId);
                cmdCopyItems.Parameters.AddWithValue("@sourceId", sourceId);
                await cmdCopyItems.ExecuteNonQueryAsync();

                // Kopiuj rezerwacje klas wagowych (RezerwacjeKlasWagowych) dla Kurczaka A
                if (copyKlasyWagowe)
                {
                    try
                    {
                        var checkRezSql = "SELECT OBJECT_ID('dbo.RezerwacjeKlasWagowych', 'U')";
                        using var cmdCheckRez = new SqlCommand(checkRezSql, cn, tr);
                        var rezObj = await cmdCheckRez.ExecuteScalarAsync();
                        if (rezObj != null && rezObj != DBNull.Value)
                        {
                            // Data produkcji dla rezerwacji = DataProdukcji nowego (lub DataZamowienia jeśli brak)
                            DateTime dataProdRez = newDataProdukcji ?? targetDate.Date;
                            var copyRezSql = @"INSERT INTO RezerwacjeKlasWagowych
                                (ZamowienieId, DataProdukcji, Klasa, IloscPojemnikow, Handlowiec, Odbiorca, DataRezerwacji, Status)
                                SELECT @newId, @newData, Klasa, IloscPojemnikow, Handlowiec, Odbiorca, GETDATE(), 'Aktywna'
                                FROM RezerwacjeKlasWagowych
                                WHERE ZamowienieId = @sourceId AND Status = 'Aktywna'";
                            using var cmdCopyRez = new SqlCommand(copyRezSql, cn, tr);
                            cmdCopyRez.Parameters.AddWithValue("@newId", newId);
                            cmdCopyRez.Parameters.AddWithValue("@newData", dataProdRez);
                            cmdCopyRez.Parameters.AddWithValue("@sourceId", sourceId);
                            await cmdCopyRez.ExecuteNonQueryAsync();
                        }
                    }
                    catch { /* nie blokuj całej operacji jeśli klasy wagowe padną */ }
                }

                await tr.CommitAsync();
                return newId;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DuplicateOrderAsync failed (src={sourceId}, target={targetDate:yyyy-MM-dd}): {ex}");
                try { await tr.RollbackAsync(); } catch { }
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

        // Cena w tabeli zamówień: wartość > 0 → "24,50 zł/kg"; brak ceny → "- zł/kg"
        public class CenaZlKgConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                decimal d = value is decimal dd ? dd : 0m;
                return d > 0 ? d.ToString("N2", CultureInfo.CurrentCulture) + " zł/kg" : "- zł/kg";
            }
            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
                => System.Windows.Data.Binding.DoNothing;
        }

        // Termin z ikoną zależną od transportu: 🏭 awizacja na zakładzie / 🚗 transport własny klienta
        public class TerminIkonaConverter : System.Windows.Data.IMultiValueConverter
        {
            public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
            {
                string s = values != null && values.Length > 0 ? values[0]?.ToString() ?? "" : "";
                if (string.IsNullOrWhiteSpace(s)) return "";
                bool wlasny = values.Length > 1 && values[1] is bool b && b;
                return (wlasny ? "\U0001F697 " : "\U0001F3ED ") + s;
            }
            public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
                => throw new NotSupportedException();
        }

        // Hex string ("#C0392B") → SolidColorBrush, defensywnie (panel ostatnich zmian kg)
        public class HexToBrushSafeConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                string hex = value as string;
                if (string.IsNullOrWhiteSpace(hex)) return Brushes.Black;
                try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
                catch { return Brushes.Black; }
            }
            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
                => System.Windows.Data.Binding.DoNothing;
        }

        // Sama liczba ceny (jednostka "zł/kg" jest osobnym wierszem pod spodem)
        public class CenaLiczbaConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                decimal d = value is decimal dd ? dd : 0m;
                return d > 0 ? d.ToString("N2", CultureInfo.CurrentCulture) : "-";
            }
            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
                => System.Windows.Data.Binding.DoNothing;
        }

        // Termin (awizacja) w tabeli zamówień: ikonka ZAKŁADU przed godziną/dniem —
        // przypomina, że to godzina i data awizacji NA ZAKŁADZIE (puste zostaje puste)
        public class TerminAutoConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                string s = value?.ToString() ?? "";
                return string.IsNullOrWhiteSpace(s) ? "" : "🏭 " + s;
            }
            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
                => System.Windows.Data.Binding.DoNothing;
        }

        // Rzeczywiste wydanie: ikonka AUTA przed godziną/dniem wyjazdu (puste zostaje puste)
        public class WydanieAutoConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                string s = value?.ToString() ?? "";
                return string.IsNullOrWhiteSpace(s) ? "" : "🚚 " + s;
            }
            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
                => System.Windows.Data.Binding.DoNothing;
        }

        // Konwerter dla edytowalnych kolumn z jednostką: wyświetla "1 200 kg" / "24,50 zł/kg",
        // a przy zapisie zdejmuje sufiks i parsuje liczbę (kultura PL)
        public class JednostkaEditConverter : IValueConverter
        {
            private readonly string _suffix;
            private readonly string _format;
            public JednostkaEditConverter(string suffix, string format) { _suffix = suffix; _format = format; }

            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (value is decimal d) return d.ToString(_format, CultureInfo.CurrentCulture) + " " + _suffix;
                if (value is double db) return db.ToString(_format, CultureInfo.CurrentCulture) + " " + _suffix;
                return value?.ToString() ?? "";
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                string s = (value?.ToString() ?? "")
                    .Replace(_suffix, "", StringComparison.OrdinalIgnoreCase)
                    .Replace("zł/kg", "", StringComparison.OrdinalIgnoreCase)
                    .Replace("kg", "", StringComparison.OrdinalIgnoreCase)
                    .Replace(" ", " ")
                    .Trim();
                return decimal.TryParse(s, NumberStyles.Any, CultureInfo.CurrentCulture, out var d)
                    ? d
                    : System.Windows.Data.Binding.DoNothing;
            }
        }

        public class UtworzoneInitialsConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (value == null) return "?";
                string text = value.ToString();
                // Extract initials from text like "Sty 12 (Ania)" - get first letter of name in parentheses
                int parenStart = text.IndexOf('(');
                int parenEnd = text.IndexOf(')');
                if (parenStart >= 0 && parenEnd > parenStart)
                {
                    string name = text.Substring(parenStart + 1, parenEnd - parenStart - 1);
                    return name.Length >= 1 ? name.Substring(0, 1).ToUpper() : "?";
                }
                return text.Length >= 1 ? text.Substring(0, 1).ToUpper() : "?";
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }
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

            // Pobierz zamówienia (WSZYSTKIE dnia, bez filtra produktu)
            var orderSum = new Dictionary<int, decimal>();
            var orderIds = new List<int>();
            {
                string dateCol = (_showBySlaughterDate && _slaughterDateColumnExists) ? "DataUboju" : "DataZamowienia";
                await using var cnIds = new SqlConnection(_connLibra);
                await cnIds.OpenAsync();
                var sqlIds = $"SELECT Id FROM [dbo].[ZamowieniaMieso] WHERE {dateCol} = @Day AND Status <> 'Anulowane'";
                await using var cmdIds = new SqlCommand(sqlIds, cnIds);
                cmdIds.Parameters.AddWithValue("@Day", day.Date);
                await using var rdIds = await cmdIds.ExecuteReaderAsync();
                while (await rdIds.ReadAsync()) orderIds.Add(rdIds.GetInt32(0));
            }

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
                _ when _editColumnName.Contains("Folia") => row.Field<bool>("Folia"),
                _ when _editColumnName.Contains("Halal") => row.Field<bool>("Hallal"),
                _ when _editColumnName.Contains("Strefa") => row.Field<bool>("Strefa"),
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
                // Wartości wyświetlane mają sufiksy jednostek ("kg", "zł/kg") — zdejmij przed parsowaniem
                string newText = textBox.Text
                    .Replace("zł/kg", "", StringComparison.OrdinalIgnoreCase)
                    .Replace("kg", "", StringComparison.OrdinalIgnoreCase)
                    .Replace(" ", " ")
                    .Trim();

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
                    _ when columnName.Contains("Folia") => "Folia",
                    _ when columnName.Contains("Halal") => "Hallal",
                    _ when columnName.Contains("Strefa") => "Strefa",
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
                _ when columnName.Contains("Folia") => (bool)value ? "TAK" : "NIE",
                _ when columnName.Contains("Halal") => (bool)value ? "TAK" : "NIE",
                _ when columnName.Contains("Strefa") => (bool)value ? "TAK" : "NIE",
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
