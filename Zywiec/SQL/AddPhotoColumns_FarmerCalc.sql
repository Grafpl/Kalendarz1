-- =============================================
-- Skrypt migracyjny: Dodanie kolumn dla zdjec z wazenia
-- Tabela: dbo.FarmerCalc
-- Data: 2026-01-11
-- Opis: Dodaje kolumny ZdjecieTaraPath i ZdjecieBruttoPath
--       do przechowywania sciezek do zdjec z wagi samochodowej
-- =============================================

USE [LibraNet]
GO

-- Dodaj kolumne ZdjecieTaraPath jesli nie istnieje
IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.FarmerCalc')
    AND name = 'ZdjecieTaraPath'
)
BEGIN
    ALTER TABLE dbo.FarmerCalc
    ADD ZdjecieTaraPath NVARCHAR(500) NULL;

    PRINT 'Dodano kolumne ZdjecieTaraPath do tabeli FarmerCalc';
END
ELSE
BEGIN
    PRINT 'Kolumna ZdjecieTaraPath juz istnieje w tabeli FarmerCalc';
END
GO

-- Dodaj kolumne ZdjecieBruttoPath jesli nie istnieje
IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.FarmerCalc')
    AND name = 'ZdjecieBruttoPath'
)
BEGIN
    ALTER TABLE dbo.FarmerCalc
    ADD ZdjecieBruttoPath NVARCHAR(500) NULL;

    PRINT 'Dodano kolumne ZdjecieBruttoPath do tabeli FarmerCalc';
END
ELSE
BEGIN
    PRINT 'Kolumna ZdjecieBruttoPath juz istnieje w tabeli FarmerCalc';
END
GO

-- =============================================
-- Komentarz do kolumn (opcjonalnie)
-- =============================================
EXEC sys.sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'Pelna sciezka sieciowa do zdjecia TARA z wagi samochodowej (np. \\192.168.0.170\Install\WagaSamochodowa\2026-01-11\AVILOG_06-30-15_WGM12345_WE54321_TARA.jpg)',
    @level0type = N'SCHEMA', @level0name = N'dbo',
    @level1type = N'TABLE',  @level1name = N'FarmerCalc',
    @level2type = N'COLUMN', @level2name = N'ZdjecieTaraPath';
GO

EXEC sys.sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'Pelna sciezka sieciowa do zdjecia BRUTTO z wagi samochodowej (np. \\192.168.0.170\Install\WagaSamochodowa\2026-01-11\AVILOG_06-35-22_WGM12345_WE54321_BRUTTO.jpg)',
    @level0type = N'SCHEMA', @level0name = N'dbo',
    @level1type = N'TABLE',  @level1name = N'FarmerCalc',
    @level2type = N'COLUMN', @level2name = N'ZdjecieBruttoPath';
GO

PRINT 'Migracja zakonczona pomyslnie!';
GO
