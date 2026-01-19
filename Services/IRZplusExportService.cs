using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Kalendarz1.Models.IRZplus;

namespace Kalendarz1.Services
{
    /// <summary>
    /// Serwis eksportu danych do formatow CSV i XML zgodnych z portalem IRZplus.
    ///
    /// WAZNE: Portal IRZplus oczekuje:
    /// - JEDNEGO zgloszenia (naglowek z gatunkiem, numerem rzezni, numerem partii uboju)
    /// - WIELU pozycji (kazdy transport/aut to osobna pozycja)
    ///
    /// ZASADA GLOWNA: 1 TRANSPORT = 1 PLIK CSV = 1 ZGLOSZENIE ZURD W PORTALU
    ///
    /// Format CSV:
    /// - Separator kolumn: srednik (;)
    /// - Masa: LICZBA CALKOWITA bez separatorow tysiecy (13851 nie "13 851,00")
    /// - Daty: format DD-MM-RRRR (12-01-2026)
    /// - Data kupna/wwozu: NIE MOZE BYC PUSTA - taka sama jak data zdarzenia
    /// </summary>
    public class IRZplusExportService
    {
        // ===== STALE DANE RZEZNI - NIE ZMIENIAJ! =====
        private const string NUMER_RZEZNI = "039806095-001";
        private const string NUMER_PRODUCENTA = "039806095";
        private const string GATUNEK = "kury";

        // Polskie nazwy dni tygodnia
        private static readonly string[] _polskieDniTygodnia = new[]
        {
            "niedziela", "poniedzialek", "wtorek", "sroda", "czwartek", "piatek", "sobota"
        };

        private readonly string _exportPath;
        private readonly CultureInfo _polishCulture;

        public IRZplusExportService(string exportPath = null)
        {
            _exportPath = exportPath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "IRZplus_Export");

            Directory.CreateDirectory(_exportPath);

            // Polski format - przecinek jako separator dziesietny
            _polishCulture = new CultureInfo("pl-PL");
        }

        #region ========== STRUKTURA FOLDEROW I NAZEWNICTWO ==========

        /// <summary>
        /// Tworzy sciezke folderu wg struktury: Rok/Miesiac/Dzien-DzienTygodnia
        /// np. "2026/01/16-czwartek"
        /// </summary>
        public string UtworzSciezkeFolderu(DateTime data, string bazowaScieszka = null)
        {
            var baza = bazowaScieszka ?? _exportPath;

            // Numer dnia tygodnia (0=niedziela, 1=poniedzialek, ...)
            int numerDnia = (int)data.DayOfWeek;
            // Zamieniamy na numer gdzie 1=poniedzialek, 7=niedziela
            int numerDniaPolski = numerDnia == 0 ? 7 : numerDnia;

            string nazwaFolderuDnia = $"{data.Day}-{_polskieDniTygodnia[numerDnia]}";

            var pelnaSciezka = Path.Combine(
                baza,
                data.Year.ToString(),
                data.Month.ToString("00"),
                nazwaFolderuDnia
            );

            if (!Directory.Exists(pelnaSciezka))
                Directory.CreateDirectory(pelnaSciezka);

            return pelnaSciezka;
        }

        /// <summary>
        /// Generuje nazwe pliku wg wzoru: kolejnosc-NazwaHodowcy-pelnaData
        /// np. "1-Kowalski_Jan-16-01-2026.csv"
        /// </summary>
        public string GenerujNazwePliku(int kolejnosc, string nazwaHodowcy, DateTime data, string rozszerzenie = "csv")
        {
            // Usun niedozwolone znaki z nazwy hodowcy
            var bezpiecznaNazwa = UsunNiedozwoloneZnaki(nazwaHodowcy);

            // Ogranicz dlugosc nazwy
            if (bezpiecznaNazwa.Length > 50)
                bezpiecznaNazwa = bezpiecznaNazwa.Substring(0, 50);

            return $"{kolejnosc}-{bezpiecznaNazwa}-{data:dd-MM-yyyy}.{rozszerzenie}";
        }

        /// <summary>
        /// Usuwa niedozwolone znaki z nazwy pliku
        /// </summary>
        private string UsunNiedozwoloneZnaki(string nazwa)
        {
            if (string.IsNullOrEmpty(nazwa))
                return "NIEZNANY";

            // Zamien polskie znaki na ASCII
            var zamiana = new Dictionary<char, char>
            {
                {'ą', 'a'}, {'ć', 'c'}, {'ę', 'e'}, {'ł', 'l'}, {'ń', 'n'},
                {'ó', 'o'}, {'ś', 's'}, {'ź', 'z'}, {'ż', 'z'},
                {'Ą', 'A'}, {'Ć', 'C'}, {'Ę', 'E'}, {'Ł', 'L'}, {'Ń', 'N'},
                {'Ó', 'O'}, {'Ś', 'S'}, {'Ź', 'Z'}, {'Ż', 'Z'}
            };

            var wynik = new StringBuilder();
            foreach (var c in nazwa)
            {
                if (zamiana.ContainsKey(c))
                    wynik.Append(zamiana[c]);
                else if (char.IsLetterOrDigit(c) || c == '-' || c == '_')
                    wynik.Append(c);
                else if (c == ' ')
                    wynik.Append('_');
                // pomijamy inne znaki
            }

            return wynik.ToString().Trim('_');
        }

        #endregion

        #region ========== ZURD - Zgloszenie Uboju Drobiu w Rzezni ==========

