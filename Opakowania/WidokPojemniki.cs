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
using System.Linq;



namespace Kalendarz1
{
    public partial class WidokPojemniki : Form
    {
        private RozwijanieComboBox RozwijanieComboBox = new RozwijanieComboBox();
        private string connectionString2 = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        public WidokPojemniki()
        {
            InitializeComponent();
            RozwijanieComboBox.RozwijanieKontrPoKatalogu(comboBoxKontrahent, "Odbiorcy Drobiu");
        }

        private void LoadKontrahenci()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString2))
                {
                    connection.Open();
                    string query = "SELECT id, nazwa FROM kontrahenci";
                    SqlCommand command = new SqlCommand(query, connection);
                    SqlDataReader reader = command.ExecuteReader();

                    while (reader.Read())
                    {
                        comboBoxKontrahent.Items.Add(new
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1)
                        });
                    }
                    reader.Close();
                }

                comboBoxKontrahent.DisplayMember = "Name";
                comboBoxKontrahent.ValueMember = "Id";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania kontrahentów: {ex.Message}");
            }
        }


        private void btnSearch_Click(object sender, EventArgs e)
        {
            if (comboBoxKontrahent.SelectedItem == null || dateTimePickerOd.Value == null || dateTimePickerDo.Value == null)
            {
                MessageBox.Show("Wybierz kontrahenta i określ zakres dat.");
                return;
            }

            // Pobranie ID kontrahenta
            string kontrahentId = ((KeyValuePair<string, string>)comboBoxKontrahent.SelectedItem).Key;

            DateTime dataOd = dateTimePickerOd.Value;
            DateTime dataDo = dateTimePickerDo.Value;

            LoadData(kontrahentId, dataOd, dataDo);
        }


        private void LoadData(string kontrahentId, DateTime dataOd, DateTime dataDo)
        {
            string saldoPoczatkoweQuery = @"
SELECT 
    'Saldo początkowe' AS Dokumenty,
    SUM(CASE WHEN z.kod = 'Pojemnik drobiowy E2' THEN z.ilosc ELSE 0 END) AS E2,
    SUM(CASE WHEN z.kod = 'PALETA H1' THEN z.ilosc ELSE 0 END) AS H1,
    SUM(CASE WHEN z.kod = 'PALETA EURO' THEN z.ilosc ELSE 0 END) AS EURO,
    SUM(CASE WHEN z.kod = 'PALETA PLASTIKOWA' THEN z.ilosc ELSE 0 END) AS PCV,
    SUM(CASE WHEN z.kod = 'Paleta Drewniana' THEN z.ilosc ELSE 0 END) AS Drew
FROM 
    hm.MG AS d
JOIN 
    hm.MZ AS z ON d.id = z.super
WHERE 
    d.khid = @kontrahentId
    AND d.magazyn = 65559
    AND d.typ_dk IN ('MW1', 'MP')
    AND d.anulowany = 0
    AND d.data <= @dataOd";

            string daneQuery = @"
SELECT 
    d.kod AS NrDok,
    d.data AS Data,
    DATENAME(WEEKDAY, d.data) AS DzienTyg,
    d.opis AS Dokumenty,
    SUM(CASE WHEN z.kod = 'Pojemnik drobiowy E2' THEN z.ilosc ELSE 0 END) AS E2,
    SUM(CASE WHEN z.kod = 'PALETA H1' THEN z.ilosc ELSE 0 END) AS H1,
    SUM(CASE WHEN z.kod = 'PALETA EURO' THEN z.ilosc ELSE 0 END) AS EURO,
    SUM(CASE WHEN z.kod = 'PALETA PLASTIKOWA' THEN z.ilosc ELSE 0 END) AS PCV,
    SUM(CASE WHEN z.kod = 'Paleta Drewniana' THEN z.ilosc ELSE 0 END) AS Drew
FROM 
    hm.MG AS d
JOIN 
    hm.MZ AS z ON d.id = z.super
WHERE 
    d.khid = @kontrahentId
    AND d.magazyn = 65559
    AND d.typ_dk IN ('MW1', 'MP')
    AND d.anulowany = 0
    AND d.data BETWEEN '1999-01-01' AND @dataDo
GROUP BY 
    d.id, d.kod, d.data, d.opis
HAVING 
    d.data >= DATEADD(DAY, 1, @dataOd) -- Wyświetlanie danych od jednego dnia później
ORDER BY 
    d.data DESC";

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString2))
                {
                    connection.Open();

                    // Pobierz saldo początkowe
                    SqlCommand saldoCommand = new SqlCommand(saldoPoczatkoweQuery, connection);
                    saldoCommand.Parameters.AddWithValue("@kontrahentId", kontrahentId);
                    saldoCommand.Parameters.AddWithValue("@dataOd", dataOd);

                    SqlDataAdapter saldoAdapter = new SqlDataAdapter(saldoCommand);
                    DataTable saldoTable = new DataTable();
                    saldoAdapter.Fill(saldoTable);

                    // Pobierz dane od dataOd do dataDo
                    SqlCommand daneCommand = new SqlCommand(daneQuery, connection);
                    daneCommand.Parameters.AddWithValue("@kontrahentId", kontrahentId);
                    daneCommand.Parameters.AddWithValue("@dataOd", dataOd);
                    daneCommand.Parameters.AddWithValue("@dataDo", dataDo);

                    SqlDataAdapter daneAdapter = new SqlDataAdapter(daneCommand);
                    DataTable daneTable = new DataTable();
                    daneAdapter.Fill(daneTable);

                    // Dodaj saldo początkowe na górę tabeli
                    if (saldoTable.Rows.Count > 0)
                    {
                        DataRow saldoRow = daneTable.NewRow();
                        saldoRow["Dokumenty"] = $"Saldo {dataOd.ToShortDateString()}";
                        saldoRow["E2"] = saldoTable.Rows[0]["E2"];
                        saldoRow["H1"] = saldoTable.Rows[0]["H1"];
                        saldoRow["EURO"] = saldoTable.Rows[0]["EURO"];
                        saldoRow["PCV"] = saldoTable.Rows[0]["PCV"];
                        saldoRow["Drew"] = saldoTable.Rows[0]["Drew"];

                       
                        daneTable.Rows.Add(saldoRow); // Dodaj saldo na dół
                    }

                    // Dodaj wiersz sumy na dół tabeli
                    if (daneTable.Rows.Count > 0)
                    {
                        DataRow sumRow = daneTable.NewRow();
                        sumRow["Dokumenty"] = $"Saldo {dataDo.ToShortDateString()}";
                        sumRow["E2"] = daneTable.Compute("SUM(E2)", string.Empty);
                        sumRow["H1"] = daneTable.Compute("SUM(H1)", string.Empty);
                        sumRow["EURO"] = daneTable.Compute("SUM(EURO)", string.Empty);
                        sumRow["PCV"] = daneTable.Compute("SUM(PCV)", string.Empty);
                        sumRow["Drew"] = daneTable.Compute("SUM(Drew)", string.Empty);

                        
                        daneTable.Rows.InsertAt(sumRow, 0); // Dodaj saldo na górę
                    }

                    dataGridViewZestawienie.DataSource = daneTable;

                    // Ustawienia kolumn
                    dataGridViewZestawienie.Columns["E2"].Width = 100;
                    dataGridViewZestawienie.Columns["H1"].Width = 100;
                    dataGridViewZestawienie.Columns["EURO"].Width = 100;
                    dataGridViewZestawienie.Columns["PCV"].Width = 100;
                    dataGridViewZestawienie.Columns["NrDok"].Width = 160;
                    dataGridViewZestawienie.Columns["Dokumenty"].Width = 220;
                    dataGridViewZestawienie.Columns["Drew"].Width = 100;

                    // Formatowanie specjalne dla wierszy sald
                    foreach (DataGridViewRow row in dataGridViewZestawienie.Rows)
                    {
                        if (row.Cells["Dokumenty"].Value?.ToString().StartsWith("Saldo") == true)
                        {
                            row.DefaultCellStyle.Font = new System.Drawing.Font(dataGridViewZestawienie.Font.FontFamily, 12, FontStyle.Bold);
                        }

                        else
                        {
                            foreach (DataGridViewCell cell in row.Cells)
                            {
                                if (decimal.TryParse(cell.Value?.ToString(), out decimal cellValue))
                                {
                                    cell.Style.ForeColor = cellValue > 0 ? Color.Red : Color.Green;
                                }
                            }
                        }
                    }

                    // Automatyczne dopasowanie szerokości pozostałych kolumn
                    dataGridViewZestawienie.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania danych: {ex.Message}");
            }
        }
        private void btnGeneratePDF_Click(object sender, EventArgs e)
        {
            if (dataGridViewZestawienie.Rows.Count == 0)
            {
                MessageBox.Show("Brak danych w tabeli. Nie można wygenerować PDF.");
                return;
            }

            string kontrahent = comboBoxKontrahent.Text.Trim();
            string dataDo = dateTimePickerDo.Value.ToString("yyyy-MM-dd");
            string folderPath = Path.Combine(@"\\192.168.0.170\Public\Salda Opakowan", kontrahent);
            string baseFileName = $"ZestawienieOpakowań_{dataDo}";
            string filePath = Path.Combine(folderPath, $"{baseFileName}.pdf");

            try
            {
                // Sprawdź, czy folder istnieje, jeśli nie, utwórz go
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                // Sprawdź, czy plik już istnieje, i nadaj numer porządkowy
                int fileIndex = 1;
                while (File.Exists(filePath))
                {
                    filePath = Path.Combine(folderPath, $"{baseFileName}_{fileIndex++}.pdf");
                }

                // Ścieżka do czcionki Arial
                string arialFontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");

                // Tworzenie dokumentu PDF
                using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    Document doc = new Document(PageSize.A4, 50, 50, 50, 50); // A4 w orientacji pionowej
                    PdfWriter writer = PdfWriter.GetInstance(doc, fs);

                    doc.Open();

                    // Czcionka obsługująca polskie znaki
                    BaseFont baseFont = BaseFont.CreateFont(arialFontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                    iTextSharp.text.Font titleFont = new iTextSharp.text.Font(baseFont, 18, iTextSharp.text.Font.BOLD);
                    iTextSharp.text.Font cellFontLarge = new iTextSharp.text.Font(baseFont, 14, iTextSharp.text.Font.NORMAL); // Duża czcionka na pierwszej stronie
                    iTextSharp.text.Font cellFont = new iTextSharp.text.Font(baseFont, 10, iTextSharp.text.Font.NORMAL); // Czcionka w tabeli na kolejnych stronach
                    iTextSharp.text.Font boldFont = new iTextSharp.text.Font(baseFont, 14, iTextSharp.text.Font.BOLD);  // Duża pogrubiona
                    iTextSharp.text.Font footerFont = new iTextSharp.text.Font(baseFont, 14, iTextSharp.text.Font.NORMAL); // Większa czcionka
                    iTextSharp.text.Font smallFont = new iTextSharp.text.Font(baseFont, 10, iTextSharp.text.Font.ITALIC);

                    // Nagłówek firmy
                    Paragraph header = new Paragraph(
                        "Ubojnia Drobiu \"Piórkowscy\"\nKoziołki 40, 95-061 Dmosin\n46 874 71 70, wew 122 Magazyn Opakowań", smallFont)
                    {
                        Alignment = Element.ALIGN_LEFT,
                        SpacingAfter = 20 // Odstęp po nagłówku
                    };
                    doc.Add(header);

                    // Tytuł
                    Paragraph title = new Paragraph($"Zestawienie Opakowań Zwrotnych dla Kontrahenta: {kontrahent}", titleFont)
                    {
                        Alignment = Element.ALIGN_CENTER,
                        SpacingAfter = 20
                    };
                    doc.Add(title);

                    // Podtytuł
                    Paragraph subtitle = new Paragraph(
                        $"W związku z koniecznością uzgodnienia salda opakowań zwrotnych na dzień {dataDo}, poniżej przedstawiamy szczegółowe zestawienie opakowań zgodnie z naszą ewidencją. Prosimy o weryfikację przedstawionych danych oraz potwierdzenie ich zgodności.",
                        footerFont)
                    {
                        Alignment = Element.ALIGN_JUSTIFIED, // Tekst wyjustowany
                        SpacingAfter = 55 // Odstęp po podtytule
                    };

                    // Dodajemy wcięcie dla całego akapitu (pierwsza linia + całość)
                    subtitle.FirstLineIndent = 20f; // Wcięcie pierwszej linii

                    // Dodanie paragrafu do dokumentu
                    doc.Add(subtitle);


                    // Tabela sumaryczna na pierwszej stronie
                    PdfPTable summaryTable = new PdfPTable(2)
                    {
                        WidthPercentage = 85
                    };
                    summaryTable.SetWidths(new float[] { 5f, 5f }); // Szerokie kolumny


                    // Dane z pierwszego wiersza DataGridView
                    DataGridViewRow firstRow = dataGridViewZestawienie.Rows[0];
                    var opakowania = new Dictionary<string, string>
{
    { "E2", "Pojemniki E2" },
    { "H1", "Palety H1" },
    { "EURO", "Palety EURO" },
    { "PCV", "Palety plastikowe" },
    { "Drew", "Palety drewniane (bez zwrotne)" } // Zawsze 0
};

                    foreach (var opakowanie in opakowania)
                    {
                        string columnName = opakowanie.Value;
                        string columnValue = firstRow.Cells[opakowanie.Key]?.Value?.ToString() ?? "0";

                        // Przetwarzanie wartości liczbowej
                        if (decimal.TryParse(columnValue, out decimal numericValue))
                        {
                            string formattedValue;

                            if (numericValue < 0) // Ujemna wartość
                            {
                                formattedValue = $"Ubojnia winna : {Math.Abs(numericValue)}";
                            }
                            else // Dodatnia wartość
                            {
                                formattedValue = $"Kontrahent winny : {numericValue}";
                            }

                            columnValue = formattedValue; // Aktualizacja wartości kolumny
                        }

                        summaryTable.AddCell(new PdfPCell(new Phrase(columnName, cellFontLarge)) { HorizontalAlignment = Element.ALIGN_CENTER });
                        summaryTable.AddCell(new PdfPCell(new Phrase(columnValue, cellFontLarge)) { HorizontalAlignment = Element.ALIGN_LEFT });
                    }

                    doc.Add(summaryTable);

                    // Informacja pod tabelą
                    Paragraph footer = new Paragraph(
                        "Prosimy o przesłanie potwierdzenia zgodności danych na adres e-mail: opakowania@piorkowscy.com.pl. W przypadku braku odpowiedzi w ciągu 7 dni od daty otrzymania niniejszego dokumentu, saldo przedstawione przez naszą firmę zostanie uznane za zgodne. " +
                        "W razie jakichkolwiek pytań lub wątpliwości prosimy o kontakt telefoniczny z naszym magazynem opakowań pod numerem 46 874 71 70, wew. 122. Dziękujemy za współpracę.",
                        footerFont)
                    {
                        Alignment = Element.ALIGN_JUSTIFIED,
                        SpacingBefore = 50, // Odstęp po tekście
                        SpacingAfter = 20 // Odstęp po tekście
                    };
                    // Dodajemy wcięcie dla całego akapitu (pierwsza linia + całość)
                    footer.FirstLineIndent = 20f; // Wcięcie pierwszej linii
                    doc.Add(footer);

                    // Miejsce na podpis kontrahenta
                    Paragraph signature = new Paragraph("\n\n\nPodpis kontrahenta: .......................................................", cellFontLarge)
                    {
                        Alignment = Element.ALIGN_CENTER,
                        SpacingBefore = 30 // Duży odstęp od reszty tekstu
                    };
                    doc.Add(signature);

                    // Czcionka dla małego tekstu
                    

                    // Miejsce na podpis autora
                    Paragraph autor = new Paragraph("\nOprogramowanie utworzone przez Sergiusza Piórkowskiego", smallFont)
                    {
                        Alignment = Element.ALIGN_RIGHT,
                        SpacingBefore = 30
                    };
                    doc.Add(autor);


                    doc.NewPage(); // Dodaj nową stronę dla szczegółowych danych
                                   // Notka po prawej stronie
                    Paragraph note = new Paragraph("- wydanie do odbiorcy, + przyjęcie na ubojnię", smallFont)
                    {
                        Alignment = Element.ALIGN_RIGHT, // Wyrównanie notki do prawej
                        SpacingAfter = 10
                    };
                    
                    doc.Add(new Paragraph(" ")); // Pusta linia
                    doc.Add(note);
                    // Szczegółowa tabela dla kolejnych stron
                    PdfPTable detailTable = new PdfPTable(dataGridViewZestawienie.Columns.Count - 1) // Usuwamy kolumnę "DzieńTyg"
                    {
                        WidthPercentage = 100
                    };

                    // Ustawienie szerokości kolumn
                    float[] columnWidths = new float[dataGridViewZestawienie.Columns.Count - 1];
                    for (int i = 0, colIndex = 0; i < dataGridViewZestawienie.Columns.Count; i++)
                    {
                        if (dataGridViewZestawienie.Columns[i].Name == "DzienTyg") continue;

                        if (dataGridViewZestawienie.Columns[i].Name == "Dokumenty")
                        {
                            columnWidths[colIndex] = 6f;
                        }
                        else if (i >= 4) // Kolumny od E2 do Drew
                        {
                            columnWidths[colIndex] = 2f;
                        }
                        else
                        {
                            columnWidths[colIndex] = 3f;
                        }
                        colIndex++;
                    }
                    detailTable.SetWidths(columnWidths);

                    // Dodanie nagłówków
                    foreach (DataGridViewColumn column in dataGridViewZestawienie.Columns)
                    {
                        if (column.Name == "DzienTyg") continue;

                        PdfPCell headerCell = new PdfPCell(new Phrase(column.HeaderText, cellFont))
                        {
                            HorizontalAlignment = Element.ALIGN_CENTER,
                            VerticalAlignment = Element.ALIGN_MIDDLE,
                            BackgroundColor = BaseColor.LIGHT_GRAY,
                            FixedHeight = 20f
                        };
                        detailTable.AddCell(headerCell);
                    }

                    // Dodanie danych z DataGridView
                    foreach (DataGridViewRow row in dataGridViewZestawienie.Rows)
                    {
                        if (row.IsNewRow) continue;

                        foreach (DataGridViewCell cell in row.Cells)
                        {
                            if (cell.OwningColumn.Name == "DzienTyg") continue;

                            string cellValue = cell.Value?.ToString() ?? string.Empty;

                            if (cell.OwningColumn.Name == "Data" && DateTime.TryParse(cellValue, out DateTime parsedDate))
                            {
                                cellValue = parsedDate.ToString("yyyy-MM-dd");
                            }

                            PdfPCell pdfCell = new PdfPCell(new Phrase(cellValue, cellFont))
                            {
                                HorizontalAlignment = Element.ALIGN_CENTER,
                                VerticalAlignment = Element.ALIGN_MIDDLE,
                                FixedHeight = 20f
                            };

                            detailTable.AddCell(pdfCell);
                        }
                    }

                    detailTable.HeaderRows = 1;
                    doc.Add(detailTable);

                    doc.Close();
                }

                MessageBox.Show($"PDF został wygenerowany i zapisany w lokalizacji: {filePath}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void dateTimePickerOd_ValueChanged(object sender, EventArgs e)
        {

        }
    }
}
