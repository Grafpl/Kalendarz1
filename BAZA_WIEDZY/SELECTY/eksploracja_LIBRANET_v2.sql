/* ============================================================================
   eksploracja_LIBRANET_v2.sql — Pełna inwentaryzacja LibraNet
   ----------------------------------------------------------------------------
   Uruchom na: 192.168.0.109 / LibraNet (user pronova/pronova)
   ============================================================================ */

USE [LibraNet];
GO

SET NOCOUNT ON;

/* ----------------------------------------------------------------------------
   1. WSZYSTKIE TABELE + WIDOKI
---------------------------------------------------------------------------- */
SELECT N'1 — Wszystkie tabele dbo.* + liczba kolumn + liczba wierszy' AS [Raport];

WITH RowCounts AS (
    SELECT t.object_id, t.name AS Tabela, SUM(p.rows) AS LiczbaWierszy
    FROM sys.tables t
    INNER JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1)
    INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = 'dbo'
    GROUP BY t.object_id, t.name
),
ColCounts AS (SELECT object_id, COUNT(*) AS LiczbaKolumn FROM sys.columns GROUP BY object_id)
SELECT rc.Tabela, ISNULL(cc.LiczbaKolumn, 0) AS LiczbaKolumn, rc.LiczbaWierszy
FROM RowCounts rc LEFT JOIN ColCounts cc ON cc.object_id = rc.object_id
ORDER BY rc.LiczbaWierszy DESC;

SELECT N'2 — Wszystkie widoki dbo.* + definicja' AS [Raport];

SELECT v.name AS Widok,
       LEFT(OBJECT_DEFINITION(v.object_id), 500) AS DefinicjaSkrocona
FROM sys.views v
INNER JOIN sys.schemas s ON s.schema_id = v.schema_id
WHERE s.name = 'dbo'
ORDER BY v.name;

/* ----------------------------------------------------------------------------
   2. PEŁNE KOLUMNY GŁÓWNYCH TABEL
---------------------------------------------------------------------------- */
SELECT N'3 — In0E (ważenia produkcyjne) — wszystkie kolumny' AS [Raport];
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH AS Dlugosc, IS_NULLABLE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'In0E' ORDER BY ORDINAL_POSITION;

SELECT N'4 — listapartii — wszystkie kolumny' AS [Raport];
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH AS Dlugosc, IS_NULLABLE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'listapartii' ORDER BY ORDINAL_POSITION;

SELECT N'5 — PartiaDostawca — wszystkie kolumny' AS [Raport];
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH AS Dlugosc, IS_NULLABLE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'PartiaDostawca' ORDER BY ORDINAL_POSITION;

SELECT N'6 — HarmonogramDostaw — wszystkie kolumny (kontrakty żywca!)' AS [Raport];
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH AS Dlugosc, IS_NULLABLE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'HarmonogramDostaw' ORDER BY ORDINAL_POSITION;

SELECT N'7 — FarmerCalc (rozliczenia hodowcy) — wszystkie kolumny' AS [Raport];
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH AS Dlugosc, IS_NULLABLE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'FarmerCalc' ORDER BY ORDINAL_POSITION;

SELECT N'8 — Pozyskiwanie_Hodowcy (CRM hodowców) — wszystkie kolumny' AS [Raport];
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH AS Dlugosc, IS_NULLABLE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Pozyskiwanie_Hodowcy' ORDER BY ORDINAL_POSITION;

SELECT N'9 — Pozyskiwanie_Aktywnosci — wszystkie kolumny' AS [Raport];
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH AS Dlugosc, IS_NULLABLE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Pozyskiwanie_Aktywnosci' ORDER BY ORDINAL_POSITION;

SELECT N'10 — ZamowieniaMieso (54 kol.) — wszystkie kolumny' AS [Raport];
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH AS Dlugosc, IS_NULLABLE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ZamowieniaMieso' ORDER BY ORDINAL_POSITION;

SELECT N'11 — ZamowieniaMiesoTowar — wszystkie kolumny' AS [Raport];
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH AS Dlugosc, IS_NULLABLE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ZamowieniaMiesoTowar' ORDER BY ORDINAL_POSITION;