        /// <summary>
        /// Eksportuje ZURD do pliku CSV zgodnego z importem w portalu IRZplus.
        /// WAZNE: Plik CSV jest CZYSTY - tylko naglowek i dane, BEZ komentarzy!
        /// Portal IRZplus nie obsluguje komentarzy w pliku CSV.
        /// </summary>
        public ExportResult EksportujZURD_CSV(ZgloszenieZURD zgloszenie)
        {
            try
            {
                var fileName = $"ZURD_{zgloszenie.DataUboju:yyyyMMdd}_{DateTime.Now:HHmmss}.csv";
                var filePath = Path.Combine(_exportPath, fileName);

                var csv = new StringBuilder();

                // === POZYCJE (BEZ NAGLOWKA, BEZ Lp!) ===
                // Format CSV zgodny z oficjalnym szablonem ARiMR (13 kolumn):
                // 1. Numer identyfikacyjny/numer partii (puste lub numer rzezni)
                // 2. Liczba sztuk drobiu
                // 3. Masa drobiu poddanego ubojowi (liczba calkowita)
                // 4. Typ zdarzenia = "ZURDUR"
                // 5. Data zdarzenia (RRRR-MM-DD)
                // 6. Kraj wwozu (puste dla PL)
                // 7. Data kupna/wwozu (RRRR-MM-DD)
                // 8. Przyjete z dzialalnosci (numer siedliska hodowcy)
                // 9. WNI przewoznika (puste lub wartosc)
                // 10. Srodek transportu (puste lub "SA"/"PR"/"NA")
                // 11. Numer rejestracyjny srodka transportu (puste lub numer)
                // 12. Transport wlasny ("TAK" lub "NIE")
                // 13. Uboj rytualny ("TAK" lub "NIE")
                foreach (var poz in zgloszenie.Pozycje.OrderBy(p => p.Lp))
                {
                    var dataZdarzeniaStr = poz.DataZdarzenia.ToString("yyyy-MM-dd");
                    var masaStr = ((int)Math.Round(poz.MasaKg)).ToString(CultureInfo.InvariantCulture);
                    var numerSiedliska = NormalizujNumerSiedliska(poz.PrzyjeteZDzialalnosci);

                    csv.AppendLine(string.Join(";", new[]
                    {
                        zgloszenie.NumerPartiiUboju,                  // Kol 1: Numer partii uboju (np. 26011601)
                        poz.LiczbaSztuk.ToString(),                   // Kol 2: Liczba sztuk
                        masaStr,                                      // Kol 3: Masa (liczba calkowita)
                        "ZURDUR",                                     // Kol 4: Typ zdarzenia
                        dataZdarzeniaStr,                             // Kol 5: Data zdarzenia
                        "",                                           // Kol 6: Kraj wwozu (puste dla PL)
                        dataZdarzeniaStr,                             // Kol 7: Data kupna/wwozu
                        numerSiedliska,                               // Kol 8: Przyjete z dzialalnosci
                        "",                                           // Kol 9: WNI przewoznika (puste)
                        "",                                           // Kol 10: Srodek transportu (puste)
                        "",                                           // Kol 11: Nr rejestracyjny (puste)
                        "NIE",                                        // Kol 12: Transport wlasny
                        poz.UbojRytualny ? "TAK" : "NIE"              // Kol 13: Uboj rytualny
                    }));
                }

                // Zapisz z kodowaniem UTF-8 z BOM
                File.WriteAllText(filePath, csv.ToString(), new UTF8Encoding(true));

                // Zapisz osobny plik z instrukcja
                ZapiszInstrukcjeImportu(zgloszenie, filePath);

                return new ExportResult
                {
                    Success = true,
                    FilePath = filePath,
                    FileName = fileName,
                    Message = $"Wyeksportowano {zgloszenie.LiczbaPozycji} pozycji do pliku CSV"
                };
            }
            catch (Exception ex)
            {
                return new ExportResult
                {
                    Success = false,
                    Message = $"Blad eksportu CSV: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Normalizuje numer siedliska do formatu NNNNNNNNN-NNN.
        /// Usuwa podwojne "-001" jesli wystepuje (np. "068736945-001-001" -> "068736945-001").
        /// </summary>
        private string NormalizujNumerSiedliska(string numer)
        {
            if (string.IsNullOrEmpty(numer))
                return "";

            // Usun biale znaki
            numer = numer.Trim();

            // Sprawdz czy ma za duzo segmentow (np. 068736945-001-001 lub 068736945-001-001-001)
            var parts = numer.Split('-');
            if (parts.Length > 2)
            {
                // Zostaw tylko pierwsze dwa segmenty: NNNNNNNNN-NNN
                return $"{parts[0]}-{parts[1]}";
            }

            return numer;
        }

        /// <summary>
        /// Zapisuje plik z instrukcja importu (osobny plik TXT).
        /// </summary>
        private void ZapiszInstrukcjeImportu(ZgloszenieZURD zgloszenie, string csvFilePath)
        {
            try
            {
                var instrukcjaPath = Path.ChangeExtension(csvFilePath, ".INSTRUKCJA.txt");
                var sb = new StringBuilder();

                sb.AppendLine("╔══════════════════════════════════════════════════════════════════════════╗");
                sb.AppendLine("║           INSTRUKCJA IMPORTU DO PORTALU IRZPLUS                          ║");
                sb.AppendLine("╚══════════════════════════════════════════════════════════════════════════╝");
                sb.AppendLine();
                sb.AppendLine("PRZED IMPORTEM WYPELNIJ RECZNIE W PORTALU:");
                sb.AppendLine("─────────────────────────────────────────────────────────────────────────────");
                sb.AppendLine($"  Gatunek:              {GATUNEK}");
                sb.AppendLine($"  Numer rzeźni:         {NUMER_RZEZNI}");
                sb.AppendLine($"  Numer partii uboju:   {zgloszenie.NumerPartiiUboju}");
                sb.AppendLine("─────────────────────────────────────────────────────────────────────────────");
                sb.AppendLine();
                sb.AppendLine($"Data uboju: {zgloszenie.DataUboju:dd.MM.yyyy}");
                sb.AppendLine($"Liczba pozycji: {zgloszenie.LiczbaPozycji}");
                sb.AppendLine($"Suma sztuk: {zgloszenie.SumaLiczbaSztuk:N0}");
                sb.AppendLine($"Suma masa: {zgloszenie.SumaMasaKg:N0} kg");
                sb.AppendLine();
                sb.AppendLine($"Plik CSV: {Path.GetFileName(csvFilePath)}");
                sb.AppendLine($"Wygenerowano: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

                File.WriteAllText(instrukcjaPath, sb.ToString(), new UTF8Encoding(true));
            }
            catch
            {
                // Ignoruj bledy zapisu instrukcji
            }
        }

        /// <summary>
        /// Eksportuje ZURD do pliku XML zgodnego z API IRZplus
        /// </summary>
        public ExportResult EksportujZURD_XML(ZgloszenieZURD zgloszenie)
        {
            try
            {
                var fileName = $"ZURD_{zgloszenie.DataUboju:yyyy-MM-dd}_{DateTime.Now:HHmmss}.xml";
                var filePath = Path.Combine(_exportPath, fileName);

                var xml = new XDocument(
                    new XDeclaration("1.0", "UTF-8", null),
                    new XComment(" ZURD - Zgloszenie Uboju Drobiu w Rzezni "),
                    new XComment($" Wygenerowano: {DateTime.Now:yyyy-MM-dd HH:mm:ss} "),
                    new XComment($" Pozycji: {zgloszenie.LiczbaPozycji}, Sztuk: {zgloszenie.SumaLiczbaSztuk}, Masa: {zgloszenie.SumaMasaKg:N2} kg "),
                    new XElement("DyspozycjaZURD",
                        new XElement("numerProducenta", zgloszenie.NumerProducenta),
                        new XElement("zgloszenie",
                            new XElement("numerRzezni", zgloszenie.NumerRzezni),
                            new XElement("numerPartiiUboju", zgloszenie.NumerPartiiUboju),
                            new XElement("gatunek",
                                new XElement("kod", zgloszenie.Gatunek)
                            ),
                            new XElement("pozycje",
                                zgloszenie.Pozycje.OrderBy(p => p.Lp).Select(poz =>
                                    new XElement("pozycja",
                                        new XElement("lp", poz.Lp),
                                        new XElement("numerIdenPartiiDrobiu", poz.NumerPartiiDrobiu),
                                        new XElement("liczbaDrobiu", poz.LiczbaSztuk),
                                        new XElement("masaDrobiu", poz.MasaKg.ToString("F2", CultureInfo.InvariantCulture)),
                                        new XElement("typZdarzenia",
                                            new XElement("kod", poz.TypZdarzenia ?? "ZURDUR")
                                        ),
                                        new XElement("dataZdarzenia", poz.DataZdarzenia.ToString("yyyy-MM-dd")),
                                        !string.IsNullOrEmpty(poz.KrajWwozu)
                                            ? new XElement("krajWwozu", poz.KrajWwozu)
                                            : null,
                                        poz.DataKupnaWwozu.HasValue
                                            ? new XElement("dataKupnaWwozu", poz.DataKupnaWwozu.Value.ToString("yyyy-MM-dd"))
                                            : null,
                                        new XElement("przyjeteZDzialalnosci", poz.PrzyjeteZDzialalnosci),
                                        new XElement("ubojRytualny", poz.UbojRytualny.ToString().ToLower())
                                    )
                                )
                            )
                        )
                    )
                );

                xml.Save(filePath);

                return new ExportResult
                {
                    Success = true,
                    FilePath = filePath,
                    FileName = fileName,
                    Message = $"Wyeksportowano {zgloszenie.LiczbaPozycji} pozycji do pliku XML"
                };
            }
            catch (Exception ex)
            {
                return new ExportResult
                {
                    Success = false,
                    Message = $"Blad eksportu XML: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Eksportuje POJEDYNCZY transport do pliku CSV zgodnego z portalem IRZplus.
        /// ZASADA: 1 TRANSPORT = 1 PLIK CSV = 1 ZGLOSZENIE ZURD
        ///
        /// Generuje rowniez plik INSTRUKCJA.txt z opisem jak importowac do portalu.
        /// </summary>
        /// <param name="dataUboju">Data uboju/zdarzenia</param>
        /// <param name="numerSiedliskaHodowcy">Numer siedliska hodowcy (np. 068736945-001)</param>
        /// <param name="liczbaSztuk">Liczba sztuk drobiu</param>
        /// <param name="masaKg">Masa drobiu w kg</param>
        /// <param name="nazwaHodowcy">Opcjonalna nazwa hodowcy (do instrukcji)</param>
        /// <param name="numerPartiiWewnetrzny">Opcjonalny numer partii wewnetrzny (do nazwy pliku)</param>
        /// <returns>Wynik eksportu z informacja o utworzonych plikach</returns>
        public ExportResult EksportujPojedynczyTransport_CSV(
            DateTime dataUboju,
            string numerSiedliskaHodowcy,
            int liczbaSztuk,
            decimal masaKg,
            string nazwaHodowcy = null,
            string numerPartiiWewnetrzny = null)
        {
            // === WALIDACJA DANYCH ===
            if (liczbaSztuk <= 0)
            {
                return new ExportResult
                {
                    Success = false,
                    Message = $"Blad: Liczba sztuk musi byc wieksza od 0. Otrzymano: {liczbaSztuk}" +
                              (nazwaHodowcy != null ? $" (Hodowca: {nazwaHodowcy})" : "")
                };
            }

            if (masaKg <= 0)
            {
                return new ExportResult
                {
                    Success = false,
                    Message = $"Blad: Masa musi byc wieksza od 0. Otrzymano: {masaKg}" +
                              (nazwaHodowcy != null ? $" (Hodowca: {nazwaHodowcy})" : "")
                };
            }

            if (string.IsNullOrWhiteSpace(numerSiedliskaHodowcy))
            {
                return new ExportResult
                {
                    Success = false,
                    Message = $"Blad: Brak numeru siedliska hodowcy (pole 'Przyjete z dzialalnosci')." +
                              (nazwaHodowcy != null ? $" (Hodowca: {nazwaHodowcy})" : "")
                };
            }

            // Walidacja formatu numeru siedliska (powinien byc NNNNNNNNN-NNN)
            if (!numerSiedliskaHodowcy.Contains("-") || numerSiedliskaHodowcy.Length < 10)
            {
                return new ExportResult
                {
                    Success = false,
                    Message = $"Blad: Nieprawidlowy format numeru siedliska: '{numerSiedliskaHodowcy}'. " +
                              $"Oczekiwany format: NNNNNNNNN-NNN (np. 068736945-001)" +
                              (nazwaHodowcy != null ? $" (Hodowca: {nazwaHodowcy})" : "")
                };
            }
            // === KONIEC WALIDACJI ===

            try
            {
                // Normalizuj numer siedliska (usun podwojne -001 jesli wystepuje)
                numerSiedliskaHodowcy = NormalizujNumerSiedliska(numerSiedliskaHodowcy);

                // Generuj numer partii uboju w formacie RRDDDNNN
                var numerPartiiUboju = ZgloszenieZURD.GenerujNumerPartiiUboju(dataUboju);

                // Usun myslniki z numeru siedliska dla nazwy pliku
                var numerSiedliskaBezMyslnikow = numerSiedliskaHodowcy.Replace("-", "");

                // Nazwa pliku: ZURD_RRRRMMDD_NUMERSIEDLISKA_HHMMSS.csv
                var timestamp = DateTime.Now.ToString("HHmmss");
                var fileName = $"ZURD_{dataUboju:yyyyMMdd}_{numerSiedliskaBezMyslnikow}_{timestamp}.csv";
                var filePath = Path.Combine(_exportPath, fileName);

                // Nazwa pliku instrukcji
                var instrukcjaFileName = $"INSTRUKCJA_{dataUboju:yyyyMMdd}_{numerSiedliskaBezMyslnikow}_{timestamp}.txt";
                var instrukcjaFilePath = Path.Combine(_exportPath, instrukcjaFileName);

                // Data zdarzenia w formacie RRRR-MM-DD (oficjalny format ARiMR)
                var dataZdarzeniaStr = dataUboju.ToString("yyyy-MM-dd");

                // Masa jako liczba calkowita BEZ separatorow
                var masaStr = ((int)Math.Round(masaKg)).ToString(CultureInfo.InvariantCulture);

                // === BUDUJ CSV (BEZ NAGLOWKA, BEZ Lp!) ===
                // Format CSV zgodny z oficjalnym szablonem ARiMR (13 kolumn):
                // 1. Numer identyfikacyjny/numer partii (puste lub numer rzezni)
                // 2. Liczba sztuk drobiu
                // 3. Masa drobiu poddanego ubojowi (liczba calkowita)
                // 4. Typ zdarzenia = "ZURDUR"
                // 5. Data zdarzenia (RRRR-MM-DD)
                // 6. Kraj wwozu (puste dla PL)
                // 7. Data kupna/wwozu (RRRR-MM-DD)
                // 8. Przyjete z dzialalnosci (numer siedliska hodowcy)
                // 9. WNI przewoznika (puste lub wartosc)
                // 10. Srodek transportu (puste lub "SA"/"PR"/"NA")
                // 11. Numer rejestracyjny srodka transportu (puste lub numer)
                // 12. Transport wlasny ("TAK" lub "NIE")
                // 13. Uboj rytualny ("TAK" lub "NIE")
                var csv = new StringBuilder();

                csv.AppendLine(string.Join(";", new[]
                {
                    numerPartiiUboju,                   // Kol 1: Numer partii uboju (np. 26011601)
                    liczbaSztuk.ToString(),             // Kol 2: Liczba sztuk
                    masaStr,                            // Kol 3: Masa (liczba calkowita)
                    "ZURDUR",                           // Kol 4: Typ zdarzenia
                    dataZdarzeniaStr,                   // Kol 5: Data zdarzenia
                    "",                                 // Kol 6: Kraj wwozu (puste dla PL)
                    dataZdarzeniaStr,                   // Kol 7: Data kupna/wwozu
                    numerSiedliskaHodowcy,              // Kol 8: Przyjete z dzialalnosci
                    "",                                 // Kol 9: WNI przewoznika (puste)
                    "",                                 // Kol 10: Srodek transportu (puste)
                    "",                                 // Kol 11: Nr rejestracyjny (puste)
                    "NIE",                              // Kol 12: Transport wlasny
                    "NIE"                               // Kol 13: Uboj rytualny
                }));

                // Zapisz CSV z UTF-8 BOM
                File.WriteAllText(filePath, csv.ToString(), new UTF8Encoding(true));

                // === BUDUJ INSTRUKCJE ===
                var instrukcja = new StringBuilder();

                instrukcja.AppendLine("╔════════════════════════════════════════════════════════════════════════════╗");
                instrukcja.AppendLine("║                                                                            ║");
                instrukcja.AppendLine("║   ██╗    ██╗ █████╗ ██████╗ ███╗   ██╗██╗███╗   ██╗ ██████╗ ██╗            ║");
                instrukcja.AppendLine("║   ██║    ██║██╔══██╗██╔══██╗████╗  ██║██║████╗  ██║██╔════╝ ██║            ║");
                instrukcja.AppendLine("║   ██║ █╗ ██║███████║██████╔╝██╔██╗ ██║██║██╔██╗ ██║██║  ███╗██║            ║");
                instrukcja.AppendLine("║   ██║███╗██║██╔══██║██╔══██╗██║╚██╗██║██║██║╚██╗██║██║   ██║╚═╝            ║");
                instrukcja.AppendLine("║   ╚███╔███╔╝██║  ██║██║  ██║██║ ╚████║██║██║ ╚████║╚██████╔╝██╗            ║");
                instrukcja.AppendLine("║    ╚══╝╚══╝ ╚═╝  ╚═╝╚═╝  ╚═╝╚═╝  ╚═══╝╚═╝╚═╝  ╚═══╝ ╚═════╝ ╚═╝            ║");
                instrukcja.AppendLine("║                                                                            ║");
                instrukcja.AppendLine("╠════════════════════════════════════════════════════════════════════════════╣");
                instrukcja.AppendLine("║                                                                            ║");
                instrukcja.AppendLine("║   NAGLOWEK ZGLOSZENIA NIE IMPORTUJE SIE Z PLIKU CSV!                       ║");
                instrukcja.AppendLine("║                                                                            ║");
                instrukcja.AppendLine("║   PRZED kliknieciem 'Wczytaj dane z pliku CSV' musisz RECZNIE              ║");
                instrukcja.AppendLine("║   wypelnic w portalu IRZplus nastepujace pola:                             ║");
                instrukcja.AppendLine("║                                                                            ║");
                instrukcja.AppendLine("║   ┌───────────────────────────────────────────────────────────────────┐    ║");
                instrukcja.AppendLine($"║   │  1. Gatunek:              {GATUNEK,-38}│    ║");
                instrukcja.AppendLine($"║   │  2. Numer rzezni:         {NUMER_RZEZNI,-38}│    ║");
                instrukcja.AppendLine($"║   │  3. Numer partii uboju:   {numerPartiiUboju,-38}│    ║");
                instrukcja.AppendLine("║   └───────────────────────────────────────────────────────────────────┘    ║");
                instrukcja.AppendLine("║                                                                            ║");
                instrukcja.AppendLine("║   Dopiero PO wypelnieniu tych pol mozesz zaimportowac plik CSV!            ║");
                instrukcja.AppendLine("║                                                                            ║");
                instrukcja.AppendLine("╚════════════════════════════════════════════════════════════════════════════╝");
                instrukcja.AppendLine();
                instrukcja.AppendLine();

                instrukcja.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
                instrukcja.AppendLine("                    INSTRUKCJA IMPORTU DO PORTALU IRZPLUS");
                instrukcja.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
                instrukcja.AppendLine();
                instrukcja.AppendLine($"Data uboju:              {dataUboju:dd.MM.yyyy} ({dataUboju:dddd})");
                instrukcja.AppendLine($"Plik CSV:                {fileName}");
                if (!string.IsNullOrEmpty(nazwaHodowcy))
                    instrukcja.AppendLine($"Hodowca:                 {nazwaHodowcy}");
                instrukcja.AppendLine($"Numer siedliska:         {numerSiedliskaHodowcy}");
                instrukcja.AppendLine($"Liczba sztuk:            {liczbaSztuk:N0}");
                instrukcja.AppendLine($"Masa drobiu:             {masaKg:N2} kg (w CSV: {masaStr})");
                instrukcja.AppendLine();
                instrukcja.AppendLine("───────────────────────────────────────────────────────────────────────────────");
                instrukcja.AppendLine("KROK PO KROKU:");
                instrukcja.AppendLine("───────────────────────────────────────────────────────────────────────────────");
                instrukcja.AppendLine();
                instrukcja.AppendLine("1. Zaloguj sie do portalu IRZplus (https://irz.arimr.gov.pl)");
                instrukcja.AppendLine();
                instrukcja.AppendLine("2. Przejdz do: UBOJ -> Zgloszenie uboju drobiu w rzezni (ZURD)");
                instrukcja.AppendLine();
                instrukcja.AppendLine("3. WYPELNIJ RECZNIE NAGLOWEK ZGLOSZENIA:");
                instrukcja.AppendLine($"   - Gatunek:            {GATUNEK}");
                instrukcja.AppendLine($"   - Numer rzezni:       {NUMER_RZEZNI} (wybierz z listy)");
                instrukcja.AppendLine($"   - Numer partii uboju: {numerPartiiUboju}");
                instrukcja.AppendLine();
                instrukcja.AppendLine("4. Kliknij przycisk: 'Wczytaj dane z pliku CSV/TXT'");
                instrukcja.AppendLine();
                instrukcja.AppendLine($"5. Wybierz plik: {fileName}");
                instrukcja.AppendLine();
                instrukcja.AppendLine("6. Sprawdz czy dane zaimportowaly sie poprawnie:");
                instrukcja.AppendLine($"   - Numer identyfikacyjny/numer partii: {NUMER_RZEZNI}");
                instrukcja.AppendLine("   - Typ zdarzenia: Przybycie do rzezni i uboj");
                instrukcja.AppendLine($"   - Liczba sztuk drobiu: {liczbaSztuk}");
                instrukcja.AppendLine($"   - Data zdarzenia: {dataZdarzeniaStr}");
                instrukcja.AppendLine($"   - Masa drobiu: {masaStr}");
                instrukcja.AppendLine($"   - Data kupna/wwozu: {dataZdarzeniaStr}");
                instrukcja.AppendLine($"   - Przyjete z dzialalnosci: {numerSiedliskaHodowcy}");
                instrukcja.AppendLine("   - Uboj rytualny: Nie");
                instrukcja.AppendLine();
                instrukcja.AppendLine("7. Zatwierdz zgloszenie");
                instrukcja.AppendLine();
                instrukcja.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
                instrukcja.AppendLine($"Wygenerowano: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                instrukcja.AppendLine("System: ZPSP (Zajebisty Program Sergiusza Szapilkowskiego)");
                instrukcja.AppendLine("Ubojnia Drobiu Piórkowscy, Brzeziny kolo Lodzi");
                instrukcja.AppendLine("═══════════════════════════════════════════════════════════════════════════════");

                // Zapisz instrukcje
                File.WriteAllText(instrukcjaFilePath, instrukcja.ToString(), new UTF8Encoding(true));

                return new ExportResult
                {
                    Success = true,
                    FilePath = filePath,
                    FileName = fileName,
                    Message = $"Wyeksportowano transport do pliku CSV.\n" +
                              $"Plik CSV: {fileName}\n" +
                              $"Instrukcja: {instrukcjaFileName}\n" +
                              (nazwaHodowcy != null ? $"Hodowca: {nazwaHodowcy}\n" : "") +
                              $"Liczba sztuk: {liczbaSztuk:N0}, Masa: {masaKg:N2} kg"
                };
            }
            catch (Exception ex)
            {
                return new ExportResult
                {
                    Success = false,
                    Message = $"Blad eksportu transportu: {ex.Message}" +
                              (nazwaHodowcy != null ? $" (Hodowca: {nazwaHodowcy})" : "")
                };
            }
        }

        /// <summary>
        /// Eksportuje WIELE transportow do ODDZIELNYCH plikow CSV.
        /// Kazdy transport = osobny plik CSV = osobne zgloszenie ZURD.
        ///
        /// ZASADA: 1 TRANSPORT = 1 PLIK CSV = 1 ZGLOSZENIE ZURD W PORTALU
        /// </summary>
        public List<ExportResult> EksportujTransportyPojedynczo_CSV(
            DateTime dataUboju,
            IEnumerable<PozycjaZgloszeniaIRZ> transporty)
        {
            var wyniki = new List<ExportResult>();

            foreach (var transport in transporty)
            {
                var wynik = EksportujPojedynczyTransport_CSV(
                    dataUboju: dataUboju,
                    numerSiedliskaHodowcy: transport.PrzyjeteZDzialalnosci,
                    liczbaSztuk: transport.LiczbaSztuk,
                    masaKg: transport.MasaKg,
                    nazwaHodowcy: transport.Uwagi,
                    numerPartiiWewnetrzny: transport.NumerPartiiWewnetrzny
                );

                wyniki.Add(wynik);

                // Krótka pauza między plikami, żeby timestampy były różne
                System.Threading.Thread.Sleep(100);
            }

            return wyniki;
        }

        /// <summary>
        /// Eksportuje transporty POJEDYNCZO do struktury folderow Rok/Miesiac/Dzien-dzienTygodnia
        /// Nazwy plikow: kolejnosc-NazwaHodowcy-data.csv
        /// </summary>
        public List<ExportResult> EksportujTransportyPojedynczoZStruktura_CSV(
            DateTime dataUboju,
            IEnumerable<PozycjaZgloszeniaIRZ> transporty,
            string bazowaScieszka = null)
        {
            var wyniki = new List<ExportResult>();
            var folderPath = UtworzSciezkeFolderu(dataUboju, bazowaScieszka);

            int kolejnosc = 1;
            foreach (var transport in transporty)
            {
                try
                {
                    // Pobierz nazwe hodowcy z Uwagi (tam zapisujemy nazwe hodowcy)
                    var nazwaHodowcy = transport.Uwagi ?? "NIEZNANY";
                    var fileName = GenerujNazwePliku(kolejnosc, nazwaHodowcy, dataUboju, "csv");
                    var filePath = Path.Combine(folderPath, fileName);

                    // Normalizuj numer siedliska
                    var numerSiedliska = NormalizujNumerSiedliska(transport.PrzyjeteZDzialalnosci);

                    // Generuj numer partii uboju (format: yyMMddNN)
                    var numerPartiiUboju = ZgloszenieZURD.GenerujNumerPartiiUboju(dataUboju, kolejnosc);

                    // Buduj CSV (format ARiMR 13 kolumn)
                    var csv = new StringBuilder();
                    var dataZdarzeniaStr = dataUboju.ToString("yyyy-MM-dd");
                    var masaStr = ((int)Math.Round(transport.MasaKg)).ToString(CultureInfo.InvariantCulture);

                    csv.AppendLine(string.Join(";", new[]
                    {
                        numerPartiiUboju,                             // Kol 1: Numer partii uboju (np. 26011601)
                        transport.LiczbaSztuk.ToString(),             // Kol 2: Liczba sztuk
                        masaStr,                                      // Kol 3: Masa
                        "ZURDUR",                                     // Kol 4: Typ zdarzenia
                        dataZdarzeniaStr,                             // Kol 5: Data zdarzenia
                        "",                                           // Kol 6: Kraj wwozu (puste)
                        dataZdarzeniaStr,                             // Kol 7: Data kupna
                        numerSiedliska,                               // Kol 8: Przyjete z dzialalnosci
                        "",                                           // Kol 9: WNI przewoznika (puste)
                        "",                                           // Kol 10: Srodek transportu (puste)
                        "",                                           // Kol 11: Nr rejestracyjny (puste)
                        "NIE",                                        // Kol 12: Transport wlasny
                        transport.UbojRytualny ? "TAK" : "NIE"        // Kol 13: Uboj rytualny
                    }));

                    File.WriteAllText(filePath, csv.ToString(), new UTF8Encoding(true));

                    wyniki.Add(new ExportResult
                    {
                        Success = true,
                        FilePath = filePath,
                        FileName = fileName,
                        Message = $"Wyeksportowano: {nazwaHodowcy}"
                    });
                }
                catch (Exception ex)
                {
                    wyniki.Add(new ExportResult
                    {
                        Success = false,
                        Message = $"Blad eksportu auta {kolejnosc}: {ex.Message}"
                    });
                }

                kolejnosc++;
            }

            return wyniki;
        }

        /// <summary>
        /// Eksportuje WSZYSTKIE transporty do JEDNEGO pliku CSV w strukturze folderow
        /// </summary>
        public ExportResult EksportujTransportyRazem_CSV(
            ZgloszenieZURD zgloszenie,
            string bazowaScieszka = null)
        {
            try
            {
                var folderPath = UtworzSciezkeFolderu(zgloszenie.DataUboju, bazowaScieszka);
                var fileName = $"ZURD_RAZEM_{zgloszenie.DataUboju:dd-MM-yyyy}_{DateTime.Now:HHmmss}.csv";
                var filePath = Path.Combine(folderPath, fileName);

                var csv = new StringBuilder();

                // Format CSV zgodny z oficjalnym szablonem ARiMR (13 kolumn)
                foreach (var poz in zgloszenie.Pozycje.OrderBy(p => p.Lp))
                {
                    var dataZdarzeniaStr = poz.DataZdarzenia.ToString("yyyy-MM-dd");
                    var masaStr = ((int)Math.Round(poz.MasaKg)).ToString(CultureInfo.InvariantCulture);
                    var numerSiedliska = NormalizujNumerSiedliska(poz.PrzyjeteZDzialalnosci);

                    csv.AppendLine(string.Join(";", new[]
                    {
                        zgloszenie.NumerPartiiUboju,                  // Kol 1: Numer partii uboju (np. 26011601)
                        poz.LiczbaSztuk.ToString(),                   // Kol 2: Liczba sztuk
                        masaStr,                                      // Kol 3: Masa
                        "ZURDUR",                                     // Kol 4: Typ zdarzenia
                        dataZdarzeniaStr,                             // Kol 5: Data zdarzenia
                        "",                                           // Kol 6: Kraj wwozu (puste)
                        dataZdarzeniaStr,                             // Kol 7: Data kupna
                        numerSiedliska,                               // Kol 8: Przyjete z dzialalnosci
                        "",                                           // Kol 9: WNI przewoznika (puste)
                        "",                                           // Kol 10: Srodek transportu (puste)
                        "",                                           // Kol 11: Nr rejestracyjny (puste)
                        "NIE",                                        // Kol 12: Transport wlasny
                        poz.UbojRytualny ? "TAK" : "NIE"              // Kol 13: Uboj rytualny
                    }));
                }

                File.WriteAllText(filePath, csv.ToString(), new UTF8Encoding(true));

                // Zapisz instrukcje
                ZapiszInstrukcjeImportu(zgloszenie, filePath);

                return new ExportResult
                {
                    Success = true,
                    FilePath = filePath,
                    FileName = fileName,
                    Message = $"Wyeksportowano {zgloszenie.LiczbaPozycji} pozycji do jednego pliku"
                };
            }
            catch (Exception ex)
            {
                return new ExportResult
                {
                    Success = false,
                    Message = $"Blad eksportu: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Eksportuje transporty POJEDYNCZO do struktury folderow w formacie XML
        /// </summary>
        public List<ExportResult> EksportujTransportyPojedynczoZStruktura_XML(
            DateTime dataUboju,
            IEnumerable<PozycjaZgloszeniaIRZ> transporty,
            string bazowaScieszka = null)
        {
            var wyniki = new List<ExportResult>();
            var folderPath = UtworzSciezkeFolderu(dataUboju, bazowaScieszka);

            int kolejnosc = 1;
            foreach (var transport in transporty)
            {
                try
                {
                    var nazwaHodowcy = transport.Uwagi ?? "NIEZNANY";
                    var fileName = GenerujNazwePliku(kolejnosc, nazwaHodowcy, dataUboju, "xml");
                    var filePath = Path.Combine(folderPath, fileName);

                    var numerSiedliska = NormalizujNumerSiedliska(transport.PrzyjeteZDzialalnosci);
                    var numerPartiiUboju = ZgloszenieZURD.GenerujNumerPartiiUboju(dataUboju, kolejnosc);

                    var xml = new XDocument(
                        new XDeclaration("1.0", "UTF-8", null),
                        new XComment($" ZURD - {nazwaHodowcy} - {dataUboju:dd.MM.yyyy} "),
                        new XElement("DyspozycjaZURD",
                            new XElement("numerProducenta", NUMER_PRODUCENTA),
                            new XElement("zgloszenie",
                                new XElement("numerRzezni", NUMER_RZEZNI),
                                new XElement("numerPartiiUboju", numerPartiiUboju),
                                new XElement("gatunek",
                                    new XElement("kod", GATUNEK.ToUpper())
                                ),
                                new XElement("pozycje",
                                    new XElement("pozycja",
                                        new XElement("lp", 1),
                                        new XElement("numerIdenPartiiDrobiu", numerSiedliska),
                                        new XElement("liczbaDrobiu", transport.LiczbaSztuk),
                                        new XElement("masaDrobiu", transport.MasaKg.ToString("F2", CultureInfo.InvariantCulture)),
                                        new XElement("typZdarzenia",
                                            new XElement("kod", "UR")
                                        ),
                                        new XElement("dataZdarzenia", dataUboju.ToString("yyyy-MM-dd")),
                                        new XElement("przyjeteZDzialalnosci", numerSiedliska),
                                        new XElement("ubojRytualny", transport.UbojRytualny.ToString().ToLower())
                                    )
                                )
                            )
                        )
                    );

                    xml.Save(filePath);

                    wyniki.Add(new ExportResult
                    {
                        Success = true,
                        FilePath = filePath,
                        FileName = fileName,
                        Message = $"Wyeksportowano XML: {nazwaHodowcy}"
                    });
                }
                catch (Exception ex)
                {
                    wyniki.Add(new ExportResult
                    {
                        Success = false,
                        Message = $"Blad eksportu XML auta {kolejnosc}: {ex.Message}"
                    });
                }

                kolejnosc++;
            }

            return wyniki;
        }

        /// <summary>
        /// Eksportuje WSZYSTKIE transporty do JEDNEGO pliku XML w strukturze folderow
        /// </summary>
        public ExportResult EksportujTransportyRazem_XML(
            ZgloszenieZURD zgloszenie,
            string bazowaScieszka = null)
        {
            try
            {
                var folderPath = UtworzSciezkeFolderu(zgloszenie.DataUboju, bazowaScieszka);
                var fileName = $"ZURD_RAZEM_{zgloszenie.DataUboju:dd-MM-yyyy}_{DateTime.Now:HHmmss}.xml";
                var filePath = Path.Combine(folderPath, fileName);

                var xml = new XDocument(
                    new XDeclaration("1.0", "UTF-8", null),
                    new XComment($" ZURD - RAZEM - {zgloszenie.DataUboju:dd.MM.yyyy} "),
                    new XComment($" Pozycji: {zgloszenie.LiczbaPozycji}, Sztuk: {zgloszenie.SumaLiczbaSztuk} "),
                    new XElement("DyspozycjaZURD",
                        new XElement("numerProducenta", zgloszenie.NumerProducenta),
                        new XElement("zgloszenie",
                            new XElement("numerRzezni", zgloszenie.NumerRzezni),
                            new XElement("numerPartiiUboju", zgloszenie.NumerPartiiUboju),
                            new XElement("gatunek",
                                new XElement("kod", zgloszenie.Gatunek)
                            ),
                            new XElement("pozycje",
                                zgloszenie.Pozycje.OrderBy(p => p.Lp).Select(poz =>
                                    new XElement("pozycja",
                                        new XElement("lp", poz.Lp),
                                        new XElement("numerIdenPartiiDrobiu", poz.NumerPartiiDrobiu),
                                        new XElement("liczbaDrobiu", poz.LiczbaSztuk),
                                        new XElement("masaDrobiu", poz.MasaKg.ToString("F2", CultureInfo.InvariantCulture)),
                                        new XElement("typZdarzenia",
                                            new XElement("kod", poz.TypZdarzenia ?? "ZURDUR")
                                        ),
                                        new XElement("dataZdarzenia", poz.DataZdarzenia.ToString("yyyy-MM-dd")),
                                        new XElement("przyjeteZDzialalnosci", poz.PrzyjeteZDzialalnosci),
                                        new XElement("ubojRytualny", poz.UbojRytualny.ToString().ToLower())
                                    )
                                )
                            )
                        )
                    )
                );

                xml.Save(filePath);

                return new ExportResult
                {
                    Success = true,
                    FilePath = filePath,
                    FileName = fileName,
                    Message = $"Wyeksportowano {zgloszenie.LiczbaPozycji} pozycji do jednego pliku XML"
                };
            }
            catch (Exception ex)
            {
                return new ExportResult
                {
                    Success = false,
                    Message = $"Blad eksportu XML: {ex.Message}"
                };
            }
        }

        #endregion

        #region ========== ZSSD - Zgloszenie Zmiany Stanu Stada (dla hodowcy) ==========

        /// <summary>
        /// Eksportuje ZSSD do pliku CSV (plik dla hodowcy)
        /// </summary>
        public ExportResult EksportujZSSD_CSV(ZgloszenieZSSD zgloszenie)
        {
            try
            {
                var fileName = $"ZSSD_{zgloszenie.NumerSiedliska}_{zgloszenie.DataZdarzenia:yyyy-MM-dd}_{DateTime.Now:HHmmss}.csv";
                var filePath = Path.Combine(_exportPath, fileName);

                var sb = new StringBuilder();

                sb.AppendLine("# ============================================");
                sb.AppendLine("# ZSSD - Zgloszenie Zmiany Stanu Stada Drobiu");
                sb.AppendLine("# PLIK DLA HODOWCY");
                sb.AppendLine("# ============================================");
                sb.AppendLine($"# Wygenerowano: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"# Numer siedliska hodowcy: {zgloszenie.NumerSiedliska}");
                sb.AppendLine($"# Gatunek: {zgloszenie.Gatunek}");
                sb.AppendLine($"# Data zdarzenia: {zgloszenie.DataZdarzenia:yyyy-MM-dd}");
                sb.AppendLine($"# Liczba pozycji: {zgloszenie.Pozycje.Count}");
                sb.AppendLine($"# Suma sztuk: {zgloszenie.Pozycje.Sum(p => p.LiczbaSztuk):N0}");
                sb.AppendLine($"# ");
                sb.AppendLine($"# Typy zdarzen ZSSD:");
                sb.AppendLine($"#   RU = Rozchod - przekazanie do uboju");
                sb.AppendLine($"#   RS = Rozchod - sprzedaz");
                sb.AppendLine($"#   P  = Padniecie");
                sb.AppendLine($"# ============================================");
                sb.AppendLine();

                sb.AppendLine("Lp;Typ zdarzenia;Liczba sztuk;Data zdarzenia;Masa (kg);Przekazane do;Uwagi");

                foreach (var poz in zgloszenie.Pozycje.OrderBy(p => p.Lp))
                {
                    sb.AppendLine(string.Join(";", new[]
                    {
                        poz.Lp.ToString(),
                        poz.TypZdarzenia ?? "RU",
                        poz.LiczbaSztuk.ToString(),
                        poz.DataZdarzenia.ToString("dd-MM-yyyy"),
                        poz.MasaKg.ToString("N2", _polishCulture),
                        poz.PrzyjeteZDzialalnosci ?? "",
                        poz.Uwagi ?? ""
                    }));
                }

                File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(true));

                return new ExportResult
                {
                    Success = true,
                    FilePath = filePath,
                    FileName = fileName,
                    Message = $"Wyeksportowano ZSSD dla hodowcy {zgloszenie.NumerSiedliska}"
                };
            }
            catch (Exception ex)
            {
                return new ExportResult
                {
                    Success = false,
                    Message = $"Blad eksportu ZSSD: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Eksportuje ZSSD do pliku XML
        /// </summary>
        public ExportResult EksportujZSSD_XML(ZgloszenieZSSD zgloszenie)
        {
            try
            {
                var fileName = $"ZSSD_{zgloszenie.NumerSiedliska}_{zgloszenie.DataZdarzenia:yyyy-MM-dd}_{DateTime.Now:HHmmss}.xml";
                var filePath = Path.Combine(_exportPath, fileName);

                var xml = new XDocument(
                    new XDeclaration("1.0", "UTF-8", null),
                    new XComment(" ZSSD - Zgloszenie Zmiany Stanu Stada Drobiu "),
                    new XComment(" PLIK DLA HODOWCY "),
                    new XComment($" Wygenerowano: {DateTime.Now:yyyy-MM-dd HH:mm:ss} "),
                    new XElement("DyspozycjaZSSD",
                        new XElement("numerSiedliska", zgloszenie.NumerSiedliska),
                        new XElement("gatunek",
                            new XElement("kod", zgloszenie.Gatunek)
                        ),
                        new XElement("pozycje",
                            zgloszenie.Pozycje.OrderBy(p => p.Lp).Select(poz =>
                                new XElement("pozycja",
                                    new XElement("lp", poz.Lp),
                                    new XElement("typZdarzenia",
                                        new XElement("kod", poz.TypZdarzenia ?? "RU")
                                    ),
                                    new XElement("liczbaDrobiu", poz.LiczbaSztuk),
                                    new XElement("dataZdarzenia", poz.DataZdarzenia.ToString("yyyy-MM-dd")),
                                    poz.MasaKg > 0
                                        ? new XElement("masaDrobiu", poz.MasaKg.ToString("F2", CultureInfo.InvariantCulture))
                                        : null,
                                    !string.IsNullOrEmpty(poz.PrzyjeteZDzialalnosci)
                                        ? new XElement("przekazaneDoRzezni", poz.PrzyjeteZDzialalnosci)
                                        : null
                                )
                            )
                        )
                    )
                );

                xml.Save(filePath);

                return new ExportResult
                {
                    Success = true,
                    FilePath = filePath,
                    FileName = fileName,
                    Message = $"Wyeksportowano ZSSD XML dla hodowcy {zgloszenie.NumerSiedliska}"
                };
            }
            catch (Exception ex)
            {
                return new ExportResult
                {
                    Success = false,
                    Message = $"Blad eksportu ZSSD XML: {ex.Message}"
                };
            }
        }

        #endregion

        #region ========== Metody pomocnicze ==========

        public void OtworzFolderEksportu()
        {
            System.Diagnostics.Process.Start("explorer.exe", _exportPath);
        }

        public void OtworzFolderZPlikiem(string filePath)
        {
            if (File.Exists(filePath))
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
            }
            else
            {
                OtworzFolderEksportu();
            }
        }

        public string GetExportPath() => _exportPath;

        #endregion

        #region ========== DEBUG - Narzedzia diagnostyczne ==========

        /// <summary>
        /// Generuje TESTOWY plik CSV z przykladowymi danymi do weryfikacji formatu.
        /// Uzyj tej metody do sprawdzenia czy format CSV jest poprawny przed eksportem prawdziwych danych.
        /// </summary>
        public ExportResult GenerujTestowyCSV()
        {
            try
            {
                var timestamp = DateTime.Now.ToString("HHmmss");
                var fileName = $"TEST_ZURD_{DateTime.Now:yyyyMMdd}_{timestamp}.csv";
                var filePath = Path.Combine(_exportPath, fileName);

                var csv = new StringBuilder();

                // Dane testowe BEZ NAGLOWKA, BEZ Lp!
                // Kolejnosc: NumerSiedliska;LiczbaSztuk;Masa;TypZdarzenia;Data;KrajWwozu;DataKupna;Przyjete;Uboj
                var dataTest = DateTime.Now.ToString("dd-MM-yyyy");
                // Przykladowe siedlisko hodowcy: 068736945-001
                csv.AppendLine($"068736945-001;4173;13851;Przybycie do rzeźni i ubój;{dataTest};;{dataTest};068736945-001;N");

                File.WriteAllText(filePath, csv.ToString(), new UTF8Encoding(true));

                // Wygeneruj tez plik z analiza
                var debugFileName = $"DEBUG_TEST_{timestamp}.txt";
                var debugFilePath = Path.Combine(_exportPath, debugFileName);
                var debug = GenerujRaportDebug(csv.ToString(), fileName);
                File.WriteAllText(debugFilePath, debug, new UTF8Encoding(true));

                return new ExportResult
                {
                    Success = true,
                    FilePath = filePath,
                    FileName = fileName,
                    Message = $"Wygenerowano testowy plik CSV: {fileName}\nRaport debug: {debugFileName}"
                };
            }
            catch (Exception ex)
            {
                return new ExportResult
                {
                    Success = false,
                    Message = $"Blad generowania testowego CSV: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Waliduje zawartosc pliku CSV i zwraca raport z bledami.
        /// </summary>
        public string WalidujPlikCSV(string filePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("╔════════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║              WALIDACJA PLIKU CSV DLA IRZPLUS                   ║");
            sb.AppendLine("╚════════════════════════════════════════════════════════════════╝");
            sb.AppendLine();

            if (!File.Exists(filePath))
            {
                sb.AppendLine($"[BLAD] Plik nie istnieje: {filePath}");
                return sb.ToString();
            }

            sb.AppendLine($"Plik: {Path.GetFileName(filePath)}");
            sb.AppendLine($"Sciezka: {filePath}");
            sb.AppendLine();

            var lines = File.ReadAllLines(filePath, Encoding.UTF8);
            var errors = new List<string>();
            var warnings = new List<string>();

            // Pomijamy linie komentarzy (#)
            var dataLines = lines.Where(l => !l.StartsWith("#") && !string.IsNullOrWhiteSpace(l)).ToList();

            if (dataLines.Count == 0)
            {
                errors.Add("Plik jest pusty lub zawiera tylko komentarze");
            }
            else
            {
                // Sprawdz naglowek
                var header = dataLines[0];
                sb.AppendLine("─────────────────────────────────────────────────────────────────");
                sb.AppendLine("NAGLOWEK CSV:");
                sb.AppendLine("─────────────────────────────────────────────────────────────────");
                sb.AppendLine(header);
                sb.AppendLine();

                var expectedHeader = "Lp;Numer identyfikacyjny/numer partii;Typ zdarzenia;Liczba sztuk drobiu;Data zdarzenia;Masa drobiu poddanego ubojowi (kg);Kraj wwozu;Data kupna/wwozu;Przyjęte z działalności;Ubój rytualny";
                if (header != expectedHeader)
                {
                    warnings.Add("Naglowek rozni sie od oczekiwanego!");
                    sb.AppendLine("[OSTRZEZENIE] Oczekiwany naglowek:");
                    sb.AppendLine(expectedHeader);
                    sb.AppendLine();
                }

                // Sprawdz dane
                for (int i = 1; i < dataLines.Count; i++)
                {
                    var line = dataLines[i];
                    var cols = line.Split(';');

                    sb.AppendLine($"─────────────────────────────────────────────────────────────────");
                    sb.AppendLine($"WIERSZ {i} (pozycja {i}):");
                    sb.AppendLine($"─────────────────────────────────────────────────────────────────");
                    sb.AppendLine($"RAW: {line}");
                    sb.AppendLine();

                    if (cols.Length != 10)
                    {
                        errors.Add($"Wiersz {i}: Nieprawidlowa liczba kolumn ({cols.Length} zamiast 10)");
                        continue;
                    }

                    // Analiza kazdej kolumny
                    var kolumny = new[]
                    {
                        ("Lp", cols[0]),
                        ("Numer identyfikacyjny/numer partii", cols[1]),
                        ("Typ zdarzenia", cols[2]),
                        ("Liczba sztuk drobiu", cols[3]),
                        ("Data zdarzenia", cols[4]),
                        ("Masa drobiu (kg)", cols[5]),
                        ("Kraj wwozu", cols[6]),
                        ("Data kupna/wwozu", cols[7]),
                        ("Przyjete z dzialalnosci", cols[8]),
                        ("Uboj rytualny", cols[9])
                    };

                    foreach (var (nazwa, wartosc) in kolumny)
                    {
                        var status = "OK";
                        var uwaga = "";

                        // Walidacje specyficzne
                        if (nazwa == "Numer identyfikacyjny/numer partii")
                        {
                            if (wartosc != NUMER_RZEZNI)
                            {
                                status = "BLAD?";
                                uwaga = $" (oczekiwano: {NUMER_RZEZNI})";
                            }
                        }
                        else if (nazwa == "Masa drobiu (kg)")
                        {
                            if (wartosc.Contains(" "))
                            {
                                status = "BLAD!";
                                uwaga = " (ZAWIERA SPACJE - portal nie zaakceptuje!)";
                                errors.Add($"Wiersz {i}: Masa zawiera spacje: '{wartosc}'");
                            }
                            else if (wartosc.Contains(",") || wartosc.Contains("."))
                            {
                                status = "BLAD!";
                                uwaga = " (ZAWIERA SEPARATOR DZIESIETNY - portal wymaga liczby calkowitej!)";
                                errors.Add($"Wiersz {i}: Masa zawiera separator dziesietny: '{wartosc}'");
                            }
                            else if (!int.TryParse(wartosc, out _))
                            {
                                status = "BLAD!";
                                uwaga = " (NIE JEST LICZBA CALKOWITA!)";
                                errors.Add($"Wiersz {i}: Masa nie jest liczba calkowita: '{wartosc}'");
                            }
                        }
                        else if (nazwa == "Data kupna/wwozu")
                        {
                            if (string.IsNullOrEmpty(wartosc))
                            {
                                status = "BLAD!";
                                uwaga = " (PUSTE - portal wymaga wypelnienia!)";
                                errors.Add($"Wiersz {i}: Data kupna/wwozu jest pusta");
                            }
                        }
                        else if (nazwa == "Data zdarzenia")
                        {
                            if (!System.Text.RegularExpressions.Regex.IsMatch(wartosc, @"^\d{2}-\d{2}-\d{4}$"))
                            {
                                status = "BLAD!";
                                uwaga = " (Nieprawidlowy format - oczekiwano DD-MM-RRRR)";
                                errors.Add($"Wiersz {i}: Nieprawidlowy format daty: '{wartosc}'");
                            }
                        }
                        else if (nazwa == "Typ zdarzenia")
                        {
                            if (wartosc != "UR" && wartosc != "BD")
                            {
                                status = "OSTRZEZENIE";
                                uwaga = " (Nieznany typ - oczekiwano UR lub BD)";
                                warnings.Add($"Wiersz {i}: Nieznany typ zdarzenia: '{wartosc}'");
                            }
                        }

                        sb.AppendLine($"  [{status,-10}] {nazwa,-35}: \"{wartosc}\"{uwaga}");
                    }
                    sb.AppendLine();
                }
            }

            // Podsumowanie
            sb.AppendLine("═════════════════════════════════════════════════════════════════");
            sb.AppendLine("PODSUMOWANIE WALIDACJI:");
            sb.AppendLine("═════════════════════════════════════════════════════════════════");

            if (errors.Count == 0 && warnings.Count == 0)
            {
                sb.AppendLine();
                sb.AppendLine("  ✓ PLIK CSV JEST POPRAWNY - MOZNA IMPORTOWAC DO PORTALU IRZPLUS");
                sb.AppendLine();
            }
            else
            {
                if (errors.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine($"BLEDY ({errors.Count}):");
                    foreach (var err in errors)
                    {
                        sb.AppendLine($"  ✗ {err}");
                    }
                }

                if (warnings.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine($"OSTRZEZENIA ({warnings.Count}):");
                    foreach (var warn in warnings)
                    {
                        sb.AppendLine($"  ! {warn}");
                    }
                }
            }

            sb.AppendLine();
            sb.AppendLine($"Walidacja wykonana: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            return sb.ToString();
        }

        /// <summary>
        /// Generuje szczegolowy raport debug dla zawartosci CSV.
        /// </summary>
        private string GenerujRaportDebug(string csvContent, string fileName)
        {
            var sb = new StringBuilder();

            sb.AppendLine("╔════════════════════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║                    RAPORT DEBUG - EKSPORT CSV IRZPLUS                      ║");
            sb.AppendLine("╚════════════════════════════════════════════════════════════════════════════╝");
            sb.AppendLine();
            sb.AppendLine($"Data generowania:    {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Plik CSV:            {fileName}");
            sb.AppendLine($"Stale uzyte w eksporcie:");
            sb.AppendLine($"  NUMER_RZEZNI:      {NUMER_RZEZNI}");
            sb.AppendLine($"  NUMER_PRODUCENTA:  {NUMER_PRODUCENTA}");
            sb.AppendLine($"  GATUNEK:           {GATUNEK}");
            sb.AppendLine();

            sb.AppendLine("────────────────────────────────────────────────────────────────────────────────");
            sb.AppendLine("SUROWA ZAWARTOSC CSV:");
            sb.AppendLine("────────────────────────────────────────────────────────────────────────────────");
            sb.AppendLine(csvContent);
            sb.AppendLine("────────────────────────────────────────────────────────────────────────────────");
            sb.AppendLine();

            sb.AppendLine("ANALIZA BAJTOW (dla weryfikacji kodowania):");
            var bytes = Encoding.UTF8.GetBytes(csvContent);
            sb.AppendLine($"  Rozmiar:           {bytes.Length} bajtow");
            sb.AppendLine($"  Kodowanie:         UTF-8 z BOM");

            // Sprawdz BOM
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                sb.AppendLine($"  BOM:               OBECNY (EF BB BF)");
            }
            else
            {
                sb.AppendLine($"  BOM:               BRAK (moze byc problem z polskimi znakami!)");
            }

            sb.AppendLine();
            sb.AppendLine("OCZEKIWANY FORMAT KOLUMN W PORTALU IRZPLUS:");
            sb.AppendLine("────────────────────────────────────────────────────────────────────────────────");
            sb.AppendLine("| Kol | Nazwa                                | Przyklad         | Uwagi        |");
            sb.AppendLine("|-----|--------------------------------------|------------------|--------------|");
            sb.AppendLine("| 1   | Lp                                   | 1                | zawsze \"1\"   |");
            sb.AppendLine($"| 2   | Numer identyfikacyjny/numer partii   | {NUMER_RZEZNI}   | nr rzezni!   |");
            sb.AppendLine("| 3   | Typ zdarzenia                        | UR               | UR lub BD    |");
            sb.AppendLine("| 4   | Liczba sztuk drobiu                  | 4173             | calkowita    |");
            sb.AppendLine("| 5   | Data zdarzenia                       | 12-01-2026       | DD-MM-RRRR   |");
            sb.AppendLine("| 6   | Masa drobiu poddanego ubojowi (kg)   | 13851            | BEZ SPACJI!  |");
            sb.AppendLine("| 7   | Kraj wwozu                           | (puste)          | dla PL       |");
            sb.AppendLine("| 8   | Data kupna/wwozu                     | 12-01-2026       | NIE PUSTE!   |");
            sb.AppendLine("| 9   | Przyjete z dzialalnosci              | 068736945-001    | nr hodowcy   |");
            sb.AppendLine("| 10  | Uboj rytualny                        | N                | N lub T      |");
            sb.AppendLine("────────────────────────────────────────────────────────────────────────────────");

            return sb.ToString();
        }

        /// <summary>
        /// Zapisuje log eksportu do pliku tekstowego.
        /// </summary>
        public void ZapiszLogEksportu(ExportResult result, string dodatkoweInfo = null)
        {
            try
            {
                var logFileName = $"LOG_EKSPORT_{DateTime.Now:yyyyMMdd}.txt";
                var logFilePath = Path.Combine(_exportPath, logFileName);

                var log = new StringBuilder();
                log.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]");
                log.AppendLine($"  Status:  {(result.Success ? "SUKCES" : "BLAD")}");
                log.AppendLine($"  Plik:    {result.FileName ?? "(brak)"}");
                log.AppendLine($"  Message: {result.Message}");
                if (!string.IsNullOrEmpty(dodatkoweInfo))
                {
                    log.AppendLine($"  Info:    {dodatkoweInfo}");
                }
                log.AppendLine();

                File.AppendAllText(logFilePath, log.ToString(), new UTF8Encoding(true));
            }
            catch
            {
                // Ignoruj bledy logowania
            }
        }

        /// <summary>
        /// Wyswietla okno diagnostyczne z informacjami o eksporcie (tylko Windows).
        /// </summary>
        public string PobierzDiagnostyke()
        {
            var sb = new StringBuilder();

            sb.AppendLine("╔════════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║              DIAGNOSTYKA SERWISU IRZPLUS EXPORT                ║");
            sb.AppendLine("╚════════════════════════════════════════════════════════════════╝");
            sb.AppendLine();
            sb.AppendLine($"Folder eksportu:     {_exportPath}");
            sb.AppendLine($"Folder istnieje:     {Directory.Exists(_exportPath)}");
            sb.AppendLine();
            sb.AppendLine("STALE:");
            sb.AppendLine($"  NUMER_RZEZNI:      {NUMER_RZEZNI}");
            sb.AppendLine($"  NUMER_PRODUCENTA:  {NUMER_PRODUCENTA}");
            sb.AppendLine($"  GATUNEK:           {GATUNEK}");
            sb.AppendLine();

            // Lista ostatnich plikow
            if (Directory.Exists(_exportPath))
            {
                var files = Directory.GetFiles(_exportPath, "*.csv")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .Take(10)
                    .ToList();

                sb.AppendLine($"OSTATNIE PLIKI CSV ({files.Count}):");
                foreach (var file in files)
                {
                    sb.AppendLine($"  {file.CreationTime:yyyy-MM-dd HH:mm:ss} | {file.Length,8} B | {file.Name}");
                }
            }

            sb.AppendLine();
            sb.AppendLine($"Diagnostyka wykonana: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            return sb.ToString();
        }

        /// <summary>
        /// DEBUGGER: Pokazuje dokladna zawartosc pliku CSV bajt po bajcie.
        /// Uzyj gdy import nie dziala - pokaze dokladnie co jest w pliku.
        /// </summary>
        public string DebugPokarzZawartoscCSV(string filePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("╔═══════════════════════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║                    DEBUG: ZAWARTOSC PLIKU CSV                                  ║");
            sb.AppendLine("╚═══════════════════════════════════════════════════════════════════════════════╝");
            sb.AppendLine();

            if (!File.Exists(filePath))
            {
                sb.AppendLine($"BLAD: Plik nie istnieje: {filePath}");
                return sb.ToString();
            }

            var fileInfo = new FileInfo(filePath);
            sb.AppendLine($"Plik:     {fileInfo.Name}");
            sb.AppendLine($"Sciezka:  {filePath}");
            sb.AppendLine($"Rozmiar:  {fileInfo.Length} bajtow");
            sb.AppendLine($"Utworzony: {fileInfo.CreationTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            // Odczytaj bajty
            var bytes = File.ReadAllBytes(filePath);

            // Sprawdz BOM
            sb.AppendLine("─── ANALIZA BOM (Byte Order Mark) ───");
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                sb.AppendLine("BOM: UTF-8 (EF BB BF) - OK");
            }
            else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                sb.AppendLine("BOM: UTF-16 LE (FF FE) - MOZE BYC PROBLEM!");
            }
            else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                sb.AppendLine("BOM: UTF-16 BE (FE FF) - MOZE BYC PROBLEM!");
            }
            else
            {
                sb.AppendLine("BOM: BRAK - plik bez BOM");
            }
            sb.AppendLine();

            // Odczytaj jako tekst
            var content = File.ReadAllText(filePath, Encoding.UTF8);
            var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            sb.AppendLine($"─── LICZBA LINII: {lines.Length} ───");
            sb.AppendLine();

            for (int i = 0; i < lines.Length && i < 10; i++)
            {
                var line = lines[i];
                sb.AppendLine($"LINIA {i + 1} (dlugosc: {line.Length} znakow):");
                sb.AppendLine($"  [{line}]");

                // Pokaz kolumny
                var cols = line.Split(';');
                sb.AppendLine($"  Kolumn: {cols.Length}");
                for (int j = 0; j < cols.Length; j++)
                {
                    sb.AppendLine($"    [{j + 1}] = \"{cols[j]}\"");
                }
                sb.AppendLine();
            }

            if (lines.Length > 10)
            {
                sb.AppendLine($"... i {lines.Length - 10} wiecej linii");
            }

            sb.AppendLine();
            sb.AppendLine($"Debug wykonany: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            return sb.ToString();
        }

        /// <summary>
        /// DEBUGGER: Analizuje obiekt ZgloszenieZURD PRZED eksportem.
        /// Pokazuje wszystkie dane ktore beda eksportowane.
        /// </summary>
        public string DebugAnalizujZgloszenie(ZgloszenieZURD zgloszenie)
        {
            var sb = new StringBuilder();
            sb.AppendLine("╔═══════════════════════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║                    DEBUG: ANALIZA ZGLOSZENIA ZURD                              ║");
            sb.AppendLine("╚═══════════════════════════════════════════════════════════════════════════════╝");
            sb.AppendLine();

            if (zgloszenie == null)
            {
                sb.AppendLine("BLAD: Zgloszenie jest NULL!");
                return sb.ToString();
            }

            sb.AppendLine("─── DANE NAGLOWKOWE ───");
            sb.AppendLine($"  Gatunek:           {zgloszenie.Gatunek ?? "(NULL)"}");
            sb.AppendLine($"  NumerRzezni:       {zgloszenie.NumerRzezni ?? "(NULL)"}");
            sb.AppendLine($"  NumerProducenta:   {zgloszenie.NumerProducenta ?? "(NULL)"}");
            sb.AppendLine($"  NumerPartiiUboju:  {zgloszenie.NumerPartiiUboju ?? "(NULL)"}");
            sb.AppendLine($"  DataUboju:         {zgloszenie.DataUboju:yyyy-MM-dd}");
            sb.AppendLine();

            sb.AppendLine("─── STATYSTYKI ───");
            sb.AppendLine($"  LiczbaPozycji:     {zgloszenie.LiczbaPozycji}");
            sb.AppendLine($"  LiczbaHodowcow:    {zgloszenie.LiczbaHodowcow}");
            sb.AppendLine($"  SumaLiczbaSztuk:   {zgloszenie.SumaLiczbaSztuk}");
            sb.AppendLine($"  SumaMasaKg:        {zgloszenie.SumaMasaKg:N2}");
            sb.AppendLine();

            sb.AppendLine("─── POZYCJE (transporty) ───");
            if (zgloszenie.Pozycje == null)
            {
                sb.AppendLine("  BLAD: Pozycje = NULL!");
            }
            else if (zgloszenie.Pozycje.Count == 0)
            {
                sb.AppendLine("  UWAGA: Brak pozycji (pusta lista)!");
            }
            else
            {
                foreach (var poz in zgloszenie.Pozycje.OrderBy(p => p.Lp))
                {
                    sb.AppendLine($"  --- Pozycja {poz.Lp} ---");
                    sb.AppendLine($"    NumerPartiiDrobiu:      {poz.NumerPartiiDrobiu ?? "(NULL)"}");
                    sb.AppendLine($"    TypZdarzenia:           {poz.TypZdarzenia ?? "(NULL)"}");
                    sb.AppendLine($"    LiczbaSztuk:            {poz.LiczbaSztuk}");
                    sb.AppendLine($"    MasaKg:                 {poz.MasaKg:N2} -> po zaokr: {(int)Math.Round(poz.MasaKg)}");
                    sb.AppendLine($"    DataZdarzenia:          {poz.DataZdarzenia:yyyy-MM-dd} -> format: {poz.DataZdarzenia:dd-MM-yyyy}");
                    sb.AppendLine($"    KrajWwozu:              {poz.KrajWwozu ?? "(NULL/puste)"}");
                    sb.AppendLine($"    DataKupnaWwozu:         {(poz.DataKupnaWwozu.HasValue ? poz.DataKupnaWwozu.Value.ToString("dd-MM-yyyy") : "(NULL)")}");
                    sb.AppendLine($"    PrzyjeteZDzialalnosci:  {poz.PrzyjeteZDzialalnosci ?? "(NULL)"}");
                    sb.AppendLine($"      -> po normalizacji:   {NormalizujNumerSiedliska(poz.PrzyjeteZDzialalnosci)}");
                    sb.AppendLine($"    UbojRytualny:           {poz.UbojRytualny} -> {(poz.UbojRytualny ? "T" : "N")}");
                    sb.AppendLine($"    Uwagi:                  {poz.Uwagi ?? "(NULL)"}");
                    sb.AppendLine();
                }
            }

            sb.AppendLine($"Debug wykonany: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            return sb.ToString();
        }

        /// <summary>
        /// DEBUGGER: Generuje CSV do stringa (bez zapisu do pliku) i pokazuje wynik.
        /// Uzywaj do sprawdzenia co DOKLADNIE bedzie w pliku CSV.
        /// </summary>
        public string DebugGenerujCSVDoStringa(ZgloszenieZURD zgloszenie)
        {
            var sb = new StringBuilder();
            sb.AppendLine("╔═══════════════════════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║                    DEBUG: PODGLAD CSV (bez zapisu)                             ║");
            sb.AppendLine("╚═══════════════════════════════════════════════════════════════════════════════╝");
            sb.AppendLine();

            if (zgloszenie == null || zgloszenie.Pozycje == null)
            {
                sb.AppendLine("BLAD: Zgloszenie lub pozycje sa NULL!");
                return sb.ToString();
            }

            // Generuj CSV dokladnie tak jak w eksporcie - BEZ NAGLOWKA, BEZ Lp!
            // Format ARiMR: 13 kolumn
            var csv = new StringBuilder();

            foreach (var poz in zgloszenie.Pozycje.OrderBy(p => p.Lp))
            {
                var dataZdarzeniaStr = poz.DataZdarzenia.ToString("yyyy-MM-dd");
                var masaStr = ((int)Math.Round(poz.MasaKg)).ToString(CultureInfo.InvariantCulture);
                var numerSiedliska = NormalizujNumerSiedliska(poz.PrzyjeteZDzialalnosci);

                csv.AppendLine(string.Join(";", new[]
                {
                    zgloszenie.NumerPartiiUboju,                  // Kol 1: Numer partii uboju (np. 26011601)
                    poz.LiczbaSztuk.ToString(),                   // Kol 2: Liczba sztuk
                    masaStr,                                      // Kol 3: Masa
                    "ZURDUR",                                     // Kol 4: Typ zdarzenia
                    dataZdarzeniaStr,                             // Kol 5: Data zdarzenia
                    "",                                           // Kol 6: Kraj wwozu (puste)
                    dataZdarzeniaStr,                             // Kol 7: Data kupna
                    numerSiedliska,                               // Kol 8: Przyjete z dzialalnosci
                    "",                                           // Kol 9: WNI przewoznika (puste)
                    "",                                           // Kol 10: Srodek transportu (puste)
                    "",                                           // Kol 11: Nr rejestracyjny (puste)
                    "NIE",                                        // Kol 12: Transport wlasny
                    poz.UbojRytualny ? "TAK" : "NIE"              // Kol 13: Uboj rytualny
                }));
            }

            sb.AppendLine("─── ZAWARTOSC CSV (dokladnie to co bedzie w pliku) ───");
            sb.AppendLine();
            sb.AppendLine(csv.ToString());
            sb.AppendLine("─── KONIEC CSV ───");
            sb.AppendLine();

            // Analiza
            var csvLines = csv.ToString().Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            sb.AppendLine($"Liczba linii w CSV: {csvLines.Length}");
            sb.AppendLine($"  - BEZ naglowka (format ARiMR)");
            sb.AppendLine($"  - {csvLines.Length} linii danych");
            sb.AppendLine();

            // Sprawdz pierwszy wiersz danych (format ARiMR 13 kolumn)
            if (csvLines.Length > 0)
            {
                var firstDataLine = csvLines[0];
                var cols = firstDataLine.Split(';');
                sb.AppendLine("─── ANALIZA PIERWSZEGO WIERSZA DANYCH ───");
                sb.AppendLine($"Liczba kolumn: {cols.Length} (oczekiwano: 13)");

                var expectedCols = new[]
                {
                    ("Nr identyfikacyjny", "puste"),
                    ("Liczba sztuk", "liczba"),
                    ("Masa", "liczba calkowita"),
                    ("Typ zdarzenia", "ZURDUR"),
                    ("Data zdarzenia", "RRRR-MM-DD"),
                    ("Kraj wwozu", "puste"),
                    ("Data kupna", "RRRR-MM-DD"),
                    ("Przyjete z dzial.", "NNN-NNN"),
                    ("WNI przewoznika", "puste"),
                    ("Srodek transportu", "puste"),
                    ("Nr rejestracyjny", "puste"),
                    ("Transport wlasny", "NIE"),
                    ("Uboj rytualny", "NIE lub TAK")
                };

                for (int i = 0; i < Math.Min(cols.Length, expectedCols.Length); i++)
                {
                    var (nazwa, oczekiwane) = expectedCols[i];
                    var actual = cols[i];
                    var status = "?";

                    // Proste walidacje dla nowego formatu ARiMR
                    if (i == 0 && string.IsNullOrEmpty(actual)) status = "OK";
                    else if (i == 1 && int.TryParse(actual, out _)) status = "OK";
                    else if (i == 2 && int.TryParse(actual, out _) && !actual.Contains(" ") && !actual.Contains(",")) status = "OK";
                    else if (i == 3 && actual == "ZURDUR") status = "OK";
                    else if ((i == 4 || i == 6) && System.Text.RegularExpressions.Regex.IsMatch(actual, @"^\d{4}-\d{2}-\d{2}$")) status = "OK";
                    else if (i == 5 && string.IsNullOrEmpty(actual)) status = "OK";
                    else if (i == 7 && !string.IsNullOrEmpty(actual)) status = "OK";
                    else if ((i == 8 || i == 9 || i == 10) && string.IsNullOrEmpty(actual)) status = "OK";
                    else if (i == 11 && actual == "NIE") status = "OK";
                    else if (i == 12 && (actual == "NIE" || actual == "TAK")) status = "OK";

                    sb.AppendLine($"  [{i + 1}] {nazwa,-20}: \"{actual}\" ({oczekiwane}) [{status}]");
                }
            }

            sb.AppendLine();
            sb.AppendLine($"Debug wykonany: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            return sb.ToString();
        }

        /// <summary>
        /// DEBUGGER: Zapisuje pelny raport diagnostyczny do pliku.
        /// Uzywaj gdy cos nie dziala - wygeneruj raport i wklej tutaj.
        /// </summary>
        public string DebugZapiszPelnyRaport(ZgloszenieZURD zgloszenie = null, string csvFilePath = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("╔═══════════════════════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║              PELNY RAPORT DIAGNOSTYCZNY IRZPLUS EXPORT                         ║");
            sb.AppendLine("║                    (wklej ten raport do Claude)                                ║");
            sb.AppendLine("╚═══════════════════════════════════════════════════════════════════════════════╝");
            sb.AppendLine();
            sb.AppendLine($"Data raportu: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            // Diagnostyka ogolna
            sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
            sb.AppendLine("CZESC 1: KONFIGURACJA");
            sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
            sb.AppendLine(PobierzDiagnostyke());
            sb.AppendLine();

            // Analiza zgloszenia
            if (zgloszenie != null)
            {
                sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
                sb.AppendLine("CZESC 2: ANALIZA ZGLOSZENIA");
                sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
                sb.AppendLine(DebugAnalizujZgloszenie(zgloszenie));
                sb.AppendLine();

                sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
                sb.AppendLine("CZESC 3: PODGLAD CSV");
                sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
                sb.AppendLine(DebugGenerujCSVDoStringa(zgloszenie));
                sb.AppendLine();
            }

            // Analiza pliku CSV
            if (!string.IsNullOrEmpty(csvFilePath) && File.Exists(csvFilePath))
            {
                sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
                sb.AppendLine("CZESC 4: ANALIZA PLIKU CSV");
                sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
                sb.AppendLine(DebugPokarzZawartoscCSV(csvFilePath));
                sb.AppendLine();

                sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
                sb.AppendLine("CZESC 5: WALIDACJA CSV");
                sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
                sb.AppendLine(WalidujPlikCSV(csvFilePath));
            }

            sb.AppendLine();
            sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
            sb.AppendLine("KONIEC RAPORTU");
            sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");

            // Zapisz do pliku
            try
            {
                var reportFileName = $"DEBUG_RAPORT_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                var reportFilePath = Path.Combine(_exportPath, reportFileName);
                File.WriteAllText(reportFilePath, sb.ToString(), new UTF8Encoding(true));

                sb.AppendLine();
                sb.AppendLine($"Raport zapisany do: {reportFilePath}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Blad zapisu raportu: {ex.Message}");
            }

            return sb.ToString();
        }

        #endregion
    }

    /// <summary>
    /// Wynik eksportu
    /// </summary>
    public class ExportResult
    {
        public bool Success { get; set; }
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string Message { get; set; }
    }
}
