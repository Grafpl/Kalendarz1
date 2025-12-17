using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Kalendarz1.Opakowania.Models;

namespace Kalendarz1.Opakowania.Services
{
    /// <summary>
    /// Generator raportów PDF dla systemu opakowań - używa QuestPDF
    /// Styl zgodny z PrzypomnieniePlatnosciPDFGenerator
    /// </summary>
    public class OpakowaniaPDFGenerator
    {
        // Kolory firmowe
        private readonly string ColorGreen = "#4B833C";
        private readonly string ColorBlue = "#1E88E5";
        private readonly string ColorText = "#374151";
        private readonly string ColorGray = "#9CA3AF";
        private readonly string ColorBorder = "#E5E7EB";
        private readonly string ColorBackground = "#F9FAFB";
        private readonly string ColorRed = "#CC2F37";
        private readonly string ColorOrange = "#F59E0B";
        private readonly string ColorLightGreen = "#E8F5E9";
        private readonly string ColorLightRed = "#FFEBEE";
        private readonly string ColorLightBlue = "#E3F2FD";

        private string _outputDirectory;

        public OpakowaniaPDFGenerator()
        {
            _outputDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "OpakowaniaRaporty");

            if (!Directory.Exists(_outputDirectory))
            {
                Directory.CreateDirectory(_outputDirectory);
            }

            QuestPDF.Settings.License = LicenseType.Community;
        }