SELECT N'12 — Reklamacje (39 kol.) — wszystkie kolumny' AS [Raport];
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH AS Dlugosc, IS_NULLABLE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Reklamacje' ORDER BY ORDINAL_POSITION;

SELECT N'13 — Odbiorcy (28 kol.) — wszystkie kolumny' AS [Raport];
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH AS Dlugosc, IS_NULLABLE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Odbiorcy' ORDER BY ORDINAL_POSITION;

SELECT N'14 — OdbiorcyCRM (60 kol.) — wszystkie kolumny' AS [Raport];
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH AS Dlugosc, IS_NULLABLE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'OdbiorcyCRM' ORDER BY ORDINAL_POSITION;

SELECT N'15 — OdbiorcyDaneFinansowe — wszystkie kolumny' AS [Raport];
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH AS Dlugosc, IS_NULLABLE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'OdbiorcyDaneFinansowe' ORDER BY ORDINAL_POSITION;

SELECT N'16 — KartotekaOdbiorcyDane (22 kol.) — wszystkie kolumny' AS [Raport];
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH AS Dlugosc, IS_NULLABLE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'KartotekaOdbiorcyDane' ORDER BY ORDINAL_POSITION;

SELECT N'17 — operators (system użytkowników) — wszystkie kolumny' AS [Raport];
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH AS Dlugosc, IS_NULLABLE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'operators' ORDER BY ORDINAL_POSITION;

SELECT N'18 — CallReminderConfig (39 kol.) — wszystkie kolumny' AS [Raport];
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH AS Dlugosc, IS_NULLABLE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CallReminderConfig' ORDER BY ORDINAL_POSITION;

SELECT N'19 — CallReminderLog — wszystkie kolumny' AS [Raport];
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH AS Dlugosc, IS_NULLABLE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CallReminderLog' ORDER BY ORDINAL_POSITION;

SELECT N'20 — Driver / CarTrailer / DriverDetails / VehicleDetails — wszystkie kolumny' AS [Raport];
SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('Driver','CarTrailer','DriverDetails','VehicleDetails',
                     'DriverVehicleAssignment','VehicleServiceLog')
ORDER BY TABLE_NAME, ORDINAL_POSITION;

SELECT N'21 — Article (kartoteka towarów LibraNet) — wszystkie kolumny' AS [Raport];
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH AS Dlugosc, IS_NULLABLE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Article' ORDER BY ORDINAL_POSITION;

SELECT N'22 — TowarZdjecia (BLOBs zdjęć towarów) — kolumny' AS [Raport];
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH AS Dlugosc, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'TowarZdjecia';

SELECT N'23 — Haccp, QC_Normy, Wazenia, Out1A — kolumny' AS [Raport];
SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('Haccp','QC_Normy','Wazenia','Out1A','HaccpNormy',
                     'QualityControl','Temperatury','CCP')
ORDER BY TABLE_NAME, ORDINAL_POSITION;

/* ----------------------------------------------------------------------------
   3. PRÓBKA DANYCH (TOP 5)
---------------------------------------------------------------------------- */
SELECT N'24 — Sample HarmonogramDostaw (10 najbliższych potwierdzonych)' AS [Raport];
SELECT TOP 10 * FROM dbo.HarmonogramDostaw
WHERE Bufor = 'Potwierdzony'
ORDER BY DataOdbioru DESC;

SELECT N'25 — Sample Pozyskiwanie_Hodowcy (10 z najnowszym statusem)' AS [Raport];
SELECT TOP 10 * FROM dbo.Pozyskiwanie_Hodowcy ORDER BY 1 DESC;

SELECT N'26 — Sample listapartii (10 najnowszych)' AS [Raport];
SELECT TOP 10 * FROM dbo.listapartii ORDER BY CreateData DESC;

SELECT N'27 — Sample FarmerCalc (5 najnowszych)' AS [Raport];
SELECT TOP 5 * FROM dbo.FarmerCalc ORDER BY 1 DESC;

SELECT N'28 — Sample CallReminderLog (5 ostatnich)' AS [Raport];
SELECT TOP 5 * FROM dbo.CallReminderLog ORDER BY 1 DESC;

