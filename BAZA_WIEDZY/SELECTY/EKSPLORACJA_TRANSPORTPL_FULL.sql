-- =============================================================================
-- EKSPLORACJA TransportPL (192.168.0.109) - PEŁNY OBRAZ
-- =============================================================================
-- Cel: poznać schema TransportPL gdzie są główne tabele transportu
-- (Kierowca, Pojazd, Kurs, Ladunek)
--
-- INSTRUKCJA:
--   1. SSMS -> 192.168.0.109 (pronova/pronova)
--   2. Ctrl+T -> F5 -> Ctrl+A -> Ctrl+C
--   3. Wklej do BAZA_WIEDZY/SELECTY/WYNIKI_TRANSPORTPL.txt
-- =============================================================================

USE TransportPL;
GO

SET NOCOUNT ON;
GO

-- =============================================================================
-- BLOK T01 - METADANE
-- =============================================================================

SELECT 'T01_A_baza' AS __SEKCJA,
       DB_NAME() AS BazaDanych,
       CAST(SERVERPROPERTY('ProductVersion') AS varchar(50)) AS WersjaSQL,
       CAST(DATABASEPROPERTYEX(DB_NAME(), 'Collation') AS varchar(100)) AS Collation;
GO

SELECT
    'T01_B_liczba' AS __SEKCJA,
    (SELECT COUNT(*) FROM sys.tables) AS LiczbaTabel,
    (SELECT COUNT(*) FROM sys.views) AS LiczbaWidokow,
    (SELECT COUNT(*) FROM sys.procedures) AS LiczbaProcedur,
    (SELECT COUNT(*) FROM sys.foreign_keys) AS LiczbaFK;
GO

-- =============================================================================
-- BLOK T02 - LISTA TABEL
-- =============================================================================

SELECT
    'T02_A_top_tabel' AS __SEKCJA,
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

-- =============================================================================
-- BLOK T03 - KIEROWCA
-- =============================================================================

SELECT
    'T03_A_kierowca_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Kierowca'
ORDER BY ORDINAL_POSITION;
GO

SELECT TOP 20 'T03_B_kierowca_sample' AS __SEKCJA, *
FROM dbo.Kierowca ORDER BY 1;
GO

-- =============================================================================
-- BLOK T04 - POJAZD
-- =============================================================================

SELECT
    'T04_A_pojazd_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Pojazd'
ORDER BY ORDINAL_POSITION;
GO

SELECT TOP 20 'T04_B_pojazd_sample' AS __SEKCJA, *
FROM dbo.Pojazd ORDER BY 1;
GO

-- =============================================================================
-- BLOK T05 - KURS
-- =============================================================================

SELECT
    'T05_A_kurs_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Kurs'
ORDER BY ORDINAL_POSITION;
GO

SELECT TOP 10 'T05_B_kurs_sample' AS __SEKCJA, *
FROM dbo.Kurs ORDER BY KursID DESC;
GO

-- 05.C - Statusy kursow
SELECT
    'T05_C_statusy_kursow' AS __SEKCJA,
    Status,
    COUNT(*) AS liczba
FROM dbo.Kurs
GROUP BY Status
ORDER BY liczba DESC;
GO

-- 05.D - Kursy per miesiac (12 miesiecy)
SELECT
    'T05_D_kursy_per_miesiac' AS __SEKCJA,
    YEAR(DataKursu) AS rok,
    MONTH(DataKursu) AS miesiac,
    COUNT(*) AS liczba_kursow
FROM dbo.Kurs
WHERE DataKursu >= DATEADD(MONTH, -12, GETDATE())
GROUP BY YEAR(DataKursu), MONTH(DataKursu)
ORDER BY rok DESC, miesiac DESC;
GO

-- =============================================================================
-- BLOK T06 - LADUNEK
-- =============================================================================

SELECT
    'T06_A_ladunek_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Ladunek'
ORDER BY ORDINAL_POSITION;
GO

SELECT TOP 10 'T06_B_ladunek_sample' AS __SEKCJA, *
FROM dbo.Ladunek ORDER BY LadunekID DESC;
GO

-- =============================================================================
-- BLOK T07 - TRANSPORTZMIANY (workflow akceptacji)
-- =============================================================================

SELECT
    'T07_A_zmiany_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'TransportZmiany';
GO

SELECT TOP 10 'T07_B_zmiany_sample' AS __SEKCJA, *
FROM dbo.TransportZmiany ORDER BY 1 DESC;
GO

-- =============================================================================
-- BLOK T08 - WIDOKI vKursWypelnienie i inne
-- =============================================================================

SELECT
    'T08_A_widoki' AS __SEKCJA,
    v.name,
    LEFT(m.definition, 2500) AS definicja
FROM sys.views v
JOIN sys.sql_modules m ON m.object_id = v.object_id
ORDER BY v.name;
GO

-- =============================================================================
-- BLOK T09 - PROCEDURY
-- =============================================================================

SELECT
    'T09_A_procedury' AS __SEKCJA,
    p.name,
    LEFT(m.definition, 2500) AS definicja
FROM sys.procedures p
JOIN sys.sql_modules m ON m.object_id = p.object_id
ORDER BY p.name;
GO

-- =============================================================================
-- BLOK T10 - FOREIGN KEYS
-- =============================================================================

SELECT
    'T10_A_foreign_keys' AS __SEKCJA,
    fk.name AS FK_Name,
    OBJECT_NAME(fk.parent_object_id) AS Tabela_dziecko,
    cp.name AS Kolumna_dziecko,
    OBJECT_NAME(fk.referenced_object_id) AS Tabela_rodzic,
    cr.name AS Kolumna_rodzic
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns cp ON cp.object_id = fkc.parent_object_id AND cp.column_id = fkc.parent_column_id
JOIN sys.columns cr ON cr.object_id = fkc.referenced_object_id AND cr.column_id = fkc.referenced_column_id
ORDER BY OBJECT_NAME(fk.parent_object_id);
GO

PRINT 'TransportPL eksploracja zakończona';
GO
