using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Kalendarz1.PrzypomnieniePlatnosci
{
    public class PlanSplatyPDFGenerator
    {
        private readonly string ColorGreen = "#4B833C";
        private readonly string ColorText = "#374151";
        private readonly string ColorGray = "#9CA3AF";
        private readonly string ColorBorder = "#E5E7EB";
        private readonly string ColorBackground = "#F9FAFB";
        private readonly string ColorHighlight = "#EFF6FF";

        public void GenerujPDF(string sciezka, DaneKontrahenta daneKontrahenta,
            List<DokumentPlatnosci> dokumenty, decimal kwotaDlugu,
            int liczbaRat, DateTime dataPierwszejRaty, int dniPomiedzyRatami)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Calibri).FontColor(ColorText));

                    page.Header().Element(c => DodajNaglowek(c));
                    page.Content().Element(c => DodajTresc(c, daneKontrahenta, dokumenty, kwotaDlugu,
                        liczbaRat, dataPierwszejRaty, dniPomiedzyRatami));
                    page.Footer().Element(DodajStopke);
                });
            })
            .GeneratePdf(sciezka);
        }

        private void DodajNaglowek(IContainer container)
        {
            container.Column(column =>
            {
                column.Item().ShowOnce().Row(row =>
                {
                    row.ConstantItem(70).Height(70).Element(c =>
                    {
                        try
                        {
                            string logoPath = @"C:\Users\sergi\source\repos\Grafpl\Kalendarz1\Logo.png";
                            if (File.Exists(logoPath))
                            {
                                var bytes = File.ReadAllBytes(logoPath);
                                c.Image(bytes);
                            }
                        }
                        catch { }
                    });

                    row.RelativeItem();

                    row.ConstantItem(220).AlignRight().Column(col =>
                    {
                        col.Item().Text("Ubojnia Drobiu \"Piórkowscy\"").Bold().FontSize(10).FontColor(ColorGreen);
                        col.Item().Text("Jerzy Piórkowski w spadku").Bold().FontSize(9).FontColor(ColorGreen);
                        col.Item().PaddingTop(2).Text("Koziołki 40, 95-061 Dmosin").FontSize(7).FontColor(ColorGray);
                        col.Item().Text("NIP: 726-162-54-06").FontSize(7).FontColor(ColorGray);
                        col.Item().Text("tel: +48 46 874 71 70").FontSize(7).FontColor(ColorGray);
                        col.Item().Text("email: kasa@piorkowscy.com.pl").FontSize(7).FontColor(ColorGray);
                        col.Item().Text("www.piorkowscy.com.pl").FontSize(7).FontColor(ColorGreen).Bold();
                    });
                });

                column.Item().PaddingTop(6).Row(row =>
                {
                    row.RelativeItem().Height(2).Background(ColorGreen);
                });
            });
        }

        private void DodajTresc(IContainer container, DaneKontrahenta daneKontrahenta,
            List<DokumentPlatnosci> dokumenty, decimal kwotaDlugu,
            int liczbaRat, DateTime dataPierwszejRaty, int dniPomiedzyRatami)
        {
            container.PaddingVertical(10).Column(column =>
            {
                // Miejsce i data + adresat
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text($"Dmosin, {DateTime.Now:dd MMMM yyyy} r.")
                        .FontSize(9).FontColor(ColorGray);
                });

                column.Item().PaddingTop(8).Column(col =>
                {
                    col.Item().Text(daneKontrahenta.PelnaNazwa).Bold().FontSize(10);
                    if (!string.IsNullOrEmpty(daneKontrahenta.Adres))
                        col.Item().Text(daneKontrahenta.Adres).FontSize(9);
                    col.Item().Text($"{daneKontrahenta.KodPocztowy} {daneKontrahenta.Miejscowosc}").FontSize(9);
                });

                // Tytuł
                column.Item().PaddingTop(15).AlignCenter().Text("PLAN SPŁATY / UGODA")
                    .FontSize(18).Bold().FontColor(ColorGreen);

                // Wprowadzenie
                column.Item().PaddingTop(15).Text(text =>
                {
                    text.Span("W nawiązaniu do rozmowy dotyczącej uregulowania zaległych należności, ").FontSize(9);
                    text.Span("Ubojnia Drobiu \"Piórkowscy\" Jerzy Piórkowski w spadku").Bold().FontSize(9);
                    text.Span(" (").FontSize(9);
                    text.Span("Wierzyciel").Bold().FontSize(9);
                    text.Span(") oraz ").FontSize(9);
                    text.Span(daneKontrahenta.PelnaNazwa).Bold().FontSize(9);
                    text.Span(" (").FontSize(9);
                    text.Span("Dłużnik").Bold().FontSize(9);
                    text.Span(") uzgadniają następujący plan spłaty zadłużenia.").FontSize(9);
                });

                // § 1 - Kwota zadłużenia
                column.Item().PaddingTop(12).Border(2).BorderColor(ColorGreen).Background(ColorHighlight)
                    .Padding(12).Column(col =>
                    {
                        col.Item().Text("§ 1. Kwota zadłużenia").Bold().FontSize(11).FontColor(ColorGreen);
                        col.Item().PaddingTop(5).Text(text =>
                        {
                            text.Span("1. Strony potwierdzają, że na dzień ").FontSize(9);
                            text.Span(DateTime.Now.ToString("dd.MM.yyyy")).Bold().FontSize(9);
                            text.Span(" Dłużnik jest zobowiązany wobec Wierzyciela do zapłaty kwoty ").FontSize(9);
                            text.Span($"{kwotaDlugu:N2} zł").Bold().FontSize(11).FontColor(ColorGreen);
                            text.Span(".").FontSize(9);
                        });

                        col.Item().PaddingTop(5).Text("2. Powyższe zadłużenie wynika z niezapłaconych faktur wyszczególnionych w załączniku nr 1 do niniejszej ugody.")
                            .FontSize(9);
                    });

                // § 2 - Plan spłaty
                column.Item().PaddingTop(10).Column(col =>
                {
                    col.Item().Text("§ 2. Plan spłaty").Bold().FontSize(11).FontColor(ColorGreen);
                    col.Item().PaddingTop(5).Text($"1. Strony uzgadniają, że spłata zadłużenia nastąpi w {liczbaRat} ratach według poniższego harmonogramu:")
                        .FontSize(9);
                });

                // Tabela rat
                column.Item().PaddingTop(8).Element(c => DodajTabeleRat(c, kwotaDlugu, liczbaRat, dataPierwszejRaty, dniPomiedzyRatami));

                // § 3 - Warunki
                column.Item().PaddingTop(10).Column(col =>
                {
                    col.Item().Text("§ 3. Warunki ugody").Bold().FontSize(11).FontColor(ColorGreen);
                    col.Item().PaddingTop(5).Text("1. W przypadku terminowej spłaty wszystkich rat zgodnie z harmonogramem, " +
                        "Wierzyciel odstępuje od naliczania odsetek ustawowych za opóźnienie.")
                        .FontSize(9);

                    col.Item().PaddingTop(5).Text("2. Wpłaty należy dokonywać na rachunek bankowy Wierzyciela:")
                        .FontSize(9);

                    col.Item().PaddingTop(3).Background(ColorBackground).Padding(8).Column(innerCol =>
                    {
                        innerCol.Item().Text("Bank Pekao S.A.").Bold().FontSize(9).FontColor(ColorGreen);
                        innerCol.Item().Text("60 1240 3060 1111 0010 4888 9213").Bold().FontSize(10).FontColor(ColorGreen);
                        innerCol.Item().PaddingTop(3).Text("W tytule przelewu należy podać: \"Rata [nr] - Plan spłaty\"")
                            .FontSize(8).Italic().FontColor(ColorGray);
                    });

                    col.Item().PaddingTop(5).Text("3. Datą zapłaty jest data wpływu środków na rachunek bankowy Wierzyciela.")
                        .FontSize(9);
                });

                // § 4 - Konsekwencje
                column.Item().PaddingTop(10).Border(2).BorderColor("#F59E0B").Background("#FFF3E0")
                    .Padding(10).Column(col =>
                    {
                        col.Item().Text("§ 4. Konsekwencje opóźnienia").Bold().FontSize(11).FontColor("#F59E0B");
                        col.Item().PaddingTop(5).Text("1. W przypadku opóźnienia w zapłacie którejkolwiek z rat o więcej niż 7 dni, " +
                            "całość pozostałego zadłużenia staje się natychmiast wymagalna.")
                            .FontSize(9);

                        col.Item().PaddingTop(5).Text("2. W przypadku, o którym mowa w ust. 1, Wierzyciel może:")
                            .FontSize(9);

                        col.Item().PaddingTop(3).PaddingLeft(15).Text("a) naliczyć odsetki ustawowe od całości zadłużenia,")
                            .FontSize(9);
                        col.Item().PaddingLeft(15).Text("b) skierować sprawę na drogę postępowania sądowego,")
                            .FontSize(9);
                        col.Item().PaddingLeft(15).Text("c) dokonać wpisu do Krajowego Rejestru Długów.")
                            .FontSize(9);
                    });

                // § 5 - Postanowienia końcowe
                column.Item().PaddingTop(10).Column(col =>
                {
                    col.Item().Text("§ 5. Postanowienia końcowe").Bold().FontSize(11).FontColor(ColorGreen);
                    col.Item().PaddingTop(5).Text("1. Ugoda została sporządzona w dwóch jednobrzmiących egzemplarzach, " +
                        "po jednym dla każdej ze stron.")
                        .FontSize(9);

                    col.Item().PaddingTop(5).Text("2. W sprawach nieuregulowanych niniejszą ugodą zastosowanie mają " +
                        "przepisy Kodeksu cywilnego.")
                        .FontSize(9);
                });

                // Podpisy
                column.Item().PaddingTop(25).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().AlignCenter().Text("........................................").FontSize(9);
                        col.Item().PaddingTop(3).AlignCenter().Text("Wierzyciel").FontSize(8).FontColor(ColorGray);
                        col.Item().AlignCenter().Text("Ubojnia Drobiu \"Piórkowscy\"").FontSize(8).FontColor(ColorGray);
                        col.Item().AlignCenter().Text("Jerzy Piórkowski w spadku").FontSize(8).FontColor(ColorGray);
                    });

                    row.ConstantItem(50);

                    row.RelativeItem().Column(col =>
                    {
                        col.Item().AlignCenter().Text("........................................").FontSize(9);
                        col.Item().PaddingTop(3).AlignCenter().Text("Dłużnik").FontSize(8).FontColor(ColorGray);
                        col.Item().AlignCenter().Text(daneKontrahenta.PelnaNazwa).FontSize(8).FontColor(ColorGray);
                    });
                });

                // Załączniki
                column.Item().PaddingTop(25).Column(col =>
                {
                    col.Item().Text("Załączniki:").Bold().FontSize(9);
                    col.Item().PaddingTop(2).Text("1. Zestawienie faktur / dokumentów sprzedaży").FontSize(8);
                });

                // Zestawienie faktur
                if (dokumenty != null && dokumenty.Any())
                {
                    column.Item().PageBreak();
                    column.Item().Text("Załącznik nr 1 - Zestawienie faktur").Bold().FontSize(12).FontColor(ColorGreen);
                    column.Item().PaddingTop(10).Element(c => DodajZestawienieFaktur(c, dokumenty));
                }
            });
        }

        private void DodajTabeleRat(IContainer container, decimal kwotaDlugu, int liczbaRat,
            DateTime dataPierwszejRaty, int dniPomiedzyRatami)
        {
            decimal kwotaRaty = Math.Round(kwotaDlugu / liczbaRat, 2);
            decimal sumaRat = kwotaRaty * (liczbaRat - 1);
            decimal ostatniaRata = kwotaDlugu - sumaRat;

            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(60);
                    columns.RelativeColumn();
                    columns.ConstantColumn(100);
                });

                table.Header(header =>
                {
                    header.Cell().Background(ColorGreen).Padding(5).AlignCenter()
                        .Text("Rata").FontColor("#FFFFFF").Bold().FontSize(9);
                    header.Cell().Background(ColorGreen).Padding(5).AlignCenter()
                        .Text("Termin płatności").FontColor("#FFFFFF").Bold().FontSize(9);
                    header.Cell().Background(ColorGreen).Padding(5).AlignCenter()
                        .Text("Kwota").FontColor("#FFFFFF").Bold().FontSize(9);
                });

                DateTime dataRaty = dataPierwszejRaty;
                for (int i = 1; i <= liczbaRat; i++)
                {
                    decimal kwota = i == liczbaRat ? ostatniaRata : kwotaRaty;
                    var bgColor = i % 2 == 0 ? ColorBackground : "#FFFFFF";

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder)
                        .Padding(5).AlignCenter().Text($"{i}/{liczbaRat}").FontSize(9).Bold();

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder)
                        .Padding(5).AlignCenter().Text(dataRaty.ToString("dd.MM.yyyy")).FontSize(9);

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder)
                        .Padding(5).AlignRight().Text($"{kwota:N2} zł").FontSize(9).Bold().FontColor(ColorGreen);

                    dataRaty = dataRaty.AddDays(dniPomiedzyRatami);
                }

                // Suma
                table.Cell().Background("#E0E0E0").Padding(5).AlignCenter()
                    .Text("RAZEM").FontSize(9).Bold();
                table.Cell().Background("#E0E0E0").Padding(5);
                table.Cell().Background("#E0E0E0").Padding(5).AlignRight()
                    .Text($"{kwotaDlugu:N2} zł").FontSize(10).Bold().FontColor(ColorGreen);
            });
        }

        private void DodajZestawienieFaktur(IContainer container, List<DokumentPlatnosci> dokumenty)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(25);
                    columns.RelativeColumn(2f);
                    columns.RelativeColumn(1.2f);
                    columns.RelativeColumn(1.2f);
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(1.5f);
                });

                table.Header(header =>
                {
                    Action<string> naglowek = text =>
                    {
                        header.Cell().Background(ColorGreen).Padding(4).AlignCenter()
                            .Text(text).FontColor("#FFFFFF").Bold().FontSize(8);
                    };

                    naglowek("Lp.");
                    naglowek("Numer dokumentu");
                    naglowek("Data");
                    naglowek("Termin");
                    naglowek("Wartość brutto");
                    naglowek("Pozostało");
                });

                foreach (var dok in dokumenty)
                {
                    var bgColor = dok.Lp % 2 == 0 ? ColorBackground : "#FFFFFF";

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder)
                        .Padding(4).AlignCenter().Text(dok.Lp.ToString()).FontSize(8);

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder)
                        .Padding(4).Text(dok.NumerDokumentu).FontSize(8);

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder)
                        .Padding(4).AlignCenter().Text(dok.DataDokumentu.ToString("dd.MM.yyyy")).FontSize(8);

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder)
                        .Padding(4).AlignCenter().Text(dok.TerminPlatnosci.ToString("dd.MM.yyyy")).FontSize(8);

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder)
                        .Padding(4).AlignRight().Text($"{dok.WartoscBrutto:N2}").FontSize(8);

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder)
                        .Padding(4).AlignRight().Text($"{dok.PozostaloDoZaplaty:N2}").FontSize(8).Bold();
                }
            });
        }

        private void DodajStopke(IContainer container)
        {
            container.Column(col =>
            {
                col.Item().BorderTop(1).BorderColor(ColorBorder).PaddingTop(6);
                col.Item().AlignCenter().Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(7).FontColor(ColorGray));
                    text.Span("Ubojnia Drobiu \"Piórkowscy\" Jerzy Piórkowski w spadku | NIP: 726-162-54-06 | kasa@piorkowscy.com.pl | Strona ");
                    text.CurrentPageNumber();
                    text.Span(" z ");
                    text.TotalPages();
                });
            });
        }
    }
}