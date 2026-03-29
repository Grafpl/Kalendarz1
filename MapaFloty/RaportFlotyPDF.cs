using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Kalendarz1.MapaFloty
{
    public static class RaportFlotyPDF
    {
        private const string Zielony = "#2e7d32";
        private const string Niebieski = "#1565c0";
        private const string Szary = "#546e7a";
        private const string JasnyBg = "#f5f5f8";

        public static byte[] Generuj(List<MapaFlotyView.VehiclePosition> vehicles, string tytul = "Raport Floty")
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var moving = vehicles.Count(v => v.IsMoving);
            var stopped = vehicles.Count - moving;
            var inGeo = vehicles.Count(v => v.InGeofence);
            var avgSpd = vehicles.Where(v => v.IsMoving).Select(v => v.Speed).DefaultIfEmpty(0).Average();
            var maxSpd = vehicles.Select(v => v.Speed).DefaultIfEmpty(0).Max();

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
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text(tytul).FontSize(18).Bold().FontColor(Niebieski);
                                c.Item().Text($"Ubojnia Drobiu \"PIÓRKOWSCY\" — Koziołki 40, 95-061 Dmosin")
                                    .FontSize(9).FontColor(Szary);
                            });
                            row.ConstantItem(200).AlignRight().Column(c =>
                            {
                                c.Item().Text($"Data: {DateTime.Now:dd.MM.yyyy HH:mm}").FontSize(10).Bold();
                                c.Item().Text($"Wygenerował: {App.UserFullName ?? "system"}").FontSize(8).FontColor(Szary);
                            });
                        });
                        col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    });

                    page.Content().PaddingTop(10).Column(content =>
                    {
                        // ── Podsumowanie ──
                        content.Item().Row(row =>
                        {
                            void StatBox(string label, string value, string color)
                            {
                                row.RelativeItem().Background(JasnyBg).Padding(8).Column(c =>
                                {
                                    c.Item().Text(value).FontSize(22).Bold().FontColor(color);
                                    c.Item().Text(label).FontSize(8).FontColor(Szary);
                                });
                            }
                            StatBox("Pojazdów", vehicles.Count.ToString(), Niebieski);
                            StatBox("W trasie", moving.ToString(), Zielony);
                            StatBox("Postój", stopped.ToString(), "#e65100");
                            StatBox("W strefie", inGeo.ToString(), "#c62828");
                            StatBox("Śr. km/h", $"{(int)avgSpd}", "#5e35b1");
                            StatBox("Max km/h", maxSpd.ToString(), "#c62828");
                        });

                        content.Item().PaddingTop(12);

                        // ── Tabela pojazdów ──
                        content.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(2.5f); // Pojazd
                                cols.RelativeColumn(2);    // Kierowca
                                cols.RelativeColumn(1);    // Prędkość
                                cols.RelativeColumn(1);    // Status
                                cols.RelativeColumn(2.5f); // Adres
                                cols.RelativeColumn(1.2f); // Do ubojni
                                cols.RelativeColumn(1);    // ETA
                                cols.RelativeColumn(2);    // Kurs
                                cols.RelativeColumn(1.5f); // Aktualizacja
                            });

                            // Nagłówek
                            table.Header(header =>
                            {
                                void H(string t) => header.Cell().Background(Niebieski).Padding(5)
                                    .Text(t).FontSize(8).Bold().FontColor(Colors.White);
                                H("Pojazd"); H("Kierowca"); H("Prędkość"); H("Status");
                                H("Lokalizacja"); H("Do ubojni"); H("Szac. dojazd"); H("Kurs dziś"); H("Ost. sygnał");
                            });

                            var sorted = vehicles.OrderByDescending(v => v.IsMoving)
                                .ThenByDescending(v => v.Speed).ThenBy(v => v.ObjectName).ToList();

                            for (int i = 0; i < sorted.Count; i++)
                            {
                                var v = sorted[i];
                                var bg = i % 2 == 0 ? "#ffffff" : JasnyBg;
                                var statusTxt = v.IsMoving ? "W trasie" : (v.Ignition ? "Zapłon" : "Wył.");
                                var statusColor = v.IsMoving ? Zielony : (v.Ignition ? "#e65100" : Szary);
                                var etaStr = v.EtaMinutes > 0 ? $"ok. {v.EtaMinutes} min" : "—";

                                void C(string t, string? color = null) =>
                                    table.Cell().Background(bg).Padding(4).Text(t).FontSize(8)
                                        .FontColor(color ?? "#263238");

                                var name = v.ObjectName;
                                if (!string.IsNullOrEmpty(v.InternalName)) name += $"\n({v.InternalName})";
                                C(name);
                                C(v.Driver);
                                C(v.Speed.ToString(), v.Speed > 90 ? "#c62828" : null);
                                C(statusTxt, statusColor);
                                C(!string.IsNullOrEmpty(v.Address) ? v.Address : "brak danych");
                                C($"{v.DistToUbojnia:F1} km");
                                C(etaStr, v.EtaMinutes > 0 ? Niebieski : null);
                                C(!string.IsNullOrEmpty(v.KursTrasa) ? v.KursTrasa : "brak");
                                C(!string.IsNullOrEmpty(v.LastUpdate) ? v.LastUpdate : "—");
                            }
                        });

                        // ── Pojazdy w geofence ──
                        var geoVehicles = vehicles.Where(v => v.InGeofence).ToList();
                        if (geoVehicles.Count > 0)
                        {
                            content.Item().PaddingTop(12);
                            content.Item().Text("Pojazdy w strefie Łyszkowice").FontSize(11).Bold().FontColor("#c62828");
                            content.Item().PaddingTop(4);
                            foreach (var v in geoVehicles)
                            {
                                content.Item().Text($"  • {v.ObjectName} — {v.Driver} — {v.Address}")
                                    .FontSize(9).FontColor("#c62828");
                            }
                        }
                    });

                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span("Strona ").FontSize(8).FontColor(Szary);
                        t.CurrentPageNumber().FontSize(8).FontColor(Szary);
                        t.Span(" / ").FontSize(8).FontColor(Szary);
                        t.TotalPages().FontSize(8).FontColor(Szary);
                        t.Span("  |  Mapa Floty — Webfleet.connect").FontSize(7).FontColor(Colors.Grey.Lighten1);
                    });
                });
            }).GeneratePdf();
        }
    }
}
