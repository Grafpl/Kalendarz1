-- ============================================================
-- 01 — Lista wszystkich tabel + liczba wierszy + rozmiar w MB
-- ============================================================
USE LibraNet;
GO

-- A) Top tabel po liczbie wierszy
SELECT TOP 100
    s.name + '.' + t.name           AS TableFullName,
    p.rows                          AS RowCount_,
    SUM(a.total_pages) * 8 / 1024.0 AS TotalMB,
    SUM(a.used_pages)  * 8 / 1024.0 AS UsedMB
FROM sys.tables t
JOIN sys.schemas s          ON s.schema_id = t.schema_id
JOIN sys.partitions p       ON p.object_id = t.object_id AND p.index_id IN (0,1)
JOIN sys.allocation_units a ON a.container_id = p.partition_id
GROUP BY s.name, t.name, p.rows
ORDER BY p.rows DESC;
GO

-- B) Liczba wszystkich tabel + ilość kolumn
SELECT
    COUNT(DISTINCT t.object_id) AS LiczbaTabel,
    COUNT(c.column_id)          AS LiczbaKolumnLacznie
FROM sys.tables t
JOIN sys.columns c ON c.object_id = t.object_id;
GO

-- C) Tabele najnowsze (kiedy ostatnio zmodyfikowane)
SELECT TOP 30
    s.name + '.' + t.name AS TableFullName,
    t.create_date,
    t.modify_date
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
ORDER BY t.modify_date DESC;
GO
