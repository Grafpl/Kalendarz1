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
using System.Windows.Data;
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
        private DataTable hodowcyTable;

        // Dostawy z harmonogramu (bufor='Potwierdzone')
        private ObservableCollection<HarmonogramDostawaItem> harmonogramDostawy;

        public WidokMatrycaWPF()
        {
            InitializeComponent();
            matrycaData = new ObservableCollection<MatrycaRow>();
            harmonogramDostawy = new ObservableCollection<HarmonogramDostawaItem>();
            dataGridMatryca.ItemsSource = matrycaData;
            cmbHarmonogramDostawy.ItemsSource = harmonogramDostawy;
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
            LoadHarmonogramDostawy();
            UpdateDayOfWeekLabel();
            UpdateStatistics();
            UpdateStatus("Wybierz datę i kliknij 'WCZYTAJ Z BAZY' lub zaimportuj dane z PDF/Excel");
        }

        #region Ładowanie danych

        private void LoadComboBoxSources()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Hodowcy
                    string hodowcaQuery = @"SELECT DISTINCT ID, Name FROM dbo.DOSTAWCY WHERE halt = '0' ORDER BY Name ASC";
                    SqlDataAdapter hodowcaAdapter = new SqlDataAdapter(hodowcaQuery, connection);
                    hodowcyTable = new DataTable();
                    hodowcaAdapter.Fill(hodowcyTable);
                    colHodowca.ItemsSource = hodowcyTable.DefaultView;

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

        /// <summary>
        /// Ładuje dostawy z harmonogramu (bufor = 'Potwierdzone') dla wybranej daty
        /// </summary>
        private void LoadHarmonogramDostawy()
        {
            try
            {
                harmonogramDostawy.Clear();
                DateTime selectedDate = dateTimePicker1.SelectedDate ?? DateTime.Today;

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    string query = @"SELECT
                                        LP,
                                        Dostawca,
                                        SztukiDek,
                                        WagaDek,
                                        ISNULL(Cena, 0) as Cena,
                                        ISNULL(Ubytek, 0) as Ubytek,
                                        ISNULL(typCeny, '') as TypCeny
                                    FROM [LibraNet].[dbo].[HarmonogramDostaw]
                                    WHERE DataOdbioru = @SelectedDate
                                    AND Bufor IN ('Potwierdzony', 'Potwierdzone')
                                    ORDER BY LP";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@SelectedDate", selectedDate);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int lp = reader["LP"] != DBNull.Value ? Convert.ToInt32(reader["LP"]) : 0;
                                string dostawca = reader["Dostawca"]?.ToString()?.Trim() ?? "";
                                int sztuki = reader["SztukiDek"] != DBNull.Value ? Convert.ToInt32(reader["SztukiDek"]) : 0;
                                decimal waga = reader["WagaDek"] != DBNull.Value ? Convert.ToDecimal(reader["WagaDek"]) : 0;
                                decimal cena = reader["Cena"] != DBNull.Value ? Convert.ToDecimal(reader["Cena"]) : 0;
                                decimal ubytek = reader["Ubytek"] != DBNull.Value ? Convert.ToDecimal(reader["Ubytek"]) : 0;
                                string typCeny = reader["TypCeny"]?.ToString()?.Trim() ?? "";

                                harmonogramDostawy.Add(new HarmonogramDostawaItem
                                {
                                    LP = lp,
                                    Dostawca = dostawca,
                                    SztukiDek = sztuki,
                                    WagaDek = waga,
                                    Cena = cena,
                                    Ubytek = ubytek,
                                    TypCeny = typCeny,
                                    DisplayText = $"LP:{lp} - {dostawca} (Szt:{sztuki}, Cena:{cena:N2}, Ubytek:{ubytek:N2}%)"
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd ładowania harmonogramu: {ex.Message}");
            }
        }

        /// <summary>
        /// Obsługa zmiany wybranej dostawy z harmonogramu
        /// </summary>
        private void CmbHarmonogramDostawy_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Nic nie robimy przy zmianie - czekamy na kliknięcie przycisku "Zastosuj cenę"
        }

        /// <summary>
        /// Zastosuj cenę, ubytek i typ ceny z wybranej dostawy do wszystkich wierszy tego dostawcy
        /// </summary>
        private void BtnApplyHarmonogramPrice_Click(object sender, RoutedEventArgs e)
        {
            var selectedDostawa = cmbHarmonogramDostawy.SelectedItem as HarmonogramDostawaItem;
            if (selectedDostawa == null)
            {
                MessageBox.Show("Wybierz dostawę z harmonogramu.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string dostawcaNazwa = selectedDostawa.Dostawca?.Trim().ToLowerInvariant() ?? "";
            if (string.IsNullOrEmpty(dostawcaNazwa))
            {
                MessageBox.Show("Wybrana dostawa nie ma nazwy dostawcy.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Znajdź wszystkie wiersze z tym samym dostawcą
            int count = 0;
            foreach (var row in matrycaData)
            {
                string rowDostawca = row.HodowcaNazwa?.Trim().ToLowerInvariant() ?? "";
                if (rowDostawca == dostawcaNazwa)
                {
                    row.Price = selectedDostawa.Cena;
                    row.Loss = selectedDostawa.Ubytek;
                    row.PriceTypeName = selectedDostawa.TypCeny;
                    row.HarmonogramLP = selectedDostawa.LP;
                    // Pobierz PriceTypeID z nazwy
                    row.PriceTypeID = zapytaniasql.ZnajdzIdCeny(selectedDostawa.TypCeny);
                    count++;
                }
            }

            if (count > 0)
            {
                UpdateStatus($"Zastosowano cenę do {count} wierszy dostawcy: {selectedDostawa.Dostawca}");
            }
            else
            {
                MessageBox.Show($"Nie znaleziono wierszy dla dostawcy: {selectedDostawa.Dostawca}\n\nUżyj przycisku 'Wklej ręcznie' aby zastosować do zaznaczonego wiersza.",
                    "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// Ręczne wklejenie ceny z wybranej dostawy do zaznaczonego wiersza i wszystkich z tym samym dostawcą
        /// </summary>
        private void BtnApplyHarmonogramPriceManual_Click(object sender, RoutedEventArgs e)
        {
            var selectedDostawa = cmbHarmonogramDostawy.SelectedItem as HarmonogramDostawaItem;
            if (selectedDostawa == null)
            {
                MessageBox.Show("Wybierz dostawę z harmonogramu.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var selectedRow = dataGridMatryca.SelectedItem as MatrycaRow;
            if (selectedRow == null)
            {
                MessageBox.Show("Zaznacz wiersz w tabeli do którego chcesz wkleić cenę.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Pytanie: tylko zaznaczony wiersz czy wszyscy o tej samej nazwie dostawcy?
            var result = MessageBox.Show(
                $"Zastosować cenę z dostawy LP:{selectedDostawa.LP} ({selectedDostawa.Dostawca}):\n" +
                $"  Cena: {selectedDostawa.Cena:N2}\n" +
                $"  Ubytek: {selectedDostawa.Ubytek:N2}%\n" +
                $"  Typ: {selectedDostawa.TypCeny}\n\n" +
                $"TAK = Tylko zaznaczony wiersz ({selectedRow.HodowcaNazwa})\n" +
                $"NIE = Wszystkie wiersze o nazwie: {selectedRow.HodowcaNazwa}",
                "Ręczne wklejenie ceny",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel)
                return;

            int count = 0;
            string targetDostawca = selectedRow.HodowcaNazwa?.Trim().ToLowerInvariant() ?? "";

            if (result == MessageBoxResult.Yes)
            {
                // Tylko zaznaczony wiersz
                selectedRow.Price = selectedDostawa.Cena;
                selectedRow.Loss = selectedDostawa.Ubytek;
                selectedRow.PriceTypeName = selectedDostawa.TypCeny;
                selectedRow.HarmonogramLP = selectedDostawa.LP;
                selectedRow.PriceTypeID = zapytaniasql.ZnajdzIdCeny(selectedDostawa.TypCeny);
                count = 1;
            }
            else
            {
                // Wszystkie wiersze o tej samej nazwie dostawcy
                foreach (var row in matrycaData)
                {
                    string rowDostawca = row.HodowcaNazwa?.Trim().ToLowerInvariant() ?? "";
                    if (rowDostawca == targetDostawca)
                    {
                        row.Price = selectedDostawa.Cena;
                        row.Loss = selectedDostawa.Ubytek;
                        row.PriceTypeName = selectedDostawa.TypCeny;
                        row.HarmonogramLP = selectedDostawa.LP;
                        row.PriceTypeID = zapytaniasql.ZnajdzIdCeny(selectedDostawa.TypCeny);
                        count++;
                    }
                }
            }

            UpdateStatus($"Zastosowano cenę do {count} wierszy");
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
                                NotkaWozek,
                                ISNULL(Price, 0) AS Price,
                                ISNULL(Loss, 0) AS Loss,
                                ISNULL(PriceTypeID, -1) AS PriceTypeID,
                                Number,
                                YearNumber,
                                CarLp
                            FROM [LibraNet].[dbo].[FarmerCalc]
                            WHERE CalcDate = @SelectedDate
                            ORDER BY CarLp, LpDostawy";

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

                            // Wczytaj dane cenowe jeśli są z FarmerCalc
                            if (isFarmerCalc)
                            {
                                matrycaRow.Price = table.Columns.Contains("Price") && row["Price"] != DBNull.Value
                                    ? Convert.ToDecimal(row["Price"]) : 0;
                                matrycaRow.Loss = table.Columns.Contains("Loss") && row["Loss"] != DBNull.Value
                                    ? Convert.ToDecimal(row["Loss"]) : 0;
                                matrycaRow.PriceTypeID = table.Columns.Contains("PriceTypeID") && row["PriceTypeID"] != DBNull.Value
                                    ? Convert.ToInt32(row["PriceTypeID"]) : (int?)null;
                                matrycaRow.Number = table.Columns.Contains("Number") && row["Number"] != DBNull.Value
                                    ? Convert.ToInt32(row["Number"]) : (int?)null;
                            }

                            matrycaData.Add(matrycaRow);
                            lpCounter++;
                        }
                    }

                    UpdateStatus($"Załadowano {matrycaData.Count} wierszy" + (isFarmerCalc ? " (FarmerCalc)" : " (Harmonogram)"));
                }

                // Załaduj historię SMS dla aktualnych danych
                LoadSmsHistory();
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
                // Tylko aktualizuj dzień tygodnia - dane wczytaj ręcznie przyciskiem
                UpdateDayOfWeekLabel();

                // Załaduj dostawy z harmonogramu dla nowej daty
                LoadHarmonogramDostawy();

                // Wyczyść matrycę i pokaż informację
                if (matrycaData.Count > 0)
                {
                    // Zapytaj czy wyczyścić obecne dane
                    var result = MessageBox.Show(
                        $"Zmieniono datę na {dateTimePicker1.SelectedDate:dd.MM.yyyy}.\n\n" +
                        "Czy chcesz wyczyścić obecne dane?\n" +
                        "(Kliknij 'WCZYTAJ Z BAZY' aby załadować dane dla nowej daty)",
                        "Zmiana daty",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        matrycaData.Clear();
                        UpdateStatistics();
                        lblDataSource.Text = "-";
                    }
                }

                UpdateStatus($"Data: {dateTimePicker1.SelectedDate:dd.MM.yyyy} - kliknij 'WCZYTAJ Z BAZY' lub zaimportuj dane");
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
            // Odśwież = wczytaj z bazy
            BtnLoadFromDatabase_Click(sender, e);
        }

        #endregion

        #region Import AVILOG

        private void BtnImportAvilog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Sprawdź czy są już dane w matrycy
                if (matrycaData.Count > 0)
                {
                    var result = MessageBox.Show(
                        "W matrycy są już dane. Import z AVILOG zastąpi wszystkie obecne wiersze.\n\n" +
                        "Czy chcesz kontynuować?",
                        "Potwierdzenie",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result != MessageBoxResult.Yes)
                        return;
                }

                // Otwórz okno importu
                var importWindow = new ImportAvilogWindow();
                importWindow.Owner = this;

                if (importWindow.ShowDialog() == true && importWindow.ImportSuccess)
                {
                    // Aktualizuj datę jeśli różna
                    if (importWindow.ImportedDate.HasValue)
                    {
                        dateTimePicker1.SelectedDate = importWindow.ImportedDate.Value;
                    }

                    // Wyczyść obecne dane i załaduj zaimportowane
                    matrycaData.Clear();

                    int lpCounter = 1;
                    DateTime selectedDate = dateTimePicker1.SelectedDate ?? DateTime.Today;

                    foreach (var importRow in importWindow.ImportedRows)
                    {
                        // Pobierz dane hodowcy z bazy
                        string hodowcaNazwa = "";
                        string hodowcaAdres = "";
                        string hodowcaMiejscowosc = "";
                        string hodowcaOdleglosc = "";
                        string hodowcaTelefon = "";
                        string hodowcaEmail = "";

                        if (!string.IsNullOrEmpty(importRow.MappedHodowcaGID))
                        {
                            string idDostawcy = importRow.MappedHodowcaGID;
                            hodowcaNazwa = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(idDostawcy, "ShortName") ?? "";
                            hodowcaAdres = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(idDostawcy, "address") ?? "";
                            hodowcaMiejscowosc = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(idDostawcy, "city") ?? "";
                            hodowcaOdleglosc = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(idDostawcy, "distance") ?? "";
                            hodowcaTelefon = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(idDostawcy, "Phone1") ?? "";
                            hodowcaEmail = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(idDostawcy, "email") ?? "";
                        }

                        // Przygotuj godziny z daty importu
                        DateTime? wyjazd = null;
                        DateTime? zaladunek = null;
                        DateTime? przyjazd = null;

                        if (importRow.WyjazdZaklad.HasValue)
                        {
                            wyjazd = selectedDate.Date.Add(importRow.WyjazdZaklad.Value.TimeOfDay);
                        }
                        if (importRow.PoczatekZaladunku.HasValue)
                        {
                            zaladunek = selectedDate.Date.Add(importRow.PoczatekZaladunku.Value);
                        }
                        if (importRow.PowrotZaklad.HasValue)
                        {
                            przyjazd = selectedDate.Date.Add(importRow.PowrotZaklad.Value.TimeOfDay);
                        }

                        var matrycaRow = new MatrycaRow
                        {
                            ID = 0,
                            LpDostawy = lpCounter.ToString(),
                            CustomerGID = importRow.MappedHodowcaGID ?? "",
                            HodowcaNazwa = hodowcaNazwa,
                            WagaDek = importRow.WagaDek,
                            SztPoj = importRow.Sztuki, // Dla PDF używamy całkowitej liczby sztuk
                            DriverGID = importRow.MappedKierowcaGID,
                            CarID = importRow.MappedCiagnikID ?? importRow.Ciagnik,
                            TrailerID = importRow.MappedNaczepaID ?? importRow.Naczepa,
                            Wyjazd = wyjazd,
                            Zaladunek = zaladunek,
                            Przyjazd = przyjazd,
                            NotkaWozek = importRow.Obserwacje,
                            Adres = hodowcaAdres,
                            Miejscowosc = hodowcaMiejscowosc,
                            Odleglosc = hodowcaOdleglosc,
                            Telefon = hodowcaTelefon,
                            Email = hodowcaEmail,
                            IsFarmerCalc = false,
                            AutoNrUHodowcy = 1,
                            IloscAutUHodowcy = 1
                        };

                        matrycaData.Add(matrycaRow);
                        lpCounter++;
                    }

                    UpdateStatistics();
                    UpdateStatus($"Zaimportowano {matrycaData.Count} wierszy z AVILOG. Sprawdź dane i zapisz do bazy.");

                    MessageBox.Show(
                        $"Pomyślnie zaimportowano {matrycaData.Count} wierszy z planu AVILOG.\n\n" +
                        "Dane zostały wczytane do matrycy. Sprawdź poprawność i kliknij \"ZAPISZ DO BAZY\" aby zapisać.",
                        "Import zakończony",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas importu z AVILOG:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatus("Błąd importu AVILOG");
            }
        }

        private void BtnImportExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Sprawdź czy są już dane w matrycy
                if (matrycaData.Count > 0)
                {
                    var result = MessageBox.Show(
                        "W matrycy są już dane. Import z Excel zastąpi wszystkie obecne wiersze.\n\n" +
                        "Czy chcesz kontynuować?",
                        "Potwierdzenie",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result != MessageBoxResult.Yes)
                        return;
                }

                // Otwórz okno importu Excel
                var importWindow = new ImportExcelWindow();
                importWindow.Owner = this;

                if (importWindow.ShowDialog() == true && importWindow.ImportSuccess)
                {
                    // Aktualizuj datę jeśli różna
                    if (importWindow.ImportedDate.HasValue)
                    {
                        dateTimePicker1.SelectedDate = importWindow.ImportedDate.Value;
                    }

                    // Wyczyść obecne dane i załaduj zaimportowane
                    matrycaData.Clear();

                    int lpCounter = 1;
                    DateTime selectedDate = dateTimePicker1.SelectedDate ?? DateTime.Today;

                    foreach (var importRow in importWindow.ImportedRows)
                    {
                        // Pobierz dane hodowcy z bazy
                        string hodowcaNazwa = "";
                        string hodowcaAdres = "";
                        string hodowcaMiejscowosc = "";
                        string hodowcaOdleglosc = "";
                        string hodowcaTelefon = "";
                        string hodowcaEmail = "";

                        if (!string.IsNullOrEmpty(importRow.MappedHodowcaGID))
                        {
                            string idDostawcy = importRow.MappedHodowcaGID;
                            hodowcaNazwa = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(idDostawcy, "ShortName") ?? "";
                            hodowcaAdres = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(idDostawcy, "address") ?? "";
                            hodowcaMiejscowosc = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(idDostawcy, "city") ?? "";
                            hodowcaOdleglosc = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(idDostawcy, "distance") ?? "";
                            hodowcaTelefon = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(idDostawcy, "Phone1") ?? "";
                            hodowcaEmail = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(idDostawcy, "email") ?? "";
                        }

                        // Przygotuj godziny z daty importu
                        DateTime? wyjazd = null;
                        DateTime? zaladunek = null;
                        DateTime? przyjazd = null;

                        if (importRow.WyjazdZaklad.HasValue)
                        {
                            wyjazd = selectedDate.Date.Add(importRow.WyjazdZaklad.Value.TimeOfDay);
                        }
                        if (importRow.GodzinaZaladunku.HasValue)
                        {
                            zaladunek = selectedDate.Date.Add(importRow.GodzinaZaladunku.Value);
                        }
                        if (importRow.PowrotZaklad.HasValue)
                        {
                            przyjazd = selectedDate.Date.Add(importRow.PowrotZaklad.Value.TimeOfDay);
                        }

                        var matrycaRow = new MatrycaRow
                        {
                            ID = 0,
                            LpDostawy = lpCounter.ToString(),
                            CustomerGID = importRow.MappedHodowcaGID ?? "",
                            HodowcaNazwa = hodowcaNazwa,
                            WagaDek = importRow.WagaDek,
                            SztPoj = importRow.SztukiNaSkrzynke, // Liczba sztuk na skrzynkę (z "16 x 264" -> 16)
                            DriverGID = importRow.MappedKierowcaGID,
                            CarID = !string.IsNullOrEmpty(importRow.MappedCiagnikID) ? importRow.MappedCiagnikID : importRow.Ciagnik,
                            TrailerID = !string.IsNullOrEmpty(importRow.MappedNaczepaID) ? importRow.MappedNaczepaID : importRow.Naczepa,
                            Wyjazd = wyjazd,
                            Zaladunek = zaladunek,
                            Przyjazd = przyjazd,
                            NotkaWozek = importRow.Obserwacje,
                            Adres = hodowcaAdres,
                            Miejscowosc = hodowcaMiejscowosc,
                            Odleglosc = hodowcaOdleglosc,
                            Telefon = hodowcaTelefon,
                            Email = hodowcaEmail,
                            IsFarmerCalc = false,
                            AutoNrUHodowcy = 1,
                            IloscAutUHodowcy = 1
                        };

                        matrycaData.Add(matrycaRow);
                        lpCounter++;
                    }

                    UpdateStatistics();
                    UpdateStatus($"Zaimportowano {matrycaData.Count} wierszy z Excel. Sprawdź dane i zapisz do bazy.");

                    MessageBox.Show(
                        $"Pomyślnie zaimportowano {matrycaData.Count} wierszy z planu Excel AVILOG.\n\n" +
                        "Dane zostały wczytane do matrycy. Sprawdź poprawność i kliknij \"ZAPISZ DO BAZY\" aby zapisać.",
                        "Import zakończony",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas importu z Excel:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatus("Błąd importu Excel");
            }
        }

        private void BtnLoadFromDatabase_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Sprawdź czy są niezapisane dane
                if (matrycaData.Count > 0)
                {
                    var result = MessageBox.Show(
                        "W matrycy są już dane. Wczytanie z bazy zastąpi wszystkie obecne wiersze.\n\n" +
                        "Czy chcesz kontynuować?",
                        "Potwierdzenie",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result != MessageBoxResult.Yes)
                        return;
                }

                // Wczytaj dane z bazy
                LoadData();
                UpdateStatistics();

                if (matrycaData.Count > 0)
                {
                    UpdateStatus($"Wczytano {matrycaData.Count} wierszy z bazy dla {dateTimePicker1.SelectedDate:dd.MM.yyyy}");
                    lblDataSource.Text = "Baza danych";
                }
                else
                {
                    UpdateStatus($"Brak danych w bazie dla {dateTimePicker1.SelectedDate:dd.MM.yyyy}. Zaimportuj dane z PDF/Excel.");
                    lblDataSource.Text = "Brak danych";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas wczytywania z bazy:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatus("Błąd wczytywania z bazy");
            }
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

                // Sprawdź czy SMS był wysłany - wymagaj potwierdzenia
                if (!CheckAndWarnAboutSmsChange(item))
                    return;

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

                // Sprawdź czy SMS był wysłany - wymagaj potwierdzenia
                if (!CheckAndWarnAboutSmsChange(item))
                    return;

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

            // Sprawdź czy są wysłane SMSy - wymagaj potwierdzenia dla wszystkich
            if (!CheckAndWarnAboutSmsChangeForAll())
                return;

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

        /// <summary>
        /// Sprawdza czy SMS został wysłany dla danego wiersza i wyświetla ostrzeżenie
        /// Zwraca true jeśli można kontynuować zmianę, false jeśli anulowano
        /// </summary>
        private bool CheckAndWarnAboutSmsChange(MatrycaRow row)
        {
            if (row == null || !row.SmsSent)
                return true; // Nie wysłano SMS - można kontynuować bez ostrzeżenia

            // Znajdź wszystkich hodowców z tym samym CustomerGID którzy mają wysłany SMS
            var affectedRows = matrycaData
                .Where(r => r.CustomerGID == row.CustomerGID && r.SmsSent)
                .ToList();

            string smsInfo = row.SmsDataWyslania.HasValue
                ? $"wysłano {row.SmsDataWyslania.Value:dd.MM.yyyy HH:mm} przez {row.SmsUserId}"
                : "wysłano wcześniej";

            var result = MessageBox.Show(
                $"⚠️ UWAGA! SMS został już wysłany do hodowcy!\n\n" +
                $"Hodowca: {row.HodowcaNazwa}\n" +
                $"SMS: {smsInfo}\n\n" +
                $"Zmieniasz kolejność/plan dla tego hodowcy.\n\n" +
                $"❗ MUSISZ wysłać POPRAWKĘ do hodowcy!\n\n" +
                $"Czy potwierdzasz, że wyślesz poprawkę SMS do hodowcy\n" +
                $"z nową godziną załadunku?",
                "⚠️ WYMAGANA POPRAWKA SMS",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                // Zapisz log o zaakceptowaniu zmiany
                LogSmsChangeAcknowledgment(row, "CHANGE_ACKNOWLEDGED");
                UpdateStatus($"⚠️ Zmiana zaakceptowana - wyślij poprawkę do {row.HodowcaNazwa}!");
                return true;
            }

            UpdateStatus("Zmiana anulowana - SMS już wysłany");
            return false;
        }

        /// <summary>
        /// Sprawdza czy są jakiekolwiek wysłane SMSy i wyświetla ostrzeżenie
        /// Używane przy sortowaniu całej listy
        /// </summary>
        private bool CheckAndWarnAboutSmsChangeForAll()
        {
            var rowsWithSms = matrycaData.Where(r => r.SmsSent).ToList();
            if (rowsWithSms.Count == 0)
                return true; // Brak wysłanych SMS - można kontynuować

            var hodowcyZSms = rowsWithSms
                .Select(r => r.HodowcaNazwa)
                .Distinct()
                .ToList();

            var result = MessageBox.Show(
                $"⚠️ UWAGA! SMSy zostały już wysłane do {hodowcyZSms.Count} hodowców!\n\n" +
                $"Hodowcy z wysłanym SMS:\n" +
                $"{string.Join("\n", hodowcyZSms.Take(5))}" +
                (hodowcyZSms.Count > 5 ? $"\n... i {hodowcyZSms.Count - 5} więcej" : "") +
                $"\n\n" +
                $"Zmiana kolejności może wymagać wysłania POPRAWEK!\n\n" +
                $"❗ Czy potwierdzasz, że wyślesz poprawki SMS\n" +
                $"do wszystkich hodowców, których to dotyczy?",
                "⚠️ WYMAGANE POPRAWKI SMS",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                foreach (var row in rowsWithSms)
                {
                    LogSmsChangeAcknowledgment(row, "BULK_CHANGE_ACKNOWLEDGED");
                }
                UpdateStatus($"⚠️ Zmiana zaakceptowana - wyślij poprawki do {hodowcyZSms.Count} hodowców!");
                return true;
            }

            UpdateStatus("Zmiana anulowana - SMSy już wysłane");
            return false;
        }

        /// <summary>
        /// Zapisuje log o zaakceptowaniu zmiany po wysłaniu SMS
        /// </summary>
        private void LogSmsChangeAcknowledgment(MatrycaRow row, string changeType)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Upewnij się że tabela istnieje
                    string createTableSql = @"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SmsChangeLog')
                        CREATE TABLE dbo.SmsChangeLog (
                            ID BIGINT IDENTITY(1,1) PRIMARY KEY,
                            CalcDate DATE NOT NULL,
                            CustomerGID NVARCHAR(50) NOT NULL,
                            HodowcaNazwa NVARCHAR(200) NULL,
                            ChangeType NVARCHAR(50) NOT NULL,
                            AcknowledgedDate DATETIME NOT NULL,
                            AcknowledgedByUser NVARCHAR(100) NOT NULL,
                            OriginalSmsDate DATETIME NULL,
                            OriginalSmsUser NVARCHAR(100) NULL
                        )";
                    using (SqlCommand createCmd = new SqlCommand(createTableSql, conn))
                    {
                        createCmd.ExecuteNonQuery();
                    }

                    DateTime selectedDate = dateTimePicker1.SelectedDate ?? DateTime.Today;

                    string insertSql = @"
                        INSERT INTO dbo.SmsChangeLog
                        (CalcDate, CustomerGID, HodowcaNazwa, ChangeType, AcknowledgedDate, AcknowledgedByUser, OriginalSmsDate, OriginalSmsUser)
                        VALUES
                        (@CalcDate, @CustomerGID, @HodowcaNazwa, @ChangeType, @AcknowledgedDate, @AcknowledgedByUser, @OriginalSmsDate, @OriginalSmsUser)";

                    using (SqlCommand cmd = new SqlCommand(insertSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@CalcDate", selectedDate);
                        cmd.Parameters.AddWithValue("@CustomerGID", row.CustomerGID ?? "");
                        cmd.Parameters.AddWithValue("@HodowcaNazwa", row.HodowcaNazwa ?? "");
                        cmd.Parameters.AddWithValue("@ChangeType", changeType);
                        cmd.Parameters.AddWithValue("@AcknowledgedDate", DateTime.Now);
                        cmd.Parameters.AddWithValue("@AcknowledgedByUser", App.UserID);
                        cmd.Parameters.AddWithValue("@OriginalSmsDate", row.SmsDataWyslania ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@OriginalSmsUser", row.SmsUserId ?? (object)DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd zapisywania logu zmian SMS: {ex.Message}");
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
                        // Sprawdź czy SMS był wysłany - wymagaj potwierdzenia
                        if (!CheckAndWarnAboutSmsChange(droppedData))
                        {
                            dragStartPoint = null;
                            draggedRowIndex = -1;
                            return;
                        }

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

        #region Keyboard Navigation for Time Cells

        /// <summary>
        /// Automatycznie ustawia focus i zaznacza tekst gdy komórka wchodzi w tryb edycji
        /// </summary>
        private void TimeTextBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.Focus();
                textBox.SelectAll();
            }
        }

        /// <summary>
        /// Obsługuje nawigację klawiaturą: Enter/Tab przechodzi do następnej komórki,
        /// strzałki góra/dół przeskakują między wierszami
        /// </summary>
        private void TimeTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!(sender is TextBox textBox))
                return;

            if (e.Key == Key.Enter || e.Key == Key.Tab)
            {
                e.Handled = true;

                // Zatwierdź edycję
                var binding = textBox.GetBindingExpression(TextBox.TextProperty);
                if (binding != null)
                {
                    binding.UpdateSource();
                }

                // Zakończ edycję bieżącej komórki
                dataGridMatryca.CommitEdit(DataGridEditingUnit.Cell, true);

                // Pobierz aktualny indeks kolumny i wiersza
                var currentCell = dataGridMatryca.CurrentCell;
                int currentColumnIndex = dataGridMatryca.Columns.IndexOf(currentCell.Column);
                int currentRowIndex = dataGridMatryca.SelectedIndex;

                // Znajdź następną edytowalną kolumnę (Wyjazd, Załadunek, Przyjazd)
                int nextColumnIndex = currentColumnIndex + 1;
                int nextRowIndex = currentRowIndex;

                // Indeksy kolumn czasowych (Wyjazd=8, Załadunek=9, Przyjazd=10)
                int wyjazdIndex = 8;
                int zaladunekIndex = 9;
                int przyjazdIndex = 10;

                // Jeśli jesteśmy w Przyjazd (ostatnia kolumna czasowa), przejdź do Wyjazd w następnym wierszu
                if (currentColumnIndex >= przyjazdIndex)
                {
                    nextColumnIndex = wyjazdIndex;
                    nextRowIndex = currentRowIndex + 1;

                    // Jeśli to ostatni wiersz, zostań w miejscu
                    if (nextRowIndex >= matrycaData.Count)
                    {
                        nextRowIndex = currentRowIndex;
                        nextColumnIndex = przyjazdIndex;
                    }
                }

                // Przejdź do następnej komórki i rozpocznij edycję
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (nextRowIndex < matrycaData.Count && nextColumnIndex < dataGridMatryca.Columns.Count)
                    {
                        dataGridMatryca.SelectedIndex = nextRowIndex;
                        dataGridMatryca.CurrentCell = new DataGridCellInfo(
                            dataGridMatryca.Items[nextRowIndex],
                            dataGridMatryca.Columns[nextColumnIndex]);

                        dataGridMatryca.BeginEdit();
                    }
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
            else if (e.Key == Key.Down)
            {
                e.Handled = true;

                // Zatwierdź edycję
                var binding = textBox.GetBindingExpression(TextBox.TextProperty);
                if (binding != null)
                {
                    binding.UpdateSource();
                }

                dataGridMatryca.CommitEdit(DataGridEditingUnit.Cell, true);

                int currentRowIndex = dataGridMatryca.SelectedIndex;
                var currentCell = dataGridMatryca.CurrentCell;
                int currentColumnIndex = dataGridMatryca.Columns.IndexOf(currentCell.Column);

                if (currentRowIndex < matrycaData.Count - 1)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        dataGridMatryca.SelectedIndex = currentRowIndex + 1;
                        dataGridMatryca.CurrentCell = new DataGridCellInfo(
                            dataGridMatryca.Items[currentRowIndex + 1],
                            dataGridMatryca.Columns[currentColumnIndex]);
                        dataGridMatryca.BeginEdit();
                    }), System.Windows.Threading.DispatcherPriority.Input);
                }
            }
            else if (e.Key == Key.Up)
            {
                e.Handled = true;

                // Zatwierdź edycję
                var binding = textBox.GetBindingExpression(TextBox.TextProperty);
                if (binding != null)
                {
                    binding.UpdateSource();
                }

                dataGridMatryca.CommitEdit(DataGridEditingUnit.Cell, true);

                int currentRowIndex = dataGridMatryca.SelectedIndex;
                var currentCell = dataGridMatryca.CurrentCell;
                int currentColumnIndex = dataGridMatryca.Columns.IndexOf(currentCell.Column);

                if (currentRowIndex > 0)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        dataGridMatryca.SelectedIndex = currentRowIndex - 1;
                        dataGridMatryca.CurrentCell = new DataGridCellInfo(
                            dataGridMatryca.Items[currentRowIndex - 1],
                            dataGridMatryca.Columns[currentColumnIndex]);
                        dataGridMatryca.BeginEdit();
                    }), System.Windows.Threading.DispatcherPriority.Input);
                }
            }
            else if (e.Key == Key.Escape)
            {
                dataGridMatryca.CancelEdit(DataGridEditingUnit.Cell);
            }
        }

        #endregion

        #region Selection & Panel Update

        private void DataGridMatryca_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedMatrycaRow = dataGridMatryca.SelectedItem as MatrycaRow;
            UpdateInfoPanel();
        }

        /// <summary>
        /// Obsługa zakończenia edycji komórki - automatyczne nadawanie numerów specyfikacji
        /// </summary>
        private void DataGridMatryca_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                // Sprawdź czy edytowano kolumnę "Nr Spec" (Number)
                var column = e.Column as DataGridTextColumn;
                if (column != null && column.Header?.ToString() == "Nr Spec")
                {
                    var editedRow = e.Row.Item as MatrycaRow;
                    if (editedRow != null)
                    {
                        var textBox = e.EditingElement as System.Windows.Controls.TextBox;
                        if (textBox != null && int.TryParse(textBox.Text, out int firstNumber))
                        {
                            // Jeśli edytowano pierwszy wiersz, zapytaj czy wypełnić wszystkie
                            int rowIndex = matrycaData.IndexOf(editedRow);
                            if (rowIndex == 0 && matrycaData.Count > 1)
                            {
                                var result = MessageBox.Show(
                                    $"Czy chcesz automatycznie nadać numery specyfikacji dla wszystkich wierszy?\n\n" +
                                    $"Pierwszy wiersz: {firstNumber}\n" +
                                    $"Kolejne wiersze: {firstNumber + 1}, {firstNumber + 2}, ...",
                                    "Automatyczne numerowanie",
                                    MessageBoxButton.YesNo,
                                    MessageBoxImage.Question);

                                if (result == MessageBoxResult.Yes)
                                {
                                    // Wypełnij numery dla wszystkich wierszy
                                    for (int i = 0; i < matrycaData.Count; i++)
                                    {
                                        matrycaData[i].Number = firstNumber + i;
                                    }
                                    dataGridMatryca.Items.Refresh();
                                }
                            }
                        }
                    }
                }
            }
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

            // Status SMS
            if (selectedMatrycaRow.SmsSent)
            {
                string smsTyp = selectedMatrycaRow.SmsTyp == "ALL" ? "Zbiorczy" : "Pojedynczy";
                lblSmsStatus.Text = $"✅ Wysłano ({smsTyp})";
                lblSmsStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"));
                lblSmsUser.Text = selectedMatrycaRow.SmsUserId ?? "-";
                lblSmsData.Text = selectedMatrycaRow.SmsDataWyslania?.ToString("dd.MM.yyyy HH:mm") ?? "-";
            }
            else
            {
                lblSmsStatus.Text = "❌ Nie wysłano";
                lblSmsStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C62828"));
                lblSmsUser.Text = "-";
                lblSmsData.Text = "-";
            }
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
            lblSmsStatus.Text = "-";
            lblSmsStatus.Foreground = new SolidColorBrush(Colors.Gray);
            lblSmsUser.Text = "-";
            lblSmsData.Text = "-";
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

            // Format daty: "z 27 na 28 listopada (piątek)"
            string formattedDate = FormatDateForSms(selectedDate);

            // Buduj SMS zbiorczy
            var smsLines = new List<string>();
            smsLines.Add($"Piorkowscy {formattedDate}");

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

                // Zapisz historię SMS dla wszystkich aut tego hodowcy
                SaveSmsHistoryForAll(rowsForFarmer, smsContent);

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
            string formattedDate = FormatDateForSms(selectedDate);
            string zaladunekTime = selectedMatrycaRow.Zaladunek.Value.ToString("HH:mm");
            string ciagnikNr = selectedMatrycaRow.CarID ?? "";
            string naczepaNr = selectedMatrycaRow.TrailerID ?? "";
            decimal sredniaWaga = selectedMatrycaRow.WagaDek;

            // Generuj treść SMS
            string smsContent = $"Piorkowscy {formattedDate}: Załadunek godz.{zaladunekTime}";
            if (!string.IsNullOrEmpty(ciagnikNr))
                smsContent += $" ciągnik:{ciagnikNr}";
            if (!string.IsNullOrEmpty(naczepaNr))
                smsContent += $" naczepa:{naczepaNr}";
            smsContent += $" śr.waga:{sredniaWaga:N2}kg";

            try
            {
                Clipboard.SetText(smsContent);

                // Zapisz historię SMS dla pojedynczego auta
                SaveSmsHistory(selectedMatrycaRow, "ONE", smsContent);

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
                var form = new HodowcaForm(idDostawcy, App.UserID);
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

        #region Statystyki Pracowników

        private void BtnStatystyki_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var statsWindow = new StatystykiPracownikowWindow();
                statsWindow.Show();
                UpdateStatus("Otwarto okno statystyk pracowników");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas otwierania statystyk:\n{ex.Message}",
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
                        (ID, CalcDate, CustomerGID, CustomerRealGID, DriverGID, LpDostawy, CarLp, SztPoj, WagaDek,
                         CarID, TrailerID, NotkaWozek, Wyjazd, Zaladunek, Przyjazd, Price,
                         Loss, PriceTypeID, YearNumber, Number)
                        VALUES
                        (@ID, @Date, @Dostawca, @Dostawca, @Kierowca, @LpDostawy, @CarLp, @SztPoj, @WagaDek,
                         @Ciagnik, @Naczepa, @NotkaWozek, @Wyjazd, @Zaladunek,
                         @Przyjazd, @Cena, @Ubytek, @TypCeny, @YearNumber, @Number)";

                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            int savedCount = 0;
                            DateTime selectedDate = dateTimePicker1.SelectedDate ?? DateTime.Today;

                            foreach (var row in matrycaData)
                            {
                                // Użyj wartości z wiersza (ustawione przez "Zastosuj cenę" z harmonogramu)
                                double Ubytek = (double)row.Loss;
                                double Cena = (double)row.Price;
                                int intTypCeny = row.PriceTypeID ?? -1;

                                // Znajdź ID hodowcy
                                string dostawcaId = "-1";
                                if (row.IsFarmerCalc && !string.IsNullOrEmpty(row.CustomerGID?.Trim()) && row.CustomerGID.Trim() != "-1")
                                {
                                    // Dane z FarmerCalc - CustomerGID już zawiera poprawne ID
                                    dostawcaId = row.CustomerGID.Trim();
                                }
                                else if (!string.IsNullOrEmpty(row.HodowcaNazwa))
                                {
                                    // Dane z importu - szukaj ID po nazwie hodowcy
                                    var foundId = zapytaniasql.ZnajdzIdHodowcyString(row.HodowcaNazwa);
                                    if (!string.IsNullOrEmpty(foundId))
                                    {
                                        dostawcaId = foundId;
                                    }
                                }

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
                                    cmd.Parameters.AddWithValue("@Dostawca", dostawcaId);
                                    cmd.Parameters.AddWithValue("@Kierowca", row.DriverGID ?? (object)DBNull.Value);
                                    // LpDostawy = LP z harmonogramu, CarLp = kolejność auta (numer wiersza)
                                    cmd.Parameters.AddWithValue("@LpDostawy", row.HarmonogramLP ?? (object)DBNull.Value);
                                    cmd.Parameters.AddWithValue("@CarLp", savedCount + 1);
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

                                    // YearNumber = rok z wybranej daty
                                    cmd.Parameters.AddWithValue("@YearNumber", selectedDate.Year);
                                    // Number = numer specyfikacji (jeśli ustawiony w wierszu)
                                    cmd.Parameters.AddWithValue("@Number", row.Number ?? (object)DBNull.Value);

                                    cmd.ExecuteNonQuery();
                                    savedCount++;
                                }
                            }

                            transaction.Commit();

                            // Loguj transfer do MatrycaTransferLog
                            LogTransferToDatabase(selectedDate, savedCount);

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

        /// <summary>
        /// Loguje operację zapisu do bazy (transfer) do tabeli MatrycaTransferLog
        /// </summary>
        private void LogTransferToDatabase(DateTime calcDate, int recordCount)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Upewnij się, że tabela MatrycaTransferLog istnieje
                    string createTableSql = @"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MatrycaTransferLog')
                        CREATE TABLE dbo.MatrycaTransferLog (
                            ID BIGINT IDENTITY(1,1) PRIMARY KEY,
                            TransferDate DATETIME NOT NULL,
                            TransferByUser NVARCHAR(100) NOT NULL,
                            CalcDate DATE NOT NULL,
                            RecordCount INT NOT NULL
                        )";
                    using (SqlCommand createCmd = new SqlCommand(createTableSql, conn))
                    {
                        createCmd.ExecuteNonQuery();
                    }

                    string insertSql = @"
                        INSERT INTO dbo.MatrycaTransferLog (TransferDate, TransferByUser, CalcDate, RecordCount)
                        VALUES (@TransferDate, @TransferByUser, @CalcDate, @RecordCount)";

                    using (SqlCommand cmd = new SqlCommand(insertSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@TransferDate", DateTime.Now);
                        cmd.Parameters.AddWithValue("@TransferByUser", App.UserID);
                        cmd.Parameters.AddWithValue("@CalcDate", calcDate);
                        cmd.Parameters.AddWithValue("@RecordCount", recordCount);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd logowania transferu: {ex.Message}");
            }
        }

        #endregion

        #region SMS History - Zapisywanie i ładowanie historii

        /// <summary>
        /// Zapisuje historię wysłania SMS do bazy danych
        /// </summary>
        private void SaveSmsHistory(MatrycaRow row, string smsType, string smsContent)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Upewnij się, że tabela SmsHistory istnieje
                    string createTableSql = @"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SmsHistory')
                        CREATE TABLE dbo.SmsHistory (
                            ID BIGINT IDENTITY(1,1) PRIMARY KEY,
                            CalcDate DATE NOT NULL,
                            CustomerGID NVARCHAR(50) NOT NULL,
                            FarmerCalcID BIGINT NULL,
                            LpDostawy NVARCHAR(20) NULL,
                            SmsType NVARCHAR(10) NOT NULL,
                            SmsContent NVARCHAR(MAX) NULL,
                            SentDate DATETIME NOT NULL,
                            SentByUser NVARCHAR(100) NOT NULL,
                            PhoneNumber NVARCHAR(50) NULL
                        )";
                    using (SqlCommand createCmd = new SqlCommand(createTableSql, conn))
                    {
                        createCmd.ExecuteNonQuery();
                    }

                    DateTime selectedDate = dateTimePicker1.SelectedDate ?? DateTime.Today;
                    string userId = App.UserID;
                    DateTime sentDate = DateTime.Now;

                    string insertSql = @"
                        INSERT INTO dbo.SmsHistory
                        (CalcDate, CustomerGID, FarmerCalcID, LpDostawy, SmsType, SmsContent, SentDate, SentByUser, PhoneNumber)
                        VALUES
                        (@CalcDate, @CustomerGID, @FarmerCalcID, @LpDostawy, @SmsType, @SmsContent, @SentDate, @SentByUser, @PhoneNumber)";

                    using (SqlCommand cmd = new SqlCommand(insertSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@CalcDate", selectedDate);
                        cmd.Parameters.AddWithValue("@CustomerGID", row.CustomerGID ?? "");
                        cmd.Parameters.AddWithValue("@FarmerCalcID", row.ID > 0 ? (object)row.ID : DBNull.Value);
                        cmd.Parameters.AddWithValue("@LpDostawy", row.LpDostawy ?? "");
                        cmd.Parameters.AddWithValue("@SmsType", smsType);
                        cmd.Parameters.AddWithValue("@SmsContent", smsContent ?? "");
                        cmd.Parameters.AddWithValue("@SentDate", sentDate);
                        cmd.Parameters.AddWithValue("@SentByUser", userId);
                        cmd.Parameters.AddWithValue("@PhoneNumber", row.Telefon ?? "");
                        cmd.ExecuteNonQuery();
                    }

                    // Aktualizuj wiersz w pamięci
                    row.SmsSent = true;
                    row.SmsDataWyslania = sentDate;
                    row.SmsUserId = userId;
                    row.SmsTyp = smsType;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd zapisywania historii SMS: {ex.Message}");
            }
        }

        /// <summary>
        /// Zapisuje historię SMS dla wszystkich aut hodowcy (SMS zbiorczy)
        /// </summary>
        private void SaveSmsHistoryForAll(List<MatrycaRow> rows, string smsContent)
        {
            foreach (var row in rows)
            {
                SaveSmsHistory(row, "ALL", smsContent);
            }
        }

        /// <summary>
        /// Ładuje historię SMS z bazy danych dla danych wierszy
        /// </summary>
        private void LoadSmsHistory()
        {
            try
            {
                DateTime selectedDate = dateTimePicker1.SelectedDate ?? DateTime.Today;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Sprawdź czy tabela istnieje
                    string checkTableSql = "SELECT COUNT(*) FROM sys.tables WHERE name = 'SmsHistory'";
                    using (SqlCommand checkCmd = new SqlCommand(checkTableSql, conn))
                    {
                        int tableExists = (int)checkCmd.ExecuteScalar();
                        if (tableExists == 0) return;
                    }

                    // Pobierz ostatni SMS dla każdego CustomerGID + LpDostawy w danym dniu
                    string query = @"
                        SELECT CustomerGID, LpDostawy, SmsType, SentDate, SentByUser
                        FROM (
                            SELECT CustomerGID, LpDostawy, SmsType, SentDate, SentByUser,
                                   ROW_NUMBER() OVER (PARTITION BY CustomerGID, LpDostawy ORDER BY SentDate DESC) AS rn
                            FROM dbo.SmsHistory
                            WHERE CalcDate = @CalcDate
                        ) sub
                        WHERE rn = 1";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@CalcDate", selectedDate);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string customerGID = reader["CustomerGID"]?.ToString() ?? "";
                                string lpDostawy = reader["LpDostawy"]?.ToString() ?? "";
                                string smsType = reader["SmsType"]?.ToString() ?? "";
                                DateTime sentDate = reader["SentDate"] != DBNull.Value ? Convert.ToDateTime(reader["SentDate"]) : DateTime.MinValue;
                                string sentByUser = reader["SentByUser"]?.ToString() ?? "";

                                // Znajdź pasujący wiersz i zaktualizuj dane SMS
                                var matchingRows = matrycaData
                                    .Where(r => r.CustomerGID == customerGID && r.LpDostawy == lpDostawy)
                                    .ToList();

                                foreach (var row in matchingRows)
                                {
                                    row.SmsSent = true;
                                    row.SmsDataWyslania = sentDate;
                                    row.SmsUserId = sentByUser;
                                    row.SmsTyp = smsType;
                                }

                                // Jeśli to SMS zbiorczy (ALL), zaznacz wszystkie wiersze tego hodowcy
                                if (smsType == "ALL")
                                {
                                    var allRowsForFarmer = matrycaData
                                        .Where(r => r.CustomerGID == customerGID)
                                        .ToList();

                                    foreach (var row in allRowsForFarmer)
                                    {
                                        if (!row.SmsSent || row.SmsDataWyslania < sentDate)
                                        {
                                            row.SmsSent = true;
                                            row.SmsDataWyslania = sentDate;
                                            row.SmsUserId = sentByUser;
                                            row.SmsTyp = smsType;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd ładowania historii SMS: {ex.Message}");
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

        /// <summary>
        /// Formatuje datę dla SMS: "z 27 na 28 listopada (piątek)"
        /// </summary>
        private string FormatDateForSms(DateTime date)
        {
            var culture = new CultureInfo("pl-PL");

            // Nazwy miesięcy w dopełniaczu
            string[] miesiace = { "", "stycznia", "lutego", "marca", "kwietnia", "maja", "czerwca",
                                  "lipca", "sierpnia", "września", "października", "listopada", "grudnia" };

            DateTime prevDay = date.AddDays(-1);
            string dayOfWeek = date.ToString("dddd", culture);
            // Pierwsza litera wielka
            dayOfWeek = char.ToUpper(dayOfWeek[0]) + dayOfWeek.Substring(1);

            string miesiac = miesiace[date.Month];

            return $"z {prevDay.Day} na {date.Day} {miesiac} ({dayOfWeek})";
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
        private int? _number; // Numer specyfikacji
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

        // Dane SMS
        private bool _smsSent;
        private DateTime? _smsDataWyslania;
        private string _smsUserId;
        private string _smsTyp; // "ALL" lub "ONE"

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

        /// <summary>
        /// Numer specyfikacji
        /// </summary>
        public int? Number
        {
            get => _number;
            set { _number = value; OnPropertyChanged(nameof(Number)); }
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

        // Właściwości SMS
        public bool SmsSent
        {
            get => _smsSent;
            set { _smsSent = value; OnPropertyChanged(nameof(SmsSent)); OnPropertyChanged(nameof(SmsStatus)); }
        }

        public DateTime? SmsDataWyslania
        {
            get => _smsDataWyslania;
            set { _smsDataWyslania = value; OnPropertyChanged(nameof(SmsDataWyslania)); OnPropertyChanged(nameof(SmsStatus)); }
        }

        public string SmsUserId
        {
            get => _smsUserId;
            set { _smsUserId = value; OnPropertyChanged(nameof(SmsUserId)); OnPropertyChanged(nameof(SmsStatus)); }
        }

        public string SmsTyp
        {
            get => _smsTyp;
            set { _smsTyp = value; OnPropertyChanged(nameof(SmsTyp)); OnPropertyChanged(nameof(SmsStatus)); }
        }

        /// <summary>
        /// Wyświetlany status SMS - kto wysłał i kiedy
        /// </summary>
        public string SmsStatus
        {
            get
            {
                if (!SmsSent || !SmsDataWyslania.HasValue)
                    return "";

                string typ = SmsTyp == "ALL" ? "Zb." : "Poj.";
                return $"{typ} {SmsDataWyslania.Value:HH:mm} ({SmsUserId})";
            }
        }

        // === DANE CENOWE Z HARMONOGRAMU ===
        private decimal _price;
        private decimal _loss;
        private int? _priceTypeID;
        private string _priceTypeName;

        public decimal Price
        {
            get => _price;
            set { _price = value; OnPropertyChanged(nameof(Price)); }
        }

        public decimal Loss
        {
            get => _loss;
            set { _loss = value; OnPropertyChanged(nameof(Loss)); }
        }

        public int? PriceTypeID
        {
            get => _priceTypeID;
            set { _priceTypeID = value; OnPropertyChanged(nameof(PriceTypeID)); }
        }

        public string PriceTypeName
        {
            get => _priceTypeName;
            set { _priceTypeName = value; OnPropertyChanged(nameof(PriceTypeName)); }
        }

        // LP z harmonogramu dostaw (do zapisu w FarmerCalc.LpDostawy)
        private int? _harmonogramLP;
        public int? HarmonogramLP
        {
            get => _harmonogramLP;
            set { _harmonogramLP = value; OnPropertyChanged(nameof(HarmonogramLP)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Konwerter do obsługi skrótów godzin (np. 8 -> 08:00, 804 -> 08:04, 1430 -> 14:30)
    /// </summary>
    public class TimeShortcutConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return "";

            if (value is DateTime dt)
            {
                return dt.ToString("HH:mm");
            }

            // Obsługa DateTime? (nullable)
            Type valueType = value.GetType();
            if (valueType == typeof(DateTime))
            {
                return ((DateTime)value).ToString("HH:mm");
            }

            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
                return null;

            string input = value.ToString().Trim();

            // Usuń dwukropek jeśli jest
            input = input.Replace(":", "");

            // Spróbuj sparsować jako liczbę
            if (int.TryParse(input, out int numericValue))
            {
                int hours = 0;
                int minutes = 0;

                if (numericValue >= 0 && numericValue <= 24)
                {
                    // Tylko godzina (np. 8 -> 08:00, 14 -> 14:00)
                    hours = numericValue;
                    minutes = 0;
                }
                else if (numericValue >= 100 && numericValue <= 2459)
                {
                    // Format 3-4 cyfrowy (np. 804 -> 08:04, 915 -> 09:15, 1430 -> 14:30)
                    if (numericValue < 1000)
                    {
                        // 3 cyfry: pierwsza to godzina, dwie ostatnie to minuty (np. 804 -> 8:04)
                        hours = numericValue / 100;
                        minutes = numericValue % 100;
                    }
                    else
                    {
                        // 4 cyfry: dwie pierwsze to godzina, dwie ostatnie to minuty (np. 1430 -> 14:30)
                        hours = numericValue / 100;
                        minutes = numericValue % 100;
                    }
                }
                else
                {
                    // Nieprawidłowa wartość
                    return Binding.DoNothing;
                }

                // Walidacja
                if (hours < 0 || hours > 23 || minutes < 0 || minutes > 59)
                {
                    return Binding.DoNothing;
                }

                // Zwróć DateTime z dzisiejszą datą i podaną godziną
                return new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day, hours, minutes, 0);
            }

            // Próba parsowania jako standardowy format godziny
            if (DateTime.TryParseExact(input, new[] { "HH:mm", "H:mm", "HHmm", "Hmm" },
                CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedTime))
            {
                return new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day,
                    parsedTime.Hour, parsedTime.Minute, 0);
            }

            return Binding.DoNothing;
        }
    }

    /// <summary>
    /// Model danych dla dostawy z harmonogramu
    /// </summary>
    public class HarmonogramDostawaItem
    {
        public int LP { get; set; }
        public string Dostawca { get; set; }
        public int SztukiDek { get; set; }
        public decimal WagaDek { get; set; }
        public decimal Cena { get; set; }
        public decimal Ubytek { get; set; }
        public string TypCeny { get; set; }
        public string DisplayText { get; set; }
    }
}