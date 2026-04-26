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
                writer.PageEvent = new PdfFooterWithNote(_baseFont, "- wydanie do odbiorcy, + przyjęcie na ubojnię");

                doc.Open();

                // Nagłówek firmy — ikona "■" przed nazwą
                var headerPara = new Paragraph { Alignment = Element.ALIGN_LEFT, SpacingAfter = 20 };
                var fontIkona = new Font(_baseFont, 12, Font.BOLD);
                headerPara.Add(new Chunk("■  ", fontIkona));
                headerPara.Add(new Chunk("Ubojnia Drobiu \"Piórkowscy\"\n", _fontSmall));
                headerPara.Add(new Chunk("    Koziołki 40, 95-061 Dmosin\n", _fontSmall));
                headerPara.Add(new Chunk("    46 874 71 70, wew 122 Magazyn Opakowań", _fontSmall));
                doc.Add(headerPara);

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
            DateTime dataDo,
            SaldoOpakowania saldoPoczatkowe = null,
            string uzytkownik = null)
        {
            string fileName = $"Saldo_{kontrahentId}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            string filePath = Path.Combine(Path.GetTempPath(), fileName);

            using (var fs = new FileStream(filePath, FileMode.Create))
            {
                // Marginesy: top mały (20pt) żeby nagłówek firmy był wyżej; boczne 42pt (~15mm)
                Document doc = new Document(PageSize.A4, 42, 42, 20, 60);
                PdfWriter writer = PdfWriter.GetInstance(doc, fs);

                // Stopka: Strona X z Y, data, użytkownik + notatka na stronach z tabelą
                writer.PageEvent = new PdfFooterWithNote(
                    _baseFont,
                    "- wydanie do kontrahenta,  + zwrot od kontrahenta",
                    skipFirstPage: true,
                    uzytkownik: uzytkownik);

                // #17: PDF metadata — widoczne we właściwościach pliku
                doc.AddTitle($"Saldo {kontrahentNazwa} {dataDo:yyyy-MM-dd}");
                doc.AddAuthor(string.IsNullOrWhiteSpace(uzytkownik) ? "Ubojnia Drobiu Piórkowscy" : uzytkownik);
                doc.AddSubject($"Kontrahent ID {kontrahentId} — okres {dataOd:yyyy-MM-dd}..{dataDo:yyyy-MM-dd}");
                doc.AddCreator("Kalendarz1 — Moduł Opakowania");
                doc.AddKeywords("opakowania, saldo, palety, pojemniki");
                // #8: Język dokumentu pl-PL (TTS, indeksowanie, czytniki ekranu)
                writer.ExtraCatalog.Put(PdfName.LANG, new PdfString("pl-PL"));

                doc.Open();

                // #18: Bookmark "Podsumowanie" — kotwica na pierwszej stronie
                var anchorSummary = new Chunk(" ");
                anchorSummary.SetLocalDestination("summary");
                doc.Add(new Paragraph(anchorSummary));
                new PdfOutline(writer.RootOutline,
                    PdfAction.GotoLocalPage("summary", false), "Podsumowanie");

                // ============================================
                // STRONA 1 - PODSUMOWANIE
                // ============================================

                // Nagłówek firmy — ikona "■" przed nazwą
                var headerPara = new Paragraph { Alignment = Element.ALIGN_LEFT, SpacingAfter = 20 };
                var fontIkona = new Font(_baseFont, 12, Font.BOLD);
                headerPara.Add(new Chunk("■  ", fontIkona));
                headerPara.Add(new Chunk("Ubojnia Drobiu \"Piórkowscy\"\n", _fontSmall));
                headerPara.Add(new Chunk("    Koziołki 40, 95-061 Dmosin\n", _fontSmall));
                headerPara.Add(new Chunk("    46 874 71 70, wew 122 Magazyn Opakowań", _fontSmall));
                doc.Add(headerPara);

                // Tytuł
                var title = new Paragraph(
                    $"Zestawienie Opakowań Zwrotnych dla Kontrahenta:\n{kontrahentNazwa}",
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
                    SpacingAfter = 30,
                    FirstLineIndent = 20f
                };
                doc.Add(introText);

                // BANNER: kto komu winny — saldo wprost z SQL (bez negacji):
                // - saldo = kontrahent winny (ma nasze opakowania), + saldo = ubojnia winna
                int _pos = 0, _neg = 0, _posSum = 0, _negSum = 0;
                int[] _salda =
                {
                    saldo?.SaldoE2 ?? 0,
                    saldo?.SaldoH1 ?? 0,
                    saldo?.SaldoEURO ?? 0,
                    saldo?.SaldoPCV ?? 0,
                    saldo?.SaldoDREW ?? 0
                };
                foreach (var v in _salda)
                {
                    if (v > 0) { _pos++; _posSum += v; }
                    else if (v < 0) { _neg++; _negSum += Math.Abs(v); }
                }

                string bannerText = null;
                if (_pos == 0 && _neg == 0)
                    bannerText = "SALDO ZEROWE — wszystkie opakowania rozliczone";
                else if (_pos == 0)
                    bannerText = $"KONTRAHENT WINNY — łącznie {FmtNum(_negSum)} szt./pal.";
                else if (_neg == 0)
                    bannerText = $"UBOJNIA WINNA — łącznie {FmtNum(_posSum)} szt./pal.";

                if (bannerText != null)
                {
                    var bannerPara = new Paragraph(bannerText, new Font(_baseFont, 16, Font.BOLD))
                    {
                        Alignment = Element.ALIGN_CENTER,
                        SpacingAfter = 20
                    };
                    doc.Add(bannerPara);
                }

                // Tabela podsumowania sald
                PdfPTable summaryTable = new PdfPTable(2)
                {
                    WidthPercentage = 85,
                    HorizontalAlignment = Element.ALIGN_CENTER
                };
                summaryTable.SetWidths(new float[] { 5f, 5f });

                // Dane opakowań — saldo wprost z SQL: - = kontrahent winny, + = ubojnia winna
                var opakowania = new Dictionary<string, (string Nazwa, int Wartosc)>
                {
                    { "E2", ("Pojemniki E2", saldo?.SaldoE2 ?? 0) },
                    { "H1", ("Palety H1", saldo?.SaldoH1 ?? 0) },
                    { "EURO", ("Palety EURO", saldo?.SaldoEURO ?? 0) },
                    { "PCV", ("Palety plastikowe", saldo?.SaldoPCV ?? 0) },
                    { "DREW", ("Palety Drewniane (nie zwrotne)", saldo?.SaldoDREW ?? 0) }
                };

                foreach (var opakowanie in opakowania)
                {
                    string nazwaOpakowania = opakowanie.Value.Nazwa;
                    int wartosc = opakowanie.Value.Wartosc;

                    string wartoscText;
                    if (wartosc < 0)
                    {
                        wartoscText = $"Kontrahent winny : {FmtNum(Math.Abs(wartosc))}";
                    }
                    else if (wartosc > 0)
                    {
                        wartoscText = $"Ubojnia winna : {FmtNum(wartosc)}";
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

                // Informacja o potwierdzeniu — z klikalnymi mailto i tel (#19)
                var footerInfo = new Paragraph
                {
                    Alignment = Element.ALIGN_JUSTIFIED,
                    SpacingBefore = 30,
                    SpacingAfter = 20,
                    FirstLineIndent = 20f,
                    Leading = 22f  // odstępy między wierszami (font 14pt → standardowo ~17pt; teraz luźniej)
                };
                var fontLink = new Font(_baseFont, _fontFooter.Size, Font.UNDERLINE);
                footerInfo.Add(new Chunk("Prosimy o przesłanie potwierdzenia zgodności danych na adres e-mail: ", _fontFooter));
                var emailAnchor = new Chunk("opakowania@piorkowscy.com.pl", fontLink);
                emailAnchor.SetAnchor("mailto:opakowania@piorkowscy.com.pl?subject=Potwierdzenie%20salda%20opakowan%20-%20" + Uri.EscapeDataString(kontrahentNazwa));
                footerInfo.Add(emailAnchor);
                footerInfo.Add(new Chunk(". W przypadku braku odpowiedzi w ciągu 7 dni od daty otrzymania niniejszego dokumentu, " +
                    "saldo przedstawione przez naszą firmę zostanie uznane za zgodne. " +
                    "W razie jakichkolwiek pytań lub wątpliwości prosimy o kontakt telefoniczny z naszym magazynem " +
                    "opakowań pod numerem ", _fontFooter));
                var telAnchor = new Chunk("46 874 71 70, wew. 122", fontLink);
                telAnchor.SetAnchor("tel:+48468747170");
                footerInfo.Add(telAnchor);
                footerInfo.Add(new Chunk(". Dziękujemy za współpracę.", _fontFooter));
                doc.Add(footerInfo);

                // Miejsce na podpis - NA PIERWSZEJ STRONIE
                // SpacingBefore zmniejszone (z 40 na 15) bo footerInfo dostało większy Leading
                // i samo z siebie urosło — podpis zostaje mniej-więcej w tym samym miejscu
                var signature = new Paragraph(
                    "\n\nPodpis kontrahenta: .......................................................",
                    _fontLarge)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingBefore = 15
                };
                doc.Add(signature);

                // ============================================
                // STRONA 2+ - SZCZEGÓŁOWA TABELA
                // ============================================

                if (dokumenty != null && dokumenty.Any())
                {
                    // Strona detail: standardowe A4 portrait (jak strona 1)
                    // Top margin 50pt — zostawia miejsce na notatkę (legendę +/-) nad tabelą
                    doc.SetPageSize(PageSize.A4);
                    doc.SetMargins(28, 28, 50, 60);
                    doc.NewPage();

                    // #18: Bookmark "Szczegóły dokumentów"
                    var anchorDocs = new Chunk(" ");
                    anchorDocs.SetLocalDestination("docs");
                    doc.Add(new Paragraph(anchorDocs));
                    new PdfOutline(writer.RootOutline,
                        PdfAction.GotoLocalPage("docs", false), "Szczegóły dokumentów");

                    // Tabela szczegółowa: 10 kolumn — kolumny wartości mają TĘ SAMĄ szerokość
                    PdfPTable detailTable = new PdfPTable(10);
                    detailTable.WidthPercentage = 100;
                    // NrDok / Data / Dokumenty zachowują swoje proporcje, wszystkie 7 value-cols = 1.0
                    detailTable.SetWidths(new float[] { 1.4f, 1.4f, 2.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f });

                    // Nagłówki — "Saldo\nE2" łamane na 2 linie żeby zmieściły się w wąskich kolumnach
                    var fontHeaderBlack = new Font(_baseFont, 9, Font.BOLD);
                    string[] docHeaders = { "NrDok", "Data", "Dokumenty", "E2", "Saldo\nE2", "H1", "Saldo\nH1", "EURO", "PCV", "Drew" };
                    foreach (var h in docHeaders)
                    {
                        var headerCell = new PdfPCell(new Phrase(h, fontHeaderBlack))
                        {
                            HorizontalAlignment = Element.ALIGN_CENTER,
                            VerticalAlignment = Element.ALIGN_MIDDLE,
                            Padding = 6,
                            MinimumHeight = 28f,
                            BorderWidth = 0.4f,
                            BorderColor = new BaseColor(180, 180, 180)
                        };
                        detailTable.AddCell(headerCell);
                    }

                    // Sortowanie ASC (od najstarszych) — najstarszy dok u góry, najnowszy na dole
                    var dokumentyPosortowane = dokumenty
                        .Where(d => !d.JestSaldem && d.TypDokumentu != "GRP" && (!d.Dokumenty?.StartsWith("Saldo") ?? true))
                        .OrderBy(d => d.Data)
                        .ThenBy(d => d.NrDok)
                        .ToList();

                    // KONWENCJE — saldo i dokumenty wprost z SQL (jak w programie):
                    // - SALDO: "-" kontrahent winny, "+" ubojnia winna
                    // - DOKUMENTY: "-" wydanie do kontrahenta, "+" zwrot od kontrahenta
                    // - Math: saldo += dok.value (wydanie -1320 → saldo idzie w dół o 1320)
                    int saldoE2End = saldo?.SaldoE2 ?? 0;
                    int saldoH1End = saldo?.SaldoH1 ?? 0;
                    int saldoEUREnd = saldo?.SaldoEURO ?? 0;
                    int saldoPCVEnd = saldo?.SaldoPCV ?? 0;
                    int saldoDREWEnd = saldo?.SaldoDREW ?? 0;

                    // Saldo otwarcia — wprost z SQL (bez negacji)
                    int e2s, h1s, euros, pcvs, drews;
                    if (saldoPoczatkowe != null)
                    {
                        e2s = saldoPoczatkowe.SaldoE2; h1s = saldoPoczatkowe.SaldoH1;
                        euros = saldoPoczatkowe.SaldoEURO; pcvs = saldoPoczatkowe.SaldoPCV; drews = saldoPoczatkowe.SaldoDREW;
                    }
                    else
                    {
                        var saldoStart = dokumenty.FirstOrDefault(d => d.JestSaldem || (d.Dokumenty?.Contains("Saldo") == true));
                        if (saldoStart != null)
                        {
                            e2s = saldoStart.E2; h1s = saldoStart.H1; euros = saldoStart.EURO; pcvs = saldoStart.PCV; drews = saldoStart.DREW;
                        }
                        else
                        {
                            e2s = h1s = euros = pcvs = drews = 0;
                        }
                    }

                    // 1. PIERWSZY WIERSZ - Saldo OTWARCIA (na datę OD) u góry — pogrubione
                    // E2 i H1 puste (to kolumny delta dokumentów), Saldo E2 / Saldo H1 z wartością
                    AddDetailCellEx(detailTable, "", isBold: true);
                    AddDetailCellEx(detailTable, "", isBold: true);
                    AddDetailCellEx(detailTable, $"Saldo {dataOd:dd.MM.yyyy}", isBold: true, align: Element.ALIGN_LEFT);
                    AddDetailCellEx(detailTable, "", isBold: true, align: Element.ALIGN_RIGHT);              // E2
                    AddDetailCellEx(detailTable, FmtVal(e2s), isBold: true, align: Element.ALIGN_RIGHT);    // Saldo E2
                    AddDetailCellEx(detailTable, "", isBold: true, align: Element.ALIGN_RIGHT);              // H1
                    AddDetailCellEx(detailTable, FmtVal(h1s), isBold: true, align: Element.ALIGN_RIGHT);    // Saldo H1
                    AddDetailCellEx(detailTable, FmtVal(euros), isBold: true, align: Element.ALIGN_RIGHT);
                    AddDetailCellEx(detailTable, FmtVal(pcvs), isBold: true, align: Element.ALIGN_RIGHT);
                    AddDetailCellEx(detailTable, FmtVal(drews), isBold: true, align: Element.ALIGN_RIGHT);

                    // 2. Dokumenty od najstarszego — saldo narastające startuje od salda otwarcia
                    //    Każdy wiersz pokazuje saldo PO operacji.
                    //    Math: saldo += dok.value (obie wartości w tej samej konwencji SQL)
                    //    Wydanie -1320 → saldo idzie w dół o 1320 (kontrahent winien więcej)
                    int saldoE2Run = e2s;
                    int saldoH1Run = h1s;
                    foreach (var dok in dokumentyPosortowane)
                    {
                        // Zastosuj dok PRZED wyświetleniem — saldoRun = stan PO tej operacji
                        saldoE2Run += dok.E2;
                        saldoH1Run += dok.H1;

                        AddDetailCellEx(detailTable, dok.NrDok ?? "", align: Element.ALIGN_CENTER);
                        AddDetailCellEx(detailTable, dok.Data?.ToString("yyyy-MM-dd") ?? "", align: Element.ALIGN_CENTER);
                        AddDetailCellEx(detailTable, dok.Dokumenty ?? "", align: Element.ALIGN_LEFT);
                        AddDetailCellEx(detailTable, FmtVal(dok.E2), align: Element.ALIGN_RIGHT);
                        AddDetailCellEx(detailTable, FmtVal(saldoE2Run), align: Element.ALIGN_RIGHT, isBold: true);
                        AddDetailCellEx(detailTable, FmtVal(dok.H1), align: Element.ALIGN_RIGHT);
                        AddDetailCellEx(detailTable, FmtVal(saldoH1Run), align: Element.ALIGN_RIGHT, isBold: true);
                        AddDetailCellEx(detailTable, FmtVal(dok.EURO), align: Element.ALIGN_RIGHT);
                        AddDetailCellEx(detailTable, FmtVal(dok.PCV), align: Element.ALIGN_RIGHT);
                        AddDetailCellEx(detailTable, FmtVal(dok.DREW), align: Element.ALIGN_RIGHT);
                    }

                    // 3. OSTATNI WIERSZ - Saldo ZAMKNIĘCIA (na datę DO) na dole — pogrubione
                    // E2 i H1 puste (to kolumny delta dokumentów), Saldo E2 / Saldo H1 z wartością
                    AddDetailCellEx(detailTable, "", isBold: true);
                    AddDetailCellEx(detailTable, "", isBold: true);
                    AddDetailCellEx(detailTable, $"Saldo {dataDo:dd.MM.yyyy}", isBold: true, align: Element.ALIGN_LEFT);
                    AddDetailCellEx(detailTable, "", isBold: true, align: Element.ALIGN_RIGHT);                       // E2
                    AddDetailCellEx(detailTable, FmtVal(saldoE2End), isBold: true, align: Element.ALIGN_RIGHT);       // Saldo E2
                    AddDetailCellEx(detailTable, "", isBold: true, align: Element.ALIGN_RIGHT);                       // H1
                    AddDetailCellEx(detailTable, FmtVal(saldoH1End), isBold: true, align: Element.ALIGN_RIGHT);       // Saldo H1
                    AddDetailCellEx(detailTable, FmtVal(saldoEUREnd), isBold: true, align: Element.ALIGN_RIGHT);
                    AddDetailCellEx(detailTable, FmtVal(saldoPCVEnd), isBold: true, align: Element.ALIGN_RIGHT);
                    AddDetailCellEx(detailTable, FmtVal(saldoDREWEnd), isBold: true, align: Element.ALIGN_RIGHT);

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

        private void AddDetailCell(PdfPTable table, string text, bool isBold = false)
        {
            // Pozostawione dla wstecznej zgodności — deleguje do AddDetailCellEx
            AddDetailCellEx(table, text, isBold: isBold);
        }

        /// <summary>
        /// Komórka tabeli dokumentów: 8pt (#15), cieńszy border (#6), opcjonalne tło (#1, #2), wyrównanie.
        /// </summary>
        private void AddDetailCellEx(PdfPTable table, string text, bool isBold = false, BaseColor bg = null, int align = Element.ALIGN_CENTER)
        {
            var font = new Font(_baseFont, 8, isBold ? Font.BOLD : Font.NORMAL);
            var cell = new PdfPCell(new Phrase(text, font))
            {
                HorizontalAlignment = align,
                VerticalAlignment = Element.ALIGN_MIDDLE,
                PaddingTop = 5,
                PaddingBottom = 5,
                PaddingLeft = 6,
                PaddingRight = 6,
                MinimumHeight = 18f,
                BorderWidth = 0.4f,
                BorderColor = new BaseColor(190, 195, 200)
            };
            if (bg != null) cell.BackgroundColor = bg;
            table.AddCell(cell);
        }

        // #3: Polska kultura — separator tysięcy = spacja
        private static readonly System.Globalization.CultureInfo _plPL = new System.Globalization.CultureInfo("pl-PL");

        /// <summary>Liczba ze spacjami jako separatorem tysięcy: 1 250</summary>
        private string FmtNum(int v) => v.ToString("N0", _plPL);

        /// <summary>Formatuje wartość dokumentu: +1 250 dla przyjęć, -1 250 dla wydań, 0 dla zera</summary>
        private string FmtVal(int v) => v > 0 ? "+" + FmtNum(v) : FmtNum(v);

        private string FormatSaldo(int value)
        {
            if (value == 0) return "0";
            return value > 0 ? "+" + FmtNum(value) : FmtNum(value);
        }

        private string FormatSaldoZOpis(int value)
        {
            if (value == 0) return "0";
            if (value > 0) return "+" + FmtNum(value);
            return FmtNum(value);
        }

        #endregion
    }

    /// <summary>
    /// Helper do dodawania numerów stron, daty, użytkownika i notatki na każdej stronie.
    /// Używa szablonu, aby na końcu wstawić liczbę stron (X z Y).
    /// </summary>
    public class PdfFooterWithNote : PdfPageEventHelper
    {
        private readonly BaseFont _baseFont;
        private readonly string _noteText;
        private readonly bool _skipFirstPage;
        private readonly string _uzytkownik;
        private readonly string _wygenerowano;
        private PdfTemplate _totalPagesTemplate;
        private int _pageCount;

        public PdfFooterWithNote(BaseFont baseFont = null, string noteText = null, bool skipFirstPage = false, string uzytkownik = null)
        {
            _baseFont = baseFont;
            _noteText = noteText;
            _skipFirstPage = skipFirstPage;
            _uzytkownik = uzytkownik;
            _wygenerowano = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
        }

        public override void OnOpenDocument(PdfWriter writer, Document document)
        {
            // Wyższy template, aby zmieścić ascender/descender bez przycinania
            _totalPagesTemplate = writer.DirectContent.CreateTemplate(50, 14);
        }

        public override void OnEndPage(PdfWriter writer, Document document)
        {
            _pageCount = writer.PageNumber;

            PdfContentByte cb = writer.DirectContent;
            BaseFont bf = _baseFont ?? BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);

            float footerY = document.PageSize.GetBottom(28);
            float left = document.LeftMargin;
            float right = document.PageSize.Width - document.RightMargin;
            float center = document.PageSize.Width / 2;

            // Linia separatora nad stopką
            cb.SetLineWidth(0.4f);
            cb.SetGrayStroke(0.7f);
            cb.MoveTo(left, footerY + 14);
            cb.LineTo(right, footerY + 14);
            cb.Stroke();
            cb.SetGrayStroke(0f);

            // Lewy: data wygenerowania
            cb.BeginText();
            cb.SetFontAndSize(bf, 8);
            cb.ShowTextAligned(PdfContentByte.ALIGN_LEFT, $"Wygenerowano: {_wygenerowano}", left, footerY, 0);
            cb.EndText();

            // Środek: Strona X z Y
            string pageText = $"Strona {writer.PageNumber} z ";
            float textWidth = bf.GetWidthPoint(pageText, 9);
            cb.BeginText();
            cb.SetFontAndSize(bf, 9);
            cb.ShowTextAligned(PdfContentByte.ALIGN_LEFT, pageText, center - textWidth / 2, footerY, 0);
            cb.EndText();
            cb.AddTemplate(_totalPagesTemplate, center - textWidth / 2 + textWidth, footerY);

            // Prawy: użytkownik
            if (!string.IsNullOrWhiteSpace(_uzytkownik))
            {
                cb.BeginText();
                cb.SetFontAndSize(bf, 8);
                cb.ShowTextAligned(PdfContentByte.ALIGN_RIGHT, $"Wygenerował: {_uzytkownik}", right, footerY, 0);
                cb.EndText();
            }

            // Notatka (legenda) w obszarze górnego marginesu — wewnątrz marginesu top, NAD tabelą
            if (!string.IsNullOrEmpty(_noteText) && (!_skipFirstPage || writer.PageNumber > 1))
            {
                cb.BeginText();
                cb.SetFontAndSize(bf, 8);
                cb.ShowTextAligned(PdfContentByte.ALIGN_RIGHT, _noteText,
                    document.PageSize.Width - document.RightMargin,
                    document.PageSize.GetTop(20), 0);
                cb.EndText();
            }
        }

        public override void OnCloseDocument(PdfWriter writer, Document document)
        {
            BaseFont bf = _baseFont ?? BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
            _totalPagesTemplate.BeginText();
            _totalPagesTemplate.SetFontAndSize(bf, 9);
            // Baseline na Y=0 — taki sam jak ShowTextAligned w OnEndPage (template origin == footerY)
            _totalPagesTemplate.SetTextMatrix(0, 0);
            _totalPagesTemplate.ShowText(_pageCount.ToString());
            _totalPagesTemplate.EndText();
        }
    }
}
