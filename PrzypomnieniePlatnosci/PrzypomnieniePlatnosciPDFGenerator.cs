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
    public class PrzypomnieniePlatnosciPDFGenerator
    {
        private DaneKontrahenta _daneKontrahenta;
        private List<DokumentPlatnosci> _dokumenty;
        private bool _czyDodacOdsetki;
        private WersjaPrzypomnienia _wersja;
        private int _liczbaDni;

        private readonly string ColorGreen = "#4B833C";
        private readonly string ColorBlue = "#2563EB";
        private readonly string ColorText = "#374151";
        private readonly string ColorGray = "#9CA3AF";
        private readonly string ColorBorder = "#E5E7EB";
        private readonly string ColorBackground = "#F9FAFB";
        private readonly string ColorHighlight = "#EFF6FF";
        private readonly string ColorInfo = "#DBEAFE";
        private readonly string ColorRed = "#1E40AF"; // Zmiana z czerwonego na ciemnoniebieski
        private readonly string ColorOrange = "#F59E0B";
        private readonly string ColorAccent = "#3B82F6"; // Nowy kolor akcentu

        public void GenerujPDF(string sciezka, DaneKontrahenta daneKontrahenta, List<DokumentPlatnosci> dokumenty,
            bool czyDodacOdsetki, WersjaPrzypomnienia wersja, int liczbaDni)
        {
            _daneKontrahenta = daneKontrahenta;
            _dokumenty = dokumenty;
            _czyDodacOdsetki = czyDodacOdsetki;
            _wersja = wersja;
            _liczbaDni = liczbaDni;

            QuestPDF.Settings.License = LicenseType.Community;

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Calibri).FontColor(ColorText));

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

                string separatorColor = _wersja switch
                {
                    WersjaPrzypomnienia.Przedsadowa => ColorAccent,
                    WersjaPrzypomnienia.Mocna => ColorOrange,
                    _ => ColorBlue
                };

                column.Item().PaddingTop(6).Row(row =>
                {
                    row.RelativeItem().Height(2).Background(ColorGreen);
                    row.ConstantItem(60).Height(2).Background(separatorColor);
                });
            });
        }

        private void DodajTresc(IContainer container)
        {
            container.PaddingVertical(10).Column(column =>
            {
                // Miejsce i data + adresat dla wersji przedsądowej
                if (_wersja == WersjaPrzypomnienia.Przedsadowa)
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"Dmosin, {DateTime.Now:dd MMMM yyyy} r.")
                            .FontSize(9).FontColor(ColorGray);
                    });

                    column.Item().PaddingTop(8).Column(col =>
                    {
                        col.Item().Text(_daneKontrahenta.PelnaNazwa).Bold().FontSize(10);
                        if (!string.IsNullOrEmpty(_daneKontrahenta.Adres))
                            col.Item().Text(_daneKontrahenta.Adres).FontSize(9);
                        col.Item().Text($"{_daneKontrahenta.KodPocztowy} {_daneKontrahenta.Miejscowosc}").FontSize(9);
                    });
                }

                // Tytuł
                column.Item().PaddingTop(_wersja == WersjaPrzypomnienia.Przedsadowa ? 12 : 0).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        string tytul = _wersja switch
                        {
                            WersjaPrzypomnienia.Przedsadowa => "📋 Wezwanie do zapłaty",
                            WersjaPrzypomnienia.Mocna => "⚠️ Pilne przypomnienie o płatności",
                            _ => "💌 Przypomnienie o płatności"
                        };

                        string kolor = _wersja switch
                        {
                            WersjaPrzypomnienia.Przedsadowa => ColorAccent,
                            WersjaPrzypomnienia.Mocna => ColorOrange,
                            _ => ColorBlue
                        };

                        int rozmiar = _wersja == WersjaPrzypomnienia.Przedsadowa ? 15 :
                                     _wersja == WersjaPrzypomnienia.Mocna ? 16 : 16;

                        col.Item().Text(tytul).FontSize(rozmiar).Bold().FontColor(kolor);

                        if (_wersja != WersjaPrzypomnienia.Przedsadowa)
                        {
                            col.Item().PaddingTop(2).Text($"Dokument: PR/{DateTime.Now:yyyy/MM/dd/HHmm}")
                                .FontSize(8).FontColor(ColorGray);
                        }
                    });

                    if (_wersja != WersjaPrzypomnienia.Przedsadowa)
                    {
                        row.ConstantItem(140).AlignRight().Column(col =>
                        {
                            col.Item().Text("Data wystawienia:").FontSize(7).FontColor(ColorGray);
                            col.Item().PaddingTop(2).Text(DateTime.Now.ToString("dd MMMM yyyy", CultureInfo.GetCultureInfo("pl-PL")) + " r.")
                                .Bold().FontSize(9).FontColor(ColorGreen);
                        });
                    }
                });

                // WERSJA PRZEDSĄDOWA - treść formalna na początku
                if (_wersja == WersjaPrzypomnienia.Przedsadowa)
                {
                    var przeterminowane = _dokumenty.Where(d => d.DniPoTerminie > 0).ToList();
                    decimal sumaPrzeterminowana = przeterminowane.Sum(d => d.PozostaloDoZaplaty);
                    decimal odsetkiPrzeterminowane = przeterminowane.Sum(d => d.Odsetki);

                    string kwotaSlownie = LiczbyNaSlowa.NaSlowa((int)sumaPrzeterminowana);
                    string grosze = ((int)((sumaPrzeterminowana - (int)sumaPrzeterminowana) * 100)).ToString("00");

                    column.Item().PaddingTop(12).Border(3).BorderColor(ColorRed).Background("#FFE5E5").Padding(15).Column(col =>
                    {
                        col.Item().Text(text =>
                        {
                            text.Span("Wzywam Państwa do zapłaty kwoty ").FontSize(10);
                            text.Span($"{sumaPrzeterminowana:N2} zł").Bold().FontSize(12).FontColor(ColorRed);
                            text.Span($" ({kwotaSlownie} złotych {grosze}/100)").FontSize(10);

                            if (_czyDodacOdsetki && odsetkiPrzeterminowane > 0)
                            {
                                text.Span(" wraz z odsetkami ustawowymi od transakcji handlowych").FontSize(10);
                            }

                            text.Span($" w terminie ").FontSize(10);
                            text.Span($"{_liczbaDni} {OdmianaDni(_liczbaDni)}").Bold().FontSize(11).FontColor(ColorRed);
                            text.Span(" od dnia otrzymania niniejszego wezwania.").FontSize(10);
                        });

                        col.Item().PaddingTop(10).Text("Wpłaty proszę dokonać na rachunek bankowy:")
                            .FontSize(9).Bold();
                        col.Item().PaddingTop(3).Text("Ubojnia Drobiu \"Piórkowscy\" Jerzy Piórkowski w spadku")
                            .FontSize(9);
                        col.Item().Text("95-061 Dmosin, Koziołki 40").FontSize(9);
                        col.Item().PaddingTop(3).Text("60 1240 3060 1111 0010 4888 9213")
                            .Bold().FontSize(11).FontColor(ColorGreen);

                        col.Item().PaddingTop(8).Background("#FFFFFF").Padding(8).Text(
                            "⚠️ Brak zapłaty w terminie będzie podstawą do wystąpienia na drogę sądową " +
                            "i doliczenia kosztów windykacji, jak również naprawienia szkody związanej " +
                            "z niewykonaniem zobowiązań. Informujemy o możliwym wpisie do Krajowego Rejestru Długów.")
                            .FontSize(8).Bold().FontColor(ColorRed);
                    });
                }

                // Informacje o przeterminowanych - dla wersji łagodnej i mocnej
                var przeterminowaneDoc = _dokumenty.Where(d => d.DniPoTerminie > 0).ToList();
                if (przeterminowaneDoc.Any() && _wersja != WersjaPrzypomnienia.Przedsadowa)
                {
                    decimal sumaPrzeterminowana = przeterminowaneDoc.Sum(d => d.PozostaloDoZaplaty);
                    decimal odsetkiPrzeterminowane = przeterminowaneDoc.Sum(d => d.Odsetki);
                    int liczbaDokumentow = przeterminowaneDoc.Count;

                    if (_wersja == WersjaPrzypomnienia.Lagodna)
                    {
                        column.Item().PaddingTop(12).Border(1).BorderColor(ColorBlue).Background(ColorInfo).Padding(12).Column(col =>
                        {
                            col.Item().Text("ℹ️ Informacja o zaległych płatnościach").Bold().FontSize(12).FontColor(ColorBlue);
                            col.Item().PaddingTop(6).Row(r =>
                            {
                                r.RelativeItem().Column(c =>
                                {
                                    c.Item().Text("Kwota do uregulowania:").FontSize(9).FontColor(ColorGray);
                                    c.Item().PaddingTop(2).Text($"{sumaPrzeterminowana:N2} zł").Bold().FontSize(14).FontColor(ColorBlue);
                                });
                                r.RelativeItem().Column(c =>
                                {
                                    c.Item().Text("Liczba dokumentów:").FontSize(9).FontColor(ColorGray);
                                    c.Item().PaddingTop(2).Text($"{liczbaDokumentow} szt.").Bold().FontSize(12).FontColor(ColorBlue);
                                });
                                if (_czyDodacOdsetki && odsetkiPrzeterminowane > 0)
                                {
                                    r.RelativeItem().Column(c =>
                                    {
                                        c.Item().Text("Odsetki:").FontSize(9).FontColor(ColorGray);
                                        c.Item().PaddingTop(2).Text($"{odsetkiPrzeterminowane:N2} zł").Bold().FontSize(12).FontColor(ColorBlue);
                                    });
                                }
                            });
                        });
                    }
                    else if (_wersja == WersjaPrzypomnienia.Mocna)
                    {
                        column.Item().PaddingTop(12).Border(3).BorderColor(ColorOrange).Background("#FFF3E0").Padding(15).Column(col =>
                        {
                            col.Item().Text("🚨 PILNE - FAKTURY PRZETERMINOWANE").Bold().FontSize(14).FontColor(ColorOrange);
                            col.Item().PaddingTop(8).Row(r =>
                            {
                                r.RelativeItem().Column(c =>
                                {
                                    c.Item().Text("Kwota do pilnej zapłaty:").FontSize(9).FontColor(ColorGray);
                                    c.Item().PaddingTop(3).Text($"{sumaPrzeterminowana:N2} zł").Bold().FontSize(18).FontColor(ColorOrange);
                                });
                                r.RelativeItem().Column(c =>
                                {
                                    c.Item().Text("Liczba dokumentów:").FontSize(9).FontColor(ColorGray);
                                    c.Item().PaddingTop(3).Text($"{liczbaDokumentow} szt.").Bold().FontSize(14).FontColor(ColorOrange);
                                });
                                if (_czyDodacOdsetki && odsetkiPrzeterminowane > 0)
                                {
                                    r.RelativeItem().Column(c =>
                                    {
                                        c.Item().Text("Naliczone odsetki:").FontSize(9).FontColor(ColorGray);
                                        c.Item().PaddingTop(3).Text($"{odsetkiPrzeterminowane:N2} zł").Bold().FontSize(14).FontColor(ColorOrange);
                                    });
                                }
                            });
                            col.Item().PaddingTop(8).Background("#FFFFFF").Padding(8).Text(
                                $"⚠️ Prosimy o niezwłoczną płatność kwoty przeterminowanej w ciągu {_liczbaDni} {OdmianaDni(_liczbaDni)} roboczych. " +
                                "Brak reakcji skutkuje zastosowaniem dalszych kroków windykacyjnych.")
                                .FontSize(9).Bold().FontColor(ColorOrange);
                        });
                    }
                }

                // Informacje wprowadzające
                if (_wersja != WersjaPrzypomnienia.Przedsadowa)
                {
                    string borderColor = _wersja == WersjaPrzypomnienia.Mocna ? ColorOrange : ColorGreen;
                    string bgColor = _wersja == WersjaPrzypomnienia.Mocna ? "#FFF3E0" : ColorHighlight;
                    string tekst = _wersja == WersjaPrzypomnienia.Mocna
                        ? $"Uprzejmie informujemy o zaległych płatnościach na poniższych dokumentach. Prosimy o pilną regulację należności w ciągu {_liczbaDni} {OdmianaDni(_liczbaDni)} roboczych."
                        : $"Uprzejmie przypominamy o płatnościach za poniższe dokumenty. Będziemy wdzięczni za uregulowanie należności w ciągu {_liczbaDni} {OdmianaDni(_liczbaDni)}.";

                    column.Item().PaddingTop(6).Border(_wersja == WersjaPrzypomnienia.Mocna ? 2 : 1)
                        .BorderColor(borderColor).Background(bgColor).Padding(8).Column(col =>
                        {
                            col.Item().Text("Szanowni Państwo,").FontSize(9).Bold();
                            col.Item().PaddingTop(2).Text(tekst).FontSize(8);
                        });
                }

                // Dane kontrahenta (tylko dla wersji łagodnej i mocnej)
                if (_wersja != WersjaPrzypomnienia.Przedsadowa)
                {
                    column.Item().PaddingTop(6).Border(1).BorderColor(ColorBorder).Background(ColorBackground).Padding(8).Column(col =>
                    {
                        col.Item().Text("🏢 Dane kontrahenta").Bold().FontSize(9).FontColor(ColorGreen);
                        col.Item().PaddingTop(2).Row(r =>
                        {
                            r.ConstantItem(60).Text("Nazwa:").FontSize(8).FontColor(ColorGray);
                            r.RelativeItem().Text(_daneKontrahenta.PelnaNazwa).Bold().FontSize(9);
                        });
                        col.Item().PaddingTop(2).Row(r =>
                        {
                            r.ConstantItem(60).Text("NIP:").FontSize(8).FontColor(ColorGray);
                            r.RelativeItem().Text(_daneKontrahenta.NIP).Bold().FontSize(8);
                        });
                        if (!string.IsNullOrEmpty(_daneKontrahenta.Adres))
                        {
                            col.Item().PaddingTop(2).Row(r =>
                            {
                                r.ConstantItem(60).Text("Adres:").FontSize(8).FontColor(ColorGray);
                                r.RelativeItem().Text($"{_daneKontrahenta.Adres}, {_daneKontrahenta.KodPocztowy} {_daneKontrahenta.Miejscowosc}").FontSize(8);
                            });
                        }
                        else
                        {
                            col.Item().PaddingTop(2).Row(r =>
                            {
                                r.ConstantItem(60).Text("Adres:").FontSize(8).FontColor(ColorGray);
                                r.RelativeItem().Text($"{_daneKontrahenta.KodPocztowy} {_daneKontrahenta.Miejscowosc}").FontSize(8);
                            });
                        }
                    });
                }

                // Tabela dokumentów
                column.Item().PaddingTop(6).Element(DodajTabeleDokumentow);

                // Podsumowanie
                column.Item().PaddingTop(5).Element(DodajPodsumowanie);

                // Dane do przelewu (tylko dla wersji łagodnej i mocnej)
                if (_wersja != WersjaPrzypomnienia.Przedsadowa)
                {
                    column.Item().PaddingTop(6).Element(DodajDaneDoPrzelewu);
                }

                // Ostrzeżenia
                if (_wersja == WersjaPrzypomnienia.Lagodna)
                {
                    column.Item().PaddingTop(6).Border(1).BorderColor(ColorBlue).Background(ColorInfo)
                        .Padding(6).Column(col =>
                        {
                            col.Item().Text("ℹ️ Informacje:").Bold().FontSize(9).FontColor(ColorBlue);
                            col.Item().PaddingTop(2).Text("• W razie problemów z płatnością prosimy o kontakt - chętnie ustalimy dogodny termin.").FontSize(8);
                            col.Item().PaddingTop(2).Text("• Dziękujemy za dotychczasową współpracę i liczymy na dalsze dobre relacje.").FontSize(8);
                        });
                }
                else if (_wersja == WersjaPrzypomnienia.Mocna)
                {
                    column.Item().PaddingTop(6).Border(2).BorderColor(ColorOrange).Background("#FFF3E0")
                        .Padding(6).Column(col =>
                        {
                            col.Item().Text("⚠️ WAŻNE INFORMACJE:").Bold().FontSize(9).FontColor(ColorOrange);
                            col.Item().PaddingTop(2).Text($"• Prosimy o pilną realizację zaległych płatności w ciągu {_liczbaDni} {OdmianaDni(_liczbaDni)} od daty niniejszego dokumentu.").FontSize(8);
                            col.Item().PaddingTop(2).Text("• Brak płatności może skutkować wstrzymaniem dostaw i naliczeniem odsetek ustawowych.").FontSize(8);
                            col.Item().PaddingTop(2).Text("• W razie problemów z płatnością prosimy o natychmiastowy kontakt.").FontSize(8);
                        });
                }
                else if (_wersja == WersjaPrzypomnienia.Przedsadowa)
                {
                    column.Item().PaddingTop(10).Column(col =>
                    {
                        col.Item().Text("Załączniki:").Bold().FontSize(9);
                        col.Item().PaddingTop(2).Text("1. Zestawienie faktur / dokumentów sprzedaży").FontSize(8);
                    });
                }

                // Kontakt
                if (_wersja != WersjaPrzypomnienia.Przedsadowa)
                {
                    column.Item().PaddingTop(5).Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("📞 W sprawie płatności prosimy o kontakt:").Bold().FontSize(9).FontColor(ColorGreen);
                            col.Item().PaddingTop(2).Text("Dział Księgowości: +48 46 874 71 70").FontSize(8);
                            col.Item().Text("Email: kasa@piorkowscy.com.pl").FontSize(8).FontColor(ColorGreen).Bold();
                            col.Item().Text("Dostępność: Pn-Pt 8:00-16:00").FontSize(7).FontColor(ColorGray);
                        });
                    });
                }
            });
        }

        private void DodajTabeleDokumentow(IContainer container)
        {
            string headerColor = _wersja switch
            {
                WersjaPrzypomnienia.Przedsadowa => ColorAccent,
                WersjaPrzypomnienia.Mocna => ColorOrange,
                _ => ColorGreen
            };

            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(25);
                    columns.RelativeColumn(2.2f);
                    columns.RelativeColumn(1.2f);
                    columns.RelativeColumn(1.2f);
                    columns.RelativeColumn(0.9f);
                    columns.RelativeColumn(1.3f);
                    columns.RelativeColumn(0.9f);
                    columns.RelativeColumn(1.3f);
                    columns.RelativeColumn(1.3f);
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(1f);

                    if (_czyDodacOdsetki)
                        columns.RelativeColumn(1.2f);
                });

                table.Header(header =>
                {
                    Action<string> naglowek = text =>
                    {
                        header.Cell().Background(headerColor).Padding(3).AlignCenter()
                            .Text(text).FontColor("#FFFFFF").Bold().FontSize(7);
                    };

                    naglowek("Lp.");
                    naglowek("Numer");
                    naglowek("Data dok.");
                    naglowek("Termin");
                    naglowek("Dni");
                    naglowek("Netto");
                    naglowek("VAT");
                    naglowek("Brutto");
                    naglowek("Zapłacono");
                    naglowek("Pozostało");
                    naglowek("Po term.");

                    if (_czyDodacOdsetki)
                        naglowek("Odsetki");
                });

                foreach (var dok in _dokumenty)
                {
                    var bgColor = dok.DniPoTerminie > 0 ? "#FFE5E5" : (dok.Lp % 2 == 0 ? ColorBackground : "#FFFFFF");

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder).Padding(3).AlignCenter()
                        .Text(dok.Lp.ToString()).FontSize(7);

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder).Padding(3)
                        .Text(dok.NumerDokumentu).FontSize(7).Bold();

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder).Padding(3).AlignCenter()
                        .Text(dok.DataDokumentu.ToString("dd.MM.yy")).FontSize(7);

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder).Padding(3).AlignCenter()
                        .Text(dok.TerminPlatnosci.ToString("dd.MM.yy")).FontSize(7).Bold()
                        .FontColor(dok.DniPoTerminie > 0 ? ColorRed : ColorText);

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder).Padding(3).AlignCenter()
                        .Text(dok.DniTerminu.ToString()).FontSize(7);

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder).Padding(3).AlignRight()
                        .Text($"{dok.WartoscNetto:N2}").FontSize(7);

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder).Padding(3).AlignRight()
                        .Text($"{dok.WartoscVAT:N2}").FontSize(7).FontColor(ColorGray);

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder).Padding(3).AlignRight()
                        .Text($"{dok.WartoscBrutto:N2}").FontSize(7);

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder).Padding(3).AlignRight()
                        .Text($"{dok.Zaplacono:N2}").FontSize(7).FontColor("#27AE60");

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder).Padding(3).AlignRight()
                        .Text($"{dok.PozostaloDoZaplaty:N2}").FontSize(7).Bold()
                        .FontColor(dok.DniPoTerminie > 0 ? ColorRed : ColorText);

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder).Padding(3).AlignCenter()
                        .Text(dok.DniPoTerminie > 0 ? $"{dok.DniPoTerminie}" : "—")
                        .FontSize(7).Bold().FontColor(ColorRed);

                    if (_czyDodacOdsetki)
                    {
                        table.Cell().Background(bgColor).BorderBottom(1).BorderColor(ColorBorder).Padding(3).AlignRight()
                            .Text(dok.Odsetki > 0 ? $"{dok.Odsetki:N2}" : "—")
                            .FontSize(7).Bold().FontColor(ColorRed);
                    }
                }
            });
        }

        private void DodajPodsumowanie(IContainer container)
        {
            decimal sumaWartoscBrutto = _dokumenty.Sum(d => d.WartoscBrutto);
            decimal sumaZaplacono = _dokumenty.Sum(d => d.Zaplacono);
            decimal sumaPozostalo = _dokumenty.Sum(d => d.PozostaloDoZaplaty);
            decimal sumaOdsetki = _dokumenty.Sum(d => d.Odsetki);

            string borderColor = _wersja switch
            {
                WersjaPrzypomnienia.Przedsadowa => ColorAccent,
                WersjaPrzypomnienia.Mocna => ColorOrange,
                _ => ColorBlue
            };

            string bgColor = _wersja switch
            {
                WersjaPrzypomnienia.Przedsadowa => "#EFF6FF",
                WersjaPrzypomnienia.Mocna => "#FFF3E0",
                _ => ColorInfo
            };

            string accentColor = _wersja switch
            {
                WersjaPrzypomnienia.Przedsadowa => ColorAccent,
                WersjaPrzypomnienia.Mocna => ColorOrange,
                _ => ColorBlue
            };

            container.Border(2).BorderColor(borderColor).Background(bgColor).Padding(6).Column(col =>
            {
                col.Item().Text("📊 Podsumowanie").Bold().FontSize(9).FontColor(accentColor);

                col.Item().PaddingTop(4).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Wartość brutto:").FontSize(8).FontColor(ColorGray);
                        c.Item().PaddingTop(2).Text($"{sumaWartoscBrutto:N2} zł").Bold().FontSize(10).FontColor(ColorGreen);
                    });

                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Zapłacono:").FontSize(8).FontColor(ColorGray);
                        c.Item().PaddingTop(2).Text($"{sumaZaplacono:N2} zł").Bold().FontSize(10).FontColor("#27AE60");
                    });

                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Pozostało do zapłaty:").FontSize(8).FontColor(ColorGray).Bold();
                        c.Item().PaddingTop(2).Text($"{sumaPozostalo:N2} zł").Bold().FontSize(13).FontColor(accentColor);
                    });

                    if (_czyDodacOdsetki && sumaOdsetki > 0)
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Odsetki (11,5%):").FontSize(8).FontColor(ColorGray);
                            c.Item().PaddingTop(2).Text($"{sumaOdsetki:N2} zł").Bold().FontSize(10).FontColor(accentColor);
                        });
                    }
                });

                if (_czyDodacOdsetki && sumaOdsetki > 0)
                {
                    col.Item().PaddingTop(5).Background("#FFFFFF").Padding(4).Text(
                        $"💡 Łączna kwota z odsetkami: {(sumaPozostalo + sumaOdsetki):N2} zł")
                        .FontSize(9).Bold().FontColor(accentColor);
                }
            });
        }

        private void DodajDaneDoPrzelewu(IContainer container)
        {
            container.Border(1).BorderColor(ColorGreen).Background(ColorBackground).Padding(6).Column(col =>
            {
                col.Item().Text("💳 Dane do przelewu").Bold().FontSize(9).FontColor(ColorGreen);

                col.Item().PaddingTop(4).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Row(r =>
                        {
                            r.ConstantItem(70).Text("Odbiorca:").FontSize(8).FontColor(ColorGray);
                            r.RelativeItem().Column(innerCol =>
                            {
                                innerCol.Item().Text("Ubojnia Drobiu \"Piórkowscy\"").Bold().FontSize(8);
                                innerCol.Item().Text("Jerzy Piórkowski w spadku").Bold().FontSize(8);
                            });
                        });
                        c.Item().PaddingTop(2).Row(r =>
                        {
                            r.ConstantItem(70).Text("NIP:").FontSize(8).FontColor(ColorGray);
                            r.RelativeItem().Text("726-162-54-06").Bold().FontSize(8);
                        });
                    });

                    row.ConstantItem(240).BorderLeft(1).BorderColor(ColorGreen).PaddingLeft(6).Column(c =>
                    {
                        c.Item().Text("Bank Pekao S.A.").Bold().FontSize(9).FontColor(ColorGreen);
                        c.Item().PaddingTop(2).Row(r =>
                        {
                            r.ConstantItem(50).Text("Konto:").FontSize(8).FontColor(ColorGray);
                            r.RelativeItem().Text("60 1240 3060 1111 0010 4888 9213").Bold().FontSize(8).FontColor(ColorGreen);
                        });
                        c.Item().PaddingTop(2).Row(r =>
                        {
                            r.ConstantItem(50).Text("SWIFT:").FontSize(8).FontColor(ColorGray);
                            r.RelativeItem().Text("PKOPPLPW").Bold().FontSize(8).FontColor(ColorGreen);
                        });
                    });
                });

                col.Item().PaddingTop(4).Background(ColorHighlight).Padding(4).Text("💡 W tytule przelewu prosimy podać numery dokumentów.")
                    .FontSize(7).Italic().FontColor(ColorGray);
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

        private string OdmianaDni(int liczba)
        {
            if (liczba == 1) return "dzień";

            int reszta = liczba % 100;
            if (reszta >= 10 && reszta <= 21) return "dni";

            int ostatniaCyfra = liczba % 10;
            if (ostatniaCyfra >= 2 && ostatniaCyfra <= 4) return "dni";

            return "dni";
        }
    }

    // Pomocnicza klasa do konwersji liczb na słowa
    public static class LiczbyNaSlowa
    {
        private static readonly string[] Jednosci = { "", "jeden", "dwa", "trzy", "cztery", "pięć", "sześć", "siedem", "osiem", "dziewięć" };
        private static readonly string[] Nastki = { "dziesięć", "jedenaście", "dwanaście", "trzynaście", "czternaście", "piętnaście", "szesnaście", "siedemnaście", "osiemnaście", "dziewiętnaście" };
        private static readonly string[] Dziesiatki = { "", "", "dwadzieścia", "trzydzieści", "czterdzieści", "pięćdziesiąt", "sześćdziesiąt", "siedemdziesiąt", "osiemdziesiąt", "dziewięćdziesiąt" };
        private static readonly string[] Setki = { "", "sto", "dwieście", "trzysta", "czterysta", "pięćset", "sześćset", "siedemset", "osiemset", "dziewięćset" };

        public static string NaSlowa(int liczba)
        {
            if (liczba == 0) return "zero";
            if (liczba < 0) return "minus " + NaSlowa(-liczba);

            string wynik = "";

            if (liczba >= 1000000)
            {
                int miliony = liczba / 1000000;
                wynik += NaSlowa(miliony) + " " + OdmianaMiliony(miliony) + " ";
                liczba %= 1000000;
            }

            if (liczba >= 1000)
            {
                int tysiace = liczba / 1000;
                wynik += NaSlowa(tysiace) + " " + OdmianaTysiace(tysiace) + " ";
                liczba %= 1000;
            }

            if (liczba >= 100)
            {
                wynik += Setki[liczba / 100] + " ";
                liczba %= 100;
            }

            if (liczba >= 10 && liczba < 20)
            {
                wynik += Nastki[liczba - 10] + " ";
                return wynik.Trim();
            }

            if (liczba >= 20)
            {
                wynik += Dziesiatki[liczba / 10] + " ";
                liczba %= 10;
            }

            if (liczba > 0)
            {
                wynik += Jednosci[liczba] + " ";
            }

            return wynik.Trim();
        }

        private static string OdmianaTysiace(int liczba)
        {
            if (liczba == 1) return "tysiąc";
            int reszta = liczba % 100;
            if (reszta >= 10 && reszta <= 21) return "tysięcy";
            int ostatniaCyfra = liczba % 10;
            if (ostatniaCyfra >= 2 && ostatniaCyfra <= 4) return "tysiące";
            return "tysięcy";
        }

        private static string OdmianaMiliony(int liczba)
        {
            if (liczba == 1) return "milion";
            int reszta = liczba % 100;
            if (reszta >= 10 && reszta <= 21) return "milionów";
            int ostatniaCyfra = liczba % 10;
            if (ostatniaCyfra >= 2 && ostatniaCyfra <= 4) return "miliony";
            return "milionów";
        }
    }
}