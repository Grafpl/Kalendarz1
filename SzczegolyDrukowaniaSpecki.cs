using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.IO;
using System.Windows.Forms;

namespace Kalendarz1
{
    public partial class SzczegolyDrukowaniaSpecki : Form
    {
        public SzczegolyDrukowaniaSpecki()
        {
            InitializeComponent();
        }

        private void PrintButton_Click(object sender, EventArgs e)
        {
            string variable1 = textBox1.Text;
            string variable2 = textBox2.Text;
            GeneratePDFReport(variable1, variable2);
        }

        private void GeneratePDFReport(string variable1, string variable2)
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
                Font headerFont = new Font(baseFont, 18, iTextSharp.text.Font.BOLD);
                Font tytulTablicy = new Font(baseFont, 15, iTextSharp.text.Font.BOLD);
                Font ItalicFont = new Font(baseFont, 8, iTextSharp.text.Font.ITALIC);
                Font textFont = new Font(baseFont, 12, iTextSharp.text.Font.NORMAL);
                Font smallTextFont = new Font(baseFont, 8, iTextSharp.text.Font.NORMAL); // Small font for the data table
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
                string buyerInfo = $"Nabywca:\nImię: {variable1}\nNazwisko: {variable2}";
                PdfPCell buyerInfoCell = new PdfPCell(new Phrase(buyerInfo, textFont))
                {
                    Border = PdfPCell.NO_BORDER,
                    HorizontalAlignment = Element.ALIGN_RIGHT,
                    PaddingBottom = 0 // Remove the existing padding
                };

                // Split buyer info into lines and add empty lines between them
                string[] buyerLines = { "Sprzedawca:", $"{variable1}", $"{variable2}", "", "" }; // Empty lines for spacing
                foreach (string line in buyerLines)
                {
                    buyerInfoCell.AddElement(new Phrase(line, textFont));
                }

                infoTable.AddCell(buyerInfoCell);


                // Add the info table to the document
                infoTable.SpacingAfter = 20f;
                doc.Add(infoTable);

                // Summary
                Paragraph sredniaWaga = new Paragraph("Ogólna srednia waga: 3,01 kg", smallTextFont);
                sredniaWaga.Alignment = Element.ALIGN_RIGHT;
                sredniaWaga.SpacingAfter = 5f;
                doc.Add(sredniaWaga);

                // Create the data table with adjusted column widths if necessary
                PdfPTable dataTable = new PdfPTable(new float[] { 0.1F, 0.3F, 0.3F, 0.3F, 0.25F, 0.25F, 0.25F, 0.25F, 0.3F, 0.40F, 0.3F, 0.3F, 0.3F, 0.4F, 0.20F, 0.5F });
                dataTable.WidthPercentage = 100;

                // Add merged header for "Waga samochodowa"
                PdfPCell mergedHeaderCell1 = new PdfPCell(new Phrase("Waga samochodowa", tytulTablicy));
                mergedHeaderCell1.Colspan = 4;
                dataTable.AddCell(mergedHeaderCell1);
                // Add merged header for "Rozliczenie sztuk"
                PdfPCell mergedHeaderCell2 = new PdfPCell(new Phrase("Rozliczenie sztuk", tytulTablicy));
                mergedHeaderCell2.Colspan = 5;
                dataTable.AddCell(mergedHeaderCell2);
                // Add merged header for "Rozliczenie kilogramów"
                PdfPCell mergedHeaderCell3 = new PdfPCell(new Phrase("Rozliczenie kilogramów", tytulTablicy));
                mergedHeaderCell3.Colspan = 7;
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
                AddTableHeader(dataTable, "Kilogramy Całość", smallTextFont);
                AddTableHeader(dataTable, "Kilogramy Padłe", smallTextFont);
                AddTableHeader(dataTable, "Kilogramy Konfiskaty", smallTextFont);
                AddTableHeader(dataTable, "Kilogramy Potrąceń", smallTextFont);
                AddTableHeader(dataTable, "Kilogramy do Zapłaty", smallTextFont);
                AddTableHeader(dataTable, "Cena", smallTextFont);
                AddTableHeader(dataTable, "Wartość", smallTextFont);


                // Add sample rows to the data table
                AddTableData(dataTable, smallTextFont, "1.", "36 424", "26 000", "10 424", "4224", "2,46", "10", "9", "4 208", "10 424", "25", "22", "", "10 377", "5.01", "51 988,77");
                AddTableData(dataTable, smallTextFont, "2.", "36 424", "26 000", "10 424", "4224", "2,46", "10", "9", "4 208", "10 424", "25", "22", "", "10 377", "5.01", "51 988,77");
                AddTableData(dataTable, smallTextFont, "3.", "36 424", "26 000", "10 424", "4224", "2,46", "10", "9", "4 208", "10 424", "25", "22", "", "10 377", "5.01", "51 988,77");
                AddTableData(dataTable, smallTextFont, "4.", "36 424", "26 000", "10 424", "4224", "2,46", "10", "9", "4 208", "10 424", "25", "22", "", "10 377", "5.01", "51 988,77");
                AddTableData(dataTable, smallTextFont, "5.", "36 424", "26 000", "10 424", "4224", "2,46", "10", "9", "4 208", "10 424", "25", "22", "", "10 377", "5.01", "51 988,77");
                AddTableData(dataTable, smallTextFont, "6.", "36 424", "26 000", "10 424", "4224", "2,46", "10", "9", "4 208", "10 424", "25", "22", "", "10 377", "5.01", "51 988,77");
                AddTableData(dataTable, smallTextFont, "7.", "36 424", "26 000", "10 424", "4224", "2,46", "10", "9", "4 208", "10 424", "25", "22", "", "10 377", "5.01", "51 988,77");
                AddTableData(dataTable, smallTextFont, "8.", "36 424", "26 000", "10 424", "4224", "2,46", "10", "9", "4 208", "10 424", "25", "22", "", "10 377", "5.01", "51 988,77");
                AddTableData(dataTable, smallTextFont, "9.", "36 424", "26 000", "10 424", "4224", "2,46", "10", "9", "4 208", "10 424", "25", "22", "", "10 377", "5.01", "51 988,77");
                AddTableData(dataTable, smallTextFont, "10.", "36 424", "26 000", "10 424", "4224", "2,46", "10", "9", "4 208", "10 424", "25", "22", "", "10 377", "5.01", "51 988,77");
                // Add the data table to the document
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



    }
}
