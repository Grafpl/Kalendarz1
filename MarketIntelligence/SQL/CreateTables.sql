-- ============================================================
-- MARKET INTELLIGENCE - Tabele SQL Server
-- Baza: LibraNet @ 192.168.0.109
-- Schemat zsynchronizowany z DatabaseSetup.cs (kanoniczne źródło)
-- Aktualizacja: 2026-05-15 (dopisane intel_HpaiAlerts + intel_FetchLog + intel_Sources + intel_DailySummary)
-- ============================================================

-- 1. ARTYKUŁY (pełny schemat zgodny z DatabaseSetup.CreateArticlesTableSql)
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
END;

-- 2. HPAI alerty (wcześniej brakowało w tym pliku — INSERT'y w NewsFetchOrchestrator.cs:448 wymagają tej tabeli)
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

        IsActive BIT DEFAULT 1,
        ResolvedDate DATETIME,
        Notes NVARCHAR(MAX),

        DistanceFromCompanyKm DECIMAL(10, 2),
        RiskLevel NVARCHAR(20)
    );
END;

-- 3. Log pobrań (wcześniej brakowało — używane w NewsFetchOrchestrator.LogFetchAsync + DatabaseStatistics)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'intel_FetchLog')
BEGIN
    CREATE TABLE intel_FetchLog (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        FetchTime DATETIME NOT NULL DEFAULT GETDATE(),
        FetchType NVARCHAR(50) DEFAULT 'Full',

        RssArticles INT DEFAULT 0,
        ScrapedArticles INT DEFAULT 0,
        TotalArticles INT DEFAULT 0,
        RelevantArticles INT DEFAULT 0,
        AnalyzedArticles INT DEFAULT 0,
        HpaiAlerts INT DEFAULT 0,

        DurationMs INT,

        Success BIT DEFAULT 1,
        ErrorMessage NVARCHAR(MAX),

        SourcesSuccessful NVARCHAR(MAX),
        SourcesFailed NVARCHAR(MAX)
    );
END;

-- 4. Ceny rynkowe (rozszerzony schemat z PriceType/Currency/ContractMonth)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'intel_Prices')
BEGIN
    CREATE TABLE intel_Prices (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        PriceType NVARCHAR(50) NOT NULL,
        Source NVARCHAR(100) NOT NULL,
        ProductName NVARCHAR(200) NOT NULL,
        Price DECIMAL(18, 4) NOT NULL,
        PriceChange DECIMAL(18, 4),
        PriceChangePercent DECIMAL(8, 4),
        Currency NVARCHAR(10) DEFAULT 'PLN',
        Unit NVARCHAR(20) DEFAULT 'kg',
        PriceDate DATE NOT NULL,
        FetchedAt DATETIME NOT NULL DEFAULT GETDATE(),

        ContractMonth NVARCHAR(20),
        Exchange NVARCHAR(50),

        CONSTRAINT UQ_intel_Prices UNIQUE (PriceType, Source, ProductName, PriceDate)
    );
END;

-- 5. Konfiguracja źródeł (RSS / scrapingu / API)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'intel_Sources')
BEGIN
    CREATE TABLE intel_Sources (
        Id NVARCHAR(50) PRIMARY KEY,
        Name NVARCHAR(200) NOT NULL,
        Url NVARCHAR(1000) NOT NULL,
        SourceType NVARCHAR(20) NOT NULL,
        Category NVARCHAR(50),
        Language NVARCHAR(10) DEFAULT 'pl',
        Priority INT DEFAULT 5,
        IsActive BIT DEFAULT 1,

        LastFetchTime DATETIME,
        LastSuccessTime DATETIME,
        ConsecutiveFailures INT DEFAULT 0,
        TotalArticlesFetched INT DEFAULT 0,

        FetchIntervalMinutes INT DEFAULT 60,
        RequiresAuth BIT DEFAULT 0,
        CustomHeaders NVARCHAR(MAX),

        CreatedAt DATETIME DEFAULT GETDATE(),
        UpdatedAt DATETIME DEFAULT GETDATE()
    );
END;

-- 6. Dzienne streszczenia (rolowe summary z Claude)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'intel_DailySummary')
BEGIN
    CREATE TABLE intel_DailySummary (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        SummaryDate DATE NOT NULL,
        Headline NVARCHAR(500),

        CeoSummary NVARCHAR(MAX),
        SalesSummary NVARCHAR(MAX),
        BuyerSummary NVARCHAR(MAX),

        MarketMood NVARCHAR(20),
        MarketMoodReason NVARCHAR(500),
        WeeklyOutlook NVARCHAR(MAX),

        TopAlerts NVARCHAR(MAX),
        ActionItems NVARCHAR(MAX),

        ArticlesAnalyzed INT,
        CriticalCount INT,
        WarningCount INT,
        PositiveCount INT,

        GeneratedAt DATETIME DEFAULT GETDATE(),
        AiModel NVARCHAR(50),

        CONSTRAINT UQ_intel_DailySummary_Date UNIQUE (SummaryDate)
    );
END;

-- 7. Ceny pasz/surowców (legacy — zachowane dla wstecznej kompatybilności)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'intel_FeedPrices')
BEGIN
    CREATE TABLE intel_FeedPrices (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Date DATE NOT NULL,
        Commodity NVARCHAR(50) NOT NULL,
        Value DECIMAL(10,2) NOT NULL,
        Unit NVARCHAR(20) DEFAULT 'EUR/t',
        Market NVARCHAR(50) DEFAULT 'MATIF',
        CreatedAt DATETIME DEFAULT GETDATE()
    );
END;

-- 8. Konkurencja
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'intel_Competitors')
BEGIN
    CREATE TABLE intel_Competitors (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(200) NOT NULL,
        Owner NVARCHAR(200),
        Country NVARCHAR(50),
        Revenue NVARCHAR(100),
        DailyCapacity NVARCHAR(100),
        Notes NVARCHAR(MAX),
        LastUpdated DATETIME DEFAULT GETDATE()
    );
END;

-- 9. Benchmark EU
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'intel_EuBenchmark')
BEGIN
    CREATE TABLE intel_EuBenchmark (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Date DATE NOT NULL,
        Country NVARCHAR(50) NOT NULL,
        PricePer100kg DECIMAL(10,2) NOT NULL,
        ChangeMonthPercent DECIMAL(5,2),
        CreatedAt DATETIME DEFAULT GETDATE()
    );
END;

-- ============================================================
-- INDEKSY
-- ============================================================

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

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_intel_HpaiAlerts_ReportDate')
    CREATE INDEX IX_intel_HpaiAlerts_ReportDate ON intel_HpaiAlerts(ReportDate DESC);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_intel_HpaiAlerts_Voivodeship')
    CREATE INDEX IX_intel_HpaiAlerts_Voivodeship ON intel_HpaiAlerts(Voivodeship);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_intel_HpaiAlerts_IsActive')
    CREATE INDEX IX_intel_HpaiAlerts_IsActive ON intel_HpaiAlerts(IsActive);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_intel_Prices_PriceDate')
    CREATE INDEX IX_intel_Prices_PriceDate ON intel_Prices(PriceDate DESC);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_intel_Prices_ProductName')
    CREATE INDEX IX_intel_Prices_ProductName ON intel_Prices(ProductName);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_intel_FetchLog_FetchTime')
    CREATE INDEX IX_intel_FetchLog_FetchTime ON intel_FetchLog(FetchTime DESC);

PRINT 'Tabele Market Intelligence utworzone/zweryfikowane pomyślnie.';
