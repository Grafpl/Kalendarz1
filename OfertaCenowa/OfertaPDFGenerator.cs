// OfertaPDFGenerator.cs
using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.Data;
using System.IO;
using System.Windows.Forms;

namespace Kalendarz1
{
    public class OfertaPDFGenerator
    {
        private readonly Font _titleFont;
        private readonly Font _headerFont;
        private readonly Font _normalFont;
        private readonly Font _boldFont;
        private readonly Font _smallFont;
        private readonly BaseColor _primaryColor;
        private readonly BaseColor _secondaryColor;

        public OfertaPDFGenerator()
        {
            string fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
            BaseFont baseFont = BaseFont.CreateFont(fontPath, BaseFont.CP1250, BaseFont.EMBEDDED);

            _titleFont = new Font(baseFont, 18, Font.BOLD);
            _headerFont = new Font(baseFont, 14, Font.BOLD, new BaseColor(59, 130, 246));
            _normalFont = new Font(baseFont, 10, Font.NORMAL);
            _boldFont = new Font(baseFont, 10, Font.BOLD);
            _smallFont = new Font(baseFont, 8, Font.NORMAL, BaseColor.GRAY);

            _primaryColor = new BaseColor(59, 130, 246);
            _secondaryColor = new BaseColor(249, 250, 251);
        }

