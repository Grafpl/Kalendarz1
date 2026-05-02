-- ════════════════════════════════════════════════════════════════════
-- DIAGNOSTYKA: LibraNet (192.168.0.109)
-- Cel: poznać strukturę danych z wagi i partii
-- W SSMS: Ctrl+T (Results to Text), F5
-- ════════════════════════════════════════════════════════════════════
USE LibraNet;
SET NOCOUNT ON;
GO

-- ─────────────────────────────────────────────────────────────────
-- D. SCHEMATY (najpierw)
-- ─────────────────────────────────────────────────────────────────
PRINT '═══ D1. Wszystkie tabele w bazie LibraNet ═══';
SELECT t.name AS Tabela, p.[rows] AS LiczbaWierszy
FROM sys.tables t
LEFT JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1)
ORDER BY t.name;
GO

PRINT '═══ D2. Tabele związane ze zmianami / produkcją / RKZ / linią ═══';
SELECT name FROM sys.tables
WHERE name LIKE '%zmian%' OR name LIKE '%shift%' OR name LIKE '%RKZ%'
   OR name LIKE '%ubic%' OR name LIKE '%hala%' OR name LIKE '%linia%'
   OR name LIKE '%produkc%' OR name LIKE '%tasm%' OR name LIKE '%operator%'
   OR name LIKE '%terminal%'
ORDER BY name;
GO

PRINT '═══ D3. Kolumny In0E (surowe ważenia) ═══';
SELECT name, TYPE_NAME(user_type_id) AS Typ, max_length, is_nullable
FROM sys.columns WHERE object_id = OBJECT_ID('dbo.In0E') ORDER BY column_id;
GO

PRINT '═══ D4. Kolumny Out1A (?) ═══';
SELECT name, TYPE_NAME(user_type_id) AS Typ, max_length, is_nullable
FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Out1A') ORDER BY column_id;
GO

PRINT '═══ D5. Kolumny Article ═══';
SELECT name, TYPE_NAME(user_type_id) AS Typ, max_length
FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Article') ORDER BY column_id;
GO

PRINT '═══ D6. Kolumny listapartii ═══';
SELECT name, TYPE_NAME(user_type_id) AS Typ, max_length
FROM sys.columns WHERE object_id = OBJECT_ID('dbo.listapartii') ORDER BY column_id;
GO

PRINT '═══ D7. Kolumny PartiaDostawca ═══';
SELECT name, TYPE_NAME(user_type_id) AS Typ, max_length
FROM sys.columns WHERE object_id = OBJECT_ID('dbo.PartiaDostawca') ORDER BY column_id;
GO

PRINT '═══ D8. Kolumny HarmonogramDostaw ═══';
SELECT name, TYPE_NAME(user_type_id) AS Typ, max_length
FROM sys.columns WHERE object_id = OBJECT_ID('dbo.HarmonogramDostaw') ORDER BY column_id;
GO

PRINT '═══ D9. Kolumny PartiaStatus (jeśli istnieje) ═══';
IF OBJECT_ID('dbo.PartiaStatus') IS NOT NULL
    SELECT name, TYPE_NAME(user_type_id) AS Typ, max_length
    FROM sys.columns WHERE object_id = OBJECT_ID('dbo.PartiaStatus') ORDER BY column_id;
GO

PRINT '═══ D10. Kolumny QC_Normy (jeśli istnieje) ═══';
IF OBJECT_ID('dbo.QC_Normy') IS NOT NULL
    SELECT name, TYPE_NAME(user_type_id) AS Typ, max_length
    FROM sys.columns WHERE object_id = OBJECT_ID('dbo.QC_Normy') ORDER BY column_id;
GO

-- ─────────────────────────────────────────────────────────────────
-- A. PRÓBKI DANYCH
-- ─────────────────────────────────────────────────────────────────
PRINT '═══ A1. In0E — 10 najnowszych ═══';
SELECT TOP 10 * FROM dbo.In0E ORDER BY 1 DESC;
GO

PRINT '═══ A2. Out1A — 5 wierszy (jeśli istnieje) ═══';
IF OBJECT_ID('dbo.Out1A') IS NOT NULL
    SELECT TOP 5 * FROM dbo.Out1A ORDER BY 1 DESC;
ELSE PRINT 'Out1A nie istnieje';
GO

PRINT '═══ A3. Article — 5 wierszy ═══';
SELECT TOP 5 * FROM dbo.Article;
GO

PRINT '═══ A4. listapartii — 5 najnowszych ═══';
SELECT TOP 5 * FROM dbo.listapartii ORDER BY 1 DESC;
GO

PRINT '═══ A5. PartiaDostawca — 5 wierszy ═══';
SELECT TOP 5 * FROM dbo.PartiaDostawca;
GO

PRINT '═══ A6. HarmonogramDostaw — 5 najnowszych ═══';
SELECT TOP 5 * FROM dbo.HarmonogramDostaw ORDER BY 1 DESC;
GO

PRINT '═══ A7. PartiaStatus — 5 wierszy ═══';
IF OBJECT_ID('dbo.PartiaStatus') IS NOT NULL
    SELECT TOP 5 * FROM dbo.PartiaStatus ORDER BY 1 DESC;
GO

PRINT '═══ A8. PartiaAuditLog — 5 wierszy ═══';
IF OBJECT_ID('dbo.PartiaAuditLog') IS NOT NULL
    SELECT TOP 5 * FROM dbo.PartiaAuditLog ORDER BY 1 DESC;
GO

