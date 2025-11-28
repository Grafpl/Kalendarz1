using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Kalendarz1.Opakowania.Models;

namespace Kalendarz1.Opakowania.Services
{
    /// <summary>
    /// Serwis do generowania raportów PDF dla systemu opakowań
    /// </summary>
    public class PdfReportService
    {
        // Kolory firmy
        private static readonly BaseColor PrimaryGreen = new BaseColor(75, 131, 60);
        private static readonly BaseColor AccentRed = new BaseColor(204, 47, 55);
        private static readonly BaseColor LightGray = new BaseColor(243, 244, 246);
        private static readonly BaseColor DarkText = new BaseColor(44, 62, 80);
        private static readonly BaseColor MediumText = new BaseColor(75, 85, 99);

        // Czcionki
        private Font _fontTitle;
        private Font _fontSubtitle;
        private Font _fontHeader;
        private Font _fontNormal;
        private Font _fontSmall;
        private Font _fontBold;

        public PdfReportService()
        {
            InitializeFonts();
        }

        private void InitializeFonts()
        {
            // Użyj czcionki z systemu
            string fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
            
            if (File.Exists(fontPath))
            {
                BaseFont baseFont = BaseFont.CreateFont(fontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                _fontTitle = new Font(baseFont, 18, Font.BOLD, DarkText);
                _fontSubtitle = new Font(baseFont, 12, Font.NORMAL, MediumText);
                _fontHeader = new Font(baseFont, 10, Font.BOLD, BaseColor.WHITE);
                _fontNormal = new Font(baseFont, 9, Font.NORMAL, DarkText);
                _fontSmall = new Font(baseFont, 8, Font.NORMAL, MediumText);
                _fontBold = new Font(baseFont, 9, Font.BOLD, DarkText);
            }
            else
            {
                // Fallback do wbudowanej czcionki
                _fontTitle = new Font(Font.FontFamily.HELVETICA, 18, Font.BOLD, DarkText);
                _fontSubtitle = new Font(Font.FontFamily.HELVETICA, 12, Font.NORMAL, MediumText);
                _fontHeader = new Font(Font.FontFamily.HELVETICA, 10, Font.BOLD, BaseColor.WHITE);
                _fontNormal = new Font(Font.FontFamily.HELVETICA, 9, Font.NORMAL, DarkText);
                _fontSmall = new Font(Font.FontFamily.HELVETICA, 8, Font.NORMAL, MediumText);
                _fontBold = new Font(Font.FontFamily.HELVETICA, 9, Font.BOLD, DarkText);
            }
        }

        /// <summary>
        /// Generuje raport zestawienia sald dla typu opakowania
        /// </summary>
        public string GenerujRaportZestawienia(
            List<ZestawienieSalda> zestawienie,
            TypOpakowania typOpakowania,
            DateTime dataOd,
            DateTime dataDo,
            string handlowiec = null)
        {
            string fileName = $"Zestawienie_{typOpakowania.Kod}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            string filePath = Path.Combine(Path.GetTempPath(), fileName);

            using (var fs = new FileStream(filePath, FileMode.Create))
            {
                Document doc = new Document(PageSize.A4.Rotate(), 30, 30, 40, 40);
                PdfWriter writer = PdfWriter.GetInstance(doc, fs);
                
                // Footer z numerem strony
                writer.PageEvent = new PdfPageEventHelper();

                doc.Open();

                // Nagłówek
                DodajNaglowekRaportu(doc, $"ZESTAWIENIE SALD OPAKOWAŃ - {typOpakowania.Nazwa.ToUpper()}", 
                    $"Okres: {dataOd:dd.MM.yyyy} - {dataDo:dd.MM.yyyy}" + 
                    (string.IsNullOrEmpty(handlowiec) ? "" : $" | Handlowiec: {handlowiec}"));

                // Statystyki
                var stats = new Dictionary<string, string>
                {
                    { "Liczba kontrahentów", zestawienie.Count.ToString() },
                    { "Suma sald dodatnich", zestawienie.Where(z => z.IloscDrugiZakres > 0).Sum(z => z.IloscDrugiZakres).ToString() },
                    { "Suma sald ujemnych", zestawienie.Where(z => z.IloscDrugiZakres < 0).Sum(z => z.IloscDrugiZakres).ToString() },
                    { "Potwierdzone", zestawienie.Count(z => z.JestPotwierdzone).ToString() }
                };
                DodajStatystyki(doc, stats);

                doc.Add(new Paragraph(" "));

                // Tabela zestawienia
                PdfPTable table = new PdfPTable(7);
                table.WidthPercentage = 100;
                table.SetWidths(new float[] { 3, 5, 2, 2, 2, 2, 2 });

                // Nagłówki
                string[] headers = { "✓", "Kontrahent", "Saldo początkowe", "Saldo końcowe", "Zmiana", "Ostatni dok.", "Potwierdzenie" };
                foreach (var header in headers)
                {
                    PdfPCell cell = new PdfPCell(new Phrase(header, _fontHeader));
                    cell.BackgroundColor = PrimaryGreen;
                    cell.HorizontalAlignment = Element.ALIGN_CENTER;
                    cell.VerticalAlignment = Element.ALIGN_MIDDLE;
                    cell.Padding = 8;
                    table.AddCell(cell);
                }

                // Dane
                bool alternate = false;
                foreach (var item in zestawienie)
                {
                    BaseColor bgColor = alternate ? LightGray : BaseColor.WHITE;
                    
                    // Potwierdzenie
                    AddCell(table, item.JestPotwierdzone ? "✓" : "", bgColor, Element.ALIGN_CENTER);
                    
                    // Kontrahent
                    PdfPCell kontrahentCell = new PdfPCell();
                    kontrahentCell.AddElement(new Paragraph(item.Kontrahent, _fontBold));
                    kontrahentCell.AddElement(new Paragraph(item.Handlowiec ?? "", _fontSmall));
                    kontrahentCell.BackgroundColor = bgColor;
                    kontrahentCell.Padding = 5;
                    table.AddCell(kontrahentCell);

                    // Saldo początkowe
                    AddCell(table, FormatSaldo(item.IloscPierwszyZakres), bgColor, Element.ALIGN_RIGHT, 
                        item.IloscPierwszyZakres > 0 ? AccentRed : item.IloscPierwszyZakres < 0 ? PrimaryGreen : DarkText);

                    // Saldo końcowe
                    AddCell(table, FormatSaldo(item.IloscDrugiZakres), bgColor, Element.ALIGN_RIGHT,
                        item.IloscDrugiZakres > 0 ? AccentRed : item.IloscDrugiZakres < 0 ? PrimaryGreen : DarkText);

                    // Zmiana
                    AddCell(table, FormatSaldo(item.Roznica), bgColor, Element.ALIGN_RIGHT,
                        item.Roznica > 0 ? AccentRed : item.Roznica < 0 ? PrimaryGreen : DarkText);

                    // Ostatni dokument
                    AddCell(table, item.DataOstatniegoDokumentu?.ToString("dd.MM.yyyy") ?? "-", bgColor, Element.ALIGN_CENTER);

                    // Potwierdzenie data
                    AddCell(table, item.DataPotwierdzenia?.ToString("dd.MM.yyyy") ?? "-", bgColor, Element.ALIGN_CENTER);

                    alternate = !alternate;
                }

                doc.Add(table);

                // Stopka
                doc.Add(new Paragraph(" "));
                doc.Add(new Paragraph($"Wygenerowano: {DateTime.Now:dd.MM.yyyy HH:mm:ss}", _fontSmall));

                doc.Close();
            }

            return filePath;
        }

        /// <summary>
        /// Generuje raport szczegółowy salda dla kontrahenta
        /// </summary>
        public string GenerujRaportKontrahenta(
            int kontrahentId,
            string kontrahentNazwa,
            SaldoOpakowania saldo,
            List<DokumentOpakowania> dokumenty,
            List<PotwierdzenieSalda> potwierdzenia,
            DateTime dataOd,
            DateTime dataDo)
        {
            string fileName = $"Saldo_{kontrahentId}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            string filePath = Path.Combine(Path.GetTempPath(), fileName);

            using (var fs = new FileStream(filePath, FileMode.Create))
            {
                Document doc = new Document(PageSize.A4, 30, 30, 40, 40);
                PdfWriter writer = PdfWriter.GetInstance(doc, fs);

                doc.Open();

                // Nagłówek
                DodajNaglowekRaportu(doc, "ZESTAWIENIE SALDA OPAKOWAŃ", 
                    $"Kontrahent: {kontrahentNazwa}");

                doc.Add(new Paragraph($"Okres: {dataOd:dd.MM.yyyy} - {dataDo:dd.MM.yyyy}", _fontSubtitle));
                doc.Add(new Paragraph(" "));

                // Karty sald
                PdfPTable saldaTable = new PdfPTable(5);
                saldaTable.WidthPercentage = 100;
                
                string[] typy = { "E2", "H1", "EURO", "PCV", "DREW" };
                int[] salda = { saldo?.SaldoE2 ?? 0, saldo?.SaldoH1 ?? 0, saldo?.SaldoEURO ?? 0, 
                               saldo?.SaldoPCV ?? 0, saldo?.SaldoDREW ?? 0 };

                for (int i = 0; i < typy.Length; i++)
                {
                    PdfPCell cell = new PdfPCell();
                    cell.AddElement(new Paragraph(typy[i], _fontBold));
                    cell.AddElement(new Paragraph(FormatSaldo(salda[i]), new Font(_fontBold) 
                    { 
                        Color = salda[i] > 0 ? AccentRed : salda[i] < 0 ? PrimaryGreen : DarkText,
                        Size = 14 
                    }));
                    cell.BackgroundColor = LightGray;
                    cell.Padding = 10;
                    cell.HorizontalAlignment = Element.ALIGN_CENTER;
                    saldaTable.AddCell(cell);
                }
                doc.Add(saldaTable);

                doc.Add(new Paragraph(" "));
                doc.Add(new Paragraph("HISTORIA DOKUMENTÓW", _fontTitle));
                doc.Add(new Paragraph(" "));

                // Tabela dokumentów
                if (dokumenty != null && dokumenty.Any())
                {
                    PdfPTable docsTable = new PdfPTable(9);
                    docsTable.WidthPercentage = 100;
                    docsTable.SetWidths(new float[] { 2, 2, 3, 3, 1.5f, 1.5f, 1.5f, 1.5f, 1.5f });

                    string[] docHeaders = { "Data", "Dzień", "Nr dok.", "Opis", "E2", "H1", "EURO", "PCV", "DREW" };
                    foreach (var h in docHeaders)
                    {
                        PdfPCell cell = new PdfPCell(new Phrase(h, _fontHeader));
                        cell.BackgroundColor = PrimaryGreen;
                        cell.HorizontalAlignment = Element.ALIGN_CENTER;
                        cell.Padding = 6;
                        docsTable.AddCell(cell);
                    }

                    bool alt = false;
                    foreach (var dok in dokumenty)
                    {
                        BaseColor bg = alt ? LightGray : BaseColor.WHITE;
                        
                        AddCell(docsTable, dok.Data?.ToString("dd.MM.yyyy") ?? "-", bg, Element.ALIGN_CENTER);
                        AddCell(docsTable, dok.DzienTyg, bg, Element.ALIGN_CENTER);
                        AddCell(docsTable, dok.NrDok ?? "", bg, Element.ALIGN_LEFT);
                        AddCell(docsTable, dok.Dokumenty ?? "", bg, Element.ALIGN_LEFT);
                        AddSaldoCell(docsTable, dok.E2, bg);
                        AddSaldoCell(docsTable, dok.H1, bg);
                        AddSaldoCell(docsTable, dok.EURO, bg);
                        AddSaldoCell(docsTable, dok.PCV, bg);
                        AddSaldoCell(docsTable, dok.DREW, bg);

                        alt = !alt;
                    }
                    doc.Add(docsTable);
                }
                else
                {
                    doc.Add(new Paragraph("Brak dokumentów w wybranym okresie.", _fontNormal));
                }

                // Potwierdzenia
                if (potwierdzenia != null && potwierdzenia.Any())
                {
                    doc.Add(new Paragraph(" "));
                    doc.Add(new Paragraph("HISTORIA POTWIERDZEŃ", _fontTitle));
                    doc.Add(new Paragraph(" "));

                    PdfPTable potTable = new PdfPTable(6);
                    potTable.WidthPercentage = 100;
                    potTable.SetWidths(new float[] { 2, 2, 2, 2, 2, 3 });

                    string[] potHeaders = { "Data", "Typ", "Potwierdzone", "W systemie", "Różnica", "Status" };
                    foreach (var h in potHeaders)
                    {
                        PdfPCell cell = new PdfPCell(new Phrase(h, _fontHeader));
                        cell.BackgroundColor = PrimaryGreen;
                        cell.HorizontalAlignment = Element.ALIGN_CENTER;
                        cell.Padding = 6;
                        potTable.AddCell(cell);
                    }

                    foreach (var pot in potwierdzenia)
                    {
                        AddCell(potTable, pot.DataPotwierdzenia.ToString("dd.MM.yyyy"), BaseColor.WHITE, Element.ALIGN_CENTER);
                        AddCell(potTable, pot.KodOpakowania, BaseColor.WHITE, Element.ALIGN_CENTER);
                        AddCell(potTable, pot.IloscPotwierdzona.ToString(), BaseColor.WHITE, Element.ALIGN_RIGHT);
                        AddCell(potTable, pot.SaldoSystemowe.ToString(), BaseColor.WHITE, Element.ALIGN_RIGHT);
                        AddSaldoCell(potTable, pot.Roznica, BaseColor.WHITE);
                        AddCell(potTable, pot.StatusPotwierdzenia, BaseColor.WHITE, Element.ALIGN_CENTER);
                    }
                    doc.Add(potTable);
                }

                // Stopka
                doc.Add(new Paragraph(" "));
                doc.Add(new Paragraph($"Wygenerowano: {DateTime.Now:dd.MM.yyyy HH:mm:ss}", _fontSmall));

                doc.Close();
            }

            return filePath;
        }

        #region Helper Methods

        private void DodajNaglowekRaportu(Document doc, string tytul, string podtytul)
        {
            // Logo/Nazwa firmy
            Paragraph firma = new Paragraph("PRONOVA SP. Z O.O.", _fontTitle);
            firma.Alignment = Element.ALIGN_CENTER;
            doc.Add(firma);

            // Tytuł raportu
            Paragraph title = new Paragraph(tytul, _fontTitle);
            title.Alignment = Element.ALIGN_CENTER;
            title.SpacingBefore = 10;
            doc.Add(title);

            // Podtytuł
            Paragraph subtitle = new Paragraph(podtytul, _fontSubtitle);
            subtitle.Alignment = Element.ALIGN_CENTER;
            subtitle.SpacingAfter = 20;
            doc.Add(subtitle);

            // Linia oddzielająca
            PdfPTable line = new PdfPTable(1);
            line.WidthPercentage = 100;
            PdfPCell lineCell = new PdfPCell();
            lineCell.BorderWidthTop = 2;
            lineCell.BorderColorTop = PrimaryGreen;
            lineCell.BorderWidthBottom = 0;
            lineCell.BorderWidthLeft = 0;
            lineCell.BorderWidthRight = 0;
            lineCell.FixedHeight = 5;
            line.AddCell(lineCell);
            doc.Add(line);
        }

        private void DodajStatystyki(Document doc, Dictionary<string, string> stats)
        {
            PdfPTable statsTable = new PdfPTable(stats.Count);
            statsTable.WidthPercentage = 100;
            statsTable.SpacingBefore = 15;
            statsTable.SpacingAfter = 15;

            foreach (var stat in stats)
            {
                PdfPCell cell = new PdfPCell();
                cell.AddElement(new Paragraph(stat.Key, _fontSmall));
                cell.AddElement(new Paragraph(stat.Value, new Font(_fontBold) { Size = 14 }));
                cell.BackgroundColor = LightGray;
                cell.Padding = 10;
                cell.HorizontalAlignment = Element.ALIGN_CENTER;
                cell.Border = Rectangle.NO_BORDER;
                statsTable.AddCell(cell);
            }

            doc.Add(statsTable);
        }

        private void AddCell(PdfPTable table, string text, BaseColor bgColor, int alignment, BaseColor textColor = null)
        {
            Font font = textColor != null ? new Font(_fontNormal) { Color = textColor } : _fontNormal;
            PdfPCell cell = new PdfPCell(new Phrase(text, font));
            cell.BackgroundColor = bgColor;
            cell.HorizontalAlignment = alignment;
            cell.VerticalAlignment = Element.ALIGN_MIDDLE;
            cell.Padding = 5;
            table.AddCell(cell);
        }

        private void AddSaldoCell(PdfPTable table, int value, BaseColor bgColor)
        {
            BaseColor textColor = value > 0 ? AccentRed : value < 0 ? PrimaryGreen : DarkText;
            AddCell(table, FormatSaldo(value), bgColor, Element.ALIGN_RIGHT, textColor);
        }

        private string FormatSaldo(int value)
        {
            if (value == 0) return "0";
            return value > 0 ? $"+{value}" : value.ToString();
        }

        #endregion
    }

    /// <summary>
    /// Helper do dodawania numerów stron
    /// </summary>
    public class PdfPageEventHelper : iTextSharp.text.pdf.PdfPageEventHelper
    {
        public override void OnEndPage(PdfWriter writer, Document document)
        {
            PdfContentByte cb = writer.DirectContent;
            BaseFont bf = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
            cb.SetFontAndSize(bf, 9);
            cb.BeginText();
            string text = $"Strona {writer.PageNumber}";
            cb.ShowTextAligned(PdfContentByte.ALIGN_CENTER, text, 
                document.PageSize.Width / 2, document.PageSize.GetBottom(20), 0);
            cb.EndText();
        }
    }
}
