-- ============================================================
-- 11 — Klucze obce (relacje miedzy tabelami) + indeksy
-- ============================================================
USE LibraNet;
GO

-- A) Wszystkie foreign keys
SELECT
    fk.name AS FK_Name,
    OBJECT_NAME(fk.parent_object_id) AS Tabela_dziecko,
    cp.name AS Kolumna_dziecko,
    OBJECT_NAME(fk.referenced_object_id) AS Tabela_rodzic,
    cr.name AS Kolumna_rodzic,
    fk.delete_referential_action_desc AS OnDelete,
    fk.update_referential_action_desc AS OnUpdate
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc
    ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns cp
    ON cp.object_id = fkc.parent_object_id
   AND cp.column_id = fkc.parent_column_id
JOIN sys.columns cr
    ON cr.object_id = fkc.referenced_object_id
   AND cr.column_id = fkc.referenced_column_id
ORDER BY OBJECT_NAME(fk.parent_object_id), fk.name;
GO

-- B) Indeksy dla kluczowych tabel (po jednej kolumnie na wiersz)
SELECT
    OBJECT_NAME(i.object_id) AS table_name,
    i.name AS index_name,
    i.type_desc,
    c.name AS column_name,
    ic.key_ordinal,
    i.is_unique,
    i.is_primary_key
FROM sys.indexes i
JOIN sys.index_columns ic
    ON ic.object_id = i.object_id
   AND ic.index_id = i.index_id
JOIN sys.columns c
    ON c.object_id = ic.object_id
   AND c.column_id = ic.column_id
WHERE OBJECT_NAME(i.object_id) IN (
    'In0E','Out1A','Article','PartiaDostawca','listapartii',
    'ZamowieniaMieso','ZamowieniaMiesoTowar','HarmonogramDostaw',
    'FarmerCalc','PartiaStatus','QC_Normy','KartotekaOdbiorcyDane',
    'Pozyskiwanie_Hodowcy','Kierowca','Pojazd','Kurs','Ladunek'
)
ORDER BY table_name, index_name, key_ordinal;
GO

-- C) Liczba indeksow per tabela
SELECT
    OBJECT_NAME(i.object_id) AS table_name,
    COUNT(DISTINCT i.index_id) AS liczba_indeksow,
    SUM(CASE WHEN i.is_primary_key = 1 THEN 1 ELSE 0 END) AS pk_count,
    SUM(CASE WHEN i.is_unique = 1 AND i.is_primary_key = 0 THEN 1 ELSE 0 END) AS unique_count
FROM sys.indexes i
WHERE i.index_id > 0
  AND OBJECT_NAME(i.object_id) NOT LIKE 'sys%'
GROUP BY i.object_id
ORDER BY liczba_indeksow DESC;
GO

-- D) Default constraints
SELECT
    OBJECT_NAME(parent_object_id) AS table_name,
    name AS constraint_name,
    OBJECT_DEFINITION(object_id) AS definicja
FROM sys.default_constraints
WHERE OBJECT_NAME(parent_object_id) IN (
    'In0E','listapartii','ZamowieniaMieso','PartiaStatus','QC_Normy'
)
ORDER BY table_name;
GO

-- E) Check constraints
SELECT
    OBJECT_NAME(parent_object_id) AS table_name,
    name AS constraint_name,
    definition
FROM sys.check_constraints
ORDER BY table_name;
GO
