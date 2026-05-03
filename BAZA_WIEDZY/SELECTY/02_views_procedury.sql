-- ============================================================
-- 02 — Lista widoków i stored procedures
-- ============================================================
USE LibraNet;
GO

-- A) Lista wszystkich widoków
SELECT
    name,
    create_date,
    modify_date
FROM sys.views
ORDER BY name;
GO

-- B) Lista wszystkich stored procedures
SELECT
    name,
    create_date,
    modify_date
FROM sys.procedures
ORDER BY name;
GO

-- C) Lista wszystkich funkcji
SELECT
    name,
    type_desc,
    create_date
FROM sys.objects
WHERE type IN ('FN', 'IF', 'TF', 'FS', 'FT')
ORDER BY name;
GO

-- D) Definicje top 5 widoków (skrócone, max 1500 znaków)
SELECT TOP 5
    v.name,
    LEFT(m.definition, 1500) AS definition_preview
FROM sys.views v
JOIN sys.sql_modules m ON m.object_id = v.object_id
ORDER BY v.modify_date DESC;
GO

-- E) Triggery
SELECT
    OBJECT_NAME(parent_id) AS table_name,
    name AS trigger_name,
    create_date,
    is_disabled
FROM sys.triggers
WHERE parent_class_desc = 'OBJECT_OR_COLUMN'
ORDER BY OBJECT_NAME(parent_id), name;
GO
