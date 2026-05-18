-- =============================================================================
-- EKSPLORACJA HANDEL (Symfonia, 192.168.0.112) - PEŁNY OBRAZ
-- =============================================================================
-- Cel: poznać schema Symfonii Handel którego ZPSP używa do faktur, magazynów,
-- kontrahentów. Po skończeniu będę mógł zaproponować integracje analityczne.
--
-- INSTRUKCJA:
--   1. SSMS -> 192.168.0.112 (login: sa / haslo: ?cs_'Y6,n5#Xd'Yd)
--      UWAGA: nie pronova! tu uzytkownik 'sa' z trudnym haslem
--   2. Ctrl+T (tryb tekstowy)
--   3. F5 (uruchom calosc)
--   4. Ctrl+A -> Ctrl+C
--   5. Wklej do BAZA_WIEDZY/SELECTY/WYNIKI_HANDEL.txt
-- =============================================================================

USE master;
GO

SET NOCOUNT ON;
GO

PRINT '################################################################';
PRINT '## EKSPLORACJA HANDEL (Symfonia 112) - START                  ##';
PRINT '################################################################';
GO

-- =============================================================================
-- BLOK H00 - METADANE SERWERA
-- =============================================================================

SELECT 'H00_A_wersja' AS __SEKCJA, @@VERSION AS WersjaServera;
GO

SELECT
    'H00_B_dostepne_bazy' AS __SEKCJA,
    name AS DatabaseName,
    state_desc,
    create_date,
    collation_name
FROM sys.databases
WHERE name NOT IN ('master','tempdb','msdb','model')
ORDER BY name;
GO

-- =============================================================================
-- BLOK H01 - LISTA SCHEMAS w Handel (HM, SSCommon, MF, dbo, MFPriv)
-- =============================================================================

USE Handel;
GO

SELECT
    'H01_A_schemas' AS __SEKCJA,
    s.name AS schema_name,
    COUNT(t.object_id) AS liczba_tabel
FROM sys.schemas s
LEFT JOIN sys.tables t ON t.schema_id = s.schema_id
GROUP BY s.name
ORDER BY liczba_tabel DESC;
GO

-- =============================================================================
-- BLOK H02 - TOP 100 TABEL z Handel po liczbie wierszy (wszystkie schematy)
-- =============================================================================

SELECT TOP 100
    'H02_A_top_tabel' AS __SEKCJA,
    s.name + '.' + t.name AS TableFullName,
    p.rows AS RowCount_,
    SUM(a.total_pages) * 8 / 1024.0 AS TotalMB
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1)
JOIN sys.allocation_units a ON a.container_id = p.partition_id
GROUP BY s.name, t.name, p.rows
ORDER BY p.rows DESC;
GO

SELECT
    'H02_B_liczba_obiektow' AS __SEKCJA,
    (SELECT COUNT(*) FROM sys.tables) AS LiczbaTabel,
    (SELECT COUNT(*) FROM sys.views) AS LiczbaWidokow,
    (SELECT COUNT(*) FROM sys.procedures) AS LiczbaProcedur,
    (SELECT COUNT(*) FROM sys.foreign_keys) AS LiczbaForeignKeys;
GO

-- =============================================================================
-- BLOK H03 - HM.DK (dokumenty handlowe - faktury, WZ, korekty)
-- =============================================================================

SELECT
    'H03_A_dk_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'HM' AND TABLE_NAME = 'DK'
ORDER BY ORDINAL_POSITION;
GO

SELECT TOP 5 'H03_B_dk_sample' AS __SEKCJA, *
FROM HM.DK
ORDER BY id DESC;
GO

-- 30 ostatnich dokumentów per typ (FVS, sWZ, FKS, sPZ, sPWU, RWP, FKSB, FWK)
SELECT
    'H03_C_dk_typy_dokumentow' AS __SEKCJA,
    seria AS DocSeria,
    COUNT(*) AS liczba_dokumentow,
    MIN(data) AS najstarsza,
    MAX(data) AS najnowsza
