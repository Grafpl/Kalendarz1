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
<<<<<<< HEAD
<<<<<<< HEAD
            Document doc = new Document(PageSize.A4, 50, 50, 25, 25);
=======
            Document doc = new Document(PageSize.A4.Rotate(), 40, 40, 15, 15);

>>>>>>> e8313c5da0ac4393a02ca5ecdee917aca0268e89
=======
            Document doc = new Document(PageSize.A4.Rotate(), 40, 40, 15, 15);
>>>>>>> 0a896a22e34e99e3654e05184b91dd082174024f
            string filePath = @"\\192.168.0.170\Public\Przel\raport.pdf";

            using (FileStream fs = new FileStream(filePath, FileMode.Create))
            {
                PdfWriter writer = PdfWriter.GetInstance(doc, fs);
                doc.Open();

                // Load a BaseFont that supports Polish characters
                BaseFont baseFont = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1250, BaseFont.EMBEDDED);
<<<<<<< HEAD
                Font headerFont = new Font(baseFont, 18, iTextSharp.text.Font.BOLD);
<<<<<<< HEAD
                Font textFont = new Font(baseFont, 12, iTextSharp.text.Font.NORMAL);
                Font smallTextFont = new Font(baseFont, 8, iTextSharp.text.Font.NORMAL); // Small font for the data table
=======
                Font tytulTablicy = new Font(baseFont, 15, iTextSharp.text.Font.BOLD);
=======
                Font headerFont = new Font(baseFont, 15, iTextSharp.text.Font.BOLD);
                Font textFont = new Font(baseFont, 12, iTextSharp.text.Font.NORMAL);
                Font smallTextFont = new Font(baseFont, 8, iTextSharp.text.Font.NORMAL); // Small font for the data table
                Font tytulTablicy = new Font(baseFont, 13, iTextSharp.text.Font.BOLD);
>>>>>>> 0a896a22e34e99e3654e05184b91dd082174024f
                Font ItalicFont = new Font(baseFont, 8, iTextSharp.text.Font.ITALIC);
                Font smallerTextFont = new Font(baseFont, 7, iTextSharp.text.Font.NORMAL); // Small font for the data table
<<<<<<< HEAD
>>>>>>> e8313c5da0ac4393a02ca5ecdee917aca0268e89
=======
>>>>>>> 0a896a22e34e99e3654e05184b91dd082174024f

                // Header paragraph
                Paragraph header = new Paragraph("Rozliczenie przyjętego drobiu", headerFont);
                header.Alignment = Element.ALIGN_CENTER;
                doc.Add(header);

                // Create a table for the seller and buyer info, each in its own cell
                PdfPTable infoTable = new PdfPTable(2);
                infoTable.WidthPercentage = 100;
                infoTable.SetWidths(new float[] { 1f, 1f });

                // Seller information (left column)
<<<<<<< HEAD
<<<<<<< HEAD
=======
                // Seller information (left column)
>>>>>>> e8313c5da0ac4393a02ca5ecdee917aca0268e89
=======
                // Seller information (left column)
