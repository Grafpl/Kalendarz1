-- ============================================================
-- 05 — In0E (rdzen wazen produkcji)
-- ============================================================
USE LibraNet;
GO

-- A) Struktura
SELECT
    ORDINAL_POSITION,
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    NUMERIC_PRECISION,
    NUMERIC_SCALE,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'In0E'
ORDER BY ORDINAL_POSITION;
GO

-- B) 10 najnowszych wazen
SELECT TOP 10 *
FROM dbo.In0E
ORDER BY Data DESC, Godzina DESC;
GO

-- C) Rozklad klas wagowych ostatnie 30 dni (tylko Kurczak A)
SELECT
    QntInCont AS klasa,
    COUNT(*) AS liczba_wazen,
    SUM(CASE WHEN ActWeight > 0 THEN ActWeight ELSE 0 END) AS suma_kg
FROM dbo.In0E
WHERE Data >= CONVERT(varchar(10), DATEADD(DAY, -30, GETDATE()), 120)
  AND ArticleID = '40'
GROUP BY QntInCont
ORDER BY klasa;
GO

-- D) Rozklad TermID (terminale wagowe) ostatnie 30 dni
SELECT
    TermID,
    TermType,
    COUNT(*) AS liczba_wazen,
    MIN(Data) AS pierwsza,
    MAX(Data) AS ostatnia
FROM dbo.In0E
WHERE Data >= CONVERT(varchar(10), DATEADD(DAY, -30, GETDATE()), 120)
GROUP BY TermID, TermType
ORDER BY liczba_wazen DESC;
GO

-- E) Rozklad Direction
SELECT
    Direction,
    COUNT(*) AS liczba
FROM dbo.In0E
WHERE Data >= CONVERT(varchar(10), DATEADD(DAY, -90, GETDATE()), 120)
GROUP BY Direction;
GO

-- F) Aktywni operatorzy ostatnie 30 dni
SELECT
    OperatorID,
    MIN(Wagowy) AS imie,
    COUNT(*) AS liczba_wazen,
    SUM(CASE WHEN ActWeight > 0 THEN 1 ELSE 0 END) AS prawidlowe,
    SUM(CASE WHEN ActWeight < 0 THEN 1 ELSE 0 END) AS storno,
    SUM(CASE WHEN ArticleID = '40' THEN 1 ELSE 0 END) AS palety_kurczaka_A,
    SUM(CASE WHEN ArticleID <> '40' THEN 1 ELSE 0 END) AS porcje_inne
FROM dbo.In0E
WHERE Data >= CONVERT(varchar(10), DATEADD(DAY, -30, GETDATE()), 120)
  AND OperatorID IS NOT NULL
GROUP BY OperatorID
ORDER BY liczba_wazen DESC;
GO

-- G) Histogram godzinowy wazen
SELECT
    LEFT(Godzina, 2) AS godzina,
    COUNT(*) AS liczba_wazen
FROM dbo.In0E
WHERE Data >= CONVERT(varchar(10), DATEADD(DAY, -30, GETDATE()), 120)
  AND ActWeight > 0
GROUP BY LEFT(Godzina, 2)
ORDER BY godzina;
GO

-- H) Empiryczna tolerancja wagowa per towar (top 30)
SELECT TOP 30
    ArticleID,
    ArticleName,
    AVG(ABS(ActWeight - Weight)) AS sr_odchylenie_kg,
    AVG(ABS(ActWeight - Weight) / NULLIF(Weight, 0) * 100) AS sr_odchylenie_proc,
    MAX(ABS(ActWeight - Weight)) AS max_odchylenie_kg,
    COUNT(*) AS liczba_wazen
FROM dbo.In0E
WHERE Data >= CONVERT(varchar(10), DATEADD(DAY, -30, GETDATE()), 120)
  AND ActWeight > 0 AND Weight > 0
GROUP BY ArticleID, ArticleName
HAVING COUNT(*) > 50
ORDER BY liczba_wazen DESC;
GO

-- I) Wazenia gdzie P1 jest puste (problem z partia)
SELECT
    COUNT(*) AS liczba_bez_partii,
    MIN(Data) AS najstarsze,
    MAX(Data) AS najnowsze
FROM dbo.In0E
WHERE (P1 IS NULL OR P1 = '')
  AND Data >= CONVERT(varchar(10), DATEADD(DAY, -90, GETDATE()), 120);
GO

-- J) Czy P2 != P1 sie zdarza?
SELECT
    COUNT(*) AS liczba_gdzie_P2_rozne,
    SUM(CASE WHEN P1 = P2 THEN 1 ELSE 0 END) AS p2_rowne_p1,
    SUM(CASE WHEN P2 IS NULL OR P2 = '' THEN 1 ELSE 0 END) AS p2_puste
FROM dbo.In0E
WHERE Data >= CONVERT(varchar(10), DATEADD(DAY, -90, GETDATE()), 120)
  AND P1 IS NOT NULL AND P1 <> '';
GO

-- K) Min i max Data
SELECT
    MIN(Data) AS najstarsze_wazenie,
    MAX(Data) AS najnowsze_wazenie,
    COUNT(*) AS razem
FROM dbo.In0E;
GO
