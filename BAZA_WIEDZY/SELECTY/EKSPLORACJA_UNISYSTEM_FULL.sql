-- =============================================================================
-- EKSPLORACJA UNISYSTEM (192.168.0.23\SQLEXPRESS) - UNICARD RCP
-- =============================================================================
-- Cel: poznać schema UNICARD - rejestrator czasu pracy pracowników
--
-- INSTRUKCJA:
--   1. SSMS -> 192.168.0.23\SQLEXPRESS (jakie konto?)
--      jeśli SSPI - Windows Authentication
--      jeśli SQL - dany login (sprawdzić w app)
--   2. Ctrl+T -> F5 -> Ctrl+A -> Ctrl+C
--   3. Wklej do BAZA_WIEDZY/SELECTY/WYNIKI_UNISYSTEM.txt
-- =============================================================================

USE master;
GO

SET NOCOUNT ON;
GO

-- =============================================================================
-- BLOK U01 - DOSTEPNE BAZY
-- =============================================================================

SELECT
    'U01_A_bazy' AS __SEKCJA,
    name AS DatabaseName,
    state_desc,
    create_date
FROM sys.databases
WHERE name NOT IN ('master','tempdb','msdb','model')
ORDER BY name;
GO

-- =============================================================================
-- BLOK U02 - UNISYSTEM (UNICARD)
-- =============================================================================

USE UNISYSTEM;
GO

SELECT 'U02_A_baza' AS __SEKCJA,
       DB_NAME() AS BazaDanych,
       CAST(SERVERPROPERTY('ProductVersion') AS varchar(50)) AS WersjaSQL;
GO

SELECT
    'U02_B_top_tabel' AS __SEKCJA,
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

SELECT 'U02_C_widoki' AS __SEKCJA, name FROM sys.views ORDER BY name;
GO

-- =============================================================================
-- BLOK U03 - V_RCINE_EMPLOYEES (kluczowy widok)
-- =============================================================================

-- Czy istnieje?
SELECT 'U03_A_view_exists' AS __SEKCJA,
       CASE WHEN OBJECT_ID('V_RCINE_EMPLOYEES','V') IS NOT NULL THEN 'TAK' ELSE 'NIE' END AS exists_;
GO

-- Struktura
SELECT
    'U03_B_employees_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'V_RCINE_EMPLOYEES'
ORDER BY ORDINAL_POSITION;
GO

-- Sample 10
SELECT TOP 10 'U03_C_employees_sample' AS __SEKCJA, *
FROM V_RCINE_EMPLOYEES;
GO

-- =============================================================================
-- BLOK U04 - REJESTRACJE WEJSCIA / WYJSCIA (czytniki kart)
-- =============================================================================

-- Lista tabel z RC w nazwie
SELECT
    'U04_A_rc_tabele' AS __SEKCJA,
    name AS table_name,
    (SELECT SUM(p.rows) FROM sys.partitions p
     WHERE p.object_id = t.object_id AND p.index_id IN (0,1)) AS row_count
FROM sys.tables t
WHERE name LIKE '%RC%' OR name LIKE '%Time%' OR name LIKE '%Punch%'
   OR name LIKE '%Reg%' OR name LIKE '%Card%' OR name LIKE '%Pres%'
ORDER BY row_count DESC;
GO

-- =============================================================================
-- BLOK U05 - STRUKTURY TOP 20 TABEL
-- =============================================================================

DECLARE @sql nvarchar(max) = '';
SELECT TOP 20 @sql = @sql +
    'SELECT ''U05_struktury'' AS __SEKCJA, ''' + name + ''' AS tabela, COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = ''' + name + ''';' + CHAR(13)
FROM (
    SELECT TOP 20 t.name,
           (SELECT SUM(p.rows) FROM sys.partitions p
            WHERE p.object_id = t.object_id AND p.index_id IN (0,1)) AS rc
    FROM sys.tables t
    ORDER BY rc DESC
) x;
EXEC(@sql);
GO

PRINT 'UNISYSTEM eksploracja zakończona';
GO
