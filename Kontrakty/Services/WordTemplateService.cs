using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Kalendarz1.Kontrakty.Models;

namespace Kalendarz1.Kontrakty.Services
{
    /// <summary>
    /// Generator umów Word (OpenXML 3.4.1). Wypełnia szablon dwoma mechanizmami:
    ///   1) bookmarki Worda (np. bm_NumerKontraktu) — wstawia tekst po BookmarkStart,
    ///   2) tokeny [Klucz] w treści (np. [NumerKontraktu]) — replace w runach.
    /// Szablon = .docx z folderu sieciowego; output kopiowany i wypełniany.
    /// </summary>
    public class WordTemplateService
    {
        private static readonly string[] MiesiacePl =
        {
            "stycznia","lutego","marca","kwietnia","maja","czerwca",
            "lipca","sierpnia","września","października","listopada","grudnia"
        };

        public static string DataPl(DateTime? d) =>
            d is null ? "—" : $"{d.Value.Day} {MiesiacePl[d.Value.Month - 1]} {d.Value.Year}";

        /// <summary>Wyciąga czytelny tekst z docx (akapity + wiersze tabel) — do podglądu treści umowy.</summary>
        public static List<string> WyciagnijTekst(string docxPath)
        {
            var linie = new List<string>();
            using var doc = WordprocessingDocument.Open(docxPath, false);
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body == null) return linie;
            foreach (var el in body.Elements())
            {
                if (el is Paragraph p)
                {
                    linie.Add((p.InnerText ?? "").Trim());
                }
                else if (el is Table tbl)
                {
                    linie.Add("");
                    foreach (var row in tbl.Elements<TableRow>())
                    {
                        var cells = row.Elements<TableCell>().Select(c => (c.InnerText ?? "").Trim());
                        linie.Add("    " + string.Join("   |   ", cells));
                    }
                    linie.Add("");
                }
            }
            return linie;
        }

        /// <summary>Zwraca słownik wartości dla bookmarków/tokenów na podstawie kontraktu + wersji.</summary>
        public static Dictionary<string, string> BuildValues(KontraktDetail h, KontraktWersja w)
        {
            var ci = new CultureInfo("pl-PL");
            string cena = w.Cena.HasValue ? w.Cena.Value.ToString("0.00", ci) + " zł/kg" : "wg cennika dnia";
            if (w.DodatekZl is { } dod && dod != 0) cena += " + " + dod.ToString("0.00", ci) + " zł/kg";

            var bm = new Dictionary<string, string>
            {
                ["NumerKontraktu"] = h.NumerKontraktu,
                ["NazwaHodowcy"] = h.NazwaHodowcySnapshot ?? "",
                ["Nip"] = h.NipSnapshot ?? "",
                ["NrGospodarstwa"] = h.NrGospodarstwaSnapshot ?? "",
                ["AdresHodowcy"] = h.AdresSnapshot ?? "",
                ["NazwaPiorkowscy"] = h.PodmiotLabel,
                ["TypKontraktu"] = h.TypLabel,
                ["DataPodpisania"] = DataPl(w.DataPodpisania ?? DateTime.Today),
                ["DataOd"] = DataPl(w.ObowiazujeOd),
                ["DataDo"] = w.ObowiazujeDo is null ? "na czas nieokreślony" : DataPl(w.ObowiazujeDo),
                ["OkresWypowiedzenia"] = $"{w.OkresWypowiedzeniaDni} dni",
                ["ProcentUbytku"] = w.ProcentUbytku.HasValue ? w.ProcentUbytku.Value.ToString("0.00", ci) + " %" : "—",
                ["TypCeny"] = w.TypCeny,
                ["Cena"] = cena,
                ["TerminPlatnosci"] = $"{w.TerminPlatnosciDni} dni",
                ["RozliczanaWaga"] = w.RozliczanaWaga == "NETTO_UBOJNI" ? "netto ubojni" : "netto hodowcy",
                ["MinimalnaIlosc"] = w.MinimalnaIloscSzt.HasValue ? $"{w.MinimalnaIloscSzt} szt." : "—",
                ["Klauzule"] = w.KlauzuleSzczegolne ?? "",
            };

            // klucze dostępne także jako bm_<Klucz> (bookmark) i [<Klucz>] (token)
            var full = new Dictionary<string, string>();
            foreach (var kv in bm)
            {
                full["bm_" + kv.Key] = kv.Value;
                full[kv.Key] = kv.Value;
            }
            return full;
        }

