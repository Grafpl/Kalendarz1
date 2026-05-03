-- ============================================================
-- 04 — listapartii (master partii ubojowych)
-- ============================================================
USE LibraNet;
GO

-- A) Struktura kolumn
SELECT
    ORDINAL_POSITION,
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    NUMERIC_PRECISION,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'listapartii'
ORDER BY ORDINAL_POSITION;
GO

-- B) 10 najnowszych rekordów (wszystkie kolumny)
SELECT TOP 10 *
FROM dbo.listapartii
ORDER BY CreateData DESC;
GO

-- C) Rozkład statusów V2
SELECT
    StatusV2,
    COUNT(*) AS liczba
FROM dbo.listapartii
WHERE StatusV2 IS NOT NULL
GROUP BY StatusV2
ORDER BY liczba DESC;
GO

-- D) Rozkład działów
SELECT
    DirID,
    COUNT(*) AS liczba
FROM dbo.listapartii
WHERE DirID IS NOT NULL
GROUP BY DirID
ORDER BY liczba DESC;
GO

-- E) Partie dziennie ostatnie 30 dni
SELECT TOP 30
    CreateData,
    COUNT(*) AS liczba_partii,
    SUM(CASE WHEN IsClose = 1 THEN 1 ELSE 0 END) AS zamkniete,
    SUM(CASE WHEN ISNULL(IsClose,0) = 0 THEN 1 ELSE 0 END) AS otwarte
FROM dbo.listapartii
WHERE CreateData >= CONVERT(varchar(10), DATEADD(DAY, -30, GETDATE()), 120)
GROUP BY CreateData
ORDER BY CreateData DESC;
GO

-- F) Liczba partii per rok
SELECT
    LEFT(CONVERT(varchar(10), CreateData, 120), 4) AS rok,
    COUNT(*) AS liczba_partii
FROM dbo.listapartii
WHERE CreateData IS NOT NULL
GROUP BY LEFT(CONVERT(varchar(10), CreateData, 120), 4)
ORDER BY rok DESC;
GO

-- G) Min i max CreateData (zakres czasowy danych)
SELECT
    MIN(CreateData) AS najstarsza,
    MAX(CreateData) AS najnowsza,
    COUNT(*) AS razem
FROM dbo.listapartii;
GO
