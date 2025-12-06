using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace Kalendarz1.OfertaCenowa
{
    public class OfertaPDFGenerator
    {
        private byte[]? _logoOkragle;
        private byte[]? _logoDlugie;
        private byte[]? _aktualneLogo;
        private TypLogo _aktualnyTypLogo;
        private readonly string _plikTlumaczen;
        private Dictionary<string, string> _tlumaczenia = new();

        // Kolory firmowe
        private static readonly string KolorZielony = "#4B833C";
        private static readonly string KolorZielonyJasny = "#E8F5E9";
        private static readonly string KolorZielonyCiemny = "#2E5A28";
        private static readonly string KolorCzerwony = "#CC2F37";
        private static readonly string KolorCzerwonyJasny = "#FEF2F2";
        private static readonly string KolorSzary = "#6B7280";
        private static readonly string KolorSzaryJasny = "#F9FAFB";
        private static readonly string KolorSzaryCiemny = "#374151";

        // Dane firmy
        private const string FIRMA_NAZWA = "Ubojnia Drobiu \"PIÓRKOWSCY\"";
        private const string FIRMA_PELNA = "Ubojnia Drobiu \"PIÓRKOWSCY\" Jerzy Piórkowski";
        private const string FIRMA_ADRES = "Koziołki 40";
        private const string FIRMA_MIASTO = "95-061 Dmosin";
        private const string FIRMA_NIP = "726-16-25-406";
        private const string FIRMA_REGON = "750045476";
        private const string FIRMA_TEL = "+48 46 874 71 70";
        private const string FIRMA_FAX = "+48 46 874 60 01";
        private const string FIRMA_EMAIL = "sekretariat@piorkowscy.com.pl";
        private const string FIRMA_WWW = "www.piorkowscy.com.pl";
        private const string FIRMA_OPIS_PL = "Rodzinna firma z ponad 28-letnią tradycją w branży drobiarskiej. Specjalizujemy się w uboju i przetwórstwie mięsa drobiowego najwyższej jakości.";
        private const string FIRMA_OPIS_EN = "Family company with over 28 years of tradition in the poultry industry. We specialize in slaughter and processing of highest quality poultry meat.";

        // Konta bankowe
        private const string KONTO_PLN = "60 1240 3060 1111 0010 4888 9213";
        private const string KONTO_EUR = "70 1240 3060 1978 0010 4888 9721";

        public OfertaPDFGenerator()
        {
            QuestPDF.Settings.License = LicenseType.Community;

            // Wczytaj oba logo z Embedded Resources
            _logoOkragle = WczytajLogoZZasobow("logo.png");
            _logoDlugie = WczytajLogoZZasobow("logo2white");

            _plikTlumaczen = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "OfertaHandlowa", "tlumaczenia.json");

            WczytajTlumaczenia();
        }

        private byte[]? WczytajLogoZZasobow(string nazwaLogo)
        {
            try
            {
                // Sprawdź różne assembly (główne i wykonywane)
                var assemblies = new[] 
                { 
                    Assembly.GetExecutingAssembly(),
                    Assembly.GetEntryAssembly(),
                    Assembly.GetCallingAssembly()
                }.Where(a => a != null).Distinct().ToList();

                foreach (var assembly in assemblies)
                {
                    if (assembly == null) continue;
                    
                    var allResources = assembly.GetManifestResourceNames();
                    
                    // Szukaj zasobu zawierającego podaną nazwę
                    string? resourceName = allResources
                        .FirstOrDefault(name => name.ToLower().Contains(nazwaLogo.ToLower().Replace(".png", "")));

                    if (resourceName != null)
                    {
                        using var stream = assembly.GetManifestResourceStream(resourceName);
                        if (stream != null)
                        {
                            using var ms = new MemoryStream();
                            stream.CopyTo(ms);
                            var bytes = ms.ToArray();
                            if (bytes.Length > 0)
                            {
                                return bytes;
                            }
                        }
                    }
                }

                // Fallback: próbuj wczytać z folderu aplikacji
                var exeAssembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                string appFolder = Path.GetDirectoryName(exeAssembly.Location) ?? AppDomain.CurrentDomain.BaseDirectory;
                
                var possiblePaths = new[]
                {
                    Path.Combine(appFolder, nazwaLogo),
                    Path.Combine(appFolder, nazwaLogo + ".png"),
                    Path.Combine(appFolder, "Resources", nazwaLogo),
                    Path.Combine(appFolder, "Resources", nazwaLogo + ".png")
                };

                foreach (var logoPath in possiblePaths)
                {
                    if (File.Exists(logoPath))
                    {
                        return File.ReadAllBytes(logoPath);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd wczytywania logo {nazwaLogo}: {ex.Message}");
            }

            return null;
        }

        private void WczytajTlumaczenia()
        {
            try
            {
                if (File.Exists(_plikTlumaczen))
                {
                    string json = File.ReadAllText(_plikTlumaczen);
                    var slownik = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (slownik != null)
                        _tlumaczenia = slownik;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd wczytywania tłumaczeń: {ex.Message}");
            }
        }

        private string PobierzNazweProduktu(TowarOferta produkt, JezykOferty jezyk)
        {
            if (jezyk == JezykOferty.English && _tlumaczenia.TryGetValue(produkt.Kod, out var nazwaEN))
            {
                if (!string.IsNullOrEmpty(nazwaEN))
                    return nazwaEN;
            }
            return produkt.Nazwa;
        }

        public void GenerujPDF(string sciezka, KlientOferta klient, List<TowarOferta> produkty,
            string notatki, string transport, ParametryOferty parametry)
        {
            bool czyAngielski = parametry.Jezyk == JezykOferty.English;
            var txt = PobierzTeksty(czyAngielski);

            // Wybierz odpowiednie logo na podstawie typu
            _aktualnyTypLogo = parametry.TypLogo;
            _aktualneLogo = parametry.TypLogo == TypLogo.Dlugie ? _logoDlugie : _logoOkragle;

            string transportTekst = txt["transportWlasny"];
            string numerOferty = $"OF/{DateTime.Now:yyyyMMdd}/{DateTime.Now:HHmm}";
            DateTime dataWaznosci = DateTime.Now.AddDays(parametry.DniWaznosci);
            bool maUwagi = !string.IsNullOrWhiteSpace(notatki);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(0);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Segoe UI"));

                    page.Header().Element(c => GenerujNaglowek(c, parametry, txt, numerOferty, czyAngielski));
                    page.Content().Element(c => GenerujTresc(c, klient, produkty, notatki, transportTekst, parametry, txt, maUwagi));
                    page.Footer().Element(c => GenerujStopke(c, parametry, txt, dataWaznosci));
                });
            }).GeneratePdf(sciezka);
        }

        private Dictionary<string, string> PobierzTeksty(bool czyAngielski)
        {
            return new Dictionary<string, string>
            {
                ["tytul"] = czyAngielski ? "COMMERCIAL OFFER" : "OFERTA HANDLOWA",
                ["numer"] = czyAngielski ? "Offer No." : "Nr oferty",
                ["data"] = czyAngielski ? "Issue date" : "Data wystawienia",
                ["wazna"] = czyAngielski ? "Valid until" : "Oferta ważna do",
                ["odbiorca"] = czyAngielski ? "RECIPIENT" : "ODBIORCA",
                ["dostawca"] = czyAngielski ? "SUPPLIER" : "DOSTAWCA",
                ["nip"] = "NIP",
                ["regon"] = "REGON",
                ["adres"] = czyAngielski ? "Address" : "Adres",
                ["produkty"] = czyAngielski ? "OFFERED PRODUCTS" : "OFEROWANE PRODUKTY",
                ["lp"] = czyAngielski ? "No." : "Lp.",
                ["nazwa"] = czyAngielski ? "Product name" : "Nazwa produktu",
                ["opakowanie"] = czyAngielski ? "Pack." : "Opak.",
                ["ilosc"] = czyAngielski ? "Qty (kg)" : "Ilość (kg)",
                ["cena"] = czyAngielski ? "Price" : "Cena",
                ["wartosc"] = czyAngielski ? "Value" : "Wartość",
                ["suma"] = czyAngielski ? "TOTAL VALUE" : "WARTOŚĆ CAŁKOWITA",
                ["warunki"] = czyAngielski ? "TERMS AND CONDITIONS" : "WARUNKI HANDLOWE",
                ["terminPlatnosci"] = czyAngielski ? "Payment terms" : "Termin płatności",
                ["transport"] = czyAngielski ? "Delivery" : "Transport",
                ["transportWlasny"] = czyAngielski ? "Seller's transport (included in price)" : "Transport własny (w cenie)",
                ["konto"] = czyAngielski ? "Bank account" : "Rachunek bankowy",
                ["uwagi"] = czyAngielski ? "ADDITIONAL NOTES" : "UWAGI DODATKOWE",
                ["wystawil"] = czyAngielski ? "Prepared by" : "Ofertę sporządził",
                ["cenyNetto"] = czyAngielski ? "* All prices are net (excluding VAT)" : "* Wszystkie ceny są cenami netto (bez VAT)",
                ["dziekujemy"] = czyAngielski ? "Thank you for your interest in our products!" : "Dziękujemy za zainteresowanie naszymi produktami!",
                ["kg"] = "kg",
                ["zl"] = czyAngielski ? "PLN" : "zł",
                ["pozycji"] = czyAngielski ? "items" : "pozycji",
                ["zapraszamy"] = czyAngielski ? "We invite you to cooperate!" : "Zapraszamy do współpracy!",
                ["firmaOpis"] = czyAngielski ? FIRMA_OPIS_EN : FIRMA_OPIS_PL,
                ["tel"] = czyAngielski ? "Phone" : "Tel",
                ["fax"] = "Fax"
            };
        }

        private void GenerujNaglowek(IContainer container, ParametryOferty parametry,
            Dictionary<string, string> txt, string numerOferty, bool czyAngielski)
        {
            container.Column(col =>
            {
                // Górny pasek z logo i danymi firmy
                col.Item().Background(Color.FromHex(KolorZielony)).Padding(15).Row(row =>
                {
                    if (_aktualnyTypLogo == TypLogo.Dlugie && _aktualneLogo != null && _aktualneLogo.Length > 0)
                    {
                        // DŁUGIE LOGO - logo2white.png, 280px, wyrównane w lewo
                        row.RelativeItem().Column(logoCol =>
                        {
                            // Logo wyrównane w lewo
                            logoCol.Item().AlignLeft().Width(280).Image(_aktualneLogo);
                            
                            // Formułka pod logo - ta sama szerokość co logo
                            logoCol.Item().PaddingTop(8).Width(280).Text(txt["firmaOpis"])
                                .FontSize(9).FontColor(Colors.White).Light();
                        });

                        // Dane kontaktowe po prawej
                        row.ConstantItem(160).AlignRight().AlignMiddle().Column(kontaktCol =>
                        {
                            kontaktCol.Item().Text($"Tel: {FIRMA_TEL}").FontSize(9).FontColor(Colors.White);
                            kontaktCol.Item().Text($"Fax: {FIRMA_FAX}").FontSize(9).FontColor(Colors.White);
                            kontaktCol.Item().Text(FIRMA_EMAIL).FontSize(9).FontColor(Colors.White);
                            kontaktCol.Item().Text(FIRMA_WWW).FontSize(9).FontColor(Colors.White);
                            kontaktCol.Item().PaddingTop(5).Text($"NIP: {FIRMA_NIP}").FontSize(8).FontColor(Colors.White).Light();
                        });
                    }
                    else
                    {
                        // OKRĄGŁE LOGO - logo.png + nazwa firmy i opis
                        row.ConstantItem(90).Column(logoCol =>
                        {
                            if (_aktualneLogo != null && _aktualneLogo.Length > 0)
                            {
                                logoCol.Item().Width(80).Image(_aktualneLogo);
                            }
                            else
                            {
                                logoCol.Item().Width(70).Height(70).Background(Colors.White)
                                    .AlignCenter().AlignMiddle()
                                    .Text("LOGO").FontSize(12).Bold().FontColor(Color.FromHex(KolorZielony));
                            }
                        });

                        // Nazwa firmy i opis
                        row.RelativeItem().PaddingLeft(15).Column(firmaCol =>
                        {
                            firmaCol.Item().Text(FIRMA_NAZWA).FontSize(18).Bold().FontColor(Colors.White);
                            firmaCol.Item().PaddingTop(3).Text(txt["firmaOpis"]).FontSize(9).FontColor(Colors.White).Light();
                            firmaCol.Item().PaddingTop(8).Text($"{FIRMA_ADRES}, {FIRMA_MIASTO}").FontSize(9).FontColor(Colors.White);
                        });

                        // Dane kontaktowe po prawej
                        row.ConstantItem(160).AlignRight().Column(kontaktCol =>
                        {
                            kontaktCol.Item().Text($"Tel: {FIRMA_TEL}").FontSize(9).FontColor(Colors.White);
                            kontaktCol.Item().Text($"Fax: {FIRMA_FAX}").FontSize(9).FontColor(Colors.White);
                            kontaktCol.Item().Text(FIRMA_EMAIL).FontSize(9).FontColor(Colors.White);
                            kontaktCol.Item().Text(FIRMA_WWW).FontSize(9).FontColor(Colors.White);
                            kontaktCol.Item().PaddingTop(5).Text($"NIP: {FIRMA_NIP}").FontSize(8).FontColor(Colors.White).Light();
                        });
                    }
                });

                // Czerwona linia akcentowa
                col.Item().Height(3).Background(Color.FromHex(KolorCzerwony));

                // Pasek z tytułem oferty
                col.Item().Background(Color.FromHex(KolorZielonyCiemny)).PaddingVertical(10).PaddingHorizontal(20).Row(row =>
                {
                    row.RelativeItem().Column(tytulCol =>
                    {
                        tytulCol.Item().Text(txt["tytul"]).FontSize(18).Bold().FontColor(Colors.White);
                    });

                    row.ConstantItem(220).AlignRight().Column(daneCol =>
                    {
                        daneCol.Item().Row(r =>
                        {
                            r.AutoItem().Text($"{txt["numer"]}: ").FontSize(10).FontColor(Colors.White).Light();
                            r.AutoItem().Text(numerOferty).FontSize(10).Bold().FontColor(Colors.White);
                        });
                        daneCol.Item().Row(r =>
                        {
                            r.AutoItem().Text($"{txt["data"]}: ").FontSize(10).FontColor(Colors.White).Light();
                            r.AutoItem().Text(DateTime.Now.ToString("dd.MM.yyyy")).FontSize(10).FontColor(Colors.White);
                        });
                    });
                });
            });
        }

        private void GenerujTresc(IContainer container, KlientOferta klient, List<TowarOferta> produkty,
            string notatki, string transport, ParametryOferty parametry, Dictionary<string, string> txt, bool maUwagi)
        {
            container.PaddingHorizontal(25).PaddingVertical(15).Column(col =>
            {
                // Sekcja odbiorcy i dostawcy obok siebie (lub tylko dostawca jeśli bez odbiorcy)
                col.Item().Row(row =>
                {
                    // ODBIORCA - lewa strona (tylko jeśli nie jest to oferta bez odbiorcy)
                    if (!parametry.BezOdbiorcy)
                    {
                        row.RelativeItem().Border(1).BorderColor(Color.FromHex("#E5E7EB")).Column(odbCol =>
                        {
                            // Nagłówek z czerwonym akcentem
                            odbCol.Item().Row(headerRow =>
                            {
                                headerRow.ConstantItem(4).Background(Color.FromHex(KolorCzerwony));
                                headerRow.RelativeItem().Background(Color.FromHex(KolorZielonyJasny)).Padding(8)
                                    .Text(txt["odbiorca"]).FontSize(10).Bold().FontColor(Color.FromHex(KolorZielonyCiemny));
                            });

                            odbCol.Item().Padding(10).Column(daneCol =>
                            {
                                daneCol.Item().Text(klient.Nazwa).FontSize(11).Bold().FontColor(Color.FromHex(KolorSzaryCiemny));

                                if (!string.IsNullOrEmpty(klient.NIP))
                                    daneCol.Item().PaddingTop(3).Text($"{txt["nip"]}: {klient.NIP}")
                                        .FontSize(9).FontColor(Color.FromHex(KolorSzary));

                                string adres = $"{klient.Adres}".Trim();
                                string miejscowosc = $"{klient.KodPocztowy} {klient.Miejscowosc}".Trim();

                                if (!string.IsNullOrEmpty(adres))
                                    daneCol.Item().PaddingTop(2).Text(adres).FontSize(9).FontColor(Color.FromHex(KolorSzary));
                                if (!string.IsNullOrEmpty(miejscowosc))
                                    daneCol.Item().Text(miejscowosc).FontSize(9).FontColor(Color.FromHex(KolorSzary));
                            });
                        });

                        row.ConstantItem(15);
                    }

                    // DOSTAWCA - prawa strona (lub pełna szerokość jeśli bez odbiorcy)
                    row.RelativeItem().Border(1).BorderColor(Color.FromHex("#E5E7EB")).Column(dostCol =>
                    {
                        // Nagłówek z czerwonym akcentem
                        dostCol.Item().Row(headerRow =>
                        {
                            headerRow.ConstantItem(4).Background(Color.FromHex(KolorCzerwony));
                            headerRow.RelativeItem().Background(Color.FromHex(KolorZielonyJasny)).Padding(8)
                                .Text(txt["dostawca"]).FontSize(10).Bold().FontColor(Color.FromHex(KolorZielonyCiemny));
                        });

                        dostCol.Item().Padding(10).Column(daneCol =>
                        {
                            daneCol.Item().Text(FIRMA_NAZWA).FontSize(11).Bold().FontColor(Color.FromHex(KolorSzaryCiemny));
                            daneCol.Item().PaddingTop(3).Text($"{txt["nip"]}: {FIRMA_NIP}").FontSize(9).FontColor(Color.FromHex(KolorSzary));
                            daneCol.Item().Text($"{FIRMA_ADRES}, {FIRMA_MIASTO}").FontSize(9).FontColor(Color.FromHex(KolorSzary));
                            
                            // Kto sporządził - wyróżnione z większą spacją
                            daneCol.Item().PaddingTop(12).Column(sporCol =>
                            {
                                sporCol.Item().Row(r =>
                                {
                                    r.AutoItem().Text($"{txt["wystawil"]}:  ").FontSize(9).FontColor(Color.FromHex(KolorSzary));
                                    r.AutoItem().Text(parametry.WystawiajacyNazwa).FontSize(9).Bold().FontColor(Color.FromHex(KolorCzerwony));
                                });

                                // Email sporządzającego
                                if (!string.IsNullOrEmpty(parametry.WystawiajacyEmail))
                                {
                                    sporCol.Item().PaddingTop(2).Text(parametry.WystawiajacyEmail)
                                        .FontSize(8).FontColor(Color.FromHex(KolorZielony));
                                }

                                // Telefon sporządzającego
                                if (!string.IsNullOrEmpty(parametry.WystawiajacyTelefon))
                                {
                                    sporCol.Item().PaddingTop(1).Text(parametry.WystawiajacyTelefon)
                                        .FontSize(8).FontColor(Color.FromHex(KolorSzary));
                                }
                            });
                        });
                    });
                });

                col.Item().PaddingTop(15);

                // Nagłówek tabeli produktów z czerwonym akcentem
                col.Item().Row(headerRow =>
                {
                    headerRow.ConstantItem(4).Background(Color.FromHex(KolorCzerwony));
                    headerRow.RelativeItem().Background(Color.FromHex(KolorZielony)).PaddingVertical(8).PaddingHorizontal(10).Row(prodRow =>
                    {
                        prodRow.RelativeItem().Text(txt["produkty"]).FontSize(11).Bold().FontColor(Colors.White);
                        prodRow.ConstantItem(80).AlignRight().Text($"{produkty.Count} {txt["pozycji"]}")
                            .FontSize(9).FontColor(Colors.White).Light();
                    });
                });

                // Tabela produktów
                col.Item().Border(1).BorderColor(Color.FromHex("#E5E7EB")).Element(e =>
                    GenerujTabeleProduktow(e, produkty, parametry, txt));

                col.Item().PaddingTop(12);

                // Warunki handlowe i uwagi
                if (maUwagi)
                {
                    // Dwie kolumny jeśli są uwagi
                    col.Item().Row(row =>
                    {
                        // Warunki handlowe - lewa
                        row.RelativeItem().Element(e => GenerujWarunkiHandlowe(e, parametry, transport, txt));

                        row.ConstantItem(12);

                        // Uwagi - prawa
                        row.ConstantItem(180).Border(1).BorderColor(Color.FromHex("#E5E7EB")).Column(uwagiCol =>
                        {
                            uwagiCol.Item().Row(headerRow =>
                            {
                                headerRow.ConstantItem(4).Background(Color.FromHex(KolorCzerwony));
                                headerRow.RelativeItem().Background(Color.FromHex(KolorCzerwonyJasny)).Padding(8)
                                    .Text(txt["uwagi"]).FontSize(10).Bold().FontColor(Color.FromHex(KolorCzerwony));
                            });

                            uwagiCol.Item().Padding(10).Text(notatki).FontSize(8).Italic().FontColor(Color.FromHex(KolorSzary));
                        });
                    });
                }
                else
                {
                    // Warunki wyśrodkowane na całą szerokość jeśli brak uwag
                    col.Item().AlignCenter().Width(350).Element(e => GenerujWarunkiHandlowe(e, parametry, transport, txt));
                }

                col.Item().PaddingTop(8);
                col.Item().Text(txt["cenyNetto"]).FontSize(7).Italic().FontColor(Color.FromHex(KolorSzary));
            });
        }

        private void GenerujWarunkiHandlowe(IContainer container, ParametryOferty parametry, string transport, Dictionary<string, string> txt)
        {
            container.Border(1).BorderColor(Color.FromHex("#E5E7EB")).Column(warCol =>
            {
                warCol.Item().Row(headerRow =>
                {
                    headerRow.ConstantItem(4).Background(Color.FromHex(KolorCzerwony));
                    headerRow.RelativeItem().Background(Color.FromHex(KolorZielonyJasny)).Padding(8)
                        .Text(txt["warunki"]).FontSize(10).Bold().FontColor(Color.FromHex(KolorZielonyCiemny));
                });

                warCol.Item().Padding(12).Column(listaCol =>
                {
                    // Termin płatności - tabela z odstępem
                    if (parametry.PokazTerminPlatnosci)
                    {
                        listaCol.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(110);
                                c.RelativeColumn();
                            });
                            t.Cell().Text($"{txt["terminPlatnosci"]}:").FontSize(9).FontColor(Color.FromHex(KolorSzary));
                            t.Cell().Text(parametry.TerminPlatnosci).FontSize(9).Bold().FontColor(Color.FromHex(KolorSzaryCiemny));
                        });
                    }

                    // Transport - tabela z odstępem
                    listaCol.Item().PaddingTop(6).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(110);
                            c.RelativeColumn();
                        });
                        t.Cell().Text($"{txt["transport"]}:").FontSize(9).FontColor(Color.FromHex(KolorSzary));
                        t.Cell().Text(transport).FontSize(9).Bold().FontColor(Color.FromHex(KolorSzaryCiemny));
                    });

                    // Konto bankowe
                    listaCol.Item().PaddingTop(6).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(110);
                            c.RelativeColumn();
                        });
                        t.Cell().Text($"{txt["konto"]}:").FontSize(9).FontColor(Color.FromHex(KolorSzary));

                        string konto = parametry.WalutaKonta == "EUR" ? KONTO_EUR : KONTO_PLN;
                        t.Cell().Column(kCol =>
                        {
                            kCol.Item().Text($"{konto}").FontSize(8).FontColor(Color.FromHex(KolorSzaryCiemny));
                            kCol.Item().Text($"({parametry.WalutaKonta})").FontSize(8).FontColor(Color.FromHex(KolorSzary));
                        });
                    });
                });
            });
        }

        private void GenerujTabeleProduktow(IContainer container, List<TowarOferta> produkty,
            ParametryOferty parametry, Dictionary<string, string> txt)
        {
            bool pokazIlosc = parametry.PokazIlosc;
            bool pokazCene = parametry.PokazCene;
            bool pokazOpakowanie = parametry.PokazOpakowanie;
            bool pokazWartosc = pokazIlosc && pokazCene;

            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(25);
                    columns.RelativeColumn();
                    if (pokazOpakowanie) columns.ConstantColumn(50);
                    if (pokazIlosc) columns.ConstantColumn(55);
                    if (pokazCene) columns.ConstantColumn(60);
                    if (pokazWartosc) columns.ConstantColumn(75);
                });

                // Nagłówek tabeli
                table.Header(header =>
                {
                    var headerStyle = TextStyle.Default.FontSize(8).Bold().FontColor(Colors.White);
                    var headerBg = Color.FromHex(KolorSzaryCiemny);

                    header.Cell().Background(headerBg).PaddingVertical(5).PaddingHorizontal(4).AlignCenter()
                        .Text(txt["lp"]).Style(headerStyle);
                    header.Cell().Background(headerBg).PaddingVertical(5).PaddingHorizontal(4)
                        .Text(txt["nazwa"]).Style(headerStyle);
                    if (pokazOpakowanie)
                        header.Cell().Background(headerBg).PaddingVertical(5).PaddingHorizontal(4).AlignCenter()
                            .Text(txt["opakowanie"]).Style(headerStyle);
                    if (pokazIlosc)
                        header.Cell().Background(headerBg).PaddingVertical(5).PaddingHorizontal(4).AlignRight()
                            .Text(txt["ilosc"]).Style(headerStyle);
                    if (pokazCene)
                        header.Cell().Background(headerBg).PaddingVertical(5).PaddingHorizontal(4).AlignRight()
                            .Text(txt["cena"]).Style(headerStyle);
                    if (pokazWartosc)
                        header.Cell().Background(headerBg).PaddingVertical(5).PaddingHorizontal(4).AlignRight()
                            .Text(txt["wartosc"]).Style(headerStyle);
                });

                // Wiersze produktów
                int lp = 1;
                decimal suma = 0;

                foreach (var p in produkty)
                {
                    var bgColor = lp % 2 == 0 ? Color.FromHex(KolorSzaryJasny) : Colors.White;
                    decimal wartosc = p.Ilosc * p.CenaJednostkowa;
                    suma += wartosc;

                    string nazwaProduktu = PobierzNazweProduktu(p, parametry.Jezyk);

                    table.Cell().Background(bgColor).PaddingVertical(4).PaddingHorizontal(4).AlignCenter()
                        .Text(lp.ToString()).FontSize(8).FontColor(Color.FromHex(KolorSzary));

                    table.Cell().Background(bgColor).PaddingVertical(4).PaddingHorizontal(4)
                        .Text(nazwaProduktu).FontSize(8).FontColor(Color.FromHex(KolorSzaryCiemny));

                    if (pokazOpakowanie)
                        table.Cell().Background(bgColor).PaddingVertical(4).PaddingHorizontal(4).AlignCenter()
                            .Text(p.Opakowanie).FontSize(8).FontColor(Color.FromHex(KolorSzary));

                    if (pokazIlosc)
                        table.Cell().Background(bgColor).PaddingVertical(4).PaddingHorizontal(4).AlignRight()
                            .Text(p.Ilosc > 0 ? $"{p.Ilosc:N0}" : "—").FontSize(8);

                    if (pokazCene)
                        table.Cell().Background(bgColor).PaddingVertical(4).PaddingHorizontal(4).AlignRight()
                            .Text(p.CenaJednostkowa > 0 ? $"{p.CenaJednostkowa:N2}" : "—").FontSize(8);

                    if (pokazWartosc)
                        table.Cell().Background(bgColor).PaddingVertical(4).PaddingHorizontal(4).AlignRight()
                            .Text(wartosc > 0 ? $"{wartosc:N2} {txt["zl"]}" : "—")
                            .FontSize(8).Bold().FontColor(Color.FromHex(KolorZielonyCiemny));

                    lp++;
                }

                // Wiersz sumy
                if (pokazWartosc && suma > 0)
                {
                    int colspan = 1;
                    if (pokazOpakowanie) colspan++;
                    if (pokazIlosc) colspan++;
                    if (pokazCene) colspan++;

                    table.Cell().ColumnSpan((uint)colspan).Background(Color.FromHex(KolorZielony)).PaddingVertical(6).PaddingHorizontal(8).AlignRight()
                        .Text(txt["suma"]).FontSize(10).Bold().FontColor(Colors.White);

                    table.Cell().Background(Color.FromHex(KolorZielony)).PaddingVertical(6).PaddingHorizontal(8).AlignRight()
                        .Text($"{suma:N2} {txt["zl"]}").FontSize(11).Bold().FontColor(Colors.White);
                }
            });
        }

        private void GenerujStopke(IContainer container, ParametryOferty parametry, Dictionary<string, string> txt, DateTime dataWaznosci)
        {
            container.Column(col =>
            {
                // Podziękowanie
                col.Item().PaddingHorizontal(25).PaddingBottom(8).Row(row =>
                {
                    row.RelativeItem().Column(dziekCol =>
                    {
                        dziekCol.Item().Text(txt["dziekujemy"]).FontSize(9).Italic()
                            .FontColor(Color.FromHex(KolorZielony));
                        dziekCol.Item().PaddingTop(2).Text(txt["zapraszamy"]).FontSize(10).Bold()
                            .FontColor(Color.FromHex(KolorZielonyCiemny));
                    });
                });

                // Dolny pasek z danymi firmy
                col.Item().Background(Color.FromHex(KolorZielonyCiemny)).PaddingVertical(10).PaddingHorizontal(25).Row(row =>
                {
                    row.RelativeItem().Column(firmaCol =>
                    {
                        firmaCol.Item().Text(FIRMA_PELNA).FontSize(8).Bold().FontColor(Colors.White);
                        firmaCol.Item().Text($"{FIRMA_ADRES}, {FIRMA_MIASTO} | NIP: {FIRMA_NIP} | REGON: {FIRMA_REGON}")
                            .FontSize(7).FontColor(Colors.White).Light();
                    });

                    row.ConstantItem(170).AlignRight().Column(kontaktCol =>
                    {
                        kontaktCol.Item().Text($"Tel: {FIRMA_TEL} | {FIRMA_EMAIL}").FontSize(7).FontColor(Colors.White);
                        kontaktCol.Item().Text(FIRMA_WWW).FontSize(7).FontColor(Colors.White).Light();
                    });
                });

                // Data ważności w czerwonym pasku
                col.Item().Background(Color.FromHex(KolorCzerwony)).PaddingVertical(4).PaddingHorizontal(25).Row(row =>
                {
                    row.RelativeItem().AlignCenter()
                        .Text($"{txt["wazna"]}: {dataWaznosci:dd.MM.yyyy}")
                        .FontSize(8).FontColor(Colors.White);
                });
            });
        }
    }
}
