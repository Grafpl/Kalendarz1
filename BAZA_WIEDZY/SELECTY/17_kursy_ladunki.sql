-- ============================================================
-- 17 — Kursy + Ladunki + Kierowca + Pojazd (transport w LibraNet)
-- ============================================================
USE LibraNet;
GO

-- A) Struktury
SELECT
    TABLE_NAME, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('Kierowca', 'Pojazd', 'Kurs', 'Ladunek',
                     'Driver', 'CarTrailer', 'MatrycaTransferLog')
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

-- B) Liczba rekordow
SELECT 'Kierowca' AS tabela, COUNT(*) AS rekordow FROM dbo.Kierowca
UNION ALL SELECT 'Pojazd', COUNT(*) FROM dbo.Pojazd
UNION ALL SELECT 'Kurs', COUNT(*) FROM dbo.Kurs
UNION ALL SELECT 'Ladunek', COUNT(*) FROM dbo.Ladunek;
GO

-- C) Sample 5 najnowszych kursow
SELECT TOP 5 *
FROM dbo.Kurs
ORDER BY 1 DESC;
GO

-- D) Sample 5 najnowszych ladunkow
SELECT TOP 5 *
FROM dbo.Ladunek
ORDER BY 1 DESC;
GO

-- E) Sample 5 kierowcow
SELECT TOP 5 *
FROM dbo.Kierowca;
GO

-- F) Sample 5 pojazdow
SELECT TOP 5 *
FROM dbo.Pojazd;
GO

-- G) Czy istnieje tabela TransportPL (na tym samym serwerze, inna baza)
USE master;
GO
SELECT name AS bazy_dostepne
FROM sys.databases
WHERE name IN ('LibraNet', 'TransportPL', 'Handel', 'master', 'tempdb', 'msdb', 'model')
ORDER BY name;
GO
