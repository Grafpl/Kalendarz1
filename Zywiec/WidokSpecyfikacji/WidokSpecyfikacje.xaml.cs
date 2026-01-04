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

using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

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

        // Backwards compatibility
        private List<DostawcaItem> listaDostawcow { get => ListaDostawcow; set => ListaDostawcow = value; }
        private List<string> listaTypowCen { get => ListaTypowCen; set => ListaTypowCen = value; }

        // Ustawienia PDF
        private static string defaultPdfPath = @"\\192.168.0.170\Public\Przel\";
        private static bool useDefaultPath = true;
        private decimal sumaWartosc = 0;
        private decimal sumaKG = 0;

        // Arrow buttons for row movement (replaces drag & drop)

        // === WYDAJNOŚĆ: Cache dostawców (static - współdzielony między oknami) ===
        private static List<DostawcaItem> _cachedDostawcy = null;
        private static DateTime _cacheTimestamp = DateTime.MinValue;
        private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(5);

        // === WYDAJNOŚĆ: Debounce dla auto-zapisu ===
        private DispatcherTimer _debounceTimer;
        private HashSet<int> _pendingSaveIds = new HashSet<int>();
        private const int DebounceDelayMs = 500;

        // === UNDO: Stos zmian do cofnięcia ===
        private Stack<UndoAction> _undoStack = new Stack<UndoAction>();
        private const int MaxUndoHistory = 50;

        // === HISTORIA: Log zmian ===
        private List<ChangeLogEntry> _changeLog = new List<ChangeLogEntry>();

        // === TRANSPORT: Dane transportowe ===
        private ObservableCollection<TransportRow> transportData;

        // === HARMONOGRAM: Dane harmonogramu dostaw ===
        private ObservableCollection<HarmonogramRow> harmonogramDataLeft;
        private ObservableCollection<HarmonogramRow> harmonogramDataRight;

        public WidokSpecyfikacje()
        {
            InitializeComponent();

            // Inicjalizuj timer debounce
            _debounceTimer = new DispatcherTimer();
            _debounceTimer.Interval = TimeSpan.FromMilliseconds(DebounceDelayMs);
            _debounceTimer.Tick += DebounceTimer_Tick;

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

            harmonogramDataLeft = new ObservableCollection<HarmonogramRow>();
            harmonogramDataRight = new ObservableCollection<HarmonogramRow>();
            // Bindujemy do nowych DataGridów (jeśli nazwałeś je tak jak w XAML wyżej)
            dataGridHarmonogramLeft.ItemsSource = harmonogramDataLeft;
            dataGridHarmonogramRight.ItemsSource = harmonogramDataRight;

            dateTimePicker1.SelectedDate = DateTime.Today;

            // Dodaj obsługę skrótów klawiszowych
            this.KeyDown += Window_KeyDown;
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

        // === WYDAJNOŚĆ: Debounce Timer - zapisuje zmiany po 500ms nieaktywności ===
        private void DebounceTimer_Tick(object sender, EventArgs e)
        {
            _debounceTimer.Stop();

            if (_pendingSaveIds.Count > 0)
            {
                var idsToSave = _pendingSaveIds.ToList();
                _pendingSaveIds.Clear();

                // Zapisz wszystkie oczekujące zmiany w jednym batch
                SaveRowsBatch(idsToSave);
            }
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
            LoadData(dateTimePicker1.SelectedDate ?? DateTime.Today);
            UpdateFullDateLabel();
            UpdateTransportDateLabel();
            UpdateStatus("Dane załadowane pomyślnie");

            // Odśwież cache dostawców w tle (async)
            _ = LoadDostawcyAsync();
        }

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

        private void BtnRefreshTransport_Click(object sender, RoutedEventArgs e)
        {
            LoadTransportData();
            UpdateStatus("Dane transportowe odświeżone");
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
                    Nr = nr++,
                    Kierowca = spec.KierowcaNazwa ?? "",
                    Samochod = spec.CarID ?? "",
                    NrRejestracyjny = spec.TrailerID ?? "",
                    GodzinaPrzyjazdu = spec.ArrivalTime,
                    Trasa = spec.Dostawca ?? spec.RealDostawca ?? "",
                    Sztuki = spec.SztukiDek,
                    Kilogramy = spec.NettoUbojniValue,
                    Status = spec.ArrivalTime.HasValue ? "Zakończony" : "Oczekuje"
                };

                transportData.Add(transportRow);
            }
        }

        private void LoadHarmonogramData()
        {
            // Czyścimy obie listy
            harmonogramDataLeft.Clear();
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

                            // === LOGIKA PODZIAŁU NA DWIE TABELE ===
                            int halfPoint = (int)Math.Ceiling(allRows.Count / 2.0);

                            for (int i = 0; i < allRows.Count; i++)
                            {
                                if (i < halfPoint)
                                {
                                    harmonogramDataLeft.Add(allRows[i]);
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
            }
        }

        private void LoadData(DateTime selectedDate)
        {
            try
            {
                UpdateStatus("Ładowanie danych...");
                specyfikacjeData.Clear();

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"SELECT fc.ID, fc.CarLp, fc.CustomerGID, fc.CustomerRealGID, fc.DeclI1, fc.DeclI2, fc.DeclI3, fc.DeclI4, fc.DeclI5,
                                    fc.LumQnt, fc.ProdQnt, fc.ProdWgt, fc.FullFarmWeight, fc.EmptyFarmWeight, fc.NettoFarmWeight,
                                    fc.FullWeight, fc.EmptyWeight, fc.NettoWeight, fc.Price, fc.Addition, fc.PriceTypeID, fc.IncDeadConf, fc.Loss,
                                    fc.Opasienie, fc.KlasaB, fc.TerminDni, fc.CalcDate,
                                    fc.DriverGID, fc.CarID, fc.TrailerID, fc.Przyjazd,
                                    d.Name AS DriverName
                                    FROM [LibraNet].[dbo].[FarmerCalc] fc
                                    LEFT JOIN [LibraNet].[dbo].[Driver] d ON fc.DriverGID = d.GID
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

                            var specRow = new SpecyfikacjaRow
                            {
                                ID = ZapytaniaSQL.GetValueOrDefault<int>(row, "ID", 0),
                                Nr = ZapytaniaSQL.GetValueOrDefault<int>(row, "CarLp", 0),
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
                                KierowcaNazwa = ZapytaniaSQL.GetValueOrDefault<string>(row, "DriverName", "")?.Trim()
                            };

                            specyfikacjeData.Add(specRow);
                        }
                        UpdateStatistics();
                        LoadTransportData(); // Załaduj dane transportowe
                        LoadHarmonogramData(); // Załaduj harmonogram dostaw
                        LoadPdfStatusForAllRows(); // Załaduj status PDF dla wszystkich wierszy
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
            if (dataGridView1.SelectedItem != null)
            {
                selectedRow = dataGridView1.SelectedItem as SpecyfikacjaRow;
            }
            else if (dataGridView1.CurrentCell != null && dataGridView1.CurrentCell.Item != null)
            {
                selectedRow = dataGridView1.CurrentCell.Item as SpecyfikacjaRow;
            }
        }

        // === CurrentCellChanged: Aktualizacja wybranego wiersza ===
        private void DataGridView1_CurrentCellChanged(object sender, EventArgs e)
        {
            if (dataGridView1.CurrentCell.Item != null)
            {
                selectedRow = dataGridView1.CurrentCell.Item as SpecyfikacjaRow;
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
            UpdateStatus($"Przesunięto wiersz LP {selected.Nr} w dół");
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
            // === ENTER: Przejście w dół (Shift+Enter = góra) ===
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                var direction = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)
                    ? FocusNavigationDirection.Up
                    : FocusNavigationDirection.Down;

                var uiElement = e.OriginalSource as UIElement;
                uiElement?.MoveFocus(new TraversalRequest(direction));
            }

            // === TAB: Przejście w prawo (Shift+Tab = lewo) ===
            else if (e.Key == Key.Tab)
            {
                // Domyślne zachowanie Tab jest OK, ale upewniamy się że działa
                var direction = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)
                    ? FocusNavigationDirection.Left
                    : FocusNavigationDirection.Right;

                var uiElement = e.OriginalSource as UIElement;
                if (uiElement != null)
                {
                    e.Handled = true;
                    uiElement.MoveFocus(new TraversalRequest(direction));
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

            // Mapowanie kolumn na właściwości
            string header = currentColumn.Header?.ToString() ?? "";
            bool copied = false;

            switch (header)
            {
                case "Cena":
                    currentRow.Cena = rowAbove.Cena;
                    SaveFieldToDatabase(currentRow.ID, "Price", currentRow.Cena);
                    copied = true;
                    break;
                case "Dodatek":
                    currentRow.Dodatek = rowAbove.Dodatek;
                    SaveFieldToDatabase(currentRow.ID, "Addition", currentRow.Dodatek);
                    copied = true;
                    break;
                case "Ubytek%":
                    currentRow.Ubytek = rowAbove.Ubytek;
                    SaveFieldToDatabase(currentRow.ID, "Loss", currentRow.Ubytek / 100);
                    copied = true;
                    break;
                case "Typ Ceny":
                    currentRow.TypCeny = rowAbove.TypCeny;
                    SaveFieldToDatabase(currentRow.ID, "PriceType", currentRow.TypCeny);
                    copied = true;
                    break;
                case "Termin":
                    currentRow.TerminDni = rowAbove.TerminDni;
                    SaveFieldToDatabase(currentRow.ID, "TermDays", currentRow.TerminDni);
                    copied = true;
                    break;
                case "Szt.Dek":
                    currentRow.SztukiDek = rowAbove.SztukiDek;
                    QueueSaveRow(currentRow.ID);
                    copied = true;
                    break;
            }

            if (copied)
            {
                UpdateStatus($"Ctrl+D: Skopiowano wartość z wiersza powyżej");
                dataGridView1.Items.Refresh();
            }
        }

        // === CTRL+SHIFT+D: Zastosuj wartość do wszystkich wierszy tego samego dostawcy ===
        private void ApplyValueToAllRowsOfSupplier()
        {
            var currentRow = selectedRow ?? dataGridView1.SelectedItem as SpecyfikacjaRow;
            if (currentRow == null || string.IsNullOrEmpty(currentRow.RealDostawca)) return;

            var currentColumn = dataGridView1.CurrentColumn;
            if (currentColumn == null) return;

            string header = currentColumn.Header?.ToString() ?? "";
            var rowsToUpdate = specyfikacjeData.Where(x => x.RealDostawca == currentRow.RealDostawca).ToList();
            int count = 0;

            foreach (var row in rowsToUpdate)
            {
                switch (header)
                {
                    case "Cena":
                        row.Cena = currentRow.Cena;
                        SaveFieldToDatabase(row.ID, "Price", row.Cena);
                        count++;
                        break;
                    case "Dodatek":
                        row.Dodatek = currentRow.Dodatek;
                        SaveFieldToDatabase(row.ID, "Addition", row.Dodatek);
                        count++;
                        break;
                    case "Ubytek%":
                        row.Ubytek = currentRow.Ubytek;
                        SaveFieldToDatabase(row.ID, "Loss", row.Ubytek / 100);
                        count++;
                        break;
                    case "Typ Ceny":
                        row.TypCeny = currentRow.TypCeny;
                        SaveFieldToDatabase(row.ID, "PriceType", row.TypCeny);
                        count++;
                        break;
                }
            }

            if (count > 0)
            {
                UpdateStatus($"Ctrl+Shift+D: Zastosowano do {count} wierszy dostawcy {currentRow.RealDostawca}");
                dataGridView1.Items.Refresh();
            }
        }

        #region === MENU KONTEKSTOWE: Handlery ===

        private void ContextMenu_CopyFromAbove(object sender, RoutedEventArgs e)
        {
            CopyValueFromCellAbove();
        }

        private void ContextMenu_ApplyCenaToSupplier(object sender, RoutedEventArgs e)
        {
            var currentRow = selectedRow ?? dataGridView1.SelectedItem as SpecyfikacjaRow;
            if (currentRow == null || string.IsNullOrEmpty(currentRow.RealDostawca)) return;

            var rowsToUpdate = specyfikacjeData.Where(x => x.RealDostawca == currentRow.RealDostawca).ToList();
            foreach (var row in rowsToUpdate)
            {
                row.Cena = currentRow.Cena;
                SaveFieldToDatabase(row.ID, "Price", row.Cena);
            }
            UpdateStatus($"💰 Cena {currentRow.Cena:F2} zł → {rowsToUpdate.Count} wierszy dostawcy {currentRow.RealDostawca}");
            dataGridView1.Items.Refresh();
        }

        private void ContextMenu_ApplyDodatekToSupplier(object sender, RoutedEventArgs e)
        {
            var currentRow = selectedRow ?? dataGridView1.SelectedItem as SpecyfikacjaRow;
            if (currentRow == null || string.IsNullOrEmpty(currentRow.RealDostawca)) return;

            var rowsToUpdate = specyfikacjeData.Where(x => x.RealDostawca == currentRow.RealDostawca).ToList();
            foreach (var row in rowsToUpdate)
            {
                row.Dodatek = currentRow.Dodatek;
                SaveFieldToDatabase(row.ID, "Addition", row.Dodatek);
            }
            UpdateStatus($"➕ Dodatek {currentRow.Dodatek:F2} zł → {rowsToUpdate.Count} wierszy dostawcy {currentRow.RealDostawca}");
            dataGridView1.Items.Refresh();
        }

        private void ContextMenu_ApplyUbytekToSupplier(object sender, RoutedEventArgs e)
        {
            var currentRow = selectedRow ?? dataGridView1.SelectedItem as SpecyfikacjaRow;
            if (currentRow == null || string.IsNullOrEmpty(currentRow.RealDostawca)) return;

            var rowsToUpdate = specyfikacjeData.Where(x => x.RealDostawca == currentRow.RealDostawca).ToList();
            foreach (var row in rowsToUpdate)
            {
                row.Ubytek = currentRow.Ubytek;
                SaveFieldToDatabase(row.ID, "Loss", row.Ubytek / 100);
            }
            UpdateStatus($"📉 Ubytek {currentRow.Ubytek:F2}% → {rowsToUpdate.Count} wierszy dostawcy {currentRow.RealDostawca}");
            dataGridView1.Items.Refresh();
        }

        private void ContextMenu_ApplyTypCenyToSupplier(object sender, RoutedEventArgs e)
        {
            var currentRow = selectedRow ?? dataGridView1.SelectedItem as SpecyfikacjaRow;
            if (currentRow == null || string.IsNullOrEmpty(currentRow.RealDostawca)) return;

            var rowsToUpdate = specyfikacjeData.Where(x => x.RealDostawca == currentRow.RealDostawca).ToList();
            foreach (var row in rowsToUpdate)
            {
                row.TypCeny = currentRow.TypCeny;
                SaveFieldToDatabase(row.ID, "PriceType", row.TypCeny);
            }
            UpdateStatus($"🏷️ Typ ceny '{currentRow.TypCeny}' → {rowsToUpdate.Count} wierszy dostawcy {currentRow.RealDostawca}");
            dataGridView1.Items.Refresh();
        }

        private void ContextMenu_RefreshData(object sender, RoutedEventArgs e)
        {
            // Odśwież dane z bazy
            LoadData(dateTimePicker1.SelectedDate ?? DateTime.Today);
            UpdateStatus("🔄 Dane odświeżone");
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
                    // Używamy Dispatchera, aby obliczenia wykonały się po zaktualizowaniu wartości w modelu
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        // Tutaj wywołaj swoją logikę przeliczania wiersza
                        // Zakładam, że masz metodę RecalculateWartosc() w klasie SpecyfikacjaRow 
                        // lub logikę w setterach właściwości.

                        // Jeśli logika jest w setterach, to wystarczy odświeżyć podsumowania:
                        UpdateStatistics();

                        // Opcjonalnie: Kolejkuj auto-zapis (jeśli używasz)
                        // QueueRowForSave(row.ID); 
                    }), System.Windows.Threading.DispatcherPriority.Background);
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

            // Suma netto ubojni
            decimal sumaNetto = specyfikacjeData.Sum(r => r.NettoUbojniValue);
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
        }

        private void BtnSaveAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateStatus("Zapisywanie wszystkich zmian...");
                int savedCount = 0;

                foreach (var row in specyfikacjeData)
                {
                    SaveRowToDatabase(row);
                    savedCount++;
                }

                MessageBox.Show($"Zapisano {savedCount} rekordów.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                UpdateStatus($"Zapisano {savedCount} rekordów");
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
            }
        }

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
            if (colOpasienie != null)
            {
                colOpasienie.Visibility = chkShowOpasienie.IsChecked == true
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        // === Checkbox: Pokaż/ukryj kolumnę Klasa B ===
        private void ChkShowKlasaB_Changed(object sender, RoutedEventArgs e)
        {
            if (colKlasaB != null)
            {
                colKlasaB.Visibility = chkShowKlasaB.IsChecked == true
                    ? Visibility.Visible
                    : Visibility.Collapsed;
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
                    sumaNetto += row.NettoUbojniValue;
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

                // Pobierz email hodowcy
                string email = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(customerRealGID, "Email");

                // Jeśli brak email - otwórz formularz edycji hodowcy
                if (string.IsNullOrWhiteSpace(email))
                {
                    var confirmEdit = MessageBox.Show(
                        $"Brak adresu email dla hodowcy: {sellerName}\n\nCzy chcesz teraz uzupełnić dane kontaktowe?",
                        "Brak email",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (confirmEdit == MessageBoxResult.Yes)
                    {
                        var hodowcaForm = new HodowcaForm(customerRealGID, Environment.UserName);
                        hodowcaForm.ShowDialog();

                        // Sprawdź ponownie
                        email = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(customerRealGID, "Email");
                        if (string.IsNullOrWhiteSpace(email))
                        {
                            MessageBox.Show("Adres email nadal nie został uzupełniony.", "Brak email", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }
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

                // Oblicz podsumowanie
                decimal sumaNetto = 0;
                decimal sumaWartosc = 0;
                int sumaSztWszystkie = 0;

                foreach (var row in rozliczeniaHodowcy)
                {
                    sumaNetto += row.NettoUbojniValue;
                    int sztWszystkie = row.LUMEL + row.Padle;
                    sumaSztWszystkie += sztWszystkie;
                    sumaWartosc += row.Wartosc;
                }

                decimal sredniaWaga = sumaSztWszystkie > 0 ? sumaNetto / sumaSztWszystkie : 0;
                DateTime dzienUbojowy = dateTimePicker1.SelectedDate ?? DateTime.Today;

                // Wygeneruj PDF
                var allIds = rozliczeniaHodowcy.Select(r => r.ID).ToList();
                GenerateShortPDFReport(allIds, showMessage: false);

                // Ścieżka do PDF
                string strDzienUbojowy = dzienUbojowy.ToString("yyyy.MM.dd");
                string pdfPath = Path.Combine(defaultPdfPath, strDzienUbojowy, $"{sellerName} {strDzienUbojowy} - SKROCONY.pdf");

                // Temat emaila
                string emailSubject = $"Rozliczenie - Piórkowscy - {sellerName} - {dzienUbojowy:dd.MM.yyyy}";

                // Treść emaila
                string emailBody = $"Szanowny Panie/Pani {sellerName},\n\n" +
                                  $"W załączeniu przesyłamy rozliczenie z dnia {dzienUbojowy:dd MMMM yyyy}.\n\n" +
                                  $"PODSUMOWANIE:\n" +
                                  $"─────────────────────────────\n" +
                                  $"  Sztuki:        {sumaSztWszystkie}\n" +
                                  $"  Kilogramy:     {sumaNetto:N0} kg\n" +
                                  $"  Średnia waga:  {sredniaWaga:N2} kg\n" +
                                  $"  DO WYPŁATY:    {sumaWartosc:N0} zł\n" +
                                  $"─────────────────────────────\n\n" +
                                  $"W razie pytań prosimy o kontakt.\n\n" +
                                  $"Z poważaniem,\n" +
                                  $"Ubojnia Drobiu \"Piórkowscy\"\n" +
                                  $"Koziołki 40, 95-061 Dmosin\n" +
                                  $"Tel: +48 46 874 68 55";

                // Pokaż okno z gotową treścią
                var result = MessageBox.Show(
                    $"📧 EMAIL DO: {email}\n\n" +
                    $"📎 ZAŁĄCZNIK:\n{pdfPath}\n\n" +
                    $"📝 TEMAT:\n{emailSubject}\n\n" +
                    $"📄 TREŚĆ:\n{emailBody.Substring(0, Math.Min(300, emailBody.Length))}...\n\n" +
                    $"─────────────────────────────\n" +
                    $"Kliknij TAK aby:\n" +
                    $"1. Skopiować email do schowka\n" +
                    $"2. Otworzyć program pocztowy\n\n" +
                    $"Kliknij NIE aby tylko skopiować treść.",
                    "Email - Gotowy do wysłania",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    // Skopiuj email do schowka
                    System.Windows.Clipboard.SetText(email);

                    // Otwórz domyślnego klienta pocztowego
                    try
                    {
                        string mailto = $"mailto:{Uri.EscapeDataString(email)}?subject={Uri.EscapeDataString(emailSubject)}&body={Uri.EscapeDataString(emailBody)}";
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = mailto,
                            UseShellExecute = true
                        });

                        MessageBox.Show(
                            $"✅ Email skopiowany do schowka: {email}\n\n" +
                            $"📎 Załącz plik PDF:\n{pdfPath}\n\n" +
                            $"Plik PDF znajduje się w powyższej lokalizacji.\n" +
                            $"Dodaj go jako załącznik do wiadomości.",
                            "Gotowe",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    catch
                    {
                        // Jeśli nie można otworzyć mailto, skopiuj wszystko do schowka
                        System.Windows.Clipboard.SetText($"Do: {email}\nTemat: {emailSubject}\n\n{emailBody}");
                        MessageBox.Show(
                            $"Nie można otworzyć programu pocztowego.\n\n" +
                            $"Treść email skopiowana do schowka.\n\n" +
                            $"📎 Załącz plik PDF:\n{pdfPath}",
                            "Skopiowano",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }
                else if (result == MessageBoxResult.No)
                {
                    // Skopiuj tylko treść do schowka
                    System.Windows.Clipboard.SetText($"Do: {email}\nTemat: {emailSubject}\n\n{emailBody}");
                    MessageBox.Show(
                        $"✅ Treść email skopiowana do schowka.\n\n" +
                        $"📎 PDF do załączenia:\n{pdfPath}",
                        "Skopiowano",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
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

                    decimal wagaBrutto = ubytek != 0
                        ? zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "FullFarmWeight")
                        : zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "FullWeight");
                    decimal wagaTara = ubytek != 0
                        ? zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "EmptyFarmWeight")
                        : zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "EmptyWeight");
                    decimal wagaNetto = ubytek != 0
                        ? zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "NettoFarmWeight")
                        : zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "NettoWeight");

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

                // Termin płatności
                Paragraph terminInfo = new Paragraph($"Termin płatności: {terminPlatnosci:dd.MM.yyyy} ({terminZaplatyDni} dni)", new Font(polishFont, 10, Font.ITALIC, grayColor));
                terminInfo.Alignment = Element.ALIGN_CENTER;
                mainCell.AddElement(terminInfo);

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

                // === PODPISY ===
                PdfPTable sigTable = new PdfPTable(2);
                sigTable.WidthPercentage = 100;
                sigTable.SpacingBefore = 30f;

                PdfPCell sig1 = CreateRoundedCell(new BaseColor(200, 200, 200), new BaseColor(252, 252, 252), 12);
                sig1.AddElement(new Paragraph("PODPIS DOSTAWCY", new Font(polishFont, 9, Font.BOLD, orangeColor)) { Alignment = Element.ALIGN_CENTER });
                sig1.AddElement(new Paragraph(" \n \n", textFont));
                sig1.AddElement(new Paragraph("........................................", textFont) { Alignment = Element.ALIGN_CENTER });
                sigTable.AddCell(sig1);

                NazwaZiD nazwaZiD = new NazwaZiD();
                string wystawiajacyNazwa = nazwaZiD.GetNameById(App.UserID) ?? "---";

                PdfPCell sig2 = CreateRoundedCell(new BaseColor(200, 200, 200), new BaseColor(252, 252, 252), 12);
                sig2.AddElement(new Paragraph("PODPIS PRACOWNIKA", new Font(polishFont, 9, Font.BOLD, greenColor)) { Alignment = Element.ALIGN_CENTER });
                sig2.AddElement(new Paragraph(" \n \n", textFont));
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

            // A4 pionowa
            Document doc = new Document(PageSize.A4, 25, 25, 20, 20);

            string sellerName = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(
                zapytaniasql.PobierzInformacjeZBazyDanych<string>(ids[0], "[LibraNet].[dbo].[FarmerCalc]", "CustomerRealGID"),
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
                PdfPTable dataTable = new PdfPTable(new float[] { 0.3F, 0.5F, 0.5F, 0.55F, 0.5F, 0.4F, 0.45F, 0.45F, 0.55F, 0.55F, 0.5F, 0.5F, 0.5F, 0.5F, 0.45F, 0.55F, 0.5F, 0.65F });
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
                AddColoredTableHeader(dataTable, "Dostarcz.", smallTextFontBold, new BaseColor(230, 126, 34));
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

                // Dane tabeli
                for (int i = 0; i < ids.Count; i++)
                {
                    int id = ids[i];
                    bool czyPiK = zapytaniasql.PobierzInformacjeZBazyDanych<bool>(id, "[LibraNet].[dbo].[FarmerCalc]", "IncDeadConf");
                    decimal? ubytekProc = zapytaniasql.PobierzInformacjeZBazyDanych<decimal?>(id, "[LibraNet].[dbo].[FarmerCalc]", "Loss");
                    decimal ubytek = ubytekProc ?? 0;

                    decimal wagaBrutto, wagaTara, wagaNetto;
                    if (ubytek != 0)
                    {
                        wagaBrutto = zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "FullFarmWeight");
                        wagaTara = zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "EmptyFarmWeight");
                        wagaNetto = zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "NettoFarmWeight");
                    }
                    else
                    {
                        wagaBrutto = zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "FullWeight");
                        wagaTara = zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "EmptyWeight");
                        wagaNetto = zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "NettoWeight");
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
                    formula5b.Add(new Chunk($"{sumaNetto:N0} × ... = {sumaUbytekKG:N0} kg", legendaFont));
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

                // Termin płatności pod wypłatą
                sumCell.AddElement(new Paragraph(" ", new Font(polishFont, 4, Font.NORMAL)));
                Paragraph terminP = new Paragraph($"Termin płatności: {terminPlatnosci:dd.MM.yyyy} ({terminZaplatyDni} dni)", new Font(polishFont, 8, Font.ITALIC, grayColor));
                terminP.Alignment = Element.ALIGN_CENTER;
                sumCell.AddElement(terminP);

                summaryTable.AddCell(sumCell);
                doc.Add(summaryTable);

                // === PODPISY ===
                // Pobierz nazwę wystawiającego z App.UserID
                NazwaZiD nazwaZiD = new NazwaZiD();
                string wystawiajacyNazwa = nazwaZiD.GetNameById(App.UserID) ?? App.UserID ?? "---";

                PdfPTable footerTable = new PdfPTable(2);
                footerTable.WidthPercentage = 100;
                footerTable.SpacingBefore = 25f;
                footerTable.SetWidths(new float[] { 1f, 1f });

                // Podpis Dostawcy (lewa strona)
                PdfPCell signatureLeft = new PdfPCell { Border = PdfPCell.BOX, BorderColor = new BaseColor(200, 200, 200), Padding = 15, BackgroundColor = new BaseColor(252, 252, 252) };
                signatureLeft.AddElement(new Paragraph("PODPIS DOSTAWCY", new Font(polishFont, 9, Font.BOLD, orangeColor)) { Alignment = Element.ALIGN_CENTER });
                signatureLeft.AddElement(new Paragraph(" ", new Font(polishFont, 12, Font.NORMAL)));
                signatureLeft.AddElement(new Paragraph(" ", new Font(polishFont, 12, Font.NORMAL)));
                signatureLeft.AddElement(new Paragraph(" ", new Font(polishFont, 12, Font.NORMAL)));
                signatureLeft.AddElement(new Paragraph("............................................................", new Font(polishFont, 10, Font.NORMAL)) { Alignment = Element.ALIGN_CENTER });
                signatureLeft.AddElement(new Paragraph("data i czytelny podpis", new Font(polishFont, 7, Font.ITALIC, grayColor)) { Alignment = Element.ALIGN_CENTER });
                footerTable.AddCell(signatureLeft);

                // Podpis Pracownika/Wystawiającego (prawa strona)
                PdfPCell signatureRight = new PdfPCell { Border = PdfPCell.BOX, BorderColor = new BaseColor(200, 200, 200), Padding = 15, BackgroundColor = new BaseColor(252, 252, 252) };
                signatureRight.AddElement(new Paragraph("PODPIS PRACOWNIKA", new Font(polishFont, 9, Font.BOLD, greenColor)) { Alignment = Element.ALIGN_CENTER });
                signatureRight.AddElement(new Paragraph($"({wystawiajacyNazwa})", new Font(polishFont, 8, Font.NORMAL, grayColor)) { Alignment = Element.ALIGN_CENTER });
                signatureRight.AddElement(new Paragraph(" ", new Font(polishFont, 12, Font.NORMAL)));
                signatureRight.AddElement(new Paragraph(" ", new Font(polishFont, 12, Font.NORMAL)));
                signatureRight.AddElement(new Paragraph("............................................................", new Font(polishFont, 10, Font.NORMAL)) { Alignment = Element.ALIGN_CENTER });
                signatureRight.AddElement(new Paragraph("data i czytelny podpis", new Font(polishFont, 7, Font.ITALIC, grayColor)) { Alignment = Element.ALIGN_CENTER });
                footerTable.AddCell(signatureRight);

                doc.Add(footerTable);

                // Informacja o wygenerowaniu dokumentu
                Paragraph generatedBy = new Paragraph($"Wygenerowano przez: {wystawiajacyNazwa}", new Font(polishFont, 7, Font.ITALIC, grayColor));
                generatedBy.Alignment = Element.ALIGN_RIGHT;
                generatedBy.SpacingBefore = 10f;
                doc.Add(generatedBy);

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
                Padding = 4
            };
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
        private void SaveFieldToDatabase(int id, string columnName, object value)
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
                UpdateStatus($"Błąd zapisu: {ex.Message}");
            }
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

        // Handler LostFocus dla Cena - zapisuje do bazy po opuszczeniu pola
        private void Cena_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            var row = textBox.DataContext as SpecyfikacjaRow;
            if (row == null) return;

            // WAŻNE: Wymuś aktualizację bindingu przed zapisem
            var binding = textBox.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();

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

            // WAŻNE: Wymuś aktualizację bindingu przed zapisem
            var binding = textBox.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();

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

            // WAŻNE: Wymuś aktualizację bindingu przed zapisem
            var binding = textBox.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();

            // W bazie Loss jest przechowywany jako ułamek (1.5% = 0.015)
            SaveFieldToDatabase(row.ID, "Loss", row.Ubytek / 100);
            UpdateStatus($"Zapisano ubytek: {row.Ubytek}% dla LP {row.Nr}");
        }

        // Handler dla zmiany PiK (CheckBox) - zapisuje do bazy natychmiast
        private void PiK_Changed(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            if (checkBox == null) return;

            var row = checkBox.DataContext as SpecyfikacjaRow;
            if (row == null) return;

            // Binding już zaktualizował wartość
            SaveFieldToDatabase(row.ID, "IncDeadConf", row.PiK);
            UpdateStatus($"Zapisano PiK: {(row.PiK ? "TAK" : "NIE")} dla LP {row.Nr}");
        }

        // Handler dla zmiany TypCeny (ComboBox) - zapisuje do bazy natychmiast
        private void TypCeny_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox == null) return;

            var row = comboBox.DataContext as SpecyfikacjaRow;
            if (row == null) return;

            // Znajdź ID typu ceny
            int priceTypeId = -1;
            if (!string.IsNullOrEmpty(row.TypCeny))
            {
                priceTypeId = zapytaniasql.ZnajdzIdCeny(row.TypCeny);
            }

            if (priceTypeId > 0)
            {
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

            // WAŻNE: Wymuś aktualizację bindingu przed zapisem
            var binding = textBox.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();

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

            // WAŻNE: Wymuś aktualizację bindingu przed zapisem
            var binding = textBox.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();

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

            var binding = textBox.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();

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

            var binding = textBox.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();

            SaveFieldToDatabase(row.ID, "DeclI1", row.SztukiDek);
            UpdateStatus($"Zapisano Szt.Dek: {row.SztukiDek} dla LP {row.Nr}");
        }

        // Handler LostFocus dla Padle - zapisuje do bazy (DeclI2)
        private void Padle_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            var row = textBox.DataContext as SpecyfikacjaRow;
            if (row == null) return;

            var binding = textBox.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();

            SaveFieldToDatabase(row.ID, "DeclI2", row.Padle);
            UpdateStatus($"Zapisano Padłe: {row.Padle} dla LP {row.Nr}");
        }

        // Handler LostFocus dla CH - zapisuje do bazy (DeclI3)
        private void CH_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            var row = textBox.DataContext as SpecyfikacjaRow;
            if (row == null) return;

            var binding = textBox.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();

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

            var binding = textBox.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();

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

            var binding = textBox.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();

            SaveFieldToDatabase(row.ID, "DeclI5", row.ZM);
            UpdateStatus($"Zapisano ZM: {row.ZM} dla LP {row.Nr}");
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
        private int _terminDni;
        private DateTime _dataUboju;

        // Pola transportowe
        private int? _driverGID;
        private string _carID;
        private string _trailerID;
        private DateTime? _arrivalTime;
        private string _kierowcaNazwa;

        public bool Wydrukowano
        {
            get => _wydrukowano;
            set { _wydrukowano = value; OnPropertyChanged(nameof(Wydrukowano)); }
        }

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
            set { _nettoUbojniValue = value; OnPropertyChanged(nameof(NettoUbojniValue)); RecalculateWartosc(); }
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

        public int TerminDni
        {
            get => _terminDni;
            set { _terminDni = value; OnPropertyChanged(nameof(TerminDni)); OnPropertyChanged(nameof(TerminPlatnosci)); }
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

        // === WŁAŚCIWOŚCI OBLICZANE ===

        /// <summary>
        /// Suma konfiskat: CH + NW + ZM [szt]
        /// </summary>
        public int Konfiskaty => CH + NW + ZM;

        /// <summary>
        /// Średnia waga: Netto / SztukiDek [kg/szt]
        /// </summary>
        public decimal SredniaWaga => SztukiDek > 0 ? Math.Round(NettoUbojniValue / SztukiDek, 2) : 0;

        /// <summary>
        /// Sztuki zdatne: SztukiDek - Padłe - Konfiskaty [szt]
        /// </summary>
        public int Zdatne => SztukiDek - Padle - Konfiskaty;

        /// <summary>
        /// Padłe w kg: Padłe * Średnia waga [kg]
        /// </summary>
        public decimal PadleKg => Math.Round(Padle * SredniaWaga, 0);

        /// <summary>
        /// Konfiskaty w kg: Konfiskaty * Średnia waga [kg]
        /// </summary>
        public decimal KonfiskatyKg => Math.Round(Konfiskaty * SredniaWaga, 0);

        /// <summary>
        /// Do zapłaty [kg]: Netto - Padłe[kg] - Konf[kg] - Opas - KlasaB (z uwzględnieniem PiK i ubytku)
        /// </summary>
        public decimal DoZaplaty
        {
            get
            {
                decimal bazaKg = NettoUbojniValue;

                // Jeśli PiK = false, odejmujemy padłe i konfiskaty
                if (!PiK)
                {
                    bazaKg -= PadleKg;
                    bazaKg -= KonfiskatyKg;
                }

                // Zawsze odejmujemy opasienie i klasę B
                bazaKg -= Opasienie;
                bazaKg -= KlasaB;

                // Zastosowanie ubytku procentowego
                decimal poUbytku = bazaKg * (1 - Ubytek / 100);

                return Math.Round(poUbytku, 0);
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
        private int _nr;
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

        public int Nr
        {
            get => _nr;
            set { _nr = value; OnPropertyChanged(nameof(Nr)); }
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
}