>>>>>>> 0a896a22e34e99e3654e05184b91dd082174024f
                PdfPCell sellerInfoCell = new PdfPCell(new Phrase("Ubojnia Drobiu Piórkowscy\nAdres: Koziołki 40, Dmosin\nNIP: 726-162-54-06", textFont))
                {
                    Border = PdfPCell.NO_BORDER,
                    HorizontalAlignment = Element.ALIGN_LEFT,
<<<<<<< HEAD
<<<<<<< HEAD
                    PaddingBottom = 20 // Increased padding between lines
                };
                infoTable.AddCell(sellerInfoCell);

                // Buyer information (right column)
                string buyerInfo = $"Nabywca:\nImię: {variable1}\nNazwisko: {variable2}";
                PdfPCell buyerInfoCell = new PdfPCell(new Phrase(buyerInfo, textFont))
                {
                    Border = PdfPCell.NO_BORDER,
                    HorizontalAlignment = Element.ALIGN_RIGHT,
                    PaddingBottom = 20 // Increased padding between lines
                };
                infoTable.AddCell(buyerInfoCell);

                // Add the info table to the document
                infoTable.SpacingAfter = 20f;
                doc.Add(infoTable);

                // Create the data table with adjusted column widths if necessary
                PdfPTable dataTable = new PdfPTable(new float[] { 0.6F, 0.6F, 0.6F, 0.4F, 0.4F, 0.4F, 0.4F, 0.35F, 0.6F, 0.5F });
                dataTable.WidthPercentage = 100;

                // Add headers to the data table
                AddTableHeader(dataTable, "Waga Brutto", smallTextFont);
                AddTableHeader(dataTable, "Waga Tara", smallTextFont);
                AddTableHeader(dataTable, "Waga Netto", smallTextFont);
                AddTableHeader(dataTable, "Padłe", smallTextFont);
                AddTableHeader(dataTable, "Konfiskaty", smallTextFont);
                AddTableHeader(dataTable, "Sztuki Zdatne", smallTextFont);
                AddTableHeader(dataTable, "Sztuki ARIMR", smallTextFont);
                AddTableHeader(dataTable, "Średnia Waga", smallTextFont);
                AddTableHeader(dataTable, "Suma KG", smallTextFont);
                AddTableHeader(dataTable, "Wartość", smallTextFont);

                // Add sample rows to the data table
                AddTableData(dataTable, smallTextFont, "20 000", "15 000", "5 000", "10", "12", "4202", "4224", "3,01", "5 000", "15 050");
                AddTableData(dataTable, smallTextFont, "20 000", "15 000", "5 000", "10", "12", "4202", "4224", "3,01", "5 000", "15 050");

                // Add the data table to the document
                doc.Add(dataTable);

