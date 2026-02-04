using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using System.Diagnostics;

namespace Kalendarz1.MarketIntelligence.Services.DataSources
{
    /// <summary>
    /// Serwis do filtrowania artykułów pod kątem relevantności dla branży drobiarskiej
    /// </summary>
    public class ContentFilterService
    {
        private readonly string _connectionString;
        private readonly HashSet<string> _existingUrlHashes;

        // Słowa kluczowe z wagami
        private static readonly Dictionary<string, int> PolishKeywordWeights = new()
        {
            // Drób - najwyższa waga (10)
            { "drób", 10 }, { "drobiowy", 10 }, { "drobiu", 10 },
            { "kurczak", 10 }, { "kurczaka", 10 }, { "kurczaki", 10 },
            { "brojler", 10 }, { "brojlera", 10 }, { "brojlery", 10 },
            { "ubojnia drobiu", 15 }, { "ubojni drobiu", 15 },

            // HPAI - krytyczne (15)
            { "hpai", 15 }, { "ptasia grypa", 15 }, { "grypa ptaków", 15 },
            { "avian influenza", 15 }, { "ognisko grypy", 15 },
            { "strefa ochronna", 12 }, { "strefa nadzoru", 12 },

            // Ceny - wysokie (8)
            { "cena skupu", 8 }, { "ceny skupu", 8 },
            { "cena kurczaka", 10 }, { "ceny drobiu", 10 },
            { "notowania drobiu", 8 }, { "giełda drobiowa", 10 },
            { "cena fileta", 8 }, { "cena tuszki", 8 },

            // Konkurencja (8)
            { "cedrob", 8 }, { "superdrob", 8 }, { "super drob", 8 },
            { "drosed", 8 }, { "animex", 8 }, { "indykpol", 8 },
            { "drobimex", 8 }, { "wipasz", 8 }, { "konspol", 8 },

            // Produkty (6)
            { "filet", 6 }, { "fileta", 6 }, { "tuszka", 6 }, { "tuszki", 6 },
            { "udko", 5 }, { "skrzydło", 5 }, { "podudzie", 5 },

            // Inne drób (7)
            { "indyk", 7 }, { "indyka", 7 }, { "kaczka", 6 }, { "gęś", 6 },
            { "nioska", 6 }, { "jaja", 5 },

            // Pasze (5)
            { "pasza", 5 }, { "paszy", 5 }, { "kukurydza", 5 },
            { "pszenica", 4 }, { "soja", 5 }, { "śruta", 5 },
            { "matif", 6 }, { "notowania zbóż", 5 },

            // Handel (5)
            { "eksport drobiu", 8 }, { "eksport mięsa", 6 },
            { "import drobiu", 8 }, { "import mięsa", 6 },

            // Sieci (4)
            { "biedronka", 4 }, { "lidl", 4 }, { "kaufland", 4 },
            { "auchan", 4 }, { "carrefour", 4 }, { "dino", 4 },

            // Instytucje (4)
            { "arimr", 4 }, { "mrirw", 4 }, { "glw", 5 },
            { "weterynaryjny", 5 }, { "inspekcja weterynaryjna", 6 },
            { "dotacja rolnicza", 4 }, { "dopłata", 3 },

            // Produkcja (4)
            { "ubój", 5 }, { "uboju", 5 }, { "przetwórstwo", 4 },
            { "hodowla drobiu", 7 }, { "ferma drobiu", 7 },

            // Geografia (3)
            { "polska", 2 }, { "polski", 2 }, { "polskie", 2 },
            { "łódzkie", 3 }, { "brzeziny", 5 },
            { "brazylia", 4 }, { "ukraina", 4 },
        };

