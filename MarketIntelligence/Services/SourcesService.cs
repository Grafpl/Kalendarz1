using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.ServiceModel.Syndication;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Data.SqlClient;
using Kalendarz1.MarketIntelligence.Services.DataSources;

namespace Kalendarz1.MarketIntelligence.Services
{
    /// <summary>
    /// Zarządza tabelą intel_Sources — własne, edytowalne źródła wiadomości użytkownika.
    /// Doklejane do hardcoded NewsSourceConfig.GetAllRssSources().
    /// Wspiera auto-detect RSS feed dla wpisanego URL (próba kilku popularnych ścieżek).
    /// </summary>
    public class SourcesService
    {
        private readonly string _connectionString;
        private bool _tableEnsured;

        // Popularne ścieżki RSS sprawdzane przy auto-detect
        private static readonly string[] CommonRssPaths =
        {
            "/feed/", "/feed", "/rss/", "/rss.xml", "/rss",
            "/atom.xml", "/feed/rss", "/feeds/posts/default",
            "/fragments/rss/rss000.xml" // farmer.pl style
        };

        public SourcesService(string connectionString = null)
        {
            _connectionString = connectionString ?? MarketIntelligenceConfig.LibraNetConnectionString;
        }

        public async Task EnsureTableAsync()
        {
            if (_tableEnsured) return;
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                // Tabela utworzona w DatabaseSetup, ale dodatkowe kolumny dla user-managed sources
                using var cmd = new SqlCommand(@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'intel_Sources')
BEGIN
    CREATE TABLE intel_Sources (
        Id NVARCHAR(50) PRIMARY KEY,
        Name NVARCHAR(200) NOT NULL,
        Url NVARCHAR(1000) NOT NULL,
        SourceType NVARCHAR(20) NOT NULL DEFAULT 'RSS',
        Category NVARCHAR(50),
        Language NVARCHAR(10) DEFAULT 'pl',
        Priority INT DEFAULT 5,
        IsActive BIT DEFAULT 1,
        LastFetchTime DATETIME,
        LastSuccessTime DATETIME,
        ConsecutiveFailures INT DEFAULT 0,
        TotalArticlesFetched INT DEFAULT 0,
        FetchIntervalMinutes INT DEFAULT 1440,
        Topics NVARCHAR(500),
        CreatedAt DATETIME DEFAULT GETDATE(),
        UpdatedAt DATETIME DEFAULT GETDATE()
    );
END", conn);
                await cmd.ExecuteNonQueryAsync();

                // Dodaj kolumny których może brakować w starym schemacie
                var migrations = new (string Col, string Sql)[]
                {
                    ("Topics", "ALTER TABLE intel_Sources ADD Topics NVARCHAR(500) NULL"),
                    ("CreatedBy", "ALTER TABLE intel_Sources ADD CreatedBy NVARCHAR(50) NULL DEFAULT 'user'"),
                };
                foreach (var (col, sql) in migrations)
                {
                    try
                    {
                        using var checkCmd = new SqlCommand(
                            "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='intel_Sources' AND COLUMN_NAME=@c", conn);
                        checkCmd.Parameters.AddWithValue("@c", col);
                        var exists = (int)(await checkCmd.ExecuteScalarAsync() ?? 0) > 0;
                        if (!exists)
                        {
                            using var alterCmd = new SqlCommand(sql, conn);
                            await alterCmd.ExecuteNonQueryAsync();
                        }
                    }
                    catch { }
                }

                _tableEnsured = true;
                Debug.WriteLine("[Sources] Tabela intel_Sources zweryfikowana");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Sources] Ensure error: {ex.Message}");
            }
        }

        public async Task<List<UserSource>> GetAllAsync(bool onlyActive = false)
        {
            await EnsureTableAsync();
            var list = new List<UserSource>();
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                var sql = @"SELECT Id, Name, Url, SourceType, Category, Language, Priority, IsActive,
                                   LastFetchTime, LastSuccessTime, ConsecutiveFailures, TotalArticlesFetched,
                                   FetchIntervalMinutes, ISNULL(Topics, ''), CreatedAt
                            FROM intel_Sources " +
                          (onlyActive ? "WHERE IsActive = 1 " : "") +
                          "ORDER BY Priority, Name";
                using var cmd = new SqlCommand(sql, conn);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new UserSource
                    {
                        Id = reader.GetString(0),
                        Name = reader.GetString(1),
                        Url = reader.GetString(2),
                        SourceType = reader.GetString(3),
                        Category = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        Language = reader.IsDBNull(5) ? "pl" : reader.GetString(5),
                        Priority = reader.GetInt32(6),
                        IsActive = reader.GetBoolean(7),
                        LastFetchTime = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                        LastSuccessTime = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                        ConsecutiveFailures = reader.GetInt32(10),
                        TotalArticlesFetched = reader.GetInt32(11),
                        FetchIntervalMinutes = reader.GetInt32(12),
                        Topics = reader.GetString(13),
                        CreatedAt = reader.GetDateTime(14)
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Sources] GetAll error: {ex.Message}");
            }
            return list;
        }

        public async Task<bool> InsertAsync(UserSource s)
        {
            await EnsureTableAsync();
            try
            {
                if (string.IsNullOrWhiteSpace(s.Id))
                    s.Id = "user_" + Guid.NewGuid().ToString("N").Substring(0, 8);

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(@"
INSERT INTO intel_Sources (Id, Name, Url, SourceType, Category, Language, Priority, IsActive, FetchIntervalMinutes, Topics)
VALUES (@id, @name, @url, @type, @cat, @lang, @pri, @act, @int, @topics)", conn);
                cmd.Parameters.AddWithValue("@id", s.Id);
                cmd.Parameters.AddWithValue("@name", s.Name ?? s.Id);
                cmd.Parameters.AddWithValue("@url", s.Url ?? "");
                cmd.Parameters.AddWithValue("@type", s.SourceType ?? "RSS");
                cmd.Parameters.AddWithValue("@cat", (object)s.Category ?? "");
                cmd.Parameters.AddWithValue("@lang", s.Language ?? "pl");
                cmd.Parameters.AddWithValue("@pri", s.Priority);
                cmd.Parameters.AddWithValue("@act", s.IsActive);
                cmd.Parameters.AddWithValue("@int", s.FetchIntervalMinutes);
                cmd.Parameters.AddWithValue("@topics", (object)s.Topics ?? "");
                await cmd.ExecuteNonQueryAsync();
                Debug.WriteLine($"[Sources] Dodano źródło: {s.Name} ({s.Url})");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Sources] Insert error: {ex.Message}");
                return false;
            }
        }

        public async Task UpdateAsync(UserSource s)
        {
            await EnsureTableAsync();
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(@"
UPDATE intel_Sources
SET Name=@name, Url=@url, SourceType=@type, Category=@cat, Language=@lang,
    Priority=@pri, IsActive=@act, FetchIntervalMinutes=@int, Topics=@topics, UpdatedAt=GETDATE()
WHERE Id=@id", conn);
                cmd.Parameters.AddWithValue("@id", s.Id);
                cmd.Parameters.AddWithValue("@name", s.Name ?? "");
                cmd.Parameters.AddWithValue("@url", s.Url ?? "");
                cmd.Parameters.AddWithValue("@type", s.SourceType ?? "RSS");
                cmd.Parameters.AddWithValue("@cat", (object)s.Category ?? "");
                cmd.Parameters.AddWithValue("@lang", s.Language ?? "pl");
                cmd.Parameters.AddWithValue("@pri", s.Priority);
                cmd.Parameters.AddWithValue("@act", s.IsActive);
                cmd.Parameters.AddWithValue("@int", s.FetchIntervalMinutes);
                cmd.Parameters.AddWithValue("@topics", (object)s.Topics ?? "");
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Sources] Update error: {ex.Message}");
            }
        }

        public async Task DeleteAsync(string id)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand("DELETE FROM intel_Sources WHERE Id=@id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                await cmd.ExecuteNonQueryAsync();
                Debug.WriteLine($"[Sources] Usunięto źródło: {id}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Sources] Delete error: {ex.Message}");
            }
        }

        /// <summary>
        /// Auto-detect RSS feed dla URL. Sprawdza popularne ścieżki: /feed/, /rss/, /rss.xml itd.
        /// Zwraca pierwszy URL który zwraca prawidłowy RSS/Atom feed.
        /// Jeśli oryginalny URL już jest RSS feedem — zwraca go. W przeciwnym razie próbuje ścieżek.
        /// </summary>
        public async Task<RssDetectionResult> DetectRssFeedAsync(string baseUrl, CancellationToken ct = default)
        {
            var result = new RssDetectionResult { OriginalUrl = baseUrl };
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                result.Error = "Pusty URL";
                return result;
            }

            // Normalize URL — dodaj https:// jeśli brakuje schematu
            if (!baseUrl.StartsWith("http://") && !baseUrl.StartsWith("https://"))
                baseUrl = "https://" + baseUrl;

            // Zostaw tylko domenę + ścieżkę root jeśli to nie jest już feed
            Uri uri;
            try { uri = new Uri(baseUrl); }
            catch (Exception ex) { result.Error = "Nieprawidłowy URL: " + ex.Message; return result; }

            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(8)
            };
            httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            httpClient.DefaultRequestHeaders.Add("Accept",
                "application/rss+xml, application/xml, application/atom+xml, text/xml, */*");

            // 1. Najpierw spróbuj oryginalny URL — może to już jest RSS feed
            var candidateUrls = new List<string> { baseUrl };

            // 2. Potem ścieżki rss/feed na domenie głównej i bieżącej ścieżce
            var rootUrl = $"{uri.Scheme}://{uri.Host}";
            foreach (var path in CommonRssPaths)
            {
                candidateUrls.Add(rootUrl + path);
                if (!string.IsNullOrEmpty(uri.AbsolutePath) && uri.AbsolutePath != "/")
                {
                    var combined = rootUrl + uri.AbsolutePath.TrimEnd('/') + path;
                    candidateUrls.Add(combined);
                }
            }

            // De-duplicate
            candidateUrls = candidateUrls.Distinct().ToList();

            foreach (var url in candidateUrls)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeSpan.FromSeconds(8));
                    var response = await httpClient.GetAsync(url, cts.Token);
                    if (!response.IsSuccessStatusCode) continue;

                    var content = await response.Content.ReadAsStringAsync(cts.Token);
                    if (string.IsNullOrWhiteSpace(content)) continue;

                    // Sprawdź czy to RSS/Atom
                    if (LooksLikeFeed(content))
                    {
                        // Parsuj żeby pobrać tytuł + sample articles
                        try
                        {
                            using var stringReader = new StringReader(content);
                            using var xmlReader = XmlReader.Create(stringReader);
                            var feed = SyndicationFeed.Load(xmlReader);
                            result.Success = true;
                            result.FeedUrl = url;
                            result.FeedTitle = feed?.Title?.Text ?? new Uri(url).Host;
                            result.FeedDescription = feed?.Description?.Text ?? "";
                            result.SampleArticles = feed?.Items?.Take(5)
                                .Select(i => new SampleArticle
                                {
                                    Title = i.Title?.Text ?? "(bez tytułu)",
                                    Link = i.Links?.FirstOrDefault()?.Uri?.ToString() ?? "",
                                    Date = i.PublishDate.UtcDateTime
                                }).ToList() ?? new List<SampleArticle>();
                            return result;
                        }
                        catch
                        {
                            // Nieparsowalny ale wygląda jak feed — zwróć success bez sample
                            result.Success = true;
                            result.FeedUrl = url;
                            result.FeedTitle = new Uri(url).Host;
                            return result;
                        }
                    }
                }
                catch
                {
                    // Skip — try next URL
                }
            }

            result.Error = "Nie znaleziono RSS feed na " + uri.Host + " (sprawdzono " + candidateUrls.Count + " ścieżek)";
            return result;
        }

        private static bool LooksLikeFeed(string content)
        {
            // Quick check — szuka znaczników RSS/Atom w pierwszych 500 znakach
            var head = content.Substring(0, Math.Min(500, content.Length));
            return head.Contains("<rss", StringComparison.OrdinalIgnoreCase)
                || head.Contains("<feed", StringComparison.OrdinalIgnoreCase)
                || head.Contains("<rdf:RDF", StringComparison.OrdinalIgnoreCase)
                || head.Contains("<atom:feed", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Źródło wiadomości dodane przez użytkownika (intel_Sources).</summary>
    public class UserSource
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Url { get; set; } = "";
        public string SourceType { get; set; } = "RSS"; // RSS / WebScraping
        public string Category { get; set; } = "Drób";
        public string Language { get; set; } = "pl";
        public int Priority { get; set; } = 2; // 1-3
        public bool IsActive { get; set; } = true;
        public int FetchIntervalMinutes { get; set; } = 1440; // 24h
        public string Topics { get; set; } = ""; // Comma-separated keywords ("HPAI, ceny, Cedrob")
        public DateTime? LastFetchTime { get; set; }
        public DateTime? LastSuccessTime { get; set; }
        public int ConsecutiveFailures { get; set; }
        public int TotalArticlesFetched { get; set; }
        public DateTime CreatedAt { get; set; }

        public string StatusDisplay => ConsecutiveFailures switch
        {
            0 => "✅ OK",
            <= 2 => "⚠ " + ConsecutiveFailures + " błędów",
            _ => "❌ Martwe"
        };
        public string LastFetchDisplay => LastFetchTime?.ToString("yyyy-MM-dd HH:mm") ?? "—";
        public string PriorityDisplay => Priority switch
        {
            1 => "🔴 Krytyczne",
            2 => "🟠 Ważne",
            _ => "🟡 Normalne"
        };
    }

    /// <summary>Wynik auto-detect RSS feed.</summary>
    public class RssDetectionResult
    {
        public bool Success { get; set; }
        public string OriginalUrl { get; set; }
        public string FeedUrl { get; set; }
        public string FeedTitle { get; set; }
        public string FeedDescription { get; set; }
        public string Error { get; set; }
        public List<SampleArticle> SampleArticles { get; set; } = new();
    }

    public class SampleArticle
    {
        public string Title { get; set; }
        public string Link { get; set; }
        public DateTime Date { get; set; }
    }
}
