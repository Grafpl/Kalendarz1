using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Kalendarz1.Models.IRZplus;

namespace Kalendarz1.Services
{
    /// <summary>
    /// Serwis do eksportu danych do formatów XML i CSV dla portalu IRZplus.
    /// Używany gdy API zwraca "Access denied" - można wtedy zaimportować plik ręcznie w portalu.
    /// </summary>
    public class IRZplusExportService
    {
        private readonly string _exportPath;

        public IRZplusExportService(string exportPath = null)
        {
            _exportPath = exportPath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "IRZplus_Export");

            Directory.CreateDirectory(_exportPath);
        }

        #region ZURD - Zgłoszenie Uboju Drobiu w Rzeźni

        /// <summary>
        /// Eksportuje ZURD do pliku XML
        /// </summary>
        public string EksportujZURD_XML(EksportZURD dane)
        {
            var fileName = $"ZURD_{dane.DataUboju:yyyy-MM-dd}_{DateTime.Now:HHmmss}.xml";
            var filePath = Path.Combine(_exportPath, fileName);

            var xml = new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XComment($"ZURD - Zgłoszenie Uboju Drobiu w Rzeźni"),
                new XComment($"Wygenerowano: {DateTime.Now:yyyy-MM-dd HH:mm:ss}"),
                new XComment($"Data uboju: {dane.DataUboju:yyyy-MM-dd}"),
                new XComment($"Liczba pozycji: {dane.Pozycje.Count}"),
                new XComment($"Suma sztuk: {dane.Pozycje.Sum(p => p.LiczbaSztuk)}"),
                new XElement("DyspozycjaZURD",
                    new XElement("numerProducenta", dane.NumerProducenta),
                    new XElement("zgloszenie",
                        new XElement("numerRzezni", dane.NumerRzezni),
                        new XElement("numerPartiiUboju", dane.NumerPartiiUboju ?? EksportZURD.GenerujNumerPartiiUboju(dane.DataUboju)),
                        new XElement("gatunek",
                            new XElement("kod", dane.GatunekKod)
                        ),
                        new XElement("pozycje",
                            dane.Pozycje.Select(p =>
                                new XElement("pozycja",
                                    new XElement("lp", p.Lp),
                                    new XElement("numerIdenPartiiDrobiu", p.NumerPartiiDrobiu),
                                    new XElement("liczbaDrobiu", p.LiczbaSztuk),
                                    new XElement("typZdarzenia",
                                        new XElement("kod", p.TypZdarzenia ?? TypZdarzeniaZURD.UbojRzezniczy)
                                    ),
                                    new XElement("dataZdarzenia", p.DataZdarzenia.ToString("yyyy-MM-dd")),
                                    new XElement("przyjeteZDzialalnosci", p.PrzyjeteZDzialalnosci),
                                    new XElement("ubojRytualny", p.UbojRytualny.ToString().ToLower())
                                )
                            )
                        )
                    )
                )
            );

