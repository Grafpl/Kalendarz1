using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.IO;
using System.Linq;

namespace Kalendarz1
{
    /// <summary>
    /// Profesjonalny generator raportów PDF dla oceny dostawców żywca
    /// Wersja 3.0 - Rozszerzona funkcjonalność
    /// </summary>
    public class OcenaPDFGenerator
    {
        #region Pola danych
        
        private string _numerRaportu;
        private DateTime _dataOceny;
        private string _dostawcaNazwa;
        private string _dostawcaId;
        private string _uwagi;
        private int _punkty1_5;
        private int _punkty6_20;
        private int _punktyRazem;
        private bool[] _samoocena;
        private bool[] _listaKontrolna;
        private bool _dokumentacja;
        private bool _czyPustyFormularz;
        
        // NOWE: Dodatkowe opcje
        private string _watermark;  // DRAFT, KOPIA, ANULOWANO
        private bool _pokazKodQR;
        private OcenaPorownanieData _poprzedniaOcena;  // Do porównania
        private StatystykiDostawcy _statystyki;  // Statystyki historyczne

        #endregion

        #region Paleta kolorów

        private readonly string ColorPrimary = "#2E7D32";
        private readonly string ColorPrimaryLight = "#66BB6A";
        private readonly string ColorPrimaryBg = "#E8F5E9";
        private readonly string ColorSecondary = "#1565C0";
        private readonly string ColorSecondaryBg = "#E3F2FD";
        private readonly string ColorWarning = "#F57C00";
        private readonly string ColorWarningBg = "#FFF3E0";
        private readonly string ColorDanger = "#C62828";
        private readonly string ColorDangerBg = "#FFEBEE";
        private readonly string ColorText = "#212121";
        private readonly string ColorTextLight = "#757575";
        private readonly string ColorBorder = "#BDBDBD";
        private readonly string ColorBorderLight = "#E0E0E0";
        private readonly string ColorBackground = "#FAFAFA";
        private readonly string ColorWhite = "#FFFFFF";
        
        #endregion

        #region Treści pytań

        private readonly string[] PytaniaSamoocena = new[]
        {
            "Czy gospodarstwo jest zgłoszone w PIW (Powiatowy Inspektorat Weterynarii)?",
            "Czy w gospodarstwie znajduje się wydzielone miejsce do składowania środków dezynfekcyjnych?",
            "Czy obornik jest systematycznie wywożony z gospodarstwa?",
            "Czy w gospodarstwie znajdują się miejsca zapewniające właściwe warunki przechowywania produktów leczniczych?",
            "Czy teren wokół fermy jest uporządkowany oraz zabezpieczony przed dostępem innych zwierząt?"
        };

        private readonly string[] PytaniaKontrolnaA = new[]
        {
            "Czy w gospodarstwie znajduje się odzież i obuwie lub ochraniacze przeznaczone tylko do użycia w gospodarstwie?",
            "Czy w gospodarstwie znajdują się maty dezynfekcyjne przy wejściach?",
            "Czy gospodarstwo posiada środki dezynfekcyjne w ilości niezbędnej do przeprowadzenia doraźnej dezynfekcji?",
            "Czy gospodarstwo posiada aktualny numer WNI (Weterynaryjny Numer Identyfikacyjny)?",
            "Czy ferma objęta jest stałą opieką weterynaryjną?"
        };

        private readonly string[] PytaniaKontrolnaB = new[]
        {
            "Czy stado jest wolne od salmonelli (potwierdzone badaniami)?",
            "Czy kurnik jest dokładnie myty i dezynfekowany przed wstawieniem każdej partii piskląt?",
            "Czy padłe ptaki usuwane są codziennie? Czy ferma posiada chłodnię/magazyn na sztuki padłe?",
            "Czy godzina przyjazdu na fermę/wagę jest zgodna z planowaną godziną?",
            "Czy załadunek rozpoczął się o planowanej godzinie (bez opóźnień)?"
        };

        private readonly string[] PytaniaKontrolnaC = new[]
        {
            "Czy wjazd na fermę jest wybetonowany lub utwardzony?",
            "Czy wjazd na fermę jest odpowiednio oświetlony?",
            "Czy podjazd pod kurnik jest oświetlony?",
            "Czy podjazd pod kurnik jest wybetonowany/utwardzony?",
            "Czy kurnik jest dostosowany do załadunku wózkiem widłowym?"
        };

        private readonly string[] PytaniaKontrolnaD = new[]
        {
            "Czy zapewniona jest identyfikowalność? Czy kurniki są wyraźnie oznaczone?",
            "Czy podczas wyłapywania brojlerów zapewniono niebieskie oświetlenie na kurniku?",
            "Czy ściółka jest sucha i w dobrym stanie?",
            "Czy kury są czyste (bez zabrudzeń)?",
            "Czy kury są suche (odpowiedni stan upierzenia)?"
        };

