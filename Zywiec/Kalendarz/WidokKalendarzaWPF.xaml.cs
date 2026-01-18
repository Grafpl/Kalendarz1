using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Kalendarz1.Zywiec.Kalendarz.Services;

namespace Kalendarz1.Zywiec.Kalendarz
{
    /// <summary>
    /// Widok Kalendarza WPF - Kompletna wersja
    /// </summary>
    public partial class WidokKalendarzaWPF : Window
    {
        #region Pola prywatne

        // Connection string z optymalizacją puli połączeń
        private static readonly string ConnectionString =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True;" +
            "Min Pool Size=5;Max Pool Size=100;Connection Timeout=30;Command Timeout=30";

        private ObservableCollection<DostawaModel> _dostawy = new ObservableCollection<DostawaModel>();
        private ObservableCollection<DostawaModel> _dostawyNastepnyTydzien = new ObservableCollection<DostawaModel>();
        private ObservableCollection<PartiaModel> _partie = new ObservableCollection<PartiaModel>();
        private ObservableCollection<DostawaModel> _wstawienia = new ObservableCollection<DostawaModel>();
        private ObservableCollection<NotatkaModel> _notatki = new ObservableCollection<NotatkaModel>();
        private ObservableCollection<NotatkaModel> _ostatnieNotatki = new ObservableCollection<NotatkaModel>();
        private ObservableCollection<RankingModel> _ranking = new ObservableCollection<RankingModel>();

        private DateTime _selectedDate = DateTime.Today;
        private string _selectedLP = null;
        private DispatcherTimer _refreshTimer;
        private DispatcherTimer _priceTimer;
        private DispatcherTimer _surveyTimer;
        private DispatcherTimer _countdownTimer;

        // Cache dla hodowców - optymalizacja wydajności
        private static List<string> _hodowcyCache = null;
        private static DateTime _hodowcyCacheExpiry = DateTime.MinValue;
        private static readonly TimeSpan CACHE_DURATION = TimeSpan.FromMinutes(30);

        // Wyszukiwanie
        private string _searchText = "";
        private List<DostawaModel> _allDostawy = new List<DostawaModel>();
        private List<DostawaModel> _allDostawyNastepny = new List<DostawaModel>();

        // Auto-refresh countdown
        private int _refreshCountdown = 600; // 10 minut w sekundach
        private const int REFRESH_INTERVAL_SECONDS = 600;

        // Drag & Drop
        private Point _dragStartPoint;
        private DostawaModel _draggedItem;
        private bool _isDragging = false;

        // Multi-select
        private HashSet<string> _selectedLPs = new HashSet<string>();

        // Toast notifications
        private Queue<ToastMessage> _toastQueue = new Queue<ToastMessage>();
        private bool _isShowingToast = false;

        // Dummy fields for removed status bar controls (null check handles them)
        private TextBlock txtStatusBar = null;
        private TextBox txtQuickNote = null;

        // Ankieta
        private bool _surveyShownThisSession = false;
        private static readonly TimeSpan SURVEY_START = new TimeSpan(14, 30, 0);
        private static readonly TimeSpan SURVEY_END = new TimeSpan(15, 0, 0);

        // Paleciak włączony/wyłączony
        private bool _paleciakEnabled = true;

        // Cancellation token dla async operacji
        private CancellationTokenSource _cts = new CancellationTokenSource();

        // Serwis audytu zmian
        private AuditLogService _auditService;

        // ═══════════════════════════════════════════════════════════════
        // TRYB SYMULACJI - testowanie przesunięć bez zapisu do bazy
        // ═══════════════════════════════════════════════════════════════
        private bool _isSimulationMode = false;
        private List<DostawaModel> _simulationBackup = new List<DostawaModel>();
        private List<DostawaModel> _simulationBackupNastepny = new List<DostawaModel>();

        // ═══════════════════════════════════════════════════════════════
        // MINI-MAPA TYGODNI - szybka nawigacja
        // ═══════════════════════════════════════════════════════════════
        private int _weekMapOffset = 0; // Przesunięcie widoku mini-mapy (w tygodniach)
        private const int WEEK_MAP_VISIBLE_COUNT = 9; // Liczba widocznych tygodni

        #endregion

        #region Właściwości publiczne

        public string UserID { get; set; }
        public string UserName { get; set; }

        #endregion

        #region Konstruktor

        public WidokKalendarzaWPF()
        {
            InitializeComponent();

            dgDostawy.ItemsSource = _dostawy;
            dgDostawyNastepny.ItemsSource = _dostawyNastepnyTydzien;
            dgPartie.ItemsSource = _partie;
            dgWstawienia.ItemsSource = _wstawienia;
            dgNotatki.ItemsSource = _notatki;
            dgOstatnieNotatki.ItemsSource = _ostatnieNotatki;
            dgRanking.ItemsSource = _ranking;

            SetupComboBoxes();
            SetupTimers();
            SetupKeyboardShortcuts();
            SetupDragDrop();
            SetupContextMenu();
        }

        #endregion

        #region Inicjalizacja

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Ustaw użytkownika
            if (!string.IsNullOrEmpty(UserName))
                txtUserName.Text = UserName;
            else if (!string.IsNullOrEmpty(UserID))
                txtUserName.Text = await GetUserNameByIdAsync(UserID);

            // Inicjalizuj serwis audytu
            _auditService = new AuditLogService(ConnectionString, UserID, UserName ?? txtUserName.Text);

            // Ustaw kalendarz na dziś
            calendarMain.SelectedDate = DateTime.Today;
            _selectedDate = DateTime.Today;
            UpdateWeekNumber();

            // Ustaw widoczność następnego tygodnia (checkbox jest domyślnie zaznaczony)
            if (chkNastepnyTydzien?.IsChecked == true)
            {
                if (colNastepnyTydzien != null) colNastepnyTydzien.Width = new GridLength(1, GridUnitType.Star);
                if (borderNastepnyTydzien != null) borderNastepnyTydzien.Visibility = Visibility.Visible;
            }

            // Załaduj dane asynchronicznie
            await LoadAllDataAsync();

            // Sprawdź ankietę
            TryShowSurveyIfInWindow();

            // Wygeneruj mini-mapę tygodni
            GenerateWeekMap();

            // Pokaż powitanie
            ShowToast("Kalendarz załadowany", ToastType.Success);
        }

        private void SetupComboBoxes()
        {
            // Status
            cmbStatus.Items.Add("Potwierdzony");
            cmbStatus.Items.Add("Do wykupienia");
            cmbStatus.Items.Add("Anulowany");
            cmbStatus.Items.Add("Sprzedany");
            cmbStatus.Items.Add("B.Wolny.");
            cmbStatus.Items.Add("B.Kontr.");

            // Typ umowy
            cmbTypUmowy.Items.Add("Wolnyrynek");
            cmbTypUmowy.Items.Add("Kontrakt");
            cmbTypUmowy.Items.Add("W.Wolnyrynek");

            // Typ ceny
            cmbTypCeny.Items.Add("wolnyrynek");
            cmbTypCeny.Items.Add("rolnicza");
            cmbTypCeny.Items.Add("łączona");
            cmbTypCeny.Items.Add("ministerialna");

            // Osobowość
            var osobowosci = new[] { "Analityk", "Na Cel", "Wpływowy", "Relacyjny" };
            foreach (var o in osobowosci)
            {
                cmbOsobowosc1.Items.Add(o);
                cmbOsobowosc2.Items.Add(o);
            }

            // Załaduj hodowców
            LoadHodowcyToComboBox();
        }

        private void SetupTimers()
        {
            // Timer odświeżania danych co 10 minut
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(10) };
            _refreshTimer.Tick += async (s, e) => await LoadDostawyAsync();
            _refreshTimer.Start();

            // Timer odświeżania cen co 30 minut
            _priceTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
            _priceTimer.Tick += async (s, e) => { await LoadCenyAsync(); await LoadPartieAsync(); };
            _priceTimer.Start();

            // Timer ankiety
            _surveyTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            _surveyTimer.Tick += (s, e) => TryShowSurveyIfInWindow();
            _surveyTimer.Start();

