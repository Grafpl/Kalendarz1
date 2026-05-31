using System;
using System.Collections.Generic;
using System.Globalization;
using Kalendarz1.Kontrakty.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Kalendarz1.Kontrakty.Services
{
    /// <summary>
    /// Generator PDF raportu ARiMR (lista 3-letnich kontraktów + compliance) — QuestPDF.
    /// Do dołączenia do dokumentacji wniosku/audytu ARiMR.
    /// </summary>
    public static class KontraktyPdfExport
    {
        private static readonly CultureInfo Pl = new("pl-PL");
        private const string Zielony = "#2E7D32";

        public static void GenerujRaportArimr(string sciezka, ArimrCompliance c, List<KontraktListItem> kontrakty,
            List<ComplianceTrendPunkt>? trend = null)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            Document.Create(doc =>
            {
                doc.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(32);
                    page.DefaultTextStyle(t => t.FontSize(9.5f).FontFamily("Segoe UI").FontColor("#0F172A"));

                    // ── Nagłówek ──
                    page.Header().Column(col =>
                    {
                        col.Item().Text("Raport ARiMR — kontrakty 3-letnie z hodowcami").FontSize(17).Bold().FontColor(Zielony);
                        col.Item().Text($"Piórkowscy • wygenerowano {DateTime.Now:dd.MM.yyyy HH:mm} • okres: ostatnie 12 miesięcy")
                            .FontSize(9).FontColor("#64748B");
                        col.Item().PaddingTop(6).LineHorizontal(1).LineColor("#E2E8F0");
                    });

                    page.Content().PaddingVertical(12).Column(col =>
                    {
                        // ── Podsumowanie compliance ──
                        col.Item().Background("#F8FAFC").Border(1).BorderColor("#E2E8F0").Padding(12).Row(row =>
                        {
                            row.RelativeItem().Column(s =>
                            {
                                s.Item().Text("Compliance — % surowca pod 3-letnim kontraktem").FontSize(9).FontColor("#64748B");
                                s.Item().Text(c.Status == "BRAK_DANYCH" ? "—" : $"{c.ProcentArimr.ToString("0.0", Pl)} %")
                                    .FontSize(26).Bold().FontColor(KolorStatusu(c.Status));
                                s.Item().Text(OpisStatusu(c)).FontSize(9).FontColor(KolorStatusu(c.Status));
                            });
                            row.RelativeItem().Column(s =>
                            {
                                s.Item().Text("Surowiec ogółem (12 mies.)").FontSize(9).FontColor("#64748B");
                                s.Item().Text(Tony(c.SurowiecCaloscKg)).FontSize(12).SemiBold();
                                s.Item().PaddingTop(4).Text("Surowiec pod ARiMR").FontSize(9).FontColor("#64748B");
                                s.Item().Text(Tony(c.SurowiecArimrKg)).FontSize(12).SemiBold();
                            });
                            row.RelativeItem().Column(s =>
                            {
                                s.Item().Text("Hodowcy ogółem").FontSize(9).FontColor("#64748B");
                                s.Item().Text(c.HodowcowOgolem.ToString()).FontSize(12).SemiBold();
                                s.Item().PaddingTop(4).Text("Hodowcy pod ARiMR").FontSize(9).FontColor("#64748B");
                                s.Item().Text(c.HodowcowArimr.ToString()).FontSize(12).SemiBold();
                            });
                        });

                        // ── Trend zgodności (8.2) ──
                        if (trend != null && trend.Count >= 2)
                        {
                            var pierwszy = trend[0];
                            var ostatni = trend[^1];
                            decimal delta = ostatni.Procent - pierwszy.Procent;
                            decimal min = trend[0].Procent, max = trend[0].Procent;
                            foreach (var p in trend) { if (p.Procent < min) min = p.Procent; if (p.Procent > max) max = p.Procent; }

                            col.Item().PaddingTop(14).Text("Trend zgodności ARiMR").FontSize(12).Bold();
                            col.Item().PaddingTop(2).Text(
                                $"Od {pierwszy.Data:dd.MM.yyyy} ({pierwszy.Procent.ToString("0.0", Pl)}%) " +
                                $"do {ostatni.Data:dd.MM.yyyy} ({ostatni.Procent.ToString("0.0", Pl)}%): " +
                                $"{(delta >= 0 ? "+" : "")}{delta.ToString("0.0", Pl)} pp • " +
                                $"min {min.ToString("0.0", Pl)}% / max {max.ToString("0.0", Pl)}%")
                                .FontSize(9).FontColor(delta >= 0 ? "#16A34A" : "#DC2626");

                            // ostatnie do 12 pomiarów
                            var ost = trend.Count > 12 ? trend.GetRange(trend.Count - 12, 12) : trend;
                            col.Item().PaddingTop(6).Table(tt =>
                            {
                                tt.ColumnsDefinition(cc => { foreach (var _ in ost) cc.RelativeColumn(); });
                                foreach (var p in ost)
                                    tt.Cell().Background("#F1F5F9").Border(1).BorderColor("#E2E8F0").Padding(3)
                                        .Text(p.Data.ToString("dd.MM")).FontSize(7).FontColor("#64748B");
                                foreach (var p in ost)
                                    tt.Cell().Border(1).BorderColor("#E2E8F0").Padding(3)
                                        .Text(p.Procent.ToString("0.0", Pl)).FontSize(8.5f).SemiBold()
                                        .FontColor(p.Procent >= 50 ? "#16A34A" : "#D97706");
                            });
                        }

                        col.Item().PaddingTop(14).Text($"Kontrakty 3-letnie ARiMR ({kontrakty.Count})").FontSize(12).Bold();

                        // ── Tabela kontraktów ──
                        col.Item().PaddingTop(6).Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.ConstantColumn(46);   // Nr
                                cols.RelativeColumn(3);    // Hodowca
                                cols.ConstantColumn(70);   // Status
                                cols.ConstantColumn(62);   // Od
                                cols.ConstantColumn(62);   // Do
                                cols.ConstantColumn(70);   // Wygasa
                            });

                            table.Header(h =>
                            {
                                NaglowekKom(h.Cell(), "NR");
                                NaglowekKom(h.Cell(), "HODOWCA");
                                NaglowekKom(h.Cell(), "STATUS");
                                NaglowekKom(h.Cell(), "OD");
                                NaglowekKom(h.Cell(), "DO");
                                NaglowekKom(h.Cell(), "WYGASA");
                            });

                            foreach (var k in kontrakty)
                            {
                                Kom(table.Cell(), k.NumerKontraktu);
                                Kom(table.Cell(), k.Hodowca);
                                Kom(table.Cell(), k.StatusLabel);
                                Kom(table.Cell(), k.ObowiazujeOd?.ToString("dd.MM.yy") ?? "—");
                                Kom(table.Cell(), k.ObowiazujeDo?.ToString("dd.MM.yy") ?? "bezterm.");
                                Kom(table.Cell(), k.WygasaLabel);
                            }
                        });

                        if (kontrakty.Count == 0)
                            col.Item().PaddingTop(10).Text("Brak aktywnych kontraktów 3-letnich ARiMR.")
                                .FontSize(10).Italic().FontColor("#64748B");
                    });

                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span("ZPSP — Kontrakty Hodowców • strona ").FontSize(8).FontColor("#94A3B8");
                        t.CurrentPageNumber().FontSize(8).FontColor("#94A3B8");
                        t.Span(" / ").FontSize(8).FontColor("#94A3B8");
                        t.TotalPages().FontSize(8).FontColor("#94A3B8");
                    });
                });
            }).GeneratePdf(sciezka);
        }

        private static void NaglowekKom(IContainer cell, string tekst) =>
            cell.Background("#F1F5F9").PaddingVertical(5).PaddingHorizontal(4).BorderBottom(1).BorderColor("#CBD5E1")
                .Text(tekst).FontSize(8).Bold().FontColor("#64748B");

        private static void Kom(IContainer cell, string tekst) =>
            cell.BorderBottom(1).BorderColor("#EEF2F7").PaddingVertical(4).PaddingHorizontal(4)
                .Text(tekst ?? "").FontSize(9);

        private static string KolorStatusu(string s) => s switch
        {
            "OK" => "#16A34A",
            "WARN" => "#D97706",
            "CRIT" => "#DC2626",
            _ => "#64748B"
        };

        private static string OpisStatusu(ArimrCompliance c) => c.Status switch
        {
            "OK" => $"powyżej progu 50% (margines +{c.MarginesPp.ToString("0.0", Pl)} pp)",
            "WARN" => $"blisko progu 50% ({c.MarginesPp.ToString("0.0", Pl)} pp)",
            "CRIT" => $"poniżej progu 50% ({c.MarginesPp.ToString("0.0", Pl)} pp)",
            _ => "brak danych"
        };

        private static string Tony(decimal kg) => kg <= 0 ? "—" : $"{(kg / 1000m).ToString("N0", Pl)} t";
    }
}
