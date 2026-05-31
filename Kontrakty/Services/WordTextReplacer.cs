using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Kalendarz1.Kontrakty.Services
{
    /// <summary>
    /// Wspólna podmiana [tokenów] w docx — logika wzorowana na UmowyForm.ReplacePlaceholdersInDocx_ParagraphWise
    /// (UmowyForm.cs:711), ale samodzielna i bez zależności od WinForms. UmowyForm pozostaje NIETKNIĘTY.
    /// Replace per-akapit: odporne na token rozbity na kilka runów (Word często tak dzieli tekst).
    /// </summary>
    public static class WordTextReplacer
    {
        /// <summary>Podmienia tokeny w obrębie dowolnego elementu (cały Body, pojedynczy TableRow itp.).</summary>
        public static void ReplaceInScope(OpenXmlElement scope, IDictionary<string, string?> replacements)
        {
            foreach (var p in scope.Descendants<Paragraph>())
            {
                var original = p.InnerText ?? string.Empty;
                var replaced = original;
                foreach (var kv in replacements)
                {
                    if (string.IsNullOrEmpty(kv.Key)) continue;
                    if (replaced.Contains(kv.Key)) replaced = replaced.Replace(kv.Key, kv.Value ?? string.Empty);
                }
                if (string.Equals(original, replaced, StringComparison.Ordinal)) continue;

                var firstRunProps = p.Elements<Run>().FirstOrDefault()?.RunProperties?.CloneNode(true) as RunProperties;
                p.RemoveAllChildren<Run>();
                var newRun = new Run(new Text(replaced) { Space = SpaceProcessingModeValues.Preserve });
                if (firstRunProps != null) newRun.RunProperties = firstRunProps;
                p.Append(newRun);
            }
        }

        /// <summary>Wygodowa nakładka: otwiera plik, podmienia w body+headers+footers, zapisuje.</summary>
        public static void ReplaceInDocx(string docxPath, IDictionary<string, string?> replacements)
        {
            using var doc = WordprocessingDocument.Open(docxPath, true);
            var main = doc.MainDocumentPart;
            if (main?.Document?.Body != null) ReplaceInScope(main.Document.Body, replacements);
            if (main != null)
            {
                foreach (var hp in main.HeaderParts) ReplaceInScope(hp.Header, replacements);
                foreach (var fp in main.FooterParts) ReplaceInScope(fp.Footer, replacements);
                main.Document.Save();
            }
        }
    }
}