FROM HM.DK
WHERE data >= DATEADD(DAY, -90, GETDATE())
GROUP BY seria
ORDER BY liczba_dokumentow DESC;
GO

-- =============================================================================
-- BLOK H04 - HM.DP (pozycje dokumentow - linie faktur)
-- =============================================================================

SELECT
    'H04_A_dp_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'HM' AND TABLE_NAME = 'DP'
ORDER BY ORDINAL_POSITION;
GO

SELECT TOP 5 'H04_B_dp_sample' AS __SEKCJA, *
FROM HM.DP
ORDER BY id DESC;
GO

-- =============================================================================
-- BLOK H05 - HM.TW (towary - kartoteka Symfonii)
-- =============================================================================

SELECT
    'H05_A_tw_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'HM' AND TABLE_NAME = 'TW'
ORDER BY ORDINAL_POSITION;
GO

SELECT TOP 10 'H05_B_tw_sample' AS __SEKCJA, *
FROM HM.TW
WHERE rownosc IS NULL OR rownosc = 0
ORDER BY id DESC;
GO

-- =============================================================================
-- BLOK H06 - HM.MG (magazyny - katalog magazynów)
-- =============================================================================

SELECT
    'H06_A_mg_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'HM' AND TABLE_NAME = 'MG'
ORDER BY ORDINAL_POSITION;
GO

SELECT TOP 30 'H06_B_mg_sample' AS __SEKCJA, *
FROM HM.MG;
GO

-- Magazyny ktore Sergiusz wymienial: 65554, 65556, 65552, 65547, 65562, 65559, 65883
SELECT
    'H06_C_mg_konkretne' AS __SEKCJA, *
FROM HM.MG
WHERE id IN (65554, 65556, 65552, 65547, 65562, 65559, 65883);
GO

-- =============================================================================
-- BLOK H07 - HM.MZ (zapasy magazynowe / pozycje magazynu)
-- =============================================================================

SELECT
    'H07_A_mz_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'HM' AND TABLE_NAME = 'MZ'
ORDER BY ORDINAL_POSITION;
GO

SELECT TOP 5 'H07_B_mz_sample' AS __SEKCJA, *
FROM HM.MZ
ORDER BY id DESC;
GO

-- 07.C - Czy ProductionLineID jest gdzies wypelnione? (Sergiusz mowil ze 100% NULL)
SELECT
    'H07_C_mz_productionline' AS __SEKCJA,
    COUNT(*) AS razem,
    SUM(CASE WHEN ProductionLineID IS NOT NULL THEN 1 ELSE 0 END) AS z_productionline,
    SUM(CASE WHEN ProductionLineID IS NULL THEN 1 ELSE 0 END) AS bez_productionline
FROM HM.MZ;
GO

-- =============================================================================
-- BLOK H08 - SSCommon.STContractors (kontrahenci)
-- =============================================================================

SELECT
    'H08_A_kontrahenci_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'SSCommon' AND TABLE_NAME = 'STContractors'
ORDER BY ORDINAL_POSITION;
GO

SELECT TOP 10 'H08_B_kontrahenci_sample' AS __SEKCJA, *
FROM SSCommon.STContractors
ORDER BY ID DESC;
GO

-- =============================================================================
-- BLOK H09 - SSCommon.STPostOfficeAddresses (adresy)
-- =============================================================================

SELECT
    'H09_A_adresy_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'SSCommon' AND TABLE_NAME = 'STPostOfficeAddresses'
ORDER BY ORDINAL_POSITION;
GO

SELECT TOP 5 'H09_B_adresy_sample' AS __SEKCJA, *
FROM SSCommon.STPostOfficeAddresses;
GO

-- =============================================================================
-- BLOK H10 - SSCommon.ContractorClassification (klasyfikacja kontrahentów)
-- =============================================================================

SELECT
    'H10_A_classification_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'SSCommon' AND TABLE_NAME = 'ContractorClassification'
