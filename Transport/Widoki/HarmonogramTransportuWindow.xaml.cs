using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace Kalendarz1.Transport.Widoki
{
    public partial class HarmonogramTransportuWindow : Window
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private ZapytaniaSQL zapytaniasql = new ZapytaniaSQL();
        private ObservableCollection<HarmonogramTransportuRow> harmonogramData;
        private HarmonogramTransportuRow selectedRow;

        // Listy dla ComboBox
        private DataTable kierowcyTable;
        private DataTable ciagnikiTable;
        private DataTable naczepyTable;

        public HarmonogramTransportuWindow()
        {
            InitializeComponent();
            harmonogramData = new ObservableCollection<HarmonogramTransportuRow>();
            dataGridView1.ItemsSource = harmonogramData;
            dateTimePicker1.SelectedDate = DateTime.Today;

            // Skroty klawiszowe
            this.KeyDown += Window_KeyDown;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadComboBoxData();
            LoadData(dateTimePicker1.SelectedDate ?? DateTime.Today);
            UpdateDayName();
            UpdateStatus("Dane zaladowane pomyslnie");
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
            {
                BtnSaveToDatabase_Click(null, null);
                e.Handled = true;
            }
            else if (e.Key == Key.P && Keyboard.Modifiers == ModifierKeys.Control)
            {
                BtnPrintPDF_Click(null, null);
                e.Handled = true;
            }
            else if (e.Key == Key.F5)
            {
                BtnRefresh_Click(null, null);
                e.Handled = true;
            }
            else if (e.Key == Key.Up && Keyboard.Modifiers == ModifierKeys.Alt)
            {
                BtnMoveUp_Click(null, null);
                e.Handled = true;
            }
            else if (e.Key == Key.Down && Keyboard.Modifiers == ModifierKeys.Alt)
            {
                BtnMoveDown_Click(null, null);
                e.Handled = true;
            }
        }

        private void LoadComboBoxData()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Kierowcy
                    string driverQuery = @"
                        SELECT GID, [Name]
                        FROM [LibraNet].[dbo].[Driver]
                        WHERE Deleted = 0
                        ORDER BY [Name] ASC";

                    SqlDataAdapter driverAdapter = new SqlDataAdapter(driverQuery, connection);
                    kierowcyTable = new DataTable();
                    driverAdapter.Fill(kierowcyTable);

                    // Ciagniki
                    string carQuery = @"
                        SELECT DISTINCT ID
                        FROM dbo.CarTrailer
                        WHERE kind = '1'
                        ORDER BY ID DESC";

                    SqlDataAdapter carAdapter = new SqlDataAdapter(carQuery, connection);
                    ciagnikiTable = new DataTable();
                    carAdapter.Fill(ciagnikiTable);

                    // Naczepy
                    string trailerQuery = @"
                        SELECT DISTINCT ID
                        FROM dbo.CarTrailer
                        WHERE kind = '2'
                        ORDER BY ID DESC";

                    SqlDataAdapter trailerAdapter = new SqlDataAdapter(trailerQuery, connection);
                    naczepyTable = new DataTable();
                    trailerAdapter.Fill(naczepyTable);

                    // Przypisz do ComboBox w DataGrid
                    colKierowca.ItemsSource = kierowcyTable.DefaultView;
                    colCiagnik.ItemsSource = ciagnikiTable.DefaultView;
                    colNaczepa.ItemsSource = naczepyTable.DefaultView;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad podczas ladowania danych ComboBox:\n{ex.Message}",
                    "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DateTimePicker1_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dateTimePicker1.SelectedDate.HasValue)
            {
                LoadData(dateTimePicker1.SelectedDate.Value);
                UpdateDayName();
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

        private void UpdateDayName()
        {
            if (dateTimePicker1.SelectedDate.HasValue)
            {
                lblDayName.Text = dateTimePicker1.SelectedDate.Value.ToString("dddd", new CultureInfo("pl-PL"));
            }
        }

        private void LoadData(DateTime selectedDate)
        {
            try
            {
                UpdateStatus("Ladowanie danych...");
                harmonogramData.Clear();

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Sprawdz czy sa dane w FarmerCalc
                    string checkQuery = @"
                        SELECT COUNT(*)
                        FROM [LibraNet].[dbo].[FarmerCalc]
                        WHERE CalcDate = @SelectedDate";

                    SqlCommand checkCommand = new SqlCommand(checkQuery, connection);
                    checkCommand.Parameters.AddWithValue("@SelectedDate", selectedDate.Date);
                    int count = (int)checkCommand.ExecuteScalar();

                    DataTable table = new DataTable();
                    bool isFarmerCalc = false;

                    if (count > 0)
                    {
                        // Dane z FarmerCalc
                        string query = @"
                            SELECT
                                fc.ID,
                                fc.LpDostawy,
                                fc.CustomerGID,
                                c.ShortName AS HodowcaNazwa,
                                fc.WagaDek,
                                fc.SztPoj,
                                fc.DriverGID,
                                fc.CarID,
                                fc.TrailerID,
                                fc.Wyjazd,
                                fc.Zaladunek,
                                fc.Przyjazd,
                                fc.NotkaWozek,
                                fc.Price AS Cena,
                                fc.Loss AS Ubytek
                            FROM [LibraNet].[dbo].[FarmerCalc] fc
                            LEFT JOIN [LibraNet].[dbo].[CustomerAvilog] c ON fc.CustomerGID = c.GID
                            WHERE fc.CalcDate = @SelectedDate
                            ORDER BY fc.LpDostawy";

                        SqlCommand command = new SqlCommand(query, connection);
                        command.Parameters.AddWithValue("@SelectedDate", selectedDate.Date);
                        SqlDataAdapter adapter = new SqlDataAdapter(command);
                        adapter.Fill(table);
                        isFarmerCalc = true;
                    }
                    else
                    {
                        // Dane z HarmonogramDostaw
                        string query = @"
                            SELECT
                                CAST(0 AS BIGINT) AS ID,
                                Lp AS LpDostawy,
                                Dostawca AS CustomerGID,
                                d.Dostawca AS HodowcaNazwa,
                                WagaDek,
                                SztSzuflada AS SztPoj,
                                CAST(NULL AS INT) AS DriverGID,
                                CAST(NULL AS VARCHAR(50)) AS CarID,
                                CAST(NULL AS VARCHAR(50)) AS TrailerID,
                                CAST(NULL AS DATETIME) AS Wyjazd,
                                CAST(NULL AS DATETIME) AS Zaladunek,
                                CAST(NULL AS DATETIME) AS Przyjazd,
                                CAST(NULL AS VARCHAR(100)) AS NotkaWozek,
                                d.Cena,
                                d.Ubytek
                            FROM dbo.HarmonogramDostaw d
                            WHERE DataOdbioru = @StartDate
                            AND Bufor = 'Potwierdzony'
                            ORDER BY Lp";

                        SqlCommand command = new SqlCommand(query, connection);
                        command.Parameters.AddWithValue("@StartDate", selectedDate.Date);
                        SqlDataAdapter adapter = new SqlDataAdapter(command);
                        adapter.Fill(table);
                    }

                    if (table.Rows.Count > 0)
                    {
                        foreach (DataRow row in table.Rows)
                        {
                            var harmonogramRow = new HarmonogramTransportuRow
                            {
                                ID = row["ID"] != DBNull.Value ? Convert.ToInt64(row["ID"]) : 0,
                                LpDostawy = row["LpDostawy"]?.ToString() ?? "",
                                CustomerGID = row["CustomerGID"]?.ToString() ?? "",
                                HodowcaNazwa = row["HodowcaNazwa"]?.ToString() ?? "",
                                WagaDek = row["WagaDek"] != DBNull.Value ? Convert.ToDecimal(row["WagaDek"]) : 0,
                                SztPoj = row["SztPoj"] != DBNull.Value ? Convert.ToInt32(row["SztPoj"]) : 0,
                                DriverGID = row["DriverGID"] != DBNull.Value ? Convert.ToInt32(row["DriverGID"]) : (int?)null,
                                CarID = row["CarID"]?.ToString(),
                                TrailerID = row["TrailerID"]?.ToString(),
                                WyjazdGodzina = row["Wyjazd"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["Wyjazd"]) : null,
                                ZaladunekGodzina = row["Zaladunek"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["Zaladunek"]) : null,
                                PrzyjazdGodzina = row["Przyjazd"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["Przyjazd"]) : null,
                                NotkaWozek = row["NotkaWozek"]?.ToString() ?? "",
                                Cena = row["Cena"] != DBNull.Value ? Convert.ToDecimal(row["Cena"]) : 0,
                                Ubytek = row["Ubytek"] != DBNull.Value ? Math.Round(Convert.ToDecimal(row["Ubytek"]) * 100, 2) : 0,
                                Status = isFarmerCalc ? "Zaplanowany" : "Nowy",
                                IsFarmerCalc = isFarmerCalc
                            };

                            harmonogramData.Add(harmonogramRow);
                        }

                        UpdateStatistics();
                        UpdateStatus($"Zaladowano {table.Rows.Count} rekordow");
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
                MessageBox.Show($"Blad podczas ladowania danych:\n{ex.Message}",
                    "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatus("Blad ladowania danych");
            }
        }

        private void UpdateStatistics()
        {
            int recordCount = harmonogramData.Count;
            decimal totalWeight = harmonogramData.Sum(r => r.WagaDek);
            int totalPieces = harmonogramData.Sum(r => r.SztPoj);
            int carsCount = harmonogramData.Where(r => !string.IsNullOrEmpty(r.CarID)).Select(r => r.CarID).Distinct().Count();

            lblRecordCount.Text = recordCount.ToString();
            lblTotalWeight.Text = $"{totalWeight:N0} kg";
            lblTotalPieces.Text = totalPieces.ToString("N0");
            lblCarsCount.Text = carsCount.ToString();
        }

        private void DataGridView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dataGridView1.SelectedItem != null)
            {
                selectedRow = dataGridView1.SelectedItem as HarmonogramTransportuRow;
            }
        }

        private void DataGridView1_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateStatistics();
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void DataGridView1_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            var row = e.Row.Item as HarmonogramTransportuRow;
            if (row != null)
            {
                // Koloruj wiersze wedlug statusu
                if (row.Status == "Zaplanowany")
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(232, 245, 233)); // Jasny zielony
                }
                else if (row.Status == "Nowy")
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(255, 243, 224)); // Jasny pomaranczowy
                }
            }
        }

        private void BtnMoveUp_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = dataGridView1.SelectedIndex;
            if (selectedIndex > 0)
            {
                var item = harmonogramData[selectedIndex];
                harmonogramData.RemoveAt(selectedIndex);
                harmonogramData.Insert(selectedIndex - 1, item);
                dataGridView1.SelectedIndex = selectedIndex - 1;
                RefreshNumeration();
                UpdateStatus("Wiersz przeniesiony w gore");
            }
        }

        private void BtnMoveDown_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = dataGridView1.SelectedIndex;
            if (selectedIndex >= 0 && selectedIndex < harmonogramData.Count - 1)
            {
                var item = harmonogramData[selectedIndex];
                harmonogramData.RemoveAt(selectedIndex);
                harmonogramData.Insert(selectedIndex + 1, item);
                dataGridView1.SelectedIndex = selectedIndex + 1;
                RefreshNumeration();
                UpdateStatus("Wiersz przeniesiony w dol");
            }
        }

        private void RefreshNumeration()
        {
            for (int i = 0; i < harmonogramData.Count; i++)
            {
                harmonogramData[i].LpDostawy = (i + 1).ToString();
            }
        }

        private void BtnAddRow_Click(object sender, RoutedEventArgs e)
        {
            var newRow = new HarmonogramTransportuRow
            {
                ID = 0,
                LpDostawy = (harmonogramData.Count + 1).ToString(),
                Status = "Nowy",
                IsFarmerCalc = false
            };
            harmonogramData.Add(newRow);
            dataGridView1.SelectedIndex = harmonogramData.Count - 1;
            UpdateStatistics();
            UpdateStatus("Dodano nowy wiersz");
        }

        private void BtnDeleteRow_Click(object sender, RoutedEventArgs e)
        {
            if (selectedRow != null)
            {
                var result = MessageBox.Show("Czy na pewno chcesz usunac zaznaczony wiersz?",
                    "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    harmonogramData.Remove(selectedRow);
                    RefreshNumeration();
                    UpdateStatistics();
                    UpdateStatus("Wiersz usuniety");
                }
            }
            else
            {
                MessageBox.Show("Prosze zaznaczyc wiersz do usuniecia.",
                    "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadData(dateTimePicker1.SelectedDate ?? DateTime.Today);
        }

        private void BtnSpecyfikacja_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var specWindow = new WidokSpecyfikacje();
                specWindow.Show();
                UpdateStatus("Otwarto widok specyfikacji");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad podczas otwierania specyfikacji:\n{ex.Message}",
                    "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSaveToDatabase_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Czy na pewno chcesz zapisac dane do bazy?\n\nOperacja nadpisze istniejace dane dla tego dnia.",
                "Potwierdzenie zapisu",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                UpdateStatus("Zapisywanie danych...");

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Usun istniejace dane dla tego dnia
                    DateTime selectedDate = dateTimePicker1.SelectedDate ?? DateTime.Today;

                    string deleteQuery = "DELETE FROM dbo.FarmerCalc WHERE CalcDate = @Date";
                    using (SqlCommand deleteCmd = new SqlCommand(deleteQuery, conn))
                    {
                        deleteCmd.Parameters.AddWithValue("@Date", selectedDate.Date);
                        deleteCmd.ExecuteNonQuery();
                    }

                    // Wstaw nowe dane
                    string insertQuery = @"INSERT INTO dbo.FarmerCalc
                        (ID, CalcDate, CustomerGID, CustomerRealGID, DriverGID, LpDostawy, SztPoj, WagaDek,
                         CarID, TrailerID, NotkaWozek, Wyjazd, Zaladunek, Przyjazd, Price, Loss, PriceTypeID)
                        VALUES
                        (@ID, @Date, @CustomerGID, @CustomerGID, @DriverGID, @LpDostawy, @SztPoj, @WagaDek,
                         @CarID, @TrailerID, @NotkaWozek, @Wyjazd, @Zaladunek, @Przyjazd, @Cena, @Ubytek, @TypCeny)";

                    int savedCount = 0;

                    foreach (var row in harmonogramData)
                    {
                        // Znajdz nowe ID
                        long maxID;
                        string maxIDSql = "SELECT ISNULL(MAX(ID), 0) + 1 FROM dbo.[FarmerCalc]";
                        using (SqlCommand maxCmd = new SqlCommand(maxIDSql, conn))
                        {
                            maxID = Convert.ToInt64(maxCmd.ExecuteScalar());
                        }

                        // Pobierz GID hodowcy
                        int customerGID = 0;
                        if (!string.IsNullOrEmpty(row.CustomerGID))
                        {
                            int.TryParse(row.CustomerGID, out customerGID);
                        }
                        if (customerGID == 0)
                        {
                            customerGID = zapytaniasql.ZnajdzIdHodowcy(row.HodowcaNazwa);
                        }

                        using (SqlCommand cmd = new SqlCommand(insertQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@ID", maxID);
                            cmd.Parameters.AddWithValue("@Date", selectedDate.Date);
                            cmd.Parameters.AddWithValue("@CustomerGID", customerGID);
                            cmd.Parameters.AddWithValue("@DriverGID", row.DriverGID.HasValue ? (object)row.DriverGID.Value : DBNull.Value);
                            cmd.Parameters.AddWithValue("@LpDostawy", row.LpDostawy ?? "");
                            cmd.Parameters.AddWithValue("@SztPoj", row.SztPoj);
                            cmd.Parameters.AddWithValue("@WagaDek", row.WagaDek);
                            cmd.Parameters.AddWithValue("@CarID", string.IsNullOrEmpty(row.CarID) ? DBNull.Value : row.CarID);
                            cmd.Parameters.AddWithValue("@TrailerID", string.IsNullOrEmpty(row.TrailerID) ? DBNull.Value : row.TrailerID);
                            cmd.Parameters.AddWithValue("@NotkaWozek", string.IsNullOrEmpty(row.NotkaWozek) ? DBNull.Value : row.NotkaWozek);

                            // Godziny
                            cmd.Parameters.AddWithValue("@Wyjazd", row.WyjazdGodzina.HasValue
                                ? (object)CombineDateAndTime(row.WyjazdGodzina.Value, selectedDate)
                                : DBNull.Value);
                            cmd.Parameters.AddWithValue("@Zaladunek", row.ZaladunekGodzina.HasValue
                                ? (object)CombineDateAndTime(row.ZaladunekGodzina.Value, selectedDate)
                                : DBNull.Value);
                            cmd.Parameters.AddWithValue("@Przyjazd", row.PrzyjazdGodzina.HasValue
                                ? (object)CombineDateAndTime(row.PrzyjazdGodzina.Value, selectedDate)
                                : DBNull.Value);

                            cmd.Parameters.AddWithValue("@Cena", row.Cena);
                            cmd.Parameters.AddWithValue("@Ubytek", row.Ubytek / 100); // Konwersja z % na ulamek
                            cmd.Parameters.AddWithValue("@TypCeny", 1); // Domyslny typ ceny

                            cmd.ExecuteNonQuery();
                            savedCount++;
                        }
                    }

                    MessageBox.Show(
                        $"Pomyslnie zapisano {savedCount} rekordow do bazy danych.",
                        "Sukces",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    // Odswierz dane
                    LoadData(selectedDate);
                    UpdateStatus($"Zapisano {savedCount} rekordow");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Wystapil blad podczas zapisywania danych:\n\n{ex.Message}",
                    "Blad",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                UpdateStatus("Blad zapisu");
            }
        }

        private DateTime CombineDateAndTime(DateTime time, DateTime date)
        {
            return new DateTime(date.Year, date.Month, date.Day, time.Hour, time.Minute, time.Second);
        }

        private void BtnPrintPDF_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateStatus("Generowanie PDF...");

                DateTime selectedDate = dateTimePicker1.SelectedDate ?? DateTime.Today;
                string strDate = selectedDate.ToString("yyyy.MM.dd");
                string directoryPath = Path.Combine(@"\\192.168.0.170\Public\Przel\", strDate);

                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                string filePath = Path.Combine(directoryPath, $"Harmonogram_Transportu_{strDate}.pdf");

                Document doc = new Document(PageSize.A4.Rotate(), 40, 40, 30, 30);

                using (FileStream fs = new FileStream(filePath, FileMode.Create))
                {
                    PdfWriter writer = PdfWriter.GetInstance(doc, fs);
                    doc.Open();

                    // Tytul
                    var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, BaseColor.DARK_GRAY);
                    var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.WHITE);
                    var cellFont = FontFactory.GetFont(FontFactory.HELVETICA, 9, BaseColor.BLACK);

                    Paragraph title = new Paragraph($"HARMONOGRAM TRANSPORTU - {strDate}", titleFont);
                    title.Alignment = Element.ALIGN_CENTER;
                    title.SpacingAfter = 20;
                    doc.Add(title);

                    // Podsumowanie
                    var summaryFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.DARK_GRAY);
                    Paragraph summary = new Paragraph(
                        $"Rekordow: {harmonogramData.Count} | " +
                        $"Suma wagi: {harmonogramData.Sum(r => r.WagaDek):N0} kg | " +
                        $"Suma sztuk: {harmonogramData.Sum(r => r.SztPoj):N0}", summaryFont);
                    summary.Alignment = Element.ALIGN_CENTER;
                    summary.SpacingAfter = 15;
                    doc.Add(summary);

                    // Tabela
                    PdfPTable table = new PdfPTable(12);
                    table.WidthPercentage = 100;
                    table.SetWidths(new float[] { 1, 3, 2, 2, 3, 2, 2, 1.5f, 1.5f, 1.5f, 2, 1.5f });

                    // Naglowki
                    string[] headers = { "LP", "Hodowca", "Waga", "Sztuk", "Kierowca", "Ciagnik", "Naczepa", "Wyjazd", "Zaladun.", "Przyjazd", "Wozek", "Cena" };
                    foreach (var header in headers)
                    {
                        PdfPCell cell = new PdfPCell(new Phrase(header, headerFont));
                        cell.BackgroundColor = new BaseColor(92, 138, 58);
                        cell.HorizontalAlignment = Element.ALIGN_CENTER;
                        cell.Padding = 8;
                        table.AddCell(cell);
                    }

                    // Dane
                    foreach (var row in harmonogramData)
                    {
                        AddCell(table, row.LpDostawy, cellFont);
                        AddCell(table, row.HodowcaNazwa, cellFont);
                        AddCell(table, row.WagaDek.ToString("N0"), cellFont, Element.ALIGN_RIGHT);
                        AddCell(table, row.SztPoj.ToString("N0"), cellFont, Element.ALIGN_RIGHT);
                        AddCell(table, GetDriverName(row.DriverGID), cellFont);
                        AddCell(table, row.CarID ?? "", cellFont);
                        AddCell(table, row.TrailerID ?? "", cellFont);
                        AddCell(table, row.WyjazdGodzina?.ToString("HH:mm") ?? "", cellFont, Element.ALIGN_CENTER);
                        AddCell(table, row.ZaladunekGodzina?.ToString("HH:mm") ?? "", cellFont, Element.ALIGN_CENTER);
                        AddCell(table, row.PrzyjazdGodzina?.ToString("HH:mm") ?? "", cellFont, Element.ALIGN_CENTER);
                        AddCell(table, row.NotkaWozek ?? "", cellFont);
                        AddCell(table, row.Cena.ToString("F2"), cellFont, Element.ALIGN_RIGHT);
                    }

                    doc.Add(table);
                    doc.Close();
                }

                MessageBox.Show(
                    $"Wygenerowano dokument PDF:\n{Path.GetFileName(filePath)}\n\nSciezka:\n{filePath}",
                    "Sukces",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                UpdateStatus("PDF wygenerowany pomyslnie");

                // Otworz plik
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad podczas generowania PDF:\n{ex.Message}",
                    "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatus("Blad generowania PDF");
            }
        }

        private void AddCell(PdfPTable table, string text, iTextSharp.text.Font font, int alignment = Element.ALIGN_LEFT)
        {
            PdfPCell cell = new PdfPCell(new Phrase(text ?? "", font));
            cell.HorizontalAlignment = alignment;
            cell.Padding = 6;
            table.AddCell(cell);
        }

        private string GetDriverName(int? driverGID)
        {
            if (!driverGID.HasValue || kierowcyTable == null)
                return "";

            var rows = kierowcyTable.Select($"GID = {driverGID.Value}");
            return rows.Length > 0 ? rows[0]["Name"]?.ToString() ?? "" : "";
        }

        private void UpdateStatus(string message)
        {
            statusLabel.Text = message;
        }
    }

    // Model danych dla wiersza harmonogramu
    public class HarmonogramTransportuRow : INotifyPropertyChanged
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
        private DateTime? _wyjazdGodzina;
        private DateTime? _zaladunekGodzina;
        private DateTime? _przyjazdGodzina;
        private string _notkaWozek;
        private decimal _cena;
        private decimal _ubytek;
        private string _status;
        private string _uwagi;
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

        public DateTime? WyjazdGodzina
        {
            get => _wyjazdGodzina;
            set { _wyjazdGodzina = value; OnPropertyChanged(nameof(WyjazdGodzina)); }
        }

        public DateTime? ZaladunekGodzina
        {
            get => _zaladunekGodzina;
            set { _zaladunekGodzina = value; OnPropertyChanged(nameof(ZaladunekGodzina)); }
        }

        public DateTime? PrzyjazdGodzina
        {
            get => _przyjazdGodzina;
            set { _przyjazdGodzina = value; OnPropertyChanged(nameof(PrzyjazdGodzina)); }
        }

        public string NotkaWozek
        {
            get => _notkaWozek;
            set { _notkaWozek = value; OnPropertyChanged(nameof(NotkaWozek)); }
        }

        public decimal Cena
        {
            get => _cena;
            set { _cena = value; OnPropertyChanged(nameof(Cena)); }
        }

        public decimal Ubytek
        {
            get => _ubytek;
            set { _ubytek = value; OnPropertyChanged(nameof(Ubytek)); }
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
