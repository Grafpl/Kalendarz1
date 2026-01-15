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
    /// Format CSV jest separator-sensitive - portal uzywa srednika (;) jako separatora!
    /// Liczby dziesietne uzywaja PRZECINKA (13118,00) a nie kropki!
    /// </summary>
    public class IRZplusExportService
    {
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

                // === NAGLOWEK CSV - BEZ KOMENTARZY! Portal ich nie obsluguje! ===
                // Kolejnosc kolumn zgodna z dokumentacja ARiMR!
                sb.AppendLine("Lp;Numer identyfikacyjny/numer partii;Typ zdarzenia;Liczba sztuk drobiu;Data zdarzenia;Masa drobiu poddanego ubojowi (kg);Kraj wwozu;Data kupna/wwozu;Przyjęte z działalności;Ubój rytualny");

                // === POZYCJE (kazdy aut/transport to osobna linia) ===
                foreach (var poz in zgloszenie.Pozycje.OrderBy(p => p.Lp))
                {
                    var dataZdarzeniaStr = poz.DataZdarzenia.ToString("dd-MM-yyyy");
                    var masaCalkowita = Math.Round(poz.MasaKg).ToString("0", System.Globalization.CultureInfo.InvariantCulture);

                    sb.AppendLine(string.Join(";", new[]
                    {
                        poz.Lp.ToString(),                        // 1. Lp
                        poz.NumerPartiiDrobiu ?? "",              // 2. Numer identyfikacyjny/numer partii
                        poz.TypZdarzenia ?? "UR",                 // 3. Typ zdarzenia
                        poz.LiczbaSztuk.ToString(),               // 4. Liczba sztuk drobiu
                        dataZdarzeniaStr,                         // 5. Data zdarzenia (DD-MM-RRRR)
                        masaCalkowita,                            // 6. Masa drobiu - LICZBA CALKOWITA!
                        poz.KrajWwozu ?? "",                      // 7. Kraj wwozu
                        dataZdarzeniaStr,                         // 8. Data kupna/wwozu = TA SAMA DATA!
                        poz.PrzyjeteZDzialalnosci ?? "",          // 9. Przyjete z dzialalnosci
                        poz.UbojRytualny ? "T" : "N"              // 10. Uboj rytualny
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

        #region ========== EKSPORT POJEDYNCZYCH TRANSPORTOW ==========

        /// <summary>
        /// Eksportuje JEDEN transport do pliku CSV zgodnego z portalem IRZplus.
        ///
        /// WAZNE: Portal IRZplus akceptuje tylko JEDNA pozycje na zgloszenie!
        /// Dlatego kazdy transport musi byc w osobnym pliku CSV.
        ///
        /// PRAWIDLOWA KOLEJNOSC KOLUMN (z formularza portalu IRZplus):
        /// 1. Numer identyfikacyjny/numer partii = NUMER RZEZNI (039806095-001)
        /// 2. Liczba sztuk drobiu
        /// 3. Masa drobiu poddanego ubojowi (kg)
        /// 4. Typ zdarzenia (UR)
        /// 5. Data zdarzenia (DD-MM-RRRR)
        /// 6. Kraj wwozu (puste dla PL)
        /// 7. Data kupna/wwozu (puste lub data)
        /// 8. Przyjete z dzialalnosci = NUMER HODOWCY (np. 068736945-001)
        /// 9. Uboj rytualny (N/T)
        ///
        /// UWAGA: Kolumna "Lp" NIE ISTNIEJE w portalu - nie dodawac!
        /// </summary>
        public ExportResult EksportujPojedynczyTransport_CSV(
            PozycjaZgloszeniaIRZ transport,
            DateTime dataUboju,
            string numerRzezni = "039806095-001",
            string gatunek = "KURY",
            int numerKolejny = 1)
        {
            try
            {
                // Walidacja danych
                if (transport == null)
                    throw new ArgumentNullException(nameof(transport));

                // Numer hodowcy (siedliska) - idzie do pola "Przyjete z dzialalnosci"
                var numerHodowcy = transport.NumerPartiiDrobiu;
                if (string.IsNullOrWhiteSpace(numerHodowcy))
                {
                    return new ExportResult
                    {
                        Success = false,
                        Message = $"Brak numeru IRZplus dla transportu: {transport.Uwagi}"
                    };
                }

                // Nazwa pliku: ZURD_DATA_NUMER-HODOWCY_NR.csv
                var numerHodowcyBezSpecjalnych = numerHodowcy.Replace("-", "");
                var fileName = $"ZURD_{dataUboju:yyyy-MM-dd}_{numerHodowcyBezSpecjalnych}_{numerKolejny:000}.csv";
                var filePath = Path.Combine(_exportPath, fileName);

                // Numer partii uboju - unikalny dla kazdego zgloszenia
                var numerPartiiUboju = $"{dataUboju:yy}{dataUboju.DayOfYear:000}{numerKolejny:000}";

                var sb = new StringBuilder();

                // === NAGLOWEK CSV - WYMAGANY PRZEZ PORTAL IRZPLUS ===
                // Kolejnosc kolumn zgodna z dokumentacja ARiMR!
                sb.AppendLine("Lp;Numer identyfikacyjny/numer partii;Typ zdarzenia;Liczba sztuk drobiu;Data zdarzenia;Masa drobiu poddanego ubojowi (kg);Kraj wwozu;Data kupna/wwozu;Przyjęte z działalności;Ubój rytualny");

                // === JEDNA POZYCJA - JEDEN TRANSPORT ===
                // KOLEJNOSC KOLUMN ZGODNA Z DOKUMENTACJA:
                // 1. Lp = "1"
                // 2. Numer identyfikacyjny/numer partii = NUMER RZEZNI (039806095-001)
                // 3. Typ zdarzenia = "UR"
                // 4. Liczba sztuk drobiu
                // 5. Data zdarzenia (DD-MM-RRRR)
                // 6. Masa drobiu poddanego ubojowi (kg) - LICZBA CALKOWITA bez spacji!
                // 7. Kraj wwozu = puste dla PL
                // 8. Data kupna/wwozu = ta sama co data zdarzenia (NIE PUSTE!)
                // 9. Przyjete z dzialalnosci = NUMER HODOWCY
                // 10. Uboj rytualny = N lub T

                var dataZdarzeniaStr = transport.DataZdarzenia.ToString("dd-MM-yyyy");
                var masaCalkowita = Math.Round(transport.MasaKg).ToString("0", System.Globalization.CultureInfo.InvariantCulture);

                sb.AppendLine(string.Join(";", new[]
                {
                    "1",                                  // 1. Lp
                    numerRzezni,                          // 2. Numer identyfikacyjny = NUMER RZEZNI
                    transport.TypZdarzenia ?? "UR",       // 3. Typ zdarzenia
                    transport.LiczbaSztuk.ToString(),     // 4. Liczba sztuk drobiu
                    dataZdarzeniaStr,                     // 5. Data zdarzenia
                    masaCalkowita,                        // 6. Masa drobiu (kg) - LICZBA CALKOWITA!
                    transport.KrajWwozu ?? "",            // 7. Kraj wwozu (puste dla PL)
                    dataZdarzeniaStr,                     // 8. Data kupna/wwozu = TA SAMA DATA!
                    numerHodowcy,                         // 9. Przyjete z dzialalnosci = NUMER HODOWCY
                    transport.UbojRytualny ? "T" : "N"    // 10. Uboj rytualny
                }));

                // Zapisz z kodowaniem UTF-8 z BOM (dla polskich znakow)
                File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(true));

                // === ZAPISZ INSTRUKCJE DO OSOBNEGO PLIKU TXT ===
                var instrukcjaFileName = $"INSTRUKCJA_{dataUboju:yyyy-MM-dd}_{numerHodowcyBezSpecjalnych}_{numerKolejny:000}.txt";
                var instrukcjaFilePath = Path.Combine(_exportPath, instrukcjaFileName);
                var instrukcja = new StringBuilder();

                // WAZNE OSTRZEZENIE NA POCZATKU
                instrukcja.AppendLine("================================================================");
                instrukcja.AppendLine("!!! WAZNE - WYPELNIJ RECZNIE PRZED IMPORTEM CSV !!!");
                instrukcja.AppendLine("================================================================");
                instrukcja.AppendLine();
                instrukcja.AppendLine("Naglowek zgloszenia NIE importuje sie z pliku CSV!");
                instrukcja.AppendLine("Musisz RECZNIE wypelnic te pola w portalu PRZED wczytaniem CSV:");
                instrukcja.AppendLine();
                instrukcja.AppendLine($"   1. Gatunek: kury");
                instrukcja.AppendLine($"   2. Numer rzezni: {numerRzezni}");
                instrukcja.AppendLine($"   3. Numer partii uboju: {numerPartiiUboju}");
                instrukcja.AppendLine();
                instrukcja.AppendLine("Dopiero PO wypelnieniu tych pol kliknij 'Wczytaj dane z pliku CSV'");
                instrukcja.AppendLine();
                instrukcja.AppendLine("================================================================");
                instrukcja.AppendLine();
                instrukcja.AppendLine("=== DANE TRANSPORTU ===");
                instrukcja.AppendLine($"Hodowca: {transport.Uwagi}");
                instrukcja.AppendLine($"Numer siedliska hodowcy (IRZplus): {numerHodowcy}");
                instrukcja.AppendLine($"Liczba sztuk: {transport.LiczbaSztuk:N0}");
                instrukcja.AppendLine($"Masa: {transport.MasaKg:N0} kg (zaokraglona: {masaCalkowita} kg)");
                instrukcja.AppendLine($"Data uboju: {dataUboju:dd-MM-yyyy}");
                instrukcja.AppendLine();
                instrukcja.AppendLine("=== MAPOWANIE POL W CSV ===");
                instrukcja.AppendLine($"Numer identyfikacyjny/numer partii: {numerRzezni} (numer RZEZNI!)");
                instrukcja.AppendLine($"Przyjete z dzialalnosci: {numerHodowcy} (numer HODOWCY!)");
                instrukcja.AppendLine();
                instrukcja.AppendLine("=== INSTRUKCJA KROK PO KROKU ===");
                instrukcja.AppendLine("1. Zaloguj sie do portalu IRZplus");
                instrukcja.AppendLine("2. Menu: Drob > Zgloszenie uboju drobiu w rzezni (ZURD)");
                instrukcja.AppendLine("3. Kliknij: 'Dodaj zgloszenie'");
                instrukcja.AppendLine("4. NAJPIERW wypelnij naglowek:");
                instrukcja.AppendLine($"   - Gatunek: kury");
                instrukcja.AppendLine($"   - Numer rzezni: {numerRzezni}");
                instrukcja.AppendLine($"   - Numer partii uboju: {numerPartiiUboju}");
                instrukcja.AppendLine("5. DOPIERO TERAZ kliknij: 'Wczytaj dane z pliku CSV/TXT'");
                instrukcja.AppendLine($"6. Wybierz plik: {fileName}");
                instrukcja.AppendLine("7. Sprawdz czy dane sie poprawnie wczytaly");
                instrukcja.AppendLine("8. Kliknij 'Zapisz'");
                instrukcja.AppendLine();
                instrukcja.AppendLine("================================================================");
                File.WriteAllText(instrukcjaFilePath, instrukcja.ToString(), new UTF8Encoding(true));

                return new ExportResult
                {
                    Success = true,
                    FilePath = filePath,
                    FileName = fileName,
                    Message = $"Wyeksportowano: {transport.Uwagi} ({transport.LiczbaSztuk} szt.)",
                    AdditionalInfo = new Dictionary<string, string>
                    {
                        { "NumerPartiiUboju", numerPartiiUboju },
                        { "InstrukcjaFile", instrukcjaFilePath },
                        { "Gatunek", gatunek },
                        { "NumerRzezni", numerRzezni },
                        { "NumerHodowcy", numerHodowcy }
                    }
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
        /// Eksportuje WIELE transportow - kazdy do OSOBNEGO pliku CSV.
        /// Zwraca liste wynikow eksportu.
        /// </summary>
        public List<ExportResult> EksportujTransportyPojedynczo_CSV(
            IEnumerable<PozycjaZgloszeniaIRZ> transporty,
            DateTime dataUboju,
            string numerRzezni = "039806095-001",
            string gatunek = "KURY")
        {
            var results = new List<ExportResult>();
            int numerKolejny = 1;

            foreach (var transport in transporty)
            {
                var result = EksportujPojedynczyTransport_CSV(
                    transport,
                    dataUboju,
                    numerRzezni,
                    gatunek,
                    numerKolejny);

                results.Add(result);
                numerKolejny++;
            }

            return results;
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
        public Dictionary<string, string> AdditionalInfo { get; set; }
    }
}
