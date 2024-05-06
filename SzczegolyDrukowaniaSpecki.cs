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
            Document doc = new Document(PageSize.A4.Rotate(), 50, 50, 25, 25);
            string filePath = @"C:\ESD\raport.pdf";

            using (FileStream fs = new FileStream(filePath, FileMode.Create))
            {
                PdfWriter writer = PdfWriter.GetInstance(doc, fs);
                doc.Open();

                Font headerFont = FontFactory.GetFont(BaseFont.HELVETICA, 18, iTextSharp.text.Font.BOLD);
                Font subHeaderFont = FontFactory.GetFont(BaseFont.HELVETICA, 14, iTextSharp.text.Font.BOLD);
                Font textFont = FontFactory.GetFont(BaseFont.HELVETICA, 12, iTextSharp.text.Font.NORMAL);

                Image logo = Image.GetInstance(@"C:\ESD\logo.png");
                logo.SetAbsolutePosition(doc.PageSize.Width - 36f - 130f, doc.PageSize.Height - 36f - 72f);
                logo.ScaleAbsoluteWidth(50f);
                logo.ScaleAbsoluteHeight(50f);
                doc.Add(logo);

                Paragraph header = new Paragraph("Specyfikacja przyjętego drobiu", headerFont);
                header.Alignment = Element.ALIGN_CENTER;
                doc.Add(header);

               // Paragraph subHeader = new Paragraph("Szczegóły przyjęcia", subHeaderFont);
                //subHeader.Alignment = Element.ALIGN_CENTER;
                //subHeader.SpacingBefore = 20f;
                //subHeader.SpacingAfter = 30f;
                //doc.Add(subHeader);

                Paragraph companyInfo = new Paragraph("Ubojnia Drobiu Piórkowscy\nAdres: Koziołki 40, Dmosin\nNIP: 726-162-54-06", textFont);
                companyInfo.Alignment = Element.ALIGN_LEFT;
                companyInfo.SpacingAfter = 20f;
                doc.Add(companyInfo);

                //Paragraph content = new Paragraph("Imie : " + variable1 + "\nNazwisko : " + variable2, textFont);
                //content.SpacingAfter = 20f;
                //doc.Add(content);

                PdfPTable table = new PdfPTable(new float[] { 0.6F, 0.6F, 0.6F, 0.4F, 0.4F, 0.4F, 0.4F, 0.35F, 0.6F, 0.5F });
                table.WidthPercentage = 100;

                AddTableHeader(table, "Waga Brutto", textFont);
                AddTableHeader(table, "Waga Tara", textFont);
                AddTableHeader(table, "Waga Netto", textFont);
                AddTableHeader(table, "Padłe", textFont);
                AddTableHeader(table, "Konfiskaty", textFont);
                AddTableHeader(table, "Sztuki Zdatne", textFont);
                AddTableHeader(table, "Sztuki ARIMR", textFont);
                AddTableHeader(table, "Średnia Waga", textFont);
                AddTableHeader(table, "Suma KG", textFont);
                AddTableHeader(table, "Wartość", textFont);

                // Dodawanie pierwszego wiersza
                AddTableData(table, "20 000 kg", "15 000 kg", "5 000 kg", "10 szt", "12 szt", "4202 szt", "4224 szt", "3,01", "5 000 kg", "15 050 zł");

                // Dodawanie drugiego wiersza z identycznymi danymi
                AddTableData(table, "20 000 kg", "15 000 kg", "5 000 kg", "10 szt", "12 szt", "4202 szt", "4224 szt", "3,01", "5 000 kg", "15 050 zł");

                doc.Add(table);

                Paragraph summary = new Paragraph("Suma: 15 050 zł", textFont);
                summary.Alignment = Element.ALIGN_RIGHT;
                summary.SpacingBefore = 20f;
                doc.Add(summary);

                doc.Close();
            }

            MessageBox.Show("Raport PDF został wygenerowany.");
            System.Diagnostics.Process.Start(filePath);
        }

        private void AddTableHeader(PdfPTable table, string columnName, Font font)
        {
            PdfPCell header = new PdfPCell(new Phrase(columnName, font));
            header.BackgroundColor = BaseColor.LIGHT_GRAY;
            header.HorizontalAlignment = Element.ALIGN_CENTER;
            table.AddCell(header);
        }

        private void AddTableData(PdfPTable table, params string[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                PdfPCell cell = new PdfPCell(new Phrase(values[i]));
                cell.HorizontalAlignment = Element.ALIGN_CENTER;
                table.AddCell(cell);
            }
        }
    }
}
