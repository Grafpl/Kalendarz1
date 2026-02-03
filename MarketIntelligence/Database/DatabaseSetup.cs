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
            _connectionString = connectionString ??
                "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
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

                // Create indexes
                await ExecuteNonQueryAsync(conn, CreateIndexesSql);

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
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseSetup] Cleanup error: {ex.Message}");
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
