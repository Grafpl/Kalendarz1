using iTextSharp.text.pdf;
using iTextSharp.text;
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

        // Drag & Drop
        private Point _dragStartPoint;
        private bool _isDragging = false;
        private SpecyfikacjaRow _draggedRow = null;
        private DataGridRow _lastHighlightedRow = null;
        private Brush _originalRowBackground = null;

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
            UpdateStatus("Dane załadowane pomyślnie");

            // Odśwież cache dostawców w tle (async)
            _ = LoadDostawcyAsync();
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
                    string query = @"SELECT ID, CarLp, CustomerGID, CustomerRealGID, DeclI1, DeclI2, DeclI3, DeclI4, DeclI5,
                                    LumQnt, ProdQnt, ProdWgt, FullFarmWeight, EmptyFarmWeight, NettoFarmWeight,
                                    FullWeight, EmptyWeight, NettoWeight, Price, Addition, PriceTypeID, IncDeadConf, Loss,
                                    Opasienie, KlasaB, TerminDni, CalcDate
                                    FROM [LibraNet].[dbo].[FarmerCalc]
                                    WHERE CalcDate = @SelectedDate
                                    ORDER BY CarLP";

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
                                DataUboju = ZapytaniaSQL.GetValueOrDefault<DateTime>(row, "CalcDate", DateTime.Today)
                            };

                            specyfikacjeData.Add(specRow);
                        }
                        UpdateStatistics();
                        UpdateStatus($"Załadowano {dataTable.Rows.Count} rekordów");
                    }
                    else
                    {
                        UpdateStatistics();
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

        // === DRAG & DROP: Rozpoczęcie przeciągania ===
        private void DataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            _isDragging = false;

            // Znajdź wiersz pod kursorem
            var row = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);
            if (row != null)
            {
                _draggedRow = row.Item as SpecyfikacjaRow;
                dataGridView1.SelectedItem = _draggedRow;
                selectedRow = _draggedRow;
            }
        }

        // === DRAG & DROP: Wykrycie ruchu myszy ===
        private void DataGrid_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _draggedRow == null)
                return;

            Point currentPosition = e.GetPosition(null);
            Vector diff = _dragStartPoint - currentPosition;

            // Rozpocznij przeciąganie po przesunięciu o min. 5 pikseli
            if (Math.Abs(diff.X) > 5 || Math.Abs(diff.Y) > 5)
            {
                _isDragging = true;
                DataObject dragData = new DataObject("SpecyfikacjaRow", _draggedRow);
                DragDrop.DoDragDrop(dataGridView1, dragData, DragDropEffects.Move);
                _isDragging = false;
                _draggedRow = null;
            }
        }

        // === DRAG & DROP: Podgląd miejsca upuszczenia z podświetleniem ===
        private void DataGrid_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("SpecyfikacjaRow"))
            {
                e.Effects = DragDropEffects.None;
                return;
            }

            e.Effects = DragDropEffects.Move;

            // Znajdź wiersz pod kursorem i podświetl go
            var targetRow = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);
            if (targetRow != null && targetRow != _lastHighlightedRow)
            {
                // Przywróć poprzedni wiersz
                ResetHighlightedRow();

                // Podświetl nowy wiersz
                _lastHighlightedRow = targetRow;
                _originalRowBackground = targetRow.Background;
                targetRow.Background = new SolidColorBrush(Color.FromRgb(144, 238, 144)); // LightGreen
                targetRow.BorderBrush = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                targetRow.BorderThickness = new Thickness(2);
            }

            e.Handled = true;
        }

        // === Przywróć wygląd podświetlonego wiersza ===
        private void ResetHighlightedRow()
        {
            if (_lastHighlightedRow != null)
            {
                _lastHighlightedRow.Background = _originalRowBackground ?? Brushes.Transparent;
                _lastHighlightedRow.BorderThickness = new Thickness(0);
                _lastHighlightedRow = null;
                _originalRowBackground = null;
            }
        }

        // === DRAG & DROP: Opuszczenie obszaru - reset podświetlenia ===
        private void DataGrid_DragLeave(object sender, DragEventArgs e)
        {
            ResetHighlightedRow();
        }

        // === DRAG & DROP: Upuszczenie wiersza z auto-zapisem ===
        private void DataGrid_Drop(object sender, DragEventArgs e)
        {
            // Przywróć podświetlenie
            ResetHighlightedRow();

            if (!e.Data.GetDataPresent("SpecyfikacjaRow"))
                return;

            var draggedItem = e.Data.GetData("SpecyfikacjaRow") as SpecyfikacjaRow;
            if (draggedItem == null) return;

            // Znajdź wiersz docelowy
            var targetRow = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);
            if (targetRow == null) return;

            var targetItem = targetRow.Item as SpecyfikacjaRow;
            if (targetItem == null || targetItem == draggedItem) return;

            int oldIndex = specyfikacjeData.IndexOf(draggedItem);
            int newIndex = specyfikacjeData.IndexOf(targetItem);

            if (oldIndex < 0 || newIndex < 0) return;

            // Przenieś wiersz
            specyfikacjeData.Move(oldIndex, newIndex);

            // Zaktualizuj numery LP
            UpdateRowNumbers();

            // Zaznacz przeniesiony wiersz
            dataGridView1.SelectedItem = draggedItem;
            selectedRow = draggedItem;

            // AUTO-ZAPIS: Zapisz pozycje wszystkich wierszy
            SaveAllRowPositions();

            e.Handled = true;
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

        // Rozpocznij edycję po naciśnięciu klawisza (cyfry, litery)
        private void DataGridView1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var cellInfo = dataGridView1.CurrentCell;
            if (cellInfo.Column == null) return;

            // Sprawdź czy kolumna jest edytowalna (tak samo jak w PreviewMouseLeftButtonDown)
            bool isEditable = !cellInfo.Column.IsReadOnly;
            if (cellInfo.Column is DataGridTemplateColumn templateColumn)
            {
                isEditable = templateColumn.CellEditingTemplate != null;
            }

            if (!isEditable) return;

            var dataGridCell = GetDataGridCell(cellInfo);
            if (dataGridCell != null && !dataGridCell.IsEditing)
            {
                // Sprawdź czy to jest klawisz alfanumeryczny
                if ((e.Key >= Key.D0 && e.Key <= Key.D9) ||
                    (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9) ||
                    (e.Key >= Key.A && e.Key <= Key.Z) ||
                    e.Key == Key.OemComma || e.Key == Key.OemPeriod ||
                    e.Key == Key.Decimal || e.Key == Key.OemMinus || e.Key == Key.Subtract)
                {
                    dataGridView1.BeginEdit();

                    // Dla template columns - znajdź i aktywuj TextBox
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var textBox = FindVisualChild<TextBox>(dataGridCell);
                        if (textBox != null)
                        {
                            textBox.Focus();
                            textBox.SelectAll();
                        }
                    }), System.Windows.Threading.DispatcherPriority.Input);
                }
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

        private void DataGridView1_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                var row = e.Row.Item as SpecyfikacjaRow;
                if (row != null)
                {
                    // === WYDAJNOŚĆ: Użyj debounce zamiast natychmiastowego zapisu ===
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        string columnHeader = e.Column.Header?.ToString() ?? "";

                        // Aktualizuj nazwę dostawcy jeśli zmieniono GID
                        if (columnHeader == "Dostawca" && !string.IsNullOrEmpty(row.DostawcaGID))
                        {
                            var dostawca = ListaDostawcow.FirstOrDefault(d => d.GID == row.DostawcaGID);
                            if (dostawca != null)
                            {
                                row.Dostawca = dostawca.ShortName;
                            }
                        }

                        // Przelicz wartość po każdej zmianie
                        row.RecalculateWartosc();
                        UpdateStatistics();

                        // Dodaj do kolejki debounce zamiast natychmiastowego zapisu
                        QueueRowForSave(row.ID);

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
            // Pobierz aktualnie wybrany wiersz z CurrentCell
            SpecyfikacjaRow currentRow = selectedRow;
            if (currentRow == null && dataGridView1.CurrentCell != null && dataGridView1.CurrentCell.Item is SpecyfikacjaRow row)
            {
                currentRow = row;
            }

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
            if (selectedRow != null)
            {
                try
                {
                    WidokAvilog avilogForm = new WidokAvilog(selectedRow.ID);
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
            var result = MessageBox.Show(
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
                var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Wybierz folder do zapisywania PDF",
                    SelectedPath = defaultPdfPath
                };

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    defaultPdfPath = dialog.SelectedPath;
                    useDefaultPath = true;
                    MessageBox.Show($"Nowa domyślna ścieżka:\n{defaultPdfPath}", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else if (result == MessageBoxResult.No)
            {
                useDefaultPath = false;
                MessageBox.Show("Przy następnym zapisie PDF zostaniesz zapytany o lokalizację.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
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
                    int sztZdatne = zapytaniasql.PobierzInformacjeZBazyDanych<int>(id, "[LibraNet].[dbo].[FarmerCalc]", "LumQnt") - konfiskaty;
                    int sztWszystkie = konfiskaty + padle + sztZdatne;

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
                // 17 kolumn: Lp, Brutto, Tara, Netto | Dostarcz, Padłe, Konf, Zdatne | kg/szt | Netto, Padłe, Konf, Opas, KlB, DoZapł, Cena, Wartość
                PdfPTable dataTable = new PdfPTable(new float[] { 0.3F, 0.5F, 0.5F, 0.55F, 0.5F, 0.4F, 0.45F, 0.45F, 0.55F, 0.55F, 0.5F, 0.5F, 0.5F, 0.45F, 0.55F, 0.5F, 0.65F });
                dataTable.WidthPercentage = 100;

                // Nagłówki grupowe z kolorami
                BaseColor purpleColor = new BaseColor(142, 68, 173);
                AddColoredMergedHeader(dataTable, "WAGA [kg]", tytulTablicy, 4, greenColor);
                AddColoredMergedHeader(dataTable, "ROZLICZENIE SZTUK [szt.]", tytulTablicy, 4, orangeColor);
                AddColoredMergedHeader(dataTable, "ŚR. WAGA", tytulTablicy, 1, purpleColor);
                AddColoredMergedHeader(dataTable, "ROZLICZENIE KILOGRAMÓW [kg]", tytulTablicy, 8, blueColor);

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
                    int sztZdatne = zapytaniasql.PobierzInformacjeZBazyDanych<int>(id, "[LibraNet].[dbo].[FarmerCalc]", "LumQnt") - konfiskaty;
                    int sztWszystkie = konfiskaty + padle + sztZdatne;

                    decimal sredniaWaga = sztWszystkie > 0 ? wagaNetto / sztWszystkie : 0;
                    decimal padleKG = czyPiK ? 0 : Math.Round(padle * sredniaWaga, 0);
                    decimal konfiskatyKG = czyPiK ? 0 : Math.Round(konfiskaty * sredniaWaga, 0);
                    decimal opasienieKG = Math.Round(zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "Opasienie"), 0);
                    decimal klasaB = Math.Round(zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "KlasaB"), 0);

                    // Obliczenie DoZaplaty: Netto - (PiK ? 0 : Padłe+Konf) - Opasienie - KlasaB, potem * (1 - Ubytek%)
                    decimal bazaDoZaplaty = czyPiK
                        ? wagaNetto - opasienieKG - klasaB
                        : wagaNetto - padleKG - konfiskatyKG - opasienieKG - klasaB;

                    // Zastosowanie ubytku procentowego (zgodnie z modelem)
                    decimal doZaplaty = ubytek > 0
                        ? Math.Round(bazaDoZaplaty * (1 - ubytek / 100), 0)
                        : bazaDoZaplaty;

                    decimal cenaBase = zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "Price");
                    decimal dodatek = zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "Addition");
                    decimal cena = cenaBase + dodatek; // Cena zawiera dodatek
                    decimal wartosc = cena * doZaplaty;

                    // Śledzenie Ubytku
                    if (ubytek > 0)
                    {
                        czyByloUbytku = true;
                        sumaUbytekKG += bazaDoZaplaty - doZaplaty; // różnica = ile kg odliczono przez ubytek
                    }

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

                // 6. Do zapłaty = Netto - Padłe[kg] - Konfiskaty[kg] - Opasienie - Klasa B - Ubytek%
                Paragraph formula6 = new Paragraph();
                if (czyByloUbytku)
                {
                    decimal bazaBezUbytku = sumaKG + sumaUbytekKG;
                    formula6.Add(new Chunk("Do zapł. = (Netto - Padłe - Konf. - Opas. - Kl.B) × (1 - Ubytek%): ", legendaBoldFont));
                    formula6.Add(new Chunk($"({sumaNetto:N0} - {sumaPadleKG:N0} - {sumaKonfiskatyKG:N0} - {sumaOpasienieKG:N0} - {sumaKlasaB:N0}) - {sumaUbytekKG:N0} = {sumaKG:N0} kg", legendaFont));
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

            if (value is decimal decValue)
            {
                // Jeśli wartość = 0, pokaż puste pole
                if (decValue == 0)
                    return string.Empty;

                // Formatuj z 2 miejscami po przecinku
                return decValue.ToString("F2", culture);
            }

            return value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
                return 0m;

            string input = value.ToString().Trim();

            // Zamień przecinek na kropkę dla parsowania
            input = input.Replace(',', '.');

            // Spróbuj sparsować jako decimal
            if (decimal.TryParse(input, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal result))
            {
                return result;
            }

            // Jeśli nie udało się sparsować, zwróć 0
            return 0m;
        }
    }
}
