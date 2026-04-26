-- ============================================================================
-- Indeks dla okna "Dokumenty i Umowy" (SprawdzalkaUmow + UmowyForm)
-- ============================================================================
-- Cel: przyspieszyć główny SELECT z SprawdzalkaUmow.LoadDataGridKalendarz
--      oraz SELECT DISTINCT Lp z UmowyForm.
--
-- Główne zapytanie:
--   SELECT ... FROM dbo.HarmonogramDostaw h
--   LEFT JOIN dbo.operators u1 ON TRY_CAST(h.KtoUtw AS INT) = u1.ID
--   LEFT JOIN dbo.operators u2 ON TRY_CAST(h.KtoWysl AS INT) = u2.ID
--   LEFT JOIN dbo.operators u3 ON TRY_CAST(h.KtoOtrzym AS INT) = u3.ID
--   WHERE h.Bufor = 'Potwierdzony'
--     AND h.DataOdbioru BETWEEN @od AND @do
--   ORDER BY h.DataOdbioru DESC
--
-- Bez indeksu: SQL Server robi pełny skan tabeli HarmonogramDostaw (~10K-100K wierszy).
-- Z indeksem: index seek po (Bufor, DataOdbioru) + key lookup tylko dla pasujących wierszy.
-- Spodziewany efekt: zapytanie 5-50x szybsze (zwłaszcza przy archiwum 5+ lat).
-- ============================================================================

USE [LibraNet];
GO

-- Sprawdź czy indeks już istnieje (idempotentne)
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_HarmonogramDostaw_Bufor_DataOdbioru'
      AND object_id = OBJECT_ID('dbo.HarmonogramDostaw')
)
BEGIN
    PRINT 'Tworzenie indeksu IX_HarmonogramDostaw_Bufor_DataOdbioru...';

    CREATE NONCLUSTERED INDEX [IX_HarmonogramDostaw_Bufor_DataOdbioru]
    ON [dbo].[HarmonogramDostaw]
    (
        [Bufor],
        [DataOdbioru] DESC
    )
    INCLUDE
    (
        [Lp],
        [Dostawca],
        [Auta],
        [SztukiDek],
        [WagaDek],
        [SztSzuflada],
        [Utworzone],
        [Wysłane],
        [Otrzymane],
        [Posrednik],
        [KtoUtw],
        [KiedyUtw],
        [KtoWysl],
        [KiedyWysl],
        [KtoOtrzym],
        [KiedyOtrzm]
    )
    WITH (
        FILLFACTOR = 90,
        ONLINE = OFF,           -- jeśli masz Enterprise Edition: ON (bez blokady tabeli)
        DATA_COMPRESSION = PAGE
    );

    PRINT '✓ Indeks utworzony.';
END
ELSE
BEGIN
    PRINT '✓ Indeks już istnieje - pomijam.';
END
GO

-- ============================================================================
-- Drugi indeks: dla UmowyForm.SELECT DISTINCT Lp WHERE DataOdbioru >= -6 mies.
-- (Może wystarczyć powyższy + scan-and-distinct, ale ten jest węższy/szybszy.)
-- ============================================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_HarmonogramDostaw_DataOdbioru_Lp'
      AND object_id = OBJECT_ID('dbo.HarmonogramDostaw')
)
BEGIN
    PRINT 'Tworzenie indeksu IX_HarmonogramDostaw_DataOdbioru_Lp...';

    CREATE NONCLUSTERED INDEX [IX_HarmonogramDostaw_DataOdbioru_Lp]
    ON [dbo].[HarmonogramDostaw]
    (
        [DataOdbioru] DESC,
        [Lp]
    )
    WITH (
        FILLFACTOR = 90,
        ONLINE = OFF,
        DATA_COMPRESSION = PAGE
    );

    PRINT '✓ Indeks utworzony.';
END
ELSE
BEGIN
    PRINT '✓ Indeks już istnieje - pomijam.';
END
GO

-- ============================================================================
-- Statystyki + pomiar po założeniu indeksów
-- ============================================================================

UPDATE STATISTICS [dbo].[HarmonogramDostaw] WITH FULLSCAN;
GO

-- Test: czas głównego zapytania
SET STATISTICS TIME ON;
SET STATISTICS IO ON;

SELECT TOP 100
    h.[LP] AS ID, h.[DataOdbioru], h.[Dostawca],
    CAST(ISNULL(h.[Utworzone],0) AS bit) AS Utworzone,
    CAST(ISNULL(h.[Wysłane],0) AS bit) AS Wysłane,
    CAST(ISNULL(h.[Otrzymane],0) AS bit) AS Otrzymane,
    CAST(ISNULL(h.[Posrednik],0) AS bit) AS Posrednik
FROM [dbo].[HarmonogramDostaw] h
WHERE h.Bufor = 'Potwierdzony'
  AND h.DataOdbioru BETWEEN DATEADD(MONTH, -6, GETDATE()) AND DATEADD(DAY, 2, GETDATE())
ORDER BY h.DataOdbioru DESC;

SET STATISTICS TIME OFF;
SET STATISTICS IO OFF;

-- W output Messages powinno być coś typu:
-- Table 'HarmonogramDostaw'. Scan count 1, logical reads NN
-- (bez indeksu: NN = setki/tysiące, z indeksem: NN < 50)
-- SQL Server Execution Times: elapsed time = X ms

GO

-- ============================================================================
-- ROLLBACK (jeśli trzeba cofnąć)
-- ============================================================================
-- DROP INDEX [IX_HarmonogramDostaw_Bufor_DataOdbioru] ON [dbo].[HarmonogramDostaw];
-- DROP INDEX [IX_HarmonogramDostaw_DataOdbioru_Lp] ON [dbo].[HarmonogramDostaw];
