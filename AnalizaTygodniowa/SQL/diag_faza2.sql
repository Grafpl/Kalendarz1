-- ════════════════════════════════════════════════════════════════════
-- FAZA 2 — Diagnostyka celowana
-- Dwie sesje SSMS: jedna dla 112 (HANDEL), druga dla 109 (LibraNet)
-- ════════════════════════════════════════════════════════════════════

-- ════════════════════════════════════════════════════════════════════
-- ┌─ HANDEL (192.168.0.112) ────────────────────────────────────────┐
-- ════════════════════════════════════════════════════════════════════
USE HANDEL;
SET NOCOUNT ON;
GO

PRINT '═══ H1. MARŻA — czy DP.koszt jest wypełniony dla świeżych (kat. 67095) ═══';
SELECT TOP 30
    DP.id, DP.data, TW.kod, TW.nazwa,
    DP.ilosc, DP.cena,
    DP.koszt, DP.kosztAproksymowany, DP.kosztmarzy, DP.wartTowaru,
    DP.cena * DP.ilosc          AS Wart_sprz,
    DP.koszt * DP.ilosc         AS Koszt_calk,
    (DP.cena - DP.koszt) * DP.ilosc AS Marza_zl,
    CASE WHEN DP.cena > 0 THEN ROUND((DP.cena - DP.koszt) / DP.cena * 100, 1) END AS Marza_proc
FROM HM.DP DP
JOIN HM.TW TW ON TW.id = DP.idtw
WHERE TW.katalog = 67095
  AND DP.data >= DATEADD(MONTH, -1, GETDATE())
  AND DP.ilosc > 0
ORDER BY DP.data DESC;
GO

PRINT '═══ H2. ProductionLineID — ile dokumentów ma wypełnioną linię ═══';
SELECT
    COUNT(*) AS LiczbaWierszy,
    COUNT(ProductionLineID) AS ZWypelnionaLinia,
    COUNT(DISTINCT ProductionLineID) AS UnikalnychLinii
FROM HM.MZ
WHERE data >= DATEADD(MONTH, -3, GETDATE());
GO

PRINT '═══ H3. ProductionLineID — distinct wartości + wolumen kg ═══';
SELECT
    ProductionLineID,
    COUNT(*) AS Wierszy,
    SUM(przychod) AS PrzychodKg,
    SUM(rozchod) AS RozchodKg,
    MIN(data) AS Od, MAX(data) AS Do
FROM HM.MZ
WHERE data >= DATEADD(MONTH, -3, GETDATE())
  AND ProductionLineID IS NOT NULL
GROUP BY ProductionLineID
ORDER BY Wierszy DESC;
GO

PRINT '═══ H4. ProductionOrderID — czy MG ma zlecenia produkcyjne ═══';
SELECT
    COUNT(*) AS WszystkieMG,
    COUNT(ProductionOrderID) AS ZeZleceniem,
    COUNT(DISTINCT ProductionOrderID) AS UnikalnychZlecen,
    SUM(CAST(IsProductionTrash AS INT)) AS OznaczoneJakoTrash
FROM HM.MG
WHERE data >= DATEADD(MONTH, -3, GETDATE());
GO

PRINT '═══ H5. Tabele związane z produkcją (Production*, Line*) ═══';
SELECT s.name + '.' + t.name AS Tabela, p.[rows] AS Wierszy
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
LEFT JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1)
WHERE t.name LIKE '%Production%' OR t.name LIKE 'Line%'
ORDER BY Tabela;
GO

PRINT '═══ H6. Mapa magazynów — co kryje się pod kodami numerycznymi ═══';
SELECT TOP 15
    MG.magazyn,
    COUNT(*) AS Dok,
    STRING_AGG(CAST(MG.seria AS NVARCHAR(MAX)), ',') WITHIN GROUP (ORDER BY MG.seria) AS Serie
FROM (
    SELECT DISTINCT magazyn, seria FROM HM.MG WHERE data >= DATEADD(MONTH, -3, GETDATE())
) MG
GROUP BY MG.magazyn
ORDER BY Dok DESC;
GO

-- ════════════════════════════════════════════════════════════════════
-- └─ KONIEC HANDEL ─────────────────────────────────────────────────┘
-- ════════════════════════════════════════════════════════════════════