        private static readonly Dictionary<string, int> EnglishKeywordWeights = new()
        {
            // Poultry (10)
            { "poultry", 10 }, { "chicken", 10 }, { "broiler", 10 },
            { "turkey", 8 }, { "duck", 6 },

            // HPAI (15)
            { "hpai", 15 }, { "avian influenza", 15 }, { "bird flu", 15 },
            { "outbreak", 8 }, { "culling", 10 },

            // Prices (6)
            { "poultry price", 8 }, { "chicken price", 8 },
            { "meat price", 5 }, { "commodity", 4 },

            // Trade (5)
            { "export", 5 }, { "import", 5 }, { "trade", 4 },
            { "poland", 6 }, { "eu", 4 }, { "europe", 4 },
            { "brazil", 6 }, { "ukraine", 6 }, { "mercosur", 6 },

            // Feed (4)
            { "feed", 4 }, { "corn", 4 }, { "wheat", 4 },
            { "soybean", 4 }, { "matif", 5 },
        };

        // Słowa wykluczające (przepisy kulinarne, niezwiązane, nieistotne gatunki)
        private static readonly string[] ExclusionPatterns = new[]
        {
            // Przepisy kulinarne
            @"\bprzepis\b", @"\bprzepisy\b", @"\bkuchnia\b",
            @"\bgotowanie\b", @"\bugotować\b", @"\bupiec\b",
            @"\bsałatka\b", @"\bzupa\b", @"\bobiad\b",
            @"\brestauracja\b", @"\bcatering\b",
            @"\brecipe\b", @"\bcooking\b", @"\bkitchen\b",
            @"\broast\b", @"\bfried\b", @"\bgrilled\b",
            @"\bhotel\b", @"\btouris", @"\bvacation\b",

            // NIEISTOTNE GATUNKI DROBIU (nie dotyczy naszej ubojni kurczaków)
            @"\bprzepiórk", @"\bprzepióreczk",  // przepiórki
            @"\bquail\b",                        // quail (EN)
            @"\bstruś\b", @"\bstrusi",           // strusie
            @"\bostrich\b",                      // ostrich (EN)
            @"\bgołąb\b", @"\bgołęb",            // gołębie
            @"\bpigeon\b", @"\bsquab\b",         // pigeon (EN)
            @"\bpapug",                          // papugi
            @"\bparrot\b",                       // parrot (EN)
            @"\bemu\b",                          // emu
            @"\bperliczk",                       // perliczki (chyba że w kontekście Polski)

            // NIEISTOTNE REGIONY (bez kontekstu eksportu do UE/Polski)
            // Uwaga: te wzorce SĄ używane tylko gdy NIE MA słów "eksport", "import", "europa", "ue", "polska"
        };

        // Wzorce regionów nieistotnych (farmy lokalne bez eksportu do UE)
        private static readonly string[] IrrelevantRegionPatterns = new[]
        {
            @"(?i)\b(usa|united states|america|american)\b.*\b(farm|poultry|chicken)\b",
            @"(?i)\b(china|chinese|india|indian|indonesia|vietnam|philippines)\b.*\b(farm|poultry)\b",
            @"(?i)\b(africa|african|nigeria|south africa)\b.*\b(poultry|chicken)\b",
            @"(?i)\bmanchester farms\b",  // Konkretna farma przepiórek USA
            @"(?i)\btyson foods\b(?!.*(polska|europe|eu|export|import))", // Tyson bez kontekstu EU
        };

        public ContentFilterService(string connectionString = null)
        {
            _connectionString = connectionString ??
                "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
            _existingUrlHashes = new HashSet<string>();
        }

        /// <summary>
        /// Filtruj artykuły - zostaw tylko relevantne dla branży drobiarskiej
        /// </summary>
        public async Task<List<RawArticle>> FilterArticlesAsync(List<RawArticle> articles)
        {
            // Load existing URL hashes from DB for deduplication
            await LoadExistingHashesAsync();

            var filteredArticles = new List<RawArticle>();

            foreach (var article in articles)
            {
                // Skip duplicates
                if (_existingUrlHashes.Contains(article.UrlHash))
                {
                    Debug.WriteLine($"[Filter] Duplicate: {article.Title}");
                    continue;
                }

                // Calculate relevance
                var (isRelevant, score, keywords) = CalculateRelevance(article);

                article.IsRelevant = isRelevant;
                article.RelevanceScore = score;
                article.MatchedKeywords = keywords;

                if (isRelevant)
                {
                    filteredArticles.Add(article);
                }
                else
                {
                    Debug.WriteLine($"[Filter] Not relevant (score={score}): {article.Title}");
                }
            }

            // Sort by relevance score descending, then by date
            return filteredArticles
                .OrderByDescending(a => a.RelevanceScore)
                .ThenByDescending(a => a.PublishDate)
                .ToList();
        }

