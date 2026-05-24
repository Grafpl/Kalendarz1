using Kalendarz1.HDI.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Kalendarz1.HDI.Services
{
    /// <summary>
    /// Generator PDF dla HDI — układ 1:1 ze wzorca papierowego (Wzór 2 / Bałdyga).
    /// Używa QuestPDF Community license (już zainicjowane w OfertaPDFGenerator).
    /// </summary>
    public class HdiPdfGenerator
    {
        private const string FIRMA_PELNA = "UBOJNIA DROBIU \"PIÓRKOWSCY\" Jerzy Piórkowski w spadku";
        private const string FIRMA_ADRES = "Koziołki 40, 95-061 Dmosin";
        private const string FIRMA_WET_NR = "PL 10213901 WE";
        private const string FIRMA_NIP = "726-162-54-06";

        private static readonly CultureInfo Pl = new("pl-PL");

        static HdiPdfGenerator()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        // Wygenerujm obrazy stron PDF (PNG per strona) do podglądu inline w aplikacji.
        // Używamy GenerateImages() QuestPDF — domyślnie 144 DPI, ładne ostre obrazki.
        public List<byte[]> GenerateImages(HdiDokument d)
        {
            return BuildDocument(d).GenerateImages().ToList();
        }

        public byte[] Generate(HdiDokument d) => BuildDocument(d).GeneratePdf();

        private static IDocument BuildDocument(HdiDokument d)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Calibri"));

                    page.Content().Column(col =>
                    {
                        // ── HEADER ──
                        col.Item().AlignCenter().Text("HANDLOWY DOKUMENT IDENTYFIKACYJNY")
                            .FontSize(15).Bold();

                        col.Item().PaddingTop(8).AlignCenter().Text(t =>
                        {
                            t.DefaultTextStyle(x => x.FontSize(10).Bold());
                            t.Span("dla mięsa niepoddanego rozbiorowi*, mięsa poddanego rozbiorowi");
                        });
                        col.Item().AlignCenter().Text("oraz dla przetworów mięsnych⁽¹⁾, wprowadzanych na rynek")
                            .FontSize(9).Italic();

                        // ── NUMER ──
                        col.Item().PaddingTop(10).AlignCenter().Text(t =>
                        {
                            t.DefaultTextStyle(x => x.FontSize(13).Bold());
                            t.Span("Nr: ");
                            t.Span(d.NumerPelny).FontColor("#1E40AF");
                        });

                        // ── NAZWA I ADRES WYSYŁAJĄCEGO ──
                        col.Item().PaddingTop(14).Text(t =>
                        {
                            t.Span("Nazwa i adres wysyłającego: ").Bold();
                            t.Span($"{FIRMA_PELNA}, {FIRMA_ADRES}");
                        });

                        col.Item().PaddingTop(4).Text(t =>
                        {
                            t.Span("Weterynaryjny numer identyfikacyjny zakładu: ").Bold();
                            t.Span(FIRMA_WET_NR).Bold().FontSize(11);
                        });

                        col.Item().PaddingTop(6).Text("Zakład zakwalifikowany do prowadzenia sprzedaży:⁽¹⁾").Bold();
                        col.Item().PaddingLeft(8).Text(MakeRynkiText(d)).FontSize(10);

                        // ── OPIS TOWARU ──
                        col.Item().PaddingTop(6).Text(t =>
                        {
                            t.Span("Opis towaru**: ").Bold();
                            t.Span(d.OpisTowaru).Bold();
                        });

                        col.Item().PaddingTop(4).Text(t =>
                        {
                            t.Span("Rodzaj opakowań: ").Bold();
                            t.Span(d.RodzajOpakowan);
                        });

                        col.Item().PaddingTop(4).Text(t =>
                        {
                            t.Span("Liczba opakowań i waga netto: ").Bold();
                            string opak = d.LiczbaOpakowan.HasValue ? $"{d.LiczbaOpakowan} szt" : "—";
                            string netto = d.WagaNetto.HasValue ? $"waga netto: {d.WagaNetto.Value.ToString("N0", Pl)} kg" : "";
                            string brutto = d.WagaBrutto.HasValue ? $"waga brutto: {d.WagaBrutto.Value.ToString("N0", Pl)} kg" : "";
                            var parts = new[] { opak, netto, brutto };
                            t.Span(string.Join(", ", System.Array.FindAll(parts, p => !string.IsNullOrWhiteSpace(p))));
                        });

                        col.Item().PaddingTop(4).Text(t =>
                        {
                            t.Span("Pochodzenie surowca: ").Bold();
                            t.Span(d.Pochodzenie);
                        });

                        col.Item().PaddingTop(4).Text(t =>
                        {
                            t.Span("Miejsce pozyskania/przetworzenia/składowania⁽¹⁾: ").Bold();
                            t.Span(d.MiejscePozyskania);
                        });

                        col.Item().PaddingTop(4).Text(t =>
                        {
                            t.Span("Data wysyłki i miejsce przeznaczenia: ").Bold();
                            string data = d.DataWysylki?.ToString("dd.MM.yy", Pl) ?? "—";
                            t.Span($"{data} r. ; {d.MiejscePrzeznaczenia}");
                        });

                        col.Item().PaddingTop(4).Text(t =>
                        {
                            t.Span("Rodzaj środka transportu i jego numer rejestracyjny: ").Bold();
                            string nr = d.NumerRejestracyjny ?? "";
                            if (!string.IsNullOrWhiteSpace(d.NumerRejNaczepy))
                                nr = string.IsNullOrWhiteSpace(nr)
                                    ? $"naczepa: {d.NumerRejNaczepy}"
                                    : $"{nr} / naczepa: {d.NumerRejNaczepy}";
                            t.Span(nr);
                        });

                        if (!string.IsNullOrWhiteSpace(d.UwagiTransport))
                            col.Item().PaddingTop(2).Text(d.UwagiTransport).FontSize(9).Italic();

                        col.Item().PaddingTop(4).Text(t =>
                        {
                            t.Span("Dane dotyczące procesu technologicznego, norm jakościowych i produkcyjnych oraz stosowanych przez producenta systemów kontroli jakości⁽²⁾").Bold().FontSize(9);
                        });
                        if (!string.IsNullOrWhiteSpace(d.UwagiTechnologia))
                            col.Item().PaddingTop(2).Text(d.UwagiTechnologia).FontSize(9);

                        // ── TABELA PARTII ──
                        if (d.Partie != null && d.Partie.Count > 0)
                        {
                            // Kolumna "Data mrożenia" pojawia się TYLKO gdy któraś partia ma
                            // ustawioną datę mrożenia (towar mrożony). Świeże mięso nie ma
                            // tej kolumny — zgodnie ze wzorem HDI (Bałdyga vs świeży).
                            bool showMrozenie = d.Partie.Any(p => p.DataMrozenia.HasValue);

                            col.Item().PaddingTop(10).Table(table =>
                            {
                                table.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn(3);    // Asortymenty
                                    c.RelativeColumn(1.5f); // Numery partii
                                    c.RelativeColumn(2);    // Data uboju/produkcji
                                    if (showMrozenie) c.RelativeColumn(2);    // Data mrożenia (warunkowa)
                                    c.RelativeColumn(2);    // Data przydatności
                                    c.RelativeColumn(1.3f); // Waga w kg
                                });

                                table.Header(h =>
                                {
                                    h.Cell().Element(CellHeader).Text("Asortymenty");
                                    h.Cell().Element(CellHeader).Text("Numery\npartii");
                                    h.Cell().Element(CellHeader).Text("Data uboju,\ndata produkcji");
                                    if (showMrozenie) h.Cell().Element(CellHeader).Text("Data mrożenia");
                                    h.Cell().Element(CellHeader).Text("Data\nprzydatności");
                                    h.Cell().Element(CellHeader).Text("Waga w kg");
                                });

                                foreach (var p in d.Partie)
                                {
                                    table.Cell().Element(CellBody).Text(p.Asortyment ?? "").FontSize(9);
                                    table.Cell().Element(CellBody).AlignCenter().Text(p.NumerPartii ?? "").FontSize(9);
                                    table.Cell().Element(CellBody).AlignCenter().Text(FormatDate(p.DataUboju)).FontSize(9);
                                    if (showMrozenie) table.Cell().Element(CellBody).AlignCenter().Text(FormatDate(p.DataMrozenia)).FontSize(9);
                                    table.Cell().Element(CellBody).AlignCenter().Text(FormatDate(p.DataPrzydatnosci)).FontSize(9);
                                    table.Cell().Element(CellBody).AlignCenter().Text(p.WagaKg?.ToString("N0", Pl) ?? "").FontSize(9);
                                }
                            });
                        }

                        // ── PIECZĘĆ ──
                        col.Item().PaddingTop(28).Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text(t =>
                                {
                                    t.Span("Miejscowość i data: ").Bold();
                                    t.Span($"{d.MiejscowoscWystawienia} {d.DataWystawienia:dd.MM.yyyy} r.").Bold();
                                });
                            });
                            row.RelativeItem().AlignRight().Column(c =>
                            {
                                c.Item().AlignRight().Text("(pieczęć i podpis wystawiającego)").FontSize(9).Italic();
                                c.Item().PaddingTop(40).AlignRight().Text(FIRMA_PELNA).FontSize(9).Bold();
                                c.Item().AlignRight().Text(FIRMA_ADRES).FontSize(9);
                                c.Item().AlignRight().Text($"NIP {FIRMA_NIP}").FontSize(9);
                            });
                        });

                        // ── STOPKA ──
                        col.Item().PaddingTop(20).Text(t =>
                        {
                            t.DefaultTextStyle(x => x.FontSize(7).Italic());
                            t.Span("* Wzór handlowego dokumentu identyfikacyjnego dla mięsa niepoddanego rozbiorowi wprowadzanego na rynek stosuje się od dnia 1 stycznia 2008 r.\n");
                            t.Span("** W przypadku mięsa mrożonego należy podać datę zamrożenia.\n");
                            t.Span("⁽¹⁾ Niepotrzebne skreślić.   ⁽²⁾ Wypełnienie nieobligatoryjne.");
                        });
                    });
                });
            });
        }

        private static IContainer CellHeader(IContainer c) =>
            c.Border(0.5f).BorderColor(Colors.Black).Background("#E0E7EF").Padding(4).AlignMiddle().AlignCenter();

        private static IContainer CellBody(IContainer c) =>
            c.Border(0.5f).BorderColor(Colors.Black).Padding(4).AlignMiddle();

        private static string FormatDate(System.DateTime? d) =>
            d.HasValue ? d.Value.ToString("dd.MM.yyyy", Pl) + "r" : "";

        private static string MakeRynkiText(HdiDokument d)
        {
            var lines = new System.Collections.Generic.List<string>();
            lines.Add(MarkLine("na rynek Unii Europejskiej", d.RynekUE));
            lines.Add(MarkLine($"na rynek innych państw{(string.IsNullOrWhiteSpace(d.InnePanstwo) ? "" : $" — {d.InnePanstwo}")}", d.RynekInny));
            lines.Add(MarkLine("na rynek krajowy", d.RynekKrajowy));
            return string.Join("\n", lines);
        }

        private static string MarkLine(string text, bool selected) =>
            selected ? $"   ✓ {text}" : $"   ☐ {text}";
    }
}
