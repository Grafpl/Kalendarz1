-- =====================================================
-- Tabela historii SMS-ów wysyłanych do handlowców
-- o wydaniach towarów do ich odbiorców
-- =====================================================

USE [LibraNet]
GO

-- Utwórz tabelę jeśli nie istnieje
IF NOT EXISTS (SELECT * FROM sys.objects WHERE name='SmsSprzedazHistoria' AND type='U')
BEGIN
    CREATE TABLE dbo.SmsSprzedazHistoria (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        ZamowienieId INT NOT NULL,
        KlientId INT NOT NULL,
        KlientNazwa NVARCHAR(200) NULL,
        Handlowiec NVARCHAR(100) NULL,
        TelefonHandlowca NVARCHAR(20) NULL,
        TrescSms NVARCHAR(500) NULL,
        IloscKg DECIMAL(18,2) NULL,
        CzasWyjazdu DATETIME NULL,
        DataWyslania DATETIME NOT NULL DEFAULT GETDATE(),
        KtoWyslal NVARCHAR(100) NULL,
        Status NVARCHAR(20) NOT NULL DEFAULT 'Wyslany',
        BladOpis NVARCHAR(500) NULL
    );

    -- Indeksy dla szybszego wyszukiwania
    CREATE INDEX IX_SmsSprzedaz_ZamowienieId ON dbo.SmsSprzedazHistoria(ZamowienieId);
    CREATE INDEX IX_SmsSprzedaz_Handlowiec ON dbo.SmsSprzedazHistoria(Handlowiec);
    CREATE INDEX IX_SmsSprzedaz_DataWyslania ON dbo.SmsSprzedazHistoria(DataWyslania);
    CREATE INDEX IX_SmsSprzedaz_KlientId ON dbo.SmsSprzedazHistoria(KlientId);

    PRINT 'Tabela SmsSprzedazHistoria została utworzona.'
END
ELSE
BEGIN
    PRINT 'Tabela SmsSprzedazHistoria już istnieje.'
END
GO

-- =====================================================
-- Tabela konfiguracji SMS dla handlowców
-- (opcjonalnie - można przechowywać preferencje)
-- =====================================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE name='SmsSprzedazKonfiguracja' AND type='U')
BEGIN
    CREATE TABLE dbo.SmsSprzedazKonfiguracja (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Handlowiec NVARCHAR(100) NOT NULL UNIQUE,
        TelefonOverride NVARCHAR(20) NULL,    -- Nadpisanie telefonu z OperatorzyKontakt
        SmsAktywny BIT NOT NULL DEFAULT 1,    -- Czy wysyłać SMS
        SmsPoWydaniu BIT NOT NULL DEFAULT 1,  -- SMS natychmiast po wydaniu
        SmsZbiorczyDzienny BIT NOT NULL DEFAULT 0, -- SMS zbiorczy na koniec dnia
        DataUtworzenia DATETIME NOT NULL DEFAULT GETDATE(),
        DataModyfikacji DATETIME NULL
    );

    CREATE INDEX IX_SmsKonfiguracja_Handlowiec ON dbo.SmsSprzedazKonfiguracja(Handlowiec);

    PRINT 'Tabela SmsSprzedazKonfiguracja została utworzona.'
END
GO

-- =====================================================
-- Widok do raportowania SMS handlowców
-- =====================================================

IF EXISTS (SELECT * FROM sys.views WHERE name='vw_SmsSprzedazRaport')
    DROP VIEW dbo.vw_SmsSprzedazRaport
GO

CREATE VIEW dbo.vw_SmsSprzedazRaport AS
SELECT
    h.Handlowiec,
    CAST(h.DataWyslania AS DATE) AS Data,
    COUNT(*) AS LiczbaSms,
    SUM(h.IloscKg) AS SumaKg,
    COUNT(DISTINCT h.KlientId) AS LiczbaKlientow,
    SUM(CASE WHEN h.Status = 'Wyslany' THEN 1 ELSE 0 END) AS Wyslane,
    SUM(CASE WHEN h.Status = 'Kopiowany' THEN 1 ELSE 0 END) AS Kopiowane,
    SUM(CASE WHEN h.Status = 'Blad' THEN 1 ELSE 0 END) AS Bledy
FROM dbo.SmsSprzedazHistoria h
GROUP BY h.Handlowiec, CAST(h.DataWyslania AS DATE)
GO

PRINT 'Widok vw_SmsSprzedazRaport został utworzony.'
GO

-- =====================================================
-- Procedura do pobierania statystyk SMS
-- =====================================================

IF EXISTS (SELECT * FROM sys.procedures WHERE name='sp_SmsSprzedazStatystyki')
    DROP PROCEDURE dbo.sp_SmsSprzedazStatystyki
GO

CREATE PROCEDURE dbo.sp_SmsSprzedazStatystyki
    @DataOd DATE = NULL,
    @DataDo DATE = NULL,
    @Handlowiec NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @DataOd IS NULL SET @DataOd = DATEADD(DAY, -30, GETDATE())
    IF @DataDo IS NULL SET @DataDo = GETDATE()

    SELECT
        h.Handlowiec,
        COUNT(*) AS LiczbaSms,
        SUM(h.IloscKg) AS SumaKg,
        COUNT(DISTINCT h.KlientId) AS LiczbaKlientow,
        COUNT(DISTINCT CAST(h.DataWyslania AS DATE)) AS LiczbaDni,
        MIN(h.DataWyslania) AS PierwszySms,
        MAX(h.DataWyslania) AS OstatniSms
    FROM dbo.SmsSprzedazHistoria h
    WHERE h.DataWyslania >= @DataOd
      AND h.DataWyslania <= DATEADD(DAY, 1, @DataDo)
      AND (@Handlowiec IS NULL OR h.Handlowiec = @Handlowiec)
      AND h.Status IN ('Wyslany', 'Kopiowany')
    GROUP BY h.Handlowiec
    ORDER BY SumaKg DESC
END
GO

PRINT 'Procedura sp_SmsSprzedazStatystyki została utworzona.'
GO
