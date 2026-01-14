using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Drawing.Printing;
using System.Diagnostics;
using Kalendarz.Zywiec.WidokSpecyfikacji;
using Kalendarz1.Zywiec.WidokSpecyfikacji;

// Aliasy dla rozwiązania konfliktu System.Drawing vs System.Windows.Media
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Brushes = System.Windows.Media.Brushes;
using FontFamily = System.Windows.Media.FontFamily;
using Point = System.Windows.Point;
using Image = System.Windows.Controls.Image;

namespace Kalendarz1
{
    public partial class WidokSpecyfikacje : Window
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private ZapytaniaSQL zapytaniasql = new ZapytaniaSQL();
        private ObservableCollection<SpecyfikacjaRow> specyfikacjeData;
        private SpecyfikacjaRow selectedRow;

        // Publiczne właściwości dla ComboBox binding
        public List<DostawcaItem> ListaDostawcow { get; set; }
        public List<string> ListaTypowCen { get; set; } = new List<string> { "wolnyrynek", "rolnicza", "łączona", "ministerialna" };

        // Lista pośredników dla ComboBox (na razie pusta - użytkownik dostarczy dane później)
        public ObservableCollection<PosrednikItem> ListaPosrednikow { get; set; } = new ObservableCollection<PosrednikItem>();

        // Dane dla karty Podsumowanie
        private ObservableCollection<PodsumowanieRow> podsumowanieData = new ObservableCollection<PodsumowanieRow>();

        // Backwards compatibility
        private List<DostawcaItem> listaDostawcow { get => ListaDostawcow; set => ListaDostawcow = value; }
        private List<string> listaTypowCen { get => ListaTypowCen; set => ListaTypowCen = value; }

        // Ustawienia PDF
        private static string defaultPdfPath = @"\\192.168.0.170\Public\Przel\";
        private static string defaultPlachtaPath = @"\\192.168.0.170\Public\Plachty\";
        private static bool useDefaultPath = true;
        private static bool _pdfCzarnoBialy = false; // Tryb czarno-biały PDF (logo kolorowe)
        private static bool _drukujTerminPlatnosci = false; // Czy drukować termin płatności na PDF
        private decimal sumaWartosc = 0;
        private decimal sumaKG = 0;

        // Arrow buttons for row movement (replaces drag & drop)

        // === WYDAJNOŚĆ: Cache dostawców (static - współdzielony między oknami) ===
        // Cache przechowuje dostawców przez 10 minut - drugie otwarcie okna = instant load
        private static List<DostawcaItem> _cachedDostawcy = null;
        private static DateTime _cacheTimestamp = DateTime.MinValue;
        private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(10);

        // === WYDAJNOŚĆ: Debounce dla auto-zapisu ===
        private DispatcherTimer _debounceTimer;
        private HashSet<int> _pendingSaveIds = new HashSet<int>();
        private const int DebounceDelayMs = 500;

        // === TIMER EDYCJI: Mierzy czas od wprowadzenia wartości do zapisu w bazie ===
        private Stopwatch _editStopwatch = new Stopwatch();
        private DispatcherTimer _editTimerDisplay;
        private DateTime? _editStartTime = null;
        private string _lastEditedField = "";
        private bool _isSavePending = false;
        private int _lastSaveTimeMs = 0;
        private static readonly HashSet<string> TrackedEditColumns = new HashSet<string>
        {
            "Szt.Dek", "SztukiDek", "Padłe", "Padle", "CH", "NW", "ZM",
            "LUMEL", "Szt.Wyb", "SztukiWybijak", "KG Wyb", "KilogramyWybijak"
        };

        // === UNDO: Stos zmian do cofnięcia ===
        private Stack<UndoAction> _undoStack = new Stack<UndoAction>();
        private const int MaxUndoHistory = 50;

        // === HISTORIA: Log zmian ===
        private List<ChangeLogEntry> _changeLog = new List<ChangeLogEntry>();
        private Dictionary<string, string> _oldFieldValues = new Dictionary<string, string>(); // Przechowuje stare wartości pól

        // === TRANSPORT: Dane transportowe ===
        private ObservableCollection<TransportRow> transportData;

        // === HARMONOGRAM: Dane harmonogramu dostaw (3 kolumny) ===
        private ObservableCollection<HarmonogramRow> harmonogramDataLeft;
        private ObservableCollection<HarmonogramRow> harmonogramDataCenter;
        private ObservableCollection<HarmonogramRow> harmonogramDataRight;

        // === SCHOWEK: Ctrl+Shift+C/V dla ustawień cenowych ===
        private SupplierClipboard _supplierClipboard = new SupplierClipboard();

        // === HIGHLIGHT: Aktualnie podświetlona grupa dostawcy ===
        private string _highlightedSupplier = null;

        // === GRUPOWANIE: Czy wiersze są pogrupowane według dostawcy ===
        private bool _isGroupingBySupplier = false;

        // === MINI KALENDARZ: Podgląd dni z danymi ===
        private ObservableCollection<CalendarDayItem> _calendarDays = new ObservableCollection<CalendarDayItem>();

        // === BLOKADA: Zapobiega logowaniu zmian podczas ładowania danych ===
        private bool _isLoadingData = false;

        // === AUTOCOMPLETE DOSTAWCY: TextBox + Popup + ListBox ===
        public ObservableCollection<DostawcaItem> SupplierSuggestions { get; set; } = new ObservableCollection<DostawcaItem>();
        private DispatcherTimer _supplierFilterTimer;
        private TextBox _currentSupplierTextBox;
        private const int SupplierFilterDelayMs = 200;

        public WidokSpecyfikacje()
        {
            InitializeComponent();

            // Przenieś kartę Rozliczenia na koniec (po Płachta)
            ReorderTabs();

            // Inicjalizuj timer debounce dla auto-zapisu
            _debounceTimer = new DispatcherTimer();
            _debounceTimer.Interval = TimeSpan.FromMilliseconds(DebounceDelayMs);
            _debounceTimer.Tick += DebounceTimer_Tick;

            // Inicjalizuj timer debounce dla autocomplete dostawcy
            _supplierFilterTimer = new DispatcherTimer();
            _supplierFilterTimer.Interval = TimeSpan.FromMilliseconds(SupplierFilterDelayMs);
            _supplierFilterTimer.Tick += SupplierFilterTimer_Tick;

            // Inicjalizuj timer wyświetlania czasu edycji
            _editTimerDisplay = new DispatcherTimer();
            _editTimerDisplay.Interval = TimeSpan.FromMilliseconds(50); // Aktualizacja co 50ms
            _editTimerDisplay.Tick += EditTimerDisplay_Tick;

            // WAŻNE: Załaduj listy PRZED ustawieniem DataContext
            // aby binding do ListaDostawcow i ListaTypowCen działał poprawnie
            LoadDostawcyFromCache();

            // Ustaw DataContext na this - teraz ListaDostawcow jest już wypełniona
            DataContext = this;

            specyfikacjeData = new ObservableCollection<SpecyfikacjaRow>();
            dataGridView1.ItemsSource = specyfikacjeData;

            // Inicjalizuj dane transportowe
            transportData = new ObservableCollection<TransportRow>();
            dataGridTransport.ItemsSource = transportData;

            // Harmonogram na 3 kolumny - maksymalne wykorzystanie szerokiego ekranu
            harmonogramDataLeft = new ObservableCollection<HarmonogramRow>();
            harmonogramDataCenter = new ObservableCollection<HarmonogramRow>();
            harmonogramDataRight = new ObservableCollection<HarmonogramRow>();

            dataGridHarmonogramLeft.ItemsSource = harmonogramDataLeft;
            dataGridHarmonogramCenter.ItemsSource = harmonogramDataCenter;
            dataGridHarmonogramRight.ItemsSource = harmonogramDataRight;

            dateTimePicker1.SelectedDate = DateTime.Today;

            // Dodaj obsługę skrótów klawiszowych
            this.KeyDown += Window_KeyDown;
        }

        /// <summary>
        /// Przesuwa kartę Rozliczenia na koniec (za Płachtę)
        /// Obecna kolejność: Specyfikacje (0), Transport (1), Rozliczenia (2), Płachta (3)
        /// Docelowa kolejność: Specyfikacje (0), Transport (1), Płachta (2), Rozliczenia (3)
        /// </summary>
        private void ReorderTabs()
        {
            try
            {
                if (mainTabControl.Items.Count >= 4)
                {
                    // Rozliczenia jest na pozycji 2, przenosimy na koniec
                    var rozliczeniaTab = mainTabControl.Items[2];
                    mainTabControl.Items.RemoveAt(2);
                    mainTabControl.Items.Add(rozliczeniaTab);
                }
            }
            catch { }
        }

        // === WYDAJNOŚĆ: Ładowanie dostawców z cache ===
        private void LoadDostawcyFromCache()
        {
            // Sprawdź czy cache jest aktualny
            if (_cachedDostawcy != null && DateTime.Now - _cacheTimestamp < CacheExpiration)
            {
                // Użyj cache
                ListaDostawcow = new List<DostawcaItem>(_cachedDostawcy);
                return;
            }

            // Ładuj z bazy i zapisz do cache
            LoadDostawcy();
            _cachedDostawcy = new List<DostawcaItem>(ListaDostawcow);
            _cacheTimestamp = DateTime.Now;
        }

        // === WYDAJNOŚĆ: Async ładowanie dostawców (do odświeżenia cache w tle) ===
        private async Task LoadDostawcyAsync()
        {
            var newList = new List<DostawcaItem>();
            newList.Add(new DostawcaItem { GID = null, ShortName = "(nie wybrano)" });

            try
            {
                await Task.Run(() =>
                {
                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        connection.Open();
                        string query = "SELECT ID AS GID, ShortName FROM dbo.Dostawcy WHERE halt = 0 ORDER BY ShortName";
                        using (SqlCommand cmd = new SqlCommand(query, connection))
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                newList.Add(new DostawcaItem
                                {
                                    GID = reader["GID"]?.ToString()?.Trim() ?? "",
                                    ShortName = reader["ShortName"]?.ToString() ?? ""
                                });
                            }
                        }
                    }
                });

                // Aktualizuj cache i listę
                _cachedDostawcy = newList;
                _cacheTimestamp = DateTime.Now;
                ListaDostawcow = new List<DostawcaItem>(newList);
            }
            catch (Exception ex)
            {
                UpdateStatus($"Błąd odświeżania dostawców: {ex.Message}");
            }
        }

        private void LoadDostawcy()
        {
            ListaDostawcow = new List<DostawcaItem>();
            // Dodaj pustą opcję na początku (jak w ImportAvilogWindow)
            ListaDostawcow.Add(new DostawcaItem { GID = null, ShortName = "(nie wybrano)" });

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT ID AS GID, ShortName FROM dbo.Dostawcy WHERE halt = 0 ORDER BY ShortName";
                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            ListaDostawcow.Add(new DostawcaItem
                            {
                                GID = reader["GID"]?.ToString()?.Trim() ?? "",
                                ShortName = reader["ShortName"]?.ToString() ?? ""
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania dostawców: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // === WYDAJNOŚĆ: Debounce Timer - zapisuje zmiany po 500ms nieaktywności ASYNCHRONICZNIE ===
        private async void DebounceTimer_Tick(object sender, EventArgs e)
        {
            _debounceTimer.Stop();

            if (_pendingSaveIds.Count > 0)
            {
                var idsToSave = _pendingSaveIds.ToList();
                _pendingSaveIds.Clear();

                // Zapisz wszystkie oczekujące zmiany w jednym batch - ASYNCHRONICZNIE w tle
                await SaveRowsBatchAsync(idsToSave);
            }
        }

        // === TIMER EDYCJI: Aktualizacja wyświetlania czasu od wprowadzenia wartości do zapisu ===
        private void EditTimerDisplay_Tick(object sender, EventArgs e)
        {
            if (_isSavePending && _editStartTime.HasValue)
            {
                // Trwa oczekiwanie na zapis - pokaż aktualny czas
                var elapsed = DateTime.Now - _editStartTime.Value;
                var ms = (int)elapsed.TotalMilliseconds;

                // Żółte tło gdy oczekuje na zapis
                lblEditTimer.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E65100"));
                borderEditTimer.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3E0"));
                lblEditTimer.Text = $"⏳ {ms} ms";
                lblEditField.Text = $"({_lastEditedField}) - zapisywanie...";
            }
            else if (!_isSavePending && _lastSaveTimeMs > 0)
            {
                // Zapis zakończony - pokaż ostatni czas zapisu
                // Kolorowanie: zielony < 500ms, pomarańczowy < 1000ms, czerwony > 1000ms
                if (_lastSaveTimeMs < 500)
                {
                    lblEditTimer.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"));
                    borderEditTimer.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F5E9"));
                }
                else if (_lastSaveTimeMs < 1000)
                {
                    lblEditTimer.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F57C00"));
                    borderEditTimer.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3E0"));
                }
                else
                {
                    lblEditTimer.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C62828"));
                    borderEditTimer.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFEBEE"));
                }

                lblEditTimer.Text = $"✓ {_lastSaveTimeMs} ms";
                lblEditField.Text = $"({_lastEditedField}) - zapisano";
            }
        }

        // Wywołaj po edycji pola produkcyjnego aby rozpocząć pomiar czasu
        private void MarkProductionFieldEdit(string columnName)
        {
            // Pokaż timer jeśli ukryty
            borderEditTimer.Visibility = Visibility.Visible;

            // Rozpocznij pomiar czasu (lub kontynuuj jeśli już trwa)
            if (!_isSavePending)
            {
                _editStartTime = DateTime.Now;
                _editStopwatch.Restart();
            }

            _lastEditedField = columnName;
            _isSavePending = true;
            _editTimerDisplay.Start();
        }

        // Wywołaj po zapisaniu w bazie danych aby zakończyć pomiar
        private void MarkSaveCompleted()
        {
            if (_editStartTime.HasValue)
            {
                _lastSaveTimeMs = (int)(DateTime.Now - _editStartTime.Value).TotalMilliseconds;
            }
            _isSavePending = false;
            _editStartTime = null;
            _editStopwatch.Stop();

            // Ostatnia aktualizacja UI
            EditTimerDisplay_Tick(null, null);

            // Zatrzymaj timer po 3 sekundach (aby użytkownik zobaczył wynik)
            var hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            hideTimer.Tick += (s, e) =>
            {
                hideTimer.Stop();
                _editTimerDisplay.Stop();
            };
            hideTimer.Start();
        }

        // === WYDAJNOŚĆ: Dodaj wiersz do kolejki zapisu (debounce) ===
        private void QueueRowForSave(int rowId)
        {
            _pendingSaveIds.Add(rowId);
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Upewnij się że kolumna Symfonia istnieje w bazie
            EnsureSymfoniaColumnExists();

            // Upewnij się że kolumna IdPosrednik istnieje w bazie
            EnsureIdPosrednikColumnExists();

            // Upewnij się że kolumny dla zdjęć z ważenia istnieją w bazie
            EnsurePhotoColumnsExist();

            // Wczytaj ustawienia administracyjne
            LoadAdminSettings();

            LoadData(dateTimePicker1.SelectedDate ?? DateTime.Today);
            UpdateFullDateLabel();
            UpdateTransportDateLabel();
            UpdateStatus("Dane załadowane pomyślnie");

            // Odśwież cache dostawców w tle (async)
            _ = LoadDostawcyAsync();

            // Inicjalizuj mini kalendarz
            InitializeMiniCalendar();
        }

        #region Mini Kalendarz

        /// <summary>
        /// Inicjalizuje mini kalendarz z 7 dniami (3 dni wstecz, dzisiaj, 3 dni wprzód)
        /// </summary>
        private void InitializeMiniCalendar()
        {
            if (miniCalendarDays != null)
            {
                miniCalendarDays.ItemsSource = _calendarDays;
                RefreshMiniCalendar();
            }
        }

        // Przechowuje aktualny tydzień dla mini kalendarza
        private DateTime _currentWeekStart;

        /// <summary>
        /// Odświeża mini kalendarz dla aktualnie wybranej daty (Pon-Pt)
        /// </summary>
        private async void RefreshMiniCalendar()
        {
            var selectedDate = dateTimePicker1.SelectedDate ?? DateTime.Today;
            _calendarDays.Clear();

            // Znajdź poniedziałek tygodnia zawierającego wybraną datę
            int daysFromMonday = ((int)selectedDate.DayOfWeek - 1 + 7) % 7;
            _currentWeekStart = selectedDate.AddDays(-daysFromMonday);

            // Generuj 5 dni (Pon-Pt)
            for (int i = 0; i < 5; i++)
            {
                var date = _currentWeekStart.AddDays(i);
                _calendarDays.Add(new CalendarDayItem
                {
                    Date = date,
                    IsSelected = (date.Date == selectedDate.Date),
                    HasData = false
                });
            }

            // Sprawdź które dni mają dane (async)
            await CheckDaysWithData();
        }

        /// <summary>
        /// Sprawdza które dni mają dane w bazie (async)
        /// </summary>
        private async Task CheckDaysWithData()
        {
            try
            {
                var dates = _calendarDays.Select(d => d.Date).ToList();
                var minDate = dates.Min();
                var maxDate = dates.Max();

                await Task.Run(() =>
                {
                    using (var conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        string query = @"
                            SELECT CAST(CalcDate AS DATE) as DataDnia, COUNT(*) as Ilosc
                            FROM [LibraNet].[dbo].[FarmerCalc]
                            WHERE CalcDate >= @MinDate AND CalcDate <= @MaxDate
                            GROUP BY CAST(CalcDate AS DATE)";

                        using (var cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@MinDate", minDate);
                            cmd.Parameters.AddWithValue("@MaxDate", maxDate);

                            using (var reader = cmd.ExecuteReader())
                            {
                                var results = new Dictionary<DateTime, int>();
                                while (reader.Read())
                                {
                                    var date = reader.GetDateTime(0);
                                    var count = reader.GetInt32(1);
                                    results[date] = count;
                                }

                                // Aktualizuj UI w głównym wątku
                                Dispatcher.Invoke(() =>
                                {
                                    foreach (var day in _calendarDays)
                                    {
                                        if (results.TryGetValue(day.Date.Date, out int count))
                                        {
                                            day.HasData = count > 0;
                                            day.RecordCount = count;
                                        }
                                        else
                                        {
                                            day.HasData = false;
                                            day.RecordCount = 0;
                                        }
                                    }
                                });
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Błąd sprawdzania dni z danymi: {ex.Message}");
            }
        }

        /// <summary>
        /// Handler kliknięcia na dzień w mini kalendarzu
        /// </summary>
        private void MiniCalendarDay_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is CalendarDayItem dayItem)
            {
                // Zmień wybraną datę
                dateTimePicker1.SelectedDate = dayItem.Date;
            }
        }

        /// <summary>
        /// Przejdź do poprzedniego tygodnia
        /// </summary>
        private void BtnPrevWeek_Click(object sender, RoutedEventArgs e)
        {
            // Przesuń o tydzień wstecz
            var newDate = _currentWeekStart.AddDays(-7);
            dateTimePicker1.SelectedDate = newDate;
        }

        /// <summary>
        /// Przejdź do następnego tygodnia
        /// </summary>
        private void BtnNextWeek_Click(object sender, RoutedEventArgs e)
        {
            // Przesuń o tydzień wprzód
            var newDate = _currentWeekStart.AddDays(7);
            dateTimePicker1.SelectedDate = newDate;
        }

        /// <summary>
        /// Przejdź do dzisiejszej daty
        /// </summary>
        private void BtnToday_Click(object sender, RoutedEventArgs e)
        {
            dateTimePicker1.SelectedDate = DateTime.Today;
        }

        #endregion

        // === TRANSPORT: Handlery dla karty Transport ===
        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl)
            {
                // Ukryj panel LUMEL gdy nie jesteśmy na karcie Specyfikacje
                if (mainTabControl.SelectedIndex == 0)
                {
                    // Karta Specyfikacje - LUMEL panel może być widoczny
                }
                else
                {
                    // Inna karta - ukryj LUMEL panel
                    lumelPanel.Visibility = Visibility.Collapsed;
                }

                // Załaduj dane płachty gdy przełączono na kartę Płachta (teraz index 2 po ReorderTabs)
                if (mainTabControl.SelectedIndex == 2)
                {
                    LoadPlachtaData();
                }

                // Załaduj dane rozliczeń gdy przełączono na kartę Rozliczenia (teraz index 3 po ReorderTabs)
                if (mainTabControl.SelectedIndex == 3)
                {
                    LoadRozliczeniaData();
                }

                // Załaduj dane podsumowania gdy przełączono na kartę Podsumowanie (index 4)
                if (mainTabControl.SelectedIndex == 4)
                {
                    LoadPodsumowanieData();
                }
            }
        }

        private void UpdateTransportDateLabel()
        {
            if (dateTimePicker1.SelectedDate.HasValue)
            {
                lblTransportDate.Text = dateTimePicker1.SelectedDate.Value.ToString("dd.MM.yyyy (dddd)", new System.Globalization.CultureInfo("pl-PL"));
            }
        }

        private void BtnAddTransport_Click(object sender, RoutedEventArgs e)
        {
            // Dodaj nowy wiersz transportu
            var newTransport = new TransportRow
            {
                Nr = transportData.Count + 1,
                Status = "Oczekuje",
                GodzinaWyjazdu = DateTime.Today.AddHours(6), // Domyślnie 6:00
            };
            transportData.Add(newTransport);
            dataGridTransport.SelectedItem = newTransport;
            dataGridTransport.ScrollIntoView(newTransport);
        }

        /// <summary>
        /// Diagnostyka transportu - sprawdza problemy i pokazuje szczegółowy raport
        /// </summary>
        private void BtnTransportDiagnostyka_Click(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════════");
            sb.AppendLine("       DIAGNOSTYKA TRANSPORTU - RAPORT PROBLEMÓW");
            sb.AppendLine("═══════════════════════════════════════════════════════");
            sb.AppendLine();

            DateTime selectedDate = dateTimePicker1.SelectedDate ?? DateTime.Today;
            sb.AppendLine($"Data: {selectedDate:dd.MM.yyyy} ({selectedDate.ToString("dddd", new System.Globalization.CultureInfo("pl-PL"))})");
            sb.AppendLine();

            int problemCount = 0;
            var problemy = new List<string>();

            // 1. Sprawdź spójność między specyfikacjami a transportem
            sb.AppendLine(">>> SPÓJNOŚĆ SPECYFIKACJI I TRANSPORTU:");
            int specCount = specyfikacjeData?.Count ?? 0;
            int transportCount = transportData?.Count ?? 0;

            if (specCount != transportCount)
            {
                problemy.Add($"[!] Liczba specyfikacji ({specCount}) != liczba transportów ({transportCount})");
                problemCount++;
            }
            else
            {
                sb.AppendLine($"   [OK] Liczba rekordów zgodna: {specCount} specyfikacji = {transportCount} transportów");
            }
            sb.AppendLine();

            // 2. Sprawdź braki w danych transportowych
            sb.AppendLine(">>> BRAKI W DANYCH TRANSPORTOWYCH:");
            if (transportData != null)
            {
                var brakSamochodu = transportData.Where(t => string.IsNullOrEmpty(t.Samochod)).ToList();
                var brakRejestracji = transportData.Where(t => string.IsNullOrEmpty(t.NrRejestracyjny)).ToList();
                var brakKierowcy = transportData.Where(t => string.IsNullOrEmpty(t.Kierowca)).ToList();

                if (brakSamochodu.Any())
                {
                    problemy.Add($"[!] Brak samochodu: LP {string.Join(", ", brakSamochodu.Select(t => t.Nr))}");
                    problemCount++;
                }
                if (brakRejestracji.Any())
                {
                    problemy.Add($"[!] Brak nr rejestracyjnego: LP {string.Join(", ", brakRejestracji.Select(t => t.Nr))}");
                    problemCount++;
                }
                if (brakKierowcy.Any())
                {
                    problemy.Add($"[!] Brak kierowcy: LP {string.Join(", ", brakKierowcy.Select(t => t.Nr))}");
                    problemCount++;
                }

                if (!brakSamochodu.Any() && !brakRejestracji.Any() && !brakKierowcy.Any())
                {
                    sb.AppendLine("   [OK] Wszystkie transporty mają kompletne dane");
                }
            }
            sb.AppendLine();

            // 3. Sprawdź braki w specyfikacjach
            sb.AppendLine(">>> BRAKI W SPECYFIKACJACH:");
            if (specyfikacjeData != null)
            {
                var brakDostawcy = specyfikacjeData.Where(s => string.IsNullOrEmpty(s.Dostawca) || s.Dostawca == "Nieznany").ToList();
                var brakSztuk = specyfikacjeData.Where(s => s.SztukiDek == 0).ToList();
                var brakCeny = specyfikacjeData.Where(s => s.Cena == 0).ToList();

                if (brakDostawcy.Any())
                {
                    problemy.Add($"[!] Brak dostawcy: LP {string.Join(", ", brakDostawcy.Select(s => s.Nr))}");
                    problemCount++;
                }
                if (brakSztuk.Any())
                {
                    problemy.Add($"[!] Brak deklaracji sztuk: LP {string.Join(", ", brakSztuk.Select(s => s.Nr))}");
                    problemCount++;
                }
                if (brakCeny.Any())
                {
                    problemy.Add($"[!] Brak ceny: LP {string.Join(", ", brakCeny.Select(s => s.Nr))}");
                    problemCount++;
                }

                if (!brakDostawcy.Any() && !brakSztuk.Any() && !brakCeny.Any())
                {
                    sb.AppendLine("   [OK] Wszystkie specyfikacje mają kompletne dane podstawowe");
                }
            }
            sb.AppendLine();

            // 4. Sprawdź anomalie - duże odchylenia
            sb.AppendLine(">>> ANOMALIE (duże odchylenia):");
            if (specyfikacjeData != null && specyfikacjeData.Count > 0)
            {
                var avgSztuki = specyfikacjeData.Average(s => s.SztukiDek);
                var anomalieSztuki = specyfikacjeData.Where(s => s.SztukiDek > 0 && (s.SztukiDek > avgSztuki * 3 || s.SztukiDek < avgSztuki * 0.3)).ToList();

                if (anomalieSztuki.Any())
                {
                    foreach (var anom in anomalieSztuki)
                    {
                        problemy.Add($"[!] LP {anom.Nr}: Nietypowa liczba sztuk ({anom.SztukiDek}) - średnia: {avgSztuki:N0}");
                        problemCount++;
                    }
                }
                else
                {
                    sb.AppendLine("   [OK] Brak znaczących anomalii w ilościach");
                }

                // Sprawdź duplikaty dostawców
                var duplikaty = specyfikacjeData.GroupBy(s => s.Dostawca)
                    .Where(g => g.Count() > 1 && !string.IsNullOrEmpty(g.Key) && g.Key != "Nieznany")
                    .ToList();

                if (duplikaty.Any())
                {
                    foreach (var dup in duplikaty)
                    {
                        sb.AppendLine($"   [i] Dostawca '{dup.Key}' pojawia się {dup.Count()} razy (LP: {string.Join(", ", dup.Select(s => s.Nr))})");
                    }
                }
            }
            sb.AppendLine();

            // 5. Podsumowanie
            sb.AppendLine("═══════════════════════════════════════════════════════");
            sb.AppendLine($"PODSUMOWANIE: Znaleziono {problemCount} problemów");
            sb.AppendLine("═══════════════════════════════════════════════════════");
            sb.AppendLine();

            if (problemy.Any())
            {
                sb.AppendLine("LISTA PROBLEMÓW:");
                foreach (var problem in problemy)
                {
                    sb.AppendLine($"   {problem}");
                }
            }
            else
            {
                sb.AppendLine("Brak wykrytych problemów - wszystko wygląda poprawnie!");
            }

            // Pokaż okno dialogowe z raportem
            var diagWindow = new Window
            {
                Title = $"Diagnostyka Transportu - {selectedDate:dd.MM.yyyy}",
                Width = 700,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245))
            };

            var scrollViewer = new ScrollViewer { Margin = new Thickness(15) };
            var textBlock = new TextBlock
            {
                Text = sb.ToString(),
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            };
            scrollViewer.Content = textBlock;

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid.SetRow(scrollViewer, 0);
            grid.Children.Add(scrollViewer);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(15, 5, 15, 15)
            };

            var copyButton = new Button
            {
                Content = "Kopiuj do schowka",
                Padding = new Thickness(15, 8, 15, 8),
                Margin = new Thickness(0, 0, 10, 0),
                Background = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            copyButton.Click += (s, args) =>
            {
                Clipboard.SetText(sb.ToString());
                MessageBox.Show("Raport skopiowany do schowka!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            };

            var closeButton = new Button
            {
                Content = "Zamknij",
                Padding = new Thickness(20, 8, 20, 8),
                Background = new SolidColorBrush(Color.FromRgb(149, 165, 166)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            closeButton.Click += (s, args) => diagWindow.Close();

            buttonPanel.Children.Add(copyButton);
            buttonPanel.Children.Add(closeButton);

            Grid.SetRow(buttonPanel, 1);
            grid.Children.Add(buttonPanel);

            diagWindow.Content = grid;
            diagWindow.ShowDialog();
        }

        private void BtnRefreshTransport_Click(object sender, RoutedEventArgs e)
        {
            LoadTransportData();
            UpdateStatus("Dane transportowe odświeżone");
        }

        /// <summary>
        /// Odswieza wszystkie dane - przycisk obok panelu tygodni
        /// </summary>
        private void BtnRefreshAll_Click(object sender, RoutedEventArgs e)
        {
            LoadData(dateTimePicker1.SelectedDate ?? DateTime.Today);
            UpdateStatus("Wszystkie dane odswiezone");
        }

        private void BtnTransportMoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridTransport.SelectedItem is TransportRow selectedTransportRow)
            {
                int index = transportData.IndexOf(selectedTransportRow);
                if (index > 0)
                {
                    // Przesuń w Transport
                    transportData.Move(index, index - 1);
                    UpdateTransportNrNumbers();
                    dataGridTransport.SelectedItem = selectedTransportRow;

                    // Synchronizuj z specyfikacjeData
                    SyncSpecyfikacjeOrderFromTransport();
                    // Odśwież Płachtę
                    RefreshPlachtaFromSpecyfikacje();
                }
            }
        }

        private void BtnTransportMoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridTransport.SelectedItem is TransportRow selectedTransportRow)
            {
                int index = transportData.IndexOf(selectedTransportRow);
                if (index < transportData.Count - 1)
                {
                    // Przesuń w Transport
                    transportData.Move(index, index + 1);
                    UpdateTransportNrNumbers();
                    dataGridTransport.SelectedItem = selectedTransportRow;

                    // Synchronizuj z specyfikacjeData
                    SyncSpecyfikacjeOrderFromTransport();
                    // Odśwież Płachtę
                    RefreshPlachtaFromSpecyfikacje();
                }
            }
        }

        private void UpdateTransportNrNumbers()
        {
            for (int i = 0; i < transportData.Count; i++)
            {
                transportData[i].Nr = i + 1;
            }
        }

        /// <summary>
        /// Synchronizuje kolejność specyfikacjeData na podstawie kolejności transportData
        /// </summary>
        private void SyncSpecyfikacjeOrderFromTransport()
        {
            if (specyfikacjeData == null || transportData == null)
                return;

            // Stwórz mapę ID -> pozycja z transportData
            var newOrder = transportData.Select((t, idx) => new { t.SpecyfikacjaID, Index = idx }).ToList();

            // Posortuj specyfikacjeData według nowej kolejności
            var orderedSpecs = specyfikacjeData
                .OrderBy(s => newOrder.FirstOrDefault(o => o.SpecyfikacjaID == s.ID)?.Index ?? int.MaxValue)
                .ToList();

            // Zastąp kolekcję
            specyfikacjeData.Clear();
            foreach (var spec in orderedSpecs)
            {
                specyfikacjeData.Add(spec);
            }
            UpdateRowNumbers();
        }

        /// <summary>
        /// Odświeża Płachtę na podstawie aktualnej kolejności specyfikacjeData (bez ponownego ładowania z bazy)
        /// </summary>
        private void RefreshPlachtaFromSpecyfikacje()
        {
            if (plachtaData == null || specyfikacjeData == null)
                return;

            // Stwórz mapę ID -> pozycja z specyfikacjeData
            var specOrder = specyfikacjeData.Select((s, idx) => new { s.ID, Index = idx }).ToDictionary(x => x.ID, x => x.Index);

            // Posortuj plachtaData według kolejności specyfikacjeData
            var orderedPlachta = plachtaData
                .OrderBy(p => specOrder.ContainsKey(p.ID) ? specOrder[p.ID] : int.MaxValue)
                .ToList();

            plachtaData.Clear();
            foreach (var row in orderedPlachta)
            {
                plachtaData.Add(row);
            }
            UpdatePlachtaLpNumbers();
        }

        private void BtnTransportSaveOrder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (specyfikacjeData == null || specyfikacjeData.Count == 0) return;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Zapisz nową kolejność (CarLp) dla każdego wiersza na podstawie kolejności w transportData
                    for (int i = 0; i < transportData.Count; i++)
                    {
                        var transport = transportData[i];
                        // Znajdź odpowiednią specyfikację na podstawie danych transportu
                        var spec = specyfikacjeData.FirstOrDefault(s =>
                            s.CarID == transport.Samochod &&
                            s.KierowcaNazwa == transport.Kierowca &&
                            (s.Dostawca == transport.Trasa || s.RealDostawca == transport.Trasa));

                        if (spec != null)
                        {
                            string updateQuery = "UPDATE dbo.FarmerCalc SET CarLp = @CarLp WHERE ID = @ID";
                            using (SqlCommand cmd = new SqlCommand(updateQuery, conn))
                            {
                                cmd.Parameters.AddWithValue("@CarLp", i + 1);
                                cmd.Parameters.AddWithValue("@ID", spec.ID);
                                cmd.ExecuteNonQuery();
                            }
                        }
                    }
                }

                // Odśwież dane
                DateTime selectedDate = dateTimePicker1.SelectedDate ?? DateTime.Today;
                LoadData(selectedDate);
                LoadTransportData();
                MessageBox.Show("Kolejność transportu została zapisana.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadTransportData()
        {
            transportData.Clear();

            if (specyfikacjeData == null || specyfikacjeData.Count == 0)
                return;

            // Wyświetl każdą specyfikację jako wiersz transportowy z danymi z FarmerCalc + Driver
            int nr = 1;
            foreach (var spec in specyfikacjeData)
            {
                var transportRow = new TransportRow
                {
                    SpecyfikacjaID = spec.ID,
                    Nr = nr++,
                    Dostawca = spec.Dostawca ?? spec.RealDostawca ?? "",
                    Kierowca = spec.KierowcaNazwa ?? "",
                    Samochod = spec.CarID ?? "",
                    NrRejestracyjny = spec.TrailerID ?? "",
                    GodzinaWyjazdu = spec.Wyjazd,
                    GodzinaPrzyjazdu = spec.ArrivalTime,
                    Sztuki = spec.SztukiDek,
                    Kilogramy = spec.WagaNettoDoRozliczenia,
                    // Skrzynki i pojemniki
                    Skrzynki = spec.LUMEL,  // Ilość skrzynek z FarmerCalc
                    SztPoj = spec.Padle,    // Sztuki pojemników (Padłe = Padle)
                    // Godziny transportowe
                    PoczatekUslugi = spec.PoczatekUslugi,
                    DojazdHodowca = spec.DojazdHodowca,
                    Zaladunek = spec.Zaladunek,
                    ZaladunekKoniec = spec.ZaladunekKoniec,
                    WyjazdHodowca = spec.WyjazdHodowca,
                    KoniecUslugi = spec.KoniecUslugi,
                    // Wagi i ubytek
                    NettoHodowcy = spec.NettoHodowcyValue,
                    NettoUbojni = spec.NettoUbojniValue,
                    UbytekUmowny = spec.Ubytek,
                    // Cena i dopłata (do obliczenia Zysk/Strata)
                    Cena = spec.Cena,
                    Doplata = spec.Dodatek
                };

                transportData.Add(transportRow);
            }

            // Oblicz szacowany czas dojazdu dla każdego transportu
            CalculateEstimatedArrivalTimes();

            // Oblicz średnie czasy dla każdego dostawcy (do wykrywania anomalii)
            CalculateAverageTimesPerDostawca();
        }

        /// <summary>
        /// Oblicza średnie czasy dojazdu i powrotu dla każdego dostawcy na podstawie dzisiejszych danych
        /// </summary>
        private void CalculateAverageTimesPerDostawca()
        {
            if (transportData == null || transportData.Count == 0) return;

            // Grupuj po dostawcy i oblicz średnie
            var dostawcyGroup = transportData
                .Where(t => !string.IsNullOrEmpty(t.Dostawca))
                .GroupBy(t => t.Dostawca)
                .ToList();

            foreach (var group in dostawcyGroup)
            {
                // Średni czas dojazdu dla dostawcy
                var czasyDojazdu = group
                    .Where(t => t.CzasDojMin.HasValue && t.CzasDojMin > 0)
                    .Select(t => t.CzasDojMin.Value)
                    .ToList();

                int avgDoj = czasyDojazdu.Count > 0 ? (int)czasyDojazdu.Average() : 0;

                // Średni czas powrotu dla dostawcy
                var czasyPowrotu = group
                    .Where(t => t.CzasPowrotMin.HasValue && t.CzasPowrotMin > 0)
                    .Select(t => t.CzasPowrotMin.Value)
                    .ToList();

                int avgPowrot = czasyPowrotu.Count > 0 ? (int)czasyPowrotu.Average() : 0;

                // Ustaw średnie dla wszystkich wierszy tego dostawcy
                foreach (var transport in group)
                {
                    transport.SredniaCzasDojMin = avgDoj;
                    transport.SredniaCzasPowrotMin = avgPowrot;
                }
            }
        }

        /// <summary>
        /// Oblicza szacowany czas dojazdu na podstawie historycznych danych
        /// </summary>
        private void CalculateEstimatedArrivalTimes()
        {
            if (transportData == null || transportData.Count == 0) return;

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    foreach (var transport in transportData)
                    {
                        if (string.IsNullOrEmpty(transport.Dostawca)) continue;

                        // Pobierz średni czas dojazdu do tego dostawcy z ostatnich 30 dni
                        string query = @"SELECT
                            AVG(DATEDIFF(MINUTE, Wyjazd, DojazdHodowca)) as AvgMinutes,
                            COUNT(*) as Cnt
                            FROM [LibraNet].[dbo].[FarmerCalc] fc
                            INNER JOIN [LibraNet].[dbo].[Customer] c ON fc.CustomerRealGID = c.GID
                            WHERE c.ShortName = @Dostawca
                            AND fc.Wyjazd IS NOT NULL
                            AND fc.DojazdHodowca IS NOT NULL
                            AND fc.CalcDate >= DATEADD(DAY, -30, GETDATE())
                            AND DATEDIFF(MINUTE, fc.Wyjazd, fc.DojazdHodowca) > 0
                            AND DATEDIFF(MINUTE, fc.Wyjazd, fc.DojazdHodowca) < 300"; // Max 5 godzin

                        using (SqlCommand cmd = new SqlCommand(query, connection))
                        {
                            cmd.Parameters.AddWithValue("@Dostawca", transport.Dostawca);

                            using (var reader = cmd.ExecuteReader())
                            {
                                if (reader.Read() && reader["AvgMinutes"] != DBNull.Value)
                                {
                                    int avgMinutes = Convert.ToInt32(reader["AvgMinutes"]);
                                    int count = Convert.ToInt32(reader["Cnt"]);

                                    if (avgMinutes > 0 && count >= 2) // Minimum 2 historyczne rekordy
                                    {
                                        transport.SredniaMinutDojazdu = avgMinutes;
                                        int hours = avgMinutes / 60;
                                        int mins = avgMinutes % 60;
                                        transport.SzacowanyCzasDojazdu = hours > 0
                                            ? $"~{hours}h {mins}m ({count})"
                                            : $"~{mins}m ({count})";
                                    }
                                    else
                                    {
                                        transport.SzacowanyCzasDojazdu = "-";
                                    }
                                }
                                else
                                {
                                    transport.SzacowanyCzasDojazdu = "-";
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // W przypadku błędu - ustaw domyślne wartości
                foreach (var transport in transportData)
                {
                    transport.SzacowanyCzasDojazdu = "?";
                }
                System.Diagnostics.Debug.WriteLine($"Błąd obliczania szacowanego czasu: {ex.Message}");
            }
        }

        private void LoadHarmonogramData()
        {
            // Czyścimy wszystkie 3 listy
            harmonogramDataLeft.Clear();
            harmonogramDataCenter.Clear();
            harmonogramDataRight.Clear();

            if (!dateTimePicker1.SelectedDate.HasValue)
                return;

            DateTime selectedDate = dateTimePicker1.SelectedDate.Value.Date;

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    string query = @"SELECT
                                LP,
                                Dostawca,
                                SztukiDek,
                                WagaDek,
                                Cena,
                                Dodatek,
                                TypCeny,
                                Auta,
                                UWAGI
                            FROM [LibraNet].[dbo].[HarmonogramDostaw]
                            WHERE DataOdbioru = @SelectedDate
                            AND Bufor IN ('Potwierdzony', 'Potwierdzone')
                            ORDER BY LP";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@SelectedDate", selectedDate);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            // Lista tymczasowa na wszystkie pobrane wiersze
                            var allRows = new List<HarmonogramRow>();

                            while (reader.Read())
                            {
                                int sztuki = reader["SztukiDek"] != DBNull.Value ? Convert.ToInt32(reader["SztukiDek"]) : 0;
                                decimal waga = reader["WagaDek"] != DBNull.Value ? Convert.ToDecimal(reader["WagaDek"]) : 0;
                                decimal cena = reader["Cena"] != DBNull.Value ? Convert.ToDecimal(reader["Cena"]) : 0;
                                decimal razemKg = sztuki * waga;

                                var row = new HarmonogramRow
                                {
                                    LP = reader["LP"] != DBNull.Value ? Convert.ToInt32(reader["LP"]) : 0,
                                    Dostawca = reader["Dostawca"]?.ToString()?.Trim() ?? "",
                                    SztukiDek = sztuki,
                                    WagaDek = waga,
                                    RazemKg = razemKg,
                                    Cena = cena,
                                    Dodatek = reader["Dodatek"] != DBNull.Value ? Convert.ToDecimal(reader["Dodatek"]) : 0,
                                    TypCeny = reader["TypCeny"]?.ToString()?.Trim() ?? "",
                                    Auta = reader["Auta"] != DBNull.Value ? Convert.ToInt32(reader["Auta"]) : 0,
                                    Uwagi = reader["UWAGI"]?.ToString()?.Trim() ?? ""
                                };

                                allRows.Add(row);
                            }

                            // === LOGIKA PODZIAŁU NA 3 TABELE (lepsze wykorzystanie szerokiego ekranu) ===
                            int partSize = (int)Math.Ceiling(allRows.Count / 3.0);
                            int secondPartStart = partSize;
                            int thirdPartStart = partSize * 2;

                            for (int i = 0; i < allRows.Count; i++)
                            {
                                if (i < secondPartStart)
                                {
                                    harmonogramDataLeft.Add(allRows[i]);
                                }
                                else if (i < thirdPartStart)
                                {
                                    harmonogramDataCenter.Add(allRows[i]);
                                }
                                else
                                {
                                    harmonogramDataRight.Add(allRows[i]);
                                }
                            }

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Błąd ładowania harmonogramu: {ex.Message}");
            }
        }
        private void UpdateFullDateLabel()
        {
            if (dateTimePicker1.SelectedDate.HasValue)
            {
                var date = dateTimePicker1.SelectedDate.Value;
                // Polska nazwa dnia tygodnia
                string[] dniTygodnia = { "Niedziela", "Poniedziałek", "Wtorek", "Środa", "Czwartek", "Piątek", "Sobota" };
                string dzienTygodnia = dniTygodnia[(int)date.DayOfWeek];
                lblFullDate.Text = $"{dzienTygodnia}, {date:dd MMMM yyyy}";
            }
        }

        private void BtnPrevDay_Click(object sender, RoutedEventArgs e)
        {
            if (dateTimePicker1.SelectedDate.HasValue)
            {
                dateTimePicker1.SelectedDate = dateTimePicker1.SelectedDate.Value.AddDays(-1);
            }
        }

        private void BtnNextDay_Click(object sender, RoutedEventArgs e)
        {
            if (dateTimePicker1.SelectedDate.HasValue)
            {
                dateTimePicker1.SelectedDate = dateTimePicker1.SelectedDate.Value.AddDays(1);
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl + S - Zapisz
            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
            {
                BtnSaveAll_Click(null, null);
                e.Handled = true;
            }
            // Ctrl + P - Drukuj
            else if (e.Key == Key.P && Keyboard.Modifiers == ModifierKeys.Control)
            {
                Button1_Click(null, null);
                e.Handled = true;
            }
            // Ctrl + Z - Cofnij zmianę
            else if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
            {
                UndoLastChange();
                e.Handled = true;
            }
            // Delete - Usuń zaznaczony wiersz
            else if (e.Key == Key.Delete && selectedRow != null)
            {
                DeleteSelectedRow();
                e.Handled = true;
            }
            // F5 - Odśwież
            else if (e.Key == Key.F5)
            {
                LoadData(dateTimePicker1.SelectedDate ?? DateTime.Today);
                e.Handled = true;
            }
            // Alt + Up - Przesuń w górę
            else if (e.Key == Key.Up && Keyboard.Modifiers == ModifierKeys.Alt)
            {
                ButtonUP_Click(null, null);
                e.Handled = true;
            }
            // Alt + Down - Przesuń w dół
            else if (e.Key == Key.Down && Keyboard.Modifiers == ModifierKeys.Alt)
            {
                ButtonDown_Click(null, null);
                e.Handled = true;
            }
        }

        private void DateTimePicker1_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dateTimePicker1.SelectedDate.HasValue)
            {
                LoadData(dateTimePicker1.SelectedDate.Value);
                UpdateFullDateLabel();
                UpdateTransportDateLabel();

                // Odśwież wszystkie karty przy zmianie daty
                LoadRozliczeniaData();
                LoadPlachtaData();
                LoadPodsumowanieData(); // Auto-odświeżanie karty Podsumowanie

                // Odśwież mini kalendarz
                RefreshMiniCalendar();
            }
        }

        private void LoadData(DateTime selectedDate)
        {
            _isLoadingData = true; // Blokuj logowanie zmian podczas ładowania
            try
            {
                UpdateStatus("Ładowanie danych...");
                specyfikacjeData.Clear();

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"SELECT fc.ID, fc.CarLp, fc.Number, fc.YearNumber, fc.CustomerGID, fc.CustomerRealGID, fc.DeclI1, fc.DeclI2, fc.DeclI3, fc.DeclI4, fc.DeclI5,
                                    fc.LumQnt, fc.ProdQnt, fc.ProdWgt, fc.FullFarmWeight, fc.EmptyFarmWeight, fc.NettoFarmWeight,
                                    fc.FullWeight, fc.EmptyWeight, fc.NettoWeight, fc.Price, fc.Addition, fc.PriceTypeID, fc.IncDeadConf, fc.Loss,
                                    fc.Opasienie, fc.KlasaB, fc.TerminDni, fc.CalcDate, fc.PayWgt,
                                    fc.DriverGID, fc.CarID, fc.TrailerID, fc.Przyjazd,
                                    fc.PoczatekUslugi, fc.Wyjazd, fc.DojazdHodowca, fc.Zaladunek, fc.ZaladunekKoniec, fc.WyjazdHodowca, fc.KoniecUslugi,
                                    fc.ZdjecieTaraPath, fc.ZdjecieBruttoPath,
                                    fc.PartiaGuid,
                                    pd.Partia AS PartiaNumber,
                                    d.Name AS DriverName
                                    FROM [LibraNet].[dbo].[FarmerCalc] fc
                                    LEFT JOIN [LibraNet].[dbo].[Driver] d ON fc.DriverGID = d.GID
                                    LEFT JOIN [LibraNet].[dbo].[PartiaDostawca] pd ON fc.PartiaGuid = pd.guid
                                    WHERE fc.CalcDate = @SelectedDate
                                    ORDER BY fc.CarLP";

                    SqlCommand command = new SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@SelectedDate", selectedDate);

                    SqlDataAdapter adapter = new SqlDataAdapter(command);
                    DataTable dataTable = new DataTable();
                    adapter.Fill(dataTable);

                    if (dataTable.Rows.Count > 0)
                    {
                        foreach (DataRow row in dataTable.Rows)
                        {
                            // WAŻNE: Trim() usuwa spacje z nchar(10) - bez tego ComboBox nie znajdzie dopasowania
                            string customerGID = ZapytaniaSQL.GetValueOrDefault<string>(row, "CustomerGID", "-1")?.Trim();
                            decimal nettoUbojniValue = ZapytaniaSQL.GetValueOrDefault<decimal>(row, "NettoWeight", 0);
                            decimal nettoHodowcyValue = ZapytaniaSQL.GetValueOrDefault<decimal>(row, "NettoFarmWeight", 0);

                            var specRow = new SpecyfikacjaRow
                            {
                                ID = ZapytaniaSQL.GetValueOrDefault<int>(row, "ID", 0),
                                Nr = ZapytaniaSQL.GetValueOrDefault<int>(row, "CarLp", 0),
                                Number = ZapytaniaSQL.GetValueOrDefault<int>(row, "Number", 0),
                                YearNumber = ZapytaniaSQL.GetValueOrDefault<int>(row, "YearNumber", 0),
                                DostawcaGID = customerGID,
                                Dostawca = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(customerGID, "ShortName"),
                                RealDostawca = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(
                                    ZapytaniaSQL.GetValueOrDefault<string>(row, "CustomerRealGID", "-1"), "ShortName"),
                                SztukiDek = ZapytaniaSQL.GetValueOrDefault<int>(row, "DeclI1", 0),
                                Padle = ZapytaniaSQL.GetValueOrDefault<int>(row, "DeclI2", 0),
                                CH = ZapytaniaSQL.GetValueOrDefault<int>(row, "DeclI3", 0),
                                NW = ZapytaniaSQL.GetValueOrDefault<int>(row, "DeclI4", 0),
                                ZM = ZapytaniaSQL.GetValueOrDefault<int>(row, "DeclI5", 0),
                                BruttoHodowcy = FormatWeight(row, "FullFarmWeight"),
                                TaraHodowcy = FormatWeight(row, "EmptyFarmWeight"),
                                NettoHodowcy = FormatWeight(row, "NettoFarmWeight"),
                                NettoHodowcyValue = nettoHodowcyValue,
                                BruttoUbojni = FormatWeight(row, "FullWeight"),
                                TaraUbojni = FormatWeight(row, "EmptyWeight"),
                                NettoUbojni = FormatWeight(row, "NettoWeight"),
                                NettoUbojniValue = nettoUbojniValue,
                                LUMEL = ZapytaniaSQL.GetValueOrDefault<int>(row, "LumQnt", 0),
                                SztukiWybijak = ZapytaniaSQL.GetValueOrDefault<int>(row, "ProdQnt", 0),
                                KilogramyWybijak = ZapytaniaSQL.GetValueOrDefault<decimal>(row, "ProdWgt", 0),
                                Cena = ZapytaniaSQL.GetValueOrDefault<decimal>(row, "Price", 0),
                                Dodatek = ZapytaniaSQL.GetValueOrDefault<decimal>(row, "Addition", 0),
                                TypCeny = zapytaniasql.ZnajdzNazweCenyPoID(
                                    ZapytaniaSQL.GetValueOrDefault<int>(row, "PriceTypeID", -1)),
                                PiK = row["IncDeadConf"] != DBNull.Value && Convert.ToBoolean(row["IncDeadConf"]),
                                Ubytek = Math.Round(ZapytaniaSQL.GetValueOrDefault<decimal>(row, "Loss", 0) * 100, 2),
                                // Nowe pola
                                Opasienie = ZapytaniaSQL.GetValueOrDefault<decimal>(row, "Opasienie", 0),
                                KlasaB = ZapytaniaSQL.GetValueOrDefault<decimal>(row, "KlasaB", 0),
                                TerminDni = ZapytaniaSQL.GetValueOrDefault<int>(row, "TerminDni", 35),
                                DataUboju = ZapytaniaSQL.GetValueOrDefault<DateTime>(row, "CalcDate", DateTime.Today),
                                // Pola transportowe
                                DriverGID = row["DriverGID"] != DBNull.Value ? (int?)Convert.ToInt32(row["DriverGID"]) : null,
                                CarID = ZapytaniaSQL.GetValueOrDefault<string>(row, "CarID", "")?.Trim(),
                                TrailerID = ZapytaniaSQL.GetValueOrDefault<string>(row, "TrailerID", "")?.Trim(),
                                ArrivalTime = row["Przyjazd"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["Przyjazd"]) : null,
                                KierowcaNazwa = ZapytaniaSQL.GetValueOrDefault<string>(row, "DriverName", "")?.Trim(),
                                // Godziny transportowe
                                PoczatekUslugi = row["PoczatekUslugi"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["PoczatekUslugi"]) : null,
                                Wyjazd = row["Wyjazd"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["Wyjazd"]) : null,
                                DojazdHodowca = row["DojazdHodowca"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["DojazdHodowca"]) : null,
                                Zaladunek = row["Zaladunek"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["Zaladunek"]) : null,
                                ZaladunekKoniec = row["ZaladunekKoniec"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["ZaladunekKoniec"]) : null,
                                WyjazdHodowca = row["WyjazdHodowca"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["WyjazdHodowca"]) : null,
                                KoniecUslugi = row["KoniecUslugi"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["KoniecUslugi"]) : null,
                                // Pole Symfonia (sprawdź czy kolumna istnieje)
                                Symfonia = dataTable.Columns.Contains("Symfonia") && row["Symfonia"] != DBNull.Value && Convert.ToBoolean(row["Symfonia"]),
                                // Ścieżki do zdjęć z ważenia
                                ZdjecieTaraPath = dataTable.Columns.Contains("ZdjecieTaraPath") ? ZapytaniaSQL.GetValueOrDefault<string>(row, "ZdjecieTaraPath", null)?.Trim() : null,
                                ZdjecieBruttoPath = dataTable.Columns.Contains("ZdjecieBruttoPath") ? ZapytaniaSQL.GetValueOrDefault<string>(row, "ZdjecieBruttoPath", null)?.Trim() : null,
                                // PayWgt z bazy - kolumna "Do zapł." z PDF
                                PayWgt = ZapytaniaSQL.GetValueOrDefault<decimal>(row, "PayWgt", 0),
                                // Odbiorca (Customer) - pobierz nazwę z CustomerRealGID
                                Odbiorca = GetCustomerName(ZapytaniaSQL.GetValueOrDefault<string>(row, "CustomerRealGID", "-1")?.Trim()),
                                // Partia drobiu
                                PartiaGuid = dataTable.Columns.Contains("PartiaGuid") && row["PartiaGuid"] != DBNull.Value ? (Guid?)row["PartiaGuid"] : null,
                                PartiaNumber = dataTable.Columns.Contains("PartiaNumber") ? ZapytaniaSQL.GetValueOrDefault<string>(row, "PartiaNumber", null)?.Trim() : null
                            };

                            specyfikacjeData.Add(specRow);
                        }
                        UpdateStatistics();
                        LoadTransportData(); // Załaduj dane transportowe
                        LoadHarmonogramData(); // Załaduj harmonogram dostaw
                        LoadPdfStatusForAllRows(); // Załaduj status PDF dla wszystkich wierszy
                        AssignSupplierColorsAndGroups(); // Przypisz kolory dostawcom
                        UpdateStatus($"Załadowano {dataTable.Rows.Count} rekordów");
                    }
                    else
                    {
                        UpdateStatistics();
                        LoadTransportData(); // Wyczyść dane transportowe
                        LoadHarmonogramData(); // Wyczyść harmonogram
                        UpdateStatus("Brak danych dla wybranej daty");
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Błąd: {ex.Message}");
            }
            finally
            {
                // WAŻNE: Resetuj flagę DOPIERO gdy WPF skończy binding checkboxów
                // Użyj niskiego priorytetu aby poczekać na zakończenie wszystkich operacji UI
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    _isLoadingData = false;
                }), System.Windows.Threading.DispatcherPriority.ContextIdle);
            }
        }

        private string FormatWeight(DataRow row, string columnName)
        {
            if (row[columnName] != DBNull.Value)
            {
                decimal value = Convert.ToDecimal(row[columnName]);
                return value.ToString("#,0");
            }
            return string.Empty;
        }

        // Event handler dla ComboBox Dostawcy - ustawienie listy
        private void CboDostawca_Loaded(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox != null && comboBox.ItemsSource == null)
            {
                comboBox.ItemsSource = ListaDostawcow;
            }
        }

        // Event handler dla ComboBox Typ Ceny - ustawienie listy
        private void CboTypCeny_Loaded(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox != null && comboBox.ItemsSource == null)
            {
                comboBox.ItemsSource = listaTypowCen;
            }
        }

        private void DataGridView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // KLUCZ: Pobierz wiersz z e.AddedItems PRZED Dispatcherem (nie z SelectedItem!)
            if (e.AddedItems.Count == 0) return;

            var selected = e.AddedItems[0] as SpecyfikacjaRow;
            if (selected == null) return;

            // Zapisz wybrany wiersz od razu - to MUSI być natychmiastowe
            selectedRow = selected;

            // Pobierz klucz dostawcy TERAZ (przed Dispatcherem)
            var dostawcaKey = selected.Dostawca?.Trim();

            // Podświetlenie na NAJNIŻSZYM priorytecie - nie blokuje nawigacji/edycji
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!string.IsNullOrEmpty(dostawcaKey))
                {
                    HighlightSupplierGroup(dostawcaKey);
                }
                else
                {
                    ClearSupplierHighlight();
                }
            }), DispatcherPriority.ApplicationIdle);
        }

        // === CurrentCellChanged: Tylko aktualizacja selectedRow ===
        private void DataGridView1_CurrentCellChanged(object sender, EventArgs e)
        {
            if (dataGridView1.CurrentCell != null && dataGridView1.CurrentCell.Item is SpecyfikacjaRow currentRow)
            {
                selectedRow = currentRow;
            }
        }

        // === PRZYCISK STRZAŁKA W GÓRĘ: Przesuń wiersz w górę ===
        private void BtnMoveUp_Click(object sender, RoutedEventArgs e)
        {
            // Użyj selectedRow (pole klasy) zamiast SelectedItem - bardziej niezawodne
            var selected = selectedRow ?? dataGridView1.SelectedItem as SpecyfikacjaRow;
            if (selected == null)
            {
                MessageBox.Show("Wybierz wiersz do przesunięcia", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int currentIndex = specyfikacjeData.IndexOf(selected);
            if (currentIndex <= 0)
            {
                UpdateStatus("Wiersz jest już na górze");
                return;
            }

            // Przesuń wiersz w górę
            specyfikacjeData.Move(currentIndex, currentIndex - 1);
            UpdateRowNumbers();
            dataGridView1.SelectedItem = selected;
            selectedRow = selected;
            SaveAllRowPositions();

            // Synchronizuj Transport i Płachtę
            RefreshTransportFromSpecyfikacje();
            RefreshPlachtaFromSpecyfikacje();

            UpdateStatus($"Przesunięto wiersz LP {selected.Nr} w górę");
        }

        // === PRZYCISK STRZAŁKA W DÓŁ: Przesuń wiersz w dół ===
        private void BtnMoveDown_Click(object sender, RoutedEventArgs e)
        {
            // Użyj selectedRow (pole klasy) zamiast SelectedItem - bardziej niezawodne
            var selected = selectedRow ?? dataGridView1.SelectedItem as SpecyfikacjaRow;
            if (selected == null)
            {
                MessageBox.Show("Wybierz wiersz do przesunięcia", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int currentIndex = specyfikacjeData.IndexOf(selected);
            if (currentIndex < 0 || currentIndex >= specyfikacjeData.Count - 1)
            {
                UpdateStatus("Wiersz jest już na dole");
                return;
            }

            // Przesuń wiersz w dół
            specyfikacjeData.Move(currentIndex, currentIndex + 1);
            UpdateRowNumbers();
            dataGridView1.SelectedItem = selected;
            selectedRow = selected;
            SaveAllRowPositions();

            // Synchronizuj Transport i Płachtę
            RefreshTransportFromSpecyfikacje();
            RefreshPlachtaFromSpecyfikacje();

            UpdateStatus($"Przesunięto wiersz LP {selected.Nr} w dół");
        }

        /// <summary>
        /// Odświeża Transport na podstawie aktualnej kolejności specyfikacjeData
        /// </summary>
        private void RefreshTransportFromSpecyfikacje()
        {
            LoadTransportData();
        }

        // === WYDAJNOŚĆ: Auto-zapis pozycji - używa async batch update ===
        private void SaveAllRowPositions()
        {
            // Użyj async wersji dla lepszej wydajności (nie blokuje UI)
            _ = SaveAllRowPositionsAsync();
        }

        // === Aktualizacja numerów LP po przeciągnięciu ===
        private void UpdateRowNumbers()
        {
            for (int i = 0; i < specyfikacjeData.Count; i++)
            {
                specyfikacjeData[i].Nr = i + 1;
            }
        }

        // === ComboBox: Automatyczne zaznaczenie wiersza przy otwarciu ===
        private void CboDostawca_DropDownOpened(object sender, EventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox != null)
            {
                var row = FindVisualParent<DataGridRow>(comboBox);
                if (row != null)
                {
                    var item = row.Item as SpecyfikacjaRow;
                    if (item != null)
                    {
                        dataGridView1.SelectedItem = item;
                        selectedRow = item;
                    }
                }
            }
        }

        // === Natychmiastowy zapis przy zmianie ComboBox Dostawca ===
        private void ComboBox_Dostawca_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox == null || e.AddedItems.Count == 0) return;

            // Znajdź wiersz danych
            var row = FindVisualParent<DataGridRow>(comboBox);
            if (row == null) return;

            var specRow = row.Item as SpecyfikacjaRow;
            if (specRow == null) return;

            // Pobierz wybrany element
            var selected = e.AddedItems[0] as DostawcaItem;
            if (selected != null)
            {
                // Aktualizuj nazwę dostawcy
                specRow.Dostawca = selected.ShortName;

                // Natychmiastowy zapis do bazy
                try
                {
                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        connection.Open();
                        string query = "UPDATE dbo.FarmerCalc SET CustomerGID = @GID WHERE ID = @ID";
                        using (SqlCommand cmd = new SqlCommand(query, connection))
                        {
                            cmd.Parameters.AddWithValue("@GID", (object)selected.GID ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@ID", specRow.ID);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    UpdateStatus($"Zmieniono dostawcę: {selected.ShortName}");
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Błąd zapisu dostawcy: {ex.Message}");
                }
            }
        }

        // === AUTOCOMPLETE DOSTAWCY: Handlery ===

        /// <summary>
        /// Timer debounce - filtruje dostawców po 200ms nieaktywności
        /// </summary>
        private void SupplierFilterTimer_Tick(object sender, EventArgs e)
        {
            _supplierFilterTimer.Stop();

            if (_currentSupplierTextBox == null) return;

            var searchText = _currentSupplierTextBox.Text?.Trim() ?? "";
            FilterSupplierSuggestions(searchText, _currentSupplierTextBox);
        }

        /// <summary>
        /// Filtruje listę dostawców - Contains match z priorytetem StartsWith
        /// </summary>
        private void FilterSupplierSuggestions(string searchText, TextBox textBox)
        {
            SupplierSuggestions.Clear();

            if (string.IsNullOrEmpty(searchText) || searchText.Length < 2)
            {
                CloseSupplierPopup(textBox);
                return;
            }

            var searchUpper = searchText.ToUpperInvariant();

            // Filtruj: StartsWith ma priorytet, potem Contains
            var startsWithMatches = ListaDostawcow
                .Where(d => !string.IsNullOrEmpty(d.ShortName) &&
                            d.ShortName.ToUpperInvariant().StartsWith(searchUpper))
                .OrderBy(d => d.ShortName)
                .ToList();

            var containsMatches = ListaDostawcow
                .Where(d => !string.IsNullOrEmpty(d.ShortName) &&
                            !d.ShortName.ToUpperInvariant().StartsWith(searchUpper) &&
                            d.ShortName.ToUpperInvariant().Contains(searchUpper))
                .OrderBy(d => d.ShortName)
                .ToList();

            // Połącz: najpierw StartsWith, potem Contains (max 20 wyników)
            var allMatches = startsWithMatches.Concat(containsMatches).Take(20).ToList();

            if (allMatches.Count == 0)
            {
                CloseSupplierPopup(textBox);
                return;
            }

            foreach (var match in allMatches)
            {
                SupplierSuggestions.Add(match);
            }

            // Otwórz popup
            OpenSupplierPopup(textBox);
        }

        /// <summary>
        /// Otwiera popup z sugestiami
        /// </summary>
        private void OpenSupplierPopup(TextBox textBox)
        {
            var popup = FindVisualSibling<Popup>(textBox, "popupSupplierSuggestions");
            if (popup != null)
            {
                popup.IsOpen = true;
            }
        }

        /// <summary>
        /// Zamyka popup z sugestiami
        /// </summary>
        private void CloseSupplierPopup(TextBox textBox)
        {
            var popup = FindVisualSibling<Popup>(textBox, "popupSupplierSuggestions");
            if (popup != null)
            {
                popup.IsOpen = false;
            }
        }

        /// <summary>
        /// Znajduje sibling element w tym samym kontenerze
        /// </summary>
        private T FindVisualSibling<T>(DependencyObject element, string name) where T : FrameworkElement
        {
            var parent = VisualTreeHelper.GetParent(element);
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T found && found.Name == name)
                    return found;

                // Szukaj rekurencyjnie w dzieciach
                var result = FindChildByName<T>(child, name);
                if (result != null)
                    return result;
            }
            return null;
        }

        private T FindChildByName<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T found && found.Name == name)
                    return found;

                var result = FindChildByName<T>(child, name);
                if (result != null)
                    return result;
            }
            return null;
        }

        /// <summary>
        /// TextBox TextChanged - uruchamia debounce timer
        /// </summary>
        private void SupplierTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            _currentSupplierTextBox = textBox;

            // Restart debounce timer
            _supplierFilterTimer.Stop();
            _supplierFilterTimer.Start();
        }

        /// <summary>
        /// TextBox PreviewKeyDown - nawigacja klawiaturą
        /// </summary>
        private void SupplierTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            var popup = FindVisualSibling<Popup>(textBox, "popupSupplierSuggestions");
            var listBox = popup != null ? FindChildByName<ListBox>(popup.Child, "lstSupplierSuggestions") : null;

            switch (e.Key)
            {
                case Key.Down:
                    if (popup?.IsOpen == true && listBox != null)
                    {
                        // Przenieś fokus do listy
                        if (listBox.Items.Count > 0)
                        {
                            listBox.SelectedIndex = Math.Max(0, listBox.SelectedIndex);
                            if (listBox.SelectedIndex < 0) listBox.SelectedIndex = 0;
                            listBox.Focus();
                            var item = listBox.ItemContainerGenerator.ContainerFromIndex(listBox.SelectedIndex) as ListBoxItem;
                            item?.Focus();
                        }
                        e.Handled = true;
                    }
                    break;

                case Key.Escape:
                    // Zamknij popup i przywróć oryginalną wartość
                    if (popup?.IsOpen == true)
                    {
                        popup.IsOpen = false;
                        e.Handled = true;
                    }
                    break;

                case Key.Enter:
                    if (popup?.IsOpen == true && listBox?.SelectedItem != null)
                    {
                        // Wybierz zaznaczony element
                        SelectSupplierFromList(textBox, listBox.SelectedItem as DostawcaItem);
                        e.Handled = true;
                    }
                    else if (popup?.IsOpen == true && listBox?.Items.Count > 0)
                    {
                        // Wybierz pierwszy element
                        SelectSupplierFromList(textBox, listBox.Items[0] as DostawcaItem);
                        e.Handled = true;
                    }
                    break;

                case Key.Tab:
                    // Zamknij popup przy Tab
                    if (popup?.IsOpen == true)
                    {
                        popup.IsOpen = false;
                    }
                    break;
            }
        }

        /// <summary>
        /// TextBox LostFocus - zamknij popup
        /// </summary>
        private void SupplierTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            // Małe opóźnienie aby pozwolić na kliknięcie w ListBox
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // Sprawdź czy fokus nie przeszedł do ListBox
                var focused = FocusManager.GetFocusedElement(this);
                if (!(focused is ListBoxItem))
                {
                    CloseSupplierPopup(textBox);
                    SaveSupplierFromTextBox(textBox);
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        /// <summary>
        /// ListBox kliknięcie - wybierz dostawcę
        /// </summary>
        private void SupplierListBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox == null) return;

            // Pobierz kliknięty element z visual tree (PreviewMouseUp jest przed ustawieniem SelectedItem)
            var clickedElement = e.OriginalSource as DependencyObject;
            if (clickedElement == null) return;

            // Znajdź ListBoxItem który został kliknięty
            var listBoxItem = FindVisualParent<ListBoxItem>(clickedElement);
            if (listBoxItem == null) return;

            // Pobierz DostawcaItem z DataContext
            var selected = listBoxItem.DataContext as DostawcaItem;
            if (selected == null) return;

            // Użyj zapisanego TextBox (ustawionego w TextChanged)
            if (_currentSupplierTextBox != null)
            {
                SelectSupplierFromList(_currentSupplierTextBox, selected);
            }
        }

        /// <summary>
        /// ListBox PreviewKeyDown - Enter wybiera, Escape zamyka
        /// </summary>
        private void SupplierListBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox == null) return;

            var popup = FindVisualParent<Popup>(listBox);

            switch (e.Key)
            {
                case Key.Enter:
                    if (listBox.SelectedItem != null && _currentSupplierTextBox != null)
                    {
                        SelectSupplierFromList(_currentSupplierTextBox, listBox.SelectedItem as DostawcaItem);
                        e.Handled = true;
                    }
                    break;

                case Key.Escape:
                    if (popup != null)
                    {
                        popup.IsOpen = false;
                        _currentSupplierTextBox?.Focus();
                        e.Handled = true;
                    }
                    break;

                case Key.Up:
                    if (listBox.SelectedIndex == 0 && _currentSupplierTextBox != null)
                    {
                        // Wróć do TextBox
                        _currentSupplierTextBox.Focus();
                        e.Handled = true;
                    }
                    break;
            }
        }

        /// <summary>
        /// Wybiera dostawcę z listy i aktualizuje wiersz
        /// </summary>
        private void SelectSupplierFromList(TextBox textBox, DostawcaItem selected)
        {
            if (selected == null || textBox == null) return;

            // Zamknij popup
            CloseSupplierPopup(textBox);

            // Aktualizuj TextBox
            textBox.Text = selected.ShortName;
            textBox.CaretIndex = textBox.Text.Length;

            // Pobierz wiersz danych z Tag
            var specRow = textBox.Tag as SpecyfikacjaRow;
            if (specRow == null) return;

            // Aktualizuj dane wiersza
            specRow.Dostawca = selected.ShortName;
            specRow.DostawcaGID = selected.GID;
            specRow.RealDostawca = selected.ShortName;

            // Zapisz do bazy
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = "UPDATE dbo.FarmerCalc SET CustomerGID = @GID WHERE ID = @ID";
                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@GID", (object)selected.GID ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@ID", specRow.ID);
                        cmd.ExecuteNonQuery();
                    }
                }
                UpdateStatus($"Wybrano dostawcę: {selected.ShortName}");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Błąd zapisu dostawcy: {ex.Message}");
            }

            // Przejdź do następnej kolumny
            textBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }

        /// <summary>
        /// Zapisuje dostawcę wpisanego ręcznie (jeśli pasuje do listy)
        /// </summary>
        private void SaveSupplierFromTextBox(TextBox textBox)
        {
            if (textBox == null) return;

            var text = textBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(text)) return;

            // Znajdź dokładne dopasowanie
            var match = ListaDostawcow.FirstOrDefault(d =>
                d.ShortName?.Equals(text, StringComparison.OrdinalIgnoreCase) == true);

            if (match != null)
            {
                var specRow = textBox.Tag as SpecyfikacjaRow;
                if (specRow != null && specRow.DostawcaGID != match.GID)
                {
                    specRow.Dostawca = match.ShortName;
                    specRow.DostawcaGID = match.GID;
                    specRow.RealDostawca = match.ShortName;

                    // Zapisz do bazy
                    try
                    {
                        using (SqlConnection connection = new SqlConnection(connectionString))
                        {
                            connection.Open();
                            string query = "UPDATE dbo.FarmerCalc SET CustomerGID = @GID WHERE ID = @ID";
                            using (SqlCommand cmd = new SqlCommand(query, connection))
                            {
                                cmd.Parameters.AddWithValue("@GID", (object)match.GID ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@ID", specRow.ID);
                                cmd.ExecuteNonQuery();
                            }
                        }
                    }
                    catch { }
                }
            }
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T found)
                    return found;

                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        // === NAWIGACJA EXCEL-LIKE: Kompleksowa obsługa klawiszy ===
        private void DataGridView1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // === ENTER: Przejście w dół (Shift+Enter = góra) - NATYCHMIASTOWA NAWIGACJA ===
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                bool goUp = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
                NavigateToNextRow(goUp);
            }

            // === TAB: Przejście w prawo (Shift+Tab = lewo) - NATYCHMIASTOWA NAWIGACJA ===
            else if (e.Key == Key.Tab)
            {
                e.Handled = true;
                bool goLeft = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
                NavigateToNextColumn(goLeft);
            }

            // === STRZAŁKA GÓRA/DÓŁ: Szybka nawigacja między wierszami ===
            else if (e.Key == Key.Down || e.Key == Key.Up)
            {
                // Tylko jeśli nie jesteśmy w trybie edycji TextBox
                if (!(e.OriginalSource is TextBox))
                {
                    e.Handled = true;
                    NavigateToNextRow(e.Key == Key.Up);
                }
            }

            // === CTRL+D: Kopiuj wartość z komórki powyżej ===
            else if (e.Key == Key.D && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                e.Handled = true;
                CopyValueFromCellAbove();
            }

            // === CTRL+SHIFT+D: Kopiuj wartość do wszystkich wierszy tego dostawcy ===
            else if (e.Key == Key.D && Keyboard.Modifiers.HasFlag(ModifierKeys.Control | ModifierKeys.Shift))
            {
                e.Handled = true;
                ApplyValueToAllRowsOfSupplier();
            }

            // === F2: Wejdź w edycję i zaznacz wszystko ===
            else if (e.Key == Key.F2)
            {
                var cellInfo = dataGridView1.CurrentCell;
                if (cellInfo.IsValid)
                {
                    dataGridView1.BeginEdit();
                    var cellContent = cellInfo.Column.GetCellContent(cellInfo.Item);
                    var textBox = FindVisualChild<TextBox>(cellContent);
                    if (textBox != null)
                    {
                        textBox.Focus();
                        textBox.SelectAll();
                    }
                }
            }

            // === DELETE: Wyczyść zawartość komórki ===
            else if (e.Key == Key.Delete)
            {
                var cellInfo = dataGridView1.CurrentCell;
                if (cellInfo.IsValid && !cellInfo.Column.IsReadOnly)
                {
                    var cellContent = cellInfo.Column.GetCellContent(cellInfo.Item);
                    var textBox = FindVisualChild<TextBox>(cellContent);
                    if (textBox != null)
                    {
                        textBox.Text = "";
                        e.Handled = true;
                    }
                }
            }

            // === ESCAPE: Anuluj edycję ===
            else if (e.Key == Key.Escape)
            {
                dataGridView1.CancelEdit();
            }

            // === CTRL+SHIFT+C: Kopiuj ustawienia cenowe wiersza do schowka ===
            else if (e.Key == Key.C && Keyboard.Modifiers.HasFlag(ModifierKeys.Control | ModifierKeys.Shift))
            {
                e.Handled = true;
                CopyRowSettingsToClipboard();
            }

            // === CTRL+SHIFT+V: Wklej ustawienia ze schowka ===
            else if (e.Key == Key.V && Keyboard.Modifiers.HasFlag(ModifierKeys.Control | ModifierKeys.Shift))
            {
                e.Handled = true;
                PasteRowSettingsFromClipboard();
            }

            // === CTRL+SHIFT+A: Zastosuj WSZYSTKIE pola do dostawcy ===
            else if (e.Key == Key.A && Keyboard.Modifiers.HasFlag(ModifierKeys.Control | ModifierKeys.Shift))
            {
                e.Handled = true;
                ApplyFieldsToSupplier(SupplierFieldMask.All);
            }
        }

        // === NAWIGACJA: Natychmiastowe przejście do następnego/poprzedniego wiersza ===
        private void NavigateToNextRow(bool goUp)
        {
            var currentCell = dataGridView1.CurrentCell;
            if (!currentCell.IsValid || currentCell.Item == null) return;

            // Zatwierdź bieżącą edycję (nieblokujące)
            dataGridView1.CommitEdit(DataGridEditingUnit.Cell, true);

            var items = dataGridView1.Items;
            int currentIndex = items.IndexOf(currentCell.Item);
            int newIndex = goUp ? currentIndex - 1 : currentIndex + 1;

            // Sprawdź granice
            if (newIndex < 0 || newIndex >= items.Count) return;

            var newItem = items[newIndex];
            var column = currentCell.Column;

            // Natychmiast ustaw nową komórkę
            dataGridView1.CurrentCell = new DataGridCellInfo(newItem, column);
            dataGridView1.SelectedItem = newItem;

            // Rozpocznij edycję i zaznacz tekst natychmiast
            BeginEditAndSelectAll();
        }

        // === NAWIGACJA: Natychmiastowe przejście do następnej/poprzedniej kolumny ===
        private void NavigateToNextColumn(bool goLeft)
        {
            var currentCell = dataGridView1.CurrentCell;
            if (!currentCell.IsValid || currentCell.Item == null) return;

            // Zatwierdź bieżącą edycję (nieblokujące)
            dataGridView1.CommitEdit(DataGridEditingUnit.Cell, true);

            var columns = dataGridView1.Columns;
            int currentColIndex = columns.IndexOf(currentCell.Column);

            // Znajdź następną edytowalną kolumnę
            int newColIndex = FindNextEditableColumn(currentColIndex, goLeft);
            if (newColIndex < 0) return;

            var newColumn = columns[newColIndex];
            var item = currentCell.Item;

            // Natychmiast ustaw nową komórkę
            dataGridView1.CurrentCell = new DataGridCellInfo(item, newColumn);

            // Rozpocznij edycję i zaznacz tekst natychmiast
            BeginEditAndSelectAll();
        }

        // === HELPER: Rozpocznij edycję i zaznacz cały tekst ===
        private void BeginEditAndSelectAll()
        {
            // Natychmiastowe rozpoczęcie edycji
            Dispatcher.BeginInvoke(new Action(() =>
            {
                dataGridView1.BeginEdit();

                // Po rozpoczęciu edycji zaznacz cały tekst
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var cellInfo = dataGridView1.CurrentCell;
                    if (cellInfo.IsValid)
                    {
                        var cellContent = cellInfo.Column.GetCellContent(cellInfo.Item);
                        var textBox = FindVisualChild<TextBox>(cellContent);
                        if (textBox != null)
                        {
                            textBox.Focus();
                            textBox.SelectAll();
                        }
                    }
                }), DispatcherPriority.Input);
            }), DispatcherPriority.Send);
        }

        // === HELPER: Znajdź następną edytowalną kolumnę ===
        private int FindNextEditableColumn(int currentIndex, bool goLeft)
        {
            var columns = dataGridView1.Columns;
            int count = columns.Count;
            int step = goLeft ? -1 : 1;
            int index = currentIndex + step;

            // Szukaj w kierunku (zawijanie do początku/końca)
            int iterations = 0;
            while (iterations < count)
            {
                if (index < 0) index = count - 1;
                if (index >= count) index = 0;

                var col = columns[index];
                if (!col.IsReadOnly && col.Visibility == Visibility.Visible)
                {
                    return index;
                }

                index += step;
                iterations++;
            }

            return -1; // Brak edytowalnej kolumny
        }

        // === SCHOWEK: Kopiuj ustawienia cenowe wiersza ===
        private void CopyRowSettingsToClipboard()
        {
            var currentRow = selectedRow ?? dataGridView1.SelectedItem as SpecyfikacjaRow;
            if (currentRow == null) return;

            _supplierClipboard.CopyFrom(currentRow);
            UpdateStatus($"Skopiowano: {_supplierClipboard} (Ctrl+Shift+V aby wkleić)");
        }

        // === SCHOWEK: Wklej ustawienia do aktualnego wiersza ===
        private void PasteRowSettingsFromClipboard()
        {
            var currentRow = selectedRow ?? dataGridView1.SelectedItem as SpecyfikacjaRow;
            if (currentRow == null || !_supplierClipboard.HasData)
            {
                UpdateStatus("Schowek jest pusty. Użyj Ctrl+Shift+C aby skopiować ustawienia.");
                return;
            }

            // Zastosuj wartości ze schowka
            if (_supplierClipboard.Cena.HasValue) currentRow.Cena = _supplierClipboard.Cena.Value;
            if (_supplierClipboard.Dodatek.HasValue) currentRow.Dodatek = _supplierClipboard.Dodatek.Value;
            if (_supplierClipboard.Ubytek.HasValue) currentRow.Ubytek = _supplierClipboard.Ubytek.Value;
            if (_supplierClipboard.TypCeny != null) currentRow.TypCeny = _supplierClipboard.TypCeny;
            if (_supplierClipboard.TerminDni.HasValue) currentRow.TerminDni = _supplierClipboard.TerminDni.Value;

            // Zapisz do bazy
            QueueRowForSave(currentRow.ID);

            UpdateStatus($"Wklejono: {_supplierClipboard}");
            // Nie potrzeba Items.Refresh() - INotifyPropertyChanged aktualizuje UI
        }

        // === SCHOWEK: Wklej ustawienia do wszystkich wierszy dostawcy ===
        private void PasteRowSettingsToSupplier()
        {
            var currentRow = selectedRow ?? dataGridView1.SelectedItem as SpecyfikacjaRow;
            if (currentRow == null || string.IsNullOrEmpty(currentRow.RealDostawca) || !_supplierClipboard.HasData) return;

            var rowsToUpdate = specyfikacjeData.Where(x => x.RealDostawca == currentRow.RealDostawca).ToList();
            var idsToUpdate = rowsToUpdate.Select(r => r.ID).ToList();

            // Określ które pola mają być zaktualizowane
            var fields = SupplierFieldMask.None;
            if (_supplierClipboard.Cena.HasValue) fields |= SupplierFieldMask.Cena;
            if (_supplierClipboard.Dodatek.HasValue) fields |= SupplierFieldMask.Dodatek;
            if (_supplierClipboard.Ubytek.HasValue) fields |= SupplierFieldMask.Ubytek;
            if (_supplierClipboard.TypCeny != null) fields |= SupplierFieldMask.TypCeny;
            if (_supplierClipboard.TerminDni.HasValue) fields |= SupplierFieldMask.TerminDni;

            // Aktualizuj UI
            foreach (var row in rowsToUpdate)
            {
                if (_supplierClipboard.Cena.HasValue) row.Cena = _supplierClipboard.Cena.Value;
                if (_supplierClipboard.Dodatek.HasValue) row.Dodatek = _supplierClipboard.Dodatek.Value;
                if (_supplierClipboard.Ubytek.HasValue) row.Ubytek = _supplierClipboard.Ubytek.Value;
                if (_supplierClipboard.TypCeny != null) row.TypCeny = _supplierClipboard.TypCeny;
                if (_supplierClipboard.TerminDni.HasValue) row.TerminDni = _supplierClipboard.TerminDni.Value;
            }

            // Batch update
            SaveSupplierFieldsBatch(idsToUpdate, fields,
                _supplierClipboard.Cena, _supplierClipboard.Dodatek, _supplierClipboard.Ubytek,
                _supplierClipboard.TypCeny, _supplierClipboard.TerminDni);

            UpdateStatus($"Wklejono {_supplierClipboard} -> {rowsToUpdate.Count} wierszy ({currentRow.RealDostawca})");
            // Nie potrzeba Items.Refresh() - INotifyPropertyChanged aktualizuje UI
        }

        // === CTRL+D: Kopiuj wartość z komórki powyżej ===
        private void CopyValueFromCellAbove()
        {
            var currentRow = selectedRow ?? dataGridView1.SelectedItem as SpecyfikacjaRow;
            if (currentRow == null) return;

            int currentIndex = specyfikacjeData.IndexOf(currentRow);
            if (currentIndex <= 0) return; // Brak wiersza powyżej

            var rowAbove = specyfikacjeData[currentIndex - 1];
            var currentColumn = dataGridView1.CurrentColumn;
            if (currentColumn == null) return;

            // Używaj SortMemberPath lub Header
            string columnKey = currentColumn.SortMemberPath ?? currentColumn.Header?.ToString() ?? "";
            bool copied = false;

            switch (columnKey)
            {
                case "Cena":
                    currentRow.Cena = rowAbove.Cena;
                    QueueRowForSave(currentRow.ID);
                    copied = true;
                    break;
                case "Dodatek":
                    currentRow.Dodatek = rowAbove.Dodatek;
                    QueueRowForSave(currentRow.ID);
                    copied = true;
                    break;
                case "Ubytek":
                case "Ubytek%":
                    currentRow.Ubytek = rowAbove.Ubytek;
                    QueueRowForSave(currentRow.ID);
                    copied = true;
                    break;
                case "TypCeny":
                case "Typ Ceny":
                    currentRow.TypCeny = rowAbove.TypCeny;
                    QueueRowForSave(currentRow.ID);
                    copied = true;
                    break;
                case "TerminDni":
                case "Termin":
                    currentRow.TerminDni = rowAbove.TerminDni;
                    QueueRowForSave(currentRow.ID);
                    copied = true;
                    break;
                case "SztukiDek":
                case "Szt.Dek":
                    currentRow.SztukiDek = rowAbove.SztukiDek;
                    QueueRowForSave(currentRow.ID);
                    copied = true;
                    break;
            }

            if (copied)
            {
                UpdateStatus($"Ctrl+D: Skopiowano wartosc z wiersza powyzej");
                // Nie potrzeba Items.Refresh() - INotifyPropertyChanged aktualizuje UI
            }
        }

        // === CTRL+SHIFT+D: Zastosuj wartość do wszystkich wierszy tego samego dostawcy ===
        private void ApplyValueToAllRowsOfSupplier()
        {
            var currentRow = selectedRow ?? dataGridView1.SelectedItem as SpecyfikacjaRow;
            if (currentRow == null || string.IsNullOrEmpty(currentRow.RealDostawca)) return;

            var currentColumn = dataGridView1.CurrentColumn;
            if (currentColumn == null) return;

            // Używaj SortMemberPath lub Header
            string columnKey = currentColumn.SortMemberPath ?? currentColumn.Header?.ToString() ?? "";

            // Mapuj na SupplierFieldMask i użyj batch update
            SupplierFieldMask field = SupplierFieldMask.None;
            switch (columnKey)
            {
                case "Cena":
                    field = SupplierFieldMask.Cena;
                    break;
                case "Dodatek":
                    field = SupplierFieldMask.Dodatek;
                    break;
                case "Ubytek":
                case "Ubytek%":
                    field = SupplierFieldMask.Ubytek;
                    break;
                case "TypCeny":
                case "Typ Ceny":
                    field = SupplierFieldMask.TypCeny;
                    break;
                case "TerminDni":
                case "Termin":
                    field = SupplierFieldMask.TerminDni;
                    break;
            }

            if (field != SupplierFieldMask.None)
            {
                // Użyj batch update - jedna operacja SQL
                ApplyFieldsToSupplier(field);
            }
        }

        #region === MENU KONTEKSTOWE: Handlery ===

        private void ContextMenu_CopyFromAbove(object sender, RoutedEventArgs e)
        {
            CopyValueFromCellAbove();
        }

        private void ContextMenu_ApplyCenaToSupplier(object sender, RoutedEventArgs e)
        {
            // Używa batch update - jedna operacja SQL
            ApplyFieldsToSupplier(SupplierFieldMask.Cena);
        }

        private void ContextMenu_ApplyDodatekToSupplier(object sender, RoutedEventArgs e)
        {
            // Używa batch update - jedna operacja SQL
            ApplyFieldsToSupplier(SupplierFieldMask.Dodatek);
        }

        private void ContextMenu_ApplyUbytekToSupplier(object sender, RoutedEventArgs e)
        {
            // Używa batch update - jedna operacja SQL
            ApplyFieldsToSupplier(SupplierFieldMask.Ubytek);
        }

        private void ContextMenu_ApplyTypCenyToSupplier(object sender, RoutedEventArgs e)
        {
            // Używa batch update - jedna operacja SQL
            ApplyFieldsToSupplier(SupplierFieldMask.TypCeny);
        }

        private void ContextMenu_ApplyAllToSupplier(object sender, RoutedEventArgs e)
        {
            // Batch update WSZYSTKICH pól cenowych
            ApplyFieldsToSupplier(SupplierFieldMask.All);
        }

        private void ContextMenu_CopySettings(object sender, RoutedEventArgs e)
        {
            CopyRowSettingsToClipboard();
        }

        private void ContextMenu_PasteSettings(object sender, RoutedEventArgs e)
        {
            PasteRowSettingsFromClipboard();
        }

        private void ContextMenu_PasteSettingsToSupplier(object sender, RoutedEventArgs e)
        {
            PasteRowSettingsToSupplier();
        }

        private void ContextMenu_ApplyTemplate(object sender, RoutedEventArgs e)
        {
            ApplySupplierTemplateToAll();
        }

        private void ContextMenu_RefreshData(object sender, RoutedEventArgs e)
        {
            // Odśwież dane z bazy
            LoadData(dateTimePicker1.SelectedDate ?? DateTime.Today);
            UpdateStatus("Dane odswiezone");
        }

        #endregion

        #region === MENU KONTEKSTOWE: Zdjęcia z ważenia ===

        /// <summary>
        /// Podgląd zdjęcia TARA dla wybranego wiersza
        /// </summary>
        private void ContextMenu_ShowPhotoTara(object sender, RoutedEventArgs e)
        {
            if (selectedRow == null)
            {
                MessageBox.Show("Nie wybrano wiersza.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string title = $"TARA - {selectedRow.Dostawca} (LP: {selectedRow.Nr})";
            PhotoViewerWindow.ShowPhoto(selectedRow.ZdjecieTaraPath, title);
        }

        /// <summary>
        /// Podgląd zdjęcia BRUTTO dla wybranego wiersza
        /// </summary>
        private void ContextMenu_ShowPhotoBrutto(object sender, RoutedEventArgs e)
        {
            if (selectedRow == null)
            {
                MessageBox.Show("Nie wybrano wiersza.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string title = $"BRUTTO - {selectedRow.Dostawca} (LP: {selectedRow.Nr})";
            PhotoViewerWindow.ShowPhoto(selectedRow.ZdjecieBruttoPath, title);
        }

        /// <summary>
        /// Porównanie zdjęć TARA i BRUTTO obok siebie
        /// </summary>
        private void ContextMenu_ShowPhotosCompare(object sender, RoutedEventArgs e)
        {
            if (selectedRow == null)
            {
                MessageBox.Show("Nie wybrano wiersza.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string title = $"{selectedRow.Dostawca} (LP: {selectedRow.Nr})";
            PhotoCompareWindow.ShowComparison(selectedRow.ZdjecieTaraPath, selectedRow.ZdjecieBruttoPath, title);
        }

        #endregion

        #region === TOOLBAR: Handlery przycisków ===

        private void ToolBar_CopySettings(object sender, RoutedEventArgs e)
        {
            CopyRowSettingsToClipboard();
            UpdateClipboardInfo();
        }

        private void ToolBar_PasteSettings(object sender, RoutedEventArgs e)
        {
            PasteRowSettingsFromClipboard();
        }

        private void ToolBar_ApplyAllToSupplier(object sender, RoutedEventArgs e)
        {
            ApplyFieldsToSupplier(SupplierFieldMask.All);
        }

        private void ToolBar_ApplyCenaToSupplier(object sender, RoutedEventArgs e)
        {
            ApplyFieldsToSupplier(SupplierFieldMask.Cena);
        }

        private void ToolBar_ApplyDodatekToSupplier(object sender, RoutedEventArgs e)
        {
            ApplyFieldsToSupplier(SupplierFieldMask.Dodatek);
        }

        private void ToolBar_ApplyUbytekToSupplier(object sender, RoutedEventArgs e)
        {
            ApplyFieldsToSupplier(SupplierFieldMask.Ubytek);
        }

        private void ToolBar_ApplyTypCenyToSupplier(object sender, RoutedEventArgs e)
        {
            ApplyFieldsToSupplier(SupplierFieldMask.TypCeny);
        }

        private void UpdateClipboardInfo()
        {
            if (_supplierClipboard.HasData)
            {
                lblClipboardInfo.Text = $"Schowek: {_supplierClipboard}";
            }
            else
            {
                lblClipboardInfo.Text = "";
            }
        }

        private void ToolBar_ApplyTemplate(object sender, RoutedEventArgs e)
        {
            ApplySupplierTemplateToAll();
        }

        #endregion

        // === NATYCHMIASTOWA EDYCJA: Wpisywanie znaków od razu ===
        private void DataGridView1_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var cellInfo = dataGridView1.CurrentCell;
            if (cellInfo.Column == null) return;

            // Sprawdź czy kolumna jest edytowalna
            bool isEditable = !cellInfo.Column.IsReadOnly;
            if (cellInfo.Column is DataGridTemplateColumn templateColumn)
            {
                isEditable = templateColumn.CellEditingTemplate != null || templateColumn.CellTemplate != null;
            }

            if (!isEditable) return;

            var dataGridCell = GetDataGridCell(cellInfo);
            if (dataGridCell != null && !dataGridCell.IsEditing)
            {
                // Rozpocznij edycję
                dataGridView1.BeginEdit();

                // Wstaw wpisany znak do TextBoxa
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var textBox = FindVisualChild<TextBox>(dataGridCell);
                    if (textBox != null)
                    {
                        textBox.Focus();
                        textBox.Text = e.Text;
                        textBox.CaretIndex = textBox.Text.Length;
                    }
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
        }

        // === SELECTALL: Zaznacz całą zawartość TextBox przy fokusie ===
        private void NumericTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // Zaznacz całą zawartość - wpisanie cyfry zastąpi wartość
                textBox.SelectAll();
            }
        }

        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindVisualParent<T>(parentObject);
        }

        private DataGridCell GetDataGridCell(DataGridCellInfo cellInfo)
        {
            if (cellInfo.IsValid)
            {
                var cellContent = cellInfo.Column.GetCellContent(cellInfo.Item);
                if (cellContent != null)
                {
                    return FindVisualParent<DataGridCell>(cellContent);
                }
            }
            return null;
        }

        // === NATYCHMIASTOWA EDYCJA: Zaznacz całą zawartość komórki przy rozpoczęciu edycji ===
        private void DataGridView1_PreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
        {
            // Znajdź TextBox w edytowanej komórce i zaznacz całą zawartość
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var textBox = FindVisualChild<TextBox>(e.EditingElement);
                if (textBox != null)
                {
                    textBox.Focus();
                    textBox.SelectAll();
                }
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void DataGridView1_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            // Sprawdzamy, czy edycja została zatwierdzona (np. Enterem lub zmianą komórki)
            if (e.EditAction == DataGridEditAction.Commit)
            {
                var row = e.Row.Item as SpecyfikacjaRow;
                if (row != null)
                {
                    // Pobierz nazwę kolumny
                    var columnHeader = e.Column.Header?.ToString() ?? "";
                    var columnBinding = (e.Column as DataGridBoundColumn)?.Binding as System.Windows.Data.Binding;
                    var bindingPath = columnBinding?.Path?.Path ?? columnHeader;

                    // Sprawdź czy to pole produkcyjne i oznacz czas edycji
                    if (TrackedEditColumns.Contains(columnHeader) || TrackedEditColumns.Contains(bindingPath))
                    {
                        MarkProductionFieldEdit(columnHeader);
                    }

                    // WAŻNE: Zapisz wiersz od razu (queuing), ale statystyki odłóż na później
                    int rowId = row.ID;
                    QueueRowForSave(rowId);

                    // Statystyki na NAJNIŻSZYM priorytecie - nie blokują nawigacji
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        UpdateStatistics();
                    }), DispatcherPriority.ApplicationIdle);
                }
            }
        }
        private void UpdateDatabaseRow(SpecyfikacjaRow row, string columnName)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string dbColumnName = GetDatabaseColumnName(columnName);

                    if (string.IsNullOrEmpty(dbColumnName))
                        return;

                    string query = $"UPDATE dbo.FarmerCalc SET {dbColumnName} = @Value WHERE ID = @ID";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@ID", row.ID);

                        object value = GetColumnValue(row, columnName);
                        command.Parameters.AddWithValue("@Value", value ?? DBNull.Value);

                        command.ExecuteNonQuery();
                        UpdateStatus($"Zapisano: {columnName} dla LP {row.Nr}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas aktualizacji:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetDatabaseColumnName(string displayName)
        {
            var mapping = new Dictionary<string, string>
            {
                { "LP", "CarLp" },
                { "Dostawca", "CustomerGID" },
                { "Szt.Dek", "DeclI1" },
                { "Padłe", "DeclI2" },
                { "CH", "DeclI3" },
                { "NW", "DeclI4" },
                { "ZM", "DeclI5" },
                { "LUMEL", "LumQnt" },
                { "Szt.Wyb", "ProdQnt" },
                { "KG Wyb", "ProdWgt" },
                { "Cena", "Price" },
                { "Dodatek", "Addition" },
                { "Typ Ceny", "PriceTypeID" },
                { "PiK", "IncDeadConf" },
                { "Ubytek%", "Loss" },
                // Nowe kolumny
                { "Opas.", "Opasienie" },
                { "K.I.B", "KlasaB" },
                { "Termin", "TerminDni" }
            };

            return mapping.ContainsKey(displayName) ? mapping[displayName] : string.Empty;
        }

        private object GetColumnValue(SpecyfikacjaRow row, string columnName)
        {
            switch (columnName)
            {
                case "LP": return row.Nr;
                case "Dostawca": return row.DostawcaGID;
                case "Szt.Dek": return row.SztukiDek;
                case "Padłe": return row.Padle;
                case "CH": return row.CH;
                case "NW": return row.NW;
                case "ZM": return row.ZM;
                case "LUMEL": return row.LUMEL;
                case "Szt.Wyb": return row.SztukiWybijak;
                case "KG Wyb": return row.KilogramyWybijak;
                case "Cena": return row.Cena;
                case "Dodatek": return row.Dodatek;
                case "Typ Ceny": return zapytaniasql.ZnajdzIdCeny(row.TypCeny ?? "");
                case "PiK": return row.PiK;
                case "Ubytek%": return row.Ubytek / 100; // Konwersja na wartość dla bazy
                // Nowe kolumny
                case "Opas.": return row.Opasienie;
                case "K.I.B": return row.KlasaB;
                case "Termin": return row.TerminDni;
                default: return null;
            }
        }

        private void UpdateStatistics()
        {
            if (specyfikacjeData == null || specyfikacjeData.Count == 0)
            {
                lblRecordCount.Text = "0";
                lblSumaNetto.Text = "0 kg";
                lblSumaSztuk.Text = "0";
                lblSumaDoZaplaty.Text = "0 kg";
                lblSumaWartosc.Text = "0 zł";
                return;
            }

            lblRecordCount.Text = specyfikacjeData.Count.ToString();

            // Suma netto do rozliczenia (preferuje wagę hodowcy jeśli jest)
            decimal sumaNetto = specyfikacjeData.Sum(r => r.WagaNettoDoRozliczenia);
            lblSumaNetto.Text = $"{sumaNetto:N0} kg";

            // Suma sztuk LUMEL
            int sumaSztuk = specyfikacjeData.Sum(r => r.LUMEL);
            lblSumaSztuk.Text = sumaSztuk.ToString("N0");

            // Suma do zapłaty
            decimal sumaDoZaplaty = specyfikacjeData.Sum(r => r.DoZaplaty);
            lblSumaDoZaplaty.Text = $"{sumaDoZaplaty:N0} kg";

            // Suma wartości
            decimal sumaWartosc = specyfikacjeData.Sum(r => r.Wartosc);
            lblSumaWartosc.Text = $"{sumaWartosc:N0} zł";

            // Aktualizuj szybkie statystyki w panelu admina (jeśli istnieją)
            UpdateQuickStats();

            // Aktualizuj wykres słupkowy porównania wag (hodowca vs ubojnia)
            UpdateWeightComparisonChart();
        }

        /// <summary>
        /// Aktualizuje szybkie statystyki w panelu administracyjnym
        /// </summary>
        private void UpdateQuickStats()
        {
            try
            {
                var lblSpec = FindName("lblQuickStatSpec") as TextBlock;
                var lblCena = FindName("lblQuickStatCena") as TextBlock;
                var lblBezCeny = FindName("lblQuickStatBezCeny") as TextBlock;
                var lblSztuki = FindName("lblQuickStatSztuki") as TextBlock;

                if (lblSpec == null) return;

                int total = specyfikacjeData?.Count ?? 0;
                int zCena = specyfikacjeData?.Count(s => s.Cena > 0) ?? 0;
                int bezCeny = total - zCena;
                int sumaSzt = specyfikacjeData?.Sum(s => s.SztukiDek) ?? 0;

                lblSpec.Text = total.ToString();
                lblCena.Text = zCena.ToString();
                lblBezCeny.Text = bezCeny.ToString();
                lblSztuki.Text = sumaSzt.ToString("N0");
            }
            catch { /* Elementy mogą nie istnieć */ }
        }

        /// <summary>
        /// Aktualizuje mini wykres porównania wag hodowcy i ubojni
        /// Pokazuje wykres TYLKO gdy obie wagi są > 0
        /// </summary>
        private void UpdateWeightComparisonChart()
        {
            // Znajdź elementy używając FindName (mogą być w innym TabItem)
            var borderChart = FindName("borderWeightComparison") as Border;
            var barHodowcy = FindName("barWagaHodowcy") as Border;
            var barUbojni = FindName("barWagaUbojni") as Border;
            var lblHodowcy = FindName("lblBarWagaHodowcy") as TextBlock;
            var lblUbojni = FindName("lblBarWagaUbojni") as TextBlock;

            if (specyfikacjeData == null || specyfikacjeData.Count == 0)
            {
                if (borderChart != null) borderChart.Visibility = Visibility.Collapsed;
                return;
            }

            decimal sumaWagaHodowcy = specyfikacjeData.Sum(r => r.NettoHodowcyValue);
            decimal sumaWagaUbojni = specyfikacjeData.Sum(r => r.NettoUbojniValue);

            // Pokaż wykres TYLKO gdy obie wagi są > 0
            if (sumaWagaHodowcy <= 0 || sumaWagaUbojni <= 0)
            {
                if (borderChart != null) borderChart.Visibility = Visibility.Collapsed;
                return;
            }

            // Obie wagi są > 0 - pokaż wykres
            if (borderChart != null) borderChart.Visibility = Visibility.Visible;

            decimal maxWaga = Math.Max(sumaWagaHodowcy, sumaWagaUbojni);

            // Maksymalna szerokość paska (w pikselach)
            const double maxBarWidth = 200;

            double hodowcyWidth = (double)(sumaWagaHodowcy / maxWaga) * maxBarWidth;
            double ubojniWidth = (double)(sumaWagaUbojni / maxWaga) * maxBarWidth;

            if (barHodowcy != null) barHodowcy.Width = Math.Max(3, hodowcyWidth);
            if (barUbojni != null) barUbojni.Width = Math.Max(3, ubojniWidth);

            if (lblHodowcy != null)
                lblHodowcy.Text = $"{sumaWagaHodowcy:N0} kg";
            if (lblUbojni != null)
                lblUbojni.Text = $"{sumaWagaUbojni:N0} kg";

            // Sprawdź niezwykłe wartości i pokaż alerty
            CheckForUnusualValues();
        }

        /// <summary>
        /// Lista problemów wykrytych w danych
        /// </summary>
        private List<string> _currentProblems = new List<string>();

        /// <summary>
        /// Sprawdza dane pod kątem niezwykłych wartości (ubytek, padłe, brakujące dane)
        /// </summary>
        private void CheckForUnusualValues()
        {
            _currentProblems.Clear();

            // Znajdź elementy używając FindName
            var alertsBorder = FindName("borderAlerts") as Border;
            var alertsLabel = FindName("lblAlertCount") as TextBlock;

            if (specyfikacjeData == null || specyfikacjeData.Count == 0)
            {
                if (alertsBorder != null) alertsBorder.Visibility = Visibility.Collapsed;
                return;
            }

            foreach (var row in specyfikacjeData)
            {
                // Sprawdź wysoki ubytek (> 5%)
                if (row.Ubytek > 5)
                {
                    _currentProblems.Add($"⚠️ LP {row.Nr}: Wysoki ubytek {row.Ubytek:F2}% (>5%) - {row.RealDostawca}");
                }

                // Sprawdź bardzo niski ubytek (< -1%)
                if (row.Ubytek < -1)
                {
                    _currentProblems.Add($"❓ LP {row.Nr}: Ujemny ubytek {row.Ubytek:F2}% - sprawdź wagi - {row.RealDostawca}");
                }

                // Sprawdź dużą ilość padłych (> 2% sztuk)
                if (row.SztukiDek > 0 && row.Padle > 0)
                {
                    decimal procentPadlych = (decimal)row.Padle / row.SztukiDek * 100;
                    if (procentPadlych > 2)
                    {
                        _currentProblems.Add($"☠️ LP {row.Nr}: Dużo padłych {row.Padle} ({procentPadlych:F1}%) - {row.RealDostawca}");
                    }
                }

                // Sprawdź brakującą wagę hodowcy
                if (row.NettoHodowcyValue == 0 && row.SztukiDek > 0)
                {
                    _currentProblems.Add($"📝 LP {row.Nr}: Brak wagi hodowcy - {row.RealDostawca}");
                }

                // Sprawdź brakującą wagę ubojni
                if (row.NettoUbojniValue == 0 && row.LUMEL > 0)
                {
                    _currentProblems.Add($"📝 LP {row.Nr}: Brak wagi ubojni - {row.RealDostawca}");
                }

                // Sprawdź brakującą cenę
                if (row.Cena == 0 && row.SztukiDek > 0)
                {
                    _currentProblems.Add($"💰 LP {row.Nr}: Brak ceny - {row.RealDostawca}");
                }

                // Sprawdź duże różnice wag (> 10%)
                if (row.NettoHodowcyValue > 0 && row.NettoUbojniValue > 0)
                {
                    decimal roznicaProcent = Math.Abs((row.NettoHodowcyValue - row.NettoUbojniValue) / row.NettoHodowcyValue * 100);
                    if (roznicaProcent > 10)
                    {
                        _currentProblems.Add($"⚖️ LP {row.Nr}: Duża różnica wag {roznicaProcent:F1}% - {row.RealDostawca}");
                    }
                }
            }

            // Aktualizuj panel alertów
            if (alertsBorder != null)
            {
                if (_currentProblems.Count > 0)
                {
                    alertsBorder.Visibility = Visibility.Visible;
                    if (alertsLabel != null)
                        alertsLabel.Text = $"{_currentProblems.Count} problem{(_currentProblems.Count == 1 ? "" : _currentProblems.Count < 5 ? "y" : "ów")}";

                    // Zmień kolor w zależności od ilości problemów
                    if (_currentProblems.Count >= 5)
                    {
                        alertsBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFEBEE")); // czerwone
                        if (alertsLabel != null)
                            alertsLabel.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C62828"));
                    }
                    else
                    {
                        alertsBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3E0")); // pomarańczowe
                        if (alertsLabel != null)
                            alertsLabel.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E65100"));
                    }
                }
                else
                {
                    alertsBorder.Visibility = Visibility.Collapsed;
                }
            }
        }

        /// <summary>
        /// Kliknięcie na licznik alertów - pokazuje szczegóły problemów
        /// </summary>
        private void LblAlertCount_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_currentProblems.Count == 0)
            {
                MessageBox.Show("Brak wykrytych problemów.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string message = "Wykryte problemy:\n\n" + string.Join("\n", _currentProblems.Take(20));
            if (_currentProblems.Count > 20)
            {
                message += $"\n\n... i {_currentProblems.Count - 20} więcej problemów";
            }

            message += "\n\n💡 Wskazówki:\n";
            message += "• Wysoki ubytek może oznaczać błędne wagi lub problemy jakościowe\n";
            message += "• Ujemny ubytek sugeruje błąd w ważeniu\n";
            message += "• Duża ilość padłych wymaga weryfikacji jakości partii";

            MessageBox.Show(message, $"Panel problemów ({_currentProblems.Count})", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        /// <summary>
        /// Generuje raport podsumowania dnia
        /// </summary>
        private void BtnRaportDnia_Click(object sender, RoutedEventArgs e)
        {
            if (specyfikacjeData == null || specyfikacjeData.Count == 0)
            {
                MessageBox.Show("Brak danych do raportu.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════");
            sb.AppendLine("        RAPORT PODSUMOWANIA DNIA");
            sb.AppendLine($"        {dateTimePicker1.SelectedDate:yyyy-MM-dd}");
            sb.AppendLine("═══════════════════════════════════════════\n");

            sb.AppendLine($"📦 Liczba specyfikacji: {specyfikacjeData.Count}");
            sb.AppendLine($"🐓 Suma sztuk (LUMEL): {specyfikacjeData.Sum(r => r.LUMEL):N0}");
            sb.AppendLine($"⚖️ Suma wagi hodowców: {specyfikacjeData.Sum(r => r.NettoHodowcyValue):N0} kg");
            sb.AppendLine($"⚖️ Suma wagi ubojni: {specyfikacjeData.Sum(r => r.NettoUbojniValue):N0} kg");
            sb.AppendLine($"💰 Suma wartość: {specyfikacjeData.Sum(r => r.Wartosc):N2} zł\n");

            // Statystyki per dostawca
            var perDostawca = specyfikacjeData
                .GroupBy(r => r.RealDostawca ?? r.Dostawca)
                .OrderByDescending(g => g.Sum(r => r.LUMEL))
                .Take(10);

            sb.AppendLine("─────────────────────────────────────────");
            sb.AppendLine("TOP 10 DOSTAWCÓW:");
            sb.AppendLine("─────────────────────────────────────────");

            foreach (var g in perDostawca)
            {
                sb.AppendLine($"  {g.Key}: {g.Sum(r => r.LUMEL):N0} szt, {g.Sum(r => r.NettoUbojniValue):N0} kg");
            }

            if (_currentProblems.Count > 0)
            {
                sb.AppendLine($"\n⚠️ UWAGA: Wykryto {_currentProblems.Count} problemów do sprawdzenia!");
            }

            MessageBox.Show(sb.ToString(), "Raport dnia", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Generuje raport dla wybranego dostawcy
        /// </summary>
        private void BtnRaportDostawcy_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridView1.SelectedCells.Count == 0)
            {
                MessageBox.Show("Wybierz wiersz dostawcy w tabeli.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var selectedRow = dataGridView1.SelectedCells[0].Item as SpecyfikacjaRow;
            if (selectedRow == null) return;

            string dostawca = selectedRow.RealDostawca ?? selectedRow.Dostawca;
            var wiersze = specyfikacjeData.Where(r => (r.RealDostawca ?? r.Dostawca) == dostawca).ToList();

            if (wiersze.Count == 0)
            {
                MessageBox.Show($"Brak danych dla dostawcy: {dostawca}", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════");
            sb.AppendLine("        RAPORT DOSTAWCY");
            sb.AppendLine($"        {dostawca}");
            sb.AppendLine($"        Data: {dateTimePicker1.SelectedDate:yyyy-MM-dd}");
            sb.AppendLine("═══════════════════════════════════════════\n");

            sb.AppendLine($"📦 Liczba specyfikacji: {wiersze.Count}");
            sb.AppendLine($"🐓 Suma sztuk (deklaracja): {wiersze.Sum(r => r.SztukiDek):N0}");
            sb.AppendLine($"🐓 Suma sztuk (LUMEL): {wiersze.Sum(r => r.LUMEL):N0}");
            sb.AppendLine($"☠️ Suma padłych: {wiersze.Sum(r => r.Padle)}");
            sb.AppendLine($"⚖️ Waga hodowcy: {wiersze.Sum(r => r.NettoHodowcyValue):N0} kg");
            sb.AppendLine($"⚖️ Waga ubojni: {wiersze.Sum(r => r.NettoUbojniValue):N0} kg");
            sb.AppendLine($"📉 Średni ubytek: {wiersze.Average(r => r.Ubytek):F2}%");
            sb.AppendLine($"💰 Średnia cena: {wiersze.Average(r => r.Cena):N2} zł/kg");
            sb.AppendLine($"💰 Suma wartość: {wiersze.Sum(r => r.Wartosc):N2} zł\n");

            sb.AppendLine("─────────────────────────────────────────");
            sb.AppendLine("SZCZEGÓŁY SPECYFIKACJI:");
            sb.AppendLine("─────────────────────────────────────────");

            foreach (var r in wiersze)
            {
                sb.AppendLine($"  LP {r.Nr}: {r.LUMEL} szt, {r.NettoUbojniValue:N0} kg, {r.Cena:N2} zł/kg, ubytek {r.Ubytek:F2}%");
            }

            MessageBox.Show(sb.ToString(), $"Raport: {dostawca}", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Zwijanie/rozwijanie harmonogramu dostaw
        /// </summary>
        private bool _isHarmonogramCollapsed = false;

        private void BtnToggleHarmonogram_Click(object sender, RoutedEventArgs e)
        {
            _isHarmonogramCollapsed = !_isHarmonogramCollapsed;

            // Znajdź elementy używając FindName
            var harmonogramGrid = FindName("gridHarmonogramContent") as Grid;
            var toggleBtn = sender as Button;

            if (_isHarmonogramCollapsed)
            {
                // Zwiń harmonogram
                if (harmonogramGrid != null) harmonogramGrid.Visibility = Visibility.Collapsed;
                if (toggleBtn != null)
                {
                    toggleBtn.Content = "▲";
                    toggleBtn.ToolTip = "Rozwiń harmonogram";
                }
            }
            else
            {
                // Rozwiń harmonogram
                if (harmonogramGrid != null) harmonogramGrid.Visibility = Visibility.Visible;
                if (toggleBtn != null)
                {
                    toggleBtn.Content = "▼";
                    toggleBtn.ToolTip = "Zwiń harmonogram";
                }
            }
        }

        private void BtnSaveAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateStatus("Zapisywanie wszystkich zmian...");

                // Użyj batch update zamiast pętli - jedna transakcja SQL
                var allIds = specyfikacjeData.Select(r => r.ID).ToList();
                if (allIds.Count > 0)
                {
                    SaveRowsBatch(allIds);
                    MessageBox.Show($"Zapisano {allIds.Count} rekordów.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                    UpdateStatus($"Batch: Zapisano {allIds.Count} rekordów");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zapisywania:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatus("Błąd zapisu");
            }
        }

        private void SaveRowToDatabase(SpecyfikacjaRow row)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // Znajdź ID typu ceny
                int priceTypeId = -1;
                if (!string.IsNullOrEmpty(row.TypCeny))
                {
                    priceTypeId = zapytaniasql.ZnajdzIdCeny(row.TypCeny);
                }

                string query = @"UPDATE dbo.FarmerCalc SET
                    CarLp = @Nr,
                    CustomerGID = @DostawcaGID,
                    DeclI1 = @SztukiDek,
                    DeclI2 = @Padle,
                    DeclI3 = @CH,
                    DeclI4 = @NW,
                    DeclI5 = @ZM,
                    LumQnt = @LUMEL,
                    ProdQnt = @SztukiWybijak,
                    ProdWgt = @KgWybijak,
                    Price = @Cena,
                    Addition = @Dodatek,
                    PriceTypeID = @PriceTypeID,
                    Loss = @Ubytek,
                    IncDeadConf = @PiK,
                    Opasienie = @Opasienie,
                    KlasaB = @KlasaB,
                    TerminDni = @TerminDni,
                    AvgWgt = @AvgWgt,
                    PayWgt = @PayWgt,
                    PayNet = @PayNet
                    WHERE ID = @ID";

                using (SqlCommand cmd = new SqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@ID", row.ID);
                    cmd.Parameters.AddWithValue("@Nr", row.Nr);
                    cmd.Parameters.AddWithValue("@DostawcaGID", (object)row.DostawcaGID ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SztukiDek", row.SztukiDek);
                    cmd.Parameters.AddWithValue("@Padle", row.Padle);
                    cmd.Parameters.AddWithValue("@CH", row.CH);
                    cmd.Parameters.AddWithValue("@NW", row.NW);
                    cmd.Parameters.AddWithValue("@ZM", row.ZM);
                    cmd.Parameters.AddWithValue("@LUMEL", row.LUMEL);
                    cmd.Parameters.AddWithValue("@SztukiWybijak", row.SztukiWybijak);
                    cmd.Parameters.AddWithValue("@KgWybijak", row.KilogramyWybijak);
                    cmd.Parameters.AddWithValue("@Cena", row.Cena);
                    cmd.Parameters.AddWithValue("@Dodatek", row.Dodatek);
                    cmd.Parameters.AddWithValue("@PriceTypeID", priceTypeId > 0 ? priceTypeId : (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Ubytek", row.Ubytek / 100); // Konwertuj procent na ułamek
                    cmd.Parameters.AddWithValue("@PiK", row.PiK);
                    // Nowe pola
                    cmd.Parameters.AddWithValue("@Opasienie", row.Opasienie);
                    cmd.Parameters.AddWithValue("@KlasaB", row.KlasaB);
                    cmd.Parameters.AddWithValue("@TerminDni", row.TerminDni);
                    cmd.Parameters.AddWithValue("@AvgWgt", row.SredniaWaga);
                    cmd.Parameters.AddWithValue("@PayWgt", row.DoZaplaty);
                    cmd.Parameters.AddWithValue("@PayNet", row.Wartosc);

                    cmd.ExecuteNonQuery();
                }
            }
        }

        // === WYDAJNOŚĆ: Batch zapis wielu wierszy w jednej transakcji ===
        private void SaveRowsBatch(List<int> rowIds)
        {
            if (rowIds == null || rowIds.Count == 0) return;

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            foreach (var id in rowIds)
                            {
                                var row = specyfikacjeData.FirstOrDefault(r => r.ID == id);
                                if (row == null) continue;

                                // Znajdź ID typu ceny
                                int priceTypeId = -1;
                                if (!string.IsNullOrEmpty(row.TypCeny))
                                {
                                    priceTypeId = zapytaniasql.ZnajdzIdCeny(row.TypCeny);
                                }

                                string query = @"UPDATE dbo.FarmerCalc SET
                                    CarLp = @Nr,
                                    CustomerGID = @DostawcaGID,
                                    DeclI1 = @SztukiDek,
                                    DeclI2 = @Padle,
                                    DeclI3 = @CH,
                                    DeclI4 = @NW,
                                    DeclI5 = @ZM,
                                    LumQnt = @LUMEL,
                                    ProdQnt = @SztukiWybijak,
                                    ProdWgt = @KgWybijak,
                                    Price = @Cena,
                                    Addition = @Dodatek,
                                    PriceTypeID = @PriceTypeID,
                                    Loss = @Ubytek,
                                    IncDeadConf = @PiK,
                                    Opasienie = @Opasienie,
                                    KlasaB = @KlasaB,
                                    TerminDni = @TerminDni,
                                    AvgWgt = @AvgWgt,
                                    PayWgt = @PayWgt,
                                    PayNet = @PayNet
                                    WHERE ID = @ID";

                                using (SqlCommand cmd = new SqlCommand(query, connection, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@ID", row.ID);
                                    cmd.Parameters.AddWithValue("@Nr", row.Nr);
                                    cmd.Parameters.AddWithValue("@DostawcaGID", (object)row.DostawcaGID ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@SztukiDek", row.SztukiDek);
                                    cmd.Parameters.AddWithValue("@Padle", row.Padle);
                                    cmd.Parameters.AddWithValue("@CH", row.CH);
                                    cmd.Parameters.AddWithValue("@NW", row.NW);
                                    cmd.Parameters.AddWithValue("@ZM", row.ZM);
                                    cmd.Parameters.AddWithValue("@LUMEL", row.LUMEL);
                                    cmd.Parameters.AddWithValue("@SztukiWybijak", row.SztukiWybijak);
                                    cmd.Parameters.AddWithValue("@KgWybijak", row.KilogramyWybijak);
                                    cmd.Parameters.AddWithValue("@Cena", row.Cena);
                                    cmd.Parameters.AddWithValue("@Dodatek", row.Dodatek);
                                    cmd.Parameters.AddWithValue("@PriceTypeID", priceTypeId > 0 ? priceTypeId : (object)DBNull.Value);
                                    cmd.Parameters.AddWithValue("@Ubytek", row.Ubytek / 100);
                                    cmd.Parameters.AddWithValue("@PiK", row.PiK);
                                    cmd.Parameters.AddWithValue("@Opasienie", row.Opasienie);
                                    cmd.Parameters.AddWithValue("@KlasaB", row.KlasaB);
                                    cmd.Parameters.AddWithValue("@TerminDni", row.TerminDni);
                                    cmd.Parameters.AddWithValue("@AvgWgt", row.SredniaWaga);
                                    cmd.Parameters.AddWithValue("@PayWgt", row.DoZaplaty);
                                    cmd.Parameters.AddWithValue("@PayNet", row.Wartosc);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            transaction.Commit();
                            UpdateStatus($"Zapisano {rowIds.Count} zmian");

                            // Oznacz zakończenie zapisu dla timera
                            MarkSaveCompleted();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Błąd batch zapisu: {ex.Message}");
                // Oznacz zakończenie nawet przy błędzie
                MarkSaveCompleted();
            }
        }

        // === WYDAJNOŚĆ: ASYNCHRONICZNY Batch zapis wielu wierszy - NIE BLOKUJE UI ===
        // Użytkownik może od razu kontynuować wprowadzanie danych, zapis wykonuje się w tle
        private async Task SaveRowsBatchAsync(List<int> rowIds)
        {
            if (rowIds == null || rowIds.Count == 0) return;

            // Skopiuj dane przed uruchomieniem w tle (thread safety)
            var rowsToSave = new List<(int ID, int Nr, string DostawcaGID, int SztukiDek, int Padle, int CH, int NW, int ZM,
                int LUMEL, int SztukiWybijak, decimal KilogramyWybijak, decimal Cena, decimal Dodatek, string TypCeny,
                decimal Ubytek, bool PiK, decimal Opasienie, decimal KlasaB, int TerminDni, decimal SredniaWaga,
                decimal DoZaplaty, decimal Wartosc, int? IdPosrednik)>();

            foreach (var id in rowIds)
            {
                var row = specyfikacjeData.FirstOrDefault(r => r.ID == id);
                if (row != null)
                {
                    rowsToSave.Add((row.ID, row.Nr, row.DostawcaGID, row.SztukiDek, row.Padle, row.CH, row.NW, row.ZM,
                        row.LUMEL, row.SztukiWybijak, row.KilogramyWybijak, row.Cena, row.Dodatek, row.TypCeny,
                        row.Ubytek, row.PiK, row.Opasienie, row.KlasaB, row.TerminDni, row.SredniaWaga,
                        row.DoZaplaty, row.Wartosc, row.IdPosrednik));
                }
            }

            if (rowsToSave.Count == 0) return;

            try
            {
                // Uruchom zapis w tle - NIE BLOKUJE UI
                await Task.Run(() =>
                {
                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        connection.Open();
                        using (var transaction = connection.BeginTransaction())
                        {
                            try
                            {
                                foreach (var row in rowsToSave)
                                {
                                    // Znajdź ID typu ceny
                                    int priceTypeId = -1;
                                    if (!string.IsNullOrEmpty(row.TypCeny))
                                    {
                                        priceTypeId = zapytaniasql.ZnajdzIdCeny(row.TypCeny);
                                    }

                                    string query = @"UPDATE dbo.FarmerCalc SET
                                        CarLp = @Nr,
                                        CustomerGID = @DostawcaGID,
                                        DeclI1 = @SztukiDek,
                                        DeclI2 = @Padle,
                                        DeclI3 = @CH,
                                        DeclI4 = @NW,
                                        DeclI5 = @ZM,
                                        LumQnt = @LUMEL,
                                        ProdQnt = @SztukiWybijak,
                                        ProdWgt = @KgWybijak,
                                        Price = @Cena,
                                        Addition = @Dodatek,
                                        PriceTypeID = @PriceTypeID,
                                        Loss = @Ubytek,
                                        IncDeadConf = @PiK,
                                        Opasienie = @Opasienie,
                                        KlasaB = @KlasaB,
                                        TerminDni = @TerminDni,
                                        AvgWgt = @AvgWgt,
                                        PayWgt = @PayWgt,
                                        PayNet = @PayNet,
                                        IdPosrednik = @IdPosrednik
                                        WHERE ID = @ID";

                                    using (SqlCommand cmd = new SqlCommand(query, connection, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@ID", row.ID);
                                        cmd.Parameters.AddWithValue("@Nr", row.Nr);
                                        cmd.Parameters.AddWithValue("@DostawcaGID", (object)row.DostawcaGID ?? DBNull.Value);
                                        cmd.Parameters.AddWithValue("@SztukiDek", row.SztukiDek);
                                        cmd.Parameters.AddWithValue("@Padle", row.Padle);
                                        cmd.Parameters.AddWithValue("@CH", row.CH);
                                        cmd.Parameters.AddWithValue("@NW", row.NW);
                                        cmd.Parameters.AddWithValue("@ZM", row.ZM);
                                        cmd.Parameters.AddWithValue("@LUMEL", row.LUMEL);
                                        cmd.Parameters.AddWithValue("@SztukiWybijak", row.SztukiWybijak);
                                        cmd.Parameters.AddWithValue("@KgWybijak", row.KilogramyWybijak);
                                        cmd.Parameters.AddWithValue("@Cena", row.Cena);
                                        cmd.Parameters.AddWithValue("@Dodatek", row.Dodatek);
                                        cmd.Parameters.AddWithValue("@PriceTypeID", priceTypeId > 0 ? priceTypeId : (object)DBNull.Value);
                                        cmd.Parameters.AddWithValue("@Ubytek", row.Ubytek / 100);
                                        cmd.Parameters.AddWithValue("@PiK", row.PiK);
                                        cmd.Parameters.AddWithValue("@Opasienie", row.Opasienie);
                                        cmd.Parameters.AddWithValue("@KlasaB", row.KlasaB);
                                        cmd.Parameters.AddWithValue("@TerminDni", row.TerminDni);
                                        cmd.Parameters.AddWithValue("@AvgWgt", row.SredniaWaga);
                                        cmd.Parameters.AddWithValue("@PayWgt", row.DoZaplaty);
                                        cmd.Parameters.AddWithValue("@PayNet", row.Wartosc);
                                        cmd.Parameters.AddWithValue("@IdPosrednik", (object)row.IdPosrednik ?? DBNull.Value);
                                        cmd.ExecuteNonQuery();
                                    }
                                }

                                transaction.Commit();
                            }
                            catch
                            {
                                transaction.Rollback();
                                throw;
                            }
                        }
                    }
                });

                // Aktualizuj UI po zakończeniu zapisu (z powrotem w głównym wątku)
                UpdateStatus($"✓ Zapisano {rowsToSave.Count} zmian w tle");
                MarkSaveCompleted();
            }
            catch (Exception ex)
            {
                UpdateStatus($"Błąd async batch zapisu: {ex.Message}");
                MarkSaveCompleted();
            }
        }

        // === ASYNCHRONICZNY zapis pojedynczego pola - NIE BLOKUJE UI ===
        private async Task SaveFieldToDatabaseAsync(int id, string columnName, object value)
        {
            try
            {
                await Task.Run(() =>
                {
                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        connection.Open();
                        string query = $"UPDATE dbo.FarmerCalc SET {columnName} = @Value WHERE ID = @ID";
                        using (SqlCommand cmd = new SqlCommand(query, connection))
                        {
                            cmd.Parameters.AddWithValue("@ID", id);
                            cmd.Parameters.AddWithValue("@Value", value ?? DBNull.Value);
                            cmd.ExecuteNonQuery();
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => UpdateStatus($"Błąd async zapisu: {ex.Message}"));
            }
        }

        // === BATCH: Zapis wybranych pól dla wielu wierszy w jednym zapytaniu SQL ===
        private void SaveSupplierFieldsBatch(List<int> rowIds, SupplierFieldMask fields,
            decimal? cena = null, decimal? dodatek = null, decimal? ubytek = null,
            string typCeny = null, int? terminDni = null, bool? piK = null)
        {
            if (rowIds == null || rowIds.Count == 0 || fields == SupplierFieldMask.None) return;

            var setClauses = new List<string>();
            if (fields.HasFlag(SupplierFieldMask.Cena)) setClauses.Add("Price = @Cena");
            if (fields.HasFlag(SupplierFieldMask.Dodatek)) setClauses.Add("Addition = @Dodatek");
            if (fields.HasFlag(SupplierFieldMask.Ubytek)) setClauses.Add("Loss = @Ubytek");
            if (fields.HasFlag(SupplierFieldMask.TypCeny)) setClauses.Add("PriceTypeID = @PriceTypeID");
            if (fields.HasFlag(SupplierFieldMask.TerminDni)) setClauses.Add("TerminDni = @TerminDni");
            if (fields.HasFlag(SupplierFieldMask.PiK)) setClauses.Add("IncDeadConf = @PiK");

            if (setClauses.Count == 0) return;

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Jedna operacja UPDATE z WHERE ID IN (...)
                    string idList = string.Join(",", rowIds);
                    string query = $"UPDATE dbo.FarmerCalc SET {string.Join(", ", setClauses)} WHERE ID IN ({idList})";

                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        if (fields.HasFlag(SupplierFieldMask.Cena))
                            cmd.Parameters.AddWithValue("@Cena", cena ?? 0m);
                        if (fields.HasFlag(SupplierFieldMask.Dodatek))
                            cmd.Parameters.AddWithValue("@Dodatek", dodatek ?? 0m);
                        if (fields.HasFlag(SupplierFieldMask.Ubytek))
                            cmd.Parameters.AddWithValue("@Ubytek", (ubytek ?? 0m) / 100m);
                        if (fields.HasFlag(SupplierFieldMask.TypCeny))
                        {
                            // Konwertuj nazwę typu ceny na ID
                            int priceTypeId = -1;
                            if (!string.IsNullOrEmpty(typCeny))
                            {
                                priceTypeId = zapytaniasql.ZnajdzIdCeny(typCeny);
                            }
                            cmd.Parameters.AddWithValue("@PriceTypeID", priceTypeId > 0 ? priceTypeId : (object)DBNull.Value);
                        }
                        if (fields.HasFlag(SupplierFieldMask.TerminDni))
                            cmd.Parameters.AddWithValue("@TerminDni", terminDni ?? 0);
                        if (fields.HasFlag(SupplierFieldMask.PiK))
                            cmd.Parameters.AddWithValue("@PiK", piK ?? false);

                        int affected = cmd.ExecuteNonQuery();
                        UpdateStatus($"Batch: Zaktualizowano {affected} wierszy w bazie danych");
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Błąd batch zapisu pól: {ex.Message}");
            }
        }

        // === BATCH: Zastosuj wybrane pola do wszystkich wierszy dostawcy ===
        private void ApplyFieldsToSupplier(SupplierFieldMask fields, SpecyfikacjaRow sourceRow = null)
        {
            var currentRow = sourceRow ?? selectedRow ?? dataGridView1.SelectedItem as SpecyfikacjaRow;
            if (currentRow == null || string.IsNullOrEmpty(currentRow.RealDostawca)) return;

            var rowsToUpdate = specyfikacjeData.Where(x => x.RealDostawca == currentRow.RealDostawca).ToList();
            var idsToUpdate = rowsToUpdate.Select(r => r.ID).ToList();

            if (idsToUpdate.Count == 0) return;

            // Zapisz stare wartości przed aktualizacją i loguj zmiany do bazy
            foreach (var row in rowsToUpdate)
            {
                // Loguj zmiany dla każdego pola które się zmienia
                if (fields.HasFlag(SupplierFieldMask.Cena) && row.Cena != currentRow.Cena)
                {
                    LogChangeToDatabase(row.ID, "Cena", row.Cena.ToString("F2"), currentRow.Cena.ToString("F2"),
                        row.RealDostawca ?? row.Dostawca, row.Nr, row.CarID ?? "");
                }
                if (fields.HasFlag(SupplierFieldMask.Dodatek) && row.Dodatek != currentRow.Dodatek)
                {
                    LogChangeToDatabase(row.ID, "Dodatek", row.Dodatek.ToString("F2"), currentRow.Dodatek.ToString("F2"),
                        row.RealDostawca ?? row.Dostawca, row.Nr, row.CarID ?? "");
                }
                if (fields.HasFlag(SupplierFieldMask.Ubytek) && row.Ubytek != currentRow.Ubytek)
                {
                    LogChangeToDatabase(row.ID, "Ubytek", row.Ubytek.ToString("F2") + "%", currentRow.Ubytek.ToString("F2") + "%",
                        row.RealDostawca ?? row.Dostawca, row.Nr, row.CarID ?? "");
                }
                if (fields.HasFlag(SupplierFieldMask.TypCeny) && row.TypCeny != currentRow.TypCeny)
                {
                    LogChangeToDatabase(row.ID, "PriceTypeID", row.TypCeny ?? "", currentRow.TypCeny ?? "",
                        row.RealDostawca ?? row.Dostawca, row.Nr, row.CarID ?? "");
                }
                if (fields.HasFlag(SupplierFieldMask.TerminDni) && row.TerminDni != currentRow.TerminDni)
                {
                    LogChangeToDatabase(row.ID, "TerminDni", row.TerminDni.ToString(), currentRow.TerminDni.ToString(),
                        row.RealDostawca ?? row.Dostawca, row.Nr, row.CarID ?? "");
                }
                if (fields.HasFlag(SupplierFieldMask.PiK) && row.PiK != currentRow.PiK)
                {
                    LogChangeToDatabase(row.ID, "IncDeadConf", row.PiK ? "TAK" : "NIE", currentRow.PiK ? "TAK" : "NIE",
                        row.RealDostawca ?? row.Dostawca, row.Nr, row.CarID ?? "");
                }

                // Aktualizuj UI
                if (fields.HasFlag(SupplierFieldMask.Cena)) row.Cena = currentRow.Cena;
                if (fields.HasFlag(SupplierFieldMask.Dodatek)) row.Dodatek = currentRow.Dodatek;
                if (fields.HasFlag(SupplierFieldMask.Ubytek)) row.Ubytek = currentRow.Ubytek;
                if (fields.HasFlag(SupplierFieldMask.TypCeny)) row.TypCeny = currentRow.TypCeny;
                if (fields.HasFlag(SupplierFieldMask.TerminDni)) row.TerminDni = currentRow.TerminDni;
                if (fields.HasFlag(SupplierFieldMask.PiK)) row.PiK = currentRow.PiK;
            }

            // Batch zapis do bazy
            SaveSupplierFieldsBatch(idsToUpdate, fields,
                currentRow.Cena, currentRow.Dodatek, currentRow.Ubytek,
                currentRow.TypCeny, currentRow.TerminDni, currentRow.PiK);

            // Status message
            var fieldNames = new List<string>();
            if (fields.HasFlag(SupplierFieldMask.Cena)) fieldNames.Add($"Cena={currentRow.Cena:F2}");
            if (fields.HasFlag(SupplierFieldMask.Dodatek)) fieldNames.Add($"Dodatek={currentRow.Dodatek:F2}");
            if (fields.HasFlag(SupplierFieldMask.Ubytek)) fieldNames.Add($"Ubytek={currentRow.Ubytek:F2}%");
            if (fields.HasFlag(SupplierFieldMask.TypCeny)) fieldNames.Add($"TypCeny={currentRow.TypCeny}");
            if (fields.HasFlag(SupplierFieldMask.TerminDni)) fieldNames.Add($"Termin={currentRow.TerminDni}dni");
            if (fields.HasFlag(SupplierFieldMask.PiK)) fieldNames.Add($"PiK={currentRow.PiK}");

            UpdateStatus($"Batch: {string.Join(", ", fieldNames)} -> {rowsToUpdate.Count} wierszy ({currentRow.RealDostawca})");
        }

        // === BATCH: Zastosuj WSZYSTKIE pola cenowe do dostawcy ===
        private void ApplyAllFieldsToSupplier()
        {
            ApplyFieldsToSupplier(SupplierFieldMask.All);
        }

        #region === HISTORIA ZMIAN ===

        /// <summary>
        /// Zapisuje zmianę do bazy danych FarmerCalcChangeLog
        /// </summary>
        // WERSJA NIEBLOKUJĄCA - logowanie zmian w tle
        private void LogChangeToDatabase(int recordId, string fieldName, string oldValue, string newValue, string dostawca = "", int nr = 0, string carId = "")
        {
            // Nie loguj jeśli wartości są takie same
            if (oldValue == newValue) return;

            // Pobierz wartości UI PRZED uruchomieniem w tle
            DateTime calcDate = dateTimePicker1.SelectedDate ?? DateTime.Today;
            string userId = App.UserID ?? "";
            string connStr = connectionString;

            // Dodaj do lokalnej listy natychmiast (UI thread)
            _changeLog.Add(new ChangeLogEntry
            {
                Timestamp = DateTime.Now,
                RowId = recordId,
                PropertyName = fieldName,
                OldValue = oldValue,
                NewValue = newValue,
                UserName = userId ?? Environment.UserName
            });

            // Fire-and-forget: zapis do bazy w tle
            _ = Task.Run(() =>
            {
                try
                {
                    using (SqlConnection conn = new SqlConnection(connStr))
                    {
                        conn.Open();

                        // Upewnij się, że tabela istnieje z właściwą strukturą
                        string createTableSql = @"
                            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FarmerCalcChangeLog')
                            BEGIN
                                CREATE TABLE [dbo].[FarmerCalcChangeLog] (
                                    [ID] INT IDENTITY(1,1) PRIMARY KEY,
                                    [FarmerCalcID] INT NULL,
                                    [FieldName] NVARCHAR(100) NULL,
                                    [OldValue] NVARCHAR(500) NULL,
                                    [NewValue] NVARCHAR(500) NULL,
                                    [Dostawca] NVARCHAR(200) NULL,
                                    [ChangedBy] NVARCHAR(100) NULL,
                                    [UserID] NVARCHAR(50) NULL,
                                    [Nr] INT NULL,
                                    [CarID] NVARCHAR(50) NULL,
                                    [ChangeDate] DATETIME DEFAULT GETDATE(),
                                    [CalcDate] DATE NULL
                                )
                            END";

                        using (SqlCommand cmd = new SqlCommand(createTableSql, conn))
                        {
                            cmd.ExecuteNonQuery();
                        }

                        // Dodaj brakujące kolumny pojedynczo
                        string[] alterCommands = new string[]
                        {
                            "IF COL_LENGTH('FarmerCalcChangeLog', 'FarmerCalcID') IS NULL ALTER TABLE [dbo].[FarmerCalcChangeLog] ADD [FarmerCalcID] INT NULL",
                            "IF COL_LENGTH('FarmerCalcChangeLog', 'FieldName') IS NULL ALTER TABLE [dbo].[FarmerCalcChangeLog] ADD [FieldName] NVARCHAR(100) NULL",
                            "IF COL_LENGTH('FarmerCalcChangeLog', 'OldValue') IS NULL ALTER TABLE [dbo].[FarmerCalcChangeLog] ADD [OldValue] NVARCHAR(500) NULL",
                            "IF COL_LENGTH('FarmerCalcChangeLog', 'NewValue') IS NULL ALTER TABLE [dbo].[FarmerCalcChangeLog] ADD [NewValue] NVARCHAR(500) NULL",
                            "IF COL_LENGTH('FarmerCalcChangeLog', 'Dostawca') IS NULL ALTER TABLE [dbo].[FarmerCalcChangeLog] ADD [Dostawca] NVARCHAR(200) NULL",
                            "IF COL_LENGTH('FarmerCalcChangeLog', 'ChangedBy') IS NULL ALTER TABLE [dbo].[FarmerCalcChangeLog] ADD [ChangedBy] NVARCHAR(100) NULL",
                            "IF COL_LENGTH('FarmerCalcChangeLog', 'UserID') IS NULL ALTER TABLE [dbo].[FarmerCalcChangeLog] ADD [UserID] NVARCHAR(50) NULL",
                            "IF COL_LENGTH('FarmerCalcChangeLog', 'Nr') IS NULL ALTER TABLE [dbo].[FarmerCalcChangeLog] ADD [Nr] INT NULL",
                            "IF COL_LENGTH('FarmerCalcChangeLog', 'CarID') IS NULL ALTER TABLE [dbo].[FarmerCalcChangeLog] ADD [CarID] NVARCHAR(50) NULL",
                            "IF COL_LENGTH('FarmerCalcChangeLog', 'ChangeDate') IS NULL ALTER TABLE [dbo].[FarmerCalcChangeLog] ADD [ChangeDate] DATETIME NULL",
                            "IF COL_LENGTH('FarmerCalcChangeLog', 'CalcDate') IS NULL ALTER TABLE [dbo].[FarmerCalcChangeLog] ADD [CalcDate] DATE NULL"
                        };

                        foreach (var alterCmd in alterCommands)
                        {
                            try
                            {
                                using (SqlCommand cmd = new SqlCommand(alterCmd, conn))
                                {
                                    cmd.ExecuteNonQuery();
                                }
                            }
                            catch { /* Ignoruj błędy ALTER */ }
                        }

                        // Pobierz nazwę użytkownika
                        string userName = Environment.UserName;
                        if (!string.IsNullOrEmpty(userId))
                        {
                            try
                            {
                                NazwaZiD nazwaZiD = new NazwaZiD();
                                userName = nazwaZiD.GetNameById(userId) ?? userName;
                            }
                            catch { }
                        }

                        // Zapisz zmianę
                        string sql = @"INSERT INTO [dbo].[FarmerCalcChangeLog]
                            (FarmerCalcID, FieldName, OldValue, NewValue, Dostawca, ChangedBy, UserID, Nr, CarID, ChangeDate, CalcDate)
                            VALUES (@FarmerCalcID, @FieldName, @OldValue, @NewValue, @Dostawca, @ChangedBy, @UserID, @Nr, @CarID, GETDATE(), @CalcDate)";

                        using (SqlCommand cmd = new SqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@FarmerCalcID", recordId);
                            cmd.Parameters.AddWithValue("@FieldName", fieldName ?? "");
                            cmd.Parameters.AddWithValue("@OldValue", oldValue ?? "");
                            cmd.Parameters.AddWithValue("@NewValue", newValue ?? "");
                            cmd.Parameters.AddWithValue("@Dostawca", dostawca ?? "");
                            cmd.Parameters.AddWithValue("@ChangedBy", userName ?? "system");
                            cmd.Parameters.AddWithValue("@UserID", userId ?? "");
                            cmd.Parameters.AddWithValue("@Nr", nr);
                            cmd.Parameters.AddWithValue("@CarID", carId ?? "");
                            cmd.Parameters.AddWithValue("@CalcDate", calcDate);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.BeginInvoke(new Action(() => UpdateStatus($"Błąd logowania: {ex.Message}")));
                }
            });
        }

        /// <summary>
        /// Dodaje wpis do historii zmian (lokalna lista)
        /// </summary>
        private void AddChangeLogEntry(string action, string details)
        {
            _changeLog.Add(new ChangeLogEntry
            {
                Timestamp = DateTime.Now,
                Action = action,
                NewValue = details,
                UserName = Environment.UserName
            });
        }

        /// <summary>
        /// Generuje opis zastosowanych pól
        /// </summary>
        private string GetFieldMaskDescription(SupplierFieldMask fields, SpecyfikacjaRow row)
        {
            var parts = new List<string>();
            if (fields.HasFlag(SupplierFieldMask.Cena)) parts.Add($"Cena={row.Cena:F2}");
            if (fields.HasFlag(SupplierFieldMask.Dodatek)) parts.Add($"Dodatek={row.Dodatek:F2}");
            if (fields.HasFlag(SupplierFieldMask.Ubytek)) parts.Add($"Ubytek={row.Ubytek:F2}%");
            if (fields.HasFlag(SupplierFieldMask.TypCeny)) parts.Add($"TypCeny={row.TypCeny}");
            if (fields.HasFlag(SupplierFieldMask.TerminDni)) parts.Add($"Termin={row.TerminDni}dni");
            if (fields.HasFlag(SupplierFieldMask.PiK)) parts.Add($"PiK={row.PiK}");
            return string.Join(", ", parts);
        }

        /// <summary>
        /// Pokazuje okno z historią zmian
        /// </summary>
        private void ShowChangeLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var historiaWindow = new HistoriaZmianSpecyfikacjeWindow(connectionString);
                historiaWindow.Owner = Window.GetWindow(this);
                historiaWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd otwierania historii zmian: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region === IRZplus - INTEGRACJA Z ARiMR ===

        /// <summary>
        /// Otwiera okno wysyłki IRZplus dla aktualnie wybranej daty
        /// </summary>
        private void BtnIRZplus_Click(object sender, RoutedEventArgs e)
        {
            OpenIRZplusPreview();
        }

        /// <summary>
        /// Menu kontekstowe - wyślij do IRZplus
        /// </summary>
        private void ContextMenu_IRZplusSend(object sender, RoutedEventArgs e)
        {
            OpenIRZplusPreview();
        }

        /// <summary>
        /// Menu kontekstowe - historia wysyłek IRZplus
        /// </summary>
        private void ContextMenu_IRZplusHistory(object sender, RoutedEventArgs e)
        {
            try
            {
                var historyWindow = new IRZplusHistoryWindow();
                historyWindow.Owner = Window.GetWindow(this);
                historyWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd otwierania historii IRZplus: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Menu kontekstowe - ustawienia IRZplus
        /// </summary>
        private void ContextMenu_IRZplusSettings(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var service = new Services.IRZplusService())
                {
                    var settingsWindow = new IRZplusSettingsWindow(service);
                    settingsWindow.Owner = Window.GetWindow(this);
                    settingsWindow.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd otwierania ustawień IRZplus: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Obsluga przycisku wyboru partii dla specyfikacji
        /// </summary>
        private void BtnWybierzPartie_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var row = button?.Tag as SpecyfikacjaRow;

                if (row == null)
                {
                    // Alternatywna metoda - z DataContext
                    row = (sender as FrameworkElement)?.DataContext as SpecyfikacjaRow;
                }

                if (row == null)
                {
                    MessageBox.Show("Nie mozna okreslic wiersza specyfikacji.", "Blad",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Pobierz date uboju
                DateTime dataUboju = row.DataUboju != default ? row.DataUboju :
                    (dateTimePicker1.SelectedDate ?? DateTime.Today);

                // Otworz okno wyboru partii
                var partiaWindow = new PartiaSelectWindow(
                    connectionString,
                    row.DostawcaGID ?? "",
                    row.Dostawca,
                    dataUboju);

                partiaWindow.Owner = Window.GetWindow(this);

                if (partiaWindow.ShowDialog() == true)
                {
                    // Zapisz partie do bazy
                    using (var conn = new SqlConnection(connectionString))
                    {
                        conn.Open();

                        var cmd = new SqlCommand(@"
                            UPDATE dbo.FarmerCalc
                            SET PartiaGuid = @PartiaGuid
                            WHERE ID = @ID", conn);

                        if (partiaWindow.PartiaRemoved || partiaWindow.SelectedPartiaGuid == null)
                        {
                            cmd.Parameters.AddWithValue("@PartiaGuid", DBNull.Value);
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue("@PartiaGuid", partiaWindow.SelectedPartiaGuid);
                        }
                        cmd.Parameters.AddWithValue("@ID", row.ID);
                        cmd.ExecuteNonQuery();
                    }

                    // Zaktualizuj wiersz w UI
                    row.PartiaGuid = partiaWindow.SelectedPartiaGuid;
                    row.PartiaNumber = partiaWindow.SelectedPartiaNumber;

                    // Informacja
                    if (partiaWindow.PartiaRemoved)
                    {
                        statusLabel.Text = $"Usunieto partie z wiersza {row.Dostawca}";
                    }
                    else
                    {
                        statusLabel.Text = $"Przypisano partie {partiaWindow.SelectedPartiaNumber} do {row.Dostawca}";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad przypisywania partii:\n{ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Otwiera okno podglądu i wysyłki IRZplus
        /// </summary>
        private void OpenIRZplusPreview()
        {
            try
            {
                if (dateTimePicker1.SelectedDate == null)
                {
                    MessageBox.Show("Wybierz datę uboju przed wysyłką do IRZplus.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var dataUboju = dateTimePicker1.SelectedDate.Value;

                // Sprawdź czy są specyfikacje na ten dzień
                var specyfikacje = dataGridView1.ItemsSource as IEnumerable<SpecyfikacjaRow>;
                if (specyfikacje == null || !specyfikacje.Any())
                {
                    MessageBox.Show($"Brak specyfikacji na dzień {dataUboju:dd.MM.yyyy}.\nDodaj specyfikacje przed wysyłką do IRZplus.",
                        "Brak danych", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var previewWindow = new IRZplusPreviewWindow(connectionString, dataUboju);
                previewWindow.Owner = Window.GetWindow(this);

                if (previewWindow.ShowDialog() == true)
                {
                    // Wysyłka zakończona pomyślnie
                    if (!string.IsNullOrEmpty(previewWindow.NumerZgloszenia))
                    {
                        MessageBox.Show($"Zgłoszenie zostało wysłane do IRZplus.\nNumer: {previewWindow.NumerZgloszenia}",
                            "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd otwierania IRZplus: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region === HISTORIA ZMIAN ===

        /// <summary>
        /// Zwraca czytelną nazwę pola
        /// </summary>
        private string GetFieldDisplayName(string fieldName)
        {
            switch (fieldName)
            {
                case "Price":
                case "Cena": return "Cena";
                case "Addition":
                case "Dodatek": return "Dodatek";
                case "Loss":
                case "Ubytek": return "Ubytek";
                case "PriceTypeID": return "Typ ceny";
                case "IncDeadConf": return "PiK";
                case "TerminDni": return "Termin płatności";
                case "Opasienie": return "Opasienie";
                case "KlasaB": return "Klasa B";
                case "Szt.Dek": return "Sztuki deklarowane";
                case "Padłe": return "Padłe";
                case "CH": return "Chore";
                case "NW": return "Niedowaga";
                case "ZM": return "Zamarznięte";
                case "LUMEL": return "LUMEL";
                case "Number": return "Nr specyfikacji";
                default: return fieldName;
            }
        }

        #endregion

        #region === SZABLON DOSTAWCY: Auto-wypełnianie z historii ===

        /// <summary>
        /// Pobiera ostatnie ustawienia cenowe dla dostawcy z historii
        /// </summary>
        private SupplierClipboard GetSupplierTemplate(string dostawcaGID)
        {
            if (string.IsNullOrEmpty(dostawcaGID)) return null;

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Pobierz ostatnie ustawienia z FarmerCalc dla tego dostawcy
                    string query = @"
                        SELECT TOP 1 Price, Addition, Loss, PriceType, TerminDni
                        FROM dbo.FarmerCalc
                        WHERE CustomerGID = @DostawcaGID
                          AND CalcDate < @Today
                          AND Price > 0
                        ORDER BY CalcDate DESC, ID DESC";

                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@DostawcaGID", dostawcaGID);
                        cmd.Parameters.AddWithValue("@Today", dateTimePicker1.SelectedDate ?? DateTime.Today);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new SupplierClipboard
                                {
                                    Cena = reader["Price"] != DBNull.Value ? Convert.ToDecimal(reader["Price"]) : (decimal?)null,
                                    Dodatek = reader["Addition"] != DBNull.Value ? Convert.ToDecimal(reader["Addition"]) : (decimal?)null,
                                    Ubytek = reader["Loss"] != DBNull.Value ? Convert.ToDecimal(reader["Loss"]) * 100 : (decimal?)null,
                                    TypCeny = reader["PriceType"]?.ToString(),
                                    TerminDni = reader["TerminDni"] != DBNull.Value ? Convert.ToInt32(reader["TerminDni"]) : (int?)null
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Blad pobierania szablonu: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Zastosuj szablon dostawcy do aktualnego wiersza
        /// </summary>
        private void ApplySupplierTemplate()
        {
            var currentRow = selectedRow ?? dataGridView1.SelectedItem as SpecyfikacjaRow;
            if (currentRow == null || string.IsNullOrEmpty(currentRow.DostawcaGID)) return;

            var template = GetSupplierTemplate(currentRow.DostawcaGID);
            if (template == null || !template.HasData)
            {
                UpdateStatus($"Brak szablonu dla dostawcy {currentRow.RealDostawca}");
                return;
            }

            // Zastosuj tylko pola które są puste lub zerowe
            bool applied = false;
            if (currentRow.Cena == 0 && template.Cena.HasValue)
            {
                currentRow.Cena = template.Cena.Value;
                applied = true;
            }
            if (currentRow.Dodatek == 0 && template.Dodatek.HasValue)
            {
                currentRow.Dodatek = template.Dodatek.Value;
                applied = true;
            }
            if (currentRow.Ubytek == 0 && template.Ubytek.HasValue)
            {
                currentRow.Ubytek = template.Ubytek.Value;
                applied = true;
            }
            if (string.IsNullOrEmpty(currentRow.TypCeny) && !string.IsNullOrEmpty(template.TypCeny))
            {
                currentRow.TypCeny = template.TypCeny;
                applied = true;
            }
            if (currentRow.TerminDni == 0 && template.TerminDni.HasValue)
            {
                currentRow.TerminDni = template.TerminDni.Value;
                applied = true;
            }

            if (applied)
            {
                QueueRowForSave(currentRow.ID);
                UpdateStatus($"Szablon dostawcy: {template}");
                // Nie potrzeba Items.Refresh() - INotifyPropertyChanged aktualizuje UI
            }
        }

        /// <summary>
        /// Zastosuj szablon dostawcy do wszystkich wierszy tego dostawcy
        /// </summary>
        private void ApplySupplierTemplateToAll()
        {
            var currentRow = selectedRow ?? dataGridView1.SelectedItem as SpecyfikacjaRow;
            if (currentRow == null || string.IsNullOrEmpty(currentRow.DostawcaGID)) return;

            var template = GetSupplierTemplate(currentRow.DostawcaGID);
            if (template == null || !template.HasData)
            {
                UpdateStatus($"Brak szablonu dla dostawcy {currentRow.RealDostawca}");
                return;
            }

            var rowsToUpdate = specyfikacjeData.Where(x => x.DostawcaGID == currentRow.DostawcaGID).ToList();
            var idsToUpdate = new List<int>();

            foreach (var row in rowsToUpdate)
            {
                bool updated = false;
                if (row.Cena == 0 && template.Cena.HasValue) { row.Cena = template.Cena.Value; updated = true; }
                if (row.Dodatek == 0 && template.Dodatek.HasValue) { row.Dodatek = template.Dodatek.Value; updated = true; }
                if (row.Ubytek == 0 && template.Ubytek.HasValue) { row.Ubytek = template.Ubytek.Value; updated = true; }
                if (string.IsNullOrEmpty(row.TypCeny) && !string.IsNullOrEmpty(template.TypCeny)) { row.TypCeny = template.TypCeny; updated = true; }
                if (row.TerminDni == 0 && template.TerminDni.HasValue) { row.TerminDni = template.TerminDni.Value; updated = true; }

                if (updated) idsToUpdate.Add(row.ID);
            }

            if (idsToUpdate.Count > 0)
            {
                // Batch update
                SaveSupplierFieldsBatch(idsToUpdate, SupplierFieldMask.All,
                    template.Cena, template.Dodatek, template.Ubytek, template.TypCeny, template.TerminDni);

                UpdateStatus($"Szablon zastosowany do {idsToUpdate.Count} wierszy: {template}");
                // Nie potrzeba Items.Refresh() - INotifyPropertyChanged aktualizuje UI
            }
        }

        #endregion

        #region === PODSWIETLENIE GRUPY DOSTAWCY ===

        /// <summary>
        /// Podświetl wszystkie wiersze tego samego dostawcy
        /// PROSTA IMPLEMENTACJA - bez LINQ, jawna pętla po WSZYSTKICH wierszach
        /// </summary>
        private void HighlightSupplierGroup(string dostawcaNazwa)
        {
            // Normalizuj klucz wyszukiwania
            string searchKey = (dostawcaNazwa ?? "").Trim().ToLowerInvariant();

            // Jeśli ten sam klucz - nie rób nic
            if (_highlightedSupplier == searchKey) return;
            _highlightedSupplier = searchKey;

            // Prosta pętla - iteruj po WSZYSTKICH wierszach, bez LINQ
            int count = specyfikacjeData.Count;

            for (int i = 0; i < count; i++)
            {
                var row = specyfikacjeData[i];

                // Normalizuj nazwę dostawcy w wierszu
                string rowKey = (row.Dostawca ?? "").Trim().ToLowerInvariant();

                // Porównaj - jeśli pasuje i klucz nie jest pusty, podświetl
                bool shouldHighlight = !string.IsNullOrEmpty(searchKey) && rowKey == searchKey;

                if (row.IsHighlighted != shouldHighlight)
                {
                    row.IsHighlighted = shouldHighlight;
                }
            }
        }

        /// <summary>
        /// Wyczyść podświetlenie grupy
        /// </summary>
        private void ClearSupplierHighlight()
        {
            _highlightedSupplier = null;

            int count = specyfikacjeData.Count;
            for (int i = 0; i < count; i++)
            {
                if (specyfikacjeData[i].IsHighlighted)
                {
                    specyfikacjeData[i].IsHighlighted = false;
                }
            }
        }

        #endregion

        // === WYDAJNOŚĆ: Async batch zapis pozycji wierszy ===
        private async Task SaveAllRowPositionsAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        connection.Open();
                        using (var transaction = connection.BeginTransaction())
                        {
                            try
                            {
                                // Buduj jeden duży UPDATE z CASE
                                var rows = specyfikacjeData.ToList();
                                if (rows.Count == 0) return;

                                StringBuilder sb = new StringBuilder();
                                sb.Append("UPDATE dbo.FarmerCalc SET CarLp = CASE ID ");
                                foreach (var row in rows)
                                {
                                    sb.Append($"WHEN {row.ID} THEN {row.Nr} ");
                                }
                                sb.Append("END WHERE ID IN (");
                                sb.Append(string.Join(",", rows.Select(r => r.ID)));
                                sb.Append(")");

                                using (SqlCommand cmd = new SqlCommand(sb.ToString(), connection, transaction))
                                {
                                    cmd.ExecuteNonQuery();
                                }

                                transaction.Commit();
                            }
                            catch
                            {
                                transaction.Rollback();
                                throw;
                            }
                        }
                    }
                });

                Dispatcher.Invoke(() => UpdateStatus("Pozycje zapisane"));
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => UpdateStatus($"Błąd zapisu pozycji: {ex.Message}"));
            }
        }

        private void ButtonUP_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = dataGridView1.SelectedIndex;
            if (selectedIndex > 0)
            {
                var item = specyfikacjeData[selectedIndex];
                specyfikacjeData.RemoveAt(selectedIndex);
                specyfikacjeData.Insert(selectedIndex - 1, item);
                dataGridView1.SelectedIndex = selectedIndex - 1;
                RefreshNumeration();
                UpdateStatus("Wiersz przeniesiony w górę");
            }
        }

        private void ButtonDown_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = dataGridView1.SelectedIndex;
            if (selectedIndex >= 0 && selectedIndex < specyfikacjeData.Count - 1)
            {
                var item = specyfikacjeData[selectedIndex];
                specyfikacjeData.RemoveAt(selectedIndex);
                specyfikacjeData.Insert(selectedIndex + 1, item);
                dataGridView1.SelectedIndex = selectedIndex + 1;
                RefreshNumeration();
                UpdateStatus("Wiersz przeniesiony w dół");
            }
        }

        private void RefreshNumeration()
        {
            for (int i = 0; i < specyfikacjeData.Count; i++)
            {
                specyfikacjeData[i].Nr = i + 1;
            }
        }

        private void BtnLoadData_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateStatus("Pobieranie danych LUMEL...");
                string ftpUrl = "ftp://admin:wago@192.168.0.98/POMIARY.TXT";

                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(ftpUrl);
                request.Method = WebRequestMethods.Ftp.DownloadFile;

                using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                using (Stream responseStream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(responseStream))
                {
                    string content = reader.ReadToEnd();
                    string[] rows = content.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                    DataTable lumelData = new DataTable();
                    lumelData.Columns.Add("Data");
                    lumelData.Columns.Add("Godzina");
                    lumelData.Columns.Add("Ilość");

                    DateTime selectedDate = dateTimePicker1.SelectedDate ?? DateTime.Today;
                    var numberFormat = new System.Globalization.NumberFormatInfo
                    {
                        NumberDecimalSeparator = "."
                    };

                    foreach (string row in rows)
                    {
                        string[] columns = row.Split(';');
                        if (columns.Length >= 3 &&
                            DateTime.TryParse(columns[0], out DateTime rowDate) &&
                            rowDate.Date == selectedDate &&
                            columns[2] != "0.0")
                        {
                            if (double.TryParse(columns[2], System.Globalization.NumberStyles.Any,
                                numberFormat, out double quantity))
                            {
                                double roundedQuantity = Math.Ceiling(quantity);
                                lumelData.Rows.Add(columns[0], columns[1], roundedQuantity.ToString());
                            }
                        }
                    }

                    dataGridView2.ItemsSource = lumelData.DefaultView;
                    lumelPanel.Visibility = Visibility.Visible;
                    UpdateStatus($"Załadowano {lumelData.Rows.Count} rekordów LUMEL");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas pobierania danych LUMEL:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatus("Błąd pobierania danych LUMEL");
            }
        }

        private void BtnCloseLumel_Click(object sender, RoutedEventArgs e)
        {
            lumelPanel.Visibility = Visibility.Collapsed;
        }

        private void DataGridView2_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Przypisz wartość LUMEL z dataGridView2 do wybranego wiersza w dataGridView1
            if (selectedRow != null && dataGridView2.SelectedItem != null)
            {
                var lumelRow = dataGridView2.SelectedItem as DataRowView;
                if (lumelRow != null)
                {
                    string iloscStr = lumelRow["Ilość"]?.ToString();
                    if (int.TryParse(iloscStr, out int ilosc))
                    {
                        selectedRow.LUMEL = ilosc;
                        dataGridView1.Items.Refresh();
                        UpdateStatistics();
                        UpdateStatus($"Przypisano LUMEL {ilosc} do LP {selectedRow.Nr}");
                    }
                }
            }
            else
            {
                MessageBox.Show("Wybierz najpierw wiersz w tabeli głównej, a potem kliknij dwukrotnie na dane LUMEL.",
                    "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Button1_Click(object sender, RoutedEventArgs e)
        {
            // Pobierz wiersz z wielu źródeł
            var currentRow = selectedRow
                ?? dataGridView1.SelectedItem as SpecyfikacjaRow
                ?? dataGridView1.CurrentCell.Item as SpecyfikacjaRow;

            // Ostatnia próba - z zaznaczonych komórek
            if (currentRow == null && dataGridView1.SelectedCells.Count > 0)
                currentRow = dataGridView1.SelectedCells[0].Item as SpecyfikacjaRow;

            if (currentRow != null)
            {
                try
                {
                    UpdateStatus($"Generowanie PDF dla: {currentRow.RealDostawca}...");

                    // Zbierz wszystkie ID dla tego samego dostawcy (RealDostawca)
                    var wierszeDoPDF = specyfikacjeData
                        .Where(r => r.RealDostawca == currentRow.RealDostawca)
                        .ToList();

                    List<int> ids = wierszeDoPDF.Select(r => r.ID).ToList();

                    if (ids.Count > 0)
                    {
                        GeneratePDFReport(ids);

                        // Oznacz wszystkie wiersze tego dostawcy jako wydrukowane
                        foreach (var wiersz in wierszeDoPDF)
                        {
                            wiersz.Wydrukowano = true;
                        }

                        UpdateStatus($"PDF wygenerowany dla: {currentRow.RealDostawca}");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas generowania PDF:\n{ex.Message}",
                        "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    UpdateStatus("Błąd generowania PDF");
                }
            }
            else
            {
                MessageBox.Show("Proszę wybrać wiersz do wydruku.",
                    "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ButtonBon_Click(object sender, RoutedEventArgs e)
        {
            // Pobierz wiersz z wielu źródeł
            var row = selectedRow
                ?? dataGridView1.SelectedItem as SpecyfikacjaRow
                ?? dataGridView1.CurrentCell.Item as SpecyfikacjaRow;

            // Ostatnia próba - z zaznaczonych komórek
            if (row == null && dataGridView1.SelectedCells.Count > 0)
                row = dataGridView1.SelectedCells[0].Item as SpecyfikacjaRow;

            if (row != null)
            {
                try
                {
                    WidokAvilog avilogForm = new WidokAvilog(row.ID);
                    avilogForm.ShowDialog(); // ShowDialog aby po zamknięciu odświeżyć dane

                    // Odśwież dane po zamknięciu Avilog
                    LoadData(dateTimePicker1.SelectedDate ?? DateTime.Today);
                    UpdateStatus($"Odświeżono dane po edycji w AVILOG");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas otwierania AVILOG:\n{ex.Message}",
                        "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Proszę wybrać wiersz.",
                    "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void UpdateStatus(string message)
        {
            statusLabel.Text = message;
        }

        // Ustawienia lokalizacji PDF
        private void BtnPdfSettings_Click(object sender, RoutedEventArgs e)
        {
            var result = System.Windows.MessageBox.Show(
                $"Aktualna ścieżka zapisu PDF:\n{defaultPdfPath}\n\n" +
                $"Używaj domyślnej ścieżki: {(useDefaultPath ? "TAK" : "NIE")}\n\n" +
                "Czy chcesz zmienić ścieżkę zapisu?\n\n" +
                "TAK - wybierz nowy folder\n" +
                "NIE - użyj jednorazowej lokalizacji przy następnym zapisie",
                "Ustawienia PDF",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Tutaj używamy pełnej nazwy dla FolderBrowserDialog
                var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Wybierz folder do zapisywania PDF",
                    SelectedPath = defaultPdfPath
                };

                // Tutaj używamy pełnej nazwy dla DialogResult
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    defaultPdfPath = dialog.SelectedPath;
                    useDefaultPath = true;
                    System.Windows.MessageBox.Show($"Nowa domyślna ścieżka:\n{defaultPdfPath}", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else if (result == MessageBoxResult.No)
            {
                useDefaultPath = false;
                System.Windows.MessageBox.Show("Przy następnym zapisie PDF zostaniesz zapytany o lokalizację.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        // Drukuj wszystkie specyfikacje z dnia
        private void BtnPrintAll_Click(object sender, RoutedEventArgs e)
        {
            if (specyfikacjeData.Count == 0)
            {
                MessageBox.Show("Brak danych do wydruku.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Czy chcesz wydrukować wszystkie specyfikacje z dnia?\n\n" +
                $"Liczba unikalnych dostawców: {specyfikacjeData.Select(r => r.RealDostawca).Distinct().Count()}\n" +
                $"Łączna liczba rekordów: {specyfikacjeData.Count}",
                "Drukuj wszystkie",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    UpdateStatus("Generowanie PDF dla wszystkich dostawców...");

                    // Grupuj po dostawcy
                    var grupy = specyfikacjeData.GroupBy(r => r.RealDostawca).ToList();
                    int count = 0;

                    foreach (var grupa in grupy)
                    {
                        List<int> ids = grupa.Select(r => r.ID).ToList();
                        GeneratePDFReport(ids, false); // false = nie pokazuj MessageBox

                        // Oznacz wszystkie wiersze tej grupy jako wydrukowane
                        foreach (var wiersz in grupa)
                        {
                            wiersz.Wydrukowano = true;
                        }

                        count++;
                    }

                    UpdateStatus($"Wygenerowano {count} plików PDF");
                    MessageBox.Show($"Wygenerowano {count} plików PDF.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas generowania PDF:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    UpdateStatus("Błąd generowania PDF");
                }
            }
        }

        // === Checkbox: Pokaż/ukryj kolumnę Opasienie ===
        private void ChkShowOpasienie_Changed(object sender, RoutedEventArgs e)
        {
            bool isChecked = (sender as CheckBox)?.IsChecked == true;

            if (colOpasienie != null)
            {
                colOpasienie.Visibility = isChecked
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        // === Checkbox: Pokaż/ukryj kolumnę Klasa B ===
        private void ChkShowKlasaB_Changed(object sender, RoutedEventArgs e)
        {
            bool isChecked = (sender as CheckBox)?.IsChecked == true;

            if (colKlasaB != null)
            {
                colKlasaB.Visibility = isChecked
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        // === Checkbox: Pokaż/ukryj kolumnę Pośrednik ===
        private void ChkShowPosrednik_Changed(object sender, RoutedEventArgs e)
        {
            bool isChecked = (sender as CheckBox)?.IsChecked == true;

            if (colPosrednik != null)
            {
                colPosrednik.Visibility = isChecked
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        // === Checkbox: Pokaż/ukryj kolumnę Dodatek ===
        private void ChkShowDodatek_Changed(object sender, RoutedEventArgs e)
        {
            bool isChecked = (sender as CheckBox)?.IsChecked == true;

            if (colDodatek != null)
            {
                colDodatek.Visibility = isChecked
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        // === Checkbox: Grupuj wiersze według dostawcy ===
        private void ChkGroupBySupplier_Changed(object sender, RoutedEventArgs e)
        {
            var checkbox = sender as CheckBox;
            bool groupBySupplier = checkbox?.IsChecked == true;

            // Zapisz stan grupowania
            _isGroupingBySupplier = groupBySupplier;

            if (specyfikacjeData == null || specyfikacjeData.Count == 0) return;

            // WAZNE: Blokuj logowanie zmian podczas grupowania
            _isLoadingData = true;

            try
            {
                if (groupBySupplier)
                {
                    // Grupuj wedlug dostawcy, zachowujac kolejnosc LP w grupie
                    var grouped = specyfikacjeData
                        .OrderBy(x => x.RealDostawca)
                        .ThenBy(x => x.Nr)
                        .ToList();

                    // Wyczysc i dodaj ponownie w nowej kolejnosci
                    specyfikacjeData.Clear();
                    foreach (var item in grouped)
                    {
                        specyfikacjeData.Add(item);
                    }

                    // Przypisz kolory i oznacz granice grup
                    AssignSupplierColorsAndGroups();

                    UpdateStatus("Wiersze pogrupowane wedlug dostawcy");
                }
                else
                {
                    // Sortuj wedlug LP
                    var sorted = specyfikacjeData
                        .OrderBy(x => x.Nr)
                        .ToList();

                    specyfikacjeData.Clear();
                    foreach (var item in sorted)
                    {
                        specyfikacjeData.Add(item);
                    }

                    // Przypisz kolory (bez grup separatorow)
                    AssignSupplierColorsAndGroups();

                    UpdateStatus("Wiersze posortowane wedlug LP");
                }
            }
            finally
            {
                // WAZNE: Resetuj flage DOPIERO gdy WPF skonczy wszystkie operacje UI
                // To zapobiega logowaniu zmian checkbox/combobox podczas grupowania
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    _isLoadingData = false;
                }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
        }

        // === Przypisz kolory dostawcom i oznacz granice grup ===
        private void AssignSupplierColorsAndGroups()
        {
            if (specyfikacjeData == null || specyfikacjeData.Count == 0) return;

            // Paleta kolorów dla dostawców (łatwo rozróżnialne)
            var supplierColors = new List<string>
            {
                "#4CAF50", "#2196F3", "#FF9800", "#9C27B0", "#00BCD4",
                "#E91E63", "#8BC34A", "#673AB7", "#03A9F4", "#FF5722",
                "#009688", "#3F51B5", "#CDDC39", "#FFC107", "#795548",
                "#607D8B", "#F44336", "#FFEB3B", "#00E676", "#7C4DFF"
            };

            // Mapowanie dostawca -> kolor
            var supplierColorMap = new Dictionary<string, string>();
            int colorIndex = 0;

            // Przypisz kolory unikalne dla każdego dostawcy
            foreach (var row in specyfikacjeData)
            {
                string supplierKey = row.RealDostawca ?? "Nieznany";

                if (!supplierColorMap.ContainsKey(supplierKey))
                {
                    supplierColorMap[supplierKey] = supplierColors[colorIndex % supplierColors.Count];
                    colorIndex++;
                }

                row.SupplierColor = supplierColorMap[supplierKey];
            }

            // Oznacz granice grup (dla separatorów) - TYLKO gdy grupowanie jest włączone
            string previousSupplier = null;
            for (int i = 0; i < specyfikacjeData.Count; i++)
            {
                var row = specyfikacjeData[i];
                string currentSupplier = row.RealDostawca ?? "Nieznany";

                if (_isGroupingBySupplier)
                {
                    // Pierwszy w grupie - pokaż separator
                    row.IsFirstInGroup = (previousSupplier != currentSupplier) && (i > 0);

                    // Ostatni w grupie
                    if (i < specyfikacjeData.Count - 1)
                    {
                        string nextSupplier = specyfikacjeData[i + 1].RealDostawca ?? "Nieznany";
                        row.IsLastInGroup = (currentSupplier != nextSupplier);
                    }
                    else
                    {
                        row.IsLastInGroup = true;
                    }
                }
                else
                {
                    // Bez grupowania - nie pokazuj separatorów
                    row.IsFirstInGroup = false;
                    row.IsLastInGroup = false;
                }

                previousSupplier = currentSupplier;
            }
        }

        // === Checkbox: Grupuj wiersze według dostawcy (Rozliczenia) ===
        private void ChkGroupBySupplierRozliczenia_Changed(object sender, RoutedEventArgs e)
        {
            var checkbox = sender as CheckBox;
            bool groupBySupplier = checkbox?.IsChecked == true;

            if (rozliczeniaData == null || rozliczeniaData.Count == 0) return;

            if (groupBySupplier)
            {
                // Grupuj według dostawcy
                var grouped = rozliczeniaData
                    .OrderBy(x => x.Dostawca)
                    .ThenBy(x => x.Nr)
                    .ToList();

                rozliczeniaData.Clear();
                foreach (var item in grouped)
                {
                    rozliczeniaData.Add(item);
                }

                // Ustaw IsFirstInGroup dla separatorów
                string previousSupplier = null;
                for (int i = 0; i < rozliczeniaData.Count; i++)
                {
                    var row = rozliczeniaData[i];
                    string currentSupplier = row.Dostawca ?? "Nieznany";
                    row.IsFirstInGroup = (previousSupplier != currentSupplier) && (i > 0);
                    previousSupplier = currentSupplier;
                }

                UpdateStatus("Rozliczenia pogrupowane według dostawcy");
            }
            else
            {
                // Sortuj według LP
                var sorted = rozliczeniaData
                    .OrderBy(x => x.Nr)
                    .ToList();

                rozliczeniaData.Clear();
                foreach (var item in sorted)
                {
                    rozliczeniaData.Add(item);
                }

                // Wyłącz separatory
                foreach (var row in rozliczeniaData)
                {
                    row.IsFirstInGroup = false;
                }

                UpdateStatus("Rozliczenia posortowane według LP");
            }
        }

        // === Checkbox: Grupuj wiersze według dostawcy (Transport) ===
        private void ChkGroupBySupplierTransport_Changed(object sender, RoutedEventArgs e)
        {
            var checkbox = sender as CheckBox;
            bool groupBySupplier = checkbox?.IsChecked == true;

            if (transportData == null || transportData.Count == 0) return;

            if (groupBySupplier)
            {
                // Grupuj według dostawcy
                var grouped = transportData
                    .OrderBy(x => x.Dostawca)
                    .ThenBy(x => x.Nr)
                    .ToList();

                transportData.Clear();
                foreach (var item in grouped)
                {
                    transportData.Add(item);
                }

                // Ustaw IsFirstInGroup dla separatorów
                string previousSupplier = null;
                for (int i = 0; i < transportData.Count; i++)
                {
                    var row = transportData[i];
                    string currentSupplier = row.Dostawca ?? "Nieznany";
                    row.IsFirstInGroup = (previousSupplier != currentSupplier) && (i > 0);
                    previousSupplier = currentSupplier;
                }

                UpdateStatus("Transport pogrupowany według dostawcy");
            }
            else
            {
                // Sortuj według LP
                var sorted = transportData
                    .OrderBy(x => x.Nr)
                    .ToList();

                transportData.Clear();
                foreach (var item in sorted)
                {
                    transportData.Add(item);
                }

                // Wyłącz separatory
                foreach (var row in transportData)
                {
                    row.IsFirstInGroup = false;
                }

                UpdateStatus("Transport posortowany według LP");
            }
        }

        // === Checkbox: Grupuj wiersze według dostawcy (Płachta) ===
        private void ChkGroupBySupplierPlachta_Changed(object sender, RoutedEventArgs e)
        {
            var checkbox = sender as CheckBox;
            bool groupBySupplier = checkbox?.IsChecked == true;

            if (plachtaData == null || plachtaData.Count == 0) return;

            if (groupBySupplier)
            {
                // Grupuj według dostawcy (Hodowca)
                var grouped = plachtaData
                    .OrderBy(x => x.Hodowca)
                    .ThenBy(x => x.Lp)
                    .ToList();

                plachtaData.Clear();
                foreach (var item in grouped)
                {
                    plachtaData.Add(item);
                }

                // Ustaw IsFirstInGroup dla separatorów
                string previousHodowca = null;
                for (int i = 0; i < plachtaData.Count; i++)
                {
                    var row = plachtaData[i];
                    string currentHodowca = row.Hodowca ?? "Nieznany";
                    row.IsFirstInGroup = (previousHodowca != currentHodowca) && (i > 0);
                    previousHodowca = currentHodowca;
                }

                UpdateStatus("Płachta pogrupowana według hodowcy");
            }
            else
            {
                // Sortuj według LP
                var sorted = plachtaData
                    .OrderBy(x => x.Lp)
                    .ToList();

                plachtaData.Clear();
                foreach (var item in sorted)
                {
                    plachtaData.Add(item);
                }

                // Wyłącz separatory
                foreach (var row in plachtaData)
                {
                    row.IsFirstInGroup = false;
                }

                UpdateStatus("Płachta posortowana według LP");
            }
        }

        // === NOWY: Przycisk skróconego PDF (ukryty, ale metoda pozostaje dla email) ===
        private void BtnShortPdf_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridView1.SelectedCells.Count == 0)
            {
                MessageBox.Show("Wybierz wiersz z tabeli.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var selectedRow = dataGridView1.SelectedCells[0].Item as SpecyfikacjaRow;
            if (selectedRow == null) return;

            string dostawcaGID = selectedRow.DostawcaGID;
            var ids = specyfikacjeData
                .Where(x => x.DostawcaGID == dostawcaGID)
                .Select(x => x.ID)
                .ToList();

            if (ids.Count > 0)
            {
                GenerateShortPDFReport(ids);
            }
        }

        // === NOWY: Przycisk wysyłania SMS ===
        private void BtnSendSms_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridView1.SelectedCells.Count == 0)
            {
                MessageBox.Show("Wybierz wiersz z tabeli.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var selectedRow = dataGridView1.SelectedCells[0].Item as SpecyfikacjaRow;
            if (selectedRow == null) return;

            string dostawcaGID = selectedRow.DostawcaGID;
            var ids = specyfikacjeData
                .Where(x => x.DostawcaGID == dostawcaGID)
                .Select(x => x.ID)
                .ToList();

            if (ids.Count > 0)
            {
                SendSmsToFarmer(ids);
            }
        }

        // === SMS do hodowcy - kopiowanie do schowka ===
        private void SendSmsToFarmer(List<int> ids)
        {
            try
            {
                string customerRealGID = zapytaniasql.PobierzInformacjeZBazyDanych<string>(ids[0], "[LibraNet].[dbo].[FarmerCalc]", "CustomerRealGID");
                string sellerName = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(customerRealGID, "ShortName") ?? "Hodowca";

                // Pobierz numer telefonu - sprawdź Phone1, Phone2, Phone3
                string phoneNumber = GetPhoneNumber(customerRealGID);

                // Jeśli brak telefonu - otwórz formularz edycji hodowcy
                if (string.IsNullOrWhiteSpace(phoneNumber))
                {
                    var confirmEdit = MessageBox.Show(
                        $"Brak numeru telefonu dla hodowcy: {sellerName}\n\nCzy chcesz teraz uzupełnić dane kontaktowe?",
                        "Brak telefonu",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (confirmEdit == MessageBoxResult.Yes)
                    {
                        // Otwórz formularz edycji hodowcy (Windows Forms)
                        var hodowcaForm = new HodowcaForm(customerRealGID, Environment.UserName);
                        hodowcaForm.ShowDialog();

                        // Po zamknięciu formularza - sprawdź ponownie czy telefon został uzupełniony
                        phoneNumber = GetPhoneNumber(customerRealGID);
                        if (string.IsNullOrWhiteSpace(phoneNumber))
                        {
                            MessageBox.Show("Numer telefonu nadal nie został uzupełniony.", "Brak telefonu", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }
                        // Odśwież nazwę hodowcy (mogła się zmienić)
                        sellerName = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(customerRealGID, "ShortName") ?? "Hodowca";
                    }
                    else
                    {
                        return;
                    }
                }

                // Pobierz WSZYSTKIE rozliczenia dla tego hodowcy
                var rozliczeniaHodowcy = specyfikacjeData
                    .Where(r => r.DostawcaGID == customerRealGID ||
                               zapytaniasql.PobierzInformacjeZBazyDanych<string>(r.ID, "[LibraNet].[dbo].[FarmerCalc]", "CustomerRealGID") == customerRealGID)
                    .ToList();

                if (rozliczeniaHodowcy.Count == 0)
                {
                    rozliczeniaHodowcy = specyfikacjeData.Where(r => ids.Contains(r.ID)).ToList();
                }

                // Oblicz podsumowanie - POPRAWIONE OBLICZENIE ŚREDNIEJ WAGI
                decimal sumaNetto = 0;
                decimal sumaWartosc = 0;
                int sumaSztWszystkie = 0;  // LUMEL + Padłe (wszystkie sztuki)

                foreach (var row in rozliczeniaHodowcy)
                {
                    sumaNetto += row.WagaNettoDoRozliczenia;  // Preferuje wagę hodowcy
                    // Wszystkie sztuki = LUMEL + Padłe (tak jak w PDF)
                    int sztWszystkie = row.LUMEL + row.Padle;
                    sumaSztWszystkie += sztWszystkie;
                    sumaWartosc += row.Wartosc;
                }

                // Średnia waga = Netto / (LUMEL + Padłe)
                decimal sredniaWaga = sumaSztWszystkie > 0 ? sumaNetto / sumaSztWszystkie : 0;
                DateTime dzienUbojowy = dateTimePicker1.SelectedDate ?? DateTime.Today;

                // Treść SMS - krótka wersja
                string smsMessage = $"Piorkowscy: {sellerName}, " +
                                   $"{dzienUbojowy:dd.MM.yyyy}, " +
                                   $"Szt:{sumaSztWszystkie}, Kg:{sumaNetto:N0}, " +
                                   $"Sr.waga:{sredniaWaga:N2}kg, " +
                                   $"Do wyplaty:{sumaWartosc:N0}zl";

                // Skopiuj numer telefonu do schowka
                System.Windows.Clipboard.SetText(phoneNumber);

                var result = MessageBox.Show(
                    $"📱 Numer telefonu skopiowany do schowka:\n{phoneNumber}\n\n" +
                    $"📝 Treść SMS:\n{smsMessage}\n\n" +
                    $"📊 Szczegóły ({rozliczeniaHodowcy.Count} pozycji):\n" +
                    $"   Sztuki (LUMEL+Padłe): {sumaSztWszystkie}\n" +
                    $"   Kilogramy netto: {sumaNetto:N0}\n" +
                    $"   Średnia waga: {sredniaWaga:N2} kg\n" +
                    $"   Wartość: {sumaWartosc:N0} zł\n\n" +
                    $"Czy skopiować treść SMS do schowka?",
                    "SMS - Numer skopiowany",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    // Skopiuj treść SMS do schowka
                    System.Windows.Clipboard.SetText(smsMessage);
                    MessageBox.Show("✅ Treść SMS skopiowana do schowka!\n\nMożesz teraz wkleić ją do SMS Desktop.",
                        "Skopiowano", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Pobiera pierwszy dostępny numer telefonu hodowcy (Phone1, Phone2 lub Phone3)
        /// </summary>
        private string GetPhoneNumber(string customerGID)
        {
            string phone = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(customerGID, "Phone1");
            if (string.IsNullOrWhiteSpace(phone))
                phone = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(customerGID, "Phone2");
            if (string.IsNullOrWhiteSpace(phone))
                phone = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(customerGID, "Phone3");
            return phone;
        }
        private void ContextMenu_DuplicateRow(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedRow = dataGridView1.SelectedItem as SpecyfikacjaRow;
                if (selectedRow == null)
                {
                    MessageBox.Show("Wybierz wiersz do duplikowania!", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string customerRealGID = "";
                int priceTypeID = 0;
                try { customerRealGID = zapytaniasql.PobierzInformacjeZBazyDanych<string>(selectedRow.ID, "[LibraNet].[dbo].[FarmerCalc]", "CustomerRealGID") ?? ""; } catch { }
                try { priceTypeID = zapytaniasql.PobierzInformacjeZBazyDanych<int>(selectedRow.ID, "[LibraNet].[dbo].[FarmerCalc]", "PriceTypeID"); } catch { }

                var duplicateData = new NowaSpecyfikacjaWindow.DuplicateData
                {
                    SourceHodowca = selectedRow.Dostawca ?? selectedRow.RealDostawca ?? "-",
                    Cena = selectedRow.Cena,
                    TypCeny = selectedRow.TypCeny ?? "",
                    TypCenyID = priceTypeID,
                    Ubytek = selectedRow.Ubytek,
                    PiK = selectedRow.PiK,
                    CustomerGID = selectedRow.DostawcaGID ?? "",
                    CustomerRealGID = customerRealGID
                };

                DateTime? selectedDate = dateTimePicker1.SelectedDate ?? DateTime.Today;

                var window = new NowaSpecyfikacjaWindow(connectionString, selectedDate, duplicateData);
                window.Owner = Window.GetWindow(this);

                if (window.ShowDialog() == true && window.SpecyfikacjaCreated)
                {
                    // Loguj duplikację do historii zmian
                    string details = $"Źródło: LP {selectedRow.Nr}, Hodowca: {duplicateData.SourceHodowca}, " +
                                   $"Cena: {duplicateData.Cena:F2}, Typ: {duplicateData.TypCeny}, " +
                                   $"Ubytek: {duplicateData.Ubytek}%, PiK: {duplicateData.PiK}";
                    LogChangeToDatabase(window.CreatedSpecId, "DUPLICATE", $"Źródło ID: {selectedRow.ID}", details,
                        duplicateData.SourceHodowca, 0, "");

                    LoadData(selectedDate.Value);
                    UpdateStatistics();
                    UpdateStatus($"Duplikowano specyfikację. Nowe ID: {window.CreatedSpecId}");

                    var newRow = specyfikacjeData.FirstOrDefault(s => s.ID == window.CreatedSpecId);
                    if (newRow != null)
                    {
                        dataGridView1.SelectedItem = newRow;
                        dataGridView1.ScrollIntoView(newRow);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd duplikowania:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Menu kontekstowe: Usuń specyfikację
        /// </summary>
        private void ContextMenu_DeleteRow(object sender, RoutedEventArgs e)
        {
            DeleteSelectedRow();
        }

        // === Przycisk SMS ZAŁADUNEK - informacja o godzinie przyjazdu auta ===
        private void BtnSmsZaladunek_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridView1.SelectedCells.Count == 0)
            {
                MessageBox.Show("Wybierz wiersz z tabeli.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var selectedRows = dataGridView1.SelectedCells
                .Cast<DataGridCellInfo>()
                .Select(cell => cell.Item as SpecyfikacjaRow)
                .Where(row => row != null)
                .Distinct()
                .ToList();

            var ids = selectedRows.Select(x => x.ID).ToList();

            if (ids.Count > 0)
            {
                SendZaladunekSms(ids);
            }
        }

        // === SMS z informacją o załadunku (godzina, kierowca, auto) ===
        private void SendZaladunekSms(List<int> ids)
        {
            try
            {
                string customerRealGID = zapytaniasql.PobierzInformacjeZBazyDanych<string>(ids[0], "[LibraNet].[dbo].[FarmerCalc]", "CustomerRealGID");
                string sellerName = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(customerRealGID, "ShortName") ?? "Hodowca";

                // Pobierz telefon hodowcy
                string phoneNumber = GetPhoneNumber(customerRealGID);

                if (string.IsNullOrWhiteSpace(phoneNumber))
                {
                    var confirmEdit = MessageBox.Show(
                        $"Brak numeru telefonu dla hodowcy: {sellerName}\n\nCzy chcesz teraz uzupełnić dane kontaktowe?",
                        "Brak telefonu",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (confirmEdit == MessageBoxResult.Yes)
                    {
                        var hodowcaForm = new HodowcaForm(customerRealGID, Environment.UserName);
                        hodowcaForm.ShowDialog();
                        phoneNumber = GetPhoneNumber(customerRealGID);
                        if (string.IsNullOrWhiteSpace(phoneNumber))
                        {
                            MessageBox.Show("Numer telefonu nadal nie został uzupełniony.", "Brak telefonu", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }
                        sellerName = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(customerRealGID, "ShortName") ?? "Hodowca";
                    }
                    else
                    {
                        return;
                    }
                }

                // Pobierz dane z pierwszego rozliczenia
                int firstId = ids[0];

                // Godzina załadunku
                DateTime zaladunekTime = zapytaniasql.PobierzInformacjeZBazyDanych<DateTime>(firstId, "[LibraNet].[dbo].[FarmerCalc]", "Zaladunek");
                string zaladunekStr = zaladunekTime != default ? zaladunekTime.ToString("HH:mm") : "brak";

                // Kierowca
                int driverGID = zapytaniasql.PobierzInformacjeZBazyDanych<int>(firstId, "[LibraNet].[dbo].[FarmerCalc]", "DriverGID");
                string kierowcaNazwa = driverGID > 0 ? (zapytaniasql.ZnajdzNazweKierowcy(driverGID) ?? "nieprzypisany") : "nieprzypisany";
                string kierowcaTelefon = driverGID > 0 ? GetKierowcaTelefon(driverGID) : "";

                // Numer auta
                string ciagnikNr = zapytaniasql.PobierzInformacjeZBazyDanych<string>(firstId, "[LibraNet].[dbo].[FarmerCalc]", "CarID") ?? "";

                // Data
                DateTime dzienUbojowy = dateTimePicker1.SelectedDate ?? DateTime.Today;

                // Oblicz sumę sztuk
                var rozliczeniaHodowcy = specyfikacjeData
                    .Where(r => r.DostawcaGID == customerRealGID ||
                               zapytaniasql.PobierzInformacjeZBazyDanych<string>(r.ID, "[LibraNet].[dbo].[FarmerCalc]", "CustomerRealGID") == customerRealGID)
                    .ToList();

                int sumaSzt = rozliczeniaHodowcy.Sum(r => r.LUMEL);

                // Treść SMS
                string smsMessage;
                if (!string.IsNullOrWhiteSpace(kierowcaTelefon))
                {
                    smsMessage = $"Piorkowscy: {dzienUbojowy:dd.MM} godz.{zaladunekStr} " +
                                $"Kierowca:{kierowcaNazwa} tel:{kierowcaTelefon} " +
                                $"Auto:{ciagnikNr} Szt:{sumaSzt}";
                }
                else
                {
                    smsMessage = $"Piorkowscy: Zaladunk {dzienUbojowy:dd.MM} godz.{zaladunekStr} " +
                                $"Kierowca:{kierowcaNazwa} Auto:{ciagnikNr} Szt:{sumaSzt}";
                }

                // Skopiuj telefon hodowcy do schowka
                System.Windows.Clipboard.SetText(phoneNumber);

                var result = MessageBox.Show(
                    $"🚛 SMS INFORMACJA O ZAŁADUNKU\n" +
                    $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
                    $"📱 Telefon hodowcy (skopiowany):\n{phoneNumber}\n\n" +
                    $"📅 Data: {dzienUbojowy:dd.MM.yyyy}\n" +
                    $"⏰ Godzina załadunku: {zaladunekStr}\n" +
                    $"👤 Kierowca: {kierowcaNazwa}\n" +
                    (string.IsNullOrWhiteSpace(kierowcaTelefon) ? "" : $"📞 Tel. kierowcy: {kierowcaTelefon}\n") +
                    $"🚛 Auto: {ciagnikNr}\n" +
                    $"📦 Sztuk: {sumaSzt}\n\n" +
                    $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                    $"📝 Treść SMS:\n{smsMessage}\n\n" +
                    $"Czy skopiować treść SMS do schowka?",
                    "SMS Załadunek - Numer skopiowany",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    System.Windows.Clipboard.SetText(smsMessage);
                    MessageBox.Show("✅ Treść SMS skopiowana do schowka!\n\nMożesz teraz wkleić ją do SMS Desktop.",
                        "Skopiowano", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Pobiera telefon kierowcy z bazy TransportPL
        /// </summary>
        private string GetKierowcaTelefon(int driverGID)
        {
            try
            {
                string connStr = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";
                using (var conn = new Microsoft.Data.SqlClient.SqlConnection(connStr))
                {
                    conn.Open();
                    using (var cmd = new Microsoft.Data.SqlClient.SqlCommand("SELECT Telefon FROM dbo.Kierowcy WHERE ID = @ID", conn))
                    {
                        cmd.Parameters.AddWithValue("@ID", driverGID);
                        var result = cmd.ExecuteScalar();
                        return result?.ToString() ?? "";
                    }
                }
            }
            catch
            {
                return "";
            }
        }

        // === Przycisk EMAIL - wysyłka z PDF w załączniku ===
        private void BtnSendEmail_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridView1.SelectedCells.Count == 0)
            {
                MessageBox.Show("Wybierz wiersz z tabeli.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Pobierz wszystkie unikalne wiersze z zaznaczonych komórek
            var selectedRows = dataGridView1.SelectedCells
                .Cast<DataGridCellInfo>()
                .Select(cell => cell.Item as SpecyfikacjaRow)
                .Where(row => row != null)
                .Distinct()
                .ToList();

            var ids = selectedRows.Select(x => x.ID).ToList();

            if (ids.Count > 0)
            {
                SendEmailToFarmer(ids);
            }
        }

        // === Email do hodowcy z PDF w załączniku ===
        private void SendEmailToFarmer(List<int> ids)
        {
            try
            {
                string customerRealGID = zapytaniasql.PobierzInformacjeZBazyDanych<string>(ids[0], "[LibraNet].[dbo].[FarmerCalc]", "CustomerRealGID");
                string sellerName = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(customerRealGID, "ShortName") ?? "Hodowca";

                // Pobierz email hodowcy - jesli jest w bazie to wstaw, jesli nie to puste
                string email = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(customerRealGID, "Email") ?? "";

                // Pobierz WSZYSTKIE rozliczenia dla tego hodowcy
                var rozliczeniaHodowcy = specyfikacjeData
                    .Where(r => r.DostawcaGID == customerRealGID ||
                               zapytaniasql.PobierzInformacjeZBazyDanych<string>(r.ID, "[LibraNet].[dbo].[FarmerCalc]", "CustomerRealGID") == customerRealGID)
                    .ToList();

                if (rozliczeniaHodowcy.Count == 0)
                {
                    rozliczeniaHodowcy = specyfikacjeData.Where(r => ids.Contains(r.ID)).ToList();
                }

                // Oblicz podsumowanie
                decimal sumaNetto = 0;
                decimal sumaWartosc = 0;
                int sumaSztWszystkie = 0;

                foreach (var row in rozliczeniaHodowcy)
                {
                    sumaNetto += row.WagaNettoDoRozliczenia;
                    int sztWszystkie = row.LUMEL + row.Padle;
                    sumaSztWszystkie += sztWszystkie;
                    sumaWartosc += row.Wartosc;
                }

                DateTime dzienUbojowy = dateTimePicker1.SelectedDate ?? DateTime.Today;

                // Wygeneruj PDF
                var allIds = rozliczeniaHodowcy.Select(r => r.ID).ToList();
                GenerateShortPDFReport(allIds, showMessage: false);

                // Sciezka do PDF
                string strDzienUbojowy = dzienUbojowy.ToString("yyyy.MM.dd");
                string pdfPath = Path.Combine(defaultPdfPath, strDzienUbojowy, $"{sellerName} {strDzienUbojowy} - SKROCONY.pdf");

                // Otworz okno komponowania emaila
                var emailWindow = new EmailSpecyfikacjaWindow(
                    sellerName,
                    email,
                    dzienUbojowy,
                    sumaSztWszystkie,
                    sumaNetto,
                    sumaWartosc,
                    pdfPath);

                emailWindow.Owner = this;
                emailWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // === NOWY: Skrócona wersja PDF (1 strona) z zaokrąglonymi rogami ===
        private void GenerateShortPDFReport(List<int> ids, bool showMessage = true)
        {
            decimal sumaWartoscShort = 0, sumaKGShort = 0;

            // A4 pionowa z mniejszymi marginesami
            Document doc = new Document(PageSize.A4, 30, 30, 25, 25);

            string sellerName = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(
                zapytaniasql.PobierzInformacjeZBazyDanych<string>(ids[0], "[LibraNet].[dbo].[FarmerCalc]", "CustomerRealGID"),
                "ShortName") ?? "Nieznany";
            string customerRealGID = zapytaniasql.PobierzInformacjeZBazyDanych<string>(ids[0], "[LibraNet].[dbo].[FarmerCalc]", "CustomerRealGID");
            DateTime dzienUbojowy = zapytaniasql.PobierzInformacjeZBazyDanych<DateTime>(ids[0], "[LibraNet].[dbo].[FarmerCalc]", "CalcDate");

            string strDzienUbojowy = dzienUbojowy.ToString("yyyy.MM.dd");
            string strDzienUbojowyPL = dzienUbojowy.ToString("dd MMMM yyyy", new System.Globalization.CultureInfo("pl-PL"));

            string directoryPath = Path.Combine(defaultPdfPath, strDzienUbojowy);
            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            string filePath = Path.Combine(directoryPath, $"{sellerName} {strDzienUbojowy} - SKROCONY.pdf");

            using (FileStream fs = new FileStream(filePath, FileMode.Create))
            {
                PdfWriter writer = PdfWriter.GetInstance(doc, fs);
                doc.Open();

                // Kolory
                BaseColor greenColor = new BaseColor(92, 138, 58);
                BaseColor orangeColor = new BaseColor(245, 124, 0);
                BaseColor blueColor = new BaseColor(25, 118, 210);
                BaseColor grayColor = new BaseColor(128, 128, 128);
                BaseColor purpleColor = new BaseColor(142, 68, 173);

                // Czcionki
                BaseFont polishFont;
                string arialPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                polishFont = BaseFont.CreateFont(arialPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);

                Font titleFont = new Font(polishFont, 16, Font.BOLD, greenColor);
                Font subtitleFont = new Font(polishFont, 10, Font.NORMAL, grayColor);
                Font textFont = new Font(polishFont, 10, Font.NORMAL);
                Font textFontBold = new Font(polishFont, 10, Font.BOLD);
                Font smallFont = new Font(polishFont, 8, Font.NORMAL, grayColor);
                Font bigValueFont = new Font(polishFont, 24, Font.BOLD, greenColor);

                // === LOGO NA ŚRODKU ===
                try
                {
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string[] logoPaths = new string[] {
                        Path.Combine(baseDir, "logo-2-green.png"),
                        Path.Combine(baseDir, "..", "..", "..", "logo-2-green.png"),
                        Path.Combine(baseDir, "..", "..", "logo-2-green.png")
                    };
                    foreach (var path in logoPaths)
                    {
                        if (File.Exists(path))
                        {
                            iTextSharp.text.Image logo = iTextSharp.text.Image.GetInstance(path);
                            logo.ScaleToFit(200f, 85f); // Większe logo
                            logo.Alignment = Element.ALIGN_CENTER;
                            logo.SpacingAfter = 2f;
                            doc.Add(logo);
                            break;
                        }
                    }
                }
                catch { }

                // Tytuł - mniejszy, elegancki
                Paragraph title = new Paragraph("ROZLICZENIE - WERSJA SKRÓCONA", new Font(polishFont, 10, Font.NORMAL, grayColor));
                title.Alignment = Element.ALIGN_CENTER;
                title.SpacingAfter = 12f;
                doc.Add(title);

                // === BOX GŁÓWNY Z ZAOKRĄGLONYMI ROGAMI ===
                // Informacje o stronach
                PdfPTable infoTable = new PdfPTable(2);
                infoTable.WidthPercentage = 100;
                infoTable.SetWidths(new float[] { 1f, 1f });
                infoTable.SpacingAfter = 15f;

                // Nabywca
                PdfPCell buyerCell = CreateRoundedCell(greenColor, new BaseColor(248, 255, 248), 8);
                buyerCell.AddElement(new Paragraph("NABYWCA", new Font(polishFont, 9, Font.BOLD, greenColor)));
                buyerCell.AddElement(new Paragraph("Ubojnia Drobiu \"Piórkowscy\"", textFontBold));
                buyerCell.AddElement(new Paragraph("Koziołki 40, 95-061 Dmosin", textFont));
                infoTable.AddCell(buyerCell);

                // Sprzedający
                string sellerStreet = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(customerRealGID, "Address") ?? "";
                string sellerKod = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(customerRealGID, "PostalCode") ?? "";
                string sellerMiejsc = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(customerRealGID, "City") ?? "";

                PdfPCell sellerCell = CreateRoundedCell(orangeColor, new BaseColor(255, 253, 248), 8);
                sellerCell.AddElement(new Paragraph("SPRZEDAJĄCY", new Font(polishFont, 9, Font.BOLD, orangeColor)));
                sellerCell.AddElement(new Paragraph(sellerName, textFontBold));
                sellerCell.AddElement(new Paragraph($"{sellerStreet}, {sellerKod} {sellerMiejsc}", textFont));
                infoTable.AddCell(sellerCell);

                doc.Add(infoTable);

                // === POBIERZ GODZINY ZAŁADUNKU I POGODĘ ===
                List<DateTime> arrivalTimes = new List<DateTime>();
                foreach (int id in ids)
                {
                    try
                    {
                        DateTime arrTime = zapytaniasql.PobierzInformacjeZBazyDanych<DateTime>(id, "[LibraNet].[dbo].[FarmerCalc]", "Zaladunek");
                        if (arrTime != default) arrivalTimes.Add(arrTime);
                    }
                    catch { }
                }

                // Pobierz pogodę dla pierwszej dostawy
                WeatherInfo weatherInfo = null;
                if (arrivalTimes.Count > 0)
                {
                    weatherInfo = WeatherService.GetWeather(arrivalTimes[0]);
                }
                else
                {
                    // Użyj daty uboju o godzinie 8:00
                    weatherInfo = WeatherService.GetWeather(dzienUbojowy.Date.AddHours(8));
                }

                // Formatuj godziny załadunku
                string godzinyZaladunku = "";
                if (arrivalTimes.Count > 0)
                {
                    var sortedTimes = arrivalTimes.OrderBy(t => t).ToList();
                    if (sortedTimes.Count == 1)
                        godzinyZaladunku = $"Załadunek: {sortedTimes[0]:HH:mm}";
                    else
                        godzinyZaladunku = $"Załadunki: {sortedTimes.First():HH:mm} - {sortedTimes.Last():HH:mm}";
                }

                // === OBLICZENIA ===
                decimal sumaBrutto = 0, sumaTara = 0, sumaNetto = 0;
                int sumaSztWszystkie = 0, sumaPadle = 0, sumaKonfiskaty = 0;

                foreach (int id in ids)
                {
                    decimal? ubytekProc = zapytaniasql.PobierzInformacjeZBazyDanych<decimal?>(id, "[LibraNet].[dbo].[FarmerCalc]", "Loss");
                    decimal ubytek = ubytekProc ?? 0;

                    // Pobierz wagę hodowcy i ubojni
                    decimal nettoHodowcy = zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "NettoFarmWeight");
                    decimal nettoUbojni = zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "NettoWeight");

                    // Preferuj wagę hodowcy jeśli jest > 0, w przeciwnym razie wagę ubojni
                    bool uzyjWagiHodowcy = nettoHodowcy > 0;
                    decimal wagaBrutto = uzyjWagiHodowcy
                        ? zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "FullFarmWeight")
                        : zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "FullWeight");
                    decimal wagaTara = uzyjWagiHodowcy
                        ? zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "EmptyFarmWeight")
                        : zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "EmptyWeight");
                    decimal wagaNetto = uzyjWagiHodowcy ? nettoHodowcy : nettoUbojni;

                    int padle = zapytaniasql.PobierzInformacjeZBazyDanych<int>(id, "[LibraNet].[dbo].[FarmerCalc]", "DeclI2");
                    int konfiskaty = zapytaniasql.PobierzInformacjeZBazyDanych<int>(id, "[LibraNet].[dbo].[FarmerCalc]", "DeclI3") +
                                     zapytaniasql.PobierzInformacjeZBazyDanych<int>(id, "[LibraNet].[dbo].[FarmerCalc]", "DeclI4") +
                                     zapytaniasql.PobierzInformacjeZBazyDanych<int>(id, "[LibraNet].[dbo].[FarmerCalc]", "DeclI5");
                    int lumel = zapytaniasql.PobierzInformacjeZBazyDanych<int>(id, "[LibraNet].[dbo].[FarmerCalc]", "LumQnt");
                    int sztWszystkie = lumel + padle;  // Dostarczono = LUMEL + Padłe
                    int sztZdatne = lumel - konfiskaty;

                    bool czyPiK = zapytaniasql.PobierzInformacjeZBazyDanych<bool>(id, "[LibraNet].[dbo].[FarmerCalc]", "IncDeadConf");
                    decimal sredniaWaga = sztWszystkie > 0 ? wagaNetto / sztWszystkie : 0;
                    decimal padleKG = czyPiK ? 0 : Math.Round(padle * sredniaWaga, 0);
                    decimal konfiskatyKG = czyPiK ? 0 : Math.Round(konfiskaty * sredniaWaga, 0);
                    decimal opasienieKG = Math.Round(zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "Opasienie"), 0);
                    decimal klasaB = Math.Round(zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "KlasaB"), 0);

                    decimal doZaplaty = czyPiK
                        ? wagaNetto - opasienieKG - klasaB
                        : wagaNetto - padleKG - konfiskatyKG - opasienieKG - klasaB;

                    decimal cena = zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "Price");
                    decimal wartosc = cena * doZaplaty;

                    sumaBrutto += wagaBrutto;
                    sumaTara += wagaTara;
                    sumaNetto += wagaNetto;
                    sumaSztWszystkie += sztWszystkie;
                    sumaPadle += padle;
                    sumaKonfiskaty += konfiskaty;
                    sumaKGShort += doZaplaty;
                    sumaWartoscShort += wartosc;
                }

                decimal sredniaWagaSuma = sumaSztWszystkie > 0 ? sumaNetto / sumaSztWszystkie : 0;
                decimal avgCena = sumaKGShort > 0 ? sumaWartoscShort / sumaKGShort : 0;

                // Pobierz termin zapłaty z FarmerCalc (TerminDni), lub domyślny z Dostawcy
                int? terminDniFromCalc = zapytaniasql.PobierzInformacjeZBazyDanych<int?>(ids[0], "[LibraNet].[dbo].[FarmerCalc]", "TerminDni");
                int terminZaplatyDni = terminDniFromCalc ?? zapytaniasql.GetTerminZaplaty(customerRealGID);
                DateTime terminPlatnosci = dzienUbojowy.AddDays(terminZaplatyDni);

                // === GŁÓWNE DANE - DUŻY BOX ===
                PdfPTable mainBox = new PdfPTable(1);
                mainBox.WidthPercentage = 100;
                mainBox.SpacingBefore = 10f;
                mainBox.SpacingAfter = 15f;

                PdfPCell mainCell = CreateRoundedCell(greenColor, new BaseColor(245, 255, 245), 15);

                // Data i numer dokumentu
                Paragraph dateInfo = new Paragraph($"Data uboju: {strDzienUbojowyPL}  |  Dokument: {strDzienUbojowy}/{ids.Count}  |  Dostaw: {ids.Count}", new Font(polishFont, 9, Font.NORMAL, grayColor));
                dateInfo.Alignment = Element.ALIGN_CENTER;
                mainCell.AddElement(dateInfo);

                // Godzina załadunku i pogoda
                string infoLine = "";
                if (!string.IsNullOrEmpty(godzinyZaladunku))
                    infoLine += godzinyZaladunku;
                if (weatherInfo != null)
                {
                    if (!string.IsNullOrEmpty(infoLine)) infoLine += "  |  ";
                    infoLine += $"Pogoda: {weatherInfo.Temperature:0.0}°C, {weatherInfo.Description}";
                }
                if (!string.IsNullOrEmpty(infoLine))
                {
                    Paragraph weatherLine = new Paragraph(infoLine, new Font(polishFont, 8, Font.ITALIC, new BaseColor(100, 100, 100)));
                    weatherLine.Alignment = Element.ALIGN_CENTER;
                    mainCell.AddElement(weatherLine);
                }

                // Separator
                mainCell.AddElement(new Paragraph(" ", new Font(polishFont, 5, Font.NORMAL)));

                // Główna wartość
                Paragraph mainValue = new Paragraph($"DO WYPŁATY: {sumaWartoscShort:N0} zł", new Font(polishFont, 28, Font.BOLD, greenColor));
                mainValue.Alignment = Element.ALIGN_CENTER;
                mainCell.AddElement(mainValue);

                // Termin płatności (opcjonalnie)
                if (_drukujTerminPlatnosci)
                {
                    Paragraph terminInfo = new Paragraph($"Termin płatności: {terminPlatnosci:dd.MM.yyyy} ({terminZaplatyDni} dni)", new Font(polishFont, 10, Font.ITALIC, grayColor));
                    terminInfo.Alignment = Element.ALIGN_CENTER;
                    mainCell.AddElement(terminInfo);
                }

                mainBox.AddCell(mainCell);
                doc.Add(mainBox);

                // === SZCZEGÓŁY W 3 KOLUMNACH ===
                PdfPTable detailsTable = new PdfPTable(3);
                detailsTable.WidthPercentage = 100;
                detailsTable.SetWidths(new float[] { 1f, 1f, 1f });
                detailsTable.SpacingAfter = 15f;

                // Kolumna 1 - Waga
                PdfPCell col1 = CreateRoundedCell(new BaseColor(76, 175, 80), new BaseColor(232, 245, 233), 10);
                col1.AddElement(new Paragraph("WAGA", new Font(polishFont, 10, Font.BOLD, new BaseColor(76, 175, 80))) { Alignment = Element.ALIGN_CENTER });
                col1.AddElement(new Paragraph($"Brutto: {sumaBrutto:N0} kg", textFont));
                col1.AddElement(new Paragraph($"Tara: {sumaTara:N0} kg", textFont));
                col1.AddElement(new Paragraph($"Netto: {sumaNetto:N0} kg", textFontBold));
                detailsTable.AddCell(col1);

                // Kolumna 2 - Sztuki
                PdfPCell col2 = CreateRoundedCell(orangeColor, new BaseColor(255, 243, 224), 10);
                col2.AddElement(new Paragraph("SZTUKI", new Font(polishFont, 10, Font.BOLD, orangeColor)) { Alignment = Element.ALIGN_CENTER });
                col2.AddElement(new Paragraph($"Dostarczono: {sumaSztWszystkie} szt", textFont));
                col2.AddElement(new Paragraph($"Padłe: {sumaPadle} szt", textFont));
                col2.AddElement(new Paragraph($"Konfiskaty: {sumaKonfiskaty} szt", textFont));
                detailsTable.AddCell(col2);

                // Kolumna 3 - Podsumowanie
                PdfPCell col3 = CreateRoundedCell(purpleColor, new BaseColor(243, 229, 245), 10);
                col3.AddElement(new Paragraph("PODSUMOWANIE", new Font(polishFont, 10, Font.BOLD, purpleColor)) { Alignment = Element.ALIGN_CENTER });
                col3.AddElement(new Paragraph($"Śr. waga: {sredniaWagaSuma:N2} kg/szt", textFont));
                col3.AddElement(new Paragraph($"Cena: {avgCena:0.00} zł/kg", textFont));
                col3.AddElement(new Paragraph($"Do zapłaty: {sumaKGShort:N0} kg", textFontBold));
                detailsTable.AddCell(col3);

                doc.Add(detailsTable);

                // === PODPISY (kompaktowe) ===
                PdfPTable sigTable = new PdfPTable(2);
                sigTable.WidthPercentage = 100;
                sigTable.SpacingBefore = 15f; // Mniejszy odstęp

                PdfPCell sig1 = CreateRoundedCell(new BaseColor(200, 200, 200), new BaseColor(252, 252, 252), 8);
                sig1.AddElement(new Paragraph("PODPIS DOSTAWCY", new Font(polishFont, 8, Font.BOLD, orangeColor)) { Alignment = Element.ALIGN_CENTER });
                sig1.AddElement(new Paragraph(" \n", textFont));
                sig1.AddElement(new Paragraph("........................................", textFont) { Alignment = Element.ALIGN_CENTER });
                sigTable.AddCell(sig1);

                NazwaZiD nazwaZiD = new NazwaZiD();
                string wystawiajacyNazwa = nazwaZiD.GetNameById(App.UserID) ?? "---";

                PdfPCell sig2 = CreateRoundedCell(new BaseColor(200, 200, 200), new BaseColor(252, 252, 252), 8);
                sig2.AddElement(new Paragraph("PODPIS PRACOWNIKA", new Font(polishFont, 8, Font.BOLD, greenColor)) { Alignment = Element.ALIGN_CENTER });
                sig2.AddElement(new Paragraph(" \n", textFont));
                sig2.AddElement(new Paragraph("........................................", textFont) { Alignment = Element.ALIGN_CENTER });
                sigTable.AddCell(sig2);

                doc.Add(sigTable);

                // Stopka
                Paragraph footer = new Paragraph($"Wygenerowano przez: {wystawiajacyNazwa}", smallFont);
                footer.Alignment = Element.ALIGN_RIGHT;
                footer.SpacingBefore = 15f;
                doc.Add(footer);

                doc.Close();
            }

            // Zapisz historię PDF do bazy danych
            int? dostawcaGID = zapytaniasql.PobierzInformacjeZBazyDanych<int?>(ids[0], "[LibraNet].[dbo].[FarmerCalc]", "CustomerGID");
            SavePdfHistory(ids, dostawcaGID, sellerName, dzienUbojowy, filePath);

            if (showMessage)
            {
                MessageBox.Show($"Wygenerowano skrócony PDF:\n{Path.GetFileName(filePath)}", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // Helper: Tworzenie komórki z zaokrąglonymi rogami
        private PdfPCell CreateRoundedCell(BaseColor borderColor, BaseColor bgColor, float padding)
        {
            PdfPCell cell = new PdfPCell
            {
                Border = PdfPCell.BOX,
                BorderColor = borderColor,
                BorderWidth = 1.5f,
                BackgroundColor = bgColor,
                Padding = padding,
                PaddingBottom = padding + 5
            };
            // Uwaga: iTextSharp 5.x nie obsługuje natywnie zaokrąglonych rogów w komórkach
            // Zaokrąglone rogi wymagałyby użycia PdfContentByte do rysowania
            return cell;
        }

        private void GeneratePDFReport(List<int> ids, bool showMessage = true)
        {
            sumaWartosc = 0;
            sumaKG = 0;

            // A4 pionowa - mniejsze marginesy dla lepszego wykorzystania strony
            Document doc = new Document(PageSize.A4, 15, 15, 15, 15);

            string sellerName = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(
                zapytaniasql.PobierzInformacjeZBazyDanych<string>(ids[0], "[LibraNet].[dbo].[FarmerCalc]", "CustomerGID"),
                "ShortName") ?? "Nieznany";
            DateTime dzienUbojowy = zapytaniasql.PobierzInformacjeZBazyDanych<DateTime>(
                ids[0], "[LibraNet].[dbo].[FarmerCalc]", "CalcDate");

            string strDzienUbojowy = dzienUbojowy.ToString("yyyy.MM.dd");
            string strDzienUbojowyPL = dzienUbojowy.ToString("dd MMMM yyyy", new System.Globalization.CultureInfo("pl-PL"));

            // Wybierz ścieżkę
            string directoryPath;
            if (useDefaultPath)
            {
                directoryPath = Path.Combine(defaultPdfPath, strDzienUbojowy);
            }
            else
            {
                var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Wybierz folder do zapisania PDF",
                    SelectedPath = defaultPdfPath
                };

                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    return;

                directoryPath = Path.Combine(dialog.SelectedPath, strDzienUbojowy);
                useDefaultPath = true;
            }

            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            string filePath = Path.Combine(directoryPath, $"{sellerName} {strDzienUbojowy}.pdf");

            using (FileStream fs = new FileStream(filePath, FileMode.Create))
            {
                PdfWriter writer = PdfWriter.GetInstance(doc, fs);
                doc.Open();

                // Kolory firmowe
                BaseColor greenColor = new BaseColor(92, 138, 58);      // #5C8A3A
                BaseColor darkGreenColor = new BaseColor(75, 115, 47);  // #4B732F
                BaseColor lightGreenColor = new BaseColor(200, 230, 201); // #C8E6C9
                BaseColor orangeColor = new BaseColor(245, 124, 0);     // #F57C00
                BaseColor blueColor = new BaseColor(25, 118, 210);      // #1976D2
                BaseColor grayColor = new BaseColor(128, 128, 128);

                // === CZCIONKA Z POLSKIMI ZNAKAMI ===
                // Próbuj załadować Arial, jeśli nie - użyj systemowej czcionki z polskimi znakami
                BaseFont polishFont;
                try
                {
                    string arialPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                    if (File.Exists(arialPath))
                    {
                        polishFont = BaseFont.CreateFont(arialPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                    }
                    else
                    {
                        // Fallback do czcionki systemowej
                        polishFont = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1250, BaseFont.EMBEDDED);
                    }
                }
                catch
                {
                    polishFont = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1250, BaseFont.EMBEDDED);
                }

                // Fonty z polską czcionką - dostosowane do A4 pionowego
                Font titleFont = new Font(polishFont, 16, Font.BOLD, greenColor);
                Font subtitleFont = new Font(polishFont, 10, Font.NORMAL, BaseColor.DARK_GRAY);
                Font headerFont = new Font(polishFont, 9, Font.BOLD, BaseColor.WHITE);
                Font textFont = new Font(polishFont, 8, Font.NORMAL);
                Font textFontBold = new Font(polishFont, 8, Font.BOLD);
                Font smallTextFont = new Font(polishFont, 6, Font.NORMAL);  // Mniejsza czcionka dla tabeli
                Font smallTextFontBold = new Font(polishFont, 6, Font.BOLD);
                Font tytulTablicy = new Font(polishFont, 7, Font.BOLD, BaseColor.WHITE);
                Font legendaFont = new Font(polishFont, 7, Font.NORMAL, grayColor);
                Font legendaBoldFont = new Font(polishFont, 7, Font.BOLD, BaseColor.DARK_GRAY);

                // === NAGŁÓWEK Z LOGO NA ŚRODKU ===
                decimal? ubytekProcFirst = zapytaniasql.PobierzInformacjeZBazyDanych<decimal?>(ids[0], "[LibraNet].[dbo].[FarmerCalc]", "Loss");
                string wagaTyp = (ubytekProcFirst ?? 0) != 0 ? "Waga loco Hodowca" : "Waga loco Ubojnia";

                // Logo wycentrowane na górze
                try
                {
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string[] logoPaths = new string[]
                    {
                        Path.Combine(baseDir, "logo-2-green.png"),
                        Path.Combine(baseDir, "Resources", "logo-2-green.png"),
                        Path.Combine(baseDir, "Images", "logo-2-green.png"),
                        Path.Combine(baseDir, "..", "..", "..", "logo-2-green.png"),
                        Path.Combine(baseDir, "..", "..", "logo-2-green.png"),
                        @"C:\logo-2-green.png"
                    };

                    string foundLogoPath = null;
                    foreach (var path in logoPaths)
                    {
                        if (File.Exists(path))
                        {
                            foundLogoPath = path;
                            break;
                        }
                    }

                    if (foundLogoPath != null)
                    {
                        // === NAGŁÓWEK: LOGO PO LEWEJ, TYTUŁ PO PRAWEJ ===
                        PdfPTable headerLogoTable = new PdfPTable(2);
                        headerLogoTable.WidthPercentage = 100;
                        headerLogoTable.SetWidths(new float[] { 1f, 1.8f });
                        headerLogoTable.SpacingAfter = 10f;

                        // Logo (lewa strona)
                        iTextSharp.text.Image logo = iTextSharp.text.Image.GetInstance(foundLogoPath);
                        logo.ScaleToFit(200f, 75f);
                        PdfPCell logoCell = new PdfPCell(logo) { Border = PdfPCell.NO_BORDER, HorizontalAlignment = Element.ALIGN_LEFT, VerticalAlignment = Element.ALIGN_MIDDLE, PaddingRight = 5f };
                        headerLogoTable.AddCell(logoCell);

                        // Tytuł i informacje (prawa strona)
                        PdfPCell titleCell = new PdfPCell { Border = PdfPCell.NO_BORDER, VerticalAlignment = Element.ALIGN_MIDDLE };
                        titleCell.AddElement(new Paragraph("ROZLICZENIE PRZYJĘTEGO DROBIU", new Font(polishFont, 14, Font.BOLD, greenColor)) { Alignment = Element.ALIGN_LEFT });
                        titleCell.AddElement(new Paragraph($"Data uboju: {strDzienUbojowyPL}", new Font(polishFont, 10, Font.NORMAL, grayColor)) { Alignment = Element.ALIGN_LEFT });
                        titleCell.AddElement(new Paragraph($"Dokument nr: {strDzienUbojowy}/{ids.Count}  |  Ilość dostaw: {ids.Count}", new Font(polishFont, 9, Font.NORMAL, grayColor)) { Alignment = Element.ALIGN_LEFT });
                        headerLogoTable.AddCell(titleCell);

                        doc.Add(headerLogoTable);
                    }
                    else
                    {
                        // Fallback bez logo - tytuł na środku
                        Paragraph firmName = new Paragraph("PIÓRKOWSCY", new Font(polishFont, 26, Font.BOLD, greenColor));
                        firmName.Alignment = Element.ALIGN_CENTER;
                        doc.Add(firmName);
                        Paragraph mainTitle = new Paragraph("ROZLICZENIE PRZYJĘTEGO DROBIU", new Font(polishFont, 14, Font.BOLD, greenColor));
                        mainTitle.Alignment = Element.ALIGN_CENTER;
                        mainTitle.SpacingAfter = 5f;
                        doc.Add(mainTitle);
                        Paragraph docInfo = new Paragraph($"Data uboju: {strDzienUbojowyPL}  |  Dokument nr: {strDzienUbojowy}/{ids.Count}", new Font(polishFont, 9, Font.NORMAL, grayColor));
                        docInfo.Alignment = Element.ALIGN_CENTER;
                        docInfo.SpacingAfter = 10f;
                        doc.Add(docInfo);
                    }
                }
                catch
                {
                    Paragraph firmName = new Paragraph("PIÓRKOWSCY", new Font(polishFont, 26, Font.BOLD, greenColor));
                    firmName.Alignment = Element.ALIGN_CENTER;
                    doc.Add(firmName);
                    Paragraph mainTitle = new Paragraph("ROZLICZENIE PRZYJĘTEGO DROBIU", new Font(polishFont, 14, Font.BOLD, greenColor));
                    mainTitle.Alignment = Element.ALIGN_CENTER;
                    mainTitle.SpacingAfter = 10f;
                    doc.Add(mainTitle);
                }

                // === SEKCJA STRON (NABYWCA / SPRZEDAJĄCY) ===
                PdfPTable partiesTable = new PdfPTable(2);
                partiesTable.WidthPercentage = 100;
                partiesTable.SetWidths(new float[] { 1f, 1f });
                partiesTable.SpacingAfter = 12f;

                // Nabywca (lewa strona)
                PdfPCell buyerCell = new PdfPCell { Border = PdfPCell.BOX, BorderColor = greenColor, BorderWidth = 1.5f, Padding = 10, BackgroundColor = new BaseColor(248, 255, 248) };
                buyerCell.AddElement(new Paragraph("NABYWCA", new Font(polishFont, 10, Font.BOLD, greenColor)));
                buyerCell.AddElement(new Paragraph("Ubojnia Drobiu \"Piórkowscy\"", textFontBold));
                buyerCell.AddElement(new Paragraph("Koziołki 40, 95-061 Dmosin", textFont));
                buyerCell.AddElement(new Paragraph("NIP: 726-162-54-06", textFont));
                partiesTable.AddCell(buyerCell);

                // Sprzedający (prawa strona)
                string customerRealGID = zapytaniasql.PobierzInformacjeZBazyDanych<string>(ids[0], "[LibraNet].[dbo].[FarmerCalc]", "CustomerRealGID");
                string sellerStreet = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(customerRealGID, "Address") ?? "";
                string sellerKod = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(customerRealGID, "PostalCode") ?? "";
                string sellerMiejsc = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(customerRealGID, "City") ?? "";

                // Pobierz termin zapłaty z FarmerCalc (TerminDni), lub domyślny z Dostawcy
                int? terminDniFromCalc = zapytaniasql.PobierzInformacjeZBazyDanych<int?>(ids[0], "[LibraNet].[dbo].[FarmerCalc]", "TerminDni");
                int terminZaplatyDni = terminDniFromCalc ?? zapytaniasql.GetTerminZaplaty(customerRealGID);

                DateTime terminPlatnosci = dzienUbojowy.AddDays(terminZaplatyDni);

                PdfPCell sellerCell = new PdfPCell { Border = PdfPCell.BOX, BorderColor = orangeColor, BorderWidth = 1.5f, Padding = 10, BackgroundColor = new BaseColor(255, 253, 248) };
                sellerCell.AddElement(new Paragraph("SPRZEDAJĄCY (Hodowca)", new Font(polishFont, 10, Font.BOLD, orangeColor)));
                sellerCell.AddElement(new Paragraph(sellerName, textFontBold));
                sellerCell.AddElement(new Paragraph(sellerStreet, textFont));
                sellerCell.AddElement(new Paragraph($"{sellerKod} {sellerMiejsc}", textFont));
                sellerCell.AddElement(new Paragraph(wagaTyp, textFont));
                partiesTable.AddCell(sellerCell);

                doc.Add(partiesTable);

                // === MINI TABELA ZAŁADUNKÓW I POGODY ===
                if (ids.Count > 0)
                {
                    Paragraph zaladunkiTitle = new Paragraph("INFORMACJE O DOSTAWACH", new Font(polishFont, 9, Font.BOLD, new BaseColor(52, 73, 94)));
                    zaladunkiTitle.SpacingAfter = 4f;
                    doc.Add(zaladunkiTitle);

                    PdfPTable zaladunkiTable = new PdfPTable(9);
                    zaladunkiTable.WidthPercentage = 100;
                    zaladunkiTable.SetWidths(new float[] { 0.35f, 0.9f, 0.9f, 0.9f, 0.9f, 1.2f, 0.9f, 0.9f, 1.5f });
                    zaladunkiTable.SpacingAfter = 10f;

                    // Nagłówek tabeli
                    BaseColor zHeaderBg = new BaseColor(52, 73, 94);
                    Font zHeaderFont = new Font(polishFont, 7, Font.BOLD, BaseColor.WHITE);

                    PdfPCell hLp = new PdfPCell(new Phrase("Lp", zHeaderFont)) { BackgroundColor = zHeaderBg, HorizontalAlignment = Element.ALIGN_CENTER, Padding = 3 };
                    PdfPCell hPrzyjazd = new PdfPCell(new Phrase("Przyjazd", zHeaderFont)) { BackgroundColor = zHeaderBg, HorizontalAlignment = Element.ALIGN_CENTER, Padding = 3 };
                    PdfPCell hZal = new PdfPCell(new Phrase("Załadunek", zHeaderFont)) { BackgroundColor = zHeaderBg, HorizontalAlignment = Element.ALIGN_CENTER, Padding = 3 };
                    PdfPCell hZalKoniec = new PdfPCell(new Phrase("Koniec", zHeaderFont)) { BackgroundColor = zHeaderBg, HorizontalAlignment = Element.ALIGN_CENTER, Padding = 3 };
                    PdfPCell hWyjazd = new PdfPCell(new Phrase("Wyjazd", zHeaderFont)) { BackgroundColor = zHeaderBg, HorizontalAlignment = Element.ALIGN_CENTER, Padding = 3 };
                    PdfPCell hKierowca = new PdfPCell(new Phrase("Kierowca", zHeaderFont)) { BackgroundColor = zHeaderBg, HorizontalAlignment = Element.ALIGN_CENTER, Padding = 3 };
                    PdfPCell hCiagnik = new PdfPCell(new Phrase("Ciągnik", zHeaderFont)) { BackgroundColor = zHeaderBg, HorizontalAlignment = Element.ALIGN_CENTER, Padding = 3 };
                    PdfPCell hNaczepa = new PdfPCell(new Phrase("Naczepa", zHeaderFont)) { BackgroundColor = zHeaderBg, HorizontalAlignment = Element.ALIGN_CENTER, Padding = 3 };
                    PdfPCell hPogoda = new PdfPCell(new Phrase("Pogoda", zHeaderFont)) { BackgroundColor = zHeaderBg, HorizontalAlignment = Element.ALIGN_CENTER, Padding = 3 };
                    zaladunkiTable.AddCell(hLp);
                    zaladunkiTable.AddCell(hPrzyjazd);
                    zaladunkiTable.AddCell(hZal);
                    zaladunkiTable.AddCell(hZalKoniec);
                    zaladunkiTable.AddCell(hWyjazd);
                    zaladunkiTable.AddCell(hKierowca);
                    zaladunkiTable.AddCell(hCiagnik);
                    zaladunkiTable.AddCell(hNaczepa);
                    zaladunkiTable.AddCell(hPogoda);

                    // Wiersze dla każdej dostawy
                    Font zCellFont = new Font(polishFont, 7, Font.NORMAL);
                    BaseColor altBg = new BaseColor(245, 247, 250);

                    for (int idx = 0; idx < ids.Count; idx++)
                    {
                        int deliveryId = ids[idx];
                        BaseColor rowBg = (idx % 2 == 0) ? BaseColor.WHITE : altBg;

                        // Pobierz daty przyjazdu i załadunku
                        DateTime przyjazdTime = default;
                        DateTime zaladunekTime = default;
                        DateTime zaladunekKoniecTime = default;
                        DateTime wyjazdHodowcaTime = default;
                        try
                        {
                            przyjazdTime = zapytaniasql.PobierzInformacjeZBazyDanych<DateTime>(deliveryId, "[LibraNet].[dbo].[FarmerCalc]", "Przyjazd");
                            zaladunekTime = zapytaniasql.PobierzInformacjeZBazyDanych<DateTime>(deliveryId, "[LibraNet].[dbo].[FarmerCalc]", "Zaladunek");
                            zaladunekKoniecTime = zapytaniasql.PobierzInformacjeZBazyDanych<DateTime>(deliveryId, "[LibraNet].[dbo].[FarmerCalc]", "ZaladunekKoniec");
                            wyjazdHodowcaTime = zapytaniasql.PobierzInformacjeZBazyDanych<DateTime>(deliveryId, "[LibraNet].[dbo].[FarmerCalc]", "WyjazdHodowca");
                        }
                        catch { }

                        // Pobierz dane pojazdu i kierowcy
                        string ciagnikNr = "";
                        string naczepaNr = "";
                        string kierowcaNazwa = "";
                        try
                        {
                            ciagnikNr = zapytaniasql.PobierzInformacjeZBazyDanych<string>(deliveryId, "[LibraNet].[dbo].[FarmerCalc]", "CarID") ?? "";
                            naczepaNr = zapytaniasql.PobierzInformacjeZBazyDanych<string>(deliveryId, "[LibraNet].[dbo].[FarmerCalc]", "TrailerID") ?? "";
                            int driverGID = zapytaniasql.PobierzInformacjeZBazyDanych<int>(deliveryId, "[LibraNet].[dbo].[FarmerCalc]", "DriverGID");
                            if (driverGID > 0)
                                kierowcaNazwa = zapytaniasql.ZnajdzNazweKierowcy(driverGID) ?? $"#{driverGID}";
                        }
                        catch { }

                        // Pobierz pogodę dla daty załadunku
                        string pogodaStr = "-";
                        if (zaladunekTime != default && zaladunekTime.Year > 2000)
                        {
                            try
                            {
                                WeatherInfo pogodaZal = WeatherService.GetWeather(zaladunekTime);
                                if (pogodaZal != null)
                                    pogodaStr = $"{pogodaZal.Temperature:0.0}°C, {pogodaZal.Description}";
                            }
                            catch { }
                        }

                        // Formatuj daty (tylko godzina jeśli ten sam dzień)
                        string przyjazdStr = (przyjazdTime != default && przyjazdTime.Year > 2000) ? przyjazdTime.ToString("HH:mm") : "-";
                        string zaladunekStr = (zaladunekTime != default && zaladunekTime.Year > 2000) ? zaladunekTime.ToString("HH:mm") : "-";
                        string zaladunekKoniecStr = (zaladunekKoniecTime != default && zaladunekKoniecTime.Year > 2000) ? zaladunekKoniecTime.ToString("HH:mm") : "-";
                        string wyjazdStr = (wyjazdHodowcaTime != default && wyjazdHodowcaTime.Year > 2000) ? wyjazdHodowcaTime.ToString("HH:mm") : "-";

                        // Dodaj komórki
                        PdfPCell cLp = new PdfPCell(new Phrase((idx + 1).ToString(), zCellFont)) { BackgroundColor = rowBg, HorizontalAlignment = Element.ALIGN_CENTER, Padding = 2 };
                        PdfPCell cPrzyjazd = new PdfPCell(new Phrase(przyjazdStr, zCellFont)) { BackgroundColor = rowBg, HorizontalAlignment = Element.ALIGN_CENTER, Padding = 2 };
                        PdfPCell cZal = new PdfPCell(new Phrase(zaladunekStr, zCellFont)) { BackgroundColor = rowBg, HorizontalAlignment = Element.ALIGN_CENTER, Padding = 2 };
                        PdfPCell cZalKoniec = new PdfPCell(new Phrase(zaladunekKoniecStr, zCellFont)) { BackgroundColor = rowBg, HorizontalAlignment = Element.ALIGN_CENTER, Padding = 2 };
                        PdfPCell cWyjazd = new PdfPCell(new Phrase(wyjazdStr, zCellFont)) { BackgroundColor = rowBg, HorizontalAlignment = Element.ALIGN_CENTER, Padding = 2 };
                        PdfPCell cKierowca = new PdfPCell(new Phrase(kierowcaNazwa, zCellFont)) { BackgroundColor = rowBg, HorizontalAlignment = Element.ALIGN_LEFT, PaddingLeft = 3, Padding = 2 };
                        PdfPCell cCiagnik = new PdfPCell(new Phrase(ciagnikNr, zCellFont)) { BackgroundColor = rowBg, HorizontalAlignment = Element.ALIGN_CENTER, Padding = 2 };
                        PdfPCell cNaczepa = new PdfPCell(new Phrase(naczepaNr, zCellFont)) { BackgroundColor = rowBg, HorizontalAlignment = Element.ALIGN_CENTER, Padding = 2 };
                        PdfPCell cPogoda = new PdfPCell(new Phrase(pogodaStr, zCellFont)) { BackgroundColor = rowBg, HorizontalAlignment = Element.ALIGN_LEFT, PaddingLeft = 3, Padding = 2 };

                        zaladunkiTable.AddCell(cLp);
                        zaladunkiTable.AddCell(cPrzyjazd);
                        zaladunkiTable.AddCell(cZal);
                        zaladunkiTable.AddCell(cZalKoniec);
                        zaladunkiTable.AddCell(cWyjazd);
                        zaladunkiTable.AddCell(cKierowca);
                        zaladunkiTable.AddCell(cCiagnik);
                        zaladunkiTable.AddCell(cNaczepa);
                        zaladunkiTable.AddCell(cPogoda);
                    }

                    doc.Add(zaladunkiTable);
                }

                // === GŁÓWNA TABELA ROZLICZENIA === (dostosowana do A4 pionowego)
                // 18 kolumn: Lp, Brutto, Tara, Netto | Dostarcz, Padłe, Konf, Zdatne | kg/szt | Netto, Padłe, Konf, Ubytek, Opas, KlB, DoZapł, Cena, Wartość
                // Brutto/Tara/Netto szersze, Kl.B/Cena/Śr.Waga węższe
                PdfPTable dataTable = new PdfPTable(new float[] { 0.3F, 0.6F, 0.6F, 0.65F, 0.5F, 0.4F, 0.45F, 0.45F, 0.45F, 0.55F, 0.5F, 0.5F, 0.5F, 0.5F, 0.35F, 0.55F, 0.4F, 0.65F });
                dataTable.WidthPercentage = 100;

                // Nagłówki grupowe z kolorami
                BaseColor purpleColor = new BaseColor(142, 68, 173);
                AddColoredMergedHeader(dataTable, "WAGA [kg]", tytulTablicy, 4, greenColor);
                AddColoredMergedHeader(dataTable, "ROZLICZENIE SZTUK [szt.]", tytulTablicy, 4, orangeColor);
                AddColoredMergedHeader(dataTable, "ŚR. WAGA", tytulTablicy, 1, purpleColor);
                AddColoredMergedHeader(dataTable, "ROZLICZENIE KILOGRAMÓW [kg]", tytulTablicy, 9, blueColor);

                // Nagłówki kolumn - WAGA
                AddColoredTableHeader(dataTable, "Lp.", smallTextFontBold, darkGreenColor);
                AddColoredTableHeader(dataTable, "Brutto", smallTextFontBold, darkGreenColor);
                AddColoredTableHeader(dataTable, "Tara", smallTextFontBold, darkGreenColor);
                AddColoredTableHeader(dataTable, "Netto", smallTextFontBold, darkGreenColor);
                // Nagłówki kolumn - SZTUKI
                AddDostarczoneHeader(dataTable, smallTextFontBold, new BaseColor(230, 126, 34));
                AddColoredTableHeader(dataTable, "Padłe", smallTextFontBold, new BaseColor(230, 126, 34));
                AddColoredTableHeader(dataTable, "Konf.", smallTextFontBold, new BaseColor(230, 126, 34));
                AddColoredTableHeader(dataTable, "Zdatne", smallTextFontBold, new BaseColor(230, 126, 34));
                // Nagłówek kolumny - ŚREDNIA WAGA
                AddColoredTableHeader(dataTable, "kg/szt", smallTextFontBold, purpleColor);
                // Nagłówki kolumn - KILOGRAMY
                AddColoredTableHeader(dataTable, "Netto", smallTextFontBold, new BaseColor(41, 128, 185));
                AddColoredTableHeader(dataTable, "Padłe", smallTextFontBold, new BaseColor(41, 128, 185));
                AddColoredTableHeader(dataTable, "Konf.", smallTextFontBold, new BaseColor(41, 128, 185));
                AddColoredTableHeader(dataTable, "Ubytek", smallTextFontBold, new BaseColor(41, 128, 185));
                AddColoredTableHeader(dataTable, "Opas.", smallTextFontBold, new BaseColor(41, 128, 185));
                AddColoredTableHeader(dataTable, "Kl.B", smallTextFontBold, new BaseColor(41, 128, 185));
                AddColoredTableHeader(dataTable, "Do zapł.", smallTextFontBold, new BaseColor(41, 128, 185));
                AddColoredTableHeader(dataTable, "Cena", smallTextFontBold, new BaseColor(41, 128, 185));
                AddColoredTableHeader(dataTable, "Wartość", smallTextFontBold, new BaseColor(41, 128, 185));

                // Zmienne do sumowania
                decimal sumaBrutto = 0, sumaTara = 0, sumaNetto = 0, sumaPadleKG = 0, sumaKonfiskatyKG = 0, sumaOpasienieKG = 0, sumaKlasaB = 0;
                int sumaSztWszystkie = 0, sumaPadle = 0, sumaKonfiskaty = 0, sumaSztZdatne = 0;
                decimal sredniaWagaSuma = 0;
                bool czyByloUbytku = false;
                decimal sumaUbytekKG = 0; // Suma kg odliczonych przez Ubytek %
                decimal ubytekProcWyswietlany = 0; // Procent ubytku do wyświetlenia w formule

                // Dane tabeli
                for (int i = 0; i < ids.Count; i++)
                {
                    int id = ids[i];
                    bool czyPiK = zapytaniasql.PobierzInformacjeZBazyDanych<bool>(id, "[LibraNet].[dbo].[FarmerCalc]", "IncDeadConf");
                    decimal? ubytekProc = zapytaniasql.PobierzInformacjeZBazyDanych<decimal?>(id, "[LibraNet].[dbo].[FarmerCalc]", "Loss");
                    decimal ubytek = ubytekProc ?? 0;

                    // Pobierz wagę hodowcy i ubojni
                    decimal nettoHodowcy = zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "NettoFarmWeight");
                    decimal nettoUbojni = zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "NettoWeight");

                    // Preferuj wagę hodowcy jeśli jest > 0, w przeciwnym razie wagę ubojni
                    bool uzyjWagiHodowcy = nettoHodowcy > 0;
                    decimal wagaBrutto, wagaTara, wagaNetto;
                    if (uzyjWagiHodowcy)
                    {
                        wagaBrutto = zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "FullFarmWeight");
                        wagaTara = zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "EmptyFarmWeight");
                        wagaNetto = nettoHodowcy;
                    }
                    else
                    {
                        wagaBrutto = zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "FullWeight");
                        wagaTara = zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "EmptyWeight");
                        wagaNetto = nettoUbojni;
                    }

                    int padle = zapytaniasql.PobierzInformacjeZBazyDanych<int>(id, "[LibraNet].[dbo].[FarmerCalc]", "DeclI2");
                    int konfiskaty = zapytaniasql.PobierzInformacjeZBazyDanych<int>(id, "[LibraNet].[dbo].[FarmerCalc]", "DeclI3") +
                                     zapytaniasql.PobierzInformacjeZBazyDanych<int>(id, "[LibraNet].[dbo].[FarmerCalc]", "DeclI4") +
                                     zapytaniasql.PobierzInformacjeZBazyDanych<int>(id, "[LibraNet].[dbo].[FarmerCalc]", "DeclI5");
                    int lumel = zapytaniasql.PobierzInformacjeZBazyDanych<int>(id, "[LibraNet].[dbo].[FarmerCalc]", "LumQnt");
                    int sztWszystkie = lumel + padle;  // Dostarczono = LUMEL + Padłe
                    int sztZdatne = lumel - konfiskaty;

                    decimal sredniaWaga = sztWszystkie > 0 ? wagaNetto / sztWszystkie : 0;
                    decimal padleKG = czyPiK ? 0 : Math.Round(padle * sredniaWaga, 0);
                    decimal konfiskatyKG = czyPiK ? 0 : Math.Round(konfiskaty * sredniaWaga, 0);
                    decimal opasienieKG = Math.Round(zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "Opasienie"), 0);
                    decimal klasaB = Math.Round(zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "KlasaB"), 0);

                    // Obliczenie Ubytek KG = Netto × Ubytek% (Loss w bazie jest już ułamkiem, np. 0.0025 = 0.25%)
                    decimal ubytekKG = Math.Round(wagaNetto * ubytek, 0);

                    // Obliczenie DoZaplaty: Netto - Padłe - Konf - Ubytek - Opasienie - KlasaB
                    decimal doZaplaty = czyPiK
                        ? wagaNetto - ubytekKG - opasienieKG - klasaB
                        : wagaNetto - padleKG - konfiskatyKG - ubytekKG - opasienieKG - klasaB;

                    decimal cenaBase = zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "Price");
                    decimal dodatek = zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "Addition");
                    decimal cena = cenaBase + dodatek; // Cena zawiera dodatek
                    decimal wartosc = cena * doZaplaty;

                    // Śledzenie Ubytku
                    if (ubytek > 0)
                    {
                        czyByloUbytku = true;
                        ubytekProcWyswietlany = ubytek; // Zapisz procent ubytku
                    }
                    sumaUbytekKG += ubytekKG;

                    // Sumowanie
                    sumaWartosc += wartosc;
                    sumaKG += doZaplaty;
                    sumaBrutto += wagaBrutto;
                    sumaTara += wagaTara;
                    sumaNetto += wagaNetto;
                    sumaPadleKG += padleKG;
                    sumaKonfiskatyKG += konfiskatyKG;
                    sumaOpasienieKG += opasienieKG;
                    sumaKlasaB += klasaB;
                    sumaSztWszystkie += sztWszystkie;
                    sumaPadle += padle;
                    sumaKonfiskaty += konfiskaty;
                    sumaSztZdatne += sztZdatne;
                    sredniaWagaSuma = sumaSztWszystkie > 0 ? sumaNetto / sumaSztWszystkie : 0;

                    BaseColor rowColor = i % 2 == 0 ? BaseColor.WHITE : new BaseColor(248, 248, 248);

                    AddStyledTableData(dataTable, smallTextFont, rowColor, (i + 1).ToString(),
                        wagaBrutto.ToString("N0"), wagaTara.ToString("N0"), wagaNetto.ToString("N0"),
                        sztWszystkie.ToString("N0"), padle.ToString("N0"), konfiskaty.ToString("N0"), sztZdatne.ToString("N0"),
                        sredniaWaga.ToString("N2"),
                        wagaNetto.ToString("N0"),
                        padleKG > 0 ? $"-{padleKG:N0}" : "0",
                        konfiskatyKG > 0 ? $"-{konfiskatyKG:N0}" : "0",
                        ubytekKG > 0 ? $"-{ubytekKG:N0}" : "0",
                        opasienieKG > 0 ? $"-{opasienieKG:N0}" : "0",
                        klasaB > 0 ? $"-{klasaB:N0}" : "0",
                        doZaplaty.ToString("N0"),
                        cena.ToString("0.00"),
                        wartosc.ToString("N0"));
                }

                // === WIERSZ SUMY ===
                BaseColor sumRowColor = new BaseColor(220, 237, 200);
                Font sumFont = new Font(polishFont, 7, Font.BOLD);

                // Oblicz średnią cenę
                decimal avgCenaSum = sumaKG > 0 ? sumaWartosc / sumaKG : 0;

                AddStyledTableData(dataTable, sumFont, sumRowColor, "SUMA",
                    sumaBrutto.ToString("N0"), sumaTara.ToString("N0"), sumaNetto.ToString("N0"),
                    sumaSztWszystkie.ToString("N0"), sumaPadle.ToString("N0"), sumaKonfiskaty.ToString("N0"), sumaSztZdatne.ToString("N0"),
                    sredniaWagaSuma.ToString("N2"),
                    sumaNetto.ToString("N0"),
                    sumaPadleKG > 0 ? $"-{sumaPadleKG:N0}" : "0",
                    sumaKonfiskatyKG > 0 ? $"-{sumaKonfiskatyKG:N0}" : "0",
                    sumaUbytekKG > 0 ? $"-{sumaUbytekKG:N0}" : "0",
                    sumaOpasienieKG > 0 ? $"-{sumaOpasienieKG:N0}" : "0",
                    sumaKlasaB > 0 ? $"-{sumaKlasaB:N0}" : "0",
                    sumaKG.ToString("N0"),
                    avgCenaSum.ToString("0.00"),
                    sumaWartosc.ToString("N0"));

                doc.Add(dataTable);

                // === PODSUMOWANIE FINANSOWE ===
                int intTypCeny = zapytaniasql.PobierzInformacjeZBazyDanych<int>(ids[0], "[LibraNet].[dbo].[FarmerCalc]", "PriceTypeID");
                string typCeny = zapytaniasql.ZnajdzNazweCenyPoID(intTypCeny);
                decimal avgCena = sumaKG > 0 ? sumaWartosc / sumaKG : 0;

                PdfPTable summaryTable = new PdfPTable(2);
                summaryTable.WidthPercentage = 100;
                summaryTable.SetWidths(new float[] { 1.8f, 1.2f });
                summaryTable.SpacingBefore = 8f;

                // Lewa kolumna - wzory i obliczenia w jednej linii
                PdfPCell formulaCell = new PdfPCell { Border = PdfPCell.BOX, BorderColor = new BaseColor(220, 220, 220), Padding = 8, BackgroundColor = new BaseColor(252, 252, 252) };
                formulaCell.AddElement(new Paragraph("SPOSÓB OBLICZENIA:", new Font(polishFont, 9, Font.BOLD, BaseColor.DARK_GRAY)));

                // 1. Waga Netto = Brutto - Tara
                Paragraph formula1 = new Paragraph();
                formula1.Add(new Chunk("Netto = Brutto - Tara: ", legendaBoldFont));
                formula1.Add(new Chunk($"{sumaBrutto:N0} - {sumaTara:N0} = {sumaNetto:N0} kg", legendaFont));
                formulaCell.AddElement(formula1);

                // 2. Sztuki Zdatne = Dostarczono - Padłe - Konfiskaty
                Paragraph formula2 = new Paragraph();
                formula2.Add(new Chunk("Zdatne = Dostarcz. - Padłe - Konf.: ", legendaBoldFont));
                formula2.Add(new Chunk($"{sumaSztWszystkie} - {sumaPadle} - {sumaKonfiskaty} = {sumaSztZdatne} szt", legendaFont));
                formulaCell.AddElement(formula2);

                // 3. Średnia waga sztuki
                Paragraph formula3 = new Paragraph();
                formula3.Add(new Chunk("Śr. waga = Netto ÷ Dostarcz.: ", legendaBoldFont));
                formula3.Add(new Chunk($"{sumaNetto:N0} ÷ {sumaSztWszystkie} = {sredniaWagaSuma:N2} kg/szt", legendaFont));
                formulaCell.AddElement(formula3);

                // 4. Padłe [kg] = Padłe [szt] × Średnia waga
                Paragraph formula4 = new Paragraph();
                formula4.Add(new Chunk("Padłe [kg] = Padłe [szt] × Śr. waga: ", legendaBoldFont));
                formula4.Add(new Chunk($"{sumaPadle} × {sredniaWagaSuma:N2} = {sumaPadleKG:N0} kg", legendaFont));
                formulaCell.AddElement(formula4);

                // 5. Konfiskaty [kg] = Konfiskaty [szt] × Średnia waga
                Paragraph formula5 = new Paragraph();
                formula5.Add(new Chunk("Konf. [kg] = Konf. [szt] × Śr. waga: ", legendaBoldFont));
                formula5.Add(new Chunk($"{sumaKonfiskaty} × {sredniaWagaSuma:N2} = {sumaKonfiskatyKG:N0} kg", legendaFont));
                formulaCell.AddElement(formula5);

                // 5b. Ubytek [kg] = Netto × Ubytek%
                if (czyByloUbytku)
                {
                    Paragraph formula5b = new Paragraph();
                    formula5b.Add(new Chunk("Ubytek [kg] = Netto × Ubytek%: ", legendaBoldFont));
                    // ubytekProcWyswietlany jest ułamkiem (np. 0.005 = 0.5%), więc mnożymy przez 100 dla wyświetlenia
                    formula5b.Add(new Chunk($"{sumaNetto:N0} × {ubytekProcWyswietlany * 100:N2}% = {sumaUbytekKG:N0} kg", legendaFont));
                    formulaCell.AddElement(formula5b);
                }

                // 6. Do zapłaty = Netto - Padłe[kg] - Konfiskaty[kg] - Ubytek[kg] - Opasienie - Klasa B
                Paragraph formula6 = new Paragraph();
                if (czyByloUbytku)
                {
                    formula6.Add(new Chunk("Do zapł. = Netto - Padłe - Konf. - Ubytek - Opas. - Kl.B: ", legendaBoldFont));
                    formula6.Add(new Chunk($"{sumaNetto:N0} - {sumaPadleKG:N0} - {sumaKonfiskatyKG:N0} - {sumaUbytekKG:N0} - {sumaOpasienieKG:N0} - {sumaKlasaB:N0} = {sumaKG:N0} kg", legendaFont));
                }
                else
                {
                    formula6.Add(new Chunk("Do zapł. = Netto - Padłe - Konf. - Opas. - Kl.B: ", legendaBoldFont));
                    formula6.Add(new Chunk($"{sumaNetto:N0} - {sumaPadleKG:N0} - {sumaKonfiskatyKG:N0} - {sumaOpasienieKG:N0} - {sumaKlasaB:N0} = {sumaKG:N0} kg", legendaFont));
                }
                formulaCell.AddElement(formula6);

                // 7. Wartość = Kilogramy × Cena
                Paragraph formula7 = new Paragraph();
                formula7.Add(new Chunk("Wartość = Do zapł. × Cena: ", legendaBoldFont));
                formula7.Add(new Chunk($"{sumaKG:N0} × {avgCena:0.00} = {sumaWartosc:N0} zł", legendaFont));
                formulaCell.AddElement(formula7);

                // Informacja o zaokrągleniach
                Paragraph zaokraglenia = new Paragraph("* Wagi zaokrąglane do pełnych kilogramów", new Font(polishFont, 6, Font.ITALIC, grayColor));
                zaokraglenia.SpacingBefore = 5f;
                formulaCell.AddElement(zaokraglenia);

                summaryTable.AddCell(formulaCell);

                // Prawa kolumna - podsumowanie z wyrównanymi wartościami
                PdfPCell sumCell = new PdfPCell { Border = PdfPCell.BOX, BorderColor = greenColor, BorderWidth = 2f, Padding = 10, BackgroundColor = new BaseColor(245, 255, 245) };
                sumCell.AddElement(new Paragraph("PODSUMOWANIE", new Font(polishFont, 10, Font.BOLD, greenColor)));
                sumCell.AddElement(new Paragraph(" ", new Font(polishFont, 4, Font.NORMAL)));

                // Tabela z wyrównanymi wartościami
                PdfPTable valuesTable = new PdfPTable(2);
                valuesTable.WidthPercentage = 100;
                valuesTable.SetWidths(new float[] { 1.2f, 1f });

                // Średnia waga
                valuesTable.AddCell(new PdfPCell(new Phrase("Średnia waga:", new Font(polishFont, 9, Font.NORMAL, grayColor))) { Border = PdfPCell.NO_BORDER, PaddingBottom = 3 });
                valuesTable.AddCell(new PdfPCell(new Phrase($"{sredniaWagaSuma:N2} kg/szt", new Font(polishFont, 9, Font.BOLD, new BaseColor(142, 68, 173)))) { Border = PdfPCell.NO_BORDER, HorizontalAlignment = Element.ALIGN_RIGHT, PaddingBottom = 3 });

                // Suma kilogramów
                valuesTable.AddCell(new PdfPCell(new Phrase("Suma kilogramów:", new Font(polishFont, 9, Font.NORMAL, grayColor))) { Border = PdfPCell.NO_BORDER, PaddingBottom = 3 });
                valuesTable.AddCell(new PdfPCell(new Phrase($"{sumaKG:N0} kg", new Font(polishFont, 9, Font.BOLD, blueColor))) { Border = PdfPCell.NO_BORDER, HorizontalAlignment = Element.ALIGN_RIGHT, PaddingBottom = 3 });

                // Cena
                valuesTable.AddCell(new PdfPCell(new Phrase($"Cena ({typCeny}):", new Font(polishFont, 9, Font.NORMAL, grayColor))) { Border = PdfPCell.NO_BORDER, PaddingBottom = 3 });
                valuesTable.AddCell(new PdfPCell(new Phrase($"{avgCena:0.00} zł/kg", new Font(polishFont, 9, Font.BOLD, grayColor))) { Border = PdfPCell.NO_BORDER, HorizontalAlignment = Element.ALIGN_RIGHT, PaddingBottom = 3 });

                sumCell.AddElement(valuesTable);
                sumCell.AddElement(new Paragraph(" ", new Font(polishFont, 4, Font.NORMAL)));

                // Box DO WYPŁATY
                PdfPTable wartoscBox = new PdfPTable(1);
                wartoscBox.WidthPercentage = 100;
                PdfPCell wartoscCell = new PdfPCell(new Phrase($"DO WYPŁATY: {sumaWartosc:N0} zł", new Font(polishFont, 14, Font.BOLD, BaseColor.WHITE)));
                wartoscCell.BackgroundColor = greenColor;
                wartoscCell.HorizontalAlignment = Element.ALIGN_CENTER;
                wartoscCell.Padding = 8;
                wartoscBox.AddCell(wartoscCell);
                sumCell.AddElement(wartoscBox);

                // Termin płatności pod wypłatą (opcjonalnie)
                if (_drukujTerminPlatnosci)
                {
                    sumCell.AddElement(new Paragraph(" ", new Font(polishFont, 4, Font.NORMAL)));
                    Paragraph terminP = new Paragraph($"Termin płatności: {terminPlatnosci:dd.MM.yyyy} ({terminZaplatyDni} dni)", new Font(polishFont, 8, Font.ITALIC, grayColor));
                    terminP.Alignment = Element.ALIGN_CENTER;
                    sumCell.AddElement(terminP);
                }

                summaryTable.AddCell(sumCell);
                doc.Add(summaryTable);

                // === PODPISY (kompaktowe) ===
                // Pobierz nazwę wystawiającego z App.UserID
                NazwaZiD nazwaZiD = new NazwaZiD();
                string wystawiajacyNazwa = nazwaZiD.GetNameById(App.UserID) ?? App.UserID ?? "---";

                PdfPTable footerTable = new PdfPTable(2);
                footerTable.WidthPercentage = 100;
                footerTable.SpacingBefore = 12f; // Mniejszy odstęp
                footerTable.SetWidths(new float[] { 1f, 1f });

                // Podpis Dostawcy (lewa strona) - kompaktowy
                PdfPCell signatureLeft = new PdfPCell { Border = PdfPCell.BOX, BorderColor = new BaseColor(200, 200, 200), Padding = 8, BackgroundColor = new BaseColor(252, 252, 252) };
                signatureLeft.AddElement(new Paragraph("PODPIS DOSTAWCY", new Font(polishFont, 8, Font.BOLD, orangeColor)) { Alignment = Element.ALIGN_CENTER });
                signatureLeft.AddElement(new Paragraph(" ", new Font(polishFont, 8, Font.NORMAL)));
                signatureLeft.AddElement(new Paragraph("............................................................", new Font(polishFont, 9, Font.NORMAL)) { Alignment = Element.ALIGN_CENTER });
                signatureLeft.AddElement(new Paragraph("data i czytelny podpis", new Font(polishFont, 6, Font.ITALIC, grayColor)) { Alignment = Element.ALIGN_CENTER });
                footerTable.AddCell(signatureLeft);

                // Podpis Pracownika/Wystawiającego (prawa strona) - kompaktowy
                PdfPCell signatureRight = new PdfPCell { Border = PdfPCell.BOX, BorderColor = new BaseColor(200, 200, 200), Padding = 8, BackgroundColor = new BaseColor(252, 252, 252) };
                signatureRight.AddElement(new Paragraph("PODPIS PRACOWNIKA", new Font(polishFont, 8, Font.BOLD, greenColor)) { Alignment = Element.ALIGN_CENTER });
                signatureRight.AddElement(new Paragraph($"({wystawiajacyNazwa})", new Font(polishFont, 7, Font.NORMAL, grayColor)) { Alignment = Element.ALIGN_CENTER });
                signatureRight.AddElement(new Paragraph("............................................................", new Font(polishFont, 9, Font.NORMAL)) { Alignment = Element.ALIGN_CENTER });
                signatureRight.AddElement(new Paragraph("data i czytelny podpis", new Font(polishFont, 6, Font.ITALIC, grayColor)) { Alignment = Element.ALIGN_CENTER });
                footerTable.AddCell(signatureRight);

                doc.Add(footerTable);

                // Pobierz informacje o autorach z rozliczeń (ChangeLog)
                string wprowadzilNazwa = GetWprowadzilNazwa(ids[0]);
                string zaakceptowalNazwa = wystawiajacyNazwa; // Osoba generująca PDF jako akceptująca

                // Pobierz statystyki wprowadzenia/weryfikacji
                var (wprowadzenia, weryfikacje, total) = GetZatwierdzeniaStats(dzienUbojowy);

                // Tabela z informacjami o autorach
                PdfPTable authorsTable = new PdfPTable(3);
                authorsTable.WidthPercentage = 100;
                authorsTable.SpacingBefore = 10f;
                authorsTable.SetWidths(new float[] { 1f, 1f, 1f });

                Font authorLabelFont = new Font(polishFont, 7, Font.BOLD, grayColor);
                Font authorValueFont = new Font(polishFont, 7, Font.ITALIC, grayColor);

                // Wygenerował
                PdfPCell genCell = new PdfPCell { Border = PdfPCell.NO_BORDER, Padding = 2 };
                genCell.AddElement(new Paragraph("Wygenerował:", authorLabelFont) { Alignment = Element.ALIGN_CENTER });
                genCell.AddElement(new Paragraph(wystawiajacyNazwa, authorValueFont) { Alignment = Element.ALIGN_CENTER });
                authorsTable.AddCell(genCell);

                // Wprowadził - z procentami
                PdfPCell enteredCell = new PdfPCell { Border = PdfPCell.NO_BORDER, Padding = 2 };
                enteredCell.AddElement(new Paragraph("Wprowadził (rozl.):", authorLabelFont) { Alignment = Element.ALIGN_CENTER });
                if (wprowadzenia.Count > 0 && total > 0)
                {
                    foreach (var kv in wprowadzenia)
                    {
                        decimal pct = (decimal)kv.Value / total * 100;
                        enteredCell.AddElement(new Paragraph($"{kv.Key}: {pct:F0}%", authorValueFont) { Alignment = Element.ALIGN_CENTER });
                    }
                }
                else
                {
                    enteredCell.AddElement(new Paragraph(wprowadzilNazwa, authorValueFont) { Alignment = Element.ALIGN_CENTER });
                }
                authorsTable.AddCell(enteredCell);

                // Zaakceptował/Zweryfikował - z procentami
                PdfPCell approvedCell = new PdfPCell { Border = PdfPCell.NO_BORDER, Padding = 2 };
                approvedCell.AddElement(new Paragraph("Zweryfikował (rozl.):", authorLabelFont) { Alignment = Element.ALIGN_CENTER });
                if (weryfikacje.Count > 0 && total > 0)
                {
                    foreach (var kv in weryfikacje)
                    {
                        decimal pct = (decimal)kv.Value / total * 100;
                        approvedCell.AddElement(new Paragraph($"{kv.Key}: {pct:F0}%", authorValueFont) { Alignment = Element.ALIGN_CENTER });
                    }
                }
                else
                {
                    approvedCell.AddElement(new Paragraph(zaakceptowalNazwa, authorValueFont) { Alignment = Element.ALIGN_CENTER });
                }
                authorsTable.AddCell(approvedCell);

                doc.Add(authorsTable);

                doc.Close();
            }

            // Zapisz historię PDF do bazy danych
            int? dostawcaGID = zapytaniasql.PobierzInformacjeZBazyDanych<int?>(ids[0], "[LibraNet].[dbo].[FarmerCalc]", "CustomerGID");
            SavePdfHistory(ids, dostawcaGID, sellerName, dzienUbojowy, filePath);

            if (showMessage)
            {
                MessageBox.Show($"Wygenerowano dokument PDF:\n{Path.GetFileName(filePath)}\n\nŚcieżka:\n{filePath}",
                    "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void AddColoredMergedHeader(PdfPTable table, string text, Font font, int colspan, BaseColor bgColor)
        {
            PdfPCell cell = new PdfPCell(new Phrase(text, font))
            {
                Colspan = colspan,
                BackgroundColor = bgColor,
                HorizontalAlignment = Element.ALIGN_CENTER,
                VerticalAlignment = Element.ALIGN_MIDDLE,
                Padding = 6
            };
            table.AddCell(cell);
        }

        private void AddColoredTableHeader(PdfPTable table, string text, Font font, BaseColor bgColor)
        {
            PdfPCell cell = new PdfPCell(new Phrase(text, new Font(font.BaseFont, font.Size, font.Style, BaseColor.WHITE)))
            {
                BackgroundColor = bgColor,
                HorizontalAlignment = Element.ALIGN_CENTER,
                VerticalAlignment = Element.ALIGN_MIDDLE,
                Padding = 4,
                MinimumHeight = 22 // Wyższe nagłówki
            };
            table.AddCell(cell);
        }

        /// <summary>
        /// Dodaje specjalny dwuliniowy nagłówek "Dostarczone" z "(ARIMR)" pogrubionym poniżej
        /// </summary>
        private void AddDostarczoneHeader(PdfPTable table, Font font, BaseColor bgColor)
        {
            // Utwórz komórkę z dwoma liniami tekstu
            PdfPCell cell = new PdfPCell()
            {
                BackgroundColor = bgColor,
                HorizontalAlignment = Element.ALIGN_CENTER,
                VerticalAlignment = Element.ALIGN_MIDDLE,
                Padding = 2,
                MinimumHeight = 22
            };

            // Linia 1: "Dostarczone" (normalna czcionka)
            Paragraph line1 = new Paragraph("Dostarcz.", new Font(font.BaseFont, 6, Font.NORMAL, BaseColor.WHITE));
            line1.Alignment = Element.ALIGN_CENTER;
            cell.AddElement(line1);

            // Linia 2: "(ARIMR)" (pogrubiona)
            Paragraph line2 = new Paragraph("(ARIMR)", new Font(font.BaseFont, 6, Font.BOLD, BaseColor.WHITE));
            line2.Alignment = Element.ALIGN_CENTER;
            cell.AddElement(line2);

            table.AddCell(cell);
        }

        private void AddStyledTableData(PdfPTable table, Font font, BaseColor bgColor, params string[] values)
        {
            foreach (string value in values)
            {
                PdfPCell cell = new PdfPCell(new Phrase(value, font))
                {
                    BackgroundColor = bgColor,
                    HorizontalAlignment = Element.ALIGN_CENTER,
                    VerticalAlignment = Element.ALIGN_MIDDLE,
                    Padding = 3
                };
                table.AddCell(cell);
            }
        }

        private void AddSummaryRow(PdfPTable table, string label, string value, Font labelFont, Font valueFont)
        {
            PdfPCell labelCell = new PdfPCell(new Phrase(label, labelFont))
            {
                Border = PdfPCell.NO_BORDER,
                HorizontalAlignment = Element.ALIGN_RIGHT,
                PaddingRight = 10,
                PaddingBottom = 5
            };
            PdfPCell valueCell = new PdfPCell(new Phrase(value, valueFont))
            {
                Border = PdfPCell.NO_BORDER,
                HorizontalAlignment = Element.ALIGN_RIGHT,
                PaddingBottom = 5
            };
            table.AddCell(labelCell);
            table.AddCell(valueCell);
        }

        private void AddMergedHeader(PdfPTable table, string text, Font font, int colspan)
        {
            PdfPCell cell = new PdfPCell(new Phrase(text, font))
            {
                Colspan = colspan,
                VerticalAlignment = Element.ALIGN_MIDDLE,
                HorizontalAlignment = Element.ALIGN_CENTER
            };
            table.AddCell(cell);
        }

        private void AddTableHeader(PdfPTable table, string columnName, Font font)
        {
            PdfPCell cell = new PdfPCell(new Phrase(columnName, font))
            {
                BackgroundColor = BaseColor.LIGHT_GRAY,
                HorizontalAlignment = Element.ALIGN_CENTER,
                Padding = 3
            };
            table.AddCell(cell);
        }

        private void AddTableData(PdfPTable table, Font font, params string[] values)
        {
            foreach (string value in values)
            {
                PdfPCell cell = new PdfPCell(new Phrase(value, font))
                {
                    HorizontalAlignment = Element.ALIGN_CENTER,
                    Padding = 2
                };
                table.AddCell(cell);
            }
        }

        #region === DELETE / UNDO / SHAKE / HISTORIA ===

        // === DELETE: Usuwanie zaznaczonego wiersza ===
        private void DeleteSelectedRow()
        {
            if (selectedRow == null)
            {
                ShakeWindow();
                UpdateStatus("Nie zaznaczono wiersza do usunięcia");
                return;
            }

            var result = MessageBox.Show(
                $"Czy na pewno chcesz usunąć wiersz LP {selectedRow.Nr}?\n\nDostawca: {selectedRow.Dostawca}\nNetto: {selectedRow.NettoUbojni}",
                "Potwierdzenie usunięcia",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Zapisz do historii i undo
                    RecordChange(selectedRow.ID, "DELETE", "Cały wiersz", selectedRow.ToString(), null);
                    PushUndo(new UndoAction
                    {
                        ActionType = "DELETE",
                        RowId = selectedRow.ID,
                        RowData = CloneRow(selectedRow),
                        RowIndex = specyfikacjeData.IndexOf(selectedRow)
                    });

                    // Usuń z bazy
                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        connection.Open();
                        string query = "DELETE FROM dbo.FarmerCalc WHERE ID = @ID";
                        using (SqlCommand cmd = new SqlCommand(query, connection))
                        {
                            cmd.Parameters.AddWithValue("@ID", selectedRow.ID);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // Usuń z kolekcji
                    specyfikacjeData.Remove(selectedRow);
                    UpdateRowNumbers();
                    UpdateStatistics();
                    UpdateStatus($"Usunięto wiersz. Ctrl+Z aby cofnąć.");
                    selectedRow = null;
                }
                catch (Exception ex)
                {
                    ShakeWindow();
                    UpdateStatus($"Błąd usuwania: {ex.Message}");
                }
            }
        }

        // === UNDO: Cofanie ostatniej zmiany ===
        private void UndoLastChange()
        {
            if (_undoStack.Count == 0)
            {
                ShakeWindow();
                UpdateStatus("Brak zmian do cofnięcia");
                return;
            }

            var action = _undoStack.Pop();

            try
            {
                switch (action.ActionType)
                {
                    case "DELETE":
                        // Przywróć usunięty wiersz
                        RestoreDeletedRow(action);
                        break;

                    case "EDIT":
                        // Przywróć poprzednią wartość
                        RestoreEditedValue(action);
                        break;
                }

                UpdateStatus($"Cofnięto: {action.ActionType}. Pozostało {_undoStack.Count} zmian.");
            }
            catch (Exception ex)
            {
                ShakeWindow();
                UpdateStatus($"Błąd cofania: {ex.Message}");
            }
        }

        private void RestoreDeletedRow(UndoAction action)
        {
            if (action.RowData == null) return;

            // Przywróć do bazy
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                // INSERT z IDENTITY_INSERT
                string query = @"SET IDENTITY_INSERT dbo.FarmerCalc ON;
                    INSERT INTO dbo.FarmerCalc (ID, CarLp, CustomerGID, DeclI1, DeclI2, DeclI3, DeclI4, DeclI5,
                        LumQnt, ProdQnt, ProdWgt, Price, Loss, IncDeadConf, Opasienie, KlasaB, TerminDni)
                    VALUES (@ID, @Nr, @GID, @SztDek, @Padle, @CH, @NW, @ZM,
                        @LUMEL, @SztWyb, @KgWyb, @Cena, @Ubytek, @PiK, @Opas, @KlasaB, @Termin);
                    SET IDENTITY_INSERT dbo.FarmerCalc OFF;";

                using (SqlCommand cmd = new SqlCommand(query, connection))
                {
                    var row = action.RowData;
                    cmd.Parameters.AddWithValue("@ID", row.ID);
                    cmd.Parameters.AddWithValue("@Nr", row.Nr);
                    cmd.Parameters.AddWithValue("@GID", (object)row.DostawcaGID ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SztDek", row.SztukiDek);
                    cmd.Parameters.AddWithValue("@Padle", row.Padle);
                    cmd.Parameters.AddWithValue("@CH", row.CH);
                    cmd.Parameters.AddWithValue("@NW", row.NW);
                    cmd.Parameters.AddWithValue("@ZM", row.ZM);
                    cmd.Parameters.AddWithValue("@LUMEL", row.LUMEL);
                    cmd.Parameters.AddWithValue("@SztWyb", row.SztukiWybijak);
                    cmd.Parameters.AddWithValue("@KgWyb", row.KilogramyWybijak);
                    cmd.Parameters.AddWithValue("@Cena", row.Cena);
                    cmd.Parameters.AddWithValue("@Ubytek", row.Ubytek / 100);
                    cmd.Parameters.AddWithValue("@PiK", row.PiK);
                    cmd.Parameters.AddWithValue("@Opas", row.Opasienie);
                    cmd.Parameters.AddWithValue("@KlasaB", row.KlasaB);
                    cmd.Parameters.AddWithValue("@Termin", row.TerminDni);
                    cmd.ExecuteNonQuery();
                }
            }

            // Przywróć do kolekcji
            int index = Math.Min(action.RowIndex, specyfikacjeData.Count);
            specyfikacjeData.Insert(index, action.RowData);
            UpdateRowNumbers();
            UpdateStatistics();
        }

        private void RestoreEditedValue(UndoAction action)
        {
            var row = specyfikacjeData.FirstOrDefault(r => r.ID == action.RowId);
            if (row == null) return;

            // Przywróć wartość w obiekcie
            var property = typeof(SpecyfikacjaRow).GetProperty(action.PropertyName);
            if (property != null)
            {
                var oldValue = Convert.ChangeType(action.OldValue, property.PropertyType);
                property.SetValue(row, oldValue);
            }

            // Zapisz do bazy
            SaveRowToDatabase(row);
        }

        // === SHAKE: Animacja błędu ===
        private void ShakeWindow()
        {
            try
            {
                var storyboard = (System.Windows.Media.Animation.Storyboard)FindResource("ShakeAnimation");
                if (this.RenderTransform == null || !(this.RenderTransform is TranslateTransform))
                {
                    this.RenderTransform = new TranslateTransform();
                }
                storyboard.Begin(this);
            }
            catch
            {
                // Fallback - dźwięk systemowy
                System.Media.SystemSounds.Exclamation.Play();
            }
        }

        // === HISTORIA: Rejestrowanie zmian ===
        private void RecordChange(int rowId, string action, string property, string oldValue, string newValue)
        {
            _changeLog.Add(new ChangeLogEntry
            {
                Timestamp = DateTime.Now,
                RowId = rowId,
                Action = action,
                PropertyName = property,
                OldValue = oldValue,
                NewValue = newValue,
                UserName = App.UserID ?? Environment.UserName
            });

            // Ogranicz historię do 1000 wpisów
            if (_changeLog.Count > 1000)
            {
                _changeLog.RemoveAt(0);
            }
        }

        // === UNDO: Dodaj do stosu ===
        private void PushUndo(UndoAction action)
        {
            _undoStack.Push(action);
            if (_undoStack.Count > MaxUndoHistory)
            {
                // Usuń najstarsze (konwertuj na listę, usuń ostatni, odtwórz stos)
                var list = _undoStack.ToList();
                list.RemoveAt(list.Count - 1);
                _undoStack = new Stack<UndoAction>(list.AsEnumerable().Reverse());
            }
        }

        // === HELPER: Kopia wiersza dla undo ===
        private SpecyfikacjaRow CloneRow(SpecyfikacjaRow source)
        {
            return new SpecyfikacjaRow
            {
                ID = source.ID,
                Nr = source.Nr,
                DostawcaGID = source.DostawcaGID,
                Dostawca = source.Dostawca,
                SztukiDek = source.SztukiDek,
                Padle = source.Padle,
                CH = source.CH,
                NW = source.NW,
                ZM = source.ZM,
                LUMEL = source.LUMEL,
                SztukiWybijak = source.SztukiWybijak,
                KilogramyWybijak = source.KilogramyWybijak,
                Cena = source.Cena,
                Dodatek = source.Dodatek,
                TypCeny = source.TypCeny,
                Ubytek = source.Ubytek,
                PiK = source.PiK,
                Opasienie = source.Opasienie,
                KlasaB = source.KlasaB,
                TerminDni = source.TerminDni
            };
        }
        // Wklej to do klasy WidokSpecyfikacje, jeśli tego brakuje:
        private void NumericTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            // ZAWSZE ustaw selectedRow przy kliknięciu
            var row = FindVisualParent<DataGridRow>(textBox);
            if (row != null)
            {
                selectedRow = row.Item as SpecyfikacjaRow;
                dataGridView1.SelectedItem = selectedRow;
            }

            // Focus tylko jeśli TextBox nie ma jeszcze focusu
            if (!textBox.IsKeyboardFocusWithin)
            {
                e.Handled = true;
                textBox.Focus();
            }
        }
        // === NAWIGACJA STRZAŁKAMI PODCZAS EDYCJI ===
        private void NumericTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Reaguj tylko na strzałki
            if (e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Left || e.Key == Key.Right)
            {
                // 1. Zatrzymaj standardowe zachowanie (przesuwanie kursora w tekście)
                e.Handled = true;

                // 2. Określ kierunek nawigacji
                FocusNavigationDirection direction = FocusNavigationDirection.Next;

                switch (e.Key)
                {
                    case Key.Right:
                        direction = FocusNavigationDirection.Right;
                        break;
                    case Key.Left:
                        direction = FocusNavigationDirection.Left;
                        break;
                    case Key.Up:
                        direction = FocusNavigationDirection.Up;
                        break;
                    case Key.Down:
                        direction = FocusNavigationDirection.Down;
                        break;
                }

                // 3. Przenieś fokus do sąsiedniego elementu (następnej komórki)
                var uiElement = e.OriginalSource as UIElement;
                if (uiElement != null)
                {
                    uiElement.MoveFocus(new TraversalRequest(direction));
                }
            }
        }
        // === HISTORIA: Pokaż historię zmian (można wywołać przyciskiem) ===
        public void ShowChangeHistory()
        {
            if (_changeLog.Count == 0)
            {
                MessageBox.Show("Brak zmian w historii.", "Historia zmian", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("Ostatnie zmiany:");
            sb.AppendLine(new string('-', 50));

            foreach (var entry in _changeLog.TakeLast(20).Reverse())
            {
                sb.AppendLine($"[{entry.Timestamp:HH:mm:ss}] {entry.Action} - Wiersz {entry.RowId}");
                sb.AppendLine($"   {entry.PropertyName}: {entry.OldValue} → {entry.NewValue}");
                sb.AppendLine($"   Użytkownik: {entry.UserName}");
                sb.AppendLine();
            }

            MessageBox.Show(sb.ToString(), "Historia zmian", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region === ENTER - ZASTOSUJ DO WSZYSTKICH DOSTAW OD DOSTAWCY ===

        // Metoda pomocnicza do zapisu pojedynczej wartości do bazy
        // WERSJA NIEBLOKUJĄCA - zapis w tle, natychmiastowa nawigacja klawiszowa
        private void SaveFieldToDatabase(int id, string columnName, object value)
        {
            // Fire-and-forget: zapis wykonywany w tle, NIE BLOKUJE UI
            _ = Task.Run(() =>
            {
                try
                {
                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        connection.Open();
                        string query = $"UPDATE dbo.FarmerCalc SET {columnName} = @Value WHERE ID = @ID";
                        using (SqlCommand cmd = new SqlCommand(query, connection))
                        {
                            cmd.Parameters.AddWithValue("@ID", id);
                            cmd.Parameters.AddWithValue("@Value", value ?? DBNull.Value);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.BeginInvoke(new Action(() => UpdateStatus($"Błąd zapisu: {ex.Message}")));
                }
            });
        }

        #region === PDF HISTORY ===

        // Zapisz historię wygenerowanego PDF
        private void SavePdfHistory(List<int> ids, int? dostawcaGID, string dostawcaNazwa, DateTime calcDate, string pdfPath)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Sprawdź czy tabela istnieje
                    string checkTable = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PdfHistory'";
                    using (SqlCommand checkCmd = new SqlCommand(checkTable, connection))
                    {
                        int exists = (int)checkCmd.ExecuteScalar();
                        if (exists == 0)
                        {
                            // Tabela nie istnieje - utwórz ją
                            string createTable = @"CREATE TABLE [dbo].[PdfHistory] (
                                [ID] INT IDENTITY(1,1) PRIMARY KEY,
                                [FarmerCalcIDs] NVARCHAR(500) NOT NULL,
                                [DostawcaGID] INT NULL,
                                [DostawcaNazwa] NVARCHAR(200) NULL,
                                [CalcDate] DATE NOT NULL,
                                [PdfPath] NVARCHAR(500) NOT NULL,
                                [PdfFileName] NVARCHAR(200) NOT NULL,
                                [GeneratedBy] NVARCHAR(100) NOT NULL,
                                [GeneratedAt] DATETIME NOT NULL DEFAULT GETDATE(),
                                [FileSize] BIGINT NULL,
                                [IsDeleted] BIT NOT NULL DEFAULT 0
                            )";
                            using (SqlCommand createCmd = new SqlCommand(createTable, connection))
                            {
                                createCmd.ExecuteNonQuery();
                            }
                        }
                    }

                    // Pobierz rozmiar pliku
                    long? fileSize = null;
                    if (File.Exists(pdfPath))
                    {
                        fileSize = new FileInfo(pdfPath).Length;
                    }

                    string query = @"INSERT INTO [dbo].[PdfHistory]
                        ([FarmerCalcIDs], [DostawcaGID], [DostawcaNazwa], [CalcDate], [PdfPath], [PdfFileName], [GeneratedBy], [FileSize])
                        VALUES (@IDs, @DostawcaGID, @DostawcaNazwa, @CalcDate, @PdfPath, @PdfFileName, @GeneratedBy, @FileSize)";

                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@IDs", string.Join(",", ids));
                        cmd.Parameters.AddWithValue("@DostawcaGID", (object)dostawcaGID ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@DostawcaNazwa", dostawcaNazwa ?? "");
                        cmd.Parameters.AddWithValue("@CalcDate", calcDate);
                        cmd.Parameters.AddWithValue("@PdfPath", pdfPath);
                        cmd.Parameters.AddWithValue("@PdfFileName", Path.GetFileName(pdfPath));
                        cmd.Parameters.AddWithValue("@GeneratedBy", Environment.UserName);
                        cmd.Parameters.AddWithValue("@FileSize", (object)fileSize ?? DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                }

                // Odśwież status PDF dla wierszy
                RefreshPdfStatus(ids);
            }
            catch (Exception ex)
            {
                UpdateStatus($"Błąd zapisu historii PDF: {ex.Message}");
            }
        }

        /// <summary>
        /// Pobiera nazwę odbiorcy (Customer) na podstawie GID
        /// </summary>
        private string GetCustomerName(string customerGID)
        {
            if (string.IsNullOrWhiteSpace(customerGID) || customerGID == "-1")
                return "-";

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT ShortName FROM [LibraNet].[dbo].[Customer] WHERE GID = @GID";
                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@GID", customerGID);
                        var result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            return result.ToString()?.Trim() ?? "-";
                        }
                    }
                }
            }
            catch
            {
                // Ignoruj błędy
            }
            return "-";
        }

        /// <summary>
        /// Pobiera nazwę osoby która wprowadzała dane do rozliczeń (z ChangeLog)
        /// </summary>
        private string GetWprowadzilNazwa(int farmerCalcId)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Sprawdź czy tabela istnieje
                    string checkTable = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'FarmerCalcChangeLog'";
                    using (SqlCommand checkCmd = new SqlCommand(checkTable, connection))
                    {
                        int exists = (int)checkCmd.ExecuteScalar();
                        if (exists == 0) return "---";
                    }

                    // Pobierz pierwszą osobę która wprowadzała dane
                    string query = @"SELECT TOP 1 ChangedBy
                        FROM [dbo].[FarmerCalcChangeLog]
                        WHERE FarmerCalcID = @ID
                        ORDER BY ChangeDate ASC";

                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@ID", farmerCalcId);
                        var result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            return result.ToString();
                        }
                    }
                }
            }
            catch
            {
                // Ignoruj błędy
            }
            return "---";
        }

        /// <summary>
        /// Pobiera nazwę osoby która zatwierdziła/ostatnio edytowała dane (z ChangeLog)
        /// </summary>
        private string GetZatwierdzilNazwa(int farmerCalcId)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Sprawdź czy tabela istnieje
                    string checkTable = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'FarmerCalcChangeLog'";
                    using (SqlCommand checkCmd = new SqlCommand(checkTable, connection))
                    {
                        int exists = (int)checkCmd.ExecuteScalar();
                        if (exists == 0) return "---";
                    }

                    // Pobierz ostatnią osobę która edytowała dane
                    string query = @"SELECT TOP 1 ChangedBy
                        FROM [dbo].[FarmerCalcChangeLog]
                        WHERE FarmerCalcID = @ID
                        ORDER BY ChangeDate DESC";

                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@ID", farmerCalcId);
                        var result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            return result.ToString();
                        }
                    }
                }
            }
            catch
            {
                // Ignoruj błędy
            }
            return "---";
        }

        // Sprawdź czy PDF istnieje dla danego dostawcy i dnia
        private string GetPdfPathForIds(List<int> ids)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Sprawdź czy tabela istnieje
                    string checkTable = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PdfHistory'";
                    using (SqlCommand checkCmd = new SqlCommand(checkTable, connection))
                    {
                        int exists = (int)checkCmd.ExecuteScalar();
                        if (exists == 0) return null;
                    }

                    string idsString = string.Join(",", ids);
                    string query = @"SELECT TOP 1 PdfPath FROM [dbo].[PdfHistory]
                        WHERE FarmerCalcIDs = @IDs AND IsDeleted = 0
                        ORDER BY GeneratedAt DESC";

                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@IDs", idsString);
                        object result = cmd.ExecuteScalar();
                        return result as string;
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        // Odśwież status PDF dla wierszy
        private void RefreshPdfStatus(List<int> ids)
        {
            foreach (var row in specyfikacjeData.Where(r => ids.Contains(r.ID)))
            {
                row.HasPdf = true;
            }
        }

        // Załaduj status PDF dla wszystkich wierszy
        private void LoadPdfStatusForAllRows()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Sprawdź czy tabela istnieje
                    string checkTable = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PdfHistory'";
                    using (SqlCommand checkCmd = new SqlCommand(checkTable, connection))
                    {
                        int exists = (int)checkCmd.ExecuteScalar();
                        if (exists == 0) return;
                    }

                    string query = @"SELECT FarmerCalcIDs, PdfPath FROM [dbo].[PdfHistory] WHERE IsDeleted = 0";
                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string idsString = reader.GetString(0);
                                string pdfPath = reader.GetString(1);

                                // Sprawdź czy plik istnieje
                                bool fileExists = File.Exists(pdfPath);

                                var idsList = idsString.Split(',').Select(s => int.TryParse(s, out int id) ? id : 0).Where(id => id > 0).ToList();
                                foreach (var row in specyfikacjeData.Where(r => idsList.Contains(r.ID)))
                                {
                                    row.HasPdf = fileExists;
                                    row.PdfPath = pdfPath;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Błąd ładowania statusu PDF: {ex.Message}");
            }
        }

        // Kliknięcie na status PDF - otwórz plik
        private void PdfStatus_Click(object sender, MouseButtonEventArgs e)
        {
            var textBlock = sender as TextBlock;
            if (textBlock == null) return;

            var row = textBlock.DataContext as SpecyfikacjaRow;
            if (row == null || !row.HasPdf || string.IsNullOrEmpty(row.PdfPath)) return;

            if (File.Exists(row.PdfPath))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = row.PdfPath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Nie można otworzyć pliku:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                row.HasPdf = false;
                row.PdfPath = null;
                MessageBox.Show("Plik PDF nie istnieje.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion

        // === UPROSZCZONE HANDLERY: Enter tylko zapisuje i przechodzi dalej ===
        // Użyj Ctrl+Shift+D aby zastosować wartość do wszystkich wierszy dostawcy

        private void Cena_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var textBox = sender as TextBox;
                if (textBox == null) return;

                var row = textBox.DataContext as SpecyfikacjaRow;
                if (row == null) return;

                string input = textBox.Text.Replace(',', '.');
                if (decimal.TryParse(input, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out decimal cena))
                {
                    row.Cena = cena;
                    SaveFieldToDatabase(row.ID, "Price", cena);
                    UpdateStatus($"✓ Cena {cena:F2} zł | Ctrl+Shift+D = dla całego dostawcy");
                }
                // Nie blokuj Enter - pozwól DataGrid przejść do następnej komórki
            }
        }

        private void Dodatek_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var textBox = sender as TextBox;
                if (textBox == null) return;

                var row = textBox.DataContext as SpecyfikacjaRow;
                if (row == null) return;

                string input = textBox.Text.Replace(',', '.');
                if (decimal.TryParse(input, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out decimal dodatek))
                {
                    row.Dodatek = dodatek;
                    SaveFieldToDatabase(row.ID, "Addition", dodatek);
                    UpdateStatus($"✓ Dodatek {dodatek:F2} zł | Ctrl+Shift+D = dla całego dostawcy");
                }
            }
        }

        private void Ubytek_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var textBox = sender as TextBox;
                if (textBox == null) return;

                var row = textBox.DataContext as SpecyfikacjaRow;
                if (row == null) return;

                string input = textBox.Text.Replace(',', '.');
                if (decimal.TryParse(input, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out decimal ubytek))
                {
                    row.Ubytek = ubytek;
                    SaveFieldToDatabase(row.ID, "Loss", ubytek / 100);
                    UpdateStatus($"✓ Ubytek {ubytek:F2}% | Ctrl+Shift+D = dla całego dostawcy");
                }
            }
        }

        // === GotFocus handlers - zapisują starą wartość przed edycją ===
        private void Cena_GotFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            var row = textBox?.DataContext as SpecyfikacjaRow;
            if (row != null)
                _oldFieldValues[$"Cena_{row.ID}"] = row.Cena.ToString("F2");
        }

        private void Dodatek_GotFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            var row = textBox?.DataContext as SpecyfikacjaRow;
            if (row != null)
                _oldFieldValues[$"Dodatek_{row.ID}"] = row.Dodatek.ToString("F2");
        }

        private void Ubytek_GotFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            var row = textBox?.DataContext as SpecyfikacjaRow;
            if (row != null)
                _oldFieldValues[$"Ubytek_{row.ID}"] = row.Ubytek.ToString("F2");
        }

        private void SztukiDek_GotFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            var row = textBox?.DataContext as SpecyfikacjaRow;
            if (row != null)
                _oldFieldValues[$"SztukiDek_{row.ID}"] = row.SztukiDek.ToString();
        }

        private void Number_GotFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            var row = textBox?.DataContext as SpecyfikacjaRow;
            if (row != null)
                _oldFieldValues[$"Number_{row.ID}"] = row.Number.ToString();
        }

        private void Padle_GotFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            var row = textBox?.DataContext as SpecyfikacjaRow;
            if (row != null)
            {
                string oldVal = row.Padle.ToString();
                _oldFieldValues[$"Padle_{row.ID}"] = oldVal;
                UpdateStatus($"[GotFocus] Padle LP{row.Nr} = {oldVal}");
            }
        }

        private void CH_GotFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            var row = textBox?.DataContext as SpecyfikacjaRow;
            if (row != null)
                _oldFieldValues[$"CH_{row.ID}"] = row.CH.ToString();
        }

        private void NW_GotFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            var row = textBox?.DataContext as SpecyfikacjaRow;
            if (row != null)
                _oldFieldValues[$"NW_{row.ID}"] = row.NW.ToString();
        }

        private void ZM_GotFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            var row = textBox?.DataContext as SpecyfikacjaRow;
            if (row != null)
                _oldFieldValues[$"ZM_{row.ID}"] = row.ZM.ToString();
        }

        private void LUMEL_GotFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            var row = textBox?.DataContext as SpecyfikacjaRow;
            if (row != null)
                _oldFieldValues[$"LUMEL_{row.ID}"] = row.LUMEL.ToString();
        }

        private void Opasienie_GotFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            var row = textBox?.DataContext as SpecyfikacjaRow;
            if (row != null)
                _oldFieldValues[$"Opasienie_{row.ID}"] = row.Opasienie.ToString("F2");
        }

        private void KlasaB_GotFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            var row = textBox?.DataContext as SpecyfikacjaRow;
            if (row != null)
                _oldFieldValues[$"KlasaB_{row.ID}"] = row.KlasaB.ToString("F2");
        }

        // Handler LostFocus dla Cena - zapisuje do bazy po opuszczeniu pola
        private void Cena_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            var row = textBox.DataContext as SpecyfikacjaRow;
            if (row == null) return;

            // Sprawdź blokadę edycji
            if (!CheckEditingAllowed(row.DataUboju)) return;

            // Pobierz starą wartość
            string oldValue = "";
            string key = $"Cena_{row.ID}";
            if (_oldFieldValues.ContainsKey(key))
            {
                oldValue = _oldFieldValues[key];
                _oldFieldValues.Remove(key);
            }

            // WAŻNE: Wymuś aktualizację bindingu przed zapisem
            var binding = textBox.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();

            string newValue = row.Cena.ToString("F2");

            // Loguj zmianę tylko jeśli stara wartość ISTNIAŁA (nie była pusta/zero) i się różni
            if (!string.IsNullOrEmpty(oldValue) && oldValue != "0.00" && oldValue != newValue)
            {
                LogChangeToDatabase(row.ID, "Cena", oldValue, newValue, row.RealDostawca ?? row.Dostawca, row.Nr, row.CarID ?? "");
            }

            SaveFieldToDatabase(row.ID, "Price", row.Cena);
            UpdateStatus($"Zapisano cenę: {row.Cena:N2} dla LP {row.Nr}");
        }

        // Handler LostFocus dla Dodatek
        private void Dodatek_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            var row = textBox.DataContext as SpecyfikacjaRow;
            if (row == null) return;

            // Pobierz starą wartość
            string oldValue = "";
            string key = $"Dodatek_{row.ID}";
            if (_oldFieldValues.ContainsKey(key))
            {
                oldValue = _oldFieldValues[key];
                _oldFieldValues.Remove(key);
            }

            // WAŻNE: Wymuś aktualizację bindingu przed zapisem
            var binding = textBox.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();

            string newValue = row.Dodatek.ToString("F2");

            // Loguj zmianę tylko jeśli stara wartość ISTNIAŁA (nie była pusta/zero) i się różni
            if (!string.IsNullOrEmpty(oldValue) && oldValue != "0.00" && oldValue != newValue)
            {
                LogChangeToDatabase(row.ID, "Dodatek", oldValue, newValue, row.RealDostawca ?? row.Dostawca, row.Nr, row.CarID ?? "");
            }

            SaveFieldToDatabase(row.ID, "Addition", row.Dodatek);
            UpdateStatus($"Zapisano dodatek: {row.Dodatek:N2} dla LP {row.Nr}");
        }

        // Handler LostFocus dla Ubytek
        private void Ubytek_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            var row = textBox.DataContext as SpecyfikacjaRow;
            if (row == null) return;

            // Pobierz starą wartość
            string oldValue = "";
            string key = $"Ubytek_{row.ID}";
            if (_oldFieldValues.ContainsKey(key))
            {
                oldValue = _oldFieldValues[key];
                _oldFieldValues.Remove(key);
            }

            // WAŻNE: Wymuś aktualizację bindingu przed zapisem
            var binding = textBox.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();

            string newValue = row.Ubytek.ToString("F2");

            // Loguj zmianę tylko jeśli stara wartość ISTNIAŁA (nie była pusta/zero) i się różni
            if (!string.IsNullOrEmpty(oldValue) && oldValue != "0.00" && oldValue != newValue)
            {
                LogChangeToDatabase(row.ID, "Ubytek", oldValue + "%", newValue + "%", row.RealDostawca ?? row.Dostawca, row.Nr, row.CarID ?? "");
            }

            // W bazie Loss jest przechowywany jako ułamek (1.5% = 0.015)
            SaveFieldToDatabase(row.ID, "Loss", row.Ubytek / 100);
            UpdateStatus($"Zapisano ubytek: {row.Ubytek}% dla LP {row.Nr}");
        }

        // Handler dla zmiany PiK (CheckBox) - zapisuje do bazy natychmiast
        private void PiK_Changed(object sender, RoutedEventArgs e)
        {
            // BLOKADA: Ignoruj zmiany podczas ładowania danych (binding odpala Checked/Unchecked)
            if (_isLoadingData) return;

            var checkBox = sender as CheckBox;
            if (checkBox == null) return;

            var row = checkBox.DataContext as SpecyfikacjaRow;
            if (row == null) return;

            // Sprawdź blokadę edycji
            if (!CheckEditingAllowed(row.DataUboju))
            {
                // Przywróć poprzednią wartość
                checkBox.IsChecked = !row.PiK;
                return;
            }

            // Loguj zmianę
            string oldValue = row.PiK ? "NIE" : "TAK"; // Wartość przed zmianą jest odwrotna
            string newValue = row.PiK ? "TAK" : "NIE";
            LogChangeToDatabase(row.ID, "IncDeadConf", oldValue, newValue, row.RealDostawca ?? row.Dostawca, row.Nr, row.CarID ?? "");

            // Binding już zaktualizował wartość
            SaveFieldToDatabase(row.ID, "IncDeadConf", row.PiK);
            UpdateStatus($"Zapisano PiK: {(row.PiK ? "TAK" : "NIE")} dla LP {row.Nr}");
        }

        // Handler dla zmiany Symfonia (CheckBox) - zapisuje do bazy natychmiast
        private void Symfonia_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingData) return;

            var checkBox = sender as CheckBox;
            if (checkBox == null) return;

            var row = checkBox.DataContext as SpecyfikacjaRow;
            if (row == null) return;

            // Zapisz do bazy
            SaveFieldToDatabase(row.ID, "Symfonia", row.Symfonia);
            UpdateStatus($"Zapisano Symfonia: {(row.Symfonia ? "TAK" : "NIE")} dla LP {row.Nr}");
        }

        // Zmienna do śledzenia starego typu ceny
        private string _oldTypCeny = "";

        // Handler dla zmiany TypCeny (ComboBox) - zapisuje do bazy natychmiast
        private void TypCeny_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox == null) return;

            var row = comboBox.DataContext as SpecyfikacjaRow;
            if (row == null) return;

            // Pobierz starą wartość z e.RemovedItems
            string oldValue = "";
            if (e.RemovedItems.Count > 0)
                oldValue = e.RemovedItems[0]?.ToString() ?? "";

            // Sprawdź blokadę edycji
            if (!string.IsNullOrEmpty(oldValue) && !CheckEditingAllowed(row.DataUboju))
            {
                // Przywróć poprzednią wartość
                comboBox.SelectedItem = oldValue;
                return;
            }

            // Znajdź ID typu ceny
            int priceTypeId = -1;
            if (!string.IsNullOrEmpty(row.TypCeny))
            {
                priceTypeId = zapytaniasql.ZnajdzIdCeny(row.TypCeny);
            }

            if (priceTypeId > 0)
            {
                // Loguj zmianę
                if (!string.IsNullOrEmpty(oldValue) && oldValue != row.TypCeny)
                {
                    LogChangeToDatabase(row.ID, "PriceTypeID", oldValue, row.TypCeny, row.RealDostawca ?? row.Dostawca, row.Nr, row.CarID ?? "");
                }

                SaveFieldToDatabase(row.ID, "PriceTypeID", priceTypeId);
                UpdateStatus($"Zapisano typ ceny: {row.TypCeny} dla LP {row.Nr}");
            }
        }

        // Handler LostFocus dla Opasienie - zapisuje do bazy po opuszczeniu pola
        private void Opasienie_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            var row = textBox.DataContext as SpecyfikacjaRow;
            if (row == null) return;

            // Pobierz starą wartość
            string oldValue = "";
            string key = $"Opasienie_{row.ID}";
            if (_oldFieldValues.ContainsKey(key))
            {
                oldValue = _oldFieldValues[key];
                _oldFieldValues.Remove(key);
            }

            // WAŻNE: Wymuś aktualizację bindingu przed zapisem
            var binding = textBox.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();

            string newValue = row.Opasienie.ToString("F2");

            // Loguj zmianę tylko jeśli stara wartość ISTNIAŁA (nie była pusta/zero) i się różni
            if (!string.IsNullOrEmpty(oldValue) && oldValue != "0.00" && oldValue != newValue)
            {
                LogChangeToDatabase(row.ID, "Opasienie", oldValue + " kg", newValue + " kg", row.RealDostawca ?? row.Dostawca, row.Nr, row.CarID ?? "");
            }

            SaveFieldToDatabase(row.ID, "Opasienie", row.Opasienie);
            UpdateStatus($"Zapisano opasienie: {row.Opasienie:N0} kg dla LP {row.Nr}");
        }

        // Handler LostFocus dla KlasaB - zapisuje do bazy po opuszczeniu pola
        private void KlasaB_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            var row = textBox.DataContext as SpecyfikacjaRow;
            if (row == null) return;

            // Pobierz starą wartość
            string oldValue = "";
            string key = $"KlasaB_{row.ID}";
            if (_oldFieldValues.ContainsKey(key))
            {
                oldValue = _oldFieldValues[key];
                _oldFieldValues.Remove(key);
            }

            // WAŻNE: Wymuś aktualizację bindingu przed zapisem
            var binding = textBox.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();

            string newValue = row.KlasaB.ToString("F2");

            // Loguj zmianę tylko jeśli stara wartość ISTNIAŁA (nie była pusta/zero) i się różni
            if (!string.IsNullOrEmpty(oldValue) && oldValue != "0.00" && oldValue != newValue)
            {
                LogChangeToDatabase(row.ID, "KlasaB", oldValue + " kg", newValue + " kg", row.RealDostawca ?? row.Dostawca, row.Nr, row.CarID ?? "");
            }

            SaveFieldToDatabase(row.ID, "KlasaB", row.KlasaB);
            UpdateStatus($"Zapisano klasę B: {row.KlasaB:N0} kg dla LP {row.Nr}");
        }

        // Handler LostFocus dla LUMEL - zapisuje do bazy po opuszczeniu pola
        private void LUMEL_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            var row = textBox.DataContext as SpecyfikacjaRow;
            if (row == null) return;

            // Pobierz starą wartość
            string oldValue = "";
            string key = $"LUMEL_{row.ID}";
            if (_oldFieldValues.ContainsKey(key))
            {
                oldValue = _oldFieldValues[key];
                _oldFieldValues.Remove(key);
            }

            var binding = textBox.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();

            string newValue = row.LUMEL.ToString();

            // Loguj zmianę tylko jeśli stara wartość ISTNIAŁA (nie była pusta/zero) i się różni
            if (!string.IsNullOrEmpty(oldValue) && oldValue != "0" && oldValue != newValue)
            {
                LogChangeToDatabase(row.ID, "LUMEL", oldValue, newValue, row.RealDostawca ?? row.Dostawca, row.Nr, row.CarID ?? "");
            }

            SaveFieldToDatabase(row.ID, "LumQnt", row.LUMEL);
            UpdateStatus($"Zapisano LUMEL: {row.LUMEL} szt dla LP {row.Nr}");
        }

        // Handler LostFocus dla SztukiDek - zapisuje do bazy (DeclI1)
        private void SztukiDek_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            var row = textBox.DataContext as SpecyfikacjaRow;
            if (row == null) return;

            // Pobierz starą wartość
            string oldValue = "";
            string key = $"SztukiDek_{row.ID}";
            if (_oldFieldValues.ContainsKey(key))
            {
                oldValue = _oldFieldValues[key];
                _oldFieldValues.Remove(key);
            }

            var binding = textBox.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();

            string newValue = row.SztukiDek.ToString();

            // Loguj zmianę tylko jeśli stara wartość ISTNIAŁA (nie była pusta/zero) i się różni
            if (!string.IsNullOrEmpty(oldValue) && oldValue != "0" && oldValue != newValue)
            {
                LogChangeToDatabase(row.ID, "Szt.Dek", oldValue, newValue, row.RealDostawca ?? row.Dostawca, row.Nr, row.CarID ?? "");
            }

            SaveFieldToDatabase(row.ID, "DeclI1", row.SztukiDek);
            UpdateStatus($"Zapisano Szt.Dek: {row.SztukiDek} dla LP {row.Nr}");
        }

        // Handler LostFocus dla Number (Nr specyfikacji) - zapisuje do bazy i auto-increment dla następnych wierszy
        private void Number_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            var row = textBox.DataContext as SpecyfikacjaRow;
            if (row == null) return;

            // Pobierz starą wartość
            string oldValue = "";
            string key = $"Number_{row.ID}";
            if (_oldFieldValues.ContainsKey(key))
            {
                oldValue = _oldFieldValues[key];
                _oldFieldValues.Remove(key);
            }

            var binding = textBox.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();

            string newValue = row.Number.ToString();

            // Loguj zmianę tylko jeśli stara wartość ISTNIAŁA (nie pusta i nie 0) i się różni
            if (!string.IsNullOrEmpty(oldValue) && oldValue != "0" && oldValue != newValue)
            {
                LogChangeToDatabase(row.ID, "Number", oldValue, newValue, row.RealDostawca ?? row.Dostawca, row.Nr, row.CarID ?? "");
            }

            // Zapisz do bazy
            SaveFieldToDatabase(row.ID, "Number", row.Number);

            // Auto-increment dla następnych wierszy
            int currentIndex = specyfikacjeData.IndexOf(row);
            int currentNumber = row.Number;

            if (currentIndex >= 0 && currentNumber > 0)
            {
                for (int i = currentIndex + 1; i < specyfikacjeData.Count; i++)
                {
                    currentNumber++;
                    var nextRow = specyfikacjeData[i];

                    // Loguj zmianę tylko jeśli stara wartość nie była 0 (nowy rekord)
                    string nextOldValue = nextRow.Number.ToString();
                    if (nextOldValue != "0" && nextOldValue != currentNumber.ToString())
                    {
                        LogChangeToDatabase(nextRow.ID, "Number", nextOldValue, currentNumber.ToString(),
                            nextRow.RealDostawca ?? nextRow.Dostawca, nextRow.Nr, nextRow.CarID ?? "");
                    }

                    nextRow.Number = currentNumber;
                    SaveFieldToDatabase(nextRow.ID, "Number", currentNumber);
                }

                UpdateStatus($"Zapisano Nr spec. {row.Number} dla LP {row.Nr} i auto-increment dla {specyfikacjeData.Count - currentIndex - 1} następnych wierszy");
            }
            else
            {
                UpdateStatus($"Zapisano Nr spec.: {row.Number} dla LP {row.Nr}");
            }
        }

        // Handler LostFocus dla Padle - zapisuje do bazy (DeclI2)
        private void Padle_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            var row = textBox.DataContext as SpecyfikacjaRow;
            if (row == null) return;

            // Pobierz starą wartość z TextBox PRZED aktualizacją bindingu
            string oldValueFromTextBox = textBox.Text;

            // Pobierz też z naszego słownika (jeśli GotFocus został wywołany)
            string oldValueFromDict = "";
            string key = $"Padle_{row.ID}";
            if (_oldFieldValues.ContainsKey(key))
            {
                oldValueFromDict = _oldFieldValues[key];
                _oldFieldValues.Remove(key);
            }

            // Użyj wartości ze słownika lub z modelu przed aktualizacją
            string oldValue = !string.IsNullOrEmpty(oldValueFromDict) ? oldValueFromDict : row.Padle.ToString();

            var binding = textBox.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();

            string newValue = row.Padle.ToString();

            // ZAWSZE loguj jeśli wartość się zmieniła (bez względu na starą wartość)
            if (oldValue != newValue)
            {
                LogChangeToDatabase(row.ID, "Padłe", oldValue, newValue, row.RealDostawca ?? row.Dostawca, row.Nr, row.CarID ?? "");
            }

            SaveFieldToDatabase(row.ID, "DeclI2", row.Padle);
            UpdateStatus($"Padłe: {oldValue} → {newValue} dla LP {row.Nr}");
        }

        // Handler LostFocus dla CH - zapisuje do bazy (DeclI3)
        private void CH_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            var row = textBox.DataContext as SpecyfikacjaRow;
            if (row == null) return;

            // Pobierz starą wartość
            string oldValue = "";
            string key = $"CH_{row.ID}";
            if (_oldFieldValues.ContainsKey(key))
            {
                oldValue = _oldFieldValues[key];
                _oldFieldValues.Remove(key);
            }

            var binding = textBox.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();

            string newValue = row.CH.ToString();

            // Loguj zmianę tylko jeśli stara wartość ISTNIAŁA (nie była pusta/zero) i się różni
            if (!string.IsNullOrEmpty(oldValue) && oldValue != "0" && oldValue != newValue)
            {
                LogChangeToDatabase(row.ID, "CH", oldValue, newValue, row.RealDostawca ?? row.Dostawca, row.Nr, row.CarID ?? "");
            }

            SaveFieldToDatabase(row.ID, "DeclI3", row.CH);
            UpdateStatus($"Zapisano CH: {row.CH} dla LP {row.Nr}");
        }

        // Handler LostFocus dla NW - zapisuje do bazy (DeclI4)
        private void NW_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            var row = textBox.DataContext as SpecyfikacjaRow;
            if (row == null) return;

            // Pobierz starą wartość
            string oldValue = "";
            string key = $"NW_{row.ID}";
            if (_oldFieldValues.ContainsKey(key))
            {
                oldValue = _oldFieldValues[key];
                _oldFieldValues.Remove(key);
            }

            var binding = textBox.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();

            string newValue = row.NW.ToString();

            // Loguj zmianę tylko jeśli stara wartość ISTNIAŁA (nie była pusta/zero) i się różni
            if (!string.IsNullOrEmpty(oldValue) && oldValue != "0" && oldValue != newValue)
            {
                LogChangeToDatabase(row.ID, "NW", oldValue, newValue, row.RealDostawca ?? row.Dostawca, row.Nr, row.CarID ?? "");
            }

            SaveFieldToDatabase(row.ID, "DeclI4", row.NW);
            UpdateStatus($"Zapisano NW: {row.NW} dla LP {row.Nr}");
        }

        // Handler LostFocus dla ZM - zapisuje do bazy (DeclI5)
        private void ZM_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            var row = textBox.DataContext as SpecyfikacjaRow;
            if (row == null) return;

            // Pobierz starą wartość
            string oldValue = "";
            string key = $"ZM_{row.ID}";
            if (_oldFieldValues.ContainsKey(key))
            {
                oldValue = _oldFieldValues[key];
                _oldFieldValues.Remove(key);
            }

            var binding = textBox.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();

            string newValue = row.ZM.ToString();

            // Loguj zmianę tylko jeśli stara wartość ISTNIAŁA (nie była pusta/zero) i się różni
            if (!string.IsNullOrEmpty(oldValue) && oldValue != "0" && oldValue != newValue)
            {
                LogChangeToDatabase(row.ID, "ZM", oldValue, newValue, row.RealDostawca ?? row.Dostawca, row.Nr, row.CarID ?? "");
            }

            SaveFieldToDatabase(row.ID, "DeclI5", row.ZM);
            UpdateStatus($"Zapisano ZM: {row.ZM} dla LP {row.Nr}");
        }

        #endregion

        #region Rozliczenia Tab Handlers

        private ObservableCollection<RozliczenieRow> rozliczeniaData = new ObservableCollection<RozliczenieRow>();
        private ObservableCollection<PlachtaRow> plachtaData = new ObservableCollection<PlachtaRow>();

        private void BtnFilterRozliczenia_Click(object sender, RoutedEventArgs e)
        {
            LoadRozliczeniaData();
        }

        private void LoadRozliczeniaData()
        {
            try
            {
                // Upewnij się że tabela zatwierdzień istnieje
                EnsureRozliczeniaZatwierdzeniaTabelaExists();

                // Użyj tej samej daty co w Specyfikacjach (główny DatePicker w lewym górnym rogu)
                DateTime selectedDate = dateTimePicker1.SelectedDate ?? DateTime.Today;

                rozliczeniaData.Clear();

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                        SELECT
                            fc.ID,
                            fc.CalcDate as Data,
                            fc.CarLp as Nr,
                            COALESCE(k.ShortName, 'Nieznany') as Dostawca,
                            ISNULL(fc.DeclI1, 0) as SztukiDek,
                            ISNULL(fc.NettoWeight, 0) as NettoKg,
                            ISNULL(fc.Price, 0) as Cena,
                            ISNULL(fc.Addition, 0) as Dodatek,
                            pt.Name as TypCeny,
                            ISNULL(fc.TerminDni, 0) as TerminDni,
                            ISNULL(fc.NettoWeight, 0) * (ISNULL(fc.Price, 0) + ISNULL(fc.Addition, 0)) as Wartosc,
                            ISNULL(fc.Symfonia, 0) as Symfonia
                        FROM dbo.FarmerCalc fc
                        LEFT JOIN dbo.Dostawcy k ON fc.CustomerGID = k.ID
                        LEFT JOIN dbo.PriceType pt ON fc.PriceTypeID = pt.ID
                        WHERE fc.CalcDate = @CalcDate
                        ORDER BY fc.CarLp";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@CalcDate", selectedDate.Date);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                rozliczeniaData.Add(new RozliczenieRow
                                {
                                    ID = reader.GetInt32(reader.GetOrdinal("ID")),
                                    Data = reader.GetDateTime(reader.GetOrdinal("Data")),
                                    Nr = reader.IsDBNull(reader.GetOrdinal("Nr")) ? 0 : Convert.ToInt32(reader["Nr"]),
                                    Dostawca = reader.IsDBNull(reader.GetOrdinal("Dostawca")) ? "" : reader["Dostawca"].ToString(),
                                    SztukiDek = reader.IsDBNull(reader.GetOrdinal("SztukiDek")) ? 0 : Convert.ToInt32(reader["SztukiDek"]),
                                    NettoKg = reader.IsDBNull(reader.GetOrdinal("NettoKg")) ? 0 : Convert.ToDecimal(reader["NettoKg"]),
                                    Cena = reader.IsDBNull(reader.GetOrdinal("Cena")) ? 0 : Convert.ToDecimal(reader["Cena"]),
                                    Dodatek = reader.IsDBNull(reader.GetOrdinal("Dodatek")) ? 0 : Convert.ToDecimal(reader["Dodatek"]),
                                    TypCeny = reader.IsDBNull(reader.GetOrdinal("TypCeny")) ? "" : reader["TypCeny"].ToString(),
                                    TerminDni = reader.IsDBNull(reader.GetOrdinal("TerminDni")) ? 0 : Convert.ToInt32(reader["TerminDni"]),
                                    Wartosc = reader.IsDBNull(reader.GetOrdinal("Wartosc")) ? 0 : Convert.ToDecimal(reader["Wartosc"]),
                                    Symfonia = !reader.IsDBNull(reader.GetOrdinal("Symfonia")) && Convert.ToBoolean(reader["Symfonia"])
                                });
                            }
                        }
                    }
                }

                dataGridRozliczenia.ItemsSource = rozliczeniaData;

                // Załaduj stany zatwierdzenia z bazy
                LoadZatwierdzeniaForRozliczenia();

                // Aktualizuj podsumowanie
                lblRozliczeniaSumaWierszy.Text = rozliczeniaData.Count.ToString();
                lblRozliczeniaSumaSztuk.Text = rozliczeniaData.Sum(r => r.SztukiDek).ToString("N0");
                lblRozliczeniaSumaKg.Text = rozliczeniaData.Sum(r => r.NettoKg).ToString("N0");
                lblRozliczeniaSumaWartosc.Text = rozliczeniaData.Sum(r => r.Wartosc).ToString("N2") + " zł";

                UpdateStatus($"Rozliczenia: załadowano {rozliczeniaData.Count} rekordów dla {selectedDate:yyyy-MM-dd}");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Błąd ładowania rozliczeń: {ex.Message}");
            }
        }

        /// <summary>
        /// Pobiera pełną nazwę użytkownika na podstawie UserID
        /// </summary>
        private string GetCurrentUserDisplayName()
        {
            string userId = App.UserID ?? Environment.UserName;
            try
            {
                NazwaZiD nazwaZiD = new NazwaZiD();
                string fullName = nazwaZiD.GetNameById(userId);
                return !string.IsNullOrEmpty(fullName) ? fullName : userId;
            }
            catch
            {
                return userId;
            }
        }

        private void BtnZatwierdzDzien_Click(object sender, RoutedEventArgs e)
        {
            // Pierwsza kontrola - wprowadzenie WYBRANYCH wierszy
            var selectedRows = dataGridRozliczenia.SelectedItems.Cast<RozliczenieRow>()
                .Where(r => !r.Zatwierdzony).ToList();

            if (selectedRows.Count == 0)
            {
                MessageBox.Show("Zaznacz wiersze do wprowadzenia (niezatwierdzone).",
                    "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string userName = GetCurrentUserDisplayName();
            foreach (var row in selectedRows)
            {
                row.Zatwierdzony = true;
                row.ZatwierdzonePrzez = userName;
                row.DataZatwierdzenia = DateTime.Now;
                SaveZatwierdzenie(row);
            }

            UpdateStatus($"Zatwierdzono wprowadzenie {selectedRows.Count} wierszy przez {userName}");
        }

        private void BtnZatwierdzWszystko_Click(object sender, RoutedEventArgs e)
        {
            // Pierwsza kontrola - wprowadzenie WSZYSTKICH niezatwierdzonych wierszy
            string userName = GetCurrentUserDisplayName();
            int zatwierdzone = 0;

            foreach (var row in rozliczeniaData.Where(r => !r.Zatwierdzony))
            {
                row.Zatwierdzony = true;
                row.ZatwierdzonePrzez = userName;
                row.DataZatwierdzenia = DateTime.Now;
                SaveZatwierdzenie(row);
                zatwierdzone++;
            }

            if (zatwierdzone > 0)
            {
                UpdateStatus($"Zatwierdzono wprowadzenie {zatwierdzone} wierszy przez {userName}");
            }
            else
            {
                MessageBox.Show("Wszystkie wiersze są już zatwierdzone.",
                    "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnCofnijZatwierdzenie_Click(object sender, RoutedEventArgs e)
        {
            // Cofnięcie pierwszej kontroli (wprowadzenie) - tylko dla niezweryfikowanych
            int cofniete = 0;
            foreach (var row in rozliczeniaData.Where(r => r.Zatwierdzony && !r.Zweryfikowany))
            {
                row.Zatwierdzony = false;
                row.ZatwierdzonePrzez = null;
                row.DataZatwierdzenia = null;
                SaveZatwierdzenie(row);
                cofniete++;
            }

            if (cofniete > 0)
            {
                UpdateStatus($"Cofnięto zatwierdzenie wprowadzenia dla {cofniete} wierszy");
            }
            else
            {
                MessageBox.Show("Brak wierszy do cofnięcia (tylko niezweryfikowane można cofnąć).",
                    "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // === PODWÓJNA KONTROLA: Weryfikacja przez drugiego pracownika ===
        private void BtnWeryfikujDzien_Click(object sender, RoutedEventArgs e)
        {
            // Weryfikacja WYBRANYCH wierszy - wymaga najpierw zatwierdzenia wprowadzenia
            var selectedRows = dataGridRozliczenia.SelectedItems.Cast<RozliczenieRow>()
                .Where(r => r.Zatwierdzony && !r.Zweryfikowany).ToList();

            if (selectedRows.Count == 0)
            {
                MessageBox.Show("Zaznacz wiersze do weryfikacji (zatwierdzone, ale niezweryfikowane).",
                    "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string userName = GetCurrentUserDisplayName();
            int weryfikowane = 0;
            int pominieteSameOsoba = 0;

            foreach (var row in selectedRows)
            {
                // Sprawdź czy to nie ta sama osoba co wprowadzający
                if (row.ZatwierdzonePrzez == userName)
                {
                    pominieteSameOsoba++;
                    continue;
                }

                row.Zweryfikowany = true;
                row.ZweryfikowanePrzez = userName;
                row.DataWeryfikacji = DateTime.Now;
                SaveZatwierdzenie(row);
                weryfikowane++;
            }

            if (weryfikowane > 0)
            {
                string msg = $"Zweryfikowano {weryfikowane} wierszy przez {userName}";
                if (pominieteSameOsoba > 0)
                    msg += $"\nPominięto {pominieteSameOsoba} wierszy (ta sama osoba)";
                UpdateStatus(msg);
            }
            else if (pominieteSameOsoba > 0)
            {
                MessageBox.Show($"Nie można weryfikować własnych wpisów!\n{pominieteSameOsoba} wierszy wymaga weryfikacji przez innego pracownika.",
                    "Podwójna kontrola", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnWeryfikujWszystko_Click(object sender, RoutedEventArgs e)
        {
            // Weryfikacja WSZYSTKICH zatwierdzonych wierszy
            string userName = GetCurrentUserDisplayName();
            int weryfikowane = 0;
            int pominieteSameOsoba = 0;

            foreach (var row in rozliczeniaData.Where(r => r.Zatwierdzony && !r.Zweryfikowany))
            {
                // Sprawdź czy to nie ta sama osoba co wprowadzający
                if (row.ZatwierdzonePrzez == userName)
                {
                    pominieteSameOsoba++;
                    continue;
                }

                row.Zweryfikowany = true;
                row.ZweryfikowanePrzez = userName;
                row.DataWeryfikacji = DateTime.Now;
                SaveZatwierdzenie(row);
                weryfikowane++;
            }

            if (weryfikowane > 0)
            {
                string msg = $"Zweryfikowano {weryfikowane} wierszy przez {userName}";
                if (pominieteSameOsoba > 0)
                    msg += $"\nPominięto {pominieteSameOsoba} wierszy (ta sama osoba)";
                UpdateStatus(msg);
            }
            else if (pominieteSameOsoba > 0)
            {
                MessageBox.Show($"Nie można weryfikować własnych wpisów!\n{pominieteSameOsoba} wierszy wymaga weryfikacji przez innego pracownika.",
                    "Podwójna kontrola", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                MessageBox.Show("Brak wierszy do weryfikacji.\nWiersze muszą być najpierw zatwierdzone (wprowadzone).",
                    "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnCofnijWeryfikacje_Click(object sender, RoutedEventArgs e)
        {
            // Cofnięcie weryfikacji (wymaga uprawnień)
            var zweryfikowane = rozliczeniaData.Where(r => r.Zweryfikowany).ToList();
            if (zweryfikowane.Count == 0)
            {
                MessageBox.Show("Brak zweryfikowanych wierszy do cofnięcia.",
                    "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"Czy na pewno chcesz cofnąć weryfikację dla {zweryfikowane.Count} wierszy?\nOperacja wymaga uprawnień przełożonego.",
                "Cofnięcie weryfikacji", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                foreach (var row in zweryfikowane)
                {
                    row.Zweryfikowany = false;
                    row.ZweryfikowanePrzez = null;
                    row.DataWeryfikacji = null;
                    SaveZatwierdzenie(row);
                }
                UpdateStatus($"Cofnięto weryfikację dla {zweryfikowane.Count} wierszy");
            }
        }

        private void BtnExportSymfonia_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implementacja eksportu do Symfonii
            MessageBox.Show("Funkcja eksportu do Symfonii w przygotowaniu.",
                "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Tworzy tabelę RozliczeniaZatwierdzenia jeśli nie istnieje
        /// </summary>
        private void EnsureRozliczeniaZatwierdzeniaTabelaExists()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string createTable = @"
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'RozliczeniaZatwierdzenia')
                        BEGIN
                            CREATE TABLE [dbo].[RozliczeniaZatwierdzenia](
                                [ID] [int] IDENTITY(1,1) NOT NULL,
                                [FarmerCalcID] [int] NOT NULL,
                                [CalcDate] [date] NOT NULL,
                                [Zatwierdzony] [bit] NOT NULL DEFAULT 0,
                                [ZatwierdzonePrzez] [nvarchar](100) NULL,
                                [DataZatwierdzenia] [datetime] NULL,
                                [Zweryfikowany] [bit] NOT NULL DEFAULT 0,
                                [ZweryfikowanePrzez] [nvarchar](100) NULL,
                                [DataWeryfikacji] [datetime] NULL,
                                PRIMARY KEY CLUSTERED ([ID] ASC),
                                UNIQUE NONCLUSTERED ([FarmerCalcID] ASC)
                            )
                        END";
                    using (SqlCommand cmd = new SqlCommand(createTable, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Błąd tworzenia tabeli zatwierdzień: {ex.Message}");
            }
        }

        /// <summary>
        /// Zapisuje stan zatwierdzenia/weryfikacji do bazy danych
        /// </summary>
        private void SaveZatwierdzenie(RozliczenieRow row)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string upsert = @"
                        MERGE [dbo].[RozliczeniaZatwierdzenia] AS target
                        USING (SELECT @FarmerCalcID as FarmerCalcID) AS source
                        ON target.FarmerCalcID = source.FarmerCalcID
                        WHEN MATCHED THEN
                            UPDATE SET
                                Zatwierdzony = @Zatwierdzony,
                                ZatwierdzonePrzez = @ZatwierdzonePrzez,
                                DataZatwierdzenia = @DataZatwierdzenia,
                                Zweryfikowany = @Zweryfikowany,
                                ZweryfikowanePrzez = @ZweryfikowanePrzez,
                                DataWeryfikacji = @DataWeryfikacji
                        WHEN NOT MATCHED THEN
                            INSERT (FarmerCalcID, CalcDate, Zatwierdzony, ZatwierdzonePrzez, DataZatwierdzenia, Zweryfikowany, ZweryfikowanePrzez, DataWeryfikacji)
                            VALUES (@FarmerCalcID, @CalcDate, @Zatwierdzony, @ZatwierdzonePrzez, @DataZatwierdzenia, @Zweryfikowany, @ZweryfikowanePrzez, @DataWeryfikacji);";

                    using (SqlCommand cmd = new SqlCommand(upsert, conn))
                    {
                        cmd.Parameters.AddWithValue("@FarmerCalcID", row.ID);
                        cmd.Parameters.AddWithValue("@CalcDate", row.Data.Date);
                        cmd.Parameters.AddWithValue("@Zatwierdzony", row.Zatwierdzony);
                        cmd.Parameters.AddWithValue("@ZatwierdzonePrzez", (object)row.ZatwierdzonePrzez ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@DataZatwierdzenia", (object)row.DataZatwierdzenia ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Zweryfikowany", row.Zweryfikowany);
                        cmd.Parameters.AddWithValue("@ZweryfikowanePrzez", (object)row.ZweryfikowanePrzez ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@DataWeryfikacji", (object)row.DataWeryfikacji ?? DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Błąd zapisu zatwierdzenia: {ex.Message}");
            }
        }

        /// <summary>
        /// Ładuje stany zatwierdzenia/weryfikacji dla wierszy rozliczeń
        /// </summary>
        private void LoadZatwierdzeniaForRozliczenia()
        {
            try
            {
                DateTime selectedDate = dateTimePicker1.SelectedDate ?? DateTime.Today;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Sprawdź czy tabela istnieje
                    string checkTable = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'RozliczeniaZatwierdzenia'";
                    using (SqlCommand checkCmd = new SqlCommand(checkTable, conn))
                    {
                        int exists = (int)checkCmd.ExecuteScalar();
                        if (exists == 0) return;
                    }

                    string query = @"
                        SELECT FarmerCalcID, Zatwierdzony, ZatwierdzonePrzez, DataZatwierdzenia,
                               Zweryfikowany, ZweryfikowanePrzez, DataWeryfikacji
                        FROM [dbo].[RozliczeniaZatwierdzenia]
                        WHERE CalcDate = @CalcDate";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@CalcDate", selectedDate.Date);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int fcId = reader.GetInt32(0);
                                var row = rozliczeniaData.FirstOrDefault(r => r.ID == fcId);
                                if (row != null)
                                {
                                    row.Zatwierdzony = reader.GetBoolean(1);
                                    row.ZatwierdzonePrzez = reader.IsDBNull(2) ? null : reader.GetString(2);
                                    row.DataZatwierdzenia = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3);
                                    row.Zweryfikowany = reader.GetBoolean(4);
                                    row.ZweryfikowanePrzez = reader.IsDBNull(5) ? null : reader.GetString(5);
                                    row.DataWeryfikacji = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Błąd ładowania zatwierdzień: {ex.Message}");
            }
        }

        /// <summary>
        /// Pobiera statystyki wprowadzenia i weryfikacji dla danej daty
        /// </summary>
        private (Dictionary<string, int> wprowadzenia, Dictionary<string, int> weryfikacje, int total) GetZatwierdzeniaStats(DateTime date)
        {
            var wprowadzenia = new Dictionary<string, int>();
            var weryfikacje = new Dictionary<string, int>();
            int total = 0;

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Sprawdź czy tabela istnieje
                    string checkTable = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'RozliczeniaZatwierdzenia'";
                    using (SqlCommand checkCmd = new SqlCommand(checkTable, conn))
                    {
                        int exists = (int)checkCmd.ExecuteScalar();
                        if (exists == 0) return (wprowadzenia, weryfikacje, 0);
                    }

                    // Policz łączną liczbę wierszy dla daty
                    string countQuery = "SELECT COUNT(*) FROM [dbo].[FarmerCalc] WHERE CalcDate = @CalcDate";
                    using (SqlCommand countCmd = new SqlCommand(countQuery, conn))
                    {
                        countCmd.Parameters.AddWithValue("@CalcDate", date.Date);
                        total = (int)countCmd.ExecuteScalar();
                    }

                    // Pobierz statystyki wprowadzenia
                    string wpQuery = @"
                        SELECT ZatwierdzonePrzez, COUNT(*) as Cnt
                        FROM [dbo].[RozliczeniaZatwierdzenia]
                        WHERE CalcDate = @CalcDate AND Zatwierdzony = 1 AND ZatwierdzonePrzez IS NOT NULL
                        GROUP BY ZatwierdzonePrzez";

                    using (SqlCommand cmd = new SqlCommand(wpQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@CalcDate", date.Date);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string osoba = reader.GetString(0);
                                int cnt = reader.GetInt32(1);
                                wprowadzenia[osoba] = cnt;
                            }
                        }
                    }

                    // Pobierz statystyki weryfikacji
                    string wrQuery = @"
                        SELECT ZweryfikowanePrzez, COUNT(*) as Cnt
                        FROM [dbo].[RozliczeniaZatwierdzenia]
                        WHERE CalcDate = @CalcDate AND Zweryfikowany = 1 AND ZweryfikowanePrzez IS NOT NULL
                        GROUP BY ZweryfikowanePrzez";

                    using (SqlCommand cmd = new SqlCommand(wrQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@CalcDate", date.Date);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string osoba = reader.GetString(0);
                                int cnt = reader.GetInt32(1);
                                weryfikacje[osoba] = cnt;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignoruj błędy
            }

            return (wprowadzenia, weryfikacje, total);
        }

        #endregion

        #region Płachta Tab Handlers

        private void LoadPlachtaData()
        {
            try
            {
                DateTime selectedDate = dateTimePicker1.SelectedDate ?? DateTime.Today;
                plachtaData.Clear();

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Pobierz dane z FarmerCalc - takie same kolumny jak Panel Lekarza
                    string query = @"
                        SELECT
                            fc.ID,
                            ISNULL(fc.CarLp, 0) as CarLp,
                            ISNULL(fc.Number, 0) as NrSpec,
                            ISNULL(fc.CustomerGID, '') as CustomerGID,
                            (SELECT TOP 1 ShortName FROM dbo.Dostawcy WHERE LTRIM(RTRIM(ID)) = LTRIM(RTRIM(fc.CustomerGID))) as HodowcaNazwa,
                            (SELECT TOP 1 ISNULL(Address, '') + ', ' + ISNULL(PostalCode, '') + ' ' + ISNULL(City, '')
                             FROM dbo.Dostawcy WHERE LTRIM(RTRIM(ID)) = LTRIM(RTRIM(fc.CustomerGID))) as Adres,
                            ISNULL(fc.VetComment, '') as BadaniaSalmonella,
                            ISNULL(fc.VetNo, '') as NrSwZdrowia,
                            (SELECT TOP 1 AnimNo FROM dbo.Dostawcy WHERE LTRIM(RTRIM(ID)) = LTRIM(RTRIM(fc.CustomerGID))) as NrGospodarstwa,
                            ISNULL(fc.DeclI1, 0) as IloscDek,
                            ISNULL(fc.LumQnt, 0) as Lumel,
                            ISNULL(fc.CarID, '') as Ciagnik,
                            ISNULL(fc.TrailerID, '') as Naczepa,
                            ISNULL(fc.DeclI2, 0) as Padle,
                            ISNULL(fc.DeclI3, 0) as CH,
                            ISNULL(fc.DeclI4, 0) as NW,
                            ISNULL(fc.DeclI5, 0) as ZM,
                            ISNULL(fc.NettoFarmWeight, ISNULL(fc.NettoWeight, 0)) as NettoWeight
                        FROM dbo.FarmerCalc fc
                        WHERE fc.CalcDate = @CalcDate
                        ORDER BY fc.CarLp, fc.ID";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@CalcDate", selectedDate.Date);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            int lpCounter = 1;
                            while (reader.Read())
                            {
                                string hodowca = reader["HodowcaNazwa"] != DBNull.Value ? reader["HodowcaNazwa"].ToString() : "";
                                string customerGID = reader["CustomerGID"] != DBNull.Value ? reader["CustomerGID"].ToString() : "";

                                if (string.IsNullOrEmpty(hodowca))
                                {
                                    hodowca = string.IsNullOrEmpty(customerGID) ? "Nieprzypisany" : $"ID: {customerGID}";
                                }

                                plachtaData.Add(new PlachtaRow
                                {
                                    ID = reader["ID"] != DBNull.Value ? Convert.ToInt32(reader["ID"]) : 0,
                                    Lp = lpCounter++,
                                    NrSpec = reader["NrSpec"] != DBNull.Value ? Convert.ToInt32(reader["NrSpec"]) : 0,
                                    Hodowca = hodowca,
                                    Adres = reader["Adres"] != DBNull.Value ? reader["Adres"].ToString().Trim() : "",
                                    BadaniaSalmonella = reader["BadaniaSalmonella"] != DBNull.Value ? reader["BadaniaSalmonella"].ToString() : "",
                                    NrSwZdrowia = reader["NrSwZdrowia"] != DBNull.Value ? reader["NrSwZdrowia"].ToString() : "",
                                    NrGospodarstwa = reader["NrGospodarstwa"] != DBNull.Value ? reader["NrGospodarstwa"].ToString() : "",
                                    IloscDek = reader["IloscDek"] != DBNull.Value ? Convert.ToInt32(reader["IloscDek"]) : 0,
                                    Lumel = reader["Lumel"] != DBNull.Value ? Convert.ToInt32(reader["Lumel"]) : 0,
                                    Ciagnik = reader["Ciagnik"] != DBNull.Value ? reader["Ciagnik"].ToString() : "",
                                    Naczepa = reader["Naczepa"] != DBNull.Value ? reader["Naczepa"].ToString() : "",
                                    Padle = reader["Padle"] != DBNull.Value ? Convert.ToInt32(reader["Padle"]) : 0,
                                    KodHodowcy = customerGID,
                                    Chore = reader["CH"] != DBNull.Value ? Convert.ToInt32(reader["CH"]) : 0,
                                    NW = reader["NW"] != DBNull.Value ? Convert.ToInt32(reader["NW"]) : 0,
                                    ZM = reader["ZM"] != DBNull.Value ? Convert.ToInt32(reader["ZM"]) : 0,
                                    CustomerGID = 0,
                                    NettoWeight = reader["NettoWeight"] != DBNull.Value ? Convert.ToDecimal(reader["NettoWeight"]) : 0
                                });
                            }
                        }
                    }
                }

                dataGridPlachta.ItemsSource = plachtaData;
                UpdatePlachtaSummary();
                UpdateStatus($"Płachta: załadowano {plachtaData.Count} rekordów dla {selectedDate:yyyy-MM-dd}");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Błąd ładowania płachty: {ex.Message}");
            }
        }

        private void UpdatePlachtaSummary()
        {
            lblPlachtaWierszy.Text = plachtaData.Count.ToString();
            lblPlachtaSumaPadle.Text = plachtaData.Sum(r => r.Padle).ToString();
            lblPlachtaSumaCH.Text = plachtaData.Sum(r => r.Chore).ToString();
            lblPlachtaSumaNW.Text = plachtaData.Sum(r => r.NW).ToString();
            lblPlachtaSumaZM.Text = plachtaData.Sum(r => r.ZM).ToString();
        }

        private void BtnPlachtaMoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridPlachta.SelectedItem is PlachtaRow selectedPlachtaRow)
            {
                int index = plachtaData.IndexOf(selectedPlachtaRow);
                if (index > 0)
                {
                    plachtaData.Move(index, index - 1);
                    UpdatePlachtaLpNumbers();
                    dataGridPlachta.SelectedItem = selectedPlachtaRow;

                    // Synchronizuj z specyfikacjeData i Transport
                    SyncSpecyfikacjeOrderFromPlachta();
                    RefreshTransportFromSpecyfikacje();
                }
            }
        }

        private void BtnPlachtaMoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridPlachta.SelectedItem is PlachtaRow selectedPlachtaRow)
            {
                int index = plachtaData.IndexOf(selectedPlachtaRow);
                if (index < plachtaData.Count - 1)
                {
                    plachtaData.Move(index, index + 1);
                    UpdatePlachtaLpNumbers();
                    dataGridPlachta.SelectedItem = selectedPlachtaRow;

                    // Synchronizuj z specyfikacjeData i Transport
                    SyncSpecyfikacjeOrderFromPlachta();
                    RefreshTransportFromSpecyfikacje();
                }
            }
        }

        private void UpdatePlachtaLpNumbers()
        {
            for (int i = 0; i < plachtaData.Count; i++)
            {
                plachtaData[i].Lp = i + 1;
            }
        }

        /// <summary>
        /// Synchronizuje kolejność specyfikacjeData na podstawie kolejności plachtaData
        /// </summary>
        private void SyncSpecyfikacjeOrderFromPlachta()
        {
            if (specyfikacjeData == null || plachtaData == null)
                return;

            // Stwórz mapę ID -> pozycja z plachtaData
            var newOrder = plachtaData.Select((p, idx) => new { p.ID, Index = idx }).ToList();

            // Posortuj specyfikacjeData według nowej kolejności
            var orderedSpecs = specyfikacjeData
                .OrderBy(s => newOrder.FirstOrDefault(o => o.ID == s.ID)?.Index ?? int.MaxValue)
                .ToList();

            // Zastąp kolekcję
            specyfikacjeData.Clear();
            foreach (var spec in orderedSpecs)
            {
                specyfikacjeData.Add(spec);
            }
            UpdateRowNumbers();
        }

        private void BtnPlachtaRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadPlachtaData();
        }

        private void BtnPlachtaSaveOrder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Zapisz nową kolejność (CarLp) i dane oceny dla każdego wiersza
                    foreach (var row in plachtaData)
                    {
                        string updateQuery = @"
                            UPDATE dbo.FarmerCalc
                            SET CarLp = @CarLp,
                                DeclI2 = @Padle,
                                DeclI3 = @Chore,
                                DeclI4 = @NW,
                                DeclI5 = @ZM
                            WHERE ID = @ID";

                        using (SqlCommand cmd = new SqlCommand(updateQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@CarLp", row.Lp);
                            cmd.Parameters.AddWithValue("@Padle", row.Padle);
                            cmd.Parameters.AddWithValue("@Chore", row.Chore);
                            cmd.Parameters.AddWithValue("@NW", row.NW);
                            cmd.Parameters.AddWithValue("@ZM", row.ZM);
                            cmd.Parameters.AddWithValue("@ID", row.ID);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                UpdateStatus("Zapisano kolejność i dane płachty");
                MessageBox.Show("Kolejność i dane zostały zapisane.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);

                // Odśwież wszystkie karty
                DateTime selectedDate = dateTimePicker1.SelectedDate ?? DateTime.Today;
                LoadData(selectedDate);  // Odśwież Specyfikacje
                LoadRozliczeniaData();   // Odśwież Rozliczenia
                LoadPlachtaData();       // Odśwież Płachtę
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnPlachtaStatus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is PlachtaRow row)
            {
                row.Status = !row.Status;
                btn.Content = row.Status ? "✓" : "OK";
                btn.Background = row.Status ? new SolidColorBrush(Color.FromRgb(46, 125, 50)) : new SolidColorBrush(Color.FromRgb(39, 174, 96));
            }
        }

        private void BtnPlachtaPasteNrGosp_Click(object sender, RoutedEventArgs e)
        {
            var selectedRow = dataGridPlachta.SelectedItem as PlachtaRow;
            if (selectedRow == null)
            {
                MessageBox.Show("Najpierw zaznacz wiersz z którego chcesz skopiować NR GOSP.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(selectedRow.NrGospodarstwa))
            {
                MessageBox.Show("Zaznaczony wiersz nie ma wypełnionego NR GOSP.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string hodowcaNazwa = selectedRow.Hodowca;
            string nrGosp = selectedRow.NrGospodarstwa;
            string customerGID = selectedRow.KodHodowcy;

            // Znajdź wszystkie wiersze z tym samym hodowcą
            var wierszeTegSamegoHodowcy = plachtaData.Where(r => r.Hodowca == hodowcaNazwa && r.ID != selectedRow.ID).ToList();

            if (wierszeTegSamegoHodowcy.Count == 0)
            {
                MessageBox.Show($"Nie znaleziono innych wierszy z hodowcą: {hodowcaNazwa}", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Czy chcesz wkleić NR GOSP '{nrGosp}' do {wierszeTegSamegoHodowcy.Count} innych wierszy hodowcy '{hodowcaNazwa}'?\n\n" +
                $"Dodatkowo zapisać ten numer na stałe do bazy danych hodowcy?",
                "Potwierdzenie",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel)
                return;

            // Wklej do wszystkich wierszy
            foreach (var row in wierszeTegSamegoHodowcy)
            {
                row.NrGospodarstwa = nrGosp;
            }

            UpdateStatus($"Wklejono NR GOSP do {wierszeTegSamegoHodowcy.Count} wierszy");

            // Jeśli użytkownik chce zapisać na stałe do bazy dostawców
            if (result == MessageBoxResult.Yes && !string.IsNullOrWhiteSpace(customerGID))
            {
                try
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        string updateQuery = "UPDATE dbo.Dostawcy SET AnimNo = @AnimNo WHERE LTRIM(RTRIM(ID)) = @CustomerGID";
                        using (SqlCommand cmd = new SqlCommand(updateQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@AnimNo", nrGosp);
                            cmd.Parameters.AddWithValue("@CustomerGID", customerGID.Trim());
                            int affected = cmd.ExecuteNonQuery();

                            if (affected > 0)
                            {
                                MessageBox.Show($"NR GOSP '{nrGosp}' został zapisany do bazy danych hodowcy '{hodowcaNazwa}'.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            else
                            {
                                MessageBox.Show($"Nie udało się zapisać do bazy dostawców (CustomerGID: {customerGID}).", "Ostrzeżenie", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd zapisu do bazy dostawców:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show($"NR GOSP został wklejony do {wierszeTegSamegoHodowcy.Count} wierszy.\nAby zapisać na stałe, kliknij 'Zapisz zmiany'.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnPlachtaPrint_Click(object sender, RoutedEventArgs e)
        {
            if (plachtaData.Count == 0)
            {
                MessageBox.Show("Brak danych do wydruku!", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                PrintDocument printDoc = new PrintDocument();
                printDoc.PrintPage += PlachtaPrintDoc_PrintPage;

                DateTime selectedDate = dateTimePicker1.SelectedDate ?? DateTime.Today;
                printDoc.DocumentName = $"Plachta_{selectedDate:yyyy-MM-dd}";

                // Ustawienie orientacji poziomej
                printDoc.DefaultPageSettings.Landscape = true;

                System.Windows.Controls.PrintDialog printDialog = new System.Windows.Controls.PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    printDoc.PrinterSettings.PrinterName = printDialog.PrintQueue.Name;
                    printDoc.Print();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd drukowania:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PlachtaPrintDoc_PrintPage(object sender, PrintPageEventArgs e)
        {
            System.Drawing.Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            DateTime selectedDate = dateTimePicker1.SelectedDate ?? DateTime.Today;

            float pageWidth = e.PageBounds.Width;
            float pageHeight = e.PageBounds.Height;
            float leftMargin = 8;
            float rightMargin = pageWidth - 8;
            float tableWidth = rightMargin - leftMargin;
            float y = 12;

            // Czcionki
            System.Drawing.Font fontTitle = new System.Drawing.Font("Arial", 16, System.Drawing.FontStyle.Bold);
            System.Drawing.Font fontDate = new System.Drawing.Font("Arial", 14, System.Drawing.FontStyle.Bold);
            System.Drawing.Font fontHeader = new System.Drawing.Font("Arial", 8, System.Drawing.FontStyle.Bold);
            System.Drawing.Font fontKonfiskaty = new System.Drawing.Font("Arial", 7, System.Drawing.FontStyle.Bold);
            System.Drawing.Font fontData = new System.Drawing.Font("Arial", 9);
            System.Drawing.Font fontDataBold = new System.Drawing.Font("Arial", 9, System.Drawing.FontStyle.Bold);
            System.Drawing.Font fontSummary = new System.Drawing.Font("Arial", 11, System.Drawing.FontStyle.Bold);

            System.Drawing.SolidBrush brushBlack = new System.Drawing.SolidBrush(System.Drawing.Color.Black);
            System.Drawing.SolidBrush brushGray = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(60, 60, 60));
            System.Drawing.SolidBrush brushHeaderBg = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(200, 200, 200));
            System.Drawing.SolidBrush brushAltRow = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(240, 240, 240));
            System.Drawing.Pen penThick = new System.Drawing.Pen(System.Drawing.Color.Black, 2f);
            System.Drawing.Pen penMedium = new System.Drawing.Pen(System.Drawing.Color.Black, 1f);
            System.Drawing.Pen penThin = new System.Drawing.Pen(System.Drawing.Color.FromArgb(80, 80, 80), 0.5f);

            // Nagłówek
            string[] dniTygodnia = { "NIEDZIELA", "PONIEDZIAŁEK", "WTOREK", "ŚRODA", "CZWARTEK", "PIĄTEK", "SOBOTA" };
            string dzienTygodnia = dniTygodnia[(int)selectedDate.DayOfWeek];

            g.DrawString("PŁACHTA - OCENA DOBROSTANU", fontTitle, brushBlack, leftMargin, y);
            g.DrawString($"{selectedDate:dd}. {selectedDate:MMMM} {selectedDate:yyyy} - {dzienTygodnia}", fontDate, brushBlack, leftMargin + 320, y + 3);

            y += 34;

            // Tabela - 15 kolumn
            // LP, NR, HODOWCA, ADRES, SALMONELLA, NR.ŚW.ZDR, NR GOSP, SZT.DEK, CIĄGNIK, NACZEPA, PADŁE, KOD, CH, NW, ZM
            float[] colPercent = { 2.5f, 3f, 13f, 14f, 7f, 6f, 7f, 5f, 7f, 7f, 5f, 5f, 4.5f, 4.5f, 4.5f };
            float[] colWidths = new float[colPercent.Length];
            for (int i = 0; i < colPercent.Length; i++)
            {
                colWidths[i] = tableWidth * colPercent[i] / 100f;
            }

            float[] colX = new float[colWidths.Length];
            colX[0] = leftMargin;
            for (int i = 1; i < colWidths.Length; i++)
            {
                colX[i] = colX[i - 1] + colWidths[i - 1];
            }

            // Nagłówki
            string[] headers = { "LP", "NR", "HODOWCA", "ADRES", "SALMON.", "ŚW.ZDR", "NR GOSP", "SZT", "CIĄGNIK", "NACZEPA", "PADŁE", "KOD", "CH", "NW", "ZM" };

            // Nagłówek tabeli
            float headerHeight = 28;
            g.FillRectangle(brushHeaderBg, leftMargin, y, tableWidth, headerHeight);
            g.DrawRectangle(penThick, leftMargin, y, tableWidth, headerHeight);

            float headerTextY = y + 8;

            for (int i = 0; i < headers.Length; i++)
            {
                if (i > 0)
                    g.DrawLine(penMedium, colX[i], y, colX[i], y + headerHeight);

                System.Drawing.SizeF textSize = g.MeasureString(headers[i], fontHeader);
                float textX = colX[i] + (colWidths[i] - textSize.Width) / 2;
                g.DrawString(headers[i], fontHeader, brushBlack, textX, headerTextY);
            }

            y += headerHeight;

            // Oblicz wysokość wiersza
            float availableHeight = pageHeight - y - 60;
            float rowHeight = availableHeight / plachtaData.Count;
            if (rowHeight > 30) rowHeight = 30;
            if (rowHeight < 18) rowHeight = 18;

            // Dane wierszy
            int sumaPadle = 0, sumaCH = 0, sumaNW = 0, sumaZM = 0;
            int sumaIlosc = 0;
            int rowIndex = 0;

            foreach (var d in plachtaData)
            {
                if (y > pageHeight - 60)
                    break;

                // Naprzemienne tło
                if (rowIndex % 2 == 1)
                {
                    g.FillRectangle(brushAltRow, leftMargin + 1, y + 1, tableWidth - 2, rowHeight - 1);
                }

                float textY = y + (rowHeight - 12) / 2;

                // LP
                DrawPlachtaCenteredText(g, d.Lp.ToString(), fontDataBold, brushBlack, colX[0], colWidths[0], textY);

                // NR SPEC
                DrawPlachtaCenteredText(g, d.NrSpec.ToString(), fontDataBold, brushBlack, colX[1], colWidths[1], textY);

                // HODOWCA
                string hodowca = d.Hodowca ?? "-";
                if (hodowca.Length > 20) hodowca = hodowca.Substring(0, 18) + "..";
                g.DrawString(hodowca, fontData, brushBlack, colX[2] + 3, textY);

                // ADRES
                string adres = d.Adres ?? "-";
                if (adres.Length > 25) adres = adres.Substring(0, 23) + "..";
                g.DrawString(adres, fontData, brushGray, colX[3] + 3, textY);

                // SALMONELLA
                string salmonella = d.BadaniaSalmonella ?? "";
                if (salmonella.Length > 10) salmonella = salmonella.Substring(0, 8) + "..";
                DrawPlachtaCenteredText(g, salmonella, fontData, brushGray, colX[4], colWidths[4], textY);

                // NR ŚW. ZDROWIA
                DrawPlachtaCenteredText(g, d.NrSwZdrowia ?? "", fontData, brushGray, colX[5], colWidths[5], textY);

                // NR GOSPODARSTWA
                DrawPlachtaCenteredText(g, d.NrGospodarstwa ?? "", fontDataBold, brushBlack, colX[6], colWidths[6], textY);

                // SZT.DEK
                DrawPlachtaCenteredText(g, d.IloscDek.ToString(), fontDataBold, brushBlack, colX[7], colWidths[7], textY);

                // CIĄGNIK
                DrawPlachtaCenteredText(g, d.Ciagnik ?? "", fontData, brushGray, colX[8], colWidths[8], textY);

                // NACZEPA
                DrawPlachtaCenteredText(g, d.Naczepa ?? "", fontData, brushGray, colX[9], colWidths[9], textY);

                // PADŁE
                string padleText = d.Padle > 0 ? d.Padle.ToString() : "-";
                DrawPlachtaCenteredText(g, padleText, fontDataBold, brushBlack, colX[10], colWidths[10], textY);

                // KOD HODOWCY
                DrawPlachtaCenteredText(g, d.KodHodowcy ?? "", fontData, brushGray, colX[11], colWidths[11], textY);

                // CH
                string chText = d.Chore > 0 ? d.Chore.ToString() : "-";
                DrawPlachtaCenteredText(g, chText, fontDataBold, brushBlack, colX[12], colWidths[12], textY);

                // NW
                string nwText = d.NW > 0 ? d.NW.ToString() : "-";
                DrawPlachtaCenteredText(g, nwText, fontDataBold, brushBlack, colX[13], colWidths[13], textY);

                // ZM
                string zmText = d.ZM > 0 ? d.ZM.ToString() : "-";
                DrawPlachtaCenteredText(g, zmText, fontDataBold, brushBlack, colX[14], colWidths[14], textY);

                // Linie pionowe
                for (int i = 1; i < colWidths.Length; i++)
                {
                    g.DrawLine(penThin, colX[i], y, colX[i], y + rowHeight);
                }

                // Linia pozioma dolna
                g.DrawLine(penThin, leftMargin, y + rowHeight, rightMargin, y + rowHeight);

                // Sumowanie
                sumaPadle += d.Padle;
                sumaCH += d.Chore;
                sumaNW += d.NW;
                sumaZM += d.ZM;
                sumaIlosc += d.IloscDek;

                y += rowHeight;
                rowIndex++;
            }

            // Ramka zewnętrzna tabeli danych
            float tableStartY = y - (rowIndex * rowHeight) - headerHeight;
            g.DrawRectangle(penThick, leftMargin, tableStartY, tableWidth, (rowIndex * rowHeight) + headerHeight);

            // Wiersz sumy
            float sumRowHeight = 30;
            g.FillRectangle(brushHeaderBg, leftMargin, y, tableWidth, sumRowHeight);
            g.DrawRectangle(penThick, leftMargin, y, tableWidth, sumRowHeight);

            float sumTextY = y + 8;

            g.DrawString("SUMA:", fontSummary, brushBlack, colX[2] + 4, sumTextY);

            DrawPlachtaCenteredText(g, sumaIlosc.ToString(), fontSummary, brushBlack, colX[7], colWidths[7], sumTextY);
            DrawPlachtaCenteredText(g, sumaPadle.ToString(), fontSummary, brushBlack, colX[10], colWidths[10], sumTextY);
            DrawPlachtaCenteredText(g, sumaCH.ToString(), fontSummary, brushBlack, colX[12], colWidths[12], sumTextY);
            DrawPlachtaCenteredText(g, sumaNW.ToString(), fontSummary, brushBlack, colX[13], colWidths[13], sumTextY);
            DrawPlachtaCenteredText(g, sumaZM.ToString(), fontSummary, brushBlack, colX[14], colWidths[14], sumTextY);

            // Linie pionowe sumy
            for (int i = 1; i < colWidths.Length; i++)
            {
                g.DrawLine(penMedium, colX[i], y, colX[i], y + sumRowHeight);
            }

            // Cleanup
            fontTitle.Dispose();
            fontDate.Dispose();
            fontHeader.Dispose();
            fontKonfiskaty.Dispose();
            fontData.Dispose();
            fontDataBold.Dispose();
            fontSummary.Dispose();
            brushBlack.Dispose();
            brushGray.Dispose();
            brushHeaderBg.Dispose();
            brushAltRow.Dispose();
            penThick.Dispose();
            penMedium.Dispose();
            penThin.Dispose();

            e.HasMorePages = false;
        }

        private void DrawPlachtaCenteredText(System.Drawing.Graphics g, string text, System.Drawing.Font font, System.Drawing.SolidBrush brush, float x, float width, float y)
        {
            System.Drawing.SizeF size = g.MeasureString(text, font);
            g.DrawString(text, font, brush, x + (width - size.Width) / 2, y);
        }

        private void BtnPlachtaOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DateTime selectedDate = dateTimePicker1.SelectedDate ?? DateTime.Today;

                // Użyj domyślnej ścieżki zapisu płacht
                string basePath = defaultPlachtaPath;

                // Utwórz ścieżkę dla dnia (rok/miesiąc/dzień)
                string yearFolder = selectedDate.Year.ToString();
                string monthFolder = selectedDate.Month.ToString("D2");
                string dayFolder = selectedDate.Day.ToString("D2");

                string folderPath = Path.Combine(basePath, yearFolder, monthFolder, dayFolder);

                // Jeśli folder nie istnieje, spróbuj samą bazową ścieżkę
                if (!Directory.Exists(folderPath))
                {
                    // Spróbuj bez dnia
                    folderPath = Path.Combine(basePath, yearFolder, monthFolder);

                    if (!Directory.Exists(folderPath))
                    {
                        // Spróbuj bazową ścieżkę
                        folderPath = basePath;

                        if (!Directory.Exists(folderPath))
                        {
                            MessageBox.Show($"Folder płacht nie istnieje:\n{basePath}\n\nUstaw folder w ustawieniach.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }
                    }
                }

                Process.Start("explorer.exe", folderPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd otwierania folderu:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnPlachtaSavePdf_Click(object sender, RoutedEventArgs e)
        {
            if (plachtaData.Count == 0)
            {
                MessageBox.Show("Brak danych do zapisania!", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                DateTime selectedDate = dateTimePicker1.SelectedDate ?? DateTime.Today;

                // Utwórz ścieżkę dla pliku
                string yearFolder = selectedDate.Year.ToString();
                string monthFolder = selectedDate.Month.ToString("D2");
                string dayFolder = selectedDate.Day.ToString("D2");

                string folderPath = Path.Combine(defaultPlachtaPath, yearFolder, monthFolder, dayFolder);

                // Utwórz folder jeśli nie istnieje
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                string fileName = $"Plachta_{selectedDate:yyyy-MM-dd}_{DateTime.Now:HHmmss}.pdf";
                string filePath = Path.Combine(folderPath, fileName);

                // Utwórz PDF z iTextSharp
                using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    Document doc = new Document(PageSize.A4.Rotate(), 20, 20, 20, 20);
                    PdfWriter writer = PdfWriter.GetInstance(doc, fs);
                    doc.Open();

                    // Tytuł
                    string[] dniTygodnia = { "NIEDZIELA", "PONIEDZIAŁEK", "WTOREK", "ŚRODA", "CZWARTEK", "PIĄTEK", "SOBOTA" };
                    string dzienTygodnia = dniTygodnia[(int)selectedDate.DayOfWeek];

                    BaseFont bf = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1250, BaseFont.NOT_EMBEDDED);
                    iTextSharp.text.Font fontTitle = new iTextSharp.text.Font(bf, 16, iTextSharp.text.Font.BOLD);
                    iTextSharp.text.Font fontHeader = new iTextSharp.text.Font(bf, 8, iTextSharp.text.Font.BOLD);
                    iTextSharp.text.Font fontData = new iTextSharp.text.Font(bf, 8);
                    iTextSharp.text.Font fontDataBold = new iTextSharp.text.Font(bf, 8, iTextSharp.text.Font.BOLD);

                    Paragraph title = new Paragraph($"PŁACHTA - OCENA DOBROSTANU    {selectedDate:dd.MM.yyyy} - {dzienTygodnia}", fontTitle);
                    title.SpacingAfter = 15;
                    doc.Add(title);

                    // Tabela
                    PdfPTable table = new PdfPTable(15);
                    table.WidthPercentage = 100;
                    float[] widths = { 3f, 3f, 12f, 14f, 7f, 6f, 7f, 5f, 7f, 7f, 5f, 5f, 4.5f, 4.5f, 4.5f };
                    table.SetWidths(widths);

                    // Nagłówki
                    string[] headers = { "LP", "NR", "HODOWCA", "ADRES", "SALMON.", "ŚW.ZDR", "NR GOSP", "SZT", "CIĄGNIK", "NACZEPA", "PADŁE", "KOD", "CH", "NW", "ZM" };
                    foreach (var h in headers)
                    {
                        PdfPCell cell = new PdfPCell(new Phrase(h, fontHeader));
                        cell.BackgroundColor = new BaseColor(200, 200, 200);
                        cell.HorizontalAlignment = Element.ALIGN_CENTER;
                        cell.Padding = 4;
                        table.AddCell(cell);
                    }

                    // Dane
                    int sumaPadle = 0, sumaCH = 0, sumaNW = 0, sumaZM = 0, sumaIlosc = 0;

                    foreach (var d in plachtaData)
                    {
                        table.AddCell(new PdfPCell(new Phrase(d.Lp.ToString(), fontDataBold)) { HorizontalAlignment = Element.ALIGN_CENTER });
                        table.AddCell(new PdfPCell(new Phrase(d.NrSpec.ToString(), fontDataBold)) { HorizontalAlignment = Element.ALIGN_CENTER });
                        table.AddCell(new PdfPCell(new Phrase(d.Hodowca ?? "-", fontData)));
                        table.AddCell(new PdfPCell(new Phrase(d.Adres ?? "-", fontData)));
                        table.AddCell(new PdfPCell(new Phrase(d.BadaniaSalmonella ?? "", fontData)) { HorizontalAlignment = Element.ALIGN_CENTER });
                        table.AddCell(new PdfPCell(new Phrase(d.NrSwZdrowia ?? "", fontData)) { HorizontalAlignment = Element.ALIGN_CENTER });
                        table.AddCell(new PdfPCell(new Phrase(d.NrGospodarstwa ?? "", fontDataBold)) { HorizontalAlignment = Element.ALIGN_CENTER });
                        table.AddCell(new PdfPCell(new Phrase(d.IloscDek.ToString(), fontDataBold)) { HorizontalAlignment = Element.ALIGN_CENTER });
                        table.AddCell(new PdfPCell(new Phrase(d.Ciagnik ?? "", fontData)) { HorizontalAlignment = Element.ALIGN_CENTER });
                        table.AddCell(new PdfPCell(new Phrase(d.Naczepa ?? "", fontData)) { HorizontalAlignment = Element.ALIGN_CENTER });
                        table.AddCell(new PdfPCell(new Phrase(d.Padle > 0 ? d.Padle.ToString() : "-", fontDataBold)) { HorizontalAlignment = Element.ALIGN_CENTER });
                        table.AddCell(new PdfPCell(new Phrase(d.KodHodowcy ?? "", fontData)) { HorizontalAlignment = Element.ALIGN_CENTER });
                        table.AddCell(new PdfPCell(new Phrase(d.Chore > 0 ? d.Chore.ToString() : "-", fontDataBold)) { HorizontalAlignment = Element.ALIGN_CENTER });
                        table.AddCell(new PdfPCell(new Phrase(d.NW > 0 ? d.NW.ToString() : "-", fontDataBold)) { HorizontalAlignment = Element.ALIGN_CENTER });
                        table.AddCell(new PdfPCell(new Phrase(d.ZM > 0 ? d.ZM.ToString() : "-", fontDataBold)) { HorizontalAlignment = Element.ALIGN_CENTER });

                        sumaPadle += d.Padle;
                        sumaCH += d.Chore;
                        sumaNW += d.NW;
                        sumaZM += d.ZM;
                        sumaIlosc += d.IloscDek;
                    }

                    // Wiersz sumy
                    PdfPCell sumaCell = new PdfPCell(new Phrase("SUMA:", fontHeader));
                    sumaCell.Colspan = 7;
                    sumaCell.BackgroundColor = new BaseColor(200, 200, 200);
                    sumaCell.HorizontalAlignment = Element.ALIGN_RIGHT;
                    sumaCell.Padding = 4;
                    table.AddCell(sumaCell);

                    table.AddCell(new PdfPCell(new Phrase(sumaIlosc.ToString(), fontHeader)) { BackgroundColor = new BaseColor(200, 200, 200), HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase("", fontHeader)) { BackgroundColor = new BaseColor(200, 200, 200) });
                    table.AddCell(new PdfPCell(new Phrase("", fontHeader)) { BackgroundColor = new BaseColor(200, 200, 200) });
                    table.AddCell(new PdfPCell(new Phrase(sumaPadle.ToString(), fontHeader)) { BackgroundColor = new BaseColor(200, 200, 200), HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase("", fontHeader)) { BackgroundColor = new BaseColor(200, 200, 200) });
                    table.AddCell(new PdfPCell(new Phrase(sumaCH.ToString(), fontHeader)) { BackgroundColor = new BaseColor(200, 200, 200), HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(sumaNW.ToString(), fontHeader)) { BackgroundColor = new BaseColor(200, 200, 200), HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(sumaZM.ToString(), fontHeader)) { BackgroundColor = new BaseColor(200, 200, 200), HorizontalAlignment = Element.ALIGN_CENTER });

                    doc.Add(table);

                    // Dodaj sekcję podpisów z rozwinięciem userId
                    doc.Add(new Paragraph(" "));

                    string wystawiajacyNazwa = GetCurrentUserDisplayName();
                    iTextSharp.text.Font fontFooterPlachta = new iTextSharp.text.Font(bf, 9);

                    Paragraph footerPara = new Paragraph($"Wystawił: {wystawiajacyNazwa}    |    Data wydruku: {DateTime.Now:dd.MM.yyyy HH:mm}", fontFooterPlachta);
                    footerPara.Alignment = Element.ALIGN_RIGHT;
                    doc.Add(footerPara);

                    doc.Close();
                }

                UpdateStatus($"Zapisano PDF: {fileName}");
                MessageBox.Show($"Płachta została zapisana:\n{filePath}", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisywania PDF:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Generuje PDF dla lekarzy weterynarii z danymi specyfikacji
        /// Format: LP, Zestaw, Nazwa hodowcy, Nr specyfikacji, Liczba zdatnych sztuk (LUMEL - sztuki konfiskaty)
        /// </summary>
        private void BtnPlachtaDlaLekarzy_Click(object sender, RoutedEventArgs e)
        {
            if (plachtaData.Count == 0)
            {
                MessageBox.Show("Brak danych do wygenerowania raportu dla lekarzy!", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                DateTime selectedDate = dateTimePicker1.SelectedDate ?? DateTime.Today;

                // Utwórz ścieżkę dla pliku
                string yearFolder = selectedDate.Year.ToString();
                string monthFolder = selectedDate.Month.ToString("D2");
                string dayFolder = selectedDate.Day.ToString("D2");

                string folderPath = Path.Combine(defaultPlachtaPath, yearFolder, monthFolder, dayFolder);

                // Utwórz folder jeśli nie istnieje
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                string fileName = $"DlaLekarzy_{selectedDate:yyyy-MM-dd}_{DateTime.Now:HHmmss}.pdf";
                string filePath = Path.Combine(folderPath, fileName);

                // Pobierz dane specyfikacji dla obliczenia liczby zdanych sztuk
                var specyfikacjeDict = specyfikacjeData?.ToDictionary(s => s.Nr, s => s) ?? new Dictionary<int, SpecyfikacjaRow>();

                // Utwórz PDF z iTextSharp - format pionowy A4 na całą stronę
                using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    Document doc = new Document(PageSize.A4, 30, 30, 40, 60);
                    PdfWriter writer = PdfWriter.GetInstance(doc, fs);
                    doc.Open();

                    // Czcionki
                    string arialPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                    BaseFont bfArial;
                    if (File.Exists(arialPath))
                    {
                        bfArial = BaseFont.CreateFont(arialPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                    }
                    else
                    {
                        bfArial = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1250, BaseFont.NOT_EMBEDDED);
                    }

                    iTextSharp.text.Font fontTitle = new iTextSharp.text.Font(bfArial, 16, iTextSharp.text.Font.BOLD);
                    iTextSharp.text.Font fontSubtitle = new iTextSharp.text.Font(bfArial, 11, iTextSharp.text.Font.NORMAL);
                    iTextSharp.text.Font fontHeader = new iTextSharp.text.Font(bfArial, 9, iTextSharp.text.Font.BOLD, BaseColor.WHITE);
                    iTextSharp.text.Font fontData = new iTextSharp.text.Font(bfArial, 9);
                    iTextSharp.text.Font fontDataBold = new iTextSharp.text.Font(bfArial, 9, iTextSharp.text.Font.BOLD);
                    iTextSharp.text.Font fontSum = new iTextSharp.text.Font(bfArial, 10, iTextSharp.text.Font.BOLD);
                    iTextSharp.text.Font fontFooter = new iTextSharp.text.Font(bfArial, 8);
                    iTextSharp.text.Font fontSignature = new iTextSharp.text.Font(bfArial, 8, iTextSharp.text.Font.ITALIC);

                    // Nazwa dnia tygodnia
                    string[] dniTygodnia = { "NIEDZIELA", "PONIEDZIAŁEK", "WTOREK", "ŚRODA", "CZWARTEK", "PIĄTEK", "SOBOTA" };
                    string dzienTygodnia = dniTygodnia[(int)selectedDate.DayOfWeek];

                    // === NAGŁÓWEK ===
                    // Logo firmy (jeśli istnieje)
                    string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.png");
                    if (File.Exists(logoPath))
                    {
                        try
                        {
                            iTextSharp.text.Image logo = iTextSharp.text.Image.GetInstance(logoPath);
                            logo.ScaleToFit(80, 40);
                            logo.Alignment = Element.ALIGN_CENTER;
                            doc.Add(logo);
                        }
                        catch { }
                    }

                    // Tytuł główny
                    Paragraph title = new Paragraph("SPECYFIKACJA DLA WETERYNARII", fontTitle);
                    title.Alignment = Element.ALIGN_CENTER;
                    title.SpacingAfter = 3;
                    doc.Add(title);

                    // Podtytuł z datą
                    Paragraph subtitle = new Paragraph($"Data: {selectedDate:dd.MM.yyyy} ({dzienTygodnia})", fontSubtitle);
                    subtitle.Alignment = Element.ALIGN_CENTER;
                    subtitle.SpacingAfter = 8;
                    doc.Add(subtitle);

                    // === TABELA GŁÓWNA ===
                    PdfPTable table = new PdfPTable(6);
                    table.WidthPercentage = 98;
                    float[] widths = { 12f, 8f, 38f, 10f, 10f, 22f };
                    table.SetWidths(widths);
                    table.SpacingBefore = 10;

                    // Nagłówki tabeli - szare, z zawijaniem tekstu
                    BaseColor headerColor = new BaseColor(80, 80, 80);
                    iTextSharp.text.Font fontHeaderBW = new iTextSharp.text.Font(bfArial, 9, iTextSharp.text.Font.BOLD, BaseColor.WHITE);

                    string[] headers = { "LP\n(ZESTAW)", "NR\nSPEC.", "NAZWA HODOWCY", "KOD", "ŚR. WAGA\n[kg]", "SZTUKI\nZDATNE" };
                    foreach (var h in headers)
                    {
                        PdfPCell cell = new PdfPCell(new Phrase(h, fontHeaderBW));
                        cell.BackgroundColor = headerColor;
                        cell.HorizontalAlignment = Element.ALIGN_CENTER;
                        cell.VerticalAlignment = Element.ALIGN_MIDDLE;
                        cell.Padding = 6;
                        cell.PaddingTop = 8;
                        cell.PaddingBottom = 8;
                        cell.BorderColor = BaseColor.BLACK;
                        cell.BorderWidth = 1f;
                        table.AddCell(cell);
                    }

                    // Dane - wyższe wiersze
                    int sumaSztukZdatnych = 0;
                    int lp = 1;

                    foreach (var d in plachtaData)
                    {
                        // Oblicz sztuki zdatne zgodnie ze wzorem ze specyfikacji PDF:
                        // Dostarcz.(ARIMR) = LUMEL + Padłe
                        // Zdatne = Dostarcz - Padłe - Konf = (LUMEL + Padłe) - Padłe - Konf = LUMEL - Konf
                        // Padłe się kasuje! Więc: Zdatne = LUMEL - (CH + NW + ZM)
                        int sztukiZdatne = d.Lumel - (d.Chore + d.NW + d.ZM);
                        if (sztukiZdatne < 0) sztukiZdatne = 0;

                        sumaSztukZdatnych += sztukiZdatne;

                        // LP z "Zestaw"
                        PdfPCell cellLp = new PdfPCell(new Phrase($"{lp} zestaw", fontDataBold));
                        cellLp.BackgroundColor = BaseColor.WHITE;
                        cellLp.HorizontalAlignment = Element.ALIGN_CENTER;
                        cellLp.VerticalAlignment = Element.ALIGN_MIDDLE;
                        cellLp.Padding = 6;
                        cellLp.MinimumHeight = 28f;
                        cellLp.BorderColor = BaseColor.BLACK;
                        cellLp.BorderWidth = 0.5f;
                        table.AddCell(cellLp);

                        // Nr Specyfikacji
                        PdfPCell cellNrSpec = new PdfPCell(new Phrase(d.NrSpec.ToString(), fontDataBold));
                        cellNrSpec.BackgroundColor = BaseColor.WHITE;
                        cellNrSpec.HorizontalAlignment = Element.ALIGN_CENTER;
                        cellNrSpec.VerticalAlignment = Element.ALIGN_MIDDLE;
                        cellNrSpec.Padding = 3;
                        cellNrSpec.BorderColor = BaseColor.BLACK;
                        cellNrSpec.BorderWidth = 0.5f;
                        table.AddCell(cellNrSpec);

                        // Nazwa hodowcy
                        PdfPCell cellHodowca = new PdfPCell(new Phrase(d.Hodowca ?? "-", fontData));
                        cellHodowca.BackgroundColor = BaseColor.WHITE;
                        cellHodowca.HorizontalAlignment = Element.ALIGN_LEFT;
                        cellHodowca.VerticalAlignment = Element.ALIGN_MIDDLE;
                        cellHodowca.Padding = 6;
                        cellHodowca.PaddingLeft = 8;
                        cellHodowca.MinimumHeight = 28f;
                        cellHodowca.BorderColor = BaseColor.BLACK;
                        cellHodowca.BorderWidth = 0.5f;
                        table.AddCell(cellHodowca);

                        // Kod hodowcy (ID)
                        PdfPCell cellKod = new PdfPCell(new Phrase(d.KodHodowcy ?? "-", fontData));
                        cellKod.BackgroundColor = BaseColor.WHITE;
                        cellKod.HorizontalAlignment = Element.ALIGN_CENTER;
                        cellKod.VerticalAlignment = Element.ALIGN_MIDDLE;
                        cellKod.Padding = 6;
                        cellKod.MinimumHeight = 28f;
                        cellKod.BorderColor = BaseColor.BLACK;
                        cellKod.BorderWidth = 0.5f;
                        table.AddCell(cellKod);

                        // Średnia waga
                        PdfPCell cellSrWaga = new PdfPCell(new Phrase(d.SredniaWaga.ToString("F2"), fontData));
                        cellSrWaga.BackgroundColor = BaseColor.WHITE;
                        cellSrWaga.HorizontalAlignment = Element.ALIGN_CENTER;
                        cellSrWaga.VerticalAlignment = Element.ALIGN_MIDDLE;
                        cellSrWaga.Padding = 6;
                        cellSrWaga.MinimumHeight = 28f;
                        cellSrWaga.BorderColor = BaseColor.BLACK;
                        cellSrWaga.BorderWidth = 0.5f;
                        table.AddCell(cellSrWaga);

                        // Sztuki zdatne
                        PdfPCell cellSztuki = new PdfPCell(new Phrase(sztukiZdatne.ToString("N0"), fontDataBold));
                        cellSztuki.BackgroundColor = BaseColor.WHITE;
                        cellSztuki.HorizontalAlignment = Element.ALIGN_CENTER;
                        cellSztuki.VerticalAlignment = Element.ALIGN_MIDDLE;
                        cellSztuki.Padding = 6;
                        cellSztuki.MinimumHeight = 28f;
                        cellSztuki.BorderColor = BaseColor.BLACK;
                        cellSztuki.BorderWidth = 0.5f;
                        table.AddCell(cellSztuki);

                        lp++;
                    }

                    // Wiersz sumy - szary
                    BaseColor sumColor = new BaseColor(70, 70, 70);

                    PdfPCell sumaLabelCell = new PdfPCell();
                    sumaLabelCell.Colspan = 5;
                    sumaLabelCell.BackgroundColor = sumColor;
                    sumaLabelCell.HorizontalAlignment = Element.ALIGN_RIGHT;
                    sumaLabelCell.VerticalAlignment = Element.ALIGN_MIDDLE;
                    sumaLabelCell.Padding = 8;
                    sumaLabelCell.BorderColor = BaseColor.BLACK;
                    sumaLabelCell.BorderWidth = 1f;
                    iTextSharp.text.Font fontSumWhite = new iTextSharp.text.Font(bfArial, 11, iTextSharp.text.Font.BOLD, BaseColor.WHITE);
                    sumaLabelCell.Phrase = new Phrase("SUMA SZTUK ZDATNYCH:", fontSumWhite);
                    table.AddCell(sumaLabelCell);

                    PdfPCell sumaValueCell = new PdfPCell(new Phrase(sumaSztukZdatnych.ToString("N0"), fontSumWhite));
                    sumaValueCell.BackgroundColor = sumColor;
                    sumaValueCell.HorizontalAlignment = Element.ALIGN_CENTER;
                    sumaValueCell.VerticalAlignment = Element.ALIGN_MIDDLE;
                    sumaValueCell.Padding = 8;
                    sumaValueCell.BorderColor = BaseColor.BLACK;
                    sumaValueCell.BorderWidth = 1f;
                    table.AddCell(sumaValueCell);

                    doc.Add(table);

                    // === SEKCJA PODPISU - więcej miejsca ===
                    doc.Add(new Paragraph(" "));
                    doc.Add(new Paragraph(" "));
                    doc.Add(new Paragraph(" "));

                    // Tabela z podpisami
                    PdfPTable signatureTable = new PdfPTable(2);
                    signatureTable.WidthPercentage = 100;
                    signatureTable.SetWidths(new float[] { 50f, 50f });

                    // Lewa kolumna - kto wydrukował
                    PdfPCell leftCell = new PdfPCell();
                    leftCell.Border = Rectangle.NO_BORDER;
                    leftCell.Padding = 3;

                    // Pobierz imię i nazwisko użytkownika
                    NazwaZiD nazwaZiD = new NazwaZiD();
                    string userName = nazwaZiD.GetNameById(App.UserID) ?? App.UserID ?? Environment.UserName;
                    Paragraph userLabel = new Paragraph($"Wydrukował: {userName}", fontFooter);
                    Paragraph genDate = new Paragraph($"Data wydruku: {DateTime.Now:dd.MM.yyyy HH:mm}", fontFooter);
                    genDate.SpacingBefore = 2;
                    leftCell.AddElement(userLabel);
                    leftCell.AddElement(genDate);
                    signatureTable.AddCell(leftCell);

                    // Prawa kolumna - podpis
                    PdfPCell rightCell = new PdfPCell();
                    rightCell.Border = Rectangle.NO_BORDER;
                    rightCell.Padding = 3;
                    rightCell.HorizontalAlignment = Element.ALIGN_RIGHT;

                    Paragraph signLine = new Paragraph("_______________________________", fontFooter);
                    signLine.Alignment = Element.ALIGN_CENTER;
                    Paragraph signLabel = new Paragraph("Podpis lekarza weterynarii", fontSignature);
                    signLabel.Alignment = Element.ALIGN_CENTER;
                    signLabel.SpacingBefore = 2;

                    rightCell.AddElement(signLine);
                    rightCell.AddElement(signLabel);
                    signatureTable.AddCell(rightCell);

                    doc.Add(signatureTable);

                    // Stopka informacyjna
                    Paragraph footer = new Paragraph(
                        "UBOJNIA DROBIU \"PIÓRKOWSCY\" JERZY PIÓRKOWSKI - Dokument wygenerowany automatycznie",
                        fontSignature);
                    footer.Alignment = Element.ALIGN_CENTER;
                    footer.SpacingBefore = 10;
                    doc.Add(footer);

                    doc.Close();
                }

                UpdateStatus($"Zapisano PDF dla lekarzy: {fileName}");

                // Pytaj czy otworzyć plik
                var result = MessageBox.Show(
                    $"Raport dla lekarzy został zapisany:\n{filePath}\n\nCzy chcesz otworzyć plik?",
                    "Sukces", MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd generowania PDF dla lekarzy:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnPlachtaFolderSettings_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Wybierz folder do zapisywania płacht PDF",
                ShowNewFolderButton = true,
                SelectedPath = defaultPlachtaPath
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                defaultPlachtaPath = dialog.SelectedPath;
                SavePlachtaFolderSetting();
                MessageBox.Show($"Folder płacht ustawiony na:\n{defaultPlachtaPath}", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SavePlachtaFolderSetting()
        {
            EnsureSettingsTableExists();
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string user = Environment.UserName;

                    string mergeSql = @"
                        MERGE FarmerCalcSettings AS target
                        USING (SELECT 'DefaultPlachtaPath' AS SettingName) AS source
                        ON target.SettingName = source.SettingName
                        WHEN MATCHED THEN UPDATE SET SettingValue = @Value, ModifiedDate = GETDATE(), ModifiedBy = @User
                        WHEN NOT MATCHED THEN INSERT (SettingName, SettingValue, ModifiedBy) VALUES ('DefaultPlachtaPath', @Value, @User);";

                    using (SqlCommand cmd = new SqlCommand(mergeSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Value", defaultPlachtaPath);
                        cmd.Parameters.AddWithValue("@User", user);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving plachta folder: {ex.Message}");
            }
        }

        private void BtnOpenPdfFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DateTime selectedDate = dateTimePicker1.SelectedDate ?? DateTime.Today;

                // Użyj domyślnej ścieżki zapisu specyfikacji
                string basePath = defaultPdfPath;

                // Utwórz ścieżkę dla dnia (rok/miesiąc/dzień)
                string yearFolder = selectedDate.Year.ToString();
                string monthFolder = selectedDate.Month.ToString("D2");
                string dayFolder = selectedDate.Day.ToString("D2");

                string folderPath = Path.Combine(basePath, yearFolder, monthFolder, dayFolder);

                // Jeśli folder nie istnieje, spróbuj samą bazową ścieżkę
                if (!Directory.Exists(folderPath))
                {
                    folderPath = Path.Combine(basePath, yearFolder, monthFolder);

                    if (!Directory.Exists(folderPath))
                    {
                        folderPath = basePath;

                        if (!Directory.Exists(folderPath))
                        {
                            MessageBox.Show($"Folder specyfikacji nie istnieje:\n{basePath}", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }
                    }
                }

                Process.Start("explorer.exe", folderPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd otwierania folderu:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Dodawanie specyfikacji

        /// <summary>
        /// Dodaje nową specyfikację do karty Specyfikacje
        /// </summary>
        private void BtnDodajSpecyfikacje_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DateTime selectedDate = dateTimePicker1.SelectedDate ?? DateTime.Today;

                // Sprawdź czy edycja jest dozwolona
                if (!CheckEditingAllowed(selectedDate))
                {
                    return;
                }

                // Sprawdź czy Shift jest wciśnięty - wtedy stary tryb (szybkie dodanie pustego wiersza)
                bool shiftPressed = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

                if (shiftPressed)
                {
                    // Stary tryb - szybkie dodanie pustego wiersza
                    AddEmptySpecyfikacja(selectedDate);
                }
                else
                {
                    // Nowy tryb - otwórz kreator
                    var kreatorWindow = new Kalendarz1.Zywiec.WidokSpecyfikacji.NowaSpecyfikacjaWindow(connectionString, selectedDate);
                    kreatorWindow.Owner = Window.GetWindow(this);

                    if (kreatorWindow.ShowDialog() == true && kreatorWindow.SpecyfikacjaCreated)
                    {
                        // Odśwież dane
                        LoadData(selectedDate);
                        UpdateStatistics();
                        UpdateStatus($"Dodano specyfikację ID: {kreatorWindow.CreatedSpecId}");

                        // Opcjonalnie wydrukuj PDF
                        if (kreatorWindow.PrintPdf && kreatorWindow.CreatedSpecId > 0)
                        {
                            // Znajdź wiersz i wydrukuj
                            var newRow = specyfikacjeData.FirstOrDefault(s => s.ID == kreatorWindow.CreatedSpecId);
                            if (newRow != null)
                            {
                                dataGridView1.SelectedItem = newRow;
                                // TODO: Wywołaj drukowanie PDF
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd dodawania specyfikacji:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        /// <summary>
        /// Szybkie dodanie pustego wiersza specyfikacji (stary tryb)
        /// </summary>
        private void AddEmptySpecyfikacja(DateTime selectedDate)
        {
            // Znajdź następny numer LP
            int nextNr = 1;
            if (specyfikacjeData != null && specyfikacjeData.Count > 0)
            {
                nextNr = specyfikacjeData.Max(s => s.Nr) + 1;
            }

            // Utwórz nowy wiersz w bazie danych
            int newId = CreateNewSpecyfikacjaInDatabase(selectedDate, nextNr);

            if (newId > 0)
            {
                // Utwórz nowy wiersz w lokalnej kolekcji
                var newRow = new SpecyfikacjaRow
                {
                    ID = newId,
                    Nr = nextNr,
                    Number = 0,
                    YearNumber = 0,
                    Dostawca = "",
                    RealDostawca = "",
                    SztukiDek = 0,
                    Padle = 0,
                    CH = 0,
                    NW = 0,
                    ZM = 0,
                    LUMEL = 0,
                    SztukiWybijak = 0,
                    KilogramyWybijak = 0,
                    Cena = 0,
                    Dodatek = 0,
                    TypCeny = "wolnyrynek",
                    Ubytek = 0,
                    PiK = false,
                    SupplierColor = GenerateColorForSupplier("")
                };

                specyfikacjeData.Add(newRow);

                // Odśwież DataGrid i zaznacz nowy wiersz
                dataGridView1.ItemsSource = null;
                dataGridView1.ItemsSource = specyfikacjeData;
                dataGridView1.SelectedItem = newRow;
                dataGridView1.ScrollIntoView(newRow);

                // Rozpocznij edycję komórki Dostawca
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    dataGridView1.CurrentCell = new DataGridCellInfo(newRow, dataGridView1.Columns[5]); // Kolumna Dostawca
                    dataGridView1.BeginEdit();
                }), System.Windows.Threading.DispatcherPriority.Background);

                UpdateStatistics();
                UpdateStatus($"Dodano nową specyfikację LP: {nextNr}");
            }
            else
            {
                MessageBox.Show("Nie udało się utworzyć nowej specyfikacji w bazie danych.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region Import z Excel

        /// <summary>
        /// Otwiera kreator importu specyfikacji z pliku Excel/LibreOffice
        /// </summary>
        private void BtnImportExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var wizard = new Kalendarz1.Zywiec.WidokSpecyfikacji.ImportSpecyfikacjeWizard(connectionString);

                // Callback do odświeżenia danych po imporcie
                wizard.OnImportCompleted = () =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        // Odśwież dane w DataGrid po imporcie
                        if (dateTimePicker1.SelectedDate.HasValue)
                        {
                            LoadData(dateTimePicker1.SelectedDate.Value);

                            // Pokaż komunikat sukcesu
                            statusLabel.Text = "Import zakończony - dane odświeżone";
                        }
                    });
                };

                wizard.Owner = this;
                var result = wizard.ShowDialog();

                // Jeśli import się powiódł, odśwież widok
                if (result == true && dateTimePicker1.SelectedDate.HasValue)
                {
                    LoadData(dateTimePicker1.SelectedDate.Value);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd otwierania kreatora importu:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"BtnImportExcel_Click error: {ex}");
            }
        }

        #endregion

        /// <summary>
        /// Otwiera folder z plikami specyfikacji dla zaznaczonego wiersza
        /// </summary>
        private void BtnOtworzFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (selectedRow == null)
                {
                    MessageBox.Show("Najpierw zaznacz specyfikację.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Pobierz ścieżkę do folderu specyfikacji
                string folderPath = GetSpecyfikacjaFolderPath(selectedRow);

                if (string.IsNullOrEmpty(folderPath) || !System.IO.Directory.Exists(folderPath))
                {
                    // Spróbuj użyć domyślnego folderu
                    folderPath = GetDefaultSpecyfikacjaFolder();
                    if (string.IsNullOrEmpty(folderPath) || !System.IO.Directory.Exists(folderPath))
                    {
                        MessageBox.Show("Folder specyfikacji nie istnieje lub nie został skonfigurowany.\nSkonfiguruj domyślny folder w Panelu Administracyjnym.",
                            "Folder nie znaleziony", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                System.Diagnostics.Process.Start("explorer.exe", folderPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd otwierania folderu:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Zwraca ścieżkę do folderu specyfikacji dla danego wiersza
        /// </summary>
        private string GetSpecyfikacjaFolderPath(SpecyfikacjaRow row)
        {
            if (row == null) return null;

            // Sprawdź czy wiersz ma zapisaną ścieżkę PDF
            if (!string.IsNullOrEmpty(row.PdfPath) && System.IO.File.Exists(row.PdfPath))
            {
                return System.IO.Path.GetDirectoryName(row.PdfPath);
            }

            // Użyj domyślnego folderu
            return GetDefaultSpecyfikacjaFolder();
        }

        /// <summary>
        /// Zwraca domyślny folder dla specyfikacji (używa tego samego ustawienia co DefaultPdfPath)
        /// </summary>
        private string GetDefaultSpecyfikacjaFolder()
        {
            // Użyj defaultPdfPath z ustawień panelu admina
            if (!string.IsNullOrEmpty(defaultPdfPath) && System.IO.Directory.Exists(defaultPdfPath))
            {
                return defaultPdfPath;
            }

            try
            {
                // Spróbuj pobrać z bazy
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT SettingValue FROM FarmerCalcSettings WHERE SettingName = 'DefaultPdfPath'";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        var result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value && !string.IsNullOrEmpty(result.ToString()))
                        {
                            return result.ToString();
                        }
                    }
                }
            }
            catch { }

            // Domyślna ścieżka
            return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Specyfikacje");
        }

        /// <summary>
        /// Wysyła specyfikację emailem
        /// </summary>
        private void BtnWyslijEmail_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (selectedRow == null)
                {
                    MessageBox.Show("Najpierw zaznacz specyfikację.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Sprawdź czy specyfikacja ma PDF
                string pdfPath = selectedRow.PdfPath;
                if (string.IsNullOrEmpty(pdfPath) || !System.IO.File.Exists(pdfPath))
                {
                    MessageBox.Show("Specyfikacja nie ma wygenerowanego pliku PDF.\nNajpierw wygeneruj PDF.",
                        "Brak pliku PDF", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Otwórz domyślny klient email z załącznikiem
                string subject = $"Specyfikacja {selectedRow.RealDostawca} - {dateTimePicker1.SelectedDate:yyyy-MM-dd}";
                string body = $"W załączniku przesyłam specyfikację dla {selectedRow.RealDostawca}.";

                // Użyj mailto z załącznikiem (działa z Outlook)
                string mailto = $"mailto:?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}";

                // Otwórz klienta email
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = mailto,
                    UseShellExecute = true
                });

                // Poinformuj użytkownika o załączniku
                MessageBox.Show($"Otworiono klienta email.\n\nPamiętaj aby ręcznie załączyć plik:\n{pdfPath}",
                    "Wyślij email", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wysyłania email:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Tworzy nowy wiersz specyfikacji w bazie danych i zwraca jego ID
        /// </summary>
        private int CreateNewSpecyfikacjaInDatabase(DateTime calcDate, int carLp)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Sprawdź czy tabela FarmerCalc istnieje i ma odpowiednie kolumny
                    string checkQuery = @"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FarmerCalc')
                        BEGIN
                            CREATE TABLE dbo.FarmerCalc (
                                ID INT IDENTITY(1,1) PRIMARY KEY,
                                CalcDate DATE NOT NULL,
                                CarLp INT NOT NULL,
                                CustomerGID NVARCHAR(50) NULL,
                                CustomerRealGID NVARCHAR(50) NULL,
                                DeclI1 INT DEFAULT 0,
                                DeclI2 INT DEFAULT 0,
                                DeclI3 INT DEFAULT 0,
                                DeclI4 INT DEFAULT 0,
                                DeclI5 INT DEFAULT 0,
                                LumQnt INT DEFAULT 0,
                                ProdQnt INT DEFAULT 0,
                                ProdWgt DECIMAL(18,2) DEFAULT 0,
                                Price DECIMAL(18,2) DEFAULT 0,
                                Addition DECIMAL(18,2) DEFAULT 0,
                                Loss DECIMAL(18,2) DEFAULT 0,
                                IncDeadConf BIT DEFAULT 0,
                                NettoWeight DECIMAL(18,2) DEFAULT 0,
                                Symfonia BIT DEFAULT 0,
                                PriceTypeID INT NULL,
                                TerminDni INT DEFAULT 0,
                                IncPiK BIT DEFAULT 0
                            )
                        END";

                    using (SqlCommand checkCmd = new SqlCommand(checkQuery, connection))
                    {
                        checkCmd.ExecuteNonQuery();
                    }

                    string query = @"
                        INSERT INTO dbo.FarmerCalc (CalcDate, CarLp, CustomerGID, CustomerRealGID, DeclI1, DeclI2, DeclI3, DeclI4, DeclI5, LumQnt, ProdQnt, ProdWgt, Price, Addition, Loss, IncDeadConf)
                        OUTPUT INSERTED.ID
                        VALUES (@CalcDate, @CarLp, NULL, NULL, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)";

                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@CalcDate", calcDate.Date);
                        cmd.Parameters.AddWithValue("@CarLp", carLp);

                        var result = cmd.ExecuteScalar();
                        if (result != null && int.TryParse(result.ToString(), out int newId))
                        {
                            return newId;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating new specyfikacja: {ex.Message}");
                MessageBox.Show($"Szczegóły błędu tworzenia specyfikacji:\n{ex.Message}\n\nStack trace:\n{ex.StackTrace}",
                    "Błąd bazy danych", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return 0;
        }

        /// <summary>
        /// Generuje kolor dla dostawcy (do paska bocznego)
        /// </summary>
        private string GenerateColorForSupplier(string supplierName)
        {
            if (string.IsNullOrEmpty(supplierName))
                return "#CCCCCC";

            // Użyj hash stringa do generowania koloru
            int hash = supplierName.GetHashCode();
            var r = (hash & 0xFF0000) >> 16;
            var g = (hash & 0x00FF00) >> 8;
            var b = hash & 0x0000FF;

            // Rozjaśnij kolor, żeby był czytelny
            r = Math.Min(255, r + 100);
            g = Math.Min(255, g + 100);
            b = Math.Min(255, b + 100);

            return $"#{r:X2}{g:X2}{b:X2}";
        }

        #endregion

        #region Admin Panel

        private int _dniBlokady = 3;
        private List<string> _przelozeni = new List<string> { "11111" }; // Domyślny admin UserID
        private Dictionary<DateTime, bool> _odblokowaneDni = new Dictionary<DateTime, bool>();
        private string _defaultPlachtaSavePath = "";
        private string _defaultPodsumowaniePath = "";

        private bool IsCurrentUserAdmin()
        {
            string currentUserId = App.UserID ?? "";
            return _przelozeni.Any(p => p == currentUserId);
        }

        private void BtnAdmin_Click(object sender, RoutedEventArgs e)
        {
            // Sprawdź czy użytkownik ma uprawnienia (po UserID)
            if (!IsCurrentUserAdmin())
            {
                MessageBox.Show($"Brak uprawnień do panelu administracyjnego.\nTwój UserID: {App.UserID ?? "nieznany"}\nSkontaktuj się z przełożonym.",
                    "Brak dostępu", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Pokaż/ukryj panel
            adminPanel.Visibility = adminPanel.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;

            if (adminPanel.Visibility == Visibility.Visible)
            {
                LoadAdminSettings();
                UpdateBlockingStatus();
            }
        }

        private void BtnCloseAdmin_Click(object sender, RoutedEventArgs e)
        {
            adminPanel.Visibility = Visibility.Collapsed;
        }

        private void BtnBrowsePdfPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Wybierz folder do zapisywania specyfikacji PDF",
                ShowNewFolderButton = true,
                SelectedPath = txtDefaultPdfPath.Text
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                txtDefaultPdfPath.Text = dialog.SelectedPath;
            }
        }

        private void BtnBrowsePlachtaPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Wybierz folder do zapisywania PDF 'Dla Lekarzy'",
                ShowNewFolderButton = true,
                SelectedPath = defaultPlachtaPath ?? ""
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                defaultPlachtaPath = dialog.SelectedPath;
                var txtBox = this.FindName("txtDefaultPlachtaPath") as TextBox;
                if (txtBox != null) txtBox.Text = dialog.SelectedPath;
            }
        }

        private void BtnBrowsePlachtaSavePath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Wybierz folder do zapisywania PDF Płachty",
                ShowNewFolderButton = true,
                SelectedPath = _defaultPlachtaSavePath ?? ""
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _defaultPlachtaSavePath = dialog.SelectedPath;
                var txtBox = this.FindName("txtDefaultPlachtaSavePath") as TextBox;
                if (txtBox != null) txtBox.Text = dialog.SelectedPath;
            }
        }

        private void BtnBrowsePodsumowaniePath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Wybierz folder do zapisywania PDF Podsumowań",
                ShowNewFolderButton = true,
                SelectedPath = _defaultPodsumowaniePath ?? ""
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _defaultPodsumowaniePath = dialog.SelectedPath;
                var txtBox = this.FindName("txtDefaultPodsumowaniePath") as TextBox;
                if (txtBox != null) txtBox.Text = dialog.SelectedPath;
            }
        }

        private void BtnSaveAdminSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Pobierz wartości z UI
                if (int.TryParse(txtDniBlokady.Text, out int dni))
                {
                    _dniBlokady = dni;
                }

                _przelozeni = txtPrzelozeni.Text
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToList();

                // Pobierz ścieżkę PDF
                if (!string.IsNullOrWhiteSpace(txtDefaultPdfPath.Text))
                {
                    defaultPdfPath = txtDefaultPdfPath.Text.Trim();
                }

                // Pobierz tryb kolorów PDF
                _pdfCzarnoBialy = rbPdfCzarnoBialy.IsChecked == true;

                // Pobierz opcję drukowania terminu płatności
                _drukujTerminPlatnosci = chkDrukujTerminPlatnosci.IsChecked == true;

                // Zapisz do bazy
                SaveAdminSettings();

                MessageBox.Show("Ustawienia zostały zapisane.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                UpdateBlockingStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu ustawień: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnOdblokujDzien_Click(object sender, RoutedEventArgs e)
        {
            DateTime selectedDate = dateTimePicker1.SelectedDate ?? DateTime.Today;
            _odblokowaneDni[selectedDate] = true;
            SaveDayUnlockStatus(selectedDate, true);
            UpdateBlockingStatus();
            UpdateStatus($"Dzień {selectedDate:yyyy-MM-dd} został odblokowany do edycji");
        }

        private void BtnZablokujDzien_Click(object sender, RoutedEventArgs e)
        {
            DateTime selectedDate = dateTimePicker1.SelectedDate ?? DateTime.Today;
            _odblokowaneDni[selectedDate] = false;
            SaveDayUnlockStatus(selectedDate, false);
            UpdateBlockingStatus();
            UpdateStatus($"Dzień {selectedDate:yyyy-MM-dd} został zablokowany");
        }

        /// <summary>
        /// Usuwa wszystkie specyfikacje i powiązane dane z wybranego dnia
        /// </summary>
        private void BtnUsunWszystkieZDnia_Click(object sender, RoutedEventArgs e)
        {
            // Sprawdź czy użytkownik ma uprawnienia
            if (!IsCurrentUserAdmin())
            {
                MessageBox.Show("Brak uprawnień do tej operacji.\nTylko administrator może usuwać dane.",
                    "Brak dostępu", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Sprawdź czy wybrano datę
            if (!dateTimePicker1.SelectedDate.HasValue)
            {
                MessageBox.Show("Najpierw wybierz datę.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            DateTime selectedDate = dateTimePicker1.SelectedDate.Value.Date;

            // Pobierz liczbę rekordów do usunięcia
            int recordCount = 0;
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.FarmerCalc WHERE CalcDate = @Date AND (Deleted = 0 OR Deleted IS NULL)", connection))
                    {
                        cmd.Parameters.AddWithValue("@Date", selectedDate);
                        recordCount = (int)cmd.ExecuteScalar();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd sprawdzania danych:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (recordCount == 0)
            {
                MessageBox.Show($"Brak specyfikacji do usunięcia z dnia {selectedDate:dd.MM.yyyy}.",
                    "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Potwierdzenie 1
            var result1 = MessageBox.Show(
                $"Czy na pewno chcesz usunąć WSZYSTKIE specyfikacje z dnia {selectedDate:dd.MM.yyyy}?\n\n" +
                $"Liczba rekordów do usunięcia: {recordCount}\n\n" +
                "Ta operacja jest NIEODWRACALNA!",
                "⚠️ Potwierdzenie usunięcia",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result1 != MessageBoxResult.Yes) return;

            // Potwierdzenie 2 - wpisz "USUŃ"
            var inputWindow = new System.Windows.Window
            {
                Title = "Potwierdzenie usunięcia",
                Width = 400,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var stack = new StackPanel { Margin = new Thickness(20) };
            stack.Children.Add(new TextBlock
            {
                Text = $"Aby potwierdzić usunięcie danych z {selectedDate:dd.MM.yyyy},\nwpisz słowo USUŃ:",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            });
            var inputBox = new TextBox { Height = 30, FontSize = 14 };
            stack.Children.Add(inputBox);
            var btnConfirm = new Button
            {
                Content = "Potwierdź usunięcie",
                Margin = new Thickness(0, 15, 0, 0),
                Padding = new Thickness(20, 8, 20, 8),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D32F2F")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            btnConfirm.Click += (s, args) => { inputWindow.DialogResult = true; inputWindow.Close(); };
            stack.Children.Add(btnConfirm);
            inputWindow.Content = stack;

            if (inputWindow.ShowDialog() != true || inputBox.Text.Trim().ToUpper() != "USUŃ")
            {
                MessageBox.Show("Operacja anulowana - nieprawidłowe potwierdzenie.",
                    "Anulowano", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Wykonaj usunięcie
            try
            {
                int deletedSpecs = 0;
                int deletedLogs = 0;

                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Usuń logi zmian (jeśli tabela istnieje)
                    try
                    {
                        using (var cmd = new SqlCommand(@"
                            DELETE FROM dbo.FarmerCalcChangeLog 
                            WHERE FarmerCalcID IN (SELECT ID FROM dbo.FarmerCalc WHERE CalcDate = @Date)", connection))
                        {
                            cmd.Parameters.AddWithValue("@Date", selectedDate);
                            deletedLogs = cmd.ExecuteNonQuery();
                        }
                    }
                    catch
                    {
                        // Tabela FarmerCalcChangeLog może nie istnieć - ignorujemy
                        deletedLogs = 0;
                    }

                    // Usuń specyfikacje (hard delete)
                    using (var cmd = new SqlCommand("DELETE FROM dbo.FarmerCalc WHERE CalcDate = @Date", connection))
                    {
                        cmd.Parameters.AddWithValue("@Date", selectedDate);
                        deletedSpecs = cmd.ExecuteNonQuery();
                    }
                }

                // Odśwież widok
                specyfikacjeData.Clear();
                UpdateStatistics();

                MessageBox.Show(
                    $"✅ Usunięto dane z dnia {selectedDate:dd.MM.yyyy}:\n\n" +
                    $"• Specyfikacji: {deletedSpecs}\n" +
                    $"• Wpisów w logu zmian: {deletedLogs}",
                    "Usunięto",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Zamknij panel admina
                adminPanel.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd usuwania danych:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateBlockingStatus()
        {
            DateTime selectedDate = dateTimePicker1.SelectedDate ?? DateTime.Today;
            int dniOdDostawy = (DateTime.Today - selectedDate).Days;
            bool isUnlocked = _odblokowaneDni.ContainsKey(selectedDate) && _odblokowaneDni[selectedDate];

            if (isUnlocked)
            {
                lblStatusBlokady.Text = $"Dzień {selectedDate:yyyy-MM-dd} - ODBLOKOWANY przez przełożonego";
                lblStatusBlokady.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"));
                btnOdblokujDzien.Visibility = Visibility.Collapsed;
                btnZablokujDzien.Visibility = Visibility.Visible;
            }
            else if (dniOdDostawy > _dniBlokady)
            {
                lblStatusBlokady.Text = $"Dzień {selectedDate:yyyy-MM-dd} - ZABLOKOWANY ({dniOdDostawy} dni temu)";
                lblStatusBlokady.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C62828"));
                btnOdblokujDzien.Visibility = Visibility.Visible;
                btnZablokujDzien.Visibility = Visibility.Collapsed;
            }
            else
            {
                lblStatusBlokady.Text = $"Dzień {selectedDate:yyyy-MM-dd} - edycja dozwolona (pozostało {_dniBlokady - dniOdDostawy} dni)";
                lblStatusBlokady.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"));
                btnOdblokujDzien.Visibility = Visibility.Collapsed;
                btnZablokujDzien.Visibility = Visibility.Visible;
            }
        }

        private bool IsEditingAllowed(DateTime calcDate)
        {
            // Sprawdź czy dzień jest odblokowany
            if (_odblokowaneDni.ContainsKey(calcDate) && _odblokowaneDni[calcDate])
                return true;

            // Sprawdź czy minęło więcej niż X dni
            int dniOdDostawy = (DateTime.Today - calcDate).Days;
            if (dniOdDostawy > _dniBlokady)
            {
                // Sprawdź czy użytkownik jest przełożonym (po UserID)
                return IsCurrentUserAdmin();
            }

            return true;
        }

        private bool CheckEditingAllowed(DateTime calcDate)
        {
            if (IsEditingAllowed(calcDate))
                return true;

            int dniOdDostawy = (DateTime.Today - calcDate).Days;
            MessageBox.Show(
                $"Edycja dnia {calcDate:yyyy-MM-dd} jest zablokowana.\n\n" +
                $"Minęło {dniOdDostawy} dni od daty dostawy (limit: {_dniBlokady} dni).\n\n" +
                "Skontaktuj się z przełożonym w celu odblokowania.",
                "Blokada edycji",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        private void EnsureSymfoniaColumnExists()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string addColumnSql = @"
                        IF COL_LENGTH('FarmerCalc', 'Symfonia') IS NULL
                        BEGIN
                            ALTER TABLE dbo.FarmerCalc ADD Symfonia BIT DEFAULT 0
                        END";
                    using (SqlCommand cmd = new SqlCommand(addColumnSql, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding Symfonia column: {ex.Message}");
            }
        }

        /// <summary>
        /// Upewnia się, że kolumna IdPosrednik istnieje w tabeli FarmerCalc
        /// </summary>
        private void EnsureIdPosrednikColumnExists()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string addColumnSql = @"
                        IF COL_LENGTH('FarmerCalc', 'IdPosrednik') IS NULL
                        BEGIN
                            ALTER TABLE dbo.FarmerCalc ADD IdPosrednik INT NULL
                        END";
                    using (SqlCommand cmd = new SqlCommand(addColumnSql, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding IdPosrednik column: {ex.Message}");
            }
        }

        /// <summary>
        /// Upewnia się, że kolumny dla zdjęć z ważenia istnieją w tabeli FarmerCalc
        /// </summary>
        private void EnsurePhotoColumnsExist()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string addColumnsSql = @"
                        IF COL_LENGTH('FarmerCalc', 'ZdjecieTaraPath') IS NULL
                        BEGIN
                            ALTER TABLE dbo.FarmerCalc ADD ZdjecieTaraPath NVARCHAR(500) NULL
                        END;
                        IF COL_LENGTH('FarmerCalc', 'ZdjecieBruttoPath') IS NULL
                        BEGIN
                            ALTER TABLE dbo.FarmerCalc ADD ZdjecieBruttoPath NVARCHAR(500) NULL
                        END";
                    using (SqlCommand cmd = new SqlCommand(addColumnsSql, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding photo columns: {ex.Message}");
            }
        }

        private void EnsureSettingsTableExists()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string createTableSql = @"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FarmerCalcSettings')
                        BEGIN
                            CREATE TABLE [dbo].[FarmerCalcSettings] (
                                [ID] INT IDENTITY(1,1) PRIMARY KEY,
                                [SettingName] NVARCHAR(100) NOT NULL,
                                [SettingValue] NVARCHAR(500) NULL,
                                [ModifiedDate] DATETIME DEFAULT GETDATE(),
                                [ModifiedBy] NVARCHAR(100) NULL
                            )
                        END;

                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FarmerCalcDayUnlock')
                        BEGIN
                            CREATE TABLE [dbo].[FarmerCalcDayUnlock] (
                                [ID] INT IDENTITY(1,1) PRIMARY KEY,
                                [CalcDate] DATE NOT NULL,
                                [IsUnlocked] BIT DEFAULT 0,
                                [UnlockedBy] NVARCHAR(100) NULL,
                                [UnlockedDate] DATETIME DEFAULT GETDATE()
                            )
                        END";

                    using (SqlCommand cmd = new SqlCommand(createTableSql, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating settings tables: {ex.Message}");
            }
        }

        private void LoadAdminSettings()
        {
            EnsureSettingsTableExists();

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Wczytaj dni blokady
                    string queryDni = "SELECT SettingValue FROM FarmerCalcSettings WHERE SettingName = 'DniBlokady'";
                    using (SqlCommand cmd = new SqlCommand(queryDni, conn))
                    {
                        var result = cmd.ExecuteScalar();
                        if (result != null && int.TryParse(result.ToString(), out int dni))
                        {
                            _dniBlokady = dni;
                            txtDniBlokady.Text = dni.ToString();
                        }
                    }

                    // Wczytaj przełożonych
                    string queryPrzel = "SELECT SettingValue FROM FarmerCalcSettings WHERE SettingName = 'Przelozeni'";
                    using (SqlCommand cmd = new SqlCommand(queryPrzel, conn))
                    {
                        var result = cmd.ExecuteScalar();
                        if (result != null && !string.IsNullOrWhiteSpace(result.ToString()))
                        {
                            txtPrzelozeni.Text = result.ToString().Replace(";", Environment.NewLine);
                            _przelozeni = result.ToString()
                                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(p => p.Trim())
                                .ToList();
                        }
                        else
                        {
                            // Domyślnie "11111" ma dostęp
                            _przelozeni = new List<string> { "11111" };
                            txtPrzelozeni.Text = "11111";
                        }

                        // Upewnij się że "11111" zawsze ma dostęp
                        if (!_przelozeni.Contains("11111"))
                        {
                            _przelozeni.Add("11111");
                        }
                    }

                    // Wczytaj odblokowane dni
                    _odblokowaneDni.Clear();
                    string queryUnlock = "SELECT CalcDate, IsUnlocked FROM FarmerCalcDayUnlock WHERE IsUnlocked = 1";
                    using (SqlCommand cmd = new SqlCommand(queryUnlock, conn))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                DateTime date = reader.GetDateTime(0);
                                _odblokowaneDni[date] = true;
                            }
                        }
                    }

                    // Wczytaj domyślną ścieżkę PDF
                    string queryPdfPath = "SELECT SettingValue FROM FarmerCalcSettings WHERE SettingName = 'DefaultPdfPath'";
                    using (SqlCommand cmd = new SqlCommand(queryPdfPath, conn))
                    {
                        var result = cmd.ExecuteScalar();
                        if (result != null && !string.IsNullOrEmpty(result.ToString()))
                        {
                            defaultPdfPath = result.ToString();
                            txtDefaultPdfPath.Text = defaultPdfPath;
                        }
                        else
                        {
                            txtDefaultPdfPath.Text = defaultPdfPath;
                        }
                    }

                    // Wczytaj domyślną ścieżkę płacht
                    string queryPlachtaPath = "SELECT SettingValue FROM FarmerCalcSettings WHERE SettingName = 'DefaultPlachtaPath'";
                    using (SqlCommand cmd = new SqlCommand(queryPlachtaPath, conn))
                    {
                        var result = cmd.ExecuteScalar();
                        if (result != null && !string.IsNullOrEmpty(result.ToString()))
                        {
                            defaultPlachtaPath = result.ToString();
                        }
                    }

                    // Wczytaj tryb kolorów PDF
                    string queryPdfColorMode = "SELECT SettingValue FROM FarmerCalcSettings WHERE SettingName = 'PdfCzarnoBialy'";
                    using (SqlCommand cmd = new SqlCommand(queryPdfColorMode, conn))
                    {
                        var result = cmd.ExecuteScalar();
                        if (result != null && bool.TryParse(result.ToString(), out bool czarnoBialy))
                        {
                            _pdfCzarnoBialy = czarnoBialy;
                            rbPdfCzarnoBialy.IsChecked = czarnoBialy;
                            rbPdfKolorowy.IsChecked = !czarnoBialy;
                        }
                        else
                        {
                            rbPdfKolorowy.IsChecked = true;
                            rbPdfCzarnoBialy.IsChecked = false;
                        }
                    }

                    // Wczytaj opcję drukowania terminu płatności
                    string queryDrukujTermin = "SELECT SettingValue FROM FarmerCalcSettings WHERE SettingName = 'DrukujTerminPlatnosci'";
                    using (SqlCommand cmd = new SqlCommand(queryDrukujTermin, conn))
                    {
                        var result = cmd.ExecuteScalar();
                        if (result != null && bool.TryParse(result.ToString(), out bool drukujTermin))
                        {
                            _drukujTerminPlatnosci = drukujTermin;
                            chkDrukujTerminPlatnosci.IsChecked = drukujTermin;
                        }
                        else
                        {
                            _drukujTerminPlatnosci = false;
                            chkDrukujTerminPlatnosci.IsChecked = false;
                        }
                    }

                    // Wczytaj ścieżkę Płachta - Dla Lekarzy (do UI)
                    var txtPlachta = this.FindName("txtDefaultPlachtaPath") as TextBox;
                    if (txtPlachta != null)
                    {
                        txtPlachta.Text = defaultPlachtaPath ?? "";
                    }

                    // Wczytaj ścieżkę Płachta - Zapisz PDF
                    string queryPlachtaSavePath = "SELECT SettingValue FROM FarmerCalcSettings WHERE SettingName = 'DefaultPlachtaSavePath'";
                    using (SqlCommand cmd = new SqlCommand(queryPlachtaSavePath, conn))
                    {
                        var result = cmd.ExecuteScalar();
                        if (result != null && !string.IsNullOrEmpty(result.ToString()))
                        {
                            _defaultPlachtaSavePath = result.ToString();
                            var txtPlachtaSave = this.FindName("txtDefaultPlachtaSavePath") as TextBox;
                            if (txtPlachtaSave != null) txtPlachtaSave.Text = _defaultPlachtaSavePath;
                        }
                    }

                    // Wczytaj ścieżkę Podsumowanie PDF
                    string queryPodsPath = "SELECT SettingValue FROM FarmerCalcSettings WHERE SettingName = 'DefaultPodsumowaniePath'";
                    using (SqlCommand cmd = new SqlCommand(queryPodsPath, conn))
                    {
                        var result = cmd.ExecuteScalar();
                        if (result != null && !string.IsNullOrEmpty(result.ToString()))
                        {
                            _defaultPodsumowaniePath = result.ToString();
                            var txtPods = this.FindName("txtDefaultPodsumowaniePath") as TextBox;
                            if (txtPods != null) txtPods.Text = _defaultPodsumowaniePath;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading admin settings: {ex.Message}");
            }
        }

        private void SaveAdminSettings()
        {
            EnsureSettingsTableExists();

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string user = Environment.UserName;

                    // Zapisz dni blokady
                    string mergeDni = @"
                        MERGE FarmerCalcSettings AS target
                        USING (SELECT 'DniBlokady' AS SettingName) AS source
                        ON target.SettingName = source.SettingName
                        WHEN MATCHED THEN UPDATE SET SettingValue = @Value, ModifiedDate = GETDATE(), ModifiedBy = @User
                        WHEN NOT MATCHED THEN INSERT (SettingName, SettingValue, ModifiedBy) VALUES ('DniBlokady', @Value, @User);";

                    using (SqlCommand cmd = new SqlCommand(mergeDni, conn))
                    {
                        cmd.Parameters.AddWithValue("@Value", _dniBlokady.ToString());
                        cmd.Parameters.AddWithValue("@User", user);
                        cmd.ExecuteNonQuery();
                    }

                    // Zapisz przełożonych (rozdzielonych średnikiem)
                    string przelozeniStr = string.Join(";", _przelozeni);
                    string mergePrzel = @"
                        MERGE FarmerCalcSettings AS target
                        USING (SELECT 'Przelozeni' AS SettingName) AS source
                        ON target.SettingName = source.SettingName
                        WHEN MATCHED THEN UPDATE SET SettingValue = @Value, ModifiedDate = GETDATE(), ModifiedBy = @User
                        WHEN NOT MATCHED THEN INSERT (SettingName, SettingValue, ModifiedBy) VALUES ('Przelozeni', @Value, @User);";

                    using (SqlCommand cmd = new SqlCommand(mergePrzel, conn))
                    {
                        cmd.Parameters.AddWithValue("@Value", przelozeniStr);
                        cmd.Parameters.AddWithValue("@User", user);
                        cmd.ExecuteNonQuery();
                    }

                    // Zapisz domyślną ścieżkę PDF
                    string mergePdfPath = @"
                        MERGE FarmerCalcSettings AS target
                        USING (SELECT 'DefaultPdfPath' AS SettingName) AS source
                        ON target.SettingName = source.SettingName
                        WHEN MATCHED THEN UPDATE SET SettingValue = @Value, ModifiedDate = GETDATE(), ModifiedBy = @User
                        WHEN NOT MATCHED THEN INSERT (SettingName, SettingValue, ModifiedBy) VALUES ('DefaultPdfPath', @Value, @User);";

                    using (SqlCommand cmd = new SqlCommand(mergePdfPath, conn))
                    {
                        cmd.Parameters.AddWithValue("@Value", defaultPdfPath);
                        cmd.Parameters.AddWithValue("@User", user);
                        cmd.ExecuteNonQuery();
                    }

                    // Zapisz tryb kolorów PDF
                    string mergePdfColorMode = @"
                        MERGE FarmerCalcSettings AS target
                        USING (SELECT 'PdfCzarnoBialy' AS SettingName) AS source
                        ON target.SettingName = source.SettingName
                        WHEN MATCHED THEN UPDATE SET SettingValue = @Value, ModifiedDate = GETDATE(), ModifiedBy = @User
                        WHEN NOT MATCHED THEN INSERT (SettingName, SettingValue, ModifiedBy) VALUES ('PdfCzarnoBialy', @Value, @User);";

                    using (SqlCommand cmd = new SqlCommand(mergePdfColorMode, conn))
                    {
                        cmd.Parameters.AddWithValue("@Value", _pdfCzarnoBialy.ToString());
                        cmd.Parameters.AddWithValue("@User", user);
                        cmd.ExecuteNonQuery();
                    }

                    // Zapisz opcję drukowania terminu płatności
                    string mergeDrukujTermin = @"
                        MERGE FarmerCalcSettings AS target
                        USING (SELECT 'DrukujTerminPlatnosci' AS SettingName) AS source
                        ON target.SettingName = source.SettingName
                        WHEN MATCHED THEN UPDATE SET SettingValue = @Value, ModifiedDate = GETDATE(), ModifiedBy = @User
                        WHEN NOT MATCHED THEN INSERT (SettingName, SettingValue, ModifiedBy) VALUES ('DrukujTerminPlatnosci', @Value, @User);";

                    using (SqlCommand cmd = new SqlCommand(mergeDrukujTermin, conn))
                    {
                        cmd.Parameters.AddWithValue("@Value", _drukujTerminPlatnosci.ToString());
                        cmd.Parameters.AddWithValue("@User", user);
                        cmd.ExecuteNonQuery();
                    }

                    // Pobierz wartości z UI przez FindName
                    var txtPlachta = this.FindName("txtDefaultPlachtaPath") as TextBox;
                    var txtPlachtaSave = this.FindName("txtDefaultPlachtaSavePath") as TextBox;
                    var txtPods = this.FindName("txtDefaultPodsumowaniePath") as TextBox;

                    // Zapisz ścieżkę Płachta (Dla Lekarzy)
                    string plachtaPath = txtPlachta?.Text?.Trim() ?? defaultPlachtaPath;
                    if (!string.IsNullOrWhiteSpace(plachtaPath))
                    {
                        defaultPlachtaPath = plachtaPath;
                        string mergePlachtaPath = @"
                            MERGE FarmerCalcSettings AS target
                            USING (SELECT 'DefaultPlachtaPath' AS SettingName) AS source
                            ON target.SettingName = source.SettingName
                            WHEN MATCHED THEN UPDATE SET SettingValue = @Value, ModifiedDate = GETDATE(), ModifiedBy = @User
                            WHEN NOT MATCHED THEN INSERT (SettingName, SettingValue, ModifiedBy) VALUES ('DefaultPlachtaPath', @Value, @User);";

                        using (SqlCommand cmd = new SqlCommand(mergePlachtaPath, conn))
                        {
                            cmd.Parameters.AddWithValue("@Value", plachtaPath);
                            cmd.Parameters.AddWithValue("@User", user);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // Zapisz ścieżkę Płachta (Zapisz PDF)
                    string plachtaSavePath = txtPlachtaSave?.Text?.Trim() ?? _defaultPlachtaSavePath;
                    if (!string.IsNullOrWhiteSpace(plachtaSavePath))
                    {
                        _defaultPlachtaSavePath = plachtaSavePath;
                        string mergePlachtaSavePath = @"
                            MERGE FarmerCalcSettings AS target
                            USING (SELECT 'DefaultPlachtaSavePath' AS SettingName) AS source
                            ON target.SettingName = source.SettingName
                            WHEN MATCHED THEN UPDATE SET SettingValue = @Value, ModifiedDate = GETDATE(), ModifiedBy = @User
                            WHEN NOT MATCHED THEN INSERT (SettingName, SettingValue, ModifiedBy) VALUES ('DefaultPlachtaSavePath', @Value, @User);";

                        using (SqlCommand cmd = new SqlCommand(mergePlachtaSavePath, conn))
                        {
                            cmd.Parameters.AddWithValue("@Value", plachtaSavePath);
                            cmd.Parameters.AddWithValue("@User", user);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // Zapisz ścieżkę Podsumowanie
                    string podsPath = txtPods?.Text?.Trim() ?? _defaultPodsumowaniePath;
                    if (!string.IsNullOrWhiteSpace(podsPath))
                    {
                        _defaultPodsumowaniePath = podsPath;
                        string mergePodsPath = @"
                            MERGE FarmerCalcSettings AS target
                            USING (SELECT 'DefaultPodsumowaniePath' AS SettingName) AS source
                            ON target.SettingName = source.SettingName
                            WHEN MATCHED THEN UPDATE SET SettingValue = @Value, ModifiedDate = GETDATE(), ModifiedBy = @User
                            WHEN NOT MATCHED THEN INSERT (SettingName, SettingValue, ModifiedBy) VALUES ('DefaultPodsumowaniePath', @Value, @User);";

                        using (SqlCommand cmd = new SqlCommand(mergePodsPath, conn))
                        {
                            cmd.Parameters.AddWithValue("@Value", podsPath);
                            cmd.Parameters.AddWithValue("@User", user);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu do bazy: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Pobiera ścieżkę do podsumowań z bazy danych
        /// </summary>
        private string GetPodsumowaniePathFromDb()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT SettingValue FROM FarmerCalcSettings WHERE SettingName = 'DefaultPodsumowaniePath'";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        var result = cmd.ExecuteScalar();
                        if (result != null && !string.IsNullOrEmpty(result.ToString()))
                        {
                            return result.ToString();
                        }
                    }
                }
            }
            catch { }
            return _defaultPodsumowaniePath;
        }

        private void SaveDayUnlockStatus(DateTime date, bool isUnlocked)
        {
            EnsureSettingsTableExists();

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string user = Environment.UserName;

                    string mergeSql = @"
                        MERGE FarmerCalcDayUnlock AS target
                        USING (SELECT @CalcDate AS CalcDate) AS source
                        ON target.CalcDate = source.CalcDate
                        WHEN MATCHED THEN UPDATE SET IsUnlocked = @IsUnlocked, UnlockedBy = @User, UnlockedDate = GETDATE()
                        WHEN NOT MATCHED THEN INSERT (CalcDate, IsUnlocked, UnlockedBy) VALUES (@CalcDate, @IsUnlocked, @User);";

                    using (SqlCommand cmd = new SqlCommand(mergeSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@CalcDate", date.Date);
                        cmd.Parameters.AddWithValue("@IsUnlocked", isUnlocked);
                        cmd.Parameters.AddWithValue("@User", user);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving day unlock status: {ex.Message}");
            }
        }

        #endregion

        #region Karta Podsumowanie

        /// <summary>
        /// Odświeża dane na karcie Podsumowanie
        /// </summary>
        private void BtnPodsumowanieRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadPodsumowanieData();
        }

        /// <summary>
        /// Ładuje dane do karty Podsumowanie na podstawie aktualnych specyfikacji
        /// </summary>
        private void LoadPodsumowanieData()
        {
            var dataGrid = FindName("dataGridPodsumowanie") as DataGrid;
            var lblData = FindName("lblPodsumowanieData") as TextBlock;

            if (specyfikacjeData == null || specyfikacjeData.Count == 0)
            {
                podsumowanieData.Clear();
                if (dataGrid != null) dataGrid.ItemsSource = podsumowanieData;
                UpdatePodsumowanieLabels();
                return;
            }

            DateTime selectedDate = dateTimePicker1.SelectedDate ?? DateTime.Today;
            if (lblData != null) lblData.Text = selectedDate.ToString("dd.MM.yyyy");

            podsumowanieData.Clear();
            int lp = 1;

            foreach (var spec in specyfikacjeData.OrderBy(s => s.Nr))
            {
                // Oblicz DOKŁADNĄ średnią wagę (niezaokrągloną) - tak jak w specyfikacji PDF
                // Wzór: Śr.waga = Netto / Dostarcz.(ARIMR) = Netto / (LUMEL + Padłe)
                decimal dokladnaSrWaga = (spec.LUMEL + spec.Padle) > 0
                    ? spec.WagaNettoDoRozliczenia / (spec.LUMEL + spec.Padle)
                    : 0;

                // KgKonf i KgPadle pokazujemy ZAWSZE (niezależnie od PiK) - do celów informacyjnych
                // Wzór z PDF: Konf[kg] = Konf[szt] × Śr.waga (zaokrąglone do pełnych kg)
                int kgKonf = (int)Math.Round((spec.CH + spec.NW + spec.ZM) * dokladnaSrWaga, 0);
                int kgPadle = (int)Math.Round(spec.Padle * dokladnaSrWaga, 0);

                // Średnia waga wyświetlana (zaokrąglona do 2 miejsc po przecinku)
                decimal srWagaDisplay = Math.Round(dokladnaSrWaga, 2);

                var row = new PodsumowanieRow
                {
                    LP = lp++,
                    HodowcaDrobiu = spec.Dostawca,  // CustomerGID
                    Odbiorca = spec.Odbiorca ?? "-",
                    SztukiZadeklarowane = spec.SztukiDek,
                    SztukiPadle = spec.Padle,
                    SztukiKonfi = spec.CH + spec.NW + spec.ZM,
                    KgKonf = kgKonf,    // Pokazujemy ZAWSZE (informacyjnie)
                    KgPadle = kgPadle,  // Pokazujemy ZAWSZE (informacyjnie)
                    Lumel = spec.LUMEL,
                    SztukiKonfiskataT = spec.CH + spec.NW + spec.ZM,
                    // Wzór ze specyfikacji PDF: 
                    // Dostarcz.(ARIMR) = LUMEL + Padłe
                    // Zdatne = Dostarcz - Padłe - Konf = LUMEL - Konf (Padłe się kasuje!)
                    SztukiZdatne = spec.LUMEL - (spec.CH + spec.NW + spec.ZM),
                    IloscKgZywiec = spec.DoZaplaty,  // DoZaplaty uwzględnia PiK w swoim obliczeniu
                    SredniaWagaPrzedUbojem = srWagaDisplay,
                    SztukiProdukcjaTuszka = spec.SztukiWybijak,
                    WagaProdukcjaTuszka = spec.KilogramyWybijak,
                    Wprowadzil = GetWprowadzilNazwa(spec.ID),
                    Zatwierdził = GetZatwierdzilNazwa(spec.ID)
                };

                // Oblicz wydajność %: (Waga Produkcja / Kg żywiec) * 100
                if (spec.DoZaplaty > 0)
                {
                    row.WydajnoscProcent = (spec.KilogramyWybijak / spec.DoZaplaty) * 100;
                }

                podsumowanieData.Add(row);
            }

            // Dodaj wiersz sumy na koncu
            if (podsumowanieData.Count > 0)
            {
                decimal sumaKgZywiec = podsumowanieData.Sum(r => r.IloscKgZywiec);
                decimal sumaWagaProd = podsumowanieData.Sum(r => r.WagaProdukcjaTuszka);
                decimal avgWydajnosc = sumaKgZywiec > 0 ? (sumaWagaProd / sumaKgZywiec) * 100 : 0;

                var sumRow = new PodsumowanieRow
                {
                    LP = 0,
                    HodowcaDrobiu = "SUMA",
                    SztukiZadeklarowane = podsumowanieData.Sum(r => r.SztukiZadeklarowane),
                    SztukiPadle = podsumowanieData.Sum(r => r.SztukiPadle),
                    SztukiKonfi = podsumowanieData.Sum(r => r.SztukiKonfi),
                    KgKonf = podsumowanieData.Sum(r => r.KgKonf),
                    KgPadle = podsumowanieData.Sum(r => r.KgPadle),
                    WydajnoscProcent = avgWydajnosc,
                    Lumel = podsumowanieData.Sum(r => r.Lumel),
                    SztukiKonfiskataT = podsumowanieData.Sum(r => r.SztukiKonfiskataT),
                    SztukiZdatne = podsumowanieData.Sum(r => r.SztukiZdatne),
                    IloscKgZywiec = sumaKgZywiec,
                    SredniaWagaPrzedUbojem = podsumowanieData.Average(r => r.SredniaWagaPrzedUbojem),
                    SztukiProdukcjaTuszka = podsumowanieData.Sum(r => r.SztukiProdukcjaTuszka),
                    WagaProdukcjaTuszka = sumaWagaProd,
                    IsSumRow = true
                };
                podsumowanieData.Add(sumRow);
            }

            if (dataGrid != null) dataGrid.ItemsSource = podsumowanieData;
            UpdatePodsumowanieLabels();
        }

        /// <summary>
        /// Aktualizuje etykiety podsumowania w stopce karty
        /// </summary>
        private void UpdatePodsumowanieLabels()
        {
            var lblSumaWierszy = FindName("lblPodsumowanieSumaWierszy") as TextBlock;
            var lblSumaZadekl = FindName("lblPodsumowanieSumaZadekl") as TextBlock;
            var lblSumaZdatnych = FindName("lblPodsumowanieSumaZdatnych") as TextBlock;
            var lblSumaKgZywiec = FindName("lblPodsumowanieSumaKgZywiec") as TextBlock;
            var lblSrWydajnosc = FindName("lblPodsumowanieSrWydajnosc") as TextBlock;

            if (lblSumaWierszy != null) lblSumaWierszy.Text = podsumowanieData.Count.ToString();
            if (lblSumaZadekl != null) lblSumaZadekl.Text = podsumowanieData.Sum(r => r.SztukiZadeklarowane).ToString("N0");
            if (lblSumaZdatnych != null) lblSumaZdatnych.Text = podsumowanieData.Sum(r => r.SztukiZdatne).ToString("N0");
            if (lblSumaKgZywiec != null) lblSumaKgZywiec.Text = podsumowanieData.Sum(r => r.IloscKgZywiec).ToString("N0");

            if (podsumowanieData.Count > 0)
            {
                decimal srWydajnosc = podsumowanieData.Average(r => r.WydajnoscProcent);
                if (lblSrWydajnosc != null) lblSrWydajnosc.Text = $"{srWydajnosc:F2}%";
            }
            else
            {
                if (lblSrWydajnosc != null) lblSrWydajnosc.Text = "0%";
            }
        }

        /// <summary>
        /// Obsługuje zmianę checkboxa grupowania według odbiorcy w Podsumowaniu
        /// </summary>
        private void ChkGroupByOdbiorcaPodsumowanie_Changed(object sender, RoutedEventArgs e)
        {
            var checkbox = sender as CheckBox;
            bool groupByOdbiorca = checkbox?.IsChecked == true;

            if (podsumowanieData == null || podsumowanieData.Count == 0) return;

            var dataGrid = FindName("dataGridPodsumowanie") as DataGrid;

            if (groupByOdbiorca)
            {
                // Grupuj według nazwy hodowcy (odbiorca)
                var grouped = podsumowanieData
                    .OrderBy(x => x.HodowcaDrobiu)
                    .ThenBy(x => x.LP)
                    .ToList();

                podsumowanieData.Clear();
                string lastHodowca = "";
                int newLp = 1;

                foreach (var item in grouped)
                {
                    // Dodaj separator przy zmianie grupy (poprzez oznaczenie tła)
                    item.IsGroupStart = (item.HodowcaDrobiu != lastHodowca);
                    lastHodowca = item.HodowcaDrobiu;
                    item.LP = newLp++;
                    podsumowanieData.Add(item);
                }

                UpdateStatus("Podsumowanie pogrupowane według odbiorcy");
            }
            else
            {
                // Sortuj według LP oryginalnego
                LoadPodsumowanieData(); // Przeładuj dane aby zresetować sortowanie
                UpdateStatus("Podsumowanie posortowane według LP");
            }

            if (dataGrid != null) dataGrid.Items.Refresh();
        }

        /// <summary>
        /// Eksportuje dane z karty Podsumowanie do PDF
        /// </summary>
        private void BtnPodsumowanieExportPdf_Click(object sender, RoutedEventArgs e)
        {
            if (podsumowanieData.Count == 0)
            {
                MessageBox.Show("Brak danych do eksportu!", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                DateTime selectedDate = dateTimePicker1.SelectedDate ?? DateTime.Today;
                string yearFolder = selectedDate.Year.ToString();
                string monthFolder = selectedDate.Month.ToString("D2");
                string dayFolder = selectedDate.Day.ToString("D2");

                string folderPath = Path.Combine(defaultPdfPath, yearFolder, monthFolder, dayFolder);
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                string fileName = $"Podsumowanie_{selectedDate:yyyy-MM-dd}_{DateTime.Now:HHmmss}.pdf";
                string filePath = Path.Combine(folderPath, fileName);

                using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    // A4 poziomo z minimalnymi marginesami (na całą kartkę)
                    Document doc = new Document(PageSize.A4.Rotate(), 10, 10, 15, 15);
                    PdfWriter writer = PdfWriter.GetInstance(doc, fs);
                    doc.Open();

                    // Czcionki
                    string arialPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                    BaseFont bfArial;
                    if (File.Exists(arialPath))
                    {
                        bfArial = BaseFont.CreateFont(arialPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                    }
                    else
                    {
                        bfArial = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1250, BaseFont.NOT_EMBEDDED);
                    }

                    // Mniejsze czcionki dla lepszego dopasowania na stronie
                    iTextSharp.text.Font fontTitle = new iTextSharp.text.Font(bfArial, 14, iTextSharp.text.Font.BOLD);
                    iTextSharp.text.Font fontSubtitle = new iTextSharp.text.Font(bfArial, 10);
                    iTextSharp.text.Font fontHeader = new iTextSharp.text.Font(bfArial, 6, iTextSharp.text.Font.BOLD);
                    iTextSharp.text.Font fontData = new iTextSharp.text.Font(bfArial, 7);
                    iTextSharp.text.Font fontSum = new iTextSharp.text.Font(bfArial, 8, iTextSharp.text.Font.BOLD);
                    iTextSharp.text.Font fontFooter = new iTextSharp.text.Font(bfArial, 8);

                    // Dzień tygodnia
                    string[] dniTygodnia = { "Niedziela", "Poniedziałek", "Wtorek", "Środa", "Czwartek", "Piątek", "Sobota" };
                    string dzienTygodnia = dniTygodnia[(int)selectedDate.DayOfWeek];

                    // Pobierz imię i nazwisko drukującego
                    NazwaZiD nazwaZiDPods = new NazwaZiD();
                    string drukujacy = nazwaZiDPods.GetNameById(App.UserID) ?? App.UserID ?? Environment.UserName;

                    // Data wydruku + kto drukuje na górze po prawej (odsunięte od marginesu)
                    Paragraph dateprint = new Paragraph($"Data wydruku: {DateTime.Now:dd.MM.yyyy HH:mm}\nWydrukował: {drukujacy}", fontFooter);
                    dateprint.Alignment = Element.ALIGN_RIGHT;
                    dateprint.IndentationRight = 15f;  // Odsunięcie od prawego marginesu
                    dateprint.SpacingAfter = 5;
                    doc.Add(dateprint);

                    // Tytuł
                    Paragraph title = new Paragraph("RAPORT Z PRZYJĘCIA ŻYWCA DO UBOJU", fontTitle);
                    title.Alignment = Element.ALIGN_CENTER;
                    doc.Add(title);

                    // Data uboju z dniem tygodnia
                    Paragraph subtitle = new Paragraph($"Data uboju: {selectedDate:dd.MM.yyyy} ({dzienTygodnia})", fontSubtitle);
                    subtitle.Alignment = Element.ALIGN_CENTER;
                    subtitle.SpacingAfter = 10;
                    doc.Add(subtitle);

                    // Tabela z czarnymi obramowaniami (17 kolumn) - trochę węższa
                    PdfPTable table = new PdfPTable(17);
                    table.WidthPercentage = 96;
                    // Zmniejszone kolumny kg (Konfi[kg], Padłe[kg], Suma[kg])
                    float[] widths = { 3f, 11f, 5f, 4f, 4f, 4f, 3.5f, 3.5f, 3.5f, 5f, 5f, 4f, 5f, 5.5f, 5f, 5f, 5.5f };
                    table.SetWidths(widths);

                    // Ustawienie domyślnych obramowań dla tabeli
                    table.DefaultCell.BorderWidth = 1;
                    table.DefaultCell.BorderColor = BaseColor.BLACK;

                    // Nagłówki z jednostkami w osobnych liniach
                    string[] headers = { "L.P", "Hodowca\nDrobiu", "Zadekl.\n[szt]", "Padłe\n[szt]", "Konfi\n[szt]", "Suma\n[szt]", "Konfi\n[kg]", "Padłe\n[kg]", "Suma\n[kg]", "Wydaj.\n[%]", "Lumel\n[szt]", "KonT\n[szt]", "Zdatne\n[szt]", "Żywiec\n[kg]", "ŚrWaga\n[kg]", "Prod.\n[szt]", "Prod.\n[kg]" };
                    BaseColor headerColor = new BaseColor(60, 60, 60); // Ciemnoszary

                    foreach (var h in headers)
                    {
                        PdfPCell cell = new PdfPCell(new Phrase(h, fontHeader));
                        cell.BackgroundColor = headerColor;
                        cell.HorizontalAlignment = Element.ALIGN_CENTER;
                        cell.VerticalAlignment = Element.ALIGN_MIDDLE;
                        cell.Padding = 5;
                        cell.BorderWidth = 1;
                        cell.BorderColor = BaseColor.BLACK;
                        cell.Phrase.Font.Color = BaseColor.WHITE;
                        table.AddCell(cell);
                    }

                    // Dane - wyższe wiersze (pomijamy wiersz SUMA który jest na końcu)
                    float cellPadding = 8f; // Większy padding dla wyższych wierszy
                    foreach (var row in podsumowanieData.Where(r => !r.IsSumRow && r.HodowcaDrobiu != "SUMA"))
                    {
                        table.AddCell(new PdfPCell(new Phrase(row.LP.ToString(), fontData)) { HorizontalAlignment = Element.ALIGN_CENTER, VerticalAlignment = Element.ALIGN_MIDDLE, Padding = cellPadding });
                        table.AddCell(new PdfPCell(new Phrase(row.HodowcaDrobiu ?? "-", fontData)) { VerticalAlignment = Element.ALIGN_MIDDLE, Padding = cellPadding });
                        table.AddCell(new PdfPCell(new Phrase(row.SztukiZadeklarowane.ToString("N0"), fontData)) { HorizontalAlignment = Element.ALIGN_RIGHT, VerticalAlignment = Element.ALIGN_MIDDLE, Padding = cellPadding, BackgroundColor = new BaseColor(255, 224, 178) });
                        table.AddCell(new PdfPCell(new Phrase(row.SztukiPadle.ToString("N0"), fontData)) { HorizontalAlignment = Element.ALIGN_RIGHT, VerticalAlignment = Element.ALIGN_MIDDLE, Padding = cellPadding });
                        table.AddCell(new PdfPCell(new Phrase(row.SztukiKonfi.ToString("N0"), fontData)) { HorizontalAlignment = Element.ALIGN_RIGHT, VerticalAlignment = Element.ALIGN_MIDDLE, Padding = cellPadding });
                        table.AddCell(new PdfPCell(new Phrase(row.SztukiSuma.ToString("N0"), fontData)) { HorizontalAlignment = Element.ALIGN_RIGHT, VerticalAlignment = Element.ALIGN_MIDDLE, Padding = cellPadding });
                        table.AddCell(new PdfPCell(new Phrase(row.KgKonf.ToString("N0"), fontData)) { HorizontalAlignment = Element.ALIGN_RIGHT, VerticalAlignment = Element.ALIGN_MIDDLE, Padding = cellPadding });
                        table.AddCell(new PdfPCell(new Phrase(row.KgPadle.ToString("N0"), fontData)) { HorizontalAlignment = Element.ALIGN_RIGHT, VerticalAlignment = Element.ALIGN_MIDDLE, Padding = cellPadding });
                        table.AddCell(new PdfPCell(new Phrase(row.KgSuma.ToString("N0"), fontData)) { HorizontalAlignment = Element.ALIGN_RIGHT, VerticalAlignment = Element.ALIGN_MIDDLE, Padding = cellPadding });

                        // Wydajność z kolorem - skala 3-kolorowa jak w Excelu (czerwony-żółty-zielony, 74-75-79%)
                        BaseColor wydColor = GetWydajnoscPdfColor(row.WydajnoscProcent);
                        var wydCell = new PdfPCell(new Phrase($"{row.WydajnoscProcent:F2}%", fontData));
                        wydCell.HorizontalAlignment = Element.ALIGN_CENTER;
                        wydCell.VerticalAlignment = Element.ALIGN_MIDDLE;
                        wydCell.Padding = cellPadding;
                        wydCell.BackgroundColor = wydColor;
                        wydCell.BorderWidth = 1;
                        wydCell.BorderColor = BaseColor.BLACK;
                        table.AddCell(wydCell);

                        table.AddCell(new PdfPCell(new Phrase(row.Lumel.ToString("N0"), fontData)) { HorizontalAlignment = Element.ALIGN_RIGHT, VerticalAlignment = Element.ALIGN_MIDDLE, Padding = cellPadding });
                        table.AddCell(new PdfPCell(new Phrase(row.SztukiKonfiskataT.ToString("N0"), fontData)) { HorizontalAlignment = Element.ALIGN_RIGHT, VerticalAlignment = Element.ALIGN_MIDDLE, Padding = cellPadding });
                        table.AddCell(new PdfPCell(new Phrase(row.SztukiZdatne.ToString("N0"), fontData)) { HorizontalAlignment = Element.ALIGN_RIGHT, VerticalAlignment = Element.ALIGN_MIDDLE, Padding = cellPadding, BackgroundColor = new BaseColor(209, 250, 229) });
                        table.AddCell(new PdfPCell(new Phrase(row.IloscKgZywiec.ToString("N0"), fontData)) { HorizontalAlignment = Element.ALIGN_RIGHT, VerticalAlignment = Element.ALIGN_MIDDLE, Padding = cellPadding });
                        table.AddCell(new PdfPCell(new Phrase(row.SredniaWagaPrzedUbojem.ToString("F2"), fontData)) { HorizontalAlignment = Element.ALIGN_RIGHT, VerticalAlignment = Element.ALIGN_MIDDLE, Padding = cellPadding });
                        table.AddCell(new PdfPCell(new Phrase(row.SztukiProdukcjaTuszka.ToString("N0"), fontData)) { HorizontalAlignment = Element.ALIGN_RIGHT, VerticalAlignment = Element.ALIGN_MIDDLE, Padding = cellPadding });
                        table.AddCell(new PdfPCell(new Phrase(row.WagaProdukcjaTuszka.ToString("N0"), fontData)) { HorizontalAlignment = Element.ALIGN_RIGHT, VerticalAlignment = Element.ALIGN_MIDDLE, Padding = cellPadding });
                    }

                    // Wiersz sumy - pobierz z istniejącego wiersza SUMA (LP=0) zamiast liczyć ponownie
                    var sumRowData = podsumowanieData.FirstOrDefault(r => r.IsSumRow || r.HodowcaDrobiu == "SUMA");
                    if (sumRowData != null)
                    {
                        float sumPadding = 4f;
                        BaseColor sumColor = new BaseColor(200, 200, 200);
                        PdfPCell sumaCell = new PdfPCell(new Phrase("SUMA:", fontSum));
                        sumaCell.Colspan = 2;
                        sumaCell.BackgroundColor = sumColor;
                        sumaCell.HorizontalAlignment = Element.ALIGN_RIGHT;
                        sumaCell.VerticalAlignment = Element.ALIGN_MIDDLE;
                        sumaCell.Padding = sumPadding;
                        table.AddCell(sumaCell);

                        table.AddCell(new PdfPCell(new Phrase(sumRowData.SztukiZadeklarowane.ToString("N0"), fontSum)) { BackgroundColor = sumColor, HorizontalAlignment = Element.ALIGN_RIGHT, VerticalAlignment = Element.ALIGN_MIDDLE, Padding = sumPadding });
                        table.AddCell(new PdfPCell(new Phrase(sumRowData.SztukiPadle.ToString("N0"), fontSum)) { BackgroundColor = sumColor, HorizontalAlignment = Element.ALIGN_RIGHT, VerticalAlignment = Element.ALIGN_MIDDLE, Padding = sumPadding });
                        table.AddCell(new PdfPCell(new Phrase(sumRowData.SztukiKonfi.ToString("N0"), fontSum)) { BackgroundColor = sumColor, HorizontalAlignment = Element.ALIGN_RIGHT, VerticalAlignment = Element.ALIGN_MIDDLE, Padding = sumPadding });
                        table.AddCell(new PdfPCell(new Phrase(sumRowData.SztukiSuma.ToString("N0"), fontSum)) { BackgroundColor = sumColor, HorizontalAlignment = Element.ALIGN_RIGHT, VerticalAlignment = Element.ALIGN_MIDDLE, Padding = sumPadding });
                        table.AddCell(new PdfPCell(new Phrase(sumRowData.KgKonf.ToString("N0"), fontSum)) { BackgroundColor = sumColor, HorizontalAlignment = Element.ALIGN_RIGHT, VerticalAlignment = Element.ALIGN_MIDDLE, Padding = sumPadding });
                        table.AddCell(new PdfPCell(new Phrase(sumRowData.KgPadle.ToString("N0"), fontSum)) { BackgroundColor = sumColor, HorizontalAlignment = Element.ALIGN_RIGHT, VerticalAlignment = Element.ALIGN_MIDDLE, Padding = sumPadding });
                        table.AddCell(new PdfPCell(new Phrase(sumRowData.KgSuma.ToString("N0"), fontSum)) { BackgroundColor = sumColor, HorizontalAlignment = Element.ALIGN_RIGHT, VerticalAlignment = Element.ALIGN_MIDDLE, Padding = sumPadding });
                        table.AddCell(new PdfPCell(new Phrase($"{sumRowData.WydajnoscProcent:F2}%", fontSum)) { BackgroundColor = sumColor, HorizontalAlignment = Element.ALIGN_CENTER, VerticalAlignment = Element.ALIGN_MIDDLE, Padding = sumPadding });
                        table.AddCell(new PdfPCell(new Phrase(sumRowData.Lumel.ToString("N0"), fontSum)) { BackgroundColor = sumColor, HorizontalAlignment = Element.ALIGN_RIGHT, VerticalAlignment = Element.ALIGN_MIDDLE, Padding = sumPadding });
                        table.AddCell(new PdfPCell(new Phrase(sumRowData.SztukiKonfiskataT.ToString("N0"), fontSum)) { BackgroundColor = sumColor, HorizontalAlignment = Element.ALIGN_RIGHT, VerticalAlignment = Element.ALIGN_MIDDLE, Padding = sumPadding });
                        table.AddCell(new PdfPCell(new Phrase(sumRowData.SztukiZdatne.ToString("N0"), fontSum)) { BackgroundColor = sumColor, HorizontalAlignment = Element.ALIGN_RIGHT, VerticalAlignment = Element.ALIGN_MIDDLE, Padding = sumPadding });
                        table.AddCell(new PdfPCell(new Phrase(sumRowData.IloscKgZywiec.ToString("N0"), fontSum)) { BackgroundColor = sumColor, HorizontalAlignment = Element.ALIGN_RIGHT, VerticalAlignment = Element.ALIGN_MIDDLE, Padding = sumPadding });
                        table.AddCell(new PdfPCell(new Phrase($"{sumRowData.SredniaWagaPrzedUbojem:F2}", fontSum)) { BackgroundColor = sumColor, HorizontalAlignment = Element.ALIGN_RIGHT, VerticalAlignment = Element.ALIGN_MIDDLE, Padding = sumPadding });
                        table.AddCell(new PdfPCell(new Phrase(sumRowData.SztukiProdukcjaTuszka.ToString("N0"), fontSum)) { BackgroundColor = sumColor, HorizontalAlignment = Element.ALIGN_RIGHT, VerticalAlignment = Element.ALIGN_MIDDLE, Padding = sumPadding });
                        table.AddCell(new PdfPCell(new Phrase(sumRowData.WagaProdukcjaTuszka.ToString("N0"), fontSum)) { BackgroundColor = sumColor, HorizontalAlignment = Element.ALIGN_RIGHT, VerticalAlignment = Element.ALIGN_MIDDLE, Padding = sumPadding });
                    }

                    doc.Add(table);

                    // === SEKCJA STATYSTYK WPROWADZENIA/WERYFIKACJI - tabela 2 kolumny ===
                    var (wprowadzenia, weryfikacje, total) = GetZatwierdzeniaStats(selectedDate);

                    if (total > 0 && (wprowadzenia.Count > 0 || weryfikacje.Count > 0))
                    {
                        doc.Add(new Paragraph(" "));

                        // Tabela 2 kolumny - Wprowadzenie | Weryfikacja
                        // Tabela 2 kolumny - OBLICZENIA po lewej | ZATWIERDZENIE po prawej
                        PdfPTable statsTable = new PdfPTable(2);
                        statsTable.WidthPercentage = 100;
                        statsTable.HorizontalAlignment = Element.ALIGN_LEFT;
                        statsTable.SetWidths(new float[] { 55f, 45f });

                        // Lewa kolumna - SPOSÓB OBLICZENIA
                        PdfPCell leftCell = new PdfPCell();
                        leftCell.Border = Rectangle.NO_BORDER;
                        leftCell.Padding = 5;

                        iTextSharp.text.Font fontCalcTitle = new iTextSharp.text.Font(bfArial, 8, iTextSharp.text.Font.BOLD);
                        iTextSharp.text.Font fontCalc = new iTextSharp.text.Font(bfArial, 7);

                        Paragraph calcTitle = new Paragraph("SPOSÓB OBLICZENIA:", fontCalcTitle);
                        calcTitle.SpacingAfter = 3;
                        leftCell.AddElement(calcTitle);

                        // Pobierz wartości z wiersza SUMA
                        var suma = sumRowData;

                        // Konkretne i proste obliczenia
                        leftCell.AddElement(new Paragraph($"• Suma [szt] = Padłe + Konfi = {suma.SztukiPadle:N0} + {suma.SztukiKonfi:N0} = {suma.SztukiSuma:N0} szt", fontCalc));
                        leftCell.AddElement(new Paragraph($"• Zdatne [szt] = Lumel - KonT = {suma.Lumel:N0} - {suma.SztukiKonfiskataT:N0} = {suma.SztukiZdatne:N0} szt", fontCalc));
                        leftCell.AddElement(new Paragraph($"• Suma [kg] = Konfi[kg] + Padłe[kg] = {suma.KgKonf:N0} + {suma.KgPadle:N0} = {suma.KgSuma:N0} kg", fontCalc));
                        leftCell.AddElement(new Paragraph($"• Wydajność = Prod.[kg] ÷ Żywiec[kg] × 100 = {suma.WagaProdukcjaTuszka:N0} ÷ {suma.IloscKgZywiec:N0} × 100 = {suma.WydajnoscProcent:F2}%", fontCalc));
                        leftCell.AddElement(new Paragraph($"• Śr.waga = Netto ÷ (Lumel + Padłe)", fontCalc));
                        leftCell.AddElement(new Paragraph($"• Żywiec[kg] = Netto - Padłe[kg] - Konfi[kg] - Ubytek - Opas. - Kl.B", fontCalc));

                        statsTable.AddCell(leftCell);

                        // Prawa kolumna - WPROWADZENIE i WERYFIKACJA
                        PdfPCell rightCell = new PdfPCell();
                        rightCell.Border = Rectangle.NO_BORDER;
                        rightCell.Padding = 5;

                        // Wprowadzenie
                        Paragraph wpTitlePar = new Paragraph("WPROWADZENIE:", fontSum);
                        wpTitlePar.SpacingAfter = 2;
                        rightCell.AddElement(wpTitlePar);

                        if (wprowadzenia.Count > 0)
                        {
                            foreach (var kv in wprowadzenia)
                            {
                                decimal pct = (decimal)kv.Value / total * 100;
                                Paragraph p = new Paragraph($"  {kv.Key}: {kv.Value}/{total} ({pct:F0}%)", fontData);
                                rightCell.AddElement(p);
                            }
                        }
                        else
                        {
                            rightCell.AddElement(new Paragraph("  Brak", fontData));
                        }

                        // Weryfikacja
                        Paragraph wrTitlePar = new Paragraph("WERYFIKACJA:", fontSum);
                        wrTitlePar.SpacingBefore = 5;
                        wrTitlePar.SpacingAfter = 2;
                        rightCell.AddElement(wrTitlePar);

                        if (weryfikacje.Count > 0)
                        {
                            foreach (var kv in weryfikacje)
                            {
                                decimal pct = (decimal)kv.Value / total * 100;
                                Paragraph p = new Paragraph($"  {kv.Key}: {kv.Value}/{total} ({pct:F0}%)", fontData);
                                rightCell.AddElement(p);
                            }
                        }
                        else
                        {
                            rightCell.AddElement(new Paragraph("  Brak", fontData));
                        }

                        statsTable.AddCell(rightCell);

                        doc.Add(statsTable);
                    }

                    doc.Close();
                }

                UpdateStatus($"Eksportowano PDF: {fileName}");
                var result = MessageBox.Show($"Podsumowanie zostało wyeksportowane:\n{filePath}\n\nCzy otworzyć plik?", "Sukces", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (result == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo { FileName = filePath, UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd eksportu PDF:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Oblicza kolor wydajności dla PDF - skala 3-kolorowa (czerwony-żółty-zielony)
        /// Min=74%, Środek=75%, Max=79%
        /// </summary>
        private BaseColor GetWydajnoscPdfColor(decimal wydajnosc)
        {
            byte r, g, b;

            if (wydajnosc <= 74)
            {
                // Czerwony
                r = 255; g = 0; b = 0;
            }
            else if (wydajnosc <= 75)
            {
                // Interpolacja czerwony -> żółty (74-75)
                double t = (double)(wydajnosc - 74) / 1.0;
                r = 255;
                g = (byte)(255 * t);
                b = 0;
            }
            else if (wydajnosc <= 79)
            {
                // Interpolacja żółty -> zielony (75-79)
                double t = (double)(wydajnosc - 75) / 4.0;
                r = (byte)(255 * (1 - t));
                g = 255;
                b = 0;
            }
            else
            {
                // Zielony
                r = 0; g = 255; b = 0;
            }

            return new BaseColor(r, g, b);
        }

        /// <summary>
        /// Kopiuje dane z karty Podsumowanie do schowka (format dla Excel)
        /// </summary>
        private void BtnPodsumowanieCopy_Click(object sender, RoutedEventArgs e)
        {
            if (podsumowanieData.Count == 0)
            {
                MessageBox.Show("Brak danych do skopiowania!", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var sb = new StringBuilder();

                // Nagłówki
                sb.AppendLine("L.P\tHodowca Drobiu\tSzt.Zadeklarowane\tSzt.Padłe\tSzt.Konfi\tSzt.Suma\tKgKonf\tKgPadłe\tKgSuma\tWydajność%\tLumel\tSzt.KonT\tSzt.Zdatne\tKgŻywiec\tŚr.Waga\tSzt.ProdTuszka\tWagaProdTuszka\tRóżn.Zdat-Prod\tRóżn.Zdat-Zadekl");

                // Dane
                foreach (var row in podsumowanieData)
                {
                    sb.AppendLine($"{row.LP}\t{row.HodowcaDrobiu}\t{row.SztukiZadeklarowane}\t{row.SztukiPadle}\t{row.SztukiKonfi}\t{row.SztukiSuma}\t{row.KgKonf}\t{row.KgPadle}\t{row.KgSuma}\t{row.WydajnoscProcent:F2}\t{row.Lumel}\t{row.SztukiKonfiskataT}\t{row.SztukiZdatne}\t{row.IloscKgZywiec:F0}\t{row.SredniaWagaPrzedUbojem:F2}\t{row.SztukiProdukcjaTuszka}\t{row.WagaProdukcjaTuszka:F0}\t{row.RoznicaSztukZdatneProd}\t{row.RoznicaSztukZdatneZadekl}");
                }

                // Suma
                sb.AppendLine($"SUMA\t\t{podsumowanieData.Sum(r => r.SztukiZadeklarowane)}\t{podsumowanieData.Sum(r => r.SztukiPadle)}\t{podsumowanieData.Sum(r => r.SztukiKonfi)}\t{podsumowanieData.Sum(r => r.SztukiSuma)}\t{podsumowanieData.Sum(r => r.KgKonf)}\t{podsumowanieData.Sum(r => r.KgPadle)}\t{podsumowanieData.Sum(r => r.KgSuma)}\t{podsumowanieData.Average(r => r.WydajnoscProcent):F2}\t{podsumowanieData.Sum(r => r.Lumel)}\t{podsumowanieData.Sum(r => r.SztukiKonfiskataT)}\t{podsumowanieData.Sum(r => r.SztukiZdatne)}\t{podsumowanieData.Sum(r => r.IloscKgZywiec):F0}\t{podsumowanieData.Average(r => r.SredniaWagaPrzedUbojem):F2}\t{podsumowanieData.Sum(r => r.SztukiProdukcjaTuszka)}\t{podsumowanieData.Sum(r => r.WagaProdukcjaTuszka):F0}\t{podsumowanieData.Sum(r => r.RoznicaSztukZdatneProd)}\t{podsumowanieData.Sum(r => r.RoznicaSztukZdatneZadekl)}");

                Clipboard.SetText(sb.ToString());
                UpdateStatus("Dane skopiowane do schowka");
                MessageBox.Show("Dane zostały skopiowane do schowka.\nMożesz wkleić je do Excela (Ctrl+V).", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd kopiowania:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Otwiera folder z podsumowaniami PDF
        /// </summary>
        private void BtnPodsumowanieOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DateTime selectedDate = dateTimePicker1.SelectedDate ?? DateTime.Today;

                // Pobierz ścieżkę z bazy lub użyj domyślnej
                string basePath = GetPodsumowaniePathFromDb() ??
                                  Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ZPSP", "Podsumowania");

                string yearFolder = selectedDate.Year.ToString();
                string monthFolder = selectedDate.Month.ToString("D2");
                string folderPath = Path.Combine(basePath, yearFolder, monthFolder);

                // Jeśli folder nie istnieje, otwórz folder bazowy
                if (!Directory.Exists(folderPath))
                {
                    folderPath = basePath;
                    if (!Directory.Exists(folderPath))
                    {
                        Directory.CreateDirectory(folderPath);
                    }
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = folderPath,
                    UseShellExecute = true
                });

                UpdateStatus($"Otwarto folder: {folderPath}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd otwierania folderu:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Wysyła podsumowanie emailem z załącznikiem PDF
        /// </summary>
        private void BtnPodsumowanieEmail_Click(object sender, RoutedEventArgs e)
        {
            if (podsumowanieData.Count == 0)
            {
                MessageBox.Show("Brak danych do wysłania!", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                DateTime selectedDate = dateTimePicker1.SelectedDate ?? DateTime.Today;
                string[] dniTygodnia = { "Niedziela", "Poniedziałek", "Wtorek", "Środa", "Czwartek", "Piątek", "Sobota" };
                string dzienTygodnia = dniTygodnia[(int)selectedDate.DayOfWeek];

                // Oblicz sumy
                var sumaZdatne = podsumowanieData.Where(r => !r.IsSumRow).Sum(r => r.SztukiZdatne);
                var sumaKgZywiec = podsumowanieData.Where(r => !r.IsSumRow).Sum(r => r.IloscKgZywiec);
                var iloscSpec = podsumowanieData.Count(r => !r.IsSumRow);

                // Tytuł emaila
                string subject = $"Podsumowanie uboju z dnia {selectedDate:dd.MM.yyyy} ({dzienTygodnia})";

                // Treść emaila
                string body = $"Dzień dobry,\n\n" +
                             $"W załączniku przesyłam raport podsumowania z przyjęcia żywca do uboju.\n\n" +
                             $"Data uboju: {selectedDate:dd.MM.yyyy} ({dzienTygodnia})\n" +
                             $"Liczba specyfikacji: {iloscSpec}\n" +
                             $"Suma sztuk zdatnych: {sumaZdatne:N0} szt\n" +
                             $"Suma kg żywca: {sumaKgZywiec:N0} kg\n\n" +
                             $"Pozdrawiam,\n" +
                             $"UBOJNIA DROBIU \"PIÓRKOWSCY\"";

                // Otwórz domyślnego klienta poczty
                string mailto = $"mailto:?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}";

                Process.Start(new ProcessStartInfo
                {
                    FileName = mailto,
                    UseShellExecute = true
                });

                UpdateStatus("Otwarto klienta poczty - załącz PDF ręcznie");
                MessageBox.Show("Otworzyłem domyślnego klienta poczty.\n\nPamiętaj o załączeniu pliku PDF podsumowania ręcznie!\n\n(Najpierw zapisz PDF przyciskiem 'Zapisz Podsumowanie')",
                    "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd otwierania poczty:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }

    // === KLASY POMOCNICZE ===

    // Klasa dla akcji undo
    public class UndoAction
{
    public string ActionType { get; set; } // DELETE, EDIT
    public int RowId { get; set; }
    public string PropertyName { get; set; }
    public object OldValue { get; set; }
    public object NewValue { get; set; }
    public SpecyfikacjaRow RowData { get; set; } // Kopia całego wiersza dla DELETE
    public int RowIndex { get; set; }
}

// Klasa dla wpisu w historii zmian
public class ChangeLogEntry
{
    public DateTime Timestamp { get; set; }
    public int RowId { get; set; }
    public string Action { get; set; }
    public string PropertyName { get; set; }
    public string OldValue { get; set; }
    public string NewValue { get; set; }
    public string UserName { get; set; }
}

// Klasa dla elementu dostawcy w ComboBox
public class DostawcaItem
{
    public string GID { get; set; }
    public string ShortName { get; set; }
}

// Klasa modelu danych
public class SpecyfikacjaRow : INotifyPropertyChanged
{
    private int _id;
    private int _nr;
    private int _number;        // Numer specyfikacji
    private int _yearNumber;    // Numer w roku
    private string _dostawcaGID;
    private string _dostawca;
    private string _realDostawca;
    private int _sztukiDek;
    private int _padle;
    private int _ch;
    private int _nw;
    private int _zm;
    private string _bruttoHodowcy;
    private string _taraHodowcy;
    private string _nettoHodowcy;
    private decimal _nettoHodowcyValue;
    private string _bruttoUbojni;
    private string _taraUbojni;
    private string _nettoUbojni;
    private decimal _nettoUbojniValue;
    private int _lumel;
    private int _sztukiWybijak;
    private decimal _kilogramyWybijak;
    private decimal _cena;
    private decimal _dodatek;
    private string _typCeny;
    private bool _piK;
    private decimal _ubytek;
    private bool _wydrukowano;
    // Nowe pola
    private decimal _opasienie;
    private decimal _klasaB;
    private decimal _payWgt;  // Wartość PayWgt z bazy danych (Do zapł.)
    private int _terminDni;
    private DateTime _dataUboju;
    private bool _symfonia;

    // Pola transportowe
    private int? _driverGID;
    private string _carID;
    private string _trailerID;
    private DateTime? _arrivalTime;
    private string _kierowcaNazwa;

    // Godziny transportowe
    private DateTime? _poczatekUslugi;
    private DateTime? _wyjazd;
    private DateTime? _dojazdHodowca;
    private DateTime? _zaladunek;
    private DateTime? _zaladunekKoniec;
    private DateTime? _wyjazdHodowca;
    private DateTime? _koniecUslugi;

    // Podświetlenie grupy dostawcy
    private bool _isHighlighted;
    private string _supplierColor = "#CCCCCC";
    private bool _isFirstInGroup;
    private bool _isLastInGroup;

    // Pośrednik
    private int? _idPosrednik;
    private string _posrednikNazwa;

    // Ścieżki do zdjęć z ważenia
    private string _zdjecieTaraPath;
    private string _zdjecieBruttoPath;

    // Odbiorca (CustomerRealGid)
    private string _odbiorca;

    // Partia drobiu (powiazanie z PartiaDostawca)
    private Guid? _partiaGuid;
    private string _partiaNumber;

    public bool IsHighlighted
    {
        get => _isHighlighted;
        set { _isHighlighted = value; OnPropertyChanged(nameof(IsHighlighted)); }
    }

    // Kolor paska bocznego dostawcy
    public string SupplierColor
    {
        get => _supplierColor;
        set { _supplierColor = value; OnPropertyChanged(nameof(SupplierColor)); }
    }

    // Czy pierwszy wiersz w grupie dostawcy (do separatora wizualnego)
    public bool IsFirstInGroup
    {
        get => _isFirstInGroup;
        set { _isFirstInGroup = value; OnPropertyChanged(nameof(IsFirstInGroup)); }
    }

    // Czy ostatni wiersz w grupie dostawcy
    public bool IsLastInGroup
    {
        get => _isLastInGroup;
        set { _isLastInGroup = value; OnPropertyChanged(nameof(IsLastInGroup)); }
    }

    public bool Wydrukowano
    {
        get => _wydrukowano;
        set { _wydrukowano = value; OnPropertyChanged(nameof(Wydrukowano)); }
    }

    public string Odbiorca
    {
        get => _odbiorca;
        set { _odbiorca = value; OnPropertyChanged(nameof(Odbiorca)); }
    }

    // Partia - GUID i numer partii
    public Guid? PartiaGuid
    {
        get => _partiaGuid;
        set
        {
            _partiaGuid = value;
            OnPropertyChanged(nameof(PartiaGuid));
            OnPropertyChanged(nameof(HasPartia));
        }
    }

    public string PartiaNumber
    {
        get => _partiaNumber;
        set
        {
            _partiaNumber = value;
            OnPropertyChanged(nameof(PartiaNumber));
            OnPropertyChanged(nameof(HasPartia));
        }
    }

    public bool HasPartia => PartiaGuid.HasValue && !string.IsNullOrEmpty(PartiaNumber);

    public int ID
    {
        get => _id;
        set { _id = value; OnPropertyChanged(nameof(ID)); }
    }

    public int Nr
    {
        get => _nr;
        set { _nr = value; OnPropertyChanged(nameof(Nr)); }
    }

    public int Number
    {
        get => _number;
        set { _number = value; OnPropertyChanged(nameof(Number)); }
    }

    public int YearNumber
    {
        get => _yearNumber;
        set { _yearNumber = value; OnPropertyChanged(nameof(YearNumber)); }
    }

    public string DostawcaGID
    {
        get => _dostawcaGID;
        set { _dostawcaGID = value; OnPropertyChanged(nameof(DostawcaGID)); }
    }

    public string Dostawca
    {
        get => _dostawca;
        set { _dostawca = value; OnPropertyChanged(nameof(Dostawca)); }
    }

    public string RealDostawca
    {
        get => _realDostawca;
        set { _realDostawca = value; OnPropertyChanged(nameof(RealDostawca)); }
    }

    // Pośrednik - ID i nazwa
    public int? IdPosrednik
    {
        get => _idPosrednik;
        set { _idPosrednik = value; OnPropertyChanged(nameof(IdPosrednik)); OnPropertyChanged(nameof(PosrednikNazwa)); }
    }

    public string PosrednikNazwa
    {
        get => _posrednikNazwa;
        set { _posrednikNazwa = value; OnPropertyChanged(nameof(PosrednikNazwa)); }
    }

    // Ścieżki do zdjęć z ważenia
    public string ZdjecieTaraPath
    {
        get => _zdjecieTaraPath;
        set { _zdjecieTaraPath = value; OnPropertyChanged(nameof(ZdjecieTaraPath)); OnPropertyChanged(nameof(HasZdjecieTara)); }
    }

    public string ZdjecieBruttoPath
    {
        get => _zdjecieBruttoPath;
        set { _zdjecieBruttoPath = value; OnPropertyChanged(nameof(ZdjecieBruttoPath)); OnPropertyChanged(nameof(HasZdjecieBrutto)); }
    }

    // Właściwości pomocnicze - czy zdjęcie istnieje
    public bool HasZdjecieTara => !string.IsNullOrEmpty(_zdjecieTaraPath);
    public bool HasZdjecieBrutto => !string.IsNullOrEmpty(_zdjecieBruttoPath);

    public int SztukiDek
    {
        get => _sztukiDek;
        set { _sztukiDek = value; OnPropertyChanged(nameof(SztukiDek)); RecalculateWartosc(); }
    }

    public int Padle
    {
        get => _padle;
        set { _padle = value; OnPropertyChanged(nameof(Padle)); RecalculateWartosc(); }
    }

    public int CH
    {
        get => _ch;
        set { _ch = value; OnPropertyChanged(nameof(CH)); RecalculateWartosc(); }
    }

    public int NW
    {
        get => _nw;
        set { _nw = value; OnPropertyChanged(nameof(NW)); RecalculateWartosc(); }
    }

    public int ZM
    {
        get => _zm;
        set { _zm = value; OnPropertyChanged(nameof(ZM)); RecalculateWartosc(); }
    }

    public string BruttoHodowcy
    {
        get => _bruttoHodowcy;
        set { _bruttoHodowcy = value; OnPropertyChanged(nameof(BruttoHodowcy)); }
    }

    public string TaraHodowcy
    {
        get => _taraHodowcy;
        set { _taraHodowcy = value; OnPropertyChanged(nameof(TaraHodowcy)); }
    }

    public string NettoHodowcy
    {
        get => _nettoHodowcy;
        set { _nettoHodowcy = value; OnPropertyChanged(nameof(NettoHodowcy)); }
    }

    public decimal NettoHodowcyValue
    {
        get => _nettoHodowcyValue;
        set { _nettoHodowcyValue = value; OnPropertyChanged(nameof(NettoHodowcyValue)); OnPropertyChanged(nameof(WagaNettoDoRozliczenia)); RecalculateWartosc(); }
    }

    /// <summary>
    /// Waga netto do rozliczenia: preferuje wagę hodowcy, jeśli jest > 0, w przeciwnym razie wagę ubojni
    /// </summary>
    public decimal WagaNettoDoRozliczenia => NettoHodowcyValue > 0 ? NettoHodowcyValue : NettoUbojniValue;

    public string BruttoUbojni
    {
        get => _bruttoUbojni;
        set { _bruttoUbojni = value; OnPropertyChanged(nameof(BruttoUbojni)); }
    }

    public string TaraUbojni
    {
        get => _taraUbojni;
        set { _taraUbojni = value; OnPropertyChanged(nameof(TaraUbojni)); }
    }

    public string NettoUbojni
    {
        get => _nettoUbojni;
        set { _nettoUbojni = value; OnPropertyChanged(nameof(NettoUbojni)); }
    }

    public decimal NettoUbojniValue
    {
        get => _nettoUbojniValue;
        set { _nettoUbojniValue = value; OnPropertyChanged(nameof(NettoUbojniValue)); OnPropertyChanged(nameof(WagaNettoDoRozliczenia)); RecalculateWartosc(); }
    }

    public int LUMEL
    {
        get => _lumel;
        set { _lumel = value; OnPropertyChanged(nameof(LUMEL)); }
    }

    public int SztukiWybijak
    {
        get => _sztukiWybijak;
        set { _sztukiWybijak = value; OnPropertyChanged(nameof(SztukiWybijak)); }
    }

    public decimal KilogramyWybijak
    {
        get => _kilogramyWybijak;
        set { _kilogramyWybijak = value; OnPropertyChanged(nameof(KilogramyWybijak)); }
    }

    public decimal Cena
    {
        get => _cena;
        set { _cena = value; OnPropertyChanged(nameof(Cena)); RecalculateWartosc(); }
    }

    public decimal Dodatek
    {
        get => _dodatek;
        set { _dodatek = value; OnPropertyChanged(nameof(Dodatek)); RecalculateWartosc(); }
    }

    public string TypCeny
    {
        get => _typCeny;
        set { _typCeny = value; OnPropertyChanged(nameof(TypCeny)); }
    }

    public bool PiK
    {
        get => _piK;
        set { _piK = value; OnPropertyChanged(nameof(PiK)); RecalculateWartosc(); }
    }

    public decimal Ubytek
    {
        get => _ubytek;
        set { _ubytek = value; OnPropertyChanged(nameof(Ubytek)); RecalculateWartosc(); }
    }

    // === NOWE WŁAŚCIWOŚCI ===

    public decimal Opasienie
    {
        get => _opasienie;
        set { _opasienie = value; OnPropertyChanged(nameof(Opasienie)); RecalculateWartosc(); }
    }

    public decimal KlasaB
    {
        get => _klasaB;
        set { _klasaB = value; OnPropertyChanged(nameof(KlasaB)); RecalculateWartosc(); }
    }

    /// <summary>
    /// PayWgt z bazy danych - kolumna "Do zapł." z PDF specyfikacji
    /// </summary>
    public decimal PayWgt
    {
        get => _payWgt;
        set { _payWgt = value; OnPropertyChanged(nameof(PayWgt)); }
    }

    public int TerminDni
    {
        get => _terminDni;
        set { _terminDni = value; OnPropertyChanged(nameof(TerminDni)); OnPropertyChanged(nameof(TerminPlatnosci)); }
    }

    public bool Symfonia
    {
        get => _symfonia;
        set { _symfonia = value; OnPropertyChanged(nameof(Symfonia)); }
    }

    public DateTime DataUboju
    {
        get => _dataUboju;
        set { _dataUboju = value; OnPropertyChanged(nameof(DataUboju)); OnPropertyChanged(nameof(TerminPlatnosci)); }
    }

    // === WŁAŚCIWOŚCI TRANSPORTOWE ===
    public int? DriverGID
    {
        get => _driverGID;
        set { _driverGID = value; OnPropertyChanged(nameof(DriverGID)); }
    }

    public string CarID
    {
        get => _carID;
        set { _carID = value; OnPropertyChanged(nameof(CarID)); }
    }

    public string TrailerID
    {
        get => _trailerID;
        set { _trailerID = value; OnPropertyChanged(nameof(TrailerID)); }
    }

    public DateTime? ArrivalTime
    {
        get => _arrivalTime;
        set { _arrivalTime = value; OnPropertyChanged(nameof(ArrivalTime)); }
    }

    public string KierowcaNazwa
    {
        get => _kierowcaNazwa;
        set { _kierowcaNazwa = value; OnPropertyChanged(nameof(KierowcaNazwa)); }
    }

    // === GODZINY TRANSPORTOWE ===
    public DateTime? PoczatekUslugi
    {
        get => _poczatekUslugi;
        set { _poczatekUslugi = value; OnPropertyChanged(nameof(PoczatekUslugi)); }
    }

    public DateTime? Wyjazd
    {
        get => _wyjazd;
        set { _wyjazd = value; OnPropertyChanged(nameof(Wyjazd)); }
    }

    public DateTime? DojazdHodowca
    {
        get => _dojazdHodowca;
        set { _dojazdHodowca = value; OnPropertyChanged(nameof(DojazdHodowca)); }
    }

    public DateTime? Zaladunek
    {
        get => _zaladunek;
        set { _zaladunek = value; OnPropertyChanged(nameof(Zaladunek)); }
    }

    public DateTime? ZaladunekKoniec
    {
        get => _zaladunekKoniec;
        set { _zaladunekKoniec = value; OnPropertyChanged(nameof(ZaladunekKoniec)); }
    }

    public DateTime? WyjazdHodowca
    {
        get => _wyjazdHodowca;
        set { _wyjazdHodowca = value; OnPropertyChanged(nameof(WyjazdHodowca)); }
    }

    public DateTime? KoniecUslugi
    {
        get => _koniecUslugi;
        set { _koniecUslugi = value; OnPropertyChanged(nameof(KoniecUslugi)); }
    }

    // === WŁAŚCIWOŚCI OBLICZANE ===

    /// <summary>
    /// Suma konfiskat: CH + NW + ZM [szt]
    /// </summary>
    public int Konfiskaty => CH + NW + ZM;

    /// <summary>
    /// Średnia waga: WagaNettoDoRozliczenia / (LUMEL + Padłe) [kg/szt]
    /// Gdzie: LUMEL + Padłe = Dostarcz.(ARIMR)
    /// Zaokrąglane standardowo do 2 miejsc po przecinku (jak w PDF specyfikacji)
    /// </summary>
    public decimal SredniaWaga => (LUMEL + Padle) > 0 ? Math.Round(WagaNettoDoRozliczenia / (LUMEL + Padle), 2) : 0;

    /// <summary>
    /// Dokładna średnia waga (NIEZAOKRĄGLONA) - używana do obliczania kg padłych i konfiskat
    /// Zgodnie z PDF specyfikacji: kg = szt × (Netto / Dostarcz) - bez zaokrąglania pośredniego
    /// </summary>
    private decimal DokladnaSrWaga => (LUMEL + Padle) > 0 ? WagaNettoDoRozliczenia / (LUMEL + Padle) : 0;

    /// <summary>
    /// Sztuki zdatne: SztukiDek - Padłe - Konfiskaty [szt]
    /// </summary>
    public int Zdatne => SztukiDek - Padle - Konfiskaty;

    /// <summary>
    /// Padłe w kg: Padłe × Śr.waga (niezaokrąglona), wynik zaokrąglony do pełnych kg
    /// Wzór zgodny z PDF: Padłe[kg] = Padłe[szt] × (Netto ÷ Dostarcz.)
    /// </summary>
    public decimal PadleKg => Math.Round(Padle * DokladnaSrWaga, 0);

    /// <summary>
    /// Konfiskaty w kg: Konfiskaty × Śr.waga (niezaokrąglona), wynik zaokrąglony do pełnych kg
    /// Wzór zgodny z PDF: Konf.[kg] = Konf.[szt] × (Netto ÷ Dostarcz.)
    /// </summary>
    public decimal KonfiskatyKg => Math.Round(Konfiskaty * DokladnaSrWaga, 0);

    /// <summary>
    /// Do zapłaty [kg]: Netto - Padłe[kg] - Konf[kg] - Ubytek[kg] - Opas - KlB
    /// Wzór zgodny z PDF specyfikacji:
    /// Ubytek[kg] = Netto × Ubytek%
    /// Do zapł. = Netto - Padłe[kg] - Konf[kg] - Ubytek[kg] - Opas - KlB
    /// </summary>
    public decimal DoZaplaty
    {
        get
        {
            decimal netto = WagaNettoDoRozliczenia;

            // Ubytek liczony od NETTO (zgodnie z PDF)
            decimal ubytekKg = Math.Round(netto * (Ubytek / 100), 0);

            decimal doZaplaty = netto;

            // Jeśli PiK = false, odejmujemy padłe i konfiskaty
            if (!PiK)
            {
                doZaplaty -= PadleKg;
                doZaplaty -= KonfiskatyKg;
            }

            // Zawsze odejmujemy opasienie, klasę B i ubytek
            doZaplaty -= Opasienie;
            doZaplaty -= KlasaB;
            doZaplaty -= ubytekKg;

            return Math.Round(doZaplaty, 0);
        }
    }

    /// <summary>
    /// Termin płatności (data)
    /// </summary>
    public DateTime TerminPlatnosci => DataUboju.AddDays(TerminDni);

    // === WARTOŚĆ KOŃCOWA ===

    /// <summary>
    /// Wartość końcowa = DoZaplaty * (Cena + Dodatek)
    /// </summary>
    public decimal Wartosc
    {
        get
        {
            decimal cenaZDodatkiem = Cena + Dodatek;
            return Math.Round(DoZaplaty * cenaZDodatkiem, 0);
        }
    }

    public void RecalculateWartosc()
    {
        OnPropertyChanged(nameof(Konfiskaty));
        OnPropertyChanged(nameof(SredniaWaga));
        OnPropertyChanged(nameof(Zdatne));
        OnPropertyChanged(nameof(PadleKg));
        OnPropertyChanged(nameof(KonfiskatyKg));
        OnPropertyChanged(nameof(DoZaplaty));
        OnPropertyChanged(nameof(Wartosc));
    }

    // === PDF STATUS ===
    private bool _hasPdf = false;
    private string _pdfPath = null;

    public bool HasPdf
    {
        get => _hasPdf;
        set { _hasPdf = value; OnPropertyChanged(nameof(HasPdf)); OnPropertyChanged(nameof(PdfStatus)); }
    }

    public string PdfPath
    {
        get => _pdfPath;
        set { _pdfPath = value; OnPropertyChanged(nameof(PdfPath)); }
    }

    public string PdfStatus => HasPdf ? "✓" : "";

    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Konwerter dla kolumny Dodatek:
/// - Pokazuje puste pole gdy wartość = 0
/// - Obsługuje przecinek i kropkę jako separator dziesiętny
/// </summary>
public class ZeroToEmptyConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value == null)
            return string.Empty;

        // Obsługa int
        if (value is int intValue)
        {
            if (intValue == 0)
                return string.Empty;
            return intValue.ToString();
        }

        // Obsługa decimal
        if (value is decimal decValue)
        {
            if (decValue == 0)
                return string.Empty;
            return decValue.ToString("F2", culture);
        }

        // Obsługa double
        if (value is double dblValue)
        {
            if (dblValue == 0)
                return string.Empty;
            return dblValue.ToString();
        }

        return value.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            // Zwróć odpowiedni typ zera
            if (targetType == typeof(int)) return 0;
            if (targetType == typeof(decimal)) return 0m;
            if (targetType == typeof(double)) return 0.0;
            return 0;
        }

        string input = value.ToString().Trim();
        input = input.Replace(',', '.');

        // Dla int
        if (targetType == typeof(int))
        {
            if (int.TryParse(input, out int intResult))
                return intResult;
            return 0;
        }

        // Dla decimal
        if (targetType == typeof(decimal))
        {
            if (decimal.TryParse(input, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal decResult))
                return decResult;
            return 0m;
        }

        // Dla double
        if (targetType == typeof(double))
        {
            if (double.TryParse(input, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double dblResult))
                return dblResult;
            return 0.0;
        }

        return 0;
    }
}

/// <summary>
/// Model danych dla wiersza transportu
/// </summary>
public class TransportRow : INotifyPropertyChanged
{
    private int _specyfikacjaID;
    private int _nr;
    private string _dostawca;
    private string _kierowca;
    private string _samochod;
    private string _nrRejestracyjny;
    private DateTime? _godzinaWyjazdu;
    private DateTime? _godzinaPrzyjazdu;
    private string _trasa;
    private int _iloscSkrzynek;
    private int _sztuki;
    private decimal _kilogramy;
    private string _status;
    private string _uwagi;

    // ID powiązane ze SpecyfikacjaRow.ID (FarmerCalc.ID)
    public int SpecyfikacjaID
    {
        get => _specyfikacjaID;
        set { _specyfikacjaID = value; OnPropertyChanged(nameof(SpecyfikacjaID)); }
    }

    // Godziny transportowe
    private DateTime? _poczatekUslugi;
    private DateTime? _dojazdHodowca;
    private DateTime? _zaladunek;
    private DateTime? _zaladunekKoniec;
    private DateTime? _wyjazdHodowca;
    private DateTime? _koniecUslugi;

    public int Nr
    {
        get => _nr;
        set { _nr = value; OnPropertyChanged(nameof(Nr)); }
    }

    public string Dostawca
    {
        get => _dostawca;
        set { _dostawca = value; OnPropertyChanged(nameof(Dostawca)); }
    }

    public string Kierowca
    {
        get => _kierowca;
        set { _kierowca = value; OnPropertyChanged(nameof(Kierowca)); }
    }

    public string Samochod
    {
        get => _samochod;
        set { _samochod = value; OnPropertyChanged(nameof(Samochod)); }
    }

    public string NrRejestracyjny
    {
        get => _nrRejestracyjny;
        set { _nrRejestracyjny = value; OnPropertyChanged(nameof(NrRejestracyjny)); }
    }

    public DateTime? GodzinaWyjazdu
    {
        get => _godzinaWyjazdu;
        set { _godzinaWyjazdu = value; OnPropertyChanged(nameof(GodzinaWyjazdu)); }
    }

    public DateTime? GodzinaPrzyjazdu
    {
        get => _godzinaPrzyjazdu;
        set { _godzinaPrzyjazdu = value; OnPropertyChanged(nameof(GodzinaPrzyjazdu)); }
    }

    public string Trasa
    {
        get => _trasa;
        set { _trasa = value; OnPropertyChanged(nameof(Trasa)); }
    }

    public int IloscSkrzynek
    {
        get => _iloscSkrzynek;
        set { _iloscSkrzynek = value; OnPropertyChanged(nameof(IloscSkrzynek)); }
    }

    public int Sztuki
    {
        get => _sztuki;
        set { _sztuki = value; OnPropertyChanged(nameof(Sztuki)); }
    }

    public decimal Kilogramy
    {
        get => _kilogramy;
        set { _kilogramy = value; OnPropertyChanged(nameof(Kilogramy)); }
    }

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(nameof(Status)); }
    }

    public string Uwagi
    {
        get => _uwagi;
        set { _uwagi = value; OnPropertyChanged(nameof(Uwagi)); }
    }

    // === GODZINY TRANSPORTOWE ===
    public DateTime? PoczatekUslugi
    {
        get => _poczatekUslugi;
        set { _poczatekUslugi = value; OnPropertyChanged(nameof(PoczatekUslugi)); OnPropertyChanged(nameof(CzasCalkowity)); }
    }

    public DateTime? DojazdHodowca
    {
        get => _dojazdHodowca;
        set { _dojazdHodowca = value; OnPropertyChanged(nameof(DojazdHodowca)); OnPropertyChanged(nameof(CzasDojazd)); OnPropertyChanged(nameof(CzasPostoj)); }
    }

    public DateTime? Zaladunek
    {
        get => _zaladunek;
        set { _zaladunek = value; OnPropertyChanged(nameof(Zaladunek)); OnPropertyChanged(nameof(CzasZaladunek)); }
    }

    public DateTime? ZaladunekKoniec
    {
        get => _zaladunekKoniec;
        set { _zaladunekKoniec = value; OnPropertyChanged(nameof(ZaladunekKoniec)); OnPropertyChanged(nameof(CzasZaladunek)); }
    }

    public DateTime? WyjazdHodowca
    {
        get => _wyjazdHodowca;
        set { _wyjazdHodowca = value; OnPropertyChanged(nameof(WyjazdHodowca)); OnPropertyChanged(nameof(CzasPostoj)); OnPropertyChanged(nameof(CzasRozladunek)); }
    }

    public DateTime? KoniecUslugi
    {
        get => _koniecUslugi;
        set { _koniecUslugi = value; OnPropertyChanged(nameof(KoniecUslugi)); OnPropertyChanged(nameof(CzasCalkowity)); OnPropertyChanged(nameof(IsKoniec0000)); }
    }

    // === OBLICZONE CZASY TRWANIA ===

    /// <summary>
    /// Czas dojazdu do hodowcy (GodzinaWyjazdu -> DojazdHodowca)
    /// </summary>
    public string CzasDojazd
    {
        get
        {
            if (GodzinaWyjazdu.HasValue && DojazdHodowca.HasValue)
            {
                var diff = DojazdHodowca.Value - GodzinaWyjazdu.Value;
                return FormatTimeSpan(diff);
            }
            return "";
        }
    }

    /// <summary>
    /// Czas załadunku (Zaladunek -> ZaladunekKoniec)
    /// </summary>
    public string CzasZaladunek
    {
        get
        {
            if (Zaladunek.HasValue && ZaladunekKoniec.HasValue)
            {
                var diff = ZaladunekKoniec.Value - Zaladunek.Value;
                return FormatTimeSpan(diff);
            }
            return "";
        }
    }

    /// <summary>
    /// Czas postoju u hodowcy (DojazdHodowca -> WyjazdHodowca)
    /// </summary>
    public string CzasPostoj
    {
        get
        {
            if (DojazdHodowca.HasValue && WyjazdHodowca.HasValue)
            {
                var diff = WyjazdHodowca.Value - DojazdHodowca.Value;
                return FormatTimeSpan(diff);
            }
            return "";
        }
    }

    /// <summary>
    /// Czas powrotu (WyjazdHodowca -> GodzinaPrzyjazdu)
    /// </summary>
    public string CzasRozladunek
    {
        get
        {
            if (WyjazdHodowca.HasValue && GodzinaPrzyjazdu.HasValue)
            {
                var diff = GodzinaPrzyjazdu.Value - WyjazdHodowca.Value;
                return FormatTimeSpan(diff);
            }
            return "";
        }
    }

    /// <summary>
    /// Całkowity czas usługi (PoczatekUslugi -> KoniecUslugi lub GodzinaWyjazdu -> GodzinaPrzyjazdu)
    /// </summary>
    public string CzasCalkowity
    {
        get
        {
            if (PoczatekUslugi.HasValue && KoniecUslugi.HasValue)
            {
                var diff = KoniecUslugi.Value - PoczatekUslugi.Value;
                return FormatTimeSpan(diff);
            }
            else if (GodzinaWyjazdu.HasValue && GodzinaPrzyjazdu.HasValue)
            {
                var diff = GodzinaPrzyjazdu.Value - GodzinaWyjazdu.Value;
                return FormatTimeSpan(diff);
            }
            return "";
        }
    }

    private string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalHours < 0)
            return $"-{(int)Math.Abs(ts.TotalHours)}:{Math.Abs(ts.Minutes):D2}";
        return $"{(int)ts.TotalHours}:{ts.Minutes:D2}";
    }

    // === CZASY W MINUTACH (dla nowych kolumn) ===

    /// <summary>
    /// Czas dojazdu w minutach (Wyj -> Doj)
    /// </summary>
    public int? CzasDojMin
    {
        get
        {
            if (GodzinaWyjazdu.HasValue && DojazdHodowca.HasValue)
            {
                return (int)(DojazdHodowca.Value - GodzinaWyjazdu.Value).TotalMinutes;
            }
            return null;
        }
    }

    /// <summary>
    /// Czas powrotu w minutach (Wyj.H -> Przyj)
    /// </summary>
    public int? CzasPowrotMin
    {
        get
        {
            if (WyjazdHodowca.HasValue && GodzinaPrzyjazdu.HasValue)
            {
                return (int)(GodzinaPrzyjazdu.Value - WyjazdHodowca.Value).TotalMinutes;
            }
            return null;
        }
    }

    // Średnie czasy dla dostawcy (ustawiane zewnętrznie)
    private int _sredniaCzasDojMin;
    private int _sredniaCzasPowrotMin;

    public int SredniaCzasDojMin
    {
        get => _sredniaCzasDojMin;
        set
        {
            _sredniaCzasDojMin = value;
            OnPropertyChanged(nameof(SredniaCzasDojMin));
            OnPropertyChanged(nameof(IsCzasDojAbnormal));
        }
    }

    public int SredniaCzasPowrotMin
    {
        get => _sredniaCzasPowrotMin;
        set
        {
            _sredniaCzasPowrotMin = value;
            OnPropertyChanged(nameof(SredniaCzasPowrotMin));
            OnPropertyChanged(nameof(IsCzasPowrotAbnormal));
        }
    }

    /// <summary>
    /// Czy czas dojazdu jest nietypowo długi (>20min ponad średnią)
    /// </summary>
    public bool IsCzasDojAbnormal
    {
        get
        {
            if (!CzasDojMin.HasValue || SredniaCzasDojMin == 0) return false;
            return CzasDojMin.Value > SredniaCzasDojMin + 20;
        }
    }

    /// <summary>
    /// Czy czas powrotu jest nietypowo długi (>20min ponad średnią)
    /// </summary>
    public bool IsCzasPowrotAbnormal
    {
        get
        {
            if (!CzasPowrotMin.HasValue || SredniaCzasPowrotMin == 0) return false;
            return CzasPowrotMin.Value > SredniaCzasPowrotMin + 20;
        }
    }

    // === WAGI I UBYTEK ===
    private decimal _nettoHodowcy;
    private decimal _nettoUbojni;
    private decimal _ubytekUmowny;

    public decimal NettoHodowcy
    {
        get => _nettoHodowcy;
        set
        {
            _nettoHodowcy = value;
            OnPropertyChanged(nameof(NettoHodowcy));
            OnPropertyChanged(nameof(RoznicaWag));
            OnPropertyChanged(nameof(ProcentRoznicy));
        }
    }

    public decimal NettoUbojni
    {
        get => _nettoUbojni;
        set
        {
            _nettoUbojni = value;
            OnPropertyChanged(nameof(NettoUbojni));
            OnPropertyChanged(nameof(RoznicaWag));
            OnPropertyChanged(nameof(ProcentRoznicy));
        }
    }

    /// <summary>
    /// Różnica wag: Netto Hodowcy - Netto Ubojni
    /// </summary>
    public decimal RoznicaWag => NettoHodowcy - NettoUbojni;

    /// <summary>
    /// Procent różnicy od wagi hodowcy: (RoznicaWag / NettoHodowcy) * 100
    /// </summary>
    public decimal ProcentRoznicy
    {
        get
        {
            if (NettoHodowcy > 0)
                return (RoznicaWag / NettoHodowcy) * 100;
            return 0;
        }
    }

    /// <summary>
    /// Ubytek umowny z karty Specyfikacje (Loss)
    /// </summary>
    public decimal UbytekUmowny
    {
        get => _ubytekUmowny;
        set
        {
            _ubytekUmowny = value;
            OnPropertyChanged(nameof(UbytekUmowny));
            OnPropertyChanged(nameof(RoznicaProcentow));
        }
    }

    /// <summary>
    /// Różnica między % wyliczonym a ubytkiem umownym
    /// </summary>
    public decimal RoznicaProcentow => ProcentRoznicy - UbytekUmowny;

    /// <summary>
    /// Czy różnica procentów jest ujemna (dobrze - mniej ubytku niż zakładano)
    /// </summary>
    public bool IsRoznicaUjemna => NettoHodowcy > 0 && RoznicaProcentow < 0;

    /// <summary>
    /// Czy różnica procentów jest dodatnia (źle - więcej ubytku niż zakładano)
    /// </summary>
    public bool IsRoznicaDodatnia => NettoHodowcy > 0 && RoznicaProcentow > 0;

    // === CENA I DOPŁATA ===
    private decimal _cena;
    private decimal _doplata;

    /// <summary>
    /// Cena z bazy danych (Price)
    /// </summary>
    public decimal Cena
    {
        get => _cena;
        set
        {
            _cena = value;
            OnPropertyChanged(nameof(Cena));
            OnPropertyChanged(nameof(ZyskStrata));
            OnPropertyChanged(nameof(IsZyskUjemny));
            OnPropertyChanged(nameof(IsZyskDodatni));
        }
    }

    /// <summary>
    /// Dopłata z bazy danych (Addition)
    /// </summary>
    public decimal Doplata
    {
        get => _doplata;
        set
        {
            _doplata = value;
            OnPropertyChanged(nameof(Doplata));
            OnPropertyChanged(nameof(ZyskStrata));
            OnPropertyChanged(nameof(IsZyskUjemny));
            OnPropertyChanged(nameof(IsZyskDodatni));
        }
    }

    /// <summary>
    /// Zysk/Strata w zł = Różnica kg * (Cena + Dopłata). Zwraca 0 gdy brak NettoHodowcy.
    /// </summary>
    public decimal ZyskStrata => NettoHodowcy == 0 ? 0 : RoznicaWag * (Cena + Doplata);

    /// <summary>
    /// Czy zysk/strata jest ujemna (tylko gdy jest waga hodowcy)
    /// </summary>
    public bool IsZyskUjemny => NettoHodowcy > 0 && ZyskStrata < 0;

    /// <summary>
    /// Czy zysk/strata jest dodatnia (tylko gdy jest waga hodowcy)
    /// </summary>
    public bool IsZyskDodatni => NettoHodowcy > 0 && ZyskStrata > 0;

    /// <summary>
    /// Wyświetlany tekst dla RoznicaProcentow - pusty gdy brak NettoHodowcy
    /// </summary>
    public string RoznicaProcentowDisplay => NettoHodowcy == 0 ? "" : $"{RoznicaProcentow:F2}%";

    /// <summary>
    /// Wyświetlany tekst dla ZyskStrata - pusty gdy brak NettoHodowcy
    /// </summary>
    public string ZyskStrataDisplay => NettoHodowcy == 0 ? "" : $"{ZyskStrata:F0} zł";

    // Liczba skrzynek (z FarmerCalc)
    private int _skrzynki;
    public int Skrzynki
    {
        get => _skrzynki;
        set { _skrzynki = value; OnPropertyChanged(nameof(Skrzynki)); }
    }

    // Sztuki pojemników (Padle)
    private int _sztPoj;
    public int SztPoj
    {
        get => _sztPoj;
        set { _sztPoj = value; OnPropertyChanged(nameof(SztPoj)); }
    }

    /// <summary>
    /// Czy KoniecUslugi jest równy 00:00 (do kolorowania wiersza na czerwono)
    /// </summary>
    public bool IsKoniec0000 => KoniecUslugi.HasValue &&
        KoniecUslugi.Value.Hour == 0 && KoniecUslugi.Value.Minute == 0;

    // === SZACOWANY CZAS DOJAZDU (na podstawie historii) ===
    private string _szacowanyCzasDojazdu;
    private int _sredniaMinutDojazdu;

    /// <summary>
    /// Szacowany czas dojazdu na podstawie historycznych danych
    /// </summary>
    public string SzacowanyCzasDojazdu
    {
        get => _szacowanyCzasDojazdu;
        set { _szacowanyCzasDojazdu = value; OnPropertyChanged(nameof(SzacowanyCzasDojazdu)); }
    }

    /// <summary>
    /// Średnia minut dojazdu do tego dostawcy (z historii)
    /// </summary>
    public int SredniaMinutDojazdu
    {
        get => _sredniaMinutDojazdu;
        set { _sredniaMinutDojazdu = value; OnPropertyChanged(nameof(SredniaMinutDojazdu)); }
    }

    // === TIMELINE: Pozycje na osi czasu dnia ===

    /// <summary>
    /// Pozycja startu na osi czasu (0-100%)
    /// </summary>
    public double TimelineStartPercent
    {
        get
        {
            var start = PoczatekUslugi ?? GodzinaWyjazdu;
            if (!start.HasValue) return 0;
            // Dzień roboczy 4:00 - 20:00 (16 godzin)
            var minutesFromStart = (start.Value.Hour - 4) * 60 + start.Value.Minute;
            return Math.Max(0, Math.Min(100, minutesFromStart / 960.0 * 100)); // 960 = 16 * 60
        }
    }

    /// <summary>
    /// Szerokość na osi czasu (różnica końca od startu w %)
    /// </summary>
    public double TimelineWidthPercent
    {
        get
        {
            var start = PoczatekUslugi ?? GodzinaWyjazdu;
            var end = KoniecUslugi ?? GodzinaPrzyjazdu;
            if (!start.HasValue || !end.HasValue) return 5; // Minimalna szerokość
            var durationMinutes = (end.Value - start.Value).TotalMinutes;
            return Math.Max(2, Math.Min(100 - TimelineStartPercent, durationMinutes / 960.0 * 100));
        }
    }

    /// <summary>
    /// Kolor paska na timeline (zielony gdy zakończone, niebieski gdy w trakcie, szary gdy nie rozpoczęte)
    /// </summary>
    public string TimelineColor
    {
        get
        {
            if (KoniecUslugi.HasValue && KoniecUslugi.Value.Hour > 0) return "#4CAF50"; // Zakończone
            if (PoczatekUslugi.HasValue || GodzinaWyjazdu.HasValue) return "#2196F3"; // W trakcie
            return "#BDBDBD"; // Nie rozpoczęte
        }
    }

    // === GRUPOWANIE: Właściwość dla separatora grupy ===
    private bool _isFirstInGroup;
    public bool IsFirstInGroup
    {
        get => _isFirstInGroup;
        set { _isFirstInGroup = value; OnPropertyChanged(nameof(IsFirstInGroup)); }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class HarmonogramRow : INotifyPropertyChanged
{
    private int _lp;
    private string _dostawca;
    private int _sztukiDek;
    private decimal _wagaDek;
    private decimal _razemKg;
    private decimal _cena;
    private decimal _dodatek;
    private string _typCeny;
    private int _auta;
    private string _uwagi;

    public int LP
    {
        get => _lp;
        set { _lp = value; OnPropertyChanged(nameof(LP)); }
    }

    public string Dostawca
    {
        get => _dostawca;
        set { _dostawca = value; OnPropertyChanged(nameof(Dostawca)); }
    }

    public int SztukiDek
    {
        get => _sztukiDek;
        set { _sztukiDek = value; OnPropertyChanged(nameof(SztukiDek)); }
    }

    public decimal WagaDek
    {
        get => _wagaDek;
        set { _wagaDek = value; OnPropertyChanged(nameof(WagaDek)); }
    }

    public decimal RazemKg
    {
        get => _razemKg;
        set { _razemKg = value; OnPropertyChanged(nameof(RazemKg)); }
    }

    public decimal Cena
    {
        get => _cena;
        set { _cena = value; OnPropertyChanged(nameof(Cena)); }
    }

    public decimal Dodatek
    {
        get => _dodatek;
        set { _dodatek = value; OnPropertyChanged(nameof(Dodatek)); }
    }

    public string TypCeny
    {
        get => _typCeny;
        set { _typCeny = value; OnPropertyChanged(nameof(TypCeny)); }
    }

    public int Auta
    {
        get => _auta;
        set { _auta = value; OnPropertyChanged(nameof(Auta)); }
    }

    public string Uwagi
    {
        get => _uwagi;
        set { _uwagi = value; OnPropertyChanged(nameof(Uwagi)); }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Flagi do masowego kopiowania pól dostawcy
/// Pozwala na kopiowanie wielu pól jednocześnie w jednej operacji SQL
/// </summary>
[Flags]
public enum SupplierFieldMask
{
    None = 0,
    Cena = 1,
    Dodatek = 2,
    Ubytek = 4,
    TypCeny = 8,
    TerminDni = 16,
    PiK = 32,
    All = Cena | Dodatek | Ubytek | TypCeny | TerminDni | PiK
}

/// <summary>
/// Schowek do kopiowania ustawień wiersza (Ctrl+Shift+C/V)
/// </summary>
public class SupplierClipboard
{
    public decimal? Cena { get; set; }
    public decimal? Dodatek { get; set; }
    public decimal? Ubytek { get; set; }
    public string TypCeny { get; set; }
    public int? TerminDni { get; set; }
    public bool? PiK { get; set; }
    public bool HasData => Cena.HasValue || Dodatek.HasValue || Ubytek.HasValue || TypCeny != null || TerminDni.HasValue || PiK.HasValue;

    public void Clear()
    {
        Cena = null;
        Dodatek = null;
        Ubytek = null;
        TypCeny = null;
        TerminDni = null;
        PiK = null;
    }

    public void CopyFrom(SpecyfikacjaRow row)
    {
        Cena = row.Cena;
        Dodatek = row.Dodatek;
        Ubytek = row.Ubytek;
        TypCeny = row.TypCeny;
        TerminDni = row.TerminDni;
        PiK = row.PiK;
    }

    public override string ToString()
    {
        var parts = new List<string>();
        if (Cena.HasValue) parts.Add($"Cena={Cena:F2}");
        if (Dodatek.HasValue) parts.Add($"Dodatek={Dodatek:F2}");
        if (Ubytek.HasValue) parts.Add($"Ubytek={Ubytek:F2}%");
        if (TypCeny != null) parts.Add($"TypCeny={TypCeny}");
        if (TerminDni.HasValue) parts.Add($"Termin={TerminDni}dni");
        if (PiK.HasValue) parts.Add($"PiK={PiK}");
        return string.Join(", ", parts);
    }
}

/// <summary>
/// Konwerter walidacji - zwraca czerwony kolor dla wartosci 0 lub pustych
/// Uzycie: Foreground="{Binding Cena, Converter={StaticResource ZeroToRedBrush}}"
/// </summary>
public class ZeroToRedBrushConverter : System.Windows.Data.IValueConverter
{
    private static readonly System.Windows.Media.SolidColorBrush RedBrush =
        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(211, 47, 47)); // #D32F2F
    private static readonly System.Windows.Media.SolidColorBrush BlackBrush =
        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black);

    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value == null) return RedBrush;

        if (value is decimal decValue && decValue == 0) return RedBrush;
        if (value is int intValue && intValue == 0) return RedBrush;
        if (value is double dblValue && dblValue == 0) return RedBrush;
        if (value is string strValue && string.IsNullOrWhiteSpace(strValue)) return RedBrush;

        return BlackBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Konwerter walidacji - zwraca Bold dla wartosci 0 (podkreslenie braku danych)
/// </summary>
public class ZeroToBoldConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value == null) return System.Windows.FontWeights.Bold;

        if (value is decimal decValue && decValue == 0) return System.Windows.FontWeights.Bold;
        if (value is int intValue && intValue == 0) return System.Windows.FontWeights.Bold;

        return System.Windows.FontWeights.Normal;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Konwerter procentu na piksele dla Timeline (0-100% -> 0-szerokość)
/// </summary>
public class PercentToPixelConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value == null) return 0.0;

        double percent = 0;
        if (value is double d) percent = d;
        else if (value is decimal dec) percent = (double)dec;
        else if (double.TryParse(value.ToString(), out double parsed)) percent = parsed;

        double maxWidth = 500; // Domyślna szerokość timeline
        if (parameter != null && double.TryParse(parameter.ToString(), out double paramWidth))
            maxWidth = paramWidth;

        return Math.Max(0, Math.Min(maxWidth, percent / 100.0 * maxWidth));
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Konwerter procentu na Margin dla pozycji na Timeline
/// </summary>
public class PercentToMarginConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value == null) return new System.Windows.Thickness(0);

        double percent = 0;
        if (value is double d) percent = d;
        else if (value is decimal dec) percent = (double)dec;
        else if (double.TryParse(value.ToString(), out double parsed)) percent = parsed;

        // Konwertuj procent na piksele (zakładając szerokość 120px dla mini timeline)
        double pixels = percent / 100.0 * 116; // 116px - margines

        return new System.Windows.Thickness(Math.Max(0, pixels), 0, 0, 0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Konwerter procentu na Width dla paska Timeline
/// </summary>
public class PercentToWidthConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value == null) return 5.0;

        double percent = 0;
        if (value is double d) percent = d;
        else if (value is decimal dec) percent = (double)dec;
        else if (double.TryParse(value.ToString(), out double parsed)) percent = parsed;

        // Konwertuj procent na piksele (zakładając szerokość 120px dla mini timeline)
        double pixels = percent / 100.0 * 116;

        return Math.Max(3, Math.Min(116, pixels)); // Minimum 3px, max 116px
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Klasa modelu danych dla karty Rozliczenia
/// </summary>
public class RozliczenieRow : INotifyPropertyChanged
{
    public int ID { get; set; }
    public DateTime Data { get; set; }
    public int Nr { get; set; }
    public string Dostawca { get; set; }
    public int SztukiDek { get; set; }
    public decimal NettoKg { get; set; }
    public decimal Cena { get; set; }
    public decimal Dodatek { get; set; }
    public string TypCeny { get; set; }
    public int TerminDni { get; set; }
    public decimal Wartosc { get; set; }

    private bool _symfonia;
    public bool Symfonia
    {
        get => _symfonia;
        set { _symfonia = value; OnPropertyChanged(nameof(Symfonia)); }
    }

    private bool _arimr;
    public bool ARIMR
    {
        get => _arimr;
        set { _arimr = value; OnPropertyChanged(nameof(ARIMR)); }
    }

    private bool _zatwierdzony;
    public bool Zatwierdzony
    {
        get => _zatwierdzony;
        set { _zatwierdzony = value; OnPropertyChanged(nameof(Zatwierdzony)); OnPropertyChanged(nameof(JestZablokowany)); }
    }

    public string ZatwierdzonePrzez { get; set; }
    public DateTime? DataZatwierdzenia { get; set; }

    // === PODWÓJNA KONTROLA: Weryfikacja przez drugiego pracownika ===
    private bool _zweryfikowany;
    public bool Zweryfikowany
    {
        get => _zweryfikowany;
        set { _zweryfikowany = value; OnPropertyChanged(nameof(Zweryfikowany)); OnPropertyChanged(nameof(JestZablokowany)); }
    }

    public string ZweryfikowanePrzez { get; set; }
    public DateTime? DataWeryfikacji { get; set; }

    /// <summary>
    /// Wiersz jest zablokowany gdy zarówno Zatwierdzony jak i Zweryfikowany są true
    /// </summary>
    public bool JestZablokowany => Zatwierdzony && Zweryfikowany;

    // === GRUPOWANIE: Właściwość dla separatora grupy ===
    private bool _isFirstInGroup;
    public bool IsFirstInGroup
    {
        get => _isFirstInGroup;
        set { _isFirstInGroup = value; OnPropertyChanged(nameof(IsFirstInGroup)); }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

}

public class PlachtaRow : INotifyPropertyChanged
{
    public int ID { get; set; }

    private int _lp;
    public int Lp
    {
        get => _lp;
        set { _lp = value; OnPropertyChanged(nameof(Lp)); }
    }

    public int NrSpec { get; set; }  // CarLp - numer specyfikacji
    public string Hodowca { get; set; }
    public string Adres { get; set; }
    public string BadaniaSalmonella { get; set; }  // VetComment
    public string NrSwZdrowia { get; set; }  // VetNo

    private string _nrGospodarstwa;
    public string NrGospodarstwa
    {
        get => _nrGospodarstwa;
        set { _nrGospodarstwa = value; OnPropertyChanged(nameof(NrGospodarstwa)); }
    }

    public int IloscDek { get; set; }  // DeclI1
    public int Lumel { get; set; }     // LumQnt - LUMEL (Dostarcz./ARIMR)
    public string Ciagnik { get; set; }  // CarID
    public string Naczepa { get; set; }  // TrailerID

    private int _padle;
    public int Padle
    {
        get => _padle;
        set { _padle = value; OnPropertyChanged(nameof(Padle)); }
    }

    public string KodHodowcy { get; set; }  // CustomerGID

    private int _chore;
    public int Chore
    {
        get => _chore;
        set { _chore = value; OnPropertyChanged(nameof(Chore)); }
    }

    private int _nw;
    public int NW
    {
        get => _nw;
        set { _nw = value; OnPropertyChanged(nameof(NW)); }
    }

    private int _zm;
    public int ZM
    {
        get => _zm;
        set { _zm = value; OnPropertyChanged(nameof(ZM)); }
    }

    public bool Status { get; set; }
    public int CustomerGID { get; set; }

    /// <summary>
    /// Waga netto [kg] - preferuje NettoFarmWeight, fallback do NettoWeight
    /// </summary>
    public decimal NettoWeight { get; set; }

    /// <summary>
    /// Średnia waga = Netto / (Lumel + Padłe) [kg/szt]
    /// Dostarcz.(ARIMR) = Lumel + Padłe
    /// </summary>
    public decimal SredniaWaga => (Lumel + Padle) > 0 ? Math.Round(NettoWeight / (Lumel + Padle), 2) : 0;

    // === GRUPOWANIE: Właściwość dla separatora grupy ===
    private bool _isFirstInGroup;
    public bool IsFirstInGroup
    {
        get => _isFirstInGroup;
        set { _isFirstInGroup = value; OnPropertyChanged(nameof(IsFirstInGroup)); }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Model dla mini kalendarza - reprezentuje jeden dzień
/// </summary>
public class CalendarDayItem : INotifyPropertyChanged
{
    public DateTime Date { get; set; }
    public string DayNumber => Date.Day.ToString();
    public string DayName => Date.ToString("ddd", System.Globalization.CultureInfo.GetCultureInfo("pl-PL"));

    private bool _hasData;
    public bool HasData
    {
        get => _hasData;
        set { _hasData = value; OnPropertyChanged(nameof(HasData)); }
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
    }

    public bool IsToday => Date.Date == DateTime.Today;

    private int _recordCount;
    public int RecordCount
    {
        get => _recordCount;
        set { _recordCount = value; OnPropertyChanged(nameof(RecordCount)); OnPropertyChanged(nameof(ToolTipText)); }
    }

    public string ToolTipText => HasData
        ? $"{Date:dd.MM.yyyy} ({DayName})\n{RecordCount} rekordów"
        : $"{Date:dd.MM.yyyy} ({DayName})\nBrak danych";

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Model dla pośrednika - używany w ComboBox kolumny Pośrednik
/// Użytkownik dostarczy tabelę z danymi pośredników później
/// </summary>
public class PosrednikItem
{
    public int ID { get; set; }
    public string Nazwa { get; set; }
    public string Kod { get; set; }
}

/// <summary>
/// Model dla karty Podsumowanie - Raport z przyjęcia żywca do uboju
/// Agreguje dane specyfikacji dla każdego hodowcy
/// </summary>
public class PodsumowanieRow : INotifyPropertyChanged
{
    public int LP { get; set; }
    public string HodowcaDrobiu { get; set; }
    public string Odbiorca { get; set; }  // CustomerRealGid - nazwa odbiorcy
    public int SztukiZadeklarowane { get; set; }
    public int SztukiPadle { get; set; }
    public int SztukiKonfi { get; set; }
    public int SztukiSuma => SztukiPadle + SztukiKonfi;
    public int KgKonf { get; set; }
    public int KgPadle { get; set; }
    public int KgSuma => KgKonf + KgPadle;
    public decimal WydajnoscProcent { get; set; }
    public bool WydajnoscNiska => WydajnoscProcent < 77;

    /// <summary>
    /// Kolor wydajności - skala 3-kolorowa (czerwony-żółty-zielony) od 74% do 79%
    /// </summary>
    public System.Windows.Media.SolidColorBrush WydajnoscColor
    {
        get
        {
            // Progi: Min=74%, Środek=75%, Max=79%
            decimal val = WydajnoscProcent;
            byte r, g, b;

            if (val <= 74)
            {
                // Czerwony
                r = 255; g = 0; b = 0;
            }
            else if (val <= 75)
            {
                // Interpolacja czerwony -> żółty (74-75)
                double t = (double)(val - 74) / 1.0;
                r = 255;
                g = (byte)(255 * t);
                b = 0;
            }
            else if (val <= 79)
            {
                // Interpolacja żółty -> zielony (75-79)
                double t = (double)(val - 75) / 4.0;
                r = (byte)(255 * (1 - t));
                g = 255;
                b = 0;
            }
            else
            {
                // Zielony
                r = 0; g = 255; b = 0;
            }

            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
        }
    }
    public int Lumel { get; set; }
    public int SztukiKonfiskataT { get; set; }
    public int SztukiZdatne { get; set; }
    public decimal IloscKgZywiec { get; set; }
    public decimal SredniaWagaPrzedUbojem { get; set; }
    public int SztukiProdukcjaTuszka { get; set; }
    public decimal WagaProdukcjaTuszka { get; set; }
    public int RoznicaSztukZdatneProd => SztukiZdatne - SztukiProdukcjaTuszka;
    public bool RoznicaSztukUjemna => RoznicaSztukZdatneProd < 0;
    public int RoznicaSztukZdatneZadekl => SztukiZdatne - SztukiZadeklarowane;
    public bool RoznicaSztukZadeklUjemna => RoznicaSztukZdatneZadekl < 0;

    // Wlasciwosc do oznaczenia poczatku nowej grupy (dla grupowania wg odbiorcy)
    public bool IsGroupStart { get; set; }

    // Wlasciwosc do oznaczenia wiersza sumy
    public bool IsSumRow { get; set; } = false;

    // Informacje o uzytkownikach
    public string Wprowadzil { get; set; } = "-";
    public string Zatwierdził { get; set; } = "-";

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
}