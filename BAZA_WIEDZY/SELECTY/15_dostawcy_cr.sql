-- ============================================================
-- 15 — Dostawcy + DostawcyCR (workflow akceptacji)
-- ============================================================
USE LibraNet;
GO

-- A) Struktury
SELECT
    TABLE_NAME, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('Dostawcy', 'DOSTAWCY', 'DostawcyCR', 'DostawcyCRItem',
                     'RozliczeniaZatwierdzenia')
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

-- B) Liczba rekordow
SELECT 'Dostawcy' AS tabela, COUNT(*) AS rekordow FROM dbo.Dostawcy
UNION ALL SELECT 'DostawcyCR', COUNT(*) FROM dbo.DostawcyCR
UNION ALL SELECT 'DostawcyCRItem', COUNT(*) FROM dbo.DostawcyCRItem
UNION ALL SELECT 'RozliczeniaZatwierdzenia', COUNT(*) FROM dbo.RozliczeniaZatwierdzenia;
GO

-- C) Sample 5 z DostawcyCR (niezakonczone)
SELECT TOP 10 *
FROM dbo.DostawcyCR
WHERE Status = 'Proposed'
ORDER BY 1 DESC;
GO

-- D) Sample 5 z DostawcyCRItem
SELECT TOP 10 *
FROM dbo.DostawcyCRItem
ORDER BY 1 DESC;
GO

-- E) Rozklad statusow
SELECT
    Status,
    COUNT(*) AS liczba
FROM dbo.DostawcyCR
GROUP BY Status
ORDER BY liczba DESC;
GO