=======
=======
>>>>>>> 0a896a22e34e99e3654e05184b91dd082174024f
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
                string[] buyerLines = { "Nabywca:", $"{variable1}", $"{variable2}", "", "" }; // Empty lines for spacing
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


                // Add sample rows to the data table
                AddTableData(dataTable2, smallTextFont, "1.", "EBR1234", "EBR5678", "00:05", "00:23", "36 424", "26 000", "10 424", "37 424", "27 000", "10 424", "120", "2", "60", "1", "60");
                AddTableData(dataTable2, smallTextFont, "2.", "EBR1234", "EBR5678", "00:05", "00:23", "36 424", "26 000", "10 424", "37 424", "27 000", "10 424", "120", "2", "60", "1", "60");
                AddTableData(dataTable2, smallTextFont, "3.", "EBR1234", "EBR5678", "00:05", "00:23", "36 424", "26 000", "10 424", "37 424", "27 000", "10 424", "120", "2", "60", "1", "60");
                AddTableData(dataTable2, smallTextFont, "4.", "EBR1234", "EBR5678", "00:05", "00:23", "36 424", "26 000", "10 424", "37 424", "27 000", "10 424", "120", "2", "60", "1", "60");
                AddTableData(dataTable2, smallTextFont, "5.", "EBR1234", "EBR5678", "00:05", "00:23", "36 424", "26 000", "10 424", "37 424", "27 000", "10 424", "120", "2", "60", "1", "60");
                AddTableData(dataTable2, smallTextFont, "6.", "EBR1234", "EBR5678", "00:05", "00:23", "36 424", "26 000", "10 424", "37 424", "27 000", "10 424", "120", "2", "60", "1", "60");
                AddTableData(dataTable2, smallTextFont, "7.", "EBR1234", "EBR5678", "00:05", "00:23", "36 424", "26 000", "10 424", "37 424", "27 000", "10 424", "120", "2", "60", "1", "60");
                AddTableData(dataTable2, smallTextFont, "8.", "EBR1234", "EBR5678", "00:05", "00:23", "36 424", "26 000", "10 424", "37 424", "27 000", "10 424", "120", "2", "60", "1", "60");
                AddTableData(dataTable2, smallTextFont, "9.", "EBR1234", "EBR5678", "00:05", "00:23", "36 424", "26 000", "10 424", "37 424", "27 000", "10 424", "120", "2", "60", "1", "60");
                AddTableData(dataTable2, smallTextFont, "10.", "EBR1234", "EBR5678", "00:05", "00:23", "36 424", "26 000", "10 424", "37 424", "27 000", "10 424", "120", "2", "60", "1", "60");

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
                AddTableData(dataTable, smallTextFont, "1.", "36 424", "26 000", "10 424", "4224", "2,46", "10", "9", "4 208", "10 424", "25", "22", "", "120", "1000", "10 377", "5.01", "51 988,77");
                AddTableData(dataTable, smallTextFont, "2.", "36 424", "26 000", "10 424", "4224", "2,46", "10", "9", "4 208", "10 424", "25", "22", "", "120", "1000", "10 377", "5.01", "51 988,77");
                AddTableData(dataTable, smallTextFont, "3.", "36 424", "26 000", "10 424", "4224", "2,46", "10", "9", "4 208", "10 424", "25", "22", "", "120", "1000", "10 377", "5.01", "51 988,77");
                AddTableData(dataTable, smallTextFont, "4.", "36 424", "26 000", "10 424", "4224", "2,46", "10", "9", "4 208", "10 424", "25", "22", "", "120", "1000", "10 377", "5.01", "51 988,77");
                AddTableData(dataTable, smallTextFont, "5.", "36 424", "26 000", "10 424", "4224", "2,46", "10", "9", "4 208", "10 424", "25", "22", "", "120", "1000", "10 377", "5.01", "51 988,77");
                AddTableData(dataTable, smallTextFont, "6.", "36 424", "26 000", "10 424", "4224", "2,46", "10", "9", "4 208", "10 424", "25", "22", "", "120", "1000", "10 377", "5.01", "51 988,77");
                AddTableData(dataTable, smallTextFont, "7.", "36 424", "26 000", "10 424", "4224", "2,46", "10", "9", "4 208", "10 424", "25", "22", "", "120", "1000", "10 377", "5.01", "51 988,77");
                AddTableData(dataTable, smallTextFont, "8.", "36 424", "26 000", "10 424", "4224", "2,46", "10", "9", "4 208", "10 424", "25", "22", "", "120", "1000", "10 377", "5.01", "51 988,77");
                AddTableData(dataTable, smallTextFont, "9.", "36 424", "26 000", "10 424", "4224", "2,46", "10", "9", "4 208", "10 424", "25", "22", "", "120", "1000", "10 377", "5.01", "51 988,77");
                AddTableData(dataTable, smallTextFont, "10.", "36 424", "26 000", "10 424", "4224", "2,46", "10", "9", "4 208", "10 424", "25", "22", "", "120", "1000", "10 377", "5.01", "51 988,77");
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

<<<<<<< HEAD
>>>>>>> e8313c5da0ac4393a02ca5ecdee917aca0268e89
=======

>>>>>>> 0a896a22e34e99e3654e05184b91dd082174024f
                // Summary
                Paragraph summary = new Paragraph("Suma: 15 050 zł", textFont);
                summary.Alignment = Element.ALIGN_RIGHT;
                doc.Add(summary);

<<<<<<< HEAD
<<<<<<< HEAD
=======
=======
>>>>>>> 0a896a22e34e99e3654e05184b91dd082174024f
                // Summary
                Paragraph platnosc = new Paragraph("Termin płatności : 45 dni", ItalicFont);
                platnosc.Alignment = Element.ALIGN_RIGHT;
                doc.Add(platnosc);

<<<<<<< HEAD
>>>>>>> e8313c5da0ac4393a02ca5ecdee917aca0268e89
=======

>>>>>>> 0a896a22e34e99e3654e05184b91dd082174024f
                // Close the document
                doc.Close();
            }

            // Notify the user and open the file
            MessageBox.Show("Raport PDF został wygenerowany.");
            //System.Diagnostics.Process.Start(filePath);
        }

<<<<<<< HEAD
<<<<<<< HEAD
=======


>>>>>>> e8313c5da0ac4393a02ca5ecdee917aca0268e89
=======
>>>>>>> 0a896a22e34e99e3654e05184b91dd082174024f
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
