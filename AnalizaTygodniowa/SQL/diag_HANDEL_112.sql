-- ════════════════════════════════════════════════════════════════════
-- DIAGNOSTYKA: HANDEL (192.168.0.112)
-- Cel: poznać strukturę danych przed scaleniem okien analitycznych
-- W SSMS: Ctrl+T (Results to Text), F5
-- Każdy GO = osobny batch (błąd jednej sekcji nie blokuje następnych)
-- ════════════════════════════════════════════════════════════════════
USE HANDEL;
SET NOCOUNT ON;
GO

-- ─────────────────────────────────────────────────────────────────
-- D. SCHEMATY (najpierw — żebym wiedział jakie kolumny istnieją)
-- ─────────────────────────────────────────────────────────────────
PRINT '═══ D1. Wszystkie tabele w schemacie HM (lista) ═══';
SELECT name FROM sys.tables WHERE schema_id = SCHEMA_ID('HM') ORDER BY name;
GO

PRINT '═══ D2. Wszystkie tabele w SSCommon ═══';
SELECT name FROM sys.tables WHERE schema_id = SCHEMA_ID('SSCommon') ORDER BY name;
GO

PRINT '═══ D3. Kolumny HM.DK (faktury sprzedaży) ═══';
SELECT name, TYPE_NAME(user_type_id) AS Typ, max_length, is_nullable
FROM sys.columns WHERE object_id = OBJECT_ID('HM.DK') ORDER BY column_id;
GO

PRINT '═══ D4. Kolumny HM.DP (linie sprzedaży) — szukam kosztu/marży ═══';
SELECT name, TYPE_NAME(user_type_id) AS Typ, max_length, is_nullable
FROM sys.columns WHERE object_id = OBJECT_ID('HM.DP') ORDER BY column_id;
GO

PRINT '═══ D5. Kolumny HM.MZ (linie magazynowe) ═══';
SELECT name, TYPE_NAME(user_type_id) AS Typ, max_length, is_nullable
FROM sys.columns WHERE object_id = OBJECT_ID('HM.MZ') ORDER BY column_id;
GO

PRINT '═══ D6. Kolumny HM.MG (nagłówki magazynowe) ═══';
SELECT name, TYPE_NAME(user_type_id) AS Typ, max_length, is_nullable
FROM sys.columns WHERE object_id = OBJECT_ID('HM.MG') ORDER BY column_id;
GO

PRINT '═══ D7. Kolumny HM.TW (towary) ═══';
SELECT name, TYPE_NAME(user_type_id) AS Typ, max_length, is_nullable
FROM sys.columns WHERE object_id = OBJECT_ID('HM.TW') ORDER BY column_id;
GO

PRINT '═══ D8. Kolumny SSCommon.STContractors ═══';
SELECT name, TYPE_NAME(user_type_id) AS Typ, max_length
FROM sys.columns WHERE object_id = OBJECT_ID('SSCommon.STContractors') ORDER BY column_id;
GO

PRINT '═══ D9. Kolumny SSCommon.ContractorClassification (jakie wymiary klasyfikacji?) ═══';
SELECT name, TYPE_NAME(user_type_id) AS Typ, max_length
FROM sys.columns WHERE object_id = OBJECT_ID('SSCommon.ContractorClassification') ORDER BY column_id;
GO

PRINT '═══ D10. Czy są tabele zmianowe / RKZ / ubicia ═══';
SELECT s.name + '.' + t.name AS Tabela, p.[rows] AS LiczbaWierszy
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
LEFT JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1)
WHERE t.name LIKE '%zmian%' OR t.name LIKE '%shift%' OR t.name LIKE '%RKZ%'
   OR t.name LIKE '%ubic%' OR t.name LIKE '%hala%' OR t.name LIKE '%linia%'
   OR t.name LIKE '%produkc%' OR t.name LIKE '%tasm%'
ORDER BY Tabela;
GO

-- ─────────────────────────────────────────────────────────────────
-- A. PRÓBKI DANYCH (top 5 wierszy)
-- ─────────────────────────────────────────────────────────────────
PRINT '═══ A1. HM.DK — 5 najnowszych ═══';
SELECT TOP 5 * FROM HM.DK ORDER BY data DESC;
GO

PRINT '═══ A2. HM.DP — 5 najnowszych ═══';
SELECT TOP 5 * FROM HM.DP ORDER BY id DESC;
GO

PRINT '═══ A3. HM.MZ — 5 najnowszych ═══';
SELECT TOP 5 * FROM HM.MZ ORDER BY id DESC;
GO

