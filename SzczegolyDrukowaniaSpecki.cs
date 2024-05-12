using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Windows.Forms;

namespace Kalendarz1
{
    public partial class SzczegolyDrukowaniaSpecki : Form
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        DataGridViewRow selectedRow; // Zmienna przechowująca zaznaczony wiersz
        ZapytaniaSQL zapytaniasql = new ZapytaniaSQL(); // Tworzenie egzemplarza klasy ZapytaniaSQL
        // Konstruktor z parametrem DateTime
        public SzczegolyDrukowaniaSpecki(DateTime data)
        {
            InitializeComponent();

            // Ustaw wartość DateTimePicker na tej formie (opcjonalne)
            dateTimePicker1.Value = data;

            dataGridView1.CellClick += DataGridView1_CellClick;
            // Wyświetl dane na podstawie przekazanej daty
            PokazWiersze(data);
        }

        // Funkcja pobierająca dane z bazy i wyświetlająca je w DataGridView
        private void PokazWiersze(DateTime data)
        {
            string query = @"
            SELECT  CalcDate, ID, CustomerRealGid
            FROM [LibraNet].[dbo].[FarmerCalc]
            WHERE CAST(CalcDate AS DATE) = @CalcDate";

            try
            {
                // Inicjalizuj połączenie SQL
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    // Przygotuj komendę SQL z parametrem
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        // Dodaj parametr do zapytania
                        cmd.Parameters.Add("@CalcDate", SqlDbType.Date).Value = data.Date;

                        // Otwórz połączenie
                        conn.Open();

                        // Wykonaj zapytanie i odbierz dane
                        using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                        {
                            // Wypełnij dane w DataTable
                            DataTable dt = new DataTable();
                            adapter.Fill(dt);

                            // Wyświetl dane w kontrolce DataGridView
                            dataGridView1.DataSource = dt;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Wyświetl błąd w razie problemu z połączeniem lub kwerendą
                MessageBox.Show($"Błąd podczas pobierania danych: {ex.Message}");
            }
        }
        private void PrintButton_Click(object sender, EventArgs e)
        {
            if (selectedRow != null)
            {
                // Pobierz wartość CustomerRealGID z zaznaczonego wiersza
                string selectedCustomerRealGID = selectedRow.Cells["CustomerRealGID"].Value?.ToString();

                if (!string.IsNullOrEmpty(selectedCustomerRealGID))
                {
                    // Pobierz wszystkie wartości ID z kolumny "ID" dla wierszy z tym samym CustomerRealGID
                    List<int> ids = new List<int>();

                    foreach (DataGridViewRow row in dataGridView1.Rows)
                    {
                        // Sprawdź, czy bieżący wiersz ma taką samą wartość CustomerRealGID
                        string customerRealGID = row.Cells["CustomerRealGID"].Value?.ToString();

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

        private void DataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
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

        private void GeneratePDFReport(List<int> ids)
        {
            // Set up the document in portrait mode
            Document doc = new Document(PageSize.A4.Rotate(), 40, 40, 15, 15);
            string filePath = @"\\192.168.0.170\Public\Przel\raport.pdf";

            using (FileStream fs = new FileStream(filePath, FileMode.Create))
            {
                PdfWriter writer = PdfWriter.GetInstance(doc, fs);
                doc.Open();

                // Load a BaseFont that supports Polish characters
                BaseFont baseFont = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1250, BaseFont.EMBEDDED);
                Font headerFont = new Font(baseFont, 15, iTextSharp.text.Font.BOLD);
                Font textFont = new Font(baseFont, 12, iTextSharp.text.Font.NORMAL);
                Font smallTextFont = new Font(baseFont, 8, iTextSharp.text.Font.NORMAL); // Small font for the data table
                Font tytulTablicy = new Font(baseFont, 13, iTextSharp.text.Font.BOLD);
                Font ItalicFont = new Font(baseFont, 8, iTextSharp.text.Font.ITALIC);
                Font smallerTextFont = new Font(baseFont, 7, iTextSharp.text.Font.NORMAL); // Small font for the data table

                // Header paragraph
                Paragraph header = new Paragraph("Rozliczenie przyjętego drobiu", headerFont);
                header.Alignment = Element.ALIGN_CENTER;
                doc.Add(header);

                // Create a table for the seller and buyer info, each in its own cell
                PdfPTable infoTable = new PdfPTable(2);
                infoTable.WidthPercentage = 100;
                infoTable.SetWidths(new float[] { 1f, 1f });

                // Seller information (left column)
                // Seller information (left column)
                PdfPCell sellerInfoCell = new PdfPCell(new Phrase("Ubojnia Drobiu Piórkowscy\nAdres: Koziołki 40, Dmosin\nNIP: 726-162-54-06", textFont))
                {
                    Border = PdfPCell.NO_BORDER,
                    HorizontalAlignment = Element.ALIGN_LEFT,
                    PaddingBottom = 0 // Remove the existing padding
                };

                // Split seller info into lines and add empty lines between them
                string[] sellerLines = { "Ubojnia Drobiu Piórkowscy", "Koziołki 40, Dmosin", "NIP: 726-162-54-06", "", "" }; // Empty lines for spacing
                foreach (string line in sellerLines)
                {
                    sellerInfoCell.AddElement(new Phrase(line, textFont));
                }

                infoTable.AddCell(sellerInfoCell);

                // Buyer information (right column)
                string buyerInfo = $"Nabywca:\nImię: \nNazwisko: ";
                PdfPCell buyerInfoCell = new PdfPCell(new Phrase(buyerInfo, textFont))
                {
                    Border = PdfPCell.NO_BORDER,
                    HorizontalAlignment = Element.ALIGN_RIGHT,
                    PaddingBottom = 0 // Remove the existing padding
                };

                // Split buyer info into lines and add empty lines between them
                string[] buyerLines = { "Nabywca:", $"", $"", "", "" }; // Empty lines for spacing
                foreach (string line in buyerLines)
                {
                    buyerInfoCell.AddElement(new Phrase(line, textFont));
                }

                infoTable.AddCell(buyerInfoCell);


                // Add the info table to the document
                infoTable.SpacingAfter = 20f;
                doc.Add(infoTable);

                // Create the data table with adjusted column widths if necessary
                PdfPTable dataTable2 = new PdfPTable(new float[] { 0.1F, 0.3F, 0.3F, 0.3F, 0.25F, 0.25F, 0.25F, 0.25F, 0.3F, 0.40F, 0.3F, 0.3F, 0.3F, 0.4F, 0.20F, 0.5F });
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

                    // Waga Ubojnia
                    Decimal WagaUbojniaBrutto = zapytaniasql.PobierzInformacjeZBazyDanych<Decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "FullWeight");
                    Decimal WagaUbojniaTara = zapytaniasql.PobierzInformacjeZBazyDanych<Decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "EmptyWeight");
                    Decimal WagaUbojniaNetto = zapytaniasql.PobierzInformacjeZBazyDanych<Decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "NettoWeight");
                    string strWagaUbojniaBrutto = WagaUbojniaBrutto.ToString("0") + " kg";
                    string strWagaUbojniaTara = WagaUbojniaTara.ToString("0") + " kg";
                    string strWagaUbojniaNetto = WagaUbojniaNetto.ToString("0") + " kg";

                    // Waga Hodowca
                    Decimal WagaHodowcaBrutto = zapytaniasql.PobierzInformacjeZBazyDanych<Decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "FullFarmWeight");
                    Decimal WagaHodowcaTara = zapytaniasql.PobierzInformacjeZBazyDanych<Decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "EmptyFarmWeight");
                    Decimal WagaHodowcaNetto = zapytaniasql.PobierzInformacjeZBazyDanych<Decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "NettoFarmWeight");
                    string strWagaHodowcaBrutto = WagaHodowcaBrutto.ToString("0") + " kg";
                    string strWagaHodowcaTara = WagaHodowcaTara.ToString("0") + " kg";
                    string strWagaHodowcaNetto = WagaHodowcaNetto.ToString("0") + " kg";
                    // Ubytek Wyliczony
                    Decimal ubytekWyliczonyKG = WagaHodowcaNetto - WagaUbojniaNetto;
                    string strUbytekWyliczonyKG = ubytekWyliczonyKG.ToString();
                    //Decimal ubytekWyliczony = ubytekWyliczonyKG - WagaUbojniaNetto;
                    //string strUbytekWyliczonyKG = ubytekWyliczonyKG.ToString();

