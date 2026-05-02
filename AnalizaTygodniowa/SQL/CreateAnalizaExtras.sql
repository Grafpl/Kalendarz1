-- =================================================================
-- AnalizaTygodniowa — tabele pomocnicze (Dashboard Analityczny V2)
-- Uruchom JEDEN RAZ na bazie HANDEL (192.168.0.112)
-- =================================================================

USE HANDEL;
GO

-- ─────────────────────────────────────────────────────────────────
-- 1) AnalizaPresets — zestawy filtrów per użytkownik
-- ─────────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.AnalizaPresets', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AnalizaPresets (
        Id          INT IDENTITY(1,1) PRIMARY KEY,
        UserId      NVARCHAR(50)  NOT NULL,        -- App.UserID
        Nazwa       NVARCHAR(100) NOT NULL,
        FilterJson  NVARCHAR(MAX) NOT NULL,        -- serializowany AnalizaTygodniowaFilter
        DataUtw     DATETIME      NOT NULL CONSTRAINT DF_AnalizaPresets_DataUtw DEFAULT GETDATE(),
        DataMod     DATETIME      NULL,
        Domyslny    BIT           NOT NULL CONSTRAINT DF_AnalizaPresets_Domyslny DEFAULT 0,
        CONSTRAINT UQ_AnalizaPresets_User_Nazwa UNIQUE (UserId, Nazwa)
    );
    CREATE INDEX IX_AnalizaPresets_User ON dbo.AnalizaPresets(UserId);
    PRINT '✓ Utworzono AnalizaPresets';
END
ELSE PRINT '◯ AnalizaPresets już istnieje';
GO

-- ─────────────────────────────────────────────────────────────────
-- 2) AnalizaAlertyKonfig — progi alertów per użytkownik
-- ─────────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.AnalizaAlertyKonfig', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AnalizaAlertyKonfig (
        UserId             NVARCHAR(50)  NOT NULL PRIMARY KEY,
        ProgWariancjiPct   DECIMAL(6,2)  NOT NULL CONSTRAINT DF_AnalizaAlerty_PWar DEFAULT 15.0,  -- |Δ|/produkcja > X%
        ProgRyzykaKg       DECIMAL(12,2) NOT NULL CONSTRAINT DF_AnalizaAlerty_PRyz DEFAULT 500.0, -- nadwyżka > X kg
        ProgMapePct        DECIMAL(6,2)  NOT NULL CONSTRAINT DF_AnalizaAlerty_PMap DEFAULT 30.0,  -- MAPE > X%
        AlertEmail         NVARCHAR(200) NULL,
        AutoRefreshMin     INT           NOT NULL CONSTRAINT DF_AnalizaAlerty_Auto DEFAULT 0,     -- 0 = wyłączone
        DataMod            DATETIME      NULL
    );
    PRINT '✓ Utworzono AnalizaAlertyKonfig';
END
ELSE PRINT '◯ AnalizaAlertyKonfig już istnieje';
GO

-- ─────────────────────────────────────────────────────────────────
-- 3) PrognozaOverride — ręczna korekta prognozy planisty
-- ─────────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.PrognozaOverride', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PrognozaOverride (
        Id            INT IDENTITY(1,1) PRIMARY KEY,
        Data          DATE          NOT NULL,
        IdTw          INT           NULL,                 -- NULL = override globalny
        IloscPlanowana DECIMAL(12,3) NOT NULL,
        Notatka       NVARCHAR(500) NULL,
        UserId        NVARCHAR(50)  NOT NULL,
        DataUtw       DATETIME      NOT NULL CONSTRAINT DF_PrognozaOv_DataUtw DEFAULT GETDATE(),
        CONSTRAINT UQ_PrognozaOverride UNIQUE (Data, IdTw)
    );
    CREATE INDEX IX_PrognozaOverride_Data ON dbo.PrognozaOverride(Data);
    PRINT '✓ Utworzono PrognozaOverride';
END
ELSE PRINT '◯ PrognozaOverride już istnieje';
GO

PRINT '';
PRINT '════════════════════════════════════════════════════';
PRINT '  Skrypt zakończony.';
PRINT '  Tabele: AnalizaPresets, AnalizaAlertyKonfig,';
PRINT '          PrognozaOverride';
PRINT '════════════════════════════════════════════════════';
