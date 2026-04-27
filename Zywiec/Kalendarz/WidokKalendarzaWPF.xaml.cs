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
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
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

        private static readonly string ConnectionStringHandel =
            "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True;" +
            "Connection Timeout=15;Command Timeout=15";

        private ObservableCollection<DostawaModel> _dostawy = new ObservableCollection<DostawaModel>();
        private ObservableCollection<DostawaModel> _dostawyNastepnyTydzien = new ObservableCollection<DostawaModel>();
        private ObservableCollection<PartiaModel> _partie = new ObservableCollection<PartiaModel>();
        private ObservableCollection<DostawaModel> _wstawienia = new ObservableCollection<DostawaModel>();
        private ObservableCollection<NotatkaModel> _notatki = new ObservableCollection<NotatkaModel>();
        private ObservableCollection<NotatkaModel> _ostatnieNotatki = new ObservableCollection<NotatkaModel>();
        private ObservableCollection<ZmianaDostawyModel> _zmianyDostawy = new ObservableCollection<ZmianaDostawyModel>();
        private ObservableCollection<RankingModel> _ranking = new ObservableCollection<RankingModel>();

        private DateTime _selectedDate = DateTime.Today;
        private string _selectedLP = null;
        private DispatcherTimer _refreshTimer;
        private DispatcherTimer _priceTimer;
        private DispatcherTimer _surveyTimer;
        private DispatcherTimer _countdownTimer;

        // #26 Smart polling live refresh
        private DispatcherTimer _liveWatchTimer;
        private DateTime? _lastKnownMaxDataMod = null;
        private bool _isWindowActive = true;
        private const int LIVE_POLL_INTERVAL_SECONDS = 15;

        // #26b Live audit notifications — ostatnie widziane AuditID
        private long _lastSeenAuditId = 0;

        // #27 Wątki notatek + @mentions + powiadomienia
        private int? _replyToNotatkaID = null;
        private string _replyToNotatkaSnippet = null;
        private DispatcherTimer _mentionsPollTimer;
        private int _lastMentionCount = 0;
        private const int MENTIONS_POLL_SECONDS = 60;

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

        // Flaga blokująca drag & drop gdy menu kontekstowe jest otwarte lub niedawno zamknięte
        private bool _isContextMenuOpen = false;
        private DateTime _contextMenuClosedTime = DateTime.MinValue;
        private const int CONTEXT_MENU_DRAG_BLOCK_MS = 500; // Blokuj drag przez 500ms po zamknięciu menu

        // Flaga blokująca drag & drop gdy inline edit popup jest otwarty lub niedawno zamknięty
        private DateTime _inlineEditClosedTime = DateTime.MinValue;
        private bool _skipNextDragStart = false;

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
        private static readonly TimeSpan SURVEY_START_2 = new TimeSpan(20, 16, 0);
        private static readonly TimeSpan SURVEY_END_2 = new TimeSpan(20, 46, 0);

        // Paleciak włączony/wyłączony
        private bool _paleciakEnabled = true;

        // Cancellation token dla async operacji
        private CancellationTokenSource _cts = new CancellationTokenSource();
        // Dedykowany CTS dla ładowania szczegółów dostawy (anulowany przy zmianie selekcji)
        private CancellationTokenSource _detailsCts = new CancellationTokenSource();

        // Serwis audytu zmian
        private AuditLogService _auditService;

        // Preferencje użytkownika (filtry, layout, pozycja okna)
        private KalendarzUserPreferences _userPrefs = new KalendarzUserPreferences();
        private bool _prefsLoaded = false;

        // ═══════════════════════════════════════════════════════════════
        // POWIADOMIENIA O ZMIANACH (floating popup) + badge nieprzeczytanych
        // ═══════════════════════════════════════════════════════════════
        private int _unreadChangesCount = 0;

        // Drag-Drop confirmation overlay
        private TaskCompletionSource<bool> _dragDropTcs;

        // ═══════════════════════════════════════════════════════════════
        // TRYB SYMULACJI - testowanie przesunięć bez zapisu do bazy
        // ═══════════════════════════════════════════════════════════════
        private bool _isSimulationMode = false;
        private List<DostawaModel> _simulationBackup = new List<DostawaModel>();
        private List<DostawaModel> _simulationBackupNastepny = new List<DostawaModel>();
        private ObservableCollection<SimulationChange> _simulationChanges = new ObservableCollection<SimulationChange>();
        private int _simulationChangeCount = 0;
        private DateTime _simulationStartTime;
        private Border _simulationBannerElement;

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
            WindowIconHelper.SetIcon(this);

            dgDostawy.ItemsSource = _dostawy;
            dgDostawyNastepny.ItemsSource = _dostawyNastepnyTydzien;
            dgPartie.ItemsSource = _partie;
            dgWstawienia.ItemsSource = _wstawienia;
            dgNotatki.ItemsSource = _notatki;
            lstSimulationChanges.ItemsSource = _simulationChanges;
            dgHistoriaZmianDostawy.ItemsSource = _zmianyDostawy;
            dgRanking.ItemsSource = _ranking;

            SetupComboBoxes();
            SetupTimers();
            SetupKeyboardShortcuts();
            SetupDragDrop();
            SetupGridKeyboardNav();
            // Globalny handler zamykający popup edycji inline przy kliknięciu gdziekolwiek na oknie
            this.PreviewMouseDown += Window_PreviewMouseDown_PopupGuard;
            // Menu kontekstowe jest teraz zdefiniowane w XAML
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

            // Załaduj preferencje użytkownika ZANIM ustawimy widoczność kolumn/checkboxów
            ApplyUserPreferences();

            // Ustaw kalendarz na dziś
            calendarMain.SelectedDate = DateTime.Today;
            _selectedDate = DateTime.Today;
            UpdateWeekNumber();

            // Ustaw widoczność następnego tygodnia (zgodnie z preferencjami / checkboxem)
            if (chkNastepnyTydzien?.IsChecked == true)
            {
                if (colNastepnyTydzien != null) colNastepnyTydzien.Width = new GridLength(1, GridUnitType.Star);
                if (borderNastepnyTydzien != null) borderNastepnyTydzien.Visibility = Visibility.Visible;
            }
            else
            {
                if (colNastepnyTydzien != null) colNastepnyTydzien.Width = new GridLength(0);
                if (borderNastepnyTydzien != null) borderNastepnyTydzien.Visibility = Visibility.Collapsed;
            }

            // Załaduj dane asynchronicznie
            await LoadAllDataAsync();

            // Zainicjalizuj live audit notifications
            await InitLiveAuditAsync();

            // Subskrybuj kliknięcie w popup powiadomienia → skok do dostawy
            ChangeNotificationPopup.NotificationClicked += OnNotificationPopupClicked;

            // Sprawdź ankietę
            TryShowSurveyIfInWindow();

            // Wygeneruj mini-mapę tygodni
            GenerateWeekMap();

            // #27 Sprawdź nieprzeczytane wzmianki na starcie (badge)
            _lastMentionCount = -1; // -1 = nie triggeruj toast przy pierwszym uruchomieniu
            _ = CheckMentionsTickAsync();

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
            _priceTimer.Tick += async (s, e) => { await LoadCenyAsync(); await LoadCenyDodatkoweAsync(); await LoadPartieAsync(); };
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

            // #26 Smart polling live refresh - co 15s sprawdza MAX(DataMod) na serwerze
            _liveWatchTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(LIVE_POLL_INTERVAL_SECONDS) };
            _liveWatchTimer.Tick += async (s, e) => await LiveWatchTickAsync();
            _liveWatchTimer.Start();

            // #27 Mentions polling - co 60s sprawdza nieprzeczytane wzmianki
            _mentionsPollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(MENTIONS_POLL_SECONDS) };
            _mentionsPollTimer.Tick += async (s, e) => await CheckMentionsTickAsync();
            _mentionsPollTimer.Start();

            // Subskrybuj na aktywację/dezaktywację okna aby pauzować polling
            this.Activated += (s, e) =>
            {
                _isWindowActive = true;
                SetLiveIndicator(true);
                ClearUnreadBadge();
                // Zatrzymaj migający pasek zadań po aktywacji okna
                WindowFlasher.StopFlashing(this);
            };
            this.Deactivated += (s, e) => { _isWindowActive = false; SetLiveIndicator(false); };
        }

        /// <summary>
        /// #26 Smart polling - sprawdza MAX(DataMod) na serwerze i cicho odświeża jeśli zmienione.
        /// Pauzuje gdy: okno nieaktywne, tryb symulacji, otwarte popup/context menu.
        /// </summary>
        private async Task LiveWatchTickAsync()
        {
            // Pauza: okno nieaktywne
            if (!_isWindowActive) return;
            // Pauza: tryb symulacji (zmiany są lokalne, nie chcemy ich nadpisywać)
            if (_isSimulationMode) return;
            // Pauza: użytkownik właśnie coś edytuje
            if (_inlineEditPopup != null && _inlineEditPopup.IsOpen) return;
            if (_isContextMenuOpen) return;

            try
            {
                DateTime startOfWeek = _selectedDate.AddDays(-(int)_selectedDate.DayOfWeek + 1);
                if (_selectedDate.DayOfWeek == DayOfWeek.Sunday) startOfWeek = _selectedDate.AddDays(-6);
                DateTime endOfRange = startOfWeek.AddDays(14); // Oba widoczne tygodnie

                DateTime? currentMax = null;
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    string sql = @"SELECT MAX(DataMod) FROM HarmonogramDostaw
                                   WHERE DataOdbioru >= @start AND DataOdbioru < @end";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@start", startOfWeek);
                        cmd.Parameters.AddWithValue("@end", endOfRange);
                        var result = await cmd.ExecuteScalarAsync(_cts.Token);
                        if (result != null && result != DBNull.Value)
                            currentMax = Convert.ToDateTime(result);
                    }
                }

                // Pierwszy tick - tylko zapisz początkowy znacznik
                if (_lastKnownMaxDataMod == null)
                {
                    _lastKnownMaxDataMod = currentMax;
                    return;
                }

                // Brak zmian → nic nie rób
                if (currentMax == null || currentMax == _lastKnownMaxDataMod) return;

                // Zmiana wykryta → cichy reload + mrugnięcie live indicatora
                _lastKnownMaxDataMod = currentMax;
                FlashLiveIndicator();
                await LoadDostawyAsync();

                // Pobierz szczegóły zmian z AuditLog i pokaż powiadomienie
                await ShowOtherUsersChangesAsync();
            }
            catch (OperationCanceledException) { /* ignore */ }
            catch { /* ignore cichy polling */ }
        }

        /// <summary>
        /// #26b Inicjalizacja live audit — zapisz bieżące MAX(AuditID) i zweryfikuj uprawnienia użytkownika.
        /// </summary>
        private async Task InitLiveAuditAsync()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);

                    // Zapisz najwyższe AuditID, żeby nie pokazywać starych zmian
                    using (SqlCommand cmd = new SqlCommand(
                        "SELECT ISNULL(MAX(AuditID), 0) FROM AuditLog_Dostawy", conn))
                    {
                        var result = await cmd.ExecuteScalarAsync(_cts.Token);
                        _lastSeenAuditId = result != null && result != DBNull.Value
                            ? Convert.ToInt64(result) : 0;
                    }
                }
            }
            catch { /* cicha inicjalizacja */ }
        }

        /// <summary>
        /// #26b Pobiera z AuditLog_Dostawy ostatnie zmiany wykonane przez INNYCH użytkowników
        /// i pokazuje je jako powiadomienia. Wywoływane z LiveWatchTickAsync po wykryciu zmian.
        /// </summary>
        private async Task ShowOtherUsersChangesAsync()
        {
            try
            {
                var notifications = new List<ChangeNotificationItem>();

                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);

                    // Pobierz wpisy z audytu nowsze niż ostatni widziany, wykonane przez INNYCH użytkowników
                    string sql = @"
                        SELECT AuditID, UserID, UserName, RekordID, TypOperacji, ZrodloZmiany,
                               NazwaPola, StaraWartosc, NowaWartosc, DodatkoweInfo, DataZmiany
                        FROM AuditLog_Dostawy
                        WHERE AuditID > @lastId
                          AND UserID != @myId
                          AND NazwaTabeli = 'HarmonogramDostaw'
                        ORDER BY AuditID ASC";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@lastId", _lastSeenAuditId);
                        cmd.Parameters.AddWithValue("@myId", UserID ?? "");

                        long maxId = _lastSeenAuditId;

                        using (var reader = await cmd.ExecuteReaderAsync(_cts.Token))
                        {
                            // Grupuj po (UserID + RekordID + DataZmiany zaokrąglona do 5s) — łączy powiązane zmiany
                            var groups = new Dictionary<string, ChangeNotificationItem>();

                            while (await reader.ReadAsync(_cts.Token))
                            {
                                long auditId = Convert.ToInt64(reader["AuditID"]);
                                if (auditId > maxId) maxId = auditId;

                                string userId = reader["UserID"]?.ToString() ?? "";
                                string userName = reader["UserName"]?.ToString() ?? "";
                                string rekordId = reader["RekordID"]?.ToString() ?? "";
                                string typOp = reader["TypOperacji"]?.ToString() ?? "UPDATE";
                                string zrodlo = reader["ZrodloZmiany"]?.ToString() ?? "";
                                string nazwaP = reader["NazwaPola"]?.ToString() ?? "";
                                string stara = reader["StaraWartosc"]?.ToString() ?? "";
                                string nowa = reader["NowaWartosc"]?.ToString() ?? "";
                                DateTime dataZmiany = reader["DataZmiany"] != DBNull.Value
                                    ? Convert.ToDateTime(reader["DataZmiany"]) : DateTime.Now;

                                // Wyciągnij dostawcę i datę odbioru z DodatkoweInfo (JSON) jeśli dostępny
                                string dostawca = "";
                                DateTime? dataOdbioruAudit = null;
                                string dodatkoweInfo = reader["DodatkoweInfo"]?.ToString() ?? "";
                                if (!string.IsNullOrEmpty(dodatkoweInfo))
                                {
                                    try
                                    {
                                        // Prosty parsing — szukaj "Dostawca":"..."
                                        int idx = dodatkoweInfo.IndexOf("\"Dostawca\":");
                                        if (idx >= 0)
                                        {
                                            int start = dodatkoweInfo.IndexOf("\"", idx + 11) + 1;
                                            int end = dodatkoweInfo.IndexOf("\"", start);
                                            if (start > 0 && end > start)
                                                dostawca = dodatkoweInfo.Substring(start, end - start);
                                        }
                                        // Szukaj "DataOdbioru":"..."
                                        int idx2 = dodatkoweInfo.IndexOf("\"DataOdbioru\":");
                                        if (idx2 >= 0)
                                        {
                                            int start = dodatkoweInfo.IndexOf("\"", idx2 + 14) + 1;
                                            int end = dodatkoweInfo.IndexOf("\"", start);
                                            if (start > 0 && end > start)
                                            {
                                                string dStr = dodatkoweInfo.Substring(start, end - start);
                                                if (DateTime.TryParse(dStr, out DateTime parsed))
                                                    dataOdbioruAudit = parsed;
                                            }
                                        }
                                    }
                                    catch { }
                                }

                                // Klucz grupowania: ten sam user + rekord + okno 10s
                                long timeSlot = dataZmiany.Ticks / (TimeSpan.TicksPerSecond * 10);
                                string groupKey = $"{userId}|{rekordId}|{timeSlot}";

                                if (!groups.ContainsKey(groupKey))
                                {
                                    string title = typOp switch
                                    {
                                        "INSERT" => "Nowa dostawa",
                                        "DELETE" => "Usunięto dostawę",
                                        _ => zrodlo.StartsWith("DragDrop") ? "Przeniesiono dostawę"
                                             : zrodlo.StartsWith("Bulk") ? "Operacja masowa"
                                             : "Zmieniono dostawę"
                                    };

                                    var notifType = typOp switch
                                    {
                                        "INSERT" => ChangeNotificationType.FormSave,
                                        "DELETE" => ChangeNotificationType.Delete,
                                        _ => zrodlo.StartsWith("DragDrop") ? ChangeNotificationType.DragDrop
                                             : zrodlo.StartsWith("Bulk") ? ChangeNotificationType.BulkOperation
                                             : zrodlo.StartsWith("Checkbox") ? ChangeNotificationType.Confirmation
                                             : ChangeNotificationType.InlineEdit
                                    };

                                    groups[groupKey] = new ChangeNotificationItem
                                    {
                                        Title = $"{title} ({userName})",
                                        Dostawca = dostawca,
                                        LP = rekordId,
                                        DataOdbioru = dataOdbioruAudit,
                                        UserId = userId,
                                        UserName = userName,
                                        Timestamp = dataZmiany,
                                        NotificationType = notifType,
                                        Changes = new List<FieldChange>()
                                    };
                                }

                                // Dodaj zmianę pola (jeśli nie jest puste)
                                if (!string.IsNullOrEmpty(nazwaP))
                                {
                                    groups[groupKey].Changes.Add(new FieldChange
                                    {
                                        FieldName = MapFieldName(nazwaP),
                                        OldValue = stara,
                                        NewValue = nowa
                                    });
                                }

                                // Aktualizuj dostawcę jeśli był pusty
                                if (!string.IsNullOrEmpty(dostawca) && string.IsNullOrEmpty(groups[groupKey].Dostawca))
                                    groups[groupKey].Dostawca = dostawca;
                            }

                            notifications.AddRange(groups.Values);
                        }

                        _lastSeenAuditId = maxId;
                    }
                }

                // Pokaż powiadomienia (max 3 naraz, żeby nie zasypać)
                int shown = 0;
                foreach (var notif in notifications.OrderByDescending(n => n.Timestamp))
                {
                    if (shown >= 3) break;
                    if (notif.Changes.Count == 0) continue;
                    ShowChangeNotification(notif);
                    shown++;
                }

                // Jeśli było więcej niż 3, pokaż toast z podsumowaniem
                if (notifications.Count > 3)
                {
                    ShowToast($"🔄 +{notifications.Count - 3} zmian od innych użytkowników", ToastType.Info);
                }

                // Aktualizuj badge nieprzeczytanych zmian
                if (notifications.Count > 0)
                {
                    _unreadChangesCount += notifications.Count;
                    Dispatcher.InvokeAsync(() => UpdateUnreadBadge());

                    // FlashWindow: mignij paskiem zadań żeby zwrócić uwagę użytkownika
                    // gdy okno jest nieaktywne lub zminimalizowane
                    Dispatcher.InvokeAsync(() =>
                    {
                        if (!this.IsActive || this.WindowState == WindowState.Minimized)
                        {
                            WindowFlasher.Flash(this, count: 5);
                        }
                    });
                }
            }
            catch (OperationCanceledException) { }
            catch { /* cichy polling */ }
        }

        /// <summary>
        /// Ustawia wizualny stan wskaźnika LIVE (aktywny/pauzowany)
        /// </summary>
        private void SetLiveIndicator(bool active)
        {
            if (borderLiveIndicator == null || ellipseLiveDot == null) return;
            if (active)
            {
                borderLiveIndicator.Background = new SolidColorBrush(Color.FromRgb(220, 252, 231));
                ellipseLiveDot.Fill = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                borderLiveIndicator.ToolTip = "Live refresh aktywny";
            }
            else
            {
                borderLiveIndicator.Background = new SolidColorBrush(Color.FromRgb(229, 231, 235));
                ellipseLiveDot.Fill = new SolidColorBrush(Color.FromRgb(156, 163, 175));
                borderLiveIndicator.ToolTip = "Live refresh pauzowany (okno nieaktywne lub tryb symulacji)";
            }
        }

        /// <summary>
        /// Krótkie mrugnięcie wskaźnika live (żółte) gdy wykryto zmianę zewnętrzną
        /// </summary>
        private void FlashLiveIndicator()
        {
            if (ellipseLiveDot == null) return;
            var flash = new ColorAnimation
            {
                From = Color.FromRgb(234, 179, 8),   // żółty
                To = Color.FromRgb(34, 197, 94),     // zielony
                Duration = TimeSpan.FromMilliseconds(800)
            };
            ellipseLiveDot.Fill = new SolidColorBrush(Color.FromRgb(234, 179, 8));
            ((SolidColorBrush)ellipseLiveDot.Fill).BeginAnimation(SolidColorBrush.ColorProperty, flash);
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

            // Aktualizuj banner symulacji (czas trwania)
            if (_isSimulationMode)
                UpdateSimulationBanner();

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
                new KeyGesture(Key.Add, ModifierKeys.Alt)));

            this.InputBindings.Add(new KeyBinding(
                new RelayCommand(o => ChangeSelectedDeliveryDate(-1)),
                new KeyGesture(Key.Subtract, ModifierKeys.Alt)));

            this.InputBindings.Add(new KeyBinding(
                new RelayCommand(o => ChangeSelectedDeliveryDate(1)),
                new KeyGesture(Key.OemPlus, ModifierKeys.Alt)));

            this.InputBindings.Add(new KeyBinding(
                new RelayCommand(o => ChangeSelectedDeliveryDate(-1)),
                new KeyGesture(Key.OemMinus, ModifierKeys.Alt)));
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
            // Obsługa F1 lub Shift+? - okno skrótów klawiszowych
            else if (e.Key == Key.F1 ||
                     (e.Key == Key.OemQuestion && Keyboard.Modifiers == ModifierKeys.Shift))
            {
                ShowSkrotyKlawiszowe();
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

        // Singleton - jedno okno pomocy na raz
        private SkrotyKlawiszoweWindow _skrotyWindow;

        private void ShowSkrotyKlawiszowe()
        {
            // Jeśli już otwarte - zamknij (toggle)
            if (_skrotyWindow != null && _skrotyWindow.IsLoaded)
            {
                try { _skrotyWindow.Close(); } catch { }
                _skrotyWindow = null;
                return;
            }

            _skrotyWindow = new SkrotyKlawiszoweWindow { Owner = this };
            _skrotyWindow.Closed += (s, e) => _skrotyWindow = null;
            _skrotyWindow.Show();
        }

        private void BtnSkrotyKlawiszowe_Click(object sender, RoutedEventArgs e)
        {
            ShowSkrotyKlawiszowe();
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

        private void SetupGridKeyboardNav()
        {
            dgDostawy.PreviewKeyDown += DgDostawy_NavKeyDown;
            dgDostawyNastepny.PreviewKeyDown += DgDostawy_NavKeyDown;
        }

        // Nazwy nagłówków kolumn edytowalnych (w kolejności nawigacji Tab)
        private static readonly string[] _editableColumnHeaders = { "🚛", "🐔 Szt", "⚖️ Waga", "💰 Cena" };

        private void DgDostawy_NavKeyDown(object sender, KeyEventArgs e)
        {
            var dg = sender as DataGrid;
            if (dg == null) return;

            var items = dg.Items;
            int currentIndex = dg.SelectedIndex;

            // Strzałka góra/dół - przeskocz nagłówki i separatory
            if (e.Key == Key.Down || e.Key == Key.Up)
            {
                int direction = e.Key == Key.Down ? 1 : -1;
                int nextIndex = currentIndex + direction;

                while (nextIndex >= 0 && nextIndex < items.Count)
                {
                    var candidate = items[nextIndex] as DostawaModel;
                    if (candidate != null && !candidate.IsHeaderRow && !candidate.IsSeparator)
                    {
                        dg.SelectedIndex = nextIndex;
                        dg.ScrollIntoView(dg.SelectedItem);
                        e.Handled = true;
                        return;
                    }
                    nextIndex += direction;
                }
                e.Handled = true; // nie wychodź poza zakres
                return;
            }

            // Tab / Shift+Tab - przeskok między kolumnami edytowalnymi
            if (e.Key == Key.Tab)
            {
                e.Handled = true;
                bool isShift = Keyboard.Modifiers == ModifierKeys.Shift;
                var selectedItem = dg.SelectedItem as DostawaModel;
                if (selectedItem == null || selectedItem.IsHeaderRow || selectedItem.IsSeparator) return;

                // Znajdź bieżącą kolumnę
                var currentCell = dg.CurrentCell;
                string currentHeader = currentCell.Column?.Header?.ToString() ?? "";
                int colIdx = Array.IndexOf(_editableColumnHeaders, currentHeader);

                if (colIdx < 0)
                {
                    // Nie jest edytowalna - przejdź do pierwszej
                    colIdx = isShift ? _editableColumnHeaders.Length - 1 : 0;
                }
                else
                {
                    colIdx += isShift ? -1 : 1;
                }

                // Przejście do następnego/poprzedniego wiersza danych
                if (colIdx >= _editableColumnHeaders.Length || colIdx < 0)
                {
                    int direction = isShift ? -1 : 1;
                    int nextIndex = dg.SelectedIndex + direction;
                    while (nextIndex >= 0 && nextIndex < items.Count)
                    {
                        var candidate = items[nextIndex] as DostawaModel;
                        if (candidate != null && !candidate.IsHeaderRow && !candidate.IsSeparator)
                        {
                            dg.SelectedIndex = nextIndex;
                            dg.ScrollIntoView(dg.SelectedItem);
                            break;
                        }
                        nextIndex += direction;
                    }
                    colIdx = isShift ? _editableColumnHeaders.Length - 1 : 0;
                }

                // Ustaw focus na docelową kolumnę
                string targetHeader = _editableColumnHeaders[colIdx];
                foreach (var col in dg.Columns)
                {
                    if (col.Header?.ToString() == targetHeader)
                    {
                        dg.CurrentCell = new DataGridCellInfo(dg.SelectedItem, col);
                        break;
                    }
                }
                return;
            }

            // Enter - otwórz inline edit na bieżącej komórce LUB popup przeniesienia daty
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                var selectedItem = dg.SelectedItem as DostawaModel;
                if (selectedItem == null || selectedItem.IsHeaderRow || selectedItem.IsSeparator) return;

                var currentCell = dg.CurrentCell;
                string header = currentCell.Column?.Header?.ToString() ?? "";
                bool isSecond = (dg == dgDostawyNastepny);

                // Znajdź wizualną komórkę
                DataGridCell visualCell = null;
                var row = dg.ItemContainerGenerator.ContainerFromItem(dg.SelectedItem) as DataGridRow;
                if (row != null)
                {
                    var presenter = FindVisualChild<DataGridCellsPresenter>(row);
                    if (presenter != null)
                    {
                        int colIndex = currentCell.Column?.DisplayIndex ?? -1;
                        if (colIndex >= 0)
                            visualCell = presenter.ItemContainerGenerator.ContainerFromIndex(colIndex) as DataGridCell;
                    }
                }

                if (header == "🚛" || header.Contains("Auta"))
                    _ = EditCellValueAsync(selectedItem.LP, "A", isSecond, visualCell);
                else if (header == "🐔 Szt" || header.Contains("Szt"))
                    _ = EditCellValueAsync(selectedItem.LP, "Szt", isSecond, visualCell);
                else if (header == "⚖️ Waga" || header.Contains("Waga"))
                    _ = EditCellValueAsync(selectedItem.LP, "Waga", isSecond, visualCell);
                else if (header == "💰 Cena" || header.Contains("Cena"))
                    _ = EditCenaAsync(selectedItem, isSecond);
                return;
            }
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
                    LoadCenyDodatkoweAsync(),
                    LoadPartieAsync(),
                    LoadPojemnoscTuszkiAsync(),
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
                                    UwagiAutorID = reader["UwagiAutorID"]?.ToString(),
                                    UwagiAutorName = reader["UwagiAutorName"]?.ToString(),
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

                                // Zachowaj oryginalną kolejność z SQL (ORDER BY w BuildDostawyQuery)
                                dostawa.SortOrder = tempList.Count;
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
                    (SELECT TOP 1 N.KtoStworzyl FROM Notatki N WHERE N.IndeksID = HD.Lp ORDER BY N.DataUtworzenia DESC) AS UwagiAutorID,
                    (SELECT TOP 1 O.Name FROM Notatki N LEFT JOIN operators O ON N.KtoStworzyl = O.ID WHERE N.IndeksID = HD.Lp ORDER BY N.DataUtworzenia DESC) AS UwagiAutorName,
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
            // Gdy "Do Wykupienia" odznaczone: ukryj tylko te bez aut (Auta = 0 lub NULL).
            // Wiersze "Do Wykupienia" z Auta > 0 pozostają widoczne.
            if (chkDoWykupienia?.IsChecked != true) sql += " AND NOT (bufor = 'Do Wykupienia' AND (Auta IS NULL OR Auta = 0))";

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

                    double cenaTuszki = await GetLatestPriceAsync(conn, "CenaTuszki", "cena");

                    // Rolnicza, Ministerialna i Łączona - z dnia dzisiejszego
                    double cenaRolnicza = 0, cenaMinister = 0, cenaLaczona = 0;
                    string sqlDzis = @"
                        SELECT
                            (SELECT TOP 1 CAST(Cena AS DECIMAL(10,2)) FROM [LibraNet].[dbo].[CenaRolnicza] WHERE Data = @dzis) AS Rolnicza,
                            (SELECT TOP 1 CAST(Cena AS DECIMAL(10,2)) FROM [LibraNet].[dbo].[CenaMinisterialna] WHERE Data = @dzis) AS Minister";
                    using (SqlCommand cmd = new SqlCommand(sqlDzis, conn))
                    {
                        cmd.Parameters.AddWithValue("@dzis", DateTime.Today);
                        using (var reader = await cmd.ExecuteReaderAsync(_cts.Token))
                        {
                            if (await reader.ReadAsync(_cts.Token))
                            {
                                if (!reader.IsDBNull(0)) cenaRolnicza = Convert.ToDouble(reader.GetValue(0));
                                if (!reader.IsDBNull(1)) cenaMinister = Convert.ToDouble(reader.GetValue(1));
                            }
                        }
                    }
                    cenaLaczona = (cenaRolnicza > 0 && cenaMinister > 0)
                        ? Math.Round((cenaRolnicza + cenaMinister) / 2.0, 2) : 0;

                    System.Diagnostics.Debug.WriteLine($"[LoadCeny] Data={DateTime.Today:yyyy-MM-dd}, Rol={cenaRolnicza}, Min={cenaMinister}, Łącz=({cenaRolnicza}+{cenaMinister})/2={cenaLaczona}");

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
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[LoadCeny] BŁĄD OGÓLNY: {ex.Message}"); }
        }

        // Stara synchroniczna wersja dla kompatybilności
        private void LoadCeny()
        {
            _ = LoadCenyAsync();
        }

        /// <summary>
        /// Ładuje ceny "Nasza Tuszka" (z Handel DB) i "Nasz Wolny Rynek" (z LibraNet) dla wybranego dnia
        /// </summary>
        private async Task LoadCenyDodatkoweAsync()
        {
            try
            {
                var date = _selectedDate;
                double naszaTuszka = 0;
                double naszWolny = 0;

                // Nasza Tuszka - średnia cena sprzedaży Kurczak A z Handel DB
                try
                {
                    using (var conn = new SqlConnection(ConnectionStringHandel))
                    {
                        await conn.OpenAsync(_cts.Token);
                        string sql = @"
                            SELECT CASE WHEN SUM(DP.ilosc) > 0
                                        THEN SUM(DP.wartNetto) / SUM(DP.ilosc)
                                        ELSE 0 END AS SredniaCena
                            FROM [HANDEL].[HM].[DP] DP
                            INNER JOIN [HANDEL].[HM].[TW] TW ON DP.kod = TW.kod
                            WHERE DP.kod = 'Kurczak A'
                              AND TW.katalog = 67095
                              AND CAST(DP.data AS DATE) = @data";
                        using (var cmd = new SqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@data", date.Date);
                            var result = await cmd.ExecuteScalarAsync(_cts.Token);
                            if (result != null && result != DBNull.Value)
                                naszaTuszka = Convert.ToDouble(result);
                        }
                    }
                }
                catch { }

                // Nasz Wolny Rynek - średnia ważona cena dostaw wolnorynkowych z dnia
                try
                {
                    using (var conn = new SqlConnection(ConnectionString))
                    {
                        await conn.OpenAsync(_cts.Token);
                        string sql = @"
                            SELECT CASE WHEN SUM(SztukiDek) > 0
                                        THEN SUM(Cena * SztukiDek) / SUM(SztukiDek)
                                        ELSE 0 END AS SredniaCena
                            FROM [LibraNet].[dbo].[HarmonogramDostaw]
                            WHERE CAST(DataOdbioru AS DATE) = @data
                              AND LOWER(TypCeny) IN ('wolnyrynek', 'wolnorynkowa')
                              AND SztukiDek > 0 AND Cena > 0
                              AND bufor NOT IN ('Anulowany')";
                        using (var cmd = new SqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@data", date.Date);
                            var result = await cmd.ExecuteScalarAsync(_cts.Token);
                            if (result != null && result != DBNull.Value)
                                naszWolny = Convert.ToDouble(result);
                        }
                    }
                }
                catch { }

                await Dispatcher.InvokeAsync(() =>
                {
                    txtNaszaTuszka.Text = naszaTuszka > 0 ? $"{naszaTuszka:F2} zł" : "-";
                    txtNaszWolnyRynek.Text = naszWolny > 0 ? $"{naszWolny:F2} zł" : "-";
                });
            }
            catch { }
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

        private void CmbTypCeny_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            HighlightPropCenaByType();
        }

        /// <summary>
        /// Podświetla pole ceny odpowiadające wybranemu typowi ceny.
        /// Kolory jak na tablicy: rolnicza=zielony, ministerialna=niebieski, łączona=fioletowy.
        /// </summary>
        private void HighlightPropCenaByType()
        {
            string typCeny = cmbTypCeny?.SelectedItem?.ToString()?.ToLowerInvariant() ?? "";

            // Domyślne: szare, nieaktywne
            var defaultBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F4F6"));
            var defaultBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1D5DB"));
            var defaultFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF"));

            // Resetuj wszystkie do domyślnych
            var borderRol = txtPropRolnicza.Parent as System.Windows.Controls.StackPanel;
            var borderMin = txtPropMinister.Parent as System.Windows.Controls.StackPanel;
            var borderLacz = txtPropLaczona.Parent as System.Windows.Controls.StackPanel;

            if (borderRol?.Parent is Border bRol)
            {
                bRol.Background = defaultBg; bRol.BorderBrush = defaultBorder;
                txtPropRolnicza.Foreground = defaultFg;
            }
            if (borderMin?.Parent is Border bMin)
            {
                bMin.Background = defaultBg; bMin.BorderBrush = defaultBorder;
                txtPropMinister.Foreground = defaultFg;
            }
            if (borderLacz?.Parent is Border bLacz)
            {
                bLacz.Background = defaultBg; bLacz.BorderBrush = defaultBorder;
                txtPropLaczona.Foreground = defaultFg;
            }

            // Podświetl aktywne pole wg typu ceny
            if (typCeny.Contains("rolnic") && borderRol?.Parent is Border brRol)
            {
                brRol.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F5E9"));
                brRol.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#66BB6A"));
                txtPropRolnicza.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"));
            }
            else if (typCeny.Contains("minister") && borderMin?.Parent is Border brMin)
            {
                brMin.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E3F2FD"));
                brMin.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#42A5F5"));
                txtPropMinister.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1565C0"));
            }
            else if ((typCeny.Contains("łącz") || typCeny.Contains("laczo")) && borderLacz?.Parent is Border brLacz)
            {
                brLacz.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3E5F5"));
                brLacz.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AB47BC"));
                txtPropLaczona.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7B1FA2"));
            }
        }

        /// <summary>
        /// Pobiera 3 ceny z dnia dostawy: rolniczą, ministerialną i łączoną.
        /// </summary>
        private async Task UpdatePropCenaAsync()
        {
            try
            {
                DateTime? dataOdbioru = dpData.SelectedDate;

                if (dataOdbioru == null)
                {
                    txtPropRolnicza.Text = "-";
                    txtPropMinister.Text = "-";
                    txtPropLaczona.Text = "-";
                    HighlightPropCenaByType();
                    System.Diagnostics.Debug.WriteLine("[UpdatePropCena] dpData.SelectedDate is null");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[UpdatePropCena] Pobieram ceny dla daty: {dataOdbioru.Value:yyyy-MM-dd}");

                double cenaRol = 0, cenaMin = 0;

                // Pobierz ceny dokładnie z dnia dostawy
                string sql = @"
                    SELECT
                        (SELECT TOP 1 CAST(Cena AS DECIMAL(10,2)) FROM [LibraNet].[dbo].[CenaRolnicza] WHERE Data = @data) AS Rolnicza,
                        (SELECT TOP 1 CAST(Cena AS DECIMAL(10,2)) FROM [LibraNet].[dbo].[CenaMinisterialna] WHERE Data = @data) AS Minister";

                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@data", dataOdbioru.Value.Date);
                        using (var reader = await cmd.ExecuteReaderAsync(_cts.Token))
                        {
                            if (await reader.ReadAsync(_cts.Token))
                            {
                                if (!reader.IsDBNull(0)) cenaRol = Convert.ToDouble(reader.GetValue(0));
                                if (!reader.IsDBNull(1)) cenaMin = Convert.ToDouble(reader.GetValue(1));
                                System.Diagnostics.Debug.WriteLine($"[UpdatePropCena] Rol={cenaRol}, Min={cenaMin}");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("[UpdatePropCena] Brak wyników z zapytania SQL");
                            }
                        }
                    }
                }

                double cenaLacz = (cenaRol > 0 && cenaMin > 0) ? Math.Round((cenaRol + cenaMin) / 2.0, 2) : 0;

                txtPropRolnicza.Text = cenaRol > 0 ? $"{cenaRol:F2}" : "-";
                txtPropMinister.Text = cenaMin > 0 ? $"{cenaMin:F2}" : "-";
                txtPropLaczona.Text = cenaLacz > 0 ? $"{cenaLacz:F2}" : "-";

                System.Diagnostics.Debug.WriteLine($"[UpdatePropCena] Wynik: Rol={txtPropRolnicza.Text}, Min={txtPropMinister.Text}, Łącz={txtPropLaczona.Text}");

                HighlightPropCenaByType();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdatePropCena] BŁĄD: {ex.Message}\n{ex.StackTrace}");
                txtPropRolnicza.Text = "ERR";
                txtPropMinister.Text = "ERR";
                txtPropLaczona.Text = "ERR";
            }
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

        // Ładowanie pojemności tuszek dla karty Partie
        private async Task LoadPojemnoscTuszkiAsync(DateTime? data = null)
        {
            try
            {
                DateTime dataPartii = data ?? dpPartieData?.SelectedDate ?? DateTime.Today;
                var tempList = new List<PojemnoscTuszkiModel>();

                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);

                    string sql = @"
                        SELECT
                            k.QntInCont AS Pojemnosc,
                            COUNT(DISTINCT k.GUID) AS Palety
                        FROM [LibraNet].[dbo].[In0E] K
                        JOIN [LibraNet].[dbo].[PartiaDostawca] Partia ON K.P1 = Partia.Partia
                        LEFT JOIN [LibraNet].[dbo].[HarmonogramDostaw] hd
                            ON k.CreateData = hd.DataOdbioru
                            AND Partia.CustomerName = hd.Dostawca
                        WHERE k.ArticleID = 40
                            AND k.QntInCont > 4
                            AND k.CreateData = @DataPartii
                        GROUP BY k.QntInCont
                        ORDER BY k.QntInCont DESC";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@DataPartii", dataPartii.Date);
                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync(_cts.Token))
                        {
                            while (await reader.ReadAsync(_cts.Token))
                            {
                                tempList.Add(new PojemnoscTuszkiModel
                                {
                                    Pojemnosc = reader["Pojemnosc"] != DBNull.Value ? Convert.ToInt32(reader["Pojemnosc"]) : 0,
                                    Palety = reader["Palety"] != DBNull.Value ? Convert.ToInt32(reader["Palety"]) : 0
                                });
                            }
                        }
                    }
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    dgPojemnoscTuszki.ItemsSource = tempList;
                });
            }
            catch { }
        }

        #endregion

        #region Ładowanie danych - Notatki

        private async Task LoadNotatkiAsync(string lpDostawa)
        {
            var token = _detailsCts.Token;
            try
            {
                if (string.IsNullOrEmpty(lpDostawa)) return;

                var tempList = new List<NotatkaModel>();

                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(token);
                    // JOIN do siebie: wyciągnij snippet treści rodzica (pierwsze 40 znaków)
                    string sql = @"SELECT N.NotatkaID, N.ParentNotatkaID, N.DataUtworzenia, N.KtoStworzyl,
                                          O.Name AS KtoDodal, N.Tresc,
                                          LEFT(ISNULL(P.Tresc, ''), 40) AS ParentSnippet
                                   FROM [LibraNet].[dbo].[Notatki] N
                                   LEFT JOIN [LibraNet].[dbo].[operators] O ON N.KtoStworzyl = O.ID
                                   LEFT JOIN [LibraNet].[dbo].[Notatki] P ON N.ParentNotatkaID = P.NotatkaID
                                   WHERE N.IndeksID = @Lp
                                   ORDER BY N.DataUtworzenia DESC";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Lp", lpDostawa);
                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync(token))
                        {
                            while (await reader.ReadAsync(token))
                            {
                                tempList.Add(new NotatkaModel
                                {
                                    NotatkaID = reader["NotatkaID"] != DBNull.Value ? Convert.ToInt32(reader["NotatkaID"]) : 0,
                                    ParentNotatkaID = reader["ParentNotatkaID"] != DBNull.Value ? Convert.ToInt32(reader["ParentNotatkaID"]) : (int?)null,
                                    DataUtworzenia = Convert.ToDateTime(reader["DataUtworzenia"]),
                                    KtoDodal = reader["KtoDodal"]?.ToString(),
                                    KtoDodal_ID = reader["KtoStworzyl"]?.ToString(),
                                    Tresc = reader["Tresc"]?.ToString(),
                                    ParentSnippet = reader["ParentSnippet"]?.ToString()
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
                    string sql = @"SELECT TOP 20 N.DataUtworzenia, N.KtoStworzyl, FORMAT(H.DataOdbioru, 'MM-dd ddd') AS DataOdbioru,
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
                                KtoDodal = reader["KtoDodal"]?.ToString(),
                                KtoDodal_ID = reader["KtoStworzyl"]?.ToString()
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

        private async Task LoadZmianyDostawyAsync(string lpDostawa)
        {
            var token = _detailsCts.Token;
            try
            {
                if (string.IsNullOrEmpty(lpDostawa))
                {
                    await Dispatcher.InvokeAsync(() => _zmianyDostawy.Clear());
                    return;
                }

                var tempList = new List<ZmianaDostawyModel>();

                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(token);

                    // Sprawdź czy tabela istnieje
                    using (var checkCmd = new SqlCommand(
                        "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AuditLog_Dostawy'", conn))
                    {
                        var exists = (int)await checkCmd.ExecuteScalarAsync(token);
                        if (exists == 0) return;
                    }

                    string sql = @"SELECT TOP 50 DataZmiany, UserID, UserName, NazwaPola, StaraWartosc, NowaWartosc
                                   FROM AuditLog_Dostawy
                                   WHERE RekordID = @lp
                                   ORDER BY DataZmiany DESC";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@lp", lpDostawa);
                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync(token))
                        {
                            while (await reader.ReadAsync(token))
                            {
                                tempList.Add(new ZmianaDostawyModel
                                {
                                    DataZmiany = reader.GetDateTime(reader.GetOrdinal("DataZmiany")),
                                    UserID = reader.IsDBNull(reader.GetOrdinal("UserID")) ? "" : reader.GetString(reader.GetOrdinal("UserID")),
                                    UserName = reader.IsDBNull(reader.GetOrdinal("UserName")) ? "" : reader.GetString(reader.GetOrdinal("UserName")),
                                    NazwaPola = reader.IsDBNull(reader.GetOrdinal("NazwaPola")) ? "" : reader.GetString(reader.GetOrdinal("NazwaPola")),
                                    StaraWartosc = reader.IsDBNull(reader.GetOrdinal("StaraWartosc")) ? "" : reader.GetString(reader.GetOrdinal("StaraWartosc")),
                                    NowaWartosc = reader.IsDBNull(reader.GetOrdinal("NowaWartosc")) ? "" : reader.GetString(reader.GetOrdinal("NowaWartosc"))
                                });
                            }
                        }
                    }
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    _zmianyDostawy.Clear();
                    foreach (var z in tempList)
                        _zmianyDostawy.Add(z);
                });
            }
            catch { }
        }

        // Event handler dla ładowania avatarów w notatkach
        private void DgNotatki_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.DataContext is NotatkaModel notatka && !string.IsNullOrEmpty(notatka.KtoDodal_ID))
            {
                // Znajdź elementy avatara w wierszu
                e.Row.Loaded += (s, args) =>
                {
                    try
                    {
                        var presenter = FindVisualChild<System.Windows.Controls.Primitives.DataGridCellsPresenter>(e.Row);
                        if (presenter == null) return;

                        // Znajdź wszystkie Ellipse i Border w wierszu
                        var avatarImage = FindVisualChild<Ellipse>(e.Row, "avatarImage");
                        var avatarBorder = FindVisualChild<Border>(e.Row, "avatarBorder");

                        if (avatarImage != null && avatarBorder != null && UserAvatarManager.HasAvatar(notatka.KtoDodal_ID))
                        {
                            using (var avatar = UserAvatarManager.GetAvatarRounded(notatka.KtoDodal_ID, 40))
                            {
                                if (avatar != null)
                                {
                                    var brush = new ImageBrush(ConvertToImageSource(avatar));
                                    brush.Stretch = Stretch.UniformToFill;
                                    avatarImage.Fill = brush;
                                    avatarImage.Visibility = Visibility.Visible;
                                    avatarBorder.Visibility = Visibility.Collapsed;
                                }
                            }
                        }
                    }
                    catch { }
                };
            }
        }

        // Event handler dla ładowania avatarów w historii zmian
        private void DgHistoriaZmianDostawy_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.DataContext is ZmianaDostawyModel zmiana && !string.IsNullOrEmpty(zmiana.UserID))
            {
                e.Row.Loaded += (s, args) =>
                {
                    try
                    {
                        var avatarImage = FindVisualChild<Ellipse>(e.Row, "avatarZmianaImage");
                        var avatarBorder = FindVisualChild<Border>(e.Row, "avatarZmianaBorder");

                        if (avatarImage != null && avatarBorder != null && UserAvatarManager.HasAvatar(zmiana.UserID))
                        {
                            using (var avatar = UserAvatarManager.GetAvatarRounded(zmiana.UserID, 44))
                            {
                                if (avatar != null)
                                {
                                    var brush = new ImageBrush(ConvertToImageSource(avatar));
                                    brush.Stretch = Stretch.UniformToFill;
                                    avatarImage.Fill = brush;
                                    avatarImage.Visibility = Visibility.Visible;
                                    avatarBorder.Visibility = Visibility.Collapsed;
                                }
                            }
                        }
                    }
                    catch { }
                };
            }
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
                var result = FindVisualChild<T>(child, name);
                if (result != null) return result;
            }
            return null;
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

                                txtWstawienieHeaderData.Text = dataWstaw.ToString("dd.MM");
                                txtWstawienieHeaderSzt.Text = $"{reader["IloscWstawienia"]} szt";
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

        private async Task LoadHodowcyToComboBoxAsync(bool forceReload = false)
        {
            try
            {
                var list = await HodowcyCacheManager.GetAsync(ConnectionString, forceReload);

                await Dispatcher.InvokeAsync(() =>
                {
                    cmbDostawca.Items.Clear();
                    foreach (var h in list)
                        cmbDostawca.Items.Add(h);

                    // Backwards compat - synchroniczne pole nadal działa
                    _hodowcyCache = list;
                    _hodowcyCacheExpiry = DateTime.Now.Add(CACHE_DURATION);
                });
            }
            catch { }
        }

        // Stara synchroniczna wersja dla kompatybilności
        private void LoadHodowcyToComboBox()
        {
            // Synchronicznie z cache jeśli jest, async w tle żeby odświeżyć
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

                    // Zapisz do cache (lokalny + globalny)
                    _hodowcyCache = tempList;
                    _hodowcyCacheExpiry = DateTime.Now.Add(CACHE_DURATION);

                    foreach (var h in tempList)
                        cmbDostawca.Items.Add(h);
                }
            }
            catch { }
        }

        /// <summary>
        /// Ręczne odświeżenie listy hodowców z bazy (przycisk obok ComboBox).
        /// </summary>
        private async void BtnRefreshHodowcy_Click(object sender, RoutedEventArgs e)
        {
            HodowcyCacheManager.Invalidate();
            // Zachowaj wybranego hodowcę
            string previouslySelected = cmbDostawca?.SelectedItem?.ToString();
            await LoadHodowcyToComboBoxAsync(forceReload: true);
            if (!string.IsNullOrEmpty(previouslySelected) && cmbDostawca.Items.Contains(previouslySelected))
                cmbDostawca.SelectedItem = previouslySelected;
            ShowToast("Lista hodowców odświeżona", ToastType.Success);
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

        // Pending selekcja - ustawiana przez NawigujDoDaty, użyta po załadowaniu danych
        private DateTime? _pendingSelectDate;
        private string _pendingSelectDostawca;
        private string _pendingSelectLpW;
        private System.Windows.Threading.DispatcherTimer _selectRetryTimer;
        private int _selectRetryCount;
        private const int SELECT_RETRY_INTERVAL_MS = 100;
        private const int SELECT_RETRY_MAX = 30;  // 30 × 100ms = 3 sekundy

        // Publiczne API: nawigacja do konkretnej daty + opcjonalnie zaznaczenie wiersza dostawy.
        public void NawigujDoDaty(DateTime data, string dostawca = null, string lpW = null)
        {
            try
            {
                // Zatrzymaj poprzedni retry (gdy user szybko klika kolejne dostawy)
                StopSelectRetry();

                _pendingSelectDate = data.Date;
                _pendingSelectDostawca = dostawca;
                _pendingSelectLpW = lpW;

                if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
                Activate();
                Focus();

                bool sameDate = calendarMain != null
                    && calendarMain.SelectedDate.HasValue
                    && calendarMain.SelectedDate.Value.Date == data.Date;

                if (calendarMain != null)
                {
                    calendarMain.DisplayDate = data;
                    calendarMain.SelectedDate = data;
                }

                Topmost = true;
                Dispatcher.BeginInvoke(new Action(() => Topmost = false),
                    System.Windows.Threading.DispatcherPriority.ApplicationIdle);

                // Zawsze startuj retry - to gwarantuje że trafimy nawet gdy:
                // - data jest ta sama (SelectedDatesChanged nie odpali)
                // - dane jeszcze ładują (LoadDostawyAsync trwa)
                // - DataGrid wirtualizuje wiersze (kontener nie istnieje od razu)
                // - refresh timer właśnie odświeża _dostawy
                StartSelectRetry();
            }
            catch { }
        }

        // Zaznacz oczekujący wiersz w dgDostawy po załadowaniu danych tygodnia
        // Próbuje znaleźć i zaznaczyć pending wiersz. Jeśli się nie uda (dane jeszcze ładują, wirtualizacja),
        // uruchamia retry przez 3s w odstępach 100ms.
        private void TrySelectPendingDelivery()
        {
            if (!_pendingSelectDate.HasValue || dgDostawy == null)
            {
                StopSelectRetry();
                return;
            }

            var match = ZnajdzPendingMatch();
            if (match == null)
            {
                // Dane jeszcze nie załadowane - uruchom retry
                StartSelectRetry();
                return;
            }

            // Mamy match - selekcja + scroll
            dgDostawy.SelectedItem = match;
            dgDostawy.ScrollIntoView(match);

            if (dgDostawy.Columns.Count > 0)
            {
                dgDostawy.CurrentCell = new DataGridCellInfo(match, dgDostawy.Columns[0]);
            }

            // Wirtualizacja: kontener może jeszcze nie istnieć - czekaj na warstwę Loaded.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                dgDostawy.UpdateLayout();
                var row = dgDostawy.ItemContainerGenerator.ContainerFromItem(match) as DataGridRow;
                if (row != null)
                {
                    row.Focus();
                    Keyboard.Focus(row);
                }
                else
                {
                    // Jeszcze nie ma kontenera - jedna dodatkowa próba po pełnym layoucie
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var r2 = dgDostawy.ItemContainerGenerator.ContainerFromItem(match) as DataGridRow;
                        if (r2 != null) { r2.Focus(); Keyboard.Focus(r2); }
                        else dgDostawy.Focus();
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);

            // Sukces - posprzątaj
            StopSelectRetry();
            _pendingSelectDate = null;
            _pendingSelectDostawca = null;
            _pendingSelectLpW = null;
        }

        private DostawaModel ZnajdzPendingMatch()
        {
            if (!_pendingSelectDate.HasValue) return null;
            var date = _pendingSelectDate.Value;

            // 1. Najprecyzyjniej: po LpW + dacie (kilka dostaw może być tego samego dnia)
            if (!string.IsNullOrEmpty(_pendingSelectLpW))
            {
                var m = _dostawy.FirstOrDefault(d => d.DataOdbioru.Date == date
                    && d.LpW == _pendingSelectLpW);
                if (m != null) return m;
            }

            // 2. Po dostawcy + dacie
            if (!string.IsNullOrEmpty(_pendingSelectDostawca))
            {
                var m = _dostawy.FirstOrDefault(d => d.DataOdbioru.Date == date
                    && string.Equals(d.Dostawca, _pendingSelectDostawca, StringComparison.OrdinalIgnoreCase));
                if (m != null) return m;
            }

            // 3. Fallback: pierwsza dostawa z tą datą
            return _dostawy.FirstOrDefault(d => d.DataOdbioru.Date == date);
        }

        private void StartSelectRetry()
        {
            if (_selectRetryTimer == null)
            {
                _selectRetryTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(SELECT_RETRY_INTERVAL_MS)
                };
                _selectRetryTimer.Tick += (s, e) =>
                {
                    _selectRetryCount++;
                    if (_selectRetryCount > SELECT_RETRY_MAX || !_pendingSelectDate.HasValue)
                    {
                        // Timeout albo ktoś wyczyścił pending - poddaj się
                        StopSelectRetry();
                        _pendingSelectDate = null;
                        _pendingSelectDostawca = null;
                        _pendingSelectLpW = null;
                        return;
                    }
                    TrySelectPendingDelivery();
                };
            }
            if (!_selectRetryTimer.IsEnabled)
            {
                _selectRetryCount = 0;
                _selectRetryTimer.Start();
            }
        }

        private void StopSelectRetry()
        {
            if (_selectRetryTimer != null && _selectRetryTimer.IsEnabled)
            {
                _selectRetryTimer.Stop();
            }
            _selectRetryCount = 0;
        }

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

                // Po załadowaniu - zaznacz pending wiersz (jeśli ktoś wywołał NawigujDoDaty)
                TrySelectPendingDelivery();
            }
        }

        // Obsługa zmiany daty w DatePicker Partii
        private async void DpPartieData_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;

            if (dpPartieData?.SelectedDate != null)
            {
                await LoadPartieAsync(dpPartieData.SelectedDate.Value);
                await LoadPojemnoscTuszkiAsync(dpPartieData.SelectedDate.Value);
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

            // Bez animacji - szybkie przełączenie
            await LoadDostawyAsync();
        }

        /// <summary>
        /// Animacja przejścia między tygodniami - dwufazowa karuzela
        /// Faza 1: Wyjście starego widoku (slide out)
        /// Faza 2: Załadowanie danych + wejście nowego widoku (slide in)
        /// </summary>
        private async Task AnimateWeekTransition(bool isNextWeek)
        {
            // Bez animacji - szybkie przełączenie
            await LoadDostawyAsync();
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

            // Ustaw widoczność kolumny ceny dla obu tabel
            var showCena = chkPokazCeny?.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            if (colCena != null)
                colCena.Visibility = showCena;
            if (colCenaNastepny != null)
                colCenaNastepny.Visibility = showCena;

            SaveUserPreferences();
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

            SaveUserPreferences();
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

            SaveUserPreferences();
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

            // Ustaw widoczność kolumny ceny dla obu tabel
            var showCena = chkPokazCeny?.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            if (colCena != null)
                colCena.Visibility = showCena;
            if (colCenaNastepny != null)
                colCenaNastepny.Visibility = showCena;

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
                // Anuluj poprzednie ładowanie szczegółów
                _detailsCts.Cancel();
                _detailsCts.Dispose();
                _detailsCts = new CancellationTokenSource();

                _selectedLP = selected.LP;
                _ = LoadDeliveryDetailsAsync(selected.LP);
                _ = LoadNotatkiAsync(selected.LP);
                _ = LoadZmianyDostawyAsync(selected.LP);

                // Aktualizuj nazwę hodowcy w sekcjach Notatki, Wstawienia i Dane dostawy
                string lpDostawca = $"{selected.LP} - {selected.Dostawca ?? ""}";
                txtHodowcaNotatki.Text = lpDostawca;
                txtHodowcaWstawienia.Text = lpDostawca;
                txtHodowcaDaneDostawy.Text = selected.Dostawca ?? "";

                if (!string.IsNullOrEmpty(selected.LpW))
                {
                    cmbLpWstawienia.SelectedItem = selected.LpW;
                    _ = LoadWstawieniaAsync(selected.LpW);
                    // Podświetl wiersze z tym samym LpW
                    HighlightMatchingLpWRows(selected.LpW);
                }
                else
                {
                    ClearLpWHighlights();
                }

                // Podświetl nagłówek dnia
                HighlightDayHeader(dgDostawy, selected.DataOdbioru.Date);
            }
            else
            {
                ClearDayHeaderHighlight();
                ClearLpWHighlights();
            }

            // Aktualizuj status bar z informacją o zaznaczeniu
            if (_selectedLPs.Count > 1)
            {
                if (txtStatusBar != null)
                {
                    int totalAuta = dgDostawy.SelectedItems.OfType<DostawaModel>()
                        .Where(d => !d.IsHeaderRow && !d.IsSeparator)
                        .Sum(d => d.Auta);
                    txtStatusBar.Text = $"Zaznaczono: {_selectedLPs.Count} dostaw | Auta: {totalAuta}";
                }
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
                try
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
                        _highlightedDayHeader.BorderBrush = null;
                        _highlightedDayHeader.BorderThickness = new Thickness(0);
                    }
                }
                catch { /* row mógł zostać zrecyklingowany */ }
                _highlightedDayHeader = null;
            }
        }

        private List<(DataGridRow Row, Storyboard Animation)> _highlightedLpWRows = new List<(DataGridRow, Storyboard)>();

        /// <summary>
        /// Podświetla wszystkie wiersze z tym samym LpW (Lp wstawienia) - pulsująca pogrubiona czcionka
        /// </summary>
        private void HighlightMatchingLpWRows(string lpW)
        {
            // Najpierw wyczyść poprzednie podświetlenia
            ClearLpWHighlights();

            if (string.IsNullOrEmpty(lpW)) return;

            // Podświetl w obu tabelach
            HighlightLpWInDataGrid(dgDostawy, lpW);
            HighlightLpWInDataGrid(dgDostawyNastepny, lpW);
        }

        private void HighlightLpWInDataGrid(DataGrid dg, string lpW)
        {
            foreach (var item in dg.Items)
            {
                var dostawa = item as DostawaModel;
                if (dostawa != null && !dostawa.IsHeaderRow && !dostawa.IsSeparator && dostawa.LpW == lpW)
                {
                    var row = dg.ItemContainerGenerator.ContainerFromItem(item) as DataGridRow;
                    if (row != null)
                    {
                        // Pogrubiona, większa czcionka z pulsującym czarnym kolorem
                        row.FontWeight = FontWeights.Bold;
                        row.FontSize = 13;
                        row.Foreground = new SolidColorBrush(Colors.Black);

                        // Uruchom animację pulsowania koloru czcionki
                        var pulseStoryboard = (Storyboard)FindResource("LpWMatchPulseAnimation");
                        var clonedStoryboard = pulseStoryboard.Clone();
                        Storyboard.SetTarget(clonedStoryboard, row);
                        clonedStoryboard.Begin();

                        _highlightedLpWRows.Add((row, clonedStoryboard));
                    }
                }
            }
        }

        private void ClearLpWHighlights()
        {
            foreach (var (row, animation) in _highlightedLpWRows)
            {
                if (row != null)
                {
                    try
                    {
                        // Zatrzymaj animację
                        animation?.Stop();
                        // Przywróć WSZYSTKIE domyślne wartości (FontSize brakowało!)
                        row.FontWeight = FontWeights.Normal;
                        row.FontSize = 12;
                        row.Foreground = new SolidColorBrush(Color.FromRgb(33, 33, 33));
                    }
                    catch { /* row mógł zostać zrecyklingowany */ }
                }
            }
            _highlightedLpWRows.Clear();
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
                // Anuluj poprzednie ładowanie szczegółów
                _detailsCts.Cancel();
                _detailsCts.Dispose();
                _detailsCts = new CancellationTokenSource();

                _selectedLP = selected.LP;
                _ = LoadDeliveryDetailsAsync(selected.LP);
                _ = LoadNotatkiAsync(selected.LP);
                _ = LoadZmianyDostawyAsync(selected.LP);

                // Aktualizuj nazwę hodowcy w sekcjach Notatki, Wstawienia i Dane dostawy
                string lpDostawca = $"{selected.LP} - {selected.Dostawca ?? ""}";
                txtHodowcaNotatki.Text = lpDostawca;
                txtHodowcaWstawienia.Text = lpDostawca;
                txtHodowcaDaneDostawy.Text = selected.Dostawca ?? "";

                if (!string.IsNullOrEmpty(selected.LpW))
                {
                    cmbLpWstawienia.SelectedItem = selected.LpW;
                    _ = LoadWstawieniaAsync(selected.LpW);
                    // Podświetl wiersze z tym samym LpW
                    HighlightMatchingLpWRows(selected.LpW);
                }
                else
                {
                    ClearLpWHighlights();
                }

                // Podświetl nagłówek dnia
                HighlightDayHeader(dgDostawyNastepny, selected.DataOdbioru.Date);
            }
            else
            {
                ClearDayHeaderHighlight();
                ClearLpWHighlights();
            }
        }

        private async Task LoadDeliveryDetailsAsync(string lp)
        {
            var token = _detailsCts.Token;
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(token);
                    string sql = @"SELECT HD.*, HD.KiedyWaga, HD.KiedySztuki, D.Address, D.PostalCode, D.City, D.Distance, D.Phone1, D.Phone2, D.Phone3,
                                   D.Info1, D.Info2, D.Info3, D.Email, D.TypOsobowosci, D.TypOsobowosci2,
                                   O1.Name as KtoStwoName, O2.Name as KtoModName, O3.Name as KtoWagaName, O4.Name as KtoSztukiName,
                                   WK.isConf as WstawienieIsConf, WK.KtoConf as WstawienieKtoConf, WK.DataConf as WstawienieDataConf, O5.Name as KtoConfName
                                   FROM HarmonogramDostaw HD
                                   LEFT JOIN Dostawcy D ON HD.Dostawca = D.Name
                                   LEFT JOIN operators O1 ON HD.ktoStwo = O1.ID
                                   LEFT JOIN operators O2 ON HD.ktoMod = O2.ID
                                   LEFT JOIN operators O3 ON HD.KtoWaga = O3.ID
                                   LEFT JOIN operators O4 ON HD.KtoSztuki = O4.ID
                                   LEFT JOIN WstawieniaKurczakow WK ON HD.LpW = WK.Lp
                                   LEFT JOIN operators O5 ON WK.KtoConf = O5.ID
                                   WHERE HD.LP = @lp";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@lp", lp);
                        using (SqlDataReader r = await cmd.ExecuteReaderAsync(token))
                        {
                            if (await r.ReadAsync(token))
                            {
                                token.ThrowIfCancellationRequested();
                                await Dispatcher.InvokeAsync(() =>
                                {
                                    // Hodowca
                                    cmbDostawca.SelectedItem = r["Dostawca"]?.ToString();
                                    txtUlicaH.Text = r["Address"]?.ToString();
                                    txtKodPocztowyH.Text = r["PostalCode"]?.ToString();
                                    txtMiejscH.Text = r["City"]?.ToString();
                                    txtKmH.Text = r["Distance"]?.ToString();
                                    txtEmail.Text = r["Email"]?.ToString();
                                    var phone1 = r["Phone1"]?.ToString();
                                    txtTel1.Text = FormatPhoneNumber(phone1);
                                    txtTel2.Text = FormatPhoneNumber(r["Phone2"]?.ToString());
                                    txtTel3.Text = FormatPhoneNumber(r["Phone3"]?.ToString());
                                    txtInfo1.Text = r["Info1"]?.ToString();
                                    txtInfo2.Text = r["Info2"]?.ToString();
                                    txtInfo3.Text = r["Info3"]?.ToString();
                                    cmbOsobowosc1.SelectedItem = r["TypOsobowosci"]?.ToString();
                                    cmbOsobowosc2.SelectedItem = r["TypOsobowosci2"]?.ToString();

                                    // Aktualizuj numer telefonu obok nazwy hodowcy w karcie Dostawa
                                    var dostawcaName = r["Dostawca"]?.ToString() ?? "";
                                    if (!string.IsNullOrWhiteSpace(phone1))
                                        txtHodowcaDaneDostawy.Text = $"{dostawcaName} ({FormatPhoneNumber(phone1)})";
                                    else
                                        txtHodowcaDaneDostawy.Text = dostawcaName;

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
                                    UpdateObliczoneAuta();
                                    _ = UpdatePropCenaAsync();

                                    chkPotwWaga.IsChecked = r["PotwWaga"] != DBNull.Value && Convert.ToBoolean(r["PotwWaga"]);
                                    chkPotwSztuki.IsChecked = r["PotwSztuki"] != DBNull.Value && Convert.ToBoolean(r["PotwSztuki"]);
                                    borderPotwWaga.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(chkPotwWaga.IsChecked == true ? "#C8E6C9" : "#FFCDD2"));
                                    borderPotwSztuki.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(chkPotwSztuki.IsChecked == true ? "#C8E6C9" : "#FFCDD2"));
                                    txtKtoWaga.Text = r["KtoWagaName"]?.ToString();
                                    txtKtoSztuki.Text = r["KtoSztukiName"]?.ToString();

                                    // Info
                                    txtDataStwo.Text = r["DataUtw"] != DBNull.Value ? Convert.ToDateTime(r["DataUtw"]).ToString("yyyy-MM-dd HH:mm") : "";
                                    SetAvatar(avatarStwo, txtAvatarStwo, txtKtoStwo, r["KtoStwoName"]?.ToString(), r["ktoStwo"]?.ToString(), imgAvatarStwo, imgAvatarStwoBrush);
                                    txtDataMod.Text = r["DataMod"] != DBNull.Value ? Convert.ToDateTime(r["DataMod"]).ToString("yyyy-MM-dd HH:mm") : "";
                                    SetAvatar(avatarMod, txtAvatarMod, txtKtoMod, r["KtoModName"]?.ToString(), r["ktoMod"]?.ToString(), imgAvatarMod, imgAvatarModBrush);

                                    // Info potwierdzenia wagi i sztuk
                                    txtDataPotwWaga.Text = r["KiedyWaga"] != DBNull.Value ? Convert.ToDateTime(r["KiedyWaga"]).ToString("yyyy-MM-dd HH:mm") : "";
                                    SetAvatar(avatarPotwWaga, txtAvatarPotwWaga, txtKtoPotwWaga, r["KtoWagaName"]?.ToString(), r["KtoWaga"]?.ToString(), imgAvatarPotwWaga, imgAvatarPotwWagaBrush);
                                    txtDataPotwSztuki.Text = r["KiedySztuki"] != DBNull.Value ? Convert.ToDateTime(r["KiedySztuki"]).ToString("yyyy-MM-dd HH:mm") : "";
                                    SetAvatar(avatarPotwSztuki, txtAvatarPotwSztuki, txtKtoPotwSztuki, r["KtoSztukiName"]?.ToString(), r["KtoSztuki"]?.ToString(), imgAvatarPotwSztuki, imgAvatarPotwSztukiBrush);

                                    // Info potwierdzenia wstawienia
                                    txtDataPotwWstawienie.Text = r["WstawienieDataConf"] != DBNull.Value ? Convert.ToDateTime(r["WstawienieDataConf"]).ToString("yyyy-MM-dd HH:mm") : "";
                                    SetAvatar(avatarPotwWstawienie, txtAvatarPotwWstawienie, txtKtoPotwWstawienie, r["KtoConfName"]?.ToString(), r["WstawienieKtoConf"]?.ToString(), imgAvatarPotwWstawienie, imgAvatarPotwWstawienieBrush);

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
            var token = _detailsCts.Token;
            try
            {
                if (string.IsNullOrEmpty(lpWstawienia)) return;

                double sumaSztuk = 0;
                var tempList = new List<DostawaModel>();

                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(token);

                    // Dane wstawienia
                    string sql = "SELECT * FROM dbo.WstawieniaKurczakow WHERE Lp = @lp";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@lp", lpWstawienia);
                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync(token))
                        {
                            if (await reader.ReadAsync(token))
                            {
                                DateTime dataWstaw = Convert.ToDateTime(reader["DataWstawienia"]);
                                string iloscWst = reader["IloscWstawienia"]?.ToString();

                                await Dispatcher.InvokeAsync(() =>
                                {
                                    txtDataWstawienia.Text = dataWstaw.ToString("yyyy-MM-dd");
                                    txtSztukiWstawienia.Text = iloscWst;
                                    txtObecnaDoba.Text = (DateTime.Now - dataWstaw).Days.ToString();
                                    txtWstawienieHeaderData.Text = dataWstaw.ToString("dd.MM");
                                    txtWstawienieHeaderSzt.Text = $"{iloscWst} szt";
                                });
                            }
                        }
                    }

                    // Powiązane dostawy
                    sql = "SELECT LP, DataOdbioru, Auta, SztukiDek, WagaDek, bufor FROM HarmonogramDostaw WHERE LpW = @lp ORDER BY DataOdbioru";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@lp", lpWstawienia);
                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync(token))
                        {
                            while (await reader.ReadAsync(token))
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
            try
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

                // Zablokuj domyślne zachowanie DataGrid (BeginEdit)
                e.Handled = true;

                // Obsługa edycji dla konkretnych kolumn (z obsługą emoji w nagłówkach)
                if (columnHeader == "🚛" || columnHeader.Contains("Auta"))
                {
                    await EditCellValueAsync(selectedItem.LP, "A", isFromSecondTable, cell);
                }
                else if (columnHeader == "🐔 Szt" || columnHeader.Contains("Szt"))
                {
                    await EditCellValueAsync(selectedItem.LP, "Szt", isFromSecondTable, cell);
                }
                else if (columnHeader == "⚖️ Waga" || columnHeader.Contains("Waga"))
                {
                    await EditCellValueAsync(selectedItem.LP, "Waga", isFromSecondTable, cell);
                }
                else if (columnHeader == "📊 Typ" || columnHeader.Contains("Typ"))
                {
                    await EditTypCenyAsync(selectedItem, isFromSecondTable, cell);
                }
                else if (columnHeader == "💰 Cena" || columnHeader.Contains("Cena"))
                {
                    await EditCenaAsync(selectedItem, isFromSecondTable, cell);
                }
                else if (columnHeader == "📝 Uwagi" || columnHeader.Contains("Uwagi"))
                {
                    await EditNoteAsync(selectedItem.LP, cell);
                }
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd: {ex.Message}", ToastType.Error);
            }
        }

        private Popup _inlineEditPopup;

        /// <summary>
        /// Globalny handler: kliknięcie gdziekolwiek na oknie gdy popup jest otwarty
        /// zamyka popup i POCHŁANIA klik (e.Handled=true), więc drag & drop nie wystartuje.
        /// Popup jest w osobnym HWND, więc klik wewnątrz popup NIE trafia tutaj.
        /// </summary>
        private void Window_PreviewMouseDown_PopupGuard(object sender, MouseButtonEventArgs e)
        {
            if (_inlineEditPopup != null && _inlineEditPopup.IsOpen)
            {
                // Sprawdź czy popup jest naprawdę widoczny (safety: nie blokuj kliknięć gdy popup utknął)
                var popupChild = _inlineEditPopup.Child as FrameworkElement;
                if (popupChild == null || !popupChild.IsVisible)
                {
                    // Popup utknął w stanie IsOpen=true ale nie jest widoczny → wyczyść bez blokowania kliknięcia
                    _inlineEditPopup.IsOpen = false;
                    _inlineEditPopup = null;
                    return;
                }

                _inlineEditPopup.IsOpen = false;
                e.Handled = true;
            }
        }

        private void RecalculateDayHeader(ObservableCollection<DostawaModel> collection, DateTime date)
        {
            var header = collection.FirstOrDefault(d => d.IsHeaderRow && !d.IsSeparator && d.DataOdbioru.Date == date.Date);
            if (header == null) return;

            var deliveries = collection.Where(d => !d.IsHeaderRow && !d.IsSeparator && d.DataOdbioru.Date == date.Date).ToList();

            double sumaAuta = 0, sumaSztuki = 0, sumaWagaPomnozona = 0, sumaCenaPomnozona = 0;
            foreach (var d in deliveries)
            {
                sumaAuta += d.Auta;
                sumaSztuki += d.SztukiDek;
                sumaWagaPomnozona += (double)d.WagaDek * d.Auta;
                sumaCenaPomnozona += (double)d.Cena * d.Auta;
            }

            header.SumaAuta = sumaAuta;
            header.SumaSztuki = sumaSztuki;
            header.SredniaWaga = sumaAuta > 0 ? sumaWagaPomnozona / sumaAuta : 0;
            header.SredniaCena = sumaAuta > 0 ? sumaCenaPomnozona / sumaAuta : 0;
        }

        private async Task EditCellValueAsync(string lp, string columnName, bool isFromSecondTable = false, DataGridCell targetCell = null)
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

            // Zamknij poprzedni popup jeśli otwarty
            if (_inlineEditPopup != null && _inlineEditPopup.IsOpen)
                _inlineEditPopup.IsOpen = false;

            var popup = new Popup
            {
                PlacementTarget = targetCell ?? (UIElement)this,
                Placement = targetCell != null ? PlacementMode.Bottom : PlacementMode.Center,
                StaysOpen = true, // Start true - przełączymy na false po zakończeniu zdarzeń myszy z double-click
                AllowsTransparency = true,
                PopupAnimation = PopupAnimation.Fade
            };
            _inlineEditPopup = popup;

            // Domyślne kolory borderu (zmieniane przez walidację)
            var defaultBorderBrush = new SolidColorBrush(Color.FromRgb(33, 150, 243));   // niebieski
            var warnBorderBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11));      // żółty
            var errorBorderBrush = new SolidColorBrush(Color.FromRgb(211, 47, 47));      // czerwony

            var border = new Border
            {
                Background = Brushes.White,
                BorderBrush = defaultBorderBrush,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(2),
                Effect = new DropShadowEffect { BlurRadius = 8, Opacity = 0.3, ShadowDepth = 2 }
            };

            // Layout: TextBox + komunikat walidacji (mały) pod spodem
            var stack = new StackPanel { Orientation = Orientation.Vertical };

            var textBox = new TextBox
            {
                Text = currentValue,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                MinWidth = 70,
                Padding = new Thickness(6, 3, 6, 3),
                BorderThickness = new Thickness(0),
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Right
            };

            var validationMessage = new TextBlock
            {
                FontSize = 10,
                Margin = new Thickness(6, 1, 6, 2),
                MaxWidth = 200,
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed
            };

            stack.Children.Add(textBox);
            stack.Children.Add(validationMessage);
            border.Child = stack;
            popup.Child = border;

            // Stan walidacji - aktualizowany przez TextChanged
            ValidationLevel currentValidationLevel = ValidationLevel.Ok;

            void RunValidation()
            {
                var result = InlineEditValidator.Validate(fieldName, textBox.Text ?? "");
                currentValidationLevel = result.Level;

                switch (result.Level)
                {
                    case ValidationLevel.Ok:
                        border.BorderBrush = defaultBorderBrush;
                        validationMessage.Visibility = Visibility.Collapsed;
                        textBox.ToolTip = null;
                        break;
                    case ValidationLevel.Warning:
                        border.BorderBrush = warnBorderBrush;
                        validationMessage.Foreground = warnBorderBrush;
                        validationMessage.Text = "⚠ " + result.Message;
                        validationMessage.Visibility = Visibility.Visible;
                        textBox.ToolTip = result.Message;
                        break;
                    case ValidationLevel.Error:
                        border.BorderBrush = errorBorderBrush;
                        validationMessage.Foreground = errorBorderBrush;
                        validationMessage.Text = "✗ " + result.Message;
                        validationMessage.Visibility = Visibility.Visible;
                        textBox.ToolTip = result.Message;
                        break;
                }
            }

            textBox.TextChanged += (s, e) => RunValidation();

            bool saved = false;
            bool cancelled = false;

            async Task SaveValueAsync()
            {
                if (saved || cancelled) return;
                string newValue = textBox.Text.Trim().Replace(",", ".");
                if (newValue == currentValue || string.IsNullOrWhiteSpace(newValue))
                    return;

                // Walidacja: blokuj zapis dla Error
                var validation = InlineEditValidator.Validate(fieldName, newValue);
                if (validation.Level == ValidationLevel.Error)
                {
                    ShowToast($"Błędna wartość: {validation.Message}", ToastType.Error);
                    return;
                }

                saved = true;
                try
                {
                    // Parsuj wartość przed zapisem
                    object parsedValue;
                    if (fieldName == "Auta")
                        parsedValue = int.Parse(newValue);
                    else if (fieldName == "SztukiDek")
                        parsedValue = double.Parse(newValue, CultureInfo.InvariantCulture);
                    else
                        parsedValue = decimal.Parse(newValue, CultureInfo.InvariantCulture);

                    using (SqlConnection conn = new SqlConnection(ConnectionString))
                    {
                        await conn.OpenAsync();
                        string sql = $"UPDATE HarmonogramDostaw SET {fieldName} = @val, DataMod = GETDATE(), KtoMod = @kto WHERE LP = @lp";
                        using (SqlCommand cmd = new SqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@val", parsedValue);
                            cmd.Parameters.AddWithValue("@kto", UserID ?? "0");
                            cmd.Parameters.AddWithValue("@lp", lp);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    // Aktualizuj model w pamięci (PropertyChanged odświeży DataGrid natychmiast)
                    switch (fieldName)
                    {
                        case "Auta": item.Auta = (int)parsedValue; break;
                        case "SztukiDek": item.SztukiDek = (double)parsedValue; break;
                        case "WagaDek": item.WagaDek = (decimal)parsedValue; break;
                    }

                    // Przelicz sumy nagłówka dnia
                    RecalculateDayHeader(collection, item.DataOdbioru);

                    // AUDIT LOG
                    if (_auditService != null)
                    {
                        var source = columnName switch
                        {
                            "A" => AuditChangeSource.DoubleClick_Auta,
                            "Szt" => AuditChangeSource.DoubleClick_Sztuki,
                            "Waga" => AuditChangeSource.DoubleClick_Waga,
                            _ => AuditChangeSource.DoubleClick_Auta
                        };
                        // Fire-and-forget audit - nie blokuj UI
                        _ = _auditService.LogFieldChangeAsync(
                            "HarmonogramDostaw", lp, source, fieldName,
                            currentValue, newValue,
                            new AuditContextInfo { Dostawca = item.Dostawca, DataOdbioru = item.DataOdbioru },
                            _cts.Token);
                    }

                    ShowToast($"{columnName} zaktualizowane", ToastType.Success);

                    ShowChangeNotification(new ChangeNotificationItem
                    {
                        Title = "Zmieniono dostawę",
                        Dostawca = item.Dostawca,
                        LP = lp,
                        DataOdbioru = item.DataOdbioru,
                        UserId = UserID,
                        UserName = txtUserName?.Text,
                        NotificationType = ChangeNotificationType.InlineEdit,
                        Changes = { new FieldChange { FieldName = MapFieldName(fieldName), OldValue = currentValue, NewValue = newValue } }
                    });
                }
                catch (Exception ex)
                {
                    ShowToast($"Błąd: {ex.Message}", ToastType.Error);
                }
            }

            textBox.KeyDown += async (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    // Blokuj zapis przy walidacji Error - pokaż tylko toast i zostaw popup otwarty
                    if (currentValidationLevel == ValidationLevel.Error)
                    {
                        ShowToast("Popraw wartość przed zapisem", ToastType.Warning);
                        return;
                    }
                    await SaveValueAsync();
                    if (popup.IsOpen) popup.IsOpen = false;
                }
                if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    cancelled = true;
                    popup.IsOpen = false;
                }
            };

            popup.Closed += (s, e) =>
            {
                _inlineEditClosedTime = DateTime.Now;
                _skipNextDragStart = true;
                // Auto-save przy zamknięciu - tylko jeśli walidacja przechodzi (Ok lub Warning)
                if (!saved && !cancelled && currentValidationLevel != ValidationLevel.Error)
                {
                    string newValue = textBox.Text.Trim().Replace(",", ".");
                    if (newValue != currentValue && !string.IsNullOrWhiteSpace(newValue))
                    {
                        _ = Dispatcher.InvokeAsync(async () =>
                        {
                            try { await SaveValueAsync(); }
                            catch { }
                        });
                    }
                }
            };

            popup.IsOpen = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                textBox.SelectAll();
                textBox.Focus();
            }), DispatcherPriority.Input);

        }

        private async Task EditNoteAsync(string lp, DataGridCell targetCell = null)
        {
            // Zamknij poprzedni popup jeśli otwarty
            if (_inlineEditPopup != null && _inlineEditPopup.IsOpen)
                _inlineEditPopup.IsOpen = false;

            var popup = new Popup
            {
                PlacementTarget = targetCell ?? (UIElement)this,
                Placement = targetCell != null ? PlacementMode.Bottom : PlacementMode.Center,
                StaysOpen = true,
                AllowsTransparency = true,
                PopupAnimation = PopupAnimation.Fade
            };
            _inlineEditPopup = popup;

            var border = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(4),
                Effect = new DropShadowEffect { BlurRadius = 8, Opacity = 0.3, ShadowDepth = 2 }
            };

            // #27 Wątki: layout ze stackiem — belka reply + autocomplete popup + textbox
            var stack = new StackPanel();

            // Belka "Odpowiadasz na..." jeśli reply
            if (_replyToNotatkaID.HasValue)
            {
                var replyBar = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(254, 243, 199)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11)),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Padding = new Thickness(6, 3, 6, 3),
                    Margin = new Thickness(0, 0, 0, 3)
                };
                replyBar.Child = new TextBlock
                {
                    Text = $"↳ odp. do: {_replyToNotatkaSnippet}",
                    FontSize = 10,
                    FontStyle = FontStyles.Italic,
                    Foreground = new SolidColorBrush(Color.FromRgb(146, 64, 14))
                };
                stack.Children.Add(replyBar);
            }

            var textBox = new TextBox
            {
                FontSize = 14,
                MinWidth = 250,
                MaxWidth = 400,
                MinHeight = 60,
                MaxHeight = 120,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = false,
                Padding = new Thickness(6, 3, 6, 3),
                BorderThickness = new Thickness(0),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            // Hint o @mentions
            var hint = new TextBlock
            {
                Text = "💡 Wpisz @ aby oznaczyć pracownika",
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                Margin = new Thickness(6, 2, 6, 0)
            };

            stack.Children.Add(textBox);
            stack.Children.Add(hint);

            border.Child = stack;
            popup.Child = border;

            // Autocomplete popup dla @mentions
            Popup mentionsPopup = null;
            ListBox mentionsListBox = null;
            SetupMentionsAutocomplete(textBox, ref mentionsPopup, ref mentionsListBox);

            bool saved = false;
            bool cancelled = false;

            async Task SaveNoteAsync()
            {
                if (saved || cancelled) return;
                saved = true;
                string noteText = textBox.Text.Trim();
                if (string.IsNullOrEmpty(noteText)) return;

                int? parentId = _replyToNotatkaID;

                try
                {
                    int newNotatkaID = 0;
                    using (SqlConnection conn = new SqlConnection(ConnectionString))
                    {
                        await conn.OpenAsync();
                        // #27 INSERT z ParentNotatkaID + zwraca nowe ID przez SCOPE_IDENTITY
                        string sql = @"INSERT INTO Notatki (IndeksID, TypID, Tresc, KtoStworzyl, DataUtworzenia, ParentNotatkaID)
                                       VALUES (@lp, 1, @tresc, @kto, GETDATE(), @parent);
                                       SELECT CAST(SCOPE_IDENTITY() AS INT);";
                        using (SqlCommand cmd = new SqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@lp", lp);
                            cmd.Parameters.AddWithValue("@tresc", noteText);
                            cmd.Parameters.AddWithValue("@kto", UserID ?? "0");
                            cmd.Parameters.AddWithValue("@parent", (object)parentId ?? DBNull.Value);
                            var result = await cmd.ExecuteScalarAsync();
                            if (result != null && result != DBNull.Value)
                                newNotatkaID = Convert.ToInt32(result);
                        }
                    }

                    // #27 Parsowanie @mentions i zapis do NotatkiMentions
                    if (newNotatkaID > 0)
                    {
                        _ = SaveMentionsAsync(newNotatkaID, noteText);
                    }

                    // Aktualizuj model w pamięci
                    var noteItem = _dostawy.FirstOrDefault(d => d.LP == lp && !d.IsHeaderRow && !d.IsSeparator)
                                ?? _dostawyNastepnyTydzien.FirstOrDefault(d => d.LP == lp && !d.IsHeaderRow && !d.IsSeparator);
                    if (noteItem != null)
                    {
                        noteItem.Uwagi = noteText;
                        noteItem.UwagiAutorID = UserID;
                        noteItem.UwagiAutorName = UserName;
                        noteItem.DataNotatki = DateTime.Now;
                    }

                    // Fire-and-forget audit
                    if (_auditService != null)
                    {
                        _ = _auditService.LogNoteAddedAsync(lp, noteText, AuditChangeSource.DoubleClick_Uwagi,
                            cancellationToken: _cts.Token);
                    }

                    ShowToast(parentId.HasValue ? "💬 Odpowiedź dodana" : "Notatka dodana", ToastType.Success);

                    // Reset stanu reply
                    _replyToNotatkaID = null;
                    _replyToNotatkaSnippet = null;

                    // Odśwież panel notatek w tle
                    _ = Dispatcher.InvokeAsync(async () =>
                    {
                        try
                        {
                            await LoadNotatkiAsync(lp);
                            await LoadZmianyDostawyAsync(lp);
                        }
                        catch { }
                    });
                }
                catch (Exception ex)
                {
                    ShowToast($"Błąd: {ex.Message}", ToastType.Error);
                }
            }

            textBox.KeyDown += async (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    await SaveNoteAsync();
                    if (popup.IsOpen) popup.IsOpen = false;
                }
                if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    cancelled = true;
                    popup.IsOpen = false;
                }
            };

            popup.Closed += (s, e) =>
            {
                _inlineEditClosedTime = DateTime.Now;
                _skipNextDragStart = true;
                if (!saved && !cancelled)
                {
                    string noteText = textBox.Text.Trim();
                    if (!string.IsNullOrEmpty(noteText))
                    {
                        _ = Dispatcher.InvokeAsync(async () =>
                        {
                            try { await SaveNoteAsync(); }
                            catch { }
                        });
                    }
                    else
                    {
                        // Anulowano bez wpisania — zresetuj reply
                        _replyToNotatkaID = null;
                        _replyToNotatkaSnippet = null;
                    }
                }
                else if (cancelled)
                {
                    _replyToNotatkaID = null;
                    _replyToNotatkaSnippet = null;
                }
                if (mentionsPopup != null) mentionsPopup.IsOpen = false;
            };

            popup.IsOpen = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                textBox.Focus();
            }), DispatcherPriority.Input);

        }

        // ═══════════════════════════════════════════════════════════════
        // #27 @mentions + wątki - helpers
        // ═══════════════════════════════════════════════════════════════

        // Cache listy operatorów (ID, Name) do autocomplete
        private static List<(int Id, string Name)> _operatorsCache = null;
        private static DateTime _operatorsCacheExpiry = DateTime.MinValue;

        private async Task<List<(int Id, string Name)>> GetOperatorsAsync()
        {
            if (_operatorsCache != null && _operatorsCacheExpiry > DateTime.Now)
                return _operatorsCache;

            var list = new List<(int, string)>();
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    using (SqlCommand cmd = new SqlCommand("SELECT ID, Name FROM operators WHERE ISNULL(Name,'') <> '' ORDER BY Name", conn))
                    {
                        using (var reader = await cmd.ExecuteReaderAsync(_cts.Token))
                        {
                            while (await reader.ReadAsync(_cts.Token))
                            {
                                int id = reader["ID"] != DBNull.Value ? Convert.ToInt32(reader["ID"]) : 0;
                                string name = reader["Name"]?.ToString();
                                if (id > 0 && !string.IsNullOrWhiteSpace(name))
                                    list.Add((id, name));
                            }
                        }
                    }
                }
                _operatorsCache = list;
                _operatorsCacheExpiry = DateTime.Now.AddMinutes(30);
            }
            catch { }
            return list;
        }

        /// <summary>
        /// Inicjalizuje popup autocomplete dla @mentions — pokazywany przy wpisaniu '@'
        /// </summary>
        private void SetupMentionsAutocomplete(TextBox textBox, ref Popup mentionsPopup, ref ListBox listBox)
        {
            var popup = new Popup
            {
                PlacementTarget = textBox,
                Placement = PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true
            };
            var popupBorder = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(59, 130, 246)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(2),
                MaxHeight = 180,
                MinWidth = 180,
                Effect = new DropShadowEffect { BlurRadius = 6, Opacity = 0.3, ShadowDepth = 2 }
            };
            var lb = new ListBox
            {
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent
            };
            popupBorder.Child = lb;
            popup.Child = popupBorder;
            mentionsPopup = popup;
            listBox = lb;

            var popupCapture = popup;
            var lbCapture = lb;

            textBox.TextChanged += async (s, e) =>
            {
                string text = textBox.Text;
                int caret = textBox.CaretIndex;

                // Szukaj ostatniego @ przed kursorem (bez spacji między)
                int atIdx = -1;
                for (int i = caret - 1; i >= 0; i--)
                {
                    char c = text[i];
                    if (c == '@') { atIdx = i; break; }
                    if (char.IsWhiteSpace(c)) break;
                }

                if (atIdx < 0)
                {
                    popupCapture.IsOpen = false;
                    return;
                }

                string query = text.Substring(atIdx + 1, caret - atIdx - 1);
                var operators = await GetOperatorsAsync();
                var matches = operators
                    .Where(o => string.IsNullOrEmpty(query) ||
                                o.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Take(8)
                    .ToList();

                lbCapture.Items.Clear();
                foreach (var op in matches)
                {
                    var item = new ListBoxItem
                    {
                        Content = op.Name,
                        Tag = op,
                        Padding = new Thickness(6, 3, 6, 3),
                        FontSize = 12
                    };
                    lbCapture.Items.Add(item);
                }

                if (matches.Count == 0)
                {
                    popupCapture.IsOpen = false;
                }
                else
                {
                    popupCapture.IsOpen = true;
                    lbCapture.SelectedIndex = 0;
                }
            };

            textBox.PreviewKeyDown += (s, e) =>
            {
                if (!popupCapture.IsOpen) return;

                if (e.Key == Key.Down)
                {
                    lbCapture.SelectedIndex = Math.Min(lbCapture.SelectedIndex + 1, lbCapture.Items.Count - 1);
                    e.Handled = true;
                }
                else if (e.Key == Key.Up)
                {
                    lbCapture.SelectedIndex = Math.Max(lbCapture.SelectedIndex - 1, 0);
                    e.Handled = true;
                }
                else if (e.Key == Key.Enter || e.Key == Key.Tab)
                {
                    if (lbCapture.SelectedItem is ListBoxItem item && item.Tag is ValueTuple<int, string> op)
                    {
                        InsertMentionIntoTextBox(textBox, op.Item2);
                        popupCapture.IsOpen = false;
                        e.Handled = true;
                    }
                }
                else if (e.Key == Key.Escape)
                {
                    popupCapture.IsOpen = false;
                    e.Handled = true;
                }
            };

            lb.MouseLeftButtonUp += (s, e) =>
            {
                if (lbCapture.SelectedItem is ListBoxItem item && item.Tag is ValueTuple<int, string> op)
                {
                    InsertMentionIntoTextBox(textBox, op.Item2);
                    popupCapture.IsOpen = false;
                }
            };
        }

        /// <summary>
        /// Wstawia wybrane @nazwisko w miejsce zaczynające się od @
        /// </summary>
        private void InsertMentionIntoTextBox(TextBox textBox, string name)
        {
            string text = textBox.Text;
            int caret = textBox.CaretIndex;

            int atIdx = -1;
            for (int i = caret - 1; i >= 0; i--)
            {
                char c = text[i];
                if (c == '@') { atIdx = i; break; }
                if (char.IsWhiteSpace(c)) break;
            }
            if (atIdx < 0) return;

            string before = text.Substring(0, atIdx);
            string after = text.Substring(caret);
            string mention = $"@{name} ";
            textBox.Text = before + mention + after;
            textBox.CaretIndex = (before + mention).Length;
        }

        /// <summary>
        /// Menu kontekstowe "Odpowiedz na notatkę" — ustawia stan reply i otwiera popup dodawania notatki
        /// </summary>
        private void MenuReplyToNote_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgNotatki.SelectedItem as NotatkaModel;
            if (selected == null)
            {
                ShowToast("Wybierz notatkę", ToastType.Warning);
                return;
            }
            if (string.IsNullOrEmpty(_selectedLP))
            {
                ShowToast("Brak wybranej dostawy", ToastType.Warning);
                return;
            }

            _replyToNotatkaID = selected.NotatkaID;
            _replyToNotatkaSnippet = selected.Tresc?.Length > 40
                ? selected.Tresc.Substring(0, 40) + "..."
                : selected.Tresc;

            _ = EditNoteAsync(_selectedLP);
        }

        /// <summary>
        /// Klik w badge powiadomień — pokazuje listę nieprzeczytanych wzmianek i pozwala nawigować
        /// </summary>
        private async void BtnMentionsBadge_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var mentions = await LoadUnreadMentionsAsync();
                if (mentions.Count == 0)
                {
                    ShowToast("Brak nieprzeczytanych wzmianek", ToastType.Info);
                    return;
                }

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Masz {mentions.Count} nieprzeczytanych wzmianek:\n");
                foreach (var m in mentions.Take(10))
                {
                    sb.AppendLine($"• [{m.CreatedAt:dd.MM HH:mm}] {m.Dostawca ?? "?"}: {m.Tresc}");
                }
                if (mentions.Count > 10) sb.AppendLine($"\n...i {mentions.Count - 10} więcej");
                sb.AppendLine("\nKliknij OK aby oznaczyć wszystkie jako przeczytane.");

                var result = MessageBox.Show(sb.ToString(), "🔔 Wzmianki",
                    MessageBoxButton.OKCancel, MessageBoxImage.Information);
                if (result == MessageBoxResult.OK)
                {
                    await MarkAllMentionsReadAsync();
                    _lastMentionCount = 0;
                    UpdateMentionsBadge(0);

                    // Nawiguj do pierwszej wzmianki jeśli ma LP
                    var first = mentions.FirstOrDefault();
                    if (first != null && !string.IsNullOrEmpty(first.LP))
                        NavigateToDelivery(first.LP);
                }
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd: {ex.Message}", ToastType.Error);
            }
        }

        private class UnreadMention
        {
            public int MentionID { get; set; }
            public int NotatkaID { get; set; }
            public string LP { get; set; }
            public string Dostawca { get; set; }
            public string Tresc { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        /// <summary>
        /// Pobiera listę nieprzeczytanych wzmianek dla bieżącego użytkownika
        /// </summary>
        private async Task<List<UnreadMention>> LoadUnreadMentionsAsync()
        {
            var list = new List<UnreadMention>();
            if (string.IsNullOrEmpty(UserID) || !int.TryParse(UserID, out int userId)) return list;

            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    string sql = @"SELECT M.MentionID, M.NotatkaID, N.IndeksID AS LP, HD.Dostawca, N.Tresc, M.CreatedAt
                                   FROM NotatkiMentions M
                                   INNER JOIN Notatki N ON M.NotatkaID = N.NotatkaID
                                   LEFT JOIN HarmonogramDostaw HD ON N.IndeksID = HD.LP
                                   WHERE M.MentionedUserID = @uid AND M.IsRead = 0
                                   ORDER BY M.CreatedAt DESC";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@uid", userId);
                        using (var reader = await cmd.ExecuteReaderAsync(_cts.Token))
                        {
                            while (await reader.ReadAsync(_cts.Token))
                            {
                                list.Add(new UnreadMention
                                {
                                    MentionID = Convert.ToInt32(reader["MentionID"]),
                                    NotatkaID = reader["NotatkaID"] != DBNull.Value ? Convert.ToInt32(reader["NotatkaID"]) : 0,
                                    LP = reader["LP"]?.ToString(),
                                    Dostawca = reader["Dostawca"]?.ToString(),
                                    Tresc = reader["Tresc"]?.ToString(),
                                    CreatedAt = reader["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedAt"]) : DateTime.MinValue
                                });
                            }
                        }
                    }
                }
            }
            catch { }
            return list;
        }

        /// <summary>
        /// Oznacza wszystkie wzmianki bieżącego usera jako przeczytane
        /// </summary>
        private async Task MarkAllMentionsReadAsync()
        {
            if (string.IsNullOrEmpty(UserID) || !int.TryParse(UserID, out int userId)) return;
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    using (SqlCommand cmd = new SqlCommand(
                        "UPDATE NotatkiMentions SET IsRead = 1, ReadAt = GETDATE() WHERE MentionedUserID = @uid AND IsRead = 0", conn))
                    {
                        cmd.Parameters.AddWithValue("@uid", userId);
                        await cmd.ExecuteNonQueryAsync(_cts.Token);
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Aktualizuje wyświetlanie badge nieprzeczytanych wzmianek
        /// </summary>
        private void UpdateMentionsBadge(int count)
        {
            if (btnMentionsBadge == null || txtMentionsCount == null) return;
            if (count <= 0)
            {
                btnMentionsBadge.Visibility = Visibility.Collapsed;
            }
            else
            {
                btnMentionsBadge.Visibility = Visibility.Visible;
                txtMentionsCount.Text = count > 99 ? "99+" : count.ToString();
            }
        }

        /// <summary>
        /// Pobiera aktualną liczbę nieprzeczytanych wzmianek; toast przy wzroście
        /// </summary>
        private async Task CheckMentionsTickAsync()
        {
            // Pollujemy też dla nieaktywnego okna - żeby móc mignąć paskiem zadań
            int count = (await LoadUnreadMentionsAsync()).Count;
            UpdateMentionsBadge(count);

            // Toast i flash tylko przy wzroście (nowa wzmianka)
            if (count > _lastMentionCount && _lastMentionCount >= 0)
            {
                int newCount = count - _lastMentionCount;
                if (_isWindowActive)
                {
                    ShowToast($"🔔 Masz {newCount} now{(newCount == 1 ? "ą wzmiankę" : "e wzmianki")}", ToastType.Info);
                }
                else
                {
                    // Okno nieaktywne - mignij paskiem zadań żeby zwrócić uwagę
                    Dispatcher.InvokeAsync(() => WindowFlasher.Flash(this, count: 8));
                }
            }
            _lastMentionCount = count;
        }

        /// <summary>
        /// Parsuje @nazwisko w treści notatki i zapisuje wzmianki do NotatkiMentions
        /// </summary>
        private async Task SaveMentionsAsync(int notatkaId, string tresc)
        {
            try
            {
                var operators = await GetOperatorsAsync();
                if (operators.Count == 0) return;

                // Znajdź wszystkie @xxx w treści (bez spacji/znaku specjalnego po @)
                var regex = new System.Text.RegularExpressions.Regex(@"@([\p{L}0-9_.-]+(?:\s+[\p{L}0-9_.-]+)?)");
                var matches = regex.Matches(tresc);

                var mentionedUserIds = new HashSet<int>();
                foreach (System.Text.RegularExpressions.Match m in matches)
                {
                    string raw = m.Groups[1].Value.Trim();
                    // Dopasuj najdłuższy prefix operatora do raw (case-insensitive)
                    var op = operators
                        .Where(o => raw.StartsWith(o.Name, StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(o => o.Name.Length)
                        .FirstOrDefault();
                    if (op.Id > 0)
                        mentionedUserIds.Add(op.Id);
                }

                if (mentionedUserIds.Count == 0) return;

                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    foreach (var userId in mentionedUserIds)
                    {
                        // IGNORE duplikatu dzięki unique index na (NotatkaID, MentionedUserID)
                        string sql = @"IF NOT EXISTS (SELECT 1 FROM NotatkiMentions WHERE NotatkaID = @n AND MentionedUserID = @u)
                                       INSERT INTO NotatkiMentions (NotatkaID, MentionedUserID) VALUES (@n, @u)";
                        using (SqlCommand cmd = new SqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@n", notatkaId);
                            cmd.Parameters.AddWithValue("@u", userId);
                            await cmd.ExecuteNonQueryAsync(_cts.Token);
                        }
                    }
                }
            }
            catch { /* ignore cichy zapis wzmianek */ }
        }

        private async Task EditTypCenyAsync(DostawaModel selectedItem, bool isFromSecondTable, DataGridCell targetCell = null)
        {
            if (selectedItem == null || selectedItem.IsHeaderRow) return;

            // Zamknij poprzedni popup jeśli otwarty
            if (_inlineEditPopup != null && _inlineEditPopup.IsOpen)
                _inlineEditPopup.IsOpen = false;

            var typyCeny = new[] { "wolnyrynek", "rolnicza", "łączona", "ministerialna" };
            string currentTyp = selectedItem.TypCeny;

            var popup = new Popup
            {
                PlacementTarget = targetCell ?? (UIElement)this,
                Placement = targetCell != null ? PlacementMode.Bottom : PlacementMode.Center,
                StaysOpen = true,
                AllowsTransparency = true,
                PopupAnimation = PopupAnimation.Fade
            };
            _inlineEditPopup = popup;

            var border = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(4),
                Effect = new DropShadowEffect { BlurRadius = 8, Opacity = 0.3, ShadowDepth = 2 }
            };

            var stack = new StackPanel();

            var comboBox = new ComboBox
            {
                ItemsSource = typyCeny,
                SelectedItem = selectedItem.TypCeny,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                MinWidth = 160,
                Padding = new Thickness(6, 3, 6, 3),
                BorderThickness = new Thickness(0)
            };

            var checkBoxAll = new CheckBox
            {
                Content = "Wszystkie z wstawienia",
                FontSize = 11,
                Margin = new Thickness(4, 4, 4, 2),
                IsEnabled = !string.IsNullOrEmpty(selectedItem.LpW),
                Visibility = !string.IsNullOrEmpty(selectedItem.LpW) ? Visibility.Visible : Visibility.Collapsed
            };

            stack.Children.Add(comboBox);
            stack.Children.Add(checkBoxAll);
            border.Child = stack;
            popup.Child = border;

            bool saved = false;
            bool cancelled = false;

            async Task SaveTypCenyAsync()
            {
                if (saved || cancelled) return;
                saved = true;
                string newTypCeny = comboBox.SelectedItem?.ToString();
                if (string.IsNullOrEmpty(newTypCeny) || newTypCeny == currentTyp) return;

                bool changeAll = checkBoxAll.IsChecked == true && !string.IsNullOrEmpty(selectedItem.LpW);

                try
                {
                    using (SqlConnection conn = new SqlConnection(ConnectionString))
                    {
                        await conn.OpenAsync();
                        if (changeAll)
                        {
                            string sql = "UPDATE HarmonogramDostaw SET TypCeny = @typCeny, DataMod = GETDATE(), KtoMod = @kto WHERE LpW = @lpW";
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
                            string sql = "UPDATE HarmonogramDostaw SET TypCeny = @typCeny, DataMod = GETDATE(), KtoMod = @kto WHERE LP = @lp";
                            using (SqlCommand cmd = new SqlCommand(sql, conn))
                            {
                                cmd.Parameters.AddWithValue("@typCeny", newTypCeny);
                                cmd.Parameters.AddWithValue("@kto", UserID ?? "0");
                                cmd.Parameters.AddWithValue("@lp", selectedItem.LP);
                                await cmd.ExecuteNonQueryAsync();
                                ShowToast("Typ ceny zmieniony", ToastType.Success);

                                ShowChangeNotification(new ChangeNotificationItem
                                {
                                    Title = "Zmieniono typ ceny",
                                    Dostawca = selectedItem.Dostawca,
                                    LP = selectedItem.LP,
                                    DataOdbioru = selectedItem.DataOdbioru,
                                    UserId = UserID,
                                    UserName = txtUserName?.Text,
                                    NotificationType = ChangeNotificationType.InlineEdit,
                                    Changes = { new FieldChange { FieldName = "Typ ceny", OldValue = currentTyp ?? "-", NewValue = newTypCeny } }
                                });
                            }
                        }
                    }

                    // Aktualizuj model(e) w pamięci
                    if (changeAll && !string.IsNullOrEmpty(selectedItem.LpW))
                    {
                        foreach (var d in _dostawy.Where(x => x.LpW == selectedItem.LpW && !x.IsHeaderRow && !x.IsSeparator))
                            d.TypCeny = newTypCeny;
                        foreach (var d in _dostawyNastepnyTydzien.Where(x => x.LpW == selectedItem.LpW && !x.IsHeaderRow && !x.IsSeparator))
                            d.TypCeny = newTypCeny;
                    }
                    else
                    {
                        selectedItem.TypCeny = newTypCeny;
                    }
                }
                catch (Exception ex)
                {
                    ShowToast($"Błąd: {ex.Message}", ToastType.Error);
                }
            }

            comboBox.KeyDown += async (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    await SaveTypCenyAsync();
                    if (popup.IsOpen) popup.IsOpen = false;
                }
                if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    cancelled = true;
                    popup.IsOpen = false;
                }
            };

            popup.Closed += (s, e) =>
            {
                _inlineEditClosedTime = DateTime.Now;
                _skipNextDragStart = true;
                if (!saved && !cancelled)
                {
                    string newTypCeny = comboBox.SelectedItem?.ToString();
                    if (!string.IsNullOrEmpty(newTypCeny) && newTypCeny != currentTyp)
                    {
                        _ = Dispatcher.InvokeAsync(async () =>
                        {
                            try { await SaveTypCenyAsync(); }
                            catch { }
                        });
                    }
                }
            };

            popup.IsOpen = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                comboBox.Focus();
                comboBox.IsDropDownOpen = true;
            }), DispatcherPriority.Input);

        }

        private async Task EditCenaAsync(DostawaModel selectedItem, bool isFromSecondTable, DataGridCell targetCell = null)
        {
            if (selectedItem == null || selectedItem.IsHeaderRow) return;

            // Zamknij poprzedni popup jeśli otwarty
            if (_inlineEditPopup != null && _inlineEditPopup.IsOpen)
                _inlineEditPopup.IsOpen = false;

            string currentValue = selectedItem.Cena.ToString("0.00").Replace(",", ".");

            var popup = new Popup
            {
                PlacementTarget = targetCell ?? (UIElement)this,
                Placement = targetCell != null ? PlacementMode.Bottom : PlacementMode.Center,
                StaysOpen = true,
                AllowsTransparency = true,
                PopupAnimation = PopupAnimation.Fade
            };
            _inlineEditPopup = popup;

            // Walidacja - kolory borderu
            var defaultBorderBrush = new SolidColorBrush(Color.FromRgb(33, 150, 243));
            var warnBorderBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11));
            var errorBorderBrush = new SolidColorBrush(Color.FromRgb(211, 47, 47));

            var border = new Border
            {
                Background = Brushes.White,
                BorderBrush = defaultBorderBrush,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(2),
                Effect = new DropShadowEffect { BlurRadius = 8, Opacity = 0.3, ShadowDepth = 2 }
            };

            var stack = new StackPanel { Orientation = Orientation.Vertical };

            var textBox = new TextBox
            {
                Text = currentValue,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                MinWidth = 80,
                Padding = new Thickness(6, 3, 6, 3),
                BorderThickness = new Thickness(0),
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Right
            };

            var validationMessage = new TextBlock
            {
                FontSize = 10,
                Margin = new Thickness(6, 1, 6, 2),
                MaxWidth = 200,
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed
            };

            stack.Children.Add(textBox);
            stack.Children.Add(validationMessage);
            border.Child = stack;
            popup.Child = border;

            ValidationLevel currentValidationLevel = ValidationLevel.Ok;

            void RunValidation()
            {
                var result = InlineEditValidator.Validate("Cena", textBox.Text ?? "");
                currentValidationLevel = result.Level;
                switch (result.Level)
                {
                    case ValidationLevel.Ok:
                        border.BorderBrush = defaultBorderBrush;
                        validationMessage.Visibility = Visibility.Collapsed;
                        textBox.ToolTip = null;
                        break;
                    case ValidationLevel.Warning:
                        border.BorderBrush = warnBorderBrush;
                        validationMessage.Foreground = warnBorderBrush;
                        validationMessage.Text = "⚠ " + result.Message;
                        validationMessage.Visibility = Visibility.Visible;
                        textBox.ToolTip = result.Message;
                        break;
                    case ValidationLevel.Error:
                        border.BorderBrush = errorBorderBrush;
                        validationMessage.Foreground = errorBorderBrush;
                        validationMessage.Text = "✗ " + result.Message;
                        validationMessage.Visibility = Visibility.Visible;
                        textBox.ToolTip = result.Message;
                        break;
                }
            }

            textBox.TextChanged += (s, e) => RunValidation();

            bool saved = false;
            bool cancelled = false;

            async Task SaveCenaAsync()
            {
                if (saved || cancelled) return;
                string newValue = textBox.Text.Trim().Replace(",", ".");
                if (newValue == currentValue || string.IsNullOrWhiteSpace(newValue)) return;

                var validation = InlineEditValidator.Validate("Cena", newValue);
                if (validation.Level == ValidationLevel.Error)
                {
                    ShowToast($"Błędna wartość: {validation.Message}", ToastType.Error);
                    return;
                }

                saved = true;
                try
                {
                    decimal parsedCena = decimal.Parse(newValue, CultureInfo.InvariantCulture);

                    using (SqlConnection conn = new SqlConnection(ConnectionString))
                    {
                        await conn.OpenAsync();
                        string sql = "UPDATE HarmonogramDostaw SET Cena = @val, DataMod = GETDATE(), KtoMod = @kto WHERE LP = @lp";
                        using (SqlCommand cmd = new SqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@val", parsedCena);
                            cmd.Parameters.AddWithValue("@kto", UserID ?? "0");
                            cmd.Parameters.AddWithValue("@lp", selectedItem.LP);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    // Aktualizuj model w pamięci
                    selectedItem.Cena = parsedCena;
                    var cenaCollection = _dostawy.Contains(selectedItem) ? _dostawy : _dostawyNastepnyTydzien;
                    RecalculateDayHeader(cenaCollection, selectedItem.DataOdbioru);

                    // Fire-and-forget audit
                    if (_auditService != null)
                    {
                        _ = _auditService.LogFieldChangeAsync(
                            "HarmonogramDostaw", selectedItem.LP, AuditChangeSource.DoubleClick_Cena, "Cena",
                            currentValue, newValue,
                            new AuditContextInfo { Dostawca = selectedItem.Dostawca, DataOdbioru = selectedItem.DataOdbioru },
                            _cts.Token);
                    }

                    ShowToast("Cena zaktualizowana", ToastType.Success);

                    ShowChangeNotification(new ChangeNotificationItem
                    {
                        Title = "Zmieniono cenę",
                        Dostawca = selectedItem.Dostawca,
                        LP = selectedItem.LP,
                        DataOdbioru = selectedItem.DataOdbioru,
                        UserId = UserID,
                        UserName = txtUserName?.Text,
                        NotificationType = ChangeNotificationType.InlineEdit,
                        Changes = { new FieldChange { FieldName = "Cena", OldValue = currentValue, NewValue = newValue } }
                    });
                }
                catch (Exception ex)
                {
                    ShowToast($"Błąd: {ex.Message}", ToastType.Error);
                }
            }

            textBox.KeyDown += async (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    if (currentValidationLevel == ValidationLevel.Error)
                    {
                        ShowToast("Popraw wartość przed zapisem", ToastType.Warning);
                        return;
                    }
                    await SaveCenaAsync();
                    if (popup.IsOpen) popup.IsOpen = false;
                }
                if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    cancelled = true;
                    popup.IsOpen = false;
                }
            };

            popup.Closed += (s, e) =>
            {
                _inlineEditClosedTime = DateTime.Now;
                _skipNextDragStart = true;
                if (!saved && !cancelled && currentValidationLevel != ValidationLevel.Error)
                {
                    string newValue = textBox.Text.Trim().Replace(",", ".");
                    if (newValue != currentValue && !string.IsNullOrWhiteSpace(newValue))
                    {
                        _ = Dispatcher.InvokeAsync(async () =>
                        {
                            try { await SaveCenaAsync(); }
                            catch { }
                        });
                    }
                }
            };

            popup.IsOpen = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                textBox.SelectAll();
                textBox.Focus();
            }), DispatcherPriority.Input);

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

            // Zatrzymaj wszelkie animacje na tym wierszu (mogły zostać z recyklingu)
            e.Row.BeginAnimation(DataGridRow.OpacityProperty, null);
            e.Row.BeginAnimation(DataGridRow.BackgroundProperty, null);
            if (e.Row.Effect is DropShadowEffect effect)
            {
                effect.BeginAnimation(DropShadowEffect.OpacityProperty, null);
                effect.BeginAnimation(DropShadowEffect.BlurRadiusProperty, null);
            }

            // Pełny reset do domyślnych wartości (ważne przy recyklingu wierszy z wirtualizacją)
            e.Row.IsEnabled = true;
            e.Row.Background = Brushes.White;
            e.Row.Foreground = new SolidColorBrush(Color.FromRgb(33, 33, 33));
            e.Row.FontWeight = FontWeights.Normal;
            e.Row.FontSize = 12;
            e.Row.Opacity = 1.0;
            e.Row.Height = Double.NaN; // Auto
            e.Row.MinHeight = 0;
            e.Row.BorderBrush = null;
            e.Row.BorderThickness = new Thickness(0);
            e.Row.Effect = null;
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
                    e.Row.Foreground = Brushes.Black;
                    break;
                case "B.Wolny.":
                    e.Row.Background = (SolidColorBrush)FindResource("StatusBWolnyBrush");
                    break;
                case "Do Wykupienia":
                case "Do wykupienia":
                    e.Row.Background = (SolidColorBrush)FindResource("StatusDoWykupieniaBrush");
                    break;
            }

            // Ładowanie avatara autora notatki (Uwagi)
            if (!string.IsNullOrEmpty(dostawa.UwagiAutorID))
            {
                e.Row.Loaded += (s, args) =>
                {
                    try
                    {
                        // Szukaj avatara dla obu DataGridów (avatarUwagiImage i avatarUwagiImage2)
                        var avatarImage = FindVisualChild<Ellipse>(e.Row, "avatarUwagiImage") ?? FindVisualChild<Ellipse>(e.Row, "avatarUwagiImage2");
                        var avatarBorder = FindVisualChild<Border>(e.Row, "avatarUwagiBorder") ?? FindVisualChild<Border>(e.Row, "avatarUwagiBorder2");

                        if (avatarImage != null && avatarBorder != null && UserAvatarManager.HasAvatar(dostawa.UwagiAutorID))
                        {
                            using (var avatar = UserAvatarManager.GetAvatarRounded(dostawa.UwagiAutorID, 32))
                            {
                                if (avatar != null)
                                {
                                    var brush = new ImageBrush(ConvertToImageSource(avatar));
                                    brush.Stretch = Stretch.UniformToFill;
                                    avatarImage.Fill = brush;
                                    avatarImage.Visibility = Visibility.Visible;
                                    avatarBorder.Visibility = Visibility.Collapsed;
                                }
                            }
                        }
                    }
                    catch { }
                };
            }
        }

        private void LoadDeliveryDetails(string lp)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();
                    string sql = @"SELECT HD.*, HD.KiedyWaga, HD.KiedySztuki, D.Address, D.PostalCode, D.City, D.Distance, D.Phone1, D.Phone2, D.Phone3,
                                   D.Info1, D.Info2, D.Info3, D.Email, D.TypOsobowosci, D.TypOsobowosci2,
                                   O1.Name as KtoStwoName, O2.Name as KtoModName, O3.Name as KtoWagaName, O4.Name as KtoSztukiName,
                                   WK.isConf as WstawienieIsConf, WK.KtoConf as WstawienieKtoConf, WK.DataConf as WstawienieDataConf, O5.Name as KtoConfName
                                   FROM HarmonogramDostaw HD
                                   LEFT JOIN Dostawcy D ON HD.Dostawca = D.Name
                                   LEFT JOIN operators O1 ON HD.ktoStwo = O1.ID
                                   LEFT JOIN operators O2 ON HD.ktoMod = O2.ID
                                   LEFT JOIN operators O3 ON HD.KtoWaga = O3.ID
                                   LEFT JOIN operators O4 ON HD.KtoSztuki = O4.ID
                                   LEFT JOIN WstawieniaKurczakow WK ON HD.LpW = WK.Lp
                                   LEFT JOIN operators O5 ON WK.KtoConf = O5.ID
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
                                var phone1Sync = r["Phone1"]?.ToString();
                                txtTel1.Text = FormatPhoneNumber(phone1Sync);
                                txtTel2.Text = FormatPhoneNumber(r["Phone2"]?.ToString());
                                txtTel3.Text = FormatPhoneNumber(r["Phone3"]?.ToString());
                                txtInfo1.Text = r["Info1"]?.ToString();
                                txtInfo2.Text = r["Info2"]?.ToString();
                                txtInfo3.Text = r["Info3"]?.ToString();
                                cmbOsobowosc1.SelectedItem = r["TypOsobowosci"]?.ToString();
                                cmbOsobowosc2.SelectedItem = r["TypOsobowosci2"]?.ToString();

                                // Aktualizuj numer telefonu obok nazwy hodowcy w karcie Dostawa
                                var dostawcaNameSync = r["Dostawca"]?.ToString() ?? "";
                                if (!string.IsNullOrWhiteSpace(phone1Sync))
                                    txtHodowcaDaneDostawy.Text = $"{dostawcaNameSync} ({FormatPhoneNumber(phone1Sync)})";
                                else
                                    txtHodowcaDaneDostawy.Text = dostawcaNameSync;

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
                                UpdateObliczoneAuta();
                                _ = UpdatePropCenaAsync();

                                chkPotwWaga.IsChecked = r["PotwWaga"] != DBNull.Value && Convert.ToBoolean(r["PotwWaga"]);
                                chkPotwSztuki.IsChecked = r["PotwSztuki"] != DBNull.Value && Convert.ToBoolean(r["PotwSztuki"]);
                                borderPotwWaga.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(chkPotwWaga.IsChecked == true ? "#C8E6C9" : "#FFCDD2"));
                                borderPotwSztuki.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(chkPotwSztuki.IsChecked == true ? "#C8E6C9" : "#FFCDD2"));
                                txtKtoWaga.Text = r["KtoWagaName"]?.ToString();
                                txtKtoSztuki.Text = r["KtoSztukiName"]?.ToString();

                                // Info
                                txtDataStwo.Text = r["DataUtw"] != DBNull.Value ? Convert.ToDateTime(r["DataUtw"]).ToString("yyyy-MM-dd HH:mm") : "";
                                SetAvatar(avatarStwo, txtAvatarStwo, txtKtoStwo, r["KtoStwoName"]?.ToString(), r["ktoStwo"]?.ToString(), imgAvatarStwo, imgAvatarStwoBrush);
                                txtDataMod.Text = r["DataMod"] != DBNull.Value ? Convert.ToDateTime(r["DataMod"]).ToString("yyyy-MM-dd HH:mm") : "";
                                SetAvatar(avatarMod, txtAvatarMod, txtKtoMod, r["KtoModName"]?.ToString(), r["ktoMod"]?.ToString(), imgAvatarMod, imgAvatarModBrush);

                                // Info potwierdzenia wagi i sztuk
                                txtDataPotwWaga.Text = r["KiedyWaga"] != DBNull.Value ? Convert.ToDateTime(r["KiedyWaga"]).ToString("yyyy-MM-dd HH:mm") : "";
                                SetAvatar(avatarPotwWaga, txtAvatarPotwWaga, txtKtoPotwWaga, r["KtoWagaName"]?.ToString(), r["KtoWaga"]?.ToString(), imgAvatarPotwWaga, imgAvatarPotwWagaBrush);
                                txtDataPotwSztuki.Text = r["KiedySztuki"] != DBNull.Value ? Convert.ToDateTime(r["KiedySztuki"]).ToString("yyyy-MM-dd HH:mm") : "";
                                SetAvatar(avatarPotwSztuki, txtAvatarPotwSztuki, txtKtoPotwSztuki, r["KtoSztukiName"]?.ToString(), r["KtoSztuki"]?.ToString(), imgAvatarPotwSztuki, imgAvatarPotwSztukiBrush);

                                // Info potwierdzenia wstawienia
                                txtDataPotwWstawienie.Text = r["WstawienieDataConf"] != DBNull.Value ? Convert.ToDateTime(r["WstawienieDataConf"]).ToString("yyyy-MM-dd HH:mm") : "";
                                SetAvatar(avatarPotwWstawienie, txtAvatarPotwWstawienie, txtKtoPotwWstawienie, r["KtoConfName"]?.ToString(), r["WstawienieKtoConf"]?.ToString(), imgAvatarPotwWstawienie, imgAvatarPotwWstawienieBrush);

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
                IncrementSimulationChangeCount();
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
                // Batch refresh: aktualizuj in-memory zamiast pełnego reload
                ApplyLocalUpdate(lp, d =>
                {
                    d.Bufor = status;
                    d.IsConfirmed = status == "Potwierdzony";
                });
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
                // Batch refresh: aktualizuj in-memory wszystkie wiersze z tym LpW (mogą być w obu tabelach)
                foreach (var d in _dostawy.Concat(_dostawyNastepnyTydzien)
                                           .Where(d => !d.IsHeaderRow && !d.IsSeparator && d.LpW == lpW))
                {
                    d.IsWstawienieConfirmed = isChecked;
                }
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
            // TRYB SYMULACJI - dodaj tymczasową dostawę w pamięci
            if (_isSimulationMode)
            {
                var newDostawa = new DostawaModel
                {
                    LP = $"SIM_{DateTime.Now.Ticks}",
                    DataOdbioru = _selectedDate,
                    Dostawca = "(Nowa symulacja)",
                    Auta = 1,
                    SztukiDek = 0,
                    WagaDek = 0,
                    Cena = 0,
                    Bufor = "Niepotwierdzony",
                    TypCeny = ""
                };
                _dostawy.Add(newDostawa);
                RefreshDostawyView();
                IncrementSimulationChangeCount();
                ShowToast("📝 Dodano tymczasową dostawę (symulacja)", ToastType.Info);
                return;
            }

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
            // Pobierz zaznaczony element z odpowiedniej tabeli
            var selectedItem = dgDostawy.SelectedItem as DostawaModel ?? dgDostawyNastepny.SelectedItem as DostawaModel;
            DateTime dateToUse = _selectedDate;
            if (selectedItem != null && !selectedItem.IsSeparator && !selectedItem.IsHeaderRow)
                dateToUse = selectedItem.DataOdbioru;

            // TRYB SYMULACJI - dodaj tymczasową dostawę w pamięci
            if (_isSimulationMode)
            {
                var newDostawa = new DostawaModel
                {
                    LP = $"SIM_{DateTime.Now.Ticks}",
                    DataOdbioru = dateToUse,
                    Dostawca = "(Nowa symulacja)",
                    Auta = 1,
                    SztukiDek = 0,
                    WagaDek = 0,
                    Cena = 0,
                    Bufor = "Niepotwierdzony",
                    TypCeny = ""
                };
                _dostawy.Add(newDostawa);
                RefreshDostawyView();
                IncrementSimulationChangeCount();
                ShowToast("📝 Dodano tymczasową dostawę (symulacja)", ToastType.Info);
                return;
            }

            try
            {
                bool openAnother;
                do
                {
                    openAnother = false;
                    var window = new NowaDostawaWindow(dateToUse, ConnectionString, UserID, txtUserName?.Text, _auditService)
                    {
                        Owner = this
                    };
                    bool? result = window.ShowDialog();
                    if (result == true && window.DeliveryCreated)
                    {
                        ShowToast($"Utworzono dostawę LP {window.CreatedLP}", ToastType.Success);
                        _ = LoadDostawyAsync();
                        openAnother = window.OpenAnother;
                    }
                } while (openAnother);
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
                dgDostawy.Items.Refresh();
                dgDostawyNastepny.Items.Refresh();
                IncrementSimulationChangeCount();
                ShowToast("📝 Waga potwierdzona (symulacja)", ToastType.Info);
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    string sql = "UPDATE HarmonogramDostaw SET PotwWaga = 1, KiedyWaga = GETDATE(), KtoWaga = @KtoWaga WHERE Lp = @Lp";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Lp", _selectedLP);
                        cmd.Parameters.AddWithValue("@KtoWaga", UserID ?? "0");
                        await cmd.ExecuteNonQueryAsync(_cts.Token);
                    }
                }

                if (dostawa != null) dostawa.PotwWaga = true;
                chkPotwWaga.IsChecked = true;
                borderPotwWaga.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C8E6C9"));
                txtKtoWaga.Text = $"({UserName})";
                txtDataPotwWaga.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                SetAvatar(avatarPotwWaga, txtAvatarPotwWaga, txtKtoPotwWaga, UserName, UserID, imgAvatarPotwWaga, imgAvatarPotwWagaBrush);
                dgDostawy.Items.Refresh();
                dgDostawyNastepny.Items.Refresh();
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
                dgDostawy.Items.Refresh();
                dgDostawyNastepny.Items.Refresh();
                IncrementSimulationChangeCount();
                ShowToast("📝 Sztuki potwierdzone (symulacja)", ToastType.Info);
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    string sql = "UPDATE HarmonogramDostaw SET PotwSztuki = 1, KiedySztuki = GETDATE(), KtoSztuki = @KtoSztuki WHERE Lp = @Lp";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Lp", _selectedLP);
                        cmd.Parameters.AddWithValue("@KtoSztuki", UserID ?? "0");
                        await cmd.ExecuteNonQueryAsync(_cts.Token);
                    }
                }

                if (dostawa != null) dostawa.PotwSztuki = true;
                chkPotwSztuki.IsChecked = true;
                borderPotwSztuki.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C8E6C9"));
                txtKtoSztuki.Text = $"({UserName})";
                txtDataPotwSztuki.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                SetAvatar(avatarPotwSztuki, txtAvatarPotwSztuki, txtKtoPotwSztuki, UserName, UserID, imgAvatarPotwSztuki, imgAvatarPotwSztukiBrush);
                dgDostawy.Items.Refresh();
                dgDostawyNastepny.Items.Refresh();
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
                dgDostawy.Items.Refresh();
                dgDostawyNastepny.Items.Refresh();
                IncrementSimulationChangeCount();
                ShowToast("📝 Cofnięto potwierdzenie wagi (symulacja)", ToastType.Info);
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    string sql = "UPDATE HarmonogramDostaw SET PotwWaga = 0, KiedyWaga = NULL, KtoWaga = NULL WHERE Lp = @Lp";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Lp", _selectedLP);
                        await cmd.ExecuteNonQueryAsync(_cts.Token);
                    }
                }

                if (dostawa != null) dostawa.PotwWaga = false;
                chkPotwWaga.IsChecked = false;
                borderPotwWaga.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCDD2"));
                txtKtoWaga.Text = "";
                txtDataPotwWaga.Text = "";
                SetAvatar(avatarPotwWaga, txtAvatarPotwWaga, txtKtoPotwWaga, null, null, imgAvatarPotwWaga, imgAvatarPotwWagaBrush);
                dgDostawy.Items.Refresh();
                dgDostawyNastepny.Items.Refresh();
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
                dgDostawy.Items.Refresh();
                dgDostawyNastepny.Items.Refresh();
                IncrementSimulationChangeCount();
                ShowToast("📝 Cofnięto potwierdzenie sztuk (symulacja)", ToastType.Info);
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    string sql = "UPDATE HarmonogramDostaw SET PotwSztuki = 0, KiedySztuki = NULL, KtoSztuki = NULL WHERE Lp = @Lp";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Lp", _selectedLP);
                        await cmd.ExecuteNonQueryAsync(_cts.Token);
                    }
                }

                if (dostawa != null) dostawa.PotwSztuki = false;
                chkPotwSztuki.IsChecked = false;
                borderPotwSztuki.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCDD2"));
                txtKtoSztuki.Text = "";
                txtDataPotwSztuki.Text = "";
                SetAvatar(avatarPotwSztuki, txtAvatarPotwSztuki, txtKtoPotwSztuki, null, null, imgAvatarPotwSztuki, imgAvatarPotwSztukiBrush);
                dgDostawy.Items.Refresh();
                dgDostawyNastepny.Items.Refresh();
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

        // Menu kontekstowe - Potwierdź WSTAWIENIE
        private async void MenuPotwierdzWstawienie_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedLP))
            {
                ShowToast("Wybierz dostawę", ToastType.Warning);
                return;
            }

            var dostawa = _dostawy.FirstOrDefault(d => d.LP == _selectedLP) ?? _dostawyNastepnyTydzien.FirstOrDefault(d => d.LP == _selectedLP);
            if (dostawa == null || string.IsNullOrEmpty(dostawa.LpW))
            {
                ShowToast("Brak powiązanego wstawienia (LpW)", ToastType.Warning);
                return;
            }

            if (_isSimulationMode)
            {
                dostawa.IsWstawienieConfirmed = true;
                dgDostawy.Items.Refresh();
                dgDostawyNastepny.Items.Refresh();
                IncrementSimulationChangeCount();
                ShowToast("📝 Wstawienie potwierdzone (symulacja)", ToastType.Info);
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    using (SqlCommand cmd = new SqlCommand(
                        "UPDATE WstawieniaKurczakow SET isConf = 1, KtoConf = @kto, DataConf = @data WHERE Lp = @lp", conn))
                    {
                        cmd.Parameters.AddWithValue("@kto", UserID ?? "0");
                        cmd.Parameters.AddWithValue("@data", DateTime.Now);
                        cmd.Parameters.AddWithValue("@lp", dostawa.LpW);
                        await cmd.ExecuteNonQueryAsync(_cts.Token);
                    }
                }

                dostawa.IsWstawienieConfirmed = true;
                txtDataPotwWstawienie.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                string userName = txtUserName?.Text ?? UserName ?? "";
                SetAvatar(avatarPotwWstawienie, txtAvatarPotwWstawienie, txtKtoPotwWstawienie, userName, UserID, imgAvatarPotwWstawienie, imgAvatarPotwWstawienieBrush);
                dgDostawy.Items.Refresh();
                dgDostawyNastepny.Items.Refresh();
                ShowToast("🐣 Wstawienie potwierdzone!", ToastType.Success);

                if (_auditService != null)
                {
                    await _auditService.LogWstawienieConfirmationAsync(dostawa.LpW, true, _cts.Token);
                }
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd: {ex.Message}", ToastType.Error);
            }
        }

        // Menu kontekstowe - Cofnij potwierdzenie WSTAWIENIA
        private async void MenuCofnijWstawienie_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedLP))
            {
                ShowToast("Wybierz dostawę", ToastType.Warning);
                return;
            }

            var dostawa = _dostawy.FirstOrDefault(d => d.LP == _selectedLP) ?? _dostawyNastepnyTydzien.FirstOrDefault(d => d.LP == _selectedLP);
            if (dostawa == null || string.IsNullOrEmpty(dostawa.LpW))
            {
                ShowToast("Brak powiązanego wstawienia (LpW)", ToastType.Warning);
                return;
            }

            if (_isSimulationMode)
            {
                dostawa.IsWstawienieConfirmed = false;
                dgDostawy.Items.Refresh();
                dgDostawyNastepny.Items.Refresh();
                IncrementSimulationChangeCount();
                ShowToast("📝 Cofnięto potwierdzenie wstawienia (symulacja)", ToastType.Info);
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    using (SqlCommand cmd = new SqlCommand(
                        "UPDATE WstawieniaKurczakow SET isConf = 0, KtoConf = NULL, DataConf = NULL WHERE Lp = @lp", conn))
                    {
                        cmd.Parameters.AddWithValue("@lp", dostawa.LpW);
                        await cmd.ExecuteNonQueryAsync(_cts.Token);
                    }
                }

                dostawa.IsWstawienieConfirmed = false;
                txtDataPotwWstawienie.Text = "";
                SetAvatar(avatarPotwWstawienie, txtAvatarPotwWstawienie, txtKtoPotwWstawienie, null, null, imgAvatarPotwWstawienie, imgAvatarPotwWstawienieBrush);
                dgDostawy.Items.Refresh();
                dgDostawyNastepny.Items.Refresh();
                ShowToast("↩️ Cofnięto potwierdzenie wstawienia", ToastType.Info);

                if (_auditService != null)
                {
                    await _auditService.LogWstawienieConfirmationAsync(dostawa.LpW, false, _cts.Token);
                }
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd: {ex.Message}", ToastType.Error);
            }
        }

        // Menu kontekstowe - Edytuj wstawienie (otwiera to samo okno co w Cykle Wstawień)
        private async void MenuEdytujWstawienie_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedLP))
            {
                ShowToast("Wybierz dostawę", ToastType.Warning);
                return;
            }

            var dostawa = _dostawy.FirstOrDefault(d => d.LP == _selectedLP) ?? _dostawyNastepnyTydzien.FirstOrDefault(d => d.LP == _selectedLP);
            if (dostawa == null || string.IsNullOrEmpty(dostawa.LpW))
            {
                ShowToast("Brak powiązanego wstawienia (LpW)", ToastType.Warning);
                return;
            }

            if (!int.TryParse(dostawa.LpW, out int lpWstawienia))
            {
                ShowToast("Nieprawidłowe LpW", ToastType.Error);
                return;
            }

            try
            {
                string dostawca = null;
                DateTime dataWstawienia = DateTime.Today;
                int sztWstawione = 0;

                using (var conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    using (var cmd = new SqlCommand(
                        "SELECT Dostawca, DataWstawienia, IloscWstawienia FROM dbo.WstawieniaKurczakow WHERE Lp = @lp", conn))
                    {
                        cmd.Parameters.AddWithValue("@lp", lpWstawienia);
                        using (var reader = await cmd.ExecuteReaderAsync(_cts.Token))
                        {
                            if (await reader.ReadAsync(_cts.Token))
                            {
                                dostawca = reader["Dostawca"]?.ToString();
                                dataWstawienia = reader["DataWstawienia"] != DBNull.Value
                                    ? Convert.ToDateTime(reader["DataWstawienia"])
                                    : DateTime.Today;
                                sztWstawione = reader["IloscWstawienia"] != DBNull.Value
                                    ? Convert.ToInt32(reader["IloscWstawienia"])
                                    : 0;
                            }
                            else
                            {
                                ShowToast($"Nie znaleziono wstawienia Lp={lpWstawienia}", ToastType.Warning);
                                return;
                            }
                        }
                    }
                }

                var wstawienie = new WstawienieWindow
                {
                    UserID = UserID ?? "0",
                    SztWstawienia = sztWstawione,
                    Dostawca = dostawca,
                    LpWstawienia = lpWstawienia,
                    DataWstawienia = dataWstawienia,
                    Modyfikacja = true,
                    Owner = this
                };

                wstawienie.ShowDialog();

                // Odśwież dane aby pokazać ewentualne zmiany
                await LoadDostawyAsync();
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd: {ex.Message}", ToastType.Error);
            }
        }

        // Menu kontekstowe - Potwierdź dostawę (zmień status na Potwierdzony)
        private async void MenuPotwierdzDostawe_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedLP))
            {
                ShowToast("Wybierz dostawę", ToastType.Warning);
                return;
            }

            var dostawa = _dostawy.FirstOrDefault(d => d.LP == _selectedLP) ?? _dostawyNastepnyTydzien.FirstOrDefault(d => d.LP == _selectedLP);
            if (dostawa == null) return;

            string oldStatus = dostawa.Bufor;

            // TRYB SYMULACJI - tylko zmiana w pamięci
            if (_isSimulationMode)
            {
                dostawa.Bufor = "Potwierdzony";
                cmbStatus.SelectedItem = "Potwierdzony";
                dgDostawy.Items.Refresh();
                dgDostawyNastepny.Items.Refresh();
                IncrementSimulationChangeCount();
                ShowToast("✅ Dostawa potwierdzona (symulacja)", ToastType.Info);
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    string sql = "UPDATE HarmonogramDostaw SET bufor = 'Potwierdzony' WHERE Lp = @Lp";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Lp", _selectedLP);
                        await cmd.ExecuteNonQueryAsync(_cts.Token);
                    }
                }

                dostawa.Bufor = "Potwierdzony";
                cmbStatus.SelectedItem = "Potwierdzony";
                dgDostawy.Items.Refresh();
                dgDostawyNastepny.Items.Refresh();
                ShowToast("✅ Dostawa potwierdzona", ToastType.Success);

                // Audit log
                if (_auditService != null)
                {
                    await _auditService.LogFieldChangeAsync("HarmonogramDostaw", _selectedLP,
                        AuditChangeSource.ContextMenu_Potwierdz, "bufor", oldStatus, "Potwierdzony",
                        new AuditContextInfo { Dostawca = dostawa.Dostawca, DataOdbioru = dostawa.DataOdbioru }, _cts.Token);
                }
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd: {ex.Message}", ToastType.Error);
            }
        }

        // Menu kontekstowe - Anuluj dostawę (z pytaniem o wszystkie dostawy z tego wstawienia)
        private async void MenuAnulujDostawe_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedLP))
            {
                ShowToast("Wybierz dostawę", ToastType.Warning);
                return;
            }

            var dostawa = _dostawy.FirstOrDefault(d => d.LP == _selectedLP) ?? _dostawyNastepnyTydzien.FirstOrDefault(d => d.LP == _selectedLP);
            if (dostawa == null) return;

            // Sprawdź czy dostawa ma LpW (numer wstawienia)
            if (!string.IsNullOrEmpty(dostawa.LpW))
            {
                // Pobierz wszystkie dostawy z tego samego wstawienia
                var dostawyZWstawienia = await GetDostawyByLpWAsync(dostawa.LpW);

                if (dostawyZWstawienia.Count > 1)
                {
                    // Zbuduj listę dostaw do wyświetlenia
                    var listaInfo = string.Join("\n", dostawyZWstawienia.Select(d =>
                        $"  • {d.DataOdbioru:dd.MM.yyyy} - {d.Auta} aut, {d.SztukiDek:N0} szt, {d.WagaDek:N2} kg"));

                    var result = MessageBox.Show(
                        $"Czy chcesz anulować WSZYSTKIE dostawy z tego wstawienia?\n\n" +
                        $"Hodowca: {dostawa.Dostawca}\n" +
                        $"Znaleziono {dostawyZWstawienia.Count} dostaw:\n{listaInfo}\n\n" +
                        $"TAK = Anuluj wszystkie ({dostawyZWstawienia.Count} dostaw)\n" +
                        $"NIE = Anuluj tylko wybraną dostawę\n" +
                        $"ANULUJ = Nie rób nic",
                        "Anulowanie dostaw z wstawienia",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        // Anuluj wszystkie dostawy z tego wstawienia
                        await AnulujWszystkieDostawyZWstawieniaAsync(dostawa.LpW, dostawyZWstawienia);
                    }
                    else if (result == MessageBoxResult.No)
                    {
                        // Anuluj tylko wybraną dostawę
                        await AnulujPojedynczaDostaweAsync(dostawa);
                    }
                    return;
                }
            }

            // Jeśli nie ma LpW lub jest tylko jedna dostawa - anuluj tylko tę jedną
            await AnulujPojedynczaDostaweAsync(dostawa);
        }

        private async Task<List<(string LP, DateTime DataOdbioru, int Auta, double SztukiDek, double WagaDek, string Bufor, string Dostawca)>> GetDostawyByLpWAsync(string lpW)
        {
            var result = new List<(string LP, DateTime DataOdbioru, int Auta, double SztukiDek, double WagaDek, string Bufor, string Dostawca)>();

            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    string sql = "SELECT LP, DataOdbioru, Auta, SztukiDek, WagaDek, bufor, Dostawca FROM HarmonogramDostaw WHERE LpW = @LpW AND bufor != 'Anulowany' ORDER BY DataOdbioru";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@LpW", lpW);
                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync(_cts.Token))
                        {
                            while (await reader.ReadAsync(_cts.Token))
                            {
                                result.Add((
                                    reader["LP"].ToString(),
                                    Convert.ToDateTime(reader["DataOdbioru"]),
                                    reader["Auta"] != DBNull.Value ? Convert.ToInt32(reader["Auta"]) : 0,
                                    reader["SztukiDek"] != DBNull.Value ? Convert.ToDouble(reader["SztukiDek"]) : 0,
                                    reader["WagaDek"] != DBNull.Value ? Convert.ToDouble(reader["WagaDek"]) : 0,
                                    reader["bufor"]?.ToString() ?? "",
                                    reader["Dostawca"]?.ToString() ?? ""
                                ));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd pobierania dostaw: {ex.Message}", ToastType.Error);
            }

            return result;
        }

        private async Task AnulujWszystkieDostawyZWstawieniaAsync(string lpW, List<(string LP, DateTime DataOdbioru, int Auta, double SztukiDek, double WagaDek, string Bufor, string Dostawca)> dostawy)
        {
            // TRYB SYMULACJI
            if (_isSimulationMode)
            {
                foreach (var d in dostawy)
                {
                    var dostawa = _dostawy.FirstOrDefault(x => x.LP == d.LP) ?? _dostawyNastepnyTydzien.FirstOrDefault(x => x.LP == d.LP);
                    if (dostawa != null) dostawa.Bufor = "Anulowany";
                }
                RefreshDostawyView();
                IncrementSimulationChangeCount();
                ShowToast($"❌ Anulowano {dostawy.Count} dostaw z wstawienia (symulacja)", ToastType.Info);
                return;
            }

            try
            {
                int count = 0;
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    foreach (var d in dostawy)
                    {
                        using (SqlCommand cmd = new SqlCommand("UPDATE HarmonogramDostaw SET bufor = 'Anulowany' WHERE LP = @LP", conn))
                        {
                            cmd.Parameters.AddWithValue("@LP", d.LP);
                            count += await cmd.ExecuteNonQueryAsync(_cts.Token);
                        }
                    }
                }

                // Audit log
                if (_auditService != null)
                {
                    var lps = dostawy.Select(d => d.LP).ToList();
                    await _auditService.LogBulkOperationAsync("HarmonogramDostaw", lps,
                        AuditChangeSource.BulkCancel, "bufor", "Anulowany", null, _cts.Token);
                }

                ShowToast($"❌ Anulowano {count} dostaw z wstawienia", ToastType.Success);
                await LoadDostawyAsync();
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd: {ex.Message}", ToastType.Error);
            }
        }

        private async Task AnulujPojedynczaDostaweAsync(DostawaModel dostawa)
        {
            string oldStatus = dostawa.Bufor;

            // TRYB SYMULACJI
            if (_isSimulationMode)
            {
                dostawa.Bufor = "Anulowany";
                cmbStatus.SelectedItem = "Anulowany";
                dgDostawy.Items.Refresh();
                dgDostawyNastepny.Items.Refresh();
                IncrementSimulationChangeCount();
                ShowToast("❌ Dostawa anulowana (symulacja)", ToastType.Info);
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    string sql = "UPDATE HarmonogramDostaw SET bufor = 'Anulowany' WHERE Lp = @Lp";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Lp", dostawa.LP);
                        await cmd.ExecuteNonQueryAsync(_cts.Token);
                    }
                }

                dostawa.Bufor = "Anulowany";
                cmbStatus.SelectedItem = "Anulowany";
                dgDostawy.Items.Refresh();
                dgDostawyNastepny.Items.Refresh();
                ShowToast("❌ Dostawa anulowana", ToastType.Success);

                // Audit log
                if (_auditService != null)
                {
                    await _auditService.LogFieldChangeAsync("HarmonogramDostaw", dostawa.LP,
                        AuditChangeSource.ContextMenu_Anuluj, "bufor", oldStatus, "Anulowany",
                        new AuditContextInfo { Dostawca = dostawa.Dostawca, DataOdbioru = dostawa.DataOdbioru }, _cts.Token);
                }
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd: {ex.Message}", ToastType.Error);
            }
        }

        // Checkbox bezpośredni - Potwierdzenie WAGI (kliknięcie w checkbox)
        private async void ChkPotwWaga_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedLP))
            {
                // Przywróć stan checkboxa
                chkPotwWaga.IsChecked = !chkPotwWaga.IsChecked;
                ShowToast("Wybierz dostawę", ToastType.Warning);
                return;
            }

            var dostawa = _dostawy.FirstOrDefault(d => d.LP == _selectedLP) ?? _dostawyNastepnyTydzien.FirstOrDefault(d => d.LP == _selectedLP);
            bool isChecked = chkPotwWaga.IsChecked == true;

            // TRYB SYMULACJI - tylko zmiana w UI, bez bazy i logów
            if (_isSimulationMode)
            {
                if (dostawa != null) dostawa.PotwWaga = isChecked;
                if (isChecked)
                {
                    borderPotwWaga.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C8E6C9"));
                    txtKtoWaga.Text = $"({UserName})";
                    IncrementSimulationChangeCount();
                ShowToast("📝 Waga potwierdzona (symulacja)", ToastType.Info);
                }
                else
                {
                    borderPotwWaga.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCDD2"));
                    txtKtoWaga.Text = "";
                    IncrementSimulationChangeCount();
                ShowToast("📝 Cofnięto potwierdzenie wagi (symulacja)", ToastType.Info);
                }
                dgDostawy.Items.Refresh();
                dgDostawyNastepny.Items.Refresh();
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    string sql = isChecked
                        ? "UPDATE HarmonogramDostaw SET PotwWaga = 1, KiedyWaga = GETDATE(), KtoWaga = @KtoWaga WHERE Lp = @Lp"
                        : "UPDATE HarmonogramDostaw SET PotwWaga = 0, KiedyWaga = NULL, KtoWaga = NULL WHERE Lp = @Lp";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Lp", _selectedLP);
                        if (isChecked) cmd.Parameters.AddWithValue("@KtoWaga", UserID ?? "0");
                        await cmd.ExecuteNonQueryAsync(_cts.Token);
                    }
                }

                if (dostawa != null) dostawa.PotwWaga = isChecked;

                if (isChecked)
                {
                    borderPotwWaga.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C8E6C9"));
                    txtKtoWaga.Text = $"({UserName})";
                    txtDataPotwWaga.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                    SetAvatar(avatarPotwWaga, txtAvatarPotwWaga, txtKtoPotwWaga, UserName, UserID, imgAvatarPotwWaga, imgAvatarPotwWagaBrush);
                    ShowToast("✅ Waga potwierdzona!", ToastType.Success);

                    // Audit log
                    if (_auditService != null)
                    {
                        await _auditService.LogFieldChangeAsync("HarmonogramDostaw", _selectedLP,
                            AuditChangeSource.ContextMenu_PotwierdzWage, "PotwWaga", "0", "1",
                            new AuditContextInfo { Dostawca = dostawa?.Dostawca, DataOdbioru = dostawa?.DataOdbioru }, _cts.Token);
                    }
                }
                else
                {
                    borderPotwWaga.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCDD2"));
                    txtKtoWaga.Text = "";
                    txtDataPotwWaga.Text = "";
                    SetAvatar(avatarPotwWaga, txtAvatarPotwWaga, txtKtoPotwWaga, null, null, imgAvatarPotwWaga, imgAvatarPotwWagaBrush);
                    ShowToast("↩️ Cofnięto potwierdzenie wagi", ToastType.Info);

                    // Audit log
                    if (_auditService != null)
                    {
                        await _auditService.LogFieldChangeAsync("HarmonogramDostaw", _selectedLP,
                            AuditChangeSource.ContextMenu_CofnijWage, "PotwWaga", "1", "0",
                            new AuditContextInfo { Dostawca = dostawa?.Dostawca, DataOdbioru = dostawa?.DataOdbioru }, _cts.Token);
                    }
                }
                dgDostawy.Items.Refresh();
                dgDostawyNastepny.Items.Refresh();
            }
            catch (Exception ex)
            {
                // Przywróć stan checkboxa w przypadku błędu
                chkPotwWaga.IsChecked = !isChecked;
                ShowToast($"Błąd: {ex.Message}", ToastType.Error);
            }
        }

        // Checkbox bezpośredni - Potwierdzenie SZTUK (kliknięcie w checkbox)
        private async void ChkPotwSztuki_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedLP))
            {
                // Przywróć stan checkboxa
                chkPotwSztuki.IsChecked = !chkPotwSztuki.IsChecked;
                ShowToast("Wybierz dostawę", ToastType.Warning);
                return;
            }

            var dostawa = _dostawy.FirstOrDefault(d => d.LP == _selectedLP) ?? _dostawyNastepnyTydzien.FirstOrDefault(d => d.LP == _selectedLP);
            bool isChecked = chkPotwSztuki.IsChecked == true;

            // TRYB SYMULACJI - tylko zmiana w UI, bez bazy i logów
            if (_isSimulationMode)
            {
                if (dostawa != null) dostawa.PotwSztuki = isChecked;
                if (isChecked)
                {
                    borderPotwSztuki.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C8E6C9"));
                    txtKtoSztuki.Text = $"({UserName})";
                    IncrementSimulationChangeCount();
                ShowToast("📝 Sztuki potwierdzone (symulacja)", ToastType.Info);
                }
                else
                {
                    borderPotwSztuki.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCDD2"));
                    txtKtoSztuki.Text = "";
                    IncrementSimulationChangeCount();
                ShowToast("📝 Cofnięto potwierdzenie sztuk (symulacja)", ToastType.Info);
                }
                dgDostawy.Items.Refresh();
                dgDostawyNastepny.Items.Refresh();
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    string sql = isChecked
                        ? "UPDATE HarmonogramDostaw SET PotwSztuki = 1, KiedySztuki = GETDATE(), KtoSztuki = @KtoSztuki WHERE Lp = @Lp"
                        : "UPDATE HarmonogramDostaw SET PotwSztuki = 0, KiedySztuki = NULL, KtoSztuki = NULL WHERE Lp = @Lp";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Lp", _selectedLP);
                        if (isChecked) cmd.Parameters.AddWithValue("@KtoSztuki", UserID ?? "0");
                        await cmd.ExecuteNonQueryAsync(_cts.Token);
                    }
                }

                if (dostawa != null) dostawa.PotwSztuki = isChecked;

                if (isChecked)
                {
                    borderPotwSztuki.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C8E6C9"));
                    txtKtoSztuki.Text = $"({UserName})";
                    txtDataPotwSztuki.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                    SetAvatar(avatarPotwSztuki, txtAvatarPotwSztuki, txtKtoPotwSztuki, UserName, UserID, imgAvatarPotwSztuki, imgAvatarPotwSztukiBrush);
                    ShowToast("✅ Sztuki potwierdzone!", ToastType.Success);

                    // Audit log
                    if (_auditService != null)
                    {
                        await _auditService.LogFieldChangeAsync("HarmonogramDostaw", _selectedLP,
                            AuditChangeSource.ContextMenu_PotwierdzSztuki, "PotwSztuki", "0", "1",
                            new AuditContextInfo { Dostawca = dostawa?.Dostawca, DataOdbioru = dostawa?.DataOdbioru }, _cts.Token);
                    }
                }
                else
                {
                    borderPotwSztuki.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCDD2"));
                    txtKtoSztuki.Text = "";
                    txtDataPotwSztuki.Text = "";
                    SetAvatar(avatarPotwSztuki, txtAvatarPotwSztuki, txtKtoPotwSztuki, null, null, imgAvatarPotwSztuki, imgAvatarPotwSztukiBrush);
                    ShowToast("↩️ Cofnięto potwierdzenie sztuk", ToastType.Info);

                    // Audit log
                    if (_auditService != null)
                    {
                        await _auditService.LogFieldChangeAsync("HarmonogramDostaw", _selectedLP,
                            AuditChangeSource.ContextMenu_CofnijSztuki, "PotwSztuki", "1", "0",
                            new AuditContextInfo { Dostawca = dostawa?.Dostawca, DataOdbioru = dostawa?.DataOdbioru }, _cts.Token);
                    }
                }
                dgDostawy.Items.Refresh();
                dgDostawyNastepny.Items.Refresh();
            }
            catch (Exception ex)
            {
                // Przywróć stan checkboxa w przypadku błędu
                chkPotwSztuki.IsChecked = !isChecked;
                ShowToast($"Błąd: {ex.Message}", ToastType.Error);
            }
        }

        #region Menu kontekstowe - Widoki i raporty

        // Pokaż wagi dla zaznaczonego dostawcy
        private void MenuPokazWagi_Click(object sender, RoutedEventArgs e)
        {
            // Pobierz dostawcę z zaznaczonej dostawy
            var dostawa = _dostawy.FirstOrDefault(d => d.LP == _selectedLP) ??
                          _dostawyNastepnyTydzien.FirstOrDefault(d => d.LP == _selectedLP);

            if (dostawa == null)
            {
                ShowToast("Wybierz dostawę", ToastType.Warning);
                return;
            }

            try
            {
                // Tworzenie nowej instancji WidokWaga
                WidokWaga widokWaga = new WidokWaga();

                // Ustawienie wartości TextBoxa w WidokWaga (dostawca)
                widokWaga.TextBoxValue = dostawa.Dostawca;

                // Ustaw wartość TextBoxa przed wyświetleniem formularza
                widokWaga.SetTextBoxValue();

                // Wyświetlanie formularza
                widokWaga.Show();
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd otwierania widoku wag: {ex.Message}", ToastType.Error);
            }
        }

        // Pokaż wszystkie dostawy
        private void MenuPokazDostawy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                WidokWszystkichDostaw widokWszystkichDostaw = new WidokWszystkichDostaw();
                widokWszystkichDostaw.Show();
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd otwierania widoku dostaw: {ex.Message}", ToastType.Error);
            }
        }

        // Pokaż ceny
        private void MenuPokazCeny_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                WidokCenWszystkich widokCenWszystkich = new WidokCenWszystkich();
                widokCenWszystkich.Show();
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd otwierania widoku cen: {ex.Message}", ToastType.Error);
            }
        }

        // Dodaj cenę
        private void MenuDodajCene_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                WidokCena widokcena = new WidokCena();
                widokcena.Show();
                DodajAktywnosc(6);
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd otwierania formularza ceny: {ex.Message}", ToastType.Error);
            }
        }

        // Pokaż pasze/pisklęta
        private void MenuPokazPaszePiskleta_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                WidokPaszaPisklak widokPaszaPisklak = new WidokPaszaPisklak();
                widokPaszaPisklak.Show();
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd otwierania widoku pasz/piskląt: {ex.Message}", ToastType.Error);
            }
        }

        // Pokaż cenę tuszki
        private void MenuPokazTuszke_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PokazCeneTuszki pokazCeneTuszki = new PokazCeneTuszki();
                pokazCeneTuszki.Show();
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd otwierania widoku tuszki: {ex.Message}", ToastType.Error);
            }
        }

        // Pokaż avilog
        private void MenuPokazAvilog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                WidokAvilogPlan widokAvilogPlan = new WidokAvilogPlan();
                widokAvilogPlan.Show();
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd otwierania widoku avilog: {ex.Message}", ToastType.Error);
            }
        }

        // Pokaż plan sprzedaży
        private void MenuPokazPlanSprzedazy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                WidokSprzedazPlan widokSprzedazPlan = new WidokSprzedazPlan();
                widokSprzedazPlan.Show();
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd otwierania planu sprzedaży: {ex.Message}", ToastType.Error);
            }
        }

        /// <summary>
        /// Pokaż historię zmian dla wybranego LP (menu kontekstowe)
        /// </summary>
        private void MenuPokazHistorieLP_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Pobierz zaznaczoną dostawę
                var selected = dgDostawy.SelectedItem as DostawaModel ?? dgDostawyNastepny.SelectedItem as DostawaModel;

                if (selected == null || selected.IsHeaderRow || selected.IsSeparator)
                {
                    ShowToast("Wybierz dostawę, aby zobaczyć historię zmian", ToastType.Warning);
                    return;
                }

                // Otwórz okno historii z filtrem LP i nazwą hodowcy (pełny ekran)
                var historiaWindow = new HistoriaZmianWindow(ConnectionString, UserID, selected.LP, selected.Dostawca);
                historiaWindow.Show();
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd otwierania historii: {ex.Message}", ToastType.Error);
            }
        }

        // Dodanie aktywności do bazy danych
        private async void DodajAktywnosc(int typLicznika)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);

                    int nextLp;
                    using (SqlCommand getMaxCmd = new SqlCommand("SELECT ISNULL(MAX(Lp), 0) + 1 FROM Aktywnosc", conn))
                    {
                        var result = await getMaxCmd.ExecuteScalarAsync(_cts.Token);
                        nextLp = Convert.ToInt32(result);
                    }

                    string insertQuery = @"
                        INSERT INTO Aktywnosc (Lp, Licznik, TypLicznika, KtoStworzyl, Data)
                        VALUES (@Lp, @Licznik, @TypLicznika, @KtoStworzyl, @Data)";

                    using (SqlCommand cmd = new SqlCommand(insertQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@Lp", nextLp);
                        cmd.Parameters.AddWithValue("@Licznik", 1);
                        cmd.Parameters.AddWithValue("@TypLicznika", typLicznika);
                        cmd.Parameters.AddWithValue("@KtoStworzyl", UserID ?? "0");
                        cmd.Parameters.AddWithValue("@Data", DateTime.Now);

                        await cmd.ExecuteNonQueryAsync(_cts.Token);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd logowania aktywności: {ex.Message}");
            }
        }

        #endregion

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

        private static readonly string[] DniSkrotSms = { "niedz", "pon", "wt", "śr", "czw", "pt", "sob" };

        private void MenuKopiujSMS_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedLP))
            {
                ShowToast("Wybierz dostawę", ToastType.Warning);
                return;
            }

            var dostawa = _dostawy.FirstOrDefault(d => d.LP == _selectedLP)
                          ?? _dostawyNastepnyTydzien.FirstOrDefault(d => d.LP == _selectedLP);
            if (dostawa == null || dostawa.IsHeaderRow || dostawa.IsSeparator)
            {
                ShowToast("Wybierz dostawę", ToastType.Warning);
                return;
            }

            string sms = BuildDostawaSmsText(dostawa);

            try
            {
                Clipboard.SetText(sms);
                ShowToast("📱 SMS skopiowany do schowka", ToastType.Success);
            }
            catch
            {
                ShowToast("Nie udało się skopiować do schowka", ToastType.Error);
            }

            MessageBox.Show(sms, "📱 SMS — szczegóły dostawy", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private static string BuildDostawaSmsText(DostawaModel d)
        {
            var dataUboj = d.DataOdbioru.Date;
            var dataTransport = dataUboj.AddDays(-1);
            string dniUboj = DniSkrotSms[(int)dataUboj.DayOfWeek];
            string dniTransport = DniSkrotSms[(int)dataTransport.DayOfWeek];

            string dobaInfo;
            if (d.RoznicaDni.HasValue && d.RoznicaDni.Value > 0)
            {
                var dataWstawienia = dataUboj.AddDays(-d.RoznicaDni.Value);
                dobaInfo = $"{d.RoznicaDni.Value} dni (wstawienie {dataWstawienia:dd.MM.yyyy})";
            }
            else
            {
                dobaInfo = "brak danych";
            }

            return
                $"🚛 Dostawa z {dniTransport} {dataTransport:dd.MM.yyyy} na dzień ubojowy {dniUboj} {dataUboj:dd.MM.yyyy}." + Environment.NewLine +
                Environment.NewLine +
                $"📌 Dostawca: {d.Dostawca}" + Environment.NewLine +
                $"🚚 Auta: {d.Auta}" + Environment.NewLine +
                $"🐔 Sztuki: {d.SztukiDek:#,0}" + Environment.NewLine +
                $"⚖️ Waga: {d.WagaDek:0.00} kg" + Environment.NewLine +
                $"📅 Doba: {dobaInfo}" + Environment.NewLine +
                Environment.NewLine +
                "⚠️ Sztuki i waga do potwierdzenia." + Environment.NewLine +
                "ℹ️ Informacje o transporcie dzień przed załadunkiem." + Environment.NewLine +
                Environment.NewLine +
                "Ubojnia Drobiu Piórkowscy";
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

            // TRYB SYMULACJI - usunięcie z pamięci
            if (_isSimulationMode)
            {
                var simDostawa = _dostawy.FirstOrDefault(d => d.LP == _selectedLP) ??
                                 _dostawyNastepnyTydzien.FirstOrDefault(d => d.LP == _selectedLP);
                if (simDostawa != null)
                {
                    _dostawy.Remove(simDostawa);
                    _dostawyNastepnyTydzien.Remove(simDostawa);
                    RefreshDostawyView();
                    IncrementSimulationChangeCount();
                    ShowToast($"🗑️ Usunięto {simDostawa.Dostawca} (symulacja)", ToastType.Info);
                }
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
        private Storyboard _simulationPulseStoryboard1;
        private Storyboard _simulationPulseStoryboard2;
        private Storyboard _simulationButtonPulseStoryboard;

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

            // Wyczyść listę zmian symulacji
            _simulationChanges.Clear();
            UpdateSimulationStats();
            if (borderSimulationChanges != null) borderSimulationChanges.Visibility = Visibility.Visible;
            SetLiveIndicator(false); // Pauza live refresh w symulacji

            // Zmień wygląd UI - tryb symulacji aktywny (czerwony motyw)
            borderSimulation.Background = new SolidColorBrush(Color.FromRgb(254, 226, 226)); // Czerwone tło
            borderSimulation.BorderBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Czerwona ramka
            txtSimulationIcon.Text = "🔴";
            txtSimulationText.Text = "SYMULACJA";
            txtSimulationText.FontWeight = FontWeights.Bold;
            txtSimulationText.Foreground = new SolidColorBrush(Color.FromRgb(185, 28, 28));

            // Ustaw czerwoną ramkę na tabelach i uruchom pulsowanie
            borderDostawy.BorderBrush = new SolidColorBrush(Color.FromRgb(229, 57, 53));
            borderDostawy.BorderThickness = new Thickness(3);
            borderNastepnyTydzien.BorderBrush = new SolidColorBrush(Color.FromRgb(229, 57, 53));
            borderNastepnyTydzien.BorderThickness = new Thickness(3);

            // Uruchom animację pulsowania tabel
            _simulationPulseStoryboard1 = (Storyboard)FindResource("SimulationPulseAnimation");
            _simulationPulseStoryboard2 = _simulationPulseStoryboard1.Clone();
            Storyboard.SetTarget(_simulationPulseStoryboard1, borderDostawy);
            Storyboard.SetTarget(_simulationPulseStoryboard2, borderNastepnyTydzien);
            _simulationPulseStoryboard1.Begin();
            _simulationPulseStoryboard2.Begin();

            // Uruchom animację pulsowania przycisku
            _simulationButtonPulseStoryboard = (Storyboard)FindResource("SimulationButtonPulseAnimation");
            Storyboard.SetTarget(_simulationButtonPulseStoryboard, borderSimulation);
            _simulationButtonPulseStoryboard.Begin();

            // Dodaj pasek informacyjny na górze
            ShowSimulationBanner(true);

            ShowToast("🔴 Tryb symulacji WŁĄCZONY - zmiany nie będą zapisywane!", ToastType.Info);
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

            // Zatrzymaj animacje pulsowania
            _simulationPulseStoryboard1?.Stop();
            _simulationPulseStoryboard2?.Stop();
            _simulationButtonPulseStoryboard?.Stop();

            // Przywróć oryginalne dane z kopii
            RestoreFromBackup();

            // Przywróć wygląd UI przycisku
            borderSimulation.Background = new SolidColorBrush(Color.FromRgb(248, 250, 252));
            borderSimulation.BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240));
            txtSimulationIcon.Text = "🧪";
            txtSimulationText.Text = "Symulacja";
            txtSimulationText.FontWeight = FontWeights.Normal;
            txtSimulationText.Foreground = Brushes.Black;

            // Przywróć oryginalne ramki tabel
            borderDostawy.BorderBrush = new SolidColorBrush(Color.FromRgb(221, 221, 221));
            borderDostawy.BorderThickness = new Thickness(1);
            borderNastepnyTydzien.BorderBrush = new SolidColorBrush(Color.FromRgb(52, 152, 219));
            borderNastepnyTydzien.BorderThickness = new Thickness(2);

            // Ukryj pasek informacyjny
            ShowSimulationBanner(false);

            // Wznów auto-refresh
            _refreshTimer?.Start();
            _countdownTimer?.Start();

            // Wyczyść kopie zapasowe
            _simulationBackup.Clear();
            _simulationBackupNastepny.Clear();

            // Wyczyść listę zmian symulacji i ukryj panel
            _simulationChanges.Clear();
            if (borderSimulationChanges != null) borderSimulationChanges.Visibility = Visibility.Collapsed;
            SetLiveIndicator(_isWindowActive); // Wznów live refresh po symulacji

            ShowToast("✅ Symulacja zakończona - dane przywrócone", ToastType.Success);
        }

        /// <summary>
        /// Dodaje wpis do listy zmian symulacji
        /// </summary>
        private void AddSimulationChange(DostawaModel dostawa, DateTime oldDate, DateTime newDate)
        {
            if (!_isSimulationMode) return;
            _simulationChanges.Insert(0, new SimulationChange
            {
                LP = dostawa.LP,
                Dostawca = dostawa.Dostawca,
                OldDate = oldDate,
                NewDate = newDate,
                Timestamp = DateTime.Now
            });
            UpdateSimulationStats();
        }

        /// <summary>
        /// Aktualizuje statystyki w panelu symulacji (w przód / w tył)
        /// </summary>
        private void UpdateSimulationStats()
        {
            if (txtSimForward == null || txtSimBackward == null) return;
            int forward = _simulationChanges.Count(c => c.DayDelta > 0);
            int backward = _simulationChanges.Count(c => c.DayDelta < 0);
            txtSimForward.Text = forward.ToString();
            txtSimBackward.Text = backward.ToString();
        }

        /// <summary>
        /// Kopiuje listę zmian symulacji do schowka (w czytelnym formacie)
        /// </summary>
        private void BtnCopySimulationChanges_Click(object sender, RoutedEventArgs e)
        {
            if (_simulationChanges.Count == 0)
            {
                ShowToast("Brak zmian do skopiowania", ToastType.Warning);
                return;
            }

            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Zmiany w symulacji ({_simulationChanges.Count}) - {DateTime.Now:yyyy-MM-dd HH:mm}");
                sb.AppendLine(new string('-', 60));
                foreach (var ch in _simulationChanges)
                {
                    sb.AppendLine($"{ch.Timestamp:HH:mm:ss}  {ch.Display}");
                }
                Clipboard.SetText(sb.ToString());
                ShowToast($"📋 Skopiowano {_simulationChanges.Count} zmian do schowka", ToastType.Success);
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd kopiowania: {ex.Message}", ToastType.Error);
            }
        }

        /// <summary>
        /// Cofa pojedynczą zmianę symulacji (klik X na karcie)
        /// </summary>
        private void BtnRevertSimulationChange_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var change = btn?.Tag as SimulationChange;
            if (change == null) return;

            RevertSingleSimulationChange(change);
            e.Handled = true; // Nie propaguj do ListBoxa (SelectionChanged)
        }

        /// <summary>
        /// Cofa wszystkie zmiany symulacji (przywraca wiersze na oryginalne daty/kolejność)
        /// </summary>
        private void BtnRevertAllSimulation_Click(object sender, RoutedEventArgs e)
        {
            if (_simulationChanges.Count == 0)
            {
                ShowToast("Brak zmian do cofnięcia", ToastType.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"Cofnąć wszystkie przeniesienia symulacji ({_simulationChanges.Count})?",
                "Cofnij wszystkie zmiany",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            // Przywróć dane z kopii zapasowej
            RestoreFromBackup();
            _simulationChanges.Clear();
            UpdateSimulationStats();
            ShowToast("↶ Cofnięto wszystkie przeniesienia", ToastType.Success);
        }

        /// <summary>
        /// Cofa pojedynczą zmianę: przywraca OldDate i oryginalny SortOrder z backupu
        /// </summary>
        private void RevertSingleSimulationChange(SimulationChange change)
        {
            // Znajdź dostawę w aktualnych kolekcjach
            var dostawa = _dostawy.FirstOrDefault(d => d.LP == change.LP && !d.IsHeaderRow && !d.IsSeparator)
                       ?? _dostawyNastepnyTydzien.FirstOrDefault(d => d.LP == change.LP && !d.IsHeaderRow && !d.IsSeparator);
            if (dostawa == null)
            {
                // Nie znaleziono — tylko usuń wpis z listy
                _simulationChanges.Remove(change);
                UpdateSimulationStats();
                return;
            }

            // Odtwórz oryginalny SortOrder z backupu
            var backup = _simulationBackup.FirstOrDefault(d => d.LP == change.LP)
                      ?? _simulationBackupNastepny.FirstOrDefault(d => d.LP == change.LP);
            if (backup != null)
            {
                dostawa.SortOrder = backup.SortOrder;
            }

            // Przywróć starą datę
            dostawa.DataOdbioru = change.OldDate;

            // Usuń wpis z listy
            _simulationChanges.Remove(change);

            // Jeśli nie ma już zmian dla tego LP — odznacz flagę przesunięcia
            bool stillMoved = _simulationChanges.Any(c => c.LP == change.LP);
            if (!stillMoved)
                dostawa.IsSimulationMoved = false;

            // Odśwież widok
            RefreshDostawyView();
            UpdateSimulationStats();

            ShowToast($"↶ Cofnięto: {change.Dostawca}", ToastType.Info);
        }

        /// <summary>
        /// Klik karty zmiany → przejdź do dostawy w tabeli (selekcja + scroll)
        /// </summary>
        private void LstSimulationChanges_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listBox = sender as ListBox;
            var change = listBox?.SelectedItem as SimulationChange;
            if (change == null) return;

            NavigateToDelivery(change.LP);

            // Odznacz aby klik na ten sam element ponownie wywoływał nawigację
            listBox.SelectedItem = null;
        }

        /// <summary>
        /// Szuka dostawę w obu DataGridach i zaznacza ją + przewija do widoku
        /// </summary>
        private void NavigateToDelivery(string lp)
        {
            var item1 = _dostawy.FirstOrDefault(d => d.LP == lp && !d.IsHeaderRow && !d.IsSeparator);
            if (item1 != null)
            {
                dgDostawy.SelectedItem = item1;
                dgDostawy.ScrollIntoView(item1);
                dgDostawy.Focus();
                _selectedLP = lp;
                return;
            }

            var item2 = _dostawyNastepnyTydzien.FirstOrDefault(d => d.LP == lp && !d.IsHeaderRow && !d.IsSeparator);
            if (item2 != null)
            {
                dgDostawyNastepny.SelectedItem = item2;
                dgDostawyNastepny.ScrollIntoView(item2);
                dgDostawyNastepny.Focus();
                _selectedLP = lp;
            }
        }

        /// <summary>
        /// Przywraca dane z kopii zapasowej (instant, bez zapytania do bazy)
        /// </summary>
        private void RestoreFromBackup()
        {
            _dostawy.Clear();
            _dostawyNastepnyTydzien.Clear();

            RebuildGroupedView(_dostawy, _simulationBackup.Select(d => CloneDostawaModel(d)).ToList(), _selectedDate);
            RebuildGroupedView(_dostawyNastepnyTydzien, _simulationBackupNastepny.Select(d => CloneDostawaModel(d)).ToList(), _selectedDate.AddDays(7));
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
                UwagiAutorID = original.UwagiAutorID,
                UwagiAutorName = original.UwagiAutorName,
                LpW = original.LpW,
                DataNotatki = original.DataNotatki,
                RoznicaDni = original.RoznicaDni,
                Ubytek = original.Ubytek,
                IsConfirmed = original.IsConfirmed,
                IsWstawienieConfirmed = original.IsWstawienieConfirmed,
                IsHeaderRow = original.IsHeaderRow,
                IsSeparator = original.IsSeparator,
                IsEmptyDay = original.IsEmptyDay,
                SortOrder = original.SortOrder,
                IsSimulationMoved = original.IsSimulationMoved
            };
        }

        /// <summary>
        /// Pokazuje/ukrywa pasek informacyjny trybu symulacji
        /// </summary>
        private void ShowSimulationBanner(bool show)
        {
            if (show)
            {
                // Zmień tło okna
                this.Background = new SolidColorBrush(Color.FromRgb(255, 251, 235));

                // Utwórz banner jeśli nie istnieje
                if (_simulationBannerElement == null)
                {
                    _simulationBannerElement = new Border
                    {
                        Background = new LinearGradientBrush(
                            Color.FromRgb(220, 38, 38), Color.FromRgb(185, 28, 28), 0),
                        Padding = new Thickness(12, 6, 12, 6),
                        VerticalAlignment = VerticalAlignment.Top,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        CornerRadius = new CornerRadius(0, 0, 6, 6)
                    };
                    Panel.SetZIndex(_simulationBannerElement, 999);

                    var stack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };

                    var icon = new TextBlock
                    {
                        Text = "⚠️",
                        FontSize = 14,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 8, 0)
                    };

                    var label = new TextBlock
                    {
                        Text = "TRYB SYMULACJI — zmiany NIE są zapisywane do bazy",
                        Foreground = Brushes.White,
                        FontSize = 13,
                        FontWeight = FontWeights.SemiBold,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 16, 0)
                    };

                    var changeCounter = new TextBlock
                    {
                        Text = "Zmian: 0",
                        Foreground = new SolidColorBrush(Color.FromRgb(254, 202, 202)),
                        FontSize = 12,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 16, 0),
                        Tag = "changeCounter"
                    };

                    var timer = new TextBlock
                    {
                        Text = "Czas: 0:00",
                        Foreground = new SolidColorBrush(Color.FromRgb(254, 202, 202)),
                        FontSize = 12,
                        VerticalAlignment = VerticalAlignment.Center,
                        Tag = "timer"
                    };

                    stack.Children.Add(icon);
                    stack.Children.Add(label);
                    stack.Children.Add(changeCounter);
                    stack.Children.Add(timer);
                    _simulationBannerElement.Child = stack;
                }

                // Dodaj banner do głównego Grid
                var rootGrid = this.Content as Grid;
                if (rootGrid != null && !rootGrid.Children.Contains(_simulationBannerElement))
                {
                    Grid.SetColumnSpan(_simulationBannerElement, 2);
                    rootGrid.Children.Add(_simulationBannerElement);
                }

                // Resetuj liczniki
                _simulationChangeCount = 0;
                _simulationStartTime = DateTime.Now;
            }
            else
            {
                // Przywróć tło
                this.Background = new SolidColorBrush(Color.FromRgb(236, 239, 241));

                // Usuń banner
                if (_simulationBannerElement != null)
                {
                    var rootGrid = this.Content as Grid;
                    rootGrid?.Children.Remove(_simulationBannerElement);
                }
            }
        }

        /// <summary>
        /// Zwiększa licznik zmian w symulacji i aktualizuje banner
        /// </summary>
        private void IncrementSimulationChangeCount()
        {
            if (!_isSimulationMode) return;
            _simulationChangeCount++;
            UpdateSimulationBanner();
        }

        /// <summary>
        /// Aktualizuje tekst na banerze symulacji (licznik zmian + czas)
        /// </summary>
        private void UpdateSimulationBanner()
        {
            if (_simulationBannerElement?.Child is StackPanel stack)
            {
                foreach (var child in stack.Children.OfType<TextBlock>())
                {
                    if (child.Tag as string == "changeCounter")
                        child.Text = $"Zmian: {_simulationChangeCount}";
                    else if (child.Tag as string == "timer")
                    {
                        var elapsed = DateTime.Now - _simulationStartTime;
                        child.Text = $"Czas: {(int)elapsed.TotalMinutes}:{elapsed.Seconds:D2}";
                    }
                }
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

            // Zaktualizuj wiek kurczaków (kolumna "dni") po przesunięciu dostawy
            int shiftDays = (newDate.Date - oldDate.Date).Days;
            if (dostawa.RoznicaDni.HasValue && shiftDays != 0)
            {
                dostawa.RoznicaDni = dostawa.RoznicaDni.Value + shiftDays;
            }

            // Oznacz jako przesunięty (czerwona czcionka)
            dostawa.IsSimulationMoved = true;

            // Przydziel SortOrder na koniec dnia docelowego aby nie psuć istniejącej kolejności
            var allData = _dostawy.Where(d => !d.IsHeaderRow && !d.IsSeparator)
                .Concat(_dostawyNastepnyTydzien.Where(d => !d.IsHeaderRow && !d.IsSeparator));
            int maxSort = allData.Where(d => d.DataOdbioru.Date == newDate.Date && d.LP != lp)
                                 .Select(d => (int?)d.SortOrder).Max() ?? -1;
            dostawa.SortOrder = maxSort + 1;

            // Dodaj wpis do listy zmian symulacji
            AddSimulationChange(dostawa, oldDate, newDate);

            // Odśwież widok (przebuduj grupy)
            RefreshDostawyView();

            IncrementSimulationChangeCount();
            ShowToast($"📅 {dostawa.Dostawca}: {oldDate:dd.MM} → {newDate:dd.MM}", ToastType.Info);
        }

        /// <summary>
        /// Odświeża widok dostaw po zmianach w symulacji
        /// Redistribuuje dostawy między tygodniami na podstawie ich aktualnych dat
        /// </summary>
        private void RefreshDostawyView()
        {
            // Pobierz wszystkie dostawy z obu tabel (bez nagłówków)
            var allData = _dostawy.Where(d => !d.IsHeaderRow && !d.IsSeparator)
                .Concat(_dostawyNastepnyTydzien.Where(d => !d.IsHeaderRow && !d.IsSeparator))
                .ToList();

            // Oblicz zakresy tygodni
            DateTime startOfWeek1 = _selectedDate.AddDays(-(int)_selectedDate.DayOfWeek + 1);
            if (_selectedDate.DayOfWeek == DayOfWeek.Sunday) startOfWeek1 = _selectedDate.AddDays(-6);
            DateTime endOfWeek1 = startOfWeek1.AddDays(7);

            DateTime startOfWeek2 = endOfWeek1;
            DateTime endOfWeek2 = startOfWeek2.AddDays(7);

            // Rozdziel dostawy wg dat na właściwy tydzień
            var dostawyData = allData.Where(d => d.DataOdbioru.Date >= startOfWeek1 && d.DataOdbioru.Date < endOfWeek1).ToList();
            var dostawyNastepnyData = allData.Where(d => d.DataOdbioru.Date >= startOfWeek2 && d.DataOdbioru.Date < endOfWeek2).ToList();

            // Wyczyść i przebuduj z nowymi grupami
            _dostawy.Clear();
            _dostawyNastepnyTydzien.Clear();

            RebuildGroupedView(_dostawy, dostawyData, _selectedDate);
            RebuildGroupedView(_dostawyNastepnyTydzien, dostawyNastepnyData, _selectedDate.AddDays(7));
        }

        /// <summary>
        /// Buduje pogrupowany widok dostaw (z pustymi dniami)
        /// </summary>
        private void RebuildGroupedView(ObservableCollection<DostawaModel> collection, List<DostawaModel> data, DateTime baseDate)
        {
            // Oblicz poniedziałek tygodnia
            DateTime startOfWeek = baseDate.AddDays(-(int)baseDate.DayOfWeek + 1);
            if (baseDate.DayOfWeek == DayOfWeek.Sunday) startOfWeek = baseDate.AddDays(-6);

            var grouped = data.GroupBy(d => d.DataOdbioru.Date).ToDictionary(g => g.Key, g => g.ToList());

            // Iteruj przez wszystkie 7 dni tygodnia
            for (int i = 0; i < 7; i++)
            {
                DateTime currentDay = startOfWeek.AddDays(i);
                var deliveries = grouped.ContainsKey(currentDay) ? grouped[currentDay] : new List<DostawaModel>();

                // Oblicz sumy i średnie ważone dla tego dnia
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

                    if (item.RoznicaDni.HasValue && item.RoznicaDni.Value > 0)
                    {
                        sumaDobyPomnozona += item.RoznicaDni.Value * item.Auta;
                        iloscZDoby += item.Auta;
                    }

                    if (item.WagaDek >= 0.5m && item.WagaDek <= 2.4m)
                    {
                        sumaUbytek += item.Auta;
                    }

                    if (item.Bufor == "Sprzedany") liczbaSprzedanych++;
                    if (item.Bufor == "Anulowany") liczbaAnulowanych++;
                }

                double sredniaWaga = sumaAuta > 0 ? sumaWagaPomnozona / sumaAuta : 0;
                double sredniaCena = sumaAuta > 0 ? sumaCenaPomnozona / sumaAuta : 0;
                double sredniaKM = sumaAuta > 0 ? sumaKMPomnozona / sumaAuta : 0;
                double sredniaDoby = iloscZDoby > 0 ? sumaDobyPomnozona / iloscZDoby : 0;

                // Dodaj nagłówek dnia
                var dayHeader = new DostawaModel
                {
                    IsHeaderRow = true,
                    IsEmptyDay = deliveries.Count == 0,
                    DataOdbioru = currentDay,
                    Dostawca = GetDayName(currentDay),
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

                if (deliveries.Count > 0)
                {
                    // Zachowaj oryginalną kolejność z ładowania (SortOrder) - nie sortuj alfabetycznie
                    foreach (var d in deliveries.OrderBy(x => x.SortOrder))
                    {
                        collection.Add(d);
                    }
                }
                else
                {
                    // Dodaj separator dla pustego dnia
                    collection.Add(new DostawaModel { IsSeparator = true, DataOdbioru = currentDay });
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
                // Non-modal - bez Owner i bez ShowDialog, żeby nie blokowało innych okien
                // i żeby kalendarz mógł pozostać używalny.
                var historiaWindow = new HistoriaZmianWindow(ConnectionString, UserID);
                historiaWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd otwarcia historii zmian:\n{ex.Message}\n\n{ex.StackTrace}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        private void BtnMapaDostaw_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var monday = _selectedDate;
                while (monday.DayOfWeek != DayOfWeek.Monday)
                    monday = monday.AddDays(-1);
                var mapaWindow = new MapaDostawWindow(monday);
                mapaWindow.Show();
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd otwarcia mapy: {ex.Message}", ToastType.Error);
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
                        cmd.Parameters.AddWithValue("@WagaDek", decimal.TryParse(txtWagaDek.Text.Replace(",", "."), System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture, out decimal w) ? w : (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@SztSzuflada", int.TryParse(txtSztNaSzuflade.Text, out int sz) ? sz : (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@TypUmowy", cmbTypUmowy.SelectedItem ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@TypCeny", cmbTypCeny.SelectedItem ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Cena", decimal.TryParse(txtCena.Text.Replace(",", "."), System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture, out decimal c) ? c : (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Dodatek", decimal.TryParse(txtDodatek.Text.Replace(",", "."), System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture, out decimal d) ? d : (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Bufor", cmbStatus.SelectedItem ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@DataMod", DateTime.Now);
                        cmd.Parameters.AddWithValue("@KtoMod", UserID ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Lp", _selectedLP);

                        await cmd.ExecuteNonQueryAsync(_cts.Token);
                    }
                }

                // Zbierz zmiany (do audytu i powiadomienia)
                var changes = new Dictionary<string, (object OldValue, object NewValue)>();
                if (oldDostawa != null)
                {
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
                }

                // AUDIT LOG
                if (_auditService != null && changes.Count > 0)
                {
                    await _auditService.LogFullDeliverySaveAsync(_selectedLP, changes,
                        newDostawca, newDataOdbioru, _cts.Token);
                }

                ShowToast("Zmiany zapisane", ToastType.Success);

                // Powiadomienie ze szczegółami zmian
                if (changes.Count > 0)
                {
                    ShowChangeNotification(new ChangeNotificationItem
                    {
                        Title = "Zapisano dostawę",
                        Dostawca = newDostawca ?? oldDostawa?.Dostawca,
                        LP = _selectedLP,
                        DataOdbioru = newDataOdbioru ?? oldDostawa?.DataOdbioru,
                        UserId = UserID,
                        UserName = txtUserName?.Text,
                        NotificationType = ChangeNotificationType.FormSave,
                        Changes = changes.Select(c => new FieldChange
                        {
                            FieldName = MapFieldName(c.Key),
                            OldValue = c.Value.OldValue?.ToString() ?? "-",
                            NewValue = c.Value.NewValue?.ToString() ?? "-"
                        }).ToList()
                    });
                }

                // Batch refresh: aktualizuj in-memory zamiast pełnego reload
                bool dateChanged = oldDostawa != null && newDataOdbioru.HasValue && oldDostawa.DataOdbioru.Date != newDataOdbioru.Value.Date;
                ApplyLocalUpdate(_selectedLP, d =>
                {
                    if (newDataOdbioru.HasValue) d.DataOdbioru = newDataOdbioru.Value;
                    if (!string.IsNullOrEmpty(newDostawca)) d.Dostawca = newDostawca;
                    d.Auta = newAuta;
                    d.SztukiDek = newSztuki;
                    d.WagaDek = newWaga;
                    if (!string.IsNullOrEmpty(newTypCeny)) d.TypCeny = newTypCeny;
                    d.Cena = newCena;
                    if (!string.IsNullOrEmpty(newStatus))
                    {
                        d.Bufor = newStatus;
                        d.IsConfirmed = newStatus == "Potwierdzony";
                    }
                });
                // Jeśli zmieniła się data, przebuduj widok aby wiersz trafił do właściwego dnia/tygodnia
                if (dateChanged) RefreshDostawyView();
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

        private string FormatPhoneNumber(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return phone;
            var digits = new string(phone.Where(char.IsDigit).ToArray());
            if (digits.Length == 9)
                return $"{digits.Substring(0, 3)} {digits.Substring(3, 3)} {digits.Substring(6, 3)}";
            return phone;
        }

        private async void BtnZapiszHodowce_Click(object sender, RoutedEventArgs e)
        {
            string hodowca = cmbDostawca.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(hodowca))
            {
                ShowToast("Wybierz hodowcę", ToastType.Warning);
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    string sql = @"UPDATE Dostawcy SET
                                   Address = @Address, PostalCode = @PostalCode, City = @City,
                                   Distance = @Distance, Email = @Email,
                                   Phone1 = @Phone1, Phone2 = @Phone2, Phone3 = @Phone3,
                                   TypOsobowosci = @TypOsobowosci, TypOsobowosci2 = @TypOsobowosci2
                                   WHERE Name = @Name";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Address", (object)txtUlicaH.Text ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@PostalCode", (object)txtKodPocztowyH.Text ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@City", (object)txtMiejscH.Text ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Distance", int.TryParse(txtKmH.Text, out int km) ? km : (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Email", (object)txtEmail.Text ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Phone1", (object)txtTel1.Text ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Phone2", (object)txtTel2.Text ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Phone3", (object)txtTel3.Text ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@TypOsobowosci", cmbOsobowosc1.SelectedItem ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@TypOsobowosci2", cmbOsobowosc2.SelectedItem ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Name", hodowca);

                        await cmd.ExecuteNonQueryAsync(_cts.Token);
                    }
                }

                ShowToast("Dane hodowcy zapisane", ToastType.Success);
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd zapisu hodowcy: {ex.Message}", ToastType.Error);
            }
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

        private async void BtnEdytujWstawienie_Click(object sender, RoutedEventArgs e)
        {
            // Wybierz LpW: najpierw z comboboxa, w razie braku - z zaznaczonej dostawy
            string selectedLp = cmbLpWstawienia.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedLp) && !string.IsNullOrEmpty(_selectedLP))
            {
                var dostawa = _dostawy.FirstOrDefault(d => d.LP == _selectedLP) ?? _dostawyNastepnyTydzien.FirstOrDefault(d => d.LP == _selectedLP);
                selectedLp = dostawa?.LpW;
            }

            if (string.IsNullOrEmpty(selectedLp) || !int.TryParse(selectedLp, out int lpWstawienia))
            {
                ShowToast("Wybierz wstawienie do edycji", ToastType.Warning);
                return;
            }

            try
            {
                string dostawca = null;
                DateTime dataWstawienia = DateTime.Today;
                int sztWstawione = 0;

                using (var conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync(_cts.Token);
                    using (var cmd = new SqlCommand(
                        "SELECT Dostawca, DataWstawienia, IloscWstawienia FROM dbo.WstawieniaKurczakow WHERE Lp = @lp", conn))
                    {
                        cmd.Parameters.AddWithValue("@lp", lpWstawienia);
                        using (var reader = await cmd.ExecuteReaderAsync(_cts.Token))
                        {
                            if (await reader.ReadAsync(_cts.Token))
                            {
                                dostawca = reader["Dostawca"]?.ToString();
                                dataWstawienia = reader["DataWstawienia"] != DBNull.Value
                                    ? Convert.ToDateTime(reader["DataWstawienia"])
                                    : DateTime.Today;
                                sztWstawione = reader["IloscWstawienia"] != DBNull.Value
                                    ? Convert.ToInt32(reader["IloscWstawienia"])
                                    : 0;
                            }
                            else
                            {
                                ShowToast($"Nie znaleziono wstawienia Lp={lpWstawienia}", ToastType.Warning);
                                return;
                            }
                        }
                    }
                }

                var wstawienie = new WstawienieWindow
                {
                    UserID = UserID ?? "0",
                    SztWstawienia = sztWstawione,
                    Dostawca = dostawca,
                    LpWstawienia = lpWstawienia,
                    DataWstawienia = dataWstawienia,
                    Modyfikacja = true,
                    Owner = this
                };

                wstawienie.ShowDialog();

                // Odśwież dane po zamknięciu okna
                await LoadDostawyAsync();
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd: {ex.Message}", ToastType.Error);
            }
        }

        #endregion

        #region Obsługa transportu

        /// <summary>
        /// Oblicza obl.A = SztukiDek / (SztSzuflada × 264)
        /// </summary>
        private void UpdateObliczoneAuta()
        {
            if (txtOblA == null || txtSztuki == null || txtSztNaSzuflade == null) return;

            if (double.TryParse(txtSztuki.Text, out double sztuki) &&
                int.TryParse(txtSztNaSzuflade.Text, out int sztSzuf) && sztSzuf > 0)
            {
                double pojemnosc = sztSzuf * 264.0;
                double oblA = sztuki / pojemnosc;
                txtOblA.Text = oblA.ToString("F2");
            }
            else
            {
                txtOblA.Text = "-";
            }
        }

        private void TxtSztuki_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateObliczoneAuta();
        }

        private void TxtAuta_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateObliczoneAuta();
        }

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

                // Aktualizuj też sztuki jeśli jest podana ilość aut
                if (int.TryParse(txtObliczoneAuta.Text, out int auta))
                {
                    txtObliczoneSztuki.Text = (wyliczone * auta).ToString();
                }
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

            // Przelicz obl.A na bieżąco
            UpdateObliczoneAuta();

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

        // Notatki są dodawane przez dwuklik na kolumnie Uwagi lub przez menu kontekstowe
        // Zakładka Notatki w panelu bocznym pokazuje tylko historię (bez dodawania)

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
                string fullPath = System.IO.Path.Combine(photosRoot, partia.FolderPath.Replace('/', '\\'));

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
            bool inWindow1 = now >= SURVEY_START && now <= SURVEY_END;
            bool inWindow2 = now >= SURVEY_START_2 && now <= SURVEY_END_2;
            if (inWindow1 || inWindow2)
            {
                _surveyShownThisSession = true;
                // Tutaj wywołanie ankiety jeśli jest zaimplementowana
            }
        }

        #endregion

        #region Drag & Drop

        private void DgDostawy_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Ignoruj jeśli menu kontekstowe jest otwarte lub było niedawno zamknięte
            if (_isContextMenuOpen || (DateTime.Now - _contextMenuClosedTime).TotalMilliseconds < CONTEXT_MENU_DRAG_BLOCK_MS)
                return;

            // Ignoruj jeśli inline edit popup jest otwarty
            if (_inlineEditPopup != null && _inlineEditPopup.IsOpen)
                return;

            // Blokuj drag przez 500ms po zamknięciu inline edit popup
            if ((DateTime.Now - _inlineEditClosedTime).TotalMilliseconds < CONTEXT_MENU_DRAG_BLOCK_MS)
            {
                _skipNextDragStart = false;
                _dragStartPoint = e.GetPosition(null); // Resetuj punkt startowy aby uniknąć stale distance
                return;
            }

            // Pochłoń kliknięcie które zamknęło popup (nie startuj draga)
            if (_skipNextDragStart)
            {
                _skipNextDragStart = false;
                _dragStartPoint = e.GetPosition(null); // Resetuj punkt startowy aby uniknąć stale distance
                return;
            }

            _dragStartPoint = e.GetPosition(null);
        }

        private void DgDostawy_ContextMenuOpened(object sender, RoutedEventArgs e)
        {
            _isContextMenuOpen = true;
        }

        private void DgDostawy_ContextMenuClosed(object sender, RoutedEventArgs e)
        {
            _isContextMenuOpen = false;
            _contextMenuClosedTime = DateTime.Now;
        }

        private void DgDostawy_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                _isDragging = false;
                return;
            }

            // Ignoruj jeśli menu kontekstowe jest otwarte lub było niedawno zamknięte
            if (_isContextMenuOpen || (DateTime.Now - _contextMenuClosedTime).TotalMilliseconds < CONTEXT_MENU_DRAG_BLOCK_MS)
                return;

            // Ignoruj jeśli inline edit popup jest otwarty lub niedawno zamknięty
            if (_inlineEditPopup != null && _inlineEditPopup.IsOpen)
                return;
            if (_skipNextDragStart)
                return;
            // Blokuj drag przez 500ms po zamknięciu inline edit popup
            if ((DateTime.Now - _inlineEditClosedTime).TotalMilliseconds < CONTEXT_MENU_DRAG_BLOCK_MS)
                return;

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
                try
                {
                    _highlightedDropTarget.BorderBrush = null;
                    _highlightedDropTarget.BorderThickness = new Thickness(0);
                    _highlightedDropTarget.Effect = null;
                }
                catch { /* row mógł zostać zrecyklingowany */ }
                _highlightedDropTarget = null;
            }
        }

        private async void DgDostawy_Drop(object sender, DragEventArgs e)
        {
            // Usuń podświetlenie
            ClearDropTargetHighlight();

            // Jeśli inline edit (np. Cena) właśnie się zamknął, kliknięcie zatwierdzające
            // nie powinno otwierać dialogu przenoszenia dostawy.
            if (_skipNextDragStart || (DateTime.Now - _inlineEditClosedTime).TotalMilliseconds < CONTEXT_MENU_DRAG_BLOCK_MS)
            {
                _isDragging = false;
                return;
            }

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

            // Potwierdzenie przez MessageBox
            var culture = new CultureInfo("pl-PL");
            string oldDateStr = droppedItem.DataOdbioru.ToString("dd.MM.yyyy (dddd)", culture);
            string newDateStr = newDate.ToString("dd.MM.yyyy (dddd)", culture);
            var result = MessageBox.Show(
                $"Przenieść dostawę?\n\n" +
                $"Dostawca: {droppedItem.Dostawca}\n" +
                $"Auta: {droppedItem.Auta}\n\n" +
                $"{oldDateStr}  →  {newDateStr}",
                "Przeniesienie dostawy",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                _isDragging = false;
                return;
            }

            // Przenieś dostawę do nowej daty (z audytem)
            await MoveDeliveryToDateAsync(droppedItem.LP, droppedItem.DataOdbioru.Date, newDate, droppedItem.Dostawca);
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

                ShowChangeNotification(new ChangeNotificationItem
                {
                    Title = "Przeniesiono dostawę",
                    Dostawca = dostawca,
                    LP = lp,
                    DataOdbioru = newDate,
                    UserId = UserID,
                    UserName = txtUserName?.Text,
                    NotificationType = ChangeNotificationType.DragDrop,
                    Changes = { new FieldChange
                    {
                        FieldName = "Data odbioru",
                        OldValue = oldDate.ToString("dd.MM.yyyy (dddd)", new CultureInfo("pl-PL")),
                        NewValue = newDate.ToString("dd.MM.yyyy (dddd)", new CultureInfo("pl-PL"))
                    }}
                });

                // Batch refresh: zmień datę in-memory i przebuduj widok (bez SQL)
                var (item, _) = FindDostawaByLP(lp);
                if (item != null)
                {
                    int shiftDays = (newDate.Date - oldDate.Date).Days;
                    item.DataOdbioru = newDate;
                    // Zaktualizuj wiek kurczaków (kolumna "dni") po przesunięciu dostawy
                    if (item.RoznicaDni.HasValue && shiftDays != 0)
                    {
                        item.RoznicaDni = item.RoznicaDni.Value + shiftDays;
                    }
                    RefreshDostawyView();
                }
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
                // Batch refresh: aktualizuj wszystkie zmienione wiersze in-memory
                ApplyLocalBulkUpdate(lpsToProcess, d =>
                {
                    d.Bufor = status;
                    d.IsConfirmed = confirm;
                });
                _selectedLPs.Clear();
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
                // Batch refresh: aktualizuj wszystkie zmienione wiersze in-memory
                ApplyLocalBulkUpdate(lpsToProcess, d =>
                {
                    d.Bufor = "Anulowany";
                    d.IsConfirmed = false;
                });
                _selectedLPs.Clear();
            }
            catch (Exception ex)
            {
                ShowToast($"Błąd: {ex.Message}", ToastType.Error);
            }
        }

        #endregion

        #region Helpers

        // ═══════════════════════════════════════════════════════════════
        // BATCH REFRESH - aktualizacje in-memory bez pełnego reload
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Znajduje dostawę po LP w obu tabelach i zwraca ją wraz z kolekcją w której się znajduje
        /// </summary>
        private (DostawaModel item, ObservableCollection<DostawaModel> collection) FindDostawaByLP(string lp)
        {
            var inCurrent = _dostawy.FirstOrDefault(d => d.LP == lp && !d.IsHeaderRow && !d.IsSeparator);
            if (inCurrent != null) return (inCurrent, _dostawy);
            var inNext = _dostawyNastepnyTydzien.FirstOrDefault(d => d.LP == lp && !d.IsHeaderRow && !d.IsSeparator);
            if (inNext != null) return (inNext, _dostawyNastepnyTydzien);
            return (null, null);
        }

        /// <summary>
        /// Aktualizuje wiersz dostawy in-memory bez pełnego reload z bazy.
        /// Wywołuje updateAction na modelu, przelicza header dnia, refresh row.
        /// </summary>
        private void ApplyLocalUpdate(string lp, Action<DostawaModel> updateAction)
        {
            var (item, collection) = FindDostawaByLP(lp);
            if (item == null) return;

            updateAction(item);
            RecalculateDayHeader(collection, item.DataOdbioru);
        }

        /// <summary>
        /// Aktualizuje wiele wierszy in-memory (dla bulk operations)
        /// </summary>
        private void ApplyLocalBulkUpdate(IEnumerable<string> lps, Action<DostawaModel> updateAction)
        {
            var affectedDates = new HashSet<(ObservableCollection<DostawaModel>, DateTime)>();
            foreach (var lp in lps)
            {
                var (item, collection) = FindDostawaByLP(lp);
                if (item == null) continue;
                updateAction(item);
                affectedDates.Add((collection, item.DataOdbioru.Date));
            }
            // Przelicz tylko te nagłówki dni, które zostały dotknięte
            foreach (var (col, date) in affectedDates)
                RecalculateDayHeader(col, date);
        }

        private string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";

            var parts = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                return $"{char.ToUpper(parts[0][0])}{char.ToUpper(parts[1][0])}";
            }
            else if (parts.Length == 1 && parts[0].Length >= 2)
            {
                return $"{char.ToUpper(parts[0][0])}{char.ToUpper(parts[0][1])}";
            }
            return parts.Length > 0 ? parts[0].Substring(0, 1).ToUpper() : "";
        }

        private void SetAvatar(Border avatarBorder, TextBlock avatarText, TextBlock nameText, string name, string userId = null, Ellipse imgEllipse = null, ImageBrush imgBrush = null)
        {
            nameText.Text = name ?? "";

            if (string.IsNullOrEmpty(name))
            {
                avatarBorder.Visibility = Visibility.Collapsed;
                if (imgEllipse != null) imgEllipse.Visibility = Visibility.Collapsed;
                return;
            }

            // Spróbuj załadować avatar z UserAvatarManager
            bool hasAvatar = false;
            if (!string.IsNullOrEmpty(userId) && imgEllipse != null && imgBrush != null)
            {
                try
                {
                    if (UserAvatarManager.HasAvatar(userId))
                    {
                        using (var avatar = UserAvatarManager.GetAvatarRounded(userId, 36))
                        {
                            if (avatar != null)
                            {
                                imgBrush.ImageSource = ConvertToImageSource(avatar);
                                imgEllipse.Visibility = Visibility.Visible;
                                avatarBorder.Visibility = Visibility.Collapsed;
                                hasAvatar = true;
                            }
                        }
                    }
                }
                catch { }
            }

            if (!hasAvatar)
            {
                // Fallback do inicjałów
                string initials = GetInitials(name);
                avatarText.Text = initials;
                avatarBorder.Visibility = Visibility.Visible;
                if (imgEllipse != null) imgEllipse.Visibility = Visibility.Collapsed;
            }
        }

        private System.Windows.Media.ImageSource ConvertToImageSource(System.Drawing.Image image)
        {
            using (var ms = new System.IO.MemoryStream())
            {
                image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;
                var bitmapImage = new System.Windows.Media.Imaging.BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = ms;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
                return bitmapImage;
            }
        }

        #endregion

        #region Toast Notifications

        private void ShowToast(string message, ToastType type = ToastType.Info)
        {
            // Dla sukcesów automatycznie dodaj avatar aktualnego użytkownika
            if (type == ToastType.Success)
            {
                ShowToastWithAvatar(message, type, UserID, UserName);
            }
            else
            {
                _toastQueue.Enqueue(new ToastMessage { Message = message, Type = type });
                if (!_isShowingToast)
                {
                    _ = ProcessToastQueueAsync();
                }
            }
        }

        private void ShowToastWithAvatar(string message, ToastType type, string userId, string userName)
        {
            _toastQueue.Enqueue(new ToastMessage { Message = message, Type = type, UserId = userId, UserName = userName });
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

                // Obsługa avatara
                if (!string.IsNullOrEmpty(toast.UserId) && toastAvatarGrid != null)
                {
                    toastAvatarGrid.Visibility = Visibility.Visible;

                    // Ustaw kolor inicjałów zgodny z kolorem toasta
                    var bgColor = (toastBorder.Background as SolidColorBrush)?.Color ?? Colors.Gray;
                    toastAvatarInitials.Foreground = new SolidColorBrush(bgColor);

                    // Ustaw inicjały
                    string initials = "";
                    if (!string.IsNullOrEmpty(toast.UserName))
                    {
                        var parts = toast.UserName.Split(' ');
                        if (parts.Length >= 2)
                            initials = $"{parts[0][0]}{parts[1][0]}".ToUpper();
                        else if (parts.Length == 1 && parts[0].Length >= 2)
                            initials = parts[0].Substring(0, 2).ToUpper();
                    }
                    toastAvatarInitials.Text = initials;
                    toastAvatarBorder.Visibility = Visibility.Visible;
                    toastAvatarImage.Visibility = Visibility.Collapsed;

                    // Spróbuj załadować prawdziwy avatar
                    try
                    {
                        if (UserAvatarManager.HasAvatar(toast.UserId))
                        {
                            using (var avatar = UserAvatarManager.GetAvatarRounded(toast.UserId, 56))
                            {
                                if (avatar != null)
                                {
                                    var brush = new ImageBrush(ConvertToImageSource(avatar));
                                    brush.Stretch = Stretch.UniformToFill;
                                    toastAvatarImage.Fill = brush;
                                    toastAvatarImage.Visibility = Visibility.Visible;
                                    toastAvatarBorder.Visibility = Visibility.Collapsed;
                                }
                            }
                        }
                    }
                    catch { }
                }
                else if (toastAvatarGrid != null)
                {
                    toastAvatarGrid.Visibility = Visibility.Collapsed;
                }

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

        #region Change Notifications

        private void ShowChangeNotification(ChangeNotificationItem item)
        {
            if (item == null || item.Changes == null || item.Changes.Count == 0) return;

            // Floating popup — widoczny ponad wszystkimi oknami (Topmost)
            // ShowNotification sam zadba o UI wątek
            ChangeNotificationPopup.ShowNotification(item);
        }

        private static string MapFieldName(string dbField)
        {
            return dbField switch
            {
                "DataOdbioru" => "Data odbioru",
                "Dostawca" => "Dostawca",
                "Auta" => "Auta",
                "SztukiDek" => "Sztuki",
                "WagaDek" => "Waga",
                "TypCeny" => "Typ ceny",
                "Cena" => "Cena",
                "Bufor" => "Status",
                "TypUmowy" => "Typ umowy",
                "Dodatek" => "Dodatek",
                "SztSzuflada" => "Szt/szuflada",
                "Uwagi" => "Uwagi",
                "PotwWaga" => "Potw. wagi",
                "PotwSztuki" => "Potw. sztuk",
                _ => dbField
            };
        }

        /// <summary>
        /// Kliknięcie w popup powiadomienia → aktywuj okno kalendarza i przeskocz do dostawy
        /// </summary>
        private void OnNotificationPopupClicked(string lp, DateTime? dataOdbioru)
        {
            if (string.IsNullOrEmpty(lp)) return;

            Dispatcher.InvokeAsync(async () =>
            {
                // Aktywuj okno kalendarza
                this.Activate();
                if (this.WindowState == WindowState.Minimized)
                    this.WindowState = WindowState.Normal;

                ClearUnreadBadge();

                // Spróbuj znaleźć dostawę w aktualnie załadowanych danych
                var (item, _) = FindDostawaByLP(lp);

                if (item != null)
                {
                    // Dostawa jest w widocznym tygodniu — zaznacz i scrolluj
                    var targetGrid = _dostawy.Contains(item) ? dgDostawy : dgDostawyNastepny;
                    targetGrid.SelectedItem = item;
                    targetGrid.ScrollIntoView(item);
                    _selectedLP = lp;
                }
                else
                {
                    // Dostawa jest w innym tygodniu — pobierz datę z bazy i zmień tydzień
                    DateTime? dostawaDate = dataOdbioru;

                    if (dostawaDate == null)
                    {
                        try
                        {
                            using (SqlConnection conn = new SqlConnection(ConnectionString))
                            {
                                await conn.OpenAsync(_cts.Token);
                                using (SqlCommand cmd = new SqlCommand(
                                    "SELECT DataOdbioru FROM HarmonogramDostaw WHERE LP = @lp", conn))
                                {
                                    cmd.Parameters.AddWithValue("@lp", lp);
                                    var result = await cmd.ExecuteScalarAsync(_cts.Token);
                                    if (result != null && result != DBNull.Value)
                                        dostawaDate = Convert.ToDateTime(result);
                                }
                            }
                        }
                        catch { }
                    }

                    if (dostawaDate.HasValue)
                    {
                        // Zmień tydzień na ten z dostawą
                        _selectedDate = dostawaDate.Value;
                        calendarMain.SelectedDate = _selectedDate;
                        calendarMain.DisplayDate = _selectedDate;
                        UpdateWeekNumber();
                        await LoadDostawyAsync();
                        GenerateWeekMap();

                        // Po załadowaniu — zaznacz dostawę
                        var (found, _) = FindDostawaByLP(lp);
                        if (found != null)
                        {
                            var targetGrid = _dostawy.Contains(found) ? dgDostawy : dgDostawyNastepny;
                            targetGrid.SelectedItem = found;
                            targetGrid.ScrollIntoView(found);
                            _selectedLP = lp;
                        }
                    }
                }

                ShowToast($"Przejście do LP: {lp}", ToastType.Info);
            });
        }

        /// <summary>
        /// Aktualizuje badge z liczbą nieprzeczytanych zmian od innych użytkowników
        /// </summary>
        private void UpdateUnreadBadge()
        {
            if (btnUnreadBadge == null) return;

            if (_unreadChangesCount > 0)
            {
                txtUnreadCount.Text = _unreadChangesCount > 99 ? "99+" : _unreadChangesCount.ToString();
                btnUnreadBadge.Visibility = Visibility.Visible;
            }
            else
            {
                btnUnreadBadge.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Czyści badge nieprzeczytanych zmian (przy aktywacji okna)
        /// </summary>
        private void ClearUnreadBadge()
        {
            _unreadChangesCount = 0;
            if (btnUnreadBadge != null)
                btnUnreadBadge.Visibility = Visibility.Collapsed;
        }

        private async void BtnUnreadBadge_Click(object sender, RoutedEventArgs e)
        {
            ClearUnreadBadge();
            await LoadDostawyAsync();
            ShowToast("Dane odświeżone", ToastType.Success);
        }

        #endregion

        #region Drag-Drop Confirmation Overlay

        private async Task<bool> ShowDragDropConfirmationAsync(DostawaModel item, DateTime newDate)
        {
            _dragDropTcs = new TaskCompletionSource<bool>();

            await Dispatcher.InvokeAsync(() =>
            {
                ddcDostawca.Text = item.Dostawca ?? "nieznany";
                ddcAuta.Text = item.Auta.ToString();
                ddcOldDate.Text = item.DataOdbioru.ToString("dd.MM.yyyy (dddd)", new CultureInfo("pl-PL"));
                ddcNewDate.Text = newDate.ToString("dd.MM.yyyy (dddd)", new CultureInfo("pl-PL"));

                dragDropConfirmOverlay.Visibility = Visibility.Visible;

                // Fade in
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
                dragDropConfirmOverlay.BeginAnimation(OpacityProperty, fadeIn);
            });

            return await _dragDropTcs.Task;
        }

        private void HideDragDropConfirm()
        {
            // Natychmiast zablokuj interakcję, potem animuj
            dragDropConfirmOverlay.IsHitTestVisible = false;
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
            fadeOut.Completed += (s, e) =>
            {
                dragDropConfirmOverlay.Visibility = Visibility.Collapsed;
                dragDropConfirmOverlay.IsHitTestVisible = true; // Przywróć dla następnego użycia
            };
            dragDropConfirmOverlay.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void BtnDdcConfirm_Click(object sender, RoutedEventArgs e)
        {
            HideDragDropConfirm();
            _dragDropTcs?.TrySetResult(true);
        }

        private void BtnDdcCancel_Click(object sender, RoutedEventArgs e)
        {
            HideDragDropConfirm();
            _dragDropTcs?.TrySetResult(false);
        }

        private void DdcBackdrop_MouseDown(object sender, MouseButtonEventArgs e)
        {
            HideDragDropConfirm();
            _dragDropTcs?.TrySetResult(false);
        }

        #endregion

        #region Preferencje użytkownika (filtry, layout, pozycja okna)

        /// <summary>
        /// Wczytuje preferencje z dysku i nakłada je na UI (checkboxy, szerokości kolumn, pozycję okna).
        /// Wywoływane raz w Window_Loaded zanim zaczniemy ładować dane.
        /// </summary>
        private void ApplyUserPreferences()
        {
            try
            {
                _userPrefs = UserPreferencesService.Load(UserID);
                _prefsLoaded = false; // Blokuje zapisywanie podczas nakładania ustawień

                // Filtry checkbox
                if (chkAnulowane != null) chkAnulowane.IsChecked = _userPrefs.ChkAnulowane;
                if (chkSprzedane != null) chkSprzedane.IsChecked = _userPrefs.ChkSprzedane;
                if (chkDoWykupienia != null) chkDoWykupienia.IsChecked = _userPrefs.ChkDoWykupienia;
                if (chkPokazCeny != null) chkPokazCeny.IsChecked = _userPrefs.ChkPokazCeny;
                if (chkPokazCheckboxy != null) chkPokazCheckboxy.IsChecked = _userPrefs.ChkPokazCheckboxy;
                if (chkNastepnyTydzien != null) chkNastepnyTydzien.IsChecked = _userPrefs.ChkNastepnyTydzien;

                // Widoczność kolumny ceny zgodna z preferencją
                var showCena = _userPrefs.ChkPokazCeny ? Visibility.Visible : Visibility.Collapsed;
                if (colCena != null) colCena.Visibility = showCena;
                if (colCenaNastepny != null) colCenaNastepny.Visibility = showCena;

                // Widoczność kolumn checkbox potwierdzenia
                var showCheck = _userPrefs.ChkPokazCheckboxy ? Visibility.Visible : Visibility.Collapsed;
                if (colCheckConfirm != null) colCheckConfirm.Visibility = showCheck;
                if (colCheckWstawienie != null) colCheckWstawienie.Visibility = showCheck;
                if (colCheckConfirm2 != null) colCheckConfirm2.Visibility = showCheck;

                // Szerokości kolumn (tylko numeryczne; gwiazdkowe pomijamy)
                if (_userPrefs.ColumnWidths != null)
                {
                    ApplyColumnWidth("colDostawcaHeader", colDostawcaHeader);
                    ApplyColumnWidth("colCena", colCena);
                    ApplyColumnWidth("colDostawcaHeader2", colDostawcaHeader2);
                    ApplyColumnWidth("colCenaNastepny", colCenaNastepny);
                }

                // Pozycja okna - tylko jeśli nie maximized i mamy ROZSĄDNY zapisany rozmiar.
                // Sanity check: ignoruj zapisaną pozycję jeśli wymiary są podejrzanie małe
                // (mogłoby się zdarzyć po awarii lub złym SW_RESTORE z WinAPI).
                if (!_userPrefs.WindowMaximized
                    && _userPrefs.WindowLeft >= 0 && _userPrefs.WindowTop >= 0
                    && _userPrefs.WindowWidth >= 800 && _userPrefs.WindowHeight >= 600)
                {
                    this.WindowState = WindowState.Normal;
                    this.WindowStartupLocation = WindowStartupLocation.Manual;
                    this.Left = _userPrefs.WindowLeft;
                    this.Top = _userPrefs.WindowTop;
                    this.Width = _userPrefs.WindowWidth;
                    this.Height = _userPrefs.WindowHeight;
                }
            }
            catch
            {
                // Tolerancyjne - jeśli cokolwiek pójdzie źle, zostaw defaultowe ustawienia
            }
            finally
            {
                _prefsLoaded = true;
            }
        }

        private void ApplyColumnWidth(string key, DataGridColumn column)
        {
            if (column == null) return;
            if (_userPrefs.ColumnWidths == null) return;
            if (!_userPrefs.ColumnWidths.TryGetValue(key, out double width)) return;
            if (width <= 10 || width > 2000) return; // sanity check
            try { column.Width = new DataGridLength(width); } catch { }
        }

        /// <summary>
        /// Pobiera obecny stan UI do obiektu preferencji (wywoływane przed zapisem).
        /// </summary>
        private void CaptureUserPreferences()
        {
            if (_userPrefs == null) _userPrefs = new KalendarzUserPreferences();

            // Filtry
            _userPrefs.ChkAnulowane = chkAnulowane?.IsChecked == true;
            _userPrefs.ChkSprzedane = chkSprzedane?.IsChecked == true;
            _userPrefs.ChkDoWykupienia = chkDoWykupienia?.IsChecked == true;
            _userPrefs.ChkPokazCeny = chkPokazCeny?.IsChecked == true;
            _userPrefs.ChkPokazCheckboxy = chkPokazCheckboxy?.IsChecked == true;
            _userPrefs.ChkNastepnyTydzien = chkNastepnyTydzien?.IsChecked == true;

            // Szerokości kolumn (tylko zdefiniowane numerycznie)
            if (_userPrefs.ColumnWidths == null)
                _userPrefs.ColumnWidths = new Dictionary<string, double>();

            CaptureColumnWidth("colDostawcaHeader", colDostawcaHeader);
            CaptureColumnWidth("colCena", colCena);
            CaptureColumnWidth("colDostawcaHeader2", colDostawcaHeader2);
            CaptureColumnWidth("colCenaNastepny", colCenaNastepny);

            // Pozycja okna - zapisujemy tylko sensowne wymiary (sanity check przeciw awariom WinAPI).
            _userPrefs.WindowMaximized = (this.WindowState == WindowState.Maximized);
            if (this.WindowState == WindowState.Normal && this.Width >= 800 && this.Height >= 600)
            {
                _userPrefs.WindowLeft = this.Left;
                _userPrefs.WindowTop = this.Top;
                _userPrefs.WindowWidth = this.Width;
                _userPrefs.WindowHeight = this.Height;
            }
        }

        private void CaptureColumnWidth(string key, DataGridColumn column)
        {
            if (column == null) return;
            try
            {
                if (column.ActualWidth > 10 && column.ActualWidth < 2000)
                    _userPrefs.ColumnWidths[key] = column.ActualWidth;
            }
            catch { }
        }

        /// <summary>
        /// Zapisuje preferencje na dysk. Używane przy zamykaniu okna i po zmianach checkboxów.
        /// </summary>
        private void SaveUserPreferences()
        {
            if (!_prefsLoaded) return; // nie zapisuj zanim ApplyUserPreferences nie skończy
            try
            {
                CaptureUserPreferences();
                UserPreferencesService.Save(UserID, _userPrefs);
            }
            catch { /* tolerancyjne */ }
        }

        #endregion

        #region Cleanup

        protected override void OnClosed(EventArgs e)
        {
            // Zapisz preferencje użytkownika (filtry, layout, pozycja okna)
            try { SaveUserPreferences(); } catch { }

            // Odsubskrybuj zdarzenia
            ChangeNotificationPopup.NotificationClicked -= OnNotificationPopupClicked;

            // Flush kolejki audytu - poczekaj max 5s żeby nie zginęły wpisy
            try
            {
                if (_auditService != null)
                {
                    _auditService.FlushAndWaitAsync().Wait(TimeSpan.FromSeconds(5));
                    _auditService.Dispose();
                }
            }
            catch { }

            // Anuluj wszystkie async operacje
            _cts?.Cancel();
            _cts?.Dispose();

            // Zatrzymaj timery
            _refreshTimer?.Stop();
            _priceTimer?.Stop();
            _surveyTimer?.Stop();
            _countdownTimer?.Stop();
            _liveWatchTimer?.Stop();
            _mentionsPollTimer?.Stop();

            base.OnClosed(e);
        }

        #endregion
    }
}
