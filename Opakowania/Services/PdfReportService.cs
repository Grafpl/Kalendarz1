using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Kalendarz1.Opakowania.Models;

namespace Kalendarz1.Opakowania.Services
{
    /// <summary>
    /// Serwis do generowania raportów PDF dla systemu opakowań
    /// Format zgodny z oryginalnym WidokPojemniki
    /// </summary>
    public class PdfReportService
    {
        // Czcionki
        private Font _fontTitle;
        private Font _fontSubtitle;
        private Font _fontHeader;
        private Font _fontNormal;
        private Font _fontSmall;
        private Font _fontBold;
        private Font _fontLarge;
        private Font _fontFooter;
        private BaseFont _baseFont;

        public PdfReportService()
        {
            InitializeFonts();
        }

        private void InitializeFonts()
        {
            // Użyj czcionki Arial z systemu (obsługuje polskie znaki)
            string fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");

            if (File.Exists(fontPath))
            {
                _baseFont = BaseFont.CreateFont(fontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
            }
            else
            {
                // Fallback - spróbuj znaleźć inną czcionkę
                string[] alternatywne = { "arialuni.ttf", "verdana.ttf", "tahoma.ttf" };
                foreach (var alt in alternatywne)
                {
                    string altPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), alt);
                    if (File.Exists(altPath))
                    {
                        _baseFont = BaseFont.CreateFont(altPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                        break;
                    }
                }

                if (_baseFont == null)
                {
                    _baseFont = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1250, BaseFont.NOT_EMBEDDED);
                }
            }

            _fontTitle = new Font(_baseFont, 18, Font.BOLD);
            _fontSubtitle = new Font(_baseFont, 14, Font.NORMAL);
            _fontHeader = new Font(_baseFont, 10, Font.BOLD, BaseColor.WHITE);
            _fontNormal = new Font(_baseFont, 10, Font.NORMAL);
            _fontSmall = new Font(_baseFont, 10, Font.ITALIC);
            _fontBold = new Font(_baseFont, 14, Font.BOLD);
            _fontLarge = new Font(_baseFont, 14, Font.NORMAL);
            _fontFooter = new Font(_baseFont, 14, Font.NORMAL);
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
                writer.PageEvent = new PdfPageEventHelper(_baseFont);

                doc.Open();

                // Nagłówek firmy
                var header = new Paragraph(
                    "Ubojnia Drobiu \"Piórkowscy\"\nKoziołki 40, 95-061 Dmosin\n46 874 71 70, wew 122 Magazyn Opakowań",
                    _fontSmall)
                {
                    Alignment = Element.ALIGN_LEFT,
                    SpacingAfter = 20
                };
                doc.Add(header);

                // Tytuł
                var title = new Paragraph(
                    $"Zestawienie Sald Opakowań - {typOpakowania.Nazwa}",
                    _fontTitle)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingAfter = 10
                };
                doc.Add(title);