            xml.Save(filePath);
            return filePath;
        }

        /// <summary>
        /// Eksportuje ZURD do pliku CSV
        /// </summary>
        public string EksportujZURD_CSV(EksportZURD dane, char separator = ';')
        {
            var fileName = $"ZURD_{dane.DataUboju:yyyy-MM-dd}_{DateTime.Now:HHmmss}.csv";
            var filePath = Path.Combine(_exportPath, fileName);

            var sb = new StringBuilder();

            // Komentarze/metadane
            sb.AppendLine($"# ZURD - Zgłoszenie Uboju Drobiu w Rzeźni");
            sb.AppendLine($"# Wygenerowano: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"# Numer producenta: {dane.NumerProducenta}");
            sb.AppendLine($"# Numer rzeźni: {dane.NumerRzezni}");
            sb.AppendLine($"# Numer partii uboju: {dane.NumerPartiiUboju ?? EksportZURD.GenerujNumerPartiiUboju(dane.DataUboju)}");
            sb.AppendLine($"# Gatunek: {dane.GatunekKod}");
            sb.AppendLine($"# Data uboju: {dane.DataUboju:yyyy-MM-dd}");
            sb.AppendLine($"# Liczba pozycji: {dane.Pozycje.Count}");
            sb.AppendLine($"# Suma sztuk: {dane.Pozycje.Sum(p => p.LiczbaSztuk)}");
            sb.AppendLine();

            // Nagłówek
            sb.AppendLine(string.Join(separator.ToString(), new[] {
                "Lp",
                "Numer identyfikacyjny partii drobiu",
                "Typ zdarzenia",
                "Liczba sztuk drobiu",
                "Data zdarzenia",
                "Przyjęte z działalności",
                "Ubój rytualny"
            }));

            // Dane
            foreach (var p in dane.Pozycje)
            {
                sb.AppendLine(string.Join(separator.ToString(), new[] {
                    p.Lp.ToString(),
                    p.NumerPartiiDrobiu,
                    p.TypZdarzenia ?? TypZdarzeniaZURD.UbojRzezniczy,
                    p.LiczbaSztuk.ToString(),
                    p.DataZdarzenia.ToString("yyyy-MM-dd"),
                    p.PrzyjeteZDzialalnosci,
                    p.UbojRytualny ? "T" : "N"
                }));
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            return filePath;
        }

        #endregion

        #region ZSSD - Zgłoszenie Zmiany Stanu Stada Drobiu

        /// <summary>
        /// Eksportuje ZSSD do pliku XML (dla hodowcy)
        /// </summary>
        public string EksportujZSSD_XML(EksportZSSD dane)
        {
            var fileName = $"ZSSD_{dane.NumerSiedliskaHodowcy}_{dane.DataZdarzenia:yyyy-MM-dd}_{DateTime.Now:HHmmss}.xml";
            var filePath = Path.Combine(_exportPath, fileName);

            var xml = new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XComment($"ZSSD - Zgłoszenie Zmiany Stanu Stada Drobiu"),
                new XComment($"Wygenerowano: {DateTime.Now:yyyy-MM-dd HH:mm:ss}"),
                new XComment($"Numer siedliska hodowcy: {dane.NumerSiedliskaHodowcy}"),
                new XComment($"Data zdarzenia: {dane.DataZdarzenia:yyyy-MM-dd}"),
                new XComment($"Liczba pozycji: {dane.Pozycje.Count}"),
                new XComment($"Suma sztuk: {dane.Pozycje.Sum(p => p.LiczbaSztuk)}"),
                new XElement("DyspozycjaZSSD",
                    new XElement("numerSiedliska", dane.NumerSiedliskaHodowcy),
                    new XElement("gatunek",
                        new XElement("kod", dane.GatunekKod)
                    ),
                    new XElement("pozycje",
                        dane.Pozycje.Select(p =>
                            new XElement("pozycja",
                                new XElement("lp", p.Lp),
                                new XElement("typZdarzenia",
                                    new XElement("kod", p.TypZdarzenia ?? TypZdarzeniaZSSD.RozchodUboj)
                                ),
                                new XElement("liczbaDrobiu", p.LiczbaSztuk),
                                new XElement("dataZdarzenia", p.DataZdarzenia.ToString("yyyy-MM-dd")),
                                p.PrzyjeteZDzialalnosci != null
                                    ? new XElement("przekazaneDoRzezni", p.PrzyjeteZDzialalnosci)
                                    : null,
                                p.Uwagi != null
                                    ? new XElement("uwagi", p.Uwagi)
                                    : null
                            )
                        )
                    )
                )
            );

            xml.Save(filePath);
            return filePath;
        }

        /// <summary>
        /// Eksportuje ZSSD do pliku CSV (dla hodowcy)
        /// </summary>
        public string EksportujZSSD_CSV(EksportZSSD dane, char separator = ';')
        {
            var fileName = $"ZSSD_{dane.NumerSiedliskaHodowcy}_{dane.DataZdarzenia:yyyy-MM-dd}_{DateTime.Now:HHmmss}.csv";
            var filePath = Path.Combine(_exportPath, fileName);

            var sb = new StringBuilder();

            // Komentarze/metadane
            sb.AppendLine($"# ZSSD - Zgłoszenie Zmiany Stanu Stada Drobiu");
            sb.AppendLine($"# Wygenerowano: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"# Numer siedliska hodowcy: {dane.NumerSiedliskaHodowcy}");
            sb.AppendLine($"# Gatunek: {dane.GatunekKod}");
            sb.AppendLine($"# Data zdarzenia: {dane.DataZdarzenia:yyyy-MM-dd}");
            sb.AppendLine($"# Liczba pozycji: {dane.Pozycje.Count}");
            sb.AppendLine($"# Suma sztuk: {dane.Pozycje.Sum(p => p.LiczbaSztuk)}");
            sb.AppendLine($"# ");
            sb.AppendLine($"# Typy zdarzeń ZSSD:");
            sb.AppendLine($"#   PZ = Przychód - zakup");
            sb.AppendLine($"#   PU = Przychód - wylęg/urodzenie");
            sb.AppendLine($"#   RS = Rozchód - sprzedaż");
            sb.AppendLine($"#   RU = Rozchód - przekazanie do uboju");
            sb.AppendLine($"#   P  = Padnięcie");
            sb.AppendLine($"#   S  = Strata (kradzież, ucieczka)");
            sb.AppendLine();

            // Nagłówek
            sb.AppendLine(string.Join(separator.ToString(), new[] {
                "Lp",
                "Typ zdarzenia",
                "Liczba sztuk drobiu",
                "Data zdarzenia",
                "Przekazane do rzeźni (numer)",
                "Uwagi"
            }));

            // Dane
            foreach (var p in dane.Pozycje)
            {
                sb.AppendLine(string.Join(separator.ToString(), new[] {
                    p.Lp.ToString(),
                    p.TypZdarzenia ?? TypZdarzeniaZSSD.RozchodUboj,
                    p.LiczbaSztuk.ToString(),
                    p.DataZdarzenia.ToString("yyyy-MM-dd"),
                    p.PrzyjeteZDzialalnosci ?? "",
                    p.Uwagi ?? ""
                }));
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            return filePath;
        }

        #endregion

        #region Pomocnicze

        /// <summary>
        /// Otwiera folder eksportu w Eksploratorze Windows
        /// </summary>
        public void OtworzFolderEksportu()
        {
            System.Diagnostics.Process.Start("explorer.exe", _exportPath);
        }

        /// <summary>
        /// Otwiera Eksplorator z zaznaczonym plikiem
        /// </summary>
        public void OtworzFolderZPlikiem(string filePath)
        {
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
        }

        /// <summary>
        /// Zwraca ścieżkę do folderu eksportu
        /// </summary>
        public string GetExportPath() => _exportPath;

        /// <summary>
        /// Generuje raport podsumowujący eksport
        /// </summary>
        public string GenerujRaport(string typDokumentu, string sciezkaPliku, int liczbaPozycji, int sumaSztuk, DateTime dataZdarzenia)
        {
            return $"=== EKSPORT {typDokumentu.ToUpper()} ===\n\n" +
                   $"Plik: {sciezkaPliku}\n\n" +
                   $"Statystyki:\n" +
                   $"   - Pozycji: {liczbaPozycji}\n" +
                   $"   - Suma sztuk: {sumaSztuk:N0}\n" +
                   $"   - Data zdarzenia: {dataZdarzenia:yyyy-MM-dd}\n\n" +
                   $"Instrukcja importu w portalu IRZplus:\n" +
                   $"   1. Otwórz portal: https://irz.arimr.gov.pl\n" +
                   $"   2. Zaloguj się na swoje konto\n" +
                   $"   3. Przejdź do: Dokumenty -> {typDokumentu}\n" +
                   $"   4. Kliknij przycisk 'Import z pliku XML' lub 'Wczytaj dane z pliku CSV/TXT'\n" +
                   $"   5. Wybierz wygenerowany plik\n" +
                   $"   6. Sprawdź dane i zatwierdź zgłoszenie\n";
        }

        #endregion
    }
}