        /// <summary>
        /// Generuje raport zestawienia sald dla wybranego typu opakowania
        /// </summary>
        public string GenerujZestawienieSald(
            List<ZestawienieSalda> zestawienie,
            TypOpakowania typOpakowania,
            DateTime dataOd,
            DateTime dataDo,
            string handlowiec = null)
        {
            var fileName = $"Zestawienie_{typOpakowania.Kod}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            var filePath = Path.Combine(_outputDirectory, fileName);

            // Oblicz statystyki
            var daneDoStatystyk = zestawienie.Where(z => z.Kontrahent != "Suma").ToList();
            var liczbKontrahentow = daneDoStatystyk.Count;
            var sumaDodatnich = daneDoStatystyk.Where(z => z.IloscDrugiZakres > 0).Sum(z => z.IloscDrugiZakres);
            var sumaUjemnych = daneDoStatystyk.Where(z => z.IloscDrugiZakres < 0).Sum(z => z.IloscDrugiZakres);
            var liczbaPotwierdzen = daneDoStatystyk.Count(z => z.JestPotwierdzone);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Calibri).FontColor(ColorText));

                    page.Header().Element(c => DodajNaglowek(c,
                        $"ZESTAWIENIE SALD OPAKOWAŃ - {typOpakowania.Nazwa.ToUpper()}",
                        $"Okres: {dataOd:dd.MM.yyyy} - {dataDo:dd.MM.yyyy}" +
                        (string.IsNullOrEmpty(handlowiec) ? "" : $" | Handlowiec: {handlowiec}")));

                    page.Content().PaddingVertical(10).Column(column =>
                    {
                        // Statystyki
                        column.Item().Element(c => DodajStatystykiZestawienia(c,
                            liczbKontrahentow, sumaDodatnich, sumaUjemnych, liczbaPotwierdzen, typOpakowania));

                        column.Item().PaddingTop(10);

                        // Tabela zestawienia
                        column.Item().Element(c => DodajTabeleZestawienia(c, zestawienie, typOpakowania));
                    });

                    page.Footer().Element(DodajStopke);
                });
            })
            .GeneratePdf(filePath);

            return filePath;
        }

        /// <summary>
        /// Generuje raport szczegółowy salda dla kontrahenta
        /// </summary>
        public string GenerujRaportKontrahenta(
            string kontrahentNazwa,
            int kontrahentId,
            SaldoOpakowania saldoAktualne,
            List<DokumentOpakowania> dokumenty,
            List<PotwierdzenieSalda> potwierdzenia,
            DateTime dataOd,
            DateTime dataDo)
        {
            var bezpiecznaNazwa = SanitizeFileName(kontrahentNazwa);
            var fileName = $"Saldo_{bezpiecznaNazwa}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            var filePath = Path.Combine(_outputDirectory, fileName);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Calibri).FontColor(ColorText));

                    page.Header().Element(c => DodajNaglowek(c,
                        "ZESTAWIENIE SALDA OPAKOWAŃ",
                        $"Kontrahent: {kontrahentNazwa}"));

                    page.Content().PaddingVertical(10).Column(column =>
                    {
                        // Okres
                        column.Item().Text($"Okres: {dataOd:dd.MM.yyyy} - {dataDo:dd.MM.yyyy}")
                            .FontSize(10).FontColor(ColorGray);

                        column.Item().PaddingTop(10);

                        // Karty sald
                        column.Item().Element(c => DodajKartySald(c, saldoAktualne));

                        column.Item().PaddingTop(15);

                        // Historia dokumentów
                        if (dokumenty != null && dokumenty.Any())
                        {
                            column.Item().Text("HISTORIA DOKUMENTÓW").Bold().FontSize(12).FontColor(ColorGreen);
                            column.Item().PaddingTop(5);
                            column.Item().Element(c => DodajTabeleDokumentow(c, dokumenty));
                        }

                        // Historia potwierdzeń
                        if (potwierdzenia != null && potwierdzenia.Any())
                        {
                            column.Item().PaddingTop(15);
                            column.Item().Text("HISTORIA POTWIERDZEŃ").Bold().FontSize(12).FontColor(ColorGreen);
                            column.Item().PaddingTop(5);
                            column.Item().Element(c => DodajTabelePotwierdzen(c, potwierdzenia));
                        }
                    });

                    page.Footer().Element(DodajStopke);
                });
            })
            .GeneratePdf(filePath);

            return filePath;
        }

        /// <summary>
        /// Generuje potwierdzenie salda do wydruku/wysłania
        /// </summary>
        public string GenerujPotwierdzenieSalda(
            string kontrahentNazwa,
            string kontrahentAdres,
            TypOpakowania typOpakowania,
            int saldoSystemowe,
            DateTime dataPotwierdzenia)
        {
            var bezpiecznaNazwa = SanitizeFileName(kontrahentNazwa);
            var fileName = $"Potwierdzenie_{typOpakowania.Kod}_{bezpiecznaNazwa}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            var filePath = Path.Combine(_outputDirectory, fileName);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Calibri).FontColor(ColorText));

                    page.Header().Element(c => DodajNaglowekPotwierdzenia(c));

                    page.Content().PaddingVertical(15).Column(column =>
                    {
                        // Dane kontrahenta
                        column.Item().Text(kontrahentNazwa).Bold().FontSize(12);
                        if (!string.IsNullOrEmpty(kontrahentAdres))
                        {
                            column.Item().Text(kontrahentAdres).FontSize(10).FontColor(ColorGray);
                        }

                        column.Item().PaddingTop(20);

                        // Box potwierdzenia
                        column.Item().Border(2).BorderColor(ColorGreen).Background("#F8FFF8")
                            .Padding(20).Column(box =>
                            {
                                box.Item().AlignCenter().Text("POTWIERDZENIE SALDA OPAKOWAŃ")
                                    .Bold().FontSize(16).FontColor(ColorGreen);

                                box.Item().PaddingTop(15);

                                // Pola
                                box.Item().Row(row =>
                                {
                                    row.RelativeItem().Text("Typ opakowania:").FontColor(ColorGray);
                                    row.RelativeItem().AlignRight().Text(typOpakowania.Nazwa).Bold();
                                });

                                box.Item().PaddingTop(5).Row(row =>
                                {
                                    row.RelativeItem().Text("Stan na dzień:").FontColor(ColorGray);
                                    row.RelativeItem().AlignRight().Text(dataPotwierdzenia.ToString("dd.MM.yyyy")).Bold();
                                });

                                box.Item().PaddingTop(15);

                                // Saldo
                                var saldoKolor = saldoSystemowe > 0 ? ColorRed : (saldoSystemowe < 0 ? ColorGreen : ColorGray);
                                var saldoTekst = saldoSystemowe > 0 ? $"+{saldoSystemowe}" : saldoSystemowe.ToString();
                                var saldoOpis = saldoSystemowe > 0
                                    ? "opakowań do zwrotu przez kontrahenta"
                                    : (saldoSystemowe < 0 ? "opakowań do wydania kontrahentowi" : "brak należności");

                                box.Item().AlignCenter().Background("#FFFFFF").Padding(15).Column(saldoBox =>
                                {
                                    saldoBox.Item().AlignCenter().Text("SALDO OPAKOWAŃ")
                                        .FontSize(11).FontColor(ColorGray);
                                    saldoBox.Item().PaddingTop(5).AlignCenter().Text(saldoTekst)
                                        .Bold().FontSize(36).FontColor(saldoKolor);
                                    saldoBox.Item().PaddingTop(3).AlignCenter().Text(saldoOpis)
                                        .FontSize(10).FontColor(ColorGray);
                                });

                                box.Item().PaddingTop(15);
                                box.Item().AlignCenter().Text("Prosimy o potwierdzenie powyższego salda opakowań.")
                                    .FontSize(9).FontColor(ColorGray);
                                box.Item().AlignCenter().Text("W przypadku rozbieżności prosimy o kontakt.")
                                    .FontSize(9).FontColor(ColorGray);

                                // Podpisy
                                box.Item().PaddingTop(40).Row(row =>
                                {
                                    row.RelativeItem().Column(col =>
                                    {
                                        col.Item().BorderTop(1).BorderColor(ColorText).PaddingTop(5);
                                        col.Item().AlignCenter().Text("Podpis i pieczęć wystawcy")
                                            .FontSize(8).FontColor(ColorGray);
                                    });

                                    row.ConstantItem(50);

                                    row.RelativeItem().Column(col =>
                                    {
                                        col.Item().BorderTop(1).BorderColor(ColorText).PaddingTop(5);
                                        col.Item().AlignCenter().Text("Podpis i pieczęć kontrahenta")
                                            .FontSize(8).FontColor(ColorGray);
                                    });
                                });
                            });
                    });

                    page.Footer().Element(DodajStopke);
                });
            })
            .GeneratePdf(filePath);

            return filePath;
        }

        #region Sekcje PDF

        private void DodajNaglowek(IContainer container, string tytul, string podtytul)
        {
            container.Column(column =>
            {
                column.Item().ShowOnce().Row(row =>
                {
                    row.ConstantItem(70).Height(70).Element(c =>
                    {
                        try
                        {
                            // Próbuj znaleźć logo w różnych lokalizacjach
                            var logoLocations = new[]
                            {
                                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logo.png"),
                                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Logo.png"),
                                @"C:\Users\sergi\source\repos\Grafpl\Kalendarz1\Logo.png"
                            };

                            foreach (var logoPath in logoLocations)
                            {
                                if (File.Exists(logoPath))
                                {
                                    var bytes = File.ReadAllBytes(logoPath);
                                    c.Image(bytes);
                                    break;
                                }
                            }
                        }
                        catch { }
                    });

                    row.RelativeItem().PaddingLeft(15).Column(col =>
                    {
                        col.Item().Text(tytul).Bold().FontSize(14).FontColor(ColorGreen);
                        col.Item().PaddingTop(3).Text(podtytul).FontSize(10).FontColor(ColorGray);
                    });

                    row.ConstantItem(200).AlignRight().Column(col =>
                    {
                        col.Item().Text("Ubojnia Drobiu \"Piórkowscy\"").Bold().FontSize(9).FontColor(ColorGreen);
                        col.Item().Text("Jerzy Piórkowski w spadku").Bold().FontSize(8).FontColor(ColorGreen);
                        col.Item().PaddingTop(2).Text("Koziołki 40, 95-061 Dmosin").FontSize(7).FontColor(ColorGray);
                        col.Item().Text("NIP: 726-162-54-06").FontSize(7).FontColor(ColorGray);
                        col.Item().Text("tel: +48 46 874 71 70").FontSize(7).FontColor(ColorGray);
                    });
                });

                column.Item().PaddingTop(6).Row(row =>
                {
                    row.RelativeItem().Height(2).Background(ColorGreen);
                    row.ConstantItem(80).Height(2).Background(ColorBlue);
                });
            });
        }

        private void DodajNaglowekPotwierdzenia(IContainer container)
        {
            container.Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.ConstantItem(70).Height(70).Element(c =>
                    {
                        try
                        {
                            // Próbuj znaleźć logo w różnych lokalizacjach
                            var logoLocations = new[]
                            {
                                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logo.png"),
                                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Logo.png"),
                                @"C:\Users\sergi\source\repos\Grafpl\Kalendarz1\Logo.png"
                            };

                            foreach (var logoPath in logoLocations)
                            {
                                if (File.Exists(logoPath))
                                {
                                    var bytes = File.ReadAllBytes(logoPath);
                                    c.Image(bytes);
                                    break;
                                }
                            }
                        }
                        catch { }
                    });

                    row.RelativeItem();

                    row.ConstantItem(220).AlignRight().Column(col =>
                    {
                        col.Item().Text("Ubojnia Drobiu \"Piórkowscy\"").Bold().FontSize(10).FontColor(ColorGreen);
                        col.Item().Text("Jerzy Piórkowski w spadku").Bold().FontSize(9).FontColor(ColorGreen);
                        col.Item().PaddingTop(2).Text("Koziołki 40, 95-061 Dmosin").FontSize(8).FontColor(ColorGray);
                        col.Item().Text("NIP: 726-162-54-06").FontSize(8).FontColor(ColorGray);
                        col.Item().Text("tel: +48 46 874 71 70").FontSize(8).FontColor(ColorGray);
                    });
                });

                column.Item().PaddingTop(6).Row(row =>
                {
                    row.RelativeItem().Height(2).Background(ColorGreen);
                    row.ConstantItem(60).Height(2).Background(ColorBlue);
                });

                // Data
                column.Item().PaddingTop(10).AlignRight()
                    .Text($"Dmosin, {DateTime.Now:dd MMMM yyyy} r.")
                    .FontSize(9).FontColor(ColorGray);
            });
        }

        private void DodajStatystykiZestawienia(IContainer container, int liczbaKontrahentow,
            int sumaDodatnich, int sumaUjemnych, int liczbaPotwierdzen, TypOpakowania typOpakowania)
        {
            container.Background(ColorBackground).Padding(10).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Liczba kontrahentów").FontSize(8).FontColor(ColorGray);
                    col.Item().Text(liczbaKontrahentow.ToString()).Bold().FontSize(16).FontColor(ColorBlue);
                });

                row.RelativeItem().Column(col =>
                {
                    col.Item().Text($"Winni nam ({typOpakowania.Kod})").FontSize(8).FontColor(ColorGray);
                    col.Item().Text($"+{sumaDodatnich}").Bold().FontSize(16).FontColor(ColorRed);
                });

                row.RelativeItem().Column(col =>
                {
                    col.Item().Text($"My winni ({typOpakowania.Kod})").FontSize(8).FontColor(ColorGray);
                    col.Item().Text(sumaUjemnych.ToString()).Bold().FontSize(16).FontColor(ColorGreen);
                });

                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Potwierdzone").FontSize(8).FontColor(ColorGray);
                    col.Item().Text($"{liczbaPotwierdzen}/{liczbaKontrahentow}").Bold().FontSize(16).FontColor(ColorBlue);
                });
            });
        }

        private void DodajKartySald(IContainer container, SaldoOpakowania saldo)
        {
            if (saldo == null) return;

            container.Row(row =>
            {
                DodajKarteSalda(row, "E2", "Pojemnik", saldo.SaldoE2, ColorBlue);
                row.ConstantItem(10);
                DodajKarteSalda(row, "H1", "Paleta", saldo.SaldoH1, ColorOrange);
                row.ConstantItem(10);
                DodajKarteSalda(row, "EURO", "Paleta", saldo.SaldoEURO, ColorGreen);
                row.ConstantItem(10);
                DodajKarteSalda(row, "PCV", "Plastik", saldo.SaldoPCV, "#9B59B6");
                row.ConstantItem(10);
                DodajKarteSalda(row, "DREW", "Drewno", saldo.SaldoDREW, ColorOrange);
            });
        }

        private void DodajKarteSalda(RowDescriptor row, string kod, string opis, int saldo, string kolor)
        {
            var saldoKolor = saldo > 0 ? ColorRed : (saldo < 0 ? ColorGreen : ColorGray);
            var saldoTekst = saldo == 0 ? "0" : (saldo > 0 ? $"+{saldo}" : saldo.ToString());

            row.RelativeItem().Background(ColorBackground).Padding(10).Column(col =>
            {
                col.Item().Row(r =>
                {
                    r.ConstantItem(35).Background(kolor).Padding(5).AlignCenter()
                        .Text(kod).Bold().FontSize(10).FontColor("#FFFFFF");
                    r.RelativeItem().PaddingLeft(8).AlignMiddle()
                        .Text(opis).FontSize(9).FontColor(ColorGray);
                });
                col.Item().PaddingTop(5).AlignCenter()
                    .Text(saldoTekst).Bold().FontSize(18).FontColor(saldoKolor);
            });
        }

        private void DodajTabeleZestawienia(IContainer container, List<ZestawienieSalda> zestawienie, TypOpakowania typOpakowania)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(25);  // Lp
                    columns.RelativeColumn(4);   // Kontrahent
                    columns.RelativeColumn(2);   // Handlowiec
                    columns.RelativeColumn(1.5f); // Saldo początkowe
                    columns.RelativeColumn(1.5f); // Saldo końcowe
                    columns.RelativeColumn(1.2f); // Zmiana
                    columns.RelativeColumn(1.5f); // Ostatni dok.
                    columns.RelativeColumn(1.5f); // Potwierdzenie
                });

                // Nagłówki
                table.Header(header =>
                {
                    Action<string> naglowek = text =>
                    {
                        header.Cell().Background(ColorGreen).Padding(5).AlignCenter()
                            .Text(text).FontColor("#FFFFFF").Bold().FontSize(8);
                    };

                    naglowek("Lp.");
                    naglowek("Kontrahent");
                    naglowek("Handlowiec");
                    naglowek("Saldo pocz.");
                    naglowek("Saldo końc.");
                    naglowek("Zmiana");
                    naglowek("Ostatni dok.");
                    naglowek("Potwierdz.");
                });

                int lp = 1;
                bool alternate = false;
                foreach (var item in zestawienie.Where(z => z.Kontrahent != "Suma")
                    .OrderByDescending(z => Math.Abs(z.IloscDrugiZakres)))
                {
                    var bgColor = alternate ? ColorBackground : "#FFFFFF";

                    // Lp
                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder)
                        .Padding(4).AlignCenter().Text((lp++).ToString()).FontSize(8);

                    // Kontrahent
                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder)
                        .Padding(4).Text(item.Kontrahent).FontSize(8).Bold();

                    // Handlowiec
                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder)
                        .Padding(4).Text(item.Handlowiec ?? "-").FontSize(8).FontColor(ColorGray);

                    // Saldo początkowe
                    var kolorPocz = item.IloscPierwszyZakres > 0 ? ColorRed :
                        (item.IloscPierwszyZakres < 0 ? ColorGreen : ColorGray);
                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder)
                        .Padding(4).AlignRight().Text(FormatSaldo(item.IloscPierwszyZakres)).FontSize(8).FontColor(kolorPocz);

                    // Saldo końcowe
                    var kolorKonc = item.IloscDrugiZakres > 0 ? ColorRed :
                        (item.IloscDrugiZakres < 0 ? ColorGreen : ColorGray);
                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder)
                        .Padding(4).AlignRight().Text(FormatSaldo(item.IloscDrugiZakres)).Bold().FontSize(9).FontColor(kolorKonc);

                    // Zmiana
                    var kolorZmiana = item.Roznica > 0 ? ColorRed :
                        (item.Roznica < 0 ? ColorGreen : ColorGray);
                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder)
                        .Padding(4).AlignRight().Text(FormatSaldo(item.Roznica)).FontSize(8).FontColor(kolorZmiana);

                    // Ostatni dokument
                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder)
                        .Padding(4).AlignCenter().Text(item.DataOstatniegoDokumentu?.ToString("dd.MM.yyyy") ?? "-").FontSize(8);

                    // Potwierdzenie
                    var potwierdzeneTekst = item.JestPotwierdzone
                        ? $"✓ {item.DataPotwierdzenia?.ToString("dd.MM.yy") ?? ""}"
                        : "-";
                    var potwierdzenieKolor = item.JestPotwierdzone ? ColorGreen : ColorGray;
                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder)
                        .Padding(4).AlignCenter().Text(potwierdzeneTekst).FontSize(8).FontColor(potwierdzenieKolor);

                    alternate = !alternate;
                }
            });
        }

        private void DodajTabeleDokumentow(IContainer container, List<DokumentOpakowania> dokumenty)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.5f); // Data
                    columns.RelativeColumn(1);    // Dzień
                    columns.RelativeColumn(2);    // Nr dok.
                    columns.RelativeColumn(1);    // E2
                    columns.RelativeColumn(1);    // H1
                    columns.RelativeColumn(1);    // EURO
                    columns.RelativeColumn(1);    // PCV
                    columns.RelativeColumn(1);    // DREW
                });

                // Nagłówki
                table.Header(header =>
                {
                    Action<string> naglowek = text =>
                    {
                        header.Cell().Background(ColorGreen).Padding(5).AlignCenter()
                            .Text(text).FontColor("#FFFFFF").Bold().FontSize(8);
                    };

                    naglowek("Data");
                    naglowek("Dzień");
                    naglowek("Nr dokumentu");
                    naglowek("E2");
                    naglowek("H1");
                    naglowek("EURO");
                    naglowek("PCV");
                    naglowek("DREW");
                });

                bool alternate = false;
                foreach (var dok in dokumenty)
                {
                    var bgColor = dok.JestSaldem ? "#FEF9C3" : (alternate ? ColorBackground : "#FFFFFF");

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder)
                        .Padding(3).Text(dok.Data?.ToString("dd.MM.yyyy") ?? "-").FontSize(8);

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder)
                        .Padding(3).AlignCenter().Text(dok.DzienTyg ?? "").FontSize(7).FontColor(ColorGray);

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder)
                        .Padding(3).Text(dok.NrDok ?? "-").FontSize(8);

                    DodajKomorkeSalda(table, dok.E2, bgColor);
                    DodajKomorkeSalda(table, dok.H1, bgColor);
                    DodajKomorkeSalda(table, dok.EURO, bgColor);
                    DodajKomorkeSalda(table, dok.PCV, bgColor);
                    DodajKomorkeSalda(table, dok.DREW, bgColor);

                    if (!dok.JestSaldem) alternate = !alternate;
                }
            });
        }

        private void DodajTabelePotwierdzen(IContainer container, List<PotwierdzenieSalda> potwierdzenia)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.5f); // Data
                    columns.RelativeColumn(1.2f); // Typ
                    columns.RelativeColumn(1.2f); // Potwierdzone
                    columns.RelativeColumn(1.2f); // W systemie
                    columns.RelativeColumn(1.2f); // Różnica
                    columns.RelativeColumn(1.5f); // Status
                    columns.RelativeColumn(2);    // Wprowadził
                });

                // Nagłówki
                table.Header(header =>
                {
                    Action<string> naglowek = text =>
                    {
                        header.Cell().Background(ColorGreen).Padding(5).AlignCenter()
                            .Text(text).FontColor("#FFFFFF").Bold().FontSize(8);
                    };

                    naglowek("Data");
                    naglowek("Typ");
                    naglowek("Potwierdzone");
                    naglowek("W systemie");
                    naglowek("Różnica");
                    naglowek("Status");
                    naglowek("Wprowadził");
                });

                bool alternate = false;
                foreach (var pot in potwierdzenia)
                {
                    var bgColor = alternate ? ColorBackground : "#FFFFFF";

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder)
                        .Padding(3).Text(pot.DataPotwierdzenia.ToString("dd.MM.yyyy")).FontSize(8);

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder)
                        .Padding(3).AlignCenter().Text(pot.KodOpakowania ?? pot.TypOpakowania ?? "-").FontSize(8);

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder)
                        .Padding(3).AlignRight().Text(pot.IloscPotwierdzona.ToString()).FontSize(8);

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder)
                        .Padding(3).AlignRight().Text(pot.SaldoSystemowe.ToString()).FontSize(8);

                    DodajKomorkeSalda(table, pot.Roznica, bgColor);

                    var statusKolor = pot.StatusPotwierdzenia switch
                    {
                        "Potwierdzone" => ColorGreen,
                        "Rozbieżność" => ColorRed,
                        "Oczekujące" => ColorOrange,
                        _ => ColorGray
                    };
                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder)
                        .Padding(3).AlignCenter().Text(pot.StatusPotwierdzenia ?? "-").FontSize(8).FontColor(statusKolor);

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder)
                        .Padding(3).Text(pot.UzytkownikNazwa ?? "-").FontSize(7).FontColor(ColorGray);

                    alternate = !alternate;
                }
            });
        }

        private void DodajKomorkeSalda(TableDescriptor table, int wartosc, string bgColor)
        {
            if (wartosc == 0)
            {
                table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder)
                    .Padding(3).AlignRight().Text("-").FontSize(8).FontColor(ColorGray);
            }
            else
            {
                var kolor = wartosc > 0 ? ColorRed : ColorGreen;
                var tekst = wartosc > 0 ? $"+{wartosc}" : wartosc.ToString();
                table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder)
                    .Padding(3).AlignRight().Text(tekst).Bold().FontSize(8).FontColor(kolor);
            }
        }

        private void DodajStopke(IContainer container)
        {
            container.Column(col =>
            {
                col.Item().BorderTop(1).BorderColor(ColorBorder).PaddingTop(5);

                col.Item().Row(row =>
                {
                    row.RelativeItem().Text($"Wygenerowano: {DateTime.Now:dd.MM.yyyy HH:mm}")
                        .FontSize(7).FontColor(ColorGray);

                    row.RelativeItem().AlignCenter().Text(text =>
                    {
                        text.DefaultTextStyle(x => x.FontSize(7).FontColor(ColorGray));
                        text.Span("Strona ");
                        text.CurrentPageNumber();
                        text.Span(" z ");
                        text.TotalPages();
                    });

                    row.RelativeItem().AlignRight().Text("Ubojnia Drobiu \"Piórkowscy\"")
                        .FontSize(7).FontColor(ColorGray);
                });
            });
        }

        #endregion

        #region Helpers

        private string FormatSaldo(int value)
        {
            if (value == 0) return "0";
            return value > 0 ? $"+{value}" : value.ToString();
        }

        private string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return "raport";
            var invalid = Path.GetInvalidFileNameChars();
            return string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries))
                .Replace(" ", "_").Substring(0, Math.Min(50, fileName.Length));
        }

        #endregion
    }
}