ORDER BY ORDINAL_POSITION;
GO

SELECT TOP 30 'H10_B_classification_sample' AS __SEKCJA, *
FROM SSCommon.ContractorClassification;
GO

-- =============================================================================
-- BLOK H11 - MF.Production* (87 tabel ktore Sergiusz mowil ze sa puste)
-- =============================================================================

SELECT
    'H11_A_mf_production_tabele' AS __SEKCJA,
    s.name + '.' + t.name AS TableFullName,
    p.rows AS RowCount_
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1)
WHERE s.name = 'MF'
  AND t.name LIKE '%Production%'
ORDER BY p.rows DESC;
GO

-- =============================================================================
-- BLOK H12 - PROBA: czy moduł produkcji Symfonii ma cokolwiek?
-- =============================================================================

-- 12.A - Wszystkie tabele MF.* z liczbą wierszy
SELECT
    'H12_A_mf_wszystkie' AS __SEKCJA,
    s.name + '.' + t.name AS TableFullName,
    p.rows AS RowCount_
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1)
WHERE s.name LIKE 'MF%'
ORDER BY p.rows DESC;
GO

-- =============================================================================
-- BLOK H13 - SERIE DOKUMENTOW W UZYCIU (sPZ, sPWU, RWP, sWZ, FVS, FKS, FKSB, FWK)
-- =============================================================================

SELECT TOP 50
    'H13_A_serie_dokumentow' AS __SEKCJA,
    seria,
    COUNT(*) AS liczba_dokumentow,
    MIN(data) AS najstarsza,
    MAX(data) AS najnowsza
FROM HM.DK
GROUP BY seria
ORDER BY liczba_dokumentow DESC;
GO

-- =============================================================================
-- BLOK H14 - DOKUMENTY DZIENNE (90 dni) per seria
-- =============================================================================

SELECT
    'H14_A_dokumenty_30dni' AS __SEKCJA,
    seria,
    COUNT(*) AS liczba_dni_z_dokumentami,
    SUM(1) AS dokumenty,
    MIN(data) AS pierwsza,
    MAX(data) AS ostatnia
FROM HM.DK
WHERE data >= DATEADD(DAY, -30, GETDATE())
GROUP BY seria
ORDER BY dokumenty DESC;
GO

-- =============================================================================
-- BLOK H15 - DOKUMENT TYP NAJCZESCIEJ (sample 10 sWZ - wydania)
-- =============================================================================

SELECT TOP 10
    'H15_A_swz_sample' AS __SEKCJA,
    *
FROM HM.DK
WHERE seria LIKE '%WZ%'
  AND data >= DATEADD(DAY, -30, GETDATE())
ORDER BY data DESC, id DESC;
GO

-- =============================================================================
-- BLOK H16 - PROCEDURY i WIDOKI w Handel
-- =============================================================================

SELECT
    'H16_A_widoki' AS __SEKCJA,
    s.name + '.' + v.name AS view_name,
    v.create_date
FROM sys.views v
JOIN sys.schemas s ON s.schema_id = v.schema_id
ORDER BY v.create_date DESC;
GO

SELECT
    'H16_B_procedury' AS __SEKCJA,
    s.name + '.' + p.name AS proc_name,
    p.create_date
FROM sys.procedures p
JOIN sys.schemas s ON s.schema_id = p.schema_id
ORDER BY p.create_date DESC;
GO

-- =============================================================================
-- BLOK H17 - WSZYSTKIE TABELE FULL (alfabetycznie z liczbami)
-- =============================================================================

SELECT
    'H17_A_wszystkie_tabele' AS __SEKCJA,
    s.name + '.' + t.name AS TableFullName,
    p.rows AS RowCount_
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1)
ORDER BY s.name, t.name;
GO

PRINT '################################################################';
PRINT '## EKSPLORACJA HANDEL - KONIEC                                 ##';
PRINT '################################################################';
GO
