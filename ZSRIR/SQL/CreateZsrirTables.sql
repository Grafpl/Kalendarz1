-- ════════════════════════════════════════════════════════════════════
-- ZSRIR — tabela historii wysyłek do API Ministerstwa Rolnictwa
-- Baza: LibraNet (192.168.0.109). Tworzona raz, idempotentnie.
-- Sergiusz Piórkowski, 2026-05-19.
-- ════════════════════════════════════════════════════════════════════

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ZsrirSubmissions' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.ZsrirSubmissions (
        Id                      INT IDENTITY(1,1) PRIMARY KEY,
        OkresOd                 DATE NOT NULL,
        OkresDo                 DATE NOT NULL,
        KategoriaTowaru         NVARCHAR(80) NOT NULL,            -- np. "Brojler kurzy"
        CommodityGroupId        INT NULL,                          -- ID z API
        KgRazem                 DECIMAL(18,2) NOT NULL DEFAULT 0,
        TonyRazem               DECIMAL(18,3) NOT NULL DEFAULT 0,
        WartoscNetto            DECIMAL(18,2) NOT NULL DEFAULT 0,
        CenaZlTona              DECIMAL(18,2) NOT NULL DEFAULT 0,
        FormReportingPeriodId   INT NULL,                          -- ID okresu z API
        DataSupplierId          INT NULL,                          -- ID dostawcy z API
        Status                  VARCHAR(20) NOT NULL DEFAULT 'Pending',  -- Pending/Sent/Failed/Zero
        ApiResponse             NVARCHAR(MAX) NULL,                -- raw JSON odpowiedzi
        ErrorMessage            NVARCHAR(1000) NULL,
        WyslanyPrzez            INT NULL,                          -- operators.ID
        WyslanyDataCzas         DATETIME NULL,
        CreatedAt               DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT UQ_ZsrirSubmissions_Okres_Kategoria UNIQUE (OkresOd, OkresDo, KategoriaTowaru)
    );

    CREATE INDEX IX_ZsrirSubmissions_OkresOd ON dbo.ZsrirSubmissions(OkresOd DESC);
    CREATE INDEX IX_ZsrirSubmissions_Status ON dbo.ZsrirSubmissions(Status);

    PRINT 'Tabela dbo.ZsrirSubmissions utworzona.';
END
ELSE
BEGIN
    PRINT 'Tabela dbo.ZsrirSubmissions już istnieje — pomijam CREATE.';
END
GO