            // Timer odliczania do odświeżenia (co 1 sekundę)
            _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _countdownTimer.Tick += (s, e) => UpdateRefreshCountdown();
            _countdownTimer.Start();
            _refreshCountdown = REFRESH_INTERVAL_SECONDS;
        }

        private void UpdateRefreshCountdown()
        {
            _refreshCountdown--;
            if (_refreshCountdown <= 0)
            {
                _refreshCountdown = REFRESH_INTERVAL_SECONDS;
            }

            // Aktualizuj wskaźnik odświeżania
            if (txtRefreshCountdown != null)
            {
                int minutes = _refreshCountdown / 60;
                int seconds = _refreshCountdown % 60;
                txtRefreshCountdown.Text = $"{minutes}:{seconds:D2}";
            }

            // Aktualizuj bieżącą datę, czas i tydzień w nagłówku kalendarza
            UpdateCurrentDateTimeDisplay();
        }

        private void UpdateCurrentDateTimeDisplay()
        {
            var now = DateTime.Now;
            var culture = new System.Globalization.CultureInfo("pl-PL");

            // Format: "18.01 sob 12:30"
            if (txtCurrentDateTime != null)
            {
                string dayOfWeek = culture.DateTimeFormat.GetAbbreviatedDayName(now.DayOfWeek).ToLower();
                txtCurrentDateTime.Text = $"{now:dd.MM} {dayOfWeek} {now:HH:mm}";
            }

            // Numer tygodnia
            if (txtCurrentWeek != null)
            {
                var cal = culture.Calendar;
                int weekNum = cal.GetWeekOfYear(now, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
                txtCurrentWeek.Text = weekNum.ToString();
            }
        }

        private void SetupKeyboardShortcuts()
        {
            // Rejestruj skróty klawiszowe
            this.KeyDown += Window_KeyDown;

            // Dodatkowe InputBindings dla popularnych skrótów
            this.InputBindings.Add(new KeyBinding(
                new RelayCommand(o => DuplicateSelectedDelivery()),
                new KeyGesture(Key.D, ModifierKeys.Control)));

            this.InputBindings.Add(new KeyBinding(
                new RelayCommand(o => CreateNewDelivery()),
                new KeyGesture(Key.N, ModifierKeys.Control)));

            this.InputBindings.Add(new KeyBinding(
                new RelayCommand(o => DeleteSelectedDelivery()),
                new KeyGesture(Key.Delete)));

            this.InputBindings.Add(new KeyBinding(
                new RelayCommand(o => ChangeSelectedDeliveryDate(1)),
                new KeyGesture(Key.Add)));

            this.InputBindings.Add(new KeyBinding(
                new RelayCommand(o => ChangeSelectedDeliveryDate(-1)),
                new KeyGesture(Key.Subtract)));

            this.InputBindings.Add(new KeyBinding(
                new RelayCommand(o => ChangeSelectedDeliveryDate(1)),
                new KeyGesture(Key.OemPlus)));

            this.InputBindings.Add(new KeyBinding(
                new RelayCommand(o => ChangeSelectedDeliveryDate(-1)),
                new KeyGesture(Key.OemMinus)));
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Obsługa Ctrl+S - zapisz
            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
            {
                BtnZapiszDostawe_Click(sender, new RoutedEventArgs());
                e.Handled = true;
            }
            // Obsługa Ctrl+R - odśwież
            else if (e.Key == Key.R && Keyboard.Modifiers == ModifierKeys.Control)
            {
                _ = LoadAllDataAsync();
                e.Handled = true;
            }
            // Obsługa F5 - odśwież
            else if (e.Key == Key.F5)
            {
                _ = LoadAllDataAsync();
                e.Handled = true;
            }
            // Obsługa Escape - anuluj zaznaczenie
            else if (e.Key == Key.Escape)
            {
                dgDostawy.SelectedItem = null;
                _selectedLP = null;
                _selectedLPs.Clear();
                e.Handled = true;
            }
            // Obsługa Ctrl+A - zaznacz wszystko
            else if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
            {
                SelectAllDeliveries();
                e.Handled = true;
            }
        }

        private void SetupDragDrop()
        {
            // Drag & Drop dla DataGrid
            dgDostawy.PreviewMouseLeftButtonDown += DgDostawy_PreviewMouseLeftButtonDown;
            dgDostawy.PreviewMouseMove += DgDostawy_PreviewMouseMove;
            dgDostawy.Drop += DgDostawy_Drop;
            dgDostawy.DragEnter += DgDostawy_DragEnter;
            dgDostawy.DragLeave += DgDostawy_DragLeave;
            dgDostawy.DragOver += DgDostawy_DragOver;
            dgDostawy.AllowDrop = true;

            dgDostawyNastepny.PreviewMouseLeftButtonDown += DgDostawy_PreviewMouseLeftButtonDown;
            dgDostawyNastepny.PreviewMouseMove += DgDostawy_PreviewMouseMove;
            dgDostawyNastepny.Drop += DgDostawy_Drop;
            dgDostawyNastepny.DragEnter += DgDostawy_DragEnter;
            dgDostawyNastepny.DragLeave += DgDostawy_DragLeave;
            dgDostawyNastepny.DragOver += DgDostawy_DragOver;
            dgDostawyNastepny.AllowDrop = true;
        }

        private void SetupContextMenu()
        {
            // Context menu dla głównej tabeli dostaw
            var contextMenu = new ContextMenu();

            var menuDuplikuj = new MenuItem { Header = "Zduplikuj (Ctrl+D)", Icon = new TextBlock { Text = "📋" } };
            menuDuplikuj.Click += (s, e) => DuplicateSelectedDelivery();

            var menuNowa = new MenuItem { Header = "Nowa dostawa (Ctrl+N)", Icon = new TextBlock { Text = "➕" } };
            menuNowa.Click += (s, e) => CreateNewDelivery();

            var menuUsun = new MenuItem { Header = "Usuń (Delete)", Icon = new TextBlock { Text = "🗑" } };
            menuUsun.Click += (s, e) => DeleteSelectedDelivery();

            contextMenu.Items.Add(menuDuplikuj);
            contextMenu.Items.Add(menuNowa);
            contextMenu.Items.Add(new Separator());

            var menuDateUp = new MenuItem { Header = "Przesuń +1 dzień (+)", Icon = new TextBlock { Text = "▲" } };
            menuDateUp.Click += (s, e) => ChangeSelectedDeliveryDate(1);

            var menuDateDown = new MenuItem { Header = "Przesuń -1 dzień (-)", Icon = new TextBlock { Text = "▼" } };
            menuDateDown.Click += (s, e) => ChangeSelectedDeliveryDate(-1);

            contextMenu.Items.Add(menuDateUp);
            contextMenu.Items.Add(menuDateDown);
            contextMenu.Items.Add(new Separator());

            var menuPotwierdz = new MenuItem { Header = "Potwierdź zaznaczone", Icon = new TextBlock { Text = "✓" } };
            menuPotwierdz.Click += async (s, e) => await BulkConfirmAsync(true);

            var menuAnuluj = new MenuItem { Header = "Anuluj zaznaczone", Icon = new TextBlock { Text = "✗" } };
            menuAnuluj.Click += async (s, e) => await BulkCancelAsync();

            contextMenu.Items.Add(menuPotwierdz);
            contextMenu.Items.Add(menuAnuluj);
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(menuUsun);

            dgDostawy.ContextMenu = contextMenu;
            dgDostawyNastepny.ContextMenu = contextMenu;
        }

        private async Task LoadAllDataAsync()
        {
            try
            {
                // Równoległe ładowanie niezależnych danych
                var tasks = new List<Task>
                {
                    LoadDostawyAsync(),
                    LoadCenyAsync(),
                    LoadPartieAsync(),
                    LoadOstatnieNotatkiAsync(),
                    LoadRankingAsync()
                };

                await Task.WhenAll(tasks);
                _refreshCountdown = REFRESH_INTERVAL_SECONDS;
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd ładowania: {ex.Message}", ToastType.Error);
            }
        }

        // Stara synchroniczna wersja dla kompatybilności
        private void LoadAllData()
        {
            _ = LoadAllDataAsync();
        }

        #endregion

        #region Ładowanie danych - Dostawy

        private async Task LoadDostawyAsync()
        {
            if (!IsLoaded) return; // Nie ładuj przed pełną inicjalizacją

            await LoadDostawyForWeekAsync(_dostawy, _selectedDate);

            if (chkNastepnyTydzien?.IsChecked == true)
            {
                await LoadDostawyForWeekAsync(_dostawyNastepnyTydzien, _selectedDate.AddDays(7));
            }

            // Aktualizuj status bar
            UpdateStatusBar();
        }

        // Stara synchroniczna wersja dla kompatybilności
        private void LoadDostawy()
        {
            _ = LoadDostawyAsync();
        }

        private async Task LoadDostawyForWeekAsync(ObservableCollection<DostawaModel> collection, DateTime baseDate)
        {
            try
            {
                DateTime startOfWeek = baseDate.AddDays(-(int)baseDate.DayOfWeek);
                if (baseDate.DayOfWeek == DayOfWeek.Sunday) startOfWeek = baseDate.AddDays(-6);
                else startOfWeek = baseDate.AddDays(-(int)baseDate.DayOfWeek + 1);

                DateTime endOfWeek = startOfWeek.AddDays(7);

                // Ustaw nagłówek kolumny z numerem tygodnia - na głównym wątku
                int weekNum = GetIso8601WeekOfYear(baseDate);
                string headerText = $"tyg.{weekNum} ({startOfWeek:dd.MM}-{endOfWeek.AddDays(-1):dd.MM})";
                await Dispatcher.InvokeAsync(() =>
                {
                    // Aktualizuj nagłówek kolumny DataGrida
                    if (collection == _dostawy && colDostawcaHeader != null)
                    {
                        colDostawcaHeader.Header = headerText;
                    }
                    else if (collection == _dostawyNastepnyTydzien && colDostawcaHeader2 != null)
                    {
                        colDostawcaHeader2.Header = headerText;
                    }
                });

                string sql = BuildDostawyQuery();

                // Tymczasowa lista na dane z bazy
                var tempList = new List<DostawaModel>();

                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@startDate", startOfWeek);
                        cmd.Parameters.AddWithValue("@endDate", endOfWeek);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync(_cts.Token))
                        {
                            while (await reader.ReadAsync(_cts.Token))
                            {
                                DateTime dataOdbioru = reader.GetDateTime(reader.GetOrdinal("DataOdbioru"));

                                var dostawa = new DostawaModel
                                {
                                    LP = reader["LP"]?.ToString(),
                                    DataOdbioru = dataOdbioru,
                                    Dostawca = reader["Dostawca"]?.ToString(),
                                    Auta = reader["Auta"] != DBNull.Value ? Convert.ToInt32(reader["Auta"]) : 0,
                                    SztukiDek = reader["SztukiDek"] != DBNull.Value ? Convert.ToDouble(reader["SztukiDek"]) : 0,
                                    WagaDek = reader["WagaDek"] != DBNull.Value ? Convert.ToDecimal(reader["WagaDek"]) : 0,
                                    Bufor = reader["bufor"]?.ToString(),
                                    TypCeny = reader["TypCeny"]?.ToString(),
                                    Cena = reader["Cena"] != DBNull.Value ? Convert.ToDecimal(reader["Cena"]) : 0,
                                    Distance = reader["Distance"] != DBNull.Value ? Convert.ToInt32(reader["Distance"]) : 0,
                                    Uwagi = reader["UWAGI"]?.ToString(),
                                    DataNotatki = reader["DataNotatki"] != DBNull.Value ? Convert.ToDateTime(reader["DataNotatki"]) : (DateTime?)null,
                                    IsConfirmed = reader["bufor"]?.ToString() == "Potwierdzony",
                                    IsWstawienieConfirmed = reader["isConf"] != DBNull.Value && Convert.ToBoolean(reader["isConf"]),
                                    LpW = reader["LpW"] != DBNull.Value ? reader["LpW"].ToString() : null,
                                    PotwWaga = reader["PotwWaga"] != DBNull.Value && Convert.ToBoolean(reader["PotwWaga"]),
                                    PotwSztuki = reader["PotwSztuki"] != DBNull.Value && Convert.ToBoolean(reader["PotwSztuki"]),
                                    Ubytek = reader["Ubytek"] != DBNull.Value ? Convert.ToInt32(reader["Ubytek"]) : 0
                                };

                                if (reader["DataWstawienia"] != DBNull.Value)
                                {
                                    DateTime dataWstawienia = Convert.ToDateTime(reader["DataWstawienia"]);
                                    dostawa.RoznicaDni = (dataOdbioru - dataWstawienia).Days;
                                }

                                tempList.Add(dostawa);
                            }
                        }
                    }
                }

                // Filtruj według wyszukiwania
                if (!string.IsNullOrWhiteSpace(_searchText))
                {
                    tempList = tempList.Where(d =>
                        d.Dostawca?.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        d.Uwagi?.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0
                    ).ToList();
                }

                // Pobierz osobno liczby sprzedanych i anulowanych dla każdego dnia (bez filtrowania)
                var countsByDay = new Dictionary<DateTime, (int sprzedane, int anulowane)>();
                using (SqlConnection conn2 = new SqlConnection(ConnectionString))
                {
                    await conn2.OpenAsync(_cts.Token);
                    string countSql = @"SELECT DataOdbioru,
                        SUM(CASE WHEN bufor = 'Sprzedany' THEN 1 ELSE 0 END) as Sprzedane,
                        SUM(CASE WHEN bufor = 'Anulowany' THEN 1 ELSE 0 END) as Anulowane
                        FROM HarmonogramDostaw
                        WHERE DataOdbioru >= @startDate AND DataOdbioru < @endDate
                        GROUP BY DataOdbioru";
                    using (SqlCommand cmd2 = new SqlCommand(countSql, conn2))
                    {
                        cmd2.Parameters.AddWithValue("@startDate", startOfWeek);
                        cmd2.Parameters.AddWithValue("@endDate", endOfWeek);
                        using (var reader2 = await cmd2.ExecuteReaderAsync(_cts.Token))
                        {
                            while (await reader2.ReadAsync(_cts.Token))
                            {
                                DateTime date = Convert.ToDateTime(reader2["DataOdbioru"]).Date;
                                int sprzedane = Convert.ToInt32(reader2["Sprzedane"]);
                                int anulowane = Convert.ToInt32(reader2["Anulowane"]);
                                countsByDay[date] = (sprzedane, anulowane);
                            }
                        }
                    }
                }

                // Zachowaj pełną listę do wyszukiwania
                if (collection == _dostawy)
                    _allDostawy = new List<DostawaModel>(tempList);
                else
                    _allDostawyNastepny = new List<DostawaModel>(tempList);

                // Grupuj dane według daty
                var groupedByDate = tempList.GroupBy(d => d.DataOdbioru.Date).ToDictionary(g => g.Key, g => g.ToList());

                // Aktualizuj UI na głównym wątku
                await Dispatcher.InvokeAsync(() =>
                {
                    collection.Clear();
                    bool isFirst = true;

                    // Iteruj przez wszystkie dni tygodnia (Pon-Ndz)
                    for (int i = 0; i < 7; i++)
                    {
                        DateTime currentDay = startOfWeek.AddDays(i);

                        // Dodaj separator między dniami (oprócz pierwszego)
                        if (!isFirst)
                        {
                            collection.Add(new DostawaModel { IsHeaderRow = true, IsSeparator = true });
                        }
                        isFirst = false;

                        // Sprawdź czy są dostawy dla tego dnia
                        bool hasDeliveries = groupedByDate.ContainsKey(currentDay.Date);
                        var deliveries = hasDeliveries ? groupedByDate[currentDay.Date] : new List<DostawaModel>();

                        // Oblicz sumy i średnie ważone dla tego dnia
                        double sumaAuta = 0;
                        double sumaSztuki = 0;
                        double sumaWagaPomnozona = 0;
                        double sumaCenaPomnozona = 0;
                        double sumaKMPomnozona = 0;
                        double sumaDobyPomnozona = 0;
                        int sumaUbytek = 0;
                        int iloscZDoby = 0;

                        foreach (var item in deliveries)
                        {
                            sumaAuta += item.Auta;
                            sumaSztuki += item.SztukiDek;
                            sumaWagaPomnozona += (double)item.WagaDek * item.Auta;
                            sumaCenaPomnozona += (double)item.Cena * item.Auta;
                            sumaKMPomnozona += item.Distance * item.Auta;

                            // Średnia Doby
                            if (item.RoznicaDni.HasValue && item.RoznicaDni.Value > 0)
                            {
                                sumaDobyPomnozona += item.RoznicaDni.Value * item.Auta;
                                iloscZDoby += item.Auta;
                            }

                            // Licz ubytki (lekkie kurczaki 0.5-2.4 kg)
                            if (item.WagaDek >= 0.5m && item.WagaDek <= 2.4m)
                            {
                                sumaUbytek += item.Auta;
                            }
                        }

                        // Oblicz średnie ważone
                        double sredniaWaga = sumaAuta > 0 ? sumaWagaPomnozona / sumaAuta : 0;
                        double sredniaCena = sumaAuta > 0 ? sumaCenaPomnozona / sumaAuta : 0;
                        double sredniaKM = sumaAuta > 0 ? sumaKMPomnozona / sumaAuta : 0;
                        double sredniaDoby = iloscZDoby > 0 ? sumaDobyPomnozona / iloscZDoby : 0;

                        // Pobierz liczby sprzedanych i anulowanych z osobnego zapytania
                        int liczbaSprzedanych = 0;
                        int liczbaAnulowanych = 0;
                        if (countsByDay.TryGetValue(currentDay.Date, out var counts))
                        {
                            liczbaSprzedanych = counts.sprzedane;
                            liczbaAnulowanych = counts.anulowane;
                        }

                        // Dodaj wiersz nagłówka dnia z sumami
                        collection.Add(new DostawaModel
                        {
                            IsHeaderRow = true,
                            IsEmptyDay = !hasDeliveries,
                            DataOdbioru = currentDay,
                            Dostawca = currentDay.ToString("yyyy-MM-dd dddd", new CultureInfo("pl-PL")),
                            SumaAuta = sumaAuta,
                            SumaSztuki = sumaSztuki,
                            SredniaWaga = sredniaWaga,
                            SredniaCena = sredniaCena,
                            SredniaKM = sredniaKM,
                            SredniaDoby = sredniaDoby,
                            SumaUbytek = sumaUbytek,
                            LiczbaSprzedanych = liczbaSprzedanych,
                            LiczbaAnulowanych = liczbaAnulowanych
                        });

                        // Dodaj wszystkie dostawy dla tego dnia
                        foreach (var dostawa in deliveries)
                        {
                            collection.Add(dostawa);
                        }
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // Operacja anulowana - ignoruj
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                    ShowToast($"Błąd ładowania dostaw: {ex.Message}", ToastType.Error));
            }
        }

        // Stara synchroniczna wersja dla kompatybilności
        private void LoadDostawyForWeek(ObservableCollection<DostawaModel> collection, DateTime baseDate)
        {
            _ = LoadDostawyForWeekAsync(collection, baseDate);
        }

        private string BuildDostawyQuery()
        {
            string sql = @"
                SELECT DISTINCT
                    HD.LP, HD.DataOdbioru, HD.Dostawca, HD.Auta, HD.SztukiDek, HD.WagaDek, HD.bufor,
                    HD.TypCeny, HD.Cena, WK.DataWstawienia, D.Distance, HD.Ubytek, HD.LpW,
                    (SELECT TOP 1 N.Tresc FROM Notatki N WHERE N.IndeksID = HD.Lp ORDER BY N.DataUtworzenia DESC) AS UWAGI,
                    (SELECT TOP 1 N.DataUtworzenia FROM Notatki N WHERE N.IndeksID = HD.Lp ORDER BY N.DataUtworzenia DESC) AS DataNotatki,
                    HD.PotwWaga, HD.PotwSztuki, WK.isConf,
                    CASE WHEN HD.bufor = 'Potwierdzony' THEN 1 WHEN HD.bufor = 'B.Kontr.' THEN 2
                         WHEN HD.bufor = 'B.Wolny.' THEN 3 WHEN HD.bufor = 'Do Wykupienia' THEN 5 ELSE 4 END AS buforPriority
                FROM HarmonogramDostaw HD
                LEFT JOIN WstawieniaKurczakow WK ON HD.LpW = WK.Lp
                LEFT JOIN [LibraNet].[dbo].[Dostawcy] D ON HD.Dostawca = D.Name
                WHERE HD.DataOdbioru >= @startDate AND HD.DataOdbioru < @endDate AND (D.Halt = '0' OR D.Halt IS NULL)";

            if (chkAnulowane?.IsChecked != true) sql += " AND bufor != 'Anulowany'";
            if (chkSprzedane?.IsChecked != true) sql += " AND bufor != 'Sprzedany'";
            if (chkDoWykupienia?.IsChecked != true) sql += " AND bufor != 'Do Wykupienia'";

            sql += " ORDER BY HD.DataOdbioru, buforPriority, HD.WagaDek DESC";
            return sql;
        }

        #endregion

        #region Ładowanie danych - Ceny

        private async Task LoadCenyAsync()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);

                    // Cena rolnicza
                    double cenaRolnicza = await GetLatestPriceAsync(conn, "CenaRolnicza", "cena");
                    double cenaMinister = await GetLatestPriceAsync(conn, "CenaMinister", "cena");
                    double cenaLaczona = (cenaRolnicza + cenaMinister) / 2;
                    double cenaTuszki = await GetLatestPriceAsync(conn, "CenaTuszki", "cena");

                    // Aktualizuj UI na głównym wątku
                    await Dispatcher.InvokeAsync(() =>
                    {
                        txtCenaRolnicza.Text = cenaRolnicza > 0 ? $"{cenaRolnicza:F2} zł" : "-";
                        txtCenaMinister.Text = cenaMinister > 0 ? $"{cenaMinister:F2} zł" : "-";
                        txtCenaLaczona.Text = cenaLaczona > 0 ? $"{cenaLaczona:F2} zł" : "-";
                        txtCenaTuszki.Text = cenaTuszki > 0 ? $"{cenaTuszki:F2} zł" : "-";
                    });
                }
            }
            catch { }
        }

        // Stara synchroniczna wersja dla kompatybilności
        private void LoadCeny()
        {
            _ = LoadCenyAsync();
        }

        private async Task<double> GetLatestPriceAsync(SqlConnection conn, string table, string column)
        {
            try
            {
                string sql = $"SELECT TOP 1 {column} FROM [LibraNet].[dbo].[{table}] ORDER BY data DESC";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    var result = await cmd.ExecuteScalarAsync(_cts.Token);
                    return result != DBNull.Value && result != null ? Convert.ToDouble(result) : 0;
                }
            }
            catch { return 0; }
        }

        private double GetLatestPrice(SqlConnection conn, string table, string column)
        {
            try
            {
                string sql = $"SELECT TOP 1 {column} FROM [LibraNet].[dbo].[{table}] ORDER BY data DESC";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    var result = cmd.ExecuteScalar();
                    return result != DBNull.Value && result != null ? Convert.ToDouble(result) : 0;
                }
            }
            catch { return 0; }
        }

        #endregion

        #region Ładowanie danych - Partie

        private async Task LoadPartieAsync(DateTime? data = null)
        {
            try
            {
                // Użyj przekazanej daty lub pobierz z DatePicker lub domyślnie dziś
                DateTime dataPartii = data ?? dpPartieData?.SelectedDate ?? DateTime.Today;

                var tempList = new List<PartiaModel>();

                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);

                    string sql = @"
                        WITH Partie AS (
                            SELECT k.CreateData AS Data, CAST(k.P1 AS nvarchar(50)) AS PartiaFull,
                                   RIGHT(CONVERT(varchar(10), k.P1), 2) AS PartiaShort, pd.CustomerName AS Dostawca,
                                   AVG(k.QntInCont) AS Srednia,
                                   CONVERT(decimal(18, 2), (15.0 / CAST(AVG(CAST(k.QntInCont AS decimal(18, 2))) AS decimal(18, 2))) * 1.22) AS SredniaZywy,
                                   hd.WagaDek AS WagaDek
                            FROM [LibraNet].[dbo].[In0E] k
                            JOIN [LibraNet].[dbo].[PartiaDostawca] pd ON k.P1 = pd.Partia
                            LEFT JOIN [LibraNet].[dbo].[HarmonogramDostaw] hd ON k.CreateData = hd.DataOdbioru AND pd.CustomerName = hd.Dostawca
                            WHERE k.ArticleID = 40 AND k.QntInCont > 4 AND CONVERT(date, k.CreateData) = @DataPartii
                            GROUP BY k.CreateData, k.P1, pd.CustomerName, hd.WagaDek
                        )
                        SELECT p.*, CONVERT(decimal(18,2), p.SredniaZywy - p.WagaDek) AS Roznica,
                               w.Skrzydla_Ocena, w.Nogi_Ocena, w.Oparzenia_Ocena, pod.KlasaB_Proc, pod.Przekarmienie_Kg,
                               z.PhotoCount, z.FolderRel
                        FROM Partie p
                        LEFT JOIN dbo.QC_WadySkale w ON w.PartiaId = p.PartiaFull
                        LEFT JOIN dbo.QC_Podsum pod ON pod.PartiaId = p.PartiaFull
                        OUTER APPLY (SELECT PhotoCount = COUNT(*), FolderRel = MAX(LEFT(SciezkaPliku, LEN(SciezkaPliku) - CHARINDEX('\', REVERSE(SciezkaPliku))))
                                     FROM dbo.QC_Zdjecia z WHERE z.PartiaId = p.PartiaFull) z
                        ORDER BY p.PartiaFull DESC";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@DataPartii", dataPartii.Date);
                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync(_cts.Token))
                        {
                            while (await reader.ReadAsync(_cts.Token))
                            {
                                tempList.Add(new PartiaModel
                                {
                                    Partia = reader["PartiaShort"]?.ToString(),
                                    PartiaFull = reader["PartiaFull"]?.ToString(),
                                    Dostawca = reader["Dostawca"]?.ToString(),
                                    Srednia = reader["Srednia"] != DBNull.Value ? Convert.ToDecimal(reader["Srednia"]) : 0,
                                    Zywiec = reader["SredniaZywy"] != DBNull.Value ? Convert.ToDecimal(reader["SredniaZywy"]) : 0,
                                    Roznica = reader["Roznica"] != DBNull.Value ? Convert.ToDecimal(reader["Roznica"]) : 0,
                                    Skrzydla = reader["Skrzydla_Ocena"] != DBNull.Value ? Convert.ToInt32(reader["Skrzydla_Ocena"]) : (int?)null,
                                    Nogi = reader["Nogi_Ocena"] != DBNull.Value ? Convert.ToInt32(reader["Nogi_Ocena"]) : (int?)null,
                                    Oparzenia = reader["Oparzenia_Ocena"] != DBNull.Value ? Convert.ToInt32(reader["Oparzenia_Ocena"]) : (int?)null,
                                    KlasaB = reader["KlasaB_Proc"] != DBNull.Value ? Convert.ToDecimal(reader["KlasaB_Proc"]) : (decimal?)null,
                                    Przekarmienie = reader["Przekarmienie_Kg"] != DBNull.Value ? Convert.ToDecimal(reader["Przekarmienie_Kg"]) : (decimal?)null,
                                    PhotoCount = reader["PhotoCount"] != DBNull.Value ? Convert.ToInt32(reader["PhotoCount"]) : 0,
                                    FolderPath = reader["FolderRel"]?.ToString()
                                });
                            }
                        }
                    }
                }

                // Aktualizuj UI na głównym wątku
                await Dispatcher.InvokeAsync(() =>
                {
                    _partie.Clear();
                    foreach (var p in tempList)
                        _partie.Add(p);
                    txtPartieSuma.Text = _partie.Count > 0 ? $"| {_partie.Count} partii" : "";
                });
            }
            catch { }
        }

        // Stara synchroniczna wersja dla kompatybilności
        private void LoadPartie()
        {
            _ = LoadPartieAsync();
        }

        #endregion

        #region Ładowanie danych - Notatki

        private async Task LoadNotatkiAsync(string lpDostawa)
        {
            try
            {
                if (string.IsNullOrEmpty(lpDostawa)) return;

                var tempList = new List<NotatkaModel>();

                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    string sql = @"SELECT N.DataUtworzenia, O.Name AS KtoDodal, N.Tresc
                                   FROM [LibraNet].[dbo].[Notatki] N
                                   LEFT JOIN [LibraNet].[dbo].[operators] O ON N.KtoStworzyl = O.ID
                                   WHERE N.IndeksID = @Lp ORDER BY N.DataUtworzenia DESC";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Lp", lpDostawa);
                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync(_cts.Token))
                        {
                            while (await reader.ReadAsync(_cts.Token))
                            {
                                tempList.Add(new NotatkaModel
                                {
                                    DataUtworzenia = Convert.ToDateTime(reader["DataUtworzenia"]),
                                    KtoDodal = reader["KtoDodal"]?.ToString(),
                                    Tresc = reader["Tresc"]?.ToString()
                                });
                            }
                        }
                    }
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    _notatki.Clear();
                    foreach (var n in tempList)
                        _notatki.Add(n);
                    // Aktualizuj też DataGrid w zakładce Karta
                    dgNotatkiKarta.ItemsSource = _notatki;
                });
            }
            catch { }
        }

        // Stara synchroniczna wersja dla kompatybilności
        private void LoadNotatki(string lpDostawa)
        {
            _ = LoadNotatkiAsync(lpDostawa);
        }

        private async Task LoadOstatnieNotatkiAsync()
        {
            try
            {
                var tempList = new List<NotatkaModel>();

                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    string sql = @"SELECT TOP 20 N.DataUtworzenia, FORMAT(H.DataOdbioru, 'MM-dd ddd') AS DataOdbioru,
                                   H.Dostawca, N.Tresc, O.Name AS KtoDodal
                                   FROM [LibraNet].[dbo].[Notatki] N
                                   LEFT JOIN [LibraNet].[dbo].[operators] O ON N.KtoStworzyl = O.ID
                                   LEFT JOIN [LibraNet].[dbo].[HarmonogramDostaw] H ON N.IndeksID = H.LP
                                   WHERE N.TypID = 1 ORDER BY N.DataUtworzenia DESC";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync(_cts.Token))
                    {
                        while (await reader.ReadAsync(_cts.Token))
                        {
                            tempList.Add(new NotatkaModel
                            {
                                DataUtworzenia = Convert.ToDateTime(reader["DataUtworzenia"]),
                                DataOdbioru = reader["DataOdbioru"]?.ToString(),
                                Dostawca = reader["Dostawca"]?.ToString(),
                                Tresc = reader["Tresc"]?.ToString(),
                                KtoDodal = reader["KtoDodal"]?.ToString()
                            });
                        }
                    }
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    _ostatnieNotatki.Clear();
                    foreach (var n in tempList)
                        _ostatnieNotatki.Add(n);
                });
            }
            catch { }
        }

        // Stara synchroniczna wersja dla kompatybilności
        private void LoadOstatnieNotatki()
        {
            _ = LoadOstatnieNotatkiAsync();
        }

        #endregion

        #region Ładowanie danych - Ranking

        private async Task LoadRankingAsync()
        {
            try
            {
                var tempList = new List<RankingModel>();

                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    string sql = @"SELECT TOP 20 Dostawca, AVG(WagaDek) as SredniaWaga, COUNT(*) as LiczbaD,
                                   SUM(CASE WHEN bufor = 'Potwierdzony' THEN 10 ELSE 5 END) as Punkty
                                   FROM HarmonogramDostaw
                                   WHERE DataOdbioru >= DATEADD(month, -3, GETDATE()) AND bufor NOT IN ('Anulowany')
                                   GROUP BY Dostawca ORDER BY Punkty DESC, SredniaWaga DESC";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync(_cts.Token))
                    {
                        int pos = 1;
                        while (await reader.ReadAsync(_cts.Token))
                        {
                            tempList.Add(new RankingModel
                            {
                                Pozycja = pos++,
                                Dostawca = reader["Dostawca"]?.ToString(),
                                SredniaWaga = reader["SredniaWaga"] != DBNull.Value ? $"{Convert.ToDecimal(reader["SredniaWaga"]):F2}" : "-",
                                LiczbaD = Convert.ToInt32(reader["LiczbaD"]),
                                Punkty = Convert.ToInt32(reader["Punkty"])
                            });
                        }
                    }
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    _ranking.Clear();
                    foreach (var r in tempList)
                        _ranking.Add(r);
                });
            }
            catch { }
        }

        // Stara synchroniczna wersja dla kompatybilności
        private void LoadRanking()
        {
            _ = LoadRankingAsync();
        }

        #endregion

        #region Ładowanie danych - Wstawienia

        private void LoadWstawienia(string lpWstawienia)
        {
            try
            {
                _wstawienia.Clear();
                double sumaSztuk = 0;

                if (string.IsNullOrEmpty(lpWstawienia)) return;

                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();

                    // Dane wstawienia
                    string sql = "SELECT * FROM dbo.WstawieniaKurczakow WHERE Lp = @lp";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@lp", lpWstawienia);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                txtDataWstawienia.Text = Convert.ToDateTime(reader["DataWstawienia"]).ToString("yyyy-MM-dd");
                                txtSztukiWstawienia.Text = reader["IloscWstawienia"]?.ToString();

                                DateTime dataWstaw = Convert.ToDateTime(reader["DataWstawienia"]);
                                txtObecnaDoba.Text = (DateTime.Now - dataWstaw).Days.ToString();
                            }
                        }
                    }

                    // Powiązane dostawy
                    sql = "SELECT LP, DataOdbioru, Auta, SztukiDek, WagaDek, bufor FROM HarmonogramDostaw WHERE LpW = @lp ORDER BY DataOdbioru";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@lp", lpWstawienia);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                double sztuki = reader["SztukiDek"] != DBNull.Value ? Convert.ToDouble(reader["SztukiDek"]) : 0;
                                sumaSztuk += sztuki;

                                _wstawienia.Add(new DostawaModel
                                {
                                    DataOdbioru = Convert.ToDateTime(reader["DataOdbioru"]),
                                    Auta = reader["Auta"] != DBNull.Value ? Convert.ToInt32(reader["Auta"]) : 0,
                                    SztukiDek = sztuki,
                                    WagaDek = reader["WagaDek"] != DBNull.Value ? Convert.ToDecimal(reader["WagaDek"]) : 0,
                                    Bufor = reader["bufor"]?.ToString()
                                });
                            }
                        }
                    }

                    // Oblicz pozostałe
                    if (double.TryParse(txtSztukiWstawienia.Text, out double wstawione))
                    {
                        double pozostale = (wstawione * 0.97) - sumaSztuk;
                        txtSztukiPozostale.Text = $"{pozostale:#,0} szt";
                    }
                }
            }
            catch { }
        }

        #endregion

        #region Ładowanie danych - Hodowcy (z cache)

        private async Task LoadHodowcyToComboBoxAsync()
        {
            try
            {
                // Sprawdź czy cache jest aktualny
                if (_hodowcyCache != null && DateTime.Now < _hodowcyCacheExpiry)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        cmbDostawca.Items.Clear();
                        foreach (var h in _hodowcyCache)
                            cmbDostawca.Items.Add(h);
                    });
                    return;
                }

                var tempList = new List<string>();

                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    string sql = "SELECT Name FROM [LibraNet].[dbo].[Dostawcy] WHERE Halt = '0' ORDER BY Name";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync(_cts.Token))
                    {
                        while (await reader.ReadAsync(_cts.Token))
                        {
                            tempList.Add(reader["Name"]?.ToString());
                        }
                    }
                }

                // Zapisz do cache
                _hodowcyCache = tempList;
                _hodowcyCacheExpiry = DateTime.Now.Add(CACHE_DURATION);

                await Dispatcher.InvokeAsync(() =>
                {
                    cmbDostawca.Items.Clear();
                    foreach (var h in tempList)
                        cmbDostawca.Items.Add(h);
                });
            }
            catch { }
        }

        // Stara synchroniczna wersja dla kompatybilności
        private void LoadHodowcyToComboBox()
        {
            // Użyj cache jeśli dostępny
            if (_hodowcyCache != null && DateTime.Now < _hodowcyCacheExpiry)
            {
                cmbDostawca.Items.Clear();
                foreach (var h in _hodowcyCache)
                    cmbDostawca.Items.Add(h);
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();
                    string sql = "SELECT Name FROM [LibraNet].[dbo].[Dostawcy] WHERE Halt = '0' ORDER BY Name";
                    var tempList = new List<string>();
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            tempList.Add(reader["Name"]?.ToString());
                        }
                    }

                    // Zapisz do cache
                    _hodowcyCache = tempList;
                    _hodowcyCacheExpiry = DateTime.Now.Add(CACHE_DURATION);

                    foreach (var h in tempList)
                        cmbDostawca.Items.Add(h);
                }
            }
            catch { }
        }

        private async Task LoadLpWstawieniaForHodowcaAsync(string hodowca)
        {
            try
            {
                var tempList = new List<string>();

                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    string sql = "SELECT Lp FROM WstawieniaKurczakow WHERE Hodowca = @h ORDER BY DataWstawienia DESC";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@h", hodowca);
                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync(_cts.Token))
                        {
                            while (await reader.ReadAsync(_cts.Token))
                            {
                                tempList.Add(reader["Lp"]?.ToString());
                            }
                        }
                    }
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    cmbLpWstawienia.Items.Clear();
                    foreach (var lp in tempList)
                        cmbLpWstawienia.Items.Add(lp);
                });
            }
            catch { }
        }

        // Stara synchroniczna wersja dla kompatybilności
        private void LoadLpWstawieniaForHodowca(string hodowca)
        {
            _ = LoadLpWstawieniaForHodowcaAsync(hodowca);
        }

        // Wyszukiwanie hodowcy w cache
        public IEnumerable<string> SearchHodowcy(string searchText)
        {
            if (_hodowcyCache == null) return Enumerable.Empty<string>();

            return _hodowcyCache.Where(h =>
                h.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0
            );
        }

        #endregion

        #region Obsługa kalendarza

        private async void CalendarMain_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            if (calendarMain.SelectedDate.HasValue)
            {
                _selectedDate = calendarMain.SelectedDate.Value;
                UpdateWeekNumber();

                // Aktualizuj mini-mapę tygodni
                int currentWeek = GetIso8601WeekOfYear(DateTime.Today);
                int selectedWeek = GetIso8601WeekOfYear(_selectedDate);
                _weekMapOffset = selectedWeek - currentWeek;
                if (_selectedDate.Year > DateTime.Today.Year)
                    _weekMapOffset += GetWeeksInYear(DateTime.Today.Year);
                else if (_selectedDate.Year < DateTime.Today.Year)
                    _weekMapOffset -= GetWeeksInYear(_selectedDate.Year);
                GenerateWeekMap();

                await LoadDostawyAsync();
            }
        }

        // Obsługa zmiany daty w DatePicker Partii
        private async void DpPartieData_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;

            if (dpPartieData?.SelectedDate != null)
            {
                await LoadPartieAsync(dpPartieData.SelectedDate.Value);
            }
        }

        private void UpdateWeekNumber()
        {
            int week = GetIso8601WeekOfYear(_selectedDate);
            if (txtWeekNumber != null)
                txtWeekNumber.Text = week.ToString();
        }

        private int GetIso8601WeekOfYear(DateTime time)
        {
            var cal = CultureInfo.CurrentCulture.Calendar;
            return cal.GetWeekOfYear(time, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        }

        #endregion

        #region Nawigacja

        private async void BtnPreviousWeek_Click(object sender, RoutedEventArgs e)
        {
            _selectedDate = _selectedDate.AddDays(-7);
            calendarMain.SelectedDate = _selectedDate;
            calendarMain.DisplayDate = _selectedDate;
            UpdateWeekNumber();
            _weekMapOffset--;
            GenerateWeekMap();

            // Animacja: prawy idzie w prawo (znika), lewy idzie w prawo, nowy lewy wchodzi z lewej
            await AnimateWeekTransition(isNextWeek: false);
        }

        private async void BtnNextWeek_Click(object sender, RoutedEventArgs e)
        {
            _selectedDate = _selectedDate.AddDays(7);
            calendarMain.SelectedDate = _selectedDate;
            calendarMain.DisplayDate = _selectedDate;
            UpdateWeekNumber();
            _weekMapOffset++;
            GenerateWeekMap();

            // Animacja: lewy idzie w lewo (znika), prawy idzie w lewo na jego miejsce, nowy prawy wchodzi z prawej
            await AnimateWeekTransition(isNextWeek: true);
        }

        private async void BtnToday_Click(object sender, RoutedEventArgs e)
        {
            _selectedDate = DateTime.Today;
            calendarMain.SelectedDate = _selectedDate;
            calendarMain.DisplayDate = _selectedDate;
            UpdateWeekNumber();
            _weekMapOffset = 0;
            GenerateWeekMap();

            // Fade in bez slide
            var fadeAnimation = new DoubleAnimation(0.5, 1, TimeSpan.FromMilliseconds(300));
            dgDostawy.BeginAnimation(OpacityProperty, fadeAnimation);
            if (chkNastepnyTydzien?.IsChecked == true)
                dgDostawyNastepny.BeginAnimation(OpacityProperty, fadeAnimation);
            await LoadDostawyAsync();
        }

        /// <summary>
        /// Animacja przejścia między tygodniami - dwufazowa karuzela
        /// Faza 1: Wyjście starego widoku (slide out)
        /// Faza 2: Załadowanie danych + wejście nowego widoku (slide in)
        /// </summary>
        private async Task AnimateWeekTransition(bool isNextWeek)
        {
            bool showSecondTable = chkNastepnyTydzien?.IsChecked == true;

            // Upewnij się, że transformacje są ustawione
            EnsureTransformsInitialized();

            // FAZA 1: Animacja wyjścia
            var slideOutKey = isNextWeek ? "WeekSlideOutLeftAnimation" : "WeekSlideOutRightAnimation";
            var slideOutStoryboard = (Storyboard)FindResource(slideOutKey);

            // Klonuj storyboard dla każdej tabeli
            var slideOut1 = slideOutStoryboard.Clone();
            Storyboard.SetTarget(slideOut1, dgDostawy);

            // Rozpocznij animację wyjścia
            var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
            slideOut1.Completed += (s, e) => tcs.TrySetResult(true);
            slideOut1.Begin();

            if (showSecondTable)
            {
                var slideOut2 = slideOutStoryboard.Clone();
                Storyboard.SetTarget(slideOut2, dgDostawyNastepny);
                slideOut2.Begin();
            }

            // Czekaj na zakończenie animacji wyjścia (350ms)
            await tcs.Task;

            // FAZA 2: Załaduj dane (podczas gdy elementy są niewidoczne)
            await LoadDostawyAsync();

            // FAZA 3: Animacja wejścia z nowych danych
            var slideInKey = isNextWeek ? "WeekSlideInFromRightAnimation" : "WeekSlideInFromLeftAnimation";
            var slideInStoryboard = (Storyboard)FindResource(slideInKey);

            // Klonuj storyboard dla każdej tabeli
            var slideIn1 = slideInStoryboard.Clone();
            Storyboard.SetTarget(slideIn1, dgDostawy);
            slideIn1.Begin();

            if (showSecondTable)
            {
                var slideIn2 = slideInStoryboard.Clone();
                Storyboard.SetTarget(slideIn2, dgDostawyNastepny);
                slideIn2.Begin();
            }
        }

        /// <summary>
        /// Inicjalizuje TranslateTransform dla obu DataGridów
        /// </summary>
        private void EnsureTransformsInitialized()
        {
            if (!(dgDostawy.RenderTransform is TranslateTransform))
            {
                dgDostawy.RenderTransform = new TranslateTransform(0, 0);
            }
            if (!(dgDostawyNastepny.RenderTransform is TranslateTransform))
            {
                dgDostawyNastepny.RenderTransform = new TranslateTransform(0, 0);
            }
        }

        #endregion

        #region Mini-mapa tygodni

        /// <summary>
        /// Generuje przyciski tygodni w mini-mapie
        /// </summary>
        private void GenerateWeekMap()
        {
            if (spWeekMap == null) return;

            spWeekMap.Children.Clear();

            int currentWeek = GetIso8601WeekOfYear(DateTime.Today);
            int selectedWeek = GetIso8601WeekOfYear(_selectedDate);
            int currentYear = DateTime.Today.Year;
            int selectedYear = _selectedDate.Year;

            // Generuj przyciski dla tygodni: od (bieżący + offset - 4) do (bieżący + offset + 4)
            int startWeek = currentWeek + _weekMapOffset - (WEEK_MAP_VISIBLE_COUNT / 2);

            for (int i = 0; i < WEEK_MAP_VISIBLE_COUNT; i++)
            {
                int weekNum = startWeek + i;
                int year = currentYear;

                // Obsługa przejścia między latami
                if (weekNum < 1)
                {
                    year--;
                    weekNum = GetWeeksInYear(year) + weekNum;
                }
                else if (weekNum > GetWeeksInYear(year))
                {
                    weekNum = weekNum - GetWeeksInYear(year);
                    year++;
                }

                var btn = new Button
                {
                    Content = weekNum.ToString(),
                    Style = (Style)FindResource("WeekMapButtonStyle"),
                    ToolTip = GetWeekDateRange(weekNum, year)
                };

                // Ustaw Tag dla stylowania
                bool isCurrent = (weekNum == currentWeek && year == currentYear);
                bool isSelected = (weekNum == selectedWeek && year == selectedYear);

                if (isCurrent && isSelected)
                    btn.Tag = "CurrentSelected";
                else if (isCurrent)
                    btn.Tag = "Current";
                else if (isSelected)
                    btn.Tag = "Selected";

                // Zapisz dane tygodnia w DataContext
                btn.DataContext = new WeekMapItem { WeekNumber = weekNum, Year = year };
                btn.Click += WeekMapButton_Click;

                spWeekMap.Children.Add(btn);
            }
        }

        /// <summary>
        /// Zwraca zakres dat dla danego tygodnia (tooltip)
        /// </summary>
        private string GetWeekDateRange(int weekNumber, int year)
        {
            DateTime jan1 = new DateTime(year, 1, 1);
            int daysOffset = DayOfWeek.Monday - jan1.DayOfWeek;
            if (daysOffset > 0) daysOffset -= 7;

            DateTime firstMonday = jan1.AddDays(daysOffset);
            DateTime weekStart = firstMonday.AddDays((weekNumber - 1) * 7);
            DateTime weekEnd = weekStart.AddDays(6);

            return $"Tydzień {weekNumber}/{year}\n{weekStart:dd.MM} - {weekEnd:dd.MM}";
        }

        /// <summary>
        /// Zwraca liczbę tygodni w danym roku
        /// </summary>
        private int GetWeeksInYear(int year)
        {
            DateTime dec31 = new DateTime(year, 12, 31);
            var cal = CultureInfo.CurrentCulture.Calendar;
            int lastWeek = cal.GetWeekOfYear(dec31, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);

            // Jeśli 31 grudnia jest w tygodniu 1 następnego roku, weź tydzień z 24 grudnia
            if (lastWeek == 1)
            {
                lastWeek = cal.GetWeekOfYear(dec31.AddDays(-7), CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            }
            return lastWeek;
        }

        /// <summary>
        /// Zwraca datę poniedziałku dla danego numeru tygodnia
        /// </summary>
        private DateTime GetMondayOfWeek(int weekNumber, int year)
        {
            DateTime jan1 = new DateTime(year, 1, 1);
            int daysOffset = DayOfWeek.Monday - jan1.DayOfWeek;
            if (daysOffset > 0) daysOffset -= 7;

            DateTime firstMonday = jan1.AddDays(daysOffset);
            return firstMonday.AddDays((weekNumber - 1) * 7);
        }

        /// <summary>
        /// Obsługa kliknięcia przycisku tygodnia
        /// </summary>
        private async void WeekMapButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is WeekMapItem weekItem)
            {
                DateTime targetDate = GetMondayOfWeek(weekItem.WeekNumber, weekItem.Year);

                // Sprawdź czy to zmiana tygodnia
                int currentSelectedWeek = GetIso8601WeekOfYear(_selectedDate);
                bool isNextWeek = weekItem.WeekNumber > currentSelectedWeek ||
                                  (weekItem.Year > _selectedDate.Year);

                _selectedDate = targetDate;
                calendarMain.SelectedDate = _selectedDate;
                calendarMain.DisplayDate = _selectedDate;
                UpdateWeekNumber();

                // Wycentruj mini-mapę na wybranym tygodniu
                int currentWeek = GetIso8601WeekOfYear(DateTime.Today);
                _weekMapOffset = weekItem.WeekNumber - currentWeek;
                if (weekItem.Year > DateTime.Today.Year)
                    _weekMapOffset += GetWeeksInYear(DateTime.Today.Year);
                else if (weekItem.Year < DateTime.Today.Year)
                    _weekMapOffset -= GetWeeksInYear(weekItem.Year);

                GenerateWeekMap();

                // Animacja przejścia
                await AnimateWeekTransition(isNextWeek);

                ShowToast($"Tydzień {weekItem.WeekNumber}", ToastType.Info);
            }
        }

        /// <summary>
        /// Przesuń mini-mapę w lewo (wcześniejsze tygodnie)
        /// </summary>
        private void BtnWeekMapPrev_Click(object sender, RoutedEventArgs e)
        {
            _weekMapOffset -= WEEK_MAP_VISIBLE_COUNT;
            GenerateWeekMap();
        }

        /// <summary>
        /// Przesuń mini-mapę w prawo (późniejsze tygodnie)
        /// </summary>
        private void BtnWeekMapNext_Click(object sender, RoutedEventArgs e)
        {
            _weekMapOffset += WEEK_MAP_VISIBLE_COUNT;
            GenerateWeekMap();
        }

        /// <summary>
        /// Klasa pomocnicza dla elementu mini-mapy
        /// </summary>
        private class WeekMapItem
        {
            public int WeekNumber { get; set; }
            public int Year { get; set; }
        }

        #endregion

        #region Obsługa filtrów

        private async void Filter_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;

            if (colCena != null && chkPokazCeny != null)
                colCena.Visibility = chkPokazCeny.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

            await LoadDostawyAsync();
        }

        private async void ChkNastepnyTydzien_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;

            if (chkNastepnyTydzien?.IsChecked == true)
            {
                // Animacja expand z prawej strony
                if (colNastepnyTydzien != null) colNastepnyTydzien.Width = new GridLength(1, GridUnitType.Star);
                if (borderNastepnyTydzien != null)
                {
                    AnimateExpandCollapse(borderNastepnyTydzien, expand: true);
                }
                await LoadDostawyForWeekAsync(_dostawyNastepnyTydzien, _selectedDate.AddDays(7));
            }
            else
            {
                // Animacja collapse w prawo
                if (borderNastepnyTydzien != null)
                {
                    AnimateExpandCollapse(borderNastepnyTydzien, expand: false, onComplete: () =>
                    {
                        if (colNastepnyTydzien != null) colNastepnyTydzien.Width = new GridLength(0);
                    });
                }
                else if (colNastepnyTydzien != null)
                {
                    colNastepnyTydzien.Width = new GridLength(0);
                }
            }
        }

        private void ChkPokazCheckboxy_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;

            var visibility = chkPokazCheckboxy?.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

            // Tabela główna
            if (colCheckConfirm != null) colCheckConfirm.Visibility = visibility;
            if (colCheckWstawienie != null) colCheckWstawienie.Visibility = visibility;

            // Tabela następny tydzień
            if (colCheckConfirm2 != null) colCheckConfirm2.Visibility = visibility;
        }

        // Otwieranie menu filtrów
        private void BtnFiltry_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.ContextMenu != null)
            {
                element.ContextMenu.PlacementTarget = element;
                element.ContextMenu.IsOpen = true;
            }
        }

        // Synchronizacja menu z ukrytymi checkboxami
        private async void MenuFilter_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;

            // Synchronizuj menu z ukrytymi checkboxami
            if (menuChkAnulowane != null && chkAnulowane != null)
                chkAnulowane.IsChecked = menuChkAnulowane.IsChecked;
            if (menuChkSprzedane != null && chkSprzedane != null)
                chkSprzedane.IsChecked = menuChkSprzedane.IsChecked;
            if (menuChkDoWykupienia != null && chkDoWykupienia != null)
                chkDoWykupienia.IsChecked = menuChkDoWykupienia.IsChecked;
            if (menuChkPokazCeny != null && chkPokazCeny != null)
                chkPokazCeny.IsChecked = menuChkPokazCeny.IsChecked;

            if (colCena != null && chkPokazCeny != null)
                colCena.Visibility = chkPokazCeny.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

            await LoadDostawyAsync();
        }

        private void MenuChkPokazCheckboxy_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;

            if (menuChkPokazCheckboxy != null && chkPokazCheckboxy != null)
                chkPokazCheckboxy.IsChecked = menuChkPokazCheckboxy.IsChecked;

            var visibility = chkPokazCheckboxy?.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

            if (colCheckConfirm != null) colCheckConfirm.Visibility = visibility;
            if (colCheckWstawienie != null) colCheckWstawienie.Visibility = visibility;
            if (colCheckConfirm2 != null) colCheckConfirm2.Visibility = visibility;
        }

        private async void MenuChkNastepnyTydzien_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;

            if (menuChkNastepnyTydzien != null && chkNastepnyTydzien != null)
                chkNastepnyTydzien.IsChecked = menuChkNastepnyTydzien.IsChecked;

            if (chkNastepnyTydzien?.IsChecked == true)
            {
                if (colNastepnyTydzien != null) colNastepnyTydzien.Width = new GridLength(1, GridUnitType.Star);
                if (borderNastepnyTydzien != null)
                {
                    AnimateExpandCollapse(borderNastepnyTydzien, expand: true);
                }
                await LoadDostawyForWeekAsync(_dostawyNastepnyTydzien, _selectedDate.AddDays(7));
            }
            else
            {
                if (borderNastepnyTydzien != null)
                {
                    AnimateExpandCollapse(borderNastepnyTydzien, expand: false, onComplete: () =>
                    {
                        if (colNastepnyTydzien != null) colNastepnyTydzien.Width = new GridLength(0);
                    });
                }
                else if (colNastepnyTydzien != null)
                {
                    colNastepnyTydzien.Width = new GridLength(0);
                }
            }
        }

        #endregion

        #region Obsługa DataGrid - Dostawy

        private bool _isUpdatingSelection = false;

        private DataGridRow _highlightedDayHeader = null;

        private void DgDostawy_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Wyczyść pola kalkulatora transportu przy zmianie zaznaczenia
            txtSztNaSzufladeCalc.Text = "";
            txtWyliczone.Text = "";
            txtObliczoneAuta.Text = "";
            txtObliczoneSztuki.Text = "";

            // Wyczyść zaznaczenie w drugiej tabeli
            if (!_isUpdatingSelection && dgDostawyNastepny.SelectedItems.Count > 0)
            {
                _isUpdatingSelection = true;
                dgDostawyNastepny.SelectedItem = null;
                _isUpdatingSelection = false;
            }

            // Obsługa multi-select
            _selectedLPs.Clear();
            foreach (var item in dgDostawy.SelectedItems)
            {
                var dostawa = item as DostawaModel;
                if (dostawa != null && !dostawa.IsHeaderRow && !dostawa.IsSeparator && !string.IsNullOrEmpty(dostawa.LP))
                {
                    _selectedLPs.Add(dostawa.LP);
                }
            }

            var selected = dgDostawy.SelectedItem as DostawaModel;
            if (selected != null && !selected.IsHeaderRow)
            {
                _selectedLP = selected.LP;
                _ = LoadDeliveryDetailsAsync(selected.LP);
                _ = LoadNotatkiAsync(selected.LP);

                // Aktualizuj nazwę hodowcy w sekcjach Notatki, Wstawienia i Dane dostawy
                txtHodowcaNotatki.Text = selected.Dostawca ?? "";
                txtHodowcaWstawienia.Text = selected.Dostawca ?? "";
                txtHodowcaDaneDostawy.Text = selected.Dostawca ?? "";

                if (!string.IsNullOrEmpty(selected.LpW))
                {
                    cmbLpWstawienia.SelectedItem = selected.LpW;
                    _ = LoadWstawieniaAsync(selected.LpW);
                }

                // Podświetl nagłówek dnia
                HighlightDayHeader(dgDostawy, selected.DataOdbioru.Date);
            }
            else
            {
                ClearDayHeaderHighlight();
            }

            // Aktualizuj status bar z informacją o zaznaczeniu
            if (_selectedLPs.Count > 1)
            {
                int totalAuta = dgDostawy.SelectedItems.Cast<DostawaModel>()
                    .Where(d => !d.IsHeaderRow && !d.IsSeparator)
                    .Sum(d => d.Auta);
                txtStatusBar.Text = $"Zaznaczono: {_selectedLPs.Count} dostaw | Auta: {totalAuta}";
            }
            else
            {
                UpdateStatusBar();
            }
        }

        private void HighlightDayHeader(DataGrid dg, DateTime date)
        {
            ClearDayHeaderHighlight();

            // Znajdź nagłówek dnia dla podanej daty
            foreach (var item in dg.Items)
            {
                var dostawa = item as DostawaModel;
                if (dostawa != null && dostawa.IsHeaderRow && !dostawa.IsSeparator && dostawa.DataOdbioru.Date == date)
                {
                    var row = dg.ItemContainerGenerator.ContainerFromItem(item) as DataGridRow;
                    if (row != null)
                    {
                        row.BorderBrush = new SolidColorBrush(Color.FromRgb(33, 150, 243)); // Blue
                        row.BorderThickness = new Thickness(2, 0, 2, 0);
                        _highlightedDayHeader = row;
                    }
                    break;
                }
            }
        }

        private void ClearDayHeaderHighlight()
        {
            if (_highlightedDayHeader != null)
            {
                // Przywróć domyślne wartości
                var dostawa = _highlightedDayHeader.DataContext as DostawaModel;
                if (dostawa != null && dostawa.DataOdbioru.Date == DateTime.Today)
                {
                    _highlightedDayHeader.BorderBrush = new SolidColorBrush(Color.FromRgb(230, 81, 0));
                    _highlightedDayHeader.BorderThickness = new Thickness(0, 2, 0, 2);
                }
                else
                {
                    _highlightedDayHeader.BorderThickness = new Thickness(0);
                }
                _highlightedDayHeader = null;
            }
        }

        private void DgDostawyNastepny_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Wyczyść pola kalkulatora transportu przy zmianie zaznaczenia
            txtSztNaSzufladeCalc.Text = "";
            txtWyliczone.Text = "";
            txtObliczoneAuta.Text = "";
            txtObliczoneSztuki.Text = "";

            // Wyczyść zaznaczenie w pierwszej tabeli
            if (!_isUpdatingSelection && dgDostawy.SelectedItems.Count > 0)
            {
                _isUpdatingSelection = true;
                dgDostawy.SelectedItem = null;
                _isUpdatingSelection = false;
            }

            var selected = dgDostawyNastepny.SelectedItem as DostawaModel;
            if (selected != null && !selected.IsHeaderRow)
            {
                _selectedLP = selected.LP;
                _ = LoadDeliveryDetailsAsync(selected.LP);
                _ = LoadNotatkiAsync(selected.LP);

                // Aktualizuj nazwę hodowcy w sekcjach Notatki, Wstawienia i Dane dostawy
                txtHodowcaNotatki.Text = selected.Dostawca ?? "";
                txtHodowcaWstawienia.Text = selected.Dostawca ?? "";
                txtHodowcaDaneDostawy.Text = selected.Dostawca ?? "";

                if (!string.IsNullOrEmpty(selected.LpW))
                {
                    cmbLpWstawienia.SelectedItem = selected.LpW;
                    _ = LoadWstawieniaAsync(selected.LpW);
                }

                // Podświetl nagłówek dnia
                HighlightDayHeader(dgDostawyNastepny, selected.DataOdbioru.Date);
            }
            else
            {
                ClearDayHeaderHighlight();
            }
        }

        private async Task LoadDeliveryDetailsAsync(string lp)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    string sql = @"SELECT HD.*, D.Address, D.PostalCode, D.City, D.Distance, D.Phone1, D.Phone2, D.Phone3,
                                   D.Info1, D.Info2, D.Info3, D.Email, D.TypOsobowosci, D.TypOsobowosci2,
                                   O1.Name as KtoStwoName, O2.Name as KtoModName, O3.Name as KtoWagaName, O4.Name as KtoSztukiName
                                   FROM HarmonogramDostaw HD
                                   LEFT JOIN Dostawcy D ON HD.Dostawca = D.Name
                                   LEFT JOIN operators O1 ON HD.ktoStwo = O1.ID
                                   LEFT JOIN operators O2 ON HD.ktoMod = O2.ID
                                   LEFT JOIN operators O3 ON HD.KtoWaga = O3.ID
                                   LEFT JOIN operators O4 ON HD.KtoSztuki = O4.ID
                                   WHERE HD.LP = @lp";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@lp", lp);
                        using (SqlDataReader r = await cmd.ExecuteReaderAsync(_cts.Token))
                        {
                            if (await r.ReadAsync(_cts.Token))
                            {
                                await Dispatcher.InvokeAsync(() =>
                                {
                                    // Hodowca
                                    cmbDostawca.SelectedItem = r["Dostawca"]?.ToString();
                                    txtUlicaH.Text = r["Address"]?.ToString();
                                    txtKodPocztowyH.Text = r["PostalCode"]?.ToString();
                                    txtMiejscH.Text = r["City"]?.ToString();
                                    txtKmH.Text = r["Distance"]?.ToString();
                                    txtEmail.Text = r["Email"]?.ToString();
                                    txtTel1.Text = r["Phone1"]?.ToString();
                                    txtTel2.Text = r["Phone2"]?.ToString();
                                    txtTel3.Text = r["Phone3"]?.ToString();
                                    txtInfo1.Text = r["Info1"]?.ToString();
                                    txtInfo2.Text = r["Info2"]?.ToString();
                                    txtInfo3.Text = r["Info3"]?.ToString();
                                    cmbOsobowosc1.SelectedItem = r["TypOsobowosci"]?.ToString();
                                    cmbOsobowosc2.SelectedItem = r["TypOsobowosci2"]?.ToString();

                                    // Dostawa
                                    dpData.SelectedDate = r["DataOdbioru"] != DBNull.Value ? Convert.ToDateTime(r["DataOdbioru"]) : (DateTime?)null;
                                    cmbStatus.SelectedItem = r["bufor"]?.ToString();
                                    CmbStatus_SelectionChanged(null, null); // Aktualizuj kolor
                                    txtAuta.Text = r["Auta"]?.ToString();
                                    txtSztuki.Text = r["SztukiDek"]?.ToString();
                                    txtWagaDek.Text = r["WagaDek"]?.ToString();
                                    txtSztNaSzuflade.Text = r["SztSzuflada"]?.ToString();
                                    cmbTypCeny.SelectedItem = r["TypCeny"]?.ToString();
                                    txtCena.Text = r["Cena"]?.ToString();
                                    cmbTypUmowy.SelectedItem = r["TypUmowy"]?.ToString();
                                    txtDodatek.Text = r["Dodatek"]?.ToString();

                                    chkPotwWaga.IsChecked = r["PotwWaga"] != DBNull.Value && Convert.ToBoolean(r["PotwWaga"]);
                                    chkPotwSztuki.IsChecked = r["PotwSztuki"] != DBNull.Value && Convert.ToBoolean(r["PotwSztuki"]);
                                    txtKtoWaga.Text = r["KtoWagaName"]?.ToString();
                                    txtKtoSztuki.Text = r["KtoSztukiName"]?.ToString();

                                    // Info
                                    txtDataStwo.Text = r["DataUtw"] != DBNull.Value ? Convert.ToDateTime(r["DataUtw"]).ToString("yyyy-MM-dd HH:mm") : "";
                                    txtKtoStwo.Text = r["KtoStwoName"]?.ToString();
                                    txtDataMod.Text = r["DataMod"] != DBNull.Value ? Convert.ToDateTime(r["DataMod"]).ToString("yyyy-MM-dd HH:mm") : "";
                                    txtKtoMod.Text = r["KtoModName"]?.ToString();

                                    // Transport
                                    txtSztNaSzufladeCalc.Text = r["SztSzuflada"]?.ToString();

                                    // Załadunek AVILOG - ustaw wszystkie 3 wiersze
                                    string sztSzuflada = r["SztSzuflada"]?.ToString();
                                    txtSztNaSzufladeWaga.Text = sztSzuflada; // Środkowy (podświetlony)
                                    if (int.TryParse(sztSzuflada, out int szt))
                                    {
                                        txtSztNaSzufladeWaga2.Text = szt > 1 ? (szt - 1).ToString() : szt.ToString(); // Górny = Szt - 1
                                        txtSztNaSzufladeWaga3.Text = (szt + 1).ToString(); // Dolny = Szt + 1
                                    }
                                    CalculateZaladunekRow1();
                                    CalculateZaladunekRow2();
                                    CalculateZaladunekRow3();
                                });
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private async Task LoadWstawieniaAsync(string lpWstawienia)
        {
            try
            {
                if (string.IsNullOrEmpty(lpWstawienia)) return;

                double sumaSztuk = 0;
                var tempList = new List<DostawaModel>();

                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);

                    // Dane wstawienia
                    string sql = "SELECT * FROM dbo.WstawieniaKurczakow WHERE Lp = @lp";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@lp", lpWstawienia);
                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync(_cts.Token))
                        {
                            if (await reader.ReadAsync(_cts.Token))
                            {
                                DateTime dataWstaw = Convert.ToDateTime(reader["DataWstawienia"]);
                                string iloscWst = reader["IloscWstawienia"]?.ToString();

                                await Dispatcher.InvokeAsync(() =>
                                {
                                    txtDataWstawienia.Text = dataWstaw.ToString("yyyy-MM-dd");
                                    txtSztukiWstawienia.Text = iloscWst;
                                    txtObecnaDoba.Text = (DateTime.Now - dataWstaw).Days.ToString();
                                });
                            }
                        }
                    }

                    // Powiązane dostawy
                    sql = "SELECT LP, DataOdbioru, Auta, SztukiDek, WagaDek, bufor FROM HarmonogramDostaw WHERE LpW = @lp ORDER BY DataOdbioru";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@lp", lpWstawienia);
                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync(_cts.Token))
                        {
                            while (await reader.ReadAsync(_cts.Token))
                            {
                                double sztuki = reader["SztukiDek"] != DBNull.Value ? Convert.ToDouble(reader["SztukiDek"]) : 0;
                                sumaSztuk += sztuki;

                                tempList.Add(new DostawaModel
                                {
                                    DataOdbioru = Convert.ToDateTime(reader["DataOdbioru"]),
                                    Auta = reader["Auta"] != DBNull.Value ? Convert.ToInt32(reader["Auta"]) : 0,
                                    SztukiDek = sztuki,
                                    WagaDek = reader["WagaDek"] != DBNull.Value ? Convert.ToDecimal(reader["WagaDek"]) : 0,
                                    Bufor = reader["bufor"]?.ToString()
                                });
                            }
                        }
                    }
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    _wstawienia.Clear();
                    foreach (var w in tempList)
                        _wstawienia.Add(w);

                    // Oblicz pozostałe
                    if (double.TryParse(txtSztukiWstawienia.Text, out double wstawione))
                    {
                        double pozostale = (wstawione * 0.97) - sumaSztuk;
                        txtSztukiPozostale.Text = $"{pozostale:#,0} szt";
                    }
                });
            }
            catch { }
        }

        private async void DgDostawy_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var dg = sender as DataGrid;
            if (dg == null) return;

            var selectedItem = dg.SelectedItem as DostawaModel;
            if (selectedItem == null || selectedItem.IsHeaderRow || selectedItem.IsSeparator) return;

            // Sprawdź którą kolumnę kliknięto
            var point = e.GetPosition(dg);
            var hitResult = VisualTreeHelper.HitTest(dg, point);
            if (hitResult == null) return;

            var cell = FindVisualParent<DataGridCell>(hitResult.VisualHit);
            if (cell == null) return;

            var column = cell.Column;
            if (column == null) return;

            string columnHeader = column.Header?.ToString() ?? "";

            // Określ z której tabeli pochodzi element
            bool isFromSecondTable = (dg == dgDostawyNastepny);

            // Obsługa edycji dla konkretnych kolumn (z obsługą emoji w nagłówkach)
            if (columnHeader == "🚛" || columnHeader.Contains("Auta"))
            {
                await EditCellValueAsync(selectedItem.LP, "A", isFromSecondTable);
            }
            else if (columnHeader == "🐔 Szt" || columnHeader.Contains("Szt"))
            {
                await EditCellValueAsync(selectedItem.LP, "Szt", isFromSecondTable);
            }
            else if (columnHeader == "⚖️ Waga" || columnHeader.Contains("Waga"))
            {
                await EditCellValueAsync(selectedItem.LP, "Waga", isFromSecondTable);
            }
            else if (columnHeader == "📊 Typ" || columnHeader.Contains("Typ"))
            {
                await EditTypCenyAsync(selectedItem, isFromSecondTable);
            }
            else if (columnHeader == "💰 Cena" || columnHeader.Contains("Cena"))
            {
                await EditCenaAsync(selectedItem, isFromSecondTable);
            }
            else if (columnHeader == "📝 Uwagi" || columnHeader.Contains("Uwagi"))
            {
                await EditNoteAsync(selectedItem.LP);
            }
        }

        private async Task EditCellValueAsync(string lp, string columnName, bool isFromSecondTable = false)
        {
            // Pobierz aktualną wartość z odpowiedniej kolekcji
            var collection = isFromSecondTable ? _dostawyNastepnyTydzien : _dostawy;
            var item = collection.FirstOrDefault(d => d.LP == lp);
            if (item == null) return;

            string currentValue = "";
            string fieldName = "";

            switch (columnName)
            {
                case "A":
                    currentValue = item.Auta.ToString();
                    fieldName = "Auta";
                    break;
                case "Szt":
                    currentValue = item.SztukiDek.ToString("0");
                    fieldName = "SztukiDek";
                    break;
                case "Waga":
                    currentValue = item.WagaDek.ToString("0.00").Replace(",", ".");
                    fieldName = "WagaDek";
                    break;
            }

            // Pokaż dialog edycji
            var dialog = new Window
            {
                Title = $"Edycja {columnName}",
                Width = 250,
                Height = 120,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var stack = new StackPanel { Margin = new Thickness(10) };
            var textBox = new TextBox { Text = currentValue, FontSize = 14, Margin = new Thickness(0, 0, 0, 10) };
            textBox.SelectAll();

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnOk = new Button { Content = "OK", Width = 60, Margin = new Thickness(0, 0, 5, 0) };
            var btnCancel = new Button { Content = "Anuluj", Width = 60 };

            btnOk.Click += async (s, e) =>
            {
                string newValue = textBox.Text.Trim().Replace(",", ".");
                try
                {
                    using (SqlConnection conn = new SqlConnection(ConnectionString))
                    {
                        await conn.OpenAsync();
                        string sql = $"UPDATE HarmonogramDostaw SET {fieldName} = @val, DataMod = GETDATE(), KtoMod = @kto WHERE LP = @lp";
                        using (SqlCommand cmd = new SqlCommand(sql, conn))
                        {
                            if (fieldName == "Auta")
                                cmd.Parameters.AddWithValue("@val", int.Parse(newValue));
                            else if (fieldName == "SztukiDek")
                                cmd.Parameters.AddWithValue("@val", double.Parse(newValue, CultureInfo.InvariantCulture));
                            else
                                cmd.Parameters.AddWithValue("@val", decimal.Parse(newValue, CultureInfo.InvariantCulture));
                            cmd.Parameters.AddWithValue("@kto", UserID ?? "0");
                            cmd.Parameters.AddWithValue("@lp", lp);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    // AUDIT LOG - logowanie zmiany
                    if (_auditService != null)
                    {
                        var source = columnName switch
                        {
                            "A" => AuditChangeSource.DoubleClick_Auta,
                            "Szt" => AuditChangeSource.DoubleClick_Sztuki,
                            "Waga" => AuditChangeSource.DoubleClick_Waga,
                            _ => AuditChangeSource.DoubleClick_Auta
                        };
                        await _auditService.LogFieldChangeAsync(
                            "HarmonogramDostaw", lp, source, fieldName,
                            currentValue, newValue,
                            new AuditContextInfo { Dostawca = item.Dostawca, DataOdbioru = item.DataOdbioru },
                            _cts.Token);
                    }

                    dialog.Close();
                    ShowToast($"{columnName} zaktualizowane", ToastType.Success);
                    await LoadDostawyAsync();
                }
                catch (Exception ex)
                {
                    ShowToast($"Błąd: {ex.Message}", ToastType.Error);
                }
            };

            btnCancel.Click += (s, e) => dialog.Close();

            textBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) btnOk.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (e.Key == Key.Escape) dialog.Close();
            };

            buttonPanel.Children.Add(btnOk);
            buttonPanel.Children.Add(btnCancel);
            stack.Children.Add(new TextBlock { Text = $"Podaj nową wartość dla {columnName}:", Margin = new Thickness(0, 0, 0, 5) });
            stack.Children.Add(textBox);
            stack.Children.Add(buttonPanel);
            dialog.Content = stack;

            textBox.Focus();
            dialog.ShowDialog();
        }

        private async Task EditNoteAsync(string lp)
        {
            // Pokaż dialog edycji notatki
            var dialog = new Window
            {
                Title = "Nowa notatka",
                Width = 350,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var stack = new StackPanel { Margin = new Thickness(10) };
            var textBox = new TextBox { Height = 60, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, FontSize = 12, Margin = new Thickness(0, 0, 0, 10) };

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnOk = new Button { Content = "Dodaj", Width = 60, Margin = new Thickness(0, 0, 5, 0) };
            var btnCancel = new Button { Content = "Anuluj", Width = 60 };

            btnOk.Click += async (s, e) =>
            {
                string noteText = textBox.Text.Trim();
                if (string.IsNullOrEmpty(noteText))
                {
                    ShowToast("Wpisz treść notatki", ToastType.Warning);
                    return;
                }

                if (MessageBox.Show("Czy na pewno chcesz dodać tę notatkę?", "Potwierdzenie",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (SqlConnection conn = new SqlConnection(ConnectionString))
                        {
                            await conn.OpenAsync();
                            string sql = "INSERT INTO Notatki (IndeksID, TypID, Tresc, KtoStworzyl, DataUtworzenia) VALUES (@lp, 1, @tresc, @kto, GETDATE())";
                            using (SqlCommand cmd = new SqlCommand(sql, conn))
                            {
                                cmd.Parameters.AddWithValue("@lp", lp);
                                cmd.Parameters.AddWithValue("@tresc", noteText);
                                cmd.Parameters.AddWithValue("@kto", UserID ?? "0");
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        // AUDIT LOG - logowanie dodania notatki
                        if (_auditService != null)
                        {
                            await _auditService.LogNoteAddedAsync(lp, noteText, AuditChangeSource.DoubleClick_Uwagi,
                                cancellationToken: _cts.Token);
                        }

                        dialog.Close();
                        ShowToast("Notatka dodana", ToastType.Success);
                        await LoadNotatkiAsync(lp);
                        await LoadOstatnieNotatkiAsync();
                        // Odśwież tabele dostaw
                        await LoadDostawyAsync();
                    }
                    catch (Exception ex)
                    {
                        ShowToast($"Błąd: {ex.Message}", ToastType.Error);
                    }
                }
            };

            btnCancel.Click += (s, e) => dialog.Close();

            buttonPanel.Children.Add(btnOk);
            buttonPanel.Children.Add(btnCancel);
            stack.Children.Add(new TextBlock { Text = "Wpisz treść notatki:", Margin = new Thickness(0, 0, 0, 5) });
            stack.Children.Add(textBox);
            stack.Children.Add(buttonPanel);
            dialog.Content = stack;

            textBox.Focus();
            dialog.ShowDialog();
        }

        private async Task EditTypCenyAsync(DostawaModel selectedItem, bool isFromSecondTable)
        {
            if (selectedItem == null || selectedItem.IsHeaderRow) return;

            // Lista typów cen
            var typyCeny = new[] { "wolnyrynek", "rolnicza", "łączona", "ministerialna" };

            // Pokaż dialog wyboru typu ceny
            var dialog = new Window
            {
                Title = "Zmień typ ceny",
                Width = 320,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(250, 250, 250))
            };

            var mainStack = new StackPanel { Margin = new Thickness(15) };

            // Nagłówek
            mainStack.Children.Add(new TextBlock
            {
                Text = $"Dostawa: {selectedItem.Dostawca}",
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 10)
            });

            // ComboBox z typami cen
            var comboBox = new ComboBox
            {
                ItemsSource = typyCeny,
                SelectedItem = selectedItem.TypCeny,
                FontSize = 13,
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 0, 0, 15)
            };
            mainStack.Children.Add(new TextBlock { Text = "Wybierz typ ceny:", Margin = new Thickness(0, 0, 0, 5), FontSize = 11 });
            mainStack.Children.Add(comboBox);

            // Checkbox - zmień wszystkie powiązane dostawy
            var checkBoxAll = new CheckBox
            {
                Content = "Zmień wszystkie dostawy z tego wstawienia",
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 15),
                IsEnabled = !string.IsNullOrEmpty(selectedItem.LpW)
            };
            if (string.IsNullOrEmpty(selectedItem.LpW))
            {
                checkBoxAll.ToolTip = "Ta dostawa nie ma przypisanego wstawienia";
            }
            mainStack.Children.Add(checkBoxAll);

            // Przyciski
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnOk = new Button
            {
                Content = "Zapisz",
                Width = 70,
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(0, 5, 0, 5),
                Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            var btnCancel = new Button
            {
                Content = "Anuluj",
                Width = 70,
                Padding = new Thickness(0, 5, 0, 5)
            };

            btnOk.Click += async (s, e) =>
            {
                string newTypCeny = comboBox.SelectedItem?.ToString();
                if (string.IsNullOrEmpty(newTypCeny) || newTypCeny == selectedItem.TypCeny)
                {
                    dialog.Close();
                    return;
                }

                bool changeAll = checkBoxAll.IsChecked == true && !string.IsNullOrEmpty(selectedItem.LpW);

                // Potwierdzenie
                string message = changeAll
                    ? $"Czy zmienić typ ceny na \"{newTypCeny}\" dla WSZYSTKICH dostaw z wstawienia {selectedItem.LpW}?"
                    : $"Czy zmienić typ ceny na \"{newTypCeny}\" dla tej dostawy?";

                if (MessageBox.Show(message, "Potwierdzenie zmiany typu ceny",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (SqlConnection conn = new SqlConnection(ConnectionString))
                        {
                            await conn.OpenAsync();

                            string sql;
                            if (changeAll)
                            {
                                // Zmień dla wszystkich dostaw z tego samego wstawienia
                                sql = "UPDATE HarmonogramDostaw SET TypCeny = @typCeny, DataMod = GETDATE(), KtoMod = @kto WHERE LpW = @lpW";
                                using (SqlCommand cmd = new SqlCommand(sql, conn))
                                {
                                    cmd.Parameters.AddWithValue("@typCeny", newTypCeny);
                                    cmd.Parameters.AddWithValue("@kto", UserID ?? "0");
                                    cmd.Parameters.AddWithValue("@lpW", selectedItem.LpW);
                                    int affected = await cmd.ExecuteNonQueryAsync();
                                    ShowToast($"Zmieniono typ ceny dla {affected} dostaw", ToastType.Success);
                                }
                            }
                            else
                            {
                                // Zmień tylko dla tej dostawy
                                sql = "UPDATE HarmonogramDostaw SET TypCeny = @typCeny, DataMod = GETDATE(), KtoMod = @kto WHERE LP = @lp";
                                using (SqlCommand cmd = new SqlCommand(sql, conn))
                                {
                                    cmd.Parameters.AddWithValue("@typCeny", newTypCeny);
                                    cmd.Parameters.AddWithValue("@kto", UserID ?? "0");
                                    cmd.Parameters.AddWithValue("@lp", selectedItem.LP);
                                    await cmd.ExecuteNonQueryAsync();
                                    ShowToast("Typ ceny zmieniony", ToastType.Success);
                                }
                            }
                        }

                        dialog.Close();
                        // Odśwież tabele dostaw
                        await LoadDostawyAsync();
                    }
                    catch (Exception ex)
                    {
                        ShowToast($"Błąd: {ex.Message}", ToastType.Error);
                    }
                }
            };

            btnCancel.Click += (s, e) => dialog.Close();

            buttonPanel.Children.Add(btnOk);
            buttonPanel.Children.Add(btnCancel);
            mainStack.Children.Add(buttonPanel);

            dialog.Content = mainStack;
            dialog.ShowDialog();
        }

        private async Task EditCenaAsync(DostawaModel selectedItem, bool isFromSecondTable)
        {
            if (selectedItem == null || selectedItem.IsHeaderRow) return;

            string currentValue = selectedItem.Cena.ToString("0.00").Replace(",", ".");

            // Pokaż dialog edycji ceny
            var dialog = new Window
            {
                Title = "Edycja ceny",
                Width = 250,
                Height = 120,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var stack = new StackPanel { Margin = new Thickness(10) };
            var textBox = new TextBox { Text = currentValue, FontSize = 14, Margin = new Thickness(0, 0, 0, 10) };
            textBox.SelectAll();

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnOk = new Button { Content = "OK", Width = 60, Margin = new Thickness(0, 0, 5, 0) };
            var btnCancel = new Button { Content = "Anuluj", Width = 60 };

            btnOk.Click += async (s, e) =>
            {
                string newValue = textBox.Text.Trim().Replace(",", ".");
                try
                {
                    using (SqlConnection conn = new SqlConnection(ConnectionString))
                    {
                        await conn.OpenAsync();
                        string sql = "UPDATE HarmonogramDostaw SET Cena = @val, DataMod = GETDATE(), KtoMod = @kto WHERE LP = @lp";
                        using (SqlCommand cmd = new SqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@val", decimal.Parse(newValue, CultureInfo.InvariantCulture));
                            cmd.Parameters.AddWithValue("@kto", UserID ?? "0");
                            cmd.Parameters.AddWithValue("@lp", selectedItem.LP);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    // AUDIT LOG - logowanie zmiany ceny
                    if (_auditService != null)
                    {
                        await _auditService.LogFieldChangeAsync(
                            "HarmonogramDostaw", selectedItem.LP, AuditChangeSource.DoubleClick_Cena, "Cena",
                            currentValue, newValue,
                            new AuditContextInfo { Dostawca = selectedItem.Dostawca, DataOdbioru = selectedItem.DataOdbioru },
                            _cts.Token);
                    }

                    dialog.Close();
                    ShowToast("Cena zaktualizowana", ToastType.Success);
                    await LoadDostawyAsync();
                }
                catch (Exception ex)
                {
                    ShowToast($"Błąd: {ex.Message}", ToastType.Error);
                }
            };

            btnCancel.Click += (s, e) => dialog.Close();

            textBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) btnOk.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (e.Key == Key.Escape) dialog.Close();
            };

            buttonPanel.Children.Add(btnOk);
            buttonPanel.Children.Add(btnCancel);
            stack.Children.Add(new TextBlock { Text = "Podaj nową wartość dla Cena:", Margin = new Thickness(0, 0, 0, 5) });
            stack.Children.Add(textBox);
            stack.Children.Add(buttonPanel);
            dialog.Content = stack;

            textBox.Focus();
            dialog.ShowDialog();
        }

        private void DgWstawienia_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var dg = sender as DataGrid;
            if (dg == null) return;

            var selectedItem = dg.SelectedItem as DostawaModel;
            if (selectedItem == null || selectedItem.IsHeaderRow) return;

            // Znajdź dostawę w głównych tabelach
            DostawaModel targetDostawa = null;
            DataGrid targetGrid = null;

            // Szukaj w bieżącym tygodniu
            targetDostawa = _dostawy.FirstOrDefault(d => d.LP == selectedItem.LP && !d.IsHeaderRow && !d.IsSeparator);
            if (targetDostawa != null)
            {
                targetGrid = dgDostawy;
            }
            else
            {
                // Szukaj w następnym tygodniu
                targetDostawa = _dostawyNastepnyTydzien.FirstOrDefault(d => d.LP == selectedItem.LP && !d.IsHeaderRow && !d.IsSeparator);
                if (targetDostawa != null)
                {
                    targetGrid = dgDostawyNastepny;
                }
            }

            // Jeśli znaleziono - przeskocz do tej dostawy i podświetl
            if (targetDostawa != null && targetGrid != null)
            {
                targetGrid.SelectedItem = targetDostawa;
                targetGrid.ScrollIntoView(targetDostawa);

                // Dodatkowe podświetlenie (pulsujące czarne)
                var row = targetGrid.ItemContainerGenerator.ContainerFromItem(targetDostawa) as DataGridRow;
                if (row != null)
                {
                    var originalBackground = row.Background;

                    // Animacja pulsująca czarnego koloru - bardzo powolna
                    var brush = new SolidColorBrush(Color.FromRgb(40, 40, 40)); // Czarny
                    row.Background = brush;
                    row.Foreground = Brushes.White;

                    var pulseAnimation = new ColorAnimation
                    {
                        From = Color.FromRgb(40, 40, 40), // Czarny
                        To = Color.FromRgb(80, 80, 80),   // Ciemnoszary
                        Duration = TimeSpan.FromSeconds(2), // Bardzo powolna pulsacja
                        AutoReverse = true,
                        RepeatBehavior = new RepeatBehavior(3) // 3 pełne cykle pulsacji
                    };

                    pulseAnimation.Completed += (s, args) =>
                    {
                        row.Background = originalBackground;
                        row.Foreground = new SolidColorBrush(Color.FromRgb(33, 33, 33));
                    };

                    brush.BeginAnimation(SolidColorBrush.ColorProperty, pulseAnimation);
                }

                ShowToast($"Przejście do dostawy: {targetDostawa.Dostawca}", ToastType.Info);
            }
            else
            {
                // Jeśli nie znaleziono - zmień tydzień na ten, w którym jest dostawa
                _selectedDate = selectedItem.DataOdbioru.Date;
                calendarMain.SelectedDate = _selectedDate;
                calendarMain.DisplayDate = _selectedDate;
                UpdateWeekNumber();
                _ = LoadDostawyAsync();

                ShowToast($"Zmiana tygodnia na {_selectedDate:dd.MM}", ToastType.Info);
            }
        }

        private void DgDostawy_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            var dostawa = e.Row.DataContext as DostawaModel;
            if (dostawa == null) return;

            // Reset do domyślnych wartości
            e.Row.Background = Brushes.White;
            e.Row.Foreground = new SolidColorBrush(Color.FromRgb(33, 33, 33));
            e.Row.FontWeight = FontWeights.Normal;
            e.Row.FontSize = 12;
            e.Row.Height = Double.NaN; // Auto
            e.Row.MinHeight = 0;
            e.Row.BorderThickness = new Thickness(0);
            e.Row.Resources.Clear(); // Usuń style przekreślenia

            if (dostawa.IsSeparator)
            {
                e.Row.Height = 4;
                e.Row.MinHeight = 4;
                e.Row.Background = new SolidColorBrush(Color.FromRgb(224, 224, 224));
                e.Row.IsEnabled = false;
                return;
            }

            if (dostawa.IsHeaderRow)
            {
                bool isToday = dostawa.DataOdbioru.Date == DateTime.Today;
                bool isPast = dostawa.DataOdbioru.Date < DateTime.Today;

                // DZISIEJSZY DZIEŃ - ZAWSZE PULSUJE (nawet bez dostaw)
                if (isToday)
                {
                    e.Row.FontWeight = FontWeights.Bold;
                    e.Row.FontSize = 13;
                    e.Row.Height = 30;
                    e.Row.MinHeight = 30;
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(255, 152, 0)); // Orange
                    e.Row.Foreground = Brushes.White;
                    e.Row.BorderBrush = new SolidColorBrush(Color.FromRgb(230, 81, 0)); // Dark Orange border
                    e.Row.BorderThickness = new Thickness(0, 2, 0, 2);

                    // Jeśli pusty dzień - dodaj przekreślenie ale ZACHOWAJ pomarańczowy styl
                    if (dostawa.IsEmptyDay)
                    {
                        var style = new Style(typeof(TextBlock));
                        style.Setters.Add(new Setter(TextBlock.TextDecorationsProperty, TextDecorations.Strikethrough));
                        style.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brushes.White));
                        e.Row.Resources[typeof(TextBlock)] = style;
                    }

                    // Pulsujące podświetlenie (glow) dla dzisiejszego dnia - ZAWSZE
                    var glowEffect = new DropShadowEffect
                    {
                        Color = Color.FromRgb(255, 152, 0),
                        BlurRadius = 10,
                        ShadowDepth = 0,
                        Opacity = 0.5
                    };
                    e.Row.Effect = glowEffect;

                    // Animacja pulsowania
                    var pulseAnimation = new DoubleAnimation
                    {
                        From = 0.3,
                        To = 0.8,
                        Duration = TimeSpan.FromMilliseconds(1000),
                        AutoReverse = true,
                        RepeatBehavior = RepeatBehavior.Forever
                    };
                    var blurAnimation = new DoubleAnimation
                    {
                        From = 8,
                        To = 18,
                        Duration = TimeSpan.FromMilliseconds(1000),
                        AutoReverse = true,
                        RepeatBehavior = RepeatBehavior.Forever
                    };
                    glowEffect.BeginAnimation(DropShadowEffect.OpacityProperty, pulseAnimation);
                    glowEffect.BeginAnimation(DropShadowEffect.BlurRadiusProperty, blurAnimation);
                    return;
                }

                // PUSTY DZIEŃ (nie dzisiaj) - mała czcionka, przekreślony
                if (dostawa.IsEmptyDay)
                {
                    e.Row.FontWeight = FontWeights.Normal;
                    e.Row.FontSize = 9;
                    e.Row.Height = 16;
                    e.Row.MinHeight = 16;
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(245, 245, 245));
                    e.Row.Foreground = Brushes.Black;
                    var style = new Style(typeof(TextBlock));
                    style.Setters.Add(new Setter(TextBlock.TextDecorationsProperty, TextDecorations.Strikethrough));
                    e.Row.Resources[typeof(TextBlock)] = style;
                    return;
                }

                // DZIEŃ Z DOSTAWAMI (nie dzisiaj)
                e.Row.FontWeight = FontWeights.Bold;
                e.Row.FontSize = 12;
                e.Row.Height = 26;
                e.Row.MinHeight = 26;

                if (isPast)
                {
                    // Przeszły dzień - czarny z białą czcionką
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(0, 0, 0));
                    e.Row.Foreground = Brushes.White;
                }
                else
                {
                    // Przyszły dzień - ciemnozielony
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(46, 125, 50));
                    e.Row.Foreground = Brushes.White;
                }
                return;
            }

            // Zwykły wiersz danych - ustaw standardową wysokość
            e.Row.Height = 22;
            e.Row.MinHeight = 22;

            switch (dostawa.Bufor)
            {
                case "Potwierdzony":
                    e.Row.Background = (SolidColorBrush)FindResource("StatusPotwierdzonyBrush");
                    e.Row.FontWeight = FontWeights.SemiBold;
                    break;
                case "Anulowany":
                    e.Row.Background = (SolidColorBrush)FindResource("StatusAnulowanyBrush");
                    break;
                case "Sprzedany":
                    e.Row.Background = (SolidColorBrush)FindResource("StatusSprzedanyBrush");
                    break;
                case "B.Kontr.":
                    e.Row.Background = (SolidColorBrush)FindResource("StatusBKontrBrush");
                    e.Row.Foreground = Brushes.White;
                    break;
                case "B.Wolny.":
                    e.Row.Background = (SolidColorBrush)FindResource("StatusBWolnyBrush");
                    break;
                case "Do Wykupienia":
                case "Do wykupienia":
                    e.Row.Background = (SolidColorBrush)FindResource("StatusDoWykupieniaBrush");
                    break;
            }
        }

        private void LoadDeliveryDetails(string lp)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();
                    string sql = @"SELECT HD.*, D.Address, D.PostalCode, D.City, D.Distance, D.Phone1, D.Phone2, D.Phone3,
                                   D.Info1, D.Info2, D.Info3, D.Email, D.TypOsobowosci, D.TypOsobowosci2,
                                   O1.Name as KtoStwoName, O2.Name as KtoModName, O3.Name as KtoWagaName, O4.Name as KtoSztukiName
                                   FROM HarmonogramDostaw HD
                                   LEFT JOIN Dostawcy D ON HD.Dostawca = D.Name
                                   LEFT JOIN operators O1 ON HD.ktoStwo = O1.ID
                                   LEFT JOIN operators O2 ON HD.ktoMod = O2.ID
                                   LEFT JOIN operators O3 ON HD.KtoWaga = O3.ID
                                   LEFT JOIN operators O4 ON HD.KtoSztuki = O4.ID
                                   WHERE HD.LP = @lp";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@lp", lp);
                        using (SqlDataReader r = cmd.ExecuteReader())
                        {
                            if (r.Read())
                            {
                                // Hodowca
                                cmbDostawca.SelectedItem = r["Dostawca"]?.ToString();
                                txtUlicaH.Text = r["Address"]?.ToString();
                                txtKodPocztowyH.Text = r["PostalCode"]?.ToString();
                                txtMiejscH.Text = r["City"]?.ToString();
                                txtKmH.Text = r["Distance"]?.ToString();
                                txtEmail.Text = r["Email"]?.ToString();
                                txtTel1.Text = r["Phone1"]?.ToString();
                                txtTel2.Text = r["Phone2"]?.ToString();
                                txtTel3.Text = r["Phone3"]?.ToString();
                                txtInfo1.Text = r["Info1"]?.ToString();
                                txtInfo2.Text = r["Info2"]?.ToString();
                                txtInfo3.Text = r["Info3"]?.ToString();
                                cmbOsobowosc1.SelectedItem = r["TypOsobowosci"]?.ToString();
                                cmbOsobowosc2.SelectedItem = r["TypOsobowosci2"]?.ToString();

                                // Dostawa
                                dpData.SelectedDate = r["DataOdbioru"] != DBNull.Value ? Convert.ToDateTime(r["DataOdbioru"]) : (DateTime?)null;
                                cmbStatus.SelectedItem = r["bufor"]?.ToString();
                                CmbStatus_SelectionChanged(null, null); // Aktualizuj kolor
                                txtAuta.Text = r["Auta"]?.ToString();
                                txtSztuki.Text = r["SztukiDek"]?.ToString();
                                txtWagaDek.Text = r["WagaDek"]?.ToString();
                                txtSztNaSzuflade.Text = r["SztSzuflada"]?.ToString();
                                cmbTypCeny.SelectedItem = r["TypCeny"]?.ToString();
                                txtCena.Text = r["Cena"]?.ToString();
                                cmbTypUmowy.SelectedItem = r["TypUmowy"]?.ToString();
                                txtDodatek.Text = r["Dodatek"]?.ToString();

                                chkPotwWaga.IsChecked = r["PotwWaga"] != DBNull.Value && Convert.ToBoolean(r["PotwWaga"]);
                                chkPotwSztuki.IsChecked = r["PotwSztuki"] != DBNull.Value && Convert.ToBoolean(r["PotwSztuki"]);
                                txtKtoWaga.Text = r["KtoWagaName"]?.ToString();
                                txtKtoSztuki.Text = r["KtoSztukiName"]?.ToString();

                                // Info
                                txtDataStwo.Text = r["DataUtw"] != DBNull.Value ? Convert.ToDateTime(r["DataUtw"]).ToString("yyyy-MM-dd HH:mm") : "";
                                txtKtoStwo.Text = r["KtoStwoName"]?.ToString();
                                txtDataMod.Text = r["DataMod"] != DBNull.Value ? Convert.ToDateTime(r["DataMod"]).ToString("yyyy-MM-dd HH:mm") : "";
                                txtKtoMod.Text = r["KtoModName"]?.ToString();

                                // Transport
                                txtSztNaSzufladeCalc.Text = r["SztSzuflada"]?.ToString();

                                // Załadunek AVILOG - ustaw Szt na szufladę i przelicz
                                string sztSzuflada = r["SztSzuflada"]?.ToString();
                                txtSztNaSzufladeWaga.Text = sztSzuflada; // Środkowy (podświetlony)
                                if (int.TryParse(sztSzuflada, out int szt))
                                {
                                    txtSztNaSzufladeWaga2.Text = szt > 1 ? (szt - 1).ToString() : szt.ToString(); // Górny = Szt - 1
                                    txtSztNaSzufladeWaga3.Text = (szt + 1).ToString(); // Dolny = Szt + 1
                                }
                                CalculateZaladunekRow1();
                                CalculateZaladunekRow2();
                                CalculateZaladunekRow3();
                            }
                        }
                    }
                }
            }
            catch { }
        }

        #endregion

        #region Obsługa Checkboxów

        private async void ChkConfirm_Click(object sender, RoutedEventArgs e)
        {
            var checkbox = sender as CheckBox;
            if (checkbox == null) return;

            string lp = checkbox.Tag?.ToString();
            if (string.IsNullOrEmpty(lp)) return;

            bool isChecked = checkbox.IsChecked == true;
            string status = isChecked ? "Potwierdzony" : "Niepotwierdzony";
            string oldStatus = isChecked ? "Niepotwierdzony" : "Potwierdzony";

            // Pobierz info o dostawie
            var dostawa = _dostawy.FirstOrDefault(d => d.LP == lp) ?? _dostawyNastepnyTydzien.FirstOrDefault(d => d.LP == lp);

            // TRYB SYMULACJI - tylko zmiana w pamięci
            if (_isSimulationMode)
            {
                if (dostawa != null)
                {
                    dostawa.Bufor = status;
                }
                ShowToast(isChecked ? "📝 Potwierdzono (symulacja)" : "📝 Cofnięto potwierdzenie (symulacja)", ToastType.Info);
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    using (SqlCommand cmd = new SqlCommand("UPDATE HarmonogramDostaw SET bufor = @status WHERE LP = @lp", conn))
                    {
                        cmd.Parameters.AddWithValue("@status", status);
                        cmd.Parameters.AddWithValue("@lp", lp);
                        await cmd.ExecuteNonQueryAsync(_cts.Token);
                    }
                }

                // AUDIT LOG - logowanie zmiany statusu
                if (_auditService != null)
                {
                    await _auditService.LogStatusChangeAsync(lp, oldStatus, status,
                        AuditChangeSource.Checkbox_Potwierdzenie,
                        dostawa?.Dostawca, dostawa?.DataOdbioru, _cts.Token);
                }

                ShowToast(isChecked ? "Dostawa potwierdzona" : "Potwierdzenie usunięte", ToastType.Success);
                await LoadDostawyAsync();
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd: {ex.Message}", ToastType.Error);
                // Przywróć poprzedni stan
                checkbox.IsChecked = !isChecked;
            }
        }

        private async void ChkWstawienie_Click(object sender, RoutedEventArgs e)
        {
            var checkbox = sender as CheckBox;
            if (checkbox == null) return;

            string lpW = checkbox.Tag?.ToString();
            if (string.IsNullOrEmpty(lpW)) return;

            bool isChecked = checkbox.IsChecked == true;

            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    using (SqlCommand cmd = new SqlCommand(
                        "UPDATE WstawieniaKurczakow SET isConf = @isConf, KtoConf = @kto, DataConf = @data WHERE Lp = @lp", conn))
                    {
                        cmd.Parameters.AddWithValue("@isConf", isChecked ? 1 : 0);
                        cmd.Parameters.AddWithValue("@kto", UserID ?? "0");
                        cmd.Parameters.AddWithValue("@data", DateTime.Now);
                        cmd.Parameters.AddWithValue("@lp", lpW);
                        await cmd.ExecuteNonQueryAsync(_cts.Token);
                    }
                }

                // AUDIT LOG - logowanie zmiany potwierdzenia wstawienia
                if (_auditService != null)
                {
                    await _auditService.LogWstawienieConfirmationAsync(lpW, isChecked, _cts.Token);
                }

                ShowToast(isChecked ? "Wstawienie potwierdzone" : "Potwierdzenie wstawienia usunięte", ToastType.Success);
                await LoadDostawyAsync();
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd: {ex.Message}", ToastType.Error);
                // Przywróć poprzedni stan
                checkbox.IsChecked = !isChecked;
            }
        }

        #endregion

        #region Akcje na dostawach

        private async void BtnDateUp_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedLP))
            {
                ShowToast("Wybierz dostawę", ToastType.Warning);
                return;
            }

            // TRYB SYMULACJI - tylko zmiana lokalna
            if (_isSimulationMode)
            {
                SimulationChangeDate(_selectedLP, 1);
                return;
            }

            await ChangeDeliveryDateAsync(_selectedLP, 1);
            await LoadDostawyAsync();
        }

        private async void BtnDateDown_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedLP))
            {
                ShowToast("Wybierz dostawę", ToastType.Warning);
                return;
            }

            // TRYB SYMULACJI - tylko zmiana lokalna
            if (_isSimulationMode)
            {
                SimulationChangeDate(_selectedLP, -1);
                return;
            }

            await ChangeDeliveryDateAsync(_selectedLP, -1);
            await LoadDostawyAsync();
        }

        /// <summary>
        /// Zmienia datę dostawy tylko w pamięci (tryb symulacji)
        /// </summary>
        private void SimulationChangeDate(string lp, int days)
        {
            var dostawa = _dostawy.FirstOrDefault(d => d.LP == lp) ??
                          _dostawyNastepnyTydzien.FirstOrDefault(d => d.LP == lp);

            if (dostawa == null) return;

            DateTime oldDate = dostawa.DataOdbioru;
            DateTime newDate = oldDate.AddDays(days);

            MoveDeliveryToDate(lp, newDate);
        }

        /// <summary>
        /// Zapisuje zmiany w dostawie tylko w pamięci (tryb symulacji)
        /// </summary>
        private void SimulationSaveDelivery(DostawaModel dostawa)
        {
            if (dostawa == null)
            {
                ShowToast("Nie znaleziono dostawy", ToastType.Warning);
                return;
            }

            // Aktualizuj właściwości z formularza
            if (dpData.SelectedDate.HasValue)
            {
                DateTime oldDate = dostawa.DataOdbioru;
                DateTime newDate = dpData.SelectedDate.Value;
                if (oldDate.Date != newDate.Date)
                {
                    dostawa.DataOdbioru = newDate;
                }
            }

            if (cmbDostawca.SelectedItem != null)
                dostawa.Dostawca = cmbDostawca.SelectedItem.ToString();

            if (int.TryParse(txtAuta.Text, out int auta))
                dostawa.Auta = auta;

            if (int.TryParse(txtSztuki.Text, out int sztuki))
                dostawa.SztukiDek = sztuki;

            if (decimal.TryParse(txtWagaDek.Text.Replace(",", "."), System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture, out decimal waga))
                dostawa.WagaDek = waga;

            if (cmbTypCeny.SelectedItem != null)
                dostawa.TypCeny = cmbTypCeny.SelectedItem.ToString();

            if (decimal.TryParse(txtCena.Text.Replace(",", "."), System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture, out decimal cena))
                dostawa.Cena = cena;

            if (cmbStatus.SelectedItem != null)
                dostawa.Bufor = cmbStatus.SelectedItem.ToString();

            // Odśwież widok
            RefreshDostawyView();

            ShowToast("📝 Zmiany zapisane w symulacji", ToastType.Info);
        }

        /// <summary>
        /// Duplikuje dostawę tylko w pamięci (tryb symulacji)
        /// </summary>
        private void SimulationDuplicateDelivery(string lp)
        {
            var dostawa = _dostawy.FirstOrDefault(d => d.LP == lp) ??
                          _dostawyNastepnyTydzien.FirstOrDefault(d => d.LP == lp);

            if (dostawa == null)
            {
                ShowToast("Nie znaleziono dostawy", ToastType.Warning);
                return;
            }

            // Utwórz kopię z nowym LP (symulacyjnym)
            var copy = CloneDostawaModel(dostawa);
            copy.LP = $"SIM_{DateTime.Now.Ticks}"; // Unikalne LP dla symulacji

            // Dodaj do odpowiedniej kolekcji
            if (_dostawy.Any(d => d.LP == lp))
            {
                _dostawy.Add(copy);
            }
            else
            {
                _dostawyNastepnyTydzien.Add(copy);
            }

            // Odśwież widok
            RefreshDostawyView();

            ShowToast($"📋 Zduplikowano: {dostawa.Dostawca}", ToastType.Info);
        }

        private async Task ChangeDeliveryDateAsync(string lp, int days)
        {
            // Pobierz info o dostawie dla audytu
            var dostawa = _dostawy.FirstOrDefault(d => d.LP == lp) ?? _dostawyNastepnyTydzien.FirstOrDefault(d => d.LP == lp);
            DateTime? oldDate = dostawa?.DataOdbioru;

            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    string sql = "UPDATE HarmonogramDostaw SET DataOdbioru = DATEADD(day, @dni, DataOdbioru) WHERE LP = @lp";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@dni", days);
                        cmd.Parameters.AddWithValue("@lp", lp);
                        await cmd.ExecuteNonQueryAsync(_cts.Token);
                    }
                }

                // AUDIT LOG - logowanie zmiany daty
                if (_auditService != null && oldDate.HasValue)
                {
                    DateTime newDate = oldDate.Value.AddDays(days);
                    var source = days > 0 ? AuditChangeSource.Button_DataUp : AuditChangeSource.Button_DataDown;
                    await _auditService.LogDateChangeAsync(lp, oldDate, newDate, source,
                        dostawa?.Dostawca, _cts.Token);
                }

                ShowToast($"Data przesunięta o {days} dni", ToastType.Success);
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd: {ex.Message}", ToastType.Error);
            }
        }

        // Stara synchroniczna wersja dla kompatybilności
        private void ChangeDeliveryDate(string lp, int days)
        {
            _ = ChangeDeliveryDateAsync(lp, days);
        }

        private void BtnNowaDostawa_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dostawa = new Dostawa("", _selectedDate);
                dostawa.UserID = App.UserID;
                dostawa.FormClosed += (s, args) => LoadDostawy();
                dostawa.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MenuItemNowaDostawaZDaty_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Pobierz zaznaczony element z odpowiedniej tabeli
                var selectedItem = dgDostawy.SelectedItem as DostawaModel ?? dgDostawyNastepny.SelectedItem as DostawaModel;

                DateTime dateToUse = _selectedDate;

                if (selectedItem != null && !selectedItem.IsSeparator && !selectedItem.IsHeaderRow)
                {
                    // Użyj daty z zaznaczonego wiersza
                    dateToUse = selectedItem.DataOdbioru;
                }

                var dostawa = new Dostawa("", dateToUse);
                dostawa.UserID = App.UserID;
                dostawa.FormClosed += (s, args) => LoadDostawy();
                dostawa.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Menu kontekstowe - Potwierdzenie WAGI
        private async void MenuPotwierdzWage_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedLP))
            {
                ShowToast("Wybierz dostawę", ToastType.Warning);
                return;
            }

            var dostawa = _dostawy.FirstOrDefault(d => d.LP == _selectedLP) ?? _dostawyNastepnyTydzien.FirstOrDefault(d => d.LP == _selectedLP);

            // TRYB SYMULACJI - tylko zmiana w UI, bez bazy i logów
            if (_isSimulationMode)
            {
                if (dostawa != null) dostawa.PotwWaga = true;
                chkPotwWaga.IsChecked = true;
                borderPotwWaga.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C8E6C9"));
                txtKtoWaga.Text = $"({UserName})";
                ShowToast("📝 Waga potwierdzona (symulacja)", ToastType.Info);
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    string sql = "UPDATE HarmonogramDostaw SET PotwWaga = 1, WagaKto = @User WHERE Lp = @Lp";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Lp", _selectedLP);
                        cmd.Parameters.AddWithValue("@User", UserName ?? "System");
                        await cmd.ExecuteNonQueryAsync(_cts.Token);
                    }
                }

                chkPotwWaga.IsChecked = true;
                borderPotwWaga.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C8E6C9"));
                txtKtoWaga.Text = $"({UserName})";
                ShowToast("✅ Waga potwierdzona!", ToastType.Success);

                // Audit log
                if (_auditService != null)
                {
                    await _auditService.LogFieldChangeAsync("HarmonogramDostaw", _selectedLP,
                        AuditChangeSource.ContextMenu_PotwierdzWage, "PotwWaga", "0", "1",
                        new AuditContextInfo { Dostawca = dostawa?.Dostawca, DataOdbioru = dostawa?.DataOdbioru }, _cts.Token);
                }
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd: {ex.Message}", ToastType.Error);
            }
        }

        // Menu kontekstowe - Potwierdzenie SZTUK
        private async void MenuPotwierdzSztuki_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedLP))
            {
                ShowToast("Wybierz dostawę", ToastType.Warning);
                return;
            }

            var dostawa = _dostawy.FirstOrDefault(d => d.LP == _selectedLP) ?? _dostawyNastepnyTydzien.FirstOrDefault(d => d.LP == _selectedLP);

            // TRYB SYMULACJI - tylko zmiana w UI, bez bazy i logów
            if (_isSimulationMode)
            {
                if (dostawa != null) dostawa.PotwSztuki = true;
                chkPotwSztuki.IsChecked = true;
                borderPotwSztuki.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C8E6C9"));
                txtKtoSztuki.Text = $"({UserName})";
                ShowToast("📝 Sztuki potwierdzone (symulacja)", ToastType.Info);
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    string sql = "UPDATE HarmonogramDostaw SET PotwSztuki = 1, SztukiKto = @User WHERE Lp = @Lp";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Lp", _selectedLP);
                        cmd.Parameters.AddWithValue("@User", UserName ?? "System");
                        await cmd.ExecuteNonQueryAsync(_cts.Token);
                    }
                }

                chkPotwSztuki.IsChecked = true;
                borderPotwSztuki.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C8E6C9"));
                txtKtoSztuki.Text = $"({UserName})";
                ShowToast("✅ Sztuki potwierdzone!", ToastType.Success);

                // Audit log
                if (_auditService != null)
                {
                    await _auditService.LogFieldChangeAsync("HarmonogramDostaw", _selectedLP,
                        AuditChangeSource.ContextMenu_PotwierdzSztuki, "PotwSztuki", "0", "1",
                        new AuditContextInfo { Dostawca = dostawa?.Dostawca, DataOdbioru = dostawa?.DataOdbioru }, _cts.Token);
                }
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd: {ex.Message}", ToastType.Error);
            }
        }

        // Menu kontekstowe - Cofnij potwierdzenie WAGI
        private async void MenuCofnijWage_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedLP))
            {
                ShowToast("Wybierz dostawę", ToastType.Warning);
                return;
            }

            var dostawa = _dostawy.FirstOrDefault(d => d.LP == _selectedLP) ?? _dostawyNastepnyTydzien.FirstOrDefault(d => d.LP == _selectedLP);

            // TRYB SYMULACJI - tylko zmiana w UI, bez bazy i logów
            if (_isSimulationMode)
            {
                if (dostawa != null) dostawa.PotwWaga = false;
                chkPotwWaga.IsChecked = false;
                borderPotwWaga.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCDD2"));
                txtKtoWaga.Text = "";
                ShowToast("📝 Cofnięto potwierdzenie wagi (symulacja)", ToastType.Info);
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    string sql = "UPDATE HarmonogramDostaw SET PotwWaga = 0, WagaKto = NULL WHERE Lp = @Lp";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Lp", _selectedLP);
                        await cmd.ExecuteNonQueryAsync(_cts.Token);
                    }
                }

                chkPotwWaga.IsChecked = false;
                borderPotwWaga.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCDD2"));
                txtKtoWaga.Text = "";
                ShowToast("↩️ Cofnięto potwierdzenie wagi", ToastType.Info);

                // Audit log
                if (_auditService != null)
                {
                    await _auditService.LogFieldChangeAsync("HarmonogramDostaw", _selectedLP,
                        AuditChangeSource.ContextMenu_CofnijWage, "PotwWaga", "1", "0",
                        new AuditContextInfo { Dostawca = dostawa?.Dostawca, DataOdbioru = dostawa?.DataOdbioru }, _cts.Token);
                }
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd: {ex.Message}", ToastType.Error);
            }
        }

        // Menu kontekstowe - Cofnij potwierdzenie SZTUK
        private async void MenuCofnijSztuki_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedLP))
            {
                ShowToast("Wybierz dostawę", ToastType.Warning);
                return;
            }

            var dostawa = _dostawy.FirstOrDefault(d => d.LP == _selectedLP) ?? _dostawyNastepnyTydzien.FirstOrDefault(d => d.LP == _selectedLP);

            // TRYB SYMULACJI - tylko zmiana w UI, bez bazy i logów
            if (_isSimulationMode)
            {
                if (dostawa != null) dostawa.PotwSztuki = false;
                chkPotwSztuki.IsChecked = false;
                borderPotwSztuki.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCDD2"));
                txtKtoSztuki.Text = "";
                ShowToast("📝 Cofnięto potwierdzenie sztuk (symulacja)", ToastType.Info);
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    string sql = "UPDATE HarmonogramDostaw SET PotwSztuki = 0, SztukiKto = NULL WHERE Lp = @Lp";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Lp", _selectedLP);
                        await cmd.ExecuteNonQueryAsync(_cts.Token);
                    }
                }

                chkPotwSztuki.IsChecked = false;
                borderPotwSztuki.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCDD2"));
                txtKtoSztuki.Text = "";
                ShowToast("↩️ Cofnięto potwierdzenie sztuk", ToastType.Info);

                // Audit log
                if (_auditService != null)
                {
                    await _auditService.LogFieldChangeAsync("HarmonogramDostaw", _selectedLP,
                        AuditChangeSource.ContextMenu_CofnijSztuki, "PotwSztuki", "1", "0",
                        new AuditContextInfo { Dostawca = dostawa?.Dostawca, DataOdbioru = dostawa?.DataOdbioru }, _cts.Token);
                }
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd: {ex.Message}", ToastType.Error);
            }
        }

        private async void BtnDuplikuj_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedLP))
            {
                ShowToast("Wybierz dostawę", ToastType.Warning);
                return;
            }

            if (MessageBox.Show("Czy na pewno chcesz zduplikować tę dostawę?", "Potwierdzenie",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                // TRYB SYMULACJI - duplikuj tylko w pamięci
                if (_isSimulationMode)
                {
                    SimulationDuplicateDelivery(_selectedLP);
                    return;
                }

                await DuplicateDeliveryAsync(_selectedLP);
                await LoadDostawyAsync();
            }
        }

        private async Task DuplicateDeliveryAsync(string lp)
        {
            // Pobierz info o dostawie dla audytu
            var dostawa = _dostawy.FirstOrDefault(d => d.LP == lp) ?? _dostawyNastepnyTydzien.FirstOrDefault(d => d.LP == lp);

            try
            {
                int newLp;
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    string getMaxLp = "SELECT MAX(Lp) FROM HarmonogramDostaw";
                    using (SqlCommand cmd = new SqlCommand(getMaxLp, conn))
                    {
                        var result = await cmd.ExecuteScalarAsync(_cts.Token);
                        newLp = Convert.ToInt32(result) + 1;
                    }

                    string sql = @"INSERT INTO HarmonogramDostaw (Lp, DataOdbioru, Dostawca, KmH, Kurnik, KmK, Auta, SztukiDek, WagaDek,
                                   SztSzuflada, TypUmowy, TypCeny, Cena, Bufor, UWAGI, Dodatek, DataUtw, LpW, Ubytek, ktoStwo)
                                   SELECT @newLp, DataOdbioru, Dostawca, KmH, Kurnik, KmK, Auta, SztukiDek, WagaDek,
                                   SztSzuflada, TypUmowy, TypCeny, Cena, Bufor, UWAGI, Dodatek, GETDATE(), LpW, Ubytek, @userId
                                   FROM HarmonogramDostaw WHERE Lp = @lp";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@newLp", newLp);
                        cmd.Parameters.AddWithValue("@lp", lp);
                        cmd.Parameters.AddWithValue("@userId", UserID ?? "0");
                        await cmd.ExecuteNonQueryAsync(_cts.Token);
                    }

                    // AUDIT LOG - logowanie duplikacji
                    if (_auditService != null)
                    {
                        await _auditService.LogDuplicateAsync(lp, newLp.ToString(),
                            dostawa?.Dostawca, dostawa?.DataOdbioru, _cts.Token);
                    }

                    ShowToast("Dostawa zduplikowana", ToastType.Success);
                }
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd: {ex.Message}", ToastType.Error);
            }
        }

        // Stara synchroniczna wersja dla kompatybilności
        private void DuplicateDelivery(string lp)
        {
            _ = DuplicateDeliveryAsync(lp);
        }

        private async void BtnUsun_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedLP))
            {
                ShowToast("Wybierz dostawę", ToastType.Warning);
                return;
            }

            // TRYB SYMULACJI - blokuj
            if (_isSimulationMode)
            {
                ShowToast("⚠️ Tryb symulacji - akcja zablokowana", ToastType.Warning);
                return;
            }

            // Pobierz info o dostawie PRZED usunięciem dla audytu
            var dostawa = _dostawy.FirstOrDefault(d => d.LP == _selectedLP) ?? _dostawyNastepnyTydzien.FirstOrDefault(d => d.LP == _selectedLP);

            if (MessageBox.Show("Czy na pewno chcesz usunąć tę dostawę? Nie lepiej anulować?", "Potwierdzenie",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    string deletedLP = _selectedLP;
                    using (SqlConnection conn = new SqlConnection(ConnectionString))
                    {
                        await conn.OpenAsync(_cts.Token);
                        using (SqlCommand cmd = new SqlCommand("DELETE FROM HarmonogramDostaw WHERE Lp = @lp", conn))
                        {
                            cmd.Parameters.AddWithValue("@lp", _selectedLP);
                            await cmd.ExecuteNonQueryAsync(_cts.Token);
                        }
                    }

                    // AUDIT LOG - logowanie usunięcia dostawy
                    if (_auditService != null)
                    {
                        await _auditService.LogDeliveryDeleteAsync(deletedLP, dostawa?.Dostawca,
                            dostawa?.DataOdbioru, dostawa?.Auta, dostawa?.SztukiDek, _cts.Token);
                    }

                    ShowToast("Dostawa usunięta", ToastType.Success);
                    _selectedLP = null;
                    await LoadDostawyAsync();
                }
                catch (Exception ex)
                {
                    ShowToast($"Błąd: {ex.Message}", ToastType.Error);
                }
            }
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (_isSimulationMode)
            {
                ShowToast("⚠️ Tryb symulacji - odświeżanie wyłączone", ToastType.Warning);
                return;
            }
            await LoadAllDataAsync();
            ShowToast("Dane odświeżone", ToastType.Success);
        }

        #region Tryb Symulacji

        /// <summary>
        /// Włącza/wyłącza tryb symulacji
        /// </summary>
        private void BtnSimulation_Click(object sender, RoutedEventArgs e)
        {
            if (!_isSimulationMode)
            {
                StartSimulationMode();
            }
            else
            {
                EndSimulationMode();
            }
        }

        /// <summary>
        /// Rozpoczyna tryb symulacji - tworzy kopię danych i zmienia UI
        /// </summary>
        private void StartSimulationMode()
        {
            _isSimulationMode = true;

            // Zatrzymaj auto-refresh
            _refreshTimer?.Stop();
            _countdownTimer?.Stop();

            // Utwórz głęboką kopię danych
            _simulationBackup = _dostawy
                .Where(d => !d.IsHeaderRow && !d.IsSeparator)
                .Select(d => CloneDostawaModel(d))
                .ToList();

            _simulationBackupNastepny = _dostawyNastepnyTydzien
                .Where(d => !d.IsHeaderRow && !d.IsSeparator)
                .Select(d => CloneDostawaModel(d))
                .ToList();

            // Zmień wygląd UI - tryb symulacji aktywny
            borderSimulation.Background = new SolidColorBrush(Color.FromRgb(254, 243, 199)); // Żółte tło
            borderSimulation.BorderBrush = new SolidColorBrush(Color.FromRgb(251, 191, 36)); // Pomarańczowa ramka
            txtSimulationIcon.Text = "🔬";
            txtSimulationText.Text = "SYMULACJA";
            txtSimulationText.FontWeight = FontWeights.Bold;
            txtSimulationText.Foreground = new SolidColorBrush(Color.FromRgb(180, 83, 9));

            // Dodaj pasek informacyjny na górze
            ShowSimulationBanner(true);

            ShowToast("🧪 Tryb symulacji WŁĄCZONY - zmiany nie będą zapisywane!", ToastType.Info);
        }

        /// <summary>
        /// Kończy tryb symulacji - przywraca oryginalne dane
        /// </summary>
        private void EndSimulationMode()
        {
            var result = MessageBox.Show(
                "Czy chcesz zakończyć symulację?\n\nWszystkie zmiany zostaną COFNIĘTE i dane powrócą do stanu sprzed symulacji.",
                "Zakończ symulację",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            _isSimulationMode = false;

            // Przywróć oryginalne dane z kopii
            RestoreFromBackup();

            // Przywróć wygląd UI
            borderSimulation.Background = new SolidColorBrush(Color.FromRgb(248, 250, 252));
            borderSimulation.BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240));
            txtSimulationIcon.Text = "🧪";
            txtSimulationText.Text = "Symulacja";
            txtSimulationText.FontWeight = FontWeights.Normal;
            txtSimulationText.Foreground = Brushes.Black;

            // Ukryj pasek informacyjny
            ShowSimulationBanner(false);

            // Wznów auto-refresh
            _refreshTimer?.Start();
            _countdownTimer?.Start();

            // Wyczyść kopie zapasowe
            _simulationBackup.Clear();
            _simulationBackupNastepny.Clear();

            ShowToast("✅ Symulacja zakończona - dane przywrócone", ToastType.Success);
        }

        /// <summary>
        /// Przywraca dane z kopii zapasowej
        /// </summary>
        private void RestoreFromBackup()
        {
            // Przeładuj dane z bazy (najczystszy sposób na przywrócenie)
            _ = LoadDostawyAsync();
        }

        /// <summary>
        /// Tworzy głęboką kopię modelu dostawy
        /// </summary>
        private DostawaModel CloneDostawaModel(DostawaModel original)
        {
            return new DostawaModel
            {
                LP = original.LP,
                DataOdbioru = original.DataOdbioru,
                Dostawca = original.Dostawca,
                Auta = original.Auta,
                SztukiDek = original.SztukiDek,
                WagaDek = original.WagaDek,
                TypCeny = original.TypCeny,
                Cena = original.Cena,
                Distance = original.Distance,
                Bufor = original.Bufor,
                PotwWaga = original.PotwWaga,
                PotwSztuki = original.PotwSztuki,
                Uwagi = original.Uwagi,
                LpW = original.LpW,
                DataNotatki = original.DataNotatki,
                RoznicaDni = original.RoznicaDni,
                Ubytek = original.Ubytek,
                IsHeaderRow = original.IsHeaderRow,
                IsSeparator = original.IsSeparator,
                IsEmptyDay = original.IsEmptyDay
            };
        }

        /// <summary>
        /// Pokazuje/ukrywa pasek informacyjny trybu symulacji
        /// </summary>
        private void ShowSimulationBanner(bool show)
        {
            // Znajdź lub utwórz banner
            var existingBanner = this.FindName("simulationBanner") as Border;

            if (show)
            {
                if (existingBanner == null)
                {
                    // Zmień tło całego okna na lekko żółte
                    this.Background = new SolidColorBrush(Color.FromRgb(255, 251, 235)); // Bardzo jasny żółty
                }
            }
            else
            {
                // Przywróć oryginalne tło
                this.Background = new SolidColorBrush(Color.FromRgb(236, 239, 241)); // #ECEFF1
            }
        }

        /// <summary>
        /// Przesuwa dostawę na inny dzień (tylko w trybie symulacji - bez zapisu do bazy)
        /// </summary>
        private void MoveDeliveryToDate(string lp, DateTime newDate)
        {
            // Znajdź dostawę
            var dostawa = _dostawy.FirstOrDefault(d => d.LP == lp) ??
                          _dostawyNastepnyTydzien.FirstOrDefault(d => d.LP == lp);

            if (dostawa == null) return;

            DateTime oldDate = dostawa.DataOdbioru;
            dostawa.DataOdbioru = newDate;

            // Odśwież widok (przebuduj grupy)
            RefreshDostawyView();

            ShowToast($"📅 {dostawa.Dostawca}: {oldDate:dd.MM} → {newDate:dd.MM}", ToastType.Info);
        }

        /// <summary>
        /// Odświeża widok dostaw po zmianach w symulacji
        /// </summary>
        private void RefreshDostawyView()
        {
            // Pobierz dane bez nagłówków
            var dostawyData = _dostawy.Where(d => !d.IsHeaderRow && !d.IsSeparator).ToList();
            var dostawyNastepnyData = _dostawyNastepnyTydzien.Where(d => !d.IsHeaderRow && !d.IsSeparator).ToList();

            // Wyczyść i przebuduj z nowymi grupami
            _dostawy.Clear();
            _dostawyNastepnyTydzien.Clear();

            // Dodaj z powrotem pogrupowane
            RebuildGroupedView(_dostawy, dostawyData, _selectedDate);
            RebuildGroupedView(_dostawyNastepnyTydzien, dostawyNastepnyData, _selectedDate.AddDays(7));
        }

        /// <summary>
        /// Buduje pogrupowany widok dostaw
        /// </summary>
        private void RebuildGroupedView(ObservableCollection<DostawaModel> collection, List<DostawaModel> data, DateTime baseDate)
        {
            DateTime startOfWeek = baseDate.AddDays(-(int)baseDate.DayOfWeek);
            if (baseDate.DayOfWeek == DayOfWeek.Sunday) startOfWeek = baseDate.AddDays(-6);

            var grouped = data.GroupBy(d => d.DataOdbioru.Date).OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                var deliveries = group.ToList();

                // Oblicz sumy i średnie ważone dla tego dnia (tak jak w LoadDostawyForWeekAsync)
                double sumaAuta = 0;
                double sumaSztuki = 0;
                double sumaWagaPomnozona = 0;
                double sumaCenaPomnozona = 0;
                double sumaKMPomnozona = 0;
                double sumaDobyPomnozona = 0;
                int sumaUbytek = 0;
                int iloscZDoby = 0;
                int liczbaSprzedanych = 0;
                int liczbaAnulowanych = 0;

                foreach (var item in deliveries)
                {
                    sumaAuta += item.Auta;
                    sumaSztuki += item.SztukiDek;
                    sumaWagaPomnozona += (double)item.WagaDek * item.Auta;
                    sumaCenaPomnozona += (double)item.Cena * item.Auta;
                    sumaKMPomnozona += item.Distance * item.Auta;

                    // Średnia Doby
                    if (item.RoznicaDni.HasValue && item.RoznicaDni.Value > 0)
                    {
                        sumaDobyPomnozona += item.RoznicaDni.Value * item.Auta;
                        iloscZDoby += item.Auta;
                    }

                    // Licz ubytki (lekkie kurczaki 0.5-2.4 kg)
                    if (item.WagaDek >= 0.5m && item.WagaDek <= 2.4m)
                    {
                        sumaUbytek += item.Auta;
                    }

                    // Licz sprzedane i anulowane
                    if (item.Bufor == "Sprzedany") liczbaSprzedanych++;
                    if (item.Bufor == "Anulowany") liczbaAnulowanych++;
                }

                // Oblicz średnie ważone
                double sredniaWaga = sumaAuta > 0 ? sumaWagaPomnozona / sumaAuta : 0;
                double sredniaCena = sumaAuta > 0 ? sumaCenaPomnozona / sumaAuta : 0;
                double sredniaKM = sumaAuta > 0 ? sumaKMPomnozona / sumaAuta : 0;
                double sredniaDoby = iloscZDoby > 0 ? sumaDobyPomnozona / iloscZDoby : 0;

                // Dodaj nagłówek dnia z pełnymi statystykami
                var dayHeader = new DostawaModel
                {
                    IsHeaderRow = true,
                    IsEmptyDay = deliveries.Count == 0,
                    DataOdbioru = group.Key,
                    Dostawca = GetDayName(group.Key),
                    SumaAuta = sumaAuta,
                    SumaSztuki = sumaSztuki,
                    SredniaWaga = sredniaWaga,
                    SredniaCena = sredniaCena,
                    SredniaKM = sredniaKM,
                    SredniaDoby = sredniaDoby,
                    SumaUbytek = sumaUbytek,
                    LiczbaSprzedanych = liczbaSprzedanych,
                    LiczbaAnulowanych = liczbaAnulowanych
                };
                collection.Add(dayHeader);

                // Dodaj dostawy
                foreach (var d in deliveries.OrderBy(x => x.Dostawca))
                {
                    collection.Add(d);
                }
            }
        }

        /// <summary>
        /// Zwraca nazwę dnia tygodnia
        /// </summary>
        private string GetDayName(DateTime date)
        {
            string[] days = { "Niedziela", "Poniedziałek", "Wtorek", "Środa", "Czwartek", "Piątek", "Sobota" };
            return $"{days[(int)date.DayOfWeek]} {date:dd.MM}";
        }

        #endregion

        private void BtnHistoriaZmian_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var historiaWindow = new HistoriaZmianWindow(ConnectionString, UserID);
                historiaWindow.Owner = this;
                historiaWindow.Show();
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd: {ex.Message}", ToastType.Error);
            }
        }

        private async void BtnZapiszDostawe_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedLP))
            {
                ShowToast("Wybierz dostawę", ToastType.Warning);
                return;
            }

            // Pobierz stare wartości dla audytu
            var oldDostawa = _dostawy.FirstOrDefault(d => d.LP == _selectedLP) ?? _dostawyNastepnyTydzien.FirstOrDefault(d => d.LP == _selectedLP);

            // TRYB SYMULACJI - zapisz tylko w pamięci, bez bazy i logów
            if (_isSimulationMode)
            {
                SimulationSaveDelivery(oldDostawa);
                return;
            }

            // Nowe wartości z formularza
            DateTime? newDataOdbioru = dpData.SelectedDate;
            string newDostawca = cmbDostawca.SelectedItem?.ToString();
            int.TryParse(txtAuta.Text, out int newAuta);
            int.TryParse(txtSztuki.Text, out int newSztuki);
            decimal.TryParse(txtWagaDek.Text.Replace(",", "."), System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture, out decimal newWaga);
            int.TryParse(txtSztNaSzuflade.Text, out int newSztSzuflada);
            string newTypUmowy = cmbTypUmowy.SelectedItem?.ToString();
            string newTypCeny = cmbTypCeny.SelectedItem?.ToString();
            decimal.TryParse(txtCena.Text.Replace(",", "."), System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture, out decimal newCena);
            decimal.TryParse(txtDodatek.Text.Replace(",", "."), System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture, out decimal newDodatek);
            string newStatus = cmbStatus.SelectedItem?.ToString();

            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    string sql = @"UPDATE HarmonogramDostaw SET
                                   DataOdbioru = @DataOdbioru, Dostawca = @Dostawca, Auta = @Auta,
                                   SztukiDek = @SztukiDek, WagaDek = @WagaDek, SztSzuflada = @SztSzuflada,
                                   TypUmowy = @TypUmowy, TypCeny = @TypCeny, Cena = @Cena, Dodatek = @Dodatek,
                                   Bufor = @Bufor, DataMod = @DataMod, KtoMod = @KtoMod
                                   WHERE Lp = @Lp";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@DataOdbioru", dpData.SelectedDate ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Dostawca", cmbDostawca.SelectedItem ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Auta", int.TryParse(txtAuta.Text, out int a) ? a : (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@SztukiDek", int.TryParse(txtSztuki.Text, out int s) ? s : (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@WagaDek", decimal.TryParse(txtWagaDek.Text.Replace(",", "."), out decimal w) ? w : (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@SztSzuflada", int.TryParse(txtSztNaSzuflade.Text, out int sz) ? sz : (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@TypUmowy", cmbTypUmowy.SelectedItem ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@TypCeny", cmbTypCeny.SelectedItem ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Cena", decimal.TryParse(txtCena.Text.Replace(",", "."), out decimal c) ? c : (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Dodatek", decimal.TryParse(txtDodatek.Text.Replace(",", "."), out decimal d) ? d : (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Bufor", cmbStatus.SelectedItem ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@DataMod", DateTime.Now);
                        cmd.Parameters.AddWithValue("@KtoMod", UserID ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Lp", _selectedLP);

                        await cmd.ExecuteNonQueryAsync(_cts.Token);
                    }
                }

                // AUDIT LOG - logowanie zmian (tylko tych, które się zmieniły)
                if (_auditService != null && oldDostawa != null)
                {
                    var changes = new Dictionary<string, (object OldValue, object NewValue)>();

                    if (oldDostawa.DataOdbioru.Date != newDataOdbioru?.Date)
                        changes["DataOdbioru"] = (oldDostawa.DataOdbioru.ToString("yyyy-MM-dd"), newDataOdbioru?.ToString("yyyy-MM-dd"));
                    if (oldDostawa.Dostawca != newDostawca)
                        changes["Dostawca"] = (oldDostawa.Dostawca, newDostawca);
                    if (oldDostawa.Auta != newAuta)
                        changes["Auta"] = (oldDostawa.Auta, newAuta);
                    if ((int)oldDostawa.SztukiDek != newSztuki)
                        changes["SztukiDek"] = (oldDostawa.SztukiDek, newSztuki);
                    if (oldDostawa.WagaDek != newWaga)
                        changes["WagaDek"] = (oldDostawa.WagaDek, newWaga);
                    if (oldDostawa.TypCeny != newTypCeny)
                        changes["TypCeny"] = (oldDostawa.TypCeny, newTypCeny);
                    if (oldDostawa.Cena != newCena)
                        changes["Cena"] = (oldDostawa.Cena, newCena);
                    if (oldDostawa.Bufor != newStatus)
                        changes["Bufor"] = (oldDostawa.Bufor, newStatus);

                    if (changes.Count > 0)
                    {
                        await _auditService.LogFullDeliverySaveAsync(_selectedLP, changes,
                            newDostawca, newDataOdbioru, _cts.Token);
                    }
                }

                ShowToast("Zmiany zapisane", ToastType.Success);
                await LoadDostawyAsync();
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd zapisu: {ex.Message}", ToastType.Error);
            }
        }

        #endregion

        #region Obsługa hodowcy

        private void CmbDostawca_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string hodowca = cmbDostawca.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(hodowca))
            {
                LoadLpWstawieniaForHodowca(hodowca);
                // Aktualizuj nagłówek z nazwą hodowcy
                txtNazwaHodowcyHeader.Text = $"- {hodowca}";
            }
            else
            {
                txtNazwaHodowcyHeader.Text = "";
            }
        }

        private void CmbStatus_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Formatowanie kolorów usunięte na życzenie użytkownika
            // ComboBox zachowuje domyślny wygląd
        }

        private void BtnMapa_Click(object sender, RoutedEventArgs e)
        {
            string adres = $"{txtUlicaH.Text}, {txtKodPocztowyH.Text}";
            if (!string.IsNullOrWhiteSpace(adres))
            {
                try
                {
                    Process.Start(new ProcessStartInfo($"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(adres)}") { UseShellExecute = true });
                }
                catch { }
            }
        }

        private void BtnSMS_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Funkcja SMS wymaga konfiguracji Twilio.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Obsługa wstawień

        private void CmbLpWstawienia_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string lp = cmbLpWstawienia.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(lp))
            {
                LoadWstawienia(lp);
            }
        }

        private void BtnNoweWstawienie_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Otwórz okno nowego wstawienia
                var wstawienieWindow = new WstawienieWindow();
                wstawienieWindow.Owner = this;
                wstawienieWindow.ShowDialog();

                // Odśwież dane po zamknięciu okna
                _ = LoadDostawyAsync();
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd: {ex.Message}", ToastType.Error);
            }
        }

        private void BtnEdytujWstawienie_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string selectedLp = cmbLpWstawienia.SelectedItem?.ToString();
                if (string.IsNullOrEmpty(selectedLp))
                {
                    ShowToast("Wybierz wstawienie do edycji", ToastType.Warning);
                    return;
                }

                // Otwórz okno edycji wstawienia
                var wstawienieWindow = new WstawienieWindow();
                wstawienieWindow.Modyfikacja = true;
                if (int.TryParse(selectedLp, out int lpInt))
                {
                    wstawienieWindow.LpWstawienia = lpInt;
                }
                wstawienieWindow.Owner = this;
                wstawienieWindow.ShowDialog();

                // Odśwież dane po zamknięciu okna
                _ = LoadDostawyAsync();
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd: {ex.Message}", ToastType.Error);
            }
        }

        #endregion

        #region Obsługa transportu

        private void TxtSztNaSzufladeCalc_TextChanged(object sender, TextChangedEventArgs e)
        {
            CalculateTransport();
        }

        private void TxtObliczoneAuta_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(txtWyliczone.Text, out int wyliczone) && int.TryParse(txtObliczoneAuta.Text, out int auta))
            {
                txtObliczoneSztuki.Text = (wyliczone * auta).ToString();
            }
        }

        private void CalculateTransport()
        {
            if (int.TryParse(txtSztNaSzufladeCalc.Text, out int sztNaSzuflade))
            {
                int wyliczone = sztNaSzuflade * 264; // 264 szuflady w aucie
                txtWyliczone.Text = wyliczone.ToString();
            }
        }

        // Synchronizacja Szt/szuflade z DANE DOSTAWY do ZAŁADUNEK AVILOG
        private void TxtSztNaSzuflade_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtSztNaSzufladeWaga == null || txtSztNaSzufladeWaga2 == null || txtSztNaSzufladeWaga3 == null) return;

            // Wiersz środkowy (podświetlony) = wartość z dostawy
            txtSztNaSzufladeWaga.Text = txtSztNaSzuflade.Text;

            if (int.TryParse(txtSztNaSzuflade.Text, out int szt))
            {
                // Wiersz 1 (górny) = Szt - 1
                txtSztNaSzufladeWaga2.Text = szt > 1 ? (szt - 1).ToString() : szt.ToString();
                // Wiersz 3 (dolny) = Szt + 1
                txtSztNaSzufladeWaga3.Text = (szt + 1).ToString();
            }
            else
            {
                txtSztNaSzufladeWaga2.Text = "";
                txtSztNaSzufladeWaga3.Text = "";
            }

            // Przelicz wszystkie wiersze
            CalculateZaladunekRow1();
            CalculateZaladunekRow2();
            CalculateZaladunekRow3();
        }

        // Gdy zmienia się Waga dek - przelicz wszystkie wiersze
        private void TxtWagaDek_TextChanged(object sender, TextChangedEventArgs e)
        {
            CalculateZaladunekRow1();
            CalculateZaladunekRow2();
            CalculateZaladunekRow3();
        }

        // Obliczenie dla wiersza 1: KG/skrzyn = Szt × Waga, KG skrzyn = KG/skrzyn × 264
        private void CalculateZaladunekRow1()
        {
            // Sprawdź czy kontrolki są zainicjalizowane
            if (txtWagaDek == null || txtSztNaSzufladeWaga == null || txtKGwSkrzynce == null || txtKGSkrzyn264 == null)
                return;

            // Pobierz wagę dek z pola txtWagaDek (obsłuż oba separatory: , i .)
            if (!double.TryParse(txtWagaDek.Text?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double wagaDek))
                wagaDek = 0;

            // Pobierz sztuki na szufladę
            if (!double.TryParse(txtSztNaSzufladeWaga.Text?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double sztNaSzuflade))
                sztNaSzuflade = 0;

            // KG/skrzyn = Sztuki na szufladę × Waga dek
            double kgSkrzyn = sztNaSzuflade * wagaDek;
            txtKGwSkrzynce.Text = kgSkrzyn > 0 ? kgSkrzyn.ToString("N2") : "";

            // Aktualizuj kolor obramowania (żółty 49-51, czerwony >51)
            UpdateKGBorderColor(borderKGwSkrzynce, kgSkrzyn);

            // KG skrzyn (×264) = KG/skrzyn × 264
            double kgSkrzyn264 = kgSkrzyn * 264;
            txtKGSkrzyn264.Text = kgSkrzyn264 > 0 ? kgSkrzyn264.ToString("N0") : "";

            CalculateKGSum();
        }

        // Obliczenie dla wiersza 2
        private void CalculateZaladunekRow2()
        {
            // Sprawdź czy kontrolki są zainicjalizowane
            if (txtWagaDek == null || txtSztNaSzufladeWaga2 == null || txtKGwSkrzynce2 == null || txtKGSkrzyn264_2 == null)
                return;

            // Pobierz wagę dek z pola txtWagaDek (obsłuż oba separatory: , i .)
            if (!double.TryParse(txtWagaDek.Text?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double wagaDek))
                wagaDek = 0;

            // Pobierz sztuki na szufladę (wiersz 2 - edytowalny)
            if (!double.TryParse(txtSztNaSzufladeWaga2.Text?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double sztNaSzuflade2))
                sztNaSzuflade2 = 0;

            // KG/skrzyn = Sztuki na szufladę × Waga dek
            double kgSkrzyn2 = sztNaSzuflade2 * wagaDek;
            txtKGwSkrzynce2.Text = kgSkrzyn2 > 0 ? kgSkrzyn2.ToString("N2") : "";

            // Aktualizuj kolor obramowania (żółty 49-51, czerwony >51)
            UpdateKGBorderColor(borderKGwSkrzynce2, kgSkrzyn2);

            // KG skrzyn (×264) = KG/skrzyn × 264
            double kgSkrzyn264_2 = kgSkrzyn2 * 264;
            txtKGSkrzyn264_2.Text = kgSkrzyn264_2 > 0 ? kgSkrzyn264_2.ToString("N0") : "";

            CalculateKGSum2();
        }

        // Obliczenie dla wiersza 3: Szt + 1
        private void CalculateZaladunekRow3()
        {
            // Sprawdź czy kontrolki są zainicjalizowane
            if (txtWagaDek == null || txtSztNaSzufladeWaga3 == null || txtKGwSkrzynce3 == null || txtKGSkrzyn264_3 == null)
                return;

            // Pobierz wagę dek z pola txtWagaDek (obsłuż oba separatory: , i .)
            if (!double.TryParse(txtWagaDek.Text?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double wagaDek))
                wagaDek = 0;

            // Pobierz sztuki na szufladę (wiersz 3)
            if (!double.TryParse(txtSztNaSzufladeWaga3.Text?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double sztNaSzuflade3))
                sztNaSzuflade3 = 0;

            // KG/skrzyn = Sztuki na szufladę × Waga dek
            double kgSkrzyn3 = sztNaSzuflade3 * wagaDek;
            txtKGwSkrzynce3.Text = kgSkrzyn3 > 0 ? kgSkrzyn3.ToString("N2") : "";

            // Aktualizuj kolor obramowania (żółty 49-51, czerwony >51) - ostatni wiersz
            UpdateKGBorderColor(borderKGwSkrzynce3, kgSkrzyn3, true);

            // KG skrzyn (×264) = KG/skrzyn × 264
            double kgSkrzyn264_3 = kgSkrzyn3 * 264;
            txtKGSkrzyn264_3.Text = kgSkrzyn264_3 > 0 ? kgSkrzyn264_3.ToString("N0") : "";

            CalculateKGSum3();
        }

        // Zdarzenie zmiany wartości w polach Załadunku
        private void TxtZaladunekField_TextChanged(object sender, TextChangedEventArgs e)
        {
            CalculateKGSum();
            CalculateKGSum2();
            CalculateKGSum3();
        }

        // Metoda do aktualizacji koloru obramowania na podstawie wartości KG/skrzyn
        private void UpdateKGBorderColor(Border border, double kgValue, bool isLastRow = false)
        {
            if (border == null) return;

            if (kgValue > 51.0)
            {
                // Czerwone obramowanie dla wartości > 51
                border.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
                border.BorderThickness = new Thickness(2);
            }
            else if (kgValue >= 49.0 && kgValue <= 51.0)
            {
                // Żółte obramowanie dla wartości 49-51
                border.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EAB308"));
                border.BorderThickness = new Thickness(2);
            }
            else
            {
                // Domyślne obramowanie
                border.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E2E8F0"));
                border.BorderThickness = isLastRow ? new Thickness(0, 0, 1, 0) : new Thickness(0, 0, 1, 1);
            }
        }

        // Kliknięcie w nagłówek Paleciak - włącz/wyłącz
        private void BorderPaleciakHeader_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _paleciakEnabled = !_paleciakEnabled;

            if (_paleciakEnabled)
            {
                // Włącz paleciak
                txtPaleciakHeader.Text = "Paleciak ✓";
                borderPaleciakHeader.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFF59D"));
                txtKGwPaleciak.Text = "3150";
                txtKGwPaleciak2.Text = "3150";
                txtKGwPaleciak3.Text = "3150";
            }
            else
            {
                // Wyłącz paleciak
                txtPaleciakHeader.Text = "Paleciak ✗";
                borderPaleciakHeader.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFCDD2"));
                txtKGwPaleciak.Text = "";
                txtKGwPaleciak2.Text = "";
                txtKGwPaleciak3.Text = "";
            }

            // Przelicz sumy
            CalculateKGSum();
            CalculateKGSum2();
            CalculateKGSum3();
        }

        // Obliczenie Suma KG dla wiersza 1: KG skrzyn (×264) + WagaSamochodu + Paleciak
        private void CalculateKGSum()
        {
            // Sprawdź czy kontrolki są zainicjalizowane
            if (txtKGSkrzyn264 == null || txtWagaSamochodu == null || txtKGwPaleciak == null || txtKGSuma == null)
                return;

            double sum = 0;

            // KG skrzyn (×264)
            if (double.TryParse(txtKGSkrzyn264.Text?.Replace(",", "").Replace(" ", ""), out double kgSkrzyn264))
                sum += kgSkrzyn264;

            // Waga samochodu
            if (double.TryParse(txtWagaSamochodu.Text?.Replace(",", "").Replace(" ", ""), out double wagaSam))
                sum += wagaSam;

            // Waga paleciaka (stała 3150)
            if (double.TryParse(txtKGwPaleciak.Text?.Replace(",", "").Replace(" ", ""), out double paleciak))
                sum += paleciak;

            txtKGSuma.Text = sum.ToString("N0");
        }

        // Obliczenie Suma KG dla wiersza 2
        private void CalculateKGSum2()
        {
            // Sprawdź czy kontrolki są zainicjalizowane
            if (txtKGSkrzyn264_2 == null || txtWagaSamochodu2 == null || txtKGwPaleciak2 == null || txtKGSuma2 == null)
                return;

            double sum = 0;

            // KG skrzyn (×264) wiersz 2
            if (double.TryParse(txtKGSkrzyn264_2.Text?.Replace(",", "").Replace(" ", ""), out double kgSkrzyn264_2))
                sum += kgSkrzyn264_2;

            // Waga samochodu (wiersz 2)
            if (double.TryParse(txtWagaSamochodu2.Text?.Replace(",", "").Replace(" ", ""), out double wagaSam))
                sum += wagaSam;

            // Waga paleciaka (wiersz 2)
            if (double.TryParse(txtKGwPaleciak2.Text?.Replace(",", "").Replace(" ", ""), out double paleciak2))
                sum += paleciak2;

            txtKGSuma2.Text = sum.ToString("N0");
        }

        // Obliczenie Suma KG dla wiersza 3
        private void CalculateKGSum3()
        {
            // Sprawdź czy kontrolki są zainicjalizowane
            if (txtKGSkrzyn264_3 == null || txtWagaSamochodu3 == null || txtKGwPaleciak3 == null || txtKGSuma3 == null)
                return;

            double sum = 0;

            // KG skrzyn (×264) wiersz 3
            if (double.TryParse(txtKGSkrzyn264_3.Text?.Replace(",", "").Replace(" ", ""), out double kgSkrzyn264_3))
                sum += kgSkrzyn264_3;

            // Waga samochodu (wiersz 3)
            if (double.TryParse(txtWagaSamochodu3.Text?.Replace(",", "").Replace(" ", ""), out double wagaSam))
                sum += wagaSam;

            // Waga paleciaka (wiersz 3)
            if (double.TryParse(txtKGwPaleciak3.Text?.Replace(",", "").Replace(" ", ""), out double paleciak3))
                sum += paleciak3;

            txtKGSuma3.Text = sum.ToString("N0");
        }

        private void BtnWklejObliczenia_Click(object sender, RoutedEventArgs e)
        {
            txtSztuki.Text = txtObliczoneSztuki.Text;
            txtAuta.Text = txtObliczoneAuta.Text;
            txtSztNaSzuflade.Text = txtSztNaSzufladeCalc.Text;
        }

        #endregion

        #region Obsługa notatek

        private async void BtnDodajNotatke_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedLP))
            {
                ShowToast("Wybierz dostawę", ToastType.Warning);
                return;
            }

            string tresc = txtNowaNotatka.Text?.Trim();
            if (string.IsNullOrEmpty(tresc))
            {
                ShowToast("Wpisz treść notatki", ToastType.Warning);
                return;
            }

            // Pobierz info dla audytu
            var dostawa = _dostawy.FirstOrDefault(d => d.LP == _selectedLP) ?? _dostawyNastepnyTydzien.FirstOrDefault(d => d.LP == _selectedLP);

            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    string sql = "INSERT INTO Notatki (IndeksID, TypID, Tresc, KtoStworzyl, DataUtworzenia) VALUES (@lp, 1, @tresc, @kto, GETDATE())";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@lp", _selectedLP);
                        cmd.Parameters.AddWithValue("@tresc", tresc);
                        cmd.Parameters.AddWithValue("@kto", UserID ?? "0");
                        await cmd.ExecuteNonQueryAsync(_cts.Token);
                    }
                }

                // AUDIT LOG - logowanie dodania notatki
                if (_auditService != null)
                {
                    await _auditService.LogNoteAddedAsync(_selectedLP, tresc, AuditChangeSource.Form_DodajNotatke,
                        dostawa?.Dostawca, dostawa?.DataOdbioru, _cts.Token);
                }

                txtNowaNotatka.Text = "";
                ShowToast("Notatka dodana", ToastType.Success);
                await LoadNotatkiAsync(_selectedLP);
                await LoadOstatnieNotatkiAsync();
                // Odśwież tabele dostaw
                await LoadDostawyAsync();
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd: {ex.Message}", ToastType.Error);
            }
        }

        // UWAGA: Notatki w zakładce Karta są tylko do odczytu
        // Dodawanie notatek odbywa się przez zakładkę Notatki

        #endregion

        #region Obsługa partii

        private void DgPartie_CellFormatting(object sender, DataGridCellEditEndingEventArgs e)
        {
            // Formatowanie
        }

        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            var partia = dgPartie.SelectedItem as PartiaModel;
            if (partia != null && !string.IsNullOrEmpty(partia.FolderPath))
            {
                string photosRoot = ConfigurationManager.AppSettings["PhotosRoot"] ?? @"\\192.168.0.170\Install\QC_Foto";
                string fullPath = Path.Combine(photosRoot, partia.FolderPath.Replace('/', '\\'));

                if (Directory.Exists(fullPath))
                {
                    try { Process.Start("explorer.exe", fullPath); } catch { }
                }
            }
        }

        #endregion

        #region Obsługa rankingu

        private void BtnPokazHistorie_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgRanking.SelectedItem as RankingModel;
            if (selected != null)
            {
                try
                {
                    var window = new Kalendarz1.AnkietyHodowcow.HistoriaHodowcyWindowPremium(ConnectionString, selected.Dostawca);
                    window.ShowDialog();
                }
                catch { MessageBox.Show("Nie można otworzyć okna historii.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information); }
            }
        }

        #endregion

        #region Ankiety

        private void TryShowSurveyIfInWindow()
        {
            if (_surveyShownThisSession) return;

            var now = DateTime.Now.TimeOfDay;
            if (now >= SURVEY_START && now <= SURVEY_END)
            {
                _surveyShownThisSession = true;
                // Tutaj wywołanie ankiety jeśli jest zaimplementowana
            }
        }

        #endregion

        #region Drag & Drop

        private void DgDostawy_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void DgDostawy_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                _isDragging = false;
                return;
            }

            var dg = sender as DataGrid;
            if (dg == null) return;

            Point mousePos = e.GetPosition(null);
            Vector diff = _dragStartPoint - mousePos;

            // Sprawdź czy ruch jest wystarczająco duży
            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                var selectedItem = dg.SelectedItem as DostawaModel;
                if (selectedItem != null && !selectedItem.IsHeaderRow && !selectedItem.IsSeparator)
                {
                    _draggedItem = selectedItem;
                    _isDragging = true;

                    // Rozpocznij operację Drag & Drop
                    DataObject dragData = new DataObject("DostawaModel", selectedItem);
                    DragDrop.DoDragDrop(dg, dragData, DragDropEffects.Move);
                }
            }
        }

        private DataGridRow _highlightedDropTarget = null;

        private void DgDostawy_DragEnter(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("DostawaModel"))
            {
                e.Effects = DragDropEffects.None;
                return;
            }
            e.Effects = DragDropEffects.Move;
        }

        private void DgDostawy_DragLeave(object sender, DragEventArgs e)
        {
            // Usuń podświetlenie
            ClearDropTargetHighlight();
        }

        private void DgDostawy_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("DostawaModel"))
            {
                e.Effects = DragDropEffects.None;
                return;
            }

            var targetDg = sender as DataGrid;
            if (targetDg == null) return;

            Point dropPos = e.GetPosition(targetDg);
            var hit = VisualTreeHelper.HitTest(targetDg, dropPos);
            if (hit == null) return;

            // Znajdź wiersz pod kursorem
            DataGridRow row = FindVisualParent<DataGridRow>(hit.VisualHit);

            // Usuń stare podświetlenie
            if (_highlightedDropTarget != null && _highlightedDropTarget != row)
            {
                ClearDropTargetHighlight();
            }

            // Podświetl nowy cel
            if (row != null && row != _highlightedDropTarget)
            {
                var targetItem = row.DataContext as DostawaModel;
                if (targetItem != null && targetItem.IsHeaderRow && !targetItem.IsSeparator)
                {
                    // Podświetl nagłówek dnia - można tu upuścić
                    row.BorderBrush = new SolidColorBrush(Color.FromRgb(33, 150, 243)); // Blue
                    row.BorderThickness = new Thickness(2);

                    // Dodaj efekt glow
                    row.Effect = new DropShadowEffect
                    {
                        Color = Color.FromRgb(33, 150, 243),
                        BlurRadius = 15,
                        ShadowDepth = 0,
                        Opacity = 0.6
                    };

                    _highlightedDropTarget = row;
                }
            }

            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        private void ClearDropTargetHighlight()
        {
            if (_highlightedDropTarget != null)
            {
                _highlightedDropTarget.BorderThickness = new Thickness(0);
                _highlightedDropTarget.Effect = null;
                _highlightedDropTarget = null;
            }
        }

        private void DgDostawy_Drop(object sender, DragEventArgs e)
        {
            // Usuń podświetlenie
            ClearDropTargetHighlight();

            if (!e.Data.GetDataPresent("DostawaModel")) return;

            var droppedItem = e.Data.GetData("DostawaModel") as DostawaModel;
            if (droppedItem == null || droppedItem.IsHeaderRow) return;

            // Pobierz docelowy element
            var targetDg = sender as DataGrid;
            if (targetDg == null) return;

            Point dropPos = e.GetPosition(targetDg);
            var hit = VisualTreeHelper.HitTest(targetDg, dropPos);
            if (hit == null) return;

            // Znajdź wiersz docelowy
            DataGridRow row = FindVisualParent<DataGridRow>(hit.VisualHit);
            if (row == null) return;

            var targetItem = row.DataContext as DostawaModel;
            if (targetItem == null) return;

            // Jeśli upuszczono na nagłówek dnia - przenieś do tego dnia
            DateTime newDate;
            if (targetItem.IsHeaderRow && !targetItem.IsSeparator)
            {
                newDate = targetItem.DataOdbioru.Date;
            }
            else
            {
                newDate = targetItem.DataOdbioru.Date;
            }

            // Nie przenoś jeśli data się nie zmieniła
            if (droppedItem.DataOdbioru.Date == newDate)
            {
                _isDragging = false;
                return;
            }

            // Pokaż dialog potwierdzenia
            string hodowca = droppedItem.Dostawca ?? "nieznany";
            string auta = droppedItem.Auta.ToString();
            string oldDateStr = droppedItem.DataOdbioru.ToString("dd.MM.yyyy (dddd)");
            string newDateStr = newDate.ToString("dd.MM.yyyy (dddd)");

            string message = $"Czy na pewno chcesz przenieść dostawę?\n\n" +
                             $"Hodowca: {hodowca}\n" +
                             $"Auta: {auta}\n\n" +
                             $"Z: {oldDateStr}\n" +
                             $"Na: {newDateStr}";

            MessageBoxResult result = MessageBox.Show(message, "Potwierdzenie przeniesienia dostawy",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                _isDragging = false;
                return;
            }

            // Przenieś dostawę do nowej daty (z audytem)
            _ = MoveDeliveryToDateAsync(droppedItem.LP, droppedItem.DataOdbioru.Date, newDate, droppedItem.Dostawca);
            _isDragging = false;
        }

        private async Task MoveDeliveryToDateAsync(string lp, DateTime oldDate, DateTime newDate, string dostawca)
        {
            // TRYB SYMULACJI - tylko zmiana lokalna, bez zapisu do bazy i logów
            if (_isSimulationMode)
            {
                MoveDeliveryToDate(lp, newDate);
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    using (SqlCommand cmd = new SqlCommand("UPDATE HarmonogramDostaw SET DataOdbioru = @data WHERE LP = @lp", conn))
                    {
                        cmd.Parameters.AddWithValue("@data", newDate);
                        cmd.Parameters.AddWithValue("@lp", lp);
                        await cmd.ExecuteNonQueryAsync(_cts.Token);
                    }
                }

                // AUDIT LOG - logowanie drag & drop
                if (_auditService != null)
                {
                    await _auditService.LogDragDropAsync(lp, oldDate, newDate, dostawca, _cts.Token);
                }

                ShowToast($"Przeniesiono dostawę na {newDate:dd.MM.yyyy}", ToastType.Success);
                await LoadDostawyAsync();
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd przenoszenia: {ex.Message}", ToastType.Error);
            }
        }

        private static T FindVisualParent<T>(DependencyObject obj) where T : DependencyObject
        {
            while (obj != null)
            {
                if (obj is T t) return t;
                obj = VisualTreeHelper.GetParent(obj);
            }
            return null;
        }

        #endregion

        #region Multi-select i Bulk Actions

        private void SelectAllDeliveries()
        {
            _selectedLPs.Clear();
            foreach (var d in _dostawy.Where(d => !d.IsHeaderRow && !d.IsSeparator))
            {
                _selectedLPs.Add(d.LP);
            }
            dgDostawy.SelectAll();
        }

        private async Task BulkConfirmAsync(bool confirm)
        {
            if (_selectedLPs.Count == 0 && !string.IsNullOrEmpty(_selectedLP))
            {
                _selectedLPs.Add(_selectedLP);
            }

            if (_selectedLPs.Count == 0)
            {
                ShowToast("Brak zaznaczonych dostaw", ToastType.Warning);
                return;
            }

            string status = confirm ? "Potwierdzony" : "Niepotwierdzony";
            int count = 0;
            var lpsToProcess = _selectedLPs.ToList(); // Kopia dla audytu

            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    foreach (var lp in lpsToProcess)
                    {
                        using (SqlCommand cmd = new SqlCommand("UPDATE HarmonogramDostaw SET bufor = @status WHERE LP = @lp", conn))
                        {
                            cmd.Parameters.AddWithValue("@status", status);
                            cmd.Parameters.AddWithValue("@lp", lp);
                            count += await cmd.ExecuteNonQueryAsync(_cts.Token);
                        }
                    }
                }

                // AUDIT LOG - logowanie operacji masowej
                if (_auditService != null)
                {
                    await _auditService.LogBulkOperationAsync("HarmonogramDostaw", lpsToProcess,
                        AuditChangeSource.BulkConfirm, "Bufor", status, null, _cts.Token);
                }

                ShowToast($"Potwierdzono {count} dostaw", ToastType.Success);
                _selectedLPs.Clear();
                await LoadDostawyAsync();
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd: {ex.Message}", ToastType.Error);
            }
        }

        private async Task BulkCancelAsync()
        {
            if (_selectedLPs.Count == 0 && !string.IsNullOrEmpty(_selectedLP))
            {
                _selectedLPs.Add(_selectedLP);
            }

            if (_selectedLPs.Count == 0)
            {
                ShowToast("Brak zaznaczonych dostaw", ToastType.Warning);
                return;
            }

            if (MessageBox.Show($"Czy na pewno chcesz anulować {_selectedLPs.Count} dostaw?", "Potwierdzenie",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            int count = 0;
            var lpsToProcess = _selectedLPs.ToList(); // Kopia dla audytu

            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    foreach (var lp in lpsToProcess)
                    {
                        using (SqlCommand cmd = new SqlCommand("UPDATE HarmonogramDostaw SET bufor = 'Anulowany' WHERE LP = @lp", conn))
                        {
                            cmd.Parameters.AddWithValue("@lp", lp);
                            count += await cmd.ExecuteNonQueryAsync(_cts.Token);
                        }
                    }
                }

                // AUDIT LOG - logowanie masowego anulowania
                if (_auditService != null)
                {
                    await _auditService.LogBulkOperationAsync("HarmonogramDostaw", lpsToProcess,
                        AuditChangeSource.BulkCancel, "Bufor", "Anulowany", null, _cts.Token);
                }

                ShowToast($"Anulowano {count} dostaw", ToastType.Success);
                _selectedLPs.Clear();
                await LoadDostawyAsync();
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd: {ex.Message}", ToastType.Error);
            }
        }

        #endregion

        #region Toast Notifications

        private void ShowToast(string message, ToastType type = ToastType.Info)
        {
            _toastQueue.Enqueue(new ToastMessage { Message = message, Type = type });
            if (!_isShowingToast)
            {
                _ = ProcessToastQueueAsync();
            }
        }

        private async Task ProcessToastQueueAsync()
        {
            _isShowingToast = true;

            while (_toastQueue.Count > 0)
            {
                var toast = _toastQueue.Dequeue();
                await ShowToastAsync(toast);
            }

            _isShowingToast = false;
        }

        private async Task ShowToastAsync(ToastMessage toast)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                if (toastBorder == null) return;

                // Ikona i kolor w zależności od typu
                string icon = "";
                switch (toast.Type)
                {
                    case ToastType.Success:
                        toastBorder.Background = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                        icon = "✓ ";
                        break;
                    case ToastType.Error:
                        toastBorder.Background = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                        icon = "✗ ";
                        break;
                    case ToastType.Warning:
                        toastBorder.Background = new SolidColorBrush(Color.FromRgb(255, 152, 0));
                        icon = "⚠ ";
                        break;
                    default:
                        toastBorder.Background = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                        icon = "ℹ ";
                        break;
                }

                txtToastMessage.Text = icon + toast.Message;
                toastBorder.Visibility = Visibility.Visible;

                // Animacja wejścia - slide z góry + fade
                var translateTransform = new TranslateTransform(0, -30);
                toastBorder.RenderTransform = translateTransform;

                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250));
                var slideIn = new DoubleAnimation(-30, 0, TimeSpan.FromMilliseconds(250));
                slideIn.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };

                // Dla sukcesu - dodatkowy efekt pulse
                if (toast.Type == ToastType.Success)
                {
                    var scaleTransform = new ScaleTransform(1, 1);
                    var transformGroup = new TransformGroup();
                    transformGroup.Children.Add(translateTransform);
                    transformGroup.Children.Add(scaleTransform);
                    toastBorder.RenderTransform = transformGroup;
                    toastBorder.RenderTransformOrigin = new Point(0.5, 0.5);

                    var pulseX = new DoubleAnimation(1, 1.05, TimeSpan.FromMilliseconds(100));
                    pulseX.AutoReverse = true;
                    pulseX.BeginTime = TimeSpan.FromMilliseconds(250);
                    var pulseY = new DoubleAnimation(1, 1.05, TimeSpan.FromMilliseconds(100));
                    pulseY.AutoReverse = true;
                    pulseY.BeginTime = TimeSpan.FromMilliseconds(250);

                    scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, pulseX);
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, pulseY);
                }

                toastBorder.BeginAnimation(OpacityProperty, fadeIn);
                translateTransform.BeginAnimation(TranslateTransform.YProperty, slideIn);
            });

            // Pokaż przez 2.5 sekundy
            await Task.Delay(2500);

            await Dispatcher.InvokeAsync(() =>
            {
                // Animacja wyjścia - slide do góry + fade
                var translateTransform = toastBorder.RenderTransform as TranslateTransform;
                if (translateTransform == null)
                {
                    var group = toastBorder.RenderTransform as TransformGroup;
                    translateTransform = group?.Children.OfType<TranslateTransform>().FirstOrDefault();
                }
                if (translateTransform == null)
                {
                    translateTransform = new TranslateTransform(0, 0);
                    toastBorder.RenderTransform = translateTransform;
                }

                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
                var slideOut = new DoubleAnimation(0, -20, TimeSpan.FromMilliseconds(200));
                slideOut.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn };

                fadeOut.Completed += (s, e) =>
                {
                    toastBorder.Visibility = Visibility.Collapsed;
                };

                toastBorder.BeginAnimation(OpacityProperty, fadeOut);
                translateTransform.BeginAnimation(TranslateTransform.YProperty, slideOut);
            });

            await Task.Delay(250);
        }

        #endregion

        #region Animacje

        /// <summary>
        /// Animacja fade out/in dla DataGrid
        /// </summary>
        private async Task AnimateDataGridTransition(DataGrid dg, Func<Task> loadAction, bool slideDirection = true)
        {
            if (dg == null) return;

            // Fade out z przesunięciem
            var translateTransform = new TranslateTransform(0, 0);
            dg.RenderTransform = translateTransform;

            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
            var slideOut = new DoubleAnimation(0, slideDirection ? -30 : 30, TimeSpan.FromMilliseconds(150));
            slideOut.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn };

            dg.BeginAnimation(OpacityProperty, fadeOut);
            translateTransform.BeginAnimation(TranslateTransform.XProperty, slideOut);

            await Task.Delay(150);

            // Załaduj dane
            await loadAction();

            // Fade in z przesunięciem
            translateTransform.X = slideDirection ? 30 : -30;
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            var slideIn = new DoubleAnimation(slideDirection ? 30 : -30, 0, TimeSpan.FromMilliseconds(200));
            slideIn.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };

            dg.BeginAnimation(OpacityProperty, fadeIn);
            translateTransform.BeginAnimation(TranslateTransform.XProperty, slideIn);
        }

        /// <summary>
        /// Animacja expand/collapse dla panelu
        /// </summary>
        private void AnimateExpandCollapse(FrameworkElement element, bool expand, Action onComplete = null)
        {
            if (element == null) return;

            var translateTransform = new TranslateTransform(expand ? 50 : 0, 0);
            element.RenderTransform = translateTransform;

            if (expand)
            {
                element.Visibility = Visibility.Visible;
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
                var slideIn = new DoubleAnimation(50, 0, TimeSpan.FromMilliseconds(300));
                slideIn.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };

                element.BeginAnimation(OpacityProperty, fadeIn);
                translateTransform.BeginAnimation(TranslateTransform.XProperty, slideIn);
            }
            else
            {
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
                var slideOut = new DoubleAnimation(0, 50, TimeSpan.FromMilliseconds(200));
                slideOut.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn };

                fadeOut.Completed += (s, e) =>
                {
                    element.Visibility = Visibility.Collapsed;
                    onComplete?.Invoke();
                };

                element.BeginAnimation(OpacityProperty, fadeOut);
                translateTransform.BeginAnimation(TranslateTransform.XProperty, slideOut);
            }
        }

        /// <summary>
        /// Flash zielony na wierszu przy zapisie
        /// </summary>
        private void AnimateRowSuccessFlash(DataGridRow row)
        {
            if (row == null) return;

            var originalBackground = row.Background;

            var flashAnimation = new ColorAnimation
            {
                To = Color.FromRgb(76, 175, 80), // Green
                Duration = TimeSpan.FromMilliseconds(150),
                AutoReverse = true
            };

            var brush = new SolidColorBrush(Colors.Transparent);
            row.Background = brush;
            brush.BeginAnimation(SolidColorBrush.ColorProperty, flashAnimation);

            // Po animacji przywróć oryginalny kolor
            flashAnimation.Completed += (s, e) =>
            {
                row.Background = originalBackground;
            };
        }

        /// <summary>
        /// Pokaż animowany checkmark przy sukcesie
        /// </summary>
        private void ShowSuccessCheckmark(FrameworkElement targetElement)
        {
            // Utwórz checkmark overlay
            var checkmark = new TextBlock
            {
                Text = "✓",
                FontSize = 32,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(0, 0)
            };

            // Animacja pojawiania się
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            var scaleXIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
            var scaleYIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
            scaleXIn.EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 };
            scaleYIn.EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 };

            // Animacja znikania
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
            fadeOut.BeginTime = TimeSpan.FromSeconds(1);

            checkmark.BeginAnimation(OpacityProperty, fadeIn);
            ((ScaleTransform)checkmark.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, scaleXIn);
            ((ScaleTransform)checkmark.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, scaleYIn);
            checkmark.BeginAnimation(OpacityProperty, fadeOut);
        }

        #endregion

        #region Status Bar

        private void UpdateStatusBar()
        {
            if (txtStatusBar == null) return;

            int totalRows = _dostawy.Count(d => !d.IsHeaderRow && !d.IsSeparator);
            int totalAuta = _dostawy.Where(d => !d.IsHeaderRow && !d.IsSeparator).Sum(d => d.Auta);
            double totalSztuki = _dostawy.Where(d => !d.IsHeaderRow && !d.IsSeparator).Sum(d => d.SztukiDek);
            int potwierdzone = _dostawy.Count(d => !d.IsHeaderRow && !d.IsSeparator && d.Bufor == "Potwierdzony");

            Dispatcher.InvokeAsync(() =>
            {
                txtStatusBar.Text = $"Dostaw: {totalRows} | Auta: {totalAuta} | Sztuki: {totalSztuki:#,0} | Potwierdzone: {potwierdzone}";
            });
        }

        #endregion

        #region Search

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            _searchText = textBox?.Text ?? "";

            // Debounce - poczekaj 300ms przed wyszukiwaniem
            _ = PerformSearchAsync();
        }

        private async Task PerformSearchAsync()
        {
            await Task.Delay(300);
            await LoadDostawyAsync();
        }

        #endregion

        #region Quick Notes

        private async void BtnQuickNote_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedLP))
            {
                ShowToast("Wybierz dostawę", ToastType.Warning);
                return;
            }

            string note = txtQuickNote?.Text?.Trim();
            if (string.IsNullOrEmpty(note))
            {
                ShowToast("Wpisz notatkę", ToastType.Warning);
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    string sql = "INSERT INTO Notatki (IndeksID, TypID, Tresc, KtoStworzyl, DataUtworzenia) VALUES (@lp, 1, @tresc, @kto, GETDATE())";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@lp", _selectedLP);
                        cmd.Parameters.AddWithValue("@tresc", note);
                        cmd.Parameters.AddWithValue("@kto", UserID ?? "0");
                        await cmd.ExecuteNonQueryAsync(_cts.Token);
                    }
                }

                await Dispatcher.InvokeAsync(() => txtQuickNote.Text = "");
                ShowToast("Notatka dodana", ToastType.Success);
                await LoadNotatkiAsync(_selectedLP);
                await LoadOstatnieNotatkiAsync();
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd: {ex.Message}", ToastType.Error);
            }
        }

        #endregion

        #region Statistics Panel

        private async Task LoadStatisticsAsync()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);

                    // Statystyki tygodniowe
                    string sql = @"
                        SELECT
                            COUNT(*) as TotalDeliveries,
                            SUM(Auta) as TotalAuta,
                            SUM(SztukiDek) as TotalSztuki,
                            AVG(WagaDek) as AvgWaga,
                            SUM(CASE WHEN bufor = 'Potwierdzony' THEN 1 ELSE 0 END) as Potwierdzone,
                            SUM(CASE WHEN bufor = 'Anulowany' THEN 1 ELSE 0 END) as Anulowane
                        FROM HarmonogramDostaw
                        WHERE DataOdbioru >= DATEADD(day, -7, GETDATE())";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync(_cts.Token))
                    {
                        if (await reader.ReadAsync(_cts.Token))
                        {
                            await Dispatcher.InvokeAsync(() =>
                            {
                                if (txtStatTotal != null)
                                    txtStatTotal.Text = reader["TotalDeliveries"]?.ToString() ?? "0";
                                if (txtStatAuta != null)
                                    txtStatAuta.Text = reader["TotalAuta"]?.ToString() ?? "0";
                                if (txtStatSztuki != null)
                                    txtStatSztuki.Text = $"{Convert.ToDouble(reader["TotalSztuki"] ?? 0):#,0}";
                                if (txtStatAvgWaga != null)
                                    txtStatAvgWaga.Text = $"{Convert.ToDecimal(reader["AvgWaga"] ?? 0):F2} kg";
                                if (txtStatPotwierdzone != null)
                                    txtStatPotwierdzone.Text = reader["Potwierdzone"]?.ToString() ?? "0";
                                if (txtStatAnulowane != null)
                                    txtStatAnulowane.Text = reader["Anulowane"]?.ToString() ?? "0";
                            });
                        }
                    }
                }
            }
            catch { }
        }

        #endregion

        #region Pomocnicze

        private void DuplicateSelectedDelivery()
        {
            if (string.IsNullOrEmpty(_selectedLP))
            {
                ShowToast("Wybierz dostawę", ToastType.Warning);
                return;
            }
            BtnDuplikuj_Click(null, null);
        }

        private void CreateNewDelivery()
        {
            BtnNowaDostawa_Click(null, null);
        }

        private void DeleteSelectedDelivery()
        {
            if (string.IsNullOrEmpty(_selectedLP))
            {
                ShowToast("Wybierz dostawę", ToastType.Warning);
                return;
            }
            BtnUsun_Click(null, null);
        }

        private void ChangeSelectedDeliveryDate(int days)
        {
            if (string.IsNullOrEmpty(_selectedLP))
            {
                ShowToast("Wybierz dostawę", ToastType.Warning);
                return;
            }
            ChangeDeliveryDate(_selectedLP, days);
            _ = LoadDostawyAsync();
        }

        private async Task<string> GetUserNameByIdAsync(string userId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    using (SqlCommand cmd = new SqlCommand("SELECT Name FROM operators WHERE ID = @id", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", userId);
                        var result = await cmd.ExecuteScalarAsync(_cts.Token);
                        return result?.ToString() ?? "-";
                    }
                }
            }
            catch { return "-"; }
        }

        private string GetUserNameById(string userId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("SELECT Name FROM operators WHERE ID = @id", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", userId);
                        return cmd.ExecuteScalar()?.ToString() ?? "-";
                    }
                }
            }
            catch { return "-"; }
        }

        #endregion

        #region Cleanup

        protected override void OnClosed(EventArgs e)
        {
            // Anuluj wszystkie async operacje
            _cts?.Cancel();
            _cts?.Dispose();

            // Zatrzymaj timery
            _refreshTimer?.Stop();
            _priceTimer?.Stop();
            _surveyTimer?.Stop();
            _countdownTimer?.Stop();

            base.OnClosed(e);
        }

        #endregion
    }

    #region Helper Classes

    public enum ToastType
    {
        Info,
        Success,
        Warning,
        Error
    }

    public class ToastMessage
    {
        public string Message { get; set; }
        public ToastType Type { get; set; }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);

        public void Execute(object parameter) => _execute(parameter);
    }

    #endregion

    #region Modele danych

    public class DostawaModel : INotifyPropertyChanged
    {
        public string LP { get; set; }
        public DateTime DataOdbioru { get; set; }
        public string Dostawca { get; set; }
        public int Auta { get; set; }
        public double SztukiDek { get; set; }
        public decimal WagaDek { get; set; }
        public string Bufor { get; set; }
        public string TypCeny { get; set; }
        public decimal Cena { get; set; }
        public int Distance { get; set; }
        public string Uwagi { get; set; }
        public DateTime? DataNotatki { get; set; }
        public int? RoznicaDni { get; set; }
        public string LpW { get; set; }
        public bool PotwWaga { get; set; }
        public bool PotwSztuki { get; set; }
        public int Ubytek { get; set; }

        private bool _isConfirmed;
        public bool IsConfirmed { get => _isConfirmed; set { _isConfirmed = value; OnPropertyChanged(); } }

        private bool _isWstawienieConfirmed;
        public bool IsWstawienieConfirmed { get => _isWstawienieConfirmed; set { _isWstawienieConfirmed = value; OnPropertyChanged(); } }

        public bool IsHeaderRow { get; set; }
        public bool IsSeparator { get; set; }
        public bool IsEmptyDay { get; set; }

        // Pola dla sum i średnich w nagłówku dnia
        public double SumaAuta { get; set; }
        public double SumaSztuki { get; set; }
        public double SredniaWaga { get; set; }
        public double SredniaCena { get; set; }
        public double SredniaKM { get; set; }
        public double SredniaDoby { get; set; }
        public int SumaUbytek { get; set; }
        public int LiczbaSprzedanych { get; set; }
        public int LiczbaAnulowanych { get; set; }
        public bool HasBothCounts => IsHeaderRow && !IsSeparator && LiczbaSprzedanych > 0 && LiczbaAnulowanych > 0;

        // Główna kolumna - dla nagłówka pokazuje datę, dla danych pokazuje dostawcę
        private static readonly string[] DniSkrot = { "niedz.", "pon.", "wt.", "śr.", "czw.", "pt.", "sob." };
        public string DostawcaDisplay => IsHeaderRow && !IsSeparator
            ? $"{DniSkrot[(int)DataOdbioru.DayOfWeek]} {DataOdbioru:dd.MM}"
            : (IsSeparator ? "" : Dostawca);

        public string SztukiDekDisplay => IsHeaderRow
            ? (SumaSztuki > 0 ? $"{SumaSztuki:#,0}" : "")
            : (SztukiDek > 0 ? $"{SztukiDek:#,0}" : "");
        public string WagaDekDisplay => IsHeaderRow
            ? (SredniaWaga > 0 ? $"{SredniaWaga:0.00}" : "")
            : (WagaDek > 0 ? $"{WagaDek:0.00}" : "");
        public string CenaDisplay => IsHeaderRow
            ? (SredniaCena > 0 ? $"{SredniaCena:0.00}" : "")
            : (Cena > 0 ? $"{Cena:0.00}" : "");
        public string KmDisplay => IsHeaderRow
            ? (SredniaKM > 0 ? $"{SredniaKM:0}" : "")
            : (Distance > 0 ? $"{Distance}" : "");
        public string RoznicaDniDisplay => IsHeaderRow
            ? (SumaUbytek > 0 ? $"Ub:{SumaUbytek}" : "")
            : (RoznicaDni.HasValue ? $"{RoznicaDni}" : "");
        public string AutaDisplay => IsHeaderRow
            ? (SumaAuta > 0 ? $"{SumaAuta:0}" : "")
            : (Auta > 0 ? Auta.ToString() : "");
        public string TypCenyDisplay => IsHeaderRow ? "" : GetTypCenyAbbrev(TypCeny);
        private static string GetTypCenyAbbrev(string typ)
        {
            if (string.IsNullOrEmpty(typ)) return "";
            var lower = typ.ToLowerInvariant();
            if (lower.Contains("wolny")) return "wol.";
            if (lower.Contains("rolnic")) return "rol.";
            if (lower.Contains("minister")) return "mini.";
            if (lower.Contains("łącz") || lower.Contains("laczo")) return "łącz.";
            return typ;
        }
        public string UwagiDisplay => IsHeaderRow && !IsSeparator
            ? BuildHeaderUwagiDisplay()
            : Uwagi;

        private string BuildHeaderUwagiDisplay()
        {
            var parts = new List<string>();
            if (LiczbaSprzedanych > 0) parts.Add($"S:{LiczbaSprzedanych}");
            if (LiczbaAnulowanych > 0) parts.Add($"A:{LiczbaAnulowanych}");
            return string.Join(" ", parts);
        }

        // Właściwość sprawdzająca czy notatka została dodana w ciągu ostatnich 3 dni
        // Notatka z ostatniego 1 dnia - pulsująca czerwona
        public bool IsVeryRecentNote => DataNotatki.HasValue && (DateTime.Now - DataNotatki.Value).TotalDays <= 1;

        // Notatka z 2-3 dni - żółta bez pulsowania
        public bool IsRecentNote => DataNotatki.HasValue && (DateTime.Now - DataNotatki.Value).TotalDays > 1 && (DateTime.Now - DataNotatki.Value).TotalDays <= 3;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class PartiaModel
    {
        public string Partia { get; set; }
        public string PartiaFull { get; set; }
        public string Dostawca { get; set; }
        public decimal Srednia { get; set; }
        public decimal Zywiec { get; set; }
        public decimal Roznica { get; set; }
        public int? Skrzydla { get; set; }
        public int? Nogi { get; set; }
        public int? Oparzenia { get; set; }
        public decimal? KlasaB { get; set; }
        public decimal? Przekarmienie { get; set; }
        public int PhotoCount { get; set; }
        public string FolderPath { get; set; }

        public string SredniaDisplay => Srednia > 0 ? $"{Srednia:0.00} poj" : "";
        public string ZywiecDisplay => Zywiec > 0 ? $"{Zywiec:0.00} kg" : "";
        public string RoznicaDisplay => $"{Roznica:0.00} kg";
        public string SkrzydlaDisplay => Skrzydla.HasValue ? $"{Skrzydla} pkt" : "";
        public string NogiDisplay => Nogi.HasValue ? $"{Nogi} pkt" : "";
        public string OparzeniaDisplay => Oparzenia.HasValue ? $"{Oparzenia} pkt" : "";
        public string KlasaBDisplay => KlasaB.HasValue ? $"{KlasaB:0.##} %" : "";
        public string PrzekarmienieDisplay => Przekarmienie.HasValue ? $"{Przekarmienie:0.00} kg" : "";
        public string ZdjeciaLink => PhotoCount > 0 ? $"Zdjęcia ({PhotoCount})" : "";
    }

    public class NotatkaModel
    {
        public DateTime DataUtworzenia { get; set; }
        public string DataOdbioru { get; set; }
        public string Dostawca { get; set; }
        public string KtoDodal { get; set; }
        public string Tresc { get; set; }
    }

    public class RankingModel
    {
        public int Pozycja { get; set; }
        public string Dostawca { get; set; }
        public string SredniaWaga { get; set; }
        public int LiczbaD { get; set; }
        public int Punkty { get; set; }
    }

    #endregion
}
