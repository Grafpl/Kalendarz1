using Kalendarz1.Customer360.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Linq;

namespace Kalendarz1.Customer360.Services
{
    /// <summary>
    /// Eksport pelnej analizy transparentnosci klienta do PDF.
    /// Uzywane przez Sergiusza/Maje przed decyzjami strategicznymi
    /// (blokada kredytu, windykacja, eskalacja jakosci) — dokument do dossiera.
    /// </summary>
    public static class Customer360TransparentnoscPdfExporter
    {
        public static byte[] Generate(string nazwaKlienta, int klientId, TransparentnoscDane d)
        {
            var doc = Document.Create(c =>
            {
                c.Page(p =>
                {
                    p.Size(PageSizes.A4);
                    p.Margin(30);
                    p.DefaultTextStyle(t => t.FontFamily("Segoe UI").FontSize(10).FontColor("#0F172A"));

                    p.Header().Column(col =>
                    {
                        col.Item().Row(r =>
                        {
                            r.RelativeItem().Column(left =>
                            {
                                left.Item().Text("ANALIZA TRANSPARENTNOSCI ODBIORCY").FontSize(16).Bold().FontColor("#1E40AF");
                                left.Item().Text(nazwaKlienta).FontSize(13).SemiBold();
                                left.Item().Text($"Id klienta: {klientId}   Wygenerowano: {DateTime.Now:dd.MM.yyyy HH:mm}").FontSize(9).FontColor("#64748B");
                            });
                            r.ConstantItem(80).Background(d.Klasyfikacja.KolorHex).PaddingVertical(8).AlignCenter()
                                .Text(d.Klasyfikacja.Litera).FontSize(40).Bold().FontColor("#FFFFFF");
                        });
                        col.Item().PaddingTop(4).LineHorizontal(1).LineColor("#E2E8F0");
                    });

                    p.Content().PaddingVertical(10).Column(col =>
                    {
                        // KLASYFIKACJA RYZYKA — duzy nagłowek
                        col.Item().Background("#F8FAFC").Padding(10).Column(s =>
                        {
                            s.Item().Text($"KLASYFIKACJA: {d.Klasyfikacja.Kategoria} ({d.Klasyfikacja.TotalScore}/100)")
                                .FontSize(13).Bold().FontColor(d.Klasyfikacja.KolorHex);
                            s.Item().PaddingTop(4).Text("4 wymiary ryzyka (waga: 30%/30%/30%/10%):").FontSize(9).FontColor("#64748B");
                            DodajWymiar(s, "Reputacyjny", d.Klasyfikacja.RiskReputacyjny, d.Klasyfikacja.OpisReputacyjny);
                            DodajWymiar(s, "Finansowy",   d.Klasyfikacja.RiskFinansowy,    d.Klasyfikacja.OpisFinansowy);
                            DodajWymiar(s, "Operacyjny",  d.Klasyfikacja.RiskOperacyjny,   d.Klasyfikacja.OpisOperacyjny);
                            DodajWymiar(s, "Komunikacyjny", d.Klasyfikacja.RiskKomunikacyjny, d.Klasyfikacja.OpisKomunikacyjny);
                        });

                        // 6 KPI TILE
                        col.Item().PaddingTop(10).Text("KLUCZOWE SYGNALY (12 mies)").FontSize(11).Bold();
                        col.Item().PaddingTop(4).Table(t =>
                        {
                            t.ColumnsDefinition(c2 =>
                            {
                                for (int i = 0; i < 3; i++) c2.RelativeColumn();
                            });
                            DodajKpi(t, "Anulowane", d.LiczbaAnulowanych.ToString(), $"{d.SumaKgAnulowanych:N0} kg · {d.ProcAnulowanych:N1}%");
                            DodajKpi(t, "Reklamacje", d.LiczbaReklamacji.ToString(), $"{d.WartoscReklamacji:N0} zl · {d.LiczbaReklamacjiOtwartych} otwartych");
                            DodajKpi(t, "Niedotrzymanie", d.SredniaRealizacjaProc > 0 ? $"{d.SredniaRealizacjaProc:N0}%" : "—", $"{d.LiczbaPozycjiUcietych} poz. · {d.SumaKgUcietych:N0} kg");
                            DodajKpi(t, "Korekty minus", d.LiczbaKorektMinus.ToString(), $"{d.SumaKorektMinus:N0} zl");
                            DodajKpi(t, "Zmiany terminow", d.LiczbaZmianTerminow.ToString(), $"sr. +{d.SredniePrzesuniecieTerminowDni:N0} dni");
                            DodajKpi(t, "Przeterminowane", $"{d.Przeterminowane:N0} zl", d.MaxDniOpoznienia > 0 ? $"max {d.MaxDniOpoznienia} dni" : "OK");
                        });

                        // REKOMENDACJA
                        col.Item().PaddingTop(10).Background(d.RekomendacjaPoziom switch
                            { "CRITICAL" => "#FEE2E2", "WARNING" => "#FEF3C7", _ => "#F1F5F9" })
                            .Padding(10).Column(s =>
                            {
                                s.Item().Text("REKOMENDACJA SYSTEMU").FontSize(10).Bold().FontColor("#475569");
                                s.Item().PaddingTop(4).Text(d.RekomendacjaTekst).FontSize(10);
                            });

                        // TIMELINE INCYDENTOW
                        if (d.Timeline.Any())
                        {
                            col.Item().PaddingTop(12).Text("OSTATNIE INCYDENTY (timeline)").FontSize(11).Bold();
                            col.Item().PaddingTop(4).Table(t =>
                            {
                                t.ColumnsDefinition(c2 =>
                                {
                                    c2.ConstantColumn(70);
                                    c2.ConstantColumn(80);
                                    c2.RelativeColumn();
                                    c2.ConstantColumn(70);
                                });
                                t.Header(h =>
                                {
                                    h.Cell().Background("#F1F5F9").Padding(4).Text("Data").Bold();
                                    h.Cell().Background("#F1F5F9").Padding(4).Text("Typ").Bold();
                                    h.Cell().Background("#F1F5F9").Padding(4).Text("Opis").Bold();
                                    h.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text("Kwota").Bold();
                                });
                                foreach (var inc in d.Timeline.Take(20))
                                {
                                    t.Cell().Padding(3).Text($"{inc.Data:dd.MM.yyyy}").FontSize(9);
                                    t.Cell().Padding(3).Text(inc.Typ).FontSize(9).FontColor(inc.KolorHex);
                                    t.Cell().Padding(3).Text(inc.Opis).FontSize(9);
                                    t.Cell().Padding(3).AlignRight().Text(inc.Kwota != 0 ? $"{inc.Kwota:N0} zl" : "").FontSize(9)
                                        .FontColor(inc.Kwota < 0 ? "#DC2626" : "#475569");
                                }
                            });
                        }

                        // REKLAMACJE - top 15
                        if (d.Reklamacje.Any())
                        {
                            col.Item().PageBreak();
                            col.Item().Text($"REKLAMACJE — {d.LiczbaReklamacji} sztuk (top 15)").FontSize(11).Bold();
                            col.Item().PaddingTop(4).Table(t =>
                            {
                                t.ColumnsDefinition(c2 =>
                                {
                                    c2.ConstantColumn(70);
                                    c2.ConstantColumn(80);
                                    c2.ConstantColumn(70);
                                    c2.ConstantColumn(60);
                                    c2.RelativeColumn();
                                    c2.ConstantColumn(70);
                                });
                                t.Header(h =>
                                {
                                    h.Cell().Background("#F1F5F9").Padding(4).Text("Data").Bold();
                                    h.Cell().Background("#F1F5F9").Padding(4).Text("Status").Bold();
                                    h.Cell().Background("#F1F5F9").Padding(4).Text("Priorytet").Bold();
                                    h.Cell().Background("#F1F5F9").Padding(4).Text("Zrodlo").Bold();
                                    h.Cell().Background("#F1F5F9").Padding(4).Text("Typ").Bold();
                                    h.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text("Kwota").Bold();
                                });
                                foreach (var r in d.Reklamacje.Take(15))
                                {
                                    t.Cell().Padding(3).Text($"{r.DataZgloszenia:dd.MM.yyyy}").FontSize(9);
                                    t.Cell().Padding(3).Text(r.StatusV2Etykieta).FontSize(9).FontColor(r.StatusKolor);
                                    t.Cell().Padding(3).Text(r.Priorytet).FontSize(9);
                                    t.Cell().Padding(3).Text(r.ZrodloZgloszenia).FontSize(9);
                                    t.Cell().Padding(3).Text(r.TypReklamacji).FontSize(9);
                                    t.Cell().Padding(3).AlignRight().Text($"{r.Kwota:N0} zl").FontSize(9);
                                }
                            });
                        }
                    });

                    p.Footer().AlignCenter().Text(x =>
                    {
                        x.CurrentPageNumber().FontSize(8).FontColor("#94A3B8");
                        x.Span(" / ").FontSize(8).FontColor("#94A3B8");
                        x.TotalPages().FontSize(8).FontColor("#94A3B8");
                        x.Span("   |   Customer360 — Analiza Transparentnosci   |   ").FontSize(8).FontColor("#94A3B8");
                        x.Span("ZPSP Piorkowscy").FontSize(8).FontColor("#94A3B8");
                    });
                });
            });
            return doc.GeneratePdf();
        }

        private static void DodajWymiar(QuestPDF.Infrastructure.IContainer s, string nazwa, int pkt, string opis)
        {
            string kolor = pkt < 20 ? "#16A34A" : pkt < 50 ? "#EAB308" : pkt < 75 ? "#F97316" : "#DC2626";
            s.PaddingTop(6).Row(r =>
            {
                r.ConstantItem(110).Text(nazwa).FontSize(10).SemiBold();
                r.ConstantItem(50).Text($"{pkt}/100").FontSize(10).Bold().FontColor(kolor);
                r.RelativeItem().Text(opis).FontSize(9).FontColor("#64748B");
            });
        }

        private static void DodajKpi(QuestPDF.Infrastructure.ITableContainer t, string label, string val, string sub)
        {
            t.Cell().Border(1).BorderColor("#E2E8F0").Padding(6).Column(c =>
            {
                c.Item().Text(label.ToUpper()).FontSize(8).FontColor("#64748B").SemiBold();
                c.Item().PaddingTop(2).Text(val).FontSize(14).Bold();
                c.Item().Text(sub).FontSize(8).FontColor("#94A3B8");
            });
        }
    }
}
