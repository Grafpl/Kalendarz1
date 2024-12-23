using iTextSharp.text.pdf;
using iTextSharp.text;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Net;
using System.Windows.Forms;
using System.Globalization;
using DrawingFont = System.Drawing.Font;
using PdfFont = iTextSharp.text.Font;



namespace Kalendarz1
{

    public partial class WidokSpecyfikacje : Form
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        ZapytaniaSQL zapytaniasql = new ZapytaniaSQL(); // Tworzenie egzemplarza klasy ZapytaniaSQL
        decimal sumaWartosc = 0;
        decimal sumaKG = 0;
        DataGridViewRow selectedRow; // Zmienna przechowująca zaznaczony wiersz
        // Konstruktor z parametrem DateTime


        public WidokSpecyfikacje()
        {

            InitializeComponent();

        }

        private void WidokSpecyfikacje_Load(object sender, EventArgs e)
        {
            // Inicjalizacja formularza
            dateTimePicker1.ValueChanged += dateTimePicker1_ValueChanged;
        }


        private void dateTimePicker1_ValueChanged(object sender, EventArgs e)
        {
            // Obsługa zmiany daty w dateTimePicker1
            // Użyj tylko daty bez informacji o czasie
            LoadData(dateTimePicker1.Value.Date);
        }
        private void LoadData(DateTime selectedDate)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    SqlCommand command = new SqlCommand("SELECT ID, CarLp, CustomerGID, CustomerRealGID, DeclI1, DeclI2, DeclI3, DeclI4, DeclI5, LumQnt, ProdQnt, ProdWgt, " +
                        "FullFarmWeight, EmptyFarmWeight, NettoFarmWeight, FullWeight, EmptyWeight, NettoWeight, " +
                        "Price, PriceTypeID, IncDeadConf, Loss FROM [LibraNet].[dbo].[FarmerCalc] WHERE CalcDate = @SelectedDate Order By CarLP"
                        , connection);
                    command.Parameters.AddWithValue("@SelectedDate", selectedDate);

                    SqlDataAdapter adapter = new SqlDataAdapter(command);
                    DataTable dataTable = new DataTable();
                    adapter.Fill(dataTable);

                    // Clear existing data in DataGridView
                    dataGridView1.Rows.Clear();

