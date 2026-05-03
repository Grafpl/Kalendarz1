-- ============================================================
-- 16 — AvilogHodowcyMapping + WstawieniaKurczakow + v_WstawieniaDoKontaktu
-- ============================================================
USE LibraNet;
GO

-- A) Struktury
SELECT
    TABLE_NAME, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('AvilogHodowcyMapping', 'WstawieniaKurczakow',
                     'v_WstawieniaDoKontaktu')
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

-- B) Liczba rekordow
SELECT 'AvilogHodowcyMapping' AS tabela, COUNT(*) AS rekordow FROM dbo.AvilogHodowcyMapping
UNION ALL SELECT 'WstawieniaKurczakow', COUNT(*) FROM dbo.WstawieniaKurczakow;
GO

-- C) Sample 10 najnowszych mapowan
SELECT TOP 10 *
FROM dbo.AvilogHodowcyMapping
ORDER BY 1 DESC;
GO

-- D) Sample 10 najnowszych wstawien
SELECT TOP 10 *
FROM dbo.WstawieniaKurczakow
ORDER BY 1 DESC;
GO

-- E) v_WstawieniaDoKontaktu — sample 5 i definicja
SELECT TOP 5 *
FROM dbo.v_WstawieniaDoKontaktu;
GO

SELECT m.definition
FROM sys.views v
JOIN sys.sql_modules m ON m.object_id = v.object_id
WHERE v.name = 'v_WstawieniaDoKontaktu';
GO
