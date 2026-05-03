-- ============================================================
-- 12 — Triggery + procedury uzywajace tabel kluczowych
-- ============================================================
USE LibraNet;
GO

-- A) Wszystkie triggery na tabelach
SELECT
    OBJECT_NAME(parent_id) AS table_name,
    name AS trigger_name,
    is_disabled,
    is_instead_of_trigger,
    create_date,
    modify_date
FROM sys.triggers
WHERE parent_class_desc = 'OBJECT_OR_COLUMN'
ORDER BY OBJECT_NAME(parent_id), name;
GO

-- B) Procedury i widoki uzywajace tabel kluczowych
SELECT DISTINCT
    o.name AS obj_name,
    o.type_desc,
    o.create_date,
    o.modify_date
FROM sys.sql_modules m
JOIN sys.objects o ON o.object_id = m.object_id
WHERE m.definition LIKE '%In0E%'
   OR m.definition LIKE '%listapartii%'
   OR m.definition LIKE '%PartiaDostawca%'
   OR m.definition LIKE '%ZamowieniaMieso%'
   OR m.definition LIKE '%HarmonogramDostaw%'
ORDER BY o.modify_date DESC;
GO

-- C) Definicje wszystkich procedur (skrocone do 1500 znakow)
SELECT
    p.name,
    LEFT(m.definition, 1500) AS definition_preview,
    p.modify_date
FROM sys.procedures p
JOIN sys.sql_modules m ON m.object_id = p.object_id
ORDER BY p.name;
GO

-- D) Definicje wszystkich widokow (skrocone do 1500 znakow)
SELECT
    v.name,
    LEFT(m.definition, 1500) AS definition_preview,
    v.modify_date
FROM sys.views v
JOIN sys.sql_modules m ON m.object_id = v.object_id
ORDER BY v.name;
GO
