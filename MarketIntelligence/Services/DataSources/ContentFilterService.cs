using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Kalendarz1.MarketIntelligence.Services.DataSources
{
    /// <summary>
    /// Serwis do filtrowania artykulow - blacklist/whitelist
    /// </summary>
    public class ContentFilterService
    {
        #region Blacklist - ODRZUC te artykuły

        // Gatunki do odrzucenia (chyba ze w kontekscie HPAI lub duzego gracza)
        private static readonly string[] BlacklistSpecies = new[]
        {
            "przepiórka", "przepiorka", "quail",
            "struś", "strus", "ostrich",
            "gołąb", "golab", "pigeon",
            "bażant", "bazant", "pheasant",
            "perliczka", "guinea fowl",
            "emu",
            "kuropatwa", "partridge"
        };

        // Geografie do odrzucenia (chyba ze eksport do UE lub globalne ceny)
        private static readonly string[] BlacklistGeographies = new[]
        {
            "kansas", "iowa", "arkansas", "georgia state", "alabama", "mississippi", // US states
            "texas poultry", "california chicken",
            "pakistan poultry", "bangladesh chicken", "nigeria poultry",
            "ethiopia chicken", "kenya poultry", "south africa chicken local",
            "indonesia local", "philippines local farm", "vietnam local",
            "peru local", "colombia local", "ecuador local"
        };

        // Tematy do odrzucenia
        private static readonly string[] BlacklistTopics = new[]
        {
            "backyard chicken", "hobby farm", "pet chicken", "chicken coop diy",
            "chicken recipe", "przepis na kurczaka", "jak ugotować",
            "chicken dance", "rubber chicken",
            "free range eggs only", // jaja bez kontekstu drobiu
            "chicken stock market" // giełda nie drób
        };

        #endregion

        #region Whitelist - ZAWSZE akceptuj te artykuły

        // Polskie firmy - zawsze istotne
        private static readonly string[] WhitelistPolishCompanies = new[]
        {
            "cedrob", "superdrob", "super-drob", "lipco", "drosed", "animex",
            "drobimex", "wipasz", "indykpol", "plukon", "gobarto", "konspol",
            "raddrob", "system-drob", "drobex", "roldrob", "exdrob",
            "piorkowscy", "piórkowscy"
        };

        // Sieci handlowe - zawsze istotne
        private static readonly string[] WhitelistRetailChains = new[]
        {
            "biedronka", "lidl", "kaufland", "carrefour", "auchan", "tesco",
            "dino polska", "makro", "selgros", "stokrotka", "polomarket",
            "netto", "aldi", "intermarche", "leclerc", "topaz", "chata polska",
            "chorten", "lewiatan", "spar polska", "freshmarket", "delikatesy centrum"
        };

        // Tematy krytyczne - zawsze istotne
        private static readonly string[] WhitelistCriticalTopics = new[]
        {
            "hpai", "ptasia grypa", "avian influenza", "bird flu",
            "newcastle disease", "salmonella drób",
            "import drobiu", "eksport drobiu", "mercosur",
            "cena żywca", "cena skupu", "ceny drobiu",
            "ubojnia drób", "ubój drób", "slaughterhouse poultry",
            "ksef", "vat e-faktura",
            "arimr dotacje", "nfoś dotacje przetwórstwo"
        };

        // Słowa kluczowe branżowe
        private static readonly string[] WhitelistIndustryKeywords = new[]
        {
            "kurczak", "chicken", "broiler", "brojler",
            "drób", "poultry", "drobiarski",
            "filet z kurczaka", "tuszka", "carcass",
            "żywiec drobiowy", "live poultry",
            "pasze drobiowe", "feed poultry",
            "kukurydza paszowa", "soja paszowa",
            "ubojnia", "slaughterhouse", "processing plant",
            "hodowla drobiu", "ferma drobiu"
        };

        #endregion

        #region Scoring weights

        private const int WhitelistCompanyScore = 50;
        private const int WhitelistRetailScore = 30;
        private const int WhitelistCriticalScore = 60;
        private const int WhitelistKeywordScore = 20;

        private const int BlacklistSpeciesScore = -40;
        private const int BlacklistGeographyScore = -30;
        private const int BlacklistTopicScore = -50;

        private const int MinimumScoreThreshold = 10;

        #endregion

        /// <summary>
        /// Filtruje liste artykulow - zwraca tylko istotne
        /// </summary>
        public List<FilteredArticle> FilterArticles(List<(string Title, string Snippet, string Url)> articles)
        {
            var results = new List<FilteredArticle>();

            foreach (var article in articles)
            {
                var result = EvaluateArticle(article.Title, article.Snippet);
                result.Url = article.Url;

                if (result.IsRelevant)
                {
                    results.Add(result);
                }
            }

            // Sortuj po score malejaco
            return results.OrderByDescending(r => r.Score).ToList();
        }

        /// <summary>
        /// Ocenia pojedynczy artykul
        /// </summary>
        public FilteredArticle EvaluateArticle(string title, string content)
        {
            var result = new FilteredArticle
            {
                Title = title,
                Content = content,
                Score = 0,
                Reasons = new List<string>()
            };

            var textToAnalyze = $"{title} {content}".ToLowerInvariant();

            // === WHITELIST - dodaj punkty ===

            // Polskie firmy
            foreach (var company in WhitelistPolishCompanies)
            {
                if (textToAnalyze.Contains(company.ToLowerInvariant()))
                {
                    result.Score += WhitelistCompanyScore;
                    result.Reasons.Add($"+{WhitelistCompanyScore} firma: {company}");
                    break; // Tylko raz za firmy
                }
            }

            // Sieci handlowe
            foreach (var chain in WhitelistRetailChains)
            {
                if (textToAnalyze.Contains(chain.ToLowerInvariant()))
                {
                    result.Score += WhitelistRetailScore;
                    result.Reasons.Add($"+{WhitelistRetailScore} siec: {chain}");
                    break;
                }
            }

            // Tematy krytyczne
            foreach (var topic in WhitelistCriticalTopics)
            {
                if (textToAnalyze.Contains(topic.ToLowerInvariant()))
                {
                    result.Score += WhitelistCriticalScore;
                    result.Reasons.Add($"+{WhitelistCriticalScore} temat: {topic}");
                    break;
                }
            }

            // Slowa kluczowe branzowe
            int keywordCount = 0;
            foreach (var keyword in WhitelistIndustryKeywords)
            {
                if (textToAnalyze.Contains(keyword.ToLowerInvariant()))
                {
                    keywordCount++;
                    if (keywordCount <= 3) // Max 3 slowa kluczowe
                    {
                        result.Score += WhitelistKeywordScore;
                    }
                }
            }
            if (keywordCount > 0)
            {
                result.Reasons.Add($"+{Math.Min(keywordCount, 3) * WhitelistKeywordScore} słowa kluczowe: {keywordCount}");
            }

            // === BLACKLIST - odejmij punkty ===

            // Gatunki do odrzucenia
            bool hasHpaiContext = textToAnalyze.Contains("hpai") || textToAnalyze.Contains("ptasia grypa") || textToAnalyze.Contains("avian");
            if (!hasHpaiContext) // Nie odrzucaj jesli kontekst HPAI
            {
                foreach (var species in BlacklistSpecies)
                {
                    if (textToAnalyze.Contains(species.ToLowerInvariant()))
                    {
                        result.Score += BlacklistSpeciesScore;
                        result.Reasons.Add($"{BlacklistSpeciesScore} gatunek: {species}");
                        break;
                    }
                }
            }

            // Geografie do odrzucenia
            bool hasExportContext = textToAnalyze.Contains("export") || textToAnalyze.Contains("eksport") ||
                                    textToAnalyze.Contains("import") || textToAnalyze.Contains("global") ||
                                    textToAnalyze.Contains("ue") || textToAnalyze.Contains("eu ");
            if (!hasExportContext)
            {
                foreach (var geo in BlacklistGeographies)
                {
                    if (textToAnalyze.Contains(geo.ToLowerInvariant()))
                    {
                        result.Score += BlacklistGeographyScore;
                        result.Reasons.Add($"{BlacklistGeographyScore} geografia: {geo}");
                        break;
                    }
                }
            }

            // Tematy do odrzucenia
            foreach (var topic in BlacklistTopics)
            {
                if (textToAnalyze.Contains(topic.ToLowerInvariant()))
                {
                    result.Score += BlacklistTopicScore;
                    result.Reasons.Add($"{BlacklistTopicScore} temat: {topic}");
                    break;
                }
            }

            // === SPECJALNE REGUŁY ===

            // Bonus za Polske/UE
            if (textToAnalyze.Contains("polska") || textToAnalyze.Contains("poland") ||
                textToAnalyze.Contains("polish"))
            {
                result.Score += 15;
                result.Reasons.Add("+15 Polska");
            }

            // Bonus za ceny
            if (Regex.IsMatch(textToAnalyze, @"\d+[,\.]\d+\s*(zł|pln|eur|usd|zl)/kg"))
            {
                result.Score += 25;
                result.Reasons.Add("+25 cena/kg");
            }

            // Bonus za daty 2026
            if (textToAnalyze.Contains("2026") || textToAnalyze.Contains("luty 2026") ||
                textToAnalyze.Contains("february 2026"))
            {
                result.Score += 10;
                result.Reasons.Add("+10 aktualna data");
            }

            // === DECYZJA ===
            result.IsRelevant = result.Score >= MinimumScoreThreshold;
            result.Summary = result.IsRelevant
                ? $"AKCEPTUJ (score: {result.Score})"
                : $"ODRZUĆ (score: {result.Score})";

            return result;
        }

        /// <summary>
        /// Szybka walidacja - czy artykul w ogole warto analizowac
        /// </summary>
        public bool QuickReject(string title, string snippet)
        {
            var text = $"{title} {snippet}".ToLowerInvariant();

            // Natychmiastowe odrzucenie
            foreach (var topic in BlacklistTopics)
            {
                if (text.Contains(topic.ToLowerInvariant()))
                {
                    return true; // Odrzuc
                }
            }

            // Sprawdz czy ma jakiekolwiek slowo kluczowe branzowe
            bool hasAnyKeyword = WhitelistIndustryKeywords.Any(k => text.Contains(k.ToLowerInvariant())) ||
                                 WhitelistPolishCompanies.Any(c => text.Contains(c.ToLowerInvariant())) ||
                                 WhitelistCriticalTopics.Any(t => text.Contains(t.ToLowerInvariant()));

            if (!hasAnyKeyword)
            {
                return true; // Odrzuc - brak slow kluczowych
            }

            return false; // Nie odrzucaj - warto przeanalizowac
        }

        /// <summary>
        /// Kategoryzuje artykul na podstawie tresci
        /// </summary>
        public string CategorizeArticle(string title, string content)
        {
            var text = $"{title} {content}".ToLowerInvariant();

            if (text.Contains("hpai") || text.Contains("ptasia grypa") || text.Contains("avian"))
                return "HPAI";

            if (text.Contains("cena") || text.Contains("price") || text.Contains("zł/kg") || text.Contains("eur/t"))
                return "Ceny";

            if (WhitelistPolishCompanies.Any(c => text.Contains(c.ToLowerInvariant())))
                return "Konkurencja";

            if (WhitelistRetailChains.Any(c => text.Contains(c.ToLowerInvariant())))
                return "Klienci";

            if (text.Contains("import") || text.Contains("eksport") || text.Contains("export"))
                return "Eksport";

            if (text.Contains("regulac") || text.Contains("ustawa") || text.Contains("ksef") || text.Contains("prawo"))
                return "Regulacje";

            if (text.Contains("kukurydz") || text.Contains("pasza") || text.Contains("soja") || text.Contains("matif"))
                return "Koszty";

            if (text.Contains("pogod") || text.Contains("mroz") || text.Contains("weather"))
                return "Pogoda";

            if (text.Contains("transport") || text.Contains("logistyk"))
                return "Logistyka";

            return "Info";
        }

        /// <summary>
        /// Okresla severity artykulu
        /// </summary>
        public string DetermineSeverity(string title, string content, int score)
        {
            var text = $"{title} {content}".ToLowerInvariant();

            // Critical
            if (text.Contains("hpai") && (text.Contains("łódzk") || text.Contains("lodzk") || text.Contains("brzezin")))
                return "critical";

            if (text.Contains("przejęci") || text.Contains("przejecie") || text.Contains("acquisition"))
                return "critical";

            if (text.Contains("zamkni") || text.Contains("bankrut") || text.Contains("upad"))
                return "critical";

            // Warning
            if (text.Contains("hpai") || text.Contains("ptasia grypa"))
                return "warning";

            if (text.Contains("spadek") || text.Contains("strata") || text.Contains("kryzys"))
                return "warning";

            if (text.Contains("import") && text.Contains("brazylia"))
                return "warning";

            // Positive
            if (text.Contains("wzrost") || text.Contains("rekord") || text.Contains("sukces"))
                return "positive";

            if (text.Contains("dotacj") || text.Contains("dofinansow"))
                return "positive";

            if (text.Contains("ekspansj") || text.Contains("nowe sklepy") || text.Contains("nowy klient"))
                return "positive";

            // Default
            return "info";
        }
    }

    public class FilteredArticle
    {
        public string Title { get; set; }
        public string Content { get; set; }
        public string Url { get; set; }
        public int Score { get; set; }
        public bool IsRelevant { get; set; }
        public string Summary { get; set; }
        public List<string> Reasons { get; set; } = new List<string>();
    }
}
