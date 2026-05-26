using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
using System.Linq;

namespace Kalendarz1.Customer360.Services
{
    /// <summary>Generuje raport PDF karty klienta (QuestPDF Community).</summary>
    public class Customer360PdfExporter
    {
        private static readonly CultureInfo Pl = new("pl-PL");
        private const string Granat = "#1E40AF";
        private const string Szary = "#64748B";
        private const string Tlo = "#F1F5F9";

        public byte[] Generate(Customer360Snapshot s)
        {
            byte[] chartPng = ChartImageRenderer.RenderObrotMiesieczny(s.Obrot, 1000, 320);

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(28);
                    page.DefaultTextStyle(t => t.FontSize(10).FontColor("#0F172A"));

                    // ── Nagłówek ──
                    page.Header().Column(col =>
                    {
                        col.Item().Text($"Karta Klienta 360°").FontSize(20).Bold().FontColor(Granat);
                        col.Item().Text(s.Nazwa).FontSize(14).SemiBold();
                        col.Item().Text(t =>
                        {
                            t.Span($"NIP: {(string.IsNullOrWhiteSpace(s.NIP) ? "—" : s.NIP)}   ").FontColor(Szary);
                            t.Span($"ID: {s.KlientId}   ").FontColor(Szary);
                            t.Span($"Handlowiec: {(string.IsNullOrWhiteSpace(s.Handlowiec) ? "—" : s.Handlowiec)}").FontColor(Szary);
                        });
                        if (!string.IsNullOrWhiteSpace(s.Adres))
                            col.Item().Text(s.Adres).FontColor(Szary).FontSize(9);
                        col.Item().PaddingTop(2).Text($"Wygenerowano: {s.Wygenerowano:dd.MM.yyyy HH:mm}").FontSize(8).FontColor(Szary);
                        col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Granat);
                    });

                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        col.Spacing(12);

                        // ── KPI + Scoring ──
                        var k = s.Kpi;
                        var sc = s.Score;
                        col.Item().Row(row =>
                        {
                            // KPI
                            row.RelativeItem().Column(kc =>
                            {
                                kc.Item().Text("Kluczowe wskaźniki (12 mies)").SemiBold().FontColor(Granat);
                                kc.Item().PaddingTop(4).Table(tbl =>
                                {
                                    tbl.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(1); });
                                    void Wiersz(string e, string v)
                                    {
                                        tbl.Cell().PaddingVertical(2).Text(e).FontColor(Szary);
                                        tbl.Cell().PaddingVertical(2).AlignRight().Text(v).SemiBold();
                                    }
                                    if (k != null)
                                    {
                                        Wiersz("Obrót 12M", $"{k.Obrot12M:N0} zł");
                                        Wiersz("Śr. wartość faktury", k.LiczbaFaktur12M > 0 ? $"{k.Obrot12M / k.LiczbaFaktur12M:N0} zł" : "—");
                                        Wiersz("Zamówień 12M", $"{k.LiczbaZamowien12M}");
                                        Wiersz("Suma kg 12M", $"{k.SumaKg12M:N0} kg");
                                        Wiersz("Limit kredytowy", $"{k.LimitKredytowy:N0} zł");
                                        Wiersz("Do zapłaty", $"{k.DoZaplaty:N0} zł");
                                        Wiersz("Przeterminowane", $"{k.Przeterminowane:N0} zł" + (k.MaxDniOpoznienia > 0 ? $" (max {k.MaxDniOpoznienia} dni)" : ""));
                                        Wiersz("Ostatnie zamówienie", k.OstatnieZamowienie.HasValue ? $"{k.DniOdOstatniegoZamowienia} dni temu" : "brak");
                                        Wiersz("Reklamacje 12M", $"{k.LiczbaReklamacji12M}");
                                        Wiersz("Ryzyko odejścia", k.ChurnRiskLevel);
                                    }
                                });
                            });

                            row.ConstantItem(16);

