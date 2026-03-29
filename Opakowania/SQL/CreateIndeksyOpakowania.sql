-- =====================================================================
-- INDEKSY DLA MODULU OPAKOWAN
-- Uruchom RAZ w SSMS na serwerze 192.168.0.112 (baza Handel)
--
-- Obecny problem:
--   MG: 621,588 logical reads (pelny skan tabeli 224K rekordow)
--   MZ: 311,463 logical reads (pelny skan tabeli 893K rekordow)
--   Czas zapytania: ~2.2s
--
-- Po indeksach spodziewany czas: ~200-500ms
-- =====================================================================

-- Sprawdz czy indeks juz istnieje
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MG_Opakowania' AND object_id = OBJECT_ID('[HM].[MG]'))
BEGIN
    -- Indeks filtrowany na dokumenty opakowan
    -- Pokrywa: magazyn=65559, anulowany=0, typ_dk IN (MW1, MP)
    -- Sortuje po dacie (najczestszy filtr)
    -- INCLUDE: khid (kontrahent), kod, seria (uzywane w SELECT)
    CREATE NONCLUSTERED INDEX IX_MG_Opakowania
    ON [HM].[MG] (data, khid)
    INCLUDE (kod, seria, opis, typ_dk)
    WHERE magazyn = 65559 AND anulowany = 0 AND typ_dk IN ('MW1', 'MP');

    PRINT 'Utworzono IX_MG_Opakowania';
END
ELSE
    PRINT 'IX_MG_Opakowania juz istnieje';
GO

-- Indeks na MZ.super + idtw (JOIN MZ→MG i MZ→TW)
-- Ten indeks istnieje (IDX_MZ_SUPER) ale nie ma INCLUDE
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MZ_Opakowania' AND object_id = OBJECT_ID('[HM].[MZ]'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_MZ_Opakowania
    ON [HM].[MZ] (super, idtw)
    INCLUDE (Ilosc, data);

    PRINT 'Utworzono IX_MZ_Opakowania';
END
ELSE
    PRINT 'IX_MZ_Opakowania juz istnieje';
GO

-- Sprawdz rozmiar indeksow
SELECT
    i.name AS IndexName,
    CAST(SUM(s.used_page_count) * 8.0 / 1024 AS DECIMAL(10,2)) AS SizeMB,
    SUM(s.row_count) AS Rows
FROM sys.dm_db_partition_stats s
INNER JOIN sys.indexes i ON s.object_id = i.object_id AND s.index_id = i.index_id
WHERE i.name IN ('IX_MG_Opakowania', 'IX_MZ_Opakowania')
GROUP BY i.name;
GO
