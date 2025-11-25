using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.IO;

namespace Kalendarz1
{
    /// <summary>
    /// Generator pustego formularza oceny dostawcy - WERSJA DZIAŁAJĄCA
    /// Bazuje na sprawdzonym wzorcu z OcenaPDFGenerator_v3
    /// </summary>
    public class BlankOcenaFormPDFGenerator
    {
        #region Kolory

        private readonly string ColorPrimary = "#1E3A5F";       // Granatowy
        private readonly string ColorPrimaryLight = "#2E5A8F";
        private readonly string ColorGold = "#D4AF37";          // Złoty
        private readonly string ColorGreen = "#2E8B57";         // Zielony
        private readonly string ColorRed = "#DC143C";           // Czerwony
        private readonly string ColorGray = "#808080";          // Szary
        private readonly string ColorLightGray = "#F5F5F5";     // Jasny szary
        private readonly string ColorWhite = "#FFFFFF";
        private readonly string ColorText = "#333333";

        #endregion

        #region Pytania

        private readonly string[] PytaniaSamoocena = new[]
        {
            "Czy gospodarstwo jest zgłoszone w PIW (Powiatowy Inspektorat Weterynarii)?",
            "Czy w gospodarstwie znajduje się wydzielone miejsce do składowania środków dezynfekcyjnych?",
            "Czy obornik jest systematycznie wywożony z gospodarstwa?",
            "Czy w gospodarstwie znajdują się miejsca do przechowywania produktów leczniczych weterynaryjnych?",
            "Czy teren wokół fermy jest uporządkowany oraz zabezpieczony przed dostępem osób postronnych?",
            "Czy w gospodarstwie znajduje się odzież i obuwie przeznaczone tylko do użycia w gospodarstwie?",
            "Czy w gospodarstwie znajdują się maty dezynfekcyjne przy wejściach do budynków inwentarskich?",
            "Czy gospodarstwo posiada środki dezynfekcyjne w ilości niezbędnej do przeprowadzenia dezynfekcji?"
        };

        private readonly string[] PytaniaHodowca = new[]
        {
            "Czy gospodarstwo posiada aktualny numer WNI (Weterynaryjny Numer Identyfikacyjny)?",
            "Czy ferma objęta jest stałą opieką lekarsko-weterynaryjną?",
            "Czy stado jest wolne od salmonelli (potwierdzone badaniami)?",
            "Czy kurnik jest dokładnie myty i dezynfekowany przed wstawieniem każdej partii piskląt?",
            "Czy padłe ptaki usuwane są codziennie? Czy ferma posiada chłodnię na sztuki padłe?"
        };

        private readonly string[] PytaniaKierowca = new[]
        {
            "Czy jest niecka dezynfekcyjna przy wjeździe na teren gospodarstwa?",
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

        #endregion

        private bool _showPoints = true;

        /// <summary>
        /// Generuje pusty formularz PDF do ręcznego wypełnienia
        /// </summary>
        /// <param name="outputPath">Ścieżka do pliku PDF</param>
        /// <param name="showPoints">Czy pokazywać punktację</param>
        public void GenerujPustyFormularz(string outputPath, bool showPoints = true)
        {
            _showPoints = showPoints;

            // Ustaw licencję QuestPDF
            QuestPDF.Settings.License = LicenseType.Community;

            // Tworzenie dokumentu - DOKŁADNIE jak w działającym OcenaPDFGenerator_v3
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(25);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Calibri).FontColor(ColorText));