PRINT '═══ A9. QC_Normy — 10 wierszy ═══';
IF OBJECT_ID('dbo.QC_Normy') IS NOT NULL
    SELECT TOP 10 * FROM dbo.QC_Normy;
GO

-- ─────────────────────────────────────────────────────────────────
-- B. SŁOWNIKI / DISTINCT VALUES
-- ─────────────────────────────────────────────────────────────────
PRINT '═══ B1. In0E — unikalne terminale + wolumen 7 dni ═══';
SELECT TOP 30 terminal, COUNT(*) AS Wazen, SUM(waga) AS Kg, MIN(data) AS Od, MAX(data) AS Do
FROM dbo.In0E
WHERE data >= DATEADD(DAY, -7, GETDATE())
GROUP BY terminal
ORDER BY Wazen DESC;
GO

PRINT '═══ B2. In0E — top 30 operatorów (7 dni) ═══';
SELECT TOP 30 operator, COUNT(*) AS Wazen, SUM(waga) AS Kg
FROM dbo.In0E
WHERE data >= DATEADD(DAY, -7, GETDATE())
GROUP BY operator
ORDER BY Wazen DESC;
GO

PRINT '═══ B3. In0E — klasy (A / B / inne) ═══';
SELECT klasa, COUNT(*) AS Wazen, SUM(waga) AS Kg, AVG(CAST(waga AS DECIMAL(10,3))) AS AvgKg
FROM dbo.In0E
WHERE data >= DATEADD(DAY, -30, GETDATE())
GROUP BY klasa
ORDER BY Wazen DESC;
GO

PRINT '═══ B4. listapartii — wszystkie statusy ═══';
SELECT
    ISNULL(StatusV2, '(NULL)') AS StatusV2,
    COUNT(*) AS Liczba
FROM dbo.listapartii
GROUP BY StatusV2
ORDER BY Liczba DESC;
GO

PRINT '═══ B5. listapartii — top 20 dostawców ═══';
SELECT TOP 20
    LP.dostawca,
    COUNT(*) AS LiczbaPartii
FROM dbo.listapartii LP
WHERE LP.dostawca IS NOT NULL
GROUP BY LP.dostawca
ORDER BY LiczbaPartii DESC;
GO

PRINT '═══ B6. Article — grupy / rodzaje ═══';
SELECT TOP 30 grupa, COUNT(*) AS LiczbaArt
FROM dbo.Article
WHERE grupa IS NOT NULL
GROUP BY grupa
ORDER BY LiczbaArt DESC;
GO

-- ─────────────────────────────────────────────────────────────────
-- C. STATYSTYKI WOLUMENU + ROZKŁAD CZASOWY (klucz dla zmian!)
-- ─────────────────────────────────────────────────────────────────
PRINT '═══ C1. In0E — historia (zakres dat, liczba ważeń) ═══';
SELECT MIN(data) AS Najstarsza, MAX(data) AS Najnowsza, COUNT(*) AS Wazen,
       COUNT(DISTINCT terminal) AS Terminali,
       COUNT(DISTINCT operator) AS Operatorow
FROM dbo.In0E;
GO

PRINT '═══ C2. listapartii — historia ═══';
SELECT MIN(data) AS Najstarsza, MAX(data) AS Najnowsza, COUNT(*) AS Partie
FROM dbo.listapartii;
GO

PRINT '═══ C3. Wolumen In0E per dzień (ostatnie 14) — pierwsze/ostatnie ważenie dnia ═══';
-- Jeśli "data" zawiera czas — DATEPART(HOUR, data) zadziała
-- Jeśli jest osobna kolumna "godzina" — zamień
SELECT TOP 14
    CAST(data AS DATE) AS Dzien,
    COUNT(*) AS Wazen,
    SUM(waga) AS Kg,
    MIN(data) AS PierwszeWazenie,
    MAX(data) AS OstatnieWazenie
FROM dbo.In0E
WHERE data >= DATEADD(DAY, -14, GETDATE())
GROUP BY CAST(data AS DATE)
ORDER BY Dzien DESC;
GO

PRINT '═══ C4. ⭐ ROZKŁAD GODZINOWY ważeń (zobaczyć granice zmian!) ═══';
-- Pokaże wzór godzinowy. Jeśli widać 2 garby (np. 6-14 i 14-22) = 2 zmiany
-- Jeśli ciągle (5-22) = 1 długa zmiana
SELECT
    DATEPART(HOUR, data) AS Godzina,
    COUNT(*) AS Wazen,
    SUM(waga) AS SumaKg
FROM dbo.In0E
WHERE data >= DATEADD(DAY, -7, GETDATE())
GROUP BY DATEPART(HOUR, data)
ORDER BY Godzina;
GO

PRINT '═══ C5. Wolumen Out1A (wydania z linii?) per dzień ═══';
IF OBJECT_ID('dbo.Out1A') IS NOT NULL
BEGIN
    EXEC sp_executesql N'
    SELECT TOP 14
        CAST(data AS DATE) AS Dzien,
        COUNT(*) AS Wydan,
        SUM(waga) AS Kg
    FROM dbo.Out1A
    WHERE data >= DATEADD(DAY, -14, GETDATE())
    GROUP BY CAST(data AS DATE)
    ORDER BY Dzien DESC';
END
GO

PRINT '═══ C6. HarmonogramDostaw — co czeka w przyszłości ═══';
SELECT TOP 20 *
FROM dbo.HarmonogramDostaw
WHERE 1=1  -- użytkownik może dorzucić filtr daty jeśli HarmonogramDostaw ma pole "data"
ORDER BY 1 DESC;
GO

PRINT '═══ KONIEC LibraNet ═══';