PRINT '═══ A4. HM.MG — 5 najnowszych ═══';
SELECT TOP 5 * FROM HM.MG ORDER BY data DESC;
GO

PRINT '═══ A5. HM.TW (katalog 67095 = świeże) — 10 wierszy ═══';
SELECT TOP 10 * FROM HM.TW WHERE katalog = '67095';
GO

PRINT '═══ A6. SSCommon.STContractors — 5 wierszy ═══';
SELECT TOP 5 * FROM SSCommon.STContractors;
GO

PRINT '═══ A7. SSCommon.ContractorClassification — 10 wierszy ═══';
SELECT TOP 10 * FROM SSCommon.ContractorClassification;
GO

-- ─────────────────────────────────────────────────────────────────
-- B. SŁOWNIKI / DISTINCT VALUES
-- ─────────────────────────────────────────────────────────────────
PRINT '═══ B1. Wszystkie SERIE w HM.MG (typy dokumentów magazynowych) ═══';
SELECT seria, COUNT(*) AS Ile, MIN(data) AS Najstarsza, MAX(data) AS Najnowsza
FROM HM.MG WHERE anulowany = 0
GROUP BY seria
ORDER BY Ile DESC;
GO

PRINT '═══ B2. Wszystkie KATALOGI w HM.TW ═══';
SELECT katalog, COUNT(*) AS LiczbaTowarow
FROM HM.TW
GROUP BY katalog
ORDER BY LiczbaTowarow DESC;
GO

PRINT '═══ B3. MAGAZYNY w HM.MG (gdzie 65554 = mroźnia, 67095 = świeże?) ═══';
SELECT magazyn, COUNT(*) AS LiczbaDok, MIN(data) AS Najstarsza, MAX(data) AS Najnowsza
FROM HM.MG
GROUP BY magazyn
ORDER BY LiczbaDok DESC;
GO

PRINT '═══ B4. Najczęstsze wartości CDim_Handlowiec_Val ═══';
SELECT TOP 20 CDim_Handlowiec_Val, COUNT(*) AS Ile
FROM SSCommon.ContractorClassification
WHERE CDim_Handlowiec_Val IS NOT NULL
GROUP BY CDim_Handlowiec_Val
ORDER BY Ile DESC;
GO

-- ─────────────────────────────────────────────────────────────────
-- C. STATYSTYKI WOLUMENU / HISTORIA
-- ─────────────────────────────────────────────────────────────────
PRINT '═══ C1. Historia DK: zakres dat + liczba dokumentów ═══';
SELECT MIN(data) AS NajstarszaDK, MAX(data) AS NajnowszaDK, COUNT(*) AS LiczbaDK
FROM HM.DK WHERE anulowany = 0;
GO

PRINT '═══ C2. Historia MG (przyjęcia magazynowe): zakres dat ═══';
SELECT MIN(data) AS NajstarszaMG, MAX(data) AS NajnowszaMG, COUNT(*) AS LiczbaMG
FROM HM.MG WHERE anulowany = 0;
GO

PRINT '═══ C3. Wolumen DK per rok (czy jest 2+ lata historii?) ═══';
SELECT YEAR(data) AS Rok, COUNT(*) AS Dok, COUNT(DISTINCT khid) AS Klientow
FROM HM.DK WHERE anulowany = 0
GROUP BY YEAR(data)
ORDER BY Rok DESC;
GO

PRINT '═══ C4. Wolumen MG per seria per rok (produkcja świeżych) ═══';
SELECT YEAR(MG.data) AS Rok, MG.seria, COUNT(*) AS LiczbaDok
FROM HM.MG MG
WHERE MG.anulowany = 0
  AND MG.seria IN ('sPWU','PWP','PWX','PRZY','PZ','RWP','WZ','WZS')
GROUP BY YEAR(MG.data), MG.seria
ORDER BY Rok DESC, MG.seria;
GO

PRINT '═══ C5. Próbka DP — co siedzi w kolumnach (do oceny czy jest koszt) ═══';
-- Wszystkie kolumny pokażą się przez SELECT *
-- Ale uważam że typowe dla Symfonii: cena, cenazak, wartzak, wartnetto, wartbrutto, marza
SELECT TOP 3 * FROM HM.DP WHERE id IN (
    SELECT TOP 3 id FROM HM.DP ORDER BY id DESC
);
GO

PRINT '═══ KONIEC HANDEL ═══';
