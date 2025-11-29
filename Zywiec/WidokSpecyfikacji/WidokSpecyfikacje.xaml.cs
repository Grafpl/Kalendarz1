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

        public WidokSpecyfikacje()
        {
            InitializeComponent();
            specyfikacjeData = new ObservableCollection<SpecyfikacjaRow>();
            dataGridView1.ItemsSource = specyfikacjeData;
            dateTimePicker1.SelectedDate = DateTime.Today;

            // Dodaj obsługę skrótów klawiszowych
            this.KeyDown += Window_KeyDown;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadData(dateTimePicker1.SelectedDate ?? DateTime.Today);
            UpdateStatus("Dane załadowane pomyślnie");
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
                            var specRow = new SpecyfikacjaRow
                            {
                                ID = ZapytaniaSQL.GetValueOrDefault<int>(row, "ID", 0),
                                Nr = ZapytaniaSQL.GetValueOrDefault<int>(row, "CarLp", 0),
                                Dostawca = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(
                                    ZapytaniaSQL.GetValueOrDefault<string>(row, "CustomerGID", "-1"), "ShortName"),
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

        private void DataGridView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dataGridView1.SelectedItem != null)
            {
                selectedRow = dataGridView1.SelectedItem as SpecyfikacjaRow;
            }
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
                        UpdateDatabaseRow(row, e.Column.Header.ToString());
                        UpdateStatistics();
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
        }

        private void DataGridView1_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            var row = e.Row.Item as SpecyfikacjaRow;
            if (row != null && row.PiK)
            {
                // Zastosuj formatowanie dla wierszy z zaznaczonym PiK
                e.Row.Foreground = new SolidColorBrush(Colors.Red);
                e.Row.FontStyle = FontStyles.Italic;
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
                { "Szt.Dek", "DeclI1" },
                { "Padłe", "DeclI2" },
                { "CH", "DeclI3" },
                { "NW", "DeclI4" },
                { "ZM", "DeclI5" },
                { "LUMEL", "LumQnt" },
                { "Szt.Wyb", "ProdQnt" },
                { "KG Wyb", "ProdWgt" },
                { "Cena", "Price" },
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
                case "Szt.Dek": return row.SztukiDek;
                case "Padłe": return row.Padle;
                case "CH": return row.CH;
                case "NW": return row.NW;
                case "ZM": return row.ZM;
                case "LUMEL": return row.LUMEL;
                case "Szt.Wyb": return row.SztukiWybijak;
                case "KG Wyb": return row.KilogramyWybijak;
                case "Cena": return row.Cena;
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
                return;
            }

            lblRecordCount.Text = specyfikacjeData.Count.ToString();

            // Suma netto ubojni
            decimal sumaNetto = 0;
            foreach (var row in specyfikacjeData)
            {
                if (decimal.TryParse(row.NettoUbojni?.Replace(" ", "").Replace(",", ""), out decimal netto))
                {
                    sumaNetto += netto;
                }
            }
            lblSumaNetto.Text = $"{sumaNetto:N0} kg";

            // Suma sztuk LUMEL
            int sumaSztuk = specyfikacjeData.Sum(r => r.LUMEL);
            lblSumaSztuk.Text = sumaSztuk.ToString("N0");
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
                string query = @"UPDATE dbo.FarmerCalc SET
                    CarLp = @Nr,
                    DeclI1 = @SztukiDek,
                    DeclI2 = @Padle,
                    DeclI3 = @CH,
                    DeclI4 = @NW,
                    DeclI5 = @ZM,
                    LumQnt = @LUMEL,
                    ProdQnt = @SztukiWybijak,
                    ProdWgt = @KgWybijak,
                    Price = @Cena,
                    Loss = @Ubytek,
                    IncDeadConf = @PiK
                    WHERE ID = @ID";

                using (SqlCommand cmd = new SqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@ID", row.ID);
                    cmd.Parameters.AddWithValue("@Nr", row.Nr);
                    cmd.Parameters.AddWithValue("@SztukiDek", row.SztukiDek);
                    cmd.Parameters.AddWithValue("@Padle", row.Padle);
                    cmd.Parameters.AddWithValue("@CH", row.CH);
                    cmd.Parameters.AddWithValue("@NW", row.NW);
                    cmd.Parameters.AddWithValue("@ZM", row.ZM);
                    cmd.Parameters.AddWithValue("@LUMEL", row.LUMEL);
                    cmd.Parameters.AddWithValue("@SztukiWybijak", row.SztukiWybijak);
                    cmd.Parameters.AddWithValue("@KgWybijak", row.KilogramyWybijak);
                    cmd.Parameters.AddWithValue("@Cena", row.Cena);
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

        private void GeneratePDFReport(List<int> ids)
        {
            Document doc = new Document(PageSize.A4.Rotate(), 40, 40, 15, 15);

            string sellerName = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(
                zapytaniasql.PobierzInformacjeZBazyDanych<string>(ids[0], "[LibraNet].[dbo].[FarmerCalc]", "CustomerRealGID"),
                "ShortName");
            DateTime dzienUbojowy = zapytaniasql.PobierzInformacjeZBazyDanych<DateTime>(
                ids[0], "[LibraNet].[dbo].[FarmerCalc]", "CalcDate");

            string strDzienUbojowy = dzienUbojowy.ToString("yyyy.MM.dd");
            string directoryPath = Path.Combine(@"\\192.168.0.170\Public\Przel\", strDzienUbojowy);

            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            string filePath = Path.Combine(directoryPath, $"{sellerName} {strDzienUbojowy}.pdf");

            using (FileStream fs = new FileStream(filePath, FileMode.Create))
            {
                PdfWriter writer = PdfWriter.GetInstance(doc, fs);
                doc.Open();

                // Nagłówek
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
                var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
                var boldFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);

                doc.Add(new Paragraph($"Specyfikacja dostaw - {sellerName}", headerFont));
                doc.Add(new Paragraph($"Data uboju: {strDzienUbojowy}", normalFont));
                doc.Add(new Paragraph(" "));

                // Tabela
                PdfPTable table = new PdfPTable(12);
                table.WidthPercentage = 100;
                table.SetWidths(new float[] { 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 });

                // Nagłówki
                AddTableHeader(table, "LP", boldFont);
                AddTableHeader(table, "Szt.Dek", boldFont);
                AddTableHeader(table, "Padłe", boldFont);
                AddTableHeader(table, "LUMEL", boldFont);
                AddTableHeader(table, "Brutto H", boldFont);
                AddTableHeader(table, "Tara H", boldFont);
                AddTableHeader(table, "Netto H", boldFont);
                AddTableHeader(table, "Brutto U", boldFont);
                AddTableHeader(table, "Tara U", boldFont);
                AddTableHeader(table, "Netto U", boldFont);
                AddTableHeader(table, "Cena", boldFont);
                AddTableHeader(table, "Ubytek", boldFont);

                // Dane
                foreach (var row in specyfikacjeData.Where(r => ids.Contains(r.ID)))
                {
                    AddTableData(table, normalFont,
                        row.Nr.ToString(),
                        row.SztukiDek.ToString(),
                        row.Padle.ToString(),
                        row.LUMEL.ToString(),
                        row.BruttoHodowcy ?? "-",
                        row.TaraHodowcy ?? "-",
                        row.NettoHodowcy ?? "-",
                        row.BruttoUbojni ?? "-",
                        row.TaraUbojni ?? "-",
                        row.NettoUbojni ?? "-",
                        row.Cena.ToString("F2"),
                        row.Ubytek.ToString("F2") + "%"
                    );
                }

                doc.Add(table);
                doc.Close();
            }

            MessageBox.Show($"Wygenerowano dokument PDF:\n{Path.GetFileName(filePath)}\n\nŚcieżka:\n{filePath}",
                "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AddTableHeader(PdfPTable table, string columnName, iTextSharp.text.Font font)
        {
            PdfPCell cell = new PdfPCell(new Phrase(columnName, font))
            {
                BackgroundColor = BaseColor.LIGHT_GRAY,
                HorizontalAlignment = Element.ALIGN_CENTER,
                Padding = 5
            };
            table.AddCell(cell);
        }

        private void AddTableData(PdfPTable table, iTextSharp.text.Font font, params string[] values)
        {
            foreach (string value in values)
            {
                PdfPCell cell = new PdfPCell(new Phrase(value, font))
                {
                    HorizontalAlignment = Element.ALIGN_CENTER,
                    Padding = 4
                };
                table.AddCell(cell);
            }
        }
    }

    // Klasa modelu danych
    public class SpecyfikacjaRow : INotifyPropertyChanged
    {
        private int _id;
        private int _nr;
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
            set { _sztukiDek = value; OnPropertyChanged(nameof(SztukiDek)); }
        }

        public int Padle
        {
            get => _padle;
            set { _padle = value; OnPropertyChanged(nameof(Padle)); }
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
            set { _cena = value; OnPropertyChanged(nameof(Cena)); }
        }

        public string TypCeny
        {
            get => _typCeny;
            set { _typCeny = value; OnPropertyChanged(nameof(TypCeny)); }
        }

        public bool PiK
        {
            get => _piK;
            set { _piK = value; OnPropertyChanged(nameof(PiK)); }
        }

        public decimal Ubytek
        {
            get => _ubytek;
            set { _ubytek = value; OnPropertyChanged(nameof(Ubytek)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
