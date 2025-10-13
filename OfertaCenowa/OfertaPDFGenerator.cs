using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Kalendarz1.OfertaCenowa
{
    public class OfertaPDFGenerator
    {
        private KlientOferta _klient;
        private List<TowarOferta> _towary;
        private string _notatki;
        private string _transport;
        private ParametryOferty _parametry;
        private JezykOferty _jezyk;

        private readonly string ColorGreen = "#4B833C";
        private readonly string ColorGreenLight = "#6EAD5A";
        private readonly string ColorRed = "#CC2F37";
        private readonly string ColorText = "#374151";
        private readonly string ColorGray = "#9CA3AF";
        private readonly string ColorBorder = "#E5E7EB";
        private readonly string ColorBackground = "#F9FAFB";
        private readonly string ColorHighlight = "#FEF3C7";

        private readonly Dictionary<string, DaneKonta> KontaBankowe = new Dictionary<string, DaneKonta>
        {
            {
                "PLN", new DaneKonta
                {
                    NumerKonta = "60 1240 3060 1111 0010 4888 9213",
                    IBAN = "PL60 1240 3060 1111 0010 4888 9213",
                    NazwaBanku = "Bank Pekao S.A.",
                    SWIFT = "PKOPPLPW",
                    AdresBanku = "ul. Grzybowska 53/57, 00-844 Warszawa",
                    Waluta = "PLN"
                }
            },
            {
                "EUR", new DaneKonta
                {
                    NumerKonta = "70 1240 3060 1978 0010 4888 9721",
                    IBAN = "PL70 1240 3060 1978 0010 4888 9721",
                    NazwaBanku = "Bank Pekao S.A.",
                    SWIFT = "PKOPPLPW",
                    AdresBanku = "ul. Grzybowska 53/57, 00-844 Warszawa",
                    Waluta = "EUR"
                }
            }
        };

        // Słownik tłumaczeń produktów PL -> EN
        private readonly Dictionary<string, string> TlumaczeniaProduktow = new Dictionary<string, string>
        {
            // Części kurczaka
            {"KURCZAK CALY", "WHOLE CHICKEN"},
            {"KURCZAK CAŁY", "WHOLE CHICKEN"},
            {"KURCZAK", "CHICKEN"},
            {"FILET", "FILLET"},
            {"FILET Z SKOR", "FILLET WITH SKIN"},
            {"FILET Z SKÓRĄ", "FILLET WITH SKIN"},
            {"UDZIEC", "THIGH"},
            {"UDŹCE", "THIGH"},
            {"SKRZYDLA", "WINGS"},
            {"SKRZYDŁO", "WING"},
            {"PODUDZIE", "DRUMSTICK"},
            {"PIERSI", "BREAST"},
            {"PIERŚ", "BREAST"},
            {"KORPUS", "CARCASS"},
            {"NOGA", "LEG"},
            {"NÓŻKA", "LEG"},
            {"PODBIODRKI", "OYSTER MEAT"},
            {"PODBRÓDEK", "OYSTER MEAT"},
            {"WĄTROBKI", "LIVER"},
            {"WĄTROBA", "LIVER"},
            {"SERCA", "HEARTS"},
            {"SERCE", "HEART"},
            {"ZOLADKI", "GIZZARDS"},
            {"ŻOŁĄDKI", "GIZZARDS"},
            {"ŻOŁĄDEK", "GIZZARD"},
            
            // Przetwory
            {"SZYNKA", "HAM"},
            {"KIELBASA", "SAUSAGE"},
            {"KIEŁBASA", "SAUSAGE"},
            {"KABANOS", "KABANOS"},
            {"PARÓWKA", "FRANKFURTER"},
            
            // Ogólne
            {"ŚWIEŻE", "FRESH"},
            {"SWIEZE", "FRESH"},
            {"MROŻONE", "FROZEN"},
            {"MROZONE", "FROZEN"},
            {"PAKOWANE", "PACKED"},
            {"BEZ SKÓRY", "SKINLESS"},
            {"BEZ SKORY", "SKINLESS"},
            {"ZE SKÓRĄ", "WITH SKIN"},
            {"ZE SKORA", "WITH SKIN"},
            {"MIELONE", "MINCED"},
            {"CALE", "WHOLE"},
            {"CAŁE", "WHOLE"},
            {"PORCJOWANE", "PORTIONED"},
        };

        public void GenerujPDF(string sciezka, KlientOferta klient, List<TowarOferta> towary, string notatki, string transport, ParametryOferty parametry)
        {
            _klient = klient;
            _towary = towary;
            _notatki = notatki;
            _transport = transport;
            _parametry = parametry;
            _jezyk = parametry.Jezyk;

            QuestPDF.Settings.License = LicenseType.Community;

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(25); // Zmniejszone marginesy
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Calibri).FontColor(ColorText));

                    page.Header().Element(DodajNaglowek);
                    page.Content().Element(DodajTresc);
                    page.Footer().Element(DodajStopke);
                });
            })
            .GeneratePdf(sciezka);
        }

        private string T(string pl, string en)
        {
            return _jezyk == JezykOferty.Polski ? pl : en;
        }

        private string TlumaczProdukt(string nazwaPL)
        {
            if (_jezyk == JezykOferty.Polski)
                return nazwaPL;

            string nazwaTrans = nazwaPL.ToUpper();
            foreach (var tlum in TlumaczeniaProduktow)
            {
                nazwaTrans = nazwaTrans.Replace(tlum.Key, tlum.Value);
            }

            // Jeśli tekst się nie zmienił (nie było tłumaczenia), zwróć oryginalny tekst z zachowaniem wielkości liter
            if (nazwaTrans == nazwaPL.ToUpper())
                return nazwaPL;

            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(nazwaTrans.ToLower());
        }

        private void DodajNaglowek(IContainer container)
        {
            container.Column(column =>
            {
                column.Item().Row(row =>
                {
                    // Wybór rozmiaru i logo na podstawie typu
                    if (_parametry.TypLogo == TypLogo.Okragle)
                    {
                        // Logo okrągłe (logo.png) - mniejsze, kwadratowe
                        row.ConstantItem(100).Height(100).Element(container =>
                        {
                            try
                            {
                                string logoPath = @"C:\Users\PC\source\repos\Grafpl\Kalendarz1\logo.png";
                                if (File.Exists(logoPath))
                                {
                                    var bytes = File.ReadAllBytes(logoPath);
                                    container.Image(bytes);
                                }
                            }
                            catch { /* Błąd logo jest ignorowany */ }
                        });
                    }
                    else
                    {
                        // Logo długie (logo-2-green.png) - większe, panoramiczne
                        row.ConstantItem(280).Height(100).Element(container =>
                        {
                            try
                            {
                                string logoPath = @"C:\Users\PC\source\repos\Grafpl\Kalendarz1\logo-2-green.png";
                                if (File.Exists(logoPath))
                                {
                                    var bytes = File.ReadAllBytes(logoPath);
                                    container.Image(bytes);
                                }
                            }
                            catch { /* Błąd logo jest ignorowany */ }
                        });
                    }

                    row.RelativeItem();
                    row.ConstantItem(230).AlignRight().Column(col =>
                    {
                        col.Item().Text("Ubojnia Drobiu Piórkowscy").Bold().FontSize(11).FontColor(ColorGreen);
                        col.Item().PaddingTop(2).Text("Koziołki 40, 95-061 Dmosin").FontSize(8).FontColor(ColorGray);
                        col.Item().Text("NIP: 726-162-54-06").FontSize(8).FontColor(ColorGray);
                        col.Item().Text("tel: +48 46 874 71 70").FontSize(8).FontColor(ColorGray);
                        col.Item().Text("www.piorkowscy.com.pl").FontSize(8).FontColor(ColorGreen).Bold();
                    });
                });
                column.Item().PaddingTop(8).Row(row =>
                {
                    row.RelativeItem().Height(3).Background(ColorGreen);
                    row.ConstantItem(80).Height(3).Background(ColorRed);
                });
            });
        }

        private void DodajTresc(IContainer container)
        {
            int iloscTowarow = _towary.Count;
            float skalaWierszy = iloscTowarow > 15 ? 0.7f : iloscTowarow > 10 ? 0.85f : 1.0f;

            container.PaddingVertical(12).Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(T("Oferta Handlowa", "Commercial Offer")).FontSize(24).Bold().FontColor(ColorGreen);
                        col.Item().PaddingTop(2).Text($"{T("Numer", "Number")}: OFR/{DateTime.Now:yyyy/MM/dd/HHmm}").FontSize(9).FontColor(ColorGray);
                    });
                    row.ConstantItem(160).AlignRight().Column(col =>
                    {
                        col.Item().Text(T("Data wystawienia:", "Issue date:")).FontSize(8).FontColor(ColorGray);

                        // Formatowanie daty w zależności od języka
                        string dataStr;
                        if (_jezyk == JezykOferty.English)
                        {
                            dataStr = DateTime.Now.ToString("dd MMMM yyyy", CultureInfo.GetCultureInfo("en-US"));
                        }
                        else
                        {
                            dataStr = DateTime.Now.ToString("dd MMMM yyyy", CultureInfo.GetCultureInfo("pl-PL")) + " r.";
                        }

                        col.Item().PaddingTop(2).Text(dataStr).Bold().FontSize(10).FontColor(ColorGreen);
                    });
                });

                column.Item().PaddingTop(15).Border(2).BorderColor(ColorGreen).Background(ColorBackground).Padding(18).Column(col =>
                {
                    col.Item().Text(T("Nabywca:", "Buyer:")).FontSize(10).FontColor(ColorGray).Bold();
                    col.Item().PaddingTop(5).Text(_klient.Nazwa).Bold().FontSize(16).FontColor(ColorGreen);
                    if (!string.IsNullOrWhiteSpace(_klient.Adres))
                        col.Item().PaddingTop(3).Text(_klient.Adres).FontSize(11);
                    if (!string.IsNullOrWhiteSpace(_klient.KodPocztowy))
                        col.Item().Text($"{_klient.KodPocztowy} {_klient.Miejscowosc}").FontSize(11);
                    if (!string.IsNullOrWhiteSpace(_klient.NIP))
                        col.Item().PaddingTop(3).Text($"NIP: {_klient.NIP}").FontSize(11).FontColor(ColorGray);
                });

                column.Item().PaddingTop(15).Element(c => DodajTabeleProduktow(c, skalaWierszy));

                if (!_parametry.PokazTylkoCeny && _parametry.PokazIlosc)
                {
                    column.Item().PaddingTop(8).Element(DodajPodsumowanie);
                }

                column.Item().PaddingTop(15).Element(DodajWarunkiHandlowe);

                if (!string.IsNullOrWhiteSpace(_notatki))
                {
                    column.Item().PaddingTop(10).Border(1).BorderColor(ColorGreen).BorderLeft(3).Background("#F0FDF4")
                        .Padding(8).Column(col =>
                        {
                            col.Item().Text($"📝 {T("Dodatkowe uwagi:", "Additional notes:")}").Bold().FontSize(9).FontColor(ColorGreen);
                            col.Item().PaddingTop(3).Text(_notatki).FontSize(8);
                        });
                }
            });
        }

        private void DodajTabeleProduktow(IContainer container, float skala)
        {
            bool tylkoCena = _parametry.PokazTylkoCeny;
            int paddingKomorek = (int)(5 * skala);
            int fontSizeHeader = (int)(9 * skala);
            int fontSizeData = (int)(8 * skala);

            container.Table(table =>
            {
                List<string> kolumny = new List<string> { "Lp.", T("Nazwa produktu", "Product name") };

                if (!tylkoCena && _parametry.PokazIlosc) kolumny.Add(T("Ilość (kg)", "Quantity (kg)"));
                if (_parametry.PokazCene) kolumny.Add(T("Cena jedn. (zł)", "Unit price (PLN)"));
                if (_parametry.PokazOpakowanie) kolumny.Add(T("Opakowanie", "Packaging"));
                if (!tylkoCena && _parametry.PokazIlosc) kolumny.Add(T("Wartość (zł)", "Value (PLN)"));

                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(30);
                    columns.RelativeColumn(4);

                    if (!tylkoCena && _parametry.PokazIlosc) columns.RelativeColumn(1.3f);
                    if (_parametry.PokazCene) columns.RelativeColumn(1.3f);
                    if (_parametry.PokazOpakowanie) columns.RelativeColumn(1.3f);
                    if (!tylkoCena && _parametry.PokazIlosc) columns.RelativeColumn(1.5f);
                });

                table.Header(header =>
                {
                    header.Cell().Background(ColorGreen).Padding(paddingKomorek).Text("Lp.").FontColor("#FFFFFF").Bold().FontSize(fontSizeHeader);
                    header.Cell().Background(ColorGreen).Padding(paddingKomorek).Text(T("Nazwa produktu", "Product name")).FontColor("#FFFFFF").Bold().FontSize(fontSizeHeader);

                    if (!tylkoCena && _parametry.PokazIlosc)
                        header.Cell().Background(ColorGreen).Padding(paddingKomorek).AlignRight().Text(T("Ilość (kg)", "Qty (kg)")).FontColor("#FFFFFF").Bold().FontSize(fontSizeHeader);

                    if (_parametry.PokazCene)
                        header.Cell().Background(ColorGreen).Padding(paddingKomorek).AlignRight().Text(T("Cena (zł)", "Price (PLN)")).FontColor("#FFFFFF").Bold().FontSize(fontSizeHeader);

                    if (_parametry.PokazOpakowanie)
                        header.Cell().Background(ColorGreen).Padding(paddingKomorek).AlignCenter().Text(T("Opak.", "Pack.")).FontColor("#FFFFFF").Bold().FontSize(fontSizeHeader);

                    if (!tylkoCena && _parametry.PokazIlosc)
                        header.Cell().Background(ColorGreen).Padding(paddingKomorek).AlignRight().Text(T("Wartość (zł)", "Value (PLN)")).FontColor("#FFFFFF").Bold().FontSize(fontSizeHeader);
                });

                int lp = 1;
                foreach (var towar in _towary)
                {
                    var bgColor = lp % 2 == 0 ? ColorBackground : "#FFFFFF";

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder).Padding(paddingKomorek).AlignCenter()
                        .Text(lp++.ToString()).FontSize(fontSizeData);

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder).Padding(paddingKomorek)
                        .Text(TlumaczProdukt(towar.Nazwa)).FontSize(fontSizeData).FontColor(ColorText);

                    if (!tylkoCena && _parametry.PokazIlosc)
                        table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder).Padding(paddingKomorek).AlignRight()
                            .Text(towar.Ilosc.ToString("N0")).FontSize(fontSizeData);

                    if (_parametry.PokazCene)
                        table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder).Padding(paddingKomorek).AlignRight()
                            .Text(towar.CenaJednostkowa.ToString("N2")).FontSize(fontSizeData).Bold().FontColor(ColorGreen);

                    if (_parametry.PokazOpakowanie)
                        table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder).Padding(paddingKomorek).AlignCenter()
                            .Text(towar.Opakowanie).FontSize((int)(7 * skala)).FontColor(ColorGray);

                    if (!tylkoCena && _parametry.PokazIlosc)
                        table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder).Padding(paddingKomorek).AlignRight()
                            .Text(towar.Wartosc.ToString("N2")).FontSize(fontSizeData).Bold().FontColor(ColorGreen);
                }
            });
        }

        private void DodajPodsumowanie(IContainer container)
        {
            decimal netto = _towary.Sum(t => t.Wartosc);
            decimal vat = netto * 0.05m;
            decimal brutto = netto + vat;

            container.AlignRight().Width(260).Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Text(T("Suma netto:", "Net total:")).FontSize(9);
                    row.ConstantItem(100).AlignRight().Text($"{netto:N2} zł").FontSize(9);
                });
                col.Item().PaddingTop(2).Row(row =>
                {
                    row.RelativeItem().Text(T("Podatek VAT (5%):", "VAT tax (5%):")).FontSize(9);
                    row.ConstantItem(100).AlignRight().Text($"{vat:N2} zł").FontSize(9);
                });
                col.Item().PaddingTop(5).BorderTop(2).BorderColor(ColorGreen).PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Text(T("Do zapłaty:", "To pay:")).Bold().FontSize(11).FontColor(ColorGreen);
                    row.ConstantItem(100).AlignRight().Background(ColorGreen).Padding(6).AlignCenter()
                       .Text($"{brutto:N2} zł").FontColor("#FFFFFF").Bold().FontSize(12);
                });
            });
        }

        private void DodajWarunkiHandlowe(IContainer container)
        {
            var daneKonta = KontaBankowe[_parametry.WalutaKonta];
            DateTime dataWystawienia = DateTime.Now;
            DateTime terminPlatnosci = _parametry.DniPlatnosci > 0
                ? dataWystawienia.AddDays(_parametry.DniPlatnosci)
                : dataWystawienia;

            // Tłumaczenie terminu płatności
            string terminPlatnosciTekst = _parametry.TerminPlatnosci;
            if (_jezyk == JezykOferty.English)
            {
                if (terminPlatnosciTekst.Contains("Przedpłata"))
                {
                    terminPlatnosciTekst = "Prepayment";
                }
                else if (terminPlatnosciTekst.Contains("dni"))
                {
                    // Zamiana "7 dni" na "7 days"
                    terminPlatnosciTekst = terminPlatnosciTekst.Replace(" dni", " days");
                }
            }

            container.Column(col =>
            {
                col.Item().Text(T("Warunki handlowe:", "Commercial terms:")).Bold().FontSize(11).FontColor(ColorGreen);

                col.Item().PaddingTop(8).Border(2).BorderColor(ColorGreen).Background(ColorBackground).Padding(12).Row(row =>
                {
                    row.RelativeItem().Column(leftCol =>
                    {
                        leftCol.Item().Row(r =>
                        {
                            r.ConstantItem(110).Text($"🚚 {T("Transport:", "Transport:")}").FontSize(8).FontColor(ColorGray).Bold();

                            string transportTekst = _transport;
                            if (_jezyk == JezykOferty.English)
                            {
                                if (_transport.Contains("własny"))
                                    transportTekst = "Own transport";
                                else if (_transport.Contains("klienta"))
                                    transportTekst = "Customer transport";
                            }

                            r.RelativeItem().Text(transportTekst).Bold().FontSize(9).FontColor(ColorGreen);
                        });

                        if (_parametry.PokazTerminPlatnosci)
                        {
                            leftCol.Item().PaddingTop(5).Row(r =>
                            {
                                r.ConstantItem(110).Text($"💳 {T("Termin płatności:", "Payment term:")}").FontSize(8).FontColor(ColorGray).Bold();
                                r.RelativeItem().Text(terminPlatnosciTekst).Bold().FontSize(9).FontColor(ColorGreen);
                            });

                            if (_parametry.DniPlatnosci > 0)
                            {
                                leftCol.Item().PaddingTop(3).Row(r =>
                                {
                                    r.ConstantItem(110).Text($"📅 {T("Zapłata do:", "Payment by:")}").FontSize(8).FontColor(ColorGray).Bold();
                                    r.RelativeItem().Text(terminPlatnosci.ToString("dd.MM.yyyy")).Bold().FontSize(9).FontColor(ColorRed);
                                });
                            }
                        }
                    });

                    row.ConstantItem(240).BorderLeft(2).BorderColor(ColorGreen).PaddingLeft(12).Column(rightCol =>
                    {
                        rightCol.Item().Text($"💰 {T("Dane do przelewu:", "Bank details:")}").Bold().FontSize(9).FontColor(ColorGreen);

                        rightCol.Item().PaddingTop(5).Row(r =>
                        {
                            r.ConstantItem(55).Text(T("Bank:", "Bank:")).FontSize(8).FontColor(ColorGray);
                            r.RelativeItem().Text(daneKonta.NazwaBanku).FontSize(8).Bold();
                        });

                        rightCol.Item().PaddingTop(2).Row(r =>
                        {
                            r.ConstantItem(55).Text("SWIFT:").FontSize(8).FontColor(ColorGray);
                            r.RelativeItem().Text(daneKonta.SWIFT).FontSize(8).Bold().FontColor(ColorGreen);
                        });

                        rightCol.Item().PaddingTop(2).Row(r =>
                        {
                            r.ConstantItem(55).Text($"{T("Konto", "Account")} {daneKonta.Waluta}:").FontSize(8).FontColor(ColorGray);
                            r.RelativeItem().Text(daneKonta.IBAN).FontSize(8).Bold().FontColor(ColorGreen);
                        });

                        rightCol.Item().PaddingTop(2).Text(daneKonta.AdresBanku).FontSize(7).FontColor(ColorGray);
                    });
                });

                col.Item().PaddingTop(8).Background(ColorHighlight).Padding(6).BorderLeft(3).BorderColor(ColorGreen)
                    .Text($"⏱️ {T("Oferta ważna 7 dni od daty wystawienia. Ceny nie zawierają podatku VAT.", "Offer valid for 7 days from issue date. Prices do not include VAT tax.")}")
                   .FontSize(8).Italic().FontColor(ColorGray);
            });
        }

        private void DodajStopke(IContainer container)
        {
            container.AlignCenter().Text(text =>
            {
                text.DefaultTextStyle(x => x.FontSize(7).FontColor(ColorGray));
                text.Span(T("W razie pytań pozostajemy do dyspozycji. Strona ", "For any questions we remain at your disposal. Page "));
                text.CurrentPageNumber();
                text.Span(T(" z ", " of "));
                text.TotalPages();
            });
        }
    }
}