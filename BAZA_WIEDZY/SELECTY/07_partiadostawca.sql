-- ============================================================
-- 07 — PartiaDostawca (hodowcy + dekoder partii)
-- ============================================================
USE LibraNet;
GO

-- A) Struktura
SELECT
    ORDINAL_POSITION,
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'PartiaDostawca'
ORDER BY ORDINAL_POSITION;
GO

-- B) 10 najnowszych partii
SELECT TOP 10 *
FROM dbo.PartiaDostawca
ORDER BY CreateData DESC;
GO

-- C) Top 30 hodowcow po liczbie partii w ostatnich 90 dniach
SELECT TOP 30
    pd.CustomerID,
    pd.CustomerName,
    COUNT(*) AS liczba_partii,
    MIN(pd.CreateData) AS pierwsza_dostawa,
    MAX(pd.CreateData) AS ostatnia_dostawa
FROM dbo.PartiaDostawca pd
WHERE pd.CreateData >= CONVERT(varchar(10), DATEADD(DAY, -90, GETDATE()), 120)
GROUP BY pd.CustomerID, pd.CustomerName
ORDER BY liczba_partii DESC;
GO

-- D) Czy ten sam CustomerName ma wiele CustomerID (duplikaty hodowcow)
SELECT
    CustomerName,
    COUNT(DISTINCT CustomerID) AS liczba_id,
    STUFF((
        SELECT ', ' + CustomerID
        FROM dbo.PartiaDostawca pd2
        WHERE pd2.CustomerName = pd.CustomerName
        FOR XML PATH('')
    ), 1, 2, '') AS lista_id
FROM dbo.PartiaDostawca pd
GROUP BY CustomerName
HAVING COUNT(DISTINCT CustomerID) > 1
ORDER BY liczba_id DESC;
GO

-- E) Liczba unikalnych hodowcow
SELECT
    COUNT(DISTINCT CustomerID) AS unikalne_id,
    COUNT(DISTINCT CustomerName) AS unikalne_nazwy,
    COUNT(*) AS partii_lacznie
FROM dbo.PartiaDostawca;
GO

-- F) Test dekodera partii (dla 5 najnowszych)
SELECT TOP 5
    pd.CustomerID,
    pd.Partia,
    pd.CustomerName,
    pd.CreateData,
    LEFT(pd.Partia, 2) AS rok_z_partii,
    SUBSTRING(pd.Partia, 3, 3) AS dzien_z_partii,
    SUBSTRING(pd.Partia, 6, 3) AS auto_z_partii,
    DATEADD(DAY, CAST(SUBSTRING(pd.Partia, 3, 3) AS INT) - 1,
            DATEFROMPARTS(2000 + CAST(LEFT(pd.Partia, 2) AS INT), 1, 1))
                AS data_z_partii_obliczona
FROM dbo.PartiaDostawca pd
WHERE pd.Partia IS NOT NULL
  AND LEN(pd.Partia) = 8
ORDER BY pd.CreateData DESC;
GO

-- G) Czy data z partii zgadza sie z CreateData (sanity check)
SELECT
    SUM(CASE WHEN
        DATEADD(DAY, CAST(SUBSTRING(pd.Partia, 3, 3) AS INT) - 1,
                DATEFROMPARTS(2000 + CAST(LEFT(pd.Partia, 2) AS INT), 1, 1))
        = CONVERT(date, pd.CreateData)
    THEN 1 ELSE 0 END) AS zgadza_sie,
    COUNT(*) AS razem
FROM dbo.PartiaDostawca pd
WHERE pd.Partia IS NOT NULL
  AND LEN(pd.Partia) = 8
  AND pd.CreateData IS NOT NULL;
GO
