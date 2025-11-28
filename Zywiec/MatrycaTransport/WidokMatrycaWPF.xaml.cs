using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
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
                    }

                    if (table.Rows.Count == 0)
                    {
                        UpdateStatus("Brak danych dla wybranej daty");
                        UpdateStatistics();
                        return;
                    }

                    // Konwertuj na ObservableCollection
                    foreach (DataRow row in table.Rows)
                    {
                        string customerGID = row["CustomerGID"]?.ToString() ?? "";
                        string hodowcaNazwa = "";
                        string hodowcaAdres = "";
                        string hodowcaMiejscowosc = "";
                        string hodowcaOdleglosc = "";

                        // Pobierz dane hodowcy
                        if (!string.IsNullOrEmpty(customerGID))
                        {
                            string idDostawcy = isFarmerCalc ? customerGID : zapytaniasql.ZnajdzIdHodowcyString(customerGID);
                            hodowcaNazwa = isFarmerCalc
                                ? zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(customerGID, "ShortName")
                                : customerGID;
                            hodowcaAdres = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(idDostawcy, "address");
                            hodowcaMiejscowosc = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(idDostawcy, "city");
                            hodowcaOdleglosc = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(idDostawcy, "distance");
                        }

                        var matrycaRow = new MatrycaRow
                        {
                            ID = row["ID"] != DBNull.Value ? Convert.ToInt64(row["ID"]) : 0,
                            LpDostawy = row["LpDostawy"]?.ToString() ?? "",
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
                            IsFarmerCalc = isFarmerCalc
                        };

                        matrycaData.Add(matrycaRow);
                    }

                    UpdateStatus($"Załadowano {table.Rows.Count} rekordów" + (isFarmerCalc ? " (z FarmerCalc)" : " (z Harmonogramu)"));
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
            // Sortuj według odległości
            var sortedList = matrycaData
                .OrderBy(r =>
                {
                    if (decimal.TryParse(r.Odleglosc?.Replace(" km", "").Replace(",", "."), out decimal dist))
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

            // Znajdź wiersz pod kursorem
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

        #region Selection & Specyfikacja

        private void DataGridMatryca_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedMatrycaRow = dataGridMatryca.SelectedItem as MatrycaRow;
            UpdateSpecyfikacjaPanel();
        }

        private void UpdateSpecyfikacjaPanel()
        {
            if (selectedMatrycaRow == null)
            {
                lblWybranaDostawaInfo.Text = "Wybierz dostawę w zakładce Matryca";
                ClearSpecyfikacjaFields();
                return;
            }

            // Aktualizuj nagłówek
            lblWybranaDostawaInfo.Text = $"LP: {selectedMatrycaRow.LpDostawy} | {selectedMatrycaRow.HodowcaNazwa}";

            // Dane hodowcy
            lblHodowcaNazwa.Text = selectedMatrycaRow.HodowcaNazwa ?? "-";
            lblHodowcaAdres.Text = selectedMatrycaRow.Adres ?? "-";
            lblHodowcaMiejscowosc.Text = selectedMatrycaRow.Miejscowosc ?? "-";
            lblHodowcaOdleglosc.Text = !string.IsNullOrEmpty(selectedMatrycaRow.Odleglosc)
                ? $"{selectedMatrycaRow.Odleglosc} km"
                : "-";

            // Dane transportowe
            lblKierowca.Text = GetKierowcaNazwa(selectedMatrycaRow.DriverGID);
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

        private void ClearSpecyfikacjaFields()
        {
            lblHodowcaNazwa.Text = "-";
            lblHodowcaAdres.Text = "-";
            lblHodowcaMiejscowosc.Text = "-";
            lblHodowcaOdleglosc.Text = "-";
            lblKierowca.Text = "-";
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

        private void BtnShowFullSpecyfikacja_Click(object sender, RoutedEventArgs e)
        {
            if (selectedMatrycaRow != null)
            {
                try
                {
                    // Otwórz pełne okno specyfikacji
                    WidokSpecyfikacje specWindow = new WidokSpecyfikacje();
                    specWindow.Show();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas otwierania specyfikacji:\n{ex.Message}",
                        "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Proszę wybrać dostawę.",
                    "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnPrintPDF_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Funkcja drukowania PDF - do zaimplementowania",
                "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
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
                                // Pobierz dodatkowe dane
                                double Ubytek = 0.0;
                                double Cena = 0.0;
                                int intTypCeny = -1;

                                if (!string.IsNullOrWhiteSpace(row.LpDostawy))
                                {
                                    double.TryParse(zapytaniasql.PobierzInformacjeZBazyDanychKonkretne(row.LpDostawy, "Ubytek"), out Ubytek);
                                    double.TryParse(zapytaniasql.PobierzInformacjeZBazyDanychKonkretne(row.LpDostawy, "Cena"), out Cena);
                                    string typCeny = zapytaniasql.PobierzInformacjeZBazyDanychKonkretne(row.LpDostawy, "TypCeny");
                                    intTypCeny = zapytaniasql.ZnajdzIdCeny(typCeny);
                                }

                                int userId2 = row.IsFarmerCalc
                                    ? (int.TryParse(row.CustomerGID, out int cid) ? cid : 0)
                                    : zapytaniasql.ZnajdzIdHodowcy(row.CustomerGID);

                                // Znajdź nowe ID
                                long maxLP;
                                string maxLPSql = "SELECT MAX(ID) AS MaxLP FROM dbo.[FarmerCalc];";
                                using (SqlCommand command = new SqlCommand(maxLPSql, conn, transaction))
                                {
                                    object result = command.ExecuteScalar();
                                    maxLP = result == DBNull.Value ? 1 : Convert.ToInt64(result) + 1;
                                }

                                // Wstaw do bazy
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
                            LoadData(); // Odśwież dane
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
                return;
            }

            lblRecordCount.Text = matrycaData.Count.ToString();
            lblTotalWeight.Text = $"{matrycaData.Sum(r => r.WagaDek * r.SztPoj):N0} kg";
            lblTotalPieces.Text = $"{matrycaData.Sum(r => r.SztPoj):N0}";
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
        private bool _isFarmerCalc;

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

        public bool IsFarmerCalc
        {
            get => _isFarmerCalc;
            set { _isFarmerCalc = value; OnPropertyChanged(nameof(IsFarmerCalc)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