        private readonly string[] PytaniaKontrolnaE = new[]
        {
            "Czy podczas załadunku kurniki są puste (bez pozostałych ptaków)?",
            "Czy technika łapania i ładowania kurczat jest odpowiednia (zgodna z procedurami)?",
            "Czy ilość osób do załadunku jest odpowiednia (wydajność pracy)?",
            "Czy zapewniono odpowiednie warunki BHP podczas załadunku?",
            "Czy stan sanitarny miejsca załadunku jest zadowalający?"
        };

        #endregion

        #region Metoda główna generowania PDF

        /// <summary>
        /// Generuje profesjonalny raport PDF oceny dostawcy
        /// </summary>
        public void GenerujPdf(
            string sciezkaDoPliku, 
            string numerRaportu, 
            DateTime dataOceny,
            string dostawcaNazwa, 
            string dostawcaId,
            bool[] samoocena, 
            bool[] listaKontrolna, 
            bool dokumentacja,
            int p1_5, 
            int p6_20, 
            int pRazem, 
            string uwagi, 
            bool czyPustyFormularz)
        {
            GenerujPdfRozszerzony(
                sciezkaDoPliku, numerRaportu, dataOceny,
                dostawcaNazwa, dostawcaId,
                samoocena, listaKontrolna, dokumentacja,
                p1_5, p6_20, pRazem, uwagi, czyPustyFormularz,
                watermark: null,
                pokazKodQR: false,
                poprzedniaOcena: null,
                statystyki: null
            );
        }

        /// <summary>
        /// ROZSZERZONA wersja - z dodatkowymi opcjami
        /// </summary>
        public void GenerujPdfRozszerzony(
            string sciezkaDoPliku, 
            string numerRaportu, 
            DateTime dataOceny,
            string dostawcaNazwa, 
            string dostawcaId,
            bool[] samoocena, 
            bool[] listaKontrolna, 
            bool dokumentacja,
            int p1_5, 
            int p6_20, 
            int pRazem, 
            string uwagi, 
            bool czyPustyFormularz,
            string watermark = null,  // "DRAFT", "KOPIA", "ANULOWANO"
            bool pokazKodQR = false,
            OcenaPorownanieData poprzedniaOcena = null,
            StatystykiDostawcy statystyki = null)
        {
            _numerRaportu = numerRaportu;
            _dataOceny = dataOceny;
            _dostawcaNazwa = dostawcaNazwa;
            _dostawcaId = dostawcaId;
            _samoocena = samoocena;
            _listaKontrolna = listaKontrolna;
            _dokumentacja = dokumentacja;
            _punkty1_5 = p1_5;
            _punkty6_20 = p6_20;
            _punktyRazem = pRazem;
            _uwagi = uwagi ?? "";
            _czyPustyFormularz = czyPustyFormularz;
            _watermark = watermark;
            _pokazKodQR = pokazKodQR;
            _poprzedniaOcena = poprzedniaOcena;
            _statystyki = statystyki;

            QuestPDF.Settings.License = LicenseType.Community;

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Calibri).FontColor(ColorText));

