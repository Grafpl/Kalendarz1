using System;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Drawing;
using System.Windows.Forms;

namespace Kalendarz1
{
    public class BlankOcenaFormPDFGenerator
    {
        // Elegancka kolorystyka premium
        private readonly string ColorNavy = "#1E3A5F";      // Granatowy główny
        private readonly string ColorGold = "#D4AF37";      // Złoty premium
        private readonly string ColorEmerald = "#2E8B57";   // Szmaragdowy
        private readonly string ColorCrimson = "#DC143C";   // Karmazynowy
        private readonly string ColorSilver = "#C0C0C0";    // Srebrny
        private readonly string ColorPearl = "#F8F8F8";     // Perłowy tło
        private readonly string ColorCharcoal = "#36454F";  // Grafitowy tekst

        private bool _showPoints = true;  // Czy pokazywać punktację

        public void GenerujPustyFormularz(string outputPath, bool showPoints = true)
        {
            _showPoints = showPoints;
            
            QuestPDF.Settings.License = LicenseType.Community;

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(15, Unit.Millimetre);
                    page.DefaultTextStyle(x => x.FontSize(11).FontColor(ColorCharcoal));

                    // Nagłówek
                    page.Header().Element(BuildHeader);
                    
                    // Zawartość
                    page.Content().Element(BuildContent);
                    
                    // Stopka
                    page.Footer().Element(BuildFooter);
                });
            }).GeneratePdf(outputPath);
        }

        private void BuildHeader(IContainer container)
        {
            container.Column(column =>
            {
                // Gradient header z logo
                column.Item().Height(80).Background(ColorNavy).Row(row =>
                {
                    // Logo po lewej
                    row.RelativeItem(2).Padding(10).AlignLeft().Column(col =>
                    {
                        string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logo.png");
                        if (File.Exists(logoPath))
                        {
                            col.Item().Height(60).Width(120).Image(logoPath);
                        }
                        else
                        {
                            col.Item().Height(60).Width(120)
                               .Background(Colors.White)
                               .Border(2).BorderColor(ColorGold)
                               .AlignCenter().AlignMiddle()
                               .Text("LOGO")
                               .FontColor(ColorNavy)
                               .Bold().FontSize(20);
                        }
                    });
                    
                    // Tytuł w środku
                    row.RelativeItem(4).AlignCenter().AlignMiddle().Column(col =>
                    {
                        col.Item().Text("FORMULARZ OCENY DOSTAWCY")
                            .FontColor(Colors.White)
                            .Bold().FontSize(20);
                        col.Item().PaddingTop(5).Text("SYSTEM ZARZĄDZANIA JAKOŚCIĄ")
                            .FontColor(ColorGold)
                            .SemiBold().FontSize(12);
                    });
                    
                    // Nr raportu po prawej
                    row.RelativeItem(2).Padding(10).AlignRight().AlignMiddle()
                       .Container().Background(ColorGold).Padding(10).Column(col =>
                       {
                           col.Item().Text("Nr raportu:").FontColor(ColorNavy).Bold().FontSize(10);
                           col.Item().Height(30).Background(Colors.White)
                              .Border(1).BorderColor(ColorNavy);
                       });
                });
                
                column.Item().PaddingTop(10);
            });
        }

        private void BuildContent(IContainer container)
        {
            container.Column(column =>
            {
                // Dane dostawcy z większymi polami
                column.Item().Element(BuildSupplierData);
                column.Item().PaddingTop(15);
                
                // Sekcja I - Samoocena hodowcy
                column.Item().Element(c => BuildSectionI(c));
                column.Item().PageBreak();
                
                // Sekcja II - Lista kontrolna część 1
                column.Item().Element(c => BuildSectionII_Part1(c));
                column.Item().PageBreak();
                
                // Sekcja II - Lista kontrolna część 2
                column.Item().Element(c => BuildSectionII_Part2(c));
                column.Item().PaddingTop(15);
                
                // Sekcja III - Dokumentacja
                column.Item().Element(c => BuildSectionIII(c));
                column.Item().PaddingTop(15);
                
                // Podsumowanie
                column.Item().Element(BuildSummary);
                column.Item().PaddingTop(15);
                
                // Podpisy
                column.Item().Element(BuildSignatures);
            });
        }

        private void BuildSupplierData(IContainer container)
        {
            container.Background(ColorPearl).Border(2).BorderColor(ColorNavy).Padding(15).Column(column =>
            {
                column.Item().Text("DANE DOSTAWCY").Bold().FontSize(14).FontColor(ColorNavy);
                column.Item().PaddingTop(10);
                
                column.Item().Row(row =>
                {
                    row.RelativeItem(2).Column(col =>
                    {
                        // Większe pola do wypełnienia
                        BuildLargeField(col, "Nazwa dostawcy:", 40);
                        BuildLargeField(col, "Adres:", 40);
                        BuildLargeField(col, "Miasto:", 35);
                    });
                    
                    row.ConstantItem(20); // Odstęp
                    
                    row.RelativeItem(2).Column(col =>
                    {
                        BuildLargeField(col, "NIP:", 35);
                        BuildLargeField(col, "Telefon:", 35);
                        BuildLargeField(col, "Data oceny:", 35);
                    });
                });
            });
        }

        private void BuildLargeField(ColumnDescriptor column, string label, int height)
        {
            column.Item().PaddingBottom(10).Row(row =>
            {
                row.ConstantItem(120).Text(label).FontSize(10).SemiBold();
                row.RelativeItem().Height(height).Background(Colors.White)
                   .Border(1).BorderColor(ColorSilver)
                   .PaddingLeft(5).AlignMiddle();
            });
        }

        private void BuildSectionI(IContainer container)
        {
            container.Column(column =>
            {
                // Nagłówek sekcji
                column.Item().Background(ColorEmerald).Padding(10).Row(row =>
                {
                    row.RelativeItem().Text("SEKCJA I - SAMOOCENA HODOWCY")
                        .FontColor(Colors.White).Bold().FontSize(12);
                    if (_showPoints)
                    {
                        row.ConstantItem(150).AlignRight()
                            .Text("Max 24 punkty (8 × 3 pkt)")
                            .FontColor(ColorGold).SemiBold();
                    }
                });
                
                // Pytania
                column.Item().Border(1).BorderColor(ColorSilver);
                
                string[] pytaniaSamooceny = new[]
                {
                    "Czy gospodarstwo jest zgłoszone w Powiatowym Inspektoracie Weterynarii?",
                    "Czy znajduje się wydzielone miejsce do składowania środków dezynfekcyjnych?",
                    "Czy obornik jest systematycznie wywożony z terenu gospodarstwa?",
                    "Czy są wydzielone miejsca do przechowywania produktów leczniczych weterynaryjnych?",
                    "Czy teren gospodarstwa jest uporządkowany i zabezpieczony przed dostępem osób postronnych?",
                    "Czy jest odzież ochronna i obuwie do użycia tylko w gospodarstwie?",
                    "Czy są maty dezynfekcyjne przy wejściach do budynków inwentarskich?",
                    "Czy są środki dezynfekcyjne w odpowiedniej ilości i ważności?"
                };
                
                for (int i = 0; i < pytaniaSamooceny.Length; i++)
                {
                    BuildQuestionRow(column, i + 1, pytaniaSamooceny[i], _showPoints ? "3/0" : "", 
                        i % 2 == 0 ? Colors.White : ColorPearl);
                }
            });
        }

        private void BuildSectionII_Part1(IContainer container)
        {
            container.Column(column =>
            {
                // Nagłówek sekcji
                column.Item().Background(ColorNavy).Padding(10).Row(row =>
                {
                    row.RelativeItem().Text("SEKCJA II - LISTA KONTROLNA (Część 1: HODOWCA)")
                        .FontColor(Colors.White).Bold().FontSize(12);
                    if (_showPoints)
                    {
                        row.ConstantItem(150).AlignRight()
                            .Text("Max 15 punktów (5 × 3 pkt)")
                            .FontColor(ColorGold).SemiBold();
                    }
                });
                
                column.Item().Border(1).BorderColor(ColorSilver);
                
                string[] pytaniaHodowca = new[]
                {
                    "Czy gospodarstwo posiada numer WNI (Weterynaryjny Numer Identyfikacyjny)?",
                    "Czy ferma objęta jest stałą opieką lekarsko-weterynaryjną?",
                    "Czy stado jest wolne od salmonelli (badania w kierunku salmonelli)?",
                    "Czy kurnik jest myty i dezynfekowany przed wstawieniem nowej partii piskląt?",
                    "Czy padłe ptaki są usuwane codziennie? Czy jest chłodnia/magazyn do ich przechowywania?"
                };
                
                for (int i = 0; i < pytaniaHodowca.Length; i++)
                {
                    BuildQuestionRow(column, i + 1, pytaniaHodowca[i], _showPoints ? "3/0" : "", 
                        i % 2 == 0 ? Colors.White : ColorPearl, "HODOWCA", ColorGold);
                }
            });
        }

        private void BuildSectionII_Part2(IContainer container)
        {
            container.Column(column =>
            {
                // Nagłówek sekcji
                column.Item().Background(ColorNavy).Padding(10).Row(row =>
                {
                    row.RelativeItem().Text("SEKCJA II - LISTA KONTROLNA (Część 2: KIEROWCA)")
                        .FontColor(Colors.White).Bold().FontSize(12);
                    if (_showPoints)
                    {
                        row.ConstantItem(150).AlignRight()
                            .Text("Max 15 punktów (15 × 1 pkt)")
                            .FontColor(ColorGold).SemiBold();
                    }
                });
                
                column.Item().Border(1).BorderColor(ColorSilver);
                
                string[] pytaniaKierowca = new[]
                {
                    "Czy jest niecki dezynfekcyjna przy wjeździe na teren gospodarstwa?",
                    "Czy są śluzy dezynfekcyjne?",
                    "Czy wentylacja w kurniku działa prawidłowo?",
                    "Czy oświetlenie jest odpowiednie i równomierne?",
                    "Czy temperatura jest właściwa dla wieku ptaków?",
                    "Czy jest odpowiednia obsada ptaków (zgodna z normami)?",
                    "Czy ściółka jest sucha i czysta?",
                    "Czy ptaki wyglądają zdrowo i są aktywne?",
                    "Czy dostęp do paszy i wody jest swobodny?",
                    "Czy linia pojenia jest czysta i sprawna?",
                    "Czy załadunek odbywa się zgodnie z dobrymi praktykami?",
                    "Czy skrzynki/kontenery są czyste i zdezynfekowane?",
                    "Czy czas załadunku jest optymalny?",
                    "Czy stosowana jest właściwa technika łapania ptaków?",
                    "Czy przestrzegane są zasady dobrostanu podczas transportu?"
                };
                
                for (int i = 0; i < pytaniaKierowca.Length; i++)
                {
                    BuildQuestionRow(column, i + 6, pytaniaKierowca[i], _showPoints ? "1/0" : "", 
                        i % 2 == 0 ? Colors.White : ColorPearl, "KIEROWCA", ColorNavy);
                }
            });
        }

        private void BuildSectionIII(IContainer container)
        {
            container.Column(column =>
            {
                // Nagłówek sekcji
                column.Item().Background(ColorCrimson).Padding(10).Row(row =>
                {
                    row.RelativeItem().Text("SEKCJA III - DOKUMENTACJA WETERYNARYJNO-ZOOTECHNICZNA")
                        .FontColor(Colors.White).Bold().FontSize(12);
                    if (_showPoints)
                    {
                        row.ConstantItem(100).AlignRight()
                            .Text("Max 6 punktów")
                            .FontColor(ColorGold).SemiBold();
                    }
                });
                
                column.Item().Border(1).BorderColor(ColorSilver).Background(ColorPearl).Padding(15).Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem(2).Column(c =>
                        {
                            BuildCheckItem(c, "☐ Książka leczenia zwierząt");
                            BuildCheckItem(c, "☐ Ewidencja padłych sztuk");
                            BuildCheckItem(c, "☐ Dokumenty WNI");
                        });
                        row.RelativeItem(2).Column(c =>
                        {
                            BuildCheckItem(c, "☐ Wyniki badań salmonelli");
                            BuildCheckItem(c, "☐ Świadectwa zdrowia");
                            BuildCheckItem(c, "☐ Karty leczenia");
                        });
                    });
                    
                    col.Item().PaddingTop(15).Text("Uwagi:")
                        .Bold().FontSize(11).FontColor(ColorNavy);
                    col.Item().Height(80).Background(Colors.White).Border(1).BorderColor(ColorSilver);
                });
            });
        }

        private void BuildQuestionRow(ColumnDescriptor column, int number, string question, 
            string points, string bgColor, string label = null, string labelColor = null)
        {
            column.Item().Background(bgColor).Border(0.5f).BorderColor(ColorSilver).Padding(8).Row(row =>
            {
                // Numer
                row.ConstantItem(30).AlignCenter().AlignMiddle()
                    .Text($"{number}.").Bold().FontSize(11);
                
                // Etykieta (jeśli jest)
                if (!string.IsNullOrEmpty(label))
                {
                    row.ConstantItem(80).AlignCenter()
                       .Container().Background(labelColor).Padding(3)
                       .AlignCenter().AlignMiddle()
                       .Text(label).FontColor(Colors.White).Bold().FontSize(9);
                }
                
                // Pytanie
                row.RelativeItem(5).PaddingLeft(5).AlignMiddle()
                    .Text(question).FontSize(10);
                
                // Większe checkboxy
                row.ConstantItem(80).AlignCenter().AlignMiddle().Row(r =>
                {
                    r.RelativeItem().AlignCenter().Column(c =>
                    {
                        c.Item().Text("TAK").Bold().FontSize(9).FontColor(ColorEmerald);
                        c.Item().Height(25).Width(25).Background(Colors.White)
                         .Border(2).BorderColor(ColorEmerald);
                    });
                    r.ConstantItem(10);
                    r.RelativeItem().AlignCenter().Column(c =>
                    {
                        c.Item().Text("NIE").Bold().FontSize(9).FontColor(ColorCrimson);
                        c.Item().Height(25).Width(25).Background(Colors.White)
                         .Border(2).BorderColor(ColorCrimson);
                    });
                });
                
                // Punkty (jeśli pokazywać)
                if (_showPoints && !string.IsNullOrEmpty(points))
                {
                    row.ConstantItem(50).AlignCenter().AlignMiddle()
                        .Container().Background(ColorGold).Padding(3)
                        .AlignCenter().Text(points).Bold().FontSize(9).FontColor(ColorNavy);
                }
                
                // Pole na punkty
                row.ConstantItem(40).AlignCenter().AlignMiddle()
                    .Height(25).Width(35).Background(Colors.White)
                    .Border(1).BorderColor(ColorCharcoal);
            });
        }

        private void BuildCheckItem(ColumnDescriptor column, string text)
        {
            column.Item().PaddingBottom(8).Text(text).FontSize(11);
        }

        private void BuildSummary(IContainer container)
        {
            container.Background(ColorPearl).Border(2).BorderColor(ColorNavy).Padding(15).Column(column =>
            {
                column.Item().Text("PODSUMOWANIE PUNKTACJI").Bold().FontSize(14).FontColor(ColorNavy);
                column.Item().PaddingTop(10);
                
                column.Item().Row(row =>
                {
                    // Tabela punktacji
                    row.RelativeItem(3).Column(col =>
                    {
                        if (_showPoints)
                        {
                            BuildSummaryRow(col, "Sekcja I - Samoocena", "_____ / 24 pkt");
                            BuildSummaryRow(col, "Sekcja II - Lista kontrolna (Hodowca)", "_____ / 15 pkt");
                            BuildSummaryRow(col, "Sekcja II - Lista kontrolna (Kierowca)", "_____ / 15 pkt");
                            BuildSummaryRow(col, "Sekcja III - Dokumentacja", "_____ / 6 pkt");
                        }
                        else
                        {
                            BuildSummaryRow(col, "Sekcja I - Samoocena", "_____");
                            BuildSummaryRow(col, "Sekcja II - Lista kontrolna (Hodowca)", "_____");
                            BuildSummaryRow(col, "Sekcja II - Lista kontrolna (Kierowca)", "_____");
                            BuildSummaryRow(col, "Sekcja III - Dokumentacja", "_____");
                        }
                        
                        col.Item().PaddingTop(10).BorderTop(2).BorderColor(ColorNavy);
                        BuildSummaryRow(col, "SUMA PUNKTÓW", _showPoints ? "_____ / 60 pkt" : "_____", true);
                    });
                    
                    row.ConstantItem(30);
                    
                    // Skala ocen
                    row.RelativeItem(2).Column(col =>
                    {
                        col.Item().Text("SKALA OCEN:").Bold().FontSize(11).FontColor(ColorNavy);
                        col.Item().PaddingTop(10);
                        
                        if (_showPoints)
                        {
                            BuildScaleItem(col, "≥ 30 pkt", "BARDZO DOBRY", ColorEmerald);
                            BuildScaleItem(col, "20-29 pkt", "DOBRY", ColorGold);
                            BuildScaleItem(col, "< 20 pkt", "NIEZADOWALAJĄCY", ColorCrimson);
                        }
                        else
                        {
                            BuildScaleItem(col, "", "BARDZO DOBRY", ColorEmerald);
                            BuildScaleItem(col, "", "DOBRY", ColorGold);
                            BuildScaleItem(col, "", "NIEZADOWALAJĄCY", ColorCrimson);
                        }
                        
                        col.Item().PaddingTop(15).Height(40).Background(Colors.White)
                           .Border(2).BorderColor(ColorNavy)
                           .AlignCenter().AlignMiddle()
                           .Text("OCENA: _________")
                           .Bold().FontSize(12);
                    });
                });
            });
        }

        private void BuildSummaryRow(ColumnDescriptor column, string label, string value, bool isBold = false)
        {
            column.Item().PaddingVertical(5).Row(row =>
            {
                row.RelativeItem(3).Text(label)
                    .FontSize(isBold ? 12 : 11)
                    .Bold(isBold)
                    .FontColor(isBold ? ColorNavy : ColorCharcoal);
                row.RelativeItem(2).AlignRight().Text(value)
                    .FontSize(isBold ? 12 : 11)
                    .Bold(isBold)
                    .FontColor(isBold ? ColorNavy : ColorCharcoal);
            });
        }

        private void BuildScaleItem(ColumnDescriptor column, string range, string grade, string color)
        {
            column.Item().PaddingBottom(8).Row(row =>
            {
                if (!string.IsNullOrEmpty(range))
                {
                    row.ConstantItem(80).Text(range).FontSize(10);
                }
                row.RelativeItem().Container().Background(color).Padding(5)
                   .AlignCenter().Text(grade)
                   .FontColor(Colors.White).Bold().FontSize(10);
            });
        }

        private void BuildSignatures(IContainer container)
        {
            container.Column(column =>
            {
                column.Item().Text("PODPISY I ZATWIERDZENIE")
                    .Bold().FontSize(12).FontColor(ColorNavy);
                column.Item().PaddingTop(15);
                
                column.Item().Row(row =>
                {
                    BuildSignatureBox(row.RelativeItem(), "Hodowca", "Imię i nazwisko / Data / Podpis");
                    row.ConstantItem(20);
                    BuildSignatureBox(row.RelativeItem(), "Kierowca", "Imię i nazwisko / Data / Podpis");
                    row.ConstantItem(20);
                    BuildSignatureBox(row.RelativeItem(), "Zatwierdzający", "Imię i nazwisko / Data / Podpis");
                });
            });
        }

        private void BuildSignatureBox(IContainer container, string title, string subtitle)
        {
            container.Column(column =>
            {
                column.Item().Text(title.ToUpper())
                    .Bold().FontSize(10).FontColor(ColorNavy);
                column.Item().Text(subtitle)
                    .FontSize(8).FontColor(Colors.Grey);
                column.Item().PaddingTop(5).Height(60)
                    .Background(Colors.White)
                    .Border(1).BorderColor(ColorSilver);
            });
        }

        private void BuildFooter(IContainer container)
        {
            container.Background(ColorCharcoal).Padding(10).Row(row =>
            {
                row.RelativeItem().AlignLeft().Text(text =>
                {
                    text.Span("Dokument wygenerowany: ").FontColor(Colors.White).FontSize(8);
                    text.Span(DateTime.Now.ToString("yyyy-MM-dd HH:mm"))
                        .FontColor(ColorGold).FontSize(8);
                });
                
                row.RelativeItem().AlignCenter().Text("System Zarządzania Jakością ISO 9001:2015")
                    .FontColor(Colors.White).FontSize(8);
                
                row.RelativeItem().AlignRight().Text(text =>
                {
                    text.Span("Strona ").FontColor(Colors.White).FontSize(8);
                    text.CurrentPageNumber().FontColor(ColorGold).FontSize(8);
                    text.Span(" z ").FontColor(Colors.White).FontSize(8);
                    text.TotalPages().FontColor(ColorGold).FontSize(8);
                });
            });
        }
    }
    
    // Klasa pomocnicza do dialogu
    public static class FormularzDialog
    {
        public static bool ZapytajOPunktacje()
        {
            var result = MessageBox.Show(
                "Czy wyświetlić punktację na formularzu?\n\n" +
                "TAK - pokaże wartości punktów za każde pytanie\n" +
                "NIE - wydrukuje formularz bez punktacji (tylko pola do wypełnienia)",
                "Opcje wydruku formularza",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button1);
                
            return result == DialogResult.Yes;
        }
    }
}
