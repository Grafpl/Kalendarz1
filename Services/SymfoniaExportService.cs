using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml.Linq;

namespace Kalendarz1.Services
{
    /// <summary>
    /// Serwis eksportu dokumentów do Sage Handel Symfonia 2025.3
    /// Obsługuje formaty: XML (natywny), EDI, CSV
    /// </summary>
    public class SymfoniaExportService
    {
        private readonly string _exportPath;
        private readonly string _firmaNIP;
        private readonly string _firmaRegon;

        public SymfoniaExportService(string exportPath = null)
        {
            _exportPath = exportPath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Symfonia_Export");

            // Dane firmy Piórkowscy
            _firmaNIP = "7752632016";
            _firmaRegon = "101432572";

            if (!Directory.Exists(_exportPath))
                Directory.CreateDirectory(_exportPath);
        }

        /// <summary>
        /// Eksportuje rozliczenie jako fakturę zakupu do Symfonii
        /// </summary>
        public ExportResult ExportRozliczenieToSymfonia(RozliczenieData rozliczenie)
        {
            try
            {
                // Generuj XML w formacie Symfonia Handel
                var xml = GenerateSymfoniaXml(rozliczenie);

                string fileName = $"FZ_{rozliczenie.NumerDokumentu}_{DateTime.Now:yyyyMMdd_HHmmss}.xml";
                string filePath = Path.Combine(_exportPath, fileName);

                xml.Save(filePath);

                return new ExportResult
                {
                    Success = true,
                    FilePath = filePath,
                    Message = $"Wyeksportowano do: {filePath}"
                };
            }
            catch (Exception ex)
            {
                return new ExportResult
                {
                    Success = false,
                    Message = $"Błąd eksportu: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Eksportuje wiele rozliczeń jako paczkę importową
        /// </summary>
        public ExportResult ExportBatchToSymfonia(List<RozliczenieData> rozliczenia)
        {
            try
            {
                var root = new XElement("DokumentyHandlowe",
                    new XAttribute("wersja", "2.0"),
                    new XAttribute("program", "Symfonia Handel"),
                    new XAttribute("dataEksportu", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                );

                foreach (var rozliczenie in rozliczenia)
                {
                    var dokument = GenerateDokumentElement(rozliczenie);
                    root.Add(dokument);
                }

                var xml = new XDocument(
                    new XDeclaration("1.0", "utf-8", "yes"),
                    root
                );

                string fileName = $"FZ_BATCH_{DateTime.Now:yyyyMMdd_HHmmss}.xml";
                string filePath = Path.Combine(_exportPath, fileName);

                xml.Save(filePath);

                return new ExportResult
                {
                    Success = true,
                    FilePath = filePath,
                    Message = $"Wyeksportowano {rozliczenia.Count} dokumentów do: {filePath}"
                };
            }
            catch (Exception ex)
            {
                return new ExportResult
                {
                    Success = false,
                    Message = $"Błąd eksportu: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Generuje XML w formacie Symfonia Handel 2025
        /// </summary>
        private XDocument GenerateSymfoniaXml(RozliczenieData rozliczenie)
        {
            var dokument = GenerateDokumentElement(rozliczenie);

            return new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XElement("DokumentyHandlowe",
                    new XAttribute("wersja", "2.0"),
                    new XAttribute("program", "Symfonia Handel"),
                    dokument
                )
            );
        }

        private XElement GenerateDokumentElement(RozliczenieData rozliczenie)
        {
            var pozycje = new XElement("Pozycje");
            int lp = 1;

            foreach (var poz in rozliczenie.Pozycje)
            {
                pozycje.Add(new XElement("Pozycja",
                    new XElement("Lp", lp++),
                    new XElement("KodTowaru", poz.KodTowaru),
                    new XElement("NazwaTowaru", poz.NazwaTowaru),
                    new XElement("JednostkaMiary", "kg"),
                    new XElement("Ilosc", FormatDecimal(poz.Ilosc)),
                    new XElement("CenaJednostkowa", FormatDecimal(poz.CenaJednostkowa)),
                    new XElement("WartoscNetto", FormatDecimal(poz.WartoscNetto)),
                    new XElement("StawkaVAT", poz.StawkaVAT),
                    new XElement("WartoscVAT", FormatDecimal(poz.WartoscVAT)),
                    new XElement("WartoscBrutto", FormatDecimal(poz.WartoscBrutto)),
                    new XElement("Uwagi", poz.Uwagi ?? "")
                ));
            }

            return new XElement("Dokument",
                new XElement("TypDokumentu", "FZ"), // Faktura Zakupu
                new XElement("NumerDokumentu", rozliczenie.NumerDokumentu),
                new XElement("DataWystawienia", rozliczenie.DataWystawienia.ToString("yyyy-MM-dd")),
                new XElement("DataSprzedazy", rozliczenie.DataSprzedazy.ToString("yyyy-MM-dd")),
                new XElement("DataPlatnosci", rozliczenie.DataPlatnosci.ToString("yyyy-MM-dd")),
                new XElement("FormaPlatnosci", rozliczenie.FormaPlatnosci),
                new XElement("Waluta", "PLN"),

                new XElement("Kontrahent",
                    new XElement("Typ", "Dostawca"),
                    new XElement("Nazwa", rozliczenie.KontrahentNazwa),
                    new XElement("NIP", rozliczenie.KontrahentNIP ?? ""),
                    new XElement("REGON", rozliczenie.KontrahentRegon ?? ""),
                    new XElement("Adres",
                        new XElement("Ulica", rozliczenie.KontrahentUlica ?? ""),
                        new XElement("Miasto", rozliczenie.KontrahentMiasto ?? ""),
                        new XElement("KodPocztowy", rozliczenie.KontrahentKodPocztowy ?? ""),
                        new XElement("Kraj", "PL")
                    ),
                    new XElement("NumerWeterynaryjny", rozliczenie.NumerWeterynaryjny ?? ""),
                    new XElement("Email", rozliczenie.KontrahentEmail ?? ""),
                    new XElement("Telefon", rozliczenie.KontrahentTelefon ?? "")
                ),

                new XElement("NaszaFirma",
                    new XElement("NIP", _firmaNIP),
                    new XElement("REGON", _firmaRegon)
                ),

                pozycje,

                new XElement("Podsumowanie",
                    new XElement("WartoscNetto", FormatDecimal(rozliczenie.WartoscNetto)),
                    new XElement("WartoscVAT", FormatDecimal(rozliczenie.WartoscVAT)),
                    new XElement("WartoscBrutto", FormatDecimal(rozliczenie.WartoscBrutto)),
                    new XElement("DoZaplaty", FormatDecimal(rozliczenie.DoZaplaty)),
                    new XElement("Zaplacono", FormatDecimal(rozliczenie.Zaplacono)),
                    new XElement("Pozostalo", FormatDecimal(rozliczenie.Pozostalo))
                ),

                new XElement("DaneDostawcy",
                    new XElement("IloscSztuk", rozliczenie.IloscSztuk),
                    new XElement("WagaBrutto", FormatDecimal(rozliczenie.WagaBrutto)),
                    new XElement("WagaTara", FormatDecimal(rozliczenie.WagaTara)),
                    new XElement("WagaNetto", FormatDecimal(rozliczenie.WagaNetto)),
                    new XElement("Ubytek", FormatDecimal(rozliczenie.Ubytek)),
                    new XElement("NumerPojazdu", rozliczenie.NumerPojazdu ?? "")
                ),

                new XElement("Uwagi", rozliczenie.Uwagi ?? "")
            );
        }

        /// <summary>
        /// Eksportuje do formatu CSV dla starszych wersji Symfonii
        /// </summary>
        public ExportResult ExportToCsv(RozliczenieData rozliczenie)
        {
            try
            {
                var sb = new StringBuilder();

                // Nagłówek
                sb.AppendLine("TYP;NUMER;DATA_WYST;DATA_SPRZ;KONTRAHENT;NIP;WARTOSC_NETTO;VAT;BRUTTO;FORMA_PLAT");

                // Dane główne
                sb.AppendLine($"FZ;{rozliczenie.NumerDokumentu};" +
                    $"{rozliczenie.DataWystawienia:yyyy-MM-dd};" +
                    $"{rozliczenie.DataSprzedazy:yyyy-MM-dd};" +
                    $"\"{rozliczenie.KontrahentNazwa}\";" +
                    $"{rozliczenie.KontrahentNIP ?? ""};" +
                    $"{FormatDecimal(rozliczenie.WartoscNetto)};" +
                    $"{FormatDecimal(rozliczenie.WartoscVAT)};" +
                    $"{FormatDecimal(rozliczenie.WartoscBrutto)};" +
                    $"{rozliczenie.FormaPlatnosci}");

                sb.AppendLine();
                sb.AppendLine("LP;KOD;NAZWA;JM;ILOSC;CENA;NETTO;VAT%;VAT;BRUTTO");

                int lp = 1;
                foreach (var poz in rozliczenie.Pozycje)
                {
                    sb.AppendLine($"{lp++};{poz.KodTowaru};\"{poz.NazwaTowaru}\";kg;" +
                        $"{FormatDecimal(poz.Ilosc)};" +
                        $"{FormatDecimal(poz.CenaJednostkowa)};" +
                        $"{FormatDecimal(poz.WartoscNetto)};" +
                        $"{poz.StawkaVAT};" +
                        $"{FormatDecimal(poz.WartoscVAT)};" +
                        $"{FormatDecimal(poz.WartoscBrutto)}");
                }

                string fileName = $"FZ_{rozliczenie.NumerDokumentu}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                string filePath = Path.Combine(_exportPath, fileName);

                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);

                return new ExportResult
                {
                    Success = true,
                    FilePath = filePath,
                    Message = $"Wyeksportowano CSV do: {filePath}"
                };
            }
            catch (Exception ex)
            {
                return new ExportResult
                {
                    Success = false,
                    Message = $"Błąd eksportu CSV: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Eksportuje do formatu EDI (Electronic Data Interchange)
        /// </summary>
        public ExportResult ExportToEdi(RozliczenieData rozliczenie)
        {
            try
            {
                var sb = new StringBuilder();

                // Segment UNH - nagłówek wiadomości
                sb.AppendLine($"UNH+1+INVOIC:D:96A:UN'");

                // Segment BGM - początek wiadomości
                sb.AppendLine($"BGM+380+{rozliczenie.NumerDokumentu}+9'");

                // Segment DTM - daty
                sb.AppendLine($"DTM+137:{rozliczenie.DataWystawienia:yyyyMMdd}:102'");
                sb.AppendLine($"DTM+35:{rozliczenie.DataSprzedazy:yyyyMMdd}:102'");

                // Segment NAD - kontrahent (dostawca)
                sb.AppendLine($"NAD+SU+++{rozliczenie.KontrahentNazwa}+{rozliczenie.KontrahentUlica}+" +
                    $"{rozliczenie.KontrahentMiasto}++{rozliczenie.KontrahentKodPocztowy}+PL'");

                // Pozycje
                int lp = 1;
                foreach (var poz in rozliczenie.Pozycje)
                {
                    sb.AppendLine($"LIN+{lp}++{poz.KodTowaru}:SA'");
                    sb.AppendLine($"IMD+F++:::{poz.NazwaTowaru}'");
                    sb.AppendLine($"QTY+47:{FormatDecimal(poz.Ilosc)}:KGM'");
                    sb.AppendLine($"PRI+AAA:{FormatDecimal(poz.CenaJednostkowa)}::NTP'");
                    sb.AppendLine($"MOA+203:{FormatDecimal(poz.WartoscNetto)}'");
                    lp++;
                }

                // Segment UNS - separacja sekcji
                sb.AppendLine("UNS+S'");

                // Segment MOA - kwoty
                sb.AppendLine($"MOA+86:{FormatDecimal(rozliczenie.WartoscBrutto)}'");
                sb.AppendLine($"MOA+125:{FormatDecimal(rozliczenie.WartoscNetto)}'");
                sb.AppendLine($"MOA+176:{FormatDecimal(rozliczenie.WartoscVAT)}'");

                // Segment UNT - zakończenie
                sb.AppendLine($"UNT+{10 + rozliczenie.Pozycje.Count * 5}+1'");

                string fileName = $"FZ_{rozliczenie.NumerDokumentu}_{DateTime.Now:yyyyMMdd_HHmmss}.edi";
                string filePath = Path.Combine(_exportPath, fileName);

                File.WriteAllText(filePath, sb.ToString(), Encoding.ASCII);

                return new ExportResult
                {
                    Success = true,
                    FilePath = filePath,
                    Message = $"Wyeksportowano EDI do: {filePath}"
                };
            }
            catch (Exception ex)
            {
                return new ExportResult
                {
                    Success = false,
                    Message = $"Błąd eksportu EDI: {ex.Message}"
                };
            }
        }

        private string FormatDecimal(decimal value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Pobiera ścieżkę folderu eksportu
        /// </summary>
        public string GetExportPath() => _exportPath;
    }

    #region Data Models

    public class RozliczenieData
    {
        public string NumerDokumentu { get; set; }
        public DateTime DataWystawienia { get; set; }
        public DateTime DataSprzedazy { get; set; }
        public DateTime DataPlatnosci { get; set; }
        public string FormaPlatnosci { get; set; } = "Przelew";

        // Kontrahent (hodowca/dostawca)
        public string KontrahentNazwa { get; set; }
        public string KontrahentNIP { get; set; }
        public string KontrahentRegon { get; set; }
        public string KontrahentUlica { get; set; }
        public string KontrahentMiasto { get; set; }
        public string KontrahentKodPocztowy { get; set; }
        public string KontrahentEmail { get; set; }
        public string KontrahentTelefon { get; set; }
        public string NumerWeterynaryjny { get; set; }

        // Pozycje dokumentu
        public List<PozycjaRozliczenia> Pozycje { get; set; } = new List<PozycjaRozliczenia>();

        // Podsumowanie
        public decimal WartoscNetto { get; set; }
        public decimal WartoscVAT { get; set; }
        public decimal WartoscBrutto { get; set; }
        public decimal DoZaplaty { get; set; }
        public decimal Zaplacono { get; set; }
        public decimal Pozostalo { get; set; }

        // Dane dostawy drobiu
        public int IloscSztuk { get; set; }
        public decimal WagaBrutto { get; set; }
        public decimal WagaTara { get; set; }
        public decimal WagaNetto { get; set; }
        public decimal Ubytek { get; set; }
        public string NumerPojazdu { get; set; }
        public string Uwagi { get; set; }
    }

    public class PozycjaRozliczenia
    {
        public string KodTowaru { get; set; }
        public string NazwaTowaru { get; set; }
        public decimal Ilosc { get; set; }
        public decimal CenaJednostkowa { get; set; }
        public decimal WartoscNetto { get; set; }
        public string StawkaVAT { get; set; } = "5%"; // Drób - 5% VAT
        public decimal WartoscVAT { get; set; }
        public decimal WartoscBrutto { get; set; }
        public string Uwagi { get; set; }
    }

    

    #endregion
}