                    // Populate DataGridView with data from database
                    if (dataTable.Rows.Count > 0)
                    {
                        foreach (DataRow row in dataTable.Rows)
                        {
                            string customerGID = ZapytaniaSQL.GetValueOrDefault<string>(row, "CustomerGID", defaultValue: "-1");
                            string customerRealGID = ZapytaniaSQL.GetValueOrDefault<string>(row, "CustomerRealGID", defaultValue: "-1");
                            string Dostawca = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(customerGID, "ShortName");
                            string RealDostawca = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(customerRealGID, "ShortName");

                            string BruttoHodowcy = row["FullFarmWeight"] != DBNull.Value ? Convert.ToDecimal(row["FullFarmWeight"]).ToString("#,0") : null;
                            string TaraHodowcy = row["EmptyFarmWeight"] != DBNull.Value ? Convert.ToDecimal(row["EmptyFarmWeight"]).ToString("#,0") : null;
                            string NettoHodowcy = row["NettoFarmWeight"] != DBNull.Value ? Convert.ToDecimal(row["NettoFarmWeight"]).ToString("#,0") : null;

                            string BruttoUbojni = row["FullWeight"] != DBNull.Value ? Convert.ToDecimal(row["FullWeight"]).ToString("#,0") : null;
                            string TaraUbojni = row["EmptyWeight"] != DBNull.Value ? Convert.ToDecimal(row["EmptyWeight"]).ToString("#,0") : null;
                            string NettoUbojni = row["NettoWeight"] != DBNull.Value ? Convert.ToDecimal(row["NettoWeight"]).ToString("#,0") : null;

                            int priceTypeID = ZapytaniaSQL.GetValueOrDefault<int>(row, "PriceTypeID", defaultValue: -1);
                            string typCeny = zapytaniasql.ZnajdzNazweCenyPoID(priceTypeID);

                            decimal ubytek = Math.Round(ZapytaniaSQL.GetValueOrDefault<decimal>(row, "Loss", defaultValue: 0) * 100, 2);

                            // Set the checkbox value for "PiK" based on "IncDeadConf"
                            bool incDeadConf = row["IncDeadConf"] != DBNull.Value && Convert.ToBoolean(row["IncDeadConf"]);

                            // Populate the DataGridView row
                            dataGridView1.Rows.Add(
                                row["ID"],
                                row["CarLp"],
                                Dostawca,
                                RealDostawca,
                                row["DeclI1"],
                                row["DeclI2"],
                                row["DeclI3"],
                                row["DeclI4"],
                                row["DeclI5"],
                                BruttoHodowcy,
                                TaraHodowcy,
                                NettoHodowcy,
                                BruttoUbojni,
                                TaraUbojni,
                                NettoUbojni,
                                row["LumQnt"],
                                row["ProdQnt"],
                                row["ProdWgt"],
                                row["Price"],
                                typCeny,
                                incDeadConf, // Add the checkbox value for "PiK"
                                ubytek
                            );
                        }
                    }
                    else
                    {
                        // Handle case where no data is returned
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Wystąpił błąd podczas ładowania danych: " + ex.Message);
            }
        }



        private void DataGridView1_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            // Sprawdź, czy wiersz i kolumna zostały kliknięte
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                // Pobierz wartość ID z klikniętego wiersza i przekonwertuj na int
                int idSpecyfikacja = Convert.ToInt32(dataGridView1.Rows[e.RowIndex].Cells["ID"].Value);


                // Wywołaj nowe okno Dostawa, przekazując wartość LP
                WidokAvilog dostawaForm = new WidokAvilog(idSpecyfikacja);
                dostawaForm.Show();
            }
        }
        private void dataGridView1_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                DataGridViewRow editedRow = dataGridView1.Rows[e.RowIndex];

                // Check if the changed cell is in the "PiK" column
                if (dataGridView1.Columns[e.ColumnIndex].Name == "PiK")
                {
                    int id = Convert.ToInt32(editedRow.Cells["ID"].Value);
                    bool newValue = Convert.ToBoolean(editedRow.Cells["PiK"].Value);

                    // Update the database with the new checkbox value
                    UpdateDatabase(id, e.ColumnIndex, newValue.ToString());

                    // Apply formatting based on the checkbox value
                    ApplyFormattingToRow(editedRow, newValue);
                }
            }
        }
        private void dataGridView1_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            DataGridViewRow row = dataGridView1.Rows[e.RowIndex];
            bool isChecked = Convert.ToBoolean(row.Cells["PiK"].Value);

            ApplyFormattingToRow(row, isChecked);
        }

        private void ApplyFormattingToRow(DataGridViewRow row, bool isChecked)
        {
            DataGridViewCellStyle style = new DataGridViewCellStyle();

            if (isChecked)
            {
                style.Font = new System.Drawing.Font(dataGridView1.Font, FontStyle.Strikeout); // Użyj System.Drawing.Font
                style.ForeColor = Color.Red;
            }
            else
            {
                style.Font = dataGridView1.Font;
                style.ForeColor = dataGridView1.ForeColor;
            }

            row.Cells["Padle"].Style = style; // Assuming DeclI2 is the "Padle" column
        }



        private void dataGridView1_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                // Sprawdź czy edytowana komórka jest w drugiej kolumnie i w pierwszym wierszu
                if (e.ColumnIndex == 1 && e.RowIndex == 0)
                {
                    // Sprawdź czy wpisano liczbę w pierwszym wierszu pierwszej kolumny
                    int newValue;
                    if (int.TryParse(dataGridView1.Rows[0].Cells[0].Value?.ToString(), out newValue))
                    {
                        // Inkrementuj liczbę w dół dla pozostałych wierszy
                        for (int i = 1; i < dataGridView1.Rows.Count; i++)
                        {
                            newValue++;
                            dataGridView1.Rows[i].Cells[1].Value = newValue;
                        }
                    }
                }
                else
                {
                    DataGridViewRow editedRow = dataGridView1.Rows[e.RowIndex];

                    // Pobierz ID z edytowanego wiersza
                    int id = Convert.ToInt32(editedRow.Cells["ID"].Value);

                    // Pobierz nową wartość z edytowanej komórki
                    string newValue = editedRow.Cells[e.ColumnIndex].Value.ToString();

                    // Zaktualizuj odpowiednią kolumnę w bazie danych
                    UpdateDatabase(id, e.ColumnIndex, newValue);
                }


            }
            catch (Exception ex)
            {
                MessageBox.Show("Wystąpił błąd podczas aktualizacji danych: " + ex.Message);
            }
        }




        private void UpdateDatabase(int id, int columnIndex, string newValue)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string columnName = GetColumnName(columnIndex); // Funkcja do pobrania nazwy kolumny na podstawie indeksu
                    string strSQL = $@"UPDATE dbo.FarmerCalc
                               SET {columnName} = @NewValue
                               WHERE ID = @ID";

                    using (SqlCommand command = new SqlCommand(strSQL, connection))
                    {
                        command.Parameters.AddWithValue("@ID", id);

                        // Check if the column is "Loss" and process the newValue accordingly
                        if (columnName == "Loss")
                        {
                            if (decimal.TryParse(newValue, out decimal lossValue))
                            {
                                lossValue /= 100;
                                command.Parameters.AddWithValue("@NewValue", lossValue);
                            }
                            else
                            {
                                MessageBox.Show("Nieprawidłowa wartość dla kolumny 'Loss'.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return;
                            }
                        }
                        else if (columnName == "IncDeadConf")
                        {
                            bool checkboxValue = Convert.ToBoolean(newValue);
                            command.Parameters.AddWithValue("@NewValue", checkboxValue);
                        }
                        else
                        {
                            command.Parameters.AddWithValue("@NewValue", newValue);
                        }

                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {

                        }
                        else
                        {
                            MessageBox.Show("Nie udało się zaktualizować danych.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Wystąpił błąd podczas aktualizacji danych: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string GetColumnName(int columnIndex)
        {
            // Funkcja zwracająca nazwę kolumny na podstawie indeksu
            switch (columnIndex)
            {
                case 1: return "CarLp"; // Załóżmy, że CarLp to nazwa kolumny w bazie danych
                case 4: return "DeclI1"; // Załóżmy, że DeclI1 to nazwa kolumny w bazie danych
                case 5: return "DeclI2"; // Załóżmy, że DeclI2 to nazwa kolumny w bazie danych Padłe
                case 6: return "DeclI3"; // Załóżmy, że DeclI3 to nazwa kolumny w bazie danych
                case 7: return "DeclI4"; // Załóżmy, że DeclI4 to nazwa kolumny w bazie danych
                case 8: return "DeclI5"; // Załóżmy, że DeclI5 to nazwa kolumny w bazie danych
                case 15: return "LumQnt"; // Załóżmy, że LumQnt to nazwa kolumny w bazie danych
                case 16: return "ProdQnt"; // Załóżmy, że ProdQnt to nazwa kolumny w bazie danych
                case 17: return "ProdWgt"; // Załóżmy, że ProdWgt to nazwa kolumny w bazie danych
                case 18: return "Price"; // Załóżmy, że ProdWgt to nazwa kolumny w bazie danych
                case 20: return "IncDeadConf"; // Załóżmy, że IncDeadConf to nazwa kolumny w bazie danych
                case 21: return "Loss"; // Załóżmy, że Loss to nazwa kolumny w bazie danych

                default: throw new ArgumentException("Nieprawidłowy indeks kolumny.");
            }
        }
        private void button1_Click(object sender, EventArgs e)
        {
            /*
            // Odczytaj wartość z DateTimePicker
            DateTime wybranaData = dateTimePicker1.Value;

            // Utwórz nowy formularz z przekazaną datą
            SzczegolyDrukowaniaSpecki PDFview = new SzczegolyDrukowaniaSpecki(wybranaData);

            // Wyświetl nowy formularz
            PDFview.Show();
            */



            ////


            if (selectedRow != null)
            {
                // Pobierz wartość CustomerRealGID z zaznaczonego wiersza
                string selectedCustomerRealGID = selectedRow.Cells["RealDostawca"].Value?.ToString();

                if (!string.IsNullOrEmpty(selectedCustomerRealGID))
                {

                    // Inicjalizuj pustą listę IDs (jest wyzerowana)
                    List<int> ids = new List<int>();


                    foreach (DataGridViewRow row in dataGridView1.Rows)
                    {
                        // Sprawdź, czy bieżący wiersz ma taką samą wartość CustomerRealGID
                        string customerRealGID = row.Cells["RealDostawca"].Value?.ToString();

                        if (!string.IsNullOrEmpty(customerRealGID) && customerRealGID.Equals(selectedCustomerRealGID))
                        {
                            // Pobierz ID i dodaj je do listy ids
                            int id;
                            if (int.TryParse(row.Cells["ID"].Value?.ToString(), out id))
                            {
                                ids.Add(id);
                            }
                        }
                    }

                    // Wywołaj metodę generowania raportu PDF z pobranymi identyfikatorami
                    GeneratePDFReport(ids);
                }
                else
                {
                    MessageBox.Show("Nieprawidłowa wartość CustomerRealGID dla zaznaczonego wiersza.");
                }
            }
            else
            {
                MessageBox.Show("Nie wybrano żadnego wiersza.");
            }
        }

        private void MoveRowUp()
        {
            if (dataGridView1.CurrentCell != null)
            {
                int rowIndex = dataGridView1.CurrentCell.RowIndex;
                if (rowIndex > 0)
                {
                    DataTable table = dataGridView1.DataSource as DataTable;
                    if (table != null)
                    {
                        DataRow row = table.NewRow();
                        row.ItemArray = table.Rows[rowIndex].ItemArray;
                        table.Rows.RemoveAt(rowIndex);
                        table.Rows.InsertAt(row, rowIndex - 1);
                        RefreshNumeration();
                        dataGridView1.ClearSelection();
                        dataGridView1.Rows[rowIndex - 1].Selected = true;
                        dataGridView1.CurrentCell = dataGridView1.Rows[rowIndex - 1].Cells[0];
                    }
                }
            }
        }

        private void MoveRowDown()
        {
            if (dataGridView1.CurrentCell != null)
            {
                int rowIndex = dataGridView1.CurrentCell.RowIndex;
                if (rowIndex < dataGridView1.Rows.Count - 2) // -2 to ignore the new row
                {
                    DataTable table = (DataTable)dataGridView1.DataSource;
                    if (table != null)
                    {
                        DataRow row = table.NewRow();
                        row.ItemArray = table.Rows[rowIndex].ItemArray;
                        table.Rows.RemoveAt(rowIndex);
                        table.Rows.InsertAt(row, rowIndex + 1);
                        RefreshNumeration();
                        dataGridView1.ClearSelection();
                        dataGridView1.Rows[rowIndex + 1].Selected = true;
                        dataGridView1.CurrentCell = dataGridView1.Rows[rowIndex + 1].Cells[0];
                    }
                }
            }
        }

        private void RefreshNumeration()
        {
            for (int i = 0; i < dataGridView1.Rows.Count - 1; i++)
            {
                dataGridView1.Rows[i].Cells["Nr"].Value = i + 1;
            }
        }


        private void btnLoadData_Click_1(object sender, EventArgs e)

        {
            string ftpUrl = "ftp://admin:wago@192.168.0.98/POMIARY.TXT";
            string content;

            // Pobierz zawartość pliku TXT z serwera FTP
            try
            {
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(ftpUrl);
                request.Method = WebRequestMethods.Ftp.DownloadFile;

                using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                using (Stream responseStream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(responseStream))
                {
                    content = reader.ReadToEnd();
                }

                // Podziel zawartość na wiersze
                string[] rows = content.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                // Utwórz listę do przechowywania danych
                List<string[]> data = new List<string[]>();
                DateTime selectedDate = dateTimePicker1.Value.Date;
                // Dodaj wiersze do listy danych
                foreach (string row in rows)
                {
                    // Podziel wiersz na kolumny
                    string[] columns = row.Split(';');
                    data.Add(columns);
                }

                // Ustaw dane w DataGrid tylko dla wybranej daty
                dataGridView2.Rows.Clear();
                dataGridView2.Columns.Clear(); // Wyczyść istniejące kolumny

                // Dodaj kolumny do DataGridView
                dataGridView2.Columns.Add("Column1", "Data");
                dataGridView2.Columns.Add("Column2", "Godzina Końca Partii");
                dataGridView2.Columns.Add("Column3", "Ilość sztuk");



                // Ustawienia regionalne do konwersji liczbowej
                var numberFormat = new System.Globalization.NumberFormatInfo();
                numberFormat.NumberDecimalSeparator = ".";  // Ustawienie kropki jako separatora dziesiętnego

                foreach (string[] row in data)
                {
                    // Sprawdź czy data w wierszu jest równa wybranej dacie
                    if (DateTime.TryParse(row[0], out DateTime rowDate) && rowDate.Date == selectedDate && row[2] != "0.0")
                    {
                        // Próba konwersji trzeciej kolumny na liczbową wartość zmiennoprzecinkową
                        if (double.TryParse(row[2], System.Globalization.NumberStyles.Any, numberFormat, out double quantity))
                        {
                            double roundedQuantity = Math.Ceiling(quantity);
                            string[] rowData = new string[] { row[0], row[1], roundedQuantity.ToString() };
                            dataGridView2.Rows.Add(rowData);
                        }
                        else
                        {
                            // Logowanie nieudanej próby konwersji
                            MessageBox.Show($"Nieprawidłowy format liczbowy w wierszu: {row[0]}, {row[1]}, {row[2]}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd podczas pobierania danych: " + ex.Message);
            }

        }

        private void buttonUP_Click(object sender, EventArgs e)
        {
            MoveRowUp();
        }

        private void buttonDown_Click(object sender, EventArgs e)
        {
            MoveRowDown();
        }
        private void GeneratePDFReport(List<int> ids)
        {
            // Set up the document in portrait mode
            Document doc = new Document(PageSize.A4.Rotate(), 40, 40, 15, 15);

            // Variables for the seller
            string sellerName = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(zapytaniasql.PobierzInformacjeZBazyDanych<string>(ids[0], "[LibraNet].[dbo].[FarmerCalc]", "CustomerRealGID"), "ShortName");
            DateTime dzienUbojowy = zapytaniasql.PobierzInformacjeZBazyDanych<DateTime>(ids[0], "[LibraNet].[dbo].[FarmerCalc]", "CalcDate");

            // Format the DateTime value to the desired string format
            string strDzienUbojowy = dzienUbojowy.ToString("yyyy.MM.dd");

            // Create directory path
            string directoryPath = Path.Combine(@"\\192.168.0.170\Public\Przel\", strDzienUbojowy);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            // Create file path
            string filePath = Path.Combine(directoryPath, $"{sellerName} {strDzienUbojowy}.pdf");

            using (FileStream fs = new FileStream(filePath, FileMode.Create))
            {
                PdfWriter writer = PdfWriter.GetInstance(doc, fs);
                doc.Open();

                // Load a BaseFont that supports Polish characters
                BaseFont baseFont = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1250, BaseFont.EMBEDDED);
                iTextSharp.text.Font headerFont = new iTextSharp.text.Font(baseFont, 15, iTextSharp.text.Font.BOLD);
                iTextSharp.text.Font textFont = new iTextSharp.text.Font(baseFont, 12, iTextSharp.text.Font.NORMAL);
                iTextSharp.text.Font smallTextFont = new iTextSharp.text.Font(baseFont, 8, iTextSharp.text.Font.NORMAL); // Small font for the data table
                iTextSharp.text.Font tytulTablicy = new iTextSharp.text.Font(baseFont, 13, iTextSharp.text.Font.BOLD);
                iTextSharp.text.Font ItalicFont = new iTextSharp.text.Font(baseFont, 8, iTextSharp.text.Font.ITALIC);
                iTextSharp.text.Font smallerTextFont = new iTextSharp.text.Font(baseFont, 7, iTextSharp.text.Font.NORMAL); // Small font for the data table

                // Header paragraph
                // Header paragraph
                Paragraph header = new Paragraph("Rozliczenie przyjętego drobiu", headerFont);
                header.Alignment = Element.ALIGN_CENTER;
                doc.Add(header);
                // Create a table for the seller and buyer info, each in its own cell
                PdfPTable infoTable = new PdfPTable(3); // Three columns for seller, delivery details, and buyer
                infoTable.WidthPercentage = 100;
                infoTable.SetWidths(new float[] { 1f, 1f, 1f }); // Equal width for all three columns

                // Seller information (first column)
                PdfPCell sellerInfoCell = new PdfPCell
                {
                    Border = PdfPCell.NO_BORDER,
                    HorizontalAlignment = Element.ALIGN_LEFT,
                    PaddingBottom = 0 // Remove the existing padding
                };

                // Split seller info into lines and add empty lines between them
                string[] sellerLines = { "Nabywca:", "Ubojnia Drobiu \"Piórkowscy\"", "Koziołki 40, 95-061 Dmosin", "NIP: 726-162-54-06", "", "" }; // Empty lines for spacing
                foreach (string line in sellerLines)
                {
                    sellerInfoCell.AddElement(new Phrase(line, textFont));
                }

                infoTable.AddCell(sellerInfoCell);

                // Delivery details (second column)
                PdfPCell deliveryInfoCell = new PdfPCell
                {
                    Border = PdfPCell.NO_BORDER,
                    HorizontalAlignment = Element.ALIGN_CENTER, // Align delivery info in the center
                    PaddingBottom = 0 // Remove the existing padding
                };

                // Variables for the seller
                string czyjaWaga = "Hodowca";
                //DateTime dzienUbojowy = zapytaniasql.PobierzInformacjeZBazyDanych<DateTime>(ids[0], "[LibraNet].[dbo].[FarmerCalc]", "CalcDate");

                // Format the DateTime value to the desired string format
                //string strDzienUbojowy = dzienUbojowy.ToString("yyyy.MM.dd");

                // Split delivery info into lines and add empty lines between them
                string[] deliveryLines = { "Szczegóły dostawy:", "Data Uboju " + strDzienUbojowy, "Waga loco " + czyjaWaga, "", "" }; // Empty lines for spacing
                foreach (string line in deliveryLines)
                {
                    deliveryInfoCell.AddElement(new Phrase(line, textFont));
                }

                infoTable.AddCell(deliveryInfoCell);

                // Seller details (third column)
                PdfPCell sellerDetailsCell = new PdfPCell
                {
                    Border = PdfPCell.NO_BORDER,
                    HorizontalAlignment = Element.ALIGN_RIGHT, // Align seller details to the right
                    PaddingBottom = 0 // Remove the existing padding
                };

                // Variables for the seller
                //string sellerName = zapytaniasql.PobierzInformacjeZBazyDanychHodowcow(zapytaniasql.PobierzInformacjeZBazyDanych<int>(ids[0], "[LibraNet].[dbo].[FarmerCalc]", "CustomerRealGID"), "ShortName");
                string sellerStreet = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(zapytaniasql.PobierzInformacjeZBazyDanych<string>(ids[0], "[LibraNet].[dbo].[FarmerCalc]", "CustomerRealGID"), "Address");
                string sellerKod = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(zapytaniasql.PobierzInformacjeZBazyDanych<string>(ids[0], "[LibraNet].[dbo].[FarmerCalc]", "CustomerRealGID"), "PostalCode");
                string sellerMiejsc = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(zapytaniasql.PobierzInformacjeZBazyDanych<string>(ids[0], "[LibraNet].[dbo].[FarmerCalc]", "CustomerRealGID"), "City");

                // Split seller details into lines and add empty lines between them
                string[] sellerDetailsLines = { "Sprzedający:", sellerName, sellerStreet, sellerKod + ", " + sellerMiejsc, "", "" }; // Empty lines for spacing
                foreach (string line in sellerDetailsLines)
                {
                    Phrase phrase = new Phrase();
                    phrase.Add(new Chunk(line, textFont));
                    sellerDetailsCell.AddElement(phrase);
                }

                infoTable.AddCell(sellerDetailsCell);

                // Add the table to the document
                doc.Add(infoTable);

                // Add a blank paragraph for spacing
                Paragraph spacing = new Paragraph(" ", textFont);
                spacing.SpacingBefore = 10f; // Adjust the spacing value as needed (in points)
                doc.Add(spacing);



                // Create the data table with adjusted column widths if necessary
                PdfPTable dataTable2 = new PdfPTable(new float[] { 0.1F, 0.3F, 0.3F, 0.25F, 0.25F, 0.25F, 0.25F, 0.25F, 0.3F, 0.3F, 0.3F, 0.3F, 0.20F, 0.3F, 0.20F, 0.3F });
                dataTable2.WidthPercentage = 100;

                // Add merged header for "Waga samochodowa"
                PdfPCell mergedHeaderCell11 = new PdfPCell(new Phrase("Informacje Transportowe", tytulTablicy));
                mergedHeaderCell11.Colspan = 5;
                mergedHeaderCell11.VerticalAlignment = Element.ALIGN_MIDDLE; // Wyprostowanie w pionie
                mergedHeaderCell11.HorizontalAlignment = Element.ALIGN_CENTER; // Wyprostowanie w poziomie
                dataTable2.AddCell(mergedHeaderCell11);

                // Add merged header for "Rozliczenie sztuk"
                PdfPCell mergedHeaderCell22 = new PdfPCell(new Phrase("Waga Hodowcy", tytulTablicy));
                mergedHeaderCell22.Colspan = 3;
                mergedHeaderCell22.VerticalAlignment = Element.ALIGN_MIDDLE; // Wyprostowanie w pionie
                mergedHeaderCell22.HorizontalAlignment = Element.ALIGN_CENTER; // Wyprostowanie w poziomie
                dataTable2.AddCell(mergedHeaderCell22);

                // Add merged header for "Rozliczenie sztuk"
                PdfPCell mergedHeaderCell44 = new PdfPCell(new Phrase("Waga Ubojni", tytulTablicy));
                mergedHeaderCell44.Colspan = 3;
                mergedHeaderCell44.VerticalAlignment = Element.ALIGN_MIDDLE; // Wyprostowanie w pionie
                mergedHeaderCell44.HorizontalAlignment = Element.ALIGN_CENTER; // Wyprostowanie w poziomie
                dataTable2.AddCell(mergedHeaderCell44);

                // Add merged header for "Rozliczenie kilogramów"
                PdfPCell mergedHeaderCell33 = new PdfPCell(new Phrase("Ubytki transportowe ustalone i wyliczone", tytulTablicy));
                mergedHeaderCell33.Colspan = 5;
                mergedHeaderCell33.VerticalAlignment = Element.ALIGN_MIDDLE; // Wyprostowanie w pionie
                mergedHeaderCell33.HorizontalAlignment = Element.ALIGN_CENTER; // Wyprostowanie w poziomie
                dataTable2.AddCell(mergedHeaderCell33);


                AddTableHeader(dataTable2, "Lp.", smallTextFont);
                AddTableHeader(dataTable2, "Nr. Auta", smallTextFont);
                AddTableHeader(dataTable2, "Nr. Naczepy", smallTextFont);
                AddTableHeader(dataTable2, "Czas Przyjazdu", smallTextFont);
                AddTableHeader(dataTable2, "Czas Załadunku", smallTextFont);

                AddTableHeader(dataTable2, "Hodowca Brutto", smallTextFont);
                AddTableHeader(dataTable2, "Hodowca Tara", smallTextFont);
                AddTableHeader(dataTable2, "Hodowca Netto", smallTextFont);
                AddTableHeader(dataTable2, "Ubojnia Brutto", smallTextFont);
                AddTableHeader(dataTable2, "Ubojnia Tara", smallTextFont);
                AddTableHeader(dataTable2, "Ubojnia Netto", smallTextFont);


                // Add individual headers for remaining columns
                AddTableHeader(dataTable2, "Ubytek wyliczony [KG]", smallTextFont);
                AddTableHeader(dataTable2, "Ubytek wyliczony [%]", smallTextFont);
                AddTableHeader(dataTable2, "Ubytek ustalony [KG]", smallTextFont);
                AddTableHeader(dataTable2, "Ubytek ustalony [%]", smallTextFont);
                AddTableHeader(dataTable2, "Różnica", smallTextFont);


                for (int i = 0; i < ids.Count; i++)
                {
                    int id = ids[i];

                    // Tutaj możesz pobrać dane z bazy danych na podstawie ID
                    // Załóżmy, że dane te pochodzą z obiektu o nazwie 'data' otrzymanego z bazy danych

                    string numerAuta = zapytaniasql.PobierzInformacjeZBazyDanychKonkretneJakiejkolwiek(id, "[LibraNet].[dbo].[FarmerCalc]", "CarID");
                    string NumerNaczepy = zapytaniasql.PobierzInformacjeZBazyDanychKonkretneJakiejkolwiek(id, "[LibraNet].[dbo].[FarmerCalc]", "TrailerID");

                    DateTime godzinaDojazdy = zapytaniasql.PobierzInformacjeZBazyDanych<DateTime>(id, "[LibraNet].[dbo].[FarmerCalc]", "DojazdHodowca");
                    string strGodzinaDojazdy = godzinaDojazdy.ToString("HH:mm");

                    DateTime godzinaZaladunku = zapytaniasql.PobierzInformacjeZBazyDanych<DateTime>(id, "[LibraNet].[dbo].[FarmerCalc]", "Zaladunek");
                    string strGodzinaZaladunku = godzinaZaladunku.ToString("HH:mm");



                    // Waga Ubojnia
                    Decimal WagaUbojniaBrutto = zapytaniasql.PobierzInformacjeZBazyDanych<Decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "FullWeight");
                    Decimal WagaUbojniaTara = zapytaniasql.PobierzInformacjeZBazyDanych<Decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "EmptyWeight");
                    Decimal WagaUbojniaNetto = zapytaniasql.PobierzInformacjeZBazyDanych<Decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "NettoWeight");
                    string strWagaUbojniaBrutto = WagaUbojniaBrutto.ToString("N0") + " kg";
                    string strWagaUbojniaTara = WagaUbojniaTara.ToString("N0") + " kg";
                    string strWagaUbojniaNetto = WagaUbojniaNetto.ToString("N0") + " kg";

                    // Waga Hodowca
                    Decimal WagaHodowcaBrutto = zapytaniasql.PobierzInformacjeZBazyDanych<Decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "FullFarmWeight");
                    Decimal WagaHodowcaTara = zapytaniasql.PobierzInformacjeZBazyDanych<Decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "EmptyFarmWeight");
                    Decimal WagaHodowcaNetto = zapytaniasql.PobierzInformacjeZBazyDanych<Decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "NettoFarmWeight");
                    string strWagaHodowcaBrutto = WagaHodowcaBrutto.ToString("N0") + " kg";
                    string strWagaHodowcaTara = WagaHodowcaTara.ToString("N0") + " kg";
                    string strWagaHodowcaNetto = WagaHodowcaNetto.ToString("N0") + " kg";
                    // Pobierz ubytekUstalonyProcent z bazy danych
                    Decimal? ubytekUstalonyProcentNullable = zapytaniasql.PobierzInformacjeZBazyDanych<Decimal?>(id, "[LibraNet].[dbo].[FarmerCalc]", "Loss");
                    Decimal ubytekUstalonyProcent = ubytekUstalonyProcentNullable ?? 0;

                    // Deklaracja zmiennych poza blokiem if-else z wartościami domyślnymi
                    Decimal ubytekWyliczonyKG = 0;
                    string strUbytekWyliczonyKG = "0 kg";
                    Decimal ubytekWyliczonyProcent = 0;
                    Decimal ubytekWyliczony = 0;
                    string strUbytekWyliczony = "0.00 %";
                    Decimal ubytekUstalony = 0;
                    string strUbytekUstalony = "0.00 %";
                    Decimal ubytekUstalonyKG = 0;
                    string strUbytekUstalonyKG = "0 kg";
                    Decimal ubytekRoznicaKG = 0;
                    string strubytekRoznicaKG = "0 kg";

                    if (ubytekUstalonyProcent != 0)
                    {
                        // Ubytek Wyliczony
                        ubytekWyliczonyKG = WagaHodowcaNetto - WagaUbojniaNetto;
                        strUbytekWyliczonyKG = ubytekWyliczonyKG.ToString("N0") + " kg";
                        ubytekWyliczonyProcent = ubytekWyliczonyKG / WagaHodowcaNetto;
                        ubytekWyliczony = ubytekWyliczonyProcent * 100;
                        strUbytekWyliczony = ubytekWyliczony.ToString("0.00") + " %";

                        // Ubytek Ustalony
                        ubytekUstalony = ubytekUstalonyProcent * 100;
                        strUbytekUstalony = ubytekUstalony.ToString("0.00") + " %";
                        ubytekUstalonyKG = WagaHodowcaNetto * ubytekUstalonyProcent;
                        strUbytekUstalonyKG = ubytekUstalonyKG.ToString("N0") + " kg";

                        // Ubytek Różnica
                        ubytekRoznicaKG = ubytekWyliczonyKG - ubytekUstalonyKG;
                        strubytekRoznicaKG = ubytekRoznicaKG.ToString("N0") + " kg";
                    }

                    // Dodaj pobrane dane do tabeli danych
                    AddTableData(dataTable2, smallTextFont, (i + 1).ToString(), numerAuta, NumerNaczepy, strGodzinaDojazdy, strGodzinaZaladunku, strWagaHodowcaBrutto, strWagaHodowcaTara, strWagaHodowcaNetto, strWagaUbojniaBrutto, strWagaUbojniaTara, strWagaUbojniaNetto, strUbytekWyliczonyKG, strUbytekWyliczony, strUbytekUstalonyKG, strUbytekUstalony, strubytekRoznicaKG);

                }




                dataTable2.SpacingAfter = 10f;
                // Add the data table to the document
                doc.Add(dataTable2);


                // Create the data table with adjusted column widths if necessary
                PdfPTable dataTable = new PdfPTable(new float[] { 0.1F, 0.3F, 0.3F, 0.3F, 0.25F, 0.25F, 0.25F, 0.25F, 0.3F, 0.3F, 0.3F, 0.3F, 0.3F, 0.3F, 0.3F, 0.3F, 0.3F, 0.35F });
                dataTable.WidthPercentage = 100;

                // Add merged header for "Waga samochodowa"
                PdfPCell mergedHeaderCell1 = new PdfPCell(new Phrase("Waga samochodowa", tytulTablicy));
                mergedHeaderCell1.Colspan = 4;
                mergedHeaderCell1.VerticalAlignment = Element.ALIGN_MIDDLE; // Wyprostowanie w pionie
                mergedHeaderCell1.HorizontalAlignment = Element.ALIGN_CENTER; // Wyprostowanie w poziomie
                dataTable.AddCell(mergedHeaderCell1);
                // Add merged header for "Rozliczenie sztuk"
                PdfPCell mergedHeaderCell2 = new PdfPCell(new Phrase("Rozliczenie sztuk", tytulTablicy));
                mergedHeaderCell2.Colspan = 5;
                mergedHeaderCell2.VerticalAlignment = Element.ALIGN_MIDDLE; // Wyprostowanie w pionie
                mergedHeaderCell2.HorizontalAlignment = Element.ALIGN_CENTER; // Wyprostowanie w poziomie
                dataTable.AddCell(mergedHeaderCell2);
                // Add merged header for "Rozliczenie kilogramów"
                PdfPCell mergedHeaderCell3 = new PdfPCell(new Phrase("Rozliczenie kilogramów", tytulTablicy));
                mergedHeaderCell3.Colspan = 9;
                mergedHeaderCell3.VerticalAlignment = Element.ALIGN_MIDDLE; // Wyprostowanie w pionie
                mergedHeaderCell3.HorizontalAlignment = Element.ALIGN_CENTER; // Wyprostowanie w poziomie
                dataTable.AddCell(mergedHeaderCell3);

                AddTableHeader(dataTable, "Lp.", smallTextFont);
                AddTableHeader(dataTable, "Waga Brutto", smallTextFont);
                AddTableHeader(dataTable, "Waga Tara", smallTextFont);
                AddTableHeader(dataTable, "Waga Netto", smallTextFont);

                AddTableHeader(dataTable, "Sztuki Całość (ARiMR)", smallTextFont);
                AddTableHeader(dataTable, "Średnia Waga", smallTextFont);
                AddTableHeader(dataTable, "Sztuki Padłe", smallTextFont);
                AddTableHeader(dataTable, "Sztuki Konfiskaty", smallTextFont);
                AddTableHeader(dataTable, "Sztuki Zdatne", smallTextFont);


                // Add individual headers for remaining columns
                AddTableHeader(dataTable, "Netto [KG]", smallTextFont);
                AddTableHeader(dataTable, "Padłe [KG]", smallTextFont);
                AddTableHeader(dataTable, "Konfiskaty [KG]", smallTextFont);
                AddTableHeader(dataTable, "Opasienie [KG]", smallTextFont);
                AddTableHeader(dataTable, "Ubytek [KG]", smallTextFont);
                AddTableHeader(dataTable, "Klasa B [KG]", smallTextFont);
                AddTableHeader(dataTable, "Kilogramy do Zapłaty", smallTextFont);
                AddTableHeader(dataTable, "Cena netto", smallTextFont);
                AddTableHeader(dataTable, "Wartość netto", smallTextFont);


                // Add sample rows to the data table
                for (int i = 0; i < ids.Count; i++)
                {
                    int id = ids[i];

                    bool czyUpadkiIKonfiskaty = zapytaniasql.PobierzInformacjeZBazyDanych<bool>(id, "[LibraNet].[dbo].[FarmerCalc]", "IncDeadConf");


                    // Pobierz ubytekUstalonyProcent z bazy danych
                    Decimal? ubytekUstalonyProcentNullable = zapytaniasql.PobierzInformacjeZBazyDanych<Decimal?>(id, "[LibraNet].[dbo].[FarmerCalc]", "Loss");
                    Decimal ubytekUstalonyProcent = ubytekUstalonyProcentNullable ?? 0;

                    // Deklaracja zmiennych dla wagi
                    Decimal WagaHodowcaBrutto = 0;
                    Decimal WagaHodowcaTara = 0;
                    Decimal WagaHodowcaNetto = 0;
                    string strWagaHodowcaBrutto = "0 kg";
                    string strWagaHodowcaTara = "0 kg";
                    string strWagaHodowcaNetto = "0 kg";

                    if (ubytekUstalonyProcent != 0)
                    {
                        // Waga Hodowca
                        WagaHodowcaBrutto = zapytaniasql.PobierzInformacjeZBazyDanych<Decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "FullFarmWeight");
                        WagaHodowcaTara = zapytaniasql.PobierzInformacjeZBazyDanych<Decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "EmptyFarmWeight");
                        WagaHodowcaNetto = zapytaniasql.PobierzInformacjeZBazyDanych<Decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "NettoFarmWeight");
                        strWagaHodowcaBrutto = WagaHodowcaBrutto.ToString("N0") + " kg";
                        strWagaHodowcaTara = WagaHodowcaTara.ToString("N0") + " kg";
                        strWagaHodowcaNetto = WagaHodowcaNetto.ToString("N0") + " kg";
                    }
                    else
                    {
                        // Waga Ubojnia
                        WagaHodowcaBrutto = zapytaniasql.PobierzInformacjeZBazyDanych<Decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "FullWeight");
                        WagaHodowcaTara = zapytaniasql.PobierzInformacjeZBazyDanych<Decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "EmptyWeight");
                        WagaHodowcaNetto = zapytaniasql.PobierzInformacjeZBazyDanych<Decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "NettoWeight");
                        strWagaHodowcaBrutto = WagaHodowcaBrutto.ToString("N0") + " kg";
                        strWagaHodowcaTara = WagaHodowcaTara.ToString("N0") + " kg";
                        strWagaHodowcaNetto = WagaHodowcaNetto.ToString("N0") + " kg";

                    }


                    // Konfiskaty
                    int konfiskaty1 = zapytaniasql.PobierzInformacjeZBazyDanych<int>(id, "[LibraNet].[dbo].[FarmerCalc]", "DeclI4");
                    int konfiskaty2 = zapytaniasql.PobierzInformacjeZBazyDanych<int>(id, "[LibraNet].[dbo].[FarmerCalc]", "DeclI3");
                    int konfiskaty3 = zapytaniasql.PobierzInformacjeZBazyDanych<int>(id, "[LibraNet].[dbo].[FarmerCalc]", "DeclI5");
                    int konfiskatySuma = konfiskaty1 + konfiskaty2 + konfiskaty3;
                    string strKonfiskatySuma = konfiskatySuma.ToString("N0") + " szt";

                    // Padle
                    int padle = zapytaniasql.PobierzInformacjeZBazyDanych<int>(id, "[LibraNet].[dbo].[FarmerCalc]", "DeclI2");
                    string strPadle = padle.ToString() + " szt";

                    // Sztuki Zdatne
                    int sztZdatne = zapytaniasql.PobierzInformacjeZBazyDanych<int>(id, "[LibraNet].[dbo].[FarmerCalc]", "LumQnt") - konfiskatySuma;
                    string strSztZdatne = sztZdatne.ToString() + " szt";

                    // Sztuki Wszystkie
                    int sztWszystkie = konfiskatySuma + padle + sztZdatne;
                    string strSztWszystkie = sztWszystkie.ToString() + " szt";

                    // Średnia waga

                    Decimal sredniaWaga = WagaHodowcaNetto / (padle + konfiskatySuma + sztWszystkie);
                    string strSredniaWaga = sredniaWaga.ToString("0.00") + " kg";

                    // KG Padłe
                    decimal padleKG;
                    if (czyUpadkiIKonfiskaty == false)
                    {
                        padleKG = Math.Round(padle * sredniaWaga, 0, MidpointRounding.AwayFromZero); // Rounding to the nearest whole number
                    }

                    else
                    {
                        padleKG = 0;
                    }

                    string strPadleKG = "- " + Math.Round(padleKG, MidpointRounding.AwayFromZero).ToString("N0") + " kg";

                    // KG Padłe
                    decimal konfiskatySumaKG;
                    if (czyUpadkiIKonfiskaty == false)
                    {
                        konfiskatySumaKG = Math.Round(konfiskatySuma * sredniaWaga, 0, MidpointRounding.AwayFromZero); // Rounding to the nearest whole number

                    }
                    else
                    {
                        konfiskatySumaKG = 0;
                    }
                    string strKonfiskatySumaKG = "- " + Math.Round(konfiskatySumaKG, MidpointRounding.AwayFromZero).ToString("N0") + " kg";

                    // KG Opasienie
                    decimal opasienieKG = zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "Opasienie");
                    opasienieKG = Math.Round(opasienieKG, 0, MidpointRounding.AwayFromZero); // Rounding to the nearest whole number
                    string strOpasienieKG = "- " + Math.Round(opasienieKG, MidpointRounding.AwayFromZero).ToString("N0") + " kg";

                    // KG uUbytek

                    // Ubytek Ustalony
                    ubytekUstalonyProcent = zapytaniasql.PobierzInformacjeZBazyDanych<Decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "Loss");
                    Decimal ubytekUstalonyKG = WagaHodowcaNetto * ubytekUstalonyProcent;
                    ubytekUstalonyKG = Math.Round(ubytekUstalonyKG, 0, MidpointRounding.AwayFromZero); // Rounding to the nearest whole number
                    string strUbytekUstalonyKG = "- " + ubytekUstalonyKG.ToString("N0") + " kg";


                    // KG Klasa B
                    decimal klasaB = zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "KlasaB");
                    string strKlasaB = "- " + Math.Round(klasaB, MidpointRounding.AwayFromZero).ToString("N0") + " kg";

                    // Suma KG do Zapłaty


                    decimal sumaDoZaplaty;

                    if (czyUpadkiIKonfiskaty == false)
                    {
                        sumaDoZaplaty = WagaHodowcaNetto - padleKG - konfiskatySumaKG - opasienieKG - ubytekUstalonyKG - klasaB;
                    }
                    else
                    {
                        sumaDoZaplaty = WagaHodowcaNetto - opasienieKG - ubytekUstalonyKG - klasaB;
                    }

                    string strSumaDoZaplaty = Math.Round(sumaDoZaplaty, MidpointRounding.AwayFromZero).ToString("N0") + " kg";



                    // Cena
                    decimal Cena = zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "Price");
                    string strCena = Cena.ToString("0.00") + " zł/kg";

                    // Wartosc
                    decimal Wartosc = Cena * (WagaHodowcaNetto - padleKG - konfiskatySumaKG - opasienieKG - klasaB - ubytekUstalonyKG
                        );
                    string strWartosc = Math.Round(Wartosc, MidpointRounding.AwayFromZero).ToString("N0") + " zł";


                    sumaWartosc = Wartosc + sumaWartosc;
                    sumaKG = sumaDoZaplaty + sumaKG;



                    AddTableData(dataTable, smallTextFont, (i + 1).ToString(), strWagaHodowcaBrutto, strWagaHodowcaTara, strWagaHodowcaNetto, strSztWszystkie, strSredniaWaga, strPadle, strKonfiskatySuma, strSztZdatne, strWagaHodowcaNetto, strPadleKG, strKonfiskatySumaKG, strOpasienieKG, strUbytekUstalonyKG, strKlasaB, strSumaDoZaplaty, strCena, strWartosc);
                }
                string strSumaWartosc = Math.Round(sumaWartosc, MidpointRounding.AwayFromZero).ToString("N0") + " zł";
                string strSumaKG = Math.Round(sumaKG, MidpointRounding.AwayFromZero).ToString("N0") + " kg";
                int intTypCeny = zapytaniasql.PobierzInformacjeZBazyDanych<int>(ids[0], "[LibraNet].[dbo].[FarmerCalc]", "PriceTypeID");
                string typCeny = zapytaniasql.ZnajdzNazweCenyPoID(intTypCeny);


                doc.Add(dataTable);

                // Create a paragraph for italic text
                Paragraph italicText2 = new Paragraph("W celu uproszczenia wyliczeń, waga kurczaka wyrażona w kilogramach będzie zaokrąglana do pełnych kilogramów.", ItalicFont);
                italicText2.Alignment = Element.ALIGN_CENTER;

                // Add italic text to the document
                doc.Add(italicText2);


                // Cena
                //Paragraph cena = new Paragraph("Cena : 4,57 zł/kg", textFont);
                //cena.Alignment = Element.ALIGN_RIGHT;
                //doc.Add(cena);

                // TypCeny
                Paragraph typCena = new Paragraph($"Typ Ceny : {typCeny}", ItalicFont);
                typCena.Alignment = Element.ALIGN_RIGHT;
                doc.Add(typCena);

                // Suma KG
                Paragraph summaryKG = new Paragraph($"Suma kilogramów: {strSumaKG}", textFont);
                summaryKG.Alignment = Element.ALIGN_RIGHT;
                doc.Add(summaryKG);


                // Summary
                Paragraph summaryZL = new Paragraph($"Suma wartości netto: {strSumaWartosc}", textFont);
                summaryZL.Alignment = Element.ALIGN_RIGHT;
                doc.Add(summaryZL);

                // Summary
                // Paragraph platnosc = new Paragraph("Termin płatności : 45 dni", ItalicFont);
                //platnosc.Alignment = Element.ALIGN_RIGHT;
                // doc.Add(platnosc);
                sumaWartosc = 0;
                sumaKG = 0;
                

                // Close the document
                doc.Close();
                
            }
            MessageBox.Show($"Wygenerowano dokument PDF o nazwie: {Path.GetFileName(filePath)}\nŚcieżka: {filePath}", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // Method to add table headers
        private void AddTableHeader(PdfPTable table, string columnName, iTextSharp.text.Font font)
        {
            PdfPCell cell = new PdfPCell(new Phrase(columnName, font))
            {
                BackgroundColor = BaseColor.LIGHT_GRAY,
                HorizontalAlignment = Element.ALIGN_CENTER
            };
            table.AddCell(cell);
        }

        // Method to add table data
        private void AddTableData(PdfPTable table, iTextSharp.text.Font font, params string[] values)
        {
            foreach (string value in values)
            {
                PdfPCell cell = new PdfPCell(new Phrase(value, font))
                {
                    HorizontalAlignment = Element.ALIGN_CENTER
                };
                table.AddCell(cell);
            }
        }

        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            // Sprawdzenie, czy kliknięto na wiersz (jeśli e.RowIndex jest większe lub równe zero)
            if (e.RowIndex >= 0)
            {
                // Pobranie zaznaczonego wiersza
                selectedRow = dataGridView1.Rows[e.RowIndex];

                // Zaznaczenie całego wiersza
                selectedRow.Selected = true;
            }
        }
    }
}