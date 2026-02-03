-- ============================================================
-- MARKET INTELLIGENCE - Tabele SQL Server
-- Baza: LibraNet @ 192.168.0.109
-- Data: 2026-02-03
-- ============================================================

-- Tabela główna: artykuły/newsy
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'intel_Articles')
BEGIN
    CREATE TABLE dbo.intel_Articles (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Title NVARCHAR(500) NOT NULL,
        Body NVARCHAR(MAX),
        Source NVARCHAR(200),
        SourceUrl NVARCHAR(500),
        Category NVARCHAR(50) NOT NULL,
        Severity NVARCHAR(20) NOT NULL,
        AiAnalysis NVARCHAR(MAX),
        PublishDate DATETIME NOT NULL,
        CreatedAt DATETIME DEFAULT GETDATE(),
        IsActive BIT DEFAULT 1,
        Tags NVARCHAR(500)
    );

    CREATE INDEX IX_intel_Articles_Category ON intel_Articles(Category);
    CREATE INDEX IX_intel_Articles_Severity ON intel_Articles(Severity);
    CREATE INDEX IX_intel_Articles_PublishDate ON intel_Articles(PublishDate DESC);
END;

-- Ceny skupu i rynkowe
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'intel_Prices')
BEGIN
    CREATE TABLE dbo.intel_Prices (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Date DATE NOT NULL,
        PriceType NVARCHAR(50) NOT NULL,
        Value DECIMAL(10,2) NOT NULL,
        Unit NVARCHAR(20) DEFAULT 'PLN/kg',
        Source NVARCHAR(100),
        CreatedAt DATETIME DEFAULT GETDATE()
    );

    CREATE INDEX IX_intel_Prices_Date ON intel_Prices(Date DESC);
    CREATE INDEX IX_intel_Prices_PriceType ON intel_Prices(PriceType);
END;

-- Ceny pasz i surowców
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'intel_FeedPrices')
BEGIN
    CREATE TABLE dbo.intel_FeedPrices (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Date DATE NOT NULL,
        Commodity NVARCHAR(50) NOT NULL,
        Value DECIMAL(10,2) NOT NULL,
        Unit NVARCHAR(20) DEFAULT 'EUR/t',
        Market NVARCHAR(50) DEFAULT 'MATIF',
        CreatedAt DATETIME DEFAULT GETDATE()
    );

    CREATE INDEX IX_intel_FeedPrices_Date ON intel_FeedPrices(Date DESC);
    CREATE INDEX IX_intel_FeedPrices_Commodity ON intel_FeedPrices(Commodity);
END;

-- HPAI ogniska
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'intel_HpaiOutbreaks')
BEGIN
    CREATE TABLE dbo.intel_HpaiOutbreaks (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Date DATE NOT NULL,
        Region NVARCHAR(100) NOT NULL,
        Country NVARCHAR(50) DEFAULT 'PL',
        BirdsAffected INT,
        OutbreakCount INT DEFAULT 1,
        Notes NVARCHAR(500),
        CreatedAt DATETIME DEFAULT GETDATE()
    );

    CREATE INDEX IX_intel_HpaiOutbreaks_Date ON intel_HpaiOutbreaks(Date DESC);
    CREATE INDEX IX_intel_HpaiOutbreaks_Country ON intel_HpaiOutbreaks(Country);
END;

-- Konkurencja
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'intel_Competitors')
BEGIN
    CREATE TABLE dbo.intel_Competitors (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(200) NOT NULL,
        Owner NVARCHAR(200),
        Country NVARCHAR(50),
        Revenue NVARCHAR(100),
        DailyCapacity NVARCHAR(100),
        Notes NVARCHAR(MAX),
        LastUpdated DATETIME DEFAULT GETDATE()
    );

    CREATE INDEX IX_intel_Competitors_Country ON intel_Competitors(Country);
END;

-- Benchmark EU
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'intel_EuBenchmark')
BEGIN
    CREATE TABLE dbo.intel_EuBenchmark (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Date DATE NOT NULL,
        Country NVARCHAR(50) NOT NULL,
        PricePer100kg DECIMAL(10,2) NOT NULL,
        ChangeMonthPercent DECIMAL(5,2),
        CreatedAt DATETIME DEFAULT GETDATE()
    );

    CREATE INDEX IX_intel_EuBenchmark_Date ON intel_EuBenchmark(Date DESC);
END;

PRINT 'Tabele Market Intelligence utworzone pomyślnie.';
