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
            Document doc = new Document(PageSize.A4, 50, 50, 25, 25);
            string filePath = @"\\192.168.0.170\Public\Przel\raport.pdf";

            using (FileStream fs = new FileStream(filePath, FileMode.Create))
            {
                PdfWriter writer = PdfWriter.GetInstance(doc, fs);
                doc.Open();

                // Load a BaseFont that supports Polish characters
                BaseFont baseFont = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1250, BaseFont.EMBEDDED);
                Font headerFont = new Font(baseFont, 18, iTextSharp.text.Font.BOLD);
                Font textFont = new Font(baseFont, 12, iTextSharp.text.Font.NORMAL);
                Font smallTextFont = new Font(baseFont, 8, iTextSharp.text.Font.NORMAL); // Small font for the data table

                // Header paragraph
                Paragraph header = new Paragraph("Rozliczenie przyjętego drobiu", headerFont);
                header.Alignment = Element.ALIGN_CENTER;
                doc.Add(header);

                // Create a table for the seller and buyer info, each in its own cell
                PdfPTable infoTable = new PdfPTable(2);
                infoTable.WidthPercentage = 100;
                infoTable.SetWidths(new float[] { 1f, 1f });

                // Seller information (left column)
                PdfPCell sellerInfoCell = new PdfPCell(new Phrase("Ubojnia Drobiu Piórkowscy\nAdres: Koziołki 40, Dmosin\nNIP: 726-162-54-06", textFont))
                {
                    Border = PdfPCell.NO_BORDER,
                    HorizontalAlignment = Element.ALIGN_LEFT,
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

                // Summary
                Paragraph summary = new Paragraph("Suma: 15 050 zł", textFont);
                summary.Alignment = Element.ALIGN_RIGHT;
                summary.SpacingBefore = 20f;
                doc.Add(summary);

                // Close the document
                doc.Close();
            }

            // Notify the user and open the file
            MessageBox.Show("Raport PDF został wygenerowany.");
            System.Diagnostics.Process.Start(filePath);
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
