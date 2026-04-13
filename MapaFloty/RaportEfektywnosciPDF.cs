using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Kalendarz1.MapaFloty
{
    public static class RaportEfektywnosciPDF
    {
        public static byte[] Generuj(List<RaportEfektywnosciWindow.VehicleReport> reports, DateTime from, DateTime to)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            return Document.Create(doc =>
            {
                doc.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(25);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Segoe UI"));

                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Text($"RAPORT EFEKTYWNOŚCI FLOTY").FontSize(16).Bold().FontColor("#1a237e");
                            row.ConstantItem(200).AlignRight().Column(c =>
                            {
                                c.Item().Text($"Okres: {from:dd.MM.yyyy} — {to:dd.MM.yyyy}").FontSize(10).Bold();
                                c.Item().Text($"Wygenerował: {App.UserFullName ?? "system"} | {DateTime.Now:dd.MM.yyyy HH:mm}").FontSize(8).FontColor("#78909c");
                            });
                        });
                        col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    });

                    page.Content().PaddingTop(8).Column(content =>
                    {
                        // Podsumowanie
                        var totalKm = reports.Sum(r => r.TotalKm);
                        var totalTrip = reports.Sum(r => r.TotalTripMin);
                        var totalFuel = reports.Sum(r => r.TotalFuelL);
                        content.Item().Text($"Podsumowanie: {reports.Count} pojazdów | {totalKm:F0} km | {totalTrip / 60}h jazdy | {totalFuel:F0}L paliwa")
                            .FontSize(11).Bold().FontColor("#283593");
                        content.Item().PaddingTop(8);

                        // Tabela
                        content.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(2); // Pojazd
                                cols.RelativeColumn(1.2f); // km
                                cols.RelativeColumn(1.5f); // Czas jazdy
                                cols.RelativeColumn(1.5f); // Postój
                                cols.RelativeColumn(1); // Paliwo
                                cols.RelativeColumn(0.8f); // Trasy
                                cols.RelativeColumn(0.8f); // Dni
                                cols.RelativeColumn(1.2f); // Śr. km/dzień
                            });

                            table.Header(h =>
                            {
                                void H(string t) => h.Cell().Background("#1565c0").Padding(5).Text(t).FontSize(8).Bold().FontColor(Colors.White);
                                H("Pojazd"); H("Dystans"); H("Czas jazdy"); H("Czas postoju"); H("Paliwo"); H("Trasy"); H("Dni"); H("Śr. km/dzień");
                            });

                            for (int i = 0; i < reports.Count; i++)
                            {
                                var r = reports[i];
                                var bg = i % 2 == 0 ? "#ffffff" : "#f5f5f8";
                                var avgKm = r.ActiveDays > 0 ? r.TotalKm / r.ActiveDays : 0;

                                void C(string t, string? c = null) => table.Cell().Background(bg).Padding(4).Text(t).FontSize(8).FontColor(c ?? "#263238");
                                C(r.Vehicle, "#1a237e");
                                C($"{r.TotalKm:F0} km");
                                C($"{r.TotalTripMin / 60}h {r.TotalTripMin % 60}min", "#2e7d32");
                                C($"{r.TotalStandMin / 60}h {r.TotalStandMin % 60}min", "#e65100");
                                C(r.TotalFuelL > 0 ? $"{r.TotalFuelL:F0}L" : "—");
                                C($"{r.TotalTours}");
                                C($"{r.ActiveDays}");
                                C($"{avgKm:F0} km");
                            }
                        });
                    });

                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span("Strona ").FontSize(8).FontColor("#90a4ae");
                        t.CurrentPageNumber().FontSize(8);
                        t.Span(" / ").FontSize(8).FontColor("#90a4ae");
                        t.TotalPages().FontSize(8);
                    });
                });
            }).GeneratePdf();
        }
    }
}
