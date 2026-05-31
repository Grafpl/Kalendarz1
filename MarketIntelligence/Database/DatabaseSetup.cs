using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.MarketIntelligence.Database
{
    /// <summary>
    /// Klasa do tworzenia i zarządzania tabelami bazy danych dla Market Intelligence
    /// </summary>
    public class DatabaseSetup
    {
        private readonly string _connectionString;

        public DatabaseSetup(string connectionString = null)
        {
            _connectionString = connectionString ?? MarketIntelligenceConfig.LibraNetConnectionString;
        }

        /// <summary>
        /// Utwórz wszystkie wymagane tabele
        /// </summary>
        public async Task<bool> EnsureTablesExistAsync()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Create intel_Articles table
                await ExecuteNonQueryAsync(conn, CreateArticlesTableSql);

                // Create intel_HpaiAlerts table
                await ExecuteNonQueryAsync(conn, CreateHpaiAlertsTableSql);

                // Create intel_FetchLog table
                await ExecuteNonQueryAsync(conn, CreateFetchLogTableSql);

                // Create intel_Prices table
                await ExecuteNonQueryAsync(conn, CreatePricesTableSql);

                // Create intel_Sources table
                await ExecuteNonQueryAsync(conn, CreateSourcesTableSql);

                // Create intel_DailySummary table
                await ExecuteNonQueryAsync(conn, CreateDailySummaryTableSql);

                // ── Faza A: Story Clustering + Entity Tracking ──
                await ExecuteNonQueryAsync(conn, CreateStoriesTableSql);
                await ExecuteNonQueryAsync(conn, CreateStoryArticlesTableSql);
                await ExecuteNonQueryAsync(conn, CreateEntitiesTableSql);
                await ExecuteNonQueryAsync(conn, CreateEntityMentionsTableSql);

                // ── Faza B: Trendy w czasie ──
                await ExecuteNonQueryAsync(conn, CreateTrendDataPointsTableSql);

                // ── Faza C: Chat sesyjny z pamięcią ──
                await ExecuteNonQueryAsync(conn, CreateChatSessionsTableSql);
                await ExecuteNonQueryAsync(conn, CreateChatMessagesTableSql);

                // MIGRACJA: stare instalacje miały intel_Articles + intel_Prices z innym schematem
                // (bez FetchedAt, PriceDate itd.). Dodaj brakujące kolumny zanim spróbujemy
                // utworzyć indeksy na nich.
                await MigrateArticlesTableAsync(conn);
                await MigratePricesTableAsync(conn);
                await WidenArticlesColumnsAsync(conn);

                // Create indexes — wraz z indywidualnym try/catch per indeks żeby
                // brak jednej kolumny nie wywalał całego CreateIndexesSql (problem z PriceDate).
                await ExecuteIndexesIndividuallyAsync(conn);

                Debug.WriteLine("[DatabaseSetup] All tables created/verified successfully");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseSetup] Error creating tables: {ex.Message}");
                return false;
            }
        }

        private async Task ExecuteNonQueryAsync(SqlConnection conn, string sql)
        {
            try
            {
                using var cmd = new SqlCommand(sql, conn);
                cmd.CommandTimeout = 60;
                await cmd.ExecuteNonQueryAsync();
            }
            catch (SqlException ex) when (ex.Number == 2714) // Object already exists
            {
                // Table already exists - this is fine
            }
        }

        /// <summary>
        /// Migracja intel_Articles ze starego schematu (Title/Body/Source/SourceUrl/AiAnalysis/Tags)
        /// na nowy (SourceId/SourceName/Url/UrlHash/Summary/FullContent/FetchedAt + role-based AI).
        /// Idempotentne — bezpieczne do uruchamiania wielokrotnie.
        /// </summary>
        private async Task MigrateArticlesTableAsync(SqlConnection conn)
        {
            try
            {
                // Każda kolumna sprawdzana osobno — jeśli już jest, INFORMATION_SCHEMA pominie ALTER.
                var migrations = new (string Column, string Sql)[]
                {
                    ("SourceId",            "ALTER TABLE intel_Articles ADD SourceId NVARCHAR(50) NULL"),
                    ("SourceName",          "ALTER TABLE intel_Articles ADD SourceName NVARCHAR(200) NULL"),
                    ("Url",                 "ALTER TABLE intel_Articles ADD Url NVARCHAR(1000) NULL"),
                    ("UrlHash",             "ALTER TABLE intel_Articles ADD UrlHash NVARCHAR(64) NULL"),
                    ("Summary",             "ALTER TABLE intel_Articles ADD Summary NVARCHAR(MAX) NULL"),
                    ("FullContent",         "ALTER TABLE intel_Articles ADD FullContent NVARCHAR(MAX) NULL"),
                    ("FetchedAt",           "ALTER TABLE intel_Articles ADD FetchedAt DATETIME NOT NULL DEFAULT GETDATE() WITH VALUES"),
                    ("Language",            "ALTER TABLE intel_Articles ADD Language NVARCHAR(10) NULL DEFAULT 'pl'"),
                    ("RelevanceScore",      "ALTER TABLE intel_Articles ADD RelevanceScore INT NULL DEFAULT 0"),
                    ("IsRelevant",          "ALTER TABLE intel_Articles ADD IsRelevant BIT NULL DEFAULT 0"),
                    ("MatchedKeywords",     "ALTER TABLE intel_Articles ADD MatchedKeywords NVARCHAR(500) NULL"),
                    ("CeoAnalysis",         "ALTER TABLE intel_Articles ADD CeoAnalysis NVARCHAR(MAX) NULL"),
                    ("SalesAnalysis",       "ALTER TABLE intel_Articles ADD SalesAnalysis NVARCHAR(MAX) NULL"),
                    ("BuyerAnalysis",       "ALTER TABLE intel_Articles ADD BuyerAnalysis NVARCHAR(MAX) NULL"),
                    ("EducationalContent",  "ALTER TABLE intel_Articles ADD EducationalContent NVARCHAR(MAX) NULL"),
                    ("CeoRecommendations",  "ALTER TABLE intel_Articles ADD CeoRecommendations NVARCHAR(MAX) NULL"),
                    ("SalesRecommendations","ALTER TABLE intel_Articles ADD SalesRecommendations NVARCHAR(MAX) NULL"),
                    ("BuyerRecommendations","ALTER TABLE intel_Articles ADD BuyerRecommendations NVARCHAR(MAX) NULL"),
                    ("KeyNumbers",          "ALTER TABLE intel_Articles ADD KeyNumbers NVARCHAR(MAX) NULL"),
                    ("RelatedTopics",       "ALTER TABLE intel_Articles ADD RelatedTopics NVARCHAR(500) NULL"),
                    ("AnalyzedAt",          "ALTER TABLE intel_Articles ADD AnalyzedAt DATETIME NULL"),
                    ("AiModel",             "ALTER TABLE intel_Articles ADD AiModel NVARCHAR(50) NULL"),
                    ("IsRead",              "ALTER TABLE intel_Articles ADD IsRead BIT NULL DEFAULT 0"),
                    ("IsBookmarked",        "ALTER TABLE intel_Articles ADD IsBookmarked BIT NULL DEFAULT 0"),
                    ("UserNotes",           "ALTER TABLE intel_Articles ADD UserNotes NVARCHAR(MAX) NULL"),
                };

                foreach (var (column, sql) in migrations)
                {
                    var exists = await ColumnExistsAsync(conn, "intel_Articles", column);
                    if (!exists)
                    {
                        try
                        {
                            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 30 };
                            await cmd.ExecuteNonQueryAsync();
                            Debug.WriteLine($"[DatabaseSetup] ➕ Migracja: dodano kolumnę intel_Articles.{column}");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[DatabaseSetup] ⚠ Migracja kolumny {column} nieudana: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseSetup] Migracja intel_Articles error: {ex.Message}");
            }
        }

        /// <summary>
        /// Migracja intel_Prices: stary schemat (Date) → nowy (PriceDate, PriceType, etc.).
        /// Idempotentne — bezpieczne do uruchamiania wielokrotnie.
        /// </summary>
        private async Task MigratePricesTableAsync(SqlConnection conn)
        {
            try
            {
                var migrations = new (string Column, string Sql)[]
                {
                    ("PriceType",          "ALTER TABLE intel_Prices ADD PriceType NVARCHAR(50) NULL"),
                    ("Source",             "ALTER TABLE intel_Prices ADD Source NVARCHAR(100) NULL"),
                    ("ProductName",        "ALTER TABLE intel_Prices ADD ProductName NVARCHAR(200) NULL"),
                    ("Price",              "ALTER TABLE intel_Prices ADD Price DECIMAL(18, 4) NULL"),
                    ("PriceChange",        "ALTER TABLE intel_Prices ADD PriceChange DECIMAL(18, 4) NULL"),
                    ("PriceChangePercent", "ALTER TABLE intel_Prices ADD PriceChangePercent DECIMAL(8, 4) NULL"),
                    ("Currency",           "ALTER TABLE intel_Prices ADD Currency NVARCHAR(10) NULL DEFAULT 'PLN'"),
                    ("Unit",               "ALTER TABLE intel_Prices ADD Unit NVARCHAR(20) NULL DEFAULT 'kg'"),
                    ("PriceDate",          "ALTER TABLE intel_Prices ADD PriceDate DATE NULL"),
                    ("FetchedAt",          "ALTER TABLE intel_Prices ADD FetchedAt DATETIME NOT NULL DEFAULT GETDATE() WITH VALUES"),
                    ("ContractMonth",      "ALTER TABLE intel_Prices ADD ContractMonth NVARCHAR(20) NULL"),
                    ("Exchange",           "ALTER TABLE intel_Prices ADD Exchange NVARCHAR(50) NULL"),
                };

                foreach (var (column, sql) in migrations)
                {
                    var exists = await ColumnExistsAsync(conn, "intel_Prices", column);
                    if (!exists)
                    {
                        try
                        {
                            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 30 };
                            await cmd.ExecuteNonQueryAsync();
                            Debug.WriteLine($"[DatabaseSetup] ➕ Migracja: dodano kolumnę intel_Prices.{column}");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[DatabaseSetup] ⚠ Migracja intel_Prices.{column} nieudana: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseSetup] Migracja intel_Prices error: {ex.Message}");
            }
        }

        /// <summary>
        /// Poszerz wąskie kolumny intel_Articles — Perplexity zwraca długie tytuły (czasem >500 chars)
        /// i listy słów kluczowych (>500 chars). Stara migracja używała wąskich limitów co dawało
        /// "String or binary data would be truncated" przy zapisie.
        /// </summary>
        private async Task WidenArticlesColumnsAsync(SqlConnection conn)
        {
            try
            {
                // (Col, NewLen, IsNotNull, AlterSql)
                var widenings = new (string Column, int NewLen, bool NotNull, string Sql)[]
                {
                    ("Title",           1000, true,  "ALTER TABLE intel_Articles ALTER COLUMN Title NVARCHAR(1000) NOT NULL"),
                    ("MatchedKeywords", 2000, false, "ALTER TABLE intel_Articles ALTER COLUMN MatchedKeywords NVARCHAR(2000) NULL"),
                    ("RelatedTopics",   2000, false, "ALTER TABLE intel_Articles ALTER COLUMN RelatedTopics NVARCHAR(2000) NULL"),
                    ("Url",             2000, true,  "ALTER TABLE intel_Articles ALTER COLUMN Url NVARCHAR(2000) NOT NULL"),
                    ("SourceName",      500,  true,  "ALTER TABLE intel_Articles ALTER COLUMN SourceName NVARCHAR(500) NOT NULL"),
                };

                foreach (var (column, newLen, notNull, sql) in widenings)
                {
                    var currentLen = await GetColumnLengthAsync(conn, "intel_Articles", column);
                    if (currentLen > 0 && currentLen < newLen)
                    {
                        try
                        {
                            // Dla NOT NULL kolumn — najpierw UPDATE existing NULL → '' żeby ALTER nie failował
                            if (notNull)
                            {
                                using var prep = new SqlCommand(
                                    $"UPDATE intel_Articles SET [{column}] = '' WHERE [{column}] IS NULL",
                                    conn) { CommandTimeout = 60 };
                                var updated = await prep.ExecuteNonQueryAsync();
                                if (updated > 0)
                                    Debug.WriteLine($"[DatabaseSetup] ↔ Pre-widen {column}: wypełniono {updated} NULL → ''");
                            }

                            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
                            await cmd.ExecuteNonQueryAsync();
                            Debug.WriteLine($"[DatabaseSetup] ↔ Migracja: poszerzono intel_Articles.{column} ({currentLen} → {newLen})");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[DatabaseSetup] ⚠ Widen {column} nieudane: {ex.Message.Substring(0, Math.Min(120, ex.Message.Length))}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseSetup] WidenColumns error: {ex.Message}");
            }
        }

        private async Task<int> GetColumnLengthAsync(SqlConnection conn, string table, string column)
        {
            try
            {
                using var cmd = new SqlCommand(@"
SELECT CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = @t AND COLUMN_NAME = @c", conn);
                cmd.Parameters.AddWithValue("@t", table);
                cmd.Parameters.AddWithValue("@c", column);
                cmd.CommandTimeout = 10;
                var result = await cmd.ExecuteScalarAsync();
                if (result == null || result == DBNull.Value) return 0;
                var len = (int)result;
                return len == -1 ? int.MaxValue : len; // -1 = NVARCHAR(MAX)
            }
            catch { return 0; }
        }

        /// <summary>
        /// Tworzy indeksy pojedynczo z try/catch — brak jednej kolumny (np. PriceDate jeśli migracja
        /// nie zadziałała) nie wywala WSZYSTKICH indeksów.
        /// </summary>
        private async Task ExecuteIndexesIndividuallyAsync(SqlConnection conn)
        {
            var indexes = new (string Name, string Sql)[]
            {
                ("IX_intel_Articles_FetchedAt",      "CREATE INDEX IX_intel_Articles_FetchedAt ON intel_Articles(FetchedAt DESC)"),
                ("IX_intel_Articles_Category",       "CREATE INDEX IX_intel_Articles_Category ON intel_Articles(Category)"),
                ("IX_intel_Articles_Severity",       "CREATE INDEX IX_intel_Articles_Severity ON intel_Articles(Severity)"),
                ("IX_intel_Articles_PublishDate",    "CREATE INDEX IX_intel_Articles_PublishDate ON intel_Articles(PublishDate DESC)"),
                ("IX_intel_Articles_RelevanceScore", "CREATE INDEX IX_intel_Articles_RelevanceScore ON intel_Articles(RelevanceScore DESC)"),
                ("IX_intel_HpaiAlerts_ReportDate",   "CREATE INDEX IX_intel_HpaiAlerts_ReportDate ON intel_HpaiAlerts(ReportDate DESC)"),
                ("IX_intel_HpaiAlerts_Voivodeship",  "CREATE INDEX IX_intel_HpaiAlerts_Voivodeship ON intel_HpaiAlerts(Voivodeship)"),
                ("IX_intel_HpaiAlerts_IsActive",     "CREATE INDEX IX_intel_HpaiAlerts_IsActive ON intel_HpaiAlerts(IsActive)"),
                ("IX_intel_Prices_PriceDate",        "CREATE INDEX IX_intel_Prices_PriceDate ON intel_Prices(PriceDate DESC)"),
                ("IX_intel_Prices_ProductName",      "CREATE INDEX IX_intel_Prices_ProductName ON intel_Prices(ProductName)"),
                ("IX_intel_FetchLog_FetchTime",      "CREATE INDEX IX_intel_FetchLog_FetchTime ON intel_FetchLog(FetchTime DESC)"),
            };

            foreach (var (name, sql) in indexes)
            {
                try
                {
                    var checkSql = $"IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = '{name}') {sql}";
                    using var cmd = new SqlCommand(checkSql, conn) { CommandTimeout = 30 };
                    await cmd.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    // Brak kolumny lub inny problem — non-fatal
                    Debug.WriteLine($"[DatabaseSetup] ⚠ Index {name} skipped: {ex.Message.Substring(0, Math.Min(100, ex.Message.Length))}");
                }
            }
        }

        private async Task<bool> ColumnExistsAsync(SqlConnection conn, string table, string column)
        {
            try
            {
                using var cmd = new SqlCommand(
                    "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @t AND COLUMN_NAME = @c", conn);
                cmd.Parameters.AddWithValue("@t", table);
                cmd.Parameters.AddWithValue("@c", column);
                cmd.CommandTimeout = 10;
                var count = (int)(await cmd.ExecuteScalarAsync() ?? 0);
                return count > 0;
            }
            catch { return false; }
        }

        #region SQL Statements

        private const string CreateArticlesTableSql = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'intel_Articles')
            BEGIN
                CREATE TABLE intel_Articles (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    SourceId NVARCHAR(50) NOT NULL,
                    SourceName NVARCHAR(200) NOT NULL,
                    Title NVARCHAR(500) NOT NULL,
                    Url NVARCHAR(1000) NOT NULL,
                    UrlHash NVARCHAR(64) NOT NULL,
                    Summary NVARCHAR(MAX),
                    FullContent NVARCHAR(MAX),
                    PublishDate DATETIME NOT NULL,
                    FetchedAt DATETIME NOT NULL DEFAULT GETDATE(),
                    Language NVARCHAR(10) DEFAULT 'pl',

                    -- Categorization
                    Category NVARCHAR(50),
                    Severity NVARCHAR(20) DEFAULT 'info',
                    RelevanceScore INT DEFAULT 0,
                    IsRelevant BIT DEFAULT 0,
                    MatchedKeywords NVARCHAR(500),

                    -- AI Analysis
                    CeoAnalysis NVARCHAR(MAX),
                    SalesAnalysis NVARCHAR(MAX),
                    BuyerAnalysis NVARCHAR(MAX),
                    EducationalContent NVARCHAR(MAX),
                    CeoRecommendations NVARCHAR(MAX),
                    SalesRecommendations NVARCHAR(MAX),
                    BuyerRecommendations NVARCHAR(MAX),
                    KeyNumbers NVARCHAR(MAX),
                    RelatedTopics NVARCHAR(500),

                    AnalyzedAt DATETIME,
                    AiModel NVARCHAR(50),

                    -- Status
                    IsRead BIT DEFAULT 0,
                    IsBookmarked BIT DEFAULT 0,
                    UserNotes NVARCHAR(MAX),

                    CONSTRAINT UQ_intel_Articles_UrlHash UNIQUE (UrlHash)
                );
            END";

        private const string CreateHpaiAlertsTableSql = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'intel_HpaiAlerts')
            BEGIN
                CREATE TABLE intel_HpaiAlerts (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Title NVARCHAR(500),
                    AlertType NVARCHAR(50) NOT NULL,
                    Location NVARCHAR(200),
                    Voivodeship NVARCHAR(50),
                    County NVARCHAR(100),
                    Municipality NVARCHAR(100),
                    Latitude DECIMAL(10, 6),
                    Longitude DECIMAL(10, 6),
                    BirdCount INT,
                    ZoneRadiusKm INT,
                    ReportDate DATETIME NOT NULL,
                    SourceUrl NVARCHAR(1000),
                    FetchedAt DATETIME NOT NULL DEFAULT GETDATE(),
                    Severity NVARCHAR(20) DEFAULT 'warning',

                    -- Status tracking
                    IsActive BIT DEFAULT 1,
                    ResolvedDate DATETIME,
                    Notes NVARCHAR(MAX),

                    -- Distance from company
                    DistanceFromCompanyKm DECIMAL(10, 2),
                    RiskLevel NVARCHAR(20)
                );
            END";

        private const string CreateFetchLogTableSql = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'intel_FetchLog')
            BEGIN
                CREATE TABLE intel_FetchLog (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    FetchTime DATETIME NOT NULL DEFAULT GETDATE(),
                    FetchType NVARCHAR(50) DEFAULT 'Full',

                    -- Statistics
                    RssArticles INT DEFAULT 0,
                    ScrapedArticles INT DEFAULT 0,
                    TotalArticles INT DEFAULT 0,
                    RelevantArticles INT DEFAULT 0,
                    AnalyzedArticles INT DEFAULT 0,
                    HpaiAlerts INT DEFAULT 0,

                    -- Performance
                    DurationMs INT,

                    -- Status
                    Success BIT DEFAULT 1,
                    ErrorMessage NVARCHAR(MAX),

                    -- Source details
                    SourcesSuccessful NVARCHAR(MAX),
                    SourcesFailed NVARCHAR(MAX)
                );
            END";

        private const string CreatePricesTableSql = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'intel_Prices')
            BEGIN
                CREATE TABLE intel_Prices (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    PriceType NVARCHAR(50) NOT NULL, -- Poultry, Commodity, Retail, Exchange
                    Source NVARCHAR(100) NOT NULL,
                    ProductName NVARCHAR(200) NOT NULL,
                    Price DECIMAL(18, 4) NOT NULL,
                    PriceChange DECIMAL(18, 4),
                    PriceChangePercent DECIMAL(8, 4),
                    Currency NVARCHAR(10) DEFAULT 'PLN',
                    Unit NVARCHAR(20) DEFAULT 'kg',
                    PriceDate DATE NOT NULL,
                    FetchedAt DATETIME NOT NULL DEFAULT GETDATE(),

                    -- For commodity futures
                    ContractMonth NVARCHAR(20),
                    Exchange NVARCHAR(50),

                    -- Constraints
                    CONSTRAINT UQ_intel_Prices UNIQUE (PriceType, Source, ProductName, PriceDate)
                );
            END";

        private const string CreateSourcesTableSql = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'intel_Sources')
            BEGIN
                CREATE TABLE intel_Sources (
                    Id NVARCHAR(50) PRIMARY KEY,
                    Name NVARCHAR(200) NOT NULL,
                    Url NVARCHAR(1000) NOT NULL,
                    SourceType NVARCHAR(20) NOT NULL, -- RSS, Scraping, API
                    Category NVARCHAR(50),
                    Language NVARCHAR(10) DEFAULT 'pl',
                    Priority INT DEFAULT 5,
                    IsActive BIT DEFAULT 1,

                    -- Fetch tracking
                    LastFetchTime DATETIME,
                    LastSuccessTime DATETIME,
                    ConsecutiveFailures INT DEFAULT 0,
                    TotalArticlesFetched INT DEFAULT 0,

                    -- Settings
                    FetchIntervalMinutes INT DEFAULT 60,
                    RequiresAuth BIT DEFAULT 0,
                    CustomHeaders NVARCHAR(MAX),

                    CreatedAt DATETIME DEFAULT GETDATE(),
                    UpdatedAt DATETIME DEFAULT GETDATE()
                );
            END";

        private const string CreateDailySummaryTableSql = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'intel_DailySummary')
            BEGIN
                CREATE TABLE intel_DailySummary (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    SummaryDate DATE NOT NULL,
                    Headline NVARCHAR(500),

                    -- Role-based summaries
                    CeoSummary NVARCHAR(MAX),
                    SalesSummary NVARCHAR(MAX),
                    BuyerSummary NVARCHAR(MAX),

                    -- Market assessment
                    MarketMood NVARCHAR(20),
                    MarketMoodReason NVARCHAR(500),
                    WeeklyOutlook NVARCHAR(MAX),

                    -- Alerts and actions (JSON)
                    TopAlerts NVARCHAR(MAX),
                    ActionItems NVARCHAR(MAX),

                    -- Statistics
                    ArticlesAnalyzed INT,
                    CriticalCount INT,
                    WarningCount INT,
                    PositiveCount INT,

                    GeneratedAt DATETIME DEFAULT GETDATE(),
                    AiModel NVARCHAR(50),

                    CONSTRAINT UQ_intel_DailySummary_Date UNIQUE (SummaryDate)
                );
            END";

        // ════════════════════════════════════════════════════════════════════════
        // FAZA A — Story Clustering + Entity Tracking (2026-05-25)
        // ════════════════════════════════════════════════════════════════════════

        private const string CreateStoriesTableSql = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'intel_Stories')
            BEGIN
                CREATE TABLE intel_Stories (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Title NVARCHAR(500) NOT NULL,
                    StoryType VARCHAR(50) NOT NULL,        -- hpai_outbreak|price_movement|competitor_action|regulation|export_event|customer_event|other
                    FirstSeenAt DATETIME2 NOT NULL,
                    LastUpdatedAt DATETIME2 NOT NULL,
                    Status VARCHAR(20) NOT NULL,           -- developing|stable|closed
                    Severity INT NOT NULL,                 -- 1-5
                    PoultryRelevance INT NOT NULL,         -- 1-5
                    BusinessImpact NVARCHAR(2000) NULL,
                    EntitiesJson NVARCHAR(MAX) NULL,
                    LastDigest NVARCHAR(4000) NULL,
                    LastDigestAt DATETIME2 NULL,
                    ArticleCount INT NOT NULL DEFAULT 0,
                    INDEX IX_Stories_LastUpdated (LastUpdatedAt DESC),
                    INDEX IX_Stories_Status (Status, Severity DESC)
                );
            END";

        private const string CreateStoryArticlesTableSql = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'intel_StoryArticles')
            BEGIN
                CREATE TABLE intel_StoryArticles (
                    StoryId INT NOT NULL,
                    ArticleId INT NOT NULL,                -- BEZ FK do intel_Articles (retencja 30 dni je kasuje)
                    AddedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    PRIMARY KEY (StoryId, ArticleId),
                    FOREIGN KEY (StoryId) REFERENCES intel_Stories(Id) ON DELETE CASCADE
                );
            END";

        private const string CreateEntitiesTableSql = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'intel_Entities')
            BEGIN
                CREATE TABLE intel_Entities (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Name NVARCHAR(200) NOT NULL,
                    EntityType VARCHAR(50) NOT NULL,       -- competitor|customer|supplier|regulator|region|commodity|person|other
                    Aliases NVARCHAR(500) NULL,            -- 'Cedrob;Cedrob S.A.;Grupa Cedrob'
                    IsTracked BIT NOT NULL DEFAULT 1,
                    CustomerCode NVARCHAR(50) NULL,        -- kod kontrahenta z HANDEL SSCommon.STContractors (jeśli klient)
                    Notes NVARCHAR(2000) NULL,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    INDEX IX_Entities_Type (EntityType, IsTracked),
                    INDEX IX_Entities_Name (Name)
                );
            END";

        private const string CreateEntityMentionsTableSql = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'intel_EntityMentions')
            BEGIN
                CREATE TABLE intel_EntityMentions (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    EntityId INT NOT NULL,
                    ArticleId INT NOT NULL,                -- BEZ FK do intel_Articles (retencja)
                    StoryId INT NULL,
                    Sentiment INT NOT NULL,                -- -5..+5
                    Context NVARCHAR(1000) NULL,
                    MentionedAt DATETIME2 NOT NULL,        -- = PublishDate artykułu
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    FOREIGN KEY (EntityId) REFERENCES intel_Entities(Id) ON DELETE CASCADE,
                    INDEX IX_Mentions_EntityDate (EntityId, MentionedAt DESC),
                    INDEX IX_Mentions_Article (ArticleId)
                );
            END";

        // ════════════════════════════════════════════════════════════════════════
        // FAZA B — Trendy w czasie (2026-05-31)
        // ════════════════════════════════════════════════════════════════════════

        private const string CreateTrendDataPointsTableSql = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'intel_TrendDataPoints')
            BEGIN
                CREATE TABLE intel_TrendDataPoints (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    MetricKey VARCHAR(100) NOT NULL,       -- price.zywiec_brojler_kg | hpai.poland.outbreaks_week | mentions.cedrob.week | fx.eur_pln
                    Value DECIMAL(18,4) NOT NULL,
                    Unit NVARCHAR(20) NULL,                -- PLN/kg | count | ratio
                    SnapshotDate DATE NOT NULL,
                    SourceArticleId INT NULL,              -- gdy AIExtracted=1
                    SourceUrl NVARCHAR(1000) NULL,
                    AIExtracted BIT NOT NULL,              -- 1=AI z artykułu, 0=kalkulowane lokalnie
                    Confidence INT NOT NULL DEFAULT 3,     -- 1-5
                    Notes NVARCHAR(500) NULL,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    INDEX IX_Trend_MetricDate (MetricKey, SnapshotDate DESC)
                );
            END";

        // ════════════════════════════════════════════════════════════════════════
        // FAZA C — Chat sesyjny z pamięcią między dniami (2026-05-31)
        // ════════════════════════════════════════════════════════════════════════

        private const string CreateChatSessionsTableSql = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'intel_ChatSessions')
            BEGIN
                CREATE TABLE intel_ChatSessions (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    StartedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    EndedAt DATETIME2 NULL,
                    Summary NVARCHAR(2000) NULL,           -- AI 5-7 zdań po EndSession
                    KeyTopics NVARCHAR(500) NULL,          -- JSON array
                    OpenQuestions NVARCHAR(1000) NULL,     -- niedokończone wątki rozmowy
                    MessageCount INT NOT NULL DEFAULT 0,
                    INDEX IX_Sessions_Started (StartedAt DESC)
                );
            END";

        private const string CreateChatMessagesTableSql = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'intel_ChatMessages')
            BEGIN
                CREATE TABLE intel_ChatMessages (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    SessionId INT NOT NULL,
                    Role VARCHAR(20) NOT NULL,             -- user | assistant
                    Content NVARCHAR(MAX) NOT NULL,
                    SentAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    ReferencedStoryIds NVARCHAR(500) NULL, -- JSON array
                    ReferencedArticleIds NVARCHAR(500) NULL,
                    InputTokens INT NULL,
                    OutputTokens INT NULL,
                    CacheReadTokens INT NULL,
                    FOREIGN KEY (SessionId) REFERENCES intel_ChatSessions(Id) ON DELETE CASCADE,
                    INDEX IX_Messages_Session (SessionId, SentAt)
                );
            END";

        private const string CreateIndexesSql = @"
            -- Articles indexes
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_intel_Articles_FetchedAt')
                CREATE INDEX IX_intel_Articles_FetchedAt ON intel_Articles(FetchedAt DESC);

            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_intel_Articles_Category')
                CREATE INDEX IX_intel_Articles_Category ON intel_Articles(Category);

            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_intel_Articles_Severity')
                CREATE INDEX IX_intel_Articles_Severity ON intel_Articles(Severity);

            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_intel_Articles_PublishDate')
                CREATE INDEX IX_intel_Articles_PublishDate ON intel_Articles(PublishDate DESC);

            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_intel_Articles_RelevanceScore')
                CREATE INDEX IX_intel_Articles_RelevanceScore ON intel_Articles(RelevanceScore DESC);

            -- HPAI alerts indexes
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_intel_HpaiAlerts_ReportDate')
                CREATE INDEX IX_intel_HpaiAlerts_ReportDate ON intel_HpaiAlerts(ReportDate DESC);

            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_intel_HpaiAlerts_Voivodeship')
                CREATE INDEX IX_intel_HpaiAlerts_Voivodeship ON intel_HpaiAlerts(Voivodeship);

            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_intel_HpaiAlerts_IsActive')
                CREATE INDEX IX_intel_HpaiAlerts_IsActive ON intel_HpaiAlerts(IsActive);

            -- Prices indexes
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_intel_Prices_PriceDate')
                CREATE INDEX IX_intel_Prices_PriceDate ON intel_Prices(PriceDate DESC);

            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_intel_Prices_ProductName')
                CREATE INDEX IX_intel_Prices_ProductName ON intel_Prices(ProductName);

            -- Fetch log index
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_intel_FetchLog_FetchTime')
                CREATE INDEX IX_intel_FetchLog_FetchTime ON intel_FetchLog(FetchTime DESC);
        ";

        #endregion

        #region Data Migration

        /// <summary>
        /// Migruj istniejące dane seed do bazy (opcjonalne)
        /// </summary>
        public async Task MigrateExistingDataAsync()
        {
            // This can be used to migrate any existing seed data to the database
            // For now, just ensure tables exist
            await EnsureTablesExistAsync();
        }

        /// <summary>
        /// Wyczyść stare dane (starsze niż N dni)
        /// </summary>
        public async Task CleanupOldDataAsync(int daysToKeep = 30)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Delete old articles
                var sql = $@"
                    DELETE FROM intel_Articles
                    WHERE FetchedAt < DATEADD(day, -{daysToKeep}, GETDATE())
                      AND IsBookmarked = 0";

                using var cmd = new SqlCommand(sql, conn);
                var deleted = await cmd.ExecuteNonQueryAsync();

                Debug.WriteLine($"[DatabaseSetup] Cleaned up {deleted} old articles");

                // Delete old fetch logs
                sql = $@"
                    DELETE FROM intel_FetchLog
                    WHERE FetchTime < DATEADD(day, -{daysToKeep * 2}, GETDATE())";

                using var cmd2 = new SqlCommand(sql, conn);
                await cmd2.ExecuteNonQueryAsync();

                // Delete old resolved HPAI alerts
                sql = $@"
                    DELETE FROM intel_HpaiAlerts
                    WHERE IsActive = 0
                      AND ResolvedDate < DATEADD(day, -{daysToKeep * 3}, GETDATE())";

                using var cmd3 = new SqlCommand(sql, conn);
                await cmd3.ExecuteNonQueryAsync();

                // Faza A: sprzątanie sierot — StoryArticles/EntityMentions wskazujące na artykuły
                // już usunięte przez retencję (brak FK do intel_Articles by uniknąć kaskady).
                // Tabele mogą jeszcze nie istnieć na starych instalacjach → try/catch per zapytanie.
                await TryExecAsync(conn,
                    "DELETE sa FROM intel_StoryArticles sa WHERE NOT EXISTS (SELECT 1 FROM intel_Articles a WHERE a.Id = sa.ArticleId)");
                await TryExecAsync(conn,
                    "DELETE m FROM intel_EntityMentions m WHERE NOT EXISTS (SELECT 1 FROM intel_Articles a WHERE a.Id = m.ArticleId)");
                // Wątki bez żadnego artykułu (po sprzątnięciu) → zamknij.
                await TryExecAsync(conn,
                    "UPDATE intel_Stories SET Status='closed' WHERE Status<>'closed' AND Id NOT IN (SELECT DISTINCT StoryId FROM intel_StoryArticles)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseSetup] Cleanup error: {ex.Message}");
            }
        }

        /// <summary>Wykonaj zapytanie, ignorując błąd (np. tabela jeszcze nie istnieje).</summary>
        private static async Task TryExecAsync(SqlConnection conn, string sql)
        {
            try
            {
                using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 30 };
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseSetup] TryExec skipped: {ex.Message.Substring(0, System.Math.Min(80, ex.Message.Length))}");
            }
        }

        #endregion

        #region Statistics

        /// <summary>
        /// Pobierz statystyki bazy danych
        /// </summary>
        public async Task<DatabaseStatistics> GetStatisticsAsync()
        {
            var stats = new DatabaseStatistics();

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Article counts
                var sql = @"
                    SELECT
                        COUNT(*) as TotalArticles,
                        SUM(CASE WHEN FetchedAt >= DATEADD(day, -1, GETDATE()) THEN 1 ELSE 0 END) as ArticlesToday,
                        SUM(CASE WHEN FetchedAt >= DATEADD(day, -7, GETDATE()) THEN 1 ELSE 0 END) as ArticlesThisWeek,
                        SUM(CASE WHEN Severity = 'critical' THEN 1 ELSE 0 END) as CriticalCount,
                        SUM(CASE WHEN Severity = 'warning' THEN 1 ELSE 0 END) as WarningCount,
                        SUM(CASE WHEN IsRead = 0 THEN 1 ELSE 0 END) as UnreadCount
                    FROM intel_Articles";

                using var cmd = new SqlCommand(sql, conn);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    stats.TotalArticles = reader.GetInt32(0);
                    stats.ArticlesToday = reader.GetInt32(1);
                    stats.ArticlesThisWeek = reader.GetInt32(2);
                    stats.CriticalCount = reader.GetInt32(3);
                    stats.WarningCount = reader.GetInt32(4);
                    stats.UnreadCount = reader.GetInt32(5);
                }
                reader.Close();

                // HPAI alerts
                sql = "SELECT COUNT(*) FROM intel_HpaiAlerts WHERE IsActive = 1";
                using var cmd2 = new SqlCommand(sql, conn);
                stats.ActiveHpaiAlerts = (int)await cmd2.ExecuteScalarAsync();

                // Last fetch time
                sql = "SELECT TOP 1 FetchTime FROM intel_FetchLog ORDER BY FetchTime DESC";
                using var cmd3 = new SqlCommand(sql, conn);
                var lastFetch = await cmd3.ExecuteScalarAsync();
                stats.LastFetchTime = lastFetch as DateTime?;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseSetup] Statistics error: {ex.Message}");
            }

            return stats;
        }

        #endregion
    }

    public class DatabaseStatistics
    {
        public int TotalArticles { get; set; }
        public int ArticlesToday { get; set; }
        public int ArticlesThisWeek { get; set; }
        public int CriticalCount { get; set; }
        public int WarningCount { get; set; }
        public int UnreadCount { get; set; }
        public int ActiveHpaiAlerts { get; set; }
        public DateTime? LastFetchTime { get; set; }
    }
}
