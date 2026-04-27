-- =====================================================
-- Persistowana kolumna ZamowieniaMiesoTowar.CenaNum
-- Baza: LibraNet (192.168.0.109)
-- Uruchomić raz w SSMS jako użytkownik z prawami ALTER TABLE
-- =====================================================
--
-- Problem: zp.Cena jest VARCHAR. Każde zapytanie dashboardu wykonuje
--   TRY_CAST(NULLIF(zp.Cena, '') AS DECIMAL(18,2))
-- na milionach wierszy → znaczący narzut CPU per query.
--
-- Rozwiązanie: kolumna kalkulowana PERSISTED — wartość jest liczona
-- raz przy INSERT/UPDATE i zapisywana fizycznie w wierszu. Zapytania
-- czytają ją jak zwykłą kolumnę DECIMAL (zero CPU per row).
--
-- UWAGA: PERSISTED wymaga aby DP.Cena nie miał nigdy wartości typu
-- "1,99" (przecinek). Jeśli takie się trafiają — TRY_CAST zwróci NULL,
-- co jest poprawne (i zachowuje obecne zachowanie aplikacji).
-- =====================================================

USE LibraNet;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.ZamowieniaMiesoTowar')
      AND name = 'CenaNum'
)
BEGIN
    ALTER TABLE dbo.ZamowieniaMiesoTowar
    ADD CenaNum AS TRY_CAST(NULLIF(Cena, '') AS DECIMAL(18,2)) PERSISTED;

    PRINT 'Dodano kolumnę dbo.ZamowieniaMiesoTowar.CenaNum (PERSISTED).';
END
ELSE
    PRINT 'Kolumna CenaNum już istnieje — pomijam.';
GO

-- =====================================================
-- Po uruchomieniu tego skryptu skontaktuj się z deweloperem,
-- aby przełączył kod aplikacji z TRY_CAST(NULLIF(zp.Cena,'')...)
-- na bezpośredni odczyt zp.CenaNum we wszystkich zapytaniach
-- (Mroznia.cs + HandlowiecDashboard*.cs + powiązane).
-- =====================================================