        /// <summary>
        /// Oblicz relevantność artykułu
        /// </summary>
        public (bool IsRelevant, int Score, string[] Keywords) CalculateRelevance(RawArticle article)
        {
            var textToAnalyze = $"{article.Title} {article.Summary}".ToLowerInvariant();
            var matchedKeywords = new List<string>();
            int totalScore = 0;

            // Check exclusion patterns first
            foreach (var pattern in ExclusionPatterns)
            {
                if (Regex.IsMatch(textToAnalyze, pattern, RegexOptions.IgnoreCase))
                {
                    // Penalty for exclusion patterns
                    totalScore -= 20;
                    Debug.WriteLine($"[Filter] Exclusion pattern hit: {pattern} in '{article.Title.Substring(0, Math.Min(50, article.Title.Length))}'");
                }
            }

            // Check irrelevant regions (only if no EU/Poland context)
            bool hasEuContext = Regex.IsMatch(textToAnalyze, @"\b(polska|poland|europe|europa|ue|eu|eksport|import)\b", RegexOptions.IgnoreCase);
            if (!hasEuContext)
            {
                foreach (var pattern in IrrelevantRegionPatterns)
                {
                    if (Regex.IsMatch(textToAnalyze, pattern))
                    {
                        totalScore -= 30; // Większa kara za nieistotne regiony
                        Debug.WriteLine($"[Filter] Irrelevant region: {article.Title.Substring(0, Math.Min(50, article.Title.Length))}");
                    }
                }
            }

            // Check Polish keywords
            if (article.Language == "pl")
            {
                foreach (var (keyword, weight) in PolishKeywordWeights)
                {
                    if (textToAnalyze.Contains(keyword.ToLower()))
                    {
                        totalScore += weight;
                        matchedKeywords.Add(keyword);
                    }
                }
            }
            else
            {
                // Check English keywords
                foreach (var (keyword, weight) in EnglishKeywordWeights)
                {
                    if (textToAnalyze.Contains(keyword.ToLower()))
                    {
                        totalScore += weight;
                        matchedKeywords.Add(keyword);
                    }
                }
            }

            // Bonus for source category
            if (article.SourceCategory == "Drób" || article.SourceCategory == "HPAI")
            {
                totalScore += 10;
            }
            else if (article.SourceCategory == "Mięso" || article.SourceCategory == "Rolnictwo")
            {
                totalScore += 5;
            }

            // Bonus for high-priority sources
            var source = NewsSourceConfig.GetAllSources()
                .FirstOrDefault(s => s.Id == article.SourceId);
            if (source?.Priority == 1)
            {
                totalScore += 5;
            }

            // Minimum threshold for relevance
            bool isRelevant = totalScore >= 8;

            return (isRelevant, totalScore, matchedKeywords.Distinct().ToArray());
        }

        /// <summary>
        /// Sprawdź czy artykuł jest duplikatem (po URL hash)
        /// </summary>
        public bool IsDuplicate(RawArticle article)
        {
            return _existingUrlHashes.Contains(article.UrlHash);
        }

        /// <summary>
        /// Dodaj hash do zbioru istniejących (po zapisie do DB)
        /// </summary>
        public void MarkAsProcessed(string urlHash)
        {
            _existingUrlHashes.Add(urlHash);
        }

