/* ============================================================================
   analiza_maja_DIAGNOSTYKA_LIBRANET.sql
   ----------------------------------------------------------------------------
   Uruchom NAJPIERW na: 192.168.0.109 / LibraNet (user pronova)
   Cel: pokazać jakie tabele/kolumny faktycznie istnieją w bazie, żeby dostosować
        analiza_maja_LIBRANET.sql do rzeczywistości.

   Wklej wyniki (wszystkie 5 resultsetów) do czatu — naprawię główny skrypt.
   ============================================================================ */

USE [LibraNet];
GO

SET NOCOUNT ON;

SELECT N'1 — Wersja SQL Server' AS [Raport];
SELECT @@VERSION AS Wersja, DB_NAME() AS BiezacaBaza, SUSER_NAME() AS Uzytkownik;

SELECT N'2 — Czy istnieją tabele zamówień / reklamacji / handlowców' AS [Raport];
SELECT t.TABLE_SCHEMA, t.TABLE_NAME,
       (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS c
         WHERE c.TABLE_SCHEMA = t.TABLE_SCHEMA AND c.TABLE_NAME = t.TABLE_NAME) AS LiczbaKolumn
FROM INFORMATION_SCHEMA.TABLES t
WHERE t.TABLE_TYPE = 'BASE TABLE'
  AND (t.TABLE_NAME LIKE '%amow%'      -- Zamowienia / zamow / Zamówienia
    OR t.TABLE_NAME LIKE '%eklam%'     -- Reklamacje / reklam
    OR t.TABLE_NAME LIKE '%andlow%'    -- Handlowcy / UserHandlowcy
    OR t.TABLE_NAME LIKE '%peratorzy%' -- Operators / Operatorzy
    OR t.TABLE_NAME LIKE '%otatk%'     -- Notatki
    OR t.TABLE_NAME LIKE '%emind%'     -- CallReminder
    OR t.TABLE_NAME LIKE '%dbiorc%')   -- Odbiorcy
ORDER BY t.TABLE_NAME;

SELECT N'3 — Kolumny w pierwszej kandydatce ZamowieniaMieso (jeśli istnieje)' AS [Raport];
SELECT TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME, DATA_TYPE, IS_NULLABLE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME LIKE '%amow%'
ORDER BY TABLE_NAME, ORDINAL_POSITION;

SELECT N'4 — Kolumny w tabeli Reklamacje (jeśli istnieje)' AS [Raport];
SELECT TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME, DATA_TYPE, IS_NULLABLE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME LIKE '%eklam%'
ORDER BY TABLE_NAME, ORDINAL_POSITION;

SELECT N'5 — Kolumny w UserHandlowcy / Operators (jeśli istnieją)' AS [Raport];
SELECT TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME, DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('UserHandlowcy', 'AvailableHandlowcy', 'Operators',
                     'NotatkiUzycia', 'NotatkiSzablony', 'CallReminderConfig',
                     'HandlowcyCRM', 'WlascicieleOdbiorcow', 'MapowanieHandlowcow')
ORDER BY TABLE_NAME, ORDINAL_POSITION;
