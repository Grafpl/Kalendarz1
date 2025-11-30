using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Kalendarz1
{
    public partial class WidokMatrycaWPF : Window
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private string connectionStringTransport = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private ZapytaniaSQL zapytaniasql = new ZapytaniaSQL();

        private ObservableCollection<MatrycaRow> matrycaData;
        private MatrycaRow selectedMatrycaRow;

        // Dla drag & drop
        private Point? dragStartPoint;
        private int draggedRowIndex = -1;

        // Źródła danych dla ComboBox
        private DataTable kierowcyTable;
        private DataTable ciagnikiTable;
        private DataTable naczepyTable;

        public WidokMatrycaWPF()
        {
            InitializeComponent();
            matrycaData = new ObservableCollection<MatrycaRow>();
            dataGridMatryca.ItemsSource = matrycaData;
            dateTimePicker1.SelectedDate = DateTime.Today;

            // Ustawienie ItemsSource dla ComboBox Wózek
            colWozek.ItemsSource = new List<string>
            {
                "",
                "Wieziesz wózek",
                "Przywozisz wózek",
                "Wózek w obie strony"
            };
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadComboBoxSources();
            LoadData();
            UpdateStatistics();
            UpdateDayOfWeekLabel();
            UpdateStatus("Dane załadowane pomyślnie");
        }

        #region Ładowanie danych

        private void LoadComboBoxSources()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Kierowcy
                    string driverQuery = @"SELECT GID, [Name] FROM [LibraNet].[dbo].[Driver] WHERE Deleted = 0 ORDER BY Name ASC";
                    SqlDataAdapter driverAdapter = new SqlDataAdapter(driverQuery, connection);
                    kierowcyTable = new DataTable();
                    driverAdapter.Fill(kierowcyTable);
                    colKierowca.ItemsSource = kierowcyTable.DefaultView;

                    // Ciągniki
                    string carQuery = @"SELECT DISTINCT ID FROM dbo.CarTrailer WHERE kind = '1' ORDER BY ID DESC";
                    SqlDataAdapter carAdapter = new SqlDataAdapter(carQuery, connection);
                    ciagnikiTable = new DataTable();
                    carAdapter.Fill(ciagnikiTable);
                    colCiagnik.ItemsSource = ciagnikiTable.DefaultView;

                    // Naczepy
                    string trailerQuery = @"SELECT DISTINCT ID FROM dbo.CarTrailer WHERE kind = '2' ORDER BY ID DESC";
                    SqlDataAdapter trailerAdapter = new SqlDataAdapter(trailerQuery, connection);
                    naczepyTable = new DataTable();
                    trailerAdapter.Fill(naczepyTable);
                    colNaczepa.ItemsSource = naczepyTable.DefaultView;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania danych słownikowych:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadData()
        {
            try
            {
                UpdateStatus("Ładowanie danych...");
                matrycaData.Clear();

                DateTime selectedDate = dateTimePicker1.SelectedDate ?? DateTime.Today;

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Sprawdź czy są dane w FarmerCalc
                    string checkQuery = @"SELECT COUNT(*) FROM [LibraNet].[dbo].[FarmerCalc] WHERE CalcDate = @SelectedDate";
                    SqlCommand checkCommand = new SqlCommand(checkQuery, connection);
                    checkCommand.Parameters.AddWithValue("@SelectedDate", selectedDate);
                    int count = (int)checkCommand.ExecuteScalar();

                    DataTable table = new DataTable();
                    bool isFarmerCalc = false;

                    if (count > 0)
                    {
                        // Dane z FarmerCalc (już zaplanowane)
                        string query = @"
                            SELECT
                                ID,
                                LpDostawy,
                                CustomerGID,
                                WagaDek,
                                SztPoj,
                                DriverGID,
                                CarID,
                                TrailerID,
                                Wyjazd,
                                Zaladunek,
                                Przyjazd,
                                NotkaWozek
                            FROM [LibraNet].[dbo].[FarmerCalc]
                            WHERE CalcDate = @SelectedDate
                            ORDER BY LpDostawy";

                        SqlCommand command = new SqlCommand(query, connection);
                        command.Parameters.AddWithValue("@SelectedDate", selectedDate);
                        SqlDataAdapter adapter = new SqlDataAdapter(command);
                        adapter.Fill(table);
                        isFarmerCalc = true;
                        lblDataSource.Text = "FarmerCalc";
                    }
                    else
                    {
                        // Dane z HarmonogramDostaw (nowe - z harmonogramu)
                        string query = @"
                            SELECT
                                CAST(0 AS BIGINT) AS ID,
                                Lp AS LpDostawy,
                                Dostawca AS CustomerGID,
                                WagaDek,
                                SztSzuflada AS SztPoj,
                                ISNULL(Auta, 1) AS Auta,
                                CAST(NULL AS INT) AS DriverGID,
                                CAST(NULL AS VARCHAR(50)) AS CarID,
                                CAST(NULL AS VARCHAR(50)) AS TrailerID,
                                CAST(NULL AS DATETIME) AS Wyjazd,
                                CAST(NULL AS DATETIME) AS Zaladunek,
                                CAST(NULL AS DATETIME) AS Przyjazd,
                                CAST(NULL AS VARCHAR(100)) AS NotkaWozek
                            FROM dbo.HarmonogramDostaw
                            WHERE DataOdbioru = @StartDate
                            AND Bufor = 'Potwierdzony'
                            ORDER BY Lp";

                        SqlCommand command = new SqlCommand(query, connection);
                        command.Parameters.AddWithValue("@StartDate", selectedDate);
                        SqlDataAdapter adapter = new SqlDataAdapter(command);
                        adapter.Fill(table);
                        lblDataSource.Text = "Harmonogram";
                    }

                    if (table.Rows.Count == 0)
                    {
                        UpdateStatus("Brak danych dla wybranej daty");
                        lblDataSource.Text = "Brak danych";
                        UpdateStatistics();
                        return;
                    }

                    // Konwertuj na ObservableCollection
                    int lpCounter = 1;

                    foreach (DataRow row in table.Rows)
                    {
                        string customerGID = row["CustomerGID"]?.ToString() ?? "";
                        string hodowcaNazwa = "";
                        string hodowcaAdres = "";
                        string hodowcaMiejscowosc = "";
                        string hodowcaOdleglosc = "";
                        string hodowcaTelefon = "";
                        string hodowcaEmail = "";

                        // Pobierz dane hodowcy
                        if (!string.IsNullOrEmpty(customerGID))
                        {
                            try
                            {
                                string idDostawcy = isFarmerCalc ? customerGID : zapytaniasql.ZnajdzIdHodowcyString(customerGID);

                                if (isFarmerCalc && !string.IsNullOrEmpty(customerGID))
                                {
                                    hodowcaNazwa = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(customerGID, "ShortName") ?? "";
                                }
                                else
                                {
                                    hodowcaNazwa = customerGID;
                                }

                                if (!string.IsNullOrEmpty(idDostawcy) && idDostawcy != "-1" && idDostawcy != "0")
                                {
                                    hodowcaAdres = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(idDostawcy, "address") ?? "";
                                    hodowcaMiejscowosc = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(idDostawcy, "city") ?? "";
                                    hodowcaOdleglosc = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(idDostawcy, "distance") ?? "";

                                    // Pobierz telefon (próbuj Phone1, Phone2, Phone3)
                                    hodowcaTelefon = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(idDostawcy, "Phone1") ?? "";
                                    if (string.IsNullOrWhiteSpace(hodowcaTelefon))
                                        hodowcaTelefon = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(idDostawcy, "Phone2") ?? "";
                                    if (string.IsNullOrWhiteSpace(hodowcaTelefon))
                                        hodowcaTelefon = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(idDostawcy, "Phone3") ?? "";

                                    // Pobierz email
                                    hodowcaEmail = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(idDostawcy, "email") ?? "";
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Błąd pobierania danych hodowcy {customerGID}: {ex.Message}");
                            }
                        }

                        int iloscAut = 1;
                        if (!isFarmerCalc && table.Columns.Contains("Auta"))
                        {
                            iloscAut = row["Auta"] != DBNull.Value ? Convert.ToInt32(row["Auta"]) : 1;
                            if (iloscAut < 1) iloscAut = 1;
                        }

                        string oryginalneLP = row["LpDostawy"]?.ToString() ?? "";

                        for (int autoNr = 0; autoNr < iloscAut; autoNr++)
                        {
                            var matrycaRow = new MatrycaRow
                            {
                                ID = row["ID"] != DBNull.Value ? Convert.ToInt64(row["ID"]) : 0,
                                LpDostawy = lpCounter.ToString(),
                                OryginalneLP = oryginalneLP,
                                CustomerGID = customerGID,
                                HodowcaNazwa = hodowcaNazwa,
                                WagaDek = row["WagaDek"] != DBNull.Value ? Convert.ToDecimal(row["WagaDek"]) : 0,
                                SztPoj = row["SztPoj"] != DBNull.Value ? Convert.ToInt32(row["SztPoj"]) : 0,
                                DriverGID = row["DriverGID"] != DBNull.Value ? Convert.ToInt32(row["DriverGID"]) : (int?)null,
                                CarID = row["CarID"]?.ToString(),
                                TrailerID = row["TrailerID"]?.ToString(),
                                Wyjazd = row["Wyjazd"] != DBNull.Value ? Convert.ToDateTime(row["Wyjazd"]) : (DateTime?)null,
                                Zaladunek = row["Zaladunek"] != DBNull.Value ? Convert.ToDateTime(row["Zaladunek"]) : (DateTime?)null,
                                Przyjazd = row["Przyjazd"] != DBNull.Value ? Convert.ToDateTime(row["Przyjazd"]) : (DateTime?)null,
                                NotkaWozek = row["NotkaWozek"]?.ToString(),
                                Adres = hodowcaAdres,
                                Miejscowosc = hodowcaMiejscowosc,
                                Odleglosc = hodowcaOdleglosc,
                                Telefon = hodowcaTelefon,
                                Email = hodowcaEmail,
                                IsFarmerCalc = isFarmerCalc,
                                AutoNrUHodowcy = autoNr + 1,
                                IloscAutUHodowcy = iloscAut
                            };

                            matrycaData.Add(matrycaRow);
                            lpCounter++;
                        }
                    }

                    UpdateStatus($"Załadowano {matrycaData.Count} wierszy" + (isFarmerCalc ? " (FarmerCalc)" : " (Harmonogram)"));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania danych:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatus("Błąd ładowania danych");
            }

            UpdateStatistics();
        }

        #endregion

        #region Event Handlers - Data

        private void DateTimePicker1_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dateTimePicker1.SelectedDate.HasValue)
            {
                LoadData();
                UpdateDayOfWeekLabel();
            }
        }

        private void UpdateDayOfWeekLabel()
        {
            if (dateTimePicker1.SelectedDate.HasValue)
            {
                var culture = new CultureInfo("pl-PL");
                string dayName = dateTimePicker1.SelectedDate.Value.ToString("dddd", culture);
                lblDayOfWeek.Text = char.ToUpper(dayName[0]) + dayName.Substring(1);
            }
        }

        private void BtnPreviousDay_Click(object sender, RoutedEventArgs e)
        {
            dateTimePicker1.SelectedDate = (dateTimePicker1.SelectedDate ?? DateTime.Today).AddDays(-1);
        }

        private void BtnNextDay_Click(object sender, RoutedEventArgs e)
        {
            dateTimePicker1.SelectedDate = (dateTimePicker1.SelectedDate ?? DateTime.Today).AddDays(1);
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        #endregion

        #region Event Handlers - Row Operations

        private void BtnAddRow_Click(object sender, RoutedEventArgs e)
        {
            var newRow = new MatrycaRow
            {
                LpDostawy = (matrycaData.Count + 1).ToString(),
                WagaDek = 0,
                SztPoj = 0
            };
            matrycaData.Add(newRow);
            UpdateStatistics();
            UpdateStatus("Dodano nowy wiersz");
        }

        private void BtnDeleteRow_Click(object sender, RoutedEventArgs e)
        {
            if (selectedMatrycaRow != null)
            {
                var result = MessageBox.Show(
                    "Czy na pewno chcesz usunąć zaznaczony wiersz?",
                    "Potwierdzenie usunięcia",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    matrycaData.Remove(selectedMatrycaRow);
                    RefreshNumeration();
                    UpdateStatistics();
                    UpdateStatus("Usunięto wiersz");
                }
            }
            else
            {
                MessageBox.Show("Proszę zaznaczyć wiersz do usunięcia.",
                    "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnMoveUp_Click(object sender, RoutedEventArgs e)
        {
            int index = dataGridMatryca.SelectedIndex;
            if (index > 0)
            {
                var item = matrycaData[index];
                matrycaData.RemoveAt(index);
                matrycaData.Insert(index - 1, item);
                dataGridMatryca.SelectedIndex = index - 1;
                RefreshNumeration();
                UpdateStatus("Wiersz przesunięty w górę");
            }
        }

        private void BtnMoveDown_Click(object sender, RoutedEventArgs e)
        {
            int index = dataGridMatryca.SelectedIndex;
            if (index >= 0 && index < matrycaData.Count - 1)
            {
                var item = matrycaData[index];
                matrycaData.RemoveAt(index);
                matrycaData.Insert(index + 1, item);
                dataGridMatryca.SelectedIndex = index + 1;
                RefreshNumeration();
                UpdateStatus("Wiersz przesunięty w dół");
            }
        }

        private void BtnSuggestOrder_Click(object sender, RoutedEventArgs e)
        {
            if (matrycaData.Count == 0)
            {
                MessageBox.Show("Brak danych do sortowania.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                "Funkcja sugerowania kolejności posortuje dostawy według zasad:\n\n" +
                "1. Najbliższe fermy (< 30 km) - na rano\n" +
                "2. Średnia odległość (30-60 km) - w środku dnia\n" +
                "3. Dalsze fermy (> 60 km) - później\n\n" +
                "Czy chcesz zastosować sugerowaną kolejność?",
                "Sugerowanie kolejności",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                SortByDistance();
                RefreshNumeration();
                UpdateStatus("Zastosowano sugerowaną kolejność");
            }
        }

        private void SortByDistance()
        {
            var sortedList = matrycaData
                .OrderBy(r =>
                {
                    if (decimal.TryParse(r.Odleglosc?.Replace(" km", "").Replace(",", "."),
                        NumberStyles.Any, CultureInfo.InvariantCulture, out decimal dist))
                        return dist;
                    return decimal.MaxValue;
                })
                .ToList();

            matrycaData.Clear();
            foreach (var item in sortedList)
            {
                matrycaData.Add(item);
            }
        }

        private void RefreshNumeration()
        {
            for (int i = 0; i < matrycaData.Count; i++)
            {
                matrycaData[i].LpDostawy = (i + 1).ToString();
            }
        }

        #endregion

        #region Drag & Drop

        private void DataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            dragStartPoint = e.GetPosition(null);

            var row = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);
            if (row != null)
            {
                draggedRowIndex = row.GetIndex();
            }
        }

        private void DataGrid_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (dragStartPoint.HasValue && e.LeftButton == MouseButtonState.Pressed)
            {
                Point position = e.GetPosition(null);
                Vector diff = dragStartPoint.Value - position;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    if (draggedRowIndex >= 0 && draggedRowIndex < matrycaData.Count)
                    {
                        var dragData = new DataObject("MatrycaRow", matrycaData[draggedRowIndex]);
                        DragDrop.DoDragDrop(dataGridMatryca, dragData, DragDropEffects.Move);
                    }
                }
            }
        }

        private void DataGrid_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("MatrycaRow"))
            {
                e.Effects = DragDropEffects.Move;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void DataGrid_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("MatrycaRow"))
            {
                var droppedData = e.Data.GetData("MatrycaRow") as MatrycaRow;
                var targetRow = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);

                if (droppedData != null && targetRow != null)
                {
                    int targetIndex = targetRow.GetIndex();
                    int sourceIndex = matrycaData.IndexOf(droppedData);

                    if (sourceIndex != targetIndex && sourceIndex >= 0 && targetIndex >= 0)
                    {
                        matrycaData.RemoveAt(sourceIndex);
                        if (targetIndex > sourceIndex) targetIndex--;
                        matrycaData.Insert(targetIndex, droppedData);

                        RefreshNumeration();
                        dataGridMatryca.SelectedIndex = targetIndex;
                        UpdateStatus("Zmieniono kolejność (drag & drop)");
                    }
                }
            }

            dragStartPoint = null;
            draggedRowIndex = -1;
        }

        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent)
                    return parent;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        #endregion

        #region Selection & Panel Update

        private void DataGridMatryca_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedMatrycaRow = dataGridMatryca.SelectedItem as MatrycaRow;
            UpdateInfoPanel();
        }

        private void UpdateInfoPanel()
        {
            if (selectedMatrycaRow == null)
            {
                ClearInfoPanel();
                return;
            }

            // Dane hodowcy
            lblHodowcaNazwa.Text = selectedMatrycaRow.HodowcaNazwa ?? "-";
            lblHodowcaAdres.Text = selectedMatrycaRow.Adres ?? "-";
            lblHodowcaMiejscowosc.Text = selectedMatrycaRow.Miejscowosc ?? "-";
            lblHodowcaOdleglosc.Text = !string.IsNullOrEmpty(selectedMatrycaRow.Odleglosc)
                ? $"{selectedMatrycaRow.Odleglosc} km"
                : "-";
            lblHodowcaTelefon.Text = !string.IsNullOrEmpty(selectedMatrycaRow.Telefon)
                ? selectedMatrycaRow.Telefon
                : "Brak telefonu";
            lblHodowcaEmail.Text = !string.IsNullOrEmpty(selectedMatrycaRow.Email)
                ? selectedMatrycaRow.Email
                : "Brak email";

            // Dane transportowe
            lblKierowca.Text = GetKierowcaNazwa(selectedMatrycaRow.DriverGID);
            lblKierowcaTelefon.Text = GetKierowcaTelefon(selectedMatrycaRow.DriverGID);
            lblCiagnik.Text = selectedMatrycaRow.CarID ?? "-";
            lblNaczepa.Text = selectedMatrycaRow.TrailerID ?? "-";
            lblWozek.Text = selectedMatrycaRow.NotkaWozek ?? "-";

            // Dane ilościowe
            lblSztukiDek.Text = $"{selectedMatrycaRow.SztPoj:N0} szt";
            lblWagaDek.Text = $"{selectedMatrycaRow.WagaDek:N2} kg";
            decimal wagaCalkowita = selectedMatrycaRow.WagaDek * selectedMatrycaRow.SztPoj;
            lblWagaCalkowita.Text = $"{wagaCalkowita:N0} kg";

            // Godziny
            lblWyjazd.Text = selectedMatrycaRow.Wyjazd?.ToString("HH:mm") ?? "-";
            lblZaladunek.Text = selectedMatrycaRow.Zaladunek?.ToString("HH:mm") ?? "-";
            lblPrzyjazd.Text = selectedMatrycaRow.Przyjazd?.ToString("HH:mm") ?? "-";
        }

        private void ClearInfoPanel()
        {
            lblHodowcaNazwa.Text = "-";
            lblHodowcaAdres.Text = "-";
            lblHodowcaMiejscowosc.Text = "-";
            lblHodowcaOdleglosc.Text = "-";
            lblHodowcaTelefon.Text = "-";
            lblHodowcaEmail.Text = "-";
            lblKierowca.Text = "-";
            lblKierowcaTelefon.Text = "-";
            lblCiagnik.Text = "-";
            lblNaczepa.Text = "-";
            lblWozek.Text = "-";
            lblSztukiDek.Text = "-";
            lblWagaDek.Text = "-";
            lblWagaCalkowita.Text = "-";
            lblWyjazd.Text = "-";
            lblZaladunek.Text = "-";
            lblPrzyjazd.Text = "-";
        }

        private string GetKierowcaNazwa(int? driverGID)
        {
            if (!driverGID.HasValue || kierowcyTable == null) return "-";

            var rows = kierowcyTable.Select($"GID = {driverGID.Value}");
            if (rows.Length > 0)
            {
                return rows[0]["Name"]?.ToString() ?? "-";
            }
            return "-";
        }

        private string GetKierowcaTelefon(int? driverGID)
        {
            if (!driverGID.HasValue) return "-";

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionStringTransport))
                {
                    conn.Open();
                    string query = "SELECT Telefon FROM dbo.Kierowcy WHERE GID = @GID";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@GID", driverGID.Value);
                        var result = cmd.ExecuteScalar();
                        return result?.ToString() ?? "-";
                    }
                }
            }
            catch
            {
                return "-";
            }
        }

        #endregion

        #region SMS Załadunek

        /// <summary>
        /// SMS zbiorczy - wszystkie auta dla tego hodowcy
        /// </summary>
        private void BtnSmsZaladunekAll_Click(object sender, RoutedEventArgs e)
        {
            if (selectedMatrycaRow == null)
            {
                MessageBox.Show("Proszę wybrać wiersz z matrycy.", "Informacja",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Sprawdź czy jest numer telefonu
            string phone = selectedMatrycaRow.Telefon;
            if (string.IsNullOrWhiteSpace(phone))
            {
                var openForm = MessageBox.Show(
                    $"Hodowca {selectedMatrycaRow.HodowcaNazwa} nie ma numeru telefonu.\n\n" +
                    "Czy chcesz otworzyć formularz edycji hodowcy?",
                    "Brak telefonu",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (openForm == MessageBoxResult.Yes)
                {
                    OpenHodowcaForm();
                }
                return;
            }

            // Znajdź wszystkie wiersze dla tego samego hodowcy
            string customerGID = selectedMatrycaRow.CustomerGID;
            var rowsForFarmer = matrycaData
                .Where(r => r.CustomerGID == customerGID && r.Zaladunek.HasValue)
                .OrderBy(r => r.Zaladunek)
                .ToList();

            if (rowsForFarmer.Count == 0)
            {
                MessageBox.Show("Brak godzin załadunku dla tego hodowcy.\nProszę uzupełnić godziny załadunku.",
                    "Brak danych", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DateTime selectedDate = dateTimePicker1.SelectedDate ?? DateTime.Today;

            // Buduj SMS zbiorczy
            var smsLines = new List<string>();
            smsLines.Add($"Piorkowscy {selectedDate:dd.MM}");

            foreach (var row in rowsForFarmer)
            {
                string zaladunekTime = row.Zaladunek.Value.ToString("HH:mm");
                string ciagnikNr = row.CarID ?? "";
                string naczepaNr = row.TrailerID ?? "";

                string autoInfo = $"Załadunek godz.{zaladunekTime}";
                if (!string.IsNullOrEmpty(ciagnikNr))
                    autoInfo += $" ciągnik:{ciagnikNr}";
                if (!string.IsNullOrEmpty(naczepaNr))
                    autoInfo += $" naczepa:{naczepaNr}";

                smsLines.Add(autoInfo);
            }

            // Średnia waga (WagaDek) dla wszystkich aut tego hodowcy
            decimal sredniaWaga = rowsForFarmer.Average(r => r.WagaDek);
            smsLines.Add($"Razem: {rowsForFarmer.Count} aut, śr.waga:{sredniaWaga:N2}kg");

            string smsContent = string.Join("\n", smsLines);

            // Kopiuj do schowka
            try
            {
                Clipboard.SetText(smsContent);

                string displayMessage = $"SMS ZBIORCZY skopiowany do schowka:\n\n" +
                                       $"Do: {phone}\n" +
                                       $"Hodowca: {selectedMatrycaRow.HodowcaNazwa}\n" +
                                       $"Liczba aut: {rowsForFarmer.Count}\n\n" +
                                       $"Treść:\n{smsContent}\n\n" +
                                       $"Wklej w SMS Desktop i wyślij.";

                MessageBox.Show(displayMessage, "SMS Załadunek - Wszystkie Auta", MessageBoxButton.OK, MessageBoxImage.Information);
                UpdateStatus($"SMS zbiorczy ({rowsForFarmer.Count} aut) - {selectedMatrycaRow.HodowcaNazwa}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd kopiowania do schowka: {ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// SMS pojedynczy - tylko wybrane auto
        /// </summary>
        private void BtnSmsZaladunekOne_Click(object sender, RoutedEventArgs e)
        {
            if (selectedMatrycaRow == null)
            {
                MessageBox.Show("Proszę wybrać wiersz z matrycy.", "Informacja",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string phone = selectedMatrycaRow.Telefon;
            if (string.IsNullOrWhiteSpace(phone))
            {
                var openForm = MessageBox.Show(
                    $"Hodowca {selectedMatrycaRow.HodowcaNazwa} nie ma numeru telefonu.\n\n" +
                    "Czy chcesz otworzyć formularz edycji hodowcy?",
                    "Brak telefonu",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (openForm == MessageBoxResult.Yes)
                {
                    OpenHodowcaForm();
                }
                return;
            }

            if (!selectedMatrycaRow.Zaladunek.HasValue)
            {
                MessageBox.Show("Brak godziny załadunku dla tego wiersza.\nProszę uzupełnić godzinę załadunku.",
                    "Brak danych", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DateTime selectedDate = dateTimePicker1.SelectedDate ?? DateTime.Today;
            string zaladunekTime = selectedMatrycaRow.Zaladunek.Value.ToString("HH:mm");
            string ciagnikNr = selectedMatrycaRow.CarID ?? "";
            string naczepaNr = selectedMatrycaRow.TrailerID ?? "";
            decimal sredniaWaga = selectedMatrycaRow.WagaDek;

            // Generuj treść SMS
            string smsContent = $"Piorkowscy {selectedDate:dd.MM}: Załadunek godz.{zaladunekTime}";
            if (!string.IsNullOrEmpty(ciagnikNr))
                smsContent += $" ciągnik:{ciagnikNr}";
            if (!string.IsNullOrEmpty(naczepaNr))
                smsContent += $" naczepa:{naczepaNr}";
            smsContent += $" śr.waga:{sredniaWaga:N2}kg";

            try
            {
                Clipboard.SetText(smsContent);

                string displayMessage = $"SMS skopiowany do schowka:\n\n" +
                                       $"Do: {phone}\n" +
                                       $"Auto: {selectedMatrycaRow.AutoNrUHodowcy}/{selectedMatrycaRow.IloscAutUHodowcy}\n" +
                                       $"Treść:\n{smsContent}\n\n" +
                                       $"Wklej w SMS Desktop i wyślij.";

                MessageBox.Show(displayMessage, "SMS Załadunek - To Auto", MessageBoxButton.OK, MessageBoxImage.Information);
                UpdateStatus($"SMS pojedynczy (auto {selectedMatrycaRow.AutoNrUHodowcy}/{selectedMatrycaRow.IloscAutUHodowcy}) - {selectedMatrycaRow.HodowcaNazwa}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd kopiowania do schowka: {ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Edycja Hodowcy

        private void BtnEdytujHodowce_Click(object sender, RoutedEventArgs e)
        {
            if (selectedMatrycaRow == null)
            {
                MessageBox.Show("Proszę wybrać wiersz z matrycy.", "Informacja",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            OpenHodowcaForm();
        }

        private void OpenHodowcaForm()
        {
            if (selectedMatrycaRow == null) return;

            try
            {
                string customerGID = selectedMatrycaRow.CustomerGID;
                string idDostawcy = selectedMatrycaRow.IsFarmerCalc
                    ? customerGID
                    : zapytaniasql.ZnajdzIdHodowcyString(customerGID);

                if (string.IsNullOrEmpty(idDostawcy) || idDostawcy == "-1" || idDostawcy == "0")
                {
                    MessageBox.Show("Nie można znaleźć ID hodowcy w bazie danych.",
                        "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // HodowcaForm wymaga string idKontrahenta i string appUser
                var form = new HodowcaForm(idDostawcy, Environment.UserName);
                form.ShowDialog();

                // Po zamknięciu formularza odśwież dane
                LoadData();
                UpdateStatus($"Dane hodowcy zaktualizowane");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas otwierania formularza hodowcy:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Specyfikacja

        private void BtnShowFullSpecyfikacja_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                WidokSpecyfikacje specWindow = new WidokSpecyfikacje();
                if (dateTimePicker1.SelectedDate.HasValue)
                {
                    // Przekaż datę do okna specyfikacji jeśli to możliwe
                }
                specWindow.Show();
                UpdateStatus("Otwarto okno specyfikacji");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas otwierania specyfikacji:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Save to Database

        private void BtnSaveToDatabase_Click(object sender, RoutedEventArgs e)
        {
            var confirmResult = MessageBox.Show(
                "Czy na pewno chcesz zapisać dane do bazy?\n\nOperacja nadpisze istniejące dane dla tego dnia.",
                "Potwierdzenie zapisu",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmResult != MessageBoxResult.Yes)
                return;

            try
            {
                UpdateStatus("Zapisywanie danych...");

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string sql = @"INSERT INTO dbo.FarmerCalc
                        (ID, CalcDate, CustomerGID, CustomerRealGID, DriverGID, LpDostawy, SztPoj, WagaDek,
                         CarID, TrailerID, NotkaWozek, Wyjazd, Zaladunek, Przyjazd, Price,
                         Loss, PriceTypeID)
                        VALUES
                        (@ID, @Date, @Dostawca, @Dostawca, @Kierowca, @LpDostawy, @SztPoj, @WagaDek,
                         @Ciagnik, @Naczepa, @NotkaWozek, @Wyjazd, @Zaladunek,
                         @Przyjazd, @Cena, @Ubytek, @TypCeny)";

                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            int savedCount = 0;
                            DateTime selectedDate = dateTimePicker1.SelectedDate ?? DateTime.Today;

                            foreach (var row in matrycaData)
                            {
                                double Ubytek = 0.0;
                                double Cena = 0.0;
                                int intTypCeny = -1;

                                string lpDoZapytan = !string.IsNullOrEmpty(row.OryginalneLP) ? row.OryginalneLP : row.LpDostawy;

                                if (!string.IsNullOrWhiteSpace(lpDoZapytan))
                                {
                                    try
                                    {
                                        double.TryParse(zapytaniasql.PobierzInformacjeZBazyDanychKonkretne(lpDoZapytan, "Ubytek"), out Ubytek);
                                        double.TryParse(zapytaniasql.PobierzInformacjeZBazyDanychKonkretne(lpDoZapytan, "Cena"), out Cena);
                                        string typCeny = zapytaniasql.PobierzInformacjeZBazyDanychKonkretne(lpDoZapytan, "TypCeny");

                                        if (!string.IsNullOrWhiteSpace(typCeny))
                                        {
                                            intTypCeny = zapytaniasql.ZnajdzIdCeny(typCeny);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Błąd pobierania danych dla LP {lpDoZapytan}: {ex.Message}");
                                    }
                                }

                                int userId2 = row.IsFarmerCalc
                                    ? (int.TryParse(row.CustomerGID, out int cid) ? cid : 0)
                                    : zapytaniasql.ZnajdzIdHodowcy(row.CustomerGID);

                                long maxLP;
                                string maxLPSql = "SELECT MAX(ID) AS MaxLP FROM dbo.[FarmerCalc];";
                                using (SqlCommand command = new SqlCommand(maxLPSql, conn, transaction))
                                {
                                    object result = command.ExecuteScalar();
                                    maxLP = result == DBNull.Value ? 1 : Convert.ToInt64(result) + 1;
                                }

                                using (SqlCommand cmd = new SqlCommand(sql, conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@ID", maxLP);
                                    cmd.Parameters.AddWithValue("@Dostawca", userId2);
                                    cmd.Parameters.AddWithValue("@Kierowca", row.DriverGID ?? (object)DBNull.Value);
                                    cmd.Parameters.AddWithValue("@LpDostawy", string.IsNullOrEmpty(row.LpDostawy) ? DBNull.Value : row.LpDostawy);
                                    cmd.Parameters.AddWithValue("@SztPoj", row.SztPoj);
                                    cmd.Parameters.AddWithValue("@WagaDek", row.WagaDek);
                                    cmd.Parameters.AddWithValue("@Date", selectedDate);

                                    cmd.Parameters.AddWithValue("@Wyjazd", row.Wyjazd ?? (object)DBNull.Value);
                                    cmd.Parameters.AddWithValue("@Zaladunek", row.Zaladunek ?? (object)DBNull.Value);
                                    cmd.Parameters.AddWithValue("@Przyjazd", row.Przyjazd ?? (object)DBNull.Value);

                                    cmd.Parameters.AddWithValue("@Cena", Cena);
                                    cmd.Parameters.AddWithValue("@Ubytek", Ubytek);
                                    cmd.Parameters.AddWithValue("@TypCeny", intTypCeny);

                                    cmd.Parameters.AddWithValue("@Ciagnik", string.IsNullOrEmpty(row.CarID) ? DBNull.Value : row.CarID);
                                    cmd.Parameters.AddWithValue("@Naczepa", string.IsNullOrEmpty(row.TrailerID) ? DBNull.Value : row.TrailerID);
                                    cmd.Parameters.AddWithValue("@NotkaWozek", string.IsNullOrEmpty(row.NotkaWozek) ? DBNull.Value : row.NotkaWozek);

                                    cmd.ExecuteNonQuery();
                                    savedCount++;
                                }
                            }

                            transaction.Commit();

                            MessageBox.Show(
                                $"Pomyślnie zapisano {savedCount} rekordów do bazy danych.",
                                "Sukces",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);

                            UpdateStatus($"Zapisano {savedCount} rekordów");
                            LoadData();
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            throw new Exception($"Błąd podczas zapisu transakcji: {ex.Message}", ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Wystąpił błąd podczas zapisywania danych:\n\n{ex.Message}",
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                UpdateStatus("Błąd zapisu");
            }
        }

        #endregion

        #region Statistics & Status

        private void UpdateStatistics()
        {
            if (matrycaData == null || matrycaData.Count == 0)
            {
                lblRecordCount.Text = "0";
                lblTotalWeight.Text = "0 kg";
                lblTotalPieces.Text = "0";
                lblHodowcyCount.Text = "0";
                lblTotalKm.Text = "0 km";
                return;
            }

            // Liczba dostaw
            lblRecordCount.Text = matrycaData.Count.ToString();

            // Suma wagi
            lblTotalWeight.Text = $"{matrycaData.Sum(r => r.WagaDek * r.SztPoj):N0} kg";

            // Suma sztuk
            lblTotalPieces.Text = $"{matrycaData.Sum(r => r.SztPoj):N0}";

            // Liczba unikalnych hodowców
            int uniqueHodowcy = matrycaData.Select(r => r.CustomerGID).Distinct().Count();
            lblHodowcyCount.Text = uniqueHodowcy.ToString();

            // Suma kilometrów
            decimal totalKm = 0;
            foreach (var row in matrycaData)
            {
                if (!string.IsNullOrEmpty(row.Odleglosc))
                {
                    string kmStr = row.Odleglosc.Replace(" km", "").Replace(",", ".");
                    if (decimal.TryParse(kmStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal km))
                    {
                        totalKm += km;
                    }
                }
            }
            lblTotalKm.Text = $"{totalKm:N0} km";
        }

        private void UpdateStatus(string message)
        {
            lblStatus.Text = message;
        }

        #endregion
    }

    /// <summary>
    /// Model danych dla wiersza matrycy transportu
    /// </summary>
    public class MatrycaRow : INotifyPropertyChanged
    {
        private long _id;
        private string _lpDostawy;
        private string _customerGID;
        private string _hodowcaNazwa;
        private decimal _wagaDek;
        private int _sztPoj;
        private int? _driverGID;
        private string _carID;
        private string _trailerID;
        private DateTime? _wyjazd;
        private DateTime? _zaladunek;
        private DateTime? _przyjazd;
        private string _notkaWozek;
        private string _adres;
        private string _miejscowosc;
        private string _odleglosc;
        private string _telefon;
        private string _email;
        private bool _isFarmerCalc;
        private int _autoNrUHodowcy;
        private int _iloscAutUHodowcy;
        private string _oryginalneLP;

        public long ID
        {
            get => _id;
            set { _id = value; OnPropertyChanged(nameof(ID)); }
        }

        public string LpDostawy
        {
            get => _lpDostawy;
            set { _lpDostawy = value; OnPropertyChanged(nameof(LpDostawy)); }
        }

        public string CustomerGID
        {
            get => _customerGID;
            set { _customerGID = value; OnPropertyChanged(nameof(CustomerGID)); }
        }

        public string HodowcaNazwa
        {
            get => _hodowcaNazwa;
            set { _hodowcaNazwa = value; OnPropertyChanged(nameof(HodowcaNazwa)); }
        }

        public decimal WagaDek
        {
            get => _wagaDek;
            set { _wagaDek = value; OnPropertyChanged(nameof(WagaDek)); }
        }

        public int SztPoj
        {
            get => _sztPoj;
            set { _sztPoj = value; OnPropertyChanged(nameof(SztPoj)); }
        }

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

        public DateTime? Wyjazd
        {
            get => _wyjazd;
            set { _wyjazd = value; OnPropertyChanged(nameof(Wyjazd)); }
        }

        public DateTime? Zaladunek
        {
            get => _zaladunek;
            set { _zaladunek = value; OnPropertyChanged(nameof(Zaladunek)); }
        }

        public DateTime? Przyjazd
        {
            get => _przyjazd;
            set { _przyjazd = value; OnPropertyChanged(nameof(Przyjazd)); }
        }

        public string NotkaWozek
        {
            get => _notkaWozek;
            set { _notkaWozek = value; OnPropertyChanged(nameof(NotkaWozek)); }
        }

        public string Adres
        {
            get => _adres;
            set { _adres = value; OnPropertyChanged(nameof(Adres)); }
        }

        public string Miejscowosc
        {
            get => _miejscowosc;
            set { _miejscowosc = value; OnPropertyChanged(nameof(Miejscowosc)); }
        }

        public string Odleglosc
        {
            get => _odleglosc;
            set { _odleglosc = value; OnPropertyChanged(nameof(Odleglosc)); }
        }

        public string Telefon
        {
            get => _telefon;
            set { _telefon = value; OnPropertyChanged(nameof(Telefon)); }
        }

        public string Email
        {
            get => _email;
            set { _email = value; OnPropertyChanged(nameof(Email)); }
        }

        public bool IsFarmerCalc
        {
            get => _isFarmerCalc;
            set { _isFarmerCalc = value; OnPropertyChanged(nameof(IsFarmerCalc)); }
        }

        public int AutoNrUHodowcy
        {
            get => _autoNrUHodowcy;
            set { _autoNrUHodowcy = value; OnPropertyChanged(nameof(AutoNrUHodowcy)); }
        }

        public int IloscAutUHodowcy
        {
            get => _iloscAutUHodowcy;
            set { _iloscAutUHodowcy = value; OnPropertyChanged(nameof(IloscAutUHodowcy)); }
        }

        public string OryginalneLP
        {
            get => _oryginalneLP;
            set { _oryginalneLP = value; OnPropertyChanged(nameof(OryginalneLP)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
