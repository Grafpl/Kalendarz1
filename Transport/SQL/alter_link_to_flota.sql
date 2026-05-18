-- ═══════════════════════════════════════════════════════════════════════════
-- Faza 6-A — Link TransportPL.Kierowca/Pojazd → LibraNet.Driver/CarTrailer (Flota)
-- ═══════════════════════════════════════════════════════════════════════════
-- Cel: zlikwidować tri-silos (Transport vs Flota vs MapaFloty) — wprowadzić
--      "soft FK" z TransportPL do LibraNet master tables. SQL Server nie wspiera
--      cross-DB FK constraints, więc to są tylko kolumny + indeksy. Spójność
--      utrzymywana przez aplikację (FlotaTransportBridgeService).
--
-- Mapowanie:
--   TransportPL.Kierowca.LibraNetDriverGID   → LibraNet.Driver.GID         (int)
--   TransportPL.Pojazd.LibraNetCarTrailerID  → LibraNet.CarTrailer.ID      (varchar(10))
--
-- Uruchom w SSMS na bazie TransportPL (192.168.0.109).
-- Skrypt jest IDEMPOTENTNY — można uruchamiać wielokrotnie bezpiecznie.
-- ═══════════════════════════════════════════════════════════════════════════

USE TransportPL;
GO

-- ────────────────────────────────────────────────────────────
-- KROK 1: Kolumna Kierowca.LibraNetDriverGID
-- ────────────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Kierowca') AND name = 'LibraNetDriverGID'
)
BEGIN
    ALTER TABLE dbo.Kierowca ADD LibraNetDriverGID int NULL;
    PRINT 'Dodano Kierowca.LibraNetDriverGID (int NULL).';
END
ELSE
BEGIN
    PRINT 'Kierowca.LibraNetDriverGID juz istnieje — pomijam.';
END
GO

-- ────────────────────────────────────────────────────────────
-- KROK 2: Kolumna Pojazd.LibraNetCarTrailerID
-- ────────────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Pojazd') AND name = 'LibraNetCarTrailerID'
)
BEGIN
    ALTER TABLE dbo.Pojazd ADD LibraNetCarTrailerID varchar(10) NULL;
    PRINT 'Dodano Pojazd.LibraNetCarTrailerID (varchar(10) NULL).';
END
ELSE
BEGIN
    PRINT 'Pojazd.LibraNetCarTrailerID juz istnieje — pomijam.';
END
GO

-- ────────────────────────────────────────────────────────────
-- KROK 3: Indeks na Kierowca.LibraNetDriverGID
-- ────────────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_Kierowca_LibraNetDriverGID'
      AND object_id = OBJECT_ID('dbo.Kierowca')
)
BEGIN
    CREATE INDEX IX_Kierowca_LibraNetDriverGID
        ON dbo.Kierowca(LibraNetDriverGID)
        WHERE LibraNetDriverGID IS NOT NULL;
    PRINT 'Dodano indeks IX_Kierowca_LibraNetDriverGID (filtered).';
END
GO

-- ────────────────────────────────────────────────────────────
-- KROK 4: Indeks na Pojazd.LibraNetCarTrailerID
-- ────────────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_Pojazd_LibraNetCarTrailerID'
      AND object_id = OBJECT_ID('dbo.Pojazd')
)
BEGIN
    CREATE INDEX IX_Pojazd_LibraNetCarTrailerID
        ON dbo.Pojazd(LibraNetCarTrailerID)
        WHERE LibraNetCarTrailerID IS NOT NULL;
    PRINT 'Dodano indeks IX_Pojazd_LibraNetCarTrailerID (filtered).';
END
GO

-- ────────────────────────────────────────────────────────────
-- KROK 5: Weryfikacja
-- ────────────────────────────────────────────────────────────
PRINT '';
PRINT '═══ Weryfikacja ═══';

SELECT
    'Kierowca' AS Tabela,
    COUNT(*) AS Wszystkich,
    SUM(CASE WHEN LibraNetDriverGID IS NULL THEN 1 ELSE 0 END) AS Niezmapowanych,
    SUM(CASE WHEN LibraNetDriverGID IS NOT NULL THEN 1 ELSE 0 END) AS Zmapowanych
FROM dbo.Kierowca
UNION ALL
SELECT
    'Pojazd',
    COUNT(*),
    SUM(CASE WHEN LibraNetCarTrailerID IS NULL THEN 1 ELSE 0 END),
    SUM(CASE WHEN LibraNetCarTrailerID IS NOT NULL THEN 1 ELSE 0 END)
FROM dbo.Pojazd;

PRINT '';
PRINT 'Po uruchomieniu SQL: otworz aplikacje > Flota > "Mapowanie systemow" zeby zlinkowac rekordy.';
GO

-- ────────────────────────────────────────────────────────────
-- ROLLBACK (w razie potrzeby — kasuje kolumny i indeksy)
-- ────────────────────────────────────────────────────────────
-- DROP INDEX IX_Kierowca_LibraNetDriverGID ON dbo.Kierowca;
-- DROP INDEX IX_Pojazd_LibraNetCarTrailerID ON dbo.Pojazd;
-- ALTER TABLE dbo.Kierowca DROP COLUMN LibraNetDriverGID;
-- ALTER TABLE dbo.Pojazd DROP COLUMN LibraNetCarTrailerID;
