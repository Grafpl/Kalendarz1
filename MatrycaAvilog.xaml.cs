using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kalendarz1
{
    public partial class MatrycaAvilog : Window
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private ZapytaniaSQL zapytaniasql = new ZapytaniaSQL();
        private DataTable dataTable;

        public MatrycaAvilog()
        {
            InitializeComponent();
            InitializeWindow();
            LoadData();
        }

        private void InitializeWindow()
        {
            // Ustaw dzisiejszą datę
            datePickerMain.SelectedDate = DateTime.Today;
        }

        private void LoadData()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    DateTime selectedDate = datePickerMain.SelectedDate ?? DateTime.Today;

                    // Pobierz listę kierowców
                    string driverQuery = @"
                        SELECT GID, [Name]
                        FROM [LibraNet].[dbo].[Driver]
                        WHERE Deleted = 0
                        ORDER BY Name ASC";

                    SqlDataAdapter driverAdapter = new SqlDataAdapter(driverQuery, connection);
                    DataTable driverTable = new DataTable();
                    driverAdapter.Fill(driverTable);

                    // Tabela CarID
                    string carQuery = @"
                        SELECT DISTINCT ID
                        FROM dbo.CarTrailer
                        WHERE kind = '1'
                        ORDER BY ID DESC";

                    SqlDataAdapter carAdapter = new SqlDataAdapter(carQuery, connection);
                    DataTable carTable = new DataTable();
                    carAdapter.Fill(carTable);

                    // Tabela TrailerID
                    string trailerQuery = @"
                        SELECT DISTINCT ID
                        FROM dbo.CarTrailer
                        WHERE kind = '2'
                        ORDER BY ID DESC";

                    SqlDataAdapter trailerAdapter = new SqlDataAdapter(trailerQuery, connection);
                    DataTable trailerTable = new DataTable();
                    trailerAdapter.Fill(trailerTable);

                    // Tabela Wózek
                    DataTable wozekTable = new DataTable();
                    wozekTable.Columns.Add("WozekValue", typeof(string));
                    wozekTable.Rows.Add("");
                    wozekTable.Rows.Add("Wieziesz wozek");
                    wozekTable.Rows.Add("Przywozisz wozek");
                    wozekTable.Rows.Add("Wozek w obie strony");

                    // Sprawdź czy są dane w FarmerCalc
                    string checkQuery = @"
                        SELECT COUNT(*)
                        FROM [LibraNet].[dbo].[FarmerCalc]
                        WHERE CalcDate = @SelectedDate";

                    SqlCommand checkCommand = new SqlCommand(checkQuery, connection);
                    checkCommand.Parameters.AddWithValue("@SelectedDate", selectedDate.Date);
                    int count = (int)checkCommand.ExecuteScalar();

                    bool isFarmerCalc = count > 0;

                    if (isFarmerCalc)
                    {
                        // Dane z FarmerCalc
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
                        command.Parameters.AddWithValue("@SelectedDate", selectedDate.Date);
                        SqlDataAdapter adapter = new SqlDataAdapter(command);
                        dataTable = new DataTable();
                        adapter.Fill(dataTable);
                    }
                    else
                    {
                        // Dane z HarmonogramDostaw
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
                        command.Parameters.AddWithValue("@StartDate", selectedDate.Date);
                        SqlDataAdapter adapter = new SqlDataAdapter(command);
                        dataTable = new DataTable();
                        adapter.Fill(dataTable);
                    }

                    if (dataTable.Rows.Count == 0)
                    {
                        MessageBox.Show(
                            "Brak danych do wyświetlenia na wybrany dzień.\n\n" +
                            "Możliwe przyczyny:\n" +
                            "• Brak potwierdzonych dostaw w Harmonogramie\n" +
                            "• Nieprawidłowa data\n" +
                            "• Dane jeszcze nie zostały wprowadzone",
                            "Informacja",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);

                        // Utwórz pustą tabelę
                        CreateEmptyDataTable();
                    }

                    // Konfiguracja DataGrid
                    ConfigureDataGrid(driverTable, carTable, trailerTable, wozekTable, isFarmerCalc);
                    dataGridMatryca.ItemsSource = dataTable.DefaultView;
                    UpdateStatistics();
                }
            }
            catch (SqlException sqlEx)
            {
                MessageBox.Show(
                    $"Błąd połączenia z bazą danych:\n\n" +
                    $"Komunikat: {sqlEx.Message}\n" +
                    $"Numer błędu: {sqlEx.Number}\n\n" +
                    $"Sprawdź:\n" +
                    $"• Czy serwer SQL jest dostępny (192.168.0.109)\n" +
                    $"• Czy masz uprawnienia do bazy LibraNet\n" +
                    $"• Czy tabele FarmerCalc i HarmonogramDostaw istnieją",
                    "Błąd SQL",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Wystąpił błąd podczas ładowania danych:\n\n" +
                    $"Typ: {ex.GetType().Name}\n" +
                    $"Komunikat: {ex.Message}",
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void CreateEmptyDataTable()
        {
            dataTable = new DataTable();
            dataTable.Columns.Add("ID", typeof(long));
            dataTable.Columns.Add("LpDostawy", typeof(string));
            dataTable.Columns.Add("CustomerGID", typeof(string));
            dataTable.Columns.Add("WagaDek", typeof(decimal));
            dataTable.Columns.Add("SztPoj", typeof(int));
            dataTable.Columns.Add("DriverGID", typeof(string));
            dataTable.Columns.Add("CarID", typeof(string));
            dataTable.Columns.Add("TrailerID", typeof(string));
            dataTable.Columns.Add("Wyjazd", typeof(DateTime));
            dataTable.Columns.Add("Zaladunek", typeof(DateTime));
            dataTable.Columns.Add("Przyjazd", typeof(DateTime));
            dataTable.Columns.Add("NotkaWozek", typeof(string));
        }

        private void ConfigureDataGrid(DataTable driverTable, DataTable carTable, DataTable trailerTable, DataTable wozekTable, bool isFarmerCalc)
        {
            dataGridMatryca.Columns.Clear();

            // Ukryta kolumna ID
            DataGridTextColumn colID = new DataGridTextColumn
            {
                Header = "ID",
                Binding = new System.Windows.Data.Binding("ID"),
                Visibility = Visibility.Collapsed
            };
            dataGridMatryca.Columns.Add(colID);

            // LP Dostawy
            DataGridTextColumn colLp = new DataGridTextColumn
            {
                Header = "LP Dostawy",
                Binding = new System.Windows.Data.Binding("LpDostawy"),
                Width = new DataGridLength(100),
                IsReadOnly = true
            };
            dataGridMatryca.Columns.Add(colLp);

            // Hodowca
            DataGridTextColumn colCustomer = new DataGridTextColumn
            {
                Header = "Hodowca",
                Binding = new System.Windows.Data.Binding("CustomerGID"),
                Width = new DataGridLength(200)
            };
            dataGridMatryca.Columns.Add(colCustomer);

            // Waga (kg)
            DataGridTextColumn colWeight = new DataGridTextColumn
            {
                Header = "Waga (kg)",
                Binding = new System.Windows.Data.Binding("WagaDek") { StringFormat = "N2" },
                Width = new DataGridLength(100)
            };
            dataGridMatryca.Columns.Add(colWeight);

            // Sztuk
            DataGridTextColumn colPieces = new DataGridTextColumn
            {
                Header = "Sztuk",
                Binding = new System.Windows.Data.Binding("SztPoj") { StringFormat = "N0" },
                Width = new DataGridLength(100)
            };
            dataGridMatryca.Columns.Add(colPieces);

            // Kierowca (ComboBox)
            DataGridComboBoxColumn colDriver = new DataGridComboBoxColumn
            {
                Header = "Kierowca",
                SelectedValueBinding = new System.Windows.Data.Binding("DriverGID"),
                SelectedValuePath = "GID",
                DisplayMemberPath = "Name",
                ItemsSource = driverTable.DefaultView,
                Width = new DataGridLength(180)
            };
            dataGridMatryca.Columns.Add(colDriver);

            // Ciągnik (ComboBox)
            DataGridComboBoxColumn colCar = new DataGridComboBoxColumn
            {
                Header = "Ciągnik",
                SelectedValueBinding = new System.Windows.Data.Binding("CarID"),
                SelectedValuePath = "ID",
                DisplayMemberPath = "ID",
                ItemsSource = carTable.DefaultView,
                Width = new DataGridLength(120)
            };
            dataGridMatryca.Columns.Add(colCar);

            // Naczepa (ComboBox)
            DataGridComboBoxColumn colTrailer = new DataGridComboBoxColumn
            {
                Header = "Naczepa",
                SelectedValueBinding = new System.Windows.Data.Binding("TrailerID"),
                SelectedValuePath = "ID",
                DisplayMemberPath = "ID",
                ItemsSource = trailerTable.DefaultView,
                Width = new DataGridLength(120)
            };
            dataGridMatryca.Columns.Add(colTrailer);

            // Wyjazd
            DataGridTextColumn colWyjazd = new DataGridTextColumn
            {
                Header = "Wyjazd",
                Binding = new System.Windows.Data.Binding("Wyjazd") { StringFormat = "HH:mm" },
                Width = new DataGridLength(100)
            };
            dataGridMatryca.Columns.Add(colWyjazd);

            // Załadunek
            DataGridTextColumn colZaladunek = new DataGridTextColumn
            {
                Header = "Załadunek",
                Binding = new System.Windows.Data.Binding("Zaladunek") { StringFormat = "HH:mm" },
                Width = new DataGridLength(100)
            };
            dataGridMatryca.Columns.Add(colZaladunek);

            // Przyjazd
            DataGridTextColumn colPrzyjazd = new DataGridTextColumn
            {
                Header = "Przyjazd",
                Binding = new System.Windows.Data.Binding("Przyjazd") { StringFormat = "HH:mm" },
                Width = new DataGridLength(100)
            };
            dataGridMatryca.Columns.Add(colPrzyjazd);

            // Wózek (ComboBox)
            DataGridComboBoxColumn colWozek = new DataGridComboBoxColumn
            {
                Header = "Wózek",
                SelectedValueBinding = new System.Windows.Data.Binding("NotkaWozek"),
                SelectedValuePath = "WozekValue",
                DisplayMemberPath = "WozekValue",
                ItemsSource = wozekTable.DefaultView,
                Width = new DataGridLength(180)
            };
            dataGridMatryca.Columns.Add(colWozek);
        }

        private void UpdateStatistics()
        {
            try
            {
                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    lblRecordCount.Text = "0";
                    lblTotalWeight.Text = "0";
                    lblTotalPieces.Text = "0";
                    return;
                }

                int recordCount = dataTable.Rows.Count;
                decimal totalWeight = 0;
                int totalPieces = 0;

                foreach (DataRow row in dataTable.Rows)
                {
                    if (row["WagaDek"] != null && row["WagaDek"] != DBNull.Value)
                    {
                        if (decimal.TryParse(row["WagaDek"].ToString(), out decimal weight))
                        {
                            totalWeight += weight;
                        }
                    }

                    if (row["SztPoj"] != null && row["SztPoj"] != DBNull.Value)
                    {
                        if (int.TryParse(row["SztPoj"].ToString(), out int pieces))
                        {
                            totalPieces += pieces;
                        }
                    }
                }

                lblRecordCount.Text = recordCount.ToString("N0");
                lblTotalWeight.Text = totalWeight.ToString("N0");
                lblTotalPieces.Text = totalPieces.ToString("N0");
            }
            catch (Exception ex)
            {
                lblRecordCount.Text = "Błąd";
                lblTotalWeight.Text = "Błąd";
                lblTotalPieces.Text = "Błąd";

                System.Diagnostics.Debug.WriteLine($"Błąd w UpdateStatistics: {ex.Message}");
            }
        }

        // Event Handlers
        private void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadData();
        }

        private void BtnPreviousDay_Click(object sender, RoutedEventArgs e)
        {
            if (datePickerMain.SelectedDate.HasValue)
            {
                datePickerMain.SelectedDate = datePickerMain.SelectedDate.Value.AddDays(-1);
            }
        }

        private void BtnNextDay_Click(object sender, RoutedEventArgs e)
        {
            if (datePickerMain.SelectedDate.HasValue)
            {
                datePickerMain.SelectedDate = datePickerMain.SelectedDate.Value.AddDays(1);
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        private void BtnMoveUp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (dataGridMatryca.SelectedIndex > 0)
                {
                    int selectedIndex = dataGridMatryca.SelectedIndex;
                    DataRow row = dataTable.Rows[selectedIndex];
                    DataRow newRow = dataTable.NewRow();
                    newRow.ItemArray = row.ItemArray;

                    dataTable.Rows.RemoveAt(selectedIndex);
                    dataTable.Rows.InsertAt(newRow, selectedIndex - 1);

                    dataGridMatryca.SelectedIndex = selectedIndex - 1;
                    dataGridMatryca.ScrollIntoView(dataGridMatryca.SelectedItem);
                }
                else
                {
                    MessageBox.Show(
                        "Proszę zaznaczyć wiersz do przesunięcia lub wybrany wiersz jest już na górze.",
                        "Informacja",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Błąd podczas przesuwania wiersza w górę:\n{ex.Message}",
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BtnMoveDown_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (dataGridMatryca.SelectedIndex >= 0 && dataGridMatryca.SelectedIndex < dataTable.Rows.Count - 1)
                {
                    int selectedIndex = dataGridMatryca.SelectedIndex;
                    DataRow row = dataTable.Rows[selectedIndex];
                    DataRow newRow = dataTable.NewRow();
                    newRow.ItemArray = row.ItemArray;

                    dataTable.Rows.RemoveAt(selectedIndex);
                    dataTable.Rows.InsertAt(newRow, selectedIndex + 1);

                    dataGridMatryca.SelectedIndex = selectedIndex + 1;
                    dataGridMatryca.ScrollIntoView(dataGridMatryca.SelectedItem);
                }
                else
                {
                    MessageBox.Show(
                        "Proszę zaznaczyć wiersz do przesunięcia lub wybrany wiersz jest już na dole.",
                        "Informacja",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Błąd podczas przesuwania wiersza w dół:\n{ex.Message}",
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BtnAddRow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (dataTable != null)
                {
                    DataRow newRow = dataTable.NewRow();
                    dataTable.Rows.Add(newRow);
                    UpdateStatistics();

                    // Zaznacz nowy wiersz
                    dataGridMatryca.SelectedIndex = dataTable.Rows.Count - 1;
                    dataGridMatryca.ScrollIntoView(dataGridMatryca.SelectedItem);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Błąd podczas dodawania wiersza:\n{ex.Message}",
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BtnDeleteRow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (dataGridMatryca.SelectedIndex >= 0)
                {
                    MessageBoxResult result = MessageBox.Show(
                        "Czy na pewno chcesz usunąć zaznaczony wiersz?",
                        "Potwierdzenie usunięcia",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        dataTable.Rows.RemoveAt(dataGridMatryca.SelectedIndex);
                        UpdateStatistics();
                    }
                }
                else
                {
                    MessageBox.Show(
                        "Proszę zaznaczyć wiersz do usunięcia.",
                        "Informacja",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Błąd podczas usuwania wiersza:\n{ex.Message}",
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BtnSaveToDatabase_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult confirmResult = MessageBox.Show(
                "Czy na pewno chcesz zapisać dane do bazy?\n\nOperacja nadpisze istniejące dane dla tego dnia.",
                "Potwierdzenie zapisu",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmResult != MessageBoxResult.Yes)
                return;

            try
            {
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

                            foreach (DataRow row in dataTable.Rows)
                            {
                                string Dostawca = row["CustomerGID"]?.ToString() ?? "";
                                string Kierowca = row["DriverGID"]?.ToString() ?? "";
                                string LpDostawy = row["LpDostawy"]?.ToString() ?? "";
                                string SztPoj = row["SztPoj"]?.ToString() ?? "";
                                string WagaDek = row["WagaDek"]?.ToString() ?? "";
                                string Ciagnik = row["CarID"]?.ToString() ?? "";
                                string Naczepa = row["TrailerID"]?.ToString() ?? "";
                                string NotkaWozek = row["NotkaWozek"]?.ToString() ?? "";

                                string StringPrzyjazd = row["Przyjazd"]?.ToString() ?? "";
                                string StringZaladunek = row["Zaladunek"]?.ToString() ?? "";
                                string StringWyjazd = row["Wyjazd"]?.ToString() ?? "";

                                // Pobierz dodatkowe dane
                                double Ubytek = 0.0;
                                if (!string.IsNullOrWhiteSpace(LpDostawy))
                                {
                                    double.TryParse(zapytaniasql.PobierzInformacjeZBazyDanychKonkretne(LpDostawy, "Ubytek"), out Ubytek);
                                }

                                double Cena = 0.0;
                                if (!string.IsNullOrWhiteSpace(LpDostawy))
                                {
                                    double.TryParse(zapytaniasql.PobierzInformacjeZBazyDanychKonkretne(LpDostawy, "Cena"), out Cena);
                                }

                                string typCeny = zapytaniasql.PobierzInformacjeZBazyDanychKonkretne(LpDostawy, "TypCeny");
                                int intTypCeny = zapytaniasql.ZnajdzIdCeny(typCeny);

                                int userId = zapytaniasql.ZnajdzIdKierowcy(Kierowca);
                                int userId2 = zapytaniasql.ZnajdzIdHodowcy(Dostawca);

                                // Formatowanie godzin
                                StringWyjazd = zapytaniasql.DodajDwukropek(StringWyjazd);
                                StringZaladunek = zapytaniasql.DodajDwukropek(StringZaladunek);
                                StringPrzyjazd = zapytaniasql.DodajDwukropek(StringPrzyjazd);

                                DateTime data = datePickerMain.SelectedDate ?? DateTime.Today;
                                DateTime combinedDateTimeWyjazd = ZapytaniaSQL.CombineDateAndTime(StringWyjazd, data);
                                DateTime combinedDateTimeZaladunek = ZapytaniaSQL.CombineDateAndTime(StringZaladunek, data);
                                DateTime combinedDateTimePrzyjazd = ZapytaniaSQL.CombineDateAndTime(StringPrzyjazd, data);

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
                                    cmd.Parameters.AddWithValue("@Kierowca", userId);
                                    cmd.Parameters.AddWithValue("@LpDostawy", string.IsNullOrEmpty(LpDostawy) ? DBNull.Value : LpDostawy);
                                    cmd.Parameters.AddWithValue("@SztPoj", string.IsNullOrEmpty(SztPoj) ? DBNull.Value : decimal.Parse(SztPoj));
                                    cmd.Parameters.AddWithValue("@WagaDek", string.IsNullOrEmpty(WagaDek) ? DBNull.Value : decimal.Parse(WagaDek));
                                    cmd.Parameters.AddWithValue("@Date", data);

                                    cmd.Parameters.AddWithValue("@Wyjazd", combinedDateTimeWyjazd);
                                    cmd.Parameters.AddWithValue("@Zaladunek", combinedDateTimeZaladunek);
                                    cmd.Parameters.AddWithValue("@Przyjazd", combinedDateTimePrzyjazd);

                                    cmd.Parameters.AddWithValue("@Cena", Cena);
                                    cmd.Parameters.AddWithValue("@Ubytek", Ubytek);
                                    cmd.Parameters.AddWithValue("@TypCeny", intTypCeny);

                                    cmd.Parameters.AddWithValue("@Ciagnik", string.IsNullOrEmpty(Ciagnik) ? DBNull.Value : Ciagnik);
                                    cmd.Parameters.AddWithValue("@Naczepa", string.IsNullOrEmpty(Naczepa) ? DBNull.Value : Naczepa);
                                    cmd.Parameters.AddWithValue("@NotkaWozek", string.IsNullOrEmpty(NotkaWozek) ? DBNull.Value : NotkaWozek);

                                    cmd.ExecuteNonQuery();
                                    savedCount++;
                                }
                            }

                            transaction.Commit();

                            MessageBox.Show(
                                $"✓ Pomyślnie zapisano {savedCount} rekordów do bazy danych.",
                                "Sukces",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);

                            LoadData();
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();

                            MessageBox.Show(
                                $"Wystąpił błąd podczas zapisywania danych:\n\n{ex.Message}",
                                "Błąd",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Wystąpił błąd połączenia z bazą danych:\n\n{ex.Message}",
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Możesz tutaj dodać dodatkową logikę przy zmianie zaznaczenia
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
