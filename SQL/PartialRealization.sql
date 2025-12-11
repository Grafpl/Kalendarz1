-- =====================================================
-- CZĘŚCIOWA REALIZACJA ZAMÓWIEŃ
-- Skrypt dodaje kolumny do obsługi częściowej realizacji
-- =====================================================

-- 1. Dodaj kolumny do ZamowieniaMiesoTowar dla ilości zrealizowanej i powodu braku
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ZamowieniaMiesoTowar') AND name = 'IloscZrealizowana')
BEGIN
    ALTER TABLE dbo.ZamowieniaMiesoTowar ADD IloscZrealizowana DECIMAL(18,2) NULL;
    PRINT 'Dodano kolumnę IloscZrealizowana do ZamowieniaMiesoTowar';
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ZamowieniaMiesoTowar') AND name = 'PowodBraku')
BEGIN
    ALTER TABLE dbo.ZamowieniaMiesoTowar ADD PowodBraku NVARCHAR(500) NULL;
    PRINT 'Dodano kolumnę PowodBraku do ZamowieniaMiesoTowar';
END

-- 2. Dodaj kolumnę do ZamowieniaMieso dla procentu realizacji
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ZamowieniaMieso') AND name = 'ProcentRealizacji')
BEGIN
    ALTER TABLE dbo.ZamowieniaMieso ADD ProcentRealizacji DECIMAL(5,2) NULL;
    PRINT 'Dodano kolumnę ProcentRealizacji do ZamowieniaMieso';
END

-- 3. Dodaj kolumnę CzyCzesciowoZrealizowane
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ZamowieniaMieso') AND name = 'CzyCzesciowoZrealizowane')
BEGIN
    ALTER TABLE dbo.ZamowieniaMieso ADD CzyCzesciowoZrealizowane BIT DEFAULT 0;
    PRINT 'Dodano kolumnę CzyCzesciowoZrealizowane do ZamowieniaMieso';
END

PRINT '';
PRINT 'Konfiguracja częściowej realizacji zakończona pomyślnie.';
