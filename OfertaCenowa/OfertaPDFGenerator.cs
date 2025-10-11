using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Kalendarz1.OfertaCenowa
{
    public class OfertaPDFGenerator
    {
        private KlientOferta _klient;
        private List<TowarOferta> _towary;
        private string _notatki;
        private string _transport;

        private readonly string ColorGreen = "#4B833C";
        private readonly string ColorRed = "#CC2F37";
        private readonly string ColorText = "#374151";
        private readonly string ColorGray = "#9CA3AF";
        private readonly string ColorBorder = "#E5E7EB";
        private readonly string ColorBackground = "#F9FAFB";

        public void GenerujPDF(string sciezka, KlientOferta klient, List<TowarOferta> towary, string notatki, string transport)
        {
            _klient = klient;
            _towary = towary;
            _notatki = notatki;
            _transport = transport;

            QuestPDF.Settings.License = LicenseType.Community;

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Calibri).FontColor(ColorText));

                    page.Header().Element(DodajNaglowek);
                    page.Content().Element(DodajTresc);
                    page.Footer().Element(DodajStopke);
                });
            })
            .GeneratePdf(sciezka);
        }

        private void DodajNaglowek(IContainer container)
        {
            container.Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.ConstantItem(150).Height(60).Element(container =>
                    {
                        try
                        {
                            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                            var resourceName = "Kalendarz1.visual.logo-2-green.png";
                            using var stream = assembly.GetManifestResourceStream(resourceName);
                            if (stream != null)
                            {
                                var bytes = new byte[stream.Length];
                                stream.Read(bytes, 0, bytes.Length);
                                container.Image(bytes);
                            }
                        }
                        catch { /* Błąd logo jest ignorowany */ }
                    });
                    row.RelativeItem();
                    row.ConstantItem(250).AlignRight().Column(col =>
                    {
                        col.Item().Text("Ubojnia Drobiu Piórkowscy").Bold().FontSize(11).FontColor(ColorGreen);
                        col.Item().Text("Koziołki 40, 95-061 Dmosin").FontSize(9);
                        col.Item().Text("NIP: 726-162-54-06").FontSize(9);
                        col.Item().Text("tel: +48 46 874 71 70 | www.piorrkowscy.com.pl").FontSize(9);
                    });
                });
                column.Item().PaddingTop(15).Row(row =>
                {
                    row.RelativeItem().Height(3).Background(ColorGreen);
                    row.ConstantItem(100).Height(3).Background(ColorRed);
                });
            });
        }

        private void DodajTresc(IContainer container)
        {
            container.PaddingVertical(20).Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Oferta Handlowa").FontSize(28).Bold().FontColor(ColorGreen);
                        col.Item().Text($"Numer: OFR/{DateTime.Now:yyyy/MM/dd/HHmm}").FontSize(11).FontColor(ColorGray);
                    });
                    row.ConstantItem(180).AlignRight().Column(col =>
                    {
                        col.Item().Text("Data wystawienia:").FontSize(9);
                        col.Item().Text($"{DateTime.Now:dd MMMM yyyy} r.").Bold().FontSize(11);
                    });
                });

                column.Item().PaddingTop(25).Border(1).BorderColor(ColorBorder).Background(ColorBackground).Padding(15).Column(col =>
                {
                    col.Item().Text("Nabywca:").FontSize(9).FontColor(ColorGray);
                    col.Item().Text(_klient.Nazwa).Bold().FontSize(14);
                    if (!string.IsNullOrWhiteSpace(_klient.Adres)) col.Item().Text(_klient.Adres);
                    if (!string.IsNullOrWhiteSpace(_klient.KodPocztowy)) col.Item().Text($"{_klient.KodPocztowy} {_klient.Miejscowosc}");
                    if (!string.IsNullOrWhiteSpace(_klient.NIP)) col.Item().Text($"NIP: {_klient.NIP}");
                });

                column.Item().PaddingTop(25).Element(DodajTabeleProduktow);
                column.Item().PaddingTop(10).Element(DodajPodsumowanie);

                column.Item().PaddingTop(30).Column(col =>
                {
                    col.Item().Text("Warunki dostawy:").Bold().FontColor(ColorGreen);
                    col.Item().PaddingTop(5).BorderLeft(3).BorderColor(ColorRed).PaddingLeft(10).Text($"Transport: {_transport}");

                    if (!string.IsNullOrWhiteSpace(_notatki))
                    {
                        col.Item().PaddingTop(15).Text("Dodatkowe uwagi:").Bold().FontColor(ColorGreen);
                        col.Item().PaddingTop(5).BorderLeft(3).BorderColor(ColorRed).PaddingLeft(10).Text(_notatki);
                    }
                });
            });
        }

        private void DodajTabeleProduktow(IContainer container)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(30);
                    columns.RelativeColumn(4);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                });
                table.Header(header =>
                {
                    header.Cell().Background(ColorGreen).Padding(8).Text("Lp.").FontColor("#FFFFFF").Bold();
                    header.Cell().Background(ColorGreen).Padding(8).Text("Nazwa produktu").FontColor("#FFFFFF").Bold();
                    header.Cell().Background(ColorGreen).Padding(8).AlignRight().Text("Ilość (kg)").FontColor("#FFFFFF").Bold();
                    header.Cell().Background(ColorGreen).Padding(8).AlignRight().Text("Cena jedn. (zł)").FontColor("#FFFFFF").Bold();
                    header.Cell().Background(ColorGreen).Padding(8).AlignRight().Text("Wartość (zł)").FontColor("#FFFFFF").Bold();
                });
                int lp = 1;
                foreach (var towar in _towary)
                {
                    var bgColor = lp % 2 == 0 ? ColorBackground : "#FFFFFF";
                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder).Padding(6).AlignCenter().Text(lp++.ToString());
                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder).Padding(6).Text(towar.Nazwa);
                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder).Padding(6).AlignRight().Text(towar.Ilosc.ToString("N0"));
                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder).Padding(6).AlignRight().Text(towar.CenaJednostkowa.ToString("N2"));
                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder).Padding(6).AlignRight().Text(towar.Wartosc.ToString("N2")).Bold();
                }
            });
        }

        private void DodajPodsumowanie(IContainer container)
        {
            decimal netto = _towary.Sum(t => t.Wartosc);
            decimal vat = netto * 0.05m; // VAT 5%
            decimal brutto = netto + vat;

            container.AlignRight().Width(280).Column(col =>
            {
                col.Item().Row(row => { row.RelativeItem().Text("Suma netto:"); row.ConstantItem(100).AlignRight().Text($"{netto:N2} zł"); });
                col.Item().Row(row => { row.RelativeItem().Text("Podatek VAT (5%):"); row.ConstantItem(100).AlignRight().Text($"{vat:N2} zł"); });
                col.Item().PaddingTop(5).BorderTop(1).BorderColor(ColorBorder).Row(row =>
                {
                    row.RelativeItem().Text("Do zapłaty:").Bold().FontSize(12);
                    row.ConstantItem(100).AlignRight().Background(ColorRed).Padding(5).AlignCenter().Text($"{brutto:N2} zł").FontColor("#FFFFFF").Bold().FontSize(12);
                });
            });
        }

        private void DodajStopke(IContainer container)
        {
            container.AlignCenter().Text(text =>
            {
                text.DefaultTextStyle(x => x.FontSize(8).FontColor(ColorGray));
                text.Span("W razie pytań pozostajemy do dyspozycji. Strona ");
                text.CurrentPageNumber();
                text.Span(" z ");
                text.TotalPages();
            });
        }
    }
}