SELECT N'29 — Sample OdbiorcyCRM (5 wpisów)' AS [Raport];
SELECT TOP 5 * FROM dbo.OdbiorcyCRM ORDER BY 1 DESC;

SELECT N'30 — Sample Notatki (5 najnowszych)' AS [Raport];
SELECT TOP 5 * FROM dbo.Notatki ORDER BY 1 DESC;

/* ----------------------------------------------------------------------------
   4. UŻYTKOWNICY SYSTEMU
---------------------------------------------------------------------------- */
SELECT N'31 — Wszyscy operatorzy/userzy ZPSP' AS [Raport];
SELECT * FROM dbo.operators ORDER BY Name;

SELECT N'32 — Mapowanie userId → handlowiec' AS [Raport];
SELECT * FROM dbo.UserHandlowcy ORDER BY HandlowiecName;
SELECT * FROM dbo.MapowanieHandlowcow ORDER BY HandlowiecNazwa;
SELECT * FROM dbo.AvailableHandlowcy ORDER BY HandlowiecName;
SELECT * FROM dbo.HandlowcyCRM;

/* ----------------------------------------------------------------------------
   5. TRIGGERY + STORED PROCEDURY + FUNKCJE
---------------------------------------------------------------------------- */
SELECT N'33 — Wszystkie triggery (LibraNet)' AS [Raport];
SELECT t.name AS Trigger_Name,
       OBJECT_NAME(t.parent_id) AS NaTabeli,
       CASE WHEN t.is_disabled = 1 THEN N'WYŁ' ELSE N'AKT' END AS Stan,
       LEFT(OBJECT_DEFINITION(t.object_id), 500) AS DefSkrocona
FROM sys.triggers t
ORDER BY OBJECT_NAME(t.parent_id), t.name;

SELECT N'34 — Wszystkie stored procedury' AS [Raport];
SELECT p.name AS Procedura,
       LEFT(OBJECT_DEFINITION(p.object_id), 500) AS DefSkrocona
FROM sys.procedures p
ORDER BY p.name;

SELECT N'35 — Wszystkie funkcje user-defined' AS [Raport];
SELECT o.name AS Funkcja, o.type_desc AS Typ
FROM sys.objects o
WHERE o.type IN ('FN','IF','TF') AND o.is_ms_shipped = 0
ORDER BY o.name;

/* ----------------------------------------------------------------------------
   6. TYPYCZNE ANALIZY OBIORCÓW vs ZAMÓWIEŃ
---------------------------------------------------------------------------- */
SELECT N'36 — Liczba zamówień per status (era Mai)' AS [Raport];
SELECT Status, COUNT(*) AS Liczba
FROM dbo.ZamowieniaMieso
WHERE DataZamowienia >= '2025-10-01'
GROUP BY Status ORDER BY Liczba DESC;

SELECT N'37 — Klienci z najwięcej zamówieniami (era Mai)' AS [Raport];
SELECT TOP 30 z.KlientId, COUNT(*) AS Zamowien,
       CAST(SUM(zt.Ilosc) AS DECIMAL(18,1)) AS SumaKg
FROM dbo.ZamowieniaMieso z
LEFT JOIN dbo.ZamowieniaMiesoTowar zt ON zt.ZamowienieId = z.Id
WHERE z.DataZamowienia >= '2025-10-01'
GROUP BY z.KlientId
ORDER BY Zamowien DESC;

SELECT N'38 — Zamówienia per IdUser (era Mai)' AS [Raport];
SELECT z.IdUser,
       uh.HandlowiecName,
       mh.HandlowiecNazwa,
       COUNT(*) AS Zamowien
FROM dbo.ZamowieniaMieso z
LEFT JOIN dbo.UserHandlowcy uh ON uh.UserID = CAST(z.IdUser AS NVARCHAR(50))
LEFT JOIN dbo.MapowanieHandlowcow mh ON mh.UserId = CAST(z.IdUser AS NVARCHAR(50))
WHERE z.DataZamowienia >= '2025-10-01'
GROUP BY z.IdUser, uh.HandlowiecName, mh.HandlowiecNazwa
ORDER BY Zamowien DESC;

SELECT N'39 — Zakończono eksplorację LIBRANET' AS Info;
