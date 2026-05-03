-- ============================================================
-- 06 — Article (slownik towarow)
-- ============================================================
USE LibraNet;
GO

-- A) Struktura kolumn (sprawdz czy ma MinWeight/MaxWeight/Tolerance)
SELECT
    ORDINAL_POSITION,
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Article'
ORDER BY ORDINAL_POSITION;
GO

-- B) Pelne wiersze dla Kurczaka A (ID='40')
SELECT TOP 5 *
FROM dbo.Article
WHERE ID = '40';
GO

-- C) Top 30 najczesciej wazonych towarow
SELECT TOP 30
    a.ID,
    a.Name,
    a.ShortName,
    COUNT(*) AS liczba_wazen,
    SUM(CASE WHEN e.ActWeight > 0 THEN e.ActWeight ELSE 0 END) AS suma_kg
FROM dbo.Article a
JOIN dbo.In0E e ON e.ArticleID = a.ID
WHERE e.Data >= CONVERT(varchar(10), DATEADD(DAY, -30, GETDATE()), 120)
GROUP BY a.ID, a.Name, a.ShortName
ORDER BY liczba_wazen DESC;
GO

-- D) Wszystkie towary (lista) - max 100
SELECT TOP 100
    ID,
    Name,
    ShortName
FROM dbo.Article
WHERE ID IS NOT NULL AND ID <> ''
ORDER BY Name;
GO

-- E) Liczba wszystkich towarow
SELECT
    COUNT(*) AS razem,
    COUNT(DISTINCT ID) AS unikalne_id,
    SUM(CASE WHEN Name IS NULL OR Name = '' THEN 1 ELSE 0 END) AS bez_nazwy
FROM dbo.Article;
GO

-- F) Szukanie kolumn tolerancji w jakichkolwiek tabelach
SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE COLUMN_NAME LIKE '%Toler%'
   OR COLUMN_NAME LIKE '%MinW%'
   OR COLUMN_NAME LIKE '%MaxW%'
   OR COLUMN_NAME LIKE '%WeightStandard%'
   OR COLUMN_NAME LIKE '%StdWeight%'
ORDER BY TABLE_NAME, COLUMN_NAME;
GO
