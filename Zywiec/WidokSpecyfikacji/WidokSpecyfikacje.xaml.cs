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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Kalendarz1
{
    public partial class WidokSpecyfikacje : Window
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private ZapytaniaSQL zapytaniasql = new ZapytaniaSQL();
        private ObservableCollection<SpecyfikacjaRow> specyfikacjeData;
        private SpecyfikacjaRow selectedRow;
        private List<DostawcaItem> listaDostawcow;
        private List<string> listaTypowCen = new List<string> { "wolnyrynek", "rolnicza", "łączona", "ministerialna" };

        // Ustawienia PDF
        private static string defaultPdfPath = @"\\192.168.0.170\Public\Przel\";
        private static bool useDefaultPath = true;
        private decimal sumaWartosc = 0;
        private decimal sumaKG = 0;

        public WidokSpecyfikacje()
        {
            InitializeComponent();
            specyfikacjeData = new ObservableCollection<SpecyfikacjaRow>();
            dataGridView1.ItemsSource = specyfikacjeData;
            dateTimePicker1.SelectedDate = DateTime.Today;

            // Dodaj obsługę skrótów klawiszowych
            this.KeyDown += Window_KeyDown;

            // Załaduj listę dostawców
            LoadDostawcy();
        }

        private void LoadDostawcy()
        {
            listaDostawcow = new List<DostawcaItem>();
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
                            listaDostawcow.Add(new DostawcaItem
                            {
                                GID = reader["GID"].ToString(),
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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadData(dateTimePicker1.SelectedDate ?? DateTime.Today);
            UpdateFullDateLabel();
            UpdateStatus("Dane załadowane pomyślnie");
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
                                    FullWeight, EmptyWeight, NettoWeight, Price, PriceTypeID, IncDeadConf, Loss
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
                            string customerGID = ZapytaniaSQL.GetValueOrDefault<string>(row, "CustomerGID", "-1");
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
                                TypCeny = zapytaniasql.ZnajdzNazweCenyPoID(
                                    ZapytaniaSQL.GetValueOrDefault<int>(row, "PriceTypeID", -1)),
                                PiK = row["IncDeadConf"] != DBNull.Value && Convert.ToBoolean(row["IncDeadConf"]),
                                Ubytek = Math.Round(ZapytaniaSQL.GetValueOrDefault<decimal>(row, "Loss", 0) * 100, 2)
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

        // Event handler dla ComboBox Dostawcy - automatyczne rozwijanie
        private void CboDostawca_Loaded(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox != null)
            {
                comboBox.ItemsSource = listaDostawcow;
                // Automatyczne rozwinięcie listy
                comboBox.IsDropDownOpen = true;
            }
        }

        // Event handler dla ComboBox Typ Ceny - automatyczne rozwijanie
        private void CboTypCeny_Loaded(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox != null)
            {
                comboBox.ItemsSource = listaTypowCen;
                // Automatyczne rozwinięcie listy
                comboBox.IsDropDownOpen = true;
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

        // Edycja po jednym kliknięciu
        private void DataGridView1_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var cell = FindVisualParent<DataGridCell>(e.OriginalSource as DependencyObject);
            if (cell == null || cell.IsEditing) return;

            // Sprawdź czy kolumna jest edytowalna (sprawdź kolumnę, nie komórkę)
            var column = cell.Column;
            if (column == null) return;

            // Dla DataGridTemplateColumn sprawdź czy ma CellEditingTemplate
            bool isEditable = !column.IsReadOnly;
            if (column is DataGridTemplateColumn templateColumn)
            {
                isEditable = templateColumn.CellEditingTemplate != null;
            }

            if (!isEditable) return;

            // Pobierz wiersz i ustaw CurrentCell
            var row = FindVisualParent<DataGridRow>(cell);
            if (row != null)
            {
                dataGridView1.CurrentCell = new DataGridCellInfo(row.Item, cell.Column);
                selectedRow = row.Item as SpecyfikacjaRow;
            }

            if (!cell.IsFocused)
            {
                cell.Focus();
            }

            // Rozpocznij edycję z opóźnieniem
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!cell.IsEditing)
                {
                    dataGridView1.BeginEdit();

                    // Dla template columns - znajdź i aktywuj TextBox
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var textBox = FindVisualChild<TextBox>(cell);
                        if (textBox != null)
                        {
                            textBox.Focus();
                            textBox.SelectAll();
                        }
                    }), System.Windows.Threading.DispatcherPriority.Input);
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
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
                    // Zapisz zmiany do bazy danych po zakończeniu edycji
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        string columnHeader = e.Column.Header?.ToString() ?? "";
                        UpdateDatabaseRow(row, columnHeader);

                        // Aktualizuj nazwę dostawcy jeśli zmieniono GID
                        if (columnHeader == "Dostawca" && !string.IsNullOrEmpty(row.DostawcaGID))
                        {
                            var dostawca = listaDostawcow.FirstOrDefault(d => d.GID == row.DostawcaGID);
                            if (dostawca != null)
                            {
                                row.Dostawca = dostawca.ShortName;
                            }
                        }

                        // Przelicz wartość po każdej zmianie
                        row.RecalculateWartosc();
                        UpdateStatistics();
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
                { "Typ Ceny", "PriceTypeID" },
                { "PiK", "IncDeadConf" },
                { "Ubytek%", "Loss" }
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
                case "Typ Ceny": return zapytaniasql.ZnajdzIdCeny(row.TypCeny ?? "");
                case "PiK": return row.PiK;
                case "Ubytek%": return row.Ubytek / 100; // Konwersja na wartość dla bazy
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
                    IncDeadConf = @PiK
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

                    cmd.ExecuteNonQuery();
                }
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
            if (selectedRow != null)
            {
                try
                {
                    UpdateStatus("Generowanie PDF...");

                    // Zbierz wszystkie ID dla tego samego dostawcy
                    List<int> ids = specyfikacjeData
                        .Where(r => r.RealDostawca == selectedRow.RealDostawca)
                        .Select(r => r.ID)
                        .ToList();

                    if (ids.Count > 0)
                    {
                        GeneratePDFReport(ids);
                        UpdateStatus("PDF wygenerowany pomyślnie");
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

        private void GeneratePDFReport(List<int> ids, bool showMessage = true)
        {
            sumaWartosc = 0;
            sumaKG = 0;

            Document doc = new Document(PageSize.A4.Rotate(), 30, 30, 20, 20);

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

                // Fonty
                BaseFont baseFont = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1250, BaseFont.EMBEDDED);
                Font titleFont = new Font(baseFont, 20, Font.BOLD, greenColor);
                Font subtitleFont = new Font(baseFont, 12, Font.NORMAL, BaseColor.DARK_GRAY);
                Font headerFont = new Font(baseFont, 11, Font.BOLD, BaseColor.WHITE);
                Font textFont = new Font(baseFont, 10, Font.NORMAL);
                Font textFontBold = new Font(baseFont, 10, Font.BOLD);
                Font smallTextFont = new Font(baseFont, 8, Font.NORMAL);
                Font smallTextFontBold = new Font(baseFont, 8, Font.BOLD);
                Font tytulTablicy = new Font(baseFont, 10, Font.BOLD, BaseColor.WHITE);
                Font ItalicFont = new Font(baseFont, 8, Font.ITALIC, BaseColor.GRAY);
                Font summaryFont = new Font(baseFont, 12, Font.BOLD, darkGreenColor);

                // === NAGŁÓWEK Z LOGO ===
                PdfPTable headerTable = new PdfPTable(3);
                headerTable.WidthPercentage = 100;
                headerTable.SetWidths(new float[] { 1.5f, 3f, 1.5f });
                headerTable.SpacingAfter = 15f;

                // Logo po lewej
                PdfPCell logoCell = new PdfPCell { Border = PdfPCell.NO_BORDER, VerticalAlignment = Element.ALIGN_MIDDLE };
                try
                {
                    string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo-2-green.png");
                    if (File.Exists(logoPath))
                    {
                        iTextSharp.text.Image logo = iTextSharp.text.Image.GetInstance(logoPath);
                        logo.ScaleToFit(120f, 60f);
                        logoCell.AddElement(logo);
                    }
                }
                catch { }
                headerTable.AddCell(logoCell);

                // Tytuł na środku
                PdfPCell titleCell = new PdfPCell { Border = PdfPCell.NO_BORDER, HorizontalAlignment = Element.ALIGN_CENTER, VerticalAlignment = Element.ALIGN_MIDDLE };
                Paragraph title = new Paragraph("ROZLICZENIE PRZYJĘTEGO DROBIU", titleFont) { Alignment = Element.ALIGN_CENTER };
                Paragraph subtitle = new Paragraph($"Data uboju: {strDzienUbojowyPL}", subtitleFont) { Alignment = Element.ALIGN_CENTER };
                titleCell.AddElement(title);
                titleCell.AddElement(subtitle);
                headerTable.AddCell(titleCell);

                // Numer dokumentu po prawej
                PdfPCell docNumCell = new PdfPCell { Border = PdfPCell.NO_BORDER, HorizontalAlignment = Element.ALIGN_RIGHT, VerticalAlignment = Element.ALIGN_MIDDLE };
                docNumCell.AddElement(new Paragraph($"Nr: {strDzienUbojowy}/{ids.Count}", textFontBold) { Alignment = Element.ALIGN_RIGHT });
                headerTable.AddCell(docNumCell);

                doc.Add(headerTable);

                // === LINIA ROZDZIELAJĄCA ===
                PdfPTable lineTable = new PdfPTable(1);
                lineTable.WidthPercentage = 100;
                PdfPCell lineCell = new PdfPCell { Border = PdfPCell.NO_BORDER, BorderColorBottom = greenColor, BorderWidthBottom = 2f, Padding = 0, PaddingBottom = 5f };
                lineTable.AddCell(lineCell);
                lineTable.SpacingAfter = 15f;
                doc.Add(lineTable);

                // === SEKCJA STRON ===
                PdfPTable partiesTable = new PdfPTable(2);
                partiesTable.WidthPercentage = 100;
                partiesTable.SetWidths(new float[] { 1f, 1f });
                partiesTable.SpacingAfter = 20f;

                // Nabywca (lewa strona)
                PdfPCell buyerCell = new PdfPCell { Border = PdfPCell.BOX, BorderColor = lightGreenColor, Padding = 10, BackgroundColor = new BaseColor(248, 255, 248) };
                buyerCell.AddElement(new Paragraph("NABYWCA", new Font(baseFont, 10, Font.BOLD, greenColor)));
                buyerCell.AddElement(new Paragraph("Ubojnia Drobiu \"Piórkowscy\"", textFontBold));
                buyerCell.AddElement(new Paragraph("Koziołki 40, 95-061 Dmosin", textFont));
                buyerCell.AddElement(new Paragraph("NIP: 726-162-54-06", textFont));
                partiesTable.AddCell(buyerCell);

                // Sprzedający (prawa strona)
                string sellerStreet = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(
                    zapytaniasql.PobierzInformacjeZBazyDanych<string>(ids[0], "[LibraNet].[dbo].[FarmerCalc]", "CustomerRealGID"), "Address") ?? "";
                string sellerKod = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(
                    zapytaniasql.PobierzInformacjeZBazyDanych<string>(ids[0], "[LibraNet].[dbo].[FarmerCalc]", "CustomerRealGID"), "PostalCode") ?? "";
                string sellerMiejsc = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(
                    zapytaniasql.PobierzInformacjeZBazyDanych<string>(ids[0], "[LibraNet].[dbo].[FarmerCalc]", "CustomerRealGID"), "City") ?? "";

                PdfPCell sellerCell = new PdfPCell { Border = PdfPCell.BOX, BorderColor = orangeColor, Padding = 10, BackgroundColor = new BaseColor(255, 253, 248) };
                sellerCell.AddElement(new Paragraph("SPRZEDAJĄCY", new Font(baseFont, 10, Font.BOLD, orangeColor)));
                sellerCell.AddElement(new Paragraph(sellerName, textFontBold));
                sellerCell.AddElement(new Paragraph(sellerStreet, textFont));
                sellerCell.AddElement(new Paragraph($"{sellerKod} {sellerMiejsc}", textFont));
                partiesTable.AddCell(sellerCell);

                doc.Add(partiesTable);

                // === GŁÓWNA TABELA ROZLICZENIA ===
                PdfPTable dataTable = new PdfPTable(new float[] { 0.4F, 0.6F, 0.6F, 0.6F, 0.5F, 0.5F, 0.5F, 0.5F, 0.6F, 0.6F, 0.6F, 0.6F, 0.6F, 0.6F, 0.7F, 0.7F, 0.8F });
                dataTable.WidthPercentage = 100;

                // Nagłówki grupowe z kolorami
                AddColoredMergedHeader(dataTable, "WAGA", tytulTablicy, 4, greenColor);
                AddColoredMergedHeader(dataTable, "ROZLICZENIE SZTUK", tytulTablicy, 4, orangeColor);
                AddColoredMergedHeader(dataTable, "ROZLICZENIE KILOGRAMÓW", tytulTablicy, 9, blueColor);

                // Nagłówki kolumn
                AddColoredTableHeader(dataTable, "Lp.", smallTextFontBold, darkGreenColor);
                AddColoredTableHeader(dataTable, "Brutto", smallTextFontBold, darkGreenColor);
                AddColoredTableHeader(dataTable, "Tara", smallTextFontBold, darkGreenColor);
                AddColoredTableHeader(dataTable, "Netto", smallTextFontBold, darkGreenColor);
                AddColoredTableHeader(dataTable, "Razem", smallTextFontBold, new BaseColor(230, 126, 34));
                AddColoredTableHeader(dataTable, "Padłe", smallTextFontBold, new BaseColor(230, 126, 34));
                AddColoredTableHeader(dataTable, "Konf.", smallTextFontBold, new BaseColor(230, 126, 34));
                AddColoredTableHeader(dataTable, "Zdatne", smallTextFontBold, new BaseColor(230, 126, 34));
                AddColoredTableHeader(dataTable, "Netto", smallTextFontBold, new BaseColor(41, 128, 185));
                AddColoredTableHeader(dataTable, "Padłe", smallTextFontBold, new BaseColor(41, 128, 185));
                AddColoredTableHeader(dataTable, "Konf.", smallTextFontBold, new BaseColor(41, 128, 185));
                AddColoredTableHeader(dataTable, "Opas.", smallTextFontBold, new BaseColor(41, 128, 185));
                AddColoredTableHeader(dataTable, "Ubytek", smallTextFontBold, new BaseColor(41, 128, 185));
                AddColoredTableHeader(dataTable, "Kl. B", smallTextFontBold, new BaseColor(41, 128, 185));
                AddColoredTableHeader(dataTable, "Do zapłaty", smallTextFontBold, new BaseColor(41, 128, 185));
                AddColoredTableHeader(dataTable, "Cena", smallTextFontBold, new BaseColor(41, 128, 185));
                AddColoredTableHeader(dataTable, "Wartość", smallTextFontBold, new BaseColor(41, 128, 185));

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
                    decimal ubytekKG = Math.Round(wagaNetto * ubytek, 0);
                    decimal klasaB = Math.Round(zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "KlasaB"), 0);

                    decimal sumaDoZaplaty = czyPiK
                        ? wagaNetto - opasienieKG - ubytekKG - klasaB
                        : wagaNetto - padleKG - konfiskatyKG - opasienieKG - ubytekKG - klasaB;

                    decimal cena = zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "Price");
                    decimal wartosc = cena * sumaDoZaplaty;

                    sumaWartosc += wartosc;
                    sumaKG += sumaDoZaplaty;

                    // Naprzemienne kolory wierszy
                    BaseColor rowColor = i % 2 == 0 ? BaseColor.WHITE : new BaseColor(245, 245, 245);

                    AddStyledTableData(dataTable, smallTextFont, rowColor, (i + 1).ToString(),
                        wagaBrutto.ToString("N0"), wagaTara.ToString("N0"), wagaNetto.ToString("N0"),
                        sztWszystkie.ToString(), padle.ToString(), konfiskaty.ToString(), sztZdatne.ToString(),
                        wagaNetto.ToString("N0"),
                        padleKG > 0 ? $"-{padleKG:N0}" : "0",
                        konfiskatyKG > 0 ? $"-{konfiskatyKG:N0}" : "0",
                        opasienieKG > 0 ? $"-{opasienieKG:N0}" : "0",
                        ubytekKG > 0 ? $"-{ubytekKG:N0}" : "0",
                        klasaB > 0 ? $"-{klasaB:N0}" : "0",
                        sumaDoZaplaty.ToString("N0"),
                        cena.ToString("0.00"),
                        wartosc.ToString("N0"));
                }

                doc.Add(dataTable);

                // === PODSUMOWANIE ===
                doc.Add(new Paragraph(" ") { SpacingBefore = 10f });

                // Typ ceny
                int intTypCeny = zapytaniasql.PobierzInformacjeZBazyDanych<int>(ids[0], "[LibraNet].[dbo].[FarmerCalc]", "PriceTypeID");
                string typCeny = zapytaniasql.ZnajdzNazweCenyPoID(intTypCeny);

                // Ramka podsumowania
                PdfPTable summaryTable = new PdfPTable(2);
                summaryTable.WidthPercentage = 40;
                summaryTable.HorizontalAlignment = Element.ALIGN_RIGHT;

                AddSummaryRow(summaryTable, "Typ ceny:", typCeny, textFont, ItalicFont);
                AddSummaryRow(summaryTable, "Suma kilogramów:", $"{sumaKG:N0} kg", textFontBold, summaryFont);
                AddSummaryRow(summaryTable, "SUMA WARTOŚCI:", $"{sumaWartosc:N0} zł", summaryFont, new Font(baseFont, 14, Font.BOLD, greenColor));

                doc.Add(summaryTable);

                // Informacja na dole
                doc.Add(new Paragraph(" ") { SpacingBefore = 15f });
                Paragraph italicText = new Paragraph("Wagi zaokrąglane do pełnych kilogramów.", ItalicFont) { Alignment = Element.ALIGN_CENTER };
                doc.Add(italicText);

                // Stopka
                doc.Add(new Paragraph(" ") { SpacingBefore = 20f });
                PdfPTable footerTable = new PdfPTable(1);
                footerTable.WidthPercentage = 100;
                PdfPCell footerCell = new PdfPCell { Border = PdfPCell.TOP_BORDER, BorderColorTop = lightGreenColor, BorderWidthTop = 1f, PaddingTop = 10f };
                footerCell.AddElement(new Paragraph($"Wygenerowano: {DateTime.Now:dd.MM.yyyy HH:mm}", new Font(baseFont, 7, Font.ITALIC, BaseColor.GRAY)) { Alignment = Element.ALIGN_CENTER });
                footerTable.AddCell(footerCell);
                doc.Add(footerTable);

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
        private string _typCeny;
        private bool _piK;
        private decimal _ubytek;

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
            set { _ch = value; OnPropertyChanged(nameof(CH)); }
        }

        public int NW
        {
            get => _nw;
            set { _nw = value; OnPropertyChanged(nameof(NW)); }
        }

        public int ZM
        {
            get => _zm;
            set { _zm = value; OnPropertyChanged(nameof(ZM)); }
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

        // Obliczona wartość - Cena * NettoUbojni
        // Gdy PiK = true (padłe i konfiskaty wliczone) - nie odejmujemy nic
        // Gdy PiK = false - padłe i konfiskaty są odejmowane (normalne zachowanie)
        public decimal Wartosc
        {
            get
            {
                // Podstawowa wartość: Cena * NettoUbojni
                decimal wartoscBazowa = Cena * NettoUbojniValue;

                // Uwzględnienie ubytku
                decimal ubytek = wartoscBazowa * (Ubytek / 100);
                decimal wartoscPoUbytku = wartoscBazowa - ubytek;

                return Math.Round(wartoscPoUbytku, 0);
            }
        }

        public void RecalculateWartosc()
        {
            OnPropertyChanged(nameof(Wartosc));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
