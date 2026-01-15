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

        #region ========== ZURD - Zgloszenie Uboju Drobiu w Rzezni ==========

        /// <summary>
        /// Eksportuje ZURD do pliku CSV zgodnego z importem w portalu IRZplus
        /// Format: JEDNO zgloszenie, WIELE pozycji (autow/transportow)
        /// </summary>
        public ExportResult EksportujZURD_CSV(ZgloszenieZURD zgloszenie)
        {
            try
            {
                var fileName = $"ZURD_{zgloszenie.DataUboju:yyyy-MM-dd}_{DateTime.Now:HHmmss}.csv";
                var filePath = Path.Combine(_exportPath, fileName);

                var sb = new StringBuilder();

                // === SEKCJA NAGLOWKOWA (metadane - komentarze) ===
                sb.AppendLine("# ============================================");
                sb.AppendLine("# ZURD - Zgloszenie Uboju Drobiu w Rzezni");
                sb.AppendLine("# Portal IRZplus - Import CSV");
                sb.AppendLine("# ============================================");
                sb.AppendLine($"# Wygenerowano: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"# ");
                sb.AppendLine($"# NAGLOWEK ZGLOSZENIA:");
                sb.AppendLine($"# Gatunek: {zgloszenie.Gatunek}");
                sb.AppendLine($"# Numer rzezni: {zgloszenie.NumerRzezni}");
                sb.AppendLine($"# Numer partii uboju: {zgloszenie.NumerPartiiUboju}");
                sb.AppendLine($"# Data uboju: {zgloszenie.DataUboju:yyyy-MM-dd}");
                sb.AppendLine($"# ");
                sb.AppendLine($"# STATYSTYKI:");
                sb.AppendLine($"# Liczba pozycji (autow): {zgloszenie.LiczbaPozycji}");
                sb.AppendLine($"# Liczba hodowcow: {zgloszenie.LiczbaHodowcow}");
                sb.AppendLine($"# Suma sztuk: {zgloszenie.SumaLiczbaSztuk:N0}");
                sb.AppendLine($"# Suma masa: {zgloszenie.SumaMasaKg:N2} kg");
                sb.AppendLine($"# ");
                sb.AppendLine($"# INSTRUKCJA IMPORTU:");
                sb.AppendLine($"# 1. Zaloguj sie do portalu IRZplus");
                sb.AppendLine($"# 2. Przejdz do: Zgloszenie uboju drobiu w rzezni");
                sb.AppendLine($"# 3. Uzupelnij naglowek (gatunek, numer rzezni, numer partii)");
                sb.AppendLine($"# 4. Kliknij: Wczytaj dane z pliku CSV/TXT");
                sb.AppendLine($"# 5. Wybierz ten plik");
                sb.AppendLine($"# ============================================");
                sb.AppendLine();

                // === NAGLOWEK KOLUMN ===
                // Dokladnie takie nazwy jak w portalu IRZplus!
                sb.AppendLine("Lp;Numer identyfikacyjny/numer partii;Typ zdarzenia;Liczba sztuk drobiu;Data zdarzenia;Masa drobiu poddanego ubojowi (kg);Kraj wwozu;Data kupna/wwozu;Przyjęte z działalności;Ubój rytualny");

                // === POZYCJE (kazdy aut/transport to osobna linia) ===
                foreach (var poz in zgloszenie.Pozycje.OrderBy(p => p.Lp))
                {
                    // Data zdarzenia w formacie DD-MM-RRRR
                    var dataZdarzeniaStr = poz.DataZdarzenia.ToString("dd-MM-yyyy");

                    // Masa jako liczba calkowita BEZ separatorow tysiecy, BEZ czesci dziesietnej
                    // Portal IRZplus wymaga formatu: "13851" (nie "13 851.00" ani "13851,00")
                    var masaStr = ((int)Math.Round(poz.MasaKg)).ToString(CultureInfo.InvariantCulture);

                    sb.AppendLine(string.Join(";", new[]
                    {
                        poz.Lp.ToString(),                            // Kol 1: Lp
                        NUMER_RZEZNI,                                  // Kol 2: Nr identyfikacyjny = NUMER RZEZNI (stala!)
                        poz.TypZdarzenia ?? "UR",                      // Kol 3: Typ zdarzenia = "UR"
                        poz.LiczbaSztuk.ToString(),                    // Kol 4: Liczba sztuk
                        dataZdarzeniaStr,                              // Kol 5: Data zdarzenia = "DD-MM-RRRR"
                        masaStr,                                       // Kol 6: Masa = liczba calkowita BEZ spacji!
                        poz.KrajWwozu ?? "",                           // Kol 7: Kraj wwozu = puste dla PL
                        dataZdarzeniaStr,                              // Kol 8: Data kupna = taka sama jak data zdarzenia (WYMAGANE!)
                        poz.PrzyjeteZDzialalnosci ?? "",               // Kol 9: Przyjete z dzial. = numer siedliska hodowcy
                        poz.UbojRytualny ? "T" : "N"                   // Kol 10: Uboj rytualny = "N" lub "T"
                    }));
                }

                // Zapisz z kodowaniem UTF-8 z BOM (dla polskich znakow)
                File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(true));

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
                                            new XElement("kod", poz.TypZdarzenia ?? "UR")
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

                // Data zdarzenia w formacie DD-MM-RRRR
                var dataZdarzeniaStr = dataUboju.ToString("dd-MM-yyyy");

                // Masa jako liczba calkowita BEZ separatorow
                var masaStr = ((int)Math.Round(masaKg)).ToString(CultureInfo.InvariantCulture);

                // === BUDUJ CSV ===
                var csv = new StringBuilder();

                // Naglowek CSV
                csv.AppendLine("Lp;Numer identyfikacyjny/numer partii;Typ zdarzenia;Liczba sztuk drobiu;Data zdarzenia;Masa drobiu poddanego ubojowi (kg);Kraj wwozu;Data kupna/wwozu;Przyjęte z działalności;Ubój rytualny");

                // Dane - JEDNA linia (jeden transport)
                csv.AppendLine(string.Join(";", new[]
                {
                    "1",                                // Kol 1: Lp - zawsze "1" dla pojedynczego transportu
                    NUMER_RZEZNI,                       // Kol 2: Nr identyfikacyjny = NUMER RZEZNI (stala!)
                    "UR",                               // Kol 3: Typ zdarzenia = "UR" (Przybycie do rzezni i uboj)
                    liczbaSztuk.ToString(),             // Kol 4: Liczba sztuk
                    dataZdarzeniaStr,                   // Kol 5: Data zdarzenia
                    masaStr,                            // Kol 6: Masa = liczba calkowita BEZ spacji!
                    "",                                 // Kol 7: Kraj wwozu = puste dla polskich hodowcow
                    dataZdarzeniaStr,                   // Kol 8: Data kupna = taka sama jak data zdarzenia (WYMAGANE!)
                    numerSiedliskaHodowcy,              // Kol 9: Przyjete z dzial. = numer siedliska hodowcy
                    "N"                                 // Kol 10: Uboj rytualny = "N"
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