                // Podtytuł z okresem
                var subtitle = new Paragraph(
                    $"Okres: {dataOd:dd.MM.yyyy} - {dataDo:dd.MM.yyyy}" +
                    (string.IsNullOrEmpty(handlowiec) ? "" : $" | Handlowiec: {handlowiec}"),
                    _fontSubtitle)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingAfter = 20
                };
                doc.Add(subtitle);

                // Tabela zestawienia
                PdfPTable table = new PdfPTable(6);
                table.WidthPercentage = 100;
                table.SetWidths(new float[] { 5, 3, 2, 2, 2, 2 });

                // Nagłówki
                string[] headers = { "Kontrahent", "Handlowiec", "Saldo", "Ostatni dok.", "Potwierdzenie", "Status" };
                foreach (var h in headers)
                {
                    var cell = new PdfPCell(new Phrase(h, _fontHeader))
                    {
                        BackgroundColor = new BaseColor(75, 131, 60),
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        VerticalAlignment = Element.ALIGN_MIDDLE,
                        Padding = 8
                    };
                    table.AddCell(cell);
                }

                // Dane
                bool alternate = false;
                foreach (var item in zestawienie)
                {
                    var bgColor = alternate ? new BaseColor(243, 244, 246) : BaseColor.WHITE;

                    // Kontrahent
                    AddCell(table, item.Kontrahent, bgColor, Element.ALIGN_LEFT);

                    // Handlowiec
                    AddCell(table, item.Handlowiec ?? "-", bgColor, Element.ALIGN_CENTER);

                    // Saldo
                    string saldoText = FormatSaldoZOpis(item.IloscDrugiZakres);
                    var saldoColor = item.IloscDrugiZakres > 0 ? new BaseColor(204, 47, 55) :
                                     item.IloscDrugiZakres < 0 ? new BaseColor(75, 131, 60) :
                                     BaseColor.BLACK;
                    AddCell(table, saldoText, bgColor, Element.ALIGN_RIGHT, saldoColor);

                    // Ostatni dokument
                    AddCell(table, item.DataOstatniegoDokumentu?.ToString("dd.MM.yyyy") ?? "-", bgColor, Element.ALIGN_CENTER);

                    // Potwierdzenie
                    AddCell(table, item.DataPotwierdzenia?.ToString("dd.MM.yyyy") ?? "-", bgColor, Element.ALIGN_CENTER);

                    // Status
                    AddCell(table, item.JestPotwierdzone ? "✓" : "-", bgColor, Element.ALIGN_CENTER);

                    alternate = !alternate;
                }

                doc.Add(table);

                // Stopka
                doc.Add(new Paragraph(" "));
                var footerText = new Paragraph($"Wygenerowano: {DateTime.Now:dd.MM.yyyy HH:mm:ss}", _fontSmall)
                {
                    Alignment = Element.ALIGN_RIGHT
                };
                doc.Add(footerText);

                doc.Close();
            }

            return filePath;
        }

        /// <summary>
        /// Generuje raport szczegółowy salda dla kontrahenta
        /// W formacie identycznym jak WidokPojemniki - z podsumowaniem na pierwszej stronie
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
                Document doc = new Document(PageSize.A4, 50, 50, 50, 50);
                PdfWriter writer = PdfWriter.GetInstance(doc, fs);
                writer.PageEvent = new PdfPageEventHelper(_baseFont);

                doc.Open();

                // ============================================
                // STRONA 1 - PODSUMOWANIE
                // ============================================

                // Nagłówek firmy
                var header = new Paragraph(
                    "Ubojnia Drobiu \"Piórkowscy\"\nKoziołki 40, 95-061 Dmosin\n46 874 71 70, wew 122 Magazyn Opakowań",
                    _fontSmall)
                {
                    Alignment = Element.ALIGN_LEFT,
                    SpacingAfter = 20
                };
                doc.Add(header);

                // Tytuł
                var title = new Paragraph(
                    $"Zestawienie Opakowań Zwrotnych dla Kontrahenta: {kontrahentNazwa}",
                    _fontTitle)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingAfter = 20
                };
                doc.Add(title);

                // Tekst wyjaśniający
                string dataSalda = dataDo.ToString("dd.MM.yyyy");
                var introText = new Paragraph(
                    $"W związku z koniecznością uzgodnienia salda opakowań zwrotnych na dzień {dataSalda}, " +
                    "poniżej przedstawiamy szczegółowe zestawienie opakowań zgodnie z naszą ewidencją. " +
                    "Prosimy o weryfikację przedstawionych danych oraz potwierdzenie ich zgodności.",
                    _fontFooter)
                {
                    Alignment = Element.ALIGN_JUSTIFIED,
                    SpacingAfter = 55,
                    FirstLineIndent = 20f
                };
                doc.Add(introText);

                // Tabela podsumowania sald
                PdfPTable summaryTable = new PdfPTable(2)
                {
                    WidthPercentage = 85,
                    HorizontalAlignment = Element.ALIGN_CENTER
                };
                summaryTable.SetWidths(new float[] { 5f, 5f });

                // Dane opakowań
                var opakowania = new Dictionary<string, (string Nazwa, int Wartosc)>
                {
                    { "E2", ("Pojemniki E2", saldo?.SaldoE2 ?? 0) },
                    { "H1", ("Palety H1", saldo?.SaldoH1 ?? 0) },
                    { "EURO", ("Palety EURO", saldo?.SaldoEURO ?? 0) },
                    { "PCV", ("Palety plastikowe", saldo?.SaldoPCV ?? 0) },
                    { "DREW", ("Palety drewniane (bez zwrotne)", saldo?.SaldoDREW ?? 0) }
                };

                foreach (var opakowanie in opakowania)
                {
                    string nazwaOpakowania = opakowanie.Value.Nazwa;
                    int wartosc = opakowanie.Value.Wartosc;

                    string wartoscText;
                    if (wartosc < 0)
                    {
                        wartoscText = $"Ubojnia winna : {Math.Abs(wartosc)}";
                    }
                    else if (wartosc > 0)
                    {
                        wartoscText = $"Kontrahent winny : {wartosc}";
                    }
                    else
                    {
                        wartoscText = "0";
                    }

                    var nazwaCell = new PdfPCell(new Phrase(nazwaOpakowania, _fontLarge))
                    {
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        VerticalAlignment = Element.ALIGN_MIDDLE,
                        Padding = 10
                    };
                    summaryTable.AddCell(nazwaCell);

                    var wartoscCell = new PdfPCell(new Phrase(wartoscText, _fontLarge))
                    {
                        HorizontalAlignment = Element.ALIGN_LEFT,
                        VerticalAlignment = Element.ALIGN_MIDDLE,
                        Padding = 10
                    };
                    summaryTable.AddCell(wartoscCell);
                }

                doc.Add(summaryTable);

                // Informacja o potwierdzeniu
                var footerInfo = new Paragraph(
                    "Prosimy o przesłanie potwierdzenia zgodności danych na adres e-mail: opakowania@piorkowscy.com.pl. " +
                    "W przypadku braku odpowiedzi w ciągu 7 dni od daty otrzymania niniejszego dokumentu, " +
                    "saldo przedstawione przez naszą firmę zostanie uznane za zgodne. " +
                    "W razie jakichkolwiek pytań lub wątpliwości prosimy o kontakt telefoniczny z naszym magazynem " +
                    "opakowań pod numerem 46 874 71 70, wew. 122. Dziękujemy za współpracę.",
                    _fontFooter)
                {
                    Alignment = Element.ALIGN_JUSTIFIED,
                    SpacingBefore = 50,
                    SpacingAfter = 20,
                    FirstLineIndent = 20f
                };
                doc.Add(footerInfo);

                // Miejsce na podpis
                var signature = new Paragraph(
                    "\n\n\nPodpis kontrahenta: .......................................................",
                    _fontLarge)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingBefore = 30
                };
                doc.Add(signature);

                // Autor
                var autor = new Paragraph(
                    "\nOprogramowanie utworzone przez Sergiusza Piórkowskiego",
                    _fontSmall)
                {
                    Alignment = Element.ALIGN_RIGHT,
                    SpacingBefore = 30
                };
                doc.Add(autor);

                // ============================================
                // STRONA 2 - SZCZEGÓŁOWA TABELA
                // ============================================

                if (dokumenty != null && dokumenty.Any())
                {
                    doc.NewPage();

                    // Notka
                    var note = new Paragraph(
                        "- wydanie do odbiorcy, + przyjęcie na ubojnię",
                        _fontSmall)
                    {
                        Alignment = Element.ALIGN_RIGHT,
                        SpacingAfter = 10
                    };
                    doc.Add(new Paragraph(" "));
                    doc.Add(note);

                    // Tabela szczegółowa
                    PdfPTable detailTable = new PdfPTable(8);
                    detailTable.WidthPercentage = 100;
                    detailTable.SetWidths(new float[] { 2f, 3f, 3f, 1.5f, 1.5f, 1.5f, 1.5f, 1.5f });

                    // Nagłówki
                    string[] docHeaders = { "Data", "Nr dok.", "Dokumenty", "E2", "H1", "EURO", "PCV", "Drew" };
                    foreach (var h in docHeaders)
                    {
                        var headerCell = new PdfPCell(new Phrase(h, _fontHeader))
                        {
                            BackgroundColor = BaseColor.LIGHT_GRAY,
                            HorizontalAlignment = Element.ALIGN_CENTER,
                            VerticalAlignment = Element.ALIGN_MIDDLE,
                            FixedHeight = 20f
                        };
                        detailTable.AddCell(headerCell);
                    }

                    // Dane
                    foreach (var dok in dokumenty)
                    {
                        // Data
                        string dataText = dok.Data?.ToString("yyyy-MM-dd") ?? "";
                        AddDetailCell(detailTable, dataText);

                        // Nr dok
                        AddDetailCell(detailTable, dok.NrDok ?? "");

                        // Dokumenty (opis)
                        AddDetailCell(detailTable, dok.Dokumenty ?? "");

                        // E2
                        AddDetailCell(detailTable, dok.E2.ToString());

                        // H1
                        AddDetailCell(detailTable, dok.H1.ToString());

                        // EURO
                        AddDetailCell(detailTable, dok.EURO.ToString());

                        // PCV
                        AddDetailCell(detailTable, dok.PCV.ToString());

                        // Drew
                        AddDetailCell(detailTable, dok.DREW.ToString());
                    }

                    detailTable.HeaderRows = 1;
                    doc.Add(detailTable);
                }

                doc.Close();
            }

            return filePath;
        }

        #region Helper Methods

        private void AddCell(PdfPTable table, string text, BaseColor bgColor, int alignment, BaseColor textColor = null)
        {
            var font = textColor != null ? new Font(_baseFont, 10, Font.NORMAL, textColor) : _fontNormal;
            var cell = new PdfPCell(new Phrase(text, font))
            {
                BackgroundColor = bgColor,
                HorizontalAlignment = alignment,
                VerticalAlignment = Element.ALIGN_MIDDLE,
                Padding = 5
            };
            table.AddCell(cell);
        }

        private void AddDetailCell(PdfPTable table, string text)
        {
            var cell = new PdfPCell(new Phrase(text, _fontNormal))
            {
                HorizontalAlignment = Element.ALIGN_CENTER,
                VerticalAlignment = Element.ALIGN_MIDDLE,
                FixedHeight = 20f
            };
            table.AddCell(cell);
        }

        private string FormatSaldo(int value)
        {
            if (value == 0) return "0";
            return value > 0 ? $"+{value}" : value.ToString();
        }

        private string FormatSaldoZOpis(int value)
        {
            if (value == 0) return "0";
            if (value > 0) return $"+{value}";
            return value.ToString();
        }

        #endregion
    }

    /// <summary>
    /// Helper do dodawania numerów stron
    /// </summary>
    public class PdfPageEventHelper : iTextSharp.text.pdf.PdfPageEventHelper
    {
        private readonly BaseFont _baseFont;

        public PdfPageEventHelper(BaseFont baseFont = null)
        {
            _baseFont = baseFont;
        }

        public override void OnEndPage(PdfWriter writer, Document document)
        {
            PdfContentByte cb = writer.DirectContent;
            BaseFont bf = _baseFont ?? BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
            cb.SetFontAndSize(bf, 9);
            cb.BeginText();
            string text = $"Strona {writer.PageNumber}";
            cb.ShowTextAligned(PdfContentByte.ALIGN_CENTER, text,
                document.PageSize.Width / 2, document.PageSize.GetBottom(20), 0);
            cb.EndText();
        }
    }
}