        /// <summary>Wypełnia szablon i zapisuje pod outputPath. Zwraca outputPath.</summary>
        /// <summary>Skalarne tokeny [Pole] dla szablonu kontraktacji (wspólne: kreator + karta).</summary>
        public static Dictionary<string, string?> BuildKontraktacjaTokens(KontraktDetail h, KontraktWersja w, string numer)
        {
            var pl = new CultureInfo("pl-PL");
            string D(decimal? v) => v?.ToString("0.##", pl) ?? "";
            return new Dictionary<string, string?>
            {
                ["[NumerUmowy]"] = numer,
                ["[Dostawca]"] = h.NazwaHodowcySnapshot,
                ["[NIP]"] = h.NipSnapshot,
                ["[NumerGospodarstwa]"] = h.NrGospodarstwaSnapshot,
                ["[AdresHodowcy]"] = h.AdresSnapshot,
                ["[EmailRODO]"] = h.EmailRODO,
                ["[PESEL]"] = h.PeselSnapshot,
                ["[REGON]"] = h.RegonSnapshot,
                ["[NrDowodu]"] = h.NrDowoduSnapshot,
                ["[TelefonProducenta]"] = h.TelefonSnapshot,
                ["[Podmiot]"] = h.PodmiotLabel,
                ["[DataZawarcia]"] = (w.DataPodpisania ?? DateTime.Today).ToString("dd.MM.yyyy"),
                ["[DataOd]"] = w.ObowiazujeOd.ToString("dd.MM.yyyy"),
                ["[DataDo]"] = w.ObowiazujeDo?.ToString("dd.MM.yyyy") ?? "na czas nieokreślony",
                ["[TypCeny]"] = w.TypCeny,
                ["[Cena]"] = D(w.Cena),
                ["[DodatekStały]"] = D(w.DodatekZl),
                ["[Ubytek]"] = D(w.ProcentUbytku),
                ["[TerminPlatnosci]"] = w.TerminPlatnosciDni.ToString(),
                ["[DostawcaPaszy]"] = w.DostawcaPaszyNazwa,
                ["[DostawcaPiskląt]"] = w.DostawcaPisklatNazwa,
                ["[BonusJesli]"] = w.BonusOpis,
                ["[InneUstalenia]"] = w.KlauzuleSzczegolne,
                ["[KonfiskatyPokrywa]"] = w.KonfiskatyHodowca ? "hodowca (potrącane od rozliczenia)" : "ubojnia",
            };
        }

        public string Generuj(string templatePath, string outputPath, IDictionary<string, string> values)
        {
            if (!File.Exists(templatePath))
                throw new FileNotFoundException("Szablon Word nie istnieje: " + templatePath, templatePath);

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.Copy(templatePath, outputPath, overwrite: true);

            using var doc = WordprocessingDocument.Open(outputPath, isEditable: true);
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body is null) return outputPath;

            // 1) bookmarki
            foreach (var start in body.Descendants<BookmarkStart>().ToList())
            {
                if (start.Name?.Value is not string name) continue;
                if (!values.TryGetValue(name, out var txt)) continue;
                var run = new Run(new Text(txt) { Space = SpaceProcessingModeValues.Preserve });
                start.Parent?.InsertAfter(run, start);
            }

            // 2) tokeny [Klucz] (gdy mieszczą się w jednym runie)
            foreach (var t in body.Descendants<Text>())
            {
                string s = t.Text;
                foreach (var kv in values)
                {
                    string token = "[" + kv.Key + "]";
                    if (s.Contains(token)) s = s.Replace(token, kv.Value);
                }
                if (!ReferenceEquals(s, t.Text) && s != t.Text) t.Text = s;
            }