        /// <summary>
        /// Załaduj istniejące hashe URL z bazy danych
        /// </summary>
        private async Task LoadExistingHashesAsync()
        {
            _existingUrlHashes.Clear();

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Get hashes from last 30 days
                var sql = @"
                    SELECT UrlHash
                    FROM intel_Articles
                    WHERE UrlHash IS NOT NULL
                      AND FetchedAt > DATEADD(day, -30, GETDATE())";

                using var cmd = new SqlCommand(sql, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var hash = reader.GetString(0);
                    _existingUrlHashes.Add(hash);
                }

                Debug.WriteLine($"[Filter] Loaded {_existingUrlHashes.Count} existing URL hashes");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Filter] Error loading existing hashes: {ex.Message}");
                // Continue anyway - just won't deduplicate against DB
            }
        }

        /// <summary>
        /// Kategoria artykułu na podstawie słów kluczowych
        /// </summary>
        public string DetermineCategory(RawArticle article)
        {
            var text = $"{article.Title} {article.Summary}".ToLowerInvariant();

            // Priority order for categories
            if (Regex.IsMatch(text, @"\bhpai\b|ptasia grypa|grypa ptak|avian influenza|bird flu"))
                return "HPAI";

            if (Regex.IsMatch(text, @"cen[ay]|notowania|giełda|price"))
                return "Ceny";

            if (Regex.IsMatch(text, @"eksport|import|handel|export|import|trade"))
                return "Eksport";

            if (Regex.IsMatch(text, @"cedrob|superdrob|drosed|animex|indykpol|konkurenc"))
                return "Konkurencja";

            if (Regex.IsMatch(text, @"dotacj|dopłat|arimr|program|subsid"))
                return "Dotacje";

            if (Regex.IsMatch(text, @"pasza|kukurydza|pszenica|soja|feed|corn|wheat"))
                return "Pasze";

            if (Regex.IsMatch(text, @"pogoda|mróz|upał|weather|frost"))
                return "Pogoda";

            if (Regex.IsMatch(text, @"regula|prawo|ustawa|dyrektywa|law|regulation"))
                return "Regulacje";

            if (Regex.IsMatch(text, @"klient|odbiorc|sieć|retail|biedronka|lidl"))
                return "Klienci";

            // Default based on source category
            return article.SourceCategory ?? "Info";
        }

        /// <summary>
        /// Poziom ważności artykułu
        /// </summary>
        public string DetermineSeverity(RawArticle article)
        {
            var text = $"{article.Title} {article.Summary}".ToLowerInvariant();

            // Critical - HPAI, major price changes
            if (Regex.IsMatch(text, @"hpai|ptasia grypa|ognisko|outbreak|kryzys|crisis|alert"))
                return "Critical";

            if (Regex.IsMatch(text, @"wzrost.*\d+%|spadek.*\d+%|rise.*\d+%|drop.*\d+%"))
            {
                // Check if significant percentage change
                var percentMatch = Regex.Match(text, @"(\d+)[,.]?(\d*)\s*%");
                if (percentMatch.Success)
                {
                    var percent = double.Parse(percentMatch.Groups[1].Value);
                    if (percent >= 10) return "Critical";
                    if (percent >= 5) return "Warning";
                }
            }

            // Warning - price changes, competition, regulations
            if (Regex.IsMatch(text, @"ostrzeżenie|uwaga|warning|alert|zmiana|change"))
                return "Warning";

            if (Regex.IsMatch(text, @"wzrost|spadek|rise|fall|increase|decrease"))
                return "Warning";

            // Positive - opportunities, subsidies, good news
            if (Regex.IsMatch(text, @"dotacj|sukces|szansa|opportunity|grant|growth|rozwój"))
                return "Positive";

            if (Regex.IsMatch(text, @"rekord|najwyżs|record|highest"))
                return "Positive";

            // Default
            return "Info";
        }

        /// <summary>
        /// Pobierz top N artykułów po relevantności
        /// </summary>
        public List<RawArticle> GetTopArticles(List<RawArticle> articles, int count = 20)
        {
            return articles
                .Where(a => a.IsRelevant)
                .OrderByDescending(a => a.RelevanceScore)
                .ThenByDescending(a => a.PublishDate)
                .Take(count)
                .ToList();
        }
    }
}