                    page.Header().Element(ComposeHeader);
                    page.Content().Element(ComposeContent);
                    page.Footer().Element(ComposeFooter);
                });
            })
            .GeneratePdf(outputPath);
        }

        #region Nagłówek

        private void ComposeHeader(IContainer container)
        {
            container.Column(column =>
            {
                // Pasek tytułowy
                column.Item().Background(ColorPrimary).Padding(12).Row(row =>
                {
                    row.ConstantItem(100).Height(45).Background(ColorWhite).Padding(5)
                        .AlignCenter().AlignMiddle()
                        .Text("LOGO").FontSize(14).Bold().FontColor(ColorPrimary);

                    row.RelativeItem().PaddingLeft(15).AlignMiddle().Column(col =>
                    {
                        col.Item().Text("FORMULARZ OCENY DOSTAWCY")
                            .FontSize(16).Bold().FontColor(ColorWhite);
                        col.Item().Text("System Zarządzania Jakością Dostaw Żywca")
                            .FontSize(9).FontColor(ColorGold);
                    });

                    row.ConstantItem(120).AlignRight().AlignMiddle().Column(col =>
                    {
                        col.Item().Text("Nr raportu:").FontSize(8).FontColor(ColorGold);
                        col.Item().Height(20).Background(ColorWhite).Border(1).BorderColor(ColorGold);
                    });
                });

                // Pasek danych
                column.Item().Background(ColorLightGray).Border(1).BorderColor(ColorPrimary)
                    .Padding(8).Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Row(r =>
                            {
                                r.ConstantItem(80).Text("Dostawca:").Bold().FontSize(9);
                                r.RelativeItem().Height(18).Background(ColorWhite)
                                    .Border(1).BorderColor(ColorGray);
                            });
                            col.Item().PaddingTop(3).Row(r =>
                            {
                                r.ConstantItem(80).Text("Adres:").Bold().FontSize(9);
                                r.RelativeItem().Height(18).Background(ColorWhite)
                                    .Border(1).BorderColor(ColorGray);
                            });
                        });

                        row.ConstantItem(15);

                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Row(r =>
                            {
                                r.ConstantItem(70).Text("Data oceny:").Bold().FontSize(9);
                                r.RelativeItem().Height(18).Background(ColorWhite)
                                    .Border(1).BorderColor(ColorGray);
                            });
                            col.Item().PaddingTop(3).Row(r =>
                            {
                                r.ConstantItem(70).Text("NIP:").Bold().FontSize(9);
                                r.RelativeItem().Height(18).Background(ColorWhite)
                                    .Border(1).BorderColor(ColorGray);
                            });
                        });
                    });

                column.Item().Height(8);
            });
        }

        #endregion

        #region Zawartość

        private void ComposeContent(IContainer container)
        {
            container.Column(column =>
            {
                // SEKCJA I - Samoocena hodowcy
                column.Item().Element(c => ComposeSectionHeader(c, "SEKCJA I", "SAMOOCENA HODOWCY", 
                    _showPoints ? "Wypełnia Hodowca | 3 punkty za TAK | Max: 24 pkt" : "Wypełnia Hodowca", ColorGreen));
                column.Item().Element(c => ComposeQuestionTable(c, PytaniaSamoocena, 1, 3));
                column.Item().Height(12);

                // SEKCJA II część 1 - Hodowca
                column.Item().Element(c => ComposeSectionHeader(c, "SEKCJA II", "LISTA KONTROLNA - HODOWCA", 
                    _showPoints ? "Wypełnia Hodowca | 3 punkty za TAK | Max: 15 pkt" : "Wypełnia Hodowca", ColorGold));
                column.Item().Element(c => ComposeQuestionTable(c, PytaniaHodowca, 1, 3));
                column.Item().Height(12);

                // SEKCJA II część 2 - Kierowca
                column.Item().Element(c => ComposeSectionHeader(c, "SEKCJA II", "LISTA KONTROLNA - KIEROWCA", 
                    _showPoints ? "Wypełnia Kierowca | 1 punkt za TAK | Max: 15 pkt" : "Wypełnia Kierowca", ColorPrimary));
                column.Item().Element(c => ComposeQuestionTable(c, PytaniaKierowca, 6, 1));
                column.Item().Height(12);

                // SEKCJA III - Dokumentacja
                column.Item().Element(ComposeDokumentacja);
                column.Item().Height(12);

                // Podsumowanie
                column.Item().Element(ComposePodsumowanie);
                column.Item().Height(12);

                // Podpisy
                column.Item().Element(ComposePodpisy);
            });
        }

        private void ComposeSectionHeader(IContainer container, string numer, string tytul, string opis, string kolor)
        {
            container.Background(kolor).Padding(8).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text($"{numer}: {tytul}").FontSize(11).Bold().FontColor(ColorWhite);
                    col.Item().Text(opis).FontSize(8).FontColor(ColorWhite);
                });
            });
        }

        private void ComposeQuestionTable(IContainer container, string[] pytania, int startNum, int punkty)
        {
            container.Border(1).BorderColor(ColorGray).Column(column =>
            {
                // Nagłówek tabeli
                column.Item().Background(ColorLightGray).BorderBottom(1).BorderColor(ColorGray)
                    .Padding(5).Row(row =>
                    {
                        row.ConstantItem(25).AlignCenter().Text("Nr").Bold().FontSize(8);
                        row.RelativeItem().Text("Pytanie").Bold().FontSize(8);
                        row.ConstantItem(35).AlignCenter().Text("TAK").Bold().FontSize(8).FontColor(ColorGreen);
                        row.ConstantItem(35).AlignCenter().Text("NIE").Bold().FontSize(8).FontColor(ColorRed);
                        if (_showPoints)
                        {
                            row.ConstantItem(35).AlignCenter().Text("Pkt").Bold().FontSize(8);
                        }
                        row.ConstantItem(30).AlignCenter().Text("Wynik").Bold().FontSize(8);
                    });

                // Wiersze z pytaniami
                for (int i = 0; i < pytania.Length; i++)
                {
                    var bgColor = i % 2 == 0 ? ColorWhite : ColorLightGray;
                    int numer = startNum + i;

                    column.Item().Background(bgColor).BorderBottom(1).BorderColor(ColorLightGray)
                        .Padding(4).Row(row =>
                        {
                            row.ConstantItem(25).AlignCenter().AlignMiddle()
                                .Text($"{numer}.").FontSize(9).Bold();

                            row.RelativeItem().AlignMiddle().PaddingRight(5)
                                .Text(pytania[i]).FontSize(8);

                            // Checkbox TAK
                            row.ConstantItem(35).AlignCenter().AlignMiddle()
                                .Width(16).Height(16).Border(2).BorderColor(ColorGreen);

                            // Checkbox NIE
                            row.ConstantItem(35).AlignCenter().AlignMiddle()
                                .Width(16).Height(16).Border(2).BorderColor(ColorRed);

                            // Punkty
                            if (_showPoints)
                            {
                                row.ConstantItem(35).AlignCenter().AlignMiddle()
                                    .Background(ColorGold).Padding(2)
                                    .Text($"{punkty}/0").FontSize(8).Bold().FontColor(ColorPrimary);
                            }

                            // Pole wynik
                            row.ConstantItem(30).AlignCenter().AlignMiddle()
                                .Width(22).Height(16).Border(1).BorderColor(ColorText);
                        });
                }
            });
        }

        private void ComposeDokumentacja(IContainer container)
        {
            container.Border(1).BorderColor(ColorRed).Column(column =>
            {
                column.Item().Background(ColorRed).Padding(8).Row(row =>
                {
                    row.RelativeItem().Text("SEKCJA III: DOKUMENTACJA WETERYNARYJNO-ZOOTECHNICZNA")
                        .FontSize(11).Bold().FontColor(ColorWhite);
                    if (_showPoints)
                    {
                        row.ConstantItem(80).AlignRight()
                            .Text("Max: 6 pkt").FontSize(9).FontColor(ColorGold);
                    }
                });

                column.Item().Padding(10).Column(col =>
                {
                    col.Item().Text("Sprawdź dostępność następujących dokumentów:").FontSize(9).Bold();
                    col.Item().PaddingTop(8).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("☐ Książka leczenia zwierząt").FontSize(9);
                            c.Item().PaddingTop(3).Text("☐ Ewidencja padłych sztuk").FontSize(9);
                            c.Item().PaddingTop(3).Text("☐ Dokumenty WNI").FontSize(9);
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("☐ Wyniki badań salmonelli").FontSize(9);
                            c.Item().PaddingTop(3).Text("☐ Świadectwa zdrowia").FontSize(9);
                            c.Item().PaddingTop(3).Text("☐ Karty leczenia").FontSize(9);
                        });
                    });

                    col.Item().PaddingTop(10).Text("Uwagi i zalecenia:").FontSize(9).Bold();
                    col.Item().PaddingTop(3).Height(50).Border(1).BorderColor(ColorGray).Background(ColorWhite);
                });
            });
        }

        private void ComposePodsumowanie(IContainer container)
        {
            container.Border(2).BorderColor(ColorPrimary).Column(column =>
            {
                column.Item().Background(ColorPrimary).Padding(8)
                    .Text("PODSUMOWANIE PUNKTACJI").FontSize(12).Bold().FontColor(ColorWhite);

                column.Item().Padding(10).Row(row =>
                {
                    // Tabela punktów
                    row.RelativeItem(3).Column(col =>
                    {
                        ComposeSumRow(col, "Sekcja I - Samoocena hodowcy", _showPoints ? "/ 24" : "");
                        ComposeSumRow(col, "Sekcja II - Lista kontrolna (Hodowca)", _showPoints ? "/ 15" : "");
                        ComposeSumRow(col, "Sekcja II - Lista kontrolna (Kierowca)", _showPoints ? "/ 15" : "");
                        ComposeSumRow(col, "Sekcja III - Dokumentacja", _showPoints ? "/ 6" : "");

                        col.Item().PaddingTop(8).BorderTop(2).BorderColor(ColorPrimary);
                        col.Item().PaddingTop(5).Row(r =>
                        {
                            r.RelativeItem().Text("SUMA PUNKTÓW:").FontSize(11).Bold();
                            r.ConstantItem(80).AlignRight()
                                .Text(_showPoints ? "_______ / 60 pkt" : "_______").FontSize(11).Bold();
                        });
                    });

                    row.ConstantItem(15);

                    // Skala ocen
                    row.RelativeItem(2).Column(col =>
                    {
                        col.Item().Text("SKALA OCEN:").FontSize(10).Bold();
                        col.Item().PaddingTop(5);

                        col.Item().Background(ColorGreen).Padding(5)
                            .Text(_showPoints ? "≥ 30 pkt = BARDZO DOBRY" : "BARDZO DOBRY")
                            .FontSize(9).Bold().FontColor(ColorWhite);

                        col.Item().PaddingTop(2).Background(ColorGold).Padding(5)
                            .Text(_showPoints ? "20-29 pkt = DOBRY" : "DOBRY")
                            .FontSize(9).Bold().FontColor(ColorWhite);

                        col.Item().PaddingTop(2).Background(ColorRed).Padding(5)
                            .Text(_showPoints ? "< 20 pkt = NIEZADOWALAJĄCY" : "NIEZADOWALAJĄCY")
                            .FontSize(9).Bold().FontColor(ColorWhite);

                        col.Item().PaddingTop(8).Height(30).Border(2).BorderColor(ColorPrimary)
                            .AlignCenter().AlignMiddle()
                            .Text("OCENA: ____________").FontSize(10).Bold();
                    });
                });
            });
        }

        private void ComposeSumRow(ColumnDescriptor col, string label, string max)
        {
            col.Item().PaddingVertical(3).Row(row =>
            {
                row.RelativeItem().Text(label).FontSize(9);
                row.ConstantItem(80).AlignRight().Text($"_______ {max}").FontSize(9);
            });
        }

        private void ComposePodpisy(IContainer container)
        {
            container.Column(column =>
            {
                column.Item().Text("PODPISY I ZATWIERDZENIE").FontSize(11).Bold().FontColor(ColorPrimary);
                column.Item().PaddingTop(8).Row(row =>
                {
                    ComposeSignBox(row.RelativeItem(), "HODOWCA", "Data i podpis");
                    row.ConstantItem(10);
                    ComposeSignBox(row.RelativeItem(), "KIEROWCA", "Data i podpis");
                    row.ConstantItem(10);
                    ComposeSignBox(row.RelativeItem(), "ZATWIERDZAJĄCY", "Data i podpis");
                });
            });
        }

        private void ComposeSignBox(IContainer container, string tytul, string opis)
        {
            container.Border(1).BorderColor(ColorGray).Column(col =>
            {
                col.Item().Background(ColorLightGray).Padding(5)
                    .Text(tytul).FontSize(9).Bold().FontColor(ColorPrimary);
                col.Item().Padding(3).Text(opis).FontSize(7).FontColor(ColorGray);
                col.Item().Height(45).Background(ColorWhite);
            });
        }

        #endregion

        #region Stopka

        private void ComposeFooter(IContainer container)
        {
            container.Background(ColorPrimary).Padding(8).Row(row =>
            {
                row.RelativeItem().AlignLeft().Text(text =>
                {
                    text.Span("Wygenerowano: ").FontSize(8).FontColor(ColorWhite);
                    text.Span(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).FontSize(8).FontColor(ColorGold);
                });

                row.RelativeItem().AlignCenter()
                    .Text("System Oceny Dostawców Żywca").FontSize(8).FontColor(ColorWhite);

                row.RelativeItem().AlignRight().Text(text =>
                {
                    text.Span("Strona ").FontSize(8).FontColor(ColorWhite);
                    text.CurrentPageNumber().FontSize(8).FontColor(ColorGold);
                    text.Span(" z ").FontSize(8).FontColor(ColorWhite);
                    text.TotalPages().FontSize(8).FontColor(ColorGold);
                });
            });
        }

        #endregion
    }

    /// <summary>
    /// Klasa pomocnicza do pytania o opcje formularza - wersja Windows Forms
    /// </summary>
    public static class FormularzDialog
    {
        /// <summary>
        /// Pyta użytkownika czy pokazać punktację na formularzu
        /// </summary>
        public static bool ZapytajOPunktacje()
        {
            var result = System.Windows.Forms.MessageBox.Show(
                "Czy wyświetlić punktację na formularzu?\n\n" +
                "TAK - pokaże wartości punktów za każde pytanie\n" +
                "NIE - wydrukuje formularz bez punktacji (tylko pola do wypełnienia)",
                "Opcje wydruku formularza",
                System.Windows.Forms.MessageBoxButtons.YesNo,
                System.Windows.Forms.MessageBoxIcon.Question,
                System.Windows.Forms.MessageBoxDefaultButton.Button1);

            return result == System.Windows.Forms.DialogResult.Yes;
        }
    }
}