                    page.Header().Element(ComposeHeader);
                    page.Content().Element(ComposeContent);
                    page.Footer().Element(ComposeFooter);
                });
            })
            .GeneratePdf(sciezkaDoPliku);
        }

        #endregion

        #region Sekcje dokumentu

        private void ComposeHeader(IContainer container)
        {
            container.Column(column =>
            {
                column.Item().Background(ColorPrimary).Padding(15).Row(row =>
                {
                    row.ConstantItem(120).Element(logoContainer =>
                    {
                        try
                        {
                            string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logo.png");
                            if (File.Exists(logoPath))
                            {
                                logoContainer.Height(50).Background(ColorWhite)
                                    .Padding(5).Image(File.ReadAllBytes(logoPath)).FitArea();
                            }
                            else
                            {
                                logoContainer.Height(50).Background(ColorWhite)
                                    .AlignCenter().AlignMiddle()
                                    .Text("LOGO").FontSize(16).Bold().FontColor(ColorPrimary);
                            }
                        }
                        catch
                        {
                            logoContainer.Height(50).Background(ColorWhite)
                                .AlignCenter().AlignMiddle()
                                .Text("LOGO").FontSize(16).Bold().FontColor(ColorPrimary);
                        }
                    });

                    row.RelativeItem().PaddingLeft(20).Column(col =>
                    {
                        col.Item().AlignLeft().Text("FORMULARZ OCENY DOSTAWCY")
                            .FontSize(18).Bold().FontColor(ColorWhite);
                        col.Item().AlignLeft().Text("System Zarządzania Jakością Dostaw Żywca")
                            .FontSize(10).FontColor(ColorWhite);
                    });
                });

                // Pasek informacyjny
                column.Item().Background(ColorPrimaryBg).Border(1).BorderColor(ColorPrimary)
                    .Padding(10).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(text =>
                        {
                            text.Span("DOSTAWCA: ").Bold().FontSize(9);
                            text.Span(_dostawcaNazwa ?? "").FontSize(10).FontColor(ColorPrimary).Bold();
                        });
                        col.Item().Text(text =>
                        {
                            text.Span("ID Dostawcy: ").FontSize(8).FontColor(ColorTextLight);
                            text.Span(_dostawcaId ?? "").FontSize(8).Bold();
                        });
                    });

                    row.RelativeItem().AlignRight().Column(col =>
                    {
                        col.Item().AlignRight().Text(text =>
                        {
                            text.Span("Raport Nr: ").FontSize(8).FontColor(ColorTextLight);
                            text.Span(_czyPustyFormularz ? "_______________" : _numerRaportu)
                                .FontSize(10).Bold().FontColor(ColorPrimary);
                        });
                        col.Item().AlignRight().Text(text =>
                        {
                            text.Span("Data oceny: ").FontSize(8).FontColor(ColorTextLight);
                            text.Span(_czyPustyFormularz ? "__.__.____" : _dataOceny.ToString("dd.MM.yyyy"))
                                .FontSize(9).Bold();
                        });
                    });
                });

                column.Item().Height(10);
            });
        }

        private void ComposeContent(IContainer container)
        {
            container.Column(column =>
            {
                // WATERMARK jeśli ustawiony
                if (!string.IsNullOrEmpty(_watermark))
                {
                    column.Item().Element(ComposeWatermark);
                    column.Item().Height(10);
                }

                // Instrukcja dla pustego formularza
                if (_czyPustyFormularz)
                {
                    column.Item().Element(ComposeInstrukcja);
                    column.Item().Height(10);
                }

                // KOD QR jeśli włączony
                if (_pokazKodQR && !_czyPustyFormularz)
                {
                    column.Item().Element(ComposeKodQR);
                    column.Item().Height(10);
                }

                // SEKCJA I - Samoocena
                column.Item().Element(c => ComposeSectionHeader(c, "I", "SAMOOCENA DOSTAWCY - HODOWCY", 
                    "Sekcję wypełnia Hodowca przed wizytą kontrolną - 3 punkty za każde TAK"));
                column.Item().Height(5);
                column.Item().Element(c => ComposeQuestionTable(c, PytaniaSamoocena, _samoocena, 0, 1, 3));
                column.Item().Height(15);

                // SEKCJA II - Lista kontrolna A
                column.Item().Element(c => ComposeSectionHeader(c, "II", "LISTA KONTROLNA - CZĘŚĆ A", 
                    "Wypełnia Hodowca (Pytania 6-10) - 1 punkt za każde TAK"));
                column.Item().Height(5);
                column.Item().Element(c => ComposeQuestionTable(c, PytaniaKontrolnaA, _listaKontrolna, 0, 6, 1));
                column.Item().Height(15);

                // SEKCJA II - Lista kontrolna B
                column.Item().Element(c => ComposeSectionHeader(c, "II", "LISTA KONTROLNA - CZĘŚĆ B", 
                    "Wypełnia Kierowca - Weryfikacja (Pytania 11-15) - 1 punkt za każde TAK"));
                column.Item().Height(5);
                column.Item().Element(c => ComposeQuestionTable(c, PytaniaKontrolnaB, _listaKontrolna, 5, 11, 1));
                column.Item().Height(15);

                // SEKCJA II - Lista kontrolna C
                column.Item().Element(c => ComposeSectionHeader(c, "II", "LISTA KONTROLNA - CZĘŚĆ C", 
                    "Wypełnia Kierowca - Infrastruktura (Pytania 16-20) - 1 punkt za każde TAK"));
                column.Item().Height(5);
                column.Item().Element(c => ComposeQuestionTable(c, PytaniaKontrolnaC, _listaKontrolna, 10, 16, 1));
                
                column.Item().PageBreak();

                // SEKCJA II - Lista kontrolna D
                column.Item().Element(c => ComposeSectionHeader(c, "II", "LISTA KONTROLNA - CZĘŚĆ D", 
                    "Wypełnia Kierowca - Stan ptaków (Pytania 21-25) - 1 punkt za każde TAK"));
                column.Item().Height(5);
                column.Item().Element(c => ComposeQuestionTable(c, PytaniaKontrolnaD, _listaKontrolna, 15, 21, 1));
                column.Item().Height(15);

                // SEKCJA II - Lista kontrolna E
                column.Item().Element(c => ComposeSectionHeader(c, "II", "LISTA KONTROLNA - CZĘŚĆ E", 
                    "Wypełnia Kierowca - Proces załadunku (Pytania 26-30) - 1 punkt za każde TAK"));
                column.Item().Height(5);
                column.Item().Element(c => ComposeQuestionTable(c, PytaniaKontrolnaE, _listaKontrolna, 20, 26, 1));
                column.Item().Height(15);

                // SEKCJA III - Dokumentacja
                column.Item().Element(c => ComposeSectionHeader(c, "III", "DOKUMENTACJA", 
                    "Sprawdzenie kompletności wymaganej dokumentacji"));
                column.Item().Height(5);
                column.Item().Element(ComposeDokumentacja);
                column.Item().Height(20);

                // PODSUMOWANIE
                if (!_czyPustyFormularz)
                {
                    column.Item().Element(ComposeSummary);
                    column.Item().Height(15);

                    // PORÓWNANIE z poprzednią oceną
                    if (_poprzedniaOcena != null)
                    {
                        column.Item().Element(ComposePorownanieZPoprzednia);
                        column.Item().Height(15);
                    }

                    // STATYSTYKI
                    if (_statystyki != null)
                    {
                        column.Item().Element(ComposeStatystyki);
                        column.Item().Height(15);
                    }

                    // REKOMENDACJE
                    column.Item().Element(ComposeRekomendacje);
                    column.Item().Height(15);
                }

                // UWAGI
                column.Item().Element(ComposeUwagi);
                column.Item().Height(20);

                // PODPISY
                column.Item().Element(ComposeSignatures);
            });
        }

        private void ComposeFooter(IContainer container)
        {
            container.BorderTop(1).BorderColor(ColorBorderLight).PaddingTop(5).Row(row =>
            {
                row.RelativeItem().Text(text =>
                {
                    text.Span("Wygenerowano: ").FontSize(7).FontColor(ColorTextLight);
                    text.Span(DateTime.Now.ToString("dd.MM.yyyy HH:mm")).FontSize(7).Bold();
                });

                row.RelativeItem().AlignCenter().Text("Dokument wewnętrzny - Poufne")
                    .FontSize(7).Italic().FontColor(ColorTextLight);

                row.RelativeItem().AlignRight().Text(text =>
                {
                    text.Span("Strona ").FontSize(7).FontColor(ColorTextLight);
                    text.CurrentPageNumber().FontSize(7).Bold();
                    text.Span(" z ").FontSize(7).FontColor(ColorTextLight);
                    text.TotalPages().FontSize(7).Bold();
                });
            });
        }

        #endregion

        #region Komponenty pomocnicze

        /// <summary>
        /// NOWA FUNKCJA: Watermark (DRAFT, KOPIA, ANULOWANO)
        /// </summary>
        private void ComposeWatermark(IContainer container)
        {
            string kolor = _watermark.ToUpper() switch
            {
                "ANULOWANO" => ColorDanger,
                "DRAFT" => ColorWarning,
                "KOPIA" => ColorSecondary,
                _ => ColorTextLight
            };

            string tlo = _watermark.ToUpper() switch
            {
                "ANULOWANO" => ColorDangerBg,
                "DRAFT" => ColorWarningBg,
                "KOPIA" => ColorSecondaryBg,
                _ => ColorBackground
            };

            container.Background(tlo).Border(2).BorderColor(kolor).Padding(8).Row(row =>
            {
                row.RelativeItem().AlignCenter().Text(_watermark.ToUpper())
                    .FontSize(16).Bold().FontColor(kolor);
            });
        }

        /// <summary>
        /// NOWA FUNKCJA: Kod QR z danymi dostawcy
        /// </summary>
        private void ComposeKodQR(IContainer container)
        {
            container.Background(ColorBackground).Border(1).BorderColor(ColorBorder).Padding(10).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Identyfikator dokumentu:").FontSize(8).Bold();
                    col.Item().Text($"DOC-{_dostawcaId}-{_dataOceny:yyyyMMdd}").FontSize(7);
                    col.Item().PaddingTop(3).Text("Zeskanuj kod QR aby zweryfikować autentyczność")
                        .FontSize(7).Italic().FontColor(ColorTextLight);
                });

                row.ConstantItem(80).AlignRight().Column(col =>
                {
                    col.Item().Border(1).BorderColor(ColorBorder).Width(70).Height(70)
                        .AlignCenter().AlignMiddle()
                        .Text("QR").FontSize(20).Bold().FontColor(ColorTextLight);
                    col.Item().AlignCenter().Text("Placeholder").FontSize(6).FontColor(ColorTextLight);
                });
            });
        }

        private void ComposeInstrukcja(IContainer container)
        {
            container.Background(ColorSecondaryBg).Border(2).BorderColor(ColorSecondary)
                .Padding(12).Column(column =>
            {
                column.Item().Text("📋 INSTRUKCJA WYPEŁNIANIA FORMULARZA")
                    .FontSize(11).Bold().FontColor(ColorSecondary);
                
                column.Item().PaddingTop(8).Text(text =>
                {
                    text.Span("1. ").Bold();
                    text.Span("Sekcję I (Pytania 1-5) wypełnia HODOWCA przed rozpoczęciem procedury odbioru ptaków. ");
                    text.Span("Każde TAK = 3 punkty.").Bold().FontColor(ColorPrimary);
                });
                
                column.Item().PaddingTop(3).Text(text =>
                {
                    text.Span("2. ").Bold();
                    text.Span("Sekcję II Część A (Pytania 6-10) wypełnia HODOWCA - dotyczy gospodarstwa i procedur. ");
                    text.Span("Każde TAK = 1 punkt.").Bold().FontColor(ColorSecondary);
                });
                
                column.Item().PaddingTop(3).Text(text =>
                {
                    text.Span("3. ").Bold();
                    text.Span("Sekcję II Część B-E (Pytania 11-30) wypełnia KIEROWCA/ODBIERAJĄCY podczas odbioru ptaków. ");
                    text.Span("Każde TAK = 1 punkt.").Bold().FontColor(ColorSecondary);
                });
                
                column.Item().PaddingTop(3).Text(text =>
                {
                    text.Span("4. ").Bold();
                    text.Span("Zaznacz X w odpowiedniej kolumnie (TAK/NIE). Zaznaczaj wyraźnie!");
                });
                
                column.Item().PaddingTop(3).Text(text =>
                {
                    text.Span("5. ").Bold();
                    text.Span("Po wypełnieniu przekaż formularz do biura w ciągu 24h.");
                });
                
                column.Item().PaddingTop(8).Background(ColorWarningBg).Border(1).BorderColor(ColorWarning)
                    .Padding(6).Text("⚠️ WAŻNE: Pytania 1-5: po 3 pkt | Pytania 6-30: po 1 pkt | Maksymalnie: 40 punktów | Minimum do pozytywnej oceny: 30 pkt")
                    .FontSize(8).Bold().FontColor(ColorWarning);
            });
        }

        private void ComposeSectionHeader(IContainer container, string numerSekcji, string tytul, string opis)
        {
            container.Background(ColorPrimary).Padding(8).Row(row =>
            {
                row.ConstantItem(30).AlignCenter().AlignMiddle().Text(numerSekcji)
                    .FontSize(16).Bold().FontColor(ColorWhite);

                row.RelativeItem().PaddingLeft(10).Column(col =>
                {
                    col.Item().Text(tytul).FontSize(11).Bold().FontColor(ColorWhite);
                    col.Item().Text(opis).FontSize(8).Italic().FontColor(ColorWhite);
                });
            });
        }

        private void ComposeQuestionTable(IContainer container, string[] pytania, bool[] odpowiedzi, 
            int indexStart, int numerPoczatkowy, int wartoscPunktu)
        {
            container.Border(1).BorderColor(ColorBorder).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(30);
                    columns.RelativeColumn(10);
                    columns.ConstantColumn(50);
                    columns.ConstantColumn(50);
                    columns.ConstantColumn(45);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).AlignCenter().Text("Lp.");
                    header.Cell().Element(HeaderCell).Text("Pytanie kontrolne");
                    header.Cell().Element(HeaderCell).AlignCenter().Text("TAK");
                    header.Cell().Element(HeaderCell).AlignCenter().Text("NIE");
                    header.Cell().Element(HeaderCell).AlignCenter().Text("Punkty");
                });

                for (int i = 0; i < pytania.Length; i++)
                {
                    int numerPytania = numerPoczatkowy + i;
                    int indexOdpowiedzi = indexStart + i;
                    bool odpowiedz = odpowiedzi != null && odpowiedzi.Length > indexOdpowiedzi 
                        ? odpowiedzi[indexOdpowiedzi] : false;

                    bool isEvenRow = i % 2 == 0;

                    table.Cell().Element(c => BodyCell(c, isEvenRow)).AlignCenter()
                        .Text($"{numerPytania}.").FontSize(9).Bold().FontColor(ColorTextLight);

                    table.Cell().Element(c => BodyCell(c, isEvenRow)).PaddingLeft(5)
                        .Text(pytania[i]).FontSize(9);

                    table.Cell().Element(c => BodyCell(c, isEvenRow)).AlignCenter().AlignMiddle()
                        .Element(c => DrawCheckbox(c, odpowiedz, true));

                    table.Cell().Element(c => BodyCell(c, isEvenRow)).AlignCenter().AlignMiddle()
                        .Element(c => DrawCheckbox(c, odpowiedz, false));

                    table.Cell().Element(c => BodyCell(c, isEvenRow)).AlignCenter().AlignMiddle()
                        .Text(_czyPustyFormularz ? $"({wartoscPunktu})" : 
                            (odpowiedz ? wartoscPunktu.ToString() : "0"))
                        .FontSize(9).Bold()
                        .FontColor(_czyPustyFormularz ? ColorTextLight : 
                            (odpowiedz ? ColorPrimary : ColorTextLight));
                }
            });
        }

        private void ComposeDokumentacja(IContainer container)
        {
            container.Border(1).BorderColor(ColorBorder).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(30);
                    columns.RelativeColumn(10);
                    columns.ConstantColumn(50);
                    columns.ConstantColumn(50);
                    columns.ConstantColumn(45);
                });

                table.Cell().Element(c => BodyCell(c)).AlignCenter()
                    .Text("31.").FontSize(9).Bold().FontColor(ColorTextLight);

                table.Cell().Element(c => BodyCell(c)).PaddingLeft(5)
                    .Text("Czy do dostawy dostarczono aktualne świadectwo zdrowia ptaków?")
                    .FontSize(9).Bold();

                table.Cell().Element(c => BodyCell(c)).AlignCenter().AlignMiddle()
                    .Element(c => DrawCheckbox(c, _dokumentacja, true));

                table.Cell().Element(c => BodyCell(c)).AlignCenter().AlignMiddle()
                    .Element(c => DrawCheckbox(c, _dokumentacja, false));

                table.Cell().Element(c => BodyCell(c)).AlignCenter().AlignMiddle()
                    .Text("Obowiązkowe")
                    .FontSize(7).Bold().FontColor(ColorDanger);
            });
        }

        private void ComposeSummary(IContainer container)
        {
            string wynikKolor;
            string wynikTekst;
            string wynikTlo;

            if (_punktyRazem >= 30)
            {
                wynikKolor = ColorPrimary;
                wynikTekst = "POZYTYWNA";
                wynikTlo = ColorPrimaryBg;
            }
            else if (_punktyRazem >= 20)
            {
                wynikKolor = ColorWarning;
                wynikTekst = "WARUNKOWO POZYTYWNA";
                wynikTlo = ColorWarningBg;
            }
            else
            {
                wynikKolor = ColorDanger;
                wynikTekst = "NEGATYWNA";
                wynikTlo = ColorDangerBg;
            }

            container.Border(2).BorderColor(wynikKolor).Column(column =>
            {
                column.Item().Background(wynikKolor).Padding(10)
                    .Text("📊 PODSUMOWANIE OCENY").FontSize(12).Bold().FontColor(ColorWhite);

                column.Item().Background(wynikTlo).Padding(15).Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Punkty za pytania 1-5 (po 3 pkt):")
                            .FontSize(10);
                        row.ConstantItem(80).AlignRight()
                            .Text($"{_punkty1_5} / 15").FontSize(10).Bold();
                    });

                    col.Item().PaddingTop(5).Row(row =>
                    {
                        row.RelativeItem().Text("Punkty za pytania 6-30 (po 1 pkt):")
                            .FontSize(10);
                        row.ConstantItem(80).AlignRight()
                            .Text($"{_punkty6_20} / 25").FontSize(10).Bold();
                    });

                    col.Item().PaddingTop(10).LineHorizontal(2).LineColor(wynikKolor);

                    col.Item().PaddingTop(10).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("SUMA PUNKTÓW:").FontSize(12).Bold();
                            c.Item().Text($"Ocena: {wynikTekst}").FontSize(9).FontColor(wynikKolor).Bold();
                        });
                        
                        row.ConstantItem(100).AlignRight().AlignMiddle()
                            .Text($"{_punktyRazem} / 40").FontSize(20).Bold().FontColor(wynikKolor);
                    });
                });

                column.Item().Background(ColorBackground).Padding(10).Column(col =>
                {
                    col.Item().Text("SKALA OCEN:").FontSize(8).Bold().FontColor(ColorTextLight);
                    col.Item().PaddingTop(3).Text("• 30-40 pkt: Ocena POZYTYWNA - Dostawca spełnia wszystkie wymagania")
                        .FontSize(7).FontColor(ColorPrimary);
                    col.Item().Text("• 20-29 pkt: Ocena WARUNKOWO POZYTYWNA - Wymagane działania korygujące w ciągu 30 dni")
                        .FontSize(7).FontColor(ColorWarning);
                    col.Item().Text("• 0-19 pkt: Ocena NEGATYWNA - Dostawca nie spełnia wymagań, zawieszenie dostaw")
                        .FontSize(7).FontColor(ColorDanger);
                });
            });
        }

        /// <summary>
        /// NOWA FUNKCJA: Porównanie z poprzednią oceną
        /// </summary>
        private void ComposePorownanieZPoprzednia(IContainer container)
        {
            if (_poprzedniaOcena == null) return;

            int roznica = _punktyRazem - _poprzedniaOcena.PunktyRazem;
            string kierunek = roznica > 0 ? "↑" : (roznica < 0 ? "↓" : "→");
            string kolorRoznicy = roznica > 0 ? ColorPrimary : (roznica < 0 ? ColorDanger : ColorTextLight);

            container.Background(ColorBackground).Border(1).BorderColor(ColorBorder).Padding(12).Column(column =>
            {
                column.Item().Text("📈 PORÓWNANIE Z POPRZEDNIĄ OCENĄ")
                    .FontSize(10).Bold().FontColor(ColorSecondary);

                column.Item().PaddingTop(8).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text($"Poprzednia ocena: {_poprzedniaOcena.DataOceny:dd.MM.yyyy}")
                            .FontSize(8).FontColor(ColorTextLight);
                        col.Item().Text($"Punkty: {_poprzedniaOcena.PunktyRazem}/40")
                            .FontSize(8);
                    });

                    row.ConstantItem(100).AlignRight().Column(col =>
                    {
                        col.Item().AlignRight().Text($"Zmiana: {kierunek} {Math.Abs(roznica)} pkt")
                            .FontSize(10).Bold().FontColor(kolorRoznicy);
                        col.Item().AlignRight().Text(roznica > 0 ? "Poprawa" : (roznica < 0 ? "Pogorszenie" : "Bez zmian"))
                            .FontSize(8).FontColor(kolorRoznicy);
                    });
                });

                if (roznica < 0)
                {
                    column.Item().PaddingTop(5).Background(ColorDangerBg).Border(1).BorderColor(ColorDanger)
                        .Padding(5).Text("⚠️ Uwaga: Wynik gorszy niż poprzednio. Wymagana analiza przyczyn.")
                        .FontSize(8).FontColor(ColorDanger);
                }
            });
        }

        /// <summary>
        /// NOWA FUNKCJA: Statystyki dostawcy
        /// </summary>
        private void ComposeStatystyki(IContainer container)
        {
            if (_statystyki == null) return;

            container.Background(ColorSecondaryBg).Border(1).BorderColor(ColorSecondary).Padding(12).Column(column =>
            {
                column.Item().Text("📊 STATYSTYKI DOSTAWCY (ostatnie 12 miesięcy)")
                    .FontSize(10).Bold().FontColor(ColorSecondary);

                column.Item().PaddingTop(8).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text($"Liczba ocen: {_statystyki.LiczbaOcen}")
                            .FontSize(8);
                        col.Item().Text($"Średnia punktów: {_statystyki.SredniaPunktow:F1}/40")
                            .FontSize(8).Bold();
                    });

                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text($"Najwyższa: {_statystyki.NajwyzszaOcena}/40")
                            .FontSize(8).FontColor(ColorPrimary);
                        col.Item().Text($"Najniższa: {_statystyki.NajnizszaOcena}/40")
                            .FontSize(8).FontColor(ColorDanger);
                    });

                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text($"Trend: {_statystyki.Trend}")
                            .FontSize(8).Bold()
                            .FontColor(_statystyki.Trend.Contains("wzrostowy") ? ColorPrimary : ColorDanger);
                        col.Item().Text($"Stabilność: {_statystyki.Stabilnosc}")
                            .FontSize(8);
                    });
                });
            });
        }

        /// <summary>
        /// NOWA FUNKCJA: Automatyczne rekomendacje
        /// </summary>
        private void ComposeRekomendacje(IContainer container)
        {
            var rekomendacje = GenerujRekomendacje();
            if (rekomendacje.Count == 0) return;

            container.Background(ColorWarningBg).Border(1).BorderColor(ColorWarning).Padding(12).Column(column =>
            {
                column.Item().Text("💡 REKOMENDACJE I DZIAŁANIA NAPRAWCZE")
                    .FontSize(10).Bold().FontColor(ColorWarning);

                column.Item().PaddingTop(5);

                foreach (var rekomendacja in rekomendacje)
                {
                    column.Item().PaddingTop(3).Row(row =>
                    {
                        row.ConstantItem(15).Text("•").FontColor(ColorWarning);
                        row.RelativeItem().Text(rekomendacja).FontSize(8);
                    });
                }
            });
        }

        private System.Collections.Generic.List<string> GenerujRekomendacje()
        {
            var rekomendacje = new System.Collections.Generic.List<string>();

            if (_czyPustyFormularz) return rekomendacje;

            // Analiza wyników i generowanie rekomendacji
            if (_punktyRazem < 20)
            {
                rekomendacje.Add("KRYTYCZNE: Natychmiastowe wstrzymanie dostaw do czasu wdrożenia działań naprawczych.");
                rekomendacje.Add("Wymagany audyt wewnętrzny w ciągu 7 dni.");
            }
            else if (_punktyRazem < 30)
            {
                rekomendacje.Add("Wymagane wdrożenie działań korygujących w ciągu 30 dni.");
                rekomendacje.Add("Ponowna kontrola zaplanowana za miesiąc.");
            }

            // Analiza konkretnych sekcji
            if (_punkty1_5 < 9)
            {
                rekomendacje.Add("Problemy w podstawowych wymogach gospodarstwa (Sekcja I). Priorytet: szkolenie hodowcy.");
            }

            if (_punkty6_20 < 15)
            {
                rekomendacje.Add("Niewystarczająca infrastruktura i procedury (Sekcja II). Zalecane inwestycje.");
            }

            if (!_dokumentacja)
            {
                rekomendacje.Add("BRAK DOKUMENTACJI - dostawa może być odrzucona. Natychmiast dostarczyć świadectwo zdrowia.");
            }

            // Pozytywna ocena
            if (_punktyRazem >= 35)
            {
                rekomendacje.Add("Dostawca wzorowy! Utrzymać obecny poziom. Rozważyć zwiększenie wolumenu dostaw.");
            }

            return rekomendacje;
        }

        private void ComposeUwagi(IContainer container)
        {
            container.Column(col =>
            {
                col.Item().Background(ColorBackground).Padding(8)
                    .Text("📝 UWAGI I ZALECENIA:").FontSize(10).Bold().FontColor(ColorPrimary);

                if (_czyPustyFormularz)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        col.Item().Border(1).BorderColor(ColorBorderLight).Height(20);
                        if (i < 3) col.Item().Height(3);
                    }
                }
                else
                {
                    col.Item().Border(1).BorderColor(ColorBorder).Background(ColorWhite)
                        .MinHeight(60).Padding(10)
                        .Text(string.IsNullOrWhiteSpace(_uwagi) ? "Brak uwag." : _uwagi)
                        .FontSize(9);
                }
            });
        }

        private void ComposeSignatures(IContainer container)
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Border(1).BorderColor(ColorBorder).Height(60);
                    column.Item().PaddingTop(5).AlignCenter()
                        .Text("Podpis Hodowcy").FontSize(8).Bold();
                    column.Item().AlignCenter()
                        .Text("(potwierdzenie poprawności danych)").FontSize(7).Italic().FontColor(ColorTextLight);
                });

                row.ConstantItem(40);

                row.RelativeItem().Column(column =>
                {
                    column.Item().Border(1).BorderColor(ColorBorder).Height(60);
                    column.Item().PaddingTop(5).AlignCenter()
                        .Text("Podpis Kierowcy / Odbierającego").FontSize(8).Bold();
                    column.Item().AlignCenter()
                        .Text("(potwierdzenie przeprowadzonej kontroli)").FontSize(7).Italic().FontColor(ColorTextLight);
                });
            });
        }

        #endregion

        #region Pomocnicze metody stylizacji

        private IContainer HeaderCell(IContainer container)
        {
            return container
                .Background(ColorPrimary)
                .Border(1)
                .BorderColor(ColorWhite)
                .Padding(6)
                .DefaultTextStyle(x => x.FontColor(ColorWhite).FontSize(9).Bold());
        }

        private IContainer BodyCell(IContainer container, bool isEvenRow = false)
        {
            return container
                .Background(isEvenRow ? ColorWhite : "#F5F5F5")
                .Border(1)
                .BorderColor(ColorBorderLight)
                .PaddingVertical(6)
                .PaddingHorizontal(4);
        }

        private void DrawCheckbox(IContainer container, bool isChecked, bool isYesColumn)
        {
            if (_czyPustyFormularz)
            {
                container.Width(16).Height(16).Border(2).BorderColor(ColorText);
                return;
            }

            bool shouldMark = isYesColumn ? isChecked : !isChecked;

            if (shouldMark)
            {
                container.Width(16).Height(16)
                    .Border(2).BorderColor(ColorPrimary)
                    .Background(ColorPrimaryBg)
                    .AlignCenter().AlignMiddle()
                    .Text("✓").FontSize(12).Bold().FontColor(ColorPrimary);
            }
            else
            {
                container.Width(16).Height(16)
                    .Border(2).BorderColor(ColorBorderLight);
            }
        }

        #endregion
    }

    #region Klasy pomocnicze dla nowych funkcji

    /// <summary>
    /// Dane poprzedniej oceny do porównania
    /// </summary>
    public class OcenaPorownanieData
    {
        public DateTime DataOceny { get; set; }
        public int PunktyRazem { get; set; }
        public string Ocena { get; set; }
    }

    /// <summary>
    /// Statystyki dostawcy z historii
    /// </summary>
    public class StatystykiDostawcy
    {
        public int LiczbaOcen { get; set; }
        public double SredniaPunktow { get; set; }
        public int NajwyzszaOcena { get; set; }
        public int NajnizszaOcena { get; set; }
        public string Trend { get; set; }  // "wzrostowy", "spadkowy", "stabilny"
        public string Stabilnosc { get; set; }  // "wysoka", "średnia", "niska"
    }

    #endregion
}