                            // Scoring
                            row.RelativeItem().Column(scc =>
                            {
                                scc.Item().Text("Ocena klienta").SemiBold().FontColor(Granat);
                                if (sc != null)
                                {
                                    scc.Item().PaddingTop(4).Background(sc.KategoriaKolor).Padding(8).Text($"{sc.Litera}  ·  {sc.Total}/100  ·  {sc.Kategoria}").FontColor("#FFFFFF").Bold().FontSize(14);
                                    scc.Item().PaddingTop(6).Table(tbl =>
                                    {
                                        tbl.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(1); });
                                        void Skl(string e, int pkt, int waga)
                                        {
                                            tbl.Cell().PaddingVertical(2).Text($"{e} ({waga}%)").FontColor(Szary);
                                            tbl.Cell().PaddingVertical(2).AlignRight().Text($"{pkt}/100").SemiBold();
                                        }
                                        Skl("Obrót 12M", sc.ObrotPkt, sc.WagaObrot);
                                        Skl("Częstotliwość", sc.CzestotliwoscPkt, sc.WagaCzestotliwosc);
                                        Skl("Terminowość", sc.TerminowoscPkt, sc.WagaTerminowosc);
                                        Skl("Długość relacji", sc.DlugoscPkt, sc.WagaDlugosc);
                                    });
                                    scc.Item().PaddingTop(6).Text(sc.RekomendacjaLimitu > 0
                                        ? $"💡 Rekomendowany limit: {sc.RekomendacjaLimitu:N0} zł"
                                        : "💡 Wstrzymać kredyt kupiecki").FontColor(Granat).SemiBold().FontSize(9);
                                    if (!string.IsNullOrWhiteSpace(sc.RekomendacjaOpis))
                                        scc.Item().Text(sc.RekomendacjaOpis).FontColor(Szary).FontSize(8);
                                }
                                else scc.Item().Text("Brak scoringu").FontColor(Szary);
                            });
                        });

                        // ── Wykres obrotu ──
                        col.Item().Text("Obrót miesięczny (faktury, brutto)").SemiBold().FontColor(Granat);
                        col.Item().Image(chartPng).FitWidth();

                        // ── Top towary ──
                        col.Item().Text("Top 10 towarów").SemiBold().FontColor(Granat);
                        col.Item().Table(tbl =>
                        {
                            tbl.ColumnsDefinition(c => { c.ConstantColumn(24); c.RelativeColumn(3); c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(1); });
                            void Hdr(string t) => tbl.Cell().Background(Tlo).Padding(4).Text(t).SemiBold().FontSize(9);
                            Hdr("#"); Hdr("Towar"); Hdr("Suma kg"); Hdr("Wartość"); Hdr("Śr. cena");
                            int i = 1;
                            foreach (var t in s.TopTowary.Take(10))
                            {
                                tbl.Cell().Padding(4).Text($"{i++}").FontSize(9);
                                tbl.Cell().Padding(4).Text(string.IsNullOrWhiteSpace(t.Nazwa) ? $"#{t.KodTowaru}" : t.Nazwa).FontSize(9);
                                tbl.Cell().Padding(4).AlignRight().Text($"{t.SumaKg:N0}").FontSize(9);
                                tbl.Cell().Padding(4).AlignRight().Text($"{t.Wartosc:N0} zł").FontSize(9);
                                tbl.Cell().Padding(4).AlignRight().Text($"{t.SredniaCena:N2} zł").FontSize(9);
                            }
                            if (s.TopTowary.Count == 0)
                                tbl.Cell().ColumnSpan(5).Padding(4).Text("Brak danych").FontColor(Szary).FontSize(9);
                        });

                        // ── Alerty ──
                        if (s.Alerty.Count > 0)
                        {
                            col.Item().Text("Alerty i sygnały").SemiBold().FontColor(Granat);
                            col.Item().Column(ac =>
                            {
                                foreach (var a in s.Alerty)
                                    ac.Item().PaddingVertical(1).Text("• " + a).FontSize(9);
                            });
                        }
                    });

                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span("ZPSP · Customer 360 · ").FontSize(8).FontColor(Szary);
                        t.CurrentPageNumber().FontSize(8).FontColor(Szary);
                        t.Span(" / ").FontSize(8).FontColor(Szary);
                        t.TotalPages().FontSize(8).FontColor(Szary);
                    });
                });
            }).GeneratePdf();
        }
    }
}