            doc.MainDocumentPart!.Document.Save();
            return outputPath;
        }

        // ════════════════════════════════════════════════════════════════════
        // KONTRAKTACJA: pełny szablon ([tokeny] + tabela harmonogramu BMK_HARMONOGRAM)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Generuje umowę kontraktacji: kopiuje szablon, wypełnia tabelę harmonogramu (klonując
        /// wiersz-wzorzec z tokenami [Cykl_*]), potem podmienia skalarne [tokeny] (Dostawca, NIP...).
        /// `tokeny` mają klucze DOKŁADNIE jak w szablonie, np. "[Dostawca]".
        /// </summary>
        public string GenerujKontraktacja(string templatePath, string outputPath,
            IDictionary<string, string?> tokeny, List<HarmonogramCykl> cykle, string bookmarkName = "BMK_HARMONOGRAM")
        {
            if (!File.Exists(templatePath))
                throw new FileNotFoundException("Szablon Word nie istnieje: " + templatePath, templatePath);

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.Copy(templatePath, outputPath, overwrite: true);

            using var doc = WordprocessingDocument.Open(outputPath, isEditable: true);
            var main = doc.MainDocumentPart;
            var body = main?.Document?.Body;
            if (body is null) return outputPath;

            FillScheduleTable(doc, bookmarkName, cykle);     // 1) wiersze harmonogramu
            UsunPusteAkapity(body, tokeny);                  // 2) wytnij akapity z samymi pustymi tokenami (np. brak ceny/dodatku)
            WordTextReplacer.ReplaceInScope(body, tokeny);   // 3) skalarne [tokeny] w body
            foreach (var hp in main!.HeaderParts) WordTextReplacer.ReplaceInScope(hp.Header, tokeny);
            foreach (var fp in main.FooterParts) WordTextReplacer.ReplaceInScope(fp.Footer, tokeny);

            main.Document.Save();
            return outputPath;
        }

        /// <summary>
        /// Wypełnia tabelę harmonogramu: znajduje tabelę po bookmarku BMK_HARMONOGRAM (fallback: tabela
        /// zawierająca tokeny [Cykl_*]), klonuje wiersz-wzorzec per cykl i podmienia tokeny.
        /// </summary>
        public void FillScheduleTable(WordprocessingDocument doc, string bookmarkName, List<HarmonogramCykl> cykle)
        {
            if (cykle is null || cykle.Count == 0) return;
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body is null) return;

            var bm = body.Descendants<BookmarkStart>().FirstOrDefault(b => b.Name?.Value == bookmarkName);
            Table? table = bm?.Ancestors<Table>().FirstOrDefault()
                        ?? bm?.ElementsAfter().OfType<Table>().FirstOrDefault()
                        ?? body.Descendants<Table>().FirstOrDefault(t => t.InnerText.Contains("[Cykl_"));
            if (table is null) return;

            var template = table.Elements<TableRow>().LastOrDefault(r => r.InnerText.Contains("[Cykl_"));
            if (template is null) return;

            foreach (var c in cykle)
            {
                var nowy = (TableRow)template.CloneNode(true);
                WordTextReplacer.ReplaceInScope(nowy, BuildCyklTokens(c));
                template.InsertBeforeSelf(nowy);
            }
            template.Remove();   // usuń wiersz-wzorzec z tokenami
        }

        /// <summary>
        /// Usuwa akapity, w których WSZYSTKIE występujące tokeny [..] mają pustą wartość
        /// (np. „Dodatek stały: [DodatekStały] zł/kg" gdy brak dodatku → cały wiersz znika).
        /// Akapit zostaje, jeśli ma choć jeden token z wartością (lub nie ma tokenów wcale).
        /// Działa tylko na akapity z tokenami SKALARNymi (harmonogram [Cykl_*] nie jest w `tokeny`).
        /// </summary>
        private static void UsunPusteAkapity(OpenXmlElement scope, IDictionary<string, string?> tokeny)
        {
            var puste = new HashSet<string>(tokeny.Where(kv => string.IsNullOrWhiteSpace(kv.Value)).Select(kv => kv.Key));
            var klucze = tokeny.Keys.ToList();
            foreach (var p in scope.Descendants<Paragraph>().ToList())
            {
                string t = p.InnerText ?? "";
                if (t.Length == 0) continue;
                var obecne = klucze.Where(k => t.Contains(k)).ToList();
                if (obecne.Count > 0 && obecne.All(k => puste.Contains(k)))
                    p.Remove();
            }
        }

        private static IDictionary<string, string?> BuildCyklTokens(HarmonogramCykl c) => new Dictionary<string, string?>
        {
            ["[Cykl_Nr]"] = c.NrCyklu.ToString(),
            ["[Cykl_DataWstawienia]"] = c.DataWstawienia?.ToString("dd.MM.yyyy") ?? "",
            ["[Cykl_IloscWstaw]"] = c.IloscWstawiona?.ToString() ?? "",
            ["[Cykl_IloscUbiorki]"] = c.IloscUbiorki?.ToString() ?? "",
            ["[Cykl_DzienUbiorki]"] = c.DzienUbiorki?.ToString() ?? "",
            ["[Cykl_DataUboju]"] = c.DataUbojuKoncowego?.ToString("dd.MM.yyyy") ?? "",
            ["[Cykl_IloscUboju]"] = c.IloscUboju?.ToString() ?? "",
        };
    }
}