-- ════════════════════════════════════════════════════════════════════
-- ┌─ LibraNet (192.168.0.109) ───────────────────────────────────────┐
-- ════════════════════════════════════════════════════════════════════
USE LibraNet;
SET NOCOUNT ON;
GO

PRINT '═══ L1. ⭐ ROZKŁAD GODZINOWY ważeń przyjęć (czy 2 zmiany?) ═══';
-- W In0E Data jest VARCHAR(10), Godzina VARCHAR(8). LEFT(Godzina, 2) = godzina.
SELECT
    LEFT(Godzina, 2) AS Godz,
    COUNT(*) AS Wazen,
    SUM(Weight) AS SumaKg,
    AVG(Weight) AS AvgKg
FROM dbo.In0E
WHERE TermType = 'K2'
  AND Direction = '1A'
  AND TRY_CONVERT(date, Data, 120) >= DATEADD(DAY, -30, GETDATE())
  AND Godzina IS NOT NULL
GROUP BY LEFT(Godzina, 2)
ORDER BY Godz;
GO

PRINT '═══ L2. ZamowieniaMieso — kolumny + 5 najnowszych ═══';
SELECT name, TYPE_NAME(user_type_id) AS Typ, max_length
FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ZamowieniaMieso') ORDER BY column_id;
GO
SELECT TOP 5 * FROM dbo.ZamowieniaMieso ORDER BY 1 DESC;
GO

PRINT '═══ L3. ZamowieniaMiesoTowar — kolumny + 5 najnowszych ═══';
SELECT name, TYPE_NAME(user_type_id) AS Typ, max_length
FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ZamowieniaMiesoTowar') ORDER BY column_id;
GO
SELECT TOP 5 * FROM dbo.ZamowieniaMiesoTowar ORDER BY 1 DESC;
GO

PRINT '═══ L4. ⭐ INTEL — ceny rynkowe (3 tabele) ═══';
SELECT name, TYPE_NAME(user_type_id) AS Typ
FROM sys.columns WHERE object_id = OBJECT_ID('dbo.intel_Prices') ORDER BY column_id;
GO
SELECT TOP 5 * FROM dbo.intel_Prices ORDER BY 1 DESC;
GO
SELECT TOP 5 * FROM dbo.intel_FeedPrices ORDER BY 1 DESC;
GO
SELECT TOP 5 * FROM dbo.intel_EuBenchmark ORDER BY 1 DESC;
GO

PRINT '═══ L5. ⭐ CenaTuszki — referencyjna cena tuszki (rynkowa) ═══';
SELECT name, TYPE_NAME(user_type_id) AS Typ
FROM sys.columns WHERE object_id = OBJECT_ID('dbo.CenaTuszki') ORDER BY column_id;
GO
SELECT TOP 10 * FROM dbo.CenaTuszki ORDER BY 1 DESC;
GO

PRINT '═══ L6. CenaRolnicza i CenaMinisterialna — 5 wierszy ═══';
SELECT TOP 5 * FROM dbo.CenaRolnicza ORDER BY 1 DESC;
GO
SELECT TOP 5 * FROM dbo.CenaMinisterialna ORDER BY 1 DESC;
GO

PRINT '═══ L7. HPAI — alerty + ogniska grypy ptaków ═══';
SELECT TOP 5 * FROM dbo.intel_HpaiOutbreaks ORDER BY 1 DESC;
GO

PRINT '═══ L8. OdbiorcyCRM (20k!) — 3 wiersze + kolumny ═══';
SELECT name, TYPE_NAME(user_type_id) AS Typ, max_length
FROM sys.columns WHERE object_id = OBJECT_ID('dbo.OdbiorcyCRM') ORDER BY column_id;
GO
SELECT TOP 3 * FROM dbo.OdbiorcyCRM;
GO

PRINT '═══ L9. State0E — stan magazynowy (live?) — 5 wierszy ═══';
SELECT name, TYPE_NAME(user_type_id) AS Typ
FROM sys.columns WHERE object_id = OBJECT_ID('dbo.State0E') ORDER BY column_id;
GO
SELECT TOP 5 * FROM dbo.State0E;
GO

PRINT '═══ L10. Reklamacje — kolumny + 3 ostatnie ═══';
SELECT name, TYPE_NAME(user_type_id) AS Typ
FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Reklamacje') ORDER BY column_id;
GO
SELECT TOP 3 * FROM dbo.Reklamacje ORDER BY 1 DESC;
GO

PRINT '═══ KONIEC LibraNet ═══';
