// ════════════════════════════════════════════════════════════════════════════
// WordTemplateService.cs — generator umów Word z szablonu + bookmarki
// Część 4 audytu (2026-05-23)
// Target: Kontrakty/Services/WordTemplateService.cs
//
// Zależności:
//   - DocumentFormat.OpenXml 3.4.1 (już w Kalendarz1.csproj:78)
//
// Wzorzec użycia:
//   var values = new Dictionary<string,string> {
//       ["bm_NumerKontraktu"] = "1/27",
//       ["bm_NazwaHodowcy"]   = "Jan Kowalski",
//       ...
//   };
//   var svc = new WordTemplateService();
//   svc.GenerateContract(templatePath, outputPath, values);
//   Process.Start("explorer", outputPath); // otwórz Word
// ════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;                 // ← SpaceProcessingModeValues
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Kalendarz1.Kontrakty.Models;

namespace Kalendarz1.Kontrakty.Services
{
    public class WordTemplateService
    {
        /// <summary>
        /// Generuje umowę Word z szablonu, podstawiając wartości za bookmarki.
        /// </summary>
        /// <param name="templatePath">Pełna ścieżka do szablonu .docx (UNC)</param>
        /// <param name="outputPath">Pełna ścieżka gdzie zapisać wygenerowany .docx (UNC)</param>
        /// <param name="values">Słownik bookmark_name → wartość tekstowa</param>
        /// <returns>Ścieżka do wygenerowanego pliku</returns>
        public string GenerateContract(string templatePath, string outputPath, Dictionary<string, string> values)
        {
            if (!File.Exists(templatePath))
                throw new FileNotFoundException($"Szablon Word nie istnieje: {templatePath}");

            var outDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
                Directory.CreateDirectory(outDir);

            File.Copy(templatePath, outputPath, overwrite: true);

            using var doc = WordprocessingDocument.Open(outputPath, isEditable: true);
            var body = doc.MainDocumentPart?.Document?.Body
                ?? throw new InvalidOperationException("Word nie ma body — uszkodzony szablon?");

            ReplaceBookmarks(body, values);

            doc.MainDocumentPart!.Document.Save();
            return outputPath;
        }

        /// <summary>
        /// Pomocnik: buduje słownik wartości z DTO kontraktu + danych hodowcy.
        /// </summary>
        public Dictionary<string, string> BuildValuesFromKontrakt(KontraktDto k, string nazwaPiorkowscy = "Piórkowscy")
        {
            return new Dictionary<string, string>
            {
                ["bm_NumerKontraktu"] = k.NumerKontraktu,
                ["bm_DataPodpisania"] = FormatDate(k.DataPodpisania ?? DateTime.Today),
                ["bm_NazwaHodowcy"]   = k.NazwaHodowcySnapshot ?? "(brak)",
                ["bm_AdresHodowcy"]   = k.AdresSnapshot ?? "(brak)",
                ["bm_Nip"]            = k.NipSnapshot ?? "(brak)",
                ["bm_NrGospodarstwa"] = k.NrGospodarstwaSnapshot ?? "(brak)",
                ["bm_ProcentUbytku"]  = $"{k.ProcentUbytku:N2} %".Replace('.', ','),
                ["bm_TypCeny"]        = k.TypCeny,
                ["bm_Cena"]           = k.Cena.HasValue
                    ? $"{k.Cena.Value:N2} zł/kg netto".Replace('.', ',')
                    : "wg cennika dnia",
                ["bm_TerminPlatnosci"]= $"{k.TerminPlatnosciDni} dni",
                ["bm_DataOd"]         = FormatDate(k.DataObowiazujeOd),
                ["bm_DataDo"]         = k.DataObowiazujeDo.HasValue
                    ? FormatDate(k.DataObowiazujeDo.Value)
                    : "na czas nieokreślony",
                ["bm_OkresWypowiedzenia"] = $"{k.OkresWypowiedzeniaDni} dni",
                ["bm_NazwaPiorkowscy"]= nazwaPiorkowscy,
                ["bm_RozliczanaWaga"] = k.RozliczanaWaga switch
                {
                    "NETTO_HODOWCY" => "waga netto deklarowana przez Hodowcę",
                    "NETTO_UBOJNI"  => "waga netto z ważenia w Ubojni",
                    _ => k.RozliczanaWaga
                }
            };
        }

        // ────────────────────────────────────────────────────────────────────
        // PRIVATE — bookmark replacement logic
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Strategia: dla każdego bookmark znajdujemy BookmarkStart → wstawiamy Run z nowym tekstem
        /// bezpośrednio PO bookmarku (zachowując oryginalny tekst zakładki jako placeholder/blank).
        /// Word renderuje: oryginalny tekst → nasz nowy Run.
        ///
        /// LIMITACJA: zachowuje oryginalny tekst placeholder w dokumencie (zwykle nieistotne,
        /// bo placeholder w szablonie to puste pole). Dla pełnej zamiany — używać MERGEFIELD zamiast bookmark.
        /// </summary>
        private static void ReplaceBookmarks(Body body, Dictionary<string, string> values)
        {
            var bookmarks = body.Descendants<BookmarkStart>().ToList();
            foreach (var bm in bookmarks)
            {
                if (bm.Name == null) continue;
                if (!values.TryGetValue(bm.Name!.Value!, out var newText)) continue;

                var run = new Run(new Text(newText) { Space = SpaceProcessingModeValues.Preserve });
                bm.Parent?.InsertAfter(run, bm);
            }
        }

        private static string FormatDate(DateTime d)
        {
            // "26 maja 2026" po polsku
            var pl = new CultureInfo("pl-PL");
            return d.ToString("d MMMM yyyy", pl);
        }
    }
}