                    // Ubytek Ustalony
                    Decimal ubytekUstalony = zapytaniasql.PobierzInformacjeZBazyDanych<Decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "Loss");
                    string strUbytekUstalony = ubytekUstalony.ToString();
                    Decimal ubytekUstalonyKG = WagaHodowcaNetto * ubytekUstalony;
                    string strUbytekUstalonyKG = ubytekUstalonyKG.ToString();

                    // Ubytek Różnica
                    Decimal roznicaUbytek = WagaHodowcaNetto * ubytekUstalony;
                    string strRoznicaUbytek = roznicaUbytek.ToString();


                    // Dodaj pobrane dane do tabeli danych
                    AddTableData(dataTable2, smallTextFont, (i + 1).ToString(), numerAuta, NumerNaczepy, "sa:45", "00:23", strWagaHodowcaBrutto, strWagaHodowcaTara, strWagaHodowcaNetto, strWagaUbojniaBrutto, strWagaUbojniaTara, strWagaUbojniaNetto, strUbytekWyliczonyKG, "2", strUbytekUstalonyKG, strUbytekUstalony, "60");
                }




                dataTable2.SpacingAfter = 10f;
                // Add the data table to the document
                doc.Add(dataTable2);


                // Create the data table with adjusted column widths if necessary
                PdfPTable dataTable = new PdfPTable(new float[] { 0.1F, 0.3F, 0.3F, 0.3F, 0.25F, 0.25F, 0.25F, 0.25F, 0.3F, 0.40F, 0.3F, 0.3F, 0.3F, 0.4F, 0.2F, 0.3F, 0.20F, 0.5F });
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

                AddTableHeader(dataTable, "Sztuki Całość", smallTextFont);
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
                AddTableHeader(dataTable, "Cena", smallTextFont);
                AddTableHeader(dataTable, "Wartość", smallTextFont);


                // Add sample rows to the data table
                for (int i = 0; i < ids.Count; i++)
                {
                    int id = ids[i];


                    // Waga 
                    Decimal WagaHodowcaBrutto = zapytaniasql.PobierzInformacjeZBazyDanych<Decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "FullFarmWeight");
                    Decimal WagaHodowcaTara = zapytaniasql.PobierzInformacjeZBazyDanych<Decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "EmptyFarmWeight");
                    Decimal WagaHodowcaNetto = zapytaniasql.PobierzInformacjeZBazyDanych<Decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "NettoFarmWeight");
                    string strWagaHodowcaBrutto = WagaHodowcaBrutto.ToString("0") + " kg";
                    string strWagaHodowcaTara = WagaHodowcaTara.ToString("0") + " kg";
                    string strWagaHodowcaNetto = WagaHodowcaNetto.ToString("0") + " kg";

                    // Konfiskaty
                    int konfiskaty1 = zapytaniasql.PobierzInformacjeZBazyDanych<int>(id, "[LibraNet].[dbo].[FarmerCalc]", "DeclI4");
                    int konfiskaty2 = zapytaniasql.PobierzInformacjeZBazyDanych<int>(id, "[LibraNet].[dbo].[FarmerCalc]", "DeclI3");
                    int konfiskaty3 = zapytaniasql.PobierzInformacjeZBazyDanych<int>(id, "[LibraNet].[dbo].[FarmerCalc]", "DeclI5");
                    int konfiskatySuma = konfiskaty1 + konfiskaty2 + konfiskaty3;
                    string strKonfiskatySuma = konfiskatySuma.ToString();

                    // Padle
                    int padle = zapytaniasql.PobierzInformacjeZBazyDanych<int>(id, "[LibraNet].[dbo].[FarmerCalc]", "DeclI2");
                    string strPadle = padle.ToString();

                    // Sztuki Zdatne
                    int sztZdatne = zapytaniasql.PobierzInformacjeZBazyDanych<int>(id, "[LibraNet].[dbo].[FarmerCalc]", "LumQnt") - konfiskatySuma;
                    string strSztZdatne = sztZdatne.ToString();

                    // Sztuki Wszystkie
                    int sztWszystkie = konfiskatySuma + padle + sztZdatne;
                    string strSztWszystkie = sztWszystkie.ToString();

                    // Średnia waga
                    Decimal sredniaWaga = WagaHodowcaNetto / (padle + konfiskatySuma + sztWszystkie);
                    string strSredniaWaga = sredniaWaga.ToString("0.00");

                    // KG Padłe
                    Decimal padleKG = padle * sredniaWaga;
                    string strPadleKG = padleKG.ToString("0.00"); ;

                    // KG Padłe
                    Decimal konfiskatySumaKG = konfiskatySuma * sredniaWaga;
                    string strKonfiskatySumaKG = konfiskatySumaKG.ToString("0.00");

                    // Cena
                    decimal Cena = zapytaniasql.PobierzInformacjeZBazyDanych<decimal>(id, "[LibraNet].[dbo].[FarmerCalc]", "Price");
                    string strCena = Cena.ToString();

                    // Wartosc
                    decimal Wartosc = Cena * (WagaHodowcaNetto - padleKG - konfiskatySumaKG);
                    string strWartosc = Wartosc.ToString("0.00");




                    AddTableData(dataTable, smallTextFont, (i + 1).ToString(), strWagaHodowcaBrutto, strWagaHodowcaTara, strWagaHodowcaNetto, strSztWszystkie, strSredniaWaga, strPadle, strKonfiskatySuma, strSztZdatne, strWagaHodowcaNetto, strPadleKG, strKonfiskatySumaKG, "", "120", "1000", "10 377", strCena, strWartosc);
                }

                doc.Add(dataTable);

                // Create a paragraph for italic text
                Paragraph italicText = new Paragraph("Jest tutaj notka, mówiąca, że od tej ceny bedzie odjęta wartośc na rzecz KOWR.", ItalicFont);
                italicText.Alignment = Element.ALIGN_CENTER;

                // Add italic text to the document
                doc.Add(italicText);


                // Cena
                Paragraph cena = new Paragraph("Cena : 4,57 zł/kg", textFont);
                cena.Alignment = Element.ALIGN_RIGHT;
                doc.Add(cena);
                // TypCeny
                Paragraph typCena = new Paragraph("Typ Ceny : Wolnyrynek", ItalicFont);
                typCena.Alignment = Element.ALIGN_RIGHT;
                doc.Add(typCena);


                // Summary
                Paragraph summary = new Paragraph("Suma: 15 050 zł", textFont);
                summary.Alignment = Element.ALIGN_RIGHT;
                doc.Add(summary);

                // Summary
                Paragraph platnosc = new Paragraph("Termin płatności : 45 dni", ItalicFont);
                platnosc.Alignment = Element.ALIGN_RIGHT;
                doc.Add(platnosc);


                // Close the document
                doc.Close();
            }

            // Notify the user and open the file
            MessageBox.Show("Raport PDF został wygenerowany.");
            //System.Diagnostics.Process.Start(filePath);
        }

        // Method to add table headers
        private void AddTableHeader(PdfPTable table, string columnName, Font font)
        {
            PdfPCell cell = new PdfPCell(new Phrase(columnName, font))
            {
                BackgroundColor = BaseColor.LIGHT_GRAY,
                HorizontalAlignment = Element.ALIGN_CENTER
            };
            table.AddCell(cell);
        }

        // Method to add table data
        private void AddTableData(PdfPTable table, Font font, params string[] values)
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

        private void dateTimePicker1_ValueChanged(object sender, EventArgs e)
        {
            // Pobierz nową datę z DateTimePicker
            DateTime wybranaData = dateTimePicker1.Value;

            // Zaktualizuj dane w DataGridView na podstawie nowej daty
            PokazWiersze(wybranaData);
        }
    }
}