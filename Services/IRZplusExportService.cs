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
                sb.AppendLine(string.Join(";", new[]
                {
                    "Lp",
                    "Numer identyfikacyjny/numer partii",
                    "Typ zdarzenia",
                    "Liczba sztuk drobiu",
                    "Data zdarzenia",
                    "Masa drobiu (kg)",
                    "Kraj wwozu",
                    "Data kupna/wwozu",
                    "Przyjete z dzialalnosci",
                    "Uboj rytualny"
                }));

                // === POZYCJE (kazdy aut/transport to osobna linia) ===
                foreach (var poz in zgloszenie.Pozycje.OrderBy(p => p.Lp))
                {
                    sb.AppendLine(string.Join(";", new[]
                    {
                        poz.Lp.ToString(),
                        poz.NumerPartiiDrobiu ?? "",
                        poz.TypZdarzenia ?? "UR",
                        poz.LiczbaSztuk.ToString(),
                        poz.DataZdarzenia.ToString("dd-MM-yyyy"),  // Format DD-MM-RRRR jak w portalu!
                        poz.MasaKg.ToString("N2", _polishCulture), // Przecinek jako separator (13118,00)
                        poz.KrajWwozu ?? "",
                        poz.DataKupnaWwozu?.ToString("dd-MM-yyyy") ?? "",
                        poz.PrzyjeteZDzialalnosci ?? "",
                        poz.UbojRytualny ? "T" : "N"
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
