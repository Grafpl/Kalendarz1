-- ════════════════════════════════════════════════════════════════════
-- GUS — tabele historii sprawozdań GUS (P-02, R-09A, DG-1, C-01)
-- Baza: LibraNet (192.168.0.109). Tworzona raz, idempotentnie.
-- ════════════════════════════════════════════════════════════════════

-- 1) GusSubmissions — historia wygenerowanych/wysłanych sprawozdań
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'GusSubmissions' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.GusSubmissions (
        Id                  INT IDENTITY(1,1) PRIMARY KEY,
        Formularz           VARCHAR(20) NOT NULL,              -- 'P-02','R-09A','DG-1','C-01'
        FormularzWersja     VARCHAR(10) NOT NULL DEFAULT '',   -- np. '16.0'
        OkresOd             DATE NOT NULL,
        OkresDo             DATE NOT NULL,
        Rok                 INT NOT NULL,
        Miesiac             INT NULL,                          -- 1-12 dla miesięcznych, NULL dla rocznych
        Regon               VARCHAR(14) NOT NULL,
        GeneratedXml        NVARCHAR(MAX) NULL,                -- pełna treść XML (audit + reimport)
        PlikXml             NVARCHAR(500) NULL,                -- ścieżka do zapisanego pliku
        Status              VARCHAR(20) NOT NULL DEFAULT 'Draft',
            -- Draft / Generated / Exported / Sent / Failed
        ValidationLog       NVARCHAR(MAX) NULL,                -- log walidacji (XSD lub własna)
        ErrorMessage        NVARCHAR(1000) NULL,
        NumerWPortalu       VARCHAR(50) NULL,                  -- numer identyfikacyjny po imporcie do PS (wpisuje user)
        IloscPozycji        INT NOT NULL DEFAULT 0,            -- ile linii produktów
        SumaWartosc         DECIMAL(18,2) NOT NULL DEFAULT 0,  -- suma wartości dla szybkiego podglądu
        GeneratedBy         INT NULL,                          -- operators.ID
        GeneratedAt         DATETIME NOT NULL DEFAULT GETDATE(),
        ExportedAt          DATETIME NULL,
        SentAt              DATETIME NULL,
        Notatki             NVARCHAR(1000) NULL
    );

    CREATE INDEX IX_GusSubmissions_Formularz_Okres ON dbo.GusSubmissions(Formularz, OkresOd DESC);
    CREATE INDEX IX_GusSubmissions_Status ON dbo.GusSubmissions(Status);
    CREATE INDEX IX_GusSubmissions_Rok_Miesiac ON dbo.GusSubmissions(Rok, Miesiac);

    PRINT 'Tabela dbo.GusSubmissions utworzona.';
END
ELSE
BEGIN
    PRINT 'Tabela dbo.GusSubmissions już istnieje — pomijam CREATE.';
END
GO

-- 2) GusPkwiuMapping — opcjonalne nadpisanie HM.TW.sww (jeśli puste lub błędne w Sage)
--    Klucz: (kod_towaru) → (PKWiU + jednostka + uwzględniać_w_p02)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'GusPkwiuMapping' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.GusPkwiuMapping (
        Id                  INT IDENTITY(1,1) PRIMARY KEY,
        KodTowaru           NVARCHAR(100) NOT NULL,            -- HM.TW.kod
        IdTowaru            INT NULL,                          -- HM.TW.id (cache, dla wydajności)
        PkwiuKod            VARCHAR(20) NOT NULL,              -- np. '10.12.10-50'
        Jednostka           VARCHAR(10) NOT NULL DEFAULT 'kg', -- kg/szt/t
        UwzgledniacP02      BIT NOT NULL DEFAULT 1,            -- 0 = wykluczyć z P-02 (np. usługi)
        Komentarz           NVARCHAR(500) NULL,
        CreatedBy           INT NULL,
        CreatedAt           DATETIME NOT NULL DEFAULT GETDATE(),
        ModifiedAt          DATETIME NULL,
        CONSTRAINT UQ_GusPkwiuMapping_Kod UNIQUE (KodTowaru)
    );

    CREATE INDEX IX_GusPkwiuMapping_Pkwiu ON dbo.GusPkwiuMapping(PkwiuKod);

    PRINT 'Tabela dbo.GusPkwiuMapping utworzona.';
END
ELSE
BEGIN
    PRINT 'Tabela dbo.GusPkwiuMapping już istnieje — pomijam CREATE.';
END
GO
