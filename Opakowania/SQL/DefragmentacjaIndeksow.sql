-- =====================================================================
-- DEFRAGMENTACJA INDEKSOW — bezpieczne dla Symfonia Handel
-- Uruchom w SSMS na 192.168.0.112 (baza Handel)
--
-- ALTER INDEX REBUILD to standardowa operacja konserwacyjna.
-- NIE zmienia struktury tabeli, NIE dodaje nowych indeksow.
-- Tylko reorganizuje istniejace dane na dysku.
-- Mozna uruchomic w godzinach pracy (ONLINE = ON).
--
-- PRZED:
--   IDX_MG_KARTEX:  99.3% fragmentacji (25K skanow!)
--   PK_MG_ID:       88.4% fragmentacji
--   IDX_MG_KHID:    75.9% fragmentacji
--
-- SPODZIEWANY EFEKT:
--   Zapytania opakowan: z ~2.2s na ~0.8-1.2s
--   Wszystkie zapytania na MG szybsze
-- =====================================================================

-- Sprawdz aktualny stan PRZED
SELECT
    i.name AS Indeks,
    CAST(ps.avg_fragmentation_in_percent AS DECIMAL(5,1)) AS [Fragmentacja %],
    ps.page_count AS Stron
FROM sys.dm_db_index_physical_stats(DB_ID(), OBJECT_ID('[HM].[MG]'), NULL, NULL, 'LIMITED') ps
INNER JOIN sys.indexes i ON ps.object_id = i.object_id AND ps.index_id = i.index_id
WHERE ps.page_count > 100
ORDER BY ps.avg_fragmentation_in_percent DESC;
GO

-- =====================================================================
-- REBUILD najwazniejszych indeksow (od najbardziej sfragmentowanych)
-- ONLINE = ON pozwala innym uzytkownikom pracowac w trakcie
-- =====================================================================

PRINT 'Rebuilding PK_MG_ID (clustered, 88%)...';
ALTER INDEX PK_MG_ID ON [HM].[MG] REBUILD WITH (ONLINE = ON);
PRINT 'OK';
GO

PRINT 'Rebuilding IDX_MG_KARTEX (99%)...';
ALTER INDEX IDX_MG_KARTEX ON [HM].[MG] REBUILD WITH (ONLINE = ON);
PRINT 'OK';
GO

PRINT 'Rebuilding IDX_MG_TYP_KOD_BUFOR (98%)...';
ALTER INDEX IDX_MG_TYP_KOD_BUFOR ON [HM].[MG] REBUILD WITH (ONLINE = ON);
PRINT 'OK';
GO

PRINT 'Rebuilding IDX_MG_KHID (76%)...';
ALTER INDEX IDX_MG_KHID ON [HM].[MG] REBUILD WITH (ONLINE = ON);
PRINT 'OK';
GO

PRINT 'Rebuilding IDX_MG_KHADID (77%)...';
ALTER INDEX IDX_MG_KHADID ON [HM].[MG] REBUILD WITH (ONLINE = ON);
PRINT 'OK';
GO

PRINT 'Rebuilding IDX_MG_MAGAZYN (55%)...';
ALTER INDEX IDX_MG_MAGAZYN ON [HM].[MG] REBUILD WITH (ONLINE = ON);
PRINT 'OK';
GO

PRINT 'Rebuilding IDX_MG_SERIA (54%)...';
ALTER INDEX IDX_MG_SERIA ON [HM].[MG] REBUILD WITH (ONLINE = ON);
PRINT 'OK';
GO

PRINT 'Rebuilding IDX_MG_DATASP (63%)...';
ALTER INDEX IDX_MG_DATASP ON [HM].[MG] REBUILD WITH (ONLINE = ON);
PRINT 'OK';
GO

-- MZ tez moze byc sfragmentowane
PRINT 'Rebuilding PK_MZ_ID...';
ALTER INDEX PK_MZ_ID ON [HM].[MZ] REBUILD WITH (ONLINE = ON);
PRINT 'OK';
GO

PRINT 'Rebuilding IDX_MZ_SUPER...';
ALTER INDEX IDX_MZ_SUPER ON [HM].[MZ] REBUILD WITH (ONLINE = ON);
PRINT 'OK';
GO

-- =====================================================================
-- Sprawdz stan PO
-- =====================================================================

PRINT '';
PRINT '=== STAN PO REBUILIDZIE ===';

SELECT
    i.name AS Indeks,
    CAST(ps.avg_fragmentation_in_percent AS DECIMAL(5,1)) AS [Fragmentacja %],
    ps.page_count AS Stron
FROM sys.dm_db_index_physical_stats(DB_ID(), OBJECT_ID('[HM].[MG]'), NULL, NULL, 'LIMITED') ps
INNER JOIN sys.indexes i ON ps.object_id = i.object_id AND ps.index_id = i.index_id
WHERE ps.page_count > 100
ORDER BY ps.avg_fragmentation_in_percent DESC;
GO

-- =====================================================================
-- Aktualizuj statystyki (po rebuildzie)
-- =====================================================================

UPDATE STATISTICS [HM].[MG] WITH FULLSCAN;
UPDATE STATISTICS [HM].[MZ] WITH FULLSCAN;
PRINT 'Statystyki zaktualizowane';
GO
