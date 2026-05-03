-- ============================================================
-- 03 — Wersja serwera + collation + funkcje dostepne
-- ============================================================
USE LibraNet;
GO

-- A) Wersja SQL Server
SELECT @@VERSION AS WersjaServera;
GO

-- B) Nazwa bazy + collation
SELECT
    DB_NAME()                  AS BazaDanych,
    SERVERPROPERTY('ProductVersion')  AS ProductVersion,
    SERVERPROPERTY('ProductLevel')    AS ProductLevel,
    SERVERPROPERTY('Edition')         AS Edition,
    SERVERPROPERTY('Collation')       AS DefaultCollation,
    DATABASEPROPERTYEX(DB_NAME(), 'Collation') AS DBCollation;
GO

-- C) Test ktore funkcje sa dostepne
-- Jesli ktorys SELECT zwroci blad - funkcja nie istnieje na tej wersji
BEGIN TRY
    SELECT TRY_CONVERT(int, '123') AS test_try_convert;
END TRY
BEGIN CATCH
    SELECT 'TRY_CONVERT NIE DZIALA' AS info;
END CATCH;
GO

-- D) Rozmiar bazy
SELECT
    name AS LogicalName,
    type_desc,
    size * 8 / 1024.0 AS SizeMB
FROM sys.master_files
WHERE database_id = DB_ID();
GO
