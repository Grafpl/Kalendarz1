using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Kalendarz1.CentrumNagranAI.Services
{
    /// <summary>
    /// Caption strukturalny (JSON) z vocabulary grounding (E1+E2).
    /// Zamiast wolnego tekstu, AI generuje JSON o sztywnym schemacie używając
    /// słownictwa zakładu. Daje:
    ///  - lepsze embeddings (powtarzalne terminy)
    ///  - łatwy text-search (po polach JSON)
    ///  - tagowanie obiektów bez YOLO
    /// </summary>
    public static class CaptionService
    {
        public class StructuredCaption
        {
            public string Lokalizacja { get; set; } = string.Empty;     // "hala uboju", "pakowalnia", "rampa"
            public List<string> Ludzie { get; set; } = new();           // ["pracownik w czepku", "kierowca"]
            public List<string> Obiekty { get; set; } = new();          // ["wózek", "klatka transportowa"]
            public List<string> Aktywnosc { get; set; } = new();        // ["linia pracuje", "przenośnik w ruchu"]
            public string Anomalia { get; set; } = string.Empty;        // pusty lub konkretny opis
            public string CzasDnia { get; set; } = string.Empty;        // "dzień", "noc", "świt"
            public string Caption { get; set; } = string.Empty;         // 1 zdanie wolnego tekstu (do KNN search)
        }

        // Słownik zakładu - VLM ma używać tych terminów konsekwentnie.
        private const string PromptStructured =
            "Analizujesz zdjęcie z kamery przemysłowej zakładu drobiarskiego (ubojnia kurczaków, firma Piórkowscy).\n\n" +
            "===== UŻYWAJ KONSEKWENTNIE TYCH TERMINÓW =====\n" +
            "Lokalizacja:  hala uboju, pakowalnia, magazyn, magazyn klatek, rampa skupu, brama wjazdowa, brama wyjazdowa, parking, biuro, korytarz, korytarz produkcyjny\n" +
            "Ludzie:       pracownik (w czepku, w fartuchu, bez czepka, bez fartucha, w masce, z rękawicami), kierowca, lekarz weterynarii, kierownik\n" +
            "Obiekty:      ciężarówka, wózek widłowy, wózek platformowy, paleta, klatka transportowa, kontener E2, przenośnik, linia produkcyjna, urządzenia do uboju, ważka, myjka, kosz odpadów, drzwi rolowane, brama\n" +
            "Aktywność:    linia pracuje, linia stoi, ludzie pracują, brak ludzi, transport towaru, dezynfekcja, mgła/para, sprzątanie, wjazd pojazdu, wyjazd pojazdu\n" +
            "Anomalia:     osoba bez czepka/fartucha, otwarta brama bez ludzi, leżący przedmiot na podłodze, niezidentyfikowany pojazd, otwarte drzwi w nocy, brak ludzi gdy linia pracuje. Pusty string '' jeśli nic nieoczekiwanego.\n" +
            "Czas dnia:    dzień / noc / świt / zmrok (z jasności obrazu)\n\n" +
            "===== ODPOWIEDŹ =====\n" +
            "Zwróć WYŁĄCZNIE JSON o tym dokładnym schemacie, używając WYŁĄCZNIE polskich terminów ze słownika powyżej:\n" +
            "{\n" +
            "  \"lokalizacja\": \"<jedno pole z listy lokalizacji, najlepsze dopasowanie>\",\n" +
            "  \"ludzie\": [\"<lista, max 5 pozycji, pusty jeśli brak>\"],\n" +
            "  \"obiekty\": [\"<lista, max 8 pozycji>\"],\n" +
            "  \"aktywnosc\": [\"<lista, max 4 pozycje>\"],\n" +
            "  \"anomalia\": \"<konkretny opis lub pusty string>\",\n" +
            "  \"czas_dnia\": \"<dzień|noc|świt|zmrok>\",\n" +
            "  \"caption\": \"<jedno zdanie po polsku z najważniejszymi elementami, dla wyszukiwarki>\"\n" +
            "}\n\n" +
            "BEZ ```, BEZ komentarzy, BEZ tekstu przed/po. Tylko JSON.";

        public static async Task<(StructuredCaption Structured, double CostUsd)> CaptionStructuredAsync(string jpegPath, CancellationToken ct = default)
        {
            var result = await VlmClient.AnalyzeImageAsync(
                jpegPath, PromptStructured,
                model: VlmClient.ModelHaiku,
                maxTokens: 400,
                ct: ct);

            var sc = ParseStructured(result.Text);
            return (sc, result.CostUsd);
        }

        // Backward compat: stara metoda zwraca caption + cost
        public static async Task<(string Caption, double CostUsd)> CaptionAsync(string jpegPath, CancellationToken ct = default)
        {
            var (sc, cost) = await CaptionStructuredAsync(jpegPath, ct);
            return (sc.Caption, cost);
        }

        private static StructuredCaption ParseStructured(string text)
        {
            var sc = new StructuredCaption();
            try
            {
                // Wyciągnij JSON z odpowiedzi (czasem owinięty w ```json ... ```)
                var match = Regex.Match(text, @"\{[\s\S]+\}", RegexOptions.Singleline);
                if (!match.Success) { sc.Caption = text.Trim(); return sc; }

                var jo = JObject.Parse(match.Value);
                sc.Lokalizacja = (string?)jo["lokalizacja"] ?? string.Empty;
                sc.Anomalia = (string?)jo["anomalia"] ?? string.Empty;
                sc.CzasDnia = (string?)jo["czas_dnia"] ?? string.Empty;
                sc.Caption = (string?)jo["caption"] ?? string.Empty;

                if (jo["ludzie"] is JArray la) sc.Ludzie = la.Select(x => (string?)x ?? "").Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                if (jo["obiekty"] is JArray oa) sc.Obiekty = oa.Select(x => (string?)x ?? "").Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                if (jo["aktywnosc"] is JArray aa) sc.Aktywnosc = aa.Select(x => (string?)x ?? "").Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

                // Fallback caption: jeśli pusty, sklej z pól
                if (string.IsNullOrWhiteSpace(sc.Caption))
                {
                    var parts = new List<string> { sc.Lokalizacja };
                    if (sc.Ludzie.Count > 0) parts.Add($"ludzie: {string.Join(", ", sc.Ludzie)}");
                    if (sc.Obiekty.Count > 0) parts.Add($"obiekty: {string.Join(", ", sc.Obiekty.Take(4))}");
                    if (!string.IsNullOrWhiteSpace(sc.Anomalia)) parts.Add($"anomalia: {sc.Anomalia}");
                    sc.Caption = string.Join(". ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
                }
            }
            catch
            {
                sc.Caption = text.Trim();
            }
            return sc;
        }

        /// <summary>
        /// Buduje string captionu używanego do embeddingu OpenAI.
        /// Zawiera wszystkie pola strukturalne — embedding kompresuje to do wektora 1536.
        /// </summary>
        public static string BuildEmbeddingText(StructuredCaption sc)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(sc.Lokalizacja)) parts.Add($"Lokalizacja: {sc.Lokalizacja}");
            if (sc.Ludzie.Count > 0) parts.Add($"Ludzie: {string.Join(", ", sc.Ludzie)}");
            if (sc.Obiekty.Count > 0) parts.Add($"Obiekty: {string.Join(", ", sc.Obiekty)}");
            if (sc.Aktywnosc.Count > 0) parts.Add($"Aktywność: {string.Join(", ", sc.Aktywnosc)}");
            if (!string.IsNullOrWhiteSpace(sc.Anomalia)) parts.Add($"Anomalia: {sc.Anomalia}");
            if (!string.IsNullOrWhiteSpace(sc.CzasDnia)) parts.Add($"Czas dnia: {sc.CzasDnia}");
            if (!string.IsNullOrWhiteSpace(sc.Caption)) parts.Add(sc.Caption);
            return string.Join(". ", parts);
        }

        public static List<string> ExtractTags(StructuredCaption sc)
        {
            var tags = new List<string>();
            if (!string.IsNullOrWhiteSpace(sc.Lokalizacja)) tags.Add(sc.Lokalizacja.ToLowerInvariant());
            tags.AddRange(sc.Ludzie.Select(s => s.ToLowerInvariant()));
            tags.AddRange(sc.Obiekty.Select(s => s.ToLowerInvariant()));
            if (!string.IsNullOrWhiteSpace(sc.Anomalia)) tags.Add("anomalia");
            return tags.Distinct().ToList();
        }
    }
}