        public bool GeneratePDF(OfertaData oferta, string filePath)
        {
            try
            {
                Document doc = new Document(PageSize.A4, 50, 50, 50, 50);
                PdfWriter writer = PdfWriter.GetInstance(doc, new FileStream(filePath, FileMode.Create));

                // Dodaj metadane
                doc.AddAuthor("Ubojnia Drobiu Piórkowscy");
                doc.AddCreator("System Ofert Handlowych");
                doc.AddTitle($"Oferta {oferta.NumerOferty}");
                doc.AddSubject($"Oferta dla {oferta.Kontrahent}");

                doc.Open();

                // Nagłówek firmy
                AddCompanyHeader(doc);

                // Separator
                doc.Add(new Paragraph(" "));
                //doc.Add(new LineSeparator(0.5f, 100, _primaryColor, Element.ALIGN_CENTER, -2));
                doc.Add(new Paragraph(" "));

                // Dane oferty
                AddOfferHeader(doc, oferta);

                // Tabela produktów
                AddProductsTable(doc, oferta);

                // Podsumowanie
                AddSummarySection(doc, oferta);

                // Warunki handlowe
                AddTermsSection(doc, oferta);

                // Stopka
                AddFooter(doc, oferta);

                doc.Close();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd generowania PDF: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void AddCompanyHeader(Document doc)
        {
            // Logo i nazwa firmy
            PdfPTable headerTable = new PdfPTable(2);
            headerTable.WidthPercentage = 100;
            headerTable.SetWidths(new float[] { 60, 40 });
            headerTable.DefaultCell.Border = Rectangle.NO_BORDER;

            // Lewa strona - dane firmy
            PdfPCell leftCell = new PdfPCell();
            leftCell.Border = Rectangle.NO_BORDER;

            Paragraph companyName = new Paragraph("UBOJNIA DROBIU PIÓRKOWSCY", _titleFont);
            companyName.Alignment = Element.ALIGN_LEFT;
            leftCell.AddElement(companyName);

            leftCell.AddElement(new Paragraph("ul. Przemysłowa 15", _normalFont));
            leftCell.AddElement(new Paragraph("42-200 Częstochowa", _normalFont));
            leftCell.AddElement(new Paragraph("NIP: 573-123-45-67", _normalFont));
            leftCell.AddElement(new Paragraph("Tel: +48 34 365 00 00", _normalFont));
            leftCell.AddElement(new Paragraph("Email: oferty@piorkowscy.pl", _normalFont));

            headerTable.AddCell(leftCell);

            // Prawa strona - certyfikaty
            PdfPCell rightCell = new PdfPCell();
            rightCell.Border = Rectangle.NO_BORDER;
            rightCell.HorizontalAlignment = Element.ALIGN_RIGHT;

            Paragraph certTitle = new Paragraph("CERTYFIKATY:", _boldFont);
            certTitle.Alignment = Element.ALIGN_RIGHT;
            rightCell.AddElement(certTitle);

            rightCell.AddElement(new Paragraph("✓ HACCP", _smallFont));
            rightCell.AddElement(new Paragraph("✓ ISO 22000:2018", _smallFont));
            rightCell.AddElement(new Paragraph("✓ IFS Food", _smallFont));
            rightCell.AddElement(new Paragraph("✓ Halal", _smallFont));

            headerTable.AddCell(rightCell);

            doc.Add(headerTable);
        }

        private void AddOfferHeader(Document doc, OfertaData oferta)
        {
            PdfPTable infoTable = new PdfPTable(2);
            infoTable.WidthPercentage = 100;
            infoTable.SpacingBefore = 20;
            infoTable.DefaultCell.Border = Rectangle.NO_BORDER;

            // Lewa kolumna - dane kontrahenta
            PdfPCell kontrahentCell = new PdfPCell();
            kontrahentCell.Border = Rectangle.BOX;
            kontrahentCell.BorderColor = _secondaryColor;
            kontrahentCell.BorderWidth = 1;
            kontrahentCell.Padding = 10;
            kontrahentCell.BackgroundColor = _secondaryColor;

            kontrahentCell.AddElement(new Paragraph("ODBIORCA:", _boldFont));
            kontrahentCell.AddElement(new Paragraph(oferta.Kontrahent, _normalFont));
            kontrahentCell.AddElement(new Paragraph(oferta.Adres, _normalFont));
            kontrahentCell.AddElement(new Paragraph($"NIP: {oferta.NIP}", _normalFont));
            kontrahentCell.AddElement(new Paragraph($"Kraj: {oferta.Kraj}", _normalFont));

            infoTable.AddCell(kontrahentCell);

            // Prawa kolumna - dane oferty
            PdfPCell ofertaCell = new PdfPCell();
            ofertaCell.Border = Rectangle.BOX;
            ofertaCell.BorderColor = _primaryColor;
            ofertaCell.BorderWidth = 2;
            ofertaCell.Padding = 10;

            ofertaCell.AddElement(new Paragraph($"OFERTA NR: {oferta.NumerOferty}", _headerFont));
            ofertaCell.AddElement(new Paragraph($"Data: {oferta.DataOferty:dd.MM.yyyy}", _normalFont));
            ofertaCell.AddElement(new Paragraph($"Ważna do: {oferta.DataWaznosci:dd.MM.yyyy}", _normalFont));
            ofertaCell.AddElement(new Paragraph($"Handlowiec: {oferta.Handlowiec}", _boldFont));

            infoTable.AddCell(ofertaCell);

            doc.Add(infoTable);
        }

        private void AddProductsTable(Document doc, OfertaData oferta)
        {
            doc.Add(new Paragraph(" "));
            doc.Add(new Paragraph("OFEROWANE PRODUKTY:", _headerFont));
            doc.Add(new Paragraph(" "));

            PdfPTable table = new PdfPTable(6);
            table.WidthPercentage = 100;
            table.SetWidths(new float[] { 5, 35, 15, 10, 15, 20 });

            // Nagłówki
            string[] headers = { "Lp.", "Produkt", "Cena netto\n[zł/kg]", "Jedn.", "Min. ilość", "Wartość [zł]" };
            foreach (string header in headers)
            {
                PdfPCell cell = new PdfPCell(new Phrase(header, _boldFont));
                cell.BackgroundColor = _primaryColor;
                cell.HorizontalAlignment = Element.ALIGN_CENTER;
                cell.VerticalAlignment = Element.ALIGN_MIDDLE;
                cell.Padding = 8;
                cell.BorderColor = BaseColor.WHITE;

                // Biały tekst na niebieskim tle
                cell.Phrase = new Phrase(header, new Font(_boldFont.BaseFont, 10, Font.BOLD, BaseColor.WHITE));
                table.AddCell(cell);
            }

            // Dane produktów
            int lp = 1;
            decimal sumaWartosc = 0;

            foreach (DataRow row in oferta.Produkty.Rows)
            {
                decimal cenaNetto = Convert.ToDecimal(row["CenaNetto"]);
                decimal iloscMin = Convert.ToDecimal(row["IloscMin"]);
                decimal wartosc = cenaNetto * iloscMin;
                sumaWartosc += wartosc;

                // Lp.
                AddTableCell(table, lp.ToString(), Element.ALIGN_CENTER, _normalFont);

                // Produkt
                string produkt = row["Nazwa"].ToString();
                if (!string.IsNullOrEmpty(row["Uwagi"]?.ToString()))
                {
                    produkt += $"\n({row["Uwagi"]})";
                }
                AddTableCell(table, produkt, Element.ALIGN_LEFT, _normalFont);

                // Cena
                AddTableCell(table, $"{cenaNetto:N2}", Element.ALIGN_RIGHT, _normalFont);

                // Jednostka
                AddTableCell(table, row["Jednostka"].ToString(), Element.ALIGN_CENTER, _normalFont);

                // Min. ilość
                AddTableCell(table, $"{iloscMin:N0}", Element.ALIGN_RIGHT, _normalFont);

                // Wartość
                AddTableCell(table, $"{wartosc:N2}", Element.ALIGN_RIGHT, _boldFont);

                lp++;
            }

            // Wiersz sumy
            PdfPCell sumCell = new PdfPCell(new Phrase($"SUMA NETTO:", _boldFont));
            sumCell.Colspan = 5;
            sumCell.HorizontalAlignment = Element.ALIGN_RIGHT;
            sumCell.Padding = 8;
            sumCell.BackgroundColor = _secondaryColor;
            table.AddCell(sumCell);

            PdfPCell sumValueCell = new PdfPCell(new Phrase($"{sumaWartosc:N2} zł", _headerFont));
            sumValueCell.HorizontalAlignment = Element.ALIGN_RIGHT;
            sumValueCell.Padding = 8;
            sumValueCell.BackgroundColor = _secondaryColor;
            table.AddCell(sumValueCell);

            doc.Add(table);
        }

        private void AddSummarySection(Document doc, OfertaData oferta)
        {
            doc.Add(new Paragraph(" "));
            doc.Add(new Paragraph("KALKULACJA MARŻY:", _headerFont));
            doc.Add(new Paragraph(" "));

            PdfPTable margeTable = new PdfPTable(4);
            margeTable.WidthPercentage = 80;
            margeTable.HorizontalAlignment = Element.ALIGN_LEFT;

            margeTable.AddCell(CreateLabelCell("Koszt żywca:"));
            margeTable.AddCell(CreateValueCell($"{oferta.KosztZywca:N2} zł/kg"));
            margeTable.AddCell(CreateLabelCell("Marża brutto:"));
            margeTable.AddCell(CreateValueCell($"{oferta.MarzaBrutto:N2} zł/kg", oferta.MarzaBrutto > 2));

            margeTable.AddCell(CreateLabelCell("Koszt transportu:"));
            margeTable.AddCell(CreateValueCell($"{oferta.KosztTransportu:N2} zł/kg"));
            margeTable.AddCell(CreateLabelCell("Marża %:"));
            margeTable.AddCell(CreateValueCell($"{oferta.MarzaProc:N1} %", oferta.MarzaProc > 20));

            doc.Add(margeTable);
        }

        private void AddTermsSection(Document doc, OfertaData oferta)
        {
            doc.Add(new Paragraph(" "));
            doc.Add(new Paragraph("WARUNKI HANDLOWE:", _headerFont));
            doc.Add(new Paragraph(" "));

            PdfPTable termsTable = new PdfPTable(2);
            termsTable.WidthPercentage = 100;
            termsTable.DefaultCell.Border = Rectangle.NO_BORDER;

            AddTermRow(termsTable, "Warunki płatności:", oferta.WarunkiPlatnosci);
            AddTermRow(termsTable, "Warunki dostawy:", oferta.WarunkiDostawy);
            AddTermRow(termsTable, "Termin realizacji:", "Do 48 godzin od złożenia zamówienia");
            AddTermRow(termsTable, "Minimalne zamówienie:", "1000 kg");

            doc.Add(termsTable);

            if (!string.IsNullOrEmpty(oferta.Uwagi))
            {
                doc.Add(new Paragraph(" "));
                doc.Add(new Paragraph("UWAGI:", _boldFont));
                doc.Add(new Paragraph(oferta.Uwagi, _normalFont));
            }
        }

        private void AddFooter(Document doc, OfertaData oferta)
        {
            doc.Add(new Paragraph(" "));
            //doc.Add(new LineSeparator(0.5f, 100, BaseColor.GRAY, Element.ALIGN_CENTER, -2));
            doc.Add(new Paragraph(" "));

            Paragraph footer = new Paragraph();
            footer.Add(new Chunk("Dziękujemy za zainteresowanie naszą ofertą!\n", _boldFont));
            footer.Add(new Chunk($"W sprawie zamówień prosimy kontaktować się z {oferta.Handlowiec}\n", _normalFont));
            footer.Add(new Chunk("Tel: +48 34 365 00 00 | Email: oferty@piorkowscy.pl", _smallFont));
            footer.Alignment = Element.ALIGN_CENTER;

            doc.Add(footer);

            // Dodaj informację o certyfikatach na dole
            doc.Add(new Paragraph(" "));
            Paragraph certInfo = new Paragraph("Wszystkie produkty posiadają pełną dokumentację weterynaryjną zgodną z wymogami UE", _smallFont);
            certInfo.Alignment = Element.ALIGN_CENTER;
            doc.Add(certInfo);
        }

        private void AddTableCell(PdfPTable table, string text, int alignment, Font font)
        {
            PdfPCell cell = new PdfPCell(new Phrase(text, font));
            cell.HorizontalAlignment = alignment;
            cell.VerticalAlignment = Element.ALIGN_MIDDLE;
            cell.Padding = 5;
            table.AddCell(cell);
        }

        private PdfPCell CreateLabelCell(string text)
        {
            PdfPCell cell = new PdfPCell(new Phrase(text, _boldFont));
            cell.Border = Rectangle.NO_BORDER;
            cell.Padding = 5;
            cell.BackgroundColor = _secondaryColor;
            return cell;
        }

        private PdfPCell CreateValueCell(string text, bool highlight = false)
        {
            Font font = highlight ? _headerFont : _normalFont;
            PdfPCell cell = new PdfPCell(new Phrase(text, font));
            cell.Border = Rectangle.NO_BORDER;
            cell.Padding = 5;
            return cell;
        }

        private void AddTermRow(PdfPTable table, string label, string value)
        {
            PdfPCell labelCell = new PdfPCell(new Phrase(label, _boldFont));
            labelCell.Border = Rectangle.NO_BORDER;
            labelCell.Padding = 3;
            table.AddCell(labelCell);

            PdfPCell valueCell = new PdfPCell(new Phrase(value, _normalFont));
            valueCell.Border = Rectangle.NO_BORDER;
            valueCell.Padding = 3;
            table.AddCell(valueCell);
        }
    }

    // Klasa danych oferty
    public class OfertaData
    {
        public string NumerOferty { get; set; } = "";
        public DateTime DataOferty { get; set; }
        public DateTime DataWaznosci { get; set; }
        public string Kontrahent { get; set; } = "";
        public string Adres { get; set; } = "";
        public string NIP { get; set; } = "";
        public string Kraj { get; set; } = "";
        public string Handlowiec { get; set; } = "";
        public DataTable Produkty { get; set; } = new DataTable();
        public decimal KosztZywca { get; set; }
        public decimal KosztTransportu { get; set; }
        public decimal MarzaBrutto { get; set; }
        public decimal MarzaProc { get; set; }
        public string WarunkiPlatnosci { get; set; } = "";
        public string WarunkiDostawy { get; set; } = "";
        public string Uwagi { get; set; } = "";
    }
}